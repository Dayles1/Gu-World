/** The solid things of a location's content, as colliders. */

import * as THREE from 'three';
import type { ContentObject } from '../content/types';
import type { Ground } from '../world/ground';
import type { Collider } from './colliders';

/** How deep a stone sits in the ground, as a share of its size. */
export const STONE_SINK = 0.45;

/** A thing's collider: a block's box from below the ground to its top; a stone's sphere, half sunk. */
export function colliderFor(object: ContentObject, ground: Ground): Collider {
    const base = ground.heightAt(object.x, object.z);

    if (object.kind === 'stone') {
        return {
            kind: 'sphere',
            center: new THREE.Vector3(
                object.x,
                base + object.radius * (1 - STONE_SINK * 2),
                object.z,
            ),
            radius: object.radius,
        };
    }

    return {
        kind: 'box',
        min: new THREE.Vector3(
            object.x - object.width / 2,
            base - 1,
            object.z - object.depth / 2,
        ),
        max: new THREE.Vector3(
            object.x + object.width / 2,
            base + object.height,
            object.z + object.depth / 2,
        ),
    };
}
