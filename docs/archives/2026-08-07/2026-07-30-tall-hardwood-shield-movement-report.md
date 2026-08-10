# Tall Hardwood Shield movement — session report

> **Archived: reference only.** This document is finished work, kept so the
> decision can be traced back to its reasoning. Do not execute it and do not
> cite it as the reason to change anything.

Date: 2026-07-30
Branch: `movement-shield`, based on `main` at `5afae08`.
Executes: [`docs/archives/2026-07-31/movement/tall-hardwood-shield.md`](../../archives/2026-07-31/movement/tall-hardwood-shield.md)
under the prompt in
[`docs/archives/2026-08-10/2026-07-30-weapon-movement-weapon-template.md`](../2026-08-10/2026-07-30-weapon-movement-weapon-template.md).
Consumes the handoff in
[`2026-07-30-weapon-movement-foundation-report.md`](2026-07-30-weapon-movement-foundation-report.md)
section 8.

This is the shield slice of the weapon-relative movement program. It covers the
two tall-hardwood-shield loadouts: shielded Kalis (`KS`) and shielded Itak
(`IS`).

## 1. Preflight

1. **Preset in force.** `MovementPresetId.EquipmentRelativeFootworkV6 = 6`, opt-in
   only. The weapon plan's `EquipmentRelativeFootworkV5` does not exist; that
   numeric value belongs to `PersistentContingentsV5`, the rank-aware leader
   scan.
2. **Combat preset used.** `CombatPresetId.PrecolonialPhilippinesV2`, named
   explicitly in every scenario, test, and benchmark in this session. It remains
   the only registered combat preset that fields both shielded loadouts. The
   shipped default is `PrecolonialPhilippinesV4`, which fields four solo
   loadouts and no shielded one, so a shielded roster survives only by naming V2.
3. **Foundation present.** Every shared symbol the weapon plan depends on exists
   and matches the handoff: `LoadoutMovementProfile`, `Facing16`, `FacingRules`,
   `WeaponMovementRules`, `LocalMovementContext`, `MovementContextQuery`,
   `MovementRouteRules`, the V6 registry entry, and
   `tests/Hukbo.Core.Tests/Movement/MovementScenarioMatrix.cs`.
4. **The two shield profile rows already existed.** The foundation session
   created `src/Hukbo.Core/Movement/Profiles/TallHardwoodMovementProfiles.cs` in
   commit `c6be88d`, carrying both rows. All thirty-two field values were
   independently confirmed against the weapon plan's table and match exactly.
   Task H1 step 4 was therefore already done, and this session added no
   production code at all.
5. **Baseline.** `./scripts/verify.ps1` on the untouched worktree exited 0 with
   every stage `PASS`: prerequisites, locked restore, formatting, Release build,
   Core and Client tests (Core 1,623 passed; Client 2,829 passed), and the
   200-agent, 10,000-tick, seed-1 headless determinism workload. The baseline was
   green, so nothing here is inherited red.

### Where a plan document disagreed with the code

The code wins in every row below. None of these documents was edited; correcting
them is separate work.

| Topic | The weapon plan says | The code or the handoff says |
| --- | --- | --- |
| Live combat default | Combat V2 is the live default, and a separately approved task will switch it to V3 | The default is already `PrecolonialPhilippinesV4`. Shared task T2 was declared obsolete by the foundation and never executed, so the plan's H6 step 1 has no target |
| Solo-versus-shield differences | Eight paired differences | Thirteen of sixteen fields differ for `KS` and fourteen of sixteen for `IS`. The plan's table omits committed pace, deceleration, commitment ticks, pursuit support, the six opponent offsets, and shielded Itak's reengage rise from 10,000 to 11,000 |
| Frozen fixture scope | V1 through V4 movement fixtures stay byte-identical | V1 through V6 are all frozen; V6 shipped with the foundation |
| Facing penalty | Not stated as adopted, but the research proposed a 0.88 multiplier | No facing penalty exists. Both shield rows turn at the solo value of two sectors, because 0.88 is unrepresentable in sixteen sectors and was deliberately rejected |
| Shielded Kalis preferred distance | 13,000 | 13,000. The conflicting 1.10-reach sentence lives in the Kalis plan, not this one, and is the wrong figure |
| Second-threat lane omission | Absent from the plan entirely | A real shared rule: with two immediate enemies the direct lane is dropped only when its endpoint is strictly closer to the second threat than the tick-start position, and `Commit`'s lone direct candidate is exempt. Covered here anyway |
| `MovementRuleset` documentation | — | `docs/archives/2026-07-31/movement/README.md` still says "V5" throughout for the preset that shipped as V6, and `MovementPresetRegistry.cs` still carries a comment claiming no `BattleSimulation` path consults the V6 flag. Both are stale and neither is this session's to fix |

## 2. What landed

Three new test files, 5,810 lines, 447 test cases. No production file was created
or changed, and no shared file was touched.

| Commit | Change |
| --- | --- |
| `47f5ab9` | `TallHardwoodMovementProfileTests.cs` — the two rows and the shield envelope (plan tasks H1 and H2) |
| `3cb5083` | `TallHardwoodMovementTests.cs` — count thresholds and opponent spacing (H3, first half) |
| `f6470e7` | `TallHardwoodMovementTests.cs` — facing, pace, lanes, and tie stability (H3 second half and H4) |
| `5ccfab8` | `TallHardwoodMovementScenarioTests.cs` — the shield slice of the scenario matrix (H5) |
| `c95690d` | `TallHardwoodMovementScenarioTests.cs` — matchup geometry, group cases, asymmetric counts (H5) |
| `0876a3e` | `TallHardwoodMovementScenarioTests.cs` — explicit combat V2 roster and legacy neutrality (H6) |

The plan names two test files. A third, `TallHardwoodMovementScenarioTests.cs`,
carries the matchup and roster coverage that the plan routed through a shared
`WeaponMovementMatchupTests.cs`. That shared file does not exist and no session
created it; the Kampilan and Itak sessions both delivered the same coverage
inside their own weapon-named files, and this session follows that precedent
rather than creating a shared file it does not own.

## 3. Tests

Focused, Release configuration:

| Filter | Result |
| --- | --- |
| `FullyQualifiedName~TallHardwoodMovementProfileTests` | Passed. Failed 0, Passed 41, Skipped 0 |
| `FullyQualifiedName~TallHardwoodMovementTests` | Passed. Failed 0, Passed 121, Skipped 0 |
| `FullyQualifiedName~TallHardwoodMovementScenarioTests` | Passed. Failed 0, Passed 285, Skipped 0, 3 s |

Shared regression suites, all green: `MovementPresetFreezeTests`,
`MovementPresetRegistryTests`, `MovementProfileRegistrationTests`,
`DeterminismTests`, `FootworkPhaseRulesTests`, `TacticalPostureRulesTests`,
`MovementRouteRulesTests`, `MovementScenarioMatrixTests`, and
`MovementPipelineIntegrationTests`.

The plan's H3 step 6 names a shared `WeaponMovementRulesTests` suite. No such
file exists; the nearest shared suites are `FootworkPhaseRulesTests` and
`TacticalPostureRulesTests`, which were run in its place.

Most assertions were green on their first successful compile, because they pin
behaviour the foundation had already implemented. That is reported as observed
rather than dressed up as a red-then-green cycle. Where a test passed
immediately, its load-bearing quality was confirmed by inverting one expectation,
observing the single expected failure, and restoring the file. Two assertions in
`TallHardwoodMovementTests.cs` and five in `TallHardwoodMovementScenarioTests.cs`
were genuinely red first; in every case the fault was in the probe construction,
not in shared code.

## 4. Gate

`./scripts/format.ps1 -Verify` → `[PASS] Formatting verification completed.`
Formatted 0 of 396 files.

`./scripts/verify.ps1`, run once by the orchestrator after integration, **exit
0**:

```
[PASS] Required prerequisites and repository configuration are present.
[PASS] Locked package restore completed.
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
     Passed: 2070          (Core)
     Passed: 2829          (Client)
[PASS] Release repository tests completed.
  "outcome": "Faction1Victory",
  "eventHash": "AC55684F24D39344",
  "stateHash": "1B73FC5923879AA0",
  "deterministic": true,
  "firstMismatchTick": null,
  "coreAllocatedBytes": 161168,
  "movementMetrics": {
    "approachAgentTicks": 0, "engageAgentTicks": 0, "commitAgentTicks": 0,
    "recoverAgentTicks": 0, "refuseAgentTicks": 0, "disengageAgentTicks": 0,
    "regroupAgentTicks": 0, "pursueAgentTicks": 0, "postureTransitions": 0,
    "facingStepsTurned": 0, "disengagementEntries": 0, "conflictDenials": 0
  }
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

Core rose from 1,623 to 2,070 passing cases, which is exactly the 447 cases the
three new files add. Client is unchanged at 2,829. The seed-1 state hash and
event hash are byte-identical to the baseline, so no replay moved. The all-zero
`movementMetrics` block confirms again that no V6 stage runs under the shipped
default.

## 5. Determinism

No content hash moved, because no profile value moved. The V6
`MovementRuleset.ContentHash` literal `0x0FFE5D202B324D25` in
`MovementPresetRegistryTests.cs` and the V6 trajectory digest in
`MovementPresetFreezeTests.cs` are untouched and passing, as are the V1 through
V5 fixtures. This session added no production code, so there was nothing for a
hash to record.

## 6. Calibration evidence

Twenty measured runs, one discarded warm run per cell, combat
`PrecolonialPhilippinesV2` named explicitly, 10,000 requested ticks, seeds
1, 2, 3, 5, 8, on the same machine as the foundation baseline (Windows
10.0.26200, .NET 10.0.10, X64, 20 cores).

| Movement preset | Agents | Median elapsed (ms) | Median `p50Milliseconds` | Median measured ticks | Outcomes |
| --- | ---: | ---: | ---: | ---: | --- |
| `PersistentContingentsV4` | 200 | 375.93 | 0.06 | 2,037 | 3 Faction0, 2 Faction1 |
| `EquipmentRelativeFootworkV6` | 200 | 3,222.85 | 0.29 | 10,000 | 5 Draw |
| `PersistentContingentsV4` | 500 | 1,297.19 | 0.23 | 2,934 | 4 Faction0, 1 Faction1 |
| `EquipmentRelativeFootworkV6` | 500 | 10,171.27 | 0.88 | 10,000 | 5 Draw |

**Budget verdict, reported three ways because the three disagree.** The ceilings
are 2.0× at 200 agents and 2.5× at 500.

| Reading | 200 agents | 500 agents | Verdict |
| --- | ---: | ---: | --- |
| Median elapsed, the metric the shared plan names | 8.57× | 7.84× | **Fails** |
| Median `p50Milliseconds`, the fairest per-tick measure | 4.83× | 3.83× | **Fails** |
| Median elapsed divided by median measured ticks | 1.75× | 2.30× | Passes |

This reproduces the Kampilan session's finding independently, and the honest
reading is that the budget fails. The third row passes only because it divides
V6's full 10,000-tick run by V4's roughly 2,000-tick run, which rewards V6 for
never terminating. Allocation is the one clean pass: per measured tick, V6
allocates less than V4 (196 bytes against 410 at 200 agents), and the Release
allocation tests in the gate are green.

**The dominant cause is non-termination, not stage cost.** Every V6 run at both
sizes ends in a `Draw` at the tick limit with both sides substantially alive —
40 to 78 survivors per side at 200 agents, 106 to 156 at 500. Every V4 run
terminates with one side annihilated. The refusal and denial counters say why:
`refuseAgentTicks` between 658,162 and 1,140,221 and `conflictDenials` between
42,436 and 205,488 in a single 200-agent run. The clearance and disengage rules
bind hard enough to produce a standoff equilibrium after the first few thousand
ticks.

That is a calibration property of the provisional values, not a defect in the
foundation and not something the shield slice can fix — see section 7.

## 7. Handoffs

No defect was found in shared runtime code, and no failing test was left in
place. Four items belong to someone else.

### 7.1 This session cannot calibrate its own rows

Task H7 step 5 of the weapon plan — reject or tune an owned row, one field at a
time — is not executable by a weapon session, and no weapon session has executed
it.

Every one of the sixteen fields per row reaches the V6 content hash and the V6
trajectory digest. Those are pinned in
`tests/Hukbo.Core.Tests/MovementPresetRegistryTests.cs` and
`tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs`, both of which the weapon
template forbids this session to edit, and the profile file's own documentation
records that a change after the digest ships requires appending a new preset
version rather than editing this one. Any retuning of `KS` or `IS` is therefore a
V7 decision for the shared preset owner, taken with the whole preset in view.

The evidence for that decision is in section 6 and in 7.2 through 7.4.

### 7.2 A shield bearer in continuous contact never reaches its disengage test

Verified, and the most consequential behavioural finding of this session.

Both shield rows spend three commitment ticks plus three recovery ticks, six
ticks per accepted attack. Under combat V2, shielded Kalis reloads in five ticks
and shielded Itak in four (`PhilippineCombatPresetV2.cs`, `cooldownTicks: 5` and
`cooldownTicks: 4`). The commitment and recovery steps of the footwork ladder sit
ahead of the support-ratio steps, deliberately, so that local pressure cannot
cancel a commitment. The consequence is that a shield bearer who is landing blows
re-enters `Commit` before the ladder ever reaches its disengage test, and cycles
`Commit, Commit, Commit, Recover, Recover, Commit` indefinitely.

Its carefully specified disengage threshold is therefore unreachable while it is
in contact. Kampilan's seven-tick reload against the same six-tick lifecycle
leaves a one-tick window; the shield rows leave none. This is the specified step
ordering working exactly as written, so it is a tuning question and not a bug,
but it means the `7:4` and `3:2` thresholds do less work in a real battle than
the plan assumes.

### 7.3 The shipped entry thresholds sit outside the research band

`docs/research/movement/tall-hardwood-shield.md` proposes entering disengagement
around an ally-to-enemy ratio of 0.67 to 0.80 and leaving near 0.85 to 1.00.

| Row | Shipped entry | As ally-to-enemy | Research band | Inside? |
| --- | --- | --- | --- | --- |
| `KS` | 17,500 basis points, 1.75 enemies per ally | 0.571 | 0.67 to 0.80 | No, below the band |
| `IS` | 15,000 basis points, 1.50 enemies per ally | 0.667 | 0.67 to 0.80 | At the very bottom edge |
| Both | 11,000 basis points, 1.10 enemies per ally | 0.909 | 0.85 to 1.00 | Yes |

Both shield rows tolerate more pressure before disengaging than the research
band suggested. That is a defensible reading of "protected deliberation", and
every number carries its provisional label, so no historical claim is affected.
It is recorded here because the departure originates in the plan's own table and
the design document that superseded it, not in the code, and because nobody has
signed off on it as a deliberate choice.

### 7.4 The body-contact pace clamp is unreachable from ordinary duel geometry

Two shield bearers settle at reach and never touch, so the collision-denial pace
clamp cannot be reached from a merely closing duel. Proving it needs agents
started at exact tangency with cooldowns pinned, which is what the test does. The
clamp is correct; it is just not exercised by ordinary approach.

## 8. Left out, and why

Scaling this work down is not this session's call, so everything below is
reported rather than quietly dropped.

| Item | Why |
| --- | --- |
| H1 step 4, add the two rows | Already landed by the foundation in `c6be88d`. Verified identical to the plan's table |
| The unsupported-armor rejection case in H1 step 2 | `ArmorId` declares only `LightOrganic`, so no invalid armor value can be constructed without casting a bogus enum. Shielded Kampilan and shielded Wasay are both covered, and the omission is documented in the test |
| H3 step 6, the shared `WeaponMovementRulesTests` suite | The file does not exist. `FootworkPhaseRulesTests` and `TacticalPostureRulesTests` were run instead |
| H5's shared `WeaponMovementMatchupTests.cs` | Shared-owned and never created. The coverage is delivered in `TallHardwoodMovementScenarioTests.cs`, following the Kampilan and Itak precedent |
| H6 step 1, change the default assertion ahead of a V2-to-V3 switch | Obsolete. Shared task T2 was never executed and the combat default is already V4 |
| H7 step 5, tune an owned row | Not executable by a weapon session. See section 7.1 |
| The 100v100 and 250v250 runs as unit tests | They are mass workloads and belong to the benchmark script, not to a suite inside the canonical gate. Delivered as the calibration evidence in section 6, at 200 and 500 agents across five seeds. The Kampilan session recorded the same decision |
| "No universal shield dominance" and "no viable shieldless entry" | Both are outcome statistics. Asserting them would mean asserting a win rate, which the plan and the standards forbid. They are left to the calibration evidence |
| Two focused sub-clauses, "uses a shallow lane" and "avoids the planted commitment line" | Neither has a metric that distinguishes it from the lane-clearance and rigid-line assertions already present, so they were folded into those rather than asserted vacuously. All twelve focused matchup cells do execute |
| `RunReport.movementMetrics` inside a unit test | Reachable, but `HeadlessRunner` takes wall-clock timings and writes output, which would import the wall clock into the gate. The equivalent observations come from per-agent state and from `BattleSimulation.MovementConflictDenials` |
| Manual spectator checks | Every interactive row in `docs/development/testing.md` remains `PENDING`. No agent flipped one, and nothing was `BLOCKED` |
| Default activation of V6 | Expressly out of scope. `Scenario.MovementPreset` remains `PersistentContingentsV4` |

## 9. Definition-of-done check

- Preflight recorded, including the preset in force, the combat preset used, and
  every place a plan document disagreed with the code. ✓
- Both shield rows resolve under their exact `CombatLoadout` keys, are immutable,
  carry the approved values, never fall back to a solo row, and are documented as
  provisional gameplay tuning. ✓
- Boundary tests pass at one raw unit outside, exact equality, and one raw unit
  inside, for entry distance, ally clearance, disengage entry, and disengage
  release, asserting the shared convention rather than a weapon-local one. ✓
- Pace caps, facing step caps, acceleration and deceleration, commitment and
  recovery durations, and collision-clamped retained pace are pinned by test. ✓
- Count behaviour proved: self-inclusion, dead and out-of-radius exclusion,
  hysteresis between the two thresholds, zero-hostile behaviour, and integer
  overflow safety, with no division and no float. ✓
- Matchup and group coverage runs through the shared scenario matrix — 11 of 21
  one-versus-one cells and 176 of 231 team matchups — with no equal-win-rate
  assertion anywhere. ✓
- Every existing preset's fixtures are byte-identical and no content hash moved. ✓
- No shared file was edited. ✓
- `./scripts/verify.ps1` exit 0, run once by the orchestrator, output in section
  4. ✓
- No manual smoke-checklist row flipped. ✓
- Performance budget: fails on median elapsed and on median per-tick cost, passes
  on allocation. Reported honestly in section 6; the cause is non-termination
  under the provisional values, and the remedy is a V7 calibration decision this
  session cannot take. ⚠
