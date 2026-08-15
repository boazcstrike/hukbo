# In-fight evasion — plan

Date: 2026-08-15

Executes the in-fight evasion design document dated 2026-08-15. Where this plan
and that design disagree, the design wins.

## Rules binding every task

- One task is one agent's work. No task may commit or stage anything; the
  orchestrator stages and commits.
- File sets are disjoint inside any `PARALLEL-GROUP-n`. Tasks marked `SERIAL`
  run alone.
- **`src/Hukbo.Core/Simulation/BattleSimulation.cs` is the shared seam. Every
  task touching it is `SERIAL` and never runs beside another task.**
- Each task names the exact test file it adds or updates. A task that adds
  behaviour without naming a test file is malformed.
- Task 1 is measurement only and changes no behaviour.
- The client default flip is last, after the gate is green.
- No task may weaken, delete, or rebaseline a pinned literal to go green. If a
  frozen literal moves, the task that moved it is wrong.

## Ordered tasks

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| 1 `PARALLEL-GROUP-1` | **Task zero: measure V13.** Build a calibration harness in the test project that runs seeds 1-20 at 200 agents under `CohortLateralSpreadV13` and reports, per seed and pooled: rooted share (per-tick displacement below 60 raw divided by living agent-ticks), total travel per living agent, mean net spawn-to-terminal displacement, mean contact-retention agent-ticks, terminal tick, and outcome. Headless-shaped, reads agent views. Paste the table into design section 8 as the V13 column. | `tests/Hukbo.Core.Tests/Movement/EvasionCalibrationHarness.cs` (new) | The harness prints a twenty-seed table and a pooled row; every design-section-8 bar has a concrete V13 number behind it; no production file is touched. | — | `./scripts/test.ps1 -Configuration Release -Game Hukbo` |
| 2 `PARALLEL-GROUP-1` | **Gait direction defect.** `GaitAnimationSystem.Advance` must derive the stride sign from `deltaY` when `deltaX` is zero, before falling back to the retained sign. A pure-vertical step must animate legs instead of zeroing them through `PawnGeometry`'s `DirectionSign` multiply. | `src/Hukbo.Client/Presentation/GaitAnimationSystem.cs`, `tests/Hukbo.Client.Tests/GaitAnimationSystemTests.cs` | A new entry moving `(0, +1600)` resolves a non-zero `DirectionSign` and `GaitMode.Run`; every existing horizontal-motion case is unchanged; `GaitGeometryTests`, `PawnGeometryTests`, `PawnGaitQuadParityTests`, `GaitPixelHeightTests` all still pass untouched. | — | `./scripts/test.ps1 -Configuration Release -Game Hukbo` |
| 3 `SERIAL` | **Register V14.** `EvasiveFootworkV14 = 14` appended to the enum with full XML documentation naming the three identity gates it joins; ruleset restating V13's field values verbatim under its own id; both registry switches; selector option and display name. | `src/Hukbo.Core/Movement/MovementPresetId.cs`, `src/Hukbo.Core/Movement/MovementPresetRegistry.cs`, `src/Hukbo.Client/UI/ArmyCompositionPanel.cs`, `tests/Hukbo.Core.Tests/MovementPresetRegistryTests.cs`, `tests/Hukbo.Client.Tests/ArmyCompositionPanelTests.cs` | `IsRegistered(EvasiveFootworkV14)` is true; V14's pinned `ContentHash` literal is asserted and differs from all thirteen others; the selector offers it; `EveryRegisteredMovementPresetHasAMatchingDisplayName` and the wrap test are green; `BattleSimulationTests:1731-1736` is green untouched. | 1 | `./scripts/test.ps1 -Configuration Release -Game Hukbo` |
| 4 `SERIAL` | **Admit V14 to all three closed gates.** `UsesBattlefieldRealism` (`:5214-5218`), `YieldsLastStandEngagement` (`:1532-1535`), and `spreadCohortsLaterally` (`:708-709`). No other change. | `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `tests/Hukbo.Core.Tests/Movement/EvasiveFootworkV14Tests.cs` (new) | A differential test proves V14 and V13 on seed 1 at 200 agents produce identical final agent positions, identical event fold, and identical terminal tick, while producing **different** state hashes; a second test asserts V14 takes `FormationPlanner`'s square-root sizing path, not V12's authored-sizes branch. | 3 | `./scripts/test.ps1 -Configuration Release -Game Hukbo` |
| 5 `SERIAL` | **`EvasiveAction`: enum, state, view, hash gate.** New enum with the six frozen values; `AgentState` property appended after `BrokeOffUnderPressure`; `AgentView` parameter appended and defaulted; `ToView` and `UpdateViews` projection; death cleanup clears to `None`; `StateHasher.Compute` gains a `foldsEvasiveAction` parameter appended last, folding inside the per-agent loop after the pressure block; `ComputeStateHash` passes preset identity 14. | `src/Hukbo.Core/Movement/EvasiveAction.cs` (new), `src/Hukbo.Core/Simulation/AgentState.cs`, `src/Hukbo.Core/Simulation/AgentView.cs`, `src/Hukbo.Core/Determinism/StateHasher.cs`, `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `tests/Hukbo.Core.Tests/Movement/MovementStateHashTests.cs`, `tests/Hukbo.Core.Tests/AgentViewTests.cs` | New pinned fold literals for both gate states are recorded from a real run and asserted; the V6 literals `0xC7B6C46A3D086571` / `0x2F465B4A80E658B2` are unchanged; all nine freeze fixtures and all five gate baselines still reproduce; a V13 run's state hash is bit-identical to `4A0723BC9A1B924B`. | 4 | `./scripts/test.ps1 -Configuration Release -Game Hukbo`, then `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1 -Preset PrecolonialPhilippinesV5 -MovementPreset CohortLateralSpreadV13` |
| 6 `PARALLEL-GROUP-2` | **`EvasionRules`: the pure arithmetic.** `FiresThisTick`, `DutySign`, `PerpendicularOffset`, and every named constant of design section 5, each carrying the provisional-reconstruction label. No simulation reference of any kind. | `src/Hukbo.Core/Movement/EvasionRules.cs` (new), `tests/Hukbo.Core.Tests/Movement/EvasionRulesTests.cs` (new) | Duty phase spreads across the period and fires exactly once per period per entity; `DutySign` alternates on consecutive duty windows; `PerpendicularOffset` is exactly perpendicular within truncation, has the requested magnitude within one raw unit, returns `(0,0)` at zero distance, and cannot overflow at `int.MaxValue` inputs; every step constant is asserted at least 384. | 5 | `./scripts/test.ps1 -Configuration Release -Game Hukbo` |
| 7 `PARALLEL-GROUP-2` | **Derived evasion metrics.** New `EvasiveMovementMetrics` record struct and accumulator mirroring `MovementBehaviorMetrics`; `HeadlessRunner` reconstructs rooted share, travel, net displacement, contact retention, and per-`EvasiveAction` agent-ticks from consecutive `AgentView` diffs; `RunReport` reports them additively. Never hashed, never snapshotted. | `src/Hukbo.Core/Simulation/EvasiveMovementMetrics.cs` (new), `src/Hukbo.Headless/HeadlessRunner.cs`, `src/Hukbo.Headless/RunReport.cs`, `tests/Hukbo.Core.Tests/HeadlessRunnerTests.cs` | Two same-seed runs report identical values in every field; a V13 run reports zero in every `EvasiveAction` counter and non-zero travel; the report survives a JSON round trip and a report missing the property still deserializes; the five gate baselines are unmoved. | 5 | `./scripts/test.ps1 -Configuration Release -Game Hukbo` |
| 8 `PARALLEL-GROUP-2` | **Inspector row.** `FormatEvasiveActionLine` returning `null` at `None`, `GetEvasiveActionLabel`, and the row appended after the pace row in `BuildLowerLines`. | `src/Hukbo.Client/UI/AgentInspectorContent.cs`, `tests/Hukbo.Client.Tests/AgentInspectorContentTests.cs` | Every non-`None` value renders a distinct label; `None` renders nothing; the existing exact-equality budget assertion at `:2122-2123` is still green with `MaximumLowerRowCount = 46` unchanged; a new assertion proves a V14-shaped view wraps to at most 46. | 5 | `./scripts/test.ps1 -Configuration Release -Game Hukbo` |
| 9 `SERIAL` | **Wire the stage and M2 (slip laterally while closing).** `ApplyEvasiveFootwork` called at the tail of `GatherMovementProposals` behind a single V14 identity return; zero-length scratch under every other preset; the priority ladder skeleton with only the slip rung live; scratch committed once after the loop. **Also supersede task 4's differential test** — see the note below this table. | `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `tests/Hukbo.Core.Tests/Movement/EvasiveSlipTests.cs` (new), `tests/Hukbo.Core.Tests/Movement/EvasiveFootworkV14Tests.cs` | A hand-built two-agent case at 20,000 raw separation proposes the exact pinned endpoint on its duty tick and the unchanged straight-line endpoint on every other tick; the rung never fires inside contact, never fires for `Regrouping`/`Holding`/`BackingAway`, and yields on a bounds clamp; V13 is byte-identical. | 6, 7, 8 | `./scripts/test.ps1 -Configuration Release -Game Hukbo` |
| 10 `SERIAL` | **M4 (give ground while pinned).** The give-ground rung, ordered above slip in the ladder. | `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `tests/Hukbo.Core.Tests/Movement/EvasiveGiveGroundTests.cs` (new) | Fires only when in contact, tick-start `MovementResolution` is `Blocked`, and the duty phase matches; moves exactly 1024 raw directly away; `Intent` and `TargetEntityId` are provably unchanged; the warrior stays inside its own `AttackRangeRaw` after three consecutive fires; `AgentIntent.BackingAway` is never written. | 9 | `./scripts/test.ps1 -Configuration Release -Game Hukbo` |
| 11 `SERIAL` | **M1 (break off after an intercepted exchange).** The arm write in `GatherAndCommitAttacks` under the "only when `None`" condition, and the break-off rung ordered above give-ground. | `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `tests/Hukbo.Core.Tests/Movement/EvasiveBreakOffTests.cs` (new) | Each of `ShieldBlocked`, `Parried`, `Deflected`, `Evaded` arms the defender and `Landed` does not; a defender that already executed an evasive movement this tick is not armed; three attackers hitting one defender in one tick produce the same value regardless of order; the armed warrior circles at contact distance on the next tick and cannot break off two ticks running. | 10 | `./scripts/test.ps1 -Configuration Release -Game Hukbo` |
| 12 `SERIAL` | **M3 (dodge an inbound missile).** The projectile scan and dodge rung at the top of the ladder. Movement only — no clash change. | `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `tests/Hukbo.Core.Tests/Movement/EvasiveDodgeTests.cs` (new) | Fires at `TicksRemaining <= 2` and not at 3; ties break on `(LaunchTick, SourceEntityId)`; the step is perpendicular to the origin-to-agent line; a melee-only roster observes an empty pool and behaves identically to task 11's build; a test documents that a dodging warrior can still be recorded as hit, with the launch-tick reason. | 11 | `./scripts/test.ps1 -Configuration Release -Game Hukbo` |
| 13 `SERIAL` | **Calibrate against the anti-goals.** Run the task-1 harness against V14, tune only `EvasionRules` constants, and record the twenty-seed table beside the V13 column. | `src/Hukbo.Core/Movement/EvasionRules.cs`, `tests/Hukbo.Core.Tests/Movement/EvasionCalibrationHarness.cs` | All eight bars of design section 8 pass and are recorded with numbers: defence share in `[0.25, 0.45]` for 20 of 20; 19 of 20 seeds decisive with median within +25 per cent; net drift within +15 per cent; contact retention at least 90 per cent; rooted share strictly lower; travel within +30 per cent; give-ground at most 10 per cent and evasion share above zero and at most 40 per cent; five baselines and nine fixtures unmoved. | 12 | `./scripts/test.ps1 -Configuration Release -Game Hukbo` |
| 14 `PARALLEL-GROUP-3` | **Give-ground lean.** Suppress the run torso lean when the warrior's `EvasiveAction` is `GiveGround`, so a yielded foot stops reading as a rout. | `src/Hukbo.Client/Rendering/GaitPoseResolver.cs`, `src/Hukbo.Client/Rendering/GaitGeometry.cs`, `tests/Hukbo.Client.Tests/GaitPoseResolverTests.cs`, `tests/Hukbo.Client.Tests/GaitGeometryTests.cs` | A `GiveGround` view resolves `TorsoLeanX == 0` at run mode; every other action and every default-constructed call is bit-identical to before; `PawnGaitQuadParityTests` and `GaitPixelHeightTests` pass untouched. | 5, 13 | `./scripts/test.ps1 -Configuration Release -Game Hukbo` |
| 15 `SERIAL` | **Sixth gate workload.** Append a V14 block to `scripts/verify.ps1` after the V13 block. **Do not repoint the V13 block** — it stays as the leak detector. | `scripts/verify.ps1`, `tests/Hukbo.Client.Tests/ScriptDefaultsTests.cs` | The benchmark-invocation count assertion moves 6 to 7 with the test method renamed; the V14 block is asserted by position after the V13 one and inside the `-eq 'Hukbo'` guard; a bare `./scripts/verify.ps1` still runs both games. | 13, 14 | `./scripts/verify.ps1` |
| 16 `SERIAL` | **Record the gate.** Add the V14 row to the workload table in `docs/development/testing.md` with its recorded `stateHash`/`eventHash`, re-confirm all five earlier rows byte-identical in the same run, and add the evasion measurement table to `docs/development/measurement-history.md`. | `docs/development/testing.md`, `docs/development/measurement-history.md` | The table has six rows; the five earlier pairs are quoted unchanged from the same run's output; the 500-agent result required by standards section 10 question 7 is recorded. | 15 | `./scripts/verify.ps1` |
| 17 `SERIAL`, **last** | **Flip the client default to V14.** `DefaultMovementPreset` only. **No settings schema bump** — `SupportedSchemaVersion` stays 12, exactly as the V10-to-V11 and V11-to-V13 flips took none. | `src/Hukbo.Client/Settings/ClientSettingsStore.cs`, `tests/Hukbo.Client.Tests/ClientSettingsStoreTests.cs` | A fresh install resolves `EvasiveFootworkV14`; a settings file that already records any earlier preset reads back verbatim through `ResolveMovementPreset`; `SupportedSchemaVersion` is asserted still 12; the full gate is green after the flip. | 16 | `./scripts/verify.ps1` |

### Amendment: task 4's differential test is scaffolding with a known expiry

Task 4 asserts that V14 and V13 produce identical positions, an identical event
fold, and an identical terminal tick. That is true only while V14 has no rungs.
From task 9 onward V14 diverges from V13 **by design**, and the assertion will
fail for the right reason.

Task 9 must therefore supersede it rather than weaken it. Specifically, task 9
edits `EvasiveFootworkV14Tests.cs` to:

- **Keep** the assertion that V14 and V13 produce different state hashes, and
  keep the `FormationPlanner` sizing-path assertion. Both remain true forever.
- **Replace** the position, event-fold, and terminal-tick equality assertions
  with a single documented test named for what it now proves — that V14 diverges
  from V13 only through `ApplyEvasiveFootwork`, demonstrated by a run in which
  no agent ever satisfies a rung's guard (a scenario with one faction only, so
  no agent ever holds a living enemy target) still matching V13 exactly.

Deleting the equality assertions without that replacement loses the only proof
that the three identity gates of design section 3 are complete, which is the
defect commit `3163fbf` exists to prevent. An implementer who finds this test red
at task 9 and simply deletes it has removed the safety net, not fixed the test.

## Known red by construction, and the task that fixes each

| Goes red when | What fails | Fixed by |
| --- | --- | --- |
| V14 is registered | `ArmyCompositionPanelTests.EveryRegisteredMovementPresetHasAMatchingDisplayName` — it asserts `registered == MovementPresetOptions` as an ordered sequence | Task 3, same task, same commit |
| V14 is registered | `ArmyCompositionPanelTests` selector-wrap test, which walks to the last option | Task 3, same task |
| The enum value exists without a registry entry | `BattleSimulationTests:1731-1736`, which asserts `IsRegistered` for every `Enum.GetValues<MovementPresetId>()` value | Never observed red: task 3 adds the enum value and both registry switch arms in one task |
| The sixth gate block is added | `ScriptDefaultsTests` — `Assert.Equal(6, benchmarkInvocations.Count)` | Task 15, same task |
| The client default flips | `ClientSettingsStoreTests` default assertions naming `CohortLateralSpreadV13` | Task 17, same task |
| The first rung lands | Task 4's position and event-fold equality assertions | Task 9, by supersession — see the amendment above |
| The inspector row is added | Nothing, **if** the formatter returns `null` at `None`. The budget test asserts *exact* equality with 46 against a deepest view that leaves `EvasiveAction` at its default | Task 8 adds a V14-shaped assertion rather than moving the constant |
| The hash fold is added | Nothing, **if** the fold sits inside the per-agent loop after the pressure block and behind its own gate. `MovementStateHashTests:192-193` V6 literals and the V7 literals must not move | Task 5. If either literal moves, the fold is in the wrong place — revert, do not rebaseline |

Standing negatives that must **never** go red at any point in the sequence: the
nine `MovementPresetFreezeTests` fixtures; `CohortLateralSpreadV13Tests.cs:612-613`;
`ContingentShapeV12Tests.cs:259-260` and `:279-280`; `DeterminismTests.cs:58`,
`:243-244`, `:311-312`; and the five recorded gate baselines. Any movement in
these is a defect in the task that caused it.

## Rollback

The feature is reachable through exactly two switches: preset identity 14, and
the one folded field gated on that identity. Nothing else in the build can see
it.

- **Partial rollback, keeping the work:** revert task 17 alone. The client
  returns to `CohortLateralSpreadV13`, V14 stays registered and selectable, and
  no hash, fixture, or schema version changes in either direction. This is the
  cheap escape and it should be the first response to any field problem.
- **Remove from the gate:** revert task 15 as well, restoring the count
  assertion. The gate returns to five Hukbo workloads.
- **Full revert:** reverse task order, 17 down to 3. Because every V1-V13
  baseline, fixture, and pinned literal is untouched throughout, a full revert
  restores the pre-change tree exactly; there is no fixture to regenerate and no
  baseline to rewrite.
- **Do not** revert task 2 (the gait direction fix) with the rest. It is an
  independent renderer defect fix, benefits every preset, and depends on nothing
  in this feature.

## What was actually run, and what is still owed

Branch `hukbo-fight-evasion`, based at `main` `cfe0c22`. Every task from 1 to 17
is implemented and committed.

`./scripts/verify.ps1 -Game Hukbo` ends `[PASS] Canonical repository
verification completed.` with exit code 0, at 2,636 Core and 4,011 Client tests,
all passing. Its six workload hashes are recorded in
`docs/development/testing.md`, and the five that predate this work are
byte-identical in that same run.

Three things are still owed, and none of them may be closed by an agent.

1. **Every smoke-checklist row for this package is `PENDING`.** The fourteen
   rows drafted during research were never added to
   `docs/development/smoke-checklist.md`, because that file is edited
   concurrently by other sessions and a blind write would clobber their work.
   The rows still need adding, and then only a person at an interactive desktop
   may flip one. Nothing in this package has been seen running.
2. **The tuning has been calibrated but not judged.** All eight numeric bars of
   design section 8 pass, and the rooted share falls from 0.6221 to 0.5839. That
   is a real improvement of about six per cent relative, and it is smaller than
   the phrase "warriors now move during a fight" might suggest. Whether it looks
   different enough is a judgement only watching it can settle. Design open
   question 3 names the periods as the first knobs, and
   `EvasionCalibrationHarness` is the instrument for turning them.
3. **The deferred melee telegraph.** A reactive dodge against a melee blow is
   not built and cannot be, because an attack resolves inside a single tick with
   no interval during which a blow is incoming. Building one means authoritative
   pending-blow state mirroring the projectile pattern, a new combat preset, and
   new golden expectations. Design section 2 records it as deferred.

Two defects were found by the rung tests after the first green gate, and both
are worth remembering because neither was visible by reading the code: a corpse
carried its last evasive action when it died in the same tick it stepped, and a
warrior backing away could be armed with a break-off step it could never spend.
Neither moved the recorded digest, because both values were transient and the
terminal hash is taken once at the end of a run.

## Housekeeping carried by this package

- `src/Hukbo.Core/Movement/RangedRetreatRules.cs:12-13` claims the class is "Not
  yet wired into `BattleSimulation`". It is wired, at
  `BattleSimulation.cs:2036-2037`, and reachable under every battlefield-realism
  preset including the shipped default. Correct the comment; change no code.
