[CmdletBinding()]
param(
    [switch] $InstallSdk
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

$root = Get-RepositoryRoot
$globalJsonPath = Join-Path $root 'global.json'
$requiredSdk = (Get-Content -Raw -LiteralPath $globalJsonPath | ConvertFrom-Json).sdk.version
$hasRequiredSdk = $false

if ($null -ne (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $installedSdks = @(& dotnet --list-sdks)
    if ($LASTEXITCODE -ne 0) {
        throw "'dotnet --list-sdks' failed with exit code $LASTEXITCODE."
    }

    $hasRequiredSdk = $installedSdks -match "^$([regex]::Escape($requiredSdk))\s"
}

if (-not $hasRequiredSdk) {
    if (-not $InstallSdk) {
        throw ".NET SDK $requiredSdk is missing. Install it with 'winget install --id Microsoft.DotNet.SDK.10 --exact', then rerun this script. Bootstrap installs nothing unless -InstallSdk is supplied."
    }

    Assert-CommandAvailable -Name 'winget' -InstallHint 'Install App Installer from Microsoft Store, or install the .NET 10 SDK from https://dotnet.microsoft.com/download/dotnet/10.0.'
    & winget install --id Microsoft.DotNet.SDK.10 --exact --source winget --accept-source-agreements --accept-package-agreements
    if ($LASTEXITCODE -ne 0) {
        throw "winget could not install the .NET 10 SDK (exit code $LASTEXITCODE)."
    }
}

& (Join-Path $PSScriptRoot 'doctor.ps1')
if ($LASTEXITCODE -ne 0) {
    throw "Prerequisite doctor failed with exit code $LASTEXITCODE."
}

Push-Location $root
try {
    Restore-RepositoryTools
    Invoke-DotNet -Arguments @('restore', 'Hukbo.slnx', '--locked-mode')
}
finally {
    Pop-Location
}

Write-Host '[PASS] Locked package restore completed.'
