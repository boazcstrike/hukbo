# Army Composition, Rank, and Force Size in the Sixteenth-Century Philippines

Research date: 2026-07-28

Purpose: establish what the sixteenth-century record actually says about who
took part in a fight, what those people were called, who owed service to whom,
and how many of them a leader could put in one place. This document is a
research reference for Hukbo's battle layer. It does not define gameplay
statistics, balance values, or final reconstructions, and nothing in it
authorizes work that Gate 3 has not cleared.

Navigate:

- Deep-past track (pre-contact archaeology):
  [Depth 1: Overall warfare](battles/01-deep-past-overall-warfare.md),
  [Depth 2: Forces and command](battles/02-deep-past-forces-and-command.md),
  [Depth 3: Formations and tactics](battles/03-deep-past-formations-and-tactics.md),
  [Depth 4: Individual combat](battles/04-deep-past-individual-combat.md)
- Early-contact equipment track:
  [Historical Philippine Weapons 1500s](HISTORICAL_1500s_WEAPONS.md),
  [Weapon Clash 1500s](WEAPON_CLASH_1500s.md)
- Early-contact social-rank track:
  [Historical Philippine Ranks 1500s](HISTORICAL_1500s_RANKS.md)

[HISTORICAL_1500s_RANKS.md](HISTORICAL_1500s_RANKS.md) covers the same rank
vocabulary as section 4 below and is the evidence base already cited by
`docs/plans/2026-07-29-warrior-standing-design.md`. The two documents were
written independently. **They were reconciled term by term on 2026-07-29**
(see §9.1 below for the full record): three label and tier disagreements were
resolved in RANKS's favour, the `aliping namamahay` UI-clearance question was
settled with a stated reason, §11.4's "no rank enum" wording was corrected so
it no longer contradicts the `RankId` enum RANKS's evidence supports, and
cross-references were added in both directions so a reader of either document
reaches the same conclusion. What deliberately remains open: Mindanao and
Sulu rank vocabulary (§4.5, and item 1 of §12's open-questions list below),
the Alcina, Colin, and Boxer Codex material neither document has read (§12,
items 2-3), and whether a household dependent was ever actually fielded in a
battle line, which is recorded as an inference either way rather than a
settled fact. This document's contribution
is force size, coalition structure, contingent shape, and the maritime
organization of a force — not the rank ladder, which the other file treats in
more depth.

This file is the early-contact counterpart to
[Depth 2: Forces and command](battles/02-deep-past-forces-and-command.md).
That document works from archaeology and asks what can be inferred without any
written testimony. This one works from written testimony and asks what named
groups, obligations, and headcounts appear in it. The two tracks must not be
merged: a sixteenth-century Spanish observation is not evidence for the
eighth century, and archaeological inference is not evidence for a rank name.

## 1. Research boundary

"The Philippines in the 1500s" had no single military culture, no single
political structure, and no single vocabulary. The sources gathered here cover
a handful of places and moments:

| Window | Place | Source |
| --- | --- | --- |
| 1521 | Cebu and Mactan | Pigafetta's account of the Magellan voyage |
| 1565-1572 | Panay, Mindoro, Lubang, Manila Bay, Laguna, Pampanga | Legazpi-era relations and the anonymous 1570 and 1572 campaign narratives |
| 1577-1579 | Borneo, Sulu, the Rio Grande de Mindanao | Sande's expedition instructions and testimony |
| 1582 | Panay and the Visayas | Loarca's *Relacion de las Yslas Filipinas* |
| 1589 | Tagalog country | Plasencia's *Customs of the Tagalogs* |
| 1589 | Pampanga | *Instructions regarding the customs which the natives of Pampanga formerly observed in their lawsuits* |
| 1609 (retrospective) | Luzon and the Visayas | Morga's *Sucesos de las Islas Filipinas*, describing conditions the author dates to before and around the conquest |

Every one of these was written by a Spanish participant, official, or friar.
They are evidence about what Spaniards saw, were told, and found administratively
useful to record. They are not neutral ethnography. Three specific distortions
matter for this document:

1. **Enemy numbers are self-serving.** A relation that reports "for every
   Spaniard there were a hundred Moros" is making an argument about the valor
   of the writer's side. Treat large round enemy counts as an upper bound on
   the writer's rhetoric, not a census.
2. **Spanish administrative categories were imposed on the material.**
   Plasencia's three "castes" of nobles, commoners, and slaves, and his
   comparison of datus to "our knights", are European frames laid over
   relationships that did not have to fit them.
3. **The written record was collected to settle questions of tribute, labor,
   and litigation.** What survives about hierarchy survives because a Spanish
   court needed to know who owed what to whom, not because anyone was
   documenting an order of battle.

### Confidence labels

These follow [HISTORICAL_1500s_WEAPONS.md](HISTORICAL_1500s_WEAPONS.md) so a
claim can move between the two files without changing meaning:

- **Documented** — directly stated in a sixteenth-century source (or, where
  noted, in Morga's 1609 retrospective description of pre-conquest conditions).
- **Documented, form uncertain** — the institution or obligation is attested,
  but its local form, its extent, or the exact term for it is uncertain.
- **Provisional reconstruction** — a plausible reading supported by later
  material or by arithmetic on documented figures, but not firmly established
  for any cited event.

## 2. The single most important structural fact: there was no army

**Documented.** No source in this set describes a standing force, a permanent
command, or a leader with authority over more than his own following.

Rajah Sulayman of Manila said so himself, in a speech recorded in the 1572
relation of the conquest of Luzon. Explaining to Legazpi why he could not be
held responsible for chiefs who had broken the peace, he is quoted as saying:

> As you already know, there is no king and no sole authority in this land;
> but everyone holds his own view and opinion, and does as he prefers. There
> were some persons more powerful than I ... If I were king of this land,
> instead of being only the master of my own estate, the word I had given would
> not have been broken. But as this depended on the many, I could not, nor can
> I henceforth, do more than personally endeavor that my subjects and friends
> keep the peace and friendship that was established.

The 1589 Pampanga instructions open with the same observation in
administrative language:

> They never had anyone whom they all generally obeyed, except that only in
> each barangay they obeyed their chief, whose people are called timaguas.
> Among the chiefs, lords of barangay, he who was most powerful tyrannized over
> the others, even though they were brothers, because they were all intent upon
> their own interests.

Plasencia says it of the Tagalog barangays directly:

> There were many of these barangays in each town, or, at least, on account of
> wars, they did not settle far from one another. They were not, however,
> subject to one another, except in friendship and relationship. The chiefs, in
> their various wars, helped one another with their respective barangays.

The consequence for any simulation is structural, not decorative. A force in
the field was a **coalition of independently commanded followings**, each of
which arrived with its own leader, fought under him, and could leave with him.
The correct top-level abstraction is not "army with divisions" but "several
contingents that agreed to show up".

## 3. The unit that produced fighters: the barangay

**Documented.** Plasencia gives the size directly:

> These chiefs ruled over but few people; sometimes as many as a hundred
> houses, sometimes even less than thirty. This tribal gathering is called in
> Tagalo a *barangay*.

He also records the term's origin story as his informants gave it — that
*barangay* is the name of a boat, and that the head of a boatload became a
*dato*. Whether or not that etymology is historically correct, it is what
sixteenth-century Tagalogs told a Spanish friar, and it explains why the same
word appears in the same sources as a **type of vessel** (see section 6).

**Documented.** Loarca describes the Visayan arrangement as many chiefs per
settlement rather than one:

> ... for each village usually has many chiefs, each of whom has his own
> district, with slaves and timaguas, well known to him ...

**Provisional reconstruction.** Thirty to a hundred houses does not convert
cleanly into a fighter count. If a house held one adult man able and willing to
go, a barangay of thirty to a hundred houses yields roughly **30 to 100
fighters**, and a settlement holding several barangays yields a few hundred.
This arithmetic is consistent with the recorded engagement sizes in section 5,
but it is an inference from two documented numbers, not a documented number
itself. Do not present it in player-facing text as a historical measurement.

## 4. Rank ladders, region by region

The two best-documented ladders do not agree with each other, and the same word
means different things in different places. This is the single most common
error in popular writing on the subject, and the doc-level rule from
[AGENTS.md](../../AGENTS.md) §7 applies with full force: **do not generalize one
region's vocabulary to "the Philippines".**

### 4.1 Tagalog, from Plasencia (1589)

| Layer | Term as written in the source | Military obligation as stated | Confidence |
| --- | --- | --- | --- |
| Chief | *dato* | "governed them and were captains in their wars" | **Documented** |
| Free-born | *maharlica* | "must accompany him in war, at their own expense"; row for the dato on the water | **Documented** |
| Dependent living in own house | *aliping namamahay* | "accompanied him whenever he went beyond the island, and rowed for him" | **Documented** |
| Household dependent | *aliping sa guiguilir* | served in the house and on cultivated lands; could be sold | **Documented** |

Two details in Plasencia's account are directly usable as simulation concepts,
and both are about **reciprocity rather than rank**:

> The chief offered them beforehand a feast, and afterward they divided the
> spoils.

That is a two-sided contract, not an order. The obligation is enforced by the
expectation of a feast before and a share after, and the maharlica bears his
own cost.

> The maharlicas could not, after marriage, move from one village to another,
> or from one barangay to another, without paying a certain fine in gold ...
> Failure to pay the fine might result in a war between the barangay which the
> person left and the one which he entered.

Movement between followings was possible, priced, and a plausible cause of war.

Plasencia also notes that the *aliping namamahay* and their children could not
be made *sa guiguilir* and could not be sold, and that captives taken in war
were a principal source of the sellable class. Warfare fed the dependency
system, which is a campaign-layer concern and must not enter `Hukbo.Core`.

**The maharlika misconception.** In modern Philippine usage *maharlika* is
frequently read as "nobility" or "royalty". Plasencia's own text does not
support that: his maharlica are the free-born who owe war service to the dato
and are distinct from the chiefly class above them. The popularized
"nobility" reading is a twentieth-century development and must not be used in
this project, per
[`HISTORICAL_1500s_RANKS.md`](HISTORICAL_1500s_RANKS.md), "Terms deliberately
excluded". This is a naming trap of exactly the kind the weapons document
warns about, and it is the single most likely error for anyone adding rank
content from general knowledge.

### 4.2 Visayan, from Loarca (1582)

| Layer | Term as written in the source | Military obligation as stated | Confidence |
| --- | --- | --- | --- |
| Chief | *principal* (Spanish); *dato* elsewhere in the corpus | leads the raid, offers the sacrifice, takes the booty | **Documented** |
| Freeman | *timagua* | attends the chief's feasts; accompanies him armed on a journey; plies the oar and carries his weapons for the defense of the vessel | **Documented** |
| Dependent, three grades | *ayuey*, *tumaranpoc*, *tomataban* | graded by how many days they work for the master and what they keep | **Documented** |

Loarca's description of the timagua relationship is the closest thing in the
corpus to a service contract, and it runs **both ways**:

> For this service the chief is under obligation to defend the timagua, in his
> own person and those of his relatives, against anyone who seeks to injure him
> without cause; and thus it happens that, to defend the timaguas, fathers
> fight against their sons, and brothers against one another. ... Thus the
> timaguas live in security, and are free to pass from the service of one chief
> to that of another, whenever they so desire, and without any obstacle being
> placed in their way.

**Free exit is documented.** A follower who was not defended could leave. This
is a far better model for cohesion than discipline or drill, neither of which
appears anywhere in the corpus.

### 4.3 The *timawa* trap

**Documented, form uncertain.** In Loarca's Visayan account, *timagua* names a
free man who serves a chief with his weapons and his oar — a fighting client.
In the Tagalog and Pampangan material, the cognate word appears as the general
term for the chief's ordinary people, and by the time Rizal annotated Morga it
had come to mean simply "free, at peace, tranquil" in Tagalog. Do not treat
Visayan *timagua* and Tagalog *timawa* as one rank. Do not build a single
archipelago-wide ladder out of them.

### 4.4 Titles of address

**Documented, form uncertain.** Rizal's notes to Morga record that Colin gives
*gat* and *lakan* as chiefly titles and *dayang* for women, and that *maginoo*
(from *ginoo*, "dignity") is the title of the chiefs, with *kaginoohan* for
their assembly. These are secondary attestations reported by a nineteenth-century
annotator from a seventeenth-century author. They are usable as flavor with a
provisional marker; they are not sixteenth-century eyewitness testimony and must
not be presented as such.

### 4.5 Mindanao and Sulu

**Documented, thin.** Sande's 1578-1579 instructions and the resulting testimony
treat Jolo and the Rio Grande de Mindanao as ruled by figures the Spaniards
called kings — a "king of Jolo" who had fought them at Borneo and fled, and a
"king of Mindanao" named in testimony as Limasancay, who fled up the river with
"one virey and ten vancas". The documents are about submission, tribute, and
elephants, not about internal organization.

**Unknown or unsupported for the sixteenth century in this source set:** the
internal rank vocabulary of the Mindanao and Sulu polities. The familiar
sultanate offices are attested in later documentation, and importing them into
a 1500s battle would break the attestation-gap rule that already excluded the
*panabas* in [HISTORICAL_1500s_WEAPONS.md](HISTORICAL_1500s_WEAPONS.md). If
Hukbo ever adds a Mindanao faction, that faction needs its own research pass
against sources of the right date.

## 5. Recorded force sizes

This is the direct answer to "how many people are usually in one army". The
honest answer is that the recorded engagements are in the **hundreds to low
thousands**, and that everything above about two thousand in these sources is
either a coalition, a population estimate, or rhetoric.

| Year | Place | Recorded figure | Source | Confidence |
| --- | --- | --- | --- | --- |
| 1521 | Mactan | "more than one thousand five hundred persons", formed in **three divisions** | Pigafetta | **Documented** as a participant's estimate |
| 1521 | Cebu to Mactan | Humabon accompanied Magellan with "twenty or thirty balanguais" | Pigafetta | **Documented** |
| 1570 | Manila | one war boat with "three or four hundred fighting men and rowers on board, with many culverins and large pieces of artillery" | 1570 relation of the voyage to Luzon | **Documented** as an estimate at a distance |
| 1570 | Manila | that boat "surrounded by more than five hundred Moro praus and other large ships full of armed men, bowmen, and lancers" | 1570 relation | **Documented**, but this is the relation's own scale-setting rhetoric |
| 1570 | Manila | thirteen artillery pieces, small and large, taken from the town | 1570 relation | **Documented** |
| 1570 | Lubang | a rock held by "about three hundred warriors" | 1572 conquest narrative | **Documented** |
| 1570 | Lubang | two principal forts, square, with "ten or twelve culverins on each side", a wall two *estados* high and a water-filled ditch two and a half *brazas* deep | 1572 conquest narrative | **Documented** |
| 1571 | Bangkusay | "twenty or thirty of their boats, with one or two culverins in each boat"; five or six culverins in a shore fort | 1572 conquest narrative | **Documented** |
| 1571 | Bangkusay | two hundred natives taken prisoner, "and later they captured two or three hundred more" | 1572 conquest narrative | **Documented** as a Spanish tally |
| 1572 | Cainta | a palisaded fort taken; "of the Indians there were slain, men and women, four hundred persons" | 1572 conquest narrative | **Documented** as a Spanish tally |

Reading these together:

- **A single leader's committed force is a few hundred.** Three hundred
  warriors on a rock, a fort garrison of similar size, twenty to thirty boats.
- **The thousand-plus figures are coalitions or defenses of home ground.**
  Mactan's fifteen hundred came from an island resisting an attack on it, and
  Pigafetta explicitly notes the defenders asked for a delay "so that they might
  have more men" — the force was still assembling when the fight started.
- **Three divisions is the only formation structure named in the corpus.**
  Pigafetta reports the Mactan force "had formed in three divisions ... two
  divisions on our flanks and the other on our front". This is a documented
  tactical arrangement. It is not evidence of a standing three-part
  organization, a fixed division size, or a name for such a body.

**Not documented anywhere in this set:** a muster roll, a levy quota, a
service term, a unit strength, a stated span of control, or any number
describing how many followers a rank was supposed to have.

## 6. The boat is the organizational unit

For coastal and riverine societies in this corpus, the vessel does more
organizational work than any land formation. It sets who travels together, who
arrives together, and who owes the same person.

**Documented.** Morga describes the largest type in detail:

> Some are so long that they can carry one hundred rowers on a side and thirty
> soldiers above to fight. The boats commonly used are barangays and vireys,
> which carry a less crew and fighting force.

He also describes the structure that makes this work: a bamboo platform or
gangway above the rowers "upon which the fighting-men stand, in order not to
interfere with the rowing of the oarsmen", with the number of men on the
gangway set by the capacity of the vessel, and outriggers along each side that
keep the hull from capsizing or sinking even when swamped. Rowing was timed to
sung refrains "by which they understand whether to hasten or retard their
rowing".

Two things follow, and both are load-bearing:

1. **The largest attested crew is around 230 people, of whom only about 30 are
   fighting men on the platform.** The 1570 relation's "three or four hundred
   fighting men and rowers" for a single Manila war boat is in the same order of
   magnitude, and its phrasing — fighting men *and rowers*, counted together —
   matches Morga's division of the crew.
2. **The rowers are not a separate service arm.** Plasencia's maharlica row for
   the dato; his aliping namamahay row when the dato goes beyond the island;
   Loarca's timagua "must go to ply the oar, and to carry his weapons for the
   defense of the vessel". The same free man is the oarsman and the fighter. Any
   model that splits "sailors" from "warriors" as permanent classes is inventing
   a distinction the sources do not support.

### Terminology caution

*Barangay* in these documents is both a social group and a vessel type, and
Spanish writers also use *virey*, *prau*, *banca*, *joanga*, *lapis*, and
*tapaque* — some of them loanwords picked up elsewhere in the region and applied
here. Morga's *caracoa* is the large type quoted above. The term *karakoa* is
therefore attested in early-seventeenth-century Spanish usage for a Philippine
vessel class; the precise indigenous form behind each Spanish label is not
recoverable from these texts alone. Mark any hull-specific claim
**Documented, form uncertain**.

## 7. How a force was raised, paid, and dissolved

The corpus is nearly silent on command and completely silent on drill, but it is
unusually specific about **incentives**, which is where its simulation value
lies.

**Documented.** Loarca on the raiding season and the division of spoils:

> The Indians along the coast are accustomed to set out every year on their
> plundering expeditions in the season of the bonancas, which come between the
> brisas and the vendabals. The Tinguianes set out after they have gathered
> their harvests ...

> The booty that they take, whatever it may be, belongs to the chiefs, except a
> small portion which is given to the timaguas who go with them as oarsmen. But
> if many chiefs went on a raid, the one who offered the *magaanito*, or the
> sacrifice mentioned above, received half of the booty, and the other half
> belonged to the other chiefs.

That last sentence is the closest thing in the whole corpus to a rule of
command. **Sponsorship, expressed as a ritual sacrifice, bought half the
proceeds.** It did not buy obedience, a title, or authority over the other
chiefs' followers — only the larger share. A coalition therefore had a
recognizable sponsor and an agreed split, and nothing else.

**Documented.** Morga records that following itself was earned by war record,
not fixed by birth alone:

> When any of these chiefs was more courageous than others in war and upon
> other occasions, such a one enjoyed more followers and men; and the others
> were under his leadership, even if they were chiefs.

This is the direct evidentiary basis for §11.1 and §11.2's argument that
contingent sizes should be unequal and leader-earned rather than derived by
lattice-packing, and it is the design's stated basis for rank-aware leader
selection: a leading chief did not become a stronger fighter, he became one
whom more people stood with.

**Documented.** Loarca also records that a captive taken alive was worth more
than one killed: whoever slew a captive after his surrender "must pay for him
with his own money; and if he were unable to do so he was held as a slave", and
a captured chief was well treated and ransomed at double what a friend paid.
Capture was economically preferred to killing.

**Documented.** Divination preceded departure — casting lots with a crocodile
or wild boar tooth, and knots or loops in cords, to ask "as to the result of
their wars and their journeys".

**Documented.** Plasencia's feast-before, spoils-after sequence (section 4.1)
is the Tagalog counterpart of the same logic.

**Unknown or unsupported.** Nothing in this set establishes a shouted command
vocabulary, a signal code by horn, gong, drum, or flag, a messenger
organization, a reserve, a rule for replacing a fallen leader, or a procedure
for resolving contradictory orders between allied chiefs. Morga's sung rowing
refrains are a work rhythm on one boat, not a battlefield signal system. The
deep-past track reaches
[the same conclusion from different evidence](battles/02-deep-past-forces-and-command.md#command-control-and-cohesion),
which is worth noting: two independent evidence bases both come up empty.

## 8. Defensive works and where a force stops being mobile

**Documented.** The 1572 narrative is explicit that fortification was not
universal and that its authors noticed where it began: describing the Lubang
forts, the writer says "these were the first natives whom we found with forts
and means of defense", and then describes them because they were novel.

Attested features across the Luzon campaigns:

- Manila: a town "defended by a palisade all along its front", with artillery
  standing at the gates and bombardiers with linstock in hand.
- Lubang: two square forts, ten or twelve culverins per side, a wall two
  *estados* high, a water-filled ditch two and a half *brazas* deep.
- Cainta: a fort "made of palm-tree logs", with openings cut for artillery, in
  bamboo thickets, near the lake.
- Bangkusay: a small shore fort with five or six culverins supporting the boats.
- Pampanga: natives who "retired to forts which they had built, and tried to
  resist".

**Documented.** Locally cast artillery is repeatedly recorded, both in the forts
and in the boats — consistent with the bronze *verso* evidence already recorded
in [HISTORICAL_1500s_WEAPONS.md](HISTORICAL_1500s_WEAPONS.md).

**Out of scope for the battle layer.** Hukbo's do-not list forbids terrain,
pathfinding, and any structure work before Gate 3. This section exists so that a
future terrain design starts from evidence rather than from a fantasy palisade,
not as an argument to build one now.

## 9. Vocabulary reference

Every entry below is either quoted from a sixteenth-century source or flagged
as later. Where a term would reach player-facing UI, the pair form required by
[AGENTS.md](../../AGENTS.md) §7 is proposed: Filipino name, em dash, plain
English descriptor.

| Term | As attested | Meaning in the source | Proposed pair-form label | Confidence |
| --- | --- | --- | --- | --- |
| *barangay* | Plasencia 1589; Morga | A chief's following of thirty to a hundred houses; also a vessel type | **Barangay — Chief's Following** | **Documented** |
| *dato* | Plasencia 1589; Loarca 1582 | Chief; "captains in their wars" | **Dato — War Chief** | **Documented** |
| *maharlica* | Plasencia 1589 | Tagalog free-born who serves in war at his own expense | **Maharlika — Sworn Freeman** | **Documented** (Tagalog only) |
| *timagua* | Loarca 1582 | Visayan freeman bound to a chief by mutual service and defense | **Timawa — Bound Freeman** | **Documented** (Visayan sense only; see §4.3) |
| *aliping namamahay* | Plasencia 1589 | Dependent with his own house; rows and travels with the dato | **Aliping Namamahay — Householder** | **Documented** |
| *aliping sa guiguilir* | Plasencia 1589 | Household dependent; salable | not recommended for UI | **Documented** |
| *ayuey*, *tumaranpoc*, *tomataban* | Loarca 1582 | Three graded Visayan dependent statuses, with recorded prices: ayuey two gold *taes* (about twelve pesos), tumaranpoc the same twelve pesos in rice-equivalent, tomataban one *tae* (six pesos) — see [`HISTORICAL_1500s_RANKS.md`](HISTORICAL_1500s_RANKS.md), Visayas dependency-grade table | not recommended for UI | **Documented, form uncertain** |
| *mangubas* | Morga 1609 | "to go out for plunder"; the raid itself | **Mangubat — Raid** | **Documented, form uncertain** — Morga prints *mangubas*, Rizal's note gives *mangubat* |
| *magaanito* | Loarca 1582 | The sacrifice whose sponsor takes half the booty | **Magaanito — Sponsor's Sacrifice** | **Documented, form uncertain** |
| *balanguai* | Pigafetta 1521 | The boats Humabon brought to Mactan | **Balangay — War Boat** | **Documented** |
| *caracoa* | Morga 1609 | Large oared vessel, up to 100 rowers a side plus about 30 fighters | **Karakoa — Great War Boat** | **Documented, form uncertain** |
| *virey* | 1570s relations; Morga | Smaller, quicker oared craft | not recommended for UI | **Documented, form uncertain** |
| *gat*, *lakan*, *dayang*, *maginoo* | Rizal's notes to Morga, citing Colin | Chiefly titles and address forms | **Lakan — Paramount Chief** if used at all | **Provisional reconstruction** — later attestation, see §4.4 |

### 9.1 Reconciliation with `HISTORICAL_1500s_RANKS.md` (2026-07-29)

The table above was amended on 2026-07-29 to match
[`HISTORICAL_1500s_RANKS.md`](HISTORICAL_1500s_RANKS.md), which the design
document for `RankId` treats as the controlling rank vocabulary. Each change
and its reason:

- **`Maharlika — Free Warrior` became `Maharlika — Sworn Freeman`.** "Free
  Warrior" names only the combat role. Plasencia's passage (§4.1 above) is
  explicit that the obligation runs both ways — a feast before, a share of
  spoils after — and RANKS's descriptor keeps that reciprocity in the label
  itself rather than leaving it implicit.
- **`Timawa — Sworn Follower` became `Timawa — Bound Freeman`.** Loarca's
  timagua is not merely sworn; the chief owes defense in return, and the
  freeman is free to leave a chief who fails to provide it (§4.2 above).
  "Bound" names the standing relationship — a durable, transferable bond to
  a named chief — that "Sworn" alone does not capture.
- **The `ayuey`, `tumaranpoc`, `tomataban` row moved from Documented to
  Documented, form uncertain.** This document's own §1 defines that tier as
  "the institution or obligation is attested, but its local form, its
  extent, or the exact term for it is uncertain." That is a precise
  description of this row: Loarca attests three graded dependent statuses
  and gives prices for them, but the spelling rests on a single Spanish
  transliteration with unsettled modern orthography, which is exactly the
  uncertainty the lower tier exists to flag.
- **`aliping namamahay` moved from "not recommended for UI" to a cleared
  pair-form label.** The cell previously carried no written rationale. The
  user decided on 2026-07-29 that the class is fielded, with the
  player-facing label `Aliping Namamahay — Householder` — Plasencia's
  attested full form, chosen because it names the documented fact that this
  class held its own houses, land, and gold, rather than translating
  "commoner" or another Spanish gloss.

  That labeling decision is independent of a separate, unresolved question:
  whether a household dependent was ever actually put in a battle line.
  Plasencia gives the *aliping namamahay* maritime and agricultural service
  and states the armed war obligation explicitly only for the maharlika
  (§4.1 above); nothing in the corpus rules a battle role in or out for this
  class. `HISTORICAL_1500s_RANKS.md`, "Gaps and unknowns" (lines 314-319),
  records the same caution and requires that a roster fielding this class
  say in the inspector that doing so is a reconstruction, not an attested
  fact. That inspector note is implementation scope, tracked in the
  `2026-07-29-warrior-rank.md` plan's C1 and C2 tasks, not something this
  research document can satisfy on its own.

Terms **not** to use: any rank word implying a delegated military office
(general, captain, sergeant, lieutenant, corporal), any term for a standing unit
(regiment, company, squad, platoon), and any sultanate office title in a
sixteenth-century Visayan or Tagalog context.

## 10. What the sources do not establish

Listing this explicitly, because the gap is where invented content tends to
appear:

- A standing or seasonal military service obligation with a stated term.
- Any training, drill, or practice regime.
- Any unit below the chief's personal following, or any name for one.
- Any fixed number of fighters per leader, per boat, or per settlement.
- Uniform, badge, insignia, or any visual rank marker.
- A command signal system of any kind.
- A chain of command spanning more than one barangay.
- A reserve, a rearguard, or a designated line of retreat.
- Casualty evacuation, field medicine, or an armorer attached to a force.
- Any archipelago-wide title, ladder, or institution.
- Anything at all about the internal organization of the Mindanao and Sulu
  polities in the 1500s.

Absence from this list is an evidence limit, not proof that something did not
exist. It is, however, a hard boundary on what Hukbo may assert.

## 11. What this means for Hukbo

These are research observations for a future design document, not authorized
work. Nothing here changes `Hukbo.Core` until a plan document under
`docs/plans/` says so, and none of it may introduce campaign, economy, or
diplomacy state into the battle layer.

### 11.1 The contingent concept is already the right shape, for the wrong reason

`Hukbo.Core` already carries a contingent abstraction —
`src/Hukbo.Core/Simulation/ContingentState.cs` and the persistent-contingent
movement presets — and its documented semantics are a good match for the
evidence: a behavioral mode for a group, explicitly "never a positional
assignment", with `Break` for a group that has "lost too many members to act as
one". That maps onto a following whose cohesion is personal and can fail, which
is exactly what sections 4 and 7 describe.

The **derivation** of contingents does not match. `FormationPlanner`
currently splits a faction into `clamp(isqrt(warriorCount) / 2, 1, 8)` equal
contingents with the remainder spread over the earliest ones. That is a
lattice-packing convenience with no historical content: it produces equal
groups, a count that grows with the square root of the army, and no notion of
who led which group.

The evidence suggests the opposite on both axes:

- **Contingent count is set by how many chiefs joined**, not by total headcount.
  Mactan's three divisions and Bangkusay's twenty to thirty boats are both
  consistent with a small number of separately led groups.
- **Contingent sizes are unequal**, because barangays ranged from under thirty
  to a hundred houses and chiefs differed in wealth and standing.

A future scenario-level contingent roster — explicit sizes, unequal, drawn from
the documented thirty-to-a-hundred band — would be more historically defensible
than the current derivation and would cost nothing in determinism, since it is
scenario input rather than derived state. Any such change is a **new movement
preset version** with new golden expectations, under the rules in
`SIMULATION-GAME-STANDARDS.md` §4.

### 11.2 Cohesion should be leader-local and exit should be possible

Loarca's timagua could leave a chief who failed to defend him, and Plasencia's
maharlica could move between barangays for a price. Both point the same way: the
thing that holds a group together is its relationship to one leader, not to the
faction. A cohesion model keyed to a contingent's own leader, with failure
modeled as the group ceasing to act as one rather than as individual panic, is
better supported than a faction-wide morale bar.

`ContingentState.Break` already expresses "this group has stopped acting as one".
What is missing is any notion of a leader whose loss or survival matters. Adding
one is a real design question with determinism consequences, and it belongs in a
design document, not in a research file.

### 11.3 Force scale for scenarios

The recorded engagements support the scale the simulation already targets. A
plausible, defensible band for a battle scenario:

| Scenario size | Historical analogue | Confidence |
| --- | --- | --- |
| 50-150 per side | One chief's following; a fort garrison; the Lubang rock | **Provisional reconstruction** from §3 and §5 |
| 200-400 per side | A few allied chiefs; a large war boat's complement | **Provisional reconstruction** |
| 500-1,500 per side | A coalition defending its own island, as at Mactan | **Documented** at the upper end for one event only |

The 200-agent canonical gate workload therefore sits comfortably inside the
documented range, and the 500-agent stress target named in Gate 3 sits at the
low end of the coalition band rather than in fantasy territory. That is worth
recording: the simulation's existing scale is historically reasonable, and there
is no evidentiary pressure to make battles larger.

### 11.4 Things to keep out

- **No graded military-office hierarchy.** There is no attested captain,
  sergeant, lieutenant, or corporal, and no named unit below a chief's
  personal following; inventing one would violate §7 of `AGENTS.md` on the
  same grounds that excluded *panabas*. This is a different claim from the
  social and legal standing catalogued in §4 above — dato, maharlika,
  timagua, and the dependent grades — which is documented, not invented.
  `Hukbo.Core`'s `RankId` enum carries that social and legal standing only;
  it is not a delegated military office and does not contradict this
  section.
- **No signal, order, or command-radius system** presented as historical. Any
  such mechanic is a game-design hypothesis and must be commented as one, in the
  same style as the provisional shield multiplier already in the code.
- **No booty, ransom, capture, or reward economy in `Hukbo.Core`.** Sections 4
  and 7 describe a reward system that is genuinely load-bearing for why people
  fought, and it is genuinely interesting — and it belongs to the future
  campaign layer, which consumes `BattleOutcome`. The battle core never learns
  what a barangay is.
- **No sailors-versus-warriors split.** Section 6 rules it out.

### 11.5 The spectator test

`SIMULATION-GAME-STANDARDS.md` §10 asks whether a spectator can discover an
effect without reading source code. For the material in this document, the
answer shapes what is worth building at all. Unequal contingent sizes are
visible on screen the moment they are drawn. A leader whose death breaks a
contingent is visible. A sponsorship share is not visible in a battle at all,
which is a further reason it belongs to the campaign layer.

## 12. Open questions for a later research pass

1. **Mindanao and Sulu in the 1500s.** Section 4.5 is the weakest part of this
   document. A dedicated pass against sources of the right date is required
   before any Mindanao faction is proposed.
2. **Alcina and Colin.** The seventeenth-century Jesuit accounts contain far
   more Visayan social vocabulary than Loarca does, at the cost of being
   fifty to a hundred years later than the target period. A pass over them,
   with every term tagged by attestation date, would fill most of the gaps in
   section 9 — and would need the century-gap rule from `AGENTS.md` §7 applied
   term by term.
3. **The Boxer Codex text.** [HISTORICAL_1500s_WEAPONS.md](HISTORICAL_1500s_WEAPONS.md)
   uses the Codex for silhouette and color. Its accompanying text also
   describes social ranks, and it falls inside the target window. It has not
   been read for this document.
4. **Population baselines.** Loarca and the early tribute assessments give
   settlement and island populations that would let section 3's arithmetic move
   from provisional reconstruction toward something firmer.
5. **Whether "three divisions" recurs.** Mactan is the only engagement in this
   set where a formation is described. If a second independent attestation of a
   three-part arrangement exists, that changes what section 5 can claim.

## Bibliography

All Blair and Robertson volumes cited below are the Project Gutenberg
transcriptions of *The Philippine Islands, 1493-1898* (Cleveland: A. H. Clark,
1903-1909), edited by Emma Helen Blair and James Alexander Robertson.

1. Anonymous. 1570. "Relation of the Voyage to Luzon." In *The Philippine
   Islands*, [Volume III, 1569-1576](https://www.gutenberg.org/ebooks/13616).
   Accessed 2026-07-28.
2. Anonymous. 1572. "Conquest of the Island of Luzon." In *The Philippine
   Islands*, [Volume III, 1569-1576](https://www.gutenberg.org/ebooks/13616).
   Accessed 2026-07-28.
3. Anonymous. 1589. "Instructions Regarding the Customs Which the Natives of
   Pampanga Formerly Observed in Their Lawsuits." In *The Philippine Islands*,
   [Volume XVI, 1609](https://www.gutenberg.org/ebooks/15157). Accessed
   2026-07-28.
4. Loarca, Miguel de. 1582. *Relacion de las Yslas Filipinas*. In *The
   Philippine Islands*, [Volume V,
   1582-1583](https://www.gutenberg.org/ebooks/16501). Accessed 2026-07-28.
5. Morga, Antonio de. 1609. *Sucesos de las Islas Filipinas*, with the
   annotations of José Rizal. In *The Philippine Islands*, [Volume XVI,
   1609](https://www.gutenberg.org/ebooks/15157). Accessed 2026-07-28.
6. Pigafetta, Antonio. c. 1525. *Primo Viaggio Intorno al Mondo*. In *The
   Philippine Islands*, [Volume XXXIII,
   1519-1522](https://www.gutenberg.org/ebooks/42884). Accessed 2026-07-28.
7. Plasencia, Juan de. 1589. "Customs of the Tagalogs." In *The Philippine
   Islands*, [Volume VII,
   1588-1591](https://www.gutenberg.org/ebooks/13701). Accessed 2026-07-28.
8. Sande, Francisco de. 1578-1579. "Expeditions to Borneo, Jolo, and Mindanao."
   In *The Philippine Islands*, [Volume IV,
   1576-1582](https://www.gutenberg.org/ebooks/12635). Accessed 2026-07-28.

Secondary works consulted for framing but not quoted, and not relied on for any
claim above:

9. Junker, Laura Lee. 1999. [*Raiding, Trading, and
   Feasting: The Political Economy of Philippine
   Chiefdoms*.](https://www.jstor.org/stable/j.ctt6wr1cq) Honolulu: University
   of Hawai'i Press. Cited in full in
   [Depth 2: Forces and command](battles/02-deep-past-forces-and-command.md).

Recommended for the next pass, and deliberately **not** used here because it
has not been read against the primary text term by term:

10. Scott, William Henry. 1994. *Barangay: Sixteenth-Century Philippine Culture
    and Society*. Quezon City: Ateneo de Manila University Press. The standard
    synthesis of exactly this subject. Any future use must keep its
    reconstructions distinguishable from the primary attestations recorded
    above, because much of its Visayan rank vocabulary rests on
    seventeenth-century sources.
