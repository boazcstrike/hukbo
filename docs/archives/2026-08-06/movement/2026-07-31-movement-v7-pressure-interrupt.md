# Movement V7 pressure interrupt — task plan

> **Archived: reference only.** The movement V7 pressure-interrupt workstream
> finished and merged to main on 2026-08-06. V7 shipped as a registered, pinned,
> fully tested preset that is reachable only by explicit selection, and it does
> not meet the design section 2.1 termination bar at any tuning. Decision D6
> stands: `Scenario.MovementPreset` remains `PersistentContingentsV4`. Do not
> execute this plan; its task list, line numbers, and verification steps are
> historical. The dated annotations inside record where measurement overturned
> what the document originally claimed.

Date: 2026-07-31
Status: **plan only. No code written, no test run, the canonical gate not
invoked by this document.**

The design this plan executes is
[`2026-07-31-movement-v7-pressure-interrupt-design.md`](2026-07-31-movement-v7-pressure-interrupt-design.md).
The settled brief behind that design is
[`2026-07-31-movement-v7-calibration-decisions.md`](2026-07-31-movement-v7-calibration-decisions.md).
Neither is reopened here.

## How to read this plan

Twenty-one tasks in six phases. Each task is small enough for one agent, names
the files it owns, names how it is verified, and names what it depends on.

**File ownership is exclusive.** Two tasks marked parallel never name the same
file. Where a file appears in more than one task, those tasks are serial by
construction and the dependency column says so.

**Three files are shared seams** and are the reason most of phase 2 is serial:

| Seam | Tasks that touch it |
| --- | --- |
| `src/Hukbo.Core/Movement/MovementRuleset.cs` | A2, B1, B2 |
| `src/Hukbo.Core/Movement/MovementPresetRegistry.cs` | A2, B1, B3 |
| `src/Hukbo.Core/Simulation/BattleSimulation.cs` | B4, B6, D1 |

**Ordering rule from the brief, binding.** Scalar tuning comes *after* the
interrupt lands, never before, because tuning a threshold no warrior can reach
measures nothing. Phase 5 therefore follows phase 3 completely, and the V7
trajectory digest and content-hash pins are captured only after phase 5, so they
are captured once rather than twice.

**Every tuning value produced by this plan is a provisional reconstruction of
gameplay tuning under `CLAUDE.md` section 7. None is a historical measurement
and none may be presented as one.** Any task that writes a number writes that
label beside it.

**No agent flips a manual smoke-checklist row.** Task F1 creates rows; every
one lands `PENDING` and stays there until a human at an interactive desktop runs
it.

**The canonical gate is not delegated.** `./scripts/verify.ps1` runs once, after
integration, and its real output is the evidence. A task's "Verified by" column
names the focused check that task is responsible for, not a substitute for the
gate.

---

## Phase 0 — baseline and truth-in-comments

Nothing in phase 0 changes behaviour. A0 and A1 are independent of everything
and of each other. A2 must land before phase 1 so no later task is written
against a comment that lies.

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| **A0** ∥ | Record the pre-V7 baseline. Run the design section 2.2 protocol — seeds 1, 2, 3, 5, 8 at 200 and at 500 agents, combat preset `PrecolonialPhilippinesV2` pinned, one discarded warm run per cell, report the median — for both `PersistentContingentsV4` and `EquipmentRelativeFootworkV6`. Record terminal tick, outcome, survivor counts, and `p50Milliseconds` per cell, plus the machine identification `SIMULATION-GAME-STANDARDS.md` section 8 requires. No recorded result exists today for the shipped default pair, so there is no "before" until this task produces one | `docs/archives/2026-08-06/movement/2026-07-31-movement-v7-baseline.md` (new) | The document contains twenty measured cells with real command output pasted, and states plainly which V6 cells ended `Draw` at the tick limit | — | The pasted output of the runs themselves. No claim in the document may exceed what the output shows |
| **A1** ∥ | Annotate the tall-hardwood shield research with the decision D4 ratification note: the shipped `KS` entry of 17,500 basis points and `IS` entry of 15,000 sit at or below the bottom of the 0.67-to-0.80 ally-to-enemy band the document proposes, and both are now ratified as deliberate "protected deliberation" tuning. **Annotate; never overwrite the proposed band.** The band is a research finding and the note records that gameplay tuning departed from it, not that the research was wrong | `docs/research/movement/tall-hardwood-shield.md` | The proposed band is still present verbatim and a dated note sits beside it | — | Read-back confirming the original band text is unchanged and the note is additive |
| **A2** | Correct three stale assertions that claim the opposite of what V6 actually does: `MovementRuleset.cs:17-32` and `MovementPresetRegistry.cs:18-24` both claim `ContentHash` never reaches the state hash, and `MovementPresetRegistry.cs:216-217` claims no `BattleSimulation` path consults `UsesEquipmentRelativeFootwork`. Replace each with what is true, citing `BattleSimulation.cs:654-656` and `StateHasher.cs:81-84` for the first two, and for the third the twelve code sites in `BattleSimulation.cs` that do consult the flag (`:142`, `:146`, `:297`, `:420`, `:584`, `:593`, `:606`, `:654`, `:922`, `:1461`, `:3183`, `:3337`). **Comments only — no code, no signature, no value changes** | `src/Hukbo.Core/Movement/MovementRuleset.cs`, `src/Hukbo.Core/Movement/MovementPresetRegistry.cs` | All three passages state the correct behaviour and no non-comment line differs | — | `./scripts/test.ps1 -Configuration Release` green, and `git diff` showing only comment lines |

---

## Phase 1 — the version gate

One task, and it is the foundation everything else hangs from. It must land and
prove V1 through V6 unmoved before any field is added anywhere.

**Read this before starting B1.** Task A2's report claimed that any new
`MovementRuleset` field folded into `ComputeContentHash` requires re-recording
the V6 trajectory digest. **That claim is false for this task, and acting on it
would destroy the freeze guarantee.** It is true only of *unconditional*
folding, which is what `ComputeContentHash` does today — every `Fnv1a.Add` at
`MovementRuleset.cs:388-397` runs on every call. B1 folds the four new values
inside `if (AppliesPressureInterrupt)`, and that flag is `false` on all six
existing presets. Nothing new therefore enters V6's fold, V6's `ContentHash` is
byte-identical, V6's state hash is unchanged, and V6's digest cannot move.

If any of the twelve pinned artifacts moves after B1, **the conditional fold is
wrong. Fix the fold. Never re-pin a literal or re-record a digest to make the
task go green** — that converts a caught mistake into a silent loss of the
property the freeze exists to prove.

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| **B1** | Add `MovementRuleset.AppliesPressureInterrupt` plus the three shared weight fields `SupportPressureWeightBasisPoints`, `IncomingDamageWeightBasisPoints`, `AllyCollapseWeightBasisPoints`. Fold all four **conditionally** inside `if (AppliesPressureInterrupt)` in `ComputeContentHash`, positioned after the `UsesEquipmentRelativeFootwork` fold at `:394`. Extend `ValidateEquipmentRelativeFootworkCoupling` per design section 6.3: weights all zero when the flag is `false`; non-negative and totalling exactly 10,000 when `true`; the flag `true` only when `UsesEquipmentRelativeFootwork` is `true`. Register `false` with zero weights on all six existing presets | `src/Hukbo.Core/Movement/MovementRuleset.cs`, `src/Hukbo.Core/Movement/MovementPresetRegistry.cs` | All six pinned `ContentHash` literals in `MovementPresetRegistryTests` (`:33`, `:42`, `:51`, `:60`, `:69`, `:79`) pass **unchanged**, and all six trajectory digest freeze tests pass unchanged | A2 | `dotnet test` filtered to `MovementPresetRegistryTests` and `MovementPresetFreezeTests`. **If any of the twelve moves, the conditional fold is wrong and the task is not done** |

---

## Phase 2 — the V7 preset and its state

B2 and B3 are serial behind B1 and behind each other because all three funnel
through `MovementRuleset.cs` or `MovementPresetRegistry.cs`. B4 and B5 own
disjoint files and run in parallel once B3 lands. B6 needs both.

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| **B2** | Add `LoadoutMovementProfile.PressureInterruptThresholdBasisPoints` as a **trailing optional constructor parameter defaulting to `0`**, validated non-negative, plus an immutable `WithPressureInterruptThreshold(int)` returning a new instance and never mutating the source. Fold it inside the existing per-row loop in `MovementRuleset.ComputeContentHash`, again inside `if (AppliesPressureInterrupt)`. Extend the coupling validator: every row's threshold zero when the flag is `false`, in `[1, SignalCeilingBasisPoints]` when `true`. **Leave `MovementProfileRegistrationTests.cs:141`'s `scalar < 15` literal at 15** — the sixteenth scalar is not folded under V6 and a V7-specific test covers it in E2. **The trailing default is load-bearing, not stylistic**: three direct `new LoadoutMovementProfile(` sites exist outside this task's file set — `tests/Hukbo.Core.Tests/Movement/MovementProfileRegistrationTests.cs:98` and `tests/Hukbo.Core.Tests/Movement/FacingRulesTests.cs:278` and `:310` — plus the target-typed `new(` sites in the profile files. A required seventeenth parameter would break every one of them and pull two more test files into this task's ownership. The trailing default leaves all of them compiling untouched, exactly as B4 and B5 do for their own signatures | `src/Hukbo.Core/Movement/LoadoutMovementProfile.cs`, `src/Hukbo.Core/Movement/MovementRuleset.cs`, `tests/Hukbo.Core.Tests/Movement/LoadoutMovementProfileTests.cs` | V6's `ContentHash` literal and trajectory digest still pass unchanged; `AssertRow`'s six call sites still compile without a seventeenth argument because the property is asserted separately; **`FacingRulesTests.cs` and `MovementProfileRegistrationTests.cs` compile with no edit at all — if either needs one, the parameter was not added as a trailing default and the task is not done** | B1 | `dotnet test` filtered to `MovementPresetRegistryTests`, `MovementPresetFreezeTests`, `LoadoutMovementProfileTests`, `MovementProfileRegistrationTests`, `FacingRulesTests` |
| **B3** | Append `MovementPresetId.EquipmentRelativeFootworkV7 = 7` with a summary saying what it adds and that it is reachable only by explicit selection. Register the V7 ruleset: V6's cohesion tunables and radii unchanged, `AppliesPressureInterrupt: true`, the three weights, and six rows built from V6's via `WithPressureInterruptThreshold`. Extend `IsRegistered` and `Get`. Amend the three exclusivity tests at `MovementPresetRegistryTests.cs:244`, `:276`, `:305` with an `Assert.True` for V7 each, and add a fourth test in the same shape for `AppliesPressureInterrupt` — `False` on V1 through V6, `True` on V7. **Do not pin a V7 `ContentHash` literal yet**; task E1 pins it after tuning, so it is pinned once. **Do not move `Scenario.MovementPreset`** | `src/Hukbo.Core/Movement/MovementPresetId.cs`, `src/Hukbo.Core/Movement/MovementPresetRegistry.cs`, `tests/Hukbo.Core.Tests/MovementPresetRegistryTests.cs` | V7 resolves through `MovementPresetRegistry.Get`, `Scenario.Validate` accepts it, all six earlier presets' pins and digests still pass, and `BattleSimulationTests.ExactlyOneLivingLeaderPerNonEmptyContingentAcrossEveryRegisteredMovementPreset` passes with V7 automatically included via its `Enum.GetValues` loop | B2 | `dotnet test` filtered to `MovementPresetRegistryTests`, `MovementPresetFreezeTests`, `ScenarioTests`, `BattleSimulationTests` |
| **B4** ∥ | Add three `AgentState` properties — `DamageTakenLastTick` (int), `PriorSupportAllies` (int), `BrokeOffUnderPressure` (bool) — declared **after** `FootworkTicksRemaining`, because `AgentState.cs:163-167` freezes the five properties above it in V6 fold order. Add a trailing `bool appliesPressureInterrupt = false` parameter to `StateHasher.Compute` and a **new** conditional block folding the three fields after the existing `movementContentHash is not null` block at `:122-129`, in declaration order, the bool as 1 or 0. Do not touch the existing block. Pass `_movementRules.AppliesPressureInterrupt` from `BattleSimulation.ComputeStateHash` at `:642-656` | `src/Hukbo.Core/Simulation/AgentState.cs`, `src/Hukbo.Core/Determinism/StateHasher.cs`, `src/Hukbo.Core/Simulation/BattleSimulation.cs` (`ComputeStateHash` only) | All six trajectory digests pass unchanged, proving V1 through V6 byte layout is untouched | B3 | `dotnet test` filtered to `MovementPresetFreezeTests`, `MovementStateHashTests`, `DeterminismTests` |
| **B5** ∥ | Add `WeaponMovementRules.SignalCeilingBasisPoints` and the pure predicate `ShouldPressureInterrupt` per design section 5.1 — the transition-only guard, three saturating basis-point signals, the weighted sum, `>=` comparison, all on `long` under `checked`. Add one trailing `bool pressureInterruptFired = false` parameter to `ResolveProvisionalFootwork` and a new **step 1a** branch returning `(FootworkPhase.Disengage, 0)`, inserted **after the argument validation at line 213 and before step 2's comment at line 215**. Do not renumber the existing ten steps. **Correct the class summary at `:16`, which currently asserts "nothing here divides"** | `src/Hukbo.Core/Movement/WeaponMovementRules.cs` | All six existing test call sites — `FootworkPhaseRulesTests.cs:39`, `ItakMovementTransitionTests.cs:54`, `KalisMovementTransitionTests.cs:711`, `KampilanMovementTests.cs:1252`, `TallHardwoodMovementTests.cs:2236`, `WasayMovementTests.cs:758` — compile and pass **without edits**, proving the default preserves the legacy ladder | B3 | `dotnet test` filtered to the six named files plus `MovementPipelineIntegrationTests`. **Any edit to those six files means the parameter was not added as a trailing default and the task is not done** |
| **B6** | Wire the interrupt into the simulation. In `ResolveEquipmentPosturesAndProvisionalFootwork` (`:1583-1639`), when `_movementRules.AppliesPressureInterrupt`: call `ShouldPressureInterrupt`, keep the answer in a new scratch array alongside `_provisionalFootworkPhases`, pass it into `ResolveProvisionalFootwork` at `:1625`, and on a firing interrupt write `AttackCooldownRemaining = AttackCooldownTicks`, `ComboStepsRemaining = 0`, `ComboTargetEntityId = null`. Also write the derived-scratch `_pressureBasisPoints` for the inspector. In `ApplyEquipmentAttackFootworkAndDeathCleanup` (`:2589`), gated on the same flag, stamp `DamageTakenLastTick` from `_damageTotals[index]` and `PriorSupportAllies` from `_localMovementContexts[index].SupportAllies`, maintain `BrokeOffUnderPressure` per design section 8 channel 1, and clear all three in the dead-agent branch at `:2594-2601`. **Add a cross-reference comment at both `AttackCooldownRemaining` writer sites — the new one and `ResolveComboTransition` at `:3724` — each naming the other** | `src/Hukbo.Core/Simulation/BattleSimulation.cs` | Under V7 an interrupted warrior leaves the tick in `Disengage` with a full cooldown and no chain; under V6 and V4 nothing changes and every existing test passes | B4, B5 | `dotnet test` filtered to `MovementPresetFreezeTests`, `MovementPipelineIntegrationTests`, `ComboChainTests`, `BattleSimulationTests`, `DeterminismTests` |

---

## Phase 3 — tests for the interrupt

C1 and C2 own new files and run in parallel with each other. C3 depends on B6
landing.

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| **C1** ∥ | Unit-test `ShouldPressureInterrupt` in isolation: threshold equality fires (`>=`, matching step 5's entry rule); each signal alone below and above the bar; a combination that fires only in the sum; saturation at `SignalCeilingBasisPoints`; `priorSupportAllies == 0` yielding a zero collapse signal; ally *growth* yielding zero rather than a negative; and the transition-only rule — no fire from `Approach`, `Engage`, `Disengage`, `Refuse`, `Regroup`, `Pursue`, or `None`. Add a ladder test proving a prior `Commit` with a live timer resolves to `Disengage` when the flag is set and to `Commit` when it is not | `tests/Hukbo.Core.Tests/Movement/FootworkPressureInterruptTests.cs` (new) | Every listed case is asserted and the file names no other test file's helper | B5 | `dotnet test` filtered to the new file |
| **C2** ∥ | State-hash and byte-layout tests: V1 through V6 fold exactly what they fold today with the new `StateHasher` parameter defaulted and explicitly `false`; V7 folds the three new per-agent fields and the four new ruleset values; a V7 ruleset differing only in one weight, and one differing only in one row threshold, produce different `ContentHash` values, while the same two variants under a `false` flag produce identical ones — the direct proof that the gate, not the field, is what moves the hash | `tests/Hukbo.Core.Tests/Movement/MovementStateHashTests.cs` | All listed assertions present and green | B4 | `dotnet test` filtered to `MovementStateHashTests` |
| **C3** | Close the combo-chain coverage gap named in design section 7. `ComboChainTests`' fixtures run under `PersistentContingentsV4` where the footwork stage never executes, so they cannot see this feature. Add coverage under V7 asserting an interrupted warrior's chain is cleared, its cooldown is `AttackCooldownTicks` and not `ComboCooldownTicks`, and its next landed blow carries no chain position — plus a V6 control with the same roster and seed asserting none of that happens | `tests/Hukbo.Core.Tests/ComboChainPressureInterruptTests.cs` (new) | The V7 assertions fail against a build with the interrupt stubbed out, and the V6 control passes in both builds | B6 | `dotnet test` filtered to the new file and to `ComboChainTests` |

---

## Phase 4 — the spectator channels

D1 is serial behind B6 because it edits `BattleSimulation.cs`. D2 and D3 own
disjoint client files and run in parallel behind D1.

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| **D1** | Project the interrupt onto the view. Add trailing `AgentView` members — `BrokeOffUnderPressure` (bool, default `false`), `PressureBasisPoints` (int, default 0), `PressureThresholdBasisPoints` (int, default 0) — defaulted for the same reason every member from `MovementResolution` down is, so presentation tests written before this feature still compile. Populate them in `AgentState.ToView` and `UpdateViews` (`:3973-3984`), reading `_pressureBasisPoints` for the derived value. Under every preset that does not apply the interrupt all three stay at their defaults | `src/Hukbo.Core/Simulation/AgentView.cs`, `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `tests/Hukbo.Core.Tests/Movement/MovementViewProjectionTests.cs` | A V7 view carries live values, a V6 view carries the defaults, and the digest freeze tests still pass — the view is not hashed, so they must | B6 | `dotnet test` filtered to `MovementViewProjectionTests`, `MovementPresetFreezeTests` |
| **D2** ∥ | Inspector rows. Extend `FormatFootworkLine` so a pressure-driven `Disengage` reads differently from an ordinary one, keeping `FootworkPhase.None` returning `null` so legacy output stays byte-identical. Add a pressure row showing the current weighted value against this warrior's own threshold in basis points, returning `null` when the threshold is zero. Update `ComputeRequiredHeight` for the extra row. **Pure helpers only — no `ArenaGame`, no graphics device, no sprite batch, no window** | `src/Hukbo.Client/UI/AgentInspectorContent.cs`, `tests/Hukbo.Client.Tests/AgentInspectorContentTests.cs` | Both new outputs asserted, legacy output asserted byte-identical, and the height math asserted against the real row count | D1 | `dotnet test` filtered to `AgentInspectorContentTests` |
| **D3** ∥ | Pawn-level break-off mark. Wire `AgentView.BrokeOffUnderPressure` through to a mark drawn above the pawn, following the `IsLeader` chain at `ArenaGame.Rendering.cs:973` and `PawnRenderer`. It must not collide with the leader mark or the selection ring. Colour comes from an existing semantic theme role; **no new role unless the theme tests demand one** | `src/Hukbo.Client/Rendering/PawnRenderer.cs`, `src/Hukbo.Client/ArenaGame.Rendering.cs`, `tests/Hukbo.Client.Tests/PawnRendererTests.cs` | The mark's placement math is asserted through a pure helper, and a view with the flag `false` produces no mark | D1 | `dotnet test` filtered to `PawnRendererTests` and the Client theme tests |

---

## Phase 5 — tuning, and only now

Nothing in phase 5 starts before phase 3 is complete. Tuning a threshold no
warrior could reach measures nothing, which is why the brief orders it this way.

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| **E0** | Build the calibration harness. It lives in `Hukbo.Core.Tests`, not in `Hukbo.Headless`: the headless runner has no movement-preset flag and `AgentState` is `internal`, so measurement code cannot reach what it needs from outside the test project. The harness runs the design section 2.2 matrix under a caller-supplied preset and reports terminal tick, outcome, survivors, the redefined design section 2.3 phase-flip percentage over ticks 101 through 400, and per-row interrupt counts. It is a hand-run harness, **not a `[Fact]`**, and must not join the canonical gate | `tests/Hukbo.Core.Tests/Movement/PressureInterruptCalibrationHarness.cs` (new) | The harness runs from a single explicit entry point and the gate's test count is unchanged by its presence | C1, C2, C3 | Harness output pasted into the E1 record; `./scripts/verify.ps1 -SkipBootstrap` showing the gate's test count unmoved |
| **E1** | Tune the three shared weights and the six per-row thresholds against the design section 2.1 termination bar and the section 2.3 phase-flip ceiling. Record every candidate set tried and its measured result, not only the winner. **Every value is labelled a provisional reconstruction of gameplay tuning in the code comment beside it and in the record.** V6 is not touched | `src/Hukbo.Core/Movement/MovementPresetRegistry.cs` (V7 entry only), `docs/archives/2026-08-06/movement/2026-07-31-movement-v7-calibration-record.md` (new) | Seeds 1, 2, 3, 5, 8 at 200 and 500 agents each reach a decisive outcome within 6,000 ticks, and the redefined phase-flip metric sits at or below 25% | E0 | Pasted harness output for all ten cells. **A cell that still draws is a failure, not a rounding issue** |
| **E2** | Pin V7's identity, once, now that its values are final. Add the `EquipmentRelativeFootworkV7ContentHash` literal to `MovementPresetRegistryTests`, **computed from the built code and never calculated by hand**. Capture the V7 trajectory digest fixture from a control run pinning `MovementPreset = EquipmentRelativeFootworkV7`, `CombatPreset = PrecolonialPhilippinesV2`, and `BodyRadiusRaw = 4 * FixedPoint.Scale`, exactly as `MovementPresetFreezeTests.CreateControlRun` does at `:288-340`. Add the V7 freeze test. Add the V7-specific folded-scalar test the B2 note deferred | `tests/Hukbo.Core.Tests/MovementPresetRegistryTests.cs`, `tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs`, `tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-movement-v7-digest.json` (new), `tests/Hukbo.Core.Tests/Movement/MovementProfileRegistrationTests.cs` | The V7 freeze test passes on a second run from a clean build, and all six earlier digests still pass | E1 | `dotnet test` filtered to `MovementPresetFreezeTests`, `MovementPresetRegistryTests`, `MovementProfileRegistrationTests` |

---

## Phase 6 — evidence and closure

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| **F0** | Determinism and logging neutrality under V7. Assert same seed plus same build gives identical state hash, event hash, winner, and ordered event stream across repeated runs, and that the seed-1 workload under V7 with logging off and at `trc` produces identical results — the rule `CLAUDE.md` section 5 states and an existing test already enforces for other presets | `tests/Hukbo.Core.Tests/DeterminismTests.cs`, `tests/Hukbo.Core.Tests/LoggingNeutralityTests.cs` (extend whichever file already owns the neutrality run) | Both properties asserted for V7 | E2 | `dotnet test` filtered to both files |
| **F1** ∥ | Create the manual smoke rows that do not exist. `docs/development/testing.md` has no footwork, posture, or pace rows at all. Add a section modelled on the "Leader marker and inspector annotation smoke" block at `:4487-4507`, including its honest preamble stating no interactive run was performed and naming which automated tests prove what, and its L-7-equivalent legacy regression row: launch under `PersistentContingentsV4` and confirm no warrior ever shows the break-off mark and no inspector line ever carries the pressure row. Cover the mark's visibility at 1× speed, its coexistence with the leader mark and the selection ring, both inspector rows, and that a spectator can see a warrior peel out of a losing knot without reading source. **Every row `PENDING`** | `docs/development/testing.md` | The section exists, every row is `PENDING`, and the preamble is honest about what was not run | D2, D3 | Read-back confirming no row reads `PASS` |
| **F2** | Re-measure and record. Run the section 2.2 protocol under V7 and compare against the A0 baseline: the termination bar per cell, and median `p50Milliseconds` against `PersistentContingentsV4` with the 2.0× and 2.5× ceilings. **If the termination bar passes and the `p50Milliseconds` ceiling fails, record it as a separate performance question, not a calibration failure** — decision D2 says so, and `ResolveCollisions` at 58.11% to 77.44% of tick time is the flagged suspect | `docs/archives/2026-08-06/movement/2026-07-31-movement-v7-calibration-record.md` | Twenty cells recorded with real output, and an explicit verdict per criterion | E2, F0 | Pasted run output |
| **F3** | Run the canonical gate once, on the integrated branch, and record its exact output. Then archive the finished plans into `docs/archives/2026-07-31/movement/` with the "Archived: reference only" banner, per `CLAUDE.md` section 6 step 5. **`Scenario.MovementPreset` does not move** — decision D6 stands until the section 2.1 bar passes and a separate decision is taken on the evidence | `docs/development/testing.md` (results section only), `docs/archives/2026-07-31/movement/` | The gate's real output is pasted, and no claim exceeds it | F1, F2 | `./scripts/verify.ps1`, output pasted verbatim |

---

## Parallelism summary

Tasks marked ∥ may run at the same time as the other ∥ tasks in their group.
Everything else is serial.

| Group | Parallel tasks | Why they are safe together |
| --- | --- | --- |
| Phase 0 | **A0, A1** | Two different new or unrelated documents; no source file touched |
| Phase 2 | **B4, B5** | `AgentState.cs` + `StateHasher.cs` + `BattleSimulation.ComputeStateHash` against `WeaponMovementRules.cs`. Disjoint |
| Phase 3 | **C1, C2** | Two different test files, one new |
| Phase 4 | **D2, D3** | `AgentInspectorContent.cs` against `PawnRenderer.cs` and `ArenaGame.Rendering.cs`. Disjoint |
| Phase 6 | **F1** with F0 | A documentation file against test files |

Serial and why:

| Task | Funnels through |
| --- | --- |
| A2 → B1 → B2 → B3 | `MovementRuleset.cs` and `MovementPresetRegistry.cs` |
| B4, B5 → B6 | B6 needs both, and B6 owns `BattleSimulation.cs` alone |
| B6 → C3, D1 | Both need the wired behaviour; D1 also edits `BattleSimulation.cs` |
| D1 → D2, D3 | Both read the new `AgentView` members |
| C1, C2, C3 → E0 → E1 → E2 | Tuning follows the interrupt; pinning follows tuning |
| E2 → F0 → F2 → F3 | Evidence follows the frozen values |

Two tasks — **B4 and D1** — both touch `src/Hukbo.Core/Simulation/BattleSimulation.cs`,
as does **B6**. All three are serial with respect to each other and none is
marked ∥ against another `BattleSimulation.cs` task. B4's edit is confined to
`ComputeStateHash`; B6's to the two footwork stages; D1's to `UpdateViews`. That
confinement is deliberate and should be preserved so a reviewer can read each
diff on its own.

## Verification criteria for the whole plan

The work is done when all of the following hold, each with real output pasted:

1. `./scripts/verify.ps1` passes, and its output is recorded verbatim.
2. All six pre-existing `MovementRuleset.ContentHash` literals are unchanged.
3. All six pre-existing trajectory digest fixtures are unchanged and their freeze
   tests pass.
4. V7 has its own content-hash pin and its own trajectory digest, each captured
   once from the built code after tuning.
5. Seeds 1, 2, 3, 5, and 8, at 200 and 500 agents under combat preset
   `PrecolonialPhilippinesV2`, each reach a decisive outcome within 6,000 ticks
   under V7.
6. The redefined phase-flip metric — posture-intent changes only, excluding the
   scripted `Commit` and `Recover` transitions — sits at or below 25% over ticks
   101 through 400.
7. Median `p50Milliseconds` against `PersistentContingentsV4` is recorded for
   both sizes, with an explicit verdict against the 2.0× and 2.5× ceilings, and
   a failure there is recorded as a separate performance question rather than a
   calibration failure.
8. `Scenario.MovementPreset` is still `PersistentContingentsV4`.
9. Every new smoke row in `docs/development/testing.md` reads `PENDING`.
10. Every tuning value in the shipped V7 entry carries a provisional-gameplay-tuning
    label in the code and in the calibration record, and none is presented as a
    historical measurement.
