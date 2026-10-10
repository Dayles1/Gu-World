<?php

namespace App\Games\GuWorld;

use Illuminate\Console\Events\CommandStarting;
use Illuminate\Support\Facades\Event;
use Illuminate\Support\Facades\Route;
use Illuminate\Support\ServiceProvider;
use Throwable;

/**
 * Plugs GU World into the host application. The game is one folder —
 * app/Games/GuWorld, its browser client included — with its own config,
 * routes, views, migrations and database. To remove it, delete the
 * folder and drop this provider from bootstrap/providers.php and its
 * entry from vite.config.ts; to move it, copy the folder and add the
 * same two lines.
 *
 * List it before any provider with a catch-all /games route, so
 * /games/gu-world is matched here first.
 */
class GuWorldServiceProvider extends ServiceProvider
{
    public function register(): void
    {
        $this->mergeConfigFrom(__DIR__.'/config/gu_world.php', 'gu_world');
        $this->registerConnection();
    }

    public function boot(): void
    {
        $this->loadMigrationsFrom(__DIR__.'/Database/Migrations');
        $this->loadViewsFrom(__DIR__.'/views', 'gu-world');

        Route::middleware('web')->group(__DIR__.'/routes/web.php');
        Route::middleware('api')
            ->prefix('api/gu-world')
            ->name('api.gu-world.')
            ->group(__DIR__.'/routes/api.php');

        if ($this->app->runningInConsole()) {
            $this->prepareDatabaseForMigrations();
        }
    }

    /**
     * The game's connection: the default connection with only the
     * database swapped, unless GU_WORLD_DB_DATABASE names one.
     */
    private function registerConnection(): void
    {
        $default = config('database.connections.'.config('database.default'), []);
        $name = config('gu_world.connection');
        $database = config('gu_world.database');

        if (($default['driver'] ?? null) === 'sqlite') {
            $database ??= ($default['database'] ?? null) === ':memory:' ? ':memory:' : database_path('gu_world.sqlite');
        } else {
            $database ??= ($default['database'] ?? 'laravel').'_gu_world';
        }

        config(["database.connections.{$name}" => [...$default, 'database' => $database, 'url' => null]]);
    }

    /**
     * `php artisan migrate` creates the game's database when it is missing;
     * `migrate:fresh` wipes it too, since it only drops the default
     * connection's tables.
     */
    private function prepareDatabaseForMigrations(): void
    {
        Event::listen(function (CommandStarting $event): void {
            if (! in_array($event->command, ['migrate', 'migrate:fresh'], true)) {
                return;
            }

            $database = $this->app->make(GuWorldDatabase::class);

            try {
                $database->ensureExists();

                if ($event->command === 'migrate:fresh') {
                    $database->dropAllTables();
                }
            } catch (Throwable $exception) {
                report($exception);
            }
        });
    }
}
