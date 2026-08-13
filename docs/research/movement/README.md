# Weapon-Relative Movement Research Program

Research date: 2026-07-29

Status: research requirements and evidence contract; no implementation
authorization

## Purpose

This directory defines the research basis for weapon- and shield-relative
movement in Hukbo. It asks how a warrior's equipment may affect movement as:

- an individual;
- a member of a nearby cooperating group;
- a member of a contingent reacting to the whole battle; and
- one participant in battles with different ally and enemy counts and
  loadout compositions.

The program covers the four implemented weapon identities and the implemented
shield identity. It is a 12-file set: this research index, five equipment
research documents, one plan index, and five equipment implementation plans.

| Research document | Plan document | Implemented identity | Player-facing descriptor |
| --- | --- | --- | --- |
| [`kampilan.md`](kampilan.md) | pruned 2026-08-07 | `WeaponId.Kampilan` | Kampilan — Great Blade |
| [`wasay.md`](wasay.md) | pruned 2026-08-07 | `WeaponId.Wasay` | Wasay — War Axe |
| [`kalis.md`](kalis.md) | pruned 2026-08-07 | `WeaponId.Kalis` | Kalis — Thrusting Blade |
| [`itak.md`](itak.md) | pruned 2026-08-07 | `WeaponId.Itak` | Itak — Work Blade |
| [`tall-hardwood-shield.md`](tall-hardwood-shield.md) | pruned 2026-08-07 | `ShieldId.TallHardwood` | Tall Hardwood Shield |

The companion plan-side movement README, pruned on 2026-08-07,
owns program-wide sequencing, shared code changes, dependencies, and
verification. Equipment plan files own only the implementation tasks derived
from their corresponding research files. Neither research nor plans authorize
implementation.

The shield research is layered. It establishes shared movement constraints for
the shield and separate specializations for:

- Kalis + Tall Hardwood Shield; and
- Itak + Tall Hardwood Shield.

The implemented identity is retained for traceability. `TallHardwood` is not a
historical claim that all relevant shields were tall, made of hardwood, or
shared one construction.

## Current code baseline and version boundary

The source of truth at the research date distinguishes the live default from
the planning target:

- `Scenario.CombatPreset` defaults to
  `CombatPresetId.PrecolonialPhilippinesV2`.
- V2 fields the six loadouts used by this research matrix: the four solo
  weapons plus Kalis + Tall Hardwood Shield and Itak + Tall Hardwood Shield.
- `PrecolonialPhilippinesV3` is already registered in the current source, is
  non-default, and fields the four weapons solo-only. It is not an unused
  version name available for a future movement preset.
- The approved planning target explicitly changes `Scenario.CombatPreset`
  from V2 to the already-registered V3 in its own attributable task. It does
  not modify either combat preset or reuse either identity. Shield-aware
  movement remains exercised through explicitly selected V2 scenarios.
- `MovementPresetId.PersistentContingentsV4 = 4` is registered and is the
  current movement default. Weapon-relative movement remains planned work; its
  new identity must use the next available movement-preset value after V4 and
  must not silently change any existing preset or content hash.

The six-loadout matrix intentionally evaluates the current default V2 surface
and both implemented shield pairings. The planned default switch changes only
which existing combat preset `Scenario` selects automatically; it does not
delete V2, replace V3, or change either preset's replay/content-hash contract.

## Approved product decisions

The following decisions govern every document in this directory.

1. Movement is the full lifecycle: formation placement, approach, spacing,
   engagement, attack-linked commitment, recovery, disengagement, regrouping,
   pursuit, and retreat.
2. Movement decisions use both ally/enemy headcount and loadout composition.
3. Immediate individual behavior uses locally perceived information.
   Contingent posture may also use global surviving totals and composition.
4. The contingent selects broad posture, frontage, cohesion, and tactical
   withdrawal. The warrior selects equipment-relative footwork inside that
   posture.
5. Numerical disadvantage may cause tactical disengagement, yielding ground,
   seeking allies, brief refusal of a poor engagement, or protecting a flank.
   It does not introduce morale, panic, rout, surrender, or battlefield flight.
6. All warriors retain a shared human locomotion baseline. Equipment may make
   modest changes to acceleration, turning, lateral or backward travel, and
   movement during commitment or recovery. Numerical advantage never grants a
   speed boost.
7. Authoritative facing may be proposed for locomotion. Facing does not alter
   attack acceptance, shield interception, hit location, or damage in this
   movement scope.
8. Movement must work in homogeneous and mixed battles. It must not require an
   immediate mixed-contingent rewrite.
9. The implementation plan will switch the default combat preset from V2 to
   the already-registered, shieldless V3. Shield-aware movement remains
   required and testable through explicit V2 scenarios, but shielded loadouts
   must not be added to V3.
10. Balance means role viability, not equal duel win rates. Every loadout
    should contribute in at least one individual and one group context.
11. Research uses evidence-led reconstruction. Later Philippine evidence,
    living combat traditions, experimental archaeology, combat sports, and
    biomechanics may suggest hypotheses, but they never become silent proof of
    sixteenth-century Philippine practice.
12. Research scenarios are explicit; implementation should prefer continuous
    deterministic ratios and a small number of stable thresholds over bespoke
    behavior for every named count.

## Historical boundary

There was no single Philippine martial culture in the 1500s. Evidence differs
by island, language, polity, decade, observer, and source purpose. Spanish
accounts were written inside invasion and colonial projects. Later
ethnographies describe communities separated from the target period by
centuries and substantial political change. Modern Filipino martial arts are
living practices, not an unbroken technical transcript of 1521.

The research must therefore keep four things separate:

1. historical observation;
2. interpretation of physical or social constraints;
3. simulation behavior proposed for Hukbo; and
4. numerical tuning.

No gameplay number gains historical authority because it was inspired by a
historical source.

## Evidence labels

Use Hukbo's three required labels on every load-bearing historical claim.

### Documented

A source directly describes or pictures the claim within its explicitly
stated place and date. The ledger must always state that date; a later or
modern observation is documented only for its own time and context.

This label does not make an observation universal. Pigafetta's account of
Mactan in 1521 documents Mactan in that encounter, not every Philippine polity
throughout the century.

### Documented, form uncertain

The source attests the broad equipment class or action, but the local name,
object form, construction, grip, posture, sequence, or regional distribution
is unresolved.

### Provisional reconstruction

The claim is a bounded synthesis from later Philippine evidence, material
affordance, modern practice, cross-cultural experiment, biomechanics, or game
design. Its source distance and transfer limit must be stated.

### Unknown or unsupported

Use this additional research result when no source supports a proposed
historical claim. It is not a fourth player-facing evidence badge. It is an
instruction not to historicize the idea.

Examples include:

- fixed ranks and rank depth;
- a shield wall;
- synchronized army-wide advance;
- formal one-on-one duel rules;
- a universal triangular footwork system;
- exact movement speed, turning rate, or engagement radius;
- equipment-specific ally/enemy thresholds; and
- an archipelago-wide weapon doctrine.

---

**V10 gameplay divergence (2026-08-11).** `MovementPresetId.BattlefieldRealismV10`
(`docs/plans/2026-08-11-battlefield-realism-design.md`) places shield bearers at
the forward-most slots of their own contingent, one of the items this list
names as unsupported. The finding is unchanged: nothing above becomes source
support for that placement. As a research claim, forward placement is none of
Documented, Documented (form uncertain), or Provisional reconstruction — it is
a gameplay model, adopted for legibility, and the list above does not support
it as history.

**V13 gameplay divergence (2026-08-14).** `MovementPresetId.CohortLateralSpreadV13`
(`docs/plans/2026-08-14-cohort-lateral-spread-design.md`) spreads weapon-cohort
groups laterally across a team's own frontage in place of collecting them
toward one edge; like V10's forward placement above, this is a gameplay
legibility choice, not a claim about a historical formation.

## Source hierarchy

Research should descend this hierarchy and record when a higher tier is silent.

| Priority | Source class | Permitted use |
| --- | --- | --- |
| 1 | Sixteenth-century Philippine primary or near-eyewitness source | Direct observation within its exact place, date, observer, and encounter |
| 2 | Late-sixteenth-century visual source with source criticism | Broad silhouette, carried pairing, and posture; not hidden construction or motion sequence |
| 3 | Early-seventeenth-century vocabulary or relation within the hundred-year naming rule | Terminology and near-period equipment context after page-level verification |
| 4 | Scholarly archaeology, history, and museum object catalog | Material bounds, provenance, political context, and evidence gaps |
| 5 | Later Philippine ethnography and oral/epic record | Explicitly later comparative behavior, never automatic continuity |
| 6 | Living Filipino martial practice | Named modern doctrine and testable hypotheses only |
| 7 | Cross-cultural experimental archaeology, HEMA, or combat sport | Method, physical possibility, and comparison only |
| 8 | General biomechanics and team-sport research | Human movement or measurement method only |

Wikipedia, enthusiast summaries, commercial weapon sellers, AI-generated
encyclopedias, and unattributed videos may locate sources but may not carry a
claim.

## Claim ledger

Every research document must contain a ledger with at least these fields:

| Field | Requirement |
| --- | --- |
| Claim ID | Stable document-local identifier |
| Claim | One falsifiable statement |
| Place and date | Exact scope, or `not established` |
| Source and locator | Page, folio, plate, object ID, section, or stable fragment |
| Source class | One hierarchy class above |
| Evidence label | Required Hukbo label |
| Transfer limit | What the source does not establish |
| Movement consequence | Observation, candidate, or rejected extrapolation |

A modern or later observation may therefore be `Documented` at its own date
while the proposed 1500s transfer remains `Provisional reconstruction`. The
ledger must state both rather than laundering the source date through the
label.

## Shared movement vocabulary

These terms are simulation vocabulary, not claimed historical drill names.

| Term | Meaning |
| --- | --- |
| Contingent posture | Broad advance, hold, yield, regroup, pursue, or withdraw preference selected above the individual |
| Formation placement | A preferred local position relative to contingent anchor, allies, frontage, and threats; never a rigid formation slot |
| Approach | Travel from awareness distance toward a tactically useful range |
| Preferred distance | Equipment-relative separation that preserves a useful action while managing the opponent's action |
| Engagement | A local state in which at least one combatant can plausibly threaten the other after movement |
| Commitment | Movement coupled to beginning an attack; may restrict redirection without changing attack timing or damage |
| Recovery | Movement needed to regain useful facing, spacing, and options after commitment |
| Refusal | Briefly declining to enter a poor local engagement while remaining in battle |
| Tactical disengagement | Yielding or moving obliquely to break immediate pressure and seek a safer local geometry |
| Regroup | Rejoining allies or the contingent anchor; not a morale recovery |
| Pursuit | Following an opponent who yields, subject to cohesion and overextension limits |
| Threat bearing | Direction from which a perceived enemy can plausibly enter effective range |
| Free lane | Space in which the warrior can move or commit without intersecting a teammate's body or required weapon clearance |

## Layered decision model

### Contingent layer

The contingent may consider:

- all surviving ally and enemy totals;
- surviving loadout composition;
- distance and cohesion of its own members;
- broad frontage pressure; and
- whether the faction is advancing, holding, yielding, regrouping, pursuing,
  or withdrawing.

Global information may influence posture, never exact individual footwork or
perfect knowledge of a remote enemy's position.

### Individual layer

The warrior may consider only perceived local information:

- nearest and second-nearest threat;
- threat bearings;
- local ally and enemy counts;
- local ally and enemy loadout composition;
- distance to allies and contingent anchor;
- current facing and turn cost;
- free movement and weapon-clearance lanes;
- engagement, commitment, and recovery state; and
- a reachable exit or regroup route.

The individual applies equipment-relative behavior inside the contingent
posture. A withdrawal posture does not force every warrior to turn and run in
the same tick; an advance posture does not force a warrior into a blocked or
outnumbered engagement.

## Count and composition model

Counts and composition are different inputs.

- **Headcount** estimates local numerical pressure.
- **Composition** estimates which ranges, clearances, and commitment risks are
  present.
- Composition must not be converted into fictional extra people.
- A locally perceived unit counts once regardless of weapon.
- Equipment-relative risk may break ties between otherwise similar local
  ratios.

Research should use concentric contexts without assuming final radii:

> **Provisional reconstruction:** Gameplay tuning; no historical measurement.

| Context | Candidate purpose | Starting band, wholly provisional |
| --- | --- | --- |
| Immediate-contact neighborhood | Threat bearings, body congestion, and commitment clearance | 2–3 body diameters |
| Local-support neighborhood | Ally support, isolation, local ratio, and regroup target | 5–8 body diameters |
| Contingent context | Anchor, broad posture, and local cohesion | Scenario-defined contingent membership |
| Global context | Surviving totals and composition | Whole battlefield, contingent layer only |

Candidate ratio bands for experiments, not historical facts or final code:

> **Provisional reconstruction:** Gameplay tuning; no historical measurement.

| Local perceived ally:enemy ratio | Research condition |
| --- | --- |
| at least 1.25–1.50 | Local advantage |
| approximately 0.80–1.25 | Contested |
| at most 0.67–0.80 | Local disadvantage |
| at most 0.40–0.50 | Severe local disadvantage |

The later plan should test hysteresis: entering tactical disengagement at a
lower ratio than the ratio required to leave it. That avoids oscillation at a
threshold. Exact bands and persistence durations have no historical
confidence.

## Required scenario matrix

The research corpus uses six loadouts:

1. Kampilan;
2. Wasay;
3. Kalis;
4. Itak;
5. Kalis + Tall Hardwood Shield; and
6. Itak + Tall Hardwood Shield.

For canonical matrix identifiers, use `KP`, `WA`, `KA`, `IT`, `KS`, and `IS`
in that order. The 21 unordered pair IDs are:

| ID | Pair | ID | Pair | ID | Pair |
| --- | --- | --- | --- | --- | --- |
| 01 | KP–KP | 08 | WA–KA | 15 | KA–IS |
| 02 | KP–WA | 09 | WA–IT | 16 | IT–IT |
| 03 | KP–KA | 10 | WA–KS | 17 | IT–KS |
| 04 | KP–IT | 11 | WA–IS | 18 | IT–IS |
| 05 | KP–KS | 12 | KA–KA | 19 | KS–KS |
| 06 | KP–IS | 13 | KA–IT | 20 | KS–IS |
| 07 | WA–WA | 14 | KA–KS | 21 | IS–IS |

This same ordered list serves as the complete set of unordered two-member team
compositions, `T01` through `T21`. The exhaustive unordered 2v2 matrix is every
`(Ti, Tj)` for `1 <= i <= j <= 21`: `21 × 22 / 2 = 231` matchups. A generator
must preserve this ordering so omissions and duplicates are mechanically
detectable.

### One versus one

Cover all 21 unordered loadout pairings, including the six mirrors. Record:

- starting and sustained distance;
- facing and turn demand;
- approach direction;
- commitment and recovery movement;
- disengagement opportunity;
- pursuit tendency; and
- whether the loadout has a viable role without requiring an even win rate.

One-versus-one is a temporary local geometry inside battle, not a formal duel.

### Two versus two

There are 21 unordered two-member team compositions. A fully crossed,
unordered team-versus-team matrix therefore contains 231 composition
matchups, including mirrors. Research documents need not pretend that
historical sources describe those cells. They must define hypotheses and
coverage tags so the later plan can generate them mechanically.

Each cell should record:

- homogeneous or mixed team;
- which ally provides pressure, coverage, or space;
- whether both allies compete for one lane;
- whether either unit becomes isolated;
- loadout-specific spacing;
- local superiority created during the exchange; and
- deterministic outcome and movement observations to collect.

### Asymmetric small groups

At minimum:

- 1v2;
- 2v3; and
- 3v5.

The disadvantaged side should be studied for exit preservation, threat
stacking, refusal, yielding, and regrouping. The advantaged side should be
studied for distinct approach bearings, ally clearance, overconcentration, and
over-pursuit.

### Mixed small groups

Use 4v4, 5v5, and 8v8 curated compositions to study:

- loadout cooperation;
- local composition changes;
- free lanes;
- teammate obstruction;
- flank exposure;
- group splitting and recombination; and
- role viability beyond duels.

### Contingent and mass battles

Use multiple internally coherent contingents rather than one army-wide rigid
formation. Cover:

- local advance, hesitation, yield, and regroup;
- irregular frontage;
- stronger within-contingent than cross-contingent cohesion;
- weapon-clearance effects on density;
- mixed battles without requiring mixed contingent ownership;
- default 100v100; and
- stress 250v250.

Mass scenarios validate emergent local geometry, determinism, congestion, and
performance. They do not prove a historical doctrine.

## Shared observations

Every future experiment should report, where applicable:

- local ally and enemy count;
- local loadout composition;
- nearest and second-nearest threat distance;
- number and spread of threat bearings;
- distance to contingent anchor;
- accepted versus rejected movement;
- blocked or unsafe free-lane attempts;
- time isolated from local allies;
- approach, commitment, recovery, disengagement, regroup, and pursuit time;
- facing change requested and accepted;
- body and equipment-clearance pressure;
- posture transitions;
- state hash, event hash, winner, and ordered event stream; and
- runtime and allocation measurements for large scenarios.

These are research requirements. They do not authorize new diagnostics or
simulation state.

## Numerical tuning contract

Research files may provide:

- an evidence-backed direction, such as “a large off-hand object may constrain
  turning more than unloaded travel”;
- a starting range;
- one candidate default; and
- a calibration procedure.

Every number must be labeled:

> **Provisional reconstruction:** Gameplay tuning; no historical measurement.

Prefer dimensionless multipliers and body- or weapon-relative distances over
false historical units. Candidate defaults must be rejected if they:

- create artificial speed from numerical advantage;
- erase the shared human locomotion baseline;
- cause threshold oscillation;
- make one loadout universally dominant;
- require perfect individual battlefield knowledge;
- create rigid formations;
- introduce nondeterministic ordering; or
- change damage, attack timing, hit location, or shield interception.

## Required structure of each equipment research file

1. purpose and scope;
2. identity and terminology boundary;
3. headline evidence finding;
4. research questions;
5. source criticism;
6. claim ledger;
7. physical affordances and uncertainties;
8. full individual movement lifecycle;
9. local count and composition behavior;
10. 1v1, 2v2, asymmetric, small-group, contingent, and mass coverage;
11. mixed-loadout cooperation;
12. candidate tuning ranges and calibration;
13. role-viability criteria;
14. rejected extrapolations;
15. evidence gaps; and
16. source register with direct locators.

## Shared sources

### Sixteenth-century and near-primary

- Antonio Pigafetta, [*First Voyage Round the World*, Mactan
  account](https://en.wikisource.org/wiki/The_First_Voyage_Round_the_World/Pigafetta%27s_Account_of_Magellan%27s_Voyage)
  — Mactan passage beginning with the landing and three squadrons; the Library
  of Congress provides the
  [manuscript provenance record](https://www.loc.gov/item/2021667606/).
- Blair and Robertson,
  [*The Philippine Islands*, volume II](https://www.gutenberg.org/ebooks/13280)
  and
  [volume III](https://www.gutenberg.org/ebooks/13616) — Legazpi-era
  relations; translations require source criticism.
- [Boxer Codex, Lilly Library catalog record and digital
  object](https://iucat.iu.edu/lilly/8843094), catalog ID 8843094, cited as
  *Sino-Spanish codex (Boxer codex), ca. 1590*, Boxer mss. II.
- Loreto Romero, ["The Likely Origins of the Boxer
  Codex"](https://www.ehumanista.ucsb.edu/sites/secure.lsit.ucsb.edu.span.d7_eh/files/sitefiles/ehumanista/volume40/ehum40.romero_0.pdf),
  *eHumanista* 40 (2018), pp. 117–133 — governs limits on reading the
  illustrations literally.

### Philippine scholarship, museum, and later comparison

- National Museum of the Philippines,
  ["Weapons, Shields, and Armors"](https://www.nationalmuseum.gov.ph/our-collections/ethnology/weapons-and-shields/)
  — current museum synthesis; later and cross-regional, not a sixteenth-century
  technical manual.
- Herbert W. Krieger,
  [*The Collection of Primitive Weapons and Armor of the Philippine Islands in
  the United States National Museum*](https://repository.si.edu/items/c2f4a202-42a1-40bc-bb49-3b665785ff39),
  US National Museum Bulletin 137 (1926), pp. 1–128 and plates 1–21.
- Fay-Cooper Cole,
  [*The Wild Tribes of Davao District,
  Mindanao*](https://www.gutenberg.org/cache/epub/18273/pg18273-images.html),
  Field Museum Publication 170 (1913), Bagobo chapter, “Warfare.”
- Fay-Cooper Cole,
  [*The Tinguian*](https://www.gutenberg.org/files/12849/12849-h/12849-h.htm#d0e11813),
  Field Museum Publication 209 (1922), pp. 376–377, “Shields, kalasag.”
- William Henry Scott, *Barangay: Sixteenth-Century Philippine Culture and
  Society* (1994) — page-level warfare review remains an open requirement.
- Laura Lee Junker, [*Raiding, Trading, and
  Feasting*](https://uhpress.hawaii.edu/title/raiding-trading-and-feasting-the-political-economy-of-philippine-chiefdoms/)
  (1999) — political and raiding context, not weapon technique.

### Method and analogy only

- Krabben et al.,
  ["Combat as an Interpersonal
  Synergy"](https://pmc.ncbi.nlm.nih.gov/articles/PMC6851042/),
  *Sports Medicine* 49 (2019) — relative distance, orientation, and velocity
  as interaction variables.
- Gonçalves et al.,
  ["Effects of emphasising opposition and cooperation on collective movement
  behaviour during football small-sided
  games"](https://pubmed.ncbi.nlm.nih.gov/26928336/),
  *Journal of Sports Sciences* 34 (2016) — a method reference for changing
  teammate and opponent counts, not combat evidence.
- Rolf Warming,
  ["Round Shields and Body
  Techniques"](https://combatarchaeology.org/wp-content/uploads/2014/10/CA-article-Experimental-archaeology-1.pdf)
  (2015), especially pp. 10–18 — an experimental method and active/passive
  round-shield comparison from a foreign weapon system.
- Park et al.,
  ["Effect of armor and carrying load on body balance and leg muscle
  function"](https://pubmed.ncbi.nlm.nih.gov/24021525/),
  *Gait & Posture* 39 (2014), pp. 430–435 — an asymmetric-load method
  reference, not a shield-speed source.

## Program-wide unknowns

No source in the current corpus supplies:

- a Philippine fight manual from the target period;
- weapon-specific footwork sequences;
- 2v2 or larger movement drills;
- movement behavior by exact local count;
- shield-specific turn, acceleration, or speed measurements;
- universal loadout roles;
- perception radii or reaction times;
- a fixed formation doctrine; or
- reliable statistical distributions for any movement parameter.

The honest result may be a shared set of constraints with modest,
equipment-specific tuning rather than five wholly separate historical
movement systems.
