# Kalis — Thrusting Blade Movement Implementation Plan

> **For Codex:** REQUIRED SUB-SKILL: Use `executing-plans` to implement this
> plan task-by-task.

**Goal:** Add deterministic, materialized movement profiles and acceptance
coverage for solo Kalis and Kalis + Tall Hardwood Shield without changing
combat rules or duplicating the shared shield algorithm.

**Architecture:** The shared
[`movement plan`](README.md) owns the movement preset, authoritative facing,
local-neighborhood scan, lifecycle state machine, proposal arithmetic,
snapshot/hash integration, and Tall Hardwood shield invariants. This plan adds
two complete `CombatLoadout`-keyed profile contracts and Kalis-specific unit,
scenario, determinism, and calibration tests. Profile selection happens once
through the shared catalog; runtime code consumes the selected immutable
profile and never composes weapon and shield modifiers dynamically.

**Tech Stack:** C# 14, .NET 10, xUnit, Hukbo fixed-point integer math,
`SplitMix64`, immutable/versioned movement rules, and the repository-local
PowerShell verification scripts.

---

## Status, authority, and dependencies

Status: implementation plan only; no code authorization.

This plan is subordinate to:

- the shared [movement implementation contract](README.md);
- the [program research contract](../../research/movement/README.md);
- the [Kalis research PRD](../../research/movement/kalis.md); and
- the [Tall Hardwood Shield research PRD](../../research/movement/tall-hardwood-shield.md).

The type names below are proposed interfaces so tasks and tests can be written
precisely before the shared foundation lands. If `README.md` selects different
names, use its names and preserve every value, equality rule, ownership
boundary, and observable assertion in this plan. Do not create a second
catalog, state machine, neighborhood scan, or shield helper to preserve these
provisional names.

Implementation order:

1. land the shared architecture through its profile/catalog test seam;
2. implement this plan's solo catalog row and focused tests;
3. let the Tall Hardwood plan materialize the shielded row, then enable this
   plan's shielded Kalis scenario tests;
4. run cross-weapon calibration; and
5. leave the new movement preset opt-in until a later activation task is
   explicitly approved.

## Current behavior

- `Scenario.CombatPreset` defaults to `PrecolonialPhilippinesV2`.
- V2 has six roster entries in stable order: Kampilan, Wasay, solo Kalis,
  shielded Kalis, solo Itak, and shielded Itak.
- The already-registered `PrecolonialPhilippinesV3` has four solo loadouts.
  The separately planned default switch from combat V2 to combat V3 must not
  edit either roster.
- `Scenario.MovementPreset` defaults to `PersistentContingentsV4`.
  `MovementPresetId`, `MovementRuleset`, and `MovementPresetRegistry` freeze
  V1–V4 behavior.
- `BattleSimulation.SelectTargetsAndIntents()` selects the nearest perceived
  enemy, breaking equal distance by the lower `EntityId`.
- `GatherMovementProposals()` sends a moving agent either toward a contingent
  cohesion point or toward its selected enemy.
- `BuildMovementProposal()` uses the same `AgentState.MovementSpeedRaw` and
  arrival taper for every loadout and normally stops one body diameter from
  the enemy. There is no authoritative facing, weapon-relative preferred
  distance, commitment movement factor, recovery movement state, or
  local-composition decision.
- Movement proposals are resolved simultaneously through the deterministic
  collision system. Preserve that boundary.

## Desired Kalis behavior

### Goal

Solo Kalis is the measure-and-line one-handed profile: it prefers the outer
part of its useful distance, makes modest lateral adjustments, commits briefly,
and leaves divided threat angles earlier than a shielded Kalis. Shielded Kalis
keeps the same lane-conscious identity but closes and turns more deliberately,
with lower lateral/reverse freedom and a longer recovery.

### Non-goals

Do not:

- change damage, reach, cooldown, combos, clash resolution, hit location, or
  shield interception;
- add directional shield defense, attack arcs, shield bash, or collision
  pushing;
- edit combat V3's roster or restore shielded Kalis to that roster;
- edit frozen movement-preset behavior or golden fixtures;
- implement a rigid rank, shield wall, morale, panic, rout, terrain,
  pathfinding, or campaign state;
- infer historical Kalis footwork from the gameplay numbers; or
- calculate the shielded row at runtime by multiplying a solo row by shield
  modifiers.

## Materialized profiles

All values are **Provisional reconstruction:** gameplay tuning with no
historical measurement. Multipliers use the shared catalog's `10_000 == 1.0` basis-point
scale. Distances multiply the Kalis gameplay reach configured by the selected
combat ruleset, not a museum measurement.

| `LoadoutMovementProfile` field | Solo Kalis | Kalis + Tall Hardwood |
| --- | ---: | ---: |
| `ForwardPaceBasisPoints` | 9,700 | 9,400 |
| `LateralPaceBasisPoints` | 8,900 | 8,400 |
| `BackwardPaceBasisPoints` | 7,600 | 6,700 |
| `CommittedPaceBasisPoints` | 3,300 | 3,000 |
| `PreferredDistanceBasisPoints` | 12,000 | 13,000 |
| `OpponentDistanceOffsetBasisPoints` (`KP, WA, KA, IT, KS, IS`) | `[-500, -250, 0, 250, 250, 500]` | `[-250, 0, 250, 500, 0, 250]` |
| `MaximumFacingStepsPerTick` | 2 | 2 |
| `CommittedFacingStepsPerTick` | 1 | 1 |
| `AccelerationBasisPointsPerTick` | 6,000 | 5,600 |
| `DecelerationBasisPointsPerTick` | 7,000 | 6,000 |
| `CommitmentTicks` | 2 | 3 |
| `RecoveryTicks` | 2 | 3 |
| `AllyClearanceBodyDiametersBasisPoints` | 12,000 | 14,000 |
| `DisengageEnemyToAllyBasisPoints` | 15,000 | 17,500 |
| `ReengageEnemyToAllyBasisPoints` | 11,000 | 11,000 |
| `PursuitSupportBodyDiametersBasisPoints` | 12,500 | 10,000 |

The Tall Hardwood plan owns
`Profiles/TallHardwoodMovementProfiles.cs` and must materialize the shielded
column exactly. The shared profile validator accepts that row only when it
satisfies the shield envelope. Runtime code must not reapply a shield
multiplier or branch on `WeaponId.Kalis`.

The profile key is the complete loadout
`(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None|TallHardwood)`.
Unsupported armor/shield combinations fail through the shared catalog's
documented exception path; they do not fall back to the solo row.

## Evidence-to-mechanic trace

| Research finding | Planned mechanic | Required evidence label and limit |
| --- | --- | --- |
| Repository synthesis reports a 1521 blade-name lead elsewhere rendered *calis*, but this research has not independently located its transcription or folio. | Retain the Kalis identity as a qualified product label only. | **Provisional reconstruction** pending source verification; supplies no movement number. |
| Later one-handed, point-capable Kalis/Kris objects provide a handling analogy. | Preferred distance is 1.20 reach solo and 1.10 reach shielded; Kalis values lane control over deep entry. | **Documented, form uncertain** objects; mechanic is **Provisional reconstruction**. |
| Mactan describes side-to-side movement while approaching with shields. | Shielded Kalis may approach and correct laterally; it is not stationary. | **Documented** for that encounter; exact factors and weapon pairing are **Provisional reconstruction**. |
| A large off-hand shield plausibly increases clearance and turn cost. | Select the materialized shielded row and shared `TallHardwood` behavior. | **Provisional reconstruction**; no directional interception follows. |
| No reviewed period source supplies Kalis footwork, a count threshold, or a turn rate. | Use generic lifecycle states, integer ratios, and calibration gates; expose no historical technique names. | **Unknown or unsupported:** no historical mechanic follows. |

## Exact decision and transition rules

The shared README owns stage priority and algorithms. Kalis tests must pin the
following observable results:

1. **Count definition.** `friendlyCount` includes the subject plus every
   living, locally perceived same-faction agent admitted by the shared local
   support query. `enemyCount` includes every living, locally perceived enemy
   admitted by that query. Dead and out-of-radius agents count as zero.
2. **No-enemy case.** `enemyCount == 0` can never enter or retain tactical
   disengagement, regardless of cross-multiplication results.
3. **Enter equality.** Solo Kalis enters when
   `enemyCount * 2 >= friendlyCount * 3`. Shielded Kalis enters when
   `enemyCount * 4 >= friendlyCount * 7`. Equality is on the disengage side.
4. **Exit equality.** Solo Kalis becomes eligible to leave when
   `enemyCount * 10 <= friendlyCount * 11`; shielded Kalis uses the same
   `11:10` release equality. Equality is on the non-disengage
   side. Persistence comes from the shared footwork phase.
5. **Preferred-distance equality.** At exactly the profile's preferred
   center-to-center distance, an approaching Kalis enters `Engage`. Until it
   reaches the existing post-movement attack gate, it may cross the remaining
   distance through a free lane at the shared engaged-entry cap. One raw unit outside remains
   `Approach`; preferred distance never changes combat reach.
6. **Direction-band equality.** Circular facing/travel separation of `0–1`
   `Facing16` sectors is forward, `2–5` is lateral, and `6–8` is backward.
   The common direction classifier—not Kalis code—makes this choice.
7. **Turn-cap equality.** Normal Kalis can traverse at most two sectors per
   tick and committed Kalis at most one. A request exactly at the cap reaches
   the desired facing; one sector beyond advances only by the cap.
8. **Commitment.** An attack accepted after movement on tick `T` enters
   `Commit` without changing attack eligibility or same-tick movement. Solo
   Kalis remains commitment-limited on `T+1`; shielded Kalis on `T+1` and
   `T+2`.
9. **Recovery duration.** Solo Kalis then receives two whole recovery ticks;
   shielded Kalis receives three. A miss, clash, or landed hit uses the same
   movement lifecycle.
10. **Priority.** Death and the shared attack/commitment contract outrank a
    new disengagement request. An attack accepted by unchanged combat gates
    interrupts recovery and starts a fresh commitment. Kalis-specific code
    must not invent another priority ladder.
11. **Tie-breaking.** Equal candidate destinations, exits, allies, or threats
    resolve by the shared stable key, ultimately lower `EntityId`; never by
    collection insertion order.
12. **Speed ceiling.** Counts, composition, pursuit, and disengagement select
    directions and willingness only. No Kalis proposal can exceed the
    profile-adjusted share of `MovementSpeedRaw`, and no favorable count can
    increase that share.

## Matchup and count behavior to pin

All cases below are provisional gameplay expectations, not promised win rates.

| Case | Solo Kalis expected movement | Shielded Kalis expected movement |
| --- | --- | --- |
| vs Kampilan | Preserve distance; prefer a shallow line change after commitment; refuse a stationary trade. | Use a deliberate collision-safe lane and stop pursuit before losing support. |
| vs Wasay | Avoid the planted entry line; use recovery timing without changing combat cooldown. | Narrow the approach line but retain a backward exit. |
| vs solo Kalis | Stable entity-ID tie-break prevents endless symmetric target churn. | Close deliberately while preserving turn room. |
| vs shielded Kalis | Seek an open line or disengage instead of repeating a blocked direct entry. | Mirror behavior remains deterministic without a bespoke deadlock rule. |
| vs solo Itak | Preserve the longer preferred distance and yield rather than grant crowded entry. | Deny the rush while retaining clearance from allies. |
| vs shielded Itak | Use solo lateral freedom or wait for ally pressure. | Keep the slightly greater Kalis distance and avoid a tight circling contest. |
| 1v2 | `2*2 >= 1*3`: solo enters disengagement at equality-or-worse; shielded also enters because `2*4 >= 1*7`. | Same count result, with lower backward/lateral budgets. |
| 2v3 | `3*2 == 2*3`: solo enters at exact equality. `3*4 < 2*7`: shielded does not enter on count alone, but may refuse a blocked or divided-bearing commitment through shared rules. | Preserve separate ally lanes; no formation slot. |
| 3v5 | Solo enters because `5*2 >= 3*3`; shielded does not enter on count alone because `5*4 < 3*7`, so contingent withdrawal and separated threats must still remain effective. | No last-stand or shield-wall bonus. |
| Local advantage | Do not shorten physical step time or exceed profile speed. | Advance only through a distinct lane. |

The `3v5` shielded result is intentionally explicit: the candidate 7:4 count
threshold is more tolerant than the research narrative. If calibration shows
that this prevents timely withdrawal, reject or retune the threshold openly;
do not hide an extra Kalis-only branch.

## Granular implementation tasks

### Task K1: Pin the solo row and verify the shield-owned row

**Depends on:** shared profile type and the Tall Hardwood exported-row
contract from [`README.md`](README.md). Shared registry composition happens
later in T4.

**Files:**

- Create: `tests/Hukbo.Core.Tests/Movement/KalisMovementProfileTests.cs`
- Create: `src/Hukbo.Core/Movement/Profiles/KalisMovementProfile.cs`

**Step 1: Write the failing solo-profile theory row**

Add a theory asserting that the exported solo row uses
`new CombatLoadout(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None)` and
contains every solo value in the materialized table, including both ratio
thresholds and the six opponent-distance offsets.

**Step 2: Run the focused test**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release `
  --filter "FullyQualifiedName~KalisMovementProfileTests"
```

Expected: FAIL because the exported Kalis row is absent.

**Step 3: Add the complete solo row**

Export one immutable row keyed by the complete loadout for the shared owner to
compose in T4. Do not edit the registry or add a Kalis branch to proposal code.

**Step 4: Run the test**

Expected: PASS for solo Kalis.

**Step 5: Run the Tall Hardwood profile dependency**

Run `TallHardwoodMovementProfileTests` and confirm its exported row asserts
every shielded Kalis value plus the complete `CombatLoadout` key.

**Step 6: Run the focused test**

Expected before the Tall Hardwood task lands: FAIL because its exported
shielded row is absent. Do not add that row from the Kalis-owned task.

**Step 7: Complete the declared dependency**

Have the Tall Hardwood owner add the already-materialized row through its
owned profile file and shared validator. Resume this plan after that task
passes.

**Step 8: Run the focused test**

Expected: PASS for both exported rows. Shared T4 later proves registry
resolution and unsupported-shield rejection.

**Step 9: Commit**

```powershell
git add src/Hukbo.Core/Movement/Profiles/KalisMovementProfile.cs `
  tests/Hukbo.Core.Tests/Movement/KalisMovementProfileTests.cs
git commit -m "feat(movement): add solo Kalis movement profile"
```

### Task K2: Pin Kalis equality and lifecycle transitions

**Files:**

- Test: `tests/Hukbo.Core.Tests/Movement/KalisMovementTransitionTests.cs`
- Shared defects: hand off the failing test to the foundation owner; do not
  edit shared movement or simulation files from this equipment task.

**Step 1: Add exact count-boundary tests**

Use the shared decision test seam with explicit local observations:

- solo `(friendly: 2, enemy: 3)` enters;
- solo `(friendly: 10, enemy: 11)` leaves at equality;
- shielded `(4, 7)` enters;
- shielded `(4, 6)` does not enter from neutral and `(10, 11)` leaves at
  equality;
- either profile with zero enemies does not disengage; and
- reverse the input-agent enumeration and assert identical decisions.

**Step 2: Run the transition tests**

Expected: FAIL until the catalog ratios flow through the shared ratio
comparison.

**Step 3: Verify shared profile wiring or hand it off**

If the shared foundation has not already passed the resolved profile to common
ratio/lifecycle functions, stop and hand off the failing test. Never test
`WeaponId.Kalis` inside those functions.

**Step 4: Add preferred-distance raw-boundary tests**

Construct distances at `preferredRaw - 1`, `preferredRaw`, and
`preferredRaw + 1`. Assert the equality rules above using squared-distance
comparisons and checked `long`/`Int128` arithmetic chosen by the shared plan;
do not introduce floating point.

**Step 5: Add commitment/recovery tests**

Run a clash-neutral test ruleset with Kalis in range and cooldown zero. Assert
unchanged movement on attack tick `T`, commitment caps beginning on `T+1`,
the exact two- and three-tick recovery windows after commitment, and unchanged
attack eligibility/resolution.

**Step 6: Run the focused tests**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release `
  --filter "FullyQualifiedName~KalisMovementTransitionTests"
```

Expected: PASS with no modified combat assertions.

**Step 7: Commit**

```powershell
git add tests/Hukbo.Core.Tests/Movement/KalisMovementTransitionTests.cs
git commit -m "test(movement): pin Kalis movement transitions"
```

### Task K3: Cover all Kalis-relevant 1v1 and 2v2 matrix cells

**Files:**

- Test: `tests/Hukbo.Core.Tests/Movement/KalisMovementScenarioTests.cs`

The shared integration owner supplies `MovementScenarioMatrix`. Hand off any
missing generic case generation or metric rather than editing that helper.

**Step 1: Add the six 1v1 rows for each Kalis variant**

Use the canonical loadout IDs in the research README. Give the harness explicit
positions, headings, cooldowns, and a bounded tick count. Assert movement
properties—distance band, chosen lane, transition sequence, speed ceiling,
and stable target—not a required winner.

**Step 2: Run the 1v1 theory**

Expected: FAIL on at least preferred-distance and lifecycle observations before
the Kalis profiles are consumed end to end.

**Step 3: Verify shared integration or hand it off**

If the common proposal engine does not resolve the profile once per
agent/loadout, stop and hand off the failing test to the shared owner. No
opponent-name runtime branch is allowed.

**Step 4: Add mechanically generated 2v2 coverage**

Generate the program contract's 21 unordered team compositions and 231
unordered team-vs-team cells, then select every cell in which either team
contains `KA` or `KS`. Assert:

- every selected cell executes for the required seeds;
- input-order reversal produces the same ordered result;
- no movement exceeds the profile ceiling;
- allies do not resolve to overlapping destinations; and
- homogeneous and mixed Kalis teams both appear in the executed case IDs.

Do not encode 231 bespoke outcomes or expected winners.

**Step 5: Add focused geometry cases**

Pin one separate-lane 2v2, one ally-blocked refusal, and one post-ally-death
reassessment for solo and shielded Kalis.

**Step 6: Run the focused suite**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release `
  --filter "FullyQualifiedName~KalisMovementScenarioTests"
```

Expected: PASS; zero skipped matrix case IDs.

**Step 7: Commit**

```powershell
git add tests/Hukbo.Core.Tests/Movement/KalisMovementScenarioTests.cs
git commit -m "test(movement): cover Kalis matchup geometry"
```

### Task K4: Preserve shielded Kalis under explicit combat V2

**Files:**

- Test: `tests/Hukbo.Core.Tests/Movement/KalisMovementScenarioTests.cs`

The shared integration owner alone updates `ScenarioTests.cs` for the combat
default switch. This task runs that suite but does not edit it.

**Step 1: Add explicit V2 scenarios**

After the approved combat-default switch lands, construct shielded Kalis
scenarios with:

```csharp
CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
RosterCounts = [0, 0, 0, agentsPerFaction, 0, 0],
```

Construct the paired solo comparison with
`RosterCounts = [0, 0, agentsPerFaction, 0, 0, 0]`. Never rely on the default
combat preset or V2's round-robin roster assignment in these tests.

**Step 2: Run the focused V2 tests**

Expected before explicit selection: FAIL because the new default V3 roster has
four entries and cannot materialize shielded Kalis. Expected after the test is
correctly explicit: PASS without modifying V3.

**Step 3: Assert profile resolution and deterministic repetition**

Assert every spawned loadout matches the requested row, shielded agents resolve
the Kalis + Tall Hardwood movement profile, and two identical runs produce the
same ordered event stream, state hash, event hash, and outcome.

**Step 4: Run V2 plus default-V3 scenario tests**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release `
  --filter "FullyQualifiedName~ScenarioTests|FullyQualifiedName~KalisMovementScenarioTests.KalisV2"
```

Expected: PASS. `PhilippineCombatPresetV3.Rules.Roster` remains four solo
entries.

**Step 5: Commit**

```powershell
git add tests/Hukbo.Core.Tests/Movement/KalisMovementScenarioTests.cs
git commit -m "test(movement): preserve shielded Kalis scenarios on combat v2"
```

### Task K5: Hash, snapshot, and frozen-preset regression

**Files:**

- Test: `tests/Hukbo.Core.Tests/Movement/KalisMovementScenarioTests.cs`

The shared integration owner alone edits frozen-preset, determinism, hashing,
and snapshot fixtures. This task adds Kalis acceptance cases and consumes
those shared tests.

**Step 1: Run the existing frozen-preset tests before touching fixtures**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release `
  --filter "FullyQualifiedName~MovementPresetFreezeTests"
```

Expected: PASS with the checked-in V1–V4 fixtures.

**Step 2: Add Kalis state-sensitivity tests**

Under the new movement preset, vary one authoritative Kalis facing/lifecycle
field at a time and assert different state hashes. Round-trip through
`CreateSnapshot()` or the shared snapshot contract and assert exact field
preservation. Do not snapshot the immutable profile or derived neighborhood
counts.

**Step 3: Add old-preset neutrality tests**

Run identical V1–V4 Kalis scenarios and assert their recorded digests remain
byte-identical. New fields must stay at their neutral values and old proposal
paths must not resolve or consume Kalis profiles.

**Step 4: Run focused determinism and freeze tests**

Expected: PASS without rewriting any old fixture.

**Step 5: Commit**

```powershell
git add tests/Hukbo.Core.Tests/Movement/KalisMovementScenarioTests.cs
git commit -m "test(movement): verify Kalis movement determinism"
```

### Task K6: Calibrate, reject bad values, and record activation evidence

**Files:**

- Modify: `docs/plans/movement/kalis.md` only for measured results
- Modify shared calibration artifact/tool only if assigned by `README.md`

**Step 1: Run count tiers**

Run 1v1 and all Kalis-relevant 2v2 cases, then curated 1v2, 2v3, 3v5, 4v4,
5v5, 8v8, 100v100, and 250v250 scenarios for solo and explicit-V2 shielded
Kalis. Use the same seeds and geometries for paired comparisons.

**Step 2: Record observations**

Capture preferred-distance occupancy, commitment/recovery ticks,
disengagement entries/exits, isolation time, ally-lane conflicts, blocked
moves, pursuit separation, ordered events, state/event hashes, runtime, and
warm-tick allocations. Do not persist derived observations in battle snapshots.

**Step 3: Apply rejection criteria**

Reject the candidate row if any of these occur:

- a favorable count increases physical movement speed;
- solo or shielded Kalis can reverse at full speed during commitment/recovery;
- a threshold equality differs by iteration order;
- shielded Kalis is computed by applying a second runtime shield multiplier;
- any Kalis 1v1 geometry becomes an indefinite no-contact orbit in the bounded
  calibration window;
- 1v2 cannot produce disengagement with an open exit;
- shielded pairs form persistent wall-like equal spacing;
- Kalis is universally dominant or has no viable individual and group role;
- V1–V4 digests move;
- identical seeds diverge; or
- the shared 250v250 runtime/allocation budget fails.

**Step 4: Tune whole rows only**

Change a materialized profile value in one place, rerun K1–K6, and record the
reason. Never add matchup-specific exceptions to rescue a failed candidate.

**Step 5: Run the canonical gate**

```powershell
./scripts/verify.ps1
```

Expected: locked restore, formatting, Release build, Core/client tests, and
the 200-agent/10,000-tick determinism workload all pass. Record actual output
in the shared plan's verification section; do not claim manual spectator
validation from this gate.

## K6 measured results

Measured on 2026-07-30 against branch `movement-kalis`, on Windows
10.0.26200, .NET 10.0.10, X64, 20 cores. Every run named combat preset
`PrecolonialPhilippinesV2` explicitly, because it is the only preset
fielding all six canonical loadouts and therefore the only one under which a
shielded Kalis warrior exists at all. Movement preset
`EquipmentRelativeFootworkV6` throughout — the preset this plan calls V5 was
renumbered by the shared foundation, and the code wins.

The count tiers were run through a throwaway in-process harness rather than
the headless runner, because the headless command line has no roster-count
option and the asymmetric tiers need one faction larger than the other. The
harness lived outside the committed tree and was deleted after the
measurement; the results below are what it printed. Every tier ran on seeds
1, 2, 3, 5, and 8, for at most 2,000 ticks, with both factions fielding the
same Kalis variant so the comparison between the two rows is paired.

### Count tiers

Agent-tick shares are of living agent-ticks. "In band" is the share of
living agent-ticks in which the warrior had a selected target at or inside
its own offset-adjusted preferred distance. Figures are the seed-1 run
except where a range is given across all five seeds.

| Tier | Row | Ticks to a result | In band | Disengage share | Refuse share | Regroup share |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| 1v1 | solo | 74–99 | 68% | 0% | 0% | 0% |
| 1v1 | shielded | 115–134 | 77% | 0% | 0% | 0% |
| 2v2 | solo | 89–109 | 71% | under 1% | 0% | 0% |
| 2v2 | shielded | 134–188 | 80% | 1–8% | 0% | 0% |
| 1v2 | solo | 251–256 | 15% | 30% | 0% | 0% |
| 1v2 | shielded | 270–286 | 35% | 27% | under 1% | 0% |
| 2v3 | solo | 154–176 | 67% | 24% | 1–2% | 0% |
| 2v3 | shielded | 147–216 | 64% | 21% | under 1% | 0% |
| 3v5 | solo | 112–149 | 50% | 23% | 3% | 0% |
| 3v5 | shielded | 141–173 | 67% | 18% | 2–6% | 0% |
| 4v4 | solo | 89–104 | 72% | 1% | 0% | 0% |
| 4v4 | shielded | 124–185 | 77% | 1–4% | 0% | 0% |
| 5v5 | solo | 99–116 | 70% | 1% | 0% | 0% |
| 5v5 | shielded | 155–253 | 80% | 2–5% | 1% | 0% |
| 8v8 | solo | 188–311 | 49% | 1–2% | 1–2% | 18–33% |
| 8v8 | shielded | 250–340 | 58% | 1–4% | 9–14% | 14–29% |
| 100v100 | solo | 1,389–2,000 | 8–20% | 0–15% | 7–13% | 54–68% |
| 100v100 | shielded | 2,000 (draw) | 1–6% | 0% | 22–49% | 38–75% |
| 250v250 | solo | 2,000 (draw) | 4–11% | 0% | 12–18% | 62–71% |
| 250v250 | shielded | 2,000 (draw) | 1–4% | 0% | 27–56% | 53–70% |

Every one of the hundred runs replayed to an identical state hash and an
identical outcome, so no seed diverged and no threshold equality depended on
iteration order.

### What the tiers say about the two rows

- **Both rows occupy their band rather than orbiting it.** In every tier up
  to 5v5 the warriors spend the majority of their living agent-ticks at or
  inside their own preferred distance, and every small-tier battle reaches a
  result well inside the bounded window. The shielded row occupies its band
  more of the time and takes roughly half again as long to settle a duel,
  which is the deliberate consequence of a slower approach, a longer
  commitment, and a longer recovery.
- **The 1v2 disengagement works on both rows.** A lone Kalis warrior against
  two enemies spends about 30% of its remaining life disengaging and loses,
  which is the intended shape: disengagement buys time and an exit, not a
  win.
- **3v5 behaves as the plan predicted, by two different routes.** The solo
  row enters on its own count (`5*2 >= 3*3`). The shielded row does not
  (`5*4 < 3*7`), yet it still disengages, because three against five puts the
  contingent in `Yield` and the shared posture step disengages every member
  unconditionally. The 7:4 threshold is therefore tolerant without leaving
  the shielded row unable to withdraw, which is exactly the open question the
  plan flagged and asked to be resolved openly.

### Rejection criteria

| Criterion | Verdict | Evidence |
| --- | --- | --- |
| A favourable count increases physical movement speed | Not triggered | `NoKalisProposalEverExceedsTheSpeedCeiling` and the per-cell pace ceiling in every 2v2 matrix cell |
| Full-speed reversal during commitment or recovery | Not triggered | Committed pace pinned at 3,300 and 3,000 basis points; `AnAcceptedAttackDoesNotCapTheMovementOfItsOwnTick` shows the cap binding from `T+1` |
| A threshold equality differs by iteration order | Not triggered | Reverse-caller-order equality on every 2v2 cell and on the crowded transition roster; all 100 tier runs replayed identically |
| Shielded Kalis computed by a second runtime multiplier | Not triggered | One materialised row resolved once by exact loadout key; `TheShieldedRowCarriesEveryApprovedValue` and `BothKalisRowsResolveUnderTheirExactLoadoutKey` |
| An indefinite no-contact orbit in any Kalis 1v1 geometry | Not triggered | All twelve directed 1v1 cells reach the preferred band, engage, commit, recover, and attack; every 1v1 tier settles in 74–134 ticks |
| 1v2 cannot produce disengagement with an open exit | Not triggered | 226 and 228 disengaging agent-ticks on the solo and shielded 1v2 tiers |
| Shielded pairs form persistent wall-like equal spacing | Not triggered at pair scale | Every shielded 2v2 tier run terminates in 134–188 ticks |
| Kalis universally dominant, or no viable individual and group role | **Not adjudicated** | Deliberately not measured. This plan forbids tuning toward equal win rates and this session asserted no winner anywhere; a cross-weapon viability judgement needs all five weapon sessions' rows and belongs to the shared calibration task |
| V1–V4 digests move | Not triggered | `MovementPresetFreezeTests`, `MovementProfileRegistrationTests`, and `DeterminismTests` all green; no profile value changed, so no content hash moved |
| Identical seeds diverge | Not triggered | 100 of 100 tier runs reproducible; `deterministic: true` on every benchmark below |
| The shared 250v250 runtime and allocation budget fails | **Fails, and not on account of anything this session owns** | See below |

### Performance, reported honestly

Like-for-like against the shared baseline shape — the headless benchmark,
combat preset V2 named explicitly, seed 1, 10,000 requested ticks:

| Movement preset | Agents | Measured ticks | Elapsed (ms) | p50 per tick (ms) | Outcome |
| --- | ---: | ---: | ---: | ---: | --- |
| `PersistentContingentsV4` | 200 | 1,279 | 335.1 | 0.120 | Faction0Victory |
| `EquipmentRelativeFootworkV6` | 200 | 10,000 | 3,716.6 | 0.335 | Draw |
| `PersistentContingentsV4` | 500 | 2,934 | 1,128.4 | 0.134 | Faction0Victory |
| `EquipmentRelativeFootworkV6` | 500 | 10,000 | 11,927.0 | 1.032 | Draw |

Raw elapsed is 11.1× at 200 agents and 10.6× at 500, far outside the 2.0×
and 2.5× ceilings. Normalised per measured tick it is 2.8× and 3.1×, still
outside them. Core allocation moved the other way: 142,640 bytes for the
full V6 run at 200 agents against V4's 154,976, and 322,328 against 338,736
at 500, so the new stages allocate nothing per tick.

The reason the elapsed figure fails is the one the foundation session
already recorded and deferred to T11: V6 does not terminate. V4 annihilates
one side by tick 1,279 and 2,934; V6 draws at the 10,000-tick limit with
151 and 279 survivors. The count tiers above show what the run degenerates
into — at 100v100 and 250v250 both Kalis rows spend 53% to 71% of their
living agent-ticks in `Regroup` and a further 12% to 56% in `Refuse`, with
`Disengage` at zero. `Regroup` is resolved by the shared contingent stage
from a `Hold` contingent, and `Refuse` is a lane-clearance finalisation;
neither is selected by any value in a Kalis profile row, and both appear
under the whole-roster benchmark where four of the six rows belong to other
sessions.

**No Kalis value was moved.** Nothing in the approved calibration ranges of
section 5 addresses a shared posture or a shared clearance rule, and tuning a
Kalis row to mask a program-level standoff is the kind of matchup-specific
rescue this plan's task K6 step 4 forbids. The budget re-measurement stays
with the shared calibration task, which owns all six rows and the shared
stages at once.

The warm-window allocation column the harness printed is not evidence and is
not reproduced here: the harness allocated inside its own measurement window,
so the figure describes the harness as much as the simulation. The
authoritative allocation evidence is `coreAllocatedBytes` above and the
Release bounded-allocation tests, which pass.

## Activation and rollback boundary

- Profile rows may merge behind the new movement preset while it remains
  opt-in.
- This plan does not authorize changing the combat default. The separately
  approved V2-to-V3 combat-default task owns that one-line scenario change and
  its repository-wide expectation updates.
- This plan does not authorize changing the movement default. A later,
  separately approved task may activate the new preset only after every
  equipment plan, exhaustive scenario generation, frozen-preset tests,
  determinism checks, performance thresholds, and manual checklist are
  complete.
- Rollback selects `PersistentContingentsV4` (or another explicitly requested
  frozen preset). It does not delete Kalis profiles, rewrite V1–V4 fixtures, or
  mutate combat V2/V3.
- Shielded Kalis remains reachable only through an explicit combat V2 scenario
  after combat V3 becomes default.
- If shared interface names differ, reconcile this file before execution; do
  not maintain adapters solely for plan wording.

## Completion criteria

- Both complete Kalis rows resolve by exact `CombatLoadout`.
- Shared code contains no `WeaponId.Kalis` behavior branch.
- Tall Hardwood behavior is selected once and not multiplied twice.
- Every equality rule above has a boundary test.
- All twelve directed Kalis-variant 1v1 cases and every mechanically selected Kalis-relevant
  2v2 composition cell execute.
- Explicit combat V2 tests preserve shielded Kalis after the planned default
  switch to combat V3.
- Frozen V1–V4 movement behavior remains byte-identical.
- Determinism, allocation, performance, role-viability, canonical gate, and
  manual activation requirements are honestly recorded.
