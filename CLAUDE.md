# Hukbo — Agent Instructions

Read this before touching anything. `AGENTS.md` is the standalone contract for
non-Claude agents — naming, commands, non-negotiables, workflow, historical
policy, do-nots. It stands on its own for tools that never load this file;
keep the two consistent.

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

### The second game: Sandata

Since 2026-08-07 this repository builds **two** games, not one. **Sandata** is a
deterministic, offline, top-down modern tactical game — a room-clearing
simulation in the Door Kickers tradition, in which small squads move through an
interior map, and in which both autonomous squad behaviour and hand-drawn player
orders are first-class. It is a separate product with a separate simulation, a
separate ruleset, a separate preset stream, and separate hashes. It is not a
mode of Hukbo, it is not a fork, and neither game may reach into the other's
simulation.

| Game | Status | Lives in |
| --- | --- | --- |
| **Hukbo** — pre-colonial Philippine battle, spectator only | Tactical layer built (v0.1) | `src/Hukbo.Core`, `src/Hukbo.Client`, `src/Hukbo.Headless` |
| **Sandata** — modern tactical room clearing, autonomous squads plus player orders | In development, no v0.1 yet | `src/Sandata.Core`, `src/Sandata.Client`, `src/Sandata.Headless` |

The only code the two share is `src/Hukbo.Shared.Core` — four determinism
primitives and nothing else, described in section 3. There is no shared
simulation type, no shared entity, no shared event, and no shared ruleset.

Sandata's binding documents are its design document,
`docs/plans/2026-08-07-sandata-scaffold-design.md`, which outranks everything
else about Sandata including this file's summary of it, and its plan document,
`docs/plans/2026-08-07-sandata-scaffold.md`. The design document's section 3 is
the reference graph and the tier-2 boundary, section 4 is Sandata's own
determinism contract and unit table, section 5 is the fourteen-stage tick
pipeline, section 8 is the squad model, section 12 is the map format, and
section 16 is the order layer.

**The name `Sandata` is not settled.** Design section 15 records it as an open
question along with whether shipped display strings use real weapon names or
generic aliases. Do not treat either as decided.

## 2. Naming

Use `Hukbo` (product), `hukbo` (slugs), `Hukbo.*` (projects, assemblies,
namespaces, tests). Never reintroduce the former `AutonomousArena` name in code,
config, docs, or instructions. Stale `AutonomousArena.*.nuget.*` files under
`obj/` are untracked build leftovers — ignore them, do not "fix" them. The
working-directory name `autonomous-arena` is legacy and is not the product name.

Generic arena-domain terms (`ArenaGame`, arena bounds) describe gameplay and are
not alternate product names.

Sandata follows the same rule under its own prefix: `Sandata` for the product,
`sandata` for slugs, and `Sandata.*` for projects, assemblies, namespaces, and
tests. The one deliberate exception is that Sandata's client and headless
entry points write their debug log through `Hukbo.Diagnostics` and read the
`HUKBO_LOG_LEVEL`, `HUKBO_LOG_CHANNELS`, and `HUKBO_LOG_DIR` environment
variables unchanged. Those names are not forked, because they configure the
logger and the logger is shared. The log file stem is `sandata`, so a Sandata
run writes `artifacts/logs/sandata-<utc>-<pid>.jsonl`.

## 3. Layout

Twelve projects, all in `Hukbo.slnx`.

```
src/Hukbo.Shared.Core  tier 1, shared by both games: FixedPoint, SplitMix64, Fnv1a, Facing16 — and nothing else
src/Hukbo.Core         Hukbo's authoritative simulation: tick pipeline, agents, events, RNG, hashing
src/Hukbo.Client       Hukbo's MonoGame DesktopGL shell: rendering, camera, UI, themes, input
src/Hukbo.Headless     Hukbo's determinism + benchmark runner, no window
src/Hukbo.Diagnostics  JSON Lines debug log shared by every client and headless runner; never referenced by either Core
src/Sandata.Core       Sandata's authoritative simulation: fourteen-stage tick pipeline, navigation, squads, weapons, orders, hashing
src/Sandata.Client     Sandata's MonoGame DesktopGL shell: rendering, camera, HUD, themes, order drawing, audio
src/Sandata.Headless   Sandata's determinism + navigation-benchmark runner, no window
tests/Hukbo.Core.Tests
tests/Hukbo.Client.Tests
tests/Sandata.Core.Tests
tests/Sandata.Client.Tests
scripts/               the only supported entry points (PowerShell 7)
tools/                 hand-run measurement harnesses; not in Hukbo.slnx, not in the gate
docs/                  design, plans, research, agent-role evidence
```

The reference graph, which is what the `.csproj` files actually declare:

```
Hukbo.Shared.Core  ←  Hukbo.Core  ←  Hukbo.Client, Hukbo.Headless
Hukbo.Shared.Core  ←  Sandata.Core  ←  Sandata.Client, Sandata.Headless
Hukbo.Diagnostics  ←  Hukbo.Client, Hukbo.Headless, Sandata.Client, Sandata.Headless
```

**`Hukbo.Shared.Core` is tier 1 and is exactly four files** —
`Mathematics/FixedPoint.cs`, `Determinism/SplitMix64.cs`, `Determinism/Fnv1a.cs`,
and `Movement/Facing16.cs`. They are shared because they are pure integer
determinism primitives with pinned golden vectors and no game concepts in them
at all. **Nothing else may move into that project without a design decision.** A
type that knows what an agent, an operator, a weapon, a tick stage, or a map is
belongs to one game and stays there, and a change to a tier-1 file is a change
to both games' hashes at once.

A tier-2 extraction — a `Hukbo.Shared.Client` holding presentation code both
clients duplicate — is **deferred**, and design section 3 states the boundary it
would have to be given first. Whether `Hukbo.Client` instead grants
`InternalsVisibleTo` to `Sandata.Client` is an open question and is not settled.

Neither `Hukbo.Core` nor `Sandata.Core` may reference MonoGame, the filesystem,
the network, windowing, audio, the wall clock, or `Hukbo.Diagnostics`. Neither
client may decide targeting, damage, retreat, or victory.

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

Every game-specific script takes `-Game`, validated to `Hukbo` or `Sandata` and
**defaulting to `Hukbo`**, so a command with no `-Game` runs exactly what it ran
before the second game existed:

```powershell
./scripts/run.ps1 -Game Sandata                          # launch Sandata
./scripts/test.ps1 -Configuration Release -Game Sandata  # Sandata's two suites
./scripts/benchmark.ps1 -Game Sandata -Seed 1            # Sandata's headless workload
./scripts/package.ps1 -Runtime win-x64 -Game Sandata
./scripts/verify.ps1 -Game Sandata                       # the gate stages against Sandata
```

The project paths live in one table, `scripts/_gametargets.ps1`, and no script
body hardcodes a project path any more; two tests in `Hukbo.Client.Tests` assert
both halves of that. `build.ps1`, `format.ps1`, and `bootstrap.ps1` take no
`-Game` because they operate on the whole solution, and `doctor.ps1` takes none
because it checks every project's lock file rather than one game's.

**`./scripts/verify.ps1` with no flag runs both games, since 2026-08-14.** It
runs the five Hukbo workloads, then Sandata's test suite and its seed-1
benchmark, and prints a banner between them. **The two are still two results and
must never be reported as one.**

An explicit `-Game` still runs exactly one game, byte-identically to before, and
that is what every scripted caller depends on. The guard is on whether the
caller bound the parameter at all, not on its value, because `-Game Hukbo` and
no `-Game` resolve to the same value and must not resolve to the same behaviour.
`ScriptDefaultsTests` pins both halves.

The default was Hukbo alone until 2026-08-14. Design section 14 held it there
until Sandata had a recorded, stable seed-1 baseline, so that a red Sandata
workload could never be mistaken for a red Hukbo one. It got one:
`stateHash A644B7F8A394885D` and `eventHash AEDE4D16B5E6FAAF` held unchanged
across four gate runs that day, through a pathfinding change, an inspector
change, an audio change, and a combat-rule change.

**The state hash moved later the same day and the event hash did not.** The
mission-never-ends fix writes each operator's selected intent into authoritative
state, and intent is state rather than an event, so the current baseline is
`stateHash 13EF0685BB46CA5E` with `eventHash AEDE4D16B5E6FAAF` unchanged. One
hash moving alone is the expected signature of that change and is what having
two independent hashes is for. The superseded figure and the full reasoning are
in `docs/development/testing.md`; quote the new one.

Sandata's core suite runs 1,113 tests in about **4.5 seconds** inside the gate.
It was 38 seconds until task 91, and 36 of those were a single `InlineData`
value on a single theory that ran the navigation benchmark for 2,000 ticks.
Before reasoning about suite cost here, get per-test durations
(`dotnet test ... --logger 'console;verbosity=normal'` prints one per test)
rather than trusting a summary — the received figure was wrong about which
tests were expensive for three sessions running.

A `Debug` run writes `artifacts/logs/hukbo-<utc>-<pid>.jsonl` with no flags at
all, and a Sandata `Debug` run writes `artifacts/logs/sandata-<utc>-<pid>.jsonl`.
Read either back with, for example:

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
  major .NET versions. Use `src/Hukbo.Shared.Core/Determinism/SplitMix64.cs`.
- Hash-set / dictionary iteration order may not affect gameplay.
- Fixed-point math (`src/Hukbo.Shared.Core/Mathematics/FixedPoint.cs`) for
  anything that reaches the state hash.
- Same seed + same build + same commands ⇒ identical state hash, event hash,
  winner, and ordered event stream. Changing enum numeric values, enum order,
  roster order, weights, or a hash mixer requires a **new preset version** plus
  new golden expectations.

Everything above binds `Sandata.Core` exactly as it binds `Hukbo.Core`. Sandata
adds the following, all from design section 4, and none of them is optional
because Sandata is the newer game:

- **Units.** Distance is a world unit (`wu`) stored as `FixedPoint` raw at a
  scale of 1024, with 1 metre equal to 16 world units. Time is an integer tick
  at a `TickRate` of 50, so one tick is exactly 20 milliseconds — Hukbo's rate
  is 20 Hz and the two are not interchangeable. Coarse angles reuse `Facing16`;
  fine angles are `Bam16`, a `ushort` binary angular measurement over a full
  turn of 65,536.
- **Milliseconds are authored, ticks are derived.** Every published weapon
  timing is stored as an integer millisecond count and converted once, at
  ruleset bake time, by the single pinned rule
  `ticks = (milliseconds * TickRate + 500) / 1000`. That rule's identifier and
  the tick rate both fold into `SandataRuleset.ContentHash`, so changing either
  requires a new preset version and new golden expectations.
- **Two independent hashes**, as in Hukbo: an FNV-1a state hash over
  authoritative state in fixed field order, and an FNV-1a event hash over the
  ordered event stream. A bug that moves state without emitting an event moves
  one and not the other.
- **Derived structures are never hashed and never snapshotted** — the nav grid,
  the clearance field, wall buckets, A\* scratch, line-of-sight results, the
  collision grid and pair list, and, the one worth stating aloud, **published
  path polylines**. A path is a pure function of the nav data, the start cell,
  and the goal cell: the *request* is authoritative and snapshotted, the
  *result* is not, and on resume every outstanding and published path is
  recomputed from its stored request record before the first tick executes.
- **The banned-token scan.** `float`, `double`, `System.Random`, `Math.Sqrt`,
  `Math.Atan2`, `Dictionary<`, `HashSet<`, and `PriorityQueue<` may not appear
  in `src/Sandata.Core` outside a doc comment, and a test enforces it. Use
  `FixedPoint.Sqrt`, `Cordic.Atan2`, the pinned sine table, and flat arrays
  indexed by node index. Heuristics are the integer octile form
  `10 * (max - min) + 14 * min`; there is no epsilon anywhere in `Sandata.Core`.
- **Fixed-latency path amortisation, never a per-tick search budget.** A path
  requested at tick `t` becomes valid at tick `t + PathLatencyTicks` regardless
  of how many searches the machine actually completed, so nothing branches on
  scheduling.
- **The preset is `SandataPresetId.ModernTacticalV1 = 1`**, append-only and
  pinned by a test, under the same rule Hukbo's presets follow.
- **`SandataRuleset.ContentHash` is `8_955_292_433_887_190_872`** and is pinned
  in `SandataRulesetTests`. Sandata's recorded seed-1 baseline is in
  `docs/development/testing.md` (the smoke rows are in
  `docs/development/smoke-checklist.md` and the superseded runs in
  `docs/development/measurement-history.md`), and its golden replay baselines are the two
  fixtures in `tests/Sandata.Core.Tests/Fixtures/seed-1-baseline.json`. That
  JSON file is where a Sandata run's digest belongs: exactly one absolute
  state-hash literal is permitted in C# under `tests/Sandata.Core.Tests/`, and
  it is already spent on `MissionStateTests.PreTask79cBaselineHash`.

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
  never `Debug.WriteLine`, never a bespoke text file. A test scans the whole of
  `src/` — both games — and fails the build if anything but the four `Program.cs`
  entry points touches the console.
- **Neither `Hukbo.Core` nor `Sandata.Core` may ever reference
  `Hukbo.Diagnostics`.** A simulation is forbidden the filesystem and the wall
  clock, and the logger needs both. Observe it from outside, reading state the
  caller already holds. Tests assert the absence of the assembly reference for
  `Hukbo.Core` and `Hukbo.Shared.Core` in `DiagnosticLoggingBoundaryTests`, and
  for `Sandata.Core` in `SandataSourceHygieneTests`, each paired with the
  matching headless runner as the positive control that proves the assertion can
  fail.
- New Sandata `ev` identifiers are `const` members on the same `LogEvents`
  catalog, under a `sandata.` prefix.
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
   `docs/development/smoke-checklist.md`. Compilation, unit tests, or a
   window-opening probe do not let you flip a row to `PASS`. Leave untouched
   rows `PENDING`; report `BLOCKED` honestly.
5. Move finished plans to `docs/archives/<YYYY-MM-DD>/`, dated for the day of
   archiving, and add the "Archived: reference only" banner under the title.

**`docs/archives/` is deprecated by definition.** It is the dump for finished and
abandoned work, kept only so a past decision can be traced to its reasoning.
Never execute an archived plan, never treat its versions or tooling references as
current, and never cite one as justification for a change. Active work lives in
`docs/plans/`; the live contract is this file, `SIMULATION-GAME-STANDARDS.md`,
`docs/development/testing.md`, `docs/development/smoke-checklist.md`, and
`.claude/skills/`. Archived files are grouped into dated subfolders, and that
folder's own `README.md` holds the layout rules.

**No file outside `docs/archives/` may link to a file inside it.** The folder is
deleted periodically, so a path into it is a path that breaks. Name the archived
document in prose if a reader needs to know it existed; never write the path as a
link. This applies to documentation, plans, research notes, skills, and source
comments alike.
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
- Specific cultural identifications appear in player-facing UI only in pair
  form — the Filipino name, an em dash, and a plain English descriptor
  (`Kampilan — Great Blade`, `Wasay — War Axe`) — and only with an evidence
  tier recorded in metadata and shown in the agent inspector. A cultural
  identification never appears as a bare, unqualified label, and a name whose
  earliest attestation postdates the depicted period by more than a century is
  not used at all. That final clause is what keeps this policy load-bearing
  rather than decorative: it is the rule that excluded the panabas, whose
  first documented mentions are nineteenth-century, and the next weapon added
  has to clear the same bar.
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

The `codebase-memory-mcp` graph for this repository is the project named
**`hukbo-main`**. Pass that name to `search_graph`, `query_graph`, `trace_path`,
`get_code_snippet`, and `get_architecture`. Do not use `hukbo` — that project's
database is corrupt and locked, and every attempt to re-index over it kills the
indexing worker. It is scheduled for deletion once no server process holds its
file handle.

Re-index through the CLI, never through the MCP tool. The MCP-hosted worker
crashes on this repository; the same operation succeeds as a separate process:

```powershell
& "$HOME/.local/bin/codebase-memory-mcp.exe" cli index_repository `
  '{"repo_path":"C:/Users/boazs/webdev/autonomous-arena","name":"hukbo-main","mode":"full"}'
```

The indexer skips `.claude/`, `.git/`, `artifacts/`, `tools/mix-output/`, and
every `bin/` and `obj/` on its own, so the four live worktrees under
`.claude/worktrees/` never pollute the root index. A worktree that needs its own
graph gets indexed as its own project, keyed by branch. Do not enable
`persistence`; the `.codebase-memory/graph.db.zst` artifact it writes is a
stale partial from an earlier crashed run and is not a trustworthy bootstrap.

Two graph servers are installed and both work. `tokensave` stays the default —
it is the one the user-level rules mandate, it is git-aware, and it carries the
edit and metrics tools. Reach for `codebase-memory-mcp` when you specifically
want Cypher through `query_graph`, call-chain tracing through `trace_path`, or
Leiden cluster detection through `get_architecture`.

Project-local skills in `.claude/skills/` — prefer these over generic advice:

| Skill | Covers |
| --- | --- |
| `hukbo-orchestrate` | The three-stage pipeline in §10 as an invocable procedure: parallel read-only research, one planner, scoped implementers, worktree isolation, the prompt contract |
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
| `caveman` | **Required** on every agent-to-agent prompt. **Never** on repository files |
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
- Import Arch or another general-purpose ECS without a new profile and design
  decision. Arch 2.1.0 is a reference implementation only; reuse compatible,
  measured techniques under `SIMULATION-GAME-STANDARDS.md` section 15.
- Commit credentials, absolute local paths, `bin/`, `obj/`, or package output.
- Start terrain, pathfinding, morale, ammunition (quiver sizes, resupply, or
  any stock-and-consumption model for a projectile), persistence migrations,
  multiplayer, or mod APIs before the gate that authorizes them. Projectiles
  and projectile flight time were authorized on 2026-08-07 for the
  ranged-units package (archived under the title "Ranged units — plan") alone;
  ammunition was not authorized and stays deferred. Sandata's own navigation
  and pathfinding are authorized by its design document and are not covered by
  that bar; Hukbo's are not.
  **Sandata's magazine and reload were authorised on 2026-08-14**, in the
  narrow form its own design recommends: a round is consumed per shot, a
  reload costs the firearm's authored `ReloadMs` converted by the one pinned
  rule, and **spare magazines are infinite**. A finite spare count is a
  stock-and-consumption economy and is the thing this bullet exists to stop;
  it stays unauthorised. Hukbo's ammunition remains deferred entirely.
- Let either game reach into the other. No `Sandata.*` project may reference a
  `Hukbo.Core` or `Hukbo.Client` type, and no `Hukbo.*` project may reference a
  `Sandata.*` type. Move code into `Hukbo.Shared.Core` only under section 3's
  tier-1 rule, and never as a shortcut around this one.
- Report one game's green as the other's. A bare `./scripts/verify.ps1` has run
  both since 2026-08-14, but they remain two workloads with two results, and
  `-Game Hukbo` still runs Hukbo alone.
- Run `./scripts/sfx.ps1` for Sandata beyond the slice authorized on
  2026-08-11 and extended on 2026-08-12. That slice now covers forty files —
  ten variants each of `gun-762x39-single-close`, `gun-762x39-single-indoor`,
  `gun-9x19-single-close`, and `gun-9x19-single-indoor`, covering the
  AK-pattern rifle and the Glock-pattern pistol in the two acoustic
  environments an interior map reaches — and both those files and the
  MonoGame-backed playback path that plays them are already committed; the
  full provenance is recorded in `src/Sandata.Client/Content/Audio/README.md`.
  Those four rows are the only ones raised above the catalog's default six
  declared variants, because they are the only rows with real files on disk
  and because `ShotSlotResolver` picks a variant uniformly across a row's
  declared count, so a higher declared count is the only way more of the
  generated takes are ever heard; every other row still declares six and
  remains wholly ungenerated. The remaining catalog is still 114 slots
  expanding to 572 variant files in total, roughly 114,400 ElevenLabs
  credits — 106 slots and 540 files until 2026-08-14, when the automatic loop
  and tail rows were declared for every caliber family rather than the six
  rifle calibers alone, closing a crash an automatic-capable pistol would have
  hit on its first round. Declaring a row generates no file and spends no
  credit, and that remaining spend is **not authorized**; design section 15
  keeps it behind a reviewed dry-run manifest, and `scripts/sfx-manifest.ps1`
  is the network-free script that produces one.

## 10. Agent orchestration

Non-trivial work runs through the following shape. Each stage consumes the
output of the stage above it, and no stage starts before the one it depends on
has actually reported.

```
Research agents (plan and knowledge)
         ↙        ↘
Requirements     Existing code
        ↘        ↙
Task planner agent (list of granular tasks)
        ↓
Developer agent
```

**Stage 1 — research, in parallel.** One group establishes what the change has
to do: the requirement, the acceptance criteria, the user-visible effect, the
historical evidence when the change touches weapons or culture. The other group
establishes what the repository already does: the existing types, the tick
stage the change lands in, the tests that already cover the area, the
conventions to match. These two groups are independent, so run them at the same
time rather than one after the other.

**Stage 2 — planning.** A single planner agent reads both research outputs and
produces one ordered list of granular tasks, each small enough for one agent to
finish, with its files, its verification, and its dependencies named. The
planner writes the plan document described in section 6. It does not write
code.

**Stage 3 — implementation.** Developer agents execute the task list. Give each
one an explicit, non-overlapping set of files; two agents editing the same file
in parallel is a merge conflict you created on purpose.

Rules that bind this pipeline:

- **Eight parallel agents is the ceiling.** Fan out to at most eight at once.
  Beyond that the results arrive faster than they can be read, and the review
  quality drops below what the work is worth. Prefer fewer, better-scoped
  agents.
- **Research and planning agents are read-only.** Only the implementation stage
  writes files. If a research agent proposes an edit, that proposal becomes a
  task in the plan, not an edit.
- **Never spawn an Explore agent for code research in this repository.** Code
  discovery goes through the `tokensave` MCP tools and the codebase-memory
  graph, as section 8 requires. That rule applies inside the orchestration
  pipeline exactly as it applies outside it, and it applies to the prompts you
  hand to sub-agents.
- **Every agent prompt names its evidence and its return format.** State the
  files, the symbols, and the exact shape of the answer you expect back. An
  agent that returns prose you then have to re-read has cost more than it
  saved.
- **The canonical gate is not delegated.** `./scripts/verify.ps1` runs once,
  after integration, and its real output is the evidence. No sub-agent's report
  substitutes for it, and no agent may flip a manual smoke-checklist row.
- **Coding tasks run on Sonnet.** Every agent that writes or edits code in this
  repository is dispatched on Sonnet, every time, with no per-task exception.
  Research and planning agents keep whatever model their own agent file
  declares; this rule binds the implementation stage. Confirm the roster with
  `bo agents` before spawning, since an agent's declared model can change.
- **Every agent prompt is caveman-compressed.** Orchestration is not authorized
  without it: run the `caveman` skill over each sub-agent prompt before
  dispatching it, at every stage of the pipeline. Repository documentation,
  commits, pull requests, and user-facing prose stay in full English — see the
  rule in section 6.

The `hukbo-orchestrate` skill is the invocable form of this section — the
worktree setup, the two research groups, the planner audit, the prompt contract,
and the known failure modes, in one place. Invoke it rather than restating the
diagram in a prompt.

The full agent roster, the model each one runs on, and which ones are allowed
to write live in the user-level `~/.claude/rules/agents.md`. The `/flow:*`
slash commands run a general version of this pipeline and suit work in other
repositories; inside Hukbo they do not know about worktree isolation, the
`tokensave`-only discovery rule, or the canonical gate, so `hukbo-orchestrate`
takes precedence here.
