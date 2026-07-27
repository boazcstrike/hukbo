# Hukbo — Agent Instructions

Read this before touching anything. `AGENTS.md` holds the naming contract and is
the companion file for non-Claude agents; keep the two consistent.

## 1. Product

**Hukbo** is a deterministic, offline, single-player strategy game about
warfare in the pre-colonial and early-contact Philippines (roughly 1500s).

Two layers, one long-term goal:

| Layer | Status | Lives in |
| --- | --- | --- |
| Tactical battle simulation — two autonomous factions, spectator controls, deterministic replay | **Built (v0.1)** | `src/Hukbo.Core`, `src/Hukbo.Client`, `src/Hukbo.Headless` |
| 4X campaign — explore islands, expand polities, exploit trade, exterminate rivals | **Not started** | Future `Hukbo.Campaign` (does not exist yet) |

The battle simulation is the tactical resolution layer of the future 4X game.
It is not a separate product and it is not a prototype to throw away.

**Do not add campaign, economy, diplomacy, or map-generation state to
`Hukbo.Core`.** Gate 3 in `SIMULATION-GAME-STANDARDS.md` must pass first
(scenario, snapshot, replay verification, save/resume equivalence, 500-agent
stress report). When the campaign layer starts, it goes in a new project that
*produces* `Scenario` values and *consumes* `BattleOutcome`, and it owns its own
deterministic seed stream. Battle Core never learns what a barangay is.

## 2. Naming

Use `Hukbo` (product), `hukbo` (slugs), `Hukbo.*` (projects, assemblies,
namespaces, tests). Never reintroduce the former `AutonomousArena` name in code,
config, docs, or instructions. Stale `AutonomousArena.*.nuget.*` files under
`obj/` are untracked build leftovers — ignore them, do not "fix" them. The
working-directory name `autonomous-arena` is legacy and is not the product name.

Generic arena-domain terms (`ArenaGame`, arena bounds) describe gameplay and are
not alternate product names.

## 3. Layout

```
src/Hukbo.Core       authoritative simulation: tick pipeline, agents, events, RNG, hashing
src/Hukbo.Client     MonoGame DesktopGL shell: rendering, camera, UI, themes, input
src/Hukbo.Headless   determinism + benchmark runner, no window
src/Hukbo.Diagnostics JSON Lines debug log shared by Client and Headless; never referenced by Core
tests/Hukbo.Core.Tests
tests/Hukbo.Client.Tests
scripts/             the only supported entry points (PowerShell 7)
tools/               hand-run measurement harnesses; not in Hukbo.slnx, not in the gate
docs/                design, plans, research, agent-role evidence
```

`Hukbo.Core` must not reference MonoGame, the filesystem, the network, windowing,
audio, the wall clock, or `Hukbo.Diagnostics`. `Hukbo.Client` must not decide
targeting, damage, retreat, or victory.

## 4. Commands

```powershell
./scripts/bootstrap.ps1                        # prerequisites + locked restore
./scripts/run.ps1                              # launch the game
./scripts/verify.ps1                           # canonical gate — run before integrating
./scripts/verify.ps1 -SkipBootstrap            # gate without re-restoring
./scripts/test.ps1 -Configuration Release
./scripts/format.ps1 -Verify
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1
./scripts/package.ps1 -Runtime win-x64
./scripts/doctor.ps1
./scripts/sfx.ps1 -List                        # sound slots and which ones have a file
./scripts/sfx.ps1 -Slot death                  # generate that slot with ElevenLabs
./scripts/run.ps1 -Configuration Debug -LogLevel dbg
./scripts/run.ps1 -Configuration Debug -LogLevel trc -LogChannels audio,input
./scripts/benchmark.ps1 -LogLevel err          # log only a determinism mismatch
```

A `Debug` run writes `artifacts/logs/hukbo-<utc>-<pid>.jsonl` with no flags at
all. Read it back with, for example:

```powershell
Get-Content (Get-ChildItem artifacts/logs -Filter *.jsonl |
  Sort-Object Name | Select-Object -Last 1) |
  ConvertFrom-Json | Where-Object ch -eq 'audio' | Select-Object -First 40
```

`sfx.ps1` is an authoring tool, not part of any pipeline. It is the only script
that talks to a network service, and it runs only when a person asks for a
sound. It reads `ELEVENLABS_API_KEY` from the environment or from the untracked
`.env` file; that key never belongs in a tracked file, in output, or in a commit
message. The game, the build, the tests, and the gate remain fully offline.

The canonical gate runs: prerequisites and locked restore, format verification,
Release build, Core + GPU-independent Client tests, then a 200-agent /
10,000-tick / seed-1 headless determinism workload.

**There is no CI.** Verification is local-only and deliberate. Never propose a
GitHub Actions workflow, and never claim a change is verified without pasting
the actual gate output. `.NET SDK 10.0.302` is pinned in `global.json`.

## 5. Non-negotiables

Determinism (full contract in `SIMULATION-GAME-STANDARDS.md` §4):

- Authoritative time is an integer tick. Never wall-clock time.
- Fixed tick-stage order; incidental call order never decides an outcome.
- Every multi-result query has a total order; ties break on stable `EntityId`.
- `System.Random` is banned — Microsoft does not guarantee its sequence across
  major .NET versions. Use `Hukbo.Core/Determinism/SplitMix64.cs`.
- Hash-set / dictionary iteration order may not affect gameplay.
- Fixed-point math (`Hukbo.Core/Mathematics/FixedPoint.cs`) for anything that
  reaches the state hash.
- Same seed + same build + same commands ⇒ identical state hash, event hash,
  winner, and ordered event stream. Changing enum numeric values, enum order,
  roster order, weights, or a hash mixer requires a **new preset version** plus
  new golden expectations.

Build and quality:

- `TreatWarningsAsErrors` is on repo-wide with nullable enabled. Do not weaken a
  test, a warning, or an analyzer to get green.
- Package versions live in `Directory.Packages.props`; regenerate
  `packages.lock.json` only for a reviewed dependency change.
- Conventional Commits. Keep diffs scoped to the requested change.
- Client presentation tests must never construct `ArenaGame`, a graphics device,
  a sprite batch, or a window, and must not depend on GPU, audio, focus,
  network, or the wall clock.
- The battle event feed retains at most 200 ordered events.

Debug logging (full design in
`docs/plans/2026-07-27-debug-logging-standard-design.md`):

- The game is in development and testing, and every development run must leave
  behind a record an agent can read without having watched the screen. That
  record is JSON Lines, one object per line, under `artifacts/logs/`.
- Write through `Hukbo.Diagnostics.DiagnosticLog`. Never `Console.Write*`,
  never `Debug.WriteLine`, never a bespoke text file. A test scans `src/` and
  fails the build if anything but the two `Program.cs` entry points touches the
  console.
- **`Hukbo.Core` must never reference `Hukbo.Diagnostics`.** The simulation is
  forbidden the filesystem and the wall clock, and the logger needs both.
  Observe the simulation from outside, reading state the caller already holds.
  A test asserts the absence of the assembly reference.
- Every line carries `seq`, `t`, `ms`, `lvl`, `ch`, `ev` first, in that order,
  followed by flat `camelCase` payload fields. No nesting, no arrays.
- `ev` is a stable dotted identifier declared as a `const` on `LogEvents` — a
  machine key, never a sentence, never carrying a value or a count, never
  reworded. Free prose goes in an optional `msg` field on `err` and `warn`
  only.
- Levels are `err`, `warn`, `inf`, `dbg`, `trc`. Anything firing more than once
  a second belongs at `dbg` or below. Per-tick and per-frame lines are `trc`.
- Default `dbg` in `Debug`, `off` in `Release`; `HUKBO_LOG_LEVEL`,
  `HUKBO_LOG_CHANNELS`, and `HUKBO_LOG_DIR` override both. The canonical gate
  builds `Release` and its determinism workload runs unlogged.
- A disabled call must allocate nothing. Test the level and channel before
  doing any work whose only purpose is to produce a payload value, and never
  add a query the run would not otherwise make.
- Logging may not change a simulation. A test runs the seed-1 headless workload
  with logging off and at `trc` and requires identical state hash, event hash,
  outcome, and event stream.

## 6. Workflow

1. **Design doc first** for any non-trivial feature:
   `docs/plans/YYYY-MM-DD-<slug>-design.md`. Design documents do not authorize
   implementation.
2. **Plan doc** next: `docs/plans/YYYY-MM-DD-<slug>.md` with the ordered task
   list and verification criteria.
3. Implement, then run the canonical gate and record the exact result.
4. Interactive behavior is only proven by the manual checklist in
   `docs/development/testing.md`. Compilation, unit tests, or a
   window-opening probe do not let you flip a row to `PASS`. Leave untouched
   rows `PENDING`; report `BLOCKED` honestly.
5. Move finished plans to `docs/archives/` and add the "Archived: reference only"
   banner under the title.

**`docs/archives/` is deprecated by definition.** It is the dump for finished and
abandoned work, kept only so a past decision can be traced to its reasoning.
Never execute an archived plan, never treat its versions or tooling references as
current, and never cite one as justification for a change. Active work lives in
`docs/plans/`; the live contract is this file, `SIMULATION-GAME-STANDARDS.md`,
`docs/development/testing.md`, and `.claude/skills/`. See
[docs/archives/README.md](docs/archives/README.md).
6. Every feature proposal answers the nine questions in
   `SIMULATION-GAME-STANDARDS.md` §10, including: *can a spectator discover this
   effect without reading source code?* If not, the feature is incomplete.

**Write documentation in full, normal English.** Never run a prose-compression
pass over repository documentation — compression tooling exists for agent-to-agent
prompts and private memory files only.

**Tool output in this environment is lossily compressed.** File contents you read
can come back with filler words dropped, so a doc may look mangled and code may
look syntactically invalid when the bytes on disk are correct. Before reporting a
file as damaged, confirm it numerically (line counts, regex match counts) rather
than trusting the rendered text. Before an `Edit`, confirm the anchor string
exactly, for example:

```powershell
Get-Content README.md | Select-String -Pattern '^#' |
  ForEach-Object { "$($_.LineNumber)>" + (($_.Line.ToCharArray()) -join '|') }
```

`Edit` fails loudly on a mismatched anchor, which is the safe outcome; a `Write`
of reconstructed content is not, because it would overwrite real prose with your
lossy view of it.

## 7. Historical accuracy policy

This is a game about a real place and real people, built on contested colonial-era
sources. The rules in `docs/research/HISTORICAL_1500s_WEAPONS.md` are binding:

- Label every claim **Documented**, **Documented, form uncertain**, or
  **Provisional reconstruction**.
- Player-facing UI uses plain descriptors (`Great Blade`, `Heavy Chopper`).
  Specific cultural identifications (Kampilan, Panabas, Kris) live in evidence
  metadata with a `PROVISIONAL` note, never as an unqualified label.
- Spanish accounts are evidence about equipment, not neutral ethnography. The
  Boxer Codex guides silhouette and color, not exact technical cataloging.
- Gameplay tuning values (for example the tall-hardwood shield multiplier) are
  marked provisional in code comments and tests — never presented as a
  historical measurement.
- Do not generalize one region or decade to "the Philippines".

## 8. Tooling

Code discovery: use the `tokensave` MCP tools (`tokensave_context`,
`tokensave_search`, `tokensave_callers`, `tokensave_impact`) and the
codebase-memory graph before Grep/Glob. Do not spawn Explore agents for code
research in this repo.

Project-local skills in `.claude/skills/` — prefer these over generic advice:

| Skill | Covers |
| --- | --- |
| `hukbo-verify-and-record` | The five gate stages, headless exit codes, which `RunReport` fields are evidence, and the smoke-checklist honesty protocol |
| `hukbo-client-ui` | The pure-helper testability pattern, the 27 semantic theme roles, pointer priority |
| `hukbo-determinism-change` | The two independent hashes, the pinned SplitMix64 vectors, the recorded seed-1 baseline |
| `hukbo-sound-effects` | Generating a sound slot with ElevenLabs through `scripts/sfx.ps1`, the API-key rule, the PCM WAV requirement, prompt guidance |
| `hukbo-debug-logging` | Turning the JSON Lines debug log on, reading it, adding an event to `LogEvents`, and the four rules enforced by tests |

Plugins that earn their keep here (see `.claude/settings.json`):

| Plugin | Use for |
| --- | --- |
| `dotnet` | Roslyn LSP over `Hukbo.Core`/`Client`/`Headless`. Requires `ENABLE_LSP_TOOL=1` and a restart. Do **not** also enable `csharp-lsp` — both claim `.cs` and the first registered wins |
| `dotnet-diag` | `dotnet-trace` / `dotnet-dump`, allocation and GC-pressure work against the per-tick allocation budget |
| `dotnet-msbuild` | Binlog failure analysis for `Directory.Build.props`, central package management, `.slnx`, MonoGame content builder |
| `dotnet-test` | Test quality, gap and coverage analysis. Coverage needs a provider the repo does not have yet — adding one is a reviewed dependency change with lock-file regeneration |
| `context7` | MonoGame 3.8.5, .NET 10, xunit API facts — check docs, do not answer from memory |
| `code-review` | `/code-review` on a diff before integrating |
| `codex` | Second opinion on a determinism bug or a stuck investigation |
| `caveman` | Agent-to-agent prompts only. **Never** on repository files |
| `ui-ux-pro-max` | HUD, theme, and inspector layout work in `Hukbo.Client/UI` |
| `last30days` | Genre and community research, as used for `RESEARCHED.md` |

Off for this repo: `vercel`, `sentry`, `atlassian`, `php-lsp`,
`frontend-design` — no web app, no hosted service, no PHP. There is no C#/.NET
language-server plugin installed; `context7` plus the graph tools cover it.

Skills worth reaching for: `game-feel` (hit stop, screen shake — must stay
presentation-only), `game-ui-ux` (HUD anchoring, controller focus),
`verification-loop`, `tdd-workflow`.

## 9. Do not

- Add a hosted CI workflow, or delete `.github/` contents without saying so.
- Introduce rigid-body physics; distance checks and hitscan are the model.
- Cache targets, or add any unbounded cache.
- Save derived caches, render data, or metrics into a snapshot.
- Add a general-purpose ECS framework before a profiler demands it.
- Commit credentials, absolute local paths, `bin/`, `obj/`, or package output.
- Start terrain, pathfinding, morale, projectile ammunition, persistence
  migrations, multiplayer, or mod APIs before the gate that authorizes them.
