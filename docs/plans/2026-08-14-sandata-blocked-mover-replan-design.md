# Sandata: a blocked mover re-requests its path — design

Status: draft, awaiting review. Authorizes nothing; see this file's own final
section for what it deliberately leaves undecided. This is a design document,
not a plan document — no task list appears below, per this repository's
workflow.

## Contents

1. The measured problem, restated
2. Scope of this decision, and what it is not
3. Vocabulary and the mechanisms this design reasons from
4. Decision 1 — what counts as stalled
5. Decision 2 — what stops re-request thrashing
6. Decision 3 — how the fixed-latency rule applies, and what the mover does while it waits
7. Decision 4 — the goal is unchanged; only the start is recomputed
8. Decision 5 — the blocker never enters the search, and what that costs
9. Decision 6 — what is hashed, and whether an event fires
10. Decision 7 — does this move the seed-1 baseline
11. Spectator discoverability
12. What this design does not decide
13. Test impact

---

## 1. The measured problem, restated

Task 89 of Sandata's archived scaffold plan measured a specific composition
failure and it is not re-litigated here. `LocalAvoidance.Commit`'s own remarks
carry the finding in full, and the short version is this: stage 10 gives a
blocked unit exactly one retry, a single 22.5-degree sidestep rotated from its
own already-proposed displacement, chosen by `entityId` parity. If a body
blocks the direct route and that same body also blocks the sidestepped route,
the unit's committed position for the tick is its own start position — held,
not moved. Design section 8 states the rule in full, verbatim: "if that is
also blocked, it waits a tick." It says nothing about a blocker that never
moves.

When the blocker is a body standing still, "waits a tick" is not a wait. The
mover's start position, the group's published polyline, and therefore stage
9's next proposal are all bit-for-bit identical on the following tick, so both
candidates are rejected again, and again, for as long as the run continues.
Nothing in the composition — not stage 9's arclength arithmetic, not the
squad-grouping derivation, not the sidestep's own rotation geometry — has any
input left that could change. `LocalAvoidance.Commit`'s remarks name the
geometric reason a head-on encounter in particular can never clear by
sidestep alone: a 22.5-degree turn cannot open daylight around a body whose
radius (4,352 raw) is larger than the step that turns (1,638 raw at the
shipped tick rate).

`LocalAvoidanceTests.CommitAgainstAStaticBody_StallsForeverBecauseTheOneSidestepIsBlockedToo`
pins this at the stage-10 level, deliberately, as a known gap rather than a
desired outcome — its own remarks say so, and say that if a future change lets
a blocked mover route around a static body, the right response is to delete
the test, not widen it. Section 13 below names it again as the one artifact
this design's implementation must revisit.

## 2. Scope of this decision, and what it is not

Two alternative remedies were on the table and both were rejected on
2026-08-14, before this document was commissioned:

- **Entering operators into the nav search's `blocked` span**, so `NavSearch`
  routes around a body the same way it already routes around a wall. This is
  the remedy that would most directly clear a permanent, exactly-on-corridor
  blocker, and it is exactly the remedy the user declined.
- **Leaving the stall as measured**, accepting that a mover pinned against a
  stationary body simply never resumes.

The decision taken instead, and the only question this document answers, is:
**a stalled mover re-requests its path.** This document's job is to design how
that re-request is triggered, throttled, and executed — not to reopen whether
re-requesting is the right remedy, and not to design the blocked-span
alternative even as a comparison. Where the reasoning below shows that a bare
re-request cannot, by itself, clear the literal head-on case the pinned test
reproduces, that finding is reported honestly and is not treated as grounds to
reconsider the decision; section 8 below explains exactly why that gap exists
and exactly what the re-request mechanism still buys in spite of it.

## 3. Vocabulary and the mechanisms this design reasons from

Four existing pieces compose to produce the decisions below, and this design
introduces no new one:

- **`PathService.RequestPath(groupId, startCellIndex, goalCellIndex,
  requestTick)`** is a no-op if the group already has an outstanding request,
  and otherwise records the request and clears the group's pending search
  state. It never inspects or clears the group's *already published*
  `CurrentPath`/`CurrentCorridor` — those are only ever overwritten by
  `Publish`, which `Advance` calls once `currentTick` reaches
  `requestTick + PathLatencyTicks`.
- **`SandataCollisionResolver.Resolve`**, called twice by
  `LocalAvoidance.Commit` per tick (first pass, then a second pass with a
  sidestepped substitute for anything the first pass blocked), returns one
  `SandataMovementResolution` per entity: `Moved` or `Held` for a proposal
  whose committed position matches what it asked for (moving or not,
  respectively), `Separated` for the resolver's own exact-coincidence repair,
  and `Blocked` for a proposal whose committed position was forced back to its
  own start position. Because `LocalAvoidance.Commit` already substitutes the
  sidestepped candidate for anything the first pass blocked before running the
  second pass, **a second-pass `Blocked` result means both the direct step and
  the sidestep were refused** — exactly, and only, the condition task 89
  measured. A proposal whose desired position already equalled its start
  position (an operator standing still on purpose) is never routed into that
  substitution at all and returns `Held`, never `Blocked`. This fact is the
  hinge of decision 1 below: the signal this design needs already exists,
  computed at zero extra cost, once per entity per tick.
- **`GroupPathState`** (`MissionState.cs`) is the authoritative, hashed,
  snapshotted record of one group's destination and outstanding request:
  `GroupId`, `DestinationCellIndex`, `HasOutstandingRequest`,
  `StartCellIndex`, `GoalCellIndex`, `RequestTick`. `SandataStateHasher` folds
  every one of those six fields, in that declared order, for every entry in
  `MissionState.Groups`, and `RestorePublishedPath` rebuilds a group's
  polyline from exactly this record on resume — it is, by design section 4,
  the only part of a group's path that is authoritative at all.
- **`AdvancePathService`** is stage 7's call-site obligation inside
  `SandataSimulation.RunTick`: for every `MissionState.Groups` entry whose
  `HasOutstandingRequest` is `true`, it submits the request to `PathService`,
  advances the service by one tick, and then rewrites `State.Groups` to clear
  `HasOutstandingRequest` for any group whose request just published. Its own
  remarks are explicit that "no autonomous destination-request source exists
  in this worktree" — nothing today ever sets `HasOutstandingRequest` to
  `true` on its own initiative. This design is the first thing in
  `Sandata.Core` that does.

## 4. Decision 1 — what counts as stalled

**Decision: a group's leader is stalled once its stage-10 committed
resolution is `Blocked` for `StallReRequestThresholdTicks` consecutive ticks.
No displacement threshold and no distance math are used, because the
resolver's own second-pass `Blocked` result is already, by construction,
exactly zero displacement — there is nothing left to threshold.**

The three candidate signals the brief named are a refusal count, consecutive
refused ticks, and displacement below a threshold over a window. The third
collapses into the first once the actual mechanics of
`SandataCollisionResolver` are read: a `Blocked` result's `CommittedXRaw`/
`CommittedYRaw` are literally `request.StartXRaw`/`request.StartYRaw` — the
resolver's own "blocked holds still" rule, which `LocalAvoidance.Commit`'s
remarks already lean on to describe "waits a tick" as a fact about the
composition rather than a state any file tracks. A window-based displacement
check would need to store a rolling position history (more authoritative
state, more hash surface) and compare a distance against a threshold (an
epsilon this codebase's own banned-token scan forbids introducing casually),
to recover an answer that a single already-computed enum value gives for
free. Refusal counting is strictly less machinery for the identical answer.

**What "stalled" is scoped to.** Design section 8's grouping model gives a
group exactly one shared path, published for its leader and consumed by every
follower through an arclength offset along the same polyline — "followers are
literally standing on the leader's past path." `PathService.RequestPath`
takes a `groupId`, not an operator id, so the only re-plannable unit is the
group's route as a whole, anchored at its leader. This design therefore tracks
and reacts to the **leader's** stall only — the entity whose id equals its
own group's `GroupId`, which design section 8 defines as "the minimum entity
id in the component" and which is simultaneously the leader ("the lowest
living entity id in the component") for as long as that entity is alive and
proposing movement. A follower that is individually blocked by some third body
while its own leader is making progress is a different, already-handled case:
the leader's continued progress keeps moving the follower's arclength target,
so the follower's own stage-9 proposal changes tick to tick even though the
blocker beside it does not, and the existing one-sidestep-then-wait behaviour
is not permanently stuck the way task 89's fixture is. Section 12 names the
one case this scoping does not cover.

**The counter is a new field, `GroupPathState.ConsecutiveStalledTicks` (int),
appended after `RequestTick`.** It increments by one on any tick where the
leader's second-pass resolution is `Blocked` **and** the group currently has
no outstanding request (`HasOutstandingRequest == false` — see decision 3 for
why the counter is frozen, not merely irrelevant, while a request is in
flight). It resets to zero on any tick where the leader's resolution is
`Moved`, `Held`, or `Separated`, and on any tick where the group has no
`GroupPathState` entry at all (nothing to react to). This satisfies the
brief's requirement directly: it is an `int`, it changes only in response to
already-computed integer facts, it reads no wall clock, and it costs one
comparison and one increment-or-reset per group's leader per tick — a cost
already bounded by the same "one indoor operator squad, never a battlefield
roster" scale `LocalAvoidance`'s own remarks invoke for its linear scans.

**Where the counter is written.** Stage 10, not stage 7. `AdvancePathService`
(stage 7) runs before stage 9 and stage 10 in the same tick, so at the moment
it runs it can only ever see the *previous* tick's committed outcome — exactly
the same rhythm `HasOutstandingRequest` and the published polyline already
follow across the stage 7/stage 10 boundary from one tick to the next.
`ResolveLocalAvoidanceAndCollision` (stage 10) already holds, in the same
call, both `PendingMovementProposals` (which carries each proposal's `GroupId`
directly) and `LocalAvoidance.Commit`'s per-entity resolution list, so it is
the one place the leader-identification test (`EntityId == GroupId`) and the
resolution-classification test (`Blocked` vs. everything else) can both be
read from data already in hand, without adding a lookup, a second pass over
the roster, or a new parameter threaded through `RunTick`. This is a small
widening of stage 10's existing responsibility — it already writes committed
operator positions into `State`; this design has it also write the matching
`GroupPathState` entry's counters — and it does not require any change to the
fourteen-stage table's declared order or its "reads" column: stage 10 already
reads only `PendingMovementProposals` and `State`, never the tick-start view,
and the two new fields it writes are ordinary committed state, read back by
stage 7 of the *next* tick exactly the way `HasOutstandingRequest` already is.

**The threshold itself is a compile-time constant, not a `SandataRuleset`
field — see decision 7 for why, and for what that choice buys.** A concrete
illustrative default: `StallReRequestThresholdTicks = 25`, half a second at
the shipped 50 Hz tick rate. That value is provisional and marked as such in
code, in the same spirit as the hearing radii and the tall-hardwood shield
multiplier — long enough that an incidental, one-or-two-tick refusal between
two moving bodies renegotiating a doorway never trips it, short enough that a
genuine permanent obstruction is noticed and responded to well inside a
player's own attention span.

## 5. Decision 2 — what stops re-request thrashing

**Decision: a second new field, `GroupPathState.ReRequestAttempts` (int),
appended after `ConsecutiveStalledTicks`, caps how many times this mechanism
will fire for one unresolved episode. A concrete illustrative default,
provisional in the same sense as the threshold above:
`MaxStallReRequestAttempts = 3`. Once the cap is reached, the mechanism stops
intervening and the group's behaviour degrades back to exactly what task 89
measured — one sidestep, then wait a tick, forever — rather than inventing a
new terminal state.**

The threshold in decision 1 already does most of the throttling work by
itself: because the counter only advances on ticks where the group has *no*
outstanding request, and a fresh request immediately occupies that slot for
at least `PathLatencyTicks` ticks (decision 3), the fastest this mechanism can
possibly re-fire is once every `StallReRequestThresholdTicks +
PathLatencyTicks` ticks. That already rules out the literal worst case the
brief warns about — a group re-requesting every tick while still blocked —
without any additional field.

What the threshold alone does not rule out is an **unbounded number of cycles**
against a body that truly never moves. Decision 5 below shows that, because
the blocker never enters the search, a re-request issued from an unchanged
start cell against an unchanged goal cell is very likely to reproduce the
identical route it just failed on. Left unchecked, a permanently parked
blocker would make the group re-request forever, at the throttled cadence
above rather than every tick, but forever nonetheless — a real, if slower,
version of the same thrashing concern, and a standing cost against
`PathService`'s "at most one A\* per group per tick" budget for a search whose
outcome is already knowable in advance.

`ReRequestAttempts` bounds that. It increments by one every time a re-request
is actually issued (not every time the threshold is merely reached — those
are the same event under decision 1's rules, since crossing the threshold
*is* what issues the request), and it resets to zero the moment the group's
leader is observed to make real progress: a `Moved` resolution after the
group's most recent request has published. A `Held` or `Separated` resolution
also resets it, on the same reasoning as decision 1's counter reset — the
group is not currently obstructed, so there is nothing left for the cap to
guard against. Crucially, **it does not reset merely because the group is
waiting out a latency window** (a `Held` resolution produced by an
intentionally emptied path per decision 3 would otherwise reset the cap every
single episode, defeating it entirely) — see decision 3 for the precise rule
that keeps a holding group from being misread as a recovered one.

**Why a hard cap rather than exponential backoff.** A growing delay between
attempts is the more familiar answer to thrashing, but it needs either a
second counter recording how long to wait this time (more hashed state for
the same outcome) or a formula applied to `ConsecutiveStalledTicks` itself
(which would make the threshold's own meaning depend on history, complicating
decision 1's clean "N consecutive ticks" definition for no benefit this
scenario needs). A hard cap is simpler, is exactly as deterministic, and —
because reaching it does not invent a new stuck state but simply stops adding
new intervention on top of the one that already existed before this design —
it cannot make a spectator's experience of a permanently blocked group worse
than task 89's original, already-shipped-and-tested behaviour. The two
outcomes a spectator can ever see are "the group re-routed and kept walking"
and "the group is standing exactly where it always would have stood without
this feature at all."

**Where the cap is enforced.** Stage 7, alongside where the threshold check
now lives (decision 1's "where the counter is written" section explains why
the increment lives in stage 10; the *decision* to issue a re-request, by
contrast, belongs in stage 7 — the only stage that calls
`PathService.RequestPath` at all). `AdvancePathService`'s existing per-group
loop is extended: before its current "if `HasOutstandingRequest`, submit"
step, it now also asks, for every group with no outstanding request, whether
`ConsecutiveStalledTicks >= StallReRequestThresholdTicks` and
`ReRequestAttempts < MaxStallReRequestAttempts`. When both hold, it performs
the re-request sequence decision 3 and decision 4 describe, which — among
other things — sets `HasOutstandingRequest = true`, so the very same tick's
existing "if `HasOutstandingRequest`, submit" loop immediately hands the new
request to `PathService`, with no extra tick of delay beyond
`PathLatencyTicks` itself.

## 6. Decision 3 — how the fixed-latency rule applies, and what the mover does while it waits

**Decision: a stall-triggered re-request also clears the group's already
published path (`PathService` gains one new method for this, tentatively
`ClearPublishedPath(groupId)`), so that for the whole of the
`PathLatencyTicks` window the group is in exactly the same observable state a
group that has never published any path is in — `PathReasonCode.AwaitingLatency`,
an empty `GetCurrentPath`, and stage 9 proposing no movement for the whole
group. The mover holds; it does not keep refusing.**

Design section 7 states the general rule this design must not violate:
"Until a path is valid, the group's units hold their current intent. There is
no 'no path yet, move directly at the goal' fallback." Read literally, that
rule is written for a group that has never published anything. This design's
re-request is a different case: the group already has a stale, obstructed
polyline sitting in `PathService`'s per-group state at the moment the
threshold trips, and `RequestPath` by itself does not touch it —
`GroupState.CurrentPath`/`CurrentCorridor` are only ever overwritten by
`Publish`. Left alone, `PathService.GetReasonCode` would keep reporting
`PathReasonCode.PathValid` throughout the whole latency window, because its
first check is "is `CurrentCorridor` non-empty", and the stale corridor still
is. Stage 9 would then keep proposing the identical, already-known-obstructed
step every tick, and stage 10 would keep refusing it every tick — silently
correct (nothing desyncs), but observably indistinguishable from the stall
this whole design exists to fix, for the entire duration of the wait.

Explicitly clearing the stale path at the moment of re-request avoids that.
It costs one new, narrowly scoped method on `PathService` — clear
`CurrentCorridor` and `CurrentPath` to empty, touching nothing else on the
group's state — called from `AdvancePathService` only on the re-request path
this design adds, never on an ordinary first-ever request (which has nothing
to clear). Once cleared, `GetReasonCode`'s existing, already-tested logic
does the rest with no special-casing: an empty corridor with an outstanding
request reads as `AwaitingLatency`, exactly the reason code a first-time
request already produces, so this design adds no new `PathReasonCode` member
and no new branch to that method.

**Why this reading is preferred over leaving the stale path active during the
wait.** The alternative — do nothing to `CurrentPath` at request time — was
seriously considered, because it needs no new `PathService` method at all.
It was rejected because it produces a visibly worse spectator experience for
no compensating benefit: the group keeps twitching against the same obstacle
for the entire latency window, then silently swaps to whatever the new search
found only at the moment it publishes. `CLAUDE.md` section 1 and
`SIMULATION-GAME-STANDARDS.md` section 10 both ask whether an effect is
discoverable without reading source; "the squad stops trying while it
reconsiders, then either resumes walking or is seen to try again" reads as
legible cause and effect on screen, and "the squad keeps twitching for no
visible reason, then abruptly changes direction" does not.

**Interaction with decision 1's counter freeze and decision 2's cap reset.**
While `HasOutstandingRequest` is `true`, the leader's stage-9 proposal is now
the empty-path hold, so its stage-10 resolution is `Held`, not `Blocked`
(desired position equals start position, the same case decision 1 already
excludes from ever entering the sidestep substitution at all). Decision 1
freezes `ConsecutiveStalledTicks` rather than reading this `Held` as recovery,
and decision 2 likewise does not reset `ReRequestAttempts` on it — both
counters are explicitly gated on `HasOutstandingRequest == false` before they
react to anything, precisely so that the act of waiting is never
misinterpreted as the act of having recovered. Genuine recovery is only ever
read from a `Moved` resolution observed *after* `HasOutstandingRequest` has
gone back to `false` — that is, after the new path has actually published and
the leader has taken at least one real step along it.

## 7. Decision 4 — the goal is unchanged; only the start is recomputed

**Decision: a re-request keeps `GoalCellIndex` and `DestinationCellIndex`
exactly as they were. Only `StartCellIndex` is recomputed, from the leader
operator's current position at the tick the re-request fires, and
`RequestTick` is set to that same tick.**

Nothing about task 89's finding says the group's destination was ever wrong —
the failure is that the route to it, as originally searched, ran into a body
that turned out not to move. Design section 7's own description of
`PathRequest` already treats the goal as the durable, player- or
mission-level fact and the start as a launch condition specific to one search
attempt; re-deriving only the start and reusing the stored goal is the
natural reading of "re-request the path" rather than "re-target the group."
Changing the goal would also require deciding what a "smarter" goal even
means with no blocked-span information available to the search at all
(decision 5) — that is squarely the alternative the user declined, entering
the blocker into the search, and this design does not smuggle a weaker
version of it in through the goal instead of the start.

The start cell is read from the same place decision 1's stage-10 write and
decision 3's `AdvancePathService` extension already read the leader's
position from: `State.Operators`, matched by `EntityId == group.GroupId`,
converted to a nav cell index the same way the group's original request was —
`NavGrid`'s existing world-to-cell mapping, not a new coordinate convention.
No FixedPoint-to-raw-to-cell conversion in this design differs from what
`RequestPath`'s existing callers already do.

**The consequence this decision has to state plainly, not bury:** in the
literal fixture `LocalAvoidanceTests` pins — a mover 24 raw from a blocker of
diameter twice the step size, head-on, with no lateral give at all — the
leader's position at the moment `ConsecutiveStalledTicks` reaches the
threshold is, by definition, identical to its position when the original
request was issued. It never moved a single raw unit; that is what "stalled"
means. So the recomputed `StartCellIndex` in that exact scenario is the same
cell the group already searched from. Decision 5 carries this thread the rest
of the way.

## 8. Decision 5 — the blocker never enters the search, and what that costs

**Decision: a re-request runs `NavSearch.TryFindPath` against exactly the
same `blocked` span every other search already uses —
`SandataSimulation._pathBlockedCells`, seeded once at construction from the
nav grid's baked static passability. No operator, live or dead, moving or
stationary, is ever written into that span, or into any second span unioned
with it, as part of this design. This restates the user's 2026-08-14
rejection of the blocked-span alternative in the language this design
actually touches, so the choice is unambiguous at the implementation site and
not just at the level of the overall decision.**

This is stated as its own decision, separate from decision 4, because it has
a consequence decision 4 could not fully state on its own: **a re-request is
a pure function of the nav grid, the (possibly unchanged) start cell, and the
(always unchanged) goal cell — the same purity `PathService`'s own remarks
already rely on to justify never hashing or snapshotting a published
polyline.** Re-running that pure function with the same two cell indices
against the same static grid reproduces the identical corridor and the
identical line-of-sight-smoothed polyline, because nothing about the
function's three inputs changed. In the exact scenario decision 4 flagged —
the mover that never moved a single raw unit before the threshold tripped —
the re-request is therefore very likely to hand the group back the exact
route it just spent `StallReRequestThresholdTicks` ticks failing to walk,
head-on into the same still-parked body.

**What this design still buys, honestly stated, in spite of that.** It is not
"the group now gets past a permanent, exactly-on-the-corridor blocker" —
that claim would be false, and the reasoning above is exactly why. What it
is: a silent, invisible, forever-repeating twitch (task 89's measured
behaviour, one refused proposal every tick with nothing in the event feed and
nothing on screen distinguishing tick 40 from tick 40,000) becomes a bounded,
periodic, and — per decision 3 — *visibly different* cycle: try, fail
`StallReRequestThresholdTicks` times, visibly stop and hold for
`PathLatencyTicks`, then either resume walking or visibly try again, up to
`MaxStallReRequestAttempts` times before falling back to the original
shipped behaviour for good. Two cases this design is squarely aimed at both
benefit for real: a blocker that is stationary only *for now* — an allied
operator pausing to reload, a squad member that has not yet been assigned a
group and has not started its own path yet, anything that eventually moves —
lets a later re-request search from a start cell that may now differ (if the
mover's earlier sidestep attempts, even the refused ones, ever did register
partial progress before fully wedging) or find that the previously blocking
body has simply vacated the corridor by the time the new search runs, either
of which is a genuine, different, successful route. The pinned test's fixture
is deliberately the *worst* case for this mechanism — zero lateral give at
all — and this design does not claim to solve the worst case; it claims to
convert an unbounded, silent failure into a bounded, legible one, which is
what "re-request the path" can honestly deliver without also being told the
blocker's position.

## 9. Decision 6 — what is hashed, and whether an event fires

**Decision: two new `int` fields on `GroupPathState`,
`ConsecutiveStalledTicks` and `ReRequestAttempts`, both default `0`, appended
after `RequestTick` in the record's parameter list and folded into
`SandataStateHasher`'s per-group loop in that same order, immediately after
the existing `RequestTick` fold. `StallReRequestThresholdTicks` and
`MaxStallReRequestAttempts` are `internal const int` values declared beside
the mechanism that reads them — not `SandataRuleset` fields, and therefore
not folded into `SandataRuleset.ContentHash` at all. Exactly one new
`MissionEventKind`, emitted once per re-request actually issued — not once
per tick a counter merely changes.**

**Why the two counters are hashed.** Both are authoritative simulation state
by the same test design section 4 already applies to every other
`GroupPathState` field: a resumed mission must reproduce identical future
behaviour, and whether a group is 24 ticks into its stall window or 26 is
exactly the kind of fact that changes what the very next tick does. Leaving
either counter out of the hash and the snapshot would let a saved-and-resumed
run diverge from an uninterrupted one the same way task 90's finding
(recorded in `RecomputePublishedPaths`'s own remarks) already showed for the
published polyline before that gap was closed — except here the gap would be
in an *input*, not a derived output, so it could not be repaired the way a
derived polyline is repaired on resume. Both fields must be real, snapshotted
state.

**Why the two thresholds are constants, not ruleset fields.** `PathLatencyTicks`,
`GroupCohesionRadiusWu`, `LoweredWallDistanceWu`, and `AimToleranceBam` are
`SandataRuleset` fields because design section 4 and the ruleset's own remarks
treat them as mission-tunable design knobs — the kind of number a future
difficulty setting or map-specific preset might reasonably vary. This
mechanism's two thresholds are not that: they are a retry *policy*, the same
category `SidestepRules.TurnMagnitudeBam` already occupies as a fixed,
code-level constant rather than a ruleset input, chosen for exactly the same
reason — there is no design requirement anywhere that a mission be able to
retune how patient the path service is before it tries again, only a
requirement that the number be pinned, deterministic, and documented as
provisional. Declaring them as `const` rather than as ruleset fields is also
what keeps decision 7's answer clean; see that section.

**Whether an event fires.** Yes, once per re-request actually issued, and not
on any other transition this design adds. `SubmitOrder`'s existing
`EmitOrderRejectedEvent` call site is the direct precedent: design section 16
requires a rejected order to "emit an authoritative event carrying the order
id and a reason code" rather than being "silently dropped," using the same
"assign then advance `NextEventSequence`" shape every other authoritative
counter in `SandataSimulation` already follows. This design adds one
comparable event, tentatively `MissionEventKind.GroupPathReRequested`,
carrying the group id and the tick, emitted from the same
`AdvancePathService` extension that decides to re-request at all — the
submission action, exactly where `OrderRejected` fires, not deferred to a
later stage.

**Why not an event on every counter change.** `CLAUDE.md` section 5 caps the
battle event feed at 200 ordered events (stated there for Hukbo, and the
`MissionState.EventFeed` shape `EmitOrderRejectedEvent` appends to is the same
kind of bounded, ordered collection). `ConsecutiveStalledTicks` changes on
every tick a leader is blocked, which — for exactly the permanently-parked
case decision 5 describes as this mechanism's honest limit — could otherwise
mean one event every tick for the life of the run. That would drown genuinely
rare, meaningful events (an order rejection, a death, a breach) under noise
carrying no information beyond "still counting," which the state hash already
carries losslessly. An event fires only at the one moment a real decision was
taken: the re-request itself. This is also the answer to the brief's own
warning to "remember that a bug moving state without an event moves one hash
and not the other" — the two counters move the state hash on every stalled
tick by design, silently, exactly the way position already does; only the
discrete act of re-requesting is meant to also move the event hash, and a
future implementer must keep those two facts distinct rather than trying to
make the event hash track the counters tick for tick.

**A compatibility note, not a decision this document is making on its own
authority.** Every existing test construction of `GroupPathState` in this
repository uses named arguments (`new GroupPathState(GroupId: ..., ...)`), so
appending two new positional parameters with a default of `0` each keeps
every one of those call sites compiling unchanged. Whether the two new
parameters actually carry a `= 0` default, or whether every call site is
instead updated explicitly, is left to implementation; either is consistent
with everything decided above.

## 10. Decision 7 — does this move the seed-1 baseline

**Decision, stated precisely rather than as a bare yes or no: no. The seed-1
workload's recorded state hash, event hash, winner, and event stream are
byte-for-byte unchanged by this design, because the seed-1 workload never
exercises any of the code this design adds. This is a claim about that one
workload's specific fixture, not a general claim that adding fields to
`GroupPathState` is always free.**

`Sandata.Headless.HeadlessRunner.BuildInitialState` is the fixture behind the
recorded seed-1 baseline in `docs/development/testing.md` and the two golden
fixtures in `tests/Sandata.Core.Tests/Fixtures/seed-1-baseline.json`, and it
sets `Groups = ImmutableArray<GroupPathState>.Empty` explicitly. `BuildOpenGrid`,
the companion fixture that supplies the nav grid for the same workload,
fills every cell as `NavCellFlags.Open` and builds `WallBuckets` from four
empty segment lists — there is no static obstruction in that workload either,
which would have been the other way this design's machinery could fire.
With `State.Groups` empty for the whole run:

- `AdvancePathService`'s existing per-group loop, now extended by decisions 1
  through 4, iterates zero entries every tick. The new re-request check this
  design adds to it never evaluates a single condition, because there is
  nothing to iterate.
- Stage 10's new leader-identification scan (decision 1's "where the counter
  is written") matches proposals against `State.Groups` by `GroupId`; with
  that array empty, no proposal is ever a leader's proposal under this
  design's definition, so no `ConsecutiveStalledTicks` or `ReRequestAttempts`
  field is ever written, because none exists to write to.
- `SandataStateHasher`'s per-group fold loop — the one place the two new
  fields would ever reach the state hash — folds `groups.Length` (zero,
  unchanged) and then iterates zero entries. The bytes it emits for that
  section of the hash are identical to what it emitted before this design,
  not merely equal in effect.
- `SandataRuleset.ContentHash` is unaffected for a stronger reason than the
  workload's fixture: decision 6 puts both new thresholds on `const` fields,
  and `SandataRuleset.ComputeContentHash` only ever folds its own declared
  properties (`PathLatencyTicks`, `GroupCohesionRadiusWu`,
  `LoweredWallDistanceWu`, `AimToleranceBam`, and the tick-rate and
  conversion-rule fields it already carries). A `const` outside that type
  cannot appear in that fold no matter what value it holds. Had this design
  instead put the thresholds on `SandataRuleset`, `ContentHash` — which
  `SandataStateHasher` folds unconditionally, last, into every single state
  hash regardless of what `State.Groups` contains — would have changed for
  every recorded baseline this repository has, including seed-1's, forcing a
  recapture of `SandataRulesetTests`' pinned `ContentHash` literal, the two
  `Fixtures/seed-1-baseline.json` values, the `docs/development/testing.md`
  row, and `MissionStateTests.PreTask79cBaselineHash`, none of which this
  design's actual gameplay change would have justified. Keeping the
  thresholds off `SandataRuleset` is what makes decision 7's answer a clean
  "no" instead of a mechanical, behaviour-free "yes."

**What this does not settle.** Any mission fixture that *does* populate
`State.Groups` — which, by `AdvancePathService`'s own remarks, is every
fixture except the seed-1 headless workload today, since "no autonomous
destination-request source exists" means only a test or a future order layer
ever sets `HasOutstandingRequest` — will see its state hash move the moment a
tracked group's leader is ever `Blocked` for even one tick, because
`GroupPathState`'s field list itself changed shape. That is expected and is
the same category of change `SandataStateHasher`'s remarks already describe
for every other field addition to a hashed record: it is a determinism
change under this repository's own contract, requiring new golden
expectations for whichever fixtures exercise it, and it is not a change to
`SandataPresetId` or to `SandataRuleset.ContentHash` on its own.

## 11. Spectator discoverability

`SIMULATION-GAME-STANDARDS.md` section 10 asks whether a spectator can
discover an effect without reading source code. This design gives two
independent, source-free signals, and both already reuse an existing,
already-tested surface rather than inventing a new one:

- **The squad's own visible motion changes shape.** Before this design: a
  squad approaches an obstruction and then twitches in place indefinitely,
  with no visible distinction between the tick it first got stuck and the
  ten-thousandth tick after. After this design: the squad tries, then
  visibly halts (decision 3's cleared path makes the whole group stop, not
  just refuse), then either resumes walking on a new heading or is seen to
  try again. A stop-then-resume-or-retry rhythm is a legible, on-screen fact
  a spectator reads the same way section 5's sensing-versus-resolution seam
  is legible — no source-reading required, only watching.
- **`PathService.GetReasonCode`, already exposed through
  `SandataSimulation.GetPublishedPathReasonCode` for exactly this purpose,
  now genuinely reports `AwaitingLatency` during a stall-triggered
  re-request**, where before this design a group with a stale published path
  would have reported `PathValid` throughout any hypothetical re-request
  window (there being no such window before this design existed). Any
  inspector or HUD surface that already renders this reason code — design
  section 11 lists the agent inspector as the intended consumer of exactly
  this kind of reason code — needs no new code of its own to make the stall
  and the recovery attempt visible; it already renders whatever
  `GetReasonCode` returns.
- **The new `MissionEventKind` from decision 6** puts a discrete, timestamped,
  ordered record of "this group's path was re-requested because it was
  stuck" into the same event feed the client can already read for
  `OrderRejected` and every other mission event, giving a textual or
  log-style surface the same discoverability `OrderRejected` already has,
  independent of whatever the render layer does with the reason code above.

This design does not itself add or change any `Sandata.Client` rendering
code — that remains implementation's job, out of this document's no-code
scope — but it establishes that the underlying authoritative signals a
client would need already exist, or are added here, so that job requires no
new query surface of its own.

## 12. What this design does not decide

- **The literal numeric values of `StallReRequestThresholdTicks` and
  `MaxStallReRequestAttempts`.** Decisions 1 and 2 commit to the mechanism —
  an integer tick count and an integer attempt cap, both compile-time
  constants — and offer illustrative, explicitly provisional defaults (25
  ticks, 3 attempts). Tuning either number against real playtesting is
  implementation's job, the same way the hearing radii and the tall-hardwood
  shield multiplier were left as marked-provisional numbers for a later
  tuning pass rather than pinned here.
- **A follower stalled independently of its leader.** Decision 1 scopes this
  design to leader stalls, because `PathService` only ever plans one route
  per group. A follower wedged against a third body while its own leader
  walks freely is not covered by any mechanism this document adds — it
  remains exactly the "one sidestep, then wait a tick" behaviour design
  section 8 already specifies, on the reasoning that the leader's continued
  motion keeps changing that follower's own arclength target tick to tick,
  which is a materially different situation from the permanently frozen
  inputs task 89 measured. Whether that residual case ever needs its own
  remedy is not decided here.
- **Whether `SandataPresetId` needs a new value.** `GroupPathState` gaining
  two new fields is a schema change to authoritative, hashed state, in the
  same broad category `CLAUDE.md`'s Sandata rules name — "changing enum
  numeric values, enum order, roster order, weights, or a hash mixer requires
  a new preset version" — without being a textbook instance of any one item
  on that list. This document does not rule on whether `ModernTacticalV1`
  stays as-is or a `ModernTacticalV2` is warranted; that is a judgement call
  for whoever implements this change, made with the full weight of
  `CLAUDE.md` section 4's determinism contract in front of them, not a
  design-time guess made here without an implementation to point at.
- **Any `Sandata.Client` rendering, HUD, or inspector change.** Section 11
  names the authoritative signals a client could read; it does not design,
  and this document does not authorize, any code that reads them.
- **Whether the blocked-span or leave-it-stalled alternatives remain rejected
  for future work outside this problem.** This document only carries forward
  the 2026-08-14 decision that re-requesting is this problem's remedy; it
  does not re-argue that decision, and it does not speak to whether some
  unrelated future problem might still want the blocked-span mechanism for
  its own reasons.

## 13. Test impact

`LocalAvoidanceTests.CommitAgainstAStaticBody_StallsForeverBecauseTheOneSidestepIsBlockedToo`
is the test that pins the stall as expected, deliberate behaviour, and its
own remarks already say what to do about it: "If a future change lets a
blocked mover route around a static body, this test fails, and the right
response then is to delete it rather than to widen it." This design's
mechanism, however, operates one layer above where that test exercises the
system — it calls `LocalAvoidance.Commit` directly, in isolation, with no
`PathService`, no `AdvancePathService`, and no `GroupPathState` in the loop
at all, so `LocalAvoidance.Commit`'s own behaviour (one sidestep, then wait a
tick, forever, for as long as the caller keeps asking) does not change under
this design and this exact test does not need to fail. What changes is one
level up: a caller running the full `SandataSimulation.RunTick` pipeline no
longer keeps asking `LocalAvoidance` the same unanswerable question forever
without ever trying anything else. Whoever implements this design should
re-read that test's own fixture once the mechanism exists, specifically to
confirm this reasoning holds rather than assuming it: the fixture calls
`LocalAvoidance.Commit` directly forty ticks in a row with no group-level
machinery present at all, so it should keep passing unmodified — and if it
does not, that is new information this document did not have.

`TickPipelineTests.RunTick_HostileBodyOnThePublishedPath_HaltsTheLeaderToEngageBeforeItWalksIntoTheBody`
is the full-pipeline descendant of the original stall fixture, already
rewritten on 2026-08-11 once stage 9's halt-to-engage rule made the original
opposing-faction blocker unreachable as a stage-10 stall at all — its own
remarks record that history. It exercises a hostile blocker, which stage 9
now stops in front of before the operator ever proposes a step into it, so
this design's leader-stall detection has no `Blocked` resolution to observe
in that fixture either, and this test is not expected to need any change.
No same-faction, non-combat, full-pipeline fixture reproducing task 89's
original finding currently exists in `TickPipelineTests`; building one (an
allied or neutral body parked on a group's route, walked through
`SandataSimulation.RunTick` rather than `LocalAvoidance.Commit` directly, so
that `AdvancePathService`, `GroupPathState`, and the state hash are all
genuinely exercised) is the natural verification this design's
implementation should add, though authoring it is implementation's task, not
a decision this design document is making on its own.

## Decision reversed, 2026-08-14

**Dynamic bodies now enter the nav search's blocked span.** The user's first
answer was to re-request the path and explicitly not to do this. That answer was
reversed the same day, once this document's own section 5 had established why it
could not work.

The reasoning that reversed it is worth keeping, because it is short and it is
checkable. A blocked mover has zero displacement by definition — that is what
`SandataMovementResolution.Blocked` means — so its start cell is unchanged. Its
goal is unchanged. The blocked span is unchanged. `NavSearch` contains no
randomness at all. A search over identical inputs therefore returns the identical
route, and the mover refuses it again on the next tick exactly as it refused it
on the last one. Re-requesting alone converts a silent forever-stall into a
bounded, legible, logged forever-stall. That is an improvement in observability
and no improvement at all in behaviour.

Everything above stays. The stall detector, the attempt cap, the published-path
clear, and the event are all still wanted, because a re-plan needs a trigger and
a spectator needs to see it happen. What changes is that the search the trigger
fires now has something new to see.

**What the reversal opens, and what this document does not yet answer.** Putting
a body into the span is not a one-line change, and the questions it raises belong
in a follow-up design rather than being answered here by assertion:

- **Which bodies?** Every living operator, or only those that have not moved for
  some number of ticks? Marking every operator every tick makes squads unable to
  path through their own formation.
- **When is the span written and cleared?** It is currently allocated once at
  construction and never written after. A per-tick write is a per-tick
  allocation unless the same array is reused, and stage 5 was once ninety-four
  percent of the tick's allocation.
- **Does a body block its own group?** A mover should not be blocked by the
  squadmate it is following.
- **Does this move the seed-1 baseline?** The workload leaves `Groups` empty, so
  no path is searched in it. That insulation is the same one this document
  already relies on, and it holds only until something wires the headless runner
  to a real map.
- **What happens to the path already published** when the body that blocked it
  moves away? A route around an obstruction that has gone is worse than the
  direct one.

Until those are answered, the mechanism above is the part that is designed.
