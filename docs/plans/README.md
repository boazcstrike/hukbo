# Plans — what is live, and what each document is for

Every file in this folder is live. Finished and superseded work moves to
`docs/archives/<YYYY-MM-DD>/` under the rules that folder's own `README.md`
states, so if a document is here it is still load-bearing for something.

**Nothing in this folder may link into `docs/archives/`.** That folder is
deleted periodically, so a path into it is a path that breaks. Name an archived
document in prose when a reader needs to know it existed.

"Live" does not mean "in progress". A design document that authorizes nothing, a
backlog entry parked by user decision, and a completed package's measurement
record are all live, because a future session still has to read them. What none
of them means is *go and build this* — only an explicit authorization does that,
and section 6 of [`../../CLAUDE.md`](../../CLAUDE.md) says how one is given.

**One document came back the same day, was executed, and settled a question that
had been open since July.** The follower-trailing deadlock design was archived in
the seventh sweep below and revived within the hour, when the user read the
sweep's finding that none of its five options had ever been built and directed
that the work be finished. An archived document may never be executed, which is
why the revival happened before a line of code was written rather than after.

The work ran and **shipped no code**. Option 6.4, rotation and swap detection,
was built twice in two independent implementations and does not fix the stall:
the five stalled seeds are still five, and forcing an exchange at all costs the
resolver's displacement rule. The measured reason is that locked warriors want
the *same* ground rather than each other's, so an exchange rule has nothing to
exchange. Both documents are live and carry the numbers —
[`2026-08-15-collision-mutual-lock.md`](2026-08-15-collision-mutual-lock.md) is
the plan and holds the counters, and the design's banner records what its
section 6 now means. One incidental finding is worth more than the attempt: at
body radius 4.5, which is the value this bug has pinned since July, **0 of 200
seeds now stall**, so the body radius is a tuning question again rather than a
workaround. The two questions the sweep moved into [`TODO.md`](TODO.md) are
parked there again, one of them now with an answer.

Swept an eighth time on 2026-08-15, and **nothing left the folder.** Every
document still here was re-checked against the code by a separate reader, one
document set each, and each one describes work that is not on disk: sixteen
unstarted tasks and no `MovementPresetId` past 13 for the contingent cohesion
pair, sixteen unstarted tasks for the thousand-unit pair, no magazine decrement
and no `Reloading` phase, no Sandata scenario type, stages 1 and 2 of
clear-the-map absent, nothing at all of the blocked-mover replan, D3 and D4 of
the mission-does-not-end pair absent, no reversal logic for the column
about-face, and a weapon sprite package that lives in a worktree at roughly
eleven of its twenty-seven tasks with the whole renderer half unbuilt. **The
follower-trailing deadlock design is the one document that pass did not
cover**: it was in the archive while the audit ran, and it returned to this
folder afterwards under the revival recorded above.

That pass kept the formation blocking baseline, on the reasoning that all
eleven counters it quotes are still emitted and it is therefore still
re-measurable. **The user overruled that on the same day and it has been
archived**, under the title "Formation blocking at 500 agents — backlog entry
and measured baseline". Re-measurability is not liveness: the document held no
task, named no owner, and by its own section 6 no longer described any preset a
spectator watches, because its twenty-seed sweep ran under
`LastStandEngagementV11` and the shipped default is `CohortLateralSpreadV13`.
The parked work itself did not leave with it — the entry under the
second-round lag report in [`TODO.md`](TODO.md) now carries the numbers, the
warning that neither table is a target, and the instruction to re-measure
before comparing, so nothing has to be read out of the archive to pick the work
up. Twenty citations were rewritten to name the document in prose: eighteen in
the ranged movement and formation research note, whose figure table lost its
`Source line` column entirely, plus this file's table row and `TODO.md`'s link.
Two more survive inside `docs/archives/`, where a path is allowed to die with
the folder it points into.

That pass produced corrections rather than archives, each recorded in the
document it corrects. The formation baseline gained a section saying its
twenty-seed sweep is a `LastStandEngagementV11` record and no longer describes
what a spectator sees, because the shipped movement default is now
`CohortLateralSpreadV13`. The attack animation V2 backlog kept sections 2 and 3
and the `SwingPose` naming item, which are all still true, and now records in
place that its section 4 and four of its section 5 items have since been fixed.
The contingent cohesion design no longer claims to be blocked by a workstream
that has landed, and its R2 and R3 now state that their premises are false and
point at the reshaped form its own plan already carries. The magazine design no
longer calls `RosterStrip.FormatMagazineLine` tested — it has no caller at all,
production or test. The weapon sprite plan now records that the settings-schema
collision it predicted has already fired, because `main` spent version 12 on
`PawnVisualStyle` while the worktree spends 12 on `WeaponVisualStyle`. One
symbol name in this file was wrong and is fixed: the field is
`GroupPathState.TargetRoomId`, not `SquadSlot.TargetRoomId`.

Swept a seventh time on 2026-08-15, later the same day, over the whole folder
rather than only over the day's closures. Three documents left and none joined:
the attack animation V2 design, the ranged units session handoff, and the
follower-trailing deadlock design. Each was checked against code on `main`
rather than against its own status line. The attack animation design shipped and
its twenty-four `AA` rows closed on 2026-08-13; its backlog stays here because
`ConservativePawnCull` still has no production caller, re-checked that day. The
ranged package merged at `9daa271` with every `RG-*` row closed, so its handoff
is a record, and its three surviving items — the V9 termination gap, Phase 2's
line of sight and friendly fire, and the `HUKBO_AUTOPLAY` proposal — moved into
[`TODO.md`](TODO.md) rather than leaving with it. The follower-trailing design
was never built and must not be: `b9003a9` closed its stall in the intent layer,
`CollisionResolver` is unchanged, and the 2026-08-13 re-measurement found zero
stalls at the shipping configuration; its two surviving questions, the body
radius pinned at 4.25 by a hang rather than by a decision and the 2,000-agent
traffic jam, moved into `TODO.md` too — all of which the revival above reverses,
and the sweep's finding is exactly what prompted it. Four path citations were rewritten to
name the documents in prose: one in the formation blocking baseline, one in the
collision deadlock diagnosis research note, and one in the attack animation
backlog, plus these table rows.

Everything else in the folder was checked the same way and stays, because each
one describes work that is not on disk. The Sandata magazine and reload design:
no tick stage decrements `MagazineRounds` and `SandataSimulation` says so by
name. The Sandata scenario and roster design: there is no scenario type. The
Sandata blocked-mover replan design: no re-request mechanism exists. The Sandata
clear-the-map design: only its stage 0 shipped, as `RoomLayout` and
`RoomClearStates` — the corner bake of stage 1 and the map residual of stage 2
are absent, and `MissionState` records the simplification in its own remarks.
The mission-does-not-end plan: its tasks 2 and 3 shipped at `51c0a86` and
`ea3bbc1`, but tasks 5 through 9 have not. `OutcomeRules` does exist, and it
predates this plan: `OutcomeRules.Resolve` is wired into the tick pipeline and
sets `MissionState.Winner`, but it decides on elimination alone. What is absent
is the stalemate predicate task 6 would add to it — the word does not appear
anywhere in `src/Sandata.Core` — so a mission that nobody can win and nobody can
lose still cannot end. The contingent
cohesion plan: `MovementPresetId` stops at 13. The thousand-unit plan: not
started, and not authorized.

Swept a sixth time on 2026-08-15, after that day's smoke closures. Three
documents left this folder and none joined it: the pawn sprite body plan and its
design, and the hide-the-event-log plan. Each was checked against code on `main`
before it moved rather than against its own status line — `PawnSpriteAtlas`, the
`B` command arm, the `Events` control-bar button, and the F8 toggle all exist
there, at `21e1abb` and `8a25abf` — and each had every smoke row it owed run and
passed by a person at an interactive desktop the same day, eight `SB` rows and
five `HEL` rows. One thing survives the pawn sprite pair and is recorded in both
archive banners rather than only here: nothing on screen announces the `B` key,
because the menu panel is full, and making room for it is its own design. No
source, test, or live document cited any of the three by path, so nothing needed
rewriting into prose.

Swept a fifth time on 2026-08-14, after the UI chrome nine-slice package
finally reached `main`. Three documents left this folder and none joined it:
the UI chrome nine-slice design, and the death-collapse plan and its design.
Each was checked against code on `main` before it moved rather than against
its own status line — `UiChromeStyle`, `UiNineSlice`, and the `PANEL STYLE`
selector all exist on `main`, and the collapse pose, the per-warrior collapse
clocks, and the prone-body pawn layout all ship there too. The chrome plan had
already been archived on its own branch before that branch merged, so this
pass only moved its design after it.

Two things are worth carrying forward rather than leaving in a file nobody
opens. The chrome design's persistence section describes a 9-to-10 settings
schema bump that never happened that way: the calibrated army composition took
version 10 on `main` first, so the chrome style shipped as version 11 with an
accepted window of `[10, 11]`, and the archive banner records that. The chrome
design's section 8 left one live question — whether linear filtering bleeds
across the nine-slice seams, and therefore whether a nested `PointClamp` batch
is worth splitting the interface batch into three — and that question, with the
mitigation spelled out, moved to the `CH-4` row's own preamble in the smoke
checklist instead of staying in the archived design. That row was run and passed
on 2026-08-15 and left the checklist with its family, so the question and its
mitigation now live in the archive record titled "UI chrome nine-slice smoke —
`CH-4`, and the family closing in full", which also records that the pass
carried no written observation of which scale tiers were reached. No source,
test, or live document cited any of the three by path, so nothing needed
rewriting into prose. The death-collapse family's ten `DC` smoke rows closed
`PASS` on 2026-08-15 and left the checklist; a plan is archived when its build
is finished, not when its rows are, which is why those two dates differ.

Swept a fourth time on 2026-08-14, and that pass changed the rule below rather
than only applying it. Eleven finished designs left this folder and none joined
it: the contingent shape design, the armor bulk, adornment accents, and trample
legibility design, the battlefield realism design, the combat cadence V6
design, the display DPI awareness design, the corpse placeholder design, the
last-stand engagement design, the strike-while-moving legibility design, the
cohort lateral spread design, the contingent chief membership design, and the
agent inspector row wrapping design. Each was checked against the code before
it moved rather than against its own status line — the enum member, the preset,
the constant, or the resolver branch it existed to ship is on `main`, and every
smoke row it owned had closed. Sixty-three path citations across thirty-seven
files were rewritten to name each document in prose, and one of those was not a
comment at all: `AgentInspectorContent.IntentGameplayModelNote` is a string a
spectator reads on screen, and it used to print a `docs/plans/` path at them.

Several of those eleven had been held here in earlier passes precisely because
source cited them by path. That is no longer a reason to keep a finished
design. What is left in this folder is work that is unfinished, unauthorized,
or still being argued about — with two exceptions named in the table: the
ranged design and the Sandata scaffold pair, which are live contracts rather
than finished packages.

Swept again on 2026-08-14, the third of that day's four passes. Five documents left
this folder and none joined it, and all five were plans whose build was finished
rather than designs: "Ranged units — plan", "Contingent shape — task plan
(Phase C)", "Last-stand engagement — plan", "Cohort lateral spread — plan", and
"Gait default visibility — plan". Each was checked against the code before it
moved — the enum member, the registry arm, or the constant it existed to ship is
on `main`, and each one's own status section records the gate it ran under. Two
things are worth carrying forward from that check rather than leaving in a file
nobody opens. The ranged package left four open items, and they live in the
ranged units handoff below, not in the archived plan. The gait default
visibility plan never had a row in this table at all and never pasted the
canonical gate output its own section 5 asked for, so its task table is verified
by the suites and not by the gate; that debt is recorded in its archive banner.
Thirty-nine citations of the five paths were rewritten to name each document in
prose: twenty-three across sixteen source, test, and fixture files, and sixteen
across eight live documents.

Reviewed before that on 2026-08-14, earlier the same day. Two documents left this
folder and none joined it: the inspector row wrapping plan and the contingent
chief membership plan, both archived once the smoke rows they existed to close
were run by a person and passed - `BR-10` for the first, `CS-1` and `CS-2` for
the second. Both of their design documents stayed here under the source-cited
rule below: `AgentInspectorContentTests.cs` names the first and
`ArmyCompositionPanelTests.cs` cites the second by path. Four documents changed
state without moving: the battlefield realism design's ten smoke rows all closed
that day, the cohort lateral spread design's rows 58 and 59 closed with them,
the contingent chief membership design's recommendation reached the screen, and
the contingent cohesion before contact design outlived the row that motivated
it - `BR-1` passed without a line of that design being built, so it stays here
as a standing diagnosis of gates nobody has touched rather than as pending work.

The review before those, also on 2026-08-14. Four documents left this folder in
that review and
none joined it: the lethal blow legibility plan and its design, and the pressure
interrupt observability plan and its design. All four were archived on the day
the smoke rows they existed to close were run and passed — rows 92 and 94 for
the first pair, and `P-1` through `P-10` plus `L-7` for the second. The lethal
blow pair was cited by path from fourteen doc comments across six files under
`src/Hukbo.Client` and `tests/Hukbo.Client.Tests`, which would have held it here
under the source-cited rule below; all fourteen were rewritten on the same day to
name the documents in prose, which is what the rule against paths into
`docs/archives/` requires of them, and once nothing cited them by path there was
nothing keeping them. **Both archived plans carry an unpaid gate debt, recorded
in each of them rather than hidden:** neither ever got the green
`./scripts/verify.ps1` run its own task table asked for, and the attempt made on
2026-08-14 failed at the build stage on concurrent, unrelated cohort deployment
work. Read the "How this closed, 2026-08-14" section in either document before
treating its task table as fully verified.

The review before it, on 2026-08-13. Five documents left this folder in that
review and one joined it. The five that left are the projectile prop scale plan, the
shield-clash audio legibility plan, the shield-clash audio legibility
design, and the auto camera centring plan and its design, each archived once
its build finished. The auto camera centring pair went last, after the
canonical gate was finally recorded against its task `AC-T6`, which had been
the one thing its task table still owed; neither file was cited by path from
any source or test file, so the source-cited rule below did not hold either of
them here. The design was held back at first
under this README's rule that a source-cited design stays live, because six doc
comments under `src/Hukbo.Client/Audio` cited it by path. It went later the same
day: those six comments were rewritten to name the document in prose instead,
which is what the rule against paths into `docs/archives/` requires of them, and
once nothing cited the design by path there was nothing keeping it here. The
constants those comments document — the `0.85` reference peak, the four voicing
rows, the melee-clash slot gate — are described in the comments themselves and
do not depend on the design surviving. The one that joined is the contingent
shape task plan, the
planning pass the 2026-07-29 contingent shape design had been waiting on since
it was written; it has itself since been archived, in the sweep recorded at the
top of this file. Two documents' states changed. The display DPI awareness
design's `UI-2`, `UI-4`, and `UI-6` rows were re-run by a person and closed
`PASS` on 2026-08-13. The contingent
shape design was corrected against its own evidence base and against the code,
and three of the four questions it left open are now closed in the new plan.

**A plan is archived when its build is finished, not when its smoke rows are.**
The battlefield-realism and projectile-props plans both left with rows still
`PENDING` when they were archived — projectile-props has since closed all eight
of its rows, the last of them `PP-3` on 2026-08-13 — because
those rows live in
[`../development/smoke-checklist.md`](../development/smoke-checklist.md) and are
tracked there. A finished design leaves on the same terms, and a path citation
from source or tests is not a reason to keep one: rewrite the citation to name
the document in prose, then archive it. That is what the rule against paths
into `docs/archives/` requires of the citation anyway, and it is how all eleven
designs archived in the fourth sweep of 2026-08-14 left.

## Hukbo

| Document | What it is | State |
| --- | --- | --- |
| [`TODO.md`](TODO.md) | The backlog. Every entry names the decision that parked it and the document holding its context | Parked work, nothing authorized |
| [`2026-08-14-contingent-cohesion-before-contact-design.md`](2026-08-14-contingent-cohesion-before-contact-design.md) | Why smoke row `BR-1` failed with "they visibly form up but not enough, some just charged and fought". The weapon grouping works; cohesion does not. Two gates compose into it: under `ContingentState.Advance`, `IsCohesionEligible` gate 4 denies every member that is not straggling, and a 24-body-radius cohesion square shared by eight contingents in one map half keeps the geometric gates failing, so contingents rarely reach the gathering `Hold` state at all. Section 3 is the binding historical constraint — a per-contingent local pause is a Provisional reconstruction, but tightening or dressing the group is barred outright by the corpus's only spacing finding | Design only; authorizes nothing. **No longer blocked** — `CohortLateralSpreadV13` landed and released `BattleSimulation.cs`, so the preset value to append is 14. Its plan directly below carries the tasks, and records five places where this design describes code that is not on disk |
| [`2026-08-14-contingent-cohesion-before-contact.md`](2026-08-14-contingent-cohesion-before-contact.md) | That design's sixteen tasks behind one new appended `MovementPresetId`, with the twenty-seed termination sweep as the gate on the whole change and movement preset V7 named as the precedent for a preset that behaved interestingly and never resolved a battle. Its findings section is the reason to read it before the design: R3's premise is false, because the cohesion square is already sized to the contingent at `jitterRaw + BodyRadiusRaw`; R2 is a no-op, because `ParticipatesInCrossContingentScan` already excludes exactly `Close` and `Break`; and R1 names the aim point where gate 4 measures to the leader | Plan only; authorizes nothing. All thirty-six of its file and line citations were checked against disk |
| [`2026-08-09-attack-animation-v2-backlog.md`](2026-08-09-attack-animation-v2-backlog.md) | What the twelve-task attack-animation plan left behind. **All twenty-four `AA` smoke rows closed `PASS` on 2026-08-13**, so its section 6, "Smoke rows still unobserved", is spent outright and its section 1 no longer describes a failing row. What survives is the engineering: sections 3, 4, and 5, and section 2's finding that `ConservativePawnCull` has no production caller — which is still true, and which a person's `AA-24` `PASS` did not change | Open on its engineering items only; every smoke row it names has closed |
| [`2026-07-28-follower-trailing-deadlock-design.md`](2026-07-28-follower-trailing-deadlock-design.md) | The follower-trailing mutual block in the collision resolver, with its diagnosis measured. Archived, revived, and executed on 2026-08-15. Its banner records what the attempt established: option 6.4 is refuted, and 6.5 — sliding along the obstruction — is the only remaining option that can move a warrior whose neighbour wants the same ground | Design; 6.4 tried and failed. 6.5 needs its own design before anyone builds it |
| [`2026-08-15-collision-mutual-lock.md`](2026-08-15-collision-mutual-lock.md) | The plan that executed the design above and closed with no code shipped. It holds the instrumentation counters that refuted rotation and swap detection: 14,218 of 14,791 candidates rejected because two members' claims overlapped each other, then 3,560 rotations committed under the corrected rule with the stall count unchanged at five. It also records that body radius 4.5 no longer stalls any of 200 seeds | **Closed 2026-08-15.** A record of what was tried; never execute its task list |
| [`2026-08-14-thousand-unit-performance-design.md`](2026-08-14-thousand-unit-performance-design.md) | Whether a 1,000-unit battle can be watched at all: the render matrix that predates the corpse layer, the gait legs, and the projectile props, and the per-tick scans in `BattleSimulation` that are quadratic in agent count. Section 2.4 closes the instanced-rendering question rather than leaving it open, and section 4.1 shows why a spatial index buys target selection nothing under the shipped scenario | Design only; authorizes nothing |
| [`2026-08-14-thousand-unit-performance.md`](2026-08-14-thousand-unit-performance.md) | That design's sixteen tasks in four phases, every one of them hash-neutral by construction, with a genuine stop condition at TU-4: if re-measurement shows the 1,000-unit frame already inside budget, the correct action is to run `GR-5` and close the workstream having written no code. `GR-3` closed `PASS` on 2026-08-15; `GR-5` remains open | Plan only; **not authorized**. Not started |
| [`2026-08-15-weapon-sprite-design.md`](2026-08-15-weapon-sprite-design.md) | Why every weapon in Hukbo is three colinear lines, and what an authored atlas of eighty cells — ten variants for each of the seven weapon roles and ten for the tall hardwood shield — would replace `DrawBlade` and `DrawShield` with. It leaves `PawnGeometry`, the arms, and the swing trail alone, and keeps the bowstring procedural because the bowstring is the one weapon element that genuinely deforms | Accepted; implementation authorized |
| [`2026-08-15-weapon-sprite.md`](2026-08-15-weapon-sprite.md) | That design's task list, behind a client setting that defaults off | Plan; in flight on branch `weapon-sprites`, nothing merged to `main` |

## Sandata

| Document | What it is | State |
| --- | --- | --- |
| [`2026-08-07-sandata-scaffold-design.md`](2026-08-07-sandata-scaffold-design.md) | **Sandata's binding document.** It outranks everything else about Sandata, including `CLAUDE.md`'s summary of it | Live contract |
| [`2026-08-14-sandata-scenario-and-roster-design.md`](2026-08-14-sandata-scenario-and-roster-design.md) | Where per-operator data lives once a mission stops being one hardcoded map and one hardcoded roster, which fields become authored, and what the map format version costs. Today `SandataGame.BuildInitialState` reads position, faction, and facing from the map's `SPAWN` records and fills every other field from placeholder constants | Design only; authorizes nothing. No scenario type exists on disk |
| [`2026-08-14-sandata-magazine-and-reload-design.md`](2026-08-14-sandata-magazine-and-reload-design.md) | How a magazine is consumed by fire and refilled by a reload. `OperatorState.MagazineRounds` is stored, snapshotted, and hashed, and no tick stage has ever changed its value; `SandataSimulation` says so by name at the site that would decrement it. Spare magazines are infinite by decision — a finite spare count is a stock-and-consumption economy and stays unauthorised | Design only; authorizes nothing. Not built |
| [`2026-08-14-sandata-blocked-mover-replan-design.md`](2026-08-14-sandata-blocked-mover-replan-design.md) | What counts as stalled, what stops re-request thrashing, and how the fixed-latency path rule applies to a mover that asks again. Seven decisions, none of them a task list | Design only; awaiting review. No re-request mechanism exists on disk |
| [`2026-08-14-sandata-clear-the-map-design.md`](2026-08-14-sandata-clear-the-map-design.md) | What an autonomous squad wants when nobody has drawn it an order: rooms first, then the map, with corners as the unit of clearing. Its stage 0 has since shipped — `RoomLayout`, `RoomClearStates`, and `GroupPathState.TargetRoomId` — and `MissionState`'s own remarks record that the shipped record carries no corner mask | Design only; authorizes nothing. Stage 0 shipped, stages 1 and 2 absent |
| [`2026-08-14-sandata-mission-does-not-end-design.md`](2026-08-14-sandata-mission-does-not-end-design.md) | The measured freeze: the shipped four-operator mission was ticked 3,000 times and never resolved. Four decisions, D1 through D4, from writing the selected intent into authoritative state to resolving a mission that can no longer progress | Design only; binds the plan below |
| [`2026-08-14-sandata-mission-does-not-end.md`](2026-08-14-sandata-mission-does-not-end.md) | That design's nine tasks. Task 1 answered the open sensing question — the frozen survivor is behind a wall and the sensing layer is correct — and dropped task 4; tasks 2 and 3 shipped at `51c0a86` and `ea3bbc1`. **Tasks 5 through 9 have not been built**. `OutcomeRules` already exists and already resolves the mission, but only by elimination; the stalemate predicate task 6 would add to it does not, so a mission nobody can win still cannot end. Read task 3 with care as well: it shipped as `ContactMemoryTests.cs`, not under the `RetargetOnDeathTests.cs` name the table gives it | Plan; partly built, five tasks open |
| [`2026-08-15-sandata-column-about-face-design.md`](2026-08-15-sandata-column-about-face-design.md) | How a squad reverses direction without deadlocking. A column whose path turns back the way it came stops permanently, because the follower ends up in front and stage 10 refuses the leader; this has been measured, reproduced, and mis-diagnosed three times. Decision 2 redefines `SquadSlot.LeaderEntityId` as the entity holding slot 0 | Design only; not authorized. Its staging is what the audit below executes |
| [`2026-08-15-sandata-column-about-face-stage-0-audit.md`](2026-08-15-sandata-column-about-face-stage-0-audit.md) | Stage 0 of that design's staging, read-only by definition: every production reader of `LeaderEntityId` audited against `main` at `cfe0c22`. Its answer is that no reader wants the lowest living id, so decision 2 does not reopen | Audit; stage 0 complete, later stages unstarted |

## Where the rest of it went

Finished plans, one-off orchestration prompts, and superseded handoffs live in
`docs/archives/`, in dated batches. The most recent batch is `2026-08-15`, and
it is where the live checklist emptied down to a single row. It holds thirteen
documents. Seven are smoke records — for the calibrated army composition, death
collapse, the UI chrome nine-slice family's last row, pawn visual fidelity, the
pawn sprite body, the hidden battle event log, and the render family's `GR-3`.
Three are the finished plans and designs behind two of those, the pawn sprite
body plan and design and the hide-the-event-log plan. Three more arrived from
the sweeps of that day: the attack animation V2 design, which shipped; the
ranged units session handoff, whose package closed; and the formation blocking
baseline, archived by user decision after a sweep had argued for keeping it.
Every one of the smoke records says the same thing about its evidence: the
tester returned pass verdicts and wrote no separate observation, and several of
the rows had asked for one. The batch
before it is `2026-08-14`, and
it grew through that day as one smoke family after another closed. It began with
"Last-stand engagement smoke — closed 2026-08-14", the record of a one-row
family: `LS-1` was the only row it had, so it closed at one of one and its
section was deleted from the live checklist whole. Three larger records joined
it when a person at an interactive Windows desktop ran three families to
completion in one sitting — "Leader identification smoke — closed 2026-08-14"
at eleven rows, "Movement gait animation smoke — closed 2026-08-14" at fourteen,
and "Ranged units smoke — closed 2026-08-14" at eleven — and all three of those
sections were deleted from the live checklist whole as well. Further records
were added to the same batch that day for other families as they closed, and the
day's last pass added five finished plans to it rather than smoke records:
"Ranged units — plan", "Contingent shape — task plan (Phase C)", "Last-stand
engagement — plan", "Cohort lateral spread — plan", and "Gait default
visibility — plan". Read
the folder itself rather than this paragraph for the full list. The batch before it is
`2026-08-13`, and it holds twenty-one documents, not two: sixteen of them are
the archive records
for the smoke rows a person ran at an interactive desktop that day and removed
from the live checklist, and the other five are the finished plans and designs
behind the fixes those runs called for. Some of those families closed entirely
and some only in part, and each record's own title says which —
"Typography smoke — closed 2026-08-13", "Responsive menu, startup display, and
UI motion smoke — closed 2026-08-13", "Sound gain compensation smoke — closed
2026-08-13", "Adornment accent legibility, smoke row 129 — closed 2026-08-13",
"Event feed lifetime smoke (T17) — closed 2026-08-13", "Tactical hit
animations smoke — closed 2026-08-13", "Last-stand formation smoke — closed
2026-08-13", "Persistent contingent smoke — closed 2026-08-13", "Quit
confirmation, maximize, and Core faction metrics smoke — rows 156 to 170
closed 2026-08-13", "Shield-clash audio smoke — closed 2026-08-13",
"Shield-clash loudness re-check smoke — closed 2026-08-13", "Leader marker and
inspector annotation smoke — six rows closed 2026-08-13", "Attack animation V2
smoke — closed 2026-08-13", "Projectile props and embedded projectiles smoke —
closed 2026-08-13", "Auto camera centring smoke — closed 2026-08-13", and
"Visual improvement smoke (VIS) — closed 2026-08-13".
The batch before it is
`2026-08-12`, and it holds two documents, not one: the Sandata smoke
checklist's own closed-row history and the record of the first two runs,
lifted out when six of that section's nine rows had closed and the prose
about them had outgrown the three rows a tester still had to run, and a
separate record of the auto camera modes smoke. The batch before it is
`2026-08-11`: the
projectile-props plan, the Sandata playable-client plan, and the Sandata wave-11
handoff. The batch before it is `2026-08-10`: the attack-animation V2
implementation plan and its continuation prompt, the gait animation plan, the
2026-08-08 ranged handoff, the Sandata wave-5 continuation prompt, and the three
July orchestration prompts. Read one to answer "why was it built this way";
never to decide what to do next. Do not link to one — the folder is deleted
periodically.

The seventh sweep added two documents to the `2026-08-15` batch, neither a smoke
record: the attack animation V2 design and the ranged units session handoff.
Each carries a banner saying why it left and, where it left something behind,
which `TODO.md` entry now holds it. It moved a third, the follower-trailing
deadlock design, and that one came back to `docs/plans/` the same day when the
user directed the work be finished; the archive holds no copy of it.

There is no session continuation prompt folder any more. The one document in
it, the 2026-08-10 Hukbo continuation prompt, had gone stale — three of the
five open ranged items it listed have since closed, including all eleven
`RG-*` smoke rows — and it was deleted on 2026-08-14 rather than left to be
read as current. Read the ranged units handoff above for what is actually
open, and `CLAUDE.md` for the contract.

Results and evidence do not live in this folder at all.
[`../development/testing.md`](../development/testing.md) holds the recorded
baselines and every interactive smoke checklist, and only a person at a desktop
may flip one of those rows.
