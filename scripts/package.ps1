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
$packageRoot = Join-Path $root 'artifacts/packages'
$outputLeaf = "client-$Runtime"
$output = Join-Path $packageRoot $outputLeaf
$stagingLeaf = ".$outputLeaf-staging-$([guid]::NewGuid().ToString('N'))"
$staging = Join-Path $packageRoot $stagingLeaf

New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null

function Assert-ExpectedPackagePath {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $ExpectedLeaf
    )

    $resolvedPackageRoot = [System.IO.Path]::GetFullPath($packageRoot)
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $expectedPath = [System.IO.Path]::GetFullPath(
        (Join-Path $resolvedPackageRoot $ExpectedLeaf))
    if (-not [string]::Equals(
        $resolvedPath,
        $expectedPath,
        [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify unexpected package path '$resolvedPath'."
    }
}

Assert-ExpectedPackagePath -Path $output -ExpectedLeaf $outputLeaf
Assert-ExpectedPackagePath -Path $staging -ExpectedLeaf $stagingLeaf

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
        '--output', $staging,
        '-p:PublishSingleFile=false'
    )

    if (Test-Path -LiteralPath $output) {
        Remove-Item -LiteralPath $output -Recurse -Force
    }

    Move-Item -LiteralPath $staging -Destination $output
}
finally {
    Pop-Location
    if (Test-Path -LiteralPath $staging) {
        Assert-ExpectedPackagePath -Path $staging -ExpectedLeaf $stagingLeaf
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
}

Write-Host "[PASS] Windows package published to $output"
