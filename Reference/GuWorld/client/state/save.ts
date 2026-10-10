/**
 * The saved game and getting a game back from one.
 *
 * Format v2 holds the format's version, the location the hero is in, the
 * game minutes since the world began (the day and time of day follow),
 * where the hero stands and faces, and what has changed in the world
 * (world/changes.ts, by entity id). Format v1 — the same without the
 * changes — is still read. The server checks a save on the way in
 * (Http/Requests/SaveGameRequest.php); the client checks it again on the
 * way out of the server and before sending.
 *
 * Restoring never trusts what comes back: anything that cannot be used
 * is replaced by something safe — a broken time by the first morning, a
 * broken or out-of-bounds position by the location's spawn, an unknown
 * location or format by the starting location, broken changes dropped —
 * and each reason is kept in `problems`, so it can be shown and logged.
 * Fields it does not know are ignored.
 */

import { WorldClock } from '../engine/clock';
import { MAX_CHANGES, WorldChanges } from '../world/changes';
import type { EntityChange } from '../world/changes';
import type { LocationConfig, LocationId, WorldConfig } from '../world/config';
import { LOCATION_ID } from '../world/config';
import { inside, isPose, wrapAngle } from '../world/space';
import type { Pose } from '../world/space';

export const SAVE_VERSION = 2;

/** The formats a save may come in (older ones are read and saved again as the current). */
const READABLE = [1, 2];

export interface Save {
    version: typeof SAVE_VERSION;
    location: LocationId;
    world_minutes: number;
    player: Pose;
    changes: Record<string, EntityChange>;
}

export interface RestoredGame {
    location: LocationId;
    clock: WorldClock;
    player: Pose;
    changes: WorldChanges;
    /** No save at all: a new game. */
    fresh: boolean;
    /** Why parts of the save were replaced; empty when it was sound. */
    problems: string[];
}

type Raw = Record<string, unknown>;

/** A new game: the starting location's spawn, the first morning, an untouched world. */
export function newGame(config: WorldConfig): RestoredGame {
    return {
        location: config.start,
        clock: WorldClock.newGame(),
        player: { ...config.locations.get(config.start)!.spawn },
        changes: new WorldChanges(),
        fresh: true,
        problems: [],
    };
}

/**
 * The game a save describes. `canBuild` says whether the client can
 * build a location (has its content).
 */
export function restoreGame(
    saved: unknown,
    config: WorldConfig,
    canBuild: (location: LocationId) => boolean = () => true,
): RestoredGame {
    if (saved === null || saved === undefined) {
        return newGame(config);
    }

    const start = newGame(config);
    const problems: string[] = [];

    if (typeof saved !== 'object' || Array.isArray(saved)) {
        return {
            ...start,
            fresh: false,
            problems: ['the save is not an object'],
        };
    }

    const raw = saved as Raw;

    if (!READABLE.includes(raw.version as number)) {
        return {
            ...start,
            fresh: false,
            problems: [`unknown save format ${JSON.stringify(raw.version)}`],
        };
    }

    // The location: one of the world's that the client can build.
    let location: LocationConfig = config.locations.get(config.start)!;
    const id = raw.location;
    const accepted =
        typeof id === 'string' && config.locations.has(id) && canBuild(id);

    if (accepted) {
        location = config.locations.get(id)!;
    } else {
        problems.push(
            `unknown location ${JSON.stringify(id)}: back to "${config.start}"`,
        );
    }

    const { clock, repaired } = WorldClock.fromSaved(raw.world_minutes);

    if (repaired) {
        problems.push(
            `broken time ${JSON.stringify(raw.world_minutes) ?? 'undefined'}: the first morning instead`,
        );
    }

    // The hero and the world's changes belong to the saved location: in
    // one that could not be used they mean nothing.
    let player: Pose = { ...location.spawn };
    let changes = new WorldChanges();

    if (accepted) {
        if (!isPose(raw.player)) {
            problems.push('broken position: back to the spawn');
        } else if (!inside(location.bounds, raw.player)) {
            problems.push('position out of the location: back to the spawn');
        } else {
            const { x, y, z, yaw } = raw.player;
            player = { x, y, z, yaw: wrapAngle(yaw) };
        }

        const restored = WorldChanges.restore(raw.changes);
        changes = restored.changes;
        problems.push(...restored.problems);
    }

    return {
        location: location.id,
        clock,
        player,
        changes,
        fresh: false,
        problems,
    };
}

/** The save for a game. */
export function makeSave(
    location: LocationId,
    clock: WorldClock,
    player: Pose,
    changes: WorldChanges = new WorldChanges(),
): Save {
    return {
        version: SAVE_VERSION,
        location,
        world_minutes: clock.minutes,
        player: {
            x: player.x,
            y: player.y,
            z: player.z,
            yaw: wrapAngle(player.yaw),
        },
        changes: changes.toJSON(),
    };
}

/** What is wrong with a save before it is sent (empty when it is sound). */
export function checkSave(save: Save, config: WorldConfig): string[] {
    const problems: string[] = [];
    const location = config.locations.get(save.location);

    if (save.version !== SAVE_VERSION) {
        problems.push('wrong version');
    }

    if (!LOCATION_ID.test(save.location) || !location) {
        problems.push('unknown location');
    }

    if (WorldClock.fromSaved(save.world_minutes).repaired) {
        problems.push('broken time');
    }

    if (!isPose(save.player)) {
        problems.push('broken position');
    } else if (location && !inside(location.bounds, save.player)) {
        problems.push('position out of the location');
    }

    const changes = WorldChanges.restore(save.changes);

    if (
        changes.problems.length ||
        Object.keys(save.changes ?? {}).length > MAX_CHANGES
    ) {
        problems.push('broken world changes');
    }

    return problems;
}
