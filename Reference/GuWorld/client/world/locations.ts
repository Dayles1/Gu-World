/**
 * A location as the game uses it: its world settings (bounds, spawn —
 * from the server) joined with its content (ground, things standing on
 * it — from content/). A location the server lists but the client has no
 * content for is an error, said plainly, not an empty world.
 */

import { testGrounds } from '../content/test-grounds';
import type { LocationContent } from '../content/types';
import type { LocationConfig, LocationId, WorldConfig } from './config';

export interface Location extends LocationConfig, LocationContent {}

/** Every location the client can build, by id. */
export const CONTENT: Record<LocationId, LocationContent> = {
    test_grounds: testGrounds,
};

export function resolveLocation(
    config: WorldConfig,
    id: LocationId,
    content: Record<LocationId, LocationContent> = CONTENT,
): Location {
    const settings = config.locations.get(id);

    if (!settings) {
        throw new Error(`There is no location "${id}" in the world's settings`);
    }

    const made = content[id];

    if (!made) {
        throw new Error(`The client cannot build the location "${id}"`);
    }

    return { ...settings, ...made };
}
