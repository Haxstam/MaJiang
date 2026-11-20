$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.utmp/dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
dotnet build (Join-Path $PSScriptRoot 'MaJiang.CoreStandalone.sln') --configuration Release --disable-build-servers -m:1 --verbosity minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
foreach ($checkName in @('MaJiang.CoreChecks', 'MaJiang.ScoringChecks', 'MaJiang.MortalChecks', 'MaJiang.CurseChecks'))
{
    dotnet (Join-Path $PSScriptRoot "tests/$checkName/bin/Release/net10.0/$checkName.dll")
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
