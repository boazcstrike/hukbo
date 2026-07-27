# Weapon Clash, Swing Animation, and Clash Sound — Design

Date: 2026-07-27
Revision: 6. Revision 5 put the seam on `Create`; revision 6 makes the hasher take a content
hash rather than a ruleset, and adds a per-tick state hash to the fixture.
Status: design only. A design document does not authorize implementation.
Plan: [2026-07-27-weapon-clash.md](2026-07-27-weapon-clash.md)
Research: [docs/research/WEAPON_CLASH_1500s.md](../research/WEAPON_CLASH_1500s.md)

## 1. What this proposes

Three related changes that together turn a landed blow from a certainty into an
exchange a spectator can read.

| Part | Layer | Summary |
| --- | --- | --- |
| A. Defensive resolution, "the clash" | `Hukbo.Core` | An accepted attack is now resolved against the defender shield and weapon before damage is applied. Four outcomes: the blow lands, the shield takes it, the defender hard-parries it, or the defender brushes it aside. |
| B. Swing animation | `Hukbo.Client` | A pawn weapon stops being a static line. An attack event drives a four-phase swing whose impact phase reflects the authoritative resolution. |
| C. Clash sound | `Hukbo.Client` plus new audio content | Three new sound slots, one per non-landed resolution, so the spectator hears which defensive surface took the blow. |

Part A is authoritative and moves both hashes. Parts B and C are presentation only
and must move neither hash. That last sentence is not a hope: the plan requires the
Phase 4 workload to reproduce the hash pair recorded at the end of Phase 2 byte for
byte, which is the only evidence that the client work stayed in presentation.

## 2. What the research authorizes and what it does not

The research document is binding on evidence labelling and on the honest distance
between the sources and any number in this design. Three of its findings shape the
design directly.

**The shield is the only defensive channel with sixteenth-century documentary
support.** Section 0 of the research is unambiguous: no source describing Philippine
combat in the period describes a blade-on-blade parry, while evasion and shield cover
are both described. This design therefore gives the shield the largest single
interception share and marks the weapon-clash channel as the more speculative of the
two in code comments and in tests.

**Contact happened, but almost never edge-on-edge.** The European forte-wear
archaeology in section 1.4 is the strongest physical evidence available for the
question, and it says weapon-on-weapon contact was frequent enough to wear out the
defensive part of a blade while leaving the edge undamaged. That is the justification
for having a weapon channel at all, and for splitting it into a rare hard arrest and a
common soft brush.

**A single scalar clash probability is not spectator-discoverable and must not ship.**
Section 5.6 states this directly. The design answers it by making the resolution an
authoritative, named, five-valued outcome that appears in the event stream, in the
event log text, in the sound, and in the animation.

### 2.1 The composition rule is normative

Research round two added a composition rule that revision 2 of this document violated.
It is reproduced here because every number below depends on it.

```
P(landed)         = 1 - P(active defence)
P(active defence) = P(shield intercept) + P(weapon intercept) + P(void)
```

The three channels are **mutually exclusive and jointly exhaustive of the defence**.
They are never summed on top of a separate base clash probability. Any attacker-side
accuracy roll, which this simulation does not have, would be a separate and prior
stage.

Revision 2 read the per-pair table as a free-standing lookup added on top of a 0.20
base, which produced a shielded interception near 0.48 and dropped the void channel
entirely. Carried through consistently that error would have reached 0.58 to 0.60
total non-landed, above the 0.55 the research calls indefensible. The correction is
the single largest change in this revision.

Two consequences follow immediately.

**Void becomes a real outcome.** Revision 2 rejected the void channel as
undiscoverable. It cannot be rejected now, because the acceptance criterion is
measured over shield plus weapon plus void, and omitting roughly ten points of void
would put the model at about 0.23, outside the failure band. Void is therefore a fifth
resolution, `Evaded`, and section 5 argues that it is in fact discoverable.

**Crowding is deleted from the model.** The round-two matrices are stated as mass-melee
values with crowding, awareness, and fatigue discounts already applied, and the
research says in terms not to re-apply the modifiers on top of them. Re-applying a
crowding multiplier would double-count. Everything revision 2 built to support it —
the two-pass split of the attack stage, the per-target attacker-count scratch buffer,
the crowding word in the mixer key, and the staleness test that buffer required — is
removed. That is a simplification driven by evidence, not a retreat: with no
cross-attack input, resolution is trivially independent of attacker order and can
resolve inline in the existing single gather loop.

### 2.2 Numbers adopted, altered, and rejected

Every number in this design is a **Provisional reconstruction** and a gameplay tuning
choice. None is a historical measurement. The research is blunt that **all sixteen
cells of the weapon-intercept matrix have no evidentiary confidence whatsoever** — only
their relative ordering is argued, and weakly — so every table below carries that
statement in its code comment, in the same way the tall-hardwood shield multiplier
already does.

| Research figure | This design | Adopted, altered, or rejected |
| --- | --- | --- |
| Composition rule, section 5.1 | Implemented exactly | **Adopted, normative.** |
| Mass-melee totals: shielded 0.40, shieldless 0.25, section 5.1 | Emerges from the matrices rather than being stored | Adopted. |
| Roster weighted mean 0.33, section 5.2 | Computed at 0.325 from the shipped tables | Adopted. |
| Weapon-intercept matrix, sixteen cells, section 5.3 | Stored directly as basis points | **Adopted verbatim.** The factored defender-base times attacker-commitment model from revision 2 is deleted; a direct matrix is both what the research supplies and fewer moving parts than a factorisation that had to be reverse-engineered to reproduce it. |
| Shield intercept, eight cells spanning 0.22 to 0.27, section 5.3 | Flat 2400 basis points for `TallHardwood`, zero for `None` | **Simplified, as the research explicitly permits.** It removes seven tuning constants and the research states plainly that the spread has no source behind it. |
| Void per loadout: 0.10 / 0.09 / 0.10 / 0.11, section 5.3 | Stored per defender weapon as 1000 / 900 / 1000 / 1100 | Adopted. Keyed on the defender weapon rather than on the loadout pair, because weapon and shield are correlated one-to-one in the shipped roster and one table is cheaper than two. |
| Hard share: base by incoming weapon times multiplier by defending instrument, clamped 0.05 to 0.60, section 5.5 | Implemented as a product with that clamp | **Adopted, replacing the flat 25/75 of revision 2.** The sixfold spread from 0.46 down to 0.08 is the most legible contrast the roster offers, and flattening it discards exactly the thing the research says makes the system discoverable. |
| Hard and soft applies to the weapon channel only, section 5.5 | Implemented; a shield intercept takes no hard or soft split | Adopted. A shield intercept is not a parry. |
| Shield intercept mode split: angled 0.65, flat take 0.35, section 5.5 | Not implemented | **Adopted in principle, deferred in code.** Its only mechanical consequence is shield degradation, which is rejected below, so without it the two modes differ in timbre alone. That is one more outcome value and one more sound slot for no rule change. It is the first thing to add if the shield channel reads as monotonous, and it is recorded here so that a future reader does not think it was overlooked. |
| Limb-interception channel, 0.06 to 0.12, section 5.5 | Not implemented | **Rejected for this change.** The existing hit-location resolver already produces `WeaponArm`, `ShieldArm`, and `Hands` outcomes with substantial weight. |
| Edge degradation, 5 to 15 per cent per hard clash, section 5.5 | Not implemented | **Rejected.** Weapon durability is per-agent authoritative state with no gate authorizing it, and no metallurgical study exists to anchor the number. |
| Section 5.4 modifiers, all eleven | Not implemented | **Rejected as double-counting.** The round-two matrices already contain the crowding, awareness, and fatigue discounts. The reach, frontal-arc, buckler, and elite modifiers additionally have no backing state: verified, `Scenario.AttackRangeRaw` is one scenario-wide value copied into every agent, `AgentState` has no facing field, and the roster has exactly two shield identities. |
| Aggregate sanity bounds: below 0.15 and above 0.55 indefensible, section 5.7 | `MaximumInterceptionBasisPoints` of 5500 as a guard | Adopted as a guard only. The largest cell in the shipped tables reaches 4000, so the clamp is unreachable in practice and is exercised only by a direct unit test. |
| Spear and dagger rows | Deleted upstream | Not applicable. There is no spear and `CLAUDE.md` section 9 forbids starting one. |

### 2.3 Hukbo has no morale model, and that governs the tuning

This subsection is reproduced from research section 5.7 because a future reader will
otherwise re-litigate the tuning without it.

Pre-modern battles were decided by morale collapse and rout, not by attrition to
exhaustion. Winners typically took light casualties; losers took most of theirs during
the rout, after cohesion broke. The killing followed the decision rather than producing
it.

Hukbo has no morale model, and `CLAUDE.md` section 9 correctly defers one. **Hukbo must
therefore reach a decision purely by attrition, through a mechanism that historically
did not decide battles.** The consequence is unavoidable: the interception rate has to
sit below whatever the historical record would suggest, or the simulation produces a
battle shape that never occurred. Not because the history is wrong, but because the
simulation is missing the mechanism that actually ended battles, and attrition has to
do that work in its place.

**That is a design compensation and explicitly not a historical claim. It must never be
laundered back into `WEAPON_CLASH_1500s.md` or `HISTORICAL_1500s_WEAPONS.md` as
evidence about how often people parried.**

The practical effect is that if the two acceptance criteria in section 3.3 conflict,
**termination wins and interception is lowered**, and the resulting figure is labelled
as compensation for the absent morale model rather than presented as a finding.

## 3. The nine questions from `SIMULATION-GAME-STANDARDS.md` section 10

### 3.1 Question one: user-visible outcome

Blows stop always landing. Five outcomes are now possible for an accepted attack.

| Outcome | What the spectator sees and hears |
| --- | --- |
| `Landed` | Damage number, blood, the existing wet-cut cue |
| `ShieldBlocked` | No damage, no blood, a dull wooden thud |
| `Parried` | No damage, no blood, a hard metallic ring |
| `Deflected` | No damage, no blood, a short metallic scrape |
| `Evaded` | No damage, no blood, **no sound at all**, and the weapon swings through empty air without recoiling |

A spectator watching a battle sees fewer damage numbers, a longer engagement, weapons
visibly swinging and recoiling rather than sitting frozen at one angle, and small
bright flashes wherever two weapons meet. The second-order effect is that shielded
warriors become materially more survivable than shieldless ones — the research puts a
shielded loadout at about 0.39 to 0.40 total non-landed against 0.22 to 0.29 for the
shieldless pair — and the research says plainly that this visible gap is the part to
defend hardest, above any absolute value.

### 3.2 Question two: tick stage and state read and written

The clash resolves inside the existing `GatherAndCommitAttacks` stage, which runs
seventh of eight in `BattleSimulation.AdvanceOneTick` at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:188`. No new tick stage is added, and
the stage order recorded in `SIMULATION-GAME-STANDARDS.md` section 13 does not change.

**No structural change to the stage is required.** Revision 2 split the first loop in
two so that a crowding count could be completed before any roll. With crowding deleted
in section 2.1, the resolution depends only on the two loadouts, the two entity
identifiers, the seed, and the tick, so it resolves inline in the existing gather loop
immediately after the hit-location call. The stage keeps its four loops.

| Loop | Reads | Writes |
| --- | --- | --- |
| 1. Gather, existing, extended | `IsAlive`, `TargetEntityId`, `AttackCooldownRemaining`, positions by way of `IsWithinAttackRange`, both `Loadout` values, `Scenario.Seed`, `Tick` | `Intent`, `AttackCooldownRemaining`, the proposal buffer including its new resolution field, and `_damageTotals` **only for a landed attack** |
| 2. Event emission, existing | the proposal buffer | the event list |
| 3. Damage and death, existing and unchanged | `_damageTotals` | `HitPoints`, `Intent`, `TargetEntityId`, the event list |

Hit location continues to be resolved for every accepted attack, including ones that
never land. A non-landed attack hit location is the point the blow was aimed at, and
keeping it means the existing `BattleEvent.Attack` factory invariant — a defined weapon
and a defined hit location on every attack event — survives untouched.

Order independence is now trivial rather than argued: no input to the roll depends on
any other attack in the same tick. The permutation test in section 3.4 still ships,
because the property is worth pinning even when it is easy.

### 3.3 Question three: numeric units, bounds, and acceptance criteria

All clash arithmetic is integer basis points out of 10,000, held as `int` with `long`
intermediates where a product could exceed `int`, in the same style as the existing
shield defence multipliers. No fixed-point and no floating-point value enters the clash
path.

There are **thirty-two** tuning numbers: sixteen weapon-intercept cells, one flat shield
intercept, four void values, four hard-share bases, four hard-share multipliers, two
hard-share clamp bounds, and one interception ceiling.

**Weapon intercept, basis points, defender row against attacker column**

| Defender | `GreatBlade` | `HeavyChopper` | `ThrustingBlade` | `Bolo` |
| --- | ---: | ---: | ---: | ---: |
| `GreatBlade` with no shield | 2200 | 1900 | 1600 | 2000 |
| `HeavyChopper` with no shield | 1500 | 1300 | 1100 | 1400 |
| `ThrustingBlade` with `TallHardwood` | 500 | 400 | 600 | 600 |
| `Bolo` with `TallHardwood` | 400 | 300 | 500 | 500 |

**Shield intercept**: `None` 0, `TallHardwood` 2400, flat across every attacker.

**Void, by defender weapon**: `GreatBlade` 1000, `HeavyChopper` 900, `ThrustingBlade`
1000, `Bolo` 1100.

**Hard-share base, by incoming attacker weapon**: `HeavyChopper` 4000, `GreatBlade`
3300, `Bolo` 1800, `ThrustingBlade` 1200.

**Hard-share multiplier, by defending weapon**: `GreatBlade` 1150, `HeavyChopper` 1050,
`ThrustingBlade` 750, `Bolo` 700.

**Scalars**: hard-share clamp 500 to 6000; `MaximumInterceptionBasisPoints` 5500.

**Both clamps are guard-only and neither binds with shipped values.** The hard-share
product spans `1200 * 700 / 1000 = 840` at the light end to `4000 * 1150 / 1000 = 4600`
at the heavy end, comfortably inside 500 to 6000, and the largest total interception is
4000 against a ceiling of 5500. Every test that exercises either clamp therefore
constructs a synthetic `ClashProfile`. A clamp test written against
`PhilippineCombatPreset` is unwritable, because neither bound is reachable.

The resolution is computed in this exact order, every division truncating toward zero:

1. `shield = ShieldIntercept[defenderShield]`
2. `weapon = WeaponIntercept[defenderWeapon][attackerWeapon]`
3. `void = Void[defenderWeapon]`
4. `total = shield + weapon + void`; if `total` exceeds 5500, all three channels are
   rescaled by `5500 / total` in the same integer form, so their proportions survive
5. `hardShare = clamp(HardBase[attackerWeapon] * HardMultiplier[defenderWeapon] / 1000, 500, 6000)`
6. `hard = weapon * hardShare / 10000` and `soft = weapon - hard`

**Step 6 splits the post-rescale weapon channel, never the pre-rescale one.** This
sentence is normative and the naive oracle is required to cite it. Step 4 may reduce
`weapon`, and if step 6 split the pre-rescale value then `hard + soft` would no longer
equal the `weapon` the interval walk uses, and the five intervals would stop tiling the
roll space. Splitting the rescaled value keeps `hard + soft` exactly equal to `weapon`,
so truncation can neither leak nor invent a basis point. Revision 3 left the order
unstated, which would have let the resolver and its supposedly independent oracle
inherit the same misreading from the same sentence, and the sweep would then have agreed
with itself.

The rescale carries two further obligations. Each of the three channels is rescaled
independently and each division truncates, so the post-rescale total lands at or below
the ceiling with a small unallocated residue that silently becomes additional `Landed`
probability. That is accepted, but it must be asserted rather than assumed: no channel
may go negative, the post-rescale total may never exceed the ceiling, and the channel
proportions must survive the rescale. With the shipped tables the largest total is 4000
against a ceiling of 5500, so **the rescale branch is unreachable in production** and is
exercised only by synthetic profiles.

Worked example, `GreatBlade` with no shield defending against a `HeavyChopper`: shield
is 0, weapon is 1900, void is 1000, total 2900, no rescale. Hard share is
`4000 * 1150 / 1000 = 4600`, inside the clamp, so hard is `1900 * 4600 / 10000 = 874`
and soft is 1026. This is the roster extreme the research names: the heaviest incoming
weapon meeting the only instrument capable of arresting it, and the pairing where a
clash is both most frequent and most costly.

Opposite extreme, `Bolo` with `TallHardwood` defending against a `ThrustingBlade`:
shield 2400, weapon 500, void 1100, total 4000. Hard share is
`1200 * 700 / 1000 = 840`, so hard is 42 and soft is 458 — a hard parry in about
0.4 per cent of those exchanges.

#### The shipped total-interception matrix

| Defender configuration | vs `GreatBlade` | vs `HeavyChopper` | vs `ThrustingBlade` | vs `Bolo` | Row mean |
| --- | ---: | ---: | ---: | ---: | ---: |
| `GreatBlade`, no shield | 3200 | 2900 | 2600 | 3000 | 2925 |
| `HeavyChopper`, no shield | 2400 | 2200 | 2000 | 2300 | 2225 |
| `ThrustingBlade` with `TallHardwood` | 3900 | 3800 | 4000 | 4000 | 3925 |
| `Bolo` with `TallHardwood` | 3900 | 3800 | 4000 | 4000 | 3925 |

Unweighted mean across all sixteen cells: **3250 basis points, or 0.325**, against the
research target centre of 0.33. Shielded loadouts sit at 0.39, shieldless at 0.22 and
0.29, reproducing the shielded-versus-shieldless gap the research says matters most.
The largest single cell is 4000, so `MaximumInterceptionBasisPoints` is unreachable
with shipped values and exists only as a guard against a future tuning pass.

#### The two acceptance criteria

Both are required, and both come from research section 5.2.

**Criterion one, interception share.** The defence-attributable non-landed share over a
whole 200-agent battle is measured as shield intercepts plus weapon intercepts plus
voids, divided by **accepted attacks**. Every accepted attack is already in reach by the
`IsWithinAttackRange` gate at `src/Hukbo.Core/Simulation/BattleSimulation.cs:643`, so no
further qualifier belongs in the denominator; a narrower one would not match what
`CombatMetrics` counts. It is deliberately **not** measured as total non-landed
including any future attacker-accuracy failure, because that band would move whenever
unrelated code changed.

**There is exactly one enforced threshold, and it is the wide one.** The change **fails
outside 0.25 to 0.45**, and that single band lives in one test,
`DefenceAttributableNonLandedShareStaysInsideTheAcceptanceBand`. The narrower 0.30 to
0.40 with centre 0.33 is the **design target**: it is what the shipped tables aim at and
what a re-tune steers back toward, and it is deliberately **not** a second gate.
Enforcing two thresholds in two places is how a plan acquires an undefined re-tune
trigger, and revision 3 had exactly that, with the test at the wide band and the human
gate at the narrow one.

The distinction matters more than it looks. The shipped unweighted mean is 0.325, only
0.025 above the narrow floor, and the **measured share drifts upward over a run**:
shielded loadouts intercept at 0.3925 while shieldless sit at 0.2225 and 0.2925, so
shielded warriors outlive shieldless ones and receive a rising share of all attacks as
the battle proceeds. A whole-battle measurement is therefore expected to land above the
static mean, and a run measuring 0.36 is behaving correctly rather than drifting out of
tolerance.

**Criterion two, termination.** Across a sweep of at least twenty seeds at 200 agents,
**at least 95 per cent of battles must reach a winner before the tick cap, and the
median decisive tick must be at or below 50 per cent of the cap** — 5,000 ticks against
the default `TickLimit` of 10,000.

The median clause is the one that can actually fail. A 95-per-cent-termination clause
alone passes happily while every battle finishes at 98 per cent of the cap. Time to
decision is proportional to one over `attack rate times (1 - intercept) times damage per
landed blow`, so interception is a **multiplier on a stall rather than its cause**:
moving from 0.48 to 0.33 shortens time to decision by only about 1.29 times. If the
battle stalls at 0.48 it may still stall at 0.33, and no interception figure inside the
defensible band will rescue it. If criterion two fails, the correct response is to look
at the attack rate and the damage per landed blow before touching the clash tables at
all.

**If the two criteria genuinely conflict, the plan stops.** Section 2.3 settles the
direction, termination wins and interception is lowered, but lowering it below 0.25
fails criterion one, and the research floor of 0.15 is the hard limit below which the
universal equipment record becomes inexplicable. There is therefore a band in which no
authorized move exists. In that case the implementer **halts, records the measured
interception share and the twenty-seed termination figures, and escalates to a human to
amend the band in this document before any table or any assertion is touched.** Silently
widening a test band to accommodate a tuning failure is forbidden, and plan section 6
forbids weakening an assertion, so without this escalation the two rules deadlock.

#### Same-tick conflict rule

Each accepted attack is resolved independently against its own roll. Two attackers can
both be parried, both land, or split, and there is no ordering between them. This
preserves the section 9 invariant that simultaneous lethal attacks resolve together and
mutual kills remain possible: the clash gate only removes damage, it never reorders it.

### 3.4 Question four: total ordering and random-stream policy

There is no random stream. The clash roll is a **stateless keyed derivation** copying
the shape of `HitLocationResolver.MixAttack` at
`src/Hukbo.Core/Combat/HitLocationResolver.cs:87`, with its own domain tag.

```
ClashTag = 0x484B424F5F434C53   // ASCII "HKBO_CLS"
```

The mixer folds **eight words**, in this fixed order: the domain tag, the scenario seed,
the tick, the source entity identifier, the target entity identifier, the attacker
weapon, the defender weapon, and the defender shield. The result modulo 10,000 is the
roll. Revision 2 folded a ninth word for the crowding multiplier; that word is gone with
the modifier.

Because a dropped word is invisible to any distribution test, the plan pins **seven
isolation cases, one per folded word other than the constant tag**, each changing one
word and asserting the roll changes.

`System.Random` is not used. `SplitMix64` is not used and is not advanced: the only
`SplitMix64` instance in `Hukbo.Core` is the spawn-placement generator constructed in
`BattleSimulation.Create` at line 91 and consumed entirely before the first tick.
**Adding the clash roll costs zero draws from anything else**, so no existing behaviour
shifts merely because a cursor moved.

The roll is mapped to an outcome by walking a cumulative interval in one fixed order,
exactly as `HitLocationResolver.Resolve` walks `BodyPartCatalog.Ordered`:

| Interval | Outcome |
| --- | --- |
| `[0, shield)` | `ShieldBlocked` |
| `[shield, shield + hard)` | `Parried` |
| `[shield + hard, shield + hard + soft)` | `Deflected` |
| `[shield + hard + soft, shield + hard + soft + void)` | `Evaded` |
| everything above | `Landed` |

The shield channel is tested first because it is the only channel with documentary
support. The ordering is arbitrary with respect to the outcome distribution, since the
intervals have fixed widths, but it is pinned so that it cannot change accidentally.

Enum values are pinned as `Landed = 0`, `ShieldBlocked = 1`, `Parried = 2`,
`Deflected = 3`, `Evaded = 4`. They enter the event hash, so renumbering them requires a
new preset version.

A **zero-width channel must never be selected.** With `ShieldId.None` the shield interval
is `[0, 0)`, and a comparison written as `roll <= cumulative` rather than
`roll < cumulative` would select it on a roll of zero. That is a one-character defect a
distribution test would very likely never surface, so the plan carries a direct test.

**What order independence does and does not mean here.** Both entity identifiers are
folded into the key, so two warriors who swap identifiers are *expected* to produce
different outcomes; that is correct and is not the property under test. The property
under test is that the *storage order* of the same agents cannot change any outcome. The
repository already distinguishes these two ideas, in
`DeterminismTests.InputArrayOrderCannotChangeOrderedResults` against
`ContestedGroundGoesToTheLowerEntityIdAndFollowsARenumbering`, and the plan follows that
precedent with a permutation test over three storage orders of the same identifiers.

### 3.5 Question five: cache source and invalidation

**No cache.** The clash roll is recomputed from its tuple every time and nothing is
stored between ticks. The basis-point tables are **immutable definition data**, built
once inside the `CombatRuleset` constructor and never mutated, in the same category as
the existing precomputed `EffectiveWeightTable`. They are content-hashed rather than
cached, so there is nothing to invalidate.

Revision 2 introduced a per-target attacker-count scratch buffer. It is deleted along
with the crowding modifier, so this change now adds **no new buffer of any kind**.

### 3.6 Question six: save, event, and version effect

This is an authoritative change and it moves both hashes for every seed. Four separate
mechanisms cause the movement, and all four are intended.

1. `PhilippineCombatPreset.Version` goes from 1 to 2, and the clash tables are folded
   into `CombatRuleset.ComputeContentHash`, so `ContentHash` changes.
   `StateHasher.Compute` folds `rules.ContentHash` at
   `src/Hukbo.Core/Determinism/StateHasher.cs:32`, so the state hash moves at tick zero
   before a single blow is struck.
2. Blows that no longer land leave `HitPoints` higher, so agent state diverges.
3. Battles run longer, so the terminal tick and the survivor counts change.
4. `BattleEvent` gains a nullable resolution field folded into the headless event hash,
   and damage events stop being emitted for non-landed attacks, so the ordered event
   stream changes in both content and length.

Because all four move together, an ordinary run gives no way to tell an intended
movement from an accidental one. Section 9 describes the control test that separates
them.

**Two hard-coded golden content-hash constants die with this change**, and they are named
here because an unowned golden is how a determinism regression gets papered over:
`0x59FB4CA563D87A49UL` at `tests/Hukbo.Core.Tests/CombatConfigurationTests.cs:145` and
the same value at `tests/Hukbo.Core.Tests/DeterminismTests.cs:54`. The plan carries an
explicit task to replace them, and that task may only run **after** the two tests proving
the content hash responds to a clash-value change and is independent of dictionary supply
order have both passed.

`CombatPresetId` keeps its single value. The identity of the preset is not changing; its
version is.

`BattleSnapshot` gains **no new field**. Its embedded copy of `_lastEvents` does change,
because those events now carry a resolution, but that is the event contract changing
rather than the snapshot schema changing.

`BattleEventKind` gains **no new member**. See section 4.

### 3.7 Question seven: worst-case complexity and benchmark workload

The gather loop stays order n over agents, with one added FNV-1a mix over eight 64-bit
words and about eight integer operations per accepted attack. No new pass, no new buffer,
no new allocation. With the default `AttackCooldownTicks` of 5 at most a fifth of the army
strikes in any one tick, so at 200 agents the added work touches at most about 40
proposals per tick.

At a mean interception of 0.325, damage throughput falls by a third and battle length
rises by roughly one over `1 - 0.325`, a factor of about 1.48. Against the recorded seed-1
terminal tick of **1081** that predicts a terminal tick near **1600**, inside the
10,000-tick cap and inside the 5,000-tick median clause of criterion two, though with less
headroom than the pre-merge baseline gave.

**That baseline moved once already and the margin narrowed.** Before merging `main`'s
mirrored starting formations, seed 1 terminated at tick 657 and the same arithmetic
predicted 975. Mirrored deployment lengthened the battle to 1081 ticks on its own, with no
clash mechanic present at all, so the prediction rose with it. The lesson for whoever reads
this next: the 5,000-tick median clause is measured against a baseline that any deployment,
movement, or targeting change can move, and criterion two should be re-derived rather than
assumed after any such merge.

That is a prediction, not a budget. Criterion two is the budget.

Benchmark workloads: the canonical 200-agent, 10,000-tick, seed-1 gate workload; the
twenty-seed sweep required by criterion two; and the report-only 500-agent stress
workload.

### 3.8 Question eight: spectator explanation

Five outcomes, four discovery channels, none of which requires reading source.

| Channel | Where | What the spectator perceives |
| --- | --- | --- |
| Event log text | `BattleEventFormatter.GetActionLabel` | Five distinct lines: hit, stopped by the shield, parried, turned aside, stepped off the line |
| Sound | three new slots plus deliberate silence | Wooden thud, hard metallic ring, short metallic scrape, and for a void, nothing at all |
| Animation | the swing impact phase | Three branches: a contact outcome recoils, a landed blow follows through and stops on the target, an evaded blow follows through past it |
| Visual effect | a small bright cross at the contact midpoint | Appears for the three contact outcomes and for neither a landed blow nor a void |

**Void is discoverable, but by fewer channels than the other four, and this document
states that honestly rather than claiming parity.** Revision 2 rejected the void outright
because nothing visible or audible happens; revision 3 over-corrected and credited it
with the same four independent channels as the rest. Neither is right.

For `Evaded` the honest accounting is **one positive channel and three absences**: the
event-log line is the only thing that names it, while the missing cue, the missing cross,
and the missing blood are each an absence a spectator must notice rather than perceive.
Absences are real signals, and each is well covered by an automated test, but three
absences are not three independent discovery channels.

The animation channel needs the same correction. It separates the three contact outcomes
from everything else, because only a contact outcome recoils. Left as revision 3 had it,
`Landed` and `Evaded` were the **same motion** — a follow-through — so the animation
distinguished `{contact}` from `{landed, evaded}` and could not name the fifth outcome at
all. **`Landed` therefore gets its own pose branch**: a landed blow follows through and
stops on the target, an evaded blow follows through past it. Three pose classes rather
than two, so the animation carries the distinction that matters.

The claim this document makes is therefore narrower, checkable, and in one place still
**unproven**: a spectator can discover the clash system through any one of the four
channels, and can separate `Evaded` from `ShieldBlocked` through any of them. Separating
`Evaded` from `Landed` is the hard case, and it rests on:

| Separator | Status |
| --- | --- |
| The event-log line | Shipped and asserted by a test. Solid |
| The absent `HitEffectSystem` impact ring | Solid. The ring keys on `Damage`, so it is present for a landed blow and absent for a void, and unlike blood it survives every user setting |
| The animation | **`PENDING`.** See below |
| The absent blood | **Not counted.** Blood disappears entirely under `GoreIntensity.Off` at `src/Hukbo.Client/Presentation/BloodEffectSystem.cs:87`, which is a shipped user setting, so it cannot be leaned on. Revision 4 listed it and should not have |

The animation leg is marked `PENDING` rather than claimed, because the arithmetic does not
obviously support it. At shipped zoom a pawn is roughly 26 pixels tall and adjacent pawns
sit 10 to 16 pixels apart, and the difference between stopping on the target and
following through past it is a few pixels of weapon-tip travel held for the 50-millisecond
impact phase — one to three frames at 1x, under one frame at 4x, and sub-pixel below
`PawnDetailTier.Medium`. The third pose branch is still the right thing to build: it makes
the distinction nameable, testable, and available at high zoom, and without it the
animation could not express the outcome at all. But whether a person can see it is a
question for the smoke checklist, not an assertion for this document.

**If the smoke row returns FAIL**, the recorded disposition is: `Evaded` keeps the
event-log line and the absent impact ring, which are two independent separators and enough
to justify the outcome, and the animation leg is struck from this table rather than the
outcome being struck from the model. If both of those were also to fail, the honest move is
to drop `Evaded` and fold the void probability into `Landed`, which would then require
re-deriving the acceptance band in section 3.3.

No claim is made that the battle sounds different in aggregate where footwork is working;
that formulation is unfalsifiable and is withdrawn.

The absence channel generally matters as much as the presence channel. A spectator who
has watched a few battles will read "he swung, there was a ring, and nothing happened to
the other man" without being told anything.

Because the `Evaded` claim is the weakest of the five, the smoke checklist carries **two
separate rows** for it: one asking whether a void is distinguishable from a block, and
one asking whether a void is distinguishable from a landed blow. The second is the
question that actually decides whether the fifth outcome earns its place, and revision 3
had no row asking it.

Two channels have a live defect risk, and both are now owned by named tasks.

**The sound channel is dead as designed unless the hit class is nulled.**
`SoundDirector.Ingest` at `src/Hukbo.Client/Audio/SoundDirector.cs:72-75` derives a
`HitClass` from `battleEvent.HitLocation`, which this design deliberately keeps non-null
on a non-landed attack. It passes that class to `Resolve`, which calls
`Player.GetStatus(sound, hitClass)`. `MonoGameSoundPlayer.GetStatus` at lines 78 to 81
keys on the `(sound, hitClass)` pair, and a classless slot is only ever registered under
`(sound, null)` by `SoundLibrary.ResolveVariants` at lines 118 and 133 to 140. Every
clash cue would resolve `Missing`, the entire audio channel would be silently dead, and
no mapping test would notice. The fix is to force the hit class to null whenever
`SoundCatalog.IsHitLocationDriven(sound)` is false, with a test asserting a clash cue
reaches `Played` rather than merely that it maps to a slot.

**Recoil and the swing-through both need smoke rows**, because they are perceived
effects no automated test can confirm.

### 3.9 Question nine: tests that fail before implementation and pass afterward

The full list is the plan task table. Three structural rules govern it, and revision 2
broke all three.

**Red tests must fail on an assertion, not on a missing type.** A test file referencing a
type that does not exist yet fails the whole test assembly to compile, taking every other
test in that assembly down with it. That is recorded in `docs/development/testing.md` for
the plains-backdrop re-run, where five non-compiling sound test files had to be moved
aside before anything could be measured. The **entire public and internal surface
therefore exists as neutral stubs before any test is written**: `ClashResolver.MixClash`
returns zero, `Resolve` returns `Landed`, `SplitWeaponChannel` returns a zero pair, the
`CombatRuleset` clash accessors return zero, and the client swing and clash types are
no-ops. `InternalsVisibleTo("Hukbo.Core.Tests")` is already set, so `internal` suffices.

**Tests written against the code under test are not an oracle.** See section 9.

**Boundary values, dead references, and broken existing tests all need owners.** See
sections 5 and 9.

## 4. Event feed pressure

The feed retains at most 200 ordered events. The collision change solved its pressure
problem by refusing to emit an event at all and putting a per-agent `MovementResolution`
enum on `AgentView`, because a packed front produces thousands of contacts per tick.

**The clash is not in that regime.** A contact pair exists whenever two bodies are near
each other, every tick, for every pair — the recorded 200-agent run counted 57,295
candidate pairs. An accepted attack requires a living attacker, a living target, a target
in reach, and a cooldown at zero; the same run recorded 8,945 attack-capable agent-ticks
across the pre-merge 657-tick run, and with a five-tick cooldown the number actually accepted is a
fraction of that. Attacks are already one event each.

The design therefore adds **no new `BattleEventKind` member**. `BattleEvent` gains a
nullable resolution field alongside the existing nullable `Weapon` and `HitLocation`,
populated only for attack events by the same factory that already validates those two.

- Event count per tick is **unchanged for attacks and strictly lower overall**, because a
  non-landed attack emits no damage event. The feed gets quieter, not busier.
- The event-kind filter keeps working with the same five kinds.
- The event hash fold gains one field rather than a new kind, following the existing
  `ulong.MaxValue` null-sentinel convention.

Two costs, both corrected from revision 1, which wrongly claimed the feed needed no
modification at all.

**The defence-in-depth guard in `BattleEventFeed` must be extended.** The guard at
`src/Hukbo.Client/Presentation/BattleEventFeed.cs:418-429` checks that an attack-kind
event carries a weapon and a hit location before handing it to the formatter. Once the
formatter dereferences the resolution, a `default(BattleEvent)` whose kind reads as
`Attack` would throw through the text filter. The guard gains a resolution check in the
same task that changes the formatter.

**A non-landed attack reads `Value: 0` in the event detail panel.**
`BattleEventLogPanel.Details.cs:120` prints the raw value. That line is owned by the
formatter task, which suppresses or labels the damage line rather than leaving a
confusing zero on screen.

The remaining genuine cost is that a spectator filtering to attacks sees non-landed blows
mixed in with landed ones. That is the correct reading, and the line text distinguishes
them.

## 5. Existing tests this change breaks

An implementer meeting these mid-task will be tempted to relax an assertion, which is how
a real regression ships. Each is listed with its expected disposition, and reviewing this
list is a task that must complete **before** the attack stage is touched.

| Test | Why it breaks | Expected disposition |
| --- | --- | --- |
| `BattleSimulationTests.cs:51 AgentsAtExactRangeAttackAndRespectCooldown` | Asserts hit points of exactly 90, 90, and 80. Any interception changes those. | **Needs a clash-neutral tuple.** Construct the scenario against a zero-interception profile so the test keeps asserting cooldown behaviour rather than clash behaviour. |
| `BattleSimulationTests.cs:116 DamageIsAccumulatedBeforeMutualDeathResolution` | Asserts both agents die on one tick, which requires both blows to land. | **Needs a clash-neutral tuple.** The property is simultaneity, not interception. |
| `BattleSimulationTests.cs:635 AcceptedAttacksCarryTheSourceWeaponAndAResolvedHitLocation` | Asserts `Value == DamagePerAttack`. Its `Bolo` attacker against a `GreatBlade` with `TallHardwood` defender now computes to 3000 basis points non-landed, so the assertion fails about one time in three. | **Needs a clash-neutral tuple**, plus a new sibling asserting the same weapon and hit-location carriage on a non-landed attack with `Value == 0`. |
| `BattleSimulationTests.cs:677 MultipleAttackersOnOneTargetRetainIndividualHitLocationsButOneAggregatedDamageEvent` | Aggregation now sums only landed attacks. | **Needs a clash-neutral tuple.** |
| `BattleSimulationTests.cs:411 CanonicalTwoHundredAgentBattleTerminatesWithinTheTickLimit` | Battles get about 1.48 times longer. | **Survives as written**, since the predicted terminal tick near 1600 is far inside 10,000. It is also the cheapest early warning that interception is too high, so it runs before the full gate. |
| `PhilippineCombatIntegrationTests.cs:427 Regression_SameTickMutualDeathEventsPrecedeTheOutcomeEventInEmissionOrder` | Requires both lethal blows to land. | **Needs a clash-neutral tuple.** Emission order is the property. |
| `PhilippineCombatIntegrationTests.cs:367 Regression_AggregateDamagePerTargetPerTickEqualsSumOfIndividualAttackValuesAcrossAFullBattle` | Compares aggregated damage against the sum of individual attack values. | **Survives, but only because a non-landed attack is emitted with a value of zero.** That coupling is load-bearing: if a later change suppressed the attack event instead of zeroing its value, this test would silently start comparing a shorter list. The coupling is recorded in a comment on the test. |
| `CombatConfigurationTests.cs:268` and `:324` | Construct `CombatRuleset` with named arguments and no clash profile. | **Survive unmodified**, because the new constructor parameter is optional and defaults to a neutral all-zero profile. |
| `CombatConfigurationTests.cs:145` and `DeterminismTests.cs:54` | Pin `ContentHash` to `0x59FB4CA563D87A49UL`. | **Re-baselined by a dedicated task**, only after the two content-hash behaviour tests pass. |

### 5.1 The seam: no simulation can currently be given a ruleset

Revision 3 asserted that the neutral constructor default made the control test cheap to
build. **That was false**, and it is the largest correction in this revision.

Four call sites fetch the ruleset from the registry and nothing else can supply one:

```
src/Hukbo.Core/Determinism/StateHasher.cs:15
src/Hukbo.Core/Simulation/BattleSimulation.cs:90    Create
src/Hukbo.Core/Simulation/BattleSimulation.cs:166   CreateForTesting
src/Hukbo.Core/Simulation/Scenario.cs:195           Validate
```

The private `BattleSimulation` constructor at line 34 does take a `CombatRuleset`, but
neither factory exposes it, and `CreateForTesting(Scenario, params AgentState[])` takes
agents only. `CombatPresetId` has exactly one value and section 3.6 keeps it that way.
The neutral default therefore only ever reaches a directly constructed `CombatRuleset`,
which no simulation ever uses. Without a seam, the control run in section 9 is not
constructible, and neither are the five dispositions above that need a clash-neutral
configuration.

Three changes open the seam, and **all three are required**.

**An overload on `CreateForTesting`.** `internal static BattleSimulation
CreateForTesting(Scenario, CombatRuleset, params AgentState[])`, alongside the existing
overload which keeps fetching from the registry. This serves the five dispositions above,
every one of which already calls `CreateForTesting`, at
`BattleSimulationTests.cs:59`, `:124`, `:649`, `:683` and
`PhilippineCombatIntegrationTests.cs:444`.

**An overload on `Create`, which is the one the control run actually needs.** Revision 4
put the seam only on `CreateForTesting`, and that is not sufficient.
`CreateForTesting(Scenario, params AgentState[])` at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:151-169` takes explicit agents and never
runs spawn placement or the `SplitMix64` draws that `Create` performs at `:85-149`. The
control run is seed 1 at **200 agents**, which only `Create` can produce, and there is no
way to lift agents out of a `Create`d simulation to feed the other overload: `Agents` and
`CreateSnapshot` both return `AgentView`, and `_agentStates` is private. So
`internal static BattleSimulation Create(Scenario, CombatRuleset)` is added beside the
public `Create`, and the public one delegates to it after its registry fetch.

**The seam is bounded to rulesets that share the preset roster.** `Scenario.Validate` at
`src/Hukbo.Core/Simulation/Scenario.cs:195` fetches from the registry solely to check
`RosterCounts.Length` against `rules.Roster.Count`, and it is deliberately left alone. For
the only sanctioned use — a neutral-`ClashProfile` variant of the same preset — the roster
is identical and the validation is correct. Injecting a ruleset with a *different* roster
would have the scenario validated against the registry roster while the simulation ran on
the injected one, which is silently wrong. The overload therefore asserts roster equality
against the registry entry for `scenario.CombatPreset` and throws otherwise.

**`StateHasher.Compute` takes a `ulong contentHash` parameter rather than re-fetching the
ruleset at line 15.** Without this a simulation running on a neutral ruleset would still
fold the shipped `ContentHash` into its state hash, and the control run would be comparing
a hash that never saw the ruleset it was actually using.

**The parameter is the content hash, not the ruleset**, and that is both simpler and
strictly more durable. `rules` is used at exactly one line inside `Compute`, line 32, so
passing the `ulong` removes the dependency entirely rather than rerouting it.
`BattleSimulation.ComputeStateHash` passes `_rules.ContentHash`, the test helper passes
`CombatPresetRegistry.Get(scenario.CombatPreset).ContentHash`, and the control run in
section 9 injects `0x59FB4CA563D87A49UL` directly.

That last step needs one more member, and its absence is easy to miss. `ComputeStateHash()`
at `BattleSimulation.cs:197` is parameterless and unconditionally folds `_rules.ContentHash`,
so there is nowhere for the control run to pass the recorded value — and reaching
`StateHasher.Compute` directly is blocked by the same egress problem that forced the `Create`
overload, since it needs `IReadOnlyList<AgentState>` and `_agentStates` is private. The seam
therefore also adds **`internal ulong ComputeStateHash(ulong contentHash)`** beside the public
parameterless overload, which delegates with `_rules.ContentHash`. Without it the `ulong`
parameter stops one call site short of the only place that needed it.

The fixture capture does **not** need that overload, and deliberately does not depend on it:
on unmodified `main` the `_rules.ContentHash` that the parameterless method folds *is*
`0x59FB4CA563D87A49UL`, so the two agree by construction and the capture keeps its place as
the first executed task rather than acquiring a dependency on the seam.

The durability matters more than the simplicity. `Fnv1a.Add` at
`src/Hukbo.Core/Determinism/Fnv1a.cs:22-29` runs eight XOR-then-multiply rounds
regardless of the value it is given, so **folding a zero word is not a no-op** — it
multiplies the accumulator by the prime eight times. `ComputeContentHash` is a flat fold
with no version gate, so the moment the clash tables are folded into it, *every* ruleset
content hash moves, including a `version: 1` ruleset carrying `ClashProfile.Neutral`. Had
the parameter been the ruleset, the control run would have recomputed a content hash that
no longer matched the recorded one, and a correct guard would have started failing
mid-implementation over a non-defect. Passing the `ulong` makes the recorded equality
survive that fold permanently.

It is a production signature change with **two** call sites, not one:
`src/Hukbo.Core/Simulation/BattleSimulation.cs:198` and
`tests/Hukbo.Core.Tests/DeterminismTests.cs:152`, the latter inside
`ComputeSingleAgentStateHash`, which backs `StateHash_ChangesWhenAnyAgentWeaponArmorOrShieldChanges`
and its body-radius and collision-policy siblings. The test-side fix is one line, but the
file has to be declared or the first barrier does not compile.

It is hash-safe by construction: `BattleSimulation.ComputeStateHash` is an instance method
holding `private readonly CombatRuleset _rules`, whose `ContentHash` is exactly what the
registry path would have produced for any `Create`-built simulation, so no hashed value
changes and the seam task can prove that by re-running the seed-1 workload against the
current baseline.

**A copy helper, so the injected ruleset is provably the preset.**
`public CombatRuleset WithClashProfile(ClashProfile profile)` returns a copy with every
other field preserved. Without it, building the neutral ruleset means hand-reassembling six
constructor arguments — sixteen `ResolveWeaponWeight` reads per weapon, twenty-six
`ResolveDefenseMultiplier` reads, and a hard-coded armor list, because `_armors` has no
accessor at all yet is folded into `ComputeContentHash` at `CombatRuleset.cs:259-263`. That
reassembly happens to work today only because `ArmorId` has one member, and it silently
stops being faithful the moment a second is added. The helper removes the guess, makes the
injected ruleset identical to the preset except for the profile, and is the same helper the
five section 5 dispositions need.

**There is no such thing as a clash-neutral loadout pairing.** The minimum total
interception in the shipped tables is a `HeavyChopper` defending a `ThrustingBlade` at
1100 weapon plus 0 shield plus 900 void, or 2000 basis points, and every defender has a
non-zero void channel. So the five dispositions above cannot be met by picking a lucky
pair of weapons. Meeting them by hand-picking seeds and entity identifiers whose roll
happens to land would be worse: any re-tune of the tables, and any change to the mixer,
would silently move those rolls and convert five preserved regression tests into five
that quietly stop testing what their names claim. **The seam is the only sound
mechanism**, and every disposition above uses it.

### 5.2 One more, created rather than broken

`BattleEvent.Attack`
has **twenty call sites across eleven files**, nine of them test files owned by other
workstreams. Making the resolution parameter required would fail the whole solution build
and make the first barrier unsatisfiable. **The parameter is therefore optional and
defaults to `Landed`.** To stop that default from masking a missing wire-up in production
code, the internal `BattleSimulation.AddAttackEvent` takes the resolution as a *required*
parameter, so the default only ever benefits test call sites, and a test asserts that a
non-landed attack event actually carries its resolution.

## 6. Part B, the swing animation

### 6.1 The timing budget is set by the cooldown, not by the research

The animation research assumes a 60 Hz tick and lands on 350 to 580 milliseconds. **That
does not fit this game.** Verified: `Scenario` defaults to a tick rate of 20 and an attack
cooldown of 5 ticks, so a warrior strikes once every 250 milliseconds of simulated time at
1x. A 450-millisecond swing would still be mid-recovery when the next blow lands.

| Phase | Share | At 1x, 20 Hz, cooldown 5 | Purpose |
| --- | ---: | --- | --- |
| Anticipation | 36 per cent | 90 ms | The weapon pulls back outside the body silhouette |
| Strike | 20 per cent | 50 ms | The arc sweeps through |
| Impact hold | 20 per cent | 50 ms | Held at full extension; where the clash reads |
| Recovery | 24 per cent | 60 ms | Return to neutral |

All four shares are `PROVISIONAL`, expressed as shares of one duration constant so that a
change to the tick rate or the cooldown cannot silently desynchronise the animation.

Because the client offers 2x and 4x, the swing clock advances on elapsed seconds
multiplied by the speed multiplier. Without that, a 4x battle shows every warrior
permanently mid-swing. `HitEffectSystem` and `BloodEffectSystem` keep advancing on
unscaled presentation time, unchanged.

### 6.2 Facing is derived, never stored

`AgentState` has no facing and this design does not add one. The swing direction is
computed in presentation from the attacker position to the target position at ingest,
which is what `BloodEffectSystem.ResolveDirection` already does to aim a blood spray, and
which is already covered by interactive smoke row 37.

That precedent carries an obligation. `ResolveDirection` has an explicit path for an
attacker absent from the view list. The swing system and the clash effect system need the
same: an attack event may name an entity missing from the supplied views, and a clash
effect placed at the midpoint of two agents needs both. Neither may throw, and neither may
place an effect at the origin.

### 6.3 Bounded by construction, and testable by construction

One swing slot per agent, fixed-capacity array sized at construction, newest attack wins.
An agent cannot accumulate swings, the array cannot grow, and the draw path allocates
nothing.

`PawnGeometry.Create` gains an **optional** swing-pose parameter defaulting to none, so
every existing call site and every existing `PawnGeometryTests` case compiles and passes
unchanged, and the function stays pure.

Two structural rules, both taken from defects this repository already caught once.

**The arc trail is a field on the layout, not a formula in the renderer.**
`docs/development/testing.md` records the plains-backdrop finding: a duplicated
ground-cell formula left the shipped render loop uncovered while the tests constrained a
method with no production caller. The trail is computed once into `PawnLayout`, the
renderer only consumes it, and the test asserts the layout field.

**The per-pawn pose resolution loop does not live in `ArenaGame`.** That file is banned
from tests, so anything in it is untestable by construction. The mapping from the swing
store and the agent views to a per-pawn pose is extracted into a pure `SwingPoseResolver`,
and `ArenaGame` keeps only the wiring. `SwingPoseResolver` must also pin the **lookup
shape** a caller uses to fetch one pose inside a draw loop, because that lookup is the
part that lands in the untestable file, and a resolver whose two tests only cover "no
swing" and "one pose per swing" leaves it unspecified.

### 6.4 The render path is four files, not one

Revision 3 named only `ArenaGame.cs`. Verified, the swing pose has to travel through four
files and one of them is a third-party call site that must not break:

| File and line | What it needs |
| --- | --- |
| `src/Hukbo.Client/ArenaGame.Rendering.cs:264` | The actual per-pawn draw loop. Revision 3 did not name this file at all |
| `src/Hukbo.Client/Rendering/PawnRenderer.cs:37` | `Draw` gains an **optional** pose parameter to reach `PawnGeometry.Create` at line 56 |
| `src/Hukbo.Client/UI/AgentInspectorPanel.cs:109` | A third `PawnRenderer.Draw` call site, for the inspector portrait. It breaks unless the parameter is optional, and it deliberately passes no pose: a portrait is a still |
| `src/Hukbo.Client/Rendering/PawnRenderer.cs:26` | `GetBounds` feeds the frustum cull at `ArenaGame.Rendering.cs:254` |

The last row is a real decision rather than an oversight, and revision 4 defended it with
two arguments that are both wrong. Both are withdrawn here and replaced with the correct
one.

**The decision stands: culling deliberately uses neutral, pose-blind bounds.** The reason
is **draw-list determinism**. Making the cull depend on the swing pose makes the set of
drawn pawns a function of presentation animation phase, so the same tick renders a
different draw list depending on where each swing clock happens to sit. That is a
presentation-side dependency on a clock, of exactly the kind this repository keeps out of
every decision that matters, and it makes a rendering discrepancy irreproducible from a
tick number alone.

**Withdrawn argument one: flicker.** A pose-aware cull would not flicker a pawn in and
out. For a pawn whose neutral bounds miss the arena rectangle, it would *add* the pawn
while the weapon is genuinely inside and drop it once the swing recovered, which is
correct inclusion rather than an artefact.

**Withdrawn argument two: the padding absorbs it.** It does not, by roughly four times.
`PawnGeometry.cs:115-118` inflates by `Math.Max(3, ceil(3 * apparentScale))`, which is 3
to 8 pixels. A `GreatBlade` tip at `(15, -19) * scale` rotating about a grip at
`(1, -6) * scale` sweeps a lever of about 14 units, roughly 34 pixels at the maximum
apparent scale of 2.40.

**The real artefact, stated accurately.** `arenaBounds` is the scissored arena panel, not
the screen, at `src/Hukbo.Client/ArenaGame.Rendering.cs:64-72`. So a pawn whose body sits
outside the panel while its weapon would sweep into it is dropped entirely, and the
visible symptom is a weapon tip clipped at the **panel** edge while panning. That is
accepted, and it gets its own `PENDING` smoke row rather than being asserted away here.

### 6.5 What is explicitly not done

No hit stop, no screen shake, no full-screen flash. All three break down at 200 agents,
and `hukbo-client-ui` already forbids letting a visual effect gate, pause, or reorder
simulation advancement. No screen-space density suppression: the fixed-capacity pool plus
zoom-tiered detail is the noise control, and density suppression is the next lever if a
human reports a white bar along the front rank.

## 7. Part C, the clash sounds

### 7.1 Three slots, one silence

The audio research suggests a material-pair by solidity matrix of eight slots. This roster
does not support eight: one blade material, one shield material, no shaft, so two material
pairs exist and the solidity axis applies only to the first.

| New slot | Canonical base name | Triggered by | Character |
| --- | --- | --- | --- |
| `ClashBladeHard` | `clash-blade-hard` | `Parried` | Hard metallic arrest, full transient and ring |
| `ClashBladeSoft` | `clash-blade-soft` | `Deflected` | Short bright scrape, fast decay, no ring |
| `ClashShield` | `clash-shield` | `ShieldBlocked` | Dull wooden thud into a large resonant board |
| none | none | `Evaded` | Deliberate silence, which is the signal |

Enum members are appended at values 9, 10, and 11; nothing existing is renumbered. Five
numbered takes per slot, fifteen files. The research guidance is three to five per slot
with the effort spent on the material matrix rather than on variant count.

These are classless slots, so `SoundCatalog.IsHitLocationDriven` stays false for them and
the existing numbered-variant discovery in `SoundLibrary` over `GetSlotVariantPrefix` picks
them up with **no loader change at all** — provided the hit class is nulled on the way in,
per section 3.8, which is the one genuine defect in this area.

Variant selection continues through `SoundVariantSelector.Select`, which derives its index
from the tick and the source entity identifier, so a replay sounds identical.

### 7.2 The budget inversion this change exposes

`SoundCueBudget` allows three cues per slot and eight per frame, discarding the newest
rather than stealing an in-flight voice, which the research identifies as the correct
policy for very short impacts.

Two separate problems sit behind that, and revision 2 fixed only the first.

**Within a tick, emission order starves rare cues.** `SoundDirector.Ingest` walks a tick
event batch in emission order — attacks, damage, deaths, outcome — so a busy tick can spend
all eight cues on attacks before reaching a death. Adding three slots competing for the
same eight makes it worse.

**Across a frame, the budget is shared by several ticks.** `SoundDirector.BeginFrame` runs
once per frame at `src/Hukbo.Client/ArenaGame.cs:177`, while `Ingest` runs once per tick
inside the catch-up loop at `:534`. At 2x and 4x several ticks share one eight-cue budget,
so a two-pass ordering *within* a tick does not stop tick N attacks from starving tick N
plus one deaths. A test of the within-tick ordering would pass while the regression
persisted.

The fix is therefore both halves, and the second is load-bearing: `SoundCueBudget` gains a
**reservation** for the rare slots — death and the three outcome cues — that attack and
clash cues cannot consume, and `SoundDirector.Ingest` additionally makes two passes within
a tick. The authoritative event stream is untouched; this reorders only which cue asks for
the budget first. The research rule is to prioritise by rarity, not by loudness.

**This reorders `SoundCueLog` rows**, and that is a visible consequence rather than an
internal detail. `SoundCueLog.Append` collapses consecutive rows sharing tick, sound, and
status, so both the collapse behaviour and the panel order change: the sound log will show
deaths before attacks while the battle event log still shows attacks first. That divergence
is intentional, it is asserted by a test, and it is named here so it is not later reported
as a defect.

Per-cue pitch and gain variation are **not** implemented. `SoundDirector.CueVolume` is a
fixed 0.8, `ISoundPlayer.Play` has no pitch parameter, and the audio path does no mixing.
The variation is baked into the generated takes.

### 7.3 Generation

The three slots are generated through `scripts/sfx.ps1`, which parses slot names out of
`SoundCatalog.cs` and picks them up automatically once the catalog names them. Each needs
an entry in the default-prompt table, and the class parameter must not be passed: the
script correctly rejects a class on a non-attack slot, and a clash carries no hit location.
`-DryRun` returns before the key lookup, so the wiring is verifiable without a key.

Prompts follow the house rules: one sound event and never a scene, always excluding music
and voice, always naming the surface and the space, never naming a cultural
identification.

## 8. Ambiguities worth naming

### 8.1 What "weapon clash" means

*Reading one, one-sided defensive interception.* An attack is resolved against the defender
shield, weapon, and footwork. This is what the historical research is about from beginning
to end.

*Reading two, symmetric mutual clash.* Two agents who both strike each other in the same
tick have their blows cancel, in the manner of a mirrored attack direction in a
player-controlled game.

**Recommendation: reading one.** It is what the research supports, reading two needs facing
to be meaningful, and reading two is strictly rarer — both parties off cooldown, in reach,
and targeting each other on the same tick — so it cannot carry the feature. Nothing
forecloses adding it later, since the resolution enum is already the right carrier.

### 8.2 Whether a clash negates damage or reduces it

**Recommendation: negates.** A reduction reads as "he hit me a bit less hard", which is
indistinguishable from armour and needs new damage arithmetic. A negation reads as "nothing
happened to him", which a spectator can see, and it keeps the section 9 invariant that each
accepted attack applies damage at most once trivially true.

## 9. Test oracle strategy

Both review gates made the same point in different words: every golden this change
produces descends from one execution of the code under test. The benchmark runs the new
code, the recorded oracle is what it printed, the skill copies that, and the content-hash
constants get rewritten from the same run. Nothing independently constrains the arithmetic.
Three mechanisms fix that, and all three are mandatory rather than nice to have.

**A naive reference oracle.** `tests/Hukbo.Core.Tests/NaiveClashResolution.cs`, following
the conventions of the existing `NaiveCollisionPairs.cs`, reimplements the six-step
pipeline independently in `long` arithmetic and **calls no production helper**. Its
comment cites the step-6 ordering sentence in section 3.3 verbatim, because an oracle is
only independent if the specification it is written from is unambiguous. This is the only
thing that catches a sign error, a rescale that charges the ceiling to one channel, or a
truncation that leaks a basis point, all of which pass every distribution test as long as
they do so consistently. Section 9 of the standards requires this pattern for optimized
logic and the collision work already set the precedent.

**The sweep must not read `PhilippineCombatPreset`.** Two failure modes follow if it does.
At barrier B1 the ruleset default is `ClashProfile.Neutral`, so every channel is zero and
every resolution is `Landed`; a sweep reading the preset would have the stub and the
oracle agree on all 6,400 tuples and **pass green while proving nothing**. And a mixer
vector pinned against the preset cannot have its expected values derived until the tables
are populated, so they end up pasted from output afterwards, which restores the
self-fulfilling golden the oracle exists to prevent. Every resolver test therefore
constructs an **explicit literal `ClashProfile` written out in the test file** using the
section 3.3 values. That also decouples the sweep from any later re-tune.

**The sweep runs twice: once over the shipped tables and once over synthetic
over-ceiling profiles.** The shipped tables top out at 4000 against a ceiling of 5500, so
a sweep restricted to them never enters the rescale branch at all.

**A zero-interception control run, against a committed fixture.** A `ClashProfile` with
every interception value at zero, injected through the section 5.1 seam, must reproduce
the **pre-change ordered event stream exactly** for seed 1 at 200 agents: identical kind,
sequence, source, target, and value on every event, and every resolution reading
`Landed`. This is the single highest-value test in the plan, because it isolates the four
intended hash-movement mechanisms in section 3.6 from any unintended fifth one.

It has two prerequisites that revision 3 left implicit and that make or break it.

**The comparand must be captured before the resolver exists.** "The pre-change ordered
event stream" is not available after Phase 2 — at that point the only thing to compare
against is the same binary running with a neutral profile, which proves that zero
interception yields `Landed` and nothing whatever about the pre-change behaviour. That is
precisely the self-fulfilling shape this test was added to eliminate. The stream, or its
FNV-1a fold together with the per-event field tuples, is therefore captured from
**unmodified `main`** and committed as a fixture under `tests/Hukbo.Core.Tests/` in Phase
0, before any resolver code is written.

**The state hash is asserted, in the one form that is decidable.** The revision-3 wording,
"the state hash differs only by the `ContentHash` fold", cannot be evaluated: FNV-1a is a
linear fold and one cannot inspect an output and conclude that exactly one input word
changed. Revision 4 therefore dropped the hash entirely, which went one level too far.

The seam supplies a decidable form, provided the hasher takes a `ulong contentHash` rather
than a ruleset. Build the neutral ruleset with `WithClashProfile(ClashProfile.Neutral)`,
inject it through the new `Create` overload, run seed 1 at 200 agents, and call
`ComputeStateHash(0x59FB4CA563D87A49UL)` — the overload described in section 5.1, not the
parameterless method — asserting it equals **`DC7F2E7A107C885A`** exactly at the terminal
tick. That is a plain equality against a recorded value rather than an inference about a
fold. The per-tick state-hash column uses the same overload, for the same reason: the
parameterless method folds the injected ruleset's own `ContentHash`, which after the
content-hash fold lands differs from the recorded value on both the version word and the
thirty-two clash words, so every one of the roughly 1081 rows would mismatch.

**This only works because the parameter is the `ulong`.** Had it been the ruleset, the
assertion would hold at the first barrier and then start failing the moment the clash
tables are folded into `ComputeContentHash`, because folding thirty-two additional words
moves every content hash including the neutral one — `Fnv1a.Add` multiplies by the prime
eight times per word whatever the word contains, so zeros are not free. A correct guard
failing mid-implementation over a non-defect, with no task authorised to touch it and the
plan forbidding edits to goldens, would deadlock the second barrier. Passing the recorded
`ulong` makes the equality permanent.

The fixture comparison on the event stream and a field-by-field comparison of final agent
state both remain, alongside it.

**The fixture format is per-tick digest rows, and the exclusion is load-bearing.** Seed 1
at 200 agents runs 1081 ticks and tens of thousands of events, so serialising every event is
megabytes committed to the repository, while a single whole-run fold is one number that
destroys the event-for-event comparison this test promises. The committed shape is one row
per tick carrying the event count plus an FNV-1a fold over the ordered
`(Sequence, Tick, Kind, SourceEntityId, TargetEntityId ?? 0, Value, FactionId, Weapon,
HitLocation)` tuples, **deliberately excluding `Resolution`**, because a post-change event
carries a field a pre-change event cannot and including it would guarantee a mismatch that
means nothing. Roughly 1081 rows, and a failure reports a first-divergence tick in the same
shape `benchmark.ps1` already reports as `firstMismatchTick`. The fixture also carries the
terminal tick, the outcome, both survivor counts, and the final per-agent state tuples.

**Each row also carries that tick and its `ComputeStateHash()` value**, captured with the parameterless method for the reason given in section 5.1, one more `ulong` per row, and
this is not padding. The event half of the digest is complete — the nine folded fields are
every `BattleEvent` field except `Resolution`, FNV-1a is order-sensitive so an intra-tick
reordering is caught, and the per-tick count catches insertion and deletion. The gap is
**intermediate agent state**, and the repository has already written down why that matters:
`DeterminismTests.TwoIndependentSameSeedRunsAgreeOnOrderedEventsAndStateHashEveryTick`
carries the docstring "comparing only the final state would let a divergence that cancels
itself out pass unnoticed".

The concrete miss is a field that reaches the state hash but emits no event, diverging
mid-battle and reconverging by the end. `MovementResolution` and `Intent` are both folded
per agent at `StateHasher.cs:53-54` and neither emits an event, and a clash-induced shift in
when an agent moves between `Attacking` and `Moving` is entirely plausible once a non-landed
attack still resets the cooldown. There is a second reading of the same hole: a harness
walking the public API sees only `AgentView`, which exposes eleven of the eighteen fields
`StateHasher` folds, and the seven it misses include `AttackCooldownRemaining` — the one
hashed field this change actually touches. A per-tick state hash closes both readings at
once, and it is exactly reproducible under injection because the hasher now takes the
content hash as a parameter.

**Pinned mixer vectors at the existing standard.** `HitLocationResolverTests.cs:24-32`
pins eight `[InlineData]` rows covering seed 0 and the maximum unsigned seed, tick 0 and
the maximum tick, all four weapons and both shields, **and the resulting `BodyPart`**,
with a comment stating how the expected values were derived independently. The clash
mixer matches that standard: at least eight rows, pinning both the roll and the resulting
resolution, with the derivation method in a comment, against an explicit literal profile.

One addition the hit-location precedent does not need: **at least one pinned row per
resolution value, all five.** Void sits at roughly 1000 of 10,000 and is the newest
interval and the only one bounded on both sides, so eight arbitrarily chosen tuples may
never produce an `Evaded` at all and the interval nobody has exercised is exactly the one
most likely to be wrong.

Beyond the three oracles, three categories of case that revision 1 omitted entirely:

**Boundary values, every one against a synthetic profile.** A total of exactly 5500 must
not rescale while 5501 must; the rescale must preserve channel proportions, leave no
channel negative, and never exceed the ceiling; the roll exactly at each of the four
interval edges must select the documented side; a zero-width channel must never be
selected; a total interception of zero must always land; the hard-share clamp must bind
at both 500 and 6000; and `ClashProfile` must accept the exact range bounds and reject
one step outside each, including rejecting zero for `MaximumInterceptionBasisPoints`
where the lower bound is one. Neither clamp is reachable with shipped values, so a
boundary test written against the preset is unwritable rather than merely weak.

**The thirty-two shipped values, pinned by table.** Nothing in the oracle strategy above
constrains a transcription error: the naive sweep compares two implementations reading
the *same* profile, so a wrong digit in any of the sixteen matrix cells is invisible to
it, and a presence check only proves a value exists. The repository already has the right
pattern in `CombatConfigurationTests.PhilippinePreset_UsesApprovedWeaponOverrides` at
line 62, which pins every hit-location weight by `[InlineData]`. The clash values get the
same treatment, plus a second test asserting that the four row means reproduce the
totals in section 3.3 — 2925, 2225, 3925, 3925 — which is the cheapest possible check
that the tables were entered as designed.

**Mixed-resolution aggregation.** A damage event disappears only when *every* attack on a
target is non-landed, and the single existing multi-attacker test is one of the ones
neutralised in section 5. Two attackers on one target, one landing and one not, must
produce exactly one damage event carrying exactly one blow of damage.

**Dead and missing references.** In the client, an attack event naming an entity absent
from the supplied agent views, for both the swing system and the clash effect system,
following the `ResolveDirection` precedent. In Core there is no dead-target case to write:
deaths are applied in the third loop of `GatherAndCommitAttacks` at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:699-716`, after gathering, and a target
killed on an earlier tick is refused by the `!target.IsAlive` guard at `:640`, so no
reachable state produces a proposal against a dead target. The real case, and the one
that is written, is a target driven to zero hit points by the *aggregate* of several
attacks in one tick: every contributing attack must still emit its own event carrying its
own resolution.

**Metrics, at the patterns the repository already uses.** `HeadlessRunnerTests.cs:259`
and `:311` already carry `Run_CollisionMetricsSurviveAJsonRoundTrip` and
`Run_SerializesByteIdenticalCollisionMetricsForTwoSameSeedRuns` for an identically shaped
record; the second is the one that catches a metric leaking non-determinism. Combat
metrics get both. The derived interception ratio also needs a defined value when no
attack was accepted, because the criterion-one band test reads it.

**Metrics must also be proven to reach neither hash**, and nothing in revision 4 checked
it. The repository treats derived counters as never hashed, never snapshotted, and never
persisted, and the collision proximity band was accepted only after both hashes were shown
byte-identical across its introduction. Here the seam check proves only that the seam moved
nothing, the control run no longer speaks to metrics, and the Phase 4 comparison is against
a Phase 2 pair that already contains them. The gap is closed by recording the seed-1 hash
pair immediately **before** the metrics task and asserting it is byte-identical immediately
**after**, which is the same evidence the proximity band produced and is decidable without
inspecting a fold.

**The interception-share and termination criteria.** Criterion one is asserted as a band
over the 200-agent run rather than eyeballed from a report. Criterion two needs a seed
sweep, and the existing `SeedsOneThroughTwentyProduceVictoriesForBothFactions` already
walks twenty seeds, so it is extended rather than duplicated: it additionally asserts that
at least nineteen of twenty decide before the cap and that the median decisive tick is at
or below 5,000.

## 10. Risks

| Risk | Severity | Mitigation |
| --- | --- | --- |
| The battle stalls. Interception is a multiplier on a stall, not its cause, so a model inside the defensible band can still fail to terminate | High | Criterion two, with both the 95-per-cent clause and the median clause. If it fails, the attack rate and the damage per landed blow are examined **before** the clash tables, because lowering interception below 0.25 fails criterion one and lowering it at all is capped by the research sanity floor of 0.15 |
| The two criteria conflict | Medium | Section 2.3 settles it in advance: termination wins, interception is lowered, and the result is labelled as compensation for the absent morale model rather than presented as a finding |
| Clash sounds ship permanently `Missing` because the hit class is not nulled | High | Section 3.8, with a test asserting a clash cue reaches `Played` rather than merely mapping to a slot |
| `BloodEffectSystem` sprays blood on a non-landed blow | High | It keys on `BattleEventKind.Attack` at `src/Hukbo.Client/Presentation/BloodEffectSystem.cs:112`, not on damage. Its own task and its own regression test, landing at the start of the client phase |
| Golden constants get edited to match output | High | The content-hash re-baseline is a task that may only run after the two content-hash behaviour tests pass, and the zero-interception control run is what distinguishes an intended movement from an accidental one |
| The animation desynchronises at 2x and 4x | Medium | Speed-scaled swing clock, total budgeted at one cooldown period. Confirmable only by a human |
| Three new sound slots starve the death cue across a multi-tick frame | Medium | The budget reservation, not merely the within-tick two-pass |
| A future reader re-litigates the tuning without the morale context | Medium | Section 2.3, stated in full and marked as design compensation rather than a historical claim |
| A tuning constant is read as a historical measurement | Medium | Every table carries the research statement that all sixteen weapon-intercept cells have zero evidentiary confidence, tests assert bands rather than values, and the research document is never written back into |

## 11. Not in scope

**No per-agent inspector field is added.** `MovementResolution` exists on `AgentView`
because a packed 200-agent front generates thousands of contacts per tick and a
per-contact event would drown a 200-row feed. An accepted attack is already one event per
attack, at most a few dozen per tick, so the event that already exists is the right
carrier and a per-agent field would put a fifth value into the state hash to carry
information the event already carries. This paragraph exists because revision 3 dropped it
and the question is otherwise certain to be re-litigated.

Also not in scope:

Weapon durability, edge notching, the shield angled-versus-flat mode split, per-weapon
reach, agent facing, fatigue, morale, a limb-interception channel, a spear, a buckler,
per-cue pitch or gain, screen shake, hit stop, and screen-space density suppression of
clash effects. Each is named so that "we deliberately did not" is on the record rather than
inferred from absence.
