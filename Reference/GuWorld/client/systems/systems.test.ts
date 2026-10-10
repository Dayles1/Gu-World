import { describe, expect, it, vi } from 'vitest';
import { NEW_GAME_MINUTES } from '../engine/clock';
import { EventBus } from '../engine/events';
import { GameLoop, SIM_STEP } from '../engine/loop';
import { SystemRunner } from '../engine/systems';
import type { GameEvents } from '../game-events';
import { newGame, restoreGame } from '../state/save';
import { buildWorld, saveWorld } from '../state/world';
import type { World } from '../state/world';
import { worldSettings } from '../testing/world-settings';
import { ChunkManager } from '../world/chunk-manager';
import { parseWorldConfig } from '../world/config';
import { autosaveSystem } from './autosave';
import { PlayerSystem } from './player';
import type { Controls } from './player';
import { timeSystem } from './time';

const config = parseWorldConfig(worldSettings());
const look = {
    gender: 'male' as const,
    style: 'realistic' as const,
    appearance: {
        hair: 'parted' as const,
        hair_color: 'brown' as const,
        beard: 'none' as const,
        eyes: 'brown' as const,
    },
};

function keys(forward = 0): Controls {
    return {
        forward: () => forward,
        strafe: () => 0,
        sprint: () => false,
        rise: () => false,
        dive: () => false,
        // The camera looks along -Z.
        axes: () => ({ forwardX: 0, forwardZ: -1, rightX: 1, rightZ: 0 }),
    };
}

/** The chunks of a world, loaded around the hero (nothing drawn). */
function chunks(world: World) {
    let time = 0;
    const manager = new ChunkManager({
        location: world.location.id,
        bounds: world.location.bounds,
        content: world.location,
        settings: world.config.chunks,
        registry: world.registry,
        colliders: world.colliders,
        changes: world.changes,
        views: { show: () => {}, drop: () => {} },
        painter: { paint: () => ({ step: () => true }), erase: () => {} },
        now: () => (time += 0.1),
    });
    const { x, z } = world.player.position;

    for (let i = 0; i < 500 && !manager.ready(x, z); i++) {
        manager.update(x, z, 5);
    }

    return manager;
}

/** A world run by a real loop and runner, frames driven by hand. */
function play(world: World, controls: Controls) {
    const events = new EventBus<GameEvents>();
    const failed = vi.fn();
    const runner = new SystemRunner({ failed, disabled: vi.fn() });
    const player = new PlayerSystem(world, controls);

    runner.add(timeSystem(world.clock, events));
    runner.add(player);

    const loop = new GameLoop(
        { tick: (step) => runner.run(step), render: () => {} },
        { request: () => 0, cancel: () => {} },
    );
    let now = 0;
    const seconds = (time: number) => {
        for (let t = 0; t < time; t += 0.05) {
            now += 50;
            loop.frame(now);
        }
    };

    loop.frame(0);

    return { events, runner, loop, player, seconds, failed };
}

describe('the world, played', () => {
    it('builds the same entities, with the same ids, from the same save', () => {
        const a = buildWorld(config, newGame(config), look);
        const b = buildWorld(config, newGame(config), look);
        const ids = (world: World) =>
            [...world.registry.all()].map((e) => e.id).sort();

        chunks(a);
        chunks(b);

        expect(ids(a)).toEqual(ids(b));
        expect(ids(a)).toContain('player');
        expect(ids(a)).toContain('block:test_grounds:crate');
    });

    it('never doubles anything when built and loaded twice into one registry', () => {
        const first = buildWorld(config, newGame(config), look);

        chunks(first);
        const size = first.registry.size;

        chunks(buildWorld(config, newGame(config), look, first.registry));

        expect(first.registry.size).toBe(size);
    });

    it('keeps what changed in the world through a save and a reload', () => {
        const world = buildWorld(config, newGame(config), look);
        const loaded = chunks(world);
        const crate = world.registry.get('block:test_grounds:crate');

        if (crate?.kind === 'block') {
            crate.state.marked = true;
        }

        loaded.capture();
        const saved = JSON.parse(JSON.stringify(saveWorld(world)));
        const again = buildWorld(config, restoreGame(saved, config), look);

        chunks(again);

        expect(again.registry.get('block:test_grounds:crate')).toMatchObject({
            state: { marked: true },
        });
    });

    it('lets time run only while the loop runs', () => {
        const world = buildWorld(config, newGame(config), look);
        const { loop, seconds } = play(world, keys());

        seconds(2);
        const ran = world.clock.minutes;

        loop.pause('player');
        seconds(5);

        expect(ran).toBeGreaterThan(NEW_GAME_MINUTES);
        expect(world.clock.minutes).toBe(ran);
    });

    it('walks the hero where the keys say, through the registry', () => {
        const world = buildWorld(config, newGame(config), look);
        const { seconds } = play(world, keys(1));
        const startZ = world.player.position.z;

        seconds(1);

        expect(world.player.position.z).toBeLessThan(startZ - 3);
        expect(world.player.motion.speed).toBeGreaterThan(3);
        expect(world.registry.get('player')?.position).toEqual(
            world.player.position,
        );
    });

    it('saves and restores the hero, the location and the time', () => {
        const world = buildWorld(config, newGame(config), look);
        const { seconds } = play(world, keys(1));

        seconds(1.5);
        const saved = JSON.parse(JSON.stringify(saveWorld(world)));
        const again = buildWorld(config, restoreGame(saved, config), look);

        expect(again.player.position.x).toBeCloseTo(world.player.position.x, 5);
        expect(again.player.position.z).toBeCloseTo(world.player.position.z, 5);
        expect(again.clock.minutes).toBe(world.clock.minutes);
        expect(again.location.id).toBe('test_grounds');
    });

    it('puts a hero whose body breaks back on the spawn, reporting it, and goes on', () => {
        const world = buildWorld(config, newGame(config), look);
        const { seconds, failed, runner } = play(world, keys(1));

        world.body.position.x = NaN;
        seconds(0.2);

        expect(failed).toHaveBeenCalledWith(
            'player',
            "the hero's body broke: back to the spawn",
            1,
        );
        expect(world.player.position.x).toBeCloseTo(
            config.locations.get('test_grounds')!.spawn.x,
            1,
        );
        expect(runner.status().find((s) => s.name === 'player')?.enabled).toBe(
            true,
        );
        // And time went on all along.
        expect(world.clock.minutes).toBeGreaterThan(NEW_GAME_MINUTES);
    });

    it('keeps the engine running when one system keeps failing', () => {
        const world = buildWorld(config, newGame(config), look);
        const { runner, seconds } = play(world, keys(1));
        const startZ = world.player.position.z;

        runner.add({
            name: 'broken',
            update() {
                throw new Error('always');
            },
        });
        seconds(1);

        expect(runner.status().find((s) => s.name === 'broken')?.enabled).toBe(
            false,
        );
        expect(world.player.position.z).toBeLessThan(startZ - 3);
    });

    it('autosaves every so often of play time', () => {
        const save = vi.fn();
        const autosave = autosaveSystem(save, 1);

        for (let i = 0; i < Math.round(2.5 / SIM_STEP); i++) {
            autosave.update(SIM_STEP);
        }

        expect(save).toHaveBeenCalledTimes(2);
    });
});
