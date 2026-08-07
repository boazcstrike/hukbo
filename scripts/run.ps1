[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    # Leave unset to take the build default: verbose in Debug, silent in
    # Release. Set explicitly to override either.
    [ValidateSet('off', 'err', 'warn', 'inf', 'dbg', 'trc')]
    [string] $LogLevel,

    [string] $LogChannels,

    [switch] $NoBuild,

    # Which game's client to launch. Defaults to 'Hukbo' so a caller that
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
$clientProject = $target.Client
Push-Location $root
try {
    Restore-RepositoryTools
    if (-not $NoBuild) {
        Invoke-DotNet -Arguments @('restore', $clientProject, '--locked-mode')
        Invoke-DotNet -Arguments @('build', $clientProject, '--configuration', $Configuration, '--no-restore')
    }

    # Scoped to this process and the child it launches; the caller's shell is
    # left alone.
    if (-not [string]::IsNullOrWhiteSpace($LogLevel)) {
        $env:HUKBO_LOG_LEVEL = $LogLevel
    }

    if (-not [string]::IsNullOrWhiteSpace($LogChannels)) {
        $env:HUKBO_LOG_CHANNELS = $LogChannels
    }

    $effectiveLevel = if (-not [string]::IsNullOrWhiteSpace($LogLevel)) {
        $LogLevel
    }
    elseif ($Configuration -eq 'Debug') {
        'dbg (Debug default)'
    }
    else {
        'off (Release default)'
    }

    Write-Host "Debug log level: $effectiveLevel"
    Write-Host "Debug log directory: $(Join-Path $root 'artifacts/logs')"
    Write-Host "Starting $Game. Press Escape for Play, Pause, and Exit Game."
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
