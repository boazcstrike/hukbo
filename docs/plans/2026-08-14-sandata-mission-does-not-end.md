# Sandata: the mission freezes at first contact — plan

Design: [2026-08-14-sandata-mission-does-not-end-design.md](2026-08-14-sandata-mission-does-not-end-design.md),
which binds every decision below. Branch `sandata-sd4-sd5`, continuing from the
lowered-weapon and automatic-fire package rather than branching beside it,
because that package's decision D1 is what made this behaviour reachable and its
merge to `main` is still outstanding.

**Verification package, whole:** `./scripts/verify.ps1 -Game Sandata` and
`./scripts/verify.ps1` both green, both pasted into `docs/development/testing.md`
as two results and never as one. **No task below may flip a smoke row.**

## Standing rules

1. `TreatWarningsAsErrors` is on repo-wide. Do not weaken a test, a warning, or
   an analyzer to get green.
2. No new package. No `Dictionary<`, `HashSet<`, `PriorityQueue<`, `float`,
   `double`, `System.Random`, `Math.Sqrt`, or `Math.Atan2` anywhere under
   `src/Sandata.Core`.
3. Every hash a task moves is **re-measured by running a capture**. A fixture
   edited by hand to agree with the code proves only that somebody edited it.
4. Another session is fixing smoke row `SD-5` and owns
   `src/Sandata.Client/Audio/**` and the audio half of `SandataGame`. No task
   here touches either.

## Wave 1 — settle the one open question

| # | Task | Files | Verification |
| --- | --- | --- | --- |
| 1 | **DONE 2026-08-14.** Answer design 2.3: why entity 2 never identifies entity 4 at 88 world units. **Neither offered candidate was right — there is a wall in the way and the sensing layer is correct.** `WALL 420 60 420 120` and `WALL 420 160 420 200` leave a 40-unit aperture into the objective room; the survivor rests at `(412, 119)`, one unit north of it and behind the wall, while its squadmate fell at `(421, 120)`, inside it. Four tests pass: no line of sight from the survivor's position, line of sight from the squadmate's, both well inside identify range so range is not the difference, and the aperture's own coordinates pinned. **This collapses 2.3 into 2.4 and drops task 4.** | `tests/Sandata.Core.Tests/ContactAfterHaltTests.cs`, `docs/plans/2026-08-14-sandata-mission-does-not-end-design.md` | `dotnet test --filter ContactAfterHaltTests`: `Failed: 0, Passed: 4`. |

## Wave 2 — state and engagement

| # | Task | Files | Verification |
| --- | --- | --- | --- |
| 2 | D1: stage 8 writes each selected intent into `OperatorState.Intent`. | `src/Sandata.Core/Simulation/SandataSimulation.cs`, `tests/Sandata.Core.Tests/IntentStateTests.cs` | Tests: an operator following a path carries `Advance` in state, not only in `PendingIntents`; a dead operator carries `Dead`; the stored value equals the pending value for every operator on every tick of a twenty-tick run; a reflection test proves the field is folded into the state hash and that two states differing only in `Intent` hash differently. |
| 3 | D2: a dead contact is dropped from contact memory on the tick it dies, and an operator whose best contact is gone re-selects. | `src/Sandata.Core/Sensing/ContactMemory.cs`, `src/Sandata.Core/Simulation/SandataSimulation.cs`, `tests/Sandata.Core.Tests/RetargetOnDeathTests.cs` | Tests: a shooter with two hostiles in range re-targets the survivor on the tick the first dies; a shooter with no remaining contact leaves `Engage` within one tick; the weapon chain returns to its resting phase rather than cycling; and no `ShotFired` is ever emitted against a dead subject. |
| 4 | **DROPPED 2026-08-14.** This row existed to fix whichever sensing candidate task 1 found. Task 1 found neither: the survivor is behind a wall and correctly sees nothing. There is no sensing defect to repair, and the behaviour this row was written to explain is repaired by task 5 instead. Recorded as dropped rather than deleted, so a reader of the design's section 2.3 can see the row it produced and what became of it. | none | none |

## Wave 3 — the squad and the ending

| # | Task | Files | Verification |
| --- | --- | --- | --- |
| 5 | D3: a group whose membership changes re-requests its path from the surviving leader's cell to the same goal, through the ordinary authoritative request record. | `src/Sandata.Core/Simulation/SandataSimulation.cs`, `src/Sandata.Core/Navigation/PathService.cs`, `tests/Sandata.Core.Tests/SquadRepathTests.cs` | Tests: killing a two-operator group's leader produces exactly one new request within one tick and the survivor resumes walking; the new path becomes valid on exactly `requestTick + PathLatencyTicks`; a group that loses nobody submits nothing; and a save-and-resume across the re-request reproduces the identical polyline. |
| 6 | D4: `OutcomeRules` resolves a mission that can no longer progress, by the predicate task 1's answer allows. | `src/Sandata.Core/Combat/OutcomeRules.cs`, `src/Sandata.Core/Simulation/SandataSimulation.cs`, `tests/Sandata.Core.Tests/StalemateOutcomeTests.cs` | Tests: the shipped four-operator mission reaches an outcome within a pinned tick count rather than running forever; a mission with a live engagement does not resolve early; and the outcome is decided only after every death of the tick, matching `DamageResolution`'s existing ordering. |

## Wave 4 — make the gate able to see any of this

| # | Task | Files | Verification |
| --- | --- | --- | --- |
| 7 | A wall-bearing golden replay fixture. A third baseline built on `angle-house` rather than on `HeadlessRunner.BuildOpenGrid`, with its per-tick state hashes and final event hash measured by a capture run and recorded in the fixture JSON. | `tests/Sandata.Core.Tests/Fixtures/seed-1-baseline.json`, `tests/Sandata.Core.Tests/GoldenReplayTests.cs` | The new baseline is asserted non-degenerate — the squad crosses the doorway, at least one shot is fired, and at least one operator ends below full health — and the failure message names the first mismatch tick. Breaking the path-blocked span's seeding reddens this baseline and no other. |
| 8 | Re-measure both existing golden fixtures and the seed-1 headless baseline, by capture, and move the superseded figures to the measurement history. | `tests/Sandata.Core.Tests/Fixtures/seed-1-baseline.json`, `docs/development/testing.md`, `docs/development/measurement-history.md` | Every new digest is traceable to a pasted capture run. `SandataRuleset.ContentHash` is unchanged at `8_955_292_433_887_190_872` and no new `SandataPresetId` is declared. |
| 9 | Both canonical gates, run once, after everything above is integrated. **Not delegated.** | none | `./scripts/verify.ps1 -Game Sandata` and `./scripts/verify.ps1` both exit 0, both outputs pasted separately into `docs/development/testing.md`. |

## What is handed back rather than built

- **Whether a lowered operator raises its weapon on identifying a hostile.**
  Both attackers stand lowered beside a firefight. Design section 4 records why
  this is a tactical decision rather than defect repair.
- **Whether a defender ever patrols, investigates, or reacts to gunfire.**
  Entity 3 does nothing for the whole run and Sandata has no behaviour for it to
  do. Its own package.
- **Balance.** Health, damage, and the four-round burst ceiling are untouched.
