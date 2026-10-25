$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME = Join-Path $workspaceRoot '.utmp/dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
dotnet run --project (Join-Path $PSScriptRoot 'MaJiang.ScoringChecks/MaJiang.ScoringChecks.csproj') --configuration Release
if ($LASTEXITCODE -ne 0)
{
    exit $LASTEXITCODE
}
