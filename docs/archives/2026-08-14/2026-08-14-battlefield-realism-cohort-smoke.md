# Battlefield realism cohort smoke — closed 2026-08-14

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this project
remains `CLAUDE.md` and `docs/development/smoke-checklist.md`.

**This closes the family.** Its ranged-retreat half — `BR-5` through `BR-9` —
passed earlier on 2026-08-14 and is recorded separately in the archive document
titled "Battlefield realism cohort and retreat smoke — rows BR-5 to BR-9 closed
2026-08-14", in this same dated folder. The five rows in this record are the
cohort-deployment half, and with them the whole ten-row family is `PASS` and the
section was deleted from the live checklist whole.

| Field | Value |
| --- | --- |
| Rows in the family | 10 — `BR-1` through `BR-10` |
| Rows closed `PASS` and lifted here | 5 — `BR-1`, `BR-2`, `BR-3`, `BR-4`, `BR-10` |
| Rows closed earlier the same day | 5 — `BR-5` through `BR-9`, recorded separately |
| Rows still open in the live checklist | None |
| Prior interactive runs | One. `BR-1`, `BR-2` and `BR-10` were run on 2026-08-14 and failed; `BR-3` and `BR-4` were deliberately held back behind them |
| Lifted on | 2026-08-14 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-14 |
| Machine/platform | Not recorded |
| Source commit | Not recorded. The working tree at the time carried uncommitted documentation changes on top of `b8a3f97`; the two fixes these re-runs judged are `541b8d6` (`CohortLateralSpreadV13`) and `b566f88` (inspector row wrapping) |
| Launch path (`source` or package path) | `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

## What these rows were for

The family was added by the battlefield realism change, which flipped the
client's default preset combination to `PrecolonialPhilippinesV5` plus
`MovementPresetId.BattlefieldRealismV10`: weapon-cohort deployment, shield
bearers at the forward-most slots of their own contingent, and a three-rung
ranged retreat ladder.

The automated suite proved the cohort sort order, the shield-bearer slot pairing
inside each contingent, the threat-radius arithmetic, the retreat ladder's three
rungs, the per-index and positional mirror assertions, and the twenty-seed
termination sweep. It proved none of the three failures recorded below, and two
of them — a panel with no horizontal clip, and contingents that dissolve into
individual pursuit — had passing suites over them the whole time.

## The rows that closed

Three of these five carried a failing observation from earlier on 2026-08-14 and
were re-run against the fixes they were waiting on. The other two had been
deliberately held back behind those failures and were attempted for the first
time. The tester reported all five as passing and recorded no separate
observation for any of them; the `Actual` column below preserves the failing
observation each reopened row carried, so a later reader can see what the
re-run was judged against.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| BR-1 | Watch a contingent form up after deployment, at the default camera fit | The contingent reads as mostly carrying one weapon, with only a few warriors of a different weapon visible at its edges, rather than an even mix across the group. Failure is a contingent that still looks like a uniform round-robin blend of every weapon in the roster | First run 2026-08-14: "they visibly form up but not enough, some just charged and fought" — the weapon-grouping half worked, cohesion did not. Re-run later the same day and passed, with no separate note recorded. **Read the caution below: no cohesion fix shipped between the two runs** | PASS |
| BR-2 | Watch a contingent that includes shield bearers, before it makes contact with the enemy | The shield bearers are visibly at the forward-most slots of their own contingent — ahead of their contingent's other warriors on the approach — rather than scattered through the group or clustered only at the edge of the whole army | First run 2026-08-14: "some deployments have the shield bearers at the back" — conditional rather than a flat inversion. The within-contingent rule was correct on disk; the live fault was the shield cohort collecting at one edge of the whole army. Re-run against `CohortLateralSpreadV13`, which riffles cohort runs onto non-adjacent lanes, and passed, with no separate note recorded | PASS |
| BR-3 | Watch one contingent's shield bearers make first contact with the enemy, then watch how long the warriors behind them keep fighting | The shield bearers take the opening blows and the warriors sheltered behind them survive visibly longer than they would standing in the open. Failure is the enemy reaching the unshielded warriors just as quickly, or the shield bearers falling in the opening exchange with no visible difference | Deliberately not attempted on the first pass, because it is downstream of the deployment shape `BR-1` and `BR-2` found wanting. Attempted once those two passed, and passed, with no separate note recorded | PASS |
| BR-4 | Compare the two factions' starting deployments at the default camera fit, paused at tick 0 | An exact per-index mirror at tick 0, drifting apart once the battle runs. **The row's original premise was corrected on 2026-08-14**: it had asked a tester to confirm the two sides are *not* warrior-for-warrior mirrors, which only a broken build could satisfy, because `ArenaGame.BuildScenario` always populates `RosterCounts` and the rotating roster the row was written against belongs to `Scenario.CreateDefault`, which no client launch uses | Not attempted on the first pass; held back behind `BR-1` and `BR-2` and marked as subsumed by the starting-deployment row 59. Attempted after those closed, and passed, with no separate note recorded | PASS |
| BR-10 | Resize the game window down to the smallest supported size, 1024 by 720, and open the agent inspector on a warrior whose panel renders at its full height | The panel still fits within the window at that size without clipping against the window edge and without overlapping the HUD, the control bar, or the event feed | First run 2026-08-14: "it does render, but the width of the texts overextends the current small width of the info panel" — the fault was horizontal, not the vertical one the row was written to catch. The panel had no horizontal clip at all and roughly thirty rows were handed to a plain `DrawString` against a 277-pixel budget. Re-run against `b566f88`, which wraps every row to the panel width, and passed, with no separate note recorded | PASS |

## What a later reader should be careful of

- **`BR-1` passed without the fix that was designed for it.** The failure was
  traced to two composing gates: under `ContingentState.Advance`,
  `MovementRules.IsCohesionEligible` gate 4 denies a cohesion destination to
  every member that is *not* straggling, and the contingent's geometric gates
  rarely let it reach the gathering `Hold` state at all. A design was written
  for that — it is still live in `docs/plans/` under the contingent cohesion
  before contact title — and **none of its proposed changes had been
  implemented when this row passed.** `MovementRules.cs` still carries the
  binary straggler test and `BattleSimulation.cs` still marks every excluded
  slot as overlapping regardless of state. What changed between the two runs was
  `CohortLateralSpreadV13` becoming the client default, which changes how the
  army is laid out laterally, not how it coheres. Treat this pass as evidence
  about what a spectator sees at the default camera fit, not as evidence that
  the cohesion gates were fixed.
- **`BR-4`'s premise was corrected before it was run.** The row as originally
  written could only be passed by a broken build. Anyone reading the original
  wording in an older revision of the checklist is reading a row that was
  retired, not the row that passed.
- **`BR-2` closed against a lateral-spread change, not a within-contingent
  one.** `CohortDeploymentAssignment.AssignWithinContingent` was already sorting
  slots by depth and pairing shield bearers to the forward-most ones before the
  first run. Do not read this pass as validating a change to that function.
- **The `Actual` column is deliberately thin on the verdicts themselves.** The
  tester reported five passes and no narrative. Everything else in those cells
  is the earlier failing observation, preserved. No agent may enrich these cells
  later.
