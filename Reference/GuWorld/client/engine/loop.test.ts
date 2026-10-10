import { describe, expect, it, vi } from 'vitest';
import { GameLoop, MAX_FRAME, MAX_STEPS, SIM_STEP, watchPage } from './loop';
import type { Scheduler } from './loop';

/** A loop driven by hand: frames at the times the test says (ms). */
function loop() {
    const ticks: number[] = [];
    const renders: { alpha: number; frame: number }[] = [];
    const overrun = vi.fn();
    const scheduler: Scheduler = { request: () => 1, cancel: () => {} };
    const game = new GameLoop(
        {
            tick: (step) => ticks.push(step),
            render: (alpha, frame) => renders.push({ alpha, frame }),
            overrun,
        },
        scheduler,
    );

    return { game, ticks, renders, overrun };
}

const ms = (seconds: number) => seconds * 1000;

describe('GameLoop', () => {
    it('steps the simulation in fixed steps, whatever the frame rate', () => {
        const { game, ticks } = loop();

        game.frame(0);

        // 0.25 s in uneven frames…
        for (const t of [0.013, 0.05, 0.051, 0.12, 0.25]) {
            game.frame(ms(t));
        }

        // …is 15 steps of 1/60 s, each exactly that long.
        expect(ticks).toHaveLength(15);
        expect(new Set(ticks)).toEqual(new Set([SIM_STEP]));
    });

    it('draws every frame, between the last two steps', () => {
        const { game, renders } = loop();

        game.frame(0);
        game.frame(ms(SIM_STEP * 1.5));

        expect(renders).toHaveLength(2);
        expect(renders[1].alpha).toBeCloseTo(0.5);
    });

    it('counts a long frame as at most MAX_FRAME and never spirals', () => {
        const { game, ticks, overrun } = loop();

        game.frame(0);
        game.frame(ms(30)); // a 30-second hitch

        expect(ticks.length).toBeLessThanOrEqual(MAX_STEPS);
        expect(overrun).toHaveBeenCalledTimes(1);
        // What was dropped is less than one long frame, never the 30 s.
        expect(overrun.mock.calls[0][0]).toBeLessThanOrEqual(MAX_FRAME);
    });

    it('ignores time going backwards', () => {
        const { game, ticks } = loop();

        game.frame(ms(10));
        game.frame(ms(5));

        expect(ticks).toHaveLength(0);
    });

    it('stops the simulation while paused but goes on drawing', () => {
        const { game, ticks, renders } = loop();

        game.frame(0);
        game.pause('player');
        game.frame(ms(1));
        game.frame(ms(2));

        expect(ticks).toHaveLength(0);
        expect(renders).toHaveLength(3);
    });

    it('resumes without a jump: paused time does not count', () => {
        const { game, ticks } = loop();

        game.frame(0);
        game.pause('player');
        game.frame(ms(0.1));
        game.resume('player');
        game.frame(ms(60)); // a minute later — but timing starts afresh
        game.frame(ms(60) + ms(SIM_STEP * 1.5));

        // One step and a half of time since: one step, not a minute's worth.
        expect(ticks).toHaveLength(1);
    });

    it('runs again only when every reason to pause is gone', () => {
        const { game } = loop();

        game.pause('player');
        game.pause('hidden');
        game.resume('hidden');

        expect(game.paused).toBe(true);
        expect(game.pauseReasons).toEqual(['player']);

        game.resume('player');

        expect(game.paused).toBe(false);
    });
});

describe('watchPage', () => {
    function page() {
        const document = Object.assign(new EventTarget(), { hidden: false });
        const window = new EventTarget();

        return { document, window };
    }

    it('pauses while the tab is hidden and resumes when it is back', () => {
        const { game } = loop();
        const events = page();

        watchPage(game, events);
        events.document.hidden = true;
        events.document.dispatchEvent(new Event('visibilitychange'));

        expect(game.pauseReasons).toEqual(['hidden']);

        events.document.hidden = false;
        events.document.dispatchEvent(new Event('visibilitychange'));

        expect(game.paused).toBe(false);
    });

    it('pauses while the window is out of focus', () => {
        const { game } = loop();
        const events = page();

        watchPage(game, events);
        events.window.dispatchEvent(new Event('blur'));

        expect(game.pauseReasons).toEqual(['focus']);

        events.window.dispatchEvent(new Event('focus'));

        expect(game.paused).toBe(false);
    });

    it('stops listening when asked', () => {
        const { game } = loop();
        const events = page();
        const stop = watchPage(game, events);

        stop();
        events.window.dispatchEvent(new Event('blur'));

        expect(game.paused).toBe(false);
    });
});
