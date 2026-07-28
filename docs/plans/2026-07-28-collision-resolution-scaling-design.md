# Collision resolution scaling — design

**Status:** Design complete, no plan document, nothing implemented. Per
`CLAUDE.md` section 6 and [`docs/plans/README.md`](README.md), a `-design.md`
document does not authorize implementation. No line of `Hukbo.Core` may change
on the strength of this file alone.

**Date:** 2026-07-28.

**Scope:** `CollisionResolver` and `CollisionUniformGrid` in
`src/Hukbo.Core/Simulation`, and nothing else. The candidate ladder, the
priority order, the commit order, the boundary clamp, the co-location repair,
and the contact-band metrics are all left exactly as they are.

**Central claim:** the collision stage can be made to scale far better than it
does today *without changing a single committed position*. Every hash recorded
in [`docs/development/testing.md`](../development/testing.md) must come back
byte-identical afterward. A hash that moves is a defect in the implementation,
not a reason to cut a new preset version.

## 1. Why this document exists

The performance hardening workstream archived earlier today closed with an
explicit hand-off. Its plan document recorded that the stage profile it
produced "points at collision resolution as the next candidate for attention;
that stage is explicitly out of scope for this plan and needs its own design
document before anyone touches it." The same sentence is repeated in
[`docs/plans/README.md`](README.md), and
[`docs/research/TICK-STAGE-PROFILE.md`](../research/TICK-STAGE-PROFILE.md)
ends with "This is a finding to record, not work to start."

This is that document. It starts nothing either; it establishes what the
problem is, what the fix would be, why the fix is safe, what would have to be
measured, and — in section 8 — the case for doing nothing at all, which is a
live option and not a formality.

## 2. The measured problem

Two independent measurements agree.

**The scaling sweep** (T2 in `docs/development/testing.md`, seed 1, 10,000
ticks, fresh process per point, Release, Windows 11 Pro 10.0.26200, .NET SDK
10.0.302):

| Agents | p50 ms | p95 ms | p99 ms | max ms |
| --- | --- | --- | --- | --- |
| 200 | 0.0806 | 1.5296 | 2.6791 | 9.5617 |
| 500 | 0.3494 | 1.9734 | 3.8109 | 15.1798 |
| 1 000 | 1.3677 | 5.5727 | 6.7685 | 26.2590 |
| 2 000 | 6.2447 | 20.4695 | 29.9286 | 135.9969 |

Reading p50 growth between adjacent points as the exponent `k` in
(cost ratio) = (agent ratio)^k gives `k = 1.60` from 200 to 500 agents,
`k = 1.97` from 500 to 1,000, and `k = 2.19` from 1,000 to 2,000. The cost is
super-linear at every step measured and the exponent is *rising* with scale,
not falling.

**The stage profile** (`docs/research/TICK-STAGE-PROFILE.md`) says where that
cost sits. `ResolveCollisions` takes 63.11 % of `AdvanceOneTick` at 200 agents,
70.11 % at 1,000, and 74.77 % at 2,000. Inside it, at 2,000 agents,
`CollisionResolver.IsFree` alone is 50.62 % of all exclusive tick time — half
of the entire tick in one method — and `CollisionResolver.CommitMovers` is a
further 22.46 % exclusive.

Both figures carry the limitations that document states plainly: these are
sampled wall-clock shares at roughly 100 Hz, not per-tick percentiles, and a
sampling profiler can misattribute an inlined callee to its caller. The high
exclusive share at the `CommitMovers` frame is very likely inlined
`TryAccept` and `OverlapsCommitted` work rather than anything `CommitMovers`
does in its own body. Nothing in the argument below depends on separating
those two frames.

## 3. Where the quadratic term actually is

`CollisionResolver.IsFree` (`src/Hukbo.Core/Simulation/CollisionResolver.cs:648`)
tests a candidate position against two sets of obstacles, and it treats them
very differently.

**Committed bodies** are indexed in a `CollisionUniformGrid`. The grid answers
the cheap negative case, and only when it reports contact does
`OverlapsCommitted` run — which is then a full linear scan over every body
committed so far this tick
(`src/Hukbo.Core/Simulation/CollisionResolver.cs:683`). The grid filter is real
and it works, but it is a filter on *whether* the scan runs, not on *how long*
the scan is. In a melee line, where contact is the normal state rather than the
exception, the filter passes constantly and the full scan runs behind it.

**Pending movers** are not indexed at all. The loop at
`src/Hukbo.Core/Simulation/CollisionResolver.cs:664` walks
`_moverIndices[pendingFrom .. _moverCount]` linearly on every call, with no
spatial filter of any kind in front of it, comparing the candidate against the
tick-start position of every mover that has not yet been resolved. For the
first mover of the tick that is very nearly every agent on the field.

The multiplication that follows is the whole problem:

- Up to fourteen candidate positions are evaluated per mover — the preferred
  destination, two single-axis slides, and up to eleven truncation rungs
  (`MaximumTruncationRungs = 11`).
- Each candidate calls `IsFree` once.
- Each `IsFree` walks a pending list whose length averages half the mover
  count, and may walk the whole committed list behind the grid filter.

So the pending scan alone is `O(movers²)` per tick before the candidate factor,
and the committed scan is `O(movers × committed)` whenever the grid filter
passes. That is the `k = 2.19` in the T2 table, and it is why the exponent
rises rather than falls: the denser the field gets, the more often the grid
filter passes and the longer the average committed scan becomes.

An early exit on the first overlap keeps the constant down in practice — a
mover surrounded on all sides finds its blocker quickly — but the mover that is
in open ground, whose first candidate is accepted, is exactly the one that must
walk the entire pending list to prove it. The common case is the expensive one.

## 4. Why the fix is cheap: a bound the current code does not use

The resolver's grid uses a cell edge of one body diameter
(`CollisionResolver` constructor, `src/Hukbo.Core/Simulation/CollisionResolver.cs:205`),
and `ValidateBodyRadius` enforces that a cell is never narrower than a
diameter. At the default scenario values — `FixedPoint.Scale` 1024,
`BodyRadiusRaw` 4,096, so a diameter of 8,192 raw units, on a 1,280 × 720
world map — the grid is 160 × 90 cells, 14,400 cells, and 2,000 agents average
0.14 bodies per cell.

The average is not the interesting number. The *bound* is. Both sets the
resolver queries are guaranteed pairwise non-overlapping — the committed set by
the resolver's own output invariant, the pending set by the documented
precondition that no two request start positions strictly overlap. Centres
that are pairwise at least one diameter apart cannot pack more than four into a
square cell of edge exactly one diameter; four corners is the packing, and a
fifth centre would have to sit within one diameter of one of them. So any
three-by-three neighbourhood contains at most thirty-six bodies of either set,
**independent of agent count**.

That bound is already what makes `CollisionUniformGrid.AnyContact` fast, and it
is available to both of the linear scans in `IsFree`. Neither uses it. The
committed scan bypasses the grid the moment the filter passes; the pending scan
has no grid to bypass.

## 5. The proposal

Three changes, all inside the two collision files.

### 5.1 A strict-overlap query on the grid

Add a query alongside `AnyContact` that applies
`CollisionGeometry.Overlaps` — the strict predicate — rather than the inclusive
`CollisionGeometry.IsContact`, over the same fixed three-by-three neighbourhood
in the same fixed offset order.

The neighbourhood-sufficiency argument transfers unchanged and a fortiori:
strict overlap is a strict subset of contact, so any pair the strict predicate
would report is a pair the inclusive predicate would also report, and the
existing proof that a cell of at least one diameter makes three-by-three
sufficient for contact therefore covers overlap as well.

This replaces the current two-step `AnyContact`-then-linear-`OverlapsCommitted`
dance with one bounded pass. `IsCoincidentWithCommitted` can take the same
treatment for the same reason: exact coincidence is a subset of contact.

### 5.2 A second grid over the pending movers

Index every mover at its tick-start position in its own uniform grid, built
once in `Reset`, and query it with the strict predicate instead of walking
`_moverIndices` linearly.

The grid must support removal, because a mover stops being a pending obstacle
at the moment it becomes the mover under consideration. Removal happens at the
top of each iteration of `CommitMovers`, before any candidate for that mover is
evaluated. That reproduces today's `pendingFrom = moverIndex + 1` semantics
exactly: at every point in the loop, the pending grid holds precisely the
movers at indices `moverIndex + 1 .. _moverCount - 1`, at their tick-start
positions, and the current mover is in neither grid until it commits.

Removal from the existing singly-linked per-cell chain is a walk of that one
chain, which the bound in section 4 caps at four links. The alternative — a
per-slot liveness flag tested during the scan — is also viable and avoids
touching the chain at all, at the cost of leaving dead slots in the walk. The
plan document should pick one and say why; the chain walk is the recommendation
because it keeps the structure exact rather than accumulating tombstones across
a tick.

`TrySeparate` passes `pendingFrom: 0` today, meaning "every mover is still
pending." It runs during the stationary pass, before any removal has happened,
so the full pending grid is exactly the right answer and that call site needs no
special handling.

### 5.3 Hoist the per-query validation

`AnyContact` calls `ValidateBodyRadius` and `ValidateCoordinates` on every
invocation. Those are correct as an external contract and cheap individually,
but they run tens of millions of times per tick in this call path, on arguments
that are loop-invariant within a tick. The internal query the resolver uses
should validate once per tick rather than once per candidate, with the public
validating entry point retained for callers outside the hot loop. This is a
minor item and should not be allowed to justify the workstream on its own.

### 5.4 What this does to the cost

Per candidate, `IsFree` goes from "walk up to N committed bodies plus up to N
pending movers" to "test at most thirty-six committed bodies plus at most
thirty-six pending movers," with the bound independent of agent count. The
per-tick collision cost becomes linear in the mover count times a bounded
constant, rather than quadratic.

This is a hypothesis about the implementation's structure, not a measured
result, and section 9 requires it to be measured rather than assumed.

## 6. Why this cannot change a single outcome

This is the part that has to be right, and the argument is short enough to
check.

`IsFree` computes an existential quantification over a finite set of bodies:
*does there exist a body B, other than this agent, such that a body centred at
the candidate position would strictly overlap B?* The answer to an existential
question over a finite set does not depend on the order the set is enumerated
in, and does not depend on which enumeration mechanism produced it. It depends
only on the membership of the set and on the predicate.

The proposal changes neither. The set is the same set — every body committed so
far this tick, plus every mover not yet resolved, at its tick-start position.
The predicate is the same predicate, `CollisionGeometry.Overlaps`, with the
same radius. Only the traversal changes, and the traversal is provably complete
because of the cell-size argument in section 5.1.

Therefore every candidate that is accepted today is accepted afterward, every
candidate that is rejected today is rejected afterward, the same rung of the
truncation ladder is taken, the same `MovementResolution` is recorded, and the
same positions are committed in the same order. `MovementResolution` is part of
the state hash, so this is not a claim about positions only.

Everything upstream and downstream is untouched: the priority key and its sort,
the stationary-first commit order, the candidate ladder and its order, the
boundary clamp, the co-location repair and its four fixed directions, the
`AcceptedMoveCount` and `BlockedCount` counters, and the separate contact-band
grid that `MeasureCollision` rebuilds.

**The consequence, stated as an acceptance criterion:** every state hash and
event hash in `docs/development/testing.md` must be byte-identical after the
change. `71211929A44A16CA` / `A2DC3ECA3F7345ED` at 200 agents, and the 500-,
1,000-, and 2,000-agent pairs from T2. No new preset version is created, no
golden expectation is regenerated, and a hash that moves means the removal
bookkeeping or the neighbourhood coverage is wrong.

## 7. The standards clause that authorizes this

`SIMULATION-GAME-STANDARDS.md` section 6 says: "Prefer rebuilding over
incremental invalidation until profiling proves otherwise." The pending grid
proposed here is incremental — it is built once per tick and then mutated by
removal as movers resolve — so it sits on the far side of that preference and
needs the "until profiling proves otherwise" clause to stand.

The profiling exists. `docs/research/TICK-STAGE-PROFILE.md` is a Release-build,
unmodified-binary, external-profiler measurement of the shipped headless
workload at three agent counts, and it puts a single method at 50.62 % of
exclusive tick time. The T2 sweep independently shows the exponent rising with
scale. This is the evidence that clause asks for, and the plan document should
cite it in those terms rather than assuming the preference simply does not
apply.

Two further standards obligations attach:

- **Section 15's lookup-only-hash-container rule.** The pending grid, like the
  existing one, keys cell lookup by a packed integer `Dictionary<long, int>`
  that is never enumerated, while the thing actually iterated is an ordered
  structure. `CollisionUniformGrid` already documents that separation at the
  symbol and the second instance inherits it, but any new field added for
  removal bookkeeping must state the same contract at the symbol rather than
  leaving it implicit.
- **Section 6's cache declaration.** Both grids are derived accelerators:
  rebuilt each tick, never hashed, never snapshotted, never persisted, bounded
  by the living agent count. The second grid changes none of that and adds no
  new persisted or hashed field.

## 8. The option of doing nothing

This has to be stated honestly, because the numbers do not say what a quick
reading of "74.77 % of tick time" suggests.

The canonical gate runs a 200-agent workload. At 200 agents the p50 tick is
0.0806 ms and the p95 is 1.5296 ms against a 20 Hz tick budget of 50 ms. There
is no shipped requirement that 2,000 agents run well; the 2,000-agent point is
a stress report, exactly as `SIMULATION-GAME-STANDARDS.md` section 8's workload
matrix describes the larger workloads. At 2,000 agents the p50 of 6.24 ms and
the p95 of 20.47 ms are still inside a 50 ms 1x budget, and only break the
12.5 ms budget that a 4x requested speed would impose, at p95.

So nothing is failing today. The case for doing the work is that the exponent
is rising rather than flattening, that the change is hash-neutral by
construction and therefore unusually cheap to verify, and that the fix is
confined to two files with no new dependency and no new persisted state. The
case against is that it buys headroom nobody has asked for yet, in a stage that
already passes every contract the repository actually holds itself to.

**What should decide it:** whether the campaign layer, the 4x speed target, or
a larger supported battle size is close enough to matter. If the answer is
"not yet," the correct outcome is to leave this document in `docs/plans/` as an
approved design with no plan behind it — exactly the state the preset V3 and
shields designs are in — and revisit it when a real requirement lands.

## 9. What a plan document would have to verify

Not a task list. These are the acceptance criteria a future plan would have to
carry, and several of them are the reason the work is cheap to trust.

**Hash neutrality, which is the primary criterion.** Rerun the four T2 points at
seed 1 and 10,000 ticks in a fresh process each, and require every state hash
and event hash to be byte-identical to the recorded values. Any movement stops
the work.

**A naive-reference test for the new query**, as
`SIMULATION-GAME-STANDARDS.md` section 9 requires of optimized spatial logic:
the strict-overlap grid query must agree with an O(n²) scan over the same body
set, across many seeded random configurations including degenerate ones —
bodies exactly tangent, bodies on cell boundaries, bodies at map corners, a
single body, and an empty set.

**A removal test.** After a body is removed from the grid, no query sees it;
after every body is removed, the grid answers as an empty grid; a body removed
and reinserted is seen again. Removal of a body that is not present is either
rejected loudly or a documented no-op, and the test pins which.

**An equivalence test at the resolver level.** For a set of seeded scenarios,
the new resolver's full `Results` list — positions *and* `MovementResolution`
values, in request order — must equal the current resolver's, element for
element. This is stronger than the hash test and localizes a failure to the
resolver rather than to the tick as a whole.

**The existing overlap invariant, unchanged.** No two committed bodies strictly
overlap after `Resolve`, on every workload.

**Allocation.** A warm tick must still allocate nothing from the collision
stage. The second grid's buffers are allocated once and grown by doubling, like
the first. The evidence is the `coreAllocatedBytes` figure current at the time
the plan runs, per agent per tick, unchanged.

**Before-and-after performance on the same workload**, per
`SIMULATION-GAME-STANDARDS.md` section 8: the four-point T2 sweep rerun, and a
fresh `dotnet-trace` stage profile at 2,000 agents recorded the same way
`docs/research/TICK-STAGE-PROFILE.md` was, so the `ResolveCollisions` share can
be compared against 74.77 % directly.

**A stated, falsifiable performance hypothesis**, marked provisional in the
language section 8 uses for the Gate 0 hypotheses. The proposed one: the p50
scaling exponent `k` between 1,000 and 2,000 agents falls below 1.5, down from
2.19. If the measurement does not clear that bar, the plan reports the real
number and does not quietly move the bar.

**One expectation that must not be set.** Fixing collision will not make the
tick linear. `SelectTargetsAndIntents` is itself an all-pairs scan — every
living agent against every living agent, with an axis-delta early rejection in
front of the squared-distance test — and it is already 16.67 % of the tick at
2,000 agents. Remove collision's quadratic term and target selection becomes
the dominant one, and the overall curve stays super-linear. The archived Gate A
closed spatial acceleration for target selection as a candidate; reopening it
would be a separate design document, not a task in this one.

**The canonical gate**, `./scripts/verify.ps1`, run once after integration with
its real output pasted. It is not delegated and no sub-agent report substitutes
for it.

## 10. The nine questions

`SIMULATION-GAME-STANDARDS.md` section 10 requires every feature proposal to
answer these.

1. **User-visible outcome.** None, by construction, and that is the point —
   see question 8.
2. **Tick stage and state read/written.** Stage 4, `ResolveCollisions`. It
   reads the same agent positions and movement proposals it reads today and
   writes the same committed positions and `MovementResolution` values. No
   field is added, removed, or repurposed.
3. **Numeric units, bounds, and same-tick conflict rule.** All raw fixed-point
   integer units, unchanged. The same-tick conflict rule — stationary bodies
   first in ascending entity ID, then movers in ascending priority key, first
   legal candidate on a fixed ladder wins the ground — is unchanged.
4. **Total ordering and random-stream policy.** Unchanged. The priority key,
   its entity-ID low half, and the sort that consumes it are untouched. No
   random stream is consulted by the collision stage before or after.
5. **Cache source and invalidation.** Two derived uniform grids over living
   bodies, rebuilt per tick, bounded by the living agent count, never hashed,
   never snapshotted, never persisted. The pending grid additionally shrinks by
   removal as movers resolve, and is discarded wholesale at the next `Reset`.
6. **Save, event, and version effect.** None. No persisted field, no event
   field, no enum value, no preset version. The absence of a version bump is a
   claim the hash test proves.
7. **Worst-case complexity and benchmark workload.** Per candidate: at most
   thirty-six committed and thirty-six pending overlap tests, bounded
   independently of agent count by the packing argument in section 4. Per tick:
   linear in mover count times the bounded candidate ladder. Workload: the four
   T2 points at seed 1, plus the 2,000-agent stage profile.
8. **Spectator explanation.** There is none, and there must not be one.
   `CLAUDE.md` section 6 asks whether a spectator can discover a feature's
   effect without reading source code; for a pure optimization that question
   inverts, and discoverability would be the defect. The archived hardening
   workstream took the same position and was hash-neutral by construction; this
   is the same shape of change and is held to the same standard.
9. **Tests that fail before and pass after.** The naive-reference test, the
   removal tests, and the resolver-level equivalence test in section 9 are all
   new and all fail before the query and the pending grid exist. The hash
   tests are the inverse case and are stated as such: they pass before *and*
   after, and their value is entirely in the "after."

## 11. Alternatives considered

**Index the pending movers in a second uniform grid** — the proposal. Reuses a
structure that already exists, is already proven deterministic, and already has
a naive-reference acceptance test. Needs one new query and one new removal
operation. Chosen.

**Sweep and prune along one axis.** A sorted-by-X structure with an interval
walk would also cut the pending scan. Rejected: it introduces a second spatial
structure with its own ordering and tie-breaking rules to get right, when the
grid is present, understood, tested, and has a tighter bound in a field that is
dense in both axes rather than one.

**Add an axis-delta early rejection to the two linear scans**, the way target
selection was amended under the archived Gate A. Rejected as insufficient
rather than wrong. It reduces the constant and leaves the quadratic term
exactly where it is, so the `k = 2.19` exponent does not move. It would be a
reasonable *addition* inside the bounded scan, not a substitute for bounding it.

**Reduce the candidate ladder** — fewer truncation rungs, or skipping the
slides when the preferred step is short. Rejected outright: it changes which
candidate is accepted, therefore changes committed positions, therefore moves
both hashes and requires a new preset version and new golden expectations. That
is a gameplay change wearing a performance change's clothes, and it would have
to be argued on gameplay grounds in its own document.

**Parallelize the mover loop.** Forbidden. `SIMULATION-GAME-STANDARDS.md`
section 15 rules out parallel queries against the single-threaded authoritative
schedule, and the loop is inherently sequential anyway — each mover's legality
depends on what every earlier mover committed.

**Replace the solid resolver with impulse-based or relaxation separation.**
Rejected on two independent grounds: `CLAUDE.md` section 9 forbids introducing
rigid-body physics, and it would change every committed position, requiring a
new preset version and a full regold.

**Do nothing.** A live option with a real case behind it. Section 8.

## 12. Risks

**Removal bookkeeping is the one place a bug hides.** A mover removed too early
becomes invisible to the movers resolved before it and can have its ground
taken; removed too late, it blocks itself. Either produces a wrong committed
position. The mitigation is that both failures move the state hash immediately
and visibly, and the seed-1 baseline catches them on the first run. This is
precisely the kind of bug the hash-neutrality criterion is good at finding.

**Neighbourhood coverage.** If the strict-overlap query's neighbourhood were
ever narrower than the contact query's, it would miss a blocker and commit an
overlap. The cell-size argument makes this structurally impossible as long as
the two queries share the same offset table and the same cell size; the plan
should have them share the traversal code rather than duplicate it.

**Two grids, two allocation paths.** The second grid doubles the collision
stage's buffer footprint. It is bounded by agent count and reused across ticks,
so the warm-tick allocation figure is the evidence, and section 9 requires it.

**A grid's cell size is derived from the body radius.** Both grids are
constructed from scenario values at simulation construction; a future scenario
with a larger movement speed relative to body radius does not change the
resolver grid's cell size, which is tied to the diameter. The existing
`ValidateBodyRadius` guard already fails loudly on a radius the cell cannot
serve, and the second grid inherits it.

**The profile could be wrong about `CommitMovers`.** The 22.46 % exclusive
share at that frame is very likely inlined callee time, and the stage profile
documents inlining misattribution as a known failure mode it did not rule out.
This does not affect the argument — the fix targets `IsFree`, whose 50.62 % is
attributed to its own frame — but the plan should not build any claim on the
`CommitMovers` number specifically.

## 13. Out of scope

- `SelectTargetsAndIntents` and its all-pairs scan. Separate design document.
- The contact-band grid rebuilt by `MeasureCollision`. It is observability
  only, it is already bounded by its own cell size, and it is 6.53 % of the
  tick at 2,000 agents.
- `System.Buffer.ZeroMemoryInternal`, which is 23.64 % of exclusive time at 200
  agents but 1.65 % at 2,000. The 200-agent column rests on few samples and the
  profile says so; this is worth a look on its own evidence, not on this
  document's.
- Anything that changes a committed position, a preset version, or a golden
  expectation.
- Campaign, economy, diplomacy, or map-generation state, per `CLAUDE.md`
  section 1.

## 14. Open questions for the plan document

1. Chain removal or a per-slot liveness flag (section 5.2)? Recommendation is
   chain removal; the plan should measure or argue rather than inherit.
2. Should the two grids share one class with a mode, or should the pending
   index be a distinct type? A distinct type documents its own removal contract
   more honestly; a shared class avoids duplicating the traversal that section
   12 wants shared.
3. Does the axis-delta rejection from the archived Gate A belong inside the
   bounded scan as well, once the scan is bounded? Cheap to try, and cheap to
   drop if it measures as noise against thirty-six tests.
4. What agent count, if any, is the supported ceiling this work is meant to
   serve? Section 8 cannot be resolved without it.
