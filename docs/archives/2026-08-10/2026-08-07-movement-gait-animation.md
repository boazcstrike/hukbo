# Movement gait animation — plan

> **Archived: reference only.** Finished work, kept so a past decision can be
> traced to its reasoning. Never execute it, never treat it as current, and never
> cite it as justification for a change. The live contract is `CLAUDE.md`,
> `SIMULATION-GAME-STANDARDS.md`, `docs/development/testing.md`, and `docs/plans/`.

Design: [`2026-08-07-movement-gait-animation-design.md`](../../plans/2026-08-07-movement-gait-animation-design.md).
Branch: `worktree-movement-animation`, in `.claude/worktrees/movement-animation`,
based on `main` at `8da5538`.

Presentation only. `Hukbo.Core` is not edited by any task in this plan, and no
task may change a state hash, an event hash, a preset version, or a golden
expectation.

## Task list

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| T1 | The gait pose value type and the pure phase-to-pose mathematics: stance/walk/run mode selection from per-tick displacement, stride phase wrapping, stride amplitude, foot lift, forward lean, and the `MotionIntensity` amplitude factor. | `src/Hukbo.Client/Rendering/GaitPose.cs` (new), `src/Hukbo.Client/Rendering/GaitGeometry.cs` (new), `tests/Hukbo.Client.Tests/GaitGeometryTests.cs` (new) | A zero displacement resolves the neutral stance; a walk-magnitude displacement resolves a walk pose; a run-magnitude displacement resolves a longer stride with a forward lean; `MotionIntensity.Off` resolves the neutral stance at every displacement; `Reduced` resolves a strictly smaller amplitude than `Full`. | — | `dotnet test` on `Hukbo.Client.Tests` |
| T2 | The per-entity gait store: previous position and stride phase per `EntityId` at fixed capacity, phase advanced by distance travelled per ingested tick, idle easing back to stance in ticks, the deterministic per-entity phase offset, entity drop on death or absence, `Clear` on round reset, and the draw-loop lookup. | `src/Hukbo.Client/Presentation/GaitAnimationSystem.cs` (new), `src/Hukbo.Client/Rendering/GaitPoseResolver.cs` (new), `tests/Hukbo.Client.Tests/GaitAnimationSystemTests.cs` (new), `tests/Hukbo.Client.Tests/GaitPoseResolverTests.cs` (new) | Ingesting no tick advances no phase; two ingests with identical positions leave the phase easing toward stance; two entities with different `EntityId`s moving identically hold different phases; a dead or absent entity is dropped; the store never grows past its capacity and allocates nothing per ingest. | T1 | `dotnet test` on `Hukbo.Client.Tests` |
| T3 | `PawnLayout` gains left/right leg and left/right foot rectangles, built from the pose-invariant proportions plus the gait pose; the pose-blind path folds in the maximum stride envelope so the cull rectangle stays pose-blind; `CreateRenderedBounds` accounts for the new layers; Low tier produces empty rectangles. | `src/Hukbo.Client/Rendering/PawnGeometry.cs`, `tests/Hukbo.Client.Tests/PawnGeometryTests.cs` | Every pinned rectangle in `PawnGeometryTests` — torso, head, shield, placeholder, ground ring, the `GetBounds` regression rectangle — is unchanged; two different gait phases at one position produce identical `PoseBlindVisualBounds`; Low tier gives empty leg and foot rectangles; Medium and High give non-empty ones. | T1 | `dotnet test` on `Hukbo.Client.Tests` |
| T4 | `PawnRenderer` draws the legs and feet between the ground ring and the torso; `PawnQuadCount` counts the new quads in the same order the renderer submits them; the pinned per-pawn quad counts and the render budget estimate move with the arithmetic stated. | `src/Hukbo.Client/Rendering/PawnRenderer.cs`, `src/Hukbo.Client/Rendering/SubmissionCount.cs`, `src/Hukbo.Client/Rendering/RenderBudgetEstimate.cs`, `tests/Hukbo.Client.Tests/PawnQuadCountTests.cs`, `tests/Hukbo.Client.Tests/PawnRendererTests.cs`, `tests/Hukbo.Client.Tests/RenderBudgetEstimateTests.cs` | The quad count asserted for each pawn configuration equals what the renderer submits for that configuration; the 200-unit and 500-unit budget figures are updated with the per-pawn delta stated in the commit message; no theme role is added. | T3 | `dotnet test` on `Hukbo.Client.Tests` |
| T5 | Wiring: the gait store is constructed, ingested once per tick alongside the other presentation systems, resolved once per frame into a caller-owned buffer, cleared on round reset, given the spectator's `MotionIntensity`, and its pose is passed to `CompletePosedLayout` beside the swing pose. | `src/Hukbo.Client/ArenaGame.cs`, `src/Hukbo.Client/ArenaGame.Rendering.cs`, `src/Hukbo.Client/Presentation/PresentationCoordinator.cs`, `tests/Hukbo.Client.Tests/PresentationCoordinatorTests.cs` | A tick ingest reaches the gait store exactly once; the draw path allocates nothing new per frame; the probe pass and the draw pass still cull identically; changing the motion setting reaches the store. | T2, T3 | `dotnet test` on `Hukbo.Client.Tests` |
| T6 | Manual smoke rows for the feature in the interactive checklist, left `PENDING`. | `docs/development/testing.md` | Rows exist, describe what a person must look at, and are not marked `PASS` by anyone who did not watch the screen. | T5 | Human at an interactive desktop |
| T7 | Canonical gate, run once after integration. | — | `./scripts/verify.ps1` output pasted into the plan's results section. | T1–T6 | `./scripts/verify.ps1` |

## Execution waves

1. **Wave 1** — T1 and T2 together, in one agent. Both are new files and touch
   nothing that exists.
2. **Wave 2** — T3 alone. `PawnGeometry.cs` is a single shared seam; two agents
   in it is a merge conflict created on purpose.
3. **Wave 3** — T4 and T5 in parallel. Their file sets are disjoint.
4. **Wave 4** — T6, then T7 run by the orchestrator, not delegated.

## Verification criteria

- `./scripts/verify.ps1` passes, and its real output is recorded below.
- The seed-1 headless determinism workload reports the same state hash, event
  hash, winner, and event stream as `main` does. This feature cannot change
  them; the run is what proves it rather than the argument.
- No test is weakened, no analyzer is suppressed, and no pinned layout
  rectangle is re-pinned to make a red test green.

## Results

`./scripts/verify.ps1`, run once from the worktree after every task had
integrated. All seven stages passed:

```
[PASS] Platform: Windows x64
[PASS] PowerShell: 7.6.4
[PASS] git version 2.55.0.windows.3
[PASS] Git LFS: installed (optional; no tracked LFS assets are currently required)
[PASS] .NET SDK: 10.0.302
[PASS] MonoGame packages are centrally pinned: MonoGame.Content.Builder.Task 3.8.5, MonoGame.Framework.DesktopGL 3.8.5
[PASS] Required prerequisites and repository configuration are present.
[PASS] Locked package restore completed.
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
[PASS] Release repository tests completed.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

Tests: 2 614 passed in `Hukbo.Core.Tests` and 3 044 passed in
`Hukbo.Client.Tests`, none failed, none skipped. The client suite was 3 023
before this workstream began, so the change added 21 tests.

Determinism, from the seed-1 / 200-agent / 10 000-tick headless workload:

| | Value |
| --- | --- |
| `stateHash` | `1B73FC5923879AA0` |
| `eventHash` | `AC55684F24D39344` |
| `deterministic` | `true` |
| `firstMismatchTick` | `null` |

Both hashes are identical to the recorded seed-1 baseline carried in
`README.md` and `docs/development/testing.md`, which is the evidence — rather
than the argument — that this presentation change reaches no simulation state.

### Task status

| Task | Status |
| --- | --- |
| T1 | Done |
| T2 | Done |
| T3 | Done, then corrected by T8 |
| T4 | Done |
| T5 | Done |
| T6 | Done — rows GA-1 to GA-14 added to the interactive smoke checklist, all `PENDING` |
| T7 | Done — output above |
| T8 | Done — body restructure, added after T3 shipped an unusably small leg |

### T8, added mid-flight

T3 was given a constraint that turned out to be wrong: it was told not to move
any pinned rectangle. The torso runs down to within one layout unit of the foot
anchor, so obeying that constraint forced the entire leg into a one-unit gap.
The result was a leg roughly seven percent of the body's height, two pixels
tall, rounding to zero height at some apparent scales — geometrically correct,
tested, and useless for the thing the feature exists to show.

T8 shortened the torso from twelve layout units to eight and gave the leg band
the four units below it. The leg-plus-foot span is now about a third of the
silhouette. The head, shield, armor, sash, and adornment accents moved up six
pixels at the test fixture's scale, and their pinned rectangles moved with
them; each move was reviewed as deliberate rather than re-pinned to make a red
test green. The ground ring did not move. Stride displacement is three pixels
and foot lift is two pixels at the medium detail tier, both above the
one-whole-pixel floor a spectator needs.

### What is not proven

No row in the `GA-1` to `GA-14` smoke checklist has been run. Nobody has
watched a warrior walk. The automated suite proves the mathematics, the store,
the rectangles, the tier gating, the quad accounting, and the wiring; it cannot
prove that the restructured body reads as a warrior or that the gait reads as
walking. That requires a person at an interactive desktop.
