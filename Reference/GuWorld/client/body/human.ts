/**
 * A realistic body for the player: a skinned, textured human — Quaternius'
 * Universal Base Characters (CC0), a man or a woman in plain underwear,
 * with hair — worn over the Mannequin's joints; in the anime style it is
 * reshaped and cel-shaded (see anime). The hero's look picks the hair
 * (see hair), its colour, a beard and the eyes' colour; the skin's
 * colours are made a little richer (see paint). Copied from the Sandbox;
 * its armour and held items are left out.
 *
 * The Mannequin keeps doing all the moving — walk cycle, leg IK, crawl,
 * swim, blows — on its own (now invisible) joints; every frame each bone
 * copies the turn of the joint that drives it. Rotations are compared in
 * the figure's own space, so the bones' local axes don't matter: at load
 * the model is brought into the Mannequin's rest pose (arms and legs
 * hanging straight down), and from then on a bone's orientation is its
 * joint's orientation times that rest one.
 *
 * Some bones take only a share of their joint's turn, which the real
 * body spreads out: the bend of the back over three vertebrae, a turn of
 * the head partly in the neck, a raised arm partly in the collarbone.
 * The fingers rest loosely curled.
 */

import * as THREE from 'three';
import type { GLTF } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { clone } from 'three/examples/jsm/utils/SkeletonUtils.js';
import femaleUrl from '../assets/human-female.glb?url';
import maleUrl from '../assets/human-male.glb?url';
import { addOutline, reshape, toonMaterial } from './anime';
import { borrowHair, growHair, MODEL_HAIR } from './hair';
import { measureHead } from './head';
import { EYE_COLORS, HAIR_COLORS } from './looks';
import type { Appearance, BodyStyle, Gender, HairStyle } from './looks';
import { irisColor, saturate } from './paint';

/** The Mannequin's joints a body follows. */
export interface Rig {
    hips: THREE.Object3D;
    spine: THREE.Object3D;
    neck: THREE.Object3D;
    head: THREE.Object3D;
    shoulderL: THREE.Object3D;
    shoulderR: THREE.Object3D;
    elbowL: THREE.Object3D;
    elbowR: THREE.Object3D;
    hipL: THREE.Object3D;
    hipR: THREE.Object3D;
    kneeL: THREE.Object3D;
    kneeR: THREE.Object3D;
    ankleL: THREE.Object3D;
    ankleR: THREE.Object3D;
}

/**
 * Bone → the joint that turns it; `share` < 1 takes only that part of the
 * joint's own turn (relative to the joint's parent).
 */
const DRIVERS: Record<string, { joint: keyof Rig; share?: number }> = {
    pelvis: { joint: 'hips' },
    spine_01: { joint: 'spine', share: 0.35 },
    spine_02: { joint: 'spine', share: 0.7 },
    spine_03: { joint: 'spine' },
    neck_01: { joint: 'head', share: 0.45 },
    Head: { joint: 'head' },
    clavicle_l: { joint: 'shoulderL', share: 0.2 },
    upperarm_l: { joint: 'shoulderL' },
    lowerarm_l: { joint: 'elbowL' },
    hand_l: { joint: 'elbowL' },
    clavicle_r: { joint: 'shoulderR', share: 0.2 },
    upperarm_r: { joint: 'shoulderR' },
    lowerarm_r: { joint: 'elbowR' },
    hand_r: { joint: 'elbowR' },
    thigh_l: { joint: 'hipL' },
    calf_l: { joint: 'kneeL' },
    foot_l: { joint: 'ankleL' },
    thigh_r: { joint: 'hipR' },
    calf_r: { joint: 'kneeR' },
    foot_r: { joint: 'ankleR' },
};

/** Arms hang this far out from straight down (radians) at rest. */
const ARM_OUT = 0.1;
/** Finger curl per joint (radians), relaxed. */
const CURL_REST = 0.3;
const FINGERS = ['index', 'middle', 'ring', 'pinky'];
/** How much of the curl each knuckle takes, from the palm out. */
const KNUCKLES = [0.8, 1, 0.75];

/** How much richer the skin's colours are drawn, by style. */
const SATURATION: Record<BodyStyle, number> = { realistic: 1.15, anime: 1.35 };

/** Texture filtering at a slant (the GPU's own limit caps it). */
const ANISOTROPY = 8;

/** The hairstyle each model comes with. */
const OWN_HAIR: Record<Gender, HairStyle> = { male: 'parted', female: 'buns' };

/** Who a body is: gender, how it is drawn and the hero's look. */
export interface BodyLook {
    gender: Gender;
    style: BodyStyle;
    appearance: Appearance;
}

/** The other model to take a hairstyle from, when this one lacks it. */
export function hairDonor(gender: Gender, hair: HairStyle): Gender | null {
    const other: Gender = gender === 'male' ? 'female' : 'male';

    return MODEL_HAIR[hair] && OWN_HAIR[gender] !== hair ? other : null;
}

/** Keeps a material's textures sharp when seen at a slant. */
function sharpen(material: THREE.Material): void {
    const textured = material as THREE.MeshStandardMaterial;

    for (const texture of [
        textured.map,
        textured.normalMap,
        textured.roughnessMap,
    ]) {
        if (texture && texture.anisotropy < ANISOTROPY) {
            texture.anisotropy = ANISOTROPY;
            texture.needsUpdate = true;
        }
    }
}

const URLS: Record<Gender, string> = { male: maleUrl, female: femaleUrl };
const loading = new Map<Gender, Promise<GLTF>>();

/** Loads (once) the body for a gender. */
export function loadHuman(gender: Gender): Promise<GLTF> {
    let promise = loading.get(gender);

    if (!promise) {
        promise = new GLTFLoader().loadAsync(URLS[gender]);
        promise.catch(() => loading.delete(gender));
        loading.set(gender, promise);
    }

    return promise;
}

interface Driven {
    bone: THREE.Bone;
    joint: THREE.Object3D;
    share: number;
    /** Orientation at rest, in figure space. */
    rest: THREE.Quaternion;
    parent: THREE.Bone | null;
}

interface Finger {
    bone: THREE.Bone;
    rest: THREE.Quaternion;
    /** The curl axis in the bone's own space, and its share of the curl. */
    axis: THREE.Vector3;
    amount: number;
    side: 'left' | 'right';
}

export class Human {
    /** Holds the model: scaled to the Mannequin and turned to face +Z. */
    readonly holder = new THREE.Group();

    private bones = new Map<string, THREE.Bone>();
    private driven: Driven[] = [];
    private fingers: Finger[] = [];
    /** The pelvis' parent in figure space (it never moves). */
    private pelvisParent = new THREE.Quaternion();
    private pelvisRest = new THREE.Vector3();
    private target = new Map<THREE.Bone, THREE.Quaternion>();
    private rootInverse = new THREE.Quaternion();
    private q = new THREE.Quaternion();
    private share = new THREE.Quaternion();
    private v = new THREE.Vector3();
    /** The skin, which the hair is grown on. */
    private body: THREE.SkinnedMesh | null = null;

    /**
     * @param donor the other model, when the hairstyle is its (see hairDonor)
     * @param hipJoint height of the Mannequin's hip joints at rest
     * @param hipsDriverRest the Mannequin's hips joint at rest, in figure space
     */
    constructor(
        gltf: GLTF,
        donor: GLTF | null,
        look: BodyLook,
        private root: THREE.Object3D,
        private rig: Rig,
        hipJoint: number,
        private hipsDriverRest: THREE.Vector3,
    ) {
        const { gender, style, appearance } = look;
        const model = clone(gltf.scene);
        const unworn: THREE.Object3D[] = [];
        const paint = (material: THREE.MeshStandardMaterial) =>
            style === 'anime' ? toonMaterial(material) : material.clone();
        const tint = (material: THREE.MeshStandardMaterial) => {
            const painted = paint(material);
            painted.color.setHex(HAIR_COLORS[appearance.hair_color]);
            sharpen(painted);

            return painted;
        };

        if (style === 'anime') {
            reshape(model, gender);
        }

        model.traverse((object) => {
            if (object instanceof THREE.Bone) {
                this.bones.set(object.name, object);
            }

            if (object instanceof THREE.Mesh) {
                object.castShadow = true;
                object.receiveShadow = true;
                // Skinned meshes move away from their bounds.
                object.frustumCulled = false;

                const source = object.material as THREE.MeshStandardMaterial;
                // The hair texture is grey, to be tinted.
                const material = source.name.startsWith('MI_Hair')
                    ? tint(source)
                    : paint(source);

                if (material.map && source.name === 'MI_Eyes') {
                    material.map = irisColor(
                        material.map,
                        EYE_COLORS[appearance.eyes],
                    );
                }

                if (material.map && source.name.startsWith('MI_Superhero')) {
                    material.map = saturate(material.map, SATURATION[style]);
                }

                sharpen(material);
                object.material = material;

                if (
                    object.name.startsWith('Hair') &&
                    MODEL_HAIR[appearance.hair] !== object.name
                ) {
                    unworn.push(object);
                }

                if (
                    object instanceof THREE.SkinnedMesh &&
                    material.name.startsWith('MI_Superhero')
                ) {
                    this.body = object;
                }
            }
        });

        for (const object of unworn) {
            object.removeFromParent();
        }

        const frame = measureHead(model);
        const body = this.body;

        if (body && frame) {
            const borrowed = MODEL_HAIR[appearance.hair];
            const theirs =
                borrowed && donor ? this.donorHair(donor, borrowed) : null;

            if (borrowed && theirs) {
                const hair = borrowHair(
                    body,
                    frame,
                    donor!.scene,
                    borrowed,
                    tint(theirs),
                );

                if (hair) {
                    body.parent!.add(hair);
                }
            }

            growHair(body, frame, appearance, style);
        }

        if (body && style === 'anime') {
            addOutline(body);
        }

        this.holder.add(model);
        root.add(this.holder);
        this.fit(hipJoint);
        this.calibrate();

        model.traverse((object) => {
            const drive =
                object instanceof THREE.Bone ? DRIVERS[object.name] : null;

            if (!drive) {
                return;
            }

            const bone = object as THREE.Bone;
            const parent =
                bone.parent instanceof THREE.Bone && DRIVERS[bone.parent.name]
                    ? bone.parent
                    : null;

            this.driven.push({
                bone,
                joint: rig[drive.joint],
                share: drive.share ?? 1,
                rest: this.inFigure(bone, new THREE.Quaternion()),
                parent,
            });
            this.target.set(bone, new THREE.Quaternion());
        });

        const pelvis = this.bone('pelvis');

        this.inFigure(pelvis.parent!, this.pelvisParent);
        this.positionInFigure(pelvis, this.pelvisRest);
        this.findFingers();
    }

    /** Copies the joints' turns onto the bones. Call after the joints move. */
    update(): void {
        this.root.updateWorldMatrix(true, false);
        this.root.getWorldQuaternion(this.rootInverse).invert();

        for (const { bone, joint, share, rest, parent } of this.driven) {
            const target = this.target.get(bone)!;

            if (share < 1) {
                // The joint's parent turned, plus a share of the joint's own turn.
                joint.parent!.getWorldQuaternion(target);
                target
                    .premultiply(this.rootInverse)
                    .multiply(
                        this.share.identity().slerp(joint.quaternion, share),
                    );
            } else {
                joint.getWorldQuaternion(target).premultiply(this.rootInverse);
            }

            target.multiply(rest);

            const parentTarget = parent
                ? this.target.get(parent)!
                : this.pelvisParent;

            bone.quaternion.copy(parentTarget).invert().multiply(target);
        }

        for (const { bone, rest, axis, amount } of this.fingers) {
            bone.quaternion
                .copy(rest)
                .multiply(this.q.setFromAxisAngle(axis, CURL_REST * amount));
        }

        // The hips also carry the body's shift (crouching, sitting, lying).
        const pelvis = this.bone('pelvis');
        const shift = this.positionInFigure(this.rig.hips, this.v)
            .sub(this.hipsDriverRest)
            .add(this.pelvisRest);

        pelvis.position.copy(
            pelvis.parent!.worldToLocal(this.root.localToWorld(shift)),
        );
        this.holder.updateMatrixWorld(true);
    }

    /** Takes the body out of the figure and frees its own materials (the loaded model's geometry and textures are shared). */
    dispose(): void {
        this.holder.removeFromParent();
        this.holder.traverse((object) => {
            if (object instanceof THREE.Mesh) {
                for (const material of [object.material].flat()) {
                    material.dispose();
                }
            }
        });
    }

    /** The other model's material for its hair. */
    private donorHair(
        donor: GLTF,
        name: string,
    ): THREE.MeshStandardMaterial | null {
        let material: THREE.MeshStandardMaterial | null = null;

        donor.scene.traverse((object) => {
            if (object instanceof THREE.Mesh && object.name === name) {
                material = object.material as THREE.MeshStandardMaterial;
            }
        });

        return material;
    }

    private bone(name: string): THREE.Bone {
        const bone = this.bones.get(name);

        if (!bone) {
            throw new Error(`The body has no ${name} bone`);
        }

        return bone;
    }

    private inFigure(
        object: THREE.Object3D,
        out: THREE.Quaternion,
    ): THREE.Quaternion {
        this.root.getWorldQuaternion(this.q).invert();

        return object.getWorldQuaternion(out).premultiply(this.q);
    }

    private positionInFigure(
        object: THREE.Object3D,
        out: THREE.Vector3,
    ): THREE.Vector3 {
        return this.root.worldToLocal(object.getWorldPosition(out));
    }

    /** Scales the model so its hip joints are the Mannequin's, feet on the ground, facing +Z. */
    private fit(hipJoint: number): void {
        const at = (name: string) =>
            this.positionInFigure(this.bone(name), new THREE.Vector3());

        this.root.updateMatrixWorld(true);

        if (at('upperarm_l').x < at('upperarm_r').x) {
            this.holder.rotation.y = Math.PI;
            this.root.updateMatrixWorld(true);
        }

        const sole = () => Math.min(at('ball_leaf_l').y, at('ball_leaf_r').y);
        const hips = (at('thigh_l').y + at('thigh_r').y) / 2;
        const scale = hipJoint / Math.max(0.1, hips - sole());

        this.holder.scale.setScalar(scale);
        this.root.updateMatrixWorld(true);

        const centre = at('pelvis');

        this.holder.position.set(-centre.x, -sole(), -centre.z);
        this.root.updateMatrixWorld(true);
    }

    /** Hangs the arms and legs straight down: the Mannequin's rest pose. */
    private calibrate(): void {
        const down = (side: number) =>
            new THREE.Vector3(side * Math.sin(ARM_OUT), -Math.cos(ARM_OUT), 0);
        const straight = new THREE.Vector3(0, -1, 0);
        const aims: [string, string, THREE.Vector3][] = [
            ['upperarm_l', 'lowerarm_l', down(1)],
            ['lowerarm_l', 'hand_l', down(1)],
            ['hand_l', 'middle_01_l', down(1)],
            ['upperarm_r', 'lowerarm_r', down(-1)],
            ['lowerarm_r', 'hand_r', down(-1)],
            ['hand_r', 'middle_01_r', down(-1)],
            ['thigh_l', 'calf_l', straight],
            ['calf_l', 'foot_l', straight],
            ['thigh_r', 'calf_r', straight],
            ['calf_r', 'foot_r', straight],
        ];
        const from = new THREE.Vector3();
        const to = new THREE.Vector3();
        const turn = new THREE.Quaternion();
        const current = new THREE.Quaternion();
        const parent = new THREE.Quaternion();

        for (const [name, childName, direction] of aims) {
            const bone = this.bones.get(name);
            const child = this.bones.get(childName);

            if (!bone || !child) {
                continue;
            }

            this.root.updateMatrixWorld(true);
            this.positionInFigure(bone, from);
            this.positionInFigure(child, to);
            turn.setFromUnitVectors(to.sub(from).normalize(), direction);
            this.inFigure(bone, current).premultiply(turn);
            this.inFigure(bone.parent!, parent);
            bone.quaternion.copy(parent.invert().multiply(current));
        }

        this.root.updateMatrixWorld(true);
    }

    /**
     * Works out, per hand, which way the fingers curl: toward the palm,
     * the side the thumb is on.
     */
    private findFingers(): void {
        for (const [side, s] of [
            ['left', 'l'],
            ['right', 'r'],
        ] as const) {
            const at = (name: string) =>
                this.positionInFigure(this.bone(name), new THREE.Vector3());
            const wrist = at(`hand_${s}`);
            const along = at(`middle_01_${s}`).sub(wrist).normalize();
            const across = at(`pinky_01_${s}`).sub(at(`index_01_${s}`));
            const palm = new THREE.Vector3()
                .crossVectors(along, across)
                .normalize();

            if (palm.dot(at(`thumb_03_${s}`).sub(wrist)) < 0) {
                palm.negate();
            }

            const axis = new THREE.Vector3()
                .crossVectors(along, palm)
                .normalize();
            const add = (name: string, amount: number) => {
                const bone = this.bones.get(name);

                if (!bone) {
                    return;
                }

                const world = this.inFigure(bone, new THREE.Quaternion());

                this.fingers.push({
                    bone,
                    rest: bone.quaternion.clone(),
                    axis: axis.clone().applyQuaternion(world.invert()),
                    amount,
                    side,
                });
            };

            for (const finger of FINGERS) {
                KNUCKLES.forEach((amount, k) =>
                    add(`${finger}_0${k + 1}_${s}`, amount),
                );
            }

            add(`thumb_02_${s}`, 0.35);
            add(`thumb_03_${s}`, 0.45);
        }
    }
}
