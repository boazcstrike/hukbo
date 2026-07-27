---
name: hukbo-determinism-change
description: Procedure for changing Hukbo.Core without breaking determinism, and for diagnosing a hash that moved. Use when editing simulation code, tick order, agent state, events, RNG, or fixed-point math; when a state hash or event hash changes; when a headless run exits 3 or reports a firstMismatchTick; or when deciding whether a hash change is legitimate. Covers the two independent hashes, the pinned SplitMix64 vectors that must never be edited, and the recorded seed-1 baseline.
---

# Changing Hukbo.Core safely

## Two hashes, two different meanings

They fail independently. Know which one moved before diagnosing.

| Hash | Produced by | Covers |
| --- | --- | --- |
| State hash | `BattleSimulation.ComputeStateHash()` via `Hukbo.Core/Determinism/StateHasher.cs` | Authoritative agent and battle state at a completed tick |
| Event hash | FNV-1a fold in `HeadlessRunner.Execute` | The ordered event stream |

The event hash folds exactly these fields per event, in this order:
`Sequence`, `Tick`, `Kind`, `SourceEntityId`, `TargetEntityId ?? 0`, `Value`,
`FactionId`, `Weapon`, and `HitLocation` (with `ulong.MaxValue` as the null
sentinel for `FactionId`, `Weapon`, and `HitLocation`).

Consequence: **reordering or renumbering `BattleEvent` fields or `Kind` values
moves the event hash without touching a single unit of state.** Adding an event
kind, changing when an event is emitted, or changing emission order does the same.

## Decision tree when a hash moves

1. **Was the change presentation-only?** (anything in `Hukbo.Client`, or a
   rendering/inspector/formatting concern.) Then both hashes must be
   byte-identical. If they moved, stop — presentation has leaked into Core, or
   Core is reading something it must not.
2. **Was the change authoritative?** (tick stages, targeting, movement, damage,
   death, victory, scenario defaults, RNG use, event contract.) Hashes may
   legitimately move. You must then re-record the baseline and state in the
   change description which hash moved and why.
3. **Did only the event hash move?** Suspect event ordering, an added or removed
   event, or a field reorder — not state.
4. **Did only the state hash move?** Suspect agent state, tick order, or
   fixed-point rounding.

Never adjust a test expectation to match new output until you can explain the
mechanism. A hash test that "just needs updating" is the normal shape of a
determinism regression.

## The RNG vectors are the oracle

`tests/Hukbo.Core.Tests/DeterministicRandomTests.cs` pins SplitMix64 output:

- seed 1 → `0x910A2DEC89025CC1`, `0xBEEB8DA1658EEC67`, `0xF893A2EEFB32555E`
- seed 0 → `0xE220A8397B1DCDAF`

These are reference vectors for the algorithm, not a snapshot of current
behavior. **Never** edit them to match new output. If they fail, the RNG
implementation is wrong.

Related rules from `CLAUDE.md` §5 that cause most real failures:

- `System.Random` is banned; use `Hukbo.Core/Determinism/SplitMix64.cs`.
- Hash-set and dictionary iteration order must never affect gameplay.
- Every multi-result query needs a total order; ties break on stable `EntityId`.
- Authoritative time is an integer tick, never the wall clock.
- Anything reaching the state hash uses fixed point
  (`Hukbo.Core/Mathematics/FixedPoint.cs`).

## Recorded baseline

From `docs/development/testing.md`, seed 1, 200 agents, one final verified run of
the collision priority fairness change on the
`feature/collision-priority-fairness` branch:

| Field | Value |
| --- | --- |
| Outcome | `Faction1Victory` at tick 1154 |
| State hash | `5BEBA7A68F69BE0D` |
| Event hash | `D379B60B2E30FFFC` |
| Allocated | 71,698,480 bytes |

The 500-agent stress workload, report only, from the same run: `Faction0Victory`
at tick 2668 with 1 faction-0 and 0 faction-1 survivors, state hash
`FE44ADA93E0E202A`, event hash `9C8EF5CB79810560`, deterministic with no mismatch
tick.

### Superseded hashes — dead values, do not target

Every pair below was superseded rather than corrected. None of them is a
regression target, and none may be used to judge whether a hash "should" match.
They are listed so a hash you find in an older document can be identified as
history instead of mistaken for a live baseline.

| Dead baseline | State hash | Event hash |
| --- | --- | --- |
| 200 agents, last-stand run, tick 1176 | `BBB40D2240720DC8` | `2A6BAEA1E3567046` |
| 500 agents, last-stand run, tick 2245 | `73FB96A4C5963149` | `1531FF58B7C7557B` |
| 200 agents, mirrored-deployment run, tick 1081 | `DC7F2E7A107C885A` | `6C641E90DDF0B943` |
| 500 agents, mirrored-deployment run, tick 2231 | `0C53793DEB700A53` | `4F373537096F2551` |
| 200 agents, amended-collision run, tick 657 | `D78F0B527B7F938F` | `AC3BAAEC684854D5` |
| 200 agents, pre-amendment collision run, tick 781 | `7EE8BF6EC0F11BB2` | `9BFC18AD06F4F572` |
| 500 agents, pre-amendment collision run | `7402CCC7C6EC3B50` | `619CCC872BBB2413` |
| 200 agents, pre-collision, tick 235 | `6EBB1EA63114F6CE` | `941377BD43C556FF` |
| 200 agents, earlier still | `210C5EF8E7BE4D48` | `CE35EDA4B2A4E5A4` |

The tick-1176 pair is superseded by the collision priority amendment, which
resolves movers in ascending per-tick `CollisionPriority` key instead of
ascending `EntityId`. Contested ground therefore goes to a different agent and
agents finish ticks in different places — an authoritative movement change that
moves both hashes without adding any state field, event kind, or enum value.
Its cause is recorded in section 9 of
`docs/decisions/2026-07-27-collision-policy.md`: the old order handed faction 0,
which holds the low entity IDs, every cross-faction contest of every battle, and
that decided 19 of 20 seeds once the mirrored deployment stopped masking it.

The tick-1081 pair is superseded by the last-stand formation, which redirects a
faction's last survivors onto their own leader instead of their own nearest
enemy once the faction drops to `Scenario.LastStandThresholdAgents` or fewer
living warriors — an authoritative movement change, so it moved both hashes:
regrouping survivors stand in different places, and a regrouping warrior's
`Move` event names its rally agent rather than an enemy.

Four separate legitimate movements produced that chain. Solid-disc contact put
new fields into the state hash and changed where agents stand, which retired the
tick-235 pair. The amendment then changed the approach target from attack range
to body contact — agents now advance until their bodies meet rather than until
their weapons reach — which changed where agents stand again and retired the
tick-781 pair. The proximity band introduced for contact metrics at the same time
moved **neither** hash, because it is derived observability; that byte-identical
result is what proved it had not leaked into authoritative state. The mirrored
starting-formation deployment then planned both factions' spawns as mirrored
contingents instead of independent random scatter, which retired the tick-657
pair. Most recently, the last-stand formation changed where regrouping
survivors stand and what their `Move` events name as a target, which retired
the tick-1081 pair.

Also still recorded, and strengthened by the priority amendment from "at least
one victory each" to "at least four victories each": seeds 1-20 produce
victories for both factions rather than
one always-winning faction, verified by
`SeedsOneThroughTwentyProduceVictoriesForBothFactions` inside the ordinary Core
suite.

If your change moves any of these, update `docs/development/testing.md`
explicitly and say which hash moved and why. Do not let a new number appear
silently.

## Hashed fields that force a new preset version

Changing any of these moves the hashes for every seed, so each one requires a new
preset version plus new golden expectations. This is in addition to the
`CLAUDE.md` §5 list of enum values, enum order, roster order, weights, and hash
mixers.

| Field | Where | Why it is hashed |
| --- | --- | --- |
| `MovementResolution` | per agent, written by the collision stage | The authoritative reason an agent finished a tick where it did. Numeric values are pinned; reordering or renumbering them changes the state hash. |
| `Scenario.BodyRadiusRaw` | immutable scenario | The one common body radius. Changing it changes every legality test in the resolver and therefore every position. |
| `Scenario.CollisionPolicy` | immutable scenario | Hashed as its integer value so the contact rule is authoritative and legible in a saved scenario. Exactly one value, `Solid`, is accepted. |
| `Scenario.LastStandThresholdAgents` | immutable scenario | The per-faction living-count trigger for last-stand rallying. Changing it changes which ticks redirect survivors onto their own leader, and therefore both hashes for any seed where the threshold matters. |
| `AgentIntent.Regrouping` | enum, appended after `Dead` | Enters the state hash through the existing per-agent `Intent` write. Numeric values are pinned and append-only; reordering or renumbering moves the hash for every seed with a last stand. |

The uniform grid, the collision pair and proposal buffers, and the aggregate
collision counters are **derived**. They are never hashed, never snapshotted, and
never persisted, so a change to any of them must leave both hashes
byte-identical. If a grid or buffer change moves a hash, the derived layer has
leaked into authoritative state — treat that as a determinism defect, not as a
baseline to re-record.

## Diagnosing a mismatch

`./scripts/benchmark.ps1` exits **3** on a determinism mismatch and reports
`firstMismatchTick`. The runner advances two simulations from the same scenario
and compares tick, outcome, state hash, and the full `LastEvents` sequence every
tick — so a mismatch means the *same build* diverged from *itself*, which is
almost always unordered iteration, an unstable tie-break, floating point, or
ambient state.

Read `firstMismatchTick` first, then reproduce at that tick with a focused run:

```powershell
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1
dotnet test tests/Hukbo.Core.Tests -c Release --filter FullyQualifiedName~DeterminismTests
```

## Reviewer questions to answer in the change description

From `SIMULATION-GAME-STANDARDS.md` §10: which tick stage reads and writes what;
the total ordering and random-stream policy; the same-tick conflict rule; the
save, event, and version effect; and how a spectator can discover the effect
without reading source code. A change that cannot answer the last one is not
finished.
