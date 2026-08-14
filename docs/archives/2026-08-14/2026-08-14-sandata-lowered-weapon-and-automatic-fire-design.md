# Sandata: why the lowered weapon and automatic fire are still invisible — design

**Archived: reference only.** This is finished work, kept only so a past
decision can be traced to its reasoning. Never execute it, never treat it as a
live task list, and never cite it as the reason to make a change. The live
contract for this project remains `CLAUDE.md` and Sandata's own scaffold design
document.

Opened 2026-08-14, after the third interactive session ran `SD-4` and `SD-5` and
both failed again. This is the second design document to address these two rows.
The first, dated 2026-08-12, correctly identified that neither feature had a
production caller and wired both of them. It shipped, its tests are green, and
the rows still fail. This document explains why, and what has to change for a
person at a desktop to be able to see and hear the two behaviours.

## 1. What the tester actually reported

Two observations, quoted from the 2026-08-14 session and recorded verbatim in
`docs/development/smoke-checklist.md`:

- `SD-4`: "it didnt happen; there was no gun change, and the sounds didnt change
  as well."
- `SD-5`: "no automatic fire, i hear only single shots." The same session asked
  a question no document currently answers: how long is a burst supposed to be?

Both rows moved from `PENDING` to `FAIL` that day. `SD-7b`, run in the same
session, passed and was closed.

## 2. The investigation, and one finding that outgrew both rows

Four read-only investigations ran in parallel over the simulation, the renderer,
the fire-mode rule, and the audio path. Every finding below was re-verified
against the source before it was written down, because a finding that is merely
reported is a claim rather than evidence.

### 2.1 The pathfinder cannot see walls

`NavSearch.IsBlockedOrOffGrid` consults exactly one source of passability: the
`blocked` span its caller hands it. It never reads `NavGrid.Passability`, which
is the array `NavBake` writes every wall, every closed door, and every inflation
margin into.

The span it is handed is `SandataSimulation._pathBlockedCells`. That array is
allocated once at construction and, in the words of its own doc comment, "never
written to after construction". Every element stays `false` for the whole
lifetime of the simulation.

The two facts compose into one: **A\* on `angle-house` searches a completely open
160x180 grid.** No walls, no doors, no inflation. The published path is a bare
diagonal, string-pulled afterwards by `PathSmoothing.Smooth`. Stage 13's local
avoidance is operator-against-operator only and never tests geometry either, so
nothing downstream corrects it. Operators walk through walls.

This was not a deliberate deferral of static geometry. The comments around it
describe the span as the *dynamic* blocker channel — "no door-driven dynamic
blocker source exists in this worktree" — which is true and is a reasonable thing
to defer. The defect is the unstated assumption underneath it: that static
geometry was already being enforced somewhere inside the search. It is not
enforced anywhere.

This finding is larger than the two smoke rows that uncovered it, and fixing it
moves Sandata's state hash. Section 6 records what that costs.

### 2.2 `SD-4`: the flag works, and nothing lets a person see it

`OperatorState.WeaponLowered` does flip on the shipped map. It flips for exactly
one of the four operators, for roughly 28 ticks — about 0.56 seconds — out of a
run of several hundred. Three separate things stand between that and a tester.

**There is no doorway crossing to watch.** The map authors put a 40-world-unit
door aperture at `DOOR 300 640 340 640`, closed, on the squad's way north.
Because of 2.1 the squad never funnels through it; the wall-ignoring diagonal
crosses that wall line wherever geometry happens to put it. The transition still
fires, but from the wall-proximity branch rather than the door-cell branch, at a
place that reads on screen as walking through a wall rather than as clearing a
doorway. `SD-4` asks a person to watch a doorway crossing. The shipped mission
does not contain one.

**Half the comparison is invisible by design, and nothing labels which half.**
`SD-4` asks for a rifle operator and then a pistol operator crossing the same
doorway, expecting the first to lower and the second not to. Both blue operators
do walk the route together, one carrying an AK and one a Glock, and
`LoadoutForIndex` alternates them specifically so that this row would be
runnable. But `WeaponLoweredRules.IsForcedLowered` returns `false` for an exempt
weapon before it evaluates any geometry, so the pistol operator's correct
behaviour is *nothing happening at all*. Meanwhile nothing on screen says which
operator carries which weapon: `src/Sandata.Client/UI` contains no reference to
`Firearm` or to `WeaponClass`. A tester watching the Glock sees nothing, and has
no way to discover that they were watching the wrong operator.

**The transition has no consumer.** `MissionEventKind.WeaponLowered` and
`WeaponRaised` are emitted correctly by stage 11 and folded into the state hash
correctly by `SandataStateHasher`. Nothing reads them. There is no `LogEvents`
constant for them, both of the client's `EventFeed` readers filter to
`ShotFired`, and `SandataEventLog` is never instantiated anywhere in `src/`. The
three debug logs from the 2026-08-14 session contain zero weapon events, which is
exactly what that wiring predicts.

So the entire signal available to a tester is a sprite rotating 0.9 radians and
shortening to 55 percent of its length, on an operator about fourteen pixels
tall, for half a second, with no sound, no text, and no log line.

### 2.3 `SD-5`: two independent causes, either one sufficient

**The audio latch drops every round after the first.** `FireMode.Auto` resolves
to `SoundFamily.GunLoop`, for which no file exists on disk, so every automatic
round depends on the one-report-per-round fallback that the 2026-08-12 work added
for exactly this case. That fallback is armed on the first round of a burst and
disarmed one tick later, permanently:

- `SoundShotsFiredOn` early-outs only when the event feed is empty. The feed is a
  rolling 200-event window, so after the first event of the run it is never
  empty, and `SoundAutomaticFireStops` therefore runs on **every** tick.
- Automatic fire at 600 rounds per minute is one round every five ticks. On the
  four ticks in between, the shooter is in `_automaticShootersLastTick` and not
  in `automaticShootersThisTick`, so the client reports the burst as ended.
- `SandataSoundPlayer.HandleAutomaticFireStopped` begins by removing the shooter
  from `_loopFallbackShooters`.
- The next round arrives five ticks later. The `GunLoop` reservation has a
  60-tick tail and is still live, so the budget **renews** it rather than issuing
  a new one; the fallback flag is only ever set on a new reservation, so it is
  never re-armed and the loop is never re-attempted.

The audible result is one report per burst. That is indistinguishable from a
single shot, which is precisely what the tester reported.

**A burst cannot exceed four rounds.** An operator has 100 health and 7.62x39
does 25 damage per hit, so the fourth round kills. At one round every five ticks
that is fifteen ticks, 0.30 seconds. This is the answer to the tester's question,
and it is not a good one: **the longest automatic burst the game can currently
produce is four rounds over three tenths of a second.** The 2026-08-12 test suite
did not catch this because its fixture gives the target 100,000 health.

Two further facts bound the row further. Firing requires `OperatorIntent.Engage`,
which `IntentSelection` grants only at `ContactTier.Identified`, which is 96
world units. Ninety-six is inside the 240-unit automatic band, so a rifle either
fires automatically or does not fire at all — the single-fire band is unreachable
for a rifle by construction. And only two of the four operators on the shipped
map carry an automatic-capable weapon at all, because `LoadoutForIndex` gives the
odd indices a Glock, including the defender a tester is most likely to be
watching.

### 2.4 One latent crash, found on the way

`ShotSlotResolver.FindWithFallback`'s last resort is `SandataSoundCatalog.Find`,
which throws `KeyNotFoundException` rather than returning a miss.
`AddAutomaticLoopAndTail` only iterates rifle calibers, so no `GunLoop` row
exists for 9x19 or 5.8x21. An automatic-capable pistol would therefore hard-crash
the client on its first round. Nothing in the current 38-weapon roster does this.
It is one catalog edit away from shipping, and it costs nothing to close now.

### 2.5 One reported finding that was checked and rejected

An investigation reported that `bin/Debug/net10.0/win-x64/Content` is empty and
concluded that a Debug run therefore has no sprites, fonts, or audio, which would
have explained both failures on its own. The 2026-08-14 run's own log refutes it:
it records `"configuration":"Debug"` alongside `assets.theme.loaded`,
`assets.font.loaded`, and `assets.sprite.loaded` with `"spriteCount":2`. The
tester had their assets. That directory is not the path a Debug run reads from,
and this is recorded here so that nobody spends a session on it again.

## 3. What this design decides

**D1 — the pathfinder respects the baked map.** The simulation writes static
impassability into its path-blocked span once, at construction, from
`NavGrid.Passability`. A cell `NavBake` marked `Blocked` is blocked for the
search; a `Door` cell stays passable, because a door is something operators go
through and the weapon-lowered rule already depends on their standing in one.
The span keeps its existing meaning as the search's single passability input, so
`NavSearch` is untouched and a future dynamic blocker source composes with this
by writing into the same array.

This makes the authored 40-unit aperture the only way north, which is what gives
`SD-4` a doorway to watch. It also stops operators walking through walls, which
is worth doing on its own account.

**D2 — the operator inspector names the weapon and its state.** Two new rows on
the selected operator: the firearm, and whether the weapon is lowered or raised.
This is the smallest change that lets a tester tell the two walking operators
apart, and it converts `SD-4` from a half-second pixel observation into something
a person can read directly off the screen and hold still with pause and single
step.

Nothing about this row makes the pistol lower. The pistol's exemption is the
designed behaviour and the row exists to confirm it; what changes is that a
tester can now see *which* operator they are watching and that its weapon state
is genuinely constant rather than merely unchanging on a sprite too small to
read.

**D3 — the weapon transition reaches the debug log.** One new `LogEvents`
constant, written from the client when it observes a `WeaponLowered` or
`WeaponRaised` event, at `dbg`. The transition fires a handful of times per run,
so `dbg` is the right level and `trc` would be over-cautious. `Sandata.Core` does
not log and must not: it may not touch the filesystem or the wall clock. The
client observes state the caller already holds, which is the standard this
repository already applies to every other Sandata log line.

**D4 — a burst ends when it is over, not when a tick is quiet.** The client stops
treating "no automatic round on this tick" as the end of a burst. A shooter is
considered still firing until a grace window has passed with no automatic round
from it, sized from the slowest cyclic rate the roster contains so that no
weapon's inter-round gap can be mistaken for a burst ending.

The fallback flag also stops being cleared on a burst end that has not happened.
`HandleAutomaticFireStopped` clearing `_loopFallbackShooters` is correct in
principle — a genuinely finished burst should re-attempt the loop next time — and
wrong only because it was being called four times per round. Once D4's grace
window is in place the clear happens once per real burst, which is what it was
written to do.

**D5 — the automatic-fire catalog resolves for every caliber, or fails loudly.**
The latent `KeyNotFoundException` in 2.4 is closed. The fix is to declare the
automatic rows for every caliber family the catalog knows, not to soften `Find`
into returning a miss: a slot the resolver cannot name is a content bug, and it
should be impossible to construct rather than survivable at runtime.

## 3a. Two decisions added on 2026-08-14, after the rows were re-run

`SD-4` passed against D1 through D4. `SD-5` failed again, and a driven `Debug`
run with the audio channel at `trc` measured why. The whole run produced seven
shot cues and **every one of them was the defending pistol firing single shots.
Neither attacker fired once.** The log line at the same tick reads
`{"ev":"sim.sandata.weaponState","entityId":1,"lowered":true}`.

**D6 — an operator engaging an identified hostile is not forced lowered.**
`LoweredWallDistanceWu` is 24 world units and `angle-house`'s corridors are about
32 wide, so a rifleman indoors is inside the threshold for his entire approach
and is forced lowered at the moment of contact. In a room-clearing game the rifle
could not shoot indoors at all, and no automatic round had ever been produced on
this map. A lowered muzzle is a movement discipline; an operator engaging a
target it can see raises. A moving operator with no identified contact still
lowers, at a wall and in a door cell, which is exactly the behaviour `SD-4`
confirms.

The obvious alternative — shrinking `LoweredWallDistanceWu` — is wrong twice
over. The doorway aperture is 40 world units, so its centre is 20 from each jamb,
and any threshold below 20 stops a doorway lowering the weapon and silently
un-passes `SD-4`. That constant also folds into `SandataRuleset.ContentHash`, so
moving it costs a new preset version. D6 costs neither.

**D7 — the placeholder roster's health goes from 100 to 300.** This is the
question section 4 below was written to hand back, and the answer turned out to
be cheap enough to take here. At 100 health against 7.62x39's 25 damage the
fourth round killed, so the longest burst the game could physically produce was
four rounds over 0.30 seconds, and `SD-5` asks a person to judge *sustained*
automatic fire by ear. The value is a client-side scenario placeholder whose own
comment records that no `SpawnRecord` and no design document fixes it; it is not
a ruleset constant, it does not reach `SandataRuleset.ContentHash`, and it costs
no preset version. Its cost is that every engagement on the placeholder map takes
proportionally longer to resolve.

Measured after both, by the same driven run: eleven rounds from the AK attacker
spaced about 100 milliseconds apart, spanning 1.03 seconds — 600 rounds per
minute sustained for a full second, where the same operator had previously fired
nothing at all.

## 4. What this design does not decide, and why

**Whether a burst should last longer than four rounds — decided in D7 above,
after this section was written.** It is left here rather than deleted because the
reasoning still applies to the next tuning value somebody wants to move: health
and per-caliber damage are placeholders rather than measurements, and changing
one to make a smoke row pass is the kind of decision that has to be visible as a
decision rather than buried in a bug fix. What made D7 takeable was that the
health placeholder lives in the client's scenario builder and reaches no hash, so
it could be changed, measured, and reverted without spending a preset version. A
change to per-caliber damage would not have that property.

**Whether `SD-5`'s wording still describes anything the game can do.** The row
asks for "sustained automatic fire from the maximum operator count". The shipped
mission has four operators, two of which carry pistols, and no scenario selector
exists. The row was written against a mission that does not exist yet. This
document does not rewrite the row; it records that the row and the build have
drifted apart, so that whoever runs it next judges it against the truth.

**Whether the map's other two doors matter.** `angle-house` has three door
records, two closed and one open. D1 makes the closed ones matter to the
pathfinder for the first time. Whether the resulting routes are the ones the map
author intended is a question for whoever next edits the fixture, not for this
package.

## 5. The spectator-discoverability answer

`SIMULATION-GAME-STANDARDS.md` section 10 asks whether a spectator can discover
the effect without reading source code. For both rows the honest answer today is
no, and that is the whole finding of section 2: both features are correct in the
simulation and undiscoverable on screen.

After D1 through D4 the answer is yes for `SD-4` — the operator inspector names
the weapon and its state, the route funnels through an authored doorway, and the
debug log carries the transition for anyone reconstructing a run afterwards. For
`SD-5` the answer becomes yes for "this weapon is firing automatically" and stays
no for "this is sustained fire", because of the four-round ceiling section 4
leaves open.

## 6. What this costs, and the reason it costs less than it should

This section was written before D1 was built, predicting that changing published
paths would change operator positions and therefore both hashes, and that both
golden fixtures and the recorded seed-1 baseline would have to be re-measured.
That prediction was wrong, and why it was wrong matters more than the prediction
did.

**D1 moves no hash.** Every pinned fixture in the Sandata suite builds its
navigation grid through `HeadlessRunner.BuildOpenGrid`, which ends with
`Array.Fill(grid.Passability, NavCellFlags.Open)`. `GoldenReplayTests`,
`DeterminismEquivalenceTests`, `MissionEventFeedTests`, and the headless
determinism workload the gate runs all use it. On a grid with no `Blocked` cell,
seeding the path-blocked span from `Passability` produces an array byte-identical
to the all-`false` one it replaces, so every pinned digest is untouched and the
full Core suite stays green.

So no fixture re-measurement is owed, no baseline is superseded, and
`MissionStateTests.PreTask79cBaselineHash` still describes what it always did.
D1 also does not need a new `SandataPresetId`: the preset numbers content — the
ruleset constants and their bake — and none of those change.
`SandataRuleset.ContentHash` is unaffected. D2, D3, and D4 are client-side and
move no hash either.

**The finding underneath that.** The reason a defect this large could be fixed
without moving a single pinned digest is that **Sandata's determinism contract
has only ever been proven on an empty field.** Both golden replays, the seed-1
baseline, and the gate's own headless workload run on a grid with no walls. No
pinned fixture has ever executed a tick against a real map, which is exactly why
a pathfinder that ignored every wall on every map survived a green gate and a
green 1,113-test suite for the whole life of the project.

That gap is not closed by this package and closing it is not a task here: a
determinism fixture over a real map is a new baseline with its own capture, its
own recorded digests, and its own decision about which map is canonical. It is
recorded here so that the next person to trust a green Sandata gate knows what
that green does not cover.
