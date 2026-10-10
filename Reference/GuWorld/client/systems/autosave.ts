/**
 * Saves the game every so often — counted in simulation time, so a
 * paused game is not saved over and over (pausing itself saves once; see
 * game.ts).
 */

import type { GameSystem } from '../engine/systems';

/** Seconds of play between saves. */
export const AUTOSAVE_EVERY = 30;

export function autosaveSystem(
    save: () => void,
    every = AUTOSAVE_EVERY,
): GameSystem {
    let since = 0;

    return {
        name: 'autosave',
        update(step) {
            since += step;

            if (since >= every) {
                since = 0;
                save();
            }
        },
    };
}
