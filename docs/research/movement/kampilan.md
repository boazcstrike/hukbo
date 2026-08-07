# Kampilan — Great Blade Movement Research PRD

**Status:** Research-backed design input; not implementation authorization

**Scope:** Movement only: individual locomotion, formation placement, commitment and recovery movement, disengagement, regrouping, and count-aware decisions

**Historical confidence:** Mixed. The existence of a large cutting sword is documented for the Mactan account; identifying it specifically as a kampilan is a provisional reconstruction. Surviving kampilan examples are much later.

## Executive conclusion

Hukbo should treat Kampilan movement as a long-reach, lane-sensitive battlefield role whose wielder prefers deliberate entry, enough lateral clearance to complete a committed action, and an exit or regroup lane after the commitment. The role should be strongest when allies preserve space and distract or contain nearby enemies, and increasingly fragile when short-reach opponents enter from multiple bearings.

This is an evidence-led gameplay reconstruction, not a recovered sixteenth-century Kampilan system. Antonio Pigafetta documents difficult footing, organized groups, lateral evasive movement, focused pressure, staged retreat, pursuit, and a “great sword” at Mactan. He does not document a Kampilan footwork curriculum or a two-handed grip. The National Museum of the Philippines describes kampilan among long blades designed for single-handed use, often with a shield. Hukbo currently implements `Kampilan` as `TwoHanded`. That implementation identity must remain visibly separate from historical claims.

The initial movement model should therefore be conservative:

- Preserve the shared human movement baseline; do not create passive speed buffs.
- Express identity through approach distance, turn and lateral constraints, commitment/recovery timing, ally clearance, target-selection posture, and regroup choices.
- Let counts change decisions, not physical speed.
- Use facing for locomotion and space management only. Do not introduce directional damage, parry, interception, or shield-facing rules.
- Calibrate for role viability in mixed battles rather than equal duel win rates.

## Evidence method and labels

Every historical statement in this document uses the repository’s confidence scheme:

- **Documented:** directly described or pictured in a sixteenth-century
  source.
- **Documented, form uncertain:** an object or practice class is supported, but its exact sixteenth-century form or use is unresolved.
- **Provisional reconstruction:** a movement rule inferred for coherent gameplay from incomplete evidence.

Modern HEMA, kendo, Filipino martial arts, sports biomechanics, and combat-community observations are analogies only. They can identify useful variables or test scenarios, but cannot establish pre-1521 Philippine technique.

## Implemented identity versus historical identity

### Current Hukbo identity

The current V2 combat preset defines Kampilan as:

- damage `15`;
- reach `16` world units;
- cooldown `7` ticks;
- grip `TwoHanded`.

All loadouts currently share a movement speed of `3` world units per tick, and
the standard body radius is `4.25` world units. The weapon values come from
`PhilippineCombatPresetV2.cs`; movement speed and body radius come from
`Scenario.cs`. They are implementation facts, not historical measurements.

Within the current six-loadout roster, Kampilan is the longest-reach melee option. This PRD uses that stable gameplay identity as its design starting point without claiming that the specific reach, cadence, or grip is historically measured.

### Historical boundary

- **Documented:** Pigafetta’s Mactan narrative reports a large sword, translated as a “great sword” like a great scimitar, used in the final close combat.
- **Provisional reconstruction:** identifying this weapon specifically as a kampilan.
- **Documented, form uncertain:** A current National Museum interpretation
  groups kampilan with long blades designed for single-handed slashing and
  thrusting while the other hand could carry a shield. Applying that
  interpretation directly to the sixteenth century is
  **Provisional reconstruction**.
- **Documented, form uncertain:** later museum kampilan objects are roughly 95–102 cm overall and vary substantially in mass. They establish later material forms, not a sixteenth-century norm.
- **Unknown or unsupported:** “Kampilan was a heavy two-handed sword with a
  known historical footwork system.”

Accordingly, “two-handed Kampilan” is a current Hukbo loadout abstraction. If that abstraction remains, its movement restrictions are balance hypotheses derived from long reach and committed space use, not historical facts.

## Physical handling and movement implications

The physical record does not justify a single numeric handling profile. It does support three variables worth representing:

1. **Reach management.** A longer implement rewards entering only far enough to threaten, then preserving separation. This is a general mechanical inference, not a Kampilan-specific documented doctrine.
2. **Clearance.** A long blade needs an unobstructed lane. Nearby companions, terrain, and enemies can restrict safe motion. The Mactan account directly documents reef and water limiting approach; local repository research also treats companions and uneven ground as material constraints.
3. **Commitment and reset.** A weapon action has an entry, committed interval, recovery, and renewed guard. Generic implement biomechanics and modern fencing studies show that free opposition differs from drills and that distance changes coordination. They justify modeling phases, not copying modern techniques or timings.

The intended feel is deliberate rather than slow: the wielder can travel with the same human baseline as everyone else, but should not pivot, strafe, and reverse at full effectiveness while committed.

## Full movement lifecycle

### 1. Formation placement

**Provisional reconstruction:** place Kampilan units on an open edge, in a loose front rank, or behind a teammate-created lane. Avoid packing multiple Kampilan units shoulder-to-shoulder. A unit should prefer a location that preserves:

- a forward threat lane;
- lateral clearance from allies;
- a retreat or regroup direction;
- visibility of the nearest hostile cluster.

This is contingent positioning, not a rigid formation doctrine. Mixed groups should not be rewritten into fixed historical formations.

### 2. Approach

The wielder should close toward the edge of reach rather than directly toward the target’s center. Facing should turn progressively toward the intended travel/threat direction. When several enemies are nearby, the approach should favor an outer enemy and keep the hostile cluster on one side where possible.

Approach behavior may be conditioned by:

- nearest hostile distance and bearing;
- nearest ally distance and bearing;
- local ally and hostile counts;
- available lateral clearance;
- whether a regroup anchor exists;
- faction-wide advantage or disadvantage posture.

### 3. Entry and commitment

The unit commits only when a target is plausibly reachable after the entry step and the clearance lane is acceptable. Commitment limits sharp reversal and target switching for a short deterministic window. It must not alter damage, collision, or hit rules.

If a friendly unit enters the clearance envelope before commitment, delay or choose another lane. If that happens after commitment begins, movement should favor a shortened advance or controlled recovery; do not add friendly damage.

### 4. Recovery and reset

After a committed action, the preferred movement is a short exit, lateral reset, or stationary reorientation. Immediate repeated forward pressure should be less attractive unless the target is isolated and local numbers are favorable. Recovery ends in a renewed approach posture.

### 5. Disengagement and regrouping

When locally outnumbered, flanked by bearing spread, or deprived of an exit lane, the unit should yield toward the nearest viable ally cluster. Disengagement is tactical and temporary:

- no morale state;
- no panic or rout;
- no surrender;
- no permanent retreat order.

The unit remains combat-capable, keeps threats broadly in view, and re-enters when local conditions improve.

### 6. Pursuit and retreat

Pigafetta documents pursuit and a retreat conducted “by degrees” at Mactan. That supports the existence of controlled withdrawal and pursuit in this battle, not Kampilan-specific technique. In Hukbo, a Kampilan unit should pursue cautiously enough to retain an ally or exit reference. It should not chase a single withdrawing enemy through a denser hostile cluster merely because faction totals are favorable.

## Six-loadout 1v1 movement matrix

These are movement hypotheses, not claims of historical matchups. Shield rows affect spacing and approach only; shield activation, directional guard, and interception remain deferred.

| Opponent | Kampilan movement objective | Entry and reset behavior | Primary failure to test |
|---|---|---|---|
| Kampilan | Contest the outer edge of equal reach without endless circling. | Use measured lateral adjustment; after either commitment, create separation before re-entry. | Symmetric orbiting, simultaneous permanent retreat, or deterministic stalemate. |
| Wasay | Preserve the reach margin and deny a clean close approach. | Commit from the edge; reset laterally after a miss or contact rather than trading in place. | Kampilan kites forever, or Wasay always enters without cost. |
| Kalis | Keep the shorter, faster-cadence opponent outside the inside lane. | Prefer a shallow entry and prompt exit; turn toward a flank attempt without gaining extra speed. | Kalis never closes, or Kampilan cannot respond once crossed. |
| Kalis + Tall Hardwood | Maintain space without inventing shield-side attacks. | Avoid repeated frontal overcommitment; circle only when space exists, then reset. | Directional shield logic leaks into movement, or duel becomes an orbiting stalemate. |
| Itak | Use reach to discourage direct closure while preserving an exit. | Commit selectively; yield and face when the Itak crosses the preferred band. | Short-reach unit is permanently zoned with no viable entry. |
| Itak + Tall Hardwood | Control distance and avoid being pinned by compact forward pressure. | Reposition around congestion, not around an assumed shield opening. | Shield grants an undocumented movement bonus or Kampilan disengages forever. |

## 2v2, group, and count-aware behavior

### Local counts

“Local” means a deterministic perception radius measured from current authoritative positions. Counts should be evaluated separately for allies and hostiles and, where useful, by loadout. Counts change posture and target choice; they never multiply speed.

Candidate posture bands:

> **Provisional reconstruction:** Gameplay tuning; no historical measurement.

| Local hostile-to-ally relationship | Kampilan posture |
|---|---|
| Hostiles fewer than allies | Pressure an outer target while preserving ally clearance. |
| Rough parity | Hold the preferred reach band; commit when an ally occupies another hostile. |
| Hostiles at least 1.5 times allies | Shorten commitment preference and bias toward a regroup lane. |
| Hostiles at least 2 times allies | Refuse deep entry; disengage toward the nearest viable allied cluster unless already committed. |

Ratios are candidate thresholds, not historical measurements. Include the acting unit consistently on its own side when calculating them.

### 2v2

Two Kampilan allies should avoid synchronizing into the same lane. Candidate behavior is staggered commitment: one threatens or recovers while the other preserves separation. In a mixed pair, the Kampilan should normally use the outer lane and avoid cutting across the ally’s direct approach.

Modern multiple-opponent HEMA club observations report that allies can obstruct one another and that two-versus-three often decomposes into a one-versus-two and a one-versus-one. This is a scenario-design analogy using different weapons and culture, not historical evidence for Philippine combat.

### Outnumbered cases

- **1v2:** keep both threats within the smallest practical bearing spread, avoid entering between them, and yield toward an ally anchor if one exists.
- **2v3:** remain close enough that the third hostile cannot freely isolate one ally, but retain clearance; prefer an outer hostile.
- **3v5:** do not form a static clump. Fall back by local lanes, re-evaluate counts, and allow temporary pairings.

### Homogeneous and mixed groups

Homogeneous Kampilan groups need wider spacing and staggered lanes. Mixed groups should allow shorter-reach allies to occupy different bearings rather than requiring them to mirror Kampilan spacing. No special commander or formation-state system is required by this research.

### Large battles

At 100v100 and 250v250, faction-wide totals may set a broad contingent posture: pressure, hold, or conserve. Individual decisions must still use local perception. A global numerical advantage must not make an isolated Kampilan unit charge a locally superior cluster; a global disadvantage must not force every unit into synchronized retreat.

Count queries must have bounded cost and deterministic ordering. They should not create persistent target caches or change stable tie-breakers.

## Provisional candidate ranges

These values are starting hypotheses for playtests if Hukbo retains the current two-handed gameplay identity. Multipliers are relative to the shared movement baseline and apply by locomotion state, not to the underlying human maximum.

> **Provisional reconstruction:** Gameplay tuning; no historical measurement.

| Variable | Candidate range | Initial default | Purpose |
|---|---:|---:|---|
| Forward approach multiplier | 0.95–1.00 | 0.98 | Preserve normal travel while making identity arise elsewhere. |
| Lateral movement while engaged | 0.75–0.90 | 0.82 | Make lane choice meaningful without preventing correction. |
| Backward movement while engaged | 0.60–0.78 | 0.70 | Favor controlled yielding over full-speed reverse kiting. |
| Turn budget while committed | 1/16–1/10 turn per tick | 1/12 | Limit instant reversal; calibrate against tick rate and readability. |
| Preferred entry distance | 1.05–1.25 × attack reach | 1.15 × | Begin the entry decision before reaching strike distance. |
| Commitment duration | 2–4 ticks | 3 ticks | Create a readable movement decision without changing cooldown. |
| Recovery duration | 2–4 ticks | 3 ticks | Create an exit/reset window without altering attack cadence. |
| Ally-clearance radius | 1.25–1.75 body diameters | 1.50 | Reduce shared-lane crowding. |
| Regroup trigger | 1.5:1–2:1 local hostile ratio | 2:1 | Avoid deep commitment under clear local disadvantage. |
| Regroup release | 1:1–1.5:1 local hostile ratio | 1.25:1 | Add hysteresis and prevent posture flicker. |

Movement multipliers must never compound into an increase above the common baseline. Exact numbers require deterministic scenario testing.

## Role viability and acceptance hypotheses

Kampilan is viable when it can create and preserve a reach lane in groups without becoming a universal dueling winner.

Research-phase acceptance hypotheses:

- In equal-count mixed battles, Kampilan contributes through spacing and outer-lane pressure.
- Shorter-reach loadouts retain a credible route to close distance, especially with ally distraction or bearing spread.
- Kampilan performance declines in dense friendly congestion and local encirclement.
- Outnumbered units disengage sometimes, but do not oscillate indefinitely or abandon combat.
- Mirrored matchups resolve without permanent orbiting.
- Count-aware posture produces the same ordered decisions and state hashes for the same seed and commands.
- At 100v100 and 250v250, count logic remains bounded and does not introduce unbounded caches.

Equal 1v1 win rates are not required. The target is a legible role that remains useful in team battles.

## Calibration questions

1. What percentage of commitments are delayed by ally clearance in homogeneous versus mixed groups?
2. How often does a committed unit reverse target or heading beyond its allowed turn budget?
3. Can every shorter-reach loadout enter Kampilan range in at least one reproducible tactical condition?
4. How long do mirrored Kampilan duels spend outside engagement range?
5. Does the regroup trigger produce posture flicker near a count boundary?
6. In 1v2, 2v3, and 3v5, does the unit keep enemies on one side more often than the baseline?
7. Does global posture improperly override an individual’s local danger?
8. Do clearance rules create traffic jams at 100v100 or 250v250?
9. Are commitment and recovery visually readable at the fixed tick rate?
10. Which movement rule accounts for any win-rate change: spacing, target selection, commitment, or regrouping?

Record state hash, event hash, winner, ordered events, engagement time, path distance, heading changes, clearance delays, commitment cancellations, local ratios, and regroup transitions.

## Unknowns and non-goals

Unknowns:

- the exact form and identity of Pigafetta’s “great sword”;
- whether a specific pre-1521 Kampilan movement tradition can be documented;
- typical sixteenth-century dimensions, mass, grip, and shield pairing;
- historically authentic formation placement;
- numeric locomotion, turn, commitment, and recovery values.

Non-goals:

- claiming the Kampilan killed Magellan;
- presenting modern FMA or HEMA footwork as pre-1521 continuity;
- damage, reach, cooldown, or hit-resolution rebalance;
- directional damage, blocks, parries, shield arcs, interception, or friendly fire;
- morale, rout, surrender, terrain, pathfinding, ammunition, or campaign systems;
- fixed historical formations or a mixed-contingent rewrite;
- passive speed bonuses based on weapon or unit counts.

## Evidence ledger

| ID | Atomic claim | Place/date | Source and exact locator | Source class | Evidence label | Transfer limit | Movement consequence |
| --- | --- | --- | --- | --- | --- | --- | --- |
| KP-01 | Fighters crossed thigh-deep water and rocks. | Mactan, 1521 | [Pigafetta](https://www.gutenberg.org/cache/epub/74723/pg74723-images.html), printed pp. 100–102, reef-crossing paragraph | Colonial eyewitness narrative | **Documented** | Encounter terrain only | Test constrained approach; do not add terrain in this slice |
| KP-02 | Defenders used grouped pressure, lateral evasion, staged retreat, and pursuit. | Mactan, 1521 | Same, landing through retreat paragraphs | Colonial eyewitness narrative | **Documented** | Not weapon-specific doctrine | Permit lateral reset, bounded disengagement, and pursuit states |
| KP-03 | A “great sword” appears in the final close combat. | Mactan, 1521 | Same, Magellan-death paragraph | Colonial eyewitness narrative | **Documented** | Naming it Kampilan is provisional | Preserve great-blade role without claiming identity |
| KP-04 | The consulted manuscript record supplies period provenance. | Cebu/Mactan account, 1521 | [Library of Congress](https://www.loc.gov/resource/gdcwdl.wdl_03082/?st=gallery), catalog description | Manuscript catalog | **Documented** | Catalog does not interpret the weapon | Source provenance only |
| KP-05 | A current museum synthesis describes Kampilan as a long, single-handed, shield-compatible blade. | Philippines, current synthesis | [National Museum](https://www.nationalmuseum.gov.ph/our-collections/ethnology/weapons-and-shields/), “Long blades” | Museum synthesis | **Documented, form uncertain** | Spans later regions/forms; no 1500s handling | Treat Hukbo's two-handed row as gameplay abstraction |
| KP-06 | A later Kampilan is 95.3 cm overall. | Mindanao attribution, before 1916 | [Cleveland Museum 1916.752](https://www.clevelandart.org/art/1916.752), dimensions | Museum object | **Documented, form uncertain** | Later comparative form | Ask clearance/scale questions only |
| KP-07 | A later Kampilan is 94.6 cm and 714.4 g without scabbard. | Lanao del Sur, 18th–19th c. | [Met 27824](https://www.metmuseum.org/art/collection/search/27824), object metadata | Museum object | **Documented, form uncertain** | Too late for period norm | Demonstrate form variation only |
| KP-08 | A later Kampilan is 101 cm and 1.30 kg. | Southern Philippines attribution, later collection | [British Museum As1954-07-194](https://www.britishmuseum.org/collection/object/A_As1954-07-194), object details | Museum object | **Documented, form uncertain** | Too late for period norm | Do not derive a speed |
| KP-09 | Hafted-tool motion has coordinated phases. | Modern experiment | [Biomechanical investigation](https://pmc.ncbi.nlm.nih.gov/articles/PMC8923818/), Results, Figures 4–5, Table 2 | Experimental analogy | **Provisional reconstruction** | Tool task, not sword combat | Test commitment/recovery only |
| KP-10 | Opposed distance can change coordination mode. | Modern kendo study | [Critical interpersonal distance](https://pmc.ncbi.nlm.nih.gov/articles/PMC3527480/), Abstract and Results | Combat-sport analogy | **Provisional reconstruction** | No copied distance/timing | Test preferred-distance transitions |
| KP-11 | Free opposition differs from rehearsed HEMA strikes. | Modern study | [HEMA biomechanics](https://noah.nrw/ubbihs/content/titleinfo/5139823), Abstract | HEMA analogy | **Provisional reconstruction** | Different weapons/rules | Reject choreography-derived timing |
| KP-12 | A modern club report identifies ally obstruction and split multiple-opponent geometries. | Modern community practice | [Armoury](https://armoury.co.za/some-thoughts-on-multiple-opponent-scenarios/), caveats, 1v2, 2v3 | Community analogy | **Provisional reconstruction** | Synthetic European weapons | Add clearance and bearing tests |
| KP-13 | Repository policy makes the Magellan-to-Kampilan identification provisional. | Repository synthesis, 2026 | [Historical weapons](../HISTORICAL_1500s_WEAPONS.md), named-blade caution | Repository synthesis | **Provisional reconstruction** | Inherits cited gaps | Keep paired descriptor and caveat |
| KP-14 | Repository battle research rejects universal named footwork. | Repository synthesis, 2026 | [Individual combat](../battles/04-deep-past-individual-combat.md), movement section | Repository synthesis | **Provisional reconstruction** | General, not Kampilan-specific | Use generic lifecycle names |
