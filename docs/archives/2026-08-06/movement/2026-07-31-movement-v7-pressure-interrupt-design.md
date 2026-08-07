# Movement V7 pressure interrupt — design

> **Archived: reference only.** The movement V7 pressure-interrupt workstream
> finished and merged to main on 2026-08-06. V7 shipped as a registered, pinned,
> fully tested preset that is reachable only by explicit selection, and it does
> not meet the design section 2.1 termination bar at any tuning. Decision D6
> stands: `Scenario.MovementPreset` remains `PersistentContingentsV4`. Do not
> execute this plan; its task list, line numbers, and verification steps are
> historical. The dated annotations inside record where measurement overturned
> what the document originally claimed.

Date: 2026-07-31
Status: **design only. No code written, no test run, the canonical gate not
invoked by this document.**

A design document does not authorize implementation. The ordered task list and
its verification criteria live in
[`2026-07-31-movement-v7-pressure-interrupt.md`](2026-07-31-movement-v7-pressure-interrupt.md).

## 1. What this builds and why

`MovementPresetId.EquipmentRelativeFootworkV6` is complete and fully tested, and
it is unusable as a default. Every measured run at two hundred and at five
hundred agents ends in `Draw` at the ten-thousand-tick limit with both sides
substantially alive. The cause is structural rather than a matter of tuning.

`WeaponMovementRules.ResolveProvisionalFootwork`
(`src/Hukbo.Core/Movement/WeaponMovementRules.cs`, lines 192 through 280)
evaluates a ten-step, first-match ladder. Step 2 at lines 217 through 222
returns for every agent whose prior phase was `FootworkPhase.Commit`, whether
the commitment is continuing or expiring. Step 3 at lines 226 through 229
returns for a *continuing* `FootworkPhase.Recover`; an expiring `Recover` falls
through. The local support counts are first turned into a comparable quantity at
line 231, which is below both of those returns, so step 4 — the disengagement
release — and step 5 — the disengagement entry — are unreachable for any warrior
inside the attack lifecycle.

A warrior in sustained contact spends every tick inside that lifecycle. The
number of ticks per steady-state cycle that fall through to the ratio steps is
`max(0, cooldown - (commitment + recovery))`. Under combat preset
`PrecolonialPhilippinesV2` that value is zero for four of the six canonical
loadout rows — shielded Kalis, shielded Itak, Wasay, and Itak — and exactly one
for the remaining two, Kampilan and Kalis. Four of six rows therefore never
consult the disengage threshold their own weapon session specified and tested,
and the two that do get one tick in which to notice.

The consequence a spectator sees is a battle that never resolves. The
consequence the repository sees is a count-sensitive posture table that five
weapon sessions specified, tested, and shipped, and that no spectator can
discover by watching, which is exactly what
`SIMULATION-GAME-STANDARDS.md` section 10 question 8 forbids.

> **Annotation, 2026-08-06 (task F2). The premise above — "a warrior in
> sustained contact spends every tick inside that lifecycle" — is false, and
> it is the load-bearing sentence of this whole diagnosis.**
>
> `docs/archives/2026-08-06/movement/2026-07-31-movement-v7-baseline.md` measured the 200-agent seed-1
> cell directly: 1,140,221 agent-ticks in `FootworkPhase.Refuse` and 338,634
> regrouping, against 2,216 committing and 2,017 recovering. That is a ratio of
> about 349 to 1 *against* being inside the attack lifecycle. Warriors are not
> spending every tick inside it; they are overwhelmingly refusing to enter it
> at all.
>
> The ladder analysis above it remains correct as far as it goes — steps 4 and 5
> genuinely are unreachable for a warrior inside the lifecycle, and four of six
> rows genuinely do have a zero-tick window. What is wrong is the inference that
> this is *why* battles do not resolve. The shadowed steps affect under three
> tenths of one per cent of the run. The standoff lives in the other 99.7%,
> upstream of anything this design touches.
>
> The original text is left standing because it is the reasoning the whole
> feature was built on, and section 5 of
> `docs/archives/2026-08-06/movement/2026-07-31-movement-v7-calibration-record.md` traces the
> measurement that overturned it.

This design adds a **pressure interrupt**: a weighted sum of three signals that,
when it crosses a per-row threshold, breaks a committed warrior off the attack
lifecycle and into `FootworkPhase.Disengage`, at the cost of re-charging that
warrior's attack cooldown to full. It ships as a new preset,
`EquipmentRelativeFootworkV7`, and it does not move the shipped default.

Everything decided here executes the settled brief in
[`2026-07-31-movement-v7-calibration-decisions.md`](2026-07-31-movement-v7-calibration-decisions.md),
committed at 5c1d0b0. That document's six decisions are not reopened.

## 2. Terms restated in full, because the archive is not an authority

`docs/archives/` is deprecated by definition and may not be cited as
justification. Two criteria the V7 work is measured against currently live only
in archived session material, so they are restated here in full and this
document is their live home.

### 2.1 The termination bar (decision D2, part one)

V7 is calibrated when seeds 1, 2, 3, 5, and 8, at both two hundred and five
hundred agents, each reach a decisive outcome — anything other than
`BattleOutcome.Draw` — within **6,000 ticks**.

Six thousand is chosen against the measured `PersistentContingentsV4` spread,
which lands between 981 and 2,934 ticks across the same cells. The bound leaves
V7 room for the additional deliberation weapon-relative footwork is supposed to
introduce, while making a standoff equilibrium a failure rather than a
curiosity. The number is a gameplay-tuning choice and a provisional
reconstruction, not a measurement of anything historical.

### 2.2 The performance metric (decision D2, part two)

The budget is the median `p50Milliseconds` measured against
`PersistentContingentsV4` on the same machine, over the same seeds. The
ceilings are unchanged: 2.0× at two hundred agents and 2.5× at five hundred. The
elapsed-divided-by-measured-ticks reading is removed from this plan entirely,
because it divides a ten-thousand-tick run by a roughly two-thousand-tick run
and so rewards V6 for never terminating. A change whose entire purpose is to
make the simulation terminate cannot be gated on a metric that pays for not
terminating.

The measurement protocol is five seeds, one discarded warm run per cell, report
the median. The workload runs under combat preset
`PrecolonialPhilippinesV2`, pinned explicitly, because the shipped combat
default `PrecolonialPhilippinesV4` rosters four solo loadouts and never pairs a
shield with any weapon
(`src/Hukbo.Core/Combat/PhilippineCombatPresetV4.cs:194-197`). A workload run
under the shipped default would never field the `KS` or `IS` rows, and those two
rows have the zero-window attack lifecycle that motivated this feature. The V6
freeze fixture already pins V2 for the same reason
(`tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs:336`).

If V7 meets the termination bar and still fails the `p50Milliseconds` ceiling,
the performance work is separate work and must not be recorded as a calibration
failure. `ResolveCollisions` already consumes between 58.11% and 77.44% of tick
time per `docs/research/TICK-STAGE-PROFILE.md`; that is flagged, and flagging is
not authorization to touch it.

> **Annotation, 2026-08-06 (task F2). This deferral does not apply to V7, and a
> reader must not invoke it.**
>
> The sentence is conditional on V7 meeting the termination bar. V7 does not
> meet it — no cell does, at any measured tuning — so the antecedent is false
> and the `p50Milliseconds` overrun is recorded as a plain failure rather than
> as deferred performance work. Task F2 measures 3.44× against a 2.0× ceiling
> at two hundred agents and 4.02× against a 2.5× ceiling at five hundred.
>
> The overrun is nonetheless not a cost the interrupt introduced. V6 already
> carried substantially all of it at zero firings, which
> `docs/archives/2026-08-06/movement/2026-07-31-movement-v7-calibration-record.md` section 4.2 works
> through. That is an observation about where the cost lives, not a licence to
> defer it under this paragraph.

### 2.3 The phase-flip criterion (decision D3)

The shared acceptance criterion rejects a preset if the phase or posture flips
on more than 25% of ticks after the first hundred. Measured over ticks 101
through 400, the shipped rows ran between 23.3% and 34.7% for Wasay, 60% for
Kalis, and 50% for Itak. The criterion as originally worded cannot be met by any
row, because a pure four-tick commitment plus four-tick recovery rhythm produces
exactly 25.0% on its own, before any genuine decision has been made.

**The metric is redefined to count posture-intent changes only, excluding the
scripted `Commit` and `Recover` attack-lifecycle transitions.** The 25% ceiling
stands, applied to the redefined metric. The redefinition preserves the
criterion's actual purpose — catching indecisive oscillation — and the Kalis 60%
and Itak 50% readings remain a real signal once the lifecycle is excluded.

The pressure interrupt itself produces a transition that is *not* part of the
scripted lifecycle, so an interrupt firing does count against the redefined
ceiling. That is intentional: a preset that interrupts every warrior every few
ticks is oscillating, and the criterion should say so.

> **Annotation, 2026-08-06 (task F2). The mechanism described here is real, but
> the criterion cannot bind on this feature, so it does not do the job this
> paragraph assigns it.**
>
> An interrupt firing is counted, exactly as written. What measurement showed is
> that the interrupt cannot fire often enough to matter to the metric. At the
> shipped values the redefined flip percentage sits between 2.68% and 11.93%
> against a ceiling of 25%. Task E1's maximum-intervention probe — the minimum
> threshold on every row, which makes the predicate fire on every tick it is
> *capable* of firing on — peaked at 13.47%, still barely half the ceiling.
>
> There is no tuning of the weights or the thresholds that pushes this metric
> past 25%, because the transition-only rule in section 4.3 makes the interrupt
> eligible on well under one per cent of agent-ticks. The phase-flip ceiling is
> therefore inert as a guard on the pressure interrupt: it will pass whatever
> the tuning, and a future session must not read a passing flip percentage as
> evidence that the interrupt is well behaved. It is evidence only that the
> interrupt is rare.

## 3. The nine questions of `SIMULATION-GAME-STANDARDS.md` section 10

### Question 1 — user-visible outcome

A spectator watching a melee under V7 sees warriors peel out of a losing knot
instead of trading blows in it forever. Selecting one of those warriors shows,
in the agent inspector, both a running pressure reading against that warrior's
own threshold and an explicit statement that the warrior broke off under
pressure rather than disengaging on the ordinary ratio rule. Battles reach a
decision instead of running to the tick limit.

> **Annotation, 2026-08-06 (task F2). The final sentence is false as shipped.**
>
> Battles do not reach a decision. All ten cells of the section 2.2 matrix end
> `BattleOutcome.Draw` at the 10,000-tick limit under V7, at the shipped values
> and at every other tuning measured during task E1. The re-measurement in task
> F2 reproduced that outcome cell for cell.
>
> The two inspector channels and the pawn mark in the middle of this paragraph
> *are* delivered and are covered by tests. What is not delivered is the
> termination the last sentence promises. A reader taking this section as a
> statement of shipped behaviour would be misled on the one claim that decided
> whether the feature succeeded.

Question 8 below is the full spectator answer; this is the plain-language one.

### Question 2 — tick stage, and the state read and written

The interrupt is evaluated in exactly one place: the loop at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:1583-1639`, inside
`ResolveEquipmentPosturesAndProvisionalFootwork`, which the tick pipeline calls
at `:589` under the equipment-relative-footwork flag. The pipeline order around
it is fixed and load-bearing:

| Order | Stage | Line | Relevance |
| --- | --- | --- | --- |
| 1 | `DecrementCooldowns` | `:581` | Runs *before* the interrupt, so a cooldown written by the interrupt is not decremented on the tick it is written |
| 2 | `SelectTargetsAndIntents` | `:582` | Derives this tick's `LocalMovementContext` values, including `SupportAllies` and `SupportEnemies` |
| 3 | `ResolveContingentStates` | `:583` | — |
| 4 | **`ResolveEquipmentPosturesAndProvisionalFootwork`** | `:589` | **The interrupt fires here** |
| 5 | `GatherEquipmentRelativeMovementProposals` | `:594` | Consumes the finalised phase |
| 6 | `GatherAndCommitAttacks` | `:605` | The attack gate at `:3398` reads the cooldown the interrupt just re-charged |
| 7 | `ApplyEquipmentAttackFootworkAndDeathCleanup` | `:611` | Stamps the three new per-agent fields and clears them on death |

Reads, at stage 4: `agent.FootworkPhase`, `agent.FootworkTicksRemaining`,
`agent.MaximumHitPoints`, `agent.AttackCooldownTicks`, the new
`agent.DamageTakenLastTick` and `agent.PriorSupportAllies`, the tick's
`LocalMovementContext.SupportAllies` and `.SupportEnemies`, the resolved
`LoadoutMovementProfile`, and the three weights on `MovementRuleset`.

Writes, at stage 4: `agent.FootworkPhase` and `agent.FootworkTicksRemaining` by
the existing finalisation path; `agent.AttackCooldownRemaining`;
`agent.ComboStepsRemaining` and `agent.ComboTargetEntityId`; and
`agent.BrokeOffUnderPressure`.

Writes, at stage 7: `agent.DamageTakenLastTick` from the existing
`_damageTotals` scratch array; `agent.PriorSupportAllies` from the tick's
`LocalMovementContext`; and all three new fields cleared on a dead agent
alongside the four the pass already clears at `:2596-2599`.

**The cooldown write is a deliberate, documented inversion of an existing
invariant.** `BattleSimulation.cs:2580-2582` states that combat reads nothing
from movement and that movement recovery never suppresses an attack the combat
gates accepted. That invariant survives — combat still reads nothing from
movement, and `ApplyEquipmentAttackFootworkAndDeathCleanup` still never blocks
an attack. What changes is the reverse direction: the movement stage now writes
a field the combat stage owns. `AttackCooldownRemaining` today has exactly one
non-decrement writer, `ResolveComboTransition` at `:3724`. The interrupt is the
second, from a different stage. Both writer sites carry a comment naming the
other, and the write is gated so it is impossible under V1 through V6.

### Question 3 — numeric units, bounds, and the same-tick conflict rule

All three signals are expressed in **basis points**, ten-thousandths of one
whole, which is the unit the existing ratio thresholds on
`LoadoutMovementProfile` already use
(`DisengageEnemyToAllyBasisPoints` = 17,500 on the `KS` row means 1.75 enemies
per ally). The weights are basis points of the weighted sum and are validated
to total exactly 10,000, which makes the sum a true weighted average and makes
the threshold directly comparable to a single signal's value.

The arithmetic is section 4 below. The bounds and the overflow analysis are
section 5.

There is no same-tick conflict to resolve. The interrupt is evaluated once per
living agent per tick, in ascending storage index, and it reads only tick-start
authoritative state plus scratch that `SelectTargetsAndIntents` already
finalised before the loop begins. No agent's interrupt decision reads another
agent's interrupt decision, so the loop order cannot decide an outcome and the
existing "no peeking" invariant that
`GatherEquipmentRelativeMovementProposals` documents at `:1645-1648` is
preserved.

### Question 4 — total ordering and random-stream policy

**No random stream is consulted.** The interrupt is a pure arithmetic predicate
over integers. `SplitMix64` is not touched, no new stream is opened, and the
existing streams' consumption order is unchanged, so a V7 run's RNG sequence is
identical to what the same code would produce with the interrupt disabled.

Total ordering is inherited: the loop at `:1583` walks `_agentStates` in storage
index order, which is the same total order the surrounding stage already uses,
and there are no ties to break because the predicate is per-agent and
independent.

### Question 5 — cache source and invalidation

**No new cache, and no unbounded storage of any kind.**

Two of the three signals need history, and each is stored as a **single integer
on `AgentState`, written exactly once per tick at a pinned stage**, following the
`MovementPaceRaw` precedent at `AgentState.cs:176`: one value, one tick of
history, zeroed on death at `BattleSimulation.cs:2596`, folded into the state
hash under a version gate at `StateHasher.cs:125`.

A per-agent ring buffer holding an N-tick window was considered and rejected. No
N-tick-window precedent exists anywhere in `Hukbo.Core`, a ring buffer's fold
order would have to be defined and frozen alongside the per-agent fold, and the
buffer's head index would itself become hashed state. One tick of history is
sufficient for both signals and costs two integers per agent.

The pressure value shown in the inspector is **derived scratch** — category 3 of
`SIMULATION-GAME-STANDARDS.md` section 6 — held in a per-agent array
`_pressureBasisPoints`, rebuilt every tick from authoritative state inside the
same loop that already computes it, never hashed, never snapshotted, never
persisted. It is the same category as `LocalMovementContext`, whose own remarks
at `LocalMovementContext.cs:8-9` state the rule.

### Question 6 — save, event, and version effect

**Version.** A new preset, `MovementPresetId.EquipmentRelativeFootworkV7 = 7`,
appended after `EquipmentRelativeFootworkV6 = 6`. No existing enum value is
renumbered or reordered. One preset covers every row, per decision D5: the
interrupt lives in shared rules and moves every row's trajectory digest whether
or not that row's scalars are touched, so a per-weapon V7/V8/V9 chain would
freeze an unshipped intermediate digest per weapon for no benefit.

**Save.** No effect. There is no save or resume path in this repository:
`BattleSnapshot` is an outbound record with no deserializer, and save/resume
equivalence sits in Gate 3, which has not been reached.

**Events.** No new event kind, and no new event. Section 8 below defends that
choice against the alternative.

**Golden expectations.** V7 gets its own `MovementRuleset.ContentHash` pin in
`MovementPresetRegistryTests` and its own trajectory digest fixture and freeze
test, both captured once from the built code after tuning lands. V1 through V6
keep every pinned literal and every fixture byte-identical; section 6 is the
whole argument for how.

### Question 7 — worst-case complexity and benchmark workload

The interrupt adds `O(1)` work per living agent per tick to a stage that already
walks every agent once. It adds no scan, no query, and no allocation: the two
scratch arrays are sized once in the constructor alongside the arrays already
there, and the predicate operates entirely on locals. Total per-agent cost is
three `long` multiplications, three `long` divisions, three more multiplications
for the weighting, two additions, and one comparison.

The benchmark workload is section 2.2's: five seeds at two hundred and at five
hundred agents under combat preset `PrecolonialPhilippinesV2`, one discarded
warm run per cell, median `p50Milliseconds` against `PersistentContingentsV4`,
ceilings 2.0× and 2.5×.

### Question 8 — the spectator explanation

This is the question that made the feature necessary, so it gets the longest
answer.

**What exists today.** Footwork has exactly one spectator channel: a text row in
`AgentInspectorContent.FormatFootworkLine`
(`src/Hukbo.Client/UI/AgentInspectorContent.cs:410-441`). Nothing in the
renderer draws a phase. `docs/development/testing.md` contains no footwork,
posture, or pace smoke rows at all.

**Why an event alone cannot carry it.** The battle event feed retains at most
200 ordered events (`src/Hukbo.Client/ArenaGame.cs:50`,
`EventHistoryCapacity = 200`) against up to one `Move` event per living moving
agent per tick (`BattleSimulation.cs:3209-3241`). At two hundred agents the feed
holds roughly one tick of history. An interrupt event emitted into that feed
would be evicted before a spectator could read it, and emitting one per firing
would make the eviction worse for every other event kind. **No event is added.**

**The three channels that are added**, following the leader-marker precedent —
an authoritative field, projected onto `AgentView`, rendered at the pawn, named
in the inspector, and covered by smoke rows including a legacy-preset regression
row:

1. **A pawn-level break-off mark.** A new authoritative
   `AgentState.BrokeOffUnderPressure` boolean drives a mark drawn above the pawn,
   wired through a new trailing `AgentView.BrokeOffUnderPressure` member and the
   projection at `BattleSimulation.UpdateViews` (`:3973-3984`, via
   `agent.ToView(isLeader)`), exactly as `IsLeader` is wired to
   `ArenaGame.Rendering.cs:973` and `PawnRenderer`. The flag is **not** a
   single-tick pulse: it is set on the tick the interrupt fires and stays set for
   as long as the warrior remains in the `FootworkPhase.Disengage` the interrupt
   produced, clearing the moment the finalised phase is anything else and on
   death. That gives a mark that persists for a readable number of ticks, which
   is what makes it visible at 1× speed, and it is the same shape as the leader
   marker rather than a new one.
2. **An inspector footwork suffix.** `FormatFootworkLine` gains a suffix so a
   pressure-driven disengagement reads differently from an ordinary one — the
   difference between a warrior that hit its ratio threshold and one that was
   broken off mid-commitment. `FootworkPhase.None` still returns `null`, so
   legacy inspector output stays byte-identical.
3. **An inspector pressure row.** A new row shows the current weighted pressure
   against this warrior's own threshold, in the same basis-point unit, every
   tick — not only on the tick the interrupt fires. This is the channel that
   actually satisfies the question, because it lets a spectator *predict* the
   break-off and understand why one warrior broke and the neighbour beside it did
   not. It renders `null` under every preset that does not apply the interrupt,
   so legacy output is again byte-identical.

**Smoke rows.** `docs/development/testing.md` gains a section modelled on the
existing "Leader marker and inspector annotation smoke" block at lines 4487
through 4507, including a row equivalent to L-7: launch under a legacy preset and
confirm no warrior ever shows the mark and no inspector line ever carries the
pressure row. Every row lands `PENDING`; no agent may flip one.

### Question 9 — tests that fail before and pass after

- Unit tests on the new pure predicate: threshold equality, each signal alone
  below and above the bar, the combination that fires only in the sum, saturation
  at the signal ceiling, a zero prior-ally count, and the transition-only rule.
  These cannot compile before the predicate exists.
- A ladder test asserting that a warrior with prior phase `Commit` and a
  non-expired timer resolves to `Disengage` when the interrupt fires and to
  `Commit` when it does not. This fails today because step 2 at
  `WeaponMovementRules.cs:217` returns unconditionally.
- A simulation test asserting that an interrupted warrior's
  `AttackCooldownRemaining` equals its `AttackCooldownTicks` at the end of the
  interrupting tick, and that it lands no blow that tick.
- A combo-chain test under a footwork preset asserting the chain is cleared by an
  interrupt. This is the coverage gap named in section 7 below.
- Byte-layout tests: V1 through V6 state hashes and `ContentHash` literals
  unmoved, and V7 folding exactly the new fields.
- The V7 trajectory digest freeze test.
- A logging-neutrality run: the seed-1 headless workload under V7 with logging
  off and at `trc`, requiring identical state hash, event hash, outcome, and
  event stream.

## 4. The interrupt itself

### 4.1 Where it sits in the ladder

The interrupt must preempt `FootworkPhase.Commit`, which step 2 returns for
unconditionally at `WeaponMovementRules.cs:217-222`. It therefore sits **above
line 215**, immediately after the argument validation at lines 212 and 213 and
immediately before step 2's comment.

That position is forced rather than chosen. It must be below the dead check at
lines 207 through 210, because a dead agent resolves to `(None, 0)` and reads no
counts. It must be below the validation at lines 212 and 213, because the
predicate divides by `supportAllies` and that validation is what guarantees
`supportAllies >= 1`. And it must be above line 217, because everything from
there down is unreachable for a committed warrior.

The existing steps keep their numbers. The new branch is documented as **step
1a** rather than renumbering ten steps and every comment, test name, and design
reference that cites them.

The branch returns `(FootworkPhase.Disengage, 0)` — a zero timer, matching every
other `Disengage` return in the ladder at lines 241, 248, and 254. The finalised
phase then goes through `FinalizeFootwork` exactly as any other provisional
phase does, so lane clearance can still fall it back, and nothing downstream
learns that this particular `Disengage` arrived by a different route.

### 4.2 How a function with no ruleset argument gets version-gated

`ResolveProvisionalFootwork` is a static pure function taking eleven scalar
parameters and no ruleset. It has one production call site
(`BattleSimulation.cs:1625`) and six test call sites:

| File | Line |
| --- | --- |
| `tests/Hukbo.Core.Tests/Movement/FootworkPhaseRulesTests.cs` | 39 |
| `tests/Hukbo.Core.Tests/Movement/ItakMovementTransitionTests.cs` | 54 |
| `tests/Hukbo.Core.Tests/Movement/KalisMovementTransitionTests.cs` | 711 |
| `tests/Hukbo.Core.Tests/Movement/KampilanMovementTests.cs` | 1252 |
| `tests/Hukbo.Core.Tests/Movement/TallHardwoodMovementTests.cs` | 2236 |
| `tests/Hukbo.Core.Tests/Movement/WasayMovementTests.cs` | 758 |

Every one of the six sits inside a private test helper whose own parameters
carry defaults, so a **trailing parameter with a default** is the only shape
that leaves all six compiling unchanged.

**The gate is one trailing `bool pressureInterruptFired = false`.** The default
`false` is the legacy ladder, exactly and by construction, for V1 through V6 and
for every existing test. The production call site passes the value the
simulation computed for this agent this tick.

The predicate itself is a **separate pure function**,
`WeaponMovementRules.ShouldPressureInterrupt`, so that it is unit-testable in
isolation and so that the value is computed exactly once per agent per tick. The
simulation calls it, keeps the answer in the scratch it already keeps the
provisional phase and timer in, passes it into the ladder, and uses the same
answer to charge the cost. One computation, one authority, no duplicated
formula.

Two alternatives were rejected. Widening the return tuple to
`(FootworkPhase, int, bool)` would break the declared return type of all six
test helpers. Adding an `out bool` parameter would break all six call sites,
because an `out` argument cannot be defaulted. Inferring the interrupt from the
outputs — "prior phase was `Commit` or `Recover` and the result is `Disengage`"
— is not sound: an *expiring* `Recover` falls through step 3 and can legitimately
reach `Disengage` through step 5 or step 6 without any interrupt, and charging
that warrior a cooldown would be a silent behavioural change to a path the
weapon sessions already tested.

### 4.3 The transition-only rule, and why it is load-bearing

`ShouldPressureInterrupt` returns `false` unless the prior phase is
`FootworkPhase.Commit` or `FootworkPhase.Recover`.

Without that clause the interrupt fires on every tick that the pressure holds,
including every tick the warrior is already disengaging, and re-charges the
cooldown each time. A warrior under sustained pressure would then never attack
again, which is a worse standoff than the one V7 exists to fix. With the clause,
the cost is charged once per break-off; the warrior's subsequent stay in
`Disengage` is governed by the existing hysteresis at steps 4 and 5, whose
release threshold is validated strictly below its entry threshold so no count can
enter and leave on the same tick.

The clause also keeps the ladder's meaning intact: the interrupt exists to
preempt the attack lifecycle, and outside the attack lifecycle there is nothing
to preempt.

### 4.4 The cost, and what happens to the combo chain

On the tick the interrupt fires, at stage 4 of the tick pipeline:

```
agent.AttackCooldownRemaining = agent.AttackCooldownTicks;
agent.ComboStepsRemaining = 0;
agent.ComboTargetEntityId = null;
```

`AttackCooldownTicks` rather than the weapon profile's value, matching the
reasoning `ResolveComboTransition` already records at
`BattleSimulation.cs:3591-3604`: the two are bit-identical for every agent
`CreateAgent` produces, and the cached field is the one every other stage reads.

The timing is exact because `DecrementCooldowns` runs at `:581`, before this
stage at `:589`. A value written here is not decremented on the tick it is
written, the first decrement lands at the start of the next tick, and the attack
gate at `:3398` therefore opens again exactly `AttackCooldownTicks` ticks later.
On the interrupting tick itself, `GatherAndCommitAttacks` at `:605` sees a
non-zero cooldown and the warrior lands nothing. That is the cost, and it is
observable.

**The chain is cleared, not preserved.** This is a decision, not an oversight.
The chain's contract is that a continuing chain earns the shorter
`ComboCooldownTicks` and a chain-position value on its event. A warrior whose
cooldown was just reset to the full normal value is by definition not
continuing a chain, and leaving `ComboStepsRemaining` above zero would let the
next blow claim a chain position across an interruption — an event field
reporting a continuity that did not happen. Clearing uses the same two writes
`ClearActiveComboChain` at `:890-899` performs, and the interrupt site says so in
a comment.

This is precisely the contract erosion the brief warns about, and it is the
reason the plan carries a task adding combo-chain coverage under a footwork
preset. Section 7 covers why the existing coverage cannot catch it.

### 4.5 The three signals

**Signal A — support pressure.** The enemy-to-ally ratio in the support ring, in
basis points:

```
A = min(SignalCeiling, (long)supportEnemies * 10_000 / supportAllies)
```

Sourced from `LocalMovementContext.SupportEnemies` and `.SupportAllies`, both
already derived this tick and both already parameters of
`ResolveProvisionalFootwork`. `supportAllies` includes the actor and is
validated at least 1 at line 212, so the divisor is never zero. **No new
storage.**

**Signal B — incoming damage.** Damage taken on the previous tick as a fraction
of maximum hit points, in basis points:

```
B = min(SignalCeiling, (long)damageTakenLastTick * 10_000 / maximumHitPoints)
```

Stored in a new `AgentState.DamageTakenLastTick`. `_damageTotals` already exists
as a per-agent, per-tick accumulator (`BattleSimulation.cs:29`, cleared at
`:3335`, accumulated at `:3454-3455`, applied at `:3535-3552`), and it is still
populated when `ApplyEquipmentAttackFootworkAndDeathCleanup` runs at `:611`. The
new field is stamped from it there, once per tick, at a pinned stage — no new
query, no new pass. `Scenario.Validate` already proves
`AgentsPerFaction * worstCaseDamage` fits in `int`
(`src/Hukbo.Core/Simulation/Scenario.cs:322`), so the accumulator cannot
overflow its own type. `MaximumHitPoints` is validated to at least 1
(`Scenario.cs:233-237`), so the divisor is never zero.

**Signal C — ally collapse.** The fraction of the support ring's allies lost
since the previous tick, in basis points:

```
lost = max(0, priorSupportAllies - supportAllies)
C    = priorSupportAllies == 0 ? 0 : (long)lost * 10_000 / priorSupportAllies
```

Stored in a new `AgentState.PriorSupportAllies`, stamped in the same pass at
`:611` from `_localMovementContexts[index].SupportAllies`. `C` is naturally at
most 10,000 because `lost` cannot exceed `priorSupportAllies`, so it needs no
ceiling. At spawn the field is 0, so on tick 1 the signal is 0 and cannot fire
spuriously.

The stamp is placed after the footwork stage reads the previous value, which is
what makes a single integer sufficient: the read at stage 4 sees tick *N−1*'s
count, and the write at stage 7 replaces it with tick *N*'s.

**Clearing on death.** All three new fields — `DamageTakenLastTick`,
`PriorSupportAllies`, and `BrokeOffUnderPressure` — are cleared in the dead-agent
branch at `BattleSimulation.cs:2594-2601`, alongside the four fields that branch
already clears. The writes are idempotent for an agent that died on an earlier
tick, exactly as the existing four are.

### 4.6 The weights and the threshold

**The weights are shared across all six rows** and live on `MovementRuleset`:
`SupportPressureWeightBasisPoints`, `IncomingDamageWeightBasisPoints`, and
`AllyCollapseWeightBasisPoints`. They are validated to sum to exactly 10,000
when the preset applies the interrupt, and to be zero when it does not.

The brief records shared weights as **an assumption, not a decision** — stated by
the user and not contradicted, but never explicitly confirmed. **This design
adopts the assumption and flags it as requiring confirmation before
implementation begins.** The reason to adopt it is that per-row weights would
introduce eighteen provisional values across six rows instead of three, and the
pattern of shipping unsigned-off provisional values is exactly what decision D4
had to retroactively ratify. The reason to flag it is that it is genuinely
unconfirmed, and a later decision to make the weights per-row would move the
V7 content hash and require a fresh digest.

**The threshold is per row**, per the brief. It lives on
`LoadoutMovementProfile` as `PressureInterruptThresholdBasisPoints`.

The alternative was a six-cell `ImmutableArray<int>` on `MovementRuleset`,
indexed in canonical `KP, WA, KA, IT, KS, IS` order. It is cheaper: it avoids
the seventeenth constructor parameter, the ten `LoadoutMovementProfile`
construction sites, `MovementProfileRegistrationTests.AssertRow`'s sixteen
positional parameters and its six call sites, and the `scalarIndex` loop literal
at `MovementProfileRegistrationTests.cs:141`. It is rejected because it creates a
second structure carrying the same canonical-order invariant, with its own
validation and its own way for a future weapon row to be forgotten, while
`MovementRuleset.ResolveLoadoutProfile` already exists as the single, validated,
rank-independent lookup for exactly this kind of value. Cohesion wins; the cost
is mechanical, it is confined to construction sites and two test helpers, and the
plan isolates it in its own task so nothing else waits on it.

**Every threshold and weight value is a provisional reconstruction of gameplay
tuning under `CLAUDE.md` section 7. None of them is a historical measurement,
and none of them is presented as one.** No source describes how a warrior in the
pre-colonial Philippines decided to break off a committed blow, and this design
makes no such claim. The values are chosen to make a game terminate.

> **Annotation, 2026-08-06 (task F2).** The provisional-reconstruction labelling
> above is correct and is honoured by the shipped values. The closing clause is
> not: the values are chosen to make a game terminate, and no choice of them
> does. See the annotation under Question 1 and section 5 of
> `docs/archives/2026-08-06/movement/2026-07-31-movement-v7-calibration-record.md`.

## 5. The arithmetic, in basis points, with overflow analysis

### 5.1 The predicate

```
if (priorPhase is not (Commit or Recover))      return false;
if (thresholdBasisPoints <= 0)                  return false;

long a = Math.Min(SignalCeilingBasisPoints,
                  checked((long)supportEnemies * 10_000) / supportAllies);

long b = Math.Min(SignalCeilingBasisPoints,
                  checked((long)damageTakenLastTick * 10_000) / maximumHitPoints);

long lost = Math.Max(0, priorSupportAllies - supportAllies);
long c    = priorSupportAllies == 0
          ? 0
          : checked(lost * 10_000) / priorSupportAllies;

long weighted = checked(
      (a * supportPressureWeightBasisPoints)
    + (b * incomingDamageWeightBasisPoints)
    + (c * allyCollapseWeightBasisPoints));

return weighted >= checked((long)thresholdBasisPoints * 10_000);
```

Every operation is on `long`, every multiplication is `checked`, and no
floating-point value appears anywhere. `>=` rather than `>` is deliberate: entry
equality enters, matching step 5's entry rule at `WeaponMovementRules.cs:245`
and the exactness convention the class already documents at lines 131 through
146.

`SignalCeilingBasisPoints` is a single shared constant on `WeaponMovementRules`,
provisionally 30,000 — three whole units. Its purpose is to stop one saturated
signal from carrying the sum on its own: without it, a warrior facing forty
enemies alone contributes a signal-A value of 400,000 basis points and the other
two weights become decorative. Like every other number here it is a provisional
gameplay-tuning value.

### 5.2 Overflow, under `checked`

`long.MaxValue` is 9,223,372,036,854,775,807, roughly 9.22 × 10^18.

| Quantity | Source of the bound | Worst case |
| --- | --- | --- |
| `supportEnemies` | at most `AgentsPerFaction`, capped at `Scenario.MaximumAgentsPerFaction` (`Scenario.cs:19`) | 10,000 |
| `supportAllies` | same cap, at least 1 (`WeaponMovementRules.cs:212`) | 1 to 10,000 |
| `priorSupportAllies` | same cap | 0 to 10,000 |
| `damageTakenLastTick` | `Scenario.Validate` proves `AgentsPerFaction * worstCaseDamage <= int.MaxValue` (`Scenario.cs:322`) | 2,147,483,647 |
| `maximumHitPoints` | `ValidateInRange(1, MaximumCombatValue)` (`Scenario.cs:233-237`) | 1 to 1,000,000 |
| weights | validated to total 10,000 | 0 to 10,000 |
| `thresholdBasisPoints` | validated in `[1, SignalCeilingBasisPoints]` when the preset applies the interrupt | 30,000 |

Largest intermediate values:

| Expression | Worst case | Headroom against `long.MaxValue` |
| --- | --- | --- |
| `supportEnemies * 10_000` | 1.0 × 10^8 | 9.2 × 10^10 × |
| `damageTakenLastTick * 10_000` | 2.15 × 10^13 | 4.3 × 10^5 × |
| `lost * 10_000` | 1.0 × 10^8 | 9.2 × 10^10 × |
| `a * weight`, `b * weight`, `c * weight` | 3.0 × 10^8 each | — |
| `weighted` (sum of three) | 9.0 × 10^8 | 1.0 × 10^10 × |
| `threshold * 10_000` | 3.0 × 10^8 | 3.1 × 10^10 × |

The tightest margin is the incoming-damage numerator, at four hundred thirty
thousand times headroom, and it is only that tight because `MaximumHitPoints`
admits values up to one million while `DamagePerAttack` is separately capped at
the same value. Every other term has ten orders of magnitude to spare. There is
no reachable overflow, and `checked` is present so that an unreachable one throws
rather than wrapping silently.

### 5.3 The division, and a comment that must be corrected

The predicate performs three integer divisions on `long`. They truncate toward
zero, which is deterministic, exact on every platform, and identical to what
`FixedPoint.MultiplyRatio` already does at
`src/Hukbo.Core/Mathematics/FixedPoint.cs:37` inside hashed code paths, and to
`MovementRules.CeilDiv` at `src/Hukbo.Core/Movement/MovementRules.cs:373-375`.
No floating-point value is produced or consumed.

Division cannot be avoided here. The existing ratio steps avoid it because they
compare a single ratio against a single threshold, which cross-multiplies into
one product on each side. A weighted **sum** of three ratios with three different
denominators — `supportAllies`, `maximumHitPoints`, and `priorSupportAllies` —
has no such form: putting it over a common denominator produces a five-factor
product whose worst case is roughly 1 × 10^25, which overflows `long` and would
force `Int128` arithmetic per agent per tick, in a stage already under
performance scrutiny.

**`src/Hukbo.Core/Movement/WeaponMovementRules.cs:16` currently asserts that
"nothing here divides".** That sentence becomes false the moment this predicate
lands, and it must be corrected in the same change. It is a fourth stale
assertion beyond the three the brief already identified, and the plan carries it
as an explicit deliverable rather than leaving it to be noticed in review.

## 6. Keeping V6 byte-identical

### 6.1 The trap

`MovementRuleset.ContentHash` **does** reach the state hash under V6.
`BattleSimulation.ComputeStateHash` passes it at `:654-656`, and
`StateHasher.Compute` folds it at `:81-84`. The per-agent side is the same story:
`StateHasher.cs:122` gates the five footwork fields on
`movementContentHash is not null`, a condition V7 also satisfies.

So a new field folded unconditionally into `MovementRuleset.ComputeContentHash`
or `LoadoutMovementProfile`'s per-row fold would move V6's `ContentHash`, which
would move V6's state hash from tick 1, which would break both the pinned literal
`0x0FFE5D202B324D25UL`
(`tests/Hukbo.Core.Tests/MovementPresetRegistryTests.cs:79`) and the frozen
trajectory digest
(`tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-movement-v6-digest.json`).
The brief forbids editing either.

A new per-agent field folded unconditionally in `StateHasher` would do the same
thing one layer down.

### 6.2 The gate, named concretely

**The gate is `MovementRuleset.AppliesPressureInterrupt`** — a new boolean
following the exact precedent of the three version-gated booleans already on the
type: `NarrowsCohesionScanToCohesionCapableContingents` (`MovementRuleset.cs:180`,
registered `false` through V3), `SelectsLeaderByRank` (`:193`, registered `false`
through V4), and `UsesEquipmentRelativeFootwork` (`:205`, registered `false`
through V5). It is registered `false` on V1 through **V6** and `true` on V7
alone.

It is deliberately **not** `UsesEquipmentRelativeFootwork`, which V7 also sets.
That flag is what V6 already turns on, so gating on it would move V6.

Three conditional folds, each naming the same gate:

1. **The three weights**, in `MovementRuleset.ComputeContentHash` (currently
   `:376-436`), folded inside `if (AppliesPressureInterrupt)` placed after the
   existing `UsesEquipmentRelativeFootwork` fold at `:394` and before the radii
   at `:395-396`. Under V6 the flag is `false`, nothing is written, and the byte
   sequence is exactly what it is today.
2. **The per-row threshold**, folded inside the existing
   `foreach (var profile in LoadoutMovementProfiles)` loop at `:398-433`, again
   inside `if (AppliesPressureInterrupt)`, after
   `PursuitSupportBodyDiametersBasisPoints` at `:430-432`. The gate is available
   because the fold runs on the ruleset, which knows the flag, even though the
   value being folded lives on the row, which does not.
3. **The three new per-agent fields**, in `StateHasher.Compute`, in a **new**
   conditional block placed after the existing `movementContentHash is not null`
   block at `:122-129`, gated on a new trailing parameter
   `bool appliesPressureInterrupt = false`. The existing block is not touched, so
   V6's per-agent byte layout is unchanged; V7 appends
   `DamageTakenLastTick`, `PriorSupportAllies`, and `BrokeOffUnderPressure` as
   1 or 0, in `AgentState` declaration order. This is the same idiom the type
   already uses twice — `hasRankLevels` at `:117-120` and
   `movementContentHash` at `:122-129` — and its own remarks at `:22-31` state the
   reasoning verbatim.

`BattleSimulation.ComputeStateHash` at `:642-656` passes
`_movementRules.AppliesPressureInterrupt` as the new argument, alongside the
`_movementRules.UsesEquipmentRelativeFootwork ? ... : null` it already passes.

The three new `AgentState` properties are declared **after**
`FootworkTicksRemaining`, because `AgentState.cs:163-167` records that the five
properties from `Facing` to `FootworkTicksRemaining` are declared in the V6 fold
order and frozen once the V6 digest ships.

### 6.3 Construction-time coupling

`MovementRuleset`'s existing validator,
`ValidateEquipmentRelativeFootworkCoupling` (`:297-374`), already enforces that a
preset without equipment-relative footwork carries zero radii and no profile
rows. A parallel clause is added for the interrupt:

- When `AppliesPressureInterrupt` is `false`: all three weights must be zero, and
  every profile row's `PressureInterruptThresholdBasisPoints` must be zero.
- When it is `true`: the three weights must each be non-negative and must total
  exactly 10,000, and every profile row's threshold must lie in
  `[1, SignalCeilingBasisPoints]`.
- `AppliesPressureInterrupt` may be `true` only when
  `UsesEquipmentRelativeFootwork` is also `true`, because the interrupt is
  evaluated inside a stage that only the latter flag runs.

`LoadoutMovementProfile`'s own constructor validates the new parameter as
non-negative only. Zero means "no threshold registered", which is what every V6
row carries and what keeps those rows' folded values identical to today's.

V7's six rows are built from V6's by a new immutable
`LoadoutMovementProfile.WithPressureInterruptThreshold(int)` instance method
returning a new instance — never mutating the source — so the V7 registry entry
does not duplicate sixteen scalars per row and cannot drift from V6's tuning
before the tuning task deliberately moves it.

## 7. The combo-chain coverage gap

`ComboChainTests` cannot catch the contract erosion this feature introduces. Its
fixtures run under `PersistentContingentsV4`, where
`UsesEquipmentRelativeFootwork` is `false`, every agent's `FootworkPhase` is
`FootworkPhase.None`, and the footwork stage at `BattleSimulation.cs:589` never
runs at all. Under that preset the interrupt is unreachable by construction, so
every existing combo test would keep passing no matter what the interrupt did to
`AttackCooldownRemaining`, `ComboStepsRemaining`, or `ComboTargetEntityId`.

New coverage is required, running under `EquipmentRelativeFootworkV7`, asserting
at minimum:

- An interrupted warrior's `ComboStepsRemaining` is 0 and its
  `ComboTargetEntityId` is `null` at the end of the interrupting tick.
- Its `AttackCooldownRemaining` equals its `AttackCooldownTicks`, not
  `ComboCooldownTicks`.
- The next blow it lands carries no chain position, because the chain it was in
  did not survive.
- Under `EquipmentRelativeFootworkV6` — same rosters, same seed, interrupt flag
  `false` — none of the above happens and the chain behaves exactly as it does
  under V4.

That last assertion is the regression test for the version gate itself.

## 8. The three exclusivity tests, and how they are amended

Three tests in `tests/Hukbo.Core.Tests/MovementPresetRegistryTests.cs` assert one
flag each across an explicit, exhaustive list of every registered preset:

| Test | Line | Flag |
| --- | --- | --- |
| `OnlyPersistentContingentsV4NarrowsTheCrossContingentScan` | 244 | `NarrowsCohesionScanToCohesionCapableContingents` |
| `OnlyPersistentContingentsV5SelectsLeaderByRank` | 276 | `SelectsLeaderByRank` |
| `OnlyEquipmentRelativeFootworkV6UsesEquipmentRelativeFootwork` | 305 | `UsesEquipmentRelativeFootwork` |

**A correction to the research finding.** These three tests will not *fail* when
V7 is added. They enumerate six named presets and assert nothing about a seventh,
so a new preset is simply invisible to them. That is worse than failing: it is
the exact silent drift each test's own summary says it exists to prevent —
`MovementPresetRegistryTests.cs:236-241` states the purpose as making it
impossible for a later preset to "quietly turn it on for a frozen trajectory
without this Fact failing".

Each of the three therefore gains one assertion, `Assert.True` on V7 in all
three cases, because V7 carries V6's cohesion tunables, V5's rank-aware leader
selection, and V6's equipment-relative footwork forward unchanged. A fourth test
is added in the same shape for `AppliesPressureInterrupt`: `Assert.False` on V1
through V6, `Assert.True` on V7.

The test names are left alone. `OnlyPersistentContingentsV4Narrows...` has been
inaccurate since V5 shipped — the body already asserts `True` for V5 and V6 —
and renaming three tests inside a change this large is churn that hides the real
diff. The naming inaccuracy is recorded here instead.

Two related notes. `BattleSimulationTests:1730-1732`
(`ExactlyOneLivingLeaderPerNonEmptyContingentAcrossEveryRegisteredMovementPreset`)
iterates `Enum.GetValues<MovementPresetId>()` and so picks V7 up automatically,
with no edit and no risk of omission. And
`MovementProfileRegistrationTests.cs:141` loops `scalar < 15` over the folded
per-row scalars of V6; that literal stays at 15, because the sixteenth scalar is
not folded under V6, and the new scalar's fold gets its own V7-specific test
instead.

## 9. Stale assertions that must be corrected in this change

Four comments in the repository currently assert the opposite of what section
6.1 establishes, or of what section 5.3 introduces. Leaving any of them in place
would mislead the next agent into exactly the mistake this design exists to avoid.

| File and lines | What it says | Why it is wrong |
| --- | --- | --- |
| `src/Hukbo.Core/Movement/MovementRuleset.cs:17-32` | `ContentHash` "never reaches the state hash", so adding a field "cannot move any preset's state hash, event hash, outcome, or recorded digest" | False since V6. `BattleSimulation.cs:654-656` passes it and `StateHasher.cs:81-84` folds it |
| `src/Hukbo.Core/Movement/MovementPresetRegistry.cs:18-24` | The same claim, in the V1 entry's summary | Same reason |
| `src/Hukbo.Core/Movement/MovementPresetRegistry.cs:216-217` | "no `BattleSimulation` code path consults the flag yet" | False. `UsesEquipmentRelativeFootwork` is consulted at twelve code sites in `BattleSimulation.cs` alone — `:142`, `:146`, `:297`, `:420`, `:584`, `:593`, `:606`, `:654`, `:922`, `:1461`, `:3183`, `:3337` — plus one documentation reference at `:1556` |
| `src/Hukbo.Core/Movement/WeaponMovementRules.cs:16` | "nothing here divides" | Becomes false when the predicate in section 5.1 lands |

The first three are corrected before any V7 code is written, as a standalone
task, so that the correction is reviewable on its own and so that no later task
is written against a comment that lies. The fourth is corrected in the same task
that introduces the division.

## 10. What this design does not authorize

- Editing the V6 content hash literal, the V6 trajectory digest, or any V1
  through V6 fixture.
- Moving `Scenario.MovementPreset` off `PersistentContingentsV4`. Decision D6
  stands: the default moves only after the section 2.1 termination bar passes, on
  evidence, in a separate decision.
- Any performance work on `ResolveCollisions`. Section 2.2 flags it; flagging is
  not authorization.
- Rewriting the proposed disengagement band in
  `docs/research/movement/tall-hardwood-shield.md`. It is annotated with the
  decision D4 ratification note, never overwritten.
- Flipping any manual smoke-checklist row in `docs/development/testing.md`. Every
  new row lands `PENDING` and stays there until a human at an interactive desktop
  runs it.
- Per-row weights. The shared-weight assumption in section 4.6 is adopted and
  flagged; changing it is a new decision, not an implementation detail.

## 11. Open questions this design records rather than answers

1. ~~**Shared weights are an assumption.** Section 4.6. Confirm before
   implementation begins.~~ **Resolved 2026-07-31: the user confirmed three
   shared weights.** Section 4.6's adopted assumption is now a decision, and
   the flag it carries there is discharged. Making the weights per-row later
   would move the V7 content hash and force a fresh trajectory digest, so this
   is settled before phase 1 rather than after phase 5.
2. **The shielded Kalis `Refuse` problem.** The Wasay session's 8v8 fixture found
   the shielded Kalis row spending 162 of 400 ticks in `FootworkPhase.Refuse` and
   not reaching its first `Commit` until tick 259. Whether the pressure interrupt
   improves, worsens, or does not touch that is unknown, and the tuning task
   should measure it rather than assume.
3. ~~**Whether the interrupt is enough on its own.** If V7 with the interrupt and
   tuned thresholds still fails the section 2.1 termination bar, the remaining
   cause is elsewhere and this design does not predict where.~~
   **Answered 2026-08-06, negatively (tasks E1 and F2). The interrupt is not
   enough on its own.** V7 with tuned thresholds fails the section 2.1
   termination bar in all ten cells, at the shipped values and at every one of
   the six candidate tunings task E1 measured. The decisive measurement is task
   E1's candidate 2: the minimum legal threshold on every row makes the
   predicate fire on every agent-tick it is *capable* of firing on, and every
   cell still drew. Across the six candidates the firing count ranged over a
   factor of 4.6 and no cell's terminal tick moved by a single tick.
   The remaining cause is upstream, as this question anticipated, and is now
   located if not diagnosed: the standoff is a refusal to enter the attack
   lifecycle at all, holding `FootworkPhase.Refuse` and the regroup posture for
   roughly 349 ticks out of every 350, which leaves the interrupt an addressable
   population of about 0.3% of agent-ticks. Which rule is responsible — the
   refuse conditions, the regroup cycle, the cohesion duty window, or the
   approach-sidestep rules — is not determined by anything measured here and
   needs its own design document.
   **Do not reopen this by tuning weights or thresholds. That search is
   finished.**
