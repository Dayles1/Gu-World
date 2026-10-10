/**
 * Third-person camera: orbits behind the hero, turned by the mouse,
 * zoomed by the wheel, and pulled in so it never ends up under the ground.
 * It follows softly — the eye height eases with the stance, the view
 * widens a little when running.
 *
 * Copied from the Sandbox's camera; the ground comes from the location.
 */

import * as THREE from 'three';
import type { Ground } from '../world/ground';

const SENSITIVITY = 0.0024;
const MIN_PITCH = -0.45;
const MAX_PITCH = 1.35;
const MIN_DISTANCE = 1.8;
const MAX_DISTANCE = 14;
const FOV = 65;
const RUN_FOV = 72;

export class ThirdPersonCamera {
    readonly camera: THREE.PerspectiveCamera;
    /** Radians around Y; 0 puts the camera on +Z looking toward -Z. */
    yaw = 0;
    pitch = 0.32;
    /** The player's setting: how fast the mouse or finger turns the view. */
    sensitivity = 1;

    private trembling = 0;
    private wantedDistance = 5;
    private distance = 5;
    private eye = 1.55;
    private focus = new THREE.Vector3();
    private direction = new THREE.Vector3();
    private probe = new THREE.Vector3();

    constructor(
        aspect: number,
        private ground: Ground,
    ) {
        this.camera = new THREE.PerspectiveCamera(FOV, aspect, 0.1, 900);
    }

    /** The way the camera looks, as a body facing (0 looks along +Z). */
    get facing(): number {
        return this.yaw + Math.PI;
    }

    /** Forward and right on the ground, for turning input into motion. */
    groundAxes(): {
        forwardX: number;
        forwardZ: number;
        rightX: number;
        rightZ: number;
    } {
        const sin = Math.sin(this.yaw);
        const cos = Math.cos(this.yaw);

        return { forwardX: -sin, forwardZ: -cos, rightX: cos, rightZ: -sin };
    }

    /** A jolt (a blow taken), 0…1. */
    shake(amount: number): void {
        this.trembling = Math.min(1, this.trembling + amount);
    }

    look(dx: number, dy: number, wheel: number): void {
        this.yaw -= dx * SENSITIVITY * this.sensitivity;
        this.pitch = THREE.MathUtils.clamp(
            this.pitch + dy * SENSITIVITY * this.sensitivity,
            MIN_PITCH,
            MAX_PITCH,
        );
        this.wantedDistance = THREE.MathUtils.clamp(
            this.wantedDistance * (1 + wheel * 0.12),
            MIN_DISTANCE,
            MAX_DISTANCE,
        );
    }

    update(
        dt: number,
        target: THREE.Vector3,
        eyeHeight: number,
        running: boolean,
        snap = false,
    ): void {
        const follow = (rate: number) => (snap ? 1 : 1 - Math.exp(-rate * dt));

        this.eye += (eyeHeight - this.eye) * follow(6);
        this.focus.x += (target.x - this.focus.x) * follow(22);
        this.focus.z += (target.z - this.focus.z) * follow(22);
        this.focus.y += (target.y + this.eye - this.focus.y) * follow(12);

        const fov = running ? RUN_FOV : FOV;

        if (Math.abs(this.camera.fov - fov) > 0.01) {
            this.camera.fov += (fov - this.camera.fov) * follow(4);
            this.camera.updateProjectionMatrix();
        }

        const cosPitch = Math.cos(this.pitch);
        this.direction.set(
            Math.sin(this.yaw) * cosPitch,
            Math.sin(this.pitch),
            Math.cos(this.yaw) * cosPitch,
        );

        const allowed = this.clearDistance(this.wantedDistance);
        this.distance =
            allowed < this.distance || snap
                ? allowed
                : THREE.MathUtils.lerp(this.distance, allowed, follow(5));

        this.camera.position
            .copy(this.focus)
            .addScaledVector(this.direction, this.distance);
        this.camera.lookAt(this.focus);

        if (this.trembling > 0.001) {
            const jolt = this.trembling * this.trembling * 0.06;
            this.camera.rotation.x += (Math.random() - 0.5) * jolt;
            this.camera.rotation.y += (Math.random() - 0.5) * jolt;
            this.trembling *= Math.exp(-7 * dt);
        }
    }

    resize(aspect: number): void {
        this.camera.aspect = aspect;
        this.camera.updateProjectionMatrix();
    }

    /** How far back the camera can go before the ground is in the way. */
    private clearDistance(wanted: number): number {
        const samples = 20;

        for (let i = 1; i <= samples; i++) {
            const distance = (wanted * i) / samples;
            this.probe
                .copy(this.focus)
                .addScaledVector(this.direction, distance);

            if (
                this.probe.y <
                this.ground.heightAt(this.probe.x, this.probe.z) + 0.3
            ) {
                return Math.max(0.6, (wanted * (i - 1)) / samples);
            }
        }

        return wanted;
    }
}
