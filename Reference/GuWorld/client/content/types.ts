/**
 * What a location is made of, beyond its world settings (world/config.ts):
 * its ground and the things standing on it. Things are asked for by area
 * (`objectsIn`) as the world loads around the hero; each has a stable id
 * of its own, and the same thing comes back with the same id whatever
 * area it is asked for in — so ids never depend on how the world is cut
 * into chunks. A thing belongs to the area its anchor point (x, z) is in.
 */

import type { Area } from '../world/chunks';
import type { Ground } from '../world/ground';

/** A solid box standing on the ground, centred on (x, z). */
export interface BlockObject {
    kind: 'block';
    id: string;
    x: number;
    z: number;
    width: number;
    depth: number;
    height: number;
}

/** A rounded stone sitting on the ground at (x, z). */
export interface StoneObject {
    kind: 'stone';
    id: string;
    x: number;
    z: number;
    radius: number;
}

export type ContentObject = BlockObject | StoneObject;

export interface LocationContent {
    ground: Ground;
    /** The things whose anchor is in the area: [minX, maxX) × [minZ, maxZ). */
    objectsIn(area: Area): ContentObject[];
}

/** Whether a point lies in an area (min inclusive, max exclusive). */
export function anchoredIn(area: Area, x: number, z: number): boolean {
    return x >= area.minX && x < area.maxX && z >= area.minZ && z < area.maxZ;
}
