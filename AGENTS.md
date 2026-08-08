# Hukbo Agent Context

This repository builds **two** deterministic, offline games.

**Hukbo** is a 2D spectator battle game about warfare in the pre-colonial and
early-contact Philippines (roughly 1500s). The tactical battle simulation is
built (v0.1); a future 4X campaign layer does not exist yet and must not gain
state inside `Hukbo.Core`.

**Sandata** is a top-down modern tactical game — room clearing in the Door
Kickers tradition, with both autonomous squad behaviour and hand-drawn player
orders. It is in development and has no v0.1 yet. It is a separate product with
its own simulation, ruleset, preset stream, and hashes; it is not a Hukbo mode
and not a fork. Its binding documents are
`docs/plans/2026-08-07-sandata-scaffold-design.md`, which outranks every summary
of it including this one, and `docs/plans/2026-08-07-sandata-scaffold.md`. Its
name is still an open question, as are real weapon names versus generic aliases
in shipped display strings.

The only code the two games share is `src/Hukbo.Shared.Core`, which holds
exactly four pure integer determinism primitives —
`Mathematics/FixedPoint.cs`, `Determinism/SplitMix64.cs`,
`Determinism/Fnv1a.cs`, and `Movement/Facing16.cs`. Nothing else may move there
without a design decision: a type that knows what an agent, an operator, a
weapon, a tick stage, or a map is belongs to one game and stays there. No
`Sandata.*` project may reference a `Hukbo.Core` or `Hukbo.Client` type, and no
`Hukbo.*` project may reference a `Sandata.*` type.

The twelve projects, all in `Hukbo.slnx`:

```
src/Hukbo.Shared.Core   tier 1, shared determinism primitives, four files
src/Hukbo.Core          Hukbo's authoritative simulation
src/Hukbo.Client        Hukbo's MonoGame DesktopGL shell
src/Hukbo.Headless      Hukbo's determinism + benchmark runner, no window
src/Hukbo.Diagnostics   JSON Lines debug log; never referenced by either Core
src/Sandata.Core        Sandata's authoritative simulation
src/Sandata.Client      Sandata's MonoGame DesktopGL shell
src/Sandata.Headless    Sandata's determinism + navigation-benchmark runner
tests/Hukbo.Core.Tests   tests/Hukbo.Client.Tests
tests/Sandata.Core.Tests tests/Sandata.Client.Tests
```

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

Sandata follows the same rule under its own prefix: `Sandata`, `sandata`, and
`Sandata.*`. The one deliberate exception is that Sandata's entry points log
through `Hukbo.Diagnostics` and read the `HUKBO_LOG_LEVEL`,
`HUKBO_LOG_CHANNELS`, and `HUKBO_LOG_DIR` environment variables unchanged —
those names configure the shared logger and are not forked. A Sandata run writes
`artifacts/logs/sandata-<utc>-<pid>.jsonl`.

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

Every game-specific script takes `-Game`, validated to `Hukbo` or `Sandata` and
defaulting to `Hukbo`, so a command with no `-Game` runs exactly what it ran
before the second game existed:

```powershell
./scripts/run.ps1 -Game Sandata
./scripts/test.ps1 -Configuration Release -Game Sandata
./scripts/benchmark.ps1 -Game Sandata -Seed 1
./scripts/verify.ps1 -Game Sandata
```

The project paths live in one table, `scripts/_gametargets.ps1`; no script body
hardcodes a project path, and tests assert both halves of that. `build.ps1`,
`format.ps1`, and `bootstrap.ps1` take no `-Game` because they operate on the
whole solution, and `doctor.ps1` takes none because it checks every project's
lock file rather than one game's.

**`./scripts/verify.ps1` with no flag runs Hukbo only.** A green default gate is
not evidence about Sandata, and the two results must never be reported as one.
Sandata is deliberately not part of the default gate yet, so a red Sandata
workload can never be mistaken for a red Hukbo one.

## Non-negotiables

- Authoritative time is an integer tick. Never wall-clock time.
- `System.Random` is banned — its sequence is not guaranteed across .NET
  versions. Use `src/Hukbo.Shared.Core/Determinism/SplitMix64.cs`.
- Fixed-point math (`src/Hukbo.Shared.Core/Mathematics/FixedPoint.cs`) for
  anything that reaches the state hash. Same seed + same build + same commands
  ⇒ identical state hash, event hash, winner, and ordered event stream.
- Neither `Hukbo.Core` nor `Sandata.Core` may reference MonoGame, the
  filesystem, the network, windowing, audio, the wall clock, or
  `Hukbo.Diagnostics`.
- `TreatWarningsAsErrors` is on repo-wide with nullable enabled. Do not
  weaken a test, a warning, or an analyzer to get green.
- Conventional Commits (`feat`, `fix`, `refactor`, `docs`, `test`, `chore`,
  `perf`, `ci`). Keep diffs scoped to the requested change.
- Do not add campaign, economy, diplomacy, or map-generation state to
  `Hukbo.Core`. That layer does not exist yet, produces `Scenario` values and
  consumes `BattleOutcome` from a separate project when it starts, and owns
  its own seed stream.

Sandata adds these, all from its design document's section 4, and all binding on
`Sandata.Core` on top of everything above:

- Distance is a world unit stored as `FixedPoint` raw at a scale of 1024, with
  1 metre equal to 16 world units. Time is an integer tick at a `TickRate` of
  50, so one tick is exactly 20 milliseconds — Hukbo runs at 20 Hz and the two
  rates are not interchangeable. Fine angles are `Bam16`, a `ushort` over a full
  turn of 65,536; coarse angles reuse `Facing16`.
- Weapon timings are authored as integer milliseconds and converted to ticks
  once, at ruleset bake time, by the pinned rule
  `ticks = (milliseconds * TickRate + 500) / 1000`. That rule's identifier and
  the tick rate both fold into `SandataRuleset.ContentHash`, currently
  `8_955_292_433_887_190_872` and pinned by a test.
- Two independent hashes, as in Hukbo: an FNV-1a state hash over authoritative
  state in fixed field order, and an FNV-1a event hash over the ordered event
  stream.
- Derived structures are never hashed and never snapshotted — the nav grid, the
  clearance field, wall buckets, A\* scratch, line-of-sight results, the
  collision grid, and **published path polylines**. A path request is
  authoritative and snapshotted; the resulting polyline is not, and is
  recomputed from the stored request on resume before the first tick executes.
- `float`, `double`, `System.Random`, `Math.Sqrt`, `Math.Atan2`, `Dictionary<`,
  `HashSet<`, and `PriorityQueue<` may not appear in `src/Sandata.Core` outside
  a doc comment, and a test enforces it. There is no epsilon anywhere in that
  project.
- Path amortisation is by fixed latency, never a per-tick search budget:
  a path requested at tick `t` becomes valid at `t + PathLatencyTicks` no matter
  how many searches the machine actually finished.
- The preset is `SandataPresetId.ModernTacticalV1 = 1`, append-only and pinned.
  Sandata's golden replay baselines live in
  `tests/Sandata.Core.Tests/Fixtures/seed-1-baseline.json`, which is where a
  run's digest belongs — exactly one absolute state-hash literal is permitted in
  C# under `tests/Sandata.Core.Tests/` and it is already spent.

## Debug logging

The game is in development and testing. Every development run must leave behind
a record that can be read without having watched the screen.

- All runtime diagnostics go through `Hukbo.Diagnostics.DiagnosticLog`, which
  writes JSON Lines to `artifacts/logs/hukbo-<utc>-<pid>.jsonl` or
  `artifacts/logs/sandata-<utc>-<pid>.jsonl`. Never `Console.Write*`, never
  `Debug.WriteLine`, never a bespoke text file. Only the four entry points —
  `src/Hukbo.Client/Program.cs`, `src/Hukbo.Headless/Program.cs`,
  `src/Sandata.Client/Program.cs`, and `src/Sandata.Headless/Program.cs` — may
  touch the console, and a test enforces that across the whole of `src/`.
- Neither `Hukbo.Core` nor `Sandata.Core` may ever reference
  `Hukbo.Diagnostics`. A simulation is forbidden the filesystem and the wall
  clock; observe it from outside. Tests assert the missing assembly reference
  for both, each against a headless positive control.
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

## Agent orchestration

Non-trivial work runs as parallel read-only research, then a single planner,
then implementation agents that each own a non-overlapping set of files. Two
rules bind every dispatch: a coding task runs on Sonnet, every time, and every
agent-to-agent prompt is caveman-compressed before it is sent. Repository
files, documentation, commits, and pull requests are never compressed. The
canonical gate is never delegated to a sub-agent. Full rules: `CLAUDE.md` §10.

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
- Import Arch or another general-purpose ECS without a new profile and design
  decision. Arch 2.1.0 is a reference implementation only; reuse compatible,
  measured techniques under `SIMULATION-GAME-STANDARDS.md` section 15.
- Commit credentials, absolute local paths, `bin/`, `obj/`, or package
  output.
- Start terrain, pathfinding, morale, ammunition (quiver sizes, resupply, or
  any stock-and-consumption model for a projectile), persistence migrations,
  multiplayer, or mod APIs before the gate that authorizes them. Projectiles
  and projectile flight time were authorized on 2026-08-07 for the
  ranged-units package (`docs/plans/2026-08-07-ranged-units.md`) alone;
  ammunition was not authorized and stays deferred. Sandata's own navigation
  and pathfinding are authorized by its design document; Hukbo's are not.
- Let either game reach into the other, or move code into `Hukbo.Shared.Core`
  as a shortcut around that rule.
- Report a green `./scripts/verify.ps1` as evidence about Sandata. Without
  `-Game Sandata` the gate never built or ran a line of it.
- Run `./scripts/sfx.ps1` for Sandata. Its audio catalog is 106 slots expanding
  to 524 variant files, roughly 104,800 ElevenLabs credits, and the spend is
  **not authorized**. `scripts/sfx-manifest.ps1` is the network-free script that
  produces the dry-run manifest that would have to be reviewed first.

The full rules are in `CLAUDE.md` and `SIMULATION-GAME-STANDARDS.md`. Keep
this file and `CLAUDE.md` consistent.
