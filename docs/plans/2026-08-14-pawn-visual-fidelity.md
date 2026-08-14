# Pawn visual fidelity — plan

**This plan was not authorized when it was written.** Section 6 of `CLAUDE.md`
is explicit that a design document does not authorize implementation, and
neither does the plan that follows one.

**Status, 2026-08-14: executed, with one task deliberately withheld.** The user
directed that the pipeline run through to implementation, which is the
authorization the paragraph above was waiting for. Fourteen of the fifteen tasks
are built, verified, and committed on branch `pawn-visual-fidelity`, and PV-14's
integration gate is green for both games — see section 5.

**PV-3 was not executed and is not abandoned.** It is the one task that would
make the battle visibly *less* animated, by removing swing trails at the
`Medium` detail tier for every blow that is not a kill. `Medium` is the tier the
default camera fit resolves, so that change would be seen by every spectator on
every launch, and its only verification is a manual row that would sit `PENDING`
until a person watched it. It was held for an explicit decision rather than
folded in with the rest. Nothing else in this plan depends on it: PV-10 was
sequenced after it only because both touch `PawnGeometry.cs`, and PV-13 wrote
its gait row without the trail row that PV-3 would have justified.

Three further items reached no decision and remain in the design's section 8:
an ordinary-hit hit stop, screen shake, and a projectile double outline. The
design recommends against the first two on the record.

Date: 2026-08-14
Design: [`2026-08-14-pawn-visual-fidelity-design.md`](2026-08-14-pawn-visual-fidelity-design.md),
which outranks this document wherever the two disagree.
Research: [`../research/2026-08-14-pawn-visual-fidelity-research.md`](../research/2026-08-14-pawn-visual-fidelity-research.md).

## 1. Baseline

| | |
| --- | --- |
| Worktree | `.claude/worktrees/pawn-visual-fidelity` |
| Branch | `pawn-visual-fidelity` |
| Base commit | `8ee5a51` |
| Solution | `Hukbo.slnx`, .NET SDK `10.0.302` pinned in `global.json` |

Every file path in this document was confirmed to exist at `8ee5a51` in that
worktree while the plan was written. `main` was at `5f2fabb` at the same moment,
so the branch is behind; rebase before the final gate and prove whose failure any
red result is, rather than assuming it is this branch's.

Another session shares this checkout and has been observed adding foreign
uncommitted files mid-task. **Stage by pathspec. `git add -A` is forbidden in
every task below.**

## 2. Standing constraints on every task

These bind each row in section 4 and are not repeated in the table.

- **Presentation only.** No task edits `src/Hukbo.Core` or
  `src/Hukbo.Shared.Core`. No task moves the state hash or the event hash. If a
  task as written would move a hash, stop and say so loudly; do not re-pin a
  golden value to go green.
- **The client decides nothing.** No task lets presentation decide targeting,
  damage, retreat, or victory.
- Neither Core project may reference `Hukbo.Diagnostics`. No task adds such a
  reference.
- No `Console.Write*` outside the four `Program.cs` entry points. Log through
  `Hukbo.Diagnostics.DiagnosticLog`; any new `ev` identifier is a `const` on
  `LogEvents` under the existing channel prefix. No task below needs a new one.
- **Client tests never construct `ArenaGame`, a `GraphicsDevice`, a
  `SpriteBatch`, or a window**, and never depend on GPU, audio, focus, network,
  or the wall clock. Where a task needs to test code that lives on `ArenaGame`,
  it extracts a pure helper — the pattern the `hukbo-client-ui` skill describes —
  or asserts over source text the way `SourceHygieneTests` already does.
- `TreatWarningsAsErrors` is on and nullable is enabled. Never weaken a test, a
  warning, or an analyzer to reach green.
- No unbounded cache. No derived data in a snapshot.
- **Quad budget is real.** Ceilings are `12_000` at 200 units and `20_000` at
  500, at `src/Hukbo.Client/Rendering/SubmissionCount.cs:626` and `:629`. Any
  task that changes per-pawn or per-screen geometry states its quad delta in its
  commit message.
- Historical accuracy, section 7 of `CLAUDE.md`: legibility tuning is labelled
  `PROVISIONAL` in code comments and never presented as a historical
  measurement. No task below adds or changes a weapon silhouette claim, so no
  evidence tier moves.
- Sprite-frame animation is parked. Everything here stays procedural.
- **No agent may flip a smoke row to `PASS`.** New rows are written `PENDING`.
- Conventional Commits, one commit per task, scoped to that task's files.

## 3. Ordering, and where the seams are

Three files are shared by more than one task, so the tasks that touch them run
in a stated order rather than concurrently.

| Shared seam | Tasks, in order | Why serial |
| --- | --- | --- |
| `src/Hukbo.Client/ArenaGame.Rendering.cs` | PV-1 → PV-9 → PV-11 | Three tasks edit different regions of one 1,200-line file. Two agents in it at once is a merge conflict created on purpose |
| `tests/Hukbo.Client.Tests/RenderBudgetEstimateTests.cs` | PV-2 → PV-8 | Both add a term to the same enforced-budget assertions, and PV-8's total depends on PV-2's |
| `src/Hukbo.Client/Rendering/PawnGeometry.cs` | PV-3 → PV-10 | PV-3 changes the trail gate, PV-10 removes `AttackPose` members that `PawnGeometry` reads |
| `docs/development/testing.md` | PV-0 → PV-6 | PV-0 must run against a clean tree before any task dirties it |

Everything else is disjoint by file and runs in its group concurrently.

## 4. Tasks

### Group P0 — first, alone, before the tree is dirtied

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| PV-0 | The isolated gate receipt the archived lethal blow legibility plan still owes. Confirm the worktree is clean and at `8ee5a51` (`git status --porcelain` empty, `git rev-parse HEAD` matches), run the canonical gate against Hukbo only, and record the real output. If the gate is red for a reason unrelated to lethal blow legibility, that is the finding — record it and stop the package rather than working around it | `docs/development/testing.md` | A new dated subsection holds the verbatim `verify.ps1` output — all five stages, the headless exit code, and the seed-1 state and event hashes — attributed to `8ee5a51` with a clean tree, and states in one sentence that this is the receipt the lethal blow package's task table asked for | — | `./scripts/verify.ps1` with no `-Game` flag, pasted verbatim. Nothing else counts: a build that compiles is not a gate run, and no sub-agent's report substitutes for the output |

### Group P1 — six agents, concurrent, disjoint files

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| PV-1 | Extract the three projectile colours off `ArenaGame` into a pure, testable static class so a Client test can read them without constructing a game. Move `ProjectileShaftColor`, `ProjectileHeadColor`, and `ProjectileFletchColor` (`ArenaGame.Rendering.cs:43,51,58`) and the `ProjectilePropElementKind` switch (`:982-989`) into the new type; `GetProjectileElementColor` delegates. Values unchanged in this task | `src/Hukbo.Client/Rendering/ProjectilePalette.cs` (new), `src/Hukbo.Client/ArenaGame.Rendering.cs` | No colour literal for a projectile remains in `ArenaGame.Rendering.cs`; the existing `PROVISIONAL` doc comments move with the values; drawn output is byte-identical because no value changed | PV-0 | `./scripts/test.ps1 -Configuration Release` — the whole Client suite green, in particular `ProjectileGeometryTests`; plus `git diff` showing the three RGB triples are unchanged across the move |
| PV-2 | Add the embedded pool's term to the enforced budget assertions. Both worst-case tests gain `EmbeddedProjectileSystem.Capacity * RenderBudgetEstimate.EmbeddedProjectileQuadsPerProjectile`, read from the production constants rather than written as `512`. **Stop condition:** if the new total exceeds `12_000` or `20_000`, do not adjust either ceiling and do not weaken the assertion — record the arithmetic and report it as a decision for the user | `tests/Hukbo.Client.Tests/RenderBudgetEstimateTests.cs` | Both `WholeFrameWorstCaseArithmetic_*` tests include the embedded term and their failure messages name it, or the stop condition fired and the arithmetic is reported | PV-0 | `dotnet test tests/Hukbo.Client.Tests --filter RenderBudgetEstimateTests --logger 'console;verbosity=normal'`; the reported totals for 200 and 500 units are pasted into the commit message |
| PV-3 | Raise the swing trail's tier gate from `Medium` to `High`, exempting a lethal blow, which keeps drawing at `Medium`. `CreateSwingTrail` (`PawnGeometry.cs:1842-1875`) currently returns `default` only at `PawnDetailTier.Low`; it now also returns `default` at `Medium` unless the supplied `AttackPose` is lethal. Comment the change as `PROVISIONAL` legibility tuning | `src/Hukbo.Client/Rendering/PawnGeometry.cs`, `tests/Hukbo.Client.Tests/PawnQuadCountTests.cs` | A `Medium`-tier pawn mid-swing with a non-lethal pose counts exactly six fewer quads than before; the same pawn with a lethal pose counts the same as before; `High` and `Low` are unchanged at every existing pinned count | PV-0 | `dotnet test tests/Hukbo.Client.Tests --filter "PawnQuadCountTests|PawnGeometryTests"`; the four existing pinned counts (17, 19, 20, 40) must still hold, and the new cases assert the −6 and the exemption separately |
| PV-4 | Characterization test for the collapsed contact bundle. Drive a sixth pending contact for one attacker so `ReplacePending` (`AttackContactDispatcher.cs:237,277`) fires, and assert one by one which cues the discarded bundle costs — weapon cue, death cue, blood, clash, defender reaction. Also assert the diagnostic line's identifier is `LogEvents.RenderAttackContactCollapsed` with value `render.attackContactCollapsed`, at `dbg`, carrying `attackerId`, `collapsedCount`, `sequence`, `tick`. **Source is not changed.** Name the test so it reads as a record of a known loss, not as desired behaviour | `tests/Hukbo.Client.Tests/Presentation/AttackContactDispatcherTests.cs` | The test fails if any of those five channels starts surviving a collapse, or if the log identifier, level, or payload changes | PV-0 | `dotnet test tests/Hukbo.Client.Tests --filter AttackContactDispatcherTests`; the existing `Ingest_RetainsFivePerAttackerAndCoalescesTheSixthWholeBundle` must still pass unchanged |
| PV-5 | Re-document `ConservativePawnCull` rather than wiring or deleting it. Its own remarks (`ConservativePawnCull.cs:32-37`) say the bound is "a genuine superset, never a replacement" and "nothing here may ever be used as the only cull", so wiring draws the same pawns and cannot close a clipping question. State in the class doc that the type is a mirrored-constants guard today and that the wiring decision belongs to the thousand-unit performance plan; correct section 2 of the attack animation V2 backlog to record that wiring cannot close AA-24, and that the line numbers it cites for the `PawnGeometry` references (2136, 2241) are stale — the real ones are 925, 2243, 2348 | `src/Hukbo.Client/Rendering/ConservativePawnCull.cs`, `docs/plans/2026-08-09-attack-animation-v2-backlog.md` | Both documents name the thousand-unit performance design as the owner of the wiring decision, in prose with its live `docs/plans/` path, and neither still frames "wire or delete" as this package's open question. No code outside doc comments changes | PV-0 | `dotnet test tests/Hukbo.Client.Tests --filter ConservativePawnCullTests` still green (the constants guard is untouched); `git diff --stat` shows the `.cs` change is comments only |
| PV-6 | The leg-motion pixel-height measurement. A GPU-free test computes, for each detail tier boundary (0.95 and 1.80 apparent scale) and each of the three camera stations, the drawn leg height, the peak foot travel (`strideRatio * legLength`), and the peak foot lift (`liftRatio * legLength`) in whole pixels, for Walk and Run. Constants: `LegLengthUnits = 7.5f` (`PawnGeometry.cs:482`), `WalkStrideRatio = 0.32f`, `RunStrideRatio = 0.60f`, `WalkFootLiftRatio = 0.15f`, `RunFootLiftRatio = 0.38f` (`GaitGeometry.cs:63,70,73,80`). **Change no gait constant.** Write the resulting table into `testing.md` as a measurement | `tests/Hukbo.Client.Tests/GaitPixelHeightTests.cs` (new), `docs/development/testing.md` | The table exists in `testing.md` with units on every column and the commit it was measured at; the test recomputes it and fails if any figure drifts | PV-0 | `dotnet test tests/Hukbo.Client.Tests --filter GaitPixelHeightTests`; the table pasted into the commit message must match the test's own expectations exactly |

### Group P2 — four agents, concurrent, disjoint files

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| PV-7 | Retune the three projectile colours until each clears `ContrastEnvelope.MinimumGroundDistance = 60f` against all six shipped ground shades, and pin it. Write the failing test first: mirror the six shades the way `WeaponVisualCatalogTests.cs:31-46` already does, and assert `ContrastEnvelope.IsWithinEnvelope` for each of the three colours. Today's measured minima are shaft 28.2 (Field Manual), head 47.8 (Field Manual), fletch 29.9 (Broadcast) — the test must fail on all three before the retune. Pick new values by search against the metric, not by eye; keep the `PROVISIONAL` labelling. **Do not add an outline** — that is a user decision in the design's section 8 | `tests/Hukbo.Client.Tests/ProjectilePaletteContrastTests.cs` (new), `src/Hukbo.Client/Rendering/ProjectilePalette.cs` | Every one of the eighteen colour-to-shade distances is ≥ 60, the new distance table is recorded in the commit message in the shape of the design's section 5.1 table, and quad delta is zero because only colours changed | PV-1 | `dotnet test tests/Hukbo.Client.Tests --filter ProjectilePaletteContrastTests`, run once before the retune to see it red and once after to see it green, both pasted |
| PV-8 | The whole-screen effects-quad assertion. Sum every effect pool's capacity times its quads-per-item — blood, hit effects, clash effects, dust, trample marks, embedded projectiles — adding a `RenderBudgetEstimate` constant beside `ProjectileQuadsPerProjectile` for any pool that lacks one, each derived from the renderer that draws it. Add the per-pawn and backdrop worst cases the existing tests already build, and assert the total against a new named ceiling. **Compute the sum and report it before choosing the ceiling**; set the constant from the measured worst case with the headroom stated, never below it to force a failure nor above it by an unstated margin | `src/Hukbo.Client/Rendering/SubmissionCount.cs`, `tests/Hukbo.Client.Tests/RenderBudgetEstimateTests.cs` | The new ceiling constant carries a doc comment giving the measured worst case, the headroom, and the date; the test names every pool it summed in its failure message; no runtime governor was added | PV-2 | `dotnet test tests/Hukbo.Client.Tests --filter RenderBudgetEstimateTests --logger 'console;verbosity=normal'`; the measured per-pool breakdown pasted into the commit message |
| PV-9 | Make the probe pass record what the draw path draws. `RecordPawnQuads` (`ArenaGame.Rendering.cs:442`) passes `gaitPose: null` while `DrawPawns` (`:1072`) passes the real pose, so legs and feet — up to four quads per pawn, counted at `SubmissionCount.cs:105-106` — are never recorded. Resolve the same gait pose the draw path resolves and pass it. Correct the comment at `:434-441` that claims the probe mirrors the draw path element for element | `src/Hukbo.Client/ArenaGame.Rendering.cs`, `tests/Hukbo.Client.Tests/PawnGaitQuadParityTests.cs` (new) | No `gaitPose: null` remains in `ArenaGame.Rendering.cs`; the probe resolves the same gait pose the draw path resolves. **This row's original criterion was false and was corrected on 2026-08-14 after measurement.** It predicted that the recorded count for a Medium-tier pawn with a walking gait pose would be strictly greater than for the same pawn with a null pose, by the legs-plus-feet term. The measured delta is zero: `CountLegs` and `CountFeet` count non-empty rectangles and `PawnGeometry` gates those on detail tier alone, so a null pose resolves to `default(GaitPose)`, the standing neutral, which still produces four rectangles at Medium and High. The probe's quad count was never wrong. What was wrong is that it recorded a standing pawn while the screen drew a walking one, which is a positional defect and not a budget under-count | PV-1 | `dotnet test tests/Hukbo.Client.Tests --filter PawnGaitQuadParityTests`. The expectation must be built independently from `PawnQuadCount.Count` on a layout constructed from the pose — **not** by calling the changed path and comparing it with itself; a delegating overload is not an oracle. Plus a source-text assertion that the literal `gaitPose: null` is absent |
| PV-10 | Delete the six `AttackPose` members nothing reads: `Forward`, `Right`, `SupportHand`, `ShieldHand`, `TrailStart`, `TrailEnd` (`AttackPoseResolver.cs:11-27`). Confirmed zero readers under `src/` outside the resolver that fills them — the fifty-nine `.Right` hits are all `Rectangle.Right`. Remove the resolver arithmetic that existed only to produce them; keep as a local any intermediate a surviving member still needs | `src/Hukbo.Client/Rendering/AttackPoseResolver.cs`, `src/Hukbo.Client/Rendering/PawnGeometry.cs`, `tests/Hukbo.Client.Tests/Rendering/AttackPoseResolverTests.cs` | The record has eleven members, the build is clean under `TreatWarningsAsErrors`, and every surviving pose value is bit-identical to before | PV-3 | `dotnet test tests/Hukbo.Client.Tests --filter "AttackPoseResolverTests|AttackGeometryTests|AttackPoseRenderingTests|PawnGeometryTests"` — all pass **unchanged**. If any assertion has to be edited to stay green, a value moved and the diff is wrong |

### Group P3 — two agents, concurrent, disjoint files

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| PV-11 | `AcknowledgeDraw` releases only latches whose pawn was actually drawn, with a bounded expiry so an off-screen attacker cannot hold a latch forever. `DrawPawns` appends each drawn attacker's entity id to a coordinator-owned buffer, pre-sized at the same attacker capacity `AttackContactDispatcher`'s constructor uses (`AttackContactDispatcher.cs:35`) and reused across frames — a fixed array with a count, never a growing collection, never a per-frame allocation. `AttackFrameCoordinator.AcknowledgeDraw` (`:114`) releases a latch only for an attacker in that buffer, or when the animation's age exceeds `MaximumLatchFrames`. Set that bound from the longest legitimate hold in the existing latch tests and state the value in its doc comment. Return the drawn releases and the expired releases distinguishably | `src/Hukbo.Client/Presentation/AttackFrameCoordinator.cs`, `src/Hukbo.Client/Presentation/AttackAnimationSystem.cs`, `src/Hukbo.Client/Presentation/PresentationCoordinator.cs`, `src/Hukbo.Client/ArenaGame.Rendering.cs`, `tests/Hukbo.Client.Tests/Presentation/AttackFrameCoordinatorTests.cs`, `tests/Hukbo.Client.Tests/Presentation/AttackAnimationSystemTests.cs` | A latch for an attacker absent from the drawn buffer survives the frame; the same latch releases once its age passes `MaximumLatchFrames`; the existing `AcknowledgeDraw_ReleasesOnlyDrawnLatchesAndThenAllowsComboContact` still passes; no allocation is added to the draw path | PV-9 | `dotnet test tests/Hukbo.Client.Tests --filter "AttackFrameCoordinatorTests|AttackAnimationSystemTests|AttackContactIntegrationTests"`. Two new cases are required and neither may be satisfied by the other: one proving a culled attacker's latch survives, one proving the expiry releases it |
| PV-12 | Record the four deferrals in the backlog so they are findable without this plan. (a) The collapsed contact bundle's behavioural fix, deferred until the path is observed firing, with PV-4's characterization test named as the precondition. (b) AA-22's first contributor, deferred because the premise is false on disk — arms are gated at `PawnDetailTier.Low` (`PawnGeometry.cs:1380`), not below 1.35 zoom, and `MathF.Max(0.6f, 0.8f * scale)` (`:1398`) already floors a full arm stroke at 1.2 pixels, so it is never sub-pixel. (c) The `ConservativePawnCull` wiring decision, handed to the thousand-unit performance plan. Each entry names the decision that parked it and the document holding its context, matching the file's existing format | `docs/plans/TODO.md` | Three entries exist under a new dated heading, none of them phrased as authorized work | PV-0 | Read back against `docs/plans/TODO.md`'s own convention — every entry names its deferring decision and its context document. No test; this is documentation, and saying so is the honest verification |

### Group P4 — one agent, after the code it describes exists

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| PV-13 | Write the new smoke rows, all `PENDING`. Two rows, in one new subsection, both answerable in a single launch: **PVF-1**, the leg-motion question — at each of the three camera stations, do the legs read as walking at the pixel heights PV-6 measured, and at which station do they stop; **PVF-2**, the trail question — at the default camera fit with 500 units, does the screen read as clearer with ordinary trails gone and only killing blows trailing, or as deadened. Give PVF-1 the measured heights in its `Expected` column so the tester is judging against a number. Recount the file's status column at write time and update its total — the file is edited by other sessions live | `docs/development/smoke-checklist.md` | Both rows are `PENDING` with an empty `Actual`, the subsection preamble names the tasks that created them, and the header count matches a fresh count of the status column | PV-3, PV-6 | A recount of the status column, pasted. **No agent may flip either row.** Only a person at an interactive Windows desktop running `./scripts/run.ps1` may, and a passing automated test is not a substitute |

### Group P5 — integrator only, not delegated

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| PV-14 | Rebase onto `main`, then run the canonical gate once and record it. `main` was at `5f2fabb` when this plan was written and moves; if the gate is red, prove whose failure it is with a detached probe worktree at `main`'s tip before blaming this branch. Fill in section 5 of this document with the real output | `docs/plans/2026-08-14-pawn-visual-fidelity.md` | Section 5 holds verbatim gate output — five stages, headless exit code, seed-1 state and event hashes matching the recorded baseline — and the `Hukbo.Client.Tests` suite is green in the same run | PV-7, PV-8, PV-10, PV-11, PV-12, PV-13 | `./scripts/verify.ps1`, pasted verbatim. **The canonical gate is not delegated** — no sub-agent's report substitutes for this output, and a green default gate is evidence about Hukbo only, never about Sandata |

## 5. What was run

Empty until PV-0 and PV-14 fill it. Nothing in this section may be written from
a sub-agent's summary; only the pasted output of the command counts.

### PV-0 — isolated gate receipt at `8ee5a51`

Run and green. The full output, all five stages and all five headless
workloads, is recorded in `docs/development/testing.md` under the heading
"Canonical gate result — Hukbo, 2026-08-14 (isolated receipt at `8ee5a51`)",
which is where a gate result belongs. It was taken in a dedicated worktree
checked out detached at `8ee5a51`, confirmed clean by `git status --porcelain`
beforehand, with no task from this package yet applied, and it exited 0.

That receipt closes the debt the lethal blow legibility plan left open.

One thing it recorded is worth repeating here, because it changes how any
later "the hashes are unchanged" claim must be phrased: stage five runs **five**
headless workloads on five different preset pairs, not one.

### PV-14 — integration gate

Run and green, exit code 0, after merging `main` at `0851728` into this branch.
The merge was clean, with no conflicts.

**`main` changed the meaning of this gate while the package was being built.**
Commit `2845616` made a bare `./scripts/verify.ps1` run both games rather than
Hukbo alone. The PV-0 receipt above therefore covers Hukbo only, correctly for
the commit it names, while this run covers both. They remain two results and are
reported as two.

```
[PASS] Required prerequisites and repository configuration are present.
[PASS] Locked package restore completed.
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
[PASS] Release repository tests completed.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.   (x5, Hukbo)
[PASS] Release repository tests completed.                            (Sandata)
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.    (Sandata)
[PASS] Canonical repository verification completed.
```

**Hukbo.** All five workloads reported `deterministic: true`, and every one of
the ten digests is byte-identical to the PV-0 baseline taken at `8ee5a51`
before any task in this package landed.

| Combat / movement preset | State hash | Event hash | Against the baseline |
| --- | --- | --- | --- |
| 6 / 4 | `5460D13E3F7FD3E5` | `8E18ED1437B2924B` | unchanged |
| 5 / 8 | `C8023D3B5BEB005E` | `F709A345E2F7370E` | unchanged |
| 5 / 10 | `7C145A9E05916E4C` | `77626E104234206C` | unchanged |
| 5 / 11 | `6225182B4A470F91` | `C4DABE6AF98B6BEC` | unchanged |
| 5 / 13 | `4A0723BC9A1B924B` | `E0CE32CF8830A864` | unchanged |

That is the package's central claim discharged: thirteen commits of rendering,
budget, and latch work moved no authoritative state and no event stream.

**Sandata.** `stateHash A644B7F8A394885D`, `eventHash AEDE4D16B5E6FAAF`,
matching the recorded baseline. No file under `src/Sandata.*` was touched by
this package; Sandata ran only because the gate's default changed.

The Client suite is green at 3,961 tests and the Core suite at 2,568.

## 6. What this plan deliberately does not do

- It does not add screen shake, an ordinary-hit hit stop, or a projectile double
  outline. All three are `NEEDS USER DECISION` in the design's section 8, each
  with a recommendation on the record.
- It does not wire or delete `ConservativePawnCull`. PV-5 records why neither is
  this package's job.
- It does not change a single gait constant. PV-6 measures; nobody tunes until
  PVF-1 is answered by a person.
- It does not touch death collapse, the prone corpse, UI chrome, armor accents,
  trample marks, last-stand engagement, cohort lateral spread, inspector row
  wrapping, or any part of Sandata. Another session owns those files.
- It does not add a runtime effects governor. PV-8 asserts the bound the pools
  already impose; a cap that drops effects when the screen is busiest would
  regress legibility exactly when it matters.
