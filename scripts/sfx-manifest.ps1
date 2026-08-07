<#
.SYNOPSIS
    Builds the Sandata audio generation manifest and prints its cost estimate.
    Calls no network service. Generates no audio file. Stops after printing.

.DESCRIPTION
    Enumerates the slot matrix declared by
    src/Sandata.Client/Audio/SandataSoundCatalog.cs and writes a manifest
    listing every declared row's file name, prompt, duration, and trim
    threshold, then prints the credit and dollar estimate for a full
    generation run, then stops.

    The catalog type is internal, and its row-building loops (over
    CaliberFamily, MechanismGroup, ImpactSurface, and so on) are not the kind
    of switch-per-slot shape scripts/sfx.ps1 can lift out with a regular
    expression the way it does for Hukbo's melee SoundCatalog.cs. This script
    instead runs the one xunit fact that materialises the manifest —
    tests/Sandata.Client.Tests/SoundManifestTests.WriteManifestArtifact — via
    `dotnet test --filter`, then reads the CSV that fact wrote. Building and
    running a test project touches no network endpoint beyond whatever the
    repository's own locked NuGet restore already requires, and neither does
    anything below this comment: this file contains no HTTP call of any kind
    and no reference to the third-party sound-generation vendor scripts/sfx.ps1
    talks to.

    Plan task 40's own row is titled a "484-slot matrix", and so is design
    section 10. Both predate task 24's implementation of the catalog. Running
    this script is how the real numbers were found: SandataSoundCatalog
    declares 106 rows, and those 106 rows expand to 524 individual variant
    files. This script reports both numbers and flags the documents as wrong
    rather than silently going along with either the plan's or the design's
    stale figure.

.PARAMETER Configuration
    Build configuration used to run the manifest-writing test. Defaults to
    'Debug' for a faster round trip; this script never touches the canonical
    gate, so it does not need to match the gate's 'Release' configuration.

.PARAMETER NoBuild
    Skips the restore and build step and runs the test against whatever was
    built last. Fails if the test assembly does not already exist.

.PARAMETER RejectRate
    The fraction of takes assumed to be rejected for quality (silence, a
    musical tail, a stray voice) when estimating the "realistic" credit cost.
    Defaults to 0.5, matching the task's own "50-percent-reject rate" framing
    and the real quality variance scripts/sfx.ps1's own documentation records
    (one take at 93 percent of full scale, another at under 1 percent, from
    the same prompt). 0.0 gives the best-case, zero-reject estimate.

.EXAMPLE
    ./scripts/sfx-manifest.ps1

.EXAMPLE
    ./scripts/sfx-manifest.ps1 -Configuration Release -RejectRate 0.3
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [switch] $NoBuild,

    [ValidateRange(0.0, 0.95)]
    [double] $RejectRate = 0.5
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

# The sound-generation vendor's published price per generation call (design
# section 10). A plain number, not a request: nothing on this page opens a
# socket.
$creditsPerGeneration = 200

# The Creator tier's monthly credit allowance, and its price, as design
# section 10 records them. Used only to say whether an estimate fits under it.
$creatorTierCredits = 121000
$creatorTierUsd = 22
$proTierUsd = 99

$root = Get-RepositoryRoot
$testProject = Join-Path $root 'tests/Sandata.Client.Tests/Sandata.Client.Tests.csproj'
$manifestPath = Join-Path $root 'artifacts/audio/sandata-sound-manifest.csv'

Push-Location $root
try {
    if (-not $NoBuild) {
        Invoke-DotNet -Arguments @('restore', $testProject, '--locked-mode')
        Invoke-DotNet -Arguments @(
            'build',
            $testProject,
            '--configuration', $Configuration,
            '--no-restore'
        )
    }

    # Running this one fact is what actually enumerates the catalog: the
    # fact calls SoundManifestGenerator.BuildRows() against the real,
    # compiled SandataSoundCatalog.Rows and writes the CSV this script reads
    # next. Nothing here calls dotnet run against a network-facing exe —
    # dotnet test executes an xunit fact in-process.
    Invoke-DotNet -Arguments @(
        'test',
        $testProject,
        '--configuration', $Configuration,
        '--no-build',
        '--no-restore',
        '--filter', 'FullyQualifiedName~SoundManifestTests.WriteManifestArtifact',
        '--logger', 'console;verbosity=normal'
    )
}
finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "The manifest test ran but $manifestPath does not exist. WriteManifestArtifact may have changed its output path without this script being updated to match."
}

$rows = @(Import-Csv -LiteralPath $manifestPath)
if ($rows.Count -eq 0) {
    throw "$manifestPath was written but contains no data rows."
}

$totalVariantFiles = 0
foreach ($row in $rows) {
    $totalVariantFiles += [int] $row.VariantCount
}

function Get-CreditEstimate {
    param(
        [Parameter(Mandatory)] [int] $VariantFiles,
        [Parameter(Mandatory)] [double] $Reject
    )

    # A take that fails quality review (silence, a musical tail, a stray
    # voice) must be regenerated, so at a reject rate r the expected number
    # of attempts per kept file is 1 / (1 - r).
    $attemptsPerKeptFile = 1.0 / (1.0 - $Reject)
    $attempts = [System.Math]::Ceiling($VariantFiles * $attemptsPerKeptFile)
    return [int] ($attempts * $creditsPerGeneration)
}

$zeroRejectCredits = Get-CreditEstimate -VariantFiles $totalVariantFiles -Reject 0.0
$realisticCredits = Get-CreditEstimate -VariantFiles $totalVariantFiles -Reject $RejectRate

Write-Host ''
Write-Host 'Sandata audio manifest'
Write-Host "Catalog:  src/Sandata.Client/Audio/SandataSoundCatalog.cs"
Write-Host "Manifest: $manifestPath"
Write-Host ''
Write-Host "Declared rows (one per slot):        $($rows.Count)"
Write-Host "Individual variant files if run:     $totalVariantFiles"
Write-Host ''
Write-Host '[NOTE] Plan task 40 and design section 10 both call this a' -ForegroundColor Yellow
Write-Host "       '484-slot matrix'. Both predate task 24's implementation" -ForegroundColor Yellow
Write-Host "       of SandataSoundCatalog and are wrong: the catalog as built" -ForegroundColor Yellow
Write-Host "       declares $($rows.Count) rows expanding to $totalVariantFiles variant files, not 484." -ForegroundColor Yellow
Write-Host ''
Write-Host 'Cost estimate'
Write-Host ("  Zero-reject ({0} files, one generation each):" -f $totalVariantFiles)
Write-Host ("    {0} credits" -f $zeroRejectCredits)
if ($zeroRejectCredits -le $creatorTierCredits) {
    Write-Host ("    Fits under the Creator tier's {0} credits/month ({1} USD)." -f $creatorTierCredits, $creatorTierUsd)
}
else {
    Write-Host ("    Exceeds the Creator tier's {0} credits/month; the Pro tier ({1} USD) is required." -f $creatorTierCredits, $proTierUsd)
}

Write-Host ("  Realistic, {0}% reject rate:" -f ([int] ($RejectRate * 100)))
Write-Host ("    {0} credits" -f $realisticCredits)
if ($realisticCredits -le $creatorTierCredits) {
    Write-Host ("    Fits under the Creator tier's {0} credits/month ({1} USD)." -f $creatorTierCredits, $creatorTierUsd)
}
else {
    Write-Host ("    Exceeds the Creator tier's {0} credits/month; the Pro tier ({1} USD) is required." -f $creatorTierCredits, $proTierUsd)
}

Write-Host ''
Write-Host '[UNVERIFIED] Whether the vendor''s credits scale with requested' -ForegroundColor Yellow
Write-Host '             generation duration could not be confirmed (design' -ForegroundColor Yellow
Write-Host '             section 10). The estimate above assumes a flat' -ForegroundColor Yellow
Write-Host '             200 credits per call regardless of duration. If' -ForegroundColor Yellow
Write-Host '             credits do scale with duration, every row above' -ForegroundColor Yellow
Write-Host '             GunReport''s 0.5s (GunLoop 3.0s, GunTail 1.5s) would' -ForegroundColor Yellow
Write-Host '             cost more than this estimate states. Do not treat' -ForegroundColor Yellow
Write-Host '             this estimate as favourable until that is confirmed.' -ForegroundColor Yellow
Write-Host ''
Write-Host '[STOP] This script calls no network service and has generated no' -ForegroundColor Cyan
Write-Host '       audio file. Nothing runs until a person reviews the' -ForegroundColor Cyan
Write-Host "       manifest at $manifestPath and authorises the spend above." -ForegroundColor Cyan
