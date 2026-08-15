# Historical Philippine Armor, Head Protection, and Shields of the 1500s

Research date: 2026-08-15

Purpose: provide an evidence-backed reference for how people in the Philippine
archipelago protected their bodies in the sixteenth century, so that Hukbo's
armor layer, head layer, and shield layer rest on something a reader can check
rather than on popular assumption. This document is an evidence record only. It
does not define gameplay statistics, damage values, enum members, preset
contents, or visual specifications, and nothing in it authorizes an
implementation.

Its companion document, `docs/research/HISTORICAL_1500s_WEAPONS.md`, covers
offensive equipment. This document covers the other half: body armor, head
protection, and shields. The two are meant to be read together and use the same
confidence vocabulary.

## 1. Research boundary

"The Philippines in the 1500s" was not one military culture, and the evidence
for protective equipment is even more regionally uneven than the evidence for
weapons. Armor is precisely the subject where popular belief runs furthest ahead
of what any sixteenth-century source actually says. The sources drawn on here
cover:

- eastern Samar, Limasawa, Cebu, and Mactan in 1521, from Antonio Pigafetta's
  eyewitness account of the Magellan voyage;
- the island Pigafetta calls Caghaian, in the waters between Palawan and Borneo,
  also in 1521;
- Sarangani and the southern islands during the Villalobos expedition of
  1543-1546, from García Descalante Alvarado's relation written at Lisbon in
  1548;
- Cebu in 1565 and Luzon, the Visayas, and Los Camarines between 1573 and 1577,
  from Legazpi-era Spanish relations printed in Blair and Robertson's *The
  Philippine Islands, 1493-1803*;
- late-century clothing and equipment in the Boxer Codex, compiled in Manila
  around 1590; and
- the early seventeenth century, used only as a boundary marker rather than as
  evidence for the 1500s: Antonio de Morga's *Sucesos de las Islas Filipinas*
  of 1609, and Pedro de San Buenaventura's Tagalog *Vocabulario* of 1613.

What this document is not: it is not a survey of Moro armor traditions, which
survive almost entirely in objects and accounts from the eighteenth century
onward; it is not a catalogue of museum holdings; and it is not a reconstruction
of how any of this equipment was used in a fight. Technique, shield handling,
and blade-on-shield interaction belong to `docs/research/WEAPON_CLASH_1500s.md`
and are not re-litigated here.

A standing caution applies to every Spanish source used below. These are
documents written by men with interests. Diego de Artieda and Francisco de Sande
were writing to a Crown they wanted soldiers, ships, and money from, and an
enemy who is "well armed" is an argument for reinforcements. Pigafetta was
writing an account in which his commander had just been killed by people the
expedition had dismissed, and a narrative of that kind has its own pressure to
explain the defeat. Guido de Lavezaris was defending his own administration.
These accounts are good evidence that a piece of equipment existed and was seen.
They are weak evidence for how common it was, and they are not neutral
ethnography.

A second caution applies to the Boxer Codex. It is genuinely valuable visual
evidence for the late sixteenth century, but its pictorial conventions are
partly Chinese and its patronage was European. It should guide silhouette,
proportion, and color, not be read as an exact technical catalogue of
construction.

### Verification status of the evidence in this document

Claims in this document fall into two groups, and the difference matters.

- **Verified directly in the primary text.** Claims attributed to Pigafetta,
  to Alvarado's 1548 relation of the Villalobos expedition, to the 1565 Cebu
  skirmish account, to Artieda, to Lavezaris's letter of 17 July 1574, to
  Cauchela and Aldave, to Morga, and to Blair and Robertson volume XXVIII were
  read in the printed volumes during the preparation of this document, including
  the original Italian of Pigafetta's Visayan vocabulary and the editorial
  footnotes. Those claims are marked *(verified in the primary text)*.
- **Read only in an optical-character-recognition text.** Claims attributed to
  William Henry Scott's *Barangay* (1994) were read in a scanned transcription
  of the book, not in the book. That is more than a research summary and less
  than a reading: the transcription visibly garbles words near the passages
  used, rendering *morriones* as "monones", so it can support the substance of
  what Scott says and cannot be trusted for spellings or page numbers. Those
  claims say so where they appear.
- **Unverified, reported at second hand.** Claims attributed to Lavezaris's
  relation of 29 June 1573, to Sande, to Loarca, to San Buenaventura, to the
  Boxer Codex, or to any museum catalogue still reach this document through
  research summaries rather than through the page. Those claims are marked
  *(unverified)*. They are recorded because they are probably right and are
  worth checking, not because they have been checked. Nothing marked
  *(unverified)* should be quoted onward as established until somebody reads the
  page.

This distinction is kept visible throughout rather than collected in a footnote,
because the temptation to launder a second-hand claim into a fact is exactly
what this document exists to resist. Several of the corrections in the current
version of this document are corrections to claims an earlier version had
laundered in exactly that way — an inverted quotation from Artieda, a wrong date
and page for Cauchela and Aldave, an inference presented as a source, and three
attributions given to the wrong author.

## 2. Confidence labels

These are the same three labels used in `HISTORICAL_1500s_WEAPONS.md`, with the
same meanings, so that a reader moving between the two documents does not have
to re-learn the vocabulary.

- **Documented:** directly described or pictured in a sixteenth-century source.
- **Documented, form uncertain:** the equipment class is attested, but its exact
  local form or name is uncertain.
- **Provisional reconstruction:** a plausible identification supported by later
  traditions or objects, but not firmly established for the cited event.

Every claim in the tables and prose below carries one of these three labels.
Where a claim cannot carry one, it is stated as an open question in section 12
instead of being given a label it has not earned.

## 3. Relationship to the existing documents

Four documents in this repository now touch protective equipment. They do not
all say the same thing, and where they disagree it must be clear which one wins.

| Document | What it is | Authority over armor and shields |
| --- | --- | --- |
| `docs/research/HISTORICAL_1500s_WEAPONS.md` | The evidence record for offensive equipment, plus a short "Defensive equipment" list written as visual direction for placeholder silhouettes. | Wins on weapons. Its defensive-equipment list is a presentation note, not an evidence finding; where it and this document differ on armor or shields, **this document wins**. |
| `docs/research/improve-visuals/warrior-appearance-historical-research.md`, Category F, lines 462-566 | The armor layer that currently drives the pawn renderer: F1 No armor, F2 Corded or quilted fiber armor, F3 Carabao-hide corselet, F4 Hardwood or bark breastplate, F5 Helmet set with fish bones and shells, plus an exclusion list. | Wins on what the renderer does and on how a preset is drawn. It does **not** win on the underlying evidence. Section 10 below lists the specific places where its evidence statements are wrong or too narrow, by line number. |
| `docs/research/WEAPON_CLASH_1500s.md`, section 2.1 at lines 132-158 and the open gap at line 720 | The evidence record for how blades and shields interacted, and for the unresolved "sword-proof, enmeshing kalasag" claim. | Wins on technique, on blade-on-shield behavior, and on the enmeshing question. This document cross-references it and deliberately does not re-argue it. Section 12 question 5 below supplies the one thing that document asked for — the page the enmeshing formulation comes from, which is Scott (1994) page 151 — and leaves the substance where it belongs. |
| This document | The evidence record for body armor, head protection, and shields. | Wins on what protective equipment is attested, where, when, and how strongly. |

`CLAUDE.md` section 7, the repository's historical accuracy policy, binds all
four and overrides all four. In particular: no cultural identification appears
in player-facing text except in pair form, no name is used whose earliest
attestation postdates the depicted period by more than a century, and no
gameplay tuning value is ever presented as a historical measurement.

## 4. Body armor

### 4.1 Summary table

The Confidence column carries one of the three labels from section 2 and nothing
else. Where a row also has a status — contested between two readings, or
excluded from the depicted period — that status is recorded in its own column so
that it can never be mistaken for a fourth confidence tier.

| Item | Region and year | Source basis | Confidence | Status | Verification |
| --- | --- | --- | --- | --- | --- |
| Quilted or padded fighting garment, Visayan *baluti* | Visayas, 1521 | Pigafetta's Visayan vocabulary glosses *baluti* as garments quilted or padded for fighting. | Documented | Inside the window | Verified in the primary text |
| Small cuirass of buffalo horn | The island Pigafetta calls Caghaian, between Palawan and Borneo, 1521 | Pigafetta describes the inhabitants, whom he identifies as Moros banished from Brunei, as carrying bucklers and small cuirasses of buffalo horn. | Documented | Inside the window | Verified in the primary text |
| Cotton corselet reaching to the feet, with sleeves | Sarangani and the islands around it, from the Villalobos expedition of 1543-1546 | Alvarado, Lisbon, 7 August 1548: "cotton corselets reaching to the feet and with sleeves". | Documented, form uncertain | Inside the window | Verified in the primary text |
| Corselet of wood and buffalo horn | Sarangani and the islands around it, from the Villalobos expedition of 1543-1546 | Alvarado, 1548: "corselets made of wood and buffalo horn". | Documented, form uncertain | Inside the window | Verified in the primary text |
| Cuirass of bamboo and hard wood, covering the whole body | Sarangani and the islands around it, from the Villalobos expedition of 1543-1546 | Alvarado, 1548: "cuirasses made of bamboo and hard wood, which entirely cover them". | Documented, form uncertain | Inside the window | Verified in the primary text |
| Cotton-lined blanket armor | Unspecified, 1573 | Artieda: "Those people have armor consisting of cotton-lined blankets, and others of rattan." | Documented, form uncertain | Inside the window | Verified in the primary text |
| Rattan armor | Unspecified, 1573 | Artieda, in the same sentence, names rattan as the second armor material. | Documented, form uncertain | Inside the window | Verified in the primary text |
| Corselet of very hard black wood | Unspecified, 1573 | Artieda: "Some wear corselets, made of a very hard black wood resembling ebony." The word "some" is his, and it is the whole point. | Documented, form uncertain | Inside the window | Verified in the primary text |
| Corselet of buffalo hide and knotted rope | Luzon, 1573 | Lavezaris, *Affairs in the Philippines*, Manila, 29 June 1573. | Documented, form uncertain | Inside the window | Unverified |
| Wooden corselet and rope armor | Cebu, 1565 | Legazpi-era account of a skirmish in which the natives put on wooden corselets and rope armor. | Documented, form uncertain | Inside the window | Verified in the primary text |
| Corselet of buffalo hide, with iron greaves | Camarines, 1574 | Cauchela and Aldave to Felipe II, Manila, 17 July 1574, Blair and Robertson volume XXXIV page 295. | Documented, form uncertain | Inside the window; same campaign as the row below, see 4.6 | Verified in the primary text |
| Iron corselets, greaves, wristlets, gauntlets, helmets | Los Camarines, Bicol, 1574 | Lavezaris to Felipe II, Manila, 17 July 1574, Blair and Robertson volume III page 273. Scott, *Barangay* (1994), argues this is a confusion with Japanese equipment. | Documented, form uncertain | Contested; see 4.6 | Lavezaris verified in the primary text; Scott's reading verified in an optical-character-recognition text |
| Brass-and-horn plate armor of the *kurab-a-kulang* type | Moro regions, eighteenth to nineteenth centuries | Museum objects and later collection records only. | Provisional reconstruction | Excluded from the 1500s; see 4.7 | Unverified |
| Mail | Not Philippine | Mail appears in sixteenth-century accounts as foreign equipment that Philippine hardwood lances could pierce. | Provisional reconstruction | Excluded from the 1500s; see 4.7 | Unverified |

### 4.2 The 1521 evidence, which is stronger than it is usually given credit for

The standard summary of Philippine armor evidence says that the 1521 sources
show unarmored fighters and that armor only appears with the Legazpi-era
relations of 1565 onward. That summary is wrong, and the error is worth
correcting carefully because it has already propagated into this repository.

Pigafetta's Visayan vocabulary, collected in the Cebu area in 1521, contains
this entry (verified in the primary text):

- Italian: *A le veste inbotide per combater — baluti*
- Blair and Robertson's English: "for Quilted garments used for fighting —
  *baluti*"

That is a sixteenth-century eyewitness recording a Visayan word whose meaning he
gives as padded or quilted clothing worn specifically to fight in. It is not an
inference from a later dictionary, it is not a reconstruction, and it does not
depend on any modern scholar's reading. **Quilted body armor in the Visayas in
1521 is Documented.**

The Blair and Robertson editors' concordance in footnote 376 matches Pigafetta's
*baluti* to *baloti* in Juan Félix de la Encarnación's *Diccionario
bisaya-español* of 1885 (verified in the primary text). Antonio Sanchez de la
Rosa's *Diccionario Hispano-bisaya* of 1895 has no corresponding entry in that
concordance. The 1885 match is confirmation of continuity, not the source of the
attestation; the attestation is Pigafetta's own gloss.

What the 1521 evidence does **not** show is anyone wearing armor at Mactan.
Pigafetta's battle narrative describes the Mactan force in some detail — three
divisions, more than fifteen hundred people, bamboo spears some of them
iron-tipped, fire-hardened stakes, arrows, stones, and mud, and shields they
covered themselves with while leaping about (verified in the primary text) — and
mentions no body armor of any kind on them. He does mention body armor on his
own side, sixty men who set out armed with corselets and helmets. The absence is
meaningful: he had every reason to mention protective equipment on the people
who had just killed his commander, and he did not.

So the honest 1521 position is a split one. The word for quilted fighting
garments existed in Visayan and was recorded by an eyewitness in the same year.
Nobody in the one 1521 battle described in detail is said to have been wearing
any. Both facts are Documented, and neither cancels the other.

### 4.3 The buffalo-horn cuirass at Caghaian, 1521

At the island Pigafetta calls Caghaian, which he places about forty-three
leagues from Quipit in Mindanao at seven and a half degrees north — that is, in
the waters between Palawan and Borneo, in the Sulu approaches — he writes
(verified in the primary text):

> "The people of that island are Moros and were banished from an island called
> Burne. They go naked as do the others. They have blowpipes and small quivers
> at their side, full of arrows and a poisonous herb. They have daggers whose
> hafts are adorned with gold and precious gems, spears, bucklers, and small
> cuirasses of buffalo horn."

Three things follow, and they need to be kept separate.

- **The cuirass is Documented for 1521, not provisional.** This is a
  sixteenth-century eyewitness naming a rigid body armor of a specific material
  in a specific place in a specific year. It is one of the two strongest pieces
  of armor evidence in the whole 1500s record, alongside *baluti*.
- **Its transmission line is Borneo to Sulu, at contact.** Pigafetta himself says
  these people were Moros banished from Brunei. That means the horn cuirass
  arrived in the archipelago's southwestern waters through a Bornean connection
  that was already in place in 1521. It does not make the item Visayan, Tagalog,
  or Luzon equipment, and it must not be generalized to any of them.
- **There is a manuscript variance, and it should be recorded.** Blair and
  Robertson's footnote 393 states plainly: "MS. 5,650 does not mention the
  cuirasses" (verified in the primary text). The Italian manuscript has them and
  the French one does not. This does not demote the claim below **Documented** —
  the Italian manuscript is the fuller and generally more detailed of the two,
  and the same abridging manuscript also compresses the entire Mactan battle —
  but a reader who is told the cuirass is Documented deserves to be told which
  manuscript documents it.

### 4.4 The Spanish relations, 1548 to 1577

Most of this subsection has now been read in Blair and Robertson's printed
volumes and is marked accordingly. The Lavezaris relation of 29 June 1573 and
the Sande relation of 1577 are the exceptions: those two still reach this
document through research summaries rather than through the page, and they are
marked *(unverified)* where they appear.

**Alvarado, Lisbon, 7 August 1548.** García Descalante Alvarado sailed with Ruy
López de Villalobos and wrote his account of the expedition from Lisbon in 1548,
addressed to the viceroy of New Spain. It is printed in Blair and Robertson
volume II, and it is the earliest Spanish description of Philippine defensive
equipment that this document has read in the primary text. Describing the
islands the expedition reached — the narrative around the passage is the
occupation of Sarangani and the dealings with Mindanao — he writes (verified in
the primary text):

> "The defensive arms are cotton corselets reaching to the feet and with
> sleeves; corselets made of wood and buffalo horn; and cuirasses made of bamboo
> and hard wood, which entirely cover them. Armor for the head is made of
> dogfish-skin, which is very tough."

This one sentence does more work than any other single line in the body-armor
record, and it had been missing from this document entirely.

- It names **four** distinct body-armor types — a sleeved cotton corselet
  reaching to the feet, a wood-and-horn corselet, and bamboo-and-hardwood
  cuirasses covering the whole body — in a relation written by a participant,
  inside the depicted period and twenty-two years before Legazpi.
- It names **head armor of dogfish skin**, which is discussed in section 5.
- It is an account of the southern islands, not of the Visayas or Luzon, so it
  broadens the regional spread of the evidence rather than duplicating it.

Each of the four is tiered **Documented, form uncertain**. Alvarado names the
material and, for the cuirasses, the coverage, and he names nothing else: no
thickness, no closure, no cut, no fastening, and no illustration accompanies
him. The class is attested; the object is not described.

Two consequences follow for tiers set elsewhere in this document. Bamboo or bark
body armor outside Sulu, and horn body armor outside Sulu, were previously
carried at **Provisional reconstruction** on the strength of later material.
Alvarado attests both directly in 1548, so both are raised to **Documented, form
uncertain**. Pigafetta's horn cuirass at Caghaian in 1521 and Alvarado's
wood-and-horn corselet in the south in the 1540s are now two independent
sixteenth-century attestations of horn as a body-armor material in the southern
archipelago, from two different voyages twenty-odd years apart.

**Cebu, 1565.** In an account of a skirmish shortly after Legazpi's arrival,
printed in Blair and Robertson volume II, the natives are described as having
put on their wooden corselets and rope armor before engaging, then hurling
lances at the boat in divisions of three, returning to their station, and coming
and going as in a game of *cañas* (verified in the primary text). The same
volume's earlier equipment list for the island reads "Their weapons are long
sharp iron lances, throwing-sticks, shields, small daggers, wooden corselets,
corded breastplates, a few bows and arrows, and culverins", which attests corded
breastplates alongside the wooden ones. Two details are
worth noting. The armor is described as something put on for the occasion,
which argues that it was war gear rather than daily clothing. And the Spanish
comparison to *cañas* — a Spanish equestrian cane-game — is a European reaching
for a European reference, and tells us more about how the movement looked to him
than about what it was.

**Artieda, 1573.** Diego de Artieda's *Relation of the Western Islands Called
Filipinas*, which Blair and Robertson date to 1573, is the single most-quoted
passage on this subject. Its wording, read in the printed volume, is (verified
in the primary text):

> "Those people have armor consisting of cotton-lined blankets, and others of
> rattan. Some wear corselets, made of a very hard black wood resembling ebony."

This is worth quoting exactly, because a paraphrase that begins "those people
have no armor except cotton-lined blankets" reverses what Artieda wrote and had
been carried in this document until the page was read. Artieda does not say
these people are unarmored. He says the opposite: they **have** armor, and he
names two materials for it, cotton-lined blankets and rattan.

The passage supports three separate readings and they should be kept apart.

- **Armor is present, in two named materials.** Cotton-lined blanket armor and
  rattan armor are both attested, without qualification, for the region Artieda
  is describing.
- **A third and different item is minority equipment.** The hardwood corselet is
  introduced with "some", and that word is Artieda's own. It marks the black-wood
  corselet as something only part of the population wore. It does not reach back
  and qualify the two materials in the preceding sentence.
- **Artieda gives no proportion for any of it.** He does not say what fraction
  wore the blanket or rattan armor, and nothing in him supports uniform issue any
  more than it supports its absence. He is evidence that the equipment existed
  and was seen, and he is not a census.

Artieda is also writing to a Crown he wants men and guns from, and his relation
sits in the same volume as the weapon descriptions that
`HISTORICAL_1500s_WEAPONS.md` draws on. The self-interest cuts both ways: it
gives him a reason to talk up how dangerous the opposition is, and a reason to
talk down how well equipped they are relative to what Spain could send. Neither
direction makes him useless, but it makes him evidence about equipment rather
than a census of it.

**Lavezaris, Manila, 29 June 1573.** Guido de Lavezaris, in *Affairs in the
Philippines*, describes Luzon fighters as wearing corselets of buffalo hide and
twisted knotted rope, and carrying shields and bucklers. This is the clearest
statement in the Legazpi-era material that hide and cordage armor were worn on
Luzon, and it is the report that the Category F hide corselet rests on.

**Cauchela and Aldave to Felipe II, Manila, 17 July 1574.** The letter of the
royal officials Andres Cauchela and Salvador de Aldave is printed in Blair and
Robertson volume XXXIV beginning at page 295. Its date is **17 July 1574**, not
17 March, and its page is **295**, not 397; both figures were wrong in earlier
versions of this document and both have now been read on the page. The volume is
available as Project Gutenberg ebook 47927, so the claim recorded here
previously that no scan of it could be located was also wrong.

The region matters as much as the date. The passage is not about Luzon in
general: Cauchela and Aldave are reporting on **Camarines** specifically,
introducing it as a province in the eastern part of Luzon that had lately been
explored and pacified. The sentence reads in full (verified in the primary text):

> "The men are warlike and well armed for Indians; for they have corselets of
> buffalo hide, iron greaves, and helmets set with fishbones and stout shells,
> which no weapon except the arquebus can damage."

**The arquebus clause is ambiguous, and this document does not resolve it.** The
relative clause "which no weapon except the arquebus can damage" follows a list
of three items — hide corselets, iron greaves, and shell-set helmets — with no
punctuation or wording that separates the last from the first two. On the plain
reading of the sentence the clause covers the whole list. It is possible to read
it as attaching only to the helmets, on the grounds that a relative clause most
naturally modifies the nearest noun phrase, but that is the more strained of the
two readings, not the obvious one.

Earlier versions of this document asserted the narrow reading as though it were
what the source said, and warned readers away from the broad one. That was
backwards: the narrow reading is itself the inference. The honest position is
that the sentence will bear either reading, that Spanish word order gives no
decisive help, and that nothing else in the sixteenth-century record settles it.
What can be said without inference is that a Spanish official in 1574 wrote that
Camarines fighters had equipment an arquebus was needed to get through, and did
not say which piece of it he meant.

The iron greaves in the same report are worth flagging for a different reason,
and the reason is not the one this document used to give. They are **not**
mentioned once in a single document. Two officials wrote to Felipe II from
Manila on the same day, 17 July 1574, and both name iron leg armor in Camarines:
Cauchela and Aldave give "iron greaves", and Lavezaris, in the letter discussed
in section 4.6, gives "iron corselets, greaves, wristlets, gauntlets, helmets".

That correction cuts in both directions and it would be dishonest to report only
the half that strengthens the claim. Two documents are better than one, and the
document count in this record was wrong. But two letters written in the same
fortnight, from the same city, by officials of the same administration, about
the same Salcedo campaign into the same province, are not two independent
traditions. They are two reports out of one information environment, and if the
identification of the leg armor as iron was mistaken in one of them there is
every reason it would be mistaken in the other. The right conclusion is that
iron leg armor in Camarines in 1574 has two witnesses and one source of
knowledge.

**Sande, 1577.** Francisco de Sande's relation of 1577 has now been read in the
primary text, and it does not describe Chinese-supplied equipment reaching local
hands. What it says about native arms is general and brief: "The Indians have
thousands of lances, daggers, shields, and other pieces of armor, with which
they fight very well." That attests body armor as widespread but names no type,
no material, and no region, and it appears inside an argument to the Crown for
more arquebuses — "All those who have been killed since the coming of Miguel
Lopez de Legazpi received their death through lack of arquebuses" — so the
"thousands" is rhetoric rather than a count. Tier: `Documented` that armor was
widespread; nothing about its form. It sits alongside the 1567
Legazpi report of Chinese arquebuses in local use that
`HISTORICAL_1500s_WEAPONS.md` already records. The relevant point for armor is
that the archipelago in the 1500s was inside an active trade network, and the
presence of a foreign-made item in local hands is not evidence of local
manufacture — nor is it evidence against local use. This claim is *unverified*
and its exact wording should be checked before it is relied on.

### 4.5 What the Spanish relations add up to

Setting confidence honestly: the **class** of soft fiber, hide, wood, horn, and
bamboo body armor in the Philippine archipelago between 1548 and 1577 is
**Documented**, on several Spanish reports naming at least nine materials across
the southern islands, Cebu, Luzon, and Camarines. The **cut, coverage, closure,
thickness, and appearance** of any of it is **Provisional reconstruction** in
almost every case, because no sixteenth-century source describes construction,
no sixteenth-century illustration of a Philippine body armor is known to this
document, and no dated sixteenth-century object survives. The two exceptions are
small and both come from Alvarado in 1548: a cotton corselet that reached to the
feet and had sleeves, and cuirasses that covered the wearer entirely. Those are
coverage statements, not construction statements, and they are the only ones in
the record.

The **prevalence** of body armor is uncertain, and less strongly argued against
by the sources than this document once claimed. Artieda's "some" governs the
hardwood corselet alone; his blanket and rattan armor carry no such
qualification. What remains is a real pattern and a weaker one than the word
"some" made it look: the 1565 Cebu account describes armor being put on for a
specific engagement rather than worn as daily dress, and the two most detailed
ethnographies of the period, discussed in section 9, describe no Philippine body
armor at all. Against that stand Alvarado in 1548 and Cauchela and Aldave in
1574, neither of whom qualifies the armor they describe. The honest summary is
that prevalence is unknown, that no source states it, and that the balance of
the evidence still points away from universal issue without establishing rarity.

### 4.6 The contested Camarines iron corselets

Guido de Lavezaris wrote two letters to Felipe II from Manila in July 1574, on
the seventeenth and the thirtieth of the month; they are printed together in
Blair and Robertson volume III beginning at page 272. In the letter of **17 July
1574**, at page 273, describing the Salcedo expedition into Los Camarines in
Bicol, he writes that the people there are "the most valiant yet found in these
regions; they possess much good armor — as iron corselets, greaves, wristlets,
gauntlets, and helmets — and some arquebuses and culverins" (verified in the
primary text).

That date is the same day on which Andres Cauchela and Salvador de Aldave wrote
their own letter to the same king from the same city, describing the same
newly-pacified province. Section 4.4 sets out what follows from that: two
witnesses, one information environment.

William Henry Scott, in *Barangay: Sixteenth-Century Philippine Culture and
Society* (1994), argues that this is "no doubt a confusion with Japanese
weapons" *(this document has read the passage only in an
optical-character-recognition text of Scott, not in the printed book, and the
page number is not confirmed)*.

His full sentence is more specific than a bare charge of confusion. It reads:
"But the arquebuses, artillery, helmets, and full body armor of iron which were
reported from the Salcedo expedition of 1573 were no doubt a confusion with
Japanese weapons which that conquistador encountered the year before in a naval
engagement on the Ilocos coast." Scott is not merely saying the report looks
Japanese; he is naming an occasion on which Salcedo had recently seen Japanese
equipment at close quarters, and proposing that the report carried it into the
wrong province. Note that he dates the expedition to 1573 where Lavezaris's
letter is of July 1574.

Both positions are recorded here and **neither is adopted**. The case for
Lavezaris is that he was a senior official reporting on a specific region, that
Bicol sat on trade routes that reached Japan, and that a second letter of the
same day independently names iron greaves in the same province. The case for
Scott is that a full iron harness of corselet, greaves, wristlets, gauntlets,
and helmet is exactly what Japanese armor looks like to a Spaniard, that the
description is an outlier against everything else in the period record, that
Salcedo had fought Japanese ships on the Ilocos coast the previous year, and
that the second letter is not an independent tradition but a second report out
of the same expedition's news.

Morga supplies a piece of period support for Scott's mechanism that this
document previously lacked. Every occurrence of the word "armor" in Morga's
*Sucesos* refers to Japanese equipment, and one of them describes Japanese
armour arriving at Manila as ordinary trade cargo: the annual ships from
Nagasaki brought, among their goods, "many suits of armor, spears, catans, and
other weapons, all finely wrought" (verified in the primary text). Japanese
harness was a thing a Spaniard in the Philippines saw, handled, and could
mistake something for. That does not decide the question — it establishes that
Scott's proposed confusion was materially available, not that it occurred.

The practical consequence is that iron body armor on Philippine fighters in the
1500s cannot be labeled **Documented** and cannot be labeled excluded. It is
**contested**, and it stays in section 12 as an open question until somebody
reads Scott's page in the printed book. The Blair and Robertson page has now
been read; the Scott side has been read only in an
optical-character-recognition text.

### 4.7 Explicit exclusions

**Brass-and-horn plate armor of the *kurab-a-kulang* type.** This is the
armor most people picture when they hear "Philippine armor": overlapping brass
plates joined to carabao horn, laced together, sometimes with a mail component.
Its documented history begins far outside the depicted period *(all of the
following unverified)*. Museum accessions and published descriptions cluster in
the 1920s and 1930s; one British Museum example carries an 1876 accession. The
hundred-year attestation rule in `CLAUDE.md` section 7 excludes it from the
1500s for the same reason and by the same margin that it excluded the panabas
from the weapons roster, and a `PROVISIONAL` badge is not an acceptable
substitute for a gap of roughly three centuries.

**Mail as Philippine equipment.** In the sixteenth-century sources mail appears
as the thing that gets pierced: a Luzon account describes palm-wood lances hard
enough to go through mail *(unverified)*. That is a sentence about foreign
equipment, written from the point of view of the people wearing it. It is not
evidence that anyone in the archipelago wore mail in the 1500s. Mail-and-plate
composites belong to the *kurab-a-kulang* tradition above and are excluded on
the same grounds.

**A note on museum dating, which applies to every excluded item.** Almost all of
the dating that supports a "seventeenth-century" or "eighteenth-century" label
on a surviving Philippine armor is stylistic curatorial judgement, not a
documented provenance chain. Of the objects reported to this document, only the
1876 British Museum accession has a documentary chain at all — and an accession
date records when a museum acquired an object, not when anybody made it. An
object accessioned in 1876 could have been made in 1870 or in 1750. Nothing in
the surviving object record pushes any of this equipment back into the
sixteenth century, and treating a curatorial style estimate as though it were a
provenance is how three-century gaps get quietly closed. *(All museum dating
statements in this document are unverified.)*

## 5. Head protection

### 5.1 Summary table

As in section 4.1, the Confidence column carries only a section 2 tier, and any
contested or out-of-window status sits in its own column.

| Item | Region and year | Source basis | Confidence | Status | Verification |
| --- | --- | --- | --- | --- | --- |
| Head armor of dogfish skin | Sarangani and the islands around it, from the Villalobos expedition of 1543-1546 | Alvarado, Lisbon, 7 August 1548: "Armor for the head is made of dogfish-skin, which is very tough." | Documented | Inside the window | Verified in the primary text |
| Helmet set with fishbones and stout shells | Camarines, 1574 | Cauchela and Aldave to Felipe II, Manila, 17 July 1574, Blair and Robertson volume XXXIV page 295. Part of a three-item list said to resist any weapon except the arquebus; see 5.2. | Documented, form uncertain | Inside the window; same campaign as the Lavezaris helmet row below | Verified in the primary text |
| Wooden helmet faced with shark skin | Visayas, sixteenth century | Scott (1994), reading sixteenth-century Visayan dictionaries; corroborated by Alvarado's dogfish skin, a dogfish being a small shark. | Documented, form uncertain | Inside the window | Scott read only in an optical-character-recognition text; the dictionary entries behind him unread |
| Wooden helmet covered with octopus skin, plumed | Sarangani, from the Villalobos expedition | Scott (1994), in his Sarangani Island section, describing the defenders of the island; not from Alvarado's relation, which names dogfish skin and no octopus. See 5.3. | Provisional reconstruction | Inside the window | Scott read only in an optical-character-recognition text; his own source unread |
| Colored cap compared to a *morrión*, worn with quilted armor | Cagayan, about 1590 | Boxer Codex. | Documented, form uncertain | Inside the window | Unverified |
| Iron helmet | Los Camarines, Bicol, 1574 | Lavezaris, in the same passage as the iron corselets. | Documented, form uncertain | Contested; see 4.6 | Lavezaris verified in the primary text; Scott's reading verified only in an optical-character-recognition text |
| *Putong* head-cloth | Widely, sixteenth century | A status marker, not protective equipment. See 5.4. | Documented | Clothing, not armor | Unverified |
| Spanish corselets and helmets at Mactan, 1521 | Mactan, 1521 | Pigafetta records sixty of his own men setting out armed with corselets and helmets, and Magellan's helmet being knocked off twice. | Documented | European equipment, not Philippine | Verified in the primary text |

### 5.2 The 1574 shell-set helmet, and what it does and does not prove

The Cauchela and Aldave letter of 17 July 1574 is the only sixteenth-century
description of a Philippine helmet with any construction detail in it: helmets
set with fishbones and stout shells, in a list whose closing clause is "which no
weapon except the arquebus can damage" (verified in the primary text). The
source spells *fishbones* as one word.

This is a striking claim and it should be handled carefully in both directions.

- **It is not only a helmet claim.** Section 4.4 sets out the grammar. The
  relative clause follows a list of three items — hide corselets, iron greaves,
  and shell-set helmets — and on the plain reading it covers all three. Earlier
  versions of this document asserted that it belonged to the helmets alone and
  treated the wider reading as a mistake. That was itself the inference, and the
  less natural of the two. Neither reading is adopted here. What the source
  supplies is a clause about a described set of equipment; which member of the
  set it modifies is not resolved by anything in the text.
- It is a **Spanish assessment**, not a test result. "No weapon except the
  arquebus can damage" is a soldier's impression written into a report to a
  king who was being asked for resources, and the sentence works rather well as
  an argument that the men on the ground needed firearms. That does not make it
  a lie. It makes it an assessment.
- It is **one province, one year, and two letters written on one day**. The
  Cauchela and Aldave letter and the Lavezaris letter of 17 July 1574 both
  concern the same Camarines campaign, so the head-protection evidence for that
  province is two reports out of one information environment rather than two
  independent ones. It supports depicting the item as exceptional. It does not
  support depicting it as standard head protection anywhere.

### 5.3 The other head-protection lines, and what each of them actually rests on

Three further items reach this document. They are not all of the same strength,
and an earlier version of this section overstated two of them badly enough that
the correction is the substance of the section.

**Head armor of dogfish skin, the southern islands, 1548** (verified in the
primary text). This is the strongest head-protection evidence in the record and
it is stronger than the 1574 shell-set helmet, because it is a participant's
relation naming a material without hedging: "Armor for the head is made of
dogfish-skin, which is very tough." Alvarado's account of the Villalobos
expedition is discussed in section 4.4; the head-armor sentence closes the same
passage that gives the four body-armor types. Head protection of dogfish skin in
the southern archipelago in the 1540s is **Documented**.

A dogfish is a small shark. That single fact does real work here, because it
means Alvarado independently corroborates the item that this document had until
now carried as resting on Scott alone.

**Wooden helmets faced with shark skin, Visayas.** Scott, *Barangay* (1994),
writing on Visayan defensive arms, states that "shark-skin was used effectively
for helmets or morriones". This reached the document through Scott and has now
been read in an optical-character-recognition text of him rather than in the
printed book; the sixteenth-century dictionary entries he is reading remain
unread. What has changed is that the claim is no longer one citation deep and no
longer Scott-only. A near-primary Spanish relation of 1548 names head armor of
small-shark skin in the archipelago, from a different region and a different
line of transmission entirely. Shark or dogfish skin as a helmet facing is
therefore **Documented, form uncertain**: the material and the use are attested,
the Visayan wooden-helmet form that Scott adds to it is not.

**Wooden helmet covered with octopus skin and plumed, Sarangani.** This claim is
**Scott's, not a sixteenth-century relation's**, and this document previously
attributed it to "the Villalobos relation of 1543" and built an argument on that
attribution. Both the attribution and the argument were wrong.

What Scott writes, in his Sarangani Island section, is that the defenders came
out to battle behind body-length shields and war drums, "armed with cutlasses
and bows and arrows, and wore body armor of quilted cotton or wild boar hide,
with wooden helmets covered with octopus skin", with waving plumes on shields
and helmets and both painted in bright colors. The passage does describe the
Villalobos expedition's seven-month occupation of Sarangani, so the event and
the region were right. The source was not: Scott cites an anonymous relation
there, and the Villalobos-expedition relation this document has now read —
Alvarado's of 1548 — says dogfish skin, mentions no octopus, and mentions no
plumes.

The consequence is that the "two independent source traditions" argument this
section used to make collapses entirely, and it is withdrawn. The octopus-skin
helmet and the shark-skin helmet both reach this document through Scott. Worse
for the old argument, the octopus-skin helmet and Alvarado's dogfish-skin head
armor describe the **same expedition at the same island**, so far from being two
widely separated regions they are two accounts of one encounter. Whether they
are two accounts of one object, described by two observers who disagreed about
what the skin came from, is an open question and is added to section 12. Until
Scott's own source is identified and read, the octopus-skin plumed helmet is
**Provisional reconstruction** and belongs in the Scott-only inventory in
section 7.5.

**The Boxer Codex cap, Cagayan, about 1590** *(unverified)*. The Boxer Codex
Cagayan figure is described as wearing a colored cap that the accompanying text
compares to a *morrión* or helmet, worn together with quilted armor. Two
cautions apply. First, *morrión* is a **Spanish comparative**: it is the writer
reaching for the Spanish infantry helmet his reader would know, and it is not
the Philippine item's name, shape, or construction. Second, the Boxer Codex is
pictorial evidence with European patronage and partly Chinese pictorial
convention, and repository policy already restricts it to guiding silhouette and
color rather than technical detail. What the figure supports is that a head
covering was worn together with quilted body armor in Cagayan around 1590. What
it does not support is that the head covering was shaped like a morion.

### 5.4 The *putong* is a status marker, not armor

The *putong*, the wound or wrapped head-cloth, appears throughout the
sixteenth-century material and is one of the most consistently described items
of Philippine dress *(unverified in its lexical details)*. It carried social
meaning: color, the manner of winding, and the right to wear a particular form
were markers of standing and, in some accounts, of achievement.

It is not protective equipment, and it must not be counted as head protection,
listed in an armor layer, or given any defensive significance. This is worth
stating explicitly because a head-cloth and a padded cap look similar in a
top-down silhouette, and because the temptation to treat every head covering as
proto-armor is exactly the kind of quiet upgrade this document exists to
prevent. A *putong* belongs with clothing and status, alongside the waist cloth
and the sash — which is where the appearance research already places its
equivalents.

The same caution runs in the other direction. Because the *putong* is well
attested and helmets are barely attested, depicting head-cloths broadly and
helmets rarely is the reading the evidence supports. Depicting helmets broadly
because "warriors wore helmets" is not.

### 5.5 What Pigafetta's helmets actually were

Pigafetta's 1521 account mentions helmets three times in the Mactan sequence,
and every one of them is European (verified in the primary text). Sixty men of
the expedition set out "armed with corselets and helmets". During the fight the
Mactan warriors knocked Magellan's helmet off his head twice. And the Spanish
legs were bare, which is why Pigafetta says the natives shot at the legs.

That last detail is frequently misread as evidence about Philippine equipment.
It is not. It is a description of a gap in Spanish equipment — corselet and
helmet above, nothing below — and of an opponent who noticed the gap and
exploited it. It is excellent evidence about tactics and observation. It is no
evidence at all about what anyone from Mactan was wearing.

## 6. Shields

Shields are the strongest part of the entire protective-equipment record. Where
body armor is minority equipment described by a handful of Spanish reports,
shields appear in essentially every sixteenth-century account that describes
Philippine fighters at all, from 1521 onward, across every region the sources
reach. `WEAPON_CLASH_1500s.md` section 2.1 already states this and the finding
is not disturbed here.

### 6.1 Summary table

| Item | Region and year | Source basis | Confidence | Status | Verification |
| --- | --- | --- | --- | --- | --- |
| Large shields | Eastern Samar, near Homonhon, March 1521 | Pigafetta describes the people using daggers, knives, gold-ornamented spears, large shields, javelins, and fishing nets. | Documented | Inside the window | Verified in the primary text |
| Bucklers carried by a ruler's men | Limasawa and Butuan, 1521 | Pigafetta: the king's men stood around them in a circle with swords, daggers, spears, and bucklers. | Documented | Inside the window | Verified in the primary text |
| Shields of thin wood, penetrable by shot | Mactan, 1521 | Pigafetta: the Spanish shot "only passed through the shields which were made of thin wood and the arms [of the bearers]". | Documented | Inside the window | Verified in the primary text |
| Shields used actively for cover, with movement | Mactan, 1521 | Pigafetta: they leaped hither and thither, covering themselves with their shields; in the abridged manuscript, the arrows were "in vain, for they received them on their shields". | Documented | Inside the window | Verified in the primary text |
| Shields, Visayan *calassan* | Visayas, 1521 | Pigafetta's Visayan vocabulary glosses *calassan* as shields. | Documented | Inside the window | Verified in the primary text |
| Bucklers | Caghaian, between Palawan and Borneo, 1521 | Pigafetta, in the same passage as the buffalo-horn cuirasses. | Documented | Inside the window | Verified in the primary text |
| Shields | Cebu, 1565 | The Legazpi-era skirmish account lists shields both in the island's general equipment list and among what the natives took up with their wooden corselets and rope armor. | Documented | Inside the window | Verified in the primary text |
| Shields and bucklers | Luzon, 1573 | Lavezaris, *Affairs in the Philippines*. | Documented | Inside the window | Unverified |
| Body-length shields, plumed and brightly painted | Sarangani, from the Villalobos expedition | Scott (1994), describing the defenders of Sarangani. See 5.3. | Provisional reconstruction | Inside the window | Scott read only in an optical-character-recognition text; his own source unread |
| Breast-high shield, little more than half a *vara* wide | Unspecified, 1573 | Artieda. See 6.4. | Documented, form uncertain | Inside the window | Verified in the primary text |
| Shield of light wood with armholes fastened on the inside, covering the bearer from top to toe, called *carasas* | Unspecified, 1609 | Morga, *Sucesos*. Blair and Robertson gloss the word in brackets as *kalasag*. See 6.5. | Documented | Outside the window, 1609 only | Verified in the primary text |
| Large shields carried with spears and blades | Visayan, Tagalog, Zambal, and Cagayan figures, about 1590 | Boxer Codex. | Documented, form uncertain | Inside the window | Unverified |

### 6.2 The 1521 shield evidence in detail

Four separate 1521 observations, all verified in the primary text, establish
shields as universal, large, and actively used equipment.

**Eastern Samar, March 1521.** Describing the people near the anchorage he calls
the Watering-place of Good Signs, Pigafetta writes that they "use daggers,
knives, and spears ornamented with gold, large shields, fascines, javelins, and
fishing nets that resemble rizali". The shields are described as large, and they
are listed as
ordinary equipment rather than as something remarkable.

**Limasawa and Butuan, 1521.** When Pigafetta was received ashore, "the king's
men stood about us in a circle with swords, daggers, spears, and bucklers." This
is a ceremonial and honor-guard context rather than a battle, which is exactly
why it is useful: it shows shields as part of the normal armed presentation of a
ruler's following, not merely as battlefield equipment.

**Mactan, 1521, construction.** This is the single most valuable
sixteenth-century sentence on Philippine shield construction, and it is
negative:

> "The musketeers and crossbowmen shot from a distance for about a half-hour,
> but uselessly; for the shots only passed through the shields which were made
> of thin wood and the arms [of the bearers]."

Two facts fall out of it. First, the shields were **thin wood** — an eyewitness
material statement, not a reconstruction. Second, they did **not** stop
sixteenth-century crossbow bolts or arquebus balls, and the projectiles carried
on through the shield and into the arm behind it. Any claim that Philippine
shields of this period were proof against anything has to be reconciled with
this sentence, and it generally cannot be.

**Mactan, 1521, use.** Pigafetta says that when the muskets were discharged the
warriors "would never stand still, but leaped hither and thither, covering
themselves with their shields." The abridged French manuscript, MS. 5,650, gives
the same picture from the other side: "We shot many arrows at them from a
distance, but it was in vain, for they received them on their shields. They
leaped hither and thither in such a way that scarce could we wound one of them."
Two manuscripts independently describe the same combination of mobility and
shield cover. That combination — a shield that will not stop a bolt, carried by
someone who does not stand still — is coherent, and it is the closest thing the
1521 record gives to a description of how the equipment was used.

### 6.3 *Calassan*, and how the modern spelling arose

Pigafetta's Visayan vocabulary contains (verified in the primary text):

- Italian: *Ali targoni — calassan*
- Blair and Robertson's English: "for Shields — *calassan*"

The 1521 text spells the word **`calassan`**. The familiar modern spelling
*kalasag* does not appear in Pigafetta.

The chain by which *kalasag* became the standard form runs through Blair and
Robertson's own editorial apparatus. Their footnote 376 sets out a concordance
of Pigafetta's Visayan vocabulary against two later dictionaries, and the row
for shields reads (verified in the primary text):

| English | Pigafetta | Encarnación, 1885 | Sanchez de la Rosa, 1895 |
| --- | --- | --- | --- |
| shield | *calassan* | *calasag* | *calasag* |

So *calasag* is what two late-nineteenth-century Visayan dictionaries give, and
the identification of Pigafetta's *calassan* with it is the Blair and Robertson
editors' 1906 judgement, published in their translation. That judgement is
almost certainly correct — the phonetic distance is small and the meaning
matches exactly — but it is a judgement, made three hundred and eighty-five
years after the word was written down, on the strength of dictionaries compiled
three hundred and sixty years after it.

The practical consequence is a small one but it should be stated: the term
*kalasag* is **Documented** for 1521 as a word for shields, on Pigafetta's
authority; the **spelling** *kalasag* is a nineteenth-century dictionary form
normalized by twentieth-century editors. Anyone quoting the 1521 attestation
should know which of those two things they are quoting.

### 6.4 Dimensions

Very little of the surviving evidence gives a number, and what numbers exist
should be handled with more care than they usually get.

**Artieda, 1573** (verified in the primary text): "The weapons they use are the
following: shields, breast-high, and little more than half a *vara* wide." The
phrase is "little more than half a *vara*", without an article before "little",
and it is quoted that way here because the figure is small enough that a
paraphrase can move it.

Converting it needs one step that the source does not take. Blair and
Robertson's note 67 does **not** give a *vara* in centimeters. It prints a table
of Spanish measures of length, of which the two rows that matter are "2 sesmas =
1 piè (the foot, = 11.128 U. S. inches)" and "3 piès = 1 vara". Multiplying and
converting gives one *vara* of about 84.8 centimeters, and therefore a width of
somewhere around 42 centimeters or slightly more. **That arithmetic is this
document's, not the editors'**, and the 84.8-centimetre figure should never be
attributed to note 67 as though Blair and Robertson had printed it. Note
carefully what is and is not given:

- **Width: a number.** Roughly 42 centimeters and a bit, derived from a stated
  fraction of a stated unit.
- **Height: not a number.** "Breast-high" is a body-relative description. It
  scales with whoever is holding the shield and it is not a measurement.
  Converting it into centimeters requires assuming a stature, and that assumed
  stature would then be doing all the work.

A shield roughly 42 centimeters wide and reaching to the bearer's chest is a
tall narrow shield, which is a genuinely useful shape constraint. It is not a
specification.

**The Boxer Codex Cagayan shield width: do not quote it.** A width figure for
the Boxer Codex Cagayan warrior's shield circulates in secondary material, and
it should not be used. The figure depends on a bracketed phrase, "[of a
fathom]", which is a modern translator's editorial insertion rather than text
present in the manuscript. Depending on how the insertion is read, the resulting
width is either roughly 127 centimeters or roughly 64 centimeters — a factor of
two apart, which is the difference between a body shield and a large buckler.
This document records the figure as **unresolved** and uses no number from it.
It is listed again in section 12.

**The "about 50 by 150 centimeters" figure, and where it actually comes from.**
`WEAPON_CLASH_1500s.md` section 2.1 records that secondary summaries give
approximately 50 by 150 centimeters for a large rectangular type, and flags that
this figure, and the "sword-proof enmeshing" construction story attached to it,
propagate through community and machine-generated encyclopedia pages faster than
they can be traced to a page. The page has now been traced, and it is **Scott,
*Barangay* (1994), page 151** — not Junker (1999), to whom this document
previously attributed it. Scott writes there that the *kalasag* "was
strengthened and decorated with rattan binding coated with resinous pitch, and
was of sufficient size to give full body protection — about 50 by 150
centimeters."

Three separate claims travel in that one sentence and all three had the wrong
attribution in this document until now: the rattan binding, the resinous pitch
coating, and the 50-by-150-centimetre size. All three are Scott's. The
consequences are set out in section 8, which had built three negative findings
on the misattribution.

Tracing the figure to Scott does not make it a measurement. Scott gives no
sixteenth-century source for the dimensions in that sentence, and no
sixteenth-century text reaching this document gives any size for a Philippine
shield except Artieda's width. The figure is **Provisional reconstruction**: a
modern scholar's summary of a general shield type, not a period measurement, and
no number from it is treated as evidence here.

### 6.5 Morga's *carasa*, 1609, and why it is a boundary marker

Antonio de Morga's *Sucesos de las Islas Filipinas* of 1609 reads, in the
passage on weapons (verified in the primary text): "those generally used
throughout the islands are moderate-sized spears with well-made points; and
certain shields of light wood, with their armholes fastened on the inside. These
cover them from top to toe, and are called *carasas* [*kalasag*]."

Three points of detail should be recorded before the interpretation.

- **Morga's form is plural, *carasas*.** He also uses the spelling *carazas*
  elsewhere in the book. This document previously cited the singular *carasa* as
  though that were his word.
- **The bracketed gloss is Blair and Robertson's**, and it identifies the word as
  *kalasag*. That identification is the editors' judgement, exactly as their
  concordance of Pigafetta's *calassan* is in section 6.3, and it belongs on
  record because it means the 1609 term and the 1521 term are held by the
  editors to be the same word.
- **Morga is transcribing a local name**, not translating one. Nothing in the
  passage marks the word as Spanish or as a Hispanised form, and this document
  offers no such claim.

Two features of the description itself are worth having on record.

- **The grip is an inside arm-strap, not a centre boss.** A shield held by
  armholes fastened on the inside is carried on the forearm and is handled quite
  differently from a centre-gripped shield. This is a structural detail that no
  sixteenth-century source in this document supplies.
- **The coverage is full-body**, which is a different object from Artieda's
  breast-high shield of 1573.

But Morga is 1609, which is outside this document's window. He is cited here as
a **boundary marker**: he shows what an observer wrote down about shields once
somebody finally described one properly, and by contrast he shows how little the
sixteenth-century sources actually said. He must not be used to fill in
sixteenth-century construction detail. A shield described in 1609 is evidence
for 1609.

### 6.6 What the sixteenth-century sources say about shield construction

Almost nothing. That deserves a plain list, because the gap between what the
sources say and what is commonly asserted is very wide here.

The sixteenth-century record supports:

- shields were present everywhere, across every region and decade the sources
  reach (**Documented**);
- some were large and some were small enough to be called bucklers
  (**Documented**);
- at Mactan in 1521 they were made of thin wood (**Documented**);
- at Mactan in 1521 they did not stop crossbow bolts or arquebus balls
  (**Documented**);
- they were used with active movement rather than as a static wall
  (**Documented**);
- Artieda's 1573 example was breast-high and little more than half a *vara*
  wide, which works out at roughly 42 centimeters (**Documented, form
  uncertain**, verified in the primary text, and with the conversion this
  document's own); and
- the Boxer Codex around 1590 shows large shields carried with spears and
  blades by Visayan, Tagalog, Zambal, and Cagayan figures (**Documented, form
  uncertain**, and *unverified*).

That is the entire list.

## 7. Terminology verdicts

`CLAUDE.md` section 7 requires that a cultural identification appear in
player-facing text only in pair form — the Filipino name, an em dash, and a
plain English descriptor — and only where the evidence tier is recorded, and
never where the earliest attestation postdates the depicted period by more than
a century. This section applies that rule to every protective-equipment term
reaching this document, and records a verdict of **USE**, **EXCLUDE**, or
**OPEN** for each.

A verdict of **OPEN** is not a soft yes. Until an OPEN term is resolved it may
not be used as a player-facing label at all, in pair form or otherwise. It may
be recorded in inspector metadata or in research notes as a term under review.

| Term | Meaning | Earliest attestation reaching this document | Verdict | Pair-form label if used |
| --- | --- | --- | --- | --- |
| *baluti* | Quilted or padded garment worn for fighting | Pigafetta's Visayan vocabulary, 1521 (verified in the primary text) | **USE** | **Baluti — Quilted Armor** |
| *calassan* / *kalasag* | Shield | Pigafetta's Visayan vocabulary, 1521, spelled *calassan* (verified in the primary text) | **USE** | **Kalasag — War Shield**, with the spelling note in 6.3 recorded in metadata |
| *tamin* / *taming* | Small round buckler | Contested; see 7.3. Scott's text gives the form *tamin*; *taming* is the form in wider circulation | **OPEN** | None permitted until resolved |
| *palisay* | Round buckler, glossed as used in dances | San Buenaventura, *Vocabulario de la lengua tagala*, 1613, page 122 *(unverified)* | **OPEN**; see 7.4 | None permitted until resolved |
| *barote* | Corded or quilted body armor of braided abaca or bark cord | Scott (1994), reading sixteenth-century Visayan dictionaries *(read only in an optical-character-recognition text of Scott)* | **OPEN**; see 7.5 | None permitted until resolved |
| *pakil* | Breastplate or backplate of bamboo, bark, hardwood, or horn | Scott (1994) only *(read only in an optical-character-recognition text of Scott)* | **OPEN**; see 7.5 | None permitted until resolved |
| *batung-batung* | Breastplate or backplate of bamboo, bark, hardwood, or horn | Scott (1994) only *(read only in an optical-character-recognition text of Scott)* | **OPEN**; see 7.5 | None permitted until resolved |
| *habay-habay* | A burlap-like undergarment worn next to the body beneath the *barote*. Not armor and not a shield | Scott (1994) only *(read only in an optical-character-recognition text of Scott)* | **OPEN**; see 7.5 | None permitted until resolved |
| *carasas* / *carasa* | Full-length shield with inside armholes | Morga, 1609, plural *carasas*, glossed by Blair and Robertson as *kalasag* (verified in the primary text) | **OPEN**; see 7.6 | None permitted until resolved |
| *kurab-a-kulang* | Brass-and-horn plate armor | Nineteenth- and twentieth-century objects and records *(unverified)* | **EXCLUDE**; see 4.7 | None |
| *morrión* / morion | Spanish infantry helmet | A Spanish comparative used about a Cagayan cap around 1590 *(unverified)* | **EXCLUDE** as a Philippine item name; see 5.3 | None; it is not a Philippine term |

### 7.1 *Baluti* clears the pair-form bar outright

**Baluti — Quilted Armor** is the best-supported protective-equipment label
available for the 1500s, and it is better supported than several weapon labels
already in use. The attestation is a sixteenth-century eyewitness recording the
word and giving its meaning, in the year 1521, in the Visayas, with the meaning
he gives being specifically martial: quilted garments used for fighting. There
is no gap to bridge, no later dictionary standing in for a period source, and no
modern scholar's reading in the chain. The Encarnación 1885 form *baloti*
confirms continuity but is not needed to establish the attestation.

The pair form still applies, because the pair form is not a concession made when
evidence is weak — it is the standing rule. The plain descriptor "Quilted Armor"
is what the game guarantees; *baluti* is what the 1521 record offers. The
regional scope is Visayan and must be recorded as such. It is not a term for
Luzon, for Mindanao, or for the archipelago.

### 7.2 *Kalasag* clears the bar, with a spelling note

**Kalasag — War Shield** is likewise supported by a 1521 attestation, subject to
the spelling history in section 6.3. The evidence tier shown in the inspector
should record that the 1521 text spells the word *calassan* and that the modern
*kalasag* comes through nineteenth-century Visayan dictionaries by way of the
1906 Blair and Robertson editorial concordance. That is a note, not a
disqualification.

### 7.3 *Tamin* or *taming* is contested and stays OPEN

Two research positions on the term for a small round buckler reached this
document and they do not agree. A third question has since been added to them,
which is what the word is.

**The spelling is unsettled and this document cannot settle it.** The text of
Scott consulted for this document reads "a small round buckler called *tamin*
appears to have been copied from the Moluccans or the Spaniards themselves" —
*tamin*, with no final *g*. The form in wider circulation, and the form this
document used throughout until now, is *taming*. Both are recorded here and
neither is preferred, for a specific reason: the Scott text consulted is an
optical-character-recognition transcription, not the printed book, and the same
few pages of it visibly mangle other words, rendering *morriones* as "monones".
A dropped final letter is exactly the error that kind of text makes. The finding
that Scott spells it *tamin* is therefore a finding about a scan, and the
printed page has to be read before either form can be relied on.

**The case for USE.** Scott, *Barangay* (1994), records the term from
sixteenth-century Visayan dictionaries and judges the item to have been copied
from the Moluccans or the Spaniards. If that reading holds, the term is attested
inside the window.

**The case for EXCLUDE.** No pre-1650 Philippine attestation of either form was
found in any primary text read directly during this research *(a genuine
negative result, but a negative result from a limited search)*.

**What was verified.** Neither form appears in Pigafetta's 1521 Visayan
vocabulary. That vocabulary gives *calassan* for shields and nothing else in the
shield family. This was checked in the primary text. It is a real data point and
it is a limited one: Pigafetta recorded a few hundred words, and absence from
his list is not absence from the language.

Note also that Scott's own gloss contains a complication for anyone hoping to
use the term as a marker of indigenous equipment: he judges the item borrowed. A
term can be attested in a period dictionary and still describe an imported
object.

**Verdict: OPEN.** Until somebody reads the printed Scott page and, better, the
dictionary entry behind it, neither *tamin* nor *taming* may be used as a
player-facing label in any form, and the spelling question stays open with them.

### 7.4 *Palisay* clears the date bar but not the martial bar

San Buenaventura's Tagalog *Vocabulario* of 1613, at page 122, is reported to
gloss *palisay* as a round buckler, with the note *usase en danzas* — used in
dances *(unverified)*.

The date is fine. A 1613 Tagalog vocabulary is compiled from usage that predates
its publication, sits barely outside the depicted period, and is nowhere near
the hundred-year limit that excluded the panabas.

The gloss is the problem. A lexicographer who defines a buckler by saying it is
used in dances is telling us about its living context at the time of writing,
and that context is ceremonial rather than martial. That does not prove the item
was never a fighting shield — dance forms across the archipelago preserve
martial movement, and the reverse inference is at least as plausible — but it
means the one attestation available describes a ceremonial use.

**Verdict: OPEN**, with the caveat recorded. Using *palisay* as a label for a
combat shield would be asserting something the source does not say.

### 7.5 The one-citation-deep problem

Four terms — *barote*, *pakil*, *batung-batung*, and *habay-habay* — reach this
document through exactly one route: William Henry Scott's *Barangay* (1994),
reading sixteenth-century Spanish-compiled dictionaries. Not one of them was
verified in any primary text by anyone working on this document, and Scott's
printed pages were not read; the passage behind all four has been read only in
an optical-character-recognition text of his book.

What that passage says, so that the four terms are at least recorded accurately
rather than vaguely, is this. The *barote* was the Visayan equivalent of a
cuirass, quilted or corded body armor, woven of thick-braided abaca or bark
cords, tight enough in good examples to be waterproof and knotted so intricately
that cuts did not spread. *Habay-habay* was **not** armor: it was a piece of
material similar to burlap, worn next to the body underneath the *barote*, and
this document previously listed it as a "shield or armor term", which it is not.
*Pakil* and *batung-batung* were breastplates or backplates made of bamboo,
bark, hardwood such as ebony, or, in Mindanao, carabao horn or elephant hide
from Jolo.

Two of those descriptions overlap materials this document now has attested in a
sixteenth-century relation. Alvarado in 1548 names bamboo-and-hardwood cuirasses
and wood-and-horn corselets. That does not verify Scott's Visayan **terms**,
which is what section 7's verdicts are about; it does mean the objects he is
describing are not exotic to the period record.

This remains a structural weakness and it should be named as one rather than
softened. The chain for each of these terms is: a sixteenth-century Spanish
missionary compiles a dictionary of a language he is learning; a modern scholar
reads that dictionary and reports a term; this document reads a scan of the
modern scholar. Three links, two of them unread in their published form, and the
last link is where a term would become a player-facing label if this rule were
not enforced.

The same route now also carries the octopus-skin plumed helmet of section 5.3,
which this document previously credited to a sixteenth-century relation, and the
rattan binding, resinous pitch, and 50-by-150-centimetre shield figure of
section 6.4, which it previously credited to Junker. Everything in this
paragraph is Scott's, and none of it has been read in the printed book.

Scott is a serious scholar and these terms are probably real. That is not the
same as having checked. `WEAPON_CLASH_1500s.md` records at its line 718 that
*Barangay* "was never read at page level" and that its warfare chapter is the
single highest-value unread source for this whole subject. The same gap governs
all four of these terms.

**Verdict for all four: OPEN.** None may be used as a player-facing label.
Recording them in inspector metadata as terms under review is acceptable and is
what the appearance research already does for *barote*.

### 7.6 *Carasa* is regraded from EXCLUDE to OPEN

This document previously graded *carasa* **EXCLUDE**, with the reason "it is a
1609 term". That verdict was inconsistent with the verdict four rows below it in
the same table, and the inconsistency ran the wrong way.

*Palisay* comes from San Buenaventura's Tagalog *Vocabulario* of **1613** and is
graded **OPEN**, on the reasoning in section 7.4 that a 1613 date "sits barely
outside the depicted period, and is nowhere near the hundred-year limit that
excluded the panabas". Morga is **1609** — four years earlier than San
Buenaventura. Applying the same rule to the earlier of two dates and getting the
harsher answer is simply an error.

The hundred-year bar in `CLAUDE.md` section 7 excludes a name whose earliest
attestation postdates the depicted period by more than a century. Morga's term
postdates the window by about nine years. The bar does not reach it, and no
other rule in the policy excludes it.

**Verdict: OPEN**, on the same footing as *palisay*. What keeps it open rather
than moving it to USE is not the date. It is that the word reaches this document
from a single seventeenth-century source, that the object Morga describes —
full-length, with inside armholes — is not the object Artieda describes in 1573,
and that Blair and Robertson's bracketed gloss identifies the word as *kalasag*,
which is already a separate entry in this table with a 1521 attestation. Whether
*carasas* is a distinct shield name or the editors are right that it is
*kalasag* is unresolved, and a term cannot be a player-facing label while it may
be a spelling of another term already in use.

One justification is explicitly **not** offered here: that *carasa* is a
Hispanised spelling of a Spanish or Latin word rather than a local one. Nothing
in Morga's text establishes that. He is transcribing a name he was given, in the
same sentence and the same manner in which he transcribes *bararao* for the
dagger, and inventing a Spanish etymology to justify a verdict would be exactly
the kind of convenient reasoning section 12 forbids.

## 8. What the evidence does not support

This section is deliberately blunt, because every item on it is something that
is widely repeated, sounds plausible, and has no sixteenth-century source behind
it.

**No sixteenth-century source located for this document says that any Philippine
shield was hide-covered.** Not one.

**No sixteenth-century source says that any Philippine shield was
rattan-bound.** The finding stands, but the attribution behind it was wrong and
has been corrected. This document previously traced the rattan-strengthened
construction to Junker's *Raiding, Trading, and Feasting* (1999). It is Scott,
*Barangay* (1994), page 151: "It was strengthened and decorated with rattan
binding coated with resinous pitch." Scott is a modern work too, so the negative
finding is unaffected — no sixteenth-century text reaching this document
describes rattan binding on a shield — but the modern work in question is the
one this document elsewhere treats as its highest-value unread source, which
makes the claim worth more attention rather than less. Rattan **armor** is
attested by Artieda in 1573; rattan **shield binding** is not attested in the
sixteenth century at all. These are two different claims and they get conflated
constantly.

**No sixteenth-century source says that any Philippine shield was
resin-coated.** Same sentence, same author, same correction: the resinous pitch
is Scott's, not Junker's, and it has no sixteenth-century source reaching this
document.

**No sixteenth-century source gives a size for a shield except Artieda's
width.** The "about 50 by 150 centimeters" figure is the third claim in that
same sentence of Scott's, and it was likewise attributed to Junker here until
now. Scott offers no period source for it. The correction changes who said it;
it does not turn it into evidence.

**No sixteenth-century source describes prongs on a Philippine shield.** The
pronged Cordilleran forms come from Krieger's 1926 United States National Museum
Bulletin 137, which is a twentieth-century publication describing objects
collected in the twentieth century. Whether prongs served to trap a weapon in
close work is a question about those objects, not about 1521.

**No sixteenth-century source describes carving or painting on a Philippine
shield.** The Boxer Codex around 1590 shows color, and repository policy already
restricts the Codex to guiding color and silhouette. That is as far as it goes.
Specific carved motifs, painted devices, or heraldic markings are not attested.

**No sixteenth-century source describes a shield formation.** No shield wall, no
locked line, no interlocking front. What Pigafetta describes at Mactan in 1521
is the opposite: individual mobility, leaping about, and three divisions
converging from flanks and front. Anything more organized than that is invention.

**No sixteenth-century source describes a parry technique, a shield-strike, or
any named defensive action.** This is the same finding that
`WEAPON_CLASH_1500s.md` reaches from the other direction and records in its
comparative table at lines 125-128, where the Philippine primary record for
1521-1582 is marked "Unattested" for deliberate blade-on-blade contact and
credited only with evasion plus shield cover. Every technique description that
circulates comes from later ethnography, from Filipino martial arts literature,
or from European material, and none of it is sixteenth-century Philippine.

**No sixteenth-century source states how common armor was, anywhere.** This
entry used to read "no source supports armor as standard equipment anywhere",
with Artieda's "some" as its first piece of evidence. That was an over-reading
now that the passage has been read on the page: Artieda's "some" governs only
the black-wood corselet, and his cotton-lined blanket armor and rattan armor
carry no qualifier at all. What survives is weaker and still real. The 1565 Cebu
account describes armor being put on for a specific engagement rather than worn
as dress; the two most detailed ethnographies of the period describe no
Philippine body armor; and no source in the record says that armor was general.
Equally, no source says it was rare. Depicting armor as the normal state of a
Philippine fighter in the 1500s asserts something no source supports; depicting
it as vanishingly rare asserts something no source supports either.

**No sixteenth-century source describes armor proof against everything but the
arquebus in terms that identify which piece of armor was meant.** The one
arquebus-resistance claim in the record is the closing clause of a three-item
list — hide corselets, iron greaves, and shell-set helmets — in one letter, from
one province, in 1574. The plain reading covers all three; a narrower reading
covering only the helmets is possible but strained. This document previously
asserted the narrow reading as fact and warned readers off the plain one. That
warning is withdrawn. See sections 4.4 and 5.2.

**No sixteenth-century source describes metal plate armor on a Philippine
fighter, except in one contested passage.** The exception is Lavezaris on Los
Camarines in 1574, which Scott attributes to confusion with Japanese equipment.
See section 4.6. Everything else people picture as Philippine metal armor is
eighteenth century or later.

**No dated sixteenth-century Philippine armor, helmet, or shield object is known
to this document.** Not one surviving physical example with a provenance chain
reaching the 1500s. Every object in every collection consulted is later, and
most of the dating is stylistic judgement rather than documentation. See section
4.7.

**No measurement of any kind exists for a sixteenth-century Philippine shield
except Artieda's width.** One number, "little more than half a *vara*", from one
relation, in 1573, for one unspecified region, now read on the page. The
conversion of that to roughly 42 centimeters is this document's own arithmetic
over Blair and Robertson's table of Spanish measures, not a figure any source
prints. Every other number in circulation is either a modern secondary estimate,
a translator's bracketed insertion, or an outright fabrication.

## 9. The two silent ethnographies, and a third silence

Three sources are worth reading for what they do not say. Silence is weak
evidence in general, but silence from an observer who described everything else
in the same category, in detail, is a different matter.

### 9.1 Loarca, 1582

Miguel de Loarca's *Relacion de las Yslas Filipinas* of 1582 is one of the two
most substantial ethnographic accounts of the sixteenth-century Philippines. It
contains **no shield description at all** *(unverified)*.

That is remarkable given that shields are the single best-attested item of
Philippine protective equipment in every other source. Loarca's silence does not
mean shields were absent — Pigafetta, Artieda, Lavezaris, and the Boxer Codex
between them settle that question. It means that an observer writing a detailed
account of Visayan life in 1582 did not think shields worth describing, which is
a useful corrective to the assumption that every sixteenth-century writer was
cataloguing military equipment.

### 9.2 Morga, 1609

Antonio de Morga's *Sucesos de las Islas Filipinas* is the other major
ethnography, and its silence is much more pointed. Morga describes shields in
genuine detail — the light wood, the armholes fastened on the inside, the
top-to-toe coverage, the name *carasas* — and he describes the *bararao* dagger
in detail as well, down to its width in fingers, the material of its hilt, and
the shape of its pommel (verified in the primary text). He then mentions **no
body armor and no helmet for Filipinos anywhere**.

The silence is sharper than a word count of "armor" alone would suggest, because
the word does occur in Morga three times and **every one of the three refers to
Japanese equipment**: armor among the gifts sent from Japan to the governor,
armor in a Spanish correspondent's aside about an armory, and armor arriving at
Manila as cargo. The last of these is worth quoting, because it bears directly
on the contested Camarines report in section 4.6. The annual ships from Nagasaki
brought, Morga writes, "many suits of armor, spears, catans, and other weapons,
all finely wrought" (verified in the primary text).

Two things follow. Morga knew perfectly well what armor was and had a word for
it; he simply never applied it to a Filipino. And Japanese harness was a normal
sight in Manila, landed by the shipload as merchandise, which is period support
for the mechanism Scott proposes for the 1574 Camarines report. This document
previously lacked that support and recorded Scott's argument as an assertion
about what Japanese armor "looks like to a Spaniard".

This is the strongest single piece of negative evidence in this document. Morga
was in a position to know, he was writing at length about exactly this category
of equipment, he was willing to spend words on construction detail when he had
it, and he wrote nothing about armor. An observer who tells you precisely how
the shield's arm-straps are fastened and says nothing whatsoever about a
corselet is telling you something about how often he saw a corselet.

### 9.3 Two detailed ethnographies, both silent on universal armor

Put together, Loarca in 1582 and Morga in 1609 are the two most detailed
accounts of Philippine material life to survive from the period around this
document's window, and neither describes body armor as something Filipinos wore.
The armor evidence in the record comes almost entirely from military dispatches
written by men reporting to a Crown about an enemy, not from the accounts written
to describe how people lived.

That pattern is worth taking seriously. It is consistent with body armor having
been minority equipment, brought out for particular engagements, concentrated in
particular regions and among particular people, and not part of what an observer
describing everyday life would think to mention. It is not consistent with armor
having been ordinary kit.

**Depicting armor as universal contradicts this evidence.** Depicting it as rare
does not.

### 9.4 A third silence, from outside the window

Blair and Robertson volume XXVIII contains an eyewitness account of the Corcuera
campaign against Jolo and the neighbouring Moro positions in 1637 and 1638. The
silence in it is broader than this document previously claimed, and it has now
been counted rather than reported. Across the whole volume the words *corselet*,
*cuirass*, *helmet*, *armor*, *shield*, and *buckler* occur **zero times each**.
The word *mail* occurs six times and not once in the sense of armor: the hits
are place names such as Himamailan, a line item for the archbishop's post, and
modern Project Gutenberg boilerplate. The only Moro equipment named in the
volume at all is offensive — *kris* three times and *campilan* once (verified by
direct count in the primary text).

This is outside the depicted period by a century and is therefore not evidence
about the 1500s. It is included for one narrow reason: it is a detailed
eyewitness military account, from the region and tradition that the famous
brass-and-horn armor is associated with, written more than a hundred years after
this document's window closes, and it still describes no armor. Anyone inclined
to push the *kurab-a-kulang* tradition back into the sixteenth century has to
get past a seventeenth-century siege account that does not mention it either.

## 10. Corrections to the Category F armor layer

`docs/research/improve-visuals/warrior-appearance-historical-research.md`
defines the armor layer that currently drives the pawn renderer, in its Category
F at lines 462 to 566. That document is not superseded and its rendering
guidance is not disturbed. But seven of its evidence statements are either wrong
or narrower than the record supports, and this document does not silently
contradict them. Each correction below names the lines it applies to and states
what the evidence actually shows. Two of the seven — corrections 6 and 7 — are
places where an earlier version of **this** document was the source of the
error, and they say so.

None of these corrections is a change request. They are findings. Whether and
how the armor layer changes is a separate decision that belongs to a design
document, not to this one.

### Correction 1 — lines 465-467: armor evidence does not begin in 1565

The Category F preamble states: "The 1521 Mactan account describes effectively
unarmored fighters. Armor evidence enters with the Legazpi-era relations of
1565-1576 and is regionally uneven."

The first sentence is correct. The second is wrong.

Pigafetta's Visayan vocabulary of 1521 glosses *baluti* as quilted garments used
for fighting, and the same account describes small cuirasses of buffalo horn at
Caghaian in the same year. Both were verified in the primary text for this
document. Armor evidence therefore enters the record in **1521**, forty-four
years earlier than the preamble states, from the same source and the same voyage
that supplies the Mactan account.

The correct formulation is that the 1521 record is **split**: it attests a
Visayan word for quilted fighting garments and a horn cuirass in the
Sulu approaches, while describing no armor on anyone in the one battle it
narrates in detail. Both halves are Documented. The preamble currently records
only one half and then attributes the other half to the wrong decade.

The "regionally uneven" clause is correct and should be kept. If anything, 1521
makes it more so.

### Correction 2 — lines 482-487: F2's player-facing label is weaker than the evidence allows

F2's proposed label is "Corded Fiber Armor (plain English)", with the
explanation that Scott records the Visayan term *barote* but that the
attestation date could not be independently confirmed, so the term is confined
to inspector metadata pending review.

That reasoning is sound and the caution about *barote* is correct — this
document reaches the same verdict at section 7.5 and marks *barote* **OPEN**.

But the caution has been applied to the wrong term. There is a Visayan word for
quilted fighting armor with a **1521 eyewitness attestation**, glossed by the
observer himself, verified in the primary text for this document: *baluti*. It
does not depend on Scott, on a dictionary, or on any modern reading. It clears
the pair-form bar in `CLAUDE.md` section 7 outright, and it clears it more
cleanly than *Wasay* or *Itak* clear it in the weapons roster.

The finding is that a Visayan quilted-armor layer could carry the pair-form
label **Baluti — Quilted Armor** at evidence tier **Documented**, rather than
being restricted to a plain English descriptor because a different and
weaker term could not be confirmed. Whether the F2 slot is the right home for
that label is a design question; the evidence question is settled.

Note that *baluti* and *barote* are not necessarily the same garment. *Baluti*
is what Pigafetta recorded as quilted fighting clothing; *barote* is what Scott
reports for corded or quilted body armor. Treating them as one item would be
an assumption, and the honest position is that *baluti* is attested and *barote*
is under review.

### Correction 3 — lines 491-493: F2's date range starts too late

F2 gives its date range as "Legazpi-era descriptions, 1565-1576, and the early
Visayan lexical record", with scope "Visayas".

The scope is right. The date range should begin at **1521**, on Pigafetta's
Visayan vocabulary, which is both the earliest attestation and the only one in
the whole quilted-armor chain that was verified in a primary text. The phrase
"early Visayan lexical record" is doing the work of a specific 1521 citation and
should be replaced by one.

### Correction 4 — lines 536-548: F5 rests on one report when two more exist

F5, the helmet set with fish bones and shells, cites only the Legazpi-era crown
report family for Luzon in 1565-1576, and correctly calls the item exceptional.

Three further head-protection lines are absent from Category F entirely, and
they are not of equal strength:

- **head armor of dogfish skin**, in the southern islands, from Alvarado's
  relation of the Villalobos expedition, written at Lisbon on 7 August 1548 and
  printed in Blair and Robertson volume II (verified in the primary text). This
  is the strongest of the three and the only one read on the page;
- a wooden helmet faced with shark skin, in the Visayas, from
  sixteenth-century Visayan dictionaries via Scott (1994), now corroborated as
  to material by the dogfish-skin line above, a dogfish being a small shark;
  and
- a wooden helmet covered with octopus skin and plumed, at Sarangani, which
  this document previously credited to "the Villalobos relation of 1543" and
  which is in fact **Scott's**, in his Sarangani Island section. The
  Villalobos-expedition relation says dogfish skin and mentions neither octopus
  nor plumes. See section 5.3.

The first two broaden the head-protection evidence genuinely: organic-faced head
protection is attested in the sixteenth century in at least one region other
than Luzon, and the 1548 line predates Legazpi by seventeen years. The third
does not broaden anything, because it describes the same expedition at the same
island as the first and reaches this document through the same modern scholar as
the second. None of the three upgrades the shell-set Camarines helmet, which
remains a single letter. What they change is that F5's single citation is no
longer the whole of the head-protection record.

Note also that Category F places the shell-set helmet in "Luzon". The letter
places it in **Camarines**, a province the writers introduce as newly explored
and pacified in the eastern part of Luzon. The narrower label is the accurate
one.

F5's caution that the item is exceptional and should appear on at most one or
two presets is not disturbed. Rarity is still the right reading. The correction
concerns the breadth of the evidence, not the frequency of the depiction.

### Correction 5 — lines 556-558: the metal-armor exclusion is not a clean negative

The exclusion list states: "Metal armor in the 1500s sources belongs to Bruneian
and Spanish opponents, not to the modeled lowland warriors."

The conclusion is defensible. The statement, as written, is not clean, for two
reasons.

First, there is one contested sixteenth-century report of iron body armor on
Philippine fighters: Lavezaris on Los Camarines in 1574, describing iron
corselets, greaves, wristlets, gauntlets, and helmets. Scott attributes it to
confusion with Japanese equipment. Neither position was verified for this
document. The honest status is **contested**, recorded in section 4.6 and in the
open questions, not absent from the record. An exclusion resting on a flat "the
sources say no" is resting on something the sources do not quite say.

Second, the 1521 buffalo-horn cuirass at Caghaian complicates the "Bruneian
opponents" framing in a way worth getting right. Those people were not
opponents; they were the inhabitants of an island Pigafetta visited, in the
waters between Palawan and Borneo, and he identifies them as Moros banished from
Brunei. So the transmission line the sentence gestures at is real and is
confirmed — Borneo to the Sulu approaches, already in place by 1521 — but the
armor in question is **horn, not metal**, and it was worn by people living in
the archipelago rather than by an external enemy.

Alvarado's relation of 1548 removes what is left of the Bruneian framing for
horn. He describes corselets of wood and buffalo horn among the general
defensive arms of the southern islands, with no Bornean attribution of any kind.
Horn body armor in the archipelago is therefore attested twice in the first half
of the sixteenth century, by two voyages, in two places, and only one of the two
carries a transmission story.

The practical upshot for the exclusion is unchanged: brass-and-horn plate armor
of the *kurab-a-kulang* type stays excluded from sixteenth-century presets under
the hundred-year rule, exactly as the panabas stayed excluded from the armory,
and nothing above touches that. What changes is the supporting sentence, which
should say that no uncontested sixteenth-century report places metal body armor
on Philippine fighters, rather than that the sources place metal armor only on
outsiders.

### Correction 6 — lines 563-565: the iron greaves note is right, and should say why

The exclusion list records iron greaves as "mentioned once in the same
crown-report family as F3/F5; too thin and too anomalous to depict without
further evidence."

The verdict stands but the stated reason is wrong, and this document had the
same error until the volumes were read. Iron leg armor is **not** mentioned
once. It is mentioned by two officials writing to Felipe II from Manila on the
same day, 17 July 1574: Cauchela and Aldave give "iron greaves", and Lavezaris
gives "iron corselets, greaves, wristlets, gauntlets, and helmets". Both were
verified in the primary text.

The verdict survives that correction because the second witness is not an
independent one. Both letters concern the same Salcedo campaign into the same
province in the same fortnight, written by officials of the same administration
out of the same body of news. Two reports from one information environment are
not two lines of evidence, and if the identification of the metal was wrong in
one there is every reason it was wrong in the other. Scott's argument for that —
that Salcedo had encountered Japanese equipment in a naval action on the Ilocos
coast the year before — bears on both letters at once, not on either alone.

The accurate statement of the reason is therefore: iron leg armor in the
sixteenth-century record rests on one campaign, reported twice on one day, in
one province, and is disputed by the leading modern scholar of the period. That
is still too thin and too anomalous to depict, and it is thin in a more
interesting way than "mentioned once".

One further correction to the same exclusion note: Category F ties the greaves
to "the same crown-report family as F3/F5" and treats that as one document.
Cauchela and Aldave's letter does supply both F5's shell-set helmet and the hide
corselet alongside the greaves, so the observation is right about that letter.
It is wrong that the letter is the only one.

### Correction 7 — the arquebus-resistance claim is ambiguous in the source

This entry previously told the reader that Category F "gets it right" by
attaching the "said to resist anything but the arquebus" claim to the **helmet**
at lines 539-540 while making no such claim for F3's hide corselet at lines
510-512, and it warned that any future edit letting the claim drift onto torso
armor "manufactures a piece of equipment that no source describes". That warning
was misplaced, and this document was the party doing the inferring.

The sentence in the letter of 17 July 1574 reads: "for they have corselets of
buffalo hide, iron greaves, and helmets set with fishbones and stout shells,
which no weapon except the arquebus can damage." The relative clause follows a
list of three items with nothing separating the last from the first two. On the
plain reading it covers the whole list. The reading that restricts it to the
helmets is available — a relative clause often attaches to the nearest noun
phrase — but it is the strained one, not the safe one.

What this means for Category F is narrow and it is not a change request. F5's
attachment of the claim to the helmet is a **choice between two readings**, not
a transcription of the source, and it should be recorded as such wherever the
evidence tier for F5 is shown. Extending the claim to F3's hide corselet would
likewise be a choice between two readings rather than a fabrication. Neither
reading may be presented as what the source says. Sections 4.4 and 5.2 record
the reasoning and this document adopts neither.

### What Category F gets right and should keep

For balance, four things in Category F are well judged and this document
supports them without qualification.

- **F1, "No armor", as the documented default.** This is the correct reading of
  the whole record, and it is stronger than the appearance research claims for
  it, given the silences of Loarca in 1582 and Morga in 1609 described in
  section 9.
- **The repeated "must not generalize" clauses on F2, F3, F4, and F5.** Every
  one of them is right, and armor rarity is the single most important thing for
  the armor layer to get right.
- **The *barote* caution at lines 482-487.** Confining a one-citation-deep term
  to inspector metadata pending review is exactly the correct handling, and this
  document reaches the same verdict independently.
- **The *kurab-a-kulang* exclusion under the hundred-year rule at lines
  552-556.** The parallel drawn with the panabas is apt and the margin is
  comparable.

## 11. Later comparative material, and how far it may be used

No dated sixteenth-century Philippine armor, helmet, or shield object is known
to this document. Everything physical is later. That does not make the later
material useless, but it does mean the rules for using it need to be explicit.

### 11.1 What later objects may be used for

Later objects and later ethnography are legitimate references for **materials,
proportions, construction logic, carving, and silhouette** — the same standing
this repository already grants them in `HISTORICAL_1500s_WEAPONS.md`, which
records that later museum examples remain useful for exactly those things and
should be marked as later comparative references.

They may **not** be used to establish that an item existed in the sixteenth
century, to date a form, to supply a measurement for a sixteenth-century object,
or to attach a name to a sixteenth-century item.

### 11.2 The specific later material reaching this document

All of the following is *unverified* and reached this document through research
summaries rather than through catalogues or publications read directly.

| Material | Date | What it is | Distance from the depicted period |
| --- | --- | --- | --- |
| Mail-and-plate *kurab-a-kulang* armor described in published work | 1926 and 1934 | Twentieth-century publications describing Moro armor | Roughly 350 to 400 years |
| A British Museum accession reported under the number As,9867.23 | Acquired 1876 | The one object in this group with a documentary chain of any kind | Roughly 300 to 375 years, and see 11.3 |
| Krieger, *United States National Museum Bulletin 137* | 1926 | Pronged Cordilleran shield forms | Roughly 350 to 400 years |
| Junker, *Raiding, Trading, and Feasting* | 1999 | Modern scholarship on Philippine chiefdoms. This document previously credited it with the rattan-bound, resin-coated, 50-by-150-centimetre shield description; that description is Scott's, not Junker's, and no claim in this document now rests on Junker | Modern |
| National Museum of the Philippines ethnology holdings | Various, mostly later | Cultural context for shields, blades, and armor traditions | Various, mostly later |

### 11.3 An accession date is not a manufacture date

The 1876 British Museum accession deserves separate treatment because it is
routinely cited as though it dated an object, and it does not.

An accession record says when a museum took an item into its collection. It says
nothing about when the item was made, and for ethnographic material acquired in
the nineteenth century it usually says nothing reliable about where the item was
made either. An object accessioned in 1876 might have been made in 1875, or in
1800, or considerably earlier. The accession is a **floor** on the object's age
and nothing more.

That floor is still the strongest documentary anchor in the entire surviving
Philippine armor object record reaching this document, which is the point worth
absorbing. Every other date attached to a surviving piece of Philippine armor in
this material is **stylistic curatorial judgement**: a specialist looking at
construction, decoration, and materials and estimating a period. That is
skilled work and it is not evidence of the same kind as a documented chain of
ownership. It is an expert opinion, it can be wrong, and it is frequently
reported onward as though it were a fact established by provenance.

None of it reaches the sixteenth century in any case. The gap between the
depicted period and the best-documented surviving object is roughly three
centuries, which is the same order of gap that excluded the panabas from the
weapons roster under `CLAUDE.md` section 7.

## 12. Open questions

Each of the following is unresolved. None of them may be quietly closed by
picking the more convenient answer, and none of them may be treated as settled
in any downstream document, design, or label.

1. **Is *tamin* or *taming* attested in the sixteenth century, and which of the
   two is the word?** Scott records it from sixteenth-century Visayan
   dictionaries and judges the item copied from the Moluccans or the Spaniards.
   No pre-1650 Philippine attestation was found in any primary text read
   directly, and neither form appears in Pigafetta's 1521 Visayan vocabulary.
   The text of Scott consulted here gives *tamin*, without the final *g*, but
   that text is an optical-character-recognition transcription that mangles
   neighbouring words, so the spelling finding is a finding about a scan.
   **To close it:** read the printed Scott page and, if he cites one, the
   dictionary entry behind it. **Until closed:** verdict OPEN, both spellings
   recorded, no player-facing use in any form. See section 7.3.

2. **How wide is the Boxer Codex Cagayan shield?** The circulating figure
   depends on a modern translator's bracketed insertion, "[of a fathom]", and
   resolves to either roughly 127 centimeters or roughly 64 centimeters
   depending on how the insertion is read. **To close it:** consult the
   manuscript or a critical edition that shows the insertion. **Until closed:**
   quote no width for this shield. See section 6.4.

3. **Did anyone in Los Camarines wear iron corselets in 1574?** Lavezaris says
   yes, in his letter to Felipe II of 17 July 1574, Blair and Robertson volume
   III page 273; that page has now been read. Cauchela and Aldave, writing to
   the same king from the same city on the same day, independently name iron
   greaves in the same province; that page has now been read too. Scott says
   the whole report is a confusion with Japanese equipment that Salcedo had met
   in a naval action on the Ilocos coast the year before; his page has been
   read only in an optical-character-recognition text. Morga establishes that
   Japanese harness arrived at Manila by the shipload as trade goods, which
   makes Scott's mechanism materially available without confirming it. **To
   close it:** read Scott in the printed book, and establish whether the two
   letters of 17 July 1574 had a common informant. **Until closed:** contested,
   neither Documented nor excluded. See section 4.6.

4. **The one-citation-deep problem.** *Barote*, *pakil*, *batung-batung*, and
   *habay-habay* all reach this document solely through Scott's reading of
   sixteenth-century Spanish-compiled dictionaries. His defensive-arms passage
   has now been read in an optical-character-recognition transcription, which
   is how *habay-habay* was found to be an undergarment rather than armor and
   how *pakil* and *batung-batung* were found to be breastplates and
   backplates, but a scan is not the book and the dictionary entries behind him
   remain unread. `WEAPON_CLASH_1500s.md` records at its line 718 that
   *Barangay* was never read at page level and that its warfare chapter is the
   single highest-value unread source for this whole subject. That gap governs
   all four terms and, as section 7.5 now records, the octopus-skin helmet, the
   enmeshing *kalasag*, the rattan binding, the resinous pitch, and the
   50-by-150-centimetre shield figure as well. **To close it:** somebody reads
   the book. **Until closed:** all four verdicts stay OPEN. See section 7.5.

5. **The enmeshing kalasag — the source is now traced, and the question that
   remains is a different one.** The claim that Philippine shields were built
   from light fibrous wood chosen so that a penetrating spear or dagger became
   enmeshed and could not be withdrawn is recorded and flagged in
   `WEAPON_CLASH_1500s.md` at its section 2.1, and its line 720 states that the
   formulation "needs to be traced to a page or dropped". **It has been traced.**
   It is Scott, *Barangay* (1994), page 151: "The shield, *kalasag*, was made of
   a light, corky wood which was very fibrous so as to enmesh any spear or
   dagger which penetrated it, and it was generally considered sword-proof."
   That closes the tracing question and opens a plainer one in its place. What
   has been found is a modern scholar's assertion, written in 1994, with no
   sixteenth-century source attached to it in the sentence itself. It is not a
   period statement, and the claim therefore remains **unsupported by any
   sixteenth-century source reaching this document**. Locating the author does
   not convert an assertion into evidence. This document does not re-litigate
   the substance and adds only one observation from the primary text: at Mactan
   in 1521 Pigafetta describes shields of thin wood that crossbow bolts and
   arquebus balls passed straight through, along with the arm behind. That is a
   description of a shield that is penetrated, not of one that traps. It neither
   proves nor disproves the enmeshing claim, which concerns hand weapons rather
   than projectiles, but it is the only sixteenth-century sentence about
   Philippine shield material behaviour that exists, and it does not support the
   story. **To close what is left:** find whether Scott cites a period source
   for the sentence at his page 151. **Until closed:** the substance of the
   question still belongs to `WEAPON_CLASH_1500s.md`, whose line 720 records it
   and which this document does not edit.

6. **What was the Sarangani helmet covered with, and did anyone see two
   different helmets?** This question has changed shape completely. The
   Villalobos-expedition relation has now been read: Alvarado, writing from
   Lisbon on 7 August 1548, says head armor there was made of dogfish skin. The
   octopus-skin plumed helmet that this document previously attributed to that
   relation is not in it; it is Scott's, in his Sarangani Island section,
   describing the same island and the same expedition from an anonymous relation
   he cites and this document has not seen. So two accounts of one encounter
   disagree about the material, unless they are describing two different items.
   **To close it:** identify and read the anonymous relation behind Scott's
   Sarangani passage, and establish whether it and Alvarado are independent.
   **Until closed:** dogfish-skin head armor is Documented; the octopus-skin
   plumed helmet is Provisional reconstruction and is counted among the
   Scott-only claims. See sections 5.3 and 7.5.

   The Visayan shark-skin helmet travels with this question and has partly
   moved. Scott's statement that shark skin was used for helmets is no longer
   the only line of support for the material, because a dogfish is a small
   shark and Alvarado attests it in 1548. What remains unverified is the
   specifically Visayan, specifically wooden form Scott describes and the
   dictionary entries behind it. **To close that part:** read the printed Scott
   page and the dictionary entries he is reading.

7. **What did Sande actually write in 1577? — RESOLVED.** His relation was read
   in the primary text. It does not describe Chinese-supplied equipment in local
   hands. On native arms it says only: "The Indians have thousands of lances,
   daggers, shields, and other pieces of armor, with which they fight very
   well." That attests body armor as widespread and names no type, material, or
   region, and it sits inside an argument to the Crown for more arquebuses, so
   the "thousands" is rhetoric rather than a count. See section 4.4.

8. **Is *palisay* a fighting shield or a dance shield?** San Buenaventura's 1613
   gloss says *usase en danzas*. That is the only attestation reaching this
   document, and it describes a ceremonial context. Whether the martial use
   preceded it, followed it, or coexisted with it is unknown. See section 7.4.

9. **Are *baluti* and *barote* the same garment?** Pigafetta's 1521 gloss
   describes quilted fighting clothing; Scott's *barote* describes corded or
   quilted body armor of braided abaca or bark cord. They may be one item under
   two names, two related items, or two unrelated ones. Nothing available
   settles it, and merging them would be an assumption. See section 10,
   correction 2.

10. **Is *carasas* a shield name in its own right, or Morga's spelling of
    *kalasag*?** Blair and Robertson gloss the word in brackets as *kalasag*,
    which would make Morga's 1609 term and Pigafetta's 1521 *calassan* the same
    word reaching the record twice. The editors give no argument for the
    identification, exactly as they give none for matching *calassan* to
    *calasag* in their footnote 376. **To close it:** find whether any
    sixteenth- or seventeenth-century dictionary carries a form close to
    *carasa*. **Until closed:** the term stays OPEN, and no player-facing label
    may use it. See section 7.6.

11. **How common was body armor?** No source in the record states a proportion.
    Artieda's "some" applies to one item of the three he names, the 1565 Cebu
    account describes armor being taken up for one engagement, Alvarado in 1548
    and the two Camarines letters of 1574 describe armor without qualifying how
    many wore it, and Loarca and Morga describe none at all. Both "armor was
    normal" and "armor was rare" go beyond what any of them says. **To close
    it:** it may not be closeable; a fuller reading of the Legazpi-era
    correspondence is the only route. **Until closed:** prevalence is unknown
    and must be stated as unknown. See sections 4.5 and 9.3.

## 13. Sources

### 13.1 Sixteenth-century and near-primary material

1. [*The Philippine Islands, 1493-1803*, Volume XXXIII](https://www.gutenberg.org/ebooks/42884)
   - Blair and Robertson's translation of Antonio Pigafetta's account of the
     Magellan voyage, with the editors' notes and the Visayan vocabulary
     concordance. **This is the volume read directly for this document.**
     Everything marked *(verified in the primary text)* above comes from it:
     the Visayan vocabulary entries for *calassan* and *baluti* in Pigafetta's
     original Italian and in translation; the Caghaian passage describing
     bucklers and small cuirasses of buffalo horn, and its footnote 393 noting
     that MS. 5,650 omits the cuirasses; the Mactan battle narrative, including
     the shields of thin wood pierced by shot and the description of fighters
     covering themselves with shields while leaping about; the abridged Mactan
     account from MS. 5,650; the eastern Samar equipment list including large
     shields; the Limasawa and Butuan honor guard with bucklers; and footnote
     376, the editors' concordance of Pigafetta's Visayan words against
     Encarnación (1885) and Sanchez de la Rosa (1895).
2. [Pigafetta's account in English translation, Wikisource](https://en.wikisource.org/wiki/The_First_Voyage_Round_the_World/Pigafetta%27s_Account_of_Magellan%27s_Voyage)
   - An alternative translation of the same account, already cited by
     `HISTORICAL_1500s_WEAPONS.md`. Convenient for cross-checking wording, but
     the Blair and Robertson volume above carries the editorial apparatus this
     document relies on.
3. [Library of Congress: Pigafetta's Journal of Magellan's Voyage](https://www.loc.gov/resource/gdcwdl.wdl_03082/?st=grid)
   - Manuscript provenance and 1521 eyewitness context.
4. [*The Philippine Islands, 1493-1803*, Volume II](https://www.gutenberg.org/ebooks/13280)
   - **Read directly for this document.** Two things in it are cited above. The
     first is the Cebu skirmish account of 1565, with its equipment list —
     "long sharp iron lances, throwing-sticks, shields, small daggers, wooden
     corselets, corded breastplates, a few bows and arrows, and culverins" —
     and its description of the natives putting on their wooden corselets and
     rope armor before engaging. The second is the relation of the Villalobos
     expedition by **García Descalante Alvarado**, an officer of Villalobos,
     written to the viceroy of New Spain and dated at Lisbon, 7 August 1548.
     Alvarado's passage on arms, quoted in sections 4.4 and 5.3, gives the
     sleeved full-length cotton corselet, the wood-and-buffalo-horn corselet,
     the bamboo-and-hardwood cuirasses that cover the wearer entirely, and head
     armor of dogfish skin. It is the earliest and the richest description of
     Philippine defensive equipment in this document, and it was missing from
     earlier versions of it altogether. Note that the volume's own editorial
     preface dates the relation to 1 August 1548 while its synopsis of the
     document dates it to 7 August; the discrepancy is Blair and Robertson's,
     not this document's, and 7 August is used here.
5. [*The Philippine Islands, 1493-1803*, Volume III](https://www.gutenberg.org/ebooks/13616)
   - Documents of 1569-1576. **Read directly for this document.** The source for
     Diego de Artieda's *Relation of the Western Islands Called Filipinas*
     (1573), with its armor of cotton-lined blankets and of rattan, its
     black-wood corselets worn by "some", and its shields "breast-high, and
     little more than half a *vara* wide"; for Guido de Lavezaris's *Affairs in
     the Philippines* of 29 June 1573, with buffalo-hide and knotted-rope
     corselets on Luzon *(that document alone still unverified)*; for
     Lavezaris's two letters to Felipe II of 17 and 30 July 1574, printed
     together from page 272, of which the letter of **17 July** contains at page
     273 the contested Los Camarines report of "much good armor — as iron
     corselets, greaves, wristlets, gauntlets, and helmets — and some arquebuses
     and culverins"; and for the editors' note 67, which prints a table of
     Spanish measures of length giving "2 sesmas = 1 piè (the foot, = 11.128 U.
     S. inches)" and "3 piès = 1 vara". Note 67 does **not** print a
     centimetre figure for the *vara*; the conversion to roughly 84.8
     centimeters used in section 6.4 is this document's own arithmetic over that
     table.
6. [*The Philippine Islands, 1493-1803*, Volume XXXIV](https://www.gutenberg.org/ebooks/47927)
   - **Read directly for this document.** Letter to Felipe II from the royal
     officials Andres Cauchela and Salvador de Aldave, **Manila, 17 July 1574**,
     beginning at **page 295**: reporting on the newly pacified province of
     **Camarines**, "The men are warlike and well armed for Indians; for they
     have corselets of buffalo hide, iron greaves, and helmets set with
     fishbones and stout shells, which no weapon except the arquebus can
     damage." Earlier versions of this document dated this letter to 17 March
     1574, placed it at page 397, described its subject as Luzon in general, and
     stated that no scan of the volume could be located. All four statements
     were wrong; the volume is Project Gutenberg ebook 47927.
7. *The Philippine Islands, 1493-1803*, Volume XXVIII
   - An eyewitness account of the Corcuera campaign against Jolo and the
     neighbouring Moro positions in 1637-1638, cited here only for its silence.
     **Read directly for this document, and counted:** across the whole volume
     the words *corselet*, *cuirass*, *helmet*, *armor*, *shield*, and *buckler*
     occur zero times each, and the six occurrences of *mail* are place names,
     a postal line item, and modern boilerplate. *Kris* occurs three times and
     *campilan* once. Outside the depicted period.
8. Miguel de Loarca, *Relacion de las Yslas Filipinas*, 1582
   - Cited only for its silence: it contains no shield description. *Not read
     directly for this document.*
9. Antonio de Morga, *Sucesos de las Islas Filipinas*, 1609
   - **Read directly for this document.** Shields of light wood with armholes
     fastened on the inside, covering the bearer from top to toe, called
     *carasas* and glossed in brackets by the editors as *kalasag*; the
     *bararao* dagger described in detail; and no body armor or helmet for
     Filipinos anywhere. Every occurrence of the word "armor" in the book refers
     to Japanese equipment, including the annual cargo from Nagasaki of "many
     suits of armor, spears, catans, and other weapons, all finely wrought".
     Outside the depicted period, used as a boundary marker and, for the
     Japanese armor trade, as period context for section 4.6.
10. Pedro de San Buenaventura, *Vocabulario de la lengua tagala*, 1613, page 122
    - Glosses *palisay* as a round buckler, *usase en danzas*. *Not read
      directly for this document.*
11. The anonymous relation behind Scott's Sarangani Island section
    - Scott's description of the Sarangani defenders — body-length shields,
      quilted cotton or wild boar hide body armor, wooden helmets covered with
      octopus skin, plumes on shields and helmets, everything painted in bright
      colors — is cited by him to an anonymous relation of the Villalobos
      occupation. That relation has not been identified or read for this
      document. Earlier versions of this document attributed the octopus-skin
      helmet to "the Villalobos relation of 1543"; the Villalobos-expedition
      relation printed in Blair and Robertson volume II is Alvarado's, it says
      dogfish skin, and it says nothing about octopus or plumes. See section 5.3.

### 13.2 Late-century visual material

12. [Indiana University Lilly Library: Boxer Codex overview](https://blogs.libraries.indiana.edu/lilly/2015/11/02/boxer-codex-on-exhibit-at-new-york-asia-society/)
    - Provenance and limitations of the manuscript, compiled in Manila around
      1590.
13. [Wikimedia Commons: Boxer Codex image category](https://commons.wikimedia.org/wiki/Category:Boxer_Codex)
    - Browsable visual reference set.
14. [Boxer Codex: Cagayan warrior](https://commons.wikimedia.org/wiki/File:Cagayan_Warrior.png)
    - The figure carrying the large shield and wearing the cap compared to a
      *morrión*. The shield-width figure attached to this plate is unresolved
      and must not be quoted; see section 6.4.
15. [Critical study of the Boxer Codex's pictorial sources](https://www.ehumanista.ucsb.edu/sites/secure.lsit.ucsb.edu.span.d7_eh/files/sitefiles/ehumanista/volume40/ehum40.romero.pdf)
    - The reason to treat the illustrations as guidance for silhouette and color
      rather than as documentary photography.

### 13.3 Later comparative collections and object literature

16. [National Museum of the Philippines: Weapons and Shields](https://www.nationalmuseum.gov.ph/our-collections/ethnology/weapons-and-shields/)
    - Cultural context for shields, blades, armor, materials, social status, and
      regional traditions. Almost all cataloged objects are considerably later
      than the sixteenth century.
17. British Museum accession reported under the number As,9867.23, acquired 1876
    - Reported as the one Philippine armor object in this material with a
      documentary chain. An accession date is not a manufacture date; see
      section 11.3. *Not verified against the museum catalogue.*
18. Herbert W. Krieger, *United States National Museum Bulletin 137*, 1926
    - Pronged Cordilleran shield forms, already cited by
      `WEAPON_CLASH_1500s.md`. Twentieth-century objects and a twentieth-century
      publication. *Not read directly for this document.*
19. Published descriptions of mail-and-plate *kurab-a-kulang* armor, 1926 and
    1934
    - Twentieth-century publications describing Moro armor. Roughly three and a
      half to four centuries after the depicted period. *Not read directly for
      this document.*

### 13.4 Modern scholarship

20. William Henry Scott, *Barangay: Sixteenth-Century Philippine Culture and
    Society*, Ateneo de Manila University Press, 1994
    - The single most important source for this subject and still the single
      largest gap in this document. It supplies *barote*, *pakil*,
      *batung-batung*, *habay-habay*, the *tamin* or *taming* discussion, the
      Visayan shark-skin helmet, the octopus-skin plumed helmet at Sarangani,
      the enmeshing and sword-proof *kalasag*, the rattan binding and resinous
      pitch, the 50-by-150-centimetre shield figure, and the argument that the
      Camarines iron corselets are a confusion with Japanese equipment. Several
      of those were credited elsewhere in earlier versions of this document and
      have been returned to him. **The printed book has still not been read.**
      Its defensive-arms and *kalasag* passages, and its Sarangani Island
      section, were read for this document only in an
      optical-character-recognition transcription, which visibly garbles nearby
      words — it renders *morriones* as "monones" — and cannot be relied on for
      spellings or page numbers. `WEAPON_CLASH_1500s.md` records the same gap at
      its line 718.
21. Laura Lee Junker, *Raiding, Trading, and Feasting: The Political Economy of
    Philippine Chiefdoms*, University of Hawai'i Press, 1999
    - Modern scholarship on Philippine chiefdoms. Earlier versions of this
      document cited it for the rattan-strengthened, resin-coated shield
      construction and the 50-by-150-centimetre figure. Those are Scott's, at
      his page 151, and no claim in this document now rests on Junker. *Not read
      directly for this document.*

### 13.5 Repository documents

22. `docs/research/HISTORICAL_1500s_WEAPONS.md` — the companion evidence record
    for offensive equipment, and the source of the confidence-label vocabulary
    and the pair-form and hundred-year rules as applied to equipment names.
23. `docs/research/WEAPON_CLASH_1500s.md` — the evidence record for blade and
    shield interaction, for the unattested state of Philippine technique, and
    for the unresolved enmeshing claim at its line 720.
24. `docs/research/improve-visuals/warrior-appearance-historical-research.md` —
    the appearance research whose Category F armor layer, at lines 462-566,
    drives the pawn renderer, and which section 10 above corrects on seven
    points of evidence.
25. `CLAUDE.md` section 7 — the repository's binding historical accuracy policy.

## 14. Closing statement

The sixteenth-century record for Philippine protective equipment is thin, and
this document has tried to be exact about how thin, and about which parts of it
have actually been checked.

Shields are strong. They are attested everywhere, in every region and decade the
sources reach, from 1521 onward, and one 1521 sentence gives their material and
their performance under fire. Body armor is better attested than this document
once said and no better understood: nine or more materials named across four
regions between 1521 and 1574, three of them by a participant in 1548, and not
one sixteenth-century sentence describing how any of it was made. Its prevalence
is unknown; no source states one. Head protection is thinner still but no longer
the weakest link it was, because the 1548 relation names head armor of dogfish
skin outright and the 1574 shell-set helmet is no longer alone.

Three 1521 and 1548 attestations — *baluti* for quilted fighting garments,
*calassan* for shields, and Alvarado's four body-armor types with their
dogfish-skin head armor — are the best-supported protective-equipment evidence
available for the period, and all were read in the primary text. What is left
after that is a modern scholar read in a scan, a handful of relations still
unread, and a very large quantity of things people repeat.

Readers of an earlier version of this document should know that several of its
central claims were wrong and are corrected here: it printed Artieda's armor
sentence with its meaning reversed, dated and paged the Cauchela and Aldave
letter wrongly, presented an inference about the arquebus clause as though it
were the source's own words, credited Scott's shield description to Junker and
Scott's octopus-skin helmet to a sixteenth-century relation, called a
two-document report a one-document report, and left out the single richest
sixteenth-century passage on the subject entirely. None of those errors was
detectable from the document itself. All of them were detectable from the
volumes.

Nothing in this document proposes a mechanic, a value, an enum, or a preset. It
records what the sources say, what they do not say, what remains contested, and
what nobody has read yet. Any gameplay use of this material is a separate
artifact making a different kind of claim, and it must be labeled as such
wherever it appears, exactly as `HISTORICAL_1500s_WEAPONS.md` requires of the
combat preset built from its evidence.
