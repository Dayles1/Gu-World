import { describe, expect, it } from 'vitest';
import { WorldClock } from '../engine/clock';
import { dayLight } from './sky';

describe('dayLight', () => {
    it('has the sun up and bright on the first morning of a new game', () => {
        const light = dayLight(WorldClock.newGame().timeOfDay);

        expect(light.sunDirection.y).toBeGreaterThan(0.2);
        expect(light.sunIntensity).toBeGreaterThan(1.5);
        // Low in the east.
        expect(light.sunDirection.x).toBeGreaterThan(0.5);
    });

    it('has no sun at midnight and a dark sky', () => {
        const light = dayLight(0);

        expect(light.sunIntensity).toBe(0);
        expect(light.sky.getHSL({ h: 0, s: 0, l: 0 }).l).toBeLessThan(0.1);
    });

    it('is brightest at noon', () => {
        const noon = dayLight(0.5);

        expect(noon.sunDirection.y).toBeGreaterThan(0.9);
        expect(noon.sunIntensity).toBeGreaterThan(
            dayLight(0.3).sunIntensity - 1e-9,
        );
    });

    it('is the same light for the same minute', () => {
        expect(dayLight(0.31)).toEqual(dayLight(0.31));
    });
});
