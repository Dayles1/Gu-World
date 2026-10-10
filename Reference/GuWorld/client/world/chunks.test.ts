import { describe, expect, it } from 'vitest';
import {
    chunkArea,
    chunkAt,
    chunkInBounds,
    chunkKey,
    chunksWithin,
    distanceToChunk,
    parseChunkKey,
} from './chunks';

const bounds = {
    minX: -100,
    maxX: 100,
    minY: -10,
    maxY: 100,
    minZ: -100,
    maxZ: 100,
};

describe('the chunk grid', () => {
    it('puts every point in exactly one chunk, negative sides included', () => {
        expect(chunkAt(0, 0, 64)).toEqual({ cx: 0, cz: 0 });
        expect(chunkAt(63.99, 0, 64)).toEqual({ cx: 0, cz: 0 });
        expect(chunkAt(64, 0, 64)).toEqual({ cx: 1, cz: 0 });
        expect(chunkAt(-0.01, -64, 64)).toEqual({ cx: -1, cz: -1 });
        expect(chunkAt(-64.01, 0, 64)).toEqual({ cx: -2, cz: 0 });
    });

    it('gives each chunk its area, min inclusive, max exclusive', () => {
        expect(chunkArea({ cx: -1, cz: 2 }, 32)).toEqual({
            minX: -32,
            maxX: 0,
            minZ: 64,
            maxZ: 96,
        });
    });

    it('turns keys into chunks and back', () => {
        expect(parseChunkKey(chunkKey(-3, 7))).toEqual({ cx: -3, cz: 7 });
    });

    it('measures from a point to the nearest edge of a chunk', () => {
        expect(distanceToChunk(10, 10, { cx: 0, cz: 0 }, 64)).toBe(0);
        expect(distanceToChunk(-10, 10, { cx: 0, cz: 0 }, 64)).toBe(10);
        expect(distanceToChunk(-3, -4, { cx: 0, cz: 0 }, 64)).toBe(5);
    });

    it('knows which chunks touch the location at all', () => {
        expect(chunkInBounds({ cx: 1, cz: 1 }, 64, bounds)).toBe(true);
        expect(chunkInBounds({ cx: 2, cz: 0 }, 64, bounds)).toBe(false);
        expect(chunkInBounds({ cx: -2, cz: 0 }, 64, bounds)).toBe(true);
    });

    it('lists the chunks within a radius, nearest first, inside the location only', () => {
        const near = chunksWithin(0, 0, 70, 64, bounds);
        const keys = near.map(({ chunk }) => chunkKey(chunk.cx, chunk.cz));

        expect(near[0].distance).toBe(0);
        expect(keys).toContain('-1:-1');
        expect(keys).toContain('1:0');
        expect(keys).not.toContain('2:0');
        expect(
            near.every(
                (each, i) => i === 0 || each.distance >= near[i - 1].distance,
            ),
        ).toBe(true);
    });
});
