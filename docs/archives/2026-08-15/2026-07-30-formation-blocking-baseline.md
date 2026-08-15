# Formation blocking at 500 agents — backlog entry and measured baseline

**Archived: reference only.** Archived on 2026-08-15 by user decision. This
document was never a task list and never authorized anything; it is a record of
two measurements and the reasoning that produced them. Never execute it, never
treat its numbers as current, and never cite it as the reason for a change.

Read it only to answer "what did formation blocking measure before". Two things
about those numbers matter to anyone who does. The 2026-07-30 table in section 2
is a two-seed comparison, and section 5's twenty-seed sweep of 2026-08-13
retired it outright: `blockedAgentTicks` varies by 146 per cent across seeds, so
a two-seed difference carried no signal at all. Section 5 is the later and more
honest of the two, and its worst case is a longest blocked streak of 904 ticks.
But section 6 then retired section 5 in turn, and that matters more than either
table: the sweep ran under `LastStandEngagementV11`, which was the shipped
movement default on the day, and the shipped default has since moved to
`CohortLateralSpreadV13`. Section 5 is therefore a record of a preset a
spectator no longer watches. Nothing in this document describes what the client
launches today, and re-measuring is the only honest way to compare against it.

The one thread that outlived it is in `docs/plans/TODO.md`, under the heading
naming the second-round lag report of 2026-07-30: warriors spend long stretches
unable to move in the crush, no cause was ever identified, and the work is
parked by user decision. The population question this document brushes against
belongs to the thousand-unit performance design and plan now.

**Original status line, kept for the record:** Backlog. This document authorizes
no implementation. It records a measured baseline and the reasoning that
produced it, so that whoever picks the work up later starts from numbers instead
of from a re-derivation.

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
  any future plan here should carry. **That sweep was run on 2026-08-13 and
  section 5 records it.** It answers this caveat in the direction the caveat
  feared: a 73% gap is ordinary seed variance, not a finding.
- **No cause is identified.** Whether the blocking comes from contingent
  shape, from approach geometry, from the rank-led leadership change, or from
  the preset's speed and radius values is exactly the open question. The
  numbers bound the symptom; they do not locate it.

## 4. When this is picked up

The related work still in `docs/plans/` is
the contingent shape design, whose own planning pass is the
archived document titled "Contingent shape — task plan (Phase C)". A plan for
this backlog entry should say which
of those it extends rather than opening a third parallel account of the same
crush. Two documents this section used to name,
`2026-07-28-collision-resolution-scaling-design.md` and
`2026-07-29-approach-sidestep-design.md`, have since been archived and are no
longer in `docs/plans/`.

A future change earns its place against this table. `blockedAgentTicks` falling
while `landedAttacks`, `maximumPenetrationRaw`, and the outcome stay sane is an
improvement; `blockedAgentTicks` falling because warriors walk through each
other is not. Any change to `Hukbo.Core` here moves both hashes and needs the
preset-version and golden-expectation treatment in
`SIMULATION-GAME-STANDARDS.md` §4 and the `hukbo-determinism-change` skill.

## 5. The seed sweep, 2026-08-13

Section 3 named a sweep over many seeds as the first task any future plan here
should carry. This is that sweep. Twenty seeds, 500 agents, 2 000 ticks,
`Release`, run through `./scripts/benchmark.ps1` with the presets the client
actually launches — `-Preset PrecolonialPhilippinesV5 -MovementPreset
LastStandEngagementV11`. Every one of the twenty runs reported
`deterministic: true` with no mismatch tick, and every one reported
`maximumPenetrationRaw` of 0.

**These numbers do not extend the table in section 2, and no row below may be
read as an improvement or a regression against it.** The 2026-07-30 baseline
was taken under combat preset V4. The shipped combat preset is now V5, V5 and
V6 are both new rulesets written after that date, and `MovementPresetRegistry`
gained several hundred lines in the same span, so the simulation being measured
is not the simulation that produced section 2. This is a fresh baseline that
happens to use the same counters. Section 2 stays where it is as the record of
what was measured then.

The choice of presets is deliberate and is the second reason the two tables do
not line up. A bare `./scripts/benchmark.ps1` run resolves
`Scenario.CreateDefault`, which is combat V6 and movement `PersistentContingentsV4`;
the client overrides both in `ArenaGame.BuildScenario`. The complaint this
document exists to explain came from a spectator watching the game, so the
sweep measures what a spectator sees rather than what the gate's own workload
runs.

| Seed | Measured ticks | Outcome | `blockedAgentTicks` | Longest streak | `attackCapableAgentTicks` | Blocked ÷ capable |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 2 000 | `Draw` | 79 332 | 588 | 177 116 | 0.448 |
| 2 | 2 000 | `Draw` | 101 993 | 835 | 162 444 | 0.628 |
| 3 | 2 000 | `Draw` | 74 074 | 478 | 160 555 | 0.461 |
| 4 | 2 000 | `Draw` | 64 217 | 533 | 157 878 | 0.407 |
| 5 | 1 557 | `Faction1Victory` | 44 741 | 434 | 135 740 | 0.330 |
| 6 | 2 000 | `Draw` | 68 133 | 574 | 163 339 | 0.417 |
| 7 | 2 000 | `Draw` | 71 931 | 470 | 174 584 | 0.412 |
| 8 | 2 000 | `Draw` | 70 370 | 831 | 164 205 | 0.429 |
| 9 | 2 000 | `Draw` | 57 410 | 451 | 165 332 | 0.347 |
| 10 | 2 000 | `Draw` | 73 773 | 890 | 141 801 | 0.520 |
| 11 | 2 000 | `Draw` | 46 543 | 495 | 157 749 | 0.295 |
| 12 | 2 000 | `Draw` | 76 835 | 568 | 160 637 | 0.478 |
| 13 | 1 954 | `Faction0Victory` | 63 152 | 468 | 143 865 | 0.439 |
| 14 | 2 000 | `Draw` | 86 436 | 904 | 175 575 | 0.492 |
| 15 | 2 000 | `Draw` | 87 311 | 531 | 176 201 | 0.495 |
| 16 | 1 863 | `Faction1Victory` | 41 902 | 315 | 133 677 | 0.313 |
| 17 | 1 832 | `Faction0Victory` | 83 935 | 851 | 153 664 | 0.546 |
| 18 | 2 000 | `Draw` | 63 158 | 284 | 157 190 | 0.402 |
| 19 | 2 000 | `Draw` | 76 851 | 384 | 150 247 | 0.512 |
| 20 | 2 000 | `Draw` | 103 173 | 901 | 175 478 | 0.588 |

Four readings, the first of which is the reason the sweep was worth running:

- **A 73% gap between two seeds is ordinary variance, not a finding.**
  `blockedAgentTicks` ranges from 41 902 to 103 173 across these twenty seeds,
  a spread of 146% between the extremes, with a mean of 71 764 and a median of
  72 852. Section 3 was right to refuse to generalize from two samples, and
  section 1's second-round number should not be read as a distinct event. Any
  future change here must be measured across a sweep, because a two-seed
  comparison cannot clear this noise floor.
- **The army is never blocked more than it is able to fight.** The blocked
  against attack-capable ratio stays between 0.295 and 0.628 in every run. The
  single sharpest statement in section 2 — round 2's ratio of 1.20, more
  agent-ticks blocked than attack-capable — has no counterpart anywhere in
  these twenty runs.
- **The longest blocked streak is the number that should worry a reader.** It
  reaches 904 ticks on seed 14 and exceeds 800 on four seeds. At a tick rate of
  20 that is 45 seconds of one warrior standing still, in plain view, and the
  worst case in section 2 was 178 ticks. The ratio reading above and this one
  point in opposite directions: blocking is spread more thinly across the army
  than it was, and the worst individual case is far longer.
- **Sixteen of the twenty runs did not terminate.** They reached the 2 000-tick
  cap as a `Draw`; only seeds 5, 13, 16, and 17 produced a winner. Termination
  at 500 agents is its own open question and is not what this document set out
  to measure, but a reader comparing outcome columns should know that the
  undecided cap is the common case rather than the exception.

Section 3's third caveat still stands unchanged: **no cause is identified.**
The sweep bounds the symptom across seeds and says nothing about whether the
blocking comes from contingent shape, approach geometry, or the preset's speed
and radius values. What it removes is the temptation to chase the difference
between two particular seeds.

The twenty reports were written to `artifacts/blocking-sweep/seed-NN.json`.
`artifacts/` is not tracked, so the figures are reproduced above in full and
the directory can be deleted without losing them.

## 6. Audit, 2026-08-15 — the sweep's movement preset is stale

Section 5 says its sweep measures what a spectator sees, and half of that claim
has since expired. The combat half still holds: the client still builds with
`CombatPresetId.PrecolonialPhilippinesV5`, at
`src/Hukbo.Client/ArenaGame.cs:1585`. The movement half does not. The client's
shipped movement default has moved from `LastStandEngagementV11` to
`MovementPresetId.CohortLateralSpreadV13`, set in
`src/Hukbo.Client/Settings/ClientSettingsStore.cs:113-114` and threaded through
`ArenaGame.cs:379` into `BuildScenario` at `ArenaGame.cs:414-417`. A spectator
watching the game today is not watching V11.

The section 5 table is therefore a record of `LastStandEngagementV11` under
combat V5, and it is no longer a current baseline for what the client runs. It
stays where it is, unedited, exactly as section 2 stays as the record of what
was measured under combat V4. Read it as history, not as the number a change
has to beat.

What a future change here needs first is the same twenty-seed sweep re-run
under `CohortLateralSpreadV13`, so that the comparison is against the movement
rules a spectator actually sees. Until that re-run exists, no claim about
improvement or regression in blocking under the shipped client can be made from
this document.

The document remains re-measurable, which is the reason it is still worth
keeping. Every counter its tables quote is still emitted by the simulation and
still reaches the headless JSON report. `blockedAgentTicks`,
`attackCapableAgentTicks`, `longestBlockedStreakTicks`, `candidatePairs`,
`contactPairs`, `acceptedMoves`, `maximumFrontWidthRaw`, `maximumFrontDepthRaw`,
and `maximumPenetrationRaw` are on
`src/Hukbo.Core/Simulation/CollisionMetrics.cs:73-81`; `acceptedAttacks` and
`landedAttacks` are on `src/Hukbo.Core/Simulation/CombatMetrics.cs:50-51`; and
all eleven are written out through
`src/Hukbo.Headless/HeadlessRunner.cs:438-450`. The sweep can be reproduced by
changing one flag.
