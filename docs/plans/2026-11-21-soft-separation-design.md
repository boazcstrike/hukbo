# Soft separation — design proposal

**Status: proposal. This document authorizes nothing.** It asks to reverse a
recorded decision. `docs/decisions/2026-07-27-collision-policy.md` §12 rejected
"soft discs with bounded penetration". Section 13 of
`SIMULATION-GAME-STANDARDS.md` says "adding a second policy value is a new
decision record, not an implementation detail". Sandata's design document bans
push-apart at `:1097-1099`. No code in this document may be written until the
product owner approves a new decision record that supersedes those passages.
The plan document lists the work as blocked on that approval.

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

A new movement preset, appended after V16. It selects a new collision policy,
`CollisionPolicy.Separating`, through a new decision record. Every existing
preset keeps `CollisionPolicy.Solid` byte for byte, so no recorded hash moves
until the shipped default changes.

### 3.1 The pass, in fixed point

After `CommitMovement`, one separation pass runs over living agents:

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
3. Average over the contact count, multiply by the stiffness, cap the length,
   and write the result into a second array. Never write into the snapshot.
4. Apply all pushes, then clamp to the arena as `CollisionGeometry` already does.

Every multiply and division is integer. Every constant is a basis-point field on
the movement ruleset, labelled provisional in code and tests. The pass is
order-independent by construction, which the existing priority-ordered resolver
is not.

### 3.2 What replaces "zero overlap"

The decision record of 2026-07-27 rejected soft discs because they need "an
invented maximum-penetration constant" and give "a weaker invariant". Both
objections are fair, and this proposal answers them directly.

- **The constant is a cap, and the cap is tested.** The new invariant is that no
  living pair overlaps by more than `MaximumOverlapBasisPoints × 2R` at the end
  of any tick. A regression test asserts it over seeded battles, just as
  `CollisionRegressionTests.cs:64` asserts zero overlap today.
- **The iteration count is fixed at one pass per tick.** There is no
  convergence loop to tune.
- **The weaker invariant buys something measurable:** the defect table in
  section 1. If the measurements in section 5 do not show it, this proposal
  fails and is withdrawn.

### 3.3 What stays the same

- Attack reach stays centre-to-centre.
- Agents still advance to body contact. The proposal step still stops `2R`
  short of its target, as `BuildMovementProposal` does today
  (`BattleSimulation.cs:5768-5783`).
- Corpses still do not collide.
- Movement proposals still read tick-start state only.

### 3.4 What changes and must be rewritten

- `SIMULATION-GAME-STANDARDS.md` §13 gains a second policy with its own
  invariant. The sentence that forbids a second policy is replaced, by the
  decision record, with the rules for this one.
- Tests that assert zero overlap become per-policy:
  - `CollisionRegressionTests` (`:64`, `:87`);
  - `CollisionMetricsTests` (`:23`, `:131`);
  - `HeadlessRunnerTests` (`:437`).
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

## 7. The decision the product owner is asked for

Approve or reject a new decision record that:

1. allows a second collision policy, `Separating`, alongside `Solid`;
2. defines its bounded-overlap invariant and its single-pass rule;
3. states that a weight share on a positional correction is not rigid-body
   physics under `CLAUDE.md` §9;
4. scopes the first step to an opt-in Hukbo movement preset that changes no
   existing hash.

Sandata's stage-10 amendment is a second decision, taken after the Hukbo
measurements.
