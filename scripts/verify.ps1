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

Invoke-RepositoryScript -Name 'format.ps1' -Parameters @{
    Verify = $true
}
Invoke-RepositoryScript -Name 'build.ps1' -Parameters @{
    Configuration = 'Release'
    NoRestore = $true
}
Invoke-RepositoryScript -Name 'test.ps1' -Parameters @{
    Configuration = 'Release'
    NoBuild = $true
}
Invoke-RepositoryScript -Name 'benchmark.ps1' -Parameters @{
    Agents = 200
    Ticks = 10000
    Seed = 1
    NoBuild = $true
}

Write-Host '[PASS] Canonical repository verification completed.'
