---
name: hukbo-debug-logging
description: Hukbo's JSON Lines debug log — turning it on, reading it back, filtering it, and adding a new event without breaking the standard. Use when debugging anything about a run, when asked why a click did nothing or why a sound did not play, when investigating a determinism mismatch, when a log is empty or missing, when adding instrumentation to any subsystem, or when deciding what level and channel a new line belongs at. Covers the six leading fields, the LogEvents catalog rule, the Hukbo.Core boundary, the allocation rule, and the four tests that enforce all of it.
---

# Hukbo's debug log

## What this is

`src/Hukbo.Diagnostics` writes one JSON object per line to
`artifacts/logs/hukbo-<yyyyMMdd-HHmmss>-<pid>.jsonl`. It exists so that an agent
asked to investigate a session has a durable record of what the game actually
did, rather than a description of what someone saw.

It is a development and testing facility. It is on by default in `Debug`, off in
`Release`, produces no in-game effect, and never touches the network.

## Turning it on

A `Debug` run logs at `dbg` with every channel, with no flags at all:

```bash
./scripts/run.ps1 -Configuration Debug
```

The script prints the resolved level and directory before launching, and the
first line of the log repeats them along with the absolute path.

Overrides, in order of precedence — command line, then environment, then the
build configuration default:

```bash
./scripts/run.ps1 -Configuration Debug -LogLevel trc -LogChannels audio,input
```

| Variable | Accepts | Default |
| --- | --- | --- |
| `HUKBO_LOG_LEVEL` | `off`, `err`, `warn`, `inf`, `dbg`, `trc` | `dbg` in `Debug`, `off` in `Release` |
| `HUKBO_LOG_CHANNELS` | `all`, or a comma-separated list | `all` |
| `HUKBO_LOG_DIR` | a directory path | `artifacts/logs` under the repository root |

The headless runner takes `--log-level`, `--log-channels`, and `--log-dir`,
which outrank the environment. Note it is `--log-dir`, not `--log-output` —
`--output` already means the run report's JSON path.

```bash
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1 -LogLevel err
```

A malformed value is reported on standard error and falls back to the default.
It is never silently treated as `off`, because an empty log and a quiet run look
identical otherwise.

## Reading it back

```powershell
$log = Get-ChildItem artifacts/logs -Filter *.jsonl | Sort-Object Name | Select-Object -Last 1
Get-Content $log | ConvertFrom-Json | Where-Object ch -eq 'audio' | Select-Object -First 40
```

Because every line is one flat object, filtering is ordinary object filtering:

```powershell
Get-Content $log | ConvertFrom-Json | Where-Object { $_.ev -eq 'input.pointer' -and $_.consumedBy -ne 'arena' }
Get-Content $log | ConvertFrom-Json | Where-Object lvl -in 'err','warn'
Get-Content $log | ConvertFrom-Json | Where-Object ev -eq 'audio.cue' | Group-Object status
```

For a quick look without parsing, `Select-String` on the `ev` value works, since
identifiers are stable and never contain data.

## The line format

Six leading fields, always present, always in this order, then that event's own
payload fields.

| Field | Meaning |
| --- | --- |
| `seq` | Monotonic counter from 1. The only unambiguous ordering key. |
| `t` | Simulation tick, or `-1` when there is no tick context. |
| `ms` | Milliseconds since process start. |
| `lvl` | `err`, `warn`, `inf`, `dbg`, `trc`. |
| `ch` | `boot`, `assets`, `settings`, `sim`, `audio`, `input`, `render`, `ui`. |
| `ev` | Stable dotted identifier. The machine key. |

## What each channel tells you

| Channel | Reach for it when |
| --- | --- |
| `boot` | The game did not start, or you need the configuration and level a session ran at. |
| `assets` | A sound is silent, a font looks wrong, or the theme colors are not the edited ones. `assets.sound.scanned` gives the whole binding table in one line. |
| `settings` | A saved preference did not survive a relaunch. `settings.invalid` names the field that failed validation. |
| `sim` | Anything about the battle. `sim.mismatch` in a headless run is the single most valuable line in the facility. |
| `audio` | A cue did not sound, or the mix is wrong. `audio.cue` carries `variant`, `gain`, and `voices`, which no on-screen panel shows. |
| `input` | A click or a shortcut did nothing. `input.pointer` carries `consumedBy`, which names the surface that swallowed the press, or `arena`, or `none` for a dead click inside the window, or `outside` for a press that landed in another window or on another monitor. |
| `render` | The spectator reports lag, stutter, or warriors that jump instead of walking. `render.window` is one line per second of wall time; `render.starved` fires at `warn` when the simulation could not keep pace. |

### Answering "the game went laggy"

A slow frame and a starved simulation look identical from the spectator's
chair and are different defects. The catch-up loop in `AdvanceSimulation`
runs several ticks in one frame, so the battle can hold its exact tick rate
while the picture updates twice a second — the warriors move on time, the
screen does not.

`render.window` closes every second of wall time and carries `frames`,
`elapsedMs`, `meanMs`, `worstMs`, `worstUpdateMs`, `worstDrawMs`, and
`simTicks`. `frames` reads directly as frames per second. `worstUpdateMs` and
`worstDrawMs` are not promised to come from the same frame as `worstMs`: when
`worstMs` is large and both of the others are small, the time went to present,
to the driver, or to another process, not to the game's own code.

`render.starved` fires at `warn` only when the frame arrived so late that the
accumulator clamp dropped whole ticks. Its absence from a laggy session is
itself the finding — the simulation kept pace and the complaint is frame rate
or on-screen movement, not tick delivery.

```powershell
Get-Content $log | ConvertFrom-Json |
  Where-Object ev -in 'render.window', 'render.starved' |
  Select-Object ms, ev, frames, meanMs, worstMs, simTicks, starvedFrames
```

`render.frame` is the per-frame `trc` line behind both, for locating a single
stall exactly: `./scripts/run.ps1 -Configuration Debug -LogLevel trc -LogChannels render`.

### The two highest-value lines

`sim.mismatch` fires at `err` when the headless runner's two simulations diverge.
It carries both ticks, both outcomes, and both state hashes at the moment they
parted. Pair it with the `firstMismatchTick` field of the run report and the
`hukbo-determinism-change` skill.

`input.pointer` carries `consumedBy`. The priority chain is match summary,
control bar, event log, sound log, inspector, then arena, and the first surface
to claim a press wins. A click that "did nothing" is nearly always a click a
surface above the intended one swallowed, and that is invisible from outside the
debugger.

## Adding an event

1. Add the identifier to `src/Hukbo.Diagnostics/LogEvents.cs` as a
   `const string`, in the group for its channel. The identifier's first segment
   must be the channel's wire name.
2. Call `Write` at the call site:

```csharp
_log.Write(
    LogLevel.Debug,
    LogChannel.Audio,
    LogEvents.AudioCue,
    "slot", sound.ToString(),
    "status", status.ToString(),
    "gain", gain);
```

Overloads exist for zero through six name/value pairs. Payload values convert
implicitly from `int`, `long`, `ulong`, `float`, `double`, `bool`, and
`string?`.

3. If producing a payload value costs anything — a scan, a hash, an allocation —
   guard it:

```csharp
if (!_log.IsEnabledFor(LogLevel.Debug, LogChannel.Simulation))
{
    return;
}
```

4. A class that logs takes `DiagnosticLog? log = null` in its constructor and
   stores `log ?? DiagnosticLog.Disabled`, so every existing test construction
   keeps compiling and no test has to supply one.

## The rules, and what enforces them

| Rule | Enforced by |
| --- | --- |
| `Hukbo.Core` never references `Hukbo.Diagnostics` | `DiagnosticLoggingBoundaryTests.CoreDoesNotReferenceTheDiagnosticsAssembly`, with a positive control on `Hukbo.Headless` |
| Only the two `Program.cs` files touch the console | `SourceHygieneTests.OnlyTheEntryPointsWriteDirectlyToTheConsole`, which scans `src/` |
| `ev` identifiers are unique, lowercase-dotted, and channel-prefixed | `LogEventCatalogTests` |
| Logging cannot change a simulation | `DiagnosticLoggingBoundaryTests.FullTraceLoggingDoesNotChangeTheSimulationResult` |
| A disabled call allocates nothing | `DiagnosticLogTests.ADisabledWriteAllocatesNothing` |

Do not weaken any of these to get a line written. If a rule is in the way, the
instrumentation is in the wrong place.

## Choosing a level

| Level | Use for | Rough volume |
| --- | --- | --- |
| `err` | The run is degraded in a way the user will notice. | Rare |
| `warn` | A fallback was taken; the run continues but not as configured. | Rare |
| `inf` | Lifecycle: startup, round boundaries, outcome, a setting changed. | Tens per session |
| `dbg` | Per-decision detail: one resolved cue, one pointer press. | Thousands |
| `trc` | Per-tick and per-frame firehose. | Millions |

Anything that fires more than once a second belongs at `dbg` or below. Per-tick
lines are `trc`, except that `sim.tick` is promoted to `dbg` on every 256th tick
(`LogSampling.SimulationTickInterval`) so an ordinary verbose run still carries a
bisectable skeleton. Frame timing takes the same shape from the other side:
`render.frame` is the per-frame `trc` line and `FrameTimingAggregator` reduces
it to one `render.window` line per second at `dbg`.

## Things that are deliberately not true here

- There is no network destination, no telemetry, and no crash reporting service.
- There is no ingestion tool or viewer. `ConvertFrom-Json` is the reader.
- `Hukbo.Core` is not instrumented, now or later.
- The `ui` channel is declared so the channel set is stable; nothing emits on it.
- Logs are never committed. `artifacts/` is in `.gitignore`, and startup keeps
  only the newest twenty files (`LogPaths.RetainedFileCount`).

## Related

- `hukbo-determinism-change` — when a hash moves and `sim.mismatch` fired.
- `hukbo-verify-and-record` — the gate builds `Release`, where logging is off.
- `docs/plans/2026-07-27-debug-logging-standard-design.md` — the full design and
  the reasoning behind every decision above.
