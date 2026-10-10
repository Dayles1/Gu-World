/** Test fixtures shared by the unit tests (not tests themselves). */

/** Settings shaped like the server's (config/gu_world.php `world`). */
export function worldSettings(overrides: Record<string, unknown> = {}) {
    return {
        limits: { half_extent: 16000, min_y: -1000, max_y: 5000 },
        chunks: {
            size: 64,
            simulation_radius: 96,
            visual_radius: 192,
            margin: 16,
        },
        start: 'test_grounds',
        locations: {
            test_grounds: {
                bounds: {
                    min_x: -80,
                    max_x: 80,
                    min_y: -20,
                    max_y: 120,
                    min_z: -80,
                    max_z: 80,
                },
                spawn: { x: 0, y: 0, z: 12, yaw: Math.PI },
            },
        },
        ...overrides,
    };
}
