[CmdletBinding()]
param(
    [switch] $Verify
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

$root = Get-RepositoryRoot
Push-Location $root
try {
    Invoke-DotNet -Arguments @('restore', 'Hukbo.slnx', '--locked-mode')
    $arguments = @('format', 'Hukbo.slnx', '--no-restore')
    if ($Verify) {
        $arguments += @('--verify-no-changes', '--verbosity', 'diagnostic')
    }

    Invoke-DotNet -Arguments $arguments
}
finally {
    Pop-Location
}

$mode = if ($Verify) { 'verification' } else { 'application' }
Write-Host "[PASS] Formatting $mode completed."
