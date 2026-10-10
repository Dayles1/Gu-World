/**
 * Points, poses and boxes of the world. Every distance is in metres; Y is
 * up; yaw is radians around Y (0 looks along +Z). Nothing here knows the
 * size of any place — that is world data (world/config.ts).
 */

export interface Vec3 {
    x: number;
    y: number;
    z: number;
}

/** Where something stands and which way it faces. */
export interface Pose extends Vec3 {
    yaw: number;
}

/** An axis-aligned box: what a location allows. */
export interface Bounds {
    minX: number;
    maxX: number;
    minY: number;
    maxY: number;
    minZ: number;
    maxZ: number;
}

export function isFiniteNumber(value: unknown): value is number {
    return typeof value === 'number' && Number.isFinite(value);
}

export function isVec3(value: unknown): value is Vec3 {
    const point = value as Partial<Vec3> | null;

    return (
        typeof point === 'object' &&
        point !== null &&
        isFiniteNumber(point.x) &&
        isFiniteNumber(point.y) &&
        isFiniteNumber(point.z)
    );
}

export function isPose(value: unknown): value is Pose {
    return isVec3(value) && isFiniteNumber((value as Partial<Pose>).yaw);
}

export function inside(bounds: Bounds, point: Vec3): boolean {
    return (
        point.x >= bounds.minX &&
        point.x <= bounds.maxX &&
        point.y >= bounds.minY &&
        point.y <= bounds.maxY &&
        point.z >= bounds.minZ &&
        point.z <= bounds.maxZ
    );
}

/** The point pulled inside the bounds (at least `margin` from the sides on X and Z). */
export function clampInto(bounds: Bounds, point: Vec3, margin = 0): Vec3 {
    const clamp = (value: number, min: number, max: number) =>
        Math.min(max, Math.max(min, value));

    return {
        x: clamp(point.x, bounds.minX + margin, bounds.maxX - margin),
        y: clamp(point.y, bounds.minY, bounds.maxY),
        z: clamp(point.z, bounds.minZ + margin, bounds.maxZ - margin),
    };
}

/** An angle brought into (-π, π]. */
export function wrapAngle(angle: number): number {
    const turned = Math.atan2(Math.sin(angle), Math.cos(angle));

    return turned === -Math.PI ? Math.PI : turned;
}
