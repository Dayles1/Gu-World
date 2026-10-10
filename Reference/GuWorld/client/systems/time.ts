/**
 * Moves the world's clock on by each simulation step — so it stops when
 * the loop is paused and never looks at the real time — and tells when
 * the part of the day changes.
 */

import type { WorldClock } from '../engine/clock';
import type { EventBus } from '../engine/events';
import type { GameSystem } from '../engine/systems';
import type { GameEvents } from '../game-events';

export function timeSystem(
    clock: WorldClock,
    events: EventBus<GameEvents>,
): GameSystem {
    let phase = clock.phase;

    return {
        name: 'clock',
        update(step) {
            clock.advance(step);

            if (clock.phase !== phase) {
                phase = clock.phase;
                events.emit('clock:phase', { phase, day: clock.day });
            }
        },
    };
}
