# GPU-Instanced Arena Rendering — Design

**Status: design only. This document does not authorize implementation.**
No source file under `src/`, `tests/`, `tools/`, or `scripts/` may be changed on
the strength of this document alone. A separate plan document turns the phases
below into an ordered, checkable task list, and each phase carries its own
explicit entry condition. Phase 3 in particular is gated behind a numeric
measurement result that does not exist yet, and building it before that result
is recorded would be building against a guess.

Date: 2026-07-28. Branch: `gpu-render`.

---

## 1. Summary

The obvious reason to add GPU instancing to this renderer is wrong, and the
measurement that proves it is already recorded in the repository.

`docs/development/testing.md` lines 116 and 126 give the maximum-zoom camera
station at two army sizes. Both rows draw **exactly the same 1,028 quads** and
**exactly the same 2,056 triangles**, because at maximum zoom the camera frames
a handful of pawns and the frustum cull discards everything else. The draw work
is identical. The frame cost is not:

| Army size | Station | Quads | Frame p50 | Frame p95 |
| --- | --- | ---: | ---: | ---: |
| 200 units | maximum zoom | 1 028 | 0.22 ms | 0.26 ms |
| 500 units | maximum zoom | 1 028 | 5.33 ms | 5.50 ms |

Two and a half times the army, no change at all in the geometry submitted,
and roughly twenty-four times the frame cost. Whatever is expensive here scales
with the **total agent count**, not with the number of quads that reach the
graphics device. GPU instancing changes how quads reach the graphics device. It
therefore cannot, on this evidence, be assumed to touch the thing that is
actually costing the frame — and section 6.2 turns that into a numeric condition
rather than an assumption.

Reading the underlying artifact rather than the summary table sharpens this, but
it also complicates it, and the complication matters more than the headline. The
probe reports the arena render path in two instrumented windows — geometry build
and arena submission. How much of the frame those two windows account for
depends entirely on which camera station is being measured:

| Army size | Station | Frame p50 | Geometry build p50 | Arena submit p50 | Attributed share |
| --- | --- | ---: | ---: | ---: | ---: |
| 200 units | minimum zoom | 2 299.5 us | 546.3 us | 1 354.5 us | 83% |
| 200 units | default fit | 765.9 us | 130.2 us | 466.9 us | 78% |
| 200 units | maximum zoom | 215.8 us | 42.4 us | 75.8 us | 55% |
| 500 units | minimum zoom | 5 273.4 us | 1 372.7 us | 3 526.6 us | 93% |
| 500 units | default fit | 5 297.7 us | 207.1 us | 4 853.2 us | 96% |
| 500 units | maximum zoom | 5 334.8 us | 90.3 us | 104.9 us | 3.7% |

The maximum-zoom row at 500 units is the striking one: 5.33 milliseconds of
frame with only 195 microseconds of instrumented render path inside it. But it
is the outlier, not the rule, and it must not be generalised into a claim about
the renderer. **At the default-fit station — the camera the game actually ships
with, and the station the go/no-go budget in section 6 is written against — the
instrumented render path accounts for 96 percent of the frame at 500 units, and
`submitMicroseconds` alone is 4,853 of those 5,298 microseconds.**

So the correct statement of the problem is not "the instrumented render path is
a negligible slice of the frame". At the station that matters, it is very nearly
the whole frame. The problem is that **the span which dominates that frame is of
unknown composition.** `submitMicroseconds` times `DrawArenaLayer`, and
`DrawArenaLayer` both builds the real per-pawn geometry and issues every
`SpriteBatch.Draw` call. Those 4,853 microseconds are some unmeasured mixture of
per-agent CPU layout work, which Phase 2 addresses, and GPU submission, which
Phase 3 addresses. No instrument in this repository can currently say which.
Section 4.2 sets out that conflation in full, and it is the reason Phase 3 is
gated rather than scheduled.

One further feature of the data deserves recording, because it constrains how
far any of these percentages can be trusted. All three 500-unit stations land
within 0.06 milliseconds of one another — 5.27, 5.30, and 5.33 — despite drawing
9,326 quads at two of them and 1,028 at the third. The 200-unit stations, by
contrast, span 0.22 to 2.30 milliseconds. A frame cost that stays flat across a
nine-fold change in quad count is not being set by the quads. That is the
signature of a per-frame block, and vertical retrace is the leading candidate;
section 4.3 requires retrace to be disabled before any of these figures are
re-taken. Until that happens, the shares in the table above describe where a
blocked frame is being charged, not necessarily where work is being done.

That is the finding this design is built around, and it produces the shape of
the plan: **measure honestly first, remove the per-agent CPU work that runs
before the cull second, and build the instanced backend only if a re-measurement
still misses the target.** The instanced backend is designed in full below, in
section 8, so that the decision to build it is a decision about a known thing
rather than an open-ended one. It is nonetheless gated, and section 6 states the
numeric condition that opens the gate.

---

## 2. What the measurement actually says

### 2.1 The draw-call argument is close to worthless here

The usual case for instancing is draw-call reduction. That case does not apply,
and the numbers say so plainly.

`SpriteBatch` on MonoGame 3.8.5 batches up to `SpriteBatcher.MaxBatchSize`
sprites per flush, which is 5,461 — `short.MaxValue / 6`, the largest quad count
addressable with a 16-bit index buffer. The recorded worst case in this
repository is 9,326 quads at the 500-unit minimum-zoom station, which is two
flushes. The Tier 2 diagnostics confirm the batching directly: `batches` is 1
and `textureBinds` is 1 in every single cell of both recorded runs
(`testing.md` lines 128 to 131). One `Begin`/`End` pair, one shared 1x1 pixel
texture, for the whole arena layer.

So the honest statement of what instancing buys at the GPU-submission boundary
is: it collapses approximately two GPU draw operations into one. That is not a
performance argument. Anyone who proposes this change on draw-call grounds has
not read the measurement.

The real prizes, if Phase 3 is ever built, are different and should be named
plainly:

1. **CPU vertex assembly.** Every `SpriteBatch.Draw` call writes four
   `VertexPositionColorTexture` structs into a managed array, and
   `SpriteBatcher` then hands that array to `DrawUserIndexedPrimitives`. At
   9,326 quads that is 37,304 vertex writes per frame on the CPU. An instanced
   backend writes one 28-byte instance record per quad instead of four vertices,
   and the GPU expands it. This is a genuine reduction in CPU work per drawn
   quad — but note carefully that it scales with *drawn* quads, and section 1
   established that drawn quads are not what is hurting.
2. **Per-unit expressiveness.** An instance record can carry per-pawn data —
   a garment tone, a material index, a damage-state value — that the shader
   consumes without the CPU resolving it into a colour first. This is a
   capability argument rather than a performance one, and it is the argument
   that connects to the warrior-appearance research. It is also the argument
   most likely to justify Phase 3 on its own merits after Phase 2 lands.

Neither prize is a draw-call prize. The design must not be sold as one.

### 2.2 The target is 1,000 units, not 2,000 and not 5,000

The target for this work is **1,000 units in total, 500 per team.** That is a
deliberate ceiling and not a way-station toward a larger one.

The simulation's own recorded per-tick costs (`testing.md` lines 260 to 263)
explain why:

| Agents | Tick p50 | Tick p95 |
| ---: | ---: | ---: |
| 200 | 0.0887 ms | 1.6860 ms |
| 500 | 0.2391 ms | 1.9310 ms |
| 1 000 | 0.8481 ms | 6.2364 ms |
| 2 000 | 17.3454 ms | 51.5116 ms |

Between 1,000 and 2,000 agents the p50 tick cost rises by a factor of roughly
twenty and the p95 by a factor of roughly eight. At 2,000 agents a single
simulation tick at p95 costs 51.5 milliseconds, which is over three whole frames
at 60 Hz before the renderer has drawn anything at all. A renderer that could
draw 2,000 units in zero time would still not produce a playable 2,000-unit
battle, because the simulation cannot supply the ticks.

`docs/development/TICK-STAGE-PROFILE.md` line 90 attributes the bulk of that to
one stage: `ResolveCollisions` is 63.11 percent of `AdvanceOneTick` at 200
agents, 70.11 percent at 1,000, and 74.77 percent at 2,000. There is a separate
design document at `docs/plans/2026-07-28-collision-resolution-scaling-design.md`
that addresses exactly this. **That work is a neighbouring concern and this
design does not absorb it, does not depend on it, and does not authorize it.**
It is named here only so that a reader who asks "why stop at 1,000?" gets the
real answer: because past 1,000 the binding constraint moves out of the renderer
entirely, and fixing the renderer past that point fixes nothing a spectator can
see.

At 1,000 agents the simulation costs 0.8481 ms at p50 and 6.2364 ms at p95 per
tick. Those figures are what the render budget in section 6 has to co-exist
with, and they are the reason the budget is as tight as it is.

### 2.3 The cull is pose-blind on purpose, and that must survive

`PawnRenderer.GetBounds` (`src/Hukbo.Client/Rendering/PawnRenderer.cs` lines 56
to 87) computes deliberately pose-blind bounds, and `ArenaGame.Rendering.cs`
lines 529 to 532 repeat the reason at the call site: a pose-aware cull would make
the set of drawn pawns a function of presentation animation phase, so the same
simulation tick would render a different draw list depending on where each swing
clock happened to sit. The comment calls this draw-list determinism and says it
is the whole reason.

Every optimisation in this document preserves that property. Where section 5
proposes removing a redundant geometry computation, it removes the *redundancy*,
not the pose-blindness: the fix returns the pose-blind bounds alongside the
posed layout from a single call, so the cull still tests pose-blind bounds and
still admits exactly the same set of pawns it admits today.

### 2.4 The LOD tier is uniform across the frame

`PawnGeometry` derives the detail tier from camera zoom alone
(`src/Hukbo.Client/Rendering/PawnGeometry.cs` lines 300 to 302, thresholds
`MediumDetailScale = 0.95f` and `HighDetailScale = 1.80f`), and `DetailTierGate`
mirrors the identical thresholds at lines 23 and 24 so a catalog entry
classifies the same way. Because the input is the camera and nothing else, every
pawn in a given frame is at the same tier.

This is a genuine simplification for an instanced backend and should be
exploited rather than designed around: one tier per frame means one uniform set
per frame, and it means the instance stream never has to be partitioned by tier
or sorted by shader variant. It also means an instanced backend does not need
per-instance tier data in its instance record.

### 2.5 There is per-agent CPU work that runs before the cull

Section 1's unresolved `submitMicroseconds` span is the headline, but three
specific redundancies are visible in source and are cheap to state precisely.
Each is a candidate occupant of that span, which is why Phase 2 runs before the
Phase 3 decision rather than after it. They are
the substance of Phase 2 and they are described in section 5. Two of them run
for *every* agent, alive or not, before any cull decision is reached, which is
exactly the scaling signature section 1 measured.

---

## 3. Scope, non-goals, and what this design does not authorize

### 3.1 In scope

- Making the existing render measurement honest enough to base a decision on
  (Phase 1).
- Removing per-agent CPU work in the client render path that runs before or
  redundantly around the frustum cull (Phase 2).
- A complete design for a GPU-instanced arena rendering backend, coexisting
  with the current `SpriteBatch` backend behind a runtime capability probe
  (Phase 3), gated on section 6's numeric trigger.
- Raising the client's per-team unit cap from 250 to 500 so that a 1,000-unit
  total is reachable from the user interface at all.

### 3.2 Out of scope, explicitly

- **Any change to `Hukbo.Core`.** This is a presentation change. The
  simulation's state hash and event hash must be byte-identical before and
  after every phase. `SIMULATION-GAME-STANDARDS.md` sections at lines 74 to 84
  and 200 to 205 place rendering firmly on the presentation side, and the
  reviewer checklist at line 364 requires before-and-after hashes to be
  identical. Section 10 records the specific appearance feature that would
  require a Core change and rules it out.
- **Collision-resolution scaling.** Named in section 2.2, addressed elsewhere,
  not touched here.
- **Any minimum-specification contract.** Section 10 explains why the
  capability probe plus a retained fallback is the answer instead, and states
  plainly that this design deliberately does not create one.
- **Textures, texture atlases, or sprite assets.** The renderer is procedural
  and stays procedural. Phase 3 adds a shader; it does not add art.
- **Any new NuGet package.** See section 8.10.
- **Terrain, pathfinding, morale, projectile ammunition, persistence
  migrations, multiplayer, and mod APIs**, all of which remain gated elsewhere
  and none of which this touches.

### 3.3 What this design does not authorize

Writing this document does not authorize writing code. Each phase below states
its own entry condition. Phase 3's entry condition is a measurement that has not
been taken. If a reader reaches section 8 and starts implementing it, they have
skipped the only part of this document that carries real information.

---

## 4. Phase 1 — establish measurement truth

**Entry condition:** none. This is the first phase and everything else depends
on it.

**Deliverable:** a re-recorded render baseline at 200, 500, and 1,000 units in
which the frame time is attributable, is not contaminated by a blocking wait,
and is not contaminated by probe-only work. Nothing in Phase 2 or Phase 3 may be
justified against the current baseline once Phase 1 is complete.

Phase 1 changes measurement, not rendering. It is expected to produce no visual
change whatsoever.

### 4.1 The recorded baseline is real, but its stated artifacts are not where the document says they are

`testing.md` lines 109 and 110 cite two artifact files:
`artifacts/render-baseline-2026-07-28.json` and
`artifacts/render-baseline-500-2026-07-28.json`. **Neither file exists in the
main checkout, and neither exists in this worktree.** Both files were located in
a sibling worktree at
`.claude/worktrees/improve-visuals/artifacts/`, and `artifacts/` is matched by
`.gitignore` line 13 (`[Aa]rtifacts/`), so neither file has ever been committed
and neither is reachable from a fresh clone.

The contents were read and they corroborate the `testing.md` tables exactly —
the 500-unit maximum-zoom station really does record `frameMillisecondsP50`
5.3348, `quadsMaximum` 1028, `geometryBuildMicrosecondsP50` 90.3, and
`submitMicrosecondsP50` 104.9. So the numbers in `testing.md` are trustworthy as
numbers. What is not trustworthy is the citation: a document that points at a
gitignored file in an unrelated worktree is pointing at nothing, and the next
person to check will conclude the measurement was fabricated.

**Phase 1 task:** either commit the baseline artifacts to a tracked location, or
change `testing.md` to stop citing untracked paths and instead carry the figures
inline with a note that the raw artifacts are transient. The first option is
preferable and the tracked location should be `docs/development/` rather than
`artifacts/`, because `artifacts/` is ignored by design and should stay that
way.

### 4.2 The frame-time figure measures `Draw` only, and neither instrumented span means what its name says

`ArenaGame.Rendering.cs` line 30 takes the frame-start timestamp at the top of
`Draw`, and line 95 reads the elapsed time after `base.Draw(gameTime)`. The
simulation tick runs in `Update` and is therefore **not** inside the reported
`frameMilliseconds`. This is a good property and should be documented rather than
changed, because it means the render figure is a render figure.

Inside that window, only two spans are instrumented:

- `RecordArenaRenderMetrics` (lines 59 to 63), reported as
  `geometryBuildMicroseconds`.
- `DrawArenaLayer` (lines 66 to 81), reported as `submitMicroseconds`.

Everything else in `Draw` is unmeasured: `GraphicsDevice.Clear` at line 35,
`GetLayout` at line 46, `_camera.Fit` at line 47, `UpdateHoverSelection` at line
48, `DrawUiLayer` at lines 83 to 89, and `base.Draw` at line 91.

How large that unmeasured region is depends on the camera station, and section
1's table gives the sizes. At 500 units it is 96 percent of the frame at maximum
zoom but only about 4 percent at default fit and about 7 percent at minimum
zoom. The unmeasured region is therefore a real problem at one station and a
rounding error at the two that matter most. It still has to be closed, because a
probe that cannot account for its own frame cannot be used to authorize a
rewrite, but closing it is not by itself the finding.

The larger problem is inside the two spans that *are* instrumented, and it is
the reason Phase 3 is gated:

- `geometryBuildMicroseconds` does not time the geometry the renderer draws. It
  times `RecordArenaRenderMetrics`, which is the probe's own duplicate counting
  pass and never runs outside the probe. Section 4.4 covers this.
- `submitMicroseconds` does not time submission. It times `DrawArenaLayer`,
  which builds the real per-pawn geometry — every `PawnGeometry.Create` call the
  renderer actually draws from happens inside it — *and* issues every
  `SpriteBatch.Draw` call. One number, two very different kinds of work, with no
  way on disk today to separate them.

That second point is the load-bearing one. At the 500-unit default-fit station,
4,853 microseconds of a 5,298-microsecond frame sit inside a span of unknown
composition. Whether that 4.85 milliseconds is Phase 2 work (per-agent CPU
layout) or Phase 3 work (GPU submission) is undetermined by every measurement
this repository holds. It could plausibly be either, and the go/no-go trigger in
section 6.2 asks a question that cannot be answered until it is split.

**Phase 1 task (GPU-004):** disaggregate the `DrawArenaLayer` span into a
geometry-construction component and a submission component, reported as separate
Tier 1 spans. Section 6.2 names this a hard prerequisite of evaluating the
trigger, because against the conflated span clause 2 reads as satisfied
regardless of what the frame is actually spending its time on.

**Phase 1 task:** add Tier 1 timing spans covering the unmeasured region, at a
granularity coarse enough to stay allocation-free and cheap but fine enough to
name a culprit. The minimum useful split is four new spans — clear, layout and
hover selection, user-interface layer, and `base.Draw` — recorded through
`IRenderMetricsRecorder` in the same style as the existing two. These are Tier 1
in the sense of `RenderMetrics.cs` lines 10 to 22: they are renderer-invariant
and would mean the same thing under either backend.

Two candidate explanations are already visible in source and should be tested
directly rather than assumed:

- **`UpdateHoverSelection` is an unbounded linear scan.** `ArenaGame.cs` lines
  1305 to 1335 call `SelectAtPointer`, which calls
  `AgentSelection.SelectNearest(_simulation.Agents, ...)`, and that method
  (`src/Hukbo.Client/Presentation/AgentSelection.cs` lines 9 onward) walks every
  agent in the list. It runs every frame the pointer is inside the arena
  rectangle. `DrawUiLayer` then performs a second full-list operation at
  `ArenaGame.Rendering.cs` line 286, `_presentation.Selection.Resolve(
  _simulation.Agents)`.
- **`_simulation.Agents` is exposed as an interface.**
  `BattleSimulation.cs` line 107 declares
  `public IReadOnlyList<AgentView> Agents => _agents;`. Every `foreach` over
  that property in the client allocates a boxed enumerator and pays an
  interface-dispatched indexer call per element, and `AgentView`
  (`src/Hukbo.Core/Simulation/AgentView.cs` lines 19 to 31) is a fourteen-field
  record struct that is copied on each such call. The render path walks this
  list at least three times per frame — `DrawPawns`, `RecordPawnQuads` under the
  probe, and the two selection operations above.

Neither of these on its own explains the maximum-zoom remainder growing roughly
fifty-fold, from 97.6 to 5,139.6 microseconds, for a two-and-a-half-fold jump in
agents. That mismatch is precisely why Phase 1 has
to measure rather than reason. A third possibility must be tested and either
confirmed or eliminated: that the remainder is not CPU work at all but a
driver-side stall, described next.

### 4.3 Vertical retrace contaminates the frame figure and must be disabled during measurement

`ArenaGame.cs` line 237 sets `SynchronizeWithVerticalRetrace = true` and line
245 sets `IsFixedTimeStep = false`. Presentation happens in
`Game.EndDraw`, after `Draw` returns, so the swap itself is outside the measured
window. Driver back-pressure is not: when an OpenGL driver has buffered its
maximum number of in-flight frames, the next GL call blocks, and the first GL
call of the next frame is `GraphicsDevice.Clear` at `ArenaGame.Rendering.cs`
line 35 — **inside** the measured window.

This is a live suspect for the remainder, and it is strongly suggested by the
shape of the 500-unit data: minimum zoom 5.27 ms, default fit 5.30 ms, maximum
zoom 5.33 ms. Three stations with wildly different quad counts, 9,326 against
1,028, landing within 0.06 ms of one another is the signature of a floor imposed
by something outside the work being varied, not of a cost that scales with that
work.

**Phase 1 task:** the render probe must disable vertical retrace for the
duration of a measurement run. This is justified because the probe's purpose is
to measure CPU cost per frame, and a blocking wait for the display is not CPU
cost. The change is confined to the probe's own game configuration and does not
alter the shipped client, which keeps vertical retrace enabled. The recorded
fingerprint must state that retrace was disabled, so a probe report taken with
it enabled is never silently compared against one taken with it disabled.

This also resolves the resolution concern: `frameMilliseconds` is a `double`
derived from `Stopwatch.GetElapsedTime`, and the artifact records four decimal
places (5.2734), so sub-millisecond resolution is available. The problem was
never resolution. The problem was that a blocking wait was being counted as
work.

### 4.4 Probe-only work contaminates the geometry figure

Under the probe, `RecordPawnQuads` (`ArenaGame.Rendering.cs` lines 148 to 198)
re-walks the entire agent list and recomputes appearance, bounds, and layout for
every visible pawn — a second full pass over work `DrawPawns` will do again
moments later. The doc comment at lines 113 to 127 says this is deliberate: the
counting functions the quad budgets are pinned against must never live inside a
renderer's own per-frame path.

That reasoning is sound and the structure should stay. The consequence must be
stated: a probe run performs roughly twice the geometry work of a normal run,
which means `geometryBuildMicroseconds` is a probe-inflated figure and the
per-frame allocation figures are probe-inflated too.

**Phase 1 task:** record, in the probe report and in `testing.md`, that the
geometry-build span includes a probe-only duplicate pass, and that the allocation
figure is the probe's allocation and not the shipped client's. If Phase 2's
allocation claims are to mean anything, the two have to be separable. The
cleanest separation is a second recorded figure — the count of
`PawnGeometry.Create` invocations per frame — from which the duplication factor
is derivable rather than assumed.

### 4.5 The probe needs a 1,000-unit cell

The `--matrix` mode currently drives exactly two agent counts, 200 and 500
(`tools/Hukbo.Tools.RenderProbe/Program.cs` lines 104 onward). The target of
this work is 1,000. A 1,000-unit cell has to exist before any statement about
the target is possible.

Two further honesty items belong to the same task. First,
`RenderMatrixReport.AxesNote` already discloses that grass visibility and motion
intensity are not independently driven by the matrix; that disclosure stays and
should be extended to cover the retrace change from section 4.3. Second, the
report schema is unit-tested at
`tests/Hukbo.Client.Tests/RenderProbeReportTests.cs`, and every new field added
under Phase 1 extends those tests. The schema is testable without a graphics
device and must stay that way.

### 4.6 Phase 1 exit criteria

Phase 1 is complete when all of the following are recorded:

1. A matrix run at 200, 500, and 1,000 units, seed 1, 120 frames per station,
   Release configuration, vertical retrace disabled, with the fingerprint
   stating the retrace setting.
2. Every millisecond of `Draw` attributable to a named span, with the largest
   remaining unattributed span under ten percent of the frame at 1,000 units.
3. A tracked, committed location for the baseline artifacts, and a `testing.md`
   that cites something a fresh clone can open.
4. A statement of the probe-only duplication factor, so Phase 2's before-and-
   after comparison is comparing like with like.

Phase 1 produces no visual change and no gameplay change. `./scripts/verify.ps1`
must pass, and the seed-1 headless determinism workload must report the recorded
baseline state hash `A080E28DA7C79C20`, event hash `2B6FB3A9A9C1960D`,
`measuredTicks` 1677, and `coreAllocatedBytes` 118896 (`testing.md` lines 269 to
271) unchanged. Phase 1 touches only `Hukbo.Client` measurement code and the
hand-run probe tool, so any movement in those figures is a defect, not a
tradeoff.

---

## 5. Phase 2 — remove per-agent CPU cost

**Entry condition:** Phase 1 complete, with the remainder from section 4.2
attributed. If Phase 1 shows the remainder was entirely a driver stall, Phase 2
narrows to the three redundancies below and its expected gain shrinks
accordingly — but the redundancies are real regardless of what Phase 1 finds,
and removing them is worth doing on its own terms.

Phase 2 is expected to produce **no visual change at all**. Every item below is
a pure removal of duplicated or unnecessary work, and the set of pixels drawn
must be identical before and after. That is a testable claim and section 9
describes how it is tested.

### 5.1 R1 — `PawnGeometry.Create` runs twice per visible pawn per frame

`DrawPawns` (`ArenaGame.Rendering.cs` lines 503 to 564) calls
`PawnRenderer.GetBounds` at line 533 for the cull, and `GetBounds`
(`PawnRenderer.cs` lines 71 to 87) is a thin wrapper that calls
`PawnGeometry.Create(..., swingPose: null, ...)`, builds a complete
`PawnLayout`, and returns only `.VisualBounds` from it. Everything else the
layout computed is discarded. Then, for every pawn that survives the cull,
`DrawPawns` line 543 calls `PawnRenderer.Draw`, which at `PawnRenderer.cs` lines
149 to 157 calls `PawnGeometry.Create` a second time, this time with the real
swing pose.

So a visible pawn pays for two full layout constructions per frame. A culled
pawn pays for one. Under the render probe this becomes four, because
`RecordPawnQuads` repeats the same pair at lines 170 and 186.

**The fix must preserve pose-blindness.** The cull deliberately tests bounds
computed with `swingPose: null` (section 2.3), and switching it to test the
posed layout's bounds would change which pawns are drawn as a function of
animation phase. The redundancy is not the pose-blind cull; the redundancy is
computing a whole `PawnLayout` twice.

**Proposed shape:** a single entry point that returns both the pose-blind
`VisualBounds` and the posed `PawnLayout` from one call, with the pose-blind
bounds computed by the cheap subset of `Create` that actually determines them
rather than by running the full layout. Callers cull on the pose-blind bounds
exactly as today and draw from the posed layout. This is a pure-helper change in
`Hukbo.Client/Rendering` and is fully unit-testable without a graphics device.

The correctness test is an equivalence test: for a representative grid of
appearance, zoom, and pose inputs, the bounds returned by the new single call
must be bit-identical to `PawnRenderer.GetBounds`'s current result, and the
layout must be bit-identical to the current `PawnGeometry.Create` result. That
test fails before the change is made only in the sense that the new entry point
does not exist; the substantive assertion is that it passes afterward against
the old functions kept as the reference.

### 5.2 R2 — `PawnAppearanceFactory.Create` runs for every agent every frame, before the cull

`ArenaGame.Rendering.cs` line 524 calls
`PawnAppearanceFactory.Create(agent.EntityId, agent.Loadout.Weapon,
agent.Loadout.Shield)` for **every living agent**, and the cull test at line 538
happens afterward. So a 1,000-unit battle viewed at maximum zoom, where roughly
a dozen pawns are visible, still resolves 1,000 appearances every frame. This is
the exact scaling signature section 1 measured: cost proportional to total agent
count, indifferent to how many pawns reach the screen.

`PawnAppearanceFactory.Create`
(`src/Hukbo.Client/Presentation/PawnAppearanceFactory.cs` lines 37 to 107) is a
pure function of three inputs, performing three deterministic hash mixes and
then consulting `WeaponVisualCatalog.SelectTint`,
`ShieldVisualCatalog.SelectSkin`, and `AppearancePresets.SelectBlock` and
`SelectPreset`. Its inputs are constant for an agent's entire life: `EntityId`
never changes, and the loadout is fixed at spawn.

Two independent fixes apply and both should be taken.

**Fix 2a — move the cull ahead of the appearance resolution.** The cull needs
bounds, and bounds need appearance, so this is not a free reordering. But a
conservative pre-cull is: a pawn's visual bounds are contained within a
generous, appearance-independent radius around its foot anchor at a given zoom.
Testing that cheap conservative rectangle against the arena bounds first, and
only resolving appearance and exact bounds for pawns that pass, removes the
appearance cost for every distant pawn. The conservative radius must be a proven
upper bound over every appearance the catalogs can produce, or the cull will
drop a pawn it should have kept — so the radius is derived from the catalogs by
a test, not chosen by hand. Because the conservative bound is appearance-blind
and pose-blind, it strictly widens the admitted set relative to today's test and
then narrows it back with today's exact test, so the final drawn set is
unchanged by construction.

**Fix 2b — cache the resolved appearance.** This is a cache, so
`SIMULATION-GAME-STANDARDS.md` lines 215 to 217 apply in full and the cache must
declare source, key, value, size bound, lifetime, invalidation, counters, and a
cold-cache equivalence test. Unbounded caches are prohibited.

- **Source:** `PawnAppearanceFactory.Create`, which remains the single
  authority. The cache never computes an appearance itself.
- **Key:** the triple `(EntityId, Weapon, Shield)`. Not `EntityId` alone —
  keying on identity alone would silently return a stale appearance if a loadout
  ever became mutable, and the key should not depend on a property the
  simulation is not contractually obliged to keep.
- **Value:** the `PawnAppearance` value.
- **Size bound:** `2 * ArmyCompositionStepper.MaximumUnitsPerTeam` entries,
  which after section 5.4 is 1,000. This is a hard capacity, allocated once, not
  a growth target. Because a battle's agent set is fixed at scenario creation,
  the cache is exactly full and never evicts.
- **Lifetime:** one battle. Cleared on scenario reset, on next round, and on
  full reset — the same three points that already rebuild presentation state.
- **Invalidation:** none during a battle, because no key input can change during
  a battle. The design must state this as a load-bearing assumption, and a test
  must assert it: if `AgentView.Loadout` ever becomes mutable mid-battle, that
  test fails and this cache is invalid.
- **Counters:** hits, misses, and fill count, exposed through the existing
  `IRenderMetricsRecorder` seam so a probe run reports them.
- **Cold-cache equivalence test:** for a fixed scenario seed, the frame's
  resolved appearance for every agent must be identical whether the cache is
  cold or warm. This is directly testable as a pure-helper test with no graphics
  device.

Because the agent set is fixed and the key set is therefore fixed, the simplest
correct structure is a flat array indexed by the agent's ordinal position with
the key stored alongside for verification, rather than a dictionary. That avoids
hashing entirely and avoids the hash-iteration-order concern — though note that
this is presentation state, so iteration order here could not affect gameplay
even if it were a dictionary.

### 5.3 R3 — `HitEffectSystem.GetPulseStrength` is a linear scan called per pawn

`ArenaGame.Rendering.cs` lines 554 to 556 call
`_presentation.HitEffects.GetPulseStrength(agent.EntityId)` for every drawn
pawn. That method
(`src/Hukbo.Client/Presentation/HitEffectSystem.cs` lines 96 to 115) walks the
active effect array linearly looking for effects targeting that entity. The
array capacity is 256 (`PresentationCoordinator.cs` line 9) and effects live for
`PulseSeconds = 0.09f`, so the live count is normally far below capacity — but
in a 1,000-unit melee, where many agents are struck within any 90-millisecond
window, the live count can approach the cap.

The cost is therefore drawn-pawns multiplied by live-effects, which at the worst
case is a few hundred multiplied by a couple of hundred: tens of thousands of
comparisons per frame for what is a lookup.

**Proposed shape:** build the pulse lookup once per frame, before the pawn loop,
into a structure keyed by entity, and have the pawn loop read it. The natural
structure is the same flat, ordinal-indexed array used for the appearance cache
in 5.2, cleared and refilled each frame from the at-most-256 live effects — an
O(effects) build followed by O(1) per pawn, replacing O(pawns × effects).

This is presentation state with a one-frame lifetime, rebuilt rather than
invalidated, which is the pattern
`SIMULATION-GAME-STANDARDS.md` lines 207 to 209 prefer. It is not a cache in the
sense that needs a cache declaration, because it is discarded and rebuilt every
frame and holds nothing across frames. The equivalence test is that for any set
of live effects and any entity, the rebuilt lookup returns exactly what
`GetPulseStrength` returns today, including the maximum-over-effects and
lethal-exclusion behaviour at lines 102 and 111.

### 5.4 The unit cap has to move for the target to be reachable

`ArmyCompositionStepper.MaximumUnitsPerTeam` is 250
(`src/Hukbo.Client/UI/ArmyCompositionStepper.cs` line 23) and
`ClientSettings.DefaultUnitsPerTeam` is 250
(`src/Hukbo.Client/Settings/ClientSettings.cs` line 61). The total reachable
from the user interface today is therefore 500, and a 1,000-unit target is not
reachable at all.

The simulation is not the constraint: `Scenario.MaximumAgentsPerFaction` is
**10,000** (`src/Hukbo.Core/Simulation/Scenario.cs` line 18), so raising the
client cap to 500 per team requires no Core change and violates no Core bound.

**Proposed change:** `MaximumUnitsPerTeam` becomes 500. `DefaultUnitsPerTeam`
stays at 250, so the default experience is unchanged and the larger battle is
something a spectator opts into. This is deliberate: the default should remain a
size the measurements have covered for a long time, and 500 per team should not
become the default until Phase 2's re-measurement says it holds.

`MinimumUnitsPerTeam` stays at 4 and the stepper's clamp at line 37 continues to
do its job. The stepper's own tests extend to cover the new maximum and the new
boundary behaviour at line 72.

### 5.5 Phase 2 exit criteria

1. R1, R2, and R3 implemented, each with its equivalence test passing.
2. The appearance cache's full cache declaration recorded in the plan document
   and its cold-cache equivalence test passing.
3. The client per-team cap at 500.
4. `./scripts/verify.ps1` passing, with the seed-1 headless figures from section
   4.6 unmoved — Phase 2 touches only `Hukbo.Client`, so any movement is a
   defect.
5. A re-measurement, using the Phase 1 probe, at 200, 500, and 1,000 units.
   **This re-measurement is the input to section 6.**

---

## 6. The go/no-go trigger for Phase 3

Phase 3 is a large, risky change: a custom shader, a content-pipeline change
that breaks a pinned hygiene test, a device-reset path that does not exist
today, and a second rendering backend to keep alive forever. It must not be
built on the possibility that it might help. It is authorized only by a
measurement.

### 6.1 The target budget

**At 1,000 units (500 per team), seed 1, Release configuration, 120 frames per
station, vertical retrace disabled, at the default-fit camera station, the
client's `Draw` must hold p95 at or below 8.0 milliseconds.**

The 8.0 millisecond figure is derived, not chosen. A 60 Hz frame is 16.67
milliseconds and must contain both `Update` and `Draw`. The recorded simulation
cost at 1,000 agents is 0.8481 ms at p50 and 6.2364 ms at p95 per tick
(`testing.md` lines 260 to 263). A `Draw` p95 of 8.0 ms alongside an `Update`
p95 of 6.24 ms totals 14.24 ms, leaving 2.43 ms of headroom inside the frame for
presentation and operating-system scheduling. Any looser render budget cannot
co-exist with the measured simulation cost at the target army size, and any
tighter one would be demanding of the renderer what the simulation is not
delivering.

### 6.2 The trigger

**Phase 3 is authorized if and only if both of the following hold on the Phase 2
re-measurement:**

1. **The budget is missed.** The 1,000-unit default-fit `Draw` p95 exceeds
   **8.0 ms**.
2. **The overrun is in the submission path.** The Tier 1
   `submitMicroseconds` p95 at that same station is at least **50 percent** of
   the total `Draw` p95 at that station.

**GPU-004 is a hard prerequisite of evaluating clause 2, and evaluating clause 2
without it produces a false GO.** As sections 1 and 4.2 establish,
`submitMicroseconds` today times `DrawArenaLayer`, which builds the real
per-pawn geometry *and* submits it. A pre-GPU-004 figure therefore charges CPU
layout construction to the submission span. At the recorded 500-unit default-fit
station that span is already 92 percent of the frame, so clause 2 would read as
comfortably satisfied on a frame whose cost might be almost entirely CPU
geometry — authorizing the largest and riskiest change in this document on a
number that does not mean what the clause assumes it means. Clause 2 may be
evaluated only against a `submitMicroseconds` that GPU-004 has disaggregated
into a geometry component and a submission component, measured on a run with
vertical retrace disabled per section 4.3. An evaluation against any earlier
figure is invalid and is discarded, not argued with.

Both clauses are load-bearing and the second is the important one. Clause 1
alone would authorize an instanced backend for an overrun that instancing cannot
address — which is precisely the mistake section 1 exists to prevent. Instancing
changes how quads are submitted. If, after Phase 2, submission is not the
majority of the frame, then rewriting submission cannot bring the frame under
budget no matter how well it is done, and building it would be motion without
progress.

The 50 percent threshold is set where it is because instancing plausibly removes
most, but not all, of the submission cost: the instance record still has to be
written per quad, the buffer still has to be uploaded, and the draw still has to
be issued. Halving the submission span is an optimistic-but-defensible
expectation. If submission is 50 percent of an 8-plus millisecond frame, halving
it recovers over 2 ms and can plausibly close a moderate overrun. If submission
is 30 percent, halving it recovers under 1.2 ms and cannot.

### 6.3 What happens on a no-go

If either clause fails, **Phase 3 is not built.** The specific actions are:

- The `SpriteBatch` backend remains the sole backend. No capability probe
  ships, no `.fx` file is authored, no content-pipeline entry is added, and
  `tests/Hukbo.Client.Tests/SourceHygieneTests.cs` lines 253 to 271 stay exactly
  as they are with the six pinned spritefonts untouched.
- Section 8 of this document is archived under `docs/archives/` with the
  standard "Archived: reference only" banner and the recorded measurement that
  closed it, so that the next person to propose instancing finds the numbers
  rather than repeating the investigation.
- If clause 1 failed — the budget is met — the work is simply done. The
  1,000-unit target is reached with the existing backend and that is the good
  outcome.
- If clause 1 held but clause 2 failed — the budget is missed but not in
  submission — the overrun is characterised and routed to whichever span Phase 1
  named. If that span is the simulation's influence on the frame rather than the
  renderer's, it belongs to the collision-resolution concern in
  `docs/plans/2026-07-28-collision-resolution-scaling-design.md` and not to this
  design. This document does not authorize that work either; it hands over a
  measurement and stops.

### 6.4 The measurement that decides is not delegated

The go/no-go measurement is taken once, by hand, from a real probe run on a real
graphics device, and its JSON artifact is committed. No agent report, no
compilation success, and no test pass substitutes for it. This mirrors the rule
that `./scripts/verify.ps1` is the canonical gate and is never delegated.

Note honestly what the canonical gate can and cannot say here: the gate builds
`Release`, runs Core and GPU-independent Client tests, and runs the 200-agent
headless determinism workload. **It never opens a window and never touches a
graphics device**, so it cannot execute either rendering backend. The gate proves
that the code compiles, that the pure helpers are correct, and that the
simulation is unchanged. It proves nothing about what is on screen. Only the
hand-run probe and the interactive smoke checklist can speak to that, and only a
human at an interactive desktop may mark a smoke row as passing.

---

## 7. Blockers Phase 3 must clear before it starts

Four repository facts stand between the current state and an instanced backend.
Each is stated with its resolution, because a design that discovers these during
implementation will stall.

### 7.1 The content-pipeline hygiene test forbids a shader

`tests/Hukbo.Client.Tests/SourceHygieneTests.cs` lines 253 to 271 read
`src/Hukbo.Client/Content/Content.mgcb`, extract every `#begin` entry, and
assert two things: that the entry list equals `PinnedContentPipelineEntries`
exactly, and that every entry ends with `.spritefont`. The pinned list at lines
132 to 140 is the six fonts, and the current `Content.mgcb` matches it exactly.
The stated intent in the test's own doc comment is that "the package ships zero
textures, atlases, or shaders, per OD-4's fully procedural direction."

Adding an `.fx` file to the content pipeline fails **both** assertions.

**Resolution:** this is not a mechanical edit and must never be treated as one.
Changing this pin is a reviewed decision that Phase 3's task list carries as its
own named task, with its own justification recorded in the plan document. The
justification is narrow and should be written narrowly: the "zero shaders" pin
expressed a scope decision about *content* — that the game ships no authored art
assets — and a vertex and pixel shader that performs coordinate transformation
and colour passthrough is not authored art. The pin should be amended, not
deleted: the new assertion is that entries are either `.spritefont` or the one
named `.fx` file, so the guarantee that no texture or atlas can slip in
survives intact. Deleting the test would throw away a protection that is doing
real work.

### 7.2 No new package is needed

`SourceHygieneTests.cs` lines 278 to 293 assert that
`Directory.Packages.props` carries exactly five pinned package names:
`Microsoft.NET.Test.Sdk`, `MonoGame.Content.Builder.Task`,
`MonoGame.Framework.DesktopGL`, `xunit`, and `xunit.runner.visualstudio`.

**Resolution: no change is required.** Instancing needs nothing that is not
already present. `DynamicVertexBuffer`, `VertexBufferBinding`,
`GraphicsDevice.DrawInstancedPrimitives`, and `Effect` are all in
`MonoGame.Framework.DesktopGL`, and shader compilation is handled by
`MonoGame.Content.Builder.Task`, which is already pinned, together with the
`dotnet-mgcb` tool already pinned in `.config/dotnet-tools.json`. This test
should pass unchanged through Phase 3, and if a task ever proposes touching it,
that task has gone wrong.

### 7.3 There is no device-reset or resize recreation path anywhere

This is the largest hidden cost in Phase 3 and it is easy to miss.

`ArenaGame.cs` line 240 sets `Window.AllowUserResizing = true`. There is no
`ClientSizeChanged` handler, no `DeviceReset` or `DeviceLost` subscription, and
no `ApplyChanges` call anywhere in `src/` or `tools/`. Resizing works today
purely because the renderer re-reads `GraphicsDevice.Viewport.Bounds` at the top
of every `Draw` (`ArenaGame.Rendering.cs` line 45) and recomputes its layout from
that. It works **only** because the sole GPU resource the client owns is a 1x1
pixel texture, which no resize or device event can invalidate in a way that
matters.

An instanced backend owns three GPU resources that all need recreation: the
static quad vertex buffer, the static index buffer, and the dynamic instance
buffer, plus a compiled `Effect`. `DynamicVertexBuffer` exposes `IsContentLost`
precisely because its contents can be lost.

**Resolution:** Phase 3 designs this path from nothing, as its own task, and it
is not a footnote. The concrete requirements are:

- A single owner type for all instanced GPU resources, implementing
  `IDisposable`, with one `CreateResources` method and one `ReleaseResources`
  method, so recreation is one call and cannot be partially performed.
- Subscription to `GraphicsDeviceManager.DeviceReset` and `DeviceCreated`, with
  the handler releasing and recreating.
- A per-frame check of `DynamicVertexBuffer.IsContentLost` before upload,
  recreating on loss. This is cheap and is the belt-and-braces guard for the
  cases the events do not cover.
- The static vertex and index buffers are recreated but their contents are
  constants, so recreation is regeneration from code and never from a file.
- A fallback: if resource creation throws at any point, the backend latches off
  permanently and the `SpriteBatch` backend takes over for the rest of the
  session, with an `err`-level diagnostic line naming the failure. A renderer
  that cannot recreate its buffers must degrade, not crash.
- A debug-log line at `dbg` level on every recreation, so a log from a session
  where the user dragged the window edge shows the recreations that happened.

Because none of this exists today, it cannot be assumed to work by analogy with
existing code. It is new surface and carries its own interactive smoke rows —
resize during battle, alt-tab during battle, and display-mode change during
battle — none of which any automated test in this repository can perform.

### 7.4 Client tests can never construct a graphics device

Client tests must never construct `ArenaGame`, a `GraphicsDevice`, a
`SpriteBatch`, or a window. There are currently zero such occurrences anywhere
under `tests/`, and that is a property to preserve rather than an accident.

The consequence for Phase 3 is unavoidable and must be stated rather than
papered over: **the canonical gate cannot execute either rendering backend.** No
test in this repository can assert that a triangle appeared. Section 9 draws the
line between what stays testable and what does not, and the answer is to push
every decision into pure helpers and leave only the irreducible
upload-and-draw call untested.

---

## 8. Phase 3 — the instanced backend design

**Entry condition: section 6.2's two-clause trigger has fired on a recorded,
committed Phase 2 re-measurement.** Nothing below may be built otherwise.

The design is given in full so that the go/no-go decision in section 6 is a
decision about a known quantity. Every MonoGame-specific constraint below was
checked against MonoGame 3.8.5 DesktopGL behaviour, and the one item that could
not be confirmed is flagged as unconfirmed in section 8.12 and section 10.

### 8.1 Capability probe and latch

MonoGame's `GraphicsCapabilities` type is internal and its `SupportsInstancing`
getter is internal, so there is no public API to ask whether instancing is
available. The only supported detection is empirical: attempt one instanced draw
and see whether it throws.

**Design:**

- At `LoadContent` time, after resources are created, the backend issues one
  throwaway `DrawInstancedPrimitives` call with an instance count of 1, drawing
  a degenerate quad — zero scale, fully transparent — into the back buffer
  before the first `Clear` of the first real frame, so nothing it draws can be
  visible even if it succeeds.
- The call is wrapped in a `try`/`catch` for `PlatformNotSupportedException`.
  MonoGame's failure mode here is a thrown `PlatformNotSupportedException` on the
  first draw, not a silent no-op, so this test is decisive rather than
  suggestive.
- The result latches into a `readonly bool` set once and never re-evaluated. A
  probe that runs per-frame, or per-resize, would be both wasteful and a source
  of mid-session backend flapping.
- The latch also goes false if resource creation itself threw (section 7.3), so
  there is one boolean and one meaning: this session can use the instanced
  backend, or it cannot.
- The probe writes exactly one `inf`-level debug-log line on a new
  `render` channel, with a stable dotted event identifier declared as a `const`
  on `LogEvents` — for example `render.backend.selected` — carrying the selected
  backend name, whether the probe threw, and the exception type name if it did.
  This is once per session, so `inf` is the right level. It must not carry a
  prose sentence as its event identifier; free prose belongs in the optional
  `msg` field, and this line is not an error or a warning, so it carries no
  prose at all.

The probe is the whole answer to the minimum-specification question. See section
10.

### 8.2 Backend selection seam

`IRenderMetricsRecorder` (`src/Hukbo.Client/Rendering/RenderMetrics.cs` lines 44
to 117) was designed for exactly this and its doc comment at lines 5 to 8 says
so: "an interface rather than a static counter class, so a future GPU-instanced
backend can supply its own implementation without editing the presentation layer
that records into it." Its Tier 1 and Tier 2 split (lines 10 to 33) is the
mechanism that keeps a backend comparison honest: Tier 1 is renderer-invariant
and is the only tier a budget is written against; Tier 2 is backend-specific,
diagnostic only, never budgeted, and each Tier 2 metric is paired with an
`*Applicable` flag on `RenderMetricsSnapshot` so a metric the active backend
does not produce reports as absent rather than as a false zero.

The seam is already used correctly today.
`SpriteBatchRenderMetricsRecorder.AddBufferUploadBytes` (lines 322 to 324) is an
explicit no-op with a doc comment explaining that the `SpriteBatch` backend
uploads no instance buffer, and `Snapshot` at lines 340 and 341 reports
`BufferUploadBytes: 0, BufferUploadBytesApplicable: false`. An
`InstancedRenderMetricsRecorder` mirrors this in reverse: it reports
`BufferUploadBytes` honestly and applicable, and reports `Submissions` as 0 and
not applicable, because a `SpriteBatch.Draw` call is not a thing that happens
under it. `Batches` stays applicable under both backends with its meaning
shifted from a `Begin`/`End` pair to one instance batch, exactly as the existing
doc comment at lines 96 to 101 anticipates.

`RenderProbeFingerprint.Backend`
(`tools/Hukbo.Tools.RenderProbe/Program.cs` line 309) already records the
backend name as a string, currently the constant `"spritebatch-1x1"`, with a
comment stating that this exists so a later GPU-instanced backend's report is
legibly a different backend rather than silently comparable numbers in an
incompatible unit. The instanced backend supplies a new constant — `"instanced"`
— and the probe reports whichever the latch selected. A report from a machine
where the probe threw records `"spritebatch-1x1"`, which is the truthful answer
for that machine.

The rendering seam itself is a small interface over the arena layer, with two
implementations, selected once at startup by the latch. The presentation code
that decides *what* to draw is shared and does not know which backend it is
talking to.

### 8.3 The instance record and its byte budget

Everything a quad needs is small, because the renderer draws a single white
texel with no texture region and a constant layer depth of zero:

| Field | Type | Bytes | Notes |
| --- | --- | ---: | --- |
| Screen position | `Vector2` | 8 | pixels, top-left origin |
| Scale | `Vector2` | 8 | pixels, the quad's width and height |
| Rotation | `float` | 4 | radians |
| Origin selector | `float` | 4 | only two distinct values exist today |
| Colour | packed RGBA | 4 | one `Color` |
| **Total** | | **28** | |

The origin selector deserves comment. Across the whole pawn and backdrop
geometry there are exactly two origins in use, `(0.5, 0.5)` and `(0, 0.5)`, so
the field is effectively one bit. It is carried as a `float` rather than packed
into a spare byte for two reasons: it keeps every attribute on a four-byte
boundary without padding, and it leaves room for a third origin without a format
change. If a future pass needs more per-instance data — a material index for the
warrior-appearance work in section 10 — it takes this slot's spare capacity by
becoming a `Vector2` of `(originSelector, materialIndex)`, growing the record to
32 bytes, which is a better place to be than 28 anyway.

**Upload budget.** `tests/Hukbo.Client.Tests/PawnQuadCountTests.cs` pins the
per-pawn quad counts at 17, 19, and 20 for the ordinary cases and 40 for the
combinatorial worst case (lines 38, 50, 62, and 103). At the 1,000-unit target:

- Typical, 19 quads per pawn: 19,000 instances, **532 KB per frame**.
- Absolute worst case, 40 quads per pawn with every pawn visible: 40,000
  instances, **1.12 MB per frame**.

Adding the backdrop, which the recorded baseline shows contributes a few
thousand quads, a **2 MB per-frame upload ceiling** is a safe budget. At 60 Hz
that is 120 MB per second across the bus, which is not a concern in itself. The
concern is the buffer orphaning discussed in 8.6, not the bandwidth.

Compare this against what `SpriteBatch` does today: four
`VertexPositionColorTexture` structs per quad, each 24 bytes, so 96 bytes per
quad against 28. The instanced path writes roughly 3.4 times less CPU-side data
per quad. That ratio, not the draw-call count, is the performance argument.

### 8.4 Vertex declaration and semantics

Only thirteen semantics survive translation to GLSL on the DesktopGL path:
`POSITION`, `COLOR`, `NORMAL`, `TEXCOORD`, `BLENDINDICES`, `BLENDWEIGHT`,
`BINORMAL`, `TANGENT`, `PSIZE`, `DEPTH`, `FOG`, `SAMPLE`, and
`TESSELLATEFACTOR`. Attribute location is keyed on the pair
(usage, usage index), so `VertexElementUsage.TextureCoordinate` with usage index
*n* binds to `TEXCOORD`*n* in the shader.

**Slot 0, per-vertex, `instanceFrequency` 0** — a static four-vertex buffer
describing the unit quad:

| Offset | Type | Usage | Index | Semantic |
| ---: | --- | --- | ---: | --- |
| 0 | `Vector2` | Position | 0 | `POSITION0` |
| 8 | `Vector2` | TextureCoordinate | 0 | `TEXCOORD0` |

The position is the corner offset in unit-quad space, one of `(0,0)`, `(1,0)`,
`(0,1)`, `(1,1)`. The texture coordinate is unused by the current single-texel
renderer but is kept so that a later change that needs it does not require a
format change.

**Slot 1, per-instance, `instanceFrequency` 1** — the dynamic instance buffer:

| Offset | Type | Usage | Index | Semantic |
| ---: | --- | --- | ---: | --- |
| 0 | `Vector4` | TextureCoordinate | 1 | `TEXCOORD1` |
| 16 | `Vector2` | TextureCoordinate | 2 | `TEXCOORD2` |
| 24 | `Color` | Color | 1 | `COLOR1` |

`TEXCOORD1` packs position and scale as `(x, y, width, height)`. `TEXCOORD2`
packs `(rotation, originSelector)`. `COLOR1` is the packed RGBA tint.

The per-vertex frequency **must** be 0 and the per-instance frequency **must**
be greater than 0; `VertexBufferBinding(buffer, vertexOffset,
instanceFrequency)` enforces this and getting it backwards is a silent
correctness failure rather than a throw.

There is a hard limit on the number of vertex attribute slots, read from
`glGetInteger(GL_MAX_VERTEX_ATTRIBS)` at device setup. It is not publicly
readable from MonoGame. The design uses five attributes across two slots, which
is far below any limit a device supporting the required GL version would impose,
so this is a constraint to be aware of rather than one that binds here.

### 8.5 Quad mesh and the mandatory index buffer

`DrawInstancedPrimitives` on MonoGame 3.8.5 requires an index buffer to be set.
Calling it without one throws `InvalidOperationException`. This is not optional
and not a suggestion.

The mesh is therefore a four-vertex, six-index quad with a 16-bit index buffer
containing `0, 1, 2, 2, 1, 3`. Both the vertex buffer and the index buffer are
static, created once, and regenerated from constants on device reset. 16-bit
indices are correct and sufficient because the index buffer addresses the
four-vertex mesh, not the instance stream — the instance count is a parameter to
the draw call and is not index-bound, so the 5,461-quad `SpriteBatch` ceiling
from section 2.1 has no analogue here.

### 8.6 Dynamic instance buffer sizing and upload

**Sizing.** The buffer is created once at a fixed capacity of **65,536
instances**, which at 28 bytes per instance is **1.79 MB**. That capacity covers
the section 8.3 worst case of 40,000 instances with substantial margin and means
the growth path is never exercised within the supported range. This is
deliberate: MonoGame has no buffer resize API, so growth means allocating a new
buffer and disposing the old one, and a renderer that does that mid-battle is a
renderer that stutters mid-battle. A fixed capacity chosen from a pinned
worst-case quad count is better than a growth policy.

If the instance count ever exceeds capacity, the backend does not grow. It draws
the first 65,536 instances, emits one `warn`-level log line naming the overflow
count, and continues. This is a visible defect rather than a crash, and it
cannot happen within the supported army sizes, so the log line is the alarm that
says the supported range was exceeded.

**Upload.** `SetDataOptions.Discard` is the only correct option on this path. On
DesktopGL it maps to an orphaning `glBufferData` followed by `glBufferSubData`,
which is the standard way to write a whole dynamic buffer without stalling on
the previous frame's draw. `SetDataOptions.NoOverwrite` is **identical to
`SetDataOptions.None`** in MonoGame 3.8.5 on DesktopGL — there is no
unsynchronised write path in that version. The design must therefore **not**
attempt a sub-range append strategy, because the mechanism that would make it
worthwhile does not exist. One `Discard` write of the whole active range, once
per frame, is the design.

`SetData` pins the source array via a `GCHandle` for the duration of the call.
The backend therefore owns **one** instance array, allocated once at the fixed
capacity, reused every frame, and never reallocated. Allocating a per-frame
array here would put the renderer's own allocation into the allocation figure it
is measuring, which is the exact discipline `RenderMetrics.cs` lines 34 to 40
already require of the recording methods.

`IsContentLost` is checked before each upload, per section 7.3.

### 8.7 Shader

There is no seam inside `SpriteBatch` to reuse. `SpriteBatcher` hardcodes a
`VertexPositionColorTexture[]` and `DrawUserIndexedPrimitives`, so no amount of
configuration turns it into an instanced path. A custom `.fx` is required, not
preferred.

**Constraints on the DesktopGL path:**

- MojoShader translates the compiled bytecode, and the shader must target
  `vs_3_0` and `ps_3_0`.
- `SV_POSITION` and `SV_TARGET` do not exist on the GL path. The shader must
  `#define` them to `POSITION` and `COLOR` respectively, or use the older
  semantics directly.
- `SV_InstanceID` must be assumed unavailable. Instance identity arrives as a
  vertex attribute, which the section 8.4 layout already provides — every
  per-instance value the shader needs is in `TEXCOORD1`, `TEXCOORD2`, and
  `COLOR1`, and the shader never needs to know which instance number it is.

**Vertex shader.** Inputs: the unit-quad corner offset from `POSITION0`, and the
three per-instance attributes. Uniforms: a single `float2` of
`(2 / viewportWidth, -2 / viewportHeight)` plus the corresponding offset, which
converts screen pixels to normalised device coordinates. Because the LOD tier is
uniform across the frame (section 2.4), one uniform set per frame is sufficient
and no per-instance tier data is needed.

The transform, in order: take the corner offset, subtract the origin selected by
`originSelector`, multiply by the instance scale, rotate by the instance
rotation, add the instance position, then convert to normalised device
coordinates with the viewport uniform. Output the instance colour unchanged to
the pixel shader.

**Pixel shader.** Returns the interpolated colour. The renderer draws a single
white texel, so there is no sampling. If a texture is ever introduced, this is
where it lands — but that is out of scope per section 3.2.

**Render state.** The instanced backend must reproduce the current arena state
exactly: `BlendState.AlphaBlend`, and the same scissor-enabled `RasterizerState`
the arena layer already uses, with the same scissor rectangle set to the arena
bounds. A backend that draws correct geometry outside the arena panel is a
backend that draws over the user interface.

### 8.8 Ordering

Draw order is presentation-visible because the renderer relies on painter's
ordering with a constant layer depth and no depth buffer. The instance stream is
therefore built in exactly the order the current backend submits, and the draw
consumes it in buffer order.

`AgentView` iteration is already deterministic and ascending by `EntityId`,
which is a safe primary ordering key. One caution matters and is easy to get
wrong: `BattleSimulation._agentViews` is **rewritten in place** by `UpdateViews`
(`src/Hukbo.Core/Simulation/BattleSimulation.cs` lines 1866 to 1872, called from
line 359 each tick). It is a live view, not double-buffered the way
`LastEvents` is. The instance build must therefore complete within a single
frame's read of that list and must not retain a reference across a tick boundary
expecting stability. Because instance building already happens entirely inside
`Draw`, this is satisfied by construction — but it must be stated, because a
future "build the instance stream incrementally across frames" optimisation
would silently violate it.

### 8.9 Metrics recorder

`GraphicsDevice.Metrics` is public and exposes `ClearCount`, `DrawCount`,
`PrimitiveCount`, `SpriteCount`, `TargetCount`, `TextureCount`,
`VertexShaderCount`, and `PixelShaderCount`, with `op_Addition` and
`op_Subtraction` for differencing two snapshots. It is currently used **nowhere**
in this repository — a repository-wide search for `GraphicsDevice.Metrics`
returns no hits in `src/`, `tools/`, or `tests/`.

Two properties are worth knowing before anyone reaches for it. `DrawCount`
counts draws per effect pass, so a multi-pass effect inflates it relative to the
number of draw calls issued. And `DrawInstancedPrimitives` increments
`PrimitiveCount` by the primitive count of a single instance, not by the total
across instances, so `PrimitiveCount` under an instanced backend is not
comparable to `PrimitiveCount` under the current one.

**Recommendation:** do not introduce `GraphicsDevice.Metrics` as a Tier 1
figure. Its semantics differ between the two backends in exactly the way the
Tier 1 and Tier 2 split exists to prevent. If it is used at all it is a Tier 2
diagnostic with its own `*Applicable` flag, and the existing Tier 1 fields
remain the only budgeted ones.

The `InstancedRenderMetricsRecorder` reports:

- Tier 1, unchanged in meaning: quads, triangles, geometry-build microseconds,
  submit microseconds, managed bytes allocated. These are the comparable figures
  and the reason a backend swap can be judged at all.
- Tier 2: `BufferUploadBytes` honest and applicable, `Batches` applicable with
  its meaning shifted to instance batches, `TextureBinds` applicable, and
  `Submissions` reported as 0 and **not** applicable, because there is no
  `SpriteBatch.Draw` under this backend.

### 8.10 Content pipeline change

The `.fx` file is compiled offline by `dotnet-mgcb` version 3.8.5, which is
already pinned in `.config/dotnet-tools.json`. No new package is added and
section 7.2's pinned-package test passes unchanged.

Two operational facts matter. First, shader compilation on Windows is native and
offline — the build stays fully offline and no network access is introduced.
Second, `Content.mgcb` is edited by hand and not through the MGCB Editor
(`dotnet-mgcb-editor`), because the editor is not installed and hand-editing is
the convention this repository already follows for the six font entries. On
macOS or Linux, shader compilation would additionally require the one-time
`mgfxc_wine_setup.sh` step; this is noted for completeness and does not apply,
because the supported platform for this repository is Windows and there is no
cross-platform build to satisfy.

The `SourceHygieneTests` amendment from section 7.1 is a separate, named,
justified task and not part of this one.

### 8.11 Fallback behaviour

The `SpriteBatch` backend is retained permanently. It is not a transitional
scaffold to be removed after the instanced backend proves itself.

- If the capability probe throws, the client runs the `SpriteBatch` backend for
  the whole session. The spectator sees the game, not an error.
- If resource creation throws, likewise.
- If a device reset cannot recreate resources, the backend latches off
  mid-session and the `SpriteBatch` backend takes over from the next frame.
- Both backends must produce visually equivalent output. Section 9 explains why
  this cannot be asserted by a test and how it is checked instead.
- The active backend is reported in the render-probe fingerprint and in one
  session-level debug-log line, so any report, screenshot, or bug filed against
  a run can be traced to a backend without guesswork.

The maintenance cost of two backends is real and should be acknowledged rather
than minimised: every future change to what the arena draws must be made twice
or must be made in shared pure-helper code that both backends consume. Section 9
argues for the latter, which is the same argument that makes the change testable
at all.

### 8.12 One unconfirmed item

`VertexElement.UsageIndex` is documented as being "adjusted internally by
MonoGame when multiple vertex buffers are bound", and the exact rule governing
that adjustment is not documented anywhere that could be checked. This matters
because section 8.4's layout depends on `TEXCOORD1` and `TEXCOORD2` in slot 1
binding to those exact semantics in the shader while slot 0 uses `TEXCOORD0`.

**This is unconfirmed and must not be locked in without evidence.** Phase 3's
first task is an empirical spike: bind the two-slot layout, draw a small number
of instances with known distinct per-instance values, and verify in the output
that each attribute arrived where the shader expected it. Only after that spike
returns are the semantics fixed in the design. If the adjustment rule turns out
to renumber slot-1 indices, the layout changes to whatever the observed rule
requires, and the change is confined to the vertex declaration and the shader
input struct.

This is the single item in section 8 most likely to cost unplanned time, which
is why it is scheduled first.

---

## 9. Testability boundary

Shader mathematics is invisible to xunit. No test in this repository can execute
a vertex shader, and no test can assert that a pixel was the right colour. This
sits in tension with the client's pure-helper testability rule, which exists
precisely so that everything meaningful about the renderer is testable without a
graphics device.

The resolution is to move the boundary rather than accept a large untestable
region.

### 9.1 What stays pure and testable

Every **decision** lives in a pure helper, takes plain values, returns plain
values, and touches no MonoGame type that requires a device:

- **Instance selection.** Which agents produce instances this frame. A pure
  function of the agent list, the arena rectangle, and the camera, returning a
  count and an ordered set of entity identifiers.
- **Culling.** Both the conservative pre-cull from section 5.2 and the exact
  pose-blind test. Already pure today and stays pure.
- **Detail tier.** Already pure — `PawnGeometry`'s zoom-to-tier function and
  `DetailTierGate` mirror one another and both are testable.
- **Colour resolution.** Which colour an instance carries, given faction, visual
  state, hit-pulse strength, material, and theme. Pure, and the most
  behaviourally load-bearing thing in the whole change.
- **Packing and encoding.** The function that turns a position, scale, rotation,
  origin, and colour into the 28-byte instance record. This is a pure
  `struct`-producing function and its output is directly assertable, byte for
  byte, without a device. Round-trip tests — pack then unpack — catch field
  ordering and offset errors that would otherwise only appear as garbled
  geometry on screen.
- **Buffer sizing arithmetic.** Capacity, active count, byte count, and the
  overflow decision from section 8.6. Pure integer arithmetic, fully testable,
  including the overflow branch which is otherwise unreachable in practice.
- **Backend selection given a latch value.** Pure, given a boolean.

The existing quad-count tests are the model here.
`tests/Hukbo.Client.Tests/PawnQuadCountTests.cs` pins per-pawn counts of 17, 19,
20, and a worst case of 40 without ever touching a device, and the instanced
backend must reproduce those counts **exactly**. That is the closest thing to a
pixel-equivalence proxy this repository can have: if both backends emit the same
number of quads from the same inputs, in the same order, with the same packed
values, then the only remaining difference is the shader, and the shader is
twelve lines of coordinate transformation.

### 9.2 What is genuinely untestable

Three things, and only three:

1. The `SetData` upload call.
2. The `DrawInstancedPrimitives` call.
3. The shader itself.

Everything else is above the boundary. This is a much smaller untestable surface
than "the instanced renderer", and keeping it that small is the point of section
9.1.

These three are covered by interactive smoke rows in
`docs/development/testing.md` rather than by assertions, and those rows may only
be marked as passing by a human at an interactive desktop who has actually
looked at the screen. The rows needed are: instanced backend renders a battle
identically to the `SpriteBatch` backend at all three camera stations; forced
fallback (probe latched off) renders correctly; window resize during battle;
alt-tab during battle; and display-mode change during battle. Compilation, unit
tests, and a window-opening probe do not let anyone flip those rows. A row that
has not been performed stays `PENDING`, and a row that cannot be performed is
reported `BLOCKED` honestly.

### 9.3 The C# and HLSL drift hazard

Any mathematics that exists in both the C# packing code and the HLSL vertex
shader is a drift hazard: the two can diverge silently, and no test will catch
it because no test executes the shader.

The specific duplications this design creates are:

- **The origin selector's meaning.** C# writes a float; HLSL branches on it.
  If C# ever adds a third origin and HLSL is not updated, the third origin
  renders as one of the first two.
- **The screen-pixel to normalised-device-coordinate transform.** C# computes
  the viewport uniform; HLSL applies it. A sign error or an off-by-half-pixel
  in either is invisible to tests.
- **The rotation convention.** C# writes radians in one handedness; HLSL
  assumes one. A mismatch mirrors every rotated quad.

**Mitigations, all three of which are required rather than optional:**

1. Minimise the duplication. The shader does as little arithmetic as possible;
   anything that can be computed on the CPU and passed in is computed on the
   CPU and passed in. The shader should be short enough to read in one screen.
2. Every duplicated constant and convention is declared once in C# with a doc
   comment naming the HLSL line that mirrors it, and the HLSL carries the
   reciprocal comment. This does not prevent drift but it makes drift visible to
   a reviewer.
3. A source-hygiene test asserts that the shader source file contains the
   expected mirrored comment markers, so deleting the cross-reference fails the
   build. This is a weak guard and should be described as one — it catches
   deletion, not divergence.

The honest summary is that this hazard is not eliminated, only reduced and made
visible. It is one of the real costs of Phase 3 and it should be weighed in
section 6's decision rather than discovered afterward.

---

## 10. Risks and open questions

### 10.1 Determinism

The simulation's state hash and event hash must be byte-identical across every
phase of this work. Rendering is presentation-only, so this should be automatic
— but "should be automatic" is how determinism breaks, so it is asserted rather
than assumed. Every phase's exit criteria include the seed-1 headless figures
from section 4.6.

**Declared: this design makes no change to `Hukbo.Core`.** No new field, no new
enum value, no change to enum ordering, no change to roster order, weights, or
the hash mixer. Section 5.4's unit-cap change is a client user-interface
constant and is bounded by `Scenario.MaximumAgentsPerFaction`, which is 10,000
and is not changed.

**Out of scope, and named explicitly:** the warrior-appearance research at
`docs/research/improve-visuals/warrior-appearance-historical-research.md` lines
239 to 267 and 883 to 884 proposes an *earned* red head wrap, gated on a veteran
status or kill-count marker in agent metadata. No such authoritative state
obviously exists in `Hukbo.Core` today, and adding one would move the state
hash. **Any appearance feature that requires new authoritative simulation state
is out of scope for this design**, including that one specifically. If it is
wanted, it is a `Hukbo.Core` change with its own design, its own new preset
version, and its own new golden expectations, and it does not ride along on a
rendering change.

### 10.2 Colour, themes, and per-instance material — a position, not an open question

There is a genuine conflict in the repository and this design takes a side.

`UiThemeColors` defines twenty-seven semantic roles and no renderer may
hardcode a `Color`. Meanwhile the warrior-appearance research at
`warrior-appearance-historical-research.md` lines 829 to 841 specifies ten
literal hex swatches for garments and materials, and per-instance colour is the
main capability argument for instancing in the first place (section 2.1).

**Position: a separate, explicitly non-theme material palette, with a stated
rule that survives the high-contrast theme.**

The reasoning is that the twenty-seven roles describe *user-interface affordance
meaning* — surface, text, accent, danger, and so on. A garment dye is not an
affordance. It carries no interaction semantics, it tells the spectator nothing
about what is clickable or dangerous, and it is not something a theme should be
obliged to have an opinion about. Folding ten garment swatches into the theme
system would inflate the role count for every theme including the high-contrast
one — the catalog at `src/Hukbo.Client/Theming/UiThemeCatalogFallback.cs` line 57
shows `high-contrast` is a real shipped theme — and would force each theme to
supply values it has no basis for choosing.

So: a `WarriorMaterialPalette`, separate from `UiThemeColors`, carrying the
research-derived material tones, with one rule that keeps the accessibility
guarantee intact. **Under the high-contrast theme, per-instance material colour
is suppressed entirely and every pawn resolves to that theme's faction colour.**
The accessibility guarantee survives by suppression rather than by recolouring,
which is both simpler and more honest than trying to find ten high-contrast
garment dyes that pass contrast validation against each other.

This keeps "exactly 27 roles" true, keeps "no renderer hardcodes a `Color`" true
in the sense that matters — colours come from a named, reviewed palette rather
than from a literal at a call site — and gives the high-contrast theme a clean,
statable behaviour rather than a compromise.

The historical claims underlying the palette carry their evidence tier per the
repository's historical-accuracy policy. The material tones derived from the
Boxer Codex are **Provisional reconstruction**, because the Codex guides
silhouette and colour rather than exact dye chemistry, and any gameplay-facing
tuning value derived from them is marked provisional in code comments and tests
rather than presented as a historical measurement.

### 10.3 There is deliberately no minimum-specification contract

No document in this repository states a minimum graphics specification, and this
design **deliberately does not create one.**

The capability probe from section 8.1 plus the retained `SpriteBatch` fallback
from section 8.11 are the complete answer. A machine that supports instancing
gets the instanced backend; a machine that does not gets the backend it already
has today, which is the backend every recorded measurement was taken against.
There is no configuration a spectator can be in where the game refuses to run
because their hardware is below a line, because no line is drawn.

Writing a minimum-specification contract would be a promise the project has no
way to test — there is no hardware matrix, no continuous integration, and no
telemetry. A probe that asks the actual device is strictly better than a promise
about a class of devices.

### 10.4 Open questions

1. **Unconfirmed: `VertexElement.UsageIndex` adjustment across multiple bound
   buffers.** Section 8.12. Requires an empirical spike before the vertex
   declaration is fixed. This is the highest-uncertainty item in the design.
2. **What the section 1 remainder actually is.** Phase 1's entire purpose. Until
   it is attributed, the expected gain from Phase 2 is unknown and the
   probability that section 6's trigger fires is unknown. This is stated as an
   open question rather than hidden, because it is the reason the work is phased
   at all.
3. **Whether Phase 2 alone reaches the 1,000-unit budget.** Genuinely unknown,
   and the honest answer is that it might. That would be the best outcome and
   would close this design unbuilt.
4. **Whether the conservative pre-cull radius in section 5.2 can be derived
   tightly enough to be worth having.** If the provable upper bound over all
   catalog appearances is very generous, the pre-cull admits nearly everything
   and saves little. This is answerable by a test against the catalogs and
   should be answered before the fix is implemented, not after.
5. **Whether two permanently maintained backends is a cost worth paying at
   1,000 units.** Section 8.11 raises it; section 6's trigger is the mechanism
   that answers it with a number rather than an opinion.

---

## 11. The nine acceptance questions

`SIMULATION-GAME-STANDARDS.md` lines 320 to 330 require every feature proposal
to state the following. Each is quoted verbatim and answered.

**1. User-visible outcome**

For Phases 1 and 2, honestly: **there is no visual change.** Not a single pixel
differs. The outcome is that frame cost falls and that a 1,000-unit battle
becomes selectable from the army-composition stepper, where today the maximum is
500 in total. A spectator who never raises the unit count sees nothing at all
change, which is the intended result.

For Phase 3, if it is built: still no visual change on its own. An instanced
backend that renders differently from the `SpriteBatch` backend is a defect, not
a feature. The user-visible outcome of Phase 3 is entirely in what it makes
possible afterward — per-unit material colour at a cost the current backend
cannot pay — and that follow-on work is not part of this design.

**2. Tick stage and state read/written**

**None.** This design touches no tick stage. `Hukbo.Core` is not modified, no
simulation state is read in a way it is not already read, and no simulation
state is written. The client reads `BattleSimulation.Agents` exactly as it does
today, inside `Draw`, after the tick has completed. Section 8.8 records the
in-place rewrite hazard on `_agentViews` and the constraint it imposes on any
future incremental instance build.

**3. Numeric units/bounds and same-tick conflict rule**

Units: milliseconds for frame time, microseconds for the Tier 1 spans, bytes for
allocation and upload, and count for quads, triangles, and instances. Bounds:
`Draw` p95 at or below 8.0 ms at 1,000 units (section 6.1); instance capacity
65,536, being 1.79 MB (section 8.6); per-frame upload ceiling 2 MB (section
8.3); appearance cache size bound 1,000 entries (section 5.2); per-team unit cap
500 against a `Scenario.MaximumAgentsPerFaction` of 10,000 (section 5.4).

Same-tick conflict rule: not applicable. Nothing here writes simulation state, so
no two writers can conflict. The presentation-side analogue — two things wanting
to draw the same quad differently in one frame — is resolved by draw order,
which section 8.8 pins to the current backend's order exactly.

**4. Total ordering and random-stream policy**

Ordering: the instance stream is built in the current backend's exact submission
order, with `AgentView` iteration ascending by `EntityId` as the primary key
(section 8.8). Painter's ordering is presentation-visible and is preserved
byte-for-byte.

Random streams: **none are consumed.** No phase of this work draws from any
random stream. `PawnAppearanceFactory` performs deterministic hash mixes of
fixed inputs, which is not a stream and produces the same value on every call —
that property is exactly what makes the section 5.2 cache valid. `System.Random`
is not used and remains banned.

**5. Cache source/invalidation or "no cache"**

One cache is introduced: the appearance cache in section 5.2. Its full
declaration — source, key, value, size bound, lifetime, invalidation, counters,
and cold-cache equivalence test — is given there and is bounded at 1,000
entries. It is never unbounded and never grows.

The per-frame pulse lookup in section 5.3 is **not** a cache. It is rebuilt from
scratch every frame and holds nothing across frames, which is the
rebuild-over-invalidate pattern the standards prefer.

No other cache is added. No derived cache, no render data, and no metrics are
saved into any snapshot.

**6. Save/event/version effect or "presentation only"**

**Presentation only.** No save format changes, no event is added or altered, no
preset version changes, and no golden expectation changes. The seed-1 headless
figures — state hash `A080E28DA7C79C20`, event hash `2B6FB3A9A9C1960D`,
`measuredTicks` 1677, `coreAllocatedBytes` 118896 — must be unmoved after every
phase, and any movement is a defect to be fixed rather than a baseline to be
updated.

**7. Worst-case complexity and benchmark workload**

Current per-frame complexity in the render path is O(A) for appearance
resolution before the cull, O(A) for the pose-blind bounds construction, O(V)
for the second layout construction over visible pawns, and O(V × E) for the
pulse lookup, where A is total agents, V is visible pawns, and E is live hit
effects.

After Phase 2: O(A) for the cheap conservative pre-cull, O(V) for appearance
lookup and one layout construction, O(E) for the pulse lookup build, and O(V)
for the pulse reads. The O(A) term is reduced from a hash-and-catalog resolution
to an arithmetic rectangle test, and the O(V × E) term becomes O(E) + O(V).

Phase 3 does not change the asymptotic complexity. It changes the constant on
the per-quad submission term, from four 24-byte vertex writes to one 28-byte
instance write.

Benchmark workload: the render probe at 200, 500, and 1,000 units, seed 1, 120
frames per station, three camera stations, Release configuration, vertical
retrace disabled, on a named machine, with the JSON artifact committed. The
canonical gate's own 200-agent / 10,000-tick / seed-1 headless determinism
workload runs unchanged and unaffected.

**8. Spectator explanation: reason code, event, or inspector field**

This is genuinely hard for a backend swap and the honest answer must not invent
a user-visible effect that does not exist.

A spectator **cannot** discover a backend swap by watching the screen, and that
is by design — if they could, the backends would not be equivalent and the
change would be a defect. There is no reason code, no event, and no inspector
field that would be truthful to add, because nothing about the battle has
changed.

What is discoverable, without reading source code:

- The army-composition stepper accepts up to 500 per team where it previously
  accepted 250. This is directly visible in the user interface and is the one
  genuine spectator-facing outcome of the whole design.
- The active backend is named in one session-level debug-log line on the
  `render` channel (section 8.1), so anyone who opens a `Debug` run's JSON Lines
  log can read which backend served that session, and whether the capability
  probe threw.
- The active backend is named in the render-probe fingerprint, so any
  measurement is attributable.

The design deliberately does not surface the backend in the user interface. A
spectator does not benefit from being told which code path drew their battle,
and putting it on screen would imply a choice they do not have and should not
need.

**9. Tests that fail before implementation and pass afterward**

Phase 1: probe-report schema tests for the new Tier 1 spans and the new
fingerprint field, which fail because the fields do not exist. A test asserting
that the cited baseline artifact path resolves to a tracked file, which fails
today because it does not.

Phase 2: for R1, an equivalence test asserting the single-call helper returns
bounds identical to `PawnRenderer.GetBounds` and a layout identical to
`PawnGeometry.Create` — failing before, since the helper does not exist. For R2,
the appearance cache's cold-cache equivalence test, a size-bound test, and a
test asserting that the conservative pre-cull's radius is an upper bound over
every appearance the catalogs can produce. For R3, an equivalence test asserting
the rebuilt pulse lookup matches `GetPulseStrength` for every entity and every
live-effect configuration, including the lethal-exclusion and
maximum-over-effects behaviour. For the cap change, stepper boundary tests at
500.

Phase 3: instance-record pack and unpack round-trip tests asserting exact byte
layout; buffer-sizing and overflow-branch arithmetic tests; instance-count tests
asserting the instanced path produces exactly the quad counts
`PawnQuadCountTests` already pins at 17, 19, 20, and 40; instance-ordering tests
asserting the stream matches the current backend's submission order; and the
amended content-pipeline hygiene test from section 7.1.

What no test can do, stated plainly: none of these execute a shader, a
`GraphicsDevice`, or a draw call, so none of them prove anything appeared on
screen. That is covered by the interactive smoke rows in section 9.2 and by
nothing else.

---

## 12. Decision record

### D1 — Phased, with instancing gated

**Decision:** the design covers the full instanced backend, but the sequencing
is Phase 1 measurement truth, Phase 2 per-agent CPU cost, and Phase 3 instanced
backend built **only if** the Phase 2 re-measurement still misses the target,
with the numeric go/no-go trigger in section 6.2.

**Rationale:** section 1's measurement makes the case for gating, though not for
the reason a first reading of it suggests. The recorded data does show frame
cost scaling with total agent count rather than with drawn quads. It does *not*
show that the instrumented render path is a negligible slice of the frame: that
holds only at the maximum-zoom station, and at the default-fit station the game
ships with, the instrumented path is 96 percent of the frame at 500 units.

The actual reason to gate is that the span which dominates that frame,
`submitMicroseconds`, conflates CPU geometry construction with GPU submission,
so no recorded measurement in this repository can say whether the dominant cost
is Phase 2 work or Phase 3 work. Building the instanced backend first would be
committing the largest change in this document against a number of unknown
composition. Designing it in full, so the decision is informed, while gating it
behind a measurement that first separates those two components, so the decision
is earned, is the combination that respects both the size of the change and the
weakness of the evidence currently supporting it.

The trigger's second clause — that submission must be at least half the frame —
exists because a budget miss alone is not evidence that instancing helps. That
clause is the part of this decision most likely to prevent wasted work, and it
is why GPU-004 is a prerequisite of evaluating the trigger at all: against
today's conflated span the clause would pass on evidence it has not actually
been given.

### D2 — Target 1,000 units total, 500 per team

**Decision:** the target is 1,000 units in total. Not 2,000, not 5,000.

**Rationale:** section 2.2's simulation figures. Between 1,000 and 2,000 agents
the p50 tick cost rises roughly twentyfold and the p95 tick cost reaches 51.5
milliseconds, over three whole 60 Hz frames for a single tick. Past 1,000 the
binding constraint leaves the renderer entirely and becomes collision
resolution, which is another design's problem. Setting the target at 1,000 keeps
the renderer's budget meaningful: it is a number the simulation can actually
feed. A target of 2,000 would have the renderer optimising for frames the
simulation cannot produce, which is work that no spectator could ever perceive.

`Scenario.MaximumAgentsPerFaction` is 10,000, so the simulation imposes no
obstacle at 500 per team and the client cap is the only thing in the way.

### D3 — Keep the `SpriteBatch` fallback, with a latched runtime capability probe

**Decision:** both backends coexist permanently. Backend selection is by a
runtime capability probe whose result latches once per session.

**Rationale:** MonoGame gives no public API for asking whether instancing is
supported — `GraphicsCapabilities.SupportsInstancing` is internal — so the only
supported detection is to attempt a draw and catch
`PlatformNotSupportedException`. Since detection must be empirical, it must also
be failure-tolerant, and a failure-tolerant renderer needs somewhere to fall
back to. The `SpriteBatch` backend is that place, and it has the strong
advantage of being the backend every recorded measurement in the repository was
taken against.

Latching rather than re-probing avoids mid-session backend flapping and avoids
paying for a probe more than once. Retaining the fallback also means this design
never has to define a minimum graphics specification (section 10.3), which is a
promise the project has no hardware matrix and no continuous integration to
keep. The cost is that two backends must be maintained forever, which section
8.11 acknowledges and section 9.1 mitigates by keeping every decision in shared
pure helpers that both backends consume.

