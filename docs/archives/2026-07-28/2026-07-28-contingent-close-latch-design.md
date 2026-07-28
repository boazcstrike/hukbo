# Design — the contingent `Close` latch, and a `PersistentContingentsV3` preset

> **Archived: reference only.** This design was implemented, by the plan filed
> alongside it. Do not execute it, and do not treat its versions, file paths,
> or line-number citations as current.
>
> Its central diagnosis held up: rule 3 was the defect, and the two geometric
> gates were noise beside it. Its prediction in section 5 also held up —
> attrition became the new ceiling once rule 3 stopped dominating, rising from
> 23.51 % to 30.45 % of contingent-ticks. What it did not anticipate is how
> little headroom that left: after the fix, only **one** `Hold` episode
> followed a first `Close` across a five-seed sweep, and the `Hold`
> aspect-ratio tail got worse rather than staying flat. Section 7's open
> question about the exit-band width was measured and came back inconclusive
> — 10 re-entries into `Close` under V3 against 12 under V2. See
> `docs/development/testing.md` for the measurements.

Date: 2026-07-28
Status: design. This document does not authorize implementation. A plan
document under `docs/plans/2026-07-28-contingent-close-latch.md` follows and
carries the ordered task list.

## 1. The problem

The manual smoke pass recorded in
[`docs/development/testing.md`](../development/testing.md) failed two rows on
the persistent-contingent behaviour that shipped in commit `8f4e426`:

- **Row 104** — "A mid-battle contingent gather sometimes read as a line
  rather than as a ragged clump."
- **Row 114** — "Gathering was seen only near the start of the advance. It was
  not seen again once groups were already fighting."

Both were judgements by eye. `Hukbo.Tools.ContingentShape` was built to attach
numbers to them, and its output is recorded under "Measurement behind rows 104
and 114" in the same file. The measurement changes what the two rows mean.

### Row 114 has exactly one cause

Across a five-seed, 200-agent, 10 000-tick sweep, all fifty contingent-battles
reached `ContingentState.Close`, and **none of the fifty ever returned to
`ContingentState.Hold` afterward**. Hold ticks after a contingent's first
`Close`: zero. Hold episodes after a contingent's first `Close`: zero.
Contingents spend 63.69 % of their living ticks in `Close` and a further
23.51 % in `Break`, against 3.09 % in `Hold`.

The denial attribution puts 63.69 % of all contingent-ticks on transition rule
3 alone. The two geometric gates account for 1.81 % and 1.07 %, and a shut
duty-cycle window for 1.12 %. Rule 3 is the defect; the gates are noise beside
it, and the earlier suspicion that the cross-contingent gate was a co-equal
cause is not supported by the measurement.

Rule 3 reads, in `MovementRules.ResolveContingentState`
(`src/Hukbo.Core/Movement/MovementRules.cs:208-212`):

```csharp
var closeRadiusSquared = checked(closeRadiusRaw * closeRadiusRaw);
if (nearestEnemySquared <= closeRadiusSquared)
{
    return ContingentState.Close;
}
```

`nearestEnemySquared` is the **minimum over every living member** of that
member's squared distance to its own selected target
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:895-905`). With
`CloseRadiusMultiplier` at 16, one warrior of forty coming within sixteen body
radii of its target puts the entire contingent into `Close`, and gate 1 of
`IsCohesionEligible` then denies cohesion to all forty. In a converged melee
some member is always within that distance, so the condition never lifts. The
state is not declared terminal, but it behaves as though it were.

### Row 104 is the same defect, not a second one

The shape metric does not reproduce a line. Across 1 671 `Hold` samples the
principal-axis aspect ratio has a median of 1.56, a 99th percentile of 3.06,
and a maximum of 5.17, with 79.29 % of gathers below 2.0. A real gather is a
clump.

The two mechanisms that could have produced a line are both refuted for `Hold`.
The gathered cloud aligns more closely with the contingent's own direction of
advance (mean 12.21°) than with a world axis (mean 22.09°), which is the
opposite of what the axis-aligned bias square would produce. And no `Hold` or
`Advance` sample in the entire sweep fell within sixty ticks of a leader
change, because a leader change requires a death and deaths only begin once the
contingent has already latched into `Close`.

`Close` contingents, by contrast, have a median aspect of 3.60 and a 90th
percentile of 7.73. What the spectator saw mid-battle and called a gather was
therefore almost certainly a `Close` contingent strung out, not a `Hold` at
all. **Rows 104 and 114 are two faces of one defect.** Fixing rule 3 is the
change that both rows are waiting on, and row 104 is then re-observed rather
than separately engineered.

The control run confirms the cohesion itself is sound: under the frozen
`IndependentPursuitV1` preset the same nominal groups show a median aspect of
5.09 with both angles at 44.1°, the uniform-random value. Cohesion does real
work when it is allowed to run. It is almost never allowed to run.

## 2. What changes

Rule 3 stops asking whether *any* member has reached contact and starts asking
whether *enough of the contingent* has. A contingent closes when a configured
fraction of its living members have a selected target inside the close radius,
and it stays closed until that fraction falls to half the entry value.

Everything else about the persistent-contingent behaviour is unchanged: the
duty cycle, the hysteresis band of rule 5, the straggler test, the arrival
taper, the personal offset, and both geometric gates keep their current
semantics and their current values.

### Why a fraction and not a larger radius

Shrinking `CloseRadiusMultiplier` would delay the latch rather than remove it.
The defect is not that sixteen body radii is the wrong distance; it is that a
single member's distance decides a forty-member unit's behaviour. Any radius
still latches, only later. A fraction is the smallest change that addresses the
actual quantifier.

### Why hysteresis on the exit

Without it a contingent hovering at the entry fraction alternates between
`Close` and `Hold` on successive ticks, and cohesion pulses on and off at tick
rate. That is a worse spectator read than the latch it replaces. Rule 5 already
solves the same problem with a band — it enters `Hold` above the cohesion
radius and remains there down to three quarters of it — and rule 3 follows that
precedent. The exit fraction is half the entry fraction, written in the rule
body and documented there, exactly as rule 5's three-quarter factor is, rather
than becoming a second pair of ruleset fields.

### Proposed values, and their status

`CloseFractionNumerator = 1`, `CloseFractionDenominator = 2`: a contingent
closes when half or more of its living members are in contact, and re-opens
when fewer than a quarter are. These are **game-design choices, not historical
measurements**, and they carry that label in code and in tests exactly as
`FormationRules`' rally constants and the existing movement tunables do. No
source describes a unit's contact threshold. They are a starting point to be
re-measured by `Hukbo.Tools.ContingentShape` after implementation, not a
derived quantity.

### Explicitly out of scope

- **The geometric gates.** Together they deny 2.88 % of contingent-ticks today.
  They are not touched. See section 7 for the risk that their share rises once
  rule 3 stops dominating, and for how that is detected.
- **Rule 2, attrition.** `Break` accounts for 23.51 % of contingent-ticks and
  is terminal. Once rule 3 relaxes, rule 2 becomes the next ceiling on
  mid-battle gathering. This design does not change it; the plan's verification
  re-measures it so the next decision has a number.
- **The bias square's axis alignment.** The measurement refutes it as a cause
  of row 104. Rotating it to the direction of advance is not proposed.

## 3. Why this needs a new preset

`Scenario.MovementPreset` is folded into the state hash
(`src/Hukbo.Core/Determinism/StateHasher.cs:47`), and
`PersistentContingentsV2` is the current default with a recorded seed-1 pair.
Changing rule 3's behaviour under the existing preset id would move that pair
without changing the id that names it, which is precisely what
`SIMULATION-GAME-STANDARDS.md` section 4 forbids. The change therefore ships as
`MovementPresetId.PersistentContingentsV3 = 3`, appended to the enum, and
becomes the new default. `PersistentContingentsV2` stays registered and
selectable through `--movement-preset`, and gains a frozen digest fixture of
its own alongside `seed-1-200-agents-movement-v1-digest.json`, so both earlier
presets remain byte-reproducible.

### The closed constant set, and why it can open

`MovementRuleset`'s type remarks and `MovementPresetRegistry`'s comment both
state that the constant set is closed as of its introduction and must never
gain a field, because doing so would move `IndependentPursuitV1`'s pinned
`ContentHash`. That reasoning was sound when written but is stricter than the
thing it protects, and the difference matters here.

`MovementRuleset.ContentHash` does **not** reach the state hash.
`BattleSimulation.ComputeStateHash` folds `_rules.ContentHash`, and `_rules` is
the `CombatRuleset` (`src/Hukbo.Core/Simulation/BattleSimulation.cs:18-19,
393`); `StateHasher.Compute` never sees a `MovementRuleset` at all. Adding a
field to `MovementRuleset` therefore cannot change any preset's state hash,
event hash, outcome, or recorded digest. What it does move is two pinned
literals in `MovementPresetRegistryTests`
(`IndependentPursuitV1ContentHash` and `PersistentContingentsV2ContentHash`),
which are identity assertions over the ruleset's own fields, not behavioural
goldens.

Two paths follow, and this design recommends the first:

1. **Add the two fields and re-pin the two literals.** The freeze that has to
   hold — `IndependentPursuitV1`'s and `PersistentContingentsV2`'s simulated
   behaviour, proved by their digest fixtures — is untouched, and the plan must
   verify that by running both digest tests before and after. The three doc
   comments asserting the set is closed are corrected in the same change to say
   what is actually closed: the behaviour, not the field list.
2. **Branch on `MovementPresetId` inside `MovementRules.ResolveContingentState`.**
   This adds no field and moves no literal, but it splits behaviour selection
   across the registry and the rule body, and the split gets worse with every
   future preset. Rejected on that ground alone.

Path 1 requires the two pinned literals to be recomputed from the built code
rather than guessed, and the plan records the recomputation as its own task.

## 4. The nine questions

**1. User-visible outcome.** Contingents gather during the battle, not only
during the approach. A spectator watching a mid-battle engagement sees a
strung-out contingent draw back together and resume, which is what rows 103,
104, and 114 describe and what only row 103 currently delivers. The agent
inspector's `Contingent: <n> — <state>` row shows `Hold` at times other than
the opening advance.

**2. Tick stage and state read and written.** No new tick stage. The change is
confined to the ninth stage, `ResolveContingentStates`, which already runs
between target selection and movement. It reads each living agent's
`TargetEntityId`, position, `FactionId`, and `ContingentId`, and the
contingent's previous `ContingentState`; it writes each living agent's
`ContingentState`. One new preallocated per-slot scratch array,
`_contingentContactCounts`, sized `ContingentSlotCount` at construction like
its neighbours, replaces the role `_contingentNearestEnemySquared` plays for
rule 3. Whether `_contingentNearestEnemySquared` is retained or removed is a
plan-level decision; no other rule reads it today.

**3. Numeric units, bounds, and the same-tick conflict rule.** The contact
count is a plain member count, bounded by the contingent's living headcount and
so by `Scenario.AgentsPerFaction`. The threshold comparison is done by
cross-multiplication in `long` — `contactCount * denominator >= livingCount *
numerator` — with no division and no floating point, so no rounding rule is
needed. `closeRadiusSquared` keeps its existing `checked` `long` arithmetic.
There is no same-tick conflict to resolve: a contingent's state is written once
per tick by one stage, and no other stage writes it.

**4. Total ordering and random-stream policy.** A count is order-independent by
construction, which is a strict improvement on the minimum it replaces — a
minimum over `long` is order-independent too, but only because of the
comparison's own associativity, whereas a count cannot be made order-dependent
by any future edit short of a deliberate one. **No random draw is added.** The
change consumes nothing from any RNG stream, and `SplitMix64` is not touched.

**5. Cache source and invalidation.** No cache. `_contingentContactCounts` is
per-tick scratch, cleared at the top of the stage exactly as
`_contingentSpreadSquared` and `_contingentNearestEnemySquared` already are,
and it is never read outside the tick that wrote it. It is not persisted; see
question 6.

**6. Save, event, and version effect.** No new `BattleEvent` kind and no
change to any existing event's fields, so the event hash moves only by way of
the behaviour change itself. No new persisted field: `_contingentContactCounts`
is derived per-tick scratch and is excluded from snapshots, per the standing
rule against saving derived data. `MovementPresetId` gains value `3`, appended,
with no existing value renumbered. `MovementRuleset` gains two fields, moving
both pinned `ContentHash` literals but no state hash — see section 3. The
canonical gate's recorded seed-1 result moves, because the default preset
changes; new golden expectations are part of the change, and the plan states
the old and new values side by side.

**7. Worst-case complexity and benchmark workload.** Unchanged asymptotics. The
contact count is accumulated in the pass over living agents that already
computes `nearestEnemySquared`, so the stage stays a single `O(N)` pass plus
the existing `O(C²)` pairwise gate over at most eight contingents per faction.
No allocation is added on a warm tick. The verification workload is the
canonical gate's own: 200 agents, 10 000 ticks, seed 1, plus the 500-agent
result reported separately as section 10 of the standards requires.

**8. Spectator explanation.** Already built. The inspector's `Contingent: <n> —
<state>` row is the reason code, and it will now show `Hold` mid-battle where
today it shows `Close` from first contact to the end. Nothing new is needed to
make the effect discoverable without reading source.

**9. Tests that fail before and pass after.** Listed in section 6.

## 5. Risks

**The gates may become the new ceiling.** Once rule 3 stops denying 63.69 % of
contingent-ticks, those ticks are redistributed, and mid-melee is exactly the
situation in which contingents are packed closely enough for the
cross-contingent overlap gate to deny. It is a real possibility that gate 6's
share rises from 1.81 % to something dominant and row 114 still fails, for a
different reason. This is measurable rather than arguable:
`Hukbo.Tools.ContingentShape` re-run after the change re-attributes every
denied tick, and the plan requires that run. If gate 6 becomes dominant, that
is a second design, informed by a second measurement — not a speculative extra
change bolted onto this one.

**Rule 2 may become the new ceiling.** `Break` already holds 23.51 % of
contingent-ticks and is terminal. The same re-run reports it.

**Gathering mid-melee may look wrong even when it happens.** Warriors
disengaging to rejoin a leader could read as fleeing rather than regrouping.
Row 81 in the smoke checklist already pins the related invariant — a
regrouping warrior still strikes any enemy that passes within reach — and rows
104 and 114 are re-observed by a human after the change. No amount of
measurement substitutes for that; the harness can prove a clump is a clump, not
that a clump reads as a gather.

**The two fractions are guesses.** Half in contact to close, a quarter to
re-open, are chosen not derived. The harness makes re-tuning cheap, and the
values carry the provisional label in code and tests.

## 6. Acceptance

The change is accepted when all of the following hold, each with real output
recorded:

1. `./scripts/verify.ps1` passes, with its output pasted — formatting, Release
   build, the full test suite, and the seed-1 200-agent 10 000-tick headless
   determinism workload, whose new state hash, event hash, outcome, and
   terminal tick are recorded as the new goldens.
2. `IndependentPursuitV1` reproduces
   `seed-1-200-agents-movement-v1-digest.json` byte-identically, proving the
   `MovementRuleset` field addition moved no behaviour.
3. `PersistentContingentsV2` reproduces a newly recorded digest fixture of its
   own, generated from the code as it stands at commit `8f4e426` before the
   rule change lands.
4. New unit tests over `MovementRules.ResolveContingentState` covering, at
   minimum: a contingent with one member in contact and forty living does not
   close; a contingent at exactly the entry fraction closes; a closed
   contingent above the exit fraction stays closed; a closed contingent below
   the exit fraction re-opens; and the priority of rule 3 against rules 1, 2,
   and 4 is unchanged. Each must fail against the current rule body.
5. `Hukbo.Tools.ContingentShape` re-run at the same five-seed, 200-agent,
   10 000-tick workload, reporting a non-zero count of Hold episodes after
   first `Close`, a `Hold` aspect-ratio distribution no worse than today's
   (median 1.56, p99 3.06, max 5.17), and a fresh denial attribution. Both the
   before and after tables go into `docs/development/testing.md`.
6. A human at an interactive desktop re-observes smoke rows 104 and 114 and
   records the result. **No agent may flip either row.** Rows 106, 107, 108,
   109, 110, 112, and 113 remain `PENDING` unless that same human runs them.

## 7. Open questions

- Should the exit fraction be half the entry fraction, or should the band be
  wider? Half mirrors nothing in particular; rule 5's band is three quarters.
  A single measurement of state-flip frequency after implementation settles it,
  and the plan should capture that number whichever value ships.
- Should `_contingentNearestEnemySquared` be removed once rule 3 stops reading
  it? No other rule reads it today, but a future rule about approach distance
  plausibly would. Removing it is the honest default; keeping an unread field
  is the thing the standards call a deferred abstraction.
- Does the contact test belong on the member's *selected target* or on any
  enemy within the radius? Today's rule uses the selected target, and this
  design keeps that for continuity, but the two differ for a member whose
  target is distant while an unselected enemy is adjacent. The difference is
  measurable and is not measured yet.
