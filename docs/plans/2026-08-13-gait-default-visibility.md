# Gait default visibility — plan

Date: 2026-08-13
Base commit: `c15ca63`
Branch: `gait-default-visibility`

This plan answers the question left open in section 6 of
`docs/plans/2026-08-13-strike-while-moving-legibility-design.md` and implements
the answer. That design document diagnosed two causes and deliberately
authorized nothing. Both causes are still true of the build, and both of them
sit between a spectator and the fourteen `GA-*` rows of the movement gait
animation smoke section, which have never been run.

## 1. Why this work exists

The gait animation feature shipped with a stated user-visible outcome: a moving
warrior visibly takes steps, and walking is distinguishable from running at a
glance, without the HUD. At the default launch that outcome is not delivered,
for two independent reasons.

**Cause one — the default camera fit draws no legs.** The client opens at
1280 × 720 (`src/Hukbo.Client/ArenaGame.cs:27-28`). `ComputeLayout` gives the
arena panel 826 × 640 of that. `SpectatorCamera.Fit` takes the smaller axis fit
against the 1280 × 720 map, which is `826 * 0.88 / 1280 = 0.5682`, and
`PawnGeometry.ResolveApparentScale` multiplies by `ZoomScale = 1.35` to reach
`0.7671`. That is below `MediumDetailScale = 0.95`
(`src/Hukbo.Client/Rendering/PawnGeometry.cs:224`), so every pawn resolves
`PawnDetailTier.Low`, and `CreateLegsAndFeet` returns four empty rectangles at
`Low`. A spectator at the default view is watching warriors that have no legs.

**Cause two — in the attack band the stride phase is effectively frozen.**
Stride phase advances by distance travelled, one cycle per
`StrideCycleDistanceRaw = 6000f`
(`src/Hukbo.Client/Presentation/GaitAnimationSystem.cs:75`). A closing attacker
is under the arrival taper, which floors its step at one raw unit per tick
(`Math.Max(1L, ...)` in `src/Hukbo.Core/Movement/MovementRules.cs:517`). One raw
unit per tick is one stride cycle every 6,000 ticks, which at Hukbo's tick rate
of 20 is 300 seconds. Meanwhile `GaitGeometry.ResolveMode` returns `Stance` only
at exactly zero displacement
(`src/Hukbo.Client/Rendering/GaitGeometry.cs:84-101`), so the warrior is
classified as walking, is drawn in a walking pose, and does not visibly walk.

## 2. The decisions taken

**Cause one is answered with a fourth option, not with any of the three the
design document tabled.** Its options were to leave the tier ladder alone and
reword the rows, to draw a reduced leg pair at `Low`, or to move
`MediumDetailScale` below `0.767`. Each was rejected for a specific reason:

- Rewording concedes that a spectator at the default view never sees a warrior
  walk, which abandons the feature's own stated outcome rather than delivering
  it.
- Drawing legs at `Low` contradicts smoke row `GA-7`, which exists to check that
  legs and feet disappear cleanly at the lowest tier, and it adds primitives at
  exactly the tier a 500-warrior battle is watched at. `GA-7` closed `PASS` on
  2026-08-14, recorded in the archived movement gait animation smoke section
  titled "Movement gait animation smoke — closed 2026-08-14"; the reasoning
  above is retained because it is why the constraint exists, not because the
  row is still open.
- Moving `MediumDetailScale` below `0.767` collapses the `Low` tier into a dead
  band. `ResolveApparentScale` clamps its result to a floor of `0.72`
  (`PawnGeometry.cs:222`), so `Low` would survive only across the interval
  `[0.72, 0.767)` — roughly five per cent of the scale range. The tier that
  exists to keep a large battle readable would become almost unreachable, and
  `GA-7` would become nearly impossible to attempt. `GA-7` also closed `PASS`
  on 2026-08-14; this argument is kept because it is why the constraint
  exists, not because the row is still waiting.

**The decision is to raise the client's default window from 1280 × 720 to
1600 × 900.** This moves the default camera fit above the `Medium` threshold
without touching the tier ladder at all. The arithmetic, computed against the
layout constants at `ArenaGame.cs:38-41` (`StatusBarHeight` 68,
`EventPanelWidth` 420, `LayoutMargin` 12, `LayoutGap` 10):

```
contentHeight    = 900 - 68 - 12                 = 820
eventWidth       = min(420, 1600 / 3)            = 420
eventBounds.Left = 1600 - 420 - 12               = 1168
arenaRight       = max(12, 1168 - 10)            = 1158
arenaBounds      = 1146 x 820

horizontalZoom   = 1146 * 0.88 / 1280            = 0.7879   <- the minimum, wins
verticalZoom     =  820 * 0.80 /  720            = 0.9111
apparentScale    = 0.7879 * 1.35                 = 1.0637

1.0637 >= 0.95  ->  PawnDetailTier.Medium  ->  legs and feet are drawn
```

Four properties make this the cheapest correct answer, and each was checked
rather than assumed:

- **The whole map still fits.** The fit is still a fit; the spectator loses no
  part of the battlefield.
- **The `Low` tier stays reachable and stays meaningful.** Legs exist at and
  above `cameraZoom 0.7037`. The default fit is `0.7879`, so zooming out crosses
  back into `Low` well before the zoom floor. `GA-7` remains attemptable exactly
  as written, and it in fact closed `PASS` on 2026-08-14; the point above is
  retained because it explains why the row could be attempted, not because it
  is still pending.
- **The minimum window is unaffected.** At the 1024 × 720 minimum
  (`ArenaGame.cs:29-30`) the arena panel is 649 × 640, the fit is `0.4462`, and
  `apparentScale` clamps to the `0.72` floor — still `Low`, unchanged.
- **The cost is small and measured.** The pinned per-pawn quad counts are
  `Low` 17, `Medium` 23, `High` 24
  (`tests/Hukbo.Client.Tests/PawnQuadCountTests.cs:38`, `:53`, `:67`), computed
  by the production counter `PawnQuadCount.Count` rather than hand-tabulated.
  The default view therefore costs six more quads per pawn for an unshielded,
  unarmoured warrior: two legs, two feet, and the two-quad head treatment that
  `SubmissionCount` also gates off at `Low`.

**Cause two is answered by classifying a sub-threshold crawl as `Stance`.** The
alternative — advancing phase by elapsed time whenever the mode is `Walk` —
would reintroduce exactly the wall-clock dependence the gait design removed on
purpose, and would make the feet skate at the moment the body is barely moving.
A body creeping forward at one raw unit per tick is, to any honest description,
standing. Drawing it standing is both truthful and legible, and it costs
nothing, because the existing per-tick ease-back toward the neutral stance
(`IdleEasePerTick = 0.2f`, `GaitAnimationSystem.cs:83`) already handles the
transition smoothly, so no pop appears at the threshold.

**The threshold is derived, not chosen by eye.** Inventing a constant and
labelling it provisional would be guessing. Instead the threshold is defined by
the legibility criterion it exists to serve: a stride slower than one full cycle
every five seconds is not a stride a spectator can read as walking.

```
CrawlThresholdRawPerTick
  = StrideCycleDistanceRaw / (MaxLegibleStrideCycleSeconds * TickRate)
  = 6000 / (5 * 20)
  = 60 raw units per tick
```

Every constraint on that number holds, and each was verified on disk:

| Constraint | Value | Source |
| --- | --- | --- |
| Must exceed the arrival-taper floor | 1 raw unit per tick | `MovementRules.cs:517` |
| Must leave the pinned walk case a walk | 400 raw units per tick | `GaitGeometryTests.cs:22`, `GaitAnimationSystemTests.cs:42`, `:66`, `:70`, `:102` |
| Must stay below the run threshold | 1600 raw units per tick | `GaitGeometry.cs:39` |
| Full unimpeded speed, for scale | 3,072 raw units per tick | `Scenario.cs:38`, `MovementSpeedRaw = 3 * FixedPoint.Scale`, `FixedPoint.Scale = 1024` |

At 60 raw units per tick a warrior completes one stride cycle in 100 ticks, or
five seconds. At the pinned walk magnitude of 400 it completes one in 15 ticks,
or 0.75 seconds. At the arrival-taper floor of 1 it would have taken 300
seconds, and now resolves `Stance` instead.

## 3. Scope boundary

Presentation only. `Hukbo.Core` is not touched, no simulation field is added, no
tick stage changes, and the state hash, the event hash, and every golden
expectation stay exactly what they are. Both causes live in `Hukbo.Client`, as
the design document established.

The tier ladder is not touched. `MediumDetailScale`, `HighDetailScale`,
`ZoomScale`, `MinimumApparentScale`, and `MaximumApparentScale` all keep their
current values.

`PawnGeometry.cs` is not touched. This matters practically as well as in
principle: it is the file most likely to be under concurrent edit, and the whole
change avoids it.

## 4. Task list

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| T1 | Raise the default window to 1600 × 900 | `src/Hukbo.Client/ArenaGame.cs` | `InitialWindowWidth` is 1600 and `InitialWindowHeight` is 900, with a comment stating the detail-tier reason and citing the fit arithmetic | — | Client suite green; a new test asserting the default fit resolves `Medium` |
| T2 | Clamp the windowed back buffer to the display | `src/Hukbo.Client/Settings/StartupDisplayPolicy.cs`, `tests/Hukbo.Client.Tests/StartupDisplayPolicyTests.cs` | Windowed mode returns `min(windowWidth, displayWidth)` and `min(windowHeight, displayHeight)`; a new test covers a display smaller than the requested window | T1 | New test red before, green after |
| T3 | Assert the default fit resolves `Medium` | `tests/Hukbo.Client.Tests/` (new or existing layout test) | A test computes the arena panel for 1600 × 900, runs `SpectatorCamera.Fit`, and asserts `PawnGeometry.Create` returns `PawnDetailTier.Medium`; a companion asserts 1024 × 720 still resolves `Low` | T1 | Both tests red before T1, green after |
| T4 | Add the crawl threshold to `ResolveMode` | `src/Hukbo.Client/Rendering/GaitGeometry.cs` | `ResolveMode` returns `Stance` below `CrawlThresholdRawPerTick = 60f`, with the derivation in a doc comment | — | Client suite green |
| T5 | Test the crawl threshold | `tests/Hukbo.Client.Tests/GaitGeometryTests.cs`, `tests/Hukbo.Client.Tests/GaitAnimationSystemTests.cs` | Tests cover 1 raw unit resolving `Stance`, 59 resolving `Stance`, 60 resolving `Walk`, 400 still resolving `Walk`, and an ingest at the taper floor easing toward neutral rather than freezing mid-swing | T4 | Red before T4, green after |
| T6 | Close the composed-case pixel floor gap | `tests/Hukbo.Client.Tests/Rendering/AttackPoseRenderingTests.cs` | A test at `Medium` tier asserts a composed attack-plus-walk stride keeps at least one pixel of leg offset, which section 5 of the strike-while-moving design identified as untested | T1 | New test green |
| T7 | Refresh the render budget commentary | `src/Hukbo.Client/Rendering/SubmissionCount.cs` | The estimate comments state that the default view now resolves `Medium` at 23 quads per pawn rather than `Low` at 17, with the 200-unit and 500-unit arithmetic restated | T1 | Client suite green; no pinned count changes |
| T8 | Amend the strike-while-moving design | `docs/plans/2026-08-13-strike-while-moving-legibility-design.md` | Section 6 records option D, the decision, and the reasoning that rejected A, B, and C; the document's status line stops saying its question is unanswered | T1–T7 | Read back |

T1, T4, and T6 are independent and run in parallel. T2, T3, T5, and T7 each
depend on the task above them. T8 is written last, after the gate.

## 5. Verification

The canonical gate, `./scripts/verify.ps1`, is run once after integration and is
not delegated. Its output is recorded verbatim. Both Hukbo suites are run,
because a client-side enum or constant change has reddened the opposite suite in
this repository before.

The baseline for comparison is the run made on this branch before any change:
3,724 tests passed, zero failed, at commit `653d3fa`, since rebased onto
`c15ca63`.

## 6. What this plan does not claim

It does not claim any `GA-*` row passes. Every one of the fourteen rows is a
manual observation, and only a person at an interactive Windows desktop may flip
one. This plan removes two measured obstacles that stand between a spectator and
those rows; it does not substitute for running them.

In particular, nine of the fourteen rows have never been attempted by anyone, so
this work may well surface defects it did not predict. That is the expected
outcome of a first interactive run, not a failure of this plan.

## 7. Rows elsewhere in the checklist that this change touches

Raising the default window changes what "the default camera fit" means for every
row phrased against it. Three rows were affected and all three were `PENDING`
when this landed, so no recorded result was invalidated. All three have since
closed:

- `BR-1` — contingent form-up at the default camera fit. Closed `PASS` on
  2026-08-14, recorded in the archived record titled "Battlefield realism
  cohort smoke — closed 2026-08-14".
- `BR-4` — the two factions' starting deployments at the default camera fit.
  Closed `PASS` on 2026-08-14 in the same record.
- `RG-9` — ranged silhouettes compared across all three detail tiers. `Low`
  remains reachable, so this row is unaffected in substance, but the zoom at
  which the tester finds each tier moves. `RG-9` closed `PASS` on 2026-08-14,
  recorded in the archived ranged units smoke section titled "Ranged units
  smoke — closed 2026-08-14"; this note is retained because it records why
  the row was unaffected, not because the row is still open.

Whoever runs those rows should be told the default view is now `Medium`.
