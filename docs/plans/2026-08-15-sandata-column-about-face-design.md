# Sandata — the column about-face: how a squad reverses direction without deadlocking

Status: DESIGN. Not authorized for implementation. A design document authorizes
nothing; the plan document that follows it, and only that document, may list
tasks.

Date: 2026-08-15.

## Section list

1. Purpose and scope
2. What ships today, and exactly where it stops
3. Decision 1 — what determines a column's order
4. Decision 2 — leader identity and slot order are the same question
5. Decision 3 — when the order is recomputed
6. Decision 4 — the total order and its tie-break
7. Decision 5 — what is hashed, and what stays derived
8. Decision 6 — interaction with hand-drawn orders
9. Decision 7 — why the hold is a prerequisite
10. Cost
11. Spectator discoverability
12. Staging
13. What this design does not decide

## 1. Purpose and scope

A Sandata squad cannot reverse direction. When its path turns back the way it
came, the squad stops permanently. This has been measured, reproduced, and
mis-diagnosed three times; this document exists so the fourth attempt is
designed rather than guessed.

The scope is one thing only: **which operator leads a column, and how that is
decided.** It is not a rework of the formation shape, not a change to collision
resolution, and not a change to pathfinding. Sections 3 through 9 answer seven
specific questions; section 13 lists what is deliberately left open.

## 2. What ships today, and exactly where it stops

`SquadGrouping.Compute` assigns both the leader and every slot index in
ascending entity-id order, and nothing else ever influences either:

- The leader of a group is the first living entity in ascending entity-id order
  (`src/Sandata.Core/Squads/SquadGrouping.cs:229-232`).
- Slot indices are handed out in that same ascending scan, so slot 0 is the
  lowest living entity id, slot 1 the next, and so on
  (`src/Sandata.Core/Squads/SquadGrouping.cs:246-251`).

Both are recomputed from scratch every tick, from nothing but positions,
factions, liveness, and entity ids. Neither is stored in `MissionState`, neither
is snapshotted, and neither is folded into the state hash. That is a genuinely
good property and this design does not give it up.

The consequence is that **entity 1 leads for the entire mission**, wherever it
physically stands. `ComputeMovementProposals` projects that operator's position
onto the group's published polyline and places every other slot behind it along
the same polyline
(`src/Sandata.Core/Simulation/SandataSimulation.cs:3313-3350`), so when a path
reverses, the operator that was correctly trailing is now physically in front,
and its own slot target lies behind the leader — which is where the leader is
standing.

Measured on `angle-house.hkmap`, at the sweep's retarget from the objective room
to the closet:

```
t=679  a=(432,120)  b=(423,120)  sep=8.7   walking east, b correctly behind
t=680  retarget to room 18576, which lies west
t=690  path publishes, reason=PathValid    neither operator moves again, ever
```

`CollisionBodyRadiusRaw` is 4352 raw
(`src/Sandata.Core/Simulation/SandataSimulation.cs:1703`), which is 4.25 world
units, so two centres require 8.5 world units of separation. The pair sits at
8.7. To follow the new path they must exchange positions, and a corridor with
0.2 world units of slack does not permit it. Stage 9 proposes a step every tick
and stage 10 refuses it every tick, indefinitely.

Three fixes were attempted against this and all three failed. Each was a real
defect and none was this one:

| Attempt | Real defect | Effect on the freeze |
| --- | --- | --- |
| `SlotTargets.ComputeTarget` clamped a negative slot arclength onto the path head, stacking every trailing slot on one point | yes | moved it 8 world units east |
| `ProjectArclength` clamped an off-path leader to arclength 0, so a leader that had walked past its own path head could never re-enter it | yes | moved it about 1 world unit |
| Holding unassigned operators while their group's path request is outstanding, so the leader never overshoots the head | yes, and shipped | moved it onto the head; squad still frozen |

The first two were reverted. The third is on `main` because it is correct
independently and because section 9 explains why this design needs it.

## 3. Decision 1 — what determines a column's order

**Decision: a column's order is derived from each member's projected arclength
along the group's current published path, descending, so the member furthest
along the path leads. Entity id remains the tie-break and nothing else enters
the comparison.**

The alternative — keeping ascending entity id and having the squad physically
swap places — is what fails today, and it fails for a reason no amount of
collision tuning removes: two bodies of radius `r` cannot exchange positions in
a corridor narrower than `4r` without one of them leaving the corridor. Sandata's
maps have corridors narrower than that by construction, so a fix that requires
swapping is a fix that works only on wide maps.

Ordering by progress along the path makes reversal free. When the path turns
back, the operator that was last is now furthest along the new path, so it
becomes slot 0 and the column walks the other way without anybody passing
anybody. Nothing moves except the labels.

`ProjectArclength` already computes exactly this quantity, once per tick, for the
leader (`src/Sandata.Core/Simulation/SandataSimulation.cs:3386`). This design
extends that call to every living group member rather than inventing a new
measure of progress. Section 10 addresses what that costs.

## 4. Decision 2 — leader identity and slot order are the same question

**Decision: `SquadSlot.LeaderEntityId` is defined as the entity holding slot 0,
and stops being an independent fact.**

Today they are computed by two separate passes over the same ascending scan and
happen to agree. Under decision 1 they must agree by construction, because the
leader is whoever the path says is in front. Keeping them independent would
allow a state where the group projects its formation onto one operator's
position while a different operator is actually leading, which is the exact
incoherence this design exists to remove.

This is the one place the change is not purely additive: every reader of
`LeaderEntityId` inherits the new definition. Section 12 makes auditing those
readers the first stage, before any behaviour changes.

## 5. Decision 3 — when the order is recomputed

**Decision: every tick, unconditionally, exactly as slots are recomputed
today. No reversal is detected and no reversal event exists.**

A tempting alternative is to detect that the new path's initial direction opposes
the column's current heading and re-index only then. This design rejects it. A
detector needs a heading to compare against, which is a remembered fact, which is
new state that has to be snapshotted, hashed, and reasoned about on resume — and
it introduces a threshold angle nobody has measured. Recomputing order every
tick from current progress needs no memory, no threshold, and no event: a
reversal is not a special case, it is what the ordinary rule produces when the
path turns around.

This mirrors the existing design's own instinct, recorded for faction alert level
in the scaffold design at section "Alert level", which rejected a decay timer
because a monotonic rule "needs no duration constant, no per-faction timer in the
hash".

## 6. Decision 4 — the total order and its tie-break

**Decision: order by projected arclength descending, then by entity id
ascending. Both keys are integers and the comparison is exact.**

Projected arclength is a raw fixed-point `long` produced by integer arithmetic
with no epsilon anywhere, so two members at genuinely equal progress compare
equal and fall through to entity id, which is unique and stable. This satisfies
`CLAUDE.md` section 5's requirement that every multi-result query has a total
order with ties broken on a stable `EntityId`.

Two consequences worth stating because they are behaviour, not implementation
detail:

- A group whose published path is empty has no arclength to order by. Every
  member projects to the same value, so the order degenerates to ascending
  entity id — precisely today's behaviour. The change is a no-op for a group
  that has never been given a path.
- Members are ordered by progress, not by distance to the goal. On a path that
  doubles back on itself these differ, and progress is the correct one: a member
  that has walked further along the polyline is ahead in the column even if it is
  momentarily closer to the start in straight-line terms.

## 7. Decision 5 — what is hashed, and what stays derived

**Decision: nothing new is hashed and nothing new is snapshotted. Column order
remains a derived, per-tick quantity.**

`SquadSlot` is not part of `MissionState`; it is a `Span<SquadSlot>` computed in
stage 6 and consumed in stages 7 through 9 within the same tick. Ordering by
arclength changes the values in that span but does not change its lifetime, so
the determinism contract is untouched: same seed, same build, same commands still
produce the same span, because arclength is an integer function of positions and
the published polyline, and the polyline is itself recomputed from the stored
request on resume.

The state hash will nonetheless move for any mission that has groups, because
operators end up in different positions. It will **not** move for the seed-1
headless workload or either golden replay fixture, because both build
`MissionState.Groups` empty
(`src/Sandata.Headless/HeadlessRunner.cs:461`, and
`tests/Sandata.Core.Tests/GoldenReplayTests.cs:118` which uses that same
builder). This is the fourth change in a row the canonical gate cannot see, and
section 12 treats that as a staging problem rather than an accident.

## 8. Decision 6 — interaction with hand-drawn orders

**Decision: an operator carrying an `OrderAssignment` is excluded from the
ordering comparison entirely, and neither leads nor occupies a slot while its
order stands.**

Design section 16 of the scaffold design states that an operator with a present
assignment follows the authored polyline and that there is "no third case and no
blend of the two". An operator walking a player's drawn route has no meaningful
progress along its squad's autonomous path, so including it would let a hand-drawn
detour silently re-elect the squad's leader. Excluding it also preserves the
existing structural exemption in `ComputeMovementProposals`, where the assignment
branch never consults the group path at all
(`src/Sandata.Core/Simulation/SandataSimulation.cs:3297-3311`).

A group in which *every* member is under an order has no orderable member. That
group keeps its previous published path and requests nothing new, which is
already what happens today, and its autonomous target continues to update in the
background per design section 16.

## 9. Decision 7 — why the hold is a prerequisite

**Decision: this design depends on the hold already merged at `a0595f0`, and
must not be implemented without it.**

`PathService.RequestPath` never clears `CurrentPath`
(`src/Sandata.Core/Navigation/PathService.cs:139-154`), so before the hold a
squad kept walking its old route for the whole `PathLatencyTicks` window — ten
ticks, 200 milliseconds — after every retarget. Ordering by projected arclength
against a stale polyline would elect a leader based on progress along a path the
squad is about to abandon, and would then re-elect a different one the moment the
real path published. The hold removes that window by keeping unassigned
operators still until their new path is valid, so the first order computed
against a new path is computed against the path the squad will actually walk.

## 10. Cost

**Per tick, per group:** one `ProjectArclength` call per living unassigned member
instead of one per group. `ProjectArclength` is a linear scan over the polyline's
segments with `Int128` products, already run once per group per tick. A squad is
single-digit in size and a published polyline is a handful of vertices after
smoothing, so this is a small constant multiple of an existing cost, not a new
order of magnitude.

**Sorting:** a group's members must be ordered by the two-key comparison. With
squads in the single digits this is a fixed-size insertion sort over a stack
span, not an allocation. `Dictionary<`, `HashSet<`, and `PriorityQueue<` are
banned in `Sandata.Core` and none is needed.

**Allocation:** none beyond the existing per-tick slot span, provided the sort
runs in place over a reused buffer, matching the convention `_contactMergeBuffer`
and `_deadEntityBuffer` already follow.

**The one unmeasured risk:** ordering by progress can, in principle, oscillate.
Two operators at nearly equal arclength whose projections cross back and forth
would swap slot 0 between them on alternating ticks, and since slot 0 defines the
formation's anchor, the whole formation would jitter. No measurement of this
exists. It is named here as the risk this design most needs a benchmark for, and
section 12 makes it a staging gate rather than something to discover on screen.

## 11. Spectator discoverability

`SIMULATION-GAME-STANDARDS.md` section 10's ninth requirement asks whether a
spectator can discover the effect without reading source code.

- **Which operator is leading.** Already visible: the squad walks in file and the
  operator at the front is the leader. Under this design that statement becomes
  true rather than coincidentally true, which is an improvement in legibility,
  not a new thing to surface.
- **That the squad reversed.** Visible directly — the column turns around and
  walks back the way it came, which is the entire observable point of the change.
  Today a spectator sees a squad stop forever and has no way at all to learn why.
- **Nothing new needs a HUD element or an event.** This design adds no
  authoritative field, so there is nothing a spectator would have to be told
  about that the motion itself does not already show.

## 12. Staging

**Stage 0 — audit `LeaderEntityId`'s readers.** Decision 2 redefines it. Before
any behaviour changes, enumerate every reader and confirm each one wants "the
operator at the front" rather than "the lowest entity id". This stage writes no
behaviour and is expected to produce either an empty list of surprises or a
reason to revisit decision 2.

**Stage 1 — order by arclength.** Implement decisions 1, 3, 4, and 6, with the
existing `SquadGrouping.Compute` signature widened to take the group's published
path. Verify against the three arrival tests already written and currently
failing on branch `sandata-hold-test`: the squad reaches the closet, every room
clears, and the leader's position is not identical across a long span.

**Stage 2 — the oscillation benchmark.** Section 10's named risk. Measure slot-0
identity changes per hundred ticks across a mission and state a number. If it
oscillates, the fallback to try first is a hysteresis margin on the arclength
comparison, which is a new constant and therefore needs its own justification —
it is not assumed here to be necessary.

**A note on the gate.** Per section 7 this change is invisible to
`./scripts/verify.ps1`, as the three before it were. The three arrival tests are
the real acceptance criteria and they run against the real `angle-house.hkmap`
fixture through a real `NavBake`. They are the bar; the gate is only the floor.

## 13. What this design does not decide

- **The formation shape itself.** Trail and lateral offsets, and
  `FormationCollapse`'s doorway gating, are untouched. This design changes who
  occupies which slot, never where the slots are.
- **Whether `SlotTargets.ComputeTarget`'s clamp should still be replaced by
  backwards extrapolation.** That clamp is a genuine defect — every trailing slot
  collapses onto the path's first vertex when the leader is at the head — and it
  was reverted only because it did not fix the deadlock. Whether it is worth
  fixing on its own merits is a separate question this design does not answer.
- **Whether `ProjectArclength` should extrapolate before a path's start.** Same
  situation, same reasoning.
- **Squads larger than a handful.** The cost argument in section 10 assumes
  single-digit squads. No design here says what happens at twenty.
- **Whether a reversal should cost time.** A real squad turning around in a
  corridor takes a moment. This design reverses the column instantly, on one
  tick, with no turn animation and no delay. Whether that reads as crisp or as
  teleporting is a question for whoever watches it.
