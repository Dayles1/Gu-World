<#
.SYNOPSIS
  Builds Qing Mao Mountain (Unity batch mode, in stages) under a memory guard.

.DESCRIPTION
  Starts Unity once (never two at a time), runs the mountain builder's stages and
  watches memory every 2 seconds: if Unity's processes hold more than -LimitMB, or the
  system has less than -MinFreeMB free, Unity is stopped before the machine runs out
  and the reason is written down. A stopped or failed build is not started again by
  itself: run the script again and it goes on from the first stage not done.

  Logs: Logs/QingMaoBuild/progress.log (the builder's steps and the memory at each),
        Logs/QingMaoBuild/guard.log    (this script: memory over time, why it stopped),
        Logs/QingMaoBuild/unity-*.log  (Unity's own log of the run).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Tools\QingMao\build.ps1
  powershell -ExecutionPolicy Bypass -File Tools\QingMao\build.ps1 -Stages terrain
  powershell -ExecutionPolicy Bypass -File Tools\QingMao\build.ps1 -Test -Area "-1960,6440,1400" -Stages "terrain,fine,village-bai,assemble"
#>
param(
    # The stages to run (comma-separated); empty: every stage not done yet.
    [string]$Stages = "",
    # Build into GeneratedTest instead of the game's Generated.
    [switch]$Test,
    # Only what lies in the square "x,z,size" (metres).
    [string]$Area = "",
    # Unity's memory (all its processes, private bytes) above this stops it, MB.
    [int]$LimitMB = 4500,
    # The system's free memory below this stops it, MB.
    [int]$MinFreeMB = 1500,
    # Another method to run under the guard (a test, a render).
    [string]$Method = "GuWorld.Editor.QingMaoBuilder.BuildStages",
    # More arguments for Unity.
    [string]$Extra = "",
    # With a graphics device (the builder needs none; without one it takes less memory).
    [switch]$Graphics,
    # Without -quit: for methods that go on in play mode and exit by themselves (the route walk, the smoke test).
    [switch]$NoQuit,
    # The longest a run may take, minutes.
    [int]$TimeoutMinutes = 60,
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe"
)

$project = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$logs = Join-Path $project "Logs\QingMaoBuild"
New-Item -ItemType Directory -Force $logs | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$unityLog = Join-Path $logs "unity-$stamp.log"
$guard = Join-Path $logs "guard.log"
$progress = Join-Path $logs "progress.log"

function Note([string]$line) {
    $text = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $line"
    # Another reader (a log viewer) may hold the file: retried, and never fatal.
    for ($i = 0; $i -lt 5; $i++) {
        try { [System.IO.File]::AppendAllText($guard, $text + [Environment]::NewLine); break } catch { Start-Sleep -Milliseconds 100 }
    }
    Write-Host $text
}

if (Get-Process -Name Unity -ErrorAction SilentlyContinue) {
    Note "NOT STARTED: Unity is already running (one heavy Unity at a time)."
    exit 2
}

# The compiler servers an earlier Unity left running (each up to 1.2 GB): only those of this Unity install.
function Stop-Leftovers {
    $editor = Split-Path $Unity
    Get-CimInstance Win32_Process -Filter "Name='dotnet.exe' or Name='VBCSCompiler.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine.Contains($editor) } |
        ForEach-Object {
            Note "  stopping a compiler server Unity left running (pid $($_.ProcessId))"
            Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
        }
}

Stop-Leftovers

# One Unity run under the guard: its exit code (3 if the guard stopped it).
function Invoke-Unity([string[]]$StageArgs) {
    $unityLog = Join-Path $logs ("unity-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".log")
    $arguments = @("-batchmode", "-projectPath", "`"$project`"", "-executeMethod", $Method, "-logFile", "`"$unityLog`"")
    if ($StageArgs) { $arguments += $StageArgs }
    if ($Test) { $arguments += "-test" }
    if ($Area) { $arguments += @("-area", "`"$Area`"") }
    if ($Extra) { $arguments += $Extra.Split(" ", [System.StringSplitOptions]::RemoveEmptyEntries) }
    if (-not $Graphics) { $arguments += "-nographics" }
    if (-not $NoQuit) { $arguments += "-quit" }

    Note "START $Method $($StageArgs -join ' ') test=$Test area='$Area' limit=${LimitMB}MB minFree=${MinFreeMB}MB log=$unityLog"
    $process = Start-Process -FilePath $Unity -ArgumentList $arguments -PassThru
    $null = $process.Handle  # keeps the exit code readable once it is done
    $started = Get-Date
    $peak = 0
    $reason = ""
    $lastNote = Get-Date
    $cleaned = $false

    while (-not $process.HasExited) {
        Start-Sleep -Seconds 2

        # Once the builder runs, the scripts are compiled: the compiler server Unity keeps up (1.2 GB) is not needed.
        if (-not $cleaned -and (Test-Path $progress) -and ((Get-Item $progress).LastWriteTime -gt $started)) {
            $cleaned = $true
            Stop-Leftovers
            # The asset import worker Unity started with itself (a whole Unity, 1.4 GB): the builder imports in its own process.
            Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue |
                Where-Object { $_.CommandLine -and $_.CommandLine.Contains("AssetImportWorker") } |
                ForEach-Object {
                    Note "  stopping Unity's idle import worker (pid $($_.ProcessId))"
                    Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
                }
        }

        $held = (Get-Process -Name Unity, UnityShaderCompiler -ErrorAction SilentlyContinue | Measure-Object -Property PrivateMemorySize64 -Sum).Sum / 1MB
        $free = (Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1KB
        if ($held -gt $peak) { $peak = $held }

        if ($held -gt $LimitMB) { $reason = "Unity held $([int]$held) MB, over the limit of $LimitMB MB" }
        elseif ($free -lt $MinFreeMB) { $reason = "the system had $([int]$free) MB free, under $MinFreeMB MB" }
        elseif (((Get-Date) - $started).TotalMinutes -gt $TimeoutMinutes) { $reason = "it ran over $TimeoutMinutes minutes" }

        if ($reason) {
            Note "STOPPING: $reason"
            Note ("  unity processes: " + ((Get-Process -Name Unity -ErrorAction SilentlyContinue | ForEach-Object { "{0}:{1:0}MB" -f $_.Id, ($_.PrivateMemorySize64 / 1MB) }) -join " "))
            Get-Process -Name Unity, UnityShaderCompiler -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
            break
        }

        if (((Get-Date) - $lastNote).TotalSeconds -ge 30) {
            $lastNote = Get-Date
            $step = if (Test-Path $progress) { (Get-Content $progress -Tail 1 -Encoding UTF8) } else { "" }
            $each = (Get-Process -Name Unity -ErrorAction SilentlyContinue | ForEach-Object { "{0}:{1:0}" -f $_.Id, ($_.PrivateMemorySize64 / 1MB) }) -join " "
            Note "  unity processes: $each"
            Note ("  {0:0} s: Unity {1:0} MB (peak {2:0}), free {3:0} MB | {4}" -f ((Get-Date) - $started).TotalSeconds, $held, $peak, $free, $step)
        }
    }

    $process.WaitForExit()
    Start-Sleep -Seconds 2
    Stop-Leftovers
    $seconds = [int]((Get-Date) - $started).TotalSeconds
    $code = if ($reason) { "stopped by the guard" } else { $process.ExitCode }
    Note "END after $seconds s, exit: $code, peak Unity memory: $([int]$peak) MB"
    if ($reason) { return 3 }
    return $process.ExitCode
}

# The mountain's stages: all but the scene in one Unity, then the scene in a fresh one — a long run's memory
# grows (fragments), and putting the scene together needs it all at once.
$all = "terrain,fine,village-gu_yue,village-xiong,village-bai,water,trails,hamlets,giants"
if ($Method -eq "GuWorld.Editor.QingMaoBuilder.BuildStages") {
    $wanted = if ($Stages) { $Stages.Split(",") | ForEach-Object { $_.Trim() } } else { @() }
    $before = if ($Stages) { ($wanted | Where-Object { $_ -ne "assemble" }) -join "," } else { "" }
    $code = 0
    if (-not $Stages) { $code = Invoke-Unity @("-except", "assemble") }
    elseif ($before) { $code = Invoke-Unity @("-stages", $before) }
    if ($code -eq 0 -and (-not $Stages -or $wanted -contains "assemble")) {
        $code = if ($Stages) { Invoke-Unity @("-stages", "assemble") } else { Invoke-Unity @("-except", $all) }
    }
} else {
    $code = Invoke-Unity @()
}

if (Test-Path $progress) {
    Write-Output "---- the builder's last steps (progress.log) ----"
    Get-Content $progress -Tail 12 -Encoding UTF8
}

exit $code
