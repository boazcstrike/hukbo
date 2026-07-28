# GPU-Instanced Arena Rendering — Plan

**Status: this plan authorizes Phases 1 and 2 only.** Phase 3 — the instanced
backend — is listed below in full so that the work is legible and estimable,
but it is **not authorized**. No task numbered GPU-024 or higher may be started
until the two-clause go/no-go trigger in section 4 of this document fires on a
recorded, committed Phase 2 re-measurement. A task list is not an
authorization, and listing Phase 3 here does not turn the design's gate into a
formality.

Date: 2026-07-28. Branch: `gpu-render`. Worktree:
`.claude/worktrees/gpu-render`.

Design of record:
[`2026-07-28-gpu-render-design.md`](2026-07-28-gpu-render-design.md). Where this
plan and the design disagree on a fact, the design is the authority on intent
and this plan is the authority on sequencing. Three factual corrections that
this plan applies over the design are recorded in section 2.

---

## 1. Why the work is phased, in one paragraph

The design's central finding is that at 500 units the recorded frame cost does
not scale with the amount of geometry submitted to the graphics device. All
three camera stations at 500 units land within six hundredths of a millisecond
of one another — 5.27, 5.30, and 5.33 milliseconds — while drawing 9,326,
9,326, and 1,028 quads respectively. That is a floor, not a curve, and a
renderer optimisation that changes how quads reach the device cannot move a
floor that is indifferent to how many quads there are. Phase 1 exists to find
out what the floor is made of. Phase 2 removes the per-agent CPU work that is
visible in source and provably redundant regardless of what Phase 1 finds.
Phase 3 is built only if, after both, the frame still misses budget *and* the
overrun is genuinely in the submission path.

---

## 2. Three corrections this plan applies over the design

These were verified against the files on disk in this worktree on 2026-07-28.
Each one changes what a task has to do, so each is stated before the task table
rather than buried in it.

### 2.1 The Core is not the obstacle at 500 per team, and only two client constants are

`Scenario.MaximumAgentsPerFaction` is `10_000`
(`src/Hukbo.Core/Simulation/Scenario.cs` line 18), so the simulation imposes no
bound anywhere near the target. The only two constants standing between the
current build and a 1,000-unit battle are
`ArmyCompositionStepper.MaximumUnitsPerTeam`, which is 250
(`src/Hukbo.Client/UI/ArmyCompositionStepper.cs` line 23), and
`ClientSettings.DefaultUnitsPerTeam`, which is also 250
(`src/Hukbo.Client/Settings/ClientSettings.cs` line 61). The design already says
this; it is restated here because it makes GPU-022 a two-constant change rather
than an investigation.

### 2.2 The recorded baseline artifacts do exist, but not where a fresh clone can reach them

The design says at section 4.1 that neither
`artifacts/render-baseline-2026-07-28.json` nor
`artifacts/render-baseline-500-2026-07-28.json` exists. Both files do exist, in
a sibling worktree, at
`.claude/worktrees/improve-visuals/artifacts/render-baseline-2026-07-28.json`
and
`.claude/worktrees/improve-visuals/artifacts/render-baseline-500-2026-07-28.json`.
They are untracked, because `.gitignore` line 13 matches `[Aa]rtifacts/`, so
they have never been committed and a fresh clone cannot open either one.

The design's conclusion is therefore correct and its premise is not. The tables
in `docs/development/testing.md` are backed by real files that a person on this
machine can read today, and the citation in that document still points at
nothing a fresh clone can resolve. The remedy is unchanged — move the artifacts
to a tracked location under `docs/development/` and repair the citation — but
GPU-009 is a *move* of files that exist, not a *re-capture* of files that do
not. Anyone performing GPU-009 must confirm the source files are present before
starting, and must not silently substitute a fresh capture for the recorded
one.

### 2.3 The "96 percent unattributed" figure is station-dependent, and the design's framing generalises it

The design quotes 96 percent unattributed at 500 units. That figure is true at
the maximum-zoom station and only there. Reading every recorded cell
(`docs/development/testing.md` lines 116 to 126), where "attributed" means
geometry-build plus submit as a fraction of the frame:

| Units | Station | Quads | Frame p50 | Geometry build p50 | Submit p50 | Unattributed share of frame |
| ---: | --- | ---: | ---: | ---: | ---: | ---: |
| 200 | minimum zoom | 4 076 | 2.30 ms | 546.3 us | 1 354.5 us | ~17 % |
| 200 | default fit | 4 076 | 0.77 ms | 130.2 us | 466.9 us | ~22 % |
| 200 | maximum zoom | 1 028 | 0.22 ms | 42.4 us | 75.8 us | ~45 % |
| 500 | minimum zoom | 9 326 | 5.27 ms | 1 372.7 us | 3 526.6 us | ~7 % |
| 500 | default fit | 9 326 | 5.30 ms | 207.1 us | 4 853.2 us | ~4 % |
| 500 | maximum zoom | 1 028 | 5.33 ms | 90.3 us | 104.9 us | ~96 % |

At the 500-unit default-fit station — which is the station the go/no-go budget
in section 4 is written against — the instrumented spans account for
approximately 96 percent of the frame, not 4 percent. The unattributed
remainder is enormous at maximum zoom and small at the other two stations.

This does not weaken the case for phasing. It sharpens it, because of the next
correction.

### 2.4 The two instrumented spans are conflated, and this is the strongest argument for gating Phase 3

`ArenaGame.Rendering.cs` lines 50 to 64 time `RecordArenaRenderMetrics` and
report the result as `geometryBuildMicroseconds`. That method
(`ArenaGame.Rendering.cs` lines 128 to 138) is the **probe's own duplicate
recording pass**. It exists only inside the `_renderProbeEnabled` branch, and a
normal run never evaluates it. So `geometryBuildMicroseconds` is not the cost
of building the geometry the renderer draws. It is the cost of the probe
counting that geometry a second time.

`ArenaGame.Rendering.cs` lines 66 to 81 time `DrawArenaLayer` and report the
result as `submitMicroseconds`. `DrawArenaLayer` builds the **real** geometry —
every `PawnGeometry.Create` call the renderer actually draws from happens inside
it — and issues every `SpriteBatch.Draw` call. So `submitMicroseconds`
conflates CPU geometry construction with submission, in one number, with no way
on disk today to tell the two apart.

The consequence is direct and must not be softened. At the 500-unit default-fit
station, 4,853 microseconds of a 5,300-microsecond frame sit inside a span whose
composition is unknown. **Whether that 4.85 milliseconds is Phase 2 work
(per-agent CPU geometry) or Phase 3 work (submission) is genuinely undetermined
by any recorded measurement in this repository.** It could plausibly be either.

This is the single strongest justification for the phasing the design chose.
Building an instanced backend against a number that might be mostly CPU layout
construction would be building against a guess. It is also why GPU-004,
disaggregating the Submit span, is scheduled early and is named a prerequisite
for evaluating the go/no-go trigger in section 4: clause 2 of that trigger asks
what fraction of the frame is submission, and today no instrument in this
repository can answer that question.

### 2.5 One further correction, to a claim in `testing.md`

`docs/development/testing.md` lines 128 to 131 state that the Tier 2 figures
`batches` 1 and `textureBinds` 1 are "measured, not assumed". They are neither
measured nor assumed — they are hardcoded. `RecordArenaRenderMetrics` calls
`_renderMetricsRecorder.AddBatch()` and `AddTextureBind()` exactly once each,
unconditionally, at `ArenaGame.Rendering.cs` lines 136 and 137. The recorded
value of 1 is what those two lines write, not what the backend did. The same
applies to the `submissions equals quads` identity noted on line 129:
`RecordQuads` (`ArenaGame.Rendering.cs` lines 239 to 252) increments
`AddSubmission()` once per quad in a loop, so the identity is definitional
rather than observed. GPU-011 corrects this wording.

---

## 3. Task table

Task identifiers run from GPU-001 upward and are grouped by phase. Each task is
sized for one agent. The dependency and parallelism note in section 6 states
which identifiers must run serially and why.

### Phase 1 — establish measurement truth (authorized)

Entry condition: none. Phase 1 changes measurement only and is expected to
produce no visual change whatsoever.

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| GPU-001 | Extend the metrics contract with the new Tier 1 spans, the `PawnGeometry.Create` invocation counter, and the probe-overhead span. Add `AddClearMicroseconds`, `AddLayoutMicroseconds`, `AddHoverSelectionMicroseconds`, `AddUiLayerMicroseconds`, `AddBaseDrawMicroseconds`, `AddArenaGeometryMicroseconds`, `AddProbeOverheadMicroseconds`, and `AddPawnGeometryInvocations` to `IRenderMetricsRecorder`, with the matching fields on `RenderMetricsSnapshot`, no-op implementations on `NullRenderMetricsRecorder`, and real implementations on `SpriteBatchRenderMetricsRecorder`. Every new call must allocate nothing when the recorder is disabled. | `src/Hukbo.Client/Rendering/RenderMetrics.cs`, `tests/Hukbo.Client.Tests/RenderMetricsTests.cs` | The new members exist, the disabled recorder is still a no-op on every one of them, and `RenderMetricsTests` covers accumulation and reset for each new field | — | `./scripts/test.ps1 -Configuration Release`; new cases in `tests/Hukbo.Client.Tests/RenderMetricsTests.cs` |
| GPU-002 | Extend the probe report schema to carry the new spans as percentiles, the invocation counter as a maximum, and two new fingerprint fields: `VerticalRetraceSynchronized` (bool) and `ProbeDuplicationFactor` (double). Extend `RenderProbeStatistics.Summarize` to compute the new percentiles. | `src/Hukbo.Client/Rendering/RenderProbeReport.cs`, `tests/Hukbo.Client.Tests/RenderProbeReportTests.cs` | `RenderProbeStationResult` and `RenderProbeFingerprint` carry the new fields, they serialize and round-trip, and the percentile arithmetic is asserted against a hand-computed sample set | GPU-001 | `./scripts/test.ps1 -Configuration Release`; new cases in `tests/Hukbo.Client.Tests/RenderProbeReportTests.cs` |
| GPU-003 | Instrument the currently unattributed region of `Draw` with five of the new spans: `GraphicsDevice.Clear` (line 35), `GetLayout` plus `_camera.Fit` (lines 46 to 47), `UpdateHoverSelection` (line 48), `DrawUiLayer` (lines 83 to 89), and `base.Draw` (line 91). Every timestamp read stays inside the `_renderProbeEnabled` branch. | `src/Hukbo.Client/ArenaGame.Rendering.cs` | A probe run reports a non-zero value for each new span, and the sum of all named spans plus the residual equals the reported frame time to within measurement noise | GPU-001, GPU-002 | Hand-run probe at 200 units; every new field non-zero in the emitted JSON |
| GPU-004 | **Disaggregate the Submit span.** Split `DrawArenaLayer`'s single span into real geometry construction and real submission. Accumulate `arenaGeometryMicroseconds` around the `PawnGeometry.Create`/layout construction inside the draw path, and narrow `submitMicroseconds` to the `SpriteBatch.Draw` submission work that remains. The instrumentation is probe-only and its own overhead is reported separately under GPU-005. This task is the prerequisite for evaluating clause 2 of the go/no-go trigger. | `src/Hukbo.Client/ArenaGame.Rendering.cs`, `src/Hukbo.Client/Rendering/PawnRenderer.cs` | A probe run reports `arenaGeometryMicroseconds` and `submitMicroseconds` as two separate non-zero figures whose sum is within measurement noise of the single figure the current build reports for the same station and army size | GPU-003 | Hand-run probe at 200 and 500 units; the two new figures summing to the previously recorded single `submitMicroseconds` for the same station |
| GPU-005 | Separate probe overhead from renderer cost. Time `RecordArenaRenderMetrics` into `probeOverheadMicroseconds` rather than into `geometryBuildMicroseconds`, and count `PawnGeometry.Create` invocations per frame at every call site so the duplication factor is derived rather than assumed. Report the duplication factor on the fingerprint. | `src/Hukbo.Client/ArenaGame.Rendering.cs` | The probe report distinguishes the renderer's own geometry cost from the probe's duplicate counting pass, and `ProbeDuplicationFactor` on the fingerprint is computed from the recorded invocation count rather than hardcoded | GPU-004 | Hand-run probe; `probeOverheadMicroseconds` and `pawnGeometryInvocationsMaximum` present and self-consistent in the emitted JSON |
| GPU-006 | Add a probe-only vertical-retrace override to `ArenaGame`, in the same style as the existing `SetProbeCameraZoom` seam, and have the probe disable vertical retrace for the duration of a measurement run. The shipped client keeps `SynchronizeWithVerticalRetrace = true` and is not altered. | `src/Hukbo.Client/ArenaGame.cs`, `tools/Hukbo.Tools.RenderProbe/Program.cs` | A probe run sets `SynchronizeWithVerticalRetrace = false`, records `VerticalRetraceSynchronized: false` on the fingerprint, and a normal `./scripts/run.ps1` launch is unchanged | GPU-002 | Hand-run probe; fingerprint field present and false. Manual confirmation that `./scripts/run.ps1` still runs with retrace enabled |
| GPU-007 | Extend the `--matrix` agent-count axis from `{200, 500}` to `{200, 500, 1000}`, and extend `RenderMatrixReport.AxesNote` to disclose the vertical-retrace change alongside the existing grass and motion disclosure. | `tools/Hukbo.Tools.RenderProbe/Program.cs`, `tools/Hukbo.Tools.RenderProbe/RenderMatrixReport.cs` | A `--matrix` run produces three cells, and the emitted `AxesNote` names the retrace setting | GPU-006 | Hand-run `Hukbo.Tools.RenderProbe.exe --matrix 1 120 <path>`; three cells present in the output JSON |
| GPU-008 | Confirm on disk that the two recorded baseline artifacts named in section 2.2 are present in the sibling worktree before any move is attempted, and record their SHA-256 hashes in the plan's evidence trail. This is a read-only precondition check and must not re-capture or regenerate anything. | none (read-only) | The two files are confirmed present with recorded hashes, or their absence is reported and GPU-009 is re-scoped to a fresh capture | — | Directory listing and hash output pasted into the task's completion note |
| GPU-009 | Move the two recorded baseline artifacts to a tracked location at `docs/development/render-baselines/`, keeping their existing filenames, and repair the citation in `docs/development/testing.md` lines 108 to 110 to point at the tracked path. | `docs/development/render-baselines/render-baseline-2026-07-28.json`, `docs/development/render-baselines/render-baseline-500-2026-07-28.json`, `docs/development/testing.md` | Both artifacts are tracked by git, and the path cited in `testing.md` resolves in a fresh clone | GPU-008 | `git status` showing both files tracked; `Test-Path` against the cited path from a clean checkout |
| GPU-010 | Add a documentation-hygiene test asserting that every render-baseline artifact path cited in `docs/development/testing.md` resolves to a file that exists in the repository. This test fails before GPU-009 and passes after it. | `tests/Hukbo.Client.Tests/SourceHygieneTests.cs` | The test exists, is named for what it guards, and passes | GPU-009 | `./scripts/test.ps1 -Configuration Release`; the new case in `SourceHygieneTests.cs` |
| GPU-011 | Correct the overstated claim at `docs/development/testing.md` lines 128 to 131. Replace "measured, not assumed" with an accurate statement: `batches` and `textureBinds` are written unconditionally as 1 by `ArenaGame.Rendering.cs` lines 136 and 137, and `submissions equals quads` follows definitionally from the loop in `RecordQuads` at lines 248 to 251. State what the backend invariant actually rests on, which is source inspection rather than instrumentation. | `docs/development/testing.md` | The paragraph no longer claims instrumentation it does not have, and names the two source lines the values come from | GPU-009 | Reviewer reading the amended paragraph against `ArenaGame.Rendering.cs` lines 136, 137, and 248 to 251 |
| GPU-012 | Capture the Phase 1 baseline. Hand-run the probe matrix at 200, 500, and 1,000 units, seed 1, 120 frames per station, Release configuration, vertical retrace disabled. Commit the JSON under `docs/development/render-baselines/`. Record the three tables in `docs/development/testing.md` with every new span named. Evaluate the Phase 1 exit criteria in section 5.1 and state honestly whether each is met. | `docs/development/render-baselines/render-matrix-2026-<date>.json`, `docs/development/testing.md` | The three cells are recorded, every millisecond of `Draw` is attributed to a named span, and the largest unattributed residual at 1,000 units is stated as a percentage | GPU-003 through GPU-011 | The hand-run itself. **Not delegable.** The committed JSON artifact is the evidence |

### Phase 2 — remove per-agent CPU cost (authorized)

Entry condition: Phase 1 complete and its baseline recorded. Phase 2 is
expected to produce **no visual change at all**. Every task below removes
duplicated or unnecessary work; the set of pixels drawn must be identical
before and after.

The four pure-helper tasks — GPU-013, GPU-015, GPU-017, and GPU-019 — touch
disjoint files and may run in parallel. The four adoption tasks that follow them
all funnel through `ArenaGame.Rendering.cs` and must run one at a time.

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| GPU-013 | **R1, helper.** Add a single entry point that returns the pose-blind `VisualBounds` alongside the posed `PawnLayout` from one call, so a visible pawn stops paying for two full layout constructions per frame. The pose-blind bounds must be computed by the cheap subset of `PawnGeometry.Create` that actually determines them, not by running the full layout a second time. Keep the existing `GetBounds` and `Create` in place as the reference the equivalence test compares against. | `src/Hukbo.Client/Rendering/PawnRenderer.cs`, `src/Hukbo.Client/Rendering/PawnGeometry.cs`, `tests/Hukbo.Client.Tests/PawnGeometryTests.cs` | The new entry point exists and, over a representative grid of appearance, zoom, and pose inputs, returns bounds bit-identical to `PawnRenderer.GetBounds` and a layout bit-identical to `PawnGeometry.Create` | GPU-012 | `./scripts/test.ps1 -Configuration Release`; the equivalence cases in `PawnGeometryTests.cs` |
| GPU-014 | **R1, adoption.** Switch `DrawPawns` (`ArenaGame.Rendering.cs` lines 503 to 564) and `RecordPawnQuads` (lines 148 to 198) to the single-call entry point. The cull continues to test the pose-blind bounds exactly as it does today, so the drawn set is unchanged by construction. | `src/Hukbo.Client/ArenaGame.Rendering.cs` | Both call sites use the single call, the `PawnGeometry.Create` invocation count per visible pawn per frame recorded by GPU-005 halves, and `PawnQuadCountTests` still pins 17, 19, 20, and 40 | GPU-013 | `./scripts/test.ps1 -Configuration Release`; `tests/Hukbo.Client.Tests/PawnQuadCountTests.cs`; the invocation counter in a hand-run probe |
| GPU-015 | **R2a, helper.** Derive a conservative, appearance-blind and pose-blind upper-bound radius around a pawn's foot anchor at a given zoom, as a pure helper, together with a test that proves the radius is an upper bound over **every** appearance the catalogs can produce. Answer design open question 4 as part of this task: report how much the pre-cull actually admits at each camera station, and state plainly whether it is worth adopting. If the provable bound is so generous that the pre-cull admits nearly everything, say so and recommend that GPU-016 be dropped. | `src/Hukbo.Client/Rendering/ConservativePawnCull.cs` (new), `tests/Hukbo.Client.Tests/ConservativePawnCullTests.cs` (new) | The helper exists, the upper-bound test passes over the full catalog cross-product, and the task's completion note states the admitted fraction at each of the three camera stations | GPU-012 | `./scripts/test.ps1 -Configuration Release`; `ConservativePawnCullTests.cs` |
| GPU-016 | **R2a, adoption.** Move the conservative pre-cull ahead of `PawnAppearanceFactory.Create` at `ArenaGame.Rendering.cs` line 524, so a distant pawn never resolves an appearance. The exact pose-blind test still runs afterward on the pawns that survive, so the final drawn set is unchanged. **Conditional on GPU-015's recommendation** — if GPU-015 reports the pre-cull is not worth having, this task is dropped and that decision is recorded. | `src/Hukbo.Client/ArenaGame.Rendering.cs` | Appearance resolution runs for a number of pawns proportional to visible pawns rather than to total agents, and the drawn set is unchanged at all three camera stations | GPU-014, GPU-015 | `./scripts/test.ps1 -Configuration Release`; a hand-run probe at 1,000 units maximum zoom showing the appearance-resolution count collapse |
| GPU-017 | **R2b, helper.** Build the bounded appearance cache declared below under *The appearance cache declaration*, as a flat array indexed by the agent's ordinal position with the key stored alongside for verification. `PawnAppearanceFactory.Create` remains the single authority and the cache never computes an appearance itself. Expose hit, miss, and fill counters through `IRenderMetricsRecorder`. Include the cold-cache equivalence test and the size-bound test — both are required, not optional. Include the load-bearing-assumption test that fails if `AgentView.Loadout` ever becomes mutable mid-battle. | `src/Hukbo.Client/Presentation/PawnAppearanceCache.cs` (new), `tests/Hukbo.Client.Tests/PawnAppearanceCacheTests.cs` (new), `src/Hukbo.Client/Rendering/RenderMetrics.cs` | The cache exists with its full declaration recorded in section 3.2, the cold-cache equivalence test passes, the size-bound test pins capacity at `2 * ArmyCompositionStepper.MaximumUnitsPerTeam`, and the counters are reported by a probe run | GPU-012 | `./scripts/test.ps1 -Configuration Release`; `PawnAppearanceCacheTests.cs` |
| GPU-018 | **R2b, adoption.** Read the appearance from the cache in the pawn loop, and clear the cache at the three points that already rebuild presentation state — scenario reset, next round, and full reset. | `src/Hukbo.Client/ArenaGame.Rendering.cs`, `src/Hukbo.Client/Presentation/PresentationCoordinator.cs` | The pawn loop resolves appearance through the cache, the cache is cleared at all three reset points, and a probe run reports a hit rate approaching 1 after the first frame of a battle | GPU-016, GPU-017 | `./scripts/test.ps1 -Configuration Release`; `tests/Hukbo.Client.Tests/PresentationCoordinatorTests.cs`; cache counters in a hand-run probe |
| GPU-019 | **R3, helper.** Replace the per-pawn linear scan in `HitEffectSystem.GetPulseStrength` (`src/Hukbo.Client/Presentation/HitEffectSystem.cs` lines 96 to 115) with a per-frame lookup built once from the at-most-256 live effects. This is not a cache: it is discarded and rebuilt every frame and holds nothing across frames. Keep `GetPulseStrength` as the reference the equivalence test compares against, including its maximum-over-effects behaviour at line 102 and its lethal-exclusion behaviour at line 111. | `src/Hukbo.Client/Presentation/HitEffectSystem.cs`, `tests/Hukbo.Client.Tests/HitEffectSystemTests.cs` | The rebuilt lookup returns exactly what `GetPulseStrength` returns today for every entity and every live-effect configuration, including both edge behaviours | GPU-012 | `./scripts/test.ps1 -Configuration Release`; the equivalence cases in `HitEffectSystemTests.cs` |
| GPU-020 | **R3, adoption.** Build the pulse lookup once before the pawn loop and read it inside the loop, replacing the per-pawn call at `ArenaGame.Rendering.cs` lines 554 to 556. | `src/Hukbo.Client/ArenaGame.Rendering.cs` | The pawn loop performs one lookup read per pawn rather than one scan per pawn, and the rendered pulse strengths are unchanged | GPU-018, GPU-019 | `./scripts/test.ps1 -Configuration Release`; visual comparison against a pre-change run at the same seed |
| GPU-021 | **Reduce the `UpdateHoverSelection` cost.** `UpdateHoverSelection` (`src/Hukbo.Client/ArenaGame.cs` lines 1305 to 1335) walks the full agent list every frame the pointer is inside the arena, and `DrawUiLayer` performs a second full-list operation at `ArenaGame.Rendering.cs` line 286. Act on what GPU-003's hover-selection span actually measured: if the span is a material fraction of the frame at 1,000 units, remove the duplicate full-list walk; if it is not, record the measured figure and take no action. **This task is measurement-led and may legitimately end in no code change.** | `src/Hukbo.Client/ArenaGame.cs`, `src/Hukbo.Client/Presentation/AgentSelection.cs`, `tests/Hukbo.Client.Tests/AgentSelectionTests.cs` | Either the duplicate walk is removed with selection behaviour unchanged under `AgentSelectionTests`, or the measured span is recorded and the decision not to act is written down with its number | GPU-012 | `./scripts/test.ps1 -Configuration Release`; the hover-selection span from GPU-012's baseline |
| GPU-022 | Raise `ArmyCompositionStepper.MaximumUnitsPerTeam` from 250 to 500. `MinimumUnitsPerTeam` stays at 4 and `ClientSettings.DefaultUnitsPerTeam` stays at 250, so the default experience is unchanged and the larger battle is opt-in. Extend the stepper's tests to cover the new maximum and the clamp behaviour at the new boundary. | `src/Hukbo.Client/UI/ArmyCompositionStepper.cs`, `tests/Hukbo.Client.Tests/ArmyCompositionStepperTests.cs` | The stepper accepts 500 per team, clamps above it, still refuses below 4, and the composition panel still fits the window at the new maximum | GPU-012 | `./scripts/test.ps1 -Configuration Release`; `ArmyCompositionStepperTests.cs`, `ArmyCompositionPanelTests.cs`. Window fit is a **manual smoke row**, not a test |
| GPU-023 | **The go/no-go measurement.** Hand-run the Phase 1 probe at 200, 500, and 1,000 units after all Phase 2 tasks land. Commit the JSON. Record the tables in `docs/development/testing.md`. Evaluate both clauses of the trigger in section 4 and state the verdict in writing, with the two numbers the verdict rests on. | `docs/development/render-baselines/render-matrix-phase2-<date>.json`, `docs/development/testing.md`, `docs/plans/gpu-render/2026-07-28-gpu-render.md` | The re-measurement is recorded and the trigger verdict is written down as GO or NO-GO with both clause figures quoted | GPU-013 through GPU-022 | The hand-run itself. **Not delegable.** No agent report, compilation success, or test pass substitutes for it |

### The appearance cache declaration

`SIMULATION-GAME-STANDARDS.md` lines 215 to 217 require every cache to declare
the following before it is written, and `CLAUDE.md` line 306 prohibits unbounded
caches outright. GPU-017 implements exactly this declaration and nothing wider.

- **Source.** `PawnAppearanceFactory.Create`
  (`src/Hukbo.Client/Presentation/PawnAppearanceFactory.cs` lines 37 to 107).
  It remains the single authority. The cache never computes an appearance
  itself; it only remembers one the factory produced.
- **Key.** The triple `(EntityId, Weapon, Shield)`. Not `EntityId` alone.
  Keying on identity alone would silently return a stale appearance if a
  loadout ever became mutable, and the key should not depend on a property the
  simulation is not contractually obliged to keep.
- **Value.** The `PawnAppearance` value.
- **Size bound.** `2 * ArmyCompositionStepper.MaximumUnitsPerTeam` entries,
  which is 1,000 after GPU-022. This is a hard capacity allocated once, not a
  growth target. Because a battle's agent set is fixed at scenario creation,
  the cache fills exactly once and never evicts.
- **Lifetime.** One battle. Cleared on scenario reset, on next round, and on
  full reset.
- **Invalidation.** None during a battle, because no key input can change
  during a battle. This is a load-bearing assumption and GPU-017 must assert
  it: if `AgentView.Loadout` ever becomes mutable mid-battle, that test fails
  and this cache is invalid.
- **Counters.** Hits, misses, and fill count, exposed through the existing
  `IRenderMetricsRecorder` seam so a probe run reports them.
- **Cold-cache equivalence test.** For a fixed scenario seed, the frame's
  resolved appearance for every agent must be identical whether the cache is
  cold or warm. Required, not optional. Testable as a pure-helper test with no
  graphics device.

### Deliberately not a task: the `IReadOnlyList<AgentView>` iteration cost

The design names a fourth per-agent cost at its section 4.2 and this plan
deliberately does not carry a task for it. It is recorded here so that its
absence is a decision rather than an oversight.

`BattleSimulation.Agents` is declared as
`public IReadOnlyList<AgentView> Agents => _agents;`
(`src/Hukbo.Core/Simulation/BattleSimulation.cs` line 107). Every `foreach` over
that property in the client allocates a boxed enumerator and pays an
interface-dispatched indexer call per element, and `AgentView`
(`src/Hukbo.Core/Simulation/AgentView.cs` lines 19 to 31) is a fourteen-field
record struct copied on each access. The render path walks that list at least
three times per frame.

Two reasons this is not a Phase 2 task. First, the fix lives in
`src/Hukbo.Core/`, and this plan's determinism statement in section 9 declares
that it changes nothing under `Hukbo.Core`. Widening that declaration for a
presentation-motivated micro-optimisation would weaken the strongest guarantee
in the plan for an unmeasured gain. Second, and decisively, the gain **is**
unmeasured: Phase 1's new spans will report how much of the frame these walks
actually cost, and GPU-021 already covers the two full-list selection walks
without touching Core.

If Phase 1's measurement shows this cost is material at 1,000 units, it becomes
its own proposal against `Hukbo.Core`, with its own before-and-after seed-1
hashes, and not a line item smuggled into a rendering plan.

### Phase 3 — the instanced backend (**NOT AUTHORIZED**)

**Entry condition: the two-clause trigger in section 4 has fired on GPU-023's
recorded, committed re-measurement.** Nothing in this sub-table may be started
otherwise. The tasks are listed so that the go/no-go decision is a decision
about a known quantity rather than an open-ended one. They are listed; they are
not approved.

GPU-024 is scheduled first deliberately: it is the single item in the design
most likely to cost unplanned time, and every task after it depends on what it
finds.

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| GPU-024 | **Empirical spike, blocking.** `VertexElement.UsageIndex` is documented as being adjusted internally by MonoGame when multiple vertex buffers are bound, and the exact adjustment rule is not documented anywhere that could be checked. Bind the two-slot layout, draw a small number of instances with known distinct per-instance values, and verify which shader semantic each attribute actually arrived at. The vertex declaration is not fixed until this returns. | `tools/Hukbo.Tools.RenderProbe/` spike code, discarded after the finding is recorded | The observed attribute-binding rule is recorded in writing, and the section 8.4 layout is either confirmed or amended to match what was observed | GPU-023 (trigger fired) | The spike run itself, on a real graphics device. **Not delegable** |
| GPU-025 | Author the instance-record packing helper: the pure function turning a position, scale, rotation, origin selector, and colour into the 28-byte instance record, with pack-then-unpack round-trip tests asserting exact byte layout and field offsets. | `src/Hukbo.Client/Rendering/InstanceRecord.cs` (new), `tests/Hukbo.Client.Tests/InstanceRecordTests.cs` (new) | The record packs and unpacks byte-identically over a representative input grid, and the layout matches GPU-024's confirmed declaration | GPU-024 | `./scripts/test.ps1 -Configuration Release`; `InstanceRecordTests.cs` |
| GPU-026 | Author the buffer-sizing and overflow arithmetic as a pure helper: fixed capacity of 65,536 instances, active count, byte count, and the overflow decision that draws the first 65,536 and emits one `warn` line rather than growing. Test the overflow branch, which is otherwise unreachable in practice. | `src/Hukbo.Client/Rendering/InstanceBufferSizing.cs` (new), `tests/Hukbo.Client.Tests/InstanceBufferSizingTests.cs` (new) | Capacity, count, byte, and overflow arithmetic all pass, including the overflow branch | GPU-024 | `./scripts/test.ps1 -Configuration Release`; `InstanceBufferSizingTests.cs` |
| GPU-027 | Build the GPU resource owner: one `IDisposable` type owning the static quad vertex buffer, the static index buffer, the dynamic instance buffer, and the compiled `Effect`, with exactly one `CreateResources` and one `ReleaseResources` so recreation is a single call and cannot be partially performed. Subscribe to `GraphicsDeviceManager.DeviceReset` and `DeviceCreated`. Check `DynamicVertexBuffer.IsContentLost` before each upload. **No device-reset or resize recreation path exists anywhere in this client today**, so none of this can be assumed to work by analogy with existing code. | `src/Hukbo.Client/Rendering/InstancedResources.cs` (new) | The owner creates, releases, and recreates all four resources through one call each, and a resize during a battle does not corrupt the frame | GPU-025, GPU-026 | Manual smoke rows only. No automated test in this repository can construct a `GraphicsDevice` |
| GPU-028 | Author the `.fx` vertex and pixel shader targeting `vs_3_0`/`ps_3_0`, with `SV_POSITION` and `SV_TARGET` defined to `POSITION` and `COLOR` for the GL path and no reliance on `SV_InstanceID`. Add the single `Content.mgcb` entry. Shader compilation is offline through the already-pinned `dotnet-mgcb`; no new package is added. | `src/Hukbo.Client/Content/ArenaInstanced.fx` (new), `src/Hukbo.Client/Content/Content.mgcb` | The shader compiles under `./scripts/build` and the content pipeline produces the compiled effect | GPU-024 | `./scripts/verify.ps1` build stage. **Correct rendering is not proven by compilation** |
| GPU-029 | **Amend, do not delete, the content-pipeline hygiene pin.** `tests/Hukbo.Client.Tests/SourceHygieneTests.cs` lines 253 to 271 assert that every `Content.mgcb` entry is one of six pinned `.spritefont` files. Adding an `.fx` fails both assertions. Amend the assertion so entries are either `.spritefont` or the one named `.fx` file, preserving the guarantee that no texture or atlas can slip in. Record the narrow justification in this plan: the "zero shaders" pin expressed a scope decision about authored *art content*, and a coordinate-transform-and-colour-passthrough shader is not authored art. This is a reviewed decision with its own task and must never be treated as a mechanical edit. | `tests/Hukbo.Client.Tests/SourceHygieneTests.cs` | The pin admits exactly one named `.fx` and nothing else, and the justification is written down | GPU-028 | `./scripts/test.ps1 -Configuration Release`; `SourceHygieneTests.cs` |
| GPU-030 | Build the capability probe and latch: one throwaway `DrawInstancedPrimitives` of a degenerate, zero-scale, fully transparent instance at `LoadContent` time, wrapped in a `try`/`catch` for `PlatformNotSupportedException`, latching into a `readonly bool` set once and never re-evaluated. Emit exactly one `inf`-level line on a new `render` channel with a stable dotted `const` on `LogEvents` — for example `render.backend.selected` — carrying the backend name, whether the probe threw, and the exception type name if it did. No prose in the event identifier. | `src/Hukbo.Client/Rendering/InstancingCapabilityProbe.cs` (new), `src/Hukbo.Diagnostics/LogEvents.cs`, `tests/Hukbo.Client.Tests/LogEventCatalogTests.cs` | The latch is set once per session, the log line appears exactly once in a `Debug` run's JSON Lines log, and the new event identifier is registered in the catalog | GPU-027 | `./scripts/test.ps1 -Configuration Release` for the catalog test; the log line itself from a hand-run `Debug` session |
| GPU-031 | Implement `InstancedRenderMetricsRecorder`, mirroring `SpriteBatchRenderMetricsRecorder` in reverse: Tier 1 fields unchanged in meaning, `BufferUploadBytes` honest and applicable, `Batches` applicable with its meaning shifted to instance batches, `TextureBinds` applicable, and `Submissions` reported as 0 and **not** applicable. Do not introduce `GraphicsDevice.Metrics` as a Tier 1 figure — its `PrimitiveCount` semantics differ between the two backends in exactly the way the Tier 1/Tier 2 split exists to prevent. | `src/Hukbo.Client/Rendering/RenderMetrics.cs`, `tests/Hukbo.Client.Tests/RenderMetricsTests.cs` | The recorder reports each Tier 2 metric with a truthful `*Applicable` flag, and no Tier 1 field changes meaning | GPU-026 | `./scripts/test.ps1 -Configuration Release`; `RenderMetricsTests.cs` |
| GPU-032 | Build the backend selection seam and the instance stream builder. The stream is built in exactly the order the current backend submits, with `AgentView` iteration ascending by `EntityId`. The build must complete within a single frame's read of `BattleSimulation.Agents`, because `_agentViews` is rewritten in place by `UpdateViews` each tick and is not double-buffered the way `LastEvents` is. Instance selection, culling, detail tier, and colour resolution all stay in pure helpers that both backends consume. | `src/Hukbo.Client/Rendering/ArenaRenderBackend.cs` (new), `src/Hukbo.Client/ArenaGame.Rendering.cs`, `tests/Hukbo.Client.Tests/InstanceStreamOrderTests.cs` (new) | The instanced path produces exactly the quad counts `PawnQuadCountTests` pins at 17, 19, 20, and 40, in the same order as the current backend's submission | GPU-025, GPU-030, GPU-031 | `./scripts/test.ps1 -Configuration Release`; `PawnQuadCountTests.cs`, `InstanceStreamOrderTests.cs` |
| GPU-033 | Implement upload and draw: one `SetDataOptions.Discard` write of the whole active range once per frame, from one instance array allocated once at fixed capacity and never reallocated. **Do not attempt a sub-range append strategy** — `SetDataOptions.NoOverwrite` is identical to `SetDataOptions.None` in MonoGame 3.8.5 on DesktopGL, so the mechanism that would make it worthwhile does not exist. `DrawInstancedPrimitives` requires an index buffer to be set; calling it without one throws. Reproduce the arena's current render state exactly: `BlendState.AlphaBlend` and the same scissor-enabled `RasterizerState` and scissor rectangle. | `src/Hukbo.Client/Rendering/InstancedArenaRenderer.cs` (new) | One upload and one draw per frame, with the arena scissor rectangle honoured so nothing draws over the user interface | GPU-032 | Manual smoke rows only. The upload call, the draw call, and the shader are the three genuinely untestable things in this design |
| GPU-034 | Implement fallback behaviour. If the capability probe throws, if resource creation throws, or if a device reset cannot recreate resources, the backend latches off permanently and the `SpriteBatch` backend serves the rest of the session from the next frame, with one `err`-level diagnostic line naming the failure. A renderer that cannot recreate its buffers must degrade, not crash. Emit a `dbg` line on every resource recreation. | `src/Hukbo.Client/Rendering/InstancedResources.cs`, `src/Hukbo.Client/Rendering/ArenaRenderBackend.cs`, `src/Hukbo.Diagnostics/LogEvents.cs` | Each of the three failure paths latches off and hands over without a crash, and each emits its named log line | GPU-033 | `./scripts/test.ps1 -Configuration Release` for the log-event catalog; the failure paths themselves are manual smoke rows |
| GPU-035 | Add the `"instanced"` backend constant and report whichever backend the latch selected on `RenderProbeFingerprint.Backend`. A report from a machine where the probe threw records `"spritebatch-1x1"`, which is the truthful answer for that machine. | `tools/Hukbo.Tools.RenderProbe/Program.cs`, `src/Hukbo.Client/Rendering/RenderProbeReport.cs`, `tests/Hukbo.Client.Tests/RenderProbeReportTests.cs` | The fingerprint names the backend that actually served the run | GPU-032 | `./scripts/test.ps1 -Configuration Release`; a hand-run probe on a machine of each kind if one is available |
| GPU-036 | Mitigate the C#/HLSL drift hazard, all three mitigations required rather than optional. Minimise shader arithmetic; declare every duplicated constant and convention once in C# with a doc comment naming the HLSL line that mirrors it and the reciprocal comment in the HLSL; and add a source-hygiene test asserting the shader source contains the expected mirrored comment markers. The third mitigation is a weak guard and must be described as one — it catches deletion, not divergence. | `src/Hukbo.Client/Content/ArenaInstanced.fx`, `src/Hukbo.Client/Rendering/InstanceRecord.cs`, `tests/Hukbo.Client.Tests/SourceHygieneTests.cs` | The mirrored markers exist in both directions and the hygiene test fails if either is deleted | GPU-029, GPU-033 | `./scripts/test.ps1 -Configuration Release`; `SourceHygieneTests.cs` |
| GPU-037 | Add the Phase 3 interactive smoke rows to `docs/development/testing.md`, all left `PENDING`: instanced backend renders a battle identically to the `SpriteBatch` backend at all three camera stations; forced fallback with the probe latched off renders correctly; window resize during battle; alt-tab during battle; display-mode change during battle. | `docs/development/testing.md` | The five rows exist in the standard five-column format with `Status` `PENDING` and an empty `Actual` column | GPU-034 | Reviewer reading the rows. **No agent may flip any of them** |
| GPU-038 | Hand-run the Phase 3 measurement at 200, 500, and 1,000 units on both backends, commit both JSON artifacts, and record the comparison in `docs/development/testing.md`. State whether the 8.0 millisecond budget at 1,000 units default-fit is now met. | `docs/development/render-baselines/`, `docs/development/testing.md` | Both backends measured at the same stations with the comparison recorded, and the budget verdict written down | GPU-036, GPU-037 | The hand-run itself. **Not delegable** |

---

## 4. The go/no-go trigger for Phase 3

Restated verbatim from the design's section 6.2.

> **Phase 3 is authorized if and only if both of the following hold on the Phase
> 2 re-measurement:**
>
> 1. **The budget is missed.** The 1,000-unit default-fit `Draw` p95 exceeds
>    **8.0 ms**.
> 2. **The overrun is in the submission path.** The Tier 1
>    `submitMicroseconds` p95 at that same station is at least **50 percent** of
>    the total `Draw` p95 at that station.

The measurement conditions are fixed: 1,000 units (500 per team), seed 1,
Release configuration, 120 frames per station, vertical retrace disabled, at the
default-fit camera station.

The 8.0 millisecond figure is derived rather than chosen. A 60 Hz frame is 16.67
milliseconds and must contain both `Update` and `Draw`. The recorded simulation
cost at 1,000 agents is 6.2364 milliseconds at p95 per tick
(`docs/development/testing.md` line 262). A `Draw` p95 of 8.0 milliseconds
alongside that leaves 2.43 milliseconds of headroom inside the frame for
presentation and operating-system scheduling.

Clause 2 is the load-bearing one. Clause 1 alone would authorize an instanced
backend for an overrun that instancing cannot address, which is precisely the
mistake this whole phasing exists to prevent. Instancing changes how quads are
submitted. If submission is not the majority of the frame, rewriting submission
cannot bring the frame under budget no matter how well it is done.

### 4.1 Clause 2 cannot be evaluated until the Submit span is disaggregated

This is a hard prerequisite and it follows directly from section 2.4. Today's
`submitMicroseconds` span wraps `DrawArenaLayer`, which both constructs the real
per-pawn geometry and issues the `SpriteBatch.Draw` calls. A `submitMicroseconds`
figure taken from the current build therefore answers a different question from
the one clause 2 asks. It reports "geometry construction plus submission" where
clause 2 needs "submission".

**GPU-004 is therefore a prerequisite for evaluating the trigger at all.** Any
attempt to evaluate clause 2 against a pre-GPU-004 figure is invalid, and would
almost certainly read as a false GO, because the conflated span is large at
every 500-unit station. The trigger may only be evaluated against a measurement
taken after GPU-004 lands.

### 4.2 What happens on a no-go

If either clause fails, **Phase 3 is not built**, and GPU-024 through GPU-038
are never started.

- The `SpriteBatch` backend remains the sole backend. No capability probe ships,
  no `.fx` file is authored, no content-pipeline entry is added, and
  `tests/Hukbo.Client.Tests/SourceHygieneTests.cs` lines 253 to 271 stay exactly
  as they are with the six pinned spritefonts untouched.
- If clause 1 failed — the budget is met — the work is simply done. The
  1,000-unit target is reached with the existing backend and that is the good
  outcome.
- If clause 1 held but clause 2 failed — the budget is missed, but not in
  submission — the overrun is characterised and routed to whichever span Phase 1
  named. If that span turns out to be the simulation's influence on the frame
  rather than the renderer's, it belongs to the neighbouring concern in
  `docs/plans/2026-07-28-collision-resolution-scaling-design.md`. **This plan
  does not authorize that work either.** It hands over a measurement and stops.
- The design document's section 8 is archived with the recorded measurement that
  closed it, so the next person to propose instancing finds the numbers rather
  than repeating the investigation.

---

## 5. Exit criteria

### 5.1 Phase 1

Phase 1 is complete when all four of the following are recorded, and not before.

1. A matrix run at 200, 500, and 1,000 units, seed 1, 120 frames per station,
   Release configuration, vertical retrace disabled, with a fingerprint stating
   the retrace setting.
2. Every millisecond of `Draw` attributable to a named span, with the largest
   remaining unattributed span under ten percent of the frame at 1,000 units.
3. The baseline artifacts in a tracked, committed location, with
   `docs/development/testing.md` citing a path a fresh clone can open.
4. A stated probe-only duplication factor, derived from the recorded
   `PawnGeometry.Create` invocation count rather than assumed, so that Phase 2's
   before-and-after comparison compares like with like.

Additionally, the `submitMicroseconds` figure must have been disaggregated by
GPU-004 into separate geometry and submission components. Without that, the
Phase 2 re-measurement cannot answer clause 2 of the trigger, and Phase 1 has
not delivered its purpose.

`./scripts/verify.ps1` must pass. The seed-1 headless determinism workload must
report state hash `A080E28DA7C79C20`, event hash `2B6FB3A9A9C1960D`,
`measuredTicks` 1677, and `coreAllocatedBytes` 118896, unchanged.

### 5.2 Phase 2

1. R1, R2, and R3 implemented, each with its equivalence test passing.
2. The appearance cache's full declaration recorded in this document, with its
   cold-cache equivalence test and its size-bound test passing.
3. The client per-team cap at 500, with `DefaultUnitsPerTeam` still at 250.
4. `./scripts/verify.ps1` passing with the seed-1 headless figures from 5.1
   unmoved. Phase 2 touches only `Hukbo.Client`, so any movement in those
   figures is a defect, not a new baseline.
5. A re-measurement using the Phase 1 probe at 200, 500, and 1,000 units, with
   the trigger verdict written down. **This re-measurement is the input to
   section 4.**

### 5.3 Phase 3 (not authorized)

Stated for completeness only. Phase 3's exit criteria are the five interactive
smoke rows from GPU-037 performed and recorded by a human, both backends
measured at all three stations, and the 8.0 millisecond budget at 1,000 units
either met or honestly reported as missed. None of this may be started before
the trigger fires.

---

## 6. Dependency and parallelism

### 6.1 The serial chain through `ArenaGame.Rendering.cs`

`src/Hukbo.Client/ArenaGame.Rendering.cs` is the single busiest file in this
plan. **Every task that touches it must run serially, one at a time, in the
order listed.** Two agents editing this file in parallel is a merge conflict
created on purpose.

The tasks that touch `ArenaGame.Rendering.cs` are:

**GPU-003, GPU-004, GPU-005, GPU-014, GPU-016, GPU-018, GPU-020**, and — only if
Phase 3 is ever authorized — **GPU-032**.

### 6.2 What can run in parallel

- **Phase 1.** GPU-008 is read-only and independent of everything, so it may run
  at any time. GPU-006 and GPU-007 touch the probe tool and `ArenaGame.cs`, both
  disjoint from the serial chain, so either may run alongside a serial-chain
  task once GPU-002 has landed. GPU-006 and GPU-007 both touch
  `tools/Hukbo.Tools.RenderProbe/Program.cs`, so they are serial with respect to
  each other.
- **Phase 2, the pure-helper tasks.** GPU-013, GPU-015, GPU-017, GPU-019, and
  GPU-022 touch entirely disjoint file sets and may all run in parallel. This is
  the largest genuine parallelism opportunity in the plan and is the reason each
  redundancy was split into a helper task and an adoption task.
- **Phase 2, the adoption tasks.** GPU-014, GPU-016, GPU-018, and GPU-020 all
  funnel through `ArenaGame.Rendering.cs` and are strictly serial.
- **GPU-021** touches `ArenaGame.cs` rather than `ArenaGame.Rendering.cs`, so it
  is disjoint from the adoption chain and may run alongside it — but note that
  GPU-006 also touches `ArenaGame.cs`, so those two are serial with respect to
  each other.
- **The second serial chain, through `docs/development/testing.md`.** That file
  is a shared seam exactly as `ArenaGame.Rendering.cs` is, and it is easy to
  miss because it holds prose rather than code. **GPU-009, GPU-011, GPU-012,
  GPU-023, GPU-037, and GPU-038 all write to it and must run one at a time, in
  that order.** Two agents appending measurement tables to the same document in
  parallel produce a conflict that is harder to resolve than a code conflict,
  because neither side is obviously wrong. The same applies to the
  `docs/development/render-baselines/` directory created by GPU-009 and written
  again by GPU-012, GPU-023, and GPU-038.

### 6.3 What is never delegated

GPU-012, GPU-023, GPU-024, and GPU-038 are hand-run measurements on a real
graphics device. `./scripts/verify.ps1` runs once, after integration, and its
real pasted output is the evidence. No sub-agent report substitutes for either,
and no agent may flip a manual smoke row to `PASS`.

---

## 7. Verification — what proves what

The three verification instruments in this repository prove different things,
and the boundaries between them are load-bearing rather than pedantic.

### 7.1 What `./scripts/verify.ps1` proves

The canonical gate runs prerequisites and a locked restore, verification, a
Release build, Core and GPU-independent Client tests, and the 200-agent /
10,000-tick / seed-1 headless determinism workload.

It proves that the code compiles under `TreatWarningsAsErrors`, that every pure
helper behaves as its tests assert, and that the simulation is unchanged.

**It never opens a window and never constructs a `GraphicsDevice`, so it cannot
execute either rendering path.** Client tests must never construct `ArenaGame`,
a `GraphicsDevice`, a `SpriteBatch`, or a window, and there are currently zero
such occurrences anywhere under `tests/` — a property to preserve rather than an
accident. The gate therefore proves nothing at all about what appears on screen,
under either backend. A green gate is not evidence that the renderer renders.

### 7.2 What the render probe proves

The probe opens a real window on a real graphics device and records frame time
and the Tier 1 and Tier 2 metric spans per camera station. It proves what the
frame cost, where inside `Draw` the time went, how many quads were submitted,
and how much was allocated.

It does not prove that the picture was correct. The probe never compares pixels;
it counts and times. A renderer that drew everything in the wrong place would
produce an entirely healthy probe report.

The probe's own overhead is real and must stay disclosed. Under the probe,
`RecordArenaRenderMetrics` re-walks the agent list and recomputes appearance,
bounds, and layout for every visible pawn — a second full pass over work the
draw path does again moments later. GPU-005 exists so that this overhead is
reported separately rather than silently folded into a figure a budget is
written against.

### 7.3 What only the manual checklist can prove

Only a human at an interactive desktop who has actually looked at the screen can
establish that the picture is right. `docs/development/testing.md`'s smoke rows
are the record of that, and **only a human may flip a row to `PASS`**.
Compilation, unit tests, and a window-opening probe run do not let anyone flip a
row. A row that has not been performed stays `PENDING`; a row that cannot be
performed is reported `BLOCKED` honestly.

For Phases 1 and 2, the things that only the checklist can prove are: that no
pixel changed, that the composition panel still fits the window at 500 per team,
and that a 1,000-unit battle is watchable rather than merely measurable.

For Phase 3, the untestable surface is exactly three things — the `SetData`
upload call, the `DrawInstancedPrimitives` call, and the shader itself.
Everything else is pushed above the pure-helper boundary deliberately.

### 7.4 New `testing.md` rows this work adds, all left `PENDING`

These rows are added by the tasks named and are left `PENDING` until a human
performs them. The table uses the file's existing five-column format:
`| # | Step | Expected | Actual | Status |`.

**Added by GPU-012 (Phase 1):**

| # | Step | Expected | Status |
| --- | --- | --- | --- |
| GR-1 | Launch the game normally with `./scripts/run.ps1` after the probe's retrace override lands | The window still runs with vertical retrace enabled; no tearing appears and frame pacing is unchanged | PENDING |

**Added by GPU-022 and GPU-023 (Phase 2):**

| # | Step | Expected | Status |
| --- | --- | --- | --- |
| GR-2 | Open the army composition panel and raise a team to 500 | The stepper reaches 500, refuses to go higher, and every row and both buttons stay fully on screen | PENDING |
| GR-3 | Start a 1,000-unit battle (500 per team) and watch one full engagement | The battle renders and remains watchable; pawns, shields, swings, and hit pulses all read correctly at all three camera stations | PENDING |
| GR-4 | Compare a seed-1 200-unit battle before and after Phase 2 at the same tick and camera station | No visible difference. Phase 2 is pure removal of duplicated work; any visible difference is a defect | PENDING |
| GR-5 | Watch hit pulses in a dense 1,000-unit melee | Pulse strength and timing read exactly as before the per-frame lookup replaced the per-pawn scan | PENDING |

**Added by GPU-037, only if Phase 3 is ever authorized:**

| # | Step | Expected | Status |
| --- | --- | --- | --- |
| GR-6 | Run a battle on the instanced backend and on the `SpriteBatch` backend at all three camera stations | The two are visually identical | PENDING |
| GR-7 | Force the capability probe to latch off and run a battle | The `SpriteBatch` backend serves the session and renders correctly | PENDING |
| GR-8 | Resize the window during a battle | Resources are recreated; the frame stays correct; the `Debug` log records the recreation | PENDING |
| GR-9 | Alt-tab away and back during a battle | The frame stays correct on return | PENDING |
| GR-10 | Change display mode during a battle | Resources are recreated or the backend latches off cleanly; no crash | PENDING |

---

## 8. Rollback

### 8.1 Phase 1

Phase 1 adds instrumentation and moves documentation. It changes no rendering
behaviour, so rollback is a plain `git revert` of the Phase 1 commits with no
migration and no state to unwind. The one item needing care is GPU-009: the
baseline artifacts are moved into tracked storage, so a revert must not leave
`docs/development/testing.md` citing a path that has been reverted away. Revert
GPU-009 and GPU-011 together, or neither.

The probe's vertical-retrace override from GPU-006 is confined to the probe and
never reaches the shipped client, so reverting it cannot affect a normal run.

### 8.2 Phase 2

Each of R1, R2, and R3 is an independent, separately revertible change, which is
the reason the plan splits each into a helper task and an adoption task. If the
appearance cache from GPU-017 and GPU-018 proves wrong, reverting the adoption
task alone restores the direct call to `PawnAppearanceFactory.Create` and the
helper becomes dead code that a follow-up removes. The same holds for the
conservative pre-cull and the pulse lookup.

The unit-cap change in GPU-022 is a single constant and reverts on its own. A
persisted settings file written while the cap was 500 will hold a value above
the reverted maximum; `ArmyCompositionStepper`'s existing clamp handles that,
and GPU-022's tests must cover the case so the revert path is known to work
rather than hoped to.

Because Phase 2 is expected to produce no visual change at all, any visible
difference after Phase 2 is a rollback trigger in itself.

### 8.3 Phase 3

Rollback for Phase 3 is structurally easier than for Phase 2 and structurally
harder to complete. Easier, because the `SpriteBatch` backend is retained
permanently and never removed, so disabling the instanced backend is a
one-boolean change that returns the client to exactly the code path every
recorded measurement in this repository was taken against. Harder, because
GPU-029 amends a pinned hygiene test and GPU-028 adds a content-pipeline entry,
and reverting those two must happen together or the build breaks.

The order for a full Phase 3 rollback is: latch the backend off, then revert the
renderer, then revert the content-pipeline entry and the hygiene-test amendment
in one commit, then revert the shader file.

---

## 9. Determinism statement

**This plan makes no change to `Hukbo.Core`.** No new field, no new enum value,
no change to enum ordering, no change to roster order, weights, or the hash
mixer. Every task in every phase touches `Hukbo.Client`, `Hukbo.Diagnostics`,
`tools/`, `tests/`, or `docs/` and nothing else.

The seed-1 headless figures must be byte-identical after every phase:

- state hash `A080E28DA7C79C20`
- event hash `2B6FB3A9A9C1960D`
- `measuredTicks` 1677
- `coreAllocatedBytes` 118896

Any movement in any of those four is a defect to be fixed, never a baseline to
be updated. Rendering is presentation-only and this should be automatic, but
"should be automatic" is how determinism breaks, so it is asserted at every
phase exit rather than assumed.

The unit-cap change in GPU-022 raises a client user-interface constant only. It
is bounded by `Scenario.MaximumAgentsPerFaction`, which is 10,000 and is not
changed. No random stream is consumed by any task in this plan;
`PawnAppearanceFactory` performs deterministic hash mixes of fixed inputs, which
is not a stream and produces the same value on every call — that property is
exactly what makes the appearance cache valid.

### 9.1 Named out of scope: any appearance feature requiring new Core state

The warrior-appearance research at
`docs/research/improve-visuals/warrior-appearance-historical-research.md` lines
239 to 267 and 883 to 884 proposes an **earned red head wrap**, gated on a
veteran status or kill-count marker in agent metadata. No such authoritative
state exists in `Hukbo.Core` today, and adding one would move the state hash.

**That feature is out of scope for this plan, specifically and by name.** So is
any other appearance feature that requires new authoritative simulation state.
If the head wrap is wanted, it is a `Hukbo.Core` change with its own design, its
own new preset version, and its own new golden expectations. It does not ride
along on a rendering change.

### 9.2 Historical accuracy

No task in Phases 1 or 2 adds or alters a cultural identification, a weapon
label, or an evidence tier, so the historical-accuracy policy imposes no new
obligation on the authorized work. Should Phase 3 ever be authorized and should
the per-instance material palette described in the design's section 10.2 follow
it, the material tones derived from the Boxer Codex are **Provisional
reconstruction** — the Codex guides silhouette and colour rather than dye
chemistry — and any gameplay-facing tuning value derived from them is marked
provisional in code comments and tests rather than presented as a historical
measurement. That palette is not part of this plan.
