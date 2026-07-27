# Precolonial Philippine Battles Research Design

## Goal

Create a sourced research set that explains warfare in the Philippine
archipelago before and during early Spanish contact, progressing from broad
warfare systems to individual combat behavior, and translates the findings
into cautious planning inputs for Hukbo.

## Audience and intended use

The primary readers are future planning and implementation agents working on
Hukbo. The documents must let a reader answer:

- why and where conflicts occurred;
- how forces were raised, led, supplied, and coordinated;
- what is actually known about large-group deployment and small-unit tactics;
- how individual fighters used weapons, shields, movement, and judgment; and
- which skills, attributes, tactical states, and agent rules are defensible as
  game-design inputs.

The research is not an implementation plan and does not authorize combat-model
changes.

## Historical scope

The study uses two explicitly separated evidence tracks:

1. **Deep past and pre-contact warfare** — evidence before sustained Spanish
   observation, using archaeology, material culture, linguistics, regional
   comparison, and cautious inference.
2. **Early-contact warfare** — approximately the sixteenth and early
   seventeenth centuries, using contemporary or near-contemporary accounts
   alongside modern scholarship.

The term "Filipino" is used only as a convenient modern umbrella. The research
must identify regional societies, polities, and source contexts rather than
projecting a single uniform national fighting system backward in time.

## Four-depth model

Each historical track progresses through the same four levels:

1. **Overall warfare system** — motives, political context, conflict scale,
   terrain, maritime and land settings, strategic objectives, and battle
   termination.
2. **Forces and command** — mobilization, leadership, social roles, force
   composition, weapons, protection, communications, logistics, and cohesion.
3. **Formations and tactics** — deployment, battlefield geometry, approach,
   missile exchange, shock or melee transition, flanking, ambush, fortification,
   pursuit, withdrawal, and small-unit cooperation.
4. **Individual combat** — perception, distance management, footwork, weapon
   and shield interaction, one-versus-one and many-versus-one behavior,
   training, courage, fatigue, injury, and historically supportable attributes.

Depth means increased analytical resolution, not increased certainty. The
deepest claims may have the least direct evidence and must be labeled
accordingly.

## Document architecture

Create:

```text
docs/research/battles/
├── README.md
├── 01-deep-past-overall-warfare.md
├── 02-deep-past-forces-and-command.md
├── 03-deep-past-formations-and-tactics.md
├── 04-deep-past-individual-combat.md
├── 05-early-contact-overall-warfare.md
├── 06-early-contact-forces-and-command.md
├── 07-early-contact-formations-and-tactics.md
├── 08-early-contact-individual-combat.md
└── 09-gameplay-planning-synthesis.md
```

`README.md` is the navigation and evidence guide. The eight historical files
form two mirrored four-depth tracks. The synthesis maps findings to possible
Hukbo concepts without converting inference into fact.

## Evidence protocol

Every material claim should be traceable to an inline citation or a clearly
scoped source note. Sources are prioritized as follows:

1. contemporary primary accounts in reliable editions or translations;
2. peer-reviewed scholarship and academic-press books;
3. museum, archaeological, and institutional publications;
4. carefully identified secondary summaries used only for orientation.

Each substantial conclusion receives one evidence label:

- **Attested** — directly stated or materially demonstrated in a relevant
  source.
- **Strong reconstruction** — supported by multiple independent evidence
  types or close contextual agreement.
- **Plausible inference** — consistent with the evidence but not directly
  demonstrated.
- **Unknown or unsupported** — not presently recoverable from the evidence and
  unsuitable for a historical claim.

Primary accounts must be source-criticized for author position, translation,
colonial incentives, hostile description, geographic limits, and chronology.
Modern Filipino martial arts are not treated as unchanged survivals unless a
specific historical continuity is demonstrated.

## Research content requirements

The set must address:

- raids, feuds, alliance warfare, defense, conquest, prestige, captives, and
  resource control;
- maritime, riverine, coastal, forest, settlement, and open-ground combat;
- command authority, war leaders, retainers, levies, allied contingents, and
  force-size uncertainty;
- signaling, reconnaissance, surprise, logistics, camps, fortifications, and
  withdrawal;
- whether evidence supports formal formations, loose groupings, files,
  shielded bodies, missile screens, boarding parties, or other arrangements;
- one-versus-one combat as a battlefield event rather than an assumed formal
  duel tradition;
- weapon categories already represented by
  `docs/research/HISTORICAL_1500s_WEAPONS.md`;
- attributes and skills such as awareness, cohesion, discipline, aggression,
  courage, fatigue tolerance, mobility, weapon familiarity, shield use, and
  tactical judgment; and
- regional variation across Luzon, the Visayas, Mindanao, and the Sulu zone
  wherever evidence permits.

## Gameplay translation boundary

The synthesis separates four layers:

1. historical observation;
2. interpretation;
3. proposed simulation abstraction; and
4. tuning parameter.

It may propose concepts such as command radius, cohesion, morale, formation
state, ambush posture, target selection, weapon reach, shield coverage,
fatigue, and retreat thresholds. It must not prescribe final numeric values,
claim a universal precolonial doctrine, or make speculative mechanics appear
historically attested.

## Verification

The research is ready only when:

- all ten files exist and follow the mirrored four-depth structure;
- every major historical assertion has a usable citation;
- source links and bibliographic identities resolve;
- direct evidence and inference are visibly distinguished;
- deep-past claims do not silently borrow early-contact descriptions;
- regional and chronological limits are stated;
- formations are described only at the precision the evidence supports;
- the synthesis maps back to specific findings and identifies uncertainty;
- no unrelated repository file is modified; and
- an independent reader can distinguish history, reconstruction, and design
  proposal without prior conversation context.

## Risks and controls

- **Evidence scarcity:** state unknowns instead of manufacturing detailed
  formations.
- **Colonial-source bias:** compare accounts, identify perspective, and avoid
  taking labels or force estimates at face value.
- **Anachronism:** keep deep-past, early-contact, later colonial, and modern
  martial-arts evidence separate.
- **False uniformity:** name the society and region attached to each example.
- **Gameplay overreach:** keep mechanics in the synthesis and link them to
  evidence labels.
- **Repository overlap:** create only the approved research directory and
  planning artifacts; preserve all unrelated working-tree changes.
