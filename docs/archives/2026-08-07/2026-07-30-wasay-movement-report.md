# Wasay — War Axe: weapon-relative movement implementation report

> **Archived: reference only.** This document is finished work, kept so the
> decision can be traced back to its reasoning. Do not execute it and do not
> cite it as the reason to change anything.

Date: 2026-07-30
Branch: `movement-wasay`, rebased onto `main` at `5afae08`.
Executes: [`docs/archives/2026-07-31/movement/wasay.md`](../../archives/2026-07-31/movement/wasay.md), tasks W1 through W5.
Governed by: [`docs/archives/2026-07-31/movement/README.md`](../../archives/2026-07-31/movement/README.md) and the shared
foundation handoff in
[`2026-07-30-weapon-movement-foundation-report.md`](2026-07-30-weapon-movement-foundation-report.md).

This is the Wasay slice of the five-weapon movement programme. It contributes
tests only. It does not change any simulation source file, and it does not
activate the new movement preset.

## 1. Preflight

| Item | Finding |
| --- | --- |
| Preset in force | `MovementPresetId.EquipmentRelativeFootworkV6 = 6`. The weapon plans call it `EquipmentRelativeFootworkV5`; that numeric value belongs to `PersistentContingentsV5`, the rank-aware leader scan. |
| Combat preset used | Set explicitly on every scenario. Solo-only cells use `CombatPresetId.PrecolonialPhilippinesV4`, the current default. Every cell containing a shielded loadout uses `CombatPresetId.PrecolonialPhilippinesV2`, which is the only preset fielding all six loadouts. Nothing relies on the default. |
| Foundation present | Yes. `LoadoutMovementProfile.cs`, `Facing16.cs`, `FacingRules.cs`, `WeaponMovementRules.cs`, `LocalMovementContext.cs`, `MovementContextQuery.cs`, `MovementRouteRules.cs`, `Profiles/WasayMovementProfile.cs`, the V6 registry entry, and `tests/Hukbo.Core.Tests/Movement/MovementScenarioMatrix.cs` all exist and match the shared contract. |
| Wasay profile row | Already shipped by the foundation session, carrying exactly the values in the weapon plan's section 5 and the foundation design document's section 13. Task W1 was therefore an assert-only task: no red phase existed to observe. |
| Baseline gate | `./scripts/verify.ps1` on the untouched worktree: **exit 0**, `[PASS] Canonical repository verification completed.` The gate was already green before this session started. |

### Where a plan document disagreed with the code

In every case the code won, as the prompt requires. The plan documents were not
edited; correcting them is a separate task.

| Plan text | What the code actually does |
| --- | --- |
| `EquipmentRelativeFootworkV5` throughout section 8 and section 10 | The registered preset is `EquipmentRelativeFootworkV6 = 6`. |
| Section 6: the preferred engagement distance "stops ordinary forward approach" | It does not. Preferred distance selects the `Engage` phase and nothing more; the agent keeps closing toward the target and the unchanged post-movement combat reach gate stays authoritative. No test in this session asserts a stop line. |
| Section 6: preferred distance is a flat `108/100 × AttackRangeRaw` | The effective figure is `PreferredDistanceBasisPoints + OpponentDistanceOffsetBasisPoints[opponentIndex]`, so the flat multiple holds only against another Wasay, whose offset cell is zero. Against `KP`, `WA`, `KA`, `IT`, `KS`, `IS` the effective basis points are 11300, 10800, 11050, 11300, 11050, 11300. All six are pinned by test. |
| Section 6 leaves the clearance equality convention conditional | Resolved: intrusion is strictly closer than the radius, so exact equality is clear. The binding radius is the larger of the two agents' clearance radii, not the actor's alone — a point the plan does not mention at all. Both are pinned. |
| Section 7: faction totals "cannot force every Wasay unit to retreat in sync" | False as a statement about phase. Posture step 6 is unconditional: every member of a `Withdraw` or `Yield` contingent takes phase `Disengage`. Per-agent variation lives in the route, not the phase. The implemented truth is pinned by test and the difference is documented in that test's XML documentation. |
| Section 8 and section 9: "V1–V4 fixture results must remain byte-identical" | The correct set is V1 through V5; `PersistentContingentsV5` exists and carries its own trajectory digest fixture. V1 through V5 were asserted. |
| Section 8: use `MovementScenarioMatrix` to *run* scenarios | The shipped matrix is a pure enumerator. It never constructs a simulation and has no run helper; the runs are this session's own code. The matrix file was called, never edited. |
| Section 8, W1 step 5: stage `WasayMovementProfile.cs` | The file already existed and did not change, so it was not staged. |
| Section 5: "add exactly one catalog entry for `CombatLoadout(Wasay, LightOrganic, None)`" | `CombatLoadout` now carries a fourth `RankId` component. The movement profile is keyed on `(WeaponId, ArmorId, ShieldId)` and the constructor normalises rank away. Rank-independence is pinned by test. |

## 2. What landed

Five commits, all tests, no source file touched.

| Commit | Task | Change |
| --- | --- | --- |
| `d425670` | W1 | `tests/Hukbo.Core.Tests/Movement/WasayMovementProfileTests.cs`, new, 264 lines. Pins all sixteen scalars and all six offset cells, the loadout key, rank-independence of resolution, the absence of any Wasay-plus-shield fallback, the row's position at canonical index 1, its inertness under the shipped default preset, and each field's approved calibration range. |
| `73d8884` | W2 | `tests/Hukbo.Core.Tests/Movement/WasayMovementTests.cs`, new, 886 lines. Entry distance, ally clearance, direction-band pace caps, facing budget, acceleration and deceleration, collision-clamped retained pace, commitment and recovery durations, and the movement-speed ceiling. |
| `c7f1496` | W3 | Same file, +641 lines. Local-count hysteresis, self-inclusion, dead and out-of-radius exclusion, zero-hostile behaviour, overflow safety, permuted-span equivalence against the naive oracle, stable entity-identifier ordering, caller-order canonicalisation, and the local-over-global precedence. |
| `50e5094` | W4 | Same file, +1034 lines. A test-local observation harness plus the six generated 1v1 Wasay cells and the homogeneous and mixed 2v2 cells. |
| `78e5e0d` | W5 | Same file, +1180 lines. Asymmetric and mixed group fixtures at 1v2, 2v3, 3v5, 4v4, 5v5 and 8v8, plus replay, baseline-step, bounded-flip and no-progress invariants. |

Final diff against `main`: two files, 4005 insertions, zero deletions, zero
files under `src/` changed.

## 3. Tests

| Filter | Result |
| --- | --- |
| `FullyQualifiedName~WasayMovementProfileTests` | `Passed!  - Failed: 0, Passed: 29, Skipped: 0, Total: 29` |
| `FullyQualifiedName~WasayMovementTests` (after W5) | `Passed!  - Failed: 0, Passed: 120, Skipped: 0, Total: 120, Duration: 1 s` |
| `MovementPresetFreezeTests` and `MovementPresetRegistryTests` and `DeterminismTests` | `Passed!  - Failed: 0, Passed: 45, Skipped: 0, Total: 45` |
| Whole Core suite, Release, after rebase onto `main` | `Passed!  - Failed: 0, Passed: 1772, Skipped: 0, Total: 1772, Duration: 17 s` |
| `./scripts/format.ps1 -Verify` after rebase | `Formatted 0 of 395 files.` … `[PASS] Formatting verification completed.` |

67 test methods across the two files, expanding to 149 executed cases.

## 4. Calibration

**Nothing was moved.** `src/Hukbo.Core/Movement/Profiles/WasayMovementProfile.cs`
is byte-identical to the row the foundation shipped. Every value stayed at its
approved setting, so the V6 `MovementRuleset.ContentHash` pinned literal and the
V6 trajectory digest fixture were both left untouched, and both freeze suites
pass unchanged.

This matters beyond bookkeeping: because `StateHasher` folds
`MovementRuleset.ContentHash` for V6, moving any Wasay scalar would have moved
every V6 state hash at tick zero and invalidated the digest fixture — neither of
which this session is permitted to edit. Had a value needed to move, the correct
action was to stop and report, and that is what the instructions given to the
implementation agents said.

Recorded behaviour from the duel fixtures, at 600 ticks and seed 1, non-lethal
construction so that movement can be observed without an early-termination
confound:

| Cell | First `Engage` | First landed blow | `Commit` ticks | `Recover` ticks | Max step | Blows landed |
| --- | --- | --- | --- | --- | --- | --- |
| Wasay versus Kampilan | 18 | 22 | 288 | 286 | 481 | 42 |
| Wasay versus Wasay (west) | 106 | 19 | 268 | 266 | 481 | 53 |
| Wasay versus Kalis | 18 | 19 | 292 | 290 | 481 | 57 |
| Wasay versus Itak | 18 | 19 | 292 | 290 | 481 | 52 |
| Wasay versus shielded Kalis | 67 | 19 | 286 | 284 | 481 | 49 |
| Wasay versus shielded Itak | 18 | 19 | 292 | 290 | 481 | 42 |

The Wasay's own maximum step is 481 raw against a `MovementSpeedRaw` of 512,
consistent with its 9400 basis-point forward cap. No step anywhere exceeded the
common human baseline.

## 5. Gate

`./scripts/format.ps1 -Verify` → `[PASS] Formatting verification completed.`

`./scripts/verify.ps1`, run once by the integrator after rebasing onto `main`,
**exit 0**. Tail of the real output:

```
  "outcome": "Faction1Victory",
  "faction0Survivors": 0,
  "faction1Survivors": 6,
  "eventHash": "AC55684F24D39344",
  "stateHash": "1B73FC5923879AA0",
  "deterministic": true,
  "firstMismatchTick": null,
  ...
  "coreAllocatedBytes": 154976,
  "movementMetrics": {
    "approachAgentTicks": 0,
    "engageAgentTicks": 0,
    "commitAgentTicks": 0,
    "recoverAgentTicks": 0,
    "refuseAgentTicks": 0,
    "disengageAgentTicks": 0,
    "regroupAgentTicks": 0,
    "pursueAgentTicks": 0,
    "postureTransitions": 0,
    "facingStepsTurned": 0,
    "disengagementEntries": 0,
    "conflictDenials": 0
  }
}
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

The canonical seed-1 hashes `1B73FC5923879AA0` and `AC55684F24D39344` are
identical to the baseline recorded by the foundation session, so no replay
moved. The all-zero movement metrics are correct and expected: the gate runs the
shipped default movement preset, under which the Wasay row is inert.

## 6. Mass workloads

Single machine, single seed 1, requested 10,000 ticks, combat preset
`PrecolonialPhilippinesV4` named explicitly on every run. This is one seed, not
the five-seed median the shared plan's task T0 describes; the foundation
session's five-seed medians remain the fuller measurement.

| Movement preset | Agents | Measured ticks | Elapsed (ms) | Per measured tick (ms) | `coreAllocatedBytes` | Outcome |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| `PersistentContingentsV4` | 200 | 981 | 282.53 | 0.2880 | 154,976 | Faction1Victory |
| `EquipmentRelativeFootworkV6` | 200 | 10,000 | 3,155.62 | 0.3156 | 142,640 | Draw |
| `PersistentContingentsV4` | 500 | 2,263 | 852.67 | 0.3768 | 338,736 | Faction0Victory |
| `EquipmentRelativeFootworkV6` | 500 | 10,000 | 6,533.52 | 0.6534 | 314,112 | Draw |

Every run reported `deterministic: true` with a null `firstMismatchTick`.

Read against the shared budget of `2.0×` at 200 agents and `2.5×` at 500:

- **Raw elapsed fails**: 11.2× at 200 agents and 7.7× at 500. That ratio is
  almost entirely a termination artefact. V4 ends the battle after 981 and 2,263
  ticks; V6 runs the full 10,000 and ends in a draw, so it is being charged for
  four to ten times as many ticks.
- **Per measured tick passes**: 1.10× at 200 agents and 1.73× at 500, both
  inside the ceilings. The caveat the foundation recorded still applies and is
  repeated here honestly — V6's ticks carry more living agents on average, so
  normalising per tick is generous to V4 rather than to V6.
- **Allocation passes**: `coreAllocatedBytes` under V6 is *lower* than under V4
  at both sizes, so the new movement stages allocate nothing per tick.

The standoff draw under the shipped provisional defaults is the same behaviour
the foundation session reported. It is calibration evidence about the shared
disengage thresholds and clearance radii, not a defect in the Wasay row, and it
is unchanged by this session because this session changed no value.

## 7. Handoffs

No shared runtime file was edited, and no failing test was left behind. Three
items belong to the shared integration owner rather than to this weapon slice.

1. **A warrior with a ready blow cannot disengage while locally outnumbered.**
   `WeaponMovementRules.ResolveProvisionalFootwork` places the `Commit` and
   `Recover` decrements at steps 3 and 4, ahead of the ratio steps 6 and 7.
   Combined with a support radius of six body diameters against an attack reach
   of roughly one, any hostile close enough to count toward the 2:1 ratio is
   also close enough to be struck, so an engaged Wasay re-enters `Commit` on
   every accepted attack and never writes `Disengage` to authoritative state.
   Observed directly: an isolated Wasay measured 9 to 12 ticks at or above 2:1
   local pressure and 0 ticks of `Disengage`. Setting `AttackCooldownRemaining`
   high before the run produced 130 pressure ticks and 129 `Disengage` ticks
   from the same geometry. This appears to be behaviour as designed rather than
   a defect, but it means the count-sensitive posture table in the weapon plans'
   section 6 is not spectator-discoverable for an engaged warrior — which
   `SIMULATION-GAME-STANDARDS.md` section 10 asks about directly. It deserves a
   decision, not a silent acceptance.
2. **`MovementScenarioMatrix` has no group-roster generator.** It supplies the
   canonical loadouts, the 21 one-versus-one pairs, the 21 two-member teams, the
   231 team matchups, and the shielded-cell combat-preset rule. Nothing produces
   1v2, 2v3, 3v5, 4v4, 5v5 or 8v8 rosters, so the group placements in this
   session's fixtures are local. If matrix-generated group rosters are wanted,
   the generator belongs in `MovementScenarioMatrix.cs`, which no weapon session
   may edit.
3. **The shielded Kalis row is the weakest in the 8v8 fixture**, spending 162 of
   400 ticks in `Refuse` and not reaching its first `Commit` until tick 259,
   against 10 landed blows. Not this session's row to tune; flagged for whoever
   owns the shielded rows.

A fourth observation, smaller: the shielded Itak never records a
`FootworkPhase.Engage` tick against a Wasay in these fixtures, transitioning
from `Approach` straight to `Commit` because its attack is accepted before its
own narrower preferred-distance band is entered. This is consistent with the
documented rule that preferred distance is not a stop line, but it is worth a
sanity check by whoever owns that row.

## 8. Numeric bounds reported rather than asserted

The weapon plan forbids smuggling an unreviewed acceptance threshold into a
test. Four bounds were needed and are reported here instead of asserted.

1. **The shared 25 percent phase-flip ceiling** (`README.md`, task T11 step 7)
   is sourced but the shipped rows do not meet it. Measured over ticks 101 to
   400: Wasay 70 to 104 flips per 300 ticks, which is 23.3 to 34.7 percent — the
   pure four-tick commitment plus four-tick recovery rhythm sits at exactly 25.0
   percent on its own. Kalis reaches 60 percent and Itak 50 percent. A weapon
   session has no authority to relax a shared bound, so the tests assert only a
   structural bound and document the criterion. This is a calibration decision
   for the shared integration owner.
2. **No shared bound exists for minimum ally separation in mixed groups.**
   Observed minima across the group fixtures: 1641, 1244, 1024, 1234, 1792,
   1536 and 1160 raw units. Five of the seven sit below the Wasay's own 1792-raw
   clearance radius, because the friendly-clearance pass governs accepted
   *proposals* while the body-collision resolver governs final separation. The
   assertion was therefore made only on the homogeneous Wasay column, where it
   holds exactly.
3. **No shared bound exists for disengagement churn.** Up to 82 entries and 82
   releases in 400 ticks for an isolated Wasay, and 73 of each for Kalis.
   Bounded, but high.
4. **No shared bound exists for a minimum number of landed attacks per
   fixture.** Asserted as strictly greater than zero only where the plan's own
   prose requires contact.

The one numeric bound that *is* asserted — the 250-tick no-progress limit — comes
from `docs/archives/2026-07-31/movement/README.md`, task T10 step 6.

## 9. Left out, and why

- **Activation.** `Scenario.MovementPreset` remains `PersistentContingentsV4`.
  The Wasay row is inert unless V6 is selected explicitly, which is correct;
  activation is a separate, separately approved task.
- **The 100v100 and 250v250 scenarios as unit tests.** They ran as the
  `benchmark.ps1` workloads in section 6, which is where the plan puts them.
- **A five-seed median for the performance comparison.** The plan's W5 step 4
  names seed 1 only, and that is what was run. Section 6 says so plainly rather
  than implying a median.
- **The equal-win-rate question.** Deliberately never assessed and never tuned
  toward, per the plan: balance here means role viability, not equal duel
  outcomes.
- **Every interactive smoke-checklist row in `docs/development/testing.md`.**
  Untouched, still `PENDING`. No agent flipped a row, and neither did the
  integrator; those rows require a human at an interactive desktop.
- **Correcting the plan documents themselves.** Section 1 of this report lists
  every disagreement found. The plans are design records and editing them is a
  separate task.
