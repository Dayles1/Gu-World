import { describe, expect, it } from 'vitest';
import type { LocationContent } from '../content/types';
import { anchoredIn } from '../content/types';
import { EntityRegistry } from '../entities/registry';
import type { ContentEntity, GameEntity } from '../entities/types';
import { ColliderGrid } from '../physics/colliders';
import { WorldChanges } from './changes';
import { ChunkManager } from './chunk-manager';
import type { ChunkSettings } from './config';
import { heightField } from './ground';

const bounds = {
    minX: -512,
    maxX: 512,
    minY: -10,
    maxY: 100,
    minZ: -512,
    maxZ: 512,
};
const flat = heightField(() => 0);

/** A stone every 8 m along X at z = 4, ids named after their spot (not any chunk). */
const content: LocationContent = {
    ground: flat,
    objectsIn(area) {
        const found = [];

        for (let x = -504; x < 512; x += 8) {
            if (anchoredIn(area, x, 4)) {
                found.push({
                    kind: 'stone' as const,
                    id: `stone:test:x${x}`,
                    x,
                    z: 4,
                    radius: 0.5,
                });
            }
        }

        return found;
    },
};

/** A world around a fake clock; painting a chunk takes `stepsPerChunk` steps of `stepMs` each. */
function world(
    settings: Partial<ChunkSettings> = {},
    stepsPerChunk = 3,
    stepMs = 2,
) {
    let time = 0;
    const registry = new EntityRegistry<GameEntity>();
    const colliders = new ColliderGrid(flat);
    const changes = new WorldChanges();
    const shown = new Set<string>();
    const views = {
        shows: 0,
        show(id: string) {
            if (!shown.has(id)) {
                shown.add(id);
                this.shows++;
            }
        },
        drop: (id: string) => shown.delete(id),
    };
    const painted = new Set<string>();
    const painter = {
        steps: 0,
        paint(key: string) {
            let left = stepsPerChunk;

            return {
                step: (deadline: number) => {
                    do {
                        time += stepMs;
                        painter.steps++;
                        left--;
                    } while (left > 0 && time < deadline);

                    if (left === 0) {
                        painted.add(key);
                    }

                    return left === 0;
                },
            };
        },
        erase: (key: string) => painted.delete(key),
    };
    const problems: string[] = [];
    const manager = new ChunkManager({
        location: 'test',
        bounds,
        content,
        settings: {
            size: 32,
            simulationRadius: 48,
            visualRadius: 96,
            margin: 16,
            ...settings,
        },
        registry,
        colliders,
        changes,
        views,
        painter,
        now: () => time,
        alwaysShown: ['player'],
        problem: (message) => problems.push(message),
    });
    const settle = (x: number, z: number, frames = 200) => {
        for (let i = 0; i < frames; i++) {
            manager.update(x, z, 4);
        }
    };

    return {
        manager,
        registry,
        colliders,
        changes,
        shown,
        views,
        painted,
        painter,
        problems,
        settle,
        clock: () => time,
    };
}

const stones = (registry: EntityRegistry<GameEntity>) =>
    registry
        .ofKind('stone')
        .map((entity) => entity.id)
        .sort();

describe('ChunkManager', () => {
    it('loads the chunks around the hero, draws the near ones and simulates the nearest', () => {
        const { manager, settle } = world();

        settle(0, 0);

        expect(manager.stateAt(0, 0)).toBe('visible');
        expect(manager.isSimulated(0, 0)).toBe(true);
        // Beyond the simulation radius but within sight: drawn, not simulated.
        expect(manager.stateAt(80, 0)).toBe('visible');
        expect(manager.isSimulated(80, 0)).toBe(false);
        // Beyond sight: nothing.
        expect(manager.stateAt(300, 0)).toBe('unloaded');
        expect(manager.ready(0, 0)).toBe(true);
    });

    it('keeps far chunks active but undrawn when the simulation reaches farther than sight', () => {
        const { manager, settle } = world({
            simulationRadius: 128,
            visualRadius: 48,
        });

        settle(0, 0);

        expect(manager.stateAt(100, 0)).toBe('active');
        expect(manager.isSimulated(100, 0)).toBe(true);
        expect(manager.stateAt(10, 0)).toBe('visible');
    });

    it('passes through "loading" and works within the frame budget', () => {
        const { manager, clock } = world({}, 10, 1);

        manager.update(0, 0, 4);

        expect(manager.stateAt(0, 0)).toBe('loading');
        // One update did about the budget's worth of work, not every chunk at once.
        expect(clock()).toBeLessThanOrEqual(4 + 1);
        expect(manager.summary().queue).toBeGreaterThan(10);
    });

    it('makes each thing an entity with a collider, once', () => {
        const { registry, colliders, settle } = world();

        settle(0, 0);
        const ids = stones(registry);

        expect(ids).toContain('stone:test:x0');
        expect(new Set(ids).size).toBe(ids.length);
        expect(colliders.blocked({ x: 0, y: 0, z: 4 } as never, 0.3, 1.8)).toBe(
            true,
        );
    });

    it('lets far chunks go — their entities, colliders and pictures — and brings them back without copies', () => {
        const { manager, registry, colliders, shown, settle } = world();

        settle(0, 0);
        const before = stones(registry);

        settle(1000 - 512 + 400, 0);

        expect(registry.has('stone:test:x0')).toBe(false);
        expect(shown.has('stone:test:x0')).toBe(false);
        expect(colliders.blocked({ x: 0, y: 0, z: 4 } as never, 0.3, 1.8)).toBe(
            false,
        );
        expect(manager.summary().unloads).toBeGreaterThan(0);

        settle(0, 0);

        expect(stones(registry)).toEqual(before);
        expect(shown.has('stone:test:x0')).toBe(true);
    });

    it('keeps what changed when a chunk is let go and gives it back on loading', () => {
        const { registry, changes, settle } = world();

        settle(0, 0);
        (registry.get('stone:test:x0') as ContentEntity).state.marked = true;
        changes.remove('stone:test:x8');

        settle(400, 0);
        expect(changes.state('stone:test:x0')).toEqual({ marked: true });

        settle(0, 0);

        expect((registry.get('stone:test:x0') as ContentEntity).state).toEqual({
            marked: true,
        });
        expect(registry.has('stone:test:x8')).toBe(false);
    });

    it('does not flicker: walking to and fro across a radius keeps the chunk', () => {
        const { manager, settle } = world();

        settle(0, 0);
        const unloads = manager.summary().unloads;

        // A chunk at the edge of sight (96 m), the hero stepping 8 m either way.
        for (let i = 0; i < 20; i++) {
            settle(i % 2 ? 8 : -8, 0, 3);
        }

        expect(manager.summary().unloads).toBe(unloads);
    });

    it('never unloads a roaming entity: only its picture follows its chunk', () => {
        const { registry, shown, settle } = world();
        const wanderer: GameEntity = {
            id: 'block:wanderer',
            kind: 'block',
            location: 'test',
            position: { x: 20, y: 0, z: 20 },
            yaw: 0,
            state: {},
            width: 1,
            depth: 1,
            height: 1,
        };

        registry.add(wanderer);
        registry.add({
            ...wanderer,
            id: 'player',
            position: { x: 0, y: 0, z: 0 },
        } as GameEntity);
        settle(0, 0);

        expect(shown.has('block:wanderer')).toBe(true);

        settle(400, 0);

        expect(registry.get('block:wanderer')?.position).toEqual({
            x: 20,
            y: 0,
            z: 20,
        });
        expect(shown.has('block:wanderer')).toBe(false);
        // The hero is always drawn, wherever the chunks are.
        expect(shown.has('player')).toBe(true);

        settle(0, 0);

        expect(shown.has('block:wanderer')).toBe(true);
    });

    it('gives the same things the same ids whatever the chunk size', () => {
        const ids = [32, 64, 128].map((size) => {
            const { registry, settle } = world({
                size,
                simulationRadius: 100,
                visualRadius: 100,
            });

            settle(0, 0);

            // Only what lies within 64 m, which every size loads.
            return stones(registry).filter(
                (id) => Math.abs(Number(id.split('x')[1])) <= 64,
            );
        });

        expect(ids[0]).toEqual(ids[1]);
        expect(ids[1]).toEqual(ids[2]);
    });

    it('says it is ready only once the chunks to be simulated are loaded', () => {
        const { manager } = world();

        manager.update(0, 0, 1);

        expect(manager.ready(0, 0)).toBe(false);
        expect(manager.progress(0, 0).done).toBeLessThan(
            manager.progress(0, 0).total,
        );
    });

    it('lets everything go when disposed', () => {
        const { manager, registry, settle } = world();

        settle(0, 0);
        manager.dispose();

        expect(stones(registry)).toEqual([]);
        expect(manager.chunks()).toEqual([]);
    });
});
