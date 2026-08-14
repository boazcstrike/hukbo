# Ranged units — design

**Archived: reference only.** This document designed the ranged-units package.
The package's plan document, archived under the title "Ranged units — plan",
records that every one of its forty-seven tasks executed, the package merged
to `main` on 2026-08-09 with the canonical gate green in full, and all eleven
smoke rows closed `PASS` on 2026-08-14. Never execute this document, never
treat its diagrams or task references as current, and never cite it as the
reason for a change. The live contract for this project remains `CLAUDE.md`,
`SIMULATION-GAME-STANDARDS.md`, and `docs/development/testing.md`; nothing in
this file overrides any of them. Archived 2026-08-14.

Status: design only. This document does not authorize implementation. Under
`CLAUDE.md` section 6 an ordered plan document has to follow it before any code
is written, and this document deliberately does not contain a task list.

Date: 2026-08-07. Worktree: `.claude/worktrees/ranged-units`.

## Contents

1. [What this is, and the authorization for it](#1-what-this-is-and-the-authorization-for-it)
2. [The three weapons](#2-the-three-weapons)
3. [The nine acceptance questions](#3-the-nine-acceptance-questions)
4. [Simulation design](#4-simulation-design)
5. [Event design](#5-event-design)
6. [Movement design](#6-movement-design)
7. [Deployment design — a gameplay model](#7-deployment-design--a-gameplay-model)
8. [Presentation design](#8-presentation-design)
9. [Audio design](#9-audio-design)
10. [The standoff work — F-A and F-B only](#10-the-standoff-work--f-a-and-f-b-only)
11. [Phase 2, deferred](#11-phase-2-deferred)
12. [Risks, and what could make this design wrong](#12-risks-and-what-could-make-this-design-wrong)

---

## 1. What this is, and the authorization for it

### What is being built

Hukbo's warriors all fight at arm's length. Every weapon in the game reaches
between ten and sixteen world units, every attack resolves inside the tick that
gathered it, and nothing in `Hukbo.Core` represents an object that is not an
agent. This package adds warriors who fight at a distance: three ranged weapons,
a projectile that takes measurable time to arrive, a movement rule that lets a
warrior deliberately stop short of contact and stay there, procedural animation
for the drawing and loosing of each weapon, sixty generated sound files, and two
narrowly scoped pieces of work on the battle-termination standoff.

It is a battle-layer change and nothing else. No campaign state, no economy, no
map generation, and no morale model is introduced, and `Hukbo.Core` gains no
reference to MonoGame, the filesystem, the wall clock, or `Hukbo.Diagnostics`.

### The authorization, recorded explicitly

Two standing prohibitions blocked this work, and both were lifted by the user on
**2026-08-07**, in the session that scoped this package.

- `SIMULATION-GAME-STANDARDS.md:27` lists "projectiles/ammo" among the deferred
  layers, alongside terrain, pathfinding, cover, morale, and the rest.
- `CLAUDE.md` section 9 says: "Start terrain, pathfinding, morale, projectile
  ammunition, persistence migrations, multiplayer, or mod APIs before the gate
  that authorizes them" — that is, do not.

**The user has lifted the projectile half of both clauses for this package.**
Projectiles and a projectile flight time are authorized. That authorization is
specific and it is not a general repeal:

- **Ammunition remains deferred.** The user's decision covers projectiles, not
  ammunition. Section 11 keeps ammunition counts, quiver sizes, and resupply out
  of scope, and both `CLAUDE.md` section 9 and the tactics research
  (`docs/research/ranged/2026-08-07-RANGED-TACTICS-EVIDENCE.md` section 15,
  invention 5) agree that nothing in the sources supports them anyway.
- **Terrain, cover, pathfinding, morale, diplomacy, needs, economy, persistent
  worlds, multiplayer, and mods stay exactly as deferred as they were.**
- **Rigid-body physics stays forbidden.** `CLAUDE.md` section 9's "distance
  checks and hitscan are the model" survives this change intact. Section 4
  explains how a projectile with a flight time is still hitscan.

`SIMULATION-GAME-STANDARDS.md:27` and `CLAUDE.md` section 9 both have to be
edited by the implementing work so that the record of what is deferred stays
true. That edit is a plan-stage task, not something this document performs.

### The four other decisions the user has already taken

These are settled inputs to the design, not open questions it re-opens.

1. **Three weapons, not four.** Bangkaw — Long Spear (in its thrown role),
   Busog — War Bow, and the Imported Arquebus. The **Sumpit — Blowgun is
   dropped**: the word appears in none of Blair and Robertson volumes II, III, V,
   or XXXIII, the name clears the hundred-year rule only on a
   Proto-Malayo-Polynesian reconstruction rather than on a dated document, and
   every sixteenth-century attestation of the weapon itself places it at Palawan
   or in the Sulu Sea rather than in the Visayan and Manila-area engagements the
   game depicts
   (`docs/research/ranged/2026-08-07-RANGED-WEAPONS-EVIDENCE.md` section 3 and
   open question 3). The bronze verso stays deferred as a crew-served weapon,
   exactly as `docs/research/HISTORICAL_1500s_WEAPONS.md:254-255` already
   reserves it.
2. **Sixty generated sound files, twenty per weapon**, with the paid ElevenLabs
   spend approved, and generated **after** the Core events that trigger them
   exist. Section 9 explains why that ordering is load-bearing rather than
   tidy-minded.
3. **Procedural pose resolvers, five phases per weapon.** Not sprite frames.
   A sprite-frame animation pipeline is backlogged at
   `docs/plans/TODO.md:41-55` by the same decision and is to be revisited only as
   its own design document.
4. **The standoff work is in scope but narrow**: candidates F-A and F-B from
   `docs/research/ranged/2026-08-07-STANDOFF-ROOT-CAUSE.md` section 6, and
   nothing else. Section 10 states what each changes and why F-C, F-D, F-E, and
   F-F are excluded.

### Phase 1 and Phase 2

The design is split, and the split is a user decision rather than a discovery.

**Phase 1**, which this document specifies in full, ships hitscan resolution with
a flight time and **no blocking of any kind**. A projectile is resolved against
the agent it was launched at. It does not check what stands between the launcher
and the target, it cannot strike an ally, and it cannot be stopped by a body in
the way.

**Phase 2**, specified only in outline in section 11, adds line of sight and
friendly fire. Both are gated behind Phase 1 shipping and being measured. The
consequence of the split is stated plainly rather than buried: in Phase 1 an
arrow passes through the friendly front rank and through every enemy except the
one it was aimed at. That is a known, deliberate, visible untruth, and section 12
records it as the largest correctness gap in the design.

### The one sentence that has to appear somewhere a player can read it

The tactics research asks for it directly
(`docs/research/ranged/2026-08-07-RANGED-TACTICS-EVIDENCE.md` section 15, "The
three honest sentences"), and it applies to this package as much as to the
product: the scenario Hukbo depicts — two comparable forces meeting on open
ground and fighting to a decision — is itself the largest invention in the
product, because the surviving record says warfare in the sixteenth-century
Philippines was raid, ambush, siege, and evacuation. Adding ranged units does not
change that and must not be presented as correcting it.

## 2. The three weapons

Every weapon below carries a confidence label from the three-tier system in
`docs/research/HISTORICAL_1500s_WEAPONS.md` and binding under `CLAUDE.md`
section 7, and a player-facing label in pair form — the Filipino name, an em
dash, and a plain English descriptor — except where no Filipino name is
attested and inventing one would be the worse offence.

### The distinction between an evidence claim and a tuning value

This section gives ranges and rates of fire. **Not one of them is a historical
measurement, and the two research documents disagree about how strongly to say
so.**

`2026-08-07-RANGED-WEAPONS-EVIDENCE.md` sections 1, 2, and 4 give numeric bands
and labels them "reasoning from the physical form and from cross-cultural
comparison", explicitly not a Philippine measurement.
`2026-08-07-RANGED-TACTICS-EVIDENCE.md` section 15 is stricter: it lists
"effective ranges in metres or game units" as invention number 6, on the grounds
that "no source gives a range for any Philippine missile weapon", and it adds
that a range value fails the spectator test on its own terms before the
historical question is reached.

**This design adopts the stricter framing.** Every range and every rate of fire
below ships as a **gameplay tuning value**, marked `PROVISIONAL` in code
comments and in tests exactly as the tall-hardwood shield multiplier already is,
and none of them may be cited back into
`docs/research/HISTORICAL_1500s_WEAPONS.md` as a measurement. What the evidence
supplies, and all it supplies, is an **ordering** and a **shape**, and those are
what the design is obliged to preserve.

### The world unit is not a metre

There is no conversion between the game's world unit and a metre, and the design
must not invent one. The default melee reach is 12 world units
(`src/Hukbo.Core/Simulation/Scenario.cs:32`) and the default body diameter is
8.5 world units (`src/Hukbo.Core/Simulation/CollisionRules.cs:72`, at
`FixedPoint.Scale = 1_024`). If a body is a man's width, one unit is about seven
centimetres and a spear reaches under a metre. If a spear is two metres, one
unit is about seventeen centimetres and a man is a metre and a half wide.
Neither reading is consistent, which means the existing scale is already a
gameplay abstraction.

Ranged reaches are therefore expressed as **multiples of the longest melee
reach**, which preserves the documented ordering without asserting a distance.
The multiples below are the design's proposal; the plan document owns
calibrating them, and section 12 records what happens if calibration says they
are wrong.

### Bangkaw — Long Spear (thrown)

**Confidence: Documented.** This is the best-attested ranged weapon in the entire
sixteenth-century Philippine record — attested not merely as present but in
action, with its projectiles named and its resupply behaviour described.

**Evidence line.** Antonio Pigafetta, present and wounded at Mactan on 27 April
1521, in the Robertson translation printed in Blair and Robertson volume XXXIII
([Project Gutenberg 42884](https://www.gutenberg.org/ebooks/42884)): "They shot
so many arrows at us and hurled so many bamboo spears (some of them tipped with
iron) at the captain-general, besides pointed stakes hardened with fire, stones,
and mud, that we could scarcely defend ourselves." And, the single most
mechanically useful sentence in the corpus for a thrown weapon: "The natives
continued to pursue us, and picking up the same spear four or six times, hurled
it at us again and again." A bamboo spear thrown into Magellan's face and a
bamboo spear wound in his arm are what stopped him drawing his sword.

**Name check: passes, on a 1521 attestation with a zero-year gap.** Pigafetta's
Visayan vocabulary of 1521 records "for Spear — bancan", which Blair and
Robertson's editors identify against Encarnación (1885) and Sanchez de la Rosa
(1895) as *bangcao*, and they do not mark the row conjectural. Two cautions
carry into the tooltip: *bangkaw* is Visayan, Mindanao, and Maranao rather than
Tagalog, where the term is *sibat*, so the label is Visayan-anchored and must
not be generalised across the archipelago; and modern Filipino martial-arts
usage disagrees with itself about whether the word means a spear or a staff,
which is community usage and carries no weight against the 1521 entry.

**Player-facing label: `Bangkaw — Long Spear`**, unchanged from
`docs/research/HISTORICAL_1500s_WEAPONS.md:40`. If the thrown role needs
distinguishing in the interface from a thrusting role, it is distinguished by a
range-category chip reading `thrown`, never by a second invented Filipino name.

**Range: the shortest of the three.** Proposed at **three times** the longest
melee reach. The evidence band the weapons research reasons to is roughly 10 to
20 m effective, with an outer limit of 25 to 35 m for a light shaft thrown for
distance rather than for effect, and it is explicit that no source gives a
throwing distance at all.

**Rate of fire: the fastest of the three.** The weapons research reasons to one
throw every three to five seconds while shafts are to hand, and observes that the
Mactan reuse line implies the practical constraint was availability on the ground
rather than the speed of the arm. Since ammunition is out of scope, the game
models the arm and not the ground.

**One documented behaviour the design deliberately does not model.** The
four-to-six-times reuse is the best-attested detail about this weapon and it is
an ammunition mechanic, which is out of scope by both the user's decision and
`CLAUDE.md` section 9. Scott's reading — quoted in
`2026-08-07-RANGED-TACTICS-EVIDENCE.md` section 5 — is that "these fine spears
were thrown only where it was possible to retrieve them", an individual economic
calculation made per throw. The game's thrown spear has no such calculation, and
that is a labelled simplification rather than an oversight.

### Busog — War Bow

**Confidence: Documented.** The weapon class is beyond argument. Its performance
is not, and the two best sources disagree about it sharply enough that the
design records the disagreement rather than resolving it.

**Evidence line.** Pigafetta, Mactan, 1521: "So many of them charged down upon us
that they shot the captain through the right leg with a poisoned arrow", and
Pigafetta's own face wound days later at Cebu. Legazpi shipped a physical
specimen — "a bow with quiver and arrows, all which they use" — from Cebu on 15
July 1567 (Blair and Robertson volume II,
[Gutenberg 13280](https://www.gutenberg.org/ebooks/13280)). Diego de Artieda's
*Relation of the Western Islands Called Filipinas* (1573, Blair and Robertson
volume III, [Gutenberg 13616](https://www.gutenberg.org/ebooks/13616)) gives the
only physical description in the corpus: bows "very strong and large, and much
more powerful than those used by the English", arrows of reed with a forward
third of the hardest wood obtainable, "not feathered", poisoned at the point.

**The recorded conflict.** The anonymous *Relation of the Conquest of the Island
of Luzon* (Manila, 20 April 1572, describing 1570) calls arrows "weapons of
little value", in the same sentence in which it says iron lance points went blunt
on a fairly good coat of mail. Artieda, three years later, says the bows exceed
English bows. Both are Spanish, both are partisan, and the reconciliation that
calls neither man a liar is that the Luzon writer is grading Philippine weapons
against his own armour — a narrow test most pre-gunpowder missile weapons fail —
while Artieda is describing the object. **Neither statement may be turned into a
damage number.** The honest shape, which the design does adopt, is a weapon that
is poor against a well-armoured European and feared against an unarmoured
opponent. Hukbo has exactly one `ArmorId` value, `LightOrganic`
(`src/Hukbo.Core/Combat/CombatIdentity.cs:72`), so that shape cannot currently be
expressed at all, and the design does not pretend to express it.

**Name check: passes, and it is the strongest name in the package.** Pigafetta's
1521 Visayan vocabulary records "for Bow — bossugh" and "for Arrow — oghon",
identified by Blair and Robertson's editors against *bosog* and *odyong* /
*odiong*, neither row marked conjectural. The word is inherited rather than
borrowed, descending from Proto-Austronesian \**busuʀ*, and is etymologically
distinct from the homophonous Tagalog *busog* meaning "full". The gap between
attestation and the depicted period is zero years.

**Player-facing label: `Busog — War Bow`**, unchanged from
`docs/research/HISTORICAL_1500s_WEAPONS.md:42`. Note for anyone who later labels
the projectile separately: the Visayan word Pigafetta recorded for the arrow is
*odiong*, not the Tagalog *pana*.

**Range: the middle of the three.** Proposed at **five times** the longest melee
reach. The band the weapons research reasons to is 30 to 60 m, capped low by the
documented unfletched shaft rather than by the bow's power: an arrow with a
soft-wood plug in place of fletching and a heavy hardwood foreshaft is optimised
for a deep hit at close to moderate distance, not for grouping at range.

**Rate of fire: the middle of the three.** Six to twelve arrows a minute
sustained, that is one every five to ten seconds, reasoned from the mechanics of
drawing and loosing from a quiver. No Philippine source addresses it.

**A documented ordering that the package loses.** The single sourced range
statement in the entire corpus is Artieda's comparison: blowgun arrows have "the
same effect, although not with the same range" as bow arrows. That fixes the
ordering bow > blowgun on sixteenth-century authority without fixing either
number. With the Sumpit dropped, **that ordering has no expression in the game**,
and the strongest piece of range evidence available becomes inert. It is recorded
here so the loss is deliberate rather than silent.

### Imported Arquebus

**Confidence: Documented, form uncertain.** Matchlock firearms in local hands are
attested three separate times between roughly 1543 and 1567, and one specimen
physically went to Spain. What is uncertain is the weapon's exact form, and what
is emphatically uncertain is how many there were and who had them.

**Evidence line.** García Descalante Alvarado, writing from Lisbon on 7 August
1548 about the Sarangani and Mindanao area around 1543 to 1545, is the earliest,
and is hedged in the source itself: "In some islands they have small pieces of
artillery and a few arquebuses." Legazpi, near Bohol in 1565, fought a junk with
"a culverin and some muskets" whose crew he identifies as "Burnei Moros". And
Legazpi from Cebu, 15 July 1567, sending a specimen to the prince: "they are
bringing to your highness a Chinese arquebuse, of which there are some among
these natives. Although they are very dexterous in handling these guns, when on
the sea, aboard of their *praus*, **they carry them more to terrify than to
kill**."

**Name check: not applicable, and deliberately so.** "Arquebus" is a European
term contemporary with the depicted period, not a cultural identification, so the
pair-form rule and the hundred-year rule do not engage. No sixteenth-century
Philippine name for the weapon was located in any source consulted, and inventing
one would be a far worse offence against `CLAUDE.md` section 7 than leaving the
label in English.

**Player-facing label: `Imported Arquebus`**, with the `IMPORTED` badge, exactly
as `docs/research/HISTORICAL_1500s_WEAPONS.md:46` specifies. The absence of a
Filipino name is itself the historically accurate statement: the weapon arrived
through Chinese and Bornean maritime trade and the sources treat it as foreign
goods in local hands.

**Range: the longest of the three.** Proposed at **seven times** the longest
melee reach. The general matchlock literature puts effective range against an
individual at 50 to 100 m with hit rates of ten to twenty per cent at fifty
metres, and the nominal several-hundred-metre "deadly" range was described as
worthless by contemporaries. No Philippine source gives a range.

**Rate of fire: by a wide margin the slowest of the three**, and this is the
design's most important ordering constraint. The defensible planning figure from
the firearms literature is one shot per minute sustained; two to three a minute
is achievable only in ideal conditions, and damp powder, fouling, or a match cord
that has gone out push the interval past sixty seconds. At the game's 20 Hz tick
rate a literal minute is 1,200 ticks, and a 200-agent battle under the shipped
movement preset ends between 1,279 and 4,405 ticks
(`docs/research/ranged/2026-08-07-STANDOFF-ROOT-CAUSE.md` section 2.2), so a
literal figure would mean an arquebusier fires once or twice in a whole battle.

**The design compresses that interval deliberately and says so.** Hukbo already
compresses combat tempo everywhere — a melee blow every four to eight ticks is
one every two tenths of a second — so a literal reload would be the only
uncompressed duration in the game. The rule the design adopts is: **preserve the
ordering and the magnitude of the gap, compress the absolute value to the game's
own tempo.** The arquebus's interval must remain several times the bow's, which
must remain longer than the thrown spear's. The calibration is a plan task; the
ordering is an invariant.

**"More to terrify than to kill" is the design brief for this weapon, and Hukbo
cannot honour it.** Legazpi's sentence is the only sixteenth-century statement of
what the imported arquebus was *for*, and it describes a psychological effect the
game has no mechanism to express, because morale is a deferred layer under
`CLAUDE.md` section 9 and the tactics research lists a firearm terror effect as
invention number 10 — "an invention wearing a citation". The honest fallback,
which this design takes, is the one the weapons research recommends: make the
arquebus **rare, loud, visually distinctive, and unimpressive in raw attrition**.
High damage on a single shot, a very long interval between shots, and therefore
low total attrition. The loudness and the distinctiveness are carried by sections
8 and 9, which is the whole reason the presentation work is part of this package
rather than a follow-up to it.

Pigafetta supplies the other half of the picture, and it is a Philippine
observation rather than a European one: at Mactan "the musketeers and crossbowmen
shot from a distance for about a half-hour, but uselessly", against a target that
"would never stand still, but leaped hither and thither, covering themselves with
their shields". Thirty minutes of matchlock and crossbow fire at range against
mobile men produced nothing. A design that models the arquebus as a superior
weapon has read the corpus backwards.

### Summary table

Every value in the two right-hand columns is a **gameplay tuning value** and none
of them is a measurement.

| Weapon | Pair-form label | Confidence | Earliest attestation | Reach (multiple of longest melee reach) | Shot interval |
| --- | --- | --- | --- | ---: | --- |
| Bangkaw — Long Spear (thrown) | `Bangkaw — Long Spear` | Documented | Pigafetta, Mactan, 27 April 1521 | 3x | shortest of the three |
| Busog — War Bow | `Busog — War Bow` | Documented | Pigafetta, Mactan, 27 April 1521 | 5x | middle |
| Imported Arquebus | `Imported Arquebus` (with `IMPORTED` badge) | Documented, form uncertain | Escalante Alvarado, Sarangani and Mindanao, c. 1543-45, written 1548 | 7x | far the longest |

### Poison, and why it is not here

Poison on projectiles in the sixteenth-century Philippines is **Documented**,
five independent times across fifty years and four regions, and it is the reason
the bow was feared. It is nevertheless **out of scope for this package**, for
three reasons that are worth recording so the omission is not read as ignorance.
It is a new state-carrying mechanic on `Hukbo.Core` agents with its own
determinism surface; `docs/research/HISTORICAL_1500s_WEAPONS.md` already forbids
presenting it as universal and Artieda explicitly makes it regional; and two
independent sources — Artieda in 1573 and Loarca in 1582 — describe an antidote,
so modelling it as an irreversible sentence would be wrong even if it were in
scope. Section 11 carries it. No plant name may appear in player-facing text
under any circumstances: every period source says "herb", the standard modern
identification is a tree latex, and the discrepancy is unresolved.

### What the three weapons share, historically

Two documented behaviours apply to all three and are worth stating once.

**Every missile-armed man in this record also carried a blade.** Pigafetta at
Cagayan Sulu in 1521 describes one man with a blowpipe, a quiver, a dagger, a
spear, a buckler, and a buffalo-horn cuirass. Legazpi's 1567 specimen shipment is
a single undifferentiated kit. Morga in 1609 has the *bararao* dagger at every
waist. Scott records bladed weapons as ordinary Visayan male costume. The
tactics research draws the structural conclusion directly: the sixteenth-century
Philippine fighter "is not a 'ranged unit' who needs a position relative to a
'melee line'. He is a fighter whose behavior changes with distance." Hukbo gives
each agent exactly one weapon, so this is a **labelled simplification**, and it is
the one the design would fix first if the package were extended.

**Nothing in the corpus makes any of these weapons elite or common, or ties it to
a rank.** No source consulted associates the bow, the thrown spear, or the
arquebus with the *maginoo* or *timawa* strata or with a social role. Any such
association in the game would be invented, and this design makes none.

## 3. The nine acceptance questions

`SIMULATION-GAME-STANDARDS.md:318-330` requires every feature proposal to answer
nine questions, and `CLAUDE.md` section 6 makes question 8 in particular
mandatory. They are answered here in order.

### 1. User-visible outcome

A spectator watching a battle sees warriors who do not close. They stop at a
distance from the enemy that differs by weapon, they visibly draw, load, and
loose, and a projectile crosses the gap over several ticks before anything
happens at the far end. A thrown spear crosses a short gap quickly and often. A
bow shoots further, more slowly, and its arrow takes longer to arrive. An
arquebus is rare, fires from further away than anything else on the field,
almost never, and loudly. Meanwhile the melee warriors in the same army keep
walking in and fighting exactly as they do today.

### 2. Tick stage and state read and written

**Attack.** All ranged resolution lands inside the existing stage 10,
`GatherAndCommitAttacks` (`src/Hukbo.Core/Simulation/BattleSimulation.cs:3579`,
called at `:640`). A new pass A0 runs at the head of that method, before the
existing gather pass: it advances every in-flight projectile's countdown, and
for those that arrive it resolves hit location and clash and accumulates damage
into the same `_damageTotals` array the melee pass uses. Launch happens inside
the existing gather pass, as an alternative to immediate resolution. **No new
tick stage is added**, which means the fixed stage order pinned by
`SIMULATION-GAME-STANDARDS.md:513-523` and section 4 of the same document is
unchanged.

**Movement.** The hold rule lands in stage 5, `GatherMovementProposals`
(`BattleSimulation.cs:1500`, called at `:627`), in the legacy non-equipment-
relative body, under a new movement preset. No stage is added there either.

**State read:** agent position, faction, target, cooldown, loadout, and the
weapon profile. **State written:** the projectile pool, `AgentState.Intent` (a
new `Holding` value), and the existing cooldown, hit points, and event stream.

### 3. Numeric units, bounds, and the same-tick conflict rule

Reach and standoff distance are raw fixed-point world units at
`FixedPoint.Scale = 1_024`, as `AttackRangeRaw` already is
(`src/Hukbo.Core/Combat/WeaponProfile.cs:25-31`). Flight time is an integer tick
count, floored at one and bounded above by a per-weapon maximum derived from the
weapon's own reach, so no projectile can outlive a plausible flight. The
projectile pool has a declared per-scenario ceiling; a launch that would exceed
it is refused and the shot does not happen, which is a bounded, deterministic,
and reported outcome rather than a silent growth.

**Same-tick conflict rule:** every projectile that arrives on a tick, and every
melee blow gathered on that tick, are resolved against the same pre-tick hit
points and applied together in the existing pass C. Two archers whose arrows
arrive on the same tick and together kill a defender both get their blow
recorded, exactly as two melee attackers already do
(`tests/Hukbo.Core.Tests/BattleSimulationTests.cs:215`).

### 4. Total ordering and random-stream policy

Projectiles occupy a dense array in launch order, and launch order within a tick
is ascending source agent index, which is the order the existing gather pass
already walks. Impacts resolve in that array order. Removal compacts the array
while preserving order, so the order is total and stable across every input
permutation.

**No sequential random stream is consumed.** `HitLocationResolver.MixAttack` and
`ClashResolver.MixClash` are pure FNV-1a folds over a tuple rather than draws
from a cursor (`src/Hukbo.Core/Combat/HitLocationResolver.cs:87-102`,
`src/Hukbo.Core/Combat/ClashResolver.cs:53-72`), so a projectile resolved on a
later tick than it was launched reproduces the same roll by folding the **launch
tick** rather than the impact tick, with no stream bookkeeping at all. **Phase 1
mints no new domain tag**, because it adds no new roll — flight time is integer
division and slot allocation is ordered, not drawn. The tag inventory at
`SIMULATION-GAME-STANDARDS.md:833-840` therefore needs no edit for Phase 1. Any
Phase 2 scatter or interception roll mints its own tag and adds itself to that
paragraph.

### 5. Cache source and invalidation, or "no cache"

**No cache.** The projectile pool is authoritative state, not a cache: it is
hashed, it is snapshotted, and it has a declared size bound. Targets continue not
to be cached; the existing per-tick rescan at `BattleSimulation.cs:1013` is
untouched. Phase 1 adds no spatial structure, so the existing uniform grid's
contract (`SIMULATION-GAME-STANDARDS.md:595-611`) is unaffected.

### 6. Save, event, and version effect

All three, and none of it is presentation-only.

- **A new combat preset**, `PrecolonialPhilippinesV5`, with the three ranged
  roster entries. Presets V1 through V4 stay registered and byte-identical.
- **A new movement preset**, the next value after `EquipmentRelativeFootworkV7 =
  7` (`src/Hukbo.Core/Movement/MovementPresetId.cs:145`), carrying the hold rule.
  V1 through V7 stay registered and byte-identical.
- **Two new `BattleEventKind` members**, appended, so no existing numeric value
  moves.
- **A new `AgentIntent` member**, `Holding`, appended.
- **A new conditional tail on the state hash** for the projectile pool, gated on
  a ruleset capability, following the `hasRankLevels` precedent at
  `src/Hukbo.Core/Determinism/StateHasher.cs:136-139`, so no older preset's fold
  moves a byte.
- **A new field on `BattleSnapshot`** for the projectile pool, with the
  save/resume equivalence obligation that comes with it.
- New pinned golden expectations for the new presets. Section 4 lists them.

### 7. Worst-case complexity and benchmark workload

Phase 1 adds **no new asymptotic term**. The launch branch is O(1) per attacking
agent inside a loop that already runs. The projectile advance pass is O(projectiles
in flight), bounded by the declared ceiling. Impact resolution is the same work
an existing melee blow does. The hold check is one squared-distance comparison per
agent per tick, against a distance the proposal builder already computes.

The measured context is that `GatherAndCommitAttacks` is 2.35 % of the tick at
200 agents and 0.44 % at 2,000 (`docs/research/TICK-STAGE-PROFILE.md:107-121`),
while `ResolveCollisions` is 63 % to 75 %. There is a great deal of headroom
inside the stage this feature lands in.

**Benchmark workload:** the canonical 200-agent, 10,000-tick, seed-1 headless run
on a ranged roster, plus a 500-agent result, plus the ten-cell matrix of seeds
{1, 2, 3, 5, 8} at 200 and 500 agents against the V4 baseline in
`docs/research/ranged/2026-08-07-STANDOFF-ROOT-CAUSE.md` section 2.2. The
allocation ceiling that binds is **16,384 bytes per 1,000 warm ticks with a
4,096-byte growth tolerance at 12 agents per faction**
(`tests/Hukbo.Core.Tests/BattleSimulationTests.cs:393-395`); the 900,000 figure
recorded at `SIMULATION-GAME-STANDARDS.md:877` and repeated at
`docs/development/testing.md:1997` is **stale** and is a documentation defect
this package must correct.

### 8. Spectator explanation — can a spectator discover this effect without reading source code?

**Yes, on five independent channels, and no on three specific values, and the
design says which is which.**

Discoverable:

1. **A projectile is drawn in flight.** It exists on screen for several ticks
   between leaving one warrior and arriving at another. Nothing else in the game
   does this, and it is the single most legible thing in the package.
2. **The pose sequence.** A five-phase draw, load, release, and recover reads
   differently from a melee swing, and it differs by weapon: a spear cocked back
   past the shoulder, a bow stave held out with a string hand drawn to the cheek,
   an arquebus shouldered and levelled with a held muzzle flash.
3. **The sound arrives twice.** A release cue at the launcher and an impact cue
   at the target, separated by the flight time. The gap between them *is* the
   flight time, made audible.
4. **The warrior visibly stops.** A ranged warrior halts at a distance and stays
   there while its melee comrades keep walking in past it. That contrast is the
   whole point of the new movement rule.
5. **The inspector says why.** `AgentIntent.Holding` is a reason code that means
   "chose not to close", and it is written only by a deliberate hold, never by a
   rejected movement proposal. That distinction is what separates a working
   skirmisher from the standoff defect, and section 6 explains why it is the core
   new mechanic rather than a nicety.

Not discoverable, and therefore shipping as labelled gameplay tuning rather than
as claims: the numeric reach value, the numeric shot interval, and the roster
proportion of ranged to melee warriors. The tactics research reaches the same
verdict independently — "a ratio, a range value, or a reload time is not visible
at all… which means they fail the standard's test on their own terms before the
historical question is even reached."

One effect is deliberately **not** discoverable in Phase 1 and that is a defect
rather than a design choice: an arrow passing through the front rank looks
identical to an arrow that had a clear lane. Section 12 records it.

### 9. Tests that fail before implementation and pass afterwards

Named in full in sections 4, 5, 6, 8, 9, and 10. The load-bearing ones are: a
projectile launched on tick N resolves on tick N + flight and not before; the
same seed produces an identical state hash, event hash, winner, and ordered event
stream across repeat runs; the projectile pool reaches the state hash and the
snapshot and metrics reach neither; a scenario whose `RosterCounts` assign zero
warriors to every ranged entry under the new combat preset reproduces the V4
seed-1 state and event hashes exactly, which is the inert control in the shape of
`ZeroInterceptionProfile_ReproducesTheRecordedStateHash`
(`tests/Hukbo.Core.Tests/DeterminismTests.cs:997`); the warm-tick allocation
budget holds with projectiles in flight; a held warrior reports
`AgentIntent.Holding` and a blocked one does not; and the twenty-seed termination
and both-factions-win bars still pass on the ranged preset.

## 4. Simulation design

### 4.1 What "hitscan with flight time" means, and why it is still hitscan

`CLAUDE.md` section 9 forbids rigid-body physics and states that "distance checks
and hitscan are the model". This design keeps that rule exactly.

A projectile in Hukbo **is not a moving body**. It has no velocity that is
integrated, it has no collision radius, it is not indexed in the uniform grid, it
cannot be intercepted, and nothing about the world between its launch point and
its target affects it. What it has is a **countdown**. At launch, the simulation
computes how far the target is, divides by a per-weapon speed to get an integer
number of ticks, and stores that number. Every tick the number decreases by one.
When it reaches zero, the attack resolves through exactly the code path a melee
blow already takes: `HitLocationResolver.Resolve`, then `ClashResolver.Resolve`,
then accumulation into `_damageTotals`.

So the resolution is a distance check and a hitscan; the flight time is a delay
on when that check happens, and a position the Client can draw. The stored
origin and the stored target let the Client interpolate a screen position, and
that interpolation is presentation only — the simulation never reads it and
nothing about it reaches a hash.

The one thing this buys, and the reason it is worth the state, is that a
spectator can see a shot in the air. An instantaneous ranged attack would be
indistinguishable from a melee blow at a strange distance.

### 4.2 The attack path for a ranged weapon, end to end

Stages are numbered as in `BattleSimulation.AdvanceOneTick`
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:599`).

**Stage 2 — `SelectTargetsAndIntents` (`:952`) is unchanged.** Target selection
is already nearest-enemy over the whole array with a tie-break on the lower
`EntityId` (`:1082-1089`), and perception defaults to 2,048 world units
(`src/Hukbo.Core/Simulation/Scenario.cs:34`) on a 1,280 by 720 map, so every
living enemy is already perceived by every living agent every tick. A ranged
weapon needs no new perception machinery whatsoever, and `Scenario.Validate`'s
requirement that `PerceptionRangeRaw >= AttackRangeRaw`
(`Scenario.cs:306-309`) is satisfied at every reach this design proposes.

Intent assignment gains one arm; see section 6.

**Stage 10, pass A0 — advance and resolve projectiles.** New. Runs at the head of
`GatherAndCommitAttacks` (`:3579`), immediately after `Array.Clear(_damageTotals)`
at `:3581` and before the existing gather loop. For each active projectile, in
array order:

1. Decrement `TicksRemaining`.
2. If it is still positive, keep the projectile and continue.
3. Otherwise the projectile arrives. Look up the recorded target.
   - If the target is dead, the projectile produces **no accepted attack**, emits
     a `Miss` event, and is removed. A dead target cannot be struck, and this is
     also what gives the miss sound a trigger.
   - Otherwise resolve `HitLocationResolver.Resolve` and `ClashResolver.Resolve`
     using the **launch tick** as the tick word, buffer the five-tuple into
     `_attackProposals` exactly as the melee path does at `:3682-3684`, credit
     the per-faction counters, and, for a `Landed` resolution only, accumulate
     `Damage` into `_damageTotals[targetIndex]` under `checked` arithmetic.
     Remove the projectile.

The projectile carries the damage value it was launched with, so an archer that
died mid-flight still delivers the blow it loosed, and no dead agent's state is
read at impact.

**Stage 10, pass A — gather, with one new branch.** The existing precheck ladder
at `:3610-3727` is preserved in order, because the comment at `:3620-3624`
records that the order is load-bearing: dead-source, no-target, dead-target,
`IsWithinAttackRange`, then cooldown. A ranged weapon changes nothing about those
five. What changes is what happens after they pass: instead of resolving the
attack, a ranged weapon **launches a projectile** — allocating a pool slot,
computing the flight ticks, recording origin, target, launch tick, weapon, and
damage, and emitting a `Release` event — and then charges the cooldown exactly as
an accepted melee attack does.

That preserves two existing behaviours worth naming.
`tests/Hukbo.Core.Tests/BattleSimulationTests.cs:840` pins that a non-landed
attack still resets the attacker's cooldown; a launch is the ranged equivalent
and resets it on launch, not on impact. And `IsWithinAttackRange`
(`:4132-4139`) remains the single approved reach test, centre to centre, with an
inclusive `<=`, so intent selection and attack gathering cannot disagree about
who can shoot whom.

**Stage 10, passes B, C, and D — unchanged in structure.** Pass B emits one
attack event per buffered proposal in `_attackProposals` order, which now
contains impacts before launches within a tick. Pass C applies `_damageTotals`
and emits one `Damage` event per damaged agent. Pass D resolves death. Because
impacts were buffered into the same array before the melee gather ran, every
existing invariant about simultaneity, aggregation, and mutual death holds
without special-casing.

**Stage 12 — `ResolveOutcome` (`:3981`) is unchanged.** A projectile in flight
when the last enemy dies simply never resolves; the battle is over. The design
does **not** hold a battle open for in-flight projectiles, because that would
make the terminal tick depend on presentation-shaped state.

### 4.3 The pooled representation

`docs/research/ranged/2026-08-07-RANGED-SIM-MECHANICS.md` section 8.3 does the
arithmetic that forbids the obvious implementation, and it is worth restating
because it is the single hardest constraint in the package. A projectile as a
heap object of 64 to 72 bytes, at fifty archers with a twenty-tick cooldown,
allocates about 180 bytes a tick and 180,000 across a 1,000-tick window —
**eleven times the enforced 16,384-byte ceiling**. A `List<T>` created per tick,
or a single boxed enumerator per tick, is on its own roughly 46,000 bytes across
a window, nearly three times the ceiling
(`tests/Hukbo.Core.Tests/BattleSimulationTests.cs:376-377`).

The representation is therefore:

- **A `readonly record struct`**, in the same family as `CollisionBody`,
  `CollisionPair`, and `CollisionMoveRequest`. Fields: source entity ID, target
  entity ID, launch tick, ticks remaining, origin X and Y raw, weapon, and the
  damage recorded at launch. Every field is an integer or a small enum; nothing
  is a reference.
- **A flat array sized once at construction** from a declared scenario ceiling,
  `MaximumProjectilesInFlight`, with a live count. Launch appends at the count;
  removal compacts the tail forward, preserving order. Nothing is allocated on a
  warm tick, matching the discipline `SIMULATION-GAME-STANDARDS.md:609-610`
  already states for grid, pair, proposal, and resolution storage.
- **A hard ceiling rather than growth.** `CLAUDE.md` section 9 forbids unbounded
  caches, and an unbounded in-flight list is both a per-tick allocation waiting to
  happen and an unbounded state-hash fold. A launch at the ceiling is refused, the
  shot does not occur, the cooldown is **not** charged, and a derived counter
  records the refusal so it is visible in the report rather than invisible. The
  ceiling has to be validated at scenario construction against the worst case the
  roster can produce — the number of ranged agents times the longest flight — so
  that a refusal is a genuine anomaly rather than routine.
- **Iteration by index, never by enumerator**, for the same reason the existing
  passes walk arrays by index.

The design explicitly does **not** widen `BattleEvent`.
`src/Hukbo.Core/Simulation/BattleEvent.cs:84-90` records that packing five fields
into one `long` kept the event at 72 bytes, and
`docs/development/testing.md:2214-2221` records that merely adding the attacker's
shield to the event pushed the measured budget from 900,000 to 982,744 bytes
before packing fixed it. Bits 40 through 63 of `_combatContext` are free
(`BattleEvent.cs:74-76`) and remain free after this change.

### 4.4 What enters the state hash

The projectile pool is authoritative state. It is not derived scratch, it is not
a cache, and it is not metrics, so unlike almost every structure added to
`BattleSimulation` recently it **must** be hashed and snapshotted.

`StateHasher.Compute` (`src/Hukbo.Core/Determinism/StateHasher.cs:70`) gains one
**new conditional tail after the per-agent loop**, gated on a ruleset capability —
"this combat ruleset fields at least one ranged weapon" — in exactly the shape
V4 introduced for rank levels at `StateHasher.cs:136-139`. The tail folds the
live projectile count, then, per projectile in array order, the source entity ID,
the target entity ID, the launch tick, the ticks remaining, the origin X and Y,
the weapon's numeric value, and the damage.

Two properties of that placement matter.

**No older preset's byte layout moves.** The gate is on a capability rather than
on a preset ID, following the reasoning at `StateHasher.cs:52-59` that reusing an
existing gate would move an older preset's per-agent byte layout. A ruleset with
no ranged roster entry folds nothing at all — not even a zero — which is what
keeps V1 through V4's pinned hashes exactly where they are.

**The section is global rather than per-agent**, so it cannot disturb the
per-agent fold order that `tests/Hukbo.Core.Tests/Movement/MovementStateHashTests.cs`
pins for the V6 footwork fields and the V7 pressure fields.

What does **not** enter either hash: the projectile's interpolated screen
position (it does not exist in Core), the refusal counter, and any new
`CombatMetrics` or `MovementBehaviorMetrics` field. `CombatMetrics_ReachesNeitherHash`
(`tests/Hukbo.Core.Tests/DeterminismTests.cs:415`) already pins that for metrics
and a ranged metric must stay out of both.

`BattleSnapshot` (`src/Hukbo.Core/Simulation/BattleSnapshot.cs`) gains the
projectile array. `SIMULATION-GAME-STANDARDS.md:228` forbids saving derived
caches, render data, or metrics into a snapshot; a projectile is none of those.
Save and resume equivalence for a mid-flight projectile is a Gate 3 obligation
and the design states it here so the plan cannot forget it.

### 4.5 What the new preset version costs

Both a combat preset and a movement preset are needed, and each repeats the full
seven-step cost that
`docs/research/ranged/2026-08-07-RANGED-SIM-MECHANICS.md` section 5.5 documents
from the V3-to-V4 change.

**`CombatPresetId.PrecolonialPhilippinesV5`:**

1. A new enum value on `CombatPresetId`
   (`src/Hukbo.Core/Combat/CombatIdentity.cs:93`), with a doc comment naming
   exactly what it changes relative to V4 and stating that V1 through V4 stay
   registered and unmodified so their replays remain reproducible.
2. New arms in both switches of `CombatPresetRegistry` — `IsRegistered` and
   `Get` — which throws for an unregistered value rather than falling back.
   `tests/Hukbo.Core.Tests/WeaponProfileTests.cs:32` fails immediately if the
   registry arm is missing.
3. A whole new preset file with **every value restated rather than referenced**,
   in the discipline `PhilippineCombatPresetV4.cs:14-17` states: "version 3's
   values are restated here rather than referenced, so retuning version 4 can
   never reach back and move a hash version 3's replays depend on." V5 restates
   V4's four melee rows and its shared target-weight profile verbatim, and adds
   three ranged rows.
4. A new pinned content-hash test asserting a literal and asserting distinctness
   from all four predecessors, in the shape of
   `PresetV4ContentHash_IsPinnedAndDistinctFromV1V2AndV3`
   (`tests/Hukbo.Core.Tests/DeterminismTests.cs:192`).
5. New pinned seed-1 state and event hashes captured from a real headless run at
   20 agents and 200 ticks, in the shape of
   `PresetV4_SeedOneStateAndEventHashArePinned` (`DeterminismTests.cs:215`), with
   the exact command that produced them recorded in the test's comment.
6. The conditional hash fold of section 4.4, gated on the ranged capability.
7. Construction-time validation for the new data: every ranged profile declares a
   projectile speed, a standoff distance, and a flight ceiling, and
   `CombatRuleset` rejects a ranged roster entry missing any of them.

**Three constraints the ranged roster rows must satisfy at construction.**

- **They are two-handed and shieldless.** `WeaponProfileTests.cs:252`, `:272`,
  and `:290` pin that a two-handed weapon rostered with a shield throws, a
  one-handed weapon without a paired profile throws, and a two-handed weapon that
  declares a paired profile throws. Declaring all three ranged weapons
  `WeaponGrip.TwoHanded` with `ShieldId.None` satisfies all three rules with no
  test change, and it matches
  `docs/research/HISTORICAL_1500s_WEAPONS.md:113-114`, which reserves "small or no
  shield" for archers, blowgunners, and arquebusiers. It is nonetheless a
  **labelled simplification**: Pigafetta's Cagayan Sulu passage of 1521 puts a
  buckler on a missile-armed man in the same sentence as his quiver.
- **They clear the reach floor by a wide margin.** `CombatRuleset.ValidateProfileReach`
  (`src/Hukbo.Core/Combat/CombatRuleset.cs:487-492`) rejects a profile at or below
  `2 * DefaultBodyRadiusRaw`. A ranged weapon reaching three to seven times the
  longest melee reach is nowhere near that floor. **Nothing anywhere imposes a
  reach ceiling**, which is exactly why the standoff rule in section 6 and the
  line-of-sight gap in section 11 matter: a long reach simply works, and it
  works by shooting through everything.
- **The seven-entry roster is legal for combat and illegal for
  equipment-relative movement, and the existing validation already enforces
  that.** `MovementRuleset.ValidateEquipmentRelativeFootworkCoupling`
  (`src/Hukbo.Core/Movement/MovementRuleset.cs:389-407`) requires exactly the six
  canonical loadout rows in canonical order, and
  `MovementRouteRules.CanonicalOpponentIndex` (`MovementRouteRules.cs:301-315`)
  throws for an unmapped triple. Pairing V5 with `EquipmentRelativeFootworkV6` or
  `V7` therefore throws at construction rather than misbehaving, which is the
  correct outcome and requires no new guard. The six-loadout ceiling documented in
  `docs/research/ranged/2026-08-07-RANGED-MOVEMENT-FORMATION.md` section 8.4 is
  **not demolished by this package**; it is avoided, because the ranged package
  does not ship on the equipment-relative branch at all.

`CombatConfigurationTests.cs:121` pins the roster as the approved four-entry
configuration and `CombatConfigurationTests.cs:148` pins that `ResolveLoadout`
wraps by entity ID through the roster; both are written against the existing
presets and gain V5 siblings rather than edits.

**The movement preset** is specified in section 6 and carries its own frozen
trajectory digest under `tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs`.

### 4.6 The new goldens, listed

| Golden | Shape it copies | Why it is needed |
| --- | --- | --- |
| V5 content hash, pinned and distinct from V1-V4 | `DeterminismTests.cs:192` | A ranged roster row changes the ruleset content hash |
| V5 seed-1 state and event hash at 20 agents / 200 ticks | `DeterminismTests.cs:215` | The only end-to-end pin on per-tick behaviour |
| New movement preset's frozen trajectory digest | `MovementPresetFreezeTests.cs:187` | Any behavioural change to a preset must be pinned to its own preset |
| **Zero-ranged inert control**: a V5 scenario whose `RosterCounts` give every ranged entry zero warriors reproduces V4's seed-1 state and event hashes exactly | `ZeroInterceptionProfile_ReproducesTheRecordedStateHash`, `DeterminismTests.cs:997` | Proves the whole feature is inert when not fielded, and proves the conditional hash gate is correctly placed |
| Flight-time pin: a projectile launched on tick N resolves on tick N + flight and on no other tick | new | The core new temporal behaviour |
| Launch-tick roll pin: the clash roll for an impact equals the roll the same tuple would produce at the launch tick | new | The determinism property that makes deferred resolution free |
| Allocation pin with projectiles in flight | `BattleSimulationTests.cs:340` | 16,384 bytes per 1,000 warm ticks; the pool must allocate nothing warm |
| Pool ceiling pin: a launch at the ceiling is refused, no cooldown is charged, and the refusal is counted | new | Bounded state, per `CLAUDE.md` section 9 |
| Order-independence pin: a battle with projectiles resolves identically under every agent storage order | `BattleSimulationTests.cs:966`, `DeterminismTests.cs:690` | The new per-tick loop must be order-independent |
| Termination and both-factions-win on the ranged preset across seeds 1-20 | `BattleSimulationTests.cs:566`, `:645` | Section 6's hold rule is the most likely thing in the package to break termination |

The zero-ranged control deserves a note. `Scenario.Validate` already permits a
roster count of zero for an individual entry — the per-entry range is 0 through
`AgentsPerFaction` (`src/Hukbo.Core/Simulation/Scenario.cs:288-293`) — so the
control needs no validation change. It is the cheapest possible proof that the
conditional hash gate is on a capability the scenario can switch off, and it is
the test that fails loudly if a ranged fold leaks into a melee-only run.

### 4.7 The two acceptance bands this will move

`docs/research/ranged/2026-08-07-RANGED-SIM-MECHANICS.md` section 4.6 identifies
these as the only quantitative gates the combat contract enforces, and both take
a ranged weapon in the denominator.

**`CombatMetrics.DefenceAttributableShare` must stay inside 0.25 to 0.45 across
seeds 1 through 20 at 200 agents** (`SIMULATION-GAME-STANDARDS.md:855-861`,
enforced at `tests/Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs:687`).
Every projectile impact is an accepted attack, so it changes the denominator; and
because a ranged attacker's clash roll folds its own weapon
(`ClashResolver.cs:68`), the defensive table needs cells for three new attacker
weapons, which changes the numerator too. **The design's position is that ranged
attacks participate in the ordinary defensive contract** — a shield can stop an
arrow, a warrior can step off the line of a thrown spear — because inventing a
separate resolution path for projectiles would need a sixth `AttackResolution`
value, and inserting a sixth interval moves every roll's outcome at every edge
(`tests/Hukbo.Core.Tests/ClashResolverTests.cs:307`). Whether the V5 tables keep
the share inside the band is a **calibration question the plan owns**, and the
band is the acceptance criterion.

Note the honest caveat on that position: **no sixteenth-century source describes a
Philippine arrow, dart, or thrown spear striking a Philippine shield or corselet.
Not one sentence.** Every penetration statement in the corpus is a European weapon
against a Philippine shield or a Philippine weapon against European mail. So every
interception number for a projectile against Hukbo's own shields is a gameplay
tuning value with no evidentiary confidence whatsoever, and must be marked
`PROVISIONAL` in source and in tests exactly as the tall-hardwood shield
multiplier already is.

**`PhilippineCombatIntegrationTests.cs:797` — shielded roster entries absorb more
blows before dying than shieldless ones across seeds 1 through 20.** This
relationship weakens as the ranged share rises if projectiles are poorly stopped
by shields, and it is a second reason the ranged clash cells are calibration work
rather than a table someone fills in once.

### 4.8 What the design does not add to the simulation

Stated so a reader can see the boundary rather than infer it.

- **No line of sight, no occlusion, no segment query.** Phase 1 has none, so the
  uniform grid gains no method, no new traversal order, and no naive oracle. Both
  research documents that examined this reached the same substantive answer by
  different routes and the design takes it:
  `2026-08-07-RANGED-SIM-MECHANICS.md` section 6.4 recommends an O(n) per-shot
  scan over `_agentStates` rather than extending the grid, and
  `2026-08-07-RANGED-MOVEMENT-FORMATION.md` section 7.6 recommends piggybacking
  on the existing O(n²) target scan rather than adding a second query. They
  differ only on where the scan lives. **The resolution is that neither is built
  in Phase 1**, and when Phase 2 builds one it goes in
  `SelectTargetsAndIntents`'s existing pass, because that pass already computes
  the squared distance the check needs and V6 already established the precedent
  of deriving local context there without adding a query
  (`BattleSimulation.cs:1069-1075`).
- **No friendly fire.** The target scan hard-excludes same-faction candidates at
  `BattleSimulation.cs:1015` and that is unchanged.
- **No ammunition, no reload state, no "out of ammo" intent.**
- **No poison, no toxin, no damage-over-time.**
- **No new `AttackResolution` value.** Five members, unchanged.
- **No morale, no suppression, no terror effect.**
- **No projectile-versus-projectile interaction of any kind.**

## 5. Event design

### 5.1 The starting position

`BattleEventKind` has exactly five members — `Move = 0`, `Attack = 1`,
`Damage = 2`, `Death = 3`, `Outcome = 4`
(`src/Hukbo.Core/Simulation/BattleEvent.cs:5-12`) — and the numeric value is folded
into the event hash. There is no release event, no projectile event, no impact
event separate from the attack, and no reload event. A shot that misses produces
no event at all today, because a miss is not a resolution: every accepted attack
has a target it reached.

Two consumers depend on this. The audio director maps one event to at most one
cue (`src/Hukbo.Client/Audio/SoundDirector.cs:120-146`), so with the existing
five kinds **the Client can play exactly one sound per shot**, which would make
the release half of the sixty-file allocation untriggerable. And
`SwingAnimationSystem.Ingest` starts a swing for every `Attack` event
(`src/Hukbo.Client/Presentation/SwingAnimationSystem.cs:51-63`), so a ranged
loose emitted as an `Attack` event would start a melee swing on the archer as a
side effect.

### 5.2 The two new members

Appended, so no existing numeric value moves:

```
Release = 5
Miss    = 6
```

**`Release`** is emitted at the instant a projectile leaves a weapon, once per
launch, inside the existing attack stage. Source is the launching agent; target
is the agent the shot was launched at; faction is the launcher's;
`Value` carries the **flight time in ticks**, which is the one number the Client
needs and cannot compute. It carries no weapon, no shield, no hit location, and
no resolution — see 5.3 for why, and for how the audio layer gets the weapon
anyway.

**`Miss`** is emitted when a projectile spends itself without producing an
accepted attack. In Phase 1 there is exactly one way that happens: the recorded
target died between launch and arrival. Source is the launcher, target is the
agent that was aimed at, `Value` is zero. It exists so that an arrow which
visibly leaves a bow does not visibly evaporate with no explanation, and so the
miss sound has a trigger.

An **impact** needs no new kind. It is an ordinary `BattleEventKind.Attack`
event, carrying weapon, shield, hit location, and resolution exactly as a melee
blow does, emitted in pass B alongside melee blows. That keeps
`tests/Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs:50` satisfied — every
accepted attack in a fixed-seed battle has a configured weapon and a resolved hit
location — and keeps `PhilippineCombatIntegrationTests.cs:382`, which requires
aggregate damage per target per tick to equal the sum of individual attack
values, true without special-casing.

### 5.3 The `NonAttack` constraint, and how the release cue gets its weapon

`BattleEvent.NonAttack` (`BattleEvent.cs:309`) refuses
`BattleEventKind.Attack` outright and **forces all combat context to null**
(`:318-323`), and `tests/Hukbo.Core.Tests/BattleSimulationTests.cs:1661` pins
that non-attack events never carry a weapon or a hit location.
`tests/Hukbo.Core.Tests/BattleEventTests.cs:288` requires every non-attack kind to
be constructible through `NonAttack`.

That is a direct problem for the audio design, which needs to pick between
`release-bangkaw`, `release-busog`, and `release-arquebus`.

Two options exist and the design takes the first.

**Adopted — the Client reads the weapon off the source agent's loadout.**
`AgentView.Loadout` carries the weapon and is populated under every preset
(`src/Hukbo.Core/Simulation/AgentView.cs`), and `UpdateViews`
(`BattleSimulation.cs:4260`) writes a view for every agent including the dead, so
the lookup always succeeds even for a launcher killed on the same tick. The
audio director currently takes only events (`SoundDirector.cs:120`) and would
take the view list alongside them, which is exactly the signature
`SwingAnimationSystem.Ingest` already has. This costs nothing in `Hukbo.Core`,
keeps `NonAttack` and both its pinned tests untouched, and follows the same
discipline `CLAUDE.md` section 5 states for the debug logger: observe the
simulation from outside, reading state the caller already holds.

**Rejected — relax `NonAttack` to permit a weapon on a `Release` event.** It
would make the combat-context packing carry partial context, which is the exact
reasoning `BattleEventTests.cs:42` protects, and it would put a
presentation-motivated field into the authoritative event for no simulation
benefit.

### 5.4 Effect on the event hash

**The fold does not change.** `HeadlessRunner.AddEventToHash`
(`src/Hukbo.Headless/HeadlessRunner.cs:819-873`) already folds twelve words per
event including the kind and the value, with "absent means `ulong.MaxValue`" as
the sentinel for the five combat-context words. A `Release` event folds its kind,
its source, its target, and its flight-tick value through words that already
exist, and folds the absent sentinel for the rest. **No new word is added to the
fold and no existing word moves.**

What does change is the **stream**. Emitting new events changes the ordered event
stream and therefore the event hash for every seed on which a ranged weapon
fires. Under `CLAUDE.md` section 5 that requires a new preset version plus new
golden expectations, which section 4.5 and 4.6 already provide. It does **not**
change the event hash for any run that fields no ranged weapon, which is what the
zero-ranged inert control proves.

`BattleEventTests.cs:288` covers new non-attack kinds automatically, so
`Release` and `Miss` are exercised the moment they are declared.

### 5.5 Effect on the 200-event feed cap

`CLAUDE.md` section 5 fixes the battle event feed at a maximum of 200 ordered
events. Ranged units add events on two axes: one `Release` per shot, and one
`Miss` per shot whose target died first.

The volume is modest and the reason is historical rather than lucky. Because the
design has **no volley mechanic** — section 7 explains why individual,
opportunistic shooting is the only defensible model — shots are staggered by
independent cooldowns, and those cooldowns are long. At fifty archers with a
hundred-tick interval that is roughly one release every two ticks across the whole
army. Against a feed that already carries a `Move` event for every agent that
moved, plus attack, damage, and death lines, the release traffic is small.

Two decisions follow.

**`Release` and `Miss` enter the feed.** They are authoritative events and
suppressing them in Core would make the feed a filtered view of the stream, which
is worse than a shorter window.

**If measurement shows the feed's visible time window degrades, the filter goes
in the Client, not in Core.** The feed and its panel are presentation
(`tests/Hukbo.Client.Tests/BattleEventFeedTests.cs`,
`BattleEventLogPanelTests.cs`), a filter there is hash-neutral, and the panel
already has the machinery. That decision is deferred to measurement rather than
guessed at now.

`tests/Hukbo.Client.Tests/BattleEventFormatterTests.cs` requires the feed to
render every event kind and every resolution, so both new kinds need a formatter
case or the Client tests fail — which is the designed safety net.

### 5.6 Damage attribution, and the one thing the feed still cannot say

A `Damage` event's source and target are **both** the victim's entity ID
(`BattleSimulation.cs:3794-3795`), so the damage line does not identify who dealt
the blow. For a melee blow that is fine, because the attack event on the same tick
names the attacker. For a projectile the attack event is also on the same tick —
the impact *is* an attack event — so the attribution survives.

What does not survive is the connection between a release and its impact. The
`Release` event on tick N and the `Attack` event on tick N + flight share a source
and a target but nothing links them explicitly. In Phase 1 that is accepted: the
pair is recoverable by a reader, and adding a projectile identifier to the event
would either widen `BattleEvent` or spend the free bits 40 through 63 of
`_combatContext` for a presentation convenience. If a later phase needs it, those
bits are where it goes.

## 6. Movement design

### 6.1 The problem, stated exactly

Under the shipped default movement preset, `PersistentContingentsV4`
(`src/Hukbo.Core/Simulation/Scenario.cs:88-89`), an agent has exactly one
movement decision: walk toward the nearest enemy until the bodies touch. The
stopping distance is decided in one place, and it is measured against the body
diameter rather than against weapon reach:

```csharp
stopShortRaw: checked(2 * Scenario.BodyRadiusRaw));   // BattleSimulation.cs:4054
```

At the default body radius that stop line is 8.5 world units while the default
attack range is 12, and the comment at `BattleSimulation.cs:4047-4053` records
why: stopping at reach left permanent air between the front ranks and bodies never
met. `SIMULATION-GAME-STANDARDS.md:476` states the consequence flatly — "An agent
already inside reach keeps walking in."

There is no retreat, no hold, and no stop-at-range behaviour anywhere in the
codebase. The nearest thing is `PreferredDistanceBasisPoints` in the
equipment-relative branch, and the code says in its own comment at
`BattleSimulation.cs:2116-2118` that "the preferred distance is not a stop line:
both phases continue toward the target's centre so the existing post-movement
reach test stays authoritative." A test pins that deliberately and names it
Contract H (`tests/Hukbo.Core.Tests/Movement/MovementPipelineIntegrationTests.cs:141-150`).

So a bowman given a reach of eighty world units would walk all the way to body
contact and shoot from there, and would be indistinguishable from a spearman.

Worse, and this is the observation that makes the hold rule the core new mechanic
rather than a convenience: **today a warrior that stands still because it chose to
and a warrior that stands still because its movement proposal was rejected are
literally the same state.** `2026-08-07-STANDOFF-ROOT-CAUSE.md` section 7.3
tabulates it — same position over time, same `AgentIntent`, same absence of a
proposal, same metric counter, same inspector reading. A feature whose correct
behaviour is indistinguishable from a known defect cannot be accepted.

### 6.2 The HOLD arm

**A new movement preset**, the next `MovementPresetId` value after
`EquipmentRelativeFootworkV7 = 7`
(`src/Hukbo.Core/Movement/MovementPresetId.cs:145`), is registered as a **verbatim
restatement of `PersistentContingentsV4`'s values plus one new rule**. It sets
`usesEquipmentRelativeFootwork: false` and `appliesPressureInterrupt: false`, so
the three conditional stages at `BattleSimulation.cs:624`, `:634`, and `:646` do
not run, exactly as under V4. The registry precedent for restating rather than
editing is stated at `MovementPresetRegistry.cs:163-169`: V4 "lands as a new
preset rather than as an edit to `PersistentContingentsV3Ruleset` because V3 has
already shipped as a default."

The rule has three parts.

**Part one — a per-weapon standoff distance.** `WeaponProfile` gains a
`StandoffDistanceRaw`, zero for every melee weapon and non-zero for the three
ranged ones. Zero means "close to body contact", which is today's behaviour, so
every melee weapon under the new preset behaves exactly as it does under V4. The
value is validated at construction to sit strictly inside the weapon's own reach:
a warrior that stands beyond its own reach can never shoot, and a warrior that
stands at exactly its reach is one collision nudge from being unable to. The
design proposes the standoff band at roughly three-quarters of reach, which leaves
a working margin, and treats the exact fraction as calibration.

**Part two — approach stops short.** For an agent whose target is beyond the
standoff distance, `GatherMovementProposals` builds an ordinary pursuit proposal
with `stopShortRaw` set to the weapon's standoff distance rather than to
`2 * BodyRadiusRaw`. `stopShortRaw` is already a parameter of the shared step
arithmetic (`BattleSimulation.cs:4077-4089`) and today takes exactly two values —
`2 * BodyRadiusRaw` for closing on an enemy and `0` for a point destination — so a
third value is arithmetically trivial. Everything downstream, including the
arrival taper (`:4095-4100`) and the map clamp (`:4116-4123`), is unchanged.

**Part three — an explicit hold, with its own intent.** For an agent whose target
is **at or inside** the standoff distance, the stage **proposes no movement at
all** and sets `AgentIntent.Holding`. Proposing nothing is the only way an agent
ends a tick where it started other than being refused by the resolver, and the
codebase already does it in two places: the arrived-guard for a cohering agent at
`BattleSimulation.cs:1547-1554`, and `BuildRegroupingProposal`'s null return at
`:3198-3201`. The hold arm is the same shape with a different predicate, and the
research note proposing it says the same
(`2026-08-07-STANDOFF-ROOT-CAUSE.md` section 7.3, item 1).

One placement divergence from the research is worth recording.
`2026-08-07-STANDOFF-ROOT-CAUSE.md` section 7.3 puts the hold arm in
`GatherOneEquipmentProposal` (`BattleSimulation.cs:1876-1882`), beside the
body-contact `Attacking` hold that already forces `phaseSurvives = true`. That
location is inside the **equipment-relative** pipeline, which runs only under V6
and V7. Because this package deliberately ships on a V4-shaped preset and not on
the equipment-relative branch — see section 6.5 — the hold arm goes in the legacy
body of `GatherMovementProposals` instead. The shape is identical; the pipeline is
not.

### 6.3 `AgentIntent.Holding`, and why it is the whole point

`AgentIntent` has five members today — `Idle = 0`, `Moving = 1`, `Attacking = 2`,
`Dead = 3`, `Regrouping = 4`
(`src/Hukbo.Core/Simulation/AgentIntent.cs:12-24`). A sixth is appended:

```
Holding = 5
```

It means, and may only ever mean, **"this warrior is at the distance it wants to
fight from and is deliberately not advancing."** It is written in exactly one
place, by the hold arm, and it is never written by a rejection, a collision, a
blocked proposal, or a failed route search. That single-producer discipline is
what makes it worth adding, and it mirrors the discipline that made
`FootworkPhase.Refuse` diagnosable — that phase has exactly one producer at
`src/Hukbo.Core/Movement/WeaponMovementRules.cs:705`, which is how the root-cause
investigation was able to locate the standoff at all.

The distinction the new value draws:

| | Ranged skirmisher holding by choice | Warrior standing still because it was refused or blocked |
| --- | --- | --- |
| Position over time | unchanged | unchanged |
| `AgentIntent` | **`Holding`** | `Moving` |
| Movement proposal | none, deliberately | one, rejected, or none built |
| `MovementResolution` on the view | the resolution for a tick with no proposal | `Blocked` or the resolver's own reason |
| Inspector | "holding at range" | "blocked" |
| Spectator | stands with comrades advancing past it, shooting | stands in a crowd, doing nothing |

`AgentIntent` enters the state hash (`StateHasher.cs:125`), but **appending a
member moves no existing fold** — only emitting it changes a run, and it is
emitted only under the new preset. V1 through V7 keep their pinned artifacts.
`MovementResolution` remains the separate, unconditional explanation of what
collision did with the proposal (`BattleSimulation.cs:3436`, `:3448`), so the two
channels together say both "what did this warrior want" and "what happened to
it".

The Client shows `Holding` in the agent inspector as a first-class reason code
alongside the existing intents, and the HUD's per-faction readouts count it. That
is what turns "chose not to close" from an inference into an observation, and it
is the answer to acceptance question 8.

### 6.4 What the design deliberately does not add to movement

**No fall-back when an enemy closes.** A ranged warrior that is reached keeps
holding and keeps shooting. Three reasons. The historical one is that the sources
are unanimous that a missile-armed man carried a blade and used it when reached,
and that "the weapon was kept" rather than dropped
(`2026-08-07-RANGED-TACTICS-EVIDENCE.md` section 12) — the game's one-weapon-per-
agent model cannot express drawing a sidearm, so the honest simplification is that
the ranged weapon keeps working at contact rather than that the warrior runs. The
mechanical one is that `Disengage` in the existing codebase does not mean back
away: its route candidates aim at the nearest living ally, then at the contingent
leader, and only take an escape vector when neither anchor exists at all
(`BattleSimulation.cs:2254-2320`), so in a crowded battle it means run toward your
friends. The third is that a retreat rule is the single most likely thing to
break the termination bar, and this package is already spending its termination
budget on the hold rule.

**No formation, no screen, no rank, no depth assignment.** Section 7.

**No kiting, no reposition-under-pressure, no facing cone, no arc-limited
perception.** All four would need machinery that does not exist and, in the case
of an arc query, a ninefold increase in cells visited per query
(`2026-08-07-RANGED-MOVEMENT-FORMATION.md` section 7.5).

**No change to `PersistentContingentsV4`.** It stays registered, byte-identical,
and the shipped default for scenarios that field no ranged weapon.

### 6.5 How this composes with V4 as shipped

The new preset is V4 plus one rule, so composition is mostly a matter of naming
what stays true.

- **Contingent membership, the round-robin deal, the cohesion duty cycle, the
  contingent state machine, the trail-plus-jitter aim point, the last-stand
  rally, the arrival taper, the stall escape, and the collision candidate ladder
  are all unchanged and restated verbatim.** A melee warrior under the new preset
  takes byte-identical decisions to one under V4.
- **The stall escape and the hold rule can disagree, and the design resolves it
  in the hold rule's favour.** After 192 consecutive blocked ticks
  (`src/Hukbo.Core/Simulation/FormationRules.cs:143`) an agent is diverted to
  `BuildSidesteppingPursuitProposal` (`BattleSimulation.cs:1577`), which is an
  oblique offset drawn to get *around* a blockage and keep closing. A holding
  warrior proposes nothing, is therefore never blocked, and so never accumulates
  a blocked streak — the stall escape cannot fire on it. That is the correct
  outcome and it falls out of the design rather than needing a special case. The
  movement research warned that "left alone, a ranged unit would eventually work
  its way to the front"; the hold arm is precisely what stops that.
- **A holding warrior is still a solid body.** Allies queue behind it exactly as
  they queue behind anything else, and `SIMULATION-GAME-STANDARDS.md:695-697`
  already describes that queueing as the normal answer for a rear agent. A ranged
  warrior that stops early becomes an obstacle its own army walks around, which
  is a real and visible cost of the feature.
- **The package does not move the shipped default.** `Scenario.CreateDefault`
  keeps `PersistentContingentsV4` and `PrecolonialPhilippinesV4`. A default flip
  is a separate, measured decision, and this design does not smuggle one.
- **But the Client must be able to see the feature.** Today
  `ArenaGame.BuildScenario` overrides no movement preset, so "the Client always
  runs the shipped default" (`2026-08-07-STANDOFF-ROOT-CAUSE.md` section 3.5). A
  spectator therefore cannot see anything this package builds unless the Client's
  scenario names the ranged combat preset and the ranged movement preset. The
  design's position is that **the Client's default battle uses them**, because a
  feature a spectator cannot reach fails acceptance question 1 outright, while the
  headless default and the canonical gate's determinism workload stay on V4 until
  a separate decision moves them.
- **That leaves a coverage gap the plan must close.** If the gate's 200-agent /
  10,000-tick / seed-1 workload runs V4 and the Client runs the ranged presets,
  the gate never exercises the feature. The plan owes a **second determinism
  workload on the ranged presets**, and the twenty-seed termination test
  (`tests/Hukbo.Core.Tests/BattleSimulationTests.cs:566`) owes a ranged sibling.
  This is the same blind spot the root-cause note identifies for V6 and V7 in its
  section 8.2 — "a preset can draw all twenty seeds and the gate stays green" —
  and the ranged package must not reproduce it.

### 6.6 The termination question, stated as a risk with a measurement

The two research documents disagree about the direction of the effect, and the
disagreement is real enough to record rather than average away.

`2026-08-07-RANGED-MOVEMENT-FORMATION.md` section 4.4 says adding ranged units
makes the termination bar **harder**: "Any rule that legitimately holds a fraction
of the roster at range removes that pressure", and it warns that a ranged rank
standing off at range is the same picture on screen and in the metrics as the V6
defect, so the diagnostic signal that identified the bug is destroyed by the
feature.

`2026-08-07-STANDOFF-ROOT-CAUSE.md` section 7.2 splits the answer by preset and
says the opposite for the one that matters: under `PersistentContingentsV4`,
which has no refusal mechanism at all, giving some agents a much longer reach
raises `attackCapableAgentTicks` enormously, more attacks per tick means shorter
battles, and "the risk under V4 is battles ending *too fast*, not too slow."

**They are both right about different presets, and the design resolves the tension
by shipping on the V4-shaped one.** The movement document's warning is aimed at
the equipment-relative branch, where warriors already refuse to close and a ranged
unit would sit in the refusable `Engage` phase for almost the whole battle. This
package does not go there. On a V4-shaped preset the standoff-root-cause reading
applies: more attacks, shorter battles, with the hold rule pulling the other way
by removing closing pressure for the ranged fraction of the roster.

The net effect of those two opposing forces **is not predictable from either
document and must be measured**. The acceptance criteria are the existing ones,
applied to the new presets: at least 19 of 20 seeds decisive before the
5,000-tick cap with a median decisive tick at or below 5,000
(`SIMULATION-GAME-STANDARDS.md:865-866`), each faction winning at least four of
twenty seeds (`BattleSimulationTests.cs:571`), and the ten-cell matrix compared
against the recorded V4 baseline of 1,279 to 4,405 ticks. If the ranged share has
to be capped to hold those bars, the cap is a tuning value and is labelled as
one — it is not a historical claim about how many archers an army had, because no
source gives a ratio and the tactics research lists a fixed ranged-to-melee ratio
as invention number 3.

## 7. Deployment design — a gameplay model

### 7.1 The historical honesty statement, first

**The tactics research looked specifically for a Philippine ranged formation and
did not find one. There is no line, no missile screen, no massed volley, no
fixed ranged-to-melee ratio, and no attested position for a missile-armed man
relative to a body of close fighters.** The negative result is firm enough that
`2026-08-07-RANGED-TACTICS-EVIDENCE.md` states it as a finding rather than a gap,
in sections 4, 5, 8, and 11.

Four specific corrections carry into this design.

**The skirmisher passage usually cited for a Filipino missile screen is about
Chinese pirates.** Modern writing repeatedly attributes a skirmisher formation to
Philippine forces on the strength of a passage in Sande's 1575 relation of the
Limahong campaign. That passage describes **Limahong's Chinese landing force** —
"a squadron composed of men with battle-axes, among whom were placed some
arquebusiers, a few of the latter going ahead as skirmishers", with one man in
ten carrying a shoulder-mounted banner. The Zambals appear a few paragraphs
earlier and are described only as a people "accustomed to the use of bows and
arrows" who cut off heads. The tactics research calls this "the single most
important correction in this document", and it means any skirmish-screen feature
in Hukbo would be a mechanic built on a misread source.

**Massed volley fire on command is not attested anywhere.** Every description of
Philippine missile shooting that says anything about *how* it was done describes
continuous, hurried, individually directed shooting: "shooting arrows as rapidly
as they could" at Bombon in 1570, a simultaneous shower of arrows and bamboo
spears and fire-hardened stakes and stones and mud at Mactan, and above all "the
natives shot only at our legs, for the latter were bare" — aimed fire at an
observed weak point in an individual target's protection, which is incompatible
with a commanded volley at a body of men. The English word "volley" appears in
the Blair and Robertson translations and is the translators' word, not the
witnesses'; the one episode where the source unpacks it, the volley turns out to
be fourteen men in canoes. And a force with no attested command signal — no
voice, horn, gong, drum, or flag — cannot be asserted to have had a fire
discipline, because volley fire is a command technology before it is a shooting
technology.

**No ratio exists.** Morga in 1609 makes the bow explicitly the *provincial* case
against generally used spear and shield; Legazpi around 1565 lists "a few bows and
arrows"; Scott records archers "in most places but in small numbers". The one
headcount in the whole corpus is Rada's hundred Zambal archers in 1577, and it
counts an **allied contingent under its own chief negotiating its own price in
heads** for a Spanish campaign against a Chinese pirate. It is not a proportion of
a Philippine war party and cannot be turned into one.

**Where the specialist does exist, he is a different people.** The Negrito
archers, the Sangil bowmen sent to Mindanao allies, and the hundred Zambals are
all somebody else's contingent. Within one following, no source anywhere
distinguishes a missile specialist from a close fighter.

### 7.2 What the design therefore ships: nothing positional

**No deployment rule is added.** Ranged warriors are dealt to contingents
round-robin by faction-local index exactly as everyone else is
(`src/Hukbo.Core/Simulation/FormationPlanner.cs:104-107`), and that round-robin
exists precisely to prevent weapon-homogeneous contingents — its own comment says
so: contiguous contingents "would come out weapon-homogeneous — a stronger claim
than the evidence supports." That comment is right and the design does not
overturn it.

So there is no depth rule, no lane rule, no screen, no shooting rank, no reserve,
and no role abstraction. `ResolveAnchorX`
(`FormationPlanner.cs:330-346`) continues to set contingent depth from the parity
of the contingent index and from nothing else.
`EquipmentDeploymentAssignment` (`src/Hukbo.Core/Movement/EquipmentDeploymentAssignment.cs:56`)
continues not to run, because it requires equipment-relative footwork and this
package does not use it.

This also keeps the design inside two standing prohibitions rather than
negotiating with them. `SIMULATION-GAME-STANDARDS.md:417-421`: "Agents are never
assigned to a rank, a file, a slot, or a named formation." And
`src/Hukbo.Core/Simulation/ContingentState.cs:8-10`: "This is a behavioural mode,
never a positional assignment — no agent is ever assigned to a rank, a file, or a
named formation slot." Neither is touched.

### 7.3 The gameplay model being declared

**This section is a labelled gameplay model, in the same sense and with the same
force as `docs/research/HISTORICAL_1500s_WEAPONS.md:257` uses for the combat
targeting preset**, which records that the preset "is a gameplay model, not
measured historical probability" and that the two documents "must not be read as
making the same kind of claim". The model has three parts and each is an
invention:

1. **A ranged warrior shoots at the nearest enemy it can perceive, individually,
   at a rate set by its own weapon, with no coordination of any kind.** This part
   is the closest to defensible — the tactics research calls individual,
   opportunistic, target-of-opportunity shooting "the historically defensible
   model… at the Documented tier" — but the specific cadence is invented.
2. **A ranged warrior stops advancing at a weapon-specific distance and holds
   there.** No source describes a warrior choosing an engagement distance. This
   is invented for readability and for termination, and it is the mechanic the
   whole package turns on.
3. **A roster proportion of ranged to melee warriors, set by
   `Scenario.RosterCounts`.** Any specific percentage is a tuning value with no
   evidentiary confidence whatsoever. What the evidence supports is a direction:
   missile-armed warriors should be a **clear minority**, and the most common
   thrown weapon should be a cheap bamboo javelin rather than a bow.

All three are marked `PROVISIONAL` in code comments and in tests, and the agent
inspector records the evidence tier for the weapon while making no claim at all
about the deployment.

### 7.4 The one thing that emerges without being assigned

There is a pleasing consequence worth naming, because it is the design's answer
to the question "but where do the archers stand".

A ranged warrior that halts at its standoff distance while its melee comrades
keep walking in will, within a few hundred ticks, find itself behind them. Not
because anything placed it there, not because it has a rank or a slot, and not
because any rule knows what a screen is — but because it stopped and they did
not. Depth emerges from the hold rule the same way the battle line already
emerges from straight-line pursuit meeting a solid-disc collision resolver
(`SIMULATION-GAME-STANDARDS.md:690-697`).

That is the only kind of formation this design is willing to produce, and it is
the only kind the sources permit: **a shape that happens, rather than a shape that
is assigned.**

Two honest caveats on it. The emergent depth is fragile — a ranged warrior whose
nearest enemy is on its own side of the line will advance past its comrades, and
nothing prevents that. And a holding warrior is a solid body its own army has to
walk around, so the emergent shape includes some congestion the melee-only game
does not have. Both are visible on screen, which under acceptance question 8 makes
them a legitimate cost rather than a hidden one.

## 8. Presentation design

### 8.1 What Core must newly expose, and the move that makes it cheap

The pose research states the problem exactly: **no signal exists today at any
fidelity that says "this agent is drawing a bow", and none can be synthesised
from what the Client currently receives**
(`2026-08-07-RANGED-POSE-MECHANICS.md` section 5).
`AgentView.Intent == Attacking` is set on the tick the attack fully resolves and
carries no sub-tick phase. The `Attack` event *is* the resolution, which is why
`SwingAnimationSystem` plays anticipation, strike, hold, and recovery
retrospectively — acceptable for a quarter-second melee cut and not acceptable
for a bow whose draw is the readable part. `FootworkPhase` is the closest thing
in the codebase to an attack-phase channel and it is `None` forever under the
shipped preset. `TacticalPosture`, `MovementPaceRaw`, `Facing`,
`FootworkTicksRemaining`, `BrokeOffUnderPressure`, and both pressure fields are
likewise all zero or `None` under `PersistentContingentsV4`
(`src/Hukbo.Core/Movement/MovementPresetRegistry.cs:185`, `:189`;
`src/Hukbo.Core/Simulation/AgentView.cs:49-118`). The one exception is
`MovementResolution`, written unconditionally for every agent every tick, alive
or dead (`BattleSimulation.cs:3436`, `:3448`). And the gait system's trick —
derive motion from the position delta — is useless here, because a drawing archer
is standing still.

The pose research's recommendation is a per-agent ranged phase enum plus a
ticks-remaining counter as **new authoritative state**, mirroring
`FootworkPhase` / `FootworkTicksRemaining`. **This design does something cheaper
and takes the difference as a deliberate divergence from the research.**

**The phase is a projection, not state.** The simulation already agrees a duration
for a ranged action: it is the attack cooldown. `AgentState.AttackCooldownRemaining`
counts down from `AttackCooldownTicks` every tick in stage 1
(`BattleSimulation.cs:941`), and for a ranged weapon that countdown is long — a
hundred ticks or more — where a melee cooldown is four to eight. So `UpdateViews`
(`BattleSimulation.cs:4260`) derives the phase and the phase progress from the
pair `(AttackCooldownRemaining, AttackCooldownTicks)` and writes them onto the
view. Nothing new is stored, nothing new is hashed, nothing new is snapshotted,
and the derivation cannot change a simulation because it reads state the tick
already produced.

Core therefore newly exposes exactly **three** things:

| # | What | Where | Kind |
| --- | --- | --- | --- |
| 1 | Three new `WeaponId` members — the three ranged weapons | `src/Hukbo.Core/Combat/CombatIdentity.cs:14` | appended enum values, hashed |
| 2 | `AgentView.RangedPhase` — a six-member enum with `None = 0` | `src/Hukbo.Core/Simulation/AgentView.cs` | **derived projection**, hash-neutral |
| 3 | `AgentView.RangedPhaseTicksRemaining` — an `int` | same | **derived projection**, hash-neutral |

Plus, from section 5, the two new `BattleEventKind` members and, from section 6,
`AgentIntent.Holding`. And that is the complete list of what the Client gains.

Aim direction needs nothing new. `Facing16` exists as a type but is `None` under
every preset that is not equipment-relative, so the Client derives the direction
from `TargetEntityId` plus the two agents' positions, exactly as
`SwingAnimationSystem.ResolveDirection` already does
(`src/Hukbo.Client/Presentation/SwingAnimationSystem.cs:142-150`). The pose
research offers this as a "weaker fallback… probably good enough for a first
pass", and its cost is that a warrior aims at its currently selected target rather
than at where the projectile is actually going. In Phase 1 those are the same
thing, because a projectile is resolved against the agent it was launched at.

### 8.2 The five phases

The enum lives in `Hukbo.Core` because Core drives it, and `None = 0` so that
`default` is the neutral standing pose, exactly as `default(SwingPose)` and
`default(GaitPose)` already are.

```
None = 0     not a ranged weapon, or no ranged action in progress
Ready        weapon carried, cooldown clear, waiting for a target in range
Load         nock the arrow, or bring the shaft to the hand, or pour and ram
Draw         pull to anchor, or cock the arm back, or shoulder and level
Release      the shot leaves the weapon
Recover      return to Ready
```

The countdown maps onto them directly. The instant a shot is launched the
cooldown is charged to its full value, so reading downward from full to zero the
warrior passes `Release`, then `Recover`, then `Load`, then `Draw`, and sits in
`Ready` at a cooldown of zero until a target enters range. Each phase claims a
share of the cooldown, and `RangedPhaseTicksRemaining` reports how many ticks are
left in the phase the warrior is currently in, so the Client can interpolate
without a clock. The shares are provisional tuning values and differ by weapon —
an arquebus should spend most of its very long interval in `Load`, a bow most of
its shorter one in `Draw`.

Two properties of this scheme are worth stating because they are what make it
work.

**It needs no speed scaling and it survives pause.** The gait system's design note
records the principle (`src/Hukbo.Client/Rendering/GaitPose.cs:40-48`): a
tick-driven phase needs no handling for pause or playback speed anywhere, whereas
`SwingAnimation` has to be fed speed-scaled seconds or a 4x battle shows every
warrior permanently mid-swing (`SwingAnimationSystem.cs:10-17`). The ranged pose
inherits the gait system's discipline, not the swing system's.

**It is not retrospective.** Unlike the swing, which plays its anticipation after
the blow has already landed, the ranged draw is genuinely anticipatory: the
warrior is visibly drawing for many ticks before the shot exists. That is the
single largest readability win in the package and it is why the phase had to come
from Core rather than from an event.

### 8.3 The `RangedPoseResolver` shape

Copied structurally from `SwingPoseResolver` and `GaitPoseResolver`, which are
deliberately identical to each other, and from the pure-helper rule that
`.claude/skills/hukbo-client-ui/SKILL.md:8-40` and `CLAUDE.md` section 5 both
state: everything that *decides* lives in an `internal static` class over plain
values, everything that *paints* takes a `SpriteBatch` and is never unit tested.

| Piece | Kind | File |
| --- | --- | --- |
| `RangedPose` | `internal readonly record struct`, `default` is neutral | `src/Hukbo.Client/Rendering/RangedPose.cs` (new) |
| `RangedGeometry` | `internal static class`, keyframe mathematics over value types only | `src/Hukbo.Client/Rendering/RangedGeometry.cs` (new) |
| `RangedPoseResolver` | `internal static class` with `Resolve` and `TryGetPose` | `src/Hukbo.Client/Rendering/RangedPoseResolver.cs` (new) |
| pose plumbing | a third nullable pose parameter | `src/Hukbo.Client/Rendering/PawnGeometry.cs` (edit) |
| per-frame buffer and resolve call | `_rangedPoses` field beside `_gaitPoses` | `src/Hukbo.Client/ArenaGame.cs:159`, `:674-678` (edit) |
| draw-loop lookup | beside the two existing `TryGetPose` calls | `src/Hukbo.Client/ArenaGame.Rendering.cs:961-972` (edit) |
| three new weapon roles | `PawnWeaponRole` members | `src/Hukbo.Client/Presentation/PawnAppearance.cs:5-11` (edit) |

Five rules the new resolver inherits without negotiation:

1. **`Resolve` fills a caller-owned dictionary**, clears it, and returns the same
   instance as `IReadOnlyDictionary`. It never allocates a dictionary of its own.
   The buffer is an `ArenaGame` field constructed once, and a test pins the reuse
   contract by resolving twice into one buffer and asserting the second result
   replaces rather than accumulates
   (`tests/Hukbo.Client.Tests/SwingPoseResolverTests.cs:53-58`).
2. **An agent with no ranged action gets no entry, never a neutral one**, "so a
   caller cannot confuse 'standing still' with 'not drawn'".
3. **`TryGetPose` exists solely so the draw loop's lookup is covered by a test**
   rather than living in the untestable file.
4. **The geometry is a separate static class over value types**, testable with no
   store at all.
5. **An early-out when nothing is active.** `SwingPoseResolver` skips the whole
   pass when its store is empty (`SwingPoseResolver.cs:50-54`), and
   `GaitPoseResolver` notably does not — it walks every agent every frame with a
   linear `TryGetEntry` inside, which is 250,000 comparisons per frame at 500
   agents. Because most battles under most rosters have no ranged warriors at all,
   the ranged resolver **must** copy the swing resolver's early-out, not the gait
   resolver's omission.

**No animation store is needed.** `SwingAnimationSystem` and
`GaitAnimationSystem` exist because their state has nowhere else to live — a swing
clock and a stride phase are Client-side inventions. The ranged phase arrives on
the view every tick, so the resolver reads it directly and there is no store, no
capacity, no eviction, and no clock. That is a genuine simplification over both
existing systems and it follows from the section 8.1 decision.

**Mutual exclusion with the swing pose.** Both poses write the same two channels,
`WeaponAngleRadians` and `ExtensionRatio`, into the same `ApplySwing` call
(`src/Hukbo.Client/Rendering/PawnGeometry.cs:1576-1589`), which rotates one line
about one grip. There is only one weapon line per pawn, so summing two rotations
would produce a meaningless angle. The rule is: **a ranged pose suppresses the
swing pose for that pawn on that frame**, decided in the draw loop beside the two
existing lookups and lifted into a pure helper on `RangedPoseResolver` so it can
be tested. In practice it may never fire, but it must be explicit, because
`SwingAnimationSystem.Ingest` starts a swing for *any* attacker
(`SwingAnimationSystem.cs:51-63`) and a projectile impact is an `Attack` event —
which would start a swing on the archer as a side effect.

**Composition with the gait pose is additive and needs no rule.**
`CreateBodyAnchor` already sums two independent lean contributions
(`PawnGeometry.cs:945-952`); adding a third term is a one-line change and is
exactly the precedent the gait work established. Legs and feet are computed only
from the gait pose, the weapon line only from the swing pose, and the torso lean is
the single shared channel. A warrior can walk and reload at the same time with no
mutual exclusion at all.

### 8.4 How the three weapons differ in pose

All three are drawn from the same vocabulary — one weapon line rotating about a
grip, one secondary rectangle, and a torso lean — differentiated by which channel
moves in which phase. **Every pose value is a provisional reconstruction for
gameplay legibility, not a measurement**, and must be commented as such the way
every constant in `SwingGeometry` and `GaitGeometry` already is. The silhouette
guidance comes from `docs/research/HISTORICAL_1500s_WEAPONS.md:40-47` and the role
table at `:110-118`.

**Bangkaw — Long Spear (thrown).** "Very long dark palm or rattan shaft,
oversized leaf-shaped steel point, carried diagonally beyond the body"; archetype
"longest diagonal line". The closest of the three to the existing swing
vocabulary. `Ready` carries it diagonally across the body. `Load` shifts the grip
back along the shaft. `Draw` cocks the arm and rotates the line steeply back past
the shoulder with the torso leaning *away* from the target — the negative-lean
keyframe `SwingGeometry.PullBackLean = -0.9f`
(`src/Hukbo.Client/Rendering/SwingGeometry.cs:121`) is the exact precedent.
`Release` sweeps forward past neutral with the largest extension of the three.
`Recover` returns to an empty hand, and the one genuinely new drawing behaviour
this weapon needs is that **the weapon line shortens or vanishes once the shaft is
thrown**, for the duration of the recovery. Nothing in the codebase does that
today.

**Busog — War Bow.** "Tall bow arc outside the torso silhouette, pale reed
arrows, dark points, clearly visible back quiver"; archetype "bow arc and
quiver". This is the one that genuinely wants two lines: a near-vertical stave
held out from the body, which barely rotates across the whole sequence and is the
*reference* the other motions read against, and a short string-hand line drawn
back to the cheek, which carries the draw. A `DrawTension` channel rises through
`Draw`, holds at the top, and **snaps to zero on `Release`** — the single-frame
snap is the readable moment. The torso lean stays small; an archer at full draw is
upright and still. The quiver is a time-invariant appearance layer belonging with
the sash and adornment layers, which are explicitly documented as never reading a
pose, not a pose channel.

**Imported Arquebus.** "Long timber stock, dark iron barrel, horizontal pose,
small glowing matchcord, and an `IMPORTED` badge"; archetype "long horizontal
stock and barrel"; and the research is explicit that "it should be rare".
The longest sequence and the least like the other two. `Load` is a multi-beat
business that reads as a ramrod line moving *along* the barrel rather than about a
grip, and it occupies most of the weapon's very long interval. `Draw` is the
shoulder-and-level: a small rotation toward horizontal plus a body-anchor shift.
`Release` is the one phase in the package that should **hold** for longer than an
instant, because the muzzle flash and the recoil pulse are the readable content
and this is the weapon whose whole documented purpose was to be noticed. Two
constraints follow. The horizontal barrel is the flattest silhouette in the game
and will be hardest to tell from a Kampilan at the Low detail tier, so this weapon
most needs its secondary rectangle to survive Low tier — the Wasay's axe head is
the established precedent for exactly that exception
(`PawnGeometry.cs:1526-1532`). And the matchcord and the `IMPORTED` badge are
appearance layers, not pose channels; the badge in particular is UI, not geometry.

### 8.5 The projectile in flight

A projectile is drawn from a `Release` event plus the flight-tick count it
carries. The Client holds a small fixed-capacity store of in-flight projectiles,
advanced by tick rather than by a clock, and draws each as **one line segment**
from the recorded origin toward the target's current screen position,
interpolated by elapsed flight fraction. It is presentation only: the simulation
holds its own authoritative pool and the Client's copy exists to be drawn.

Three deliberate limits. The drawn projectile does not decide anything. It is not
culled per-pose, because the cull is pose-blind by design and a projectile is not
part of a pawn. And it is drawn at the same detail tier as the weapon line rather
than being gated off at Low tier, because at Low tier the projectile may be the
only thing that says a ranged unit exists.

### 8.6 The quad budget arithmetic

This is the binding presentation constraint and it has real but limited headroom.

The measured per-pawn High-tier baseline is **24 quads**
(`tests/Hukbo.Client.Tests/PawnQuadCountTests.cs:57-69`), and the whole-frame
arithmetic recorded at
`src/Hukbo.Client/Rendering/SubmissionCount.cs:424-447` is:

```
(24 quads/pawn x 500 units) + 4,032 backdrop = 16,032 quads
ceiling                                      = 20,000 quads
headroom                                     =  3,968 quads
             divided across 500 pawns        =  7.9 quads per pawn
```

Two things hide inside that number. The 24-quad baseline is measured with
`swingPose: null` (`PawnQuadCountTests.cs:61`), so the swing arc trail's six
stroked quads sit *outside* it — five hundred pawns all mid-swing at High tier is
`(24 + 6) x 500 + 4,032 = 19,032`, inside the ceiling with only 968 quads of
slack. And a naive bow drawn as a stave line, an arrow line, and a string line
would be three `DrawLine` calls at three quads each, six to nine quads per pawn,
which breaches the budget on its own.

**The budget this design takes:**

- **The pose itself costs zero.** Rotating the existing weapon line and shifting
  the body anchor adds no rectangle. `PawnQuadCount` counts rectangles, not
  phases, and the weapon is a flat three quads regardless of how it is posed
  (`SubmissionCount.cs:43`, applied at `:119`).
- **At most one new rectangle per ranged pawn**, reusing the existing
  `SecondaryEquipmentBounds` slot the Wasay's axe head already proves out. The
  bow's stave is the weapon line; the nocked arrow is the secondary rectangle.
  That is **+1 quad on a ranged pawn only**: `25 x 500 + 4,032 = 16,532`, leaving
  3,468 quads of headroom in the all-ranged worst case.
- **No trail-equivalent.** The swing trail's six quads are the only thing in the
  budget with that shape and the ranged pose may not add a second one.
- **The projectile is not a pawn cost.** In-flight projectiles are counted
  separately against the whole-frame estimate, one line each, at a bounded
  population. The plan owes that arithmetic explicitly rather than folding it
  into the per-pawn figure.

Two pinned tests move deliberately and their new values belong in the commit
message with the arithmetic, per the anti-density-creep rule at
`SubmissionCount.cs:412-421`:
`Count_PinsTheHighTierUnshieldedUnarmoredNormalPawn` (`PawnQuadCountTests.cs:58`,
exactly 24) and `Count_TheWeaponAlwaysContributesTheSameQuadsRegardlessOfRole`
(`PawnQuadCountTests.cs:184`).

### 8.7 The cull constraint, which is a hard limit on pose values

`ConservativePawnCull` is dead code — nothing calls it — but
`tests/Hukbo.Client.Tests/ConservativePawnCullTests.cs` proves by brute force over
the full catalog cross-product that its radius contains every pawn's real bounds,
and its radius is sized by the Kalis weapon line's upward reach of 24.2 units
(`src/Hukbo.Client/Rendering/ConservativePawnCull.cs:77-111`). **A ranged weapon
whose line reaches further than the Kalis's fails that test**, correctly, even
though nothing calls the type.

More seriously, the cull that *is* in the path is pose-blind on purpose
(`src/Hukbo.Client/ArenaGame.Rendering.cs:915-922`), and
`PawnGeometryTests.cs:2089` and `:2338` pin that the cull rectangle does not move
with the pose. **If a ranged pose extends the weapon line further than any swing
pose can, the drawn pawn escapes its own cull rectangle and is clipped at the
arena panel edge.**

The constraint is therefore explicit: **the maximum extension any ranged pose can
reach must fit inside the envelope the existing weapon-line padding already
allows**, or the pose-blind bound needs a documented, tested widening with the
cull constants moved in the same change. The design's position is to fit inside
the existing envelope — the arquebus barrel and the bow stave are held no longer
than the Kalis's 24.2 units — and to treat a widening as a fallback that must be
argued for rather than assumed.

### 8.8 The rest of the Client surface the three weapons touch

Named so the plan can scope it, each one a test that fails until it is done:

- **`PawnWeaponRole` gains three members**, and **four `switch` expressions over
  it** each need a new arm: the start, end, and padding switches in
  `CreateWeaponLayout` (`PawnGeometry.cs:1496-1522`), `CreateWeaponThickness`
  (`:1547-1562`), and `CreateSecondaryBounds` (`:1591-1612`). The first four throw
  `ArgumentOutOfRangeException` on an unrecognised role, so this is a runtime
  throw rather than a compile error if it is missed.
- **`WeaponVisualCatalog` needs a tint list, a silhouette, an evidence tier, and a
  pair-form label for each weapon.**
  `tests/Hukbo.Client.Tests/WeaponVisualCatalogTests.cs:262` fails if any defined
  weapon falls through to a category default, `:289` requires a non-empty tint
  list, `:224` requires an own silhouette, and the per-weapon evidence-tier facts
  at `:141` and `:589-601` are where `CLAUDE.md` section 7's naming policy is
  **mechanically enforced** — every entry carries a defined tier and a non-empty
  note, and every label uses the unchanged pair form.
- **`AppearanceRosterContractTests.cs:70` pins the appearance roster at 53
  presets**, and `:388` requires every pair within each regional block to stay
  visually differentiable. Archer and arquebusier presets move both.
- **`DetailTierBoundaryTests.cs:161` sweeps every shipped catalog entry**, so new
  entries are pulled into the tier sweep automatically and must classify.
- **`SourceHygieneTests`** bans `System.Random`, `GetHashCode`-based selection,
  wall-clock reads, and dictionary iteration order from presentation code
  (`:181`, `:203`, `:222`, `:249`). A ranged resolver obeys all four by
  construction if it copies the existing shape.

### 8.9 The manual checklist obligation

A projectile in flight, a five-phase draw, a release sound, a miss, a held
warrior, and three new silhouettes are all interactive behaviours, and under
`CLAUDE.md` section 6 rule 4 **no compilation, unit test, or window-opening probe
may flip a row in `docs/development/testing.md` to `PASS`.** Every one of them
gets a new `PENDING` row, and only a human at an interactive desktop may change
it. The plan owes the rows; it does not owe the results.

## 9. Audio design

### 9.1 The allocation: sixty files, twenty per weapon

Four slot families per weapon, plus one arquebus-only fifth. Every slot follows
the existing `<slot>[-<class>][-NN].wav` contract unchanged.

| Slot family | Class-driven | Fires when | Bangkaw | Busog | Arquebus |
| --- | --- | --- | ---: | ---: | ---: |
| `release-<weapon>` | no | the shot leaves the weapon | 5 | 6 | 7 |
| `attack-<weapon>` | **yes**, six hit classes | the shot reaches a body | 9 | 8 | 6 |
| `clash-shield-<weapon>` | no | the shot is stopped by a shield | 3 | 3 | 3 |
| `miss-<weapon>` | no | the shot spends itself without landing | 3 | 3 | 2 |
| `misfire-arquebus` | no | the charge fails | — | — | 2 |
| **total** | | | **20** | **20** | **20** |

Sixty files. Thirteen new `GameSoundId` members, taking the catalog from thirteen
slots to twenty-six.

**Why `release` takes the largest share on two of three weapons.** It is the only
cue that fires on one hundred per cent of shots, so it is the cue a spectator
hears most often and the one that repeats soonest. It is also the cue most
exposed to concurrent playback, which by section 9.4 makes its take count a
mix-headroom decision as well as a repetition one.

**Why the counts differ by weapon.** Takes follow where each weapon's character
lives. The **Bangkaw** is a quiet release and a very loud arrival, and a heavy
shaft into a skull genuinely differs from one into a thigh, so nine impact takes
buy a second take on the two hit classes that receive roughly sixty per cent of
all blows. The **Busog** sits between: six release, eight impact. The
**Arquebus** is the reverse of the spear — the report dominates and the impact is
a short slap — so seven release takes and only six impact, one per class, and it
spends two files on `misfire`, which is the one moment that must be unmistakable
and is the cheapest way to make a weapon that almost never fires still feel
mechanical. Its `miss` count drops to two because a lead ball striking earth
varies less than a shaft does.

**Why three shield takes rather than the melee weapons' four.** A shield block is
a minority resolution and does not stack the way a release does, so the take
bought there does less work than a take on `release`. Three is where the
twentieth file comes from.

**Where the fourth weapon's twenty went.** The Sumpit is dropped (section 1), so
eighty files became sixty. The blowgun's allocation in the audio research was the
most release-heavy of the four — eight release takes, because the puff of a
blowgun *is* the weapon acoustically — and none of that carries over.

`misfire-arquebus` needs one honest caveat: **no source describes a misfire in
Philippine hands.** It is a gameplay invention drawn from the general behaviour of
a sixteenth-century matchlock in a wet tropical climate, and the one period
Philippine data point that bears on it is a *counter-tactic* rather than a
malfunction — the Manila-area plan of 1570 to attack "at the first rain, when it
would be impossible for them to make use of the arquebuses". The design keeps the
sound and does **not** add a misfire mechanic to `Hukbo.Core` in Phase 1; the two
files are generated against a Phase 2 trigger, which section 11 records, or they
are the two files that go unspent. That is stated so the spend is a decision
rather than an accident.

### 9.2 Worked example — every filename for the Busog

Exactly as they must appear in `src/Hukbo.Client/Content/Audio/`:

```
release-busog-01.wav
release-busog-02.wav
release-busog-03.wav
release-busog-04.wav
release-busog-05.wav
release-busog-06.wav

attack-busog-extremity-01.wav
attack-busog-extremity-02.wav
attack-busog-limb-01.wav
attack-busog-limb-02.wav
attack-busog-skull-01.wav
attack-busog-neck-01.wav
attack-busog-ribcage-01.wav
attack-busog-gut-01.wav

clash-shield-busog-01.wav
clash-shield-busog-02.wav
clash-shield-busog-03.wav

miss-busog-01.wav
miss-busog-02.wav
miss-busog-03.wav
```

The eight impact takes are weighted by the hit-class shares the shared target
weights produce: `extremity` at 35.4 per cent and `limb` at 24.2 per cent get two
each, the four thinner classes one each. **`ribcage` must be among them**, because
it is the universal fallback target for every other class
(`src/Hukbo.Client/Audio/HitClass.cs:100-113`) and a hit-location driven slot
without it can resolve `Missing` for a class whose own file is absent.

### 9.3 The naming that keeps `sfx.ps1` unchanged

Three properties of the existing tooling are preserved by the names above, and
each is a concrete reason not to name them anything else.

**The slot-parsing regex accepts every name with no edit.** `Get-CatalogSlot`
(`scripts/sfx.ps1:260-272`) pulls slot names out of `SoundCatalog.cs` with the
capture group `[a-z0-9-]+` — lowercase ASCII, digits, and hyphen. Every proposed
name matches. The only way to break it is an underscore or a capital letter in a
base name, and
`SoundCatalogTests.GetFileName_IsUniqueLowercaseKebabWavForEverySlot`
(`tests/Hukbo.Client.Tests/SoundCatalogTests.cs:24-44`) already fails the build for
both.

**The `-Class` guard stays correct because the impact slots keep the `attack-`
prefix.** The guard at `scripts/sfx.ps1:626-628` tests a **string prefix**, not
`SoundCatalog.IsHitLocationDriven`:

```powershell
if (-not [string]::IsNullOrWhiteSpace($Class) -and -not $Slot.StartsWith('attack-')) {
    throw "-Class applies only to an attack slot. ..."
}
```

Naming the three ranged impact slots `attack-bangkaw`, `attack-busog`, and
`attack-arquebus` therefore makes the guard correct with no edit, and it correctly
rejects `-Class` on `release-*`, `miss-*`, `clash-shield-*`, and
`misfire-arquebus`. **Renaming the impact slot to `impact-` would silently disable
the guard for the very slots that need it**, which is a decisive argument against
the tidier name.

**The variant-index rules are already satisfied.** `NN` is one-based and exactly
two digits (`src/Hukbo.Client/Audio/SoundLibrary.cs:325-353`,
`SoundCatalog.VariantIndexDigits = 2`), and `sfx.ps1`'s `-Index` validates 1
through 99, which covers every count above. Letter case never matters; every
comparison is `OrdinalIgnoreCase`.

**No `.mgcb` edit and no content build step.** The audio folder is outside the
MonoGame content pipeline; the WAV files reach the build output through a plain
copy rule at `src/Hukbo.Client/Hukbo.Client.csproj:30-33`, and
`MonoGameSoundPlayer.Load` reads the directory at startup. Dropping sixty
correctly named uncompressed PCM WAV files into
`src/Hukbo.Client/Content/Audio/` is the whole of the content change.

**The one script change worth carrying.** `-List`
(`scripts/sfx.ps1:558-564`) probes existence by testing the **bare**
`<slot>.wav`, so it already reports nine of thirteen slots as `MISSING` while all
seventy shipped files are present. At twenty-six slots it would report
twenty-two of twenty-six as `MISSING` after every file has been generated and
paid for. That is a bad signal to hand a person doing sixty generations, and the
fix is small — count matching files with the same prefix rules the game uses,
rather than testing one exact path. The plan should carry it as its own task.

The design also adopts the audio research's recommendation to **parameterise the
default prompt table by hit class** — an optional nested `Classes` table on a
hit-location driven slot, resolved at `scripts/sfx.ps1:584-590` — so the
twenty-one class-scoped ranged files become twenty-one reviewable table rows
rather than twenty-one command-line strings typed by hand across sixty paid
generations. That is one new branch in an authoring script that no test and no
gate touches.

### 9.4 The `Evaded` fix

**There is a three-way contradiction in the shipped repository and this package is
what forces it to be resolved.**

- `src/Hukbo.Core/Combat/AttackResolution.cs:46-51` documents `Evaded` as "The
  defender stepped off the line and the blow met empty air. **Carries no sound**
  and no contact effect; the absence is the signal."
- `src/Hukbo.Client/Audio/SoundCueMapper.cs:46-51` routes only `ShieldBlocked`
  away from the weapon slot, so an `Evaded` attack reaches `MapWeapon` and **plays
  the weapon's flesh-impact take**. The mapper's own remarks at `:41-44` say so:
  "`Landed`, `Parried`, `Deflected`, and `Evaded` still share one cue."
- `SIMULATION-GAME-STANDARDS.md:884-905` sides with the mapper: the spectator
  channel table's `Sound cue` row reads "weapon impact" for `Evaded`, and the
  paragraph beneath states that `Evaded` "has no sound channel of its own".

For melee the cost is small. **For a ranged weapon it is not: a missed arrow that
plays the sound of an arrow entering a body is the single most audible way a
ranged feature can read as broken.**

**The decision: build `miss-<weapon>` for the three ranged weapons and route
`Evaded` there for a ranged attacker only.** Melee weapons keep the shared impact
cue.

The reasoning for the narrow scope is that fixing melee too would rewrite a
shipped behaviour that the standards contract describes and that a pinned test
asserts — `SoundCueMapperTests.Map_KeepsTheWeaponSlotForEveryOtherResolution`
carries `[InlineData(AttackResolution.Evaded)]` at
`tests/Hukbo.Client.Tests/SoundCueMapperTests.cs:52` — and that rewrite is a
separate decision about melee, not something a ranged package should take on its
way past. The narrow form adds a **ranged row to the spectator-channel table** in
`SIMULATION-GAME-STANDARDS.md` section 14 rather than editing the melee row, and
it keeps `SoundCueMapperTests.cs:52` passing for the four melee weapons while
requiring a new ranged case beside it.

The inconsistency that remains is recorded honestly rather than smoothed over: a
melee blow that meets empty air still plays a flesh impact, and that is as wrong
as it ever was. Section 11 carries it.

Note the second trigger for `miss-<weapon>`, which is new rather than a fix: the
`Miss` event of section 5.2, emitted when a projectile's target dies before the
shot arrives. The slot therefore has two callers and is not a marginal spend.

**If a reviewer decides `Evaded` should instead stay silent**, honouring the Core
comment rather than the mapper, then `miss-<weapon>` is not built for that trigger
and its takes move to `release-<weapon>`, giving 8 / 9 / 9 release takes. That
redistribution is the correct fallback, because `release` is where extra takes do
the most work.

### 9.5 The volley capacity problem, and why this design mostly escapes it

The audio research raises a real alarm and the design's answer is that the
historically honest mechanic is also the one that does not break the mix.

**The alarm.** Three mechanisms fail in order under synchronised fire. The
per-slot budget is sixteen cues per rendered frame
(`src/Hukbo.Client/Audio/SoundCueBudget.cs:27`) against a per-frame total of
sixty-four, so forty archers loosing in one tick all land on one slot and cues
seventeen through forty are suppressed while forty-eight of the frame's total
budget go unused. The gain correction divides by the square root of the voice
count (`src/Hukbo.Client/Audio/SoundVoiceLedger.cs:87`), which is the correct
correction for **uncorrelated** material; N correlated signals approach
20·log₁₀(N) where N uncorrelated ones approach 10·log₁₀(N), and a volley is the
correlated case in its purest form. And the measured headroom at 500 agents and
normal speed is **−0.2 dBFS** — two tenths of a decibel, in exactly the
configuration that forced `CueVolume` down from 0.8 to 0.65.

**Why it mostly does not apply here.** *This design has no volley mechanic, and
section 7 explains that it must not have one.* Shooting is individual and
opportunistic because that is the only historically defensible model, so shots
are staggered by independent cooldowns — and those cooldowns are long, a hundred
ticks and more, where melee cooldowns are four to eight. At fifty archers on a
hundred-tick interval the release rate is roughly one every two ticks across the
whole army, nowhere near sixteen in a frame. The correlated-summing problem is a
property of simultaneity, and this design produces none.

**What remains true and must still be measured.** The release cue fires on one
hundred per cent of shots and lands on one slot per weapon, which is a new kind of
concentration the melee mix does not have; the total cue rate rises because a
shot now makes two sounds instead of one; and the existing headroom is two tenths
of a decibel. The measured 500-agent baseline is 5,511 cues, nothing suppressed,
peak concurrency 113 against a hard ceiling of 256, busiest single tick 15 cues
spread across five slots.

**The required action, before sixty files are paid for:** re-run the mix harness
at `tools/Hukbo.Tools.MixAnalysis` with the new slot mapping at 500 agents and
record the peak level and the per-slot peak. That harness's slot mapping, hit-class
mapping, fallback chain, and variant draw are **replicas** of the client's, and
`docs/research/SOUND-CAPACITY-MEASUREMENTS.md:468-473` requires that "if the
client's mapping changes, this harness must change with it". Adding thirteen slots
changes the client's mapping. Updating and re-running the harness is a plan task,
and it is the only way to learn what the release cue does to the peak before the
spend.

If the per-slot cap of sixteen does bind, the fix is a raised
`DefaultMaximumPerSound`, which moves
`SoundCueBudgetTests.DefaultLimits_CapOneSlotAndTheFrameAtTheDeclaredMaxima`
(`tests/Hukbo.Client.Tests/SoundCueBudgetTests.cs:59-79`) and is a deliberate,
measured change rather than a guess.

### 9.6 Sequencing: the sixty files come after the Core events

**This is user decision 3 and it is load-bearing rather than tidy.**

Eighteen of the sixty files — the release takes — cannot be triggered at all until
`BattleEventKind.Release` exists in `Hukbo.Core`, is emitted by the attack stage,
and is carried by `SoundCueMapper`. Three more, the `misfire-arquebus` pair and
the `Miss`-driven miss takes, depend on decisions in sections 5 and 9.4. Generating
sixty files before those decisions are settled risks paying for around a third of
them to sit on disk unreachable.

There is no automated protection against that outcome, and the plan should know
it rather than discover it. **No test asserts that a file on disk matches a slot.**
Every `SoundLibrary` test feeds file names in as data; nothing walks
`src/Hukbo.Client/Content/Audio/` and checks that the files there are files the
game will read. A misnamed file is ignored silently — no warning, no log line, no
panel row. And **no automated test can confirm a sound was heard**: the canonical
gate builds `Release`, where the debug log defaults to off, and its determinism
workload runs headless with no audio device. Sixty generated files can pass the
entire gate while being inaudible, misnamed, or wrong.

Three operational facts to carry into the generation session: the API refuses
anything under 0.5 seconds, so a short impact is generated long and trimmed; a
take peaking below ten per cent of full scale is rejected without writing
anything, so re-running the same command is safe and is usually all that is
needed; and the script retries a rate-limit response six times with exponential
backoff, which matters when sixty generations run in a batch.

### 9.7 The Client changes, by file

| File | Change |
| --- | --- |
| `src/Hukbo.Client/Audio/AudioTypes.cs` | thirteen new `GameSoundId` members, appended so no existing value moves |
| `src/Hukbo.Client/Audio/SoundCatalog.cs` | thirteen entries in `AllSounds`; thirteen arms in `GetBaseName`; extend `IsHitLocationDriven` to the three new attack slots |
| `src/Hukbo.Client/Audio/SoundCueMapper.cs` | three arms in `MapWeapon`; three in `MapShieldClash`; a new `MapMiss` with a ranged-only `Evaded` branch; a branch for the `Release` event |
| `src/Hukbo.Client/Audio/SoundDirector.cs` | take the agent view list alongside the event list, so a `Release` event can resolve its weapon from the source's loadout (section 5.3) |
| `scripts/sfx.ps1` | thirteen default-prompt entries; the optional per-class prompt table; the `-List` counting fix |
| `src/Hukbo.Client/Content/Audio/README.md` | thirteen rows; it is the naming contract a person reads |
| `src/Hukbo.Client/Content/Audio/PENDING-SOUNDS.md` | the swing-slot section is partly answered by the release decision |
| `tools/Hukbo.Tools.MixAnalysis` | its replica mapping, kept in lockstep, plus a re-run at 500 agents |
| `SIMULATION-GAME-STANDARDS.md` section 14 | a ranged row in the spectator-channel table |
| `docs/development/testing.md` | new `PENDING` manual smoke rows; only a human may flip one |

**No new `HitClass` values are needed.** The six acoustic classes describe where a
projectile arrives exactly as well as where a blade does, and
`HitClassCatalog.FromBodyPart` must stay total over the thirteen `BodyPart`
values, so a `Shield` or `Ground` class could never be produced from a body part
anyway. Shield and ground are slot distinctions, and the shipped
`clash-shield-<weapon>` slots already prove that pattern.

**One guard is more important than every other test in this area.**
`SoundDirectorTests.Ingest_UsesANullHitClassForAShieldBlockDespiteTheHitLocation`
(`tests/Hukbo.Client.Tests/SoundDirectorTests.cs:41-74`) pins that the director
derives the hit class from `IsHitLocationDriven` rather than from the event.
Every new classless ranged slot — release, miss, misfire, shield clash — depends
on that. Reverting it would look up a classless slot as `(slot, HitClass.Skull)`,
a key registered nowhere, which resolves `Missing` forever with no crash, no
failing test, and no complaint in the panel.

**Two tests fail by design and their failure is the decision being taken.**
`SoundCatalogTests.EveryDefinedWeapon_HasAnAttackSlot` and
`EveryDefinedWeapon_HasAShieldClashSlot`
(`SoundCatalogTests.cs:51-98`) enumerate `Enum.GetValues<WeaponId>()` and go red
the moment a ranged `WeaponId` is added, staying red until `SoundCueMapper` has an
arm for it. Their own comments say this is the designed safety net rather than a
defect.

**Nothing in the audio layer reaches a hash.** `SoundVariantSelector` draws its
take from `SplitMix64` over the tick and the source entity ID so that a replay
requests the same take without storing state, but the draw happens in the Client
and no simulation value depends on it. Adding, removing, or renumbering an audio
file cannot move a state hash or an event hash.

## 10. The standoff work — F-A and F-B only

### 10.1 The correction that has to come first

The premise this package inherited was "Hukbo battles do not terminate". That is
false as a statement about the shipped game and the distinction changes what is in
scope.

**Under the shipped default `PersistentContingentsV4`, battles terminate — every
recorded cell.** Ten measured cells, seeds 1, 2, 3, 5, and 8 at 200 and 500
agents, all decisive: 200-agent terminal ticks between 1,279 and 2,284, 500-agent
between 2,551 and 4,405, with victories to both factions.

**The 10,000-tick standoff draw is a property of `EquipmentRelativeFootworkV6` and
`V7` and of nothing else.** Those presets are registered and reachable only by
explicit selection; the Client cannot select them today, so a spectator has never
seen the standoff on screen. All ten V6 cells drew at the tick limit with between
43 and 76 per cent of the roster still alive and the two sides finishing within
nine warriors of each other, which as the baseline record puts it "is not a battle
that was nearly decided and ran a little long. It is a standoff."

**The root cause is located.** `FootworkPhase.Refuse` is not a tactical decision;
it is the name the code gives to "this warrior's movement route was rejected by
the friendly-clearance test". It has exactly one producer in the entire codebase,
`src/Hukbo.Core/Movement/WeaponMovementRules.cs:705`, reached only when the route
search rejected all three candidates, and the only test that can reject all three
is `IsLaneClearOfAllies` at `src/Hukbo.Core/Simulation/BattleSimulation.cs:2453`.
That test demands separation from every living ally of 1.15 to 1.75 times the body
diameter the collision contract permits and actively produces, so a warrior
standing next to an ally at body contact has no reachable endpoint that satisfies
it. It refuses to move, which zeroes its own retained pace, which makes it less
able to move next tick. The measured route-search failure rate is **at least
95.61 per cent**, and the refuse-plus-regroup to commit-plus-recover ratio is
**349 to 1**.

**Ranged units do not need any of this fixed**, because they ship on a V4-shaped
preset that has no refusal mechanism at all. The standoff work is in scope because
the user put battle termination in scope, not because the ranged package depends
on it.

### 10.2 F-A — split `refuseAgentTicks` by rejection reason

**What it changes.** `MovementBehaviorMetrics` counts the phase, not the predicate
that produced it, so nothing today can say *why* a route search failed. F-A adds
four derived counters alongside `RefuseAgentTicks` — no candidates built, step
endpoint rejected, direct candidate omitted, lane not clear — incremented at the
four exit sites in `TryProposeEquipmentRoute`
(`BattleSimulation.cs:2056`, `:2062`, `:2068`, `:2079`).

**Why it is first.** It costs nothing, it moves no hash, and it converts the
root-cause ranking from a source-reading argument into a measurement.
`MovementBehaviorMetrics` is derived observability that reaches neither hash, on
the same footing as `CollisionMetrics` and `CombatMetrics`, so **no new preset
version is required and every existing pinned artifact stays exactly where it
is.** It is the only candidate in the whole set that can be verified without
moving a golden.

The historical argument for it is sharp. The entire V7 workstream — sixteen tasks,
eleven gate runs, two measurement sessions — ended by naming four suspects and
explicitly declining to choose between them. Four counters would have decided that
question in an afternoon. Building the next fix without them risks repeating
exactly that outcome.

**How it is verified.** Run the 200-agent, seed-1, 10,000-tick V6 workload and
check that the four new counters **sum to 1,140,221**, reproducing the recorded
`refuseAgentTicks` exactly. That sum is the correctness test and the answer at the
same time.

**Its second, independent justification is the ranged package.** Section 6.3
introduces `AgentIntent.Holding` so a spectator can tell "chose not to close" from
"could not move". F-A is the same distinction on the measurement side: without
per-reason counters, no report can tell a working skirmisher from a broken
warrior, and no measurement of the ranged feature can be trusted.

### 10.3 F-B — make ally clearance a monotonicity constraint instead of a state constraint

**What it changes.** `IsLaneClearOfAllies` currently rejects a candidate endpoint
on its **absolute** distance to every ally, which is what makes an
already-violating configuration absorbing: the actor's own current position is
never tested, so standing still is always legal and the rule punishes movement out
of the violation rather than the violation itself. F-B changes the predicate so a
candidate is rejected only when it moves the actor **closer** to an ally it is
already too close to:

```
reject if separation < required AND separation < currentSeparationToThatAlly
```

**This is not an invented pattern.** `ShouldOmitDirectCandidate` already uses
exactly this shape twelve lines earlier in the same file —
`return endpointSquared < startSquared;` at `BattleSimulation.cs:2416` — with the
documented convention that exact equality keeps the candidate.

The result is that a warrior in a crowd may always move, provided it does not
tighten any violation. Ally clearance still shapes the line at normal density,
because at normal density the absolute and the monotone tests agree. It stops
being a trap only in the crowded case, which is the only case where it is
currently absorbing.

**Cost.** One extra squared-distance computation per candidate-ally pair — the
actor's own tick-start separation — which does not depend on the candidate and can
therefore be hoisted out of the candidate loop entirely. Hoisted, that is 250,000
squared distances per tick at 500 agents against the existing 750,000, estimated
at +0.10 to +0.15 ms on top of a measured 0.8666 ms. It is the only one of the
four movement candidates whose cost fits inside a stage that is already 3.81 times
over its budget.

**Determinism.** It changes which position an agent occupies at the end of a tick,
so it changes the state hash and the event hash, and therefore it requires a **new
movement preset value** with a new registry row and new golden expectations. V6 and
V7 keep their frozen content hashes and trajectory digests. `PersistentContingentsV4`
is not touched.

**What it costs conceptually.** The "free lane" idea in the equipment movement
research becomes advisory rather than guaranteed: a warrior may end a tick inside
another warrior's declared weapon-clearance radius. Any weapon-differentiation
claim resting on clearance alone weakens. Every existing `IsLaneClearOfAllies`
unit test asserting the absolute rule stays true for V6 and V7 and gains a
preset-scoped sibling rather than an edit.

**How it is verified.** F-A's counters first, to confirm the lane-clear rejection
count collapses; then the ten-cell matrix against the recorded baseline, checking
terminal tick, outcome, accepted attacks, and attack-capable agent-ticks. The bar
is the standards criterion: at least 19 of 20 seeds decisive before the 5,000-tick
cap with a median decisive tick at or below 5,000. Its failure mode is legible: if
the lane-clear counter does not collapse after F-B lands, the diagnosis was wrong.

**Measured on melee-only rosters first.** This is the scoping rule that keeps the
two workstreams honest. F-B's effect must not be confounded with the ranged
change, so it is measured on a melee roster, and only then is a mixed roster
measured.

### 10.4 What is excluded, and why

**F-C — reconcile the clearance radius with the collision contact distance.**
*Excluded.* It caps every loadout row's ally clearance at the body diameter, which
flattens exactly the axis a ranged loadout would most want to differentiate on: a
shooter plausibly wants **more** lateral spacing than a melee warrior, to keep a
clear lane past its own front rank. If F-C ever ships, the ranged rows need a
different differentiation axis or a documented ranged exception with its own
invariant. It also carries the largest documentation blast radius in the set,
because the six clearance values are the most visible output of six weapon
movement research sessions and flattening them erases the ordering those sessions
recorded. Its part two — a construction-time invariant rejecting a clearance
radius the collision contract cannot honour — is genuinely worth having and is
recorded here as the right **second** move if F-B alone proves insufficient. It is
not in scope for this package.

**F-D — a never-refuse truncation ladder.** *Excluded, on two independent
grounds.* It is in **direct conflict with the feature this package is building**:
"always take the longest legal step" is precisely the wrong rule for a skirmisher
at ideal range, and without the hold arm running first a bowman would creep
forward one raw unit per tick into the melee it exists to avoid. And it is
unaffordable: an eleven-rung ladder takes the per-tick iteration count from
750,000 to 3,500,000 at 500 agents, estimated at +2.0 to +2.5 ms, which would take
the movement stage from 3.81 times over its ceiling to something near fifteen. A
depth-capped form is estimated at +0.3 to +0.6 ms and is still an unbudgeted cost
on a stage that has already overrun.

**F-E — widen the candidate arc and add an explicit separation candidate.**
*Excluded as redundant and unaffordable in this package.* It is the one candidate
that is mildly **helpful** to a skirmisher — a lateral candidate is what a shooter
needs to reposition without closing — but it interacts badly with F-B: if the
constraint is already monotone, the extra candidates are mostly redundant, and the
two "should not be built together without measuring each alone first". Five
candidates instead of three is a 67 per cent increase on the dominant term,
estimated at +0.6 to +0.8 ms at 500 agents, and it loosens the emergent front by
stepping warriors sideways out of the line.

**F-F — change what counts as a decided battle.** *Excluded, and it is the worst
possible combination with this package.* Three reasons, in increasing order of
seriousness. It contradicts a standing decision recorded at
`SIMULATION-GAME-STANDARDS.md:560-562`: "No separate anti-stall or fairness escape
rule is added… `TickLimit` remains the terminal backstop." It would decide battles
on noise — every measured V6 draw finishes within nine warriors on 200 agents and
within five on 500, so a survivor-count tiebreak on a 137-versus-142 finish is a
coin flip dressed as a result. And it hides the defect rather than removing it:
the warriors would still be standing still for seventy-three per cent of the
battle and the spectator would still see a stalemate, which fails the acceptance
question `CLAUDE.md` section 6 makes mandatory. A ranged stalemate is also the
single most likely thing to produce two near-equal survivor counts at the tick
limit, so F-F is the fix that hides the bug applied to the feature most likely to
produce it. There is no honest measurement that distinguishes "the battle was
decided" from "the tiebreak fired".

### 10.5 The ordering, and the boundary

The order is F-A, then the hold arm of section 6, then F-B on melee-only rosters,
then a mixed roster. That is the research note's own recommendation and the design
adopts it unchanged.

The boundary is worth stating once more because it is easy to blur.
**F-A and F-B touch only the equipment-relative branch, which the ranged package
does not ship on.** The ranged presets are V4-shaped. Nothing in section 10 is a
prerequisite for anything in sections 4 through 9, and nothing in sections 4
through 9 depends on the standoff being fixed.

And the trap the research names has to be named here too, because this design is
the thing that could spring it. **Ranged units make the measured symptom better
and the underlying defect harder to find.** A ranged warrior frozen in `Refuse` at
long range is inside its own reach and will shoot every time its cooldown clears,
so deaths rise and some cells might decide — and it would look intentional. A
spectator watching two armies hold position and shoot at each other cannot tell
that most of those warriors are standing still because their proposals were
rejected. The design's defences against that are `AgentIntent.Holding`, which is
written only by a deliberate hold, and F-A's per-reason counters. Neither is
optional.

## 11. Phase 2, deferred

Everything in this section is out of scope for the package this document
specifies. Each entry records what it is, why it is deferred, and what would have
to be true before it is taken up.

### 11.1 Line of sight

**What.** A predicate that answers "is anything on the segment between the
launcher and the target", so a projectile can be stopped by a body in the way.

**Why deferred.** User decision: Phase 1 ships hitscan with flight time and no
blocking. The cost is real rather than nominal. Nothing anywhere in the codebase
asks what lies between two points; every spatial predicate is about one circle at
one position, and the uniform grid's four queries all walk a fixed
three-by-three neighbourhood around one centre. A segment query needs a different
traversal, a new deterministic order, and — because
`SIMULATION-GAME-STANDARDS.md:600-602` makes O(n²) equivalence in exactly one
order the acceptance test for every grid query — its own naive reference
implementation alongside `NaiveCollisionPairs.cs`,
`NaiveCollisionResolution.cs`, and `NaiveClashResolution.cs`.

**How it should be built when it is.** Not by extending the grid. Both research
documents that examined this reach the same substantive answer: an O(n) scan with
a point-to-segment test, cut down by the cheap axis-aligned rejection already used
at `BattleSimulation.cs:1050-1061`, costs 200 tests per shot at 200 agents in a
stage that is 2.35 per cent of the tick. It also *is* the naive oracle the grid
route would owe anyway, which is precisely why it is cheaper to start there. Its
home is the existing `SelectTargetsAndIntents` pass, which already computes the
squared distance and where V6 established the precedent of deriving local context
without adding a query (`BattleSimulation.cs:1069-1075`). Line of sight also needs
a "first blocker along the segment" total order with `EntityId` as the final
tie-break.

**What Phase 1 looks like without it, stated plainly.** An arrow passes through
the friendly front rank, through every enemy except the one it was aimed at, and
lands. It is the largest correctness gap in the design and section 12 carries it
as the first risk.

### 11.2 Friendly fire

**What.** A projectile striking an agent of the launcher's own faction.

**Why deferred.** It depends entirely on line of sight — without a blocking rule
there is nothing for a projectile to hit on the way — so it cannot ship first. The
machinery is closer than it looks: `_damageTotals` is already faction-blind,
keyed by target index only, and the damage event already carries the victim's
faction. What does not exist is any path by which a cross-faction attack becomes
an ally-facing one, and the target scan hard-excludes same-faction candidates at
`BattleSimulation.cs:1015`.

**What it would move.** The defence-attributable share band, the both-factions-win
test, and every intuition about roster balance. It is a substantial gameplay
change and deserves its own design document rather than a subsection of this one.

### 11.3 Ammunition

**What.** A per-agent counter, a reserve, a resupply rule, an "out of ammo" state,
and an intent for a warrior who has run dry.

**Why deferred.** Constrained from two directions at once. `CLAUDE.md` section 9
forbids projectile ammunition before an authorizing gate, and the user's decision
lifted the projectile clause and not the ammunition one. And the sources give
nothing to build on: no ammunition quantity, no quiver capacity, no resupply
arrangement, and no statement that a force ran out, anywhere in the corpus.

**What is lost by deferring it.** The best-attested detail about the thrown spear
— picking up the same shaft four to six times at Mactan — is an ammunition
behaviour, and Scott's reading that "these fine spears were thrown only where it
was possible to retrieve them" is an economic calculation made per throw. Both are
inexpressible without a counter. This is the deferral with the highest historical
cost in the package.

### 11.4 Regional rosters and scenario place tags

**What.** A scenario that carries a place and a date — `Mactan — 1521`,
`Manila — c.1570` — and a roster whose weapons are constrained by it.

**Why deferred.** No such concept exists: `Scenario` has no place, no date, and no
regional constraint, and `RosterCounts` applies the same proportions to both
factions. It is also the mechanism that would let the Sumpit return honestly, and
it is the correct answer to `CLAUDE.md` section 7's prohibition on generalising
one region to the whole archipelago. Until it exists, the *bangkaw* label is
Visayan-anchored and used archipelago-wide, which is a known and labelled
imprecision.

### 11.5 The Sumpit — Blowgun

**Why it was dropped and what would bring it back.** Dropped by user decision on
the evidence: the word appears in none of the Blair and Robertson volumes
consulted, the name clears the hundred-year rule only on a
Proto-Malayo-Polynesian reconstruction with pan-Philippine reflexes rather than on
a dated document, and every sixteenth-century attestation of the weapon places it
at Palawan or Cagayan Sulu — and Pigafetta explicitly identifies the Cagayan Sulu
people as Bornean exiles, which makes that observation one of a Bornean community
in Philippine waters rather than of an indigenous Philippine tradition. No source
consulted places a blowgun in the Visayas, at Mactan, in Cebu, in Manila, or in
Luzon.

Two things would change the calculus. A dated documentary attestation of the word
— San Buenaventura's *Vocabulario de la lengua tagala* (1613) or Méntrida's
*Bocabulario de lengua bisaya* (1637), under the headword *cerbatana* — would
upgrade the name's tier to the same footing as *busog*. And the regional scenario
tags of 11.4 would let the weapon appear where it is attested and nowhere else.

If it ever returns, three design facts from the research come with it. Its
documented range is **shorter than the bow's** on Artieda's authority, which is
the only sourced range statement in the entire corpus. A 21-centimetre wooden dart
carries almost no kinetic energy, so the weapon works entirely by delivering a
toxin — "a blowgun modelled as a low-damage projectile with no toxin mechanic is
not a blowgun; it is a bad bow", which means it depends on 11.6. And its single
most distinctive documented feature is one a spectator could actually see:
Pigafetta records an iron spearhead fastened at the muzzle, so that "when they
have shot all their arrows they fight with that".

### 11.6 Poison

**Why deferred.** Section 2 gives the three reasons: it is new state-carrying
mechanic with its own determinism surface; it is explicitly regional in the
sources and `docs/research/HISTORICAL_1500s_WEAPONS.md` already forbids presenting
it as universal; and two independent sources describe an antidote, so modelling it
as irreversible would be wrong even in scope. If it is ever built it must be a
per-agent or per-scenario property with a regional basis, never a flat property of
a weapon class, and **no plant name may appear in player-facing text**.

### 11.7 The melee sidearm

**What.** A second weapon per agent, so that a missile-armed warrior draws a blade
when reached.

**Why it matters.** It is the single best-attested behavioural fact about
missile-armed men in this record, unanimous across every weapon list in the
corpus, and the tactics research argues it dissolves the whole "ranged unit"
concept: the sixteenth-century fighter "is a fighter whose behavior changes with
distance", which is a much better fit for an emergent agent simulation than a
role-based line. Hukbo gives each agent exactly one weapon and one
`AttackRangeRaw`, so the change is structural rather than additive. It is the
design's first choice for what to build next.

### 11.8 The melee `Evaded` cue

Section 9.4 fixes `Evaded` for ranged weapons only. A melee blow that meets empty
air still plays a flesh impact, contradicting the Core comment at
`src/Hukbo.Core/Combat/AttackResolution.cs:46-51`. Resolving that is a decision
about melee and about the spectator-channel contract at
`SIMULATION-GAME-STANDARDS.md:884-905`, and it belongs to whoever takes it, not to
this package.

### 11.9 Also deferred, briefly

- **A morale or terror model**, which is what "more to terrify than to kill" would
  need and which `CLAUDE.md` section 9 defers. The tactics research is blunt that
  implementing a firearm terror effect on that quotation "would be an invention
  wearing a citation".
- **The Bronze Verso / Small Culverin.** Better documented as a locally
  *manufactured* weapon than the imported arquebus is, and excluded solely because
  a crew-served weapon with a mount and an emplacement footprint is a different
  simulation problem. `docs/research/HISTORICAL_1500s_WEAPONS.md:254-255` already
  reserves it. Whether the name *lantaka* clears the hundred-year rule is
  genuinely unresolved and must be settled before the name is used, not assumed
  either way.
- **Thrown stones, mud, and fire-hardened stakes**, extremely well documented at
  Mactan in 1521 and Sarangani in the 1540s, excluded as a scope decision and a
  legitimate future entry.
- **An arquebus misfire mechanic**, whose two generated sound files are noted in
  section 9.1 as having no Phase 1 trigger.
- **A projectile identifier linking a release to its impact** in the event stream,
  for which bits 40 through 63 of `BattleEvent._combatContext` are free.
- **A sprite-frame animation pipeline**, already backlogged at
  `docs/plans/TODO.md:41-55` and to be revisited only as its own design document.

## 12. Risks, and what could make this design wrong

Ordered by how badly each one damages the package if it lands.

### Risk 1 — Phase 1 arrows pass through everything, and it looks wrong

**The risk.** With no line of sight, a projectile is resolved against the agent it
was launched at and nothing between them matters. An archer in the third rank
shoots through two ranks of its own army and through the enemy front rank to
strike the man it selected. `IsWithinAttackRange`
(`BattleSimulation.cs:4132-4139`) is a bare squared-distance comparison and
nothing anywhere looks at what stands between two centres — the reach research
puts it plainly: a weapon declaring a very long reach "would pass every existing
validation and would simply work, striking through allies and through the enemy
front rank, because no code looks at what lies between the two centres."

**Why it is accepted anyway.** It is a user decision, it is the only way to keep
Phase 1 small enough to measure, and the alternative — building a segment query,
its deterministic traversal order, and its naive oracle — is a package of its own.

**What makes it worse than it sounds.** The whole design turns on a spectator
being able to discover things by watching. This is the one effect they *cannot*
discover: an arrow with a clear lane and an arrow through three bodies are drawn
identically. It is the only place in the package where the game asserts something
false and gives the spectator no way to notice.

**Mitigation.** Say so in the plan, in the smoke checklist, and in a code comment
at the launch site. Do not ship a scenario that makes it maximally visible. Treat
Phase 2 as owed rather than optional.

### Risk 2 — the hold rule breaks termination

**The risk.** Every existing termination test asserts that agents eventually
converge and kill each other, and a rule that legitimately holds warriors apart
works directly against them. `NoBattleUnderPersistentContingentsStallsAtTheTickLimitAcrossSeedsOneThroughTwenty`,
`SeedsOneThroughTwentyProduceVictoriesForBothFactions`
(`tests/Hukbo.Core.Tests/BattleSimulationTests.cs:566`), the 200-agent canonical
battle (`:645`), the last-stand suite, and the contingent deadlock suite are all
in that class.

**Why it is genuinely uncertain.** Two forces pull opposite ways and the two
research documents disagree about which wins — section 6.6 records the
disagreement and its resolution. Longer reach means far more attack-capable
agent-ticks and shorter battles; the hold rule removes closing pressure for the
ranged fraction and lengthens them. Nothing measured predicts the sum.

**Mitigation.** Measure before integrating: the ten-cell matrix against the
recorded V4 baseline of 1,279 to 4,405 ticks, plus the twenty-seed bar. If the
bars fail, the tuning levers in order are the ranged roster share, the standoff
distance as a fraction of reach, and the shot interval. All three are labelled
gameplay values, so tuning them costs nothing historically. **What must not
happen is re-pinning a golden to go green**, and the second thing that must not
happen is adopting F-F to make the numbers pass.

### Risk 3 — the per-tick allocation budget

**The risk.** The enforced ceiling is 16,384 bytes per 1,000 warm ticks with a
4,096-byte growth tolerance at 12 agents per faction
(`tests/Hukbo.Core.Tests/BattleSimulationTests.cs:393-395`). A per-projectile heap
allocation is roughly eleven times over it at a quarter-archer roster, and forty
to fifty times over at half a roster with a shorter interval. A single boxed
enumerator per tick is on its own nearly three times the ceiling.

**Why it is a live risk rather than a solved one.** The measured windows today sit
between 0 and 2,064 bytes over 1,000 ticks. There is no slack being carried; the
budget is tight because the tick loop currently allocates essentially nothing.

**Compounding it: the stale documentation.** `SIMULATION-GAME-STANDARDS.md:877`
still records a 900,000-byte collision allocation ceiling and
`docs/development/testing.md:1997` repeats it. **Both are stale by a factor of
fifty-five.** An implementer who reads the standards rather than the test will
believe there is room for a per-projectile object. Correcting both documents is a
plan task and it is a safety measure, not housekeeping.

**Mitigation.** Pooled struct arrays, index iteration, a declared ceiling, and an
allocation test run *with projectiles in flight* rather than on a melee roster.

### Risk 4 — the defence-attributable share leaves its band

**The risk.** `CombatMetrics.DefenceAttributableShare` must stay inside 0.25 to
0.45 across seeds 1 through 20 at 200 agents
(`tests/Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs:687`). Every
projectile impact is an accepted attack, changing the denominator, and three new
attacker weapons need cells in the weapon-intercept matrix, changing the
numerator. The sibling relationship that shielded roster entries absorb more blows
than shieldless ones (`:797`) weakens as the ranged share rises if projectiles are
poorly stopped by shields.

**What makes it awkward rather than merely fiddly.** There is **no evidence at all**
to guide the numbers. Not one sixteenth-century sentence describes a Philippine
projectile striking a Philippine shield or corselet. Every cell is invented, must
be marked `PROVISIONAL`, and must never be cited back into the research documents
as a measurement.

**Mitigation.** Treat the ranged clash cells as calibration work with the band as
the acceptance criterion, and resist the temptation to widen the band.

### Risk 5 — the quad and cull budgets

**The risk.** Headroom is 7.9 quads per pawn at 500 units, and the 24-quad
baseline excludes the swing trail's six, so 500 pawns all mid-swing already leaves
only 968 quads of slack. A naive bow drawn as stave, arrow, and string would be
six to nine quads per pawn and would breach the budget on its own. Separately, a
ranged weapon line reaching further than the Kalis's 24.2 units fails
`ConservativePawnCullTests` even though nothing calls the type, and a pose that
extends further than any swing pose can lets a pawn escape its own pose-blind cull
rectangle and be clipped at the panel edge.

**Mitigation.** One new rectangle per ranged pawn at most, reusing the existing
secondary-equipment slot; no trail-equivalent; ranged weapon lines held inside the
existing envelope; and the two pinned quad tests moved deliberately with the
arithmetic in the commit message.

### Risk 6 — sixty paid files that nothing can trigger

**The risk.** Eighteen release takes depend on `BattleEventKind.Release` existing
and being carried by the mapper; the miss takes depend on the `Evaded` decision
holding; the two misfire takes have no Phase 1 trigger at all. A misnamed file is
ignored **silently** — no warning, no log line, no panel row — and **no test walks
the audio folder to check that the files there are files the game will read**. The
canonical gate builds `Release` with logging off and runs its determinism workload
headless with no audio device, so sixty files can pass the entire gate while being
inaudible.

**Mitigation.** Generate after the Core events exist, which is user decision 3.
Verify each file by playing it, in a session whose result goes in the manual
checklist, because that is the only verification that exists.

### Risk 7 — the mix headroom is two tenths of a decibel

**The risk.** The measured peak at 500 agents and normal speed is −0.2 dBFS, in
exactly the configuration that forced `CueVolume` down from 0.8 to 0.65. Adding a
release cue roughly doubles the cue rate and concentrates it on one slot per
weapon.

**Why it is smaller than it first appears.** The design has no volley mechanic and
cannot have one (section 7), so the correlated-summing case the audio research
warns about does not arise; shots are staggered by long, independent cooldowns.

**Mitigation.** Update the mix harness's replica mapping and re-run the 500-agent
rendering **before** the sixty files are paid for.

### Risk 8 — the feature conceals the standoff defect

Section 10.5 states it: ranged units make the measured symptom better and the
underlying defect harder to find, and a spectator watching two armies hold
position and shoot cannot tell a chosen distance from a rejected proposal. The
defences are `AgentIntent.Holding`, which has exactly one producer, and F-A's
per-reason counters. **If either is dropped from the plan to save time, the
package has removed a diagnostic signal and shipped a feature that looks like the
bug it hides.**

### Risk 9 — the gate never sees the feature

If the Client runs the ranged presets and the gate's determinism workload runs
`Scenario.CreateDefault` on V4, the canonical gate exercises none of this. That is
the same blind spot that let V6 and V7 draw every seed while the gate stayed
green. The plan owes a ranged determinism workload and a ranged sibling for the
twenty-seed termination test.

---

### What could make this design wrong

Six things, each of which would invalidate a load-bearing choice rather than a
detail.

**1. If a spectator cannot tell a held skirmisher from a stuck warrior even with
`AgentIntent.Holding` on screen.** The hold arm is the core new mechanic and its
whole justification is that it makes "chose not to close" a first-class,
observable state. If the inspector reason code and the visible contrast with
advancing comrades are not enough — if the picture still reads as a stalemate —
then the design has answered acceptance question 8 wrongly and the feature is
incomplete by the repository's own standard.

**2. If the phase-from-cooldown projection turns out to be too coarse.** Section
8.1 diverges from the pose research by deriving the ranged phase from the existing
cooldown countdown rather than storing new authoritative state. That is much
cheaper and it is hash-neutral. It is also a bet: it assumes the cooldown is a
good enough proxy for a draw-and-loose cycle. If the phases read as arbitrary — if
a warrior appears to be drawing when nothing is happening, or the release moment
does not line up with the projectile leaving — then the research's recommendation
was right and the phase has to become real per-agent state with a hash fold, which
is a substantially larger change.

**3. If measurement says the ranged share has to be tiny to hold termination.**
The evidence supports a ranged minority, so a small share is not historically
embarrassing. But if the share has to be very small to keep battles decisive, the
feature may be invisible in an ordinary battle, and a feature a spectator rarely
sees fails acceptance question 1 for a different reason than the one section 3
answers.

**4. If flight time turns out to be unreadable at battle scale.** The projectile
is the single most legible thing in the package on paper. At 500 units, at a
camera zoom where a pawn is a few pixels, a one-segment line crossing eighty world
units over a handful of ticks may be invisible. If it is, the entire justification
for carrying projectile state in `Hukbo.Core` — rather than resolving ranged
attacks instantaneously — collapses, and the honest response is to remove the
flight time rather than to keep unobservable authoritative state.

**5. If Phase 2's line of sight proves unaffordable.** The design's Phase 1 / Phase
2 split assumes Phase 2 happens. If a measured segment query is too expensive on
top of a collision stage that is already 63 to 75 per cent of the tick, then
Phase 1's shoot-through-everything behaviour becomes permanent rather than
temporary, and a permanent version of it is a different and much weaker design
than the one this document proposes.

**6. If the historical framing is judged insufficient.** Section 7 declares the
deployment model an invention and states the corrections — no screen, no volley,
no ratio, and the Limahong misattribution. If a reader concludes that shipping
ranged units at all implies a tactical structure the sources do not support, no
amount of labelling fixes it, and the answer would be to build the melee sidearm
of section 11.7 first so that the game has warriors whose behaviour changes with
distance rather than a class of warriors who are ranged.
