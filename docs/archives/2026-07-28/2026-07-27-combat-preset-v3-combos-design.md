# Combat Preset V3 — Attack Combinations — Design

> **Archived: reference only.** This plan is complete and deprecated. Do not execute it, and do not treat its steps, versions, or tooling references as current. The live contract is `CLAUDE.md` plus the skills in `.claude/skills/`.

Date: 2026-07-27

Status: design only. This document does not authorize implementation. It depends
on `docs/plans/2026-07-27-weapon-identity-and-attributes-design.md` (preset V2)
being implemented first, because V3 is defined as V2 plus combinations minus
shields.

## 1. What this adds

Today an attack is a single event: cooldown expires, one blow lands, cooldown
resets. Every exchange in a battle has the same shape, and the only variation a
spectator sees is which body part was hit and for how much.

V3 introduces attack combinations. A blow that lands may open a chain, each
subsequent blow in the chain may continue it, and the chain has a maximum length
governed by the fighter's level. Three separate quantities are therefore being
modelled, and the design keeps them separate on purpose:

1. **The chance to open a combination** — does this landed blow start a chain?
2. **The chance to continue a combination** — does the chain survive to the next
   blow?
3. **How many blows the chain may reach** — the fighter's level, capped by what
   the weapon allows.

## 2. Scope boundary — V3 fields only solo loadouts

V3's roster contains the four solo loadouts from V2 — Kampilan, Wasay, solo
Kalis, solo Itak — and omits the two paired ones.

This is a scenario choice, not a mechanical change. V2 establishes that solo and
paired are two profiles of the same weapon and that both are fieldable roster
entries; V3 simply does not field the paired two. Nothing about shields is
removed, disabled, or regressed. `ShieldId`, the `shieldMultipliers` table,
`CombatRuleset.BuildEffectiveWeightTables`, and every paired profile authored in
V2 remain exactly where V2 left them, still valid, still tested, still reachable
by any scenario that selects them.

The reason for the omission is tuning discipline. Combination probabilities
interact with damage, reach, and cooldown, and every one of those already differs
between a weapon's solo and paired profiles. Tuning combinations against both
profiles at once means eight attribute sets moving against two new probabilities,
and a benchmark that cannot attribute a shift in outcome to any single cause.
Four solo profiles is the smaller, honest experiment.

The shield system is being designed separately, and its brief includes deciding
whether a shield modifies combination behaviour at all. V3 must not pre-empt that
— see section 9.

## 3. Mechanics

### 3.1 The chain

A combination is a sequence of blows by one attacker against one target.

- **Opening.** When an attack is accepted and a blow lands, one roll decides
  whether a combination opens. This roll happens only on a blow that is not
  already part of a chain.
- **Continuing.** When a chain is active and the next blow lands, one roll
  decides whether the chain continues past it.
- **Cadence.** Blows inside a chain use the weapon's `ComboCooldownTicks`
  instead of its normal `AttackCooldownTicks`. This is what makes a combination
  feel like a combination rather than a label on ordinary attacks — the follow-up
  arrives faster than a fresh attack would.
- **Damage.** Every blow in a chain deals the weapon's normal damage. No
  escalation, no multiplier. This is a YAGNI call: escalation is easy to add
  later once the base system is proven, and adding it now would confound the
  tuning of the two probabilities with a third free variable.

### 3.2 What breaks a chain

A chain ends when any of the following happens, checked in this fixed order:

1. The continuation roll fails.
2. The chain has reached its maximum length.
3. The target dies.
4. The target is no longer within the weapon's reach.

The attacker taking damage does **not** break a chain in V3. Interrupt
mechanics are a real design area, but they couple combinations to damage
resolution ordering, and that coupling is exactly the kind of thing that turns
into a determinism bug. Deferred, deliberately.

A broken chain resets the attacker to the weapon's normal cooldown, not the
combination cadence.

### 3.3 Chain length and the level placeholder

The maximum number of blows in a chain is:

```
maxChainLength = min(agent.Level, weapon.ComboMaxSteps)
```

There is no leveling system. `AgentState.Level` is introduced as an authoritative
integer field, hashed, and populated at spawn from a new
`Scenario.PlaceholderFighterLevel` whose default is `1`.

With the placeholder at 1, **every combination is at most one follow-up blow**,
for every weapon. That is the intended starting state and it is a feature of the
design rather than a limitation of it:

- The full mechanism — opening roll, continuation roll, cadence change, chain
  break, event reporting — is exercised end to end at level 1.
- The blast radius on balance is small, because no fighter can chain more than
  twice.
- When a real leveling system arrives, it raises one integer and chains lengthen
  without any weapon, probability, or cadence value being retuned.
- `PlaceholderFighterLevel` being a scenario field means a headless benchmark can
  sweep levels 1 through 5 and measure how chain length actually affects
  outcomes, before any leveling design commits to a curve.

`weapon.ComboMaxSteps` is the ceiling a weapon imposes regardless of level. A
war axe does not become a flurry weapon because its wielder is experienced.

## 4. Attribute table

V3 carries every V2 attribute unchanged and adds four more.

**The four new values belong to the weapon profile, not to the weapon.** V2
established `WeaponProfile` as the record holding damage, reach, and cooldown,
with a solo and a paired instance per one-handed weapon. The combination values
join that record. This costs nothing in V3, where only solo profiles are fielded
and the paired values are authored once and left alone — but it means that when
the shield system decides a shield should shorten chains or lower continuation
chance, the field it needs to change already exists in the right place. Hanging
these four on the weapon instead would force that later work to either restructure
the record or bolt a second lookup path beside the resolver.

The table below therefore gives V3's solo-profile values. Paired-profile
combination values are authored in V2's paired rows as copies of the solo values,
which is the neutral starting point; the shield design owns any decision to
diverge them.

Probabilities are basis points out of 10,000, matching the existing convention in
`PhilippineCombatPreset`.

| Weapon | Dmg | Reach | CD | Open | Continue | Max steps | Combo CD |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Kampilan — Great Blade | 15 | 16 | 7 | 2000 | 3000 | 2 | 4 |
| Wasay — War Axe | 18 | 13 | 8 | 1000 | 2000 | 2 | 5 |
| Kalis — Thrusting Blade | 10 | 12 | 5 | 3500 | 4500 | 4 | 3 |
| Itak — Work Blade | 8 | 10 | 4 | 4500 | 5500 | 5 | 2 |

These are provisional gameplay tuning values, not measurements, and section 6
requires them to be validated by benchmark before they are locked.

The shape is deliberate and inverts the V2 ranking. Heavy weapons commit: a war
axe lands one enormous blow and recovers slowly, and it opens a chain one time in
ten. Light weapons chain: the itak deals the least damage per blow of any weapon
in the game and is the only one that can reach five, given the levels to support
it. Under V2 the light weapons were the shielded ones; under V3 they are the
combination weapons instead. A player who could previously describe the itak only
as "the weak one with a shield" can now describe it as "the one that keeps
going."

Note that with the level placeholder at 1, the `Max steps` column is entirely
inert — every weapon is capped at 2 by the level term. The column exists so the
ceiling is authored now rather than retrofitted, and so a level sweep has
something to reveal.

## 5. Determinism

This is a hash-moving change and must follow the `hukbo-determinism-change`
skill.

### 5.1 New preset version

`CombatPresetId.PrecolonialPhilippinesV3 = 3`, with a new `CombatPresetRegistry`
entry. V1 and V2 stay in place unmodified so their replays remain reproducible.
New golden expectations are recorded for the seed-1 baseline against V3; the V1
and V2 goldens are kept, not edited.

### 5.2 New authoritative state

Both of these enter `StateHasher` and both change the state hash:

- `AgentState.Level` — integer, from `Scenario.PlaceholderFighterLevel`.
- `AgentState.ComboStepsRemaining` — integer, zero when no chain is active.

`ComboStepsRemaining` rather than a step counter plus a maximum: one field, and
the "chain finished" test is a comparison against zero rather than against
another piece of state that could drift out of sync with it. Chain state is
per-attacker; the target identity a chain is bound to is already available as the
attacker's current target and is not duplicated.

### 5.3 Random draws

Two new draws per resolved attack at most, both through
`Hukbo.Core/Determinism/SplitMix64.cs`. `System.Random` remains banned.

Each draw mixes a distinct constant salt so that the opening roll, the
continuation roll, and the existing hit-location roll cannot correlate. The
mixing follows the existing `HitLocationResolver.MixAttack` pattern and adds the
current chain position:

```
MixCombo(seed, tick, sourceEntityId, targetEntityId, weapon, comboStepsRemaining, salt)
```

Including `comboStepsRemaining` is not optional. Without it, two continuation
rolls by the same attacker against the same target on different ticks would
already differ by tick — but two rolls on the same tick, which the design must
not silently assume are impossible, would collide. The salts must be authored as
named constants, not literals at the call site.

### 5.4 Tick-stage order

Combination resolution lives inside the existing attack stage. No new tick stage
is introduced, and the stage order in the tick pipeline does not change. Agents
continue to be iterated in stable `EntityId` order, which is what makes the draw
sequence reproducible.

### 5.5 Event surface

A combination must be visible in the event stream or it is not a real feature.
`BattleEvent` gains a nullable chain-position field, carried on attack events
only, and hashed into the event hash.

`BattleEvent` is a packed struct — see
`docs/plans/2026-07-27-battle-event-allocation-packing.md` — so this field must be
packed alongside the existing nullable `Weapon` and `HitLocation` rather than
appended naively. The per-tick allocation budget applies, and the 200-event feed
retention limit is unchanged.

## 6. Spectator discoverability

`SIMULATION-GAME-STANDARDS.md` section 10 asks whether a spectator can discover
an effect without reading source code. Combinations are at real risk here,
because a fast second blow can read as ordinary attack variance.

- **The event feed names the chain.** An attack that is part of a combination
  formats as `hit #7's neck with Itak — Work Blade for 8 (combo 2)`, so the
  chain is legible as text, in order, with its position.
- **The cadence is visible.** A chained blow arrives on the combination cooldown
  rather than the normal one, which for the itak is two ticks against four. The
  same attacker appearing twice in quick succession against the same target is
  the visual signature.
- **The agent inspector shows the weapon's combination attributes** — opening
  chance, continuation chance, and maximum steps — alongside the V2 attributes,
  which is where a spectator confirms that the itak chains more often than the
  wasay rather than inferring it from a sample of battles.

The one thing a spectator cannot discover from the screen is the fighter's level,
because at the placeholder value every fighter has the same one. The inspector
should show it anyway, so that the field is already surfaced when leveling
becomes real.

## 7. Tuning must be measured, not asserted

The probability values in section 4 are a starting point authored on paper. They
change the throughput of every weapon and they interact with the reach and
cooldown values from V2, which have themselves not yet run in a battle.

Before V3 values are locked, a headless benchmark must report, across a fixed set
of seeds:

- mean time to kill per weapon;
- fraction of blows that were part of a chain, per weapon;
- mean realised chain length, per weapon, swept across
  `PlaceholderFighterLevel` 1 through 5; and
- faction win rate for mirrored and asymmetric rosters.

If the itak's realised throughput exceeds the wasay's, the design intent has
inverted and the probabilities are wrong. That is a tuning outcome, not a
redesign, and the plan document should schedule it as an explicit task with the
benchmark output pasted in as evidence.

## 8. Open questions

1. Does a chain survive the attacker switching targets, or is it strictly bound
   to one target? Section 3.2 assumes bound; the alternative is worth a sentence
   of justification either way in the plan.
2. Should the opening roll happen on a blow that lands, or on an attack that is
   attempted? Landing is assumed. Attempting would make combinations visible even
   in whiffed exchanges, which may read better, but there is currently no miss
   concept to hang it on.
3. Does the combination cadence bypass the collision and movement stages in any
   way that could let a chaining attacker out-pace the formation logic? This
   needs checking against `BuildMovementProposal` before implementation, not
   after.

## 9. Relationship to the shield system

The shield agent's brief is to design shields as a stat-variant layer keyed on
the (weapon, shield) pair rather than as a flat targeting multiplier. That work
will need to state explicitly how a shield interacts with combinations — whether
a shield shortens chains, lowers continuation chance, or is simply orthogonal.

V3 does not decide that, and must not pre-empt it. What V3 owes the shield work
is a clean seam, and V2 already built most of it:

- Combination attributes live on `WeaponProfile`, so a shield that modifies them
  modifies the same record it already modifies for damage, reach, and cooldown.
  There is no second mechanism to learn.
- Every read goes through `ResolveWeaponProfile(WeaponId, ShieldId)`. No call
  site reads a profile field directly, so replacing that function's rule with
  per-shield resolution changes no combat code.
- The paired profiles' combination values are authored as copies of the solo
  values. That is a deliberate neutral default, not an assertion that a shield
  has no effect on chains. The shield design decides whether they diverge, and in
  which direction.
- V3's roster omits paired loadouts but does not delete them, so the shield work
  extends a live, tested configuration rather than resurrecting a dead one.
