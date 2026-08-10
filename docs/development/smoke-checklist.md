# Interactive smoke checklist

The manual rows for both games, and **the only place a manual `PASS` may be
recorded.** Split out of `docs/development/testing.md` on 2026-08-11, where it
began 4,082 lines into a 5,708-line file.

Run `./scripts/run.ps1` on an interactive Windows desktop. This repository uses
local-only verification: there is no hosted-CI substitute for this direct
interaction pass.

Every section below except the first is Hukbo's. The Sandata section
immediately below is run with `./scripts/run.ps1 -Game Sandata`.

**Only a person at an interactive desktop may flip a row.** No agent may, for
any reason, including a passing automated test. Compilation, unit tests, a
window-opening probe, and synthetic input do not make a row pass. A row nobody
attempted stays `PENDING`; a row that cannot be attempted is `BLOCKED` with the
reason recorded.

The gate, the current gate results, and the recorded baselines live in
`docs/development/testing.md`. Superseded measurement runs live in
`docs/development/measurement-history.md`.

## Where the checklist stands, 2026-08-11

332 rows across 28 subsections: **294 `PENDING`, 15 `BLOCKED`, 20 `PASS`,
3 `FAIL`**, counted from the status column of this file on 2026-08-11. The
earlier figure here, 105 rows across 29 subsections, did not match the file and
appears to have survived the split out of `docs/development/testing.md`;
recount before trusting any total. The families below are grouped by what a
single launch can actually
show, because the subsections are ordered by the change that created them
rather than by what is on screen at once, and a person working down the file in
order relaunches the game far more often than they need to.

| Batch | Families | Rows | What one launch has to show |
| --- | --- | --- | --- |
| Ranged | `PP` 8, `RG` 11 | 19 `PENDING` | A battle fielding Bangkaw, Busog, and Arquebus warriors. The shipped client runs combat preset V5 and movement preset V8, so ranged units are on the field by default at roughly a 14 per cent share |
| Pawn animation | `AA` 17, `GA` 14 | 31 `PENDING` | Warriors striking and walking, close in. `AA` also holds the one open `FAIL`, AA-22 |
| Markers | `LC` 11, `L` 7 | 18 `PENDING` | Leaders and contingents at default zoom, plus the agent inspector |
| Feed, UI, render | `CL` 9, `GR` 5 | 14 `PENDING` | The event feed, typography, and launch-time render behaviour |
| Sandata | `SD` | 5 `BLOCKED`, 2 `PASS`, 2 `FAIL` | `./scripts/run.ps1 -Game Sandata` |
| Pressure interrupt | `P` | 9 `BLOCKED`, 1 `PENDING` | **Not runnable today** — see below |
| Weapon identity | `V2` | 6 `PASS`, 3 `PENDING`, 1 `BLOCKED` | Run on 2026-08-11. The three `PENDING` rows are re-runs: one waiting on the click-target fix that landed the same day, two rewritten after the run |

**The 15 `BLOCKED` rows are blocked by the build, not by the reader.** Nine `P`
rows need movement preset V7, which the client cannot select: `BuildScenario`
overrides the preset to `RangedStandoffV8` and no preset selector is exposed, so
under the shipped default no pressure mark is ever drawn and no pressure
inspector row ever renders. Unblocking them is a code change, not an attempt.
The `SD` rows are blocked for reasons recorded in the Sandata subsection.
`V2-10` is blocked because the row asks a person to isolate one weapon's sound
in a battle of hundreds.

**Controls, so no row has to be attempted by guesswork.** `Space` plays and
pauses; `1`, `2`, and `4` set playback speed; `R` starts the next round and
`Shift+R` is a full reset; `W`/`A`/`S`/`D` or the arrow keys pan; the mouse
wheel zooms; a left click selects a warrior and opens the agent inspector; `F9`
toggles the sound log.

A row moves to `PASS` only when a person at an interactive desktop has seen the
expected result. No agent may flip one, and a passing automated test is not a
substitute for any row here.

## Sandata smoke (design section 13)

Every row below is `PENDING`, and no agent may flip one. These eight rows are
the complete list of things Sandata's design records as checkable only by a
person at a desktop; the automated suites prove the geometry, the funnel
output, the collapse threshold, the lowered-weapon rule at its exact boundary,
the theme contrast pairs, and the sound-slot lookup, and none of them proves
that any of it reads correctly on a screen.

Run with the debug log on — `./scripts/run.ps1 -Game Sandata -Configuration
Debug` — so that a row recorded `FAIL` or `BLOCKED` can be handed to someone
else with `artifacts/logs/sandata-<utc>-<pid>.jsonl` attached.

**Close the window to end a run. Never kill the process.** `JsonlLogSink` sets
`AutoFlush = false` and the log is flushed when `Program` exits normally, so a
terminated process leaves a zero-byte log file and the whole run's record is
gone.

#### Read this before the first run — 2026-08-10

Until 2026-08-10 the Sandata client never advanced the simulation and drew its
operators from the map's static `SPAWN` records, so nothing on screen could
move under any circumstances. That is fixed: the client now runs
`SandataSimulation.RunTick` on a fixed 20-millisecond timestep, draws every
pawn from live `MissionState`, and gives the assaulting squad an objective to
walk to without being asked. The full record is
`docs/archives/2026-08-11/2026-08-10-sandata-playable-client.md`.

**Controls.**

| Input | Effect |
| --- | --- |
| Space, or the first control-bar button | Play / pause |
| Period (`.`), or the second control-bar button | Advance exactly one tick, pausing first |
| Tab, or the third control-bar button | Cycle speed: half, normal, double, quadruple |
| F5, or the fourth control-bar button | Restart the mission from tick zero |
| Escape | Exit |
| Mouse wheel | Zoom |
| Left-drag on the map | Marquee-select friendly operators |
| Right-click on the map | Add a node to a hand-drawn path |
| Enter | Submit the drawn path to the selected operators |
| Any letter key, released | Submit a go-code release order for the selection |

**What the shipped map does on its own.** `angle-house` spawns two blue
operators at the bottom wall and two red ones on the two yellow objective
squares. The blue pair is one squad — they are 24 world units apart and the
cohesion radius is 96 — and on tick zero it requests a path to the objective at
the top right. Expect them to leave the bottom wall within a second or two,
cross the house through the lower door, and reach the objective at roughly nine
seconds of real time at normal speed. On the run this was written from, the
defender holding that objective was killed at tick 459 and both attackers
survived. The second defender, at the bottom-left objective, is out of range
of the whole route and never does anything.

**An ordered script for a first session.**

1. `./scripts/run.ps1 -Game Sandata -Configuration Debug`. The window opens and
   the map draws. Do not touch anything for fifteen seconds and watch the blue
   pair cross the map. This is the whole game working; if they never move,
   stop and report that before doing anything else.
2. Press Space to pause, then the period key a dozen times, watching one step
   at a time. Press Space again to resume. Press Tab to reach quadruple speed,
   then press it again twice to come back around to half speed.
3. Press F5. The pair returns to the bottom wall and walks the same route
   again.
4. Scroll from the closest zoom out to the furthest, at every stage asking
   whether you can still tell an operator from a piece of cover. **That is
   row SD-1.**
5. While zoomed in, watch the pair cross the long diagonal wall in the middle
   of the map, and then pass through the lower door. **Those are rows SD-2 and
   SD-3.**
6. At each zoom level, look at the yellow fire cones. **That is row SD-6.**
7. Left-drag a box around the blue pair, then right-click three or four points
   across the map, then press Enter. They should abandon the objective route
   and walk your polyline instead.

**What is knowingly not working. Do not spend your session rediscovering it.**

- **No text anywhere.** The client has no font: every HUD panel is an empty
  outline, and the operator inspector, contact list, mission clock, roster
  strip, order queue, and go-code panel are all blank rectangles. **Row SD-8 is
  `BLOCKED` on this** — the inspector it asks you to read does not render a
  single character. There is no on-screen tick counter, no score, and no
  victory banner.
- **The mission never ends.** Nothing in the client checks an outcome; the run
  simply stops at the 36,000-tick limit, about twelve minutes at normal speed.
- **A blocked operator stalls permanently.** If a mover's route runs into a
  body that is standing still, it refuses the step, tries exactly one
  22.5-degree sidestep, refuses that too, and then repeats both refusals for
  the rest of the run. It never re-plans. This is task 89's recorded finding
  and it is expected behaviour today, not a new bug — see
  `src/Sandata.Core/Movement/LocalAvoidance.cs`. On this map with four
  operators it is unlikely but possible.
- **Only one theme is reachable.** `daylight-ops` ships in the theme catalog
  and nothing in the client can switch to it, and there is no unknown-contact
  state to look at either. **Row SD-7b is `BLOCKED` on this. Row SD-7a is not**
  — the friendly-versus-hostile judgement, including the shape-alone half, is
  reachable in `night-ops` and is yours to close.
- **No sound at all**, for the reason recorded under the table below. **Row
  SD-5 is `BLOCKED` on this.**
- **Every operator carries the same placeholder weapon appearance**, so row
  SD-4's rifle-versus-pistol comparison has nothing visible to compare. **Row
  SD-4 is `BLOCKED` on this.**
- **Accuracy is effectively range-only**, so a defender inside sensing range
  is hit reliably. This is a deferred design question, not a defect to report.
- The mission clock in the log stops updating after the last casualty: the
  `boot.sandata.stopped` line reports whatever tick the last
  `sim.sandata.roster` line set, not the tick the run really ended on. The
  roster line's own `t` field is correct.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| SD-1 | Launch, then zoom from the closest tier out to the furthest | The window opens, the map draws, and the operators stay legible at every zoom level | 2026-08-11, tester at the desktop: "there was only 2 of them, so no" — an operator could not be told from a piece of cover at the tiers tried. Ally versus enemy *was* tellable, but by colour alone. The row asks about operator-versus-cover legibility, which is the half that failed. | FAIL |
| SD-2 | Watch a squad path across the 26.57-degree diagonal wall | The funnel path visibly follows the wall as a straight line rather than a staircase | 2026-08-11, attempted: "i am unsure which is which". Investigated after the run and the row cannot be judged by anyone — `SandataGame.DrawOrderPath` renders only `_pathDrawState.Nodes`, the polyline the player is drawing by right-click. No published autonomous group path is drawn anywhere in `Sandata.Client`, so there is no line on screen to call straight or stepped. Becomes executable when the published path is rendered. | BLOCKED |
| SD-3 | Send a squad through the entry door and on into the room behind it | The squad visibly collapses to single file at the door and re-expands inside | 2026-08-11, tester at the desktop: "single file" — the collapse at the door was observed. The re-expansion inside the room was not separately reported, so only the first half of the expected observation is evidenced. | PASS |
| SD-4 | Watch a rifle operator cross a doorway, then a pistol operator cross the same one | The rifle operator lowers the weapon and re-raises it; the pistol operator does not | Cannot be run by anyone: every operator draws the same placeholder weapon appearance, so the two halves of the comparison are visually identical. Becomes executable when per-weapon operator appearances ship. | BLOCKED |
| SD-5 | Hold sustained automatic fire from the maximum operator count | Automatic fire sounds continuous rather than machine-gun-stuttered, and no audio drops out | Cannot be run by anyone: Sandata ships no sound files at all. Becomes executable when the audio generation run is authorized and its slots exist. See the note below the table. | BLOCKED |
| SD-6 | Look at a fire cone at every detail tier, zoomed in and out | The cone reads at every tier and does not fade with zoom | 2026-08-11, tester at the desktop: "readable but not understandable". The row's literal criterion — the cone stays visible at every tier and does not fade with zoom — was met. That it does not communicate *what it means* to a viewer is a real separate finding and is recorded below the table, not folded into this row's status. | PASS |
| SD-7a | View a friendly and a hostile contact side by side in `night-ops`, then judge them again ignoring colour | The two are distinguishable at a glance, and remain distinguishable by shape alone | 2026-08-11, tester at the desktop: distinguishable at a glance, yes — "not distinguishable by shape" with colour ignored. The row requires both halves, and the colour-independent half is the accessibility half, so the row fails. | FAIL |
| SD-7b | View friendly, hostile, and unknown contacts in every shipped theme | All three are distinguishable in `daylight-ops` as well as `night-ops` | Cannot be run by anyone: `LoadTheme` always takes `catalog.DefaultThemeId`, so `daylight-ops` is unreachable from the client, and no unknown-contact state exists to render. Becomes executable when a theme switcher and an unknown-contact state ship. | BLOCKED |
| SD-8 | Click an operator that is holding position | The inspector explains the hold: reason code, path state, and weapon chain phase | Cannot be run by anyone: `Sandata.Client` has no `SpriteFont` and makes no `DrawString` call, so the inspector renders no characters at all. Becomes executable when text rendering ships. | BLOCKED |

**SD-5's blocker in full.** Sandata ships no sound files: its catalog is 106
slots expanding to 524 variant files, roughly 104,800 ElevenLabs credits, and
that spend is not authorized. The row is listed in full so that it is not
quietly forgotten once the audio question is answered.

**Which rows a tester can actually reach today: SD-1, SD-2, SD-3, SD-6, and
SD-7a — five of the nine.** All five are attemptable now that the client runs
the simulation and the assaulting squad walks a real route. The other four are
`BLOCKED`.

**Why those four are `BLOCKED` and not `PENDING`, corrected 2026-08-11.** They
were recorded `PENDING` when the table was written, on the reasoning that the
blocker was upstream of the smoke run rather than something the run
discovered. That reasoning does not survive this document's own rule, stated
for the V7 pressure-interrupt rows in "Why `BLOCKED` and not `PENDING`" later
in this file: *`PENDING` asserts that a check has not been run yet*, and that
assertion is false for a check no person can run at all. Recording SD-4, SD-5,
SD-7b, and SD-8 as `PENDING` misrepresented four impossible checks as four
untried ones, which is precisely the failure that rule exists to prevent.
Each row now names its own blocker and the condition that makes it executable,
in its `Actual` column, so a tester reading only the table learns it there
rather than from prose above it.

**SD-7 was one row and is now two, split 2026-08-11.** Its friendly-versus-
hostile half is reachable in `night-ops` today and its all-themes half is not,
so as a single row it could never be closed and could never be honestly
blocked either. `SD-7a` is the half a tester can finish; `SD-7b` is the half
that waits on a theme switcher and an unknown-contact state. The colour-removed
judgement stays with `SD-7a`, because shape-alone distinguishability is
testable in the one reachable theme.

## First Sandata smoke run — 2026-08-11

The first time a person has run Sandata and reported what they saw. Five rows
were attemptable; the result was two `PASS`, two `FAIL`, and one row that
turned out to be unjudgeable and is now `BLOCKED`. The transport controls,
which no row covers, were confirmed to do what they claim.

Four findings came out of it. None is a regression — all four are things that
were never built, surfaced by the first person to look at the screen.

**1. Gunfire is completely invisible, and it is a dead code path rather than a
missing feature.** `OperatorGeometry` has a muzzle-flash layer, anchored at
`OperatorLayout.WeaponMuzzleAnchor`, gated on an `isFiring` flag.
`SandataGame.cs:1279` supplies that flag as
`operatorState.WeaponChainPhase == (int)WeaponChainPhase.Firing`. That
comparison is **always false**: `WeaponChain`'s own remarks state that
`Firing` "is not a wait: entering it always records one resolved shot and
moves on to `Resetting` within the same pass, so it is never the phase this
method returns." The stored phase on `OperatorState` therefore never holds
`Firing`, the flash never draws a pixel, and no tracer, impact, or hit effect
was ever built to stand in for it. Combined with finding 2 below, a firefight
renders as two shapes drifting together until one stops. The tester read it as
melee combat, which is the correct reading of what is on screen.

**2. Nothing makes an operator stop at weapon range to engage.** A search of
`SandataSimulation` and `Sandata.Core/Squads` for any effective-range,
engagement-range, or stop-to-fire concept returns nothing. `InitialSquadGroups`
sends each assaulting squad to a map objective, a defender is standing on that
objective, and the squad walks to the waypoint. Closing to contact is the
absence of engagement behaviour, not a decision any code makes.

**3. No autonomous path is drawn.** See `SD-2`'s row above.

**4. The fire cone is readable but carries no meaning.** Recorded from `SD-6`.
The cone renders at every tier as the row requires, but a viewer cannot tell
what it represents. This is the section 10 discoverability standard —
*can a spectator discover this effect without reading source code?* — and the
answer today is no. It is not an `SD-6` failure, because `SD-6` asks about
legibility rather than comprehension; it is a gap the row was never written to
catch.

Findings 1, 2, and 3 are each a plain absence with a known fix. Finding 4, and
the `SD-1` and `SD-7a` failures, are the same underlying problem stated three
ways: the client draws untextured primitives with no shape vocabulary, so
everything on screen depends on colour to mean anything.

## Weapon identity and attributes smoke (preset V2)

**Run by a person on 2026-08-11.** Every row was attempted. The automated
tests prove the labels, the profiles, the resolver, the reach floor, and the
panel arithmetic; what this run adds is that an axe does read as an axe on
screen and a shield block is visible at battle scale, that a warrior could not
be clicked at all, and that the six-row composition panel two of these rows
were written against does not exist. See the findings below the table.

`V2-7` and `V2-8` have been **rewritten**. They described a six-weapon-category
composition panel; the panel that ships is the four-rank one, and no code has
ever implemented the other. The rewritten rows describe the panel that exists,
so the two `FAIL` results the original wording produced are recorded in
finding 3 rather than left as rows nobody can ever pass. The six-weapon panel
is deferred, not cancelled — it is a feature nobody has designed yet.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| V2-1 | Watch the battle event feed for one exchange | Attack lines read `Kampilan — Great Blade`, `Wasay — War Axe`, `Kalis — Thrusting Blade (solo)`, `Kalis — Thrusting Blade (shielded)`, `Itak — Work Blade (solo)`, `Itak — Work Blade (shielded)`, with differing damage values | Pair-form labels appeared in the feed and the damage values differed between them | PASS |
| V2-2 | Watch the two-handed weapons in the feed | Neither Kampilan nor Wasay ever carries a `(solo)` or `(shielded)` suffix | The `(shielded)` suffix appeared only on Kalis and Itak lines; no Kampilan or Wasay line carried a suffix. No `(solo)` line was noticed at all during the run — see finding 1 | PASS |
| V2-3 | Click a warrior, then a second of the same weapon and the other grip | The inspector shows the pair label, the evidence tier, the grip, and the three attribute values, and the two differ by one damage and one reach | Attempted 2026-08-11 and `BLOCKED`: no warrior could be selected, because the click target was about five screen pixels wide and sat at the warrior's feet rather than on its body. Fixed the same day by `AgentPickTarget` — needs a re-run, and stays `PENDING` until a person confirms it | PENDING |
| V2-4 | Look at the battlefield at default zoom | Shield bearers are distinguishable from solo warriors of the same weapon without clicking either | Shield bearers were clearly distinguishable | PASS |
| V2-5 | Zoom out to the lowest detail tier | The shield block is still visible; the Wasay is still distinguishable from the Kampilan | Both held at the lowest tier | PASS |
| V2-6 | Compare a Wasay warrior against a Kampilan warrior up close | The Wasay reads as a hafted axe with a distinct head, not as a narrow blade | The Wasay read as an axe | PASS |
| V2-7 | Open the army composition panel | Four stepper rows, one per rank — `Datu`, `Maharlika`, `Timawa`, `Aliping Namamahay` — above a units-per-team row; every row and every button is fully on screen | The 2026-08-11 run saw the four rank rows and reached every button. Rewritten after that run, so it stays `PENDING` until a person confirms the wording it now carries | PENDING |
| V2-8 | Use Distribute Evenly, then Apply, then Full Reset | The battle fields the chosen composition: each rank's count is spread across every combat-preset V5 roster row carrying that rank, so moving the `Timawa` stepper visibly changes how many Kalis, Bangkaw, Busog, and Arquebus warriors take the field | All three buttons worked and the battle fielded what was chosen. Rewritten after that run, so it stays `PENDING` until a person confirms the roster effect this wording now asks for | PENDING |
| V2-9 | Launch with an existing pre-V2 settings file present | Settings reset to defaults without an error dialog or a crash; the composition is the four-rank default | Launched cleanly, no dialog and no crash | PASS |
| V2-10 | Listen during a Wasay attack | The war-axe sound plays; no slot is silent | A wood-chop sound was audible and no slot was silent, but too many warriors were fighting at once to attribute any one sound to a Wasay attack — see finding 4 | BLOCKED |

### Findings from the 2026-08-11 V2 run

**1. No `(solo)` line was seen, and this is an observation rather than a
confirmed defect.** `BattleEventFormatter.GetGripSuffix` returns `solo` for any
one-handed weapon carrying `ShieldId.None`, and the client's own scenario does
field solo rows: `ArenaGame.CalibratedRosterEntryWeights` gives solo Kalis a
weight of 10 against 44 for the whole Timawa group, and solo Itak a weight of 9
against 18 for Aliping Namamahay, so roughly a quarter of Timawa and half of
Aliping Namamahay start the battle without a shield. The suffix should
therefore appear. Nothing in this run proves it does not — the feed retains 200
events and scrolls quickly, and the tester was watching for the two-handed case
the row asks about. Re-run `V2-1` and `V2-2` with the feed paused before
treating this as a bug.

**2. A warrior could not be clicked. Fixed on 2026-08-11.** The click target
was computed in `ArenaGame.SelectAtPointer` as
`MathF.Max(5f / _camera.Zoom, 1.5f)` world units — about five screen pixels —
and it was centred on the agent's own world position, which is the warrior's
foot anchor. A pawn draws entirely *above* that anchor, so the part of a
warrior a spectator aims at was never inside the target at any zoom. Both
halves are now derived from the geometry the renderer actually draws:
`Presentation/AgentPickTarget.cs` samples at the foot anchor rather than at the
cursor, and sizes the target at half the drawn body's height with a
ten-pixel floor, using the same `PawnGeometry.ResolveApparentScale` every pawn
layout length is multiplied by. `AgentPickTargetTests` pins it across the whole
`0.05`–`12` zoom range: a click on the feet, the waist, the chest, or the head
selects the warrior, and a click clear of the body still selects nothing.
`V2-3` is `PENDING` rather than `PASS` because no agent may flip a row — a
person has to click a warrior and see the inspector.

**3. The six-category composition panel was never built; the panel is the
four-rank one.** This was a plan-versus-repository mismatch rather than a
regression. `ArmyCompositionStepper.CategoryCount` is `4`, and
`ArmyCompositionPanel.CategoryLabels` is `Datu`, `Maharlika`, `Timawa`, and
`AlipingNamamahay` — rank names, not weapon pair-form labels.
`Settings.ArmyComposition` carries one slider per rank, and
`ArenaGame.ExpandCompositionToRosterCounts` spreads each rank's slider across
every combat-preset V5 roster row that carries that rank. So the sliders do
move real warriors and `V2-8`'s buttons do work; what does not exist is any
per-weapon control. `V2-7` and `V2-8` have been rewritten against the panel
that ships. Building a genuine six-weapon panel would widen the stepper, change
the persisted settings schema and its reset-on-old-file path, rewire the roster
expansion, and retune the calibrated share weights — a feature needing its own
design document, not a smoke-row fix.

**4. `V2-10` is not judgeable as written.** The row asks the tester to isolate
one weapon's sound in a battle of hundreds of simultaneous attacks. Sound was
present and a wood-chop timbre was heard, but attribution is impossible at
battle scale. Rewrite the row to field a single Wasay pair, or drop it in
favour of the existing sound-gain section.

## Weapon clash smoke (preset V2)

**No interactive run was performed for this change.** Every row below is
`PENDING`. The automated tests prove the resolver, the table coverage, the
event packing, and the blood/label suppression; none of them prove that a
spectator watching the arena can actually tell the five resolutions apart.
Rows marked with a dagger (†) are the ones that decide something about the
design rather than merely confirm it — see design section 3.8 for the
recorded disposition if the void-versus-landed row returns `FAIL`.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| CL-1 | Watch the battle event feed for one exchange of each resolution | The five lines are distinguishable: a damage line for `Landed`, "stopped by the shield" for `ShieldBlocked`, "parried" for `Parried`, "turned aside" for `Deflected`, "stepped off the line" for `Evaded` | | PENDING |
| CL-2 | Watch a shield-blocked, parried, or deflected blow | No blood spray and no impact ring appear for any of the three | | PENDING |
| CL-3 | Watch the clash cross render | It appears for `ShieldBlocked`, `Parried`, and `Deflected`, and for neither `Landed` nor `Evaded` | | PENDING |
| CL-4 † | Distinguish a void from a shield block | An `Evaded` blow (no clash cross, follow-through swing) reads differently on screen from a `ShieldBlocked` blow (clash cross, recoil) without reading the event log | | PENDING |
| CL-5 † | Distinguish a void from a landed blow | An `Evaded` blow (follow-through swing, no blood, no impact ring) reads differently on screen from a `Landed` blow (stops on target, blood, impact ring) without reading the event log | | PENDING |
| CL-6 | Watch any warrior attack | Weapons visibly swing through an arc rather than sitting static during an attack | | PENDING |
| CL-7 | Watch one attack at 1x, then the same weapon at 4x | The swing reads as one countable action at 1x and does not smear into a blur at 4x | | PENDING |
| CL-8 | Compare a `Parried` or `Deflected` blow, a `Landed` blow, and an `Evaded` blow | The clashed blow visibly recoils, the landed blow stops on the target, and the void follows through past it | | PENDING |
| CL-9 | Zoom to high detail, then to low detail, during a swing | The swing arc trail is visible at high zoom and absent at low zoom | | PENDING |
| CL-10 | Pan the camera so a swinging weapon crosses the arena panel edge | A weapon tip may be visibly clipped at the panel edge while panning — this is the accepted cost of the pose-blind frustum cull, not a defect | | PENDING |
| CL-11 | Observe the merged pawn silhouette in motion, both a shield-bearing and a solo warrior | The silhouette under D7 (main's geometry constants plus the clash branch's swing pose applied on top) reads correctly: shield block and swing pose both present, axe head distinguishable from blade, no visual corruption | | PENDING |

## Spectator clarity smoke

Record the observed value in `Actual` and change `Status` only after performing
the interaction. Use `PASS`, `FAIL`, or `BLOCKED`; leave untouched rows
`PENDING`.

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-07-27 |
| Machine/platform | Microsoft Windows 10.0.26200 (Windows 11 Pro) x64 |
| Source commit | `8815a3c`; the later `d6818a8` is documentation-only and builds the identical binary |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

The rows below were observed by the repository owner at an interactive Windows
desktop and reported to the role 17 review, which transcribed them. Only rows
whose **whole** expected observation was exercised are marked `PASS`. Rows 2, 4,
5, and 15 were partly observed: the observed half is recorded in `Actual` and the
row stays `PENDING`, because a row is a single status and half a row is not a
pass. Each of those four names exactly what is still missing, so closing them is
a short follow-up rather than a repeat of the whole pass.

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 1. Launch the game | The window opens, agents render, and the match starts paused with tick unchanged. | Window opened; match started paused with the tick counter sitting still. | PASS |
| 2. Activate Play | The always-visible Play button advances ticks; Space provides the same toggle while the modal is closed. | Play advanced the ticks. The Space toggle was not exercised. | PENDING |
| 3. Activate Pause | The always-visible Pause button stops tick advancement and visibly indicates the paused state. | Pause stopped tick advancement and the paused state was visible on screen. | PASS |
| 4. Open Menu | The always-visible Menu button pauses the match and opens the modal; Escape toggles that same menu behavior. | The Menu button opened the modal. Escape as a toggle was not exercised. | PENDING |
| 5. Exercise modal commands | Modal Play resumes and closes; modal Pause remains open and paused; Escape closes without resuming; Exit Game, which is available only in the modal, requests one clean shutdown. | Exit Game quit the game cleanly. Modal Play, modal Pause, and Escape-closes-without-resuming were not exercised. | PENDING |
| 6. Select an agent | A primary click on a living agent pins the inspector with ID, faction, alive/dead state, health, intent, target, and position. | Not run | PENDING |
| 7. Move away and observe death | Moving the pointer away does not clear selection; if the selected agent dies, the inspector remains pinned and shows its final `DEAD` state. | Not run | PENDING |
| 8. Check observational behavior | Selecting or inspecting an agent does not alter tick progression or the deterministic battle result; an empty-arena click clears selection and UI clicks do not click through. | Not run | PENDING |
| 9. Exercise event-log scrolling | At 1x and 4x, events remain ordered without duplicates and retain at most 200 rows. The wheel scrolls only the log while the pointer is over it and does not zoom the arena; new events do not steal an upward scroll position; returning to the bottom reveals the newest events; over the arena, the wheel zooms. | Not run | PENDING |
| 10. Reach a terminal outcome | The match pauses and the summary winner, both survivor counts, terminal tick, simulated duration, and seed match the final status and visible arena state; the summary offers Next Round. | Not run | PENDING |
| 11. Check score timing and team mapping | Team A is Blue/faction 0 and Team B is Red/faction 1. Reaching a victory does not change the score immediately; choosing Next Round adds exactly one win to that completed round's winner. Starting the next round after a draw or while the current round is ongoing adds no win. | Not run | PENDING |
| 12. Exercise ordinary Next Round | `R`, modal Next Round, and summary Next Round each preserve the score, speed, and camera; clear selection, event history, scroll state, and summary; and leave the fresh round paused. | Not run | PENDING |
| 13. Check seed progression | Each Next Round changes the seed to a distinct deterministic value. After Full Reset, repeating the same Next Round sequence produces the same seed sequence. | Not run | PENDING |
| 14. Exercise Full Reset | After changing the score, speed, and camera, press `Shift+R`; both win totals become 0, seed returns to 1, speed returns to 1x, the camera fits the arena, disposable UI state clears, and the fresh round is paused. Change state again and confirm modal Full Reset has the same result. | Not run | PENDING |
| 15. Close the window | The operating-system close button exits the process once with exit code 0. | Closing the window exited the game. The exit code was not captured, so the `0` half of this row is unproven. | PENDING |
| 16. Check the plains backdrop ground | The battle floor shows varied ground shading with scattered grass, dirt, and stone marks rather than one flat color. | Not run | PENDING |
| 17. Check backdrop stability at zoom extremes | Zooming fully out and fully in keeps the ground pattern locked to the same patches of map; the pattern does not crawl or shimmer, and decals neither vanish into flicker nor balloon into large blobs. | Not run | PENDING |
| 18. Check backdrop continuity while panning | Panning the camera across the map shows no seam lines, gaps, or overlapping bright edges between ground cells. | Not run | PENDING |
| 19. Check readability over the backdrop | Pawn silhouettes, faction ground rings, selection marks, and hit effects all remain clearly readable against the new backdrop. | Not run | PENDING |
| 20. Cycle every theme against the backdrop | Each theme produces a backdrop in its own palette, with the arena border still distinguishable from the ground. | Not run | PENDING |
| 21. Check backdrop reseeding on Next Round and Full Reset | Pressing `R` for a new round changes the backdrop with the new seed; pressing `Shift+R` for a full reset returns the seed-1 backdrop identical to the first launch. | Not run | PENDING |
| 22. Confirm the sound log is hidden by default | On launch, no sound panel is visible and the battle event log occupies the full height of the right column exactly as before. | Not run | PENDING |
| 23. Toggle the sound log | The `Sounds` control-bar button and `F9` both open and close the sound panel; the button shows an active state while it is open; the right column splits with battle events above and the sound log below, and nothing else on screen moves. | Not run | PENDING |
| 24. Check the expected-file list with an empty audio folder | With no files in `Content/Audio/`, the panel lists all thirteen expected file names, each marked `MISSING`, shows `MISSING 13/13`, and the game stays silent without errors. The list scrolls with the wheel, so all thirteen names are reachable even though only ten rows are shown at once. | Not run | PENDING |
| 25. Add one sound file | Drop a PCM WAV named `death.wav` into `Content/Audio/`, relaunch, and confirm that slot reads `READY`, the counter drops to `MISSING 12/13`, and a death audibly plays with a `PLAYED` row in the cue log. | Not run | PENDING |
| 26. Check an unusable file | Replace `death.wav` with a non-PCM file of the same name, relaunch, and confirm the slot reads `FAILED` rather than `MISSING`, and the game still runs silently for that slot. | Not run | PENDING |
| 27. Exercise mute and rate limiting | With files present, the panel's `MUTE` toggle silences playback while still logging rows; during a busy tick the cue log shows collapsed `LIMITED xN` rows rather than one row per suppressed cue. | Not run | PENDING |
| 28. Exercise sound-log scrolling and isolation | The wheel scrolls only the panel under the pointer — sound log, battle log, or arena zoom — and clicks inside the sound panel do not click through to the arena or clear the agent selection. | Not run | PENDING |
| 29. Check sound-log reset behavior | `R` and `Shift+R` clear the cue log while leaving the expected-file list and its statuses unchanged. | Not run | PENDING |
| 30. Open the Army Composition panel | Menu opens and the Army Composition button (between Next Round and Full Reset) shows the currently saved units-per-team and category counts in four steppers. | Not run | PENDING |
| 31. Adjust a category count | Left and Right arrows on a stepper adjust its value; Shift+Left and Shift+Right adjust by 10 instead of 1. The Unassigned readout updates live. | Not run | PENDING |
| 32. Check Unassigned reaches zero | Adjusting steppers such that category sum equals units-per-team displays Unassigned: 0. | Not run | PENDING |
| 33. Verify Apply gate behavior | Apply is disabled (ActionDisabled style, dimmed glyph) while Unassigned != 0 and while the draft equals the saved composition; Apply is enabled exactly when balanced and changed. | Not run | PENDING |
| 34. Check the staged banner | After pressing Apply, the panel closes, the menu shows a one-line notice stating the composition takes effect on the next Full Reset, and Apply remains disabled until a different composition is drafted and applied. | Not run | PENDING |
| 35. Verify Full Reset fields the chosen army | After applying a composition and pressing Full Reset (or `Shift+R`), the arena resets and both factions field the number and distribution of warriors specified by the staged composition, visible in the agent inspector and event log. | Not run | PENDING |
| 36. Observe blood at the default fit view | On first launch, with the default gore setting (Stylized) and the default camera fit, a landed blow shows a directional spray and a ground mark that are both plainly visible without zooming the camera in at all. | Not run | PENDING |
| 37. Check spray direction | Select an agent, watch it get struck, and confirm the spray leaves the victim along the line running from the attacker to the victim — pointing away from the attacker, never back toward it. Confirm this holds for blows arriving from several different directions. | Not run | PENDING |
| 38. Distinguish a lethal blow from a wound | A blow that kills its victim renders visibly differently from a blow that only wounds: the lethal tier is denser or longer-lived, and only the lethal blow leaves the ground mark described in row 39. A spectator can tell the two apart without reading the event log. | Not run | PENDING |
| 39. Check ground-mark persistence and fade | A ground mark stays on the battlefield after the fighters involved have moved away, then fades out gradually over time rather than vanishing in a single frame. Marks accumulate where the fighting was heaviest instead of spreading evenly. | Not run | PENDING |
| 40. Confirm gore Off draws nothing | With the gore setting on Off, no spray, spurt, or ground mark appears anywhere for any blow, including kills, at any camera zoom. The existing warm-white hit-effect ring still draws, so impacts remain readable. | Not run | PENDING |
| 41. Change gore intensity via the menu | Open Menu; the Gore Intensity control cycles Off, Stylized, Full and wraps at both ends using Left and Right and the pointer arrows. Each choice visibly changes blow rendering: Off shows nothing, Stylized shows spray and a fading mark, and Full additionally shows a sustained spurt on a kill together with denser, longer-lived marks. The change takes effect immediately, without a restart. | Not run | PENDING |
| 42. Reach the gore selector by keyboard | Inside the menu, `Tab`, `Down`, and `S` move focus from the theme selector through every button and land on the Gore Intensity selector as the final control in the order; continuing past it wraps back to the theme selector. `Up` and `W` reach it from the theme selector by wrapping backwards. While it is focused, Left and Right change the value and no button is activated. | Not run | PENDING |
| 43. Reach the gore selector by pointer | Hovering the Gore Intensity selector highlights it without changing the value; clicking its previous and next arrows changes the value; and a click on the selector does not click through to the arena or activate any menu button. | Not run | PENDING |
| 44. Check gore intensity persists across a restart | Set gore to Full, fully close the game, and relaunch it: Full is active from the first blow, without reopening the menu. Repeat with Off and confirm the same. | Not run | PENDING |
| 45. Check blood clears on Next Round and Full Reset | With sprays and ground marks visible on screen, trigger Next Round (`R`, modal, or summary); all blood clears immediately alongside the event log, inspector, and summary. Repeat separately with Full Reset (`Shift+R` and the modal command) and confirm the same. | Not run | PENDING |
| 46. Check blood readability across every theme | Cycle all six visual themes while blood is on screen. In every theme, including `datu-court` and `high-contrast`, blood stays clearly distinguishable from the Blue faction pawns, from the Red faction pawns, and from the arena ground surface; no theme makes a spray or a ground mark disappear into a pawn or the backdrop. | Not run | PENDING |
| 47. Check speed and gore independence | At 1x, 2x, and 4x speed, switch gore between Off and Full and confirm the tick counter in the window title advances at the same visible rate for both settings at each speed. The gore setting never slows, pauses, or reorders simulation advancement. | Not run | PENDING |
| 48. Confirm variants resolve | Press `F9`. Every attack slot reports `READY` with a per-class breakdown, and the counts match the files in `Content/Audio/`: 10 for each of the four attack slots, 10 for `death`. A class with no take of its own shows its real count rather than a fallback-inflated one. Scroll the expected-files list to the bottom: each of the four clash slots reports `READY` with four takes, sixteen takes across the four. Each weapon is its own slot, so a clash slot with no take shows its real count and no other weapon's takes are substituted for it. | Not run | PENDING |
| 49. Hear the variation | Watch an unpaused battle for a full minute. Blows do not sound like one repeating sample: cuts to different parts of the body are audibly different, and the same weapon striking the same class does not always play the identical take. | Not run | PENDING |
| 50. Confirm no human voice | Listen through a full battle including many deaths. No cue contains a scream, grunt, groan, or breath. Pay particular attention to `death-02`, `death-06`, and `death-07`, whose prompt wording carries the highest risk of an accidental vocalisation. Any file that vocalises must be regenerated before release. | Not run | PENDING |
| 51. Check level consistency | No cue is obviously louder or quieter than its neighbours. The known-quiet takes — `attack-kampilan-ribcage-01`, `attack-kampilan-gut-01`, `attack-wasay-neck-01`, `death-02` — are audible under a busy battle rather than disappearing. Any that vanish need a re-roll. | Not run | PENDING |
| 52. Verify a partial set falls back | Move one hit class's takes for a single weapon out of `Content/Audio/` and relaunch. That weapon still makes a sound on a hit to that body part, drawn from the fallback class, and the sound log shows the class as missing rather than the whole slot going silent. | Not run | PENDING |

For round scoring, record Team A (Blue) and Team B (Red) totals before and after
each command together with the outgoing outcome and old/new seeds. Next Round
scores only a terminal victory and always advances the deterministic seed.
Full Reset never scores the outgoing round.

## Collision readability smoke

Added by the collision change and revised by the contact-closing amendment.
**Not performed.** Observe one collision-heavy engagement in a live window and
record what was actually seen. The automated gate, the benchmarks, and the
collision regression tests above prove the rule is enforced; none of them prove
the resulting battle line is legible to a person watching it, which is the only
thing these rows are for. The amendment changed what a spectator should expect to
see here, so these rows carry more weight than they did before and none of them
has been observed.

**Amended by the persistent-contingent movement change (T18).** Under
`PersistentContingentsV2` the movement labels rows 19, 20 and 21 read change in
both meaning and frequency. A second-rank agent's blocked label can now read as
gathering toward its contingent rather than purely as blocked by the front
rank, and it can also read as easing to a stop under the arrival taper (design
section 3.6) rather than stopping dead. Row 21's rank-closing observation still
applies, but the closing approach itself now tapers rather than arriving at a
constant rate. Rows 19, 20, 21 and 21a stay `PENDING`; whoever observes them
should not assume the pre-contingent description of what they show still holds
and should record what is actually seen under the new default.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 16. Read the battle line | Agents form a visible front instead of a shapeless blob, and the shape reads as a consequence of crowding rather than as a snapped grid. | Not run | PENDING |
| 17. Look for stacking and jitter | No two living pawns visually occupy the same spot, and a pressed front settles instead of vibrating between positions tick after tick. | Not run | PENDING |
| 18. Confirm combat continues | A packed front keeps dealing damage; the match does not stall into a standoff and reaches a terminal outcome inside its tick limit. | Not run | PENDING |
| 19. Inspect a blocked agent | Selecting an agent in the second rank shows a movement label explaining why it is not advancing, and that label changes as the situation changes. | Not run | PENDING |
| 20. Inspect the front rank | Selecting a front-rank agent shows it moving or attacking rather than blocked, and an agent that has arrived at an enemy reads as attacking rather than still marching. | Not run | PENDING |
| 21. Confirm the ranks actually touch | Opposing front ranks close until their pawn bodies meet, rather than settling with a visible gap of open ground between the two lines. This is the amendment's whole visible effect and the pre-amendment behaviour was a persistent gap. | Not run | PENDING |
| 21a. Watch a contested push change hands | Added by the collision priority amendment. Select a second-rank agent pressed against the same enemy for a sustained engagement. Its movement label alternates between blocked and moving across ticks rather than reading blocked for the whole engagement, and neither faction's line is the one that always gives way. | Not run | PENDING |

## Camera auto-pan smoke

Added by the camera auto-pan change. **Not performed.** The unit tests prove the
targeting and state-machine decisions; only a person watching a live window can
say whether the resulting camera motion is helpful rather than distracting.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 53. Confirm the camera holds still during a visible fight | Zoom in on an engagement so fighting fills the screen. The camera stays exactly where it was left for the whole engagement; it never creeps, drifts, or re-centres on its own while anyone on screen is fighting. | Not run | PENDING |
| 54. Watch the camera find a fight it lost | Zoom in, then pan away until no fighting is on screen. Within a moment the camera slides on its own toward the nearest melee, slows as it arrives, and stops with the fighting comfortably inside the view rather than pinned to an edge. | Not run | PENDING |
| 55. Confirm zoom never changes | Through several auto-pans, the zoom level is exactly what the spectator set. The camera only slides; it never zooms out to find the fight or zooms in on arrival. | Not run | PENDING |
| 56. Take control back | While the camera is auto-panning, hold a pan key. Motion stops under the spectator's hand immediately, the camera goes exactly where they steer it, and it does not resume on its own for a couple of seconds after the key is released. | Not run | PENDING |
| 57. Watch the end of a long battle | Let a match run to its final few survivors at a zoom where they leave the screen. The camera follows the fighting to the end instead of leaving the spectator on empty ground, and it stands still once the match summary appears. | Not run | PENDING |

## Auto camera modes smoke

Added by the auto-camera hysteresis and mode setting, 2026-07-28. **Not
performed.** The unit tests prove the grace, dwell, re-target, and ceiling
decisions against synthetic agent lists; only a person watching a live window
can say whether the camera now feels calm rather than restless. Rows 53 to 57
above remain the baseline behaviour rows and are still `PENDING` too.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 149. Watch a small skirmish without being dragged away | Zoom in on two or three warriors fighting each other, well away from the main battle. The camera stays put for the whole exchange. It does not lurch toward the main battle between blows, which is the defect this change exists to fix. | Not run | PENDING |
| 150. Confirm the camera rests between pans | Pan away from all fighting and let the assistant take over. After it settles on a melee it stays still for a couple of seconds at minimum before any further motion, rather than immediately setting off again. | Not run | PENDING |
| 151. Watch it track a fight that moves | Pan far from a running battle so the assistant starts travelling, and pick a moment when the front is shifting. The camera adjusts its heading mid-journey and arrives at where the fighting is now, not at empty ground the fighting has left. | Not run | PENDING |
| 152. Find the setting in the menu | Open the menu. An `AUTO CAMERA` selector sits below `MOTION INTENSITY`, reads `Assisted` on a fresh install, and cycles `Off`, `Assisted`, `Follow` with the arrows, the mouse, and Left/Right while focused. Every menu control is still fully inside the panel, above the helper line. | Not run | PENDING |
| 153. Confirm `Off` means off | Set the mode to `Off`, close the menu, and pan away from every fight. The camera never moves on its own, for the rest of the match. | Not run | PENDING |
| 154. Confirm `Follow` keeps up | Set the mode to `Follow` and watch a battle. The camera re-centres on fighting noticeably sooner than in `Assisted`, and keeps the melee near the middle of the screen rather than letting it drift to an edge. | Not run | PENDING |
| 155. Confirm the choice survives a relaunch | Set the mode to `Follow`, exit, and relaunch. The menu still reads `Follow` and the camera behaves accordingly from the first tick, without the menu being reopened. | Not run | PENDING |

## Starting deployment smoke

Added by the mirrored starting-formation change. **Not performed.** The
automated evidence proves the arrangement is symmetric, separated and
overlap-free in numbers; none of it proves the opening frame reads that way to a
person watching it, which is the only thing these rows are for.

**Amended by the persistent-contingent movement change (T18).** This section's
premise — that the grouping this checklist describes is only an opening-frame
property — no longer holds under `PersistentContingentsV2`. The deployment
groups these rows describe are now the same contingents `ResolveContingentStates`
carries forward and cycles between gathering and advancing for the rest of the
battle, not a shape that exists only at tick 0 and dissolves on the first move.
Rows 58 through 61 are unchanged and still test the opening frame only; row 61a
below extends the same check past it.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 58. Read the opening frame | Before the armies move, each side reads as several separate groups of warriors rather than one undifferentiated cloud, at the default camera fit and without zooming in. | Not run | PENDING |
| 59. Check the mirror | Pausing at tick 0 and comparing the two halves shows each side as the other's reflection across the centre line: same group positions, same group sizes, same ragged front. | Not run | PENDING |
| 60. Confirm the groups look irregular | Within a group the spacing looks uneven rather than a snapped parade grid, and a new seed visibly reshuffles that spacing without moving the groups. | Not run | PENDING |
| 61. Confirm the armies still meet promptly | The two sides close and fight without a long empty march, and the battle reaches a terminal outcome inside its tick limit. | Not run | PENDING |
| 61a. Confirm the groups stay distinct past deployment | Added by the persistent-contingent movement change. Let the battle run several seconds past the opening frame, well before the armies meet. Each side still reads as several separate groups of warriors at the default camera fit, rather than merging into one crowd as soon as the armies start moving. | Not run | PENDING |

## Typography smoke

Added by the font and text quality change. **Not performed.** The automated
gate proves the ramp is internally consistent, the theme catalog resolves
every role, and text positions round to whole pixels; none of that proves the
resulting text reads as crisp, correctly sized, or correctly hierarchical to a
person watching it, which is the only thing these rows are for.

**Correction — there is no automated em-dash check.** An earlier revision of
this section claimed a "compiled em-dash byte assertion passes". No such
assertion exists. Searching `tests/` for `.xnb`, `CharacterMap`, `2014`,
`8212`, or `em-dash` returns nothing. The only thing backing the em dash is the
second `CharacterRegion` in each of the 24 `.spritefont` files under
`src/Hukbo.Client/Content/Fonts/`, which spans `&#8211;` to `&#8212;` and so
asks the content builder to include the glyph. Whether the builder actually
produced it, and whether the running game draws it instead of throwing, is
verified by row 71 below and by nothing else. That row is `PENDING`.

Per `CLAUDE.md` section 6, only a human at an interactive Windows desktop may
flip one of these rows to `PASS`. Compilation, unit tests, and a
window-opening probe do not.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 62. Glyph crispness at the smallest rung | Event log and sound log rows have solid stems and clean edges, with no grey mush and no ragged stair-stepping. | Not run | PENDING |
| 63. Glyph crispness at the largest rung | The wordmark is sharp at every edge with no fringing. | Not run | PENDING |
| 64. Wordmark hierarchy | The wordmark is unmistakably larger and heavier than the subtitle beneath it. | Not run | PENDING |
| 65. Header face renders as capitals | Every panel header renders fully and unclipped inside its header strip. | Not run | PENDING |
| 66. Mixed-case strings stay on the body face | Theme names, gore levels, the controls label, the winner line, the distribute action, and every inspector line render with real lowercase letters. | Not run | PENDING |
| 67. No vertical clipping | No descender is cut off in any panel at any rung. | Not run | PENDING |
| 68. No horizontal overflow | No label spills past its panel, button, chip, or column, and no ellipsis appears where text previously fit. | Not run | PENDING |
| 69. Row alignment | Event log columns, sound log rows, and inspector rows sit on consistent baselines with no drift down the list. | Not run | PENDING |
| 70. Agent inspector evidence note | The longest evidence note wraps fully inside the panel with nothing cut off. | Not run | PENDING |
| 71. Em-dash regression | Staging an army composition change renders the notice with a real em dash and does not crash. | Not run | PENDING |
| 72. Theme cycling | All six themes render text at the active UI scale with correct contrast, and no theme reveals a clipped or misaligned label the others hide. | Not run | PENDING |
| 73. Window resize and automatic scale tiers | With UI Scale set to Auto, resizing selects 100% at 1280x720, 125% at 1920x1080, 150% at 2560x1440, and 200% at 3840x2160. Each tier stays crisp, re-lays out without clipping, and keeps every menu control visible. | Not run | PENDING |
| 74. Subpixel blur is gone | Panning, zooming, and pausing produce no shimmering or swimming text. | Not run | PENDING |
| 75. Display scaling | Record the appearance at 100% and at 150% Windows scaling. Fed the separate, gated display-scaling measurement task. | The 100% reading was taken during implementation (viewport 1280×720, client bounds 1280×720, equal). The user declined the 150% reading on 2026-07-28, having no use for the display-scaling remedy this row was gating. | DECLINED |

## Responsive menu, startup display, and UI motion smoke

Added by the UI/UX completion work. **Not performed.** Automated layout tests
prove containment and hit-target invariants at representative viewports, but
only a person at an interactive Windows desktop may judge crispness, historical
visual coherence, focus clarity, and motion comfort. Keep these rows `PENDING`
until that observation is performed.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| UI-1. Minimum-size menu containment | At 1024x720 and UI Scale Auto, the complete two-column menu remains inside the window; its 12 controls, labels, arrows, and helper text neither overlap nor clip. | Not run | PENDING |
| UI-2. Common landscape and maximised layouts | At 1280x720, 1920x1080, and the maximised desktop size, the menu stays centred and balanced, the arena HUD remains readable, and no panel covers an unrelated control. | Not run | PENDING |
| UI-3. Tall-window layout | At 1440x1920, the menu and HUD remain contained and readable without stretched text or misplaced pointer hit targets. | Not run | PENDING |
| UI-4. Preferred UI scales and safety cap | Select Auto, 100%, 125%, 150%, and 200%. The selected preference persists after restart; when the viewport is too small for it, the active tier is safely capped while the preferred value remains selected in the menu. | Not run | PENDING |
| UI-5. Windowed startup | Select Windowed, close the game fully, and relaunch. It opens at 1280x720, cannot be resized below 1024x720, and all UI remains contained. | Not run | PENDING |
| UI-6. Fullscreen startup | Select Fullscreen, close the game fully, and relaunch. It opens in soft fullscreen at the current desktop resolution. Select Windowed, restart again, and confirm normal windowed startup returns. | Not run | PENDING |
| UI-7. Keyboard traversal | Open Menu and use Tab, Shift+Tab, W/S, and Up/Down. Focus visits the theme selector, six action buttons, gore, motion, auto camera, UI scale, and startup display exactly once before wrapping. Left/Right changes only the focused selector. | Not run | PENDING |
| UI-8. Motion Off | Select Motion Off. Hover, focus, and press menu and HUD buttons: state changes are immediate, with no animated positional movement, while hit targets remain stable. | Not run | PENDING |
| UI-9. Motion Reduced | Select Motion Reduced. Hover, focus, and press buttons: color transitions remain gentle, no control shifts position, and the setting takes effect immediately. | Not run | PENDING |
| UI-10. Motion Full | Select Motion Full. Hover and press buttons: transitions ease smoothly and a pressed control moves by no more than one active-scale pixel without changing its clickable bounds. | Not run | PENDING |
| UI-11. Cebu 1521 Court theme | Select `Cebu 1521 — Provisional` and confirm the selector label reads `PROVISIONAL RECONSTRUCTION`. The restrained dark hardwood, woven-fibre, warm metal, soot-black, and textile-red palette reads as a provisional early-contact chiefly-court interpretation rather than a generic European-medieval or modern national design; text and faction signals remain legible. | Not run | PENDING |
| UI-12. Battle event log new-event accent | With a battle running and the event log panel visible, let a new event append while the log is on screen. At Motion Off, the new row's text renders in its final new-event accent colour immediately, with no colour fade. At Motion Reduced and Motion Full, the row's text eases from the new-event accent colour back toward the normal text colour over roughly 200 ms, and the two intensities look identical to each other. Row order, row height, and every other row are unaffected at every intensity. | Not run | PENDING |
| UI-13. Selected-agent inspector accent | Select an agent, open the agent inspector, then select a different agent while the inspector stays open. At Motion Off, the inspector's accent updates to the newly selected agent immediately, with no colour fade. At Motion Reduced and Motion Full, the accent eases in from the emphasis colour over roughly 160 ms before settling, and the two intensities look identical to each other. Re-selecting the agent that is already selected does not retrigger the accent. | Not run | PENDING |
| UI-14. Selector arrow and active-marker interpolation | In the menu, hover the pointer over a selector's previous and next arrows (theme, gore, motion, auto camera, or UI scale) and change the selector's value. At Motion Off, the hovered arrow's highlight and the active-value marker snap instantly with no fade, and hit targets are unaffected. At Motion Reduced and Motion Full, the hovered arrow eases toward its highlighted colour and the marker eases toward its emphasis colour over the selector's pulse duration, and the two intensities look identical to each other. Moving focus without changing the selector's value does not retrigger the marker pulse. | Not run | PENDING |
| UI-15. Control-bar active strip | On the control bar, toggle play/pause and change the simulation speed. At Motion Off, each button's active strip snaps instantly to its active colour at its existing six-pixel width. At Motion Reduced and Motion Full, the strip's colour eases from the inactive border colour toward the active colour over roughly 120 ms when a button becomes active, and eases back when it deactivates; the two intensities look identical to each other. The strip's width and the button's hit target never change at any intensity. | Not run | PENDING |
| UI-16. Status-badge emphasis (one-shot, non-looping) | Cause the battle outcome to change, or toggle play/pause so the playing flag changes, and watch the status badge for several seconds afterward. At Motion Off, the badge's fill snaps to the new state's colour immediately with no pulse. At Motion Reduced and Motion Full, the badge briefly pulses toward its emphasis colour and settles back within roughly 450 to 650 ms, and the two intensities look identical to each other; the badge does not pulse again on its own while the state stays unchanged. Toggling the same state change again triggers a fresh, single pulse each time. | Not run | PENDING |

## Last-stand formation smoke

Added by the last-stand formation change. **Not performed.** The automated
tests prove the trigger, the rally-agent choice, the deterministic offset, the
trail distance, the give-way rule, and that a last stand still resolves inside
the tick limit. None of them prove that the resulting endgame reads as a
converging last stand rather than as warriors wandering, which is the only
thing these rows are for. Only a human running `./scripts/run.ps1` on an
interactive Windows desktop may flip one of these rows to `PASS`. Compilation,
unit tests, and a window-opening probe do not.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 76. Watch the endgame converge | Let a full 200-agent battle run to its final handful of warriors on each side. As each side thins out, its survivors visibly turn toward one another and gather instead of continuing to spread across the map. | Not run | PENDING |
| 77. Confirm the cluster is irregular | The gathered survivors form a ragged clump. They do not form a ring, a grid, a line, an arc, or any shape that looks placed. No warrior sits at an obviously exact distance from the one it gathered on. | Not run | PENDING |
| 78. Confirm the cluster advances as a body | The gathered survivors travel toward the enemy together rather than one at a time. The group arrives roughly at once, and the fight that follows is a group fight rather than a sequence of separate duels. | Not run | PENDING |
| 79. Watch a leader fall | When the warrior the group has gathered on is killed, the group re-forms on another warrior within a moment. The re-form is a short, small adjustment, not a sudden jump across the screen or a scatter. | Not run | PENDING |
| 80. Inspect a regrouping warrior | Selecting a survivor that is closing on its comrades shows `Intent: Regrouping` in the inspector, and the battle event log shows its movement naming the warrior it is closing on rather than an enemy. The intent changes to `Attacking` once it is actually swinging at an enemy. | Not run | PENDING |
| 81. Confirm regrouping never stops the fight | A warrior that is regrouping still strikes any enemy it passes within reach. The final engagement is not delayed by warriors refusing to fight while they are still gathering, and the match reaches a terminal outcome rather than two clusters standing apart. | Not run | PENDING |

## Sound gain compensation smoke

Covers the change recorded in
the sound gain compensation plan. The measured evidence is in
`docs/research/SOUND-CAPACITY-MEASUREMENTS.md`; these rows are the part that
only a person with working speakers can settle.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 82. Hear a busy melee without distortion | Let a 200-agent battle reach its densest fighting at normal speed. Blows stay individually distinguishable. There is no continuous rasp, crackle, or buzz underneath the fighting, and no moment where the sound seems to break up or drop out. | Not run | PENDING |
| 83. Compare a duel with a melee | The final one-on-one survivors sound clearly louder per blow than the same weapon does in the middle of the melee. The change is gradual as the fight thins out, not a sudden jump. | Not run | PENDING |
| 84. Watch the voice count and gain react | Open the sound log with `F9`. During heavy fighting `VOICES` climbs into the tens and `GAIN` falls well below 0.65; as the battle thins both recover, and `GAIN` returns to `0.65` once nothing is sounding. | Not run | PENDING |
| 85. Confirm nothing is being limited | Through a full 200-agent battle at normal speed, the sound log shows no `LIMITED` row and no `REFUSED` row. | Not run | PENDING |
| 86. Check 4x speed | At 4x the audio stays clean and undistorted, `VOICES` climbs higher than at 1x, and `GAIN` falls further. Still no `LIMITED` or `REFUSED` rows. | Not run | PENDING |
| 87. Confirm mute still works | Toggling `MUTE` silences everything immediately and unmuting resumes without a burst of backed-up sound. | Not run | PENDING |
| 88. Confirm a new round starts at full gain | After a match ends and a new one starts, the first blow of the new battle is at full volume rather than carrying the previous battle's reduction. | Not run | PENDING |
| 89. Confirm the header stays readable | The `VOICES n GAIN 0.nn` text in the sound log header does not overflow its panel, overlap the `MUTE` button, or clip at any of the six themes. | Not run | PENDING |

## Tactical hit animations smoke

Covers the change recorded in
the tactical hit animations plan, whose Task 6 requires a
manual checklist that this document was previously missing. **Not performed.**
`HitEffectSystemTests.cs` and `HitEffectGeometryTests.cs` prove that the effect
buffer has a fixed capacity and replaces its oldest entry in a defined order,
that ordinary and lethal effects expire on their stated schedules, that each
damage event produces exactly one effect, and that a reset clears every effect.
The system lives entirely in `Hukbo.Client`, so it cannot reach the simulation
by construction; no test asserts that a battle's tick count, outcome, state
hash, or event hash is unchanged, and row 98 below is the only check of that.
Nothing automated proves that a hit reads as a hit to a person watching the
screen, or that the effects stay legible when the fighting gets crowded, which
is the only thing these rows are for. Only a human running
`./scripts/run.ps1` on an interactive Windows desktop may flip one of these rows
to `PASS`. Compilation, unit tests, and a window-opening probe do not.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 90. Read an ordinary hit at 1x | At normal speed a non-lethal blow produces a brief pulse on the struck pawn, one thin ring, and a small restrained shard burst. The blow is unmistakable without the screen filling with debris. | Not run | PENDING |
| 91. Check hits survive 4x | At 4x, hits landed on consecutive simulation ticks are each still visible, rather than only the last tick's hit appearing in each drawn frame. | Not run | PENDING |
| 92. Tell a lethal hit apart | A killing blow reads as clearly heavier than an ordinary one: a larger double ring and longer shards, appearing after the pawn has disappeared rather than on top of it. | Not run | PENDING |
| 93. Check readability across the zoom range | At fitted, minimum, and maximum zoom the primary ring stays readable. Zooming out reduces clutter without removing the ring, so a hit is never invisible at any zoom the spectator can reach. | Not run | PENDING |
| 94. Watch a crowded exchange | With many pawns trading blows at once the effects stay bounded. No persistent trail, smear, or lingering colour builds up on the arena, and the fighting stays legible underneath. | Not run | PENDING |
| 95. Pause and resume | Pausing lets effects already on screen finish while the simulation stops advancing. Resuming produces new effects normally, with no burst of stored-up effects on the first frame. | Not run | PENDING |
| 96. Reset clears everything | Next Round (`R`) and Full Reset (`Shift+R`) both clear every pulse and burst immediately. No effect from the previous match survives into the new one. | Not run | PENDING |
| 97. Check the arena edges | Resize the window and zoom in near each arena edge. No ring or shard draws over the status bar, the agent inspector, the event log, the match summary, or the menu overlay. | Not run | PENDING |
| 98. Confirm the effects change nothing | Run to a terminal result. Effects expire on their own, and the outcome, tick count, state hash, and event hash match a run of the same seed with the effects never observed. | Not run | PENDING |

## Event feed lifetime smoke (T17)

Covers the change recorded under T7 of
the Arch-informed performance hardening plan:
`LastEvents` now returns one of two permanent double-buffered collections
instead of a fresh one created each tick. The automated tests — the seed-1
hash equality above, `LastEventsRemainsACompletedTickSnapshot`,
`RetainedLastEventsReferenceIsNotValidPastTheProducingTick`, and
`BattleEventFeedTests.Ingest_CopiesEventValuesRatherThanRetainingTheSourceBuffer`
— prove the buffer contract and the copy-out behavior in isolation; none of
them prove that a spectator watching the live feed on screen ever sees the
effect of the changed lifetime. These three rows are the only rows this
workstream adds to this checklist. They exist because T7 changed the lifetime
of the collection `LastEvents` returns, and only a person at an interactive
Windows desktop may flip one of them to `PASS`. **Not performed. All three
rows are `PENDING`.**

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 99. Watch the battle event feed during a live run | Events appear correctly and in the correct order for the whole run; nothing is missing, duplicated, or out of sequence. | Not run | PENDING |
| 100. Pause, resume, and change speed repeatedly during a run | The feed survives every pause and every speed change without losing or duplicating a single entry. | Not run | PENDING |
| 101. Let a battle run to its end | Once the battle ends, the feed shows nothing stale left over from the last live tick. | Not run | PENDING |

## Visual improvement milestone smoke (VIS-041)

Covers the first milestone of
the improve-visuals implementation plan draft
(tasks VIS-001 through VIS-038, milestone-scoped subset), closing that plan's
own VIS-041 task. **Not performed. Every row below is `PENDING`.** The
automated suites landed alongside these tasks prove the catalog validation
passes, the detail-tier and contrast-envelope thresholds fire at their exact
pinned values, the missing-visual diagnostics dedupe and cap correctly, the
reduced-motion truth table is exhaustive, and the MotionIntensity-Off sway
path is bit-identical to a static backdrop by construction. None of that
proves how the milestone's actual content — the Kalis tint family, the S1
shield skin, the five levy presets, the grass ground and its sway, the
diagnostic placeholder — reads to a person watching the screen, which is the
only thing the rows below are for. Per `CLAUDE.md` section 6 item 4 and
R-W6.17, only a human at an interactive Windows desktop may flip one of these
rows to `PASS`, `FAIL`, or `BLOCKED`; compilation, unit tests, and a
window-opening probe do not count, and no agent may perform this session.

**Review protocol.**

- **Launch and seed.** Start from a fresh `./scripts/run.ps1` session, or
  press `Shift+R` (Full Reset) if a session is already open. Either path
  returns the seed to `1`, matching the milestone's recorded reference pair
  (stateHash `A883926A3B93792E`, eventHash `2A9F2D7054CD1805` — see "The
  preset V3 reference pair" above, the same pair VIS-045's canonical gate run
  reproduced byte for byte). The package's planning documents cite the older
  Phase 2 pair `27DC94C6E9A01E35` / `372C9217E5CB8BE9`; that pair was already
  stale when the package began, because the V3 combat-preset merge changed the
  ruleset after it was recorded. The neutrality claim is unaffected — it is a
  before-and-after comparison on the same commit lineage, and both sides agree.
  Every row below is observed against this seed-1 scenario unless the row says
  otherwise.
- **Camera stations.** Three fixed stations, named per row: minimum zoom
  (zoomed fully out, the whole field visible at once), default fit (the
  camera position the game opens in, before any zoom or pan input), and
  maximum zoom (zoomed fully in, a close-up on one or two pawns). A row that
  names more than one station must be observed at each one named before it
  can be marked `PASS`.
- **Themes.** Default and high-contrast, cycled through the in-game theme
  selector. A row that names both themes must be observed under each before
  it can be marked `PASS`.
- **Settings permutations.** Gore Intensity and MotionIntensity are both
  spectator-facing settings this milestone's rows can depend on; exercise
  only the permutation a given row actually names. No row in this batch
  depends on the Gore Intensity setting. The MotionIntensity setting (`Off`,
  `Reduced`, `Full`) is exercised by the three sway rows and by the
  operability row, each of which names the value or values it needs.
- **Evidence.** Fill in the evidence-field table below once for the session
  (date, machine/platform, source commit, launch path, and any screenshot
  paths). Record what was actually seen in each row's `Actual` column, even
  for a row that ends up `PASS` — "Not run" is only correct for a row that
  was never attempted. Attach a screenshot under `artifacts/` for any row
  where a picture is useful evidence; capture one for the forced-failure
  placeholder row and for any row disposed `FAIL`, since those are the ones
  a second reader is most likely to need to see rather than take on faith.
- **Disposition.** Only a human at an interactive desktop may write `PASS`,
  `FAIL`, or `BLOCKED` into a row's `Status` column; nothing in the plan, no
  test, and no agent may. `PASS` requires the row's whole expected
  observation to have actually been seen, exactly as stated — a row that was
  only partly exercised stays `PENDING`, following the precedent set by rows
  2, 4, 5, and 15 above, and the still-missing half is named in `Actual`
  rather than left silent. `FAIL` records what was actually seen instead of
  the expected observation. `BLOCKED` records the obstacle that prevented the
  row from being exercised at all. A row untouched by this session stays
  `PENDING`.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

Rows marked with a dagger (†) instantiate a requirement traced elsewhere in
the plan and carry more weight than an ordinary readability check; the note
below the table explains each one.

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 102. Kalis tints at minimum zoom | At the minimum-zoom station, default theme, MotionIntensity Full, Kalis-armed pawns remain classifiable as Kalis-wielders; the `freshIron`/`wellOiled` tint difference is invisible or below the threshold of notice at this distance. | Not run | PENDING |
| 103. Kalis tints at default fit | At the default-fit station, default theme, compare a `freshIron` and a `wellOiled` Kalis pawn side by side. The tint reads as material variation on the same weapon, never as a different weapon. | Not run | PENDING |
| 104. Kalis tints at maximum zoom | At the maximum-zoom station, default theme, close in on a single Kalis pawn. The tint is visible without breaking weapon-role recognition — it still reads unmistakably as a Kalis. | Not run | PENDING |
| 105. S1 shield distinguishable at minimum zoom | At the minimum-zoom station, default theme, compare a shield-bearing pawn (S1 `mactanThin`) against an unshielded pawn of the same weapon. Shield bearers are distinguishable from solo warriors without zooming in or clicking either. | Not run | PENDING |
| 106. S1 shield reads as the same equipment † | At the default-fit station, default theme, examine an S1 `mactanThin` shield bearer. The skin reads as ordinary shield equipment, not as a different or a visibly reduced piece of equipment compared to an unshielded pawn's absence of one. | Not run | PENDING |
| 107. Levy presets read as varied but coherent | At the default-fit station, default theme, observe the five levy clothing presets across the roster. The five read as visibly varied from one another while still reading as clothing belonging to the same army, not as unrelated or mismatched equipment. | Not run | PENDING |
| 108. Levy presets do not misread faction or equipment | At the default-fit station, default theme, compare warriors wearing different levy presets across both factions. No preset reads as belonging to the other faction, and no preset reads as a different weapon or equipment identity than the pawn actually carries. | Not run | PENDING |
| 109. Grass reads as grassland, not a checkerboard | Cycle through the minimum-zoom, default-fit, and maximum-zoom stations, default theme, observing the battlefield ground at each. At every station the ground reads as living grassland with grass clusters scattered across it, not as a flat repeating checkerboard tile pattern. | Not run | PENDING |
| 110. Arena border still reads as the strongest line | At the default-fit station, default theme, compare the arena border against the new grass ground. The border remains the visually strongest line on the field; the grass rendering does not compete with it or make it harder to find. | Not run | PENDING |
| 111. Sway reads as alive, not as noise | At the default-fit station, default theme, MotionIntensity Full, watch the grass during a busy engagement (multiple pawns fighting on screen at once). The sway reads as gentle, organic motion — alive — rather than as flicker or visual noise. | Not run | PENDING |
| 112. No sway motion visible at minimum zoom | At the minimum-zoom station, default theme, MotionIntensity Full. No grass motion is visible at this distance — the detail-tier gate suppresses sway at minimum zoom regardless of the motion setting. | Not run | PENDING |
| 113. High-contrast theme shows zero grass motion | At the default-fit station, high-contrast theme, MotionIntensity Full. The high-contrast theme shows zero grass motion, independent of the MotionIntensity setting. | Not run | PENDING |
| 114. Motion setting is operable and gates sway exactly † | Open Menu, locate the Motion Intensity control, and cycle it through `Off`, `Reduced`, and `Full` while watching the grass at the default-fit station, default theme. The control is reachable and operable from the menu. `Off` shows exactly zero grass motion — the off switch is exact, not merely reduced. `Reduced` shows visibly damped motion. `Full` shows the full sway amplitude. | Not run | PENDING |
| 115. Forced-failure placeholder is conspicuous | Run the forced-failure debug configuration that exercises the visual-catalog resolver's fallback path (see the resolver and its tests landed under VIS-003/VIS-004/VIS-008 for the specific trigger, as this document does not fix one that was not verified against the running build). Observe the affected element's position, then inspect the session's debug log on the `assets` channel. The diagnostic placeholder is conspicuously visible at the affected element's position — not blended in, not easy to miss — and the `assets` channel logs the fallback event exactly once for that identifier. | Not run | PENDING |

**Row 106** instantiates R-X.12's false-cause guard (no equipment reading as
less mechanical coverage than another) for the milestone's single shipped
shield skin. The full multi-skin comparison the guard was written for —
whether a narrower S2/S5 skin reads as less coverage than S1, S3, or S4 —
only becomes meaningful once VIS-014 ships the other three skins, which is
post-milestone; that comparison gets its own row under VIS-043 when VIS-014
lands, per OD-10's resolution (see the implementation plan around VIS-014 and
the requirements-traceability table entry for R-X.12).

**Row 114** is the milestone completion condition the plan's milestone
section calls "sway off-switch exact" (implementation plan, First milestone
section): the row requires observing that `MotionIntensity Off` produces
literally zero grass motion, not merely a damped or reduced one, in addition
to confirming the control is reachable from the menu.

## Visual improvement full-package smoke (VIS-043)

Covers every post-milestone task in
the improve-visuals implementation plan draft
whose own "Manual visual verification" section calls for a row that the
milestone checklist above (VIS-041) did not already create: VIS-009, VIS-011,
VIS-012, VIS-014, VIS-015, VIS-016, VIS-020, VIS-022, VIS-023, VIS-024,
VIS-028, VIS-029, and VIS-033. Three further post-milestone tasks in that same
list — VIS-006, VIS-019, VIS-021 — name no new row of their own; the audit
table below explains why. VIS-027's row is a re-judgment of an existing
milestone row rather than a new one, and VIS-036 is a hand-run measurement
procedure, not a screen-look row, already recorded in its own section above.
**Not performed. Every row below is `PENDING`.** As with VIS-041, only a
human at an interactive Windows desktop may flip one of these rows to `PASS`,
`FAIL`, or `BLOCKED`; compilation, unit tests, and a window-opening probe do
not count, and no agent may perform this session, per `CLAUDE.md` section 6
item 4 and R-W6.17.

**Review protocol.** The launch-and-seed instructions, the three named camera
stations (minimum zoom, default fit, maximum zoom), the default and
high-contrast theme cycle, and the disposition rules (`PASS` requires the
whole expected observation to have actually been seen; a partly-exercised row
stays `PENDING`; `FAIL` records what was actually seen; `BLOCKED` records the
obstacle) are the same protocol recorded under the VIS-041 section above and
are not repeated here. The MotionIntensity and Gore Intensity settings are
not exercised by any row in this batch.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

Rows marked with a dagger (†) instantiate a requirement traced elsewhere in
the plan and carry more weight than an ordinary readability check; the note
below the table explains each one.

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 116. Weapon variants at minimum zoom, all four weapons | At the minimum-zoom station, default theme, with 200+ pawns: each of the four weapon roles (Kampilan, Wasay, Kalis, Itak) remains classifiable, and tint variation across all four is invisible or below the threshold of notice at this distance. | Not run | PENDING |
| 117. Weapon variants at default fit, all four weapons | At the default-fit station, default theme: each pawn's weapon role is identifiable at a glance across all four weapons, and every tint reads as material variation on the same weapon, never as a different weapon. | Not run | PENDING |
| 118. Weapon variants at maximum zoom, including the Wasay lashing band | At the maximum-zoom station, default theme, close in on pawns carrying each of the four weapons in turn. Tint and wear variation is visible without breaking role recognition for any of the four; the Wasay's rattan lashing band at the head-haft junction reads as a lashed band, not as damage or a new weapon part. | Not run | PENDING |
| 119. Weapon inspector shows label, tier, and note | Select a pawn carrying each of the four weapons in turn. The inspector shows the unchanged pair-form weapon label, the selected variant's evidence tier, and its note; for a weapon with inspector-only entries (Kampilan k2, Kalis l2/l3), those appear labelled as later-or-provisional forms, never as anything the selected pawn is shown wearing. | Not run | PENDING |
| 120. Pawns render identically to the pre-package build at all three zoom stations | Compare a pawn's rendered appearance today against the pre-package build at the minimum-zoom, default-fit, and maximum-zoom stations. Weapon grip position, shield position, and layer draw order all look unchanged — this task only added anchor fields and empty layer slots, it drew nothing new. | Not run | PENDING |
| 121. Shield skins at default fit: four skins read as variation, S5 accent reads as binding † | At the default-fit station, default theme, compare shield-bearing pawns across all four shipped skins (S1 `mactanThin`, S2 `morgaFullBody`, S3 `boxerCagayan`, S5 `visayanKalasag`). All four read as variation of one shield, not as different pieces of equipment; on an S5-skinned pawn, the horizontal rattan accent reads as a binding detail, not as damage. | Not run | PENDING |
| 122. Shield skins at maximum zoom: face tones, curvature, edge step, and angled posture | At the maximum-zoom station, default theme, close in on shield-bearing pawns across all four skins. Face tones, the S3 curvature, and the High-tier edge-tone step are all visible; the shield's angled forward posture (S12) reads as an active stance, not as a layout bug, for every skin. | Not run | PENDING |
| 123. Shield skins under the high-contrast theme remain unambiguous | Switch to the high-contrast theme at the default-fit station. The shield block remains unambiguous against both torso and ground for all four skins — no skin blends into its background or becomes hard to identify as a shield. | Not run | PENDING |
| 124. Shield inspector shows label, anchor tag, tier, note, and pending flags | Select a shield-bearing pawn for each of the four skins in turn. The inspector shows the plain label `Tall Hardwood Shield`, the skin's anchor tag, its evidence tier, and its note, including the pending-verification flags on the *kalasag* (S5) and, if OD-2's default stands, any *palisay* reference — with neither name appearing as a bare player-facing label anywhere in the panel. | Not run | PENDING |
| 125. Fifty-plus presets read as varied but coherent at normal zoom | At the default-fit station, default theme, observe the full roster (levy plus Visayan, Tagalog, and Northern Luzon blocks) across both factions. The fifty-plus presets read as visibly varied from one another while still reading as clothing belonging to the same two armies, not as unrelated or mismatched equipment. | Not run | PENDING |
| 126. Elite figures read as denser in gold and dye, not larger | At the default-fit station, default theme, compare an elite- or datu-marked preset (gold accents, richer dye) against an ordinary preset from the same block. The elite figure reads as denser in gold and dye detail; it never reads as a physically larger pawn. | Not run | PENDING |
| 127. At minimum zoom, faction and weapon role remain the dominant reads | At the minimum-zoom station, default theme, with 200+ pawns drawn from the full roster across all blocks. Faction (by ground-ring color) and weapon role remain the dominant, most legible reads on the field; no preset's clothing or color competes with either for attention at this distance. | Not run | PENDING |
| 128. Armored figures read as bulkier, not as shielded | At the default-fit or maximum-zoom station, default theme, compare a pawn wearing an armor-layer component (F2 through F5) against an unarmored pawn and against a shield-bearing pawn. The armored pawn reads as visibly bulkier through the torso, and does not read as if it were carrying a shield. | Not run | PENDING |
| 129. Adornment accents visible at maximum zoom without breaking any read | At the maximum-zoom station, default theme, close in on a pawn wearing adornment accents (gold accents I4/I5, or the C3 gold-edged putong). The accents are visible without breaking weapon-role, faction, or equipment recognition. | Not run | PENDING |
| 130. Appearance inspector shows preset name, scope tag, tier, and component notes | Select any pawn from the full roster. The inspector shows the preset's plain-English name, its scope tag, its evidence tier, a per-component tier list with must-not-generalize notes, pending-verification flags where applicable, and any non-renderable flavor lines — with no bare Filipino term appearing unpaired anywhere in the panel. | Not run | PENDING |
| 131. Trampled areas visibly thin where fighting happened | During or after a battle with visible casualties, observe the grass around a cluster of `Death` events. The grass there reads as visibly thinned or trampled compared to untouched ground elsewhere on the field. | Not run | PENDING |
| 132. Dust reads as impact punctuation, not weather (ships only if VIS-029 shipped) | If VIS-029 shipped this pass: during a busy engagement, observe the brief dust puffs spawned on `Death` (and, if implemented, a throttled `Attack`) events. The dust reads as a short, localized punctuation of an individual impact, not as ambient weather or a persistent haze across the field. If VIS-029 was not shipped this pass, record this row `BLOCKED` with that reason rather than leaving it silently unresolved. | Not run | PENDING |
| 133. With 200+ pawns, faction remains readable by ring shape and position, hue disregarded † | At the default-fit station, default theme, with 200+ pawns on the field. A human with typical color vision judges the faction ring's shape-and-position channel alone, disregarding hue, and finds faction still distinguishable by that channel. | Not run | PENDING |

**Row 121** instantiates R-X.12's false-cause guard for the full four-skin
shield roster, completing the comparison that row 106 in the VIS-041 section
above explicitly deferred to this task ("that comparison gets its own row
under VIS-043 when VIS-014 lands, per OD-10's resolution"). Row 106 itself is
left as recorded — it covered only the single milestone skin, S1, and is not
edited or duplicated here.

**Row 133** is an honest partial check, worded exactly as VIS-033's own task
text requires: it holds only the no-regression floor that no new garment,
tint, skin tone, or ground shade introduced by this package has become a
competing faction signal. It is not color-blind verification. OD-7 defers the
stronger shape-redundant faction marker to a backlog item in
`docs/plans/TODO.md`; this row does not stand in for that marker.

**Human review task, not a checklist row.** Both `implementation-plan-draft.md`
(VIS-043's own goal) and `warrior-appearance-design.md` call for a line-by-line
historical review of the full preset roster table against
`docs/research/improve-visuals/warrior-appearance-historical-research.md`.
That review is a human read-through of a document against another document,
not an observation of the running game, so it does not belong in the table
above as a `PASS`/`FAIL`/`BLOCKED` row. It is recorded here as an outstanding
task: the review has not been performed, and per
`implementation-plan-draft.md`'s VIS-044 entry, a failure found during that
review routes to a content-correction task, not to a change in this testing
document. It is due at VIS-044, the full-package manual review session,
alongside the rows above.

#### Criterion-to-row audit

Every post-milestone task named in this section's opening paragraph, and the
disposition of its own "Manual visual verification" section from
`implementation-plan-draft.md`:

| Task | Manual criterion (as stated in the task) | Disposition |
| --- | --- | --- |
| VIS-006 | None stated; runtime effect is observable only in a forced-failure build. | Already covered by row 115, created under VIS-041 for VIS-008's forced-failure placeholder path — the same observable effect VIS-006's catalog validator falls back through. No new row. |
| VIS-009 | "Pawns render identically to the pre-package build at all three zoom stations." | Row 120. |
| VIS-011 | "The three zoom rows across all four weapons; the Wasay lashing band reads as a band, not damage or a new weapon part." | Rows 116, 117, 118 (the maximum-zoom row, 118, carries the Wasay lashing-band clause, matching the single bundled row `weapon-visuals-design.md`'s own "Readability confirmation" section defines for maximum zoom). |
| VIS-012 | "Inspector shows, for a selected pawn, the pair-form weapon label, the variant's evidence tier, and its note." | Row 119. |
| VIS-014 | "The four skins read as variation of one shield, not different equipment"; "the S5 accent reads as binding, not damage"; "the maximum-zoom and high-contrast rows per the shield design." | Row 121 (first two clauses, bundled per `shield-visuals-design.md`'s own normal-zoom row), row 122 (maximum zoom, shared with VIS-015), row 123 (high-contrast). |
| VIS-015 | "The angled posture reads as an active stance, not a layout bug (maximum zoom)." | Row 122, shared with VIS-014 — `shield-visuals-design.md`'s own maximum-zoom row bundles face tones/curvature/edge steps together with the angled-posture observation as one check performed at one station under one set of conditions. |
| VIS-016 | "Inspector shows, for a selected shielded pawn, the plain shield label, the skin's anchor tag, tier, and note including pending flags." | Row 124. |
| VIS-019 | None directly; "judged through the roster rows in VIS-043." | Covered by rows 125, 126, 127 below and by the existing rows 107/108 under VIS-041. No new row. |
| VIS-020 | "Fifty-plus presets read as varied but coherent at normal zoom"; "elite figures read as denser in gold and dye, not larger." | Rows 125, 126. |
| VIS-021 | "Shared roster rows in VIS-043." | Covered by rows 125, 126 and the existing row 108. No new row. |
| VIS-022 | "Shared roster rows in VIS-043; at minimum zoom, faction and weapon role remain the dominant reads." | Row 127 (the new clause); the shared portion is covered by rows 125, 126, and 108 as with VIS-021. |
| VIS-023 | "Armored figures read as bulkier, not as shielded"; "accents visible at maximum zoom without breaking any read." | Rows 128, 129. |
| VIS-024 | "Inspector shows preset name, scope tag, tier, and component notes for any selected pawn." | Row 130. |
| VIS-027 | "Ground reads as living grassland, not checkerboard, at all zooms (re-judged after this task)." | Reuses existing row 109 under VIS-041 — the identical criterion, re-observed against the new correlated-shading formula rather than the independent per-cell hash it replaces. No new row; row 109 is not edited. |
| VIS-028 | "Trampled areas visibly thin where fighting happened." | Row 131. |
| VIS-029 | "Dust reads as impact punctuation, not weather (wording finalized when unblocked)." | Row 132, worded to account for the task's optional-per-OD-9 status. |
| VIS-033 | "With 200+ pawns, faction remains readable by ring shape and position when hue is disregarded" (honest wording). | Row 133. |
| VIS-036 | None; "the run itself is the hand procedure," BLOCKED-honest if no desktop. | Not a screen-look row. Already recorded, and already disposed `BLOCKED, honestly`, in the "Render performance measurement — full matrix (VIS-036)" section earlier in this document. No new row. |

Eighteen new rows (116 through 133) were created by this task, all `PENDING`.
No row born flipped, per VIS-043's own prohibited-scope clause.

## Collision firmness, battle report, and window shell smoke (2026-07-28)

Added by the collision report and window shell plan. The canonical
gate passed on 2026-07-28 with `stateHash A080E28DA7C79C20`,
`eventHash 2B6FB3A9A9C1960D`, `measuredTicks 1677`, `outcome Faction0Victory`,
`deterministic true`, `maximumPenetrationRaw 0`, and
`longestBlockedStreakTicks 88`. **A passing gate proves none of the rows below.**
Every one of them needs a human at an interactive desktop, and no agent may flip
one to `PASS`.

The minimize row deserves particular suspicion. `SDL_MinimizeWindow` is reached
through a `[LibraryImport("SDL2")]` P/Invoke that compiles cleanly but has never
been executed in this repository. A clean build is no evidence at all that the
native call works; if it fails, the button is dead with no visible error.

| # | Step | Expected | Result | Status |
| --- | --- | --- | --- | --- |
| 134. Watch a battle at the enlarged body radius | Crowds pack visibly tighter and the melee front blocks more firmly than at the old four-world-unit radius. No unit is stranded and no line gridlocks. | Not run | PENDING |
| 135. Run several battles to a terminal outcome | Every battle reaches a decisive result or a legitimate draw. None stalls at the tick limit with both factions alive and unable to move. | Not run | PENDING |
| 136. Confirm the OS title bar is gone | The window has no title bar and no operating-system exit, minimize, or maximize buttons. | Not run | PENDING |
| 137. Click the new Min button | The window minimizes to the taskbar. Clicking the taskbar icon restores it. Watch the taskbar — do not infer this from the button reacting. | Not run | PENDING |
| 138. Click the new Close button | The game exits cleanly. | Not run | PENDING |
| 139. Press Alt+F4, and use Escape then Exit Game | Both still quit the game. | Not run | PENDING |
| 140. Confirm the window still resizes | Dragging a window edge resizes the window, and the layout adapts. `AllowUserResizing` was deliberately left true. | Not run | PENDING |
| 141. Check all six control-bar buttons | Play, Pause, Menu, Sounds, Min, and Close all render fully inside the bar. The Close button is not clipped at the right edge. | Not run | PENDING |
| 142. Open the unit setup menu | Every label, including `Kalis — Thrusting Blade (shielded)`, renders fully inside its row and does not overrun the stepper controls. | Not run | PENDING |
| 143. Check the stepper still reads clearly | The unit count, up to its 250 maximum, centres cleanly in the narrowed value column between the two arrows. | Not run | PENDING |
| 144. Play a battle to the end and open the battle report | The Battle Report button appears on the match summary and opens the report panel. It does not crash. | Not run | PENDING |
| 145. Read the battle report numbers | Kills, damage dealt and taken, accuracy, faction totals, and the highlight lines are populated and plausible against the battle just watched. | Not run | PENDING |
| 146. Scroll the kill leaderboard | The leaderboard scrolls and clips correctly inside its section, and the panel stays inside the arena bounds. | Not run | PENDING |
| 147. Confirm weapon names in the report | Every weapon appears in pair form, for example `Kampilan — Great Blade`, never as a bare cultural name. | Not run | PENDING |
| 148. Start a second battle after finishing one | Next Round and Full Reset both clear the report. The second battle reports its own statistics with nothing carried over from the first. | Not run | PENDING |

## Quit confirmation, maximize, and Core faction metrics smoke (2026-07-28)

Added by the quit-confirmation, maximize and faction metrics plan.
**A passing gate proves none of the rows below.** Every one needs a human at an
interactive desktop, and no agent may flip one to `PASS`.

The maximize and restore rows deserve the same suspicion as the minimize row
above them: `SDL_MaximizeWindow`, `SDL_RestoreWindow`, and `SDL_GetWindowFlags`
are P/Invokes that compile cleanly and have never been executed in this
repository. A clean build is no evidence that any of them works.

| # | Step | Expected | Result | Status |
| --- | --- | --- | --- | --- |
| 156. Click `Close` on the control bar | A confirmation prompt appears. The game does not quit. The battle behind it is dimmed. | Not run | PENDING |
| 157. Cancel the prompt | The prompt closes and the battle continues untouched. | Not run | PENDING |
| 158. Confirm the prompt | The game exits. | Not run | PENDING |
| 159. Open the prompt and press `Enter` immediately | Cancel holds focus, so `Enter` cancels rather than quitting. | Not run | PENDING |
| 160. Open the prompt and press `Escape` | The prompt cancels. If the menu was open behind it, the menu stays open — `Escape` belonged to the prompt alone. | Not run | PENDING |
| 161. Open the prompt, press Tab or an arrow key, then `Enter` | Focus moves to Quit and `Enter` then quits. | Not run | PENDING |
| 162. Open the prompt and click well away from both buttons | Nothing happens. The click does not reach the control bar, the arena, or agent selection underneath. | Not run | PENDING |
| 163. Menu, then `Exit Game` | The same prompt appears. The menu path does not quit directly. | Not run | PENDING |
| 164. Press Alt+F4 | Quits immediately with no prompt, by design — it is the guaranteed escape hatch on a borderless window. | Not run | PENDING |
| 165. Click `Max` | The window maximizes. | Not run | PENDING |
| 166. Click `Max` again | The window restores to its previous size. | Not run | PENDING |
| 167. Maximize outside the app (Windows snap or taskbar), then click `Max` | It restores rather than re-maximizing — the button read the real window state instead of a tracked flag. | Not run | PENDING |
| 168. Check all seven control-bar buttons | Play, Pause, Menu, Sounds, Min, Max, and Close all render fully inside the bar. Close is not clipped at the right edge. | Not run | PENDING |
| 169. Open the battle report and read a faction line | Attack, hit, and accuracy figures are present, and the estimated figures are marked with a tilde. | Not run | PENDING |
| 170. Read the battle report disclosure line | It states that attacks and accuracy are simulation-reported while kills, damage, and warrior rows are estimated. | Not run | PENDING |
| 171. Compare the reported faction accuracy against a headless run of the same seed | It matches the simulation own counters rather than an event-derived approximation. | Not run | PENDING |

## Persistent contingent smoke

Added by the formation and movement realism change (T18 of
[2026-07-28-formation-movement-realism.md](../plans/2026-07-28-formation-movement-realism.md)),
which flips the default `Scenario.MovementPreset` to `PersistentContingentsV2`.
**Partially performed on 2026-07-28.** Rows 102, 103, 104, 105, 111 and 114 were
observed in one hands-off pass at the default camera fit. Rows 106, 107, 108,
109, 110, 112 and 113 remain unobserved. Rows 104 and 114 failed. The automated
suite —
`MovementPresetRegistryTests`, `FormationRulesTests`,
`ContingentOffsetTests`, `ContingentStateMachineTests`, `ArrivalTaperTests`,
`PersistentContingentTests` and `ContingentDeadlockTests` — proves the state
machine's six priority-ordered transition rules, the duty cycle, the leader
scan, the straggler gate, the two geometric gates, the arrival taper, and three
engineered deadlock geometries all resolve correctly, both in isolation and
inside a running simulation. None of it proves that the resulting movement
reads as a group of warriors gathering and advancing together to a person
watching it, which is the only thing these rows are for.

**Scoping note — this is not the last-stand formation smoke.** The last-stand
formation smoke above (rows 76 through 81) covers the whole-faction rally that
fires only once a side is down to its final handful of warriors, gathering
every survivor of that faction on one rally agent. This section covers a
different mechanism that runs for the whole battle, not only its ending: from
deployment onward each faction is divided into up to eight persistent
contingents, and `ResolveContingentStates` cycles each one between gathering on
its own leader and advancing independently throughout the match. A spectator
should be able to see both behaviours in the same battle and tell them apart —
several small contingents repeatedly gathering and re-forming during the
advance, and then, only once a side is reduced to its last few warriors, the
separate whole-faction convergence the last-stand rows describe.

Only a human running `./scripts/run.ps1` on an interactive Windows desktop may
flip one of these rows to `PASS`. Compilation, unit tests, and a
window-opening probe do not.

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-07-28 |
| Machine/platform | Windows 11 Pro 10.0.26200, win-x64 |
| Source commit | 8f4e426, worktree `formation-movement-realism` |
| Launch path (`source` or package path) | `source` — `./scripts/run.ps1 -Configuration Release` |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 102. Read several distinct groups well past deployment | Each side stays readable as several distinct groups well past the opening frame, at the default camera fit, rather than merging into one crowd within a few seconds. | Each side split into about three readable groups, and those stayed distinct well past the opening frames. They merged into one crowd only late in the battle, once casualties had mounted. | PASS |
| 103. Watch a strung-out group gather and resume | A group that has strung out visibly gathers on one of its own warriors, then resumes advancing, rather than gathering indefinitely or never gathering at all. | A group that had strung out was seen to fall back briefly, gather, and then carry on advancing with the group, rather than gathering indefinitely. | PASS |
| 104. Confirm the gathered shape is ragged | The gathered shape is ragged. It is not a ring, a line, an arc, a grid, or any shape that looks placed, and no warrior sits at an obviously exact distance from the one it gathered on. | Not run | PENDING |
| 105. Watch a group arrive and break apart | On reaching the enemy, a group visibly stops holding together and its warriors fight as individuals. The transition reads as arriving, not as the group breaking apart. | The transition read as the group arriving rather than as the group falling apart. | PASS |
| 106. Confirm warriors ease into contact | Warriors ease into contact rather than travelling at full speed and stopping dead against an enemy body. | Not run | PENDING |
| 107. Confirm a warrior steps aside for its leader | A warrior standing in front of the warrior its group has gathered on steps aside rather than being walked through or standing there blocking it. | Not run | PENDING |
| 108. Inspect the contingent row | Selecting any warrior shows a `Contingent: <n> — <state>` row in the inspector, and that state changes over the course of the battle rather than reading the same value throughout. | Not run | PENDING |
| 109. Confirm the contingent ground tints are distinguishable | The eight contingent ground tints within one faction are distinguishable from each other at the default camera fit, and no tint is mistakable for the opposing faction's colour, at all six themes. | Not run | PENDING |
| 110. Confirm the frozen preset is unaffected | Running the same seed under `IndependentPursuitV1` looks exactly as the game looks today: no gathering, no per-contingent tint, and no contingent row in the inspector. | Not run | PENDING |
| 111. Confirm the battle still resolves | A full 200-agent battle reaches a terminal outcome. Neither side stands gathered and unmoving until the tick limit. | The battle reached a terminal outcome and a winner was declared. | PASS |
| 112. Watch a group reach a map edge or corner | A group whose warriors reach a map edge or a corner keeps moving and fighting there rather than piling into the boundary and staying put. This is the visible face of the map-edge open-ground rule in design section 3.5. | Not run | PENDING |
| 113. Watch two groups collide and separate | Two groups on the same side that walk into each other come apart again and carry on advancing, rather than jamming into one stationary mass. This is the visible face of the cross-contingent rule in design section 3.5. | Not run | PENDING |
| 114. Watch whether gathering keeps appearing across the whole advance | Groups read as groups for the whole of the advance, not only in the first few seconds after deployment. Watch a full battle at the default camera fit and judge whether gathering behaviour keeps appearing across several different groups as the armies converge, or whether it happens once near the start and then stops. This is the spectator half of the inertness bar in design section 10.3 — the automated half asserts thresholds on how often cohesion is granted, and only a person can say whether the result looks like several groups advancing or like one crowd that briefly twitched. | Not run | PENDING |

**History.** Rows 104 and 114 both failed at commit `8f4e426`. The cause was
movement transition rule 3 latching a whole contingent into
`ContingentState.Close` as soon as a single member of that contingent reached
contact. Both rows have been reset to `PENDING` and now await re-observation
under `PersistentContingentsV3`. That re-observation is not expected to be a
clean pass: the measurement taken after the fix shows `Hold` episodes after a
contingent's first `Close` going from zero to one across a five-seed,
fifty-contingent-battle sweep, `Close` occupancy falling from 63.69 % to
53.11 %, attrition (rule 2) rising to 30.45 % and becoming the new ceiling on
mid-battle gathering, and the `Hold` aspect-ratio tail getting worse (p99 from
3.06 to 5.04, maximum from 5.17 to 14.21). See "Measurement behind rows 104 and
114" and "Re-measurement after the `Close` latch fix (T7), 2026-07-28" below
for the full after-table; it is not restated here.

Two observations from the 2026-07-28 pass do not map to any row above, and are
recorded here so that a later change can be judged against them. First, once one
side was reduced to roughly twenty warriors, the survivors fought in the centre
of the map in what the observer described as a line, taking each other on one at
a time. Second, when two bodies of warriors met, only the front rank appeared to
be fighting, and the contact edge read as a shallow concave curve. Neither
observation has been traced to a cause yet, and both concern shapes that
[03-deep-past-formations-and-tactics.md](../research/battles/03-deep-past-formations-and-tactics.md)
lists among the formations Hukbo should not present as historical.

## Measurement behind rows 104 and 114

Both failures above were judgements by eye. `Hukbo.Tools.ContingentShape`
(see [tools/README.md](../../tools/README.md)) attaches numbers to them. The
figures below are from a five-seed sweep, 200 agents, 10 000-tick limit, run at
commit `8f4e426`:

```powershell
dotnet build src/Hukbo.Core/Hukbo.Core.csproj -c Release
dotnet run --project tools/Hukbo.Tools.ContingentShape -c Release -- 10000 200 5
dotnet run --project tools/Hukbo.Tools.ContingentShape -c Release -- 10000 200 5 IndependentPursuitV1
```

**Row 114 is confirmed, and one rule causes it.** Of the fifty
contingent-battles observed, all fifty reached `ContingentState.Close`, and
none of the fifty ever returned to `ContingentState.Hold` afterward. Hold ticks
after a contingent's first `Close`: zero. Hold episodes after a contingent's
first `Close`: zero. Contingents spend 63.69 % of their living ticks in `Close`
and a further 23.51 % in `Break`, against 3.09 % in `Hold`. The denial
attribution puts 63.69 % of all contingent-ticks on transition rule 3 — an
enemy within the close radius — while the two geometric gates account for
1.81 % and 1.07 %, and a shut duty-cycle window for 1.12 %. Rule 3 tests the
minimum distance over *every* member of the contingent, so one warrior of forty
reaching contact puts the whole contingent into `Close`, and in a converged
melee that condition never lifts again.

**Row 104 is not reproduced by the shape metric, and points at the same
cause.** Across 1 671 `Hold` samples the principal-axis aspect ratio has a
median of 1.56, a 99th percentile of 3.06, and a maximum of 5.17; 79.29 % of
gathers sit below 2.0. That is a clump, not a line. The two hypotheses that
would have produced a line are both refuted for `Hold`: the gathered cloud
aligns more with the contingent's own direction of advance (mean 12.21°) than
with a world axis (mean 22.09°), which is the opposite of what an
axis-aligned bias square would produce; and no `Hold` or `Advance` sample in
the whole sweep fell within sixty ticks of a leader change, because leader
changes require deaths and deaths only begin once a contingent has already
latched into `Close`. What the observer saw mid-battle was therefore almost
certainly not a `Hold` at all — `Close` contingents have a median aspect of
3.60 and a 90th percentile of 7.73 — which makes rows 104 and 114 two faces of
one defect rather than two.

**Control.** The same sweep under the frozen `IndependentPursuitV1` preset
leaves every contingent in `ContingentState.None` for 100 % of its ticks, and
the same nominal groups then show a median aspect of 5.09 with both angles at
44.1°, which is the uniform-random value. The cohesion that `Hold` applies is
doing real work when it is allowed to run; it is almost never allowed to run.

## Re-measurement after the `Close` latch fix (T7), 2026-07-28

The measurement above is the "before" picture, taken at commit `8f4e426`,
before any rule change from this workstream landed. This is the "after"
picture, taken once T1 through T6 of
the contingent close-latch plan
had landed (commits `bde702f` through `855c797`): `MovementRuleset` now
carries `CloseFractionNumerator` and `CloseFractionDenominator`; transition
rule 3 counts members in contact against those fractions instead of taking a
minimum distance; `PersistentContingentsV3` is registered with `(1, 2)` —
close at half the living members in contact, re-open below a quarter; and
`Scenario`'s shipped default has moved from `PersistentContingentsV2` to
`PersistentContingentsV3`.

Both runs use the same workload the before-table used — a five-seed sweep,
200 agents, a 10 000-tick limit, read from this file rather than assumed:

```powershell
dotnet build src/Hukbo.Core/Hukbo.Core.csproj -c Release
dotnet run --project tools/Hukbo.Tools.ContingentShape -c Release -- 10000 200 5 PersistentContingentsV3
dotnet run --project tools/Hukbo.Tools.ContingentShape -c Release -- 10000 200 5 PersistentContingentsV2
```

**A note on the command line actually run.** The plan's T7 section writes the
first command with no fourth argument, relying on the tool's default. That
default is a literal hardcoded in `tools/Hukbo.Tools.ContingentShape/Program.cs`
(`MovementPresetId.PersistentContingentsV2`), independent of `Scenario`'s
shipped default — T5 and T6 did not touch it, and this task's file ownership
does not extend to changing it either. Running the tool with no fourth
argument today therefore still measures V2, not the new shipped default, so
both runs below pass the preset explicitly instead. The second run
(`PersistentContingentsV2`) is the control the plan asks for either way.

**Occupancy and denial attribution.**

| State / denial reason | V2 (control) share | V3 share |
| --- | --- | --- |
| `Close` / `close-enemy-within-close-radius` (rule 3) | 63.69 % | 53.11 % |
| `Break` / `break-attrition` (rule 2) | 23.51 % | 30.45 % |
| `Advance`, cohesion not needed / `already-gathered` | 5.71 % | 6.77 % |
| `gate6-square-overlap` | 1.81 % | 3.89 % |
| `Hold` / `none-cohesion-granted` | 3.09 % | 3.39 % |
| `window-shut` (duty cycle) | 1.12 % | 1.22 % |
| `gate5-map-edge` | 1.07 % | 1.17 % |

Design section 5 predicted the geometric gates and rule 2 (attrition) might
become the new ceiling once rule 3 stopped locking every contingent into
`Close` on a single member's contact. That prediction held: `break-attrition`
rose from 23.51 % to 30.45 %, and `gate6-square-overlap` roughly doubled, from
1.81 % to 3.89 %. `close-enemy-within-close-radius` fell from 63.69 % to
53.11 %, which is the fix doing what it was built to do — contingents spend
markedly less of the battle latched into `Close`.

**1. Hold episodes after first `Close` — must be non-zero.** V3: 1 episode,
14 ticks, across 50 contingent-battles, all 50 of which reached `Close`. V2
control: 0 episodes, 0 ticks, matching the frozen before-table exactly. The
count is non-zero, so the change did not fail at its stated purpose, but the
margin is thin: one `Hold` episode across the whole five-seed sweep is a long
way from "several small contingents repeatedly gathering and re-forming during
the advance," which is the spectator-visible behaviour rows 104 and 114
actually describe. That gap is recorded here as a finding rather than rounded
up.

**2. `Hold` aspect-ratio distribution.**

| Metric | V2 (today's baseline) | V3 |
| --- | --- | --- |
| Median | 1.56 | 1.59 |
| p99 | 3.06 | 5.04 |
| Max | 5.17 | 14.21 |
| Share below 2.0 | 79.29 % | 75.74 % |

The median barely moves. The tail does: p99 rises from 3.06 to 5.04 and the
observed maximum from 5.17 to 14.21, and the share of gathers reading as a
tight clump (aspect below 2.0) drops from 79.29 % to 75.74 %. That is a
materially worse tail, not a materially worse typical case, and the plan is
explicit that a worse distribution is new information rather than a thing to
quietly tune away. It is recorded here as a finding: whatever `Hold` episodes
now occur mid-battle (after a contingent has already passed through `Close` at
least once) evidently include some shaped less like a clump than the
approach-phase gathers the before-table measured. With only 1 mid-battle
`Hold` episode observed for V3 in this sweep, that is the most likely driver,
but the tool does not yet split `Hold` samples by before/after first `Close`
the way it splits ticks and episodes — the numbers above are the aggregate
across all `Hold` samples, exactly as the before-table reported them, and
that split is not built.

**3. Denial attribution**, repeated in one line per rule or gate for the
report contract: `close-enemy-within-close-radius` (rule 3) 53.11 % V3 vs
63.69 % V2; `break-attrition` (rule 2) 30.45 % V3 vs 23.51 % V2;
`already-gathered` 6.77 % V3 vs 5.71 % V2; `gate6-square-overlap` 3.89 % V3 vs
1.81 % V2; `none-cohesion-granted` (`Hold`) 3.39 % V3 vs 3.09 % V2;
`window-shut` 1.22 % V3 vs 1.12 % V2; `gate5-map-edge` 1.17 % V3 vs 1.07 % V2.

**4. `Close` state-flip frequency.** `Hukbo.Tools.ContingentShape` gained one
new counter for this task, `closeReentries`, printed as `Close re-entries
(state-flip)`. It counts a transition into `Close` that is not the
contingent's first entry into `Close` in that battle — the first entry is
excluded so the counter measures only re-entry after the contingent left for
some other state. Across the same five-seed, 200-agent sweep: V3 reports 10
re-entries, V2 reports 12. Both are non-zero: V2's rule 3 is symmetric at the
`(0, 1)` fraction (entry and exit threshold both collapse to `Max(1, ...)`),
so a contingent can in principle leave `Close` whenever the very last member
in contact drops out and re-enter once contact resumes, and the measurement
confirms that happens — twelve times across fifty contingent-battles, even
though no `Hold` episode ever followed any of those twelve. The V3 count (10)
is marginally lower than the V2 count (12), not higher: halving the entry
fraction to build the exit threshold did not produce a materially different
amount of state churn either way. That is the answer design section 7 asked
for — the two bands produce a similar order of magnitude of `Close` flipping,
and in both cases the flip essentially never routes back through `Hold`
before contact is re-established, on this five-seed sample.

**Outcome and battle length.** V2 and V3 simulate different behaviour, so the
five seeds do not produce the same terminal ticks or winners under the two
presets — that is expected and is not a determinism concern; determinism
within one preset is what `DeterminismTests` and the canonical gate check, not
agreement between two different presets. V2 control: 1064, 1712, 858, 1635,
2234 ticks (matching commit `8f4e426`'s frozen values exactly — seed 1
reproduces `Faction0Victory` at tick 1064). V3: 1334, 1909, 917, 1437, 2285
ticks.

**Verdict on the fix.** The fix works at the narrowest reading of its stated
purpose: `Hold` episodes after first `Close` are non-zero where they were
zero, and contingents spend materially less of the battle latched in `Close`
(53.11 % against 63.69 %). It does not yet produce the richer "repeatedly
gathering and re-forming during the advance" picture the design document and
rows 104 and 114 describe — one `Hold` episode in fifty contingent-battles is
a rare event on this sample, not a repeated behaviour, and the `Hold` shape
that does occur reads worse in the tail (p99 and max) than the approach-phase
gathers the before-table measured. Whether that is nonetheless visible to a
human at the default camera fit is exactly what T10's reset of rows 104 and
114 exists to find out, and no agent may answer that question.

## Shield-clash audio smoke

Added by the shield clash audio plan. **No interactive run was
performed, so every row below is unrun and its verdict is still pending.** Each
one needs a human at an interactive Windows desktop with working audio, and no
agent may flip one to a passing verdict.

Automated tests do exist for the parts of this change that can be tested without
a window or a speaker. `SoundCatalogTests.EveryDefinedWeapon_HasAShieldClashSlot`
proves that every defined weapon has a clash slot to route to.
`SoundCueMapperTests.Map_RoutesAShieldBlockToTheMatchingClashSlot` and
`Map_KeepsTheWeaponSlotForEveryOtherResolution` prove that a `ShieldBlocked`
attack maps to the clash slot for its weapon while `Landed`, `Parried`,
`Deflected`, and `Evaded` keep the weapon impact slot.
`SoundDirectorTests.Ingest_UsesANullHitClassForAShieldBlockDespiteTheHitLocation`
proves the director derives the hit class from the mapped slot rather than from
the event, which is what keeps a clash cue from resolving `Missing` forever.
`SoundLogPanelTests.ClampBindingScroll_ReachesTheLastRow`,
`ClampBindingScroll_RefusesToScrollPastEitherEnd`,
`ClampBindingScroll_ReturnsZeroWhenEveryRowFits`,
`GetWheelTarget_RoutesTheWheelToTheListUnderThePointer`, and
`GetWheelTarget_FallsBackToTheCueListOutsideBothLists` prove the scroll
arithmetic and the wheel routing as pure functions.
`CalculateLayout_FitsExactlyTenBindingRowsAtFourHundredAndSixteen` and
`CalculateLayout_CapsTheBindingViewportAtTheSlotCountRegardlessOfHeight` pin the
layout numbers.

None of that proves what these rows are for. No test hears a sound, so no test
can say whether a shield block reads as wood rather than as flesh, whether the
four weapons are audibly distinct, or whether the cue becomes a wall of noise in
a full battle. No test drives a real mouse wheel over a real panel, so none
proves that the wheel reaches the right list on screen or that it does not leak
into the arena camera. No test renders anything, so none proves that the battle
event log below the taller sound log is still readable. The sixteen clash takes
do not exist yet either — they are generated by hand in a later step — so every
row that expects `READY (4)` is blocked until that generation happens.

| # | Step | Expected | Result | Status |
| --- | --- | --- | --- | --- |
| 172. Listen to a shield-blocked blow | It sounds like a weapon striking a light wooden board, and it is plainly different from a landed cut. The difference is audible on its own, without reading the event log to find out which resolution occurred. | Not run | PENDING |
| 173. Compare the four clash slots by ear | The War Axe reads heavier and blunter than the Work Blade against the same shield, and the Work Blade is the quietest of the four. | Not run | PENDING |
| 174. Scroll the expected-files list | Open the sound log, put the pointer over the expected-files list, and scroll. The list moves through all thirty-seven rows, reaches the four clash slots at the bottom with each one reading `READY (4)`, refuses to scroll past either end, and shows no `+N more` line anywhere. Scrolling with the pointer over the cue log below still scrolls only the cue log, and neither scroll zooms the arena camera. A run with `-LogLevel dbg` whose `assets.sound.scanned` line reports thirteen slots and thirteen ready is a secondary confirmation of the same fact. | Not run | PENDING |
| 175. Run a full 200-agent battle with the shield cue audible | The shield cue does not become a wall of noise, and the cue log shows no `LIMITED` or `REFUSED` row for any clash slot. | Not run | PENDING |
| 176. Read the battle event log with the sound log open | At the sound log's new height the battle event log still reads: the selected-event pane shows its header and both detail lines, and nothing is clipped. **This row is the only check on the event-log cost of the 65 percent change.** `BattleEventLogPanel`'s layout constants are private and `ArenaGame` is banned from tests, so no automated test covers it. | Not run | PENDING |

## Leader marker and inspector annotation smoke (leader rank plan L4/L5)

**No interactive run was performed for this change.** Every row below is
`PENDING`. `ExactlyOneLivingLeaderPerNonEmptyContingentAcrossEveryRegisteredMovementPreset`
in `BattleSimulationTests` and the `AgentInspectorContentTests` assertions
prove that `AgentView.IsLeader` is wired correctly and that the inspector's
contingent line carries the `(leading)` suffix exactly when it should; neither
proves that the pawn marker reads as intended on a real battlefield at
default zoom, that it does not clash visually with the selection ring or the
adornment accent, or that it visibly changes pawn the tick a contingent's
ranking member dies.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| L-1 | Look at the battlefield at default zoom under a contingent-aware movement preset (`PersistentContingentsV2` through `V5`) | Exactly one warrior per visible, non-empty contingent shows the leader mark above its head | | PENDING |
| L-2 | Watch a contingent whose leader is killed | The leader mark visibly moves to a different warrior once the next scan reassigns leadership | | PENDING |
| L-3 | Select the current leader | The selection ring and the leader mark are both visible at once, not fighting for the same screen space | | PENDING |
| L-4 | Watch the leader die in the event feed, before the next scan | The dead mark (crossed lines) and the leader mark are both visible on that one warrior for that one tick | | PENDING |
| L-5 | Click the current leader to open the inspector | The contingent line reads `Contingent: {id} — {label} (leading)` | | PENDING |
| L-6 | Click a non-leader member of the same contingent | The contingent line carries no `(leading)` suffix | | PENDING |
| L-7 | Launch under `IndependentPursuitV1` | No warrior ever shows the leader mark, and no inspector contingent line ever carries `(leading)` | | PENDING |

## Footwork pressure interrupt smoke (movement V7 plan F1)

**No interactive run was performed for this change.** None of the rows below has
ever been executed. Nine of the ten are `BLOCKED` rather than `PENDING`, for the
reason set out below; only the legacy-regression row P-10 is `PENDING`.

What the automated tests already prove, and what they do not:
`FootworkPressureInterruptTests` covers the `ShouldPressureInterrupt` predicate
in isolation — the transition-only guard, each signal alone, saturation, and
threshold equality. `MovementStateHashTests` proves the version gate rather
than the field is what moves the two hashes.
`ComboChainPressureInterruptTests` proves an interrupted warrior's combination
chain is cleared and its cooldown is `AttackCooldownTicks`.
`MovementViewProjectionTests` proves a V7 view carries live pressure values and
a V6 view carries the defaults. `AgentInspectorContentTests` proves both new
inspector strings and the panel height arithmetic, and `PawnRendererTests`
proves the break-off mark's placement geometry against the leader mark and the
selection ring.

None of those proves that a spectator watching a real battlefield at default
zoom can see a warrior peel out of a losing knot, that the break-off mark reads
as distinct from the leader mark and the dead mark at 1× speed rather than only
in placement arithmetic, or that the two inspector rows are legible at their
shipped colour and position.

**These rows cannot be executed today, and that is a property of the build, not
an omission by the person reading this.** `MovementPresetId.EquipmentRelativeFootworkV7`
is reachable only by explicit selection, `Scenario.MovementPreset` remains
`PersistentContingentsV4` under decision D6, and the client exposes no
movement-preset selector — `ArenaGame.BuildScenario` calls
`Scenario.CreateDefault` and overrides only `RosterCounts`, so the client always
runs the shipped default. Under that default `AppliesPressureInterrupt` is
`false`, all three new `AgentView` members stay at their defaults, no mark is
ever drawn, and no pressure row ever renders. A human at an interactive desktop
therefore has no supported route to a V7 battle in the game window.

**Why `BLOCKED` and not `PENDING`.** `PENDING` asserts that a check has not been
run yet. That would be false here: these nine checks *cannot* be run by anyone,
and recording them as merely not-yet-done would misrepresent the state of the
work to the next reader. `CLAUDE.md` section 6 is explicit that a blocked row is
reported honestly as blocked. This is not a gap in V7's implementation — the
three spectator channels are built, unit-tested, and will apply unchanged to
whatever interrupt-applying preset eventually becomes selectable. It is a gap
between the feature and the player, and the honest record of it is `BLOCKED`.

These rows become executable the day any preset with
`AppliesPressureInterrupt = true` can be selected from the client, whether by a
preset selector or by the default moving. Neither is authorized by this
workstream: decision D6 moves the default only once the termination bar passes,
and section 7 of the calibration record establishes that V7 never will. When
that day comes, the rows are already worded and waiting. Until then no row may
be flipped by anyone, agent or human, who has not actually seen the screen.

The rows below also assume a V7 battle that reaches `Commit` or `Recover` often
enough to interrupt. The calibration record measures the interrupt firing on
well under one per cent of agent-ticks, so a spectator may have to watch for
some time; a row that sees nothing is evidence about frequency, not
automatically a failure of the mark.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| P-1 | Watch a V7 battle at default zoom and 1× speed | A warrior that breaks off under pressure shows the break-off mark above its head, and the mark is noticeable at 1× without pausing or zooming | | BLOCKED |
| P-2 | Watch a warrior that is losing a local fight — outnumbered, taking hits, allies dying around it | It visibly peels out of the knot, and a spectator can tell that it chose to disengage rather than that it died or was pushed. **This is the section 10 discoverability row: the effect must be readable without reading source code.** | | BLOCKED |
| P-3 | Find a warrior showing both the break-off mark and the leader mark | Both are visible at once and neither is hidden by the other | | BLOCKED |
| P-4 | Select a warrior showing the break-off mark | The selection ring, the leader mark where present, and the break-off mark are all legible together, none fighting for the same screen space | | BLOCKED |
| P-5 | Watch a warrior carrying the break-off mark as it is killed | The dead mark and the break-off mark do not merge into an unreadable smear on that warrior | | BLOCKED |
| P-6 | Click a warrior that has just broken off | The footwork row reads `Footwork: Disengaging (broke off under pressure)`, distinct from an ordinary `Footwork: Disengaging` | | BLOCKED |
| P-7 | Click any warrior in a V7 battle | The pressure row reads `Pressure: {value} of {threshold} basis points to break off`, and the value visibly moves as the warrior's local situation changes | | BLOCKED |
| P-8 | Click warriors carrying each of the six weapon rows | Each shows its own threshold, and the ordering matches the shipped values — Kampilan and Wasay highest, Itak lowest | | BLOCKED |
| P-9 | Compare an ordinary `Disengaging` warrior with a broken-off one | The two footwork rows are distinguishable at a glance, not only by careful reading | | BLOCKED |
| P-10 | Legacy regression: launch under `PersistentContingentsV4` | No warrior ever shows the break-off mark, and no inspector line ever carries the pressure row. This is the L-7-equivalent row: it proves the feature is gated, and it is the one row here that **is** runnable today, because V4 is the shipped default | | PENDING |

## GPU render smoke (gpu-render Phases 1 and 2)

**No interactive run was performed for this change.** Every row below is
`PENDING`. These rows were drafted in the plan on 2026-07-28 and moved here on
2026-08-07; they were never in this file while the workstream ran, which is why
no human has worked from them. The archived plan is
[2026-07-28-gpu-render.md](../archives/2026-08-07/gpu-render/2026-07-28-gpu-render.md)
and this copy is now the live one.

What the automated work already proves, and what it does not: the render probe
recorded a 1,000-unit default-fit `Draw` p95 of 3 276.6 us against an 8.0 ms
budget, `PawnGeometryTests` pins the two-stage geometry path bit-identical to the
entry points it replaced over a 73,728-case grid, `PawnQuadCountTests` still pins
17, 19, 20 and 40 quads, `PawnAppearanceCacheTests` proves cold-cache equivalence
and the capacity bound, `HitEffectSystemTests` proves the per-frame pulse lookup
returns what the per-pawn scan returned, and `ArmyCompositionStepperTests` proves
the stepper clamps at 500 per team. None of that proves that a 1,000-unit battle
is watchable rather than merely measurable, that the composition panel still fits
the window at the new maximum, or that Phase 2 changed no pixel — which is the
one claim the whole phase rests on.

Only a human running `./scripts/run.ps1` on an interactive Windows desktop may
flip one of these rows. Compilation, unit tests, and a window-opening probe run
do not. Leave untouched rows `PENDING`; report `BLOCKED` honestly.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| GR-1 | Launch the game normally with `./scripts/run.ps1` | The window still runs with vertical retrace enabled; no tearing appears and frame pacing is unchanged. The retrace override from GPU-006 is probe-only and must never reach a normal launch | | PENDING |
| GR-2 | Open the army composition panel and raise a team to 500 | The stepper reaches 500, refuses to go higher, and every row and both buttons stay fully on screen | | PENDING |
| GR-3 | Start a 1,000-unit battle (500 per team) and watch one full engagement | The battle renders and remains watchable; pawns, shields, swings, and hit pulses all read correctly at all three camera stations | | PENDING |
| GR-4 | Compare a seed-1 200-unit battle before and after the Phase 2 commits at the same tick and camera station | No visible difference. Phase 2 is pure removal of duplicated work; any visible difference is a defect, not a new baseline | | PENDING |
| GR-5 | Watch hit pulses in a dense 1,000-unit melee | Pulse strength and timing read exactly as before the per-frame lookup replaced the per-pawn scan | | PENDING |

Phase 3's rows GR-6 through GR-10 are deliberately absent. They covered the
instanced backend, which the NO-GO verdict closed and which does not exist.

## Leader identification smoke (leader character plan L7)

**No interactive run was performed for this change. Every row below is
`PENDING`.** The automated tests prove preset gating, the cache key, the mark
geometry, the quad accounting, and the inspector row. None of them prove that a
person watching a battle can pick a leader out.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| LC-1 | Start a battle and watch it at default zoom without clicking anything | Roughly sixteen warriors carry a mark above their head whose shape differs from every other pawn's outline, not merely its colour | | PENDING |
| LC-2 | Zoom all the way out to the Low detail tier | The leader marks are still findable; they do not vanish into the mass or read as rendering noise | | PENDING |
| LC-3 | Zoom in on one marked warrior in the Visayan-block faction | It wears datu kit — gold-edged head wrap, gold earrings and necklace, a draped shoulder cloth, a red waist sash — and its immediate neighbours do not | | PENDING |
| LC-4 | Zoom in on one marked warrior in the Tagalog-block faction | It wears chief or leader kit; if it is the red-chinina row, the red jacket is the single clearest cue at that zoom | | PENDING |
| LC-5 | Zoom in on a marked warrior in a Northern Luzon or generic-levy faction | It looks like its neighbours; the above-head mark plus the inspector are the only identification. This is the designed outcome, not a defect | | PENDING |
| LC-6 | Watch until a marked warrior dies | Exactly one other warrior in the contingent picks up the mark, and its appearance changes once, cleanly, without flickering back and forth on subsequent frames | | PENDING |
| LC-7 | Click the marked warrior | The inspector states that it is leading, and further down names the appearance preset with its scope, tag, and evidence tier — for example "Visayan Datu", Visayan, Documented, form uncertain | | PENDING |
| LC-8 | Click the marked warrior, then hover a second one, while a third is breaking off under pressure | The leader mark, the selection ring, and the break-off band are all visible and none overlaps another | | PENDING |
| LC-9 | Click a warrior in a battle running the frozen `IndependentPursuitV1` preset, where `ContingentState` is always `None` | No leadership row appears, because no leader is elected under this preset — if one somehow is elected, the row appears rather than being silently dropped | | PENDING |
| LC-10 | Watch a full battle to the end and open the battle report | The report is unchanged; its "Leaderboard" still ranks kills and makes no claim about contingent leadership | | PENDING |
| LC-11 | Run the same seed twice and compare the same warrior at the same tick in both runs | Identical appearance and identical leader marks; nothing about who leads or how they look differs between the two runs | | PENDING |
## Movement gait animation smoke (2026-08-07)

**No interactive run was performed for this change.** Every row below is
`PENDING`. The automated tests prove the pose mathematics, the per-entity
store, the leg and foot rectangles, the detail-tier gating, the quad
accounting, and the wiring; none of them prove that a warrior on screen looks
like it is walking, that a run reads differently from a walk, or that two
hundred warriors advancing together do not read as a marching band. Design:
`docs/plans/2026-08-07-movement-gait-animation-design.md`.

The restructured body is what makes this section load-bearing rather than
routine. The torso was shortened from twelve layout units to eight so the legs
could take a real share of the silhouette, which moved the head, the shield,
the armor, the sash, and the adornment accents up by six pixels at the test
fixture's scale. Nothing automated can say whether the result still reads as a
warrior.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| GA-1 | Watch one warrior cross open ground at default zoom | The legs visibly alternate and the feet lift and plant; the warrior does not slide | | PENDING |
| GA-2 | Compare a warrior closing on a target against one holding position | The moving one steps; the stationary one stands with both feet planted | | PENDING |
| GA-3 | Watch a fast advance and a slow one | The fast gait reads as a run — longer stride, higher foot lift, forward lean — not merely as the same walk played faster | | PENDING |
| GA-4 | Watch a contingent advance together at default zoom | The warriors do not all step on the same foot at the same moment | | PENDING |
| GA-5 | Pause mid-advance | Every leg freezes where it was; nothing keeps moving and nothing snaps to a neutral stance | | PENDING |
| GA-6 | Run the battle at 2x and at 4x | The step cadence speeds up with the battle; no warrior appears to skate or to run in place | | PENDING |
| GA-7 | Zoom out to the lowest detail tier | Legs and feet disappear cleanly; the pawn falls back to the ground ring with no flicker at the tier boundary | | PENDING |
| GA-8 | Zoom in to the highest detail tier | The feet are distinguishable from the legs and read as bare feet | | PENDING |
| GA-9 | Set motion to Reduced, then to Off | Reduced keeps the legs moving with a shorter stride; Off leaves the legs drawn and completely still | | PENDING |
| GA-10 | Watch a warrior die mid-stride | The corpse does not continue stepping and does not run in place | | PENDING |
| GA-11 | Look at any warrior standing still, at default zoom | The restructured body still reads as head, torso, and legs — not as a head on stilts or a torso with stumps | | PENDING |
| GA-12 | Watch a shield bearer advance | The shield still reads as covering chest and abdomen, and no swinging leg crosses or hides it | | PENDING |
| GA-13 | Watch a warrior attack while moving | The swing and the gait compose without the body jumping between two poses | | PENDING |
| GA-14 | Watch a battle at 200 agents from minimum zoom | The formation still reads as a formation; leg motion has not turned the field into noise | | PENDING |

## Ranged units smoke (ranged-units package, task RU-32)

**No interactive run was performed for this change. Every row below is
`PENDING`.** The ranged-units package adds three ranged weapons — the Bangkaw
(`Bangkaw — Long Spear`, thrown), the Busog (`Busog — War Bow`), and the
Imported Arquebus (a matchlock, carrying the `IMPORTED` badge rather than a
Filipino pair-form label because no source ties the weapon to a Philippine
name) — together with a hitscan projectile that carries a flight time, a
five-phase draw/load/release/recover cycle, a movement rule that holds a
ranged warrior at its preferred distance instead of closing to melee, and
thirteen new sound slots split across the three weapons. The automated suites
prove the countdown resolves on the correct tick, that the state and event
hashes move only for a ruleset that fields a ranged weapon, that
`AgentIntent.Holding` and a rejected route are written by independent code
paths, and that the pose geometry and the inspector strings are wired and
tested in isolation. None of that proves any of it reads correctly to a
person watching the screen, which is the only thing the rows below are for.

Two things are true about the current state of this package and both bear
directly on these rows. First, a real `./scripts/run.ps1 -Configuration
Debug` run built a 500-agent `PrecolonialPhilippinesV5` scenario and rendered
52 seconds at 185 fps with zero `err` lines in the debug log, so the game
does launch and does render ranged pawns without crashing or logging an
error — but `simTicks` stayed 0 on every frame line of that run, meaning the
battle never actually advanced a single tick. `RangedPhase` has therefore
never been observed in a non-`None` state at runtime, and
`WeaponAngleRadians`, `ExtensionRatio`, and `DrawTension` have never taken a
non-zero value outside a unit test. Rows RG-1, RG-2, RG-3, RG-4, RG-5, RG-6,
RG-8, and RG-10 below depend on exactly those runtime values and have
therefore never been seen by anyone, agent or human; nothing above should be
read as implying otherwise. Second, the sixty sound files task RU-31
generates — including every `release-<weapon>`, `attack-<weapon>`,
`clash-shield-<weapon>`, `miss-<weapon>`, and `misfire-arquebus` file the
rows below reference — do not exist yet, because RU-31 is a paid, human-run
task that has not been executed. Any row below that depends on a cue says so
plainly and still ships `PENDING`, not `BLOCKED`: the row itself is not
blocked by any defect, the attempt to run it is simply not yet possible
until those files land.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| RG-1 | Watch a Bangkaw, Busog, or Arquebus warrior fire at a target several world units away, at default zoom | A projectile is visibly drawn traveling from the launcher toward the target and exists on screen for multiple ticks before impact, rather than the target reacting the instant the release plays. Failure is a shot that resolves with no visible projectile at all, or one that appears to teleport instantly from launcher to target | | PENDING |
| RG-2 | Listen to one ranged shot from release to impact, at default zoom and 1x speed | A release cue plays at the launcher, then a separate impact or miss cue plays at the target after a perceptible gap, and that gap reads as the shot's flight time rather than as two disconnected sounds. Failure is the two cues sounding simultaneous, the gap sounding random rather than distance-related, or only one of the two cues playing. Cannot be attempted until the release and impact/miss sound files from RU-31 exist | | PENDING |
| RG-3 | Watch one Bangkaw warrior go through a full ready, load, draw, release, recover cycle at default zoom, close enough to see the weapon | The sequence reads as a spear being thrown: the shaft draws back past the shoulder during Draw, then releases forward and returns to a neutral carry during Recover. Failure is a Bangkaw sequence that reads as a generic swing, or one that shows no visible change in weapon angle across the five phases | | PENDING |
| RG-4 | Watch one Busog warrior go through a full ready, load, draw, release, recover cycle at default zoom, close enough to see the weapon | The sequence reads as a bow being drawn: the bow stave holds out from the body while the string hand draws back toward the cheek during Draw, then both return toward Ready after Release. Failure is a Busog sequence indistinguishable from the Bangkaw's throwing motion, or one that shows no build-up of draw tension before Release | | PENDING |
| RG-5 | Watch one Imported Arquebus warrior go through a full ready, load, draw, release, recover cycle at default zoom, close enough to see the weapon | The sequence reads as a matchlock being fired: the weapon is shouldered and levelled, held on target through Release rather than swept quickly, with a long barrel plainly visible out in front of the warrior. Failure is an Arquebus sequence that reads as a spear or a bow, or one indistinguishable from the other two ranged weapons at a glance | | PENDING |
| RG-6 | Watch a ranged warrior (Bangkaw, Busog, or Arquebus) approach its standoff distance from a target during an advance, alongside melee warriors closing on the same line | The ranged warrior visibly halts and holds its position once it reaches range, while melee warriors on the same approach keep walking forward and pass it. Failure is the ranged warrior continuing to close all the way to melee range like its comrades, or halting at a point indistinguishable from where a melee warrior would stop on its own | | PENDING |
| RG-7 | Click a ranged warrior that has halted at its standoff distance and read its inspector panel | The intent row reads "Intent: Holding at range", not "Blocked" and not any other movement-refusal wording. Failure is the inspector showing "Blocked" — the movement row's own wording for a warrior whose route was rejected — for a warrior that is in fact deliberately choosing not to close | | PENDING |
| RG-8 | Watch and listen to a ranged shot that resolves as a miss rather than a landed hit | A miss cue plays instead of the ordinary flesh-impact cue used for a landed blow. Failure is a missed shot playing the same body-hit sound as a hit would, or playing no sound where a miss cue exists for that weapon. Cannot be attempted until the miss-`<weapon>` sound files from RU-31 exist | | PENDING |
| RG-9 | Compare a Bangkaw, a Busog, and an Arquebus warrior side by side at the High, Medium, and Low detail tiers, from a close-up zoom down to fully zoomed out | At every tier the three ranged silhouettes are distinguishable from each other and from the four existing melee silhouettes — the Bangkaw reads as spear-armed, the Busog as bow-armed, the Arquebus as carrying a long firearm. Failure is any two of the three collapsing into the same silhouette at the Low tier, or a ranged warrior being mistaken for a melee warrior at any tier | | PENDING |
| RG-10 | Watch and listen to a battle fielding all three ranged weapons for several minutes | The Arquebus fires far less often than the Bangkaw or the Busog, matching its much longer authored shot interval, and each Arquebus shot is audibly louder and more distinctive than a Busog release or a Bangkaw throw — a spectator should be able to tell an Arquebus has fired without seeing which warrior fired it. Failure is the Arquebus firing at a cadence similar to the other two ranged weapons, or its report sounding unremarkable next to theirs. Cannot be fully attempted until the release-arquebus and attack-arquebus sound files from RU-31 exist; the firing-cadence half of this row does not depend on sound and can be attempted once RG-1 is attemptable | | PENDING |
| RG-11 | Watch a Bangkaw or Busog shot whose flight path passes through or near a friendly warrior standing between the launcher and the target | **This row has no pass/fail criterion; it is an open question, not a check.** Phase 1 deliberately implements no friendly fire and no line of sight — a projectile resolves as a pure distance-and-timer hitscan against its chosen target, with nothing checked about who or what stands between launcher and target — and that gap is deferred to Phase 2 by design, not an oversight to correct here. Record in `Actual` whatever was actually observed: does the projectile visibly passing through the friendly warrior look wrong to a spectator, or does it go unnoticed at the pace and scale of a real battle? This is the one Phase 1 effect a spectator cannot discover for themselves through any other row above, which is why it needs a person to look at it deliberately rather than being inferred from the others | | PENDING |
## Projectile props and embedded projectiles smoke (2026-08-11)

**No interactive run was performed for this change.** Every row below is
`PENDING`. The automated tests prove the three silhouettes are mutually
distinct, that the prop is centred on the shot rather than anchored at the
launcher, that it rotates to the direction of travel, that every one of the
thirteen body parts resolves to an anchor inside the host's own visual bounds,
that a shield block attaches to the shield rather than to the body part it also
carries, that the pool never exceeds 256 slots and evicts oldest-first, and
that the quad budget still fits. None of them prove that a spectator can tell a
spear from an arrow from a lead ball while a battle is running, or that a stuck
arrow reads as stuck rather than as a smear. Plan:
`docs/archives/2026-08-11/2026-08-11-projectile-props.md`.

Rows PP-4 through PP-7 need a battle that actually lands ranged hits, so they
cannot be attempted before RG-1 is attemptable.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| PP-1 | Watch a single Bangkaw or Busog shot from release to impact at default zoom, following the projectile with your eye | The drawn object is a small prop of fixed length that travels from launcher to target, and its length does not change during the flight. Failure is a line that grows out of the thrower and is longest at the moment of impact, which is the defect this change exists to fix | | PENDING |
| PP-2 | Compare a Bangkaw shot, a Busog shot, and an Arquebus shot in flight, at default zoom | The three are distinguishable in the air without seeing who fired them: the spear is the longest and carries a visible head at its leading end, the arrow is markedly shorter with a pale fletched tail, and the arquebus shot is a small ball with no shaft at all. Failure is any two of the three reading as the same object in flight | | PENDING |
| PP-3 | Watch a shot in flight while zooming from close in to fully zoomed out | The projectile stays visible at every zoom, including the most pulled-out one. Failure is a shot that scales down to nothing and disappears — the in-flight prop is deliberately never detail-gated, because at low detail it may be the only sign a ranged unit exists | | PENDING |
| PP-4 | Watch a Busog shot land on an unshielded warrior, at a zoom close enough to see the pawn's body clearly | An arrow is left standing in the warrior, at the part of the body the shot struck, with its fletched tail outward. Failure is no arrow appearing, or one appearing somewhere unrelated to where the blow landed | | PENDING |
| PP-5 | Watch a Bangkaw or Busog shot that a tall hardwood shield blocks | The projectile is left standing in the shield face rather than in the warrior behind it. Failure is an arrow appearing in the body of a warrior whose shield stopped the shot | | PENDING |
| PP-6 | Find a warrior carrying at least one embedded projectile and follow it while it walks across the field | The projectile rides with the warrior, holding its position on the body and its angle, rather than staying behind at the spot where the hit occurred or sliding around the pawn. Failure is a projectile that detaches, drifts, or re-rolls its angle from frame to frame | | PENDING |
| PP-7 | Watch a warrior carrying embedded projectiles while zooming out to a wide view of the whole battle, then zoom back in | The embedded projectiles stop being drawn as the camera pulls out and reappear on zooming back in, and the warriors themselves are unaffected. Failure is embedded projectiles still drawing at the widest zoom — they are deliberately detail-gated, unlike the in-flight prop — or the pawns changing in any other way as the gate crosses | | PENDING |
| PP-8 | Watch an Arquebus shot land on a warrior | Nothing is left standing in the wound. Failure is a shaft appearing for a weapon that fires a lead ball | | PENDING |

## Attack animation V2 smoke (2026-08-08)

**No interactive run was performed for this change.** Every row below is
`PENDING`. The automated tests prove the weapon-motion catalog, the
contact-latched timeline, the target-local geometry, the articulated arm
rectangles, the defender reaction offsets, the shield overlay legality, the
motion-intensity policy, the quad accounting, and the conservative cull's
containment of all of it. None of them prove that a Kampilan reads differently
from a Kalis on screen, that a blow appears to land on the warrior it names, or
that a dense battle of two hundred warriors striking at once reads as combat
rather than as noise. Design:
`docs/plans/2026-08-08-attack-animation-v2-design.md`.

**Interactive runs, 2026-08-09.** Two runs against `codex/attack-animation-v2`
at `3a63bb1`, both `Debug`/`dbg`, fullscreen 2048x1152, the shipped 500-agent
default scenario. Logs:
`artifacts/logs/hukbo-20260808-214856-3108.jsonl` (one battle, 107 s) and
`artifacts/logs/hukbo-20260808-215507-26172.jsonl` (two battles, 224 s, three
pause cycles, one Next Round). Across both: 6 386 Itak, 4 934 Kalis, 4 284
Kampilan and 2 805 Wasay attack cues, 1 478 deaths, and **no `warn` or `err`
line of any kind** — in particular no `render.attack.contact.collapsed`, so the
five-bundle per-attacker buffer never overflowed.

Rows below carry what the observer reported. Where an expectation could not be
attributed to an individual exchange, the row stays `PENDING` rather than being
credited from the log: the log proves an event occurred, never that a person
could read it.

The render probe measured the attack path directly at 200, 500, and 1 000
agents across all three camera stations, with every station recording at least
one frame holding an active attack pose (peaks of 2 to 20 poses per frame):
`artifacts/attack-animation-v2/render-matrix.json`. That is a performance
measurement, not a visual one, and it flips no row below.

| ID | Action | Expected | Observed | Result |
| --- | --- | --- | --- | --- |
| AA-1 | Watch a Kampilan warrior strike at close zoom | The broadest of the four arcs, both hands on the blade, a planted weight transfer | Reads as the broad one. Confirmed as part of the four-way comparison rather than in isolation. | PASS |
| AA-2 | Watch a Wasay warrior strike at close zoom | The head arrives late and stops hard; the support hand anchors the haft; the longest recovery of the four | Reads as the late, heavy one. Confirmed as part of the four-way comparison rather than in isolation. | PASS |
| AA-3 | Watch a Kalis warrior strike at close zoom | A mostly linear extension toward the target rather than a broad cut, with the fastest return | Reads as the linear one. Confirmed as part of the four-way comparison rather than in isolation. | PASS |
| AA-4 | Watch an Itak warrior strike at close zoom | The shortest, quickest chop, alternating side between consecutive blows | Reads as the short one. The combo side alternation was not separately confirmed. | PASS |
| AA-5 | Watch each of the four weapons at 1x, 2x, and 4x | Every blow stays individually visible; nothing blurs into a single continuous motion at 4x | | PENDING |
| AA-6 | Watch a blow that lands | The weapon reaches the named target, blood and the defender's recoil arrive on the same frame as the weapon | The blow lands on the warrior it names, with blood and recoil on the same frame. | PASS |
| AA-7 | Watch a blow a shield blocks | The defender braces into the contact rather than being driven back, and the clash reads on the shield | Outcomes look distinct, but the observer reported being unable to follow which outcome resolved which exchange in a live 500-agent battle. Not certifiable at this density. | PENDING |
| AA-8 | Watch a parried blow | Attacker and defender weapons visibly meet and redirect across the line of the blow | As AA-7: distinctness observed, individual attribution not possible at this density. | PENDING |
| AA-9 | Watch a deflected blow | A shallower glance than the parry, continuing rather than reversing | As AA-7: distinctness observed, individual attribution not possible at this density. | PENDING |
| AA-10 | Watch an evaded blow | Full follow-through with no blood, no clash cross, and no contact recoil | As AA-7: distinctness observed, individual attribution not possible at this density. | PENDING |
| AA-11 | Watch a two-blow combo from one warrior | The second contact installs a new blow rather than restarting the first; the return side changes | | PENDING |
| AA-12 | Watch a lethal blow at close zoom | The victim stays visible long enough for the weapon to reach it, then falls; it does not vanish before contact | | PENDING |
| AA-13 | Watch a shielded Kalis warrior strike (registered V2 replay) | The block stays between the defender and the weapon line; the weapon arm does not cross or hide it | | PENDING |
| AA-14 | Watch a shielded Itak warrior strike (registered V2 replay) | As AA-13, with the compact chop rather than the thrust | | PENDING |
| AA-15 | Watch attacks at Low, Medium, and High detail | Low keeps direction and outcome with no arms and no trail; Medium and High draw the full rig | Articulated arms are present but reported as "not significantly seen" at the zoom used. The three tiers were not compared against each other. | PENDING |
| AA-16 | Set motion to Full, then Reduced, then Off | All three keep direction, reach, and which outcome resolved the blow; Reduced damps the body; Off removes the trail entirely | | PENDING |
| AA-17 | Pause on the frame of a contact | The pose, the effect, the reaction, and the sound freeze together; nothing advances while paused | Everything freezes together. Three pause/resume cycles during combat, at ticks 94, 119 and 147. | PASS |
| AA-18 | Pause during a catch-up burst, then resume | Queued contacts resume in order and none is duplicated or lost | | PENDING |
| AA-19 | Next Round, then Full Reset, during active combat | Every attack pose, pending contact, reaction, and transient effect is cleared by both | Next Round exercised and the second battle ran clean; Full Reset was never triggered. | PENDING |
| AA-20 | Watch a 200-warrior battle at close zoom | Individual exchanges are readable; the arms and trails do not obscure who is fighting whom | | PENDING |
| AA-21 | Watch a 200-warrior battle at default fit | The formation still reads as a formation | | PENDING |
| AA-22 | Watch a 500-warrior stress battle at minimum, default-fit, and maximum zoom | Frame pacing stays comfortable and the field does not turn into visual noise at any of the three | **Animations overlap and the battle reads as chaos**; the observer could not tell what was happening. Frame pacing was not reported as a problem. Two full 500-agent battles. | FAIL |
| AA-23 | Watch a warrior strike while moving | The attack plants the stance and composes with the stride; the body does not jump between two poses | | PENDING |
| AA-24 | Watch a warrior at the edge of the arena panel strike outward | The weapon does not pop in or out at the panel edge as the blow extends | | PENDING |

