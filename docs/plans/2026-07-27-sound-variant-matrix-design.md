# Sound Variant Matrix — Design

Date: 2026-07-27
Status: design only. A design document does not authorize implementation.

## Problem

The client resolves exactly one file per sound slot. In a two-hundred-agent
battle the same four attack samples fire dozens of times per second, and
identical repetition is the single loudest tell that a battle is a simulation
rather than a fight. Variants per slot are the standard fix.

This document plans the prompts. It does not plan the client changes that would
let the game play a variant; those are listed at the end as prerequisites.

## What the event stream actually gives us

Two facts from `Hukbo.Core/Simulation/BattleEvent.cs` and
`Hukbo.Client/Audio/SoundCueMapper.cs` shape every prompt below.

1. **An attack event only exists when the attack landed.** It carries a damage
   value and a `HitLocation` from the twelve-part `BodyPart` enum. There is no
   miss event. Every attack cue is therefore the sound of a weapon meeting a
   body, never a blade cutting empty air. A prompt that produces a pure swoosh
   is wrong for this game.
2. **Death is its own event kind**, raised on the blow that kills. It is
   currently mapped to its own slot, and it fires in addition to the attack cue
   for the same blow, so the two are heard together.

Neither the damage value nor the hit location is used to choose a sound today.
Both are available, and the selection options below revisit that.

## Rules every prompt follows

- One sound event, never a scene. The mixer plays cues on top of each other
  already; a prompt that returns a small battle produces mud.
- Always end with `no music, no voice`. No screams, no grunts, no shouting, no
  breath. This is a hard rule, not a preference: a human vocalisation heard
  forty times a minute is unbearable, and a stray one in a single variant will
  be the only thing anyone hears.
- Name the contact, the material, and the wetness. "Blade" alone gives the model
  nothing; "blade biting into thick woven cloth then flesh" gives it everything.
- Player-facing descriptors only — Great Blade, Heavy Chopper, Thrusting Blade,
  Work Blade. Never a cultural identification such as Kampilan, Panabas, or Kris,
  per the historical accuracy policy. These are sound designs, not
  reconstructions of any documented weapon's sound.
- Generated at the API's half-second floor, then trimmed back to the audible
  part. Combat cues land between roughly 0.08 and 0.3 seconds.

## Variation axes

Ten variants per slot vary along five axes rather than being ten runs of one
prompt. Re-rolling one prompt ten times gives ten takes of the same idea; the
axes give a set that covers what actually differs blow to blow.

| Axis | Range |
| --- | --- |
| Contact depth | glancing graze, clean cut, deep cleave, bone catch |
| Intervening material | bare skin, thick woven cloth, hardwood shield rim |
| Wetness | dry cut, wet split, heavy wet spray |
| Blade response | dead thud with no ring, short shiver, brief metallic ring |
| Weight | quick and shallow, committed and heavy |

## Attack matrix

### `attack-great-blade` — long two-handed blade, wide arc, deep cut

| # | Prompt |
| --- | --- |
| 01 | one wide heavy blade landing a clean deep cut through flesh, brief metallic ring in the steel after contact, no music, no voice |
| 02 | one heavy blade catching bone mid-cut, hard crack inside a wet cut, no ring, no music, no voice |
| 03 | one heavy blade landing flat against a body, dull heavy slap into dense mass, no music, no voice |
| 04 | one heavy blade cleaving deep, thick wet split with a low body-weight shift, no music, no voice |
| 05 | one heavy blade glancing off a shoulder, shallow tearing cut and the blade skidding away, no music, no voice |
| 06 | one heavy blade cutting through thick woven cloth into flesh, muffled wet impact, no music, no voice |
| 07 | one heavy blade striking a hardwood shield rim then biting into flesh, wood crack into wet cut, no music, no voice |
| 08 | one heavy blade drawn through in a long cut, sustained wet tear, no music, no voice |
| 09 | one heavy overhead chop, sharp initial crack then wet follow-through, no music, no voice |
| 10 | one heavy blade landing wet with a thin metallic shiver running through the steel, no music, no voice |

### `attack-heavy-chopper` — broad front-heavy chopping blade

| # | Prompt |
| --- | --- |
| 01 | one broad heavy chopping blade landing a deep dull cleave, thick wet impact, no ring, no music, no voice |
| 02 | one heavy chopping blade burying into a torso, blunt crunch and dense wet mass, no music, no voice |
| 03 | one fast downward heavy chop, solid meat impact with a short low thud, no music, no voice |
| 04 | one heavy chopping blade hacking through a limb, wet cut with a sharp bone snap, no music, no voice |
| 05 | one heavy chopping blade landing on padded cloth, dense muffled blow with almost no cut, no music, no voice |
| 06 | one heavy chopping blade glancing, skating off the surface then thudding home, no music, no voice |
| 07 | one deep two-handed chop, wet split and a heavy settling weight, no music, no voice |
| 08 | one heavy chopping blade striking shoulder bone, sharp crack inside a wet impact, no music, no voice |
| 09 | one blunt heavy blade slapping a body, more bruise than cut, low dull thud, no music, no voice |
| 10 | one heavy chopping blade cleaving with a short dark metallic ring after it, no music, no voice |

### `attack-thrusting-blade` — narrow blade, thrust rather than cut

| # | Prompt |
| --- | --- |
| 01 | one narrow blade punching into a torso, tight wet puncture, no music, no voice |
| 02 | one quick stab with a narrow blade, short entry with the blade sliding along a rib, no music, no voice |
| 03 | one deep thrust, wet penetration followed by a slow drawing withdrawal, no music, no voice |
| 04 | one fast shallow jab, quick puncture with a sharp tick of steel, no music, no voice |
| 05 | one thrust through thick padded cloth into flesh, muffled dense entry, no music, no voice |
| 06 | one blade point skidding off bone then puncturing, thin scrape into wet entry, no music, no voice |
| 07 | one thrust and twist, wet tearing inside the wound, no music, no voice |
| 08 | one dry snappy stab, quick clean entry with very little wetness, no music, no voice |
| 09 | one thrust driven through with a light metallic hiss along the blade, no music, no voice |
| 10 | one low heavy thrust, dull deep puncture into dense mass, no music, no voice |

### `attack-work-blade` — short light single-edged working blade

| # | Prompt |
| --- | --- |
| 01 | one short light blade landing a quick clean cut, small bright contact, no music, no voice |
| 02 | one fast light blade slicing skin, thin wet zip, no music, no voice |
| 03 | one quick chop with a small blade, shallow bite into flesh, no music, no voice |
| 04 | one light blade skidding across a surface, glancing scratch with no depth, no music, no voice |
| 05 | one rapid slash cutting through cloth then skin, light double texture, no music, no voice |
| 06 | one small blade hacking, quick dry chop with little wetness, no music, no voice |
| 07 | one light cut ending in a thin high metallic ping, no music, no voice |
| 08 | one fast slash landing wet and shallow, brisk and small, no music, no voice |
| 09 | one short hard hack, small dense thud into muscle, no music, no voice |
| 10 | one quick slicing cut with a brief snap of air just before contact, no music, no voice |

## Death — open question

The death cue is unresolved and is the subject of the questions raised alongside
this document. Three candidate directions, none yet chosen:

- **Silent.** Map `BattleEventKind.Death` to no slot. A kill then sounds exactly
  like any other landed blow. One line changes in `SoundCueMapper`, plus a test.
- **A finishing blow.** Keep the slot, but make it a heavier, wetter version of a
  landed cut, layered under the attack cue that fires on the same blow. No voice.
  A kill becomes audibly distinguishable from a graze without any vocalisation.
- **A falling body.** The current prompt: a body dropping onto dry packed earth,
  cloth and gear scraping. No voice either, but it reads as an aftermath sound
  rather than a strike.

If the slot survives in any form, its prompts follow the same axes as the attack
matrix, weighted to the heavy and wet end.

## Outcome and interface cues

These play once per battle, so a spectator hears one variant and never learns the
others exist. Three each is generous; ten would be waste.

| Slot | Count | Direction |
| --- | --- | --- |
| `victory-blue` | 3 | low ceremonial gong strike, warm decaying ring, single hit |
| `victory-red` | 3 | brighter ceremonial gong strike, higher decaying ring, single hit |
| `draw` | 3 | two dull wooden strikes ending flat, unresolved, no resonance |
| `ui-click` | 2 | very short dry wooden tick, no reverb |

The three outcome cues are never trimmed. Their decay is the cue.

## Totals

| Group | Slots | Variants each | Files |
| --- | --- | --- | --- |
| Attacks | 4 | 10 | 40 |
| Death | 0 or 1 | 10 | 0 or 10 |
| Outcomes | 3 | 3 | 9 |
| Interface | 1 | 2 | 2 |
| **Total** | | | **51 or 61** |

Real API calls run above the file count because rejected takes are retried.
Observed rate so far is roughly one dud in three, so budget about 1.3 times the
file count.

## Prerequisites before any of this is generated

None of these files can be played today. `SoundCatalog` maps a slot to exactly
one file name and `SoundLibrary` resolves exactly one binding per slot, so a
second file for a slot is ignored.

1. A file naming scheme. Proposed: `death-01.wav` through `death-10.wav`, zero
   padded, with the bare `death.wav` still valid as a single.
2. `SoundLibrary` resolving an ordered list per slot; `SoundBinding` holding
   several loaded effects; the sound panel reporting `READY (10)`.
3. A selection rule. Three candidates:
   - Hash of tick and entity id. No stored state, and a replay sounds identical
     to its original run, which matches how the rest of the repository behaves.
   - Round robin per slot. Spreads repeats slightly better, but two runs of the
     same seed diverge audibly, which contradicts the determinism culture even
     though audio never reaches a state hash.
   - Selection driven by `HitLocation` or the damage value, so a head hit and a
     shin hit genuinely differ. Richest, and it makes the variant set meaningful
     rather than decorative, but it constrains what each variant must be.
4. Tests for resolution, selection spread, and the partially-populated case where
   a slot has three variants rather than ten.
5. A plan document, per the workflow in `CLAUDE.md`.
