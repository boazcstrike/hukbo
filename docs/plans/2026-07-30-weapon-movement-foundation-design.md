# Weapon-relative movement — shared foundation design

**Date:** 2026-07-30
**Status:** Design. This document does not authorize implementation. The ordered
task list lives in
[`2026-07-30-weapon-movement-foundation.md`](2026-07-30-weapon-movement-foundation.md).
**Branch:** `movement-foundation`, based on `main` at `caf0d63`.

## Authority chain

This design supersedes [`docs/archives/2026-07-31/movement/README.md`](../archives/2026-07-31/movement/README.md)
wherever the two disagree. That earlier shared plan was written on 2026-07-29
and the repository has moved since: the movement preset number it wanted is
taken, the combat default it wanted to change has already changed twice, and
`CombatLoadout` has grown a field that breaks its profile-key proposal. Every
correction is listed in section 19.3 rather than silently applied.

The five weapon documents under [`docs/archives/2026-07-31/movement/`](../archives/2026-07-31/movement/) and
[`docs/research/movement/`](../research/movement/) remain the source for their
own rows and their own acceptance tests. Where a weapon document disagrees with
this design on a shared convention, this design wins, and section 19.3 records
which weapon session has to restate an acceptance row.

Where this design disagrees with the code on disk, the code wins. Every claim
below about existing code was verified against the worktree at `caf0d63` before
it was written down.

---

## 1. Goal

Add a deterministic, opt-in, equipment-relative movement layer for Hukbo's six
implemented loadouts, without changing any existing movement preset, without
changing any combat rule, and without becoming the shipped default.

The new preset resolves an immutable movement profile per equipment loadout,
derives bounded local count and composition context in scratch storage, writes
five new pieces of authoritative agent state — an integer facing, a retained
scalar pace, a tactical posture, a footwork phase, and a phase timer — and
applies a pace-and-route abstraction immediately before the existing collision
stage.

A spectator who never reads the source has to be able to discover the effect.
That is why section 15 exists and why the agent inspector gains four rows: an
effect a spectator cannot observe is an incomplete feature under
`SIMULATION-GAME-STANDARDS.md` section 10.

## 2. Non-goals

These are not merely out of scope for the first slice. They are forbidden in
this workstream, and a task list that reintroduces one is wrong.

- No edits to damage, reach, cooldown, combos, clash resolution, hit location,
  or shield interception. The combat contract stays byte-for-byte.
- No directional attack, directional defense, shield arc, parry, interception,
  or friendly-fire damage. Facing is a movement and presentation concept only.
- No velocity vector, acceleration engine, momentum, force, rigid-body physics,
  terrain, or pathfinding. The retained pace is a one-dimensional scalar memory
  and nothing else.
- No morale, panic, rout, surrender, or campaign state.
- No rigid formation slots, shield wall, or mixed-contingent rewrite.
- No movement-speed bonus derived from ally count, enemy count, or global
  advantage. Counts change routes and phases; they never change physical speed.
- No `System.Random`, no floating-point authoritative math, no wall clock, no
  filesystem access, no `Hukbo.Diagnostics` dependency from `Hukbo.Core`, no
  unbounded cache, and no derived query data inside a snapshot.
- No movement spatial grid in this slice. The two all-agent scans described in
  sections 7 and 10 are the design. A bounded spatial query is a separate,
  later, measured decision and only if the performance budget in section 18
  actually fails.
- **No default activation.** `Scenario.MovementPreset` stays
  `PersistentContingentsV4` throughout. Changing it is a separate task with
  new golden expectations, gated on section 19.
- No hosted CI workflow of any kind.

## 3. Opt-in contract

The new preset is `EquipmentRelativeFootworkV6 = 6`, appended to
`src/Hukbo.Core/Movement/MovementPresetId.cs`. The value `5` is already taken by
`PersistentContingentsV5`, the rank-aware leader scan, declared at line 98 of
that file. No existing value is renumbered and no existing member is reordered,
because both reach the state hash through `(int)scenario.MovementPreset`.

Activation is the existing `--movement-preset` argument on the headless runner
and the `-MovementPreset` parameter on `scripts/benchmark.ps1`. Both already
accept a member name or a numeric value and both already reject an unregistered
value with exit code `2`. No new activation surface is needed and none is added.

`MovementRuleset` gains a boolean `UsesEquipmentRelativeFootwork`. Presets V1
through V5 register `false`, an empty profile collection, and zero context
radii. V6 registers `true`, exactly six profiles, and both context radii. Every
new code path in `BattleSimulation` is guarded by that flag, so a V1-through-V5
run executes the same instructions it executes today.

Every scenario, test, fixture, and benchmark written in this workstream names
its combat preset explicitly. None relies on `Scenario`'s default. This matters
because only `PrecolonialPhilippinesV2` fields all six loadouts:

| Combat preset | KP | WA | KA | IT | KS | IS | Rank assignment |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `PrecolonialPhilippinesV1` | yes | yes | no | no | yes | yes | all default `Timawa` |
| `PrecolonialPhilippinesV2` | yes | yes | yes | yes | yes | yes | all default `Timawa` |
| `PrecolonialPhilippinesV3` | yes | yes | yes | yes | no | no | all default `Timawa` |
| `PrecolonialPhilippinesV4` (shipped default) | yes | yes | yes | yes | no | no | `Datu`, `Maharlika`, `Timawa`, `AlipingNamamahay` |

Canonical loadout abbreviations, used throughout this document and binding on
every table, array index, and fold order: `KP` Kampilan solo, `WA` Wasay solo,
`KA` Kalis solo, `IT` Itak solo, `KS` Kalis plus TallHardwood, `IS` Itak plus
TallHardwood. That order — `KP, WA, KA, IT, KS, IS` — is the canonical order.

---

## 4. `LoadoutMovementProfile` — exact shape, bounds, and validation

New file `src/Hukbo.Core/Movement/LoadoutMovementProfile.cs`. A `public sealed`
immutable class with a positional constructor that validates every argument and
then assigns. It has no mutable state, no derived cache, and no method that
takes a `Scenario`.

### 4.1 The profile key excludes rank

`src/Hukbo.Core/Combat/CombatIdentity.cs` line 209 declares:

```csharp
public readonly record struct CombatLoadout(
    WeaponId Weapon,
    ArmorId Armor,
    ShieldId Shield,
    RankId Rank = RankId.Timawa);
```

The earlier shared plan says a profile resolves by "complete `CombatLoadout`".
Under combat preset `PrecolonialPhilippinesV4` the four rostered warriors carry
four different ranks, so a six-row lookup keyed on the whole record would fail
to resolve three of them.

**The profile key is `(WeaponId, ArmorId, ShieldId)` and is rank-independent.**
Rank is social standing. It is documented as carrying no combat-strength value
of its own and it carries no movement meaning either. The profile row stores
and validates weapon, armor, and shield. It stores no rank.
`MovementRuleset.ResolveLoadoutProfile(CombatLoadout)` reads only the three
equipment fields off its argument and ignores `Rank` entirely. A dedicated test
proves that two `CombatLoadout` values differing only in `Rank` resolve to the
same profile instance.

Armor stays part of the key even though all six current rows use
`ArmorId.LightOrganic`. A future armor must fail profile resolution loudly
rather than silently inherit another row's footwork.

### 4.2 Properties, in declaration order

Declaration order is load-bearing: it is the content-hash fold order in section
5, so it may never be rearranged once a preset ships.

| # | Property | Type | Meaning |
| ---: | --- | --- | --- |
| 1 | `Loadout` | `CombatLoadout` | The row's key. Its `Rank` is always the default and is never read. |
| 2 | `ForwardPaceBasisPoints` | `int` | Pace cap when facing-to-travel separation is 0 or 1 sectors. |
| 3 | `LateralPaceBasisPoints` | `int` | Pace cap when separation is 2 through 5 sectors. |
| 4 | `BackwardPaceBasisPoints` | `int` | Pace cap when separation is 6 through 8 sectors. |
| 5 | `CommittedPaceBasisPoints` | `int` | Additional cap applied while the phase is `Commit`. |
| 6 | `PreferredDistanceBasisPoints` | `int` | Basis points of the warrior's configured combat reach at which `Engage` is entered. |
| 7 | `OpponentDistanceOffsetBasisPoints` | `ImmutableArray<int>` | Exactly six signed cells in canonical opponent order, added to property 6. |
| 8 | `MaximumFacingStepsPerTick` | `int` | Sectors the warrior may turn in one ordinary tick. |
| 9 | `CommittedFacingStepsPerTick` | `int` | Sectors the warrior may turn while the phase is `Commit`. |
| 10 | `AccelerationBasisPointsPerTick` | `int` | Basis points of `MovementSpeedRaw` the retained pace may rise per tick. |
| 11 | `DecelerationBasisPointsPerTick` | `int` | Basis points of `MovementSpeedRaw` the retained pace may fall per tick. |
| 12 | `CommitmentTicks` | `int` | `Commit` duration, counted inclusive of the entry tick. |
| 13 | `RecoveryTicks` | `int` | `Recover` duration, counted inclusive of the entry tick. |
| 14 | `AllyClearanceBodyDiametersBasisPoints` | `int` | Basis points of body diameter this warrior wants clear of allies. |
| 15 | `DisengageEnemyToAllyBasisPoints` | `int` | Enemy-to-ally ratio, in basis points, at or above which `Disengage` is entered. |
| 16 | `ReengageEnemyToAllyBasisPoints` | `int` | Ratio at or below which `Disengage` is left. |
| 17 | `PursuitSupportBodyDiametersBasisPoints` | `int` | Basis points of body diameter inside which an ally must remain for `Pursue` to keep proposing a direct route. |

### 4.3 Validation rules

Every rule throws `ArgumentOutOfRangeException` — or `ArgumentException` for the
offset-array shape — from the constructor. Each rule gets its own rejection
test.

- Every pace, meaning properties 2 through 5, lies in the **inclusive** range
  `[1, 10_000]`. The bound must be inclusive or the Itak row, whose forward pace
  is exactly `10000`, fails construction.
- `CommittedPaceBasisPoints` may not exceed `ForwardPaceBasisPoints`.
- `PreferredDistanceBasisPoints` is strictly positive.
- `OpponentDistanceOffsetBasisPoints` contains exactly six values, indexed in
  canonical opponent order, each in the inclusive range `[-2_000, 2_000]`.
- For every one of the six cells, `PreferredDistanceBasisPoints + cell` is
  strictly positive.
- `MaximumFacingStepsPerTick` and `CommittedFacingStepsPerTick` lie in the
  inclusive range `[0, 8]`. Eight is a half turn and is the maximum meaningful
  value in a 16-sector model.
- `AccelerationBasisPointsPerTick` and `DecelerationBasisPointsPerTick` lie in
  the inclusive range `[1, 10_000]`.
- `CommitmentTicks` and `RecoveryTicks` are strictly positive.
- `AllyClearanceBodyDiametersBasisPoints` and
  `PursuitSupportBodyDiametersBasisPoints` are strictly positive.
- `ReengageEnemyToAllyBasisPoints` is **strictly less than**
  `DisengageEnemyToAllyBasisPoints`, so hysteresis always exists and a warrior
  can never enter and leave disengagement on the same counts.

### 4.4 Derived quantities and their arithmetic

None of these is stored on the profile. All are computed into scratch, per
actor, per tick, with truncation toward zero after a widened multiply.

```text
effectivePreferredBp = PreferredDistanceBasisPoints
                       + OpponentDistanceOffsetBasisPoints[targetLoadoutIndex]
preferredRaw   = checked((long)attackRangeRaw * effectivePreferredBp) / 10_000
bodyDiameterRaw = checked(2L * bodyRadiusRaw)
clearanceRaw   = checked(bodyDiameterRaw * AllyClearanceBodyDiametersBasisPoints) / 10_000
pursuitSupportRaw = checked(bodyDiameterRaw * PursuitSupportBodyDiametersBasisPoints) / 10_000
accelerationStepRaw = Math.Max(1L, checked((long)movementSpeedRaw * AccelerationBasisPointsPerTick) / 10_000)
decelerationStepRaw = Math.Max(1L, checked((long)movementSpeedRaw * DecelerationBasisPointsPerTick) / 10_000)
```

Preferred, clearance, immediate, support, and pursuit radii stay `long` scratch
values. A valid large-body scenario can push a radius past `int` without being
invalid, so nothing saturates them and no V6-only scenario-size restriction is
added. Every squared comparison widens: square the radius as
`checked((Int128)radiusRaw * radiusRaw)` and widen the coordinate squared
distance to `Int128` before comparing.

Only pace results are stored as `int`, and only after they are proven to fit.
Tests include non-divisible cases so that truncation direction is actually
proven rather than assumed.

---

## 5. Extended `MovementRuleset` and its content-hash fold order

`src/Hukbo.Core/Movement/MovementRuleset.cs` is a `public sealed class` at line
31 with a thirteen-parameter positional constructor at lines 33 through 65. The
last parameter is `selectsLeaderByRank`; the last property is `SelectsLeaderByRank`
at line 166; `ContentHash` is assigned last, at line 64, from
`ComputeContentHash()` at lines 176 through 195.

Three new members are appended, in this order, after `SelectsLeaderByRank`:

| Property | Type | V1–V5 value | V6 value |
| --- | --- | --- | --- |
| `UsesEquipmentRelativeFootwork` | `bool` | `false` | `true` |
| `ImmediateRadiusBodyDiametersBasisPoints` | `int` | `0` | `25_000` (2.5 body diameters) |
| `SupportRadiusBodyDiametersBasisPoints` | `int` | `0` | `60_000` (6 body diameters) |
| `LoadoutMovementProfiles` | `ImmutableArray<LoadoutMovementProfile>` | `ImmutableArray<LoadoutMovementProfile>.Empty` | exactly six rows in canonical order |

Constructor validation gains one coupled rule: when
`usesEquipmentRelativeFootwork` is `false`, both radii must be zero and the
profile collection must be empty; when it is `true`, both radii must be strictly
positive and the collection must contain exactly six rows whose keys are the six
canonical loadouts, each appearing once, in canonical order. A duplicate key, a
missing canonical row, an unsupported shield, or an unsupported armor fails
construction.

`ResolveLoadoutProfile(CombatLoadout)` is the only exposed accessor. It is
backed by a fixed-size lookup built once at ruleset construction, keyed on
`(WeaponId, ArmorId, ShieldId)`, and it throws for an unmapped key rather than
returning a default. A weapon row and a shield modifier are never layered at
runtime: each full loadout resolves one already-composed immutable profile.

### 5.1 Content-hash fold order

`ComputeContentHash` currently folds, through `Fnv1a.Add` and in declaration
order: `Id`, `Version`, `CohesionRadiusMultiplier`, `CloseRadiusMultiplier`,
`CloseFractionNumerator`, `CloseFractionDenominator`, `MinimumCohesiveMembers`,
`CohesionCycleTicks`, `CohesionDutyTicks`, `ArrivalTaperMultiplier`,
`OffsetUnit`, `NarrowsCohesionScanToCohesionCapableContingents`, and
`SelectsLeaderByRank`. Booleans fold as `1UL` or `0UL`.

Append, after `SelectsLeaderByRank` and before `return hash;`, in exactly this
order:

1. `UsesEquipmentRelativeFootwork` as `1UL` or `0UL`.
2. `ImmediateRadiusBodyDiametersBasisPoints` as `(ulong)value`.
3. `SupportRadiusBodyDiametersBasisPoints` as `(ulong)value`.
4. `LoadoutMovementProfiles.Length` as `(ulong)value`.
5. For each profile, in canonical `KP, WA, KA, IT, KS, IS` order:
   1. `(ulong)(int)Loadout.Weapon`, `(ulong)(int)Loadout.Armor`,
      `(ulong)(int)Loadout.Shield` — the rank field is not folded, because it is
      not part of the key;
   2. properties 2 through 6 of section 4.2, in declaration order, each as
      `(ulong)value`;
   3. `OpponentDistanceOffsetBasisPoints.Length` as `(ulong)value`, then its six
      cells in canonical opponent order, each as
      `unchecked((ulong)(long)cell)` so a negative offset preserves its
      two's-complement value;
   4. properties 8 through 17, in declaration order, each as `(ulong)value`.

Rebuilding the same six canonical rows through a differently ordered sequence of
constructor calls must produce the same content hash, because the ruleset sorts
nothing at hash time — it folds the canonical order it was validated to hold.
Changing any single scalar, or any single offset cell, must change the V6
content hash; that is a test per scalar and a test per cell.

### 5.2 The one-time pinned-literal move

`MovementRuleset.ContentHash` is **not folded into the state hash today.**
`BattleSimulation.ComputeStateHash()` passes `_rules.ContentHash`, and `_rules`
is the `CombatRuleset`, not the `MovementRuleset`. Only
`(int)scenario.MovementPreset` reaches `StateHasher`. Therefore extending the
`MovementRuleset` schema cannot move any state hash, any event hash, any
outcome, or any trajectory.

It moves exactly one thing: the five pinned content-hash literals in
`tests/Hukbo.Core.Tests/MovementPresetRegistryTests.cs`, and it adds a sixth.

| Preset | Current literal | Line |
| --- | --- | ---: |
| V1 | `0x5AFC8B9FBC247363UL` | 31 |
| V2 | `0x3E29AE36A0FAF440UL` | 40 |
| V3 | `0x520DD48EE818A603UL` | 49 |
| V4 | `0x443ECC578E1137B5UL` | 58 |
| V5 | `0x1D27722140CB87F5UL` | 67 |

All six must be **recomputed from built output** and never hand-calculated. The
class remarks at `MovementRuleset.cs:15-30` already anticipate this and say the
same thing.

Two existing "only" assertions in that file must be extended to state what V6
does: `OnlyPersistentContingentsV4NarrowsTheCrossContingentScan` at line 204 and
`OnlyPersistentContingentsV5SelectsLeaderByRank` at line 227. A third,
`OnlyEquipmentRelativeFootworkV6UsesEquipmentRelativeFootwork`, is added
alongside them. `AnUnassignedHighValueIsNotRegistered` at line 106 uses
`(MovementPresetId)99`, so registering value `6` does not disturb it.

---

## 6. `Facing16` — table, resolution, turning, and direction bands

New files `src/Hukbo.Core/Movement/Facing16.cs` and
`src/Hukbo.Core/Movement/FacingRules.cs`.

`Facing16 : byte` is append-only, with `East = 0` running clockwise through
`EastNorthEast = 15`, plus `None = 255`. Positive Y is screen-down in this
simulation, so increasing sector values rotate clockwise on screen.

### 6.1 The exact integer vector table

At `FixedPoint.Scale = 1_024`, binding to the digit:

```text
 0 ( 1024,     0)    1 (  946,   392)    2 (  724,   724)    3 (  392,   946)
 4 (    0,  1024)    5 ( -392,   946)    6 ( -724,   724)    7 ( -946,   392)
 8 (-1024,     0)    9 ( -946,  -392)   10 ( -724,  -724)   11 ( -392,  -946)
12 (    0, -1024)   13 (  392,  -946)   14 (  724,  -724)   15 (  946,  -392)
```

### 6.2 `FromDelta(dx, dy, factionId)`

1. Canonicalize faction 1 by negating `dx`. Faction 0 passes through unchanged.
2. Compute `long` dot products of the canonicalized delta against all sixteen
   table vectors.
3. Take the greatest dot product. **An exact tie takes the lower numeric sector
   in canonical space.**
4. Map faction 1's canonical result back to world space with
   `(8 - sector + 16) % 16`.
5. A `(0, 0)` delta returns `Facing16.None` without consulting the table.

No trigonometry, no `Math.Atan*`, no `MathF`, and no `double` appears in
`FacingRules.cs`. A source-hygiene test asserts their absence in that file, in
the same spirit as the existing console-usage scan over `src/`.

Canonicalization is what makes reflected inputs produce reflected facings, which
is what lets a mirrored-duel test assert an exact relationship between the two
factions rather than an approximate one.

### 6.3 Turning

To turn from `current` toward `desired`, in the same faction-canonical space:

```text
clockwise        = (desired - current + 16) % 16
counterClockwise = (current - desired + 16) % 16
```

Take the smaller. **An eight-step tie turns clockwise in canonical space**,
which maps to counter-clockwise in world space for faction 1. No weapon
document covers this case, so the shared foundation owns its test outright.

Advance by at most `MaximumFacingStepsPerTick`, or
`CommittedFacingStepsPerTick` when the phase is `Commit`. A turn request exactly
at the step cap reaches the desired facing; a request one sector beyond the cap
advances only by the cap.

Facing turns toward the selected living threat. When there is no threat, it
turns toward the movement destination. When there is neither, facing is
retained. Initial V6 facing is `East` for faction 0 and `West` for faction 1.
Dead agents retain their final facing, because a corpse's last orientation is
readable spectator information and clearing it would erase it.

### 6.4 Direction bands

Classify the circular separation between the committed facing and the chosen
travel direction, then cap the pace:

| Separation, in sectors | Pace cap |
| --- | --- |
| 0 to 1 | `ForwardPaceBasisPoints` |
| 2 to 5 | `LateralPaceBasisPoints` |
| 6 to 8 | `BackwardPaceBasisPoints` |

The forward band is one sector narrower at the top than the Kalis and Itak
research documents suggested. The shared band above wins; section 19.3 records the
research figure as not adopted.

If the phase is `Commit`, the cap becomes
`min(direction band cap, CommittedPaceBasisPoints)`. That capped basis-point
value converts to `desiredPaceRaw`, itself capped at `MovementSpeedRaw`.

### 6.5 Retained pace

`AgentState.MovementPaceRaw` moves toward `desiredPaceRaw` by
`accelerationStepRaw` when the desired pace is higher and by
`decelerationStepRaw` when it is lower, never overshooting the target and never
exceeding `MovementSpeedRaw`. The proposal step is then computed from the
resulting pace and the remaining distance, and the proposed pace is stored in
preallocated scratch.

After collision, `MovementPaceRaw` commits as
`min(proposedPaceRaw, actualMovedRaw)`. A blocked, rejected, or refused move
therefore leaves zero retained pace rather than fictitious momentum. Turning in
place remains permitted, because facing commits independently of displacement.

This is a one-dimensional pace memory. It is not a velocity vector, an
acceleration engine, momentum, force, or a rigid-body system, and the
implementation may not grow into one.

### 6.6 Committed turn budget and the shield non-penalty

The committed turn budget is **1 sector** for every row. The Kampilan research
suggested a `1/12` turn; one twelfth is not representable in a 16-sector model,
so it is not adopted.

**Shield rows carry no facing penalty.** The tall-hardwood research proposed a
`0.88` facing-change multiplier. A 16-sector integer model cannot express it
without rounding it to either the solo value or to a value that would make a
shielded warrior turn strictly worse than a Wasay wielder, which is not what the
research argued. `MaximumFacingStepsPerTick` for `KS` and `IS` therefore equals
the solo value of `2`, and no shield row asserts a turn difference from its solo
counterpart. This is a deliberate non-adoption, recorded in section 19.3.

---

## 7. Derived context and its oracle

### 7.1 Types

New files `src/Hukbo.Core/Movement/LoadoutCompositionCounts.cs` and
`src/Hukbo.Core/Movement/LocalMovementContext.cs`, both immutable value types:

```text
LoadoutCompositionCounts(Kampilan, Wasay, Kalis, Itak, KalisShield, ItakShield)

LocalMovementContext(
    ImmediateAllies, ImmediateEnemies,
    SupportAllies, SupportEnemies,
    AlliedComposition, EnemyComposition,
    NearestAllyEntityId, SecondThreatEntityId)
```

`SupportAllies` **includes the acting warrior**. `AlliedComposition` likewise
includes the actor's own loadout bucket. Immediate counts exclude self. Dead
agents count nowhere.

`NearestAllyEntityId` is the nearest living ally inside the support radius.
`SecondThreatEntityId` is the nearest living immediate enemy other than the
already-selected target. Squared-distance ties for both break on lower
`EntityId` — never on collection insertion order and never on PRNG state.
Absence uses the existing no-entity sentinel.

The selected target's loadout is read from authoritative agent state when the
spacing offset is applied, so no second copy of the target's identity is cached
anywhere.

### 7.2 Radii

Both radii are stored on `MovementRuleset` as basis points of body diameter —
`25_000` for the immediate radius, `60_000` for the support radius — and are
materialized per tick as derived `long` values through the arithmetic in section
4.4. Every comparison squares through `Int128` as specified there.

### 7.3 Where accumulation hooks in

The context is derived inside the existing tick-start all-agent observation in
`BattleSimulation.SelectTargetsAndIntents`, which today has **no movement-preset
branch at all**. Its structure is a source-agent loop at line 705, a dead
short-circuit at 707 through 712, a candidate loop at 719 with same-faction and
dead skips at 721 through 724, cheap axis rejects at 735 through 746, a squared
distance perception test at 748 through 752, and the target tie-break at 754
through 761.

The V6 accumulation hook is **between line 752 and line 754** — after the
perception test passes, before the comparison block — reusing the already
computed `deltaX`, `deltaY`, and `distance`. The comparison block reads only
`distance`, `selectedDistance`, and `candidate.EntityId`, so nothing written at
the hook can affect tie-breaking, and target selection stays byte-identical.

Accumulation must **not** hook between lines 721 and 752. The axis rejects at
735 through 746 are pre-filters, and a candidate rejected there never reaches
the distance test, so hooking above the test would silently drop neighbours.

One `LocalMovementContext` scratch row per scenario agent is allocated in the
constructor, cleared and overwritten every tick, and never hashed, never
snapshotted, and never grown.

### 7.4 The pure query and its oracle

The accumulation logic is extracted as an internal pure query,
`src/Hukbo.Core/Movement/MovementContextQuery.cs`, taking an explicitly supplied
`ReadOnlySpan<AgentState>` so that a test can permute storage order. The
production observation calls that query under V6.

`tests/Hukbo.Core.Tests/Movement/NaiveMovementContextQuery.cs` is an independent
O(n²) oracle written in the test project. Production context output must equal
the oracle **field for field** across: seeded worlds; explicitly permuted
candidate spans; exact-radius tangencies; dead agents; all six loadouts; zero
neighbour cases; maximum supported coordinates; and maximum valid body radius.

`BattleSimulation.CreateForTesting` may **not** be used to claim storage-order
coverage — it canonicalizes agents by `EntityId`, so it proves nothing about
order independence. The span seam is the only honest way to get that coverage.

### 7.5 Global totals and role coverage

Global surviving totals and composition live in two fixed faction slots and
twelve integer counters, derived in the same pre-movement stage, and remain
scratch. From each faction's complete-loadout counts, derive three presence
flags:

- `HasLongClearanceRole` when `KP + WA > 0`;
- `HasMobileBladeRole` when `KA + IT > 0`;
- `HasShieldSupportRole` when `KS + IS > 0`.

Their sum is `RoleCoverage`, an integer in `[0, 3]`. Presence affects only the
contested posture tie-break in section 8. It never adds virtual warriors, never
changes pace, and never changes a headcount.

---

## 8. Tactical posture resolution

New file `src/Hukbo.Core/Movement/TacticalPosture.cs`, a public append-only
enum:

```text
TacticalPosture : byte
None = 0, Advance = 1, Hold = 2, Yield = 3,
Regroup = 4, Pursue = 5, Withdraw = 6
```

Posture is resolved once per contingent per tick, in the V6 stage that runs
between the existing contingent-state resolution and movement proposal
gathering — section 11.2 places it exactly — and is written to
`AgentState.TacticalPosture` on every living member. Its inputs are the global
living faction totals and the role-coverage flags from section 7.5, plus the
contingent's own tick-start `ContingentState`, which the existing stage at
`AdvanceOneTick` line 403 has already resolved by the time posture runs.

### 8.1 The nine branches

First-match order. `allies` is the acting contingent's faction-wide living
total and `enemies` is the opposing faction's. Every comparison is a `long`
checked cross-product; nothing divides.

1. The contingent has no living member: `None`.
2. No living enemy exists: `Pursue`.
3. `allies * 2 <= enemies`: `Withdraw`.
4. `allies * 4 <= enemies * 3`: `Yield`.
5. The contingent's existing `ContingentState` is `Hold`: `Regroup`.
6. `allies * 4 >= enemies * 5`: `Advance`.
7. `allies >= enemies` and allied `RoleCoverage` is strictly greater than
   enemy coverage: `Advance`.
8. `allies <= enemies` and allied `RoleCoverage` is strictly less than enemy
   coverage: `Yield`.
9. Otherwise: `Hold`.

The operators above are exact. A ratio landing exactly on a boundary matches
the earliest branch whose operator admits it, which is always the more
conservative reading: exact double outnumbering is already `Withdraw`, exact
four-to-three pressure is already `Yield`, and exact five-to-four advantage is
already `Advance`. Each boundary gets its own equality test, and a contested
world with equal headcounts and equal coverage must fall through branches 7
and 8 to `Hold`.

### 8.2 What posture may never touch

`RoleCoverage` affects only branches 7 and 8. It never adds virtual warriors,
never changes pace, and never changes a headcount. Posture itself changes
routes and phases downstream; it never changes physical speed.

Tactical disadvantage is never written into `ContingentState.Break`, which
remains the persistent cohesion condition, nor into `AgentIntent.Regrouping`,
which remains the last-stand override. Both keep their current meanings
byte-for-byte, and a test asserts that a posture run producing `Withdraw`
writes neither.

---

## 9. Footwork phase resolution

New file `src/Hukbo.Core/Movement/FootworkPhase.cs`, a public append-only
enum:

```text
FootworkPhase : byte
None = 0, Approach = 1, Engage = 2, Commit = 3, Recover = 4,
Refuse = 5, Disengage = 6, Regroup = 7, Pursue = 8
```

### 9.1 The ten transition steps

Per living agent, in this order, first match wins:

1. Dead: `None`, zero timer.
2. Continuing `Commit`: decrement the timer; when the prior timer is `1`,
   enter `Recover` with the profile recovery duration.
3. Continuing `Recover`: decrement the timer; when the prior timer is `1`,
   fall through to the rules below.
4. An agent already disengaging remains `Disengage` until
   `enemies * 10_000 <= allies * ReengageEnemyToAllyBasisPoints`.
5. A new disadvantage enters `Disengage` when
   `enemies * 10_000 >= allies * DisengageEnemyToAllyBasisPoints`.
6. Posture `Withdraw` or `Yield`: `Disengage`.
7. Posture `Regroup`: `Regroup`.
8. The selected target sits at or inside the offset-adjusted preferred
   distance, compared inclusively on squared values: provisional `Engage`.
9. A target is present: provisional `Approach`.
10. Posture `Pursue`: provisional `Pursue`; otherwise provisional `None`.

One correction to the earlier shared plan is binding here. Its prose says
"steps 5–6" carry the enemy-to-ally ratio, but the ratio steps are **4, the
release, and 5, the entry**. Step 6 is the posture branch and carries no
ratio.

The counts in steps 4 and 5 are `SupportEnemies` and `SupportAllies` — the
six-body-diameter support scan, with the actor counted on the ally side, as
section 7 defines. Immediate counts shape route safety in section 10 only;
they never feed these ratios and never form a second threshold system.

### 9.2 Hysteresis at the boundaries

- **Entry equality enters.** A ratio exactly at
  `DisengageEnemyToAllyBasisPoints` starts disengagement.
- **Release equality leaves.** A ratio exactly at
  `ReengageEnemyToAllyBasisPoints` ends it.
- A value strictly between the two thresholds preserves the previous state.
- Construction validation in section 4.3 requires `Reengage < Disengage`
  strictly, so hysteresis always exists and no count can enter and leave
  disengagement on the same tick.
- Zero living enemies never enters and never remains in disengagement on the
  ratio arithmetic alone, with no special case: the entry test
  `0 >= allies * threshold` is false because `SupportAllies` is at least one
  — the actor counts itself — and the release test `0 <= allies * threshold`
  is true.

Every comparison is a widened checked integer cross-product. Nothing divides.

### 9.3 Step 6 is unconditional

Every member of a `Withdraw` or `Yield` contingent takes phase `Disengage`
regardless of its own local advantage. Only the *route* differs per agent —
toward the nearest ally, the leader, or the computed escape vector of section
10.4. The Kampilan and Wasay plans claim global posture "cannot force
synchronized retreat"; that claim is true of routes and false of the phase,
and section 19.3 records that both sessions must restate those acceptance rows
as "routes are not synchronized".

### 9.4 Two-step finalisation

Phases resolve in two steps without writing authoritative state between them.

First, compute the provisional phase from the ten steps above and keep any
`Commit` or `Recover` timer transition in scratch. Second, generate that
provisional mode's route candidates and clearance-test them under section 10.
Only then commit:

- If at least one candidate survives, the provisional phase stands and the
  surviving candidate becomes the proposal.
- If no candidate survives and the provisional phase is `Approach`, `Engage`,
  or `Pursue`, finalise `Refuse` with a zero timer.
- If no candidate survives and the provisional phase is `Commit`, `Recover`,
  `Disengage`, or `Regroup`, retain the phase and its timer but emit no
  movement — a blocked lane must not erase a safety or attack lifecycle.
- `None` stays `None`.

`FootworkPhase` and `FootworkTicksRemaining` are written exactly once, after
this finalisation.

### 9.5 Entry-tick timer semantics

An entry timer counts the current tick: duration `3` means the attack tick
plus two following `Commit` ticks. The committed pace cap and the committed
turn cap apply on the following retained `Commit` ticks, not on the attack
tick itself, because the attack tick's movement was proposed before the attack
was accepted.

### 9.6 Attack acceptance marking and `Commit` entry

V6 never predicts or pre-authorizes an attack before movement. The combat
contract stays byte-for-byte: agents move into reach, collision resolves, and
`GatherAndCommitAttacks` performs its existing target, combo, range, and
cooldown prechecks against post-movement positions in its existing order.

One reusable scratch bit, `AttackAcceptedThisTick`, is set inside the existing
accept path — after the five prechecks pass, alongside the attack-proposal
buffering at `BattleSimulation.cs` lines 2010 through 2012. The gather then
finishes its accumulation, damage application, cooldown, combo, and event work
unchanged.

Immediately after `GatherAndCommitAttacks` returns and before
`ResolveOutcome` — between lines 408 and 409 of `AdvanceOneTick` — two V6-only
finalisation passes run:

- Surviving accepted attackers enter `Commit` with the profile's
  `CommitmentTicks`. An accepted attack interrupts `Recover` and starts a
  fresh `Commit`; movement recovery never suppresses or delays an attack that
  the existing combat gates accept.
- Accepted attackers killed by the same gathered exchange take death cleanup
  instead: `MovementPaceRaw`, `TacticalPosture`, `FootworkPhase`, and
  `FootworkTicksRemaining` are cleared atomically before any outcome, hash, or
  snapshot work. Final `Facing` is retained, because a corpse's last
  orientation is readable spectator information.

Combo invalidation, simultaneous damage gathering, cooldown writes, event
order, and every V1-through-V5 path remain unchanged, and tests assert each of
those separately.

---

## 10. Route candidates, lane clearance, and the conflict pass

V6 generates a bounded candidate list per agent per tick — never a path, never
a queue, never a plan that outlives the tick.

### 10.1 `StepEndpoint`

Every candidate endpoint is built by one widened-integer helper, matching the
existing movement normalizer and map clamp:

```text
StepEndpoint(dx, dy, paceRaw):
  reject (0, 0)
  distance = IntegerSquareRoot(checked(dx*dx + dy*dy))
  moveX = checked(dx * paceRaw / max(1, distance))
  moveY = checked(dy * paceRaw / max(1, distance))
  if moveX == 0 and moveY == 0:
    move one raw unit on the greater absolute axis; X wins equality
  return ClampCenterToBounds(actor + (moveX, moveY))
```

Division truncates toward zero everywhere. The degenerate one-raw-unit
fallback moves on the greater absolute axis, and **X wins an exact tie**.

`IntegerSquareRoot` is today a private bitwise-restoring helper on
`BattleSimulation`, lines 2460 through 2487. The route helper needs it from
another type, so it is lifted to `FixedPoint`, which has no content hash of
its own, making the lift hash-neutral. `FormationPlanner`'s separate private
copy at its line 364 is left alone rather than risk moving formation output.
`ClampCenterToBounds` is the existing `CollisionGeometry` clamp.

### 10.2 The oblique vectors

For a target delta `(dx, dy)`, the two 22.5-degree oblique direction vectors
are, verbatim from the shared plan:

```text
clockwise        = (checked(946*dx - 392*dy) / 1024,
                    checked(392*dx + 946*dy) / 1024)
counterClockwise = (checked(946*dx + 392*dy) / 1024,
                    checked(-392*dx + 946*dy) / 1024)
```

If a nonzero source delta produces `(0, 0)` after truncation, resolve the
delta's `Facing16`, rotate that sector by one in the requested direction, and
substitute the resulting table vector before calling `StepEndpoint`.

### 10.3 Side parity

Side selection happens in faction-canonical space. `sideA` is
canonical-clockwise for an **even** faction-local index and
canonical-counter-clockwise for an odd one; `sideB` is the other. Mapping back
to world space swaps the two rotations for faction 1.

The faction-local index is the stable ascending-`EntityId` rank within that
faction, computed once into a fixed-size scratch array at simulation
construction. Normal scenario creation therefore matches the existing mirrored
deployment index, and testing scenarios with sparse entity ids remain defined.
It is never global `EntityId` parity, and it is neither hashed nor
snapshotted.

### 10.4 Candidate order per phase

- `Approach`: direct, `sideA`, `sideB`.
- `Engage`: the preferred distance selects the phase but **is not a stop
  line** — the agent continues toward the target's centre so the existing
  post-movement reach test stays authoritative. A homogeneous enemy
  composition, meaning one occupied enemy loadout bucket inside the support
  radius, uses direct, `sideA`, `sideB`; two or more occupied buckets use
  `sideA`, `sideB`, direct.
- `Commit`: if the selected target still lives, its direct delta only;
  otherwise the current `Facing16` table vector. The phase supplies the
  committed pace and turn caps.
- `Recover`: the vector opposite the current facing, then its parity-selected
  22.5-degree sides. `Facing16.None` falls back to the direction away from
  the nearest threat; if neither exists, emit no proposal.
- `Disengage`: first a step toward `NearestAllyEntityId`, then toward the
  contingent leader. If neither exists,
  `escape = (actor - nearestThreat) + (actor - secondThreat)`, where a
  missing second threat contributes zero. If that sum is zero, use the
  parity-selected perpendicular to the nearest-threat escape vector. If no
  threat exists at all, emit no proposal.
- `Regroup`: the nearest perceived ally, then the contingent leader; absence
  emits no proposal.
- `Pursue`: direct only while at least one ally remains within the profile's
  pursuit support distance.

**The second-threat rule:** when `ImmediateEnemies >= 2`, omit the direct
candidate **only when** its endpoint is **strictly closer** to
`SecondThreatEntityId` than the actor's tick-start position is. Exact equality
keeps the direct candidate.

### 10.5 Lane clearance

A candidate lane is unsafe when its one-tick endpoint sits at squared distance
**strictly less than** the square of the larger of the actor's and that ally's
profile clearance radii. **Exact equality is clear**, matching
`CollisionGeometry.Overlaps`' existing strict-`<` tangency convention — and
deliberately the opposite side from context counting, which section 7 makes
inclusive, because a neighbour on a boundary is perceived while a body on a
boundary is not intruding.

The test runs as a second stable all-agent scan per actor: for each candidate
endpoint, inspect every living same-faction agent, resolve that ally's
profile, and reject the endpoint on violation. The scan stores no neighbours
and allocates nothing after construction. No spatial grid is added; section 18
carries the budget and the stop rule that would justify one later.

If every candidate is unsafe, the agent emits no proposal and section 9.4's
finalisation applies. The collision resolver's `MovementResolution` stays
reserved for proposals actually submitted to it.

### 10.6 The friendly-clearance conflict pass

After all candidate proposals exist and before the existing body-collision
resolver runs — between `AdvanceOneTick` lines 404 and 405, the only point
where every proposal exists and nothing has been committed, preserving the
invariant documented at `BattleSimulation.cs` lines 1145 through 1148 — one
V6-only conflict pass runs per faction.

Order proposals by phase safety — `Disengage`, `Recover`, `Commit`, `Regroup`,
`Engage`, `Approach`, `Pursue` — then by lower `EntityId`. Accept a proposal
only if its endpoint lies **at or beyond** the larger profile clearance radius
from every already accepted same-faction endpoint: equality accepts, the same
side as the lane test. The pass is faction-local; it never inspects the other
faction, whose bodies remain the existing resolver's job.

The tick-start lane scan already proved each accepted endpoint clear of every
ally that stays stationary, so rejecting a later conflicting proposal cannot
make an already accepted endpoint unsafe. A rejected proposal becomes a
no-move with zero retained pace and increments the derived clearance-denial
counter of section 16; it does not reroute and does not change phase. An
independent naive pairwise oracle in the test project must match the accepted
set exactly. The existing resolver then handles physical cross-faction and
body collision unchanged.

---

## 11. Destination precedence and pipeline integration

### 11.1 Destination precedence

When more than one destination source applies to an agent on one tick, the
higher entry in this chain wins:

```text
Dead
> body-contact Attacking hold
> existing last-stand AgentIntent.Regrouping destination
> V6 Disengage / tactical Withdraw route
> existing contingent-cohesion destination
> V6 equipment approach, engage, regroup, or pursuit route
> existing ordinary target pursuit
```

A precedence test exists for every adjacent pair in that chain. After a
destination is selected, facing and phase pace constrain its one-tick proposal
under section 6; the conflict pass of section 10.6 may reject it; and the
existing body-collision resolver remains authoritative afterward.

### 11.2 Where each V6 stage inserts

Every insertion point below was verified against `BattleSimulation.cs` at
`caf0d63`, and every one is guarded by
`MovementRuleset.UsesEquipmentRelativeFootwork`.

| V6 work | Insertion point |
| --- | --- |
| Context accumulation (section 7) | Inside `SelectTargetsAndIntents`, between the perception test ending at line 752 and the comparison block starting at line 754 |
| Global totals and role coverage (section 7.5) | Same pre-movement observation stage |
| Posture, then provisional footwork phase (sections 8, 9) | Between `ResolveContingentStates` (line 403) and `GatherMovementProposals` (line 404) |
| Route candidates, lane clearance, pace application (sections 6, 10) | Inside the proposal-gathering stage, under the flag |
| Friendly-clearance conflict pass (section 10.6) | Between `GatherMovementProposals` (line 404) and `ResolveCollisions` (line 405) |
| Retained-pace commit `min(proposedPaceRaw, actualMovedRaw)` | Inside `CommitMovement` (line 406) |
| `AttackAcceptedThisTick` marking | Inside `GatherAndCommitAttacks`' accept path (line 408) |
| `Commit` entry and same-tick death cleanup (section 9.6) | Between `GatherAndCommitAttacks` (line 408) and `ResolveOutcome` (line 409) |

The conflict pass may not live inside `GatherMovementProposals`: the invariant
at lines 1145 through 1148 is that no agent sees another agent's move while
proposals are still being formed.

### 11.3 Byte-for-byte legacy preservation

A V1-through-V5 run executes the same instructions it executes today. Legacy
presets never build context, never resolve a posture or a phase, never run the
conflict pass, and skip the deployment reassignment of section 12. Control
tests prove the V6 accumulation is not invoked under a legacy preset.

`ContingentState.Break` and `AgentIntent.Regrouping` are never written to
represent tactical disadvantage; both keep their existing meanings untouched.
Legacy state hashes, event hashes, outcomes, ordered event streams, and
trajectory fixtures — the existing V1 and V2 fixtures plus the V3, V4, and V5
fixtures this workstream freezes first — remain byte-identical. The only
sanctioned movement is the one-time pinned registry-literal move of section
5.2.

---

## 12. Equipment-aware deployment assignment

V6 alone reassigns warriors to existing formation slots so that loadouts
needing more ally clearance get the roomier existing positions. Nothing about
the formation itself changes: contingent sizes, the irregular lattice, slot
coordinates, mirroring, collision-safe repair, and the SplitMix64 draw count
all stay exactly as they are.

### 12.1 The seam

The reassignment runs in `BattleSimulation.Create`, immediately after
`FormationPlanner.PlanFactionDeployment` returns at lines 247 through 249 and
before the roster expansion and the faction-0 spawn loop — that is, at line
250 — and it permutes the single canonical `deployment` array **before** the
faction-1 mirroring at line 273, or faction 1 would stop being an exact
mirror.

`random` is declared at line 240 and referenced in `Create` only at line 249;
nothing downstream of the seam draws. The permutation is therefore invisible
to the SplitMix64 stream — **zero additional draws**, pinned by a
draw-sequence test. `ResolveSpawnPlacement` at line 301 never consults the
RNG and is not edited. `FormationPlanner` itself is not edited either; the
earlier shared plan wanted changes there, and the seam above supersedes that.

The logic lives in a new internal static helper,
`src/Hukbo.Core/Movement/EquipmentDeploymentAssignment.cs`, so
`BattleSimulation.Create` gains only a guarded call.

### 12.2 The algorithm

1. A singleton contingent, or a contingent whose warriors all resolve equal
   `AllyClearanceBodyDiametersBasisPoints`, keeps its original faction-local
   entity-to-slot mapping — an exact identity, asserted directly.
2. Otherwise, compute each canonical, unmirrored slot's minimum squared
   distance to another slot in that contingent.
3. Sort canonical slots by that value descending, then by canonical `XRaw`,
   `YRaw`, and original slot index ascending.
4. Sort warriors by resolved `AllyClearanceBodyDiametersBasisPoints`
   descending, then by canonical loadout index and faction-local index
   ascending.
5. Pair the two sequences within each faction. Faction 1's mirrored slots are
   ranked by their canonical pre-reflection coordinates, so equal
   faction-local loadout multisets produce the same permutation before the
   existing X reflection.

Contingent membership never changes, no slot moves, and no front or back rank
is invented. Reflection tests use explicitly symmetric `RosterCounts`;
default round-robin rosters are not required to mirror when their
faction-local loadout multisets differ. V1 through V5 skip the reassignment
byte-identically.

---

## 13. The six profile rows and the opponent-distance offsets

This foundation session ships all six rows. The five weapon sessions own the
focused acceptance tests that assert against them, and any later retuning of a
row belongs to its owning session — but the values below are the single
authority, superseding every figure quoted in the weapon plans where they
disagree.

The rows live under `src/Hukbo.Core/Movement/Profiles/` in five files —
`KampilanMovementProfile.cs`, `WasayMovementProfile.cs`,
`KalisMovementProfile.cs`, `ItakMovementProfile.cs`, and
`TallHardwoodMovementProfiles.cs` — preserving one ownership seam per weapon
session, and are composed into the V6 registry entry in canonical order.

Every value in both tables is **Provisional reconstruction: gameplay tuning;
no historical measurement**, and that marker goes in the XML documentation of
each row.

### 13.1 The scalar rows

| Loadout | Fwd | Lat | Back | Commit | Preferred | Turn | CommitTurn | Accel | Decel | CommitTicks | Recover | Clearance | Disengage | Reengage | PursuitSupport |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `KP` Kampilan | 9800 | 8200 | 7000 | 3000 | 11500 | 2 | 1 | 5000 | 6000 | 3 | 3 | 15000 | 20000 | 12500 | 12500 |
| `WA` Wasay | 9400 | 7400 | 6400 | 2500 | 10800 | 1 | 1 | 4000 | 5000 | 4 | 4 | 17500 | 20000 | 12500 | 10000 |
| `KA` Kalis | 9700 | 8900 | 7600 | 3300 | 12000 | 2 | 1 | 6000 | 7000 | 2 | 2 | 12000 | 15000 | 11000 | 12500 |
| `IT` Itak | 10000 | 9300 | 8100 | 4000 | 11000 | 2 | 1 | 7000 | 8000 | 2 | 2 | 11500 | 12500 | 10000 | 10000 |
| `KS` Kalis + Tall Hardwood | 9400 | 8400 | 6700 | 3000 | 13000 | 2 | 1 | 5600 | 6000 | 3 | 3 | 14000 | 17500 | 11000 | 10000 |
| `IS` Itak + Tall Hardwood | 9700 | 8700 | 7100 | 3500 | 10000 | 2 | 1 | 6500 | 7000 | 3 | 3 | 13500 | 15000 | 11000 | 8000 |

The column names abbreviate the section 4.2 properties in declaration order.
Note that Itak's forward pace sits exactly at the inclusive `10_000` maximum,
which is why section 4.3's pace bound is inclusive.

### 13.2 The opponent-distance offsets

Signed basis points added to the actor's `PreferredDistanceBasisPoints`, in
canonical opponent order `KP, WA, KA, IT, KS, IS`:

| Actor | KP | WA | KA | IT | KS | IS |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `KP` | 0 | 0 | 250 | 500 | 250 | 500 |
| `WA` | 500 | 0 | 250 | 500 | 250 | 500 |
| `KA` | -500 | -250 | 0 | 250 | 250 | 500 |
| `IT` | -750 | -500 | -250 | 0 | 0 | 250 |
| `KS` | -250 | 0 | 250 | 500 | 0 | 250 |
| `IS` | -500 | -250 | 0 | 250 | -250 | 0 |

The offsets make enemy composition authoritative only through the
already-selected opponent's equipment. They do not alter target selection,
attack reach, or hit eligibility.

### 13.3 Rulings the weapon sessions must not overwrite

- **Shielded Kalis preferred distance is `13000`**, 1.30 of reach. The Kalis
  plan contains a stale sentence saying 1.10; it is wrong and must not be
  copied into a test.
- **Shielded disengage bands are `KS = 17500` and `IS = 15000`**, as tabled.
  Both sit outside the tall-hardwood research document's own suggested entry
  band of roughly 1.25 to 1.49; the weapon plans deliberately chose the more
  tolerant thresholds, and section 19.3 records the divergence.
- **Solo-to-shielded differences number thirteen fields per weapon, not
  eight.** The tall-hardwood plan's comparison table omits four of them —
  committed pace, deceleration, reengage, and pursuit support. Note in
  particular that shielded Itak's reengage rises from `10000` to `11000`, so
  it leaves disengagement at *higher* enemy pressure than solo Itak. The
  tall-hardwood session asserts every differing field.
- **The "shared engaged-entry cap" that three weapon plans reference is not a
  profile field and must not become one.** It resolves to the pace cap the
  actor already has for its direction band under section 6.4, further clamped
  by `CommittedPaceBasisPoints` while the phase is `Commit`, and finally
  capped at `MovementSpeedRaw`.

---

## 14. New authoritative state and the hash contract

### 14.1 Five new `AgentState` fields

Appended after `MovementResolution`, currently the last settable property at
`AgentState.cs` line 149, in exactly this order:

| Field | Type | Legacy value | V6 behaviour |
| --- | --- | --- | --- |
| `Facing` | `Facing16` | `None` | Initialised at spawn — `East` for faction 0, `West` for faction 1 — then turned per section 6 |
| `MovementPaceRaw` | `int` | `0` | The retained scalar pace of section 6.5 |
| `TacticalPosture` | `TacticalPosture` | `None` | Written every tick per section 8 |
| `FootworkPhase` | `FootworkPhase` | `None` | Written once per tick after section 9.4's finalisation |
| `FootworkTicksRemaining` | `int` | `0` | The `Commit`/`Recover` timer |

V1 through V5 leave all five at their legacy values forever. Declaration
order is load-bearing — it is the V6 hash fold order — and is frozen once the
V6 digest ships.

### 14.2 The `StateHasher` extension

`StateHasher.Compute` gains one trailing parameter,
`ulong? movementContentHash = null`, following the existing `hasRankLevels`
conditional fold as the exact precedent.

- **When `null`** — every preset V1 through V5 — the method executes the
  byte-for-byte legacy fold. Not one folded value moves.
- **When non-null** — V6 only — two additions apply: the movement content
  hash folds immediately after the combat `contentHash` in the scenario
  section, and the five new agent fields fold at the tail of each per-agent
  fold, in section 14.1's declaration order, enums as `(ulong)(int)value`.

One precision note against the earlier shared plan: it says the five fields
fold "after `ContingentState`", but the existing conditional `Rank` fold
already follows `ContingentState`. The five fields fold after that
conditional `Rank` fold — at the true tail — so the legacy layout is
untouched in both `hasRankLevels` states.

`BattleSimulation.ComputeStateHash` passes `null` for V1 through V5 and
`_movementRules.ContentHash` for V6. The deliberate consequence: changing any
profile scalar or any offset cell changes every V6 state hash on tick zero.
That is why V6 ships its own digest fixture last, and why retuning any value
after that digest ships requires appending V7 rather than editing V6.

### 14.3 What must not move

- The canonical-gate seed-1 headless workload: state hash
  `1B73FC5923879AA0`, event hash `AC55684F24D39344`, outcome
  `Faction1Victory`.
- The pinned pairs in `tests/Hukbo.Core.Tests/DeterminismTests.cs`:
  `PresetV4_SeedOneStateAndEventHashArePinned`
  (`2BBEDD668CC38FD6` / `228818712E5AE6C6`),
  `PresetV3_SeedOneStateAndEventHashArePinned`
  (`BD2E2055DC1E29A9` / `71E7B6746D00C5D1`), and
  `PersistentContingentsV2_SeedOneStateAndEventHashArePinned`
  (`41201454CCBADC75` / `514D986A2BD633E8`) — the last deliberately follows
  the default combat preset rather than pinning it, and stays that way.
- `ScenarioTests.CreateDefaultSelectsPersistentContingentsV4MovementPreset`:
  the shipped movement default does not change.
- The V1 and V2 trajectory fixtures, and the V3, V4, and V5 fixtures frozen
  by this workstream before any structural edit.
- `BattleSimulationTests.ExactlyOneLivingLeaderPerNonEmptyContingentAcrossEveryRegisteredMovementPreset`
  enumerates `Enum.GetValues<MovementPresetId>()`, automatically includes V6,
  and must keep passing under it.

---

## 15. Snapshot, view, and the agent inspector

This is the section that satisfies the spectator-discovery requirement: a
spectator who never reads source code discovers the feature through four new
inspector rows backed by authoritative state.

### 15.1 `AgentView` gains five trailing-default members

`AgentView` is a public readonly record struct whose last member today is
`IsLeader = false` at `AgentView.cs` line 64. Five members are appended after
it, each with a trailing default so that presentation tests written before
the field existed still compile — the same convention the five existing
defaulted members already follow:

```text
Facing16 Facing = Facing16.None,
int MovementPaceRaw = 0,
TacticalPosture TacticalPosture = TacticalPosture.None,
FootworkPhase FootworkPhase = FootworkPhase.None,
int FootworkTicksRemaining = 0
```

`AgentState.ToView` maps them positionally in declaration order, and snapshot
tests prove all five survive `CreateSnapshot`. These are authoritative fields,
not derived caches, so they belong in `BattleSnapshot`; no derived query data
rides along.

### 15.2 Four inspector rows

`AgentInspectorContent.BuildLowerLines` gains four rows — Facing, Posture,
Footwork, and Pace — each produced by an `internal static string?
Format*Line(...)` pure formatter taking only authoritative or catalog values,
following the existing pure-helper pattern. The rows are conditional: under a
legacy preset all five view fields hold their defaults and every formatter
returns `null`, so legacy inspector output is byte-identical. A dead V6
agent's retained facing still renders, because that is the point of retaining
it.

Row texts are plain language, never raw enum numerals:

- **Facing** renders a compass label — `Facing: East`,
  `Facing: North-northeast` — never a sector number.
- **Posture** and **Footwork** render plain English words for the enum
  members. The Footwork row appends the remaining ticks only while the phase
  is `Commit` or `Recover`.
- **Pace** renders the retained pace as a percentage of the warrior's full
  movement speed, computed with pure integer arithmetic inside the formatter.

Exact strings are pinned by the formatter tests, and
`AgentInspectorPanel` keeps holding only aliases of the content constants and
its one `DrawLine` closure. Client tests never construct `ArenaGame`, a
graphics device, a sprite batch, or a window.

### 15.3 The row budget

`MaximumLowerRowCount` rises from `15` to `19` at
`AgentInspectorContent.cs` line 51, because `BuildLowerLines` already emits up
to fifteen rows. The three tests that pin the budget are updated together in
`tests/Hukbo.Client.Tests/AgentInspectorContentTests.cs`:
`EveryLowerLineFitsInsideTheReservedRowBudget` — which also asserts the
shielded row count exceeds the two-handed count by exactly one and must keep
doing so — `LowerLinesWithAContingentRowNeverExceedTheRowBudget`, and
`LowerLinesWithAContingentRowAndTheRankReconstructionNoteNeverExceedTheRowBudget`.
`ComputeRequiredHeight` and the panel height follow from the constant and
need no separate change beyond their tests.

---

## 16. Derived observability

`MovementBehaviorMetrics` does not exist anywhere in `src/` today; the name
appears only in the earlier shared plan. It is created new, modelled directly
on `CollisionMetricsAccumulator` in
`src/Hukbo.Core/Simulation/CollisionMetrics.cs` lines 99 through 221: a
mutable internal accumulator struct with `Reset()`, an `AddTick(...)` that
validates every input with `ArgumentOutOfRangeException.ThrowIfNegative`,
`long` totals summed in a `checked` block, and a non-consuming
`readonly ToMetrics()`. Like `CollisionMetrics`, its counters are never
hashed, never snapshotted, never persisted, and never simulation input.

### 16.1 What is counted

Agent-ticks spent in each footwork phase, posture transitions, facing steps
taken, `Refuse` finalisations, friendly-clearance conflict denials, and
disengagement entries.

Phase occupancy, posture transitions, facing steps, refusals, and
disengagement entries are derived **outside the simulation**, in
`HeadlessRunner`, by comparing the current and previous `AgentView` arrays,
both allocated once per run. The conflict-denial counter cannot be
reconstructed from views — a denied agent is indistinguishable from a blocked
one — so it is accumulated by the simulation itself in the same
observability-only manner as the collision counters that `MeasureCollision`
feeds: it writes no agent state and reaches no hash.

### 16.2 Where it lands

`RunReport` gains one trailing defaulted member after `CoreAllocatedBytes`,
its nineteenth. Serialization is reflection-based camel-case
`System.Text.Json` with no converters, so the new member simply appears as a
camel-case property in the report JSON, and a round-trip test proves it.

The sampled `sim.tick` log payload gains flat aggregate camel-case fields
only — no per-agent event, no nesting, no arrays — at both payload sites,
`HeadlessRunner.LogTick` and `ArenaGame.LogTick`, each already guarded by
`IsEnabledFor` before any payload work. A disabled log call allocates
nothing, and no new `LogEvents` identifier is needed because `sim.tick`
already exists.

### 16.3 The boundary proof

A test runs the same seeded workload with metrics accumulation exercised and
ignored and requires identical state hash, event hash, outcome, and ordered
event stream: observability reaches neither hash. The existing
logging-off-versus-`trc` equality test keeps covering the logging side.

---

## 17. The scenario matrix generator

`tests/Hukbo.Core.Tests/Movement/MovementScenarioMatrix.cs` is a deterministic
combinatorial generator over the six canonical loadouts, plus self-tests that
run without ever constructing a simulation:

- **21 unordered 1v1 pairs**, enumerated with nested indices `i <= j` over
  the canonical order — fifteen distinct pairs plus six mirror cells.
  Self-tests assert the count of exactly `21`, uniqueness, canonical
  enumeration order, and the six mirrors.
- **21 unordered two-member team compositions**, the same `i <= j`
  enumeration read as teams.
- **231 team matchups**, crossing the 21 teams with team indices `i <= j` —
  210 distinct matchups plus 21 team mirrors. Self-tests assert the count of
  exactly `231`, uniqueness, order, and the 21 mirrors.

The generator is this session's deliverable. The matchup **runs** — mirrored
starts, reversed caller input, termination and progress invariants, crowded
2v2 variants, and the calibration evidence they produce — belong to the five
weapon sessions and are deliberately absent from this foundation's task list.
Every such run names its combat preset explicitly, and any cell containing a
shielded loadout selects `PrecolonialPhilippinesV2`, the only preset fielding
all six loadouts.

---

## 18. Performance budget

Measured baseline, task T0, on this machine — `Microsoft Windows 10.0.26200`,
`.NET 10.0.10`, `X64`, `processorCount 20` — with movement preset
`PersistentContingentsV4`, requested ticks `10000`, seeds `1, 2, 3, 5, 8`, and
one warm run discarded per cell:

| Combat preset | Agents | Median duration (ms) | Median p50 tick (ms) | Max allocated bytes |
| --- | ---: | ---: | ---: | ---: |
| `PrecolonialPhilippinesV2` | 200 | 376.51 | 0.06 | 699,840 |
| `PrecolonialPhilippinesV2` | 500 | 1288.79 | 0.22 | 1,417,496 |
| `PrecolonialPhilippinesV4` | 200 | 314.73 | 0.07 | 660,888 |
| `PrecolonialPhilippinesV4` | 500 | 1207.07 | 0.30 | 1,178,600 |

Derived performance budget for `EquipmentRelativeFootworkV6` on this machine
and run shape — 2.0x at 200 agents, 2.5x at 500 agents:

| Combat preset | Agents | Ceiling (ms) |
| --- | ---: | ---: |
| `PrecolonialPhilippinesV2` | 200 | 753.02 |
| `PrecolonialPhilippinesV2` | 500 | 3221.98 |
| `PrecolonialPhilippinesV4` | 200 | 629.46 |
| `PrecolonialPhilippinesV4` | 500 | 3017.68 |

Plus: **zero warm-tick bytes attributable to the new movement stages.** The
existing bounded-allocation tests — the quiet 1,000-tick window at `8_192`
bytes and the crowded windows at `16_384` bytes each — keep passing, and any
new per-tick `List`, array, lambda capture, or boxed enumerator fails the
crowded test.

**The stop rule:** if the measured budget fails, stop. Write a separate
spatial-query optimization design that compares a bounded query against the
naive oracle of section 7.4. Do not fold that optimization into tuning, do
not weaken the budget to pass, and do not ship V6 anywhere near a default —
activation stays gated under section 19.4 regardless.

---

## 19. Evidence labelling and recorded divergences

### 19.1 Labelling rules

Every load-bearing claim in any document this workstream writes carries one
of **Documented**, **Documented, form uncertain**, or **Provisional
reconstruction**. Every tuning number in code carries *Provisional
reconstruction: gameplay tuning; no historical measurement* in its XML
documentation. Player-facing cultural identifications appear only in pair
form — the Filipino name, an em dash, and a plain English descriptor — with
the evidence tier recorded in metadata and shown in the agent inspector.

### 19.2 What the research already labels

The movement research documents label, among others: Pigafetta's 1521 Mactan
account of a large cutting sword as **Documented**, while its identification
as a kampilan is **Provisional reconstruction**; the National Museum's
reading of Philippine axes as multipurpose implements as **Documented, form
uncertain**; Mactan shields as thin wood, so "hardwood" is explicitly *not* a
period fact — that negative is itself **Documented**; and every candidate
movement range, speed, and threshold in every weapon research file as
**Provisional reconstruction: gameplay tuning; no historical measurement**.

The research index also lists what is **Unknown or unsupported** and must
never be historicised in code comments, tests, or UI: fixed ranks, shield
walls, synchronised army-wide advance, formal duel rules, universal
triangular footwork, exact speeds, turn rates, engagement radii,
equipment-specific ally or enemy thresholds, and any archipelago-wide
doctrine.

### 19.3 Corrections and non-adoptions, recorded rather than silently applied

Corrections to the earlier shared plan and the weapon plans:

1. The preset is `EquipmentRelativeFootworkV6 = 6`; the plan's `5` is taken
   by `PersistentContingentsV5` (section 3).
2. The plan's task T2, the combat-default switch, describes a change that has
   already happened and moved on again — the default is
   `PrecolonialPhilippinesV4`. T2 is not executed (section 3).
3. The profile key excludes rank, because `CombatLoadout` has grown a
   `RankId` field since the plan was written (section 4.1).
4. `MovementRuleset.ContentHash` is not folded into the state hash today; the
   `contentHash` that `StateHasher` receives is the combat ruleset's. Growing
   the movement schema moves only the pinned registry literals (sections 5.2,
   14.2).
5. The footwork ratio steps are 4 and 5, not the plan's "5–6" (section 9.1).
6. Task T1 authors three trajectory fixtures — V3, V4, and V5 — not the two
   the plan expected, because V5 landed after the plan was written.
7. The deployment reassignment lands at the `BattleSimulation.Create` seam;
   `FormationPlanner` is not edited (section 12.1).
8. The weapon-session matrix runs and the asymmetric, group, and mass
   calibration — the plan's T11 — move out of this foundation and into the
   five weapon sessions.

Non-adoptions of research figures, all deliberate:

9. The forward direction band is 0 to 1 sectors, one narrower at the top than
   the Kalis and Itak research suggested (section 6.4).
10. The committed turn budget is 1 sector; the Kampilan research's `1/12`
    turn is not representable in a 16-sector model (section 6.6).
11. Shield rows carry no facing penalty; the tall-hardwood research's `0.88`
    multiplier is not representable, so `KS` and `IS` turn at the solo value
    of 2 (section 6.6).
12. The shielded disengage bands `17500` and `15000` sit outside the
    tall-hardwood research's suggested entry band (section 13.3).

Weapon-session acceptance rows that must be restated before implementation:

13. **Kampilan and Wasay**: preferred distance is not a stop line — the agent
    continues toward the target's centre (section 10.4).
14. **Kampilan and Wasay**: posture `Withdraw`/`Yield` forces the `Disengage`
    phase unconditionally; restate "cannot force synchronized retreat" as
    "routes are not synchronized" (section 9.3).
15. **Kalis**: the shielded Kalis preferred distance is `13000`, not the
    stale 1.10 sentence (section 13.3).
16. **Tall hardwood**: assert all thirteen solo-to-shielded field
    differences, not eight (section 13.3).
17. **Kalis, Itak, and tall hardwood**: the "shared engaged-entry cap" is not
    a profile field (section 13.3).

### 19.4 Activation gates

`Scenario.MovementPreset` does not change in this workstream. A later
activation decision requires all of: every automated criterion in the task
list met with recorded gate output; manual spectator review of every loadout;
100v100 and 250v250 performance inside section 18's budget; no unresolved
Critical or High review finding; explicit approval of the calibrated values;
and a separate new-default task with new golden expectations.

Rollback is operational only: explicitly select `PersistentContingentsV4`, or
leave the shipped default unchanged. Never "roll back" by editing V6 values
after its digest ships — append V7 for any later behavioural change. V1
through V6 identities, numeric enum values, profile ordering, hash fold
order, and golden fixtures remain frozen from that point on.
