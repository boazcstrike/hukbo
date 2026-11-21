# Decision: separating contact for one opt-in movement preset

**Status:** Approved by the product owner on 2026-11-21.

**Supersedes, for the preset named below only:** the "Soft discs with bounded
penetration" row of `docs/decisions/2026-07-27-collision-policy.md` §12, and the
sentence in `SIMULATION-GAME-STANDARDS.md` §13 that forbids any behaviour other
than zero overlap. Every other preset keeps the 2026-07-27 decision unchanged.

**Design:** `docs/plans/2026-11-21-soft-separation-design.md`.

---

## Context

Hukbo's solid discs have one clean invariant: no two living bodies overlap at
the end of a tick. The same rule refuses any move that would overlap, and those
refusals sit under the standoff draws, the mutual collision lock, the
unexplained blocked agent-ticks at 500 agents, and the 2,000-agent battle that
never ends. Each has been attacked separately, and several attacks failed. The
design document lists the record.

We March as One, another battle game, fields about a thousand units a side with
no refusals at all. Bodies may overlap a little and push each other apart, with
the lighter or less-committed body giving way. That model is a yield behaviour
that applies everywhere.

## Decision

1. **One new movement preset, `SeparatingContactV17 = 17`,** may apply
   separating contact. It is movement V14 plus separating contact and nothing
   else. No other preset changes behaviour.
2. **The behaviour lives on the movement ruleset,** behind an
   `AppliesSeparatingContact` gate that folds its parameters into
   `MovementRuleset.ContentHash` only inside the gate. `Scenario.CollisionPolicy`
   keeps its single value, `Solid`. No existing hash or pinned `ContentHash`
   moves.
3. **The resolver keeps a hard floor.** Under the preset, `CollisionResolver`
   refuses any move that would bring two living bodies closer than
   `floor = 2R × (1 − f)`, where `f` is the soft-band fraction. The ladder,
   priority order, and strict comparison are unchanged.
4. **One separation pass per tick** runs after `CommitMovement` and before
   `MeasureCollision`. It is a Jacobi pass: every body reads the same snapshot
   and writes only its own push, so the result does not depend on processing
   order. There is no convergence loop.
5. **The invariant under the preset** is, for every pair of living agents at the
   end of every tick:

   ```text
   d  >=  2R(1 - f) - 2c
   ```

   `c` is the per-tick push cap. The bound is not a consequence of item 3 and the
   cap alone: the resolver never closes a pair below its start distance or the floor, and the pass
   limits each body's motion toward every close neighbour so that no pair crosses the bound. The
   full mechanism and the constraint `B + 4 ≤ 2R` are in `docs/plans/2026-11-21-soft-separation-design.md`
   §3.2. A regression test asserts the bound. Every constant is an integer basis-point field
   on the ruleset, labelled provisional in code and tests.
6. **A weight share on a positional correction is not rigid-body physics** under
   `CLAUDE.md` §9. The pass has no velocity, no impulse, no restitution, and no
   momentum carried between ticks. Each body's share of a push comes from its
   armour weight class and whether it is committed to a fight.
7. **The shipped default does not change** under this decision. Making V17, or a
   successor, the default is a separate decision, taken from the measurements
   the design document's section 5 names.

## Consequences

- `SIMULATION-GAME-STANDARDS.md` §13 is amended with the floor rule and the
  invariant for presets that apply separating contact, in the same change that
  adds the preset.
- Zero-overlap tests become preset-aware. Under every preset except V17 they
  assert exactly what they assert today.
- `CollisionMetrics.MaximumPenetrationRaw` can be non-zero under V17. Its
  documentation says so.
- A V17 workload is added to the measurement record, not to the canonical
  gate, until it is a default candidate.
- **Added in implementation, 2026-11-21.** `BattleSimulation` switches several
  V14 behaviours on by preset identity rather than by ruleset field:
  battlefield realism, the last-stand yield, lateral cohort deployment, the
  evasive scratch and its hash fold, and the evasive-footwork arms. V17 is
  admitted at all seven of those sites, because without them V17 would not be
  "V14 plus separation". The V14 hashes are unchanged.
- **Added in implementation, 2026-11-21.** The invariant needed a limit on the
  pass to hold on every tick. The soft-separation design records the gap in
  its section 3.2 and the limit that closes it.

## Not decided here

- **Sandata.** Its stage-10 contract bans the accumulate-then-apply pattern this
  pass uses. Changing that is a second decision, taken after the Hukbo
  measurements.
- **A crowd-pressure channel.** The design defers it so the overlap push can be
  measured alone.
