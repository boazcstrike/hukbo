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

18 rows across 5 subsections: **15 `PENDING`, 3 `PASS`, and no `BLOCKED`,
`FAIL`, or `DECLINED` row** — recounted from the status column of this file on
2026-08-14, after seven families closed in full that day and their subsections
were deleted whole, and after the contingent shape selector family added `CS-1`
and `CS-2` as two new `PENDING` rows in a subsection of their own.

The previous count said sixteen rows, all `PENDING`. Sixteen was right; all
`PENDING` was not — rows 60, 61, and 61a of the starting deployment section were
already `PASS` when it was written, and they are the three `PASS` rows counted
above. No row's status was changed to arrive at this count.

**Three of the fifteen `PENDING` rows carry a failing observation in `Actual` while sitting
at `PENDING`, and that is the rule rather than an inconsistency.** `BR-1`,
`BR-2` and `BR-10` were run on 2026-08-14 and did not pass; a row that has been
observed to fail and has a fix in flight goes back to `PENDING` carrying what
the tester saw, so the re-run is judged against the observation instead of a
blank cell. Do not read `PENDING` here as "nobody has looked". The last-stand engagement family went first, at one row. Three more
followed in one sitting: leader identification at eleven rows, movement gait
animation at fourteen, and ranged units at eleven, all thirty-six run and passed
by a person at an interactive Windows desktop. Three more closed later the same
day: tactical hit animations, whose last two rows were 92 and 94; quit
confirmation, maximize, and Core faction metrics, whose last row was 171; and
footwork pressure interrupt, all eleven of whose rows — `P-1` through `P-10` and
`L-7` — were run for the first time and passed together. That last closure took
the leader marker family with it, because `L-7` was its final open row.

Two families were run on 2026-08-14 and did not close. GPU render passed two of
its five rows, `GR-1` and `GR-2`, and they were lifted out; its other three stay
below with the reason they were not run recorded against them. Battlefield
realism passed five of its ten — `BR-5` through `BR-9`, the whole ranged-retreat
half — and those five were lifted out; its other five stay below, three of them
carrying a failing observation and two deliberately not attempted behind those
three.

Every row left here is something a person still has to do, and every one of them
is something a person **can** do: none is blocked by the build, and none is
waiting on a feature that does not exist. 229 rows have been lifted out of this
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
sum to this file's own total of 16. They summed to 67 before 2026-08-14, because
two sections had never been given a row here at all; six more batches left the
table later that day when their families closed in full, and the render and
battlefield-realism batches shrank rather than leaving.

| Batch | Families | Rows | What one launch has to show |
| --- | --- | --- | --- |
| Render | `GR` 3 of 5 | 3 `PENDING` | Launch-time render behaviour at the largest battle the panel allows. `GR-1` and `GR-2` passed on 2026-08-14 and were lifted out. All three rows left were attempted that day and not run; the section preamble records why, and why two of the three reasons do not hold |
| Battlefield realism | `BR` 5 of 10 | 5 `PENDING` | Cohort deployment only. The other 5 are the V10 retreat rung, which passed on 2026-08-14 and was lifted out. Three of these five failed that day and two were held back behind them, so none is a fresh check — read every `Actual` column before launching. `BR-10` is the odd one out: it needs the window resized to 1024 by 720 and the agent inspector open, not a battle watched |
| Sandata | `SD` 3 of 9 | 3 `PENDING` | `./scripts/run.ps1 -Game Sandata`. The other 6 passed and were lifted out. All three open rows are re-runs rather than fresh checks: `SD-4` and `SD-5` were each attempted twice and failed on causes fixed on 2026-08-12, and `SD-7b` was blocked from the day it was written until the same day. Read each row's `Actual` column before starting |
| Starting deployment | rows 58 through 61a | 3 `PASS`, 2 `PENDING` | The opening frame, paused at tick 0, and the first few seconds after it. These five were missing from this table until 2026-08-14 |

**No row in this file is blocked by the build, and this paragraph used to say
the opposite.** Every `SD` row that was once blocked has stopped being so — four
on 2026-08-11 and `SD-7b` on 2026-08-12, each when what it was waiting for was
built. The last rows blocked for any other reason were the eleven movement-preset
rows, which the Army Composition panel's preset selector unblocked on 2026-08-13
and which were run and closed on 2026-08-14. What remains open here is open
because nobody has watched it yet, not because the build stands in the way.

**One thing a tester still has to set deliberately.** The client's shipped
movement preset is `ClientSettingsStore.DefaultMovementPreset`, which is
`MovementPresetId.LastStandEngagementV11`, and the Army Composition panel lists
every registered preset. A preset chosen there is staged for the **next Full
Reset** rather than applied to the battle in progress, so a round started before
the reset is still running the previous preset. Any row below that names a
preset is read against the preset actually in force, not the one most recently
clicked.

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

**Further amended by the cohort lateral spread change
(`CohortLateralSpreadV13`, 2026-08-14).** A person ran this family on
2026-08-14 against the pre-V13 build. Rows 60, 61 and 61a passed and are
recorded below. Rows 58 and 59 failed, and both are back to `PENDING` a re-run
against a build carrying `CohortLateralSpreadV13`, which is the client's
default movement preset from that date.

Two things changed in the rows themselves as a result.

Row 58 gains the clause it was missing. What the tester found was that the
grouping worked — each group did read as mostly one weapon — but that the
groups were laid across the map in sorted order, so one end of a line was the
shield-bearing group and the cohorts were not spread across a team's own
frontage. The row now asks for that spread explicitly, because the previous
wording could be satisfied by the arrangement that failed.

Row 59's premise was simply wrong and is corrected. The row asked a person to
accept a weaker-than-exact mirror "under the default rotating roster", but the
launched client does not use a rotating roster: `ArenaGame.BuildScenario`
always populates `RosterCounts`, so both factions resolve identical loadouts
per faction-local index and tick 0 owes an **exact** per-index mirror. The
rotating roster the old wording described belongs to `Scenario.CreateDefault`,
which is what the gate and the headless runner use, not the player. A Core test
now proves the exact tick-0 mirror at the shipped shape — 250 a side,
`PrecolonialPhilippinesV5`, V13, populated `RosterCounts` — so a failure of
this row means either the camera framing or something the automated evidence
cannot see. The row also now states that the mirror is **expected** to decay
once the battle advances, because cohesion jitter and every combat roll fold
the absolute `EntityId` and faction 1's ids are offset by `AgentsPerFaction`;
a tester who unpauses and sees the two armies diverge is watching intended
behaviour, not a defect.

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-14, for rows 60, 61 and 61a only |
| Machine/platform | Not recorded |
| Source commit | Not recorded; the run predates `CohortLateralSpreadV13` |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

Rows 60, 61 and 61a were observed on the pre-V13 build. V13 changes only which
contingent a weapon cohort is dealt to, and leaves the within-group spacing,
the jitter draw, the contingent persistence and the closing distance untouched,
so nothing those three rows judge is altered by it. Row 61's second clause — a
terminal outcome inside the tick limit — is additionally corroborated under V13
by the gate's fifth headless workload, recorded in
`docs/development/testing.md`. Row 61's first clause and rows 60 and 61a remain
visual judgements, and if a re-run of rows 58 and 59 is done on a V13 build it
costs nothing to look at these three again in the same sitting.

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 58. Read the opening frame | Added by the battlefield-realism change (`BattlefieldRealismV10`). Before the armies move, each side reads as several separate groups of warriors rather than one undifferentiated cloud, and each group reads as mostly one weapon cohort rather than an even mix of every weapon in the roster, at the default camera fit and without zooming in. Amended again by the cohort lateral spread change: those groups must also be spread across the team's own frontage rather than laid down it in sorted order, so that no single weapon cohort — the shield-bearing warriors above all — occupies one end of the line by itself. Failure is a field that reads as one undifferentiated cloud, groups whose weapon mix looks as uniform as a random cross-section of the whole army, or weapon cohorts collected toward one edge of a team's own frontage instead of distributed across it. | Failed 2026-08-14 on the pre-V13 build: the groups did each read as one weapon, but the cohorts were laid across the map in sorted order, with one cluster of shields and the weapon types unevenly distributed across each team's frontage | PENDING |
| 59. Check the mirror | Premise corrected by the cohort lateral spread change; the earlier "default rotating roster" wording was false for the launched client and is withdrawn. Pause at tick 0 before anything moves. The two halves are an exact reflection of each other across the vertical centre line: the same number of groups, the same group sizes, the same ragged front, the same weapon cohort in the mirrored lane, and shield bearers on the forward-most slots of a contingent on one side wherever they are on the other. Then unpause: the two armies are **expected** to drift out of exact symmetry as the battle runs, because per-warrior cohesion offsets and combat rolls are keyed on absolute entity id. Failure is the two halves not matching **at tick 0** — a different number or size of groups, a weapon cohort in a lane whose mirror holds a different one, or shield bearers forward on one side only. Divergence after the battle starts is not a failure of this row. | Failed 2026-08-14 on the pre-V13 build; the tester reported the enemy team not mirroring. Not reproduced in source or in tests: a Core test proves the exact per-index tick-0 mirror at the shipped 250-a-side shape. Re-run needed to establish whether what was seen was the row-58 lopsidedness, the assisted camera framing an off-centre view, or a frame past tick 0 | PENDING |
| 60. Confirm the groups look irregular | Within a group the spacing looks uneven rather than a snapped parade grid, and a new seed visibly reshuffles that spacing without moving the groups or changing which weapon cohort they read as. Failure is warriors within a group snapping to a visible grid or ring, or a new seed producing no visible change in spacing. | Observed 2026-08-14 on the pre-V13 build; the groups looked irregular | PASS |
| 61. Confirm the armies still meet promptly | The two sides close and fight without a long empty march, and the battle reaches a terminal outcome inside its tick limit. Failure is a long empty march before contact, or a battle that runs out the tick cap with no winner declared. | Observed 2026-08-14 on the pre-V13 build; the armies met promptly | PASS |
| 61a. Confirm the groups stay distinct past deployment | Added by the persistent-contingent movement change. Let the battle run several seconds past the opening frame, well before the armies meet. Each side still reads as several separate groups of warriors at the default camera fit, each still reading as mostly one weapon cohort, rather than merging into one crowd or losing its weapon identity as soon as the armies start moving. Failure is the groups blurring into one crowd within a few seconds of the opening frame, or a group's weapon identity becoming indistinguishable from its neighbours before the armies make contact. | Observed 2026-08-14 on the pre-V13 build; the groups stayed distinct past deployment | PASS |

## GPU render smoke (gpu-render Phases 1 and 2)

**This family was run for the first time on 2026-08-14 and two of its five rows
closed.** `GR-1` and `GR-2` passed and were lifted out; their record is the
2026-08-14 archive titled **"GPU render smoke — PARTIAL 2026-08-14"**, named
here in prose rather than linked because that folder is pruned periodically. The
three rows below are what is left, and all three are `PENDING`. These rows were
drafted in the plan on 2026-07-28 and moved here on 2026-08-07; they were never
in this file while the workstream ran, which is why no human had worked from
them before that day. This copy is the live one.

**Read this before deciding `GR-3` or `GR-5` cannot be run.** The tester who ran
this family on 2026-08-14 stopped at all three remaining rows for one stated
reason: the team size cannot be raised above 500. That observation is correct
and it is exactly what `GR-2` asks for, but it is not a blockage, because the
ceiling is **per team, not per battle**.
`ArmyCompositionStepper.MaximumUnitsPerTeam` is `500`, the panel's row is
labelled `Units Per Team`, and `ArenaGame` builds the scenario with
`composition.UnitsPerTeam * 2`. Setting the stepper to its maximum therefore
produces exactly the 1,000-unit battle these rows ask for. `GR-3` and `GR-5` are
runnable today; they have simply not been run.

`GR-4` is the one row here with a real obstacle. It asks for the same battle to
be compared *before and after* the Phase 2 commits, which means building a
pre-Phase-2 revision alongside the current one. That is a two-build comparison
rather than an observation of the shipped game, and no route to it has been
written down. It is left `PENDING` rather than `BLOCKED` because nobody has yet
established that it cannot be done — only that nobody has described how.

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
| GR-3 | Set `Units Per Team` to 500 for both teams, start the resulting 1,000-unit battle, and watch one full engagement | The battle renders and remains watchable; pawns, shields, swings, and hit pulses all read correctly at all three camera stations | Attempted 2026-08-14 and not run. The tester reported that the size cannot be raised above 500; that ceiling is per team, so 500 on each side is the 1,000-unit battle this row wants | PENDING |
| GR-4 | Compare a seed-1 200-unit battle before and after the Phase 2 commits at the same tick and camera station | No visible difference. Phase 2 is pure removal of duplicated work; any visible difference is a defect, not a new baseline | Attempted 2026-08-14 and not run. This row needs two builds, not two settings, and no route to the pre-Phase-2 build has been written down | PENDING |
| GR-5 | Watch hit pulses in a dense 1,000-unit melee | Pulse strength and timing read exactly as before the per-frame lookup replaced the per-pawn scan | Attempted 2026-08-14 and not run, for the same reason recorded against `GR-3` | PENDING |

Phase 3's rows GR-6 through GR-10 are deliberately absent. They covered the
instanced backend, which the NO-GO verdict closed and which does not exist.

## Battlefield realism cohort and retreat smoke (task 18)

Added by the battlefield realism change,
which flips the client's default preset combination to `PrecolonialPhilippinesV5`
plus `MovementPresetId.BattlefieldRealismV10`. Design:
`docs/plans/2026-08-11-battlefield-realism-design.md`.

**This family was run at an interactive desktop on 2026-08-14 and split in
two.** Five of its ten rows passed — `BR-5` through `BR-9`, the whole of the
ranged-retreat half — and were lifted out of this file. Their record is the
2026-08-14 archive titled **"Battlefield realism cohort and retreat smoke — rows
BR-5 to BR-9 closed 2026-08-14"**, named rather than linked because that folder
is pruned periodically. Find it the same way any archived record is found:

```powershell
git log --diff-filter=A --name-only --format='%h %s' -- 'docs/archives/**' |
  Select-String 'battlefield-realism-retreat-smoke'
```

The five rows below are what is left, and none of them is merely unattempted.
`BR-1`, `BR-2` and `BR-10` were observed and failed; each carries what the
tester actually saw in its `Actual` column, so the re-run is judged against the
observation rather than against a blank cell. `BR-3` and `BR-4` were
deliberately not attempted, because both are downstream of the deployment shape
the first two found wanting. **A fix is in progress for all three failures and
none of them is fixed yet** — read each row's `Actual` before starting.

The automated suite proves the cohort sort order, the shield-bearer slot pairing
inside each contingent, the threat-radius arithmetic, the retreat ladder's three
rungs, the per-index and positional mirror assertions, and the twenty-seed
termination sweep. None of it proved any of the three failures above, and two of
them — a panel with no horizontal clip, and contingents that dissolve into
individual pursuit — had passing suites over them the whole time.

**What "the default camera fit" means for `BR-1` and `BR-4`, since 2026-08-13.**
Both rows are read at the default camera fit, and what that fit shows changed on
that day. The default window was 1280 by 720, at which the fit resolved the Low
detail tier and a pawn was drawn with no legs at all; it is now 1600 by 900,
which resolves the Medium tier at the fit, so arms, the armor silhouette, the
sash, the head treatment, and the legs are all visible without zooming in. That
change arrived with the gait default visibility plan,
`docs/plans/2026-08-13-gait-default-visibility.md`. Both rows were `PENDING`
when it landed, so no recorded result was invalidated by it.

Only a human running `./scripts/run.ps1` on an interactive Windows desktop may
certify one of these rows. Compilation, unit tests, and a window-opening probe
do not.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| BR-1 | Watch a contingent form up after deployment, at the default camera fit | The contingent reads as mostly carrying one weapon, with only a few warriors of a different weapon visible at its edges, rather than an even mix across the group. Failure is a contingent that still looks like a uniform round-robin blend of every weapon in the roster, indistinguishable at a glance from the pre-V10 grouping | 2026-08-14, tester at the desktop: "they visibly form up but not enough, some just charged and fought". The weapon-grouping half works — the groups were visible. What failed is cohesion. The cause was traced in source: under `ContingentState.Advance`, `MovementRules.IsCohesionEligible` gate 4 denies a cohesion destination to every member that is **not** straggling, so those members fall through to the individual pursuit path; and a contingent reaches the gathering `Hold` state only when its geometric gates pass, which a 24-body-radius cohesion square shared by eight contingents in one map half rarely allows. Contingents therefore sit in `Advance`, where only stragglers close up and the core charges. A design for this is written and is **not yet implemented**; see `../plans/2026-08-14-contingent-cohesion-before-contact-design.md` | PENDING |
| BR-2 | Watch a contingent that includes shield bearers, before it makes contact with the enemy | The shield bearers are visibly at the forward-most slots of their own contingent — ahead of their contingent's other warriors on the approach — rather than scattered through the group or clustered only at the edge of the whole army. Failure is a contingent where a shield bearer cannot be picked out as leading its own group, or where the leading edge is indistinguishable from an unshielded warrior's | 2026-08-14, tester at the desktop: "some deployments have the shield bearers at the back". It is *some* deployments rather than all, so this is conditional rather than a flat inversion. The within-contingent rule was read against disk and is correct — `CohortDeploymentAssignment.AssignWithinContingent` sorts slots by depth and pairs shield bearers to the forward-most ones. The row's other named failure is the live one: the shield cohort collects at **one edge of the whole army** rather than being distributed across its frontage, and an all-shield contingent has no internal contrast to show. Addressed by `CohortLateralSpreadV13` under `../plans/2026-08-14-cohort-lateral-spread-design.md`, which riffles cohort runs onto non-adjacent lanes | PENDING |
| BR-3 | Watch one contingent's shield bearers make first contact with the enemy, then watch how long the warriors behind them keep fighting | The shield bearers are the ones who take the opening blows, and the warriors sheltered behind them survive visibly longer than they would standing in the open — the shield bearers read as absorbing the first exchanges rather than being bypassed. Failure is the enemy reaching the unshielded warriors behind the shield bearers just as quickly as the shield bearers themselves, or the shield bearers falling in the opening exchange with no visible difference in how long their own contingent's other warriors then last | Deliberately not attempted on 2026-08-14. It is downstream of the deployment shape `BR-1` and `BR-2` found wanting, so attempting it now would measure a known defect rather than the thing this row is for. Attempt it after those two pass | PENDING |
| BR-4 | Compare the two factions' starting deployments at the default camera fit, paused at tick 0 | **Premise corrected on 2026-08-14; this row as originally written could only be passed by a broken build.** It asked a tester to confirm that the two sides are *not* warrior-for-warrior mirrors, and named an exact per-index mirror as the failure. The launched client has no rotating roster: `ArenaGame.BuildScenario` always populates `RosterCounts`, so both factions resolve identical loadouts per faction-local index and an exact per-index mirror at tick 0 is the correct result, not a failure. The rotating roster this row was written against belongs to `Scenario.CreateDefault`, which no client launch uses. What to look for is therefore what row 59 already asks for, and this row is subsumed by it: an exact reflection at tick 0, drifting apart once the battle runs | Not attempted. Superseded by row 59; run that row instead and record the result there | PENDING |
| BR-10 | Resize the game window down to the smallest supported size, 1024 by 720, and open the agent inspector on a warrior whose panel renders at its full 953-pixel height | The panel still fits within the window at that size without clipping against the window edge and without overlapping the HUD, the control bar, or the event feed. Failure is the taller panel running off the bottom or side of the window at the minimum size, or covering another HUD element that was clear of it before this change | 2026-08-14, tester at the desktop: "it does render, but the width of the texts overextends the current small width of the info panel". The fault is horizontal, not the vertical one this row was written to catch, and it fails as written because the row's expected observation names no axis. The cause was measured rather than guessed: the panel has **no horizontal clip at all** — its two bounds tests both compare a row's bottom against a maximum row bottom — and only five prose blocks are wrapped, while the four top-detail rows and the roughly twenty-six lower rows are handed to a plain `DrawString` as finished single-line strings against a 277-pixel budget. The longest, the combo-attributes row, reaches 99 characters. A fix is in progress; this row is a re-run against that change, not a fresh check | PENDING |


## Contingent shape selector smoke (V12)

Added by the contingent chief membership change, which made
`MovementPresetId.ContingentShapeV12` selectable in the Army Composition panel.
V12 was registered on 2026-08-13 but absent from the panel's option list, so no
spectator could reach it at all — and on 2026-08-14 `CohortLateralSpreadV13` was
appended to that same list while V12 was still missing from it, so the omission
happened twice before it was caught. The Client suite now enumerates
`MovementPresetRegistry` and fails if a registered preset is missing from the
selector. **No interactive run was performed for this change.** Both rows below
are `PENDING` with their evidence cells empty.

What the automated suite does prove: that the option list contains every
registered preset, that arrow keys reach V12 and wrap past the end of the list,
and that a seed-1 headless run under V12 terminates deterministically with an
army 22 per cent narrower and 27 per cent shallower than V11's. What it does not
prove is that either of those things reads correctly on screen — which is what
the rows below are for. Design:
`docs/plans/2026-08-14-contingent-chief-membership-design.md`.

The client default is `V13 Cohort Lateral Spread`, so reaching V12 means
selecting it on the panel, applying, and then performing a **Full Reset** — the
selector stages a preset and the next full reset is what consumes it.

Only a human running `./scripts/run.ps1` on an interactive Windows desktop may
certify one of these rows. Compilation, unit tests, and a window-opening probe
do not.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| CS-1 | Open the Army Composition panel, focus the movement-preset row, and step through the whole list from the default `V13 Cohort Lateral Spread`. Select `V12 Contingent Shape`, apply, then perform a Full Reset | Both `V12 Contingent Shape` and `V13 Cohort Lateral Spread` appear in the selector, V12 immediately before V13, each label legible at the panel's default width without clipping or truncation, and the battle that follows the Full Reset is fought under V12. Failure is either preset being absent from the selector, a label overflowing the row, or the reset producing a battle indistinguishable from the V13 one because the staged preset was not consumed | | PENDING |
| CS-2 | With the same army composition, watch the opening deployment under `V11 Last-Stand Engagement` and then under `V12 Contingent Shape`, both at the default camera fit, and compare how the two armies are grouped | The V12 army reads as more, smaller contingents than the V11 one, and as occupying visibly less width and depth on the field. Failure is the two deployments being indistinguishable at a glance, or the V12 deployment reading as crowded, overlapping, or clipped against the map edge rather than merely tighter. Record in `Actual` roughly how many separate groups each side reads as, for both presets | | PENDING |
