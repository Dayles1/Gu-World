<?php

use App\Domain\Chat\Providers\ConversationServiceProvider;
use App\Games\GamesServiceProvider;
use App\Games\GuWorld\GuWorldServiceProvider;
use App\Games\Sandbox\SandboxServiceProvider;
use App\Providers\AppServiceProvider;
use App\Providers\AuthServiceProvider;
use App\Providers\InfrastructureServiceProvider;

return [
    AppServiceProvider::class,
    AuthServiceProvider::class,
    InfrastructureServiceProvider::class,
    ConversationServiceProvider::class,
    // Before GamesServiceProvider: its /games catch-all would shadow /games/sandbox and /games/gu-world.
    SandboxServiceProvider::class,
    GuWorldServiceProvider::class,
    GamesServiceProvider::class,
];
