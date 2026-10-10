<?php

use App\Domain\Identity\Models\User;
use App\Games\GuWorld\Http\Requests\SaveGameRequest;
use App\Games\GuWorld\Models\Save;
use App\Games\Sandbox\Models\Player;
use Illuminate\Support\Facades\Schema;

/**
 * A sound save of the proving ground, with some parts overridden.
 *
 * @param  array<string, mixed>  $overrides
 * @return array<string, mixed>
 */
function guWorldSave(array $overrides = []): array
{
    return [
        'version' => 1,
        'location' => 'test_grounds',
        'world_minutes' => 2 * 1440 + 9 * 60,
        'player' => ['x' => 12.5, 'y' => 3.25, 'z' => -40, 'yaw' => 1.5],
        ...$overrides,
    ];
}

test('the game has a page of its own, apart from the games SPA and the Sandbox', function () {
    $this->withoutVite()
        ->get('/games/gu-world')
        ->assertOk()
        ->assertSee('id="gu-world-root"', false)
        ->assertSee('data-back-url="'.url('/games').'"', false)
        ->assertDontSee('id="games-root"', false)
        ->assertDontSee('id="sandbox-root"', false);
});

test('the page carries the world settings the server checks saves against', function () {
    $html = $this->withoutVite()->get('/games/gu-world')->assertOk()->getContent();

    preg_match('#<script type="application/json" id="gu-world-settings">(.*?)</script>#s', $html, $match);
    $settings = json_decode($match[1] ?? 'null', true);

    expect($settings)->toBe(config('gu_world.world'))
        ->and($settings['start'])->toBe('test_grounds');
});

test('the world settings are sound: every spawn inside its bounds, every bounds inside the limits', function () {
    $world = config('gu_world.world');
    $limits = $world['limits'];

    expect($world['locations'])->toHaveKey($world['start'])
        ->and($limits['half_extent'])->toBeLessThanOrEqual(32768);

    foreach ($world['locations'] as $id => $location) {
        $bounds = $location['bounds'];
        $spawn = $location['spawn'];

        expect($id)->toMatch('/^[a-z][a-z0-9_]{0,63}$/')
            ->and(max(-$bounds['min_x'], $bounds['max_x'], -$bounds['min_z'], $bounds['max_z']))->toBeLessThanOrEqual($limits['half_extent'])
            ->and($bounds['min_y'])->toBeGreaterThanOrEqual($limits['min_y'])
            ->and($bounds['max_y'])->toBeLessThanOrEqual($limits['max_y']);

        foreach (['x', 'y', 'z'] as $axis) {
            expect($spawn[$axis])->toBeGreaterThanOrEqual($bounds["min_{$axis}"])
                ->and($spawn[$axis])->toBeLessThanOrEqual($bounds["max_{$axis}"]);
        }
    }
});

test('saves live in the GU World database only', function () {
    expect(Schema::connection('gu_world')->hasTable('saves'))->toBeTrue()
        ->and(Schema::hasTable('saves'))->toBeFalse()
        ->and(Schema::connection('sandbox')->hasTable('saves'))->toBeFalse()
        ->and(Schema::connection('games')->hasTable('saves'))->toBeFalse();
});

test('guests cannot load, save or delete a game', function () {
    $this->getJson('/api/gu-world/save')->assertUnauthorized();
    $this->putJson('/api/gu-world/save', guWorldSave())->assertUnauthorized();
    $this->deleteJson('/api/gu-world/save')->assertUnauthorized();

    expect(Save::query()->count())->toBe(0);
});

test('a new player has no saved game', function () {
    $this->actingAs(User::factory()->create(), 'sanctum')
        ->getJson('/api/gu-world/save')
        ->assertOk()
        ->assertJsonPath('data', null);
});

test('a game is saved under the player and loads back', function () {
    $user = User::factory()->create();

    $this->actingAs($user, 'sanctum')
        ->putJson('/api/gu-world/save', guWorldSave())
        ->assertOk()
        ->assertJsonPath('success', true)
        ->assertJsonPath('data.location', 'test_grounds')
        ->assertJsonPath('data.player.x', 12.5);

    expect(Save::query()->sole())
        ->user_id->toBe($user->id)
        ->version->toBe(1)
        ->world_minutes->toBe(2.0 * 1440 + 9 * 60);

    $this->getJson('/api/gu-world/save')
        ->assertOk()
        ->assertJsonPath('data.world_minutes', 2 * 1440 + 9 * 60)
        ->assertJsonPath('data.player.z', -40)
        ->assertJsonPath('data.player.yaw', 1.5);
});

test('saving again replaces the one save', function () {
    $user = User::factory()->create();

    $this->actingAs($user, 'sanctum')->putJson('/api/gu-world/save', guWorldSave())->assertOk();
    $this->putJson('/api/gu-world/save', guWorldSave([
        'world_minutes' => 5000,
        'player' => ['x' => -1, 'y' => 0, 'z' => 2, 'yaw' => -3],
    ]))->assertOk();

    expect(Save::query()->sole())
        ->world_minutes->toBe(5000.0)
        ->player->toEqual(['x' => -1, 'y' => 0, 'z' => 2, 'yaw' => -3]);
});

test('a broken save is refused and the stored one stays as it was', function (array $broken, string $field) {
    $user = User::factory()->create();
    $this->actingAs($user, 'sanctum')->putJson('/api/gu-world/save', guWorldSave())->assertOk();

    $this->putJson('/api/gu-world/save', $broken)
        ->assertUnprocessable()
        ->assertJsonValidationErrors($field);

    expect(Save::query()->sole()->player['x'])->toBe(12.5);
})->with([
    'an unknown format' => [guWorldSave(['version' => 2]), 'version'],
    'no format' => [array_diff_key(guWorldSave(), ['version' => 0]), 'version'],
    'an unknown location' => [guWorldSave(['location' => 'atlantis']), 'location'],
    'a location that is not a string' => [guWorldSave(['location' => ['test_grounds']]), 'location'],
    'negative time' => [guWorldSave(['world_minutes' => -1]), 'world_minutes'],
    'time out of range' => [guWorldSave(['world_minutes' => 1e12]), 'world_minutes'],
    'time that is not a number' => [guWorldSave(['world_minutes' => 'noon']), 'world_minutes'],
    'NaN sent as null' => [guWorldSave(['player' => ['x' => null, 'y' => 0, 'z' => 0, 'yaw' => 0]]), 'player.x'],
    'a position outside the location' => [guWorldSave(['player' => ['x' => 500, 'y' => 0, 'z' => 0, 'yaw' => 0]]), 'player.x'],
    'too deep' => [guWorldSave(['player' => ['x' => 0, 'y' => -500, 'z' => 0, 'yaw' => 0]]), 'player.y'],
    'a facing of many turns' => [guWorldSave(['player' => ['x' => 0, 'y' => 0, 'z' => 0, 'yaw' => 100]]), 'player.yaw'],
    'no position' => [array_diff_key(guWorldSave(), ['player' => 0]), 'player'],
    'an unknown field in the position' => [guWorldSave(['player' => ['x' => 0, 'y' => 0, 'z' => 0, 'yaw' => 0, 'hp' => 9]]), 'player'],
    'an unknown field' => [guWorldSave(['gold' => 1000000]), 'save'],
]);

test('a save larger than the limit is refused', function () {
    $this->actingAs(User::factory()->create(), 'sanctum')
        ->putJson('/api/gu-world/save', guWorldSave(['padding' => str_repeat('x', SaveGameRequest::MAX_BYTES)]))
        ->assertStatus(413);

    expect(Save::query()->count())->toBe(0);
});

test('nobody reads, changes or deletes another player\'s game', function () {
    $owner = User::factory()->create();
    $other = User::factory()->create();

    $this->actingAs($owner, 'sanctum')->putJson('/api/gu-world/save', guWorldSave())->assertOk();

    $this->actingAs($other, 'sanctum')
        ->getJson('/api/gu-world/save')
        ->assertOk()
        ->assertJsonPath('data', null);
    $this->putJson('/api/gu-world/save', guWorldSave(['world_minutes' => 1]))->assertOk();
    $this->deleteJson('/api/gu-world/save')->assertOk();

    expect(Save::query()->where('user_id', $owner->id)->sole())
        ->world_minutes->toBe(2.0 * 1440 + 9 * 60)
        ->player->toEqual(['x' => 12.5, 'y' => 3.25, 'z' => -40, 'yaw' => 1.5])
        ->and(Save::query()->where('user_id', $other->id)->exists())->toBeFalse();
});

test('deleting the game removes only GU World\'s save', function () {
    $user = User::factory()->create();
    Player::factory()->create(['user_id' => $user->id]);

    $this->actingAs($user, 'sanctum')->putJson('/api/gu-world/save', guWorldSave())->assertOk();
    $this->deleteJson('/api/gu-world/save')->assertOk()->assertJsonPath('success', true);

    expect(Save::query()->count())->toBe(0)
        ->and(Player::query()->where('user_id', $user->id)->exists())->toBeTrue();

    $this->getJson('/api/gu-world/save')->assertOk()->assertJsonPath('data', null);
});

test('saving is limited to 30 times a minute', function () {
    $this->actingAs(User::factory()->create(), 'sanctum');

    for ($i = 0; $i < 30; $i++) {
        $this->putJson('/api/gu-world/save', guWorldSave())->assertOk();
    }

    $this->putJson('/api/gu-world/save', guWorldSave())->assertTooManyRequests();
});
