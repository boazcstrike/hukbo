# Collision hot path — plan

The design is `docs/plans/2026-11-22-collision-hot-path-design.md`. It was
authorized on 2026-11-22 under the user's goal to compare three rounds of
implementation standards with We March as One and to implement what the research
supports.

**Baseline: `main` at `073f6ca`.** Work happens on branch `collision-hot-path`
in `.claude/worktrees/collision-hot-path`. CH-1 and CH-2 run in parallel in
their own worktrees, branched from `collision-hot-path`, and are merged back
before CH-3 starts.

## Rules for every task

- Hash-neutral. No preset version, no new golden, and no re-pinned hash. If any
  existing hash test or gate digest moves, the change is wrong.
- `System.Random`, floating point, and dictionary iteration that affects results
  stay banned in `Hukbo.Core`, as `CLAUDE.md` section 5 requires.
- Warm-tick allocation windows stay at zero bytes.
- Never weaken a test, a warning, or an analyzer.
- Commit per step, first person, with the trailer from
  `C:\Users\boazs\webdev\bin\trailer.ps1 -Engine sonnet`.

## Tasks

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| CH-1 | Change A (design §3): optional dense bounds on `CollisionUniformGrid`. The dense head array covers the bounds, with a dictionary fallback for every cell outside them and above `MaximumDenseCells`. `Clear()` resets only populated cells. `CollisionResolver` passes the map rectangle padded by one cell to both its grids; `CollisionScratch` passes the same rectangle to the metrics grid. Add tests for: in-bounds and out-of-bounds bodies giving the same answers as the naive oracle; a grid above the cap behaving exactly like an unbounded one; `Clear()` leaving no ghost body after a dense cell is reused; zero warm allocation | `src/Hukbo.Core/Simulation/CollisionUniformGrid.cs`, `src/Hukbo.Core/Simulation/CollisionResolver.cs`, `src/Hukbo.Core/Simulation/CollisionScratch.cs`, `tests/Hukbo.Core.Tests/CollisionUniformGridTests.cs` | Every collision test passes, including `Resolve_MatchesTheUnacceleratedAlgorithmOnEverySeededLayout` | — | `dotnet test tests/Hukbo.Core.Tests -c Release --filter "FullyQualifiedName~Collision"`, then the full Core suite |
| CH-2 | Change B (design §4): `int[] _agentIndexByEntityId` when IDs are dense, keeping the dictionary otherwise. Add `TryGetAgentIndex` and `AgentIndexOf`, and replace every `_agentIndexes` read with them. Add a test that seeded battles under several presets produce identical hashes, and a unit test of the helpers' edge cases: ID 0, an ID beyond the array, an unused slot, and sparse IDs taking the fallback path | `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `tests/Hukbo.Core.Tests/AgentIndexLookupTests.cs` (new) | The Core suite passes, and every pinned-hash test is unchanged | — | `./scripts/test.ps1 -Configuration Release` |
| CH-3 | Change C (design §5): a pair-count query on `CollisionUniformGrid` returning the pair count, the opposite-faction count and the minimum squared distance, with exactly `GeneratePairs`' predicate (`other.EntityId > body.EntityId` and `CollisionGeometry.IsContact` at the band radius; dead bodies are never inserted). Faction comes from a per-entity faction array that `BattleSimulation` builds once, when IDs are dense. Rewrite `MeasureCollision` to `Clear` plus `Insert` plus the query, with penetration `max(0, 2R − ⌊√minD²⌋)`; keep the old `Rebuild` path when IDs are sparse. Add the per-tick equivalence test of design §5.3 against a test-only copy of the old logic, under every movement preset runnable with combat V8, including V17 | `src/Hukbo.Core/Simulation/CollisionUniformGrid.cs`, `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `tests/Hukbo.Core.Tests/MeasureCollisionEquivalenceTests.cs` (new) | The equivalence test passes for every tick it checks, and the Core and Client suites pass | CH-1, CH-2 | `./scripts/test.ps1 -Configuration Release` |
| CH-4 | Measure (design §7): interleaved before and after binaries at 200, 500, 1,000 and 2,000 agents, plus a `dotnet-trace` stage-share table at 1,000 agents. Record both tables, the commands, and whether acceptance passed | `docs/development/measurement-history.md` | Both tables are recorded and the acceptance verdict is stated | CH-3 | The recorded figures |
| CH-5 | Run `./scripts/verify.ps1` once on the integrated branch and record that all seven Hukbo hashes are unchanged. Report Sandata's result separately. Then merge into `main` with `merge.ps1`, push, and remove the worktrees and branches | `docs/development/testing.md` | The gate is green and the branch is merged | CH-4 | The real gate output |

## Deferred, by decision

- Splitting `BattleSimulation` into partial classes by stage (design §2.4). It is
  feasible and hash-neutral, but it makes stale the `BattleSimulation.cs:NNNN`
  citations across the live documents. It needs its own decision.
- Skipping `MeasureCollision` in the client (design §2.4). Revisit only if CH-4
  shows the stage is still large.
- The client render re-baseline (TU-1 of the 2026-08-14 thousand-unit plan). It is
  the prerequisite for any client draw work, and it is not part of this package.
