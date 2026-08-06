# Historical Philippine Weapons of the 1500s

Research date: 2026-07-26

Purpose: provide an evidence-backed visual reference for Hukbo's first
placeholder unit and armory UI. This document does not define gameplay stats,
combat balance, or final historical reconstructions.

## Research boundary

"The Philippines in the 1500s" did not have one uniform military culture.
Evidence varies by place and decade. The strongest surviving accounts used here
cover:

- Mactan and Cebu in 1521;
- Visayan and Luzon encounters from 1565 to 1572;
- Manila, Mindoro, Panay, Cainta, and nearby maritime trade networks around
  1570; and
- late-century clothing and equipment in the Boxer Codex, compiled in Manila
  around 1590-1595.

Spanish observers used imprecise labels and wrote from a colonial perspective.
Their descriptions are useful evidence for equipment, but not neutral
ethnography. The Boxer Codex is valuable visual evidence, but its Chinese
pictorial influences and European patronage mean that it should guide broad
silhouettes, clothing, color, and ornament rather than be treated as an exact
technical catalog.

### Confidence labels

- **Documented:** directly described or pictured in a sixteenth-century source.
- **Documented, form uncertain:** the weapon class is attested, but its exact
  local form or name is uncertain.
- **Provisional reconstruction:** a plausible identification supported by later
  traditions or objects, but not firmly established for the cited event.

## Recommended first-wave armory

| Weapon | Sixteenth-century basis | Placeholder UI label | Readable visual language | Confidence |
| --- | --- | --- | --- | --- |
| Long iron-tipped lance | Pigafetta records iron-pointed lances at Mactan in 1521. A 1569 relation describes long lances with broad iron or steel points. | **Bangkaw - Long Spear** | Very long dark palm or rattan shaft, oversized leaf-shaped steel point, carried diagonally beyond the body. | Documented weapon class. "Bangkaw" is a plausible local name and should retain its English descriptor. |
| Fire-hardened throwing spear or javelin | Pigafetta distinguishes javelins, fire-hardened spears, and iron-pointed lances. A Luzon account describes palm-wood lances hard enough to pierce mail. | **Hardened Javelin** | Shorter and thinner than the long spear, warm brown shaft, charred dark tip, optional bundle behind the shoulder. | Documented; exact local terminology and distribution are uncertain. |
| Bow and reed arrows | Bows and arrows appear at Mactan. Legazpi later sent a native bow, quiver, and arrows as weapon specimens. A 1569 account describes large bows, reed shafts, hardwood points, and unfeathered arrows. Boxer Codex Zambal figures provide late-century visual references. | **Busog - War Bow** | Tall bow arc outside the torso silhouette, pale reed arrows, dark points, clearly visible back quiver. | Documented. Poisoned arrows are attested but must not be presented as universal. |
| Broad dagger or cutlass | Legazpi described cutlasses and daggers in common use in 1565 and sent specimens in 1567. A 1569 relation compares their broad shape to lance points. | **Broad Dagger** or **Cutlass** | Wide leaf-shaped steel blade, short wood grip, cloth or carved-wood sheath at the waist. | Documented, form uncertain. Avoid assigning a later specific type without object-level evidence. |
| Large cutting sword | Pigafetta describes the weapon that struck Magellan as a great sword comparable to a great scimitar, but does not give a local name. | **Great Blade** | Long, single-edged, forward-heavy silhouette with a widening tip and carved dark-wood hilt. | Documented weapon class. Identification as a kampilan is provisional. |
| Blowgun and darts | A 1569 relation explicitly mentions poisoned darts or arrows discharged through blowguns at shorter range than bows. | **Blowgun** | Long, straight, narrow tube held horizontally, with a small dart bundle. | Documented; regional prevalence is uncertain. |
| Chinese arquebus in local use | Legazpi wrote in 1567 that some local fighters possessed Chinese arquebuses and were skilled with them, especially aboard praus. | **Imported Arquebus** | Long timber stock, dark iron barrel, horizontal pose, small glowing matchcord, and an `IMPORTED` badge. | Documented existence; exact form and prevalence are uncertain. It should be rare. |
| Locally cast bronze culverin or verso | Manila-area forces used small artillery in 1570. Legazpi sent two locally made bronze versos to Spain as examples of metalworking skill. Culverins also appeared in forts and boats. | **Bronze Verso** or **Small Culverin** | Warm bronze barrel, hardwood swivel or yoke, wide crew-served footprint, restrained smoke and recoil. | Documented. This is an emplacement or crew weapon, not an individual firearm. |

## Defensive equipment

Defensive equipment should be part of the placeholder silhouettes because it
makes roles legible even when units are small.

- **Tall curved shield:** inspired by the late-sixteenth-century Boxer Codex
  Cagayan warrior.
- **Narrow breast-high shield:** suited to spear infantry; Spanish accounts
  describe shields or bucklers and give examples approaching breast height.
- **Small or no shield:** reserved for archers, blowgunners, arquebusiers, and
  artillery crew.
- **Cotton, rattan, hide, and dark-wood armor:** sixteenth-century descriptions
  support these materials. They offer visual variety without inventing uniform
  European plate armor.

## Named blade caution

The National Museum of the Philippines documents the cultural importance and
use of the kampilan, kris, bolo, shields, spears, axes, knives, and lantaka.
These traditions are valuable references, but most cataloged surviving objects
are considerably later than the sixteenth century.

For Hukbo's UI:

- do not state that a kampilan definitively killed Magellan;
- do not use kampilan, kris or kalis, barong, or bolo as blanket names for
  weapons across every region;
- present a local name only in **pair form** — the Filipino name, an em dash,
  and a plain English descriptor, as in **Kampilan — Great Blade** — and never
  as a bare label. The descriptor is what the game guarantees; the Filipino
  name is what the tradition offers. Every pair-form label carries an evidence
  tier shown in the agent inspector;
- do not use a name whose earliest attestation postdates the depicted period
  by more than a century. This excluded the **panabas**, first documented in
  nineteenth-century Spanish accounts of Moro resistance and surviving in
  objects dated to the eighteenth and nineteenth centuries — roughly a
  three-hundred-year gap. A `PROVISIONAL` badge is a reasonable instrument for
  "the class is attested but this identification is a reconstruction"; it is
  not a reasonable instrument for a gap of that size, and using it that way
  would drain the badge of meaning everywhere else it appears. The
  forward-weighted chopping role went to the **wasay**, a hafted axe, instead;
- use **Bronze Verso** or **Small Culverin** for the documented 1570 artillery
  before adopting the more familiar `lantaka` label; and
- keep plain-English descriptors beside local or provisional terms.

Later museum examples remain useful for materials, proportions, carving, and
silhouette, but should be marked as later comparative references.

## Visual direction for Hukbo units

The relevant lesson from RimWorld is rapid pawn recognition in a busy top-down
simulation, not its exact pawn contour, textures, portraits, palette, or UI.
Hukbo should use an original visual grammar:

1. a simple head disk with a hair or headcloth wedge;
2. a torso capsule whose height and width can vary;
3. a weapon silhouette extending clearly beyond the body; and
4. a team-colored ground ring plus a non-color selection or status mark.

The weapon silhouette should communicate the role before clothing color:

| Role | Primary silhouette |
| --- | --- |
| Spear fighter | Longest diagonal line |
| Javelin fighter | Short diagonal weapon plus rear bundle |
| Archer | Bow arc and quiver |
| Great-blade fighter | Broad, forward-heavy blade |
| Dagger fighter | Compact close stance |
| Arquebusier | Long horizontal stock and barrel |
| Shield bearer | Tall solid block beside the torso |
| Culverin crew | Wide mechanical emplacement footprint |

Faction color should remain the strongest battle-scale cue. Apply it mainly to
the ground ring, outline, cloth band, or shield mark so clothing can retain
natural and region-inspired colors.

### Placeholder body variety

Body variety is presentation-only in the first pass:

- **Stature:** short, average, tall.
- **Build:** slight, average, broad.
- Reserve **overall size** for a later `big` attribute instead of treating it as
  the same thing as broad or heavy.
- Anchor every character at the feet.
- Stature changes torso height and head offset.
- Build changes torso width.
- Head size and weapon reach remain visually stable.
- No appearance value changes hitboxes, movement, range, damage, health,
  simulation hashes, or AI.

Suggested placeholder multipliers:

| Dimension | Small | Default | Large |
| --- | ---: | ---: | ---: |
| Stature / vertical torso spacing | 0.90 | 1.00 | 1.10 |
| Build / torso width | 0.86 | 1.00 | 1.18 |
| Head scale | fixed | fixed | at most +/-3% later |
| Weapon scale | fixed per weapon class | fixed | fixed |

## Placeholder roster and armory UI

### Unit card

- 48-56 px procedural character portrait.
- Unit name or stable ID and visual role.
- Large weapon silhouette.
- Evidence-aware inspiration tag, such as `Mactan - 1521` or
  `Manila - c.1570`.
- Reserved slots for later body and equipment attributes.
- Health and state shown by shape or text as well as color.

### Armory card or tooltip

- Original weapon silhouette.
- Local or provisional label plus a plain-English descriptor.
- Range category: melee, thrown, ranged, or crew-served.
- Material chips: iron, palm wood, reed, bronze, rattan, cotton, or hide.
- Evidence badge: `DOCUMENTED`, `FORM UNCERTAIN`, or
  `PROVISIONAL RECONSTRUCTION`.
- A short source note instead of unsupported flavor text.

## Palette reference

| Material or accent | Color |
| --- | --- |
| Abaca and cotton cream | `#E7D8B7` |
| Palm and rattan ochre | `#A8743C` |
| Charred wood | `#302821` |
| Iron blue-black | `#384249` |
| Cast bronze | `#B47A3C` |
| Dyed indigo | `#354D6B` |
| Deep textile red | `#8F3F35` |
| Gold ornament | `#D0A64A` |
| Tropical patina green | `#517064` |

## Avoid in the first pass

- Flintlocks, percussion firearms, and other later firearm mechanisms.
- Modern standardized bolos presented as a single sixteenth-century type.
- Uniform European drill formations or nationally standardized equipment.
- Universal poison effects for every bow or blowgun.
- One costume or weapon set presented as representative of the entire
  archipelago.
- Historical labels that imply more certainty than the evidence supports.
- Imported or later museum artwork copied directly into Hukbo.

## Sources and visual references

### Sixteenth-century and near-primary material

1. [Library of Congress: Pigafetta's Journal of Magellan's Voyage](https://www.loc.gov/resource/gdcwdl.wdl_03082/?st=grid)
   - Manuscript provenance and 1521 eyewitness context.
2. [Pigafetta's account in English translation](https://en.wikisource.org/wiki/The_First_Voyage_Round_the_World/Pigafetta%27s_Account_of_Magellan%27s_Voyage)
   - Mactan account: shields, arrows, javelins, fire-hardened spears,
     iron-pointed lances, poisoned arrows, and a large cutting sword.
3. [The Philippine Islands, 1493-1803, Volume II](https://www.gutenberg.org/ebooks/13280)
   - Legazpi-era documents covering cutlasses, daggers, lances, bows, Chinese
     arquebuses, armor, and weapon specimens sent in 1567.
4. [The Philippine Islands, 1493-1803, Volume III](https://www.gutenberg.org/ebooks/13616)
   - Accounts from 1569-1576 covering bows, lances, daggers, shields,
     blowguns, fortifications, locally cast artillery, and the 1570 Manila
     campaign.
5. [Philippine eLib scan of Volume III](https://www.elib.gov.ph/downloadfile.php?uid=94a844b6ef7427db1fd8f3f7826ed197)
   - Alternate searchable scan of the same translated documentary collection.

### Late-century visual material

6. [Indiana University Lilly Library: Boxer Codex overview](https://blogs.libraries.indiana.edu/lilly/2015/11/02/boxer-codex-on-exhibit-at-new-york-asia-society/)
   - Provenance and limitations of the circa-1595 manuscript.
7. [Wikimedia Commons: Boxer Codex image category](https://commons.wikimedia.org/wiki/Category:Boxer_Codex)
   - Browsable visual reference set.
8. [Boxer Codex: Cagayan warrior](https://commons.wikimedia.org/wiki/File:Cagayan_Warrior.png)
   - Long spear, tall shield, headdress, and jewelry reference.
9. [Boxer Codex: Zambal figures](https://commons.wikimedia.org/wiki/File:Zambals_1.png)
   and [second plate](https://commons.wikimedia.org/wiki/File:Zambals_2.png)
   - Archer and hunter silhouettes.
10. [Critical study of the Boxer Codex's pictorial sources](https://www.ehumanista.ucsb.edu/sites/secure.lsit.ucsb.edu.span.d7_eh/files/sitefiles/ehumanista/volume40/ehum40.romero.pdf)
    - Reason to use the illustrations critically rather than as exact
      documentary photography.

### Later comparative collections

11. [National Museum of the Philippines: Weapons and Shields](https://www.nationalmuseum.gov.ph/our-collections/ethnology/weapons-and-shields/)
    - Cultural context for long blades, shields, spears, armor, materials,
      social status, and regional traditions.
12. [Metropolitan Museum of Art: later kampilan](https://www.metmuseum.org/art/collection/search/27824)
    - An 18th-19th-century object useful for comparative silhouette and
      materials, not proof of an identical 1521 form.

## Current recommendation

The safest and most readable first Hukbo catalog is:

1. Bangkaw - Long Spear
2. Hardened Javelin
3. Busog - War Bow
4. Broad Dagger
5. Great Blade
6. Blowgun
7. Imported Arquebus
8. Bronze Verso

Start with the first five for individual character placeholders. Add the
blowgun and imported arquebus once ranged behavior exists, and reserve the
bronze verso for a later crew-served or emplacement system.

## Cross-reference: the combat targeting preset is a gameplay model

Hukbo now has an authoritative pre-colonial Philippine combat preset in
`Hukbo.Core.Combat` (see the approved design at
the Philippine combat configuration design), which
gives every warrior a weapon, an armor identity, and a shield identity, and
resolves an explainable body-part hit location for every accepted attack.
That preset was built from this document's evidence, but it is a distinct
artifact with a different purpose, and the two must not be read as making the
same kind of claim. This section records that boundary so a reader of either
document understands what the other one is, and is not, asserting.

- **The combat preset is a gameplay model, not measured historical
  probability.** The general and per-weapon body-part target weights, and
  the shield defense multipliers, in `PhilippineCombatPreset` are hand-authored
  numbers chosen to produce a plausible, explainable, and replayable spread of
  hit locations. They are not derived from any statistical study of actual
  sixteenth-century wound distributions, and no such study is cited or implied
  anywhere in the combat configuration code, tests, or design document. Where
  this document above uses confidence labels such as **Documented** or
  **Provisional reconstruction** to describe how strongly a source supports a
  weapon's existence or general form, the combat preset's numeric weights
  carry no equivalent evidentiary confidence at all; they are gameplay tuning
  choices informed by, but not established by, the sources listed here.
- **Named blades are regional- and period-sensitive, and each carries its own
  evidence tier.** From combat preset V2 the four weapons carry pair-form
  player-facing labels — `Kampilan — Great Blade`, `Wasay — War Axe`,
  `Kalis — Thrusting Blade`, and `Itak — Work Blade` — with the tier shown in
  the agent inspector:

  | Label | Tier | Basis |
  | --- | --- | --- |
  | Kampilan — Great Blade | Documented, form uncertain | Pigafetta records a large cutting sword at Mactan in 1521 and gives it no local name. *Kampilan* is attached to this blade class by later tradition, and surviving cataloged objects are largely eighteenth- and nineteenth-century. |
  | Wasay — War Axe | Documented, form uncertain | A hafted battle axe with a broad metal head, attested among Tausug and Ibanag groups. Pre-contact use is implied by accounts of later iron reinforcement, but no sixteenth-century lexical attestation was located. |
  | Kalis — Thrusting Blade | Documented | The strongest of the four. Pigafetta recorded *calis* in the Visayas in 1521, and the term recurs across vocabularies from 1612 onward in Ilocano, Kapampangan, Ibanag, Tagalog, Bicolano, Waray, Hiligaynon, and Cebuano. A contemporary, pan-archipelagic term rather than a regional back-projection. |
  | Itak — Work Blade | Provisional reconstruction | A Tagalog term for a field and utility blade also used in fighting. Preferred over the former enum identity `Bolo`, a Spanish-era term this document warns against as a blanket name. The specific 1613 vocabulary attestation could not be confirmed, so the tier stays provisional. |

  None of these names may be generalized as if one region's or one later
  century's naming applied evenly across the archipelago and across the 1500s.
  The pair form is what keeps the claim honest: the plain descriptor is what
  the game guarantees, and the Filipino name is what the tradition offers.
- **The panabas is deliberately absent.** It was the obvious candidate for the
  forward-weighted chopping role and was the working assumption until the
  evidence was checked. Its first documented mentions are nineteenth-century
  and its surviving objects are eighteenth- and nineteenth-century, roughly
  three centuries after the depicted period. The hundred-year attestation rule
  in CLAUDE.md section 7 excludes it outright rather than badging it
  provisional. The role went to the wasay, whose weapon class has far better
  footing in the period, at the cost of redrawing the silhouette as an axe and
  renaming a family of sound files.
- **Weapon attribute values are provisional tuning, not measurements.** Preset
  V2 gives each weapon its own damage, reach, and attack cooldown, and gives
  each one-handed weapon a second profile for fighting behind a shield. What
  justifies those numbers is the physical character of the objects — length,
  where the mass sits, how many hands the thing takes — not any source on how
  hard a sixteenth-century blade hit. None of them may be cited back into this
  document.
- **Shield multipliers are provisional tuning values, not measurements.**
  The tall-hardwood shield's chest and abdomen defense multiplier is a
  starting gameplay balance value, not a measurement of any shield's actual
  historical stopping power, coverage, or protective effectiveness. It was
  chosen only to make a carried shield visibly change the resolved spread of
  hit locations in a plausible direction, and it is explicitly labeled
  `PROVISIONAL` in the combat configuration source and in its accompanying
  tests for exactly that reason.
- **Terrain, naval combat, directional defense, and physiology remain
  deferred.** The combat preset introduces a body-part hit location only as
  authoritative explanatory metadata for a single scalar health pool; it does
  not add per-part hit points, wounds, bleeding, disability, or death rules
  tied to a specific body part, and it does not model terrain, rice-field or
  riverine combat, naval boarding actions, facing or directional defense, or
  individual physiology. Those remain deferred design areas, not implied by
  anything in the combat preset or in this document.

In short: this document remains the evidence record for what sixteenth-century
Philippine weapons, armor, and shields plausibly looked like and how they were
described by available sources. The combat preset is a separate, clearly
labeled gameplay system that uses this evidence as inspiration for its
plain-English descriptors and comparative metadata, while keeping its
targeting weights, multipliers, and roster assignments explicitly provisional
gameplay tuning values. Nothing in the combat preset should be cited back into
this document, or presented to a spectator, as a historical measurement.
