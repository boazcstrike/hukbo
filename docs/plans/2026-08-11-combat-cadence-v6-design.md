# Combat cadence V6 — slower blows, heavier blows

**Status:** shipped, merged to `main` at `982bd6f` on 2026-08-11. This document
stays live rather than archived because it is cited by path from
`CombatIdentity.cs`, `PhilippineCombatPresetV6.cs`, `Scenario.cs`, and
`DeterminismTests.cs`. Its plan document is finished and archived at
[`../archives/2026-08-11/2026-08-11-combat-cadence-v6.md`](../archives/2026-08-11/2026-08-11-combat-cadence-v6.md);
that document holds the ordered task list, the twenty-seed measurement, and the
record of what each task actually did.

**Outcome:** all twelve `CL-*` smoke rows `PASS`. CL-1, CL-3, CL-7a, and CL-7b
were re-run by a person after this change and every one of them passed, so the
legibility failures this document was written to answer are closed.

**Date:** 2026-08-11
**Base commit:** `0c3f7f2`
**Origin:** the three `FAIL` rows recorded in the weapon-clash smoke section of
[`docs/development/smoke-checklist.md`](../development/smoke-checklist.md) on
2026-08-11 — CL-1, CL-3, and CL-7.

## 1. The problem, as observed

An interactive run on 2026-08-11 passed eight of the eleven weapon-clash smoke
rows and failed three. All three failures are legibility failures, not logic
failures. Every effect the automated tests prove is present does in fact render;
the observer simply cannot attribute an individual effect to the individual blow
that caused it.

The recorded observations:

- **CL-1** — the five event-feed wordings exist, but too many resolutions arrive
  at once for a reader to match a line to the blow that produced it.
- **CL-3** — clash crosses overlap one another, so a shield block, a parry, and
  a deflection cannot be told apart in flight.
- **CL-7** — a swing does not read clearly as one countable action.

The observer's own diagnosis, recorded in the CL-3 row, is that blows arrive too
often, and that the remedy is a slower attack cadence carrying more damage per
blow. This document accepts that diagnosis, and section 2 shows the arithmetic
that supports it.

## 2. Why the cadence is the cause

The shipped default combat preset is `CombatPresetId.PrecolonialPhilippinesV4`
(`Scenario.CombatPreset`, `Scenario.cs:97`). Its melee cadence, at the 20 Hz
tick rate:

| Weapon and grip | Damage | Cooldown | Attacks per second | Blows to kill at 100 HP |
| --- | --- | --- | --- | --- |
| Kampilan (two-handed) | 15 | 7 ticks | 2.86 | 7 |
| Wasay (two-handed) | 18 | 8 ticks | 2.50 | 6 |
| Kalis (solo) | 11 | 5 ticks | 4.00 | 10 |
| Kalis (shielded) | 10 | 5 ticks | 4.00 | 10 |
| Itak (solo) | 9 | 4 ticks | 5.00 | 12 |
| Itak (shielded) | 8 | 4 ticks | 5.00 | 13 |

An Itak warrior therefore commits a blow every 200 milliseconds. The client's
presentation timeline for that same blow is
`AttackMotionCatalog.Itak.RecoverySeconds`, which is 0.17 seconds
(`AttackMotionCatalog.cs:73`). The recovery of one swing consumes 85 per cent of
the interval before the next swing begins, so consecutive swings very nearly
abut, and a spectator sees a continuous churn rather than a sequence of
countable actions. That is CL-7 at 1x speed, stated in numbers.

The same arithmetic explains CL-1 and CL-3. Every attack, whatever its
resolution, produces exactly one event-feed line, and every non-landed,
non-evaded attack produces exactly one clash cross. The rate at which those two
artefacts are generated is the attack rate. Halving the attack rate halves both.

The battle-scale multiplier is real but is not the lever this design pulls. Two
hundred and fifty warriors a side produce a great many simultaneous exchanges no
matter what one pair does, and reducing the roster is not on the table. What is
on the table is the number of artefacts each engaged pair emits per second.

## 3. The change

Add `CombatPresetId.PrecolonialPhilippinesV6 = 6` and a
`PhilippineCombatPresetV6` that restates `PhilippineCombatPresetV4` exactly,
except for the attack cooldown, the combo cooldown, and the damage of the six
melee loadouts. Flip `Scenario.CombatPreset` to the new preset. V1 through V5
stay registered and unmodified so their replays remain reproducible.

### 3.1 The governing constraint: preserve damage per tick

The retune keeps each loadout's damage-per-tick as close to its V4 value as
integer arithmetic allows. This is deliberate and it is the reason the design is
low-risk:

- Time to kill is unchanged, so the number of pairs engaged at any instant is
  unchanged, so battle duration and the termination behaviour recorded in the
  seed-1 baseline should move very little.
- Blows per kill roughly halves, and attacks per second roughly halves, so the
  event-feed line rate and the clash-cross rate roughly halve. That is the whole
  of the legibility gain, and it is a factor of two — not more. This design does
  not claim CL-1 and CL-3 become effortless; it claims they become twice as
  sparse.
- No relative weapon balance moves. The six loadouts keep their existing
  damage-per-tick ordering with respect to one another.

### 3.2 The proposed table

| Weapon and grip | Damage V4 → V6 | Cooldown V4 → V6 | Damage per tick V4 → V6 | Drift |
| --- | --- | --- | --- | --- |
| Kampilan (two-handed) | 15 → 26 | 7 → 12 | 2.143 → 2.167 | +1.1% |
| Wasay (two-handed) | 18 → 32 | 8 → 14 | 2.250 → 2.286 | +1.6% |
| Kalis (solo) | 11 → 22 | 5 → 10 | 2.200 → 2.200 | 0.0% |
| Kalis (shielded) | 10 → 20 | 5 → 10 | 2.000 → 2.000 | 0.0% |
| Itak (solo) | 9 → 20 | 4 → 9 | 2.250 → 2.222 | −1.2% |
| Itak (shielded) | 8 → 18 | 4 → 9 | 2.000 → 2.000 | 0.0% |

Every drift is within two per cent. Attacks per second fall to 1.67 for the
Kampilan, 1.43 for the Wasay, 2.00 for the Kalis, and 2.22 for the Itak. Blows
to kill at 100 hit points fall to four, four, five, and five respectively.

### 3.3 Combo cooldowns

The combo chain is the other density source, and it is the sharper one: an Itak
chain of up to five steps at two ticks apart delivers five blows in eight ticks,
which is a burst no spectator can decompose. The combo cooldowns scale with the
attack cooldowns:

| Weapon | Combo cooldown V4 → V6 | Chain damage per tick V4 → V6 |
| --- | --- | --- |
| Kampilan | 4 → 7 | 3.75 → 3.71 |
| Wasay | 5 → 9 | 3.60 → 3.56 |
| Kalis (both grips) | 3 → 6 | 3.67 → 3.67 |
| Itak (both grips) | 2 → 5 | 4.50 → 4.00 |

The Itak chain loses eleven per cent of its damage per tick. That is the one
value in this design that is not close to neutral, and it is accepted on
purpose: the five-step two-tick Itak burst is the single least legible thing in
the battle, and stretching it is a goal rather than a side effect. Rounding it
to four ticks instead would have moved the chain eleven per cent the other way.

`comboOpenChanceBasisPoints`, `comboContinueChanceBasisPoints`, and
`comboMaxSteps` are **unchanged**. This design moves timing and damage only. How
often a chain opens, and how long it can run, are a separate question and are
deferred.

### 3.4 What is not in scope

- **The ranged preset.** `PhilippineCombatPresetV5` adds the three ranged
  weapons and is opt-in; V4 remains the shipped default, recorded in
  [`2026-08-09-ranged-units-handoff.md`](2026-08-09-ranged-units-handoff.md)
  section 5. V6 therefore descends from V4, fields the same four melee weapons
  in the same six loadouts, and leaves V5 untouched. A matching retune of the
  ranged preset is a follow-up and is named in section 6 of the plan document,
  not performed here.
- **Hit points.** `Scenario.MaximumHitPoints` stays at 100. Raising damage
  against a fixed pool is the whole mechanism; moving both would cancel out.
- **Roster size and army composition.** Unchanged.
- **The clash resolution table, the armour table, the target-weight profiles,
  and the rank table.** Restated from V4 without modification.

## 4. The part of CL-7 this does not fix

`AttackAnimationSystem.Advance` ages every animation by
`elapsedSeconds * speedMultiplier` (`AttackAnimationSystem.cs:92`). The attack
animation is therefore compressed in real time by the playback speed. At 4x, the
Itak's 0.17-second recovery plays in 0.0425 seconds, which is between two and
three frames at 60 Hz. No change to the simulation cadence can fix that, because
the compression is applied on top of whatever cadence the simulation produces.

CL-7 is thus two defects wearing one row:

- **At 1x**, the swing does not read as one action because there is almost no
  gap between swings. Section 3 fixes this.
- **At 4x**, the swing does not read as one action because it is drawn for two
  or three frames. This needs a presentation-only change — clamping the factor
  that ages attack animations so that it grows more slowly than the playback
  speed — and it is a separate task in the plan document.

Splitting the row is the honest disposition. The plan document splits CL-7 into
CL-7a (1x cadence) and CL-7b (4x compression) so that one can pass while the
other is still open.

## 5. The nine questions

1. **User-visible outcome.** Blows land roughly half as often and hurt roughly
   twice as much. The event feed scrolls at roughly half the rate, clash crosses
   appear at roughly half the rate, and a swing at 1x has visible rest either
   side of it. Battle length is unchanged.
2. **Tick stage and state read/written.** No new stage and no new state. The
   attack stage reads `AgentState.DamagePerAttack` and the weapon profile's
   cooldown exactly as it does today; only the values the preset supplies differ.
3. **Numeric units and bounds, same-tick conflict rule.** Damage is an integer
   in hit points, bounded above by `WeaponProfile`'s existing validation.
   Cooldowns are integer ticks and must be positive, which
   `WeaponProfile.Validate` already enforces (`WeaponProfile.cs:114`). Combo
   cooldown obeys the same rule (`WeaponProfile.cs:126`). Simultaneous-attack
   handling is unchanged.
4. **Total ordering and random-stream policy.** Unchanged. No new query, no new
   ordering, and no new draw from any random stream. The preset supplies
   constants; it does not consume randomness.
5. **Cache source and invalidation.** No cache. `CombatRuleset` is built once
   per scenario from a static table, as V1 through V5 already are.
6. **Save, event, and version effect.** This is a preset version change and it
   moves both hashes. `CombatPresetId.PrecolonialPhilippinesV6 = 6` is appended
   and never renumbered. The seed-1 state hash, event hash, winner, and ordered
   event stream all change, and the recorded baseline in
   [`docs/development/testing.md`](../development/testing.md) must be replaced with a
   freshly measured one rather than edited to match whatever the new run prints
   without inspection. Replays recorded under V1 through V5 continue to
   reproduce, because those presets are untouched.
7. **Worst-case complexity and benchmark workload.** Complexity is unchanged —
   the same code executes with different constants. A halved attack rate means
   strictly fewer attack resolutions and strictly fewer events per battle, so
   the 200-agent, 10,000-tick, seed-1 workload should be no slower. The
   500-agent result is re-reported because section 10 of
   `SIMULATION-GAME-STANDARDS.md` requires it, not because a regression is
   expected.
8. **Spectator explanation.** The agent inspector already prints
   `"{profile.AttackCooldownTicks} tick recovery"`
   (`AgentInspectorContent.cs:675`) and
   `"{profile.ComboCooldownTicks} tick combo cooldown"`
   (`AgentInspectorContent.cs:703`), and the composition panel already shows
   per-weapon damage. A spectator can therefore discover the new cadence by
   clicking a warrior, with no source reading. No new inspector field is needed.
9. **Tests that fail before and pass after.** A registry test asserting V6 is
   registered and resolvable; a preset test pinning all six damage, cooldown,
   and combo-cooldown values; a test asserting `Scenario`'s default combat
   preset is V6; a test asserting the six damage-per-tick ratios stay within two
   per cent of V4's, which is the invariant this design actually rests on; and
   the existing determinism and golden-replay tests, which fail against the old
   baseline and pass against the newly measured one.

## 6. Risks

- **A termination regression.** Damage per tick is held near-constant precisely
  to avoid one, but "near-constant" is not "constant", and the interaction with
  the movement preset's engagement behaviour is not analytically obvious. The
  plan document requires a measured decisive-seed count before the default is
  flipped, not after. If the count regresses, V6 ships registered but opt-in and
  the default stays on V4 — the same disposition the ranged package took for V9.
- **The rounding is arbitrary at the margin.** Choosing 26 over 25 for the
  Kampilan, and 5 over 4 for the Itak combo cooldown, are judgement calls
  recorded in sections 3.2 and 3.3. They are provisional gameplay-tuning values
  under CLAUDE.md section 7 and must be commented as such in the preset, never
  presented as historical measurements.
- **A factor of two may not be enough.** If a re-run of CL-1 and CL-3 still
  returns `FAIL`, the next lever is the combo chain's open chance and maximum
  step count, which section 3.3 explicitly deferred. Do not silently push the
  cadence further in the same change.
