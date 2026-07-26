[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [switch] $NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

$root = Get-RepositoryRoot
$clientProject = 'src/AutonomousArena.Client/AutonomousArena.Client.csproj'
Push-Location $root
try {
    Restore-RepositoryTools
    if (-not $NoBuild) {
        Invoke-DotNet -Arguments @('restore', $clientProject, '--locked-mode')
        Invoke-DotNet -Arguments @('build', $clientProject, '--configuration', $Configuration, '--no-restore')
    }

    Write-Host 'Starting Autonomous Arena. Press Escape for Play, Pause, and Exit Game.'
    Invoke-DotNet -Arguments @(
        'run',
        '--project', $clientProject,
        '--configuration', $Configuration,
        '--no-build',
        '--no-restore'
    )
}
finally {
    Pop-Location
}
