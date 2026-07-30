# Wasay — Hafted Axe Movement Implementation Plan

> **Archived: reference only.** The weapon-relative movement program finished
> and every branch merged to main by 2026-07-31. Do not execute this plan; its
> task list and verification steps are historical.

> **For Codex:** REQUIRED SUB-SKILL: Use `executing-plans` to implement this plan task-by-task.

**Goal:** Add an immutable Wasay movement profile and prove that its forceful, high-commitment gameplay role responds deterministically to engagement state, ally clearance, nearby ally/enemy counts, and recovery opportunities without changing combat statistics or presenting a standardized two-handed sixteenth-century Wasay as historical fact.

**Architecture:** This is an equipment delta on the single weapon-relative movement architecture defined by [`README.md`](README.md). The shared plan owns the new append-only movement preset, fixed-point profile type and catalog, local-perception summary, movement state machine, facing representation, integration into `BattleSimulation`, hashing/snapshots, and generated scenario-matrix harness. This plan contributes one Wasay profile row and Wasay-focused tests only; it must not create a second controller, a Wasay-specific preset, a target cache, or a formation system.

**Tech Stack:** .NET 10 / C#, xUnit, Hukbo fixed-point arithmetic, deterministic headless simulation, PowerShell verification scripts

---

**Status:** Plan only. It does not authorize implementation or tuning activation.

## 1. Dependencies and execution boundary

Do not start Task W1 until the shared implementation plan at
[`README.md`](README.md) has:

1. named the append-only weapon-relative movement preset after
   `MovementPresetId.PersistentContingentsV4`;
2. created the immutable profile seam at
   `src/Hukbo.Core/Movement/LoadoutMovementProfile.cs`;
3. created the complete-loadout resolver on `MovementRuleset` and reserved
   `src/Hukbo.Core/Movement/Profiles/WasayMovementProfile.cs` for this row;
4. created the shared pure arithmetic/state-transition surface in
   `src/Hukbo.Core/Movement/WeaponMovementRules.cs`;
5. defined the authoritative phase/facing fields and their
   snapshot/state-hash treatment;
6. created the shared deterministic scenario helper at
   `tests/Hukbo.Core.Tests/Movement/MovementScenarioMatrix.cs`; and
7. fixed the meaning of every ratio, boundary, phase duration, count radius,
   ordering rule, and metric named below.

Those files are named here so the implementation has exact integration
points. If the approved shared plan names a different path, update this plan
to match before code is written; do not create both versions.

The shared work must preserve movement presets V1 through V4 byte-for-byte.
This weapon plan does not allocate or activate a preset and does not change
`Scenario.CombatPreset`.

## 2. Current behavior

The current source has no weapon-relative locomotion:

- `Scenario.MovementSpeedRaw` supplies the same human baseline to all agents;
- `MovementRuleset` contains contingent cohesion and arrival-taper settings,
  but no loadout movement profile;
- `BattleSimulation` selects targets, resolves contingent state, then proposes
  and collision-resolves movement without a weapon movement phase or
  authoritative facing;
- `AgentState` stores the loadout, target, intent, contingent state, and
  collision result, but no weapon movement phase, phase timer, or facing;
- `MovementPresetId.PersistentContingentsV4` is the shipped movement default;
  its behavior and digest must remain frozen; and
- the current V2 combat rules configure Wasay as damage `18`, reach `13`,
  cooldown `8`, and `WeaponGrip.TwoHanded`.

The last item is a gameplay configuration, not historical certainty. Museum
evidence supports Philippine axes used for work and warfare but does not
establish a standardized sixteenth-century weapon, mass distribution, grip,
or movement system named Wasay. This plan specifies movement for Hukbo's
currently configured solo Wasay role only.

## 3. Product goal and non-goals

### Goal

Make Wasay movement read as forceful but selective close-to-mid-range
commitment:

- enter through a clear lane rather than continuously pressing into bodies;
- use wider ally clearance than Kampilan under the current gameplay roles;
- remain committed long enough to be readable and counterable;
- expose a pronounced recovery in which a shorter or longer opponent can
  reposition;
- prefer an outer/supporting lane when an ally already occupies the target;
- yield toward support when locally outnumbered or attacked from several
  bearings; and
- pursue conservatively enough that a withdrawing enemy cannot pull the unit
  through a locally superior cluster.

### Non-goals

- No change to damage, reach, cooldown, combinations, hit resolution, shields,
  armor, body radius, collision policy, or target-selection semantics.
- No claim that “Wasay” denotes one standardized sixteenth-century battle axe,
  that it was two-handed, or that these movement values are measured.
- No rigid formation, commander, morale, rout, terrain, pathfinding,
  projectile, stamina, or friendly-fire system.
- No weapon-specific controller, movement preset, random stream, spatial
  cache, or per-tick allocation.
- No winner-rate balancing target. Viable pressure, readable exposure, and
  counter-entry are required; equal duel outcomes are not.
- No V1–V4 behavior, state/event hash, outcome, ordered-event, or trajectory
  change. The shared schema task owns the expected one-time update to pinned
  `MovementRuleset.ContentHash` literals.

## 4. Evidence-to-mechanic trace

| Evidence or bounded inference | Confidence | Allowed mechanic | Prohibited interpretation |
| --- | --- | --- | --- |
| The National Museum describes Philippine axes as multipurpose implements used in subsistence and warfare and cautions against colonial “head axe” / “battle axe” categories. | **Documented, form uncertain** | Keep the roster label qualified and the movement identity explicitly game-designed. | Do not infer a standardized Wasay battlefield class, doctrine, or two-handed grip. |
| General hafted-tool studies show coordinated multi-joint swings and energy transfer through a haft. | **Provisional reconstruction** | Test a readable commitment/recovery rhythm and need for clearance. | Do not convert tool-use timings, masses, or velocities into Wasay measurements. |
| Restricted-motion sports research links higher moment of inertia with lower swing speed under its study conditions. | **Provisional reconstruction** | Permit sensitivity testing of a lower committed turn budget. | Do not claim the Hukbo Wasay had the studied inertia or a historical speed value. |
| Pigafetta documents difficult footing, grouped action, staged retreat, and pursuit at Mactan. | **Documented**; encounter context, not Wasay-specific | Ensure shared states can express support-aware withdrawal and bounded pursuit. | Do not identify a Wasay at Mactan or assign this movement to a historical axe unit. |
| Hukbo currently gives Wasay the highest nominal damage and longest cooldown in the six-loadout V2 roster. | Current gameplay fact | Make entry selective and recovery exposed without changing those combat values. | Do not use game balance values as historical evidence. |

The implementation must link its class-level comments to
[`docs/research/movement/wasay.md`](../../research/movement/wasay.md) and state
that all numeric values below are provisional gameplay tuning.

## 5. Immutable candidate profile

Add exactly one catalog entry for:

```text
CombatLoadout(
    WeaponId.Wasay,
    ArmorId.LightOrganic,
    ShieldId.None)
```

Use integer ratio fields selected by the shared plan; never store `float`,
`double`, or decimal-derived authoritative state. The following names are
semantic names. Map them one-to-one to the shared type rather than adding a
parallel Wasay type.

| `LoadoutMovementProfile` property | Exact initial value | Approved calibration range | Meaning |
| --- | ---: | ---: | --- |
| `ForwardPaceBasisPoints` | `9_400` | `9_000`–`9_800` | Forward cap relative to the shared human speed. |
| `LateralPaceBasisPoints` | `7_400` | `6_500`–`8_200` | Engaged side-step cap. |
| `BackwardPaceBasisPoints` | `6_400` | `5_500`–`7_200` | Controlled reverse cap while facing the threat. |
| `CommittedPaceBasisPoints` | `2_500` | `2_000`–`3_500` | Same-tick cap while committed. |
| `PreferredDistanceBasisPoints` | `10_800` | `10_000`–`12_000` | Multiplier on the selected combat profile's reach. |
| `OpponentDistanceOffsetBasisPoints` | `[500, 0, 250, 500, 250, 500]` | each `-2_000`–`2_000` | Spacing adjustment versus `KP, WA, KA, IT, KS, IS`. |
| `MaximumFacingStepsPerTick` | `1` of 16 | `1`–`2` | Normal authoritative turn cap. |
| `CommittedFacingStepsPerTick` | `1` of 16 | `1` | Committed authoritative turn cap. |
| `AccelerationBasisPointsPerTick` | `4_000` | `3_000`–`6_000` | Retained scalar pace rise per tick. |
| `DecelerationBasisPointsPerTick` | `5_000` | `3_500`–`7_000` | Retained scalar pace fall per tick. |
| `CommitmentTicks` | `4` | `3`–`5` | Minimum uninterrupted committed phase. |
| `RecoveryTicks` | `4` | `3`–`5` | Minimum recovery before another commitment. |
| `AllyClearanceBodyDiametersBasisPoints` | `17_500` | `15_000`–`20_000` | Ally lane-clearance radius. |
| `DisengageEnemyToAllyBasisPoints` | `20_000` | `15_000`–`20_000` | Local hostile pressure that enters disengagement. |
| `ReengageEnemyToAllyBasisPoints` | `12_500` | `10_000`–`15_000` | Lower release threshold for hysteresis. |
| `PursuitSupportBodyDiametersBasisPoints` | `10_000` | `8_000`–`12_500` | Required local support distance for pursuit. |

The common human speed remains the only base speed. Every multiplier is at
most one, so the profile cannot make Wasay faster than another human. Counts
change posture and destination choice, not base speed.

Profile construction must reject zero-or-negative durations, basis-point
values outside the shared allowed interval, a release ratio
that is not strictly below the entry ratio, or products that can overflow the
shared fixed-point comparison domain.

## 6. Weapon-specific state and threshold behavior

Use the shared movement phases and priority ordering. The following are Wasay
inputs and assertions, not a separate state machine.

### Boundary semantics

- **Preferred engagement distance:** squared distance exactly equal to the
  fixed-point representation of `108/100 × AttackRangeRaw` enters `Engage`
  and stops ordinary forward approach. It does not enter `Commit`; the
  unchanged combat reach/cooldown gates do that. The shared comparison must be
  `<=`; do not take a square root or round through floating point.
- **Clearance:** an ally whose center is exactly at the clearance radius does
  not deny entry if the shared contract defines intrusion as `< radius`; an
  ally one raw unit inside does. This equality convention must match the
  shared README and all weapons. If the shared README selects inclusive
  clearance instead, update this sentence and the test before implementation.
- **Disengage entry:** exact `2:1` local hostile-to-ally pressure enters
  disengagement.
- **Disengage hold:** values strictly between `5:4` and `2:1` preserve the
  previous disengaged/non-disengaged state.
- **Disengage release:** exact `5:4` pressure releases disengagement.
- **Counts:** the acting Wasay unit counts as one local ally; dead agents and
  agents outside the shared local-perception boundary count as neither; an
  enemy exactly on that boundary follows the shared inclusive boundary.
- **Zero hostiles:** never enters or remains in disengagement on ratio arithmetic
  alone. Avoid division; compare widened integer cross-products.
- **Phase duration:** an accepted attack starts four whole committed ticks
  including the attack tick; commitment movement caps begin on the following
  tick. Four whole recovery ticks follow. Local
  danger may suppress forward travel or redirect the unit, but only a later
  attack accepted by the unchanged combat gates may interrupt recovery and
  start a fresh commitment.

### Count-sensitive posture

| Local living relationship | Wasay behavior |
| --- | --- |
| No perceived hostile | Use shared contingent movement; do not manufacture a weapon engagement state. |
| Fewer than `5:4` hostiles per ally | May take a supported, clear-lane entry or conservative pursuit opportunity. |
| Exactly `5:4` | Release existing disengagement; otherwise remain non-disengaged. |
| Between `5:4` and `2:1` | Hysteresis band: preserve prior disengagement membership. A non-disengaged unit may hold but gains no speed or shortened recovery. |
| Exactly or above `2:1` | Enter disengagement after any non-cancellable shared safety/commitment rule; move toward the stable allied support reference instead of deeper enemy density. |
| Globally advantaged but locally `>= 2:1` hostile | Disengage locally; faction totals cannot authorize a deep charge. |
| Globally disadvantaged but locally favorable | May exploit a bounded ally-created opening; faction totals can bias caution but cannot force every Wasay unit to retreat in sync. |

Two Wasay allies have no special pair state. Actual positions and the wider
clearance radius delay the second entry when the first occupies the lane.
Mixed allies retain their own profiles; a shorter ally engaging the target can
create an outer-lane opportunity but cannot become a hard-coded “tank.”

## 7. Matchup and count behavior to prove

All checks use deterministic seeds and the shared matrix harness. They are
movement/counterplay assertions, not historical reenactments or demanded win
rates.

| Opponent/loadout | Required Wasay behavior | Reject the candidate if |
| --- | --- | --- |
| Kampilan, solo | Wasay can cross the longer reach after a Kampilan commitment or lateral displacement, with exposure on entry. | Wasay never closes or always closes without a punishable interval. |
| Wasay, solo | Equal profiles make small lane adjustments and stagger commitments. | They circle permanently, commit head-on in a fixed collision loop, or never reach contact. |
| Kalis, solo | Equal nominal reach remains meaningful while Wasay's longer recovery permits repositioning. | Wasay's damage identity overwhelms every exchange, or Kalis only survives by endless orbiting. |
| Kalis + tall hardwood | Movement is driven by geometry and counts, not an inferred directional shield opening. | Mirroring shield bearing changes authoritative movement, or shield presence grants speed. |
| Itak, solo | Wasay can deny an uncontested straight entry but must expose a crossing/reset opportunity during recovery. | Itak has no route in, or Wasay can never restore separation after a crossing. |
| Itak + tall hardwood | Congestion can redirect the lane, but the shield itself adds no locomotion rule. | Shield causes endless Wasay retreat or a movement-speed advantage. |

Count suites:

- **1v2, 2v3, 3v5:** exact and adjacent threshold fixtures prove disengagement
  equality/hysteresis independently of outcomes.
- **2v2 homogeneous:** two Wasay allies stagger commitment because of
  clearance and recovery, not hard-coded alternating turns.
- **2v2 mixed:** Wasay uses a separate outer/support lane without cutting
  through the shorter ally's approach.
- **4v4, 5v5, 8v8:** local density and bearing spread decide entry; roster
  totals do not.
- **100v100 and 250v250:** global posture may bias pressure/hold/conserve, but
  a locally exposed Wasay still refuses a deep pursuit.

## 8. Granular TDD implementation tasks

### Task W1: Pin the Wasay profile row

**Files:**

- Create: `tests/Hukbo.Core.Tests/Movement/WasayMovementProfileTests.cs`
- Create: `src/Hukbo.Core/Movement/Profiles/WasayMovementProfile.cs`

**Step 1: Write the failing facts.**

Add `WasayProfileUsesApprovedProvisionalValues` and
`WasayProfileExportsApprovedLoadoutKey`. Assert every basis-point field,
duration, opponent-offset cell, and the exact
`CombatLoadout(Wasay, LightOrganic, None)` key. Also assert there is no
Wasay+tall-hardwood fallback.

**Step 2: Run the focused test and record the expected failure.**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release --filter FullyQualifiedName~WasayMovementProfileTests
```

Expected before implementation: the exported Wasay row is absent or the exact
value assertion fails. A compile failure is acceptable only if the shared catalog
skeleton has not yet landed; in that case stop and complete the dependency
rather than inventing a local catalog.

**Step 3: Add only the immutable catalog entry.**

Export the exact row in section 5 for the shared owner to compose during T4.
Do not require registry resolution in this equipment-owned task. Add XML
documentation that calls the values provisional gameplay tuning and links the
research PRD. Do not alter another weapon row.

**Step 4: Re-run the focused test.**

Expected: all `WasayMovementProfileTests` pass.

**Step 5: Commit.**

```powershell
git add src/Hukbo.Core/Movement/Profiles/WasayMovementProfile.cs tests/Hukbo.Core.Tests/Movement/WasayMovementProfileTests.cs
git commit -m "feat(movement): add wasay movement profile"
```

### Task W2: Pin entry, clearance, turn, and phase boundaries

**Files:**

- Create: `tests/Hukbo.Core.Tests/Movement/WasayMovementTests.cs`
- Shared defects: hand off a failing test to the shared foundation owner; this
  equipment task does not edit shared runtime files.

**Step 1: Write table-driven failing tests.**

Cover:

1. entry at one raw unit outside, exact equality, and one raw unit inside;
2. clearance at one raw unit outside, exact equality, and one raw unit
   inside, using the shared convention from section 6;
3. forward, lateral, and backward step caps at 94%, 74%, and 64%;
4. normal and committed facing capped at one `Facing16` step;
5. acceleration/deceleration use `4_000`/`5_000` basis points per tick;
6. collision denial clamps retained pace to actual movement;
7. exactly four committed ticks followed by exactly four recovery ticks;
8. an attack accepted during recovery interrupts recovery and starts a fresh
   four-tick commitment without changing combat eligibility; and
9. every resulting step is `<= AgentState.MovementSpeedRaw`.

Use integer inputs chosen to divide evenly by `10_000`, then add non-divisible
cases that prove truncation toward zero.

**Step 2: Run the focused test.**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release --filter FullyQualifiedName~WasayMovementTests
```

Expected before implementation: profile-neutral shared rules produce the
wrong cap/phase, or the Wasay-specific test cases are not yet wired.

**Step 3: Hand off any demonstrated shared-rule defect.**

If the profile row alone makes the tests pass, continue. If a boundary is
wrong for every profile, stop this task and give the failing test to the shared
foundation owner. Resume only after the shared rule fix and its tests land.

**Step 4: Re-run focused and shared rule tests.**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release --filter "FullyQualifiedName~WasayMovementTests|FullyQualifiedName~WeaponMovementRulesTests"
```

Expected: pass.

**Step 5: Commit.**

```powershell
git add tests/Hukbo.Core.Tests/Movement/WasayMovementTests.cs
git commit -m "test(movement): pin wasay movement boundaries"
```

Omit the source path from `git add` when it did not change.

### Task W3: Pin local-count hysteresis and stable ally support

**Files:**

- Modify: `tests/Hukbo.Core.Tests/Movement/WasayMovementTests.cs`
- Shared defects: hand off the failing test; do not edit shared rules or
  `BattleSimulation` from this task.

**Step 1: Add failing pure tests.**

Assert disengagement entry at exact `2:1`, release at exact `5:4`, previous-state
preservation between those boundaries, self-inclusion, dead/out-of-radius
exclusion, zero-hostile behavior, and integer cross-product overflow safety.

**Step 2: Add pure-order and integration tests.**

Pass explicitly permuted candidate spans to the shared pure query. Place two
eligible allied support references at equal distance and assert the lower
stable entity ID is selected. Separately reverse caller input through
`CreateForTesting` and assert its documented canonicalization produces the
same ordered events/state hash. Also assert a global faction advantage does
not override a local `2:1` hostile ratio.

**Step 3: Run the focused test.**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release --filter FullyQualifiedName~WasayMovementTests
```

Expected before implementation: equality, hysteresis, or stable-order
assertion fails.

**Step 4: Hand off only through the shared implementation seam.**

Do not add a Wasay branch to `BattleSimulation`. If shared wiring is absent,
stop and hand the failing test to the shared owner, who feeds the catalog
profile into common count/state functions.

**Step 5: Re-run focused tests and determinism tests.**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release --filter "FullyQualifiedName~WasayMovementTests|FullyQualifiedName~DeterminismTests"
```

Expected: pass.

**Step 6: Commit.**

```powershell
git add tests/Hukbo.Core.Tests/Movement/WasayMovementTests.cs
git commit -m "test(movement): cover wasay count transitions"
```

Stage only paths actually changed.

### Task W4: Add the six-loadout duel and 2v2 calibration slice

**Files:**

- Modify: `tests/Hukbo.Core.Tests/Movement/WasayMovementTests.cs`

The shared integration owner supplies `MovementScenarioMatrix`. If a generic
metric is missing, hand the requirement back rather than editing that file.

**Step 1: Add generated matchup cases.**

Generate the six 1v1 opponents from the approved combat roster rather than
hand-maintaining a list. Mirror start sides/bearings. Add homogeneous and
mixed 2v2 cases in which Wasay is present.

**Step 2: Assert bounded movement signals.**

Record, without changing authoritative state:

- ticks to first entry and first attack;
- commitment and recovery counts;
- denied commitments by ally clearance;
- time beyond preferred engagement distance;
- collision-denied moves;
- disengagement entries/releases and phase flips; and
- maximum no-progress streak.

Assert the failure modes in section 7 are absent. Do not assert exact winners
or equal win percentages. Any numeric acceptance bound not already fixed in
the shared README must be reported first, reviewed, and then added to this
plan; do not smuggle a threshold into a test.

**Step 3: Run the focused suite.**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release --filter FullyQualifiedName~WasayMovementTests
```

Expected before implementation: at least one baseline V4 run demonstrates
profile-neutral motion or a listed recovery/counter-entry failure; retain the
captured numbers in the implementation report.

**Step 4: Tune only within section 5 ranges, one field at a time.**

Re-run the exact same seed set after each change. If no in-range value passes,
reject the candidate and return to product review; do not change combat stats
or widen a range silently.

**Step 5: Commit passing tests or the evidence-backed profile adjustment.**

```powershell
git add tests/Hukbo.Core.Tests/Movement/WasayMovementTests.cs src/Hukbo.Core/Movement/Profiles/WasayMovementProfile.cs
git commit -m "test(movement): calibrate wasay matchups"
```

Stage only paths actually changed.

### Task W5: Prove asymmetric, mixed-group, mass, and replay behavior

**Files:**

- Modify: `tests/Hukbo.Core.Tests/Movement/WasayMovementTests.cs`

The shared integration owner alone edits preset fixtures and registry tests.
This task consumes those tests but does not update their expected values.

**Step 1: Add scenario coverage.**

Use `MovementScenarioMatrix` to run Wasay-present 1v2, 2v3, 3v5, 4v4, 5v5,
8v8, 100v100, and 250v250 scenarios. Include:

- globally favorable but locally outnumbered placement;
- globally unfavorable but locally supported placement;
- homogeneous Wasay congestion; and
- a mixed roster where a shorter weapon already occupies the direct lane.

**Step 2: Assert invariants.**

Same seed/build/commands must produce identical outcome, state hash, event
hash, and ordered events. V1-V4 fixture results must remain byte-identical.
The new preset must show local disengagement decisions in both global-posture
directions, no step above baseline, bounded recovery/state flipping, and no
scenario that reaches the tick limit solely because all living units are
orbiting or disengaging.

**Step 3: Run focused and freeze suites.**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release --filter "FullyQualifiedName~WasayMovementTests|FullyQualifiedName~MovementPresetFreezeTests|FullyQualifiedName~MovementPresetRegistryTests"
```

Expected: the shared owner has recorded the built V5 fixture/hash, and all
focused and shared tests pass. Any V1-V4 mismatch is a blocking regression,
not a fixture-update opportunity.

**Step 4: Run the mass workloads.**

```powershell
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1 -Preset PrecolonialPhilippinesV3 -MovementPreset EquipmentRelativeFootworkV5
./scripts/benchmark.ps1 -Agents 500 -Ticks 10000 -Seed 1 -Preset PrecolonialPhilippinesV3 -MovementPreset EquipmentRelativeFootworkV5
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1 -Preset PrecolonialPhilippinesV3 -MovementPreset PersistentContingentsV4
./scripts/benchmark.ps1 -Agents 500 -Ticks 10000 -Seed 1 -Preset PrecolonialPhilippinesV3 -MovementPreset PersistentContingentsV4
```

The first is 100v100; the second is 250v250. Capture elapsed time, hashes,
winner, and the shared non-authoritative movement metrics. Compare against
the same scenario on V4. The shared plan sets the performance budget; this
weapon plan must not invent a relaxed one.

**Step 5: Run the canonical gate.**

```powershell
./scripts/verify.ps1
```

Expected: locked restore, format verification, Release build, Core and
GPU-independent Client tests, and the canonical 200-agent/10,000-tick/seed-1
determinism workload all pass. Record the real output in the implementation
report.

**Step 6: Commit.**

```powershell
git add tests/Hukbo.Core.Tests/Movement/WasayMovementTests.cs
git commit -m "test(movement): verify wasay at battle scale"
```

Stage only paths actually changed. The shared preset owner, not this weapon
task, owns a new digest fixture.

## 9. Calibration acceptance and rejection

Accept the initial Wasay profile only when all are true:

- exact boundary and duration tests pass;
- Wasay crosses Kampilan's reach in at least one approved reproducible
  post-commitment or lateral-entry condition, but not without exposure in
  every condition;
- Kalis and Itak each obtain at least one reproducible recovery-entry or
  repositioning opportunity;
- mirrored Wasay duels have bounded no-progress streaks and do not settle into
  permanent orbiting, head-on collision, or simultaneous retreat;
- two Wasay allies experience clearance-delayed/staggered commitments in the
  constructed congested case;
- Wasay has a measurably longer recovery occupancy than Kampilan under
  otherwise equivalent calibration geometry, using the shared metric;
- local `2:1` pressure overrides a favorable faction total, while a locally
  favorable pocket is not forced into synchronized retreat by an unfavorable
  faction total;
- shielded opponents receive no directional or speed effect from this plan;
- no movement step exceeds the common human baseline;
- replay/hash/order assertions pass; and
- the shared performance budget and canonical gate pass.

Reject and return to product review when any are true:

- passing requires changing damage, reach, cooldown, hit rules, collision, or
  another weapon's values;
- a value must move outside its approved range;
- Wasay can never cross Kampilan reach, or shorter weapons have no recovery
  opening across the approved seeds/conditions;
- recovery or clearance makes Wasay inert in ordinary supported engagements;
- the profile produces unbounded oscillation, pursuit, disengagement, or no-progress
  behavior;
- a global count overrides an explicitly locally unsafe geometry;
- V1–V4 behavior, state/event hashes, outcomes, events, or trajectories
  change beyond the declared ruleset-literal update; or
- results depend on array order, floating point, wall clock, or a new random
  draw.

## 10. Activation and rollback

Wasay cannot be activated independently. Its row is inert unless the shared
append-only weapon-relative movement preset is selected. Activation requires:

1. all weapon and shield profile plans implemented;
2. the complete 21-pair 1v1 and 231-composition 2v2 matrix reviewed;
3. asymmetric and mass scenarios passing;
4. new preset hash/digest pinned from built output;
5. V1-V4 freeze tests unchanged;
6. `./scripts/verify.ps1` passing; and
7. the manual checklist in `docs/development/testing.md` completed or reported
   honestly as `PENDING`/`BLOCKED`.

This plan leaves `Scenario.MovementPreset` at
`MovementPresetId.PersistentContingentsV4`. A later, separately approved
activation task may change that default after every gate above passes.
Rollback for an opt-in run selects V4; it does not delete or renumber the new
preset, mutate its profile, or rewrite a recorded fixture.
`Scenario.CombatPreset` activation remains the separate approved V2-to-V3
shared task.

## 11. References

- Shared architecture and cross-weapon sequence:
  [`docs/plans/movement/README.md`](README.md)
- Research PRD and source ledger:
  [`docs/research/movement/wasay.md`](../../research/movement/wasay.md)
- Research program scope and matchup matrix:
  [`docs/research/movement/README.md`](../../research/movement/README.md)
- Current movement configuration:
  `src/Hukbo.Core/Movement/MovementRuleset.cs`
- Current simulation integration:
  `src/Hukbo.Core/Simulation/BattleSimulation.cs`
- Current authoritative agent state:
  `src/Hukbo.Core/Simulation/AgentState.cs`
