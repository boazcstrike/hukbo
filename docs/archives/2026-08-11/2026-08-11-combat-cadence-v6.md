# Combat cadence V6 — plan

> **Archived: reference only.** Finished work, kept so a past decision can be
> traced to its reasoning. Never execute it, never treat it as current, and never
> cite it as justification for a change. The live contract is `CLAUDE.md`,
> `SIMULATION-GAME-STANDARDS.md`, `docs/development/testing.md`, and `docs/plans/`.
>
> All twelve tasks are done and all twelve `CL-*` smoke rows `PASS`. The design
> document is **not** archived — it is cited by path from `CombatIdentity.cs`,
> `PhilippineCombatPresetV6.cs`, `Scenario.cs`, and `DeterminismTests.cs`, and
> stays live at
> [`../../plans/2026-08-11-combat-cadence-v6-design.md`](../../plans/2026-08-11-combat-cadence-v6-design.md).

Design: [2026-08-11-combat-cadence-v6-design.md](../../plans/2026-08-11-combat-cadence-v6-design.md).
The design document outranks this one on intent; this one owns the ordered task
list and the verification criteria.

**Base commit:** `0c3f7f2`, rebased twice and finally onto `main` at `817c900`
**Branch:** `combat-cadence-v6`, merged to `main` at `982bd6f`

## Ordering rule

Tasks 1 through 4 build the preset and prove it in isolation. Task 5 is a
measurement and it is a gate: **the default is not flipped until task 5 has
produced a real number.** Tasks 6 through 9 are the flip and its consequences,
and they are skipped entirely if task 5 says the termination behaviour regressed
— in that case V6 ships registered and opt-in, exactly as V9 movement did, and
the fact is recorded rather than worked around. Task 10 is independent of all of
the above and may run in parallel with any of them.

## Tasks

### 1. Append the preset identifier

**Files:** `src/Hukbo.Core/Combat/CombatIdentity.cs`

Add `PrecolonialPhilippinesV6 = 6` after `PrecolonialPhilippinesV5 = 5`, with an
XML doc comment in the style of its five neighbours: state that it restates V4's
tables and retunes only the melee attack cooldown, combo cooldown, and damage,
that it holds damage per tick within two per cent of V4's, and that V1 through
V5 stay registered and unmodified so their replays remain reproducible.

**Verification:** the solution builds. `CombatPresetRegistry` will not compile
until task 3, so tasks 1 and 3 land together.

### 2. Add the preset

**Files:** `src/Hukbo.Core/Combat/PhilippineCombatPresetV6.cs` (new)

Restate `PhilippineCombatPresetV4` exactly — the target-weight profiles, the
armour table, the rank table, the roster, and the clash resolution table are
copied without modification. The only differences are the six melee loadouts:

| Weapon and grip | Damage | Cooldown | Combo cooldown |
| --- | --- | --- | --- |
| Kampilan (two-handed) | 26 | 12 | 7 |
| Wasay (two-handed) | 32 | 14 | 9 |
| Kalis (solo) | 22 | 10 | 6 |
| Kalis (shielded) | 20 | 10 | 6 |
| Itak (solo) | 20 | 9 | 5 |
| Itak (shielded) | 18 | 9 | 5 |

`comboOpenChanceBasisPoints`, `comboContinueChanceBasisPoints`, and
`comboMaxSteps` are copied from V4 unchanged.

A comment above the table records that every value is a **Provisional
reconstruction** gameplay-tuning value under CLAUDE.md section 7, that the
retune exists to halve the on-screen artefact rate for the CL-1, CL-3, and CL-7
smoke failures, and that damage per tick was held near-constant on purpose. It
must not read as a historical measurement.

**Verification:** the solution builds; task 4's tests pass.

### 3. Register it

**Files:** `src/Hukbo.Core/Combat/CombatPresetRegistry.cs`

Add the `IsRegistered` arm and the `Get` arm, following the five existing ones.

**Verification:** `CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV6)`
returns a ruleset rather than throwing.

### 4. Pin the preset

**Files:** `tests/Hukbo.Core.Tests/CombatCadenceV6Tests.cs` (new)

Four things get tested, and the fourth is the one that matters:

1. V6 is registered and resolvable, and the enum value is exactly 6.
2. The six loadouts carry exactly the damage, cooldown, and combo cooldown in
   task 2's table.
3. Every non-cadence table is equal to V4's — the roster, the ranks, the armour
   table, the clash table, and the target-weight profiles. This is what proves
   the preset is a cadence change and not an accidental rewrite.
4. **The damage-per-tick invariant.** For each of the six loadouts,
   `|V6.damage / V6.cooldown − V4.damage / V4.cooldown|` is within two per cent
   of the V4 value, computed in integer arithmetic by cross-multiplication
   rather than floating point. This is the assertion the whole design rests on,
   and it is the one that fails loudly if somebody later edits a damage number
   without editing its cooldown.

**Verification:** `./scripts/test.ps1 -Configuration Release`, all four pass.

### 5. Measure before flipping — this task is a gate

**Files:** none. This task produces a number, not a diff.

Run the headless workload against V6 and against V4 over the same seed set and
compare how many battles reach a decision rather than running to the tick cap:

```powershell
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1 -Preset PrecolonialPhilippinesV6
```

repeated across seeds 1 through 20, and the same twenty against
`PrecolonialPhilippinesV4` as the control. Record both decisive counts and both
median decision ticks in this document under a new "Measured" section.

**The bar:** V6's decisive-seed count is greater than or equal to V4's, and
V6's median decision tick is within twenty per cent of V4's. Damage per tick is
near-constant by construction, so this is expected to hold; the task exists
because "expected" is not "measured", and because the movement V6 default flip
was blocked by exactly this class of surprise.

**If the bar is missed:** stop. Do not retune to chase it, and do not proceed to
tasks 6 through 9. Record the measured numbers, leave the shipped default on V4,
and report the gap. V6 stays registered and opt-in.

#### Measured, 2026-08-11

Forty runs — twenty seeds against each preset — at 200 agents, a 10,000-tick
cap, and the default movement preset `PersistentContingentsV4`, on
Microsoft Windows 10.0.26200 x64, `Release`, worktree branch
`combat-cadence-v6`:

```
PrecolonialPhilippinesV4: decisive 20/20  medianDecisionTick 1668  minTicks 939  maxTicks 2249
PrecolonialPhilippinesV6: decisive 20/20  medianDecisionTick 1651  minTicks 885  maxTicks 2238
```

Re-measured after the branch was rebased onto `main` at `817c900`, the
battlefield realism merge, because that merge changed `BattleSimulation`, the
retreat rules, and `AgentIntent` underneath this branch. **Every figure above is
unchanged**, which is itself the useful result: battlefield realism's behaviour
sits behind `MovementPresetId.BattlefieldRealismV10`, and this sweep runs the
shipped `PersistentContingentsV4`, so the two changes do not interact.

**The bar is met.** V6 decides every one of the twenty seeds, exactly as V4
does, and its median decision tick is 1.0 per cent *faster* rather than slower —
seventeen ticks, which is under a second of simulated time and well inside the
twenty per cent band. The range moves in the same direction at both ends. This
is the result the near-constant damage-per-tick constraint was designed to
produce, and it is now measured rather than assumed.

Tasks 6 through 9 are therefore authorised.

### 6. Flip the shipped default

**Files:** `src/Hukbo.Core/Simulation/Scenario.cs`

Change `CombatPreset`'s initialiser from `PrecolonialPhilippinesV4` to
`PrecolonialPhilippinesV6` and extend the doc comment above it to record the
flip, its date, and the smoke rows that motivated it, in the same style the
existing comment uses for the V2-to-V4 history.

**Verification:** `ScenarioTests` — updated in task 7 — passes.

### 7. Update every default-preset assertion

**Files, all in `tests/`:**

- `Hukbo.Core.Tests/ScenarioTests.cs:24`
- `Hukbo.Core.Tests/Movement/ItakMovementScenarioTests.cs:334`
- `Hukbo.Core.Tests/Movement/TallHardwoodMovementScenarioTests.cs:1444`, `:1587`, `:1593`
- `Hukbo.Core.Tests/Movement/WasayMovementTests.cs:846`
- `Hukbo.Core.Tests/MovementPresetFreezeTests.cs:426` (comment only)
- `Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs:545` (comment only)

Every one of these asserts or describes "the shipped default is V4". Each is
re-pointed at V6. **A test that names V4 explicitly rather than as "the default"
is left alone** — `DeterminismTests` at `:198`, `:229`, `:261`, and `:361`, the
two `Hukbo.Client.Tests` composition tests, `KalisMovementScenarioTests:380`,
and `BattleSimulationTests:1740` all pin V4 deliberately and must keep passing
against the unmodified V4, which is the proof that V4's replays still reproduce.

Distinguishing the two cases is the whole of this task; read each call site
rather than running a replace.

**Verification:** `./scripts/test.ps1 -Configuration Release`, both suites green.

### 8. Update the gate script's comment

**Files:** `scripts/verify.ps1`

Line 46 says the shipped default "stays on PrecolonialPhilippinesV4". After the
flip that is false. Update the prose; the parameterless first benchmark
invocation itself does not change, because it deliberately runs whatever the
default is.

`ScriptDefaultsTests` pins only the `Preset = 'PrecolonialPhilippinesV5'` line
of the second block, so this edit does not turn the Client suite red — but run
that suite anyway, because a `scripts/*.ps1` edit is exactly the change that
has turned it red before.

**Verification:** `./scripts/test.ps1 -Configuration Release -Game Hukbo`.

### 9. Re-measure and record the seed-1 baseline

**Files:** `docs/development/testing.md`

The default-workload state hash, event hash, and `combatPreset` field all move.
Add a new dated "Canonical gate result — Hukbo" section with the real output;
move the 2026-08-11 block into the superseded-measurements section rather than
overwriting it, following the pattern the Sandata baselines already use.

The second workload — explicit `PrecolonialPhilippinesV5` and
`RangedStandoffV8` — must print **byte-identical** hashes to the recorded
baseline. If it does not, something reached V5 and the change is wrong; stop and
find it rather than recording the new number.

**Verification:** `./scripts/verify.ps1 -SkipBootstrap`, exit code 0, real
output pasted.

### 10. The 4x half of CL-7 — presentation only

**Files:** `src/Hukbo.Client/Presentation/AttackAnimationSystem.cs`,
`tests/Hukbo.Client.Tests/` (new or extended attack-animation test)

`Advance` ages every animation by `elapsedSeconds * speedMultiplier`
(`AttackAnimationSystem.cs:92`), so at 4x an Itak swing is drawn for between two
and three frames. Clamp the factor the animation ages by so that it grows more
slowly than the playback speed — the simplest defensible form is a ceiling,
so that above some multiplier the swing stops compressing further and simply
overlaps the next one less exactly.

This is presentation only. It must not touch `Hukbo.Core`, must not change
either hash, and must not depend on the wall clock beyond the frame delta the
system already receives. Pick the ceiling, state it as a provisional
presentation constant, and test that the aged value is bounded above regardless
of the multiplier passed in.

**Verification:** `./scripts/test.ps1 -Configuration Release`; the seed-1
digests in task 9 unchanged by this task alone.

### 11. Re-open the smoke rows

**Files:** `docs/development/smoke-checklist.md`

Split CL-7 into CL-7a (at 1x, the swing reads as one countable action with
visible rest either side) and CL-7b (at 4x, the swing is still drawn long enough
to read as one action). Set CL-1, CL-3, CL-7a, and CL-7b to `PENDING` with the
2026-08-11 `FAIL` observations preserved in the `Actual` column as the prior
result, and note the commit the failures were observed at.

**No agent may flip any of these four to `PASS`.** A person at an interactive
desktop does that, and only after watching a battle. Compilation, a green suite,
and a green gate are not evidence about any of them.

### 12. Run the canonical gate and record it

`./scripts/verify.ps1 -SkipBootstrap`, once, after everything is integrated.
Paste the real output into task 9's section. A sub-agent's report does not
substitute for it.

## Status, 2026-08-11

All twelve tasks are done. Task 5's gate was measured and passed, so tasks 6
through 9 were authorised rather than skipped.

| Task | State | Evidence |
| --- | --- | --- |
| 1. Append the identifier | Done | `CombatPresetId.PrecolonialPhilippinesV6 = 6` |
| 2. Add the preset | Done | `src/Hukbo.Core/Combat/PhilippineCombatPresetV6.cs` |
| 3. Register it | Done | Both `CombatPresetRegistry` arms; `WeaponProfileTests.EveryCombatPresetIdIsRegistered` sweeps it |
| 4. Pin the preset | Done | `CombatCadenceV6Tests`, 22 tests |
| 5. Measure before flipping | Done, **bar met** | 20/20 decisive both presets; median 1,651 against 1,668 |
| 6. Flip the default | Done | `Scenario.CombatPreset` |
| 7. Update default assertions | Done | Four re-pointed, four left on V4 deliberately — see below |
| 8. Gate script comment | Done | `scripts/verify.ps1` |
| 9. Re-measure the baseline | Done | `docs/development/testing.md`, new dated block |
| 10. The 4x half of CL-7 | Done | `AttackAnimationSystem.MaximumAnimationSpeedMultiplier`, 8 new tests |
| 11. Re-open the smoke rows | Done | CL-1, CL-3, CL-7a, CL-7b all `PENDING` |
| 12. Run the canonical gate | Done | Exit code 0 on the rebase onto `817c900`; output in `docs/development/testing.md` |

The gate now runs **three** headless workloads rather than the two this plan was
written against — battlefield realism added a `BattlefieldRealismV10` block on
2026-08-11. Both of the explicitly-preset workloads printed their recorded
baselines unchanged, so there are now two independent leak detectors proving V6
is a new preset rather than an edit of V4, where the plan assumed one.

**Task 7 in detail, because reading each call site was the task.** Four sites
meant "the shipped default" and were re-pointed at V6: `ScenarioTests:24`,
`ItakMovementScenarioTests:334`, `TallHardwoodMovementScenarioTests:1444` and
`:1587`. Two comment-only sites had their prose corrected. The rest were left
on V4 on purpose:

- **`WasayMovementTests`** names V4 explicitly throughout because the movement
  matrix is calibrated against V4's cadence. Following the default would have
  silently recalibrated every solo cell against doubled cooldowns. Only the
  stale "the current default" comment was corrected.
- **`MovementStateHashTests`** needed the opposite fix from the one the task
  list anticipated. Its `TheVSixFoldOrderIsPinned` literals moved, because
  `StateHasher.Compute` folds `(int)scenario.CombatPreset` and the default's
  numeric value went from 4 to 6. The literals were **not** recaptured: the
  file tests fold *order*, not which preset ships, so its scenario builder now
  pins V4 and both original literals pass unchanged. That the literals came
  back byte-identical is the proof the diagnosis was right rather than a
  re-pin.
- **`DeterminismTests.PersistentContingentsV2_SeedOneStateAndEventHashArePinned`**
  is the genuine recapture, and its own doc comment authorises it — that Fact
  leaves `--preset` unnamed precisely so it tracks the shipped default. New
  pair `DB25EB02805721BC` / `6F1A64795B7C8E96`; the superseded pairs are listed
  in the comment above the assertions, as every earlier recapture is.

Two failures that looked identical therefore had opposite correct
dispositions, and neither was resolved by making a red test green.

## Verification criteria for the package

- V6 is registered, resolvable, and pinned by task 4's four tests.
- V4 and V5 are byte-for-byte unmodified, and the explicit-V5 gate workload
  prints its recorded baseline hashes unchanged.
- Task 5's decisive-seed measurement is recorded with real numbers, whether it
  passed the bar or not.
- Both test suites green, `TreatWarningsAsErrors` intact, no analyzer or test
  weakened to get there.
- `./scripts/verify.ps1 -SkipBootstrap` exit code 0, output pasted into
  `docs/development/testing.md`.
- CL-1, CL-3, CL-7a, and CL-7b are `PENDING` and remain so until a person runs
  them.

## Follow-ups, deliberately not in this package

- **A cadence retune of the ranged preset.** `PhilippineCombatPresetV5` keeps
  V4's dense melee timing. If the ranged preset is ever flipped to default, it
  needs the same treatment, as a V7.
- **The combo chain's open chance and maximum step count.** Section 3.3 of the
  design deferred these. They are the next lever if a factor of two turns out
  not to be enough, and they are not to be pulled pre-emptively in this change.
- **Whether the event feed needs per-resolution colour or an icon.** Halving the
  line rate may be sufficient for CL-1 on its own. Measure with a person before
  building anything.
