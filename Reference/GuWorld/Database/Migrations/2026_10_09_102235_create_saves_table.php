<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    /**
     * Lives on GU World's own database, not the main one.
     */
    protected $connection = 'gu_world';

    /**
     * One saved game per player: which version of the save format wrote
     * it, which location the hero is in, how many game minutes have passed
     * since the world began (day and time of day follow from it) and where
     * the hero stands. `user_id` is the host application's user id, kept as
     * a plain number: the users table is in another database, so there is
     * no foreign key.
     */
    public function up(): void
    {
        Schema::connection($this->connection)->create('saves', function (Blueprint $table) {
            $table->id();
            $table->unsignedBigInteger('user_id')->unique();
            $table->unsignedSmallInteger('version');
            $table->string('location', 64);
            $table->double('world_minutes');
            $table->json('player');
            $table->timestamps();
        });
    }

    public function down(): void
    {
        Schema::connection($this->connection)->dropIfExists('saves');
    }
};
