<?php

namespace App\Games\GuWorld\Http\Controllers;

use App\Games\GuWorld\Http\Requests\SaveGameRequest;
use App\Games\GuWorld\Models\Save;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

/**
 * The player's saved game: load, save (create or replace), delete — only
 * ever the signed-in player's own, found by their user id, never by an
 * id from the request. Answers with the same `{success, message, data}`
 * envelope as the host API, without depending on its controllers. No
 * save yet means a new game: the client starts one (on the first
 * morning). Deleting it touches nothing but GU World's save.
 */
class SaveController
{
    public function show(Request $request): JsonResponse
    {
        $save = Save::query()->where('user_id', $this->playerId($request))->first();

        return $this->success($save === null ? null : $this->present($save));
    }

    /**
     * Saves the game: the one save of this player, created or replaced.
     */
    public function update(SaveGameRequest $request): JsonResponse
    {
        $save = Save::query()->updateOrCreate(
            ['user_id' => $this->playerId($request)],
            $request->save(),
        );

        return $this->success($this->present($save));
    }

    public function destroy(Request $request): JsonResponse
    {
        Save::query()->where('user_id', $this->playerId($request))->delete();

        return $this->success();
    }

    /**
     * @return array{version: int, location: string, world_minutes: float, player: array{x: float, y: float, z: float, yaw: float}, saved_at: string|null}
     */
    private function present(Save $save): array
    {
        return [
            'version' => $save->version,
            'location' => $save->location,
            'world_minutes' => $save->world_minutes,
            // JSON keeps -40.0 as -40: numbers go back out as numbers with a point.
            'player' => array_map(fn (int|float $value): float => (float) $value, $save->player),
            'saved_at' => $save->updated_at?->toIso8601String(),
        ];
    }

    private function success(mixed $data = null): JsonResponse
    {
        return response()->json(['success' => true, 'message' => '', 'data' => $data]);
    }

    /**
     * The player: the host application's user id — the one thing the game
     * shares with it.
     */
    private function playerId(Request $request): int
    {
        return (int) $request->user()->getAuthIdentifier();
    }
}
