/**
 * The anime look of the realistic body: reshaped at load — a bigger head
 * with bigger eyes, a smaller nose and a shorter, narrower jaw, longer
 * legs, a slimmer waist, leaner arms (and chest, for a man) — then drawn
 * in flat bands of light (cel shading) with a dark outline round it.
 *
 * The reshaping moves the model's vertices and bones together in its rest
 * pose (the T-pose it comes in) and then takes that as the new bind pose,
 * so the body still bends at its joints. The head grows by how much each
 * vertex follows the head bone, so the neck blends into it smoothly; the
 * eyes grow with the lids round them, across and up, not out.
 */

import * as THREE from 'three';
import { measureHead } from './head';
import type { Gender } from './looks';

interface Shape {
    /** How much bigger the head and the eyes, how much longer the legs. */
    head: number;
    eyes: number;
    legs: number;
    /** Shares taken off the waist's width, the nose, the jaw, the arms' and chest's thickness. */
    waist: number;
    nose: number;
    jaw: number;
    arms: number;
    chest: number;
}

const SHAPES: Record<Gender, Shape> = {
    male: {
        head: 1.12,
        eyes: 1.22,
        legs: 1.1,
        waist: 0.05,
        nose: 0.4,
        jaw: 0.08,
        arms: 0.14,
        chest: 0.07,
    },
    female: {
        head: 1.16,
        eyes: 1.28,
        legs: 1.12,
        waist: 0.1,
        nose: 0.45,
        jaw: 0.08,
        arms: 0.1,
        chest: 0,
    },
};

/** How tall (m) the waist narrowing reaches above and below the waist. */
const WAIST_SPREAD = 0.09;
/** Beyond this far out from the middle (m) are the arms, left as they are. */
const TRUNK_HALF = 0.2;
/** Outline thickness, in the model's own metres. */
const OUTLINE = 0.006;
const OUTLINE_COLOR = 0x241a1a;

const smoothstep = THREE.MathUtils.smoothstep;

/** Three bands of light: shade, half-light, full light. */
const gradient = new THREE.DataTexture(
    new Uint8Array([110, 190, 255]),
    3,
    1,
    THREE.RedFormat,
);
gradient.minFilter = THREE.NearestFilter;
gradient.magFilter = THREE.NearestFilter;
gradient.needsUpdate = true;

const outlineMaterial = new THREE.MeshBasicMaterial({
    color: OUTLINE_COLOR,
    side: THREE.BackSide,
});

// The shell is the mesh pushed out along its normals, seen from inside.
outlineMaterial.onBeforeCompile = (shader) => {
    shader.vertexShader = shader.vertexShader.replace(
        '#include <begin_vertex>',
        `#include <begin_vertex>\ntransformed += normal * ${OUTLINE.toFixed(4)};`,
    );
};

/** A cel-shaded copy of a material: its colour, texture and transparency. */
export function toonMaterial(source: THREE.Material): THREE.MeshToonMaterial {
    const standard = source as THREE.MeshStandardMaterial;

    return new THREE.MeshToonMaterial({
        name: source.name,
        color: standard.color?.clone() ?? new THREE.Color(0xffffff),
        map: standard.map ?? null,
        alphaMap: standard.alphaMap ?? null,
        gradientMap: gradient,
        transparent: source.transparent,
        opacity: source.opacity,
        alphaTest: source.alphaTest,
        depthWrite: source.depthWrite,
        side: source.side,
    });
}

/** A dark shell round a skinned mesh, bending with it. */
export function addOutline(mesh: THREE.SkinnedMesh): THREE.SkinnedMesh {
    const shell = new THREE.SkinnedMesh(mesh.geometry, outlineMaterial);

    shell.name = `${mesh.name}_outline`;
    shell.frustumCulled = false;
    shell.bind(mesh.skeleton, mesh.bindMatrix);
    mesh.parent!.add(shell);

    return shell;
}

/**
 * Reshapes a freshly cloned body (not yet put anywhere) in place. Its
 * geometries are copied first: the clone shares them with the loaded model.
 */
export function reshape(model: THREE.Object3D, gender: Gender): void {
    const shape = SHAPES[gender];
    const bones = new Map<string, THREE.Bone>();
    const meshes: THREE.SkinnedMesh[] = [];

    model.updateMatrixWorld(true);
    model.traverse((object) => {
        if (object instanceof THREE.Bone) {
            bones.set(object.name, object);
        } else if (object instanceof THREE.SkinnedMesh) {
            meshes.push(object);
        }
    });

    const at = (name: string) => {
        const bone = bones.get(name);

        if (!bone) {
            throw new Error(`The body has no ${name} bone`);
        }

        return bone.getWorldPosition(new THREE.Vector3());
    };
    const head = at('Head');
    const hip = (at('thigh_l').y + at('thigh_r').y) / 2;
    const ankle = (at('foot_l').y + at('foot_r').y) / 2;
    const waist = at('spine_01').lerp(at('spine_02'), 0.5);
    const chest = at('spine_03');
    const arms = (['l', 'r'] as const).map((side) => {
        const from = at(`upperarm_${side}`);
        const along = at(`hand_${side}`).sub(from);
        const length = along.length();

        return { from, along: along.normalize(), length };
    });
    const frame = measureHead(model);
    const headBones = new Set<THREE.Object3D>();
    const axis = new THREE.Vector3();

    bones.get('Head')!.traverse((bone) => headBones.add(bone));

    /**
     * Reshapes a vertex of the face, the arms and the chest (the bones
     * there stay put); `onHead`, `onArm` and `onChest` are how much it
     * follows those bones.
     */
    const sculpt = (
        point: THREE.Vector3,
        onHead: number,
        onArm: number,
        onChest: number,
    ): void => {
        if (frame && onHead > 0) {
            const s = frame.scale;
            const nose = frame.nose;
            const eyeY = (frame.eyes[0].y + frame.eyes[1].y) / 2;

            for (const eye of frame.eyes) {
                const reach = Math.min(
                    frame.eyeRadius * 2.3,
                    Math.abs(eye.x) * 0.95,
                );
                const grow =
                    (shape.eyes - 1) *
                    onHead *
                    (1 -
                        smoothstep(
                            point.distanceTo(eye),
                            frame.eyeRadius * 1.1,
                            reach,
                        ));

                point.x = eye.x + (point.x - eye.x) * (1 + grow);
                point.y = eye.y + (point.y - eye.y) * (1 + grow);
            }

            const across =
                1 - smoothstep(Math.abs(point.x), 0.008 * s, 0.022 * s);
            const along =
                smoothstep(point.y, nose.y - 0.02 * s, nose.y - 0.008 * s) *
                (1 - smoothstep(point.y, eyeY - 0.02 * s, eyeY - 0.005 * s));
            const base = nose.z - 0.025 * s;

            if (point.z > base) {
                point.z -=
                    (point.z - base) * shape.nose * across * along * onHead;
            }

            if (point.y < nose.y) {
                const jaw =
                    shape.jaw *
                    onHead *
                    smoothstep(nose.y - point.y, 0, 0.03 * s) *
                    smoothstep(
                        point.z,
                        frame.centre.z - 0.03 * s,
                        frame.centre.z + 0.02 * s,
                    );

                point.y = nose.y - (nose.y - point.y) * (1 - jaw);
                point.x *= 1 - jaw;
            }
        }

        if (onArm > 0) {
            const arm = arms[0].from.x * point.x > 0 ? arms[0] : arms[1];
            const along = THREE.MathUtils.clamp(
                axis.copy(point).sub(arm.from).dot(arm.along),
                0,
                arm.length,
            );

            axis.copy(arm.along).multiplyScalar(along).add(arm.from);
            point
                .sub(axis)
                .multiplyScalar(1 - shape.arms * onArm)
                .add(axis);
        }

        if (onChest > 0 && point.z > chest.z) {
            point.z =
                chest.z + (point.z - chest.z) * (1 - shape.chest * onChest);
        }
    };

    /** Moves a point of the rest pose; `onHead` is how much it follows the head. */
    const move = (point: THREE.Vector3, onHead: number): THREE.Vector3 => {
        const y = point.y;

        if (onHead > 0) {
            point
                .sub(head)
                .multiplyScalar(1 + (shape.head - 1) * onHead)
                .add(head);
        }

        const narrow =
            shape.waist *
            Math.exp(-(((y - waist.y) / WAIST_SPREAD) ** 2)) *
            (1 - smoothstep(Math.abs(point.x), TRUNK_HALF, TRUNK_HALF + 0.08));

        point.x *= 1 - narrow;
        point.z = waist.z + (point.z - waist.z) * (1 - narrow);

        if (y > hip) {
            point.y += (hip - ankle) * (shape.legs - 1);
        } else if (y > ankle) {
            point.y += (y - ankle) * (shape.legs - 1);
        }

        return point;
    };

    for (const mesh of meshes) {
        const geometry = mesh.geometry.clone();
        const position = geometry.getAttribute('position');
        const joints = geometry.getAttribute('skinIndex');
        const weights = geometry.getAttribute('skinWeight');
        const skeleton = mesh.skeleton.bones;
        const point = new THREE.Vector3();

        for (let i = 0; i < position.count; i++) {
            let onHead = 0;
            let onArm = 0;
            let onChest = 0;

            for (let k = 0; k < 4; k++) {
                const bone = skeleton[joints.getComponent(i, k)];
                const weight = weights.getComponent(i, k);

                if (headBones.has(bone)) {
                    onHead += weight;
                } else if (/^(upperarm|lowerarm)/.test(bone?.name ?? '')) {
                    onArm += weight;
                } else if (bone?.name === 'spine_03') {
                    onChest += weight;
                }
            }

            point
                .fromBufferAttribute(position, i)
                .applyMatrix4(mesh.bindMatrix);
            sculpt(point, onHead, onArm, onChest);
            move(point, onHead).applyMatrix4(mesh.bindMatrixInverse);
            position.setXYZ(i, point.x, point.y, point.z);
        }

        geometry.computeBoundingBox();
        geometry.computeBoundingSphere();
        mesh.geometry = geometry;
    }

    // Every bone where its joint now is, parents first; turns unchanged.
    const targets = new Map<THREE.Bone, THREE.Vector3>();

    for (const bone of bones.values()) {
        targets.set(
            bone,
            move(
                bone.getWorldPosition(new THREE.Vector3()),
                headBones.has(bone) ? 1 : 0,
            ),
        );
    }

    model.traverse((object) => {
        const target = object instanceof THREE.Bone && targets.get(object);

        if (target && object.parent) {
            object.parent.updateWorldMatrix(true, false);
            object.position.copy(object.parent.worldToLocal(target.clone()));
        }
    });

    model.updateMatrixWorld(true);

    for (const mesh of meshes) {
        // A new list: the clone's skeleton shares the loaded one's.
        mesh.skeleton.boneInverses = [];
        mesh.skeleton.calculateInverses();
    }
}
