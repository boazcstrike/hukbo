# Sandata — clear the map: what an autonomous squad wants, and how its destination is chosen

Status: DESIGN. Not authorized for implementation. A design document authorizes
nothing; the plan document that follows it, and only that document, may list
tasks.

Date: 2026-08-14.

User answer, verbatim, 2026-08-14: "clearing the map, looking at all corners;
prioritizing rooms first, then map."

## Section list

1. Purpose and scope
2. What ships today, and exactly where it stops
3. Decision 1 — where a room comes from
4. Decision 2 — what a corner is, mechanically
5. Decision 3 — when a room is cleared
6. Decision 4 — the total order: rooms first, then the map
7. Decision 5 — clearing versus engaging
8. Decision 6 — what is hashed, and what is emitted
9. Decision 7 — interaction with hand-drawn player orders
10. Decision 8 — cost
11. Spectator discoverability
12. Staging
13. What this design does not decide

## 1. Purpose and scope

Sandata's scaffold design left one question open longer than any other:
what does an autonomous squad actually want, moment to moment, and how does
it pick where to go? The scaffold design's own section 15 records the
question without an answer. Everything downstream of it — the fourteen-stage
tick pipeline's stage 7 path service, stage 8 intent selection, the squad
model in section 8, the order layer in section 16 — was built to consume a
destination without ever specifying where that destination comes from beyond
one placeholder.

This document answers it, for the assaulting faction, with the answer the
user gave on 2026-08-14: clear the map, look at every corner, clear rooms
before open space. It invents the new authoritative state that answer
requires — a notion of a room, a notion of a corner, a per-room cleared flag,
and a total order over rooms — states exactly what is derived versus what is
authoritative and hashed, and states exactly what is out of scope.

This document does not authorize implementation. It answers eight specific
questions decisively, states what a spectator sees, proposes a staged build
order, and lists what it deliberately leaves undecided. The plan document
that would follow it, if this design is accepted, is a separate file and is
not part of this one.

## 2. What ships today, and exactly where it stops

Today, the only source of a squad's destination is
`src/Sandata.Client/Simulation/InitialSquadGroups.cs`. It unions the
assaulting faction's operators into squads by the same collision-grid
union-find the squad model already runs every tick for group identity, and
for each resulting group it requests exactly one path: to the first
`OBJECTIVE` record in the loaded map, by ascending record index, requested
once at simulation tick zero. When that path arrives, `GroupPathState`
carries `HasOutstandingRequest = false` and nothing ever sets it back to
`true`. The squad walks to the one objective and stops. Nothing chooses a
next destination, because nothing exists that could: `PathReasonCode` has no
member for "finished, awaiting new orders," and stage 7's
`AdvancePathService` only drains and reconciles whatever is already in
`MissionState.Groups` — it does not decide what belongs there. Its own
remarks say so directly: no autonomous destination-request source exists in
this worktree.

`InitialSquadGroups` lives in `Hukbo.Client` — a **client-side**, presentation-
adjacent stopgap — precisely so that this one-shot placeholder would not move
the seed-1 headless workload's state hash: the headless runner never
constructs it, so `MissionState.Groups` has shipped empty for every one of
the eleven waves of the Sandata scaffold to date, and stage 7 has spent every
one of those ticks running its full search-and-publish machinery over an
empty collection.

Nothing in `Sandata.Core` today has a concept of a room. `NavBake.Bake`
builds `Passability[]` from `WALL` and `DOOR` records by supercover
rasterisation and Chebyshev body-radius inflation and stops there — it never
partitions the result into regions. The `.hkmap` format itself has no `ROOM`
record among its nine kinds (`HKMAP, NAME, GRID, WALL, DOOR, COVER, SPAWN,
OBJECTIVE, END`). There is no notion of a corner, a sight-line still owed, or
a per-region cleared flag anywhere in the codebase. All of it is new state
this document has to invent, and every field it invents has to be justified
against the same rules that already bind every other field in
`MissionState`: fixed hashed field order, `EntityId`-or-equivalent tie-breaks
on every multi-result query, no `float`, `double`, `System.Random`,
`Math.Sqrt`, `Math.Atan2`, `Dictionary<`, `HashSet<`, or `PriorityQueue<`
anywhere in `Sandata.Core` outside a doc comment.

## 3. Decision 1 — where a room comes from

**Decision: a room is derived, at simulation construction, by flood fill over
a new baked structure — not authored in the map file.**

The `.hkmap` format has no `ROOM` record, and adding one is a real map-format
change: a tenth record kind, a `MapCanonicalizer` change, a `MapContentHash`
change that moves every existing map's pinned content hash, and a hand-
authoring burden with no map editor to carry it. None of that is needed,
because the information a room needs already exists in the map as `WALL` and
`DOOR` geometry, and `NavBake.Bake` already proves that geometry can be
turned into a deterministic derived array without becoming authoritative or
hashed state — exactly as `Passability[]` and the clearance field are today.

A room, in this design, is a maximal 4-connected component of open grid cells
in a new derived array, `RoomBoundary`, where a cell counts as a room
boundary — not open, for this purpose — if it is blocked by a wall **or**
overlaps a door's rasterised footprint, regardless of that door's current
open/closed `State`. This is deliberately a different boundary than
`NavGrid.Passability`: `NavBake` already documents that an open door
contributes nothing to the passability bake, because passability answers "can
a body move through here right now." Room identity must not depend on a
transient door state, or a room's very existence would flicker open and shut
as a door opens and closes at runtime. `RoomBoundary` is therefore rasterised
once, at construction, straight from the map's static `WALL` and `DOOR`
segment geometry, independent of `Door.State` — the same supercover
rasterisation `NavBake` already uses for walls, applied a second time to
every door's footprint regardless of state.

Given `RoomBoundary`, room identity is derived by a single deterministic
flood fill:

- 4-connectivity only (north/east/south/west), never 8-connectivity. This
  matters specifically at rasterised diagonal walls: an 8-connected fill can
  leak diagonally past a single blocked corner cell, merging two rooms a
  human would call separate. 4-connectivity cannot.
- Seeds are visited in ascending `nodeIndex` order (`nodeIndex = y * Width +
  x`, the same indexing `NavGrid` already uses everywhere), i.e. a plain
  row-major raster scan. Every open cell not yet assigned to a component
  becomes a new seed in that order.
- Each component's flood order itself uses a fixed-size scratch array as a
  FIFO queue (a flat `int[]` sized to cell count, with head/tail indices —
  not a `Queue<T>`, `Dictionary<`, or `HashSet<`, all of which are banned
  from `Sandata.Core` outside doc comments), and visits a cell's four
  neighbours in a fixed, pinned order (north, east, south, west) every time.
- `RoomId` for a component is the lowest `nodeIndex` among its member cells —
  which, because seeds are scanned in ascending `nodeIndex` order, is always
  the seed cell itself. This is the same convention `GroupId` and `Group`
  leader identity already use elsewhere in this design: derived identity is
  the minimum stable index in the component, never a counter that depends on
  visitation order across runs.

Bit-stability follows from the same argument `NavBake` already relies on for
`Passability[]`: every input (`WALL`/`DOOR` segment geometry, in the map's
own canonical record order) is fixed integer data, every step (supercover
rasterisation, the flood fill's seed order, its neighbour order, its queue
discipline) is fully specified with no floating point and no reliance on
enumeration order of any hashed or unordered collection, so the same map
produces the same `RoomBoundary` array and the same `RoomId` assignment on
every run, on every platform, forever. `RoomBoundary` and the per-cell
`RoomId` lookup are derived structures exactly like `Passability[]` and the
clearance field: built once at construction, never snapshotted, never
hashed, and free to recompute identically on resume from nothing but the
loaded map.

One consequence worth stating plainly because section 6 depends on it: a map
whose `MapValidator` enclosure check already guarantees a fully enclosed
playable area (no unenclosed exterior in this game's maps) decomposes, under
this rule, entirely into rooms — a corridor with no doors along its own
length is one room in its own right, the same way a small chamber behind a
single door is. There is no leftover "outside the building" space. Decision 4
returns to what this implies for the "then the map" half of the ordering
rule.

## 4. Decision 2 — what a corner is, mechanically

**Decision: a corner is a blind pocket of a room — a maximal 4-connected
group of that room's cells with no line of sight to any of the room's
doorway cells — identified by the pocket's lowest-`nodeIndex` cell.**

The user's own word is "corners," and the genre this design draws on
(room-clearing, in the Door Kickers tradition this repository already names
as Sandata's reference) uses it the same way a real breacher does: the parts
of a room a doorway glance does not resolve, and that therefore have to be
walked to and looked at directly. That is a visibility fact, not a shape
fact, and this codebase already owns the machinery to test visibility
exactly: `LineOfSight.IsVisible`, the two-phase broad/exact query
`src/Sandata.Core/Navigation/LineOfSight.cs` already implements, with a
buffer-reuse overload specifically for a caller that runs the query many
times.

The construction, once per room, at simulation construction, alongside the
`RoomBoundary` bake in decision 1 — not per tick:

1. Collect the room's **doorway cells** — the room's own boundary cells that
   sit adjacent to a `DOOR` record's rasterised footprint. A room can have
   more than one doorway.
2. For every open cell belonging to the room, test `LineOfSight.IsVisible`
   from that cell's centre to the centre of every one of the room's doorway
   cells, using the existing `Span<int>` buffer overload so this bake does
   not repeat the exact allocation problem `LineOfSight`'s own remarks
   record already having been measured and fixed once, at roughly 4,684
   calls per tick before the buffer overload existed. A cell is **blind** if
   it is not visible from any doorway cell.
3. Flood fill the blind cells, 4-connected, in the same fixed seed and
   neighbour order as decision 1's room fill. Each resulting component is
   one **blind pocket** — one corner.
4. `CornerId` for a pocket is a small ordinal, 0-based, assigned in ascending
   order of the pocket's own lowest `nodeIndex` cell, scoped to that room —
   not a global id. The pocket's lowest-`nodeIndex` cell is its
   **representative cell**, the single point an operator's line of sight is
   tested against at runtime; nothing needs to test every cell of a pocket
   at runtime, only its representative.

A corner, formally, is a `(RoomId, CornerId)` pair naming one blind pocket,
and "looking at" a corner means some living assaulting-faction operator's
line of sight has reached that pocket's representative cell at least once
during the mission. This reuses two structures that already exist for other
reasons — `LineOfSight` for the visibility test, the room fill's own seed
and neighbour discipline for the pocket fill — rather than inventing a new
geometric "corner detector" over rasterised wall shapes. It is precise (a
pocket is a specific, enumerable set of grid cells), integer (every input
and comparison is a `nodeIndex` or a `LineOfSight` boolean), and testable (a
fixture map with a known partition behind a single door has a predictable,
countable pocket set a unit test can assert against directly).

Because `CornersSightedMask` (decision 6) is a 32-bit field, this bake must
assert at construction that no room produces more than 32 pockets, the same
way other parts of this codebase assert a hard cap at load time rather than
truncate silently. In practice a hand-authored room should produce a small
single-digit pocket count; a room that does not is very likely mis-authored,
and the assertion is the mechanism that would surface that rather than
silently drop a real corner. Deduplicating pockets that a jagged, diagonal
wall's stair-step rasterisation could otherwise fragment into many
one-cell pockets along a single true corner is an implementation-time
tuning question this design flags but does not resolve — see section 13.

## 5. Decision 3 — when a room is cleared

**Decision: a room becomes `Cleared` — permanently, for the rest of the
mission — on the first tick where every one of its corners has been sighted
at least once by a living assaulting-faction operator, and, on that same
tick, no living hostile-faction operator occupies a cell whose `RoomId`
equals this room's. Once `Cleared` is `true`, nothing sets it back to
`false`.**

Two facts have to both be true and both true on the same tick: the
corner checklist is exhausted, and the room is not, at that exact moment,
still occupied by a living threat. A room whose corners are all sighted
while a hostile still stands in it stays `Cleared = false` until a tick
where the hostile is also gone — dead, or having walked out — at which
point it clears immediately, without needing every corner to be re-sighted.

`Cleared` is deliberately **sticky** rather than continuously re-evaluated.
The alternative — recomputing "no hostile present" every tick and letting
`Cleared` flip back to `false` the moment a hostile re-enters an
already-cleared room — was considered and rejected, because it would make
the sweep-clearing target selector (decision 4) reopen a room the squad has
already fully swept every time a hostile wanders back through it, which
would starve genuinely unswept rooms of attention and produce visibly
indecisive squad behaviour. Safety against a hostile re-occupying a cleared
room is not this predicate's job: `IntentSelection`'s existing cascade
already ranks `Engage` above `Advance` unconditionally, driven by live
`ContactMemory` tier, completely independent of any room's `Cleared` flag.
A hostile who reappears in an already-cleared room still triggers `Engage`
on contact exactly as it would in an unswept one; only the destination
selector's decision to route the squad back through that room for a repeat
sweep is what `Cleared` being sticky suppresses.

This keeps the predicate itself simple and total-order-friendly (a boolean,
once set, never revisited) while relying on machinery that already exists —
`IntentSelection`'s cascade, `ContactMemory`'s tiered contact model — for
the actual safety property a spectator would otherwise expect from
"cleared."

## 6. Decision 4 — the total order: rooms first, then the map

**Decision: a squad's next destination is chosen by two nested
lexicographic minimisations, both tie-broken on a stable derived id, and
"rooms first, then the map" is a two-phase priority gate rather than a
single global ordering key.**

**Phase A — rooms.** Among every room reachable from the squad's current
position that is not yet `Cleared`, the squad's target room is:

```
argmin over not-Cleared, reachable rooms of
  ( octile path-cost heuristic from the squad's current cell
      to the room's nearest doorway cell,
    RoomId )
```

The heuristic distance is the same integer octile form design section 7
already pins for `Sandata.Core`'s A* (`10 * (max - min) + 14 * min`, no
epsilon, no floating point) — this design does not introduce a second
distance metric. `RoomId` — decision 1's stable, derived, minimum-`nodeIndex`
identity — is the tie-break, so two equidistant rooms always resolve the
same way on every run.

Within the target room, the squad's target corner is chosen the same way,
scoped to that room's still-unsighted corners:

```
argmin over unsighted corners in the target room of
  ( octile path-cost heuristic from the squad's current cell
      to the corner's representative cell,
    CornerId )
```

The squad's actual pathfinding destination is that corner's representative
cell. When the target room's every corner has been sighted but the room is
not yet `Cleared` because a hostile still occupies it, decision 5 governs:
the squad does not need a new walking destination, because `IntentSelection`
already routes it into `Engage`. When a room has no reachable, not-yet-
`Cleared` room at all, Phase A is exhausted for that squad.

**Phase B — the map.** Decision 1 already states the consequence this phase
inherits: because every map's playable area is fully enclosed (per
`MapValidator`'s own guarantee) and `RoomBoundary` splits at every door
regardless of state, a fully door-partitioned map decomposes entirely into
rooms, with no residual "outside any room" space left over. Phase B is
therefore the fallback for whatever that decomposition does not claim —
for instance a room-less or lightly-partitioned map, or any nav cell a
future map's geometry leaves outside every room's flood fill — and its rule
is the same argmin over remaining unswept nav cells, tie-broken by
`nodeIndex`, run only once Phase A has no candidate left. This design states
the fallback's existence and its ordering rule; it is written honestly as
likely to be a no-op on a fully enclosed, fully door-partitioned map, and
section 13 records that as an open, unmeasured question rather than a
settled fact about any specific shipped map.

"Rooms first, then the map," concretely: a squad exhausts Phase A — every
reachable room `Cleared` — before Phase B's fallback selection ever runs for
that squad. There is one ordering rule per phase, not one rule spanning
both, because a room and an arbitrary open cell are not comparable objects
under the same distance-and-id tuple until Phase A has nothing left to
offer.

## 7. Decision 5 — clearing versus engaging

**Decision: `Engage` interrupts nothing. No new "resume the sweep" state is
introduced, because none is needed — the sweep's destination already lives
in the same `GroupPathState` the order layer's own hand-drawn-order
suspension already resumes from unchanged. What changes is a freeze on
choosing a *new* destination while any group member is engaging.**

`IntentSelection`'s cascade already ranks `Engage` (an identified contact)
above `Advance` (following a path), unconditionally, for every operator,
every tick. This design does not touch that cascade and does not need to:
an engaging operator halts under existing movement-stage logic regardless
of what its group's current destination is, while the *group's* path
request — a per-group fact, not a per-operator one — is untouched in
`MissionState.Groups`. The moment the contact clears, whether by the
hostile dying or leaving detection range, the operator's own
`PathReasonCode`-driven `Advance` resumes toward the same still-valid
destination automatically, with no separate bookkeeping, because the
destination was never revoked in the first place. This is exactly the
resume behaviour design section 16's order layer already relies on for its
own case 4 — "autonomy resumes on the same tick" — inherited here for free.

The one genuine addition this design makes: the corner/room re-targeting
step described in decision 4 must not run, for a given group, on a tick
where any living member of that group holds `Engage` intent. Without this
freeze, a group mid-fight whose current corner target happens to become
sighted by a teammate mid-engagement could immediately retarget to a new
room and start walking a still-fighting operator's squadmates away, which
would look like the squad abandoning its own fight. The freeze needs no new
authoritative field: whether any living group member holds `Engage` this
tick is a fact already computable from stage 5's sensing outcome — the same
per-operator contact-tier result stage 8's `SelectIntents` reads — and stage
7's `AdvancePathService` can read that same already-computed fact before
deciding whether to issue a new destination request. The pause and its
resume are therefore both free consequences of reusing existing per-tick
facts, not new state that has to be snapshotted, hashed, or reasoned about
on save/resume.

## 8. Decision 6 — what is hashed, and what is emitted

**Decision: one new hashed collection on `MissionState`, folded in after the
last field the state hasher already covers; two new fields appended to the
end of `GroupPathState`'s existing field list; one new, appended
`MissionEventKind` member. No per-corner event — corner progress is a state
read, not a feed entry.**

### 8.1 New authoritative state

A new record, `RoomClearState`, one entry per derived room, present from
tick zero (every room gets an entry; this is not a sparse collection the way
`OrderQueue`/`OrderAssignments` are):

```
RoomClearState(
    RoomId: int,               // decision 1's stable identity
    CornersSightedMask: int,   // one bit per CornerId, 0-based, within this room
    Cleared: bool)             // decision 3's sticky flag
```

`MissionState` gains `RoomClearStates: ImmutableArray<RoomClearState>`,
ordered ascending by `RoomId` — the same ordering convention every other
`MissionState` collection already uses — folded into the state hasher
**after** the last field it currently covers (`EventFeed`'s own fold point).
This is an append-only change to the hasher's field order, the same shape as
the `OrderQueue`/`OrderAssignments` addition design section 16 already made,
and it does not disturb any field that came before it.

`GroupPathState` gains two fields, appended after its existing ones
(`GroupId, DestinationCellIndex, HasOutstandingRequest, StartCellIndex,
GoalCellIndex, RequestTick`):

```
GroupPathState(
    ..., existing fields unchanged, ...
    TargetRoomId: int,     // decision 4's current Phase A/B target room
    TargetCornerId: int)   // decision 4's current target corner within it
```

These are explicit rather than reverse-derived from `DestinationCellIndex`
on demand, matching this codebase's existing preference for explicit,
inspectable authoritative fields over ones a reader has to recompute
(`HasOutstandingRequest` is itself an explicit flag rather than an inferred
one, for exactly this reason). They exist for two reasons: an operator
inspector can show "targeting Room 4, Corner 2" without a reverse lookup
against the corner table on every paint, and a save/resume that loads
`RoomClearState` back and needs to know which corner a group's still-
outstanding path request was aimed at does not have to guess it back out of
a raw cell index.

Because `GroupPathState` has no pinned golden hash today — design section
16 already established that "no golden mission hash exists yet" is what
made its own field additions free — this addition costs nothing beyond an
update to whichever fixture eventually records the first golden seed-1
mission hash the moment one is captured, per this repository's rule that
changing a hashed field after a golden hash exists needs a new preset
version and new golden expectations. That rule binds this change exactly as
it binds every other hashed-field addition; it simply has not started
costing anything yet.

### 8.2 New events

One new `MissionEventKind` member, appended after `WeaponRaised` (the
current last member, ordinal 5):

```
RoomCleared = 6   // RoomId, tick — emitted on the false-to-true transition
```

`RoomCleared` fires once per room, on the tick `Cleared` flips from `false`
to `true`, mirroring the precedent `WeaponLowered`/`WeaponRaised` already
sets: those transitions are also readable directly from state every tick,
and still get a discrete event, specifically so a spectator watching the
event feed rather than polling state sees the cause, not only the effect.

**Deliberately not an event: sighting an individual corner.** The battle
event feed retains at most 200 ordered events for the whole mission, shared
across every event kind. A mission with several squads sweeping dozens of
rooms, each with several corners, could produce more corner-sighting
transitions over a mission than the feed retains, which would risk evicting
tactically important entries — a shot fired, an order rejected — behind a
flood of housekeeping. Corner progress does not need an event to be
spectator-visible anyway: `RoomClearStates` is authoritative, hashed
`MissionState`, readable every frame, so a HUD element can show "Room 4:
2/3 corners" or a map overlay can recolour a corner marker the instant
`CornersSightedMask` changes, with no event stream involved at all. Section
11 returns to this.

## 9. Decision 7 — interaction with hand-drawn player orders

**Decision: no new mechanism. "Its own route," in design section 16's own
phrase, is simply whatever decision 4 currently has the group targeting.
Sighting a corner is a per-operator fact, not gated on `OrderAssignment`
status, so an operator under a hand-drawn order still contributes to its
squad's clearing progress while it walks the authored polyline.**

Design section 16 already specifies the shape of this interaction and this
design changes nothing about it: an operator with a present `OrderAssignment`
follows the authored, authoritative, never-re-smoothed polyline; an operator
with none follows its squad slot along the group's current autonomous
target. What this design changes is only what populates that autonomous
target — decision 4's room-and-corner selection in place of
`InitialSquadGroups`'s single tick-zero objective — not the mechanism that
decides which of the two an operator follows, and not the four conditions
under which an assignment clears and autonomy resumes on the same tick.
"Its own route," once the autonomous target is a sweep instead of a
single fixed point, is exactly that sweep: whichever room and corner
decision 4's argmin currently names for that group.

One consequence is worth stating because it is a genuine, deliberate
interaction rather than an absence of one: sighting a corner (decision 2)
is defined as *any* living assaulting-faction operator's line of sight
reaching the representative cell, with no dependency on whether that
operator is currently under a player's hand-drawn order or following its
squad's autonomous target. A player who manually walks one operator past a
room's blind pocket gets the same corner-sighted credit the autonomous
sweep would have produced, and `RoomClearState` updates identically either
way. This is deliberate: it keeps the two path sources — authored and
autonomous — from producing inconsistent world-state bookkeeping, and it
means a player's manual routing through a room can genuinely finish that
room's checklist rather than leaving it stranded until autonomy happens to
walk back through.

The group's own sweep re-targeting evaluation (decision 4) runs
unconditionally at the group level regardless of how many of its members
currently hold an individual `OrderAssignment` — including the case where
every member is currently under one, and the group's autonomous target is
therefore not being followed by anyone at that instant. It still updates in
the background, for the same reason design section 16 already keeps squad
grouping itself running whether or not its members are under orders: an
operator whose assignment clears later should not rejoin a stale, tick-zero
target — it should rejoin a sweep that has kept pace with what the rest of
its squad, and every other squad, has since discovered.

## 10. Decision 8 — cost

**Decision: every per-tick cost this design adds is bounded to O(operators)
or O(groups), reuses existing buffer-reuse infrastructure, and stays inside
the existing "at most one A* per group per tick" amortisation rule. The one
cost this design does not bound in advance — the one-time bake in decisions
1 and 2 — is named explicitly as an unmeasured risk that needs its own
benchmark before it ships on a large map, rather than assumed free because
it happens once.**

**Per tick, per operator (bounded, O(operators)):** a corner-sighted check
tests `LineOfSight.IsVisible` from the operator's position to exactly one
cell — the representative cell of its group's *current* target corner, not
every remaining corner in the room and not every corner on the map — using
the same `Span<int>` buffer-reuse overload and scratch buffer stage 5's
sensing pass already owns, so this adds one bounded query per living
assaulting-faction operator per tick, not a new allocation source. Against
the roughly 4,684 pairwise sensing calls per tick already measured at 200
operators, 200 additional single-target queries is a small fraction, not a
new order of magnitude.

**Per tick, per group (bounded, O(groups)):** decision 4's re-targeting
argmin only runs on a tick where the group's current target resolves —
becomes `Cleared`, or its `PathReasonCode` reports `Unreachable` — which is
a rare event relative to tick count, not a per-tick recomputation. Even when
it runs, it is a linear scan over a small, bounded list (a map's room and
corner counts are tens, not thousands) to find one argmin — no new
pathfinding infrastructure. The winning destination is handed to the
existing `PathService.RequestPath`/`Advance` machinery unchanged, which
design section 7 already caps at one A* search per group per tick
regardless of how many searches would otherwise be wanted — this design
does not add a second path-request source that could compete with that cap,
it only changes what destination the existing single request names.

**Allocation discipline:** `RoomClearStates` must not be rebuilt on a tick
where no bit changes, matching the sparse-update-if-changed discipline this
codebase already applies to other per-tick collections (`OrderAssignments`,
the lowered/raised weapon transitions) — a tick where nothing new is sighted
and no room clears allocates nothing beyond what stage 5's sensing pass
already allocates for its own purposes.

**Which stages this folds into, not a new stage number:** the fourteen-stage
pipeline is fixed by design section 5, and this design recommends folding
corner-sighted evaluation into stage 5 (sensing already runs the relevant
`LineOfSight` queries and commits its results after the frozen view is
released, exactly the pattern a corner check needs) and folding sweep
re-targeting into stage 7 (`AdvancePathService` already owns "what does
`MissionState.Groups` currently want"). This is an architectural
recommendation for whichever implementation task follows this design, not
an authorization to build it — this document decides no task order.

**The one named, unmeasured risk:** decision 2's corner bake tests
`LineOfSight.IsVisible` from every open cell in a room to every one of that
room's doorway cells, once, at simulation construction. This is a one-time
cost, not a per-tick one, but `LineOfSight`'s own remarks already establish
it as the single most expensive query in this codebase before its
buffer-reuse overload existed, and a large map could still mean a large
cell-times-doorway product at load time even paid only once. This design
does not invent a number for that cost, because none has been measured. It
requires a load-time benchmark against a realistically large map before
this ships, and if that benchmark finds the naive product too slow, the
first fallback to try is bounding each cell's test to its room's *nearest*
doorway only rather than every doorway the room has — a constant-factor
reduction, itself unmeasured, not assumed here to be sufficient. Section 12
returns to this as a staging gate, not an implementation detail to gloss
over.

## 11. Spectator discoverability

`SIMULATION-GAME-STANDARDS.md` section 10's ninth requirement is direct: can
a spectator discover this effect without reading source code? For every
piece of behaviour this design adds, the answer and its concrete mechanism:

- **Which room and corner a squad is currently sweeping.** Readable live
  from `GroupPathState.TargetRoomId`/`TargetCornerId` (decision 6) in an
  operator or squad inspector panel, the same way `PathReasonCode` already
  answers "why is this operator holding" today. No polling gap: these are
  ordinary hashed `MissionState` fields, current every frame.
- **How much of a room is left to check.** Readable live from
  `RoomClearState.CornersSightedMask` against the room's known corner count
  — "Room 4: 2 of 3 corners" — again a direct state read, no event needed,
  matching decision 6's explicit reasoning for why corner sighting is not
  in the event feed.
- **A room becoming fully cleared.** The `RoomCleared` event (decision 6)
  in the battle event feed, discoverable exactly the way `WeaponLowered`,
  `ShotFired`, and every other existing event kind already is, and
  available for a map overlay to recolour a room's outline the instant it
  fires.
- **Why a squad stopped moving to fight instead of continuing its sweep.**
  Unchanged from today: `IntentSelection`'s existing `Engage` intent and
  `ContactMemory`'s tiered contact state are already spectator-visible
  through the existing inspector; this design adds nothing here and
  changes nothing here, by decision 5's design.
- **Why a squad is not moving at all.** `PathReasonCode` already carries
  `NoDestinationRequested`, `AwaitingLatency`, and `Unreachable`; this
  design does not add a new reason code, because none of its own failure
  modes need one — a squad with no reachable, not-yet-`Cleared` room simply
  falls to Phase B or, if Phase B also has nothing, to
  `NoDestinationRequested`, which already exists and already explains
  itself in the inspector.

Nothing this design adds requires a spectator to read source code to
understand: every new fact is either a direct read of hashed state already
exposed through the existing inspector pattern, or an event in the same
feed every other mission event already uses.

## 12. Staging

This design is too large for one implementation package — decision 1's bake,
decision 2's corner detection, decision 4's two-phase ordering, decision 6's
hashed-state and event additions, and the retargeting/freeze logic of
decisions 5 and 7 are each independently substantial. It should build in at
least three stages.

**Stage 0 — rooms, no corners.** Build `RoomBoundary` and `RoomId`
derivation (decision 1) in full. Skip corner/blind-pocket detection (decision
2) entirely for this stage — it is the single least-proven, most expensive
part of the design, per decision 8's named risk. Define `Cleared`
provisionally, for this stage only, by presence rather than by corners: a
room counts as cleared once a living assaulting-faction operator has entered
it and no living hostile currently occupies it. Wire decision 4's Phase A
room ordering (nearest not-yet-`Cleared` room, tie-broken by `RoomId`), the
`RoomClearState` collection and its fold into the state hasher, the
`RoomCleared` event, and the retargeting freeze from decision 5, all against
this simplified predicate. This stage alone already replaces
`InitialSquadGroups`'s one-shot single objective with a genuine, repeating,
ordered room-to-room sweep, and is independently useful and independently
verifiable — a headless run with a multi-room map and a stub hostile roster
can assert the room visitation order directly — without decision 2's
LineOfSight bake ever running.

**Stage 1 — corners.** Add the blind-pocket bake (decision 2), the full
corner-checklist predicate for `Cleared` (decision 3), and
`CornersSightedMask`. This is the stage that owes decision 8's load-time
benchmark before it ships on a large map — the benchmark and, if needed, the
nearest-doorway-only fallback are both named there and both belong to this
stage, not stage 0.

**Stage 2 — the residual and the rest.** Phase B's map-residual fallback
(decision 4), and any further tuning this design deliberately leaves open —
see section 13, particularly multi-squad room contention, which stage 0 and
stage 1 both leave unresolved and which becomes visible only once more than
one squad exists.

Recommended first stage, in three sentences: build stage 0 first, because it
proves the entire pipeline shape — derivation, hashing, ordering, the
retargeting freeze, the event — against the cheapest possible `Cleared`
predicate and the cheapest possible bake, with no dependency on the one
named unmeasured cost in this whole design. It replaces the single biggest
visible gap today, a squad that stops forever after one objective, with a
genuinely repeating sweep, and it is independently testable by a headless
run without any client involvement. Corners, which are where this design's
real cost risk and its most speculative geometric judgement calls both
live, should follow only once stage 0's ordering and hashing shape is
proven correct on a real multi-room map.

## 13. What this design does not decide

- **Contention between two squads targeting the same room.** Decision 4's
  argmin runs independently per group. Nothing reserves or claims a room
  for the squad currently sweeping toward it, so two squads with the same
  nearest not-yet-`Cleared` room will independently path toward it, which
  may look uncoordinated with more than one squad on the assaulting side.
  No claim or reservation mechanism is specified here.
- **Noise- or sound-triggered re-prioritization.** `InitialSquadGroups`'s
  own remarks describe an intended future model of "per-area clearing with
  noise attraction" from an earlier, now-deleted continuation prompt. That
  model — a squad breaking its current sweep order because it heard
  something elsewhere — is not part of this design. Hearing and alerting
  remain open, separate work.
- **The exact clustering rule for blind pockets fragmented by a staircase-
  rasterized diagonal wall.** Decision 2 states the predicate (a connected
  component of not-visible-from-any-doorway cells) precisely, but the
  practical question of whether a single true corner behind a diagonal wall
  should ever produce more than one pocket, and if not, what distance or
  cell-count threshold merges them, is left to the implementing task.
- **Whether Phase B (decision 4) ever actually triggers on a real shipped
  map.** Decision 1 already states the likely consequence — full door
  partitioning leaves no residual space — as an open, unmeasured empirical
  question about specific maps, not a settled fact.
- **Whether "looking at" a corner should require a facing check or a
  minimum dwell, not just an instantaneous `LineOfSight.IsVisible` hit.** A
  real breacher turns to look; this design's corner-sighted predicate only
  requires that some straight, unobstructed line existed on some tick. A
  richer, facing- or `VisionCone`-scoped definition of "actually looking" is
  not decided here.
- **Room naming for spectator or event-feed legibility.** The `.hkmap`
  format has no room-naming record, and this design does not add one;
  `RoomId` — an integer, the lowest cell index of the room's component — is
  what ships. Whether a future map-format addition gives rooms
  player-facing names is not decided here.
- **Measured, numeric per-tick or per-load budgets.** Decision 8 states the
  *shape* of every new cost and names one specific, unmeasured risk (the
  corner bake) that needs a real benchmark against a real map before it
  ships. No specific number is invented here; SIMULATION-GAME-STANDARDS.md's
  own instruction to adopt budgets only after measuring, not before, is
  followed rather than worked around.
- **Manual, player-driven room-priority overrides.** Sandata's binding model
  is autonomous behaviour plus hand-drawn orders (design section 16), not a
  room-priority UI a player sets directly. This design does not add one.





