# Sets up the tools the body build needs, outside the repo (re-runnable: every
# step is skipped when it is already done):
#   1. portable Blender 4.5 LTS            -> D:\Dev\Tools\blender\
#   2. the MPFB 2 extension (MakeHuman)    -> installed into that Blender
#   3. MakeHuman's CC0 system assets       -> MPFB's user data (skins, eyes, brows, lashes...)
# Downloads are checked against their SHA-256 before use.

param(
    [string]$Tools = "D:\Dev\Tools"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$blenderVersion = "4.5.14"
$blenderZip = "blender-$blenderVersion-windows-x64.zip"
$blenderUrl = "https://download.blender.org/release/Blender4.5/$blenderZip"
$blenderSha = "b9533d2397ac1984db4466fb23a7a4649391cca93f6e84209f9bcc60d071c8b9"
$blenderDir = Join-Path $Tools "blender\blender-$blenderVersion-windows-x64"
$blender = Join-Path $blenderDir "blender.exe"

$mpfbZip = "add-on-mpfb-v2.0.18.zip"
$mpfbSha = "c075b3e9b94b8e5556a5f03f4653602d02ea306de08126e8f55ed6dee28a2dad"
$mpfbUrl = "https://extensions.blender.org/download/sha256:$mpfbSha/$mpfbZip`?repository=%2Fapi%2Fv1%2Fextensions%2F&blender_version_min=4.2.0"

$assetsZip = "makehuman_system_assets_cc0.zip"
$assetsUrl = "https://files.makehumancommunity.org/asset_packs/makehuman_system_assets/$assetsZip"
$assetsSha = "b542127a8e25547c7c29c19f2d1d2adb9a664c80396ecd694095dbc8028a0107"

function Get-Checked([string]$url, [string]$dir, [string]$name, [string]$sha) {
    New-Item -ItemType Directory -Force $dir | Out-Null
    $file = Join-Path $dir $name
    if (-not (Test-Path $file) -or (Get-FileHash $file -Algorithm SHA256).Hash -ne $sha.ToUpper()) {
        Write-Host "Downloading $name ..."
        Invoke-WebRequest -Uri $url -OutFile $file -UseBasicParsing
    }
    $hash = (Get-FileHash $file -Algorithm SHA256).Hash
    if ($hash -ne $sha.ToUpper()) { throw "$name has SHA-256 $hash, expected $sha" }
    return $file
}

# 1. Blender, portable: its settings and extensions live in a "portable" folder beside it.
if (-not (Test-Path $blender)) {
    $zip = Get-Checked $blenderUrl (Join-Path $Tools "blender\download") $blenderZip $blenderSha
    Write-Host "Unpacking Blender ..."
    Expand-Archive -Path $zip -DestinationPath (Join-Path $Tools "blender") -Force
}
New-Item -ItemType Directory -Force (Join-Path $blenderDir "portable") | Out-Null
Write-Host "Blender: $blender"

# 2. MPFB.
$mpfbDir = Join-Path $blenderDir "portable\extensions\user_default\mpfb"
if (-not (Test-Path $mpfbDir)) {
    $zip = Get-Checked $mpfbUrl (Join-Path $Tools "mpfb\download") $mpfbZip $mpfbSha
    Write-Host "Installing MPFB ..."
    & $blender -c extension install-file -r user_default -e $zip
    if (-not (Test-Path $mpfbDir)) { throw "MPFB did not install" }
}
Write-Host "MPFB: $mpfbDir"

# 3. The system assets, unpacked into MPFB's user data.
$data = Join-Path $blenderDir "portable\extensions\.user\user_default\mpfb\data"
if (-not (Test-Path (Join-Path $data "skins\young_asian_male"))) {
    $zip = Get-Checked $assetsUrl (Join-Path $Tools "mpfb\assets-download") $assetsZip $assetsSha
    Write-Host "Unpacking the MakeHuman system assets ..."
    New-Item -ItemType Directory -Force $data | Out-Null
    Expand-Archive -Path $zip -DestinationPath $data -Force
}
Write-Host "Assets: $data"
Write-Host "Ready. Build with: powershell -ExecutionPolicy Bypass -File $(Join-Path $PSScriptRoot 'build.ps1')"
