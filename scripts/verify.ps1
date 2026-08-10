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
}

Write-Host '[PASS] Canonical repository verification completed.'
