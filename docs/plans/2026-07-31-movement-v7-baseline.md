# Movement V7 pre-change baseline — measured record

Date: 2026-07-31

Status: **measurement only. No source file, test, fixture, or script was
changed by this task. No pass or fail verdict is asserted here.**

This document discharges task A0 of
[`2026-07-31-movement-v7-pressure-interrupt.md`](2026-07-31-movement-v7-pressure-interrupt.md).
It records the "before" state that task F2 will later compare a V7 run against.
It deliberately stops short of the comparison. The 2.0× and 2.5×
`p50Milliseconds` ceilings described in section 2.2 of
[`2026-07-31-movement-v7-pressure-interrupt-design.md`](2026-07-31-movement-v7-pressure-interrupt-design.md)
are not evaluated here, because there is nothing yet to evaluate them against.
The medians below are the denominator that task F2 will divide into, and nothing
more.

Every number in this document was produced by a run performed while writing it.
No figure has been carried over from an earlier session, estimated, or
interpolated.

---

## 1. What was measured

Twenty cells: two movement presets, two agent counts, five seeds.

| Axis | Values |
| --- | --- |
| Movement preset | `PersistentContingentsV4`, `EquipmentRelativeFootworkV6` |
| Agent count | 200, 500 |
| Seed | 1, 2, 3, 5, 8 |
| Combat preset | `PrecolonialPhilippinesV2`, pinned explicitly on every run |
| Requested ticks | 10,000 |
| Configuration | `Release` |

The combat preset is pinned rather than left to default. The shipped default is
`CombatPresetId.PrecolonialPhilippinesV4`
(`src/Hukbo.Core/Simulation/Scenario.cs:67-68`), and the V4 roster never pairs a
shield with any weapon. The preset file says so in its own words at
`src/Hukbo.Core/Combat/PhilippineCombatPresetV4.cs:194-197`:

```
        // Restated exactly from PhilippineCombatPresetV3. V4's roster never
        // pairs a shield with any weapon, so only the ShieldId.None row is
        // ever resolved; ShieldId.TallHardwood is carried across unchanged
        // so this data stays a faithful copy rather than a partial one.
```

A workload run under the shipped default would therefore never field the
shielded Kalis or shielded Itak rows. Those two rows are exactly the ones whose
zero-tick attack-lifecycle window motivates the entire pressure-interrupt
change, so a baseline taken under the default would have measured the wrong
thing. `PrecolonialPhilippinesV2` is pinned for the same reason the V6 freeze
fixture pins it.

Each cell was run twice against the same already-built `Release` binaries. The
first run of each pair is the discarded warm run required by design section 2.2.
The second run of each pair is the measured run, and is the run every figure in
the twenty-cell table comes from. Both runs of every pair are recorded in
section 6 so the discard is visible rather than merely claimed.

---

## 2. Machine identification

Recorded to satisfy `SIMULATION-GAME-STANDARDS.md` section 8, which requires a
performance report to name its hardware, runtime, and release profile.

| Item | Value |
| --- | --- |
| CPU | Intel(R) Core(TM) i5-14600K, 14 physical cores, 20 logical processors, 3500 MHz max clock |
| RAM | 34,063,441,920 bytes total physical (31.7 GiB) |
| GPU | NVIDIA GeForce RTX 4070 SUPER (not exercised; the headless runner opens no window) |
| Operating system | Microsoft Windows 11 Pro, version 10.0.26200, build 26200 |
| Power mode | Balanced (`Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e`) |
| .NET SDK | 10.0.302, as pinned by `global.json` |
| Runtime reported by the run | `.NET 10.0.10`, `X64`, `processorCount` 20 |
| Release profile | `Release`, built once before the matrix and reused for every cell via `-NoBuild` |
| Repository commit at measurement time | `b70a2d181680dd544c0a473d36257dd0f25c12a5` |
| Working tree at measurement time | one unrelated modified file, `docs/research/movement/tall-hardwood-shield.md`, which is task A1's documentation-only edit and touches no source |

Task A1 landed its research-note edit as commit `c99eb25` while this document was
being written, so `HEAD` had moved on by the time this file was committed. That
commit changes one Markdown file under `docs/research/movement/` and no compiled
code, so the `Release` binaries every cell measured are the binaries built from
`b70a2d1` and are unaffected by it. The commit identifier is recorded here as
`b70a2d1` because that is the commit whose source produced the measured numbers.

Raw output of the machine query:

```
Name                      : Intel(R) Core(TM) i5-14600K
NumberOfCores             : 14
NumberOfLogicalProcessors : 20
MaxClockSpeed             : 3500

34063441920
Microsoft Windows 11 Pro
10.0.26200
26200
Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)
10.0.302
```

The `environment` block the runner itself writes into every report is identical
across all twenty cells:

```json
  "environment": {
    "operatingSystem": "Microsoft Windows 10.0.26200",
    "framework": ".NET 10.0.10",
    "processArchitecture": "X64",
    "processorCount": 20
  },
```

Two caveats about this machine belong in the record. It is a desktop under
`Balanced` power mode, not a fixed-clock measurement rig, so absolute
millisecond figures carry ordinary desktop variance. And the discarded warm run
is a separate process invocation, so it warms the operating-system file cache
and the build outputs but does not carry any just-in-time compilation warmth
into the measured process. That is what the protocol in design section 2.2
specifies, and it is applied uniformly to all twenty cells, so the comparison
between presets remains fair; but it does mean the first few hundred ticks of
every measured run include tiered-compilation cost. The `maximumMilliseconds`
column, which sits between 14.7 ms and 50.6 ms while the p50 sits below 1.2 ms
everywhere, is that cost.

---

## 3. Command form used

The invocation is `./scripts/benchmark.ps1` with its real parameter names, taken
from the script's own `param` block at `scripts/benchmark.ps1:1-36`. The script
accepts `-Preset` for the combat preset and `-MovementPreset` for the movement
preset, and passes each straight through to the headless runner's `--preset` and
`--movement-preset` arguments, so pinning the combat preset was possible and no
stop-and-report was needed.

The exact form used for every one of the forty runs, with the tag substituted:

```powershell
./scripts/benchmark.ps1 -Agents <200|500> -Ticks 10000 -Seed <1|2|3|5|8> `
    -Preset PrecolonialPhilippinesV2 `
    -MovementPreset <PersistentContingentsV4|EquipmentRelativeFootworkV6> `
    -Output <path>.json -NoBuild
```

`-NoBuild` is used on the matrix runs only. A single build ran first, without
that switch, so that `dotnet restore --locked-mode` and the `Release` build both
executed once and every subsequent cell measured the identical binary. Fixing
the binary across all twenty cells is the point: a rebuild between cells would
make the preset comparison a comparison of two builds as well as two presets.

The `-LogLevel` parameter was left at its default of `off`, so no cell wrote a
diagnostic log and no cell measured the log writer.

---

## 4. The twenty-cell table

All twenty cells ran to completion. None failed. Every cell reports
`deterministic: true` and `firstMismatchTick: null`, meaning the headless
runner's twin simulation agreed with the run under test at every tick, and every
cell exited 0.

`Ticks` below is `measuredTicks`. `F0` and `F1` are `faction0Survivors` and
`faction1Survivors`. `Elapsed` is `durationMilliseconds`. `p50` is
`tickPercentiles.p50Milliseconds`. `Core alloc` is `coreAllocatedBytes`, the
figure attributable to `AdvanceOneTick` alone. `Det.` is `deterministic`, and
`Mismatch` is `firstMismatchTick`.

### 4.1 `PersistentContingentsV4`, 200 agents

| Seed | Ticks | Outcome | F0 | F1 | Elapsed (ms) | p50 (ms) | Core alloc (bytes) | Det. | Mismatch |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 1279 | `Faction0Victory` | 15 | 0 | 364.9239 | 0.1195 | 154,976 | true | null |
| 2 | 1439 | `Faction0Victory` | 5 | 0 | 320.2101 | 0.0657 | 154,976 | true | null |
| 3 | 2037 | `Faction1Victory` | 0 | 9 | 388.7251 | 0.0583 | 154,976 | true | null |
| 5 | 2230 | `Faction1Victory` | 0 | 1 | 405.9803 | 0.0607 | 154,976 | true | null |
| 8 | 2284 | `Faction0Victory` | 1 | 0 | 377.2590 | 0.0519 | 154,976 | true | null |

### 4.2 `PersistentContingentsV4`, 500 agents

| Seed | Ticks | Outcome | F0 | F1 | Elapsed (ms) | p50 (ms) | Core alloc (bytes) | Det. | Mismatch |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 2934 | `Faction0Victory` | 2 | 0 | 1115.2052 | 0.1324 | 338,736 | true | null |
| 2 | 2551 | `Faction0Victory` | 20 | 0 | 1298.7216 | 0.3701 | 338,736 | true | null |
| 3 | 4085 | `Faction0Victory` | 4 | 0 | 1673.4223 | 0.2087 | 338,736 | true | null |
| 5 | 2568 | `Faction0Victory` | 8 | 0 | 1306.2083 | 0.3316 | 338,736 | true | null |
| 8 | 4405 | `Faction1Victory` | 0 | 5 | 1716.7821 | 0.2275 | 338,736 | true | null |

### 4.3 `EquipmentRelativeFootworkV6`, 200 agents

| Seed | Ticks | Outcome | F0 | F1 | Elapsed (ms) | p50 (ms) | Core alloc (bytes) | Det. | Mismatch |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 10000 | `Draw` | 78 | 73 | 3767.2236 | 0.3420 | 142,640 | true | null |
| 2 | 10000 | `Draw` | 66 | 68 | 3476.7758 | 0.2984 | 142,640 | true | null |
| 3 | 10000 | `Draw` | 50 | 46 | 2244.0904 | 0.1745 | 142,640 | true | null |
| 5 | 10000 | `Draw` | 76 | 70 | 3696.3557 | 0.3160 | 142,640 | true | null |
| 8 | 10000 | `Draw` | 40 | 49 | 2239.8374 | 0.1718 | 142,640 | true | null |

### 4.4 `EquipmentRelativeFootworkV6`, 500 agents

| Seed | Ticks | Outcome | F0 | F1 | Elapsed (ms) | p50 (ms) | Core alloc (bytes) | Det. | Mismatch |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 10000 | `Draw` | 137 | 142 | 13631.5628 | 1.1489 | 322,328 | true | null |
| 2 | 10000 | `Draw` | 156 | 153 | 12256.1104 | 1.0341 | 322,328 | true | null |
| 3 | 10000 | `Draw` | 120 | 124 | 9893.6514 | 0.7995 | 314,112 | true | null |
| 5 | 10000 | `Draw` | 128 | 125 | 10219.5716 | 0.8666 | 314,112 | true | null |
| 8 | 10000 | `Draw` | 106 | 111 | 7134.6747 | 0.6038 | 314,112 | true | null |

### 4.5 Supplementary percentiles

Section 8 of the standards asks for p50, p95, p99, and max together, so the
remaining three columns are recorded here rather than dropped.

| Preset | Agents | Seed | p95 (ms) | p99 (ms) | max (ms) |
| --- | --- | --- | --- | --- | --- |
| `PersistentContingentsV4` | 200 | 1 | 1.1393 | 1.3110 | 15.3219 |
| `PersistentContingentsV4` | 200 | 2 | 0.8259 | 1.1554 | 15.8320 |
| `PersistentContingentsV4` | 200 | 3 | 0.7796 | 1.3427 | 14.7384 |
| `PersistentContingentsV4` | 200 | 5 | 0.7719 | 1.2602 | 15.7648 |
| `PersistentContingentsV4` | 200 | 8 | 0.7480 | 1.2141 | 14.9001 |
| `PersistentContingentsV4` | 500 | 1 | 1.2128 | 2.6744 | 18.8355 |
| `PersistentContingentsV4` | 500 | 2 | 1.4384 | 2.8980 | 19.0885 |
| `PersistentContingentsV4` | 500 | 3 | 1.1916 | 2.4146 | 17.2094 |
| `PersistentContingentsV4` | 500 | 5 | 1.3783 | 2.6576 | 19.3438 |
| `PersistentContingentsV4` | 500 | 8 | 1.1712 | 1.8870 | 18.6564 |
| `EquipmentRelativeFootworkV6` | 200 | 1 | 0.4823 | 0.8133 | 24.0434 |
| `EquipmentRelativeFootworkV6` | 200 | 2 | 0.5010 | 0.7662 | 23.2104 |
| `EquipmentRelativeFootworkV6` | 200 | 3 | 0.3654 | 0.7205 | 23.9101 |
| `EquipmentRelativeFootworkV6` | 200 | 5 | 0.5458 | 0.7214 | 24.3713 |
| `EquipmentRelativeFootworkV6` | 200 | 8 | 0.3398 | 0.7874 | 23.7241 |
| `EquipmentRelativeFootworkV6` | 500 | 1 | 2.1946 | 2.8604 | 39.7227 |
| `EquipmentRelativeFootworkV6` | 500 | 2 | 1.9584 | 2.5328 | 50.6302 |
| `EquipmentRelativeFootworkV6` | 500 | 3 | 1.6485 | 2.1912 | 45.4642 |
| `EquipmentRelativeFootworkV6` | 500 | 5 | 1.8005 | 2.3489 | 50.1143 |
| `EquipmentRelativeFootworkV6` | 500 | 8 | 1.3364 | 2.1903 | 45.8601 |

### 4.6 Hashes, recorded for traceability

These are not acceptance criteria for this task. They are recorded so that a
later session can tell whether it is looking at the same workload.

| Preset | Agents | Seed | `stateHash` | `eventHash` |
| --- | --- | --- | --- | --- |
| `PersistentContingentsV4` | 200 | 1 | `2410DD94F26C82E2` | `56F66BBC10E69F0E` |
| `PersistentContingentsV4` | 200 | 2 | `87C975CA60D4976A` | `A8F8B210FCAAE164` |
| `PersistentContingentsV4` | 200 | 3 | `0AC5EDF45D9DF1D0` | `E5099CF37EB3691E` |
| `PersistentContingentsV4` | 200 | 5 | `AECCAAFE87A8F274` | `33DCB9796AAE7CB9` |
| `PersistentContingentsV4` | 200 | 8 | `2C21042DB0644374` | `EC2F70930B523E3D` |
| `PersistentContingentsV4` | 500 | 1 | `C3E362AD47641844` | `5DC0BE55BC7B1F18` |
| `PersistentContingentsV4` | 500 | 2 | `770E6FB4B111795B` | `CD579D9334ADCFDC` |
| `PersistentContingentsV4` | 500 | 3 | `B3761187947BA744` | `D3B953C319D7A130` |
| `PersistentContingentsV4` | 500 | 5 | `ACA7FC5EA4D65FAA` | `6AB7E673CA49FF1C` |
| `PersistentContingentsV4` | 500 | 8 | `0F04EEB6D30B6D5B` | `BC04202D9B9ED80D` |
| `EquipmentRelativeFootworkV6` | 200 | 1 | `66320AD76023759B` | `2531D81886469344` |
| `EquipmentRelativeFootworkV6` | 200 | 2 | `3445F8503D86F7C0` | `539A3F5D99F855AF` |
| `EquipmentRelativeFootworkV6` | 200 | 3 | `B34283755105071D` | `18058D397719EEEC` |
| `EquipmentRelativeFootworkV6` | 200 | 5 | `304988E962838344` | `1419826D6CEAE5D2` |
| `EquipmentRelativeFootworkV6` | 200 | 8 | `B234C09FD4CEE334` | `6610AE7540B486B8` |
| `EquipmentRelativeFootworkV6` | 500 | 1 | `B194D9BD0D9AF6BA` | `0D14EC01DE6F50C6` |
| `EquipmentRelativeFootworkV6` | 500 | 2 | `D553165D5F4B8022` | `70C80093AA54D7A6` |
| `EquipmentRelativeFootworkV6` | 500 | 3 | `824D72BBB5FFEF9C` | `75984DCA450AE5F4` |
| `EquipmentRelativeFootworkV6` | 500 | 5 | `0974368904205F54` | `552C9FC983E5D3D7` |
| `EquipmentRelativeFootworkV6` | 500 | 8 | `FBAAF38E70079727` | `1E72F1007F1EEDFC` |

---

## 5. Which V6 cells drew, and with how many survivors

**All ten `EquipmentRelativeFootworkV6` cells ended in `Draw` at the 10,000-tick
limit.** Not one of them reached a decisive outcome. The tables in sections 4.3
and 4.4 show `measuredTicks` equal to the requested 10,000 in every V6 row.

| Agents | Seed | Outcome | Faction 0 survivors | Faction 1 survivors | Total alive at the limit | Share of the starting roster still alive |
| --- | --- | --- | --- | --- | --- | --- |
| 200 | 1 | `Draw` | 78 | 73 | 151 | 75.5% |
| 200 | 2 | `Draw` | 66 | 68 | 134 | 67.0% |
| 200 | 3 | `Draw` | 50 | 46 | 96 | 48.0% |
| 200 | 5 | `Draw` | 76 | 70 | 146 | 73.0% |
| 200 | 8 | `Draw` | 40 | 49 | 89 | 44.5% |
| 500 | 1 | `Draw` | 137 | 142 | 279 | 55.8% |
| 500 | 2 | `Draw` | 156 | 153 | 309 | 61.8% |
| 500 | 3 | `Draw` | 120 | 124 | 244 | 48.8% |
| 500 | 5 | `Draw` | 128 | 125 | 253 | 50.6% |
| 500 | 8 | `Draw` | 106 | 111 | 217 | 43.4% |

The two sides finish close to level in every single cell. The largest gap in the
five 200-agent cells is nine warriors (seed 8, 40 against 49); the largest gap in
the five 500-agent cells is five warriors (seed 1, 137 against 142). Between 43.4%
and 75.5% of the starting roster is still standing when the tick limit arrives.
This is not a battle that was nearly decided and ran a little long. It is a
standoff.

The V6 `movementMetrics` block makes the mechanism visible. In the 200-agent
seed-1 cell, `refuseAgentTicks` is 1,140,221 and `regroupAgentTicks` is 338,634,
while `commitAgentTicks` is only 2,216 and `recoverAgentTicks` only 2,017. That is
1,478,855 agent-ticks spent refusing or regrouping against 4,233 spent committing
or recovering, a ratio of about 349 to 1. Over the same run
`acceptedAttacks` is 851 and `landedAttacks` 566, against 2,612 and 1,778
respectively for the `PersistentContingentsV4` cell at the same size and seed — a
run that lasted 1,279 ticks rather than 10,000. V6 lands about a third as many
blows in roughly eight times the duration.

The `movementMetrics` block cannot be compared across the two presets: legacy
movement presets report an all-zero block, and all ten `PersistentContingentsV4`
cells do exactly that. The comparison above is therefore between V6's own
movement metrics and both presets' combat metrics, not between two movement
metric blocks.

By contrast, all ten `PersistentContingentsV4` cells reached a decisive outcome:
six `Faction0Victory` and four `Faction1Victory`, with zero draws.

### 5.1 The `PersistentContingentsV4` termination spread, restated from measurement

| Agents | Shortest run | Longest run |
| --- | --- | --- |
| 200 | 1,279 ticks (seed 1) | 2,284 ticks (seed 8) |
| 500 | 2,551 ticks (seed 2) | 4,405 ticks (seed 8) |
| Both sizes together | **1,279 ticks** | **4,405 ticks** |

This spread is relevant to section 6 below.

---

## 6. Warm runs, discarded but recorded

The protocol requires one discarded warm run per cell. Discarding a run silently
would make the claim unverifiable, so the warm runs are recorded here and are
not used in any table above.

| Cell | Warm ticks | Warm outcome | Warm p50 (ms) | Warm `stateHash` | Measured p50 (ms) |
| --- | --- | --- | --- | --- | --- |
| `PersistentContingentsV4` 200 seed 1 | 1279 | `Faction0Victory` | 0.1166 | `2410DD94F26C82E2` | 0.1195 |
| `PersistentContingentsV4` 200 seed 2 | 1439 | `Faction0Victory` | 0.0626 | `87C975CA60D4976A` | 0.0657 |
| `PersistentContingentsV4` 200 seed 3 | 2037 | `Faction1Victory` | 0.0606 | `0AC5EDF45D9DF1D0` | 0.0583 |
| `PersistentContingentsV4` 200 seed 5 | 2230 | `Faction1Victory` | 0.0601 | `AECCAAFE87A8F274` | 0.0607 |
| `PersistentContingentsV4` 200 seed 8 | 2284 | `Faction0Victory` | 0.0534 | `2C21042DB0644374` | 0.0519 |
| `PersistentContingentsV4` 500 seed 1 | 2934 | `Faction0Victory` | 0.1400 | `C3E362AD47641844` | 0.1324 |
| `PersistentContingentsV4` 500 seed 2 | 2551 | `Faction0Victory` | 0.3762 | `770E6FB4B111795B` | 0.3701 |
| `PersistentContingentsV4` 500 seed 3 | 4085 | `Faction0Victory` | 0.2934 | `B3761187947BA744` | 0.2087 |
| `PersistentContingentsV4` 500 seed 5 | 2568 | `Faction0Victory` | 0.3383 | `ACA7FC5EA4D65FAA` | 0.3316 |
| `PersistentContingentsV4` 500 seed 8 | 4405 | `Faction1Victory` | 0.2311 | `0F04EEB6D30B6D5B` | 0.2275 |
| `EquipmentRelativeFootworkV6` 200 seed 1 | 10000 | `Draw` | 0.3323 | `66320AD76023759B` | 0.3420 |
| `EquipmentRelativeFootworkV6` 200 seed 2 | 10000 | `Draw` | 0.2914 | `3445F8503D86F7C0` | 0.2984 |
| `EquipmentRelativeFootworkV6` 200 seed 3 | 10000 | `Draw` | 0.1736 | `B34283755105071D` | 0.1745 |
| `EquipmentRelativeFootworkV6` 200 seed 5 | 10000 | `Draw` | 0.3124 | `304988E962838344` | 0.3160 |
| `EquipmentRelativeFootworkV6` 200 seed 8 | 10000 | `Draw` | 0.1601 | `B234C09FD4CEE334` | 0.1718 |
| `EquipmentRelativeFootworkV6` 500 seed 1 | 10000 | `Draw` | 1.0593 | `B194D9BD0D9AF6BA` | 1.1489 |
| `EquipmentRelativeFootworkV6` 500 seed 2 | 10000 | `Draw` | 1.0667 | `D553165D5F4B8022` | 1.0341 |
| `EquipmentRelativeFootworkV6` 500 seed 3 | 10000 | `Draw` | 0.7978 | `824D72BBB5FFEF9C` | 0.7995 |
| `EquipmentRelativeFootworkV6` 500 seed 5 | 10000 | `Draw` | 0.8833 | `0974368904205F54` | 0.8666 |
| `EquipmentRelativeFootworkV6` 500 seed 8 | 10000 | `Draw` | 0.5936 | `FBAAF38E70079727` | 0.6038 |

Two things are worth noticing in this table. The warm and measured
`stateHash` values are identical in all twenty pairs, which is a
cross-process determinism check the matrix produced for free: forty runs, twenty
pairs, twenty agreements. And the warm-to-measured p50 drift is small
everywhere except `PersistentContingentsV4` 500 seed 3, where it moves from
0.2934 ms to 0.2087 ms, a 29% swing. That single cell is the clearest evidence
that this machine's millisecond figures carry real desktop variance, and it is
why the protocol reports a median across five seeds rather than a single
reading.

---

## 7. Median `p50Milliseconds` per group

These are the four numbers task F2 will need. They are recorded as facts. No
ratio is taken and no verdict is asserted against the 2.0× or 2.5× ceiling; that
is F2's job, and there is no V7 to compare against yet.

Raw output of the median computation, showing the sorted five-seed list behind
each median:

```
PersistentContingentsV4 200: sorted=0.0519, 0.0583, 0.0607, 0.0657, 0.1195 median=0.0607
PersistentContingentsV4 500: sorted=0.1324, 0.2087, 0.2275, 0.3316, 0.3701 median=0.2275
EquipmentRelativeFootworkV6 200: sorted=0.1718, 0.1745, 0.2984, 0.316, 0.342 median=0.2984
EquipmentRelativeFootworkV6 500: sorted=0.6038, 0.7995, 0.8666, 1.0341, 1.1489 median=0.8666
```

| Movement preset | Agents | Median `p50Milliseconds` |
| --- | --- | --- |
| `PersistentContingentsV4` | 200 | 0.0607 |
| `PersistentContingentsV4` | 500 | 0.2275 |
| `EquipmentRelativeFootworkV6` | 200 | 0.2984 |
| `EquipmentRelativeFootworkV6` | 500 | 0.8666 |

One methodological warning belongs beside these numbers, for whoever runs F2.
`p50Milliseconds` is a per-tick statistic, and the two presets are not running
the same population profile over their measured ticks. A
`PersistentContingentsV4` run spends its last several hundred ticks with a
handful of survivors left, and those cheap end-of-battle ticks pull its median
down. A `EquipmentRelativeFootworkV6` run never gets there: it holds between 89
and 309 living agents for all 10,000 ticks. Part of the gap between the two
medians is therefore a difference in how many agents are alive per tick, not a
difference in per-agent cost. This is not a reason to change the metric — design
section 2.2 chose it deliberately, and the alternative it replaced was worse —
but a V7 that terminates will shift back toward the V4 population profile, and
that shift will move its median for reasons that have nothing to do with the
interrupt's own cost.

---

## 8. Allocation

`coreAllocatedBytes` is constant per preset-and-size group, and in one case
nearly so. It does not scale with run length, which is the expected shape for
allocation that happens during setup rather than per tick.

| Preset | Agents | `coreAllocatedBytes` |
| --- | --- | --- |
| `PersistentContingentsV4` | 200 | 154,976 in all five cells |
| `PersistentContingentsV4` | 500 | 338,736 in all five cells |
| `EquipmentRelativeFootworkV6` | 200 | 142,640 in all five cells |
| `EquipmentRelativeFootworkV6` | 500 | 322,328 for seeds 1 and 2; 314,112 for seeds 3, 5, and 8 |

The 8,216-byte step in the last row is the only allocation figure in the matrix
that varies within a group. It is recorded rather than explained; nothing in
this task investigated it, and no claim is made about its cause.

---

## 9. Verbatim run output

Two complete reports are pasted in full so that the extracted tables above can be
checked against unedited runner output. The remaining eighteen are in the same
shape.

### 9.1 `PersistentContingentsV4`, 200 agents, seed 1 — measured run, first twenty lines

```json
{
  "environment": {
    "operatingSystem": "Microsoft Windows 10.0.26200",
    "framework": ".NET 10.0.10",
    "processArchitecture": "X64",
    "processorCount": 20
  },
  "seed": 1,
  "agentCount": 200,
  "requestedTicks": 10000,
  "measuredTicks": 1279,
  "durationMilliseconds": 364.9238999999999,
  "tickPercentiles": {
    "p50Milliseconds": 0.1195,
    "p95Milliseconds": 1.1393,
    "p99Milliseconds": 1.311,
    "maximumMilliseconds": 15.3219
  },
  "allocatedBytes": 524808,
  "outcome": "Faction0Victory",
```

### 9.2 `EquipmentRelativeFootworkV6`, 200 agents, seed 1 — measured run, complete

```json
{
  "environment": {
    "operatingSystem": "Microsoft Windows 10.0.26200",
    "framework": ".NET 10.0.10",
    "processArchitecture": "X64",
    "processorCount": 20
  },
  "seed": 1,
  "agentCount": 200,
  "requestedTicks": 10000,
  "measuredTicks": 10000,
  "durationMilliseconds": 3767.223599999995,
  "tickPercentiles": {
    "p50Milliseconds": 0.342,
    "p95Milliseconds": 0.4823,
    "p99Milliseconds": 0.8133,
    "maximumMilliseconds": 24.0434
  },
  "allocatedBytes": 1965264,
  "outcome": "Draw",
  "faction0Survivors": 78,
  "faction1Survivors": 73,
  "eventHash": "2531D81886469344",
  "stateHash": "66320AD76023759B",
  "deterministic": true,
  "firstMismatchTick": null,
  "collisionMetrics": {
    "candidatePairs": 912231,
    "contactPairs": 1439,
    "acceptedMoves": 117518,
    "blockedAgentTicks": 82499,
    "attackCapableAgentTicks": 3072,
    "longestBlockedStreakTicks": 180,
    "maximumFrontWidthRaw": 579482,
    "maximumFrontDepthRaw": 237528,
    "maximumPenetrationRaw": 0
  },
  "combatMetrics": {
    "acceptedAttacks": 851,
    "landedAttacks": 566,
    "shieldBlockedAttacks": 86,
    "parriedAttacks": 14,
    "deflectedAttacks": 72,
    "evadedAttacks": 113,
    "defenceAttributableShare": 0.33490011750881316
  },
  "coreAllocatedBytes": 142640,
  "movementMetrics": {
    "approachAgentTicks": 52158,
    "engageAgentTicks": 204,
    "commitAgentTicks": 2216,
    "recoverAgentTicks": 2017,
    "refuseAgentTicks": 1140221,
    "disengageAgentTicks": 29043,
    "regroupAgentTicks": 338634,
    "pursueAgentTicks": 0,
    "postureTransitions": 16108,
    "facingStepsTurned": 14558,
    "disengagementEntries": 339,
    "conflictDenials": 130844
  }
}
```

### 9.3 Extraction over all twenty measured reports

Fields in order: cell, `measuredTicks`, `outcome`, `faction0Survivors`,
`faction1Survivors`, `durationMilliseconds`, `p50Milliseconds`,
`p95Milliseconds`, `coreAllocatedBytes`, `allocatedBytes`, `deterministic`,
`firstMismatchTick`, `stateHash`, `eventHash`.

```
measured-EquipmentRelativeFootworkV6-200-seed1|10000|Draw|78|73|3767.223599999995|0.342|0.4823|142640|1965264|True|null|66320AD76023759B|2531D81886469344
measured-EquipmentRelativeFootworkV6-200-seed2|10000|Draw|66|68|3476.7757999999894|0.2984|0.501|142640|1686056|True|null|3445F8503D86F7C0|539A3F5D99F855AF
measured-EquipmentRelativeFootworkV6-200-seed3|10000|Draw|50|46|2244.0904000000155|0.1745|0.3654|142640|1965264|True|null|B34283755105071D|18058D397719EEEC
measured-EquipmentRelativeFootworkV6-200-seed5|10000|Draw|76|70|3696.3557000000033|0.316|0.5458|142640|1852976|True|null|304988E962838344|1419826D6CEAE5D2
measured-EquipmentRelativeFootworkV6-200-seed8|10000|Draw|40|49|2239.8373999999912|0.1718|0.3398|142640|1223568|True|null|B234C09FD4CEE334|6610AE7540B486B8
measured-EquipmentRelativeFootworkV6-500-seed1|10000|Draw|137|142|13631.562799999987|1.1489|2.1946|322328|2324640|True|null|B194D9BD0D9AF6BA|0D14EC01DE6F50C6
measured-EquipmentRelativeFootworkV6-500-seed2|10000|Draw|156|153|12256.110400000009|1.0341|1.9584|322328|2324640|True|null|D553165D5F4B8022|70C80093AA54D7A6
measured-EquipmentRelativeFootworkV6-500-seed3|10000|Draw|120|124|9893.651399999986|0.7995|1.6485|314112|2308208|True|null|824D72BBB5FFEF9C|75984DCA450AE5F4
measured-EquipmentRelativeFootworkV6-500-seed5|10000|Draw|128|125|10219.571600000023|0.8666|1.8005|314112|2222440|True|null|0974368904205F54|552C9FC983E5D3D7
measured-EquipmentRelativeFootworkV6-500-seed8|10000|Draw|106|111|7134.674699999997|0.6038|1.3364|314112|2314400|True|null|FBAAF38E70079727|1E72F1007F1EEDFC
measured-PersistentContingentsV4-200-seed1|1279|Faction0Victory|15|0|364.9238999999999|0.1195|1.1393|154976|524808|True|null|2410DD94F26C82E2|56F66BBC10E69F0E
measured-PersistentContingentsV4-200-seed2|1439|Faction0Victory|5|0|320.2100999999994|0.0657|0.8259|154976|551688|True|null|87C975CA60D4976A|A8F8B210FCAAE164
measured-PersistentContingentsV4-200-seed3|2037|Faction1Victory|0|9|388.7250999999997|0.0583|0.7796|154976|652152|True|null|0AC5EDF45D9DF1D0|E5099CF37EB3691E
measured-PersistentContingentsV4-200-seed5|2230|Faction1Victory|0|1|405.98030000000034|0.0607|0.7719|154976|684576|True|null|AECCAAFE87A8F274|33DCB9796AAE7CB9
measured-PersistentContingentsV4-200-seed8|2284|Faction0Victory|1|0|377.25899999999973|0.0519|0.748|154976|693648|True|null|2C21042DB0644374|EC2F70930B523E3D
measured-PersistentContingentsV4-500-seed1|2934|Faction0Victory|2|0|1115.205200000009|0.1324|1.2128|338736|1170368|True|null|C3E362AD47641844|5DC0BE55BC7B1F18
measured-PersistentContingentsV4-500-seed2|2551|Faction0Victory|20|0|1298.7216000000008|0.3701|1.4384|338736|1106024|True|null|770E6FB4B111795B|CD579D9334ADCFDC
measured-PersistentContingentsV4-500-seed3|4085|Faction0Victory|4|0|1673.4223000000072|0.2087|1.1916|338736|1363736|True|null|B3761187947BA744|D3B953C319D7A130
measured-PersistentContingentsV4-500-seed5|2568|Faction0Victory|8|0|1306.2083000000025|0.3316|1.3783|338736|1108880|True|null|ACA7FC5EA4D65FAA|6AB7E673CA49FF1C
measured-PersistentContingentsV4-500-seed8|4405|Faction1Victory|0|5|1716.7821000000044|0.2275|1.1712|338736|1417496|True|null|0F04EEB6D30B6D5B|BC04202D9B9ED80D
```

### 9.4 Matrix driver output, showing all twenty cells attempted and their exit codes

```
DONE PersistentContingentsV4-200-seed1 exit=0 wall=1.54s
DONE PersistentContingentsV4-200-seed2 exit=0 wall=1.52s
DONE PersistentContingentsV4-200-seed3 exit=0 wall=1.61s
DONE PersistentContingentsV4-200-seed5 exit=0 wall=1.67s
DONE PersistentContingentsV4-200-seed8 exit=0 wall=1.69s
DONE PersistentContingentsV4-500-seed1 exit=0 wall=3.53s
DONE PersistentContingentsV4-500-seed2 exit=0 wall=3.81s
DONE PersistentContingentsV4-500-seed3 exit=0 wall=4.9s
DONE PersistentContingentsV4-500-seed5 exit=0 wall=3.91s
DONE PersistentContingentsV4-500-seed8 exit=0 wall=5.07s
DONE EquipmentRelativeFootworkV6-200-seed1 exit=0 wall=9.01s
DONE EquipmentRelativeFootworkV6-200-seed2 exit=0 wall=8.5s
DONE EquipmentRelativeFootworkV6-200-seed3 exit=0 wall=6.22s
DONE EquipmentRelativeFootworkV6-200-seed5 exit=0 wall=8.96s
DONE EquipmentRelativeFootworkV6-200-seed8 exit=0 wall=6.14s
DONE EquipmentRelativeFootworkV6-500-seed1 exit=0 wall=30.37s
DONE EquipmentRelativeFootworkV6-500-seed2 exit=0 wall=27.82s
DONE EquipmentRelativeFootworkV6-500-seed3 exit=0 wall=22.95s
DONE EquipmentRelativeFootworkV6-500-seed5 exit=0 wall=23.2s
DONE EquipmentRelativeFootworkV6-500-seed8 exit=0 wall=17.02s
ALL CELLS ATTEMPTED
```

The `wall` figure is the wall-clock cost of the measured `benchmark.ps1`
invocation including process startup, not a simulation timing. It is recorded
only to show that each cell actually ran.

---

## 10. Self-check against the recorded gate figure

The task that commissioned this document carried a known prior result: a gate run
on this commit's parent recorded, at 200 agents and seed 1 under the shipped
defaults, `stateHash 1B73FC5923879AA0`, `eventHash AC55684F24D39344`,
`measuredTicks 981`, outcome `Faction1Victory`. That run used the shipped combat
default `PrecolonialPhilippinesV4`, not the pinned `PrecolonialPhilippinesV2`
used in the twenty-cell matrix, so the matrix figures were expected to differ and
have not been adjusted toward it.

To confirm that this harness reproduces the recorded figure when the presets
match, one extra run was made outside the matrix, under the shipped defaults:

```powershell
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1 -NoBuild
```

Output:

```
  "measuredTicks": 981,
  "outcome": "Faction1Victory",
  "faction0Survivors": 0,
  "faction1Survivors": 6,
  "eventHash": "AC55684F24D39344",
  "stateHash": "1B73FC5923879AA0",
```

Exact match on all four recorded fields. The harness, the machine, and this
commit reproduce the parent commit's gate figure bit for bit, which means the
divergence in the matrix is attributable to the pinned combat preset and to
nothing else. For the same cell under `PrecolonialPhilippinesV2` the matrix
records 1,279 ticks and `Faction0Victory` with 15 survivors — a different
battle, as expected, because V2 fields shield rows that V4 does not.

---

## 11. Discrepancies found

Three, recorded honestly rather than smoothed over.

### 11.1 The design's stated `PersistentContingentsV4` spread is narrower than the measured one

Design section 2.1 justifies the 6,000-tick termination bar this way:

> Six thousand is chosen against the measured `PersistentContingentsV4` spread,
> which lands between 981 and 2,934 ticks across the same cells.

Neither endpoint survives a combat-V2 measurement of all ten cells. The measured
`PersistentContingentsV4` spread under `PrecolonialPhilippinesV2` is **1,279 to
4,405 ticks**, as section 5.1 records.

The two quoted numbers are traceable. 981 is exactly the shipped-default figure
reproduced in section 10, which is a combat-V4 run and therefore fields no shield
rows at all. 2,934 is exactly the combat-V2 figure this matrix measured for 500
agents at seed 1. The stated spread appears to mix one endpoint taken under one
combat preset with the other endpoint taken under the other, and its upper bound
happens to coincide with the lowest of the five 500-agent combat-V2 readings.

This matters for how much headroom the 6,000-tick bar actually represents. If
`PersistentContingentsV4` tops out at 2,934 ticks, the bar leaves V7 a little over
twice the reference maximum. Against the measured 4,405, it leaves 1,595 ticks —
about 36% headroom over a preset that is not even trying to deliberate. The bar
is a settled decision from the brief and this document does not reopen it, but a
later reader should know that the reasoning quoted in support of it rests on a
figure this measurement does not reproduce.

### 11.2 `PersistentContingentsV4` at 500 agents is slower to resolve than the design implies

Related to the above but worth separating. Two of the five 500-agent
`PersistentContingentsV4` cells — seed 3 at 4,085 ticks and seed 8 at 4,405 ticks
— run past 4,000 ticks. The 500-agent group's shortest run, 2,551 ticks at seed 2,
is itself longer than the entire 200-agent group's longest run of 2,284 ticks.
Termination time scales with agent count noticeably. Whoever calibrates V7 in
task E1 should size the interrupt against the 500-agent cells, because those are
where the 6,000-tick bar will actually bind.

### 11.3 A single-cell allocation step at 500 agents under V6

Recorded in section 8. `coreAllocatedBytes` is 322,328 for
`EquipmentRelativeFootworkV6` at 500 agents on seeds 1 and 2, and 314,112 on
seeds 3, 5, and 8 — an 8,216-byte difference within what is otherwise a
constant-per-group figure. Every other group in the matrix reports one identical
value across all five of its seeds. This is noted as an observation. No
investigation was performed, and no claim is made about whether it is a growable
buffer crossing a size threshold, a seed-dependent path, or something else.

### 11.4 Nothing else looked inconsistent

Every one of the twenty cells reported `deterministic: true` with
`firstMismatchTick: null`, and every one of the twenty warm runs produced a
`stateHash` identical to its measured partner's. All twenty cells exited 0. No
cell failed, none was skipped, and no seed was substituted for another.

---

## 12. What this document deliberately does not do

- It asserts no verdict against the 2.0× or 2.5× `p50Milliseconds` ceilings. That
  belongs to task F2, once a V7 exists to measure.
- It computes no phase-flip percentage. The redefined metric in design section 2.3
  requires per-tick posture-intent instrumentation that lives in the calibration
  harness task E0 will build, not in the headless runner's `movementMetrics`
  block.
- It changed no source file, no test, no fixture, and no script. The only file
  this task created is this document.
- It did not run `./scripts/verify.ps1`. The canonical gate is not delegated to a
  measurement task, and task F3 owns the single gate run for this plan.
