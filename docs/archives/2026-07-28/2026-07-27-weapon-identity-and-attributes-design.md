# Weapon Identity and Attributes — Design

> **Archived: reference only.** This plan is complete and deprecated. Do not execute it, and do not treat its steps, versions, or tooling references as current. The live contract is `CLAUDE.md` plus the skills in `.claude/skills/`.

Date: 2026-07-27

Status: design only. This document does not authorize implementation. The
ordered task list lives in the companion plan document,
`docs/plans/2026-07-27-weapon-identity-and-attributes.md`, which does not exist
yet.

## 1. What this changes and why

Hukbo's four weapons are today distinguished from one another by exactly one
thing: which body part they tend to hit. Damage, reach, and attack cooldown are
global scenario values shared by every warrior on the field. A spectator
watching a battle sees four different silhouettes swinging at four different
body parts for the same ten points of damage, at the same twelve-world-unit
range, on the same five-tick cycle. The weapons are cosmetically distinct and
mechanically identical.

Separately, the player-facing labels are deliberately generic — Great Blade,
Heavy Chopper, Thrusting Blade, Work Blade. That choice was correct when it was
made, because the alternative on offer was stamping specific and contested
cultural identifications onto units as unqualified fact. It has a cost, though:
a game about pre-colonial Philippine warfare currently shows the player no
Philippine vocabulary at all.

This design addresses both. It gives every weapon its own damage, reach, and
cooldown, and it puts a Filipino name in front of every descriptor that the
evidence can actually carry — while refusing to invent one where the evidence
cannot.

## 2. Naming

### 2.1 The form

Labels take the pair form that `docs/research/HISTORICAL_1500s_WEAPONS.md`
already uses for its recommended armory entries `Bangkaw - Long Spear` and
`Busog - War Bow`: the Filipino name leads, an em dash separates, and a plain
English descriptor follows. The agent inspector additionally shows the evidence
tier and a one-line source note.

The pair form matters. A bare `Kampilan` asserts that the sword a Mactan warrior
carried in 1521 was the object that museums catalog under that name from the
eighteenth century onward. `Kampilan — Great Blade` asserts something weaker and
truer: this is a great blade, and *kampilan* is the name the tradition attaches
to blades of this kind. The descriptor is what the game guarantees; the Filipino
name is what the tradition offers.

### 2.2 The roster

| Enum identity | Player label | Evidence tier | Basis |
| --- | --- | --- | --- |
| `Kampilan` (was `GreatBlade`) | Kampilan — Great Blade | Documented, form uncertain | Pigafetta records a large cutting sword at Mactan in 1521, comparing it to a scimitar. He records no local name for it. The word *kampilan* is attached to this blade class by later tradition, and surviving cataloged objects are largely eighteenth- and nineteenth-century. |
| `Wasay` (was `HeavyChopper`) | Wasay — War Axe | Documented, form uncertain | A hafted battle axe with a broad metal head, attested among Tausug and Ibanag groups and described as suited to chopping through shields. Pre-contact use is implied by accounts of later iron reinforcement, but no sixteenth-century lexical attestation was located during this research. |
| `Kalis` (was `ThrustingBlade`) | Kalis — Thrusting Blade | Documented | The strongest of the four. Pigafetta himself recorded *calis* in the Visayas in 1521, and the term recurs across vocabularies from 1612 through the late 1800s in Ilocano, Kapampangan, Ibanag, Tagalog, Bicolano, Waray, Hiligaynon, and Cebuano. This is a contemporary, pan-archipelagic term, not a regional back-projection. |
| `Itak` (was `Bolo`) | Itak — Work Blade | Provisional reconstruction | A Tagalog term for a field and utility blade also used in fighting. Preferred over the current enum identity `Bolo`, which is a Spanish-era term that the existing research document explicitly warns against using as a blanket name. The specific 1613 vocabulary attestation could not be confirmed from sources available during this research, so the tier stays provisional. |

### 2.3 Why the Heavy Chopper became an axe rather than a panabas

The obvious Filipino name for a forward-weighted chopping blade is *panabas*,
and it was the working assumption when this change started. The evidence does
not support it. The first documented mentions of the panabas appear in
nineteenth-century Spanish colonial accounts of Moro resistance, and the
surviving museum objects date to the eighteenth and nineteenth centuries. There
is no sixteenth-century source for it.

That is roughly a three-hundred-year gap between the game's setting and the
weapon's first appearance in the record. A `PROVISIONAL` badge is a reasonable
instrument for "the class is attested but this identification is a
reconstruction." It is not a reasonable instrument for "this weapon is not
attested until three centuries after the period depicted." Using it that way
would drain the badge of meaning everywhere else it appears.

The wasay is not perfect either — see its tier above — but an axe is a weapon
class with far better footing in the period, it preserves the forward-weighted
chopping role the profile was built around, and it does not require the badge to
paper over a gap of that size.

This costs more than a rename. The pawn silhouette must become an axe rather
than a broad blade, and the sound slot must be renamed along with its generated
files. Section 6 accounts for that.

### 2.4 Consequence for the governing documents

CLAUDE.md section 7 currently reads, in part: "Specific cultural identifications
(Kampilan, Panabas, Kris) live in evidence metadata with a `PROVISIONAL` note,
never as an unqualified label."

That sentence must be amended, because this design deliberately promotes those
names into player-facing labels. The amendment should preserve the intent while
permitting the pair form. Proposed replacement:

> Specific cultural identifications appear in player-facing UI only in pair
> form — the Filipino name, an em dash, and a plain English descriptor — and
> only with an evidence tier recorded in metadata and shown in the agent
> inspector. A cultural identification never appears as a bare, unqualified
> label, and a name whose earliest attestation postdates the depicted period by
> more than a century is not used at all.

The final clause is what keeps the policy load-bearing rather than decorative.
It is the rule that excluded the panabas, and writing it down means the next
weapon added has to clear the same bar.

`docs/research/HISTORICAL_1500s_WEAPONS.md` needs a matching update in its
"Named blade caution" section and in the cross-reference section at the end,
both of which currently state that player-facing UI uses plain descriptors
only.

## 3. Attributes

### 3.1 What moves

Three values move out of `Scenario` and into the combat ruleset:

- `DamagePerAttack` (currently 10)
- `AttackRangeRaw` (currently `12 * FixedPoint.Scale`)
- `AttackCooldownTicks` (currently 5)

The `Scenario` properties remain as the fallback used when a scenario runs
without a combat preset, and as the validated upper and lower bounds. The
authoritative per-warrior values come from the weapon.

### 3.2 Grip, and why a weapon has more than one profile

The three values above are **not** per-weapon. They are per weapon *and grip*.

A one-handed blade fought with a shield is not the same weapon as the same blade
fought with the off-hand free. The free hand lengthens the stroke, lets the
fighter commit weight into the blow, and removes the shield's mass from the
recovery — at the cost of every defensive benefit the shield was providing. That
difference is the whole reason a fighter would choose to drop the shield, and if
the attributes do not express it then the choice is not a choice.

Two concepts are therefore introduced.

**Grip** is a property of the weapon:

| Grip | Meaning |
| --- | --- |
| `TwoHanded` | The weapon occupies both hands. A shield is forbidden, not merely absent. |
| `OneHanded` | The weapon may be carried alone or paired with a shield. |

**Profiles** are the attribute sets a weapon exposes:

- A `TwoHanded` weapon declares exactly one profile, its solo profile.
- A `OneHanded` weapon declares exactly two, a solo profile and a paired
  profile.

Resolution is a single function, `ResolveWeaponProfile(WeaponId, ShieldId)`,
which returns the solo profile when the shield is `ShieldId.None` and the paired
profile otherwise. That function is deliberately the narrowest possible seam:
section 3.7 explains why.

Profiles are written out in full as explicit hand-authored rows rather than as a
base row plus an arithmetic delta. This matches the existing convention in
`PhilippineCombatPreset`, which states that configuration is explicit
hand-authored data rather than a deserialized or reflection-driven graph. It also
avoids introducing a delta operation that would need clamping, underflow checks,
and its own reach-floor validation. Six explicit rows are cheaper to reason about
than four rows and a rule.

### 3.3 The table

Reach is given in world units and stored raw, multiplied by
`FixedPoint.Scale`.

| Weapon | Grip | Profile | Damage | Reach | Cooldown | Damage per tick |
| --- | --- | --- | ---: | ---: | ---: | ---: |
| Kampilan — Great Blade | TwoHanded | solo | 15 | 16 | 7 | 2.14 |
| Wasay — War Axe | TwoHanded | solo | 18 | 13 | 8 | 2.25 |
| Kalis — Thrusting Blade | OneHanded | solo | 11 | 13 | 5 | 2.20 |
| Kalis — Thrusting Blade | OneHanded | paired | 10 | 12 | 5 | 2.00 |
| Itak — Work Blade | OneHanded | solo | 9 | 11 | 4 | 2.25 |
| Itak — Work Blade | OneHanded | paired | 8 | 10 | 4 | 2.00 |

These are provisional gameplay tuning values. They are not measurements, and
nothing in this table may be cited back into the research document or presented
to a spectator as a historical fact. What justifies them is the physical
character of the objects — length, where the mass sits, how many hands the thing
takes — not any source on how hard a sixteenth-century blade hit.

- **Kampilan.** The longest blade of the four and genuinely two-handed. Longest
  reach and high damage, paid for with the second-slowest recovery. No paired
  profile exists because no shield may be carried.
- **Wasay.** Mass concentrated behind a hafted head. Highest single-blow damage
  and the slowest recovery, with reach shorter than the long sword because a war
  axe is a shorter object. Also two-handed.
- **Kalis.** Its *paired* profile is exactly today's global defaults, which makes
  that row the control: if a battle's character shifts unexpectedly after this
  change, shielded kalis warriors are the ones whose behaviour should not have
  moved at all. Dropping the shield buys one more damage and one more world unit
  of reach.
- **Itak.** The shortest blade in the game and the fastest recovery. Dropping the
  shield buys one damage and one reach, same as the kalis, which keeps the solo
  trade uniform and therefore easy to explain and to retune in one place.

### 3.4 The balance shape

Two axes, not one.

Across weapons, the two-handed weapons top raw throughput: the wasay's 2.25 is
the highest damage-per-tick of any profile in the table, and the kampilan buys
the longest reach in the game. This is intentional headroom for V3, where the
one-handed weapons receive attack combinations. If the light blades also led on
sustained throughput here, they would dominate outright once combinations
arrived.

Within a one-handed weapon, solo trades defence for offence. The solo profile is
worth roughly ten percent more throughput plus one world unit of reach; the
paired profile is worth the tall hardwood shield's existing halving of chest and
abdomen targeting weight. Neither strictly dominates, which is the requirement —
a choice that always resolves the same way is not a choice.

Note that solo itak and the wasay both sit at 2.25 damage per tick by different
routes: the wasay lands 18 twice as slowly, the itak lands 9 twice as fast. That
collision is deliberate and is the clearest single illustration in the table that
damage-per-tick is not the whole picture — reach, burst, and survivability all
differ sharply between those two rows.

### 3.5 Invariants the preset must enforce at construction

These are validation failures thrown when the ruleset is built, not comments and
not roster conventions. A misconfigured preset must fail loudly and immediately
rather than producing a battle that quietly cannot happen.

1. **A `TwoHanded` weapon paired with any shield other than `ShieldId.None` is a
   configuration error.** This is what makes "requires no shield" a real rule
   rather than an accident of how the roster happens to be written today.
2. **A `OneHanded` weapon must declare both profiles.** A missing paired profile
   is an error, not a silent fallback to the solo one.
3. **Every profile independently satisfies the reach floor** in section 3.6.
   The floor is a property of a profile, not of a weapon, because the solo and
   paired reaches differ.

### 3.6 The reach floor is a hard constraint

`CollisionRules.DefaultBodyRadiusRaw` is four world units, giving an
eight-world-unit body diameter, and `BuildMovementProposal` stops an advancing
warrior at `2 * BodyRadiusRaw` — eight world units — so that opposing front
ranks make body contact rather than halting at weapon reach.

**Any weapon whose reach is at or below eight world units can never strike a
warrior it is standing against.** Such a warrior would advance into contact and
then be unable to attack. The paired itak's reach of 10 is the lowest value in
the table and clears the floor by two world units.

This must become an invariant test, not a comment, and it is asserted **per
profile**. The paired profile is the dangerous one: every one-handed weapon's
paired reach is shorter than its solo reach, so a future retune that shaves a
world unit off a paired profile is the most likely way anyone ever trips this
floor. Any weapon profile added to any preset needs its reach asserted greater
than `2 * Scenario.BodyRadiusRaw`.

### 3.7 The seam left for the shield system

The shield system is being designed separately and will introduce shields beyond
`TallHardwood`. V2 must not pre-empt that work, but it must leave it somewhere
clean to land. Three decisions do that.

**The resolver is the only place grip and shield meet.**
`ResolveWeaponProfile(WeaponId, ShieldId)` is the single function that turns a
loadout into attributes. In V2 its rule is trivial — `None` gives the solo
profile, anything else gives the paired profile. The shield system replaces that
one rule with per-shield resolution without touching any call site, because no
call site reads a profile directly.

**The paired profile is a base, not a final value.** V2 authors one paired
profile per one-handed weapon, which is correct while exactly one shield exists.
When the shield system adds a second and third shield, the paired profile becomes
the row those shields modify, rather than each shield needing a full attribute
table per weapon. This is what keeps the configuration from growing as
weapons × shields.

**The existing pair-keyed table is left alone.**
`CombatRuleset.BuildEffectiveWeightTables` already keys on
`(WeaponId Weapon, ShieldId Shield)` and returns an `EffectiveWeightTable`. V2
does not restructure it, does not fold attribute resolution into it, and does not
delete the `shieldMultipliers` data feeding it. That table is the natural home
for shield-driven attribute modification, and the shield agent should find it
where the current code left it.

What V2 explicitly does **not** decide: how many shields exist, what any shield
is called, whether a shield modifies attributes additively or by multiplier, and
whether shields interact with V3 combinations. Those belong to the shield design.

### 3.8 Spectator discoverability

Section 10 of `SIMULATION-GAME-STANDARDS.md` asks whether a spectator can
discover an effect without reading source code. For each attribute:

- **Damage** is already printed in the battle event feed, which formats an
  attack as `hit #7's neck with Kampilan — Great Blade for 15`. Distinct damage
  values become visible on the first exchange.
- **Cooldown** is visible as strike rate — the same warrior reappearing in the
  feed more or less often.
- **Reach** is visible as long-weapon warriors landing the first blows while
  ranks close, before the short blades are in range at all.
- **Grip and profile** are visible in the pawn silhouette — a shield bearer
  draws a shield block beside the torso and a solo fighter does not — and in the
  event feed, which must distinguish the two. A shielded and an unshielded kalis
  deal different damage under the same weapon name, so the feed formatting
  `hit #7's chest with Kalis — Thrusting Blade for 11` would be actively
  misleading if the same string could mean either 11 or 10. The feed appends the
  grip for one-handed weapons: `Kalis — Thrusting Blade (solo)` against
  `Kalis — Thrusting Blade (shielded)`. Two-handed weapons append nothing,
  because they have no second form to be confused with.

The agent inspector's weapon line carries the pair label, the evidence tier, the
grip, which profile is active, and the three attribute values — which is where a
curious spectator confirms what the feed implied, and where the solo-versus-paired
trade becomes readable as two columns rather than inferred across battles.

## 4. Determinism

This is a hash-moving change and must follow `hukbo-determinism-change`.

- **Enum symbol renames are hash-neutral.** `WeaponId` numeric values are the
  hashed quantity and none of them change: `Kampilan = 1`, `Wasay = 2`,
  `Kalis = 3`, `Itak = 4`. The `do not renumber or reorder` comment stays
  satisfied. Renaming the symbols alone would not move a hash.
- **The attribute change does move both hashes.** Per-warrior cooldown values
  feed `StateHasher` through `AgentState.AttackCooldownTicks`, and damage
  values feed the event hash through every attack event. State hash, event
  hash, and the ordered event stream all change.
- **A new preset version is therefore required**, per CLAUDE.md section 5:
  `CombatPresetId.PrecolonialPhilippinesV2 = 2`, with a new
  `CombatPresetRegistry` entry. `PrecolonialPhilippinesV1` stays in place,
  unmodified, so existing replays remain reproducible.
- **The roster grows from four entries to six**, because solo and paired are
  distinct loadouts rather than distinct weapons. `CombatRuleset.Roster` order is
  part of the content-hash contract, so the six entries have a fixed declared
  order, and `Scenario.RosterCounts` — which is indexed by roster position —
  changes length. Both move the state hash on their own, independently of the
  attribute change.
- **New golden expectations** must be recorded for the seed-1 baseline against
  V2. The old V1 goldens are kept, not edited.
- The pinned SplitMix64 vectors are not touched.
- Grip is static configuration and is never drawn from, so it introduces no new
  random stream and no new draw ordering.

## 5. Roster, army composition, and settings

### 5.1 Six roster entries

The roster is a list of loadouts, not a list of weapons, and solo and paired are
different loadouts. V2's roster is therefore:

| Index | Loadout |
| ---: | --- |
| 0 | Kampilan, light organic, no shield |
| 1 | Wasay, light organic, no shield |
| 2 | Kalis, light organic, no shield |
| 3 | Kalis, light organic, tall hardwood |
| 4 | Itak, light organic, no shield |
| 5 | Itak, light organic, tall hardwood |

Solo entries are declared before their paired counterparts so that the ordering
rule is stated rather than incidental: weapon order first, then solo before
paired within a weapon.

The alternative — keeping four roster entries and letting the unused profile sit
in the data — was rejected. A profile no scenario can field is dead
configuration, and dead configuration is how a table drifts out of agreement with
the game without anyone noticing. If both profiles exist, both must be fieldable.

This also gives V3 a much better story than the one currently written in its
design document. V3 does not remove shields; V3 fields the four solo loadouts and
leaves the two paired ones unselected. That is a scenario choice, not a
mechanical regression, and it means nothing has to be torn out and put back.

### 5.2 Army composition UI

`UI/ArmyCompositionPanel.cs` grows from four rows to six. The two extra rows are
not new weapons, and presenting them as such would be confusing — a player
reading six entries should see four weapons, two of which appear twice with
different grips. The panel should group by weapon and label the variant, rather
than listing six flat names.

Whether six rows still fit the panel's existing height and layout metrics needs
checking against the `hukbo-client-ui` skill before implementation, not after.
This is the most likely place in the whole change for the layout to break.

### 5.3 Settings migration

`ClientSettings` persists army composition as four named counts, including
`HeavyChopperCount`. This change both renames those fields and adds two more, so
the persisted shape changes twice over and an existing settings file cannot be
read forward under any interpretation.

Accept the reset rather than write a migration. The counts are six small integers
a user re-enters in seconds, the game is in development with no shipped installs,
and a migration for a shape that is about to change again when shields arrive is
work with a short half-life. This must be a stated decision carrying a note in
the settings store, not an accident — and the plan document carries it as an
explicit task either way.

## 6. Surface to change

Recorded so the plan document can assign non-overlapping file ownership.

**Core.** `Combat/CombatIdentity.cs` (enum symbol renames, new `WeaponGrip`
enum, new `CombatPresetId` value), a new `Combat/WeaponProfile.cs` holding the
damage, reach, and cooldown record, `Combat/PhilippineCombatPreset.cs` (V1
frozen, V2 added with the six-row profile table and the six-entry roster),
`Combat/CombatRuleset.cs` (carry grip and both profiles, expose
`ResolveWeaponProfile`, enforce the section 3.5 construction invariants),
`Combat/CombatPresetRegistry.cs`, `Simulation/AgentState.cs`,
`Simulation/BattleSimulation.cs` (read damage, reach, and cooldown through the
resolver rather than from `Scenario`), `Simulation/Scenario.cs` (fallback,
bounds, `RosterCounts` length), `Determinism/StateHasher.cs`.

**Client.** `Presentation/BattleEventFormatter.cs`, `Presentation/PawnAppearance.cs`,
`Presentation/PawnAppearanceFactory.cs`, `Rendering/PawnGeometry.cs`,
`Rendering/PawnRenderer.cs` (axe silhouette), `Rendering/BloodGeometry.cs`,
`Audio/AudioTypes.cs`, `Audio/SoundCueMapper.cs`, `Audio/SoundCatalog.cs`,
`Settings/ClientSettings.cs`, `UI/ArmyCompositionPanel.cs`,
`UI/AgentInspectorContent.cs` (evidence tier line), `ArenaGame.cs`.

**Content.** The `attack-heavy-chopper-*.wav` family renames to
`attack-war-axe-*`, roughly a dozen files, plus `Content/Audio/README.md` and
`Content/Audio/GENERATED.md`. The existing generation prompts describe a heavy
chopping blade, which suits an axe; regeneration is not required, only renaming
and a note in `GENERATED.md` recording that these files were authored under the
previous slot name.

**Docs.** `CLAUDE.md` section 7, `docs/research/HISTORICAL_1500s_WEAPONS.md`
("Named blade caution" and the closing cross-reference section),
`docs/development/testing.md` (new smoke rows for the pair labels and the
inspector evidence line, left `PENDING`).

**Tests.** Determinism goldens for V2; a reach-floor invariant test asserting
every profile of every weapon in every registered preset, not just V2's; a test
that a `TwoHanded` weapon paired with a non-`None` shield throws at ruleset
construction; a test that a `OneHanded` weapon missing its paired profile throws;
a `ResolveWeaponProfile` test covering both branches per one-handed weapon;
roster-order and roster-length tests; preset version tests; event formatter label
tests including the solo and shielded grip suffixes; sound catalog tests;
settings tests; blood geometry tests; pawn appearance tests.

## 7. Open questions

1. Does the agent inspector show the evidence tier as a badge, a tooltip, or a
   plain line? The `hukbo-client-ui` skill's 27 semantic theme roles need
   checking for whether a suitable role exists or a new one is required.
2. Should the itak's tier be raised if the 1613 vocabulary attestation is
   confirmed later? The plan should leave the tier a single-constant change.
3. The wasay's silhouette needs a decision on whether the head is drawn as a
   distinct shape from the haft at low detail tiers, where the pawn is only a
   few pixels tall.

## 8. Nine questions

Answered per `SIMULATION-GAME-STANDARDS.md` section 10 in the plan document,
which is the artifact that authorizes implementation. The discoverability
question — the one this design is most at risk on — is answered in section 3.5
above.
