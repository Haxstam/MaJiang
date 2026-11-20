param(
    [string]$Python = 'D:\PycharmProjects\Envirment1\venv\Scripts\python.exe',
    [string]$ModelDirectory = 'G:\Documents\Codex\2026-10-24\qing-n\outputs\Mortal-S42',
    [string]$Seats = '0,1,2,3',
    [string]$Curses = '1,2,3,4',
    [ValidateSet('v1', 'v2')][string]$CurseRuleVersion = 'v1',
    [long]$Seed = 1,
    [int]$Rounds = 100,
    [string]$Output = (Join-Path $PSScriptRoot 'experiments'),
    [ValidateSet('experiment', 'benchmark', 'validation')][string]$Purpose = 'experiment',
    [int]$Threads = 2,
    [switch]$NoAudit,
    [switch]$Smoke,
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$CurseRuleVersion = $CurseRuleVersion.ToLowerInvariant()
if (!(Test-Path -LiteralPath $Python)) { throw "Python not found: $Python" }
$nativeTag = & $Python -B -c 'import sys; print("cp%d%d-win_amd64" % sys.version_info[:2])'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$nativeDirectory = Join-Path $PSScriptRoot ".utmp/python-native/$nativeTag"
if (!(Test-Path -LiteralPath (Join-Path $nativeDirectory 'libriichi.pyd')))
{
    throw 'Native encoder missing. Run ./build-mortal-native.ps1 with this Python first.'
}
$bridge = Join-Path $PSScriptRoot 'python/mortal_bridge.py'
if ($Smoke)
{
    & $Python -B -u $bridge --model-dir $ModelDirectory --native-dir $nativeDirectory --device cuda --smoke
    exit $LASTEXITCODE
}
$project = Join-Path $PSScriptRoot 'examples/MaJiang.MortalConsole/MaJiang.MortalConsole.csproj'
if (!$NoBuild)
{
    $env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.utmp/dotnet'
    dotnet build $project --configuration Release --disable-build-servers -m:1 --verbosity minimal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
$arguments = @(
    (Join-Path $PSScriptRoot 'examples/MaJiang.MortalConsole/bin/Release/net10.0/MaJiang.MortalConsole.dll'),
    '--python', $Python, '--model-dir', $ModelDirectory, '--bridge', $bridge,
    '--native-dir', $nativeDirectory, '--device', 'cuda', '--seats', $Seats,
    '--seed', $Seed.ToString(), '--rounds', $Rounds.ToString(),
    '--purpose', $Purpose, '--threads', $Threads.ToString(), '--curse-rule-version', $CurseRuleVersion
)
if ($Curses) { $arguments += @('--curses', $Curses) }
if ($Output) { $arguments += @('--output', $Output) }
if (!$NoAudit) { $arguments += '--audit' }
dotnet @arguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
