# Persistent contingent smoke — closed 2026-08-13

**Archived: reference only.** All twelve rows below are `PASS` and were moved
out of `docs/development/smoke-checklist.md` on 2026-08-13, the day they
closed. Nothing here is outstanding and nothing here is an instruction.

The family closed in full. Every open row added by the formation and movement
realism change was attempted by a person at an interactive Windows desktop on
this date and every one passed, reported as a single bulk verdict rather than
per-row notes. Do not re-run any row from this file. If a later change touches
contingent gathering, the contingent state machine, or the cross-contingent and
map-edge rules, write a fresh row in the live checklist rather than reviving
one of these.

| Field | Value |
| --- | --- |
| Rows | 12 |
| Source family | 1 |
| Lifted on | 2026-08-13 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-13 |
| Machine/platform | `Microsoft Windows 10.0.26200 (Windows 11 Pro) x64` |
| Source commit | Not recorded |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

## What these rows were for

These rows were added by the formation and movement realism change (T18 of
`2026-07-28-formation-movement-realism.md`), which flipped the default
`Scenario.MovementPreset` to `PersistentContingentsV2`. The mechanism they
cover runs for the whole battle, not only its ending: from deployment onward
each faction is divided into up to eight persistent contingents, and
`ResolveContingentStates` cycles each one between gathering on its own leader
and advancing independently throughout the match. That is a distinct
mechanism from the last-stand rally, which fires only once a side is down to
its final handful of warriors and gathers every survivor of that faction on
one rally agent; the last-stand family closed separately on 2026-08-13, and
only its `LS-1` follow-up row remained open in the live checklist. A
spectator was expected to be able to see both behaviours in the same battle
and tell them apart.

The automated suite behind this change — `MovementPresetRegistryTests`,
`FormationRulesTests`, `ContingentOffsetTests`, `ContingentStateMachineTests`,
`ArrivalTaperTests`, `PersistentContingentTests`, and
`ContingentDeadlockTests` — proves the state machine's six priority-ordered
transition rules, the duty cycle, the leader scan, the straggler gate, the two
geometric gates, the arrival taper, and three engineered deadlock geometries
all resolve correctly, both in isolation and inside a running simulation. None
of that proves that the resulting movement reads as a group of warriors
gathering and advancing together to a person watching it, which is the only
thing these twelve rows were ever for.

## The 2026-07-28 partial run and why three rows were reset

The section was only partially performed on 2026-07-28. Rows 102, 103, 104,
105, 111, and 114 were observed in one hands-off pass at the default camera
fit; rows 106, 107, 108, 109, 110, 112, and 113 were left unobserved; rows 104
and 114 failed on that pass. Row 111 passed on that same 2026-07-28 pass and
was lifted out of the live checklist earlier, on 2026-08-11, into that date's
"Closed rows lifted out of families that are still open" record, so it never
appeared as an open row in this section and is not one of the twelve closed
here.

Rows 102, 103, and 105 were then reset from their 2026-07-28 evidence back to
`PENDING`, with that evidence cleared, because the client's default preset
moved on to `BattlefieldRealismV10`. That preset groups each contingent's
warriors into weapon cohorts — a contingent reads as mostly one weapon, split
across at most `contingentCount - 1` boundaries — rather than the round-robin
mix these three rows were originally observed against. The recorded passing
evidence had described a group composition that no longer shipped, so the
rows had to be watched again under the cohort-grouped shape before they could
close. The 2026-08-13 desktop pass observed all twelve open rows, including
these three under the current default, and passed them all.

## Rows 104 and 114: the earlier failure and its cause

Rows 104 and 114 both failed at commit `8f4e426` on the 2026-07-28 pass. The
cause was movement transition rule 3 latching a whole contingent into
`ContingentState.Close` as soon as a single member of that contingent reached
contact — the rule tests the minimum distance over every member of the
contingent, so one warrior out of forty reaching an enemy put the whole group
into `Close`, and in a converged melee that condition never lifted again. Both
rows were reset to `PENDING` after commit `8f4e426` to await re-observation
under `PersistentContingentsV3`, following a fix referred to in the live
checklist as the `Close` latch fix (T7). The full measured record behind that
diagnosis and the subsequent re-measurement after the fix — the two sections
titled "Measurement behind rows 104 and 114" and "Re-measurement after the
`Close` latch fix (T7), 2026-07-28" — is not repeated here; it now lives in
`docs/development/measurement-history.md`. Under the 2026-08-13 pass, both
rows 104 and 114 passed, so the behaviour those measurements were taken
against now reads correctly to a person watching it under the shipped
default.

## What a later reader should be careful of

These twelve rows closed under the shipped default, `BattlefieldRealismV10`,
not under `PersistentContingentsV2`, the preset the rows were originally
written against and first exercised under on 2026-07-28. If a later question
turns on the exact preset in effect, do not treat this closure as evidence
about `PersistentContingentsV2` specifically — write a fresh row rather than
reading anything here as current for that preset.

## Two unmapped observations from the 2026-07-28 pass

Two observations from the 2026-07-28 pass did not map to any row in this
section and were carried forward in the live checklist rather than resolved
by a status. First, once one side was reduced to roughly twenty warriors, the
survivors fought in the centre of the map in what the observer described as a
line, taking each other on one at a time. Second, when two bodies of warriors
met, only the front rank appeared to be fighting, and the contact edge read as
a shallow concave curve. Neither observation was ever traced to a cause, and
both concern shapes that `docs/research/battles/03-deep-past-formations-and-tactics.md`
lists among the formations Hukbo should not present as historical. The first
of the two — survivors fighting one at a time as a side thins out — is now
tracked by the live checklist's `LS-1` row, opened out of the last-stand
formation family's own findings; the second, the front-rank-only concave
contact edge, remains unmapped to any row.

## Persistent contingent smoke

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 102. Read several distinct groups well past deployment | Each side stays readable as several distinct groups well past the opening frame, at the default camera fit, rather than merging into one crowd within a few seconds. | Passed 2026-08-13 | PASS |
| 103. Watch a strung-out group gather and resume | A group that has strung out visibly gathers on one of its own warriors, then resumes advancing, rather than gathering indefinitely or never gathering at all. | Passed 2026-08-13 | PASS |
| 104. Confirm the gathered shape is ragged | The gathered shape is ragged. It is not a ring, a line, an arc, a grid, or any shape that looks placed, and no warrior sits at an obviously exact distance from the one it gathered on. | Passed 2026-08-13 | PASS |
| 105. Watch a group arrive and break apart | On reaching the enemy, a group visibly stops holding together and its warriors fight as individuals. The transition reads as arriving, not as the group breaking apart. | Passed 2026-08-13 | PASS |
| 106. Confirm warriors ease into contact | Warriors ease into contact rather than travelling at full speed and stopping dead against an enemy body. | Passed 2026-08-13 | PASS |
| 107. Confirm a warrior steps aside for its leader | A warrior standing in front of the warrior its group has gathered on steps aside rather than being walked through or standing there blocking it. | Passed 2026-08-13 | PASS |
| 108. Inspect the contingent row | Selecting any warrior shows a `Contingent: <n> — <state>` row in the inspector, and that state changes over the course of the battle rather than reading the same value throughout. | Passed 2026-08-13 | PASS |
| 109. Confirm the contingent ground tints are distinguishable | The eight contingent ground tints within one faction are distinguishable from each other at the default camera fit, and no tint is mistakable for the opposing faction's colour, at all six themes. | Passed 2026-08-13 | PASS |
| 110. Confirm the frozen preset is unaffected | Running the same seed under `IndependentPursuitV1` looks exactly as the game looks today: no gathering, no per-contingent tint, and no contingent row in the inspector. | Passed 2026-08-13 | PASS |
| 112. Watch a group reach a map edge or corner | A group whose warriors reach a map edge or a corner keeps moving and fighting there rather than piling into the boundary and staying put. This is the visible face of the map-edge open-ground rule in design section 3.5. | Passed 2026-08-13 | PASS |
| 113. Watch two groups collide and separate | Two groups on the same side that walk into each other come apart again and carry on advancing, rather than jamming into one stationary mass. This is the visible face of the cross-contingent rule in design section 3.5. | Passed 2026-08-13 | PASS |
| 114. Watch whether gathering keeps appearing across the whole advance | Groups read as groups for the whole of the advance, not only in the first few seconds after deployment. Watch a full battle at the default camera fit and judge whether gathering behaviour keeps appearing across several different groups as the armies converge, or whether it happens once near the start and then stops. This is the spectator half of the inertness bar in design section 10.3 — the automated half asserts thresholds on how often cohesion is granted, and only a person can say whether the result looks like several groups advancing or like one crowd that briefly twitched. | Passed 2026-08-13 | PASS |

No per-row observation text was recorded for this pass. The tester at the
desktop gave a single bulk verdict covering all twelve rows rather than
individual notes, so the `Actual` column above states only that each row
passed on 2026-08-13 and invents nothing further.
