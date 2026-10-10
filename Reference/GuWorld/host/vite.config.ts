import { wayfinder } from '@laravel/vite-plugin-wayfinder';
import react from '@vitejs/plugin-react';
import laravel from 'laravel-vite-plugin';
import { bunny } from 'laravel-vite-plugin/fonts';
import { defineConfig } from 'vite';

export default defineConfig({
    plugins: [
        laravel({
            input: [
                // Auth Blade
                'resources/css/blade/auth/auth.css',
                'resources/css/blade/auth/auth-pages.css',
                'resources/js/blade/auth/auth.js',
                'resources/js/blade/auth/auth-pages.js',

                // App Blade
                'resources/css/blade/app/app.css',
                'resources/js/blade/app/app.js',
                'resources/js/blade/app/authenticated.js',
                'resources/js/blade/app/sessions.js',
                'resources/js/blade/app/session-detail.js',
                'resources/js/blade/app/settings.js',
                'resources/js/blade/app/dashboard.js',
                'resources/js/blade/app/notifications.js',
                'resources/js/blade/app/currencies.js',
                'resources/js/blade/app/changelog.js',
                'resources/js/blade/app/profile.js',
                'resources/js/blade/app/user-profile.js',
                'resources/js/blade/app/chat.js',
                'resources/js/blade/app/admin-users.js',
                'resources/js/blade/app/admin-sessions.js',
                'resources/js/blade/app/admin-session-detail.js',
                'resources/js/blade/app/admin-request-logs.js',

                // Games SPA (React) — everything else of it is in resources/games
                'resources/games/src/main.tsx',

                // Sandbox — a self-contained game, all of it in app/Games/Sandbox
                'app/Games/Sandbox/client/main.ts',

                // GU World — a self-contained game, all of it in app/Games/GuWorld
                'app/Games/GuWorld/client/main.ts',

                // Если здесь будут другие Blade-страницы,
                // добавляй их сюда
            ],

            refresh: true,

            fonts: [
                // Cyrillic for ru, Latin for uz/en — without it every
                // Cyrillic glyph falls back to the system font.
                bunny('Onest', {
                    weights: [400, 500, 600, 700],
                    subsets: ['latin', 'cyrillic'],
                }),
                // Numbers, versions, times and paths in the Graphite design.
                bunny('JetBrains Mono', {
                    weights: [400, 500],
                    subsets: ['latin', 'cyrillic'],
                }),
            ],
        }),

        // Only the games SPA is written in React.
        react({ include: /resources\/games\/.*\.tsx?$/ }),

        wayfinder({
            formVariants: true,
        }),
    ],
});
