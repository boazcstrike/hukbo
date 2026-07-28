# Tick Stage Profile

**Status:** Complete. This is the T3 measurement referenced by the Gate A
verdict in
[docs/archives/2026-07-28/2026-07-28-arch-informed-performance-hardening.md](../archives/2026-07-28/2026-07-28-arch-informed-performance-hardening.md)
and by its companion,
[docs/archives/2026-07-28/2026-07-28-arch-informed-performance-hardening-design.md](../archives/2026-07-28/2026-07-28-arch-informed-performance-hardening-design.md).
The eight-stage measurement below predates the movement-preset axis
`docs/plans/2026-07-28-formation-movement-realism.md` introduced. T16 of that
plan added the ["The ninth stage"](#the-ninth-stage-resolvecontingentstates-t16)
section at the end of this document, which is the up-to-date measurement of
the tick pipeline as it stands today, nine stages, `PersistentContingentsV2`
as the shipped default. The original eight-stage table is retained exactly as
captured and is not reconciled against the new stage's numbers below — see
that section for why.

## Limitations

- **These are sampled wall-clock shares, not percentiles.** The figures below
  come from a thread-time sampler running at roughly 100 Hz. They record what
  fraction of sampled wall-clock time each call stack was observed in, not the
  per-tick p50, p95, p99, or max duration of any stage. Gate A's wording asks
  for per-stage percentiles; this document does not literally satisfy that
  wording, and its numbers should be read as inclusive/exclusive shares of
  total traced time, not as tick-level latency distributions.
- **The 200-agent column rests on few samples.** At 200 agents the traced
  simulation runs for only 867.1 ms of inclusive `AdvanceOneTick` time at
  roughly 100 Hz sampling, which is a small number of samples overall. Its
  smaller entries are noisy as a result. The 1000-agent and 2000-agent columns
  are dense enough to rank stages with confidence, and the ranking claims in
  this document lean on those two.
- **A sampling profiler can misattribute an inlined callee to its caller.**
  This is a known failure mode of stack-sampling tools, not something ruled
  out by construction here. The evidence about how much it actually happened
  in these traces is the unresolved remainder reported at each agent count:
  0.76 % at 200 agents, 0.11 % at 1000 agents, 0.09 % at 2000 agents. Those
  are small, but they are not zero, and they are the honest bound on
  misattribution rather than a guarantee against it.
- **Exclusive time at a stage's own frame is near zero in the per-stage
  inclusive table, and that is expected, not a measurement failure.** Every
  stage in `BattleSimulation.AdvanceOneTick` delegates its work to callees, so
  almost none of a stage's own time is spent in the stage method's own frame.
  The trace exporter also inserts `CPU_TIME` and `UNMANAGED_CODE_TIME`
  pseudo-leaves under every real frame. The flat exclusive tables later in
  this document fold those pseudo-leaves back into their parent frame, which
  is why the flat tables, not the per-stage table, carry the meaningful
  exclusive numbers.

## Methodology

Tool: `dotnet-trace`, version `9.0.661903`. Profile: `dotnet-sampled-thread-time`,
the built-in stack-sampling profile that samples on-CPU managed and unmanaged
thread time. Sampling rate: approximately 100 Hz. Output format: `speedscope`
(`--format speedscope`).

Build configuration: Release, the same configuration the canonical gate
builds. The binary under measurement was the unmodified shipped Release build
of `Hukbo.Headless` with no timing instrumentation added to `Hukbo.Core` or
anywhere else in the call path. `Hukbo.Core` therefore never touched the wall
clock during this measurement; the wall-clock sampling happened entirely
outside the simulation, in the external profiler attached to the process.

Workload: the same headless benchmark harness used throughout this
workstream, `scripts/benchmark.ps1`, run with seed 1 and 10,000 ticks at each
of three agent counts: 200, 1000, and 2000 agents. Each traced run was its own
process.

Hardware: Windows 11 Pro 10.0.26200, x64, the same machine used for every
other figure recorded in this workstream today, 2026-07-28. .NET SDK
`10.0.302`.

Trace wall windows (the profiler's own capture window, distinct from the
inclusive `AdvanceOneTick` time reported below): 1185.1 ms at 200 agents,
24795.2 ms at 1000 agents, 143517.2 ms at 2000 agents.

## Fixed tick order

`BattleSimulation.AdvanceOneTick` makes nine calls today, in the fixed order
the tick pipeline executes them. The eight names below are exactly what the
table two sections down measures; `ResolveContingentStates` is the ninth
stage, added between `SelectTargetsAndIntents` and `GatherMovementProposals`
by task T9 of `docs/plans/2026-07-28-formation-movement-realism.md`. It
returns on its first line under `IndependentPursuitV1` and performs the
contingent state machine, the duty-cycle window, and gates 5 and 6 of the
cohesion rule under `PersistentContingentsV2`.

1. `DecrementCooldowns`
2. `SelectTargetsAndIntents`
3. `ResolveContingentStates`
4. `GatherMovementProposals`
5. `ResolveCollisions`
6. `CommitMovement`
7. `MeasureCollision`
8. `GatherAndCommitAttacks`
9. `ResolveOutcome`

**This table's eight rows are the original T3 measurement, unmodified by
T16.** They were captured before the movement-preset axis existed, when the
simulation had only the behaviour this workstream later froze as
`IndependentPursuitV1`; there is no `ResolveContingentStates` row here because
that stage did not exist yet. T16 measured the ninth stage separately, at
`PersistentContingentsV2` (today's shipped default), rather than folding a
new number into this table's existing sums — see
["The ninth stage"](#the-ninth-stage-resolvecontingentstates-t16) at the end
of this document for why, and for the actual figures.

## Per-stage inclusive share of `AdvanceOneTick`

| stage | 200 agents | 1000 agents | 2000 agents |
| --- | --- | --- | --- |
| DecrementCooldowns | 0.00 % | 0.00 % | 0.00 % |
| SelectTargetsAndIntents | 5.04 % | 15.88 % | 16.67 % |
| GatherMovementProposals | 8.81 % | 1.93 % | 0.94 % |
| ResolveCollisions | 63.11 % | 70.11 % | 74.77 % |
| CommitMovement | 1.64 % | 0.96 % | 0.56 % |
| MeasureCollision | 18.28 % | 9.79 % | 6.53 % |
| GatherAndCommitAttacks | 2.35 % | 1.19 % | 0.44 % |
| ResolveOutcome | 0.00 % | 0.02 % | 0.01 % |
| **named stages sum to** | **99.24 %** | **99.89 %** | **99.91 %** |
| **unresolved remainder** | **0.76 %** | **0.11 %** | **0.09 %** |

`AdvanceOneTick` inclusive wall time in each trace: 867.1 ms at 200 agents,
22828.4 ms at 1000 agents, 137175.2 ms at 2000 agents.

As the Limitations block states, exclusive time at each stage's own frame is
near zero in this table because every stage delegates to callees; the tables
below carry the meaningful exclusive numbers.

## Flat exclusive profile at 200 agents

Top entries under `AdvanceOneTick`, by exclusive time, at 200 agents (867.1 ms
traced inclusive total):

| exclusive share | frame |
| --- | --- |
| 23.64 % | `System.Buffer.ZeroMemoryInternal` (205.0 ms of 867.1 ms) |
| 16.49 % | `CollisionResolver.IsFree` |
| 13.48 % | `CollisionResolver.TryAccept` |
| 10.17 % | `CollisionResolver.TryTruncate` |
| 4.82 % | `BattleSimulation.SelectTargetsAndIntents` |
| 3.81 % | `Dictionary<Int64,Int32>.FindValue` (33.0 ms of 867.1 ms) |
| 3.75 % | `CollisionGeometry.Overlaps` |
| 3.35 % | `CollisionResolver.OverlapsCommitted` |
| 3.05 % | `CollisionResolver.CommitMovers` |
| 1.39 % | `CollisionUniformGrid.GeneratePairs` |

Per the first Limitations point, this column rests on a small number of
samples relative to the two larger agent counts below, so its smaller entries
in particular should be read as noisy.

## Flat exclusive profile at 2000 agents

Top entries under `AdvanceOneTick`, by exclusive time, at 2000 agents
(137175.2 ms traced inclusive total):

| exclusive share | frame |
| --- | --- |
| 50.62 % | `CollisionResolver.IsFree` (69431.4 ms exclusive, 69560.4 ms inclusive) |
| 22.46 % | `CollisionResolver.CommitMovers` (30815.3 ms exclusive, 101485.5 ms inclusive) |
| 16.63 % | `BattleSimulation.SelectTargetsAndIntents` (22817.4 ms exclusive, 22862.9 ms inclusive) |
| 3.17 % | `CollisionUniformGrid.GeneratePairs` |
| 1.65 % | `System.Buffer.ZeroMemoryInternal` |
| 1.40 % | `BattleSimulation.IntegerSquareRoot` |
| 0.85 % | `BattleSimulation.MeasureCollision` |
| 0.01 % | `Dictionary<Int64,Int32>.FindValue` (8.6 ms exclusive of 137175.2 ms) |
| 0.01 % | `Dictionary<UInt64,Int32>.FindValue` (6.9 ms exclusive of 137175.2 ms) |

A note on the two dictionary frames that appear in both flat tables: they are
different dictionaries, not the same one sampled at two agent counts. The
`Dictionary<Int64,Int32>` entries are `CollisionUniformGrid`'s cell-key map.
The `Dictionary<UInt64,Int32>` entry is `BattleSimulation._agentIndexes`,
which is keyed by `ulong`; it appears only in the 2000-agent trace, at 6.9 ms
of 137175.2 ms, and does not appear at all in the 200-agent trace.

## Headline finding

`ResolveCollisions` dominates the tick at every scale measured, and its share
rises with agent count: 63.11 % at 200 agents, 70.11 % at 1000 agents, 74.77 %
at 2000 agents. Within it, `CollisionResolver.IsFree` alone accounts for
50.62 % of all exclusive tick time at 2000 agents, half of the entire tick.
`SelectTargetsAndIntents` — the stage every structural candidate in the
companion plan targets — never exceeds 16.67 % of the tick at any measured
agent count.

No task in the companion plan touches collision resolution, and no design
document authorizes touching it. This is a finding to record, not work to
start.

## The ninth stage: `ResolveContingentStates` (T16)

Task T16 of `docs/plans/2026-07-28-formation-movement-realism.md` measures
the stage T9 of that plan added. This section is a self-contained
measurement, dated after and separate from the eight-stage table above; it
does not revise any figure in that table.

### Why this section exists apart from the table above

The table above was traced under the behaviour later frozen as
`IndependentPursuitV1`, before `MovementPresetId` existed at all. Folding a
`ResolveContingentStates` row into that table's existing sums would imply a
single coherent trace where none exists: `ResolveContingentStates` was never
sampled in those runs because the code did not exist yet, and re-tracing the
other eight stages under `PersistentContingentsV2` would move every other row
too, since the two presets converge different agents onto different enemies
at different times, which changes the population curve `ResolveCollisions`
scales against. That is a separate, larger measurement this task was not
asked to perform. What follows is the honest, narrower thing T16 actually
measured: where the ninth stage's own cost sits, against the acceptance
budget design section 8.1 of the companion design document sets.

### Methodology

Same tool and profile as the eight-stage measurement above: `dotnet-trace`
`9.0.661903`, profile `dotnet-sampled-thread-time`, `--format speedscope`,
approximately 100 Hz sampling. Same reporting method as the table above —
`dotnet-trace report <trace> topN --inclusive`, read as inclusive wall-clock
sampling shares, not tick-level percentiles; the same Limitations caveats at
the top of this document apply here without restatement.

Build: Release, matching the canonical gate. Binary: the unmodified shipped
`Hukbo.Headless`, no timing instrumentation added anywhere in
`Hukbo.Core`. Workload: `scripts/benchmark.ps1`'s underlying headless runner,
seed 1, 10,000 requested ticks, `--movement-preset PersistentContingentsV2` —
today's shipped default — at four agent counts: 200 and 500 (the design's own
acceptance workloads, design section 8.1) plus 1,000 and 2,000 (matching the
table above's columns, for scale continuity). Each traced run was its own
process. Hardware: Intel Core i5-14600K (14 cores / 20 logical processors),
32,485 MB RAM, Windows 11 Pro 10.0.26200, x64, .NET SDK 10.0.302 — the same
machine, a later date, 2026-07-28.

### `ResolveContingentStates`'s share of `AdvanceOneTick`

| Agents | `AdvanceOneTick` inclusive (of whole trace) | `ResolveContingentStates` inclusive (of whole trace) | `ResolveContingentStates` share of `AdvanceOneTick` |
| --- | --- | --- | --- |
| 200 | 66.59 % | 0.98 % | **1.47 %** |
| 500 | 82.92 % | 0.94 % | **1.13 %** |
| 1 000 | 92.96 % | 0.55 % | **0.59 %** |
| 2 000 | 94.76 % | 0.33 % | **0.35 %** |

The share falls as agent count rises, which is the expected shape: the stage
is two forward passes over living agents (`O(n)`) plus a bounded, at-most-16
-slot, at-most-56-pair scan (`O(1)` in agent count), while `ResolveCollisions`
— the tick's dominant cost at every scale in both this table and the one
above — grows faster than linearly, so it claims a rising share of the tick
and every other stage's relative share falls, `ResolveContingentStates`
included.

**Budget verdict: met, at every agent count measured, by a wide margin.**
Design section 8.1's first acceptance figure is that the new stage's p95
inclusive share of `AdvanceOneTick` must not exceed 5%. The 1.47% measured at
200 agents and the 1.13% measured at 500 agents — the design's own two
acceptance workloads — are both well under a third of the budget. This is a
sampled aggregate share rather than a literal p95 across ticks, for the same
tooling reason the eight-stage table above reports shares rather than
percentiles; see `docs/development/testing.md`'s T16 entry for the full
environment block, the companion whole-tick p50/p95/p99/max figures, and the
second acceptance figure's verdict.

### The other eight stages, for scale, under `PersistentContingentsV2`

Recorded here because the trace was already captured; not a re-verification
of the table above; and reported without a "named stages sum" row, since
`DecrementCooldowns` and `ResolveOutcome` fell below this sampler's
resolution at every agent count measured (0.00% or absent from the top 500
entries by inclusive time), exactly as the table above also found for both
of them.

| Stage | 200 agents | 500 agents | 1 000 agents | 2 000 agents |
| --- | --- | --- | --- | --- |
| `SelectTargetsAndIntents` | 5.83 % | 13.14 % | 12.13 % | 20.54 % |
| `ResolveContingentStates` | 1.47 % | 1.13 % | 0.59 % | 0.35 % |
| `GatherMovementProposals` | 10.30 % | 4.66 % | 1.41 % | 0.99 % |
| `ResolveCollisions` | 58.11 % | 62.25 % | 77.44 % | 71.05 % |
| `CommitMovement` | 1.46 % | 1.12 % | 0.46 % | 0.43 % |
| `MeasureCollision` | 16.40 % | 13.31 % | 7.12 % | 6.09 % |
| `GatherAndCommitAttacks` | 3.23 % | 3.98 % | 0.74 % | 0.37 % |

Each column is that stage's raw inclusive share of the whole trace, divided
by that agent count's `AdvanceOneTick` inclusive share from the table two
sections up, so the column reads as "share of the tick" the same way the
table above does. `ResolveCollisions` still dominates at every scale, exactly
as the headline finding above states for the pre-workstream build;
`GatherMovementProposals`'s share is higher here than in a same-scale
`IndependentPursuitV1` trace would show, because it now carries the cohesion
aim-point branch T9 added, on top of the arrival taper T10 added to every
`BuildMovementProposal` call.
