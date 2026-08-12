# Sandata order following, weapon legibility, and automatic fire — design

Written 2026-08-12, after the second Sandata smoke session. The tester ran
`./scripts/run.ps1 -Game Sandata -Configuration Debug` seven times, worked down
the ordered script in `docs/development/smoke-checklist.md`, and reported four
things. This document explains what each one actually is, and decides how each
is fixed. It authorizes nothing on its own; the plan document beside it is
`2026-08-12-sandata-order-and-combat-legibility.md`.

The binding document for Sandata remains
`2026-08-07-sandata-scaffold-design.md`. Where this document cites a section
number without naming a file, it means a section of that design.

## 1. What the tester reported, and what each report is

| Report | Reported as | What it is |
| --- | --- | --- |
| Steps 1 through 6 of the ordered script | Passed | No action |
| Step 7 — a drawn polyline is submitted and the squad keeps walking the objective route | New | Two independent defects, D1 and D2 below |
| `SD-4` — no visible difference between a lowered and a raised weapon | Attempted, not closed | D3 below |
| `SD-5` — automatic fire is inaudible; only single shots are heard | Attempted, not closed | D4 below |
| `SD-7b` — no theme switcher, no unknown-contact state | Blocked since it was written | D5 below |

The tester also reported seeing no menu. That is expected and is not a defect:
Sandata has no menu. The client opens straight into the mission, and the
checklist's "what is knowingly not working" list already records that the
contact list, mission clock, roster strip, order queue, and go-code panel draw
as blank rectangles. What the checklist did *not* say plainly is that there is
no menu at all, and it now says so.

## 2. D1 — the order layer is half-wired, so an authored path is never followed

### What is there

Submission works end to end. `PathDrawTool.Submit` reaches
`SandataSimulation.SubmitOrder`, which reaches `OrderQueue.SubmitValidated`,
which validates the polyline against section 16's four rules and stores it.
Stage 1, `SandataSimulation.ApplyOrders`, then converts an accepted
`MoveAlongPath` order into one `OrderAssignment` per addressee with
`CurrentNodeIndex` at 0. Stage 9's ordered branch reads that assignment and
walks the operator toward `PathNodes[CurrentNodeIndex]`.

### What is missing

Nothing ever advances `CurrentNodeIndex`, and nothing ever clears an
assignment. `MovementSource.Evaluate` — the method that holds section 16's four
clearing conditions — has **no production caller anywhere in `src/`**. Its only
callers are in `tests/Sandata.Core.Tests/MovementSourceTests.cs`.

So an operator under orders walks to the polyline's *first* node and then
stands on it for the rest of the run. It never reaches node 1, never reaches the
final node, and never returns to its squad's autonomous route. The order layer
looks wired because a squad under orders does stop obeying its objective — but
only in the sense that it stops at the first waypoint.

That is not what the tester saw, though, and the reason is D2.

### The decision

Stage 1 gains a sub-step that runs **before** this tick's orders are applied:
for every existing assignment, decide whether the operator has arrived at its
current node, advance the index if a further node exists, and then run
`MovementSource.Evaluate` to decide whether the assignment survives.

Three points about that shape:

- **It runs before application, not after.** An order applied this tick sets
  `CurrentNodeIndex` to 0; evaluating arrival immediately afterwards could clear
  a brand-new assignment on the tick it was given, whenever the operator happens
  to already stand near its own first node. Evaluating the *previous* tick's
  assignments first avoids that entirely.
- **`cancelOrderApplied` is passed as `false`, and that is correct rather than
  lazy.** Cancellation is already handled by application itself: `ApplyOrders`
  clears an addressee's assignment for every `OrderKind` other than
  `MoveAlongPath`, which covers `Cancel` and `Hold`. Application runs after
  evaluation in the same stage, so a cancel submitted for this tick is honoured
  this tick regardless.
- **It reads only committed state.** The positions it compares against are last
  tick's committed positions, already on `MissionState`. No frozen view, no
  derived structure, nothing recomputed.

**Arrival radius.** Section 16 does not name one. This design sets
`NodeArrivalRadiusWu = 16` — one metre — and marks it **provisional** in the
code, for a reason worth stating: an authored order addresses several operators
at once, every one of them walks at the same node rather than at a formation
slot, and `CollisionBodyRadiusRaw` is 4,352 raw, which is 4.25 world units. Two
bodies therefore cannot approach closer than 8.5 world units centre to centre.
A radius below that would let the first operator arrive and leave the second
one pressed against it forever, never arriving, never advancing, and never
clearing — a permanent stall that would read exactly like the defect this whole
change exists to remove. Sixteen world units clears two body radii with margin
and is still well under the smallest sensible spacing between two hand-drawn
nodes.

## 3. D2 — a rejected order is invisible, which is what the tester actually saw

Section 16 requires that "rejection is observable". Today it is observable only
in the sense that `MissionEventKind.OrderRejected` enters the event feed and one
rectangle changes colour in a panel that draws no text. There is no log line at
all: `Hukbo.Diagnostics.LogEvents` carries no Sandata order event, so the seven
debug logs from the reporting session contain no record that an order was ever
submitted, accepted, or refused.

That matters here more than it would elsewhere, because the shipped map makes
rejection the *likely* outcome of the checklist's own instruction. Step 7 says
to right-click "three or four points across the map". `angle-house` is a house:
it has a long diagonal wall through the middle and interior partitions. Section
16's third rule rejects a polyline whose segment crosses a wall segment, and a
polyline drawn across a house without regard to its walls crosses one almost
every time. The order was assigned an id, consumed a sequence number, emitted a
rejection event, and vanished, and the client said nothing.

### The decision

Two changes, neither of which touches the validation rules themselves. The four
rules stay exactly as section 16 states them, and an authored polyline is still
never re-routed, re-smoothed, or silently repaired.

- **Log every submission.** One new event, `input.sandata.order`, at `dbg` for
  an accepted order and `warn` for a rejected one, carrying the kind, the
  addressee count, the target tick, the order id, and — on rejection — the
  `OrderRejectReason` by name. A tester who reports "nothing happened" can then
  be answered from the log they already have.
- **Draw the order queue rows as text.** The rows already have a formatter,
  `OrderQueueView.FormatEntryLine`, which no draw path has ever called because
  the client had no font. It has had one since 2026-08-11, and
  `DrawOperatorInspectorText` already draws real text through it. The order
  queue panel gets the same treatment, so a rejected order reads as a line
  naming its reason instead of as a differently-coloured bar.

## 4. D3 — the lowered weapon is stored nowhere and drawn nowhere (`SD-4`)

Section 9 calls the weapon-lowered rule "one conditional that generates the
whole game". `WeaponLoweredRules.IsForcedLowered` implements it correctly, and
stage 11 calls it correctly, and passes the result into `WeaponChain.Advance`,
which honours it. Two things then fail to happen:

- **The flag is never stored.** Stage 11 writes back `WeaponChainPhase`,
  `WeaponChainRemainingTicks`, and `AimAngle`, and nothing else.
  `OperatorState.WeaponLowered` is folded into the state hash by
  `SandataStateHasher` and is therefore hash contract, but it is initialised to
  `false` by the client and by the headless runner and never assigned again for
  the whole run.
- **The renderer never reads it.** `OperatorGeometry.Create` has no
  lowered-weapon parameter and no lowered-weapon layer, so a lowered weapon and
  a raised one produce byte-identical geometry.

### The decision

- Stage 11 writes `WeaponLowered = result.Phase == WeaponChainPhase.Lowered`.
  Deriving it from the resolved phase rather than from `forceLowered` directly
  is deliberate: the phase is what the chain actually holds, and a weapon that
  is lowered because it has not yet been raised is lowered in exactly the same
  sense as one forced down by a wall.
- Section 9 also requires that "the transition into it emits an authoritative
  event so the spectator can see the cause rather than only the effect". Two
  `MissionEventKind` members are appended, `WeaponLowered = 4` and
  `WeaponRaised = 5`, emitted only on the tick the stored flag changes. Both are
  appended at the next free ordinals under the same append-only rule every
  Sandata enum follows.
- `OperatorGeometry.Create` gains an `isWeaponLowered` parameter defaulting to
  `false`, so every rectangle every existing test pins is unchanged when it is
  not passed. When it is true the weapon body, foregrip, and muzzle anchor
  rotate to a fixed carry angle away from the aim direction and the body
  shortens, which is what "port arms" reads as from directly above. The muzzle
  flash cannot draw in that state, which is consistent rather than special-cased:
  a lowered weapon never fires.

**This is a presentation decision, not a documented one.** Section 9 says the
flag is hashed and that a transition is observable; it does not say what a
lowered weapon looks like from overhead. The carry angle and the shortening
factor are provisional and say so at their declarations.

## 5. D4 — the simulation never selects a fire mode, so automatic fire does not exist (`SD-5`)

Three failures stacked on top of each other, which is why the row could not
close even after the audio slice shipped.

- **The simulation never chooses a mode.** `FireModeSelection.SelectMode` is
  section 9's ordered band rule, it is fully implemented, it is fully tested,
  and it has **no production caller**. So does `CyclicFireAccumulator.Advance`,
  which is section 9's driftless per-round scheduler. Every shot in the game is
  produced by the weapon chain's own `Aiming → Firing → Resetting` cycle, one
  round per cycle, for every weapon, at every range.
- **The event carries no mode.** `MissionEvent.ShotFired` passes `0` as its
  `ReasonCode`, so even a simulation that chose a mode could not tell the client
  which one it chose.
- **The client hardcodes `FireMode.Single`.** `SandataGame.SoundShotsFiredOn`
  passes that literal for every shot, so `SandataSoundPlayer`'s automatic path —
  which exists, is correct, and holds one loop instance and one tail instance
  per shooter exactly as section 10 requires — is unreachable.

### The decision

- Stage 11 computes the range to the operator's own resolved target and calls
  `FireModeSelection.SelectMode` with the firearm's own bands and mode set. A
  `null` result means no engagement and no shot.
- For `Auto`, the weapon chain still governs ready, turn, and aim: the chain's
  own first resolved shot is the burst's first round. From then on, for as long
  as the operator is raised, in tolerance, not forced lowered, and still on the
  same target, `CyclicFireAccumulator.Advance` produces the subsequent rounds at
  the firearm's `CyclicRpm`. When any of those conditions stops holding, the
  accumulator resets to zero. For every other mode nothing changes: one round
  per chain cycle, exactly as today.
- `MissionEvent.ShotFired` carries the selected `FireModeSet` value in its
  existing `ReasonCode` field. No new field, no new event kind.
- The client maps that value to its own `Audio.FireMode` and passes it to
  `SandataSoundPlayer.HandleShotFired`, and calls `HandleAutomaticFireStopped`
  for a shooter that was firing automatically last tick and is not this tick.

### The audio files do not exist, and that is a spend decision, not a code one

Section 10's model is that a burst plays a *loop* sample plus a *tail*, not one
report per round. `ShotSlotResolver` resolves an `Auto` round to a `GunLoop`
slot. No `GunLoop` file exists on disk: the authorized slice covers
`gun-762x39-single-close`, `gun-762x39-single-indoor`, `gun-9x19-single-close`,
and `gun-9x19-single-indoor`, and nothing else. So wiring alone would turn
audible single shots into silence, which is worse than the defect.

**The decision is a documented fallback, plus a question for the user.** When
`ISandataSoundOutput.Play` declines a loop cue — which is what a missing file
already returns — the player falls back to playing the single-report slot once
per round for that burst. At 600 rounds per minute that is one report every five
ticks, which is audible, continuous, and honest about what it is. It is a
degradation, it is marked as one in the code, and it disappears the moment real
loop and tail files exist.

Generating those files is an ElevenLabs spend and is **not authorized by this
document**. `CLAUDE.md` section 9 keeps every slot outside the shipped forty
behind an explicit authorization, and this design does not grant one. The
concrete ask, if the user wants it, is four rows — a `GunLoop` and a `GunTail`
for 7.62x39mm in the `close` and `indoor` environments — at the catalog's
declared variant count.

## 6. D5 — the theme switcher and the unknown-contact state (`SD-7b`)

`SD-7b` asks a person to view friendly, hostile, and unknown contacts in every
shipped theme. Two things block it, and both are absences rather than defects.

- **`SandataGame.LoadTheme` always takes `catalog.DefaultThemeId`.** The
  catalog ships `daylight-ops` as well as `night-ops`, and nothing in the client
  can reach it.
- **There is no unknown-contact state to look at.** `Sandata.Core.Sensing`
  carries a full `ContactTier` ladder, and stage 5 maintains a real
  `ContactMemory` per operator, but the client draws every operator on the map
  from `MissionState.Operators` regardless of whether anybody has seen it. The
  string `ContactTier` does not appear anywhere in `src/Sandata.Client`.

### The decision

- A theme cycles on a key press, and the choice is not persisted — Sandata has
  no settings file and this design does not add one.
- A hostile operator is drawn according to the assaulting faction's own best
  contact tier for it: an `Identified` hostile draws as it does today, a
  `Detected` one draws as an unknown-contact marker with no facing and no weapon,
  and a hostile nobody has any memory of is not drawn at all.
- Both halves are presentation only. No `Sandata.Core` type changes, no
  simulation state changes, and no hash moves.

**This is not a fog-of-war feature and must not become one.** The simulation is
unchanged; only what the client chooses to draw changes. A defender that is not
drawn is still there, still shooting, and still hashed.

## 7. Determinism consequences, stated before the work rather than after

D1, D3, and D4 all change authoritative state, so they move Sandata's hashes.
That is expected and is accounted for as follows.

- **`SandataRuleset.ContentHash` does not move.** No ruleset constant changes,
  no tick rate changes, no millisecond conversion rule changes, and no firearm
  row changes. The pinned value `8_955_292_433_887_190_872` stands.
- **`SandataPresetId.ModernTacticalV1` does not change.** Section 4's trigger
  list for a new preset version is an enum value or order, a roster order, a
  weapon weight, the tick rate, the conversion rule, or a hash mixer. Appending
  two `MissionEventKind` members at free ordinals is none of those: no existing
  member is renumbered or reordered.
- **`MissionStateTests.PreTask79cBaselineHash` does not move.** It hashes a
  hand-constructed `MissionState`, not a run, and no field is added to the
  hasher.
- **`tests/Sandata.Core.Tests/Fixtures/seed-1-baseline.json` does move**, on
  both fixtures, and is re-measured rather than recalculated — by running the
  capture, never by typing a value. The file's own comment convention records
  which change moved it and why.
- **The recorded seed-1 headless baseline in `docs/development/testing.md`
  moves** and is re-recorded from a real run, with the superseded figures going
  to `docs/development/measurement-history.md`.
- **Hukbo is untouched.** No file under `src/Hukbo.*` changes except
  `Hukbo.Diagnostics/LogEvents.cs`, which gains one `const` string and is shared
  by both games' clients by design.

## 8. The nine questions, section 10 of `SIMULATION-GAME-STANDARDS.md`

Answered once for the package rather than four times, since the four defects
share an answer to most of them.

- **Can a spectator discover this effect without reading source code?** This is
  the question the whole package exists to answer, and today the answer is no
  four times over. Afterwards: a drawn path is walked to its end and the squad
  visibly resumes its own route; a refused path names its reason on screen; a
  rifleman visibly drops his muzzle at a doorway and brings it back up; and
  sustained fire sounds sustained.
- **What does it cost per tick?** D1 is one distance comparison per assigned
  operator, and assignments exist only when a player has drawn a path. D3 is one
  enum comparison per living operator. D4 is one band comparison and one integer
  accumulator advance per firing operator. None of the three adds an allocation.
- **What breaks if it is wrong?** D1 wrong in the strict direction stalls an
  ordered operator; wrong in the loose direction skips a waypoint. D4 wrong
  makes a rifle fire at the wrong cadence, which is audible immediately.
- **Is it deterministic?** Every part is integer arithmetic over authoritative
  state in fixed order. No wall clock, no floating point in `Sandata.Core`, no
  dictionary iteration.
