/**
 * The world's settings, as the server hands them to the page
 * (config/gu_world.php `world`, the same numbers the server checks saves
 * against): the engine's limits, the locations — each with the box it
 * allows and the spot a hero appears at — and where a new game starts.
 *
 * The world grows by adding locations or widening them here, not by
 * changing the engine. Everything is checked on the way in; a broken
 * configuration stops the game with every problem listed rather than
 * running on half-trusted numbers.
 */

import { inside, isFiniteNumber, isPose } from './space';
import type { Bounds, Pose } from './space';

export type LocationId = string;

/** A location's id: lower-case letters, digits and underscores. */
export const LOCATION_ID = /^[a-z][a-z0-9_]{0,63}$/;

/**
 * The farthest any point may be from the world's centre on X or Z, m. It
 * is what the collider grid can hold (8 m cells, 8192 across); the
 * planned region (10–20 km across — a working assumption, not canon) is
 * well within it.
 */
export const MAX_HALF_EXTENT = 32_768;

export interface WorldLimits {
    /** The farthest from the centre on X or Z, m. */
    halfExtent: number;
    minY: number;
    maxY: number;
}

export interface LocationConfig {
    id: LocationId;
    bounds: Bounds;
    spawn: Pose;
}

/** How the world is loaded around the hero (see world/chunk-manager.ts), metres. */
export interface ChunkSettings {
    /** A chunk's side. */
    size: number;
    /** Things within this are simulated. */
    simulationRadius: number;
    /** Things within this are drawn. */
    visualRadius: number;
    /** How much farther than a radius a chunk is kept before being let go, so its edge does not flicker. */
    margin: number;
}

export interface WorldConfig {
    limits: WorldLimits;
    chunks: ChunkSettings;
    start: LocationId;
    locations: Map<LocationId, LocationConfig>;
}

/** The chunk sizes the engine was measured with (see the stage 3 report). */
export const CHUNK_SIZES = [32, 64, 128];

export class WorldConfigError extends Error {
    constructor(readonly problems: string[]) {
        super(`The world's settings are broken: ${problems.join('; ')}`);
    }
}

type Raw = Record<string, unknown>;

const isObject = (value: unknown): value is Raw =>
    typeof value === 'object' && value !== null && !Array.isArray(value);

export function parseWorldConfig(raw: unknown): WorldConfig {
    const problems: string[] = [];

    if (!isObject(raw)) {
        throw new WorldConfigError(['the settings are not an object']);
    }

    const limits = parseLimits(raw.limits, problems);
    const chunks = parseChunks(raw.chunks, problems);
    const locations = new Map<LocationId, LocationConfig>();

    if (!isObject(raw.locations)) {
        problems.push('there are no locations');
    } else {
        for (const [id, entry] of Object.entries(raw.locations)) {
            const location = parseLocation(id, entry, limits, problems);

            if (location) {
                locations.set(id, location);
            }
        }
    }

    const start = typeof raw.start === 'string' ? raw.start : '';

    if (!locations.has(start)) {
        problems.push(`the starting location "${start}" is not one of them`);
    }

    if (problems.length || !limits || !chunks) {
        throw new WorldConfigError(problems);
    }

    return { limits, chunks, start, locations };
}

function parseChunks(raw: unknown, problems: string[]): ChunkSettings | null {
    if (!isObject(raw)) {
        problems.push('the chunk settings are missing');

        return null;
    }

    const {
        size,
        simulation_radius: simulationRadius,
        visual_radius: visualRadius,
        margin,
    } = raw;

    if (
        !isFiniteNumber(size) ||
        size < 8 ||
        size > 1024 ||
        !isFiniteNumber(simulationRadius) ||
        !isFiniteNumber(visualRadius) ||
        !isFiniteNumber(margin) ||
        simulationRadius <= 0 ||
        visualRadius <= 0 ||
        Math.max(simulationRadius, visualRadius) > 4096 ||
        margin < 0 ||
        margin > size
    ) {
        problems.push(
            'the chunk settings must be a size of 8–1024 m, radii up to 4096 m and a margin of at most a chunk',
        );

        return null;
    }

    return { size, simulationRadius, visualRadius, margin };
}

function parseLimits(raw: unknown, problems: string[]): WorldLimits | null {
    if (!isObject(raw)) {
        problems.push('the limits are missing');

        return null;
    }

    const { half_extent: halfExtent, min_y: minY, max_y: maxY } = raw;

    if (
        !isFiniteNumber(halfExtent) ||
        halfExtent <= 0 ||
        halfExtent > MAX_HALF_EXTENT
    ) {
        problems.push(
            `the half extent must be a number in (0, ${MAX_HALF_EXTENT}]`,
        );

        return null;
    }

    if (!isFiniteNumber(minY) || !isFiniteNumber(maxY) || minY >= maxY) {
        problems.push('the height limits are not a range');

        return null;
    }

    return { halfExtent, minY, maxY };
}

function parseLocation(
    id: string,
    raw: unknown,
    limits: WorldLimits | null,
    problems: string[],
): LocationConfig | null {
    if (!LOCATION_ID.test(id)) {
        problems.push(`"${id}" is not a valid location id`);

        return null;
    }

    if (!isObject(raw) || !isObject(raw.bounds)) {
        problems.push(`${id}: no bounds`);

        return null;
    }

    const b = raw.bounds;
    const bounds: Bounds = {
        minX: b.min_x as number,
        maxX: b.max_x as number,
        minY: b.min_y as number,
        maxY: b.max_y as number,
        minZ: b.min_z as number,
        maxZ: b.max_z as number,
    };

    if (
        !Object.values(bounds).every(isFiniteNumber) ||
        bounds.minX >= bounds.maxX ||
        bounds.minY >= bounds.maxY ||
        bounds.minZ >= bounds.maxZ
    ) {
        problems.push(`${id}: the bounds are not a box`);

        return null;
    }

    if (
        limits &&
        (Math.max(-bounds.minX, bounds.maxX, -bounds.minZ, bounds.maxZ) >
            limits.halfExtent ||
            bounds.minY < limits.minY ||
            bounds.maxY > limits.maxY)
    ) {
        problems.push(`${id}: the bounds go past the world's limits`);

        return null;
    }

    if (!isPose(raw.spawn) || !inside(bounds, raw.spawn)) {
        problems.push(`${id}: the spawn is not a point inside the bounds`);

        return null;
    }

    const { x, y, z, yaw } = raw.spawn;

    return { id, bounds, spawn: { x, y, z, yaw } };
}

/** The settings the server put in the page. */
export function readPageConfig(element: Element | null): WorldConfig {
    let raw: unknown = null;

    try {
        raw = JSON.parse(element?.textContent ?? 'null');
    } catch {
        raw = null;
    }

    return parseWorldConfig(raw);
}
