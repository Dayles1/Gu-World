<?php

namespace App\Games\GuWorld\Models;

use App\Games\GuWorld\Database\Factories\SaveFactory;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Support\Carbon;

/**
 * A player's saved game in GU World: the save format's version, the
 * location the hero is in, the game minutes since the world began, and
 * where the hero stands. `user_id` points at the host application's users
 * table, which lives in another database — hence no relation, only the id.
 *
 * @property int $id
 * @property int $user_id
 * @property int $version
 * @property string $location
 * @property float $world_minutes
 * @property array{x: float, y: float, z: float, yaw: float} $player
 * @property Carbon|null $created_at
 * @property Carbon|null $updated_at
 */
class Save extends Model
{
    /** @use HasFactory<SaveFactory> */
    use HasFactory;

    protected $connection = 'gu_world';

    protected $fillable = [
        'user_id',
        'version',
        'location',
        'world_minutes',
        'player',
    ];

    /**
     * @return array<string, string>
     */
    protected function casts(): array
    {
        return [
            'user_id' => 'integer',
            'version' => 'integer',
            'world_minutes' => 'float',
            'player' => 'array',
        ];
    }

    protected static function newFactory(): SaveFactory
    {
        return SaveFactory::new();
    }
}
