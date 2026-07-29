# Wasay — Hafted Axe Movement Research PRD

**Status:** Research-backed design input; not implementation authorization

**Scope:** Movement only: individual locomotion, formation placement, commitment and recovery movement, disengagement, regrouping, and count-aware decisions

**Historical confidence:** Documented, form uncertain for Philippine multipurpose hafted axes; provisional for a distinct sixteenth-century weapon named `Wasay` and for a dedicated battlefield movement system

## Executive conclusion

Hukbo should treat Wasay movement as a close-to-mid-range, high-commitment battlefield role that needs a clear lane, deliberate entry, a readable recovery, and nearby support. Its identity should come from when and where the unit commits, not from making the unit inherently faster or slower than another human.

This design is necessarily a reconstruction. The National Museum of the Philippines documents Philippine axes as multipurpose tools used in subsistence activities and warfare, while warning that colonial-era collectors and anthropologists often exoticized northern multipurpose axes with labels such as “head axe” or “battle axe.” Current evidence does not securely establish a standardized sixteenth-century weapon called *wasay*, a dedicated “war axe” category, or an associated footwork tradition. Later ethnographic and museum forms can show that hafted axes existed and varied, but they cannot supply a 1500s combat manual.

Hukbo currently implements `Wasay` as a two-handed, short-reach, high-damage loadout. That is a player-facing gameplay identity. If retained, its movement profile should be framed as a balance hypothesis:

- preserve the shared human movement baseline;
- use commitment, recovery, turn limits, ally clearance, and regroup choices to express the role;
- let local and faction counts change decisions, never physical speed;
- use facing for locomotion only;
- calibrate for team-battle role viability rather than equal duel performance.

## Evidence method and labels

- **Documented:** directly described or pictured in a sixteenth-century
  source.
- **Documented, form uncertain:** the weapon or tool class is supported, but its exact form, date, terminology, or use remains unresolved.
- **Provisional reconstruction:** a movement behavior inferred for deterministic gameplay from incomplete evidence.

Modern Filipino martial arts, HEMA, combat-sport research, biomechanics, and community reports are analogies only. They may reveal useful variables, such as distance transitions or ally interference, but they do not establish Philippine movement in the sixteenth century.

## Implemented identity versus historical identity

### Current Hukbo identity

The V2 combat preset defines Wasay as:

- damage `18`;
- reach `13` world units;
- cooldown `8` ticks;
- grip `TwoHanded`.

All current loadouts share movement speed `3` world units per tick, and the
standard body radius is `4.25` world units. The weapon values come from
`PhilippineCombatPresetV2.cs`; movement speed and body radius come from
`Scenario.cs`. They are game parameters, not historical measurements.

Within the six-loadout roster, Wasay has the highest nominal damage and the longest cooldown. Its reach equals solo Kalis, exceeds both Itak loadouts, and is shorter than Kampilan. This PRD does not rebalance those statistics. It asks what movement decisions make that existing role legible and counterable.

### Historical and terminology boundary

- **Documented, form uncertain:** A current National Museum interpretation
  describes Philippine axes as multipurpose implements used in subsistence
  and warfare. Applying that interpretation to a sixteenth-century Wasay role
  is **Provisional reconstruction**.
- **Documented, form uncertain:** The same museum interpretation cautions
  that labels such as “head axe” and “battle axe” were imposed on
  multipurpose northern axes by early anthropologists.
- **Documented, form uncertain:** later Philippine ethnographic collections include hafted axes with diverse regional forms.
- **Provisional reconstruction:** `Wasay` as a standardized sixteenth-century dedicated combat axe.
- **Unknown or unsupported:** a known “heavy two-handed war axe” with a
  documented period-specific movement system.

The player-facing roster name may remain `Wasay`, but research and code documentation should not silently convert it into a historical certainty. Any “two-handed,” “forward-weighted,” or “high-commitment” movement identity in Hukbo belongs to the game’s current abstraction unless better period evidence is found.

## Physical handling and movement implications

Generic hafted-tool biomechanics offers a limited mechanical analogy. Experimental chopping studies report coordinated shoulder, elbow, and wrist motion across an upswing and downswing, with hafting increasing distal-head velocity and total delivered energy. The study concerns tool use, not combat, Philippine axes, or a Wasay. It supports testing three abstract properties only:

1. **Committed cycle.** An entry and action may be followed by a meaningful recovery rather than an instant direction reversal.
2. **Distal clearance.** A hafted implement needs space around the actor; nearby allies can reduce usable motion.
3. **Turn cost during commitment.** Retargeting through a wide angle should be less available after commitment begins.

A sports-biomechanics study also found lower swing speed for implements with higher moment of inertia in restricted motion. That result can motivate sensitivity testing, but no surviving Wasay mass distribution is known well enough to convert it into a game number.

The desired feel is forceful and deliberate, not sluggish. Unengaged travel stays near the shared baseline; movement constraints appear during threat management, commitment, and recovery.

## Full movement lifecycle

### 1. Formation placement

**Provisional reconstruction:** prefer a loose front or supporting lane with more ally clearance than shorter one-handed loadouts. A Wasay unit should avoid the center of a dense friendly clump, where its movement identity cannot operate. In a mixed group, it may use an outer lane or follow a teammate who already occupies an enemy’s attention.

This is not evidence for a historical Wasay formation. It is a gameplay response to the implemented two-handed, high-commitment role.

### 2. Approach

Approach should be staged:

1. travel at the common baseline while outside the local engagement area;
2. face the intended lane and reduce lateral wandering near threat range;
3. enter only when clearance, target distance, and local support make commitment credible.

Against multiple enemies, prefer an outer target. Avoid moving between separated threats. When an ally is already engaging the target, choose a distinct bearing rather than overlapping the ally’s lane.

### 3. Entry and commitment

Commitment begins when a target is plausibly reachable and the movement lane is not occupied by an ally. During a short deterministic interval:

- restrict sharp turns and full-speed reversal;
- discourage target switching;
- continue only the selected entry or shorten it if the lane becomes unsafe;
- do not alter attack damage, cooldown, collision, or hit resolution.

Ally clearance delays an unsafe entry; it does not cause friendly damage. No hidden attack priority should allow Wasay to pass through companions.

### 4. Recovery and reset

Wasay should have a slightly more pronounced movement recovery than Kampilan under the present gameplay identities. After commitment, favor a brief stationary reset, a short lateral exit, or a controlled backward step. Repeated forward commitment is acceptable only when the opponent is isolated and local numbers remain favorable.

Recovery must not duplicate or extend the weapon’s combat cooldown. It is a movement posture that constrains locomotion and retargeting while the existing combat system remains authoritative.

### 5. Disengagement and regrouping

When a Wasay unit is locally outnumbered, threatened from a wide bearing spread, or separated from allies, it should refuse deep entry and move toward a viable allied cluster. This is tactical disengagement:

- no morale, panic, rout, or surrender;
- no permanent retreat state;
- no speed bonus;
- re-entry is allowed when local conditions improve.

Use hysteresis between the disengage and re-entry thresholds so the unit does not flicker between states each tick.

### 6. Pursuit and retreat

Pursuit should be conservative. A high-value isolated target can be followed while an exit or ally reference remains available. A withdrawing enemy should not pull the Wasay unit through a locally superior hostile cluster. Retreat should be a controlled yield toward support, with facing adjusted gradually rather than an immediate full-speed backward escape.

Pigafetta’s account of Mactan documents difficult footing, grouped opposition, staged retreat, and pursuit. It supports those phenomena in one battle, not Wasay-specific behavior.

## Six-loadout 1v1 movement matrix

These rows are deterministic gameplay hypotheses, not historical matchup doctrine. Shielded opponents affect spacing only; shield activation, facing arcs, and interception remain deferred.

| Opponent | Wasay movement objective | Entry and reset behavior | Primary failure to test |
|---|---|---|---|
| Kampilan | Cross the longer reach without following a straight, permanently punishable lane. | Enter after a Kampilan commitment or lateral displacement; reset instead of trading in place. | Wasay can never close, or always closes without exposure. |
| Wasay | Avoid simultaneous head-on commitment and permanent mirrored circling. | Make small lane adjustments, stagger commitments, then restore separation during recovery. | Deterministic stalemate or repeated mutual collision. |
| Kalis | Use equal nominal reach but accept the slower cadence identity. | Commit selectively and create space after recovery begins. | Wasay’s damage role overwhelms every exchange, or Kalis wins solely by endless orbiting. |
| Kalis + Tall Hardwood | Seek a clear entry without inventing a shield-side weakness. | Avoid repeated frontal overcommitment; use spacing and congestion changes only. | Movement encodes unapproved directional shield behavior. |
| Itak | Deny uncontested close pressure while keeping a recovery lane. | Commit at the outer edge and yield if the Itak crosses inside during recovery. | Itak has no route to enter, or Wasay cannot reset once crossed. |
| Itak + Tall Hardwood | Avoid being pinned by compact pressure while preserving clearance. | Reposition around bodies, not an assumed shield opening; re-enter with support. | Shield grants a movement-speed advantage or causes endless Wasay retreat. |

## 2v2, group, and count-aware behavior

### Local counts and bearings

Local perception should separately count allies and hostiles and, when useful, count their loadouts. Counts influence posture and target selection. They must not modify movement speed, commitment force, or recovery duration.

Candidate local posture bands:

> **Provisional reconstruction:** Gameplay tuning; no historical measurement.

| Local hostile-to-ally relationship | Wasay posture |
|---|---|
| Hostiles fewer than allies | Pressure an outer or already-occupied target while keeping clearance. |
| Rough parity | Hold entry distance and commit when an ally divides hostile attention. |
| Hostiles at least 1.5 times allies | Prefer short entries, outer targets, and an explicit regroup lane. |
| Hostiles at least 2 times allies | Refuse new commitment and regroup unless already inside a committed phase. |

The acting unit should be counted consistently on its own side. Bearing spread matters alongside totals: two hostiles on nearly the same bearing are a different movement problem from two hostiles on opposite sides.

### 2v2

Two Wasay allies should not commit down one lane simultaneously. Prefer staggered entries: one threatens or recovers while the other retains movement freedom. In a mixed pair, the Wasay unit should use a separate lane and avoid cutting across a shorter-reach ally’s approach.

Modern multiple-opponent HEMA community observations emphasize ally interference, outer-target selection, and the way 2v3 can decompose into 1v2 plus 1v1. They are useful test prompts, but use different weapons, rules, and cultural contexts.

### Outnumbered cases

- **1v2:** keep threats within the smallest feasible bearing spread, decline movement between them, and yield toward allied support.
- **2v3:** stay mutually relevant without entering each other’s clearance envelope; pressure an outer hostile when possible.
- **3v5:** regroup by local lanes rather than forming a static clump; re-evaluate after each commitment or separation event.

### Homogeneous and mixed groups

Homogeneous Wasay groups need generous spacing and staggered timing. Mixed groups should not adopt a rigid Wasay formation; each loadout keeps its own individual movement constraints while sharing a contingent posture. An ally already occupying an enemy can create an entry opportunity, but this should emerge from positions and counts rather than a new formation-command system.

### Large battles

At 100v100 and 250v250, faction totals may set a coarse pressure, hold, or conserve posture. Individual movement remains local. Global advantage cannot justify entering a locally superior cluster, and global disadvantage cannot synchronize a faction-wide retreat.

Local-count queries must remain deterministic and bounded. Do not add target caches, save derived count data into snapshots, or let iteration order change tie-breakers.

## Provisional candidate ranges

These are starting values for playtest if the two-handed, high-commitment gameplay identity remains. Multipliers apply only to movement states and never raise speed above the shared human baseline.

> **Provisional reconstruction:** Gameplay tuning; no historical measurement.

| Variable | Candidate range | Initial default | Purpose |
|---|---:|---:|---|
| Forward approach multiplier | 0.90–0.98 | 0.94 | Preserve ordinary travel while making close entry deliberate. |
| Lateral movement while engaged | 0.65–0.82 | 0.74 | Make lane selection and support important. |
| Backward movement while engaged | 0.55–0.72 | 0.64 | Prevent effortless reverse kiting while retaining a controlled yield. |
| Turn budget while committed | 1/20–1/12 turn per tick | 1/16 | Make wide retargeting costly after commitment. |
| Preferred entry distance | 1.00–1.20 × attack reach | 1.08 × | Start the entry decision near the implemented threat band. |
| Commitment duration | 3–5 ticks | 4 ticks | Give the high-commitment role a readable movement phase. |
| Recovery duration | 3–5 ticks | 4 ticks | Create a reset window without changing combat cooldown. |
| Ally-clearance radius | 1.50–2.00 body diameters | 1.75 | Reduce overlapping committed lanes. |
| Regroup trigger | 1.5:1–2:1 local hostile ratio | 2:1 | Refuse deep entry under clear local disadvantage. |
| Regroup release | 1:1–1.5:1 local hostile ratio | 1.25:1 | Prevent threshold oscillation. |

None of these values is a historical claim. Tick-based values must be calibrated against fixed-tick readability and existing attack timing.

## Role viability and acceptance hypotheses

Wasay is viable when it creates decisive pressure from a supported, clear entry without becoming either a universal duel winner or a unit that rarely reaches combat.

- Shorter-reach loadouts retain reproducible opportunities to cross inside during commitment or recovery.
- Kampilan’s reach remains meaningful without making the matchup unwinnable.
- Wasay benefits from ally-created openings but loses efficiency in friendly congestion.
- Locally outnumbered units regroup sometimes without oscillating or abandoning the battle.
- Mirrored Wasay duels do not settle into permanent orbiting or collision.
- Count-aware choices remain identical for the same seed, commands, and build.
- Local queries remain bounded at 100v100 and 250v250.

Equal duel win rates are not a requirement. Team utility, readability, and counterplay are.

## Calibration questions

1. How often is Wasay commitment delayed or cancelled by ally clearance?
2. Does the wider clearance default create traffic jams in homogeneous groups?
3. Can each shorter-reach loadout enter during a reproducible commitment or recovery window?
4. Can Wasay cross Kampilan reach without either guaranteed failure or guaranteed success?
5. How much time do mirrored duels spend outside engagement range?
6. Do turn limits create readable commitment or merely unresponsive steering?
7. Does the 2:1 regroup trigger occur early enough to prevent pointless deep entry?
8. Does hysteresis prevent state flicker at count boundaries?
9. In 1v2, 2v3, and 3v5, does bearing-aware movement outperform count-only movement without creating a hidden combat bonus?
10. Does faction posture ever override obvious local danger?

Record state hash, event hash, winner, ordered events, engagement time, path distance, heading changes, clearance delays, commitment cancellations, recovery exits, local ratios, and regroup transitions.

## Unknowns and non-goals

Unknowns:

- a firm sixteenth-century attestation for the roster term `Wasay`;
- whether any relevant axe was specialized for battle rather than multipurpose use;
- representative period head shape, haft length, mass distribution, and grip;
- an authentic movement or formation system;
- numeric movement, turn, commitment, recovery, and clearance values.

Non-goals:

- claiming a dedicated “war axe” or “head axe” identity as certain;
- translating later Cordilleran forms into a generic sixteenth-century Philippine weapon;
- treating modern FMA, HEMA, or tool chopping as historical continuity;
- changing damage, reach, cooldown, collision, or hit resolution;
- directional shield rules, blocks, parries, interception, or friendly fire;
- morale, rout, surrender, terrain, pathfinding, ammunition, or campaign systems;
- a rigid formation or mixed-contingent rewrite;
- weapon- or count-based passive speed bonuses.

## Evidence ledger

| ID | Atomic claim | Place/date | Source and exact locator | Source class | Evidence label | Transfer limit | Movement consequence |
| --- | --- | --- | --- | --- | --- | --- | --- |
| WA-01 | A current museum synthesis describes Philippine axes as multipurpose subsistence/warfare implements. | Philippines, current synthesis | [National Museum](https://www.nationalmuseum.gov.ph/our-collections/ethnology/weapons-and-shields/), axe discussion | Museum synthesis | **Documented, form uncertain** | Spans regions and later collections | Keep the role explicitly game-designed |
| WA-02 | The same synthesis warns that “head axe” and “battle axe” labels exoticized multipurpose northern axes. | Philippines, current synthesis | Same, classification caution | Museum synthesis | **Documented, form uncertain** | Historiographic warning, not period terminology | Avoid a standardized historical war-axe claim |
| WA-03 | A 1926 catalog records varied Philippine axe forms. | Philippine collections, 1926 publication | [Krieger, USNM Bulletin 137](https://repository.si.edu/items/c2f4a202-42a1-40bc-bb49-3b665785ff39), catalog and plates | Later museum catalog | **Documented, form uncertain** | Too late for 1500s movement | Comparative form only |
| WA-04 | Hafted chopping uses coordinated upswing/downstroke motion. | Modern experiment | [Hafted-tool study](https://pmc.ncbi.nlm.nih.gov/articles/PMC8923818/), Results, Figures 4–5, Table 2 | Experimental analogy | **Provisional reconstruction** | Tool use, not Philippine combat | Test commitment/recovery and clearance |
| WA-05 | Higher inertia correlated with lower restricted swing speed. | Modern experiment | [Moment-of-inertia study](https://doi.org/10.1080/14763141.2015.1027949), Abstract and Results | Sports-biomechanics analogy | **Provisional reconstruction** | No Wasay inertia is known | Sensitivity-test turn limits only |
| WA-06 | Mactan involved difficult footing, grouped action, staged retreat, and pursuit. | Mactan, 1521 | [Pigafetta](https://www.gutenberg.org/cache/epub/74723/pg74723-images.html), printed pp. 100–102 | Colonial eyewitness narrative | **Documented** | No Wasay is identified | Provide shared lifecycle states only |
| WA-07 | The manuscript catalog supplies period provenance. | Cebu/Mactan account, 1521 | [Library of Congress](https://www.loc.gov/resource/gdcwdl.wdl_03082/?st=gallery), catalog description | Manuscript catalog | **Documented** | Does not identify an axe | Source provenance only |
| WA-08 | Opposed distance can change coordination mode. | Modern kendo study | [Critical interpersonal distance](https://pmc.ncbi.nlm.nih.gov/articles/PMC3527480/), Abstract and Results | Combat-sport analogy | **Provisional reconstruction** | No copied distances/timings | Test approach/engage transition |
| WA-09 | Free opposition differs from rehearsed HEMA strikes. | Modern study | [HEMA biomechanics](https://noah.nrw/ubbihs/content/titleinfo/5139823), Abstract | HEMA analogy | **Provisional reconstruction** | Different weapons/rules | Reject drill-derived timing |
| WA-10 | A modern club report identifies ally obstruction and split multiple-opponent geometries. | Modern community practice | [Armoury](https://armoury.co.za/some-thoughts-on-multiple-opponent-scenarios/), caveats, 1v2, 2v3 | Community analogy | **Provisional reconstruction** | Synthetic European weapons | Add clearance and bearing tests |
| WA-11 | Repository synthesis treats exact Wasay form/battle role as uncertain. | Repository synthesis, 2026 | [Visual research](../improve-visuals/weapons-shields-historical-research.md), Wasay section | Repository synthesis | **Provisional reconstruction** | Inherits source gaps | Preserve paired descriptor |
| WA-12 | Repository battle research rejects universal named footwork. | Repository synthesis, 2026 | [Individual combat](../battles/04-deep-past-individual-combat.md), movement section | Repository synthesis | **Provisional reconstruction** | General, not Wasay-specific | Use generic lifecycle names |
