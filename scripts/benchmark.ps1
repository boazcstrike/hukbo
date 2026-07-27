[CmdletBinding()]
param(
    [ValidateRange(2, 100000)]
    [int] $Agents = 200,

    [ValidateRange(1, 100000000)]
    [int] $Ticks = 10000,

    [UInt64] $Seed = 1,

    [string] $Output,

    # Off by default so the canonical gate measures the simulation and not the
    # simulation plus a writer. Raise it only for a deliberate diagnostic run.
    [ValidateSet('off', 'err', 'warn', 'inf', 'dbg', 'trc')]
    [string] $LogLevel = 'off',

    [string] $LogChannels,

    [string] $LogDirectory,

    [switch] $NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

$root = Get-RepositoryRoot
$headlessProject = 'src/Hukbo.Headless/Hukbo.Headless.csproj'
Push-Location $root
try {
    if (-not $NoBuild) {
        Invoke-DotNet -Arguments @('restore', $headlessProject, '--locked-mode')
        Invoke-DotNet -Arguments @('build', $headlessProject, '--configuration', 'Release', '--no-restore')
    }

    $runnerArguments = @(
        'run',
        '--project', $headlessProject,
        '--configuration', 'Release',
        '--no-build',
        '--no-restore',
        '--',
        '--agents', [string]$Agents,
        '--ticks', [string]$Ticks,
        '--seed', [string]$Seed,
        '--log-level', $LogLevel
    )

    if (-not [string]::IsNullOrWhiteSpace($LogChannels)) {
        $runnerArguments += @('--log-channels', $LogChannels)
    }

    if (-not [string]::IsNullOrWhiteSpace($LogDirectory)) {
        $runnerArguments += @('--log-dir', $LogDirectory)
    }

    if (-not [string]::IsNullOrWhiteSpace($Output)) {
        $resolvedOutput = if ([System.IO.Path]::IsPathRooted($Output)) {
            $Output
        }
        else {
            Join-Path $root $Output
        }

        $outputDirectory = Split-Path -Parent $resolvedOutput
        if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
            New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
        }

        $runnerArguments += @('--output', $resolvedOutput)
    }

    Invoke-DotNet -Arguments $runnerArguments
}
finally {
    Pop-Location
}

Write-Host "[PASS] Headless workload completed: agents=$Agents ticks=$Ticks seed=$Seed."
