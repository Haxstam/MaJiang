param(
    [string]$Python = 'D:\PycharmProjects\Envirment1\venv\Scripts\python.exe',
    [string]$MortalRoot = 'G:\Mortal-main'
)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $Python)) { throw "Python not found: $Python" }
$nativeTag = & $Python -B -c 'import sys; print("cp%d%d-win_amd64" % sys.version_info[:2])'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$nativeOutput = Join-Path $PSScriptRoot ".utmp/python-native/$nativeTag"
$targetOutput = Join-Path $PSScriptRoot '.utmp/mortal-native-target'
$previousPython = $env:PYO3_PYTHON
$previousTarget = $env:CARGO_TARGET_DIR
try
{
    $env:PYO3_PYTHON = $Python
    $env:CARGO_TARGET_DIR = $targetOutput
    cargo build --manifest-path (Join-Path $MortalRoot 'Cargo.toml') --package libriichi --release --locked --offline
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    New-Item -ItemType Directory -Path $nativeOutput -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $targetOutput 'release/riichi.dll') -Destination (Join-Path $nativeOutput 'libriichi.pyd')
    Write-Output "Native encoder: $nativeOutput/libriichi.pyd"
}
finally
{
    $env:PYO3_PYTHON = $previousPython
    $env:CARGO_TARGET_DIR = $previousTarget
}
