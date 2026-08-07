[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [switch] $NoBuild,

    # Which game's test suite to run. Defaults to 'Hukbo' so a caller that
    # never passes -Game gets today's behavior unchanged.
    [ValidateSet('Hukbo', 'Sandata')]
    [string] $Game = 'Hukbo'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')
. (Join-Path $PSScriptRoot '_gametargets.ps1')

$root = Get-RepositoryRoot
$target = Get-GameTarget -Game $Game
$testProjects = $target.Tests
Push-Location $root
try {
    foreach ($testProject in $testProjects) {
        if (-not $NoBuild) {
            Invoke-DotNet -Arguments @('restore', $testProject, '--locked-mode')
            Invoke-DotNet -Arguments @(
                'build',
                $testProject,
                '--configuration', $Configuration,
                '--no-restore'
            )
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
}
finally {
    Pop-Location
}

Write-Host "[PASS] $Configuration repository tests completed."
