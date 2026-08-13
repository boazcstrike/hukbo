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

Last reviewed 2026-08-13. Five documents left this folder in this review and
one joined it. The five that left are the projectile prop scale plan, the
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
do not depend on the design surviving. The one that joined is
`2026-08-13-contingent-shape.md`, the
planning pass the 2026-07-29 contingent shape design has been waiting on since
it was written. Two documents' states changed. The display DPI awareness
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
| [`2026-08-07-ranged-units.md`](2026-08-07-ranged-units.md) | The ranged package's plan **and its record**. Section 9 is the narrative, with the measurements and the corrections to fifteen known-wrong task rows | Build closed and merged; section 9 is why this stays |
| [`2026-08-07-ranged-units-design.md`](2026-08-07-ranged-units-design.md) | The ranged design. Cited by name from `PhilippineCombatPresetV5.cs`, `RangedPhase.cs`, and the client's ranged geometry | Shipped; cited from source |
| [`2026-08-09-ranged-units-handoff.md`](2026-08-09-ranged-units-handoff.md) | What is still open on the ranged package: the sound-take listening acceptance, the eleven `RG-*` smoke rows, the V9 termination gap, and the default-composition decision | Current status document |
| [`2026-08-09-projectile-props-design.md`](2026-08-09-projectile-props-design.md) | In-flight projectile props and embedded projectiles, with the quad-budget arithmetic and five open decisions | Shipped at `3ec5523`. Its plan, which answered all five decisions and corrected the design's own quad arithmetic, is archived |
| [`2026-08-11-combat-cadence-v6-design.md`](2026-08-11-combat-cadence-v6-design.md) | Why `PrecolonialPhilippinesV6` exists and why it is the shipped default: the melee cadence retune that halves the on-screen artefact rate at a near-constant damage per tick, and the ceiling on how fast an attack animation may be played. Cited by path from `CombatIdentity.cs`, `PhilippineCombatPresetV6.cs`, `Scenario.cs`, and `DeterminismTests.cs` | Shipped at `982bd6f`; cited from source. All twelve `CL-*` smoke rows `PASS`. Its plan is archived |
| [`2026-08-11-battlefield-realism-design.md`](2026-08-11-battlefield-realism-design.md) | Why `BattlefieldRealismV10` exists: weapon-cohort deployment, shield bearers at the forward-most slots, and the ranged retreat rung. Sections 2.1 and 2.2 hold the evidence and the divergence register, all three behaviours shipping as a labelled gameplay model. Cited by path from `MovementPresetId.cs`, `MovementPresetRegistry.cs`, `FormationPlanner.cs`, `ContingentState.cs`, `ArenaGame.cs`, and `AgentInspectorContent.cs` | Shipped; cited from source. Its nineteen-task plan is archived; ten smoke rows `PENDING` |
| [`2026-08-11-display-dpi-awareness-design.md`](2026-08-11-display-dpi-awareness-design.md) | Why the client declares per-monitor DPI awareness, why the font ramp was not the cause of the pixelated text, and why the P/Invoke deliberately carries no test | Shipped at `b1152f7`; `UI-2`, `UI-4`, and `UI-6` were re-run by a person and closed `PASS` on 2026-08-13, and the family left the checklist. This document stays in this folder rather than the archive because `src/Hukbo.Client/Program.cs`, `src/Hukbo.Diagnostics/LogEvents.cs`, and `tests/Hukbo.Client.Tests/ProcessDpiAwarenessTests.cs` all cite it by path, and this README's own rule keeps a source-cited design document live |
| [`2026-08-11-armor-accent-trample-legibility-design.md`](2026-08-11-armor-accent-trample-legibility-design.md) | Why three visual-improvement rows came back from a tester as "not clear", with a locatable cause behind each: armor drew as a flat recolour of the torso rather than as bulk, the adornment accent cap never scaled with the camera, and a trample mark was the same colour as the grass drawn on top of it. Cited by path from `GrassGeometry.cs`, `GrassRenderer.cs`, `PawnGeometry.cs`, `PawnRenderer.cs`, `SubmissionCount.cs`, and three Client test files | Shipped at `f6d5641`; cited from source. Row 129 closed `PASS` on 2026-08-13, and rows 128 and 131 each needed a second design, both of which are in this folder |
| [`2026-08-08-attack-animation-v2-design.md`](2026-08-08-attack-animation-v2-design.md) | The attack-animation V2 design. Authoritative over its backlog where the two disagree | Shipped |
| [`2026-08-09-attack-animation-v2-backlog.md`](2026-08-09-attack-animation-v2-backlog.md) | What the twelve-task attack-animation plan left behind. **All twenty-four `AA` smoke rows closed `PASS` on 2026-08-13**, so its section 6, "Smoke rows still unobserved", is spent outright and its section 1 no longer describes a failing row. What survives is the engineering: sections 3, 4, and 5, and section 2's finding that `ConservativePawnCull` has no production caller — which is still true, and which a person's `AA-24` `PASS` did not change | Open on its engineering items only; every smoke row it names has closed |
| [`2026-08-07-movement-gait-animation-design.md`](2026-08-07-movement-gait-animation-design.md) | The gait design — legs, feet, stride phase, tier gating | Shipped; `GA-1`–`GA-14` smoke rows `PENDING` |
| [`2026-08-07-unit-test-cleanup.md`](2026-08-07-unit-test-cleanup.md) | Which tests could be removed and which must not be. T1–T5 executed; T6 and T7 are a separate scope | Partly executed, remainder open |
| [`2026-07-30-formation-blocking-baseline.md`](2026-07-30-formation-blocking-baseline.md) | Formation blocking at 500 agents, with the measured baseline a future change has to beat. Section 5 is the twenty-seed sweep the document's own section 3 asked for, run on 2026-08-13 under the presets the client actually launches: it shows a 146 per cent ordinary spread in `blockedAgentTicks` across seeds, which retires the two-seed comparison the document was built on, and it records a longest blocked streak of 904 ticks — 45 seconds of one warrior standing still | Backlog; authorizes nothing. The sweep is a fresh baseline and is explicitly not comparable to the 2026-07-30 table above it |
| [`2026-07-29-contingent-shape-design.md`](2026-07-29-contingent-shape-design.md) | Contingent shape, Phase C. Corrected on 2026-08-13 against its own evidence base and against the code: four citations misstated the research, two claims were refuted outright, and the design does not mention the determinism hazard that a changed lattice can change whether a jitter draw happens at all | Design only. Its planning pass is done and lives in `2026-08-13-contingent-shape.md`; three of its four open questions are now closed there |
| [`2026-08-13-contingent-shape.md`](2026-08-13-contingent-shape.md) | The planning pass Phase C asked for: nine ordered tasks, the blast radius across thirteen `FormationPlannerTests` and nine frozen digests, and the finding that `BattlefieldRealismV10` and `LastStandEngagementV11` — the presets the client actually ships — have no frozen trajectory digest at all | Plan only; authorizes nothing. Tasks 2, 8, and 9 are unblocked; tasks 1, 6, and 7 need decisions nobody has taken |
| [`2026-07-28-follower-trailing-deadlock-design.md`](2026-07-28-follower-trailing-deadlock-design.md) | The follower-trailing mutual block in the collision resolver, with its diagnosis measured | Design only; options unchosen |
| [`UI/`](UI/README.md) | The 2026-07-31 UI and UX package — audit, visual direction, plan, implementation report | Implemented; manual smoke rows `PENDING` |
| [`2026-08-13-last-stand-engagement-design.md`](2026-08-13-last-stand-engagement-design.md) | Why the endgame is still fought one pair at a time: a follower's aim point sits 51 world units behind its rally agent against a longest melee reach of 16. Three candidate remedies, each an authoritative change needing a new preset version and re-recorded goldens | Design only; it authorizes nothing itself, but its section 6 question was answered and remedy C was adopted — the plan row directly below carries the twelve tasks that executed it. `LS-1` was `FAIL` while no repair existed and has been `PENDING` since `MovementPresetId.LastStandEngagementV11` shipped |
| [`2026-08-13-last-stand-engagement.md`](2026-08-13-last-stand-engagement.md) | That design's twelve tasks, and the three decisions the design did not take. The last-stand code turned out to be shared and unversioned by every preset from V1 to V10, so a new movement preset is the only safe carrier for a change to it, the shipped client has to be moved onto that preset for the fix to be a fix at all, and the gate needs a fourth stage-5 block rather than the three re-recorded baselines the design asked for. Cited by path from `MovementPresetId.cs`, `MovementPresetRegistry.cs`, `ArenaGame.cs`, and `ScriptDefaultsTests.cs` | Executed and merged at `d17c8a3`; `MovementPresetId.LastStandEngagementV11` ships and the client selects it. `LS-1` is back to `PENDING` as a re-run |
| [`2026-08-13-lethal-blow-legibility-design.md`](2026-08-13-lethal-blow-legibility-design.md) | Why a kill does not read as a kill: the pawn is removed 0.10 seconds in while its effects run for 0.28 to 0.85, lethal blows are the only blows excluded from the hit pulse, the two ring colours are eleven units apart in one channel, and the default gore level produces no sustained blood. Section 4 records the evidence-based restraint the change deliberately reverses | Shipped 2026-08-13, presentation only; smoke rows 92 and 94 `PENDING` |
| [`2026-08-13-strike-while-moving-legibility-design.md`](2026-08-13-strike-while-moving-legibility-design.md) | Why no warrior is visibly striking while walking: at the default camera fit `apparentScale` is 0.767 against a `MediumDetailScale` of 0.95, so a pawn has no legs at all, and a closing attacker under the arrival taper crawls at 1 raw unit per tick, which is one stride cycle every 300 seconds. Section 2 clears the simulation, section 5 clears `PlantStride` | Design only; authorizes nothing, and its section 6 question is unanswered. **`AA-23` closed `PASS` on a later attempt the same day with nothing fixed**, so both causes stand and no row tracks them any more — which is why this document is live |
| [`2026-08-13-lethal-blow-legibility.md`](2026-08-13-lethal-blow-legibility.md) | That design's two parallel workstreams — hit effect, pulse, and hold; blood and gore level — with the value table each one owed and the pinned tests each one had to recapture | Executed; the gate result is recorded in the plan itself |
| [`2026-08-13-armor-bulk-second-fix-design.md`](2026-08-13-armor-bulk-second-fix-design.md) | Why smoke row 128 failed a second time, with the verdict "not bulky enough", after the first fix had replaced the solid armor slab with two flank bars. The first design decided in as many words to keep `MaxArmorWidthFactor` at 1.18 because the ceiling was not the problem; the second failure says it was part of it. Section 4 makes each bar exceed the capsule's own margin by at least a pixel so the armor carries the silhouette edge instead of sitting beside it | Shipped at `736c868`; row 128 closed `PASS` on its third run, on 2026-08-13, and the improve-visuals family finished at thirty-two rows of thirty-two |
| [`2026-08-13-corpse-placeholder-design.md`](2026-08-13-corpse-placeholder-design.md) | Why smoke row 131 could not be run at all rather than merely failing: a fallen warrior is drawn for its death animation and then stops being drawn, so with no minimap and no position on an event-feed entry a spectator has nowhere to look for the ground the row asks about. Four rules keep a body on the field for the rest of the battle, unanimated, drawn beneath the living, without renumbering the ordinals the appearance cache addresses its slots by. Section 2 is emphatic that this is a placeholder and not a casualty system | Shipped at `4b9253d`; row 131 moved from `BLOCKED` back to `PENDING` when it landed and then closed `PASS` on 2026-08-13 |

## Sandata

| Document | What it is | State |
| --- | --- | --- |
| [`2026-08-07-sandata-scaffold-design.md`](2026-08-07-sandata-scaffold-design.md) | **Sandata's binding document.** It outranks everything else about Sandata, including `CLAUDE.md`'s summary of it | Live contract |
| [`2026-08-07-sandata-scaffold.md`](2026-08-07-sandata-scaffold.md) | The twelve-wave task plan and every wave's measured result | Executed and merged; task list empty, nine design questions open. Cited by path from 49 files under `src/` and `tests/`, so it stays here however finished it is |
| [`2026-08-12-sandata-order-and-combat-legibility-design.md`](2026-08-12-sandata-order-and-combat-legibility-design.md) | Why an authored path was never walked, why a rejected order was invisible, why the lowered weapon and automatic fire did not exist, and what the theme switcher and unknown-contact state are. Three of the four were a finished, tested rule with no production caller | Shipped 2026-08-12; cited from tests |
| [`2026-08-12-sandata-order-and-combat-legibility.md`](2026-08-12-sandata-order-and-combat-legibility.md) | That design's thirteen tasks across five waves, and the verification each one owed | Build finished and merged; both gates green. `SD-4`, `SD-5`, and `SD-7b` are `PENDING` re-runs |

## Where the rest of it went

Finished plans, one-off orchestration prompts, and superseded handoffs live in
`docs/archives/`, in dated batches. The most recent batch is `2026-08-13`, and
it holds twenty-one documents, not two: sixteen of them are the archive records
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

Session continuation prompts live in [`../prompts/`](../prompts/). The current
one is
[`2026-08-10-hukbo-continuation.md`](../prompts/2026-08-10-hukbo-continuation.md),
which carries the verified baseline for both games, the five open ranged items,
and the hazards a fresh session would otherwise rediscover.

Results and evidence do not live in this folder at all.
[`../development/testing.md`](../development/testing.md) holds the recorded
baselines and every interactive smoke checklist, and only a person at a desktop
may flip one of those rows.
