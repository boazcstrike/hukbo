[CmdletBinding()]
param(
    [switch] $SkipBootstrap
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

if (-not $SkipBootstrap) {
    Invoke-RepositoryScript -Name 'bootstrap.ps1'
}

Invoke-RepositoryScript -Name 'format.ps1' -Arguments @('-Verify')
Invoke-RepositoryScript -Name 'build.ps1' -Arguments @('-Configuration', 'Release', '-NoRestore')
Invoke-RepositoryScript -Name 'test.ps1' -Arguments @('-Configuration', 'Release', '-NoBuild')
Invoke-RepositoryScript -Name 'benchmark.ps1' -Arguments @(
    '-Agents', '200',
    '-Ticks', '10000',
    '-Seed', '1',
    '-NoBuild'
)

Write-Host '[PASS] Canonical repository verification completed.'
