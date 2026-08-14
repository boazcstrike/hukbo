# Sandata magazine consumption and reload — design

Date: 2026-08-14
Status: design, not yet authorized for implementation
Game: Sandata

This document proposes how a Sandata operator's magazine is consumed by
fire and refilled by a reload. It is written against the working tree as it
stands today, in which `OperatorState.MagazineRounds` is stored, snapshotted,
and hashed, and no tick stage ever changes its value.

## Contents

1. Is this authorised?
2. What exists today
3. Where a round is decremented
4. What triggers a reload
5. How reload interacts with the weapon chain
6. Does an operator carry spare magazines
7. What is hashed, and what events are emitted
8. Does this move the seed-1 baseline
9. What a spectator sees and hears
10. Spectator discoverability (SIMULATION-GAME-STANDARDS section 10)
11. Automatic fire and the unimplemented stop condition
12. What this design does not decide

---

## 1. Is this authorised?

**No. This is a proposal that needs authorisation, not a settled design.**

`CLAUDE.md` section 9, "Do not", forbids starting "terrain, pathfinding,
morale, ammunition (quiver sizes, resupply, or any stock-and-consumption
model for a projectile), persistence migrations, multiplayer, or mod APIs
before the gate that authorizes them." A round leaving a magazine and a
magazine emptying are exactly a stock-and-consumption model for a
projectile, so this feature falls inside that prohibition on its face.

The same bullet immediately narrows itself for the two games at hand: it
records that Hukbo's projectiles and projectile flight time "were authorized
on 2026-08-07 for the ranged-units package alone" and that "ammunition was
not authorized and stays deferred" — a Hukbo-specific closure, not a
Sandata one. It then adds, in the very next sentence, "Sandata's own
navigation and pathfinding are authorized by its design document and are
not covered by that bar; Hukbo's are not." That sentence is evidence about
how the general prohibition is scoped — a Sandata subsystem escapes the
"before the gate that authorizes them" bar only when Sandata's own design
document actually authorizes it, the way it did in writing for navigation
and pathfinding. It is not, by itself, evidence that magazines and reload
are or are not that subsystem; the question has to be settled by reading
the design document, not by analogy to navigation.

Reading `docs/plans/2026-08-07-sandata-scaffold-design.md` section 9 for
that authorization: the section defines `FirearmDefinition.MagazineCapacity`
and `FirearmDefinition.ReloadMs` as fields on every one of the 38 roster
rows, lists "magazine rounds" among the per-operator fields section 4 marks
authoritative and hashed, and states plainly, of automatic fire, that it
"stops when the magazine empties, the target leaves the cone, or the intent
changes; there is no 'burst length' random draw." That sentence is a real
behavioural commitment — a magazine that can empty and that gates automatic
fire is part of the stated design, not an afterthought — and it is the
reason `FirearmDefinition` already carries the two fields today. But that is
where section 9's authorization stops. It does not say where in the tick
pipeline a round is decremented, whether a reload is automatic or
player-triggered, whether an empty weapon self-reloads or waits on an
intent, what a reload does to the weapon-chain phase machine, whether an
operator carries a finite or infinite supply of spare magazines, which new
fields (if any) join the hashed field order, or which events a reload
emits. None of those are stock-and-consumption *policy* decisions that
section 9 makes; they are the exact shape of "any stock-and-consumption
model" the general prohibition is cautious about, left unresolved.

So the honest reading is split down the middle. The *existence* of a
magazine that can run dry, and the field it fills, is already authorized by
the design document, in the same way `MagazineRounds` is already coded,
hashed, and snapshotted today. The *mechanism* — decrementing, reloading,
spare-magazine accounting, the new hash fields, the new events — is not
written down anywhere in the design document and is squarely inside the
class of feature CLAUDE.md section 9 says needs a gate first. This document
therefore treats decrementing and reload as a proposal awaiting the user's
authorization, and every design choice below is written as a
recommendation with its reasoning shown, not as an approved plan. Nothing
in this document should be read as clearance to write the corresponding
code.

---

## 2. What exists today

Six facts, each checked directly against the working tree while writing
this document.

- **`FirearmDefinition`** (`src/Sandata.Core/Weapons/FirearmDefinition.cs`)
  carries `int MagazineCapacity` and `int ReloadMs` on every row, alongside
  `CyclicRpm`. Its doc comment states that field order is part of the
  replay contract: `FirearmRuleset.ContentHash` folds every field of every
  row, in declaration order, so adding, removing, or reordering a field is
  a new preset version with new golden expectations.
- **`FirearmCatalog`** (`src/Sandata.Core/Weapons/FirearmCatalog.cs`)
  populates all 38 rows from two uniform templates rather than 38
  independently researched values: every rifle carries `ReloadMs: 2500`
  (the `RifleReloadMs` constant) and a magazine capacity of 20 or 30
  rounds; every pistol carries `ReloadMs: 1600` (`PistolReloadMs`) and a
  magazine capacity between 15 and 20 rounds. The catalog's own doc comment
  is explicit that reload time has "no per-weapon source in either the
  design or the research document" and is a documented provisional
  placeholder, not a measurement, applied uniformly within each weapon
  class.
- **`OperatorState.MagazineRounds`** (`src/Sandata.Core/Simulation/MissionState.cs`,
  line 159) is a plain `int` field, positioned between
  `WeaponChainRemainingTicks` and `CyclicFireAccumulator` in the record's
  declaration order. It participates in `OperatorState.Equals` and
  `GetHashCode`, and `SandataStateHasher.FoldOperator`
  (`src/Sandata.Core/Determinism/SandataStateHasher.cs`, line 335) folds it
  into the state hash at that same position, between
  `WeaponChainRemainingTicks` and `CyclicFireAccumulator`.
- **No stage decrements it.** `SandataSimulation.ResolveShotsThisTick`
  (`src/Sandata.Core/Simulation/SandataSimulation.cs`, around line 850)
  computes how many rounds leave the barrel this tick, for both a single
  chain-resolved shot and a sustained automatic burst, and its own doc
  comment says so directly: "Magazine depletion is the one of those three
  not tested here, because nothing in this worktree consumes
  `OperatorState.MagazineRounds` yet." The field is written to nowhere in
  `src/Sandata.Core`.
- **Every construction site hardcodes the same placeholder regardless of
  weapon.** Both production callers that build an initial `MissionState` —
  `SandataGame.BuildInitialState` (`src/Sandata.Client/SandataGame.cs`,
  line 2637) and `HeadlessRunner.BuildInitialState`
  (`src/Sandata.Headless/HeadlessRunner.cs`, line 446) — construct every
  `OperatorState` with the literal `MagazineRounds: 30`, independent of
  which `FirearmId` that operator's `Firearm` field names. A pistol
  operator (capacity 15 to 20 in the catalog) is therefore already hashed
  today holding more rounds than its own weapon's magazine can hold — a
  pre-existing inconsistency this design has to resolve rather than
  inherit, addressed in section 3.
- **A smoke-checklist archive already recorded the gap in plain language.**
  The 2026-08-14 close-out for the SD-5 smoke family, since archived, says:
  "Nothing consumes a magazine. `MagazineRounds` is stored and hashed and
  no stage decrements it, so automatic fire never runs a weapon dry." That
  archived document is reference only, per this repository's archive
  policy, and is named here in prose rather than linked.

Two adjacent facts bear on question 7's answer later:
`SandataSoundCatalog` (`src/Sandata.Client/Audio/SandataSoundCatalog.cs`)
already declares a twelve-row `Mechanism`, action family — magazine out,
magazine in, and bolt rack, across the four mechanism groups, 48 declared
variants — but, per `docs/weapons/guns/sound-catalog.md`, has zero
generated files for any of them; and no `MissionEventKind` member exists
for a reload today, so the event stream cannot currently narrate one even
where the catalog has the sound slots reserved for it.

---

## 3. Where a round is decremented

**Recommendation: in the same stage that already decides how many rounds
leave the barrel this tick — design section 5's stage 11, "advance every
weapon timing chain by one tick" — inside `ResolveShotsThisTick`,
immediately after `shotCount` is computed and before that count is handed
to whatever turns it into `FiredShot` records for stage 12's
`ProposeFire`.**

That placement is not a new seam. `ResolveShotsThisTick` already owns the
one decision this needs: it is the method that turns "the chain resolved a
shot" and "the cyclic accumulator cycled N times" into a single integer
count of rounds fired this tick, for both a single chain-resolved shot and
a sustained automatic burst. Decrementing anywhere else would mean a second
piece of code re-deriving a count `ResolveShotsThisTick` already produced,
which is exactly the kind of duplicated derivation that invites the two
counts drifting apart.

**On the tick a burst would fire more rounds than remain**, the rule is:
clamp the count to what the magazine actually holds, never fire more
rounds than exist, and never let the count go negative.

```
actualShotCount = min(computedShotCount, op.MagazineRounds)
newMagazineRounds = op.MagazineRounds - actualShotCount
```

Concretely, for automatic fire: if the cyclic accumulator's `Advance` call
reports two rounds cycled this tick but only one remains in the magazine,
exactly one round fires, the magazine reaches zero, and the second cycled
round is discarded rather than carried into the next tick or borrowed from
a spare magazine mid-tick — a reload, once triggered, always starts a
fresh, full magazine (section 5), never a partial one topped up from a
discarded remainder. The cyclic accumulator's own state still advances
normally regardless of the clamp, because it is a driftless timing device
counting *when* rounds would cycle, not *whether* they can; whether they
can is exactly what the clamp answers.

**The zero-rounds case is a dry fire, not a silent no-op.** If
`ResolveShotsThisTick` is asked to resolve a shot — the chain's `Firing`
phase completed, or the accumulator cycled — while `MagazineRounds` is
already `0`, no round leaves the barrel, `actualShotCount` is `0`, and the
result should be distinguished from "no engagement" (the range-band rule's
own null result) so a caller can react to it — trigger a reload rather than
silently doing nothing. `SandataSoundCatalog` already declares a `Dry`
fire family, eight caliber-family rows at three variants each, with zero
files generated for any of them; a dry-fire result is exactly what that
family exists to narrate, once a `MissionEventKind` member exists for it
(section 7) and once the client wires that event to the catalog's `Dry`
family — an additive change to the sound-slot wiring, not a change to the
audio-generation authorization, since declaring a row and playing a
declared-but-ungenerated row as silence both already happen elsewhere in
this catalog with no ElevenLabs call involved.

---

## 4. What triggers a reload

**Recommendation: automatic reload on empty, and automatic reload only.**
No tactical reload while an operator still has ammunition and no player
order to reload mid-engagement, in v0.1 of this feature.

Reasoning:

- **Empty-triggered reload is the smaller, more determinate decision.** It
  fires from one condition — `MagazineRounds == 0` and the operator's
  intent still calls for engagement — that section 3 already produces as
  the dry-fire result. A tactical reload ("top off while not engaged, so
  the next fight starts full") is a second, independent trigger that has to
  answer its own questions: how "not engaged" is measured, whether it can
  interrupt movement, whether an operator mid-squad-advance stops to top
  off unprompted. None of that is specified anywhere in the design
  document, and inventing it here would be exactly the kind of
  "the invented rule smuggled in as an implementation detail" this
  repository's workflow has already had to catch and back out once for
  the alert-level transition rule (design section 5's 2026-08-07
  amendment). A tactical reload is a legitimate later addition; it is not
  this document's decision to make by default.
- **Automatic rather than by intent.** The chain's `raiseRequested` flag,
  the range-band selection, and the weapon-lowered rule are all already
  automatic reactions to state rather than a player- or AI-issued intent
  distinct from "engage" — `OperatorIntent.Engage` is the only intent the
  chain currently reads, and reload is naturally the same shape: a
  mechanical consequence of running dry while still trying to fight, not a
  new value on `OperatorIntent` that intent selection (stage 8) would have
  to learn to emit. This also keeps reload out of the order layer
  (design section 16) entirely for v0.1 — a player order to reload on
  demand is a plausible future order kind, but it is additive to whatever
  this document settles, not a precondition for it.
- **The alternative — a player-issued reload order** — was considered and
  set aside for the same reason as the tactical reload: the order layer's
  own precedence rule (design section 16, "the per-tick movement-source
  rule") governs movement orders specifically, and extending it to a
  weapon-state order is new order-layer surface with no existing hook to
  attach to. If the order layer eventually gains a `Reload` order kind, it
  should be able to force the same state transition this section
  describes early, but that is a straightforward extension of an
  already-automatic mechanism, not a redesign of it.

The empty-triggered reload therefore engages exactly when a dry-fire result
occurs (section 3) while the operator's intent is still `Engage` — an
operator that has stopped trying to fight does not begin an automatic
reload it was never asked to justify, which keeps the trigger legible
without a decay timer or a distinct "wants to reload" flag.

---

## 5. How reload interacts with the weapon chain

**Recommendation: reload is a new phase appended to `WeaponChainPhase`, and
`WeaponChain.Advance` gains an ammo signal so the phase machine itself is
the single source of truth for whether a round can leave the barrel — not
a second, parallel state machine layered on top of it.**

`WeaponChainPhase` today is `Lowered = 0, Raising = 1, Turning, Aiming,
Firing, Resetting`. Appending `Reloading` as the next numeric value is safe
under the enum's own append-only rule — nothing renumbers, reorders, or
removes an existing member — the same way `MissionEventKind` and every
other Sandata enum stays append-only-safe by construction. The tick-derived
duration is `TickConversion.ToTicks(definition.ReloadMs, ruleset.TickRate)`,
the one pinned millisecond-to-tick rule design section 4 requires, with no
new conversion path. At the two authored values in the catalog today that
conversion is exact: 2,500 ms at 50 Hz is precisely 125 ticks for every
rifle, 1,600 ms is precisely 80 ticks for every pistol — neither value
needs the rule's rounding behaviour to be exercised, though a future
per-weapon reload time might.

**`WeaponChain.Advance` already takes three caller-supplied booleans it
reacts to rather than computes — `forceLowered`, `raiseRequested`,
`arcWithinTolerance`.** The cleanest way to add ammo without inventing a
second layer that could disagree with the first about which tick something
happens on is to add a fourth: `hasAmmo`, true whenever
`op.MagazineRounds > 0`. The walk that reaches `Firing` today reaches it
unconditionally once `Aiming`'s ticks are spent; under this change it
checks `hasAmmo` at that same point and, if false, enters `Reloading`
instead of `Firing` — never firing a phantom round and never returning a
`Firing` result the caller has to double-check against ammunition
separately. `Reloading` behaves exactly like `Resetting` structurally: it
is tick-counted, it is charged one tick per call the same way
`Raising`/`Aiming`/`Resetting` are, and on completion it transitions to
`Aiming` with a fresh `aimTicks` — not back through `Raising` or `Turning`,
because the weapon is already up and already pointed; only the aim
confirmation needs to run again before the next shot, mirroring how
`Resetting` already returns to `Aiming` rather than to `Raising`.

**Step 1's unconditional rule extends to `Reloading` with no special
case.** The weapon-lowered rule already wins immediately and
unconditionally over every other phase, cancelling an in-progress shot
rather than letting a same-tick cascade finish it late; applying the same
rule to `Reloading` — a doorway crossing or a wall approach cancels an
in-progress reload exactly as it cancels an in-progress aim, with no
partial credit toward the reload's tick count — is not a new decision, it
is the existing rule applied to a new phase, and keeping it unconditional
avoids a second special case in a method whose entire value is having
exactly one documented pass.

**Sustained automatic fire is the one place this recommendation is not a
completely clean seam**, and it is worth being honest about that rather
than hiding it. Automatic fire's per-round cadence is driven by the
`CyclicFireAccumulator`, which is deliberately a second, parallel piece of
hashed state (design section 9) rather than a `WeaponChainPhase` value —
the phase stays at `Aiming` for the whole burst while the accumulator
cycles rounds underneath it. Section 3's clamp can bring `MagazineRounds`
to zero in the middle of that burst, on a tick `WeaponChain.Advance` is not
even called for a phase transition (the phase was already `Aiming` and
stays `Aiming` from the chain's point of view). Two ways to resolve that,
both consistent with the phase-owns-ammo choice above:

- **(a) Recommended.** Extend the same `hasAmmo` gate so that, on the tick
  the clamp reports the magazine reached zero, the *caller*
  (`AdvanceWeaponChain`, which already computes `ResolveShotsThisTick`'s
  result before writing the operator's new state back) forces the stored
  phase to `Reloading` in that same write, rather than leaving it at
  `Aiming`. This keeps `WeaponChain.Advance` itself simple — it only ever
  reasons about the chain-resolved single shot — while still guaranteeing
  the two round-producing paths (chain-resolved and accumulator-sustained)
  converge on the identical `Reloading` phase and the identical
  tick-derived duration the moment either one empties the magazine.
- **(b) Not recommended.** Give the accumulator itself a "reload" output
  and let its caller decide what to do with it independently of the
  chain's own phase. This was rejected here because it produces two
  different places in the code that can each independently decide "start a
  reload", which is the exact kind of duplicated derivation section 3
  already rejected for the round count itself.

This section's recommendation is written at the level of "which phase, which
signal, which tick source" rather than literal code, and a reviewer may
reasonably prefer option (b) or a different phase-graph shape; the load-bearing
claim is only that reload has to be resolved inside the one documented pass
the chain already owns, not bolted on beside it.

---

## 6. Does an operator carry spare magazines?

**Recommendation: infinite spares. A reload always refills `MagazineRounds`
to `FirearmDefinition.MagazineCapacity` and costs only time
(`ReloadMs`/`ReloadTicks`), and no new field counts how many magazines an
operator has left.**

This is a deliberate, narrow recommendation, and the reasoning is the
distinction CLAUDE.md section 9 itself draws between the two shapes:

- **A finite spare-magazine count is exactly the stock-and-consumption
  model the "Do not" bullet names.** It needs its own authoritative field
  (spare magazines carried), its own initial-loadout decision (how many
  does an operator start with — a new, undesigned number with no source in
  the research consolidation or the design document, the same problem
  `FirearmCatalog`'s doc comment already flags for reload time itself), its
  own exhaustion behaviour (what happens when the last spare is gone — does
  the operator go permanently dry, switch to a sidearm that design section
  9 does not model as an automatic weapon swap, or something else
  undecided), and its own visible state for the roster strip and the agent
  inspector. Every one of those is a real design decision this document
  would otherwise be inventing wholesale, in a feature CLAUDE.md section 9
  is already cautious about, on a subject the scaffold design document
  never once mentions ("spare", "resupply", and "carried magazines" do not
  appear anywhere in it).
- **A reload that only costs time is a much smaller feature, closer to what
  the scaffold design already committed to.** Section 9's own sentence —
  "automatic fire stops when the magazine empties" — describes a magazine
  that empties and, implicitly, a weapon that can fire again, without ever
  describing a supply that runs out permanently. `ReloadMs` existing as a
  field on every one of the 38 rows, with no adjacent "spares carried"
  field anywhere in `FirearmDefinition`, `OperatorState`, or the design
  document's own field list, reads as the design already having assumed
  the smaller shape without stating it outright.
- **It is the reading that makes the feature's authorization question
  narrower, not the reading that avoids it.** This recommendation does not
  argue the feature is authorized — section 1 already found it is not —
  only that, if and when the user authorizes it, the time-cost-only shape
  is the smaller ask and the one closer to what section 9 already
  describes, so it is the one this document recommends putting in front of
  the user rather than the finite-stock shape.

If the user wants a finite spare-magazine economy later, this recommendation
does not foreclose it: it would add a new authoritative field (for example
`SparesRemaining`) and a new exhaustion rule on top of the phase machine
section 5 describes, without changing that machine's shape. That is future
work, not a compatibility hazard this document is creating now.

---

## 7. What is hashed, and what events are emitted

**No new hashed field is required for the core mechanism.**
`OperatorState.MagazineRounds` is already hashed at its existing position,
between `WeaponChainRemainingTicks` and `CyclicFireAccumulator`
(`SandataStateHasher.FoldOperator`, line 335), and this design changes what
writes to that field, not its presence or position. `WeaponChainPhase` is
already hashed as a raw `int` at its existing position, and appending
`Reloading` as a new named value (section 5) widens the range of values
that field can legitimately hold without moving where the field sits in
`FoldOperator`'s fixed order or in `OperatorState`'s own `Equals`/
`GetHashCode`. Under section 6's infinite-spares recommendation, there is
no new "spares remaining" field to place at all. If the user instead
authorizes the finite-spares alternative section 6 sets aside, that
alternative would need its own new hashed field, appended at the end of
`OperatorState`'s field list and at the end of `FoldOperator`'s fold order
— the same append-only positioning `Firearm` itself already followed when
it was added after every field this design touches — and that is a
decision this document explicitly leaves to whoever authorizes that
alternative, not one it makes here.

**New `MissionEventKind` members, appended after the existing `WeaponRaised
= 5`** (append-only, matching every existing member's own convention):

- **`DryFire`.** Emitted the tick `ResolveShotsThisTick` produces the
  zero-round result section 3 describes — the chain attempted to resolve a
  shot, or the accumulator attempted to cycle one, and `MagazineRounds` was
  already zero. One event per attempted-and-denied shot, not one per tick
  spent empty, mirroring `WeaponLowered`'s own rule of firing on the
  transition rather than on every tick the state holds.
- **`ReloadStarted`.** Emitted the tick the phase transitions into
  `Reloading`, whether by section 3's zero-ammo Firing-time gate or by
  section 5's mid-burst accumulator path — one event regardless of which
  of the two trigger paths caused it, so the event stream tells a
  spectator "a reload began" without exposing which of the two internal
  code paths produced it.
- **`ReloadCompleted`.** Emitted the tick the phase transitions out of
  `Reloading` into `Aiming` with a full magazine. Paired with
  `ReloadStarted` the same way `WeaponRaised` is paired with
  `WeaponLowered` — recording only half of a two-state transition is not
  a history of that state, and this repository already corrected that
  exact gap once for the lowered flag.
- **A reload cancelled by the weapon-lowered rule (section 5's step 1) is
  not a fourth event.** It is a `WeaponLowered` transition like any other
  — the existing event already tells the story "the weapon went down"; a
  reader does not additionally need a bespoke "reload was interrupted"
  event to reconstruct that the magazine did not finish refilling, because
  `MagazineRounds` in the next published snapshot is still whatever it was
  before the interrupted reload, unchanged, per section 3's clamp rule
  that a reload only completes and refills atomically at
  `ReloadCompleted`.

All four events fold into the event hash through the same ordered stream
every other `MissionEventKind` member already does; nothing about how the
event hash is computed changes, only which member values it can now carry.

---

## 8. Does this move the seed-1 baseline?

**Yes, plainly. Both hashes move, and that has a real cost.**

The reasoning is direct: the seed-1 workload fires real shots today — the
automatic-fire fix landed 2026-08-12 specifically to make the roster's
`Auto`-capable weapons fire more than once per chain cycle, and every
weapon in the roster has always resolved single shots through the chain
even before that. `MagazineRounds` is initialized to a placeholder `30`
for every operator regardless of weapon (section 2), and several catalog
rows — every pistol, and the two rifle rows at capacity 20 — hold fewer
rounds than that placeholder already implies is available. Once decrementing
is real, any mission that runs long enough for an operator to fire 15 to 30
rounds — well within a single engagement for an automatic weapon at the
roster's cyclic rates, and comfortably within the seed-1 workload's run
length — changes that operator's `WeaponChainPhase`,
`WeaponChainRemainingTicks`, `MagazineRounds`, and `CyclicFireAccumulator`
from what the current baseline recorded, on some tick strictly earlier than
that operator would otherwise have kept firing uninterrupted. That
divergence is exactly the class of change `SandataStateHasher.FoldOperator`
exists to detect, and it does not stay contained to state: every new event
this design proposes (`DryFire`, `ReloadStarted`, `ReloadCompleted`) adds
entries to the ordered event stream on a mission that, today, produces none
of them, so the event hash moves independently and for an independent
reason, exactly as design section 4 intends the two hashes to be capable of
doing.

**What that costs:**

- **`SandataRulesetTests`'s pinned `SandataRuleset.ContentHash`
  (`8_955_292_433_887_190_872`) is unaffected by this change on its own.**
  Nothing here changes an enum's numeric value, an enum's order, the roster
  order, a weight, the tick rate, or the millisecond-to-tick conversion
  rule — the four triggers `CLAUDE.md` section 5 and design section 4 name
  for a new preset version. Appending `Reloading` to `WeaponChainPhase` and
  appending three members to `MissionEventKind` are additions, not
  renumbers, matching the precedent already set when automatic fire itself
  went from unimplemented to real on 2026-08-12 without a preset bump.
- **The recorded seed-1 baseline in `docs/development/testing.md`, and the
  two golden fixtures in `tests/Sandata.Core.Tests/Fixtures/seed-1-baseline.json`,
  do need to be recaptured.** Those are recordings of what the simulation's
  *behaviour* produces, not of the preset's identity, and this design
  changes that behaviour deliberately — a weapon that used to never run dry
  now can. The correct response to that is recapturing the baseline against
  the new, intended behaviour, not treating the old baseline as ground
  truth to defend, because the old baseline recorded a known gap
  (section 2's archived smoke-checklist note) rather than a considered
  outcome.
- **Any other pinned absolute hash literal in the test suite that exercises
  a mission running long enough to matter is subject to the same
  recapture**, including any smoke or determinism fixture that asserts a
  specific state or event hash for a run of realistic length. This document
  does not enumerate every such test site — that is implementation-stage
  work — but flags that the recapture is not confined to the one named
  baseline file.
- **No new preset version is required by this design as written**, but a
  reviewer should treat that as this document's best reading of the
  existing rule rather than as settled: if the eventual implementation ends
  up changing something this design did not anticipate — for instance,
  reordering an existing field rather than appending — the general rule
  reasserts itself and a preset version is required after all.

---

## 9. What a spectator sees and hears

**No new sound.** Generating audio for this feature is not authorized.
`SandataSoundCatalog` already declares the two families this feature would
naturally reach for — an eight-row `Dry` fire family (three variants each,
one row per caliber family) for the click a dry trigger pull produces, and
a twelve-row `Mechanism`, action family (magazine out, magazine in, bolt
rack, across the four mechanism groups, four variants each) for the
physical sounds of a reload — but, per `docs/weapons/guns/sound-catalog.md`,
zero files exist on disk for either family, for any caliber or mechanism
group. Wiring `DryFire`, `ReloadStarted`, and `ReloadCompleted` (section 7)
to those already-declared rows costs nothing and generates nothing, exactly
as declaring the twelve-row action family itself already cost nothing —
but until the user separately authorizes generating those specific rows
through `./scripts/sfx.ps1`, a dry fire and a reload both resolve to
silence through the same negative-cache path every other ungenerated
catalog row already resolves through. This design does not ask for that
audio spend, and implementing decrementing and reload does not require it.

**Visually, what is observable today is less than it looks.** Two client
surfaces already carry the vocabulary this feature would use, and they are
in two different states of readiness:

- **`OperatorInspector` already draws live, real text**, per its own
  `BuildLines` method and per the archived smoke-checklist note that "the
  operator inspector ... do[es] draw rows" — unlike the roster strip below.
  Its weapon-chain-phase row, `FormatChainPhaseLine`, reads
  `"Chain: {ChainPhase} ({ChainRemainingTicks}t)"` directly from
  `WeaponChainPhase`, so the moment `Reloading` exists as a phase value and
  something actually enters it, opening the inspector on that operator
  shows `"Chain: Reloading (37t)"` counting down with no new inspector code
  at all — the same free legibility the firearm row and the lowered-state
  row already got when they were added on 2026-08-14. `InspectorContent`
  has **no magazine-rounds field today**, so the round count itself would
  need one, plus a corresponding format line, to be visible there.
- **`RosterStrip` is not actually drawing text at all.** It declares a
  `TileContent` record with a `MagazineRounds`/`MagazineCapacity` pair and
  a tested `FormatMagazineLine` helper producing `"18/30"`, matching design
  section 11's HUD list, which marks the roster strip **built**. But
  `SandataGame.DrawRosterTiles`, the only production caller that draws a
  roster tile, draws only the tile's background panel and border — it
  never constructs a `TileContent` and never calls `FormatMagazineLine`,
  `FormatHealthLine`, or `FormatChainPhaseLine`. The archived smoke record
  confirms this independently: "the contact list, mission clock, roster
  strip, and go-code panel are still blank rectangles." A magazine count
  is therefore not observable on the roster strip today regardless of this
  design, and wiring `DrawRosterTiles` to the helpers that already exist
  and are already tested is the natural, low-cost way to close that gap —
  but it is drawing-layer work outside what this document is scoped to
  decide, since the helpers and their tests already exist independently of
  whether decrementing is ever implemented.

**What a spectator can discover without any of the above is smaller but
real even today.** `WeaponChainPhase` is already an inspector-visible
value, so once `Reloading` exists and something enters it, a spectator who
opens the inspector on the right operator at the right tick already sees
"the weapon stopped firing and entered a distinct, named, counting-down
state" — which is a legible, source-code-free answer to "why did this
operator stop shooting", even before a magazine-rounds row or any sound
exists. It is a weaker answer than a visible round counter and an audible
reload, and section 10 states plainly why a weaker answer is not enough on
its own.

---

## 10. Spectator discoverability (SIMULATION-GAME-STANDARDS section 10)

Section 10's ninth item requires a "spectator explanation: reason code,
event, or inspector field" for every feature, and its own reviewer
checklist restates the same requirement as "the change exposes an
inspectable reason for autonomous behavior." Answering it honestly for this
design, in its state as written rather than in a hoped-for later state:

**Partially met as designed; not fully met until the client wiring section
9 flags is also done.**

- **Reason code.** This design adds none. It relies on the weapon-chain
  phase itself carrying its own reason: an operator in `Reloading` is, by
  construction, an operator not firing because it is reloading, the same
  way `WeaponChainPhase.Lowered` already serves as its own reason for "not
  firing because forced down." A state that already carries its own name
  needs no separate reason-code field.
- **Event.** `DryFire`, `ReloadStarted`, and `ReloadCompleted` (section 7)
  satisfy this channel directly and immediately, independent of any
  client-side drawing work — an event exists in the authoritative stream
  the moment the simulation emits it, whether or not any renderer ever
  reads it.
- **Inspector field.** Partially satisfied today, fully satisfied only
  after further work this document does not scope. `OperatorInspector`
  already renders `WeaponChainPhase` live, so `Reloading` becomes visible
  there for free the moment something actually enters it — that much is
  inspector-field compliance with no additional work. But the round count
  itself is not visible in either the inspector or the roster strip today,
  for the reasons section 9 gives, so a spectator cannot yet see how close
  to empty an operator was before the reload started.

The honest overall verdict: decrementing and reload, exactly as this
document specifies them, are discoverable without reading source code in
the narrow sense that a phase name and three events exist and need no
further wiring to be inspectable — but they are not fully legible in the
sense section 10 is really asking, "can a spectator watching normally
understand what just happened", until the round count itself is drawn
somewhere a spectator can look. That gap is named again in section 12 as
something this document does not close.

---

## 11. Automatic fire and the unimplemented stop condition

Design section 9 states the rule directly: **"automatic fire stops when
the magazine empties, the target leaves the cone, or the intent changes;
there is no 'burst length' random draw."** `ResolveShotsThisTick`'s own doc
comment confirms only two of those three conditions are implemented today:
"Magazine depletion is the one of those three not tested here, because
nothing in this worktree consumes `OperatorState.MagazineRounds` yet."

**What implementing magazine depletion changes about the burst a spectator
hears.** Today, an automatic weapon sustains fire — the accumulator keeps
cycling rounds — for as long as the operator stays aimed within tolerance,
in an automatic-fire range band, and not lowered. Those conditions hold, in
practice, for as long as the target is alive, visible, and in the cone,
because losing any of them is what changes the operator's intent away from
`Engage` in the first place. **The burst's ceiling today is therefore set
by the target dying or breaking contact, not by ammunition** — exactly the
gap this section's heading names. Once decrementing is real, a burst that
would previously have continued firing at a target that is still alive,
still visible, and still centred now stops earlier, at whichever tick
section 3's clamp brings `MagazineRounds` to zero mid-cycle. A spectator
listening to two otherwise-identical engagements can, for the first time,
hear a burst end for two audibly and mechanically different reasons: the
target went down (or broke the cone), or the shooter ran dry and is now
reloading. That distinction is the whole reason the archived smoke record
calls the current behaviour a known gap rather than a cosmetic one —
"automatic fire never runs a weapon dry" is not merely a missing sound cue,
it is a missing outcome a real firefight always has and this simulation
currently never produces.

This also means the roster's magazine capacities (20 to 30 rounds for
rifles, 15 to 20 for pistols) become load-bearing the moment this ships,
in a way they are not today: at an 800 rpm cyclic rate a 30-round magazine
empties in roughly 2.25 seconds of sustained automatic fire, which is well
within a single sustained engagement at the ranges this game's doorway
geometry produces, so magazine depletion is not a rare edge case this
feature adds for completeness — it is something a spectator should expect
to see routinely once it works.

---

## 12. What this design does not decide

- **Whether the user authorizes this feature at all.** Section 1 is the
  answer to that question as of this document's writing: no, not yet.
  Nothing below that section is a green light.
- **A tactical reload** — topping off while not engaged — is explicitly not
  decided here (section 4). If it is wanted later, it needs its own
  trigger rule, its own decision about whether it can interrupt movement or
  squad advance, and its own interaction with the phase machine section 5
  describes.
- **A player-issued reload order** through the order layer (design
  section 16) is not decided here. Section 4 notes it as a plausible future
  extension of the same automatic mechanism, not a precondition for it.
- **A finite spare-magazine economy** — how many magazines an operator
  starts a mission carrying, and what happens when the last one is spent —
  is explicitly set aside in favour of infinite spares with a time-only
  cost (section 6). That alternative is not ruled out permanently, only
  deferred as its own, separately authorized decision.
- **Whether an operator switches to a sidearm when a primary weapon's
  supply is exhausted.** Under this design's infinite-spares
  recommendation the question does not arise, because a primary weapon
  never permanently runs out; it is only relevant under the finite-spares
  alternative this document does not adopt, and is not designed here even
  conditionally.
- **Making the round count itself visible to a spectator.** Sections 9 and
  10 name the gap — no `InspectorContent` field, no `RosterStrip` drawing
  call — and recommend closing it as the natural next step, but the actual
  UI wiring, including any new pure-helper tests for it, is implementation
  work this document does not specify line by line.
- **Any audio generation.** The `Dry` and `Mechanism` action families are
  already declared with zero files; this document proposes wiring events to
  those rows, never generating the rows themselves. Generating them remains
  outside the authorization CLAUDE.md section 9 already grants.
- **Per-weapon reload timing research.** `ReloadMs` stays the two flat,
  already-documented-as-provisional template values (2,500 ms for every
  rifle, 1,600 ms for every pistol) `FirearmCatalog` already carries; this
  document does not propose researching or varying it per weapon.
- **The literal C# signature of any changed method.** Section 5 states the
  shape of the recommended change — a new phase, a new boolean signal into
  `WeaponChain.Advance` — at the level a reviewer can agree or disagree
  with, not as a diff. The exact parameter list, field names, and call-site
  edits are implementation-stage decisions.
- **Which specific pinned test literals need updating.** Section 8 names
  the two it can point to by file — the recorded seed-1 baseline and the
  golden fixture pair — and states plainly that other pinned hashes over a
  realistic-length run are also at risk, without enumerating every test
  file in the suite.
- **Whether a new preset version is ultimately required.** Section 8 gives
  this document's best reading of the existing rule — no, because nothing
  here renumbers an enum, reorders a roster, changes a weight, or changes a
  hash mixer — but states plainly that an implementation which departs from
  this design's specifics could re-trigger that requirement, and leaves the
  final call to whoever reviews the actual change against
  `SandataRulesetTests`.



