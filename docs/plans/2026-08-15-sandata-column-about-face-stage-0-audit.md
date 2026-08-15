# Sandata column about-face — stage 0: the `LeaderEntityId` reader audit

Stage 0 of the staging in `docs/plans/2026-08-15-sandata-column-about-face-design.md`
section 12. That design is binding and this document is subordinate to it.

This stage is read-only by definition. Nothing in the repository was changed to
produce it, and no behaviour question was settled by running code — every claim
below is source reading against `main` at `cfe0c22`, and the two claims that
would need a run to confirm are labelled as unconfirmed where they appear.

## The question stage 0 exists to answer

Decision 2 redefines `SquadSlot.LeaderEntityId` from "the lowest living entity
id in the component" to "the entity holding slot 0". The design asks whether
every existing reader wants the new meaning, and says that a reader which
genuinely wants the lowest living id is a reason to revisit decision 2.

**Answer: no reader wants the lowest living id, and decision 2 does not
reopen.** Every production read of `LeaderEntityId` treats it as a positional
role held for the current tick, not as an identity. The stable-identity role is
carried by `GroupId` everywhere it is needed, and `SquadSlot.cs:20`'s own
documentation already draws that distinction explicitly.

That is the narrow question, and it is answered. The audit also turned up five
findings that the design does not account for. None of them contradicts decision
2 and none of them is a reason to abandon the approach, but three of them
contradict statements the design makes about its own consequences, and they
should be resolved before stage 1 is implemented rather than discovered during
it.

## What reads `LeaderEntityId`

Four production sites, all in `src/Sandata.Core`, and none of them an identity
use.

| Site | What it does | Wants |
| --- | --- | --- |
| `SandataSimulation.cs:2568` | `AdvanceRoomSweep` finds which member is the leader, then uses its faction to gate retargeting and its nav cell as the origin of the next room-sweep search | The operator at the front |
| `SandataSimulation.cs:3345` | The formation anchor: the leader's projected arclength becomes the value every other slot's trail offset is subtracted from | The operator at the front |
| `SandataSimulation.cs:3091` | Documentation of the above | The operator at the front |
| `SandataSimulation.cs:3518` | `FindLeaderClearance` documentation, asserting the value is always non-null | The operator at the front |

Both behavioural sites are improved rather than merely preserved by the
redefinition. `:3345` in particular is the design's own argument made concrete:
anchoring the formation on the member furthest along the path is what the trail
offsets already assume, and today a mid-column leader places trailing slots
behind operators who are physically ahead of it.

The producer is `SquadGrouping.ComputeCore`, and the design's description of it
is accurate. Two independent ascending loops over the same roster decide leader
and slot index separately — lines 219 to 234 assign `leaderOfRoot[root]` to the
first living entity reached, and lines 242 to 252 assign slot indices by
counting living members. They agree only because both scan ascending and both
skip the dead. Nothing enforces the agreement.

Nine documentation sites state the old rule and will need rewriting: the
`SquadSlot` record summary at `SquadSlot.cs:26`, the `GroupId` contrast at
`SquadSlot.cs:20`, the slot-assignment note at `SquadSlot.cs:33`, the
`SquadGrouping` type summary at `SquadGrouping.cs:21`, the derivation notes at
`SquadGrouping.cs:119` and `:210`, and three passages in `SandataSimulation.cs`
at `:3489`, `:3494`, and `:3518`.

## Finding 1 — leader identity already writes hashed state

Design section 7 concludes that "nothing new is hashed and nothing new is
snapshotted", and reasons from `SquadSlot` being a per-tick span consumed within
the tick that computes it. The first half is literally true. The reasoning is
not, because stage 7 consumes the leader and writes authoritative state from it.

`AdvanceRoomSweep` selects `leaderIndex` at `SandataSimulation.cs:2568`, reads
that operator's cell at `:2609`, and writes it into the group record at `:2628`:

```csharp
updatedGroup = updatedGroup with
{
    TargetRoomId = nextRoomId,
    StartCellIndex = leaderCellIndex,
    GoalCellIndex = nextRoomCellIndex,
```

`StartCellIndex`, `GoalCellIndex`, `RequestTick`, and `HasOutstandingRequest` are
folded by `SandataStateHasher.cs:218-223`, and `TargetRoomId` by the gated tail
block at `:323`. Which operator holds slot 0 therefore decides five hashed
fields.

This is true today as well — today the leader is the lowest living id, and that
operator's cell seeds the request. What changes is that the deciding entity can
now differ from tick to tick. Section 10's oscillation risk is consequently not
confined to derived data: a slot-0 flip rewrites hashed state and issues a fresh
path request from a different origin. The stage 2 oscillation benchmark should
measure that, not only the identity churn.

## Finding 2 — slot order is a movement commit priority, not a label

Design section 3 says that nothing moves except the labels. That is not
accurate. `SlotIndex` is the second key of the collision resolver's commit
order, `SandataCollisionResolver.cs:394-396`, under the documented
`(GroupId, SlotIndex, EntityId)` ordering at `:82`, fed from
`MovementProposal.SlotIndex` through `LocalAvoidance.cs:151` and `:179`.
Reversing a column's slot order therefore reverses which of its members gets to
commit a move first when two members contend for the same space.

That is a real behavioural consequence and it is arguably the correct one — the
operator at the front should win the contested step — but it is a consequence
the design does not name, and it has a determinism implication that does.

**The seed-1 hash risk.** A group with no published path must produce exactly
today's slot numbering. Design section 6's first bullet already requires this:
every member projects to the same arclength, the tie-break yields ascending
entity id, and the result is today's answer. If the implementation instead skips
or renumbers slots for a pathless group, `SlotIndex` changes, the collision
commit order changes, and Sandata's seed-1 `stateHash` moves. The canonical gate
would catch that — it is the one part of this change the gate can see, precisely
because `HeadlessRunner.BuildInitialState` leaves `Groups` empty
(`src/Sandata.Headless/HeadlessRunner.cs:461`) and every operator is therefore a
pathless singleton.

## Finding 3 — the about-face is one tick late

`RunTick` computes slots at stage 6 and publishes paths at stage 7:

```csharp
// Stage 6.
var slots = new SquadSlot[view.Count];
ComputeSquadGrouping(view, slots);

// Stage 7.
AdvancePathService(currentTick, view, slots, sensing);
```

Stage 9 then samples the newly published polyline using slot indices derived at
stage 6 from the previous tick's path. On the tick a reversed path publishes,
the column is ordered against the route it is abandoning while walking the route
it has just been given. Decision 2's premise — that leader identity and slot
order are the same question — holds within stage 6 but not across the tick
boundary.

One tick is 20 milliseconds and the effect may well be invisible. It is recorded
here because it is a real gap between the design's model and the pipeline, and
because a single tick of backwards ordering is exactly the kind of thing that
shows up later as an unexplained one-frame jitter.

## Finding 4 — decision 6 contradicts the code it describes

Design section 8 says that a group in which every member is under a hand-drawn
order "keeps its previous published path and requests nothing new, which is
already what happens today, and its autonomous target continues to update in the
background".

Both halves of that sentence are wrong for the room sweep. Today
`LeaderEntityId` is the lowest living id regardless of any order, so
`leaderIndex` at `SandataSimulation.cs:2607` is non-negative and the group does
retarget. Under decision 6, if operators carrying an `OrderAssignment` hold no
slot, such a group has no slot-0 holder at all, `leaderIndex` stays `-1`, and the
`TrySelectNextRoom` block never runs. The autonomous target stops updating
rather than continuing in the background, and that is a behaviour change from
today rather than a preservation of it.

Separately, decision 6 has no structural exclusion to inherit.
`MovementSource.SlotTargetingRoster` has **no production caller** — every
reference outside its own file is a documentation mention or a test.
`TickStage.cs:110` claims the autonomous roster is filtered through it, which is
false; `SandataSimulation.cs:3064` is the honest description, which says the
exclusion happens implicitly through a per-operator branch on assignment
presence at `:3292`. Implementing decision 6 means writing that exclusion into
stage 6, where nothing like it exists today.

## Finding 5 — stage 6 has neither input the change needs

`ComputeSquadGrouping(TickStartView view, Span<SquadSlot> slots)` at
`SandataSimulation.cs:2318` receives only the frozen view. `TickStartView`
exposes positions, factions, aliveness, pairs, facing, health, suppression, and
contact memory, and no group, path, or order data at all. Ordering by arclength
needs the published polyline, and decision 6 needs `State.OrderAssignments`.
Both are reachable from the simulation instance, but stage 6 touches neither
today, so stage 1 introduces new coupling from the grouping stage to the path
service. Design section 12 anticipates half of this by widening
`SquadGrouping.Compute`'s signature; it does not mention the order assignments.

Two constraints on how that is implemented:

- `SquadTypesDeclareNoMutableStaticState` (`SquadGroupingTests.cs:328`) reflects
  over `SquadGrouping`, `UnionFind`, and `SquadSlot` and fails on any static
  field that is neither `readonly` nor `const`. Design section 10 suggests a
  reused buffer by analogy with `_contactMergeBuffer`, but that is an instance
  field on `SandataSimulation` while `SquadGrouping.Compute` is a static pure
  function. A static scratch array reddens that test immediately; `stackalloc`
  or a caller-supplied span is the available route.
- Four test call sites take the current signature and will need updating
  regardless of assertion survival: `SquadGroupingTests.cs:439` and `:474`,
  `MovementSourceTests.cs:322` and `:328`.

## What the existing tests do under the new rule

Every assertion in `SquadGroupingTests` and `MovementSourceTests` survives.
Those are direct unit calls with no path, so section 6's degenerate case applies
and the answer is ascending entity id — today's answer exactly. The golden
replays and the seed-1 baseline survive for the same reason, by way of the empty
`Groups` array in the headless builder.

Two `TickPipelineTests` fixtures are the real problem, and they are dangerous
precisely because they stay green:

- `RunTick_GroupLeaderInNarrowCorridor_CollapsesFollowerLateralOffsetToZero`
  spawns entity 1 at x=10 and entity 2 at x=12 on a due-east corridor path
  (`TickPipelineTests.cs:2284-2285`). Entity 2 is further along, so entity 2
  becomes slot 0. A leader has no lateral offset, so the assertion passes
  trivially and the clearance-driven collapse the test exists to prove is no
  longer exercised. The fixture's own comment — "one group with entity 1 as
  leader and slot 0, entity 2 as slot 1" — becomes false.
- `RunTick_NonLeaderSlotFarFromFormationPosition_DoesNotTeleportIntoPlace` has
  the same inversion, entity 1 at x=22 and entity 2 at x=34
  (`TickPipelineTests.cs:2033-2034`). Both assertions still hold, because the
  leader's own clamped step satisfies the same bounds, and the test stops
  exercising a non-leader slot at all.

Swapping the two spawn X values in each fixture restores the intent under either
rule, and should be done as part of stage 1 rather than left for the change to
quietly hollow out.

Ten assertions across `RoomSweepAngleHouseTests` are long real-map runs whose
room-selection origin is the leader's own cell. They are not analytically
decidable and must be re-run rather than reasoned about.
`OnceItsTargetIsDead_TheLeaderLeavesEngageAndTheSweepMovesOn` hardcodes entity 1
as "the leader" in both its scan at `:600` and its final assertion at `:613`,
and is the likeliest genuine failure, because entity 2 is the operator that
would reach the objective defender first.

## Spectator discoverability

Design section 11 argues that nothing new needs surfacing because the column's
motion shows the change directly. That argument depends on the motion being the
only thing a spectator could want, and it is worth recording what the inspector
actually offers today: nothing. `InspectorContent` carries a nullable
`SlotIndex` and no leader field at all, and `SandataGame.cs:2072` hardcodes that
slot index to `null`, so the row always renders as `Slot: -`. The client's own
comment at `:2041` acknowledges it. Surfacing slot or leader would take
`OperatorInspector.LineCount` from 13 to 14, which
`OperatorInspectorTests.cs:97` pins.

This is not a stage 0 blocker. It is the answer to the ninth question in
`SIMULATION-GAME-STANDARDS.md` section 10 being "by watching pawns walk, and
only that".

## One pre-existing defect found in passing

Not caused by this design, not fixed by it, and recorded so it is not lost.

`GroupId` is derived fresh every tick as the lowest entity id in the component,
ungated by aliveness at `SquadGrouping.cs:225`, but `MissionState.Groups[].GroupId`
is frozen at construction and never rewritten. Dead bodies produce no cohesion
pairs (`SandataCollisionGrid.cs:285`), so when the lowest-id member of a squad
dies, the surviving component re-keys to the next lowest living id. The group
record in `MissionState` keeps the old key, `PathService` lookups are keyed on
the derived one, and the two stop matching: `GetCurrentPath` returns empty and
the squad holds position, `GetReasonCode` reports `NoDestinationRequested`, and
`AdvanceRoomSweep`'s member scan matches nobody so the group never retargets
again. The same re-keying happens when two squads drift inside the cohesion
radius and merge, or when one spreads beyond it and splits.

`PathRequest.cs:33-38` states the precondition this violates — that the group id
"is stable for the group's lifetime" — and nothing enforces it. That same
passage also describes identity as "the minimum living entity id", which
contradicts the ungated derivation it documents.

**This is traced through five call sites and has not been reproduced.**
Confirming it needs a test with a non-empty `Groups` array and a killed
lowest-id operator. It is plausibly a squad-freezes-on-first-casualty bug and it
would be worth its own investigation, but it is not this design's problem and
should not be folded into stage 1.

## What stage 0 concludes

Decision 2 stands. Stage 1 may proceed on the design's terms, with four
corrections to the design's own account of its consequences — findings 1 through
4 — and with the two hollowed-out `TickPipelineTests` fixtures repaired as part
of the same change rather than after it.
