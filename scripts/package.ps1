[CmdletBinding()]
param(
    [ValidateSet('win-x64')]
    [string] $Runtime = 'win-x64'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

$root = Get-RepositoryRoot
$clientProject = 'src/AutonomousArena.Client/AutonomousArena.Client.csproj'
$output = Join-Path $root "artifacts/packages/client-$Runtime"

New-Item -ItemType Directory -Force -Path $output | Out-Null

Push-Location $root
try {
    $isSupportedHost = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Windows) -and
        [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq
        [System.Runtime.InteropServices.Architecture]::X64
    if (-not $isSupportedHost) {
        throw "Packaging $Runtime requires a Windows x64 host."
    }

    Restore-RepositoryTools
    Invoke-DotNet -Arguments @(
        'restore',
        $clientProject,
        '--locked-mode',
        '--runtime', $Runtime
    )
    Invoke-DotNet -Arguments @(
        'publish',
        $clientProject,
        '--configuration', 'Release',
        '--runtime', $Runtime,
        '--self-contained', 'true',
        '--no-restore',
        '--output', $output,
        '-p:PublishSingleFile=false'
    )
}
finally {
    Pop-Location
}

Write-Host "[PASS] Windows package published to $output"
