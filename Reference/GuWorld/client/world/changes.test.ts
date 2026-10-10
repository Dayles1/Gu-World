import { describe, expect, it } from 'vitest';
import {
    MAX_CHANGES,
    MAX_STATE_KEYS,
    WorldChanges,
    WorldChangesError,
} from './changes';

describe('WorldChanges', () => {
    it('keeps changed values and removals by entity id', () => {
        const changes = new WorldChanges();

        changes.setState('stone:test_grounds:c1_2', 'marked', true);
        changes.remove('block:test_grounds:crate');

        expect(changes.state('stone:test_grounds:c1_2')).toEqual({
            marked: true,
        });
        expect(changes.isRemoved('block:test_grounds:crate')).toBe(true);
        expect(changes.isRemoved('stone:test_grounds:c1_2')).toBe(false);
    });

    it('goes into a save and comes back unchanged', () => {
        const changes = new WorldChanges();

        changes.setState('stone:a', 'marked', true);
        changes.setState('stone:a', 'colour', 'red');
        changes.remove('block:b');

        const json = JSON.parse(JSON.stringify(changes));
        const { changes: back, problems } = WorldChanges.restore(json);

        expect(problems).toEqual([]);
        expect(back.toJSON()).toEqual(json);
    });

    it.each([
        ['a broken id', 'Stone A', 'marked', true],
        ['a bad key', 'stone:a', 'Marked!', true],
        ['NaN', 'stone:a', 'n', NaN],
        ['an object', 'stone:a', 'n', { deep: 1 }],
        ['a long string', 'stone:a', 'text', 'x'.repeat(65)],
    ])('refuses %s', (_, id, key, value) => {
        expect(() =>
            new WorldChanges().setState(
                id as string,
                key as string,
                value as never,
            ),
        ).toThrow(WorldChangesError);
    });

    it('caps the values per entity and the entities changed', () => {
        const changes = new WorldChanges();

        for (let i = 0; i < MAX_STATE_KEYS; i++) {
            changes.setState('stone:a', `k${i}`, i);
        }

        expect(() => changes.setState('stone:a', 'one_more', 1)).toThrow();

        for (let i = 1; i < MAX_CHANGES; i++) {
            changes.remove(`stone:n${i}`);
        }

        expect(() => changes.remove('stone:too_many')).toThrow();
    });

    it('drops what cannot be used from a save, saying why, and keeps the rest', () => {
        const { changes, problems } = WorldChanges.restore({
            'stone:good': { state: { marked: true, broken: { x: 1 } } },
            'Bad Id': { removed: true },
            'block:gone': { removed: true },
            'stone:null': null,
        });

        expect(changes.state('stone:good')).toEqual({ marked: true });
        expect(changes.isRemoved('block:gone')).toBe(true);
        expect(changes.size).toBe(2);
        expect(problems).toHaveLength(3);
    });

    it('takes no changes at all, and refuses a list', () => {
        expect(WorldChanges.restore(undefined).changes.size).toBe(0);
        expect(WorldChanges.restore([1, 2]).problems).toHaveLength(1);
    });
});
