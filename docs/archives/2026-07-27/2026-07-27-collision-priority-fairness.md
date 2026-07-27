# Collision Priority Fairness Implementation Plan

> **Archived: reference only.** This document is deprecated. Do not execute it, and do not treat its steps, versions, or tooling references as current. The live contract is `CLAUDE.md` plus the skills in `.claude/skills/`.

Date: 2026-07-27
Design: [2026-07-27-collision-priority-fairness-design.md](2026-07-27-collision-priority-fairness-design.md)
Branch: `feature/collision-priority-fairness`

**Goal:** Resolve movers by a per-tick priority key instead of ascending
`EntityId`, so that no faction holds a standing advantage in contested-ground
resolution.

## Tasks

1. **Add `src/Hukbo.Core/Simulation/CollisionPriority.cs`.** — done.
   `Resolve(seed, tick, entityId)` returns
   `(Fnv1a(tag, seed, tick, entityId) >> 32) << 32 | entityId`, using the same
   `Fnv1a` mixer and tagged-domain pattern as `HitLocationResolver`. Rejects a
   negative tick and an entity ID of zero or one that will not fit the low half.

2. **Order movers by the key in `CollisionResolver`.** — done.
   `CollisionMoveRequest` carries `PriorityKey`; `Reset` fills a parallel
   `_moverKeys` buffer and sorts it against `_moverIndices` with
   `Array.Sort(ulong[], int[], int, int)`, which needs no comparison delegate
   and so allocates nothing on a warm tick. The stationary pass is unchanged.

3. **Supply the key from `BattleSimulation.ResolveCollisions`.** — done. Only a
   mover pays for a mix; a standing agent's key is never read.

4. **Amend the contract documents.** — done. Section 9 of
   `docs/decisions/2026-07-27-collision-policy.md` and the priority paragraph in
   `SIMULATION-GAME-STANDARDS.md`, both stating the old rule, why it failed, and
   the measured before and after.

5. **Tests.** — done, 20 added and 2 rewritten:
   - `CollisionPriorityTests`, 19 cases counting theory rows: five golden mixer
     vectors, purity, sensitivity to each of seed, tick and entity, the entity
     ID in the low half, distinctness across one tick, neither faction's ID
     range winning consistently over 200 ticks, priority between two agents
     changing across ticks, the per-tick reshuffle observed through the battle
     simulation, and the rejected inputs;
   - `CollisionResolverTests.ContestedGroundFollowsThePriorityKeyRatherThanTheEntityId`,
     where the higher-ID mover carries the lower key and takes the ground;
   - `DeterminismTests.ContestedGroundGoesToTheLowerPriorityKeyAndFollowsARenumbering`,
     rewritten from the old ID-priority assertion;
   - `BattleSimulationTests.SeedsOneThroughTwentyProduceVictoriesForBothFactions`,
     strengthened from "at least one victory each" to "at least four each".

6. **Run the gate and record.** — done, see below.

7. **Archive both plan documents.** — done on merge.

## Result

| Measurement | Before | After |
| --- | --- | --- |
| Seeds 1-20, 200 agents | 1 faction-0 win, 19 faction-1 | 7 faction-0, 13 faction-1 |
| Seeds 1-40, 200 agents | not measured | 16 faction-0, 23 faction-1, 1 draw |
| Core tests | 398 | 418 |
| Tick p50, 200 agents | 0.0672 ms | 0.0951 ms |
| `maximumPenetrationRaw` | 0 | 0 |

`./scripts/verify.ps1` passed at all five stages. Seed 1, 200 agents,
10,000 ticks: `Faction1Victory` at tick 1154, state hash `5BEBA7A68F69BE0D`,
event hash `D379B60B2E30FFFC`, `deterministic: true`, `firstMismatchTick: null`.
Both hashes moved, as an authoritative movement change must; the combat preset
content hash did not. Full figures, including the 500-agent stress workload and
the cost discussion, are in
[docs/development/testing.md](../development/testing.md).

## Review outcome

An independent review found the rule sound and the tests underconstraining it.
Three findings were acted on:

- the randomized crowd fixture pinned every mover's key to its entity ID, so the
  resolver's no-penetration invariant was still being fuzzed only against the
  retired order. It now generates real per-tick keys;
- nothing failed when `Tick` was replaced by a constant in the seam, which would
  have silently reverted the whole fairness property.
  `TheContestSequenceFollowsThePerTickShuffle` now does, verified by running that
  exact mutation;
- `Array.Sort` is an unstable introsort, and the resolver was trusting callers to
  supply distinct keys. It now stamps the entity ID into the low half itself, so
  distinctness is structural rather than contractual.

Two were recorded rather than fixed: the `ResolveLoadout` global-entity-ID
asymmetry, which makes the two armies unequal whenever `AgentsPerFaction` is not
a multiple of the roster length and which affects the report-only 500-agent
workload; and the client-side inspector label, which now alternates for a
crowded agent where it previously read a steady `Blocked`. That is the visible
signature of the change and is what smoke row 21a exists to judge.

## Verification criteria

| Criterion | Method | Result |
| --- | --- | --- |
| No standing faction advantage | 40-seed outcome census | 16/23/1 |
| Priority order is strict and total | Unit test on key distinctness | Pass |
| Priority reshuffles per tick | Unit test across 100 ticks | Pass |
| Determinism preserved | Gate headless stage | `deterministic: true` |
| No stall introduced | Terminal tick 1154 against a 10,000 limit | Pass |
| Solid-disc invariant intact | `maximumPenetrationRaw` | 0 |
| Preset untouched | `CombatRuleset.ContentHash` | `0x59FB4CA563D87A49` |
| Interactive readability | Smoke row 21a | PENDING, not run |

## Out of scope

The candidate ladder, the boundary rule, the movement budget, the co-location
repair, corpse interaction, attack resolution, and the stationary pass's
ascending-ID order.
