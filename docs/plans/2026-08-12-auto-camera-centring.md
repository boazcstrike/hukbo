# Auto camera centring — plan

**Date:** 2026-08-12
**Design:** `docs/plans/2026-08-12-auto-camera-centring-design.md`, which
outranks this file wherever the two disagree.

The design's section 3 is the change and its section 4 is the constraint that
bounds it. This file is the ordered task list and the verification criteria.

## Tasks

| # | Task | Files | Verification | Depends on |
| --- | --- | --- | --- | --- |
| AC-T1 | Rename `SettleFraction` to `FollowOnScreenFraction`, value `0.7f` unchanged, and rewrite its doc comment so it describes only Follow mode's on-screen band and states that it has no part in ending a pan. | `src/Hukbo.Client/ArenaAutoPan.cs` | The solution builds under `TreatWarningsAsErrors`; `GetTuning` is the only remaining caller; `Controller_FollowRecentresAFightAssistedWouldLeaveAlone` still passes unchanged in substance. | — |
| AC-T2 | Add `CenteredFraction = 0.2f` with the doc comment the design's sections 3 and 5 require, including that the number is a presentation tuning value rather than a measurement. | `src/Hukbo.Client/ArenaAutoPan.cs` | The constant is `internal const float` beside the other tuning constants and is referenced by exactly one call site after AC-T3. | AC-T1 |
| AC-T3 | Point `ContinuePan`'s settle gate at `CenteredFraction`, rename the local `settleExtents` to `centeredExtents`, and correct the method's summary comment, which still describes stopping when a fighter is "comfortably inside the screen". | `src/Hukbo.Client/ArenaAutoPanController.cs` | The pan-start gate, idle grace, dwell, retarget interval, `IsWorthTravelling`, and `MaximumPanSeconds` are untouched in the diff. | AC-T2 |
| AC-T4 | Update the `IsSettled` test helper to the new constant and add `Controller_EndsAPanWithTheFightNearTheCentreNotTheEdge`, which asserts a pan that began from an empty screen ends with the melee inside `CenteredFraction` on both axes. Add a second test only if `Controller_StaysPutWhenAFighterIsAlreadyOnScreen` does not already prove the design's section 4 constraint. | `tests/Hukbo.Client.Tests/ArenaAutoPanTests.cs` | `dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release` is green, with the real output recorded. No existing assertion is weakened to get there. | AC-T3 |
| AC-T5 | Close smoke rows 149 to 155, lift the family into a dated archive record, and open the single replacement row `AC-1`, repairing every count and cross-reference the removal invalidates. | `docs/development/smoke-checklist.md`, `docs/development/measurement-history.md`, a new record under the 2026-08-12 archive folder | The live checklist's totals are recounted from its own status column rather than carried forward; no file outside the archive folder contains a path into it. | — |
| AC-T6 | Run the canonical gate and record its real output. | — | `./scripts/verify.ps1` completes, and both hashes are byte-identical to the recorded seed-1 baseline, which is the expected result for a change confined to `Hukbo.Client`. | AC-T4, AC-T5 |

AC-T5 shares no file with AC-T1 through AC-T4 and runs at the same time as
them. AC-T6 is not delegated and runs once, after everything else has landed.

## What closes this plan

`AC-1` is a manual row and only a person at an interactive desktop may flip it.
Until they do, this plan is complete but the behaviour is unproven — a green
gate says the tests pass, not that the camera now arrives centred.

## AC-T6 — the recorded gate result

`./scripts/verify.ps1` was run once on the integrated tree at `c15ca63` on
2026-08-13 and passed.

**What that tree contained, stated plainly.** The run covered this change
together with the lethal blow legibility change, the armor bulk second fix, the
corpse placeholder, the projectile prop scale fix, and the movement preset
selector — each a separate commit, but all present in the one tree the gate
built. So this is evidence that the six together do not break the gate and that
none of them moved a hash. It is not an isolated single-plan run, and anyone
who needs that stronger claim has to make it themselves.

```
[PASS] Required prerequisites and repository configuration are present.
[PASS] Locked package restore completed.
Formatted 0 of 759 files.
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
Test Run Successful.  Total tests: 2503   Passed: 2503     (Hukbo.Core.Tests)
Test Run Successful.  Total tests: 3781   Passed: 3781     (Hukbo.Client.Tests)
[PASS] Release repository tests completed.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

Both hashes are byte-identical to the recorded seed-1 baseline on every one of
stage five's four workloads, which is the expected result for a change confined
to `Hukbo.Client`:

| combat / movement preset | ticks | outcome | `stateHash` | `eventHash` |
| --- | --- | --- | --- | --- |
| 6 / 4 | 885 | `Faction0Victory` | `5460D13E3F7FD3E5` | `8E18ED1437B2924B` |
| 5 / 8 | 1 764 | `Faction1Victory` | `C8023D3B5BEB005E` | `F709A345E2F7370E` |
| 5 / 10 | 1 888 | `Faction0Victory` | `7C145A9E05916E4C` | `77626E104234206C` |
| 5 / 11 | 2 037 | `Faction0Victory` | `6225182B4A470F91` | `C4DABE6AF98B6BEC` |

This closes AC-T6. It does not close `AC-1`, which is a manual row and stays
`PENDING` until a person at an interactive desktop watches where a pan stops.
