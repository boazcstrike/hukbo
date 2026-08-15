# Armor identity — design

Status: **design document.** It records decisions and defines a model. Under the
workflow in `CLAUDE.md` section 6, a design document does not authorize
implementation. The plan document that follows it carries the ordered task list,
and the canonical gate is what proves the work.

## 1. Purpose

Hukbo has an armor identity, `ArmorId`, that is folded into three separate
hashes and read by nothing. It has exactly one member, `LightOrganic`, which
every agent in every shipped preset wears. Meanwhile the pawn a player sees
wears one of five cosmetic armor skins drawn from an entirely unrelated
catalog. Two systems share a word and neither does what a reader would assume.

This design makes armor an authoritative gameplay identity with a real
defensive effect, and gives every historically attested armor type of the
period a place in the game, without inflating the simulation roster to match.

The evidence base is `docs/research/HISTORICAL_1500s_ARMOR.md`. The engineering
assessment of the change is
`docs/research/2026-08-15-armor-gameplay-implications.md`. Where this document
and the research document disagree about a historical fact, the research
document wins.

## 2. Decisions taken

These were decided by the repository owner on 2026-08-15. They are recorded so
that a later reader can tell a decision from an assumption.

| Ref | Question | Decision |
| --- | --- | --- |
| A1 | Authoritative or cosmetic? | **Authoritative.** Armor becomes real simulation state with a real effect. |
| A2 | What does armor do? | **Damage reduction.** |
| A3 | How does a spectator discover it? | Surface it in the agent inspector and in the event stream, and document it alongside. See section 10 for why documentation alone was not sufficient. |
| A4 | How many armor identities? | **All nine attested types ship.** Realised as baselines plus variants — section 3. |
| A5 | Does armor affect movement? | **Yes**, through a weight class rather than per-armor movement rows. |
| A6 | Pair-form Filipino labels? | **Yes, where the evidence permits.** Only one body-armor term currently clears the bar. See section 9. |
| A7 | Regional binding? | **Yes.** Regional appearance presets select variants. |
| A8 | Unify the two armor systems? | **No — layer them.** Baselines in `Hukbo.Core`, variants in `Hukbo.Client`, following the shipped `ShieldVariant` precedent. |
| A9 | Evidence tier on the inspector armor row? | **Yes.** It is missing today and that is a live gap against `CLAUDE.md` section 7. |
| — | Is there a `None` baseline? | **Yes.** Bare torso is documented at Mactan in 1521. |
| — | Armor distribution across a roster | A configurable count per armor type in additional settings, evenly distributed and **mirrored across both teams**. |

Research decisions taken at the same time, affecting how the evidence is
presented rather than how the game behaves:

- The contested Los Camarines iron corselets **are included** as a variant. The
  research document continues to record Scott's objection that the report is a
  confusion with Japanese materiel; including the variant in the game does not
  settle the historical dispute, and the variant's notes must say so.
- The Scott-only terms (*barote*, *pakil*, *batung-batung*, *habay-habay*) are
  **retained**, each marked as resting on a single modern authority.
- The enmeshing *kalasag* description is **retained**, marked as a 1994
  assertion with no sixteenth-century source attached.
- The Sarangani helmet disagreement is **retained as a disagreement**. Two
  accounts of one encounter name different materials, so both become variants
  rather than one being chosen.
- The Boxer Codex Cagayan shield is **64 cm wide, not 127 cm**. A *braza* is
  1.70 m; the bracketed "of a fathom" produces a shield wider than its bearer,
  while reading *tres cuartas* as three quarter-*varas* gives 63.6 cm, which
  agrees with Morga's full-height shield and with Scott's approximately
  50-by-150-centimetre figure.
- Sande's 1577 relation is **corroboration only**. It attests that armor was
  widespread — "thousands of lances, daggers, shields, and other pieces of
  armor" — but names no type, and it appears inside a request to the Crown for
  more arquebuses, so it is rhetoric rather than an inventory.

Still open, and blocking nothing in this design: whether *tamin* or *taming* is
the attested form, whether *palisay* is a fighting or a dance shield, whether
*baluti* and *barote* name the same garment, and whether *carasas* is its own
shield name or Morga's spelling of *kalasag*. Each blocks only a player-facing
Filipino label for the term concerned.

## 3. Architecture: baselines and variants

The central structural decision. Two layers, split by whether a thing changes
the simulation.

```
Hukbo.Core        ArmorId          six values      hashed, authoritative
                                                   owns damage reduction
                                                   owns movement weight class
                       ▲
                       │ each variant declares its baseline
                       │
Hukbo.Client      ArmorVariant     nine+ rows      never hashed, presentational
                                                   owns cultural identity
                                                   owns pair-form label
                                                   owns evidence tier and notes
                                                   owns the drawn appearance
```

This is not a new pattern. `ShieldVariant` already works exactly this way: it
exists only in `Hukbo.Client`, appears nowhere in `Hukbo.Core`, and carries an
evidence tier and notes that the agent inspector renders. Armor follows it.

The reason to layer rather than merge is arithmetic. Every value added to
`ArmorId` is permanent, is folded into three hashes, and multiplies the
canonical movement loadout table. Every row added to a variant catalog costs
nothing but the authoring. Nine simulation values would have bought nine
near-identical defensive profiles at a very high price; nine variants over six
baselines buys the same historical coverage for the price of six.

The existing five-option cosmetic wardrobe
(`ArmorF1Unarmored` … `ArmorF5ShellSetHelmet`) is superseded by the variant
catalog and folded into it. That resolves the two-systems-one-word problem by
making the cosmetic layer the variant layer, keyed to a baseline, rather than
leaving it floating free.

## 4. Baseline roster — `ArmorId`

Values are append-only. `LightOrganic = 1` already exists and must never be
renumbered, because it is part of the replay and content-hash contract for
every preset shipped so far.

| Value | Member | Weight class | Role |
| --- | --- | --- | --- |
| `1` | `LightOrganic` | Light | **Legacy.** Retained unchanged so existing replays reproduce. New presets do not field it. |
| `2` | `None` | Light | No body armor. Bare torso. |
| `3` | `QuiltedCotton` | Light | Layered plant-fibre or cotton garment. |
| `4` | `HideCorselet` | Medium | Carabao or buffalo hide. |
| `5` | `RigidCuirass` | Rigid | Hardwood, horn, or bamboo plate over the torso. |
| `6` | `ImportedIron` | Rigid | Imported iron. Rare. Historically contested. |

`LightOrganic` is deliberately kept and deliberately unused. Deleting it or
renumbering around it would break every recorded replay; retiring it from new
presets costs nothing and avoids a second generic value competing with `None`.

## 5. Variant catalog — `ArmorVariant`

Lives in `Hukbo.Client`. Each row declares its baseline, its evidence tier, its
label, and its notes. Rows may be added freely without touching a hash.

| Variant | Baseline | Evidence | Tier |
| --- | --- | --- | --- |
| Baluti — Quilted Armor | `QuiltedCotton` | Pigafetta's Visayan vocabulary, 1521 | `Documented` |
| Sleeved full-length cotton corselet | `QuiltedCotton` | Alvarado, Villalobos relation, 1548 | `Documented, form uncertain` |
| Rattan armor | `LightOrganic` → see note | Artieda, 1573 | `Documented, form uncertain` |
| Corded rope corselet | `LightOrganic` → see note | Lavezaris 1573; Legazpi, Cebu, 1565 | `Documented` |
| Carabao-hide corselet | `HideCorselet` | Lavezaris 1573; Cauchela and Aldave, 17 July 1574 | `Documented` |
| Hardwood corselet | `RigidCuirass` | Artieda 1573, "some wear corselets… hard black wood" | `Documented` |
| Wood-and-buffalo-horn corselet | `RigidCuirass` | Alvarado, 1548 | `Documented, form uncertain` |
| Bamboo-and-hardwood cuirass | `RigidCuirass` | Alvarado, 1548 | `Documented, form uncertain` |
| Iron corselet with greaves and gauntlets | `ImportedIron` | Lavezaris, 17 July 1574 — **contested** | `Documented, form uncertain` |
| Bare torso | `None` | Pigafetta, Mactan, 1521 | `Documented` |

**Note on the two `LightOrganic` rows.** Rattan and corded rope are flexible
organic armors and belong to the light class, but `LightOrganic` is reserved as
a legacy value that new presets do not field. Both rows therefore bind to
`QuiltedCotton` in practice, which is the light-class baseline new presets use.
The alternative — a seventh baseline named for flexible non-cotton organics —
buys one more damage-reduction row for the cost of a permanent enum value and a
wider movement table, and is not worth it. This is a gameplay grouping, not a
historical claim that rattan and cotton are the same material, and the variant
notes must say so.

Head protection is **out of scope for this design.** Dogfish-skin head armor
(Alvarado 1548) and the shell-and-fishbone helmet (Cauchela and Aldave 1574)
are attested, and the Sarangani octopus-skin helmet is a recorded disagreement,
but Hukbo has no helmet identity of any kind and adding one is a separate
decision with its own hash cost. It gets its own design document.

## 6. Damage reduction

Armor reduces damage. It does not change where an attack lands — that is what
`ShieldId` already does through `ResolveDefenseMultiplier(ShieldId, BodyPart)`,
and the two effects must stay distinct so a spectator can tell them apart.

The model:

- A mitigation value per `(ArmorId, BodyPart)` pair, expressed in **basis
  points**, matching the existing convention in `LoadoutMovementProfile`
  (`ForwardPaceBasisPoints`, `LateralPaceBasisPoints`). Integer arithmetic
  throughout. No floating point reaches the state hash.
- Keyed by body part because these are corselets. Most cover the torso and
  nothing else; Alvarado's sleeved full-length cotton corselet covers far more.
  A model that ignores body part would make a torso-only hardwood plate protect
  a leg, which the sources do not support.
- Applied at damage application, **after** hit-location resolution and
  **before** the damage is written. This positions it against the existing
  defensive resolution contract in `SIMULATION-GAME-STANDARDS.md` section 14
  rather than building a parallel pipeline beside it.
- Baked once at ruleset construction, following `BuildEffectiveWeightTables`
  and validated the way `ValidateResolvedTotals` validates the shield table, so
  per-attack cost stays a single indexed read.

**Every mitigation number is invented gameplay tuning.** No source in the
period states what any armor stopped, and the one apparent exception — the
1574 claim that no weapon except the arquebus could damage the Camarines
equipment — follows a three-item list and is ambiguous about what it covers.
Under `CLAUDE.md` section 7 every one of these values is marked provisional in
its code comment and in its test, exactly as the tall-hardwood shield
multiplier already is. They are not measurements and must never be presented
as any.

## 7. Movement

Armor affects movement through its **weight class**, not through per-armor
rows.

The canonical loadout key is `(WeaponId, ArmorId, ShieldId)` and today resolves
six authored rows, every one of them hardcoded to `ArmorId.LightOrganic`, with
an unmatched tuple returning `-1` so an unknown combination fails loudly. Making
armor a full movement dimension across six baselines would take that table to
roughly thirty-six rows, each needing hand-authored pace values, and each
folding into `MovementRuleset.ContentHash`.

Instead the weight class — Light, Medium, Rigid — applies a modifier to the
existing rows. Three classes, one modifier table, and the loadout table keeps
its current shape. The historical effect the decision asked for is preserved: a
warrior in a hardwood cuirass moves differently from one in a bare torso.

## 8. Distribution

Armor distribution is a **client setting**, not a ruleset constant: a
configurable count per armor type, evenly distributed, and mirrored across both
teams.

This follows the existing `ArmyComposition` precedent, where composition is
chosen in client settings and moves no ruleset content hash. The chosen
composition is scenario input, so it naturally changes a given run's state
hash — that is expected and is not a determinism violation.

Mirroring matters for more than fairness. With both teams fielding identical
armor composition, any divergence in outcome is attributable to the simulation
rather than to the draw, which is what makes armor effects legible when
comparing runs.

There is no historical basis for any distribution. No source in the record
states what proportion of a force wore armor. The setting exists precisely
because the number is invented, and it must be labelled that way.

## 9. Labels and evidence tiers

Pair form applies where the evidence permits it, per `CLAUDE.md` section 7: the
Filipino name, an em dash, and a plain English descriptor, with an evidence
tier recorded and shown in the inspector.

**Only one body-armor term currently clears the bar.** *Baluti* is glossed in
Pigafetta's own 1521 vocabulary as quilted garments used for fighting — a
contact-period eyewitness recording the word and its martial meaning. It ships
as **Baluti — Quilted Armor** at tier `Documented`.

Every other Filipino armor term in the research came back OPEN or EXCLUDE.
*Barote*, *pakil*, and *batung-batung* rest on a single modern authority;
*kurab-a-kulang* postdates the period by roughly three centuries and is excluded
by the same rule that excluded the panabas. Those variants therefore ship with
**English descriptors only**, and gain a Filipino name later if and when the
open questions resolve. This is the attestation rule working as intended, not a
gap in the design.

The inspector's armor row gains an evidence tier line, closing the gap in A9.
The weapon row and the cosmetic shield row already have one; the armor row has
never had one.

## 10. Spectator discoverability

The decision recorded in A3 was to document the effect. Documentation alone
does not clear this repository's own bar, so the design goes further.

`SIMULATION-GAME-STANDARDS.md` section 10 question 8 asks how a spectator
discovers an effect, and `WEAPON_CLASH_1500s.md` states that a single hidden
scalar probability "is not discoverable and should not ship". A document in
`docs/` is outside the game and cannot satisfy a question about what a player
watching a battle can find out.

Armor is therefore discoverable three ways: the agent inspector shows the armor
identity, its variant, and its evidence tier; the event stream records when
armor changed a damage outcome, so the effect is visible in the battle feed
rather than inferred; and the documentation sits alongside both. The first two
are what satisfy the standard.

## 11. Determinism impact

This is the expensive part and it is not optional.

- **A new combat preset version.** `CLAUDE.md` requires one for any change to
  enum values, roster order, or weights. The existing presets stay registered
  and unmodified so their replays keep reproducing.
- **Three hashes move**, not two: `CombatRuleset.ContentHash`,
  `MovementRuleset.ContentHash` — armor is folded there too, which is easy to
  miss — and the per-tick state hash.
- **Five pinned literal sites** across three files record the current preset
  content hash, and one of them embeds it in explanatory prose inside a test
  fixture rather than as a bare number. All five need new golden expectations.
- **`GetArmorLabel` throws on any unrecognized member** and its test covers only
  `LightOrganic`, so the Client suite will not catch the crash. Every new
  baseline needs a label case, and the test needs to cover them. A Core enum
  addition reddening the Client suite is a failure mode this repository has hit
  before.
- **The canonical loadout table fails loudly** on an unmatched tuple by design.
  Any new `(weapon, armor, shield)` combination a preset fields must have a row.

## 12. The nine acceptance questions

1. **User-visible outcome.** Warriors in heavier armor survive blows that kill
   unarmored ones, move slightly slower, and show their armor identity, variant,
   and evidence tier in the inspector.
2. **Tick stage and state.** Damage application, after hit-location resolution
   and before damage is written. Reads `CombatLoadout.Armor` and `BodyPart`;
   writes the mitigated damage.
3. **Units and bounds.** Mitigation in integer basis points per
   `(ArmorId, BodyPart)`. Bounds are a tuning decision, not a measurement, and
   are marked provisional.
4. **Ordering and random streams.** Mitigation is a deterministic table lookup
   and introduces no new random draw, so no new domain tag is required. If a
   later revision adds a probabilistic armor channel it must mint a fresh
   never-reused tag.
5. **Cache.** The mitigation table is baked once at ruleset construction and is
   immutable thereafter. No runtime cache, no invalidation.
6. **Save, event, version.** Authoritative. New preset version, three hashes
   move, new golden expectations. Not presentation-only.
7. **Complexity.** One indexed read per resolved attack. Measured against the
   200-agent, 10,000-tick, seed-1 canonical workload.
8. **Spectator explanation.** Inspector rows plus an event when armor changed
   an outcome. See section 10.
9. **Tests.** Listed in the plan document.

## 13. What this design does not authorize

It does not authorize head protection or any helmet identity. It does not
authorize armor durability, degradation, or repair — that is a
stock-and-consumption economy and is forbidden by `CLAUDE.md` section 9 until a
gate says otherwise. It does not authorize retiring or renumbering
`LightOrganic`. It does not settle any of the four open historical questions,
and it does not authorize a Filipino label for any term the research graded
OPEN.

It does not authorize implementation. The plan document does that, and the
canonical gate is what proves it.
