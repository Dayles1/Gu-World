/**
 * The ground of a location as physics, the camera and the body's feet
 * see it: a height for every point and which way the surface faces. Each
 * location brings its own (content/); nothing in the engine assumes a
 * size or shape of land.
 */

import type * as THREE from 'three';

export interface Ground {
    heightAt(x: number, z: number): number;
    /** The surface normal at (x, z), written into `out`. */
    normalAt(x: number, z: number, out: THREE.Vector3): THREE.Vector3;
    /** The water's surface; -Infinity where there is no water. */
    readonly waterLevel: number;
}

/** A ground from a height function, its normals from nearby heights. */
export function heightField(
    height: (x: number, z: number) => number,
    waterLevel = -Infinity,
): Ground {
    const e = 0.2;

    return {
        heightAt: height,
        normalAt(x, z, out) {
            const dx = height(x + e, z) - height(x - e, z);
            const dz = height(x, z + e) - height(x, z - e);

            return out.set(-dx, 2 * e, -dz).normalize();
        },
        waterLevel,
    };
}
