<?php

/*
|--------------------------------------------------------------------------
| GU World
|--------------------------------------------------------------------------
|
| A self-contained game: everything it needs lives in app/Games/GuWorld,
| including this file (merged as the `gu_world` config by its provider)
| and its own database. The only thing it shares with the host
| application is who the player is — the user id behind the bearer token.
| It shares nothing with the Sandbox either.
|
*/

return [

    /*
     * The connection the game's tables live on. GuWorldServiceProvider
     * builds it from the default connection with only the database swapped:
     *
     * - mysql / mariadb / pgsql: GU_WORLD_DB_DATABASE, or "<main db>_gu_world".
     * - sqlite: GU_WORLD_DB_DATABASE, or database/gu_world.sqlite
     *   (an in-memory main database gets an in-memory one).
     */
    'connection' => 'gu_world',

    'database' => env('GU_WORLD_DB_DATABASE'),

    /*
     * Where the "back" button in the game leads.
     */
    'back_url' => env('GU_WORLD_BACK_URL', '/games'),

    /*
    |--------------------------------------------------------------------------
    | The world
    |--------------------------------------------------------------------------
    |
    | Handed to the page as JSON (views/app.blade.php) and checked against by
    | the server when a game is saved, so both use the same numbers. Every
    | distance is in metres; Y is up.
    |
    | The world grows here — more locations, wider bounds — not in the
    | engine. Sizes not confirmed by the novel are working assumptions.
    |
    */

    'world' => [

        /*
         * The engine's limits for any location: the farthest from the
         * centre on X and Z (the client's collider grid holds up to 32 768
         * m), and the lowest and highest points. 16 km either way leaves
         * room for the planned region of 10–20 km across — an assumption
         * to be tuned after prototyping, not the novel's measure.
         */
        'limits' => ['half_extent' => 16000, 'min_y' => -1000, 'max_y' => 5000],

        /*
         * How the world is loaded around the hero (client/world/chunk-manager.ts):
         * the chunk's side, how far things are simulated and drawn, and how
         * much farther a chunk is kept before being let go. Stage 3: the size
         * is still being measured (32 / 64 / 128 m).
         */
        'chunks' => ['size' => 64, 'simulation_radius' => 96, 'visual_radius' => 192, 'margin' => 16],

        /*
         * Where a new game begins.
         */
        'start' => 'test_grounds',

        /*
         * Each location: the box a hero may be in, and where they appear.
         * Its ground and what stands on it are built by the client
         * (client/content/).
         */
        'locations' => [

            // The engine's proving ground (stage 2) — not part of the world of Gu.
            'test_grounds' => [
                'bounds' => ['min_x' => -256, 'max_x' => 256, 'min_y' => -20, 'max_y' => 120, 'min_z' => -256, 'max_z' => 256],
                'spawn' => ['x' => 0, 'y' => 0, 'z' => 12, 'yaw' => 3.14159],
            ],

        ],

    ],

];
