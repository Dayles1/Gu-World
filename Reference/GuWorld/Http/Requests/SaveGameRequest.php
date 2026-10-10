<?php

namespace App\Games\GuWorld\Http\Requests;

use Illuminate\Foundation\Http\FormRequest;
use Illuminate\Validation\Rule;
use Illuminate\Validation\Validator;

/**
 * A saved game, format v1, as the browser sends it — not trusted: every
 * field is checked against the world's settings (config/gu_world.php
 * `world`), the same ones the client plays by.
 *
 * - the format's version must be this one;
 * - the location must be one of the world's;
 * - the game minutes a number within range;
 * - the hero's position inside that location's bounds, the facing within
 *   a couple of turns;
 * - nothing else: unknown fields, at the top or in the position, are
 *   refused, and so is a body larger than MAX_BYTES.
 *
 * JSON has no NaN or Infinity (a browser sends null for them), so they
 * fail as "not a number".
 */
class SaveGameRequest extends FormRequest
{
    public const int VERSION = 1;

    /** The largest request body a save may have, bytes. */
    public const int MAX_BYTES = 8192;

    /** The most game minutes a save may hold (about 1900 game years). */
    public const int MAX_WORLD_MINUTES = 1_000_000_000;

    /** @var list<string> */
    public const array FIELDS = ['version', 'location', 'world_minutes', 'player'];

    public function authorize(): bool
    {
        return true;
    }

    protected function prepareForValidation(): void
    {
        abort_if(strlen($this->getContent()) > self::MAX_BYTES, 413, 'The save is too large.');
    }

    /**
     * @return array<string, mixed>
     */
    public function rules(): array
    {
        $locations = (array) config('gu_world.world.locations', []);
        $location = $this->input('location');
        $bounds = is_string($location) ? ($locations[$location]['bounds'] ?? null) : null;
        $within = fn (string $axis): array => is_array($bounds)
            ? ["between:{$bounds["min_{$axis}"]},{$bounds["max_{$axis}"]}"]
            : [];

        return [
            'version' => ['required', 'integer', Rule::in([self::VERSION])],
            'location' => ['required', 'string', Rule::in(array_keys($locations))],
            'world_minutes' => ['required', 'numeric', 'min:0', 'max:'.self::MAX_WORLD_MINUTES],
            'player' => ['required', 'array:x,y,z,yaw'],
            'player.x' => ['required', 'numeric', ...$within('x')],
            'player.y' => ['required', 'numeric', ...$within('y')],
            'player.z' => ['required', 'numeric', ...$within('z')],
            'player.yaw' => ['required', 'numeric', 'between:-7,7'],
        ];
    }

    /**
     * @return list<callable(Validator): void>
     */
    public function after(): array
    {
        return [
            function (Validator $validator): void {
                $unknown = array_diff(array_keys($this->all()), self::FIELDS);

                if ($unknown !== []) {
                    $validator->errors()->add('save', 'Unknown fields: '.implode(', ', $unknown).'.');
                }
            },
        ];
    }

    /**
     * The save, its numbers as numbers.
     *
     * @return array{version: int, location: string, world_minutes: float, player: array{x: float, y: float, z: float, yaw: float}}
     */
    public function save(): array
    {
        /** @var array{version: int|string, location: string, world_minutes: int|float|string, player: array{x: int|float|string, y: int|float|string, z: int|float|string, yaw: int|float|string}} $save */
        $save = $this->validated();

        return [
            'version' => (int) $save['version'],
            'location' => $save['location'],
            'world_minutes' => (float) $save['world_minutes'],
            'player' => [
                'x' => (float) $save['player']['x'],
                'y' => (float) $save['player']['y'],
                'z' => (float) $save['player']['z'],
                'yaw' => (float) $save['player']['yaw'],
            ],
        ];
    }
}
