# Last-stand formation smoke — closed 2026-08-13

**Archived: reference only.** All six rows below are `PASS` and were moved out
of `docs/development/smoke-checklist.md` on 2026-08-13, the day they closed.
Nothing here is outstanding and nothing here is an instruction.

The family closed in full — every row added by the last-stand formation change
on 2026-07-27 was attempted by a person at an interactive Windows desktop on
this date and passed. Like the auto camera modes family before it, it left
something behind: the tester's own report on row 76 named a second, real
problem that no row here states as a criterion — the final survivors still
fight as a sequence of one-on-one duels rather than as a group. That problem is
recorded below as Finding 1 and is now tracked as a fresh row, `LS-1`, in a new
section of the live checklist titled "Last-stand engagement smoke". Do not
re-run any row from this file. If a later change touches the rally behaviour
again, write a fresh row in the live checklist rather than reviving one of
these.

| Field | Value |
| --- | --- |
| Rows | 6 |
| Source family | 1 |
| Lifted on | 2026-08-13 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-13 |
| Machine/platform | Not captured by the tester. The reporting machine for this repository's recent runs is Windows 11 Pro 10.0.26200 x64 with an NVIDIA GeForce RTX 4070 SUPER on a 2560x1440 display at 125% Windows scaling |
| Source commit | Not captured by the tester. `main` was at `8da5d92` when these results were transcribed |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

## Last-stand formation smoke

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 76. Watch the endgame converge | Let a full 200-agent battle run to its final handful of warriors on each side. As each side thins out, its survivors visibly turn toward one another and gather instead of continuing to spread across the map. | The tester at the desktop reported: "passed, but not extremely clear. Since I am still seeing 1v1 in the endgame." The row's own criterion — survivors turning toward one another and gathering rather than continuing to spread — was met, so the row passes on its own terms. The one-on-one observation names a separate problem the row never stated, and it is recorded below as Finding 1 rather than folded into this row's status. | PASS |
| 77. Confirm the cluster is irregular | The gathered survivors form a ragged clump. They do not form a ring, a grid, a line, an arc, or any shape that looks placed. No warrior sits at an obviously exact distance from the one it gathered on. | The tester at the desktop reported: "passed", and asked whether the shape is historically accurate. Answered in Finding 2 below: it is not attested, but it is not contradicted either, and the repository already labels it correctly as a game-design invention. | PASS |
| 78. Confirm the cluster advances as a body | The gathered survivors travel toward the enemy together rather than one at a time. The group arrives roughly at once, and the fight that follows is a group fight rather than a sequence of separate duels. | The tester at the desktop reported: "passed". Read against Finding 1, this row passed on its first half — the survivors do travel together and arrive at once. Its second clause, "a group fight rather than a sequence of separate duels", is the clause Finding 1 contradicts, and `LS-1` in the live checklist is the row that now carries it. | PASS |
| 79. Watch a leader fall | When the warrior the group has gathered on is killed, the group re-forms on another warrior within a moment. The re-form is a short, small adjustment, not a sudden jump across the screen or a scatter. | The tester at the desktop reported: "passed". | PASS |
| 80. Inspect a regrouping warrior | Selecting a survivor that is closing on its comrades shows `Intent: Regrouping` in the inspector, and the battle event log shows its movement naming the warrior it is closing on rather than an enemy. The intent changes to `Attacking` once it is actually swinging at an enemy. | The tester at the desktop reported: "passed". | PASS |
| 81. Confirm regrouping never stops the fight | A warrior that is regrouping still strikes any enemy it passes within reach. The final engagement is not delayed by warriors refusing to fight while they are still gathering, and the match reaches a terminal outcome rather than two clusters standing apart. | The tester at the desktop reported: "passed". | PASS |

## Finding 1 — the endgame is still a sequence of duels, and the cause is the trail distance

Row 76's own criterion — survivors turning toward one another and gathering
instead of spreading — was satisfied, and the row passes on that basis. But the
tester's report carried a second observation the row was never written to
catch: "still seeing 1v1 in the endgame."

The cause was read out of the code rather than guessed, and it is a single
constant. A regrouping follower does not aim at its rally agent. It aims at a
point `RallyTrailRadiusMultiplier` body radii behind that agent, on the far
side from the enemy the agent is closing on, and it stops when it arrives
there. The multiplier is 12 (`src/Hukbo.Core/Simulation/FormationRules.cs:188`),
the default body radius is 4.25 world units
(`src/Hukbo.Core/Simulation/CollisionRules.cs:72`), so the trail point sits
**51 world units behind the leader**. The longest melee reach in the shipped
combat preset is 16 world units
(`src/Hukbo.Core/Combat/PhilippineCombatPresetV5.cs:188`).

A follower therefore parks more than three times the longest weapon reach
behind the warrior it gathered on, and holds there: the arrival guard returns
"propose no movement" once it is within contact distance of that aim point
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:3784-3792`). The rally agent
itself is exempt from regrouping
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:1426`), so it closes and fights.
Both factions do this at once, symmetrically, once each is at or below the
default last-stand threshold of six living agents
(`src/Hukbo.Core/Simulation/FormationRules.cs:104`, applied at
`src/Hukbo.Core/Simulation/Scenario.cs:260`). Two rally agents meet in the
middle and duel while every other survivor stands 51 units off; when one dies,
the next-lowest living `EntityId` becomes the rally agent and the same duel
happens again. That is precisely "a sequence of separate duels", which is the
failure clause row 78 was written to exclude.

This is deterministic and reproducible by construction, not a matter of a
particular seed.

**Why the constant cannot simply be lowered.** The type-level remarks on
`FormationRules` require `RallyTrailRadiusMultiplier` to exceed
`RallyJitterRadiusMultiplier * sqrt(2) + 2`
(`src/Hukbo.Core/Simulation/FormationRules.cs:180-188`). With the jitter
multiplier at 6 that floor is about 10.49, so 12 is barely above it. Lowering
the trail without lowering the jitter breaks the clearance the inequality
exists to hold. Both constants feed the state hash, so any change to either is
an authoritative simulation change requiring a new preset version and
re-recorded golden expectations under `CLAUDE.md` section 5 — which is why no
fix was made when this finding was recorded, and why `LS-1` in the live
checklist is a `FAIL` awaiting a decision rather than a `PENDING` re-run.

## Finding 2 — the ragged clump is a Provisional reconstruction, and is already labelled as one

Row 77 asks a tester to confirm the gathered survivors form a ragged clump
rather than a ring, a grid, a line, or an arc. The tester passed it and asked
whether that shape is historically accurate for the period the game depicts.

It is **not attested, and not contradicted**, and under the evidence tiers in
`CLAUDE.md` section 7 the claim "this shape is period-accurate" is a
**Provisional reconstruction**. No source in the repository's research corpus
describes a last stand, a rally radius, or a formation headcount for
pre-colonial Philippine warfare. What the corpus does record, in
`docs/research/battles/03-deep-past-formations-and-tactics.md`, is a "Not
attested" list that names regular files or ranks, fixed frontage and depth, a
shield wall, and a formal reserve, and a minimum defensible geometry of
irregular spacing with leaders embedded within or near their own followers,
explicitly labelled a plausible inference rather than a recovered formation.
The ragged clump is the only shape consistent with that: a ring, a grid, a
line, or an arc would land squarely on the "Not attested" list.

**Nothing needs correcting, because the repository already says this.**
`SIMULATION-GAME-STANDARDS.md` states in its last-stand section that the rally
formation is a game-design invention and not a historical claim, and that no
source documents a rally radius, a formation headcount, or a formation shape
for this period and region. `src/Hukbo.Core/Simulation/FormationRules.cs:3-7`
repeats it at the top of the constants themselves, and the individual tuning
values are each marked as provisional. The one place the code gestures at the
research — the give-way rule — is hedged as "plausible" rather than documented.
The answer to the tester's question is therefore that the behaviour is a
labelled game rule, and passing row 77 confirmed the rule holds, not a
historical fact.
