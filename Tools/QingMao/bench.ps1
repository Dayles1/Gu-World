<#
.SYNOPSIS
  Times the built game in a few set-ups, one after another (play.ps1 each), and gathers the results.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Tools\QingMao\bench.ps1 -Out Logs\QingMaoBuild\bench -Runs "high|-quality 2;low|-quality 0;notrees|-quality 2 -off trees"
#>
param(
    # name|arguments;name|arguments
    [string]$Runs = "high|-quality 2",
    [string]$Out = "",
    [string]$View = "",
    [int]$Frames = 240,
    # The game to run (play.ps1 -Game); empty: the game's own build.
    [string]$Game = "",
    [int]$Width = 1600,
    [int]$Height = 900
)

$project = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
if (-not $Out) { $Out = Join-Path $project "Logs\QingMaoBuild\bench" }
New-Item -ItemType Directory -Force $Out | Out-Null
$summary = Join-Path $Out "summary.txt"

foreach ($run in $Runs.Split(";")) {
    $name, $more = $run.Split("|")
    $shot = Join-Path $Out "$name.png"
    Remove-Item "$shot.txt" -ErrorAction SilentlyContinue
    $view = if ($View) { "-view $View -ground" } else { "" }
    & (Join-Path $PSScriptRoot "play.ps1") -Game $Game -Width $Width -Height $Height -Arguments "-screenshot `"$shot`" $view -time 0.45 -weather clear -weather fair -bench $Frames $more" | Out-Null
    $result = if (Test-Path "$shot.txt") { (Get-Content "$shot.txt") -join " | " } else { "no result" }
    $peak = (Get-Content (Join-Path $project "Logs\QingMaoBuild\play.log") -Tail 1)
    $line = "$name ($more): $result || $peak"
    Add-Content -Path $summary -Value $line -Encoding UTF8
    Write-Output $line
}
