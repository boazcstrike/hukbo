# Itak — Work Blade Movement Implementation Plan

> **For Codex:** REQUIRED SUB-SKILL: Use `executing-plans` to implement this
> plan task-by-task.

**Goal:** Add deterministic, materialized movement profiles and acceptance
coverage for solo Itak and Itak + Tall Hardwood Shield without changing combat
rules or duplicating shared movement and shield logic.

**Architecture:** The shared
[`movement plan`](README.md) owns the versioned movement preset, local count and
composition scan, facing, lifecycle state, proposal arithmetic, snapshot/hash
integration, and Tall Hardwood constraints. This plan specifies two complete
`CombatLoadout`-keyed profile contracts, contributes the solo row, and adds
Itak-specific boundary, matchup, determinism, and calibration tests. The Tall
Hardwood plan owns the shielded row. Runtime code resolves one immutable row;
it never composes a solo weapon row with a shield multiplier.

**Tech Stack:** C# 14, .NET 10, xUnit, fixed-point/integer arithmetic,
`SplitMix64`, immutable movement rules, and repository-local PowerShell gates.

---

## Status, authority, and dependencies

Status: implementation plan only; no code authorization.

Read with:

- the shared [movement architecture and task order](README.md);
- the [program research contract](../../research/movement/README.md);
- the [Itak research PRD](../../research/movement/itak.md); and
- the [Tall Hardwood Shield research PRD](../../research/movement/tall-hardwood-shield.md).

The shared names are `LoadoutMovementProfile`, `MovementRuleset` resolution,
and `WeaponMovementRules`. Reconcile this plan if the shared README changes
before execution; do not create parallel types, a second scan, or adapter
layers merely to preserve older wording.

Implement after the shared profile/catalog test seam exists. Enable shielded
scenario coverage only after the shared Tall Hardwood layer lands. Activate
only through the shared movement-preset gate.

## Current behavior

- Combat V2 is the live default and has six entries: Kampilan, Wasay, solo
  Kalis, shielded Kalis, solo Itak, and shielded Itak.
- Combat V3 already exists, is non-default, and fields the four solo weapons.
  The approved V2-to-V3 default switch is separate from movement and must not
  edit either roster.
- Movement defaults to `PersistentContingentsV4`. V1–V4 are frozen behavior.
- Target selection uses nearest perceived enemy with lower `EntityId` as the
  equal-distance tie-breaker.
- A moving agent follows either a contingent cohesion point or its target.
  `BuildMovementProposal()` uses the same `MovementSpeedRaw`, arrival taper,
  and body-contact stop distance for every loadout.
- Itak currently has no preferred-distance, direction, facing, commitment,
  recovery, local-count, or composition movement distinction.

## Goal and non-goals

Solo Itak is the compact, entry-and-reset profile. It retains the shared human
speed ceiling, waits for a viable line against longer weapons, crosses that
distance decisively, has a short but nonzero recovery, and disengages early
from numerical or angular pressure. Shielded Itak uses a deliberate
shielded-loadout approach at a clear lateral/reverse cost while remaining slightly more
repositioning-oriented than shielded Kalis.

Do not:

- change reach, damage, cooldown, combos, clash, hit location, or shield
  interception;
- add attack arcs, directional defense, shield bash, shoving, or trapping;
- edit V3's solo-only roster or make shielded Itak a V3 default;
- alter frozen movement presets or their fixtures;
- add rigid formations, shield walls, morale, rout, terrain, pathfinding,
  fatigue, wounds, projectiles, or campaign state;
- claim the candidate behavior is documented sixteenth-century Itak
  technique; or
- branch on `WeaponId.Itak` inside the shared runtime algorithm.

## Materialized profiles

Every number is **Provisional reconstruction:** gameplay tuning with no
historical measurement. `10_000` basis points equal `1.0`. Preferred distance uses the
selected combat ruleset's gameplay reach.

| `LoadoutMovementProfile` field | Solo Itak | Itak + Tall Hardwood |
| --- | ---: | ---: |
| `ForwardPaceBasisPoints` | 10,000 | 9,700 |
| `LateralPaceBasisPoints` | 9,300 | 8,700 |
| `BackwardPaceBasisPoints` | 8,100 | 7,100 |
| `CommittedPaceBasisPoints` | 4,000 | 3,500 |
| `PreferredDistanceBasisPoints` | 11,000 | 10,000 |
| `OpponentDistanceOffsetBasisPoints` (`KP, WA, KA, IT, KS, IS`) | `[-750, -500, -250, 0, 0, 250]` | `[-500, -250, 0, 250, -250, 0]` |
| `MaximumFacingStepsPerTick` | 2 | 2 |
| `CommittedFacingStepsPerTick` | 1 | 1 |
| `AccelerationBasisPointsPerTick` | 7,000 | 6,500 |
| `DecelerationBasisPointsPerTick` | 8,000 | 7,000 |
| `CommitmentTicks` | 2 | 3 |
| `RecoveryTicks` | 2 | 3 |
| `AllyClearanceBodyDiametersBasisPoints` | 11,500 | 13,500 |
| `DisengageEnemyToAllyBasisPoints` | 12,500 | 15,000 |
| `ReengageEnemyToAllyBasisPoints` | 10,000 | 11,000 |
| `PursuitSupportBodyDiametersBasisPoints` | 10,000 | 8,000 |

The complete keys are
`(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None)` and
`(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood)`. Unsupported
loadouts fail through the shared catalog's explicit failure path.

The Tall Hardwood plan materializes the shielded column in
`Profiles/TallHardwoodMovementProfiles.cs`; the shared profile validator
checks its envelope. No runtime step multiplies the solo row by shield values,
and no Itak file reimplements shield clearance, facing, or threat-bearing
rules.

## Evidence-to-mechanic trace

| Finding | Mechanic | Evidence boundary |
| --- | --- | --- |
| The exact early Itak identity and form are not established. | Make no historical-technique API or UI claim. | **Unknown or unsupported**. |
| Hukbo retains `Itak — Work Blade` as its qualified game identity. | Keep the implemented label while exposing its uncertainty in research-facing documentation. | **Provisional reconstruction**; product taxonomy, not a historical identification. |
| Later bolo/Itak evidence supports a compact one-handed tool-and-weapon family. | Prefer a closer 1.10-reach solo distance and entry/reset decisions. | **Documented, form uncertain**; values are **Provisional reconstruction**. |
| One cited modern practitioner/community description presents a forward-weighted bolo/Itak form. | Commitment is nonzero and direction cannot reverse freely; recovery remains two ticks solo. | **Provisional reconstruction**; one modern analogy, not period evidence or a measured Itak norm. |
| Shielded approach is physically plausible but the exact pairing is not period-verified. | Use the shared Tall Hardwood class plus an explicitly materialized Itak row. | **Provisional reconstruction**. |
| No reviewed source supplies count thresholds, footwork patterns, or movement speeds. | Ratios, lifecycle states, and values are transparent calibration inputs, not history. | **Unknown or unsupported:** no historical mechanic follows. |

## Exact equality and transition rules

Shared stage priority and algorithms remain authoritative. Itak acceptance
tests pin:

1. `friendlyCount` includes the subject and every locally admitted living ally;
   `enemyCount` includes every locally admitted living enemy. Dead and
   out-of-radius agents do not count.
2. Zero enemies never enters or retains disengagement.
3. Solo enters disengagement when
   `enemyCount * 4 >= friendlyCount * 5`; equality enters.
4. Shielded enters when
   `enemyCount * 2 >= friendlyCount * 3`; equality enters.
5. Solo becomes eligible to leave disengagement when
   `enemyCount <= friendlyCount`; shielded Itak leaves when
   `enemyCount * 10 <= friendlyCount * 11`. Equality leaves. The shared
   footwork phase owns persistence.
6. At exactly preferred center-to-center distance, ordinary approach becomes
   `Engage`. Until it reaches the existing post-movement attack gate, the unit
   may cross the remaining distance through a free lane at the shared
   engaged-entry cap.
   Preferred distance never changes combat reach.
7. Circular facing/travel separation of `0–1` `Facing16` sectors is forward,
   `2–5` is lateral, and `6–8` is backward. A normal Itak turn request exactly
   two sectors reaches its desired facing; a committed request exactly one
   sector does too. One sector beyond either cap advances only by the cap.
8. An attack accepted after movement on tick `T` enters `Commit` without
   changing attack eligibility or same-tick movement. Solo Itak remains
   commitment-limited on `T+1`; shielded Itak on `T+1` and `T+2`.
9. Solo Itak then receives two whole recovery ticks; shielded Itak receives
   three. Miss, clash, and landed hit use identical movement lifecycle.
10. Shared death/attack priority outranks a new disengagement transition. An
    attack accepted by unchanged combat gates interrupts recovery and starts a
    fresh commitment. No Itak-only priority ladder is allowed.
11. Equal threat, ally, lane, and exit candidates use the common stable key,
    ultimately lower `EntityId`, independent of input enumeration.
12. Advantage may reduce refusal or choose a route; it never raises step
    magnitude above the profile-adjusted `MovementSpeedRaw`.

## Matchup and count expectations

These assert movement shape, never a mandatory winner.

| Case | Solo Itak | Itak + Tall Hardwood |
| --- | --- | --- |
| vs Kampilan | Wait outside danger, enter after commitment, then reset or leave. | Use short deliberate steps and abandon a blocked entry. |
| vs Wasay | Avoid stationary exchange and use a viable post-commitment line. | Narrow the threat line, enter, then restore exit space. |
| vs solo Kalis | Cross Kalis distance only with a viable lane; otherwise seek support. | Close through a free lane without entering an endless circle. |
| vs shielded Kalis | Refuse repeated blocked direct entry; use mobility or ally pressure. | Respect the longer Kalis distance and keep an exit. |
| vs solo Itak | Shared tie-breaks resolve symmetry; maintain reset room. | Deny free circling without chasing away from allies. |
| vs shielded Itak | Use solo lateral freedom but leave if the open line closes. | Mirror probes remain deterministic; no bespoke deadlock rule. |
| 1v2 | Solo enters because `2*4 >= 1*5`; shielded enters because `2*2 >= 1*3`. | Both preserve an exit without extra speed. |
| 2v3 | Solo enters because `3*4 >= 2*5`; shielded enters at exact equality `3*2 == 2*3`. | Equality behavior must be stable. |
| 3v5 | Both enter: `5*4 >= 3*5` and `5*2 >= 3*3`. | Follow regroup/withdraw posture; no last-stand bonus. |
| Local advantage | Occupy a viable line without increasing movement budget. | Leave separate ally lanes instead of stacking behind shields. |

## Granular TDD tasks

### Task I1: Add the solo Itak row and verify the shield-owned row

**Depends on:** the shared profile type and Tall Hardwood exported-row
contract in [`README.md`](README.md). Shared registry composition happens
later in T4.

**Files:**

- Create: `tests/Hukbo.Core.Tests/Movement/ItakMovementProfileTests.cs`
- Create: `src/Hukbo.Core/Movement/Profiles/ItakMovementProfile.cs`

**Step 1: Write the failing solo test**

Assert the exported complete solo Itak row contains every cell in the
materialized table, including ratios, pursuit support, and
opponent-distance offsets.

**Step 2: Run it**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release `
  --filter "FullyQualifiedName~ItakMovementProfileTests"
```

Expected: FAIL because the exported solo row is missing.

**Step 3: Add the complete solo row**

Export one immutable row for the shared owner to compose in T4. Do not edit
the registry or add an Itak branch to simulation code.

**Step 4: Run the test**

Expected: PASS for solo.

**Step 5: Run the Tall Hardwood profile dependency**

Run `TallHardwoodMovementProfileTests` and confirm its exported row asserts
the exact shielded Itak values and complete loadout key.

**Step 6: Run it**

Expected before the Tall Hardwood task lands: FAIL because its exported
shielded row is missing. Do not add it from the Itak-owned task.

**Step 7: Complete the declared dependency**

Have the Tall Hardwood owner add its materialized row and validation tests.
Resume after that focused test passes.

**Step 8: Run the focused test**

Expected: PASS for both exported rows. Shared T4 later proves registry
resolution and unsupported-shield rejection.

**Step 9: Commit**

```powershell
git add src/Hukbo.Core/Movement/Profiles/ItakMovementProfile.cs `
  tests/Hukbo.Core.Tests/Movement/ItakMovementProfileTests.cs
git commit -m "feat(movement): add solo Itak movement profile"
```

### Task I2: Pin ratios, preferred distance, and lifecycle

**Files:**

- Test: `tests/Hukbo.Core.Tests/Movement/ItakMovementTransitionTests.cs`
- Shared defects: hand off the failing test to the foundation owner; do not
  edit shared movement or simulation files from this equipment task.

**Step 1: Add count-boundary tests**

Assert:

- solo `(friendly: 4, enemy: 5)` enters and `(4, 4)` leaves;
- shielded `(2, 3)` enters and `(10, 11)` leaves;
- zero enemies does not disengage; and
- reversing observation enumeration changes no decision.

**Step 2: Run the tests**

Expected: FAIL until shared ratio comparison consumes Itak profile values.

**Step 3: Verify shared wiring or hand it off**

If common logic does not consume the resolved immutable profile, stop and hand
off the failing test. Do not test the weapon ID in shared logic.

**Step 4: Add raw distance tests**

Use `preferredRaw - 1`, exact preferred, and `preferredRaw + 1`. Use checked
integer squared-distance arithmetic; no float or decimal state.

**Step 5: Add attack-cycle tests**

With a clash-neutral ruleset, assert unchanged movement on attack tick `T`,
commitment caps beginning on `T+1`, exact two- and three-tick recovery windows
after commitment, no instant full reverse, and unchanged attack resolution.

**Step 6: Run**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release `
  --filter "FullyQualifiedName~ItakMovementTransitionTests"
```

Expected: PASS.

**Step 7: Commit**

```powershell
git add tests/Hukbo.Core.Tests/Movement/ItakMovementTransitionTests.cs
git commit -m "test(movement): pin Itak movement transitions"
```

### Task I3: Cover Itak 1v1 and 2v2 geometry

**Files:**

- Test: `tests/Hukbo.Core.Tests/Movement/ItakMovementScenarioTests.cs`

The shared integration owner supplies `MovementScenarioMatrix`. Hand off any
missing generic case generation or metric rather than editing that helper.

**Step 1: Add twelve 1v1 cases**

Cross solo and shielded Itak with all six loadouts. Use explicit geometry,
heading, cooldown, seed, and tick bounds. Assert distance, lane, lifecycle,
speed ceiling, and stable target properties rather than winners.

**Step 2: Run**

Expected: FAIL before the profiles are consumed end to end.

**Step 3: Verify catalog resolution or hand it off**

If shared integration does not resolve once per agent/loadout, stop and hand
off the failing test. Consume only the profile's six declared
opponent-distance offsets; do not add opponent-specific runtime branches.

**Step 4: Generate Itak-relevant 2v2 cells**

Generate the canonical 21 team compositions and 231 unordered team matchups,
then execute every cell containing `IT` or `IS` on either team. Assert no
missing/duplicate case ID, deterministic input-order reversal, profile speed
ceilings, collision-safe destinations, and presence of homogeneous and mixed
Itak teams.

**Step 5: Add focused cases**

Pin separate-lane cooperation, ally-blocked refusal, distracted-target entry,
and post-ally-death reassessment for both Itak rows.

**Step 6: Run**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release `
  --filter "FullyQualifiedName~ItakMovementScenarioTests"
```

Expected: PASS with zero skipped generated cells.

**Step 7: Commit**

```powershell
git add tests/Hukbo.Core.Tests/Movement/ItakMovementScenarioTests.cs
git commit -m "test(movement): cover Itak matchup geometry"
```

### Task I4: Preserve shielded Itak through explicit combat V2

**Files:**

- Test: `tests/Hukbo.Core.Tests/Movement/ItakMovementScenarioTests.cs`

The shared integration owner alone updates `ScenarioTests.cs` for the combat
default switch. This task runs that suite but does not edit it.

**Step 1: Add explicit V2 scenarios**

After combat V3 becomes default, construct shielded Itak with:

```csharp
CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
RosterCounts = [0, 0, 0, 0, 0, agentsPerFaction],
```

The paired solo comparison uses
`RosterCounts = [0, 0, 0, 0, agentsPerFaction, 0]`. Never depend on the default
preset or round-robin assignment.

**Step 2: Demonstrate the expected failure**

Without explicit V2, the shielded setup must fail because V3 has four solo
entries. With explicit V2 and six counts, validation and creation pass.

**Step 3: Assert end-to-end identity**

Assert all spawned loadouts, the resolved shielded Itak movement profile, and
two-run equality of ordered events, state hash, event hash, and outcome.

**Step 4: Run**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release `
  --filter "FullyQualifiedName~ScenarioTests|FullyQualifiedName~ItakMovementScenarioTests.ItakV2"
```

Expected: PASS; V3 remains four solo entries.

**Step 5: Commit**

```powershell
git add tests/Hukbo.Core.Tests/Movement/ItakMovementScenarioTests.cs
git commit -m "test(movement): preserve shielded Itak scenarios on combat v2"
```

### Task I5: Freeze deterministic state and old presets

**Files:**

- Test: `tests/Hukbo.Core.Tests/Movement/ItakMovementScenarioTests.cs`

The shared integration owner alone edits frozen-preset, determinism, hashing,
and snapshot fixtures. This task adds Itak acceptance cases and consumes
those shared tests.

**Step 1: Run V1–V4 freeze tests before edits**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release `
  --filter "FullyQualifiedName~MovementPresetFreezeTests"
```

Expected: PASS.

**Step 2: Add state-sensitivity and snapshot projection tests**

Under the new preset, vary each authoritative facing/lifecycle field and assert
the state hash changes. Assert `CreateSnapshot()` exposes the exact fields; do
not claim restore or serialization behavior that has no repository contract. Do not snapshot
the immutable profile, local neighbor lists, ratios, or derived destinations.

**Step 3: Add old-preset neutrality**

Run Itak under V1–V4 and assert byte-identical existing digests. New state stays
neutral and old proposal paths do not resolve Itak profiles.

**Step 4: Run determinism/freeze tests**

Expected: PASS without modifying old golden fixtures.

**Step 5: Commit**

```powershell
git add tests/Hukbo.Core.Tests/Movement/ItakMovementScenarioTests.cs
git commit -m "test(movement): verify Itak movement determinism"
```

### Task I6: Calibrate and enforce rejection criteria

**Files:**

- Modify: `docs/plans/movement/itak.md` only for measured results
- Modify shared calibration artifact/tool only if owned by README

**Step 1: Run all scale tiers**

Run Itak-relevant 1v1 and generated 2v2 cases plus curated 1v2, 2v3, 3v5,
4v4, 5v5, 8v8, 100v100, and 250v250. Compare solo with explicit-V2 shielded
Itak using identical seeds and geometry.

**Step 2: Record**

Measure danger-band exposure, entry attempts, commitment/recovery ticks,
disengagement, isolation, ally obstruction, blocked moves, pursuit distance,
state/event hashes, outcome, runtime, and warm-tick allocation.

**Step 3: Reject when**

- any count grants speed;
- Itak instantly reverses during commitment/recovery;
- solo Itak indefinitely orbits longer weapons or never finds a viable entry
  across the calibrated seed set;
- shielded Itak gets shield effects twice;
- equality or target choice changes with enumeration order;
- 1v2 does not attempt disengagement with an open exit;
- shield bearers form persistent wall-like spacing;
- Itak dominates every context or lacks both an individual and group role;
- V1–V4 digests move;
- repeated seeds diverge; or
- shared 250v250 runtime/allocation limits fail.

**Step 4: Tune one materialized row**

Edit catalog values only, document why, and rerun I1–I6. Do not add
matchup-specific exceptions.

**Step 5: Run the canonical gate**

```powershell
./scripts/verify.ps1
```

Expected: every gate stage passes. Record the real output in the shared
verification record. This does not prove manual spectator behavior.

## Activation and rollback

- Merge data and tests behind the new opt-in movement preset first.
- The separate combat-default task may switch V2 to V3 only after its own
  tests; this plan makes no combat roster edit.
- The shared movement plan owns any default activation after all equipment,
  exhaustive matrix, determinism, performance, and manual gates pass.
- Roll back by selecting frozen `PersistentContingentsV4`; do not delete
  profiles, rewrite old fixtures, or mutate V2/V3.
- Shielded Itak remains available through explicit combat V2 after V3 becomes
  default.
- If the shared architecture changes names, update this plan before execution
  and keep one implementation path.

## Completion criteria

- Both exact Itak loadouts resolve complete immutable rows.
- No shared runtime function branches on `WeaponId.Itak`.
- Tall Hardwood behavior is shared and applied once.
- Every equality and recovery boundary is covered.
- All twelve Itak-centered 1v1 cases and every mechanically selected
  Itak-relevant 2v2 cell execute.
- Explicit V2 tests preserve shielded Itak after the combat-default switch.
- Frozen V1–V4 movement behavior remains byte-identical.
- Determinism, performance, allocation, role viability, gate output, and
  manual activation status are recorded without historical overclaim.
