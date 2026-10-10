/**
 * The proving ground: a field to try the engine on, with what the body's
 * physics must cope with, and enough things spread over it to load and
 * let go area by area. It is a test location for the engine, not part of
 * the world of Gu.
 *
 * - a gentle mound (~20° at its steepest) to walk up and down;
 * - a steep mound (~58°) to slide off — steeper than one can stand on;
 * - five steps up to a platform, a crate to climb onto, a wall too high
 *   to climb (placed by hand, near the spawn);
 * - stones scattered over the whole field: one chance per cell of a
 *   fixed 16 m grid, seeded by the cell — so a stone's id names its cell
 *   ("stone:test_grounds:g3_-2"), never a chunk, and the field is the
 *   same every time.
 *
 * Its size and where a hero appears are world settings
 * (config/gu_world.php), not here.
 */

import type { Area } from '../world/chunks';
import { heightField } from '../world/ground';
import { anchoredIn } from './types';
import type { BlockObject, ContentObject, LocationContent } from './types';

/** A smooth round hill: `height` tall, its slope set by `radius`. */
function mound(
    x: number,
    z: number,
    centreX: number,
    centreZ: number,
    height: number,
    radius: number,
): number {
    const distance = (x - centreX) ** 2 + (z - centreZ) ** 2;

    return height * Math.exp(-distance / (radius * radius));
}

export const GENTLE_MOUND = { x: 24, z: -24, height: 6, radius: 14 };
export const STEEP_MOUND = { x: -24, z: -24, height: 8, radius: 4.2 };

const STEP_RISE = 0.3;
/** The scattering grid for stones, metres (fixed: ids name its cells). */
const STONE_CELL = 16;
const STONE_CHANCE = 0.35;
/** No stones this close to the spawn and the hand-placed blocks. */
const CLEAR_RADIUS = 22;

export const BLOCKS: BlockObject[] = [
    ...[1, 2, 3, 4, 5].map((n): BlockObject => ({
        kind: 'block',
        id: `block:test_grounds:step_${n}`,
        x: 6 + n * 0.6,
        z: -4,
        width: 0.6,
        depth: 2.4,
        height: n * STEP_RISE,
    })),
    {
        kind: 'block',
        id: 'block:test_grounds:platform',
        x: 11.4,
        z: -4,
        width: 3,
        depth: 3,
        height: 5 * STEP_RISE,
    },
    {
        kind: 'block',
        id: 'block:test_grounds:crate',
        x: -6,
        z: -6,
        width: 1.4,
        depth: 1.4,
        height: 1.2,
    },
    {
        kind: 'block',
        id: 'block:test_grounds:wall',
        x: -12,
        z: 4,
        width: 0.6,
        depth: 8,
        height: 3.5,
    },
];

/** A number in [0, 1) from a cell and a salt — the same every time. */
function hash(gx: number, gz: number, salt: number): number {
    let h = Math.imul(gx, 374761393) ^ Math.imul(gz, 668265263) ^ salt;
    h = Math.imul(h ^ (h >>> 13), 1274126177);
    h ^= h >>> 16;

    return (h >>> 0) / 4294967296;
}

/** The stone of a cell of the scattering grid, if it has one. */
function stoneOf(gx: number, gz: number): ContentObject | null {
    if (hash(gx, gz, 11) >= STONE_CHANCE) {
        return null;
    }

    const x = (gx + 0.1 + hash(gx, gz, 23) * 0.8) * STONE_CELL;
    const z = (gz + 0.1 + hash(gx, gz, 37) * 0.8) * STONE_CELL;

    if (Math.hypot(x, z) < CLEAR_RADIUS) {
        return null;
    }

    return {
        kind: 'stone',
        id: `stone:test_grounds:g${gx}_${gz}`,
        x,
        z,
        radius: 0.5 + hash(gx, gz, 53) * 0.9,
    };
}

function objectsIn(area: Area): ContentObject[] {
    const found: ContentObject[] = BLOCKS.filter((block) =>
        anchoredIn(area, block.x, block.z),
    );

    for (
        let gx = Math.floor(area.minX / STONE_CELL);
        gx * STONE_CELL < area.maxX;
        gx++
    ) {
        for (
            let gz = Math.floor(area.minZ / STONE_CELL);
            gz * STONE_CELL < area.maxZ;
            gz++
        ) {
            const stone = stoneOf(gx, gz);

            if (stone && anchoredIn(area, stone.x, stone.z)) {
                found.push(stone);
            }
        }
    }

    return found;
}

export const testGrounds: LocationContent = {
    ground: heightField(
        (x, z) =>
            mound(
                x,
                z,
                GENTLE_MOUND.x,
                GENTLE_MOUND.z,
                GENTLE_MOUND.height,
                GENTLE_MOUND.radius,
            ) +
            mound(
                x,
                z,
                STEEP_MOUND.x,
                STEEP_MOUND.z,
                STEEP_MOUND.height,
                STEEP_MOUND.radius,
            ),
    ),
    objectsIn,
};
