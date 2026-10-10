import { describe, expect, it } from 'vitest';
import { MINUTES_PER_DAY, NEW_GAME_MINUTES, WorldClock } from '../engine/clock';
import { worldSettings } from '../testing/world-settings';
import { parseWorldConfig } from '../world/config';
import {
    checkSave,
    makeSave,
    newGame,
    restoreGame,
    SAVE_VERSION,
} from './save';

const config = parseWorldConfig(worldSettings());
const spawn = { x: 0, y: 0, z: 12, yaw: Math.PI };

function save(overrides: Record<string, unknown> = {}) {
    return {
        version: SAVE_VERSION,
        location: 'test_grounds',
        world_minutes: 2 * MINUTES_PER_DAY + 15 * 60,
        player: { x: 10, y: 1, z: -20, yaw: 0.5 },
        ...overrides,
    };
}

describe('a new game', () => {
    it('starts on the first morning at the starting location spawn', () => {
        const game = newGame(config);

        expect(game).toMatchObject({
            location: 'test_grounds',
            player: spawn,
            fresh: true,
            problems: [],
        });
        expect(game.clock.label).toBe('07:00');
        expect(game.clock.phase).toBe('morning');
    });

    it('is what no save at all gives', () => {
        for (const nothing of [null, undefined]) {
            const game = restoreGame(nothing, config);

            expect(game.fresh).toBe(true);
            expect(game.clock.minutes).toBe(NEW_GAME_MINUTES);
        }
    });
});

describe('restoring a save', () => {
    it('gives back the location, the time and where the hero stood', () => {
        const game = restoreGame(save(), config);

        expect(game).toMatchObject({
            location: 'test_grounds',
            player: { x: 10, y: 1, z: -20, yaw: 0.5 },
            fresh: false,
            problems: [],
        });
        expect(game.clock.day).toBe(3);
        expect(game.clock.label).toBe('15:00');
    });

    it('ignores fields it does not know', () => {
        const game = restoreGame(save({ admin: true, gold: 1e9 }), config);

        expect(game.problems).toEqual([]);
        expect(game).not.toHaveProperty('gold');
    });

    it.each([
        ['NaN', { x: NaN, y: 0, z: 0, yaw: 0 }],
        ['Infinity', { x: 0, y: Infinity, z: 0, yaw: 0 }],
        ['strings', { x: '1', y: 0, z: 0, yaw: 0 }],
        ['no yaw', { x: 1, y: 0, z: 0 }],
        ['nothing', null],
    ])(
        'puts a hero with a broken position (%s) back at the spawn, saying why',
        (_, player) => {
            const game = restoreGame(save({ player }), config);

            expect(game.player).toEqual(spawn);
            expect(game.problems).toEqual([
                'broken position: back to the spawn',
            ]);
            // The rest of the save is still used.
            expect(game.clock.label).toBe('15:00');
        },
    );

    it('puts a hero outside the location back at the spawn', () => {
        const game = restoreGame(
            save({ player: { x: 5000, y: 0, z: 0, yaw: 0 } }),
            config,
        );

        expect(game.player).toEqual(spawn);
        expect(game.problems).toEqual([
            'position out of the location: back to the spawn',
        ]);
    });

    it.each([NaN, -60, 'noon', null, 1e12])(
        'repairs a broken time (%s) to the first morning, keeping the rest',
        (minutes) => {
            const game = restoreGame(save({ world_minutes: minutes }), config);

            expect(game.clock.minutes).toBe(NEW_GAME_MINUTES);
            expect(game.problems).toHaveLength(1);
            expect(game.problems[0]).toMatch(/broken time/);
            expect(game.player.x).toBe(10);
        },
    );

    it('sends a hero from an unknown location to the starting one', () => {
        const game = restoreGame(save({ location: 'atlantis' }), config);

        expect(game.location).toBe('test_grounds');
        expect(game.player).toEqual(spawn);
        expect(game.problems[0]).toMatch(/unknown location "atlantis"/);
    });

    it('does not trust a location the client cannot build', () => {
        const game = restoreGame(save(), config, () => false);

        expect(game.player).toEqual(spawn);
        expect(game.problems).toHaveLength(1);
    });

    it.each([
        ['an unknown format', save({ version: 99 })],
        ['not an object', 'save'],
        ['a list', [1, 2, 3]],
    ])('starts afresh from %s, saying why', (_, broken) => {
        const game = restoreGame(broken, config);

        expect(game.fresh).toBe(false);
        expect(game.player).toEqual(spawn);
        expect(game.clock.minutes).toBe(NEW_GAME_MINUTES);
        expect(game.problems).toHaveLength(1);
    });

    it('keeps the facing within a turn', () => {
        const game = restoreGame(
            save({ player: { x: 0, y: 0, z: 0, yaw: 5 * Math.PI } }),
            config,
        );

        expect(game.player.yaw).toBeCloseTo(Math.PI);
    });
});

describe("the world's changes in a save", () => {
    it('come back with the save', () => {
        const game = restoreGame(
            save({
                changes: {
                    'stone:test_grounds:g3_-2': { state: { marked: true } },
                    'block:test_grounds:crate': { removed: true },
                },
            }),
            config,
        );

        expect(game.problems).toEqual([]);
        expect(game.changes.state('stone:test_grounds:g3_-2')).toEqual({
            marked: true,
        });
        expect(game.changes.isRemoved('block:test_grounds:crate')).toBe(true);
    });

    it('lose only their broken parts, saying why', () => {
        const game = restoreGame(
            save({
                changes: {
                    'stone:a': { state: { marked: true, nan: null } },
                    'Not An Id': { removed: true },
                },
            }),
            config,
        );

        expect(game.changes.state('stone:a')).toEqual({ marked: true });
        expect(game.problems).toHaveLength(2);
    });

    it('mean nothing in a location that could not be used', () => {
        const game = restoreGame(
            save({
                location: 'atlantis',
                changes: { 'stone:a': { removed: true } },
            }),
            config,
        );

        expect(game.changes.size).toBe(0);
    });

    it('are simply empty in a format-1 save, which is still read', () => {
        const old: Record<string, unknown> = save({ version: 1 });
        delete old.changes;
        const game = restoreGame(old, config);

        expect(game.problems).toEqual([]);
        expect(game.changes.size).toBe(0);
        expect(game.player.x).toBe(10);
    });
});

describe('making a save', () => {
    it('writes what restoring reads back, unchanged', () => {
        const { clock } = WorldClock.fromSaved(5000);
        const written = makeSave('test_grounds', clock, {
            x: 1.5,
            y: 2,
            z: -3,
            yaw: 0.25,
        });
        const game = restoreGame(JSON.parse(JSON.stringify(written)), config);

        expect(checkSave(written, config)).toEqual([]);
        expect(game.clock.minutes).toBe(5000);
        expect(game.player).toEqual({ x: 1.5, y: 2, z: -3, yaw: 0.25 });
        expect(game.problems).toEqual([]);
    });

    it('finds what is wrong with a save before it is sent', () => {
        const clock = WorldClock.newGame();

        expect(
            checkSave(
                makeSave('test_grounds', clock, { x: NaN, y: 0, z: 0, yaw: 0 }),
                config,
            ),
        ).toEqual(['broken position']);
        expect(
            checkSave(
                makeSave('test_grounds', clock, { x: 900, y: 0, z: 0, yaw: 0 }),
                config,
            ),
        ).toEqual(['position out of the location']);
        expect(checkSave(makeSave('nowhere', clock, spawn), config)).toEqual([
            'unknown location',
        ]);
    });
});
