<?php

namespace App\Games\GuWorld;

use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

/**
 * The game's own database: making sure it exists before migrations run,
 * and wiping it when the main database is rebuilt from scratch.
 */
class GuWorldDatabase
{
    public function connectionName(): string
    {
        return config('gu_world.connection');
    }

    /**
     * @return array<string, mixed>
     */
    public function config(): array
    {
        return config('database.connections.'.$this->connectionName());
    }

    /**
     * Creates the sqlite file or the MySQL/MariaDB database when it is
     * missing. Other drivers need it created by hand; false then.
     */
    public function ensureExists(): bool
    {
        $connection = $this->config();

        return match ($connection['driver']) {
            'sqlite' => $this->ensureSqliteFile($connection['database']),
            'mysql', 'mariadb' => $this->ensureServerDatabase($connection),
            default => false,
        };
    }

    public function dropAllTables(): void
    {
        Schema::connection($this->connectionName())->dropAllTables();
    }

    private function ensureSqliteFile(string $path): bool
    {
        if ($path !== ':memory:' && ! file_exists($path)) {
            touch($path);
        }

        return true;
    }

    /**
     * @param  array<string, mixed>  $connection
     */
    private function ensureServerDatabase(array $connection): bool
    {
        $server = $this->connectionName().'_server';

        config(["database.connections.{$server}" => [...$connection, 'database' => null]]);

        DB::connection($server)->statement(sprintf(
            'CREATE DATABASE IF NOT EXISTS `%s` CHARACTER SET %s COLLATE %s',
            str_replace('`', '', $connection['database']),
            $connection['charset'] ?? 'utf8mb4',
            $connection['collation'] ?? 'utf8mb4_unicode_ci',
        ));

        DB::purge($server);

        return true;
    }
}
