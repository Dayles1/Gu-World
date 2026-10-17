# Builds the teenage bodies, verifies them against the current models and renders
# the comparison previews. Needs the portable Blender with MPFB (setup.ps1).
#
#   powershell -ExecutionPolicy Bypass -File Tools\Character\build.ps1
#   ... -Gender male            only the boy
#   ... -NoPreviews             skip the renders (about a minute)
#   ... -Params my.json         overrides for the presets in build_teen.py

param(
    [string]$Blender = "D:\Dev\Tools\blender\blender-4.5.14-windows-x64\blender.exe",
    [ValidateSet("male", "female", "both")]
    [string]$Gender = "both",
    [string]$Params = "",
    [switch]$NoPreviews
)

$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$out = Join-Path $here "out"
$models = Join-Path $here "..\..\Assets\GuWorld\Models" | Resolve-Path
New-Item -ItemType Directory -Force (Join-Path $out "work") | Out-Null

if (-not (Test-Path $Blender)) {
    throw "Blender not found at $Blender - run setup.ps1 or pass -Blender"
}

$genders = if ($Gender -eq "both") { @("male", "female") } else { @($Gender) }

foreach ($g in $genders) {
    $glb = Join-Path $out "human-teen-$g.glb"
    $blend = Join-Path $out "work\teen-$g.blend"
    $log = Join-Path $out "work\build-$g.log"
    $buildArgs = @("-b", "--python", (Join-Path $here "build_teen.py"), "--", "--gender", $g, "--out", $glb, "--blend", $blend)
    if ($Params) { $buildArgs += @("--params", $Params) }
    Write-Host "Building $g ..."
    & $Blender @buildArgs *> $log
    if ($LASTEXITCODE -ne 0 -or -not (Select-String -Path $log -Pattern "^\[teen\] done" -Quiet)) {
        Get-Content $log -Tail 30
        throw "Building the $g body failed (log: $log)"
    }
    Select-String -Path $log -Pattern "^\[teen\]" | Where-Object { $_.Line -notmatch "preset:" } | ForEach-Object { $_.Line }

    $report = Join-Path $out "verify-$g.txt"
    & $Blender -b --factory-startup --python (Join-Path $here "verify_glb.py") -- --new $glb --old (Join-Path $models "human-$g.glb") --report $report *> (Join-Path $out "work\verify-$g.log")
    if (-not (Test-Path $report)) { throw "Verifying the $g body failed" }
    Write-Host "Verification report: $report"
}

if (-not $NoPreviews) {
    Write-Host "Rendering previews ..."
    $previewArgs = @("-b", "--factory-startup", "--python", (Join-Path $here "render_previews.py"), "--", "--out", (Join-Path $out "preview"),
        "--model", "current male=$(Join-Path $models 'human-male.glb')")
    foreach ($g in @("male", "female")) {
        $glb = Join-Path $out "human-teen-$g.glb"
        if (Test-Path $glb) { $previewArgs += @("--model", "teen $g=$glb") }
    }
    $previewArgs += @("--model", "current female=$(Join-Path $models 'human-female.glb')")
    & $Blender @previewArgs *> (Join-Path $out "work\previews.log")
    Get-ChildItem (Join-Path $out "preview-*.png") | ForEach-Object { Write-Host "  $($_.FullName)" }
}
