/**
 * The chunk grid: a location cut into square areas of `size` metres, the
 * unit in which the world is loaded, simulated, drawn and let go. Chunk
 * (cx, cz) covers [cx·size, (cx+1)·size) on X and the same on Z, so the
 * grid is anchored at the world's origin and does not move with the
 * location's bounds. Pure geometry: no state, no three.js.
 *
 * Nothing in the world is named after a chunk: things keep their own
 * stable ids (entities/registry.ts) and only land in whichever chunk
 * their anchor point falls in, so changing the chunk size changes no id.
 */

import type { Bounds } from './space';

export interface ChunkCoord {
    cx: number;
    cz: number;
}

/** A rectangle on the ground, min inclusive, max exclusive. */
export interface Area {
    minX: number;
    maxX: number;
    minZ: number;
    maxZ: number;
}

export type ChunkKey = string;

export function chunkKey(cx: number, cz: number): ChunkKey {
    return `${cx}:${cz}`;
}

export function parseChunkKey(key: ChunkKey): ChunkCoord {
    const [cx, cz] = key.split(':').map(Number);

    return { cx, cz };
}

/** The chunk a point is in. */
export function chunkAt(x: number, z: number, size: number): ChunkCoord {
    return { cx: Math.floor(x / size), cz: Math.floor(z / size) };
}

export function chunkArea({ cx, cz }: ChunkCoord, size: number): Area {
    return {
        minX: cx * size,
        maxX: (cx + 1) * size,
        minZ: cz * size,
        maxZ: (cz + 1) * size,
    };
}

/** From a point to the nearest point of a chunk, on the ground (0 inside it). */
export function distanceToChunk(
    x: number,
    z: number,
    chunk: ChunkCoord,
    size: number,
): number {
    const area = chunkArea(chunk, size);
    const dx = Math.max(area.minX - x, 0, x - area.maxX);
    const dz = Math.max(area.minZ - z, 0, z - area.maxZ);

    return Math.hypot(dx, dz);
}

/** Whether a chunk overlaps a location's bounds at all. */
export function chunkInBounds(
    chunk: ChunkCoord,
    size: number,
    bounds: Bounds,
): boolean {
    const area = chunkArea(chunk, size);

    return (
        area.maxX > bounds.minX &&
        area.minX < bounds.maxX &&
        area.maxZ > bounds.minZ &&
        area.minZ < bounds.maxZ
    );
}

/** Every chunk of the location within `radius` of (x, z), nearest first. */
export function chunksWithin(
    x: number,
    z: number,
    radius: number,
    size: number,
    bounds: Bounds,
): { chunk: ChunkCoord; distance: number }[] {
    const from = chunkAt(x - radius, z - radius, size);
    const to = chunkAt(x + radius, z + radius, size);
    const found: { chunk: ChunkCoord; distance: number }[] = [];

    for (let cx = from.cx; cx <= to.cx; cx++) {
        for (let cz = from.cz; cz <= to.cz; cz++) {
            const chunk = { cx, cz };
            const distance = distanceToChunk(x, z, chunk, size);

            if (distance <= radius && chunkInBounds(chunk, size, bounds)) {
                found.push({ chunk, distance });
            }
        }
    }

    return found.sort((a, b) => a.distance - b.distance);
}
