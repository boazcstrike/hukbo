# Soft separation — design

**Status: the Hukbo opt-in preset is approved; Sandata is not.** The product
owner approved the Hukbo half of this proposal on 2026-11-21. The decision
record is `docs/decisions/2026-11-21-separating-contact.md`, and it supersedes
the "soft discs" row of `docs/decisions/2026-07-27-collision-policy.md` §12 for
that preset only. The Sandata half (section 4) remains a proposal and needs a
second decision after the Hukbo measurements.

File and line references are to `main` at `0ccc0d3`, the state before implementation; symbol names are
the stable reference, and line numbers are not renumbered.

When this document was first written it asked to reverse a recorded decision.
Section 13 of `SIMULATION-GAME-STANDARDS.md` said "adding a second policy value
is a new decision record, not an implementation detail". Sandata's design
document bans push-apart at `:1097-1099`.

**Baseline: `main` at `0ccc0d3`, 2026-11-21.**

---

## 1. The question this document answers

Should two bodies that want the same space be allowed to overlap slightly and
push each other apart, instead of one being refused outright?

Hukbo and Sandata both answer "no" today. Both use solid discs, propose and
commit movers in priority order, and refuse any step that would overlap. That
model has a clean invariant: no two living bodies ever overlap, checked after
every tick. It has also produced the longest-running family of defects in this
repository:

| Defect | Where it is recorded | Status |
| --- | --- | --- |
| V6 and V7 standoff draws. Refuse turned out to mean "the ally-clearance test failed" | Memory and V7 measurements | Avoided by shipping presets without footwork |
| Mutual collision lock. The resolver has no cycle detection | `docs/plans/TODO.md:55-98` | Option 6.4 failed twice. Option 6.5 is unbuilt |
| Blocked agent-ticks at 500 agents, varying 146% across seeds, worst streak 904 ticks | `docs/plans/TODO.md:32-53` | Unexplained |
| 2,000 agents: 1,943,319 blocked agent-ticks, with 1,352 of 2,000 still alive at the 10,000-tick cap | `docs/plans/TODO.md:109-116` | Open |
| Sandata column about-face deadlock: the follower ends up in front and the leader is refused | `docs/plans/2026-08-19-sandata-column-about-face.md` | Four attempts failed. Three acceptance tests fail on `main` on purpose |

The corner-placement findings say it plainly. The corner has about half a world
unit of slack, so only a **yield behaviour** can fix the hairpin
(`docs/plans/2026-08-19-sandata-corner-trail-placement-findings.md:131-142`).
Soft separation with a weight share is a yield behaviour that applies
everywhere, not one more special case.

## 2. The model that motivates this

We March as One fields about 1,060 units a side with no collision refusal at
all. Its `SeparationJob` (`SeparationJob.cs:251-647` in its decompiled source)
works like this, in outline:

1. Each unit moves **only itself**, reading the other units' positions from a
   snapshot taken at the start of the pass. This is a Jacobi pass, so the result
   does not depend on the order units are processed in.
2. For each overlapping neighbour, the push is the overlap depth along the line
   between centres. A coincident pair gets a direction from a hash.
3. The push is shared by weight: a unit takes `m_other / (m_me + m_other)` of
   it. A unit in combat counts as three times its weight, so a fighting front
   line is hard to shove.
4. Pushes are averaged over the number of contacts, scaled by a stiffness of
   0.5, and capped at a quarter of the body radius per pass.
5. A second, gentler "crowd pressure" channel lets units behind press forward.
   Any component of that pressure pointing **into an enemy** is removed, so a
   crowd cannot shove its own front line through the enemy line.
6. Smoothed pressure feeds back into movement speed. A jammed unit slows down
   instead of fighting the crowd.
7. A unit ordered to hold is never pushed. It still pushes others.
8. Terrain and the arena edge stay hard. They are resolved after the push.

Those numbers are floats tuned for another game. Only the structure transfers.

## 3. The proposal for Hukbo

A new movement preset, `SeparatingContactV17 = 17`, appended after V16. It is
movement **V14 with separation added and nothing else**. V14 is the shipped
default, so the experiment changes exactly one variable. V16's shield
encumbrance is not carried over.

The preset carries the behaviour, as every movement change in Hukbo does. The
ruleset gains an `AppliesSeparatingContact` gate and its parameters, folded into
`MovementRuleset.ContentHash` only inside that gate, following the pattern of
the existing gates (`MovementRuleset.cs:1226-1256`). V1 to V16 fold the same
bytes as before, so no recorded hash and no pinned `ContentHash` moves.
`Scenario.CollisionPolicy` keeps its single value, `Solid`. Its meaning is
amended by the decision record: under a preset that applies separating contact,
`Solid` names the hard floor in section 3.2, not zero overlap.

### 3.1 The pass, in fixed point

Two pieces change under the new preset.

**The resolver gets a smaller hard floor.** `CollisionResolver` keeps its
ladder, its priority order, and its strict overlap test. Only the distance it
tests against changes: `floor = 2R × (1 − f)`, where `f` is the **soft-band
fraction**. A mover may step into the band between `floor` and `2R`. That band
is where yielding happens.

**A separation pass runs after `CommitMovement`, before `MeasureCollision`:**

1. Snapshot all positions into a flat array.
2. For each living agent in ascending `EntityId`, sum the pushes from every
   overlapping living neighbour found through `CollisionUniformGrid`:
   - push along `(me − other)` by the overlap depth `2R − d`, using
     `FixedPoint.Sqrt` for `d`;
   - a coincident pair uses a direction from `Fnv1a` over both entity ids,
     never a random draw;
   - weight share `w_other / (w_me + w_other)`, where weight comes from the
     armour weight class that already scales movement speed
     (`ArmorWeightClasses`), times a committed-in-combat factor.
   - inside the soft band, add a small sideways component perpendicular to
     the line between centres, its side chosen by the lower entity id, so a
     symmetric head-on pair slides past instead of pressing forever. This is
     the same idea as We March as One's tangential push.
3. Average over the contact count, multiply by the stiffness, cap the length at
   `c`, and write the result into a second array. Never write into the
   snapshot.
4. Apply all pushes, then clamp to the arena as `CollisionGeometry` already does.

**Implementation notes, 2026-11-21.** The pure pass (SS-2) and the V17 ruleset
(SS-1) differ from the list above in five ways:

- Distance uses `FixedPoint.IntegerSquareRoot`, not `FixedPoint.Sqrt`, because
  `Sqrt` rescales its result by the fixed-point scale and the pass works in raw
  units throughout.
- The sideways component is applied by each body rotating its own push normal
  by +90 degrees, rather than by choosing a side from the lower entity id. The
  two bodies of a pair still slide in opposite directions, and the result is
  still independent of processing order.
- A coincident pair takes its direction from a 16-entry unit-vector table,
  indexed by the top four bits of an `Fnv1a` hash over both entity ids.
- The V17 constants are stiffness 5,000 basis points (0.5), committed weight
  multiplier 3, and sideways 2,500 basis points. The first two follow section 2.
  The sideways value was chosen in implementation and is provisional, like the
  others.
- The pass builds its own private neighbour grid, not `CollisionUniformGrid`
  (`src/Hukbo.Core/Simulation/SeparationPass.cs`, `BuildGrid`).

A crowd-pressure channel, like We March as One's, is deferred. The first preset
keeps only the overlap push, so its effect can be measured alone.

Every multiply and division is integer. Every constant is a basis-point field on
the movement ruleset, labelled provisional in code and tests. The pass is
order-independent by construction, which the existing priority-ordered resolver
is not.

### 3.2 What replaces "zero overlap"

The decision record of 2026-07-27 rejected soft discs because they need "an
invented maximum-penetration constant" and give "a weaker invariant". Both
objections are fair, and this proposal answers them directly.

- **The bound is proved, not hoped for.** The resolver guarantees that every
  living pair ends `CommitMovement` at least `floor` apart. The pass then moves
  each body at most `c`, so no pair distance shrinks by more than `2c`. After
  every tick, therefore:

  ```text
  for every pair of living agents:  d  >=  2R(1 - f) - 2c
  ```

  With the starting values `f = 15%` and `c = 5% of R`, that is `d ≥ 1.6R`, an
  overlap of at most 20% of a diameter. A regression test asserts the formula
  over seeded battles, just as `CollisionRegressionTests.cs:64` asserts zero
  overlap today. The resolver also accepts a starting position already inside
  the floor, as it already accepts merely overlapping stationary bodies
  (`CollisionResolverTests.cs:399`), so a pair the pass left below `floor`
  recovers on later ticks instead of locking.
- **Correction, 2026-11-21, found in implementation (SS-3).** The proof above
  has a gap. The resolver leaves a pair that already starts inside `floor`
  where it is, and the pass then averages pushes over all contacts, so pushes
  from third parties could squeeze such a pair closer on tick after tick.
  Measured before the fix, a 1,000-agent battle reached a closest pair of
  5,799 raw units against a bound of 6,964. The pass now limits each body's
  motion. After the arena clamp, a body's displacement is scaled toward zero
  until its component toward every neighbour closer than `2R` is at most half
  of that pair's remaining slack above the bound, less a margin of 4 raw units
  that absorbs integer rounding. The pair distance can only shrink by the sum
  of the two bodies' components toward each other, so no pair can cross the
  bound. The resolver never closes a pair below `min(start distance, floor)`,
  so the bound now holds on every tick by induction. A test asserts it over
  seeded battles at 200, 500, and 1,000 agents. The closest pair observed after
  the fix is 6,967 raw units. Any future change to `f` or `c` must keep
  `B + 4 ≤ 2R`.
- **Second correction, 2026-11-21, found in review.** The limit above had a
  hole of its own. Scaling truncates each displacement component toward zero.
  A neighbour closer than `B + 4` allows no inward motion at all, so when the
  body was not moving toward that neighbour it was skipped while the scale was
  chosen. Truncation could then turn the scaled displacement into one with a
  small component toward it, and a pair that started between `B` and `B + 4`
  could end up to about 2.8 raw units below the bound. The pass now checks
  again after any scaling: a body whose scaled displacement still moves toward
  a neighbour closer than `B + 4` is not moved at all on that tick. A unit test
  pins a configuration, found by scan, that falls below the bound when the
  second check is disabled. The hole was rare in practice. The 1,000-agent
  seed-1 battle produces the same hashes and the same closest pair, 6,967 raw
  units, with and without the fix. Parameter validation now also rejects any
  `f` and `c` for which `B < 1` or `B + 4 > 2R`.
- **The iteration count is fixed at one pass per tick.** There is no
  convergence loop to tune.
- **The weaker invariant buys something measurable:** the defect table in
  section 1. If the measurements in section 5 do not show it, this proposal
  fails and is withdrawn.

### 3.3 What stays the same

- Attack reach stays centre-to-centre.
- Agents still advance to body contact. The proposal step still stops `2R`
  short of its target, as `BuildMovementProposal` does today
  (`BattleSimulation.Movement.cs`).
- Corpses still do not collide.
- Movement proposals still read tick-start state only.

### 3.4 What changes and must be rewritten

- `SIMULATION-GAME-STANDARDS.md` §13 gains the floor rule and the bounded
  invariant for presets that apply separating contact. The sentence that
  forbids any other behaviour is narrowed, by the decision record, to every
  preset that does not.
- Tests that assert zero overlap become preset-aware:
  - `CollisionRegressionTests` (`:64`, `:87`);
  - `HeadlessRunnerTests` (`:437`).
  - `CollisionMetricsTests` (`:23`, `:131`) were left unchanged. They are accumulator unit tests
    with literal inputs, not preset-dependent.
- The blocked streak still exists under `Solid`. Under `Separating` an agent is
  rarely refused, so the 192-tick stall escape (`FormationRules.cs:143`) should
  rarely fire. Tests that assert streak 0 keep passing.
- `CLAUDE.md` §9 forbids rigid-body physics. This model has no velocity, no
  impulse, and no restitution. It does have a weight share. The decision record
  must state that a weight share on a positional correction is not rigid-body
  physics, or this proposal is out of bounds.

## 4. The proposal for Sandata

Sandata's agent-agent collision is separable from walls. The resolver has no
wall references, and walls live in the navigation grid and the clearance field.
The proposal is the same pass for **agent-agent contact only**, with three
differences:

1. **The weight comes from squad order.** A leader outweighs its followers, and
   a follower out of its slot weighs least. In the hairpin, the follower that
   ends up in front is pushed aside instead of blocking the leader forever. That
   is the yield the corner findings ask for.
2. **Walls stay hard.** A push that would move a body into a blocked nav cell,
   or below its clearance, is dropped for that axis. Whether stage 9 or stage
   10 already protects walls is an open question. It is answered before any
   code is written.
3. **Stage 10 changes contract.** `LocalAvoidance.cs:64-75` bans exactly the
   accumulate-then-read-back pattern a Jacobi pass uses. The Sandata design
   document needs the same kind of amendment as Hukbo's standards.

Sandata goes second, after the Hukbo preset has measurements, because Hukbo has
the larger evidence base and the cheaper rollback.

## 5. How it would be judged

All measurements compare the new preset against movement V14 with combat V8.

| Measure | Pass condition |
| --- | --- |
| Overlap invariant | Never exceeded in 20 seeds × 10,000 ticks |
| Decision rate | At least 19 of 20 seeds decided inside 5,000 ticks, the existing bar |
| 2,000-agent battle | Decided before the 10,000-tick cap |
| Blocked agent-ticks at 500 agents | Lower than V14 on every seed |
| Tick cost | p95 within 10% of V14 at 500 and 1,000 agents |
| Sandata | The three about-face acceptance tests pass |
| Spectator | Smoke rows judge whether a packed front still reads as a line rather than a blob |

### 5.1 Measured results — 2026-11-21

Movement V17 was built for Hukbo only and measured against V14 with combat V8.
The full figures are in `docs/development/measurement-history.md`, under
"Separating contact (movement V17) against V14".

| Measure | Result |
| --- | --- |
| Overlap invariant | **Pass.** The deepest penetration over 41 runs is 1,737 raw units against a proved floor of 1,740 |
| Decision rate | **Pass.** 20 of 20 seeds decided inside 5,000 ticks at 200 agents, and 20 of 20 at 500 |
| 2,000-agent battle | **Pass.** Faction 0 wins at tick 6,455, where V14 draws at the cap |
| Blocked agent-ticks at 500 agents | **Pass.** Lower on all 20 seeds, by a factor of 8 to 44 |
| Tick cost | **Fail at 500 agents**, where p95 is 34% above V14; pass at 1,000, where it is 8% above. p50 is lower at both |
| Sandata | **Not applicable.** Sandata separating contact was not approved and was not built |
| Spectator | **Pending.** The SEP smoke rows have not been run |

V17 stays opt-in and the default stays V14. Making V17 the default would need
the tick cost at 500 agents brought inside the bar and the spectator rows
passed.

## 6. Alternatives

- **Option 6.5, slide along the obstruction** (`docs/plans/TODO.md:80-93`).
  It is a smaller change and keeps the zero-overlap invariant. Its own entry
  says it does not resolve a symmetric head-on pair or a blocker that never
  proposes a move. It treats one symptom.
- **Sandata-only yield rules:** halt the follower, collapse the formation at a
  corner, or smooth the path. Each of these is untried and narrow. Each fixes
  the hairpin and nothing else.
- **Do nothing.** The defect table stays open.

My recommendation is to build the Hukbo preset as an opt-in experiment, measure
it against section 5, and decide from data whether it becomes the default.

## 7. The decision the product owner was asked for

**Approved on 2026-11-21**, as `docs/decisions/2026-11-21-separating-contact.md`.
The request was for a new decision record that:

1. allows a separating-contact behaviour beside the zero-overlap rule;
2. defines its bounded-overlap invariant and its single-pass rule;
3. states that a weight share on a positional correction is not rigid-body
   physics under `CLAUDE.md` §9;
4. scopes the first step to an opt-in Hukbo movement preset that changes no
   existing hash.

Sandata's stage-10 amendment is a second decision, taken after the Hukbo
measurements.
