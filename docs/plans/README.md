# Plans — what is live, and what each document is for

Every file in this folder is live. Finished and superseded work moves to
`docs/archives/<YYYY-MM-DD>/` under the rules in
[`../archives/README.md`](../archives/README.md), so if a document is here it is
still load-bearing for something.

"Live" does not mean "in progress". A design document that authorizes nothing, a
backlog entry parked by user decision, and a completed package's measurement
record are all live, because a future session still has to read them. What none
of them means is *go and build this* — only an explicit authorization does that,
and section 6 of [`../../CLAUDE.md`](../../CLAUDE.md) says how one is given.

Last reviewed 2026-08-11, when five finished documents left for
[`../archives/2026-08-11/`](../archives/2026-08-11/): the projectile-props
plan, whose build merged at `3ec5523`; the Sandata playable-client plan, whose
tasks `P1`–`P9` merged at `5508310` and `c13b696`; the wave-11 handoff,
superseded when wave 12 closed; the combat-cadence V6 plan, whose twelve
tasks merged at `982bd6f` and whose four re-opened smoke rows then passed; and
the battlefield-realism plan, whose nineteen tasks merged and whose gate result
and twenty-seed V10 sweep were recorded at `0037bc9`.

**A plan is archived when its build is finished, not when its smoke rows are.**
The battlefield-realism and projectile-props plans both left with rows still
`PENDING`, because those rows live in
[`../development/smoke-checklist.md`](../development/smoke-checklist.md) and are
tracked there. A design document is a separate question: it stays here for as
long as source or tests cite it by path, however long ago it shipped.

## Hukbo

| Document | What it is | State |
| --- | --- | --- |
| [`TODO.md`](TODO.md) | The backlog. Every entry names the decision that parked it and the document holding its context | Parked work, nothing authorized |
| [`2026-08-07-ranged-units.md`](2026-08-07-ranged-units.md) | The ranged package's plan **and its record**. Section 9 is the narrative, with the measurements and the corrections to fifteen known-wrong task rows | Build closed and merged; section 9 is why this stays |
| [`2026-08-07-ranged-units-design.md`](2026-08-07-ranged-units-design.md) | The ranged design. Cited by name from `PhilippineCombatPresetV5.cs`, `RangedPhase.cs`, and the client's ranged geometry | Shipped; cited from source |
| [`2026-08-09-ranged-units-handoff.md`](2026-08-09-ranged-units-handoff.md) | What is still open on the ranged package: RU-31's listening acceptance, the eleven `RG-*` smoke rows, the V9 termination gap, and the default-composition decision | Current status document |
| [`2026-08-09-projectile-props-design.md`](2026-08-09-projectile-props-design.md) | In-flight projectile props and embedded projectiles, with the quad-budget arithmetic and five open decisions | Shipped at `3ec5523`. Its plan, which answered all five decisions and corrected the design's own quad arithmetic, is archived at [`../archives/2026-08-11/2026-08-11-projectile-props.md`](../archives/2026-08-11/2026-08-11-projectile-props.md) |
| [`2026-08-11-combat-cadence-v6-design.md`](2026-08-11-combat-cadence-v6-design.md) | Why `PrecolonialPhilippinesV6` exists and why it is the shipped default: the melee cadence retune that halves the on-screen artefact rate at a near-constant damage per tick, and the ceiling on how fast an attack animation may be played. Cited by path from `CombatIdentity.cs`, `PhilippineCombatPresetV6.cs`, `Scenario.cs`, and `DeterminismTests.cs` | Shipped at `982bd6f`; cited from source. All twelve `CL-*` smoke rows `PASS`. Its plan is archived at [`../archives/2026-08-11/2026-08-11-combat-cadence-v6.md`](../archives/2026-08-11/2026-08-11-combat-cadence-v6.md) |
| [`2026-08-11-battlefield-realism-design.md`](2026-08-11-battlefield-realism-design.md) | Why `BattlefieldRealismV10` exists: weapon-cohort deployment, shield bearers at the forward-most slots, and the ranged retreat rung. Sections 2.1 and 2.2 hold the evidence and the divergence register, all three behaviours shipping as a labelled gameplay model. Cited by path from `MovementPresetId.cs`, `MovementPresetRegistry.cs`, `FormationPlanner.cs`, `ContingentState.cs`, `ArenaGame.cs`, and `AgentInspectorContent.cs` | Shipped; cited from source. Its nineteen-task plan is archived at [`../archives/2026-08-11/2026-08-11-battlefield-realism.md`](../archives/2026-08-11/2026-08-11-battlefield-realism.md); ten smoke rows `PENDING` |
| [`2026-08-11-display-dpi-awareness-design.md`](2026-08-11-display-dpi-awareness-design.md) | Why the client declares per-monitor DPI awareness, why the font ramp was not the cause of the pixelated text, and why the P/Invoke deliberately carries no test | Shipped at `b1152f7`; `UI-2`, `UI-4`, `UI-6` are `PENDING` re-runs |
| [`2026-08-08-attack-animation-v2-design.md`](2026-08-08-attack-animation-v2-design.md) | The attack-animation V2 design. Authoritative over its backlog where the two disagree | Shipped |
| [`2026-08-09-attack-animation-v2-backlog.md`](2026-08-09-attack-animation-v2-backlog.md) | What the twelve-task attack-animation plan left behind | Open |
| [`2026-08-07-movement-gait-animation-design.md`](2026-08-07-movement-gait-animation-design.md) | The gait design — legs, feet, stride phase, tier gating | Shipped; `GA-1`–`GA-14` smoke rows `PENDING` |
| [`2026-08-07-unit-test-cleanup.md`](2026-08-07-unit-test-cleanup.md) | Which tests could be removed and which must not be. T1–T5 executed; T6 and T7 are a separate scope | Partly executed, remainder open |
| [`2026-07-30-formation-blocking-baseline.md`](2026-07-30-formation-blocking-baseline.md) | Formation blocking at 500 agents, with the measured baseline a future change has to beat | Backlog; authorizes nothing |
| [`2026-07-29-contingent-shape-design.md`](2026-07-29-contingent-shape-design.md) | Contingent shape, Phase C | Design only; needs a planning pass first |
| [`2026-07-28-follower-trailing-deadlock-design.md`](2026-07-28-follower-trailing-deadlock-design.md) | The follower-trailing mutual block in the collision resolver, with its diagnosis measured | Design only; options unchosen |
| [`UI/`](UI/README.md) | The 2026-07-31 UI and UX package — audit, visual direction, plan, implementation report | Implemented; manual smoke rows `PENDING` |

## Sandata

| Document | What it is | State |
| --- | --- | --- |
| [`2026-08-07-sandata-scaffold-design.md`](2026-08-07-sandata-scaffold-design.md) | **Sandata's binding document.** It outranks everything else about Sandata, including `CLAUDE.md`'s summary of it | Live contract |
| [`2026-08-07-sandata-scaffold.md`](2026-08-07-sandata-scaffold.md) | The twelve-wave task plan and every wave's measured result | Executed and merged; task list empty, nine design questions open |

## Where the rest of it went

Finished plans, one-off orchestration prompts, and superseded handoffs live in
`docs/archives/`. The most recent batch is
[`2026-08-11/`](../archives/2026-08-11/): the projectile-props plan, the Sandata
playable-client plan, and the Sandata wave-11 handoff. The batch before it is
[`2026-08-10/`](../archives/2026-08-10/): the attack-animation V2 implementation
plan and its continuation prompt, the gait animation plan, the 2026-08-08 ranged
handoff, the Sandata wave-5 continuation prompt, and the three July orchestration
prompts. Read one to answer "why was it built this way"; never to decide what to
do next.

Session continuation prompts live in [`../prompts/`](../prompts/). The current
one is
[`2026-08-10-hukbo-continuation.md`](../prompts/2026-08-10-hukbo-continuation.md),
which carries the verified baseline for both games, the five open ranged items,
and the hazards a fresh session would otherwise rediscover.

Results and evidence do not live in this folder at all.
[`../development/testing.md`](../development/testing.md) holds the recorded
baselines and every interactive smoke checklist, and only a person at a desktop
may flip one of those rows.
