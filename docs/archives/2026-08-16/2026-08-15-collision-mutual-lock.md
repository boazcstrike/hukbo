# The collision mutual lock — plan

**Archived: reference only.** Archived on 2026-08-16 alongside its design. Never
execute its task list — it was abandoned deliberately, by measurement, and the
section titled "What was measured, and what it refutes" is the reason.

**Closed on 2026-08-15 with no code shipped. Option 6.4 is refuted by
measurement.** The rotation pass this plan specifies was built twice, in two
independent implementations, and measured against the five seeds that still
stall. It does not fix them. Every source and test change was reverted; the
branch carries this document and nothing else, and `Hukbo.Core.Tests` is back at
2,568 passing with no behaviour changed and no hash moved.

The evidence is in "What was measured, and what it refutes" at the end of this
document. The short form: a locked pair does not want to trade ground, it wants
the *same* ground, so a legal exchange essentially never exists — and when the
displacement rule is broken to force one anyway, the stall count does not
improve. The next option is 6.5, sliding along the obstruction, and it needs its
own design because it changes how every battle looks.

Read this document for what was tried and why it failed. Do not execute its task
list.

Date: 2026-08-15
Design: `2026-07-28-follower-trailing-deadlock-design.md`, revived from the
archive the same day and binding on mechanism. Where this plan and that design
disagree, the design wins except on the points recorded under "Findings that
correct the design" below, where the design describes code that is not on disk.
Game: Hukbo only. No file under `src/Sandata.*` is touched.

Branch `collision-mutual-lock`, in the worktree at
`.claude/worktrees/collision-mutual-lock`, based on `main` at `a92aeb2`. That
worktree was verified before planning: it builds `Release` with exit code 0, and
its `Hukbo.Core.Tests` suite runs 2,568 tests with zero failures. That is the
baseline every task below is measured against.

## Why this is being built, stated honestly

The stall this work removes **does not reach the shipping configuration.** A
re-measurement on 2026-08-13 found zero stalls in 200 seeds at
`FormationRules.DefaultLastStandThresholdAgents` = 6, which is what the client
launches. Thresholds 7 and 8 still stall, and neither is reachable from
`Scenario.CreateDefault` or from the client — only a test that sets the
threshold by hand gets there. The intent-layer escape at
`FormationRules.StallEscapeStreakTicks` = 192 is what closed the reachable case,
and it did so without touching the resolver.

What is still real is the resolver-level defect underneath: two or more agents
whose preferred destinations pass through each other cannot move at any rung of
the candidate ladder, so the geometry repeats every tick and the priority
reshuffle changes nothing. That is measured, not supposed, and section 4 of the
design records the measurement. This package removes that defect.

The user was told the cost before this plan was written: every committed
position moves, so the state hash and the event hash both move on every seed,
and nine golden digest fixtures plus four assertion pairs are recaptured. The
user directed the work proceed. Task 12 makes that recapture an explicit,
measured step rather than a surprise found at the gate.

## Findings that correct the design

Each was checked against the working tree while planning, and each contradicts
something the design says. The design is otherwise binding.

1. **Section 3's code excerpt is stale.** It quotes a linear walk over pending
   movers. The pending test is now a spatial-index query —
   `_pendingGrid.AnyOverlapUnchecked` at `CollisionResolver.cs:702-714`, with
   the pending set seeded in `Reset` at `:423-433` and each mover removing
   itself at `:488-493`. That is the collision resolution scaling work, it is
   hash-neutral, and the *meaning* of the test is unchanged, so section 3's
   reasoning survives its own excerpt.
2. **Section 9 step 1 is already done.** It asks for a diagnostic run before any
   option is chosen. That run exists: `tools/Hukbo.Tools.DeadlockProbe` is on
   disk, it builds, and its results are recorded in
   `docs/research/2026-07-28-COLLISION-DEADLOCK-DIAGNOSIS.md`. Task 1 re-runs it
   against today's code rather than repeating the analysis, because the code has
   moved since the diagnosis was taken.
3. **Radius 4.5 needs no source edit.** `Scenario.BodyRadiusRaw` is an
   init-only property (`src/Hukbo.Core/Simulation/Scenario.cs:42`) and the probe
   exposes `--radius-raw`, defaulting to 4608. The design's framing of 4.5 as a
   constant that must be changed to test is wrong for tests and for the probe;
   it remains true for `benchmark.ps1`, which has no radius flag.
4. **There is a test-only oracle the design never mentions.**
   `tests/Hukbo.Core.Tests/NaiveCollisionResolution.cs` is an independent
   transcription of the resolver at commit `a6ca2a8`, and
   `Resolve_MatchesTheUnacceleratedAlgorithmOnEverySeededLayout` compares the
   two. A rotation the resolver performs and the oracle does not is a divergence
   that reddens that test, so the oracle gains the same rule. It stays an
   independent implementation and is never rewritten to call the resolver.
5. **`CollisionGeometry`'s header claim stops being true.** Lines 14-19 say
   swept geometry is deliberately absent because `MovementSpeedRaw <=
   BodyRadiusRaw` makes a swap geometrically unreachable between committed
   positions. Once a rotation commits, two bodies do exchange ground in one
   tick. The remark is rewritten to say the exchange is explicit, atomic, and
   validated rather than impossible.

## The option, and why the other four are rejected

**Option 6.4, rotation and swap detection, extended to a connected component
rather than a pair.** The rejections are against the measurement in the
diagnosis note, not against the design's own reasoning:

- **6.2, a second pass over blocked movers.** Rejected. It repairs the case
  where the blocker vacated, which is 5 records out of 2,977 in the measured
  stall. It fixes the half-rate column and leaves the hang.
- **6.3, dependency-ordered resolution.** Rejected. The dependency graph in the
  measured stall is symmetric — every edge has a matching reverse edge — so it
  is all cycles and no acyclic part, and the cycle rule would be doing all of
  the work anyway. It also introduces exactly the ordering rule that
  `CollisionPriority` exists to keep fair.
- **6.5, sliding along the obstruction.** Rejected for this package, not
  forever. It changes movement character visibly, which is a gameplay decision
  needing its own design, and it does not obviously fix a symmetric head-on
  pair where both tangents point the same way.
- **6.1, do nothing.** Rejected by the user's direction, with the cost recorded
  above.

The caveat the measurement adds and the design did not anticipate: agents 3 and
12 each participate in two locks at once, so detection must handle a component
larger than a pair. The mechanism below does.

## The mechanism was wrong once, and the measurement is what corrected it

The first mechanism below moved every member of a component **to its own
claim**. It was built, tested, and correct against its own specification, and it
fired exactly zero times in a real battle.

Instrumenting the resolver over 2,000 ticks of seed 160 at threshold 7 — the
counters were temporary and are not in the shipped code — gives the reason
without any interpretation needed:

| Outcome for a held mover that seeded a component | Count |
| --- | --- |
| Seeded a component | 14,791 |
| Abandoned: a blocker was not a held mover | 568 |
| Abandoned: the claim overlapped nothing | 5 |
| **Rejected: two members' claims overlapped each other** | **14,218** |
| Rejected: a claim was out of bounds | 0 |
| **Committed a rotation** | **0** |

Ninety-six per cent of candidates died on the pairwise-claim check, and that
check was doing its job. Two warriors locked against each other in a crush do
not want to *trade* ground. They want the *same* ground: both claims point into
the contested gap between them, so the claims overlap, and committing both would
leave two bodies inside each other. The rule was refusing an illegal move.

The design said this correctly and the first mechanism did not read it
carefully enough. Section 6.4's own words are "detect that a set of mutually
blocking movers would each fit in **the next one's vacated position**". The
target of a rotation is the ground the blocker leaves, not the ground the mover
originally asked for. A cycle of members moving onto each other's current
positions is a permutation of positions that are already pairwise legal, so the
result cannot overlap — that is the whole reason the design phrased it that way,
and it is why the pairwise check disappears rather than being relaxed.

Two consequences follow, and both are now part of the mechanism. The walk is
restricted to a **simple cycle**, because "the next one" has to be a function
and that means exactly one blocker per member. And a **reduce-displacement
check** is added, because the class header promises collision may only reduce
displacement: a member may only take its blocker's position if that position is
no farther from its start than its own claim was.

## The mechanism, specified

A third pass inside `Resolve`, after `CommitMovers` returns and before `Resolve`
exits. Nothing about passes one and two changes.

**Definitions.** A mover is *held* when its committed result is
`MovementResolution.Blocked` and its preferred destination differs from its
tick-start position. A held mover's *claim* is its preferred full-step
destination — the first rung of the ladder, never a slide or a truncation,
because the claim is the statement "I want that ground" and the lower rungs are
concessions.

**Edges.** For a held mover `m`, collect every committed body strictly
overlapping `m`'s claim. If every one of them is itself a held mover resting on
its own tick-start position, `m` has an edge to each. If any of them is not a
held mover — a stationary body, or a mover that actually moved — `m` has no
edges at all and cannot take part in an exchange.

**This rule was widened after the mechanism was first written, and the
measurement is why.** The first version required *exactly one* overlapping body,
on the assumption that a lock is a pair. Task 1's probe run on seed 160 measured
the real distribution over 2,703 blocked records: 1,498 have one blocker,
1,170 have two, and 35 have three. A one-blocker rule would therefore have left
roughly 45 per cent of the real locks untouched and would very likely not have
cleared a single stalled seed. The query the resolver calls collects the whole
overlapping set, sorted ascending by entity id so that the result cannot depend
on grid insertion order.

**Components.** Follow edges from each held mover in resolution order — that is,
ascending `CollisionMoveRequest.PriorityKey`, the order pass two already used —
taking the transitive closure. The component is eligible when every member is
held and every member's claim is occupied only by other members of the same
component. A walk that reaches a non-held body, revisits a mover already
committed to another component this tick, or exceeds `MaximumRotationMembers` =
8 members yields nothing and stops. A ring is the common case and a pair is its
smallest instance, but nothing in the rule requires the component to be a simple
cycle.

**A claim that overlaps nothing abandons its component.** This was ambiguous in
the first draft and the independent oracle read it the other way, which is how
the ambiguity was found: the oracle advanced a lone held mover whose claim had
become free, the resolver refused, and
`Resolve_MatchesTheUnacceleratedAlgorithmOnEverySeededLayout` failed on entity
50 of a seeded layout. Both readings fit the words. The narrower one is chosen
and the oracle was corrected to match it, for two reasons. A mover whose claim
is free is not locked against anybody — its blocker vacated during the mover
pass, which is the half-rate column the diagnosis measured at 1.4 per cent of
records, not the mutual lock this package exists to remove. And it would be
labelled dishonestly: the spectator would read `Movement: Swapping` on a warrior
that simply walked into empty ground. Advancing that mover is option 6.2 wearing
a rotation's name, and if it is ever wanted it should be built and argued for on
its own.

**Validation, before anything is committed.** A cycle rotates only when all of
the following hold. Every member's claim must not strictly overlap any committed
body outside the cycle. No two members' claims may strictly overlap each other.
Every member's claim must be inside the map bounds under the existing clamp.
A cycle that fails any of these is abandoned whole; no partial commit exists.

**Commit.** For a validated cycle, every member is removed from the committed
grid at its held position and re-inserted at its claim, then its result becomes
`MovementResolution.Rotated`, `BlockedCount` decreases and `AcceptedMoveCount`
increases by the member count. The grid has no update operation — `Insert` and
`Remove` are the whole surface — so removal of every member precedes insertion
of any member, which is also what makes the exchange legal rather than a
sequence of illegal intermediate states.

**Termination.** Each held mover is walked at most once, each walk is capped at
eight members, and a member committed to one cycle is excluded from every later
walk. The pass is therefore bounded by the held-mover count times eight, with no
convergence loop, no iteration count, and no wall-clock condition. That is the
class header's termination contract restated, and task 8 pins it as a test.

**Fairness.** Cycles are discovered in resolution order, which is the per-tick
`CollisionPriority` key, not the entity id. This matters and is the reason the
order is not "ascending entity id": faction 0 holds the low entity ids, and
ordering component discovery by entity id would hand every contested overlap
between two cycles to faction 0 — the exact regression the priority reshuffle
was built to remove. A rotation is additionally symmetric: every member of a
validated cycle advances, so no member wins ground at another's expense.

**The case no option covers.** The diagnosis names one: an agent whose
destination is permanently owned by a *stationary* neighbour that never proposes
a move. A rotation cannot help — there is no cycle, because the blocker never
wants to move. This package does not change that behaviour, and says so rather
than pretending otherwise. It does make it *countable*: task 9 adds
`stationaryBlockedAgentTicks` to the collision metrics, so the next person to
pick this up starts from a number instead of a re-derivation. Whether a
stationary blocker should be pushed, walked around, or left alone is a gameplay
decision and belongs to its own design.

## Standing rules for every task

1. `TreatWarningsAsErrors` is on repo-wide with nullable enabled. Do not weaken
   a test, a warning, or an analyzer to get green.
2. No `System.Random`, no floating point, no wall clock, and no allocation on a
   warm tick anywhere in `src/Hukbo.Core`. New scratch storage is reused between
   calls and grows only on insufficient capacity, through the existing
   `Grow<T>` helper at `CollisionResolver.cs:369-377`.
3. No task may flip a smoke-checklist row. Only a person at an interactive
   desktop may do that.
4. Every hash a task moves is re-measured by running a capture. A fixture edited
   by hand to agree with the code proves only that somebody edited it.
5. No agent commits. The integrator stages by pathspec.

## Tasks

Wave A and wave B run in parallel; their file sets are disjoint. Wave C is
serial and belongs to the integrator.

### Wave A — the simulation

| # | Task | Files | Done when | Depends on |
| --- | --- | --- | --- | --- |
| 1 | Re-run the deadlock probe against today's code and record what it finds, at radius 4.5, seed 12, 18 agents, threshold 9, and as a survey over seeds 1-200. This is a measurement, not a change | none — writes only to `artifacts/`, which is untracked | The probe's own output is pasted into the plan's results section, including whether the mutual lock still reproduces on today's build | — |
| 2 | Add `MovementResolution.Rotated = 6` with a doc comment stating that the numeric values are pinned and that the member is appended, never inserted | `src/Hukbo.Core/Simulation/CollisionRules.cs` | The enum compiles, the value is 6, and the doc comment names the atomic-exchange meaning | — |
| 3 | Add `MaximumRotationMembers` = 8 to the resolver's constants beside `MaximumTruncationRungs`, with a remark stating it is the termination bound and not a tuning value | `src/Hukbo.Core/Simulation/CollisionResolver.cs` | The constant exists and is referenced by the walk in task 5 | 2 |
| 4 | Give `CollisionUniformGrid` a deterministic set query, `CollectOverlapsUnchecked`, writing every strictly overlapping body into a caller-owned span, returning the count, signalling overflow as `length + 1`, and insertion-sorting the written prefix ascending by entity id. Same 3×3 walk and same unchecked contract as its neighbours. No allocation, no `Dictionary` enumeration. The sort is the determinism guarantee: cell chains follow insertion order, which is not stable across ticks | `src/Hukbo.Core/Simulation/CollisionUniformGrid.cs`, `tests/Hukbo.Core.Tests/CollisionUniformGridTests.cs` | Five unit tests pass, including one proving byte-identical results under every input permutation of the same world | — |
| 5 | Implement the third pass exactly as the mechanism section specifies: held-mover collection, edge discovery, bounded component walk, whole-cycle validation, atomic remove-then-insert commit, counter adjustment | `src/Hukbo.Core/Simulation/CollisionResolver.cs` | A two-agent head-on swap commits as `Rotated` for both; a three-agent ring commits; an invalid cycle leaves every member `Blocked`; the pass allocates nothing on a warm tick | 2, 3, 4 |
| 6 | Rewrite the `CollisionGeometry` header remark that claims a swap is geometrically unreachable, and the `CollisionResolver` header's order and termination sections, to describe the third pass | `src/Hukbo.Core/Simulation/CollisionGeometry.cs`, `src/Hukbo.Core/Simulation/CollisionResolver.cs` | No header sentence in either file contradicts the shipped behaviour | 5 |
| 9 | Add `stationaryBlockedAgentTicks` end to end: the per-tick struct, `CollisionMetrics.AddTick`, the record field, the `ToMetrics` projection. It counts a held mover whose single blocker is a body that proposed no movement this tick | `src/Hukbo.Core/Simulation/CollisionMetrics.cs`, `src/Hukbo.Core/Simulation/BattleSimulation.cs` | The counter appears in a headless run's JSON as `collisionMetrics.stationaryBlockedAgentTicks` with a non-negative value | 5 |

### Wave B — the tests and the spectator

| # | Task | Files | Done when | Depends on |
| --- | --- | --- | --- | --- |
| 7 | Invert the two pins that assert today's refusal, and say in each test's name and body that the refusal became an exchange: `Resolve_PreventsTwoAgentsFromSwappingPositions` (`:236-252`) and `Resolve_BlocksBothAgentsInAHeadOnApproach` (`:212-230`). Add the three-agent ring case and the "cycle touching a stationary body does not rotate" case | `tests/Hukbo.Core.Tests/CollisionResolverTests.cs` | Both former pins assert `Rotated`, the ring commits, and the stationary case still resolves `Blocked` | plan only |
| 8 | Two property tests over randomized worlds seeded with `SplitMix64`, following the pattern of `CollisionUniformGridTests.Rebuild_MatchesTheOracleForGeneratedWorldsAcrossFixedSeeds`: no two committed bodies strictly overlap after any resolve including rotations, and the resolver's output is identical under every input permutation of the same world. Plus an explicit worst-case candidate-count test proving termination is bounded | `tests/Hukbo.Core.Tests/CollisionResolverTests.cs` (new region) or a new `CollisionRotationTests.cs` | All three pass, and the invariant test fails if the validation step is deliberately weakened | plan only |
| 10 | Teach the naive oracle the same rotation rule, as an independent transcription. It may not call the resolver, delegate to it, or import its helpers | `tests/Hukbo.Core.Tests/NaiveCollisionResolution.cs` | `Resolve_MatchesTheUnacceleratedAlgorithmOnEverySeededLayout` passes with rotations occurring in the generated worlds, and the oracle's own header says it is independent | plan only |
| 11 | Give the spectator the label. Add the `Rotated` arm to `GetMovementLabel` reading `"Swapping"`, and extend `FormatMovementLineLabelsEveryResolution`'s theory data so the new member is covered rather than silently absent | `src/Hukbo.Client/UI/AgentInspectorContent.cs`, `tests/Hukbo.Client.Tests/AgentInspectorContentTests.cs` | `EveryMovementResolutionHasADistinctSpectatorLabel` passes with six distinct labels for seven members, and the inspector prints `Movement: Swapping` | 2 |

### Wave C — measurement and recapture, integrator only

| # | Task | Files | Done when | Depends on |
| --- | --- | --- | --- | --- |
| 12 | Recapture every moved digest by running a capture, never by editing a literal to agree with the code: the four assertion pairs in `DeterminismTests.cs` at `:243-244`, `:311-312`, `:516-517`, `:604-605`, the preset pins in `CohortLateralSpreadV13Tests.cs:612-613` and `ContingentShapeV12Tests.cs:259-260,279-280`, and the nine `Fixtures/*digest*.json` files. Each superseded literal stays in a comment, per that file's own convention | `tests/Hukbo.Core.Tests/**` | Every new literal is traceable to a pasted run, and no fixture was hand-edited | 5, 7, 8, 10 |
| 13 | Re-run the 200-seed last-stand sweep at radius 4.5 and at the shipping radius, and the threshold-7 and threshold-8 cases the 2026-08-13 measurement found still stalling | none — a `dotnet test --filter` run | The stall count at every threshold is recorded, and thresholds 7 and 8 are zero or the reason they are not is written down | 5 |
| 14 | Fairness: no faction wins disproportionately across seeds 1-20, by the measure the collision-priority fairness work used | none — `benchmark.ps1` runs | The outcome spread is recorded and compared against the pre-change spread | 5 |
| 15 | The agent-count sweep at 200, 500, 1,000, and 2,000, recorded against the figures in the design's section 2. The 500-agent point is the shipping default and must not regress | `docs/development/measurement-history.md` | A before-and-after table covering p50, p95, p99, and max, with `maximumPenetrationRaw` 0 at every point | 5 |
| 16 | The canonical gate, run once, after integration. **Not delegated, and no sub-agent report substitutes for its output** | `docs/development/testing.md` | `./scripts/verify.ps1` exits 0 and its real output is pasted, with all five Hukbo workload digests recorded as the new baseline | 12, 13, 14, 15 |
| 17 | Sync the recorded baseline in the `hukbo-determinism-change` skill from `testing.md`, and add the smoke row this change owes | `.claude/skills/hukbo-determinism-change/SKILL.md`, `docs/development/smoke-checklist.md` | The skill's table matches `testing.md`, and a new `CM-1` row asks a person to watch a jammed melee and report whether warriors visibly exchange places | 16 |

## The nine questions

1. **User-visible outcome.** Agents that would have stood still facing each
   other now exchange places, so a crush resolves instead of setting. At the
   shipping configuration this is rare by construction — the intent-layer escape
   already prevents the reachable stall — and `CM-1` is the row that asks a
   person whether it is visible at all.
2. **Tick stage and state.** The collision stage only. Reads movement proposals
   and tick-start positions; writes committed positions and
   `MovementResolution`. One new enum member, no new state field.
3. **Units and conflict rule.** Raw fixed-point units throughout. The same-tick
   conflict rule is the subject of the change and the mechanism section states
   it: a cycle commits whole or not at all.
4. **Total ordering and random stream.** Component discovery follows the
   existing resolution order, which is the `CollisionPriority` key with the
   entity id in its low 32 bits as the tie-break. No new draw from the random
   stream; `CollisionPriority` consumes none today and that must hold.
5. **Cache.** No cache. The committed and pending grids are rebuilt every tick
   and are not caches.
6. **Save, event, version effect.** Committed positions move, so both hashes
   move and golden expectations are recaptured. No new persisted field and no
   snapshot schema change. This is a recapture rather than a new preset version,
   on the precedent of the collision-priority fairness change, which altered
   collision ordering for the shipped default and re-recorded its oracle without
   cutting a preset.
7. **Complexity and workload.** The third pass is bounded by the held-mover
   count times `MaximumRotationMembers`, with a 3×3 neighbourhood query per
   edge. The benchmark workload is the four-point agent sweep at seed 1 over
   10,000 ticks, plus the 200-seed last-stand test at 4.5.
8. **Spectator explanation.** `MovementResolution.Rotated`, rendered by the
   agent inspector as `Movement: Swapping`. A spectator can select a warrior and
   read why it moved without reading source.
9. **Tests that fail first.** `Resolve_PreventsTwoAgentsFromSwappingPositions`
   and `Resolve_BlocksBothAgentsInAHeadOnApproach`, both of which pin the
   refusal this change removes, plus the new ring case, which has no
   implementation until task 5 lands.

## Results

Filled in as the work lands. Nothing here may be written before the command that
produced it has been run.

### Baseline, before any change

`main` at `a92aeb2`, worktree `collision-mutual-lock`:

```
Release build: exit code 0
Hukbo.Core.Tests: Failed: 0, Passed: 2568, Skipped: 0, Total: 2568, Duration: 27 s
```

### Task 1 — the deadlock probe against today's code, 2026-08-15

Run from the worktree, `Release`, `tools/Hukbo.Tools.DeadlockProbe`, 18 agents.

**The design's own reproduction no longer reproduces.** Seed 12 at body radius
4.5 — the case the design was written around, and the reason
`DefaultBodyRadiusRaw` sits at 4.25 — now ends in a decision:

```
Seed 12, 18 agents, body radius 4.5 world units.
Stopped at tick 739, outcome Faction0Victory, living 1/0, last death at tick 739.
This seed did not reach the tick limit, so there is no stall to classify.
```

Surveying every seed from 1 to 200 at that radius and at the maximum threshold
gives the same answer: **0 of 200 seeds reached the tick limit.** The
intent-layer escape closed the 4.5 case as thoroughly as it closed the 4.25 one,
and the design's section 1 premise — that 4.5 hangs the simulation — is no
longer true of this build. That is a finding this plan owes the reader, because
it means raising the body radius is no longer blocked by this bug, and the
question of what the radius should be is now a tuning decision rather than a
workaround. It is not decided here.

**What does still stall, at the shipping radius of 4.25, over seeds 1 to 200:**

| `LastStandThresholdAgents` | Stalls in 200 | Seeds |
| --- | --- | --- |
| 7 | 2 | 160, 161 |
| 8 | 3 | 95, 157, 177 |
| 9 — the maximum | 0 | — |

This reproduces the 2026-08-13 table exactly, seed for seed, which is the
evidence that the measurement is sound and that nothing has drifted since. The
shipping default is 6 and is unaffected; thresholds 7 and 8 are reachable only
from a test that sets them by hand. **These five seeds are the acceptance
criterion for the whole package.** If they still stall after the rotation pass
lands, the change did not do its job, whatever the unit tests say.

Classifying seed 160 at threshold 7 over a 300-tick window confirms the
mechanism is the one the design describes and not the half-rate column:

```
Blocked agent-ticks with at least one blocker in reach: 2703.
  ... of which at least one pending blocker vacated its ground: 38 (1.4 %).
  ... of which every blocker stayed exactly put: 2633 (97.4 %).
```

Ten of the fourteen surviving agents are blocked between 91 and 99 per cent of
the window's ticks. Tallying the blockers named in those 2,703 records by the
resolution the blocker itself received:

| Blocker's own resolution | Records | Reading |
| --- | --- | --- |
| `Blocked` | 3,470 | A held mover. Rotation can move it |
| `None` | 384 | Proposed no movement at all. Rotation cannot move it |
| `Truncated`, `Moved`, `Slid` | 89 | Actually moved; incidental |

So roughly nine in ten blocker relationships are between two held movers, which
is the population the rotation pass acts on, and one in ten is the stationary
case this package deliberately does not fix and instead makes countable through
task 9.

## What was measured, and what it refutes

This section is the deliverable. Everything above it is the reasoning that led
here, and the task list above it was abandoned deliberately rather than left
half-done.

### The first build: components moving to their own claims

The rotation pass was implemented exactly as "The mechanism, specified"
describes, with an independent transcription of the same rule in the naive
oracle so that two readings could be compared. Both compiled, the unit-level
swap case worked, and the oracle and the implementation agreed across every
seeded layout after one genuine ambiguity was found and settled.

It then fired **zero times** in a real battle. Two thousand ticks of seed 160 at
threshold 7 produced 14,791 `Blocked` agent-ticks and not one rotation. The
temporary counters, quoted in full above, put 14,218 of 14,791 candidates on the
pairwise-claim-overlap check and none anywhere else.

### The second build: cycles moving into vacated ground

The rule was corrected to the design's own words — each member takes the
position the next member vacates — restricted to a simple cycle, since "the next
one" has to be a function. That version does fire. The same 2,000 ticks produced
7,120 `Rotated` agent-ticks across 3,560 committed cycles, and `Blocked` fell
from 14,791 to 7,674.

**It still does not fix the stalls**, and it costs an invariant to get that far.

| `LastStandThresholdAgents` | Stalls in 200, before | Stalls in 200, after |
| --- | --- | --- |
| 7 | 2 — seeds 160, 161 | **4** |
| 8 | 3 — seeds 95, 157, 177 | **1** |
| Total | 5 | 5 |

The count is unchanged. Which seeds hang moved, and threshold 7 got worse. A
change that relocates a hang rather than removing it has not earned a moved
state hash on every seed.

### Why no exchange rule can work here

A held mover's claim overlaps its blocker's *body*, not its blocker's *centre*.
The claim is one movement step, 3 world units. The blocker's centre is at least
one diameter away, 8.5 world units, because committed bodies never overlap. So
"move into the position the blocker vacates" is a jump of nearly three times the
approved step, and `CollisionResolver`'s own header promises that collision may
only reduce displacement. The second build satisfied that promise only by
replacing the check with a triangle-inequality assertion that is true for every
input — which is to say, by deleting it.

The first build's failure says the same thing from the other side. Two warriors
locked against each other both want the contested gap *between* them. Their
claims overlap, so moving both to their claims would leave two bodies inside
each other, and the validation refusing that was correct.

Both facts are the same fact. **The measured stall is competition for one piece
of ground, not a permutation of ground**, and an exchange rule has nothing to
exchange.

### What this corrects in the earlier diagnosis

`docs/research/2026-07-28-COLLISION-DEADLOCK-DIAGNOSIS.md` section 6 says "the
evidence points here" of option 6.4. That inference was drawn from blocker-set
stability — the same agents blocking the same agents, tick after tick, with the
priority draw changing nothing. That observation is correct and reproduces
exactly today.

What nobody had measured was *claim compatibility*: whether the locked movers
want compatible ground. They do not, in 96 per cent of candidates. Blocker-set
stability tells you a lock is mutual; it does not tell you the lock is a cycle
that can be permuted. The diagnosis's own caveat — that agents 3 and 12 each sit
in two locks at once — was the first hint that the structure is a crush rather
than a ring.

### What is left, and what it costs

Option 6.5, sliding along the obstruction, is the only option remaining that can
move a warrior whose neighbour wants the same ground: it does not need the
neighbour to go anywhere. The design already records what it costs — it changes
movement character visibly, so it is a gameplay decision rather than defect
repair, and it does not obviously resolve a symmetric head-on pair where both
tangents point the same way. **It needs its own design document before anyone
builds it**, and this document is not that authorization.

The five stalled seeds remain, at thresholds 7 and 8, unreachable from
`Scenario.CreateDefault` and from the client. The shipping configuration has
stalled zero times in 200 seeds since `b9003a9`, at both 4.25 and 4.5.

### What was run

```
Release build, whole solution: exit code 0
Hukbo.Core.Tests after the revert: Failed: 0, Passed: 2568, Skipped: 0, Total: 2568
```

The canonical gate was not run, and this document does not claim it was. Nothing
reached `src/` in the end, so there is no behaviour to gate: the branch's only
surviving content is this document, every source and test edit was reverted, and
the suite is byte-identical to the baseline recorded at the top of this file.
