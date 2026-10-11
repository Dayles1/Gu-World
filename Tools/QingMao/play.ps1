<#
.SYNOPSIS
  Runs the built game once (a picture, a timing) under a memory guard.

.DESCRIPTION
  Starts Builds/GuWorld-*/GuWorld.exe with the given arguments, windowed 1600x900, watches its
  memory every second (stops it if the system's free memory falls under -MinFreeMB, or after
  -TimeoutSeconds) and writes the peak to Logs/QingMaoBuild/play.log.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Tools\QingMao\play.ps1 -Arguments "-screenshot shot.png -view -2300,1.7,-1700,43,3 -ground -time 10 -bench 300"
#>
param(
    [string]$Arguments = "",
    [int]$MinFreeMB = 900,
    [int]$TimeoutSeconds = 240
)

$project = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$exe = Get-ChildItem -Path (Join-Path $project "Builds") -Filter GuWorld.exe -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$log = Join-Path $project "Logs\QingMaoBuild\play.log"
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null

function Note([string]$line) {
    $text = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $line"
    for ($i = 0; $i -lt 5; $i++) { try { [System.IO.File]::AppendAllText($log, $text + [Environment]::NewLine); break } catch { Start-Sleep -Milliseconds 100 } }
    Write-Output $text
}

if (-not $exe) { Note "NO BUILD FOUND"; exit 2 }
if (Get-Process -Name Unity, GuWorld -ErrorAction SilentlyContinue) { Note "NOT STARTED: Unity or the game is running"; exit 2 }

Note "START $($exe.FullName) $Arguments"
$process = Start-Process -FilePath $exe.FullName -ArgumentList ("-screen-fullscreen 0 -screen-width 1600 -screen-height 900 " + $Arguments) -PassThru
$null = $process.Handle
$started = Get-Date
$peak = 0
$reason = ""

while (-not $process.HasExited) {
    Start-Sleep -Seconds 1
    $process.Refresh()
    $held = $process.PrivateMemorySize64 / 1MB
    if ($held -gt $peak) { $peak = $held }
    $free = (Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1KB
    if ($free -lt $MinFreeMB) { $reason = "the system had $([int]$free) MB free" }
    elseif (((Get-Date) - $started).TotalSeconds -gt $TimeoutSeconds) { $reason = "it ran over $TimeoutSeconds s" }
    if ($reason) { Note "STOPPING: $reason"; Stop-Process -Id $process.Id -Force; break }
}

$process.WaitForExit()
Note ("END after {0:0} s, peak game memory {1:0} MB{2}" -f ((Get-Date) - $started).TotalSeconds, $peak, $(if ($reason) { ", stopped: $reason" } else { "" }))
