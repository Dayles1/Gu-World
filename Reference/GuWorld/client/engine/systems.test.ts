import { describe, expect, it, vi } from 'vitest';
import { FAILURE_LIMIT, SystemRunner } from './systems';
import type { GameSystem } from './systems';

function counter(name: string): GameSystem & { runs: number } {
    return {
        name,
        runs: 0,
        update() {
            this.runs++;
        },
    };
}

function broken(
    name: string,
    fails = Infinity,
): GameSystem & { calls: number } {
    return {
        name,
        calls: 0,
        update() {
            this.calls++;

            if (this.calls <= fails) {
                throw new Error(`${name} broke`);
            }
        },
    };
}

function runner() {
    const failed = vi.fn();
    const disabled = vi.fn();

    return { runner: new SystemRunner({ failed, disabled }), failed, disabled };
}

describe('SystemRunner', () => {
    it('runs every system with the step, in order', () => {
        const { runner: systems } = runner();
        const order: string[] = [];

        for (const name of ['a', 'b']) {
            systems.add({
                name,
                update: (step) => order.push(`${name}:${step}`),
            });
        }

        systems.run(0.5);

        expect(order).toEqual(['a:0.5', 'b:0.5']);
    });

    it('keeps running the others when one throws, and reports it', () => {
        const { runner: systems, failed } = runner();
        const before = counter('before');
        const after = counter('after');

        systems.add(before);
        systems.add(broken('bad'));
        systems.add(after);
        systems.run(1);

        expect(before.runs).toBe(1);
        expect(after.runs).toBe(1);
        expect(failed).toHaveBeenCalledWith('bad', 'bad broke', 1);
    });

    it('switches off a system that keeps failing, and only that one', () => {
        const { runner: systems, disabled } = runner();
        const bad = broken('bad');
        const good = counter('good');

        systems.add(bad);
        systems.add(good);

        for (let i = 0; i < FAILURE_LIMIT + 3; i++) {
            systems.run(1);
        }

        expect(bad.calls).toBe(FAILURE_LIMIT);
        expect(good.runs).toBe(FAILURE_LIMIT + 3);
        expect(disabled).toHaveBeenCalledTimes(1);
        expect(systems.status()).toMatchObject([
            { name: 'bad', enabled: false, failures: FAILURE_LIMIT },
            { name: 'good', enabled: true, failures: 0 },
        ]);
    });

    it('forgives failures that are not in a row', () => {
        const { runner: systems, disabled } = runner();
        let call = 0;

        systems.add({
            name: 'flaky',
            update() {
                call++;

                if (call % 2 === 0) {
                    throw new Error('every other time');
                }
            },
        });

        for (let i = 0; i < FAILURE_LIMIT * 4; i++) {
            systems.run(1);
        }

        expect(disabled).not.toHaveBeenCalled();
        expect(systems.status()[0]).toMatchObject({
            enabled: true,
            totalFailures: FAILURE_LIMIT * 2,
        });
    });

    it('gives a switched-off system another chance on request', () => {
        const { runner: systems } = runner();
        const bad = broken('bad', FAILURE_LIMIT);

        systems.add(bad);

        for (let i = 0; i < FAILURE_LIMIT; i++) {
            systems.run(1);
        }

        systems.enable('bad');
        systems.run(1);

        expect(systems.status()[0]).toMatchObject({
            enabled: true,
            failures: 0,
        });
    });

    it('refuses two systems with one name', () => {
        const { runner: systems } = runner();

        systems.add(counter('same'));

        expect(() => systems.add(counter('same'))).toThrow(/already/);
    });

    it('disposes systems when they are taken out or the runner ends', () => {
        const { runner: systems } = runner();
        const disposed: string[] = [];

        for (const name of ['a', 'b']) {
            systems.add({
                name,
                update() {},
                dispose: () => disposed.push(name),
            });
        }

        systems.remove('a');
        systems.dispose();

        expect(disposed).toEqual(['a', 'b']);
        expect(systems.status()).toEqual([]);
    });
});
