import { describe, expect, it, vi } from 'vitest';
import { EntityError, EntityRegistry, isEntityId } from './registry';
import type { Entity } from './registry';

function entity(id: string, x = 0): Entity {
    return {
        id,
        kind: id.split(':')[0],
        location: 'test_grounds',
        position: { x, y: 0, z: 0 },
        yaw: 0,
    };
}

describe('EntityRegistry', () => {
    it('keeps each entity under its own id', () => {
        const registry = new EntityRegistry();

        registry.add(entity('block:a'));
        registry.add(entity('block:b'));

        expect(registry.size).toBe(2);
        expect(registry.get('block:b')?.id).toBe('block:b');
    });

    it('refuses a second entity with an id already there', () => {
        const registry = new EntityRegistry();

        registry.add(entity('block:a'));

        expect(() => registry.add(entity('block:a'))).toThrow(EntityError);
        expect(registry.size).toBe(1);
    });

    it('loading the same things again never makes copies', () => {
        const registry = new EntityRegistry();
        const load = () =>
            ['block:a', 'block:b', 'block:c'].map((id) =>
                registry.ensure(id, () => entity(id)),
            );

        const first = load();
        const second = load();

        expect(registry.size).toBe(3);
        expect(second).toEqual(first);
        expect(second[0]).toBe(first[0]);
    });

    it('gives the same ids whatever the order things come in', () => {
        const ids = ['block:a', 'block:b', 'block:c'];
        const one = new EntityRegistry();
        const other = new EntityRegistry();

        ids.forEach((id) => one.ensure(id, () => entity(id)));
        [...ids].reverse().forEach((id) => other.ensure(id, () => entity(id)));

        expect([...one.all()].map((e) => e.id).sort()).toEqual(
            [...other.all()].map((e) => e.id).sort(),
        );
    });

    it('can take an entity out and put it back without a copy', () => {
        const registry = new EntityRegistry();

        registry.add(entity('block:a'));
        registry.remove('block:a');
        registry.ensure('block:a', () => entity('block:a'));

        expect(registry.size).toBe(1);
    });

    it.each(['', 'Block', 'block a', 'block::a', '1block', 'a'.repeat(200)])(
        'refuses the id "%s"',
        (id) => {
            expect(isEntityId(id)).toBe(false);
            expect(() => new EntityRegistry().add(entity(id))).toThrow(
                EntityError,
            );
        },
    );

    it('refuses an entity with a broken position', () => {
        const broken = {
            ...entity('block:a'),
            position: { x: NaN, y: 0, z: 0 },
        };

        expect(() => new EntityRegistry().add(broken)).toThrow(EntityError);
    });

    it('moves an entity, and refuses a broken move leaving it where it was', () => {
        const registry = new EntityRegistry();

        registry.add(entity('player'));
        registry.move('player', { x: 3, y: 1, z: -2 }, 1.5);

        expect(() =>
            registry.move('player', { x: Infinity, y: 0, z: 0 }),
        ).toThrow(EntityError);
        expect(() =>
            registry.move('player', { x: 0, y: 0, z: 0 }, NaN),
        ).toThrow(EntityError);
        expect(registry.get('player')).toMatchObject({
            position: { x: 3, y: 1, z: -2 },
            yaw: 1.5,
        });
    });

    it('lets an observer (a future spatial index) hear additions, moves and removals', () => {
        const registry = new EntityRegistry();
        const observer = { added: vi.fn(), moved: vi.fn(), removed: vi.fn() };
        const stop = registry.observe(observer);

        registry.add(entity('player'));
        registry.move('player', { x: 5, y: 0, z: 0 });
        registry.remove('player');
        stop();
        registry.add(entity('block:a'));

        expect(observer.added).toHaveBeenCalledTimes(1);
        expect(observer.moved).toHaveBeenCalledWith(
            expect.objectContaining({ id: 'player' }),
            { x: 0, y: 0, z: 0 },
        );
        expect(observer.removed).toHaveBeenCalledTimes(1);
    });

    it('finds entities by kind', () => {
        const registry = new EntityRegistry();

        registry.add(entity('player'));
        registry.add(entity('block:a'));
        registry.add(entity('block:b'));

        expect(registry.ofKind('block').map((e) => e.id)).toEqual([
            'block:a',
            'block:b',
        ]);
    });
});
