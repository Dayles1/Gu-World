<!DOCTYPE html>
{{-- GU World's HTML shell: a root for the game. Everything else lives in
     app/Games/GuWorld/client. --}}
<html lang="{{ str_replace('_', '-', app()->getLocale()) }}">

<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover">
    <meta name="theme-color" content="#0c0d0f">
    <title>{{ config('app.name', 'Laravel') }} · GU World</title>

    {{-- Unbounded for headings and numbers, Golos for text, JetBrains Mono for keys. --}}
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Golos+Text:wght@400;500;600;700&family=JetBrains+Mono:wght@700&family=Unbounded:wght@600;700;800&display=swap">

    @vite('app/Games/GuWorld/client/main.ts')
</head>

<body>
    <div id="gu-world-root" data-back-url="{{ url(config('gu_world.back_url')) }}"></div>
    {{-- The world's settings (config/gu_world.php), the same ones the server checks saves against. --}}
    <script type="application/json" id="gu-world-settings">@json(config('gu_world.world'))</script>
</body>

</html>
