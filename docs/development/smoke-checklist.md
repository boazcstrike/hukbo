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

## Where the checklist stands, 2026-08-14

5 rows across 2 subsections: **5 `PENDING`, and no `PASS`, `BLOCKED`,
`FAIL`, or `DECLINED` row** — recounted from the status column of this file on
2026-08-14, after ten families closed in full that day and their subsections
were deleted whole. The contingent shape selector family both joined this file
and left it on that day: `CS-1` and `CS-2` were written as two new `PENDING`
rows in a subsection of their own, then run and passed before the day was out.

The count before this one said thirteen rows, all `PENDING`. Eight of those
thirteen closed on 2026-08-14: battlefield realism's last five rows, `BR-1`
through `BR-4` and `BR-10`; the contingent shape selector's `CS-1` and `CS-2`;
and the render family's `GR-4`. The first two families are therefore gone from
this file whole and the render family is down to the two rows below. Their
records are the 2026-08-14 archives titled **"Battlefield realism cohort
smoke"**, **"Contingent shape selector smoke"**, and **"GPU render smoke —
`GR-4`"**, named here in prose rather than linked because that folder is pruned
periodically. No row's status was changed to arrive at this count.

**No row left in this file carries a failing observation.** The three that did —
`BR-1`, `BR-2` and `BR-10` — were re-run on 2026-08-14 against the fixes they
had been waiting on, passed, and left with their family. Every row below sits at
`PENDING` because nobody has watched it, not because somebody watched it fail.

The families that closed on 2026-08-14 went in roughly this order.
The last-stand engagement family went first, at one row. Three more
followed in one sitting: leader identification at eleven rows, movement gait
animation at fourteen, and ranged units at eleven, all thirty-six run and passed
by a person at an interactive Windows desktop. Three more closed later the same
day: tactical hit animations, whose last two rows were 92 and 94; quit
confirmation, maximize, and Core faction metrics, whose last row was 171; and
footwork pressure interrupt, all eleven of whose rows — `P-1` through `P-10` and
`L-7` — were run for the first time and passed together. That last closure took
the leader marker family with it, because `L-7` was its final open row. Starting
deployment closed after them at five rows, and battlefield realism and the
contingent shape selector closed last, at ten rows and two.

One family was run on 2026-08-14 and did not close. GPU render has passed three
of its five rows — `GR-1` and `GR-2` early in the day, `GR-4` later — and all
three were lifted out; its other two stay below with the reason they were not
run recorded against them, and the section preamble records why that reason does
not hold.

Every row left here is something a person still has to do, and every one of them
is something a person **can** do: none is blocked by the build, and none is
waiting on a feature that does not exist. 242 rows have been lifted out of this
file since it was split out of `docs/development/testing.md`. A closed row is
not described here once it leaves; its record is the dated archive that carries
its family's name, and this file is only what is left to run.

**There is no `PASS` column any more, and that is deliberate.** If a `PASS` ever
appears here it is a row that has just closed and has not yet been lifted — not
a row that belongs.

**Recount before trusting that total.** Every figure here that was ever taken
on faith turned out to be wrong. Count the status column itself, and count
every status — a row that is neither `PENDING` nor a result is still a row.

**This file holds open work only.** It is not a record of what has been tested.
A family every one of whose rows is `PASS` is deleted from this file outright:
it is a record rather than a checklist, and keeping it makes the file longer
without giving a tester anything to do. A family is deleted only when it is
entirely `PASS` — an open `FAIL` or `BLOCKED` row is unfinished work and stays
here where a reader will see it. The Sandata section is the worked example of
this rule being applied to prose as well as to rows: on 2026-08-12 six of its
nine rows had closed, and the paragraphs explaining when and why each of them
closed had grown longer than the three rows a tester still had to run. All of
that history left for the archive in one piece and the section was rewritten
around the three open rows.

**A single passing row is lifted out the same way, without its section.** A
section that still carries open work loses each of its rows as that row closes,
and the section's own preamble names which of its rows closed and what to be
careful of when reading the archived result. Two of the rows lifted out that way
closed under a preset or a viewport that is no longer the shipped one, which is
exactly the trap an undated `PASS` sets for the next reader.

**No file in this repository may link to `docs/archives/`.** That folder is
deleted periodically, so a link into it is a link that breaks. Nothing in this
checklist points there, and nothing added to it may. Where a closed row's
evidence is worth naming, it is named in prose — the 2026-08-11 record
**"Closed rows lifted out of families that are still open"** is named that way
here rather than linked. This paragraph used to claim four sections below
referenced that record; a count of the file finds none, and this is the second
figure in this file to have been wrong when taken on faith. Find such a record
by its title rather than by a path, so that a later prune costs a search instead
of the evidence:

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
order relaunches the game far more often than they need to. The batch rows below
sum to this file's own total of 5. They summed to 67 before 2026-08-14, because
two sections had never been given a row here at all; nine more batches left the
table later that day when their families closed in full — the battlefield
realism batch last, and the contingent shape selector batch, which had joined
the table that same morning at two rows, immediately before it. The render batch
shrank rather than leaving.

| Batch | Families | Rows | What one launch has to show |
| --- | --- | --- | --- |
| Render | `GR` 2 of 5 | 2 `PENDING` | Launch-time render behaviour at the largest battle the panel allows. `GR-1`, `GR-2` and `GR-4` passed on 2026-08-14 and were lifted out. Both rows left were attempted that day and not run; the section preamble records why, and why the stated reason does not hold |
| Sandata | `SD` 3 of 9 | 3 `PENDING` | `./scripts/run.ps1 -Game Sandata`. The other 6 passed and were lifted out. All three open rows are re-runs rather than fresh checks: `SD-4` and `SD-5` were each attempted twice and failed on causes fixed on 2026-08-12, and `SD-7b` was blocked from the day it was written until the same day. Read each row's `Actual` column before starting |

**No row in this file is blocked by the build, and this paragraph used to say
the opposite.** Every `SD` row that was once blocked has stopped being so — four
on 2026-08-11 and `SD-7b` on 2026-08-12, each when what it was waiting for was
built. The last rows blocked for any other reason were the eleven movement-preset
rows, which the Army Composition panel's preset selector unblocked on 2026-08-13
and which were run and closed on 2026-08-14. What remains open here is open
because nobody has watched it yet, not because the build stands in the way.

**One thing a tester still has to set deliberately.** The client's shipped
movement preset is `ClientSettingsStore.DefaultMovementPreset`, which is
`MovementPresetId.CohortLateralSpreadV13` and was `LastStandEngagementV11` until
2026-08-14, and the Army Composition panel lists every registered preset. A
preset chosen there is staged for the **next Full Reset** rather than applied to
the battle in progress, so a round started before the reset is still running the
previous preset. No row left below names a preset, so all of them are read
against that shipped default unless the tester deliberately changes it.

**Controls, so no row has to be attempted by guesswork.** `Space` plays and
pauses; `1`, `2`, and `4` set playback speed; `R` starts the next round and
`Shift+R` is a full reset; `W`/`A`/`S`/`D` or the arrow keys pan; the mouse
wheel zooms; a left click selects a warrior and opens the agent inspector; `F9`
toggles the sound log.

A row moves to `PASS` only when a person at an interactive desktop has seen the
expected result. No agent may flip one, and a passing automated test is not a
substitute for any row here.

## Sandata smoke (design section 13)

Run with `./scripts/run.ps1 -Game Sandata -Configuration Debug`. No agent may
flip a row here.

Three rows are open. Six others have closed and are no longer described in this
file at all — their evidence is in the 2026-08-12 archive record titled
**"Sandata smoke — the closed rows and the first two runs"**, named rather than
linked because that folder is pruned periodically. Find it the same way any
archived record is found:

```powershell
git log --diff-filter=A --name-only --format='%h %s' -- 'docs/archives/**' |
  Select-String 'sandata-smoke-closed-rows'
```

**Close the window to end a run. Never kill the process.** `JsonlLogSink` sets
`AutoFlush = false` and the log is flushed when `Program` exits normally, so a
terminated process leaves a zero-byte log file and the whole run's record is
gone.

### Controls

| Input | Effect |
| --- | --- |
| Space, or the first control-bar button | Play / pause |
| Period (`.`), or the second control-bar button | Advance exactly one tick, pausing first |
| Tab, or the third control-bar button | Cycle speed: half, normal, double, quadruple |
| F5, or the fourth control-bar button | Restart the mission from tick zero |
| F6 | Cycle the theme. Not saved: the next launch starts on `night-ops` again |
| Escape | Exit |
| Mouse wheel | Zoom |
| Left-click on an operator | Select it, and open the operator inspector |
| Left-drag on the map | Marquee-select friendly operators |
| Right-click on the map | Add a node to a hand-drawn path |
| Enter | Submit the drawn path to the selected operators |
| Any letter key, released | Submit a go-code release order for the selection |

### What the shipped map does on its own

`angle-house` spawns two blue operators at the bottom wall and two red ones on
the two yellow objective squares. The blue pair is one squad, and on tick zero
it requests a path to the objective at the top right. Expect them to leave the
bottom wall within a second or two, cross the house through the lower door, and
reach the objective at roughly nine seconds of real time at normal speed. The
second defender, at the bottom-left objective, is out of range of the whole
route and never does anything.

### Drawing a path, and why one gets refused

Right-click three or four points, then press Enter with operators selected. The
squad abandons its objective route, walks your polyline node by node, and
returns to its own route when it reaches the last node.

**A polyline that crosses a wall is refused, by design.** Design section 16
validates an authored path at submission against four rules — node count, map
bounds, blocked cells, and wall crossings — and never silently re-routes one.
`angle-house` is a house, so points dropped "across the map" without regard to
its walls will usually break the fourth rule. A refusal now says so: the order
queue panel in the bottom-right names the reason, and the run's own
`artifacts/logs/sandata-<utc>-<pid>.jsonl` carries a `warn` line reading
`input.sandata.order` with `accepted: false` and the reason by name. If a
submission produces neither a queue row nor a log line, nothing was submitted at
all — the most likely cause is an empty selection.

### What is knowingly not working. Do not spend your session rediscovering it

- **There is no menu, and there never has been.** The client opens straight into
  the mission. There is no title screen, no settings screen, and no pause menu.
- **Almost no text.** The operator inspector draws its rows for a selected
  operator, and the order queue draws its rows. The contact list, mission clock,
  roster strip, and go-code panel are still blank rectangles, and there is no
  on-screen tick counter, no score, and no victory banner.
- **The mission never ends.** Nothing in the client checks an outcome; the run
  stops at the 36,000-tick limit, about twelve minutes at normal speed.
- **A blocked operator stalls permanently.** If a mover's route runs into a body
  that is standing still, it refuses the step, tries exactly one 22.5-degree
  sidestep, refuses that too, and repeats both refusals for the rest of the run.
  It never re-plans. This is task 89's recorded finding and it is expected
  behaviour today — see `src/Sandata.Core/Movement/LocalAvoidance.cs`.
- **Sound covers gunfire and nothing else.** Forty generated files ship,
  covering an AK-pattern rifle and a Glock-pattern pistol at close and indoor
  ranges. Every other sound in the 106-slot catalog, and three of the five
  acoustic environments for those two weapons, are absent and play as silence.
  See the note under the table.
- **Accuracy is effectively range-only**, so a defender inside sensing range is
  hit reliably. This is a deferred design question, not a defect to report.
- **Nothing consumes a magazine.** `MagazineRounds` is stored and hashed and no
  stage decrements it, so automatic fire never runs a weapon dry.
- The mission clock in the log stops updating after the last casualty: the
  `boot.sandata.stopped` line reports whatever tick the last
  `sim.sandata.roster` line set, not the tick the run really ended on. The
  roster line's own `t` field is correct.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| SD-4 | Watch a rifle operator cross a doorway, then a pistol operator cross the same one | The rifle operator lowers the weapon and re-raises it; the pistol operator does not | Attempted twice and not closed. **2026-08-11:** every operator drew the same placeholder weapon, so the two halves of the comparison were identical; fixed the same day by giving the rifle and the pistol their own sprites. **2026-08-12, first attempt:** "this is ok, but still the guns are unclear" — the weapon was tinted with the operator's own faction colour, `WeaponLength` sat inside the body's own ground ring, and the sprite batch sampled linearly; all three fixed the same day. **2026-08-12, second attempt:** "i cannot see the diff for lowering the weapon and raising it". That one was not a rendering problem at all: stage 11 computed the weapon-lowered condition, handed it to the weapon chain, and threw the result away, so `OperatorState.WeaponLowered` was a constant false for the whole of every run and no renderer could have drawn it. The simulation now stores it and emits an event on the transition, and a lowered weapon is drawn swung off the aim line and shortened. **Zoom in before judging this row** — at the fitted camera an operator is about fourteen screen pixels tall, and the row asks for a close-in observation of one doorway crossing | PENDING |
| SD-5 | Hold sustained automatic fire from the maximum operator count | Automatic fire sounds continuous rather than machine-gun-stuttered, and no audio drops out | Attempted twice and not closed. **2026-08-11:** Sandata shipped no sound files and no playback path; both landed that day under the narrow authorisation recorded below. **2026-08-12, first attempt:** the sounds were audible but "the sound doesnt sound like AK47s specifically", so sixteen further takes were generated from prompts naming the weapon rather than only its cartridge. **2026-08-12, second attempt:** "no auto heard; it sounds just single shots". That was accurate and the cause was in the simulation: `FireModeSelection` and the cyclic-fire accumulator both had no production caller, and the client hardcoded `FireMode.Single` for every shot, so no weapon in the roster had ever fired automatically. A rifle inside its auto band now fires at 600 rounds per minute and the mode travels to the audio layer on the shot event. **Read the note below before running this row.** No automatic *loop* sample exists on disk, so a burst is currently carried by one report per round — audible and continuous, but not the loop sample design section 10 specifies | PENDING |
| SD-7b | View friendly, hostile, and unknown contacts in every shipped theme | All three are distinguishable in `daylight-ops` as well as `night-ops` | Was `BLOCKED` from the day it was written: `LoadTheme` always took the catalog's default id, so `daylight-ops` was unreachable, and no unknown-contact state existed to render. Both shipped on 2026-08-12. **F6 cycles the theme**, and a hostile is now drawn by the best contact tier the assaulting faction holds for it: identified draws as before, detected draws as a facingless marker with no weapon, and a hostile nobody has seen is not drawn at all. That last case is not fog of war — the operator is still there, still shooting, and still hashed | PENDING |

**SD-5's audio blocker, and what is left of it.** Sandata's sound catalog is 106
slots expanding to 540 variant files, and generating the whole of it is not
authorised. A narrow slice was authorised on 2026-08-11 and extended on
2026-08-12: an AK-pattern rifle in 7.62x39mm and a Glock-pattern pistol in
9x19mm, firing **single** shots, in the `close` and `indoor` acoustic
environments. Forty files ship, ten variants across each of those four rows.

The variant count is not a preference — it is what `SandataSoundCatalog`
declares, and `ShotSlotResolver` picks uniformly across the declared number, so
a file past the declared count is never selected and a missing file inside it
plays silence.

**No `GunLoop` or `GunTail` file exists at all**, which is why sustained fire is
currently carried by one report per round. That fallback lives in
`SandataSoundPlayer` and is marked in the code as the degradation it is: design
section 10's model is one loop instance plus one tail per shooter, and the day
real loop and tail files exist, the fallback stops firing on its own. Generating
them is an ElevenLabs spend that nobody has authorised — four rows, a `GunLoop`
and a `GunTail` for 7.62x39mm in `close` and `indoor`.

**Three of the five environments are still empty, and that is expected.**
`outdoor`, `distant`, and `suppressed` have no files, so a shot that resolves
one of them plays silence. The client passes a real range — the distance to the
nearest living hostile — and hardcodes "not indoors" and "no suppressor",
because nothing in `Sandata.Core` knows which side of a wall an operator is on
and no weapon carries a suppressor. In practice that puts a shot inside 200
world units on the `close` files and everything further out on nothing at all.
The full provenance, including the prompt wording that decides whether a
generated take is audible, is in `src/Sandata.Client/Content/Audio/README.md`.

**The rule the whole Sandata screen is built on**, learned from the two rows
that failed on it and carried forward for whoever adds the next thing: **if the
only thing separating two meanings is a colour, it is not separated.** A hostile
is a diamond without a pip, a friendly is a square with one, an unknown contact
carries no weapon at all, a rifle's silhouette is longer than a pistol's, and
the simulation's own planned route is dashed where a hand-drawn one is solid.

## GPU render smoke (gpu-render Phases 1 and 2)

**This family was run for the first time on 2026-08-14 and three of its five
rows closed.** `GR-1` and `GR-2` passed early in the day and were lifted out,
and `GR-4` — the two-build Phase 2 comparison — passed later the same day and
was lifted out with them. Their records are the 2026-08-14 archives titled
**"GPU render smoke — PARTIAL 2026-08-14"** and **"GPU render smoke — `GR-4`"**,
named here in prose rather than linked because that folder is pruned
periodically. The two rows below are what is left, and both are `PENDING`. These rows were
drafted in the plan on 2026-07-28 and moved here on 2026-08-07; they were never
in this file while the workstream ran, which is why no human had worked from
them before that day. This copy is the live one.

**Read this before deciding `GR-3` or `GR-5` cannot be run.** The tester who ran
this family on 2026-08-14 stopped at both of these rows for one stated
reason: the team size cannot be raised above 500. That observation is correct
and it is exactly what `GR-2` asks for, but it is not a blockage, because the
ceiling is **per team, not per battle**.
`ArmyCompositionStepper.MaximumUnitsPerTeam` is `500`, the panel's row is
labelled `Units Per Team`, and `ArenaGame` builds the scenario with
`composition.UnitsPerTeam * 2`. Setting the stepper to its maximum therefore
produces exactly the 1,000-unit battle these rows ask for. `GR-3` and `GR-5` are
runnable today; they have simply not been run.

What the automated work already proves, and what it does not: the render probe
recorded a 1,000-unit default-fit `Draw` p95 of 3 276.6 us against an 8.0 ms
budget, `PawnGeometryTests` pins the two-stage geometry path bit-identical to the
entry points it replaced over a 73,728-case grid, `PawnQuadCountTests` still pins
17, 19, 20 and 40 quads, `PawnAppearanceCacheTests` proves cold-cache equivalence
and the capacity bound, `HitEffectSystemTests` proves the per-frame pulse lookup
returns what the per-pawn scan returned, and `ArmyCompositionStepperTests` proves
the stepper clamps at 500 per team. None of that proves that a 1,000-unit battle
is watchable rather than merely measurable, or that the composition panel still
fits the window at the new maximum. The one claim the whole phase rested on —
that Phase 2 changed no pixel — is no longer open: `GR-4` was run and passed on
2026-08-14, and a person compared the two builds at the same tick and camera
station.

Only a human running `./scripts/run.ps1` on an interactive Windows desktop may
flip one of these rows. Compilation, unit tests, and a window-opening probe run
do not. Leave untouched rows `PENDING`; report `BLOCKED` honestly.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| GR-3 | Set `Units Per Team` to 500 for both teams, start the resulting 1,000-unit battle, and watch one full engagement | The battle renders and remains watchable; pawns, shields, swings, and hit pulses all read correctly at all three camera stations | Attempted 2026-08-14 and not run. The tester reported that the size cannot be raised above 500; that ceiling is per team, so 500 on each side is the 1,000-unit battle this row wants | PENDING |
| GR-5 | Watch hit pulses in a dense 1,000-unit melee | Pulse strength and timing read exactly as before the per-frame lookup replaced the per-pawn scan | Attempted 2026-08-14 and not run, for the same reason recorded against `GR-3` | PENDING |

Phase 3's rows GR-6 through GR-10 are deliberately absent. They covered the
instanced backend, which the NO-GO verdict closed and which does not exist.
