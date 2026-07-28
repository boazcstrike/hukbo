# Follower-trailing mutual block in the collision resolver — design

Status: design only. This document does not authorize implementation. It states
a problem, explains its cause, and lays out the options.

**Updated 2026-07-28.** Section 4 was originally a hypothesis reasoned from the
source, and section 9 made proving or refuting it the first task of any plan
that followed. That measurement has since been taken and is recorded in
[`docs/research/2026-07-28-COLLISION-DEADLOCK-DIAGNOSIS.md`](../research/2026-07-28-COLLISION-DEADLOCK-DIAGNOSIS.md).
Section 4 now states what was measured. Sections 6 and 9 are unchanged and still
describe unchosen options and unperformed work; the findings document's section
6 says which way the evidence points and why that is still not a decision.

Date: 2026-07-28. Written against `main` at `ce298fa`.

## 1. Why this document exists

The user asked for a larger collision body. They got 4.25 world units instead of
the 4.5 they asked for, and the reason was not a design decision. It was that
4.5 hangs the simulation.

`CollisionRules.DefaultBodyRadiusRaw` is `(17 * FixedPoint.Scale) / 4`, which is
4.25 world units. The remark on that constant records what happened when 4.5 was
tried: it clears every one of the static validation guards arithmetically, and
`LastStandFormationTests.NoLastStandBattleStallsAtTheTickLimitAcrossSeedsOneThroughTwenty`
still stalls at seed 12 with living counts of nine and nine for 9,976 ticks.
4.25 and 4.125 do not stall. Measured 2026-07-28.

So the shipping body radius is not the radius anybody chose. It is the largest
value that happened not to trigger a bug. That is a bad place for a tuning
constant to sit, and it is the reason to look at the bug rather than at the
constant.

## 2. What the evidence actually says

Two independent measurements point at the same behaviour.

**The stall at 4.5.** Seed 12, eighteen agents left alive in two groups of nine,
no resolution for 9,976 ticks. A battle that cannot kill anybody for that long
is not a battle in which the two sides are evenly matched. It is one in which
they are not reaching each other.

**The cost at 4.25.** The agent-count sweep re-measured today
(`docs/development/testing.md`, "Agent-count scaling sweep re-measured at the
4.25 body radius") found that the 2,000-agent point regressed by a factor of
3.44 at p50 when the radius moved from 4.0 to 4.25 — 5.0435 ms to 17.3454 ms,
with p95 going 16.4739 ms to 51.5116 ms. Both the before and after runs hit the
10,000-tick cap, so this is a like-for-like comparison rather than an artifact
of differing battle lengths.

The collision counters from that run say what the extra quarter of a world unit
bought:

| Counter | 2,000 agents at 4.25 |
| --- | --- |
| `blockedAgentTicks` | 1 943 319 |
| `longestBlockedStreakTicks` | 108 |
| `acceptedMoves` | 13 326 655 |
| `maximumFrontWidthRaw` | 104 460 |
| `faction0Survivors` / `faction1Survivors` | 674 / 678 |

The front never widened past 104,460 raw units. At 200 agents in the same sweep
it reached 621,539. One agent in eight was blocked rather than moving on any
given tick, some for 108 ticks unbroken, and 1,352 of 2,000 agents were still
alive when the tick limit arrived. The 2,000-agent battle at the shipping radius
is a traffic jam that happens to contain a fight.

These are the same phenomenon at two intensities. At 4.5 with eighteen agents it
is a hang. At 4.25 with 2,000 agents it is a threefold cost and a battle that
never finishes. The 2,000-agent case is a stress point rather than a shipping
configuration — the shipping default is now 500 agents in total, merged at
`28d0aab`, and that point is comfortable at 0.2391 ms p50 and reaches a faction
victory in 2,859 ticks. But a bug that is merely expensive at one population and
fatal at another is one bug.

## 3. How the resolver decides, today

`CollisionResolver` (`src/Hukbo.Core/Simulation/CollisionResolver.cs`) commits
one position per living agent per tick, in this order:

1. Stationary bodies first, ascending entity ID. A standing agent would
   otherwise have its ground taken by a mover considered before it.
2. Movers, ascending `CollisionMoveRequest.PriorityKey`.

Each mover then walks a fixed candidate ladder (`CommitMovers`, lines 433-497):
the preferred full-step destination; the two single-axis slides; then the
truncation ladder, which halves the step repeatedly for at most
`MaximumTruncationRungs` = 11 rungs; and finally, if nothing fits, it holds its
tick-start position and is reported as `MovementResolution.Blocked`.

The candidate test is `IsFree` (lines 648-681). A candidate is rejected if it
strictly penetrates either a body already committed this tick, or **a body that
is still pending, measured at that pending body's tick-start position**:

```csharp
for (var moverIndex = pendingFrom; moverIndex < _moverCount; moverIndex++)
{
    var pending = requests[_moverIndices[moverIndex]];

    if (pending.EntityId != entityId &&
        CollisionGeometry.Overlaps(
            xRaw, yRaw, pending.StartXRaw, pending.StartYRaw, _bodyRadiusRaw))
    {
        return false;
    }
}
```

That pending test is deliberate and the class header explains why. Without it, a
mover resolved earlier could step onto ground that a mover resolved later has
not yet vacated; when that later mover then falls through to its hold-position
fallback, it commits an overlap. A head-on approach between two tangent agents
is enough to produce the case. The pending test is what makes the resolver's
output invariant — no two committed bodies strictly overlap — hold
unconditionally rather than usually.

The mover order is not stable across ticks. `CollisionPriority.Resolve`
(`src/Hukbo.Core/Simulation/CollisionPriority.cs`) builds the key as an FNV-1a
mix of a domain tag, the scenario seed, the tick, and the entity ID, keeping the
mix in the high 32 bits and the entity ID in the low 32. It reshuffles every
tick. That was itself a fix: ascending entity ID handed every cross-faction
contest to faction 0, which held the low IDs, and once the mirrored deployment
removed the spawn noise masking it, it decided 19 of 20 seeds.

## 4. The hypothesis

Consider a follower walking behind its leader, both moving the same direction at
the same speed, separated by roughly one body diameter.

If the leader's priority key sorts first, the leader commits its new forward
position, the follower is tested against that committed position, the ground has
genuinely been vacated, and the follower advances. The column moves.

If the follower's key sorts first, the follower is tested against the leader's
**tick-start** position — ground the leader is about to leave but has not left
yet. Every forward candidate on the follower's ladder is measured against a body
that is, geometrically, still directly in front of it. If the two are within one
step of tangency, every rung fails, and the follower holds position and is
reported `Blocked`.

Because the priority reshuffles every tick and is effectively a coin flip
between any two agents, a follower in a tight column advances on roughly half of
all ticks and stands still on the other half.

That alone is a slowdown, not a hang. The hang needs the stronger case. Two
agents at or inside tangency, each of whose preferred destination lies through
the other, cannot move at any rung of the ladder regardless of order — the
minimum useful step is one raw unit and even that penetrates. Both hold. Their
positions do not change, so next tick presents the identical geometry, and the
new priority draw changes nothing because order was never the binding
constraint. That state is stable and self-perpetuating.

Body radius is what decides how often a pair lands in that state. At 4.25 the
diameter is 8.5 world units; at 4.5 it is 9.0. The approved movement speed is
3,072 raw units, three world units per tick, so a larger diameter means agents
converging at that speed reach mutual tangency from further apart and have
proportionally less room to resolve out of it. Nine agents against nine, packed
into whatever formation seed 12 produces, is apparently enough to reach it at
9.0 and not at 8.5.

**This was a hypothesis, and it has now been measured.** Steps 1 and 2 of
section 9 were carried out on 2026-07-28 and are recorded in
[`docs/research/2026-07-28-COLLISION-DEADLOCK-DIAGNOSIS.md`](../research/2026-07-28-COLLISION-DEADLOCK-DIAGNOSIS.md).
The result splits this section in two.

**The mutual-lock mechanism above is confirmed.** In the seed-12 stall, 99.8 %
of blocked agent-ticks have every blocker standing exactly still, each blocked
agent's blocker set never changes across the window, and the blocker records
split 1 348 to 1 348 between "the blocker was pending" and "the blocker was
already committed" — the same pairs counted from both sides, with the priority
draw landing on each side about half the time and changing nothing. Order is
measurably not the binding constraint, exactly as this section predicts for that
case.

**The half-rate column is refuted as the cause of this stall.** It predicts
roughly 50 % blocked and a blocker that vacates; measured blocked rates are 89 %
to 92 %, and a blocker that vacated appears in 5 records out of 2 977. The
mechanism is real but it is a rounding error here.

Two things the reasoning above did not anticipate. Every locked pair is
**intra-faction** — allies, not enemies — so neither faction ever reaches the
other, and there are no deaths in the entire run rather than a fight that ground
down to nine each. And two agents are each locked against two different allies
at once, so the obstruction is a small connected component and not only a set of
independent pairs.

## 5. What any fix must not break

- **The output invariant.** No two committed bodies may strictly overlap, ever,
  for any input. This is the property the pending-at-start test exists to
  guarantee. A fix that relaxes the test without replacing that guarantee is not
  a fix.
- **Termination.** The class header commits to a fixed, bounded candidate list
  per mover, no convergence loop, no iteration count, no wall-clock condition. A
  fix that iterates until positions settle trades a movement deadlock for an
  unbounded tick, which is worse.
- **Priority fairness.** Whatever order is introduced must not restore a
  standing advantage to the faction holding low entity IDs. That regression was
  found once and cost a design document to fix; reintroducing it through a
  dependency ordering would be the obvious way to find it a second time.
- **Total order and determinism.** Any new ordering is a multi-result query and
  needs a total order that ties-breaks on `EntityId`, per `CLAUDE.md` section 5.
- **Zero warm-tick allocation.** All resolver storage is reused and grows only
  on insufficient capacity.

## 6. Options

### 6.1 Do nothing, keep 4.25

The shipping configuration is 500 agents, which measures comfortably, and the
stall was found by a test that exists precisely to catch it. The cost is that
`DefaultBodyRadiusRaw` stays pinned by a bug rather than by a decision, the
2,000-agent point stays three times slower than it was, and the next person who
wants a larger body hits the same wall with the same lack of a diagnosis.

This is the honest baseline and it should be beaten on evidence, not dismissed.

### 6.2 Second resolution pass over blocked movers

After the first pass completes, re-run every mover that resolved to `Blocked`
against the now-final committed positions. A follower that was blocked by its
leader's stale start position now sees the leader's real destination and
advances.

Bounded by construction: exactly two passes, never more. Preserves the output
invariant, because the second pass tests against committed bodies only, which is
the strictly stronger test. Does not touch priority.

Costs: up to one extra `IsFree` walk per blocked mover per tick, which at 1.9
million blocked agent-ticks is not free, and it interacts directly with the
scaling work in section 8. It also does not fix the true mutual-tangency case in
section 4, where neither agent can move on either pass. It fixes the half-rate
column and leaves the hang.

### 6.3 Dependency-ordered resolution

Before resolving, detect that mover B's preferred destination overlaps mover A's
start position, and resolve A first. In the acyclic case this is exactly the
right answer: leaders resolve before followers and columns move at full rate
every tick.

The graph is not guaranteed acyclic — two agents walking into each other are a
two-cycle, and that is the common case in a battle, not an exotic one. So this
needs a cycle rule, and the cycle rule is where the fairness risk lives: any
deterministic tie-break inside a cycle is a rule about who wins contested
ground, which is what `CollisionPriority` exists to keep fair. Building the
dependency edges is also an all-pairs question unless it rides on the same
spatial index the scaling design proposes.

### 6.4 Rotation and swap detection

Detect that a set of mutually blocking movers would each fit in the next one's
vacated position, and commit the whole set atomically. Handles the two-cycle
head-on case and the ring case that section 4 suspects is the actual hang.

The most targeted option and the most intricate. Cycle detection has to be
bounded and deterministic, and an atomic multi-body commit is a new kind of
operation in a resolver whose current commits are all single-body.

### 6.5 Let a blocked mover slide along the obstruction

Rather than holding position, project the refused step onto the tangent of the
blocking body and try that. This is the standard character-controller answer and
it turns a column jam into a flow around the obstruction.

It changes movement character visibly — agents would appear to flow around each
other rather than stack up — which is a gameplay decision, not only a bug fix,
and it needs the historical and design judgment of whether a shield wall should
behave that way. It also does not obviously fix a symmetric head-on pair, where
both tangents point the same way.

**No recommendation is made here.** Options 6.2 and 6.4 address different halves
of the hypothesis, and which half is the real one is exactly what section 9's
first task determines. Choosing before that measurement would be guessing.

## 7. Hash impact

**Every option except 6.1 changes committed positions, and therefore changes
both the state hash and the event hash on every seed.**

That is the opposite requirement from the collision work described in
`docs/plans/2026-07-28-collision-resolution-scaling-design.md`, which is
strictly hash-neutral and whose section 13 puts "anything that changes a
committed position, a preset version, or a golden expectation" out of scope.
These two documents touch the same file and the same method and must not be
conflated. Anyone implementing one should read the other's scope section first.

Consequences, per `CLAUDE.md` section 5:

- New golden expectations for the recorded seed-1 baseline: `stateHash`,
  `eventHash`, `measuredTicks`, outcome.
- The `hukbo-determinism-change` skill's procedure applies in full.
- The moved hash is expected and legitimate here, which is the one situation in
  which a golden expectation may be regenerated — and it may only be regenerated
  after the mechanism is understood, never to make a red test green.

## 8. Interaction with the scaling design

Both documents modify `IsFree`. The scaling design's section 5.2 proposes a
second uniform grid over the pending movers, replacing the linear scan quoted in
section 3 above with a bounded neighbourhood query.

The ordering constraint is real and runs in one direction. That pending scan is
also the thing any second pass in option 6.2 would run again, so doing the
scaling work first makes this work cheaper, while doing this work first makes
the scaling work land on a moved target. The scaling work is additionally
hash-neutral and therefore verifiable by a hash comparison alone, which is a far
cheaper thing to prove correct.

**Recommendation: the scaling work goes first.** This document's work should be
planned assuming the pending index exists.

## 9. What a plan document would have to establish

In order.

1. **Prove or refute the mechanism.** Reproduce the seed-12 stall at 4.5 and
   record, for the stalled ticks, which agents resolved `Blocked`, which body
   each one's ladder was refused against, and whether that body was pending or
   committed. This is a diagnostic run, not a code change; the debug log under
   `docs/plans/2026-07-27-debug-logging-standard-design.md` is the right vehicle
   and a `trc`-level channel is the right level. Until this exists, sections 4
   and 6 are speculation and no option can be chosen honestly.
2. **Establish whether the stall is a half-rate column or a true mutual lock.**
   These need different fixes and step 1 distinguishes them.
3. Choose an option, and state why the others were rejected against the step-1
   evidence rather than against this document's reasoning.
4. Verify the output invariant survives — no two committed bodies strictly
   overlapping — as a property test over randomized inputs, not only over
   scenario runs.
5. Verify termination is still bounded, with an explicit worst-case candidate
   count per mover per tick.
6. Rerun `LastStandFormationTests.NoLastStandBattleStallsAtTheTickLimitAcrossSeedsOneThroughTwenty`
   at 4.5 across every seed, not a sample. That test is the regression lock for
   this whole class of bug.
7. Rerun the full agent-count sweep at 200, 500, 1,000, and 2,000 and compare
   against the table recorded today. The 2,000-agent p50 of 17.3454 ms is the
   number to beat; the 500-agent point is the one that must not regress, because
   it is the shipping default.
8. Verify fairness is intact: no faction wins disproportionately across seeds 1
   through 20, by the same measure the priority-fairness work used.
9. Record new golden expectations, with the moved hashes explained.
10. Only then consider whether `DefaultBodyRadiusRaw` moves to 4.5, and update
    its remark either way. The remark is currently accurate and must not be left
    describing a resolver that no longer exists.

## 10. The nine questions

1. **User-visible outcome.** Formations advance rather than stalling; a battle
   at high agent counts reaches a result instead of timing out with two thirds
   of the field alive. Secondarily, it becomes possible to choose the body
   radius on its merits.
2. **Tick stage and state.** The collision stage only. Reads movement proposals
   and tick-start positions; writes committed positions and
   `MovementResolution`. No new state.
3. **Units and conflict rule.** Raw fixed-point units throughout. The same-tick
   conflict rule is the subject of the change and each option states its own.
4. **Total ordering and random stream.** Any new ordering must be total and tie-
   break on `EntityId`. No option draws from the random stream;
   `CollisionPriority` consumes none today and that must hold.
5. **Cache.** No cache. `CLAUDE.md` section 9 forbids target caches and
   unbounded caches; a spatial index rebuilt each tick, as in the scaling
   design, is not a cache.
6. **Save, event, version effect.** Committed positions move, so the state hash
   and event hash both move and golden expectations are replaced. No new
   persisted field, no snapshot schema change.
7. **Complexity and workload.** Each option states its own worst case. The
   benchmark workload is the four-point sweep at seed 1 over 10,000 ticks, plus
   the twenty-seed last-stand test at 4.5.
8. **Spectator explanation.** `MovementResolution` already carries `Blocked`,
   `Slid`, `Truncated`, `Separated`, and `Moved`, and the agent inspector can
   show it. Any new resolution reason needs a new member and inspector support —
   a spectator must be able to see *why* an agent stood still without reading
   source. Option 6.4's atomic multi-body commit in particular would be
   invisible unless it says so.
9. **Tests that fail first.** The seed-12 last-stand case at 4.5, red before and
   green after; a targeted unit test of whatever geometry step 1 identifies,
   constructed directly rather than reached through a scenario.

## 11. Risks

- **The hypothesis is wrong.** The whole of section 6 is built on section 4.
  Mitigated by making step 1 of section 9 a measurement rather than a change.
- **A fix trades a deadlock for a fairness regression.** Options 6.3 and 6.4
  both introduce an ordering rule, and ordering rules in this resolver have
  already produced one standing faction advantage.
- **A fix trades a deadlock for an unbounded tick.** Any iterate-until-settled
  shape does this. Section 5 forbids it; a reviewer should check for it
  specifically.
- **The hash change hides a second change.** Because every hash moves anyway, a
  second unintended behaviour change would not be caught by the hash comparison
  that normally catches it. The property test in step 4 and the fairness check
  in step 8 exist partly for this reason.
- **Merge conflict with the scaling work.** Same file, same method, opposite
  hash requirements. Section 8's ordering is the mitigation.

## 12. Out of scope

- Pathfinding, steering behaviours, and flocking. `CLAUDE.md` section 9 gates
  pathfinding behind its own acceptance gate and this document does not open it.
- Rigid-body physics, which section 9 forbids outright.
- Terrain and obstacles.
- Formation shape and deployment. This is about the resolver, not about what
  shape the agents are asked to hold.
- The hash-neutral scaling work, which is its own document.
- Raising `DefaultBodyRadiusRaw`, which is a consequence to be considered after
  the fix lands and not a goal of the fix.

## 13. Open questions

1. Is the seed-12 stall a half-rate column, a true mutual lock, or both at
   different moments of the same run?
2. Is 2,000 agents a supported population at all? The scaling design asks the
   same question in its section 14 and neither document can resolve section 6.1
   without an answer.
3. Should `MovementResolution.Blocked` be split so that "blocked by a body that
   moved away" and "blocked by a body that stayed" are distinguishable in the
   inspector? That distinction is exactly the diagnostic step 1 needs, and it
   may be worth keeping permanently.
4. Does the fairness measure used for `CollisionPriority` need to be re-run for
   any option, or only for 6.3 and 6.4?
