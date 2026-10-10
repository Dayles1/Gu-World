/**
 * Pieces cut from the body's own skin — armour, a cap of hair, a beard:
 * the triangles a coverage (0…1 per vertex) takes in, lifted off the skin
 * along its normals, skinned to the same bones so they bend with it. The
 * edge fades out in the vertex alpha and the material cuts it at ½, so it
 * runs straight through the triangles rather than along them.
 */

import * as THREE from 'three';
import { toonMaterial } from './anime';
import type { BodyStyle } from './looks';

const cutOuts: Record<BodyStyle, WeakMap<THREE.Material, THREE.Material>> = {
    realistic: new WeakMap(),
    anime: new WeakMap(),
};

/**
 * The material, cut where the vertex alpha (how covered) is under ½ (and
 * cel-shaded for the anime style).
 */
export function cutOut(
    material: THREE.Material,
    style: BodyStyle,
): THREE.Material {
    let cut = cutOuts[style].get(material);

    if (!cut) {
        cut = style === 'anime' ? toonMaterial(material) : material.clone();
        cut.vertexColors = true;
        cut.alphaTest = material.alphaTest || 0.5;
        cut.side = THREE.DoubleSide;
        cutOuts[style].set(material, cut);
    }

    return cut;
}

/** How much a vertex of the body follows the bones that pass a test. */
export function boneShare(
    body: THREE.SkinnedMesh,
): (vertex: number, test: (name: string) => boolean) => number {
    const joints = body.geometry.getAttribute('skinIndex');
    const weights = body.geometry.getAttribute('skinWeight');
    const bones = body.skeleton.bones;

    return (vertex, test) => {
        let sum = 0;

        for (let k = 0; k < 4; k++) {
            if (test(bones[joints.getComponent(vertex, k)]?.name ?? '')) {
                sum += weights.getComponent(vertex, k);
            }
        }

        return sum;
    };
}

/**
 * The covered part of the body's skin, lifted `lift` (m) off it, as a
 * mesh bending with the body (not yet added anywhere).
 */
export function cutShell(
    body: THREE.SkinnedMesh,
    cover: Float32Array,
    lift: number,
    material: THREE.Material,
): THREE.SkinnedMesh {
    const geometry = body.geometry;
    const position = geometry.getAttribute('position');
    const normal = geometry.getAttribute('normal');
    const source = geometry.index!;
    const indices: number[] = [];

    for (let i = 0; i < source.count; i += 3) {
        const a = source.getX(i);
        const b = source.getX(i + 1);
        const c = source.getX(i + 2);

        if (Math.max(cover[a], cover[b], cover[c]) > 0.5) {
            indices.push(a, b, c);
        }
    }

    const lifted = new Float32Array(position.count * 3);
    const alpha = new Float32Array(position.count * 4).fill(1);

    for (let i = 0; i < position.count; i++) {
        lifted[i * 3] = position.getX(i) + normal.getX(i) * lift;
        lifted[i * 3 + 1] = position.getY(i) + normal.getY(i) * lift;
        lifted[i * 3 + 2] = position.getZ(i) + normal.getZ(i) * lift;
        alpha[i * 4 + 3] = cover[i];
    }

    const shell = new THREE.BufferGeometry();

    shell.setAttribute('position', new THREE.BufferAttribute(lifted, 3));
    shell.setAttribute('normal', normal);
    shell.setAttribute('uv', geometry.getAttribute('uv'));
    shell.setAttribute('color', new THREE.BufferAttribute(alpha, 4));
    shell.setAttribute('skinIndex', geometry.getAttribute('skinIndex'));
    shell.setAttribute('skinWeight', geometry.getAttribute('skinWeight'));
    shell.setIndex(indices);

    const piece = new THREE.SkinnedMesh(shell, material);

    piece.castShadow = true;
    piece.receiveShadow = true;
    piece.frustumCulled = false;
    piece.bind(body.skeleton, body.bindMatrix);

    return piece;
}
