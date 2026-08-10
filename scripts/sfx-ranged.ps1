<#
.SYNOPSIS
    Drives scripts/sfx.ps1 across the ranged-units package's sixty sound
    takes (RU-31), skipping files that already exist and retrying a take
    the quiet guard rejected.

.DESCRIPTION
    RU-31 asks for sixty files, twenty per ranged weapon, spread over the
    thirteen ranged slots PrecolonialPhilippinesV5 added to the catalog.
    Generating them one command at a time works, but the model returns a
    near-silent take often enough that scripts/sfx.ps1's quiet guard throws
    and stops the run, and picking the sequence back up by hand is where
    mistakes get made.

    This script is a driver and nothing more. It calls scripts/sfx.ps1 once
    per take and owns no generation logic of its own: no HTTP call, no
    prompt table, no audio handling. Change a prompt in sfx.ps1 and this
    script picks it up, because it never passes one.

    Three rules it will not break.

    It never passes -AllowQuiet. A take under ten per cent of full scale is
    inaudible under a 200-agent battle mix, and RU-20 measured the audio
    headroom assuming real levels. A slot that will not produce a loud take
    is a prompt to fix, not a threshold to lower.

    It never passes -Force, so a file that already exists is left alone and
    reported as skipped. Re-running after an interruption costs nothing for
    the takes already on disk.

    It sends nothing without -Execute. The default run resolves every take,
    prints exactly what it would request, and stops. Read that list before
    spending anything.

    Verification is unchanged and this script does not touch it: no
    automated test can confirm a sound was heard, so RU-31 is finished only
    when a person has listened to at least one take from each of the
    thirteen slots and flipped the smoke-checklist row themselves.

.PARAMETER Execute
    Actually calls the ElevenLabs API through scripts/sfx.ps1. Without it
    the run is a dry run: every take is resolved and printed, nothing is
    requested, and nothing is written.

.PARAMETER MaxAttempts
    How many times one take may be requested before the script gives up on
    it and moves to the next. Only a quiet-guard rejection is retried; any
    other failure stops the whole run, because an authentication or quota
    error repeated sixty times is sixty pointless requests.

.PARAMETER Slot
    Restricts the run to slots whose name matches this wildcard pattern,
    for example 'attack-*' or 'release-busog'. Omit it for all thirteen.

.PARAMETER OutputDirectory
    Passed straight through to scripts/sfx.ps1. Defaults to that script's
    own default, src/Hukbo.Client/Content/Audio.

.EXAMPLE
    ./scripts/sfx-ranged.ps1

.EXAMPLE
    ./scripts/sfx-ranged.ps1 -Slot 'release-*' -Execute

.EXAMPLE
    ./scripts/sfx-ranged.ps1 -Execute -MaxAttempts 6
#>
[CmdletBinding()]
param(
    [switch] $Execute,

    [ValidateRange(1, 12)]
    [int] $MaxAttempts = 4,

    [string] $Slot,

    [string] $OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

# RU-31's take counts, and how the attack takes are spread across the six
# hit classes. The totals are the plan's: twenty per weapon, sixty in all.
#
# The plan fixes the per-slot totals and requires ribcage to exist for
# every weapon, because it is the universal fallback target and a
# hit-location driven slot without it can resolve Missing. It does not fix
# how the remaining attack takes divide between the other five classes, so
# that division is made here and is meant to be edited.
#
# It follows the precedent the four melee weapons already set on disk:
# takes cluster on the classes that weapon actually hits. Each ranged
# weapon's TargetWeightProfiles overrides in
# src/Hukbo.Core/Combat/PhilippineCombatPresetV5.cs are the source --
# Bangkaw favours Chest 10, Abdomen 9, Shoulder 7; Busog Chest 10,
# Abdomen 8, Head 7; Arquebus Chest 10, Abdomen 9, Head 8. A class with no
# take here is not a gap: the mapper falls back to ribcage.
$takeMatrix = @(
    @{ Slot = 'release-bangkaw'; Class = ''; Count = 5 }
    @{ Slot = 'release-busog'; Class = ''; Count = 6 }
    @{ Slot = 'release-arquebus'; Class = ''; Count = 7 }

    @{ Slot = 'attack-bangkaw'; Class = 'ribcage'; Count = 3 }
    @{ Slot = 'attack-bangkaw'; Class = 'gut'; Count = 2 }
    @{ Slot = 'attack-bangkaw'; Class = 'limb'; Count = 2 }
    @{ Slot = 'attack-bangkaw'; Class = 'skull'; Count = 1 }
    @{ Slot = 'attack-bangkaw'; Class = 'neck'; Count = 1 }

    @{ Slot = 'attack-busog'; Class = 'ribcage'; Count = 3 }
    @{ Slot = 'attack-busog'; Class = 'gut'; Count = 2 }
    @{ Slot = 'attack-busog'; Class = 'skull'; Count = 2 }
    @{ Slot = 'attack-busog'; Class = 'neck'; Count = 1 }

    @{ Slot = 'attack-arquebus'; Class = 'ribcage'; Count = 2 }
    @{ Slot = 'attack-arquebus'; Class = 'gut'; Count = 2 }
    @{ Slot = 'attack-arquebus'; Class = 'skull'; Count = 1 }
    @{ Slot = 'attack-arquebus'; Class = 'neck'; Count = 1 }

    # sfx.ps1's own comment above the clash prompts asks for a prompt
    # influence of 0.5 rather than the 0.4 default, because what the slot
    # is defined by is its negatives -- dry wood, no metal, no ring -- and
    # a looser influence lets the model add them back.
    @{ Slot = 'clash-shield-bangkaw'; Class = ''; Count = 3; PromptInfluence = 0.5 }
    @{ Slot = 'clash-shield-busog'; Class = ''; Count = 3; PromptInfluence = 0.5 }
    @{ Slot = 'clash-shield-arquebus'; Class = ''; Count = 3; PromptInfluence = 0.5 }

    @{ Slot = 'miss-bangkaw'; Class = ''; Count = 3 }
    @{ Slot = 'miss-busog'; Class = ''; Count = 3 }
    @{ Slot = 'miss-arquebus'; Class = ''; Count = 2 }

    @{ Slot = 'misfire-arquebus'; Class = ''; Count = 2 }
)

$root = Get-RepositoryRoot
$sfxScript = Join-Path $PSScriptRoot 'sfx.ps1'
$audioDirectory = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    Join-Path $root 'src/Hukbo.Client/Content/Audio'
}
else {
    $OutputDirectory
}

function Get-TakeFileName {
    <#
        Reproduces the "<slot>[-<class>][-NN].wav" shape sfx.ps1's own
        Resolve-OutputPath builds, so the skip check tests the same path the
        generation would write.
    #>
    param(
        [Parameter(Mandatory)] [string] $SlotName,
        [Parameter(Mandatory)] [AllowEmptyString()] [string] $ClassName,
        [Parameter(Mandatory)] [int] $VariantIndex
    )

    $name = $SlotName
    if (-not [string]::IsNullOrWhiteSpace($ClassName)) {
        $name += "-$ClassName"
    }

    return ('{0}-{1:D2}.wav' -f $name, $VariantIndex)
}

$plannedTotal = ($takeMatrix | Measure-Object -Property Count -Sum).Sum
Write-Host "Ranged sound takes (RU-31): $plannedTotal declared across $($takeMatrix.Count) rows."
Write-Host "Output directory: $audioDirectory"
if (-not $Execute) {
    Write-Host '-Execute was not set. Nothing will be requested and nothing will be written.'
}

Write-Host ''

$written = 0
$skipped = 0
$abandoned = [System.Collections.Generic.List[string]]::new()
$retried = 0

foreach ($row in $takeMatrix) {
    if (-not [string]::IsNullOrWhiteSpace($Slot) -and $row.Slot -notlike $Slot) {
        continue
    }

    $className = if ($row.ContainsKey('Class')) { [string]$row.Class } else { '' }

    foreach ($index in 1..$row.Count) {
        $fileName = Get-TakeFileName -SlotName $row.Slot -ClassName $className -VariantIndex $index
        $filePath = Join-Path $audioDirectory $fileName

        if (Test-Path -LiteralPath $filePath -PathType Leaf) {
            Write-Host "[SKIP]  $fileName already exists."
            $skipped++
            continue
        }

        $arguments = @{
            Slot  = $row.Slot
            Index = $index
        }

        if (-not [string]::IsNullOrWhiteSpace($className)) {
            $arguments['Class'] = $className
        }

        if ($row.ContainsKey('PromptInfluence')) {
            $arguments['PromptInfluence'] = [double]$row.PromptInfluence
        }

        if (-not [string]::IsNullOrWhiteSpace($OutputDirectory)) {
            $arguments['OutputDirectory'] = $OutputDirectory
        }

        if (-not $Execute) {
            $influenceNote = if ($row.ContainsKey('PromptInfluence')) {
                " (prompt influence $($row.PromptInfluence))"
            }
            else {
                ''
            }

            Write-Host "[PLAN]  $fileName$influenceNote"
            continue
        }

        $succeeded = $false
        foreach ($attempt in 1..$MaxAttempts) {
            try {
                & $sfxScript @arguments
                $succeeded = $true
                $written++
                break
            }
            catch {
                $message = $_.Exception.Message

                # Only the quiet guard is worth another request. Anything
                # else -- a missing key, a rejected slot name, an exhausted
                # quota -- will fail identically every time, so retrying it
                # sixty times spends money to learn nothing.
                if ($message -notmatch 'peaks at only') {
                    throw
                }

                $retried++
                Write-Host "[RETRY] $fileName attempt $attempt of ${MaxAttempts}: the take was too quiet." -ForegroundColor Yellow
            }
        }

        if (-not $succeeded) {
            Write-Host "[GIVE UP] $fileName stayed quiet across $MaxAttempts attempts." -ForegroundColor Red
            $abandoned.Add($fileName)
        }
    }
}

Write-Host ''
if ($Execute) {
    Write-Host "Written: $written   Skipped: $skipped   Quiet retries: $retried   Abandoned: $($abandoned.Count)"
}
else {
    Write-Host "Skipped (already on disk): $skipped. Re-run with -Execute to request the rest."
}

if ($abandoned.Count -gt 0) {
    Write-Host ''
    Write-Host 'These takes never produced an audible result:'
    foreach ($name in $abandoned) {
        Write-Host "  $name"
    }

    Write-Host ''
    Write-Host 'A slot that stays quiet across every attempt is a prompt problem, not'
    Write-Host 'a threshold problem. Edit its entry in the $defaultPrompts table in'
    Write-Host 'scripts/sfx.ps1 -- words like "thin", "soft", "grazing" and "shallow"'
    Write-Host 'read to the model as an instruction to be quiet -- and run again.'
    exit 1
}

if ($Execute) {
    Write-Host ''
    Write-Host 'Nothing here has been heard yet. Run ./scripts/sfx.ps1 -List to confirm'
    Write-Host 'all twenty-six slots report present, then launch the game and listen.'
    Write-Host 'Only a person may flip the RG rows in docs/development/smoke-checklist.md.'
}
