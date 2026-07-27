# Shields as a Weapon Stat-Variant Layer — Design

Date: 2026-07-27

Status: design only. Does not authorize implementation. The ordered task list
belongs in a companion plan document, `docs/plans/YYYY-MM-DD-shields.md`, which
does not exist yet.

This design depends on two earlier designs being implemented first:
`docs/archives/2026-07-28/2026-07-27-weapon-identity-and-attributes-design.md`
(preset V2, archived: implemented and complete — gives every weapon its own
damage, reach, and cooldown) and
`docs/plans/2026-07-27-combat-preset-v3-combos-design.md` (preset V3, which adds
attack combinations and deliberately holds every roster entry at
`ShieldId.None`). Section 12 explains why this work should land as preset V4
after V3 rather than folding into V3.

## 1. What this changes and why

A shield in Hukbo today does exactly one thing. `PhilippineCombatPreset` gives
`ShieldId.TallHardwood` a target-weight multiplier profile that sets chest and
abdomen to 500 of 1,000 basis points, and `CombatRuleset` multiplies those
values into the attacker's weapon target weights when building its effective
weight tables. The result is that a warrior carrying the tall hardwood shield
gets hit in the chest and abdomen about half as often as they otherwise would,
and the probability that would have gone there is redistributed across arms,
legs, head, neck, and face instead.

That is a real effect and it is worth keeping. It is also the entirety of what a
shield does. Carrying one does not change how hard the warrior hits, how far
they reach, how quickly they recover, or — once V3 lands — how readily they
chain blows together. A shield is currently a property of the person being
attacked and never a property of the person carrying it.

This design makes a shield a property of its carrier. Specifically, a shield
becomes a **stat-variant layer over the (weapon, shield) pair**: the shield the
warrior carries modifies that warrior's own weapon attributes — damage, reach,
attack cooldown, and the four combination attributes V3 introduces. Different
weapons take different modifications from the same shield, because a tall shield
constrains a wide cut and a linear thrust differently.

The existing targeting multiplier is **kept, not replaced, and not folded in**.
Section 3 explains why those two effects cannot be merged even though they share
a key type, and why merging them would be a correctness bug rather than a
simplification.

## 2. Verified current state

Everything in this section was read out of the working tree through the
`tokensave` code-graph tools during this design pass. It is recorded because
several of the decisions below depend on details that are easy to get wrong from
memory.

| Fact | Location |
| --- | --- |
| `enum ShieldId { None = 1, TallHardwood = 2 }`, marked as part of the replay and content-hash contract | `src/Hukbo.Core/Combat/CombatIdentity.cs:36` |
| `CombatLoadout(WeaponId Weapon, ArmorId Armor, ShieldId Shield)`, authoritative state | `src/Hukbo.Core/Combat/CombatIdentity.cs:58` |
| `shieldMultipliers` maps `ShieldId` to a `TargetWeightProfile`; `TallHardwood` sets `Chest = 500` and `Abdomen = 500` against a `DefaultMultiplierBasisPoints = 1_000` baseline | `src/Hukbo.Core/Combat/PhilippineCombatPreset.cs:21`, `:108` |
| `BuildEffectiveWeightTables()` produces a `Dictionary<(WeaponId, ShieldId), EffectiveWeightTable>` over the full cross product of weapon-target keys and shield-multiplier keys | `src/Hukbo.Core/Combat/CombatRuleset.cs:192` |
| `ResolveEffectiveWeights(weapon, shield)` is `internal` and is read once per accepted attack | `src/Hukbo.Core/Combat/CombatRuleset.cs:155` |
| `HitLocationResolver.Resolve` takes both loadouts and documents that only the **attacker's** `Weapon` and only the **defender's** `Shield` affect the result | `src/Hukbo.Core/Combat/HitLocationResolver.cs:19`, `:60` |
| `ComputeContentHash()` already hashes every shield identity, its full multiplier profile, and every roster loadout's shield | `src/Hukbo.Core/Combat/CombatRuleset.cs:265`, `:278` |
| `AgentState` already carries **per-agent** `AttackRangeRaw`, `DamagePerAttack`, and `AttackCooldownTicks`, and all three are already hashed | `src/Hukbo.Core/Determinism/StateHasher.cs:49`-`:51` |
| `StateHasher` already hashes `rules.ContentHash` and `scenario.CombatPreset` | `src/Hukbo.Core/Determinism/StateHasher.cs:32`-`:33` |
| `CombatPresetRegistry` is an exhaustive switch that throws on an unregistered id | `src/Hukbo.Core/Combat/CombatPresetRegistry.cs` |
| `Scenario.RosterCounts` defaults to empty, meaning round-robin `ResolveLoadout`; when non-empty, `Validate()` requires its length to equal `rules.Roster.Count` | `src/Hukbo.Core/Simulation/Scenario.cs:65`, `:214` |
| `RosterCountExpansion` expands counts by **roster index**, not by weapon | `src/Hukbo.Core/Combat/RosterCountExpansion.cs:20` |
| The agent inspector already prints a shield line: `Shield: Tall Hardwood` | `src/Hukbo.Client/UI/AgentInspectorContent.cs:107`, `:120` |
| `ClientSettings.ArmyComposition` has **four named integer properties** and an `IsValid()` that sums exactly those four | `src/Hukbo.Client/Settings/ClientSettings.cs:15`, `:37` |
| `ArmyCompositionPanel.CategoryLabels` is a hard-coded four-element list; the panel's own `ArmyComposition.CategoryCounts` is a variable-length `ImmutableArray<int>` | `src/Hukbo.Client/UI/ArmyCompositionPanel.cs:99`, `:21` |
| Nothing in `Hukbo.Client` draws a shield. The only shield-adjacent presentation code is the `BodyPart.ShieldArm` label, a blood-offset case, and an audio hit-class case | `src/Hukbo.Client/Presentation/BattleEventFormatter.cs:66`, `src/Hukbo.Client/Rendering/BloodGeometry.cs:404`, `src/Hukbo.Client/Audio/HitClass.cs:60` |

The single most consequential item on that list is the `AgentState` row. Because
damage, reach, and cooldown are **already per-agent and already hashed**, the
stat-variant layer needs no new authoritative field. It resolves at spawn and
writes different values into fields that already exist. Section 11 draws out what
that means for the hashes.

## 3. Two different (weapon, shield) products

`CombatRuleset` already has a table keyed on `(WeaponId Weapon, ShieldId Shield)`.
It is tempting to read that as "the pair table" and to hang the new stat variants
off the same dictionary. That would be wrong, and the reason is worth stating
before any of the design decisions, because every decision below depends on it.

The existing table's pair is **cross-agent**:

- `WeaponId` is the **attacker's** weapon.
- `ShieldId` is the **defender's** shield.

`HitLocationResolver.Resolve` documents this explicitly and its call site passes
`attacker.Weapon` and `defender.Shield` into `ResolveEffectiveWeights`. The two
components of that key come from two different warriors. All twelve combinations
of four weapons and three shields are legitimate there, including combinations
that no roster entry could ever hold, because a two-handed Kampilan wielder can
certainly attack a shield-bearing Kalis wielder.

The new table's pair is **same-agent**:

- `WeaponId` is the warrior's own weapon.
- `ShieldId` is the warrior's own shield.

Only pairs that a roster entry actually holds are meaningful here. A
`(Kampilan, TallHardwood)` stat variant describes a warrior who cannot exist,
because the Kampilan is two-handed.

These are two different products that happen to share a key type. They must be
two separate tables with two separate names, and the design uses:

- `ResolveEffectiveWeights(attackerWeapon, defenderShield)` — existing, unchanged.
- `ResolveEffectiveAttributes(ownWeapon, ownShield)` — new.

Anyone who merges them will produce a system in which a defender's shield changes
the attacker's damage. That is not a balance mistake; it is a wrong answer that
would be extremely hard to see in a state hash.

## 4. Decision one — the shield roster

### 4.1 What the evidence supports

`docs/research/HISTORICAL_1500s_WEAPONS.md` describes three defensive silhouettes
under "Defensive equipment": a tall curved shield inspired by the
late-sixteenth-century Boxer Codex Cagayan warrior, a narrow breast-high shield
suited to spear infantry and supported by Spanish accounts of shields or bucklers
approaching breast height, and "small or no shield" reserved for archers,
blowgunners, arquebusiers, and artillery crew.

Hukbo has no ranged or crew-served weapons. The third category therefore has no
roster to attach to, and the existing `ShieldId.None` already covers "no shield"
adequately. That leaves two shield forms with a role to play.

The shield **class** is well attested. Pigafetta records shields in use at Mactan
in 1521, the 1569–1576 relations catalogued in the research document list shields
among the common equipment, and the Boxer Codex supplies late-century pictorial
evidence for a tall form. None of that is in question. What is in question is
whether a Filipino **name** can be attached to either form under the rule that
the V2 design made load-bearing: *a name whose earliest attestation postdates the
depicted period by more than a century is not used at all.* That rule is what
excluded the panabas, and it must bite equally here or it is decoration.

### 4.2 The roster

| Enum identity | Player label | Evidence tier | Basis |
| --- | --- | --- | --- |
| `None = 1` (unchanged) | None | Not applicable | Absence of equipment, not a reconstruction of anything. |
| `TallHardwood = 2` (unchanged numeric value, relabelled) | Kalasag — Tall Hardwood Shield | **Documented, form uncertain**; the *name* is a provisional attachment pending the confirmation in section 13 | The tall shield form is documented: shields appear in the 1521 Mactan account and across the 1569–1576 relations, and the Boxer Codex Cagayan warrior supplies a late-sixteenth-century tall curved silhouette. *Kalasag* is the general Tagalog and Visayan word for a shield and is the strongest candidate for a period-plausible name, but the specific early-vocabulary entry could not be confirmed from sources reachable during this design pass. |
| `NarrowBreastHigh = 3` (new, appended) | Narrow Breast-High Shield | **Documented, form uncertain**; deliberately **unnamed** | The research document records Spanish accounts of shields or bucklers approaching breast height. No distinct period-attested Filipino term for this specific narrower form was identified, and the general word for "shield" is already spent on the tall form. This entry therefore carries a plain descriptor only. |

### 4.3 Why the narrow shield has no Filipino name

This is not an oversight and it should not be corrected by finding a plausible
word. There is precedent inside the repository: the research document's
recommended armory lists **Hardened Javelin**, **Broad Dagger**, **Blowgun**,
**Imported Arquebus**, and **Bronze Verso** with no Filipino name at all, beside
**Bangkaw — Long Spear** and **Busog — War Bow** which do carry one. The pair
form is used where the evidence supports a name and withheld where it does not.
Applying the same restraint to a shield form is consistency, not omission.

Naming this one anyway would require either reusing *kalasag* for a second,
visually distinct object — which asserts that the tradition distinguished the two
forms lexically, something no source consulted here supports — or reaching for a
term whose attestation is later than the period. The V2 design paid a real cost
to keep that rule intact, redesigning a weapon silhouette and renaming a family
of sound files rather than badge a three-century gap as `PROVISIONAL`. Spending
that cost and then quietly relaxing the rule for a shield would waste it.

### 4.4 Two candidates considered and not used

**A small round shield or buckler.** *Taming* is a shield term of Malay origin
that appears in the Philippine lexical record, and a small buckler would pair
naturally with the fast Itak. It is not used, for two independent reasons. The
research document assigns the small-shield category to ranged and crew-served
roles that Hukbo does not have, so there is no role for it to fill. And its
attestation path runs through a loanword whose Philippine entry date could not be
established in this pass — exactly the kind of uncertainty the panabas rule
exists to catch. If it is wanted later, it is an appended `ShieldId = 4` and a
new preset version, which is cheap. It is not wanted now.

**A hide or rattan shield as a separate identity.** The research document lists
cotton, rattan, hide, and dark wood as attested defensive materials, but it lists
them as *materials*, under armor, not as distinct shield forms. Turning a material
into a `ShieldId` would multiply the roster without adding a distinguishable
silhouette, and the design has no mechanism by which material rather than form
changes an attribute. Deferred with no expectation of return.

### 4.5 Effect on the existing targeting table

Adding one shield grows `BuildEffectiveWeightTables` from four weapons × two
shields to four weapons × three shields, that is from eight precomputed tables to
twelve. Each is thirteen `ulong` weights plus a total, built once at construction.
This is not a cost worth discussing further, and `ValidateResolvedTotals` keeps
its existing guarantee that every one of the twelve resolves to a positive total.

The new shield needs a targeting multiplier profile of its own. Provisional
gameplay tuning, not a measurement:

| Body part | `TallHardwood` (existing, unchanged) | `NarrowBreastHigh` (new) |
| --- | ---: | ---: |
| Chest | 500 | 700 |
| Abdomen | 500 | 1,000 |
| Everything else | 1,000 | 1,000 |

A breast-high shield covers the chest and leaves the abdomen exposed; a tall
shield covers both. The numbers are chosen to make that difference visible in the
event feed's body-part text over the course of a battle, and for no other reason.

## 5. Decision two — how a shield modifies weapon attributes

### 5.1 The decision

**Basis-point multipliers out of 10,000 for the four quantities with enough
magnitude to carry a percentage, and signed integer deltas for the three small
ordinals measured in ticks or steps.** Rounding is half away from zero, applied
once at ruleset construction, never at runtime.

| Attribute | Form | Unit |
| --- | --- | --- |
| `DamagePerAttack` | multiplier | basis points out of 10,000 |
| `AttackRangeRaw` | multiplier | basis points out of 10,000, applied to the raw fixed-point value |
| `ComboOpenChance` | multiplier | basis points out of 10,000 |
| `ComboContinueChance` | multiplier | basis points out of 10,000 |
| `AttackCooldownTicks` | signed delta | ticks |
| `ComboCooldownTicks` | signed delta | ticks |
| `ComboMaxSteps` | signed delta | steps |

This is a hybrid, and a hybrid needs a better justification than taste.

### 5.2 Why not a pure multiplier

Work the arithmetic on the V3 values. The Kalis has an `AttackCooldownTicks` of
5. A ten per cent slowdown is 5 × 11,000 / 10,000 = 5.5 ticks. Truncated, that is
5 — no change at all. Rounded half away from zero it is 6, a twenty per cent
slowdown. There is no multiplier between 10,000 and 11,000 that expresses
anything other than "no change" or "one whole tick," because the quantity has no
resolution below one tick. The same holds for `ComboCooldownTicks`, whose V3
values run from 2 to 5, and worse for `ComboMaxSteps`, whose values run from 2 to
5 and where a twenty-five per cent reduction of 2 is a rounding decision rather
than a design decision.

A multiplier on those three quantities does not express a tuning intent. It
expresses a rounding rule wearing a tuning value's clothes. A signed tick delta
says exactly what it means: one tick slower.

### 5.3 Why not a pure additive delta

The opposite failure. `DamagePerAttack` across the V3 roster runs 8, 10, 15, 18.
A flat −2 damage is an eleven per cent cut to the Wasay and a twenty-five per cent
cut to the Itak. Any single delta is either negligible at the top of the range or
crippling at the bottom, and authoring a different delta per weapon is just a per
pair override table with extra steps. Reach has the same problem in reverse: it is
stored as a raw fixed-point value where `12 * FixedPoint.Scale` is 3,072, so an
additive delta is authored in units nobody reasons in.

### 5.4 Why not a full per-pair override

An override table decouples the shield from the weapon. Retuning the Kampilan's
damage would then silently fail to propagate to any shielded Kampilan variant,
and the two values would drift until someone noticed. A multiplier means a weapon
retune carries into every variant of that weapon automatically, which is the
behaviour a tuning pass wants. Overrides also cost the most to author — seven
numbers per pair with no default to fall back on.

Overrides are still available, but as a *sparse* second layer on top of the
shield's default profile, not as the primary form. Section 6 uses them.

### 5.5 Rounding, floors, and where the multiply happens

- **Rounding is half away from zero**, applied once, when the ruleset is
  constructed. The results are stored in the pair table. Runtime performs no
  division and no rounding, so there is no per-tick fixed-point cost and no
  opportunity for a rounding difference to appear mid-battle. This mirrors what
  `BuildEffectiveWeightTables` already does for targeting weights, and it is a
  precomputed immutable table rather than a cache — it never changes after
  construction and is therefore not subject to the "no unbounded cache" rule.
- **`DamagePerAttack` and `AttackCooldownTicks` clamp to a minimum of 1.** A zero
  cooldown is an infinite attack rate and a zero damage is an unresolvable
  stalemate. Both are validation errors at construction, not silent clamps — the
  ruleset throws, because a preset that authors them is wrong and should not
  build.
- **`ComboMaxSteps` clamps to a minimum of 1** for the same reason: zero would
  mean "a chain that cannot contain a blow," which is not a state V3's
  `ComboStepsRemaining` is defined over.
- **The V2 reach floor must be re-asserted on the effective value.** V2 section
  3.4 establishes that any weapon whose reach is at or below `2 * BodyRadiusRaw`
  — eight world units, 2,048 raw — can never strike a warrior it stands against.
  A shield reach multiplier can push a weapon under that floor. The Itak's V3
  reach of 10 world units is 2,560 raw; a multiplier of 7,500 would yield 1,920
  raw and produce a warrior who advances into contact and can never attack. The
  invariant test V2 requires must therefore run against
  `ResolveEffectiveAttributes` for every legal pair in every registered preset,
  not against the weapon's base reach.

### 5.6 Table size is bounded by the roster, not by the cross product

The user's concern about `weapons × shields` growing fast is real for a table
authored over the cross product. This design does not author over the cross
product. Two layers:

1. **A default profile per shield** — seven values. `ShieldId.None` is the
   identity (every multiplier 10,000, every delta 0) and is not authored at all;
   a test asserts it resolves to values exactly equal to the weapon's own.
2. **Sparse per-pair overrides**, authored only for pairs a roster entry actually
   holds and only for attributes where that pair genuinely differs from the
   shield's default.

Total hand-authored numbers at V4: fourteen for two shield profiles, plus two
overrides. Sixteen numbers. The bound is O(shields × 7 + overrides), and
overrides are bounded by the roster length, not by the cross product. A test
asserts that no override exists for a pair no roster entry holds, which keeps dead
tuning data from accumulating and keeps the authored set honest as the roster
changes.

Everything stays hand-authored explicit data, per the `PhilippineCombatPreset`
remarks. No reflection, no deserialization.

## 6. The V4 attribute tables

All values in this section are **provisional gameplay tuning values**. They are
not measurements, they are not historical claims, and nothing here may be cited
back into `docs/research/HISTORICAL_1500s_WEAPONS.md` or shown to a spectator as
a historical fact. What justifies them is the physical character of carrying a
shield in the off hand — a constrained follow-through and an occupied arm — not
any source on how a sixteenth-century warrior fought.

### 6.1 Shield default profiles

| Attribute | Form | `Kalasag — Tall Hardwood Shield` | `Narrow Breast-High Shield` |
| --- | --- | ---: | ---: |
| `DamagePerAttack` | multiplier | 9,000 | 10,000 |
| `AttackRangeRaw` | multiplier | 9,500 | 10,000 |
| `AttackCooldownTicks` | delta | +1 | 0 |
| `ComboOpenChance` | multiplier | 10,000 | 10,000 |
| `ComboContinueChance` | multiplier | 7,000 | 8,800 |
| `ComboMaxSteps` | delta | −1 | −1 |
| `ComboCooldownTicks` | delta | +1 | +1 |

Two of those entries deserve their reasoning written down.

**The narrow shield's damage multiplier is 10,000, not something like 9,700.**
An earlier draft used 9,700, a three per cent cut. Applied to the roster's damage
values of 8, 10, 15, and 18 and rounded half away from zero, that yields 8, 10,
15, and 17 — a visible effect on exactly one weapon and none on the other three,
and the one effect it does have is a six per cent real cut from a three per cent
nominal one. A multiplier whose rounded effect is nil at almost every value it
will ever be applied to is not a tuning value; it is decoration that will mislead
the next person who reads the table. The light shield's cost is expressed where
the quantity has the resolution to carry it: continuation chance.

**Neither shield touches `ComboOpenChance`.** The physical story a shield tells is
about follow-through, not initiation — an occupied off hand constrains the
recovery and the body rotation between blows, not the decision to swing. Keeping
the opening chance untouched also keeps the two probabilities separable during
tuning, so a benchmark can attribute a change in chained-blow fraction to the
continuation term alone.

### 6.2 Per-pair overrides

Two, both live in the V4 roster.

| Pair | Attribute | Override | Reasoning |
| --- | --- | ---: | --- |
| `(Kalis, TallHardwood)` | `AttackRangeRaw` multiplier | 10,000 instead of 9,500 | A thrust is linear and passes the shield's edge; a cut has to travel around it. The tall shield costs a cutting weapon reach and costs a thrusting weapon none. |
| `(Itak, NarrowBreastHigh)` | `ComboCooldownTicks` delta | 0 instead of +1 | A breast-high shield leaves the arms free. Its cost to a short blade is chain *length* and chain *survival*, not chain *cadence*. |

This is the pair table doing the work the flat-multiplier design could not: the
same tall shield costs the Kalis nothing in reach and the Itak five per cent,
because the two weapons attack along different lines.

### 6.3 The V4 roster and its resolved attributes

V4's roster keeps four entries — see section 10 for why that matters to the
client — and re-attaches shields that V3 held at `None`:

| Roster index | Weapon | Shield |
| ---: | --- | --- |
| 0 | Kampilan — Great Blade | None (two-handed) |
| 1 | Wasay — War Axe | None (two-handed) |
| 2 | Kalis — Thrusting Blade | Kalasag — Tall Hardwood Shield |
| 3 | Itak — Work Blade | Narrow Breast-High Shield |

Resolved effective attributes, computed from the V3 base table in that design's
section 4:

| Roster entry | Damage | Reach | Cooldown | Open | Continue | Max steps | Combo cooldown |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Kampilan, no shield | 15 | 16 | 7 | 2,000 | 3,000 | 2 | 4 |
| Wasay, no shield | 18 | 13 | 8 | 1,000 | 2,000 | 2 | 5 |
| Kalis + Kalasag | 9 | 12 | 6 | 3,500 | 3,150 | 3 | 4 |
| Itak + Narrow Breast-High | 8 | 10 | 4 | 4,500 | 4,840 | 4 | 2 |

Reach is shown in world units; the stored value is raw. The Itak's reach is
unmodified at 2,560 raw, which clears the 2,048-raw floor. The Kalis's reach is
unmodified at 3,072 raw by the override in section 6.2. **No V4 pair currently
sits close to the reach floor**, which is a comfortable position to be in and
also the reason the invariant test matters — nothing today would catch a future
authoring mistake.

The shape this produces is legible. The Kalis pays real damage and a whole tick
of recovery for the tall shield's chest-and-abdomen protection, and its chains
get shorter and slower. The Itak pays almost nothing in raw output for the narrow
shield — the same damage, the same reach, the same cadence — and pays instead in
chain survival, which is the attribute V3 built the Itak's identity around. A
spectator who learns that the Itak is "the one that keeps going" now learns that
the shielded Itak keeps going slightly less.

### 6.4 The level placeholder makes the max-steps column inert

V3 caps chain length at `min(agent.Level, weapon.ComboMaxSteps)` with
`Scenario.PlaceholderFighterLevel` defaulting to 1, and V3's own section 4 notes
that its `Max steps` column is therefore entirely inert. The same is true of
every `ComboMaxSteps` delta in this design. At level 1 no chain exceeds one
follow-up blow regardless of what the pair table says.

The deltas are authored now for the same reason V3 authored the column: so the
ceiling exists before a leveling system needs it, and so a headless level sweep
has something to reveal. They must not be counted as an observable V4 effect, and
section 9 does not count them.

## 7. Decision three — shields and combinations

### 7.1 The decision

**Not orthogonal.** A shield modifies combination attributes through the same
pair table as every other attribute: it lowers continuation chance, lengthens
combination cooldown, and lowers the maximum step ceiling. It does **not** touch
opening chance, and it does **not** become a fifth chain-break condition.

V3 section 9 left this open deliberately and asked for an explicit answer. The
answer is that combinations are exactly where a shield's cost should live.

### 7.2 Why the cost belongs here

V3's balance shape inverts V2's: heavy two-handed weapons commit to single large
blows, and light weapons chain. Under V2 the light weapons were also the shielded
ones, which V3 calls out as a temporary regression. If a shield's cost were
expressed only in damage and cooldown, V4 would restore the V2 shape — the light
weapons would be both the shielded ones and the chaining ones, which is two
advantages stacked on one archetype and one fewer axis of variation than V3
worked to create.

Routing the cost through continuation chance instead means the shield is a real
choice for exactly the archetypes that can afford to make it. The Itak trades
some of the thing it is best at for protection on the part of the body a shield
covers. That is a trade a spectator can name.

### 7.3 What this does not do

The chain-break list in V3 section 3.2 stays at four conditions: a failed
continuation roll, the maximum length, target death, and the target leaving
reach. A shielded **defender** does not break an attacker's chain.

Adding a defender-side break condition would couple chain state to the defender's
loadout at the moment the chain resolves, which re-opens precisely the interrupt
question V3 deferred on the grounds that it couples combinations to damage
resolution ordering. It would also mean a shield modifies both its carrier's own
attributes and other warriors' chain state, collapsing the clean separation
section 3 establishes. Deferred, and this design recommends it stay deferred until
V3's combination system has run in a real battle.

### 7.4 The V3 seam holds

No change to `MixCombo`, no new random draw, and no change to the number of draws
per resolved attack. A shield changes the **threshold** a roll is compared against,
not the roll. V3's requirement that the draw include `comboStepsRemaining` and a
named constant salt is untouched. This matters for determinism: adding a draw
would shift the whole downstream draw sequence for every agent, whereas changing a
comparison threshold changes only outcomes that flow from that comparison.

## 8. Decision four — two-handed weapons

### 8.1 The decision

**A hard rule enforced in code**, not a roster convention.

`WeaponId.Kampilan` and `WeaponId.Wasay` gain a two-handed flag on the weapon
attribute record V2 introduces. `CombatRuleset` validates at construction that:

1. every roster entry whose weapon is two-handed carries `ShieldId.None`; and
2. no per-pair stat-variant override is authored for a two-handed weapon paired
   with any shield other than `None`.

Both are `ArgumentException` throws at construction. A preset that violates
either does not build.

### 8.2 Why a hard rule rather than a convention

V2 made the Kampilan and Wasay shieldless as a roster convention, and under V2
that was adequate because a roster entry was the only place a shield could
appear. This design changes that. Once a `(weapon, shield)` stat-variant table
exists, `(Kampilan, TallHardwood)` becomes an *authorable* row. A convention that
nothing enforces will be violated the first time someone tunes the table without
reading this document, and the failure mode is a warrior who exists in the pair
table but cannot exist in the world — dead data that a reader will reasonably
mistake for intent.

The check costs a loop at construction, runs once per preset, has no runtime cost,
cannot affect a hash, and turns a documentation dependency into a build failure.

### 8.3 What the rule does not touch

The rule applies to the **roster** and to the **stat-variant table**. It must not
be applied to `BuildEffectiveWeightTables` or `ValidateResolvedTotals`, both of
which iterate the full weapon × shield cross product and are correct to do so:
their pair is attacker-weapon against defender-shield, and a two-handed Kampilan
wielder attacking a shield-bearing defender is an entirely ordinary event. All
twelve targeting tables remain required. Section 3 is the reason.

The two-handed flag becomes preset data and therefore enters `ContentHash`.

## 9. Spectator discoverability

`SIMULATION-GAME-STANDARDS.md` section 10 asks whether a spectator can discover
this effect without reading source code. Naming the exact surfaces:

**The agent inspector is the primary confirmation surface.** The shield line
already exists at `AgentInspectorContent.cs:107` and prints `Shield: Tall
Hardwood`. V4 changes it to the pair form and adds the shield's contribution to
the weapon line as an explicit delta, so the inspector reads something like:

```
Weapon: Itak — Work Blade
        8 dmg / 10 reach / 4 tick recovery
Shield: Narrow Breast-High Shield
        chain continue 5,500 → 4,840, chain steps 5 → 4
Combo:  open 45.0%, continue 48.4%, max 4 steps, 2 tick cadence
```

A spectator clicks a shielded warrior and an unshielded warrior of the same
weapon in sequence and reads the difference. This is the surface that makes the
feature discoverable in the strict sense, and it is not optional.

**The event feed shows the targeting shift.** `BattleEventFormatter` already
prints the struck body part — `hit #7's neck with Itak — Work Blade for 8` — so a
shielded defender's chest and abdomen hits being rarer is directly legible in the
feed text over a battle. This surface already works today and needs no change.

**The event feed shows shorter chains.** V3 prints a chain position as
`(combo 2)`. A shielded warrior's chains end sooner and their chained blows
arrive on a longer combination cooldown, both of which are visible as fewer
consecutive appearances of the same attacker against the same target.

**The event feed shows the Kalis's cadence loss.** One whole tick of extra
recovery on a five-tick weapon is a twenty per cent slowdown, visible as the same
warrior reappearing in the feed less often.

**A gap that must be closed, and is not optional.** Nothing in `Hukbo.Client`
draws a shield. `PawnGeometry` and `PawnAppearance` know about weapon roles and
nothing about `ShieldId`; the only shield-adjacent presentation code is a body
part label, a blood offset, and an audio hit class. The research document's
visual grammar table already specifies the intended silhouette — "Shield bearer:
tall solid block beside the torso" — and it has never been implemented.

Without it, a spectator watching the battlefield cannot tell which warriors carry
shields at all, and must click each one to find out. Every surface above except
the inspector describes an effect the spectator can *see* but cannot *attribute*,
because the cause is invisible. That fails section 10's question, so **the shield
silhouette is a required task in the V4 plan, not a polish item.** Two silhouettes
are needed, distinguishable at battle scale: a tall block for the kalasag and a
shorter, narrower one for the breast-high shield.

**Honest limits.** Two effects in this design are not discoverable from the
screen, and the plan should not claim otherwise:

- The narrow shield's continuation-chance cost — 55.0% down to 48.4% — is a
  6.6-point difference in a probability. No spectator distinguishes that by
  watching. It is discoverable only through the inspector's printed number.
- Every `ComboMaxSteps` delta is entirely inert at `PlaceholderFighterLevel = 1`,
  per section 6.4. The inspector should still print it, for the reason V3 gives
  for printing the level.

## 10. Client settings and the army composition panel

This section answers the explicit question. **Yes, both need shield-aware
changes, but under the recommended roster shape the changes are labels only and
no settings migration is required.** The reasoning is structural rather than
cosmetic, and it turns entirely on whether V4's roster stays at four entries.

The constraint chain, verified in section 2:

1. `Scenario.Validate()` requires `RosterCounts.Length == rules.Roster.Count`
   whenever `RosterCounts` is non-empty.
2. `RosterCountExpansion.Expand` expands by **roster index**, so a "category" in
   the client is a roster entry, not a weapon.
3. `ClientSettings.ArmyComposition` is four **named** integer properties —
   `GreatBladeCount`, `HeavyChopperCount`, `ThrustingBladeCount`,
   `WorkBladeCount` — and `IsValid()` sums exactly those four against
   `UnitsPerTeam`.
4. `ArmyCompositionPanel.CategoryLabels` is a hard-coded four-element list.
5. The panel's own `ArmyComposition.CategoryCounts` is an `ImmutableArray<int>`
   and is already variable-length-ready. The panel is not the problem; the
   persisted settings record and the label list are.

Two possible roster shapes follow from that.

**Option A — the roster stays at four entries, each with a fixed shield.**
Recommended. This is what section 6.3 authors, and it treats a shield as a
property of a roster archetype rather than as an axis the player composes along.
Required client changes:

- `ArmyCompositionPanel.CategoryLabels` gains the shield in each label, so a
  spectator choosing a composition knows what they are fielding — for example
  `Kalis — Thrusting Blade (Kalasag)` and `Itak — Work Blade (Breast-High)`.
- `ClientSettings.ArmyComposition` property names change as V2 already requires
  for the weapon renames. No arity change, no `SchemaVersion` bump beyond
  whatever V2 already decided, and no new migration question.

**Option B — the roster grows to expose shielded and unshielded variants of the
same weapon as separate categories.** Not recommended for V4. This is the natural
follow-up once the stat-variant layer has been measured, and it is where the
design becomes genuinely interesting for a player, but it costs a
`ClientSettings.SchemaVersion` bump, converting four named properties into a
collection, rewriting `IsValid()` to sum N, growing `CategoryLabels`, growing the
panel's stepper layout, and re-opening the persisted-settings reset question that
V2 section 5 already had to decide once. Deferring it keeps the V4 diff to the
simulation layer plus a silhouette and a label pass.

## 11. Determinism

This is a hash-moving change and must follow the `hukbo-determinism-change`
skill.

### 11.1 New preset version

`CombatPresetId.PrecolonialPhilippinesV4 = 4`, appended, with a new
`CombatPresetRegistry` switch arm. V1, V2, and V3 stay in place unmodified so
their replays remain reproducible. New golden expectations are recorded for the
seed-1 baseline against V4; the V1, V2, and V3 goldens are kept, not edited.

`ShieldId.NarrowBreastHigh = 3` is appended. `None = 1` and `TallHardwood = 2`
keep their numeric values. Nothing is renumbered or reordered.

### 11.2 The ruleset content hash moves

`CombatRuleset.ComputeContentHash` already hashes every shield identity, every
shield's full multiplier profile, and every roster loadout's shield. V4 changes
all three: a new shield identity, a new multiplier profile, and roster entries
that carry shields where V3 carried `None`. The stat-variant table and the
two-handed flags are new preset data and must be added to `ComputeContentHash`
in the same deterministic order the existing code uses — keys sorted ascending by
numeric enum value, body parts and attributes iterated in a fixed catalogue
order, never dictionary enumeration order.

### 11.3 The state hash moves

`StateHasher` mixes `scenario.CombatPreset` and `rules.ContentHash` directly, so
the state hash moves at tick zero from the preset change alone.

It also moves for a second, independent reason. Per-agent `DamagePerAttack`,
`AttackRangeRaw`, `AttackCooldownTicks`, and `Loadout.Shield` are all hashed
per agent, and V4 writes different values into all four for the shielded roster
entries. From there the entire trajectory diverges: different cooldowns change
who attacks on which tick, which changes hit points, which changes target
selection and death order, which changes positions.

**No new authoritative field is required and `StateHasher`'s layout does not
change.** This is the payoff from the `AgentState` observation in section 2: the
per-agent attribute fields already exist and are already hashed, so the
stat-variant layer resolves at spawn and writes into them. V3's
`AgentState.Level` and `AgentState.ComboStepsRemaining` also already exist by the
time V4 lands; `ComboStepsRemaining` is simply initialised from the
pair-resolved maximum instead of the weapon's. The plan should still assert the
absence of a layout change with a test rather than assuming it.

### 11.4 The event hash moves

Attack events carry damage values, hit locations, and — after V3 — a chain
position. V4 changes all three for shielded warriors: lower damage, a targeting
distribution shifted by the new shield's multiplier profile, and shorter chains.
The ordered event stream diverges, so the event hash moves.

### 11.5 What does not move and must not

- **No new random draw and no change to draw count per attack.** A shield changes
  a comparison threshold, not a roll. `HitLocationResolver.MixAttack` and V3's
  `MixCombo` are untouched.
- **The pinned SplitMix64 test vectors are not edited.** `System.Random` remains
  banned.
- **No new tick stage.** Attribute resolution happens at spawn; combination
  resolution stays inside V3's existing attack stage. The tick stage order in
  `SIMULATION-GAME-STANDARDS.md` section 13 does not change.
- **Total ordering is unchanged.** Agents are iterated in ascending `EntityId`,
  and the new tables are keyed lookups with no iteration that could reach
  gameplay. Where the new preset code must iterate a dictionary — content
  hashing and validation — it sorts by numeric enum value first, matching the
  existing `.OrderBy(id => (int)id)` pattern.
- **Fixed-point arithmetic only.** The reach multiplier is applied to the raw
  fixed-point value with integer arithmetic and rounded once at construction.
- **No per-tick allocation.** Tables are built once at construction; resolved
  values are copied into existing `AgentState` fields at spawn.

### 11.6 Complexity

Construction is O(shields × attributes + overrides) for the stat-variant table
and O(weapons × shields × body parts) for the targeting tables, which is twelve
tables of thirteen entries at V4. Runtime is O(1) per attack — one keyed lookup
that already exists for targeting, and per-agent attribute reads that are already
field accesses.

The 200-agent contract and the reported 500-agent result are unaffected in
principle, but the plan must still report them, because V4 changes the cooldown
distribution and therefore the number of attack events per tick, which is the
main driver of per-tick event allocation.

## 12. Decision five — V4 after V3, not folded into V3

**Recommendation: ship this as preset V4, after V3 is implemented and its
combination tuning has been measured.** Do not fold shields into V3.

The case for folding is real and should be stated. It would produce one preset
version instead of two, one golden regeneration instead of two, and it would
avoid the temporary balance regression V3 section 2 openly admits — the Kalis and
Itak losing their V2 shield protection for the duration of V3.

The case against is stronger, and it is V3's own argument. V3 holds every roster
entry at `ShieldId.None` specifically so the combination mechanics can be tuned
against one variable rather than two, and V3 section 7 commits to a benchmark that
must report mean time to kill, chained-blow fraction, and realised chain length
per weapon before its probability values are locked. Folding shields in makes
every row of that benchmark a two-variable measurement. If the Itak's realised
throughput comes out wrong, nobody can say whether the continuation probability
or the shield's multiplier on it is at fault — which is exactly the diagnosis V3's
scope boundary was constructed to make possible.

There is a second ordering risk that points the same way. V2's damage, reach, and
cooldown values have never run in a battle, by V3's own admission. V4 multiplies
those unvalidated numbers. Stacking an unmeasured multiplier on an unmeasured base
means a bad outcome has two candidate causes and no way to separate them.

Two conditions attach to this recommendation:

1. **V3's tuning values must not be locked in a way that resists a V4 retune.**
   V4 changes the effective throughput of two of four roster entries. If V3's
   benchmark is treated as final, V4 will have to re-run it anyway. The plan
   should record V3's numbers as measured-and-provisional, and schedule the same
   benchmark against V4 with the shielded entries included.
2. **The V3 regression must stay visible.** V3 section 2 states it plainly. The
   plan for V3 should carry a pointer to this document so that the regression is
   understood as scheduled work rather than as an accepted balance decision.

## 13. Historical accuracy notes

Per CLAUDE.md section 7, every claim carries a tier.

- **Documented.** Shields were in use in the sixteenth-century Philippines.
  Pigafetta records them at Mactan in 1521, and the relations of 1569–1576
  catalogued in `docs/research/HISTORICAL_1500s_WEAPONS.md` list shields among
  common equipment. This is not in doubt.
- **Documented, form uncertain.** A tall shield form is supported by the Boxer
  Codex Cagayan warrior. A narrower breast-high form is supported by Spanish
  accounts of shields or bucklers approaching breast height. In both cases the
  class is attested and the exact local construction, proportion, and regional
  distribution are not.
- **Provisional attachment, pending confirmation.** *Kalasag* as the name for the
  tall form. The word is the general Tagalog and Visayan term for a shield and is
  the strongest candidate available, but this design pass was read-only on code
  and did not perform external source research, so the specific early-vocabulary
  entry — source, edition, and page — was not confirmed. The V2 design set the
  precedent for exactly this situation when it left the Itak at *Provisional
  reconstruction* rather than claim an unconfirmed 1613 attestation. Section 14
  carries the confirmation as an open question, and the design is built so that
  withdrawing the name costs one constant: the label falls back to
  `Tall Hardwood Shield` and nothing else changes.

Three framing rules, restated because they bind:

- **Spanish accounts are evidence about equipment, not neutral ethnography.**
  Every shield description used here comes from an observer writing inside a
  colonial encounter, often at the moment of being fought. That makes the
  accounts good evidence that shields existed and roughly what shape they were,
  and poor evidence about how they were used, who used them, or what they meant.
- **The Boxer Codex guides silhouette and colour, not technical cataloguing.**
  Its Chinese pictorial influences and European patronage mean the Cagayan
  warrior's shield supports "tall and curved" and does not support any statement
  about construction, layering, or dimensions.
- **Do not generalise one region or decade to "the Philippines".** The tall form
  evidence is late-century and northern Luzon; the breast-high evidence is from
  Spanish accounts of other encounters. Two shields in a roster are two plausible
  forms drawn from different places and decades, not a survey of archipelagic
  practice. The player-facing labels must not imply otherwise, and the inspector's
  evidence line should carry the tier the way V2 specifies for weapons.

Every number in sections 4.5, 6.1, 6.2, and 6.3 is a **provisional gameplay
tuning value**, must be marked as such in code comments and tests exactly as the
existing `TallHardwood` multipliers are, and must never be presented as a
historical measurement.

## 14. Surface to change

Recorded so a plan document can assign non-overlapping file ownership. Paths that
V2 or V3 also touch are marked, because V4 must land after both and its plan must
not assume V2's or V3's version of a file.

### Core

| File | Change |
| --- | --- |
| `src/Hukbo.Core/Combat/CombatIdentity.cs` | Append `ShieldId.NarrowBreastHigh = 3`. Append `CombatPresetId.PrecolonialPhilippinesV4 = 4`. Renumber nothing. *(also touched by V2, V3)* |
| `src/Hukbo.Core/Combat/` weapon attribute record (introduced by V2) | Add the two-handed flag. *(V2 file)* |
| `src/Hukbo.Core/Combat/ShieldStatVariant.cs` | **New.** The multiplier-and-delta record, its identity value, and the sparse per-pair override table type. |
| `src/Hukbo.Core/Combat/CombatRuleset.cs` | Carry the stat-variant table. Add `ResolveEffectiveAttributes(WeaponId, ShieldId)`. Extend `ComputeContentHash` for the stat-variant table and two-handed flags. Add the two-handed roster and override validation. Add the post-multiplier reach-floor and minimum-value validation. Keep `BuildEffectiveWeightTables` and `ValidateResolvedTotals` iterating the full cross product. *(also touched by V2, V3)* |
| `src/Hukbo.Core/Combat/PhilippineCombatPreset.cs` and the V2/V3 preset files | Add the V4 preset. V1, V2, and V3 are frozen and must not be edited. |
| `src/Hukbo.Core/Combat/CombatPresetRegistry.cs` | New switch arm for V4. *(also touched by V2, V3)* |
| `src/Hukbo.Core/Simulation/BattleSimulation.cs` | Resolve per-agent damage, reach, and cooldown through `ResolveEffectiveAttributes(ownWeapon, ownShield)` at spawn instead of through the weapon alone. Read combination attributes through the same call in the attack stage. *(also touched by V2, V3)* |
| `src/Hukbo.Core/Simulation/Scenario.cs` | No change expected — `RosterCounts` validation already derives its expected length from `rules.Roster.Count`, and the default is empty. Verify, do not assume. *(also touched by V2, V3)* |
| `src/Hukbo.Core/Determinism/StateHasher.cs` | No layout change expected. Verify with a test. |

### Client

| File | Change |
| --- | --- |
| `src/Hukbo.Client/UI/AgentInspectorContent.cs` | Pair-form shield label, a case for the new shield, the evidence-tier line, and the shield-contribution delta on the weapon and combination lines. *(also touched by V2, V3)* |
| `src/Hukbo.Client/UI/AgentInspectorPanel.cs` | Line ordering for the new lines. |
| `src/Hukbo.Client/Presentation/PawnAppearance.cs` | **Shield silhouette:** a shield role alongside the existing weapon role. |
| `src/Hukbo.Client/Presentation/PawnAppearanceFactory.cs` | Map `ShieldId` to the shield role. |
| `src/Hukbo.Client/Rendering/PawnGeometry.cs` | Shield block geometry per role, per detail tier. |
| `src/Hukbo.Client/Rendering/PawnRenderer.cs` | Draw it. |
| `src/Hukbo.Client/Rendering/BloodGeometry.cs` | Only if the shield silhouette changes a hit-flash anchor. Check, then decide. |
| `src/Hukbo.Client/Settings/ClientSettings.cs` | Label-level only under Option A in section 10. *(also touched by V2)* |
| `src/Hukbo.Client/UI/ArmyCompositionPanel.cs` | `CategoryLabels` gains the shield in each label. *(also touched by V2)* |
| `src/Hukbo.Client/Presentation/BattleEventFormatter.cs` | **No change expected.** Damage, body part, and V3's chain position are already printed. Verify. |

### Content

**No change.** No new sound slot. Shields in this design do not block or deflect
— they reweight where a blow lands and constrain the carrier's own attributes —
so there is no block event to voice. A shield-block mechanic would need its own
design document, and it would have to answer the interrupt-ordering question V3
deferred. Explicitly out of scope.

### Docs

| File | Change |
| --- | --- |
| `CLAUDE.md` section 7 | Confirm that V2's amended pair-form clause covers defensive equipment and not only weapons; extend the wording if it reads as weapon-only. |
| `docs/research/HISTORICAL_1500s_WEAPONS.md` | Extend "Defensive equipment" with the two shield entries, their tiers, and the *kalasag* name candidate with its unconfirmed status. Record in the closing cross-reference section that shield stat variants are provisional tuning values, alongside the existing note about shield multipliers. |
| `docs/development/testing.md` | New smoke rows, left `PENDING`: the shield silhouette is visible at battle scale and distinguishable between the two forms; the inspector shows the pair-form shield label, the evidence tier, and the attribute delta; the army composition panel labels name the shield. |
| `docs/plans/YYYY-MM-DD-shields.md` | The plan document this design does not substitute for. |

### Tests

| Test | Asserts |
| --- | --- |
| V4 determinism goldens | Seed-1 state hash, event hash, winner, and ordered event stream against V4. V1/V2/V3 goldens untouched. |
| Frozen-preset content hash | V1, V2, and V3 `ContentHash` each equal a pinned literal. |
| V4 content hash differs | V4 `ContentHash` differs from V3's. |
| `ShieldId.None` is the identity | For every weapon in every registered preset, `ResolveEffectiveAttributes(weapon, None)` returns values exactly equal to the weapon's own. |
| Two-handed roster invariant | Every roster entry whose weapon is two-handed carries `ShieldId.None`, in every registered preset. |
| Two-handed override invariant | No stat-variant override exists for a two-handed weapon paired with a shield. |
| No dead overrides | Every authored per-pair override corresponds to a pair some roster entry holds. |
| Post-multiplier reach floor | For every legal pair in every registered preset, effective `AttackRangeRaw` is strictly greater than `2 * Scenario.BodyRadiusRaw`. |
| Minimum-value validation | A preset authoring a pair that resolves to zero damage, zero attack cooldown, zero combination cooldown, or zero max steps throws at construction. |
| Rounding rule | Half-away-from-zero, with the worked cases from section 5.2 and 6.1 as fixtures. |
| Targeting cross product intact | All twelve `(weapon, shield)` effective weight tables exist and resolve to a positive total. |
| `StateHasher` layout unchanged | The hashed field sequence is identical to V3's. |
| Shield label exhaustiveness | `AgentInspectorContent.GetShieldLabel` handles every value of `Enum.GetValues<ShieldId>()` without throwing. |
| Inspector content | The pair-form label, the evidence tier line, and the delta line format, as pure-helper tests per `hukbo-client-ui`. |
| Pawn appearance and geometry | Shield role mapping and geometry, with no `GraphicsDevice`, `SpriteBatch`, or window. |
| Army composition labels | `CategoryLabels` length matches the V4 roster length and each label names its shield. |
| Benchmark, reported not asserted | Mean time to kill, chained-blow fraction, and realised chain length per roster entry, and faction win rate for mirrored and asymmetric rosters, per section 12 condition 1. |

## 15. Open questions

1. **Confirm or withdraw *kalasag*.** The specific early-vocabulary attestation —
   source, edition, page — was not verified in this pass. Until it is, the name is
   a provisional attachment. The plan should carry this as a research task with an
   explicit fallback: if it cannot be confirmed within the same 100-year rule that
   excluded the panabas, the label becomes `Tall Hardwood Shield` and the design
   is otherwise unchanged.
2. **Basis-point base: 1,000 or 10,000?** The existing shield targeting
   multipliers use 1,000 (`DefaultMultiplierBasisPoints`); V3's combination
   probabilities use 10,000. This design uses 10,000 for the new stat-variant
   layer, which leaves two bases in one preset file. Options are to accept the
   inconsistency with a comment, or to restate the targeting multipliers in
   10,000ths in V4 — which is a value change and therefore already covered by
   V4's hash move, but which also means the V4 targeting table stops being
   diff-comparable against V2's. Recommendation: accept the inconsistency in V4,
   and normalise in a later preset if it causes a real mistake.
3. **Rounding direction.** Half away from zero is recommended in section 5.5, on
   the grounds that truncation turns a three per cent damage multiplier into a
   twelve per cent one at damage 8. This needs a test with the worked cases as
   fixtures, and someone should sanity-check that half-away-from-zero does not
   produce a surprising result at any V4 value.
4. **Should V4 ship both shields, or only the mechanism?** There is a genuine
   YAGNI tension with section 4.2. A single-shield V4 would validate the
   stat-variant machinery with a smaller diff. Against that: a one-shield table
   cannot demonstrate that different weapons take different modifications from
   different shields, which is the entire point of a pair-keyed layer, and the
   sparse-override design would go untested. Recommendation: both shields, both
   overrides, so the mechanism is exercised by the data it exists for. Flagged
   because it is a defensible call in either direction.
5. **Does a shielded defender break an attacker's chain?** Section 7.3 says no,
   deliberately. Revisit only after V3's combination system has run in a real
   battle, and only together with the interrupt question V3 deferred.
6. **Shield silhouette at low detail tiers.** This echoes V2's open question 3
   about the axe head. At a few pixels tall, is the shield a distinct block, a
   widened torso, or absent? "Absent at the lowest tier" is defensible, but it
   weakens exactly the discoverability surface section 9 declares mandatory, so
   the answer needs to name the tier at which the shield becomes visible.
7. **Does `ShieldId.None` deserve a compensating bonus?** Identity is recommended
   — an unshielded warrior gets the weapon's own numbers and nothing more, and
   the two-handed weapons already carry higher damage and reach from V2. Raised
   only so that "no shield is the identity" is a recorded decision rather than an
   assumption.
8. **Where does the two-handed flag live?** On the weapon attribute record V2
   introduces, or as a separate preset table? The record is simpler and keeps the
   flag next to the values it constrains. A separate table would let a future
   preset make the same weapon one-handed, which is speculative. Recommendation:
   the record.
9. **Does the shield line belong before or after the weapon line in the
   inspector?** The shield now modifies the weapon's printed numbers, so the
   reading order matters more than it did. This is a `hukbo-client-ui` question
   and may need one of the 27 semantic theme roles for the delta text.

## 16. Nine questions

`SIMULATION-GAME-STANDARDS.md` section 10 requires all nine to be answered in the
document that authorizes implementation, which is the plan document and not this
one. Following the pattern V2 and V3 set, the two this design is most at risk on
are answered here in full:

**Question 1, user-visible outcome.** A warrior carrying a shield hits for less,
recovers more slowly, and chains fewer and slower follow-up blows than the same
weapon carried without one, in exchange for the existing protection on the body
parts that shield covers. The trade differs by weapon: a thrusting blade loses no
reach behind a tall shield while a cutting blade does, and a short blade with a
light shield loses chain survival while keeping its cadence.

**Question 8, spectator explanation.** Answered in full in section 9, including
the named surfaces, the two effects that are honestly not discoverable from the
screen, and the shield silhouette that section 9 upgrades from polish to a
required task.

The remaining seven — tick stage and state read or written, numeric units and
bounds and the same-tick conflict rule, total ordering and random-stream policy,
cache source or "no cache", save and event and version effect, worst-case
complexity and benchmark workload, and the tests that fail before and pass after
— have their material in sections 5, 11, and 14 and must be restated as direct
answers in the plan document.
