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
$testProject = 'tests/AutonomousArena.Core.Tests/AutonomousArena.Core.Tests.csproj'
Push-Location $root
try {
    if (-not $NoBuild) {
        Invoke-DotNet -Arguments @('restore', $testProject, '--locked-mode')
        Invoke-DotNet -Arguments @('build', $testProject, '--configuration', $Configuration, '--no-restore')
    }

    Invoke-DotNet -Arguments @(
        'test',
        $testProject,
        '--configuration', $Configuration,
        '--no-build',
        '--no-restore',
        '--logger', 'console;verbosity=normal'
    )
}
finally {
    Pop-Location
}

Write-Host "[PASS] $Configuration Core tests completed."
