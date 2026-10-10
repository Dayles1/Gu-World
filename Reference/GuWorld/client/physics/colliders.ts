/**
 * Solid things a body bumps into, besides the ground: spheres, upright
 * cylinders and axis-aligned boxes. A grid on X/Z (8 m cells) finds the
 * few that are near. A collider can be switched off without leaving the
 * grid, or taken out of it.
 *
 * Copied from the Sandbox's physics and made independent of any one
 * map: the ground comes in from the location (world/ground.ts). The grid
 * holds points up to MAX_HALF_EXTENT from the centre (world/config.ts).
 */

import * as THREE from 'three';
import type { Ground } from '../world/ground';

export interface SphereCollider {
    kind: 'sphere';
    center: THREE.Vector3;
    radius: number;
    disabled?: boolean;
}

export interface CylinderCollider {
    kind: 'cylinder';
    x: number;
    z: number;
    radius: number;
    bottom: number;
    top: number;
    disabled?: boolean;
}

export interface BoxCollider {
    kind: 'box';
    min: THREE.Vector3;
    max: THREE.Vector3;
    disabled?: boolean;
}

export type Collider = SphereCollider | CylinderCollider | BoxCollider;

/** The top of whatever is under a point: ground or an object. */
export interface Surface {
    top: number;
    /** Y of the surface normal there: 1 is flat. */
    normalY: number;
    onObject: boolean;
}

const CELL = 8;

function bounds(collider: Collider): [number, number, number, number] {
    switch (collider.kind) {
        case 'sphere':
            return [
                collider.center.x - collider.radius,
                collider.center.z - collider.radius,
                collider.center.x + collider.radius,
                collider.center.z + collider.radius,
            ];
        case 'cylinder':
            return [
                collider.x - collider.radius,
                collider.z - collider.radius,
                collider.x + collider.radius,
                collider.z + collider.radius,
            ];
        case 'box':
            return [
                collider.min.x,
                collider.min.z,
                collider.max.x,
                collider.max.z,
            ];
    }
}

const point = new THREE.Vector3();
const closest = new THREE.Vector3();

/**
 * How deep an upright capsule (feet at `position`) sinks into a collider,
 * and the way out of it in `normal`; 0 when they do not touch.
 */
export function penetration(
    collider: Collider,
    position: THREE.Vector3,
    radius: number,
    height: number,
    normal: THREE.Vector3,
): number {
    const bottom = position.y + radius;
    const top = position.y + Math.max(radius, height - radius);
    const axisAt = (y: number) =>
        point.set(position.x, Math.max(bottom, Math.min(top, y)), position.z);

    switch (collider.kind) {
        case 'sphere': {
            axisAt(collider.center.y);
            normal.subVectors(point, collider.center);
            const distance = normal.length();
            const reach = radius + collider.radius;

            if (distance >= reach) {
                return 0;
            }

            if (distance < 1e-6) {
                normal.set(0, 1, 0);
            } else {
                normal.divideScalar(distance);
            }

            return reach - distance;
        }
        case 'cylinder': {
            if (
                position.y > collider.top ||
                position.y + height < collider.bottom
            ) {
                return 0;
            }

            const dx = position.x - collider.x;
            const dz = position.z - collider.z;
            const distance = Math.hypot(dx, dz);
            const reach = radius + collider.radius;

            if (distance >= reach) {
                return 0;
            }

            // Standing on a stump: out through the top when that is nearer.
            const fromTop = collider.top - position.y;

            if (fromTop < reach - distance && distance < collider.radius) {
                normal.set(0, 1, 0);

                return fromTop;
            }

            if (distance < 1e-6) {
                normal.set(1, 0, 0);
            } else {
                normal.set(dx / distance, 0, dz / distance);
            }

            return reach - distance;
        }
        case 'box': {
            axisAt((collider.min.y + collider.max.y) / 2);
            closest.copy(point).clamp(collider.min, collider.max);
            axisAt(closest.y);
            closest.copy(point).clamp(collider.min, collider.max);
            normal.subVectors(point, closest);
            const distance = normal.length();

            if (distance >= radius) {
                return 0;
            }

            if (distance > 1e-6) {
                normal.divideScalar(distance);

                return radius - distance;
            }

            // The axis is inside the box: leave by the shortest way — up
            // onto it, or out of a side.
            const exits: [number, number, number, number][] = [
                [collider.max.y - position.y, 0, 1, 0],
                [collider.max.x - point.x + radius, 1, 0, 0],
                [point.x - collider.min.x + radius, -1, 0, 0],
                [collider.max.z - point.z + radius, 0, 0, 1],
                [point.z - collider.min.z + radius, 0, 0, -1],
            ];
            exits.sort((a, b) => a[0] - b[0]);
            const [depth, nx, ny, nz] = exits[0];
            normal.set(nx, ny, nz);

            return depth;
        }
    }
}

export class ColliderGrid {
    private cells = new Map<number, Collider[]>();
    private seen = new Set<Collider>();
    private found: Collider[] = [];
    private normal = new THREE.Vector3();

    constructor(private ground: Ground) {}

    add(collider: Collider): void {
        const [minX, minZ, maxX, maxZ] = bounds(collider);

        for (
            let cx = Math.floor(minX / CELL);
            cx <= Math.floor(maxX / CELL);
            cx++
        ) {
            for (
                let cz = Math.floor(minZ / CELL);
                cz <= Math.floor(maxZ / CELL);
                cz++
            ) {
                const key = this.key(cx, cz);
                const cell = this.cells.get(key);

                if (cell) {
                    cell.push(collider);
                } else {
                    this.cells.set(key, [collider]);
                }
            }
        }
    }

    /** Takes a collider out of the grid. */
    remove(collider: Collider): void {
        const [minX, minZ, maxX, maxZ] = bounds(collider);

        for (
            let cx = Math.floor(minX / CELL);
            cx <= Math.floor(maxX / CELL);
            cx++
        ) {
            for (
                let cz = Math.floor(minZ / CELL);
                cz <= Math.floor(maxZ / CELL);
                cz++
            ) {
                const key = this.key(cx, cz);
                const cell = this.cells
                    .get(key)
                    ?.filter((each) => each !== collider);

                if (cell?.length) {
                    this.cells.set(key, cell);
                } else {
                    this.cells.delete(key);
                }
            }
        }
    }

    /** Every working collider whose cell touches the square around (x, z). */
    near(x: number, z: number, reach: number, out: Collider[]): Collider[] {
        out.length = 0;
        this.seen.clear();

        for (
            let cx = Math.floor((x - reach) / CELL);
            cx <= Math.floor((x + reach) / CELL);
            cx++
        ) {
            for (
                let cz = Math.floor((z - reach) / CELL);
                cz <= Math.floor((z + reach) / CELL);
                cz++
            ) {
                for (const collider of this.cells.get(this.key(cx, cz)) ?? []) {
                    if (!collider.disabled && !this.seen.has(collider)) {
                        this.seen.add(collider);
                        out.push(collider);
                    }
                }
            }
        }

        return out;
    }

    /** The highest surface at (x, z): the ground or an object on it. */
    surfaceAt(x: number, z: number): Surface {
        const surface: Surface = {
            top: this.ground.heightAt(x, z),
            normalY: this.ground.normalAt(x, z, this.normal).y,
            onObject: false,
        };

        for (const collider of this.near(x, z, 0.1, this.found)) {
            let top = -Infinity;
            let normalY = 1;

            if (collider.kind === 'box') {
                if (
                    x >= collider.min.x &&
                    x <= collider.max.x &&
                    z >= collider.min.z &&
                    z <= collider.max.z
                ) {
                    top = collider.max.y;
                }
            } else if (collider.kind === 'sphere') {
                const dx = x - collider.center.x;
                const dz = z - collider.center.z;
                const inside = collider.radius ** 2 - dx * dx - dz * dz;

                if (inside > 0) {
                    const rise = Math.sqrt(inside);
                    top = collider.center.y + rise;
                    normalY = rise / collider.radius;
                }
            } else if (
                Math.hypot(x - collider.x, z - collider.z) < collider.radius
            ) {
                top = collider.top;
            }

            if (top > surface.top) {
                surface.top = top;
                surface.normalY = normalY;
                surface.onObject = true;
            }
        }

        return surface;
    }

    /** Whether an upright capsule there would be inside something. */
    blocked(position: THREE.Vector3, radius: number, height: number): boolean {
        return this.near(position.x, position.z, radius + 0.5, this.found).some(
            (collider) =>
                penetration(collider, position, radius, height, this.normal) >
                0.02,
        );
    }

    private key(cx: number, cz: number): number {
        return (cx + 4096) * 8192 + (cz + 4096);
    }
}
