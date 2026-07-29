# Rank in the army composition panel — plan

> **Archived: reference only.** Completed on 2026-07-29. Tasks P1 through P5
> all landed and the canonical gate passed. Do not execute this plan; the
> default combat preset it flips has already moved.

Date: 2026-07-29
Design: [`2026-07-29-warrior-standing-design.md`](../../plans/2026-07-29-warrior-standing-design.md) §2
Unblocks: task C3 in [`2026-07-29-warrior-rank.md`](2026-07-29-warrior-rank.md)

The warrior rank design names three independent ways a spectator can discover
rank without reading source code: the inspector line, the composition panel
categories, and the observable fact that a contingent forms around its chief.
Two of the three shipped on 2026-07-29. This plan delivers the third, which is
the only one a spectator reads *before* the battle starts.

## Why this was blocked, and what changed

C3 was recorded as blocked because retargeting the panel cascades past the one
file it was scoped to. That cascade is real and is still real. What has changed
is that both of its hard parts turn out to have settled answers already in the
repository.

**The persisted settings shape.** `ClientSettings.ArmyComposition` is a record
with six named fields, one per combat preset V2 roster entry, and
`ArmyComposition.CategoryCount` is pinned at six by a test. Changing the roster
shape changes saved data. The repository already has a convention for this:
`ClientSettingsStore.SupportedSchemaVersion` is 6, `AcceptedSchemaVersions`
contains only that one value, and a settings file at any other version is
discarded rather than migrated. The record's own remarks record that this was
done once before and why it was acceptable — a handful of settings re-entered
in seconds, and no shipped installs. That reasoning still holds.

**The default combat preset.** The panel shows the active preset's roster, and
`Scenario.CombatPreset` defaults to `PrecolonialPhilippinesV2`. Rank would
therefore be invisible in the panel no matter how the labels were written,
because the running preset's roster carries no meaningful ranks — every V2
loadout resolves to the single default value. Making rank visible requires the
shipped default to become `PrecolonialPhilippinesV4`.

The warrior rank plan deferred that flip deliberately, with the words "that is
a separate decision, after V4 has been through the gate". V4 has now been
through the gate. The decision is taken here.

## What the default flip costs

The shipped seed-1 state hash and event hash move, because the default battle
is now fought under a different roster with different weapons and different
per-rank levels. Every golden that pins the *default* preset moves with it and
must be recomputed from the built code.

What does **not** move, and what the tests must keep proving:

- V1, V2, and V3 remain registered and unmodified. Their content hashes stay
  byte-identical, and any replay that names its preset explicitly still
  reproduces exactly.
- Movement presets are untouched by this plan. `PersistentContingentsV5` stays
  registered and not defaulted.

This is the same manoeuvre the movement preset default already went through
twice, from V2 to V3 and from V3 to V4, and `Scenario`'s own documentation
narrates both flips. This one is narrated the same way.

## Tasks

### P1. Flip the shipped default combat preset to V4

**Files:** `src/Hukbo.Core/Simulation/Scenario.cs`

`CombatPreset` defaults to `CombatPresetId.PrecolonialPhilippinesV4`. Extend the
existing XML documentation to narrate this flip in the same style the movement
preset default already uses, naming what moved and what stayed frozen.

**Done when:** a default scenario resolves V4 and V1 through V3 still resolve
unchanged.

**Verified by:** `ScenarioTests`, plus recomputed seed-1 goldens in
`DeterminismTests`. Every V1, V2, and V3 content-hash freeze assertion must
pass untouched. If one moves, stop — the flip is not supposed to reach them.

### P2. Reshape the persisted composition to four rank categories

**Files:** `src/Hukbo.Client/Settings/ClientSettings.cs`,
`src/Hukbo.Client/Settings/ClientSettingsStore.cs`

`ArmyComposition` becomes `UnitsPerTeam` plus four counts named for the ranks
V4 fields: Datu, Maharlika, Timawa, Aliping Namamahay. `CategoryCount` becomes
4.

Bump `SupportedSchemaVersion` to 7. Do not write a migration — the existing
convention discards an unrecognised file and rebuilds defaults, and the record's
remarks already explain why that is acceptable here. Extend those remarks to
record this third reset and its reason.

`DefaultUnitsPerTeam` stays 250. With four categories, 250 divides as 62 with a
remainder of 2, so the first two categories carry 63 and the rest 62. This must
agree with `ArmyCompositionStepper.DistributeEvenly`'s remainder-to-lowest-index
rule, exactly as the six-category arithmetic did.

**Done when:** a stale settings file is discarded and rebuilt at version 7.

**Verified by:** `ClientSettingsStore` tests, including one asserting a version
6 file is rejected rather than misread.

### P3. Rank-labelled categories

**Files:** `src/Hukbo.Client/UI/ArmyCompositionPanel.cs`

`CategoryLabels` becomes four entries, in declared roster-index order, each
naming the rank and the weapon that rank carries under V4.

Both identifications are cultural, so **both appear in pair form** — the
Filipino name, an em dash, and a plain English descriptor. A bare `Kampilan`
or a bare `Datu` violates the historical accuracy policy in `CLAUDE.md` §7.

Preferred form, if it fits the measured row width:

```
Datu — Chief · Kampilan — Great Blade
Maharlika — Sworn Freeman · Wasay — War Axe
Timawa — Bound Freeman · Kalis — Thrusting Blade
Aliping Namamahay — Householder · Itak — Work Blade
```

If the measured width will not carry both, fall back to the rank pair form
alone and report that the weapon was dropped. Do not truncate a pair form to
make it fit, and do not drop the descriptor half of either pair.

Update the remarks above `CategoryLabels`, which currently explain a six-entry
solo-and-shielded split that no longer applies.

**Done when:** the panel lists four rank-labelled categories.

**Verified by:** `ArmyCompositionPanelTests`, repinned from V2 to V4, with an
assertion that the label count equals the active preset's roster length and
that no label contains a bare cultural name.

### P4. Map four categories into the scenario roster

**Files:** `src/Hukbo.Client/ArenaGame.cs`

`ArenaGame` currently hardcodes six indices when converting a composition into
`Scenario.RosterCounts` and when rebuilding an `ArmyComposition` from one.
Reduce both to four, or derive the count from `ArmyComposition.CategoryCount`
so the next roster change does not need this edit again.

**Done when:** a composition round-trips through `Scenario.RosterCounts`
without losing a category.

**Verified by:** existing `ArenaGame` and `MenuOverlay` composition tests.

### P5. Gate

Run `./scripts/verify.ps1` once, after integration. Record the moved seed-1
goldens and the 500-agent result.

## What this plan does not do

- **No change to any registered preset.** V1 through V4 keep their data and
  their content hashes; only which one a default scenario selects changes.
- **No movement preset change.** `PersistentContingentsV5` stays registered and
  not defaulted.
- **No settings migration path.** Discard and rebuild, per the existing
  convention.
- **No change to the rank ladder, the level table, or the weapon assignment.**
  Those shipped and are frozen.
