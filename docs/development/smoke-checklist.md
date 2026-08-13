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

## Where the checklist stands, 2026-08-13

82 rows across 14 subsections: **73 `PENDING`, 9 `BLOCKED`, and no `FAIL` or
`DECLINED` row** — counted from the status column of this file on 2026-08-13,
after the improve-visuals smoke run closed 29 of its 32 rows and they were
lifted out, after `SD-1` was re-checked and closed on 2026-08-11, after the
Sandata fixes of the same day moved four `BLOCKED` rows and one `FAIL` row to
`PENDING`, after `SD-2`, `SD-7a`, and `SD-8` were re-checked and closed on
2026-08-12, after the second Sandata smoke session of that day unblocked
`SD-7b` by building the theme switcher and the unknown-contact state it was
waiting for, after all seven rows of the auto camera modes family were run and
closed `PASS` on 2026-08-12, leaving behind a single reopened row, `AC-1`, that
carries a fresh finding about where a pan ends, and after the typography family
and the `UI` family were both run and closed on 2026-08-13, taking the file's
only `DECLINED` row with them, and after the last-stand formation family and
the sound gain compensation family were both run and closed the same day,
leaving behind one new row, `LS-1`, that carries a fresh finding about
how the final engagement is fought and that was fixed and reopened as `PENDING`
the same day, and after the tactical hit animations
family was run the same day and closed eight of its nine rows, seven of which
were lifted out while the eighth was reopened by the change made in response to
the ninth, and after three further families were run on 2026-08-13: the
persistent contingent family, which passed in full and was deleted whole; the
quit confirmation family, which passed fifteen of its sixteen rows; and the
shield-clash audio family, which passed four of its five and left the fifth,
row 173, `FAIL`. Two more closures landed the same day: the event feed lifetime
family, all three of whose rows passed and which was deleted whole; and the
improve-visuals family, which finally closed at 32 of 32 and was deleted whole.
That family took three attempts in one day to finish. Its re-run passed 129 and
went backwards on the other two — 128 failed a second time, on a different cause
from the first, and 131 turned `BLOCKED` when the tester found that a casualty is
not visible on the field long enough to locate one. A second armor fix and a
corpse placeholder followed, and a further re-run the same day passed both. The last closure of
2026-08-13 was the attack animation V2 family, which closed at 24 of 24 and was
deleted whole. Sixteen of its eighteen open rows passed in a first pass; `AA-23`
failed that pass and `AA-22` was re-attempted, and both then passed later the
same day. **Three of that family's rows closed without anything being fixed**,
which its archive record states plainly — `AA-22` on an unchanged 500-agent
density, `AA-23` with both of its measured causes intact, and `AA-24` against a
feature that was never built. The projectile props family closed on the same day as well: seven of
its eight rows passed and were lifted out, leaving only `PP-3`, which the tester
found draws too large in flight and which is awaiting a re-run against the fix
made in response.

**There is no `PASS` column any more, and that is deliberate.** 160 passing rows
have been lifted out — 52 on 2026-08-11 (22 of them from families that stayed,
then 29 more when both improve-visuals families were run for the first time,
then `SD-1` when the tester re-checked it), 10 more on 2026-08-12 (`SD-2`,
`SD-7a`, and `SD-8`, re-checked and closed the same way, plus all seven rows of
the auto camera modes family, closed the same day), and 16 more on 2026-08-13
(the 13 typography rows, 62 through 74, plus the three remaining `UI` rows,
`UI-2`, `UI-4`, and `UI-6`), and 14 more on the same day when the six
last-stand formation rows, 76 through 81, and the eight sound gain
compensation rows, 82 through 89, were run and passed together, and 7 more on
the same day when the tactical hit animations family was run and rows 90, 91,
93, and 95 through 98 were lifted out, and 31 more on the same day when the
twelve persistent contingent rows, 102 through 110 and 112 through 114, the
fifteen quit confirmation rows, 156 through 170, and four of the five
shield-clash audio rows, 172, 174, 175, and 176, were run and passed, and 4
more on the same day when the three event feed lifetime rows, 99 through 101,
were run and passed together and row 129 passed its re-run alone, and 2 more on
the same day when rows 128 and 131 passed on a further re-run and closed the
improve-visuals family at 32 of 32, and 18 more
on the same day when the attack animation V2 rows `AA-5`, `AA-7` through
`AA-16`, and `AA-18` through `AA-24` were run and passed, sixteen of them
together and the last two later that day, and
7 more on the same day when seven of the eight projectile props rows, `PP-1`,
`PP-2`, and `PP-4` through `PP-8`, were run and passed together.
Typography's
row 75 closed on 2026-08-13 but is not in that count: it stayed `DECLINED`
rather than turning `PASS`, so it left the
file without ever entering the passing tally. Row 94 is not in it either,
for the opposite reason: it passed on the same run but was reopened the same
day and is still here, so it has not been lifted and must not be counted as
though it had. Every row in this file is now
something a person still has to do: 73 never attempted or awaiting a re-run,
and 9 that cannot be attempted until the build changes. No row in this file is
`FAIL` any more. If a `PASS` ever appears here again it is a row that has just
closed and has not yet been lifted — not a row that belongs.

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

**Two families were deleted whole on 2026-08-11, a third followed on
2026-08-12, and six more followed on 2026-08-13.** Spectator clarity, all
fifty-two rows, and collision readability, all seven, were closed by a person
at an interactive desktop on 2026-08-11 and left together. Their record is the
2026-08-11 archive titled **"Spectator clarity and collision readability
smoke"**, found the same way as the record named below. Auto camera modes, all
seven rows, closed `PASS` the same way on 2026-08-12, but unlike the first two,
it left something behind: one row's own passing observation named a real,
separate problem with where a pan ends when it moves in from an empty,
no-fight screen, and that problem is now a fresh row, `AC-1`, in a new section
of its own rather than a re-run of anything that closed. Its record is the
2026-08-12 archive titled **"Auto camera modes smoke — closed 2026-08-12"**.

Typography and the `UI` family were the two closed on 2026-08-13, and neither
left anything behind. Typography's rows 62 through 74 were run and passed, and
row 75 stayed `DECLINED` as it already was, so the family carried no open row
of any kind and was deleted whole; its record is the 2026-08-13 archive titled
**"Typography smoke — closed 2026-08-13"**. The `UI` family's last three open
rows — `UI-2`, `UI-4`, and `UI-6` — were the re-runs the DPI awareness fix had
been waiting for, and all three passed, closing the family entirely; its
record is the 2026-08-13 archive titled **"Responsive menu, startup display,
and UI motion smoke — closed 2026-08-13"**.

The last-stand formation family and the sound gain compensation family were
the other two closed on 2026-08-13, and they closed differently from each
other. Sound gain, all eight rows, passed cleanly and left nothing behind; its
record is the 2026-08-13 archive titled **"Sound gain compensation smoke —
closed 2026-08-13"**. The last-stand formation family, all six rows, also
passed, but like auto camera modes before it, one row's own passing report
named a separate problem the row never stated as a criterion: the survivors
gather correctly and then fight one pair at a time. That is now the fresh row
`LS-1`, in a section of its own. It was `FAIL` rather than `PENDING` while the
cause was measured and no fix had been made; the fix landed the same day, so it
is now `PENDING` and needs a person to watch a final engagement. The family's record is
the 2026-08-13 archive titled **"Last-stand formation smoke — closed
2026-08-13"**, which also answers the tester's question about whether the
gathered shape is historically accurate.

The shield-clash audio family was the sixth deleted whole on 2026-08-13, and
it is the one worth reading the record of. Four of its five rows passed
outright. Row 173, which asks a listener to tell the four melee clash slots
apart, failed on the first listen — "i cannot distinguish, sounds the same for
most" — and the cause was measured rather than guessed: the sixteen clash
takes on disk are not level-matched, and the spread between takes inside one
slot is wider than the spread between the four slots, so which take fires
decided how loud a block sounded. A fix landed the same day that normalises
each take at load and gives the four slots their own level and pitch, and the
tester then closed the row on their own judgement that the sounds are
acceptable, declining a regeneration of the takes. Its record is the
2026-08-13 archive titled **"Shield-clash audio smoke — closed 2026-08-13"**,
which states plainly that rows 172 and 175 passed against the loudness the fix
replaced, so a later question about clash loudness needs a fresh row rather
than a reading of theirs.

The persistent contingent family was the fifth deleted whole on 2026-08-13.
All twelve of its open rows — 102 through 110 and 112 through 114 — were run
and passed in one session, including rows 104 and 114, which had failed at
commit `8f4e426` and had stood reopened ever since. Its record is the
2026-08-13 archive titled **"Persistent contingent smoke — closed
2026-08-13"**. The section carried two long measurement passages behind rows
104 and 114, and those are measurements rather than checklist rows, so they
moved to `docs/development/measurement-history.md` instead of leaving with the
record. One thing that family left behind is already tracked: its 2026-07-28
observation that the last survivors fought one at a time is the finding row
`LS-1` now records with a measured cause.

**A single passing row is lifted out the same way, without its section.** Six
sections still carrying open work had rows that closed — Sandata, attack
animation V2, which lost sixteen more rows on 2026-08-13 and is now down to the
two that are still open, tactical hit animations, which lost seven of
its nine rows on 2026-08-13 and kept the two that are still open, and the three
that closed the same day down to a single row each: quit confirmation, which
kept only row 171 because that row compares a printed figure against a headless
run rather than watching the screen, and projectile props, which kept only
`PP-3` because that row did not pass. Shield-clash audio was briefly on this
list too, holding row 173 alone after its other four closed, until row 173
closed as well later the same day and the section went entirely. The `UI` family used to belong on this list too, closing single rows
while its section stayed, right up until the run that closed the section
itself on 2026-08-13; it is named in the paragraph above instead, with the
rest of that history. The improve-visuals family used to belong on this list as
well — 29 of its 32 rows left on 2026-08-11 and row 129 left alone on 2026-08-13,
all while its section stayed — right up until rows 128 and 131 passed later the
same day and the section was deleted whole at 32 of 32. The first 22 of the rows
still on this list left on 2026-08-11 while their sections stayed. Each section names,
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
| Ranged | `PP` 1, `RG` 11 | 12 `PENDING` | A battle fielding Bangkaw, Busog, and Arquebus warriors. The shipped client runs combat preset V5 and movement preset V8, so ranged units are on the field by default at roughly a 14 per cent share |
| Pawn animation | `GA` 14 | 14 `PENDING` | Warriors walking, close in. The attack animation `AA` family that used to share this batch closed at 24 of 24 on 2026-08-13 and was deleted whole |
| Markers | `LC` 11, `L` 7 | 18 `PENDING` | Leaders and contingents at default zoom, plus the agent inspector |
| Render | `GR` 5 | 5 `PENDING` | Launch-time render behaviour |
| Battlefield realism | task 18 rows | 10 `PENDING` | Cohort deployment and the V10 retreat rung |
| Sandata | `SD` 3 of 9 | 3 `PENDING` | `./scripts/run.ps1 -Game Sandata`. The other 6 passed and were lifted out. All three open rows are re-runs rather than fresh checks: `SD-4` and `SD-5` were each attempted twice and failed on causes fixed on 2026-08-12, and `SD-7b` was blocked from the day it was written until the same day. Read each row's `Actual` column before starting |
| Pressure interrupt | `P` | 9 `BLOCKED`, 1 `PENDING` | **Not runnable today** — see below |
| Auto camera centring | `AC` 1 | 1 `PENDING` | This one earns its own row: it did not exist before 2026-08-12, and unlike every other row above it is a fresh check rather than a re-run of one that closed. Pan away from every fight until the screen holds none, let the assistant take over, and watch where it leaves the camera when it moves back in |
| Tactical hit animations | rows 92 and 94, of 9 | 2 `PENDING` | A 200-agent battle watched until warriors start dying, at normal speed and at fitted zoom. Both rows are re-runs against the 2026-08-13 lethal-blow legibility change, not fresh checks: 92 never passed and 94 passed against the older, lighter effects. Watch a single kill first, then a crowded exchange, and read both rows' `Actual` columns before starting |
| Last-stand engagement | `LS` 1 | 1 `FAIL` | Written on 2026-08-13 out of the last-stand formation family that closed the same day. A full 200-agent battle run to its final few warriors. It is `FAIL` on the observation already recorded, and the fix is an authoritative simulation change nobody has authorised yet, so there is nothing to re-run until that is decided |

**The 9 `BLOCKED` rows are blocked by the build, not by the reader.** All nine
are `P` rows needing movement preset V7, which the client cannot select:
`BuildScenario` overrides the preset to `RangedStandoffV8` and no preset selector
is exposed, so under the shipped default no pressure mark is ever drawn and no
pressure inspector row ever renders. Unblocking them is a code change, not an
attempt. Every `SD` row that was once blocked has stopped being so — four on
2026-08-11 and `SD-7b` on 2026-08-12, each when what it was waiting for was
built.

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


## Auto camera centring smoke (2026-08-12)

This row is the one thing left of a seven-row auto camera modes family that
ran and closed `PASS` in full on 2026-08-12 and was lifted out of this file.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| AC-1. Confirm a pan lands the fight near the middle | Pan away from every fight until the screen holds no fighting at all, let the assistant take over, and watch where the camera stops. The melee ends up near the middle of the screen — within roughly a fifth of the way from centre toward the edge — rather than pinned in a corner. Then check the other half: zoom in on a fight that is already on screen and leave the camera alone. It must not re-centre that one. Only a pan that began from an empty screen ends centred. | 2026-08-12, tester at the desktop, reporting on row 149 of the family that closed the same day: "yes, this is good; but usually it is not centered; and fighting usually stays at the corner of the screens; we need to fix to center, not when a battle happens, only when panning from an empty on-fight screen". The cause was measured rather than guessed: a pan ended as soon as any fighter reached seventy per cent of the visible half-extent, which put the fight 13.78 world units off centre on a 20-unit half-extent. **That is fixed**; this is a re-run against the observation above, not a fresh check. | PENDING |

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

## Last-stand engagement smoke (2026-08-13)

This row is the one thing left of a six-row last-stand formation family that ran
and closed `PASS` in full on 2026-08-13 and was lifted out of this file. Its
record is the 2026-08-13 archive titled **"Last-stand formation smoke — closed
2026-08-13"**, named rather than linked because that folder is pruned
periodically. Find it the same way any archived record is found:

```powershell
git log --diff-filter=A --name-only --format='%h %s' -- 'docs/archives/**' |
  Select-String 'last-stand-formation-smoke'
```

**This row is `FAIL`, and the fix has not been made.** Unlike `AC-1` above, it
is not waiting on a person to re-check something already repaired. The cause is
known and measured, and repairing it is an authoritative simulation change that
moves both hashes, so it needs a decision before it needs an implementer. Read
the row's `Actual` column and the finding beneath the table before running it.

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-13 |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| LS-1. Confirm the last stand ends as a group fight | Let a full 200-agent battle run to its final handful of warriors on each side and watch the last engagement. Several warriors from each side are in contact at once, so the ending reads as two small bands colliding. Failure is the survivors gathering correctly and then fighting one pair at a time, with the rest standing off and waiting their turn. | 2026-08-13, tester at the desktop, reporting on row 76 of the family that closed the same day: "passed, but not extremely clear. Since I am still seeing 1v1 in the endgame." The cause was measured rather than guessed and is stated below: a follower's aim point is 51 world units behind its rally agent, against a longest melee reach of 16, so only the rally agent ever reaches an enemy. **Fixed the same day** by `MovementPresetId.LastStandEngagementV11`, which the client now selects: a follower stops regrouping and closes on its own enemy once its rally agent is within its own weapon reach of an enemy, or once the follower's own enemy is within its own reach. Back to `PENDING` because only a person watching a final engagement can say whether it now reads as two bands colliding. | PENDING |

### Finding — followers park three weapon-lengths behind the warrior they gathered on

A regrouping follower does not aim at its rally agent. It aims at a point
`RallyTrailRadiusMultiplier` body radii behind that agent, on the far side from
the enemy the agent is closing on, and it stops on arrival. The multiplier is 12
(`src/Hukbo.Core/Simulation/FormationRules.cs:188`) and the default body radius
is 4.25 world units (`src/Hukbo.Core/Simulation/CollisionRules.cs:72`), so the
aim point sits 51 world units behind the leader. The longest melee reach in the
shipped combat preset is 16
(`src/Hukbo.Core/Combat/PhilippineCombatPresetV5.cs:188`).

The rally agent is exempt from regrouping
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:1426`), so it alone closes and
fights. Both factions do this symmetrically once each is at or below the default
threshold of six living agents, so two rally agents meet and duel while every
other survivor holds station out of reach; when one falls, the next-lowest
living `EntityId` takes over and the same duel repeats. The behaviour is
deterministic and does not depend on the seed.

**Lowering the trail alone is not a fix.** `FormationRules` requires
`RallyTrailRadiusMultiplier` to exceed `RallyJitterRadiusMultiplier * sqrt(2) + 2`
(`src/Hukbo.Core/Simulation/FormationRules.cs:180-188`), which with the jitter
multiplier at 6 puts the floor at about 10.49 — so 12 is already close to it,
and the jitter has to come down with it. Both constants reach the state hash, so
any change here is an authoritative simulation change needing a new preset
version and re-recorded golden expectations under `CLAUDE.md` section 5.

## Tactical hit animations smoke

All nine rows of this family were run by a person at an interactive desktop on
2026-08-13. Eight of them passed, and seven of those eight left this file for
good. Their record is the 2026-08-13 archive titled **"Tactical hit animations
smoke — closed 2026-08-13"**. Two rows are still open, and they are open for
opposite reasons.

**Row 92 never passed.** It asks whether a killing blow reads as clearly heavier
than an ordinary one, and the tester's answer was that it does not. A design and
a plan were written the same day in response — see
[`../plans/2026-08-13-lethal-blow-legibility-design.md`](../plans/2026-08-13-lethal-blow-legibility-design.md)
— and the row below is a re-run against that change, carrying the original
observation so the next tester knows what they are checking against.

**Row 94 passed and was reopened the same day.** It passed against the effect
values that were shipping when it was run. The change built in response to row
92 raises the number of primitives a kill draws and makes the heavier gore level
the default, so a crowded exchange is exactly the thing that change could have
broken. Carrying its old `PASS` forward would have been a claim about a build
nobody watched. It is a re-run, not a fresh check.

Only a human running `./scripts/run.ps1` on an interactive Windows desktop may
flip either of these rows to `PASS`. Compilation, unit tests, and a
window-opening probe do not. Nothing automated can prove that a kill reads as a
kill, or that the fighting stays legible when it gets crowded, which is the only
thing these two rows are for.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded for the re-run. The run that closed the other seven rows was 2026-08-13 |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 92. Tell a lethal hit apart | A killing blow reads as clearly heavier than an ordinary one, and reads that way without the spectator having been told where to look. The pawn is still on screen for the loudest part of its own death, the blow marks the body it killed, and the blood is heavy enough that a kill is never mistaken for a graze. | 2026-08-13, tester at the desktop, on the first run of the family: "it's not extremely clear, we need improve this so i can really see, more blood and gore". Four causes were measured rather than guessed: the pawn was removed after `0.10` seconds while the ring lived `0.28` and the blood burst `0.42`, so most of a kill drew over bare ground; lethal blows were the only blows excluded from the hit pulse, so a kill never marked its victim; the lethal and ordinary ring colours were eleven units apart in a single channel; and the default gore level produced no sustained blood at all. **All four are changed**; this is a re-run against that observation, not a fresh check. | PENDING |
| 94. Watch a crowded exchange | With many pawns trading blows at once the effects stay bounded. No persistent trail, smear, or lingering colour builds up on the arena, and the fighting stays legible underneath. | 2026-08-13, tester at the desktop: passed, with no separate note recorded. **Reopened the same day**, unrun against the current build: the row-92 change raises the per-kill droplet cap from 8 to 12, lengthens every lethal blood lifetime, and makes `Full` the default gore level, so more is drawn per kill than when this row passed. There is no total-screen quad ceiling in the effect code to appeal to instead — only per-record caps — so a person has to look. | PENDING |

## Quit confirmation, maximize, and Core faction metrics smoke (2026-07-28)

Added by the quit-confirmation, maximize and faction metrics plan.
**Fifteen of this section's sixteen rows closed on 2026-08-13** and were lifted
out. Rows 156 through 170 were run by a person at an interactive Windows
desktop and all fifteen passed; their record is the 2026-08-13 archive titled
**"Quit confirmation, maximize, and Core faction metrics smoke — rows 156 to
170 closed 2026-08-13"**, named rather than linked because that folder is
pruned periodically. Find it the same way any archived record is found:

```powershell
git log --diff-filter=A --name-only --format='%h %s' -- 'docs/archives/**' |
  Select-String 'quit-confirmation-maximize-metrics-smoke'
```

That run is also the first time `SDL_MaximizeWindow`, `SDL_RestoreWindow`, and
`SDL_GetWindowFlags` were ever executed in this repository. They had compiled
cleanly for weeks without proof that any of them worked; rows 165, 166, and 167
are that proof, and the archived record is where it lives.

**Row 171 is the one row left, and it did not close because it is not an
observation of the screen.** It compares the faction accuracy the battle report
prints against a headless run of the same seed, so running it means running the
game and the headless runner and holding the two figures side by side. A
passing gate proves nothing about it, and no agent may flip it to `PASS`.

| # | Step | Expected | Result | Status |
| --- | --- | --- | --- | --- |
| 171. Compare the reported faction accuracy against a headless run of the same seed | It matches the simulation own counters rather than an event-derived approximation. | Not run | PENDING |

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

**Seven of this family's eight rows closed on 2026-08-13** and have been lifted
out into that day's archive record for this family, which is named here in prose
rather than linked because the archive folder is deleted periodically. A person
at an interactive desktop ran the family and passed `PP-1`, `PP-2`, and `PP-4`
through `PP-8`. Only `PP-3` is still open.

`PP-3` was attempted in the same run and did not pass. The tester found that the
in-flight projectile draws too large: a spear reads as longer than the warriors
it flies past, and it stays too large even with the camera zoomed in. The row as
written asks about the opposite failure, a shot shrinking away to nothing, so the
finding sits outside what the row itself covers, and the row stays here until it
can be re-run against the fix made in response. That fix caps the in-flight prop
at the same apparent-scale ceiling the pawns already obey; the plan is
`docs/plans/2026-08-13-projectile-prop-scale.md`.

Re-running `PP-3` means checking both ends of the zoom range in one sitting. The
shot must still be drawn at the most pulled-out camera, which is what the row has
always asked for, and at the tightest zoom it must no longer read as longer than
a warrior is tall.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| PP-3 | Watch a shot in flight while zooming from close in to fully zoomed out | The projectile stays visible at every zoom, including the most pulled-out one. Failure is a shot that scales down to nothing and disappears — the in-flight prop is deliberately never detail-gated, because at low detail it may be the only sign a ranged unit exists | 2026-08-13, tester at the desktop. The shot stayed visible across the zoom range, but the in-flight prop draws too large and still reads oversized even when the camera is zoomed in. Capped in response; awaiting a re-run against that fix. | PENDING |

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

