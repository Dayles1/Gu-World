/**
 * What is laid over the world: where and when it is (the location, the
 * day and time), whether the game is saved, the pause card with the
 * controls, notices (what had to be repaired in a save) and, on F3, the
 * debug panel. Plain DOM; everything it adds is removed in `dispose`.
 */

import type { WorldClock } from '../engine/clock';
import { t } from '../i18n';

export type StatusTone = 'quiet' | 'good' | 'bad';

const NOTICE_SECONDS = 12;

function element<K extends keyof HTMLElementTagNameMap>(
    tag: K,
    className: string,
    text = '',
): HTMLElementTagNameMap[K] {
    const node = document.createElement(tag);
    node.className = className;
    node.textContent = text;

    return node;
}

export class Hud {
    private root = element('div', 'gw-hud');
    private place = element('div', 'gw-plate gw-place');
    private status = element('div', 'gw-plate gw-status');
    private notices = element('div', 'gw-notices');
    private debug = element('div', 'gw-plate gw-debug');
    private debugText = element('pre', 'gw-debug__text');
    /** The chunk map (ui/chunk-map.ts), drawn while the debug panel is open. */
    readonly chunkMap = element('canvas', 'gw-debug__map');
    private loading = element('div', 'gw-loading');
    private loadingText = element('p', '', t.loading_world);
    private loadingBar = element('span', 'gw-loading__bar');
    private overlay = element('div', 'gw-overlay');
    private overlayTitle = element('h1', '', t.title);
    private overlayButton = element('button', 'gw-button', t.play);
    private placeKey = '';

    constructor(host: HTMLElement, location: string, onPlay: () => void) {
        this.place.append(
            element('b', 'gw-place__name', t.locations[location] ?? location),
            element('span', 'gw-place__time'),
        );
        this.debug.hidden = true;
        this.chunkMap.width = 220;
        this.chunkMap.height = 220;
        this.debug.append(this.chunkMap, this.debugText);

        const track = element('span', 'gw-loading__track');
        track.append(this.loadingBar);
        this.loading.append(this.loadingText, track);
        this.loading.hidden = true;
        this.overlayButton.type = 'button';
        this.overlayButton.addEventListener('click', onPlay);

        const card = element('div', 'gw-card');
        const controls = element('dl', 'gw-controls');

        for (const [keys, what] of t.controls) {
            controls.append(element('dt', '', keys), element('dd', '', what));
        }

        card.append(
            this.overlayTitle,
            element('p', '', t.click_to_play),
            this.overlayButton,
            controls,
        );
        this.overlay.append(card);
        this.root.append(
            this.place,
            this.status,
            this.notices,
            this.debug,
            this.overlay,
            this.loading,
        );
        host.append(this.root);
    }

    setClock(clock: WorldClock): void {
        const key = `${clock.day}|${clock.label}`;

        if (key === this.placeKey) {
            return;
        }

        this.placeKey = key;
        this.place.lastElementChild!.textContent = `${t.day} ${clock.day} · ${clock.label} · ${t.phases[clock.phase]}`;
    }

    setStatus(text: string, tone: StatusTone = 'quiet'): void {
        this.status.textContent = text;
        this.status.dataset.tone = tone;
    }

    /** The pause card; `started` once play has begun (it then says "resume"). */
    showPause(show: boolean, started = false): void {
        this.overlay.hidden = !show;
        this.overlayButton.textContent = started ? t.resume : t.play;
        this.overlayTitle.textContent = started ? t.paused : t.title;
    }

    /** The world loading around the hero: `done` of `total` chunks; null hides it. */
    setLoading(progress: { done: number; total: number } | null): void {
        this.loading.hidden = progress === null;

        if (progress) {
            this.loadingText.textContent = `${t.loading_world} ${progress.done} / ${progress.total}`;
            this.loadingBar.style.width = `${(progress.done / Math.max(1, progress.total)) * 100}%`;
        }
    }

    notice(text: string): void {
        const line = element('p', 'gw-plate gw-notice', text);
        this.notices.append(line);
        window.setTimeout(() => line.remove(), NOTICE_SECONDS * 1000);
    }

    get debugShown(): boolean {
        return !this.debug.hidden;
    }

    toggleDebug(): void {
        this.debug.hidden = !this.debug.hidden;
    }

    setDebug(lines: string[]): void {
        if (!this.debug.hidden) {
            this.debugText.textContent = lines.join('\n');
        }
    }

    dispose(): void {
        this.root.remove();
    }
}
