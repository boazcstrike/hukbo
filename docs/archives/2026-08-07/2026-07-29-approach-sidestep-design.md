# Approach sidestep for a pursuing warrior blocked by a comrade — design

> **Archived: reference only.** This document is finished work, kept so the
> decision can be traced back to its reasoning. Do not execute it and do not
> cite it as the reason to change anything.

Status: implemented and measured. The results are in section 10, and they do not
meet the acceptance criterion the plan document set: thirteen stalls became five
rather than zero. Section 10.1 establishes why, and the reason lies in the
escape's trigger rather than in anything this document designed.

## 1. Why this document exists

`docs/archives/2026-08-07/2026-07-28-rally-stall-escape.md` fixed one half of a deadlock and
left the other half standing. This document designs the second half, and it
exists as its own document because the first one is already shipped and its
reasoning should not be edited after the fact.

The rally stall escape gives a warrior whose intent is `Regrouping` a way out of
a permanent mutual block: after
`FormationRules.StallEscapeStreakTicks` consecutive blocked ticks the agent draws
a different rally aim point, keyed on its stall generation. That escape lives
entirely inside `BattleSimulation.BuildRegroupingProposal`. A warrior whose
intent is `Moving` — walking at an enemy — never reaches that method and has no
escape of any kind.

## 2. What the evidence says

Measured on `main` at commit `a47219e`, body radius 4.25 world units (4352 raw),
18 agents, through `tools/Hukbo.Tools.DeadlockProbe`.

Battles reaching the 10 000-tick limit out of 200 seeds:

| `LastStandThresholdAgents` | stalls / 200 |
| --- | --- |
| 6 — the shipping default | 0 |
| 7 | 5 |
| 8 | 8 |
| 9 — `FormationRules.MaximumLastStandThresholdAgents` | 0 |

Nothing that ships is broken: the shipping threshold is clean, and so is the
maximum, which is what
`LastStandFormationTests.NoLastStandBattleStallsAtTheTickLimitAcrossSeedsOneThroughTwoHundred`
runs at. The failing band sits between the two, in thresholds no test exercises.

The shape of that band is itself the first piece of evidence. A stall does not
appear because a faction is in last stand; at threshold 9 both factions are in
last stand from tick zero and nothing stalls. It appears when a faction *enters*
last stand partway through a battle, which is exactly when a cluster of warriors
is asked to re-form around a rally point while other warriors are still walking
at enemies through the same ground.

Classifying seed 16 at threshold 8 over the 200 ticks after the last death:

```
Stopped at tick 10000, outcome Draw, living 8/8, last death at tick 200.
Blocked agent-ticks with at least one blocker in reach: 1846.
  ... of which at least one pending blocker vacated its ground: 32 (1.7 %).
  ... of which every blocker stayed exactly put: 1793 (97.1 %).
```

This is a true mutual lock rather than a half-rate column: in 97.1 per cent of
blocked agent-ticks every blocking body stayed exactly where it was, so no
amount of waiting changes the geometry. The last death is at tick 200 and the
battle then runs 9 800 further ticks with no casualty at all.

Splitting those blocked agent-ticks by the blocked agent's intent:

| intent | blocked agent-ticks | agents |
| --- | --- | --- |
| `Regrouping` | 1460 | 10 |
| `Moving` | 386 | 2 |

The two `Moving` agents, entities 1 and 10, are blocked on 190 and 196 of the
200 ticks in the window. They are pursuing an enemy, they are refused by a
comrade's body every tick, and the escape that would free them applies only to
the other intent. Ten agents have a way out and two do not, and two is enough
to hold a battle open forever, because the warriors behind them cannot pass
either.

## 3. What the fix must not be

The decision not to fix this in the collision resolver is already recorded and
is not reopened here. The resolver correctly refuses to let two bodies overlap;
a resolver-side fix would let warriors interpenetrate, or would let them reach
aim points that remain unsatisfiable, so the pair would simply stall again on
the next tick at a slightly different position.

Two intent-layer approaches were tried during the rally work and rejected on
measurement. Both are recorded in comments in `FormationRules` and
`CollisionScratch` and neither is retried here: a generation derived from the
live blocked streak rather than from a latched counter only halved the stalls,
and a leaky-bucket net-pressure trigger detected no additional stall while
moving two recorded hashes and flipping the 1 000-agent outcome.

## 4. The design

When a warrior whose intent is `Moving` takes the ordinary pursuit branch and
has been blocked long enough to have a non-zero stall generation, its aim point
is offset laterally — perpendicular to its own line of approach — by a
deterministic displacement keyed on the seed, its entity id, and that
generation. It still walks at its enemy; it walks at a point beside its enemy
instead of at its enemy's centre, which puts it on a different line and takes it
out from behind the comrade that was refusing it.

This is the same shape as the rally escape, and deliberately so. The rally
escape redraws a rally offset; this redraws an approach offset. Both are keyed
on `(seed, entityId, generation)`, both are inert at generation 0, and both
leave the collision resolver untouched.

### 4.1 Where it attaches

`BattleSimulation`'s movement-proposal loop dispatches a living `Moving` agent
down one of two branches: a contingent cohesion aim point when a
persistent-contingent preset resolves one, and otherwise ordinary pursuit
through `BuildMovementProposal(agent, target)`.

The sidestep attaches to the ordinary pursuit branch only. The cohesion branch
is left alone: its aim point is a formation destination whose position is
already constrained by the bias-square gates of the formation-movement design,
including a combined aim-point density statement that gate 6 exists to hold, and
displacing an agent out of that square would invalidate the statement without
anybody having measured what replaces it. A contingent agent that is genuinely
stuck still has a route out, because a contingent that cannot make progress
resolves to `Advance` and its members fall back to ordinary pursuit.

Whether the pursuit branch alone clears the measured stalls is the question
section 8 leaves open, and the plan document must answer it by measurement
before this is called done.

### 4.2 The offset

The displacement reuses the existing stall generation counter,
`CollisionScratch.StallGeneration(agentIndex)`, which is already per-agent,
already latched one generation per unbroken run of
`FormationRules.StallEscapeStreakTicks` blocked ticks, and already gameplay
state rather than observation. No new per-agent state is introduced and nothing
new needs to enter a future snapshot.

The direction is perpendicular to the agent's own approach vector, and which of
the two perpendiculars is taken is drawn from the same hashed key as the
magnitude, so two warriors blocked against each other do not both step the same
way. The magnitude is bounded by the body radius in the same manner
`RallyOffset.Compute` bounds its own, so a sidestepping warrior stays within a
body's width of the line it was already walking and the manoeuvre reads as
stepping around an obstacle rather than as breaking formation.

At generation 0 the offset is exactly zero and the aim point is byte-identical
to the one computed today. This is the whole hash-neutrality argument and it is
the same one the rally escape relied on.

### 4.3 Determinism

The offset is a pure function of `(Scenario.Seed, agent.EntityId, generation)`
through `Fnv1a`, in fixed-point integer arithmetic, with no floating point and
no dependence on iteration order. It draws from no random stream, so no stream
policy changes. The proposal loop already runs in a fixed order over a stable
array and the sidestep does not read any other agent's state, so it cannot make
one agent's outcome depend on another's incidental position in that array.

## 5. Hash impact

Expected to be none on any battle that does not stall. Every recorded hash
belongs to a battle in which no agent accumulates 192 consecutive blocked ticks,
so every agent stays at generation 0, so every aim point is unchanged.

That expectation is a claim to be verified, not an assumption to be relied on.
The plan document must run the recorded seed-1 headless workload and the
determinism fixtures and report the hashes, and must treat any movement as a
finding rather than as an expected consequence to be recaptured. The rally stall
escape made the same claim and it held; that is evidence for the shape of the
argument, not for this instance of it.

If a hash does move, the change needs a new movement preset version and new
golden expectations under CLAUDE.md section 5, and the ordering question of
whether to ship it as a default belongs to the user, not to the implementer.

## 6. The nine questions

1. **User-visible outcome.** A warrior stuck behind a comrade while walking at
   an enemy eventually steps around them instead of pressing into their back
   forever. Battles that previously ran to the tick limit with no casualties
   resolve.
2. **Tick stage and state read/written.** Movement-proposal construction, the
   same stage `BuildRegroupingProposal` already runs in. It reads the agent, its
   target, and the agent's existing stall generation; it writes only the
   agent's movement proposal.
3. **Numeric units and same-tick conflict rule.** Raw fixed-point world units,
   magnitude bounded by the body radius. The same-tick conflict order is
   unchanged: `Dead > Attacking > Regrouping > contingent cohesion > ordinary
   pursuit`, and this modifies the aim point within the last of those rather
   than moving an agent between them.
4. **Total ordering and random-stream policy.** No random stream is used. The
   offset is a hash of `(seed, entityId, generation)`; ties do not arise because
   nothing is compared across agents.
5. **Cache.** No cache. The stall generation already exists and is not a cache.
6. **Save, event and version effect.** No new persisted field and no new event.
   Version effect is contingent on section 5's hash result.
7. **Worst-case complexity and benchmark workload.** Constant time and zero
   allocation per proposal; no new scan of any kind. The 200-agent,
   10 000-tick, seed-1 headless workload is the benchmark, and the 200-seed
   probe surveys at thresholds 6 through 9 are the acceptance measurement.
8. **Spectator explanation.** This is the weakest part of the design and it is
   stated rather than hidden. A spectator watching a sidestep sees a warrior
   walk around another warrior, which is legible as behaviour but carries no
   reason code and no inspector field, so a spectator cannot distinguish it from
   ordinary pursuit toward a target that happens to lie off to one side. The
   rally escape shipped with the same gap. The plan document should decide
   whether an inspector field naming the stall generation is in scope, and if it
   defers that, it should say so explicitly rather than let the question lapse.
9. **Tests that fail before and pass after.** A Fact that builds the two-body
   geometry directly — a pursuing agent, a stationary comrade exactly on its
   line of approach — and asserts that the agent is still blocked at
   generation 0 and is no longer blocked once its generation increments. Plus
   the 200-seed probe surveys at thresholds 7 and 8, which must reach zero
   stalls without moving thresholds 6 and 9 off zero.

## 7. Risks

- **It may not be sufficient.** Ten of the twelve locked agents in seed 16 are
  `Regrouping` and already have an escape that is evidently not resolving the
  battle. Freeing the two `Moving` agents may unjam the whole cluster, or may
  leave a smaller lock behind. Section 8 makes this the first thing the plan
  measures.
- **It may perturb battles that were going to resolve.** The 192-tick trigger is
  the mitigation and it is already tuned for exactly this; a battle that resolves
  on its own does not accumulate 192 unbroken blocked ticks.
- **The two-perpendicular choice could pathologically agree.** Two warriors
  blocked against each other that both step the same way stay blocked. Keying
  the side on the entity id makes agreement possible but not systematic, and the
  generation increments again after another 192 ticks, so a coincidence costs
  time rather than correctness.

## 8. Open questions

1. Does the pursuit-branch sidestep alone clear the stalls at thresholds 7 and
   8? This must be measured before anything is called fixed. If it does not, the
   next question is whether the residual lock is `Regrouping`-only, which would
   mean the rally escape's own trigger or magnitude is the thing to revisit,
   not this design.
2. Should the contingent cohesion branch get an equivalent escape? Section 4.1
   argues for leaving it alone and gives the reason. This is a real question and
   not a settled one.
3. Should the inspector expose the stall generation, closing question 8 of
   section 6 for both this escape and the rally escape at once?

## 9. Out of scope

Changing the collision resolver. Changing `StallEscapeStreakTicks`. Changing the
shipping `LastStandThresholdAgents`. Widening any test's seed range — that is
separate work already agreed with the user, tracked independently of this
design.

## 10. Results

Implemented and measured at commit `a47219e` plus this change. The acceptance
criterion in the plan document's T5 was that thresholds 7 and 8 reach zero
stalls. **They did not.** The change is a substantial improvement and it is not
a fix.

200 seeds, radius 4352, 18 agents:

| `LastStandThresholdAgents` | before | after |
| --- | --- | --- |
| 6 — the shipping default | 0 | 0 |
| 7 | 5 | 2 |
| 8 | 8 | 3 |
| 9 — the maximum | 0 | 0 |

Thirteen stalls became five. The five seeds cleared at threshold 8 are 16, 44,
50, 125 and 189; the three that remain are 5, 49 and 146. At threshold 7, seeds
89, 127 and 160 clear and 72 and 105 remain.

### 10.1 Why the residual is not a sidestep problem

Section 8 question 1 asked whether the pursuit-branch sidestep alone would clear
the stalls, and named the expected diagnosis if it did not: a residual lock that
is `Regrouping`-only would mean the rally escape was the thing to revisit. The
residual is not `Regrouping`-only, and the real reason is a third thing that
neither the design nor the plan anticipated.

Section 4.1's scoping decision was tested directly rather than assumed. Letting a
stalled agent leave the contingent cohesion path entirely — which sidesteps the
bias-square density objection instead of violating it — was measured and produced
**identical** stall counts at all four thresholds. The two locked `Moving`
warriors in the residual seeds are already on the ordinary pursuit branch. The
cohesion branch is not implicated, and section 4.1's decision to leave it alone
stands on evidence rather than on argument.

The actual cause is the trigger, not the escape. Classifying seed 49 at threshold
8 over the 200 ticks after its last death, and measuring each blocked warrior's
longest *consecutive* run rather than its blocked percentage:

| entity | intent | blocked ticks | longest consecutive run |
| --- | --- | --- | --- |
| 1 | `Moving` | 192 | 191 |
| 10 | `Moving` | 192 | 191 |
| 4 | `Regrouping` | 196 | 196 |
| 17 | `Regrouping` | 194 | 192 |

Entities 1 and 10 are blocked on 96 per cent of ticks, and their longest
consecutive run inside the classification window is 191 — one short of the 192
the escape requires.

**That reading was wrong, and the correction matters more than the original
observation.** The 191 is an artefact of the probe's 200-tick classification
window truncating a run that continues past its end, not a property of the
battle. Measured over the whole battle instead, seed 49's longest blocked streak
is **9 823 consecutive ticks**. The streak is not being reset at all. At one
generation per 192 unbroken blocked ticks the escape fires roughly fifty-one
times, and `CollisionScratch._stallGenerations` is monotonic, so those fifty-one
generations each draw a fresh aim point and none of them is ever given back.

The trigger is therefore not the bottleneck. The escape fires, repeatedly, and
the warrior does not move.

This was tested rather than argued. A trigger that resets the streak only on
real progress — displacement of at least one body radius from where the streak
began accumulating, rather than on any accepted move — was implemented and
measured. It changed nothing: stalls stayed at 2 and 3 across the same 200
seeds, and the per-tick blocked pattern of seed 49 came back byte-identical,
1 841 rows with the same per-entity counts. It was reverted.

### 10.1.1 What the residual actually is

A warrior that is refused on 9 823 consecutive ticks while being offered fifty-one
different aim points is not failing to find a direction. It is enclosed: every
candidate the resolver's ladder offers is refused by some body, whichever way the
aim point points. No intent-layer redirection can free it, because the intent
layer only chooses where to walk and the refusal is about whether any step at all
is admissible.

That places the residual squarely in the family of remedies design
`2026-07-28-follower-trailing-deadlock-design.md` sections 6.2 through 6.5
describe — a second resolution pass over blocked movers, dependency-ordered
resolution, rotation and swap detection — every one of which is resolver work,
and all of which were declined. Reopening that decision is not something this
document does. It is recorded here so that the next person to look at these five
seeds does not spend the time this session spent looking at the trigger.

### 10.2 Hash impact, verified

Section 5 predicted that no recorded hash would move and required that the
prediction be verified rather than assumed. It was verified and it held. The
seed-1 headless workload reports `stateHash 2410DD94F26C82E2` and
`eventHash 56F66BBC10E69F0E`, both identical to the recorded baseline, with
`deterministic: true`. Every determinism fixture and every movement preset freeze
test passes untouched, and nothing was recaptured. No new preset version is
needed and the shipping default is unchanged.

### 10.3 What was left open

Section 8 question 2, whether the cohesion branch needs its own escape, is now
answered no, on the measurement in section 10.1.

Section 8 question 3, whether the inspector should expose the stall generation,
is **deferred, not answered**. Section 6 question 8 already recorded that a
spectator cannot distinguish a sidestep from ordinary pursuit toward an
off-centre target, and that remains true: this change ships with the same
legibility gap the rally escape shipped with. It is recorded here so that it
lapses by decision rather than by silence.
