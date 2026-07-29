# Kampilan — Great Blade Movement Implementation Plan

> **For Codex:** REQUIRED SUB-SKILL: Use `executing-plans` to implement this plan task-by-task.

**Goal:** Add an immutable Kampilan movement profile and prove that its long-reach, lane-sensitive gameplay role responds deterministically to engagement state, nearby ally/enemy counts, and congestion without changing combat statistics or making historical claims about a two-handed Kampilan system.

**Architecture:** This is an equipment delta on the single weapon-relative movement architecture defined by [`README.md`](README.md). The shared plan owns the new append-only movement preset, fixed-point profile type and catalog, local-perception summary, movement state machine, facing representation, integration into `BattleSimulation`, hashing/snapshots, and generated scenario-matrix harness. This plan contributes one Kampilan profile row and Kampilan-focused tests only; it must not create a second controller, a Kampilan-specific preset, a target cache, or a formation system.

**Tech Stack:** .NET 10 / C#, xUnit, Hukbo fixed-point arithmetic, deterministic headless simulation, PowerShell verification scripts

---

**Status:** Plan only. It does not authorize implementation or tuning activation.

## 1. Dependencies and execution boundary

Do not start Task K1 until the shared implementation plan at
[`README.md`](README.md) has:

1. named the append-only weapon-relative movement preset after
   `MovementPresetId.PersistentContingentsV4`;
2. created the immutable profile seam at
   `src/Hukbo.Core/Movement/LoadoutMovementProfile.cs`;
3. created the complete-loadout resolver on `MovementRuleset` and reserved
   `src/Hukbo.Core/Movement/Profiles/KampilanMovementProfile.cs` for this row;
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
- the current V2 combat rules configure Kampilan as damage `15`, reach `16`,
  cooldown `7`, and `WeaponGrip.TwoHanded`.

The last item is a gameplay configuration, not historical certainty. The
National Museum of the Philippines instead describes kampilan among
single-handed blades that can be used with a shield. This plan does not decide
whether Hukbo should change that loadout. It specifies movement for the
currently configured solo Kampilan only.

## 3. Product goal and non-goals

### Goal

Make Kampilan movement read as deliberate long-reach lane control:

- preserve an outer engagement distance without infinite kiting;
- require a clear entry lane and useful ally spacing;
- stagger commitment when another ally already occupies that lane;
- complete a short commitment and recovery rather than re-evaluating into
  jitter every tick;
- yield toward nearby support when locally outnumbered; and
- pursue only while local geometry and counts leave a viable exit or support
  reference.

### Non-goals

- No change to damage, reach, cooldown, combinations, hit resolution, shields,
  armor, body radius, collision policy, or target-selection semantics.
- No claim that a two-handed Kampilan or these movement values are documented
  sixteenth-century practice.
- No rigid formation, commander, morale, rout, terrain, pathfinding,
  projectile, stamina, or friendly-fire system.
- No weapon-specific controller, movement preset, random stream, spatial
  cache, or per-tick allocation.
- No winner-rate balancing target. Role viability and counterplay are the
  acceptance criteria, not equal duel outcomes.
- No V1–V4 behavior, state/event hash, outcome, ordered-event, or trajectory
  change. The shared schema task owns the expected one-time update to pinned
  `MovementRuleset.ContentHash` literals.

## 4. Evidence-to-mechanic trace

| Evidence or bounded inference | Confidence | Allowed mechanic | Prohibited interpretation |
| --- | --- | --- | --- |
| Pigafetta documents a large unnamed sword, grouped action, evasive lateral movement, staged withdrawal, and pursuit at Mactan. | **Documented**, but not Kampilan-specific footwork | Ensure the shared state model can express lateral reset, controlled withdrawal, and bounded pursuit. | Do not call these a Kampilan doctrine or assign Pigafetta's great sword to a named wielder. |
| Museum evidence identifies later kampilan forms as long blades and describes single-handed/shield-compatible use. | **Documented, form uncertain**; applicability to the depicted period remains unresolved | Retain long-reach lane control as a gameplay role; label the current solo/two-handed restriction as configuration. | Do not describe the planned turn or speed limits as historically measured two-handed handling. |
| Longer reach generally rewards threatening from the outer edge and preserving separation. | **Provisional reconstruction** | Preferred engagement distance, shallow commitment, lateral recovery, cautious pursuit. | Do not turn reach into guaranteed denial of shorter weapons. |
| Multiple committed long implements need usable space in the current collision model. | **Provisional reconstruction** | Ally-clearance gate and staggered commitment. | Do not add friendly damage, phasing, or a rigid Kampilan formation. |
| Local hostile pressure from multiple bearings makes a committed lane fragile. | **Provisional reconstruction** | Count-triggered disengagement and refusal of unsafe deep entry. | Do not implement morale or faction-total panic. |

The implementation must link its class-level comments to
[`docs/research/movement/kampilan.md`](../../research/movement/kampilan.md) and
state that all numeric values below are provisional gameplay tuning.

## 5. Immutable candidate profile

Add exactly one catalog entry for:

```text
CombatLoadout(
    WeaponId.Kampilan,
    ArmorId.LightOrganic,
    ShieldId.None)
```

Use integer ratio fields selected by the shared plan; never store `float`,
`double`, or decimal-derived authoritative state. The following names are
semantic names. Map them one-to-one to the shared type rather than adding a
parallel Kampilan type.

| `LoadoutMovementProfile` property | Exact initial value | Approved calibration range | Meaning |
| --- | ---: | ---: | --- |
| `ForwardPaceBasisPoints` | `9_800` | `9_500`–`10_000` | Forward cap relative to the shared human speed. |
| `LateralPaceBasisPoints` | `8_200` | `7_500`–`9_000` | Engaged side-step cap. |
| `BackwardPaceBasisPoints` | `7_000` | `6_000`–`7_800` | Controlled reverse cap while facing the threat. |
| `CommittedPaceBasisPoints` | `3_000` | `2_500`–`4_000` | Same-tick cap while committed. |
| `PreferredDistanceBasisPoints` | `11_500` | `10_500`–`12_500` | Multiplier on the selected combat profile's reach. |
| `OpponentDistanceOffsetBasisPoints` | `[0, 0, 250, 500, 250, 500]` | each `-2_000`–`2_000` | Spacing adjustment versus `KP, WA, KA, IT, KS, IS`. |
| `MaximumFacingStepsPerTick` | `2` of 16 | `1`–`3` | Normal authoritative turn cap. |
| `CommittedFacingStepsPerTick` | `1` of 16 | `1`–`2` | Committed authoritative turn cap. |
| `AccelerationBasisPointsPerTick` | `5_000` | `3_500`–`7_000` | Retained scalar pace rise per tick. |
| `DecelerationBasisPointsPerTick` | `6_000` | `4_000`–`8_000` | Retained scalar pace fall per tick. |
| `CommitmentTicks` | `3` | `2`–`4` | Minimum uninterrupted committed phase. |
| `RecoveryTicks` | `3` | `2`–`4` | Minimum recovery before another commitment. |
| `AllyClearanceBodyDiametersBasisPoints` | `15_000` | `12_500`–`17_500` | Ally lane-clearance radius. |
| `DisengageEnemyToAllyBasisPoints` | `20_000` | `15_000`–`20_000` | Local hostile pressure that enters disengagement. |
| `ReengageEnemyToAllyBasisPoints` | `12_500` | `10_000`–`15_000` | Lower release threshold for hysteresis. |
| `PursuitSupportBodyDiametersBasisPoints` | `12_500` | `10_000`–`15_000` | Required local support distance for pursuit. |

The common human speed remains the only base speed. Every multiplier is at
most one, so the profile cannot make Kampilan faster than another human. Count
relationships change posture and destination choice, not base speed.

Profile construction must reject zero-or-negative durations, basis-point
values outside the shared allowed interval, a release ratio
that is not strictly below the entry ratio, or products that can overflow the
shared fixed-point comparison domain.

## 6. Weapon-specific state and threshold behavior

Use the shared movement phases and priority ordering. The following are
Kampilan inputs and assertions, not a separate state machine.

### Boundary semantics

- **Preferred engagement distance:** squared distance exactly equal to the
  fixed-point representation of `115/100 × AttackRangeRaw` enters `Engage`
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
- **Counts:** the acting Kampilan unit counts as one local ally; dead agents
  and agents outside the shared local-perception boundary count as neither;
  an enemy exactly on that boundary follows the shared inclusive boundary.
- **Zero hostiles:** never enters or remains in disengagement on ratio arithmetic
  alone. Avoid division; compare widened integer cross-products.
- **Phase duration:** an accepted attack starts three whole committed ticks
  including the attack tick; commitment movement caps begin on the following
  tick. Three whole recovery ticks follow. Local
  count changes may change destination or suppress forward travel, but only a
  later attack accepted by the unchanged combat gates may interrupt recovery
  and start a fresh commitment.

### Count-sensitive posture

| Local living relationship | Kampilan behavior |
| --- | --- |
| No perceived hostile | Use shared contingent movement; do not manufacture a weapon engagement state. |
| Fewer than `5:4` hostiles per ally | May approach or pursue, subject to lane clearance and the global contingent posture. |
| Exactly `5:4` | Release existing disengagement; otherwise remain non-disengaged. |
| Between `5:4` and `2:1` | Hysteresis band: preserve prior disengagement membership. A non-disengaged unit may hold or shallow-approach but must not acquire a speed bonus. |
| Exactly or above `2:1` | Enter disengagement after any non-cancellable shared safety/commitment rule; choose the stable allied support reference rather than a deeper enemy destination. |
| Globally advantaged but locally `>= 2:1` hostile | Disengage locally; faction totals cannot override local safety. |
| Globally disadvantaged but locally favorable | May take a bounded local opportunity; faction totals may make posture conservative but cannot force synchronized retreat. |

For multiple allies, lane occupancy comes from actual positions and the
shared stable scan. Two Kampilan units must not gain a special pairing state:
the second unit's entry is delayed only when the first living ally occupies
its clearance lane. Mixed allies keep their own profiles.

## 7. Matchup and count behavior to prove

All checks use deterministic seeds and the shared matrix harness. They are
movement/counterplay assertions, not historical reenactments or demanded win
rates.

| Opponent/loadout | Required Kampilan behavior | Reject the candidate if |
| --- | --- | --- |
| Kampilan, solo | Equal-reach units make bounded lateral adjustments and separate after commitment. | They orbit without contact until the tick limit, remain in mutual retreat, or repeatedly collide in the same lane. |
| Wasay, solo | Kampilan can preserve its reach margin, while Wasay has at least one reproducible post-commitment/lateral entry condition. | Kampilan kites forever or Wasay closes without exposure in every seed. |
| Kalis, solo | Shallow entry and recovery preserve the reach identity; Kalis can cross inside under at least one reproducible condition. | Kalis never enters or wins only through an unbounded orbit. |
| Kalis + tall hardwood | Movement is driven by geometry and counts, not a directional shield gap. | Mirroring shield bearing changes authoritative movement, or a shield creates a speed bonus. |
| Itak, solo | Kampilan turns toward pressure without extra speed; Itak can enter during a recovery/second-bearing opportunity. | Kampilan perfectly excludes Itak or becomes unable to reset after one crossing. |
| Itak + tall hardwood | Congestion can change the route, but shield presence itself does not. | Kampilan disengages forever or treats the shield as directional cover. |

Count suites:

- **1v2, 2v3, 3v5:** exact and one-raw-unit threshold fixtures prove disengagement
  equality/hysteresis independently of duel outcomes.
- **2v2 homogeneous:** two Kampilan allies stagger entry from geometry; no
  synchronized hard-coded turn-taking.
- **2v2 mixed:** Kampilan uses an available outer lane without forcing the
  shorter ally to copy its spacing.
- **4v4, 5v5, 8v8:** local counts, not roster totals, decide whether an
  isolated Kampilan disengages.
- **100v100 and 250v250:** global posture may bias pressure/hold/conserve, but
  a locally outnumbered Kampilan still refuses a deep chase.

## 8. Granular TDD implementation tasks

### Task K1: Pin the Kampilan profile row

**Files:**

- Create: `tests/Hukbo.Core.Tests/Movement/KampilanMovementProfileTests.cs`
- Create: `src/Hukbo.Core/Movement/Profiles/KampilanMovementProfile.cs`

**Step 1: Write the failing facts.**

Add `KampilanProfileUsesApprovedProvisionalValues` and
`KampilanProfileExportsApprovedLoadoutKey`. Assert every basis-point field,
duration, opponent-offset cell, and the exact
`CombatLoadout(Kampilan, LightOrganic, None)` key. Also assert there is no
Kampilan+tall-hardwood fallback.

**Step 2: Run the focused test and record the expected failure.**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release --filter FullyQualifiedName~KampilanMovementProfileTests
```

Expected before implementation: the exported Kampilan row is absent or the
exact value assertion fails. A compile failure is acceptable only if the shared
catalog skeleton has not yet landed; in that case stop and complete the
dependency rather than inventing a local catalog.

**Step 3: Add only the immutable catalog entry.**

Export the exact row in section 5 for the shared owner to compose during T4.
Do not require registry resolution in this equipment-owned task. Add XML
documentation that calls the values provisional gameplay tuning and links the
research PRD. Do not alter another weapon row.

**Step 4: Re-run the focused test.**

Expected: all `KampilanMovementProfileTests` pass.

**Step 5: Commit.**

```powershell
git add src/Hukbo.Core/Movement/Profiles/KampilanMovementProfile.cs tests/Hukbo.Core.Tests/Movement/KampilanMovementProfileTests.cs
git commit -m "feat(movement): add kampilan movement profile"
```

### Task K2: Pin entry, clearance, turn, and phase boundaries

**Files:**

- Create: `tests/Hukbo.Core.Tests/Movement/KampilanMovementTests.cs`
- Shared defects: hand off a failing test to the shared foundation owner; this
  equipment task does not edit shared runtime files.

**Step 1: Write table-driven failing tests.**

Cover:

1. entry at one raw unit outside, exact equality, and one raw unit inside;
2. clearance at one raw unit outside, exact equality, and one raw unit
   inside, using the shared convention from section 6;
3. forward, lateral, and backward step caps at 98%, 82%, and 70%;
4. normal facing capped at two `Facing16` steps and committed facing at one;
5. acceleration/deceleration use `5_000`/`6_000` basis points per tick;
6. collision denial clamps retained pace to actual movement;
7. exactly three committed ticks followed by exactly three recovery ticks;
8. an attack accepted during recovery interrupts recovery and starts a fresh
   three-tick commitment without changing combat eligibility; and
9. every resulting step is `<= AgentState.MovementSpeedRaw`.

Use integer inputs chosen to divide evenly by `10_000`, then add non-divisible
cases that prove truncation toward zero.

**Step 2: Run the focused test.**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release --filter FullyQualifiedName~KampilanMovementTests
```

Expected before implementation: profile-neutral shared rules produce the
wrong cap/phase, or the Kampilan-specific test cases are not yet wired.

**Step 3: Hand off any demonstrated shared-rule defect.**

If the profile row alone makes the tests pass, continue. If a boundary is
wrong for every profile, stop this task and give the failing test to the shared
foundation owner. Resume only after the shared rule fix and its tests land.

**Step 4: Re-run focused and shared rule tests.**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release --filter "FullyQualifiedName~KampilanMovementTests|FullyQualifiedName~WeaponMovementRulesTests"
```

Expected: pass.

**Step 5: Commit.**

```powershell
git add tests/Hukbo.Core.Tests/Movement/KampilanMovementTests.cs
git commit -m "test(movement): pin kampilan movement boundaries"
```

Omit the source path from `git add` when it did not change.

### Task K3: Pin local-count hysteresis and stable ally support

**Files:**

- Modify: `tests/Hukbo.Core.Tests/Movement/KampilanMovementTests.cs`
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
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release --filter FullyQualifiedName~KampilanMovementTests
```

Expected before implementation: equality, hysteresis, or stable-order
assertion fails.

**Step 4: Hand off only through the shared implementation seam.**

Do not add a Kampilan branch to `BattleSimulation`. If shared wiring is absent,
stop and hand the failing test to the shared owner, who feeds the catalog
profile into common count/state functions.

**Step 5: Re-run focused tests and determinism tests.**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release --filter "FullyQualifiedName~KampilanMovementTests|FullyQualifiedName~DeterminismTests"
```

Expected: pass.

**Step 6: Commit.**

```powershell
git add tests/Hukbo.Core.Tests/Movement/KampilanMovementTests.cs
git commit -m "test(movement): cover kampilan count transitions"
```

Stage only paths actually changed.

### Task K4: Add the six-loadout duel and 2v2 calibration slice

**Files:**

- Modify: `tests/Hukbo.Core.Tests/Movement/KampilanMovementTests.cs`

The shared integration owner supplies `MovementScenarioMatrix`. If a generic
metric is missing, hand the requirement back rather than editing that file.

**Step 1: Add generated matchup cases.**

Generate the six 1v1 opponents from the approved combat roster rather than
hand-maintaining a list. Mirror start sides/bearings. Add homogeneous and
mixed 2v2 cases in which Kampilan is present.

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
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release --filter FullyQualifiedName~KampilanMovementTests
```

Expected before implementation: at least one baseline V4 run demonstrates
profile-neutral motion or a listed stalemate/counter-entry failure; retain the
captured numbers in the implementation report.

**Step 4: Tune only within section 5 ranges, one field at a time.**

Re-run the exact same seed set after each change. If no in-range value passes,
reject the candidate and return to product review; do not change combat stats
or widen a range silently.

**Step 5: Commit passing tests or the evidence-backed profile adjustment.**

```powershell
git add tests/Hukbo.Core.Tests/Movement/KampilanMovementTests.cs src/Hukbo.Core/Movement/Profiles/KampilanMovementProfile.cs
git commit -m "test(movement): calibrate kampilan matchups"
```

Stage only paths actually changed.

### Task K5: Prove asymmetric, mixed-group, mass, and replay behavior

**Files:**

- Modify: `tests/Hukbo.Core.Tests/Movement/KampilanMovementTests.cs`

The shared integration owner alone edits preset fixtures and registry tests.
This task consumes those tests but does not update their expected values.

**Step 1: Add scenario coverage.**

Use `MovementScenarioMatrix` to run Kampilan-present 1v2, 2v3, 3v5, 4v4,
5v5, 8v8, 100v100, and 250v250 scenarios. Include:

- globally favorable but locally outnumbered placement;
- globally unfavorable but locally supported placement;
- homogeneous Kampilan congestion; and
- a mixed roster where a shorter weapon occupies the direct lane.

**Step 2: Assert invariants.**

Same seed/build/commands must produce identical outcome, state hash, event
hash, and ordered events. V1-V4 fixture results must remain byte-identical.
The new preset must show local disengagement decisions in both global-posture
directions, no step above baseline, no unbounded state flipping, and no
scenario that reaches the tick limit solely because all living units are
orbiting or disengaging.

**Step 3: Run focused and freeze suites.**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release --filter "FullyQualifiedName~KampilanMovementTests|FullyQualifiedName~MovementPresetFreezeTests|FullyQualifiedName~MovementPresetRegistryTests"
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
git add tests/Hukbo.Core.Tests/Movement/KampilanMovementTests.cs
git commit -m "test(movement): verify kampilan at battle scale"
```

Stage only paths actually changed. The shared preset owner, not this weapon
task, owns a new digest fixture.

## 9. Calibration acceptance and rejection

Accept the initial Kampilan profile only when all are true:

- exact boundary and duration tests pass;
- each shorter-reach loadout has at least one reproducible tactical condition
  in the approved seed set where it crosses the Kampilan outer edge;
- Kampilan still spends measurably more engaged time near its preferred outer
  band than shorter profiles, using the shared metric definition;
- mirrored Kampilan duels have bounded no-progress streaks and do not resolve
  into permanent orbiting, retreat, or collision;
- two Kampilan allies experience clearance-delayed/staggered commitments in
  the constructed congested case;
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
- a specific opponent can never enter across the approved seeds/conditions,
  or enters without meaningful exposure in all of them;
- the profile produces unbounded oscillation, pursuit, disengagement, or no-progress
  behavior;
- a global count overrides an explicitly locally unsafe geometry;
- V1–V4 behavior, state/event hashes, outcomes, events, or trajectories
  change beyond the declared ruleset-literal update; or
- results depend on array order, floating point, wall clock, or a new random
  draw.

## 10. Activation and rollback

Kampilan cannot be activated independently. Its row is inert unless the
shared append-only weapon-relative movement preset is selected. Activation
requires:

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
  [`docs/research/movement/kampilan.md`](../../research/movement/kampilan.md)
- Research program scope and matchup matrix:
  [`docs/research/movement/README.md`](../../research/movement/README.md)
- Current movement configuration:
  `src/Hukbo.Core/Movement/MovementRuleset.cs`
- Current simulation integration:
  `src/Hukbo.Core/Simulation/BattleSimulation.cs`
- Current authoritative agent state:
  `src/Hukbo.Core/Simulation/AgentState.cs`
