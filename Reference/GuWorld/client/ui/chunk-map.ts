/**
 * The debug map of the chunks (F3): every chunk known to the
 * ChunkManager around the hero, coloured by its state — loading amber,
 * active blue, visible green — with a white frame where things are
 * simulated, the two radii as circles and the hero as a dot. North (-Z)
 * is up.
 */

import type { ChunkInfo, ChunkState } from '../world/chunk-manager';
import type { ChunkSettings } from '../world/config';

const COLORS: Record<ChunkState, string> = {
    unloaded: 'transparent',
    loading: 'rgb(214 160 60 / 0.75)',
    active: 'rgb(70 120 200 / 0.7)',
    visible: 'rgb(110 170 90 / 0.7)',
};

export function drawChunkMap(
    canvas: HTMLCanvasElement,
    chunks: ChunkInfo[],
    settings: ChunkSettings,
    hero: { x: number; z: number },
): void {
    const context = canvas.getContext('2d');

    if (!context) {
        return;
    }

    const size = canvas.width;
    const reach =
        Math.max(settings.visualRadius, settings.simulationRadius) +
        settings.margin +
        settings.size;
    const scale = size / (reach * 2);
    const toX = (x: number) => (x - hero.x) * scale + size / 2;
    const toY = (z: number) => (z - hero.z) * scale + size / 2;
    const cell = settings.size * scale;

    context.clearRect(0, 0, size, size);
    context.fillStyle = 'rgb(12 13 15 / 0.85)';
    context.fillRect(0, 0, size, size);

    for (const chunk of chunks) {
        const x = toX(chunk.coord.cx * settings.size);
        const y = toY(chunk.coord.cz * settings.size);

        context.fillStyle = COLORS[chunk.state];
        context.fillRect(x + 0.5, y + 0.5, cell - 1, cell - 1);

        if (chunk.simulated) {
            context.strokeStyle = 'rgb(240 236 226 / 0.8)';
            context.lineWidth = 1;
            context.strokeRect(x + 1, y + 1, cell - 2, cell - 2);
        }
    }

    for (const [radius, color] of [
        [settings.simulationRadius, 'rgb(240 236 226 / 0.6)'],
        [settings.visualRadius, 'rgb(110 170 90 / 0.9)'],
    ] as const) {
        context.beginPath();
        context.arc(size / 2, size / 2, radius * scale, 0, Math.PI * 2);
        context.strokeStyle = color;
        context.setLineDash([3, 3]);
        context.stroke();
        context.setLineDash([]);
    }

    context.beginPath();
    context.arc(size / 2, size / 2, 3, 0, Math.PI * 2);
    context.fillStyle = '#e0675f';
    context.fill();
}
