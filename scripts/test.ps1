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
$testProjects = @(
    'tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj'
    'tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj'
)
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
