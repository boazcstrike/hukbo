# Movement V7 pressure interrupt — calibration record (task E1)

Task **E1** of `docs/plans/2026-07-31-movement-v7-pressure-interrupt.md`. This
document records the tuning search over the three shared pressure-interrupt
weights and the six per-row thresholds of
`MovementPresetId.EquipmentRelativeFootworkV7`, every candidate configuration
that was measured, and the measured result of each one.

**Every weight and every threshold recorded here is a provisional
reconstruction of gameplay tuning under `CLAUDE.md` section 7, not a historical
measurement.** No source describes how a warrior in the pre-colonial
Philippines decided to break off a committed blow, and nothing in this document
claims that any of these numbers does.

---

## 1. Verdict

**The design section 2.1 termination bar is not met, and task E1 could not
meet it at any tuning of the values it owns.**

Seeds 1, 2, 3, 5, and 8, at both two hundred and five hundred agents, were
required each to reach a decisive outcome within 6,000 ticks. At the values
shipped by this task, **all ten cells ended in `Draw` at the 10,000-tick
limit**. Not one cell passed. Every other configuration measured during the
search drew every cell it was measured over as well.

The design section 2.3 phase-flip criterion **is** met at the shipped values:
the redefined metric sits between 2.68% and 11.93% against a ceiling of 25%.

The design section 2.2 `p50Milliseconds` budget is **not** met either. Decision
D2 says a `p50` failure is separate work rather than a calibration failure
*when the termination bar passes*; the bar does not pass here, so the `p50`
reading is reported below as an unqualified failure and not as deferred work.

This is the outcome design section 11, open question 3 recorded in advance:

> **Whether the interrupt is enough on its own.** If V7 with the interrupt and
> tuned thresholds still fails the section 2.1 termination bar, the remaining
> cause is elsewhere and this design does not predict where.

Section 5 below says where the evidence points.

---

## 2. Shipped values

| Value | Registered | Note |
| --- | --- | --- |
| `SupportPressureWeightBasisPoints` | 5,000 | unchanged from what task B3 registered |
| `IncomingDamageWeightBasisPoints` | 3,000 | unchanged |
| `AllyCollapseWeightBasisPoints` | 2,000 | unchanged |
| Kampilan threshold | 10,000 | was 20,000 — unreachable |
| Wasay threshold | 10,000 | was 20,000 — unreachable |
| Kalis threshold | 7,500 | was 15,000 |
| Itak threshold | 6,250 | was 12,500 |
| Tall-hardwood-shield Kalis threshold | 8,750 | was 17,500 — effectively unreachable |
| Tall-hardwood-shield Itak threshold | 7,500 | was 15,000 |

**The reachability ceiling at these weights is
`2 * 5,000 + 10,000 = 20,000` basis points, and the weighted sum is strictly
below it for any warrior that survives the tick.** Every shipped threshold is
at or below half of that ceiling.

Each threshold is the row's own `DisengageEnemyToAllyBasisPoints` scaled by
`SupportPressureWeightBasisPoints / 10,000`. The six rows therefore keep the
relative ordering the six weapon sessions recorded — Kampilan and Wasay
highest, then the shielded Kalis row, then Kalis and the shielded Itak row,
then Itak lowest — while every one of them becomes reachable.

### 2.1 Why the starting values were unreachable, verified against the code

`WeaponMovementRules.ComputeWeightedPressure` returns

```
(A * w1) + (B * w2) + (C * w3)
```

where the three weights are validated to total exactly 10,000
(`MovementRuleset.cs`, the coupled-validation branch), so the result is a true
weighted average scaled by `RatioBasisPointScale`. A weighted average can never
exceed its largest input. For a warrior that survives the tick:

- signal A saturates at `WeaponMovementRules.SignalCeilingBasisPoints` = 30,000;
- signal B reaching 10,000 requires `damageTakenLastTick >= maximumHitPoints`,
  which kills the agent, so a survivor's B is strictly below 10,000;
- signal C is naturally capped at 10,000, because the allies lost cannot exceed
  the prior count.

The maximum reachable weighted average is therefore

```
(30,000 * w1 + 10,000 * w2 + 10,000 * w3) / 10,000
    = 3 * w1 + (w2 + w3)
    = 3 * w1 + (10,000 - w1)
    = 2 * w1 + 10,000
```

and strictly below that, since B is strictly below 10,000. At `w1 = 5,000` the
ceiling is **20,000**. The Kampilan and Wasay rows were registered at exactly
20,000 and so could never fire at all; the shielded Kalis row at 17,500 needed
all three signals saturated simultaneously. Task E0's matrix measured exactly
that: zero firings on `KP`, `WA`, and `KS` across all ten cells, against 113,
120, and 137 on the three rows below the ceiling.

The cause was a unit error rather than a bad number. Task B3 seeded each row's
threshold from its `DisengageEnemyToAllyBasisPoints` on the reasoning that "the
weighted pressure sum and that ratio are measured in the same basis-point
space". They are measured in the same units but they are not the same quantity:
the ratio is signal A raw, and the sum is a weighted average in which A carries
only `w1 / 10,000` of the vote. Scaling each ratio by `w1 / 10,000` restores the
reading the comment intended — *a warrior interrupts at roughly the odds at
which it would already have refused to close* — and is what the shipped values
do.

Two consequences of the arithmetic that shaped the search:

1. A threshold can only fire if it is strictly below `2 * w1 + 10,000`, so
   raising `SupportPressureWeightBasisPoints` raises the ceiling for all six
   rows at once.
2. Reaching that ceiling needs all three signals saturated at the same instant,
   which is rare. For a typical engaged warrior, B and C sit near zero and the
   weighted average is approximately `A * w1 / 10,000`. At `w1 = 5,000` a
   warrior facing two enemies per supporting ally produces only 10,000 — below
   every threshold registered before this task.

---

## 3. The search

Six configurations were measured. Cell counts differ: the intermediate
candidates were narrowed to keep iteration under twenty seconds, and the
shipped configuration was measured over the full ten-cell matrix. Every number
below is taken from harness output; none is estimated.

The harness is `tests/Hukbo.Core.Tests/Movement/PressureInterruptCalibrationHarness.cs`,
run as

```powershell
dotnet test tests/Hukbo.Core.Tests -c Release `
  -p:DefineConstants=HUKBO_CALIBRATION `
  --filter FullyQualifiedName~PressureInterruptCalibrationRun `
  --logger "console;verbosity=detailed"
```

with `HUKBO_CALIBRATION_SEEDS` and `HUKBO_CALIBRATION_AGENTS` narrowing the
matrix. Combat preset `PrecolonialPhilippinesV2` is pinned by the harness,
`bodyRadiusRaw` is 4,352, and `requestedTicks` is 10,000 in every run.

| # | Weights (support / damage / collapse) | Thresholds `KP WA KA IT KS IS` | Matrix measured | Cells decisive within 6,000 ticks | Total firings | Flip % range |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 5,000 / 3,000 / 2,000 | 20,000 20,000 15,000 12,500 17,500 15,000 | full ten cells (task E0); re-confirmed here on 500 agents, seeds 1 and 8 | 0 of 10 | 370 over E0's ten cells | 2.4 – 13.6 (E0) |
| 2 | 5,000 / 3,000 / 2,000 | 1 1 1 1 1 1 | 200 agents, seeds 1, 3, 8; then 500 agents, seeds 1, 8 | 0 of 5 | 3,491 (200) + 6,274 (500) | 5.76 – 13.47 |
| 3 | 5,000 / 3,000 / 2,000 | 10,000 10,000 7,500 6,250 8,750 7,500 | 200 agents, seeds 1, 3, 8; then the full ten cells | 0 of 10 | 765 (narrow) / 2,751 (full) | 2.68 – 11.93 |
| 4 | 7,000 / 2,000 / 1,000 | 14,000 14,000 10,500 8,750 12,250 10,500 | 200 agents, seeds 1, 3, 8 | 0 of 3 | 758 | 8.92 – 11.86 |
| 5 | 5,000 / 3,000 / 2,000 | 5,000 5,000 3,750 3,125 4,375 3,750 | 200 agents, seeds 1, 3, 8 | 0 of 3 | 2,027 | 7.83 – 12.65 |
| 6 | 3,000 / 5,000 / 2,000 | 6,000 6,000 4,500 3,750 5,250 4,500 | 200 agents, seeds 1, 3, 8 | 0 of 3 | 774 | 8.35 – 12.17 |

Configuration **3** is what ships.

### 3.1 Candidate 1 — the shipped starting values, re-confirmed

Task E0 measured the full matrix at these values and reported ten `Draw`s at
10,000 ticks with zero firings on `KP`, `WA`, and `KS`. Before changing
anything, the two hardest cells were re-run to confirm the harness and the
worktree agreed with that record.

```
rowThresholdsBp       : KP=20000 WA=20000 KA=15000 IT=12500 KS=17500 IS=15000
   500 agents seed 1: Draw at tick 10000 -> outside the bar
   500 agents seed 8: Draw at tick 10000 -> outside the bar
   500 agents: median p50 = 0.9613 ms over 2 seeds
```

### 3.2 Candidate 2 — the maximum-intervention probe

This is the most informative measurement in the search, and it was taken
second, before any tuning, because it bounds everything that follows. Every row
is registered at the minimum threshold the constructor allows, 1 basis point.
The predicate then returns `true` for **every** `(agent, tick)` pair it can
ever return `true` for: any living warrior whose prior phase is `Commit` or
`Recover` and who has any pressure at all. No other configuration can fire more
often, because no other configuration can fire on a pair this one does not.

200 agents, seeds 1, 3, 8:

```
rowThresholdsBp       : KP=1 WA=1 KA=1 IT=1 KS=1 IS=1
agents  seed  terminalTick  outcome           F0    F1     p50(ms)   p95(ms)   max(ms)  warmP50(ms)  flips  flipObs  flip%
   200     1         10000  Draw                58    63     0.2709    0.4495    0.9691       0.2568   5435    59818    9.09
   200     3         10000  Draw                70    69     0.2619    0.3743    0.8996       0.2621   4181    58010    7.21
   200     8         10000  Draw                39    38     0.1198    0.3510    0.8237       0.1186   7490    55617   13.47

row  spawnAgents  livingAgentTicks  firings
 KP          102            686284      531
 WA          102            588323      447
 KA           99            615880      634
 IT           99            598007      667
 KS           99            636427      610
 IS           99            618955      602
```

500 agents, seeds 1 and 8 — the cells where the bar binds hardest:

```
rowThresholdsBp       : KP=1 WA=1 KA=1 IT=1 KS=1 IS=1
agents  seed  terminalTick  outcome           F0    F1     p50(ms)   p95(ms)   max(ms)  warmP50(ms)  flips  flipObs  flip%
   500     1         10000  Draw               149   159     1.0981    1.8820    3.4399       1.1000   8352   144920    5.76
   500     8         10000  Draw               128   133     0.7732    1.4116    2.9459       0.7736  11082   136851    8.10

row  spawnAgents  livingAgentTicks  firings
 KP          168           1065767      823
 WA          168           1034367      642
 KA          166            973307     1121
 IT          166           1009376     1263
 KS          166            982800     1131
 IS          166            934533     1294
```

Every cell still drew. The reading that matters is the ratio of firings to
living agent-ticks: 3,491 firings against 3,743,876 living agent-ticks at 200
agents — 0.093% — and 6,274 against 6,000,150 at 500 agents — 0.105%. **Even
firing on every eligible agent-tick, the interrupt touches roughly one tenth of
one per cent of the simulation.**

### 3.3 Candidate 3 — the shipped values, narrowed

```
rowThresholdsBp       : KP=10000 WA=10000 KA=7500 IT=6250 KS=8750 IS=7500
agents  seed  terminalTick  outcome           F0    F1     p50(ms)   p95(ms)   max(ms)  warmP50(ms)  flips  flipObs  flip%
   200     1         10000  Draw                81    82     0.3404    0.4305    1.2897       0.3384   5332    59804    8.92
   200     3         10000  Draw                39    39     0.2033    0.3836    1.9903       0.2042   5194    57846    8.98
   200     8         10000  Draw                27    24     0.0848    0.3263    0.9231       0.0842   6601    55334   11.93

row  spawnAgents  livingAgentTicks  firings
 KP          102            641856      138
 WA          102            610159       71
 KA           99            567649      176
 IT           99            514966      149
 KS           99            555937      153
 IS           99            595759       78
```

All six rows fire, which is the reachability defect fixed. The 200-agent seed-8
cell finishes with 51 warriors alive against the 89 that `EquipmentRelativeFootworkV6`
left standing on the same cell, so the interrupt does grind the standoff down
faster — just nowhere near fast enough, and not at all on seed 1, which
finishes with 163 alive against V6's 151.

### 3.4 Candidate 4 — raising the support weight

Weights 7,000 / 2,000 / 1,000 raise the reachability ceiling to 24,000, which
would let the pre-E1 thresholds of 20,000 fire. Thresholds were scaled by the
same `w1 / 10,000` rule, giving 14,000 / 14,000 / 10,500 / 8,750 / 12,250 /
10,500.

```
rowThresholdsBp       : KP=14000 WA=14000 KA=10500 IT=8750 KS=12250 IS=10500
agents  seed  terminalTick  outcome           F0    F1     p50(ms)   p95(ms)   max(ms)  warmP50(ms)  flips  flipObs  flip%
   200     1         10000  Draw                81    85     0.3672    0.4506    1.3957       0.3645   5332    59804    8.92
   200     3         10000  Draw                40    26     0.0920    0.3731    0.7543       0.0932   5274    57855    9.12
   200     8         10000  Draw                42    47     0.1774    0.3357    0.8257       0.1767   6563    55350   11.86

row  spawnAgents  livingAgentTicks  firings
 KP          102            631776      133
 WA          102            633478      100
 KA           99            551103      182
 IT           99            501575      117
 KS           99            588895      124
 IS           99            616964      102
```

Three draws, 758 firings — indistinguishable from candidate 3 in every respect
that matters, while giving up two fifths of the incoming-damage weight and half
the ally-collapse weight. Rejected.

### 3.5 Candidate 5 — halving the shipped thresholds

Thresholds at half of candidate 3, to sample the space between candidate 3 and
the maximum-intervention probe.

```
rowThresholdsBp       : KP=5000 WA=5000 KA=3750 IT=3125 KS=4375 IS=3750
agents  seed  terminalTick  outcome           F0    F1     p50(ms)   p95(ms)   max(ms)  warmP50(ms)  flips  flipObs  flip%
   200     1         10000  Draw                86    88     0.3667    0.4540    1.0355       0.3679   5381    59786    9.00
   200     3         10000  Draw                66    61     0.2467    0.4076    0.9457       0.2481   4511    57608    7.83
   200     8         10000  Draw                19    27     0.0434    0.3511    0.9044       0.0436   7128    56353   12.65

row  spawnAgents  livingAgentTicks  firings
 KP          102            722041      205
 WA          102            627255      217
 KA           99            596133      409
 IT           99            601344      506
 KS           99            652500      235
 IS           99            579332      455
```

Firings rise from 765 to 2,027. Terminal tick does not move: three draws at
10,000. The 200-agent seed-8 cell reaches 46 alive, the lowest survivor total
seen anywhere in the search, and still does not resolve. Rejected in favour of
candidate 3 because it buys nothing on the criterion that decides the task and
loses the derivation that makes each threshold mean something.

### 3.6 Candidate 6 — leaning on incoming damage instead

Weights 3,000 / 5,000 / 2,000, thresholds scaled by the same rule.

```
rowThresholdsBp       : KP=6000 WA=6000 KA=4500 IT=3750 KS=5250 IS=4500
agents  seed  terminalTick  outcome           F0    F1     p50(ms)   p95(ms)   max(ms)  warmP50(ms)  flips  flipObs  flip%
   200     1         10000  Draw                57    56     0.1981    0.3975    0.8314       0.1971   5332    59804    8.92
   200     3         10000  Draw                46    36     0.1315    0.3642    1.2316       0.1316   4790    57331    8.35
   200     8         10000  Draw                63    67     0.2618    0.3833    2.3452       0.2621   6739    55352   12.17

row  spawnAgents  livingAgentTicks  firings
 KP          102            688240       95
 WA          102            649161       69
 KA           99            553902      128
 IT           99            516924      188
 KS           99            585612      128
 IS           99            564641      166
```

Three draws, and *fewer* firings than candidate 3 despite lower thresholds,
because signal B for a warrior that survives the tick is small: a hit costs a
fraction of maximum hit points, so B contributes little regardless of its
weight. Rejected.

---

## 4. The full ten-cell matrix at the shipped values

This is the run that decides the task. It is the complete harness report, ten
cells, seeds 1, 2, 3, 5, 8 at 200 and 500 agents.

```
Hukbo movement V7 pressure-interrupt calibration harness (task E0)
Measurement only. Nothing here asserts, passes, or fails.

movementPreset        : EquipmentRelativeFootworkV7
combatPreset          : PrecolonialPhilippinesV2 (pinned)
bodyRadiusRaw         : 4352
requestedTicks        : 10000
seeds                 : 1, 2, 3, 5, 8
agentCounts           : 200, 500
usesFootwork          : True
appliesInterrupt      : True
weightsBasisPoints    : support=5000, damage=3000, allyCollapse=2000
rowThresholdsBp       : KP=10000 WA=10000 KA=7500 IT=6250 KS=8750 IS=7500
flipWindow            : ticks 101 through 400 inclusive
operatingSystem       : Microsoft Windows 10.0.26200
framework             : .NET 10.0.10
processArchitecture   : X64
processorCount        : 20

== Cells: one discarded warm run then one measured run each ==
agents  seed  terminalTick  outcome           F0    F1     p50(ms)   p95(ms)   max(ms)  warmP50(ms)  flips  flipObs  flip%
   200     1         10000  Draw                81    82     0.3335    0.4064    1.0404       0.3329   5332    59804    8.92
   200     2         10000  Draw                30    38     0.0991    0.3550    1.1888       0.0992   3955    58198    6.80
   200     3         10000  Draw                39    39     0.2020    0.3650    1.1281       0.2018   5194    57846    8.98
   200     5         10000  Draw                76    68     0.2992    0.3965    1.1219       0.2999   1608    59973    2.68
   200     8         10000  Draw                27    24     0.0836    0.3242    0.6555       0.0829   6601    55334   11.93
   500     1         10000  Draw               192   195     1.6205    1.9454    4.7970       1.6209  10818   146080    7.41
   500     2         10000  Draw               149   144     0.9279    1.6883    3.1189       0.9277  13866   142462    9.73
   500     3         10000  Draw               122   115     0.7002    1.3619    3.4103       0.7003  10770   135454    7.95
   500     5         10000  Draw               168   167     1.1980    1.6821    9.2709       1.1943  12649   141524    8.94
   500     8         10000  Draw               110   102     0.6088    1.3010    4.6827       0.6061  14490   135366   10.70

flip% is the redefined design section 2.3 metric: the share of living agent-ticks in the
window on which the posture, the intent, or a non-scripted footwork phase transition moved.
The two excluded transitions are any change into Commit and Commit to Recover. An interrupt
firing is a Commit or Recover to Disengage transition and is counted, by design.

== Median p50 per agent count, the design section 2.2 budget denominator ==
   200 agents: median p50 = 0.2020 ms over 5 seeds
   500 agents: median p50 = 0.9279 ms over 5 seeds

== Termination bar, design section 2.1: every cell decisive within 6,000 ticks ==
   200 agents seed 1: Draw at tick 10000 -> outside the bar
   200 agents seed 2: Draw at tick 10000 -> outside the bar
   200 agents seed 3: Draw at tick 10000 -> outside the bar
   200 agents seed 5: Draw at tick 10000 -> outside the bar
   200 agents seed 8: Draw at tick 10000 -> outside the bar
   500 agents seed 1: Draw at tick 10000 -> outside the bar
   500 agents seed 2: Draw at tick 10000 -> outside the bar
   500 agents seed 3: Draw at tick 10000 -> outside the bar
   500 agents seed 5: Draw at tick 10000 -> outside the bar
   500 agents seed 8: Draw at tick 10000 -> outside the bar

The lines above are arithmetic on measured numbers, not a verdict. Task E1 owns the verdict.

== Per-row pressure-interrupt firings ==
A firing is one (agent, tick) pair on which the interrupt predicate returned true, read from
BattleSimulation's per-tick scratch and not from the persistent break-off flag, so it is a count
of break-offs and not of ticks spent broken off. spawnAgents distinguishes a row that was
fielded and never fired from a row this cell never fielded at all.

agents  seed  row  spawnAgents  livingAgentTicks  firings  note
   200     1   KP           34            298065       23
   200     1   WA           34            307000       10
   200     1   KA           33            258532       38
   200     1   IT           33            247853       33
   200     1   KS           33            260594       25
   200     1   IS           33            302990        0  fielded, never fired
   200     2   KP           34            176425       32
   200     2   WA           34            173620       24
   200     2   KA           33            136954       55
   200     2   IT           33            156379       52
   200     2   KS           33            146320       71
   200     2   IS           33            149812      126
   200     3   KP           34            214188       68
   200     3   WA           34            173472       43
   200     3   KA           33            184395       65
   200     3   IT           33            145539       64
   200     3   KS           33            183806       62
   200     3   IS           33            184514       24
   200     5   KP           34            315181        8
   200     5   WA           34            258702       19
   200     5   KA           33            250325       62
   200     5   IT           33            235023       37
   200     5   KS           33            229307       41
   200     5   IS           33            273910       39
   200     8   KP           34            129603       47
   200     8   WA           34            129687       18
   200     8   KA           33            124722       73
   200     8   IT           33            121574       52
   200     8   KS           33            111537       66
   200     8   IS           33            108255       54
   500     1   KP           84            677888        7
   500     1   WA           84            718279       10
   500     1   KA           83            602074       17
   500     1   IT           83            621339       59
   500     1   KS           83            689479        9
   500     1   IS           83            619449       27
   500     2   KP           84            521450       37
   500     2   WA           84            556598       10
   500     2   KA           83            489502       29
   500     2   IT           83            491135       40
   500     2   KS           83            511325       15
   500     2   IS           83            482689       18
   500     3   KP           84            452362       29
   500     3   WA           84            410607       38
   500     3   KA           83            410726       74
   500     3   IT           83            380127      147
   500     3   KS           83            447904       44
   500     3   IS           83            404762       84
   500     5   KP           84            566176       67
   500     5   WA           84            610508       17
   500     5   KA           83            571791       56
   500     5   IT           83            533767       71
   500     5   KS           83            554210       43
   500     5   IS           83            592890       72
   500     8   KP           84            406364       67
   500     8   WA           84            369220       46
   500     8   KA           83            366216       91
   500     8   IT           83            366735      116
   500     8   KS           83            423689       42
   500     8   IS           83            346753       38

== Per-row totals across every measured cell ==
row  spawnAgents  livingAgentTicks  firings
 KP          590           3757702      385
 WA          590           3707693      235
 KA          580           3395237      560
 IT          580           3299471      671
 KS          580           3558171      418
 IS          580           3466024      482
```

### 4.1 Both criteria, per cell

| Agents | Seed | Terminal tick | Outcome | Termination bar (≤ 6,000 and decisive) | Flip % | Flip ceiling 25% |
| --- | --- | --- | --- | --- | --- | --- |
| 200 | 1 | 10,000 | `Draw` | **FAIL** | 8.92 | PASS |
| 200 | 2 | 10,000 | `Draw` | **FAIL** | 6.80 | PASS |
| 200 | 3 | 10,000 | `Draw` | **FAIL** | 8.98 | PASS |
| 200 | 5 | 10,000 | `Draw` | **FAIL** | 2.68 | PASS |
| 200 | 8 | 10,000 | `Draw` | **FAIL** | 11.93 | PASS |
| 500 | 1 | 10,000 | `Draw` | **FAIL** | 7.41 | PASS |
| 500 | 2 | 10,000 | `Draw` | **FAIL** | 9.73 | PASS |
| 500 | 3 | 10,000 | `Draw` | **FAIL** | 7.95 | PASS |
| 500 | 5 | 10,000 | `Draw` | **FAIL** | 8.94 | PASS |
| 500 | 8 | 10,000 | `Draw` | **FAIL** | 10.70 | PASS |

Zero of ten cells meet the termination bar. Ten of ten meet the phase-flip
ceiling, with the worst cell at 11.93% against 25%.

The phase-flip headroom is not free, and it did not come from the interrupt
staying quiet. Firings rose from task E0's 370 across ten cells to 2,751 across
the same ten cells, and the metric still sits under half its ceiling. Even the
maximum-intervention probe of section 3.2, with roughly ten times the firing
rate again, peaked at 13.47%. The interrupt cannot exhaust this budget at any
tuning, for the same reason it cannot terminate a battle: there are too few
ticks on which it is allowed to act.

### 4.2 The `p50Milliseconds` budget

| Agents | V4 reference median | Ceiling | V7 median measured | Ratio | Verdict |
| --- | --- | --- | --- | --- | --- |
| 200 | 0.0607 ms | 2.0× (0.1214 ms) | 0.2020 ms | 3.33× | **FAIL** |
| 500 | 0.2275 ms | 2.5× (0.5688 ms) | 0.9279 ms | 4.08× | **FAIL** |

The V4 reference medians are taken from
`docs/plans/2026-07-31-movement-v7-baseline.md`, which records them explicitly:
`PersistentContingentsV4 200: sorted=0.0519, 0.0583, 0.0607, 0.0657, 0.1195
median=0.0607` and `PersistentContingentsV4 500: sorted=0.1324, 0.2087, 0.2275,
0.3316, 0.3701 median=0.2275`.

Decision D2 makes a `p50` failure separate work rather than a calibration
failure **when the termination bar passes**. It does not pass, so this is
recorded as a plain failure and not as deferred work. It is also not a cost the
interrupt introduced: the equipment-relative footwork of V6 already carried it
at zero firings, and the V6 baseline medians in the same document are 0.2984 ms
at 200 agents — above V7's 0.2020 ms — and 0.8666 ms at 500 agents, against
V7's 0.9279 ms. The 500-agent gap of 0.06 ms is the only part of the overrun
that could plausibly be charged to this feature; the remaining 3.5× is V6's.

---

## 5. Why the interrupt cannot reach the standoff

This section is the part of the record worth the most to the tasks after E1.

The pressure interrupt is gated by the transition-only rule in
`WeaponMovementRules.ShouldPressureInterrupt`: it returns `false` unless the
agent's prior phase was `FootworkPhase.Commit` or `FootworkPhase.Recover`. The
interrupt exists to preempt the attack lifecycle, so it can only act on a
warrior already inside it.

The V6 standoff is a *refusal to enter that lifecycle at all*. The baseline
document measured it directly, from the 200-agent seed-1 cell's
`movementMetrics` block:

| Metric | Agent-ticks |
| --- | --- |
| `refuseAgentTicks` | 1,140,221 |
| `regroupAgentTicks` | 338,634 |
| `commitAgentTicks` | 2,216 |
| `recoverAgentTicks` | 2,017 |

That is 1,478,855 agent-ticks refusing or regrouping against 4,233 committing
or recovering, a ratio of about 349 to 1. **The interrupt's entire addressable
population is those 4,233 agent-ticks — under three tenths of one per cent of
the run.**

Section 3.2's probe confirms the consequence empirically rather than by
inference. Registering the minimum threshold on every row makes the predicate
fire on every agent-tick it is capable of firing on, and that produced 3,491
firings across three 200-agent cells and 6,274 across two 500-agent cells,
against 3.74 million and 6.00 million living agent-ticks respectively. No
tuning can exceed those counts, because no tuning can make the predicate
eligible on a tick the transition rule excludes.

So the shape of the result across the whole search is: **on the three 200-agent
cells measured under every candidate, firings ranged from 758 to 3,491 — a
factor of 4.6 between the quietest tuning and the loudest possible one — and
the terminal tick did not move by a single tick in any cell.** Interrupting a
warrior more often does not help,
because warriors are not the ones holding the battle open — the warriors who
never commit are, and the interrupt has no opinion about them.

This is the answer to design section 11's open question 3, and it is a negative
one. The interrupt is not enough on its own, and the remaining cause is
upstream of it, in whatever keeps `FootworkPhase.Refuse` and the regroup
posture occupied for 349 ticks out of every 350. Section 11's open question 2 —
the shielded Kalis row spending 162 of 400 ticks in `Refuse` and not reaching
its first `Commit` until tick 259 — now reads as a symptom of the same thing
rather than as a separate curiosity.

**What this record does not do is propose a fix.** Naming the cause as
"upstream of the interrupt" is as far as the measurement goes; which of the
refuse conditions, the regroup cycle, the cohesion duty window, or the
approach-sidestep rules is responsible is a separate investigation, and the
design explicitly says it does not predict where.

---

## 6. What ships, and what does not

The values in section 2 ship. They fix a real defect — three of six rows could
not fire at all — they keep the relative ordering the six weapon sessions
recorded, they hold the phase-flip metric at well under half its ceiling, and
they are derived by a rule that can be stated in one sentence rather than
guessed. They do not meet the termination bar, and this document does not claim
they do.

Nothing else moves. `Scenario.MovementPreset` remains
`PersistentContingentsV4` under decision D6, and this record is evidence
*against* flipping it, not for it. `MovementPresetId.EquipmentRelativeFootworkV7`
stays reachable only by explicit selection. No V7 `ContentHash` is pinned here;
task E2 pins it now that these values are final. Presets V1 through V6 are
untouched, and their six pinned content hashes and six trajectory digests do
not move.
