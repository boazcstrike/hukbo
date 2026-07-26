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

From `docs/development/testing.md`, seed 1, 200 agents:

| Field | Value |
| --- | --- |
| Outcome | `Faction1Victory` at tick 235 |
| State hash | `210C5EF8E7BE4D48` |
| Event hash | `CE35EDA4B2A4E5A4` |
| Allocated | 12,108,304 bytes (captured baseline 19,856,712) |

Also recorded: seeds 1-20 produce victories for both factions rather than one
always-winning faction, and the 500-agent stress workload ends at tick 309.

If your change moves any of these, update that section explicitly. Do not let a
new number appear silently.

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
