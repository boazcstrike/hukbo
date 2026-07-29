# Personal Names in the Philippines, 1500s

Research date: 2026-07-29

Status: research. This document does not define a final name generator, and
its remaining work list in section 11 is unchanged.

A narrowed subset of Approach B was implemented on 2026-07-29 as a
presentation-only naming layer in `Hukbo.Client`, shown in the battle report
and the agent inspector. That implementation ships only the region-scoped
ledger of recorded forms and none of the optional layers: no titles, no
parenthood forms, no reputation or friendship names, no Christian-plus-local
forms, and no famous historical bearers. See
[docs/plans/2026-07-29-warrior-personal-names-design.md](../plans/2026-07-29-warrior-personal-names-design.md)
for what was cleared, what was excluded, and which tests pin each exclusion.
Nothing in this document is superseded by that implementation.

This document asks what personal names Hukbo could eventually use for people
from the Philippines in the sixteenth century, how those names might be
combined, and what can be offered for people of different genders without
inventing a false historical balance. It focuses on names recorded in sources
from 1521 to 1589, with a small, separately marked comparison set from the
seventeenth century. The 1604 material is treated as near-contemporary
comparison; the 1640–1668 material is later comparison only.

The short answer is:

1. A single personal name is the safest locally grounded form.
2. The closest historically grounded equivalent to a *Dwarf Fortress*
   two-part name is not a modern inherited surname. It is a personal name
   accompanied by a title, a parenthood name, a Christian baptismal name, or
   a later name of reputation.
3. Names must be generated from a regional source set. A Tagalog name element
   should not be joined to a Visayan or Mindanao element merely because both
   are now Philippine.
4. The surviving sixteenth-century lists are overwhelmingly lists of male
   chiefs, witnesses, defendants, and envoys. The sources consulted here do
   not provide a comparable list of women's local birth names. This is a
   documentary gap, not evidence that women lacked names and not permission to
   fill the gap with modern poetic names.
5. The best eventual design is therefore a region-scoped naming system with a
   locally recorded name core and optional layers gated by source date. It
   should not be a free-form compound-word generator.

## 1. What “like Dwarf Fortress” can mean here

The *Dwarf Fortress* naming model gives a person a first name and a non-
inherited last name whose translated form is a two-word compound. The useful
idea for Hukbo is the **combinatorial variety**, not the exact first-name-plus-
surname structure.

**Documented** at their respective source dates. The sources describe several
ways for one person to acquire more than one identifying expression:

- a personal name given at birth;
- a title or respectful form placed before a name;
- a name identifying someone as the father or mother of a child;
- a new metaphorical or reputational name [documented only in the 1663
  comparison source];
- in early colonial settings, a Christian baptismal name used with a local
  birth name;
- a reciprocal friendship name based on a shared event [documented only in the
  1663 comparison source].

Those are richer than a static surname. They can also change during a person's
life. That is a better historical foundation for a simulation than assigning
every person a permanent fantasy compound.

The important restraint is that a source describing a **new** name does not
prove that the old and new names were spoken together. A future interface
might display both for player comprehension—`Bacal, later called
Dimatanassan`—but should not silently turn that into the invented full name
`Bacal Dimatanassan`.

## 2. Evidence rules

This document follows Hukbo's existing historical policy.

- **Documented:** directly recorded in the source and at the source's own date.
- **Documented, form uncertain:** the person or naming practice is recorded,
  but the spelling, segmentation, language, exact local form, or later
  normalization is uncertain.
- **Provisional reconstruction:** a plausible use inferred from a documented
  practice, but not itself recorded in the target period.
- **Excluded from the corpus** is an editorial disposition, not a fourth
  evidence class. It applies when the available evidence is too late,
  legendary, modern, or unsourced to support even a responsible
  **Provisional reconstruction** for the target context.

Source attestation and target-period clearance are separate. A form can be
**Documented** in 1604 or 1663 while its use in a 1500s roster remains
**Provisional reconstruction** or excluded. “Within a century” is not a
substitute for target-period evidence, especially when the intended scenario
could be as early as 1521.

Two additional distinctions are essential:

- **Recorded bearer** means a source names a particular historical person. It
  does not prove that the name was common or reusable.
- **Naming example** means a source explicitly presents a form as an example
  of how people were named. This is stronger evidence for a reusable pool,
  even when the source is later than the 1500s.

The `Gender evidence` column records what the source says about the example or
bearer. It does **not** claim that a name was intrinsically restricted to that
gender.

## 3. Limits of the record

### 3.1 Colonial spelling is not a transparent transcription

**Documented, form uncertain.** Pigafetta wrote locally recorded names through
an early-sixteenth-century Italian
orthography. Spanish notaries used inconsistent spellings, sometimes spelling
the same person's name differently within one proceeding. Blair and
Robertson then translated and typeset those records in the early twentieth
century.

Examples of the resulting variation include:

- `Siaui`, `Siain`, `Siani`, and `Siagu` for the same recorded ruler;
- `Limasancay`, `Limansacay`, and similar variants in one 1579 dossier;
- `Tuambaçan`, `Tuambacan`, and `Tuam Basar` in the 1589 conspiracy record;
- `Panga` and `Pangan`;
- `Bolingui` and the editorially encountered `Balinguit`.

Hukbo should preserve the recorded spelling in metadata and select a display
spelling only after language-specific review. Modernizing spelling is an
editorial act, not a mechanical cleanup.

### 3.2 The sources name elites and people in trouble

**Documented.** The largest sixteenth-century name lists opened here come
from:

- Pigafetta's encounters with rulers and chiefs in 1521;
- conquest narratives naming rulers around Manila in 1570–1571;
- a Spanish military-notarial expedition on the Mindanao River in 1579;
- the prosecution of the Tondo conspiracy in 1589.

These are not village censuses. They overrepresent men, political leaders,
envoys, interpreters, defendants, and people already interacting with Spanish
institutions.

### 3.3 Women's names are especially under-recorded

**Documented.** Pigafetta names three women in 1521 only by the Christian
names assigned to them at baptism: Johanna, Catherina, and Lisabeta. He does
not record their local birth names. The Luzon and Mindanao dossiers opened in
this pass name wives, mothers, daughters, and women affected by war, but
usually only by relationship rather than a local personal name.

**Documented.** Pedro Chirino's 1604 account supplies the earliest explicit
Tagalog example opened in this pass: the male/female pair `Ilog` / `Iloguin`.
Francisco Colin's 1663 account later adds `Mati` and `Sanguy` with the title
`Dayang`.
These are valuable comparative evidence, but they are not direct proof of a
1521 or 1589 roster. Colin also drew heavily on Chirino, so the later account
is not fully independent corroboration.

The honest current result is therefore:

- **locally recorded non-baptismal men's names from the 1500s:** numerous;
- **locally recorded non-baptismal women's names from the 1500s in the opened
  set:** none
  securely identified;
- **women's Christian names assigned in the Philippines in 1521:** three;
- **explicit local women's naming examples by 1604:** one;
- **additional explicit women's examples by 1663:** two.

This imbalance should remain visible until additional archival sources improve
it.

### 3.4 A historical bearer is not automatically a generic name

**Documented.** `Lapulapu`, `Humabon`, `Soliman`, and `Magat Salamat`
identify particular
historical people. Reusing those names for ordinary generated soldiers may
make the roster look as though it contains copies of famous figures. They are
excellent evidence for spelling and name structure, but should remain
**reference-only** unless the product deliberately chooses historical-name
reuse.

## 4. Naming structures supported by the sources

### 4.1 A single personal name

**Documented** in Chirino's 1604 account; **Provisional reconstruction** when
projected back into a 1500s roster. Chirino says the mother named a child at
birth, the child ordinarily used that one name, and there was no continuing
inherited family surname. His examples draw on:

- a circumstance of birth: `Maliuag`, “difficult”;
- a hoped-for quality: `Malacas`, “strong”;
- an ordinary word: `Daan`, “road”; `Babui`, “pig”; `Manug`, “fowl”;
- the male/female comparison `Ilog` / `Iloguin`, from `ilog`, “river”.

The sixteenth-century lists are consistent with a single-name system, but
Chirino is the earliest opened source that explicitly explains it. His
chapter generalizes beyond the Tagalog communities he knew, so this document
uses it as Tagalog evidence rather than a universal rule for every Philippine
language. Colin's 1663 account later repeats and expands the pattern.

### 4.2 Respectful particle or title plus a name

**Documented, form uncertain.** Sixteenth-century records repeatedly use
titles translated or transcribed as `raia` or `raxa`, `dato`, and personal
forms beginning in `Si` or `Ci`. The National Quincentennial Committee treats
the `Çi` of Pigafetta's `Çilapulapu` as probably an honorific related to `Si`,
while also standardizing the historical figure's display name as `Lapulapu`.

Colin later gives `Lacan` or `Gat` before men's names and `Dayang` before
women's names, with `Dayang Mati` and `Dayang Sanguy` as examples. That is
useful comparative evidence, but it should not cause every generated person to
receive an elite title.

Candidate structures for later evaluation:

```text
Dato/Datu + personal name
Raia/Raja + personal name
Gat/Lacan + personal name       [later comparative evidence]
Dayang + personal name          [later comparative evidence]
```

Titles encode standing, region, and context. They are not interchangeable
prefixes and are not ordinary given names. Because `Çi` is only *probably* an
honorific in Pigafetta, productive `Si + personal name` is a
**Provisional reconstruction**; retain the directly recorded full forms until
a language-specific review supports the rule.

### 4.3 Christian baptismal name plus local identifier

**Documented.** The 1589 Tondo record repeatedly combines a Christian name
with a locally recorded second element:

```text
Agustin Manuguit
Phelipe Salalila
Joan Banal
Antonio Surabao
Geronimo Bassi
Luis Amanicalao
Phelipe Salonga
```

This is a strong early-contact pattern. It is historically different from an
unchanged pre-contact naming system and should belong to a dated or
contact-specific roster, not every Hukbo scenario.

Pigafetta also records Christian names assigned during the 1521 Cebu baptism:
Dom Charles, Dom Fernand, Jehan, Christofle, Johanna, Catherina, and Lisabeta.
These are evidence of the encounter and the baptismal naming act, not local
birth-name forms.

Chirino's 1604 example is especially useful for the transition: a mother names
her child `Maliuag` after a difficult birth, and the child is later baptized
`Ignacio`. Chirino then says that a Christian name could be used with the
mother-given birth name as a surname. The story documents both identities, but
does not itself print the child as the combined form `Ignacio Maliuag`; that
combination should not be silently backfilled into the quotation.

### 4.4 Parenthood or teknonymic name

**Documented, form uncertain.** Chirino says the firstborn son or daughter
gave the parents a new relational form:

```text
Ama ni [firstborn]    father of [firstborn]
Ina ni [firstborn]    mother of [firstborn]
```

Colin later prints worked forms such as `Amani Maliuag` and `Ynani Malacas`.
The 1589 Tondo record independently contains fused male forms that may be
consistent with this structure:

```text
Amarlangagui
Amanicalao
Amaghicon
```

`Amanicalao`, paired in the record with the son `Calao`, is the strongest
case. `Amarlangagui` and `Amaghicon` have been interpreted as forms like
`Ama ni Langagui` and `Ama ni Hicon`, but those segmentations are
**Provisional reconstruction** pending a Tagalog specialist. The source itself
does not provide the modern segmentations.

This is one of the best future sources of variety because it changes with
family history. It must not be treated as an inherited surname.

### 4.5 Reputational or metaphorical new name

**Documented, form uncertain** in Colin's 1663 account; excluded from a
historically labeled 1500s corpus until target-period evidence is found. Colin
says the new name was called a `Pamagat` and could be awarded at a banquet.
His two worked examples are:

| Earlier name | Meaning given by Colin | New name | Meaning given by Colin |
| --- | --- | --- | --- |
| `Bacal` | iron | `Dimatanassan` | not spoiled by time |
| `Bayani` | valiant or spirited | `Dimalapitan` | one whom no one is bold enough to approach |

This is the closest evidence-backed analogue to a translated *Dwarf Fortress*
compound surname: a memorable semantic name tied to reputation. However, the
source describes a **replacement or new name**, not an inherited second name
and not necessarily a word formed by freely joining any two dictionary roots.

Before Hukbo generates new `Di-` forms, a specialist must validate the period
meaning, morphology, phonology, and spelling. `Di + random noun + random noun`
is not a historically supported algorithm.

### 4.6 Reciprocal friendship name

**Documented, form uncertain** in Colin's 1663 account; excluded from a
historically labeled 1500s corpus until target-period evidence is found. Colin
gives `Casolasi` for two people linked by the gift of a sweet-basil branch and
`Caytlog` for people who shared an egg.

It is attractive for simulation history, but the examples are from 1663 and
their morphology has not been checked in the 1613 Tagalog dictionary during
this pass. Keep the idea in research until that work is done.

## 5. Sixteenth-century reference corpus

These tables are a **reference corpus**, not a final random-selection pool.
They preserve what the opened translations print and avoid speculative
etymologies. They identify the source document or volume, but not yet an exact
page or stable anchor for every row. Per-row source locators and
original-language checks are required before implementation.

### 5.1 Central Philippines and northeastern Mindanao, 1521

Source: Antonio Pigafetta's account, in Blair and Robertson, Volume 33.

| Recorded form | Context in the source | Gender evidence | Confidence | Reuse note |
| --- | --- | --- | --- | --- |
| `Colambu` | `Raia Colambu`, one of two rulers met at Mazaua | Recorded man | **Documented, form uncertain** | Historical bearer; reference-only by default |
| `Siaui` | Second ruler; manuscripts and editors also give `Siain`, `Siani`, `Siagu` | Recorded man | **Documented, form uncertain** | Preserve variants |
| `Humabon` | `Raia Humabon`, ruler at Cebu | Recorded man | **Documented** | Famous historical bearer |
| `Cadaio` | Humabon's brother at Cebu | Recorded man | **Documented** | Historical bearer |
| `Simiut` | One of Cebu's principal men | Recorded man by chiefly context | **Documented** | Historical bearer |
| `Sibuaia` | One of Cebu's principal men | Recorded man by chiefly context | **Documented** | Historical bearer |
| `Sisacai` | One of Cebu's principal men | Recorded man by chiefly context | **Documented** | Historical bearer |
| `Maghalibe` | One of Cebu's principal men | Recorded man by chiefly context | **Documented** | Historical bearer |
| `Cilaton` | One of the chiefs of Cinghapola | Recorded man by chiefly context | **Documented, form uncertain** | Do not assume `Ci` segmentation |
| `Ciguibucan` | Chief of Cinghapola | Recorded man by chiefly context | **Documented, form uncertain** | Recorded spelling only |
| `Cimaningha` | Chief of Cinghapola | Recorded man by chiefly context | **Documented, form uncertain** | Recorded spelling only |
| `Cimatichat` | Chief of Cinghapola | Recorded man by chiefly context | **Documented, form uncertain** | Recorded spelling only |
| `Cicanbul` | Chief of Cinghapola | Recorded man by chiefly context | **Documented, form uncertain** | Recorded spelling only |
| `Apanoaan` | Chief of Mandaui; a second `Apanoan` is reported at Puzzo in an NQC summary | Recorded man by chiefly context | **Documented, form uncertain** | Possible repeated name, title, or scribal variation |
| `Theteu` | Chief of Lalan | Recorded man by chiefly context | **Documented, form uncertain** | Recorded spelling only |
| `Tapan` | Chief of Lalutan | Recorded man by chiefly context | **Documented, form uncertain** | Recorded spelling only |
| `Zula` | One of the two chiefs of Matan | Recorded man | **Documented** | Historical bearer |
| `Cilapulapu` | Other chief of Matan | Recorded man | **Documented, form uncertain** | NQC display form: `Lapulapu`; famous historical bearer |
| `Calanao` | `Raia Calanao`, ruler at Quipit | Recorded man | **Documented, form uncertain** | Historical bearer |

`Cilumai`, `Lubucun`, `Cinghapola`, `Mandaui`, `Lalan`, `Lalutan`, `Matan`,
and `Bulaia` are settlements or places in the relevant passage, not personal
names. They must not leak into a personal-name pool.

Pigafetta's harder forms also include `Lambuzzan`, `Acibagalen`, and several
manuscript variants. They belong in a transcription ledger before any use.
`Bendara` is excluded because it may be the Malay-derived office
*bendahara*, not a personal name.

### 5.2 Cebu, Bohol, and Leyte, 1565

Source: Miguel Lopez de Legazpi's expedition relations in Blair and Robertson,
Volume 2.

| Recorded form | Context in the source | Gender evidence | Confidence | Reuse note |
| --- | --- | --- | --- | --- |
| `Tupas` | Ruler at Cebu | Recorded man | **Documented** | Famous historical bearer |
| `Simaquio` | Cebu chief, husband, and father | Recorded man | **Documented** | Historical bearer |
| `Çicatuna` / `Sikatuna` | Chief at Bohol | Recorded man | **Documented, form uncertain** | Preserve the recorded and normalized forms separately |
| `Çigala` / `Sigala` | Chief at Bohol | Recorded man | **Documented, form uncertain** | Preserve the recorded and normalized forms separately |
| `Canatuan` | Chief at Cabalian, Leyte; son of Malate | Recorded man | **Documented, form uncertain** | Historical bearer |
| `Malate` | Principal chief at Cabalian; father of Canatuan | Recorded man | **Documented, form uncertain** | Do not confuse automatically with the later Manila place name |
| `Saripara` / `Sarriparra` | Named as Tupas's father and an earlier ruler | Recorded man | **Documented, form uncertain** | Source form and identification vary |

The same expedition records women mainly by relationship. `Isabel`, assigned
to a niece of Tupas at baptism, belongs with the contact-era Christian names
in Section 6 rather than a local Visayan birth-name pool.

### 5.3 Manila and nearby Luzon, 1570–1571

Source: the 1570 voyage to Luzon and the 1571 conquest narrative in Blair and
Robertson, Volume 3.

| Recorded form | Context in the source | Gender evidence | Confidence | Reuse note |
| --- | --- | --- | --- | --- |
| `Soliman` | `Raxa Soliman`, a ruler of Manila | Recorded man | **Documented** | Title and personal name must remain distinct |
| `Laya` | A Manila chief later described as having died a Christian | Recorded man | **Documented, form uncertain** | Historical bearer |
| `Aljandora` | Listed among principal chiefs receiving Legazpi | Recorded man by context | **Documented, form uncertain** | Transcription requires checking |
| `Marlanavay` | Listed among principal chiefs | Recorded man by context | **Documented, form uncertain** | Transcription requires checking |
| `Salelaxa` | Listed among principal chiefs | Recorded man by context | **Documented, form uncertain** | May contain a title or segmentation hidden by the transcription |

The same passage also prints `Rraxa` and `Maguno` in a list of principals.
Because those strings may be titles or status terms rather than personal
names, they are excluded from the candidate corpus pending source-language
review.

### 5.4 Tondo and neighboring communities, 1589

Source: the official summary of proceedings titled “Conspiracy Against the
Spaniards,” in Blair and Robertson, Volume 7.

| Local or second element as printed | Full recorded example or context | Gender evidence | Confidence | Structural note |
| --- | --- | --- | --- | --- |
| `Panga` / `Pangan` | Martin Panga, governor of Tondo | Recorded man | **Documented, form uncertain** | Spelling varies |
| `Magat Salamat` | Chief and son of an earlier lord of Tondo | Recorded man | **Documented, form uncertain** | `Magat` may be a title; do not split or recombine yet |
| `Manuguit` | Agustin Manuguit | Recorded man | **Documented, form uncertain** | Christian plus locally recorded element |
| `Salalila` | Phelipe Salalila | Recorded man | **Documented** | Christian plus locally recorded element |
| `Banal` | Joan Banal | Recorded man | **Documented** | Christian plus locally recorded element |
| `Surabao` | Antonio Surabao | Recorded man | **Documented, form uncertain** | Christian plus locally recorded element |
| `Sumaelob` | Chief of Cuyo | Recorded man | **Documented, form uncertain** | Region is Cuyo, not Tagalog by assumption |
| `Amarlangagui` | Chief of Baibai; also Phelipe Amarlangagui | Recorded man | **Documented, form uncertain** | Likely parenthood form; segmentation unverified |
| `Amaghicon` | Chief of Navotas | Recorded man | **Documented, form uncertain** | Likely parenthood form; segmentation unverified |
| `Bassi` | Geronimo Bassi | Recorded man | **Documented, form uncertain** | Christian plus locally recorded element |
| `Tuambaçan` / `Tuambacan` / `Tuam Basar` | Gabriel, a brother of Agustin de Legaspi | Recorded man | **Documented, form uncertain** | Strong spelling uncertainty |
| `Acta` | Francisco Acta and his son | Recorded man | **Documented, form uncertain** | Christian plus locally recorded element |
| `Pitongatan` | Named defendant | Recorded man | **Documented, form uncertain** | Single recorded form |
| `Bolingui` | Pedro Bolingui, chief of Pandaca | Recorded man | **Documented, form uncertain** | Other editions may differ |
| `Amanicalao` | Luis Amanicalao; `Calao` is identified as his son | Recorded man | **Documented, form uncertain** | Direct evidence for a parenthood structure |
| `Calao` | Son of Luis Amanicalao | Recorded man | **Documented, form uncertain** | Relationship makes the parenthood reading especially strong |
| `Capolo` | Dionisio Capolo | Recorded man | **Documented, form uncertain** | Christian plus locally recorded element |
| `Salonga` | Phelipe Salonga | Recorded man | **Documented** | Christian plus locally recorded element |

This list is particularly useful for structure, but especially poor as a
gender-balanced ordinary-person pool: it is a prosecution record centered on
male political leaders.

### 5.5 Mindanao River dossier, 1579

Source: the records of Captain Gabriel de Ribera's expedition, in Blair and
Robertson, Volume 4.

| Recorded form | Context in the source | Gender evidence | Confidence | Structural note |
| --- | --- | --- | --- | --- |
| `Limasancay` / `Limansacay` | Ruler on the Mindanao River | Recorded man | **Documented, form uncertain** | Multiple spellings in one dossier |
| `Asututan` | Limasancay's deceased father | Recorded man | **Documented, form uncertain** | Historical bearer |
| `Umapas` | Envoy or intermediary | Recorded man | **Documented, form uncertain** | Historical bearer |
| `Sicuyrey` | Chief and cousin of Limasancay | Recorded man | **Documented, form uncertain** | Historical bearer |
| `Dato Bandel` / `Dato Bahandil` | Chief hostile to Limasancay | Recorded man | **Documented, form uncertain** | Keep `Dato` as title, not a name root |
| `Siproa` | Limasancay's father-in-law | Recorded man | **Documented, form uncertain** | Historical bearer |
| `Batala` | Chief and master of Sidurman | Recorded man | **Documented, form uncertain** | Do not infer relation to Tagalog religious vocabulary |
| `Sidurman` | A dependent of Batala | Recorded man | **Documented, form uncertain** | Historical bearer |
| `Atagayta` | A dependent of Limasancay | Recorded man | **Documented, form uncertain** | Historical bearer |
| `Laquidan` / `Laquian` | Interpreter named in the proceedings | Recorded man by role and grammar | **Documented, form uncertain** | Spelling varies |
| `Sihauil` | A man from Dato Bahandil's town | Recorded man | **Documented, form uncertain** | Historical bearer |
| `Simangary` | Messenger | Recorded man | **Documented, form uncertain** | Historical bearer |
| `Dato Sibatala` | Chief encountered by a messenger | Recorded man | **Documented, form uncertain** | Keep title separate |

The dossier is direct sixteenth-century evidence, but the notary's spelling
and the exact languages represented along the river require specialist
review. These forms must not be mixed into a generic Visayan or Tagalog pool.

## 6. Women's and gender-unspecified comparison corpus

This table is separate because its evidence comes from different dates or
different naming circumstances.

| Recorded form | Source and context | Gender evidence | Source attestation | Clearance for a 1500s roster |
| --- | --- | --- | --- | --- |
| `Johanna` | Pigafetta, 1521; name assigned to the queen of Cebu at baptism | Recorded woman | **Documented** | Cleared only as a contact-era Christian name; not a local birth name |
| `Catherina` | Pigafetta, 1521; name assigned to the queen's daughter | Recorded woman | **Documented** | Cleared only as a contact-era Christian name; not a local birth name |
| `Lisabeta` | Pigafetta, 1521; name assigned to the queen of Mazaua | Recorded woman | **Documented** | Cleared only as a contact-era Christian name; not a local birth name |
| `Isabel` | Legazpi expedition account, 1565; name assigned to a niece of Tupas at baptism | Recorded woman | **Documented** | Cleared only as a contact-era Christian name; not a local birth name |
| `Iloguin` | Chirino, 1604; explicit female counterpart to `Ilog` | Explicit woman naming example | **Documented, form uncertain** in 1604 | **Provisional reconstruction** for the 1500s; do not generalize the suffix |
| `Mati` | Colin, 1663; `Dayang Mati` | Explicit woman naming example | **Documented, form uncertain** in 1663 | Excluded from the 1500s corpus pending earlier evidence |
| `Sanguy` | Colin, 1663; `Dayang Sanguy` | Explicit woman naming example | **Documented, form uncertain** in 1663 | Excluded from the 1500s corpus pending earlier evidence |
| `Maliuag` | Chirino, 1604; a mother names a child after a difficult birth | Not specified | **Documented, form uncertain** in 1604 | **Provisional reconstruction** for the 1500s; narrated bearer but gender unknown |
| `Malacas` | Chirino, 1604; hoped-for-strength example | Not specified | **Documented, form uncertain** in 1604 | **Provisional reconstruction** for the 1500s; author's example |
| `Daan` | Chirino, 1604; ordinary-word example | Not specified | **Documented, form uncertain** in 1604 | **Provisional reconstruction** for the 1500s; author's example |
| `Babui` | Chirino, 1604; ordinary-word example | Not specified | **Documented, form uncertain** in 1604 | **Provisional reconstruction** for the 1500s; author's example |
| `Manug` | Chirino, 1604; ordinary-word example | Not specified | **Documented, form uncertain** in 1604 | **Provisional reconstruction** for the 1500s; author's example |
| `Damo` | Colin, 1663; thing noticed by the name-giver | Not specified | **Documented, form uncertain** in 1663 | Excluded from the 1500s corpus pending earlier evidence |
| `Bacal` | Colin, 1663; earlier name in a `Pamagat` example | Not specified | **Documented, form uncertain** in 1663 | Excluded from the 1500s corpus pending earlier evidence |
| `Bayani` | Colin, 1663; earlier name in a `Pamagat` example | Not specified | **Documented, form uncertain** in 1663 | Excluded from the 1500s corpus pending earlier evidence |

Chirino says women's names could be differentiated by adding `in`, but
supplies only `Ilog` / `Iloguin` as the worked pair. Colin's later `Mati` and
`Sanguy` examples do not show that suffix. A future system must **not**
generate women's names by appending `-in` to every recorded men's name.

The correct data model for later work is not two confident lists called
`maleNames` and `femaleNames`. It is:

```text
recordedGender: man | woman | unspecified
genderRestriction: unproven unless a source explicitly establishes it
```

That preserves the evidence without claiming that every historically male
bearer's name was linguistically male-only.

No gender-balanced local birth-name catalog for the 1500s is cleared by this
pass. A future balanced roster would require either new archival evidence or
visibly labeled **Provisional reconstruction** reviewed within each language
and region.

### 6.1 Later Visayan comparison, not a 1500s roster

**Documented, form uncertain** in a later source. Malcolm Mintz's study of
Visayan childhood cites Diego Bobadilla's 1640
Cebuano observations: `maglintí'` for a child born amid thunder and lightning,
and `gubáton` for one born amid war or the arrival of enemy boats. Bobadilla
does not specify gender. These examples support the *structure* of an
event-derived whole name, but their use in a 1500s pool would be
**Provisional reconstruction**.

**Documented, form uncertain** as later literary evidence reported through a
modern study. Francisco Ignacio Alcina's 1668 eastern Visayan history
preserves literary
women such as `Bugbung Humasanun` (with manuscript variants), `Bubung Ginbuna`
(with variants), and `Diibtang`, alongside male epic figures such as
`Sumanga` and `Kabungaw`. They are literary characters recorded well after
the target period, not documented sixteenth-century people. They may guide a
future **Provisional reconstruction** literary-inspiration layer only; they
must not be promoted into the attested historical corpus.

## 7. Three possible catalog approaches

### Approach A: exact historical forms only

Use only names recorded for sixteenth-century people, preserving a chosen
editorial spelling.

Advantages:

- strongest direct attestation;
- smallest amount of linguistic invention;
- easiest source audit.

Costs:

- heavily male and elite;
- duplicates famous historical people;
- limited ordinary-person variety;
- reproduces colonial scribal spellings as though they were a native standard.

This is suitable for named historical scenarios, not a large procedural
population.

### Approach B: regional names plus documented life-name structures

Start with a region-specific ledger of attested forms. For a historically
labeled 1500s roster, allow only layers directly supported in the target
setting: a valid title, the strongly evidenced Tondo parenthood form, or a
contact-era Christian-plus-local form. Chirino's 1604 birth-name examples are
a near-contemporary **Provisional reconstruction** layer. Colin's 1663
`Pamagat` and friendship forms remain excluded until earlier evidence is
found.

Procedural reuse of a name known only from one historical bearer is also a
**Provisional reconstruction**. Prefer repeated forms or explicit naming
examples for reusable pools; otherwise reserve the form for its known bearer.

Advantages:

- keeps the cleared 1500s core distinct from later comparison practices;
- produces variety through a person's history rather than arbitrary syllable
  mixing;
- supports famous individuals, parents, and converts without inventing a
  surname system;
- naturally fits a simulation that tracks events.

Costs:

- requires language-specific morphology and source review;
- needs a clear date and region for each rule;
- women's source material remains thin and needs more archival work.

**Recommendation:** this is the best long-term direction for Hukbo.

### Approach C: fantasy compounds from historical-language dictionaries

Select two period vocabulary roots and join them into a translated compound,
similar to *Dwarf Fortress*.

Advantages:

- very high variety;
- immediately legible translated meanings;
- mechanically simple.

Costs:

- a dictionary proves that words existed, not that their combination was a
  personal name;
- compounding, affixation, sound changes, and word order differ by language;
- arbitrary compounds can become nonsense or accidentally offensive;
- the output would be inspired by historical languages, not historical names.

This approach should be excluded from a historically labeled preset. If ever
used, it should be openly labeled **Provisional reconstruction** or fantasy
and reviewed by a speaker or historical linguist.

## 8. Recommended rules for joining names

These rules are research conclusions, not implementation instructions.

### Allowed for later design work

1. **Keep one regional grammar per person.**
2. **Preserve titles as titles.** Do not merge `Datu`, `Raja`, `Gat`,
   `Lakan`, or `Dayang` into a universal name-root list.
3. **Model parenthood names as relationships.** Generate them only when the
   referenced child exists.
4. **Keep later reputation and friendship names out of a 1500s preset.** If a
   later-period design eventually uses them, model them as aliases or
   replacements rather than inherited surnames.
5. **Use Christian-plus-local forms only in dated contact contexts.**
6. **Keep recorded spelling, display spelling, and English gloss separate.**
7. **Keep famous historical names reference-only by default.**
8. **Attach evidence metadata to every element and every joining rule.**

### Not supported

- a pan-Philippine pool mixing Tagalog, Visayan, Mindanao, Ilocano, and other
  roots;
- inherited European-style surnames for a pre-contact roster;
- random noun+noun compounds presented as historical;
- mechanically adding `-in` to create a woman's form;
- treating the recorded gender of one bearer as proof of a gender-exclusive
  name;
- using a place name as a person because it appears beside chiefs in a source;
- silently modernizing every colonial spelling;
- assigning elite titles to ordinary people for flavor;
- using modern popular names merely because their meanings sound old.

## 9. Names and forms not cleared in this pass

The following widely circulated forms should not enter a historical pool
without better period evidence:

- **Documented, form uncertain.** `Humamay` as the local birth name of
  Humabon's wife is not cleared. Pigafetta does not give
  that name; the National Quincentennial Committee describes it as folkloric.
- **Documented.** `Kalipulako`, `Calipulaco`, `Qari Pulako`, and similar
  expansions of
  Lapulapu. NQC traces major variants to later interpretation and
  nineteenth-century nationalist usage; its historically standardized form is
  `Lapulapu`.
- **Documented.** `Kalantiaw` and any names justified only through the
  fraudulent “Code of Kalantiaw” tradition are excluded.
- **Documented.** The `Maragtas` cast—such as `Datu Puti`, `Sumakwel`, `Marikudo`,
  `Kapinangan`, `Paiburong`, and `Bangkaya`—when presented as documented
  sixteenth-century people. The written work carrying these names was
  published in 1907 and is not a pre-Hispanic document.
- **Documented, form uncertain.** `Urduja` is not cleared as a securely
  identified Philippine or Pangasinan ruler; the proposed identification
  remains disputed.
- legendary or modern-literary figures presented as securely historical
  people without a contemporary source;
- **Provisional reconstruction.** Present-day poetic given names such as words
  for stars, dawn, purity, or
  aspiration when no opened sixteenth-century source uses them as personal
  names are excluded from a historically labeled pool.
- **Documented.** Plasencia's 1589 account records deity and celestial terms,
  not a registry of ordinary human personal names. Reusing those terms as
  people without separate evidence is excluded.
- any supposed “ancient Filipino name list” that does not identify a source,
  date, region, recorded spelling, and whether the entry is a person, title,
  place, or ordinary word.

This does not mean every such name is linguistically inauthentic or unusable
in modern life. It means this research has not cleared it for a historically
labeled sixteenth-century roster.

## 10. Research-ready metadata for a later phase

If implementation is approved later, every catalog row should be traceable
through fields equivalent to:

```text
recordedForm
displayForm
englishGloss
languageOrRegion
sourceDate
sourceCitation
nameKind
recordedGender
genderRestriction
evidenceTier
historicalBearer
reusePolicy
compatibleJoiningRules
notes
```

Suggested `nameKind` values:

```text
personal
title
parenthood
reputation
friendship
christianBaptismal
place
uncertain
```

Keeping `place` and `uncertain` in the research data prevents them from being
accidentally promoted into the selectable personal-name pool.

## 11. Work still needed before implementation

1. Open and search the 1613 *Vocabulario de lengua tagala* for `pamagat`,
   parenthood forms, the examples Colin repeats, and productive morphology.
2. Compare Colin's 1663 summary against Chirino and San Buenaventura rather
   than treating it as independent evidence for the entire 1500s.
3. Check Alcina's 1668 Visayan name forms against the manuscript and a
   specialist edition before using any normalized spelling. Keep the epic
   figures in a later literary layer.
4. Inspect early parish, notarial, land, and will records from roughly
   1570–1630 for women's local personal names. The male political narratives are
   not an adequate substitute.
5. Obtain review from specialists or speakers for each regional pack,
   especially before normalizing spellings or generating new forms.
6. Add an exact page, document subsection or stable anchor, original source
   form, and edition ID to every catalog row before it becomes implementation
   data.
7. Decide the first scenario's date and region. A 1521 Cebu roster, a 1579
   Mindanao River roster, and a 1589 Tondo roster should not share one
   undifferentiated catalog.
8. Decide whether famous historical names are reserved for authored
   historical figures or may be reused procedurally.
9. Decide whether a player sees the contemporary name, the source spelling,
   an English gloss, or all three.

## 12. Sources consulted for this pass

1. Antonio Pigafetta, *First Voyage Around the World* (account of 1519–1522),
   in Blair and Robertson, *The Philippine Islands, 1493–1898*, Volume 33.
   The edition presents the Italian text and English translation together.
   [Project Gutenberg edition](https://www.gutenberg.org/ebooks/42884)
2. Miguel Lopez de Legazpi expedition relations (1565), in Blair and
   Robertson, Volume 2.
   [Project Gutenberg edition](https://www.gutenberg.org/ebooks/13280)
3. “Relation of the Voyage to Luzon” (1570) and the 1571 conquest narrative,
   in Blair and Robertson, Volume 3.
   [Project Gutenberg edition](https://www.gutenberg.org/ebooks/13616)
4. Records of the Mindanao expedition (1579), in Blair and Robertson,
   Volume 4.
   [Project Gutenberg edition](https://www.gutenberg.org/ebooks/12635)
5. “Conspiracy Against the Spaniards” (1589), in Blair and Robertson,
   Volume 7.
   [Project Gutenberg edition](https://www.gutenberg.org/ebooks/13701)
6. Pedro Chirino, *Relacion de las Islas Filipinas* (Rome, 1604), Chapter
   LXXX on naming. This is the earliest opened source that explicitly explains
   the Tagalog single-name, parenthood-name, and `Ilog` / `Iloguin` patterns.
   [Blair and Robertson translation](https://en.wikisource.org/wiki/The_Philippine_Islands%2C_1493%E2%80%931898/Volume_13/Relacion)
7. Francisco Colin, *Labor evangélica* (Madrid, 1663), Book 1 selections in
   Blair and Robertson, Volume 40, especially section 97 on names.
   [Project Gutenberg edition](https://www.gutenberg.org/ebooks/30253)
8. National Quincentennial Committee, “Battle of Mactan Beyond Textbooks,”
   which checks Pigafetta's Cebu chief list and identifies the source form
   `Çilapulapu`.
   [NQC resource](https://www.nqc.gov.ph/en/resources/battle-of-mactan-beyond-textbooks/)
9. National Quincentennial Committee, “Lapulapu versus Lapu-Lapu,” explaining
   the NQC and NHCP choice of `Lapulapu` and its treatment of the initial
   `Çi`.
   [NQC resource](https://www.nqc.gov.ph/ceb/resources/lapulapu-versus-lapu-lapu/)
10. National Quincentennial Committee, “Lapulapu in the Eyes of our Heroes,”
   distinguishing Pigafetta's form from later `Kalipulako` traditions.
   [NQC resource](https://www.nqc.gov.ph/en/resources/lapulapu-in-the-eyes-of-our-heroes/)
11. Malcolm Mintz, “Childhood,” citing Diego Bobadilla's 1640 Cebuano
    observations on event-derived names.
    [Intersections](https://intersections.anu.edu.au/monograph1/mintz_childhood.html)
12. Instituto Cervantes, description of Francisco Ignacio Alcina's 1668
    eastern Visayan manuscript. Adrian De Leon's modern study, rather than a
    direct manuscript reading in this pass, supplied the reported epic name
    forms.
    [Instituto Cervantes](https://manila.cervantes.es/es/biblioteca_espanol/Filipiniana/XVII/Filipiniana_XVII_Alzina.htm)
    [De Leon study](https://escholarship.org/content/qt61q8p086/qt61q8p086.pdf)
13. Dwarf Fortress Wiki, “Name,” for the comparison model: a first name and a
   non-inherited compound last name.
   [Dwarf Fortress Wiki](https://www.dwarffortresswiki.org/index.php/Name)
14. John U. Wolff, “The Vocabulario de Lengua Tagala of Fr. Pedro de San
    Buenaventura (1613),” in *Philippine and Chamorro Linguistics Before the
    Advent of Structuralism* (2011), pp. 33–48. This confirms the importance
    and scope of the earliest surviving Tagalog dictionary; the dictionary's
    name entries were not directly opened in this pass.
    [DOI record](https://doi.org/10.1524/9783050056197.33)
15. Juan de Plasencia, “Customs of the Tagalogs” (1589), for distinguishing
    divine, cultic, and celestial terms from attested human personal names.
    [Blair and Robertson transcription](https://en.wikisource.org/wiki/The_Philippine_Islands%2C_1493%E2%80%931898/Volume_7/Documents_of_1589)

## 13. Sources identified but not opened fully

- Pedro de San Buenaventura, *Vocabulario de lengua tagala* (Pila, 1613).
- William Henry Scott, *Barangay: Sixteenth-Century Philippine Culture and
  Society* (Ateneo de Manila University Press, 1994).
- William Henry Scott, *Prehispanic Source Materials for the Study of
  Philippine History*, for the source criticism behind the Kalantiaw and
  *Maragtas* cautions.
- Source-critical scholarship on the proposed Urduja–Philippines
  identification.
  [Study record](https://tashwirulafkar.or.id/index.php/afkar/article/view/662)
- The Boxer Codex, c. 1590–1595. It remains valuable for appearance and
  social context but is not, by itself, a personal-name catalog.

## 14. Standing conclusion

**For a 1500s roster, use region-specific recorded forms first and add only
target-period-supported titles, parenthood forms, or contact-era Christian
forms. Keep Chirino's 1604 examples provisional and Colin's 1663 friendship
and earned names out of the 1500s corpus. Do not replace the missing
record—especially the missing record of women's names—with a modern fantasy
compound system and call it sixteenth-century history.**
