# Tick Stage Profile

**Status:** Complete. This is the T3 measurement referenced by the Gate A
verdict in
[docs/plans/2026-07-28-arch-informed-performance-hardening.md](../plans/2026-07-28-arch-informed-performance-hardening.md)
and by its companion,
[docs/plans/2026-07-28-arch-informed-performance-hardening-design.md](../plans/2026-07-28-arch-informed-performance-hardening-design.md).

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

The eight stage names in the table below are the eight calls
`BattleSimulation.AdvanceOneTick` makes, in the fixed order the tick pipeline
executes them:

1. `DecrementCooldowns`
2. `SelectTargetsAndIntents`
3. `GatherMovementProposals`
4. `ResolveCollisions`
5. `CommitMovement`
6. `MeasureCollision`
7. `GatherAndCommitAttacks`
8. `ResolveOutcome`

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
