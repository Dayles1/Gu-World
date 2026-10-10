import * as THREE from 'three';
import { describe, expect, it } from 'vitest';
import { testGrounds } from '../content/test-grounds';
import { worldSettings } from '../testing/world-settings';
import { MAX_HALF_EXTENT, parseWorldConfig, WorldConfigError } from './config';
import { resolveLocation } from './locations';
import { clampInto, inside, isPose, isVec3, wrapAngle } from './space';

describe('space', () => {
    it('knows a point from a broken one', () => {
        expect(isVec3({ x: 1, y: 2, z: 3 })).toBe(true);
        expect(isVec3({ x: 1, y: NaN, z: 3 })).toBe(false);
        expect(isVec3({ x: 1, y: Infinity, z: 3 })).toBe(false);
        expect(isVec3({ x: '1', y: 2, z: 3 })).toBe(false);
        expect(isVec3(null)).toBe(false);
        expect(isPose({ x: 1, y: 2, z: 3, yaw: 0 })).toBe(true);
        expect(isPose({ x: 1, y: 2, z: 3 })).toBe(false);
    });

    it('keeps points inside a box', () => {
        const box = {
            minX: -10,
            maxX: 10,
            minY: 0,
            maxY: 5,
            minZ: -10,
            maxZ: 10,
        };

        expect(inside(box, { x: 10, y: 5, z: -10 })).toBe(true);
        expect(inside(box, { x: 10.1, y: 0, z: 0 })).toBe(false);
        expect(clampInto(box, { x: 50, y: -3, z: -50 }, 1)).toEqual({
            x: 9,
            y: 0,
            z: -9,
        });
    });

    it('brings angles into (-π, π]', () => {
        expect(wrapAngle(3 * Math.PI)).toBeCloseTo(Math.PI);
        expect(wrapAngle(-Math.PI)).toBe(Math.PI);
        expect(wrapAngle(Math.PI / 2 + 4 * Math.PI)).toBeCloseTo(Math.PI / 2);
    });
});

describe('world settings', () => {
    it('reads the locations, their bounds and spawns, in metres', () => {
        const config = parseWorldConfig(worldSettings());
        const location = config.locations.get('test_grounds')!;

        expect(config.start).toBe('test_grounds');
        expect(config.limits.halfExtent).toBe(16000);
        expect(location.bounds).toEqual({
            minX: -80,
            maxX: 80,
            minY: -20,
            maxY: 120,
            minZ: -80,
            maxZ: 80,
        });
        expect(location.spawn).toEqual({ x: 0, y: 0, z: 12, yaw: Math.PI });
    });

    it('grows by data: a wide location of a 20 km region is just settings', () => {
        const config = parseWorldConfig(
            worldSettings({
                locations: {
                    ...worldSettings().locations,
                    region: {
                        bounds: {
                            min_x: -10000,
                            max_x: 10000,
                            min_y: -200,
                            max_y: 3000,
                            min_z: -10000,
                            max_z: 10000,
                        },
                        spawn: { x: 9000, y: 40, z: -9000, yaw: 0 },
                    },
                },
            }),
        );

        expect(config.locations.get('region')?.bounds.maxX).toBe(10000);
    });

    it.each([
        ['no settings at all', null],
        ['a start that is not a location', worldSettings({ start: 'nowhere' })],
        [
            'limits past what the engine holds',
            worldSettings({
                limits: {
                    half_extent: MAX_HALF_EXTENT + 1,
                    min_y: 0,
                    max_y: 1,
                },
            }),
        ],
        [
            'a spawn outside the bounds',
            worldSettings({
                locations: {
                    test_grounds: {
                        ...worldSettings().locations.test_grounds,
                        spawn: { x: 500, y: 0, z: 0, yaw: 0 },
                    },
                },
            }),
        ],
        [
            'bounds that are not a box',
            worldSettings({
                locations: {
                    test_grounds: {
                        ...worldSettings().locations.test_grounds,
                        bounds: {
                            min_x: 5,
                            max_x: -5,
                            min_y: 0,
                            max_y: 1,
                            min_z: 0,
                            max_z: 1,
                        },
                    },
                },
            }),
        ],
        [
            'a bad location id',
            worldSettings({
                locations: {
                    'Bad Id': worldSettings().locations.test_grounds,
                },
                start: 'Bad Id',
            }),
        ],
        [
            'a NaN in the bounds',
            worldSettings({
                locations: {
                    test_grounds: {
                        ...worldSettings().locations.test_grounds,
                        bounds: {
                            min_x: NaN,
                            max_x: 5,
                            min_y: 0,
                            max_y: 1,
                            min_z: 0,
                            max_z: 1,
                        },
                    },
                },
            }),
        ],
    ])('refuses %s, saying why', (_, settings) => {
        expect(() => parseWorldConfig(settings)).toThrow(WorldConfigError);
    });
});

describe('locations', () => {
    it('joins the settings with what the client builds there', () => {
        const location = resolveLocation(
            parseWorldConfig(worldSettings()),
            'test_grounds',
        );

        expect(location.spawn.z).toBe(12);
        expect(
            location.objectsIn({ minX: -20, maxX: 20, minZ: -20, maxZ: 20 })
                .length,
        ).toBeGreaterThan(0);
        // Flat away from the mounds.
        expect(location.ground.heightAt(0, 40)).toBeCloseTo(0, 3);
    });

    it('says so when the client cannot build a location the server lists', () => {
        const config = parseWorldConfig(
            worldSettings({
                locations: {
                    ...worldSettings().locations,
                    elsewhere: worldSettings().locations.test_grounds,
                },
            }),
        );

        expect(() => resolveLocation(config, 'elsewhere')).toThrow(
            /cannot build/,
        );
    });

    it('gives every thing of the proving ground its own stable id', () => {
        const all = { minX: -256, maxX: 256, minZ: -256, maxZ: 256 };
        const ids = testGrounds.objectsIn(all).map((thing) => thing.id);

        expect(new Set(ids).size).toBe(ids.length);
        expect(ids).toContain('block:test_grounds:crate');
        expect(
            ids.filter((id) => id.startsWith('stone:')).length,
        ).toBeGreaterThan(100);
    });

    it('hands out the same things whatever the areas are cut into', () => {
        const ids = (size: number) => {
            const found: string[] = [];

            for (let x = -256; x < 256; x += size) {
                for (let z = -256; z < 256; z += size) {
                    found.push(
                        ...testGrounds
                            .objectsIn({
                                minX: x,
                                maxX: x + size,
                                minZ: z,
                                maxZ: z + size,
                            })
                            .map((thing) => thing.id),
                    );
                }
            }

            return found.sort();
        };

        // Each thing once, and the same things, for 32, 64 and 128 m areas.
        expect(new Set(ids(32)).size).toBe(ids(32).length);
        expect(ids(32)).toEqual(ids(64));
        expect(ids(64)).toEqual(ids(128));
    });

    it('has a gentle mound one can walk up and a steep one one cannot', () => {
        const normal = new THREE.Vector3();
        const steepest = (cx: number, cz: number) => {
            let lowest = 1;

            for (let r = 0; r < 20; r += 0.25) {
                lowest = Math.min(
                    lowest,
                    testGrounds.ground.normalAt(cx + r, cz, normal).y,
                );
            }

            return (Math.acos(lowest) * 180) / Math.PI;
        };

        expect(steepest(24, -24)).toBeLessThan(25);
        expect(steepest(-24, -24)).toBeGreaterThan(50);
    });
});
