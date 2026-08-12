# Sandata smoke — the closed rows and the first two runs

**Archived: reference only.** This is the Sandata section of
`docs/development/smoke-checklist.md` as it stood on 2026-08-12, lifted out
when that file was rewritten to hold open work only. Six of the nine `SD` rows
had closed by then and their history had grown longer than the work that was
left. Nothing here is current: do not execute it, do not cite it as
authorisation, and read the live checklist for what a tester should actually do.

Kept because it records what the first two people to run Sandata saw, and why
each of the four findings from the first run was a thing that had never been
built rather than a regression.

---

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
| SD-4 | Watch a rifle operator cross a doorway, then a pistol operator cross the same one | The rifle operator lowers the weapon and re-raises it; the pistol operator does not | Could not be run by anyone: every operator drew the same placeholder weapon appearance, so the two halves of the comparison were visually identical. **Fixed 2026-08-11** on both counts. Operators now alternate between an AK-pattern rifle and a Glock-pattern pistol, so a pistol operator is one of the two who walk the route rather than a defender who never moves, and each draws its own top-down sprite — a long silhouette with a curved magazine against a short stubby one. Both sprites are greyscale and tinted, so they read in either theme. **Attempted 2026-08-12 and not closed**: "this is ok, but still the guns are unclear". Three causes were found and fixed the same day — the weapon was tinted with the operator's *own faction colour*, so a gun was drawn blue on blue and could not separate from the body at all; `WeaponLength` was 16 world units against a 12-unit ground ring, so most of a rifle sat inside the body's own footprint; and the sprite batch was sampling linearly, which smears a 32-pixel sprite drawn at ten pixels into a smudge. The weapon now has its own gunmetal theme role, the rifle is 22 units and clears the ring, and sampling is point. **Zoom in before judging this row.** At the fitted camera an operator is about fourteen screen pixels tall and a weapon about eight, and no amount of art makes a gun legible at that size — the row asks you to watch an operator cross a doorway, which is a close-in observation | PENDING |
| SD-5 | Hold sustained automatic fire from the maximum operator count | Automatic fire sounds continuous rather than machine-gun-stuttered, and no audio drops out | Could not be run by anyone: Sandata shipped no sound files and had no playback path. **Both landed on 2026-08-11** under the narrow authorization recorded below the table. Read that note before running this row — only two of the five acoustic environments have files, so a shot beyond 200 world units is still silent, and that is expected rather than the drop-out this row is looking for. **Attempted 2026-08-12 and not closed**: the sounds were audible, but "the sound doesnt sound like AK47s specifically". That is a report about the takes themselves rather than about continuity, which is what this row measures, so it is worth keeping the two apart when re-running. Sixteen more takes were generated on 2026-08-12 from prompts naming the weapon and its acoustic character rather than only its cartridge, and the four generated slots now declare ten variants each instead of six so that the newer takes are actually reached by the resolver. The original twenty-four are untouched and still in rotation | PENDING |
| SD-7b | View friendly, hostile, and unknown contacts in every shipped theme | All three are distinguishable in `daylight-ops` as well as `night-ops` | Cannot be run by anyone: `LoadTheme` always takes `catalog.DefaultThemeId`, so `daylight-ops` is unreachable from the client, and no unknown-contact state exists to render. Becomes executable when a theme switcher and an unknown-contact state ship. | BLOCKED |
**SD-5's blocker, and what is left of it.** Sandata's sound catalog is 106
slots expanding to 540 variant files, and generating the whole of it is not
authorized. A narrow slice of it was authorized on 2026-08-11 and generated:
files covering an AK-pattern rifle in 7.62x39mm and a Glock-pattern pistol in
9x19mm, in the `close` and `indoor` acoustic environments. Twenty-four files
were made that day at six variants a slot, and sixteen more on 2026-08-12 when
those four slots were raised to ten variants each, so forty files ship today.

The variant count is not a preference — it is what `SandataSoundCatalog`
declares, and `ShotSlotResolver` picks uniformly across the declared number, so
a file past the declared count is never selected and a missing file inside it
plays silence. That is why raising the count was the only way the newer takes
could ever be heard, and why exactly those four rows were raised and no others:
they are the only slots with real files on disk.

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
