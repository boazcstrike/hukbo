# Debug Logging Standard — Plan

Date: 2026-07-27
Design: `2026-07-27-debug-logging-standard-design.md`

Read the design document first. This plan carries the ordered task list and the
verification criteria only; every "why" lives in the design.

## Task list

### Phase 1 — The shared project

- [x] 1.1 Create `src/Hukbo.Diagnostics/Hukbo.Diagnostics.csproj`. Library, no
      `PackageReference` — `System.Text.Json` is not needed because the writer
      builds lines directly, and nothing else is required. No reference to
      `Hukbo.Core`.
- [x] 1.2 `LogLevel.cs` — `Off, Error, Warning, Information, Debug, Trace` with
      the four-letter wire names `err`, `warn`, `inf`, `dbg`, `trc`.
- [x] 1.3 `LogChannel.cs` — `[Flags]` enum: `Boot, Assets, Settings, Simulation,
      Audio, Input, Ui`, plus an `All` composite. Flags so channel filtering is
      one bitmask test.
- [x] 1.4 `LogValue.cs` — `readonly struct` union over `long`, `double`, `bool`,
      `string?` with a `LogValueKind` discriminator and implicit conversions
      from `int`, `long`, `ulong`, `float`, `double`, `bool`, `string?`. No
      boxing on any path.
- [x] 1.5 `LogEvents.cs` — every `ev` identifier from design section 6 as a
      `const string`, grouped by channel, sorted within each group.
- [x] 1.6 `LogOptions.cs` — resolves level and channels from `HUKBO_LOG_LEVEL`
      and `HUKBO_LOG_CHANNELS`, falling back to the build configuration
      default. A malformed value writes one line to standard error and falls
      back; it never throws and never silently accepts.
- [x] 1.7 `LogPaths.cs` — repository-root discovery by walking ancestors of
      `AppContext.BaseDirectory` for `Hukbo.slnx`, the
      `artifacts/logs/hukbo-<yyyyMMdd-HHmmss>-<pid>.jsonl` name, the
      `HUKBO_LOG_DIR` override, and the twenty-file retention sweep.
- [x] 1.8 `JsonlLogSink.cs` — owns the `StreamWriter` and the reused
      `StringBuilder`, escapes strings per JSON, emits the six leading fields in
      fixed order, flushes at `Warning` and above, exposes `Flush()` and
      implements `IDisposable`.
- [x] 1.9 `DiagnosticLog.cs` — the facade. `IsEnabled(level, channel)`, the
      `Write` overloads for zero through six name/value pairs, `SetTick(long)`
      so call sites do not thread the tick everywhere, `Flush()`, and a
      `DiagnosticLog.Disabled` singleton that is a no-op for tests and for the
      `Release` default.
- [x] 1.10 Add the project to `Hukbo.slnx` under `/src/`, add the project
      reference to `Hukbo.Client` and `Hukbo.Headless`, and generate
      `packages.lock.json` for the new project.

### Phase 2 — Client instrumentation

- [x] 2.1 `Program.cs` — construct the log, emit `boot.started`, wrap the game
      in `try/finally` so `boot.stopped` and a final flush always run, and emit
      `boot.crashed` with the exception type and message before the existing
      standard-error line.
- [x] 2.2 `ArenaGame` constructor — accept the log, emit
      `assets.theme.loaded` or `assets.theme.fallback`, `settings.loaded` or
      `settings.invalid`, and `sim.scenario.built`.
- [x] 2.3 `ArenaGame.LoadContent` — `assets.font.loaded` / `assets.font.failed`,
      `assets.sound.scanned` with the ready/missing/failed slot counts, one
      `assets.sound.missing` or `assets.sound.loadFailed` per bad slot, and
      `audio.player.attached`. Emit `boot.window.created` with the backbuffer
      size.
- [x] 2.4 `SoundDirector.Resolve` — one `audio.cue` per resolved cue carrying
      `slot`, `status`, `variant`, `gain`, and `voices`. The director takes the
      log through its constructor, defaulting to `DiagnosticLog.Disabled` so
      every existing test construction keeps compiling unchanged.
      `audio.mute.toggled` on `ToggleMute`. `audio.frame` at `trc` in
      `BeginFrame`.
- [x] 2.5 `ArenaGame.Update` pointer chain — one `input.pointer` per press
      carrying `button`, `x`, `y`, and `consumedBy`, where `consumedBy` is the
      name of the first surface that claimed it or `arena` or `none`. One
      `input.key` per pressed key that mapped to a `ClientCommand`.
      `input.focus.changed` when the event log's keyboard focus target moves.
- [x] 2.6 `ArenaGame.AdvanceSimulation` — `sim.tick` every tick at `trc` and
      every 256 ticks at `dbg`, carrying tick, both survivor counts, the tick's
      event count, and the state hash. `sim.outcome` once when the outcome
      leaves `Ongoing`. `sim.reset`, `sim.speed.changed`, and
      `sim.playback.changed` on the corresponding commands. Call `Log.SetTick`
      once per advanced tick so no other call site has to pass it.
- [x] 2.7 `ClientSettingsStore` and `UiThemeManager` — `settings.saved`,
      `settings.saveFailed`, `settings.changed`. Both take the log through the
      constructor with a disabled default.
- [x] 2.8 Flush the log at the end of `ArenaGame.Update`.

### Phase 3 — Headless and scripts

- [x] 3.1 `HeadlessRunner.TryParseArguments` — accept `--log-level`,
      `--log-channels`, `--log-dir`. They outrank the environment. Update the
      usage string. Keep the existing "unsupported argument" rejection working
      for anything else. Named `--log-dir` rather than `--log-output` so it
      cannot be confused with the existing `--output` report path.
- [x] 3.2 `HeadlessRunner.Execute` — `sim.scenario.built` at the start,
      `sim.tick` on the same cadence rule as the client, `sim.outcome` at the
      end, and one `sim.mismatch` at `err` carrying the tick, both state hashes,
      and both outcomes when `firstMismatchTick` is set. That last one is the
      single highest-value line in the whole facility.
- [x] 3.3 `scripts/run.ps1` — add `-LogLevel` and `-LogChannels`, set the
      environment variables for the child process only, and print the resolved
      log directory before launching.
- [x] 3.4 `scripts/benchmark.ps1` — add `-LogLevel` defaulting to `off`, passed
      through as `--log-level`, so the gate's workload is unchanged by default.

### Phase 4 — Enforcement tests

- [x] 4.1 `Hukbo.Core.Tests` — assert `typeof(Scenario).Assembly` has no
      referenced assembly named `Hukbo.Diagnostics`.
- [x] 4.2 `Hukbo.Client.Tests` — walk up to the repository root, glob
      `src/**/*.cs` excluding `bin/` and `obj/`, and assert no `Console.` usage
      outside `src/Hukbo.Client/Program.cs`,
      `src/Hukbo.Headless/Program.cs`, and the `LogOptions` fallback warning.
- [x] 4.3 `Hukbo.Client.Tests` — reflect over `LogEvents` and assert every
      constant is unique, matches `^[a-z]+(\.[a-zA-Z]+)+$`, and carries a first
      segment that names a declared channel. Declaration order is deliberately
      not asserted: reflection does not promise source order, and a flaky test
      would be worse than leaving tidiness to review.
- [x] 4.4 `Hukbo.Core.Tests` — run the seed-1, 200-agent, 2,000-tick headless
      workload twice, once with logging off and once at `trc` on all channels
      writing to a temporary directory, and assert identical state hash, event
      hash, outcome, and ordered event stream.
- [x] 4.5 `Hukbo.Client.Tests` — assert `DiagnosticLog.Disabled.Write(...)` with
      six pairs allocates zero bytes, measured with
      `GC.GetAllocatedBytesForCurrentThread`.
- [x] 4.6 `Hukbo.Client.Tests` — round-trip tests for `JsonlLogSink`: field
      order, JSON string escaping including backslashes and quotes, level
      threshold filtering, channel mask filtering, and the `seq` counter.
- [x] 4.7 `Hukbo.Client.Tests` — `LogPaths` retention keeps exactly the newest
      twenty files and deletes the rest, against a temporary directory.

### Phase 5 — Rules for future agents

- [x] 5.1 `CLAUDE.md` — a new numbered section stating the standard, placed
      after the non-negotiables. Mirror it into `AGENTS.md`; the two files are
      required to stay consistent.
- [x] 5.2 `.claude/skills/hukbo-debug-logging/SKILL.md` — how to turn the log
      on, how to read it, how to add an event, and the four mechanical rules.
      Register it in the `CLAUDE.md` §8 skill table.
- [x] 5.3 `docs/development/testing.md` — a section on capturing and attaching a
      log while running the manual smoke checklist, and the reminder that a log
      is evidence of what the code did, never a substitute for a human
      confirming what the screen showed.

### Phase 6 — Verification

- [x] 6.1 `./scripts/format.ps1 -Verify`
- [x] 6.2 `./scripts/verify.ps1` and record the exact output.
- [x] 6.3 **PASS** — the repository owner ran
      `./scripts/run.ps1 -Configuration Debug` on an interactive Windows desktop
      on 2026-07-27 and the produced log was read back. See "Interactive run"
      below. This covers verification criterion 3; criteria 4 and 5 remain
      `PENDING`.

## Verification criteria

The change is done when all of the following hold. Criterion 3 is **PASS** as of
the 2026-07-27 interactive run recorded below. Criteria 4 and 5 remain
**PENDING**: they also require a person at an interactive Windows desktop, and
nothing about compiling, testing, or probing the headless runner may be used to
flip them.

1. `./scripts/verify.ps1` passes and its output is recorded verbatim in the
   completion note. A claim of verification without pasted output does not
   count.
2. The seed-1 determinism test in 4.4 passes, proving the logger cannot move a
   hash.
3. `./scripts/run.ps1 -Configuration Debug` produces exactly one file under
   `artifacts/logs/`, every line of which parses as JSON, and the file contains
   at least one line from each of `boot`, `assets`, `settings`, `sim`, `audio`,
   and `input`.
4. The same command in `Release` with no environment variable set produces no
   file at all.
5. `HUKBO_LOG_LEVEL=off` in a `Debug` run produces no file, and
   `HUKBO_LOG_CHANNELS=audio` at `dbg` produces a file whose every line has
   `"ch":"audio"`.
6. `dotnet run --project src/Hukbo.Headless -- --agents 200 --ticks 10000
   --seed 1 --log-level err` reports the same `stateHash` and `eventHash` as the
   recorded seed-1 baseline.
7. The four enforcement tests in phase 4 fail when their rule is deliberately
   broken. Verified by temporarily breaking each one, not by assuming.

## Completion note — 2026-07-27

### Canonical gate

`./scripts/verify.ps1`

```
Hukbo prerequisite doctor
Repository: <redacted local checkout path>
[PASS] Platform: Windows x64
[PASS] PowerShell: 7.6.4
[PASS] git version 2.55.0.windows.3
[PASS] Git LFS: installed (optional; no tracked LFS assets are currently required)
[PASS] .NET SDK: 10.0.302
[PASS] MonoGame packages are centrally pinned: MonoGame.Content.Builder.Task 3.8.5, MonoGame.Framework.DesktopGL 3.8.5
[PASS] Required prerequisites and repository configuration are present.
[PASS] Locked package restore completed.
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
[PASS] Release repository tests completed.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.

Total tests: 608
     Passed: 608
```

The 200-agent, 10,000-tick, seed-1 workload:

| Field | Value |
| --- | --- |
| `measuredTicks` | 1154 |
| `outcome` | `Faction1Victory` |
| `faction0Survivors` / `faction1Survivors` | 0 / 3 |
| `eventHash` | `D379B60B2E30FFFC` |
| `stateHash` | `5BEBA7A68F69BE0D` |
| `deterministic` | `true` |
| `firstMismatchTick` | `null` |
| `allocatedBytes` | 71698480 |

Both hashes are identical to the recorded seed-1 baseline in
`docs/development/testing.md`, which is the point: the gate builds `Release`,
where logging resolves to `off`, so nothing about this change reached the
simulation.

### Non-interactive probes

Against `Release` headless, 20 agents, 400 ticks, seed 1:

- `--log-level dbg --log-dir <temp>` wrote one file containing
  `sim.scenario.built`, one sampled `sim.tick`, and `sim.outcome`. Every line
  parsed as JSON and carried all six leading fields.
- `--log-channels audio` produced a file with no simulation lines.
- `--log-level off` created no directory and no file.
- `--log-level loud` was rejected with
  `Argument error: '--log-level' must be one of off, err, warn, inf, dbg, trc.`

### Enforcement tests

26 new tests pass: 23 in `Hukbo.Client.Tests`, 3 in `Hukbo.Core.Tests`.

Criterion 7 asks that each enforcement test be proven to fail when its rule is
broken. Honest status:

| Test | Proven to fail when broken |
| --- | --- |
| `SourceHygieneTests.OnlyTheEntryPointsWriteDirectlyToTheConsole` | **Yes.** A comment containing `Console.` was temporarily added to `src/Hukbo.Diagnostics/LogSampling.cs`; the test failed, and the file was restored. |
| `DiagnosticLoggingBoundaryTests.CoreDoesNotReferenceTheDiagnosticsAssembly` | Indirectly. Breaking it means adding a real project reference to `Hukbo.Core`, which was not done. A positive control asserting `Hukbo.Headless` *does* carry the reference was added instead, so the test cannot pass vacuously. |
| `LogEventCatalogTests` | Not deliberately broken. |
| `FullTraceLoggingDoesNotChangeTheSimulationResult` | Not deliberately broken. The test does assert the traced run wrote a non-empty file, so it cannot pass with the logger unplugged. |
| `ADisabledWriteAllocatesNothing` | Not deliberately broken. |

### Interactive run — 2026-07-27, `artifacts/logs/hukbo-20260727-044454-19440.jsonl`

The repository owner ran `./scripts/run.ps1 -Configuration Debug` on a Windows
desktop. One file, 625,675 bytes, 3,352 lines, **zero unparseable**. A 300-agent
seed-1 battle played to `Faction0Victory` at tick 1835 over a 121-second
session.

| Channel | Lines |
| --- | --- |
| `audio` | 3294 |
| `input` | 39 |
| `sim` | 10 |
| `assets` | 3 |
| `boot` | 3 |
| `settings` | 3 |

All six instrumented channels emitted, which satisfies verification criterion 3.
Criteria 4 and 5 — a `Release` run producing no file, and the `off` and
channel-filter overrides in `Debug` — were not exercised and stay `PENDING`.

What the log showed, none of which was observable before:

- **The gain compensation curve is fully exercised.** Cue gain ranged from
  0.650 (one voice) down to 0.1099 (about 35 voices), with the distribution
  peaking around nine to ten concurrent voices. This is the measurement the
  gain-compensation work needed and could not previously take.
- **The cue budget suppressed nothing at all.** All 3,293 `audio.cue` lines
  report `Played`: zero `Suppressed`, zero `Refused`, zero `Missing`, across a
  300-agent battle. `SoundCueBudget` is not binding at this scale. Whether that
  is correct is a separate question, now answerable with evidence.
- **Twenty-six of thirty-six left-presses were claimed by nothing.** Every one
  had a position off the viewport — negative coordinates, or beyond 1280x720 —
  so they were presses in another window or on another monitor that MonoGame
  still reports. The game correctly ignores them. Reported as `none`, they read
  as dead clicks on our own UI, so `consumedBy` now distinguishes `outside`.
- **`settings.json` is read three times during startup**, by
  `GoreIntensityManager`, `UiThemeManager`, and the composition load. Harmless,
  but visible for the first time.
- **The `sim.outcome` guard works.** `CompleteMatch` ran every frame for the
  27 seconds between the decision at tick 1835 and the window closing, and
  emitted exactly one line.
- **`audio` is 98% of the volume.** For a readable session log, filter with
  `-LogChannels sim,input,boot,assets,settings`.

### Changes made in response to that run

Both landed after the run above, so they are not reflected in that log file:

1. `audio.cue` dropped its redundant `tick` payload field — the line's own `t`
   already carries it — and gained `voices`, the count the gain was derived
   from. Recovering the voice count by inverting the gain formula worked, but
   requiring a reader to know the formula is not a contract.
2. `input.pointer` reports `consumedBy: "outside"` for a press whose position
   lies off the viewport, instead of conflating it with a genuine dead click.

The canonical gate was re-run after both and passes: 608 tests, seed-1
`eventHash D379B60B2E30FFFC`, `stateHash 5BEBA7A68F69BE0D`, unchanged.

## Risks

| Risk | Mitigation |
| --- | --- |
| A `dbg` call site inside the frame loop allocates and pushes the per-tick budget | The zero-allocation test in 4.5, plus `LogValue` being a struct union with no boxing path |
| Someone later adds a log call inside `Hukbo.Core` | The assembly reference test in 4.1 makes it a build-time failure, not a review question |
| The sampled `sim.tick` hash call changes timing enough to matter | It is off in `Release`, off in the gate, and 4.4 proves the outcome is unchanged even at `trc` |
| Twenty retained logs is the wrong number | It is one constant with a name; changing it is a one-line change |
| The source-scanning test in 4.2 is brittle when run from an unexpected working directory | It resolves the repository root the same way `LogPaths` does, and skips with an explicit assertion message if the root cannot be found |
