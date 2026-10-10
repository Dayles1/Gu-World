import { describe, expect, it, vi } from 'vitest';
import { WorldClock } from '../engine/clock';
import { worldSettings } from '../testing/world-settings';
import { parseWorldConfig } from '../world/config';
import { makeSave } from './save';
import type { Save } from './save';
import { Saver } from './saver';

const config = parseWorldConfig(worldSettings());
const at = (x: number) =>
    makeSave('test_grounds', WorldClock.newGame(), { x, y: 0, z: 0, yaw: 0 });

function setup(put: (save: Save) => Promise<void>) {
    const report = { written: vi.fn(), failed: vi.fn(), refused: vi.fn() };
    const transport = { put: vi.fn(put) };

    return { saver: new Saver(transport, config, report), transport, report };
}

describe('Saver', () => {
    it('sends a sound save and says it was written', async () => {
        const { saver, transport, report } = setup(async () => {});

        expect(await saver.save(at(1))).toBe('saved');
        expect(transport.put).toHaveBeenCalledWith(at(1), false);
        expect(report.written).toHaveBeenCalledTimes(1);
    });

    it('never sends a broken save', async () => {
        const { saver, transport, report } = setup(async () => {});

        expect(await saver.save(at(NaN))).toBe('refused');
        expect(await saver.save(at(10_000))).toBe('refused');
        expect(transport.put).not.toHaveBeenCalled();
        expect(report.refused).toHaveBeenCalledTimes(2);
    });

    it('keeps one request on its way, then sends only the latest save', async () => {
        let finish = () => {};
        const { saver, transport } = setup(
            (save) =>
                new Promise<void>((resolve) => {
                    finish = resolve;
                    void save;
                }),
        );

        const first = saver.save(at(1));
        expect(await saver.save(at(2))).toBe('queued');
        expect(await saver.save(at(3))).toBe('queued');
        expect(transport.put).toHaveBeenCalledTimes(1);

        finish();
        await first;
        await vi.waitFor(() => expect(transport.put).toHaveBeenCalledTimes(2));
        finish();

        expect(transport.put.mock.calls.map(([save]) => save.player.x)).toEqual(
            [1, 3],
        );
    });

    it('reports a failed request and lets the next save try again', async () => {
        let fail = true;
        const { saver, report } = setup(async () => {
            if (fail) {
                throw new Error('Request failed (500)');
            }
        });

        expect(await saver.save(at(1))).toBe('failed');
        expect(report.failed).toHaveBeenCalledWith('Request failed (500)');

        fail = false;

        expect(await saver.save(at(1))).toBe('saved');
    });
});
