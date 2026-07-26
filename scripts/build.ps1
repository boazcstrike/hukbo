[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [switch] $NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

$root = Get-RepositoryRoot
Push-Location $root
try {
    Restore-RepositoryTools
    if (-not $NoRestore) {
        Invoke-DotNet -Arguments @('restore', 'AutonomousArena.slnx', '--locked-mode')
    }

    Invoke-DotNet -Arguments @('build', 'AutonomousArena.slnx', '--configuration', $Configuration, '--no-restore')
}
finally {
    Pop-Location
}

Write-Host "[PASS] $Configuration solution build completed."
