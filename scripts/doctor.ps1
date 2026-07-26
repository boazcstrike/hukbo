[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

$root = Get-RepositoryRoot
$failures = [System.Collections.Generic.List[string]]::new()

Write-Host 'Autonomous Arena prerequisite doctor'
Write-Host "Repository: $root"

$isSupportedWindows = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
    [System.Runtime.InteropServices.OSPlatform]::Windows)
$architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
if ($isSupportedWindows -and $architecture -eq [System.Runtime.InteropServices.Architecture]::X64) {
    Write-Host '[PASS] Platform: Windows x64'
}
else {
    $failures.Add("v0.1 requires Windows x64; detected $([System.Runtime.InteropServices.RuntimeInformation]::OSDescription) $architecture.")
}

$powerShellVersion = $PSVersionTable.PSVersion
if ($powerShellVersion.Major -ge 7) {
    Write-Host "[PASS] PowerShell: $powerShellVersion"
}
else {
    $failures.Add("PowerShell 7 or newer is required; detected $powerShellVersion. Install with: winget install --id Microsoft.PowerShell --exact")
}

$git = Get-Command git -ErrorAction SilentlyContinue
if ($null -eq $git) {
    $failures.Add('Git is required. Install with: winget install --id Git.Git --exact')
}
else {
    $gitVersion = (& git --version)
    if ($LASTEXITCODE -ne 0) {
        $failures.Add("Git was found but 'git --version' failed with exit code $LASTEXITCODE.")
    }
    else {
        Write-Host "[PASS] $gitVersion"
    }

    & git lfs version *> $null
    if ($LASTEXITCODE -eq 0) {
        Write-Host '[PASS] Git LFS: installed (optional; no tracked LFS assets are currently required)'
    }
    else {
        Write-Host '[INFO] Git LFS: not installed (optional for the current text-only/runtime-generated assets)'
    }
}

$globalJsonPath = Join-Path $root 'global.json'
if (-not (Test-Path -LiteralPath $globalJsonPath -PathType Leaf)) {
    $failures.Add('global.json is missing; the required SDK cannot be determined.')
}
else {
    $sdkVersion = (Get-Content -Raw -LiteralPath $globalJsonPath | ConvertFrom-Json).sdk.version
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnet) {
        $failures.Add("The .NET SDK $sdkVersion is required. Install with: winget install --id Microsoft.DotNet.SDK.10 --exact")
    }
    else {
        $installedSdks = @(& dotnet --list-sdks)
        if ($LASTEXITCODE -ne 0) {
            $failures.Add("'dotnet --list-sdks' failed with exit code $LASTEXITCODE.")
        }
        elseif ($installedSdks -match "^$([regex]::Escape($sdkVersion))\s") {
            Write-Host "[PASS] .NET SDK: $sdkVersion"
        }
        else {
            $failures.Add(".NET SDK $sdkVersion is not installed. Install with: winget install --id Microsoft.DotNet.SDK.10 --exact")
        }
    }
}

$requiredFiles = @(
    'AutonomousArena.slnx',
    'Directory.Packages.props',
    'NuGet.config',
    'src/AutonomousArena.Client/packages.lock.json',
    'src/AutonomousArena.Core/packages.lock.json',
    'src/AutonomousArena.Headless/packages.lock.json',
    'tests/AutonomousArena.Core.Tests/packages.lock.json'
)
foreach ($relativePath in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $relativePath) -PathType Leaf)) {
        $failures.Add("Required repository file is missing: $relativePath")
    }
}

$packagePropsPath = Join-Path $root 'Directory.Packages.props'
if (Test-Path -LiteralPath $packagePropsPath -PathType Leaf) {
    [xml] $packageProps = Get-Content -Raw -LiteralPath $packagePropsPath
    $monoGamePackages = @($packageProps.Project.ItemGroup.PackageVersion |
        Where-Object { $_.Include -like 'MonoGame.*' })
    if ($monoGamePackages.Count -ge 2) {
        $versions = ($monoGamePackages | ForEach-Object { "$($_.Include) $($_.Version)" }) -join ', '
        Write-Host "[PASS] MonoGame packages are centrally pinned: $versions"
    }
    else {
        $failures.Add('Expected centrally pinned MonoGame framework and content-builder packages were not found.')
    }
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Host "[FAIL] $failure" -ForegroundColor Red
    }

    throw "Prerequisite doctor found $($failures.Count) blocking issue(s)."
}

Write-Host '[PASS] Required prerequisites and repository configuration are present.'
Write-Host 'Next: ./scripts/bootstrap.ps1'
