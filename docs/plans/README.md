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

Swept again on 2026-08-14, last of that day's three passes. Five documents left
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
tracked there. A design document is a separate question: it stays here for as
long as source or tests cite it by path, however long ago it shipped.

## Hukbo

| Document | What it is | State |
| --- | --- | --- |
| [`TODO.md`](TODO.md) | The backlog. Every entry names the decision that parked it and the document holding its context | Parked work, nothing authorized |
| [`2026-08-07-ranged-units-design.md`](2026-08-07-ranged-units-design.md) | The ranged design. Cited by name from `PhilippineCombatPresetV5.cs`, `RangedPhase.cs`, and the client's ranged geometry | Shipped; cited from source |
| [`2026-08-09-ranged-units-handoff.md`](2026-08-09-ranged-units-handoff.md) | What is still open on the ranged package: the sound-take listening acceptance, the V9 termination gap, and the default-composition decision. Its eleven `RG-*` smoke rows are no longer among them: a person ran all eleven and closed them `PASS` on 2026-08-14, and the family left the checklist. `RG-11` was an open question rather than a check, and it was closed without a written observation, so the question it asked is still unanswered | Current status document |
| [`2026-08-11-combat-cadence-v6-design.md`](2026-08-11-combat-cadence-v6-design.md) | Why `PrecolonialPhilippinesV6` exists and why it is the shipped default: the melee cadence retune that halves the on-screen artefact rate at a near-constant damage per tick, and the ceiling on how fast an attack animation may be played. Cited by path from `CombatIdentity.cs`, `PhilippineCombatPresetV6.cs`, `Scenario.cs`, and `DeterminismTests.cs` | Shipped at `982bd6f`; cited from source. All twelve `CL-*` smoke rows `PASS`. Its plan is archived |
| [`2026-08-11-battlefield-realism-design.md`](2026-08-11-battlefield-realism-design.md) | Why `BattlefieldRealismV10` exists: weapon-cohort deployment, shield bearers at the forward-most slots, and the ranged retreat rung. Sections 2.1 and 2.2 hold the evidence and the divergence register, all three behaviours shipping as a labelled gameplay model. Cited by path from `MovementPresetId.cs`, `MovementPresetRegistry.cs`, `FormationPlanner.cs`, `ContingentState.cs`, `ArenaGame.cs`, and `AgentInspectorContent.cs` | Shipped; cited from source. Its nineteen-task plan is archived. All ten of its smoke rows closed `PASS` on 2026-08-14 — `BR-5` through `BR-9` first, then `BR-1` through `BR-4` and `BR-10` once the fixes they were waiting on landed — and the family left the checklist whole |
| [`2026-08-14-contingent-cohesion-before-contact-design.md`](2026-08-14-contingent-cohesion-before-contact-design.md) | Why smoke row `BR-1` failed with "they visibly form up but not enough, some just charged and fought". The weapon grouping works; cohesion does not. Two gates compose into it: under `ContingentState.Advance`, `IsCohesionEligible` gate 4 denies every member that is not straggling, and a 24-body-radius cohesion square shared by eight contingents in one map half keeps the geometric gates failing, so contingents rarely reach the gathering `Hold` state at all. Section 3 is the binding historical constraint — a per-contingent local pause is a Provisional reconstruction, but tightening or dressing the group is barred outright by the corpus's only spacing finding | Design only; authorizes nothing. **No longer blocked** — `CohortLateralSpreadV13` landed and released `BattleSimulation.cs`, so the preset value to append is 14. Its plan directly below carries the tasks, and records five places where this design describes code that is not on disk |
| [`2026-08-14-contingent-cohesion-before-contact.md`](2026-08-14-contingent-cohesion-before-contact.md) | That design's sixteen tasks behind one new appended `MovementPresetId`, with the twenty-seed termination sweep as the gate on the whole change and movement preset V7 named as the precedent for a preset that behaved interestingly and never resolved a battle. Its findings section is the reason to read it before the design: R3's premise is false, because the cohesion square is already sized to the contingent at `jitterRaw + BodyRadiusRaw`; R2 is a no-op, because `ParticipatesInCrossContingentScan` already excludes exactly `Close` and `Break`; and R1 names the aim point where gate 4 measures to the leader | Plan only; authorizes nothing. All thirty-six of its file and line citations were checked against disk |
| [`2026-08-14-inspector-row-wrapping-design.md`](2026-08-14-inspector-row-wrapping-design.md) | Why smoke row `BR-10` failed on an axis it was not written for. The row expected vertical clipping; a tester found the text running out the side. The panel has no horizontal clip anywhere — both its bounds tests compare a row's bottom against a maximum row bottom — and only five prose blocks are wrapped, while roughly thirty rows reach a plain `DrawString` against a 277-pixel budget, the longest at 99 characters. Decision D4 rejects widening the panel and D5 states plainly that the panel's vertical fit at the minimum window is a separate, unfixed defect | Design only; its plan directly below carries the tasks. Shipped at `b566f88`; `BR-10` was re-run and closed `PASS` on 2026-08-14 and its family left the checklist. The plan is archived; this design stays live because `AgentInspectorContentTests.cs` cites it |
| [`2026-08-11-display-dpi-awareness-design.md`](2026-08-11-display-dpi-awareness-design.md) | Why the client declares per-monitor DPI awareness, why the font ramp was not the cause of the pixelated text, and why the P/Invoke deliberately carries no test | Shipped at `b1152f7`; `UI-2`, `UI-4`, and `UI-6` were re-run by a person and closed `PASS` on 2026-08-13, and the family left the checklist. This document stays in this folder rather than the archive because `src/Hukbo.Client/Program.cs`, `src/Hukbo.Diagnostics/LogEvents.cs`, and `tests/Hukbo.Client.Tests/ProcessDpiAwarenessTests.cs` all cite it by path, and this README's own rule keeps a source-cited design document live |
| [`2026-08-11-armor-accent-trample-legibility-design.md`](2026-08-11-armor-accent-trample-legibility-design.md) | Why three visual-improvement rows came back from a tester as "not clear", with a locatable cause behind each: armor drew as a flat recolour of the torso rather than as bulk, the adornment accent cap never scaled with the camera, and a trample mark was the same colour as the grass drawn on top of it. Cited by path from `GrassGeometry.cs`, `GrassRenderer.cs`, `PawnGeometry.cs`, `PawnRenderer.cs`, `SubmissionCount.cs`, and three Client test files | Shipped at `f6d5641`; cited from source. Row 129 closed `PASS` on 2026-08-13, and rows 128 and 131 each needed a second design, both of which are in this folder |
| [`2026-08-08-attack-animation-v2-design.md`](2026-08-08-attack-animation-v2-design.md) | The attack-animation V2 design. Authoritative over its backlog where the two disagree | Shipped |
| [`2026-08-09-attack-animation-v2-backlog.md`](2026-08-09-attack-animation-v2-backlog.md) | What the twelve-task attack-animation plan left behind. **All twenty-four `AA` smoke rows closed `PASS` on 2026-08-13**, so its section 6, "Smoke rows still unobserved", is spent outright and its section 1 no longer describes a failing row. What survives is the engineering: sections 3, 4, and 5, and section 2's finding that `ConservativePawnCull` has no production caller — which is still true, and which a person's `AA-24` `PASS` did not change | Open on its engineering items only; every smoke row it names has closed |
| [`2026-07-30-formation-blocking-baseline.md`](2026-07-30-formation-blocking-baseline.md) | Formation blocking at 500 agents, with the measured baseline a future change has to beat. Section 5 is the twenty-seed sweep the document's own section 3 asked for, run on 2026-08-13 under the presets the client actually launches: it shows a 146 per cent ordinary spread in `blockedAgentTicks` across seeds, which retires the two-seed comparison the document was built on, and it records a longest blocked streak of 904 ticks — 45 seconds of one warrior standing still | Backlog; authorizes nothing. The sweep is a fresh baseline and is explicitly not comparable to the 2026-07-30 table above it |
| [`2026-07-29-contingent-shape-design.md`](2026-07-29-contingent-shape-design.md) | Contingent shape, Phase C. Corrected on 2026-08-13 against its own evidence base and against the code: four citations misstated the research, two claims were refuted outright, and the design does not mention the determinism hazard that a changed lattice can change whether a jitter draw happens at all | Design only. Its planning pass is done, archived under the title "Contingent shape — task plan (Phase C)". All four of its open questions are now closed, the fourth on 2026-08-14 by withdrawing section 4's chief-per-contingent claim rather than by answering it |
| [`2026-08-14-contingent-chief-membership-design.md`](2026-08-14-contingent-chief-membership-design.md) | Prices the three ways out of the contingent-shape package's one undelivered task, with the line that destroys the planner's chief spread quoted, and finds a larger problem underneath them: `ContingentShapeV12` was registered but absent from the client's preset selector, so nothing in the shipped game could reach it | Design only; authorizes nothing. Its recommendation was taken on 2026-08-14. `CS-1` and `CS-2` were run by a person and closed `PASS` on 2026-08-14; the plan that executed this design is archived, and the design stays live here because `ArmyCompositionPanelTests.cs` cites it by path |
| [`2026-07-28-follower-trailing-deadlock-design.md`](2026-07-28-follower-trailing-deadlock-design.md) | The follower-trailing mutual block in the collision resolver, with its diagnosis measured | Design only; options unchosen |
| [`2026-08-13-last-stand-engagement-design.md`](2026-08-13-last-stand-engagement-design.md) | Why the endgame is still fought one pair at a time: a follower's aim point sits 51 world units behind its rally agent against a longest melee reach of 16. Three candidate remedies, each an authoritative change needing a new preset version and re-recorded goldens | Design only; it authorizes nothing itself, but its section 6 question was answered and remedy C was adopted — the plan that executed it is archived under the title "Last-stand engagement — plan". `LS-1` was re-run by a person and closed `PASS` on 2026-08-14, and the family left the checklist. This document stays in this folder rather than the archive because `src/Hukbo.Core/Movement/MovementPresetId.cs` cites it by path, and this README's own rule keeps a source-cited design document live |
| [`2026-08-14-cohort-lateral-spread-design.md`](2026-08-14-cohort-lateral-spread-design.md) | Why smoke row 58 failed with the weapon grouping working: `CohortDeploymentAssignment` ranks cohorts by size and deals them to contingents in ascending id order, and `FormationPlanner` maps ascending contingent id monotonically onto the lateral span, so the cohorts are poured across the map from one edge to the other and the shield bearers collect at one end. Section 5 finds no defect behind row 59 — the launched client owes an exact tick-0 mirror, and that row's rotating-roster premise describes `Scenario.CreateDefault`, not the player's build | Shipped at `541b8d6` as `MovementPresetId.CohortLateralSpreadV13`, the client's default. All five starting deployment rows passed on 2026-08-14 and the family left the checklist. Stays here rather than the archive because `CohortDeploymentAssignment.cs`, `MovementPresetId.cs`, `MovementPresetRegistry.cs` and `verify.ps1` cite it by path. Rows 58 and 59 were re-run against the V13 build and closed `PASS` on 2026-08-14, so the starting deployment family left the checklist whole |
| [`2026-08-13-strike-while-moving-legibility-design.md`](2026-08-13-strike-while-moving-legibility-design.md) | Why no warrior is visibly striking while walking: at the default camera fit `apparentScale` is 0.767 against a `MediumDetailScale` of 0.95, so a pawn has no legs at all, and a closing attacker under the arrival taper crawls at 1 raw unit per tick, which is one stride cycle every 300 seconds. Section 2 clears the simulation, section 5 clears `PlantStride` | Design only. Its section 6 question was answered on 2026-08-13 with a fourth option the document had not tabled, and the ordered task list that executed it is archived under the title "Gait default visibility — plan". **`AA-23` closed `PASS` on a later attempt the same day with nothing fixed**, so both causes stand and no row tracks them any more — which is why this document is live |
| [`2026-08-13-corpse-placeholder-design.md`](2026-08-13-corpse-placeholder-design.md) | Why smoke row 131 could not be run at all rather than merely failing: a fallen warrior is drawn for its death animation and then stops being drawn, so with no minimap and no position on an event-feed entry a spectator has nowhere to look for the ground the row asks about. Four rules keep a body on the field for the rest of the battle, unanimated, drawn beneath the living, without renumbering the ordinals the appearance cache addresses its slots by. Section 2 is emphatic that this is a placeholder and not a casualty system | Shipped at `4b9253d`; row 131 moved from `BLOCKED` back to `PENDING` when it landed and then closed `PASS` on 2026-08-13 |
| [`2026-08-14-ui-chrome-nine-slice-design.md`](2026-08-14-ui-chrome-nine-slice-design.md) | The first half of a two-package sprite request: a switchable nine-slice sprite skin for panel chrome, defaulting to today's flat rectangles. Section 4 keeps `UiPrimitives.DrawBorder` untouched and puts the style branch at the call site instead, because that helper has roughly twenty-four callers. Section 8 records that panel chrome draws inside the interface batch's `LinearClamp`, which bleeds a pixel-authored atlas across slice seams, and leaves the nested `PointClamp` batch to be decided by a smoke row rather than asserted. Section 10 carries a discrepancy found while planning and deliberately not fixed in passing: `SettingsSelectorCount` reads 5 against six selectors actually constructed | Design only; authorizes nothing. Six open questions, of which the placeholder atlas is the critical path |
| [`2026-08-14-ui-chrome-nine-slice.md`](2026-08-14-ui-chrome-nine-slice.md) | That design's nine tasks and six `CH` smoke rows. The first texture the repository has ever put through the content pipeline is isolated as its own gate-verified task, because the build and lock-file cost of a texture processor here is unmeasured rather than assumed small | Plan only; authorizes nothing. Not started |

## Sandata

| Document | What it is | State |
| --- | --- | --- |
| [`2026-08-07-sandata-scaffold-design.md`](2026-08-07-sandata-scaffold-design.md) | **Sandata's binding document.** It outranks everything else about Sandata, including `CLAUDE.md`'s summary of it | Live contract |
| [`2026-08-07-sandata-scaffold.md`](2026-08-07-sandata-scaffold.md) | The twelve-wave task plan and every wave's measured result | Executed and merged; task list empty, nine design questions open. Cited by path from 49 files under `src/` and `tests/`, so it stays here however finished it is |
| [`2026-08-12-sandata-order-and-combat-legibility-design.md`](2026-08-12-sandata-order-and-combat-legibility-design.md) | Why an authored path was never walked, why a rejected order was invisible, why the lowered weapon and automatic fire did not exist, and what the theme switcher and unknown-contact state are. Three of the four were a finished, tested rule with no production caller | Shipped 2026-08-12; cited from tests |
| [`2026-08-12-sandata-order-and-combat-legibility.md`](2026-08-12-sandata-order-and-combat-legibility.md) | That design's thirteen tasks across five waves, and the verification each one owed | Build finished and merged; both gates green. `SD-7b` was run and passed on 2026-08-14 and is archived. `SD-4` and `SD-5` were re-run the same day and failed a third time, so this package did not achieve what it was opened for |

## Where the rest of it went

Finished plans, one-off orchestration prompts, and superseded handoffs live in
`docs/archives/`, in dated batches. The most recent batch is `2026-08-14`, and
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

Session continuation prompts live in [`../prompts/`](../prompts/). The last one
written was the 2026-08-10 Hukbo continuation prompt, and it is now stale rather
than current: three of the five open ranged items it lists have since closed,
including all eleven `RG-*` smoke rows. Read the ranged units handoff above for
what is actually open, and `CLAUDE.md` for the contract.

Results and evidence do not live in this folder at all.
[`../development/testing.md`](../development/testing.md) holds the recorded
baselines and every interactive smoke checklist, and only a person at a desktop
may flip one of those rows.
