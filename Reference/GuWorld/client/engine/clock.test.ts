import { describe, expect, it } from 'vitest';
import {
    MAX_MINUTES,
    MINUTES_PER_DAY,
    NEW_GAME_MINUTES,
    WorldClock,
} from './clock';

describe('WorldClock', () => {
    it('starts a new game on the first morning, at 07:00', () => {
        const clock = WorldClock.newGame();

        expect(clock.minutes).toBe(NEW_GAME_MINUTES);
        expect(clock.day).toBe(1);
        expect(clock.label).toBe('07:00');
        expect(clock.phase).toBe('morning');
    });

    it('starts every new game in the morning, whatever the real time', () => {
        const games = Array.from({ length: 5 }, () => WorldClock.newGame());

        expect(games.every((clock) => clock.phase === 'morning')).toBe(true);
    });

    it('goes on from a saved minute', () => {
        const saved = 3 * MINUTES_PER_DAY + 21 * 60 + 30;
        const { clock, repaired } = WorldClock.fromSaved(saved);

        expect(repaired).toBe(false);
        expect(clock.minutes).toBe(saved);
        expect(clock.day).toBe(4);
        expect(clock.label).toBe('21:30');
        expect(clock.phase).toBe('night');
    });

    it.each([NaN, Infinity, -1, '420', null, undefined, MAX_MINUTES + 1])(
        'repairs a broken saved value (%s) to a new morning',
        (value) => {
            const { clock, repaired } = WorldClock.fromSaved(value);

            expect(repaired).toBe(true);
            expect(clock.minutes).toBe(NEW_GAME_MINUTES);
        },
    );

    it('runs at its rate and rolls over to the next day at midnight', () => {
        const { clock } = WorldClock.fromSaved(23 * 60 + 59, 2);

        clock.advance(1);

        expect(clock.day).toBe(2);
        expect(clock.label).toBe('00:01');
    });

    it.each([NaN, -5, 0, Infinity])('ignores a step of %s seconds', (step) => {
        const clock = WorldClock.newGame();

        clock.advance(step);

        expect(clock.minutes).toBe(NEW_GAME_MINUTES);
    });

    it.each([
        [4 * 60 + 59, 'night'],
        [5 * 60, 'dawn'],
        [7 * 60, 'morning'],
        [11 * 60, 'day'],
        [17 * 60, 'evening'],
        [20 * 60, 'night'],
    ])('at minute %i of the day it is %s', (minute, phase) => {
        expect(WorldClock.fromSaved(minute).clock.phase).toBe(phase);
    });

    it('gives the time of day as a share of the day', () => {
        expect(WorldClock.fromSaved(12 * 60).clock.timeOfDay).toBe(0.5);
    });
});
