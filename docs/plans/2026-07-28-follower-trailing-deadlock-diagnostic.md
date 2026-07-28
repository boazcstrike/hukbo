# Follower-trailing deadlock — diagnostic plan

**Design:** [`2026-07-28-follower-trailing-deadlock-design.md`](2026-07-28-follower-trailing-deadlock-design.md).

**Date:** 2026-07-28. Written against `main` at `a6ca2a8`.

**Scope: steps 1 and 2 of the design's section 9, and nothing else.** This plan
authorizes a measurement. It does not authorize a fix, it does not choose among
the design's options 6.1 through 6.5, and it does not change
`CollisionRules.DefaultBodyRadiusRaw`. The design states plainly that until this
measurement exists, its sections 4 and 6 are speculation and no option can be
chosen honestly. This plan exists to end that condition, and it ends there.

A second plan document will follow once the evidence is in. It will choose an
option against the evidence rather than against the design's reasoning, and it
may only be written after
[`2026-07-28-collision-resolution-scaling.md`](2026-07-28-collision-resolution-scaling.md)
is integrated, per that design's section 8 ordering.

## 1. The obstacle the design did not anticipate

The design's section 9 step 1 nominates the JSON Lines debug log as the vehicle,
at `trc` level, for recording which agents resolved `Blocked` and which body each
one's ladder was refused against.

That vehicle is not available inside the resolver. `CLAUDE.md` section 5 states
that `Hukbo.Core` must never reference `Hukbo.Diagnostics`, and a test asserts
the absence of the assembly reference. The rule's own justification is exactly
this case: the simulation is forbidden the filesystem and the wall clock, and the
logger needs both. The instruction it gives instead is to observe the simulation
from outside, reading state the caller already holds.

So the diagnostic is built as an external observer that reconstructs the
resolver's decision from state it can see, rather than as instrumentation inside
the decision. The reconstruction is exact rather than approximate, and section 3
sets out why.

## 2. What the probe must answer

Two questions, in the design's own words.

**Step 1.** For the stalled ticks: which agents resolved `Blocked`, which body
each one's ladder was refused against, and whether that body was pending or
committed.

**Step 2.** Whether the stall is a half-rate column — a follower refused against
its leader's stale tick-start position, advancing on roughly half of ticks — or a
true mutual lock, where two agents at or inside tangency cannot move at any rung
regardless of resolution order.

These need different fixes, which is the entire reason the measurement comes
first.

## 3. How the reconstruction is exact

The discriminator between the two cases does not require replaying the candidate
ladder. It requires knowing, for each blocked agent, whether the bodies
obstructing it *vacated their ground during that same tick*.

Every input to that question is observable from outside the resolver:

- **Who was blocked.** `AgentState.MovementResolution` is authoritative, written
  by the collision stage, and included in the state hash.
- **Where every body started and finished the tick.** Read the agent positions
  before `AdvanceOneTick` and again after it.
- **Which bodies were pending versus committed for a given mover.**
  `CollisionPriority.Resolve` is a pure function of the domain tag, scenario
  seed, tick, and entity ID, with the entity ID in the low half. The probe
  recomputes the whole resolution order for a tick and knows exactly which
  movers sorted before any given mover.
- **Whether an obstructing body moved.** Its start and end positions, compared.

From those, each blocked agent is classified per tick:

- **Refused against a body that vacated.** The obstruction overlapped at its
  tick-start position and did not overlap at its committed position, and it
  sorted *after* the blocked agent. This is the design's section 4 half-rate
  column, and its signature is that the same pair alternates roles as the
  priority key reshuffles.
- **Refused against a body that stayed.** The obstruction overlaps at both
  positions. If the obstruction was itself `Blocked`, and the relation is
  symmetric, this is the true mutual lock, and its signature is that it persists
  across ticks while the priority order changes underneath it.

The probe emits both classifications per tick and the persistence of each
blocking pair across ticks, which is what separates a jam that clears from one
that does not.

The one thing this reconstruction cannot recover is which specific rung of the
truncation ladder was refused. That is not needed for either question, and the
plan does not pretend to it.

## 4. Task list

### Task 1 — the probe harness

**New:** `tools/Hukbo.Tools.DeadlockProbe/`.

`tools/` is the documented home for hand-run measurement harnesses that are not
in `Hukbo.slnx` and not in the canonical gate. This probe belongs there and must
stay out of both.

**One-line change to `Hukbo.Core`:**
`src/Hukbo.Core/Properties/AssemblyInfo.cs` gains
`InternalsVisibleTo("Hukbo.Tools.DeadlockProbe")`. There is precedent —
`Hukbo.Headless` already holds such a grant for `Fnv1a` — and the change adds no
behaviour, no field, and no hashed state. It is the only line of `Hukbo.Core`
this plan touches, and it must not be bundled into any commit belonging to the
scaling plan.

### Task 2 — reproduce the stall

The scenario needs no source change. `Scenario.BodyRadiusRaw` is `init`-settable
and the last-stand test's configuration is reproducible directly:

```csharp
var scenario = Scenario.CreateDefault(seed: 12, totalAgents: 18) with
{
    LastStandThresholdAgents = FormationRules.MaximumLastStandThresholdAgents,
    BodyRadiusRaw = (9 * FixedPoint.Scale) / 2,
};
```

Confirm before going further that this reproduces what
`CollisionRules.DefaultBodyRadiusRaw`'s remark records: a stall at seed 12 with
living counts of nine and nine, running to the tick limit. If it does not
reproduce, that is itself the finding, and this plan stops and reports it rather
than proceeding to instrument a stall that is not there.

Run seeds 1 to 20 at 4.5 as well, so the report states how many seeds stall
rather than assuming seed 12 is the only one.

### Task 3 — classify

Per tick, over the stalled window, produce the classification in section 3:
blocked agents, their obstructions, whether each obstruction vacated, whether it
sorted before or after, and whether the pair persists into the next tick.

Write it as JSON Lines under `artifacts/`, one object per line, so the output is
machine-readable. The probe is outside `Hukbo.Core` and may use
`Hukbo.Diagnostics` for this.

### Task 4 — the verdict

**File:** `docs/research/` — a new findings document, named on the day it is
written.

State which of the two mechanisms the evidence supports, with the numbers behind
it. If the evidence supports both at different moments of the same run — which
the design's section 13 open question 1 explicitly allows — say that, and say
which dominates the stalled window.

State plainly if the evidence refutes the design's section 4 hypothesis. The
design's section 11 names "the hypothesis is wrong" as its first risk, and the
whole of its section 6 is built on section 4; a refutation is a valuable result,
not a failed task.

### Task 5 — feed back into the design

Amend the design's section 4 to record the outcome, replacing "this is a
hypothesis" with what was measured, and cite the findings document. Leave the
options in section 6 unchosen: choosing is the next plan's job, after the
scaling work lands.

## 4a. Outcome

All five tasks are done. The findings are in
[`docs/research/2026-07-28-COLLISION-DEADLOCK-DIAGNOSIS.md`](../research/2026-07-28-COLLISION-DEADLOCK-DIAGNOSIS.md)
and the design's section 4 has been amended to record them.

The verdict: the stall is a **true mutual lock**, symmetric, between **allies
rather than enemies**, and the resolution order is not the binding constraint.
The design's mutual-tangency mechanism is confirmed; its half-rate column is
refuted as the cause of this stall, appearing in 5 blocker records out of 2 977.

Three things the design did not anticipate, all recorded in the findings:

1. There are no deaths at all in the stalled run. The nine-and-nine living count
   is the starting roster, not the survivors of a fight.
2. Every locked pair is intra-faction. Neither side ever reaches the other.
3. Two agents are each locked against two different allies simultaneously, so
   the obstruction is a connected component rather than a set of independent
   pairs — which constrains option 6.4 more than the design assumed.

One case that none of the design's five options addresses surfaced: an agent
permanently walled by an ally that resolves `None` every tick, having proposed
no movement at all. That is not an ordering problem and not a swap.

No option was chosen, no resolver behaviour changed, and no hash moved.

## 5. What this plan will not do

- Change `CollisionResolver` behaviour in any way.
- Change `CollisionRules.DefaultBodyRadiusRaw`.
- Choose among options 6.1 through 6.5.
- Regenerate any golden expectation. This plan moves no hash, because it changes
  no simulation behaviour. If a hash moves, something in task 1 was not as
  behaviour-neutral as claimed and that is a defect.
- Add the probe to `Hukbo.slnx` or to the canonical gate.

## 6. Verification

The canonical gate, `./scripts/verify.ps1`, with real output pasted, confirming
that the `InternalsVisibleTo` line moved nothing. That is the whole verification
burden for this plan, because the plan's product is a document, not a behaviour.

The probe's own output is evidence for the findings document, not evidence of
correctness of the tree.
