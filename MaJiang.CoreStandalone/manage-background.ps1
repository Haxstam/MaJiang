param(
    [ValidateSet('Start', 'Status', 'Stop', 'Resume')][string]$Action = 'Start',
    [string]$BatchDirectory = '',
    [string]$OutputRoot = 'G:\Documents\Test',
    [int]$Count = 1000,
    [int]$Concurrency = 2,
    [ValidateSet('experiment', 'validation')][string]$Purpose = 'experiment',
    [ValidateSet('v1', 'v2')][string]$CurseRuleVersion = 'v1',
    [long]$Seed = 101,
    [string]$Python = 'D:\PycharmProjects\Envirment1\venv\Scripts\python.exe',
    [string]$ModelDirectory = 'G:\Documents\Codex\2026-10-24\qing-n\outputs\Mortal-S42',
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$CurseRuleVersion = $CurseRuleVersion.ToLowerInvariant()
$projectRoot = $PSScriptRoot
function Read-Json([string]$Path) { Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json }
function Write-Json([string]$Path, $Value) {
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
}
function Show-Status([string]$Directory) {
    $info = Read-Json (Join-Path $Directory 'background.json')
    $task = Get-ScheduledTask -TaskName $info.taskName -ErrorAction SilentlyContinue
    $batchPath = Join-Path $Directory 'records/batch.json'
    $batch = if (Test-Path -LiteralPath $batchPath) { Read-Json $batchPath } else { $null }
    [pscustomobject]@{
        Directory = $Directory
        SupervisorStatus = $info.status
        TaskState = if ($task) { $task.State } else { 'NotFound' }
        BatchStatus = if ($batch) { $batch.status } else { 'Starting' }
        Counts = if ($batch) { $batch.statusCounts | ConvertTo-Json -Compress } else { '' }
        LastProgress = if ($batch) { $batch.updatedAtUtc } else { $info.createdAtUtc }
        StopRequested = Test-Path -LiteralPath (Join-Path $Directory 'stop.requested')
        Summary = Join-Path $Directory 'records/summary'
    } | Format-List
}
if ($Action -ne 'Start') {
    if (!$BatchDirectory) {
        $BatchDirectory = (Get-Content -LiteralPath (Join-Path $projectRoot '.utmp/last-background-batch.txt') -Raw).Trim()
    }
    $BatchDirectory = [IO.Path]::GetFullPath($BatchDirectory)
    $info = Read-Json (Join-Path $BatchDirectory 'background.json')
    if ($Action -eq 'Status') { Show-Status $BatchDirectory; exit 0 }
    $task = Get-ScheduledTask -TaskName $info.taskName
    if ($Action -eq 'Stop') {
        if ($task.State -ne 'Running') { Show-Status $BatchDirectory; exit 0 }
        [IO.File]::WriteAllText((Join-Path $BatchDirectory 'stop.requested'), [DateTimeOffset]::UtcNow.ToString('O'))
        Write-Output 'Graceful stop requested. Active matches will finish; no further matches will start.'
        Show-Status $BatchDirectory
        exit 0
    }
    if ($task.State -eq 'Running') { throw 'The task is already running. Do not start a duplicate batch.' }
    $batch = Read-Json (Join-Path $BatchDirectory 'records/batch.json')
    if ($batch.status -eq 'completed') { Write-Output 'Batch already completed.'; Show-Status $BatchDirectory; exit 0 }
    if ($batch.status -ne 'stopped' -or $info.status -ne 'stopped') {
        throw 'Resume requires a clean graceful stop. Failed or interrupted attempts require inspection first.'
    }
    $stopFile = Join-Path $BatchDirectory 'stop.requested'
    if (Test-Path -LiteralPath $stopFile) { Remove-Item -LiteralPath $stopFile }
    Start-ScheduledTask -TaskName $info.taskName
    Show-Status $BatchDirectory
    exit 0
}
if ($Count -lt 1 -or $Concurrency -lt 1 -or $Concurrency -gt 2) { throw 'Count must be positive; concurrency must be 1 or 2.' }
if (!(Test-Path -LiteralPath $Python)) { throw "Python not found: $Python" }
$pythonw = Join-Path (Split-Path -Parent $Python) 'pythonw.exe'
if (!(Test-Path -LiteralPath $pythonw)) { throw "pythonw not found: $pythonw" }
$nativeTag = & $Python -B -c 'import sys; print("cp%d%d-win_amd64" % sys.version_info[:2])'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$nativeSource = Join-Path $projectRoot ".utmp/python-native/$nativeTag/libriichi.pyd"
if (!(Test-Path -LiteralPath $nativeSource)) { throw 'Build the Python native encoder before starting.' }
if (!$NoBuild) {
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.utmp/dotnet'
    dotnet build (Join-Path $projectRoot 'examples/MaJiang.MortalConsole/MaJiang.MortalConsole.csproj') --configuration Release --disable-build-servers -m:1 --verbosity minimal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
if (!$BatchDirectory) {
    $chinaNow = [TimeZoneInfo]::ConvertTime([DateTimeOffset]::UtcNow, [TimeZoneInfo]::FindSystemTimeZoneById('China Standard Time'))
    $BatchDirectory = Join-Path $OutputRoot ('batch-' + $Count + '-background-' + $chinaNow.ToString('yyyyMMdd-HHmmss'))
}
$BatchDirectory = [IO.Path]::GetFullPath($BatchDirectory)
if (Test-Path -LiteralPath $BatchDirectory) { throw 'Output directory already exists; it will not be overwritten.' }
$runtime = Join-Path $BatchDirectory 'runtime'
$pythonTarget = Join-Path $runtime 'python'
$hostSource = Join-Path $projectRoot 'examples/MaJiang.MortalConsole/bin/Release/net10.0'
$hostTarget = Join-Path $runtime 'examples/MaJiang.MortalConsole/bin/Release/net10.0'
$nativeTarget = Join-Path $runtime ".utmp/python-native/$nativeTag"
foreach ($directory in @($pythonTarget, $hostTarget, $nativeTarget)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
foreach ($file in (Get-ChildItem -LiteralPath $hostSource -File)) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $hostTarget $file.Name) }
foreach ($name in @('run_matches.py', 'background_worker.py', 'benchmark_mortal.py', 'mortal_bridge.py', 'summarize_matches.py', 'player_statistics.py', 'analyze_batch.py', 'analyze_hypotheses.py')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot "python/$name") -Destination (Join-Path $pythonTarget $name)
}
Copy-Item -LiteralPath $nativeSource -Destination (Join-Path $nativeTarget 'libriichi.pyd')
Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $runtime 'manage-background.ps1')
Copy-Item -LiteralPath (Join-Path $projectRoot 'BACKGROUND_RUNS.zh-CN.md') -Destination (Join-Path $BatchDirectory 'README.zh-CN.md')
Copy-Item -LiteralPath (Join-Path $projectRoot 'PLAYER_STATISTICS.zh-CN.md') -Destination (Join-Path $runtime 'PLAYER_STATISTICS.zh-CN.md')
foreach ($command in @('Status', 'Stop', 'Resume')) {
    $wrapper = '& (Join-Path $PSScriptRoot ''runtime/manage-background.ps1'') -Action ' + $command + ' -BatchDirectory $PSScriptRoot'
    [IO.File]::WriteAllText((Join-Path $BatchDirectory ($command.ToLower() + '.ps1')), $wrapper, [Text.UTF8Encoding]::new($false))
}
$taskName = 'MaJiang-' + ([IO.Path]::GetFileName($BatchDirectory) -replace '[^a-zA-Z0-9_-]', '_') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6)
$snapshot = @(Get-ChildItem -LiteralPath $runtime -File -Recurse | ForEach-Object {
    [pscustomobject]@{ path = $_.FullName.Substring($runtime.Length + 1); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$info = [ordered]@{
    schemaVersion = 1; status = 'prepared'; taskName = $taskName; createdAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    clientTimezone = 'Asia/Shanghai'; runtimeDirectory = $runtime
    configuration = @{ count = $Count; concurrency = $Concurrency; firstSeed = $Seed; python = $Python; modelDirectory = $ModelDirectory; purpose = $Purpose; curseRuleVersion = $CurseRuleVersion }
    snapshotFiles = $snapshot
}
Write-Json (Join-Path $BatchDirectory 'background.json') $info
$taskAction = New-ScheduledTaskAction -Execute $pythonw -Argument ('-B -u "{0}" --root "{1}"' -f (Join-Path $pythonTarget 'background_worker.py'), $BatchDirectory) -WorkingDirectory $runtime
$principal = New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
Register-ScheduledTask -TaskName $taskName -Action $taskAction -Principal $principal -Settings $settings -Description 'User-requested isolated Mahjong batch; manually started, no recurring trigger' | Out-Null
New-Item -ItemType Directory -Path (Join-Path $projectRoot '.utmp') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $projectRoot '.utmp/last-background-batch.txt'), $BatchDirectory)
Start-ScheduledTask -TaskName $taskName
Write-Output ('Background batch started: ' + $BatchDirectory)
Show-Status $BatchDirectory
