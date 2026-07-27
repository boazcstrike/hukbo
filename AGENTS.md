# Hukbo Agent Context

This repository is **Hukbo**, a deterministic, offline, 2D spectator battle
game.

The canonical GitHub repository is
[`boazcstrike/hukbo`](https://github.com/boazcstrike/hukbo).

Use `Hukbo` for the product name, `hukbo` for repository-style slugs, and the
`Hukbo.*` prefix for .NET projects, assemblies, namespaces, and tests. Do not
reintroduce the former project name in code, configuration, documentation, or
agent instructions.

Generic arena-domain terms, such as the `ArenaGame` runtime class, describe
gameplay and are not alternate product names.

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

The full rules are in `CLAUDE.md` §5 and the design is in
`docs/plans/2026-07-27-debug-logging-standard-design.md`. Keep this file and
`CLAUDE.md` consistent.
