# Starting deployment smoke — closed 2026-08-14

**Archived: reference only.** This is a finished record of manual testing that
has already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this project
remains `CLAUDE.md` and `docs/development/smoke-checklist.md`.

**This family closed in full at five rows of five**, and the section was deleted
from the live checklist under that file's rule that a family every one of whose
rows is `PASS` is a record rather than a checklist.

| Field | Value |
| --- | --- |
| Rows in the family | 5 — rows 58, 59, 60, 61 and 61a |
| Result | 5 `PASS`, 0 `FAIL`, 0 `BLOCKED` |
| Interactive runs | Two, both on 2026-08-14: one against the pre-`CohortLateralSpreadV13` build, and one against a build carrying it |
| Closed and lifted on | 2026-08-14 |
| Live checklist at the time | `docs/development/smoke-checklist.md`, section "Starting deployment smoke" |

## The two runs

The first run, against the pre-V13 build, passed rows 60, 61 and 61a and failed
rows 58 and 59. The second run, against a build carrying
`CohortLateralSpreadV13`, passed both of those.

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-14, both runs |
| Machine/platform | Not recorded |
| Source commit | Not recorded. The first run predates `CohortLateralSpreadV13`; the second carried it |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

## What row 58 caught, and what was changed for it

Row 58 failed with the weapon grouping itself working: each group did read as
mostly one weapon. What failed was where the groups sat. `CohortDeploymentAssignment`
ranked weapon cohorts by size and dealt them to contingents in ascending
contingent id, and `FormationPlanner` maps ascending contingent id
monotonically onto the lateral span, so the cohorts were poured across the map
from one edge to the other and the shield bearers collected at one end of each
team's line. The size key decided nothing, because planner-produced contingent
sizes are non-increasing in id and the id tie-break therefore decided
everything.

`MovementPresetId.CohortLateralSpreadV13` deals the cohort runs in a lateral
riffle instead — even contingent ids ascending, then odd — and became the
client's default. The reasoning is in
`docs/plans/2026-08-14-cohort-lateral-spread-design.md`, which is live rather
than archived because source cites it by path.

## What row 59 turned out to be

Row 59 was reported as the enemy team not mirroring, and no defect was found
behind it. The row's own premise was the problem: it asked a tester to accept a
weaker-than-exact mirror "under the default rotating roster", but the launched
client has no rotating roster — `ArenaGame.BuildScenario` always populates
`RosterCounts`, so both factions resolve identical loadouts per faction-local
index and tick 0 owes an exact per-index mirror. The rotating roster the old
wording described belongs to `Scenario.CreateDefault`, which is what the gate
and the headless runner use, not the player. The row was rewritten to ask for
the exact mirror, and to state that divergence after the battle starts is
expected rather than a failure, because per-warrior cohesion offsets and combat
rolls fold the absolute `EntityId` and faction 1's ids are offset by
`AgentsPerFaction`. It passed on the re-run, with the two sides showing the same
group counts.

The same false premise was in `BR-4` of the battlefield realism family, which as
written could only have been passed by a broken build. That row was corrected at
the same time and remains in the live checklist.

## Why this section existed

It was added by the mirrored starting-formation change. The automated evidence
proved the arrangement symmetric, separated and overlap-free in numbers; none of
it proved the opening frame read that way to a person watching it, which is the
only thing these rows were for. The persistent-contingent movement change
amended the section's premise, because the deployment groups stopped being an
opening-frame-only property. The battlefield-realism change reworded all five
rows for cohort-grouped deployment. The cohort lateral spread change reworded
rows 58 and 59 once more, and closed them.

## The rows as they closed

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 58. Read the opening frame | Added by the battlefield-realism change (`BattlefieldRealismV10`). Before the armies move, each side reads as several separate groups of warriors rather than one undifferentiated cloud, and each group reads as mostly one weapon cohort rather than an even mix of every weapon in the roster, at the default camera fit and without zooming in. Amended again by the cohort lateral spread change: those groups must also be spread across the team's own frontage rather than laid down it in sorted order, so that no single weapon cohort — the shield-bearing warriors above all — occupies one end of the line by itself. Failure is a field that reads as one undifferentiated cloud, groups whose weapon mix looks as uniform as a random cross-section of the whole army, or weapon cohorts collected toward one edge of a team's own frontage instead of distributed across it. | Failed 2026-08-14 on the pre-V13 build: the groups did each read as one weapon, but the cohorts were laid across the map in sorted order, with one cluster of shields and the weapon types unevenly distributed across each team's frontage. Re-run the same day against a build carrying `CohortLateralSpreadV13` and passed: the weapon groups read as spread across each team's frontage | PASS |
| 59. Check the mirror | Premise corrected by the cohort lateral spread change; the earlier "default rotating roster" wording was false for the launched client and is withdrawn. Pause at tick 0 before anything moves. The two halves are an exact reflection of each other across the vertical centre line: the same number of groups, the same group sizes, the same ragged front, the same weapon cohort in the mirrored lane, and shield bearers on the forward-most slots of a contingent on one side wherever they are on the other. Then unpause: the two armies are **expected** to drift out of exact symmetry as the battle runs, because per-warrior cohesion offsets and combat rolls are keyed on absolute entity id. Failure is the two halves not matching **at tick 0** — a different number or size of groups, a weapon cohort in a lane whose mirror holds a different one, or shield bearers forward on one side only. Divergence after the battle starts is not a failure of this row. | Failed 2026-08-14 on the pre-V13 build; the tester reported the enemy team not mirroring, and it was not reproduced in source or in tests. Re-run the same day against a build carrying `CohortLateralSpreadV13` and passed, the two sides showing the same group counts | PASS |
| 60. Confirm the groups look irregular | Within a group the spacing looks uneven rather than a snapped parade grid, and a new seed visibly reshuffles that spacing without moving the groups or changing which weapon cohort they read as. Failure is warriors within a group snapping to a visible grid or ring, or a new seed producing no visible change in spacing. | Observed 2026-08-14 on the pre-V13 build; the groups looked irregular | PASS |
| 61. Confirm the armies still meet promptly | The two sides close and fight without a long empty march, and the battle reaches a terminal outcome inside its tick limit. Failure is a long empty march before contact, or a battle that runs out the tick cap with no winner declared. | Observed 2026-08-14 on the pre-V13 build; the armies met promptly | PASS |
| 61a. Confirm the groups stay distinct past deployment | Added by the persistent-contingent movement change. Let the battle run several seconds past the opening frame, well before the armies meet. Each side still reads as several separate groups of warriors at the default camera fit, each still reading as mostly one weapon cohort, rather than merging into one crowd or losing its weapon identity as soon as the armies start moving. Failure is the groups blurring into one crowd within a few seconds of the opening frame, or a group's weapon identity becoming indistinguishable from its neighbours before the armies make contact. | Observed 2026-08-14 on the pre-V13 build; the groups stayed distinct past deployment | PASS |
