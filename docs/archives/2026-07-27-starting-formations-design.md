# Starting Formations Design

> **Archived: reference only.** This document is deprecated. Do not execute it, and do not treat its steps, versions, or tooling references as current. The live contract is `CLAUDE.md` plus the skills in `.claude/skills/`.

Date: 2026-07-27
Status: implemented and merged
Scope: `Hukbo.Core` deployment of agents before tick 1

## Problem

`BattleSimulation.Create` currently places every warrior with two independent
random draws inside a rectangular band: the left faction anywhere in
`x ∈ [MapWidth/4, MapWidth/4 + MapWidth/10]`, the right faction anywhere in the
mirrored band, and both anywhere in the vertical interior. Overlaps are then
repaired by a ring scan.

Two consequences follow, and both are worth fixing.

1. **The start is unreadable.** A spectator sees two undifferentiated clouds of
   pawns. Nothing about the opening frame tells the viewer that an army is a
   collection of groups, and nothing distinguishes one battle's opening from
   another beyond noise.
2. **The two armies do not start from equivalent ground.** Each faction draws
   its own random offsets, so one side can begin more tightly packed, more
   spread along the vertical axis, or closer to the map edge than the other.
   Both armies are drawn from an identical roster, so any positional advantage
   at tick 0 is pure seed noise that the outcome then amplifies.

## Goal

Replace random-cloud placement with a **deterministic mirrored deployment**:

- every warrior belongs to one of several visibly separate contingents;
- the two factions receive the *same* arrangement, mirrored across the vertical
  centre line of the map, so tick 0 is positionally symmetric; and
- the arrangement stays irregular rather than parade-ground exact.

## Historical position

`docs/research/battles/03-deep-past-formations-and-tactics.md` is binding here
and it is unusually restrictive. Regular files, ranks, fixed frontage, shield
walls, spear blocks and named formations are all listed as **not attested**.
What the evidence does support, as simulation-safe envelopes, is:

- **contingent structure** — people who share a boat, a leader, or a kin
  network begin as a practical group (envelopes 1 and 2 in that document); and
- **irregular spacing** — movement, terrain and weapon reach prevent exact
  alignment (the "open ground" minimum defensible geometry).

This design therefore claims only that an army arrives as *several groups with
irregular internal spacing*. It deliberately does not claim ranks, depth,
frontage doctrine, or a formation name. The internal lattice used to generate a
contingent is an implementation device for guaranteeing non-overlapping bodies,
not a reconstruction of how anyone stood, and the plan below requires that to
be stated in the source comments and in the player-facing vocabulary — the
word used anywhere a spectator can read is "contingent", never a drill term.

The `RosterCounts` army-composition feature groups warriors of one weapon
category into a contiguous run of faction-local indices. Assigning contingents
from contiguous runs would produce weapon-homogeneous groups, which is a
stronger claim than the evidence supports. Members are therefore dealt to
contingents round-robin, so every contingent is mixed whenever the army is.

## Deployment model

All values below are raw fixed-point units. `r` is `Scenario.BodyRadiusRaw`.

### Region

Faction 0 deploys inside the left half of the map:
`x ∈ [r, MapWidthRaw/2 - r]`, `y ∈ [r, MapHeightRaw - r]`. The preferred depth
band is `x ∈ [MapWidthRaw/8, 3·MapWidthRaw/8]`, which keeps the opening
separation between the armies close to what the current random band produces,
so engagement timing does not shift noticeably.

### Contingents

`contingentCount = clamp(isqrt(AgentsPerFaction) / 2, 1, 8)`, never more than
`AgentsPerFaction`. Members are distributed as evenly as possible, with the
remainder going to the earliest contingents, and faction-local warrior `i`
joins contingent `i % contingentCount`.

Each contingent owns one horizontal lane of the region, so lane `i` is centred
at `regionMinY + laneSpan·i + laneSpan/2`. Lanes are the only mechanism that
keeps contingents apart, which matters for the overlap argument below.

Contingents alternate in depth: even-numbered contingents deploy forward
(nearer the enemy) and odd-numbered contingents deploy back. The result is a
ragged front edge rather than a single straight line.

### Members within a contingent

Members occupy a lattice of `cols = ceilSqrt(members)` columns and
`rows = ceil(members / cols)` rows, centred on the contingent anchor, with
odd-numbered rows offset by half a cell. Each member is then displaced by an
independent random jitter of up to `±(spacing - (2r + 1))/2` on each axis.

The lattice spacing is the largest value that satisfies every fit constraint:

```
spacing = min(6r, laneSpan / rows, regionWidth / cols)   floored at 2r + 1
```

`6r` is the preferred, airy value; the other two terms shrink it when the map
is small or the army is large. The floor is one raw unit beyond tangency
because the spawn repair pass counts a tangent pair as contact: a lattice that
only reached tangency would push every body through the repair scan and destroy
the mirror.

### Why no two bodies can overlap at spawn

Any two lattice members differ by at least `spacing` on at least one axis:
same-row neighbours by `spacing` in x, adjacent-row neighbours by `spacing` in
y, staggered diagonals by `spacing` in y. The distance between two points is at
least their separation on either axis, and jitter can erode a per-axis
separation by at most `2 · (spacing - (2r + 1))/2 = spacing - 2r - 1`. The
worst case is therefore `2r + 1`: one raw unit clear of tangency, never contact
and never overlap.

Across contingents the guarantee comes from the lanes. A blob is
`(rows - 1) · spacing` tall and `spacing ≤ laneSpan / rows`, so a blob plus its
jitter stays inside its own lane and consecutive lanes stay at least `spacing`
apart in y.

### The crowded-map fallback

The spacing floor can override the fit rules on a map that is nearly as dense
as `Scenario.Validate` permits, and then the lane argument no longer holds: two
lanes can be closer than a body diameter. That case is detected up front — the
tallest contingent plus its one-row gap must fit its lane and the widest must
fit the region — and the planner falls back to a single plain lattice covering
the whole region, centred, unstaggered and unjittered. Separated contingents
need room the map does not have; a body that overlaps is a worse outcome than a
body in a dull arrangement. The fallback still mirrors, still stays in its own
half, and still consumes no random draws, because the spacing floor already
forces the jitter to zero whenever this path runs.

The existing `ResolveSpawnPlacement` repair pass is kept unchanged as a final
safety net. Under either arrangement above it finds nothing to repair, which is
what preserves the mirror.

### Mirroring

The plan is computed once, for faction 0, and faction 1 receives
`x' = MapWidthRaw - x`, `y' = y` for the warrior with the same faction-local
index.

The mirror is geometric. Loadout assignment is left exactly as it is: the
`RosterCounts` path already resolves from the faction-local index and therefore
mirrors as well, while the default round-robin path resolves from the global
entity ID and only lines up when `AgentsPerFaction` is a multiple of the roster
length. Changing that is a composition question, not a formation one, and three
existing tests assert the entity-ID formula, so it stays out of this change.

## Determinism

- Two draws per faction-0 warrior, from the existing `SplitMix64` stream, in
  ascending faction-local index. Faction 1 consumes none.
- Integer arithmetic only. The square roots are integer square roots computed
  by a local loop; no floating point enters a hashed value.
- No hash-set or dictionary iteration participates in placement.
- The seed-1 state hash and event hash **will move**. That is expected and
  approved: starting positions feed the state hash directly. No combat preset
  value changes, so `CombatRuleset.ContentHash` must stay
  `0x59FB4CA563D87A49`.

## Known consequence: the seed win distribution got more one-sided

Measured over seeds 1 to 20, 200 agents, default map:

| Build | Faction 0 wins | Faction 1 wins |
| --- | --- | --- |
| `main` before this change | 4 | 16 |
| This change | 1 | 19 |

This is worth stating plainly because it looks like the opposite of the stated
goal. It is not caused by the deployment being unfair — both armies now hold
identical ground. It is caused by the deployment being *fair*: once the two
halves are reflections of each other, the only asymmetry left in the whole
simulation is the entity-ID ordering rule, which gives contested ground to the
lower ID and therefore always to faction 0. A rule bias that random spawns used
to hide behind noise now decides the battle almost every time.

Planning each faction from its own random draws — same contingent structure,
independent per-warrior jitter — was implemented and measured as a possible
mitigation. It produced exactly the same 1/19 split, so it bought nothing and
was reverted in favour of the exact mirror the feature asked for.

Fixing the underlying bias means changing a tick rule (alternating collision
priority, or deriving it from something other than raw entity ID), which needs
its own decision record under
[docs/decisions/2026-07-27-collision-policy.md](../decisions/2026-07-27-collision-policy.md)
and is deliberately out of scope here. The existing guard,
`SeedsOneThroughTwentyProduceVictoriesForBothFactions`, still passes but now
survives on a single seed, so it is one tuning change away from red.

## What this design does not do

- No `Scenario` knob and no second deployment algorithm. One arrangement,
  applied to both factions, is the whole feature.
- No leader entities, no cohesion behaviour, no formation state machine, no
  terrain. Contingents are a starting arrangement only; from tick 1 the
  existing target selection and collision rules take over and the groups
  dissolve into emergent motion.
- No change to the collision resolver, the movement budget, or victory rules.

## Spectator discoverability

Standards question nine asks whether a spectator can discover the effect
without reading source. They can: the opening frame shows several separated
groups per side instead of one cloud, and the two sides are visibly each
other's reflection. Pausing at tick 0 and comparing the halves is enough.
