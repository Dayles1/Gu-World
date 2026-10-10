/** Everything GU World's systems tell each other (see engine/events.ts). */

import type { DayPhase } from './engine/clock';
import type { PauseReason } from './engine/loop';

export interface GameEvents {
    'loop:paused': { reasons: PauseReason[] };
    'loop:resumed': Record<string, never>;
    /** The simulation fell behind; `dropped` seconds were skipped. */
    'loop:overrun': { dropped: number };
    'system:failed': { system: string; error: string; failures: number };
    'system:disabled': { system: string; error: string };
    'clock:phase': { phase: DayPhase; day: number };
    'save:written': Record<string, never>;
    'save:failed': { message: string };
    'save:refused': { problems: string[] };
    /** Parts of the loaded save could not be used and were replaced. */
    'state:repaired': { problems: string[] };
}
