# Formation blocking at 500 agents — backlog entry and measured baseline

**Status:** Backlog. This document authorizes no implementation. It records a
measured baseline and the reasoning that produced it, so that whoever picks the
work up later starts from numbers instead of from a re-derivation.

**Date:** 2026-07-30. Written against `main` at `caf0d63`, with combat preset
V4 as the shipped default (`e724348`) and the rank-led contingent change in
place (`469fca4`).

**Origin:** A spectator report after a two-round session on 2026-07-30 — the
first round looked right, the second went "laggy", with warriors appearing to
move only every few seconds. The investigation that followed found no frame
loop problem and a movement problem, and this document is its record.

## 1. What the report turned out to be

The complaint was about frames and the evidence says it was about movement.
Both readings had to be tested, because they look identical from the
spectator's chair: the client's catch-up loop in `ArenaGame.AdvanceSimulation`
runs several ticks inside one frame, so a battle can hold its exact tick rate
while the picture updates twice a second.

Session log: `artifacts/logs/hukbo-20260729-190201-36920.jsonl`, a `Debug` run
at level `dbg` on every channel. The second round advanced one tick per 48–55
milliseconds for its whole length:

```
t=172 ms=145056 gap=49
t=173 ms=145110 gap=54
t=174 ms=145159 gap=49
...
t=210 ms=146959 gap=48
```

`Scenario`'s tick rate is 20, so 50 milliseconds per tick is exactly 1.0×
speed. No `sim.speed.changed` line appears anywhere in the session, so the run
was at 1× throughout and got what it asked for. Had frames collapsed, the
accumulator clamp (`MaximumAccumulatedSeconds = 0.5`) would have forced ticks
into bursts and these gaps would read `0, 0, 0, 500`. They do not.

Neither layer is short of headroom at 500 agents:

| Layer | Measurement | Result |
| --- | --- | --- |
| Simulation | Headless, 500 agents, 2 000 ticks, `Release` | 813 ms total; p50 0.118 ms per tick, p95 1.54 ms |
| Client render | `tools/Hukbo.Tools.RenderProbe`, 500 agents, real window, 200 frames per station | p50 2.36 ms per frame at minimum zoom, 0.98 ms at default fit, 0.27 ms at maximum zoom |

At a 20 Hz tick rate and a 60 Hz screen, those figures leave roughly fortyfold
headroom in each layer. What remains is the thing the spectator actually
watched: warriors that were not moving because they were blocked, not because
the frame carrying them was late.

## 2. The baseline

Both runs are `./scripts/benchmark.ps1 -Agents 500 -Ticks 2000`, `Release`,
combat preset V4, on the seeds the reported session actually played — seed 1
for the first round, and the seed `MatchSeries` derived for the second. Both
runs reported `deterministic: true` with no mismatch tick. Full reports were
written to `artifacts/blocking-baseline-seed1.json` and
`artifacts/blocking-baseline-round2.json`; `artifacts/` is not tracked, so the
numbers are reproduced here in full.

| Figure | Round 1 (seed 1) | Round 2 (seed 11400714819323198486) |
| --- | --- | --- |
| `measuredTicks` | 2 000 (undecided at the cap) | 1 980 |
| `outcome` | `Draw`, 5 against 4 survivors | `Faction0Victory`, 20 against 0 |
| `blockedAgentTicks` | 19 488 | **33 330** |
| `longestBlockedStreakTicks` | 178 | 168 |
| `attackCapableAgentTicks` | 28 588 | 27 882 |
| `candidatePairs` | 421 825 | 595 109 |
| `contactPairs` | 15 406 | 14 511 |
| `acceptedMoves` | 298 158 | 371 663 |
| `maximumFrontWidthRaw` | 639 828 | 515 762 |
| `maximumFrontDepthRaw` | 79 586 | 73 204 |
| `maximumPenetrationRaw` | 0 | 0 |
| `acceptedAttacks` | 5 714 | 5 620 |
| `landedAttacks` | 4 190 | 4 113 |
| `stateHash` | `9B5A42D96A7D9CD1` | `DBA70A1CAF958648` |
| `eventHash` | `DD4FD0F6552393CA` | `8E765A2B4FD31407` |

Three readings derived from that table, marked as derived because they are
arithmetic on the recorded counters and not fields the runner emits:

- **Blocked agent-ticks per tick.** 9.7 in round 1, 16.8 in round 2 — a 73%
  increase between two seeds of the same scenario shape.
- **Blocked against attack-capable.** 0.68 in round 1, **1.20** in round 2. In
  the second round the army spent more agent-ticks blocked than it spent able
  to attack, which is the clearest single statement of the problem.
- **Longest blocked streak in seconds.** 178 ticks is 8.9 seconds at a tick
  rate of 20; 168 ticks is 8.4 seconds. One warrior, stationary, for most of
  ten seconds, in plain view.

`maximumPenetrationRaw` is 0 in both runs, so the resolver is not letting
bodies overlap. This is a blocking problem, not a separation failure.

## 3. What this does not claim

- **The frame rate was never measured directly during the reported session.**
  Nothing logged frame time at the moment of the report; the finding above is
  an inference from tick pacing, which is sound for detecting a starved
  simulation and silent about how many frames were drawn. The `render.window`,
  `render.starved`, and `render.frame` events added on 2026-07-30 close that
  gap, and the next session under `Debug` will answer it directly.
- **The two seeds are two samples, not a distribution.** The 73% difference in
  blocking between them is real for those two runs and says nothing about the
  variance across seeds generally. A sweep over many seeds is the first task
  any future plan here should carry.
- **No cause is identified.** Whether the blocking comes from contingent
  shape, from approach geometry, from the rank-led leadership change, or from
  the preset's speed and radius values is exactly the open question. The
  numbers bound the symptom; they do not locate it.

## 4. When this is picked up

The related work already in `docs/plans/` is
`2026-07-28-collision-resolution-scaling-design.md`,
`2026-07-28-follower-trailing-deadlock-design.md`,
`2026-07-29-approach-sidestep-design.md`, and
`2026-07-29-contingent-shape-design.md`. A plan for this backlog entry should
say which of those it extends rather than opening a fifth parallel account of
the same crush.

A future change earns its place against this table. `blockedAgentTicks` falling
while `landedAttacks`, `maximumPenetrationRaw`, and the outcome stay sane is an
improvement; `blockedAgentTicks` falling because warriors walk through each
other is not. Any change to `Hukbo.Core` here moves both hashes and needs the
preset-version and golden-expectation treatment in
`SIMULATION-GAME-STANDARDS.md` §4 and the `hukbo-determinism-change` skill.
