# Warrior Appearance in the 1500s Philippines: Historical Research for a Component System

Research date: 2026-07-28

Purpose: provide the evidence base for a warrior appearance component system in
Hukbo. The goal is a set of independently reviewable appearance components —
hair, head covering, torso garment, lower garment, armor layer, sash, accessory,
adornment, palette, and condition — that can be composed into fifty or more
distinct, historically defensible warrior appearance presets. This document does
not authorize implementation, does not define gameplay values, and does not
replace the manual review of any preset built from it.

This document is governed by the historical accuracy policy in `CLAUDE.md`
section 7 and follows the precedent set by
[`../HISTORICAL_1500s_WEAPONS.md`](../HISTORICAL_1500s_WEAPONS.md): pair-form
naming, evidence tiers on every claim, and the rule that a name whose earliest
attestation postdates the depicted period by more than a century is not used at
all.

## 1. Research boundary

"The Philippines in the 1500s" had no single costume, hairstyle, or armor
tradition. The evidence used here concentrates on:

- Suluan, Homonhon, Limasawa, Cebu, and Mactan in 1521 (Pigafetta's eyewitness
  account);
- Visayan and Luzon encounters from 1565 to 1576 (Legazpi-era relations
  translated in Blair and Robertson);
- Miguel de Loarca's *Relación de las Islas Filipinas* of 1582, written by a
  long-term resident of Panay;
- the Boxer Codex, compiled in Manila around 1590, whose colored plates are the
  single most important visual source for clothing silhouette and color;
- Pedro Chirino's *Relación de las Islas Filipinas* of 1604; and
- Antonio de Morga's *Sucesos de las Islas Filipinas* of 1609, describing
  Tagalog dress from direct observation in Manila.

Two later bodies of material are used only as labeled comparative evidence:
Francisco Alcina's *Historia de las islas e indios de Bisayas* (1668), which
describes Visayan practice a full century and a half after Mactan, and modern
scholarly synthesis, above all William Henry Scott's *Barangay:
Sixteenth-Century Philippine Culture and Society* (1994), which assembles the
early dictionaries and relations region by region. Scott is a guide to the
primary sources, not a primary source himself; where a claim below rests on
Scott's reading of an early Visayan dictionary, that is stated.

Spanish observers wrote from a colonial perspective and used imprecise labels.
Their descriptions are evidence about what people wore and looked like, not
neutral ethnography. The Boxer Codex in particular has Chinese pictorial
influences and European patronage: it should guide broad silhouette, garment
type, and color, never fine technical detail.

### Confidence labels

Identical to the weapons document:

- **Documented:** directly described or pictured in a sixteenth- or
  very-early-seventeenth-century source.
- **Documented, form uncertain:** the practice or garment class is attested,
  but its exact local form, cut, or name is uncertain.
- **Provisional reconstruction:** a plausible identification supported by later
  traditions, later dictionaries, or surviving objects, but not firmly
  established for the depicted period.

### The naming bar

A Filipino term appears in a player-facing label only in pair form — the
Filipino name, an em dash, and a plain English descriptor — and only when its
earliest attestation clears the hundred-year bar. The attestation window that
clears the bar for this document runs from Pigafetta (1521) through the early
Spanish-era vocabularies and relations of roughly 1582–1637 (Loarca, the Boxer
Codex, Chirino, Morga, San Buenaventura's 1613 Tagalog vocabulary, and the
early Visayan dictionaries Scott cites). Terms first attested in Alcina (1668)
or later are treated as sitting at or beyond the edge of the bar and are used
in plain English form only, or flagged explicitly. Terms first attested in the
eighteenth century or later (for example *salakot* in the form and spelling
now familiar) are not used as labels at all, exactly as the panabas was
excluded from the armory.

## 2. Rendering context: what actually matters on screen

Hukbo renders small top-down procedural pawns built from a head disk, a torso
capsule, a weapon silhouette, and a team-colored ground ring (see the visual
grammar in `HISTORICAL_1500s_WEAPONS.md`). At that scale the component system
can only communicate through:

1. **silhouette changes** — a head wrap wedge, a hair knot bump, a shield
   block, an armor-thickened torso capsule, a sash line;
2. **color blocks** — skin tone, garment dye color, armor material color, a
   gold glint pixel, a tattoo-darkened limb tone; and
3. **one or two accent marks** — a red head wrap, a gold edge line.

Fine detail — embroidery, specific tattoo motifs, jewelry granulation — is
invisible at pawn scale and belongs only in the agent inspector as text, where
the evidence tier is also shown. Every component below therefore records its
*pawn-scale visual read* alongside its historical fields. No appearance
component may change hitboxes, movement, range, damage, health, simulation
hashes, or AI; appearance is presentation-only, exactly as the placeholder body
variety rules already require.

## 3. Component categories and options

Each option carries these fields: proposed label; description; date range of
attestation; geographic and cultural scope; social and military context;
evidence tier; primary source; later comparative source where used; what is
directly supported versus reconstructed; safe-to-depict features; and
must-not-generalize features.

---

### Category A — Stature and build

Stature and build are presentation variation, not historical claims about any
population. Pigafetta remarks on individual physique (he calls Raja Kolambu of
Limasawa the finest-looking man he had seen in those parts, and Humabon of Cebu
short and fat), which documents only that individuals varied — something no
source was needed to establish. The existing placeholder scheme stands:

- **Stature:** short, average, tall (torso height and head offset).
- **Build:** slight, average, broad (torso width).
- Head size and weapon reach stay visually stable; every figure anchors at the
  feet; the multipliers in `HISTORICAL_1500s_WEAPONS.md` remain the reference.

Evidence tier: not applicable — this category asserts nothing historical. Nine
combinations. Do not attach ethnic, regional, or class meaning to any stature
or build value; the sources do not support it and the accuracy policy forbids
inventing it.

---

### Category B — Hair

Hair is one of the strongest documented appearance domains, and at pawn scale
it is cheap: a knot bump, a loose-hair fringe, or a bare wedge on the head
disk.

#### B1. Long hair gathered in a knot

- **Proposed label:** Long Hair, Knotted (plain English only; no single
  indigenous term for the style clears the bar archipelago-wide).
- **Description:** hair worn long by men and women, gathered and fastened in a
  knot at the crown or back of the head.
- **Date range:** 1521–1609 attestation window.
- **Scope:** widely reported for Visayans and Tagalogs; treat as the default
  lowland style.
- **Social and military context:** everyday and battle wear alike; long hair
  carried strong cultural value in early accounts.
- **Evidence tier:** Documented.
- **Primary source:** early relations translated in Blair and Robertson
  describe men and women wearing hair long and fastened in a knot on the
  crown; Boxer Codex plates show gathered hair under and around head wraps.
- **Later comparative source:** Scott, *Barangay*, ch. 1–2, synthesizing the
  early dictionaries on hair care and dressing.
- **Directly supported:** long hair; knotting at the crown.
- **Reconstructed:** exact knot position and tie method.
- **Safe to depict:** a small round bump on the head disk, dark hair color.
- **Must not generalize:** do not present one knot style as pan-Philippine or
  assign it gendered meaning at pawn scale.

#### B2. Long loose hair

- **Proposed label:** Long Loose Hair.
- **Description:** hair worn long and unbound, falling past the shoulders.
- **Date range:** c. 1590 (visual).
- **Scope:** documented visually for the Cagayan warrior figures of the Boxer
  Codex; plausible elsewhere but not the default.
- **Social and military context:** the Boxer Codex Cagayan warrior carries
  spear and tall shield with loose hair — battle wear, not undress.
- **Evidence tier:** Documented (visually, for Cagayan); Documented, form
  uncertain elsewhere.
- **Primary source:** Boxer Codex, Cagayan warrior plate (Wikimedia Commons
  file `Cagayan Warrior.png`).
- **Directly supported:** loose long hair on an armed warrior figure.
- **Reconstructed:** how common loose versus bound hair was in actual combat.
- **Safe to depict:** a hair fringe extending slightly beyond the head disk.
- **Must not generalize:** this is a northern Luzon visual reference; do not
  make it the Visayan default.

#### B3. Shoulder-cropped hair

- **Proposed label:** Cropped Hair.
- **Description:** hair cut to roughly shoulder length or shorter; Loarca
  compares one men's style to a Spanish *coleta* (cue).
- **Date range:** 1582–1609.
- **Scope:** reported among some groups; regional distribution uncertain.
- **Evidence tier:** Documented, form uncertain.
- **Primary source:** Loarca, *Relación* (1582), in Blair and Robertson vol. V.
- **Later comparative source:** Scott, *Barangay*, on regional hairstyle
  differences.
- **Directly supported:** that not all men wore the long knotted style.
- **Reconstructed:** the exact cut and its distribution.
- **Safe to depict:** a plain head disk with no knot bump.
- **Must not generalize:** do not code cropped hair as "commoner" and long
  hair as "elite"; the sources do not draw that line.

#### B4. Hair fully covered by head wrap

- **Proposed label:** none needed (this is the interaction of hair with C1).
- **Description:** hair braided or twisted and tucked entirely into a head
  wrap, so no hair silhouette shows.
- **Evidence tier:** Documented (the wrap; see C1).
- **Safe to depict:** head disk plus wrap wedge, no hair bump.

Four hair options.

---

### Category C — Head covering

The head wrap is the best-documented single garment in the sources and the
highest-value silhouette element at pawn scale.

#### C1. Putong — Head Wrap (plain)

- **Proposed label:** **Putong — Head Wrap**. The term clears the bar: Morga
  (1609) records *potong* as the Tagalog name of the narrow head kerchief, and
  the Visayan form *pudong* appears in the early Visayan lexical material Scott
  cites. Pair form is mandatory.
- **Description:** a narrow strip or kerchief of cloth wound tightly around
  the head over forehead and temples; sixteenth-century observers compared the
  fuller versions to turbans, "knotted very gracefully."
- **Date range:** 1521–1609. Pigafetta already describes kerchiefs about the
  head at Suluan and an embroidered head scarf on Humabon in 1521.
- **Scope:** Visayas and Tagalog Luzon at minimum; forms and widths varied.
- **Social and military context:** everyday male headgear worn also in war;
  the *color* of the wrap carried status meaning (see C2, C3).
- **Evidence tier:** Documented.
- **Primary source:** Pigafetta (1521); Loarca (1582), who describes showy
  head scarfs resembling turbans edged with gold; Boxer Codex plates
  (c. 1590); Morga (1609).
- **Directly supported:** the wrap itself, its tight winding, decorated
  elite versions.
- **Reconstructed:** exact winding patterns and regional style names.
- **Safe to depict:** a cloth-colored wedge or band across the head disk, in
  undyed cream, indigo, or black.
- **Must not generalize:** do not put the putong on every figure; bare heads
  are equally documented (C5), and northern Luzon figures in the Boxer Codex
  wear different headgear.

#### C2. Red head wrap of the proven warrior

- **Proposed label:** **Putong — Head Wrap** with a `Red — Proven in Battle`
  variant note in the inspector. The specific Visayan style name Scott
  transcribes for the red pudong of men who had killed an enemy is attested
  through early Visayan dictionaries; because the transcription and exact
  referent vary, the color rule, not the style name, is what this document
  endorses for player-facing text.
- **Description:** a red-dyed head wrap whose wearing was, in Visayan
  practice, an earned distinction associated with having taken an enemy life;
  more elaborate or long-tailed wraps marked still greater standing.
- **Date range:** early Visayan lexical attestation, c. 1600s dictionaries
  describing sixteenth-century practice.
- **Scope:** Visayas. Do not extend to Tagalogs, where Morga instead reports
  red *garments* as chiefly wear (see D3).
- **Social and military context:** a visible battle-honor system — exactly the
  kind of spectator-discoverable status mark Hukbo wants.
- **Evidence tier:** Documented, form uncertain (the practice is well
  attested in the early lexical record; the precise styles are not).
- **Primary source:** early Visayan dictionaries as synthesized in Scott,
  *Barangay*, ch. 2.
- **Later comparative source:** Scott (1994) is itself the synthesis; treat
  his readings as the citation chain to Méntrida and Sánchez.
- **Directly supported:** red head cloth as earned insignia in the Visayas.
- **Reconstructed:** exact criteria for earning it and exact form.
- **Safe to depict:** the C1 wedge in deep sappan red, reserved for agents
  whose metadata marks a kill record or veteran status.
- **Must not generalize:** never auto-assign red wraps to a whole faction;
  the entire point is that it was earned and therefore rare.

#### C3. Gold-edged head wrap of the elite

- **Proposed label:** **Putong — Head Wrap**, `Gold-Edged` variant.
- **Description:** head scarfs "edged with gold" on prominent men; Pigafetta's
  Humabon wore an embroidered scarf.
- **Date range:** 1521–1582.
- **Scope:** Visayas documented; plausible for Tagalog elites.
- **Evidence tier:** Documented.
- **Primary source:** Pigafetta (1521); Loarca (1582).
- **Safe to depict:** the C1 wedge with a one-pixel gold accent line.
- **Must not generalize:** elite only; co-occurs with other gold adornment
  (category I), not with slave status (see section 5).

#### C4. Woven sun hat

- **Proposed label:** Woven Sun Hat (plain English only). The now-familiar
  term *salakot* is not used: its attestation in that form was not confirmed
  inside the window, and Scott's early-dictionary hat terms are Visayan rain
  hats whose battlefield relevance is unclear. Plain English keeps the claim
  honest.
- **Description:** a broad woven palm or rattan hat against sun and rain.
- **Date range:** hat-wearing is attested in the early lexical record;
  battlefield use in the 1500s is not specifically documented.
- **Scope:** lowland farming and travel contexts.
- **Evidence tier:** Documented, form uncertain (as everyday headgear);
  Provisional reconstruction (as battle wear).
- **Primary source:** early vocabulary entries for woven hats via Scott;
  no battle account in the window describes one.
- **Safe to depict:** a wide flat disk over the head disk — visually loud, so
  use sparingly and never on elite figures.
- **Must not generalize:** do not make this a standard warrior item; it is a
  flavor option for levies at most, and its battlefield presence is a
  reconstruction.

#### C5. Bare head

- **Proposed label:** Bare Head.
- **Description:** no head covering; hair silhouette from category B shows.
- **Evidence tier:** Documented (Boxer Codex plates include bare-headed armed
  figures; Morga notes bare feet and legs and minimal covering generally).
- **Safe to depict:** default head disk.

#### C6. Feathered or plumed headdress (northern Luzon)

- **Proposed label:** Feathered Headdress (plain English; no local term
  confirmed inside the window).
- **Description:** the Boxer Codex Cagayan warrior wears a decorated headpiece
  with upright elements alongside long loose hair.
- **Date range:** c. 1590 (visual).
- **Scope:** Cagayan / northern Luzon figures only.
- **Evidence tier:** Documented (visually), form uncertain.
- **Primary source:** Boxer Codex, `Cagayan Warrior.png`.
- **Safe to depict:** a small vertical accent above the head disk, only on
  figures scoped to a northern Luzon roster.
- **Must not generalize:** never on Visayan or Tagalog presets; this is the
  clearest cross-regional mashup hazard in the whole system.

Six head-covering options.

---

### Category D — Torso garment

#### D1. Bare-chested

- **Proposed label:** Bare-Chested.
- **Description:** no upper garment. The single most common documented state
  for fighting men in the 1521 accounts; at Mactan, Pigafetta's account has
  the defenders unarmored, and the Spaniards aimed at bodies while the
  defenders learned to aim at legs.
- **Date range:** 1521–1609.
- **Scope:** archipelago-wide for labor and war; in the Visayas it also
  displays tattooing (I1–I3), which is precisely why it mattered.
- **Evidence tier:** Documented.
- **Primary source:** Pigafetta (1521); Boxer Codex Visayan plates (c. 1590).
- **Safe to depict:** skin-tone torso capsule; on Visayan tattooed presets, a
  darkened tone (see I1).
- **Must not generalize:** bare-chested does not mean low status — Humabon
  and Kolambu, both rulers, are described bare-chested with waist cloths and
  gold.

#### D2. Chinina — Collarless Jacket (blue or black)

- **Proposed label:** **Chinina — Collarless Jacket**. Morga (1609) records
  the garment and the name: a collarless jacket of *cangan* cloth, sewn in
  front, short-sleeved, reaching just past the waist, "some blue and some
  black." Attestation clears the bar.
- **Description:** a light short-sleeved jacket; the sixteenth-century cut is
  loose and hip-length.
- **Date range:** c. 1590–1609 (Boxer Codex jackets on Tagalog figures;
  Morga's description).
- **Scope:** Tagalog Luzon documented; Loarca describes comparable loose
  collarless jackets with sleeves in the Visayas made of *medriñaque* (abaca
  gauze) and silk.
- **Social and military context:** ordinary free-man's wear; not armor.
- **Evidence tier:** Documented.
- **Primary source:** Morga (1609); Loarca (1582); Boxer Codex plates.
- **Directly supported:** jacket, short sleeves, blue/black dye, front
  closure.
- **Reconstructed:** exact patterning and whether it was commonly worn into
  battle rather than shed.
- **Safe to depict:** an indigo or black torso capsule block.
- **Must not generalize:** do not dress every faction in jackets; the
  bare-chested state is at least as common in the battle-relevant sources.

#### D3. Red jacket of the chief

- **Proposed label:** **Chinina — Collarless Jacket**, `Red — Chiefly` variant.
- **Description:** Morga states that headmen wore red chininas.
- **Date range:** 1609, describing standing practice.
- **Scope:** Tagalog Luzon.
- **Evidence tier:** Documented.
- **Primary source:** Morga (1609).
- **Safe to depict:** sappan-red torso capsule, elite presets only.
- **Must not generalize:** the red-garment rule is Tagalog; the Visayan red
  honor mark is the head wrap (C2). Keep the two systems separate — merging
  them is a cross-regional mashup.

#### D4. Abaca gauze jacket (Visayan)

- **Proposed label:** Abaca Jacket (plain English; *medriñaque* is a Spanish
  trade term, not a local garment name).
- **Description:** Loarca's loose collarless jacket with tight sleeves whose
  skirts reach halfway down the leg, made of abaca gauze or colored silk.
- **Date range:** 1582.
- **Scope:** Visayas.
- **Evidence tier:** Documented, form uncertain.
- **Primary source:** Loarca (1582).
- **Safe to depict:** a cream or pale-ochre torso capsule, slightly longer
  than D2's block.
- **Must not generalize:** the silk versions were elite trade goods; default
  to undyed abaca.

Four torso options.

---

### Category E — Lower garment

#### E1. Bahag — Loincloth (plain)

- **Proposed label:** **Bahag — Loincloth**. Morga (1609) records *bahaque*;
  the term is also in the early Tagalog lexical record. Clears the bar.
- **Description:** a long cloth wrapped around the waist and passed between
  the legs; ends hang in front and behind.
- **Date range:** 1521–1609 (Pigafetta describes the cotton cloth covering
  the privates in 1521; every later source repeats the garment).
- **Scope:** archipelago-wide; the universal male lower garment in the
  sources.
- **Social and military context:** worn by everyone; quality and dye marked
  status, the garment itself did not.
- **Evidence tier:** Documented.
- **Primary source:** Pigafetta (1521); Boxer Codex plates; Morga (1609).
- **Safe to depict:** a narrow cloth-colored band at the capsule base; undyed
  cream default.
- **Must not generalize:** nothing to add — this is the safest single item in
  the system.

#### E2. Dyed, gold-edged loincloth of the elite

- **Proposed label:** **Bahag — Loincloth**, `Richly Dyed` variant.
- **Description:** Morga describes the chief's bahag as richly dyed cloth,
  generally edged with gold.
- **Date range:** 1609; consistent with Pigafetta's 1521 description of
  Kolambu's embroidered waist cloth.
- **Scope:** documented for Tagalog chiefs; elite dyed cloth is consistent
  across regions.
- **Evidence tier:** Documented.
- **Primary source:** Morga (1609); Pigafetta (1521).
- **Safe to depict:** the E1 band in deep red or indigo with a gold accent
  pixel.
- **Must not generalize:** elite presets only.

#### E3. Knee-length wrapped cloth

- **Proposed label:** Waist Cloth (plain English).
- **Description:** a larger cloth wrapped from waist to knees; Pigafetta
  describes Kolambu covered from waist to knees by a cotton cloth embroidered
  with silk, and Morga mentions a colored blanket wrapped at the waist.
- **Date range:** 1521–1609.
- **Scope:** documented on rulers and prominent men.
- **Evidence tier:** Documented.
- **Primary source:** Pigafetta (1521); Morga (1609).
- **Safe to depict:** a longer colored block at the capsule base, reading as
  "skirted" rather than "banded" at pawn scale.
- **Must not generalize:** do not read this as a poor man's garment; the
  documented wearers are elites in formal contexts. Whether one fought in it
  is uncertain — prefer E1/E2 on fighting presets and E3 on leader presets.

Three lower-garment options.

---

### Category F — Armor layer

Armor is where the evidence is thinnest relative to popular belief, and where
the accuracy policy does the most work. The 1521 Mactan account describes
effectively unarmored fighters. Armor evidence enters with the Legazpi-era
relations of 1565–1576 and is regionally uneven.

#### F1. No armor

- **Proposed label:** Unarmored.
- **Description:** the documented default. Fighting in bahag, with or without
  a shield.
- **Evidence tier:** Documented.
- **Primary source:** Pigafetta's Mactan account (1521).
- **Safe to depict:** categories A–E as-is.
- **Must not generalize:** "unarmored at Mactan in 1521" does not mean "never
  armored anywhere" — see F2–F4.

#### F2. Corded or quilted fiber armor (Visayan)

- **Proposed label:** Corded Fiber Armor (plain English). Scott records the
  Visayan term *barote* for corded or quilted body armor from the early
  lexical record; because this document could not independently confirm the
  attestation date of the term inside the window, the plain descriptor is
  used player-facing and the term is confined to inspector metadata pending
  review.
- **Description:** body armor of thick braided abaca or bark cord, quilted or
  knotted tightly; Spaniards compared it to the cotton *escaupil* armor they
  knew from the Americas.
- **Date range:** Legazpi-era descriptions, 1565–1576, and the early Visayan
  lexical record.
- **Scope:** Visayas.
- **Social and military context:** war gear proper — this is deliberate armor,
  not clothing.
- **Evidence tier:** Documented, form uncertain.
- **Primary source:** Legazpi-era relations in Blair and Robertson vols.
  II–III; early Visayan dictionaries via Scott, *Barangay*.
- **Later comparative source:** Scott (1994).
- **Directly supported:** the existence of corded/quilted fiber body armor.
- **Reconstructed:** coverage, thickness, closure, and appearance details.
- **Safe to depict:** a slightly widened torso capsule in pale ochre — the
  "thickened cream torso" reads as padded armor at pawn scale.
- **Must not generalize:** do not issue it broadly; armor was a minority kit,
  and nothing supports uniform issue.

#### F3. Carabao-hide corselet

- **Proposed label:** Hide Corselet (plain English).
- **Description:** a Legazpi-era crown report describes men "well armed for
  Indians," with corselets of buffalo (carabao) hide; related descriptions
  mention hardwood, bark, and horn elements.
- **Date range:** 1565–1576 relations.
- **Scope:** the quoted report concerns Luzon; hide armor more broadly is
  described in the relations without tight regional limits.
- **Evidence tier:** Documented (the class), form uncertain (the cut).
- **Primary source:** Blair and Robertson vols. II–III (Legazpi-era
  documents).
- **Safe to depict:** a widened torso capsule in warm brown.
- **Must not generalize:** do not model it as heavy plate; it is stiffened
  hide over a bare or cloth-clad torso.

#### F4. Hardwood or bark breastplate

- **Proposed label:** Wooden Breastplate (plain English).
- **Description:** the relations mention wood corselets and bark elements
  alongside rope armor.
- **Date range:** 1565–1576.
- **Scope:** regionally unspecific in the relations; treat as rare.
- **Evidence tier:** Documented, form uncertain.
- **Primary source:** Blair and Robertson vols. II–III.
- **Safe to depict:** a widened capsule in charred-wood dark brown.
- **Must not generalize:** rarity again; and never combine with invented
  European-style pauldrons or full coverage.

#### F5. Helmet set with fish bones and shells

- **Proposed label:** Shell-Set Helmet (plain English).
- **Description:** the same Legazpi-era report describes helmets set with
  fish bones and stout shells said to resist anything but the arquebus.
- **Date range:** 1565–1576.
- **Scope:** the quoted report's Luzon context.
- **Evidence tier:** Documented, form uncertain.
- **Primary source:** Blair and Robertson vols. II–III.
- **Safe to depict:** a pale accent cap on the head disk, on rare armored
  presets only.
- **Must not generalize:** an exceptional item in a single report family; use
  on at most one or two presets.

#### Explicitly excluded from the armor layer

- **Brass or bronze plate armor.** The familiar Moro brass-and-horn armor
  (*kurab-a-kulang* type) survives in eighteenth- and nineteenth-century
  objects and lacks a confirmed sixteenth-century attestation in this
  research. Under the hundred-year rule it is excluded from presets, exactly
  as the panabas was excluded from the armory. Metal armor in the 1500s
  sources belongs to Bruneian and Spanish opponents, not to the modeled
  lowland warriors.
- **Chain mail.** Mail appears in the sources as something *pierced by*
  Philippine hardwood lances (a Luzon account describes palm-wood lances hard
  enough to pierce mail), that is, as foreign equipment. Not a preset
  component.
- **Iron greaves.** Mentioned once in the same crown-report family as F3/F5;
  too thin and too anomalous to depict without further evidence. Record in
  the inspector as a research note only.

Five includable armor options plus a documented exclusion list.

---

### Category G — Belt, sash, and waist wrapping

#### G1. Red waist sash (Visayan)

- **Proposed label:** **Kandit — Waist Sash**, pending one review of the
  attestation chain: Scott records *kandit* as the Visayan sash from the
  early dictionaries. If review finds the earliest attestation outside the
  window, fall back to plain `Red Waist Sash`.
- **Description:** a long cloth sash wound at the waist, holding the bahag
  region visually together and carrying a sheathed blade.
- **Date range:** early Visayan lexical record describing sixteenth-century
  dress; sash lines are visible on Boxer Codex figures.
- **Scope:** Visayas.
- **Evidence tier:** Documented, form uncertain.
- **Primary source:** Boxer Codex plates (visual); early dictionaries via
  Scott.
- **Safe to depict:** a one-pixel red or indigo line across the capsule
  waist.
- **Must not generalize:** color symbolism of the sash is not established —
  the earned-red rule documented for the head wrap (C2) must not be silently
  copied onto the sash.

#### G2. Plain cloth belt

- **Proposed label:** Cloth Belt.
- **Description:** an undyed or dark waist tie securing the bahag and a
  dagger sheath.
- **Evidence tier:** Documented (visually — waist ties are ubiquitous in the
  Boxer Codex plates), form uncertain.
- **Safe to depict:** a neutral waist line.

#### G3. Woven rattan or cord belt

- **Proposed label:** Cord Belt.
- **Description:** a belt of plaited plant cordage rather than cloth.
- **Evidence tier:** Provisional reconstruction — cordage crafts are richly
  attested, a cordage *belt* as warrior wear is inferred, not described.
- **Safe to depict:** an ochre waist line.
- **Must not generalize:** keep it a minor variant.

Three sash options.

---

### Category H — Shoulder and waist accessories

#### H1. Sheathed side blade

- **Proposed label:** follows the weapons document (for example
  **Kalis — Thrusting Blade** carries its own pair-form label from combat
  preset V2).
- **Description:** a dagger or short blade in a wood or cloth sheath at the
  waist; Pigafetta describes gold-hilted daggers on Limasawa's ruler.
- **Evidence tier:** Documented.
- **Primary source:** Pigafetta (1521); Legazpi relations (1565–1569).
- **Safe to depict:** a short dark accent at the waist on figures whose main
  weapon is not the blade.
- **Must not generalize:** gold hilts are elite-only (category I rules
  apply).

#### H2. Draped shoulder cloth

- **Proposed label:** Shoulder Cloth.
- **Description:** a colored cloth or small blanket draped or slung over the
  shoulder or chest; drapes appear on Boxer Codex elite figures, and Morga
  describes colored blanket-cloths in Tagalog dress.
- **Date range:** c. 1590–1609.
- **Scope:** elite and formal contexts documented; battlefield wear
  uncertain.
- **Evidence tier:** Documented (as dress), form uncertain (in battle).
- **Safe to depict:** a diagonal color stripe across the capsule.
- **Must not generalize:** prefer on leader presets; sparing use elsewhere.

#### H3. Betel kit pouch

- **Proposed label:** Betel Pouch (plain English).
- **Description:** a small pouch or container for betel chewing materials,
  which every early account treats as pervasive in social life.
- **Evidence tier:** Documented (betel practice); Provisional reconstruction
  (a standard warrior belt pouch as its carrier).
- **Safe to depict:** invisible at pawn scale — inspector flavor text only.
- **Must not generalize:** do not render it; it exists to make inspector text
  humane rather than to change the sprite.

#### H4. Javelin bundle / back quiver

- Already governed by the weapons document (javelin bundle behind the
  shoulder; visible back quiver for archers). Listed here only so the
  appearance system reserves the shoulder slot for them. Evidence tier as in
  `HISTORICAL_1500s_WEAPONS.md`.

Four accessory options (two renderable).

---

### Category I — Social and status adornment

This category is the heart of the historical texture and also the most
policy-sensitive. Everything here is status-gated; see section 5.

#### I1. Full-body tattooing (Visayan)

- **Proposed label:** Full-Body Tattoos (plain English player-facing). The
  Visayan term *batuk* for tattoo appears in the early lexical record via
  Scott; the modern revival term *batok* is current usage. Given the layered
  transmission, keep the local term in inspector metadata with its chain, and
  the plain descriptor on the label.
- **Description:** dense geometric tattooing covering torso, back, arms, and
  legs, leaving hands and feet (and generally the face) bare. So defining
  that Spaniards called Visayans *Pintados*, "the painted ones."
- **Date range:** 1521–1604 continuously: Pigafetta repeatedly describes
  "painted" people including tattooed rulers (1521); Loarca (1582); Boxer
  Codex plates showing full patterns (c. 1590); Chirino (1604) noting bodies
  tattooed but faces untouched.
- **Scope:** **Visayas.** This is the single most important scope rule in
  the whole document. Tagalogs tattooed far less; northern Luzon traditions
  are separate and differently structured.
- **Social and military context:** tattoo coverage was earned and cumulative,
  tied to participation in raids and displays of courage — a readable war
  record on the skin.
- **Evidence tier:** Documented. (The *meaning* — earned coverage — is
  Documented via the early accounts and lexicon; specific motif meanings are
  not, and stay out of the game.)
- **Primary source:** Pigafetta (1521); Loarca (1582); Boxer Codex; Chirino
  (1604).
- **Later comparative source:** Scott, *Barangay*; the peer-reviewed study
  "Reading beneath the Skin: Indigenous Tattooing in the Early Spanish
  Philippines, ca. 1520–1720."
- **Directly supported:** dense body tattooing; status/courage association;
  Visayan scope.
- **Reconstructed:** motif vocabularies, application sequence details.
- **Safe to depict:** at pawn scale, tattooing is a *skin-tone shift* — a
  darker, cooler torso and limb tone on bare-chested Visayan presets,
  optionally with a single darker band. Do not attempt actual motifs at 8–16
  pixels; motif invention would assert detail the game cannot source.
- **Must not generalize:** never on Tagalog, Cagayan, or generic presets;
  never presented as decoration divorced from its earned meaning; inspector
  text must carry the evidence tier and the Visayan scope.

#### I2. Partial tattooing (chest and arms)

- **Proposed label:** Partial Tattoos.
- **Description:** the earned-coverage logic implies most fighting men were
  somewhere between untattooed and fully covered; early accounts and the
  lexical record support staged coverage growing with deeds.
- **Evidence tier:** Documented, form uncertain (the staging is attested in
  general terms; exact stages are not).
- **Scope:** Visayas.
- **Safe to depict:** the I1 tone shift applied to arms/upper torso only.
- **Must not generalize:** same rules as I1.

#### I3. Facial tattooing (rare, contested)

- **Proposed label:** none — inspector-only if used at all.
- **Description:** sources disagree. Chirino says the face was not touched;
  Scott's reading of Visayan material has facial tattooing as a mark of the
  most extreme warriors. At pawn scale the face does not render anyway.
- **Evidence tier:** Documented, form uncertain, sources in conflict.
- **Recommendation:** exclude from rendering; permissible as one line of
  inspector text on at most a single veteran preset, carrying the conflict
  note. The safest default is omission.

#### I4. Gold earrings and ear plugs

- **Proposed label:** Gold Earrings (plain English; the Visayan ornament
  vocabulary Scott records, such as *panika*, stays in inspector metadata
  pending attestation review).
- **Description:** large gold (and ivory — Loarca) earrings, worn by men and
  women, sometimes multiple per ear, sometimes stretching the lobe.
- **Date range:** 1521–1609 continuously, from Pigafetta's first landfall
  onward.
- **Scope:** archipelago-wide among lowland trading societies; density and
  size tracked wealth.
- **Evidence tier:** Documented.
- **Primary source:** Pigafetta (1521); Loarca (1582); Boxer Codex plates;
  Chirino (1604); Morga (1609). Also object-documented: pre-colonial gold
  ornament corpus (Ayala Museum "Gold of Ancestors" collection; Surigao
  Treasure).
- **Safe to depict:** a single gold pixel beside the head disk on elite and
  veteran presets.
- **Must not generalize:** gold density is status-graded; a slave-status
  figure with gold ornaments contradicts every account.

#### I5. Gold neck chains and necklaces

- **Proposed label:** Gold Necklace (plain English; *kamagi*, the massive
  Visayan gold necklace type, is attested in later Visayan sources and the
  object record — keep it in the inspector labeled with its chain, not on
  the pawn-facing label, pending attestation review).
- **Description:** Morga describes long chains of engraved gold links worn
  around the neck; Pigafetta saw necklaces on Cebu's ruler; the surviving
  pre-colonial gold corpus confirms the craft at the highest level.
- **Date range:** 1521–1609; object corpus is pre-1521 archaeology.
- **Scope:** elite, archipelago-wide in trading polities.
- **Evidence tier:** Documented.
- **Primary source:** Pigafetta (1521); Morga (1609); Ayala Museum object
  collection (archaeological).
- **Safe to depict:** a gold accent line at the capsule top, leaders only.
- **Must not generalize:** leaders and the wealthiest only.

#### I6. Gold armlets

- **Proposed label:** Gold Armlets (plain English; Morga's recorded term
  *colombigas* is a candidate pair-form name — it clears the bar via Morga
  1609 — but at pawn scale armlets do not render, so the pair form is
  reserved for the inspector: `Kolombiga — Gold Armlet`, flagged for spelling
  review).
- **Description:** thick engraved gold bracelets on the arms; Pigafetta also
  records gold armlets at first contact.
- **Date range:** 1521–1609.
- **Evidence tier:** Documented.
- **Primary source:** Pigafetta (1521); Morga (1609).
- **Safe to depict:** not renderable at pawn scale; inspector text.

#### I7. Gold dental work

- **Proposed label:** inspector-only: Gold-Worked Teeth.
- **Description:** gold inlays, pegs, and coverings on teeth, noted by
  Pigafetta on rulers and studied archaeologically; the general Visayan term
  recorded for tooth goldwork is *pusad* in the scholarly literature.
- **Date range:** 1521 (eyewitness) plus pre-colonial archaeology.
- **Scope:** Visayas strongly documented; Tagalog region also attested via
  Chirino.
- **Evidence tier:** Documented.
- **Primary source:** Pigafetta (1521); Chirino (1604); archaeological
  studies of Visayan dental goldwork.
- **Safe to depict:** invisible; one humane line of inspector text on elite
  presets.

#### I8. Tooth filing and blackening

- **Proposed label:** inspector-only.
- **Description:** filed and blackened teeth as beauty practice, documented
  in the early accounts and studied archaeologically.
- **Evidence tier:** Documented.
- **Safe to depict:** invisible; inspector text only.

Eight adornment options: four are renderable at pawn scale (I1, I2, I4, and
I5, where the two tattoo options I1 and I2 share a single skin-tone-shift
channel) and four are inspector texture only (I3, I6, I7, and I8). An
earlier draft of this tally read "three renderable, five as inspector
texture"; that count did not match the option annotations above and was
corrected during independent review (finding RF-07).

---

### Category J — Natural-material palette

The dye and material evidence supports a small, consistent palette. The
weapons document's palette remains the base; this extends it with dye-specific
entries. Dye plants below are attested in the early textile vocabulary record
and continuous later practice; the *availability* of each color class in the
1500s is Documented via the garment descriptions themselves (blue, black, red,
white/cream, and gold thread all appear in the 1521–1609 accounts), while the
identification of each color with a specific plant process is Documented, form
uncertain, resting partly on the early vocabularies and partly on continuous
later practice.

| Swatch role | Suggested value | Material basis | Evidence note |
| --- | --- | --- | --- |
| Undyed abaca / cotton cream | `#E7D8B7` | abaca (Manila hemp), cotton | Documented — the default cloth state |
| Indigo blue | `#354D6B` | *tayum / tagum* (Indigofera) | Blue garments Documented (Morga); indigo-plant identification Documented, form uncertain |
| Deep blue-black | `#2A3140` | saturated indigo / mud-tannin black | Black garments Documented (Morga) |
| Sappan red | `#8F3F35` | *sibukaw* (sappanwood) | Red garments and red status cloth Documented; plant identification Documented, form uncertain |
| Turmeric yellow | `#C9A23F` | *dilaw* (turmeric) | Yellow attested in the dye vocabulary record; use sparingly — no 1500s account puts yellow on a warrior |
| Bark brown | `#7A5A3A` | bark cloth, hide | Documented (hide and bark armor descriptions) |
| Skin tones | project-defined range | — | Pigafetta describes "tawny" complexions; use a natural range, never faction-coded |
| Tattoo-darkened skin | skin tone shifted darker/cooler | soot-based tattoo pigment | Documented practice; the tone-shift rendering is a design abstraction |
| Gold accent | `#D0A64A` | gold ornament corpus | Documented |
| Iron blue-black | `#384249` | blade metal | from the weapons palette |

Faction color stays on the ground ring, outline, or a designated cloth band, as
the weapons document already requires, so garments keep natural colors.

Ten palette entries.

---

### Category K — Weathering and condition

This category is **game presentation, not history**. No source describes the
soiling state of a sixteenth-century warrior's bahag, and this document makes
no historical claim here. The options exist so the component system can show
battle wear without inventing historical detail:

- **K1. Clean** — default.
- **K2. Dusty / muddy** — desaturate and darken lower garment and legs.
- **K3. Sweat sheen** — slight highlight on bare skin; tropical plausibility
  only.
- **K4. Faded dye** — washed-out garment color on long-serving units.
- **K5. Battle-worn** — combination of K2 and K4 plus disordered hair (B1
  knot rendered loosened).

Evidence tier for all of K: not applicable — presentation layer. These options
must never be described to the player as historical detail, and none of them
may leak into simulation state.

Five condition options.

---

## 4. Combination rules

The component system must forbid combinations the evidence forbids. These
rules are the review checklist for every generated preset.

### Co-occurrence rules (documented pairings)

1. **Visayan tattooed presets (I1/I2) pair with bare chest (D1).** The tattoo
   is the display; covering it with a jacket erases the point. A tattooed
   figure under armor (F2/F3) is acceptable — armor is worn over skin — but
   the tattoo tone then shows only on arms.
2. **Red head wrap (C2) requires a veteran/kill marker in agent metadata.**
   It is an earned insignia, never a random roll.
3. **Gold (C3, I4, I5) clusters.** A figure with a gold-edged putong should
   also roll gold earrings; the sources show gold as an ensemble of wealth,
   not a single stray item.
4. **Red chinina (D3) implies Tagalog elite scope** and pairs with E2 and
   gold adornment.
5. **Elite waist cloth (E3) belongs on leader presets** with H2 shoulder
   cloth as an optional pairing — the Boxer Codex elite figures read as
   "more cloth, more color, more gold" as one package.
6. **Armor options (F2–F5) pair with plain kit.** Every armor description in
   the relations is about fighting equipment, not display; do not stack heavy
   gold ensembles on armored presets.

### Prohibited combinations (anachronisms and mashups)

1. **Cagayan feathered headdress (C6) on Visayan or Tagalog presets.**
   Cross-regional mashup; the single clearest forbidden combination.
2. **Full-body tattoos (I1/I2) outside Visayan-scoped presets.** The
   Pintados scope rule.
3. **Red putong (C2) on Tagalog presets, red chinina (D3) on Visayan
   presets.** Two different documented status-color systems; do not blend.
4. **Brass or bronze plate armor, chain mail, iron greaves** — excluded
   entirely (category F exclusion list; hundred-year rule and foreign
   attribution).
5. **The term *salakot*, and battlefield sun hats on elite figures** — the
   name fails confirmation inside the window; the hat itself (C4) is levy
   flavor at most.
6. **Gold ensemble on low-status presets.** Contradicts every account of who
   wore gold; see section 5.
7. **Tattoo motifs rendered as recognizable patterns.** Motif-level detail
   is unsourceable at pawn scale; only the tone shift is permitted.
8. **European elements** — helmets with crests, doublets, boots, plate —
   never on the modeled warriors. Footwear generally: the sources describe
   bare feet (Morga explicitly); no preset gets shoes.
9. **Later Moro-specific kit (brass armor, later sultanate dress)** on
   1500s lowland presets — a period and culture mashup in both directions.
10. **One region's costume presented as "the Philippines."** Every preset
    carries a scope tag (Visayan, Tagalog, Cagayan, or Unscoped-generic)
    shown in the inspector, mirroring the weapons document's inspiration
    tags such as `Mactan — 1521`.

### Coverage arithmetic

With the options above (4 hair × 6 head coverings × 4 torso × 3 lower × 5
armor states × 3 sashes × renderable accessories × 3 renderable adornment
states × 5 conditions), the raw space is in the tens of thousands; after the
scope and co-occurrence rules cut it down, the reviewable space still yields
well over fifty distinct presets. A practical first wave:

- **Visayan block (~20 presets):** bare-chested tattooed spearmen, archers,
  and blade fighters at three tattoo-coverage stages, with putong variants
  including earned red; two corded-fiber-armor veterans; one datu leader
  ensemble.
- **Tagalog block (~15 presets):** jacketed (blue/black) spearmen and
  archers, bare-chested laborers-at-war, one red-chinina chief, one
  hide-corselet veteran, one shell-set-helmet rarity.
- **Northern Luzon block (~8 presets):** loose-haired Cagayan-referenced
  spearmen with tall shields, Zambal-referenced archers (Boxer Codex
  silhouettes), no tattoo tone.
- **Generic levy block (~10 presets):** minimal kit, undyed cloth, mixed
  builds and conditions, deliberately unscoped and so carrying no cultural
  markers at all.

That is fifty-plus reviewable presets with zero palette-swap padding: each
differs in silhouette or documented color logic, not merely hue.

## 5. Social stratification and visible appearance

The three-tier social vocabulary of the early sources — Visayan *datu /
timawa / oripun*, Tagalog *maginoo / timawa / alipin* — is Documented as
social structure (Loarca, Morga, Boxer Codex text, synthesized by Scott). What
this document endorses visually is narrower: only the appearance claims the
sources actually make.

- **Datu / maginoo (chiefly class).** Documented visible markers: gold
  ensemble (earrings, chains, armlets), richly dyed and gold-edged cloth
  (E2/E3), embroidered or gold-edged head wrap (C3), red chinina among
  Tagalog headmen (D3), gold-hilted side blades (H1 elite variant), and in
  the Visayas, extensive earned tattooing (I1) — Pigafetta's tattooed,
  gold-adorned rulers show elite status and tattoo coverage reinforcing each
  other. Depict leaders as *denser in gold and dye*, not larger in body.
- **Timawa (free warrior class).** The sources tie Visayan timawa to raiding
  service and personal followership. Safe visible expression: mid-range kit —
  partial tattoos (I2, Visayas), plain putong, maybe one gold accent,
  fiber armor on veterans. No source gives the timawa a uniform; do not
  invent one.
- **Oripun / alipin (dependent/bonded class).** The sources describe
  obligation and dependency, not a costume. The safe visible expression is
  *absence*: undyed cloth, no gold, no earned insignia, minimal kit. Do not
  invent a distinctive "slave costume" — that would be an unsupported and
  ethically loaded fabrication. Whether bonded dependents fought at all, and
  how equipped, is uncertain; if the roster includes them as porters or
  levies, the inspector must say the depiction is a reconstruction.
- **Women.** Early accounts document women's dress and adornment richly.
  Combat composition is evaluated separately in
  [Gender and Warrior Composition in the Late-Sixteenth-Century
  Philippines](../HISTORICAL_1500s_WARRIOR_GENDER.md); this appearance study
  makes no independent claim about female combat dress.

The stratification display rule in one sentence: **status is shown by adding
documented wealth markers to a common base, never by inventing class
uniforms.**

## 6. Explicit gaps and unknowns

- No source in the window describes **how armor was fastened, its coverage
  edges, or its weight** — all armor renderings are silhouette-level
  reconstructions of documented classes.
- **Regional coverage is uneven:** Visayas and Tagalog Manila dominate;
  Mindanao and Sulu lowland warrior dress in the 1500s is thin in this source
  set and deliberately unmodeled; Cagayan rests on a handful of Boxer Codex
  plates.
- **Tattoo motifs** are visually attested (Boxer Codex) but not decodable;
  the game abstains from motif rendering.
- **Battlefield versus formal dress:** several documented items (E3 waist
  cloth, H2 shoulder cloth, silk jackets) are formal-context descriptions;
  their presence in battle is inference.
- **The exact attestation dates** of *barote*, *kandit*, *panika*, *kamagi*,
  and *batuk* inside the window rest on Scott's citations of early
  dictionaries this research did not independently open; each is flagged
  above and must be reviewed before its term is promoted from inspector
  metadata to a player-facing pair-form label.
- **Footwear:** none documented; bare feet are the rule.
- **Headhunting-related regalia** of northern Luzon interior societies is a
  separate, later-documented tradition and is out of scope entirely.

## 7. Source register with licensing

### Sixteenth- and early-seventeenth-century primary sources

1. [Pigafetta's account in English translation (Wikisource)](https://en.wikisource.org/wiki/The_First_Voyage_Round_the_World/Pigafetta%27s_Account_of_Magellan%27s_Voyage)
   — 1521 eyewitness: tattoos, gold earrings and armlets, head kerchiefs,
   waist cloths, gold teeth, unarmored fighting at Mactan. Public domain.
2. [The Philippine Islands, 1493–1803, Volume II (Project Gutenberg #13280)](https://www.gutenberg.org/ebooks/13280)
   — Legazpi-era documents 1565–1567. Public domain.
3. [The Philippine Islands, 1493–1803, Volume III (Project Gutenberg #13616)](https://www.gutenberg.org/ebooks/13616)
   — relations 1569–1576, including armor, corselet, and helmet
   descriptions. Public domain.
4. Loarca, *Relación de las Islas Filipinas* (1582), in Blair and Robertson
   Volume V — Visayan dress, hair, jackets, gold-edged head scarfs, ivory and
   gold earrings. Public domain text; locate via the
   [Project Gutenberg Blair & Robertson series](https://www.gutenberg.org/ebooks/search/?query=philippine+islands+1493).
5. Chirino, *Relación de las Islas Filipinas* (1604), in Blair and Robertson
   Volumes XII–XIII — tattooing ("faces not touched"), gold jewelry
   including dental gold. Public domain text, same series.
6. [Morga, *Sucesos de las Islas Filipinas* (1609), Internet Archive scan](https://archive.org/details/ahz9387.0001.001.umich.edu)
   — the fullest Tagalog dress description: chinina, bahaque, potong, gold
   chains, colombigas, bare feet, red for headmen. Public domain.

### Late-century visual material

7. [Wikimedia Commons: Boxer Codex image category](https://commons.wikimedia.org/wiki/Category:Boxer_Codex)
   — plates including `Visayans_1.png` through `Visayans_4.png` (tattooed
   Pintados couples and warriors), `Cagayan Warrior.png`, `Cagayan
   Woman.png`, `Zambals_1.png`–`Zambals_3.png`, `Negritos.png`, and
   `Naturales_1.png`–`Naturales_5.png` (Tagalog elite figures). The
   manuscript is c. 1590; the underlying works are public domain and the
   Commons reproductions are hosted as such, but **each file's description
   page must be checked individually before any plate is redistributed**, per
   the category's own licensing note. Use as silhouette and color guides
   only, per the Boxer Codex caution in `HISTORICAL_1500s_WEAPONS.md`.
8. [Indiana University Lilly Library: Boxer Codex provenance](https://blogs.libraries.indiana.edu/lilly/2015/11/02/boxer-codex-on-exhibit-at-new-york-asia-society/)
   — manuscript context and limitations. Page text under the library's
   copyright; cite, do not copy.
9. [Critical study of the Boxer Codex's pictorial sources (eHumanista)](https://www.ehumanista.ucsb.edu/sites/secure.lsit.ucsb.edu.span.d7_eh/files/sitefiles/ehumanista/volume40/ehum40.romero.pdf)
   — why the plates guide silhouette, not technical detail. Open-access
   article; in copyright; cite only.

### Modern scholarship (comparative and synthetic; all in copyright — cite, never copy)

10. Scott, William Henry. *Barangay: Sixteenth-Century Philippine Culture and
    Society*. Ateneo de Manila University Press, 1994. The standard synthesis
    of the early dictionaries and relations on appearance, dress, tattooing,
    and social structure. [Internet Archive lending copy](https://archive.org/details/BarangaySixteenthCenturyPhilippineCultureAndSociety);
    in copyright — reference only, and every claim taken through Scott is
    flagged as such above.
11. ["Reading beneath the Skin: Indigenous Tattooing in the Early Spanish
    Philippines, ca. 1520–1720"](https://www.academia.edu/101568103/Reading_beneath_the_Skin_Indigenous_Tattooing_in_the_Early_Spanish_Philippines_ca_1520_1720)
    — recent peer-reviewed study of the tattooing source record. In
    copyright; cite only.
12. [Early Aesthetic Dentistry in the Philippines: An Anthropological
    Perspective (Acta Medica Philippina)](https://actamedicaphilippina.upm.edu.ph/index.php/acta/article/download/1204/1078/)
    — archaeological and historical study of tooth goldwork (*pusad*),
    filing, and blackening. Open-access journal article; in copyright; cite
    only.
13. [Ayala Museum: Gold of Ancestors collection](https://www.ayalamuseum.org/)
    — the pre-colonial gold object corpus (including the Surigao Treasure)
    documenting earrings, chains, sashes, and regalia at object level.
    Website and photography in copyright; the objects are evidence, the
    photos are not assets.
14. [National Museum of the Philippines: Ethnology collections](https://www.nationalmuseum.gov.ph/our-collections/ethnology/)
    — later comparative material culture. Site content in copyright.
15. [HABI Philippine Textile Council: Philippine Natural Dyes overview](https://www.habiphilippinetextilecouncil.com/blogs/habi-highlights/philippine-natural-dyes-a-short-overview)
    and
    ["Textile terms in early Philippine vocabularies" (Academia.edu)](https://www.academia.edu/124813077/Textile_terms_in_early_Philippine_vocabularies_Preliminary_reconstructions_in_the_culture_of_cloth_from_the_17th_to_the_19th_century)
    — dye plants (*tayum/tagum* indigo, *sibukaw* sappan red, *dilaw*
    turmeric) and the early textile lexicon. In copyright; cite only.

### Image licensing summary for external visual references

Only the Boxer Codex plates on Wikimedia Commons are candidates for direct
visual reference reuse, as reproductions of a c. 1590 public-domain
manuscript, subject to per-file verification on Commons. Every other visual
source in this register (museum photography, book plates, article figures) is
in copyright and may be consulted but not copied into the repository or the
game. Hukbo's pawns are original procedural art in any case; no external
image is traced or imported.

## 8. Relationship to existing documents

- `../HISTORICAL_1500s_WEAPONS.md` remains the authority for weapon and
  shield silhouettes, the pair-form labels of combat preset V2, and the base
  palette; this document extends, and must never contradict, its rules.
- The deep-past research (`../battles/01`–`04`) warns that pre-1521
  perishable equipment is archaeologically invisible; everything in this
  document therefore rests on the 1521–1609 documentary window and must not
  be projected backward into the deep-past track.
- No appearance component defined from this document may affect simulation
  state, hashes, or AI — appearance is presentation-only, per the placeholder
  body variety rules and the determinism contract.
