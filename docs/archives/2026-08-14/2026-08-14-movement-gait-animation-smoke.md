# Movement gait animation smoke — closed 2026-08-14

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this
project remains `CLAUDE.md` and `docs/development/smoke-checklist.md`; nothing
in this file overrides either of those.

This record lifts the whole of the "Movement gait animation smoke
(2026-08-07)" family from `docs/development/smoke-checklist.md` — fourteen
rows, `GA-1` through `GA-14`, covering the restructured pawn body and the leg
and foot animation the 2026-08-07 gait design added. All fourteen rows were
`PENDING` from the day they were written until 2026-08-14, when a person at an
interactive Windows desktop ran the whole family in one sitting and passed
every row. The family closed fourteen of fourteen and its section was deleted
whole from the live checklist the same day.

| Field | Value |
| --- | --- |
| Rows | 14 |
| Source family | 1 |
| Lifted on | 2026-08-14 |
| Live checklist | `docs/development/smoke-checklist.md` |

## Evidence — 2026-08-14 closing run

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-14 |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

## Movement gait animation smoke

The rows below are reproduced as they stood in the live checklist. No
per-row `Actual` observation was written down for this run; the `Actual`
column is left blank exactly as it was before the run, and the only thing
that changed is the `Status` column, flipped to `PASS` for all fourteen rows.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| GA-1 | Watch one warrior cross open ground at default zoom | The legs visibly alternate and the feet lift and plant; the warrior does not slide | | PASS |
| GA-2 | Compare a warrior closing on a target against one holding position | The moving one steps; the stationary one stands with both feet planted | | PASS |
| GA-3 | Watch a fast advance and a slow one | The fast gait reads as a run — longer stride, higher foot lift, forward lean — not merely as the same walk played faster | | PASS |
| GA-4 | Watch a contingent advance together at default zoom | The warriors do not all step on the same foot at the same moment | | PASS |
| GA-5 | Pause mid-advance | Every leg freezes where it was; nothing keeps moving and nothing snaps to a neutral stance | | PASS |
| GA-6 | Run the battle at 2x and at 4x | The step cadence speeds up with the battle; no warrior appears to skate or to run in place | | PASS |
| GA-7 | Zoom out to the lowest detail tier | Legs and feet disappear cleanly; the pawn falls back to the ground ring with no flicker at the tier boundary | | PASS |
| GA-8 | Zoom in to the highest detail tier | The feet are distinguishable from the legs and read as bare feet | | PASS |
| GA-9 | Set motion to Reduced, then to Off | Reduced keeps the legs moving with a shorter stride; Off leaves the legs drawn and completely still | | PASS |
| GA-10 | Watch a warrior die mid-stride | The corpse does not continue stepping and does not run in place | | PASS |
| GA-11 | Look at any warrior standing still, at default zoom | The restructured body still reads as head, torso, and legs — not as a head on stilts or a torso with stumps | | PASS |
| GA-12 | Watch a shield bearer advance | The shield still reads as covering chest and abdomen, and no swinging leg crosses or hides it | | PASS |
| GA-13 | Watch a warrior attack while moving | The swing and the gait compose without the body jumping between two poses | | PASS |
| GA-14 | Watch a battle at 200 agents from minimum zoom | The formation still reads as a formation; leg motion has not turned the field into noise | | PASS |

## The restructured body, and the two defects fixed before this family could be attempted

This section carries the live checklist's own section preamble over whole,
because it is the only written record of why these fourteen rows sat
`PENDING` for a week and of what had to change before a person could attempt
them at all.

The restructured body is what made this section load-bearing rather than
routine. The torso was shortened from twelve layout units to eight so the legs
could take a real share of the silhouette, which moved the head, the shield,
the armor, the sash, and the adornment accents up by six pixels at the test
fixture's scale. Nothing automated could say whether the result still read as
a warrior; only a person watching the screen could.

Two defects made the family unobservable even after that restructuring, and
both were repaired on 2026-08-13, before this section was first attempted:

- **The default camera fit drew no legs at all.** At the old 1280 × 720
  default window the arena panel was 826 × 640, the camera fit was 0.5682, and
  `apparentScale` reached only 0.7671 — below `MediumDetailScale` — so every
  pawn resolved the Low detail tier, where the leg and foot rectangles are
  empty. Every row that named the default zoom was impossible to observe
  under that build. The default window is now 1600 × 900, which resolves the
  Medium tier at the fit.
- **A closing attacker's stride was effectively frozen.** Stride phase
  advances by distance travelled, and the arrival taper floored a closing
  attacker's step at one raw unit per tick, which worked out to one stride
  cycle every 300 seconds while the warrior was still classified as walking.
  Displacement below a crawl threshold now resolves the neutral stance
  instead.

One consequence for the person who ran this family: the default view is the
Medium detail tier rather than Low, so arms, the armor silhouette, the sash,
and the head treatment were all visible at the default zoom alongside the
legs, and `GA-7`'s lowest detail tier was reached only by zooming out, not by
the camera's starting position.

The same 2026-08-13 change also moves what "the default camera fit" means for
`BR-1`, `BR-4`, and `RG-9` elsewhere in `docs/development/smoke-checklist.md`.
All three were `PENDING` when it landed, so no recorded result was
invalidated by it, and none of the three is part of this record.

## What this pass does and does not prove

The verdict recorded on 2026-08-14 is a pass on all fourteen rows, and
nothing more than a pass. No separate written observation was captured for
any individual row describing what the tester actually saw — no note on
stride length, foot-plant timing, tier-boundary flicker, or how a 200-agent
formation read at minimum zoom. Each row's own criterion was judged satisfied
by the person watching it, and that judgement is the entire evidence this
file carries for each of the fourteen rows.

What is recorded with more confidence is the state of the build the rows were
run against: the Medium detail tier at the default 1600 × 900 window, with
both 2026-08-13 fixes — the camera-fit tier resolution and the arrival-taper
stride floor — already shipped. A later question about a specific gait
behaviour therefore needs a fresh row rather than a reading of this one.

No machine identification, source commit, or screenshot was recorded with the
run, and those fields are left as "Not recorded" in the evidence table above
rather than reconstructed after the fact.

## Where the design documents live

The two documents behind this work stay in `docs/plans/` rather than joining
this archive batch, because both are cited by path from shipped source and
test files. `docs/plans/2026-08-07-movement-gait-animation-design.md` — the
gait design itself, covering the pose mathematics, the leg and foot
rectangles, and the detail-tier gating — is cited from
`src/Hukbo.Client/ArenaGame.cs` and
`src/Hukbo.Client/Presentation/PresentationCoordinator.cs`.
`docs/plans/2026-08-13-gait-default-visibility.md` — the plan that diagnosed
and fixed the camera-fit tier defect and the frozen-stride defect described
above — is cited from `src/Hukbo.Client/Rendering/SubmissionCount.cs` and from
`tests/Hukbo.Client.Tests/DefaultCameraFitDetailTierTests.cs` and
`tests/Hukbo.Client.Tests/PresentationCoordinatorTests.cs`.

They stay live under the rule in `docs/plans/README.md`: a design document
stays in that folder for as long as source or tests cite it by path, however
long ago it shipped, because this archive folder is pruned periodically and a
citation into it would become a broken path.
