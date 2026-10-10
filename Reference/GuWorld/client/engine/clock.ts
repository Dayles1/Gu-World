/**
 * The world's clock: game minutes since the world began. The day and the
 * time of day follow from it, so it is the one number that is saved.
 *
 * A new game always starts on the first morning (NEW_GAME_MINUTES); a
 * loaded game goes on from the saved minute. A saved value that is not a
 * finite, non-negative number of minutes is repaired to a new morning
 * rather than trusted.
 *
 * Time runs at `rate` game minutes per real second (one by default: a day
 * lasts 24 real minutes). Pure logic, no three.js or DOM.
 */

export const MINUTES_PER_DAY = 24 * 60;

/** A new game starts on day 1 at 07:00. */
export const NEW_GAME_MINUTES = 7 * 60;

/** Game minutes per real second. */
export const DEFAULT_RATE = 1;

/** The most minutes a clock holds (about 1900 game years): beyond that a save is broken. */
export const MAX_MINUTES = 1e9;

export type DayPhase = 'night' | 'dawn' | 'morning' | 'day' | 'evening';

/** Where each phase starts, in hours; the last one runs past midnight. */
const PHASES: [number, DayPhase][] = [
    [5, 'dawn'],
    [7, 'morning'],
    [11, 'day'],
    [17, 'evening'],
    [20, 'night'],
];

export function isValidMinutes(value: unknown): value is number {
    return (
        typeof value === 'number' &&
        Number.isFinite(value) &&
        value >= 0 &&
        value <= MAX_MINUTES
    );
}

export class WorldClock {
    private constructor(
        private total: number,
        readonly rate: number,
    ) {}

    /** A new game: the first morning. */
    static newGame(rate = DEFAULT_RATE): WorldClock {
        return new WorldClock(NEW_GAME_MINUTES, rate);
    }

    /** A loaded game's clock; `repaired` when the saved value could not be trusted. */
    static fromSaved(
        minutes: unknown,
        rate = DEFAULT_RATE,
    ): { clock: WorldClock; repaired: boolean } {
        return isValidMinutes(minutes)
            ? { clock: new WorldClock(minutes, rate), repaired: false }
            : { clock: WorldClock.newGame(rate), repaired: true };
    }

    /** Game minutes since the world began: what is saved. */
    get minutes(): number {
        return this.total;
    }

    /** The day, counted from 1. */
    get day(): number {
        return Math.floor(this.total / MINUTES_PER_DAY) + 1;
    }

    /** Minutes since midnight, 0 ≤ m < 1440. */
    get minuteOfDay(): number {
        return this.total % MINUTES_PER_DAY;
    }

    /** 0 at midnight, 0.25 at 06:00, 0.5 at noon. */
    get timeOfDay(): number {
        return this.minuteOfDay / MINUTES_PER_DAY;
    }

    get phase(): DayPhase {
        const hours = this.minuteOfDay / 60;
        let phase: DayPhase = 'night';

        for (const [from, name] of PHASES) {
            if (hours >= from) {
                phase = name;
            }
        }

        return phase;
    }

    /** "07:00". */
    get label(): string {
        const whole = Math.floor(this.minuteOfDay);

        return `${String(Math.floor(whole / 60)).padStart(2, '0')}:${String(whole % 60).padStart(2, '0')}`;
    }

    /** Lets `seconds` of real time pass; anything not a finite positive number is ignored. */
    advance(seconds: number): void {
        if (!Number.isFinite(seconds) || seconds <= 0) {
            return;
        }

        this.total = Math.min(MAX_MINUTES, this.total + seconds * this.rate);
    }
}
