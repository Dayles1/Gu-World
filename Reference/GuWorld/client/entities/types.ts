/**
 * The kinds of entity GU World has so far. Plain data: what the systems
 * change and the views draw.
 */

import type { Look } from '../body/mannequin';
import type { Stance } from '../physics/character';
import type { EntityState } from '../world/changes';
import type { Vec3 } from '../world/space';
import type { Entity } from './registry';

/** What the body is doing, for its figure to show (no function, no three.js). */
export interface Motion {
    speed: number;
    walkSpeed: number;
    runSpeed: number;
    stance: Stance;
    grounded: boolean;
    verticalSpeed: number;
    turnRate: number;
    acceleration: number;
    swimming: boolean;
    climb: number | null;
    /** How hard the body last landed, m/s (eases its knees). */
    landingSpeed: number;
}

export interface PlayerEntity extends Entity {
    kind: 'player';
    /** Where it was a simulation step ago, for drawing between steps. */
    previous: Vec3;
    previousYaw: number;
    motion: Motion;
    look: Look;
}

/**
 * A thing of a location's content, loaded with the area it stands in.
 * `state` is what can change about it (see world/changes.ts).
 */
interface PropEntity extends Entity {
    state: EntityState;
}

/** A solid box. */
export interface BlockEntity extends PropEntity {
    kind: 'block';
    width: number;
    depth: number;
    height: number;
}

/** A rounded stone. */
export interface StoneEntity extends PropEntity {
    kind: 'stone';
    radius: number;
}

export type ContentEntity = BlockEntity | StoneEntity;

export type GameEntity = PlayerEntity | ContentEntity;

export const PLAYER_ID = 'player';

export const STANDING: Motion = {
    speed: 0,
    walkSpeed: 4.2,
    runSpeed: 7.5,
    stance: 'stand',
    grounded: true,
    verticalSpeed: 0,
    turnRate: 0,
    acceleration: 0,
    swimming: false,
    climb: null,
    landingSpeed: 0,
};
