/**
 * The hero, each simulation step: turns the held keys (relative to where
 * the camera looks) into movement for the body's physics, then writes
 * where the body got to into the hero's entity — through the registry,
 * which refuses a broken position. Should the body's numbers ever break,
 * it is put back on the location's spawn and the failure reported, rather
 * than the broken numbers written into the world.
 *
 * Jumping, crouching and crawling come in as one-off actions.
 */

import type { GameSystem } from '../engine/systems';
import { STANCE_SPEED } from '../physics/character';
import type { World } from '../state/world';
import { isFiniteNumber, isVec3 } from '../world/space';

export interface Controls {
    /** -1…1: forward/back and right/left. */
    forward(): number;
    strafe(): number;
    sprint(): boolean;
    rise(): boolean;
    dive(): boolean;
    /** Forward and right on the ground, from where the camera looks. */
    axes(): {
        forwardX: number;
        forwardZ: number;
        rightX: number;
        rightZ: number;
    };
}

export class PlayerSystem implements GameSystem {
    readonly name = 'player';
    private jumpQueued = false;

    constructor(
        private world: World,
        private controls: Controls,
    ) {}

    /** Jump — or climb, when there is a ledge ahead to climb onto. */
    jump(): void {
        const body = this.world.body;
        const { x, z } = this.direction();
        const ahead =
            Math.hypot(x, z) > 0.1
                ? { x, z }
                : { x: Math.sin(body.facing), z: Math.cos(body.facing) };

        if (!body.grounded || !body.tryClimb(ahead.x, ahead.z)) {
            this.jumpQueued = true;
        }
    }

    /** Crouch or crawl, or stand up again from it. */
    toggleStance(stance: 'crouch' | 'crawl'): void {
        const body = this.world.body;
        body.setStance(body.stance === stance ? 'stand' : stance);
    }

    update(step: number): void {
        const { body, player, registry, location } = this.world;

        player.previous = { ...player.position };
        player.previousYaw = player.yaw;

        const { x, z } = this.direction();

        body.update(step, {
            moveX: x,
            moveZ: z,
            sprint: this.controls.sprint(),
            jump: this.jumpQueued,
            speedScale: 1,
            rise: this.controls.rise(),
            dive: this.controls.dive(),
        });
        this.jumpQueued = false;

        if (!isVec3(body.position) || !isFiniteNumber(body.facing)) {
            const { spawn } = location;
            body.placeAt(spawn.x, spawn.y, spawn.z);
            body.facing = spawn.yaw;
            player.previous = { ...body.position };

            throw new Error("the hero's body broke: back to the spawn");
        }

        registry.move(player.id, body.position, body.facing);

        const [walk, run] = STANCE_SPEED[body.stance];
        player.motion = {
            speed: body.horizontalSpeed,
            walkSpeed: walk,
            runSpeed: run,
            stance: body.stance,
            grounded: body.grounded,
            verticalSpeed: body.velocity.y,
            turnRate: body.turnRate,
            acceleration: body.acceleration,
            swimming: body.swimming,
            climb: body.climbProgress,
            landingSpeed: body.landingSpeed,
        };
        body.landingSpeed = 0;
    }

    /** Where the keys say to go, on the ground, at most length 1. */
    private direction(): { x: number; z: number } {
        const axes = this.controls.axes();
        const forward = this.controls.forward();
        const strafe = this.controls.strafe();
        let x = axes.forwardX * forward + axes.rightX * strafe;
        let z = axes.forwardZ * forward + axes.rightZ * strafe;
        const length = Math.hypot(x, z);

        if (length > 1) {
            x /= length;
            z /= length;
        }

        return { x, z };
    }
}
