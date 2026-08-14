# Cohort lateral spread — design

**Archived: reference only.** Shipped at `541b8d6` as
`MovementPresetId.CohortLateralSpreadV13`, the client's default, and smoke rows
58 and 59 closed `PASS` on 2026-08-14. Its plan is archived beside this
document. Every path citation to it under `src/`, `tests/`, `scripts/`, and
`docs/` was rewritten on the day it was archived to name this document in
prose, which is what the rule against paths into `docs/archives/` requires.
Never execute it, never treat it as a live task list, and never cite it as the
reason to make a change. The live contract for this project remains `CLAUDE.md`
and `docs/development/testing.md`; nothing in this file overrides either of
those. Archived 2026-08-14.

Date: 2026-08-14
Status: proposed
Author: agent session working from smoke rows 58 and 59

## 1. What a person reported

A tester ran the starting-deployment smoke family in
`docs/development/smoke-checklist.md` and reported:

- **Row 58, fail.** The armies do read as separate groups, and each group does
  read as mostly one weapon — that part of the battlefield-realism change works.
  What is wrong is *where those groups sit*: the weapon cohorts are not spread
  across the team's own front. One end of a team's line is the shield-bearing
  group, another region is a single other weapon, and the assignment reads as a
  sorted list laid down from one edge of the map to the other rather than as an
  army whose weapon types are distributed across its frontage.
- **Row 59, fail.** The enemy team did not look like a mirror of the near team.
- **Rows 60, 61 and 61a, pass.** Within-group spacing is irregular and reseeds
  visibly, the armies still meet promptly, and the groups stay distinct past
  deployment.

This document proposes a fix for row 58 and states what the evidence actually
says about row 59.

## 2. Row 58 — the mechanism, traced in source

Two independent, individually reasonable rules compose into the reported
result.

**The lateral axis is the contingent id, in order.** `FormationPlanner` gives
contingent `c` the lane anchor
`region.MinY + (laneSpan * c) + (laneSpan / 2)`
(`src/Hukbo.Core/Simulation/FormationPlanner.cs:453`), with
`laneSpan = (region.MaxY - region.MinY) / contingentCount`
(`FormationPlanner.cs:113`). Contingent 0 therefore always occupies the lane at
minimum Y, and contingent `n - 1` always occupies the lane at maximum Y. Depth
is only a two-value alternation of the anchor X on the parity of the contingent
id (`FormationPlanner.cs:482-483`). Ascending contingent id is, exactly,
ascending position along the team's frontage.

**The cohort assignment consumes contingents in ascending id order.**
`CohortDeploymentAssignment` sorts warriors by cohort size descending, then
cohort key ascending (`CohortDeploymentAssignment.cs:142-155`), lays them out as
one contiguous sequence, and cuts that sequence into runs by walking
`contingentOrder` (`CohortDeploymentAssignment.cs:165-173`). `contingentOrder`
is sorted by slot count descending, then contingent id ascending
(`CohortDeploymentAssignment.cs:128-134`).

The second rule was written expecting those two keys to disagree. Under the
planner they never do. `SplitEvenly` hands the remainder to the earliest
indices — `sizes[index] = baseSize + (index < remainder ? 1 : 0)`,
`FormationPlanner.cs:317` — so planner-produced contingent sizes are
non-increasing in id, and both size-resolution rules
(`ResolveContingentSizesBySquareRoot`, `FormationPlanner.cs:250`;
`ResolveContingentSizesByChiefCount`, `FormationPlanner.cs:289`) route through
it. Sorting a non-increasing sequence by size descending with id ascending as
the tie-break returns `[0, 1, 2, … n-1]`. At 250 warriors a side the sizes are
`[36, 36, 36, 36, 36, 35, 35]`: the size key decides nothing at all and the
tie-break decides everything.

So the composed rule is: **rank the weapon cohorts by size, then lay them down
the map from one edge to the other in that order.** The largest cohort takes the
lane at minimum Y; the smallest cohorts take the lanes at maximum Y; a cohort
larger than one contingent takes a contiguous block of adjacent lanes. That is
precisely the "sorted list poured across the map" the tester described, and it
is what row 58 is failing on.

The pathology is already pinned as expected behaviour by a test:
`tests/Hukbo.Core.Tests/Movement/ContingentShapeV12Tests.cs:153` asserts that the
smallest cohort's warriors all land in contingent id 7 — the extreme lane —
which is only true because rank order equals id order equals lane order.

## 3. Row 58 — the proposed rule

Under a new movement preset, replace the traversal order of the run cut with a
**lateral riffle over contingent ids**: even ids in ascending order, then odd
ids in ascending order. For seven contingents that is `0, 2, 4, 6, 1, 3, 5`.

Properties this gives, all of them checkable:

- Two cohort runs that are adjacent in the size-ranked sequence are never in
  adjacent lanes, except at the single wrap point where the even pass hands over
  to the odd pass.
- A cohort large enough to span several runs is spread over non-adjacent lanes
  instead of occupying one contiguous block of the frontage.
- Shield-bearing cohorts, which sort late because they are the last two roster
  rows and neither is the largest cohort, no longer collect at one end of the
  line.
- The properties the battlefield-realism change bought are untouched: a
  contingent is still dominated by one weapon, the number of cohort splits is
  still at most `contingentCount - 1`, and shield bearers still take the
  forward-most slots of whichever contingent they land in
  (`CohortDeploymentAssignment.cs:196-245`, unchanged).

The size ranking is dropped for the new preset rather than kept alongside the
riffle. Under every planner-produced size table the ranking is a no-op, as
section 2 shows; keeping a key that decides nothing while a second key decides
everything is what made this defect hard to see in the first place. Contingents
are traversed by the riffle over ids alone, which is a total order over distinct
integers and therefore deterministic without a tie-break.

This is an integer permutation of values the caller already holds. It draws no
random numbers, allocates one `int[contingentCount]`, and cannot move the RNG
stream — the same discipline the existing assignment already follows.

## 4. Row 58 — versioning

The rule changes spawn positions, so it changes both hashes. Under CLAUDE.md §5
that requires a new preset version and new golden expectations, never an edit in
place to a registered preset.

- New `MovementPresetId.CohortLateralSpreadV13 = 13`, appended.
- Its ruleset restates `LastStandEngagementV11`'s fields verbatim, exactly as
  V11 restates V10's; only the folded `Id` separates the content hashes.
- It is admitted to `UsesBattlefieldRealism` and `YieldsLastStandEngagement`
  (`src/Hukbo.Core/Simulation/BattleSimulation.cs:5202`, `:1526`), so it inherits
  V11's behaviour whole.
- It is **not** admitted to the `ContingentShapeV12` branch of
  `FormationPlanner.ResolveContingentSizes` (`FormationPlanner.cs:233`), so it
  takes the square-root split like V11 does.
- The lateral riffle is gated on V13 alone. V10, V11 and V12 keep the ascending
  traversal and every pinned trajectory they already have.

Because the gate is preset-local, no existing golden expectation moves. That is
the whole reason for the new id.

**The client default flips to V13** (`ClientSettingsStore.DefaultMovementPreset`,
`src/Hukbo.Client/Settings/ClientSettingsStore.cs:84`) and V13 is added to
`ArmyCompositionPanel.MovementPresetOptions`
(`src/Hukbo.Client/UI/ArmyCompositionPanel.cs:113-124`). Without the flip the
fix is invisible to the person who reported the defect, which would make the
whole change worthless.

## 5. Row 59 — what the evidence says

I could not find a defect. In the build a player actually launches, the tick-0
deployment is an exact per-index mirror, and the pipeline has no branch that
could make it otherwise:

- `ArenaGame.BuildScenario` always populates `RosterCounts`
  (`src/Hukbo.Client/ArenaGame.cs:1480-1485`), and with it populated
  `ResolveSpawnLoadout` returns `rules.Roster[expandedRosterIndices[localIndex]]`
  keyed on the **faction-local** index for both factions
  (`BattleSimulation.cs:623-626`, `:717`, `:744`). Both factions therefore get
  identical loadouts per faction-local index.
- One canonical deployment is planned (`BattleSimulation.cs:649-652`), the cohort
  permutation runs per faction on that same canonical deployment before any
  reflection (`BattleSimulation.cs:708-711`), and faction 1 is then reflected in
  X alone (`BattleSimulation.cs:733-734`) with its contingent id carried through
  unchanged.
- The spawn repair pass clamps into a range symmetric about the map centre, so
  it is mirror-equivariant; its asymmetric ring-scan branch is unreachable at the
  default density.

The rotating roster that row 59's current wording is built on
(`CombatRuleset.ResolveLoadout`, `src/Hukbo.Core/Combat/CombatRuleset.cs:527`)
only applies when `RosterCounts` is empty. That is `Scenario.CreateDefault`,
which is what the gate and the headless runner use — **not** what the client
builds. Row 59's premise is therefore false for the launched game, and the row
asks a tester to accept a weaker mirror than the game actually provides.

Two things follow.

1. **The row's text is wrong and is corrected.** At tick 0 the launched build
   owes an exact mirror, and the row should say so.
2. **The mirror is expected to decay once the battle advances**, and the row
   should say that too, so that a tester who unpauses does not report a true
   behaviour as a failure. Cohesion jitter and every combat roll fold the
   absolute `EntityId` (`BattleSimulation.cs:3709-3712`, `:3919`;
   `ClashResolver.cs:66-67`; `HitLocationResolver.cs:94-96`), and faction 1's ids
   are offset by `AgentsPerFaction`, so the two armies diverge from the first
   cohesion tick onward by design.

The most likely explanation of the report is row 58 itself: when every team's
weapon groups are poured across the frontage in sorted order, each army looks
lopsided, and a lopsided pair is easy to read as "these two do not match". The
second candidate is the camera — the default auto-camera mode is `Assisted`
(`ClientSettingsStore.cs:66-67`), and a panned frame shows the two halves
unequally framed even when the world is symmetric.

Rather than change simulation code on that inference, this design adds a Core
test that asserts the exact per-index mirror **under the shape the client
actually builds** — 250 a side, `PrecolonialPhilippinesV5`, the new V13 movement
preset, and a `RosterCounts` derived the way `ArenaGame` derives it. The
existing mirror test covers 80 agents under V10
(`tests/Hukbo.Core.Tests/Movement/BattlefieldRealismV10Tests.cs:208`), which is
not the shipped shape. If that new test is green, the simulation mirrors and
row 59 needs a re-run against the corrected wording; if it is red, it has found
the defect the tester saw and this design is wrong about section 5.

## 6. The nine questions (`SIMULATION-GAME-STANDARDS.md` §10)

1. **User-visible outcome.** At the opening frame, each army's weapon groups sit
   spread across its own frontage instead of sorted from one map edge to the
   other; shield-bearing groups no longer collect at one end of the line.
2. **Tick stage and state.** Construction only, before tick 1: the spawn
   placement path in `BattleSimulation.Create`. It writes spawn position and
   contingent id and reads the resolved loadouts and the canonical deployment.
   No tick stage changes.
3. **Units and bounds.** Contingent ids are integers in `[0, contingentCount)`,
   `contingentCount` is at most 8 (`FormationPlanner`). No same-tick conflict
   exists: the assignment is a permutation of a fixed slot set, so two warriors
   can never claim one slot.
4. **Total ordering and random stream.** The riffle is a total order over
   distinct integer ids. The warrior ordering inside a contingent is unchanged
   and already ends in a distinct faction-local index. No random draw is added
   and no existing draw moves.
5. **Cache.** No cache.
6. **Save, event, version effect.** New preset id 13; V13's state and event
   hashes are new golden expectations. No existing preset's expectations move.
   No new event type and no new persisted field.
7. **Complexity and workload.** `O(warriors log warriors)` unchanged — the
   riffle is `O(contingentCount)` on a value of at most 8, run once per faction
   at construction. Benchmark workload: the canonical 200-agent, 10,000-tick,
   seed-1 headless run, plus the 500-agent report.
8. **Spectator explanation.** The effect is the opening frame itself, which is
   what rows 58 through 61a exist to judge. A warrior's contingent id is already
   shown in the agent inspector, so a spectator can confirm which group a warrior
   belongs to without reading source.
9. **Tests that fail before and pass after.** Listed in the plan document — a
   lateral-spread property test on V13, a regression test that V10/V11 keep the
   ascending traversal, and the shipped-shape mirror test from section 5.

## 7. Historical accuracy

No change. This moves where an existing, already-labelled gameplay model places
its groups; it makes no new claim about how any real force was arrayed. The
existing divergence notes in `docs/research/movement/README.md` and
`docs/research/movement/tall-hardwood-shield.md` — which record that the weapon
grouping is a deliberate gameplay model rather than an attested formation —
remain accurate and gain one clause about lateral spread.

## 8. Out of scope

- `FormationPlanner`'s lane geometry. Changing the anchor rule would move every
  preset's planned positions and redden both freeze suites; it is not needed,
  because the traversal order alone decides which cohort lands in which lane.
- Any change to the shield-forward rule inside a contingent.
- `ContingentShapeV12`'s authored `ContingentSizes` path.
- Making `EntityId`-keyed offsets mirror-symmetric. A permanently mirrored
  battle is a different feature and nobody has asked for it.
