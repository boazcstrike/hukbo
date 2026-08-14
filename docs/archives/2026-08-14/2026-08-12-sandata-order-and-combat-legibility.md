# Sandata order following, weapon legibility, and automatic fire — plan

**Archived: reference only.** This is finished work, kept only so a past
decision can be traced to its reasoning. Never execute it, never treat it as a
live task list, and never cite it as the reason to make a change. Sandata's live
contract is `CLAUDE.md` and the scaffold *design* document, which is still in
`docs/plans/`.

The ordered task list for
[`2026-08-12-sandata-order-and-combat-legibility-design.md`](2026-08-12-sandata-order-and-combat-legibility-design.md),
which is binding for every decision below. Opened 2026-08-12 after the second
Sandata smoke session.

**Verification for the package as a whole:** `./scripts/verify.ps1 -Game Sandata`
and `./scripts/verify.ps1` both green, with the real output pasted into
`docs/development/testing.md`. A green default gate says nothing about Sandata
and a green Sandata gate says nothing about Hukbo, so both run and both are
recorded.

**No task below may flip a smoke row.** `SD-4`, `SD-5`, and `SD-7b` stay
`PENDING` or `BLOCKED` until a person at a desktop says otherwise.

## Wave 1 — the order layer (D1, D2)

| # | Task | Files | Verification |
| --- | --- | --- | --- |
| 1 | Advance and clear order assignments in stage 1, before this tick's orders are applied. Arrival is `NodeArrivalRadiusWu = 16`, provisional, documented at its declaration. Clearing goes through `MovementSource.Evaluate` with `cancelOrderApplied: false` | `src/Sandata.Core/Simulation/SandataSimulation.cs` | New tests in `tests/Sandata.Core.Tests`: an operator walks a two-node path end to end; the assignment clears at the final node; an operator that dies mid-path loses its assignment; an unassigned run is byte-identical to before |
| 2 | Log every order submission. One new `const` on `LogEvents`, `input.sandata.order`, written from the client's two submission call sites | `src/Hukbo.Diagnostics/LogEvents.cs`, `src/Sandata.Client/SandataGame.cs` | A `Debug` run's `.jsonl` carries one line per submission, `warn` on rejection with the reason by name |
| 3 | Draw order-queue rows as text through `OrderQueueView.FormatEntryLine` | `src/Sandata.Client/SandataGame.cs` | `Sandata.Client.Tests` covers the formatter's rejection wording; the draw path itself is smoke-only |

## Wave 2 — the lowered weapon (D3, row `SD-4`)

| # | Task | Files | Verification |
| --- | --- | --- | --- |
| 4 | Stage 11 stores `WeaponLowered` from the resolved chain phase and emits `WeaponLowered` / `WeaponRaised` on the tick it changes. Both kinds appended at free ordinals | `src/Sandata.Core/Events/MissionEventKind.cs`, `src/Sandata.Core/Events/MissionEvent.cs`, `src/Sandata.Core/Simulation/SandataSimulation.cs` | A test walks an operator into wall range and asserts the stored flag and exactly one event per transition |
| 5 | Draw a lowered weapon differently: `OperatorGeometry.Create` gains `isWeaponLowered`, defaulting to `false` so every pinned rectangle is unchanged | `src/Sandata.Client/Rendering/OperatorGeometry.cs`, `src/Sandata.Client/SandataGame.cs` | `Sandata.Client.Tests`: the lowered layout differs from the raised one in weapon body and muzzle anchor, and is identical everywhere else |

## Wave 3 — automatic fire (D4, row `SD-5`)

| # | Task | Files | Verification |
| --- | --- | --- | --- |
| 6 | Stage 11 selects a fire mode per shot through `FireModeSelection.SelectMode`, drives `CyclicFireAccumulator` for `Auto`, and carries the mode on `MissionEvent.ShotFired`'s existing `ReasonCode` | `src/Sandata.Core/Simulation/SandataSimulation.cs`, `src/Sandata.Core/Events/MissionEvent.cs` | Tests: a rifle inside the auto band fires at its cyclic rate; a pistol never selects `Auto`; a target beyond the single band produces no shot; the accumulator resets when fire stops |
| 7 | Client reads the mode off the event instead of hardcoding `Single`, and reports the end of a burst to `HandleAutomaticFireStopped` | `src/Sandata.Client/SandataGame.cs` | `Sandata.Client.Tests` for the `FireModeSet` to `Audio.FireMode` mapping |
| 8 | Fall back to one report per round when a loop cue is declined, so a missing loop file is audible rather than silent | `src/Sandata.Client/Audio/SandataSoundPlayer.cs` | `Sandata.Client.Tests` with a fake output that declines the loop and records the report calls |

## Wave 4 — `SD-7b`, in its own worktree

| # | Task | Files | Verification |
| --- | --- | --- | --- |
| 9 | A theme switcher key, unpersisted | `src/Sandata.Client/SandataGame.cs` | `Sandata.Client.Tests` for the pure cycle helper over the catalog's ids |
| 10 | Unknown-contact rendering: a hostile draws by the assaulting faction's best `ContactTier` for it — identified as today, detected as a facingless marker, unseen not drawn | `src/Sandata.Client/SandataGame.cs`, `src/Sandata.Client/Rendering/*` | `Sandata.Client.Tests` for the pure tier-to-appearance resolver |

## Wave 5 — records

| # | Task | Files | Verification |
| --- | --- | --- | --- |
| 11 | Re-measure both golden replay fixtures by running a capture, never by hand | `tests/Sandata.Core.Tests/Fixtures/seed-1-baseline.json` | `GoldenReplayTests` green against the re-measured values |
| 12 | Re-record the seed-1 headless baseline; superseded figures move to the measurement history | `docs/development/testing.md`, `docs/development/measurement-history.md` | Real gate output pasted, not summarised |
| 13 | Rewrite the Sandata section of the smoke checklist so it holds open work only, and archive the finished plan documents | `docs/development/smoke-checklist.md`, `docs/plans/README.md`, `docs/archives/2026-08-12/` | The file's own counts recount correctly |
