/**
 * The game world as data: the location, the clock, the registry of every
 * entity, the colliders, what has changed in the world, and the hero
 * (entity and body). Built from a restored game (a new one or a save).
 * The location's things are not here yet: they come and go with the
 * chunks around the hero (world/chunk-manager.ts), into this registry
 * and these colliders, with these changes applied. Nothing here draws.
 */

import type { Look } from '../body/mannequin';
import type { WorldClock } from '../engine/clock';
import { EntityRegistry } from '../entities/registry';
import { PLAYER_ID, STANDING } from '../entities/types';
import type { GameEntity, PlayerEntity } from '../entities/types';
import { Character } from '../physics/character';
import { ColliderGrid } from '../physics/colliders';
import type { WorldChanges } from '../world/changes';
import type { WorldConfig } from '../world/config';
import { resolveLocation } from '../world/locations';
import type { Location } from '../world/locations';
import type { Pose } from '../world/space';
import { makeSave } from './save';
import type { RestoredGame, Save } from './save';

export interface World {
    config: WorldConfig;
    location: Location;
    clock: WorldClock;
    registry: EntityRegistry<GameEntity>;
    colliders: ColliderGrid;
    changes: WorldChanges;
    /** The hero's body (physics) and the hero as an entity. */
    body: Character;
    player: PlayerEntity;
}

export function buildWorld(
    config: WorldConfig,
    restored: RestoredGame,
    look: Look,
    registry = new EntityRegistry<GameEntity>(),
): World {
    const location = resolveLocation(config, restored.location);
    const colliders = new ColliderGrid(location.ground);
    const body = new Character(colliders, location.ground, location.bounds);
    const { x, y, z, yaw } = restored.player;

    body.placeAt(x, y, z);
    body.facing = yaw;

    const player = registry.ensure(PLAYER_ID, (): PlayerEntity => ({
        id: PLAYER_ID,
        kind: 'player',
        location: location.id,
        position: { ...body.position },
        yaw,
        previous: { ...body.position },
        previousYaw: yaw,
        motion: { ...STANDING },
        look,
    })) as PlayerEntity;

    return {
        config,
        location,
        clock: restored.clock,
        registry,
        colliders,
        changes: restored.changes,
        body,
        player,
    };
}

/** Where the hero stands now. */
export function playerPose(world: World): Pose {
    return { ...world.player.position, yaw: world.player.yaw };
}

/** The world's save (with the changes as they stand: capture loaded things' changes first). */
export function saveWorld(world: World): Save {
    return makeSave(
        world.location.id,
        world.clock,
        playerPose(world),
        world.changes,
    );
}
