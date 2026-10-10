<?php

use Illuminate\Support\Facades\Route;

/*
|--------------------------------------------------------------------------
| GU World — the page
|--------------------------------------------------------------------------
|
| One HTML shell; the game runs in the browser (client/). Like the rest
| of the app the HTML is public — the client checks the bearer token
| against the API and sends guests to /login.
|
*/

Route::get('/games/gu-world', fn () => view('gu-world::app'))->name('gu-world');
