/**
 * The game loop: the world is simulated in fixed steps (SIM_STEP), the
 * picture is drawn once per display frame, between the last two steps
 * (`alpha`), so motion is smooth at any refresh rate and the simulation
 * behaves the same at any frame rate. Physics runs its own finer fixed
 * step inside a simulation step (see physics/character.ts).
 *
 * - A long frame (a hitch, a breakpoint) counts as at most MAX_FRAME;
 *   at most MAX_STEPS steps run per frame, and time beyond that is
 *   dropped (reported), so a slow machine never spirals.
 * - Pausing stops the simulation (and so the world's clock); drawing goes
 *   on. Pauses have reasons — the player, a hidden tab, a window out of
 *   focus — and the loop runs again only when none is left. Resuming
 *   starts timing afresh: no jump in time or position.
 *
 * Time comes from a Scheduler, so tests drive the loop frame by frame.
 */

export const SIM_STEP = 1 / 60;
/** The longest a frame may count, seconds. */
export const MAX_FRAME = 0.25;
/** The most simulation steps run in one frame. */
export const MAX_STEPS = 8;

export interface LoopHandlers {
    /** One fixed simulation step of `step` seconds. */
    tick(step: number): void;
    /**
     * Draw the frame. `alpha` (0…1) is how far time is between the last
     * step and the next; `frame` is the frame's real time, seconds.
     */
    render(alpha: number, frame: number): void;
    /** The simulation fell behind and `dropped` seconds were skipped. */
    overrun?(dropped: number): void;
}

export interface Scheduler {
    request(callback: (time: number) => void): number;
    cancel(handle: number): void;
}

export const browserScheduler: Scheduler = {
    request: (callback) => requestAnimationFrame(callback),
    cancel: (handle) => cancelAnimationFrame(handle),
};

export type PauseReason = 'player' | 'hidden' | 'focus' | 'loading';

export class GameLoop {
    /** Simulation steps run so far. */
    ticks = 0;

    private pauses = new Set<PauseReason>();
    private accumulator = 0;
    /** Time of the last frame, ms; null when timing starts afresh. */
    private last: number | null = null;
    private handle: number | null = null;

    constructor(
        private handlers: LoopHandlers,
        private scheduler: Scheduler = browserScheduler,
        private step = SIM_STEP,
    ) {}

    get running(): boolean {
        return this.handle !== null;
    }

    get paused(): boolean {
        return this.pauses.size > 0;
    }

    get pauseReasons(): PauseReason[] {
        return [...this.pauses];
    }

    start(): void {
        if (this.running) {
            return;
        }

        this.last = null;
        this.handle = this.scheduler.request(this.onFrame);
    }

    stop(): void {
        if (this.handle !== null) {
            this.scheduler.cancel(this.handle);
            this.handle = null;
        }
    }

    pause(reason: PauseReason): void {
        this.pauses.add(reason);
    }

    resume(reason: PauseReason): void {
        if (!this.pauses.delete(reason) || this.paused) {
            return;
        }

        // Time paused does not count: the next frame starts afresh.
        this.accumulator = 0;
        this.last = null;
    }

    /** One display frame at `time` (ms). Public so tests can drive it. */
    frame(time: number): void {
        const elapsed =
            this.last === null ? 0 : Math.max(0, (time - this.last) / 1000);
        const frame = Math.min(elapsed, MAX_FRAME);

        this.last = time;

        if (!this.paused) {
            this.accumulator += frame;
            let steps = 0;

            while (this.accumulator >= this.step) {
                if (steps === MAX_STEPS) {
                    const dropped = this.accumulator;
                    this.accumulator = 0;
                    this.handlers.overrun?.(dropped);
                    break;
                }

                this.handlers.tick(this.step);
                this.accumulator -= this.step;
                this.ticks++;
                steps++;
            }
        }

        this.handlers.render(
            this.paused ? 1 : this.accumulator / this.step,
            frame,
        );
    }

    private onFrame = (time: number): void => {
        // Asked for the next frame first, so a throwing handler cannot stop the loop.
        this.handle = this.scheduler.request(this.onFrame);
        this.frame(time);
    };
}

/** What the page tells the loop: a hidden tab and a window out of focus pause it. */
export interface PageEvents {
    document: EventTarget & { hidden: boolean };
    window: EventTarget;
}

/** Pauses the loop while the tab is hidden or the window out of focus; returns the way to stop watching. */
export function watchPage(loop: GameLoop, page: PageEvents): () => void {
    const visibility = () =>
        page.document.hidden ? loop.pause('hidden') : loop.resume('hidden');
    const blur = () => loop.pause('focus');
    const focus = () => loop.resume('focus');

    page.document.addEventListener('visibilitychange', visibility);
    page.window.addEventListener('blur', blur);
    page.window.addEventListener('focus', focus);
    visibility();

    return () => {
        page.document.removeEventListener('visibilitychange', visibility);
        page.window.removeEventListener('blur', blur);
        page.window.removeEventListener('focus', focus);
    };
}
