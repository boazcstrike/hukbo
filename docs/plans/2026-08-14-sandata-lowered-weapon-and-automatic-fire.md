# Sandata: making the lowered weapon and automatic fire observable — plan

The ordered task list for
[`2026-08-14-sandata-lowered-weapon-and-automatic-fire-design.md`](2026-08-14-sandata-lowered-weapon-and-automatic-fire-design.md),
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
