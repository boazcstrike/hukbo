# Attack Animation V2 Implementation Plan

> **Archived: reference only.** Finished work, kept so a past decision can be
> traced to its reasoning. Never execute it, never treat it as current, and never
> cite it as justification for a change. The live contract is `CLAUDE.md`,
> `SIMULATION-GAME-STANDARDS.md`, `docs/development/testing.md`, and `docs/plans/`.

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make every melee contact read instantly and give Kampilan, Wasay, Kalis, and Itak distinct, excellent procedural attack motion without changing authoritative combat or determinism.

**Architecture:** Treat each Core `Attack` event as the already-resolved contact instant. A bounded Client-side dispatcher releases the complete visual and audio contact bundle atomically, while weapon-specific presentation profiles drive contact, recoil, recovery, guard, shield, and combo poses. Rendering uses target-local directions, articulated arms, quality budgets, and motion policy; `Hukbo.Core` remains byte-for-byte untouched.

**Tech Stack:** C# 14, .NET 10, MonoGame, xUnit, repository PowerShell scripts.

---

## Execution contract

- Work only in `C:\Users\boazs\webdev\autonomous-arena-attack-animation-v2` on `codex/attack-animation-v2`.
- Read `docs/plans/2026-08-08-attack-animation-v2-design.md` before coding.
- Follow red-green-refactor for each task: add a focused failing test, run it, implement the smallest complete change, then rerun it.
- Do not edit `src/Hukbo.Core/**` or `tests/Hukbo.Core.Tests/**`.
- Do not change event order, event payloads, simulation cadence, state hashing, or event hashing.
- Keep every contact channel in one bundle: attacker pose, defender reaction, hit/blood/clash, lethal hold, and contact sound.
- Keep new state bounded. Never add a target cache, render state to snapshots, or wall-clock time to Core.
- Use `DiagnosticLog` for overflow visibility; do not write to the console.
- Leave interactive checklist outcomes `PENDING` until a human observes them.
- Commit after each coherent green task using Conventional Commits.

## Baseline oracle already captured

Before implementation, the worktree produced these Release reports:

| Preset | Seed | Outcome | Measured ticks | Event hash | State hash | Deterministic |
| --- | ---: | --- | ---: | --- | --- | --- |
| `PrecolonialPhilippinesV4` | 1 | Faction1Victory | 981 | `AC55684F24D39344` | `1B73FC5923879AA0` | true |
| `PrecolonialPhilippinesV3` | 1 | Faction0Victory | 1097 | `082F98C214611DCF` | `8EA60CC41625DA6E` | true |

Baseline files are local ignored artifacts:

- `artifacts/attack-animation-v2/baseline-v4-seed1.json`
- `artifacts/attack-animation-v2/baseline-v3-seed1.json`

Final verification must regenerate matching reports and compare the stable report fields and ordered event evidence byte-for-byte. A zero diff under `src/Hukbo.Core` is also mandatory.

## Task 1: Add the exhaustive weapon-motion catalog

**Files:**

- Create: `src/Hukbo.Client/Presentation/AttackMotionFamily.cs`
- Create: `src/Hukbo.Client/Presentation/AttackMotionProfile.cs`
- Create: `src/Hukbo.Client/Presentation/AttackMotionCatalog.cs`
- Create: `tests/Hukbo.Client.Tests/Presentation/AttackMotionCatalogTests.cs`

**Step 1: Write failing catalog tests**

Cover all registered `WeaponId` values and pin these mappings:

- Kampilan -> `CommittedCleaver`
- Wasay -> `HeadWeightedChop`
- Kalis -> `LinearThrustCut`
- Itak -> `CompactChopSlash`

Assert that unknown values fail explicitly, not by silently choosing a generic family. Assert that the catalog carries a visual extension envelope, arc, lateral bias, recoil, recovery, hand count, trail eligibility, and shield compatibility as presentation-only data. The visual extension envelope is not Core gameplay reach and must never be copied back into combat authority.

**Step 2: Run the focused test and confirm failure**

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release --filter AttackMotionCatalogTests
```

Expected: failure because the catalog types do not exist.

**Step 3: Implement the immutable exhaustive catalog**

Use an exhaustive switch over `WeaponId`. Do not infer combat properties or modify the Core weapon profiles. Document all named choreography as **Provisional reconstruction** and retain the design's historical qualification for each physical weapon class.

**Step 4: Rerun the focused test**

Expected: all `AttackMotionCatalogTests` pass.

**Step 5: Commit**

```powershell
git add src/Hukbo.Client/Presentation/AttackMotionFamily.cs src/Hukbo.Client/Presentation/AttackMotionProfile.cs src/Hukbo.Client/Presentation/AttackMotionCatalog.cs tests/Hukbo.Client.Tests/Presentation/AttackMotionCatalogTests.cs
git commit -m "feat(combat): classify procedural weapon motion"
```

## Task 2: Introduce a complete bounded contact bundle

**Files:**

- Create: `src/Hukbo.Client/Presentation/AttackContactBundle.cs`
- Create: `src/Hukbo.Client/Presentation/AttackContactDispatcher.cs`
- Create: `tests/Hukbo.Client.Tests/Presentation/AttackContactDispatcherTests.cs`
- Modify: `src/Hukbo.Diagnostics/LogEvents.cs`

**Step 1: Write failing dispatcher tests**

Prove:

- a released record preserves tick, attacker, defender, weapon, combo position, outcome, damage, and lethal status;
- all bundle channels are released together and in stable tick/insertion order;
- each warrior can retain five catch-up contacts;
- a sixth custom contact coalesces into the newest complete bundle and increments an overflow count;
- overflow never leaves orphan pose, effect, reaction, lethal hold, or sound state;
- two landed attacks against one target each retain their own `Attack.Value` contact, while a later aggregate `Damage` event spawns no extra effect;
- a same-tick `Death` attaches lethal hold and death sound exactly once to the highest-sequence landed bundle for that target;
- mutual deaths apply that rule independently per target, including when several ticks arrive in one catch-up update;
- reset clears both pending and latched contacts.

**Step 2: Run the focused test and confirm failure**

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release --filter AttackContactDispatcherTests
```

**Step 3: Implement the bounded dispatcher**

Derive the capacity in comments from the 0.5-second Client clamp, 20 Hz simulation, and two-tick fastest V4 combo. Use fixed arrays or another explicitly bounded representation. Build primary contact channels from each `Attack` event's own resolution and value. Treat `Damage` as aggregate semantics only; attach `Death` once to the target's highest-sequence same-tick landed bundle. Add one stable diagnostic event for whole-bundle overflow and avoid logging in the normal path.

**Step 4: Rerun the focused test and commit**

```powershell
git add src/Hukbo.Client/Presentation/AttackContactBundle.cs src/Hukbo.Client/Presentation/AttackContactDispatcher.cs src/Hukbo.Diagnostics/LogEvents.cs tests/Hukbo.Client.Tests/Presentation/AttackContactDispatcherTests.cs
git commit -m "feat(combat): dispatch bounded attack contacts"
```

## Task 3: Replace event-started swings with contact-latched timelines

**Files:**

- Create: `src/Hukbo.Client/Presentation/AttackAnimation.cs`
- Create: `src/Hukbo.Client/Presentation/AttackAnimationSystem.cs`
- Create: `tests/Hukbo.Client.Tests/Presentation/AttackAnimationSystemTests.cs`
- Remove after migration: `src/Hukbo.Client/Presentation/SwingAnimation.cs`
- Remove after migration: `src/Hukbo.Client/Presentation/SwingAnimationSystem.cs`

**Step 1: Write failing timeline tests**

Pin these contracts:

- ingestion places the weapon at contact on the same frame;
- recovery moves away from contact and eases to a guarded ready pose;
- a combo event two ticks later produces a distinct fresh contact, not a reset to anticipation;
- lethal contacts retain a short presentation hold;
- normal cadence and combo cadence are speed-scaled at 1x, 2x, and 4x;
- the complete 30/60/120 Hz x 1x/2x/4x x Full/Reduced/Off matrix preserves a newly ingested contact until one draw acknowledgment, then advances by equal presentation time without frame-rate-dependent phase changes;
- pause freezes ages and pending latches;
- reset leaves no timeline state.

**Step 2: Confirm the tests fail, then implement**

Represent contact as the timeline origin. Preserve weapon, tick, combo position, attack direction, outcome, and motion policy in animation state. Shorten or omit recovery when another combo contact arrives; never invent a pre-event hit. Drive the 27-cell timing matrix with fixed elapsed values and an explicit draw acknowledgment rather than sleeping or reading wall-clock time.

**Step 3: Run focused tests and commit**

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release --filter AttackAnimationSystemTests
git add src/Hukbo.Client/Presentation tests/Hukbo.Client.Tests/Presentation/AttackAnimationSystemTests.cs
git commit -m "feat(combat): anchor attack motion at contact"
```

Do not remove the old swing files until all consumers migrate in Tasks 4-6.

## Task 4: Build target-local procedural attack geometry

**Files:**

- Create: `src/Hukbo.Client/Rendering/AttackGeometry.cs`
- Create: `src/Hukbo.Client/Rendering/AttackPoseResolver.cs`
- Create: `tests/Hukbo.Client.Tests/Rendering/AttackGeometryTests.cs`
- Create: `tests/Hukbo.Client.Tests/Rendering/AttackPoseResolverTests.cs`

**Step 1: Write failing geometry tests**

For eight target headings, assert the resolved weapon-forward vector has a positive dot product with the attacker-to-target direction at contact. Also prove:

- easing is continuous at phase boundaries;
- strike reach and trail endpoints stay finite;
- left/right combo alternation mirrors lateral bias without reversing target direction;
- shield overlays never move the shield onto the weapon hand;
- all four families produce measurably distinct contact or recovery curves.

**Step 2: Implement local-space curves**

Resolve `atan2(target - attacker)` once per displayed contact, create forward/right basis vectors, evaluate eased local curves, then transform to world space. Avoid simulation-facing floating-point state; these values exist only in Client rendering.

**Step 3: Run focused tests and commit**

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release --filter "AttackGeometryTests|AttackPoseResolverTests"
git add src/Hukbo.Client/Rendering/AttackGeometry.cs src/Hukbo.Client/Rendering/AttackPoseResolver.cs tests/Hukbo.Client.Tests/Rendering/AttackGeometryTests.cs tests/Hukbo.Client.Tests/Rendering/AttackPoseResolverTests.cs
git commit -m "feat(combat): add target-local attack geometry"
```

## Task 5: Prepare atomic contact consumers

**Files:**

- Create: `src/Hukbo.Client/Presentation/DefenderReactionSystem.cs`
- Modify: `src/Hukbo.Client/Audio/SoundDirector.cs`
- Modify: `src/Hukbo.Client/Audio/SoundCueMapper.cs`
- Modify: `src/Hukbo.Client/Presentation/HitEffectSystem.cs`
- Modify: `src/Hukbo.Client/Presentation/BloodEffectSystem.cs`
- Modify: `src/Hukbo.Client/Presentation/ClashEffectSystem.cs`
- Create: `tests/Hukbo.Client.Tests/Presentation/AttackContactIntegrationTests.cs`
- Create: `tests/Hukbo.Client.Tests/Presentation/DefenderReactionSystemTests.cs`
- Modify: `tests/Hukbo.Client.Tests/SoundDirectorTests.cs`
- Modify: `tests/Hukbo.Client.Tests/SoundCueMapperTests.cs`

**Step 1: Write failing atomicity tests**

Use ordered batches containing hit, clash, two landed attacks against one target, aggregate damage, death, and mutual-death variants. Assert a bundle release simultaneously starts the attacker contact pose, defender reaction, appropriate blood/hit/clash effect, optional lethal hold, and contact sound request. Assert the aggregate `Damage` event adds no duplicate contact, and the `Death` event augments only the highest-sequence same-tick landed bundle for its target. Pause on that frame and prove every released or queued contact transient freezes together; only an already-started audio one-shot may finish.

**Step 2: Add bundle-driven consumer entry points**

Create the bounded reaction/lethal-hold store. Add explicit bundle-driven start methods to the attack, hit, blood, clash, reaction, and sound consumers. `SoundCueMapper`/`SoundDirector` must be able to accept atomic contact sound requests while retaining immediate outcome sound. Exercise the whole release through a pure integration fixture, but do not switch the live `PresentationCoordinator` or `ArenaGame` routes yet; Task 6 performs that switch and removal together so no committed revision loses attacks or double-fires contact channels.

**Step 3: Run focused tests and commit**

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release --filter "AttackContactIntegrationTests|DefenderReactionSystemTests|SoundDirectorTests|SoundCueMapperTests"
git add src/Hukbo.Client/Presentation src/Hukbo.Client/Audio/SoundDirector.cs src/Hukbo.Client/Audio/SoundCueMapper.cs tests/Hukbo.Client.Tests/Presentation/AttackContactIntegrationTests.cs tests/Hukbo.Client.Tests/Presentation/DefenderReactionSystemTests.cs tests/Hukbo.Client.Tests/SoundDirectorTests.cs tests/Hukbo.Client.Tests/SoundCueMapperTests.cs
git commit -m "feat(combat): prepare atomic contact feedback"
```

## Task 6: Correct frame ordering and contact-latch lifetime

**Files:**

- Create: `src/Hukbo.Client/Presentation/AttackFrameCoordinator.cs`
- Modify: `src/Hukbo.Client/Presentation/PresentationCoordinator.cs`
- Modify: `src/Hukbo.Client/Audio/SoundDirector.cs`
- Modify: `src/Hukbo.Client/Audio/SoundCueMapper.cs`
- Modify: `src/Hukbo.Client/ArenaGame.cs`
- Modify: `src/Hukbo.Client/ArenaGame.Rendering.cs`
- Create: `tests/Hukbo.Client.Tests/Presentation/AttackFrameCoordinatorTests.cs`

**Step 1: Add a failing update-order regression test**

Use a pure frame coordinator rather than constructing the GPU-backed game. Prove that a contact ingested during the simulation phase is resolved for the immediately following draw, not one frame later. Prove multiple catch-up contacts drain in bounded order, a newly latched contact survives any large elapsed update until `AcknowledgeDraw`, and the acknowledgment advances only after the actual pawn draw phase. Add a coordinator integration assertion that one authoritative attack produces exactly one pose/effect/reaction/sound release after the legacy routes are removed.

**Step 2: Reorder the update path**

Advance eligible presentation clocks, advance simulation/ingest events, dispatch the contact bundle, then resolve attack poses for drawing. Switch `PresentationCoordinator` from independent Attack ingestion to bundle dispatch and remove `ArenaGame`'s direct `_simulation.LastEvents` contact-sound route in the same edit; no second legacy path may remain. Replace `_swingPoses` with the new bounded attack-pose store and pass `AttackPose` through both live pawn-draw call paths in `ArenaGame.Rendering.cs`. Let a lethal-held target bypass the ordinary dead-agent skip only while its bounded hold is active. Call `AcknowledgeDraw` after `DrawPawns` has consumed the pose, never during `Update`. When paused, do not advance contact, defender, blood, hit, clash, or pending-sound clocks. Keep ambient grass behavior unchanged.

**Step 3: Run focused tests and commit**

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release --filter "AttackFrameCoordinatorTests|AttackContactIntegrationTests|SoundDirectorTests|SoundCueMapperTests"
git add src/Hukbo.Client/Presentation/AttackFrameCoordinator.cs src/Hukbo.Client/Presentation/PresentationCoordinator.cs src/Hukbo.Client/Audio/SoundDirector.cs src/Hukbo.Client/Audio/SoundCueMapper.cs src/Hukbo.Client/ArenaGame.cs src/Hukbo.Client/ArenaGame.Rendering.cs tests/Hukbo.Client.Tests/Presentation/AttackFrameCoordinatorTests.cs tests/Hukbo.Client.Tests/Presentation/AttackContactIntegrationTests.cs tests/Hukbo.Client.Tests/SoundDirectorTests.cs tests/Hukbo.Client.Tests/SoundCueMapperTests.cs
git commit -m "fix(combat): draw contacts in their ingestion frame"
```

## Task 7: Render composed stance, articulated arms, and weapon trails

**Files:**

- Modify: `src/Hukbo.Client/Rendering/PawnGeometry.cs`
- Modify: `src/Hukbo.Client/Rendering/PawnRenderer.cs`
- Modify: `src/Hukbo.Client/Rendering/ConservativePawnCull.cs`
- Modify: `tests/Hukbo.Client.Tests/PawnGeometryTests.cs`
- Modify: `tests/Hukbo.Client.Tests/ConservativePawnCullTests.cs`
- Create: `tests/Hukbo.Client.Tests/Rendering/AttackPoseRenderingTests.cs`

**Step 1: Add failing render-geometry tests**

Assert finite, non-degenerate arm segments and weapon quads for all four families, both facings, and allowed shield overlays. Pin draw-layer ordering so the rear arm/weapon, torso, shield, front arm/weapon, and trail do not visually invert. Prove conservative bounds contain every rotated weapon edge, secondary axe head, arm segment, trail segment, shield offset, and defender-reaction offset.

**Step 2: Implement pose composition**

Compose gait/stance with attack offsets rather than replacing the whole pawn pose. Use articulated shoulder-elbow-hand points at Medium and High quality. Trails are geometry driven by the actual recent weapon edge and fade quickly after contact; do not add particles for every swing.

**Step 3: Run focused rendering tests and commit**

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release --filter "PawnGeometryTests|ConservativePawnCullTests|AttackPoseRenderingTests"
git add src/Hukbo.Client/Rendering tests/Hukbo.Client.Tests/PawnGeometryTests.cs tests/Hukbo.Client.Tests/ConservativePawnCullTests.cs tests/Hukbo.Client.Tests/Rendering/AttackPoseRenderingTests.cs
git commit -m "feat(rendering): articulate procedural weapon attacks"
```

## Task 8: Tune distinct outcomes and lethal reactions

**Files:**

- Modify: `src/Hukbo.Client/Presentation/DefenderReactionSystem.cs`
- Modify: `src/Hukbo.Client/Rendering/AttackPoseResolver.cs`
- Modify: `src/Hukbo.Client/Rendering/PawnRenderer.cs`
- Modify: `tests/Hukbo.Client.Tests/Presentation/DefenderReactionSystemTests.cs`

**Step 1: Write failing outcome tests**

Pin small, readable differences for landed, shield-blocked, parried, deflected, evaded, and lethal contact. Reactions must remain target-local, must not move simulation positions, and must return to neutral. A lethal defender remains renderable for the brief hold already wired in Task 6, even when the ordinary live-pawn filter would omit it.

**Step 2: Implement bounded reactions and lethal hold**

Use presentation offsets only. Make defended and clash recoil sharper but lower-displacement than a landed hit. The lethal pose hold should be short, deterministic in presentation time, and cleared on reset.

**Step 3: Run tests and commit**

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release --filter DefenderReactionSystemTests
git add src/Hukbo.Client/Presentation/DefenderReactionSystem.cs src/Hukbo.Client/Rendering/AttackPoseResolver.cs src/Hukbo.Client/Rendering/PawnRenderer.cs tests/Hukbo.Client.Tests/Presentation/DefenderReactionSystemTests.cs
git commit -m "feat(combat): add defender contact reactions"
```

## Task 9: Tune all four families and shield overlays

**Files:**

- Modify: `src/Hukbo.Client/Presentation/AttackMotionCatalog.cs`
- Modify: `src/Hukbo.Client/Rendering/AttackGeometry.cs`
- Modify: `tests/Hukbo.Client.Tests/Presentation/AttackMotionCatalogTests.cs`
- Modify: `tests/Hukbo.Client.Tests/Rendering/AttackGeometryTests.cs`

**Step 1: Add quantitative distinction tests**

Pin qualitative intent with measurable ranges:

- Kampilan: broad committed two-hand cleave, long follow-through, no shield.
- Wasay: compact lift, accelerating head-heavy chop, stronger recoil, no shield.
- Kalis: linear thrust-cut with restrained lateral travel; shield-compatible one hand.
- Itak: short fast chop-slash with alternating combo side; shield-compatible one hand.

Reject shield overlays for incompatible profiles. Ensure solo and shielded Kalis/Itak remain the same base family.

**Step 2: Tune only profile and curve data**

Avoid family-specific conditionals in `PawnRenderer`; distinctions belong in profiles and curve evaluation.

**Step 3: Run the catalog and geometry suites and commit**

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release --filter "AttackMotionCatalogTests|AttackGeometryTests"
git add src/Hukbo.Client/Presentation/AttackMotionCatalog.cs src/Hukbo.Client/Rendering/AttackGeometry.cs tests/Hukbo.Client.Tests/Presentation/AttackMotionCatalogTests.cs tests/Hukbo.Client.Tests/Rendering/AttackGeometryTests.cs
git commit -m "feat(combat): tune weapon-specific attack motion"
```

## Task 10: Apply motion policy, quality tiers, and render budgets

**Files:**

- Modify: `src/Hukbo.Client/Settings/MotionIntensity.cs` documentation only; preserve persisted numeric values
- Modify: `src/Hukbo.Client/ArenaGame.cs`
- Modify: `src/Hukbo.Client/ArenaGame.Rendering.cs`
- Modify: `src/Hukbo.Client/Rendering/PawnRenderer.cs`
- Modify: `src/Hukbo.Client/Rendering/DetailTierGate.cs`
- Modify: `src/Hukbo.Client/Rendering/SubmissionCount.cs`
- Modify: `src/Hukbo.Client/Rendering/RenderProbeSample.cs`
- Modify: `src/Hukbo.Client/Rendering/RenderProbeReport.cs`
- Modify: `tools/Hukbo.Tools.RenderProbe/Program.cs`
- Create: `tests/Hukbo.Client.Tests/Rendering/AttackRenderPolicyTests.cs`
- Create: `tests/Hukbo.Client.Tests/Rendering/AttackRenderAllocationTests.cs`
- Modify: `tests/Hukbo.Client.Tests/PawnQuadCountTests.cs`
- Modify: `tests/Hukbo.Client.Tests/RenderBudgetEstimateTests.cs`
- Modify: `tests/Hukbo.Client.Tests/RenderProbeReportTests.cs`

**Step 1: Write failing policy tests**

Assert:

- Low quality: contact-readable torso/weapon pose only; no articulated arms or trail.
- Medium: articulated arms and short trail for nearby visible contacts.
- High: full permitted articulation/trail, still culled by visibility and distance.
- `Full`: complete allowed stance, counter-motion, arms, trail, and outcome response.
- `Reduced`: lower overshoot, recoil, and trail persistence without hiding the contact pose.
- `Off`: retain the contact communication key, suppress its decorative interpolation and trail.
- Paused: exact retained pose and effects.
- exact active-pawn quad deltas include the new arms, weapon, trail, and reaction bounds;
- 200- and 500-warrior all-active arithmetic stays within the existing arena estimates;
- the post-warm-up 200- and 500-warrior pipeline covering bundle dispatch, frame coordination, reaction storage, pose resolution, attack geometry, and quad counting allocates zero managed bytes.

**Step 2: Implement policy at evaluation/draw boundaries**

Reuse `DetailTierGate` and existing visibility decisions. Extend `PawnQuadCount` in `SubmissionCount.cs` to mirror the exact live branches; do not count from the live renderer. Propagate existing Full/Reduced/Off values without renumbering their persisted enum values. Add a probe-only playback seam guarded by the existing `HUKBO_RENDER_PROBE` opt-in. It starts real simulation playback and delays each station's counted window until live attack poses have appeared; it does not synthesize Core events or alter normal game startup. Extend probe samples/reports with active-attack-pose count and assert every recorded station contains at least one active-contact sample. Do not allocate per pawn per frame. Do not add global hit-stop or camera shake.

**Step 3: Run tests and commit**

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release --filter "AttackRenderPolicyTests|AttackRenderAllocationTests|PawnQuadCountTests|RenderBudgetEstimateTests|RenderProbeReportTests"
git add src/Hukbo.Client/Settings/MotionIntensity.cs src/Hukbo.Client/ArenaGame.cs src/Hukbo.Client/ArenaGame.Rendering.cs src/Hukbo.Client/Rendering/PawnRenderer.cs src/Hukbo.Client/Rendering/DetailTierGate.cs src/Hukbo.Client/Rendering/SubmissionCount.cs src/Hukbo.Client/Rendering/RenderProbeSample.cs src/Hukbo.Client/Rendering/RenderProbeReport.cs tools/Hukbo.Tools.RenderProbe/Program.cs tests/Hukbo.Client.Tests/Rendering/AttackRenderPolicyTests.cs tests/Hukbo.Client.Tests/Rendering/AttackRenderAllocationTests.cs tests/Hukbo.Client.Tests/PawnQuadCountTests.cs tests/Hukbo.Client.Tests/RenderBudgetEstimateTests.cs tests/Hukbo.Client.Tests/RenderProbeReportTests.cs
git commit -m "perf(rendering): bound procedural attack detail"
```

**Step 4: Capture a report-only live render probe**

```powershell
dotnet build tools/Hukbo.Tools.RenderProbe/Hukbo.Tools.RenderProbe.csproj -c Release
& 'tools/Hukbo.Tools.RenderProbe/bin/Release/net10.0/win-x64/Hukbo.Tools.RenderProbe.exe' --matrix 1 120 artifacts/attack-animation-v2/render-matrix.json
```

The apphost launch is mandatory because matrix mode re-invokes `Environment.ProcessPath`. Fail the run if any station reports zero active-attack samples. Record 200- and 500-agent frame percentiles, maximum quads, active-attack samples, and managed bytes against the latest tracked matrix baseline. This naturally occurring battle probe complements, but does not replace, the deterministic 200/500 all-active arithmetic and whole-pipeline zero-allocation test. Treat regressions as blocking until attributed; do not commit the ignored artifact.

## Task 11: Complete migration and document human visual checks

**Files:**

- Remove: `src/Hukbo.Client/Presentation/SwingAnimation.cs`
- Remove: `src/Hukbo.Client/Presentation/SwingAnimationSystem.cs`
- Remove: `src/Hukbo.Client/Rendering/SwingGeometry.cs`
- Remove: `src/Hukbo.Client/Rendering/SwingPoseResolver.cs`
- Remove after their assertions are migrated: `tests/Hukbo.Client.Tests/SwingAnimationSystemTests.cs`
- Remove after their assertions are migrated: `tests/Hukbo.Client.Tests/SwingGeometryTests.cs`
- Remove after their assertions are migrated: `tests/Hukbo.Client.Tests/SwingPoseResolverTests.cs`
- Modify: `docs/development/testing.md`

**Step 1: Search for remaining swing-system consumers**

```powershell
rg "SwingAnimation|SwingGeometry|SwingPoseResolver|\.Swings" src tests
```

Expected before cleanup: only old definitions/tests or migration misses. Fix every real consumer; do not leave parallel attack systems.

**Step 2: Remove obsolete files and add manual checklist rows**

Add `PENDING` rows for:

- each weapon family at 1x, 2x, and 4x;
- hit, defended, clash, combo, and lethal contacts;
- shielded Kalis and Itak;
- Low, Medium, High detail and Full, Reduced, Off motion modes;
- pause on contact and pause with queued catch-up contacts;
- a dense 200-warrior battle at normal and close zoom;
- a 500-warrior stress battle at minimum, default-fit, and maximum zoom, with frame pacing and visual noise reported rather than automatically marked acceptable.

Do not mark these rows `PASS` without human observation.

**Step 3: Run all GPU-independent Client tests and commit**

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release
git add -A src/Hukbo.Client tests/Hukbo.Client.Tests docs/development/testing.md
git commit -m "refactor(combat): retire legacy swing presentation"
```

## Task 12: Prove neutrality, review the complete diff, and run the gate

**Files:**

- Modify only files required to resolve verified Critical or High findings.
- Do not commit generated benchmark output.

**Step 1: Confirm Core is untouched**

```powershell
git diff --name-only 10197eb..HEAD -- src/Hukbo.Core tests/Hukbo.Core.Tests
```

Expected: no output.

**Step 2: Regenerate deterministic reports**

```powershell
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1 -Preset PrecolonialPhilippinesV4 -Output 'artifacts/attack-animation-v2/final-v4-seed1.json'
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1 -Preset PrecolonialPhilippinesV3 -Output 'artifacts/attack-animation-v2/final-v3-seed1.json' -NoBuild
```

Expected stable fields:

- V4: outcome `Faction1Victory`, measured ticks `981`, event hash `AC55684F24D39344`, state hash `1B73FC5923879AA0`, deterministic `true`.
- V3: outcome `Faction0Victory`, measured ticks `1097`, event hash `082F98C214611DCF`, state hash `8EA60CC41625DA6E`, deterministic `true`.

Compare the final reports with the baselines while excluding only documented volatile timing/path fields. If an ordered-event oracle exists in the benchmark report, compare it byte-for-byte; otherwise add a Client-neutral test harness that compares the ordered Core event sequence without changing Core.

**Step 3: Inspect the complete diff**

```powershell
git status --short
git diff --check 10197eb..HEAD
git diff --stat 10197eb..HEAD
git diff 10197eb..HEAD -- src/Hukbo.Client tests/Hukbo.Client.Tests docs/development/testing.md
```

Confirm no temporary diagnostics, allocations in hot loops, unbounded collections, unrelated formatting, or historical claims without qualification.

**Step 4: Request independent review**

The reviewer must classify findings as Critical, High, Medium, or Low. Resolve every Critical and High issue, rerun the affected focused tests, and request re-review. Do not broaden the task for unrelated Medium/Low findings.

**Step 5: Run the canonical gate locally**

```powershell
./scripts/verify.ps1
```

Expected: locked restore, format check, Release build, Core tests, GPU-independent Client tests, and the 200-agent/10,000-tick/seed-1 determinism workload all pass. Record the actual output in the completion report; never substitute a build-only result.

**Step 6: Final worktree status**

```powershell
git status --short --branch
```

Expected: clean feature worktree. Report manual visual checklist rows as `PENDING` until the user or another human runs the game and observes them.

## Objective definition of done

- Every registered weapon maps exhaustively to one of four distinct procedural families.
- Every attack event displays contact in its ingestion frame.
- Attacker pose, defender response, blood/hit/clash, lethal hold, and sound release as one atomic contact bundle.
- Combo contacts remain individually visible under catch-up and at 1x/2x/4x.
- Pause and reset preserve or clear every contact channel consistently.
- Shielded Kalis/Itak and two-handed Kampilan/Wasay obey their hand constraints.
- Low/Medium/High and reduced-motion policies satisfy their pinned budgets.
- No `Hukbo.Core` or simulation-hash behavior changes.
- Focused tests, all Client tests, baseline oracle comparisons, independent review, and `./scripts/verify.ps1` pass.
- Human visual checklist remains honestly `PENDING` until observed.
