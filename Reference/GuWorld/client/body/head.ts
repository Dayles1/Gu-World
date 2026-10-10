/**
 * Measures a body's head in its rest pose: how far it reaches, where the
 * eyes, brows, nose tip, mouth and chin are, and how much each vertex
 * follows the head. The anime reshaping and the hair and beards are
 * placed by it.
 */

import * as THREE from 'three';

/** Where the parts of a face are, in the model's rest pose. */
export interface HeadFrame {
    /** The middle of the head and how far it reaches. */
    centre: THREE.Vector3;
    top: number;
    back: number;
    front: number;
    halfWidth: number;
    /** Left (+x) and right eye centres, and an eye's radius. */
    eyes: [THREE.Vector3, THREE.Vector3];
    eyeRadius: number;
    /** Top of the eyebrows. */
    brow: number;
    nose: THREE.Vector3;
    mouth: number;
    chin: number;
    /** Size relative to the plain model's head: distances scale with it. */
    scale: number;
    /** Per body vertex: how much it follows the head. */
    onHead: Float32Array;
}

/** The plain model's head, top to chin (m). */
const HEAD_HEIGHT = 0.235;
/** The body mesh of a model: the skin the clothes, hair and beard are cut from. */
export function findBody(model: THREE.Object3D): THREE.SkinnedMesh | null {
    let body: THREE.SkinnedMesh | null = null;

    model.traverse((object) => {
        if (
            object instanceof THREE.SkinnedMesh &&
            (object.material as THREE.Material).name.startsWith('MI_Superhero')
        ) {
            body = object;
        }
    });

    return body;
}

/** The mesh of a model with this name, if it has one. */
export function named(model: THREE.Object3D, name: string): THREE.Mesh | null {
    let found: THREE.Mesh | null = null;

    model.traverse((object) => {
        if (object instanceof THREE.Mesh && object.name === name) {
            found = object;
        }
    });

    return found;
}

/** The skeleton's indices of the head bone and every bone under it. */
function headBones(body: THREE.SkinnedMesh): Set<number> {
    const indices = new Set<number>();

    body.skeleton.bones.forEach((bone, index) => {
        for (let up: THREE.Object3D | null = bone; up; up = up.parent) {
            if (up.name === 'Head') {
                indices.add(index);

                return;
            }
        }
    });

    return indices;
}

/** A mesh's vertex in the rest pose's space. */
export function restPoint(
    mesh: THREE.Mesh,
    index: number,
    out: THREE.Vector3,
): THREE.Vector3 {
    out.fromBufferAttribute(mesh.geometry.getAttribute('position'), index);

    return mesh instanceof THREE.SkinnedMesh
        ? out.applyMatrix4(mesh.bindMatrix)
        : out;
}

/** Measures the head of a model in its rest pose (null without a body). */
export function measureHead(model: THREE.Object3D): HeadFrame | null {
    const body = findBody(model);

    if (!body) {
        return null;
    }

    const position = body.geometry.getAttribute('position');
    const joints = body.geometry.getAttribute('skinIndex');
    const weights = body.geometry.getAttribute('skinWeight');
    const head = headBones(body);
    const onHead = new Float32Array(position.count);
    const box = new THREE.Box3();
    const p = new THREE.Vector3();

    for (let i = 0; i < position.count; i++) {
        for (let k = 0; k < 4; k++) {
            if (head.has(joints.getComponent(i, k))) {
                onHead[i] += weights.getComponent(i, k);
            }
        }

        if (onHead[i] > 0.9) {
            box.expandByPoint(restPoint(body, i, p));
        }
    }

    const centre = box.getCenter(new THREE.Vector3());
    const eyes: [THREE.Vector3, THREE.Vector3] = [
        new THREE.Vector3(),
        new THREE.Vector3(),
    ];
    let eyeRadius = 0.012;
    const eyeMesh = named(model, 'Eyes');

    if (eyeMesh) {
        const count = eyeMesh.geometry.getAttribute('position').count;
        const sides = [0, 0];

        for (let i = 0; i < count; i++) {
            restPoint(eyeMesh, i, p);
            const side = p.x >= 0 ? 0 : 1;
            eyes[side].add(p);
            sides[side]++;
        }

        eyes[0].divideScalar(Math.max(1, sides[0]));
        eyes[1].divideScalar(Math.max(1, sides[1]));
        eyeRadius = 0;

        for (let i = 0; i < count; i++) {
            restPoint(eyeMesh, i, p);
            eyeRadius = Math.max(
                eyeRadius,
                p.distanceTo(eyes[p.x >= 0 ? 0 : 1]),
            );
        }
    } else {
        eyes[0].set(0.032, centre.y, box.max.z - 0.02);
        eyes[1].set(-0.032, centre.y, box.max.z - 0.02);
    }

    const eyeY = (eyes[0].y + eyes[1].y) / 2;
    const brows = named(model, 'Eyebrows');
    let brow = eyeY + 0.022;

    if (brows) {
        const count = brows.geometry.getAttribute('position').count;
        brow = -Infinity;

        for (let i = 0; i < count; i++) {
            brow = Math.max(brow, restPoint(brows, i, p).y);
        }
    }

    // The nose tip: the front-most point of the midline below the eyes.
    const nose = new THREE.Vector3(0, eyeY - 0.03, box.max.z);
    let noseZ = -Infinity;

    for (let i = 0; i < position.count; i++) {
        restPoint(body, i, p);

        if (
            onHead[i] > 0.9 &&
            Math.abs(p.x) < 0.006 &&
            p.y < eyeY - 0.005 &&
            p.y > eyeY - 0.08 &&
            p.z > noseZ
        ) {
            noseZ = p.z;
            nose.copy(p);
        }
    }

    // The chin: the lowest point of the midline still at the front of the face.
    let chin = nose.y - 0.07;

    for (let i = 0; i < position.count; i++) {
        restPoint(body, i, p);

        if (
            onHead[i] > 0.5 &&
            Math.abs(p.x) < 0.006 &&
            p.y < nose.y &&
            p.z > nose.z - 0.05 &&
            p.y < chin
        ) {
            chin = p.y;
        }
    }

    return {
        centre,
        top: box.max.y,
        back: box.min.z,
        front: box.max.z,
        halfWidth: Math.max(-box.min.x, box.max.x),
        eyes,
        eyeRadius,
        brow,
        nose,
        mouth: nose.y - 0.4 * (nose.y - chin),
        chin,
        scale: (box.max.y - chin) / HEAD_HEIGHT,
        onHead,
    };
}
