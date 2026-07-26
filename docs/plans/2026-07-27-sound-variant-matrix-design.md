# Sound Variant Matrix — Design

Date: 2026-07-27
Status: design only. A design document does not authorize implementation.

## Problem

The client resolves exactly one file per sound slot. In a two-hundred-agent
battle the same four attack samples fire dozens of times per second, and
identical repetition is the single loudest tell that a battle is a simulation
rather than a fight. Variants per slot are the standard fix.

Two decisions were taken before this matrix was written:

- **The death cue is a body falling to earth.** No voice, as with every other
  cue here.
- **Variant selection is driven by hit location.** A variant is not decorative
  alternation; it is the sound of a specific kind of blow landing on a specific
  kind of body part.

## What the event stream gives us, and what it withholds

From `Hukbo.Core/Simulation/BattleEvent.cs`, `Hukbo.Client/Audio/SoundCueMapper.cs`,
and `Hukbo.Core/Combat/PhilippineCombatPreset.cs`:

1. **An attack event only exists when the attack landed.** It carries a damage
   value and a `HitLocation` from the thirteen-part `BodyPart` enum. There is no
   miss event. Every attack cue is a weapon meeting a body, never a blade
   cutting empty air. A prompt that returns a pure swoosh is wrong for this game.
2. **Each weapon already aims differently.** Target weights are near flat, 7 to
   10 across the parts, with a deliberate per-weapon bias: the Great Blade
   favours head and neck, the Heavy Chopper the shoulder, the Thrusting Blade
   the abdomen, the Work Blade the arms and hands. Hit-location variants
   therefore land on genuinely different distributions per weapon, and the take
   counts below follow those biases.
3. **A tall hardwood shield halves chest and abdomen targeting weight**
   (provisional tuning, 500 of 1000 basis points), raising the relative share of
   limb, head, neck, and face hits. Torso variants matter less against shielded
   warriors than unshielded ones.
4. **The death event carries neither a weapon nor a hit location.** Both are
   populated only for attack events. Death variants therefore cannot be
   hit-location driven, and fall back to an index chosen from tick and entity
   id. This is a hard constraint from the event model, not a preference.
5. **A shield block is not an event.** Nothing in the stream says an attack was
   stopped by a shield, so no variant should be the sound of a blade hitting
   wood alone.

## Acoustic classes

Thirteen body parts do not need thirteen sounds; several are acoustically
identical, and a shin, a hand, and a forearm are the same event to an ear. The
parts collapse into six classes.

| Class | Body parts | Character |
| --- | --- | --- |
| `skull` | Head, Face | Hard bone over thin flesh. Sharp dry crack, shallow wet edge |
| `neck` | Neck | Soft tissue, deep and wet, spine catching underneath |
| `ribcage` | Chest | Dense wet impact with a rib crack inside it |
| `gut` | Abdomen | Soft, deep, wet, no bone at all |
| `limb` | Shoulder, Thigh, Knee | Thick muscle with a heavy joint crunch |
| `extremity` | WeaponArm, ShieldArm, Shin, Hands, Feet | Thin bone, little meat, quick and small |

## Rules every prompt follows

- One sound event, never a scene. Cues already stack on each other; a prompt
  that returns a small battle produces mud.
- Always end with `no music, no voice`. No screams, grunts, shouting, or breath.
  This is a hard rule: a human vocalisation heard forty times a minute is
  unbearable, and one stray variant will be the only thing anyone hears.
- Name the contact, the material, and the wetness.
- Player-facing descriptors only — Great Blade, Heavy Chopper, Thrusting Blade,
  Work Blade. Never a cultural identification such as Kampilan, Panabas, or
  Kris, per the historical accuracy policy. These are sound designs, not
  reconstructions of any documented weapon's sound.
- Generated at the API's half-second floor, then trimmed back to the audible
  part. Combat cues land between roughly 0.08 and 0.3 seconds.

## Take allocation

Ten takes per attack slot, distributed by that weapon's own targeting bias, with
every class guaranteed at least one take so no hit location is ever unvoiced.

| Slot | skull | neck | ribcage | gut | limb | extremity |
| --- | --- | --- | --- | --- | --- | --- |
| `attack-great-blade` | 2 | 2 | 2 | 1 | 2 | 1 |
| `attack-heavy-chopper` | 2 | 1 | 1 | 1 | 3 | 2 |
| `attack-thrusting-blade` | 1 | 2 | 2 | 3 | 1 | 1 |
| `attack-work-blade` | 2 | 2 | 1 | 1 | 1 | 3 |

## Attack matrix

### `attack-great-blade` — long two-handed blade, wide arc, deep cut

| Class | Prompt |
| --- | --- |
| skull | one heavy two-handed blade splitting into a skull, hard dry bone crack with a thin wet edge and a brief metallic shiver, no music, no voice |
| skull | one heavy blade landing across a face, sharp bone crack opening into a shallow wet cut, no music, no voice |
| neck | one heavy blade cutting deep through a neck, thick wet sever with no ring, no music, no voice |
| neck | one heavy blade landing across a neck, wet cut catching spine with a dull crack, no music, no voice |
| ribcage | one heavy blade cleaving into a ribcage, wet impact with a dense rib crack inside it, no music, no voice |
| ribcage | one heavy blade landing flat against a chest, heavy dull slap into dense mass, no music, no voice |
| gut | one heavy blade opening a belly, deep soft wet split with no bone, no music, no voice |
| limb | one heavy blade chopping into a shoulder joint, wet meat with a hard joint crunch, no music, no voice |
| limb | one heavy blade cutting deep into a thigh, thick wet cut through heavy muscle, no music, no voice |
| extremity | one heavy blade severing a forearm, quick wet cut with a thin bone snap, no music, no voice |

### `attack-heavy-chopper` — broad front-heavy chopping blade

| Class | Prompt |
| --- | --- |
| skull | one broad heavy chopping blade crushing into a skull, blunt dark bone crack, no music, no voice |
| skull | one heavy chopping blade landing across a face, flat bone crunch with little cut, no music, no voice |
| neck | one broad chopping blade hacking through a neck, deep wet cleave ending in a low thud, no music, no voice |
| ribcage | one heavy chopping blade burying into a chest, thick wet crunch through ribs, no music, no voice |
| gut | one heavy chopping blade sinking into a belly, dull deep wet impact with no bone, no music, no voice |
| limb | one heavy chopping blade cleaving a shoulder, wet meat and a hard joint break, no music, no voice |
| limb | one heavy chopping blade landing on a knee, blunt joint crunch with a short wet edge, no music, no voice |
| limb | one heavy chopping blade hacking into a thigh, heavy wet chop into dense muscle, no music, no voice |
| extremity | one heavy chopping blade taking off a hand, quick wet chop with a small bone snap, no music, no voice |
| extremity | one heavy chopping blade striking a shin, sharp bone crack under thin flesh, no music, no voice |

### `attack-thrusting-blade` — narrow blade, thrust rather than cut

| Class | Prompt |
| --- | --- |
| skull | one narrow blade punching into a face, tight puncture skidding across bone, no music, no voice |
| neck | one narrow blade thrust into a neck, tight wet entry through soft tissue, no music, no voice |
| neck | one narrow blade driven through a throat, deep wet puncture and a slow withdrawal, no music, no voice |
| ribcage | one narrow blade punching into a chest, wet entry with the point skidding on a rib, no music, no voice |
| ribcage | one narrow blade thrust deep between ribs, tight puncture into dense mass, no music, no voice |
| gut | one narrow blade sinking into a belly, soft deep wet entry with no bone, no music, no voice |
| gut | one narrow blade thrust into a gut and twisted, wet tearing inside the wound, no music, no voice |
| gut | one narrow blade punching through a belly, quick soft entry with a light metallic hiss, no music, no voice |
| limb | one narrow blade thrust into a thigh, tight wet puncture through heavy muscle, no music, no voice |
| extremity | one narrow blade punching through a hand, quick shallow puncture with a thin bone tick, no music, no voice |

### `attack-work-blade` — short light single-edged working blade

| Class | Prompt |
| --- | --- |
| skull | one short light blade slashing a face, shallow quick cut skidding across bone, no music, no voice |
| skull | one small blade chopping at a skull, light bone tap with a thin wet edge, no music, no voice |
| neck | one short blade slashing a neck, fast wet cut through soft tissue, no music, no voice |
| neck | one small blade hacking at a neck, quick shallow wet chop, no music, no voice |
| ribcage | one short blade cutting across a chest, shallow wet slice over bone, no music, no voice |
| gut | one short blade slicing a belly, quick soft wet cut, no music, no voice |
| limb | one short blade chopping into a shoulder, small dense wet hack, no music, no voice |
| extremity | one short blade slashing a forearm, fast light cut with a thin bone tick, no music, no voice |
| extremity | one small blade hacking at a hand, quick sharp chop with a small bone snap, no music, no voice |
| extremity | one short blade striking a shin, light dry chop with thin flesh over bone, no music, no voice |

## `death` — ten variants, body falling to earth

Selected by tick and entity id rather than hit location, because the death event
carries no hit location. No voice, as everywhere else.

| # | Prompt |
| --- | --- |
| 01 | a body dropping onto dry packed earth, dull heavy thud with cloth settling, no music, no voice |
| 02 | a body collapsing to the ground, heavy soft impact and gear scraping dirt, no music, no voice |
| 03 | a body falling knees first then flat, two stage thud on dry ground, no music, no voice |
| 04 | a body hitting dry earth with a wooden shield clattering beside it, no music, no voice |
| 05 | a body dropping heavily, dust and loose grit shifting under the weight, no music, no voice |
| 06 | a body slumping sideways onto packed soil, muffled thud with cloth dragging, no music, no voice |
| 07 | a body falling flat onto dry ground, sharp slap of flesh and cloth, no music, no voice |
| 08 | a body collapsing with a blade clattering down onto dirt beside it, no music, no voice |
| 09 | a body dropping onto dry grass and earth, soft crushed rustle under the weight, no music, no voice |
| 10 | a heavy body falling limp to the ground, low dull impact with no rebound, no music, no voice |

These are generated longer than a combat hit, around 0.7 seconds, and trimmed.

## Outcome and interface cues

These are notification sounds in the manner of a phone messaging app: very
short, soft, clean, and immediately legible. Not ceremonial gongs.

This is a deliberate break from the combat cues. Everything in the attack and
death matrix is diegetic — a sound something in the world made. These four are
not; they are the interface speaking to the person watching. That is consistent
with the rest of the client, whose panels, control bar, and inspector are all
non-diegetic, and the three outcome cues fire only once fighting has stopped, so
nothing diegetic is competing with them at that moment.

**One file each, not several.** A notification sound earns its meaning by being
identical every single time; that recognisability is the whole mechanism, and it
is why no messaging app ships ten alternating message tones. Variants here would
actively make the cues worse.

Several candidates are still generated per cue, but only to audition. The best
one is kept and the rest are deleted, so the shipped count stays at one.

| Slot | Files | Direction |
| --- | --- | --- |
| `victory-blue` | 1 | two soft glass notes rising, bright and cool, clean, resolved |
| `victory-red` | 1 | two soft notes rising in a lower warmer register, the same gesture darker |
| `draw` | 1 | two soft notes at one pitch, flat and unresolved, deliberately going nowhere |
| `ui-click` | 1 | a single tiny soft tap, the quietest thing in the game |

The two victory cues are written as a matched pair on purpose: the same
two-note gesture, separated by register rather than by melody, so a listener
learns one shape and reads the winner from its colour. Blue sits higher and
cooler, Red lower and warmer. The draw cue uses the same shape with the rise
removed, which is what makes it read as unresolved rather than as a third
outcome.

Candidate prompts:

| Slot | Prompt |
| --- | --- |
| `victory-blue` | two soft glass notes rising a fifth, gentle bright interface notification, very short, clean decay, no reverb tail, no music, no voice |
| `victory-red` | two soft mallet notes rising a fifth in a low warm register, gentle interface notification, very short, clean decay, no reverb tail, no music, no voice |
| `draw` | two soft muted notes at the same pitch, flat and unresolved interface notification, very short, dry, no reverb tail, no music, no voice |
| `ui-click` | one very short soft tap, subtle interface tick, tiny and clean, almost no tone, no reverb, no music, no voice |

Unlike the earlier gong direction, these **are** trimmed, at a 2 percent
threshold rather than the combat cues' 5 percent. A pitched tone decays smoothly,
and cutting it at 5 percent of peak audibly chops the tail; 2 percent removes the
dead air without touching the decay.

Loudness deserves a note, because the game plays every cue at one fixed volume
and never ducks. Subtlety here comes from duration and timbre, not from a quiet
file: a 200 millisecond soft sine at full scale is perceived as far gentler than
a broadband crack at the same peak. The files should still normalise near full
scale, which also keeps them clear of the generator's near-silence rejection.

## Totals

| Group | Slots | Files each | Files |
| --- | --- | --- | --- |
| Attacks | 4 | 10 | 40 |
| Death | 1 | 10 | 10 |
| Outcomes | 3 | 1 | 3 |
| Interface | 1 | 1 | 1 |
| **Total** | | | **54** |

Rejected takes are retried, and the observed dud rate so far is roughly one in
three, so budget about **70 API calls** for 54 shipped files — plus roughly 12
more for the outcome and interface candidates that are auditioned and discarded.
Total disk is under 1 MB.

## Prerequisites before any of this is generated

None of these files can be played today. `SoundCatalog` maps a slot to exactly
one file name and `SoundLibrary` resolves exactly one binding per slot, so a
second file for a slot is ignored.

1. **File naming.** `attack-great-blade-skull-01.wav`, that is
   `<slot>-<class>-<index>.wav`, with the class token drawn from the six above.
   The class belongs in the file name rather than in a code-side table, so the
   mapping cannot silently drift from the files on disk. Death and the outcome
   cues have no class: `death-01.wav`, `victory-blue-01.wav`. A bare
   `death.wav` stays valid as a single.
2. **A `HitClass` enum and a `BodyPart` to `HitClass` map** in the client, with a
   test asserting every one of the thirteen parts maps somewhere.
3. **`SoundLibrary` resolving a list per slot and class**, `SoundBinding`
   holding several loaded effects, and the sound panel reporting `READY (10)`
   with a per-class breakdown.
4. **A fallback order**, because a partially generated set must still sound
   right: `extremity` falls back to `limb`, `limb` to `ribcage`, `skull` to
   `neck`, `neck` and `gut` to `ribcage`, and `ribcage` to the bare `<slot>.wav`
   single. A slot with no file at all stays silent, exactly as today.
5. **The index within a class chosen by hashing tick and entity id**, so no state
   is stored and a replay sounds identical to its original run.
6. **Tests** for the part-to-class map, resolution, the fallback chain, selection
   spread within a class, and the partially populated case.
7. **A plan document**, per the workflow in `CLAUDE.md`.
