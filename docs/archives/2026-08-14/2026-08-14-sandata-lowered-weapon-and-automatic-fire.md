# Sandata: making the lowered weapon and automatic fire observable — plan

**Archived: reference only.** This is finished work, kept only so a past
decision can be traced to its reasoning. Never execute it, never treat it as a
live task list, and never cite it as the reason to make a change. The live
contract for this project remains `CLAUDE.md` and Sandata's own scaffold design
document.

The ordered task list for
the archived 2026-08-14 Sandata lowered-weapon and automatic-fire design document,
whose decisions D1 through D5 bind every task below. Opened 2026-08-14 after the
third interactive session failed `SD-4` and `SD-5`.

**Verification package as a whole:** `./scripts/verify.ps1 -Game Sandata` and
`./scripts/verify.ps1` both green, with real output pasted into
`docs/development/testing.md`. A green default gate says nothing about Sandata
and a green Sandata gate says nothing about Hukbo, so both are run and both are
recorded.

**No task below may flip a smoke row.** `SD-4` and `SD-5` stay `FAIL` until a
person at a desktop says otherwise. A fixed row returns to `PENDING`, never
straight to `PASS`, and keeps its failing observation in `Actual` so the re-run
is judged against what was actually seen.

**Task 1 was expected to move the state hash and does not.** Every pinned
fixture in the Sandata suite runs on a wall-free grid built by
`HeadlessRunner.BuildOpenGrid`, so seeding the path-blocked span from a map's
passability changes nothing any of them measures. Section 6 of the design
records what that says about the fixtures. Wave 3 below is therefore much
smaller than it was written to be, and the tasks that shrank are marked.

## Wave 1 — two independent fixes, run in parallel

| # | Task | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| 1 | D1. Seed the path-blocked span from the baked map once at construction: a cell `NavGrid.Passability` marks `Blocked` is blocked for the search, a `Door` cell stays passable. Rewrite the field's doc comment and `AdvancePathService`'s `PROVISIONAL` remark, both of which currently assert the span is permanently all-`false` | `src/Sandata.Core/Simulation/SandataSimulation.cs` | A path request across a wall routes around it; the array is still written exactly once and never per tick | — | New test file under `tests/Sandata.Core.Tests`: a search whose straight line crosses a wall returns a path with no blocked cell on it; a search through the `angle-house` fixture's closed door aperture succeeds and passes through the door cells; a fully open grid returns the same path as before this change |
| 2 | D4 and D5, inside the audio layer only. Give `SandataSoundPlayer` an explicit burst-end grace window instead of ending a burst on the first quiet tick, sized from the slowest cyclic rate in `FirearmCatalog` with margin and named as a constant. Stop clearing `_loopFallbackShooters` on a burst end that has not happened. Declare the `GunLoop` and `GunTail` rows for every caliber family the catalog knows, closing the `KeyNotFoundException` reachable from `FindWithFallback` | `src/Sandata.Client/Audio/SandataSoundPlayer.cs`, `src/Sandata.Client/Audio/SandataSoundCatalog.cs`, `src/Sandata.Client/Audio/ShotSlotResolver.cs` | A sustained burst plays one report per round for its whole length, not one report total; a pistol caliber resolves an automatic slot instead of throwing | — | `tests/Sandata.Client.Tests/AutomaticFireAudioTests.cs`: a ten-round burst at five ticks per round produces ten reports; the existing `AutomaticFire_AfterTheBurstEnds_AttemptsTheLoopAgainOnTheNextBurst` still passes; a new test asserts every caliber family resolves both automatic slots without throwing |

Task 2 defines the method the client will call at the end of each tick. Name it
and document it as public surface, because task 3 calls it and the two agents do
not share a file.

## Wave 2 — the client, single owner, after wave 1

| # | Task | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| 3 | D2. Two new operator-inspector rows for the selected operator: the firearm it carries, and whether its weapon is lowered or raised. The firearm row names the weapon and its class. The state row reads `OperatorState.WeaponLowered` directly and updates live | `src/Sandata.Client/UI/**` | Selecting either walking operator says which weapon it carries; the state row changes for the rifle operator and never changes for the pistol operator | — | `tests/Sandata.Client.Tests`: pure-helper tests over the row content for a rifle operator lowered, a rifle operator raised, and a pistol operator. No `GraphicsDevice`, no `SpriteBatch` |
| 4 | D3. One new `LogEvents` constant for the weapon transition, written from the client when it observes `MissionEventKind.WeaponLowered` or `WeaponRaised`, at `dbg`, carrying the operator and the new state. The level check runs before any payload value is touched | `src/Hukbo.Diagnostics/LogEvents.cs`, `src/Sandata.Client/SandataGame.cs` | A `Debug` run's `.jsonl` carries the transition; a `Release` run carries nothing | 2 | The debug-logging boundary tests already in the suite stay green; a new test asserts the disabled call allocates nothing |
| 5 | D4's client half. Call task 2's burst-end method at the end of each tick instead of ending a burst whenever a tick carried no automatic round from that shooter | `src/Sandata.Client/SandataGame.cs` | `HandleAutomaticFireStopped` is reached once per real burst rather than four times per round | 2 | `tests/Sandata.Client.Tests`: a simulated five-tick round gap does not end the burst; a gap longer than the grace window does |

Tasks 3, 4, and 5 all land in the client and two of them share
`SandataGame.cs`, so they go to **one** agent in one pass. Splitting them across
parallel agents would be a merge conflict created on purpose.

## Wave 3 — re-measurement, not delegated

| # | Task | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| 6 | **Not owed.** Task 1 left every golden fixture bit-identical, because they all run on a wall-free grid. No re-measurement, and no fixture is edited | — | Confirmed by `GoldenReplayTests` passing unchanged | 1 | The full Core suite green with no fixture in the diff |
| 7 | **Not owed**, for the same reason. `MissionStateTests.PreTask79cBaselineHash` still describes what it always did | — | — | 6 | Its own test passing unchanged |
| 8 | Run both gates once each and record the real output. The seed-1 baseline is unchanged, so nothing moves to measurement history | `docs/development/testing.md` | Real gate output pasted, not summarised | 1, 2, 3, 4, 5 | `./scripts/verify.ps1 -Game Sandata` and `./scripts/verify.ps1`, both run once, both pasted |
| 9 | Record the fixture gap the design's section 6 found: every Sandata determinism fixture and the gate's own headless workload run on a wall-free grid, so no pinned digest has ever executed against a real map | `docs/plans/TODO.md` | The gap is written down as parked work with the decision that parked it | 1 | The entry names the design document that found it |

## Wave 4 — added after the rows were re-run, 2026-08-14

`SD-4` passed against waves 1 to 3. `SD-5` failed again, and a driven `Debug` run
with the audio channel at `trc` measured the cause: seven shot cues in the whole
run, every one the defending pistol firing single shots, and neither attacker
firing once.

| # | Task | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| 10 | D6. An operator engaging a hostile it identified this tick is not forced lowered. `IsForcedLowered` takes the new condition and early-outs on it beside the exempt-weapon flag; `AdvanceWeaponChain` runs target acquisition before computing `forceLowered` so the condition is known. `raiseRequested` alone is not sufficient — an operator with no identified contact still lowers | `src/Sandata.Core/Combat/WeaponLoweredRules.cs`, `src/Sandata.Core/Simulation/SandataSimulation.cs` | A rifleman in a corridor with an identified hostile fires; the same rifleman with no contact stays lowered | 1 | `WeaponLoweredRulesTests` extended for the wall branch and the door branch separately; new `WeaponLoweredEngagementTests` at simulation level; `SandataRuleset.ContentHash` unmoved |
| 11 | D7. The placeholder roster's health goes from 100 to 300, as a named constant whose doc comment records that it is provisional tuning rather than a measurement, and why it was raised | `src/Sandata.Client/SandataGame.cs` | A burst runs long enough to hear as a burst | 10 | A driven `Debug` run at `trc` on the audio channel, with the round timings read out of the log |

Neither task moved a hash. Task 10 changes behaviour only where an operator has
an identified contact, and every pinned fixture runs on a wall-free grid where
`forceLowered` was already always false. Task 11 touches a client scenario value
that reaches no hash at all.

**Measured result**, from the driven run after both: eleven reports from the AK
attacker at `ms` 14106, 14205, 14304, 14421, 14521, 14621, 14721, 14838, 14938,
15038, and 15138 — eleven rounds over 1.03 seconds, about 100 milliseconds apart,
which is the AK's 600 rounds per minute. The same operator fired nothing at all
in the run before these two tasks.

That is measurement, not a smoke row. `SD-5` stays `FAIL` until a person listens
to it.

## What was run, 2026-08-14

Every task above is done and integrated on branch `sandata-sd4-sd5`, which is
`main` at `8f2207f` plus the three merges below. It is **not on `main` yet**:
another session held uncommitted work across the main checkout for the whole of
this session, including `CLAUDE.md` and `AGENTS.md`, which are two of the files
this package edits. Merging into a tree somebody else is editing is how a merge
conflict gets created on purpose, so the integration was done here instead and
the merge to `main` is the one step still outstanding.

| Wave | Task | Merge | Result |
| --- | --- | --- | --- |
| 1 | 1 — seed the path-blocked span | `78e512e` | `PathBlockedCellsTests` added; the whole Sandata core suite green at 1,135 |
| 1 | 2 — burst-end grace window, automatic rows for every caliber | `1a8062e` | Sandata client suite green at 305 after four failures were resolved, below |
| 2 | 3, 4, 5 — inspector rows, log line, client burst tracking | worked directly on this branch | Sandata client suite green at 320 |
| 3 | 6, 7, 8 — re-measurement | worked directly on this branch | Both gates green; no digest moved |

**Task 2 arrived red and the four failures were each a decision rather than a
typo.** They are recorded because three of them are the kind of failure that
looks like a test being in the way:

| Failure | What it actually was |
| --- | --- |
| `SandataAudioCatalogSourceDeclaresNoDictionary` | The new per-shooter last-round map was a `Dictionary`, which the audio folder's own hygiene test forbids. Rewritten as a flat immutable array scanned linearly, which is what the loop-fallback state next to it already does |
| `ShotSlotResolverTests.AutoModeForAPistolCaliberHasNoDeclaredRowAndThrows` | A test that pinned the latent crash D5 exists to close. Rewritten to assert the pistol caliber resolves its own row, with the supersession written into its doc comment rather than the test deleted |
| `SandataSoundBudgetTests.StoppingAutomaticFirePlaysExactlyOneTailInstance` | Reported the stop one tick after the last round, which under D4 is a quiet tick inside the burst and is now correctly a no-op. The stop moved clear of the grace window and the reason is in the test's remarks |
| `SoundManifestTests.TotalVariantFileCountIsFiveHundredForty` | D5's extra rows take the catalog from 106 rows and 540 hypothetical variant files to 114 and 572. Re-pinned, and the two documents quoting the old totals corrected. Declaring a row generates no file and authorizes no spend |

**Task 5 turned out to be load-bearing for task 2, not merely paired with it.**
The design describes the client as reporting a stop on every quiet tick, and it
did not: it reported once, on the first quiet tick, and then dropped the shooter
from its tracking set. A grace window on its own would therefore have swallowed
that single report and never played a tail at all — strictly worse than the
defect. `HandleAutomaticFireStopped` now answers whether it ended a real burst,
and `AutomaticBurstTracking` keeps a shooter in the mid-burst set until it says
yes. The window stays in one place and the client's job is to keep asking.

**Tasks 6 and 7 found the opposite of what they were written to expect.** The
design's section 6 said D1 moves every Sandata digest and that both golden
fixtures must be re-measured. Neither moved, and neither needed re-measuring:
the seed-1 workload and both golden fixtures are built by
`HeadlessRunner.BuildOpenGrid`, which synthesises a grid with no walls, no
doors, and no map file, so the array D1 seeds is still every-cell-false there.
The finding is recorded in three places rather than as a passing test nobody
reads — at the fixture, at `MissionStateTests.PreTask79cBaselineHash`, and in
`docs/development/testing.md` — because it is a standing limit on the gate:
**the seed-1 workload cannot detect a pathfinding change that only manifests
around geometry.**

The gates, both run on this branch, both pasted in full in
`docs/development/testing.md`:

- `./scripts/verify.ps1 -Game Sandata -SkipBootstrap`, exit 0. 1,135 core and
  320 client tests. `stateHash A644B7F8A394885D`, `eventHash AEDE4D16B5E6FAAF`,
  `deterministic true`, 70 and 64 survivors, `allocatedBytes` 6,120,455,624 —
  the same two hashes the 2026-08-12 baseline records.
- `./scripts/verify.ps1 -SkipBootstrap`, all five stages `PASS`. 2,568
  `Hukbo.Core.Tests` and 3,785 `Hukbo.Client.Tests`, four headless workloads.

**No smoke row was touched.** `SD-4` and `SD-5` remain `FAIL` with their failing
observations intact, which is where they stay until a person at a desktop
re-runs them.

## What is handed back rather than built

Section 4 of the design leaves one question open on purpose: whether a burst
should be able to last longer than four rounds. That needs a change to operator
health or to per-caliber damage, both of which are placeholder values and both of
which are balance decisions rather than defect repair. It is not in this task
list. After wave 2, a four-round burst plays four reports in three tenths of a
second — audibly a burst rather than a single shot — and whether that satisfies
`SD-5`'s ear is a judgement only the person re-running the row can make.

`SD-5`'s own wording is also on the table. It asks for sustained fire "from the
maximum operator count", and the shipped mission has four operators with no
scenario selector. The row and the build have drifted apart. Rewriting the row is
a decision for the person who owns the checklist, not for this package.
