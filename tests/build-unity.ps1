$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME = Join-Path $workspaceRoot '.utmp/dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$targetPath = Join-Path $PSScriptRoot 'UnitySources.targets'
dotnet build (Join-Path $workspaceRoot 'MaJiangUnity/MaJiangUnity.sln') --no-restore --nologo "-p:CustomAfterMicrosoftCommonTargets=$targetPath"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
