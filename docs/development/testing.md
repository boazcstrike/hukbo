# Testing and Verification

## Canonical gate

```powershell
./scripts/verify.ps1
```

The gate performs, in order:

1. prerequisite validation and locked restore;
2. formatting verification;
3. Release solution build;
4. Core and GPU-independent Client tests without rebuilding;
5. a 200-agent, 10,000-tick, seed-1 headless determinism workload.

It does not launch a window or alter authoritative game state. It never runs a
destructive Git or filesystem cleanup.

This repository intentionally uses local-only verification. There is no GitHub
Actions workflow or hosted-CI completion gate. Run the canonical gate on the
integration workstation and record its exact result.

## Focused commands

```powershell
./scripts/test.ps1 -Configuration Release
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release
dotnet test tests/Hukbo.Core.Tests -c Release `
  --filter FullyQualifiedName~DeterminismTests
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1
./scripts/format.ps1 -Verify
```

Client presentation tests must not create an `ArenaGame`, graphics device,
sprite batch, or window. Tests must remain independent from GPU, audio
hardware, window focus, network, wall clock, `System.Random`, and platform input
types. Performance output is evidence, not a universal frame-time guarantee.

## Capturing a debug log

The debug log is on by default in `Debug` and off in `Release`. The canonical
gate builds `Release`, so a gate run is unlogged and its timing figures measure
the simulation rather than the simulation plus a writer.

Every interactive session should be run with the log on, so that a smoke row
recorded as `FAIL` or `BLOCKED` can be handed to someone else with evidence
attached:

```powershell
./scripts/run.ps1 -Configuration Debug
```

That writes `artifacts/logs/hukbo-<yyyyMMdd-HHmmss>-<pid>.jsonl`. The script
prints the directory before launching, and the log's first line repeats the
resolved level, channels, and absolute path. Only the newest twenty files are
kept, so copy a log you intend to keep out of that directory.

To narrow a session to one subsystem:

```powershell
./scripts/run.ps1 -Configuration Debug -LogLevel trc -LogChannels audio,input
```

Reading it back:

```powershell
$log = Get-ChildItem artifacts/logs -Filter *.jsonl | Sort-Object Name | Select-Object -Last 1
Get-Content $log | ConvertFrom-Json | Where-Object lvl -in 'err','warn'
```

For a headless determinism failure, `--log-level err` is enough: it emits the
one `sim.mismatch` line carrying both state hashes at the tick the two
simulations parted.

```powershell
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1 -LogLevel err
```

**A log is evidence of what the code did, never a substitute for a person
confirming what the screen showed.** An `audio.cue` line with
`"status":"Played"` proves the client asked the device to play a sound. It does
not prove a sound was audible, that it arrived at the right moment, or that it
sounded right. Smoke rows below still require a human at an interactive desktop;
see `.claude/skills/hukbo-debug-logging/SKILL.md` for the full reading guide.

## Latest non-interactive result — attack combinations on preset V3, 2026-07-28

Adds the section 3 attack-combination state machine (an opening roll on a
landed blow, a continuation roll on each following blow, a maximum chain
length bounded by both the weapon and a placeholder fighter level, and a
faster cooldown while a chain is active) behind a new
`CombatPresetId.PrecolonialPhilippinesV3 = 3`, registered alongside V1 and
V2, not instead of them. V3 fields exactly the four solo loadouts V2 already
carries — Kampilan, Wasay, solo Kalis, solo Itak — with V2's own
damage/reach/cooldown/target-weight/grip/clash values for those four
weapons, plus the new combo attributes. See
[docs/plans/2026-07-27-combat-preset-v3-combos.md](../plans/2026-07-27-combat-preset-v3-combos.md)
and its design document. `AgentState` gains `Level`, `ComboStepsRemaining`,
and `ComboTargetEntityId`; `BattleEvent` gains `ComboPosition`; both are
folded into `StateHasher.Compute` and `HeadlessRunner.AddEventToHash` for
every `CombatPresetId`, not only V3 — see "what moved" below.

This entry is task 4 of the plan's section 6 table, and records only
`./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1 -Preset
PrecolonialPhilippinesV3`. The canonical gate, `./scripts/verify.ps1`, is run
once by the orchestrator after every task in the plan has landed, not
per-task, so its result belongs in a separate entry once that run has
happened.

`--preset` is new on `HeadlessRunner` and `scripts/benchmark.ps1`, added by
this task because no earlier task in the plan owned giving the headless
workload a way to select a non-default `CombatPresetId`. It accepts either a
`CombatPresetId` member name (for example `PrecolonialPhilippinesV3`) or its
numeric value, and rejects anything `CombatPresetRegistry.IsRegistered`
does not recognize.

| Field | Value |
| --- | --- |
| `measuredTicks` | 1 473 |
| `outcome` | `Faction1Victory`, 0 against 2 survivors |
| `eventHash` | `8C2E3752572E3946` |
| `stateHash` | `81C6655CFC5F8881` |
| `deterministic` | `true` |
| `firstMismatchTick` | `null` |
| `allocatedBytes` | 80 445 216 |
| Tick p50 / p95 / p99 / max | 0.0863 / 1.554 / 2.5919 / 13.1881 ms |
| `defenceAttributableShare` | 0.2685 |
| `acceptedAttacks` / `landedAttacks` | 2 335 / 1 708 |
| `parriedAttacks` / `deflectedAttacks` / `evadedAttacks` | 93 / 277 / 257 |

**Every pinned hash literal in `DeterminismTests.cs` moved, not only the new
V3 ones — expected, per the plan's own "consequence that must not be
missed."** `StateHasher.Compute` folds three new per-agent words (`Level`,
`ComboStepsRemaining`, `ComboTargetEntityId ?? 0`) for every
`CombatPresetId`, so a V1 or V2 scenario's state hash under this build
differs from the same scenario under the pre-combo build even though neither
preset's own gameplay changed. Re-recorded, from an actual test run's
failure output rather than by calculation:

- `DeterminismTests.PreClashTerminalStateHash` — the seed-1, 200-agent,
  zero-interception preset-V1 control run's terminal state hash — moved from
  `0x5BEBA7A68F69BE0D` to `0xFD85207FF329F02D`. The terminal tick is
  unchanged, at 1154.
- The committed fixture
  `tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-preclash-digest.json`'s
  per-tick `stateHash` field, across all 1,154 rows, and its
  `terminalStateHash`, re-captured against the same zero-interception
  control run with the widened `StateHasher` fold. Its `eventFold` and
  `eventCount` rows, final agent rows, outcome, and survivor counts are
  unchanged — only `StateHasher`'s own output moved, because the folded
  event fields (`Weapon`/`HitLocation`, deliberately excluding
  `Resolution`) never touch the three new agent-state words.
- V2's pinned `ContentHash` (`0x10AB1CC226AB3636`) did **not** move.
  `CombatRuleset.ComputeContentHash`'s `AddProfile` helper only folds
  `DamagePerAttack`, `AttackRangeRaw`, and `AttackCooldownTicks` from a
  `WeaponProfile` — not the four new `ComboXxx` fields — so widening V2's
  `Build()` to supply real no-op combo values (task 1) left V2's content
  hash exactly where it was. Confirmed by running the pinned
  `PresetV2ContentHash_IsPinnedAndDistinctFromV1` fact before touching
  anything else in this task: it already passed against the new build,
  unedited.

New V3 pinned facts added to `DeterminismTests.cs`:

- `PresetV3ContentHash_IsPinnedAndDistinctFromV1AndV2`: `0xCD790E489293B304`.
- `PresetV3_SeedOneStateAndEventHashArePinned`: a fast 20-agent, 200-tick,
  seed-1 workload through the same `HeadlessRunner.Run` path
  `CombatMetrics_ReachesNeitherHash` already uses, pinned at
  `stateHash 0xC2728456AEB9F760` and `eventHash 0xE30AD003EFDDD267`. Not a
  substitute for the 200-agent/10,000-tick benchmark above — it runs on
  every `dotnet test` invocation, the benchmark does not.

`tests/Hukbo.Core.Tests/ComboChainTests.cs` (new) covers the section 3 state
machine directly, against constructed `AgentState`/`WeaponProfile` fixtures
rather than a full battle: one attacker against one inert target
(`damagePerAttack: 0`, so the target can never harm the attacker back),
close enough to stay in attack range and never move.
`ComboOpenChanceBasisPoints` and `ComboContinueChanceBasisPoints` are pinned
to either `0` or `ClashProfile.BasisPointScale` per fixture, so a roll's
outcome is certain by construction rather than dependent on predicting
`ComboResolver.MixCombo`'s hash for a given seed/tick/entity tuple, and
`ClashProfile.Neutral` (guaranteed `Landed`) or a custom always-`Evaded`
profile stand in for the clash roll the same way. Covered: the opening roll
succeeding and failing; the continuation roll succeeding below the cap,
failing below the cap, and being overridden by the cap on an otherwise
successful roll; a target switch breaking the chain before any roll is
evaluated; the bound target dying breaking the chain on the tick the
attacker discovers it (observed through the "no other candidate" pre-check
clause, since `SelectTargetsAndIntents` always refreshes `TargetEntityId` to
a living candidate or `null` before `GatherAndCommitAttacks` ever runs, so a
stale reference to a literally-dead target is not reachable through
`AdvanceOneTick`); the target leaving attack range breaking the chain
through the distinct "target now out of reach" pre-check clause, with
`TargetEntityId` unchanged so it cannot be mistaken for a retarget; and a
non-landed follow-up leaving `ComboStepsRemaining` and `ComboTargetEntityId`
exactly as they were.

`dotnet test tests/Hukbo.Core.Tests` (full, unfiltered): 603 passed, 0
failed, 0 skipped — zero pinned-hash mismatches anywhere in the suite.

## Previous non-interactive result — weapon clash on preset V2, 2026-07-28

Merges the weapon-clash defensive-resolution feature onto preset V2. See
[docs/plans/2026-07-27-clash-preset-v2-integration.md](../plans/2026-07-27-clash-preset-v2-integration.md),
its design document, and its handoff. An accepted attack now resolves against
a five-way `AttackResolution` — `Landed`, `ShieldBlocked`, `Parried`,
`Deflected`, `Evaded` — instead of landing unconditionally. Preset V1 stays
frozen with no clash profile (D1); preset V2 carries the clash tables for its
six-loadout roster, including the ten new cells the two shieldless loadouts
(solo Kalis, solo Itak) needed that the four-loadout V1 roster never had to
resolve.

`./scripts/verify.ps1 -SkipBootstrap` passed at all five stages:

```
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
[PASS] Release repository tests completed.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

| Field | Value |
| --- | --- |
| Tests | 660 passed (Client), 587 passed (Core), 0 failed, 0 skipped |
| `measuredTicks` | 1 710 |
| `outcome` | `Faction1Victory` |
| `eventHash` | `A2DC3ECA3F7345ED` |
| `stateHash` | `71211929A44A16CA` |
| `deterministic` | `true` |
| `firstMismatchTick` | `null` |
| `allocatedBytes` | 93 905 304 |
| Tick p50 / p95 / p99 / max | 0.0812 / 1.5217 / 2.8857 / 9.4846 ms |

**Both hashes moved, which is the point.** The previous baseline was
`eventHash CF8C3EDBC59C3319` and `stateHash C669281B67CF8871`, recorded before
this change under the weapon-identity preset V2. Damage is now conditional on
`Landed` and the packed `Resolution` byte enters the event, so an unchanged
hash would have meant the clash stage was never actually wired in.

- V1's `ContentHash` still equals its pinned literal `0x59FB4CA563D87A49`,
  proving V1 was not disturbed by the merge (D2's conditional fold).
- V2's `ContentHash` is pinned at `0x10AB1CC226AB3636`. It moved twice during
  this integration: once when the clash profile was first attached
  (`0x718825F30DC69593`), and again after the T60 retune below moved four
  shieldless weapon-intercept cells and two void cells within their existing
  bands. Both moves are legitimate content changes, not re-baseline drift —
  see the retune note under "T60 — the 20-seed defence-attributable share"
  below.
- The collision allocation ceiling stays at 900,000 bytes. The merged
  `BattleEvent` — carrying `Weapon`, `Shield`, `HitLocation`, and `Resolution`
  all packed into one `int` per D5 — measures 815,312 bytes, comfortably under
  the ceiling and smaller than the pre-clash 200-agent figure above.
- `CombatMetrics` reaches neither hash: `DeterminismTests.CombatMetrics_ReachesNeitherHash`
  captures the before/after pair on the merged tree and both are
  byte-identical.

### T60 — the 20-seed defence-attributable share

Gate task, not a report: the merged share must fall inside 0.25 to 0.45 across
seeds 1 through 20 at 200 agents, 10,000-tick cap.

| Seed | Share | Seed | Share | Seed | Share | Seed | Share |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 0.3055 | 6 | 0.29 | 11 | 0.32 | 16 | 0.32 |
| 2 | 0.29 | 7 | 0.30 | 12 | 0.33 | 17 | 0.30 |
| 3 | 0.31 | 8 | 0.30 | 13 | 0.31 | 18 | 0.31 |
| 4 | 0.32 | 9 | 0.30 | 14 | 0.31 | 19 | 0.30 |
| 5 | 0.31 | 10 | 0.31 | 15 | 0.32 | 20 | 0.30 |

Range 0.292 to 0.3301, inside the 0.25 to 0.45 band. No further retune needed
after the one described below.

**One retune fired during Phase 4, not Phase 5.** The unit test
`PhilippineCombatIntegrationTests.ShieldedRosterEntriesAbsorbMoreBlowsBeforeDyingThanShieldlessOnesAcrossSeedsOneThroughTwenty`
already measures a related but distinct acceptance criterion — the
shielded-versus-shieldless survival ratio, required above 1.15 — across the
same 20 seeds, and it failed once at 1.145 with the cells' first-authored
values. The four shieldless Kalis and four shieldless Itak weapon-intercept
cells, plus their two void cells, were lowered within their already-declared
0.10-to-0.18 and 0.11-to-0.19 bands (design section 5) until the ratio
cleared 1.15. Per the plan's sequencing rule, this retune invalidated the
pinned V2 content hash and the row-mean regression test, both re-captured
against the retuned tables; the 20-seed share above was measured after the
retune, not before.

### T61 — termination

At least 19 of 20 seeds must decide before the 5,000-tick cap, median
decisive tick at or below 5,000.

| Field | Value |
| --- | --- |
| Seeds decided before cap | 20 / 20 |
| Median decisive tick | 1 616 |
| Deterministic on every seed | `true` |

### T71 — 500-agent stress workload, reported not asserted

`./scripts/benchmark.ps1 -Agents 500 -Ticks 10000 -Seed 1`:

| Field | Value |
| --- | --- |
| `outcome` | `Faction0Victory`, 11 against 0 survivors |
| `measuredTicks` | 2 832 |
| `eventHash` | `A5C77685987DBA49` |
| `stateHash` | `A4C8B82F2A445691` |
| `deterministic` | `true` |
| `allocatedBytes` | 409 560 528 |
| Tick p95 / p99 / max | 1.9817 / 4.0499 / 14.0668 ms |
| `defenceAttributableShare` | 0.3159 |

## T32 — weapon balance measurement (preset V2 + clash), 2026-07-28

Closes T32/T27 of
[docs/plans/2026-07-27-weapon-identity-and-attributes.md](../plans/2026-07-27-weapon-identity-and-attributes.md),
recorded as "not done, deliberately" in that plan's completion record. Measures
mean ticks-to-kill per weapon loadout and per-faction win rate against the
current tree — preset V2 plus the weapon-clash defensive-resolution system
merged above (commit `dbd907a`) — using a new hand-run harness,
[`tools/Hukbo.Tools.WeaponBalance`](../../tools/Hukbo.Tools.WeaponBalance/Program.cs).
Read-only against `Hukbo.Core`; not part of `Hukbo.slnx` or the canonical
gate, per the `tools/` convention. No `Hukbo.Core` file was touched to
produce this measurement, so no hash moved and the gate was not re-run.

`dotnet run --project tools/Hukbo.Tools.WeaponBalance -c Release -- 10000`, 5
seeds (1 through 5) per scenario, `TickLimit 10000`.

**Method.** For every death, the ticks between the victim's first landed hit
and its death tick are attributed to the weapon loadout of whichever
attacker(s) landed a hit on it during the death tick (split credit, no
double-counting guard, if more than one attacker lands in the same tick — an
approximation acceptable for a tuning diagnostic, not exact kill attribution).
`Scenario.RosterCounts` is applied identically to both factions (see its doc
comment on `Scenario.cs`), so there is no built-in way to field two different
rosters against each other — a genuine per-faction asymmetric matchup needs
`Scenario` extended to carry a roster per faction, which is a separate,
non-trivial change with its own design document and was **not** attempted
here. "Asymmetric roster" below means a composition stacked toward one
loadout, still mirrored on both sides.

### 200-agent and 500-agent, mirrored, even roster

| Loadout | 200-agent kills | 200-agent mean TTK (ticks) | 500-agent kills | 500-agent mean TTK (ticks) |
| --- | ---: | ---: | ---: | ---: |
| Kampilan (solo) | 277 | 49.08 | 643 | 46.64 |
| Wasay (solo) | 161 | 58.11 | 403 | 54.50 |
| Kalis (solo) | 157 | 58.64 | 403 | 59.66 |
| Kalis (paired) | 167 | 63.11 | 413 | 62.96 |
| Itak (solo) | 137 | 59.20 | 375 | 60.18 |
| Itak (paired) | 148 | 65.00 | 375 | 69.35 |

200-agent: `faction0Wins=0 faction1Wins=5 draws=0`. 500-agent:
`faction0Wins=3 faction1Wins=2 draws=0`. The 200-agent split is a 5-seed
sample of a symmetric matchup — with only 5 seeds, a 0/5 split is within
normal noise, not evidence of first-mover bias; the 500-agent split at the
same roster is close to even.

### 500-agent, mirrored, single-loadout-heavy roster (one loadout at half the faction, remainder split across the other five)

| Heavy loadout | Heavy loadout kills | Heavy loadout mean TTK | Win split (faction0/faction1/draw) |
| --- | ---: | ---: | --- |
| Kampilan (solo) | 1 542 | 45.57 | 4/1/0 |
| Wasay (solo) | 1 258 | 50.63 | 3/2/0 |
| Kalis (solo) | 1 228 | 58.09 | 1/4/0 |
| Kalis (paired) | 1 279 | 68.49 | 4/1/0 |
| Itak (solo) | 1 205 | 61.67 | 2/3/0 |
| Itak (paired) | 1 234 | 72.89 | 2/3/0 |

Full per-scenario minority-loadout breakdown is in the tool's own console
output; the table above keeps the headline number.

### Finding: Kampilan (solo) outperforms its intended role at every roster mix tested

Design section 3.4 expected the wasay to lead sustained throughput (highest
damage-per-tick, 2.25 against the kampilan's 2.14) and the kampilan to trade
that for the longest reach. In every one of the eight scenarios above — the
even roster and all seven single-loadout-heavy variants — Kampilan (solo)
records both the most kills per capita and the lowest mean ticks-to-kill of
any loadout, typically 30 to 70 percent more kills than Wasay (solo) at a
comparable population share, and several ticks faster per kill than every
other loadout. The most likely mechanism is reach, not damage: at 16 world
units against Wasay's 13, a kampilan-wielder can start landing hits before an
approaching wasay-wielder is in range at all, which compounds every
clash-resolution roll and every point of accumulated damage in the kampilan's
favor before the fight is otherwise even. The clash-integration retune above
changed how often a landed hit is blocked, parried, deflected, or evaded, and
lengthened every mean-ticks-to-kill figure accordingly (compare the ticks
above against the earlier commit this measurement was first taken against),
but did not change the ordering: Kampilan (solo) topped every scenario both
before and after that retune.

**Not retuned.** Per the plan's own framing ("the attribute values in design
section 3.3 are therefore still unvalidated tuning ... preset V3 should not
treat them as settled"), and confirmed with the user rather than decided
unilaterally, this measurement is recorded as evidence for V3 tuning rather
than acted on inside V2. No preset value changed, no hash moved, no gate
re-run was required.

## Previous non-interactive result — weapon identity and attributes (preset V2), 2026-07-27

Every weapon now carries its own damage, reach, and attack cooldown, split by
grip, and a Filipino pair-form name with an evidence tier. See
[docs/plans/2026-07-27-weapon-identity-and-attributes.md](../plans/2026-07-27-weapon-identity-and-attributes.md).

**This is a hash-moving change.** `CombatPresetId.PrecolonialPhilippinesV2` is
appended, V1 stays registered and unmodified, and `Scenario.CombatPreset`
defaults to V2.

`./scripts/verify.ps1` passed at all five stages:

```
[PASS] Locked package restore completed.
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
[PASS] Release repository tests completed.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

| Field | Value |
| --- | --- |
| Tests | 621 passed, 0 failed, 0 skipped (Client 621 in the gate run; Core 453 locally) |
| `measuredTicks` | 1 209 |
| `outcome` | `Faction0Victory` |
| `eventHash` | `CF8C3EDBC59C3319` |
| `stateHash` | `C669281B67CF8871` |
| `deterministic` | `true` |
| `firstMismatchTick` | `null` |
| `allocatedBytes` | 66 391 224 |
| Tick p50 / p95 / p99 / max | 0.0857 / 1.441 / 3.2661 / 10.4976 ms |

**Both hashes moved, which is the point.** The previous baseline was
`eventHash D379B60B2E30FFFC` and `stateHash 5BEBA7A68F69BE0D`. An unchanged
hash after this change would have meant the preset was not actually being
read.

Two independent verifications ran during implementation and are recorded
because they are what makes the move trustworthy:

- After the `WeaponId` symbol rename alone — `GreatBlade` to `Kampilan`,
  `HeavyChopper` to `Wasay`, `ThrustingBlade` to `Kalis`, `Bolo` to `Itak`,
  numeric values untouched — the seed-1 workload returned
  `eventHash D379B60B2E30FFFC` and `stateHash 5BEBA7A68F69BE0D`, byte-identical
  to the baseline. The rename is hash-neutral, as it must be, because the
  numeric value is the hashed quantity.
- V1's `ContentHash` still equals its pinned literal `0x59FB4CA563D87A49`,
  proving V1 was not disturbed. V2's is pinned at `0xE653F1802A447662`.

### 500-agent result, reported not asserted

`./scripts/benchmark.ps1 -Agents 500 -Ticks 10000 -Seed 1`:

| Field | Value |
| --- | --- |
| `outcome` | `Faction1Victory`, 0 against 7 survivors |
| `eventHash` | `B6FA93AB66696485` |
| `stateHash` | `DA4AA823020FAB3C` |
| `deterministic` | `true` |
| `allocatedBytes` | 316 682 016 |
| Tick p95 / p99 / max | 2.8523 / 4.8983 / 15.306 ms |
| `maximumPenetrationRaw` | 0 |

### A note on per-tick allocation

Adding the attacker's shield to `BattleEvent` — needed so a feed line can say
whether a one-handed blow was solo or shielded — first pushed the collision
allocation budget from its 900,000-byte ceiling to 982,744 bytes. Rather than
raise the ceiling, `Weapon`, `Shield`, and `HitLocation` were packed into a
single `int`. The event went from 80 bytes to 72, so it is now smaller with
three combat-context fields than it was with two.

## Previous non-interactive result — sound gain compensation, 2026-07-27

## Phase 2 reference pair, superseded at T39

Weapon clash, Phase 2. See
[docs/plans/2026-07-27-weapon-clash.md](../plans/2026-07-27-weapon-clash.md).
Every figure below comes from `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1`
run on this branch. These pairs are a comparand for the far side of the Phase 3
fan-out and are superseded once that work lands.

### Combat metrics reach neither hash

The combat metrics are derived observability counters. The repository treats
derived counters as never hashed, never snapshotted, and never persisted, and
nothing else in this plan would notice if one leaked into `StateHasher`: the
seam check predates the metrics, the zero-interception control run does not
speak to them, and the Phase 4 comparison is against a Phase 2 pair that would
already contain them. The proof is therefore the pair below, recorded
immediately before the accumulation was wired into the gather loop and again
immediately after, on the same workload and the same build.

| Field | Immediately before accumulation | Immediately after accumulation |
| --- | --- | --- |
| Commit | `75fd24f` | `10c4be9` |
| `measuredTicks` | 1 858 | 1 858 |
| `outcome` | `Faction1Victory` | `Faction1Victory` |
| `eventHash` | `A67575E7BAB6BDCC` | `A67575E7BAB6BDCC` |
| `stateHash` | `27DC94C6E9A01E35` | `27DC94C6E9A01E35` |
| `deterministic` | `true` | `true` |
| `firstMismatchTick` | `null` | `null` |

Both hashes are byte-identical across the change. That is the whole point of
recording them: accumulating the counters moved nothing the simulation reads.

The event hash in that pair, `A67575E7BAB6BDCC`, is not the Phase 2 reference
value. It was measured before the resolution was folded into the headless event
hash, which is a later task and which moved it on purpose. The reference pair is
below.

### The Phase 2 reference pair

Measured at commit `cffbb6c`, the end of Phase 2, by
`./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1`. Phase 3 is
presentation-only, so a Phase 4 run of the same workload must reproduce both
hashes byte for byte; any difference means presentation work leaked into the
simulation.

| Field | Value |
| --- | --- |
| `measuredTicks` | 1 858 |
| `outcome` | `Faction1Victory` |
| `eventHash` | `372C9217E5CB8BE9` |
| `stateHash` | `27DC94C6E9A01E35` |
| `deterministic` | `true` |
| `firstMismatchTick` | `null` |
| `allocatedBytes` | 122 880 440 |
| Tick p50 / p95 / p99 / max | 0.0871 / 1.0934 / 2.8755 / 9.4353 ms |
| Ruleset `ContentHash` | `0x4EAFE27A42DE87B2UL` (preset version 2) |

Combat metrics from the same run:

| Field | Value |
| --- | --- |
| `acceptedAttacks` | 3 026 |
| `landedAttacks` | 1 993 |
| `shieldBlockedAttacks` | 432 |
| `parriedAttacks` | 79 |
| `deflectedAttacks` | 237 |
| `evadedAttacks` | 285 |
| `defenceAttributableShare` | 0.3414 |

Both Phase 2 acceptance criteria are met with no re-tuning of the shipped
tables.

**Criterion one, interception share.** 0.3414 on seed 1, and across seeds 1 to
20 the share ranges from 0.3137 to 0.3478. Every seed is inside the enforced
0.25 to 0.45 band, and every seed is also inside the narrower 0.30 to 0.40
design target, which is not a gate.

**Criterion two, termination.** All twenty of twenty seeds decided before the
tick cap, and the median decisive tick is 1 916 against the 5 000 clause. Per
seed:

| Seed | Terminal tick | Outcome | Seed | Terminal tick | Outcome |
| ---: | ---: | --- | ---: | ---: | --- |
| 1 | 1 858 | `Faction1Victory` | 11 | 1 924 | `Faction1Victory` |
| 2 | 1 945 | `Faction0Victory` | 12 | 1 920 | `Faction1Victory` |
| 3 | 1 743 | `Faction1Victory` | 13 | 1 916 | `Faction0Victory` |
| 4 | 1 994 | `Faction1Victory` | 14 | 1 820 | `Faction0Victory` |
| 5 | 1 550 | `Faction0Victory` | 15 | 2 044 | `Faction0Victory` |
| 6 | 1 812 | `Faction1Victory` | 16 | 2 139 | `Faction1Victory` |
| 7 | 1 308 | `Faction0Victory` | 17 | 1 790 | `Faction1Victory` |
| 8 | 1 527 | `Faction1Victory` | 18 | 1 751 | `Faction1Victory` |
| 9 | 1 856 | `Faction0Victory` | 19 | 2 047 | `Faction0Victory` |
| 10 | 2 077 | `Faction0Victory` | 20 | 2 050 | `Faction0Victory` |

The battle lengthened from a terminal tick of 1 154 to 1 858 on seed 1, a factor
of 1.61 against the 1.48 the design predicted at a mean interception of 0.325.

### Two pre-existing cases Phase 2 had to amend

Two cases failed when Phase 2 landed. Neither was a criterion and neither was
owned by a Phase 2 task, so both were investigated before anything was edited,
and the owner approved each change on 2026-07-27.

**The last-stand blocked-streak bound was stale.** The case, now
`LastStandFormationTests.AMaximumSizedLastStandNeverLeavesAWarriorBlockedTooLongAcrossSeedsOneThroughTwenty`,
measured a longest blocked streak of 69 ticks against a 60-tick bound. The
decisive evidence that collision behaviour itself is unchanged came from the
ruleset seam: running the same scenario at the same commit with
`ClashProfile.Neutral` reproduces a streak of 45, which is exactly the figure
recorded when the 60-tick bound was chosen. Interception means fewer landed
blows per exchange, so battles last longer and a maximally packed cluster stays
packed longer; the collision resolver, the last-stand formation, and the
collision priority amendment are all untouched.

Seed 1 turned out to be a 25th-percentile seed for this metric, so the case now
sweeps twenty seeds and asserts on the worst. Across seeds 1 to 20 at the
maximum threshold the streak runs 59 to 92 with a median of 74, and the bound is
now 125 — 1.36 times the worst observed, the same headroom the original 60 had
over its measured 45. Risk R4, which the case guards, is a cluster that thrashes
permanently and produces a no-casualty draw at the tick limit: across those
twenty seeds no battle reached the tick limit, none drew, and none ended without
casualties, and terminal ticks ran 649 to 919 against a limit of 10 000.

**The shield survivability case could never have passed, for arithmetic
reasons.** It counted end-of-battle survivors and measured 41 shielded of 2 000
against 46 shieldless of 2 000. Maximum hit points are 100 and damage per attack
is 10, so exactly ten landed blows kill anyone. Shieldless entries take about
13.3 swings at an intercepted share of 0.26 and shielded entries about 16.3 at
0.39, so both absorb about 9.9 landed blows. Landed damage is equal by
construction, which pins survivorship, hit points remaining, and damage taken at
saturation regardless of how good the shield is. It is why the pre-clash
measurement read exactly 31 of 2 000 against 31 of 2 000.

The clash did close the gap, but only on blows absorbed before dying: 1.00
before, 1.22 after, with a per-seed minimum of 1.17 and a standard deviation of
0.04. The case was re-pointed at that statistic, given a PROVISIONAL band of
1.15, and renamed to
`PhilippineCombatIntegrationTests.ShieldedRosterEntriesAbsorbMoreBlowsBeforeDyingThanShieldlessOnesAcrossSeedsOneThroughTwenty`
so that it still claims what it measures. The same measurement against
`ZeroInterceptionRules` pools to 1.00 with a maximum of 1.02, so the bound
cannot be met without the clash.

One consequence worth carrying into Part B and the smoke rows: mean tick of
death separates the two groups by only 1.04, and already reads 1.02 with
interception switched off. A spectator therefore perceives the shield as blows
turned aside, not as a warrior who visibly lives longer, which is what the
per-resolution event-log labels in T54 have to convey.

## Latest non-interactive result — sound gain compensation, 2026-07-27

Presentation-only change: per-cue gain now scales with the number of voices
still sounding, and the per-frame cue budget was raised from a throttle to a
backstop. See
[docs/plans/2026-07-27-sound-gain-compensation.md](../plans/2026-07-27-sound-gain-compensation.md)
and [docs/research/SOUND-CAPACITY-MEASUREMENTS.md](../research/SOUND-CAPACITY-MEASUREMENTS.md).

`./scripts/verify.ps1 -SkipBootstrap` passed at all five stages:

```
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
[PASS] Release repository tests completed.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

| Field | Value |
| --- | --- |
| `Hukbo.Client.Tests` | 585 passed, 0 failed, 0 skipped |
| `measuredTicks` | 1 154 |
| `outcome` | `Faction1Victory` |
| `eventHash` | `D379B60B2E30FFFC` |
| `stateHash` | `5BEBA7A68F69BE0D` |
| `deterministic` | `true` |
| `allocatedBytes` | 71 704 672 |
| Tick p50 / p95 / p99 / max | 0.0955 / 1.5235 / 2.5473 / 9.3252 ms |

**Both hashes are unchanged from the collision priority fairness baseline
recorded further down.** That is the point: nothing in this change reaches
`Hukbo.Core`, so a moved hash would have meant the change was wrong. Those
hashes were the authoritative baseline until preset V2 replaced them; the
weapon identity section above is now the current one.

Audio evidence, from `tools/Hukbo.Tools.MixAnalysis` against the shipped policy:
every cue played, zero suppressed, and peak level between −6.1 and −0.2 dBFS
with zero flattened samples at 200 and 500 agents and at 1x and 4x. Before the
change the same workloads peaked between +7.7 and +11.0 dBFS.

Every row in the sound gain compensation smoke checklist is `PENDING`. Nothing
here proves how it sounds.

## Superseded: the collision priority fairness run

Every figure in this section comes from one final verified run of the collision
priority fairness change on 2026-07-27, taken on the
`feature/collision-priority-fairness` branch. See
[docs/archives/2026-07-27/2026-07-27-collision-priority-fairness-design.md](../archives/2026-07-27/2026-07-27-collision-priority-fairness-design.md),
kept for traceability only, and section 9 of
[docs/decisions/2026-07-27-collision-policy.md](../decisions/2026-07-27-collision-policy.md).

Both hashes moved because this is an authoritative movement change: movers are
now resolved in ascending per-tick `CollisionPriority` key instead of ascending
`EntityId`, so contested ground goes to a different agent and agents finish
ticks in different places. No state field, event kind, or enum value was added
or reordered, and `CombatRuleset.ContentHash` is unchanged at
`0x59FB4CA563D87A49`.

**Everything below the next heading predates this change and is superseded.**

### Canonical gate

`./scripts/verify.ps1` passed at all five stages: prerequisite validation and
locked restore, format verification, the Release solution build with 0 warnings
and 0 errors, the Release repository tests, and the seed-1 / 200-agent /
10,000-tick headless determinism workload. It ended with
`[PASS] Canonical repository verification completed.`

| Suite | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| `Hukbo.Core.Tests` | 418 | 0 | 0 |
| `Hukbo.Client.Tests` | 564 | 0 | 0 |

The Core figure was recorded as 412 when this section was first written and was
corrected to 418 by the role 17 handoff review on 2026-07-27, which measured
`dotnet test tests/Hukbo.Core.Tests -c Release` directly at merge commit
`8815a3c` and read back `Passed: 418, Failed: 0, Skipped: 0`. The merge added no
test file that the branch tip `c01ea9f` did not already carry, so the branch and
`main` run the identical suite and 418 is the count for both. The paragraph
below already implied that figure: 398 plus 20 is 418. See
[docs/agents/17-technical-review-handoff.md](../agents/17-technical-review-handoff.md).

The Core count rises from `main`'s 398 by 20: 19 new `CollisionPriorityTests`
cases, counting theory rows, covering five golden mixer vectors, the key's
purity, its sensitivity to each of seed, tick and entity, the entity ID in its
low half, distinctness across a tick, the absence of a standing advantage for
either faction's ID range, the per-tick reshuffle observed through the battle
simulation itself, and the rejected inputs; and one new `CollisionResolverTests`
case proving the resolver follows the key rather than the entity ID. Two further
cases were rewritten rather than added: the `DeterminismTests` contested-ground
case, and `SeedsOneThroughTwentyProduceVictoriesForBothFactions`, strengthened
from "at least one victory each" to "at least four each" — it had been passing
on exactly one seed. The Client count is unchanged from `main`'s 564: no
`Hukbo.Client` file was touched.

Two of those tests exist because a review found the rule was underconstrained.
`TheContestSequenceFollowsThePerTickShuffle` was verified by mutation: replacing
`Tick` with a constant in `BattleSimulation.ResolveCollisions` makes it fail, and
before it was added the whole 412-case suite stayed green under that mutation.
The randomized crowd fixture in `CollisionResolverTests` now generates real
per-tick keys, so the resolver's no-penetration invariant is fuzzed against the
shuffled order the battle actually uses rather than the retired ascending-ID
order.

### 200-agent acceptance workload

`./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1`. This is the current
recorded oracle.

| Field | Value |
| --- | --- |
| Measured ticks | 1154 |
| Outcome | `Faction1Victory` |
| Faction 0 survivors | 0 |
| Faction 1 survivors | 3 |
| State hash | `5BEBA7A68F69BE0D` |
| Event hash | `D379B60B2E30FFFC` |
| Deterministic | `true` |
| First mismatch tick | `null` |
| Tick p50 | 0.0951 ms |
| Tick p95 | 1.5156 ms |
| Tick p99 | 2.4546 ms |
| Tick maximum | 8.4526 ms |
| Allocated | 71,698,480 bytes |

Collision metrics for the same run:

| Metric | Value |
| --- | --- |
| `candidatePairs` | 110,970 |
| `contactPairs` | 5,198 |
| `acceptedMoves` | 71,780 |
| `blockedAgentTicks` | 24,703 |
| `attackCapableAgentTicks` | 9,231 |
| `longestBlockedStreakTicks` | 47 |
| `maximumFrontWidthRaw` | 629,652 |
| `maximumFrontDepthRaw` | 51,086 |
| `maximumPenetrationRaw` | 0 |

### 500-agent stress workload

The same command with `-Agents 500`. Report only; not gated.

| Field | Value |
| --- | --- |
| Measured ticks | 2668 |
| Outcome | `Faction0Victory` |
| Faction 0 survivors | 1 |
| Faction 1 survivors | 0 |
| State hash | `FE44ADA93E0E202A` |
| Event hash | `9C8EF5CB79810560` |
| Deterministic | `true` |
| First mismatch tick | `null` |
| Tick p50 | 0.2609 ms |
| Tick p95 | 1.813 ms |
| Tick p99 | 4.4052 ms |
| Tick maximum | 13.19 ms |
| Allocated | 416,546,128 bytes |

| Metric | Value |
| --- | --- |
| `candidatePairs` | 699,589 |
| `contactPairs` | 12,497 |
| `acceptedMoves` | 372,527 |
| `blockedAgentTicks` | 102,147 |
| `attackCapableAgentTicks` | 23,319 |
| `longestBlockedStreakTicks` | 54 |
| `maximumFrontWidthRaw` | 637,159 |
| `maximumFrontDepthRaw` | 69,415 |
| `maximumPenetrationRaw` | 0 |

### The seed distribution, which is the point of the change

`./scripts/benchmark.ps1 -Agents 200 -Ticks 10000`, one run per seed, outcomes
counted:

| Build | Seeds | Faction 0 | Faction 1 | Draw |
| --- | --- | --- | --- | --- |
| `main`, before this change | 1-20 | 1 | 19 | 0 |
| This change | 1-20 | 7 | 13 | 0 |
| This change | 21-40 | 9 | 10 | 1 |
| This change | 1-40 | 16 | 23 | 1 |

The old rule gave faction 0 every cross-faction push of every battle, which cost
it 19 seeds in 20. It now wins 16 of 40. That is not a claim of a perfectly fair
simulation — 16 against 23 over 40 samples still leans, and 40 battles is a
small sample — but the standing structural advantage is gone.

The seed-24 draw is a genuine mutual annihilation at tick 1197 with zero
survivors on both sides, not a `TickLimit` timeout. Draws were previously
unobserved in this range.

One caveat on the 500-agent stress row above: at 250 warriors per faction,
`CombatRuleset.ResolveLoadout` — which keys off the **global** entity ID while
positions are mirrored by faction-local index — gives faction 1 two more
tall-hardwood shields than faction 0. That workload therefore compares slightly
unequal armies. It is report-only and it is not the evidence for this change;
the seed census above uses the 200-agent workload, where 100 per faction divides
evenly into the four-entry roster and both armies are identical. The loadout
asymmetry is a separate defect, recorded in the design document and not fixed
here.

### What moved, on the same workload

| Metric | Last-stand run | This change |
| --- | --- | --- |
| Terminal tick, 200 agents | 1176 | 1154 |
| `acceptedMoves`, 200 agents | 67,112 | 71,780 |
| `blockedAgentTicks`, 200 agents | 28,609 | 24,703 |
| `maximumFrontDepthRaw`, 200 agents | 40,469 | 51,086 |
| `maximumPenetrationRaw`, 200 agents | 0 | 0 |

Accepted moves rose and blocked agent ticks fell by roughly the same proportion,
which is the mechanical signature of the change: an agent that lost a contest
last tick can win the next one, so fewer agents sit blocked for long runs. Front
depth grew because both sides now push into one another instead of one side
consistently giving way. Penetration stayed at exactly zero, which is the guard.

### Cost

Tick p50 at 200 agents rose from 0.0672 ms to 0.0951 ms. **That figure is not a
clean attribution**: the two runs are different battles — 1176 ticks against
1154, seven per cent more accepted moves, twenty-six per cent more front depth —
so an unknown part of the difference is the battle rather than the rule. The
rule's own cost is one FNV-1a mix per mover per tick plus one sort of at most
`TotalAgents` keys, which for 200 movers is microseconds, not tens of them. An
A/B at a fixed tick count on one seed would separate the two and has not been
run.

In absolute terms the measured p50 is a tenth of a millisecond against a 50 ms
tick budget at the 20 Hz tick rate. p95, p99 and the maximum are within noise of
the previous run, the 500-agent percentiles are lower rather than higher, and the
allocation figures are comparable at both populations, so the sort buffers did
not add steady-state allocation. If a future population makes the sort matter,
the recorded fallback is a per-tick rotation of the ascending order, which is
O(1) and delivers roughly half of the cross-faction pairs to each side.

### Superseded oracles

Dead values, kept so the transition can be traced. Not regression targets.

| Superseded oracle | State hash | Event hash | Note |
| --- | --- | --- | --- |
| 200 agents, seed 1, last-stand run | `BBB40D2240720DC8` | `2A6BAEA1E3567046` | Terminal tick 1176. Superseded by the priority amendment. |
| 500 agents, seed 1, last-stand run | `73FB96A4C5963149` | `1531FF58B7C7557B` | Report-only workload. Superseded by the priority amendment. |

### Interactive verification

**Not performed.** No `Hukbo.Client` file changed, and the visible effect of this
change is a statistical one across many battles rather than anything a single
frame shows. The one single-screen observation worth making is recorded as a
`PENDING` row in the collision readability checklist below: a second-rank agent
pressed against the same enemy should alternate between blocked and moving
rather than staying blocked for the whole engagement.

## Superseded: the last-stand formation run

Every figure in this section comes from one final verified run of the
last-stand formation change on 2026-07-27, taken on the
`worktree-last-stand-formation` branch after it was rebased onto `main`'s
mirrored starting-formation deployment. See
[docs/plans/2026-07-27-last-stand-formation-design.md](../plans/2026-07-27-last-stand-formation-design.md)
and
[docs/plans/2026-07-27-last-stand-formation.md](../plans/2026-07-27-last-stand-formation.md).
Nothing here is estimated, rounded, or carried over from an earlier run.

Both hashes moved because this is an authoritative movement change: a
faction's last survivors now rally on their own lowest-`EntityId` comrade
instead of continuing to advance on the nearest enemy once the faction's
living count drops to `Scenario.LastStandThresholdAgents` or fewer, so
regrouping survivors stand in different places than they would under ordinary
targeting, and a regrouping warrior's `Move` event names its rally agent in
the event's target field rather than an enemy.

**Everything below the next heading predates this change and is superseded.**

### Canonical gate

`./scripts/verify.ps1 -SkipBootstrap` passed at all five stages: prerequisite
validation and locked restore, format verification, the Release solution
build, the Release repository tests, and the seed-1 / 200-agent / 10,000-tick
headless determinism workload. It ended with
`[PASS] Canonical repository verification completed.` The Release build
produced 0 warnings and 0 errors.

| Suite | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| `Hukbo.Core.Tests` | 398 | 0 | 0 |
| `Hukbo.Client.Tests` | 564 | 0 | 0 |

The Core count rises from `main`'s 351 by the 47 new last-stand tests. The
Client count is unchanged from `main`'s 564: no `Hukbo.Client` file was touched
by this change.

### 200-agent acceptance workload

`./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1`. This is the current
recorded oracle.

| Field | Value |
| --- | --- |
| Measured ticks | 1176 |
| Outcome | `Faction1Victory` |
| Faction 0 survivors | 0 |
| Faction 1 survivors | 3 |
| State hash | `BBB40D2240720DC8` |
| Event hash | `2A6BAEA1E3567046` |
| Deterministic | `true` |
| First mismatch tick | `null` |
| Tick p50 | 0.0672 ms |
| Tick p95 | 1.4434 ms |
| Tick p99 | 2.4551 ms |
| Tick maximum | 7.3394 ms |
| Allocated | 72,856,392 bytes |

Collision metrics for the same run:

| Metric | Value |
| --- | --- |
| `candidatePairs` | 107,401 |
| `contactPairs` | 4,974 |
| `acceptedMoves` | 67,112 |
| `blockedAgentTicks` | 28,609 |
| `attackCapableAgentTicks` | 9,248 |
| `longestBlockedStreakTicks` | 48 |
| `maximumFrontWidthRaw` | 630,752 |
| `maximumFrontDepthRaw` | 40,469 |
| `maximumPenetrationRaw` | 0 |

### 500-agent stress workload

The same command with `-Agents 500`. Report only; not gated.

| Field | Value |
| --- | --- |
| Measured ticks | 2245 |
| Outcome | `Faction1Victory` |
| Faction 0 survivors | 0 |
| Faction 1 survivors | 5 |
| State hash | `73FB96A4C5963149` |
| Event hash | `1531FF58B7C7557B` |
| Deterministic | `true` |
| First mismatch tick | `null` |
| Tick p50 | 0.3384 ms |
| Tick p95 | 2.9438 ms |
| Tick p99 | 4.5846 ms |
| Tick maximum | 11.4977 ms |
| Allocated | 355,573,472 bytes |

| Metric | Value |
| --- | --- |
| `candidatePairs` | 636,139 |
| `contactPairs` | 12,722 |
| `acceptedMoves` | 346,926 |
| `blockedAgentTicks` | 91,845 |
| `attackCapableAgentTicks` | 23,112 |
| `longestBlockedStreakTicks` | 48 |
| `maximumFrontWidthRaw` | 639,480 |
| `maximumFrontDepthRaw` | 62,961 |
| `maximumPenetrationRaw` | 0 |

### What the last-stand formation moved, on the same workload

| Metric | Mirrored deployment | Last-stand formation |
| --- | --- | --- |
| Terminal tick, 200 agents | 1081 | 1176 |
| `longestBlockedStreakTicks`, 200 agents | 48 | 48 |
| `maximumPenetrationRaw`, 200 agents | 0 | 0 |
| Allocated, 200 agents | 69,693,688 bytes | 72,856,392 bytes |

The battle runs 95 ticks longer under the last-stand formation, and
`longestBlockedStreakTicks` stayed unchanged at exactly 48 on both the
200-agent and 500-agent workloads: the rally cluster does not create a new
worst-case blocked streak anywhere on the field. `maximumPenetrationRaw`
stayed at exactly 0, which is the guard: the last-stand formation did not
weaken the solid-disc invariant. Allocation rose from 69,693,688 to 72,856,392
bytes on the 200-agent workload, consistent with more ticks paid for rather
than a new steady-state allocation source — the battle also ran 95 ticks
longer.

### Superseded oracles

Dead values, kept so the transition can be traced. None may be used as a
regression target.

| Superseded oracle | State hash | Event hash | Note |
| --- | --- | --- | --- |
| 200 agents, seed 1, amended collision | `D78F0B527B7F938F` | `AC3BAAEC684854D5` | Terminal tick 657. Superseded by the mirrored deployment. |
| 500 agents, seed 1, amended collision | `C81B4F48DE54B983` | `D03F1213563DFD49` | Report-only workload. Superseded by the mirrored deployment. |
| 200 agents, seed 1, mirrored deployment | `DC7F2E7A107C885A` | `6C641E90DDF0B943` | Terminal tick 1081, 3 survivors. Superseded by the last-stand formation, an authoritative movement change. |
| 500 agents, seed 1, mirrored deployment | `0C53793DEB700A53` | `4F373537096F2551` | Terminal tick 2231. Report-only workload. Superseded by the last-stand formation, an authoritative movement change. |

The combat preset is untouched: `CombatRuleset.ContentHash` is still
`0x59FB4CA563D87A49`, asserted by two tests in the passing suite.

### Interactive verification

**Not performed.** The opening frame is the whole visible point of the
mirrored deployment, and the converging endgame is the whole visible point of
the last-stand formation, and no person has watched either in a live window.
The rows in the deployment smoke checklist and the new last-stand formation
smoke checklist below stay `PENDING`.

## Superseded: the mirrored starting-formation deployment run

Every figure in this section comes from the mirrored starting-formation change
on 2026-07-27, taken on the `feature/starting-formations` branch. Starting
positions are now planned once per battle as a set of contingents and mirrored
across the vertical centre line, so both hashes moved. See
[docs/archives/2026-07-27/2026-07-27-starting-formations-design.md](../archives/2026-07-27/2026-07-27-starting-formations-design.md),
kept for traceability only.

**This entire section is superseded by the last-stand formation run recorded
at the top of this file.** Its two oracle pairs are the mirrored-deployment
rows in that section's "Superseded oracles" table. Everything in this section,
including the "Everything below the next heading predates this change and is
superseded" sentence that follows, described the live baseline only until the
last-stand formation shipped.

**Everything below the next heading predates this change and is superseded.**

### Canonical gate

`./scripts/verify.ps1` passed at all five stages: prerequisite validation and
locked restore, format verification, the Release solution build with zero
warnings, the Release repository tests, and the seed-1 / 200-agent /
10,000-tick headless determinism workload.

| Suite | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| `Hukbo.Core.Tests` | 351 | 0 | 0 |
| `Hukbo.Client.Tests` | 532 | 0 | 0 |

These are post-merge figures, taken from a clean checkout of the merge commit.
The Client count is the 532 the camera auto-pan change brought with it; no
Client test was added or changed here.

The Core count is 25 higher than the 326 recorded on `main`; all 25 are the new
`FormationPlannerTests`, which cover mirror symmetry, spawn clearance, map
bounds, half-of-map containment on narrow maps, seed reproducibility, the
five-contingent structure of a default army, the eight-contingent cap, the
crowded-map fallback lattice, and the minimum-map, maximum-map, narrow-half and
single-warrior edge cases. No Client code changed and the Client count is
unchanged.

### 200-agent acceptance workload

`./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1`. This was the
recorded oracle before the last-stand formation.

| Field | Value |
| --- | --- |
| Measured ticks | 1081 |
| Outcome | `Faction1Victory` |
| Faction 0 survivors | 0 |
| Faction 1 survivors | 3 |
| State hash | `DC7F2E7A107C885A` |
| Event hash | `6C641E90DDF0B943` |
| Deterministic | `true` |
| First mismatch tick | `null` |
| Tick p50 | 0.0827 ms |
| Tick p95 | 1.2937 ms |
| Tick p99 | 2.4169 ms |
| Tick maximum | 7.3589 ms |
| Allocated | 69,693,688 bytes |

Collision metrics for the same run:

| Metric | Value |
| --- | --- |
| `candidatePairs` | 107,634 |
| `contactPairs` | 5,007 |
| `acceptedMoves` | 66,416 |
| `blockedAgentTicks` | 29,040 |
| `attackCapableAgentTicks` | 9,283 |
| `longestBlockedStreakTicks` | 48 |
| `maximumFrontWidthRaw` | 630,752 |
| `maximumFrontDepthRaw` | 29,114 |
| `maximumPenetrationRaw` | 0 |

### 500-agent stress workload

The same command with `-Agents 500`. Report only; not gated.

| Field | Value |
| --- | --- |
| Measured ticks | 2231 |
| Outcome | `Faction1Victory` |
| Faction 0 survivors | 0 |
| Faction 1 survivors | 3 |
| State hash | `0C53793DEB700A53` |
| Event hash | `4F373537096F2551` |
| Deterministic | `true` |
| First mismatch tick | `null` |
| Tick p50 | 0.3425 ms |
| Tick p95 | 2.6284 ms |
| Tick p99 | 4.9597 ms |
| Tick maximum | 11.6425 ms |
| Allocated | 358,456,096 bytes |

| Metric | Value |
| --- | --- |
| `candidatePairs` | 636,262 |
| `contactPairs` | 12,746 |
| `acceptedMoves` | 346,688 |
| `blockedAgentTicks` | 92,070 |
| `attackCapableAgentTicks` | 23,207 |
| `longestBlockedStreakTicks` | 48 |
| `maximumFrontWidthRaw` | 639,480 |
| `maximumFrontDepthRaw` | 62,961 |
| `maximumPenetrationRaw` | 0 |

### What the deployment change moved, on the same workload

| Metric | Amended collision run | Mirrored deployment |
| --- | --- | --- |
| Terminal tick, 200 agents | 657 | 1081 |
| Faction 1 survivors, 200 agents | 10 | 3 |
| `contactPairs`, 200 agents | 5,649 | 5,007 |
| `blockedAgentTicks`, 200 agents | 14,544 | 29,040 |
| `maximumFrontDepthRaw`, 200 agents | 51,072 | 29,114 |
| `maximumPenetrationRaw`, 200 agents | 0 | 0 |

The battles now run considerably longer and end with fewer survivors on the
winning side. Front depth roughly halved and blocked agent ticks roughly
doubled, both consistent with armies that arrive as several columns and queue up
behind their own contingents instead of converging as one cloud. Penetration
stayed at exactly zero, which is the guard: the deployment change did not weaken
the solid-disc invariant.

The win distribution went the other way and that must be recorded, not glossed.
Measured directly, `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000` over seeds
1 to 20:

| Build | Faction 0 wins | Faction 1 wins |
| --- | --- | --- |
| `main` | 4 | 16 |
| This change | 1 | 19 |

Individual battles are closer; which faction wins is more predictable. The cause
is not an unfair deployment — both armies now hold identical ground. It is that
a symmetric deployment leaves the entity-ID ordering rule as the only asymmetry
in the simulation, and that rule always favours the same faction. Random spawns
used to hide it behind noise. Planning each faction from its own jitter draws
was implemented and measured as a mitigation and produced the same 1/19 split,
so it was reverted. Correcting the underlying bias is a tick-rule change that
needs its own decision record and was not attempted here.
`SeedsOneThroughTwentyProduceVictoriesForBothFactions` still passes, on one
seed.

Allocation rose from 42,568,888 to 69,693,688 bytes on the 200-agent workload.
That is **not** an efficiency regression claim in either direction: the battle
also ran 424 ticks longer, and per-tick timing is unchanged or slightly better
(p50 0.0878 ms to 0.0827 ms). The next meaningful allocation comparison is
against the 69,693,688-byte figure above, at the same agent count and seed.

### Superseded oracles

Dead values, kept so the transition can be traced. None may be used as a
regression target.

| Superseded oracle | State hash | Event hash | Note |
| --- | --- | --- | --- |
| 200 agents, seed 1, amended collision | `D78F0B527B7F938F` | `AC3BAAEC684854D5` | Terminal tick 657. Superseded by the mirrored deployment. |
| 500 agents, seed 1, amended collision | `C81B4F48DE54B983` | `D03F1213563DFD49` | Report-only workload. Superseded by the mirrored deployment. |

The combat preset is untouched: `CombatRuleset.ContentHash` is still
`0x59FB4CA563D87A49`, asserted by two tests in the passing suite.

### Interactive verification

**Not performed.** The opening frame is the whole visible point of this change
and no person has watched it in a live window. The rows in the deployment smoke
checklist below stay `PENDING`.

### Font and text quality gate run — 2026-07-27

`./scripts/verify.ps1 -SkipBootstrap` was run at the repository root on
2026-07-27 after the font and text quality change (design document
[docs/plans/2026-07-27-font-text-quality-design.md](../plans/2026-07-27-font-text-quality-design.md),
plan document
[docs/plans/2026-07-27-font-text-quality.md](../plans/2026-07-27-font-text-quality.md)).
It ended with `[PASS] Canonical repository verification completed.` and printed
exactly:

```
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
[PASS] Release repository tests completed.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

`Hukbo.Client.Tests` reported 564 passed and 0 failed; `Hukbo.Core.Tests`
reported 351 passed and 0 failed. The Core count is unchanged from the 351
recorded above, because zero files under `src/Hukbo.Core` were touched. The
Client count rises from the 532 recorded above by the new tests this change
added — the font ramp, the font set, the whole-pixel text geometry helper, and
the extended theme catalog coverage for the six-role font map. The Release
build produced 0 warnings and 0 errors.

The seed-1, 200-agent, 10,000-tick headless workload's `RunReport` recorded
seed `1`, `agentCount` `200`, `requestedTicks` `10000`, `measuredTicks` `1081`,
outcome `Faction1Victory`, `faction0Survivors` `0`, `faction1Survivors` `3`,
state hash `DC7F2E7A107C885A`, event hash `6C641E90DDF0B943`,
`deterministic: true`, `firstMismatchTick: null`, tick p50 `0.0827` ms, p95
`1.3886` ms, p99 `2.4117` ms, maximum `6.9264` ms, and `allocatedBytes`
`69693688`.

**Both hashes were unchanged from the 200-agent acceptance oracle this section
recorded above** (`DC7F2E7A107C885A` and `6C641E90DDF0B943`, respectively).
That was the expected result for a presentation-only change: the font ramp,
the six vendored typeface bakes, the sampler-state switch from `PointClamp` to
`LinearClamp` in the user interface sprite batch, and the whole-pixel text
geometry helper all live entirely in `Hukbo.Client`, and the scope boundary
enforced by the font plan means zero files under `src/Hukbo.Core`,
`src/Hukbo.Headless`, or `tests/Hukbo.Core.Tests` were touched. Both hashes
are now dead values in their own right, superseded along with the rest of
this section by the last-stand formation run at the top of this file.

The pair `D78F0B527B7F938F` and `AC3BAAEC684854D5`, recorded further down this
file both under this section's "Superseded oracles" table and again under
"Superseded: the amended collision run", is the terminal-tick-657
amended-collision baseline. It was superseded by the mirrored
starting-formation deployment change before this font work began, and it was
**not** the current baseline even when this entry was written; it must not be
cited as one, and it is not the pair this run reproduced.

These results proved the non-interactive gate only. No visual claim was made by
this entry. The "Typography smoke" subsection in the interactive checklist
below remains `PENDING`, and the display-scaling measurement task (gated,
separate, and requiring a human at an interactive Windows desktop) remains
untouched by this run.

## Superseded: the amended collision run

Every figure in this section comes from one final verified run of the **amended**
collision change on 2026-07-27, taken on the `feature/collision-mechanics`
branch after the contact-closing amendment recorded in
[docs/decisions/2026-07-27-collision-policy.md](../decisions/2026-07-27-collision-policy.md).
Nothing here is estimated, rounded, or carried over from an earlier run.

**Every result recorded further down this file predates the amendment.** The
pre-amendment collision figures, the plains-backdrop run, the sound-system run,
the sound-variant run, and the blood-and-gore run were all taken before agents
closed to body contact and before the contact metric used a proximity band. They
are kept as history and must not be read as current.

Note on test counts: collision was verified on a branch taken before the
sound-variant work was committed, so this section's 437 Client tests and the
sound-variant run's 505 are each partial views. After the merge, `main` reports
**326 Core and 513 Client tests passing, 0 failed**, with the canonical gate
green at all five stages. The differing branch figures are a sequencing artefact,
not a lost test.

Environment: Windows 11 Pro 10.0.26200, .NET SDK 10.0.302 as pinned in
`global.json`. The CPU model and installed memory were not captured, so they are
not stated; a future performance comparison that depends on them has to capture
them first.

### Canonical gate

`./scripts/verify.ps1 -SkipBootstrap` passed at all five stages: format
verification, the Release solution build with zero warnings, the Release
repository tests, the seed-1 / 200-agent / 10,000-tick headless determinism
workload, and the overall gate.

| Suite | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| `Hukbo.Core.Tests` | 326 | 0 | 0 |
| `Hukbo.Client.Tests` | 437 | 0 | 0 |

Both counts are higher than the figures recorded for the pre-amendment collision
run because `main` was merged into this branch in the meantime, bringing the
sound, plains backdrop, blood, and army-composition suites with it. The increase
is not attributable to the collision work and must not be cited as its coverage.

### 200-agent acceptance workload

`./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1`. This is the
acceptance workload named in the collision policy decision record, and these
values are the current recorded oracle.

| Field | Value |
| --- | --- |
| Measured ticks | 657 |
| Outcome | `Faction1Victory` |
| Faction 0 survivors | 0 |
| Faction 1 survivors | 10 |
| State hash | `D78F0B527B7F938F` |
| Event hash | `AC3BAAEC684854D5` |
| Deterministic | `true` |
| First mismatch tick | `null` |
| Tick p50 | 0.0878 ms |
| Tick p95 | 1.6322 ms |
| Tick p99 | 2.1088 ms |
| Tick maximum | 9.249 ms |
| Allocated | 42,568,888 bytes |

Collision metrics for the same run:

| Metric | Value |
| --- | --- |
| `candidatePairs` | 57,295 |
| `contactPairs` | 5,649 |
| `acceptedMoves` | 40,868 |
| `blockedAgentTicks` | 14,544 |
| `attackCapableAgentTicks` | 8,945 |
| `longestBlockedStreakTicks` | 52 |
| `maximumFrontWidthRaw` | 549,331 |
| `maximumFrontDepthRaw` | 51,072 |
| `maximumPenetrationRaw` | 0 |

### 500-agent stress workload

The same command with `-Agents 500`. This workload is **report only**. It is not
gated, and its timing and allocation figures are recorded rather than budgeted.

| Field | Value |
| --- | --- |
| Measured ticks | 978 |
| Outcome | `Faction1Victory` |
| Faction 0 survivors | 0 |
| Faction 1 survivors | 17 |
| State hash | `C81B4F48DE54B983` |
| Event hash | `D03F1213563DFD49` |
| Deterministic | `true` |
| First mismatch tick | `null` |
| Tick p50 | 0.3167 ms |
| Tick p95 | 1.9138 ms |
| Tick p99 | 4.1672 ms |
| Tick maximum | 12.6946 ms |
| Allocated | 157,426,736 bytes |

| Metric | Value |
| --- | --- |
| `candidatePairs` | 280,675 |
| `contactPairs` | 14,270 |
| `acceptedMoves` | 155,460 |
| `blockedAgentTicks` | 48,573 |
| `attackCapableAgentTicks` | 22,848 |
| `longestBlockedStreakTicks` | 61 |
| `maximumFrontWidthRaw` | 695,062 |
| `maximumFrontDepthRaw` | 50,868 |
| `maximumPenetrationRaw` | 0 |

### What the amendment moved, on the same workload

Stated plainly, because these four numbers are the whole point of the amendment.
All figures are the 200-agent, seed-1 workload.

| Metric | Before the amendment | After the amendment |
| --- | --- | --- |
| `contactPairs` | 0 | 5,649 |
| `blockedAgentTicks` | 7,154 | 14,544 |
| Terminal tick | 781 | 657 |
| `maximumPenetrationRaw` | 0 | 0 |

Contact went from unobservable to observable, crowding roughly doubled, the
battle resolves sooner because the fighting ranks are closer together, and
penetration stayed at exactly zero. The last row is the guard: neither change
weakened the solid-disc invariant.

### Tactical guards inside the passing suite

Three named guards ride inside the 326 passing `Hukbo.Core.Tests` above rather
than in a separate report, because they are ordinary deterministic tests:

- `SeedsOneThroughTwentyProduceVictoriesForBothFactions` keeps the seed
  distribution honest, so solid contact did not turn every seed into a draw or
  hand every seed to one faction.
- `PackedFront_OpposingBodiesInContactStayInsideReachAndDealDamage` proves a
  packed line stays inside the approved attack geometry and deals damage instead
  of deadlocking.
- `PackedFront_DenseLinesThatMarchIntoReachStillDealDamage` proves agents that
  have to march into reach through their own crowd still get there and still deal
  damage.

### Reading the hashes and the allocation figure

Both hashes moved again, and the movement is expected and was approved in
advance. The amendment changed the approach target from attack range to body
contact, which changes where agents stand and therefore changes both the state
hash and the ordered event stream. The proximity band introduced for contact
metrics moved neither hash: it was confirmed byte-identical before and after,
which is the evidence that it stayed derived rather than authoritative.

The tables above are the only recorded oracle. Two earlier pairs are
**superseded** and are listed here so the transition can be traced rather than
guessed at. They are dead values and may not be used as a regression target:

| Superseded oracle | State hash | Event hash | Note |
| --- | --- | --- | --- |
| 200 agents, seed 1, pre-amendment | `7EE8BF6EC0F11BB2` | `9BFC18AD06F4F572` | Terminal tick 781. Superseded by the amendment. |
| 500 agents, seed 1, pre-amendment | `7402CCC7C6EC3B50` | `619CCC872BBB2413` | Report-only workload. Superseded by the amendment. |
| 200 agents, seed 1, pre-collision | `6EBB1EA63114F6CE` | `941377BD43C556FF` | Terminal tick 235. Superseded when the collision policy first shipped. |

Allocation for the 200-agent workload is 42,568,888 bytes, against the 50,454,728
bytes recorded before the amendment. That is a same-agent-count, same-seed
comparison, but it is **not** a like-for-like efficiency claim: the amended battle
also ends 124 ticks earlier, so fewer ticks were paid for. Neither figure is
comparable to the much older 15,128,696-byte measurement, which covered a far
shorter battle under a different contact rule, and no ratio between them is
stated here. The open allocation-packing item in
[docs/plans/2026-07-27-battle-event-allocation-packing.md](../plans/2026-07-27-battle-event-allocation-packing.md)
is unaffected by the collision work and remains the place where per-event
allocation is paid down. The next meaningful allocation comparison is against the
42,568,888-byte figure above, at the same agent count and the same seed.

The collision stage itself is required to add no steady-state allocation: all
grid, pair, proposal, and resolution storage is preallocated and reused, and a
Release test asserts that a warm collision tick reuses its buffers.

### Collision metric definitions

These counters are derived observability data. They are never hashed, never
snapshotted, and never persisted, so they cannot influence an outcome. Two
same-seed runs of the same build must produce identical values in every field.

| Metric | Definition |
| --- | --- |
| `candidatePairs` | Living pairs the metrics broad phase emitted, summed over ticks: every pair whose bodies are inside the proximity band described below, allies and enemies alike. |
| `contactPairs` | The cross-faction subset of `candidatePairs`, summed over ticks. This is the fighting front rather than incidental friendly crowding. |
| `acceptedMoves` | Movement proposals that resolved to a destination other than the agent's tick-start position, summed over ticks. |
| `blockedAgentTicks` | One unit per agent per tick that resolved to `MovementResolution.Blocked`. An agent-tick count, not a count of distinct agents. |
| `attackCapableAgentTicks` | One unit per agent per tick in which that agent held a target inside attack reach at its resolved position. Also an agent-tick count. |
| `longestBlockedStreakTicks` | The longest run of consecutive ticks any single agent spent blocked. A running maximum, not a sum. |
| `maximumFrontWidthRaw` | The largest vertical span, in raw fixed-point units, of the agents holding an enemy inside attack reach in any one tick. A running maximum. |
| `maximumFrontDepthRaw` | The horizontal span of that same set, in raw fixed-point units. A running maximum. |
| `maximumPenetrationRaw` | The deepest overlap between two living bodies observed at the end of any tick, in raw fixed-point units. A guard metric, not a tuning signal: under `CollisionPolicy.Solid` a correct run reports exactly `0`, and any nonzero value is a contract violation. |

**`candidatePairs` and `contactPairs` are counted over a proximity band, not over
exact tangency.** This is the single most important thing to understand before
reading either figure. The solid resolver guarantees that every living pair ends
the tick at or beyond `(2R)^2`, so an exact-tangency test asks for a squared
distance of *precisely* `(2R)^2`. On an integer lattice that needs a Pythagorean
coincidence between the two axis deltas and the diameter, and it is unreachable
in practice. That is the mechanical reason the earlier run reported `contactPairs`
of `0`: an exact-tangency counter can essentially never fire, whatever the agents
are doing.

The band is `BodyRadiusRaw + (MovementSpeedRaw / 2)` per body, so a pair counts
as in contact when the two bodies are within one movement step of touching. At
the default values that is `5632` raw units per body, pairing bodies whose
centres are within `11264` raw units. The band is derived observability: no rule
consults it, the resolver's own legality tests still use the exact
`2 * BodyRadiusRaw` contact distance, and both hashes were confirmed
byte-identical before and after it was introduced.

**Front width and depth are measured over agents holding an enemy in reach, not
over agents in body contact.** Width and depth are named for the default
left-versus-right deployment. They are a readability signal only, and no rule
depends on them.

No penetration percentiles are reported. Under the solid contact policy,
penetration between two living bodies is identically zero at the end of every
tick, so a p50 or p95 histogram would be a column of zeros carrying no
information.

### What the collision numbers actually show

Opposing bodies meet. `contactPairs` is 5,649 at 200 agents against 57,295
candidate pairs, and 14,270 at 500 agents against 280,675. An advancing agent
closes until its body meets its target's body, so the two front ranks press
together instead of halting with air in front of them. The earlier zero was the
product of two separate problems, both now fixed: agents stopped at
twelve-world-unit attack reach while a body is only eight world units across, and
the counter itself asked for exact tangency.

Allies also still queue behind their own front line. A rear agent trying to
advance into space its own front rank already occupies is refused, holds position,
and reports `Blocked`. That shows up as 14,544 blocked agent-ticks at 200 agents
and 48,573 at 500 agents, against 8,945 and 22,848 attack-capable agent-ticks
respectively. Crowding roughly doubled at 200 agents once the front closed all the
way, which is the expected consequence rather than a regression: being blocked
does not remove an agent from combat, which is exactly why no separate anti-stall
rule was added.

`maximumPenetrationRaw` is `0` on both workloads. It was also `0` before the
amendment. Where agents choose to stop does not affect the solid-disc invariant,
and any nonzero value in this field would be a contract violation rather than a
tuning signal.

Anyone tuning contact behaviour later should start from the fact that the binding
constraint on the battle line is now the body diameter, while attack reach decides
who can strike. The two are deliberately different distances, and the four world
units between them are what let a second rank strike past a pressed first rank.

### Scope of these results

These results prove the non-interactive gate only. **The interactive
`./scripts/run.ps1` spectator check for this change has not been performed.**
Every row in the interactive smoke checklist below is therefore left `PENDING`.
Automated tests, a clean gate, a benchmark, and a zero-warning build do not
substitute for that check and do not entitle anyone to flip a row to `PASS`.

The amendment makes that outstanding check matter more, not less. It changes what
a spectator sees: front ranks now press their bodies together instead of stopping
four world units apart, roughly twice as many agents are held up behind their own
line, and `AgentIntent.Attacking` now appears only once an agent has arrived at
contact. None of that has been observed in a live window by a person. Nothing in
the automated evidence above speaks to whether the resulting battle line is
legible, and no row may be flipped on the strength of it.

### Superseded records below this line

Everything from here to the interactive smoke checklist is kept for traceability
and is **not current**. All of it predates the contact-closing amendment. Where
one of those entries says a hash is "unchanged from the values recorded above", it
means unchanged relative to the values that were current when it was written, all
of which are now superseded by the tables at the top of this section. Do not read
any hash, tick count, test count, or allocation figure below as a live baseline.

### The sound-variant run

Superseded, and kept for traceability. This run verified the hit-location sound
variant matrix, which lives entirely in `Hukbo.Client` and touches no Core code.
`./scripts/verify.ps1 -SkipBootstrap` passed every stage:

- 505/505 Client tests passed;
- 156/156 Core tests passed;
- formatting verification and the Release build passed with 0 warnings and
  0 errors;
- the seed-1 200-agent workload ended in `Faction1Victory` at tick 235 with
  state hash `6EBB1EA63114F6CE` and event hash `941377BD43C556FF`, reporting
  `deterministic: true` and `firstMismatchTick: null`;
- that workload allocated 15,122,504 bytes.

Those two hashes were unchanged relative to the baseline that was current when
this run was recorded, which was the correct expectation for a Client-only
change. **Both are now dead values**, superseded first by the pre-amendment
collision baseline and then by the amended baseline at the top of this file. The
tick-235 figure belongs to a build in which agents halted at weapon reach and is
not comparable to the current terminal tick.

Interactive variant playback remains unverified. Compiling the Client and listing
the files on disk does not establish that a single sound was ever heard.

### Retained evidence from the earlier spectator-clarity work

Kept so it is not lost when the section above is next replaced. These
observations belong to the earlier spectator-clarity package run, not to the
collision change:

- the package run produced
  `artifacts/packages/client-win-x64/Hukbo.Client.exe`;
- that packaged Client opened visibly, remained responsive, showed
  `Hukbo — A 0 : 0 B — Seed 1 — Tick 0 — 1x — Paused — Ongoing`, and returned
  exit code 0 after a normal window-close request;
- the spectator-clarity independent review reported no Critical, High, Medium, or
  Low findings.

None of that was re-observed after the collision change.

### 2026-07-27 plains-backdrop gate run

A second local run on 2026-07-27, recorded after the plains battlefield
backdrop change, showed:

- `./scripts/format.ps1 -Verify` passed with 0 warnings and 0 errors;
- `./scripts/verify.ps1 -SkipBootstrap` passed all five stages;
- 141/141 Core tests passed;
- 223/223 Client tests passed, up from the 189 recorded above because of the 34
  new plains backdrop geometry test cases across 14 test methods;
- the seed-1, 200-agent, 10,000-tick headless workload ended in
  `Faction1Victory` at tick 235 with state hash `6EBB1EA63114F6CE` and event
  hash `941377BD43C556FF`, and the run reported `deterministic: true`;
- the same workload allocated 15,122,504 bytes, slightly below the previously
  recorded 15,128,696-byte baseline.

Both the state hash and the event hash are unchanged from the values recorded
above. That is the expected result for a presentation-only change: the plains
backdrop touches only `Hukbo.Client` rendering, `Hukbo.Core` was not modified,
and neither hash moving confirms the backdrop did not leak into the
deterministic simulation.

### 2026-07-27 plains-backdrop review-fix partial re-run

Code review of the change above produced two high-severity findings, both fixed:
a duplicated ground-cell formula that left the shipped render loop uncovered
while the tests constrained a method with no production caller, and incorrect
test counts in the entry above. Four medium findings were also fixed: decal
shades are now bounded by a named ceiling so the high-contrast theme does not
receive mid-grey speckle on pure black, decals are clipped to the map rectangle
so they cannot bleed past the arena border, the shade-count and decal-kind
couplings are now asserted by tests, and the renderer's positional parameter
lists are grouped into a `PlainsBackdropFrame` value.

The canonical gate could **not** be re-run in full after these fixes, and this
is recorded as a limitation rather than a pass. At the time of the re-run the
working tree also carried in-flight, unrelated work for a sound system, a
blood-and-gore layer, and army-composition settings, and several of those
untracked test files did not compile:

```
SoundCueMapperTests.cs(14,17): error CS0051: Inconsistent accessibility:
parameter type 'GameSoundId' is less accessible than method
'SoundCueMapperTests.Map_ReturnsTheWeaponSlotForAnAttack(WeaponId, GameSoundId)'
```

That failure belongs to the sound workstream, not to the backdrop. What was
verified after the review fixes:

- `./scripts/format.ps1 -Verify` passed, 0 of 148 files reformatted;
- the `Hukbo.Client` Release build succeeded with 0 warnings and 0 errors;
- all 42 plains backdrop test cases passed;
- 284/284 Client tests passed with the five non-compiling sound test files
  temporarily set aside and then restored;
- 145/145 Core tests passed.

The Core and Client totals above are higher than the 141 and 223 recorded for
the earlier run because the concurrent sound and gore workstreams have added
their own tests. Those totals are therefore not attributable to the backdrop
change alone and should not be cited as its baseline.

The headless determinism stage was not re-run after the review fixes. Every fix
is confined to `Hukbo.Client` presentation code, so no hash movement is
possible, but that remains an argument rather than recorded evidence. The full
`./scripts/verify.ps1` must be re-run once the sound workstream's test files
compile, and its output recorded here before this change is integrated.

### 2026-07-27 sound-system gate run

`./scripts/verify.ps1 -SkipBootstrap` on 2026-07-27, after the sound system
change, ended with `[PASS] Canonical repository verification completed` and
showed:

- `./scripts/format.ps1 -Verify` passed: `Formatted 0 of 150 files`;
- the Release build produced 0 warnings and 0 errors;
- 156/156 Core tests passed;
- 373/373 Client tests passed, including the 8 new sound suites — catalog,
  library, mapper, budget, cue log, director, cue formatter, and panel layout —
  plus the right-column split;
- the seed-1, 200-agent, 10,000-tick headless workload reported state hash
  `6EBB1EA63114F6CE`, event hash `941377BD43C556FF`, and
  `deterministic: true`.

Both hashes are unchanged from the values recorded above. That is the expected
result for a presentation-only change: the audio path lives entirely in
`Hukbo.Client`, reads the existing `BattleEvent` stream, and adds no Core type,
no Core file, and no simulation state.

An earlier attempt at this gate on the same day failed in the Core test stage,
and then failed to compile `Hukbo.Core` at all, because the working tree
simultaneously held an unfinished army-composition change to `Hukbo.Core`. That
failure was in Core, not in the sound system, and it cleared once the Core change
compiled again. Neither hash moved across either attempt.

### 2026-07-27 blood-and-gore gate run

`./scripts/verify.ps1 -SkipBootstrap` was run at the repository root on
2026-07-27 after the blood-and-gore feature was completed. It ended with
`[PASS] Canonical repository verification completed.` and printed:

```
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
[PASS] Release repository tests completed.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.

Test Run Successful.
Total tests: 429
     Passed: 429
 Total time: 0.5805 Seconds
```

The headless determinism workload emitted this `RunReport`:

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
  "measuredTicks": 235,
  "durationMilliseconds": 28.14780000000001,
  "tickPercentiles": {
    "p50Milliseconds": 0.0856,
    "p95Milliseconds": 0.1655,
    "p99Milliseconds": 0.2715,
    "maximumMilliseconds": 2.9543
  },
  "allocatedBytes": 15122504,
  "outcome": "Faction1Victory",
  "faction0Survivors": 0,
  "faction1Survivors": 30,
  "eventHash": "941377BD43C556FF",
  "stateHash": "6EBB1EA63114F6CE",
  "deterministic": true,
  "firstMismatchTick": null
}
```

Both the state hash (`6EBB1EA63114F6CE`) and the event hash
(`941377BD43C556FF`) are unchanged from the values recorded above, the run
reported `deterministic: true` with no first mismatch tick, and the outcome is
still `Faction1Victory` at tick 235 with 0 and 30 survivors. That is the
expected result for a presentation-only change: the blood layer lives entirely
in `Hukbo.Client`, reads the existing `BattleEvent` stream, and adds no
`Hukbo.Core` type, file, or simulation state. Neither hash moving is what
confirms `Hukbo.Core` was not modified.

Allocation for the same workload was 15,122,504 bytes, matching the figure
recorded for the plains-backdrop run above.

The reported test-run summary was `Total tests: 429` with all 429 passing. That
figure covers the whole repository test run at the time of this gate, and the
working tree also carried tests belonging to concurrent workstreams, so it is
not attributable to the blood-and-gore feature alone and should not be cited as
its baseline.

These results prove the non-interactive gate only. The blood-and-gore smoke rows
below remain `PENDING` a human at an interactive Windows desktop.

## The camera auto-pan run — 2026-07-27

Superseded by the mirrored starting-formation change at the top of this file.
The gate result and the Client test count below still stand; the two hashes it
quotes do not, because deployment positions moved after this run. Its point —
that a Client-only change must not move a hash — was correct when written. This change adds `ArenaAutoPan` and
`ArenaAutoPanController` to `Hukbo.Client`, plus a `Center` property, a
`MoveCenterTo` method, a `GetVisibleHalfExtents` helper, and an `Update` return
value on `SpectatorCamera`. It touches no `Hukbo.Core` file.

`./scripts/verify.ps1` passed at all five stages: prerequisites and locked
restore, format verification, the Release solution build, the Release repository
tests, and the seed-1 / 200-agent / 10,000-tick headless determinism workload.

| Suite | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| `Hukbo.Core.Tests` | 326 | 0 | 0 |
| `Hukbo.Client.Tests` | 532 | 0 | 0 |

Core is unchanged from `main`'s 326. Client rises from `main`'s 513 by exactly
the 19 new `ArenaAutoPanTests` cases.

The gate's headless workload reported state hash `D78F0B527B7F938F` and event
hash `AC3BAAEC684854D5` at 657 measured ticks, `Faction1Victory`, 0 and 10
survivors, `deterministic: true`, `firstMismatchTick: null`, and 42,568,888
allocated bytes. Every one of those values is identical to the recorded 200-agent
acceptance oracle at the top of this file, which is the required outcome for a
Client-only change: a moved hash here would have meant the camera work had
reached simulation state.

These results prove the non-interactive gate only. **The interactive
`./scripts/run.ps1` spectator check for this change has not been performed.**
The five camera auto-pan rows in the checklist below are therefore left
`PENDING`. The unit tests prove that the controller picks the nearest melee,
engages only on an empty screen, settles inside the inner margin, and yields to
spectator input. None of them prove that the resulting camera motion reads as
helpful rather than as the view drifting on its own, which is the only thing
those rows are for.

## Interactive smoke checklist

Run `./scripts/run.ps1` on an interactive Windows desktop. This repository uses
local-only verification: there is no hosted-CI substitute for this direct
interaction pass. Compilation, automated tests, a window-opening probe, or
synthetic input do not make a manual row pass.

### Weapon identity and attributes smoke (preset V2)

**No interactive run was performed for this change.** Every row below is
`PENDING`. The automated tests prove the labels, the profiles, the resolver,
the reach floor, and the panel arithmetic; none of them prove that an axe reads
as an axe on screen, that a shield block is visible at battle scale, or that
the six-row composition panel fits the window.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| V2-1 | Watch the battle event feed for one exchange | Attack lines read `Kampilan — Great Blade`, `Wasay — War Axe`, `Kalis — Thrusting Blade (solo)`, `Kalis — Thrusting Blade (shielded)`, `Itak — Work Blade (solo)`, `Itak — Work Blade (shielded)`, with differing damage values | | PENDING |
| V2-2 | Watch the two-handed weapons in the feed | Neither Kampilan nor Wasay ever carries a `(solo)` or `(shielded)` suffix | | PENDING |
| V2-3 | Click a warrior, then a second of the same weapon and the other grip | The inspector shows the pair label, the evidence tier, the grip, and the three attribute values, and the two differ by one damage and one reach | | PENDING |
| V2-4 | Look at the battlefield at default zoom | Shield bearers are distinguishable from solo warriors of the same weapon without clicking either | | PENDING |
| V2-5 | Zoom out to the lowest detail tier | The shield block is still visible; the Wasay is still distinguishable from the Kampilan | | PENDING |
| V2-6 | Compare a Wasay warrior against a Kampilan warrior up close | The Wasay reads as a hafted axe with a distinct head, not as a narrow blade | | PENDING |
| V2-7 | Open the army composition panel | Six rows, each naming its weapon in pair form and its grip where the weapon appears twice; every row and both buttons are fully on screen | | PENDING |
| V2-8 | Use Distribute Evenly, then Apply, then Full Reset | The battle fields the chosen composition across all six categories | | PENDING |
| V2-9 | Launch with an existing pre-V2 settings file present | Settings reset to defaults without an error dialog or a crash; the composition is the six-category default | | PENDING |
| V2-10 | Listen during a Wasay attack | The war-axe sound plays; no slot is silent | | PENDING |

### Weapon clash smoke (preset V2)

**No interactive run was performed for this change.** Every row below is
`PENDING`. The automated tests prove the resolver, the table coverage, the
event packing, and the blood/label suppression; none of them prove that a
spectator watching the arena can actually tell the five resolutions apart.
Rows marked with a dagger (†) are the ones that decide something about the
design rather than merely confirm it — see design section 3.8 for the
recorded disposition if the void-versus-landed row returns `FAIL`.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| CL-1 | Watch the battle event feed for one exchange of each resolution | The five lines are distinguishable: a damage line for `Landed`, "stopped by the shield" for `ShieldBlocked`, "parried" for `Parried`, "turned aside" for `Deflected`, "stepped off the line" for `Evaded` | | PENDING |
| CL-2 | Watch a shield-blocked, parried, or deflected blow | No blood spray and no impact ring appear for any of the three | | PENDING |
| CL-3 | Watch the clash cross render | It appears for `ShieldBlocked`, `Parried`, and `Deflected`, and for neither `Landed` nor `Evaded` | | PENDING |
| CL-4 † | Distinguish a void from a shield block | An `Evaded` blow (no clash cross, follow-through swing) reads differently on screen from a `ShieldBlocked` blow (clash cross, recoil) without reading the event log | | PENDING |
| CL-5 † | Distinguish a void from a landed blow | An `Evaded` blow (follow-through swing, no blood, no impact ring) reads differently on screen from a `Landed` blow (stops on target, blood, impact ring) without reading the event log | | PENDING |
| CL-6 | Watch any warrior attack | Weapons visibly swing through an arc rather than sitting static during an attack | | PENDING |
| CL-7 | Watch one attack at 1x, then the same weapon at 4x | The swing reads as one countable action at 1x and does not smear into a blur at 4x | | PENDING |
| CL-8 | Compare a `Parried` or `Deflected` blow, a `Landed` blow, and an `Evaded` blow | The clashed blow visibly recoils, the landed blow stops on the target, and the void follows through past it | | PENDING |
| CL-9 | Zoom to high detail, then to low detail, during a swing | The swing arc trail is visible at high zoom and absent at low zoom | | PENDING |
| CL-10 | Pan the camera so a swinging weapon crosses the arena panel edge | A weapon tip may be visibly clipped at the panel edge while panning — this is the accepted cost of the pose-blind frustum cull, not a defect | | PENDING |
| CL-11 | Observe the merged pawn silhouette in motion, both a shield-bearing and a solo warrior | The silhouette under D7 (main's geometry constants plus the clash branch's swing pose applied on top) reads correctly: shield block and swing pose both present, axe head distinguishable from blade, no visual corruption | | PENDING |

### Spectator clarity smoke

Record the observed value in `Actual` and change `Status` only after performing
the interaction. Use `PASS`, `FAIL`, or `BLOCKED`; leave untouched rows
`PENDING`.

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-07-27 |
| Machine/platform | Microsoft Windows 10.0.26200 (Windows 11 Pro) x64 |
| Source commit | `8815a3c`; the later `d6818a8` is documentation-only and builds the identical binary |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

The rows below were observed by the repository owner at an interactive Windows
desktop and reported to the role 17 review, which transcribed them. Only rows
whose **whole** expected observation was exercised are marked `PASS`. Rows 2, 4,
5, and 15 were partly observed: the observed half is recorded in `Actual` and the
row stays `PENDING`, because a row is a single status and half a row is not a
pass. Each of those four names exactly what is still missing, so closing them is
a short follow-up rather than a repeat of the whole pass.

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 1. Launch the game | The window opens, agents render, and the match starts paused with tick unchanged. | Window opened; match started paused with the tick counter sitting still. | PASS |
| 2. Activate Play | The always-visible Play button advances ticks; Space provides the same toggle while the modal is closed. | Play advanced the ticks. The Space toggle was not exercised. | PENDING |
| 3. Activate Pause | The always-visible Pause button stops tick advancement and visibly indicates the paused state. | Pause stopped tick advancement and the paused state was visible on screen. | PASS |
| 4. Open Menu | The always-visible Menu button pauses the match and opens the modal; Escape toggles that same menu behavior. | The Menu button opened the modal. Escape as a toggle was not exercised. | PENDING |
| 5. Exercise modal commands | Modal Play resumes and closes; modal Pause remains open and paused; Escape closes without resuming; Exit Game, which is available only in the modal, requests one clean shutdown. | Exit Game quit the game cleanly. Modal Play, modal Pause, and Escape-closes-without-resuming were not exercised. | PENDING |
| 6. Select an agent | A primary click on a living agent pins the inspector with ID, faction, alive/dead state, health, intent, target, and position. | Not run | PENDING |
| 7. Move away and observe death | Moving the pointer away does not clear selection; if the selected agent dies, the inspector remains pinned and shows its final `DEAD` state. | Not run | PENDING |
| 8. Check observational behavior | Selecting or inspecting an agent does not alter tick progression or the deterministic battle result; an empty-arena click clears selection and UI clicks do not click through. | Not run | PENDING |
| 9. Exercise event-log scrolling | At 1x and 4x, events remain ordered without duplicates and retain at most 200 rows. The wheel scrolls only the log while the pointer is over it and does not zoom the arena; new events do not steal an upward scroll position; returning to the bottom reveals the newest events; over the arena, the wheel zooms. | Not run | PENDING |
| 10. Reach a terminal outcome | The match pauses and the summary winner, both survivor counts, terminal tick, simulated duration, and seed match the final status and visible arena state; the summary offers Next Round. | Not run | PENDING |
| 11. Check score timing and team mapping | Team A is Blue/faction 0 and Team B is Red/faction 1. Reaching a victory does not change the score immediately; choosing Next Round adds exactly one win to that completed round's winner. Starting the next round after a draw or while the current round is ongoing adds no win. | Not run | PENDING |
| 12. Exercise ordinary Next Round | `R`, modal Next Round, and summary Next Round each preserve the score, speed, and camera; clear selection, event history, scroll state, and summary; and leave the fresh round paused. | Not run | PENDING |
| 13. Check seed progression | Each Next Round changes the seed to a distinct deterministic value. After Full Reset, repeating the same Next Round sequence produces the same seed sequence. | Not run | PENDING |
| 14. Exercise Full Reset | After changing the score, speed, and camera, press `Shift+R`; both win totals become 0, seed returns to 1, speed returns to 1x, the camera fits the arena, disposable UI state clears, and the fresh round is paused. Change state again and confirm modal Full Reset has the same result. | Not run | PENDING |
| 15. Close the window | The operating-system close button exits the process once with exit code 0. | Closing the window exited the game. The exit code was not captured, so the `0` half of this row is unproven. | PENDING |
| 16. Check the plains backdrop ground | The battle floor shows varied ground shading with scattered grass, dirt, and stone marks rather than one flat color. | Not run | PENDING |
| 17. Check backdrop stability at zoom extremes | Zooming fully out and fully in keeps the ground pattern locked to the same patches of map; the pattern does not crawl or shimmer, and decals neither vanish into flicker nor balloon into large blobs. | Not run | PENDING |
| 18. Check backdrop continuity while panning | Panning the camera across the map shows no seam lines, gaps, or overlapping bright edges between ground cells. | Not run | PENDING |
| 19. Check readability over the backdrop | Pawn silhouettes, faction ground rings, selection marks, and hit effects all remain clearly readable against the new backdrop. | Not run | PENDING |
| 20. Cycle every theme against the backdrop | Each theme produces a backdrop in its own palette, with the arena border still distinguishable from the ground. | Not run | PENDING |
| 21. Check backdrop reseeding on Next Round and Full Reset | Pressing `R` for a new round changes the backdrop with the new seed; pressing `Shift+R` for a full reset returns the seed-1 backdrop identical to the first launch. | Not run | PENDING |
| 22. Confirm the sound log is hidden by default | On launch, no sound panel is visible and the battle event log occupies the full height of the right column exactly as before. | Not run | PENDING |
| 23. Toggle the sound log | The `Sounds` control-bar button and `F9` both open and close the sound panel; the button shows an active state while it is open; the right column splits with battle events above and the sound log below, and nothing else on screen moves. | Not run | PENDING |
| 24. Check the expected-file list with an empty audio folder | With no files in `Content/Audio/`, the panel lists all nine expected file names, each marked `MISSING`, shows `MISSING 9/9`, and the game stays silent without errors. | Not run | PENDING |
| 25. Add one sound file | Drop a PCM WAV named `death.wav` into `Content/Audio/`, relaunch, and confirm that slot reads `READY`, the counter drops to `MISSING 8/9`, and a death audibly plays with a `PLAYED` row in the cue log. | Not run | PENDING |
| 26. Check an unusable file | Replace `death.wav` with a non-PCM file of the same name, relaunch, and confirm the slot reads `FAILED` rather than `MISSING`, and the game still runs silently for that slot. | Not run | PENDING |
| 27. Exercise mute and rate limiting | With files present, the panel's `MUTE` toggle silences playback while still logging rows; during a busy tick the cue log shows collapsed `LIMITED xN` rows rather than one row per suppressed cue. | Not run | PENDING |
| 28. Exercise sound-log scrolling and isolation | The wheel scrolls only the panel under the pointer — sound log, battle log, or arena zoom — and clicks inside the sound panel do not click through to the arena or clear the agent selection. | Not run | PENDING |
| 29. Check sound-log reset behavior | `R` and `Shift+R` clear the cue log while leaving the expected-file list and its statuses unchanged. | Not run | PENDING |
| 30. Open the Army Composition panel | Menu opens and the Army Composition button (between Next Round and Full Reset) shows the currently saved units-per-team and category counts in four steppers. | Not run | PENDING |
| 31. Adjust a category count | Left and Right arrows on a stepper adjust its value; Shift+Left and Shift+Right adjust by 10 instead of 1. The Unassigned readout updates live. | Not run | PENDING |
| 32. Check Unassigned reaches zero | Adjusting steppers such that category sum equals units-per-team displays Unassigned: 0. | Not run | PENDING |
| 33. Verify Apply gate behavior | Apply is disabled (ActionDisabled style, dimmed glyph) while Unassigned != 0 and while the draft equals the saved composition; Apply is enabled exactly when balanced and changed. | Not run | PENDING |
| 34. Check the staged banner | After pressing Apply, the panel closes, the menu shows a one-line notice stating the composition takes effect on the next Full Reset, and Apply remains disabled until a different composition is drafted and applied. | Not run | PENDING |
| 35. Verify Full Reset fields the chosen army | After applying a composition and pressing Full Reset (or `Shift+R`), the arena resets and both factions field the number and distribution of warriors specified by the staged composition, visible in the agent inspector and event log. | Not run | PENDING |
| 36. Observe blood at the default fit view | On first launch, with the default gore setting (Stylized) and the default camera fit, a landed blow shows a directional spray and a ground mark that are both plainly visible without zooming the camera in at all. | Not run | PENDING |
| 37. Check spray direction | Select an agent, watch it get struck, and confirm the spray leaves the victim along the line running from the attacker to the victim — pointing away from the attacker, never back toward it. Confirm this holds for blows arriving from several different directions. | Not run | PENDING |
| 38. Distinguish a lethal blow from a wound | A blow that kills its victim renders visibly differently from a blow that only wounds: the lethal tier is denser or longer-lived, and only the lethal blow leaves the ground mark described in row 39. A spectator can tell the two apart without reading the event log. | Not run | PENDING |
| 39. Check ground-mark persistence and fade | A ground mark stays on the battlefield after the fighters involved have moved away, then fades out gradually over time rather than vanishing in a single frame. Marks accumulate where the fighting was heaviest instead of spreading evenly. | Not run | PENDING |
| 40. Confirm gore Off draws nothing | With the gore setting on Off, no spray, spurt, or ground mark appears anywhere for any blow, including kills, at any camera zoom. The existing warm-white hit-effect ring still draws, so impacts remain readable. | Not run | PENDING |
| 41. Change gore intensity via the menu | Open Menu; the Gore Intensity control cycles Off, Stylized, Full and wraps at both ends using Left and Right and the pointer arrows. Each choice visibly changes blow rendering: Off shows nothing, Stylized shows spray and a fading mark, and Full additionally shows a sustained spurt on a kill together with denser, longer-lived marks. The change takes effect immediately, without a restart. | Not run | PENDING |
| 42. Reach the gore selector by keyboard | Inside the menu, `Tab`, `Down`, and `S` move focus from the theme selector through every button and land on the Gore Intensity selector as the final control in the order; continuing past it wraps back to the theme selector. `Up` and `W` reach it from the theme selector by wrapping backwards. While it is focused, Left and Right change the value and no button is activated. | Not run | PENDING |
| 43. Reach the gore selector by pointer | Hovering the Gore Intensity selector highlights it without changing the value; clicking its previous and next arrows changes the value; and a click on the selector does not click through to the arena or activate any menu button. | Not run | PENDING |
| 44. Check gore intensity persists across a restart | Set gore to Full, fully close the game, and relaunch it: Full is active from the first blow, without reopening the menu. Repeat with Off and confirm the same. | Not run | PENDING |
| 45. Check blood clears on Next Round and Full Reset | With sprays and ground marks visible on screen, trigger Next Round (`R`, modal, or summary); all blood clears immediately alongside the event log, inspector, and summary. Repeat separately with Full Reset (`Shift+R` and the modal command) and confirm the same. | Not run | PENDING |
| 46. Check blood readability across every theme | Cycle all five visual themes while blood is on screen. In every theme, including `high-contrast`, blood stays clearly distinguishable from the Blue faction pawns, from the Red faction pawns, and from the arena ground surface; no theme makes a spray or a ground mark disappear into a pawn or the backdrop. | Not run | PENDING |
| 47. Check speed and gore independence | At 1x, 2x, and 4x speed, switch gore between Off and Full and confirm the tick counter in the window title advances at the same visible rate for both settings at each speed. The gore setting never slows, pauses, or reorders simulation advancement. | Not run | PENDING |
| 48. Confirm variants resolve | Press `F9`. Every attack slot reports `READY` with a per-class breakdown, and the counts match the files in `Content/Audio/`: 10 for each of the four attack slots, 10 for `death`. A class with no take of its own shows its real count rather than a fallback-inflated one. | Not run | PENDING |
| 49. Hear the variation | Watch an unpaused battle for a full minute. Blows do not sound like one repeating sample: cuts to different parts of the body are audibly different, and the same weapon striking the same class does not always play the identical take. | Not run | PENDING |
| 50. Confirm no human voice | Listen through a full battle including many deaths. No cue contains a scream, grunt, groan, or breath. Pay particular attention to `death-02`, `death-06`, and `death-07`, whose prompt wording carries the highest risk of an accidental vocalisation. Any file that vocalises must be regenerated before release. | Not run | PENDING |
| 51. Check level consistency | No cue is obviously louder or quieter than its neighbours. The known-quiet takes — `attack-kampilan-ribcage-01`, `attack-kampilan-gut-01`, `attack-wasay-neck-01`, `death-02` — are audible under a busy battle rather than disappearing. Any that vanish need a re-roll. | Not run | PENDING |
| 52. Verify a partial set falls back | Move one hit class's takes for a single weapon out of `Content/Audio/` and relaunch. That weapon still makes a sound on a hit to that body part, drawn from the fallback class, and the sound log shows the class as missing rather than the whole slot going silent. | Not run | PENDING |

For round scoring, record Team A (Blue) and Team B (Red) totals before and after
each command together with the outgoing outcome and old/new seeds. Next Round
scores only a terminal victory and always advances the deterministic seed.
Full Reset never scores the outgoing round.

### Collision readability smoke

Added by the collision change and revised by the contact-closing amendment.
**Not performed.** Observe one collision-heavy engagement in a live window and
record what was actually seen. The automated gate, the benchmarks, and the
collision regression tests above prove the rule is enforced; none of them prove
the resulting battle line is legible to a person watching it, which is the only
thing these rows are for. The amendment changed what a spectator should expect to
see here, so these rows carry more weight than they did before and none of them
has been observed.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 16. Read the battle line | Agents form a visible front instead of a shapeless blob, and the shape reads as a consequence of crowding rather than as a snapped grid. | Not run | PENDING |
| 17. Look for stacking and jitter | No two living pawns visually occupy the same spot, and a pressed front settles instead of vibrating between positions tick after tick. | Not run | PENDING |
| 18. Confirm combat continues | A packed front keeps dealing damage; the match does not stall into a standoff and reaches a terminal outcome inside its tick limit. | Not run | PENDING |
| 19. Inspect a blocked agent | Selecting an agent in the second rank shows a movement label explaining why it is not advancing, and that label changes as the situation changes. | Not run | PENDING |
| 20. Inspect the front rank | Selecting a front-rank agent shows it moving or attacking rather than blocked, and an agent that has arrived at an enemy reads as attacking rather than still marching. | Not run | PENDING |
| 21. Confirm the ranks actually touch | Opposing front ranks close until their pawn bodies meet, rather than settling with a visible gap of open ground between the two lines. This is the amendment's whole visible effect and the pre-amendment behaviour was a persistent gap. | Not run | PENDING |
| 21a. Watch a contested push change hands | Added by the collision priority amendment. Select a second-rank agent pressed against the same enemy for a sustained engagement. Its movement label alternates between blocked and moving across ticks rather than reading blocked for the whole engagement, and neither faction's line is the one that always gives way. | Not run | PENDING |

### Camera auto-pan smoke

Added by the camera auto-pan change. **Not performed.** The unit tests prove the
targeting and state-machine decisions; only a person watching a live window can
say whether the resulting camera motion is helpful rather than distracting.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 53. Confirm the camera holds still during a visible fight | Zoom in on an engagement so fighting fills the screen. The camera stays exactly where it was left for the whole engagement; it never creeps, drifts, or re-centres on its own while anyone on screen is fighting. | Not run | PENDING |
| 54. Watch the camera find a fight it lost | Zoom in, then pan away until no fighting is on screen. Within a moment the camera slides on its own toward the nearest melee, slows as it arrives, and stops with the fighting comfortably inside the view rather than pinned to an edge. | Not run | PENDING |
| 55. Confirm zoom never changes | Through several auto-pans, the zoom level is exactly what the spectator set. The camera only slides; it never zooms out to find the fight or zooms in on arrival. | Not run | PENDING |
| 56. Take control back | While the camera is auto-panning, hold a pan key. Motion stops under the spectator's hand immediately, the camera goes exactly where they steer it, and it does not resume on its own for a couple of seconds after the key is released. | Not run | PENDING |
| 57. Watch the end of a long battle | Let a match run to its final few survivors at a zoom where they leave the screen. The camera follows the fighting to the end instead of leaving the spectator on empty ground, and it stands still once the match summary appears. | Not run | PENDING |

### Starting deployment smoke

Added by the mirrored starting-formation change. **Not performed.** The
automated evidence proves the arrangement is symmetric, separated and
overlap-free in numbers; none of it proves the opening frame reads that way to a
person watching it, which is the only thing these rows are for.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 58. Read the opening frame | Before the armies move, each side reads as several separate groups of warriors rather than one undifferentiated cloud, at the default camera fit and without zooming in. | Not run | PENDING |
| 59. Check the mirror | Pausing at tick 0 and comparing the two halves shows each side as the other's reflection across the centre line: same group positions, same group sizes, same ragged front. | Not run | PENDING |
| 60. Confirm the groups look irregular | Within a group the spacing looks uneven rather than a snapped parade grid, and a new seed visibly reshuffles that spacing without moving the groups. | Not run | PENDING |
| 61. Confirm the armies still meet promptly | The two sides close and fight without a long empty march, and the battle reaches a terminal outcome inside its tick limit. | Not run | PENDING |

### Typography smoke

Added by the font and text quality change. **Not performed.** The automated
gate proves the ramp is internally consistent, the theme catalog resolves
every role, and text positions round to whole pixels; none of that proves the
resulting text reads as crisp, correctly sized, or correctly hierarchical to a
person watching it, which is the only thing these rows are for.

**Correction — there is no automated em-dash check.** An earlier revision of
this section claimed a "compiled em-dash byte assertion passes". No such
assertion exists. Searching `tests/` for `.xnb`, `CharacterMap`, `2014`,
`8212`, or `em-dash` returns nothing. The only thing backing the em dash is the
second `CharacterRegion` in each of the six `.spritefont` files under
`src/Hukbo.Client/Content/Fonts/`, which spans `&#8211;` to `&#8212;` and so
asks the content builder to include the glyph. Whether the builder actually
produced it, and whether the running game draws it instead of throwing, is
verified by row 71 below and by nothing else. That row is `PENDING`.

Per `CLAUDE.md` section 6, only a human at an interactive Windows desktop may
flip one of these rows to `PASS`. Compilation, unit tests, and a
window-opening probe do not.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 62. Glyph crispness at the smallest rung | Event log and sound log rows have solid stems and clean edges, with no grey mush and no ragged stair-stepping. | Not run | PENDING |
| 63. Glyph crispness at the largest rung | The wordmark is sharp at every edge with no fringing. | Not run | PENDING |
| 64. Wordmark hierarchy | The wordmark is unmistakably larger and heavier than the subtitle beneath it. | Not run | PENDING |
| 65. Header face renders as capitals | Every panel header renders fully and unclipped inside its header strip. | Not run | PENDING |
| 66. Mixed-case strings stay on the body face | Theme names, gore levels, the controls label, the winner line, the distribute action, and every inspector line render with real lowercase letters. | Not run | PENDING |
| 67. No vertical clipping | No descender is cut off in any panel at any rung. | Not run | PENDING |
| 68. No horizontal overflow | No label spills past its panel, button, chip, or column, and no ellipsis appears where text previously fit. | Not run | PENDING |
| 69. Row alignment | Event log columns, sound log rows, and inspector rows sit on consistent baselines with no drift down the list. | Not run | PENDING |
| 70. Agent inspector evidence note | The longest evidence note wraps fully inside the panel with nothing cut off. | Not run | PENDING |
| 71. Em-dash regression | Staging an army composition change renders the notice with a real em dash and does not crash. | Not run | PENDING |
| 72. Theme cycling | All five themes render text at the same sizes with correct contrast, and no theme reveals a clipped or misaligned label the others hide. | Not run | PENDING |
| 73. Window resize | Resizing between small and maximised keeps text pixel size constant and re-lays out panels without clipping. | Not run | PENDING |
| 74. Subpixel blur is gone | Panning, zooming, and pausing produce no shimmering or swimming text. | Not run | PENDING |
| 75. Display scaling | Record the appearance at 100% and at 150% Windows scaling. Feeds the separate, gated display-scaling measurement task; not itself a pass/fail row for the font ramp. | Not run | PENDING |

### Last-stand formation smoke

Added by the last-stand formation change. **Not performed.** The automated
tests prove the trigger, the rally-agent choice, the deterministic offset, the
trail distance, the give-way rule, and that a last stand still resolves inside
the tick limit. None of them prove that the resulting endgame reads as a
converging last stand rather than as warriors wandering, which is the only
thing these rows are for. Only a human running `./scripts/run.ps1` on an
interactive Windows desktop may flip one of these rows to `PASS`. Compilation,
unit tests, and a window-opening probe do not.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 76. Watch the endgame converge | Let a full 200-agent battle run to its final handful of warriors on each side. As each side thins out, its survivors visibly turn toward one another and gather instead of continuing to spread across the map. | Not run | PENDING |
| 77. Confirm the cluster is irregular | The gathered survivors form a ragged clump. They do not form a ring, a grid, a line, an arc, or any shape that looks placed. No warrior sits at an obviously exact distance from the one it gathered on. | Not run | PENDING |
| 78. Confirm the cluster advances as a body | The gathered survivors travel toward the enemy together rather than one at a time. The group arrives roughly at once, and the fight that follows is a group fight rather than a sequence of separate duels. | Not run | PENDING |
| 79. Watch a leader fall | When the warrior the group has gathered on is killed, the group re-forms on another warrior within a moment. The re-form is a short, small adjustment, not a sudden jump across the screen or a scatter. | Not run | PENDING |
| 80. Inspect a regrouping warrior | Selecting a survivor that is closing on its comrades shows `Intent: Regrouping` in the inspector, and the battle event log shows its movement naming the warrior it is closing on rather than an enemy. The intent changes to `Attacking` once it is actually swinging at an enemy. | Not run | PENDING |
| 81. Confirm regrouping never stops the fight | A warrior that is regrouping still strikes any enemy it passes within reach. The final engagement is not delayed by warriors refusing to fight while they are still gathering, and the match reaches a terminal outcome rather than two clusters standing apart. | Not run | PENDING |

### Sound gain compensation smoke

Covers the change recorded in
`docs/plans/2026-07-27-sound-gain-compensation.md`. The measured evidence is in
`docs/research/SOUND-CAPACITY-MEASUREMENTS.md`; these rows are the part that
only a person with working speakers can settle.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 82. Hear a busy melee without distortion | Let a 200-agent battle reach its densest fighting at normal speed. Blows stay individually distinguishable. There is no continuous rasp, crackle, or buzz underneath the fighting, and no moment where the sound seems to break up or drop out. | Not run | PENDING |
| 83. Compare a duel with a melee | The final one-on-one survivors sound clearly louder per blow than the same weapon does in the middle of the melee. The change is gradual as the fight thins out, not a sudden jump. | Not run | PENDING |
| 84. Watch the voice count and gain react | Open the sound log with `F9`. During heavy fighting `VOICES` climbs into the tens and `GAIN` falls well below 0.65; as the battle thins both recover, and `GAIN` returns to `0.65` once nothing is sounding. | Not run | PENDING |
| 85. Confirm nothing is being limited | Through a full 200-agent battle at normal speed, the sound log shows no `LIMITED` row and no `REFUSED` row. | Not run | PENDING |
| 86. Check 4x speed | At 4x the audio stays clean and undistorted, `VOICES` climbs higher than at 1x, and `GAIN` falls further. Still no `LIMITED` or `REFUSED` rows. | Not run | PENDING |
| 87. Confirm mute still works | Toggling `MUTE` silences everything immediately and unmuting resumes without a burst of backed-up sound. | Not run | PENDING |
| 88. Confirm a new round starts at full gain | After a match ends and a new one starts, the first blow of the new battle is at full volume rather than carrying the previous battle's reduction. | Not run | PENDING |
| 89. Confirm the header stays readable | The `VOICES n GAIN 0.nn` text in the sound log header does not overflow its panel, overlap the `MUTE` button, or clip at any of the five themes. | Not run | PENDING |

### Tactical hit animations smoke

Covers the change recorded in
`docs/plans/2026-07-26-tactical-hit-animations.md`, whose Task 6 requires a
manual checklist that this document was previously missing. **Not performed.**
`HitEffectSystemTests.cs` and `HitEffectGeometryTests.cs` prove that the effect
buffer has a fixed capacity and replaces its oldest entry in a defined order,
that ordinary and lethal effects expire on their stated schedules, that each
damage event produces exactly one effect, and that a reset clears every effect.
The system lives entirely in `Hukbo.Client`, so it cannot reach the simulation
by construction; no test asserts that a battle's tick count, outcome, state
hash, or event hash is unchanged, and row 98 below is the only check of that.
Nothing automated proves that a hit reads as a hit to a person watching the
screen, or that the effects stay legible when the fighting gets crowded, which
is the only thing these rows are for. Only a human running
`./scripts/run.ps1` on an interactive Windows desktop may flip one of these rows
to `PASS`. Compilation, unit tests, and a window-opening probe do not.

| Evidence field | Recorded value |
| --- | --- |
| Date | Not recorded |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 90. Read an ordinary hit at 1x | At normal speed a non-lethal blow produces a brief pulse on the struck pawn, one thin ring, and a small restrained shard burst. The blow is unmistakable without the screen filling with debris. | Not run | PENDING |
| 91. Check hits survive 4x | At 4x, hits landed on consecutive simulation ticks are each still visible, rather than only the last tick's hit appearing in each drawn frame. | Not run | PENDING |
| 92. Tell a lethal hit apart | A killing blow reads as clearly heavier than an ordinary one: a larger double ring and longer shards, appearing after the pawn has disappeared rather than on top of it. | Not run | PENDING |
| 93. Check readability across the zoom range | At fitted, minimum, and maximum zoom the primary ring stays readable. Zooming out reduces clutter without removing the ring, so a hit is never invisible at any zoom the spectator can reach. | Not run | PENDING |
| 94. Watch a crowded exchange | With many pawns trading blows at once the effects stay bounded. No persistent trail, smear, or lingering colour builds up on the arena, and the fighting stays legible underneath. | Not run | PENDING |
| 95. Pause and resume | Pausing lets effects already on screen finish while the simulation stops advancing. Resuming produces new effects normally, with no burst of stored-up effects on the first frame. | Not run | PENDING |
| 96. Reset clears everything | Next Round (`R`) and Full Reset (`Shift+R`) both clear every pulse and burst immediately. No effect from the previous match survives into the new one. | Not run | PENDING |
| 97. Check the arena edges | Resize the window and zoom in near each arena edge. No ring or shard draws over the status bar, the agent inspector, the event log, the match summary, or the menu overlay. | Not run | PENDING |
| 98. Confirm the effects change nothing | Run to a terminal result. Effects expire on their own, and the outcome, tick count, state hash, and event hash match a run of the same seed with the effects never observed. | Not run | PENDING |

## Failure classification

Classify failures as implementation, test, environment/dependency, pre-existing,
incorrect assumption, unrelated, or flaky. Make the narrowest correction, rerun
the focused check, and expand only after it passes.
