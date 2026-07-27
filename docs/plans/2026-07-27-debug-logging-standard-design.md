# Debug Logging Standard — Design

Date: 2026-07-27
Status: Design. This document does not authorize implementation; see the
companion plan `2026-07-27-debug-logging-standard.md`.

## 1. Problem

Hukbo is in active development and testing, and today it produces almost no
durable evidence of what it did.

- `src/Hukbo.Client/Program.cs` writes a single line to standard error when the
  game fails to start. Nothing else in the client ever writes anything.
- `src/Hukbo.Headless/HeadlessRunner.cs` serializes one `RunReport` at the end
  of a run. It is an excellent summary and a poor debugging record: when a run
  reports `firstMismatchTick`, the report says which tick diverged and nothing
  about what was happening at that tick.
- `SoundCueLog` and `BattleEventFeed` are rich, but they are on-screen only.
  Both are bounded ring buffers that live and die with the process. When the
  window closes, the evidence is gone.

The practical consequence: when someone reports "the sound cut out around the
big melee" or "a click on the control bar did nothing", an agent asked to
investigate has no record of the session and cannot reproduce a real-time,
input-driven, audio-dependent situation from a description. The agent's only
honest options are to guess or to ask the human to reproduce it while watching
the screen.

A second, quieter consequence: several failure modes are silent by
construction. A missing sound file, a font that fell back, a `settings.json`
that failed schema validation and was silently replaced by defaults, and a
theme catalog that fell back to the built-in list all produce a game that runs
and looks approximately correct while being wrong.

## 2. Goal

Every development run leaves behind a machine-readable record that is complete
enough for an agent to reconstruct what happened without watching the screen,
and cheap enough that its presence cannot change the outcome of a simulation or
the result of the canonical gate.

This is a testing and development facility. It is not a player-facing feature,
it is not telemetry, and nothing it produces leaves the machine.

## 3. Decisions

These four were settled with the repository owner before this document was
written.

| Question | Decision |
| --- | --- |
| Format and destination | JSON Lines, one object per line, written to `artifacts/logs/` |
| Enablement | On in `Debug` configuration, off in `Release`, environment variable overrides both |
| Channels in the first pass | `sim`, `audio`, `input`, and the boot/assets/settings group |
| Shared or duplicated | One shared project, referenced by `Hukbo.Client` and `Hukbo.Headless` |

JSON Lines was chosen over fixed-column text because the primary reader is an
agent. A line-delimited object stream can be filtered with `Select-String`,
parsed with `ConvertFrom-Json`, and queried field-by-field without a parser that
has to be kept in sync with a format string. It remains readable enough to tail
in a terminal.

## 4. The record format

One JSON object per line, no wrapping array, UTF-8 without a byte order mark,
`\n` line endings. Every line carries the same six leading fields in the same
order, followed by that event's own payload fields in the order the call site
declares them.

```jsonl
{"seq":1,"t":-1,"ms":0,"lvl":"inf","ch":"boot","ev":"boot.started","configuration":"Debug","level":"dbg","channels":"all","path":"C:\\...\\artifacts\\logs\\hukbo-20260727-142211-31544.jsonl"}
{"seq":2,"t":-1,"ms":41,"lvl":"warn","ch":"assets","ev":"assets.sound.missing","slot":"death_heavy"}
{"seq":3,"t":-1,"ms":42,"lvl":"inf","ch":"sim","ev":"sim.scenario.built","seed":1,"agents":200,"mapWidth":1600,"mapHeight":900}
{"seq":4,"t":1420,"ms":8123,"lvl":"dbg","ch":"audio","ev":"audio.cue","slot":"hit_flesh","status":"suppressed","variant":-1,"gain":0.0,"voices":11}
{"seq":5,"t":1420,"ms":8124,"lvl":"dbg","ch":"input","ev":"input.pointer","button":"left","x":612,"y":688,"consumedBy":"controlBar"}
```

### Leading fields

| Field | Type | Meaning |
| --- | --- | --- |
| `seq` | integer | Monotonic emission counter starting at 1. The only unambiguous ordering key; two events in the same millisecond still order correctly. |
| `t` | integer | Simulation tick the event belongs to, or `-1` when there is no tick context (startup, asset loading, settings). |
| `ms` | integer | Milliseconds since process start. Wall clock, and therefore presentation-only — nothing in `Hukbo.Core` ever reads it. |
| `lvl` | string | One of `err`, `warn`, `inf`, `dbg`, `trc`. |
| `ch` | string | One of `boot`, `assets`, `settings`, `sim`, `audio`, `input`, `ui`. |
| `ev` | string | Stable dotted identifier, lowercase, `noun.verb` shaped. The machine key. |

### The `ev` rule

`ev` is a stable identifier, not a sentence. `audio.cue`, `settings.invalid`,
`sim.outcome`. It never contains a value, never contains a count, never gets
reworded to read better, and never varies with the data — the data goes in
payload fields. This is what makes a log greppable a year from now.

Free prose is allowed only as an optional `msg` field on `err` and `warn`
lines, where a human needs the explanation that a payload cannot carry.

Every identifier is declared once as a `const string` on a single `LogEvents`
class so the whole catalog can be read in one file, and so a test can assert the
identifiers are unique, correctly shaped, and sorted.

### Payload fields

Payload names are `camelCase`. Values are limited to what JSON represents
without ambiguity: integer, floating point, boolean, and string. There are no
nested objects and no arrays — a flat line is trivially filterable, and nesting
buys nothing at this scale. A composite value is expressed as sibling fields
(`survivors0` and `survivors1`), not as a nested structure.

## 5. Levels

| Level | Meaning | Volume |
| --- | --- | --- |
| `err` | The run is degraded in a way the user will notice. | Rare |
| `warn` | A fallback was taken; the run continues but not as configured. | Rare |
| `inf` | Lifecycle: startup, shutdown, scenario built, round boundaries, outcome, a setting changed. | Tens per session |
| `dbg` | Per-decision detail: each resolved sound cue, each pointer press and what consumed it, each key that mapped to a command. The default verbose tier. | Thousands per session |
| `trc` | The firehose: per-tick simulation state, per-frame audio budget. Never on by default. | Millions per session |

The level is a threshold. Selecting `dbg` emits `err`, `warn`, `inf`, and `dbg`.

`inf` is deliberately the level at which a session is still summarizable at a
glance. Anything that fires more than once per second belongs at `dbg` or below.

## 6. Channels

| Channel | Covers | First-pass events |
| --- | --- | --- |
| `boot` | Process lifecycle | `boot.started`, `boot.window.created`, `boot.stopped`, `boot.crashed` |
| `assets` | Content that is loaded from disk at runtime | `assets.theme.loaded`, `assets.theme.fallback`, `assets.font.loaded`, `assets.font.failed`, `assets.sound.scanned`, `assets.sound.missing`, `assets.sound.loadFailed` |
| `settings` | `settings.json` and the preferences derived from it | `settings.loaded`, `settings.invalid`, `settings.saved`, `settings.saveFailed`, `settings.changed` |
| `sim` | Client-side observation of `Hukbo.Core` | `sim.scenario.built`, `sim.round.started`, `sim.tick`, `sim.outcome`, `sim.reset`, `sim.speed.changed`, `sim.playback.changed` |
| `audio` | The sound pipeline | `audio.cue`, `audio.mute.toggled`, `audio.player.attached`, `audio.frame` |
| `input` | Keyboard, pointer, and the pointer priority chain | `input.key`, `input.pointer`, `input.focus.changed` |
| `ui` | Reserved. Declared so the enum is stable; nothing emits on it in the first pass. | — |

Channels filter independently of level, so `HUKBO_LOG_CHANNELS=audio` at level
`trc` produces an audio-only firehose without the simulation noise around it.

### Why these events, specifically

`audio.cue` carries `slot`, `hitClass`, `status`, `variant`, `gain`, and
`voices`. The on-screen `SoundLogPanel` shows the slot and the status. The rest
are exactly the fields needed to explain the current gain-compensation work —
why a blow in a crowded melee is quieter than the same blow in a duel — and they
are invisible today. `voices` is the count the gain was derived from, captured
before the cue itself joins the ledger. The tick is not repeated as a payload
field because the line's own `t` already carries it.

`input.pointer` carries `consumedBy`. The pointer priority chain in
`ArenaGame.Update` walks summary panel, control bar, event log, sound log,
inspector, then arena, and the first surface to claim the press wins. A click
that "did nothing" is almost always a click that a surface above the intended
one swallowed. That is currently unobservable from outside the debugger, and it
is one line in the log.

`consumedBy` distinguishes `outside` from `none`. MonoGame reports a press whose
position lies off the viewport — the spectator clicked another window or another
monitor — and the game correctly ignores it, but recording that as `none` would
read as a dead click on our own UI and send a reader hunting a layout bug that
does not exist.

`sim.tick` is the only genuinely expensive event, and it is `trc` for that
reason. At `dbg` the simulation channel samples instead: one `sim.tick` line
every 256 ticks, carrying tick, both survivor counts, the tick's event count,
and the state hash. That is enough to bisect a divergence to a 256-tick window
from an ordinary development run, at roughly forty lines per ten thousand ticks.

## 7. Enablement and destination

### Resolution order

1. `HUKBO_LOG_LEVEL` if set. Accepts `off`, `err`, `warn`, `inf`, `dbg`, `trc`,
   case-insensitively.
2. Otherwise `dbg` in the `Debug` configuration, `off` in `Release`.

An unrecognized value is a startup error written to standard error, and the
process falls back to the configuration default rather than guessing. Silently
accepting a typo in a debugging switch is how a person spends an hour wondering
why their log is empty.

`HUKBO_LOG_CHANNELS` accepts a comma-separated list of channel names, or `all`.
Default `all`. `HUKBO_LOG_DIR` overrides the destination directory.

The headless runner additionally accepts `--log-level`, `--log-channels`, and
`--log-dir`, which outrank the environment. A one-off diagnostic run should not
require mutating the shell's environment. The switch is named `--log-dir` rather
than `--log-output` because `--output` already means the run report's JSON path,
and two switches a character apart with different meanings is a trap.

### Destination

`artifacts/logs/hukbo-<yyyyMMdd-HHmmss>-<pid>.jsonl`, resolved against the
repository root.

The repository root is found by walking up from `AppContext.BaseDirectory`
looking for `Hukbo.slnx`. If no ancestor has it — which is the case for a
packaged build — the destination falls back to `logs/` beside the executable.
The resolved absolute path is the `path` field of the very first line, so there
is never a question about where a log went.

`artifacts/` and `*.log` are both already in `.gitignore`; `*.jsonl` under
`artifacts/` is covered by the directory rule, so no log can be committed by
accident.

### Retention

On startup, after creating the new file, delete all but the newest twenty log
files in the directory. Unbounded growth is a real hazard for a facility that is
on by default during development, and `CLAUDE.md` forbids unbounded caches for
the same reason. Twenty is enough to cover a working session's worth of runs
plus the run being investigated.

## 8. Determinism and cost

This is the part that has to be right, because a logging facility that can
change a simulation outcome is worse than no logging at all.

**`Hukbo.Core` never learns the logger exists.** The shared project is
referenced by `Hukbo.Client` and `Hukbo.Headless` only. `Hukbo.Core` is
forbidden the filesystem and the wall clock by `CLAUDE.md` §5, and the logger
needs both. Every simulation observation is made from outside, by the caller
that already holds the `BattleSimulation`, reading public state it can already
read. A test asserts the absence of the reference so this cannot drift.

**Reads only.** Every value written to a log line is read from state that the
caller already had. No log call may invoke a method that mutates, allocates
into simulation state, or advances an RNG stream. In particular, nothing may
call `ComputeStateHash` at a cadence the simulation would not otherwise use —
the sampled `sim.tick` line calls it once every 256 ticks and the sampling
interval is a constant, not a function of anything observed.

**Proven, not asserted.** A test runs the seed-1 headless workload twice, once
with logging off and once at `trc` with every channel on, and asserts an
identical state hash, event hash, outcome, and ordered event stream. If the
logger can perturb the simulation, that test fails.

**The gate stays clean.** `verify.ps1` runs a `Release` build, where the default
is `off`, so the canonical determinism workload is unlogged unless someone
explicitly asks for a log. Timing measurements in `RunReport` are therefore
measurements of the simulation, not of the simulation plus a writer.

### Allocation

`CLAUDE.md` names a per-tick allocation budget, and `dbg` fires inside the frame
loop. The API is therefore shaped so that a disabled call allocates nothing and
does approximately nothing:

```csharp
Log.Write(LogLevel.Debug, LogChannel.Audio, LogEvents.AudioCue,
    "slot", slotName, "status", statusName, "voices", voiceCount);
```

The name/value pairs are `string` and a `readonly struct LogValue` carrying a
discriminated union of `long`, `double`, `bool`, and `string?`. Implicit
conversions from the primitive types mean call sites stay readable, and the
struct means an integer payload never boxes. Overloads exist for zero through
six pairs, which covers every event in section 6.

The very first statement of every overload is the level-and-channel test, and it
returns before touching the builder. When logging is off, the cost of a call
site is the argument evaluation the caller was doing anyway plus one comparison
against a precomputed bitmask.

Serialization reuses a single `StringBuilder` and a single `StreamWriter` held
by the sink. The writer does not auto-flush; it flushes when a line at `warn` or
above is written, at the end of each frame, and on disposal. A crash path
flushes explicitly before rethrowing.

### Threading

The client and the headless runner are both single-threaded with respect to
logging. The sink takes an uncontended lock anyway. The cost is negligible, and
it removes an entire class of "the log file is interleaved garbage" bug that
would otherwise be discovered at the worst possible moment.

## 9. Making the standard stick

A convention that lives only in prose gets ignored by the third agent to touch
the file. Four of the five rules here are enforced mechanically.

| Rule | Enforcement |
| --- | --- |
| `Hukbo.Core` must not reference the logger | Test on the `Hukbo.Core` assembly's referenced assemblies |
| No bare `Console.Write*` in `src/` outside the two entry points | Test that scans `src/**/*.cs`, excluding `bin/` and `obj/` |
| Every `ev` identifier is unique, lowercase-dotted, and declared on `LogEvents` | Test over the `LogEvents` constants by reflection |
| Logging cannot change a simulation | Seed-1 headless run, off versus `trc`, identical hashes |
| Payload names are `camelCase`, no nesting | Review. Not worth an analyzer. |

The prose that remains goes in three places: a numbered section in `CLAUDE.md`
and its mirror in `AGENTS.md`, a project-local skill at
`.claude/skills/hukbo-debug-logging/`, and a section in
`docs/development/testing.md` explaining how to read a log while running the
manual smoke checklist.

## 10. Standards questionnaire

`SIMULATION-GAME-STANDARDS.md` §10 requires nine answers from every feature
proposal. The one that matters most here is the discoverability question.

**Can a spectator discover this effect without reading source code?** No, and
that is correct — this is not a spectator-facing feature. It is a development
facility, off in `Release`, that produces no in-game effect at all. The
corresponding question for a development facility is whether an *agent* can
discover the effect without reading source code, and the answer is yes: the
first line of every log states the configuration, the level, the channels, and
the path, and the `LogEvents` catalog is a single readable file.

The determinism questions are answered in section 8. The cost question is
answered by the allocation discussion in section 8 and bounded by the retention
policy in section 7.

## 11. Explicitly out of scope

- Any network destination, any telemetry, any crash reporting service. The game
  is offline and stays offline.
- Log ingestion, indexing, or a viewer. `Select-String` and
  `ConvertFrom-Json` are the reader.
- Instrumenting `Hukbo.Core`. Not now and not later; see section 8.
- Player-facing log output or an in-game log viewer beyond the existing
  `SoundLogPanel` and `BattleEventLogPanel`.
- A `ui` channel implementation. The enum member is declared so the channel set
  is stable; nothing emits on it yet.
