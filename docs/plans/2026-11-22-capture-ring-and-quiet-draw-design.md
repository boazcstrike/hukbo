# Capture ring and quiet-battle draw — design

**Status: design only. It authorizes nothing, and its recommendation is to build
neither feature now.** The measurement in section 2 found no standoff for either
feature to fix under the presets a player can select. Sections 4 and 5 specify
both features anyway, so that a future session that does measure a stall can
start from a reviewed design rather than from the idea.

**Baseline: `main` at `570020b`, 2026-11-22.** File and line references are to
that baseline. Symbol names are the stable reference; line numbers are not
renumbered.

---

## 1. The question this document answers

The 2026-11-22 research post comparing We March as One's map mechanics with
Hukbo and Sandata ranked two ideas first for Hukbo:

1. **A centre capture ring as a tie-break and an attractor.** A crowd-capped
   progress bar decides who holds a ring at the map centre. At `TickLimit` the
   holder wins instead of the battle being a draw, and the ring gives idle
   warriors somewhere to go, so a standoff becomes a contest.
2. **A quiet-battle draw timer.** A battle ends as a draw after 1,800 ticks
   (90 seconds at 20 Hz) with no damage once blood has been drawn, or after
   6,000 ticks (300 seconds) if no blood is ever drawn.

Both ideas exist to fix one failure, which the post names as "the 10,000-tick
standoff draw". The user asked for this design document on 2026-11-22. This
document first checks whether that failure still happens, then specifies both
features against the code as it stands.

In We March as One, capture points never decide a battle. Their progress is
`float`, driven by frame `DeltaTime`, and its siege-zone sort is unstable. Only
the ideas transfer. Everything below is integer ticks, integer or `FixedPoint`
arithmetic, and `EntityId` tie-breaks.

## 2. The premise, measured

### 2.1 What was run

Every combination below used the shipped combat preset,
`CombatPresetId.PrecolonialPhilippinesV8`, which `ArenaGame.BuildScenario`
(`src/Hukbo.Client/ArenaGame.cs:1745`) hard-codes for every player battle. The
movement preset is the one a player chooses in the menu. The default is
`MovementPresetId.EvasiveFootworkV14` (`ClientSettingsStore.cs:142`).

The runner was the Release build of `Hukbo.Headless` at `570020b`, invoked as:

```
dotnet Hukbo.Headless.dll --agents <N> --ticks 10000 --seed <S> --preset 8 --movement-preset <M>
```

`--ticks 10000` equals the shipped `TickLimit` (`Scenario.CreateDefault`), so a
standoff would show up as either `Draw` or `Ongoing` at tick 10,000.

### 2.2 Results

| Movement preset | Agents | Seeds | Draws | Decisive | Ticks to end, min / median / max |
| --- | --- | --- | --- | --- | --- |
| V11 last-stand engagement | 200 | 1–30 | 0 | 30 | 1,555 / 2,160 / 4,670 |
| V11 last-stand engagement | 500 | 1–30 | 0 | 30 | 1,979 / 2,466 / 3,277 |
| V13 cohort lateral spread | 200 | 1–30 | 0 | 30 | 1,595 / 2,513 / 3,714 |
| V13 cohort lateral spread | 500 | 1–30 | 0 | 30 | 2,200 / 2,810 / 4,741 |
| **V14 evasive footwork (default)** | 200 | 1–30 | 0 | 30 | 1,568 / 2,112 / 3,173 |
| **V14 evasive footwork (default)** | 500 | 1–30 | 0 | 30 | 1,921 / 2,519 / 4,115 |
| **V14 evasive footwork (default)** | 1,000 | 1–15 | 0 | 15 | 2,808 / 3,708 / 4,849 |
| V15 contingent cohesion | 200 | 1–30 | 0 | 30 | 1,749 / 2,522 / 3,533 |
| V15 contingent cohesion | 500 | 1–30 | 0 | 30 | 1,777 / 2,658 / 4,668 |
| V16 shield encumbrance | 200 | 1–30 | 0 | 30 | 1,273 / 1,986 / 2,801 |
| V16 shield encumbrance | 500 | 1–30 | 0 | 30 | 1,734 / 2,707 / 5,429 |
| V17 separating contact | 200 | 1–30 | 0 | 30 | 1,151 / 1,827 / 2,529 |
| V17 separating contact | 500 | 1–30 | 0 | 30 | 1,431 / 2,287 / 4,214 |
| V17 separating contact | 1,000 | 1–15 | 0 | 15 | 1,948 / 2,401 / 2,871 |

**All 390 battles ended in annihilation, and none of them was a draw.** The
slowest took 5,429 ticks, which is 54 percent of the tick limit. Both factions
won in every row, so there is no systematic bias that a tie-break would have to
correct either.

V6 and V7, the presets under which the 10,000-tick standoff was measured, cannot
run under the shipped combat preset at all:

```
Headless run failed: No movement profile is registered for this loadout under movement preset EquipmentRelativeFootworkV6.
```

The standoff was real, and it was specific to presets a player no longer
reaches. The recorded root cause, clearance rejection in the footwork phase, was
specific to those presets too.

### 2.3 What the measurement does not cover

- **Army composition.** The headless runner uses each preset's default roster
  counts. A player sets the rank and armor sliders, and `ArenaGame` expands them
  through `ExpandCompositionToRosterCounts`. The runner has no roster flag, so
  a composition that might stall, such as an army of ranged warriors only, was
  not tested. That would need a measurement harness in `Hukbo.Core.Tests`, which
  is where calibration code that needs internal state already lives.
- **Seeds beyond 30, and agent counts above 1,000.** The 500-per-team cap is
  1,000 agents, so the 1,000-agent rows are the largest battle a player can set
  up.
- **Interactive play.** These are headless runs. No battle was watched.

## 3. Facts about the code that both designs depend on

- **The outcome rule** is `BattleSimulation.ResolveOutcome`
  (`BattleSimulation.Combat.cs`). It is a switch over whether each faction has a
  living warrior. Its fourth arm, `_ when Tick >= Scenario.TickLimit =>
  BattleOutcome.Draw` in the same method, is the only place the tick limit decides
  anything.
- **The outcome event carries no reason.** `ResolveOutcome` emits one
  `BattleEventKind.Outcome` with `Value = (int)Outcome` and `FactionId` set to
  the winner or `null`. A mutual-annihilation draw and a tick-limit draw produce
  the same event.
- **`BattleOutcome` has four values**: `Ongoing = 0`, `Faction0Victory = 1`,
  `Faction1Victory = 2`, `Draw = 3`.
- **The tick pipeline** is `AdvanceOneTick` (`:937`): cooldowns, target and
  intent selection, contingent states, the opt-in footwork stages, movement
  proposals, collisions, movement commit, the opt-in separating contact,
  collision metrics, attacks, the opt-in death cleanup, then `ResolveOutcome`.
- **No warrior is ever idle while an enemy lives.** `PerceptionRangeRaw`
  defaults to 2,048 world units and the default map is 1,280 × 720, so every
  living warrior always perceives, and targets, an enemy. This matters for the
  capture ring's attractor (section 5.4). There is no idle warrior to give a
  destination to. An attractor can only work by overriding target selection.
- **Preset gating.** Every behaviour added since V6 is gated by a flag that
  earlier presets leave clear, and its state is folded into the state hash only
  under that flag (`ComputeStateHash`, `:1025` onwards). That pattern is how
  every recorded golden has survived each new feature, and both designs follow
  it.
- **The client menu has no room.** When it was last measured, its panel budget
  was 657 pixels and both columns already used 634. Neither feature can add a selector, so whichever
  preset turns one on has to be chosen by `ArenaGame.BuildScenario`, not by the
  player.

## 4. Design A: the quiet-battle draw

### 4.1 The rule

- Before any damage has landed, a battle that reaches 6,000 ticks ends as a
  draw.
- After the first damage, a battle that goes 1,800 consecutive ticks with no
  damage ends as a draw.
- "Damage" means a `BattleEventKind.Damage` event. An `ArmorSaved` event always
  comes with a `Damage` event, so it adds nothing.
- Both thresholds are game-design tuning values copied from We March as One's
  90 and 300 seconds. They are provisional and not a historical measurement, and
  the code comments and tests must say so.

### 4.2 State

One authoritative `long LastDamageTick` on `BattleSimulation`, initialised to
`-1` for "no damage yet". It is set to the current tick at the one site that
adds a `Damage` event, inside `GatherAndCommitAttacks` (`:5750`). It is snapshotted, and it
is folded into the state hash only when the gate below is set.

### 4.3 Stage and same-tick order

The check is a new arm in `ResolveOutcome`, after the three annihilation arms
and before the `TickLimit` arm:

```
_ when QuietBattleExpired() => BattleOutcome.Draw,
```

Damage on the deciding tick is recorded before the check runs, because attacks
resolve in `GatherAndCommitAttacks` before `ResolveOutcome`. A blow landed on
tick 1,800 after a quiet stretch therefore keeps the battle alive. Annihilation
always beats a quiet draw on the same tick. A quiet draw and the tick limit on
the same tick are both `Draw`, and the reason recorded is `QuietBattle`, because
its arm is evaluated first.

### 4.4 The reason

The outcome event has to say *why* the battle ended, or a spectator cannot tell
a quiet draw from a tick-limit draw (section 10 question 8). There are three
ways to do this:

- **Append new `BattleOutcome` values (rejected).** Every consumer that reads
  `Draw` today would have to learn a second draw value, and the outcome stops
  being a pure result.
- **Overload a field on the existing `Outcome` event (rejected).** It changes
  the event stream of every preset, which moves every recorded event hash.
- **Append a new `BattleEventKind.EndReason = 8` (recommended).** It is emitted
  immediately after the `Outcome` event, only when the gate is set. `Value`
  carries a new appended enum: `BattleEndReason { Annihilation = 1,
  MutualAnnihilation = 2, TickLimit = 3, QuietBattle = 4, RingHeld = 5 }`.
  Ungated presets never emit it, so their event streams stay byte-identical.

### 4.5 The gate

This document recommends a new axis rather than a new movement preset. It adds a
`Scenario.EndConditionPreset` of type `EndConditionPresetId`, with
`None = 0` as the record default and one appended value per registered rule set.
The registry follows the shape of `CombatPresetRegistry` and
`MovementPresetRegistry`: a content hash per entry, folded into the state hash
only when the preset is not `None`.

A movement preset would be wrong for three reasons:

- An end condition has nothing to do with movement.
- Movement presets form a lineage, each one building on the last (V17 is V14
  plus separating contact). Tying an end condition to one entry would leave it
  unreachable from the others.
- The menu would then show a tick-limit rule as a movement choice.

`ArenaGame.BuildScenario` sets the preset directly, and no selector is needed.
Under `None`, every existing preset folds and emits exactly what it does today,
so no golden moves.

### 4.6 Spectator explanation

- The event feed shows "Draw — no blood drawn for 90 seconds" or "Draw — no blow
  landed for 90 seconds" from the `EndReason` event.
- The outcome banner shows the same reason line under "Draw".
- The `DiagnosticLog` outcome line gains a `reason` field.

### 4.7 Cost

One integer comparison per tick and one store per `Damage` event. There is no
cache, and the benchmark is unaffected beyond noise.

## 5. Design B: the capture ring

### 5.1 Geometry

There is one ring, centred on (`MapWidth / 2`, `MapHeight / 2`), with a radius
of 64 world units (provisional; about 15 body diameters across at the default
body radius of 4.25, `CollisionRules.DefaultBodyRadiusRaw`). A
warrior is inside the ring when the squared distance from its position to the
centre is no greater than the squared radius. That is an integer comparison on
raw `FixedPoint` values in `long`, with no square root.

### 5.2 Progress

- Each tick, after attacks and death cleanup and before `ResolveOutcome`, count
  the living warriors of each faction inside the ring. Dead warriors do not
  count.
- `Progress` is an integer in `[-P, +P]` with `P = CaptureTicks × Crowd`. With
  `CaptureTicks = 160` (8 seconds at 20 Hz) and `Crowd = 10`, `P = 1,600`.
- Each tick, progress moves by `sign(diff) × min(|diff|, Crowd)`, where
  `diff = faction1Inside - faction0Inside`, and is then clamped. This is the
  We March as One rule with `dt / captureTime` replaced by an integer budget. A
  margin of 10 or more takes a neutral ring in 160 ticks and an enemy ring in
  320.
- `Owner` flips to a faction at ±P, and goes back to neutral when progress
  crosses 0. Counting by faction is order-free, so no iteration order can affect
  the result.

State: `int RingProgress` and `sbyte RingOwner` (-1 for neutral). Both are
authoritative, snapshotted, and folded under the gate.

### 5.3 The tie-break

A new arm in `ResolveOutcome`, placed after the quiet-battle arm if both are on:

```
_ when Tick >= Scenario.TickLimit && RingOwner >= 0 => victory for RingOwner,
```

The `EndReason` is `RingHeld`. A neutral ring at the limit is still a draw.

**Under the measurements in section 2, this arm would never fire.** No battle
reached 5,500 ticks, let alone 10,000. A tie-break on its own is dead code for
every battle a player can set up today.

### 5.4 The attractor is the whole feature

The ring's only possible effect on a shipped battle comes from its attractor,
and section 3 shows that an attractor cannot be added gently. Every warrior
always has a target, so the ring can draw warriors only by changing target or
intent selection for some of them, and that changes the combat loop of every
battle. The design space is:

- **Idle warriors only.** This is We March as One's rule. No warrior is ever
  idle here, so it does nothing.
- **Contingent leaders steer toward the ring when no enemy is within N world
  units.** This is closer to "a reason to commit", but it moves every battle's
  hashes and outcomes, including battles that end decisively today.
- **A new `AgentIntent.SeekObjective`.** This is the most legible option, and it
  can be shown in the inspector. Adding an `AgentIntent` reddens the Client
  suite through `GetIntentLabel`, which is known and fixable.

Any of these turns the ring from a safety net into a change in how battles
play. That is a game-design decision rather than a fix, and it needs its own
seeded before-and-after measurement: outcomes, ticks to end, and who wins.

### 5.5 Events and spectator explanation

- Two appended event kinds, `RingCaptured = 9` and `RingNeutralised = 10`, each
  carrying `FactionId`.
- The client draws the ring on the ground, with a progress arc in the holder's
  team colour.
- The feed reports each change of hands.
- If an attractor is built, the inspector shows the warrior's intent.

### 5.6 Historical policy

A central objective ring is a gameplay abstraction, not a documented practice of
any polity in the depicted period. It gets no cultural name, and it is labelled
**Provisional reconstruction** wherever it is described to the player, under
`CLAUDE.md` section 7.

### 5.7 Cost

One pass over living agents per tick, O(n), with an integer distance check. The
ring-search targeting grid could answer the same question for less, but a flat
pass at 1,000 agents is cheaper than the targeting stage already runs. There is
no cache.

## 6. Section 10 questions, both designs

| # | Question | Quiet-battle draw | Capture ring |
| --- | --- | --- | --- |
| 1 | User-visible outcome | A dead battle ends as a draw with a stated reason | A ring at the centre; the holder wins at the limit; warriors contest it if an attractor exists |
| 2 | Tick stage, state | `ResolveOutcome`; reads and writes `LastDamageTick` | A new count step before `ResolveOutcome`; `RingProgress`, `RingOwner` |
| 3 | Units, bounds, same-tick rule | Integer ticks, 1,800 and 6,000; annihilation first | Integer progress ±1,600, crowd 10, radius 64 wu; dead warriors do not count |
| 4 | Ordering, random stream | No ordering, no randomness | Counts are order-free; no randomness |
| 5 | Cache | No cache | No cache |
| 6 | Save, event, version | New snapshot field; `EndReason` event; new end-condition preset | Two snapshot fields; three event kinds; new end-condition preset; attractor also needs a new movement preset |
| 7 | Complexity, benchmark | O(1) per tick | O(n) per tick; 500 and 1,000-agent benchmark |
| 8 | Spectator explanation | Reason line in the feed and banner | Ring drawn on the ground, feed events, inspector intent |
| 9 | Tests that fail first | Quiet stretch ends at exactly 1,800; a blow at 1,800 keeps it alive; `None` leaves every golden unchanged | Progress boundaries; neutral at crossing 0; holder wins at the limit; neutral ring is a draw; `None` leaves every golden unchanged |

## 7. Recommendation

1. **Build neither now.** Section 2 found no standoff in 390 battles under the
   presets a player can reach. A fix with nothing to fix adds state, events and a
   preset axis for no observable effect.
2. **If a stall is ever measured, build Design A first.** It is small,
   append-only, and changes no behaviour before its threshold, so it cannot make a
   decisive battle worse. The first step would be the composition harness in
   section 2.3, run against the compositions most likely to stall.
3. **Treat Design B as a game-design proposal, not a fix.** The tie-break alone
   never fires. The attractor changes every battle. Whether Hukbo's battles
   should have an objective is a question about what the game is, and it belongs
   to the user. If the answer is yes, this document's section 5 is the starting
   point, and the 4X campaign boundary in `CLAUDE.md` section 1 still applies:
   the ring is a battle mechanic and must not become campaign state in
   `Hukbo.Core`.

## 8. Open questions

- Does any reachable army composition stall? This is unmeasured, and section 2.3
  says how to measure it.
- If Design B is wanted, which attractor (section 5.4), and is one ring at the
  centre the right place, given where each army deploys? This document did not
  check the deployment geometry.
- Should the end-condition axis also carry the existing `TickLimit` draw, so that
  every end rule lives in one registry? Doing so would move nothing, because
  `None` keeps today's behaviour.
