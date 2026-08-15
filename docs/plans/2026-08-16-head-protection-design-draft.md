# Head protection — design draft

Status: **draft, backlog.** Not authorized, not scheduled, and not part of the
armor identity package. It exists so that the head-protection evidence gathered
on 2026-08-15 has somewhere to live rather than being rediscovered later. Do not
execute it. Finish `docs/plans/2026-08-15-armor-identity.md` first.

## 1. Why this is separate

The armor identity design deliberately scoped head protection out. Hukbo has no
helmet identity of any kind — no enum, no field on `CombatLoadout`, no
mitigation, no inspector row. Adding one is not an extension of the armor work;
it is a second authoritative identity with its own hash cost, its own preset
version, and its own movement question.

The armor mitigation table already reserves a `Head` column and sets it to zero
for every baseline. That column is the seam this design would fill.

## 2. The evidence

From `docs/research/HISTORICAL_1500s_ARMOR.md`. Tiers are that document's.

| Item | Material | Region and date | Source | Tier |
| --- | --- | --- | --- | --- |
| Dogfish-skin head armor | Dogfish skin, described as very tough | Villalobos expedition, 1548 | García Descalante Alvarado, relation, Blair and Robertson volume II | `Documented` |
| Helmet set with fishbones and stout shells | Shell and bone over an unstated base | Camarines, 17 July 1574 | Cauchela and Aldave to Felipe II, volume XXXIV page 295 | `Documented, form uncertain` |
| Iron helmet | Iron, imported | Los Camarines, 17 July 1574 | Lavezaris to Felipe II — **contested**, Scott argues Japanese materiel | `Documented, form uncertain` |
| Wooden helmet faced with shark skin | Wood faced with shark skin | Visayas, sixteenth century | Scott 1994, reading period dictionaries | `Documented, form uncertain` |
| Wooden helmet covered with octopus skin, plumed | Wood, octopus skin, plume | Sarangani | Scott 1994, citing an anonymous Villalobos relation | `Provisional reconstruction` |
| Cap likened to a coloured morrión | Unstated, worn with quilted armor | Cagayan, about 1590 | Boxer Codex — **unverified**, manuscript not read | `Documented, form uncertain` |
| *Putong* head-cloth | Cloth | Visayas and Tagalog | Scott 1994; Morga 1609 | `Documented` — **status marker, never described as protective** |

Two things this table must not be allowed to blur.

**The *putong* is not armor.** Every sixteenth-century description treats it as a
status marker — red if the wearer had personally killed. It belongs to the
appearance layer, not to a protective identity, and giving it mitigation would
be inventing a fact.

**The Sarangani helmet is a disagreement, not a fact.** Alvarado's 1548 relation
and Scott's anonymous relation describe the same island and the same expedition
and name different materials — dogfish skin with no plume, against octopus skin
with a waving plume. The decision recorded on 2026-08-15 was to keep both as
variants rather than choose. That decision carries into this design.

## 3. Open questions this design must answer

1. **Own enum, or a field on the armor identity?** A `HelmetId` mirrors
   `ArmorId` and keeps the two independent, at the cost of a third identity in
   the loadout key. Folding head protection into `ArmorId` would halve the
   enum count but forces every armor value to carry a helmet assumption, which
   the sources do not support — hide corselets and shell-set helmets appear in
   the same sentence, but hardwood corselets appear without any helmet at all.
   *Leaning: own enum.*
2. **Does it reach the movement key?** The canonical loadout key is already
   `(weapon, armor, shield)`. A fourth dimension is the expensive option; a
   weight-class contribution, as armor uses, is the cheap one.
3. **What does a head hit mean today?** The mitigation model is keyed by
   `BodyPart`, so this depends on how often `Head` is actually selected by
   hit-location resolution. If head hits are rare, helmet mitigation is nearly
   invisible and the feature fails the spectator-discoverability bar for the
   same reason a hidden scalar does. **Measure the head-hit rate under the
   shipped preset before designing anything.**
4. **Is a helmet lethal-blow prevention instead of mitigation?** If head hits
   are rare but disproportionately lethal, converting a lethal head blow into a
   survivable one is more legible than a percentage, and reuses the event
   already defined for armor.
5. **Which variants ship?** Six candidates, one of which is contested, one
   unverified, and one a recorded disagreement.

## 4. What it would cost

The same shape as the armor package, and no cheaper:

- A new preset version. `PrecolonialPhilippinesV7` will be taken by the armor
  package, so this is V8.
- Three hashes move again — combat ruleset content, movement ruleset content if
  the weight class is touched, and the per-tick state hash.
- The five pinned content-hash literal sites need new golden expectations again,
  including the one embedded in fixture prose.
- A label switch with a throwing default, the same crash trap `GetArmorLabel`
  has. Whatever enum is added needs exhaustive label coverage from the start.
- Battles must still terminate. Head protection stacks with armor mitigation,
  so the standoff risk in the armor design's section 6.1 compounds here.

## 5. What this draft does not do

It does not decide anything. It does not authorize implementation, and it is not
a plan document — there is no task list, deliberately. It records evidence and
names the questions so that whoever picks this up starts from the sources rather
than from memory.
