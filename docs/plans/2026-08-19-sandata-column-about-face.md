# Sandata column about-face — plan

**Status as of 2026-08-19: blocked, and not to be executed as written.** Tasks 1
through 3 were implemented in full on branch `sandata-about-face-attempt-4` at
`4c74336`. Decision 1 works as specified — the column reorders correctly, with
no oscillation — and it does not fix the deadlock; it relocates it and costs two
tests that passed before. Task 6's premise then failed too: bringing
`SlotTargets.ComputeTarget` into scope was tried in three variants and none of
them frees the map. The measurements and what they rule in and out are in
`docs/plans/2026-08-19-sandata-corner-trail-placement-findings.md`, which should
be read before anything below is attempted again. Tasks 4, 5, and 7 were never
started.

The three acceptance tests named below are on `main` and are failing there
deliberately.

Design: `docs/plans/2026-08-15-sandata-column-about-face-design.md`. That
document is binding and this one is subordinate to it. Where the two disagree,
the design wins and the disagreement is recorded here rather than resolved
silently.

Stage 0 is already complete and is its own document,
`docs/plans/2026-08-15-sandata-column-about-face-stage-0-audit.md`. It concluded
that decision 2 stands, and it raised five findings. Three of those findings
contradict statements the design makes about its own consequences, so this plan
records how each one is handled before any code is written.

Date: 2026-08-19. Branch: `sandata-about-face-acceptance`, worktree
`.claude/worktrees/sandata-about-face-acceptance`, based on `main` at `d83163a`.

## What this fixes

A Sandata squad cannot reverse direction. When its path turns back the way it
came, the operator that was correctly trailing is now physically in front, its
own slot target lies behind the leader, and the two would have to exchange
positions in a corridor that does not have the room. Stage 9 proposes a step
every tick and stage 10 refuses it every tick, forever.

The fix is decision 1: derive the column's order from each member's projected
arclength along the group's published path, descending, so the member furthest
along the path leads. When the path reverses, the operator that was last is now
furthest along the new path, so it becomes slot 0 and the column walks the other
way without anybody passing anybody.

## Acceptance criteria

The three tests already on this branch at `43ed199`, currently failing exactly as
intended:

- `SweepingTheRealMap_TheSquadReachesTheClosetRoom`
- `SweepingTheRealMap_EveryRoomIsClearedEventually`
- `SweepingTheRealMap_TheSquadNeverStopsForGood`

They drive the real `angle-house.hkmap` fixture through a real `NavBake` and
assert against real geometry rather than against the broken run's own
coordinates. This is the bar. The canonical gate is the floor, and per design
section 7 it cannot see this change at all, because both the seed-1 headless
workload and the two golden replay fixtures build `MissionState.Groups` empty.

**Both gates must still be green and must be reported as two results, never
one.** Sandata's recorded seed-1 baseline is `stateHash DA3D1BEB99978A75` with
`eventHash 260A20BC8F578E19`, and neither may move — see task 6.

## Corrections to the design, carried from the stage 0 audit

1. **Finding 4 — decision 6 as written stops the room sweep.** Design section 8
   says a group whose every member is under a hand-drawn order "keeps its
   previous published path and requests nothing new, which is already what
   happens today". Both halves are wrong: today `LeaderEntityId` is the lowest
   living id regardless of any order, so `AdvanceRoomSweep` always finds a
   leader index and always retargets. Under decision 6 that group would have no
   slot-0 holder, `leaderIndex` would stay `-1`, and the autonomous target would
   stop updating entirely.

   **Resolution: a component with no orderable member falls back to today's
   rule** — lowest living entity id leads, and living members take slots in
   ascending entity-id order. This preserves current behaviour in exactly the
   case the design mispredicted, and it costs nothing in the case decision 6
   actually cares about, where at least one member is free.

2. **Finding 2 — slot order is movement commit priority, not a label.** Design
   section 3 says nothing moves except labels. `SlotIndex` is the second key of
   the collision resolver's commit order, so reversing the column also reverses
   which member commits its step first under contention. That is a real
   behavioural consequence and arguably the correct one. It is accepted, not
   worked around, and it is why task 6 checks the seed-1 hash rather than
   assuming it.

3. **Finding 1 — the leader already decides hashed state.** `AdvanceRoomSweep`
   writes the leader's own cell into `StartCellIndex`, and that field is hashed.
   Slot-0 identity therefore reaches the state hash for any mission with a
   non-empty `Groups` array. This does not block stage 1, but it is why the
   stage 2 oscillation benchmark must measure path re-requests and not only
   identity churn.

4. **Finding 3 — the about-face lands one tick late.** Stage 6 computes slots
   before stage 7 publishes paths, so on the tick a reversed path publishes the
   column is still ordered against the route it is abandoning. It corrects on
   the next tick. Accepted as-is: one tick is 20 milliseconds, and the hold
   merged at `a0595f0` keeps the operators still through the whole latency
   window anyway. Recorded so a future one-frame jitter has a known cause.

5. **Finding 5 — stage 6 has neither input this needs.** `ComputeSquadGrouping`
   receives only the frozen `TickStartView`, which carries no group, path, or
   order data. The design anticipated widening `SquadGrouping.Compute`'s
   signature to take the group's published path, but a group's path is keyed by
   `GroupId` and `GroupId` is not known until the union-find has run. The
   signature cannot be widened that way without a circular dependency.

   **Resolution: two passes.** `SquadGrouping.Compute` keeps its current
   signature and its current job — components, group ids, and the ascending-id
   fallback ordering. The simulation then computes each operator's progress
   along its own group's published path, which it can do because it now knows
   every operator's `GroupId`, and hands that to a second entry point,
   `SquadGrouping.ReorderByPathProgress`, which rewrites `LeaderEntityId` and
   `SlotIndex` per component. `SquadGrouping` stays free of the path service and
   free of mutable static state.

## Tasks

Each task names its files and how it is verified. Tasks run in order; nothing
below starts before the task above it has actually reported.

### Task 1 — the ordering key

**File:** `src/Sandata.Core/Squads/SquadOrderKey.cs` (new).

A readonly struct carrying `GroupId`, `ProgressRaw`, `EntityId`, and
`ResultIndex`, implementing `IComparable<SquadOrderKey>` with the total order of
decision 4: group id ascending, then progress **descending**, then entity id
ascending. Sorting by group id first is what lets one sort serve every component
at once.

The comparison is exact integer comparison with no epsilon, as
`Sandata.Core` requires. Entity ids are unique, so the order is total and no two
keys ever compare equal — which means the sort's own stability is irrelevant to
the result.

**Verified by:** task 5's unit tests, and by the source hygiene test that already
scans `src/Sandata.Core` for banned tokens.

### Task 2 — `SquadGrouping.ReorderByPathProgress`

**File:** `src/Sandata.Core/Squads/SquadGrouping.cs`.

A new `internal static` method taking the already-computed slots, a progress
value per entity, an orderable flag per entity, and a caller-supplied scratch
span of `SquadOrderKey`. It sorts the scratch span in place, walks each run of
equal `GroupId`, and assigns `SlotIndex` 0, 1, 2 … to the orderable members in
sorted order, with `LeaderEntityId` set to the entity that took slot 0.

Rules it must hold:

- A non-orderable entity — dead, or carrying an `OrderAssignment` — gets
  `SlotIndex` null and is skipped when handing out slots, per decision 6.
- A component with **no** orderable member keeps whatever `Compute` already
  wrote, which is the ascending-entity-id fallback. This is correction 1.
- Nothing is allocated: the scratch buffer belongs to the caller, matching the
  `_contactMergeBuffer` and `_deadEntityBuffer` convention the design's section
  10 points at.
- `GroupId` is never rewritten. Group identity is not this method's business.

**Verified by:** task 5's unit tests, and by `SquadGroupingTests`'
`SquadTypesDeclareNoMutableStaticState`, which reflects over the type and fails
on any static field that is neither `readonly` nor `const`.

### Task 3 — wire stage 6

**File:** `src/Sandata.Core/Simulation/SandataSimulation.cs`,
`ComputeSquadGrouping` and two new instance buffers beside
`_contactMergeBuffer`.

After the existing `SquadGrouping.Compute` call, compute per entity:

- `orderable` = alive **and** carrying no `OrderAssignment`, via the existing
  `FindAssignment` binary search over `State.OrderAssignments`.
- `progressRaw` = `ProjectArclength` of the operator's position against its own
  group's current published path, or `0` when that path is empty. A group with
  no published path therefore has every member at progress 0 and falls through
  to the entity-id tie-break, which is today's answer exactly — this is what
  keeps the seed-1 workload's hash still.

Then call `ReorderByPathProgress`.

`ProjectArclength` needs a `PolylineArclength`, and `PolylineArclength.Build`
allocates. `ComputeMovementProposals` already builds one per operator per tick at
line 3352, so building one per orderable operator here is no worse than the
convention already in place. It is still wasteful, and if the stage 2 benchmark
shows it mattering, the fix is to build once per distinct `GroupId` rather than
once per operator. Not done pre-emptively.

**Verified by:** the three acceptance tests, and task 6's gate run.

### Task 4 — rewrite the documentation the redefinition invalidates

**Files:** `src/Sandata.Core/Squads/SquadSlot.cs`,
`src/Sandata.Core/Squads/SquadGrouping.cs`,
`src/Sandata.Core/Simulation/SandataSimulation.cs`.

The stage 0 audit enumerated nine documentation sites that state the old rule:
`SquadSlot.cs` at lines 20, 26, and 33; `SquadGrouping.cs` at lines 21, 119, and
210; and `SandataSimulation.cs` at lines 3489, 3494, and 3518. Each states or
implies that the leader is the lowest living entity id. Each needs to state the
new rule and to keep the `GroupId` contrast that `SquadSlot.cs:20` already draws,
since `GroupId` remains the stable-identity role and is now the *only* one.

**Verified by:** reading. No test covers a doc comment.

### Task 5 — tests for the ordering rule

**File:** `tests/Sandata.Core.Tests/SquadGroupingTests.cs`, or a new
`SquadOrderingTests.cs` if that file is already long.

At minimum:

- Two members on a path, the trailing one further along, become slot 0 and slot
  1 in progress order rather than entity-id order.
- Reversing the path swaps which of the two holds slot 0, with no other field
  changing.
- A group with an empty published path produces exactly the ascending-entity-id
  slots and leader it produces today. This is the regression guard for the
  seed-1 hash.
- An operator carrying an `OrderAssignment` holds no slot and does not lead.
- A group in which every member carries an `OrderAssignment` keeps the
  ascending-id leader — correction 1, asserted directly.
- Ties on progress break on ascending entity id.

**Verified by:** these tests passing, and by them failing when the comparison
keys are swapped.

### Task 6 — repair the two hollowed-out fixtures, then run both gates

**File:** `tests/Sandata.Core.Tests/TickPipelineTests.cs`.

The audit found two fixtures that stay green under the new rule while silently
ceasing to test what they name:

- `RunTick_GroupLeaderInNarrowCorridor_CollapsesFollowerLateralOffsetToZero`
  spawns entity 1 at x=10 and entity 2 at x=12 on a due-east path
  (lines 2284-2285). Entity 2 is further along, so entity 2 becomes slot 0, a
  leader has no lateral offset, and the clearance-driven collapse the test
  exists to prove is no longer exercised.
- `RunTick_NonLeaderSlotFarFromFormationPosition_DoesNotTeleportIntoPlace` has
  the same inversion at lines 2033-2034, entity 1 at x=22 and entity 2 at x=34.

Swapping the two spawn X values in each fixture restores the intent under either
rule. Their comments name entity 1 as the leader and must be corrected too.

Then run `./scripts/verify.ps1` and record both results separately in
`docs/development/testing.md`. Sandata's seed-1 pair must be unchanged at
`stateHash DA3D1BEB99978A75` / `eventHash 260A20BC8F578E19`. **If it moves, stop
and diagnose rather than re-pinning** — the design predicts it cannot move,
because the headless builder leaves `Groups` empty, so a move means the pathless
degenerate case is not degenerating to today's answer.

### Task 7 — stage 2, the oscillation benchmark

**File:** `tests/Sandata.Core.Tests/`, a measurement test in the style of the
existing calibration harnesses.

Design section 10 names this the one unmeasured risk: two operators at nearly
equal arclength whose projections cross back and forth would swap slot 0 on
alternating ticks, and since slot 0 anchors the formation, the whole formation
would jitter. Per finding 1 it is worse than jitter — a slot-0 flip rewrites
`StartCellIndex` and issues a fresh path request from a different origin — so
the benchmark measures **both** slot-0 identity changes per hundred ticks and
path requests per hundred ticks, over the angle-house mission.

If it oscillates, the fallback is a hysteresis margin on the arclength
comparison. That margin would be a new tuning constant and needs its own
justification; the design explicitly does not assume one is necessary, and none
is added unless the measurement demands it.

## Out of scope

Named here so they are not folded in during implementation. All five are the
design's own section 13 plus one the audit found:

- The formation shape, trail and lateral offsets, and `FormationCollapse`'s
  doorway gating.
- Whether `SlotTargets.ComputeTarget` should extrapolate backwards past the path
  head. A genuine defect, reverted once because it did not fix the deadlock.
- Whether `ProjectArclength` should extrapolate before the path's start. Same
  situation, same reasoning.
- Squads larger than a handful. The cost argument assumes single-digit squads.
- Whether a reversal should cost time. This reverses the column instantly, in
  one tick, with no turn animation and no delay.
- The pre-existing `GroupId` re-keying defect the audit found in passing: a
  squad whose lowest-id member dies re-keys its derived `GroupId` while
  `MissionState.Groups[].GroupId` stays frozen, after which path lookups stop
  matching and the group never retargets again. It is plausibly a
  squad-freezes-on-first-casualty bug, it deserves its own investigation, and it
  is not this change's problem.
