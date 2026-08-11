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

188 rows across 22 subsections: **176 `PENDING`, 10 `BLOCKED`, 1 `FAIL`,
1 `DECLINED`**, counted from the status column of this file on 2026-08-12,
after the improve-visuals smoke run closed 29 of its 32 rows and they were
lifted out, after `SD-1` was re-checked and closed on 2026-08-11, after the
Sandata fixes of the same day moved four `BLOCKED` rows and one `FAIL` row to
`PENDING`, and after `SD-2`, `SD-7a`, and `SD-8` were re-checked and closed on
2026-08-12.

**There is no `PASS` column any more, and that is deliberate.** 55 passing rows
have been lifted out — 52 on 2026-08-11 (22 of them from families that stayed,
then 29 more when both improve-visuals families were run for the first time,
then `SD-1` when the tester re-checked it), and 3 more on 2026-08-12 (`SD-2`,
`SD-7a`, and `SD-8`, re-checked and closed the same way). Every row
in this file is now something a person still has to do: 176 never attempted or
awaiting a re-run, 10 that cannot be attempted until the build changes, 1 that
was attempted and failed, and 1 declined. If a `PASS` ever appears here again it is a row that
has just closed and has not yet been lifted — not a row that belongs.

**Recount before trusting that total.** Every figure here that was ever taken
on faith turned out to be wrong. Count the status column itself, and count
every status — a row that is neither `PENDING` nor a result is still a row.

**This file holds open work only.** It is not a record of what has been tested.
A family every one of whose rows is `PASS` is deleted from this file outright:
it is a record rather than a checklist, and keeping it makes the file longer
without giving a tester anything to do. A family is deleted only when it is
entirely `PASS` — an open `FAIL` or `BLOCKED` row is unfinished work and stays
here where a reader will see it. Until earlier on 2026-08-11 the Sandata
section was the standing example of a family kept for that reason alone, having
no `PENDING` rows at all: it held 2 `FAIL` and 5 `BLOCKED`. The fixes of the
same day left it with 5 `PENDING` re-runs and 1 `BLOCKED`, which is ordinary
open work. Three of those five re-runs — `SD-2`, `SD-7a`, and `SD-8` — closed
`PASS` on 2026-08-12, leaving 2 `PENDING` and 1 `BLOCKED`.

**Two families were deleted whole earlier the same day.** Spectator clarity, all
fifty-two rows, and collision readability, all seven, were closed by a person at
an interactive desktop on 2026-08-11 and left together. Their record is the
2026-08-11 archive titled **"Spectator clarity and collision readability smoke"**,
found the same way as the record named below.

**A single passing row is lifted out the same way, without its section.** Five
sections still carrying open work had rows that closed — Sandata, the `UI`
family, the persistent-contingent section, attack animation V2, and the
improve-visuals families, whose two sections were merged into one when 29 of
their 32 rows left. The first 22 of those rows left on 2026-08-11 while their
sections stayed, and the improve-visuals 29 left the same day. Each section names,
in its own preamble, which of its rows closed and what to be careful of when
reading the archived result: two of them closed under a preset or a viewport
that is no longer the shipped one, which is exactly the trap an undated `PASS`
sets for the next reader.

**No file in this repository may link to `docs/archives/`.** That folder is
deleted periodically, so a link into it is a link that breaks. Nothing in this
checklist points there, and nothing added to it may. Where a closed row's
evidence is worth naming, it is named in prose — the 2026-08-11 record
**"Closed rows lifted out of families that are still open"** is referenced that
way in four sections below. Find such a record by its title rather than by a
path, so that a later prune costs a search instead of the evidence:

```powershell
git log --diff-filter=A --name-only --format='%h %s' -- 'docs/archives/**' |
  Select-String 'closed-rows-from-open-families'
```

**A fixed row goes back to `PENDING`, never straight to `PASS`.** A row keeps
its `FAIL` observation in `Actual` when it reopens, so the re-run is judged
against what was actually seen. An agent may write the fix; only a person may
close the row.

The families below are grouped by what a single launch can actually
show, because the subsections are ordered by the change that created them
rather than by what is on screen at once, and a person working down the file in
order relaunches the game far more often than they need to.

| Batch | Families | Rows | What one launch has to show |
| --- | --- | --- | --- |
| Ranged | `PP` 8, `RG` 11 | 19 `PENDING` | A battle fielding Bangkaw, Busog, and Arquebus warriors. The shipped client runs combat preset V5 and movement preset V8, so ranged units are on the field by default at roughly a 14 per cent share |
| Pawn animation | `AA` 18 of 24, `GA` 14 | 31 `PENDING`, 1 `FAIL` | Warriors striking and walking, close in. `AA` also holds the one open `FAIL`, AA-22, and its other 6 rows passed and were lifted out |
| Markers | `LC` 11, `L` 7 | 18 `PENDING` | Leaders and contingents at default zoom, plus the agent inspector |
| Render | `GR` 5 | 5 `PENDING` | Launch-time render behaviour |
| Battlefield realism | task 18 rows | 10 `PENDING` | Cohort deployment and the V10 retreat rung |
| Menu, display, motion | `UI` 3 of 16 | 3 `PENDING` | Run on 2026-08-11; the other 13 passed and were lifted out. The three open rows, `UI-2`, `UI-4`, and `UI-6`, all failed that run on one shared cause — the process never declared DPI awareness, so Windows rendered the game at a virtualised size and bitmap-stretched the result. **That is fixed**; the three are re-runs, not fresh checks. Set UI Scale to Auto first. See finding 1 in that section |
| Improve visuals | `VIS` 3 of 32 | 3 `PENDING` | Run on 2026-08-11; the other 29 passed and were lifted out. The three open rows — 128 armor bulk, 129 adornment accents, 131 trampled ground — all failed that run, each on its own cause. **All three are fixed**; they are re-runs, not fresh checks. 128 and 129 want the maximum-zoom station; 131 wants a battle that has produced casualties |
| Sandata | `SD` 3 of 9 | 2 `PENDING`, 1 `BLOCKED` | `./scripts/run.ps1 -Game Sandata`. The other 6 passed and were lifted out. The two `PENDING` rows were fixed on 2026-08-11 and are re-runs rather than fresh checks — read each row's `Actual` column before starting, since both explain what could not be run before the fix |
| Pressure interrupt | `P` | 9 `BLOCKED`, 1 `PENDING` | **Not runnable today** — see below |

**The 10 `BLOCKED` rows are blocked by the build, not by the reader.** Nine `P`
rows need movement preset V7, which the client cannot select: `BuildScenario`
overrides the preset to `RangedStandoffV8` and no preset selector is exposed, so
under the shipped default no pressure mark is ever drawn and no pressure
inspector row ever renders. Unblocking them is a code change, not an attempt.
The tenth is `SD-7b`, blocked for the reason recorded in its own row: no theme
switcher and no unknown-contact state. The four other `SD` rows that were
blocked stopped being so on 2026-08-11, when what each of them was waiting for
was built.

**Controls, so no row has to be attempted by guesswork.** `Space` plays and
pauses; `1`, `2`, and `4` set playback speed; `R` starts the next round and
`Shift+R` is a full reset; `W`/`A`/`S`/`D` or the arrow keys pan; the mouse
wheel zooms; a left click selects a warrior and opens the agent inspector; `F9`
toggles the sound log.

A row moves to `PASS` only when a person at an interactive desktop has seen the
expected result. No agent may flip one, and a passing automated test is not a
substitute for any row here.

## Sandata smoke (design section 13)

No agent may flip a row below. These rows are what is left of the complete list
of things Sandata's design records as checkable only by a
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
walk to without being asked.

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
   whether you can still tell an operator from a piece of cover. This was row
   `SD-1`, which closed on 2026-08-11 and is no longer in the table; it is
   still worth doing once as orientation before the rows that follow.
5. While zoomed in, watch the pair cross the long diagonal wall in the middle
   of the map, and then pass through the lower door. The dashed line they are
   following is the route the simulation planned; the solid one, if you have
   drawn one, is yours. **The dashed line was row SD-2, which closed on
   2026-08-12 and is no longer in the table.** The door half of this
   step was row `SD-3`, which closed on 2026-08-11.
6. At each zoom level, look at the yellow fire cones. **That is row SD-6.**
7. Left-drag a box around the blue pair, then right-click three or four points
   across the map, then press Enter. They should abandon the objective route
   and walk your polyline instead.

**What is knowingly not working. Do not spend your session rediscovering it.**

- **Almost no text.** The client had no font at all until 2026-08-11. It now
  bakes two, and the operator inspector draws its rows when a warrior is
  selected — that is the whole of what text does today. The contact list,
  mission clock, roster strip, order queue, and go-code panel are still blank
  rectangles, and there is still no on-screen tick counter, no score, and no
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
- **A click selects one operator; a drag selects several.** Before 2026-08-11
  a click selected nothing at all, silently, which is worth knowing if you are
  reading an older account of a session.
- **Only one theme is reachable.** `daylight-ops` ships in the theme catalog
  and nothing in the client can switch to it, and there is no unknown-contact
  state to look at either. **Row SD-7b is `BLOCKED` on this.** Row `SD-7a`
  closed `PASS` on 2026-08-12 — the friendly-versus-hostile judgement,
  including the shape-alone half, was reachable in `night-ops` and is no
  longer in the table.
- **Sound covers gunfire and nothing else.** There was none at all until
  2026-08-11. Twenty-four generated gunshot files now ship, covering a rifle
  and a pistol at close and indoor ranges; every other sound in the 106-slot
  catalog, and three of the five acoustic environments for those two weapons,
  are still absent and play as silence. The note under the table has the
  detail.
- **Only two weapon appearances exist**, a rifle and a pistol. Every operator
  drew the same placeholder before 2026-08-11.
- **Accuracy is effectively range-only**, so a defender inside sensing range
  is hit reliably. This is a deferred design question, not a defect to report.
- The mission clock in the log stops updating after the last casualty: the
  `boot.sandata.stopped` line reports whatever tick the last
  `sim.sandata.roster` line set, not the tick the run really ended on. The
  roster line's own `t` field is correct.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| SD-4 | Watch a rifle operator cross a doorway, then a pistol operator cross the same one | The rifle operator lowers the weapon and re-raises it; the pistol operator does not | Could not be run by anyone: every operator drew the same placeholder weapon appearance, so the two halves of the comparison were visually identical. **Fixed 2026-08-11** on both counts. Operators now alternate between an AK-pattern rifle and a Glock-pattern pistol, so a pistol operator is one of the two who walk the route rather than a defender who never moves, and each draws its own top-down sprite — a long silhouette with a curved magazine against a short stubby one. Both sprites are greyscale and tinted by the faction role, so they read in either theme | PENDING |
| SD-5 | Hold sustained automatic fire from the maximum operator count | Automatic fire sounds continuous rather than machine-gun-stuttered, and no audio drops out | Could not be run by anyone: Sandata shipped no sound files and had no playback path. **Both landed on 2026-08-11** under the narrow authorization recorded below the table. Read that note before running this row — only two of the five acoustic environments have files, so a shot beyond 200 world units is still silent, and that is expected rather than the drop-out this row is looking for | PENDING |
| SD-7b | View friendly, hostile, and unknown contacts in every shipped theme | All three are distinguishable in `daylight-ops` as well as `night-ops` | Cannot be run by anyone: `LoadTheme` always takes `catalog.DefaultThemeId`, so `daylight-ops` is unreachable from the client, and no unknown-contact state exists to render. Becomes executable when a theme switcher and an unknown-contact state ship. | BLOCKED |
**SD-5's blocker, and what is left of it.** Sandata's sound catalog is 106
slots expanding to 524 variant files, roughly 104,800 ElevenLabs credits, and
that spend as a whole is still not authorized. A narrow slice of it was
authorized on 2026-08-11 and generated: twenty-four files covering an
AK-pattern rifle in 7.62x39mm and a Glock-pattern pistol in 9x19mm, six
variants each, in the `close` and `indoor` acoustic environments. Six variants
per slot is not a preference — it is what `SandataSoundCatalog` declares for
those rows, and `ShotSlotResolver` picks across all six, so shipping fewer
would leave a proportion of shots resolving a filename that is not there.

**Three of the five environments are still empty, and that is expected.**
`outdoor`, `distant`, and `suppressed` have no files, so a shot that resolves
one of them plays silence. The client passes a real range — the distance to the
nearest living hostile — and hardcodes "not indoors" and "no suppressor",
because nothing in `Sandata.Core` knows which side of a wall an operator is on
and no weapon carries a suppressor. In practice that puts a shot inside 200
world units on the `close` files and everything further out on nothing at all.
The full provenance, including the prompt wording that decides whether a
generated take is audible, is in `src/Sandata.Client/Content/Audio/README.md`.

**`SD-1`, `SD-2`, `SD-3`, `SD-6`, `SD-7a`, and `SD-8` passed and are no longer
in the table.** `SD-1`, `SD-3`, and `SD-6` were lifted out on 2026-08-11, and
`SD-2`, `SD-7a`, and `SD-8` were lifted out on 2026-08-12, all into
the 2026-08-11 record **"Closed rows lifted out of families that are still
open"**, named rather than linked for the reason given at the top of this file,
with their evidence, so what remains below is the open work: 2 `PENDING` and 1
`BLOCKED`. Read `SD-6`'s archived entry before acting on finding 4 of the first
run — the row passed on legibility, and the separate finding that the cone
communicates nothing is recorded there rather than as a failure.

**Which rows a tester could reach on the first run: SD-1, SD-2, SD-3, SD-6, and
SD-7a — five of the nine.** All five were attemptable once the client ran the
simulation and the assaulting squad walked a real route. `SD-3` and `SD-6`
closed on that run; `SD-2` turned out to be unjudgeable and was recorded
`BLOCKED`; `SD-1` and `SD-7a` failed. `SD-1` was re-checked later the same day
and closed. The other four were `BLOCKED` from the start.

**All of that changed later on 2026-08-11.** Four of the five blockers were
built — the published path, the per-weapon appearance, the gunshot files and
the playback path that plays them, and click selection together with a font
for the inspector — and `SD-7a`'s shape complaint was answered. Eight of the
nine rows could then be attempted by a person; only `SD-7b` could not. `SD-2`,
`SD-7a`, and `SD-8` were re-checked on 2026-08-12 and closed `PASS`, leaving
`SD-4` and `SD-5` `PENDING` and `SD-7b` still `BLOCKED` — the table above is
the current account.

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
hostile half was reachable in `night-ops` and its all-themes half was not, so
as a single row it could never be closed and could never be honestly blocked
either. `SD-7a` was the half a tester could finish, and did, closing `PASS` on
2026-08-12; `SD-7b` is the half that still waits on a theme switcher and an
unknown-contact state. The colour-removed judgement stayed with `SD-7a`,
because shape-alone distinguishability was testable in the one reachable
theme.

## First Sandata smoke run — 2026-08-11

The first time a person has run Sandata and reported what they saw. Five rows
were attemptable; the result was two `PASS`, two `FAIL`, and one row that
turned out to be unjudgeable and is now `BLOCKED`. The transport controls,
which no row covers, were confirmed to do what they claim.

One of the two failures did not stay a failure. `SD-1` was re-checked by the
same tester later on 2026-08-11 and reported passing, so it left this file for
the archive along with `SD-3` and `SD-6`. This section is kept as the record of
the first run and is deliberately not rewritten to hide the reversal.

Four findings came out of it. None is a regression — all four are things that
were never built, surfaced by the first person to look at the screen. Findings
1 and 3 have since been built and are marked as such below; the text of each is
kept as it was written so that a reader can see what the state of the client
was, rather than being quietly rewritten into a description of the fix.

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

**Fixed.** `CombatFeedback` now drives the flash from the `ShotFired` event
feed instead of from the stored phase, and adds tracers and an X-shaped impact
mark. The same event feed also drives gunfire audio as of 2026-08-11.

**2. Nothing makes an operator stop at weapon range to engage.** A search of
`SandataSimulation` and `Sandata.Core/Squads` for any effective-range,
engagement-range, or stop-to-fire concept returns nothing. `InitialSquadGroups`
sends each assaulting squad to a map objective, a defender is standing on that
objective, and the squad walks to the waypoint. Closing to contact is the
absence of engagement behaviour, not a decision any code makes.

**3. No autonomous path is drawn.** This was `SD-2`'s finding; the row closed
`PASS` on 2026-08-12 and is no longer in the table above. **Fixed
2026-08-11**: it is drawn dashed, under the pawns, in the same role as the
player's own solid drawn path.

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

That is the finding this whole day of work was really about, and it is the one
worth carrying forward. `SD-1` closed on a re-check on 2026-08-11, and `SD-7a`
closed on a re-check on 2026-08-12. `SD-7a`'s fix gave the two
factions different shapes rather than different colours; the weapon sprites gave
a rifle a different silhouette from a pistol; and the autonomous route was made
dashed rather than merely a different shade of the blue its own operators are
drawn in. Every one of those was chosen the same way, and the rule is worth
stating for whoever adds the next thing to this screen: **if the only thing
separating two meanings is a colour, it is not separated.**

## Auto camera modes smoke

Added by the auto-camera hysteresis and mode setting, 2026-07-28. **Not
performed.** The unit tests prove the grace, dwell, re-target, and ceiling
decisions against synthetic agent lists; only a person watching a live window
can say whether the camera now feels calm rather than restless. The baseline
auto-pan motion underneath this setting is already closed; these rows are about
the modes built on top of it.

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
Rows 58 through 61 still test the opening frame only; row 61a below extends
the same check past it.

**Further amended by the battlefield-realism change (`BattlefieldRealismV10`).**
Rows 58, 59, 60, 61, and 61a are reworded below to describe cohort-grouped
deployment and the weaker, positionally-equivalent-but-not-per-index mirror
the default rotating roster now produces, in place of the exact per-index
mirror these rows previously asked a person to confirm.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 58. Read the opening frame | Added by the battlefield-realism change (`BattlefieldRealismV10`). Before the armies move, each side reads as several separate groups of warriors rather than one undifferentiated cloud, and each group reads as mostly one weapon cohort rather than an even mix of every weapon in the roster, at the default camera fit and without zooming in. Failure is a field that reads as one undifferentiated cloud, or groups whose weapon mix looks as uniform as a random cross-section of the whole army. | Not run | PENDING |
| 59. Check the mirror | Amended by the battlefield-realism change. Pausing at tick 0 and comparing the two halves shows each side with the same overall shape and depth as the other — the same number of groups, the same rough group sizes, the same ragged front — but under the default rotating roster the two halves are no longer exact reflections warrior-for-warrior: which weapon cohort lands where, and which warriors occupy the forward-most slots of their own contingent, can differ between the two sides in the fine detail. Only a fixed, identical roster on both sides produces an exact per-index mirror; the default launch does not use one. Failure is the two halves failing to look positionally equivalent at all — visibly more or larger groups on one side, or shield bearers sitting at the forward-most slots on one side's contingents but not the other's. | Not run | PENDING |
| 60. Confirm the groups look irregular | Within a group the spacing looks uneven rather than a snapped parade grid, and a new seed visibly reshuffles that spacing without moving the groups or changing which weapon cohort they read as. Failure is warriors within a group snapping to a visible grid or ring, or a new seed producing no visible change in spacing. | Not run | PENDING |
| 61. Confirm the armies still meet promptly | The two sides close and fight without a long empty march, and the battle reaches a terminal outcome inside its tick limit. Failure is a long empty march before contact, or a battle that runs out the tick cap with no winner declared. | Not run | PENDING |
| 61a. Confirm the groups stay distinct past deployment | Added by the persistent-contingent movement change. Let the battle run several seconds past the opening frame, well before the armies meet. Each side still reads as several separate groups of warriors at the default camera fit, each still reading as mostly one weapon cohort, rather than merging into one crowd or losing its weapon identity as soon as the armies start moving. Failure is the groups blurring into one crowd within a few seconds of the opening frame, or a group's weapon identity becoming indistinguishable from its neighbours before the armies make contact. | Not run | PENDING |

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

Added by the UI/UX completion work. **Run by a person on 2026-08-11.** Every
row was attempted. Thirteen passed. Three failed — `UI-2`, `UI-4`, and `UI-6`,
the three rows a person exercises at a maximised or fullscreen viewport — and
all three failed for the same single cause, recorded as finding 1 below the
table. The automated layout tests prove containment and hit-target invariants
at representative viewports; what this run adds is that the menu, the focus
order, the three motion intensities, and all five interpolated accents behave
as written, and that glyphs stopped being crisp the moment the window filled
the screen.

**Only the three open rows are listed below.** The thirteen that passed —
`UI-1`, `UI-3`, `UI-5`, `UI-7` through `UI-16` — were lifted out on 2026-08-11
into
the 2026-08-11 record **"Closed rows lifted out of families that are still
open"**, named rather than linked for the reason given at the top of this file,
with their evidence intact, so this table shows what is left to do rather than
what is already done. `UI-5` is worth re-reading there before the re-run: it
asserts the window opens at 1280x720, which now means 1280x720 real pixels
rather than virtual ones, so it is physically smaller than it was when it
passed.

**That cause was fixed the same day and the three rows are now `PENDING`
re-runs.** The fix is the DPI awareness declaration described in finding 1 and
designed in
[`../plans/2026-08-11-display-dpi-awareness-design.md`](../plans/2026-08-11-display-dpi-awareness-design.md).
A fix is not a result: no agent may close these three, and the `FAIL`
observation stays in each `Actual` column so the re-run is judged against what
was actually seen rather than against an empty row.

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-11 |
| Machine/platform | Microsoft Windows 10.0.26200 (Windows 11 Pro) x64, NVIDIA GeForce RTX 4070 SUPER, 2560x1440 display at 125% Windows scaling (`AppliedDPI` 120) |
| Source commit | Not captured by the tester. `main` was at `ae64485` when these results were transcribed, and every commit between that and the run was documentation-only, so the binary is unchanged. Capture the commit on the next run — this row should not say "not captured" twice |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| UI-2. Common landscape and maximised layouts | At 1280x720, 1920x1080, and the maximised desktop size, the menu stays centred and balanced, the arena HUD remains readable, and no panel covers an unrelated control. | 2026-08-11, tester at the desktop. Layout held: the menu stayed centred and balanced and no panel covered an unrelated control at any of the three sizes. The row also asks that the arena HUD remain **readable**, and at the maximised desktop size it does not — every glyph is visibly pixelated. The layout half passed and the readability half failed, and a row is a single status, so the row fails. Cause in finding 1. **Fixed the same day** by the DPI awareness declaration; a logged run now reports a 2560x1440 viewport where it reported the virtualised size before. Back to `PENDING` because only a person can say the glyphs read as crisp | PENDING |
| UI-4. Preferred UI scales and safety cap | Select Auto, 100%, 125%, 150%, and 200%. The selected preference persists after restart; when the viewport is too small for it, the active tier is safely capped while the preferred value remains selected in the menu. | 2026-08-11, tester at the desktop. Selection, persistence across a restart, and the safety cap all behaved as written. But no tier renders crisply once the window fills the screen, and the tier the policy selects at that size is itself wrong: on this 2560x1440 display the game is handed a virtualised 2048x1152 viewport, which clears `UiScalePolicy`'s 1920x1080 bar but not its 2560x1440 one, so Auto resolves to 125% where the real screen deserves 150%. Cause in finding 1. **Fixed the same day**: the viewport is now real, so Auto resolves correctly with no change to `UiScalePolicy` itself. Back to `PENDING` for a re-run. **Set UI Scale to Auto before re-running** — the saved preference on the reporting machine is an explicit `100`, left over from this row's own sweep, and an explicit preference is honoured rather than overridden, so a re-run that skips this step measures the 100% tier and learns nothing about Auto | PENDING |
| UI-6. Fullscreen startup | Select Fullscreen, close the game fully, and relaunch. It opens in soft fullscreen at the current desktop resolution. Select Windowed, restart again, and confirm normal windowed startup returns. | 2026-08-11, tester at the desktop. The mode round-trip worked: Fullscreen persisted across a full close and relaunch, opened in soft fullscreen, and selecting Windowed restored normal windowed startup. It does not open at "the current desktop resolution" — it opens at the virtualised 2048x1152 the OS reports instead of the true 2560x1440 — and the text is pixelated throughout. Cause in finding 1. **Fixed the same day**: a logged fullscreen run now reports `client` and `viewport` both at the display's true 2560x1440. Back to `PENDING` because the row's own wording — that it opens at the current desktop resolution — is now satisfied in the log but has not been seen by a person | PENDING |

### Findings from the 2026-08-11 UI run

**1. Text is pixelated whenever the window fills the screen, and the cause is
that the process never declares DPI awareness.** This is the single cause behind
all three failures — `UI-2`, `UI-4`, and `UI-6` — and it is not a defect in the
font ramp.

The typography pipeline is doing exactly what it was designed to do.
`UiFontRamp` bakes twenty-four separate `SpriteFont` atlases, one per role per
tier, and `UiPrimitives.DrawText` and `UiPrimitives.DrawCenteredText` both draw
at a hardcoded scale of `1f` from a whole-pixel origin snapped by
`UiTextGeometry.SnapToPixel`. There is no render target, no float resampling,
and no scale multiplier anywhere on the text path. Every glyph is crisp when it
leaves the game.

What resamples it is Windows. Nothing in the repository declares a DPI
awareness level: `src/Hukbo.Client/Hukbo.Client.csproj` has no
`ApplicationManifest`, there is no `app.manifest` anywhere in the tree, no code
calls `SetProcessDpiAwarenessContext`, and neither the client nor its launch
script sets SDL's `SDL_WINDOWS_DPI_AWARENESS` hint. A process that says nothing
is treated as DPI-unaware, so Windows reports a virtualised desktop size, lets
the application render at that size, and then bitmap-stretches the finished
frame up to the real panel. On the machine this run was performed on the
stretch factor is 1.25 and non-integer, which is precisely what a pixelated
glyph looks like.

The machine's numbers: the display is 2560x1440 and Windows display scaling is
125%, read from `HKCU:\Control Panel\Desktop\WindowMetrics\AppliedDPI`, which is
`120`. A DPI-unaware process on that machine is told the desktop is 2048x1152.

That mis-report has a second consequence, which is why `UI-4` fails as well as
looking bad. `UiScalePolicy.Resolve` picks a tier from the viewport in pixels:
2048x1152 clears its 1920x1080 threshold but not its 2560x1440 one, so Auto
resolves to `Percent125` on a display that should be getting `Percent150`. The
tier is chosen from a number the operating system fabricated.

The remedy is to declare per-monitor awareness once, before the graphics device
exists — a `SetProcessDpiAwarenessContext(PER_MONITOR_AWARE_V2)` call at the top
of `Program.Main`, which matches the `LibraryImport` P/Invoke pattern
`ArenaGame` already uses for its SDL window-chrome calls, or an
`ApplicationManifest` declaring `PerMonitorV2`. Either one makes
`GraphicsAdapter.DefaultAdapter.CurrentDisplayMode` report 2560x1440, removes
the OS stretch entirely, and lets `UiScalePolicy` select the 150% bake it was
always meant to select at that size.

**This has been recorded once before and was deliberately deferred.** Row 75 of
the typography section, "Display scaling", is the gated measurement task this
finding is the other half of; it is marked `DECLINED` because the 150% Windows
scaling reading was declined on 2026-07-28. That decision is what left the
awareness declaration unbuilt, and the defect stayed latent until somebody ran
the game on a scaled display. Row 75 stays `DECLINED`: it asked for a
measurement to justify building this, and the justification arrived instead as
three failed rows, which is the better evidence.

**Fixed on 2026-08-11.** `ProcessDpiAwareness.Apply` declares per-monitor v2
awareness from `Program.Main`, before `ArenaGame` builds its
`GraphicsDeviceManager` and before SDL creates a window, which is the ordering
the declaration requires. The design, the rejected manifest alternative, and
the reason the P/Invoke itself carries no test are in
[`../plans/2026-08-11-display-dpi-awareness-design.md`](../plans/2026-08-11-display-dpi-awareness-design.md).
`UiScalePolicy` is unchanged — it was never wrong, only fed a fabricated
number.

**The measurement this finding originally asked for was taken, after the fix
rather than before it.** A logged run on the reporting machine now writes
`boot.dpi.awareness` with `state` `applied`, and the `render.viewport.changed`
line that follows reports `client` and `viewport` both at **2560x1440** — the
display's true resolution, where an unaware process would have reported
2048x1152. The pre-fix line was never captured and now cannot be, since the
build that produced it no longer exists; the registry reading and the policy
threshold arithmetic are what stand behind the 2048x1152 figure.

**A re-run needs one setup step.** The saved `uiScale` preference on the
reporting machine is an explicit `100`, left behind by `UI-4`'s own sweep
through every tier, and an explicit preference is honoured rather than
overridden — the logged run above resolved `Percent100` at 2560x1440 for
exactly that reason, correctly. Set UI Scale back to Auto before re-running, or
the re-run measures the 100% tier and says nothing about the fix.

**2. The `Cebu 1521 — Provisional` theme is disliked, and that is not what
`UI-11` measures.** Every criterion the row states was met — the label, the
palette's reading, and the legibility of text and faction signals — so the row
is `PASS`. Separately, the tester does not like how the theme looks. That is a
real report and worth acting on, but it is a design preference rather than a
failure of any stated criterion, and folding it into `UI-11`'s status would
leave a row nobody could ever close without agreeing on taste.

Acting on it needs the preference turned into a criterion first: which of the
five palette anchors is wrong, and wrong against what. The theme is a
**Provisional reconstruction** under the historical accuracy policy in section
7 of `CLAUDE.md`, so a change to it is a change to a labelled provisional
interpretation and needs the evidence tier restated alongside it, not just a
new set of colours. Until that is written down, no row here covers the
complaint.

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

## Visual improvement smoke — the three open rows

Both improve-visuals families, `VIS-041` (rows 102 to 115) and `VIS-043` (rows
116 to 133), were run by a person at an interactive Windows desktop on
2026-08-11. It was the first time any of the thirty-two rows had been
attempted; both had stood entirely `PENDING` since they were written.

**Twenty-nine of the thirty-two rows passed and were lifted out.** They left
this file on 2026-08-11 with the session's evidence fields and the dagger notes
that belonged to rows 106, 114, 121, and 133. The record is titled **"Visual
improvement smoke (VIS-041 and VIS-043) — passed rows"**; find it by that title
rather than by a path:

```powershell
git log --diff-filter=A --name-only --format='%h %s' -- 'docs/archives/**' |
  Select-String 'visual-improvement-smoke'
```

If a later change touches weapon tints, shield skins, the appearance roster,
the grass ground, the sway setting, or the visual-catalog fallback path, write
fresh rows here rather than reviving the lifted ones.

**Three rows failed that run and stay here.** All three have since had a fix
shipped, so each is back at `PENDING` and each keeps the observation that
failed it, exactly as this file's reopening rule requires. They are re-runs,
not fresh checks. Read the `Actual` column before attempting one — it says what
was on screen and why.

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-11 |
| Machine/platform | Microsoft Windows 10.0.26200 (Windows 11 Pro) x64 |
| Source commit | `4fbbdf9`, the repository head at the time of the run |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 128. Armored figures read as bulkier, not as shielded | At the default-fit or maximum-zoom station, default theme, compare a pawn wearing an armor-layer component (F2 through F5) against an unarmored pawn and against a shield-bearing pawn. The armored pawn reads as visibly bulkier through the torso, and does not read as if it were carrying a shield. | 2026-08-11, tester at the desktop: `FAIL` — not clear. Investigated after the run and the cause is in the draw, not in the eye: `PawnRenderer.DrawArmor` filled the whole widened capsule solid in `BarkBrown`, replacing the torso's dye, outline, and belt with a flat block — a recolour rather than bulk, and a flat single block over the body is the silhouette a held shield draws. The widening itself was under a pixel at the default-fit station. Reopened `PENDING` against the fix below. | PENDING |
| 129. Adornment accents visible at maximum zoom without breaking any read | At the maximum-zoom station, default theme, close in on a pawn wearing adornment accents (gold accents I4/I5, or the C3 gold-edged putong). The accents are visible without breaking weapon-role, faction, or equipment recognition. | 2026-08-11, tester at the desktop: `FAIL` — not clear. Investigated after the run: `PawnGeometry.CreateAdornmentAccents` sized a mark as `min(2, round(2 × apparentScale))`, which can never exceed 2 because the constant appears on both sides of the `min`. An accent was two pixels at every zoom including the clamp ceiling, so this row could not have been passed at any station by anyone. Reopened `PENDING` against the fix below. | PENDING |
| 131. Trampled areas visibly thin where fighting happened | During or after a battle with visible casualties, observe the grass around a cluster of `Death` events. The grass there reads as visibly thinned or trampled compared to untouched ground elsewhere on the field. | 2026-08-11, tester at the desktop: `FAIL` — not clear. Investigated after the run: a trample mark drew at shade interpolation `0.22`, the exact tone of a Large grass cluster, with the grass drawn on top of it — the worn ground and the grass that was supposed to have thinned had no contrast against each other at all. The suppression radius of 40 world units also thinned part of one clump rather than an area. Reopened `PENDING` against the fix below. | PENDING |

**The fix, shipped the same day.** The design is
`docs/plans/2026-08-11-armor-accent-trample-legibility-design.md`. Armor now
draws as two symmetric flank bars that thicken the body and leave the torso's
dye, outline, and belt visible down the middle, instead of one slab covering
them. The accent area cap is read as the scale-relative bound R-W3.6's own
wording states, so a mark is two pixels at apparent scale 1 and five at the
apparent-scale clamp ceiling rather than two everywhere. Trampled stubble drops
to a shade below every grass tone, the mark covers real ground, and adjacent
marks merge, so a worn area has a boundary against the grass around it. No
pinned constant changed value and no shade exceeds the backdrop ceiling.

The canonical gate passed with the seed-1 hashes unmoved, which is what a
presentation-only change owes. **A green gate proves none of these three
rows**, and neither does the design document. Each closes only when a person at
an interactive desktop looks at the screen again.

**Still outstanding, and not a row.** Both the implementation plan draft and
`warrior-appearance-design.md` called for a line-by-line historical review of
the full preset roster table against
`docs/research/improve-visuals/warrior-appearance-historical-research.md`. That
is a human read-through of one document against another rather than an
observation of the running game, so it never was a checklist row and the
2026-08-11 session did not touch it. It has not been performed. A failure found
during it routes to a content-correction task, not to a change in this
document.

## Quit confirmation, maximize, and Core faction metrics smoke (2026-07-28)

Added by the quit-confirmation, maximize and faction metrics plan.
**A passing gate proves none of the rows below.** Every one needs a human at an
interactive desktop, and no agent may flip one to `PASS`.

The maximize and restore rows deserve particular suspicion:
`SDL_MaximizeWindow`, `SDL_RestoreWindow`, and `SDL_GetWindowFlags` are
P/Invokes that compile cleanly and have never been executed in this repository.
A clean build is no evidence that any of them works. `SDL_MinimizeWindow` has
been executed and does work, which says nothing about these three.

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
109, 110, 112 and 113 remain unobserved. Rows 104 and 114 failed.

**Row 111 passed and is no longer in the table**, lifted out on 2026-08-11 into
the 2026-08-11 record **"Closed rows lifted out of families that are still
open"**, named rather than linked for the reason given at the top of this file,
with its evidence. It is the only row of this section that ever closed. Note
that it closed under `PersistentContingentsV2`, and the shipped default is now
`BattlefieldRealismV10`, so if a later question turns on whether the battle
still resolves, write a fresh row rather than reading the archived one as
current. The automated
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

**Correction — rows 102, 103, and 105 no longer describe what ships.** The
client's default preset is now `BattlefieldRealismV10`, which groups each
contingent's warriors into weapon cohorts (a contingent reads as mostly one
weapon, split across at most `contingentCount - 1` boundaries) rather than the
round-robin mix these three rows were observed against on 2026-07-28. The
recorded passing evidence described a group composition that no longer exists
under the shipped default, so all three are reset to `PENDING` with their
evidence cleared until someone watches the cohort-grouped shape.

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
| 102. Read several distinct groups well past deployment | Each side stays readable as several distinct groups well past the opening frame, at the default camera fit, rather than merging into one crowd within a few seconds. | Not run | PENDING |
| 103. Watch a strung-out group gather and resume | A group that has strung out visibly gathers on one of its own warriors, then resumes advancing, rather than gathering indefinitely or never gathering at all. | Not run | PENDING |
| 104. Confirm the gathered shape is ragged | The gathered shape is ragged. It is not a ring, a line, an arc, a grid, or any shape that looks placed, and no warrior sits at an obviously exact distance from the one it gathered on. | Not run | PENDING |
| 105. Watch a group arrive and break apart | On reaching the enemy, a group visibly stops holding together and its warriors fight as individuals. The transition reads as arriving, not as the group breaking apart. | Not run | PENDING |
| 106. Confirm warriors ease into contact | Warriors ease into contact rather than travelling at full speed and stopping dead against an enemy body. | Not run | PENDING |
| 107. Confirm a warrior steps aside for its leader | A warrior standing in front of the warrior its group has gathered on steps aside rather than being walked through or standing there blocking it. | Not run | PENDING |
| 108. Inspect the contingent row | Selecting any warrior shows a `Contingent: <n> — <state>` row in the inspector, and that state changes over the course of the battle rather than reading the same value throughout. | Not run | PENDING |
| 109. Confirm the contingent ground tints are distinguishable | The eight contingent ground tints within one faction are distinguishable from each other at the default camera fit, and no tint is mistakable for the opposing faction's colour, at all six themes. | Not run | PENDING |
| 110. Confirm the frozen preset is unaffected | Running the same seed under `IndependentPursuitV1` looks exactly as the game looks today: no gathering, no per-contingent tint, and no contingent row in the inspector. | Not run | PENDING |
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
no human has worked from them. This copy is the live one.

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
| RG-6 | Amended by the battlefield-realism change (`BattlefieldRealismV10`). Watch a ranged warrior (Bangkaw, Busog, or Arquebus) approach its standoff distance from a target during an advance, alongside melee warriors closing on the same line, and separately watch one that has a melee enemy close on it | While no melee enemy is inside its threat radius, the ranged warrior visibly halts and holds its position once it reaches range, while melee warriors on the same approach keep walking forward and pass it — this is now only the unthreatened case. Once a melee enemy closes inside the threat radius, the ranged warrior instead backs directly away from that enemy rather than holding still, continuing until it is clear of the threat again or is stopped by the map edge. Failure is the ranged warrior continuing to close all the way to melee range like its comrades, halting at a point indistinguishable from where a melee warrior would stop on its own, or standing still once a melee enemy is inside the threat radius instead of backing away | | PENDING |
| RG-7 | Amended by the battlefield-realism change. Click a ranged warrior that has halted at its standoff distance with no melee enemy nearby, and separately click one that is currently backing away from a melee enemy | The unthreatened warrior's inspector reads "Intent: Holding at range". The threatened, backing-away warrior's inspector instead reads "Intent: Backing away from close fighters" — a second, distinct intent string that did not exist before this change — and switches back to reading "Intent: Holding at range" once the warrior is cornered by the map edge and can no longer retreat. Neither warrior's inspector ever reads "Blocked" or any other movement-refusal wording. Failure is either warrior's inspector showing "Blocked" — the movement row's own wording for a warrior whose route was rejected — or a cornered, retreat-blocked warrior continuing to read "Backing away from close fighters" instead of falling back to "Holding at range" | | PENDING |
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
arrow reads as stuck rather than as a smear.

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

**Six rows passed and are no longer in the table** — `AA-1`, `AA-2`, `AA-3`,
`AA-4`, `AA-6`, and `AA-17`, lifted out on 2026-08-11 into
the 2026-08-11 record **"Closed rows lifted out of families that are still
open"**, named rather than linked for the reason given at the top of this file,
with their evidence. What remains below is 17 `PENDING` and one `FAIL`, `AA-22`.
**Read the archived six against the note on fullscreen resolution in the run
record beneath this paragraph**: they were observed at 2048x1152, which is the
virtualised viewport a DPI-unaware process was handed on that display. The DPI
awareness fix of 2026-08-11 means the same run today would be at 2560x1440, so
any of the six that turned on glyph or silhouette legibility is worth a fresh
row rather than trust in the archived result.

**No interactive run was performed for the rows below when this section was
written.** The automated tests prove the weapon-motion catalog, the
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
| AA-5 | Watch each of the four weapons at 1x, 2x, and 4x | Every blow stays individually visible; nothing blurs into a single continuous motion at 4x | | PENDING |
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
| AA-18 | Pause during a catch-up burst, then resume | Queued contacts resume in order and none is duplicated or lost | | PENDING |
| AA-19 | Next Round, then Full Reset, during active combat | Every attack pose, pending contact, reaction, and transient effect is cleared by both | Next Round exercised and the second battle ran clean; Full Reset was never triggered. | PENDING |
| AA-20 | Watch a 200-warrior battle at close zoom | Individual exchanges are readable; the arms and trails do not obscure who is fighting whom | | PENDING |
| AA-21 | Watch a 200-warrior battle at default fit | The formation still reads as a formation | | PENDING |
| AA-22 | Watch a 500-warrior stress battle at minimum, default-fit, and maximum zoom | Frame pacing stays comfortable and the field does not turn into visual noise at any of the three | **Animations overlap and the battle reads as chaos**; the observer could not tell what was happening. Frame pacing was not reported as a problem. Two full 500-agent battles. | FAIL |
| AA-23 | Watch a warrior strike while moving | The attack plants the stance and composes with the stride; the body does not jump between two poses | | PENDING |
| AA-24 | Watch a warrior at the edge of the arena panel strike outward | The weapon does not pop in or out at the panel edge as the blow extends | | PENDING |

## Battlefield realism cohort and retreat smoke (task 18)

Added by the battlefield realism change,
which flips the client's default preset combination to `PrecolonialPhilippinesV5`
plus `MovementPresetId.BattlefieldRealismV10`. **No interactive run was
performed for this change.** Every row below is `PENDING` with its evidence
cell empty. The automated suite proves the cohort sort order, the
shield-bearer slot pairing inside each contingent, the threat-radius
arithmetic, the retreat ladder's three rungs, the per-index and positional
mirror assertions, and the twenty-seed termination sweep. None of it proves
that a spectator can read a cohort as mostly one weapon, that a shield bearer
visibly leads its own group, that a back-pedalling shooter reads as retreating
rather than as fleeing or stuck, or that the taller inspector panel still fits
the smallest supported window — which is what the rows below are for. Design:
`docs/plans/2026-08-11-battlefield-realism-design.md`.

Only a human running `./scripts/run.ps1` on an interactive Windows desktop may
certify one of these rows. Compilation, unit tests, and a window-opening probe
do not.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| BR-1 | Watch a contingent form up after deployment, at the default camera fit | The contingent reads as mostly carrying one weapon, with only a few warriors of a different weapon visible at its edges, rather than an even mix across the group. Failure is a contingent that still looks like a uniform round-robin blend of every weapon in the roster, indistinguishable at a glance from the pre-V10 grouping | | PENDING |
| BR-2 | Watch a contingent that includes shield bearers, before it makes contact with the enemy | The shield bearers are visibly at the forward-most slots of their own contingent — ahead of their contingent's other warriors on the approach — rather than scattered through the group or clustered only at the edge of the whole army. Failure is a contingent where a shield bearer cannot be picked out as leading its own group, or where the leading edge is indistinguishable from an unshielded warrior's | | PENDING |
| BR-3 | Watch one contingent's shield bearers make first contact with the enemy, then watch how long the warriors behind them keep fighting | The shield bearers are the ones who take the opening blows, and the warriors sheltered behind them survive visibly longer than they would standing in the open — the shield bearers read as absorbing the first exchanges rather than being bypassed. Failure is the enemy reaching the unshielded warriors behind the shield bearers just as quickly as the shield bearers themselves, or the shield bearers falling in the opening exchange with no visible difference in how long their own contingent's other warriors then last | | PENDING |
| BR-4 | Compare the two factions' starting deployments under the default rotating roster, at the default camera fit | The two sides read as positionally equivalent — similar contingent shapes and similar cohort groupings on each flank — without being warrior-for-warrior mirror images of each other; a warrior at a given position on one side does not necessarily correspond to the same weapon at the mirrored position on the other side. Failure is the two sides reading as exact per-index mirrors indistinguishable from the pre-V10 mirrored layout, or reading as unrelated rather than equivalent | | PENDING |
| BR-5 | Watch a ranged warrior (Bangkaw, Busog, or Arquebus) whose standoff distance a melee enemy closes inside | The ranged warrior visibly backs directly away from the closing melee enemy rather than holding its ground and continuing to fire. Failure is the ranged warrior standing still and shooting as the melee enemy closes to contact, indistinguishable from its behaviour before this change | | PENDING |
| BR-6 | Watch a ranged warrior that is backing away from a melee enemy until it is stopped by the map edge or a corner | Once cornered, the ranged warrior stops backing away and stands its ground rather than continuing to retreat in place or oscillating at the boundary. Failure is a cornered ranged warrior that appears to keep trying to back away indefinitely — visibly jittering, sliding along the edge, or kiting back and forth — instead of settling into a stationary hold | | PENDING |
| BR-7 | Watch the same back-pedalling ranged warrior from BR-5 with an eye specifically toward how the motion reads, as distinct from whether it happens at all. **This row has no automated proxy; it is a judgement call only a person watching the game can make.** | The retreat reads as a warrior deliberately backing away from a threat — facing the danger, moving with evident purpose — rather than as panicked flight, and rather than as a warrior stuck sliding against terrain or another agent. Record in `Actual` which of the three readings the observer actually got: backing away, fleeing, or stuck | | PENDING |
| BR-8 | Watch a full battle between two rosters that each field ranged warriors, under V10, to its conclusion | The battle reaches a terminal outcome — one side is defeated or the tick limit is reached with a clear winner — rather than a ranged side backing away for the whole of the tick limit and the battle never resolving. Failure is a battle that visibly stalls, with the ranged side perpetually retreating and no side able to close and finish the fight | | PENDING |
| BR-9 | Click a ranged warrior that is backing away from a melee enemy, then click one that is holding at range with no melee threat nearby, and read both inspector panels | The two intent strings — "Backing away from close fighters" and "Holding at range" — are both legible at a glance and clearly distinct from each other; a spectator reading the inspector can tell which of the two states the warrior is in without needing to also watch the battlefield. Failure is either string being hard to read at the panel's default size, or the two strings reading as similar enough to be mistaken for each other | | PENDING |
| BR-10 | Resize the game window down to the smallest supported size, 1024 by 720, and open the agent inspector on a warrior whose panel renders at its full 953-pixel height | The panel still fits within the window at that size without clipping against the window edge and without overlapping the HUD, the control bar, or the event feed. Failure is the taller panel running off the bottom or side of the window at the minimum size, or covering another HUD element that was clear of it before this change | | PENDING |

