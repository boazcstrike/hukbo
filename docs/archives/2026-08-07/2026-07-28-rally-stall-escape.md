# Rally stall escape — plan

> **Archived: reference only.** This document is finished work, kept so the
> decision can be traced back to its reasoning. Do not execute it and do not
> cite it as the reason to change anything.

**Evidence:** [`docs/research/2026-07-28-COLLISION-DEADLOCK-DIAGNOSIS.md`](../../research/2026-07-28-COLLISION-DEADLOCK-DIAGNOSIS.md).
**Design context:** [`2026-07-28-follower-trailing-deadlock-design.md`](../../plans/2026-07-28-follower-trailing-deadlock-design.md).

**Date:** 2026-07-28. Written against `main` at `9a00e38`.

## 1. What is being fixed, and where

The measured cause: the last-stand rally gives each follower an independently
drawn jitter offset, nothing guarantees two allies' aim points are compatible,
and an agent whose aim point lies beyond an ally walks to exact tangency and
then pushes forever. The resolver correctly refuses every rung. Roughly one seed
in a hundred never terminates, **at the shipping radius and the shipping
threshold**.

The fix goes in the intent layer, not the resolver, for the reason the evidence
gives: a resolver-only fix would leave agents chasing unsatisfiable aim points,
trading a frozen jam for a perpetual shuffle. The repository already fixed this
family of problem in the intent layer once, with the give-way corridor.

## 2. Two things that already exist and shape the design

**An arrived-guard already exists.** `BuildRegroupingProposal` returns `null`
when a follower is already within `ContactSquaredDistance` of its aim point. The
stalled agents are not near their aim points — they are a diameter short of them
with an ally in between — so it never fires. A proximity-based arrival rule is
therefore not the fix; that idea was checked against the source and dropped.

**A per-agent blocked streak already exists.** `CollisionScratch.RecordBlocked`
maintains `_blockedStreakTicks` per agent and today feeds only the
`LongestBlockedStreakTicks` metric. The fix reads it rather than adding state.

**`RallyOffset` deliberately excludes the tick**, and its remarks explain why: a
tick-keyed offset makes every follower chase a fleeing goalpost and nothing ever
settles. Any fix that re-rolls an aim point must therefore re-roll it *rarely*
and only on proof that the current one is unreachable — never per tick.

## 3. The change

Give the rally offset a **generation**, derived from the agent's own blocked
streak:

```
generation = blockedStreakTicks / StallEscapeStreakTicks
```

Generation `0` reproduces today's offset exactly. An agent blocked for
`StallEscapeStreakTicks` consecutive ticks moves to generation 1 and draws a
different aim point, which breaks a mutual lock because the two locked agents
have different entity IDs and so draw different new offsets. Still stuck at
twice the threshold, it moves to generation 2, and so on.

This is "if you cannot reach your place in the formation, take a different
place", which is what a real formation does, and it attacks the cause — the
unsatisfiable demand — rather than the resolver's correct refusal of it.

### `StallEscapeStreakTicks = 192`

Chosen to sit above every healthy observation, so that the escape valve cannot
fire in a battle that is merely crowded:

| Source | Longest blocked streak |
| --- | --- |
| Seed 1, 200 agents | 88 |
| Seed 1, 500 agents | 87 |
| Seed 1, 2 000 agents | 108 |
| Seed 1, 1 000 agents | 111 |
| Last-stand test's asserted bound, seeds 1-20 | 125 (provisional) |

192 is 1.73x the largest observed streak in a shipped workload and 1.54x the
repository's own asserted bound. Against a stall that otherwise runs to a
10 000-tick limit, waiting 192 ticks costs nothing.

**The intended consequence: every recorded hash stays unchanged.** No workload
in `docs/development/testing.md` reaches a 192-tick streak, so generation stays
0 throughout and behaviour is bit-identical. This fix is not hash-neutral *by
construction* — it changes behaviour in stalled battles by design — but it is
hash-neutral *on every recorded workload*, and that is the acceptance criterion
in task 6. If a recorded hash moves, either the threshold is too low or the
derivation is wrong.

### Deliberately not stored

The generation is derived from the existing streak, not stored. An agent that
escapes has its streak reset to 0 by `RecordBlocked`, so its generation returns
to 0 and it resumes its original aim point. That can in principle re-lock and
oscillate on a 192-tick cycle. It is left derived rather than made monotonic
because a monotonic counter is new cross-tick gameplay state, which raises a
state-hash question this plan would rather not answer speculatively. **Task 5
measures whether oscillation actually happens.** If any seed still fails to
terminate, the counter becomes monotonic and that decision is made against
evidence.

## 4. Task list

### Task 1 — `RallyOffset` takes a generation
`src/Hukbo.Core/Simulation/RallyOffset.cs`. Add `int generation = 0`, mixed into
the FNV-1a key alongside the tag, seed, and entity ID. The default keeps the
existing call sites honest: generation 0 is the normal case, and the parameter
exists for the escape. Document that the tick is still excluded and why the
generation is not a back door to the fleeing-goalpost failure.

### Task 2 — expose the streak
`src/Hukbo.Core/Simulation/CollisionScratch.cs`. Add a reader for one agent's
current blocked streak. It is already maintained; only the accessor is new.

### Task 3 — the threshold and the derivation
`src/Hukbo.Core/Simulation/FormationRules.cs`. Add `StallEscapeStreakTicks` and
`ComputeStallGeneration(int blockedStreakTicks)`, with the table above recorded
as the justification and the value marked provisional, per the repository's rule
about tuning values.

### Task 4 — use it
`src/Hukbo.Core/Simulation/BattleSimulation.cs`. `BuildRegroupingProposal` takes
the agent's index, reads the streak, computes the generation, and passes it to
`RallyOffset.Compute`. Nothing else in the method changes.

### Task 5 — measure
`tools/Hukbo.Tools.DeadlockProbe`, 200 seeds, at both radii and at thresholds 6
through 9 — the same eight cells already measured, so the before-and-after is
like-for-like. **Acceptance: zero stalls in all eight cells.** Any residual
stall is reported with its seed rather than explained away, and triggers the
monotonic-counter decision in section 3.

### Task 6 — confirm the recorded hashes did not move
The four-point sweep, seed 1, plus the canonical gate. Every state hash and
event hash must equal the value recorded in `docs/development/testing.md`.

### Task 7 — raise the body radius to 4.5
`src/Hukbo.Core/Simulation/CollisionRules.cs`. The evidence shows the radius was
never the cause, so 4.25 is no longer forced. Its remark must be rewritten: it
currently records 4.5 as unsafe, which is true only in the sense that 4.25 is
equally unsafe.

**This moves every hash, legitimately, and this is the one regeneration in this
plan.** It is authorized because the mechanism is understood, not because a test
went red. New goldens are recorded with the change that produced them.

### Task 8 — widen the last-stand seed range
`tests/Hukbo.Core.Tests/LastStandFormationTests.cs`. Twenty seeds cannot detect
a one-percent failure; the test passed by luck. Widen
`NoLastStandBattleStallsAtTheTickLimitAcrossSeedsOneThroughTwoHundred` to 200 seeds
and rename it accordingly. Report the runtime cost; if it is unacceptable for
the gate, say so and propose a split rather than quietly keeping 20.

### Task 9 — the canonical gate
`./scripts/verify.ps1` once, after integration, real output pasted.

## 4a. Results — the escape works and is not sufficient

**Task 5 did not meet its acceptance criterion.** It required zero stalls in all
eight cells. Measured, over 200 seeds per cell:

| Threshold | Radius 4.25 before | after | Radius 4.5 before | after |
| --- | --- | --- | --- | --- |
| 6 (shipping) | 2 | **1** (seed 139) | 1 | **0** |
| 7 | 2 | **1** (seed 108) | 6 | **1** (seed 129) |
| 8 | 5 | **0** | 5 | **3** (seeds 71, 158, 177) |
| 9 | 1 | **0** | 2 | **0** |
| **Total** | **10** | **2** | **14** | **4** |

24 stalls become 6, a 75 % reduction, and the two cells the last-stand
regression test actually exercises — threshold 9, both radii — go to zero. The
shipping cell goes from 2 to 1. It is a real improvement and it is not a fix.

**Task 6 passed.** All four recorded state hashes and event hashes at seed 1 are
unchanged, with tick counts and outcomes intact. The escape is inert in every
recorded workload, as designed.

### Why the remaining six survive

The probe was extended to record agent intent. In seed 139, six of the eight
stalled agents are `Regrouping` and two — one per faction — are **`Moving`**,
advancing on an enemy target. `BuildMovementProposal` has no escape at all, so
those two push into a comrade forever and anchor the jam that the other six are
caught in. No amount of rally-offset re-rolling helps, because the agents
holding the knot are not using a rally offset.

This also settles something about the deferred work in section 6: **deterministic
non-conflicting rally slots would not have fixed these either**, for the same
reason. The remaining cause is on the attack-approach path, not the rally path.

### Two things tried and rejected, recorded so they are not retried

**A generation derived from the current streak, not stored.** An agent that
escaped returned to generation 0 and its original aim point, walked back into
the same ally, and re-locked. 24 stalls became 10. Replaced by a monotonic
counter, which took it to 6.

**A net-pressure trigger — a leaky bucket rising on a blocked tick and draining
on a moving one.** Motivated by the observation that stalled agents are blocked
on 96 % to 99 % of ticks rather than 100 %, so a consecutive-run counter rarely
fills. It detected **no** additional stall — 6 either way — and it fired in
healthy battles at 500 and 1 000 agents, moving two recorded hashes and flipping
the 1 000-agent outcome from `Faction1Victory` to `Faction0Victory`. The 192
margin is derived from consecutive runs and does not transfer to a net measure.
Reverted.

### Tasks 7 and 8 are deliberately not done yet

Task 7 raises the body radius and regenerates every golden. Task 8 widens the
last-stand seed range. Both are now measurably safe — threshold 9 is at zero
stalls over 200 seeds at both radii, so the widened test would pass at 4.5 — but
doing them before the `Moving`-path gap is closed means regenerating every
golden twice. They wait for one regold at the end.

## 5. What stops this plan

- Task 5 finding any residual stall — that is a finding, not a failure to hide.
- Task 6 finding a moved hash *before* task 7. That means the escape fired in a
  healthy battle and the threshold is wrong.
- The widened test in task 8 failing on a seed outside 1-20 after the fix.

## 6. Out of scope

Deterministic non-conflicting rally slots — "Part 1" in the discussion that
produced this plan. It removes the conflict by construction rather than escaping
it after the fact, and it is the better long-term answer. It is deferred until
task 5 says whether the escape valve alone is sufficient, which is the whole
reason the cheap fix goes first.
