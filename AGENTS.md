# Hukbo Agent Context

This repository is **Hukbo**, a deterministic, offline, 2D spectator battle
game about warfare in the pre-colonial and early-contact Philippines (roughly
1500s). The tactical battle simulation is built (v0.1); a future 4X campaign
layer does not exist yet and must not gain state inside `Hukbo.Core`.

The canonical GitHub repository is
[`boazcstrike/hukbo`](https://github.com/boazcstrike/hukbo).

This file is the standalone contract for agents that do not read `CLAUDE.md`
— it must be complete on its own. `CLAUDE.md` is the fuller, Claude-facing
version of the same rules; keep both consistent.

## Naming

Use `Hukbo` for the product name, `hukbo` for repository-style slugs, and the
`Hukbo.*` prefix for .NET projects, assemblies, namespaces, and tests. Do not
reintroduce the former project name in code, configuration, documentation, or
agent instructions.

Generic arena-domain terms, such as the `ArenaGame` runtime class, describe
gameplay and are not alternate product names.

## Commands

```powershell
./scripts/bootstrap.ps1     # prerequisites + locked restore
./scripts/run.ps1           # launch the game
./scripts/verify.ps1        # canonical gate — run before integrating
./scripts/test.ps1 -Configuration Release
./scripts/format.ps1 -Verify
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1
```

`./scripts/verify.ps1` is the canonical gate: locked restore, format check,
Release build, Core + GPU-independent Client tests, then a 200-agent /
10,000-tick / seed-1 headless determinism workload. There is no CI — never
propose a GitHub Actions workflow, and never claim a change is verified
without pasting the actual gate output. `.NET SDK 10.0.302` is pinned in
`global.json`.

## Non-negotiables

- Authoritative time is an integer tick. Never wall-clock time.
- `System.Random` is banned — its sequence is not guaranteed across .NET
  versions. Use `Hukbo.Core/Determinism/SplitMix64.cs`.
- Fixed-point math (`Hukbo.Core/Mathematics/FixedPoint.cs`) for anything that
  reaches the state hash. Same seed + same build + same commands ⇒ identical
  state hash, event hash, winner, and ordered event stream.
- `Hukbo.Core` must not reference MonoGame, the filesystem, the network,
  windowing, audio, the wall clock, or `Hukbo.Diagnostics`.
- `TreatWarningsAsErrors` is on repo-wide with nullable enabled. Do not
  weaken a test, a warning, or an analyzer to get green.
- Conventional Commits (`feat`, `fix`, `refactor`, `docs`, `test`, `chore`,
  `perf`, `ci`). Keep diffs scoped to the requested change.
- Do not add campaign, economy, diplomacy, or map-generation state to
  `Hukbo.Core`. That layer does not exist yet, produces `Scenario` values and
  consumes `BattleOutcome` from a separate project when it starts, and owns
  its own seed stream.

## Debug logging

The game is in development and testing. Every development run must leave behind
a record that can be read without having watched the screen.

- All runtime diagnostics go through `Hukbo.Diagnostics.DiagnosticLog`, which
  writes JSON Lines to `artifacts/logs/hukbo-<utc>-<pid>.jsonl`. Never
  `Console.Write*`, never `Debug.WriteLine`, never a bespoke text file. Only
  `src/Hukbo.Client/Program.cs` and `src/Hukbo.Headless/Program.cs` may touch
  the console, and a test enforces that.
- `Hukbo.Core` must never reference `Hukbo.Diagnostics`. The simulation is
  forbidden the filesystem and the wall clock; observe it from outside.
- Every line begins with `seq`, `t`, `ms`, `lvl`, `ch`, `ev` in that order,
  followed by flat `camelCase` payload fields.
- `ev` is a stable dotted identifier declared as a `const` on `LogEvents`. It is
  a machine key: no values, no counts, no rewording.
- Logging is on by default in `Debug` and off in `Release`. `HUKBO_LOG_LEVEL`,
  `HUKBO_LOG_CHANNELS`, and `HUKBO_LOG_DIR` override both.
- A disabled call must allocate nothing, and logging must never change a
  simulation result. Both are enforced by tests.

## Workflow

Non-trivial work: a design doc in `docs/plans/` first (does not authorize
implementation), then a plan doc with the ordered task list, then
implementation, then the canonical gate with its real output recorded.
Interactive behavior is proven only by the manual checklist in
`docs/development/testing.md` — a passing build or test suite does not
authorize marking a checklist row `PASS`; leave it `PENDING` or report
`BLOCKED` honestly. Full rules: `CLAUDE.md` §6.

## Historical accuracy

This is a game about a real place and real people, built on contested
colonial-era sources. Label every claim **Documented**, **Documented, form
uncertain**, or **Provisional reconstruction**. A cultural identification
never appears as a bare, unqualified label, and a name whose earliest
attestation postdates the depicted period by more than a century is not used
at all. Full rules: `docs/research/HISTORICAL_1500s_WEAPONS.md` and
`CLAUDE.md` §7.

## Do not

- Add a hosted CI workflow, or delete `.github/` contents without saying so.
- Introduce rigid-body physics; distance checks and hitscan are the model.
- Cache targets, or add any unbounded cache; save derived caches, render
  data, or metrics into a snapshot.
- Add a general-purpose ECS framework before a profiler demands it.
- Commit credentials, absolute local paths, `bin/`, `obj/`, or package
  output.
- Start terrain, pathfinding, morale, projectile ammunition, persistence
  migrations, multiplayer, or mod APIs before the gate that authorizes them.

The full rules are in `CLAUDE.md` and `SIMULATION-GAME-STANDARDS.md`. Keep
this file and `CLAUDE.md` consistent.
