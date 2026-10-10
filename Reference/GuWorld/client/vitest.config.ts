/**
 * Unit tests of GU World's client logic (clock, chunks, entities, NPCs,
 * saves): pure TypeScript, run in Node without a browser. Run them with
 * `npm run test:gu-world`.
 */

import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

export default defineConfig({
    root: fileURLToPath(new URL('.', import.meta.url)),
    // Vitest's cache goes with the project's other caches, not into the game's folder.
    cacheDir: fileURLToPath(
        new URL('../../../../node_modules/.vite/gu-world', import.meta.url),
    ),
    test: {
        include: ['**/*.test.ts'],
        environment: 'node',
    },
});
