/**
 * A body's physics: an upright capsule pushed around by its input,
 * gravity, the ground and solid objects. Runs on its own fixed step
 * (STEP, 120 Hz) inside the game's simulation step, so it behaves the
 * same at any frame rate.
 *
 * Besides moving it can stand, crouch or crawl (a shorter capsule), climb
 * onto a ledge (a scripted move from the foot of the ledge onto its top)
 * and swim: in deep water it floats with the head out, rises and dives on
 * demand and runs out of breath when kept under. It never leaves its
 * location's bounds.
 *
 * Copied from the Sandbox's physics; the Sandbox's game mechanics on top
 * of it (seats, the assassin's dash, artifact bonuses, double jumps) are
 * left out, and the ground and the bounds come from the location instead
 * of one fixed map.
 *
 * `position` is the point between the feet.
 */

import * as THREE from 'three';
import type { Ground } from '../world/ground';
import type { Bounds } from '../world/space';
import { penetration } from './colliders';
import type { Collider, ColliderGrid } from './colliders';

export const RADIUS = 0.3;

export type Stance = 'stand' | 'crouch' | 'crawl';

export const STANCE_HEIGHT: Record<Stance, number> = {
    stand: 1.8,
    crouch: 1.2,
    crawl: 0.62,
};

/** [walk, run] speeds per stance, m/s. */
export const STANCE_SPEED: Record<Stance, [number, number]> = {
    stand: [4.2, 7.5],
    crouch: [2.1, 2.6],
    crawl: [1.1, 1.3],
};

const STEP = 1 / 120;
const MAX_STEPS = 10;
const GRAVITY = 25;
const JUMP_SPEED = 8.6;
const GROUND_ACCELERATION = 30;
const GROUND_BRAKING = 38;
const AIR_ACCELERATION = 9;
/** The steepest ground one can stand on: cos 50°. */
const WALKABLE = Math.cos((50 * Math.PI) / 180);
/** How far down the feet stick to the ground when walking downhill. */
const SNAP = 0.4;
/** Objects up to this high are stepped onto without climbing. */
const STEP_UP = 0.45;
/** Ledges this high (above the feet) can be climbed onto. */
const CLIMB_MIN = 0.5;
const CLIMB_MAX = 2.3;
/** Deeper than this (water above the feet) the body swims… */
const SWIM_DEPTH = 1.25;
/** …and stops swimming once it is this shallow again. */
const WADE_DEPTH = 0.95;
/** Feet this far under the surface while floating: the head is out. */
const FLOAT_DEPTH = 1.35;
const SWIM_SPEED = 2.3;
const SWIM_SPRINT = 3.3;
const SWIM_VERTICAL = 2.2;
/** Seconds of breath under water. */
const BREATH_SECONDS = 15;
const EYES = 1.6;
/** A jump still works this long after walking off an edge… */
const COYOTE_TIME = 0.1;
/** …and a press counts this long before landing. */
const JUMP_BUFFER = 0.12;

export interface CharacterInput {
    /** Where to go in world space (X, Z), length 0…1. */
    moveX: number;
    moveZ: number;
    sprint: boolean;
    /** True on the frame the jump key went down. */
    jump: boolean;
    /** Movement is scaled by this (slower while busy). */
    speedScale: number;
    /** Held keys: swim up / dive. */
    rise: boolean;
    dive: boolean;
}

interface Climb {
    from: THREE.Vector3;
    to: THREE.Vector3;
    time: number;
    duration: number;
}

export class Character {
    readonly position = new THREE.Vector3();
    readonly velocity = new THREE.Vector3();
    readonly groundNormal = new THREE.Vector3(0, 1, 0);
    grounded = false;
    /** Standing on the terrain itself (not on an object). */
    onTerrain = true;
    /** Which way the body faces (radians around Y; 0 looks along +Z). */
    facing = 0;
    /** How fast `facing` turns, rad/s (smoothed). */
    turnRate = 0;
    /** Change of ground speed, m/s² (smoothed). */
    acceleration = 0;
    /** Downward speed at the last landing, for the animation. */
    landingSpeed = 0;
    /** Height popped up by the last step onto an object (for smoothing). */
    steppedUp = 0;

    stance: Stance = 'stand';
    swimming = false;
    /** 0…1, used up while the head is under water. */
    breath = 1;
    private climb: Climb | null = null;

    private accumulator = 0;
    private sinceGrounded = 0;
    private jumpBuffered = 0;
    private lastSpeed = 0;
    private nearby: Collider[] = [];
    private normal = new THREE.Vector3();
    private probe = new THREE.Vector3();

    /** How far inside the location's bounds the body is kept, m. */
    static readonly EDGE = 1;

    constructor(
        private colliders: ColliderGrid,
        private ground: Ground,
        private bounds: Bounds,
    ) {}

    get height(): number {
        return STANCE_HEIGHT[this.stance];
    }

    get horizontalSpeed(): number {
        return Math.hypot(this.velocity.x, this.velocity.z);
    }

    /** 0…1 through a climb, or null when not climbing. */
    get climbProgress(): number | null {
        return this.climb ? this.climb.time / this.climb.duration : null;
    }

    /** Whether the body is free to act (not climbing or swimming). */
    get free(): boolean {
        return !this.climb && !this.swimming;
    }

    /** How deep the feet are under water (negative: above it). */
    get waterDepth(): number {
        return this.ground.waterLevel - this.position.y;
    }

    get underwater(): boolean {
        return this.position.y + EYES < this.ground.waterLevel;
    }

    /** Puts the character at (x, z), standing on whatever is there. */
    placeAt(x: number, y: number, z: number): void {
        this.position.set(x, Math.max(y, this.ground.heightAt(x, z)), z);
        this.velocity.set(0, 0, 0);
        this.accumulator = 0;
    }

    /** Changes stance; false when there is no room to stand taller. */
    setStance(stance: Stance): boolean {
        if (!this.free || this.waterDepth > 0.6) {
            return false;
        }

        if (
            STANCE_HEIGHT[stance] > this.height &&
            this.colliders.blocked(this.position, RADIUS, STANCE_HEIGHT[stance])
        ) {
            return false;
        }

        this.stance = stance;

        return true;
    }

    /**
     * Starts climbing onto a ledge in the given direction, if there is one
     * within reach. In the air a lower ledge is enough (a grab).
     */
    tryClimb(dirX: number, dirZ: number): boolean {
        if (this.climb || this.stance === 'crawl') {
            return false;
        }

        const length = Math.hypot(dirX, dirZ);

        if (length < 1e-3) {
            return false;
        }

        dirX /= length;
        dirZ /= length;

        const min = this.grounded ? CLIMB_MIN : 0.25;

        for (const reach of [RADIUS + 0.2, RADIUS + 0.45]) {
            const x = this.position.x + dirX * reach;
            const z = this.position.z + dirZ * reach;
            const surface = this.colliders.surfaceAt(x, z);
            const rise = surface.top - this.position.y;

            if (rise < min || rise > CLIMB_MAX || surface.normalY < WALKABLE) {
                continue;
            }

            // From the water only onto a ledge that is out of it.
            if (this.swimming && surface.top < this.ground.waterLevel + 0.1) {
                continue;
            }

            const toX = x + dirX * 0.3;
            const toZ = z + dirZ * 0.3;
            const landing = this.colliders.surfaceAt(toX, toZ);
            const to = new THREE.Vector3(
                toX,
                Math.max(surface.top, landing.top),
                toZ,
            );

            if (this.colliders.blocked(to, RADIUS, STANCE_HEIGHT.crouch)) {
                continue;
            }

            this.swimming = false;
            this.climb = {
                from: this.position.clone(),
                to,
                time: 0,
                duration: 0.4 + rise * 0.3,
            };
            this.facing = Math.atan2(dirX, dirZ);
            this.velocity.set(0, 0, 0);
            this.stance = 'stand';

            return true;
        }

        return false;
    }

    update(dt: number, input: CharacterInput): void {
        const before = this.facing;
        this.steppedUp = 0;

        if (this.climb) {
            this.updateClimb(dt);
        } else {
            if (input.jump) {
                this.jumpBuffered = JUMP_BUFFER;
            }

            this.accumulator = Math.min(
                this.accumulator + dt,
                STEP * MAX_STEPS,
            );

            while (this.accumulator >= STEP) {
                this.step(input);
                this.accumulator -= STEP;
            }

            if (input.moveX !== 0 || input.moveZ !== 0) {
                const target = Math.atan2(input.moveX, input.moveZ);
                let turn = target - this.facing;
                turn = Math.atan2(Math.sin(turn), Math.cos(turn));
                const rate = this.stance === 'crawl' ? 6 : 11;
                this.facing += turn * (1 - Math.exp(-rate * dt));
            }

            // Running into a ledge in mid-air grabs it.
            if (
                !this.grounded &&
                this.velocity.y < 3 &&
                (input.moveX !== 0 || input.moveZ !== 0)
            ) {
                this.tryClimb(input.moveX, input.moveZ);
            }
        }

        if (dt > 0) {
            const turned = Math.atan2(
                Math.sin(this.facing - before),
                Math.cos(this.facing - before),
            );
            const speed = this.horizontalSpeed;
            const smoothing = 1 - Math.exp(-10 * dt);
            this.turnRate += (turned / dt - this.turnRate) * smoothing;
            this.acceleration +=
                ((speed - this.lastSpeed) / dt - this.acceleration) * smoothing;
            this.lastSpeed = speed;
        }
    }

    private updateClimb(dt: number): void {
        const climb = this.climb!;
        climb.time += dt;
        const progress = Math.min(1, climb.time / climb.duration);
        const ease = (t: number) => t * t * (3 - 2 * t);

        // Up the face first, then over the top.
        if (progress < 0.65) {
            const up = ease(progress / 0.65);
            this.position.set(
                climb.from.x,
                climb.from.y + (climb.to.y - climb.from.y) * up,
                climb.from.z,
            );
        } else {
            const over = ease((progress - 0.65) / 0.35);
            this.position.set(
                climb.from.x + (climb.to.x - climb.from.x) * over,
                climb.to.y,
                climb.from.z + (climb.to.z - climb.from.z) * over,
            );
        }

        if (progress >= 1) {
            this.climb = null;
            this.grounded = true;
            this.accumulator = 0;
        }
    }

    private step(input: CharacterInput): void {
        const position = this.position;
        const velocity = this.velocity;
        const wasGrounded = this.grounded;

        this.breathe();

        const depth = this.waterDepth;

        if (depth > SWIM_DEPTH || (this.swimming && depth > WADE_DEPTH)) {
            this.swim(input);

            return;
        }

        this.swimming = false;

        this.sinceGrounded = wasGrounded ? 0 : this.sinceGrounded + STEP;
        this.jumpBuffered = Math.max(0, this.jumpBuffered - STEP);

        const [walk, run] = STANCE_SPEED[this.stance];
        const wading = depth > 0.3 ? 0.6 : 1;
        const speed = (input.sprint ? run : walk) * input.speedScale * wading;
        const wishX = input.moveX * speed;
        const wishZ = input.moveZ * speed;
        const braking =
            wishX * velocity.x + wishZ * velocity.z <= 0 ||
            Math.hypot(wishX, wishZ) < Math.hypot(velocity.x, velocity.z);
        const acceleration =
            (wasGrounded
                ? braking
                    ? GROUND_BRAKING
                    : GROUND_ACCELERATION
                : AIR_ACCELERATION) * STEP;
        const dx = wishX - velocity.x;
        const dz = wishZ - velocity.z;
        const change = Math.hypot(dx, dz);
        const scale = change > acceleration ? acceleration / change : 1;
        velocity.x += dx * scale;
        velocity.z += dz * scale;

        let jumped = false;

        if (
            this.jumpBuffered > 0 &&
            this.sinceGrounded <= COYOTE_TIME &&
            this.stance !== 'crawl'
        ) {
            velocity.y = JUMP_SPEED * (this.stance === 'crouch' ? 0.75 : 1);
            this.jumpBuffered = 0;
            this.sinceGrounded = COYOTE_TIME + 1;
            jumped = true;
        }

        if (wasGrounded && !jumped) {
            this.stepUp(input.moveX, input.moveZ);
        }

        velocity.y -= GRAVITY * STEP;
        const fallSpeed = -velocity.y;
        position.addScaledVector(velocity, STEP);

        this.grounded = false;
        this.onTerrain = false;
        this.collideWithObjects();
        this.collideWithGround();

        if (!this.grounded && wasGrounded && !jumped && velocity.y <= 0) {
            this.snapToGround();
        }

        if (this.grounded && !wasGrounded) {
            this.landingSpeed = Math.max(this.landingSpeed, fallSpeed);
        }

        this.keepInside();
    }

    /**
     * Swimming: slow and floaty. Holding jump rises, holding crouch dives;
     * otherwise the body bobs up to float with the head out. Out of breath
     * it is pushed up whatever the keys say.
     */
    private swim(input: CharacterInput): void {
        const position = this.position;
        const velocity = this.velocity;

        if (!this.swimming) {
            this.landingSpeed = Math.max(this.landingSpeed, -velocity.y);
        }

        this.swimming = true;
        this.stance = 'stand';
        this.grounded = false;

        const speed =
            (input.sprint ? SWIM_SPRINT : SWIM_SPEED) * input.speedScale;
        const change = 6 * STEP;
        velocity.x += THREE.MathUtils.clamp(
            input.moveX * speed - velocity.x,
            -change,
            change,
        );
        velocity.z += THREE.MathUtils.clamp(
            input.moveZ * speed - velocity.z,
            -change,
            change,
        );

        const float = this.ground.waterLevel - FLOAT_DEPTH;
        let targetY = THREE.MathUtils.clamp(
            (float - position.y) * 2.5,
            -1.5,
            1.5,
        );

        if (input.rise) {
            targetY = SWIM_VERTICAL;
        } else if (input.dive) {
            targetY = -SWIM_VERTICAL;
        }

        if (this.breath <= 0) {
            targetY = 3;
        }

        // Can't jump out of the water, only float up to the surface.
        if (position.y > float && targetY > 0) {
            targetY = Math.min(targetY, (float - position.y) * 4 + 0.3);
        }

        velocity.y += (targetY - velocity.y) * Math.min(1, 4 * STEP);
        position.addScaledVector(velocity, STEP);

        this.onTerrain = false;
        this.collideWithObjects();
        this.grounded = false;

        const ground = this.ground.heightAt(position.x, position.z);

        if (position.y < ground) {
            position.y = ground;
            velocity.y = Math.max(0, velocity.y);
        }

        this.keepInside();
    }

    /** Keeps the body inside its location: a wall at the edges, a floor and a ceiling. */
    private keepInside(): void {
        const { position, velocity, bounds } = this;
        const edge = Character.EDGE;
        const x = THREE.MathUtils.clamp(
            position.x,
            bounds.minX + edge,
            bounds.maxX - edge,
        );
        const z = THREE.MathUtils.clamp(
            position.z,
            bounds.minZ + edge,
            bounds.maxZ - edge,
        );

        if (x !== position.x) {
            position.x = x;
            velocity.x = 0;
        }

        if (z !== position.z) {
            position.z = z;
            velocity.z = 0;
        }

        if (position.y < bounds.minY) {
            position.y = bounds.minY;
            velocity.y = Math.max(0, velocity.y);
        } else if (position.y > bounds.maxY) {
            position.y = bounds.maxY;
            velocity.y = Math.min(0, velocity.y);
        }
    }

    private breathe(): void {
        if (this.underwater) {
            this.breath = Math.max(0, this.breath - STEP / BREATH_SECONDS);
        } else {
            this.breath = Math.min(1, this.breath + STEP / 3);
        }
    }

    /** Walks up onto a low object (a block, a stump) instead of stopping. */
    private stepUp(moveX: number, moveZ: number): void {
        const length = Math.hypot(moveX, moveZ);

        if (length < 0.1) {
            return;
        }

        const x = this.position.x + (moveX / length) * (RADIUS + 0.08);
        const z = this.position.z + (moveZ / length) * (RADIUS + 0.08);
        const surface = this.colliders.surfaceAt(x, z);
        const rise = surface.top - this.position.y;

        if (
            !surface.onObject ||
            rise <= 0.03 ||
            rise > STEP_UP ||
            surface.normalY < WALKABLE
        ) {
            return;
        }

        this.probe.set(this.position.x, surface.top + 0.01, this.position.z);

        if (this.colliders.blocked(this.probe, RADIUS, this.height)) {
            return;
        }

        this.position.y = surface.top + 0.01;
        this.steppedUp += rise;
    }

    /**
     * Standing on ground that is flat enough; on steeper ground the body is
     * pushed out along the slope, so it slides instead of climbing.
     */
    private collideWithGround(): void {
        const position = this.position;
        const ground = this.ground.heightAt(position.x, position.z);

        if (position.y >= ground) {
            return;
        }

        this.ground.normalAt(position.x, position.z, this.normal);

        if (this.normal.y >= WALKABLE) {
            position.y = ground;
            this.land(this.normal);
            this.onTerrain = true;

            return;
        }

        position.addScaledVector(
            this.normal,
            (ground - position.y) * this.normal.y,
        );
        this.removeVelocityInto(this.normal);
    }

    /** Keeps the feet on the ground when walking down a slope or a step. */
    private snapToGround(): void {
        const position = this.position;
        const ground = this.ground.heightAt(position.x, position.z);
        const gap = position.y - ground;

        if (gap < 0 || gap > SNAP) {
            return;
        }

        this.ground.normalAt(position.x, position.z, this.normal);

        if (this.normal.y >= WALKABLE) {
            position.y = ground;
            this.land(this.normal);
            this.onTerrain = true;
        }
    }

    private collideWithObjects(): void {
        const position = this.position;

        for (const collider of this.colliders.near(
            position.x,
            position.z,
            RADIUS + 0.5,
            this.nearby,
        )) {
            const depth = penetration(
                collider,
                position,
                RADIUS,
                this.height,
                this.normal,
            );

            if (depth > 0) {
                position.addScaledVector(this.normal, depth);
                this.removeVelocityInto(this.normal);

                if (this.normal.y >= WALKABLE) {
                    this.land(this.normal);
                }
            }
        }
    }

    private removeVelocityInto(normal: THREE.Vector3): void {
        const into = this.velocity.dot(normal);

        if (into < 0) {
            this.velocity.addScaledVector(normal, -into);
        }
    }

    private land(normal: THREE.Vector3): void {
        this.grounded = true;
        this.groundNormal.copy(normal);

        if (this.velocity.y < 0) {
            this.velocity.y = 0;
        }
    }
}
