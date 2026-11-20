# Optimizations from the We March as One comparison — plan

**Authorized on 2026-11-21** for phases 0 to 5, after these documents were
written. Phase 5 covers only the opt-in Hukbo preset that the decision record approves.

**Baseline: `main` at `0ccc0d3`, 2026-11-21.** Branch `wmao-optimizations`.

Design documents, one per phase:

| Phase | Design document | Touches |
| --- | --- | --- |
| 1 | `docs/plans/2026-11-21-ring-search-targeting-design.md` | `Hukbo.Core`, hash-neutral |
| 2 | `docs/plans/2026-11-21-play-rate-governor-design.md` | `Hukbo.Client` only |
| 3 | `docs/plans/2026-11-21-weapon-damage-tally-design.md` | `Hukbo.Client` only |
| 5 | `docs/plans/2026-11-21-soft-separation-design.md` | `Hukbo.Core`, opt-in preset V17 |

The comparison that produced these documents studied the decompiled source of
We March as One, a Unity DOTS battle game. Its ideas transfer, and its code does
not. No line of it enters this repository, and the documents cite its files by
name only.

---

## Rules for every task

- Phase 1 is hash-neutral **by construction**. If any digest moves, the task
  is wrong. Never recapture a golden value to get green.
- Phases 2 and 3 never touch `Hukbo.Core`, `Hukbo.Shared.Core`, or `Sandata.*`.
- Implementers do not commit. The integrator stages by pathspec and commits.
- Every implementer runs the named test command and pastes its real output.
- `./scripts/verify.ps1` runs once, after integration, run by the integrator.
- No agent flips a smoke-checklist row.

---

## Phase 0 — measure first

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| W-1 | Record a baseline tick profile under the **shipped** presets (combat V8, movement V14) at 200, 500, 1,000, and 2,000 agents, seed 1, 10,000 ticks, with `./scripts/benchmark.ps1`. Record p50, p95, p99, max, and allocation | `docs/development/measurement-history.md` | Four rows recorded with the exact commands and commit | — | The rows exist and quote the commands |
| W-2 | Attribute tick cost at 1,000 agents with external `dotnet-trace`. Record the inclusive share of `SelectTargetsAndIntents`, `FindNearestMeleeThreatPosition`, `GatherMovementProposals`, `ResolveCollisions`, and `GatherAndCommitAttacks`. No instrumentation is added to `Hukbo.Core` | `docs/development/measurement-history.md` | The share table is recorded with the trace command | — | No diff under `src/` |

---

## Phase 1 — ring-search targeting (`Hukbo.Core`)

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| RS-1 | Write `TargetingGrid` (design §4) and its differential unit tests against a test-only `NaiveNearestEnemy`. Cover the design §6 item 1 scenarios, including the far-ring equal-distance lower-`EntityId` case and negative coordinates | `src/Hukbo.Core/Simulation/TargetingGrid.cs` (new), `tests/Hukbo.Core.Tests/TargetingGridTests.cs` (new), `tests/Hukbo.Core.Tests/NaiveNearestEnemy.cs` (new) | Every differential case passes | — | `dotnet test tests/Hukbo.Core.Tests -c Release --filter FullyQualifiedName~TargetingGrid` |
| RS-2 | Wire the grid into `SelectTargetsAndIntents` (design §3, §4.4). Extract the per-candidate body into one shared method. Keep the brute-force scan when `derivesLocalContext` is true. Add an internal test seam that forces brute force. Add the whole-simulation differential test for every registered preset that does not derive local context, and a warm-tick allocation window at 1,000 agents | `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `tests/Hukbo.Core.Tests/RingSearchTargetingTests.cs` (new) | Differential and allocation tests pass. The full Core suite passes | RS-1 | `./scripts/test.ps1 -Configuration Release` |
| RS-3 | Sweep the cell size: 16, 32, 64, and 128 wu, at 500, 1,000, and 2,000 agents under the shipped presets. Run each candidate in a scratch build that is never committed. Commit only the winning constant | `src/Hukbo.Core/Simulation/TargetingGrid.cs`, `docs/development/measurement-history.md` | The sweep table is recorded and the constant set | RS-2 | Benchmarks quoted. Differential tests still pass |
| RS-4 | Re-run W-1 and W-2 on the result. Extend the dated corrections already added on 2026-11-21 to section 4.1 of `docs/plans/2026-08-14-thousand-unit-performance-design.md` and to its plan with the measured figures. Add the targeting grid to the derived-oracle passage of `SIMULATION-GAME-STANDARDS.md` | `docs/development/measurement-history.md`, `docs/development/testing.md`, the two 2026-08-14 documents, `SIMULATION-GAME-STANDARDS.md` | Before and after figures are recorded side by side | RS-3 | The figures meet design §6 item 5 |
| RS-5 | **Conditional.** Answer `FindNearestMeleeThreatPosition` from the same grids (design §5), keeping its no-perception semantics. Start only if RS-4's trace still shows it above 5% of tick time | `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `tests/Hukbo.Core.Tests/RingSearchTargetingTests.cs` | A differential test passes for every threatened ranged agent over 2,000 ticks | RS-4 | As RS-2 |

---

## Phase 2 — play-rate governor and interpolation (`Hukbo.Client`)

PG-1 and PG-3 are pure and can run in parallel with each other and with phase 1.
PG-2 and PG-4 both edit `ArenaGame.cs` and `ArenaGame.Rendering.cs`, so they run
one after the other.

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| PG-1 | Write the pure `PlayRateGovernor` (design §3.2, §3.7) and its tests (design §5) | `src/Hukbo.Client/Presentation/PlayRateGovernor.cs` (new), `tests/Hukbo.Client.Tests/PlayRateGovernorTests.cs` (new) | Tests pass | — | `dotnet test tests/Hukbo.Client.Tests -c Release --filter FullyQualifiedName~PlayRateGovernor` |
| PG-2 | Wire the governor into `AdvanceSimulation`. Time the simulation work with `Stopwatch.GetTimestamp`, and feed the effective speed to the accumulator, `AdvanceEffects`, and the status text with its `holding` suffix. Add the `render.playRateGoverned` event | `src/Hukbo.Client/ArenaGame.cs`, `src/Hukbo.Client/ArenaGame.Rendering.cs`, `src/Hukbo.Diagnostics/LogEvents.cs` | The client suite passes, including `LogEventCatalogTests` | PG-1 | `./scripts/test.ps1 -Configuration Release` |
| PG-3 | Write the pure `RenderPositionInterpolator` (design §4.2, §4.3) and its tests | `src/Hukbo.Client/Presentation/RenderPositionInterpolator.cs` (new), `tests/Hukbo.Client.Tests/RenderPositionInterpolatorTests.cs` (new) | Tests pass | — | `dotnet test tests/Hukbo.Client.Tests -c Release --filter FullyQualifiedName~RenderPositionInterpolator` |
| PG-4 | Wire interpolation into pawn drawing, the probe path, attached marks, and picking. Gait, effects, and projectiles stay on tick positions. Capture previous positions in the `AdvanceSimulation` tick loop, before each `AdvanceOneTick`. Append `ClientCommand.ToggleInterpolation` on `I` | `src/Hukbo.Client/ArenaGame.cs`, `src/Hukbo.Client/ArenaGame.Rendering.cs`, `src/Hukbo.Client/Presentation/AgentSelection.cs`, `src/Hukbo.Client/Presentation/ClientCommand.cs` | The client suite passes | PG-2, PG-3 | `./scripts/test.ps1 -Configuration Release` |

---

## Phase 3 — weapon damage tally (`Hukbo.Client`)

DT-1 to DT-4 are independent of phase 2. DT-5 edits the same `ArenaGame` files
and `ClientCommand.cs` as phase 2, so it runs after PG-4.

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| DT-1 | The armour-equality test (design §3.1). Over a real 500-agent battle under the shipped presets, check whether per-target sums of `ApplyArmorMitigation` over landed blows equal each tick's `Damage` events. Record the answer in the design document | `tests/Hukbo.Client.Tests/WeaponDamageArmorEqualityTests.cs` (new), the tally design document | The test passes in whichever form the evidence supports, and the design records the result | — | `dotnet test tests/Hukbo.Client.Tests -c Release --filter FullyQualifiedName~WeaponDamageArmorEquality` |
| DT-2 | Write the pure `WeaponDamageTally`, fed from `PresentationCoordinator.IngestTick`, using DT-1's damage rule and its tests | `src/Hukbo.Client/Presentation/WeaponDamageTally.cs` (new), `src/Hukbo.Client/Presentation/PresentationCoordinator.cs`, `tests/Hukbo.Client.Tests/WeaponDamageTallyTests.cs` (new) | Tests pass | DT-1 | Filtered `dotnet test` on `WeaponDamageTally` |
| DT-3 | Write `DamageTallyLayout` and `DamageTallyPanel`, with layout tests at three window sizes | `src/Hukbo.Client/UI/DamageTallyLayout.cs` (new), `src/Hukbo.Client/UI/DamageTallyPanel.cs` (new), `tests/Hukbo.Client.Tests/DamageTallyLayoutTests.cs` (new) | Tests pass | DT-2 | Filtered `dotnet test` on `DamageTally` |
| DT-4 | Add the spotlight ring to `PawnRenderer` and its quad count to `SubmissionCount`, and update the count tests | `src/Hukbo.Client/Rendering/PawnRenderer.cs`, `src/Hukbo.Client/Rendering/SubmissionCount.cs`, the matching tests | The client suite passes | — | `./scripts/test.ps1 -Configuration Release` |
| DT-5 | Wire it up: append `ClientCommand.ToggleDamageTally` on `T`, add the panel to the pointer chain and `DrawUiLayer`, clear the tally on reset, and draw the spotlight on hover | `src/Hukbo.Client/ArenaGame.cs`, `src/Hukbo.Client/ArenaGame.Rendering.cs`, `src/Hukbo.Client/Presentation/ClientCommand.cs` | The client suite passes | DT-3, DT-4, PG-4 | `./scripts/test.ps1 -Configuration Release` |

---

## Phase 4 — integrate, gate, and record

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| IN-1 | Rebase onto the current local `main`. Run `./scripts/verify.ps1` once and paste its output. Both games must be green, and all seven Hukbo workload hashes must be unchanged | — | The gate is green and the hashes match `docs/development/testing.md` | All of phases 1 to 3, and SS-3 | The real gate output |
| IN-2 | Add the smoke rows named in the three design documents as `PENDING`, and update the controls paragraph with `I` and `T`. Recount the status column at write time, because another session edits this file | `docs/development/smoke-checklist.md` | The rows exist and the counts are right | IN-1 | Recount by `Select-String` |
| IN-3 | Merge into `main` through a pull request, then remove the worktree and the merged branch | — | Merged | IN-2 | `git log main` |

---

## Phase 5 — separating contact, opt-in Hukbo preset V17

Approved on 2026-11-21. Decision record:
`docs/decisions/2026-11-21-separating-contact.md`. Design:
`docs/plans/2026-11-21-soft-separation-design.md`, sections 3 and 5. Sandata
is out of scope.

V1 to V16 must keep every recorded hash and every pinned `ContentHash`. V17
gets new pins of its own. A moved existing hash means a task is wrong.

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| SS-0 | Write the decision record and update the design | `docs/decisions/2026-11-21-separating-contact.md`, the design | Committed | Approval | Done 2026-11-21 |
| SS-1 | Append `SeparatingContactV17 = 17`. Add the ruleset gate `AppliesSeparatingContact` and its basis-point fields: soft-band fraction, push cap as a share of the radius, stiffness, committed-weight multiplier, and sideways share. Fold them only inside the gate. Register V17 as a copy of V14's row plus the gate. Pin V17's new `ContentHash` and extend the registration tests | `src/Hukbo.Core/Movement/MovementPresetId.cs`, `src/Hukbo.Core/Movement/MovementRuleset.cs`, `src/Hukbo.Core/Movement/MovementPresetRegistry.cs`, the registration and `ContentHash` tests | Every V1–V16 pin is unchanged and V17 has its own | — | `./scripts/test.ps1 -Configuration Release` |
| SS-2 | Write a pure `SeparationPass` (design §3.1): a Jacobi pass over a position snapshot, with a weight share, an `Fnv1a` direction for coincident pairs, a sideways component in the soft band, the push cap, and the arena clamp. Integer math only. Test order independence (shuffled storage gives the same result), the weight share, the cap, the coincident direction, the sideways side, and zero warm allocation | `src/Hukbo.Core/Simulation/SeparationPass.cs` (new), `tests/Hukbo.Core.Tests/SeparationPassTests.cs` (new) | Tests pass | — | Filtered `dotnet test` on `SeparationPass` |
| SS-3 | Wire V17 into the tick. Pass the floor `2R × (1 − f)` to `CollisionResolver` under the gate, with `2R` everywhere else. Run the pass after `CommitMovement` under the gate. Make the zero-overlap tests preset-aware, and add the invariant test `d ≥ 2R(1 − f) − 2c` over seeded V17 battles. Add V17 to the army-composition preset list. Amend `SIMULATION-GAME-STANDARDS.md` §13 and the `CollisionMetrics` documentation | `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `src/Hukbo.Core/Simulation/CollisionResolver.cs`, `src/Hukbo.Client/UI/ArmyCompositionPanel.cs`, `src/Hukbo.Core/Simulation/CollisionMetrics.cs`, collision and panel tests, `tests/Hukbo.Core.Tests/SeparatingContactTests.cs` (new), `SIMULATION-GAME-STANDARDS.md` | Both suites pass, all existing gate hashes unchanged, and the V17 invariant holds | SS-1, SS-2, RS-2 (shared `BattleSimulation.cs`) | `./scripts/test.ps1 -Configuration Release` and a gate-hash check |
| SS-4 | Measure V17 against V14 under combat V8 (design §5): 20 seeds at 200 agents for decision rate, blocked agent-ticks at 500 agents, the 2,000-agent battle, and tick cost at 500 and 1,000. Record the results. Add smoke rows as `PENDING`. Do not change the default | `docs/development/measurement-history.md`, `docs/development/smoke-checklist.md` | The table is recorded with commands, and each design §5 condition is marked pass or fail | SS-3 | The recorded figures |

---

## Deferred, by decision

- **Staggered rescans and a sleep timer for targeting.** They change behaviour,
  so they need a preset. They move forward only if phase 1 leaves 1,000 agents
  over budget.
- **Sandata's millisecond truncation** at `SandataGame.cs:720`. This is a
  one-line fix recorded in the governor design §6. It stays out of this package.
- **Thousand-unit plan TU-5 to TU-10.** They target footwork scans that no
  shipped preset runs.
