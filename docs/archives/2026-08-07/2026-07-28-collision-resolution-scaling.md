# Collision resolution scaling — plan

> **Archived: reference only.** This document is finished work, kept so the
> decision can be traced back to its reasoning. Do not execute it and do not
> cite it as the reason to change anything.

**Design:** [`2026-07-28-collision-resolution-scaling-design.md`](2026-07-28-collision-resolution-scaling-design.md).
This plan authorizes the implementation that document describes, and nothing
beyond it.

**Date:** 2026-07-28. Written against `main` at `a6ca2a8`, whose canonical gate
was run clean before this plan was written.

**Scope:** `src/Hukbo.Core/Simulation/CollisionUniformGrid.cs`,
`src/Hukbo.Core/Simulation/CollisionResolver.cs`, and the two test files that
cover them. Nothing else in `Hukbo.Core` changes.

## 1. The one correction this plan makes to the design

The design's section 6 names `71211929A44A16CA` and `A2DC3ECA3F7345ED` as the
200-agent golden pair that must not move. Those values predate the body-radius
change to 4.25 and are no longer what the repository records. The live pair,
reproduced by `./scripts/verify.ps1` on `a6ca2a8` immediately before this plan
was written, is:

| Field | Value |
| --- | --- |
| `stateHash` | `A080E28DA7C79C20` |
| `eventHash` | `2B6FB3A9A9C1960D` |
| `measuredTicks` | 1 677 |
| `outcome` | `Faction0Victory` |
| `maximumPenetrationRaw` | 0 |

The acceptance criterion is unchanged in substance — every recorded hash must
come back byte-identical — but it is checked against these values and against
the four-point sweep currently recorded in
[`docs/development/testing.md`](../../development/testing.md), not against the
figures quoted in the design.

## 2. Decisions the design left to this plan

The design's section 14 asks four open questions. Three are answered here; the
fourth is not this plan's to answer.

**Chain removal or a per-slot liveness flag (question 1).** Chain removal, as
the design recommends. The packing bound in the design's section 4 caps a cell
chain at four links, so an unlink is a walk of at most four slots, and the
structure stays exact rather than accumulating tombstones that every later query
in the same tick would have to walk past. A liveness flag would make removal
`O(1)` but would make every query in the tick progressively more expensive,
which is the wrong trade in a structure queried tens of millions of times per
tick and mutated once per mover.

**One class with new operations, or a distinct pending type (question 2).** One
class. `CollisionUniformGrid` gains the strict-overlap query, the coincidence
query, and removal; the resolver holds two instances of it. This is what the
design's section 12 asks for when it says the two queries should share the
traversal rather than duplicate it, and it means the neighbourhood offset table
and the cell-coordinate function exist in exactly one place.

The queries do **not** share one parameterized loop body. Three near-identical
inner loops are written out, one per predicate, because a predicate switch
inside the innermost loop of the hottest method in the program is a real cost
and this whole workstream exists to remove cost from that loop. What protects
coverage instead is the naive-reference test required by task 8: each query is
checked against an `O(n^2)` scan over the same body set, so a neighbourhood that
diverged between two queries fails a test rather than silently committing an
overlap. This is a deliberate departure from the letter of the design's section
12 and it is recorded here rather than made quietly.

**Axis-delta early rejection inside the bounded scan (question 3).** Not in this
plan. The design itself calls it cheap to try and cheap to drop; adding it here
would mean a second, independently-motivated change riding inside a workstream
whose entire verification strategy is "the hashes did not move". It stays
available as its own small piece of work once the bounded scan exists.

**The supported agent ceiling (question 4).** Not answered. It is a product
decision, it belongs to the user, and neither this plan nor the deadlock work
can resolve the design's section 8 without it. This plan proceeds on the narrow
ground that the change is hash-neutral, confined to two files, and cheap to
prove — not on a claim that 2,000 agents is a supported population.

## 3. Task list

Each task is small enough for one sitting and names its files and its
verification. Tasks 1 to 6 are strictly ordered; 7 to 12 are verification.

### Task 1 — the strict-overlap grid query

**File:** `src/Hukbo.Core/Simulation/CollisionUniformGrid.cs`.

Add `AnyOverlap(int xRaw, int yRaw, int bodyRadiusRaw, ulong excludeEntityId)`,
answering "does any indexed body other than the excluded one strictly overlap a
body centred here", using `CollisionGeometry.Overlaps` over the existing
`NeighbourOffsets` table in the existing order.

Add `AnyOverlapUnchecked` alongside it with the same body and no argument
validation, and make `AnyOverlap` a validating wrapper over it. The unchecked
form is what the resolver calls; the validating form is what tests and any
future caller outside the hot loop call.

The doc comment must state the neighbourhood-sufficiency argument in the terms
the design's section 5.1 uses: strict overlap is a subset of contact, so the
cell-size guard that makes a three-by-three neighbourhood sufficient for contact
is a fortiori sufficient for overlap.

**Verification:** compiles; task 8 tests it.

### Task 2 — the coincidence grid query

**File:** `src/Hukbo.Core/Simulation/CollisionUniformGrid.cs`.

Add `AnyCoincident` and `AnyCoincidentUnchecked` the same way, using
`CollisionGeometry.IsCoincident`. Exact coincidence is a subset of contact, so
the same sufficiency argument carries.

**Verification:** compiles; task 8 tests it.

### Task 3 — removal

**File:** `src/Hukbo.Core/Simulation/CollisionUniformGrid.cs`.

Add `Remove(in CollisionBody body)`, which unlinks the slot holding that entity
from the chain of the cell the body's coordinates map to.

The contract has to be written at the symbol, not left implicit, because this is
the operation the design's section 12 names as the one place a bug hides:

- A dead body is ignored on the way in, so removing one is a no-op, matching
  `Insert`.
- Removing a body that is not present is a no-op rather than a throw, and the
  test in task 9 pins that choice.
- Removal does not update `Pairs`, exactly as `Insert` does not. The pending
  index never calls `Rebuild` and never reads `Pairs`.
- A removed slot stays allocated and stays counted by `_bodyCount`. That field
  drives capacity and the `_bodyCount < 2` early return in `GeneratePairs`, not
  traversal, and no traversal can reach an unlinked slot.

**Verification:** compiles; task 9 tests it.

### Task 4 — the resolver uses the grid for its committed queries

**File:** `src/Hukbo.Core/Simulation/CollisionResolver.cs`.

Replace the `AnyContact`-then-`OverlapsCommitted` pair in `IsFree` with a single
`AnyOverlapUnchecked` call on the committed grid, and the
`AnyContact`-then-linear-scan pair in `IsCoincidentWithCommitted` with a single
`AnyCoincidentUnchecked` call.

Delete `OverlapsCommitted` and the linear coincidence scan once nothing calls
them.

**Amended during implementation.** This task originally said the `_committed`
array would stay, on the reasoning that it is the resolver's own record of the
tick. That turned out to be false once both scans were gone: `_committed` and
`_committedCount` were written by `Commit` and grown by `Reset` and then read by
nothing at all. Keeping a per-tick array that no code reads is dead state, and
it would have carried a real cost — one `CollisionBody` write and a capacity
check per agent per tick, plus the array itself. Both were removed. The
committed set now lives only in the committed index, which is the thing that
actually answers questions about it.

Validate the body radius against both grids once, in the resolver constructor,
where the radius is fixed and known. That is the design's section 5.3 hoist. The
coordinate validation the unchecked queries skip is provably redundant on these
paths: every candidate reaching `IsFree` has been through
`CollisionGeometry.ClampCenterToBounds` into `[R, dimension - R]`, and the
`TrySeparate` candidate is bounds-checked by `IsInsideBounds` before the call,
so both are at least `R`, which is positive. Say so in a comment at the call
site rather than leaving a reader to reconstruct it.

**Verification:** the full Core test suite. Every hash in the suite must be
unchanged at this point, before the pending index exists — this task alone is
already required to be hash-neutral, and isolating it means a later failure
cannot be ambiguous between the two halves of the change.

### Task 5 — the pending index

**File:** `src/Hukbo.Core/Simulation/CollisionResolver.cs`.

Add a second `CollisionUniformGrid` field, constructed in the resolver
constructor with the same diameter cell size as the committed grid.

In `Reset`, after the mover list is built and sorted, clear the pending index
and insert every mover as a `CollisionBody` at its tick-start position, alive.

In `CommitMovers`, remove the current mover from the pending index at the top of
each iteration, before any candidate for that mover is evaluated.

Replace the linear pending loop in `IsFree` with an `AnyOverlapUnchecked` call
on the pending index. The `pendingFrom` parameter disappears from `IsFree`,
`TryAccept`, and `TryTruncate`, because the index now carries that state
structurally.

The equivalence to argue in the code comment, and the thing task 10 tests: at
every point in `CommitMovers`, the pending index holds exactly the movers at
resolution indices `moverIndex + 1` through `_moverCount - 1`, at their
tick-start positions, which is precisely the set the old `pendingFrom` walk
enumerated. `TrySeparate` runs during the stationary pass, before any removal,
against the full index, which is exactly what its `pendingFrom: 0` meant.

**Verification:** the full Core test suite, and the seed-1 headless workload.

### Task 6 — remarks and cache declarations

**Files:** both collision files.

Update the `CollisionResolver` class remarks, which currently describe the grid
as a broad-phase negative filter over committed bodies and describe the pending
test as a linear walk. Both sentences become false in task 4 and task 5 and must
not be left describing a resolver that no longer exists.

State the section 15 obligation at the new symbols: the pending index keys cell
lookup by a packed-integer dictionary that is never enumerated, and the thing
actually traversed is an ordered chain. State the section 6 cache declaration
for the second grid: derived, rebuilt per tick, never hashed, never snapshotted,
never persisted, bounded by the living mover count.

**Verification:** documentation review; no behaviour.

### Task 7 — the resolver-level equivalence test

**File:** `tests/Hukbo.Core.Tests/CollisionResolverTests.cs`.

For a set of seeded pseudo-random scenarios — varying agent count, density,
proposal direction, and including a fully packed configuration — assert that the
resolver's whole `Results` list, positions *and* `MovementResolution` values in
request order, equals what a reference implementation of the old algorithm
produces on the same input.

The reference implementation lives in the test file, is the linear form written
out plainly, and is the design's stronger-than-hash criterion: it localizes a
failure to the resolver rather than to the tick.

### Task 8 — naive-reference tests for the new queries

**File:** `tests/Hukbo.Core.Tests/CollisionUniformGridTests.cs`.

For `AnyOverlap` and `AnyCoincident`, over many seeded random body
configurations, assert agreement with an `O(n^2)` scan using the same predicate.
Include the degenerate cases the design's section 9 names: bodies exactly
tangent, bodies straddling a cell boundary, bodies at map corners, a single
body, and an empty grid.

### Task 9 — removal tests

**File:** `tests/Hukbo.Core.Tests/CollisionUniformGridTests.cs`.

After a body is removed, no query sees it. After every body is removed, every
query answers as an empty grid. A body removed and reinserted is seen again.
Removing a body that was never inserted is a no-op, and the test says so
explicitly rather than leaving it to be discovered.

### Task 10 — the pending-set invariant test

**File:** `tests/Hukbo.Core.Tests/CollisionResolverTests.cs`.

The claim in task 5 — that the index holds exactly movers `moverIndex + 1`
onward at every step — is the one that removal bugs violate. Test it directly
rather than only through its consequences.

### Task 11 — the existing guarantees, unchanged

No new code. Confirm the existing suite still covers, and still passes: no two
committed bodies strictly overlap after `Resolve`, on every workload; the warm
tick allocates within the existing 8,192-byte and 16,384-byte ceilings in
`BattleSimulationTests`; the twenty-seed last-stand tests are green.

The allocation ceilings deserve a sentence of reasoning rather than a shrug. The
second grid's buffers grow by doubling and are sized by the living mover count,
which only falls as a battle proceeds, and both allocation tests warm up
thirty-two ticks before the measured window opens. Growth therefore happens
before measurement. If either window regresses, the pending index is being
rebuilt rather than reused and the implementation is wrong.

### Task 12 — measurement

Rerun the four-point sweep — 200, 500, 1,000, and 2,000 agents, seed 1, 10,000
ticks, a fresh process per point — and compare every hash against
`docs/development/testing.md`. A hash that moves stops the work; it is not
regenerated.

Record the before-and-after timing table. The falsifiable hypothesis, stated
provisionally per the design's section 9: the p50 scaling exponent between 1,000
and 2,000 agents falls below 1.5, from the 2.19 recorded at 4.0 body radius. If
the measured number does not clear that bar, this plan records the real number
and does not move the bar.

The `dotnet-trace` stage profile at 2,000 agents is desirable and is **not** a
blocker for integration. It is recorded as a separate follow-up if the timing
table already shows the effect, because the profile is a hand-run tooling
exercise and the hash evidence is what actually establishes correctness.

### Task 13 — the canonical gate

`./scripts/verify.ps1`, run once, after integration, with its real output
pasted. Not delegated. No sub-agent report substitutes for it.

## 3a. Results

Measured 2026-07-28 on the tree this plan produced. Seed 1, 10,000 requested
ticks, a fresh process per point, Release, Windows 11 Pro 10.0.26200,
.NET 10.0.10, twenty logical processors.

**Hash neutrality: achieved at every point.** All four state hashes, all four
event hashes, all four tick counts, and all four outcomes are byte-identical to
the values recorded in `docs/development/testing.md`. At 200 agents every
collision counter is identical too, down to `candidatePairs 172643`,
`acceptedMoves 91766`, `blockedAgentTicks 45265`, and
`maximumPenetrationRaw 0`.

| Agents | ticks | `stateHash` | `eventHash` | outcome | matches record |
| --- | --- | --- | --- | --- | --- |
| 200 | 1 677 | `A080E28DA7C79C20` | `2B6FB3A9A9C1960D` | `Faction0Victory` | yes |
| 500 | 2 859 | `F9267D5B9DFB50E1` | `BD3E753BEB76CD33` | `Faction0Victory` | yes |
| 1 000 | 9 294 | `6D35D701D9423C27` | `8B22790BAC7940EB` | `Faction1Victory` | yes |
| 2 000 | 10 000 | `AF9E348B016FF09F` | `5EA9027348AE764F` | `Draw` | yes |

**Timing, before and after.**

| Agents | p50 before | p50 after | p95 before | p95 after | max before | max after |
| --- | --- | --- | --- | --- | --- | --- |
| 200 | 0.0887 | 0.0916 | 1.6860 | 0.8952 | 11.0047 | 11.6005 |
| 500 | 0.2391 | 0.1969 | 1.9310 | 1.3553 | 16.9044 | 12.8902 |
| 1 000 | 0.8481 | 0.6989 | 6.2364 | 3.5361 | 43.2692 | 22.5612 |
| 2 000 | 17.3454 | 6.7007 | 51.5116 | 11.4528 | 274.8558 | 43.9161 |

The 2,000-agent point improves by 61 % at p50, 78 % at p95, and 84 % at the
worst tick. The 3.44x regression that the body-radius move from 4.0 to 4.25
introduced at that point is now a 1.33x difference against the 4.0 figure of
5.0435 ms, rather than a 3.44x one. The 200-agent p50 is unchanged within noise,
which is expected: at that density the linear scans were already short.

**The stated hypothesis is refuted, and the bar is not moved.** Task 12
predicted the p50 scaling exponent between 1,000 and 2,000 agents would fall
below 1.5. Measured, it falls from 4.35 to 3.26. The real numbers:

| Comparison | p50 ratio | exponent `k` |
| --- | --- | --- |
| 1 000 to 2 000, before | 20.45 | 4.35 |
| 1 000 to 2 000, after | 9.59 | 3.26 |

The prediction was wrong, and it was wrong in a way the design already
warned about. Its section 9 says in terms that fixing collision will not make
the tick linear, because `SelectTargetsAndIntents` is itself an all-pairs scan
and becomes the dominant quadratic term once collision's is gone. Writing a
`k < 1.5` bar into this plan contradicted that warning; the design was right and
the plan's own hypothesis was too aggressive. The absolute improvement is large
and real, the exponent is lower, and the curve is still super-linear. Whoever
picks up target-selection acceleration should treat `k = 3.26` as the number to
beat, and it needs its own design document.

**Allocation.** `coreAllocatedBytes` rose by roughly 31 % at every point — 118 896
to 154 976 at 200 agents, 1 141 912 to 1 492 256 at 2 000. This figure measures
simulation startup rather than per-tick behaviour, and the increase is the second
index's buffers being allocated once. It is not hashed and not persisted. The
figure that matters for the no-allocation contract is the warm-tick window, and
both windows in `BattleSimulationTests` remain inside their existing 8,192-byte
and 16,384-byte ceilings.

**Tests.** 627 Core tests pass, up from 610. The seventeen new ones are the
naive-reference tests for both new queries, the removal tests, the pending-set
boundary test, and the resolver-level equivalence test against
`NaiveCollisionResolution`, which agrees element for element across 24 seeds in
both a jittered and a tangent-packed layout.

The `dotnet-trace` stage profile at 2,000 agents was not run, per task 12's
explicit allowance. The hash evidence establishes correctness and the timing
table establishes the effect; the profile would only re-apportion credit within
the tick. It stays available as follow-up work.

## 4. What stops this plan

- Any recorded state hash or event hash moving. That is a defect in the removal
  bookkeeping or the neighbourhood coverage, and the design says so.
- Any warm-tick allocation ceiling regressing.
- The equivalence test in task 7 disagreeing on any seed.

None of these is a case for regenerating a golden expectation. This workstream
has no legitimate hash change in it at all.

## 5. Out of scope

Everything the design's section 13 excludes, and additionally the axis-delta
rejection from section 14's question 3, per the decision in section 2 above.

## 6. Relationship to the deadlock work

[`2026-07-28-follower-trailing-deadlock-design.md`](../../plans/2026-07-28-follower-trailing-deadlock-design.md)
touches the same method with the opposite hash requirement. Its section 8 puts
this work first and this plan takes that ordering as given. The deadlock
investigation's first step is a diagnostic run rather than a code change, so it
may proceed alongside this plan; no deadlock *fix* may be implemented until this
plan is integrated and its hashes are confirmed unmoved.
