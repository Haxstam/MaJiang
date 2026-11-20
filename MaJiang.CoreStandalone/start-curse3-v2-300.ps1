param(
    [string]$OutputRoot = 'G:\Documents\Test',
    [ValidateRange(1, 2)][int]$Concurrency = 2,
    [long]$Seed = 1101
)
$ErrorActionPreference = 'Stop'

# A new directory and frozen runtime are created for every invocation.
# V2 changes only curse 3's initial hand; curses 1, 2, and 4 retain V1 rules.
& (Join-Path $PSScriptRoot 'manage-background.ps1') -Action Start `
    -OutputRoot $OutputRoot -Count 300 -Concurrency $Concurrency `
    -Seed $Seed -Purpose experiment -CurseRuleVersion v2
