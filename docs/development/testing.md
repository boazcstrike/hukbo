# Testing and Verification

How this repository is verified, what the current recorded results are, and
where the rest of the record lives.

Verification here is **local-only and deliberate**. There is no CI and none is
wanted: never propose a GitHub Actions workflow, and never claim a change is
verified without the actual output of the command that verified it. A build
that compiles is not a passing test run, and a test run is not a manual smoke
check.

| Document | What it holds |
| --- | --- |
| This file | The canonical gate, the focused commands, how to capture a debug log, the current gate results, and the recorded baselines for both games |
| [smoke-checklist.md](smoke-checklist.md) | Every interactive row for both games, and the only place a manual `PASS` may be recorded |
| [measurement-history.md](measurement-history.md) | Dated records of runs that have since been superseded, kept verbatim and still citable |

Split into three on 2026-08-11. This file had reached 5,708 lines, 62 per cent
of it superseded measurement records, which put the live smoke checklist 4,082
lines in. Nothing was deleted or rewritten in the split — the two blocks were
cut whole and moved.

**`./scripts/verify.ps1` with no flag runs Hukbo only.** A green default gate is
no evidence at all about Sandata; use `-Game Sandata` and report the two
results separately.

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

## Canonical gate result — Hukbo, 2026-08-09

`./scripts/verify.ps1` with no flags, all five stages, exit code 0, at
`9e28a65`:

```
[PASS] Platform: Windows x64
[PASS] PowerShell: 7.6.4
[PASS] .NET SDK: 10.0.302
[PASS] packages.lock.json present for all 973 projects.
[PASS] MonoGame packages are centrally pinned: 3.8.5
[PASS] Required prerequisites and repository configuration are present.
[PASS] Locked package restore completed.
[PASS] Formatting verification completed.
Build succeeded.  0 Warning(s)  0 Error(s)
[PASS] Release solution build completed.
Hukbo.Core.Tests     Total tests: 2376   Passed: 2376   Total time: 29.4877 Seconds
Hukbo.Client.Tests   Total tests: 3270   Passed: 3270   Total time:  2.0697 Seconds
[PASS] Release repository tests completed.
measuredTicks 981   outcome Faction1Victory   survivors 0 / 6
stateHash 1B73FC5923879AA0   eventHash AC55684F24D39344   deterministic true
p50 0.1297 ms   p95 0.9696 ms   p99 1.3251 ms   max 15.3551 ms
coreAllocatedBytes 154976   allocatedBytes 480936
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

Both recorded seed-1 baseline hashes are unchanged. This run was made after the
attack-animation V2 work merged, which is where the Client suite's growth from
3,152 to 3,270 tests comes from; it is not a Sandata change and none of the
documentation edits made on this day altered test discovery in either suite,
which was confirmed by listing discovered tests with and without them.

## Canonical gate result — Hukbo, 2026-08-11 (armor, accent, and trample legibility)

**This run supersedes nothing.** All three pairs are byte-identical to the
combat cadence V6 block below, which remains the live baseline. That is the
whole point of recording it: the change it covers is presentation-only —
`PawnRenderer.DrawArmor`, `PawnGeometry`'s accent sizing, and the grass and
trample shading — so a moved hash would have meant a renderer had reached into
the simulation.

`./scripts/verify.ps1 -SkipBootstrap`, exit code 0, on `main`:

```
Formatted 0 of 739 files.
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
Hukbo.Core.Tests     Total tests: 2492   Passed: 2492
Hukbo.Client.Tests   Total tests: 3682   Passed: 3682
[PASS] Release repository tests completed.
measuredTicks 885    outcome Faction0Victory
stateHash 5460D13E3F7FD3E5   eventHash 8E18ED1437B2924B   combatPreset 6   movementPreset 4
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
measuredTicks 1764   outcome Faction1Victory
stateHash C8023D3B5BEB005E   eventHash F709A345E2F7370E   combatPreset 5   movementPreset 8
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
measuredTicks 1888   outcome Faction0Victory
stateHash 7C145A9E05916E4C   eventHash 77626E104234206C   combatPreset 5   movementPreset 10
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

`deterministic true` on all three.

**Read the Client test count with care.** It is 3,682 against the 3,651 the
combat cadence V6 block records. Twenty-three of those thirty-one new tests
belong to this change — the armor flank-bar helper, the scale-relative accent
cap, and the trample stubble shade. **The other eight come from the separate
display-DPI change another session had in the same working tree when this gate
ran**, uncommitted at the time and landed as `b1152f7` shortly afterwards. They
are not part of the commit this block documents. The suite stood at 3,659 with
that change present and this one absent, which is where both figures come from.
The hashes are unaffected either way: both changes are `Hukbo.Client` only, and
the headless workloads never load the client.

**A green gate proves nothing about smoke rows 128, 129, and 131.** Those three
are the reason this change exists and every one of them needs a person at an
interactive desktop. See `docs/development/smoke-checklist.md`.

## Canonical gate result — Hukbo, 2026-08-13 (shield-clash audio legibility)

**This is the live Hukbo baseline.** It supersedes the last-stand engagement
block below without changing a single recorded value. All four workloads are
byte-identical to that block, which is the entire point: the shield-clash audio
work is presentation-only, its plan required that no hash move, and none did.

`./scripts/verify.ps1`, exit code 0, on branch `sc-refs` at `910e309`, which is
`main` at `c15ca63` plus the two commits of this change:

```
[PASS] .NET SDK: 10.0.302
[PASS] packages.lock.json present for all 21 projects.
[PASS] Required prerequisites and repository configuration are present.
[PASS] Locked package restore completed.
Formatted 0 of 759 files.
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
Hukbo.Core.Tests     Total tests: 2503   Passed: 2503
Hukbo.Client.Tests   Total tests: 3783   Passed: 3783
[PASS] Release repository tests completed.
stateHash 5460D13E3F7FD3E5   eventHash 8E18ED1437B2924B   combatPreset 6   movementPreset 4
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
stateHash C8023D3B5BEB005E   eventHash F709A345E2F7370E   combatPreset 5   movementPreset 8
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
stateHash 7C145A9E05916E4C   eventHash 77626E104234206C   combatPreset 5   movementPreset 10
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
stateHash 6225182B4A470F91   eventHash C4DABE6AF98B6BEC   combatPreset 5   movementPreset 11
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

Workload 4 ran 200 agents to a decision at tick 2,037 with `Faction0Victory`
and 18 survivors, `deterministic true` and `firstMismatchTick null`, exactly as
the block below records it.

**What this run does and does not prove.** It proves that adding per-take
sample-domain normalisation, a four-slot voicing table, and their tests left
every simulation hash where it was, and that the two suites are green with
nothing suppressed and nothing weakened. It proves nothing at all about whether
a person can now tell the four melee clash slots apart by ear — the gate never
opens an audio device. That question belongs to smoke rows `SCL-1` and `SCL-2`,
which are `PENDING` in `docs/development/smoke-checklist.md` and which only a
person at an interactive desktop may close.

The Client suite is 3,783 tests here against 3,682 in the block below. That
difference is not all this change: it includes work merged by other sessions on
the same day. This change contributed the twenty `WavePeak` tests, the ten
`SoundVoicing` tests, the director's pitch-passthrough test, and two tests added
on 2026-08-13 to close acceptance clauses the original work had left untested —
`LoudestClashCue_PlaysQuieterThanTheFullScaleTakeDidBefore` and
`IsMeleeShieldClash_IsTrueForExactlyFourOfTheWholeCatalog`.

## Canonical gate result — Hukbo, 2026-08-13 (last-stand engagement)

**This was the live Hukbo baseline until the shield-clash audio block above
captured the same four workloads unchanged.** It supersedes the combat cadence V6 block
below by adding a **fourth workload** and changing nothing else. Workloads 1, 2,
and 3 are byte-identical to that block, and this run is their next independent
capture.

`./scripts/verify.ps1`, exit code 0, on branch `ls-endgame` off `main` at
`8da5d92`:

```
Formatted 0 of 753 files.
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
Hukbo.Core.Tests     Total tests: 2503   Passed: 2503
Hukbo.Client.Tests   Total tests: 3682   Passed: 3682
[PASS] Release repository tests completed.
stateHash 5460D13E3F7FD3E5   eventHash 8E18ED1437B2924B   combatPreset 6   movementPreset 4
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
stateHash C8023D3B5BEB005E   eventHash F709A345E2F7370E   combatPreset 5   movementPreset 8
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
stateHash 7C145A9E05916E4C   eventHash 77626E104234206C   combatPreset 5   movementPreset 10
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
stateHash 6225182B4A470F91   eventHash C4DABE6AF98B6BEC   combatPreset 5   movementPreset 11
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

**None of the three existing pairs moved, which is the required result.** The
last-stand regroup yields change shared, unversioned simulation code, so the
whole point of carrying them on a new movement preset is that every preset from
V1 to V10 keeps its behaviour. Workload 1 (`combatPreset 6` / `movementPreset
4`), workload 2 (`5` / `8`), and workload 3 (`5` / `10`) are each byte-identical
to the pairs recorded in the combat cadence V6 block. Had the yields leaked past
their preset gate, or had the six battlefield-realism identity gates been
rewritten wrongly when V11 was admitted to them, workload 3 in particular would
have moved.

**Workload 4, `LastStandEngagementV11` (`combatPreset 5` / `movementPreset 11`),
is new**, and it is the preset the client now selects. `PrecolonialPhilippinesV5`
paired with it ran 200 agents to a decision at tick 2,037 with `Faction0Victory`
and 18 survivors, `stateHash 6225182B4A470F91`, `eventHash C4DABE6AF98B6BEC`,
`deterministic true`.

It is worth reading workload 4 against workload 3, since V11 restates every one
of V10's registered field values and differs only by the two yields. Same
outcome and the same 18 survivors, but 2,037 ticks against V10's 1,888. The
trajectory diverging is the change working: followers that used to hold station
51 world units behind their rally agent now close on their own enemies once the
fighting starts. A byte-identical pair here would have meant the yields never
fired.

`Hukbo.Core.Tests` grew from 2,492 to 2,503 and `Hukbo.Client.Tests` from 3,651
to 3,682. The 11 new Core tests are `LastStandEngagementV11Tests`; the Client
growth is not this change's, which only edited two assertions in
`ScriptDefaultsTests` to match the fourth gate block.

**Note on running the suites in `Debug`.** Two allocation-budget tests —
`MovementContextObservationTests.RepeatedQuietV6TicksHaveBoundedAllocations` and
`MovementPipelineIntegrationTests.RepeatedVSixCollisionTicksHaveBoundedAllocations`
— fail under `dotnet test -c Debug` and pass in the `Release` run the gate
makes. This was confirmed to be pre-existing by running them on unmodified
`main`, where they fail the same way. Do not read a red pair there as a
regression, and do not adjust either budget on the strength of a `Debug` run.

## Canonical gate result — Hukbo, 2026-08-11 (combat cadence V6)

**Superseded by the 2026-08-13 last-stand engagement block above**, which adds a
fourth workload and reproduces all three of this block's pairs unchanged. It
superseded the battlefield realism block below for **workload 1 only**.
Workloads 2 and 3 were unchanged, and this run was the third independent capture
of both.

`./scripts/verify.ps1 -SkipBootstrap`, exit code 0, on branch
`combat-cadence-v6` rebased onto `main` at `817c900`, the battlefield realism
merge:

```
Formatted 0 of 737 files.
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
Hukbo.Core.Tests     Total tests: 2492   Passed: 2492
Hukbo.Client.Tests   Total tests: 3651   Passed: 3651
[PASS] Release repository tests completed.
stateHash 5460D13E3F7FD3E5   eventHash 8E18ED1437B2924B   combatPreset 6   movementPreset 4
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
stateHash C8023D3B5BEB005E   eventHash F709A345E2F7370E   combatPreset 5   movementPreset 8
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
stateHash 7C145A9E05916E4C   eventHash 77626E104234206C   combatPreset 5   movementPreset 10
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

**Exactly one of the three pairs moved, which is the required result.**

**Workload 1, the shipped default, moved and had to.** It names no preset, so
it follows `Scenario.CombatPreset`, which this change flipped from
`PrecolonialPhilippinesV4` to `PrecolonialPhilippinesV6` — V4's tables with
every melee attack cooldown, combo cooldown, and damage retuned. Both hashes
had to move: the preset identifier folds into the state hash, and halving the
attack rate changes the ordered event stream from the first exchange onward.
The superseded pair is `1B73FC5923879AA0` (state) and `AC55684F24D39344`
(event). `movementPreset` is still 4 — this change does not touch the movement
default.

**Workloads 2 and 3 are byte-identical to the pairs battlefield realism
recorded** — `C8023D3B5BEB005E` / `F709A345E2F7370E` for
`PrecolonialPhilippinesV5` with `RangedStandoffV8`, and `7C145A9E05916E4C` /
`77626E104234206C` for `PrecolonialPhilippinesV5` with
`BattlefieldRealismV10`. Both name their combat preset explicitly, so both are
leak detectors: had V6 been an in-place edit of V4 rather than a new preset, or
had anything reached V5, these pairs would have moved. They did not.

The default workload also decided faster, at 885 ticks against the previous
981, with `Faction0Victory` replacing `Faction1Victory`. A different winner on
a retuned ruleset is expected, not a regression. Twenty seeds were measured
against both presets before the flip, and re-measured after the rebase onto the
battlefield realism merge with identical results — both decide all twenty, V6's
median decision tick is 1,651 against V4's 1,668. That the sweep did not move
across the rebase is itself informative: battlefield realism's changes sit
behind `BattlefieldRealismV10`, which the default workload never selects. The
measurement is recorded under task 5 of the combat cadence V6 plan, since
archived out of `docs/plans/`.

`Hukbo.Core.Tests` grew from 2,470 to 2,492 and `Hukbo.Client.Tests` from 3,643
to 3,651. The 22 new Core tests are `CombatCadenceV6Tests`; the 8 new Client
tests are the attack-animation speed ceiling.

Still no evidence about anything interactive. CL-1, CL-3, CL-7a, and CL-7b in
the weapon-clash smoke checklist are all `PENDING` and only a person may
change that.

## Canonical gate result — Hukbo, 2026-08-11 (projectile-props)

> **Superseded for the default workload.** The `combatPreset 4` pair below was
> the live baseline until the combat-cadence change flipped the shipped default
> to `PrecolonialPhilippinesV6`; see the block above. The `combatPreset 5` pair
> is still current and is unchanged.

`./scripts/verify.ps1 -SkipBootstrap`, exit code 0, run on the merge of
`projectile-props` into `main`:

```
Formatted 0 of 725 files.
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
Hukbo.Core.Tests     Total tests: 2433   Passed: 2433
Hukbo.Client.Tests   Total tests: 3561   Passed: 3561
[PASS] Release repository tests completed.
stateHash 1B73FC5923879AA0   eventHash AC55684F24D39344   combatPreset 4   movementPreset 4
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
stateHash C8023D3B5BEB005E   eventHash F709A345E2F7370E   combatPreset 5   movementPreset 8
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

**Both hash pairs are byte-identical to the recorded baselines**, which is the
result projectile-props needed rather than merely a green gate: the change is
entirely presentation, so a moved hash would have meant something reached the
simulation and the change was wrong. `Hukbo.Client.Tests` grew from 3,499 to
3,561; the 62 new tests are the projectile silhouettes, the embedded-projectile
ring buffer, and the attachment anchors.

`Sandata.Core.Tests` moved from 1,113 to 1,118 and Sandata's own digests moved
on the same day. **Neither is projectile-props.** Both come from the
`MissionState.Tick` fix recorded under the Sandata gate result below, and no
file this change touched is outside `Hukbo.Client`, its tests, and `docs/`.

Still no evidence about anything interactive. All eight `PP-*` rows in the
projectile-props smoke checklist were `PENDING` when this gate ran. They stayed
that way until 2026-08-13, when a person ran the family and passed seven of
them; the eighth, `PP-3`, did not pass on that sitting. What that tester found
was an in-flight prop drawing far larger than the warriors it flew past, which
is the opposite of the failure the row is written against. The in-flight prop
was capped at the pawns' own apparent-scale ceiling in response, shipping on
2026-08-13 as commit `c772849` under a green `./scripts/verify.ps1` whose seed-1
digests did not move. A person then re-ran `PP-3` against that build and passed
it, so the family is closed at 8 of 8 and none of it is evidence about this
gate, which ran before the cap existed.

## Canonical gate result — Hukbo, 2026-08-11 — battlefield realism

`./scripts/verify.ps1`, all five stages, exit code 0, run once on the
`battlefield-realism` branch at `449a443`, rebased onto `main` at `0cc5ce5`,
after all nineteen tasks of the battlefield realism plan landed:

```
[PASS] Platform: Windows x64
[PASS] PowerShell: 7.6.4
[PASS] git version 2.55.0.windows.3
[PASS] .NET SDK: 10.0.302
[PASS] packages.lock.json present for all 21 projects.
[PASS] MonoGame packages are centrally pinned: 3.8.5
[PASS] Required prerequisites and repository configuration are present.
[PASS] Locked package restore completed.
[PASS] Formatting verification completed.
[PASS] Release solution build completed.   0 Error(s)
Hukbo.Core.Tests     Total tests: 2470   Passed: 2470
Hukbo.Client.Tests   Total tests: 3579   Passed: 3579
[PASS] Release repository tests completed.
measuredTicks 981   outcome Faction1Victory   survivors 0 / 6
stateHash 1B73FC5923879AA0   eventHash AC55684F24D39344   combatPreset 4   movementPreset 4
deterministic true   firstMismatchTick null
p50 0.1324 ms   p95 0.9661 ms   p99 1.5207 ms   coreAllocatedBytes 154976
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
measuredTicks 1764   outcome Faction1Victory   survivors 0 / 20
stateHash C8023D3B5BEB005E   eventHash F709A345E2F7370E   combatPreset 5   movementPreset 8
deterministic true   firstMismatchTick null
p50 0.1336 ms   p95 0.8155 ms   p99 1.3750 ms   coreAllocatedBytes 161168
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
measuredTicks 1888   outcome Faction0Victory   survivors 18 / 0
stateHash 7C145A9E05916E4C   eventHash 77626E104234206C   combatPreset 5   movementPreset 10
deterministic true   firstMismatchTick null
p50 0.0804 ms   p95 0.7977 ms   p99 1.3022 ms   coreAllocatedBytes 161168
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

Three headless workloads now run inside the gate, added by task 14 of the
battlefield realism plan: the shipped default, the V8 ranged-standoff preset,
and the new `BattlefieldRealismV10` preset.

**Workload 1, the shipped default (`combatPreset 4` / `movementPreset 4`), is
byte-identical to the recorded baseline** — `stateHash 1B73FC5923879AA0`,
`eventHash AC55684F24D39344`, `Faction1Victory`, 981 ticks — which is rule 1 of
the plan holding: `PersistentContingentsV4` and `PrecolonialPhilippinesV4`
never moved.

**Workload 2, the V8 ranged preset (`combatPreset 5` / `movementPreset 8`), is
byte-identical to the pre-change capture recorded above under "Canonical gate
result — Hukbo, 2026-08-11"** — `stateHash C8023D3B5BEB005E`,
`eventHash F709A345E2F7370E`, taken on the base commit before any battlefield
realism task landed. That identity is the proof that `BattlefieldRealismV10`
did not leak into `RangedStandoffV8Ruleset`'s behaviour.

**Workload 3, `BattlefieldRealismV10` (`combatPreset 5` / `movementPreset 10`),
is new.** `PrecolonialPhilippinesV5` paired with `BattlefieldRealismV10` ran
1,888 ticks to `Faction0Victory`, 18 survivors on faction 0 against 0 on
faction 1, `stateHash 7C145A9E05916E4C`, `eventHash 77626E104234206C`,
deterministic true.

`Hukbo.Core.Tests` grew from 2,433 to 2,470 and `Hukbo.Client.Tests` from 3,561
to 3,579 over this package, with zero failures and zero skipped in either
suite.

This gate result is evidence about the build, the tests, and the three
headless workloads only. It proves nothing about anything interactive: the
`BR-1` through `BR-10` rows needed a person at an interactive desktop, and got
one. All ten were run and passed on 2026-08-14 — `BR-5` through `BR-9` first,
then the other five once the fixes they were waiting on landed — so the family
left `smoke-checklist.md` whole. Its records are the two archive documents
titled "Battlefield realism cohort and retreat smoke — rows BR-5 to BR-9 closed
2026-08-14" and "Battlefield realism cohort smoke — closed 2026-08-14". The
persistent-contingent reset rows
that used to sit alongside them closed on 2026-08-13; their record is the
archive document titled "Persistent contingent smoke — closed 2026-08-13".

### Task 10 — the twenty-seed termination sweep

`BattlefieldRealismV10` paired with `PrecolonialPhilippinesV5`, 200 agents,
10,000-tick cap, seeds 1 through 20. Every seed reports `deterministic true`.

| Seed | Ticks | Outcome | Faction 0 survivors | Faction 1 survivors | State hash | Event hash |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 1,888 | Faction0Victory | 18 | 0 | `7C145A9E05916E4C` | `77626E104234206C` |
| 2 | 2,275 | Faction0Victory | 13 | 0 | `35F7ECFA46403889` | `3F93E64D6578A53F` |
| 3 | 2,364 | Faction1Victory | 0 | 17 | `B36DA7A4959525DD` | `BBA3ABEC31567E2D` |
| 4 | 2,007 | Faction1Victory | 0 | 19 | `E6D74A7955F105E6` | `8BC3971E36D892C1` |
| 5 | 2,872 | Faction0Victory | 9 | 0 | `89466D8E0641AB6E` | `E9D51389BCA66123` |
| 6 | 1,719 | Faction0Victory | 23 | 0 | `F0AFD0ED02E609A1` | `66E25E30CD49457C` |
| 7 | 3,264 | Faction1Victory | 0 | 10 | `E9B9B7F47D1365EA` | `B67CC21198931F3D` |
| 8 | 2,296 | Faction1Victory | 0 | 14 | `F68011F70E431BD7` | `DB439E52DFEA5D3E` |
| 9 | 1,733 | Faction0Victory | 35 | 0 | `14507D8A7770A93B` | `265118917182DCCB` |
| 10 | 1,987 | Faction0Victory | 20 | 0 | `DA4D017322E24E48` | `A870FB7F09F24621` |
| 11 | 1,817 | Faction1Victory | 0 | 20 | `77D006A3051CABD6` | `B20546170E8A5692` |
| 12 | 2,348 | Faction1Victory | 0 | 16 | `F810B5D60B287C5B` | `093740B9CA3A3D4D` |
| 13 | 2,752 | Faction0Victory | 10 | 0 | `165EDA317EC630AA` | `F6F3CFD1906DB4E4` |
| 14 | 2,019 | Faction1Victory | 0 | 18 | `D4CEB5E22BD71C78` | `BD78ABD40E3F77DA` |
| 15 | 2,253 | Faction0Victory | 18 | 0 | `D092307F13143A60` | `F274E77FB594B464` |
| 16 | 1,442 | Faction0Victory | 33 | 0 | `30355013F514E821` | `FF2EBFD56C01FE78` |
| 17 | 1,554 | Faction0Victory | 28 | 0 | `531C95EBF673B0F7` | `95C60E98FCA241CA` |
| 18 | 2,753 | Faction1Victory | 0 | 9 | `3B5F03CDC6D94A02` | `0BD6D3CA491A4925` |
| 19 | 1,597 | Faction0Victory | 23 | 0 | `D11976D5E24AC65B` | `F631AA61F476A128` |
| 20 | 2,750 | Faction1Victory | 0 | 8 | `E6BB5457DD6144A4` | `3FE8FA25B312AD11` |

Against design section 8.3's bar, all four clauses passed on the first
measurement, with no tuning performed and `ThreatRadiusBasisPoints` left at its
committed value of 5,000:

- **No seed reaches the 10,000-tick cap.** The longest run is seed 7 at 3,264
  ticks.
- **Seed 1 is at or under 1,962 ticks.** It measured 1,888.
- **The median is at or under 3,000 ticks.** The median of the twenty measured
  values is 2,253.
- **Both factions win at least one battle.** Faction 0 wins 11 of the twenty
  seeds; faction 1 wins 9.

## Canonical gate result — Hukbo, 2026-08-14 (cohort lateral spread)

`./scripts/verify.ps1` with no arguments, run at commit `541b8d6`, green in
full. Every stage passed: prerequisites, locked restore for all 21 projects,
formatting verification, the Release solution build, the Release test suites,
and the headless determinism workloads.

| Stage | Result |
| --- | --- |
| Prerequisites (.NET SDK 10.0.302, PowerShell 7.6.4) | `[PASS]` |
| Locked package restore | `[PASS]` |
| Formatting verification | `[PASS]` |
| Release solution build | `[PASS]` |
| `Hukbo.Core.Tests` | 2,568 passed, 0 failed |
| `Hukbo.Client.Tests` | 3,791 passed, 0 failed |
| Headless workloads, 200 agents / 10,000 ticks / seed 1 | five, all `deterministic: true`, `firstMismatchTick: null` |

The gate ran five headless workloads rather than four for the first time. The
fifth was added by this change, because the fourth block existed to exercise
whatever preset the client actually ships, and `CohortLateralSpreadV13` is now
that preset. The `LastStandEngagementV11` block was kept rather than repointed
— the same choice the V11 and V10 blocks each record for their predecessor —
and it is what proves the new riffled deployment never leaked into the
ascending traversal every earlier preset still uses.

| Workload | Combat / movement preset | Outcome | State hash | Event hash |
| --- | --- | --- | --- | --- |
| Canonical | 6 / 4 | `Faction0Victory` | `5460D13E3F7FD3E5` | `8E18ED1437B2924B` |
| Ranged | 5 / 8 | `Faction1Victory` | `C8023D3B5BEB005E` | `F709A345E2F7370E` |
| Battlefield realism | 5 / 10 | `Faction0Victory` | `7C145A9E05916E4C` | `77626E104234206C` |
| Last stand | 5 / 11 | `Faction0Victory` | `6225182B4A470F91` | `C4DABE6AF98B6BEC` |
| Cohort lateral spread | 5 / 13 | `Faction1Victory` | `4A0723BC9A1B924B` | `E0CE32CF8830A864` |

**The first four digests are byte-identical to the values already recorded in
this file.** That is the load-bearing result, not the fifth row: the whole
point of introducing a new preset id rather than editing a registered one was
that no existing golden expectation should move, and none did.
`FormationDeploymentFreezeTests` and `MovementPresetFreezeTests` — the fourteen
facts that exist to catch exactly this — are green untouched, which is the
evidence that `FormationPlanner` was not modified.

The V13 workload's own digest is a new baseline, recorded here for the first
time. Its seed-1 battle ends at a terminal outcome well inside the tick cap, so
the second half of smoke row 61 holds under the new preset; the first half of
that row is a visual judgement and is not evidence this gate can produce.

**This gate was run in an isolated worktree at `541b8d6`, not in the main
checkout.** Another session was working in the shared tree at the time, with
uncommitted changes to `AgentInspectorContent.cs`, `AgentInspectorPanel.cs` and
`AgentInspectorContentTests.cs`. A gate run in the main checkout failed three
`AgentInspectorContentTests` facts — an inspector geometry baseline, a row
budget, and a `ZZZ_TEMP_Diagnostic` test left in the suite — all of which belong
to that in-flight work and none of which this change touches. The worktree run
is the verdict on this change; the main checkout cannot produce a clean one
until the other session's work settles.

**Re-run on main at `33de5cd`, also green.** Once the V13 work was on main, a
concurrent session's commit `c7ecdec` — messaged as a documentation change —
also carried nine `Hukbo.Client` source and test files, so the tree the gate
above verified was no longer main's. The gate was run again from a clean
worktree at `33de5cd`: every stage passed, `Hukbo.Core.Tests` 2,568 of 2,568,
`Hukbo.Client.Tests` 3,771 of 3,771, and all five headless workloads
`deterministic: true` with the five state hashes byte-identical to the table
above, `4A0723BC9A1B924B` included. The Client count fell from 3,791 to 3,771
because a concurrent unit-test cleanup merge removed twenty tests; no test of
this change's was among them. Main has moved on past `33de5cd` since, so that
commit is the last point this workstream verified, not a claim about whatever
main holds now.

**The interactive evidence arrived the same day.** Smoke rows 58 and 59, which
this change was made for, were re-run by a person against a build carrying
`CohortLateralSpreadV13` and both passed: the weapon groups read as spread
across each team's frontage, and the two sides showed the same group counts.
With rows 60, 61 and 61a already passed on the pre-V13 build, the starting
deployment family closed at five of five and left the live checklist. Its record
is the 2026-08-14 archive titled **"Starting deployment smoke"**, named in prose
rather than linked because that folder is pruned periodically.

## Canonical gate result — Hukbo, 2026-08-14 (agent inspector row wrapping)

`./scripts/verify.ps1 -SkipBootstrap`, run in the main checkout on 2026-08-14.
**Verdict: pass.** The run ended:

```
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

The headless workload reported `"firstMismatchTick": null`, `"combatPreset": 5`,
`"movementPreset": 13`, `"coreAllocatedBytes": 154976`, 2 487 accepted attacks
and 1 710 landed, and `"maximumPenetrationRaw": 0`.

**This run is not a clean verdict on the inspector change alone, and must not be
cited as one.** `"movementPreset": 13` says what the working tree was: the
cohort lateral spread workstream's uncommitted `CohortLateralSpreadV13` was
present and was the selected default, alongside its uncommitted client
presentation work. The run is evidence that the two changes are green
*together* in this tree at this moment. Whoever commits either one separately
owes a gate run against that commit.

What the inspector change itself is entitled to claim is narrower and cleaner:
`./scripts/test.ps1 -Configuration Release` reported **2 568 of 2 568 Core tests
passed** and **3 805 of 3 805 Client tests passed**. The change touches three
files under `src/Hukbo.Client/UI` and `tests/Hukbo.Client.Tests`, no
`Hukbo.Core` file, no preset and no ruleset, so it cannot move either hash — and
the identical `movementPreset` and null mismatch tick above are consistent with
that rather than proof of it.

**The new width test was proven able to fail** before being trusted. Removing
the helper's second, narrower wrap pass — so a continuation line was measured
against the full budget instead of the budget less the indent — turned the
width-budget sweep red at all four pixels-per-character theories, with
overflow assertions naming the 277-pixel budget. Restoring it returned both
suites to green. That falsification matters here more than usual: this
repository has shipped assertions that passed in both directions.

**One consequence is recorded rather than buried.** Wrapping raises the reserved
lower-row count from 24 to 47, and the panel's height baseline from 953 to
1 505 pixels. At the smallest supported 1024 × 720 window that panel is more
than twice the window height, so a fully-loaded warrior drops more trailing
provenance rows than before. The panel refuses those rows rather than drawing
past its bounds, which is the pre-existing contract, but the vertical fit is
worse than it was and the change does not claim otherwise. It is the open
question in section 6 of the inspector row wrapping design.

**No evidence about anything interactive.** Smoke row `BR-10` is what this change
was made for, and a person at an interactive desktop re-ran it against this fix
on 2026-08-14 and closed it `PASS`. The row is no longer in the live checklist;
its record is the archive document titled "Battlefield realism cohort smoke —
closed 2026-08-14".

## Canonical gate result — Hukbo, 2026-08-14 (ranged package closeout)

`./scripts/verify.ps1 -SkipBootstrap`, run twice on 2026-08-14 in a dedicated
integration worktree at branch `ranged-integration`. The first run had both
closeout branches merged in and reported 763 files, 2 568 Core tests and 3 787
Client tests. **The run recorded here is the second**, taken after `main` moved
four commits underneath the branch — the death-collapse feature at `0d4b34e`,
the nine-slice panel texture at `54c0bca`, an archive prune, and a backlog tidy
— and `main` was merged into the branch. That merge produced no conflicts.
**Verdict: pass, exit code 0.** The run reported:

```
Formatted 0 of 770 files.
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
Total tests: 2568     Passed: 2568
Total tests: 3850     Passed: 3850
[PASS] Release repository tests completed.
[PASS] Canonical repository verification completed.
```

The Client suite's rise from 3 787 to 3 850 is the death-collapse work's own
tests arriving with the merge, not anything this closeout added.

All five headless workloads reproduced their recorded pairs:

| Workload | combat / movement | stateHash | eventHash |
| --- | --- | --- | --- |
| Default | 6 / 4 | `5460D13E3F7FD3E5` | `8E18ED1437B2924B` |
| Ranged standoff | 5 / 8 | `C8023D3B5BEB005E` | `F709A345E2F7370E` |
| Battlefield realism | 5 / 10 | `7C145A9E05916E4C` | `77626E104234206C` |
| Last-stand engagement | 5 / 11 | `6225182B4A470F91` | `C4DABE6AF98B6BEC` |
| Cohort lateral spread | 5 / 13 | `4A0723BC9A1B924B` | `E0CE32CF8830A864` |

**Why this run was not made in the main checkout.** Another session was working
in the shared tree throughout. At first that was uncommitted death-collapse
work, including 115 new lines in `PawnGeometry.cs`, a file the archiving change
also edits; `git merge` refused outright rather than touching it. That session
then committed, and by the time this second run finished it was holding
`docs/development/smoke-checklist.md`, `docs/development/testing.md`, and
`docs/plans/README.md` staged — the same three files this closeout changes. So
the merge and both gate runs were done in a separate worktree and `main` was
left alone throughout. **The closeout is therefore verified but unmerged**, and
whoever merges it owes nothing further: this is the gate run for that merge,
taken at the merged tree with `main`'s own latest work folded in.

**The ranged workload reproducing `C8023D3B5BEB005E` / `F709A345E2F7370E` is
the load-bearing line here.** The composition change moves the client's default
army, which is the sort of change that looks like it should move a hash. It does
not, and this is the evidence rather than the argument: `ArmyComposition` is a
`Hukbo.Client` settings record, the headless workloads build their scenarios
without it, and every recorded pair is unchanged to the byte.

**The Client suite fell from 3 805 to 3 787.** That is not a regression and not
a deletion of coverage. The composition work replaced one test that asserted
behaviour which no longer exists — a schema-8 file loading and defaulting its
movement preset — with one asserting the behaviour that replaced it, and the
roster-expansion suite's theory cases collapsed as the expected apportionment
became a single calibrated array rather than several even-split cases.

**No evidence about anything interactive.** The three new `AC-*` rows in the
smoke checklist are what this change owes, and every one of them is `PENDING`.
The gate never opened the Army Composition panel, never discarded a settings
file, and never watched a battle.

## Sandata — recorded baselines and measurement runs, 2026-08-09

This repository builds two games. Everything above and below this section, unless
it names Sandata, is about Hukbo. This section is Sandata's, and it is separate
on purpose: the two games have independent simulations, independent hashes, and
independent gate invocations, and a result from one is never evidence about the
other.

### How Sandata is run

```powershell
./scripts/run.ps1 -Game Sandata                          # launch it
./scripts/test.ps1 -Configuration Release -Game Sandata  # both Sandata suites
./scripts/benchmark.ps1 -Game Sandata -Seed 1            # the headless workload
./scripts/verify.ps1 -Game Sandata                       # the five gate stages
```

**`./scripts/verify.ps1` with no `-Game` flag runs Hukbo only.** It never builds
or runs a line of Sandata, so a green default gate says nothing whatever about
this game. The default gate stays on the Hukbo workload alone until Sandata's
seed-1 baseline has settled, so that a red Sandata run can never be mistaken for
a red Hukbo one.

### Test suites, measured 2026-08-09

| Suite | Tests | Inside `verify.ps1 -Game Sandata` |
| --- | --- | --- |
| `Sandata.Core.Tests` | 1,113 | 4.5 s |
| `Sandata.Client.Tests` | 199 | 0.5 s |

**Both counts have moved since; the timings have not.** The 2026-08-12 gate
result below records 1,132 core tests in 4.96 s and 295 client tests in 0.63 s.
The row above is kept because the paragraphs that follow it explain how the
runtime got to where it is, and that explanation is about the 4.5 seconds rather
than about the count.

Tasks 88, 89, and 90 added seven tests to the core suite and no measurable time
to it — the count moved from 1,106 to 1,113 while the runtime stayed where task
91 left it.

**These figures replace the ones recorded earlier the same day**, and the reason
they moved is worth keeping. The core suite was 1,104 tests in 37.77 seconds
warm and 1.08 minutes inside the gate. Thirty-six of those seconds were one
`InlineData` value on one theory that ran the navigation benchmark for 2,000
ticks; every other test in the project cost under 121 milliseconds. Task 91
measured what that endpoint actually detected, found that a 200-tick endpoint
detects the same defect by 33 points of margin instead of 84, and removed the
2,000-tick case. Tasks 87 and 91 together moved the count from 1,104 to 1,106.

The lesson generalises past this suite: **get per-test durations before
reasoning about what a suite costs.** `dotnet test --logger
'console;verbosity=normal'` prints a bracketed duration per test. Three sessions
carried the belief that "seven benchmark test cases" were responsible and that
they were "roughly half" the runtime; both halves of that were wrong, and one
sorted list of durations settled it.

For comparison, the canonical Hukbo gate run on the same machine and the same
day reports `Hukbo.Core.Tests` at 2,376 tests in 29.49 s and
`Hukbo.Client.Tests` at 3,270 tests in 2.07 s. Every one of Hukbo's 3,270
presentation tests runs in about two seconds because none of them constructs a
graphics device or a window; Sandata's slowness is entirely the benchmark cases
and not a presentation problem.

Whether the benchmark cases belong in the suite at all was settled by task 55:
they stay. Task 91 then removed the one endpoint that made the question look
expensive. What remains of the navigation benchmark inside the suite runs in
about three seconds and locks a defect the same wave found and fixed.

### The seed-1 headless workload, measured 2026-08-09

```
BO | Microsoft Windows 10.0.26200 (X64) | 20 logical processors | .NET 10.0.10
```

`./scripts/benchmark.ps1 -Game Sandata -Seed 1`, which is 200 operators — 100
per faction — over 10,000 ticks:

```
measuredTicks 10000   outcome Ongoing   survivors 70 / 64
stateHash BDD56EBD06F76674   eventHash 7C1B37876769DEC7   deterministic true
p50 2.6761 ms   p95 3.8265 ms   p99 4.8984 ms   max 64.1713 ms
durationMilliseconds 28393.9   allocatedBytes ~42.18 GB
```

> **Superseded on 2026-08-11. Both hashes above are stale and must not be
> compared against.** The live seed-1 baseline is the block under
> "The seed-1 headless workload, re-measured 2026-08-11" below. The run above
> is kept because the outcome, both survivor counts, and the timings it
> records are still the reference for everything except the two digests.

**The allocation figure is now about 6.08 GB, and everything else above is
unchanged.** Task 88 gave stage 5's line-of-sight and contact-memory calls
caller-owned scratch buffers, which took the whole tick from about 2.37 MB to
about 330 KB per simulation-tick at 200 operators and the workload from about
42.18 GB to about 6.08 GB over ten thousand ticks. Both hashes, both survivor
counts, the outcome, and `deterministic: true` are all exactly as printed
above, which is the proof that a pure allocation change changed no outcome.

`SandataRuleset.ContentHash` is `8_955_292_433_887_190_872`, pinned by
`SandataRulesetTests`. It is **unchanged** by the 2026-08-11 re-measurement
below, which is the point of that entry: the ruleset content did not move, a
defect in `SandataSimulation.RunTick` did.

### The seed-1 headless workload, re-measured 2026-08-11

> **Superseded on 2026-08-12. Both hashes in this block are stale and must not
> be compared against.** The live seed-1 baseline is the block under "The seed-1
> headless workload, re-measured 2026-08-12" below. Everything else this block
> records — the outcome, both survivor counts, the timings, and the allocation
> magnitude — still holds.

This block was the live Sandata seed-1 baseline between 2026-08-11 and
2026-08-12. It replaced the 2026-08-09 digests above.

```
BO | Microsoft Windows 10.0.26200 (X64) | 20 logical processors | .NET 10.0.10
```

`./scripts/verify.ps1 -Game Sandata`, stage five, which is 200 operators — 100
per faction — over 10,000 ticks:

```
measuredTicks 10000   outcome Ongoing   survivors 70 / 64
stateHash AB44D2319A91422A   eventHash 3C0C243989A09A43   deterministic true
p50 2.3975 ms   p95 2.7172 ms   p99 3.6566 ms   max 62.4547 ms
durationMilliseconds 24853.4   allocatedBytes ~6.08 GB
```

**Both hashes moved, and nothing else did.** `outcome`, both survivor counts,
and `deterministic: true` are identical to the 2026-08-09 run. The cause is
that `SandataSimulation.RunTick` now writes `MissionState.Tick`. Until
2026-08-11 nothing in `Sandata.Core` ever wrote that field, so it stayed 0 for
the whole of every run: `SandataStateHasher` folded a constant zero, every
emitted event carried tick 0 regardless of when it fired, and
`HeadlessRunner`'s per-tick divergence check compared 0 against 0 on every
tick of every run. The state hash moved because the folded field is now the
real tick, and the event hash moved because the events carry the tick they
happened on.

**This is deliberately not a new `SandataPresetId`.** Design section 4's
trigger list for a new preset value is an enum's numeric value, an enum's
order, the roster order, a weapon weight, the tick rate, the millisecond
conversion rule, or a hash mixer. A defect in `RunTick` is none of them, the
ruleset content is untouched, and `SandataRuleset.ContentHash` is unchanged —
so `ModernTacticalV1 = 1` still names exactly the ruleset it always named.
Sandata has no v0.1 and no recorded replay outside this repository, so nothing
existed that the old digests had to keep reproducing.

The golden replay fixtures moved with it.
`tests/Sandata.Core.Tests/Fixtures/seed-1-baseline.json` was re-measured in the
same change: seventy-eight of its eighty state hashes and both of its event
hashes are new, and the two tick-0 state hashes are byte-identical to their
recorded values. That signature is the confirmation rather than a coincidence —
on tick 0 the field was already 0, so only tick 1 onward could move.
`MissionStateTests.PreTask79cBaselineHash` is unchanged and was not re-pinned,
because it hashes a hand-built state whose `Tick` is set explicitly and the fix
did not touch `SandataStateHasher`.

**The allocation figure is a magnitude and must never be recorded as an exact
byte count.** It is not part of the determinism contract and it is not
bit-reproducible: three runs of identical trees during this wave reported
42,184,447,672, then 42,184,446,424, then 42,184,440,712 bytes. Those differ by
thousands of bytes and mean the same thing. The figure to carry forward is
"about 42.18 GB over ten thousand ticks, down from about 48.64 GB before the
per-stage allocation work". A fourth run the same day reported
42,184,446,456 bytes, which makes the point again.

### The seed-1 headless workload, re-measured 2026-08-12

This is the live Sandata seed-1 baseline. It replaces the 2026-08-11 digests
above.

```
BO | Microsoft Windows 10.0.26200 (X64) | 20 logical processors | .NET 10.0.10
```

`./scripts/verify.ps1 -Game Sandata`, stage five, which is 200 operators — 100
per faction — over 10,000 ticks:

```
measuredTicks 10000   outcome Ongoing   survivors 70 / 64
stateHash A644B7F8A394885D   eventHash AEDE4D16B5E6FAAF   deterministic true
p50 2.3630 ms   p95 3.3004 ms   p99 4.4539 ms   max 67.8065 ms
durationMilliseconds 25188.5   allocatedBytes ~6.12 GB
```

**Both hashes moved, and nothing else did** — `outcome`, both survivor counts,
and `deterministic: true` are identical to the two runs above. Three changes
landed together, and each one is a rule that had been fully implemented and
fully unit-tested in `Sandata.Core` while having no production caller anywhere
in `src/`:

- **Stage 1 now advances and clears an `OrderAssignment`.**
  `MovementSource.Evaluate` had no caller and nothing ever advanced
  `OrderAssignment.CurrentNodeIndex`, so an operator handed an authored
  polyline walked to its first node and stood on it for the rest of the run.
  This moves no hash in *this* workload, which carries no orders at all — see
  "Two things this workload does not prove" below — but it moves both golden
  replay fixtures.
- **Stage 11 now stores `OperatorState.WeaponLowered`.** The field was folded
  into the state hash on every tick of every run while never once being
  assigned. It is written from `WeaponLoweredRules.IsForcedLowered`, and the
  transition emits the authoritative event design section 9 requires. This
  workload has no walls, so the flag stays false throughout it and this change
  alone would not have moved either digest here either.
- **Stage 11 now selects a fire mode and drives the cyclic accumulator.**
  `FireModeSelection.SelectMode` and `CyclicFireAccumulator.Advance` both had no
  caller, so every shot in the game came from the weapon chain's own cycle, one
  round per cycle, for every weapon at every range. This is the change that
  actually moved both digests here: a rifle inside its auto band now fires at
  600 rounds per minute, which lands more rounds in the same ten thousand ticks.

**This is deliberately not a new `SandataPresetId`**, for the same reasons the
2026-08-11 entry gives. `MissionEventKind` gained two members at free ordinals 4
and 5 with nothing renumbered or reordered, `SandataRuleset.ContentHash` is
unchanged at `8_955_292_433_887_190_872`, and no weapon row, tick rate, or hash
mixer moved.

The golden replay fixtures moved with it, re-measured in the same change by
running a capture rather than by hand. `EmptyOrderStream`'s per-tick state
hashes are byte-identical through tick 36 and differ from tick 37 onward, which
is the signature this change should have on a wall-free, order-free fixture:
only automatic fire could move it, and only once the first burst had resolved.
Both event hashes moved because `MissionEvent.ShotFired`'s reason code now
carries the fire mode instead of a constant zero.
`MissionStateTests.PreTask79cBaselineHash` is unchanged and was not re-pinned,
because it hashes a hand-built state and `SandataStateHasher` did not change.

`NonEmptyOrderStream`'s authored path was lengthened in the same change, from
`(4,4)->(12,4)` to `(4,4)->(36,4)`. Eight world units is half the node-arrival
radius stage 1 now uses, so the old path was finished on the tick after it was
given, and a baseline recording an order nobody ever walked is a baseline that
proves less than it appears to.

### Canonical gate result — Sandata, 2026-08-09

`./scripts/verify.ps1 -Game Sandata`, all five stages, exit code 0:

```
[PASS] Platform: Windows x64
[PASS] PowerShell: 7.6.4
[PASS] .NET SDK: 10.0.302
[PASS] packages.lock.json present for all 973 projects.
[PASS] MonoGame packages are centrally pinned: 3.8.5
[PASS] Required prerequisites and repository configuration are present.
[PASS] Locked package restore completed.
[PASS] Formatting verification completed.
Build succeeded.  0 Warning(s)  0 Error(s)
[PASS] Release solution build completed.
Sandata.Core.Tests     Total tests: 1104   Passed: 1104   Total time: 1.0803 Minutes
Sandata.Client.Tests   Total tests:  199   Passed:  199   Total time: 0.5005 Seconds
[PASS] Release repository tests completed.
measuredTicks 10000   outcome Ongoing   survivors 70 / 64
stateHash BDD56EBD06F76674   eventHash 7C1B37876769DEC7   deterministic true
p50 2.6383 ms   p95 4.6475 ms   p99 6.8726 ms   max 64.1272 ms
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

This is the first time Sandata's gate has been run and recorded. It proves the
five stages and the seed-1 digest; it proves nothing interactive, and every row
in the Sandata smoke checklist below stays `PENDING`.

> **The two digests in the transcript above were superseded on 2026-08-11.**
> The transcript itself is left exactly as the run printed it, because a
> recorded gate result is evidence and evidence is not edited after the fact.
> The live values are `stateHash AB44D2319A91422A` and
> `eventHash 3C0C243989A09A43`; see the 2026-08-11 gate result below for the
> run that produced them and the re-measured seed-1 block above for why they
> moved.

### Canonical gate result — Sandata, 2026-08-11

`./scripts/verify.ps1 -Game Sandata`, all five stages, exit code 0, run after
`SandataSimulation.RunTick` began writing `MissionState.Tick`:

```
[PASS] Platform: Windows x64
[PASS] PowerShell: 7.6.4
[PASS] git version 2.55.0.windows.3
[PASS] .NET SDK: 10.0.302
[PASS] packages.lock.json present for all 868 projects.
[PASS] MonoGame packages are centrally pinned: MonoGame.Content.Builder.Task 3.8.5, MonoGame.Framework.DesktopGL 3.8.5
[PASS] Required prerequisites and repository configuration are present.
[PASS] Locked package restore completed.
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
Sandata.Core.Tests     Total tests: 1118   Passed: 1118
Sandata.Client.Tests   Total tests:  219   Passed:  219   Total time: 0.4802 Seconds
[PASS] Release repository tests completed.
measuredTicks 10000   outcome Ongoing   survivors 70 / 64
stateHash AB44D2319A91422A   eventHash 3C0C243989A09A43   deterministic true
p50 2.3975 ms   p95 2.7172 ms   p99 3.6566 ms   max 62.4547 ms
durationMilliseconds 24853.4   allocatedBytes 6,080,464,120 (~6.08 GB)
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

> **The two digests in the transcript above were superseded on 2026-08-12.**
> The transcript is left as the run printed it. The live values are
> `stateHash A644B7F8A394885D` and `eventHash AEDE4D16B5E6FAAF`; see the
> 2026-08-12 gate result below.

`Sandata.Core.Tests` is 1,118 rather than the 1,113 of the previous wave: the
five added tests are the ones that bind the advancing tick, and all five were
break-proofed by pinning `MissionState.Tick` back to 0 and confirming each one
fails. The two golden-replay tests failed alongside them in that same run,
which is seven failures out of 1,118 — recorded here because a break-proof that
does not fail proves nothing, and this one did.

This gate is still no evidence about anything interactive. Every row in the
Sandata smoke checklist below stays `PENDING`.

**Two things this workload does not prove**, both established by measurement
during wave 12 and both worth knowing before anyone reads an unchanged hash as
a result:

- **Nothing in it ever moves.** The fixture publishes no group paths and carries
  no orders, so every operator proposes its own current position on every tick.
  A change to movement speed, formation, or collision is therefore invisible to
  these hashes, and an unchanged hash after such a change is the expected
  outcome rather than a disappointment.
- **It carries no cover and one loadout.** The runner loads no map, so no cover
  record exists, and every operator carries the default firearm. A change to the
  cover or caliber tables cannot move these hashes either.

Both are why Sandata's behavioural evidence lives in `TickPipelineTests` and in
the golden replay fixture rather than in this workload.

### Canonical gate result — Sandata, 2026-08-12

`./scripts/verify.ps1 -Game Sandata -SkipBootstrap`, exit code 0, run on the
merge that closed the order layer, the lowered weapon, and automatic fire:

```
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
Sandata.Core.Tests     Total tests: 1132   Passed: 1132   Total time: 4.9632 Seconds
Sandata.Client.Tests   Total tests:  295   Passed:  295   Total time: 0.6316 Seconds
[PASS] Release repository tests completed.
measuredTicks 10000   outcome Ongoing   survivors 70 / 64
stateHash A644B7F8A394885D   eventHash AEDE4D16B5E6FAAF   deterministic true
p50 2.3630 ms   p95 3.3004 ms   p99 4.4539 ms   max 67.8065 ms
durationMilliseconds 25188.5   allocatedBytes 6,120,559,480 (~6.12 GB)
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

`-SkipBootstrap` was passed, so the prerequisite and locked-restore stage above
is the one from the same day's earlier full run rather than from this one.

`Sandata.Core.Tests` is 1,132 rather than 1,118: nine tests cover the three
simulation changes and the rest come from the same wave's client work.
`Sandata.Client.Tests` is 295 rather than 219, which is the theme switcher, the
unknown-contact resolver, the lowered-weapon geometry, and the automatic-fire
audio fallback.

**The Hukbo gate was run separately on the same tree and is also green** — 2,492
`Hukbo.Core.Tests` and 3,682 `Hukbo.Client.Tests`, with all three of its headless
workloads passing. Two games, two gates, two results, never reported as one.

This gate remains no evidence about anything interactive, and 2026-08-14 is what
that costs. A person at a desktop ran all three rows this gate was meant to
support. `SD-7b` passed as built. `SD-4` and `SD-5` failed for the third time
each against this very tree, and every test behind this green result was written
by the same package those two rows say did not work on screen. Both were closed
later the same day by further work, recorded below.

#### The gate after seeding the path-blocked span, 2026-08-14

`./scripts/verify.ps1 -Game Sandata`, on branch `sandata-sd4-sd5`, after the
A* search was given the baked map to read:

```
Sandata.Core.Tests     Total tests: 1135   Passed: 1135
Sandata.Client.Tests   Total tests:  320   Passed:  320
stateHash A644B7F8A394885D   eventHash AEDE4D16B5E6FAAF   deterministic true
[PASS] Canonical repository verification completed.
```

**Both hashes are unchanged, and that is the result worth reading rather than
the green.** The design predicted that seeding the path-blocked span would move
every Sandata digest, because A* had been searching a fully open grid on every
map and operators had been walking through walls. It moves none of them, because
`HeadlessRunner.BuildOpenGrid` synthesises a grid with no walls, no doors, and no
map file, so the seeded array is still every-cell-false in this workload.

The consequence is a standing limit on what this gate can say: **the seed-1
workload cannot detect a pathfinding change that only manifests around
geometry.** What proves the change is `PathBlockedCellsTests`, which searches
across a wall and asserts the returned path avoids it, and smoke row `SD-4`,
which asks a person to watch an operator funnel through a doorway.

#### The gate after the engagement exemption and the health placeholder, 2026-08-14

`SD-4` passed against the tree above. `SD-5` failed against it again, and a
driven `Debug` run found why: the whole run produced seven shot cues and every
one was the defending pistol firing single shots, because a rifleman sits inside
`LoweredWallDistanceWu` for the entire approach through `angle-house`'s corridors
and is forced lowered at the moment of contact. Two further changes followed, and
the gate was re-run on branch `sandata-engage-raise` at `7db52fa`:

```
[PASS] Release repository tests completed.
measuredTicks 10000   outcome Ongoing   survivors 70 / 64
stateHash A644B7F8A394885D   eventHash AEDE4D16B5E6FAAF   deterministic true
p50 2.3322 ms   p95 3.4453 ms   p99 3.9786 ms   max 60.9105 ms
durationMilliseconds 25714.48   allocatedBytes 6,120,477,496 (~6.12 GB)
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

**Both hashes are again unchanged**, for two reasons worth keeping apart. The
engagement exemption alters behaviour only where an operator has an identified
contact, and this workload's grid has no walls, so the lowered condition was
already false for every operator on every tick of it. The health change touches a
placeholder in the client's scenario builder that never reaches a hash at all.
`SandataRuleset.ContentHash` is untouched and no preset version was spent.

`Sandata.Core.Tests` is 1,141: six further tests cover the engagement exemption,
three at the rule and three at the simulation.

**What the gate could not tell anyone, and a driven run could.** Neither defect
was visible to any automated stage, and neither would have been found by running
the suite again. Both were found by launching the game with `HUKBO_LOG_LEVEL=trc`
and `HUKBO_LOG_CHANNELS=audio,sim`, driving it with `keybd_event`, closing the
window with `WM_CLOSE` so the log flushed, and reading the cue lines. The
measurement that preceded `SD-5` closing is eleven reports from the AK attacker
spanning 1.03 seconds at roughly 100-millisecond spacing — the weapon's 600
rounds per minute sustained for a second, where the same operator had fired
nothing at all in the run before. That is a measurement and not a smoke row: a
person still listened to it before the row moved.

The Hukbo gate was run separately on the same tree and is also green, all five
stages `PASS`, with `combatPreset 5` and `movementPreset 13`.

### Canonical gate result — Sandata, 2026-08-14 (lowered weapon and automatic fire)

`./scripts/verify.ps1 -Game Sandata -SkipBootstrap`, exit code 0, run on branch
`sandata-sd4-sd5` with all three of that package's waves integrated:

```
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
Sandata.Core.Tests     Total tests: 1135   Passed: 1135
Sandata.Client.Tests   Total tests:  320   Passed:  320
[PASS] Release repository tests completed.
measuredTicks 10000   outcome Ongoing   survivors 70 / 64
stateHash A644B7F8A394885D   eventHash AEDE4D16B5E6FAAF   deterministic true
p50 2.3588 ms   p95 2.5587 ms   p99 3.2322 ms   max 59.9452 ms
durationMilliseconds 24147.97   allocatedBytes 6,120,455,624 (~6.12 GB)
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

**Both hashes are unchanged from the 2026-08-12 baseline above, and that is the
result worth reading rather than the green.** This package's decision D1 seeds
the path-blocked span the A\* search reads from the baked map, so that a search
stops crossing walls — the design predicted it would move every Sandata digest.
It moves none of them here, because `HeadlessRunner.BuildOpenGrid` synthesises a
grid with no walls, no doors, and no map file, so the seeded array is still
every-cell-false in this workload and every search still runs on open ground.

The consequence is a limit on what this gate can ever say: **the seed-1 workload
cannot detect a pathfinding change that only manifests around geometry.** What
proves D1 is `tests/Sandata.Core.Tests/PathBlockedCellsTests.cs`, which searches
across a wall and asserts the returned path avoids it, and smoke row `SD-4`,
which asks a person to watch an operator funnel through the `angle-house`
doorway. The same reasoning is recorded at the fixture itself and at
`MissionStateTests.PreTask79cBaselineHash`, whose value was re-examined on the
same day and deliberately left where it is.

`Sandata.Core.Tests` is 1,135 rather than 1,132: three tests cover the seeded
blocked span. `Sandata.Client.Tests` is 320 rather than 295, which is the
inspector's two new rows, the burst-tracking set, and the weapon-state log line.

The Hukbo gate was run on the same tree, separately, and is also green:
`./scripts/verify.ps1 -SkipBootstrap`, all five stages `PASS`, 2,568
`Hukbo.Core.Tests` and 3,785 `Hukbo.Client.Tests`, and all four of its headless
workloads passing. Two games, two gates, two results, never reported as one. The
Hukbo figures come from `main` at `8f2207f` and nothing in this package touches
Hukbo's simulation; the only file it changes outside Sandata is an added `const`
on the shared `LogEvents` catalog.

Neither gate is evidence about anything interactive. `SD-4` and `SD-5` stay
`FAIL` until a person at a desktop re-runs them.

### Canonical gate result — Sandata, 2026-08-14 (the intent field is written)

`./scripts/verify.ps1 -Game Sandata -SkipBootstrap`, exit code 0, run on branch
`sandata-sd4-sd5` after decision D1 of the "the shipped mission freezes at first
contact" design:

```
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
Sandata.Core.Tests     Total tests: 1143   Passed: 1143
Sandata.Client.Tests   Total tests:  320   Passed:  320
[PASS] Release repository tests completed.
outcome Ongoing
stateHash 13EF0685BB46CA5E   eventHash AEDE4D16B5E6FAAF
allocatedBytes 6,187,695,224 (~6.19 GB)
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

**The state hash moved and the event hash did not, and that is the signature
this change is supposed to have.** Stage 8's selected intent is now written into
`OperatorState.Intent`, which nothing had ever done: `IntentSelection.SelectAll`
ran every tick, its results were correct, and they lived only in
`SandataSimulation.PendingIntents`, so a field that is folded into the state hash
and carried in the snapshot read `0` — `OperatorIntent.Hold` — for every operator
on every tick of every run ever simulated. Intent is state and not an event, so a
change that writes it must move exactly one of the two hashes. This is what
having two independent hashes is for.

The superseded figure is `stateHash A644B7F8A394885D` with the same
`eventHash AEDE4D16B5E6FAAF`, recorded on 2026-08-12 and re-confirmed earlier the
same day; it moves to `docs/development/measurement-history.md`. Both golden
replay fixtures were re-measured by a capture run in the same change, and their
event hashes likewise did not move.

No new `SandataPresetId`. `SandataRuleset.ContentHash` is unchanged at
`8_955_292_433_887_190_872`.

**This gate still cannot see the behaviour the change was made for.** The seed-1
workload has no walls, no objectives, and no squads walking to them, so it
reaches none of the mission freeze this package exists to fix. The wall-bearing
golden fixture that would is task 7 of that plan and is not built yet.

### Golden replay and determinism equivalence

Sandata's pinned digests live in
`tests/Sandata.Core.Tests/Fixtures/seed-1-baseline.json`, not in any `.cs` file.
Exactly one absolute state-hash literal is permitted in C# under
`tests/Sandata.Core.Tests/`, and it is already spent on
`MissionStateTests.PreTask79cBaselineHash`.

`GoldenReplayTests` pins two seed-1 baselines over eight operators and forty
ticks: one mission with an empty order stream and one with two real orders
submitted through `SandataSimulation.SubmitOrder`. Both are asserted to be
non-degenerate — each emits events including shots fired, and at least one
operator ends below full health — and the failure message names the first
mismatch tick.

The non-empty baseline's own evidence changed on 2026-08-12 and the change is
worth reading before trusting it. It used to assert that the ordered operator
still held an `OrderAssignment` after forty ticks, which was always true because
nothing ever cleared one, and which an operator that had walked nowhere
satisfied exactly as well as one that had walked its whole polyline. On this
dense 4v4 fixture every operator is inside identify range from tick 0, so the
ordered operator is halted to engage before it walks anywhere and then loses the
firefight; the assignment now clears under design section 16's third condition,
the operator's death. The test asserts that death explicitly rather than
asserting a waypoint it will never reach.

`DeterminismEquivalenceTests` adds four relational tests that pin no absolute
hash: a same-seed repeat in process, a cold-cache run whose derived structures
are all rebuilt from scratch at the midpoint, a save-and-resume round trip, and
a run with logging off compared against the same run at `trc`. Each calls a
shared activity check that requires events emitted and total health below its
starting value, so neither side can pass by standing still.

One clause of Sandata's determinism contract is **not** proven by any of these,
and it is recorded here rather than assumed discharged: design section 4's rule
that a *derived* published path polyline is recomputed from its stored request
on resume. The merged save-and-resume test snapshots an authored order, whose
polyline is stored state, so it proves the round trip and not the
recomputation. Nothing suggests the recomputation is wrong; it simply has no
test yet.

### Task 53's measurement runs, 2026-08-08

These are the figures from the third and final run of the navigation matrix,
after two real defects in the harness itself were found and fixed. The two
earlier tables are kept in the plan document as the record of those defects and
are not measurements of anything.

```
BO | Microsoft Windows 10.0.26200 (X64) | 20 logical processors | .NET 10.0.10
```

**Audio instance pool.** The 257th concurrent `SoundEffectInstance` throws
`InstancePlayLimitException`, so 256 is the usable pool.
`SandataSoundBudget.DefaultMaximumInstances` was moved from a provisional 64 to
the measured 256. Eight shooters sustaining automatic fire for ten seconds held
sixteen instances — one loop and one tail each — with fourteen tail cues fired,
zero refused, and no exception.

The constant equals the measured ceiling rather than sitting below it. That is
deliberate and it carries a precondition: this budget refuses the same 257th
reservation MonoGame would have thrown on, so no headroom is needed **only while
every played instance is reserved here first**. Every cue path on
`SandataSoundPlayer` goes through `TryReserve` before playing today. A future
play path that bypasses the budget reintroduces exactly the exception the
constant exists to prevent.

**Navigation matrix.** The `angle-house` fixture bakes to a 160-by-180-cell nav
grid. Seed 1, 2,000 ticks per row.

| Row | Density % | Changed cells | Seekers | Query wu | Replan % | Probes | Found % | Successful p50 / p95 / p99 (ms) | Stage 7 p50 / p95 / p99 (ms) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| baseline | 20 | 0 | 4 | 512 | 5 | 408 | 100.0 | 0.9376 / 1.5742 / 1.8825 | 0.0001 / 1.1056 / 1.6790 |
| many-seekers | 20 | 0 | 16 | 512 | 5 | 1,603 | 100.0 | 0.7900 / 1.5222 / 1.8724 | 0.0672 / 1.7753 / 2.8294 |
| long-queries | 20 | 0 | 4 | 2,048 | 5 | 408 | 100.0 | 1.5465 / 2.9441 / 3.2198 | 0.0001 / 2.4036 / 3.2181 |
| doors-light | 10 | 20 | 4 | 512 | 10 | 820 | 100.0 | 0.5794 / 1.2112 / 1.3738 | 0.0001 / 0.9457 / 1.3047 |
| stress-connected | 10 | 50 | 32 | 2,048 | 25 | 15,937 | 93.7 | 1.3415 / 4.8988 / 6.0748 | 3.8829 / 11.5732 / 15.9489 |

What those numbers say:

- A single A\* query over 512 world units costs well under a millisecond at p50
  and under two at p99.
- Quadrupling the query distance to 2,048 world units roughly doubles the cost,
  which is the expected shape for grid A\* over a bounded indoor map.
- Going from four concurrent seekers to sixteen barely moves p50, because the
  searches are independent. What it moves is the stage-7 total, which is their
  sum.
- **The stress row is the one worth watching.** Thirty-two seekers replanning at
  25 percent put stage 7 at 3.88 ms p50 and 15.95 ms p99. One tick at the
  ruleset's 50 Hz is 20 ms, so that row spends most of a tick budget in one
  stage. It is far past anything the design anticipates — the cost table there
  is written for sixteen operators and four groups — and it is not a
  configuration the game runs. It is recorded because a measured ceiling is
  worth more than an assumed one.
- The 6.3 percent of stress-row queries that fail are genuine: at 50 toggling
  cells and 32 seekers, some goals sit behind a cell that is blocked in the
  toggled configuration. That is a real dynamic-blocker case, which is what the
  row was written to measure.

**Where the raw capture lives, stated honestly.** Both runs wrote their raw
output under `artifacts/`, which `.gitignore` excludes, so those files exist on
the measuring workstation and in no clone of this repository. The transcript of
record is the plan document,
Sandata's archived scaffold plan, in the section titled "Task 53
complete, after tasks 82 and 83". Nothing here should be read as a citation to a
file a fresh clone can open, because there is no such file.

### The largest allocators, measured and then cut

Per-stage instrumentation at 200 operators and seed 1, over 300 measured ticks
after 50 warm-up ticks, ranked two allocation sites well above everything the
first allocation task was allowed to touch:

| Site | Bytes per simulation-tick | Shape |
| --- | --- | --- |
| `src/Sandata.Core/Navigation/LineOfSight.cs` | 1,761,332 | one `int[]` sized to the grid per call, at roughly 4,684 calls per tick |
| `src/Sandata.Core/Sensing/ContactMemory.cs` | 456,130 | one array per operator per tick |

Use this table for its **ranking**, which is unambiguous, and not for its
fractions. Those two figures sum to more than the benchmark's own per-tick
total, because the harness instrumented one simulation directly while the
benchmark figure covers two constructed simulations plus everything outside the
tick. No percentage derived from this measurement should be quoted as a result.

Task 88 cut both. Both sites sit in stage 5, and the same harness re-run after
the cut records the change per stage:

| Stage | Bytes per simulation-tick, before | After |
| --- | --- | --- |
| 5 — sensing | 2,229,069 | 187,857 |
| the whole tick | 2,371,482 | 330,245 |

Every other stage is within twenty bytes of where it was, which is the noise
floor of this instrument rather than a change. The next largest allocator is
now stage 10 at about 46,500 bytes per simulation-tick, an order of magnitude
below where stage 5 used to sit and roughly a seventh of what the whole tick
now costs.


## Pawn gait leg-motion pixel measurement, 2026-08-14 (PV-6)

No published source gives an on-screen pixel height below which drawn leg
motion stops being worth animating, and two research passes looking for one
both failed. `tests/Hukbo.Client.Tests/GaitPixelHeightTests.cs` measures the
game's own numbers instead of guessing, and pins them so the table below
cannot drift silently. Measured at commit `8ee5a51`. This is a measurement,
not a tuning pass — no gait constant was changed to produce it.

Five points on the zoom axis are covered: the two detail-tier boundaries
(`PawnGeometry`'s `MediumDetailScale` = 0.95 and `HighDetailScale` = 1.80,
PawnGeometry.cs:235-236) and the three camera stations
`ConservativePawnCullTests` already names in its own review protocol — the
camera's minimum and maximum zoom clamps, and the default-fit zoom the panel
resolves for the tracked Phase 1 render baseline's 1920x1080 arena bounds
(obtained in the test by calling `SpectatorCamera.Fit`, not by a copied
literal). For each point, drawn leg height is
`PawnGeometry.ToSize(LegLengthUnits * apparentScale)` (`LegLengthUnits` =
7.5, PawnGeometry.cs:482), where `apparentScale` is read from
`PawnGeometry.Create`'s own output rather than recomputed by hand. Peak foot
travel is `strideRatio * legHeightPx` and peak foot lift is
`liftRatio * legHeightPx`, both rounded with the same `MathF.Round` and no
floor that `PawnGeometry.BuildLeg` itself uses (PawnGeometry.cs:1745) —
unlike leg height, a foot travel or lift figure is allowed to round to zero
pixels. Stride and lift ratios are `GaitGeometry`'s own constants:
`WalkStrideRatio` = 0.32, `RunStrideRatio` = 0.60, `WalkFootLiftRatio` =
0.15, `RunFootLiftRatio` = 0.38 (GaitGeometry.cs:63,70,73,80).

| Tier boundary | Station | Gait | Apparent scale (unitless) | Leg height (px) | Foot travel (px) | Foot lift (px) |
| --- | --- | --- | --- | --- | --- | --- |
| — | Minimum-zoom station (camera zoom 0.05) | Walk | 0.72 (clamp floor — Low tier, legs do not draw) | 5 | 2 | 1 |
| — | Minimum-zoom station (camera zoom 0.05) | Run | 0.72 (clamp floor — Low tier, legs do not draw) | 5 | 3 | 2 |
| Low/Medium boundary | — | Walk | 0.95 | 7 | 2 | 1 |
| Low/Medium boundary | — | Run | 0.95 | 7 | 4 | 3 |
| — | Default-fit station (camera zoom ≈1.00787, 1920x1080 arena bounds) | Walk | ≈1.3606 | 10 | 3 | 2 |
| — | Default-fit station (camera zoom ≈1.00787, 1920x1080 arena bounds) | Run | ≈1.3606 | 10 | 6 | 4 |
| Medium/High boundary | — | Walk | 1.80 | 14 | 4 | 2 |
| Medium/High boundary | — | Run | 1.80 | 14 | 8 | 5 |
| — | Maximum-zoom station (camera zoom 12) | Walk | 2.40 (clamp ceiling) | 18 | 6 | 3 |
| — | Maximum-zoom station (camera zoom 12) | Run | 2.40 (clamp ceiling) | 18 | 11 | 7 |

**Reading this as a measurement, not a recommendation.** At every one of the
five points, neither gait's foot travel nor its foot lift rounds below 1 px —
the smallest nonzero figure recorded is 1 px, at the minimum-zoom station's
Walk foot lift and at the Low/Medium boundary's Walk foot lift. Leg motion
does not fade toward invisibility as apparent scale drops; it instead hits a
step function. `PawnGeometry.CreateLegsAndFeet` returns an empty layout at
`PawnDetailTier.Low` regardless of the leg-height figure this table computes
for that tier (design section 9's Low-tier non-occlusion guarantee), so the
minimum-zoom station's row above describes what the formula would produce,
not what is drawn — at that station the legs do not draw at all, at any
travel or lift. Confirmed directly:
`GaitPixelHeightTests.MinimumZoomStation_LandsInLowTier_WhereLegsNeverDraw`,
`DefaultFitStation_LandsInMediumTier`, and
`MaximumZoomStation_LandsInHighTier`.

## Canonical gate result — Hukbo, 2026-08-14 (isolated receipt at `8ee5a51`)

This is the receipt the lethal blow legibility package's task table asked for
and never obtained. That plan required one isolated green gate proving the
change alone left the gate green. The attempt made on the day failed at the
build stage on unrelated concurrent work, and the only green run available
afterwards bundled the lethal blow change together with cohort lateral spread
and other uncommitted work, which is strong evidence but is not the receipt the
plan asked for.

The run recorded here was made in a dedicated worktree checked out detached at
`8ee5a51`, confirmed clean beforehand — `git status --porcelain` produced no
output and `git rev-parse HEAD` returned
`8ee5a51843073fc5f1c3e1555e1cbdb7ee6e8beb`. No task from the pawn visual
fidelity package had run yet, so nothing in the tree was newer than the lethal
blow work. The command was `./scripts/verify.ps1` with no flags, so all five
stages ran, including the locked restore that `-SkipBootstrap` would have
skipped. It exited with code 0.

```
[PASS] Platform: Windows x64
[PASS] PowerShell: 7.6.4
[PASS] git version 2.55.0.windows.3
[PASS] Git LFS: installed (optional; no tracked LFS assets are currently required)
[PASS] .NET SDK: 10.0.302
[PASS] packages.lock.json present for all 21 projects.
[PASS] MonoGame packages are centrally pinned: MonoGame.Content.Builder.Task 3.8.5, MonoGame.Framework.DesktopGL 3.8.5
[PASS] Required prerequisites and repository configuration are present.
[PASS] Locked package restore completed.
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
[PASS] Release repository tests completed.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

**Stage five runs five headless workloads, not one.** Each is 200 agents,
10,000 ticks, and seed 1, and each reported `deterministic: true` with a
`firstMismatchTick` of `null`. They differ by preset pair, and a later claim
that "the hashes are unchanged" has to name which of the five it means.

| Combat preset | Movement preset | Outcome | State hash | Event hash |
| --- | --- | --- | --- | --- |
| 6 | 4 | `Faction0Victory` | `5460D13E3F7FD3E5` | `8E18ED1437B2924B` |
| 5 | 8 | `Faction1Victory` | `C8023D3B5BEB005E` | `F709A345E2F7370E` |
| 5 | 10 | `Faction0Victory` | `7C145A9E05916E4C` | `77626E104234206C` |
| 5 | 11 | `Faction0Victory` | `6225182B4A470F91` | `C4DABE6AF98B6BEC` |
| 5 | 13 | `Faction1Victory` | `4A0723BC9A1B924B` | `E0CE32CF8830A864` |

The final row is the pair the recorded seed-1 baseline elsewhere in this
document refers to.

## The interactive smoke checklist

Moved to [smoke-checklist.md](smoke-checklist.md) on 2026-08-11.

As of that date it carries 105 rows across 29 sections: 82 `PENDING`,
13 `BLOCKED`, 8 `PASS`, and 2 `FAIL`. **Only a person at an interactive desktop
may flip a row**, and no agent may, for any reason, including a passing
automated test.

Nothing in this file, and nothing the canonical gate prints, is evidence about
interactive behaviour. The gate never formats a battle event, never opens the
agent inspector, and never draws blood — on 2026-08-09 the first play session
that reached a ranged blow crashed four times while the gate stayed green in
full throughout.

## Superseded measurement records

Moved to [measurement-history.md](measurement-history.md) on 2026-08-11: 3,556
lines covering the VIS-036 render matrix and its Phase 1 and Phase 2 baselines,
both agent-count scaling sweeps, the movement-preset default flips, the T32
weapon balance measurements, the attack-combination results, and four
superseded collision runs.

They were moved rather than archived deliberately. `docs/archives/` carries a
"never cite this as a reason to do something" rule, and several of those runs
are the evidence a live constraint still rests on — the render baselines are
what the arena quad budget in `Hukbo.Client/Rendering/SubmissionCount.cs` is
argued against.

Read one for what it measured, not for what is true now. Figures, ceilings, and
file paths drift, and at least two records there are known to be stale.

## Failure classification

Classify failures as implementation, test, environment/dependency, pre-existing,
incorrect assumption, unrelated, or flaky. Make the narrowest correction, rerun
the focused check, and expand only after it passes.
