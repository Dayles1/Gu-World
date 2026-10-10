/**
 * GU World — entry point of the browser client: checks the player is
 * signed in, reads the world's settings from the page, loads the saved
 * game and starts it — a new game on the first morning, a saved one from
 * its saved time and place (repaired where it had to be).
 *
 * If the save cannot be loaded (the network, the server), the game does
 * not start at all: starting a new one would save over the real one. It
 * says so and offers to try again.
 */

import { goToLogin, hasToken, loadSave, putSave } from './api';
import { Game } from './game';
import type { GameSnapshot } from './game';
import { t } from './i18n';
import { restoreGame } from './state/save';
import { CHUNK_SIZES, readPageConfig, WorldConfigError } from './world/config';
import type { WorldConfig } from './world/config';
import { CONTENT } from './world/locations';
import './styles.css';

declare global {
    interface Window {
        /** Read-only look at the running game (for checks and debugging). */
        __GU_WORLD__?: { snapshot(): GameSnapshot };
    }
}

const root = document.getElementById('gu-world-root')!;

/** A card instead of the game: a message, and maybe a button. */
function message(lines: string[], retry?: () => void): void {
    const card = document.createElement('main');
    card.className = 'gw-card gw-card--message';

    const title = document.createElement('h1');
    title.textContent = t.title;
    card.append(title);

    for (const line of lines) {
        const paragraph = document.createElement('p');
        paragraph.textContent = line;
        card.append(paragraph);
    }

    if (retry) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'gw-button';
        button.textContent = t.retry;
        button.addEventListener('click', retry);
        card.append(button);
    }

    const back = document.createElement('a');
    back.className = 'gw-back';
    back.href = root.dataset.backUrl || '/';
    back.textContent = t.back;
    card.append(back);

    root.replaceChildren(card);
}

/**
 * For measuring (see the stage 3 report): `?chunk_size=32|64|128` loads
 * the world in chunks of that size instead of the configured one. Any
 * other value is ignored.
 */
function measuringOverride(config: WorldConfig): WorldConfig {
    const size = Number(
        new URLSearchParams(window.location.search).get('chunk_size'),
    );

    if (!CHUNK_SIZES.includes(size)) {
        return config;
    }

    return {
        ...config,
        chunks: {
            ...config.chunks,
            size,
            margin: Math.min(config.chunks.margin, size),
        },
    };
}

async function boot(config: WorldConfig): Promise<void> {
    message([t.loading]);

    let saved: unknown;

    try {
        saved = await loadSave();
    } catch (error) {
        console.error('GU World: the save could not be loaded', error);
        message([t.load_failed], () => void boot(config));

        return;
    }

    const restored = restoreGame(saved, config, (id) => id in CONTENT);

    root.replaceChildren();

    const game = new Game({
        root,
        config,
        restored,
        transport: { put: putSave },
    });

    game.start();
    window.__GU_WORLD__ = { snapshot: () => game.snapshot() };
}

if (!hasToken()) {
    goToLogin();
} else {
    try {
        void boot(
            measuringOverride(
                readPageConfig(document.getElementById('gu-world-settings')),
            ),
        );
    } catch (error) {
        console.error('GU World: the world settings are broken', error);
        message([
            t.settings_broken,
            ...(error instanceof WorldConfigError
                ? error.problems
                : [String(error)]),
        ]);
    }
}
