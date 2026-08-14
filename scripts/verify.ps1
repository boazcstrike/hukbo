[CmdletBinding()]
param(
    [switch] $SkipBootstrap,

    # Which game's test suite and benchmark workload the gate exercises.
    # Defaults to 'Hukbo' so a caller that never passes -Game runs exactly
    # the command sequence this gate has always run, against exactly the
    # same projects, producing the same output -- see design section 14,
    # "The default must be byte-identical, and here is how that is proven."
    # The default gate keeps running the Hukbo workload alone; a second,
    # unconditional benchmark invocation for Sandata is not added here --
    # that is its own task, gated on task 51 recording a baseline.
    [ValidateSet('Hukbo', 'Sandata')]
    [string] $Game = 'Hukbo'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

if (-not $SkipBootstrap) {
    Invoke-RepositoryScript -Name 'bootstrap.ps1'
}

Invoke-RepositoryScript -Name 'format.ps1' -Parameters @{
    Verify = $true
}
Invoke-RepositoryScript -Name 'build.ps1' -Parameters @{
    Configuration = 'Release'
    NoRestore = $true
}
Invoke-RepositoryScript -Name 'test.ps1' -Parameters @{
    Configuration = 'Release'
    NoBuild = $true
    Game = $Game
}
Invoke-RepositoryScript -Name 'benchmark.ps1' -Parameters @{
    Agents = 200
    Ticks = 10000
    Seed = 1
    NoBuild = $true
    Game = $Game
}

# The invocation above never exercises the ranged combat preset: the
# shipped default scenario is PrecolonialPhilippinesV6 (V4's tables with a
# retuned melee cadence, flipped 2026-08-11) and PersistentContingentsV4,
# neither of which fields a ranged weapon, so a completely broken ranged path (every
# projectile refused, every archer stalling to the tick cap) would leave
# this gate green. Guarded to Hukbo only -- PrecolonialPhilippinesV5 and
# RangedStandoffV8 are Hukbo-specific preset ids that do not exist in the
# Sandata game target.
if ($Game -eq 'Hukbo') {
    Invoke-RepositoryScript -Name 'benchmark.ps1' -Parameters @{
        Agents = 200
        Ticks = 10000
        Seed = 1
        NoBuild = $true
        Game = $Game
        Preset = 'PrecolonialPhilippinesV5'
        MovementPreset = 'RangedStandoffV8'
    }

    # Neither invocation above ever exercises the V10 retreat rung: the V8
    # workload holds a threatened shooter in place forever, so a broken
    # BackingAway path -- a shooter that never retreats, one that retreats
    # into a deadlock, or a corrupted three-way standoff ladder -- would
    # still leave this gate green. PrecolonialPhilippinesV5 pairs with
    # BattlefieldRealismV10 here on purpose; see design section 9.2 for why
    # this is a third block rather than a repointed V8 block.
    Invoke-RepositoryScript -Name 'benchmark.ps1' -Parameters @{
        Agents = 200
        Ticks = 10000
        Seed = 1
        NoBuild = $true
        Game = $Game
        Preset = 'PrecolonialPhilippinesV5'
        MovementPreset = 'BattlefieldRealismV10'
    }

    # None of the three invocations above runs the preset the client actually
    # ships. LastStandEngagementV11 became that preset when the last-stand
    # regroup yields landed, and V10 stays registered and covered by the block
    # above rather than being repointed -- the same choice the V10 block itself
    # records for V8. Without this fourth block a broken yield could leave the
    # whole gate green while every shipped battle ran it.
    Invoke-RepositoryScript -Name 'benchmark.ps1' -Parameters @{
        Agents = 200
        Ticks = 10000
        Seed = 1
        NoBuild = $true
        Game = $Game
        Preset = 'PrecolonialPhilippinesV5'
        MovementPreset = 'LastStandEngagementV11'
    }

    # None of the four invocations above runs the preset the client ships
    # after 2026-08-14. CohortLateralSpreadV13 became that preset when the
    # cohort lateral spread landed
    # (the cohort lateral spread design), and V11 stays
    # registered and covered by the block above rather than being repointed --
    # the same choice the V11 block itself records for V10. The V11 block is
    # now the leak detector proving V13's riffled deployment never reached the
    # ascending traversal every earlier preset still uses.
    Invoke-RepositoryScript -Name 'benchmark.ps1' -Parameters @{
        Agents = 200
        Ticks = 10000
        Seed = 1
        NoBuild = $true
        Game = $Game
        Preset = 'PrecolonialPhilippinesV5'
        MovementPreset = 'CohortLateralSpreadV13'
    }
}


# Both games, when the caller named neither. Sandata is not part of the
# Hukbo workload above and never has been: -Game selects one game's test
# suite and benchmark, and until 2026-08-14 a bare ./scripts/verify.ps1
# exercised Hukbo alone, so a completely broken Sandata could leave this
# gate green. That was deliberate while Sandata had no stable baseline,
# because a red Sandata workload must never be mistakable for a red Hukbo
# one. It now has one: stateHash A644B7F8A394885D and eventHash
# AEDE4D16B5E6FAAF held unchanged across four gate runs on 2026-08-14,
# through a pathfinding change, an inspector change, an audio change and a
# combat-rule change.
#
# An explicit -Game still runs exactly one game, byte-identically to
# before, which is what every existing scripted caller and both script
# tests depend on. Only the bare invocation gained a second half.
#
# The two results are two results. This block prints its own banner so
# that nobody reading the output can mistake one game's green for the
# other's, which is the rule CLAUDE.md section 4 states and the reason the
# stages are not interleaved.
if (-not $PSBoundParameters.ContainsKey('Game')) {
    Write-Host ''
    Write-Host '[INFO] Hukbo stages complete. Running the Sandata workload; the two are separate results.'

    Invoke-RepositoryScript -Name 'test.ps1' -Parameters @{
        Configuration = 'Release'
        NoBuild = $true
        Game = 'Sandata'
    }
    Invoke-RepositoryScript -Name 'benchmark.ps1' -Parameters @{
        Agents = 200
        Ticks = 10000
        Seed = 1
        NoBuild = $true
        Game = 'Sandata'
    }
}

Write-Host '[PASS] Canonical repository verification completed.'
