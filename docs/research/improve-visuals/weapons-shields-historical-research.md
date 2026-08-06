# Weapons and Shields — Historical and Visual Research for the Visual Improvement Pass

Research date: 2026-07-28.

Purpose: provide the evidence base for improving the procedural visual
representation of Hukbo's four implemented weapons (`WeaponId.Kampilan`,
`WeaponId.Wasay`, `WeaponId.Kalis`, `WeaponId.Itak`) and its implemented shield
(`ShieldId.TallHardwood`, alongside `ShieldId.None`). This is a research
document. It authorizes nothing. It proposes visual variants and shield skins as
candidates for a future design document, and it flags — but does not authorize —
any entry that would require new mechanics.

This document is bound by the historical accuracy policy in `CLAUDE.md`
section 7 and by the evidence-labelling rules established in
[`docs/research/HISTORICAL_1500s_WEAPONS.md`](../HISTORICAL_1500s_WEAPONS.md):

- Every claim carries one of three labels: **Documented**, **Documented, form
  uncertain**, or **Provisional reconstruction**.
- Player-facing cultural identifications appear only in pair form — the
  Filipino name, an em dash, and a plain English descriptor — and only with an
  evidence tier recorded in metadata.
- A name whose earliest attestation postdates the depicted period by more than
  a century is not used at all. That rule excluded the panabas and it binds
  every proposal in this document equally.
- Spanish accounts are evidence about equipment, not neutral ethnography. The
  Boxer Codex guides silhouette and color, not exact technical cataloging.
- Nothing from one region or decade may be generalized to "the Philippines".

One prior document requires a standing caution.
the archived shields research note is an archived design for a shield
stat-variant system. It was consulted here as prior research only. It is
reference material, it authorizes nothing, and its proposed
`ShieldId.NarrowBreastHigh` does not exist in the implemented game. Where this
document agrees with it — for example on the *kalasag* name being a provisional
attachment pending verification — that agreement is restated here on this
document's own evidence, not by citation to the archive as authority.

## 1. Method and rendering constraints

Hukbo renders warriors as small, top-down, procedurally drawn vector pawns. A
weapon is a silhouette extending beyond the body; a shield would be a solid
block beside the torso. At battle scale a spectator can distinguish length,
width, curvature, and color, and almost nothing else. This constrains what a
"visual variant" can honestly be:

1. **Silhouette variants** change the shape a spectator can read at battle
   scale. These are the only variants that carry historical meaning on screen,
   and they are held to the full evidence standard.
2. **Material and condition variants** change color, tone, and small accents —
   a darker hilt, a resin-brown shield face, a worn edge. These are
   presentation-only artistic variation. They must still stay inside the
   documented material palette (iron, palm wood, rattan, hardwood, resin,
   hide, abaca), but they assert nothing historical about a specific object.
3. **Inspector-only detail** — evidence tiers, source notes, regional scope —
   lives in the agent inspector text, not in the pawn drawing.

External images referenced below are **visual references only, never assets to
copy**. The renderer is procedural; nothing is imported. Every external visual
reference in the source register (section 5) records its licensing and
provenance so a later reader knows what may legally be *looked at* versus
*reproduced*, even though Hukbo reproduces none of it.

A recurring evidence problem shapes everything below: the sixteenth-century
sources describe weapon and shield *classes* with almost no object-level
detail, while the object-level detail (blade profiles, pommel carvings, shield
prongs, grips) comes from museum pieces and ethnographies three to four
centuries later. The honest pattern for a visual variant is therefore usually
**one bounded period silhouette plus restrained presentation-only variants**,
with any later-object feature explicitly labelled as a later comparative
projection. Where that is the case, this document says so instead of inventing
three "historical" variants that the evidence cannot carry.

## 2. Weapons

### 2.1 Kampilan — Great Blade

**Current tier (from `HISTORICAL_1500s_WEAPONS.md`): Documented, form
uncertain.** Pigafetta records that Magellan was wounded at Mactan in 1521 by
"a large cutlass, which resembles a scimitar, only being larger", and gives the
weapon no local name. The identification of that blade as a kampilan is a later
traditional attachment. Surviving cataloged kampilan are overwhelmingly
eighteenth- and nineteenth-century objects. The name itself is attested in the
seventeenth century — Francisco Combés describes the weapon among Mindanao
peoples in 1667 — which keeps *kampilan* inside the hundred-year attestation
window, unlike the panabas.

The evidence supports **one uncertain period form plus later-object features
that must be labelled as such**. Three variants are proposed, but only the
first is a period silhouette; the second is an explicitly later comparative
form; the third is presentation-only.

**Variant K1 — the bounded period silhouette (primary battle form).**

- Player-facing name: `Kampilan — Great Blade` (unchanged).
- Plain-English description: a long, single-edged cutting sword, forward-heavy,
  widening toward a truncated tip, with a simple carved dark-wood hilt.
- Date range: anchored to 1521 (Mactan); the weapon class recurs in
  Spanish-era accounts through the century.
- Geographic and cultural scope: the one documented event is Mactan, Cebu
  region, Visayas. The class cannot be assigned to any wider region on
  sixteenth-century evidence.
- Social and military context: Pigafetta associates it with the close-quarters
  killing blow in a melee otherwise dominated by missiles. No source assigns it
  to a rank or class of warrior in the 1500s.
- Evidence tier: **Documented weapon class; Documented, form uncertain** for
  the specific shape.
- Primary source: Pigafetta's Mactan account (1521).
- Later comparative sources: Combés (1667) for the name; Metropolitan Museum of
  Art kampilan (18th–19th century) and Krieger (1926) for proportions.
- Directly supported: existence of a large single-edged cutting sword, larger
  than a scimitar in a European observer's eyes; its use in the killing melee.
- Reconstructed: everything else — the widening profile, the truncated tip,
  the hilt form are all read backwards from later objects and marked
  provisional in that sense.
- Safe to depict: a long forward-heavy blade silhouette clearly longer and
  broader than the other three weapons; dark hardwood hilt tones; iron
  blue-black blade color from the existing palette.
- Must NOT be generalized or depicted as period fact: the tip spikelet, brass
  filled holes, chain-mail hand guards, hair tassels, and the bifurcated
  zoomorphic pommel — all documented only on later objects (see K2).
- Historically meaningful versus presentation-only: the blade's *length and
  forward weight* are the historically meaningful features (they are what
  Pigafetta actually attests, comparatively); hilt tone and edge wear are
  presentation-only.

**Variant K2 — bifurcated-pommel form (later comparative; inspector or
armory-card art only, or clearly badged if ever drawn on a pawn).**

- Proposed player-facing name: `Kampilan — Great Blade` with an inspector note
  "later ornamented form"; it must not become a separate unlabelled pawn
  variant implying a second documented 1500s type.
- Plain-English description: the same blade with the bifurcated ("crocodile
  mouth" / forked) pommel and hair tassels seen on surviving objects.
- Date range of the supporting objects: mostly 18th–19th century; the earliest
  textual anchor for the named weapon is 1667.
- Geographic and cultural scope of the later objects: Moro groups (Maranao,
  Maguindanao, Tausug, Iranun) with hornbill or cockatoo pommel motifs; Visayan
  and Luzon traditions associated with a bakunawa (serpent) motif; Lumad
  examples with plain non-zoomorphic curves. These attributions are themselves
  later ethnographic classifications.
- Evidence tier: **Provisional reconstruction** for any 1500s presence of these
  features.
- Sources: Wikipedia "Kampilan" synthesis (tertiary, used only as a finding
  aid); Metropolitan Museum object 27824 (later comparative); Krieger (1926).
- Directly supported: that later kampilan carry these features.
- Reconstructed: any claim that a 1521 blade did.
- Safe to depict: as armory-card or inspector art labelled as a later
  comparative form.
- Must NOT be generalized: the creature motif is culture-specific in the later
  record. Assigning a hornbill pommel to a generic Hukbo warrior would smear a
  Moro-attributed feature across the archipelago — exactly the generalization
  the policy forbids. If any pommel detail is drawn, it should be the plain
  non-zoomorphic curve, which asserts the least.
- Meaningful versus presentation-only: the pommel is culturally meaningful in
  the later record and therefore *dangerous* as decoration; treat it as
  excluded from pawn-scale art rather than as a free ornament.

**Variant K3 — material and condition variants (presentation-only).**

Two or three tints of the same K1 silhouette: fresh dark iron, duller worn
iron, and a hilt tone range across the existing palm/rattan-ochre to
charred-wood palette entries. No historical claim attaches; these exist to
break up visual repetition among many Kampilan-armed pawns. Evidence tier: not
applicable (artistic variation inside the documented material palette).

### 2.2 Wasay — War Axe

**Current tier: Documented, form uncertain.** The hafted battle axe with a
broad metal head is attested as a class among Tausug and Ibanag groups; the
V2 combat design adopted it for the forward-weighted chopping role after the
panabas was excluded. No sixteenth-century lexical attestation of *wasay* was
located then or in this pass; the word is a widespread everyday term for an axe
(it is also the Kankanaey and other Cordilleran term), which cuts both ways —
it is unlikely to be an anachronistic back-projection precisely because it is a
common tool word, but the *battle* usage in the 1500s remains an inference from
the class, not a document.

The evidence supports **one bounded silhouette plus restrained variants**. A
second, genuinely distinct axe silhouette exists in the record — the
Cordilleran head axe — but it is regionally specific and documented late, so it
is recorded here as a scoped possibility rather than proposed as a general pawn
variant.

**Variant W1 — the bounded silhouette (primary battle form).**

- Player-facing name: `Wasay — War Axe` (unchanged).
- Plain-English description: a one-handed to hand-and-a-half hafted axe with a
  broad iron head on a hardwood haft.
- Date range: the class of hafted metal axes is materially present from the
  Metal Age onward (National Museum archaeological collections include copper
  and bronze adzes and axes); battle use in the 1500s is inferred.
- Geographic and cultural scope: attested as a weapon among Tausug (Sulu) and
  Ibanag (Cagayan Valley) groups in later records — two widely separated
  regions, which supports the *class* being widespread without supporting any
  single standard form.
- Social and military context: axes are tool-weapons; the deep-past research
  (battles/04) explicitly warns that a metal axe does not by itself belong to
  a specialized martial system. Depicting the wasay as an everyman's weapon
  rather than an elite one is the safer reading.
- Evidence tier: **Documented, form uncertain.**
- Primary sources: none for the 1500s battle usage specifically; the
  archaeological attestation of hafted metal axe heads is the anchor.
- Later comparative sources: ethnographic descriptions of Tausug and Ibanag
  axes; Krieger (1926) plates for Philippine axes generally.
- Directly supported: hafted broad-headed metal axes existed and were widely
  distributed; some groups used axes in fighting.
- Reconstructed: the specific head profile, haft length, and any 1500s battle
  role.
- Safe to depict: a clearly axe-shaped silhouette — short haft, mass
  concentrated in a broad head — distinct at battle scale from every blade in
  the roster; iron head, palm/rattan-ochre or charred-wood haft.
- Must NOT be generalized: any specific decorated form; any claim that "the"
  Filipino battle axe had one shape.
- Meaningful versus presentation-only: head breadth and the axe silhouette
  itself are the meaningful features; haft tone and head wear are
  presentation-only.

**Variant W2 — Cordilleran head axe (recorded, NOT proposed for the pawn
roster).**

The northern Luzon highland head axe — narrow curved bit on one side, a spike
on the other — is a real, well-documented, visually striking form (Willcox
1912, Krieger 1926, Cole 1922 for the Tinguian sphere). It is documented in the
nineteenth and twentieth centuries, among specific Cordilleran groups, in a
headhunting context. Projecting it onto a generic 1500s Visayan-flavored
battle roster would generalize one region's later-documented practice across
the archipelago and would attach a loaded cultural practice to pawns that have
no regional identity. Evidence tier for any 1500s pan-regional use:
**Provisional reconstruction at best, and excluded here on anti-generalization
grounds.** It is recorded so a future campaign layer with real regional
factions knows the form exists; it should not appear in the current roster's
visuals.

**Variant W3 — material and condition variants (presentation-only).**

Haft tone range and head tone range within the existing palette; optional
rattan lashing band at the head-haft junction, which stays inside the
documented material set (rattan lashing is a ubiquitous Philippine hafting
technique and asserts nothing specific). No historical claim attaches.

### 2.3 Kalis — Thrusting Blade

**Current tier: Documented — the strongest of the four.** Pigafetta recorded
*calis* as a word in the Visayas in 1521, and the term recurs across
vocabularies from 1612 onward in at least eight Philippine languages. The name
is contemporary and pan-archipelagic; what remains uncertain is the exact
1521 blade form.

The later object record (the Moro kris and its relatives) shows three blade
configurations: fully straight, half-wavy, and fully wavy, with straight and
half-wavy blades described as at least as common as the fully wavy form.
Whether the wave existed on 1521 Visayan blades is not established by any
source consulted here. This is the one weapon where **three silhouette
variants are honestly proposable**, provided the wavy forms carry a provisional
tier and the straight form remains the default.

**Variant L1 — straight double-edged blade (primary battle form, default).**

- Player-facing name: `Kalis — Thrusting Blade` (unchanged).
- Plain-English description: a straight, double-edged, pointed one-handed
  sword built for the thrust.
- Date range: the word is 1521; the straight form is the most conservative
  reading of the class.
- Geographic and cultural scope: the word is Visayan in its 1521 attestation
  and pan-archipelagic in seventeenth-century vocabularies; the straight
  silhouette asserts the least about any one region.
- Social and military context: later tradition treats kalis/kris-class blades
  as both weapons and status objects; for the 1500s, no rank association is
  documented, so the pawn art should not encode one.
- Evidence tier: **Documented** (name and class); form conservative.
- Primary source: Pigafetta (1521) for the word; seventeenth-century
  vocabularies for its spread.
- Later comparative sources: surviving Moro kalis (blade 46–66 cm typical on
  later objects) for proportions only.
- Directly supported: the name, the blade class, its Visayan presence in 1521.
- Reconstructed: exact length, cross-section, hilt.
- Safe to depict: a slim, straight, symmetric, pointed silhouette clearly
  narrower than the Kampilan; one-handed grip; iron blue-black blade.
- Must NOT be generalized: any specific hilt or pommel form (horse-hoof and
  cockatoo pommels are later Sulu/Moro attributions); the asymmetric gangya
  guard, which is characteristic of later objects and too small to read at
  pawn scale anyway.
- Meaningful versus presentation-only: straightness and slimness are the
  meaningful features (they carry the thrusting identity the gameplay preset
  already encodes); hilt tone is presentation-only.

**Variant L2 — half-wavy blade (secondary silhouette, provisional).**

- Proposed player-facing name: `Kalis — Thrusting Blade`, inspector note
  "half-waved form, later attestation".
- Plain-English description: the same sword with gentle waves in the lower
  half of the blade, running straight to the point.
- Date range of support: surviving objects and descriptions are Spanish-era
  and later; the form is common in the later record.
- Evidence tier: **Provisional reconstruction** for the 1500s.
- Sources: later object record (tertiary synthesis; Krieger 1926 plates).
- Safe to depict: only if the wave is legible at pawn scale, which is
  doubtful; more realistic as armory-card and inspector art.
- Must NOT be generalized: the wave must not become the *default* silhouette,
  which would project the iconic later Moro kris backwards onto 1521 Visayas.
- Meaningful versus presentation-only: the wave is historically meaningful in
  the later record (it distinguishes object lineages), which is exactly why it
  should stay provisional rather than being treated as free decoration.

**Variant L3 — fully wavy blade (tertiary, provisional, likely inspector-only).**

Identical reasoning to L2 with a weaker footing: the fully wavy blade is the
most iconic and, per the later record itself, not the most common. Evidence
tier: **Provisional reconstruction.** Recommended use: armory-card art at most.
At pawn scale a fully wavy edge degenerates into a blurry line and loses the
thrusting-blade legibility that the visual grammar requires, so this variant is
recorded mainly to document that the choice was considered and bounded.

### 2.4 Itak — Work Blade

**Current tier: Provisional reconstruction.** *Itak* is a Tagalog term for a
field and utility blade also used in fighting, preferred over the Spanish-era
blanket term *bolo*. The specific early-vocabulary attestation (the 1613
Tagalog vocabulary) could not be confirmed when the V2 preset was designed and
was not confirmed in this pass either, so the tier stays provisional.

The evidence supports **one bounded silhouette plus presentation-only
variants, and nothing more.** This is the weapon with the weakest object-level
record for the period: work blades are precisely the objects that were used up,
resharpened, and never curated. Proposing three "historical" itak variants
would be invention.

**Variant I1 — the bounded silhouette (primary and only battle form).**

- Player-facing name: `Itak — Work Blade` (unchanged).
- Plain-English description: a short, broad, single-edged utility blade with a
  plain wooden grip — a tool carried into a fight.
- Date range: the class (broad daggers and cutlasses in common use) is
  documented in Legazpi-era accounts of 1565–1569, which describe broad
  leaf-shaped blades compared to lance points; the *itak* name is a Tagalog
  attachment whose period attestation is unconfirmed.
- Geographic and cultural scope: the broad-blade class is described in Visayan
  and Luzon encounters; the name is Tagalog. The pair label already carries
  this tension and the inspector tier discloses it.
- Social and military context: an everyman's sidearm; the sources describe
  cutlasses and daggers "in common use", which supports depicting it as the
  plainest weapon in the roster.
- Evidence tier: **Documented, form uncertain** for the broad-blade class;
  **Provisional reconstruction** for the *itak* identification (the composite
  tier stays Provisional, matching the preset).
- Primary sources: Legazpi-era relations (1565–1569) via Blair & Robertson
  volumes II–III.
- Later comparative sources: none needed; modern standardized bolos are
  explicitly excluded as references by `HISTORICAL_1500s_WEAPONS.md`.
- Directly supported: broad single-edged working blades in common use.
- Reconstructed: the specific profile, the name's period currency.
- Safe to depict: the shortest, plainest blade silhouette in the roster; wide
  relative to its length; wooden grip in warm ochre tones.
- Must NOT be generalized: any specific modern bolo or tabak profile; any
  regional decoration.
- Meaningful versus presentation-only: shortness and plainness are the
  meaningful features (the "work blade" identity); everything else is
  presentation-only tone variation.

### 2.5 Weapon summary table

| Weapon | Proposed silhouette variants | Tier of each | Presentation-only variants |
| --- | --- | --- | --- |
| Kampilan — Great Blade | K1 bounded period form | Documented, form uncertain | K3 material/condition tints; K2 later-object pommel form confined to labelled inspector/armory art |
| Wasay — War Axe | W1 bounded form | Documented, form uncertain | W3 haft/head tints, rattan lashing; W2 Cordilleran head axe recorded but excluded from the roster |
| Kalis — Thrusting Blade | L1 straight (default), L2 half-wavy, L3 fully wavy | Documented / Provisional / Provisional | Hilt and blade tone tints |
| Itak — Work Blade | I1 bounded form only | Provisional reconstruction (composite) | Grip and blade tone tints |

## 3. Shields

Twelve entries follow. Each records silhouette, approximate proportions,
construction, grip and carrying posture, surface treatment, regional and
chronological scope, evidence tier, sources, and two flags: whether the entry
is a candidate **visual skin only** or would be a candidate **future
mechanical shield** (no mechanical addition is authorized by this document —
the flag exists so a future design knows which entries it would have to treat
as new `ShieldId` values), and whether it is historically compatible as a skin
for the existing `ShieldId.TallHardwood`.

A general caution that applies to every dimension figure below: the widely
circulated "about 50 × 150 cm" figure for the large kalasag travels through a
secondary and tertiary citation chain that `WEAPON_CLASH_1500s.md` already
flagged as untraceable to a page in Scott or Junker. It is repeated here only
as an approximate proportion (roughly three times taller than wide, covering
most of the body) and never as a measurement.

### S1 — Mactan thin-wood shield (1521)

- Silhouette: a body shield large enough to cover a leaping man; exact outline
  unrecorded.
- Approximate proportions: unknown; large relative to the body.
- Construction: thin, light wood — Pigafetta records Spanish shot passing
  through both the shield and the arm behind it.
- Grip and posture: actively interposed while "leaping about"; the account
  couples footwork and shield cover in a single clause. Grip form unrecorded.
- Surface treatment: unrecorded.
- Regional and chronological scope: Mactan, Cebu region, April 1521. One
  battle, one decade, one island.
- Evidence tier: **Documented** (existence, thinness, active use);
  **Documented, form uncertain** (shape).
- Sources: Pigafetta's Mactan account.
- Skin or mechanics: visual-skin candidate.
- TallHardwood compatibility: **compatible** — a tall light body shield is the
  closest period anchor the existing `TallHardwood` identity has, though the
  enum's name ("hardwood") slightly overstates what Pigafetta describes
  ("thin wood"). A future label pass could note this in the inspector.

### S2 — Full-body light-wood shield, "top to toe" (Morga, 1609)

- Silhouette: a long shield covering the bearer from head to foot.
- Approximate proportions: full body height; narrow enough to carry.
- Construction: light wood, "with their armholes fastened on the inside" —
  i.e. an enarme-style inside fastening rather than a single center grip.
- Grip and posture: worn on the arm via the inside fastening; covering posture.
- Surface treatment: unrecorded in the passage used.
- Regional and chronological scope: Morga writes of the Manila-centered world
  he administered, published 1609 — just past the century's edge, near-primary
  for late-1500s practice.
- Evidence tier: **Documented, form uncertain.** Caveat: the "carasas"
  quotation reached this document through a secondary web article (FMA Pulse)
  quoting Morga; the passage should be verified against Blair & Robertson's
  Morga text before it is quoted in any player-facing string.
- Sources: Morga, *Sucesos de las Islas Filipinas* (1609), via secondary
  transmission; verify against Blair & Robertson.
- Skin or mechanics: visual-skin candidate.
- TallHardwood compatibility: **compatible** — this is arguably the best
  textual anchor for the tall-shield identity.

### S3 — Boxer Codex Cagayan tall curved shield (c. 1590–1595)

- Silhouette: tall, gently curved rectangular shield carried with a long
  spear by the Cagayan warrior figure.
- Approximate proportions: roughly shoulder-to-shin on the painted figure;
  read as proportion, not measurement — the Codex is not technical drawing.
- Construction: not determinable from a painting; wood implied.
- Grip and posture: carried upright beside the body in the illustration.
- Surface treatment: the Codex figure shows a plain face; the manuscript's
  value is silhouette and color, per standing policy.
- Regional and chronological scope: Cagayan Valley, northern Luzon, as
  depicted in a Manila-compiled manuscript c. 1590–1595 with Chinese pictorial
  influence and European patronage.
- Evidence tier: **Documented** as a late-century visual depiction;
  **Documented, form uncertain** as evidence of construction.
- Sources: Boxer Codex, Cagayan warrior plate (Wikimedia Commons scan).
- Skin or mechanics: visual-skin candidate.
- TallHardwood compatibility: **compatible** — this image is already the
  named inspiration for the tall shield in `HISTORICAL_1500s_WEAPONS.md`.

### S4 — Narrow breast-high shield (Legazpi-era relations, 1565–1576)

- Silhouette: a narrower shield reaching about breast height.
- Approximate proportions: roughly half to two-thirds body height.
- Construction: unrecorded beyond the shield/buckler classing in the
  relations.
- Grip and posture: unrecorded; suits spear infantry (the earlier research
  document assigns it that role on visual-grammar grounds, not evidence).
- Surface treatment: unrecorded.
- Regional and chronological scope: Visayan and Luzon encounters, 1565–1576.
- Evidence tier: **Documented, form uncertain.**
- Sources: Legazpi-era relations via Blair & Robertson volumes II–III.
- Skin or mechanics: **future mechanical candidate** — a breast-high shield
  that protected the chest but not the abdomen would need its own targeting
  multiplier profile, i.e. a new `ShieldId`. The archived SHIELDS.md design
  reached the same conclusion; that design is not authorized and the flag here
  is independent of it.
- TallHardwood compatibility: **not compatible as a skin** — drawing a
  visibly shorter shield on a warrior whose simulation carries the tall
  shield's full chest-and-abdomen multiplier would show the spectator a false
  cause.

### S5 — Visayan kalasag, long narrow form (Alcina 1668; Scott 1994)

- Silhouette: long, narrow body shield.
- Approximate proportions: roughly three times taller than wide (see the
  dimension caution above).
- Construction: light fibrous wood; rattan strengthening; resin coating that
  hardens when dry (Junker's synthesis). The often-repeated claim that the
  light wood was chosen so penetrating weapons become enmeshed and cannot be
  withdrawn is flagged as an untraceable formulation — treat as *Documented,
  form uncertain* and never cite as a property.
- Grip and posture: center or inside grip; the *Hinilawod* epic (Panay
  Bukidnon, recorded much later) shows the shield also used offensively to
  strike and drive.
- Surface treatment: carving and rattan binding on the face are described in
  the later record.
- Regional and chronological scope: Visayas; Alcina writes in 1668 about
  Visayan practice, Scott synthesizes sixteenth-century culture from
  documentary sources. The *name* kalasag is the general Tagalog/Visayan word
  for shield; its specific early-vocabulary attestation was not verified in
  this pass, so the name remains a **provisional attachment** exactly as the
  archived shield design concluded independently.
- Evidence tier: **Documented, form uncertain** for the form; name attachment
  provisional pending vocabulary verification.
- Sources: Alcina (1668); Scott, *Barangay* (1994); Junker (1999); FMA Pulse
  synthesis (secondary, finding aid only).
- Skin or mechanics: visual-skin candidate.
- TallHardwood compatibility: **compatible** — and this entry is the best
  candidate for the pair-form label upgrade (`Kalasag — Tall Hardwood
  Shield`) if and only if the vocabulary attestation is confirmed within the
  hundred-year rule. Until then the label stays `Tall Hardwood Shield`.

### S6 — Tagalog palisay, round buckler (Scott 1994)

- Silhouette: small round buckler.
- Approximate proportions: forearm-scale; a parrying disc, not a body shield.
- Construction: wood; details unrecorded in the material consulted.
- Grip and posture: single center grip implied by buckler classing; active
  parrying use; Scott records it also being wielded in war dance.
- Surface treatment: unrecorded.
- Regional and chronological scope: Tagalog region (Manila hinterland), as
  synthesized by Scott from early colonial documentation. The word's earliest
  attestation is likely an early-1600s vocabulary; this was not verified at
  page level in this pass.
- Evidence tier: **Documented, form uncertain**; the *palisay* name is a
  provisional attachment pending page-level verification of an early
  vocabulary entry.
- Sources: Scott, *Barangay* (1994); WEAPON_CLASH_1500s.md already carries
  the palisay as Scott's record.
- Skin or mechanics: **future mechanical candidate** — a buckler is a
  different defensive object with different coverage; the existing tall-shield
  multiplier profile cannot honestly represent it.
- TallHardwood compatibility: **not compatible as a skin.**

### S7 — Taming, round woven buckler (later record; Sulu and Basilan)

- Silhouette: round, slightly convex buckler.
- Approximate proportions: forearm- to elbow-scale disc.
- Construction: wood, or tightly woven rattan.
- Grip and posture: hand grip; parrying use.
- Regional and chronological scope: Sulu, Basilan, and Moro groups broadly in
  the later record; also attested among Lumad and Visayan groups in later
  images (earliest historical image consulted dates c. 1668). *Taming* is a
  term of Malay origin whose Philippine entry date could not be established —
  the archived shield design flagged the same problem independently.
- Evidence tier: **Provisional reconstruction** for any 1500s Philippine
  presence under this name; the round-buckler *class* overlaps S6's tier.
- Sources: Wikipedia "Taming (shield)" (tertiary, finding aid); FMA Pulse
  (secondary); museum specimens 18th–19th century.
- Skin or mechanics: **future mechanical candidate** (same reasoning as S6).
- TallHardwood compatibility: **not compatible as a skin.**

### S8 — Kalinga/Tinguian pronged shield (Willcox 1912; Cole 1922; Krieger 1926)

- Silhouette: long shield with three prongs at the top and two at the bottom.
- Approximate proportions: body shield; prongs extend the outline top and
  bottom.
- Construction: carved in one piece from very light wood; cylindrical
  projections form ribs along the edges; braided bejuco (rattan) bands
  reinforce it; easily pierced by a spear.
- Grip and posture: Cole's grip description is the most mechanically specific
  in the entire Philippine record — a hand grip cut into the center of the
  back, large enough for the first three fingers, with thumb and little finger
  outside to tilt the shield to the proper angle. The shield deflects
  missiles rather than stopping them; fighters crouch and give ground behind
  it.
- Surface treatment: plain to darkened; later examples show soot and incised
  bands.
- Regional and chronological scope: Kalinga, Tinguian (Itneg), and neighboring
  northern Luzon highland groups, documented 1912–1926. The prongs are
  described in that record as functional for trapping a fallen enemy's limbs
  or neck in a headhunting context.
- Evidence tier: **Provisional reconstruction** for the 1500s (the record is
  three to four centuries late), on top of **Documented** twentieth-century
  ethnography.
- Sources: Willcox (1912); Cole (1922); Krieger (1926, US National Museum
  Bulletin 137).
- Skin or mechanics: the *pronged outline* is a visual-skin question, but the
  prongs' documented function is offensive trapping — drawing them while the
  simulation gives them no function is tolerable (plenty of documented
  features go unsimulated), yet the regional problem below dominates.
- TallHardwood compatibility: **compatible in coverage, not recommended as a
  general skin** — this is a specific highland-Luzon form tied to a specific
  practice, and scattering it across a generic roster is precisely the
  pan-archipelagic-warrior error. Appropriate only if pawns ever acquire
  regional identity. Cole's *grip and posture* description, by contrast, is
  safe to borrow as animation/stance inspiration for any shield, since it is
  the best surviving description of how a light Philippine shield was
  actually held.

### S9 — Bontoc shield, blunted-prong variant (Krieger 1926)

- Silhouette: the same five-pronged plan as S8 with shortened, flattened
  points — two on one side, three on the other — no longer of practical
  trapping size.
- Approximate proportions: body shield, slightly more compact than S8.
- Construction: hollowed light wood, per Krieger's classing of "oblong,
  pronged, clubbed, and tufted shields of hollowed wood".
- Grip and posture: as S8.
- Surface treatment: plain wood, darkened faces in surviving examples.
- Regional and chronological scope: Bontoc, Mountain Province, documented
  1926.
- Evidence tier: **Provisional reconstruction** for the 1500s; **Documented**
  as 1920s ethnography.
- Sources: Krieger (1926); Smithsonian repository scan.
- Skin or mechanics: visual-skin question only.
- TallHardwood compatibility: same verdict as S8 — coverage-compatible,
  regionally inappropriate as a generic skin.

### S10 — Ifugao and neighboring Cordilleran shield forms (Krieger 1926)

- Silhouette: variants of the pronged oblong family across Ifugao and
  neighboring groups, with prong length and opening varying by group; Krieger
  notes forms where prongs survive only as vestigial points.
- Approximate proportions: body shields of the same family as S8–S9.
- Construction: hollowed light wood, rattan binding.
- Grip and posture: as S8.
- Surface treatment: plain to soot-darkened.
- Regional and chronological scope: Cordilleran highlands, documented
  1912–1926.
- Evidence tier: **Provisional reconstruction** for the 1500s.
- Sources: Krieger (1926); vikingsword.com plate scans of the same public
  domain bulletin (finding aid).
- Skin or mechanics: visual-skin question only.
- TallHardwood compatibility: as S8–S9 — not for a generic roster.

### S11 — Bagobo shield, round or oblong, tufted (Cole 1913/1922; Krieger 1926)

- Silhouette: either a round buckler or an oblong body shield; the oblong
  form in the later record carries tufts of hair set along the face or edges
  ("tufted shields of hollowed wood" in Krieger's classing).
- Approximate proportions: oblong form is a body shield; round form is
  buckler-scale.
- Construction: hollowed light wood; hair tufts; incised and darkened
  decoration in surviving examples.
- Grip and posture: Cole describes Bagobo fighters crouching and dancing
  backwards behind the shield, glancing spears and arrows off it — active
  angled deflection, consistent with the S8 grip mechanics.
- Surface treatment: blackened faces, incised geometric bands, hair tufts.
- Regional and chronological scope: Bagobo, Davao Gulf region, Mindanao,
  documented 1913–1922.
- Evidence tier: **Provisional reconstruction** for the 1500s; **Documented**
  early-twentieth-century ethnography.
- Sources: Cole, *The Wild Tribes of Davao District* (1913); Cole (1922);
  Krieger (1926).
- Skin or mechanics: oblong form is a visual-skin candidate; round form falls
  with S6/S7 as a mechanical question.
- TallHardwood compatibility: the oblong form is **coverage-compatible**, with
  the same regional caution as S8–S10: it belongs to a named Mindanao group in
  a late record. The hair tufts are culturally specific decoration and should
  not appear on generic pawns.

### S12 — Hinilawod epic shield, offensive use (Panay Bukidnon oral epic)

This entry is behavioral rather than a distinct silhouette. The *Hinilawod*
epic — recorded from Panay Bukidnon chanters in the twentieth century, with
claimed but undatable deep roots — has a hero striking and driving enemies
with the shield itself before threatening with the spear. Together with Cole's
tilting grip and Warming's experimental finding (Viking material, not
Philippine) that active angled shield use radically outperforms passive
blocking, it supports depicting Hukbo's shield-bearers with an *active* shield
posture — shield forward and angled, not a static wall.

- Evidence tier: **Provisional reconstruction** for any 1500s practice;
  **Documented** as recorded oral literature.
- Sources: *Hinilawod* as summarized in WEAPON_CLASH_1500s.md's source set;
  Cole (1922); Warming (comparative, non-Philippine).
- Skin or mechanics: neither — a posture/animation reference. Flagged here
  because it is the strongest justification available for drawing the tall
  shield slightly angled in front of the pawn rather than as a passive slab at
  its side.
- TallHardwood compatibility: applies to how the existing shield is *drawn*,
  not what it is.

### 3.1 Compatibility map for the implemented ShieldId values

| Entry | Historically compatible skin for `TallHardwood`? | Would require new mechanics (flag only, not authorized) |
| --- | --- | --- |
| S1 Mactan thin-wood shield | Yes | No |
| S2 Morga full-body shield | Yes | No |
| S3 Boxer Codex Cagayan shield | Yes (current named inspiration) | No |
| S4 Narrow breast-high shield | No | Yes — distinct coverage profile |
| S5 Visayan kalasag | Yes (best label-upgrade candidate, pending name verification) | No |
| S6 Palisay round buckler | No | Yes — buckler coverage |
| S7 Taming round buckler | No | Yes — buckler coverage |
| S8 Kalinga/Tinguian pronged | Coverage yes; regionally inappropriate for a generic roster | Prong function would be mechanics; not proposed |
| S9 Bontoc blunted-prong | Coverage yes; regionally inappropriate | No |
| S10 Cordilleran variants | Coverage yes; regionally inappropriate | No |
| S11 Bagobo oblong | Coverage yes; regionally inappropriate; no hair tufts on generic pawns | Round form as S6/S7 |
| S12 Hinilawod offensive posture | Posture reference for the existing shield | Shield-strike would be mechanics; not proposed |
| `ShieldId.None` | Not applicable — absence of equipment | Not applicable |

The practical reading: the existing `TallHardwood` shield has four honest
skins (S1, S2, S3, S5) that differ only in presentation-level detail — face
tone, rattan-binding accents, slight outline curvature — and one posture
recommendation (S12). Everything round or breast-high is a future-mechanics
question, and everything Cordilleran or Bagobo is a future-regional-identity
question. No mechanical addition is authorized by this document.

## 4. Anti-generalization notes

These bind any design or implementation work that consumes this document.

1. **There is no pan-archipelagic warrior.** The usable evidence clusters
   around Mactan/Cebu 1521, Visayan and Luzon encounters 1565–1582, and
   Manila-compiled late-century visual material. A Hukbo pawn is a *composite
   within that cluster*, and the inspector's evidence text is the place where
   that compositeness is disclosed. No single named culture's diagnostic
   features (Moro pommel creatures, Cordilleran prongs, Bagobo hair tufts) may
   be drawn on generic pawns.
2. **No bare cultural labels.** Every player-facing name stays in pair form
   with its tier: `Kampilan — Great Blade`, `Wasay — War Axe`, `Kalis —
   Thrusting Blade`, `Itak — Work Blade`, and — only if the name verification
   succeeds — `Kalasag — Tall Hardwood Shield`. Until then the shield label
   remains the plain descriptor.
3. **The hundred-year rule is live.** Names verified inside the window:
   *kalis* (1521), *kampilan* (1667 for a 1590s-adjacent depiction window —
   within a century of the late-1500s material, though not of Mactan itself,
   which is why the kampilan tier stays "form uncertain" rather than
   "documented"). Names still awaiting verification: *kalasag*, *palisay*.
   Names that failed or cannot be established: *panabas* (excluded),
   *taming* (entry date unknown — treat as excluded until shown otherwise).
4. **Decade and region scoping in flavor text.** Any inspiration tag shown to
   the player should name a place and time (`Mactan — 1521`,
   `Manila — c.1590`), never "ancient Philippines".
5. **Later ethnography stays later.** Cole, Willcox, and Krieger describe
   1910s–1920s highland and Mindanao practice. They are the best construction
   and grip evidence in existence, and they are three to four centuries and
   several cultural worlds away from a 1521 Visayan beach. They may inform
   *how* a shield is held and built; they may not put a pronged shield in a
   generic pawn's hand.
6. **Tuning values never become history.** The tall-hardwood targeting
   multipliers, and any future visual-variant weighting, are gameplay values
   marked `PROVISIONAL` in code. Nothing in this document upgrades them, and
   nothing here may be cited as if it did.
7. **No metallurgical claims.** The "rivals Toledo" genre of claim about
   pre-colonial Philippine steel has no analytical basis and stays out of the
   repository, per WEAPON_CLASH_1500s.md's exclusion list.

## 5. Source register

Every source consulted for this document, with what it evidences and the
licensing status of any associated visual material. "Reference only" means the
material may inform procedural art direction but must never be copied,
traced, or imported as an asset.

### Sixteenth-century and near-primary

1. **Pigafetta, Mactan account (1521).** Via Wikisource translation and the
   Library of Congress manuscript record. Evidences: thin wooden shields
   actively used with evasive footwork; the large cutting sword ("larger than
   a scimitar") that wounded Magellan; the word *calis*. Text: public domain.
   No images. Reference only.
2. **Legazpi-era relations (1565–1576),** Blair & Robertson, *The Philippine
   Islands*, volumes II–III (Project Gutenberg). Evidences: cutlasses and
   daggers in common use; shields and bucklers including breast-high forms;
   broad leaf-shaped blades. Text: public domain. Reference only.
3. **Loarca, Relación (1582),** Blair & Robertson volume V. Evidences: Visayan
   equipment context. Public domain. Reference only.
4. **Boxer Codex (c. 1590–1595),** Wikimedia Commons scans (Cagayan warrior,
   Zambal figures, Visayan and Tagalog figures). Evidences: tall curved shield
   silhouette, spear-and-shield pairing, clothing color. Manuscript is public
   domain; Commons hosts the scans under public-domain tags (PD-Art /
   PD-old). Licensing must be confirmed per file page before any reproduction;
   for Hukbo they are reference only. The eHumanista critical study of the
   Codex's pictorial sources (Romero, open-access PDF) governs how literally
   the images may be read: silhouette and color only.
5. **Morga, *Sucesos de las Islas Filipinas* (1609).** Evidences: light-wood
   full-body shields with inside armhole fastening ("carasas"). Reached in
   this pass only through a secondary quotation (FMA Pulse); flagged for
   verification against the Blair & Robertson translation (public domain)
   before player-facing use. Reference only.

### Seventeenth-century and later near-primary

6. **Combés, *Historia de Mindanao y Joló* (1667).** Evidences: the earliest
   located textual anchor for the name *kampilan* in a Philippine context.
   Consulted at second hand (tertiary synthesis); page-level verification
   outstanding. Public domain text. Reference only.
7. **Alcina, *Historia de las Islas e Indios de Bisayas* (1668).** Evidences:
   Visayan long narrow shields, round bucklers, equipment picture; historical
   illustrations. Original: public domain; modern critical editions and
   translations are copyrighted. Reference only.

### Later comparative — ethnography and collections

8. **Willcox, *The Head Hunters of Northern Luzon* (1912).** Evidences:
   Kalinga one-piece pronged shield construction, rib projections, braided
   bejuco bands. Published pre-1930: public domain in the US. Reference only.
9. **Cole, *The Wild Tribes of Davao District, Mindanao* (1913, Field
   Museum).** Evidences: Bagobo round and oblong shields, decoration.
   Public domain. Reference only.
10. **Cole, *The Tinguian* (1922, Field Museum).** Evidences: the
    three-finger tilting grip, deflection-not-stopping doctrine, light easily
    pierced wood, crouching posture. Public domain. Reference only.
11. **Krieger, *The Collection of Primitive Weapons and Armor of the
    Philippine Islands in the United States National Museum* (1926, US
    National Museum Bulletin 137).** Evidences: shield typology (circular
    parrying shields and targets; oblong, pronged, clubbed, tufted hollowed
    wood shields), Bontoc blunted-prong variant, axe and blade plates. US
    government publication: public domain; scans at repository.si.edu and
    plate reproductions at vikingsword.com (the scans themselves are of
    public-domain material; the hosting pages are reference only).
12. **Metropolitan Museum of Art, kampilan (object 27824, 18th–19th
    century).** Evidences: later kampilan silhouette, materials, bifurcated
    pommel. The Met's Open Access program releases images of public-domain
    objects under CC0; confirm the CC0 tag on the object page before any
    reproduction. For Hukbo: reference only.
13. **National Museum of the Philippines, Weapons and Shields collection
    pages.** Evidences: cultural context for kampilan, kris, bolo, shields,
    spears, axes; explicit statement that most cataloged objects are much
    later than the 1500s. Website content copyrighted. Reference only.
14. **University of Michigan Museum of Anthropological Archaeology (UMMAA)
    Philippines blog, Kalinga shield entry (2022).** Evidences: a specific
    collected Kalinga shield. Museum photograph, copyright presumed reserved.
    Reference only.
15. **Mapping Philippine Material Culture (philippinestudies.uk), Bontoc
    shield and axe items.** Evidences: object records across UK collections.
    Image rights vary per holding institution; treat all as copyrighted.
    Reference only.

### Secondary syntheses and finding aids

16. **Scott, *Barangay: Sixteenth-Century Philippine Culture and Society*
    (1994).** Evidences: the long narrow kalasag, the Tagalog *palisay* round
    buckler, cotton and hide armor context. Copyrighted; text reference only.
    Standing gap (inherited from WEAPON_CLASH_1500s.md and still open): the
    warfare chapter has never been read at page level for this repository.
17. **Junker, *Raiding, Trading, and Feasting* (1999).** Evidences:
    rattan-strengthened resin-coated shield construction; the political
    economy of raiding. Copyrighted; reference only.
18. **FMA Pulse, "Kalasag: The Filipino War Shield" (Mallari).** Secondary
    web synthesis quoting Morga 1609, Willcox 1912, Cole 1922, Scott 1994.
    Used as a finding aid; every load-bearing quotation it supplied is flagged
    above for primary verification. Copyrighted webpage; reference only.
19. **Wikipedia: "Kampilan", "Kalis", "Kalasag", "Taming (shield)", "Head
    axe".** Tertiary finding aids only; multiple citation-needed passages
    noted on the Kalis article in particular. Text CC BY-SA; images variously
    licensed per file. No claim in this document rests on Wikipedia alone —
    each was used to locate the underlying primary or museum source, and
    claims that could not be pushed down to a better source are labelled
    Provisional above. Reference only.
20. **Deep-past research set,** `docs/research/battles/01`–`04` (this
    repository). Evidences: the archaeological ceiling — perishable equipment
    does not survive, so no deep-past find establishes a standard shield; a
    shield may appear in a reconstructed loadout only on period-specific
    justification; weapon affordances, not techniques. These bounds are
    inherited wholesale by this document.
21. **`docs/research/WEAPON_CLASH_1500s.md`** (this repository). Evidences:
    shield primacy in the defensive record; the untraceable "50 × 150 cm,
    sword-proof, enmeshing" formulation flag; active-angled shield use
    (Warming, Viking comparative); the exclusion of enthusiast metallurgy.
22. **the archived shields research note** (this repository, archived).
    Prior shield design research. Reference only; authorizes nothing; its
    conclusions on the *kalasag* name and the narrow shield's coverage were
    independently re-derived here.

## 6. Biggest evidence gaps

Recorded so the next research pass knows where to dig.

1. **Scott's *Barangay* warfare chapter, page level.** Still the single
   highest-value unread source. It would settle the *kalasag* and *palisay*
   attestations, the kalasag dimension chain, and probably the Visayan kalis
   blade form.
2. **Morga's shield passage in Blair & Robertson.** The "top to toe" quote
   must be verified in the public-domain translation before it is used
   anywhere player-facing.
3. **The early Tagalog and Visayan vocabularies (1604–1637).** Direct
   consultation would convert several "provisional attachment" names to
   Documented or kill them cleanly.
4. **No sixteenth-century image of a Visayan shield's construction exists.**
   The Boxer Codex gives silhouette only; every construction detail in this
   document is 1609 or later. That gap cannot be closed, only disclosed.
5. **No metallurgical study of a dated sixteenth-century Philippine blade.**
   Inherited gap; still open; still blocks any edge, hardness, or durability
   claim.
