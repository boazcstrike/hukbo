# Warrior personal names — implementation plan

Date: 2026-07-29

Design: [2026-07-29-warrior-personal-names-design.md](2026-07-29-warrior-personal-names-design.md)

Research: [docs/names/HISTORICAL_1500s_PERSONAL_NAMES.md](../names/HISTORICAL_1500s_PERSONAL_NAMES.md)

## Tasks

| # | Task | Files | Status |
| --- | --- | --- | --- |
| 1 | Register the two new presentation salts and extend the registry list | `src/Hukbo.Client/Presentation/PresentationSalts.cs` | DONE |
| 2 | Add the name corpus: three regional pools, the entry record with its evidence metadata, the region and name selection streams, and the two standalone research notes | `src/Hukbo.Client/Presentation/Catalogs/WarriorNameCatalog.cs` | DONE |
| 3 | Add the resolver and the report-facing label helper | `src/Hukbo.Client/Presentation/WarriorNames.cs` | DONE |
| 4 | Add the inspector's identity row and provenance lines, and raise the panel's reserved row budget | `src/Hukbo.Client/UI/AgentInspectorContent.cs` | DONE |
| 5 | Draw the name in the inspector: thread the scenario seed in, replace the bare ID row, append the provenance block | `src/Hukbo.Client/UI/AgentInspectorPanel.cs` | DONE |
| 6 | Name warriors in the battle report: leaderboard rows, both attack highlights, longest survivor, per-faction top killer | `src/Hukbo.Client/UI/BattleReportPanel.cs` | DONE |
| 7 | Pass `Scenario.Seed` into both panels at their draw sites | `src/Hukbo.Client/ArenaGame.Rendering.cs` | DONE |
| 8 | Pin the corpus and every research exclusion with tests | `tests/Hukbo.Client.Tests/WarriorNameCatalogTests.cs` | DONE |
| 9 | Pin the derivation: stability, one region per faction, name spread, label format | `tests/Hukbo.Client.Tests/WarriorNamesTests.cs` | DONE |
| 10 | Pin the inspector lines for every shipped name | `tests/Hukbo.Client.Tests/AgentInspectorContentTests.cs` | DONE |
| 11 | Update the two salt-count assertions from eleven to thirteen | `tests/Hukbo.Client.Tests/PresentationSaltsTests.cs`, `tests/Hukbo.Client.Tests/VisualCatalogContractTests.cs` | DONE |
| 12 | Record the implementation status on the research document | `docs/names/HISTORICAL_1500s_PERSONAL_NAMES.md` | DONE |
| 13 | Name the actor in the event log: a full label for the detail pane and the text filter, a narrow label for the row column | `src/Hukbo.Client/Presentation/BattleEventFormatter.cs` | DONE |
| 14 | Carry the match seed on the feed so its text filter searches the same names the panel draws, alongside the older faction-and-identifier form | `src/Hukbo.Client/Presentation/BattleEventFeed.cs` | DONE |
| 15 | Draw the named actor in the log rows and the detail pane, with the seed joining both caches' keys | `src/Hukbo.Client/UI/BattleEventLogPanel.cs`, `.List.cs`, `.Details.cs` | DONE |
| 16 | Set the feed's seed at scenario build and at round reset | `src/Hukbo.Client/ArenaGame.cs` | DONE |
| 17 | Pin the log labels: actor naming, outcome rows, the fifteen-character row budget, and name-based filtering | `tests/Hukbo.Client.Tests/BattleEventFormatterTests.cs`, `tests/Hukbo.Client.Tests/BattleEventFeedTests.cs` | DONE |

## Verification

| Stage | Command | Result |
| --- | --- | --- |
| Build | `dotnet build Hukbo.slnx -c Debug` | PASS — 0 warnings, 0 errors |
| Client tests | `dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Debug` | PASS — 2,631 passed, 0 failed, 0 skipped |
| Canonical gate | `./scripts/verify.ps1` | PASS — prerequisites, locked restore, formatting, Release build, Core 747/747, Client 2,631/2,631, headless agents=200 ticks=10000 seed=1 |
| Manual smoke: name shows on the inspector for a selected warrior | interactive desktop run | PENDING |
| Manual smoke: names show on every battle report row and highlight | interactive desktop run | PENDING |
| Manual smoke: log rows name their actor and fit the column, and a name typed into the filter matches | interactive desktop run | PENDING |

The two manual rows may only be flipped by a human at an interactive desktop,
per `docs/development/testing.md`. They are not claimed here.

Note on the gate run: the first `verify.ps1` invocation reported one failing
Core test out of 747, in the `Hukbo.Core.Tests` Release stage. A standalone
`dotnet test tests/Hukbo.Core.Tests -c Release` rerun passed 747/747, and a
second full gate run passed every stage. The failing test's name scrolled past
before it was captured, so it is recorded here as an unidentified intermittent
failure rather than left unmentioned. No code in `Hukbo.Core` was touched by
this work.

## Deferred

- A log line's target stays `#<id>`. The event carries the actor's faction but
  not the target's, and naming a target would need either a determinism-level
  change to `BattleEvent` or a client-side entity-to-faction lookup threaded
  into the feed. See the design document, section 5a.
- Faction appearance block and faction name region are independent draws, so a
  faction can wear Visayan clothing and carry Tondo names in the same match.
  Resolving that needs either a Cagayan name corpus or a Mindanao appearance
  block — see the design document, section 2.
