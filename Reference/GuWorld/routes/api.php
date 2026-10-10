<?php

use App\Games\GuWorld\Http\Controllers\SaveController;
use Illuminate\Support\Facades\Route;

/*
|--------------------------------------------------------------------------
| GU World — API (/api/gu-world/*)
|--------------------------------------------------------------------------
|
| Behind the host app's `auth.api` group: the player is whoever the
| bearer token belongs to.
|
*/

Route::middleware('auth.api')->group(function (): void {
    Route::get('save', [SaveController::class, 'show'])->name('save.show');
    // Autosaves come every 30 s of play and on pausing: 30 a minute is plenty.
    Route::put('save', [SaveController::class, 'update'])->middleware('throttle:30,1')->name('save.update');
    Route::delete('save', [SaveController::class, 'destroy'])->name('save.destroy');
});
