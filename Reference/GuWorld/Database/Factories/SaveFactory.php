<?php

namespace App\Games\GuWorld\Database\Factories;

use App\Games\GuWorld\Models\Save;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends Factory<Save>
 */
class SaveFactory extends Factory
{
    protected $model = Save::class;

    /**
     * A game saved on the first morning in the test valley.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        return [
            'user_id' => fake()->unique()->numberBetween(1, 1_000_000),
            'version' => 1,
            'location' => 'test_valley',
            'world_minutes' => 7 * 60,
            'player' => [
                'x' => fake()->randomFloat(2, -50, 50),
                'y' => fake()->randomFloat(2, 0, 10),
                'z' => fake()->randomFloat(2, -50, 50),
                'yaw' => fake()->randomFloat(2, -3, 3),
            ],
        ];
    }
}
