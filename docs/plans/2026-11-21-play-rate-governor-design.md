# Play-rate governor and render interpolation — design

**Status: design.** The plan document `docs/plans/2026-11-21-wmao-optimizations.md`
carries the ordered task list. The user authorized execution of this package on
2026-11-21, after the documents are written.

**Baseline: `main` at `0ccc0d3`, 2026-11-21.** Hukbo only. Sandata's client is
out of scope except where section 6 says otherwise.

---

## 1. The question this document answers

When a battle is heavy, what should the spectator see: a battle that stutters
and skips, or a battle that plays smoothly at a slightly lower speed?

Today Hukbo does the first. This document proposes the second, plus smooth
movement between simulation ticks. Both changes live entirely in
`Hukbo.Client`. Neither touches `Hukbo.Core`, so neither can move a hash.

## 2. What the client does today

`ArenaGame` runs with `IsFixedTimeStep = false` and vertical sync on
(`ArenaGame.cs:406`, `:417`), so `Update` runs once per displayed frame.
`AdvanceSimulation` (`ArenaGame.cs:2167-2218`) then does this:

1. It adds `elapsedSeconds * _speedMultiplier` to `_simulationAccumulator`.
2. It clamps the accumulator to `MaximumAccumulatedSeconds = 0.5` simulation
   seconds (`:104`), which is 10 ticks at 20 Hz.
3. It runs `AdvanceOneTick`, `IngestTick`, and the sound ingest once per whole
   tick in the accumulator.

There is no per-frame tick cap and no per-tick timing. When ticks get
expensive, a frame takes longer, the next frame's `elapsedSeconds` is larger,
and the frame after that runs more ticks. The loop feeds itself until the clamp
fires. Then the surplus is thrown away, `_frameSimulationStarved` is set, and the
battle **jumps**.

The 2026-08-14 measurements make this concrete. At 1,000 agents the worst tick
was 23.4 ms, already more than a 16.7 ms frame, and at 4× speed the client asks
for 80 ticks a second.

Positions are also drawn straight from the latest tick. `Draw` reads
`agent.XRaw` and `agent.YRaw` from `_simulation.Agents`
(`ArenaGame.Rendering.cs:1233-1238`). At 20 Hz on a 60 Hz display, a pawn moves
on one frame in three and stands still on the other two.

## 3. The governor

### 3.1 Behaviour

The spectator picks a speed: 1×, 2×, or 4×. That is the **requested** speed.
The governor computes a **governed** speed that the machine can sustain, and the
client plays at `min(requested, governed)`. The governed speed falls quickly when
ticks get expensive and recovers slowly when they get cheap. A battle that
cannot hold 4× plays at, for example, 2.6×, smoothly, instead of jumping.

The idea comes from We March as One's `PlayRateManager`, which slows its play
rate to hold a frame-rate target instead of dropping frames. The numbers below
are our own.

### 3.2 The rule

Each frame, the client measures the wall time spent on simulation work: the
calls to `AdvanceOneTick`, `IngestTick`, and `IngestImmediate`, timed with
`Stopwatch.GetTimestamp`. The governor keeps a smoothed cost per tick.

- **Budget.** Simulation work may use at most half of each frame. That is
  the same 50% share the thousand-unit performance plan uses for submit time.
- **Sustainable rate.** `sustainableTicksPerSecond = 0.5 / smoothedCostPerTick`.
- **Governed speed.** `sustainableTicksPerSecond / TickRate`, clamped to the
  range `[0.25, requested]`. The floor keeps a battle moving on any machine.
- **Smoothing.** The governed speed moves toward its target with a time
  constant of 0.4 s when falling and 2.5 s when rising. These two numbers are a
  starting point, labelled provisional in code, to tune during the smoke rows.
- **No measurement, no change.** A frame that ran no ticks leaves the smoothed
  cost unchanged. A paused battle does not drift the governor.

The accumulator then grows by `elapsedSeconds * effectiveSpeed`. The 0.5 s
clamp stays as a last-resort guard. With the governor in place it should fire
only on a frame stall the governor could not see coming, such as a window drag.

### 3.3 Determinism

The governor changes **how many ticks run per wall-clock second**. It never
changes what a tick computes. The same seed produces the same battle at any
governed speed, tick for tick. That is the property that makes a play-rate
governor safe in a fixed-tick simulation.

### 3.4 Where the effective speed must go

`_speedMultiplier` feeds three places, and all three must read the effective
speed:

1. The accumulator in `AdvanceSimulation`.
2. `AdvanceEffects(…, _speedMultiplier)` (`ArenaGame.cs:1141-1144`), so
   effects stay in step with the battle.
3. The status text (`ArenaGame.Rendering.cs:1534`, `ArenaGame.cs:2582`).

The requested speed stays the integer the keys set. The effective speed is a
`double`.

### 3.5 Telling the spectator

`SIMULATION-GAME-STANDARDS.md` §10 asks whether a spectator can discover an
effect without reading the source. A battle that quietly plays slower fails that
test. While the effective speed is more than 5% below the requested speed, the
status text shows both, for example `4× (holding 2.6×)`.

### 3.6 Logging

A new `LogEvents.RenderPlayRateGoverned` constant, `render.playRateGoverned`,
logs at `dbg` on each transition into or out of the governed state, with fields
`requested`, `effective`, and `costPerTickMicroseconds`. It never logs per frame.

### 3.7 Code shape

A pure internal sealed class, `PlayRateGovernor`, in
`src/Hukbo.Client/Presentation/PlayRateGovernor.cs`, beside
`PlaybackController`. It holds no clock. The caller passes the frame's elapsed
seconds, the measured simulation-work seconds, and the tick count, and receives
the effective speed. That makes every behaviour testable in
`tests/Hukbo.Client.Tests/PlayRateGovernorTests.cs` with synthetic costs, with
no `ArenaGame`, no graphics device, and no wall clock.

Sandata's `TickPacing` (`Sandata.Client/Simulation/TickPacing.cs`) is the
precedent for a pure pacing helper. It cannot be shared, because
`CLAUDE.md` §3 defers a shared client tier. The governor is written fresh for
Hukbo.

## 4. Render interpolation

### 4.1 Behaviour

Each pawn is drawn between its position at the previous tick and its position
at the current tick:

```text
alpha = _simulationAccumulator / secondsPerTick   (after the tick loop, in [0, 1))
drawn = previous + (current - previous) * alpha
```

The drawing runs one tick behind the simulation: 50 ms at 1×, 12.5 ms at 4×.
In exchange, a pawn moves on every frame instead of one frame in three. This is
the standard fixed-timestep interpolation that We March as One's `SimLerpSystem`
also uses.

### 4.2 What reads the interpolated position

A single pure helper, `RenderPositionInterpolator`, in
`src/Hukbo.Client/Presentation/RenderPositionInterpolator.cs`, owns two flat
arrays of previous and current raw positions, indexed like the agent views. It
returns a drawn position per agent. These consumers use it:

- the pawn body, legs, and weapon (`ArenaGame.Rendering.cs:1233-1238`) and the
  render-probe path (`:456-461`);
- anything drawn attached to a pawn: selection and hover marks, the leader
  mark, and health accents;
- pointer picking (`Presentation/AgentSelection.cs:27-28`), so a click lands on
  the pawn the spectator sees.

These consumers keep **authoritative** tick positions:

- `GaitAnimationSystem`, which derives motion from tick-to-tick position deltas
  (`GaitAnimationSystem.cs:209-258`). Feeding it interpolated positions would
  corrupt its speed estimate.
- Effects spawned at an event's position: blood, dust, clash, and death.
- Projectiles, which already advance on their own per-tick flight
  (`ProjectileFlightSystem.cs:142-147`).

### 4.3 Edge cases

- **No history.** On reset, on spawn, and on the first tick, the previous
  position equals the current one. Nothing slides in from the origin.
- **Paused.** The accumulator does not change, so alpha does not change and
  pawns hold still.
- **Several ticks in one frame.** Only the last tick's previous and current
  positions matter. Alpha is the fraction left over after the loop.
- **Dead agents.** A corpse does not move, so previous equals current.

### 4.4 The visible cost, and how it is judged

Event effects stay on tick positions while pawns are drawn up to one tick
behind. A blow's blood can appear up to one step (3 wu) ahead of the pawn that
receives it. Whether that reads as wrong is a judgement for a person at the
desktop, not for a test. To make that comparison possible, a new
`ClientCommand.ToggleInterpolation` on the free `I` key switches interpolation
off and on. It is not persisted. If the smoke rows prefer it off, the default
flips and the feature stays available.

## 5. Tests and smoke rows

- `PlayRateGovernorTests`:
  - a cheap tick never governs below the requested speed;
  - a cost above budget converges to the sustainable speed;
  - recovery is slower than the fall;
  - the 0.25 floor holds;
  - a frame with zero ticks leaves the state unchanged.
- `RenderPositionInterpolatorTests`:
  - alpha 0 and alpha near 1 return the endpoints;
  - reset removes history;
  - a dead agent holds still;
  - several ticks in one frame use only the last pair.
- `StatusText` coverage for the `holding` suffix, wherever the status string is
  built purely.
- New smoke rows, all `PENDING`:
  - a 1,000-agent battle at 4× plays without visible jumps and shows `holding`;
  - pawns move smoothly at 1×;
  - pressing `I` toggles interpolation, so a person can compare;
  - clicking a moving pawn selects it.

## 6. Found while reading, not in scope

`SandataGame.cs:720` converts elapsed time with
`(long)TotalMilliseconds * 1_000`. That truncates to whole milliseconds before
it reaches `TickPacing`'s microsecond accumulator, which contradicts the
rationale at `TickPacing.cs:37-41`, and loses most of a millisecond per frame.
It is a one-line Sandata fix, recorded here so it is not lost. It is not part of
this package.

## 7. What this design does not do

- It does not change the simulation, a hash, or a preset.
- It does not add a setting to `ClientSettingsStore`. The schema stays at 14.
- It does not interpolate rotation. Hukbo pawns have no facing; only the weapon
  arm is directional, and it follows the attack animation.
