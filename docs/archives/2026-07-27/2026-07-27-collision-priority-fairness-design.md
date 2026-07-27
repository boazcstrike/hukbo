# Collision Priority Fairness Design

> **Archived: reference only.** This document is deprecated. Do not execute it, and do not treat its steps, versions, or tooling references as current. The live contract is `CLAUDE.md` plus the skills in `.claude/skills/`. The shipped rule lives in section 9 of `docs/decisions/2026-07-27-collision-policy.md`.

Date: 2026-07-27
Status: implemented and merged
Scope: `Hukbo.Core` collision stage, tick rule

## Problem

`CollisionResolver` commits movers in ascending `EntityId`, so a lower
`EntityId` wins any contested destination. Faction 0 holds entity IDs
`1..AgentsPerFaction` and faction 1 holds the rest, so **faction 0 wins every
cross-faction contest, in every engagement, on every seed**. The rule never
varies.

The decision record accepted this in section 9 on the grounds that a blocked
agent is still inside attack reach and therefore still fighting. That reasoning
is sound about individual agents and wrong about outcomes. Measured over seeds
1 to 20, 200 agents, default map:

| Build | Faction 0 wins | Faction 1 wins |
| --- | --- | --- |
| Random spawn scatter (before mirrored deployment) | 4 | 16 |
| Mirrored deployment, exact reflection | 1 | 19 |
| Mirrored deployment, independent per-faction jitter | 1 | 19 |
| Mirrored deployment, half-lane vertical offset | 1 | 19 |
| Current `main` (mirrored deployment plus last stand) | 1 | 19 |

Three independent attempts to break the symmetry geometrically — jitter of
±8 world units, a half-lane offset of about 65 units, and the exact mirror
itself — all produce exactly 1/19. That rules out a knife-edge symmetry and
identifies a standing structural advantage.

The mechanism is that winning a push is a disadvantage. A faction-0 agent that
takes contested ground advances into the enemy mass, where more enemies are
inside reach of it than of the agent it displaced. Damage is accumulated and
applied simultaneously, so being surrounded converts directly into taking more
damage per tick. Faction 0 therefore pushes forward everywhere and dies
everywhere.

Random spawn scatter used to hide this behind positional noise large enough to
sometimes hand faction 0 a real advantage. A symmetric deployment removed the
noise and left the rule bare. The fix belongs to the rule, not to the
deployment.

### One remaining asymmetry, which this change does not address

The collision order is not the *only* thing that differs between the two
factions. `CombatRuleset.ResolveLoadout` assigns a warrior's weapon from its
**global** entity ID, `(entityId - 1) % rosterLength`, while positions are
mirrored by faction-local index. Whenever `AgentsPerFaction` is not a multiple
of the roster length, the two armies therefore field slightly different
equipment. The gated 200-agent workload has 100 warriors per faction and a
four-entry roster, so it is unaffected; the reported 500-agent stress workload,
at 250 per faction, is not — faction 1 fields two more tall-hardwood shields
than faction 0 there.

That is a real second fairness defect and it is recorded rather than fixed
here: `ResolveLoadout`'s entity-ID formula is asserted by three existing tests
and belongs to army composition, not to the collision stage. The seed census
below uses the 200-agent workload, which the defect does not touch.

## Goal

No faction holds a standing advantage in contested-ground resolution, while
every determinism guarantee in `CLAUDE.md` §5 is preserved exactly.

## Proposal

Give each mover a **per-tick priority key** and resolve movers in ascending key
order instead of ascending entity ID.

```text
key = (Fnv1a(CollisionPriorityTag, seed, tick, entityId) >> 32) << 32
    | (entityId & 0xFFFFFFFF)
```

- The high half is a pseudorandom function of the scenario seed, the tick, and
  the agent, following the mixer `HitLocationResolver.MixAttack` already uses.
- The low half is the entity ID, so the order is a **strict total order** even
  when two mixes collide in their top 32 bits. Ties still break on stable
  `EntityId`, which is what `CLAUDE.md` §5 requires. Entity IDs are bounded by
  `2 * Scenario.MaximumAgentsPerFaction`, that is 20,000, so they fit the low
  half with room to spare.
- The key is recomputed every tick, so priority reshuffles every tick and no
  agent — and therefore no faction — holds a standing advantage.

Stationary bodies keep committing first, in ascending entity ID. They do not
contest ground with one another: a standing body neither moves nor yields, and
the only decision in that pass is the exact co-location repair, which is a
repair of an illegal input state rather than a contest. Reordering it would add
churn without changing an outcome.

## Determinism

Answering `SIMULATION-GAME-STANDARDS.md` §10 directly.

**Which tick stage reads and writes what.** `ResolveCollisions` reads tick-start
positions and the movement proposals, and now also reads `Scenario.Seed` and
`Tick` to compute one key per **mover**; a standing agent's key is never read,
so it is not computed. It writes nothing but the resolver's request list. The committed positions are still written by `CommitMovement`, in
request order, unchanged.

**Total ordering.** The key is a strict total order because its low 32 bits are
the unique entity ID. The sort is therefore order-determined regardless of the
sorting algorithm's stability.

**Random-stream policy.** No stream is consumed. The key is a pure hash of
`(tag, seed, tick, entityId)`, exactly like hit-location selection, so it cannot
shift any other consumer's draws and it reproduces identically on a resumed
save.

**Same-tick conflict rule.** Unchanged in shape: the first mover to be committed
takes the ground. Only the order in which movers are considered changes.

**Save, event, and version effect.** No new state, no new event, no new
`BattleSnapshot` field. Both hashes move for every seed because agents finish
ticks in different places. This is an authoritative movement change, so it
retires the current oracle and requires a re-recorded baseline. The combat
preset is untouched and `CombatRuleset.ContentHash` must not move.

**Spectator discoverability.** Honest answer: not in a single frame. What a
spectator can discover, without reading source, is that a stalled push resolves
differently from tick to tick rather than one side always shouldering through,
and that over a run of battles both colours win. The agent inspector already
shows a movement resolution per agent, so watching one second-rank agent
alternate between blocked and moving while pressed against the same enemy is the
closest single-screen evidence. A change that only shows up across many battles
is weaker than the standards prefer, and that is stated here rather than dressed
up.

## Performance

One `Fnv1a` mix per mover per tick, plus one sort of at most `TotalAgents` keys
per tick. At the gated 200-agent workload that is a 200-key
sort; at the reported 500-agent stress workload, 500. Sorting uses
`Array.Sort(ulong[], int[], int, int)` over pre-allocated buffers, so the hot
loop still allocates nothing. The 200-agent tick percentiles and the allocation
figure are re-measured and recorded; if the sort proves expensive at large
populations, the cheaper fallback is a per-tick rotation of the existing
ascending order, which is O(1) and delivers roughly half of the cross-faction
pairs to each side, and that alternative is recorded here rather than pursued
speculatively.

## Rejected alternatives

- **Interleave entity IDs by faction** (odd to faction 0, even to faction 1).
  Cheaper, but `ResolveLoadout(entityId)` derives a warrior's loadout from the
  entity ID and three tests assert that formula, so the blast radius is wider
  than the collision stage and the fix would be implicit rather than stated.
- **Situational priority** (the fresher fighter, or the agent not yet in
  contact, wins the push). Reads well to a spectator, but under a symmetric
  deployment the compared state is symmetric too, so it falls back to the entity
  ID tie and reproduces the bias.
- **Leave it and rebalance the deployment.** Measured three ways; all three
  produced the same 1/19. Geometry cannot correct a rule that never varies.

## What this design does not do

No change to the candidate ladder, the boundary rule, the movement budget, the
co-location repair, the corpse rule, or attack resolution. No new scenario
field, no new event kind, no new enum value. The stationary pass keeps its
ascending-ID order.
