[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# PowerShell 7 is a hard prerequisite of this script, not merely something it
# reports on. The platform probe below calls
# [System.Runtime.InteropServices.RuntimeInformation], which Windows PowerShell
# 5.1 cannot resolve; under Set-StrictMode that probe throws a
# PropertyNotFoundStrict error naming OSArchitecture. Collecting the version
# into $failures further down would therefore never be reached, and an operator
# on 5.1 would see the engine's error about a missing property instead of the
# actionable message below. The check has to come first, and it has to stop the
# script rather than accumulate a failure.
if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw "PowerShell 7 or newer is required; detected $($PSVersionTable.PSVersion). Install with: winget install --id Microsoft.PowerShell --exact"
}

. (Join-Path $PSScriptRoot '_common.ps1')

$root = Get-RepositoryRoot
$failures = [System.Collections.Generic.List[string]]::new()

Write-Host 'Hukbo prerequisite doctor'
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

# The guard at the top of this script already stopped anything older than 7, so
# reaching this line means the version is supported and only the report remains.
Write-Host "[PASS] PowerShell: $($PSVersionTable.PSVersion)"

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
    'Hukbo.slnx',
    'Directory.Packages.props',
    'NuGet.config'
)
foreach ($relativePath in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $relativePath) -PathType Leaf)) {
        $failures.Add("Required repository file is missing: $relativePath")
    }
}

# Every project in the repository must carry a packages.lock.json sibling.
# The expected set is derived from the *project list* -- every .csproj under
# the repository outside build output -- rather than from the lock files
# already sitting on disk. A list built by scanning for packages.lock.json
# files can only ever confirm files that already exist; it cannot notice a
# project whose lock file was never generated, because there is nothing on
# disk for that scan to find. Scanning for .csproj files first and then
# asserting each one's sibling lock file exists closes that hole: the
# invariant this check protects is "every project that should have a lock
# file has one," not "every lock file that exists is where we expect it."
#
# tools/ projects are included. They are real, independently restorable
# .csproj projects that already carry their own packages.lock.json (eight of
# them, as of this writing), and CLAUDE.md section 3's "tools/ ... not in
# Hukbo.slnx, not in the gate" describes verify.ps1's build/test/benchmark
# gate -- it says nothing about this prerequisite-and-configuration doctor.
# Excluding tools/ here would leave those eight lock files permanently
# unchecked and contradict the "every lock file in the repository" bar this
# script is asked to meet.
#
# This check proves presence, matching the Test-Path-only style every other
# check in this script already uses; it does not re-derive whether a lock
# file's content is stale relative to Directory.Packages.props. Currency in
# that deeper sense is what build.ps1 and bootstrap.ps1 already prove on
# every run, because they restore with --locked-mode and fail loudly the
# moment a lock file no longer matches the packages it locks.
$projectFiles = @(Get-ChildItem -LiteralPath $root -Recurse -Filter '*.csproj' |
    Where-Object {
        $relative = $_.FullName.Substring($root.Length + 1)
        $relative -notmatch '[\\/](bin|obj)[\\/]'
    })

if ($projectFiles.Count -eq 0) {
    $failures.Add('No .csproj files were found under the repository; the lock-file check could not run.')
}

$missingLockFileCount = 0
foreach ($projectFile in $projectFiles) {
    $lockPath = Join-Path $projectFile.DirectoryName 'packages.lock.json'
    if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf)) {
        $relativeProject = $projectFile.FullName.Substring($root.Length + 1)
        $failures.Add("Project is missing its packages.lock.json: $relativeProject")
        $missingLockFileCount++
    }
}

if ($projectFiles.Count -gt 0 -and $missingLockFileCount -eq 0) {
    Write-Host "[PASS] packages.lock.json present for all $($projectFiles.Count) projects."
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
