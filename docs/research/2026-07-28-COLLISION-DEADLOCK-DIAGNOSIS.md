# What actually stalls seed 12 at a 4.5 body radius

**Date:** 2026-07-28. Measured on `main` at `a6ca2a8` plus the collision scaling
work, with `Hukbo.Tools.DeadlockProbe`.

**What this settles.** Step 1 and step 2 of section 9 of the design titled
"Follower-trailing mutual block in the collision resolver — design", under the
follower-trailing deadlock diagnostic plan. That design has since been archived:
its stall was closed in the intent layer by `b9003a9`, not by any of its five
options, and a 2026-08-13 re-measurement found zero stalls at the shipping
configuration.
Until now that design's sections 4 and 6 were explicitly labelled hypothesis and
no fix could be chosen honestly. They can be now.

**The short answer.** The stall is a **true mutual lock**, not a half-rate
column. It is **symmetric**, it is **between allies rather than enemies**, and
the resolution order is **not** the binding constraint. The design's second
mechanism is confirmed and its first is refuted as the cause of this stall.

## 1. How the measurement was taken

`tools/Hukbo.Tools.DeadlockProbe` reconstructs the resolver's decision from
outside the simulation. The design nominated the JSON Lines debug log at `trc`
level as the vehicle; that is not available, because `Hukbo.Core` may never
reference `Hukbo.Diagnostics` and the rule's own instruction is to observe from
outside instead. Every input to the reconstruction is observable:
`AgentView.MovementResolution` is authoritative and hashed, tick-start and
tick-end positions are read before and after `AdvanceOneTick`, and the pending
versus committed split follows from `CollisionPriority.Resolve`, a pure function
of seed, tick, and entity ID.

One quantity is recovered as a bounded set rather than as a single body. The
resolver knows which body refused which rung; from outside, what is exactly
recoverable is the set of bodies geometrically capable of refusing any candidate
on the ladder. Every candidate lies within one movement step of the agent's
tick-start position, and a refusal needs a centre distance below one diameter,
so a body further than `diameter + movementSpeed` from that start cannot have
been involved. That is a bound, not an estimate, and it is a superset of the
true answer. It turns out not to matter: in this stall the set has one or two
members.

## 2. The stall reproduces exactly as recorded

Seeds 1 to 20, eighteen agents, `LastStandThresholdAgents` at the maximum, body
radius 4.5 world units:

| Seeds | Result |
| --- | --- |
| 1-11, 13-20 | Terminal outcome between tick 762 and tick 905 |
| 12 | Reached the 10 000-tick limit, `Draw`, nine alive on each side |

One seed of twenty stalls, which is what the remark on
`CollisionRules.DefaultBodyRadiusRaw` records.

**The stall is present from the first tick.** There are no deaths in the entire
run — the living counts are nine and nine because that is how the battle
started, not because it fought down to that. The design's section 2 reads the
9/9 figure as "a battle in which the two sides are not reaching each other", and
that reading is right, but it is stronger than it appears: they never reach each
other at all.

## 3. The classification

Ticks 1 to 300, all eighteen agents.

| Measure | Value |
| --- | --- |
| Blocked agent-ticks with at least one blocker in reach | 2 439 |
| ... where at least one pending blocker vacated its ground | 2 (0.1 %) |
| ... where every blocker stayed exactly put | 2 435 (99.8 %) |

Nine of the eighteen agents are blocked on 89 % to 92 % of ticks. The other nine
are never blocked once. Every blocked agent has exactly **one** distinct blocker
set across the whole 300-tick window: the thing obstructing it never changes.

Breaking the 2 977 individual blocker records down by whether the blocker was
pending or committed, whether it moved, and how it resolved:

| Classification | Records |
| --- | --- |
| `committed` / stayed / `Blocked` | 1 348 |
| `pending` / stayed / `Blocked` | 1 348 |
| `committed` / stayed / `None` | 276 |
| `committed` / vacated / `Truncated` | 3 |
| `pending` / vacated / `Truncated` | 2 |

## 4. Why this is a mutual lock and not a column

**The 1 348 / 1 348 split is the finding.** Those are the same pairs counted
from both sides. Whenever A is blocked by B, B is blocked by A on the same tick,
and which of the two counts as "pending" and which as "committed" simply flips
with the priority draw. The priority key reshuffles every tick, and across 300
ticks it lands on each side of every pair almost exactly half the time — and the
outcome is identical either way.

That is precisely the design's section 4 prediction for a true mutual lock:
*"Both hold. Their positions do not change, so next tick presents the identical
geometry, and the new priority draw changes nothing because order was never the
binding constraint."* Order is measurably not the binding constraint here.

The half-rate column mechanism predicts the opposite pattern — a follower
refused against a leader's stale start position, where the leader does vacate
and the follower advances whenever the draw favours it, giving roughly 50 %
blocked. Blocked rates are 89 % to 92 %, and blockers that vacated appear in
5 records out of 2 977. The mechanism is present but it is a rounding error in
this stall, not its cause.

The remaining 276 records are a different, simpler case: agent 5 is walled by
agent 2, which resolves `None` every tick — an agent with no movement proposal
at all, standing still and permanently occupying the ground agent 5 wants. No
ordering rule can help there either.

## 5. The locked pairs, and the part the design did not predict

| Pair | Factions | Ticks blocked |
| --- | --- | --- |
| 17 ↔ 16 | 1 ↔ 1 | 276 / 275 |
| 3 ↔ 1 | 0 ↔ 0 | 269 / 269 |
| 3 ↔ 6 | 0 ↔ 0 | 269 / 270 |
| 12 ↔ 10 | 1 ↔ 1 | 269 / 267 |
| 12 ↔ 15 | 1 ↔ 1 | 269 / 268 |
| 5 → 2 | 0 → 0 | 276 (one-way; 2 never proposes) |

**Every locked pair is intra-faction.** Not one is a warrior against an enemy.
The design's section 4 reasons about "a follower walking behind its leader",
which is an ally relationship, so this is consistent with it — but the design's
section 2 frames the whole problem as two sides failing to reach each other, and
its option 6.3 worries about a dependency ordering handing "every cross-faction
contest" to one side. The contests that matter here are not cross-faction at
all. Both factions independently tie themselves in a knot inside their own
last-stand cluster and neither ever arrives at the other.

Agents 3 and 12 are each locked against two different allies simultaneously,
so the obstruction is not only pairwise: it is a small connected structure, and
any fix framed purely as a two-body swap has to cope with a three-body case on
the very seed it is meant to fix.

## 5a. The locked pairs are at exact tangency, and that is the whole mechanism

Every mutually locked pair sits at a centre distance of **exactly 9 216 raw
units — one body diameter at radius 4.5 — with minimum equal to maximum across
all 269 sampled ticks.** They are not drifting, not oscillating, not settling.
They are frozen at tangency.

That closes the mechanism. Tangency is a legal resting position by deliberate
design: the resolver refuses a candidate only on *strict* penetration, which is
what lets a packed front settle instead of jittering by one raw unit forever. So
two agents at exact tangency who each want to close further are each asking for
a position one raw unit inside the other, every rung of the ladder is refused,
and both correctly hold. **The resolver is behaving exactly as specified. It is
not the defect.**

The defect is upstream: something keeps asking these agents to walk into ground
that is permanently occupied.

## 5b. This is not a 4.5 problem. The shipping configuration has it too

> **This section's headline claim is no longer true, as of `b9003a9` on
> 2026-07-28.** The table below was measured before that commit, which lets a
> rally follower give up an unreachable aim point after 192 blocked ticks.
> Re-measured on 2026-08-13 against current code, same 200 seeds, same 18 agents,
> same shipping body radius: threshold 6 gives **0 stalls in 200**, threshold 7
> gives 2 (seeds 160, 161), threshold 8 gives 3 (seeds 95, 157, 177), and
> threshold 9 gives **0**. The shipping default is 6, so the sentence "the
> shipped game deadlocks on roughly one seed in a hundred" describes the code as
> it was, not as it is. Consequences 1 and 2 below fall with it; consequence 3,
> that seeds 1 to 20 cannot detect a one-percent failure, still stands and is why
> the regression test now runs 200.
>
> **The 4.5 column was re-measured on 2026-08-16, and it does not vanish.** Same
> probe, same 200 seeds, same 18 agents: threshold 6 gives 1 stall in 200 (seed
> 166), threshold 7 gives 0, threshold 8 gives 2 (seeds 21 and 153), and
> threshold 9 gives 0. Summed over the four thresholds that is 3 stalls for 4.5
> against 5 for 4.25, and 4.5 is the worse of the two at the shipping threshold
> of 6, where it stalls one seed and 4.25 stalls none. This section's own thesis
> survives its numbers: the radius re-rolls which seeds are unlucky rather than
> making the packing safer.

The rally jitter target is drawn from a span of `8 * BodyRadiusRaw + 1`, so the
body radius is the *modulus* of the draw. Changing it does not make the packing
geometrically tighter or looser — it re-rolls every agent's target. That
predicts 4.25 is not safer than 4.5, merely differently unlucky, and that seeds
1 to 20 are too small a sample to establish anything about either.

Surveyed over 200 seeds at every combination of body radius and last-stand
threshold:

| `LastStandThresholdAgents` | Radius 4.25 (shipping) | Radius 4.5 |
| --- | --- | --- |
| 6 — the shipping default | **2 / 200** (seeds 86, 139) | 1 / 200 (seed 65) |
| 7 | 2 / 200 (seeds 108, 118) | 6 / 200 |
| 8 | 5 / 200 | 5 / 200 |
| 9 — `MaximumLastStandThresholdAgents` | 1 / 200 (seed 39) | 2 / 200 (seeds 12, 99) |

**The shipped game deadlocks on roughly one seed in a hundred.** Neither the
radius nor the threshold changes that in any consistent direction; both merely
decide which seeds are the unlucky ones.

Three consequences follow, and they matter more than the original question did:

1. **`DefaultBodyRadiusRaw` is pinned at 4.25 by a bug that 4.25 does not
   avoid.** The remark on that constant is accurate about what was observed and
   wrong in what it implies. Seed 39 stalls at 4.25 at the same threshold where
   seed 12 stalls at 4.5.
2. **"Do nothing and keep 4.25" is not a safe baseline.** The design's option
   6.1 rests on the stall being confined to a radius the game does not ship. It
   is not.
3. **Seeds 1 to 20 cannot detect a one-percent failure.** The last-stand
   regression test passes at the shipping radius by luck, not by construction.
   Whatever fix is chosen, that test's seed range is itself a finding.

## 6. What this means for the options

> **Option 6.4 was built on 2026-08-15, and this section's reading of it is
> refuted.** Rotation and swap detection was implemented twice, in two
> independent transcriptions of the rule. In the first form, where every member
> of a component moves to its own claim, it fired zero times in 2,000 ticks of a
> real stalled battle: 14,218 of 14,791 candidates were rejected because two
> members' claims overlapped each other. In the second form, where each member
> takes the ground the next one vacates, it committed 3,560 rotations and left
> the stall count exactly where it found it — five seeds before, five after,
> with threshold 7 going from two stalls to four. Every source and test change
> was reverted and no code shipped.
>
> The bullet below reasoned from blocker-set stability without claim
> compatibility ever having been measured, and that is the step that does not
> hold. A blocker's centre is at least one body diameter away, because committed
> bodies never overlap, while a claim is one movement step, so taking the vacated
> ground is a jump of nearly three times the approved step and the resolver's own
> displacement budget forbids it. Two locked warriors want the contested gap
> *between* them, so their claims are mutually illegal. The stall is competition
> for one piece of ground, not a permutation of ground, and an exchange rule has
> nothing to exchange.
>
> **6.5 is the only remaining option that can move a warrior whose neighbour
> wants the same ground, and it needs a design document of its own** — it changes
> movement character in every battle and moves both hashes on every seed. The
> plan titled "The collision mutual lock — plan" holds the counters; it and the
> design were archived on 2026-08-16.

This section states consequences, not a choice. Choosing is the next plan's job,
after the scaling work is integrated, per the design's section 8.

- **6.2, a second resolution pass over blocked movers.** The evidence is against
  it as a fix. It repairs the case where a blocker vacated, which is 5 records
  out of 2 977 here. The design already said it "fixes the half-rate column and
  leaves the hang"; the measurement says this stall is almost entirely the hang.
- **6.3, dependency-ordered resolution.** The evidence is against it. The
  dependency graph in this stall is symmetric — every edge has a matching
  reverse edge — so it is all cycles and no acyclic part, and the cycle rule
  would be doing all of the work. The design predicted the two-cycle would be
  common; it is not merely common here, it is everything.
- **6.4, rotation and swap detection.** The evidence points here, with a caveat
  the design did not anticipate: agents 3 and 12 each participate in two locks
  at once, so the cycle detection has to handle a component larger than a pair.
- **6.5, sliding along the obstruction.** Still viable and now more attractive
  than it looked, because the obstructions are allies rather than enemies, so
  flowing around one is not a shield wall opening a gap to the enemy. It remains
  a gameplay decision as well as a bug fix.
- **6.1, do nothing.** Still the honest baseline. The shipping default of 500
  agents at radius 4.25 is unaffected by any of this.

One case none of the five options addresses: agent 5 blocked by a stationary
agent 2 that never proposes a move. That is not a resolution-order problem and
not a swap; it is one agent's destination being permanently owned by another
agent that has no reason to leave. A complete fix has to say something about it.

## 7. Reproducing this

```powershell
dotnet build src/Hukbo.Core/Hukbo.Core.csproj -c Release
dotnet build tools/Hukbo.Tools.DeadlockProbe/Hukbo.Tools.DeadlockProbe.csproj -c Release

dotnet run --project tools/Hukbo.Tools.DeadlockProbe/Hukbo.Tools.DeadlockProbe.csproj `
  -c Release --no-build -- --survey

# The 200-seed survey behind section 5b, for one cell of the table.
dotnet run --project tools/Hukbo.Tools.DeadlockProbe/Hukbo.Tools.DeadlockProbe.csproj `
  -c Release --no-build -- --survey --first-seed 1 --last-seed 200 `
  --radius-raw 4352 --threshold 6

dotnet run --project tools/Hukbo.Tools.DeadlockProbe/Hukbo.Tools.DeadlockProbe.csproj `
  -c Release --no-build -- --seed 12 --window 300
```

The second command writes `artifacts/deadlock-probe-seed12.jsonl`, one object
per blocked agent per tick, carrying the blocker list with each blocker's
pending-or-committed status, whether it vacated, and how it resolved.

## 8. What was not measured

- Which specific rung of the truncation ladder was refused. Not recoverable from
  outside and not needed for either question, as section 1 explains.
- Anything at radius 4.25, the shipping value. This diagnosis is about 4.5.
- Whether a fix would work. No fix exists yet and none is proposed here.
