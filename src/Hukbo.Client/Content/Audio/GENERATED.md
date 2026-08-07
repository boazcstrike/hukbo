# Generated sound provenance

Every file in this folder produced by `scripts/sfx.ps1` is logged here.
The game ignores this file; it exists so a sound can be traced back to
the prompt and model that made it.

Most rows dated 2026-07-27 were **reconstructed after the fact**, not written by
the run that produced the file. A defect in `Add-ProvenanceRow` threw after each
successful write, so the first large batch of variants landed on disk with no
log row. The prompts and requested durations were recovered from the generation
instructions and the kept durations were measured from the WAV headers, so the
rows are accurate — but they are a reconstruction, and the exact time of day and
the number of attempts each take needed are lost.

**No weapon-attack row below names a file that still exists under that name.**
Every one of the forty weapon takes was generated before the sound slots were
renamed to carry the weapon identity, and none of them was regenerated. The
rows are left exactly as written, because this file records what was generated
and under which prompt — not what the files are called today. To find a row's
file on disk, apply this mapping to its base name:

| Generated as | On disk today | Weapon |
| --- | --- | --- |
| `attack-great-blade-*` | `attack-kampilan-*` | Kampilan — Great Blade |
| `attack-heavy-chopper-*` | `attack-wasay-*` | Wasay — War Axe |
| `attack-thrusting-blade-*` | `attack-kalis-*` | Kalis — Thrusting Blade |
| `attack-work-blade-*` | `attack-itak-*` | Itak — Work Blade |

The heavy chopper rows carry a second layer of history: combat preset V2 changed
that weapon from a broad chopping blade to a hafted axe, and its takes were kept
rather than re-rolled because the prompts describe a heavy chopping impact, which
suits an axe as well as it suited the previous weapon.

| Date | File | Model | Requested | Kept | Influence | Prompt |
| --- | --- | --- | --- | --- | --- | --- |
| 2026-07-27 | `ui-click.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.48s | 0.4 | one very short dry wooden tick, quiet interface click, no reverb, no music, no voice |
| 2026-07-27 | `ui-click.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.48s | 0.4 | one very short dry wooden tick, quiet interface click, no reverb, no music, no voice |
| 2026-07-27 | `ui-click.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.08s | 0.4 | one very short dry wooden tick, quiet interface click, no reverb, no music, no voice |
| 2026-07-27 | `death-10.wav` | `eleven_text_to_sound_v2` | 0.7s | 0.23s | 0.4 | a heavy body falling limp to the ground, low dull impact with no rebound, no music, no voice |
| 2026-07-27 | `attack-heavy-chopper-extremity-02.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.08s | 0.4 | one heavy chopping blade striking a shin, sharp bone crack under thin flesh, no music, no voice |
| 2026-07-27 | `death-04.wav` | `eleven_text_to_sound_v2` | 0.7s | 0.12s | 0.4 | a body hitting dry earth with a wooden shield clattering beside it, no music, no voice |
| 2026-07-27 | `attack-great-blade-skull-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.11s | 0.4 | one heavy two-handed blade splitting into a skull, hard dry bone crack with a thin wet edge and a brief metallic shiver, no music, no voice |
| 2026-07-27 | `attack-great-blade-skull-02.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.13s | 0.4 | one heavy blade landing across a face, sharp bone crack opening into a shallow wet cut, no music, no voice |
| 2026-07-27 | `attack-great-blade-neck-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.26s | 0.4 | one heavy blade cutting deep through a neck, thick wet sever with no ring, no music, no voice |
| 2026-07-27 | `attack-great-blade-neck-02.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.12s | 0.4 | one heavy blade landing across a neck, wet cut catching spine with a dull crack, no music, no voice |
| 2026-07-27 | `attack-great-blade-ribcage-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.16s | 0.4 | one heavy blade cleaving into a ribcage, wet impact with a dense rib crack inside it, no music, no voice |
| 2026-07-27 | `attack-great-blade-ribcage-02.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.1s | 0.4 | one heavy blade landing flat against a chest, heavy dull slap into dense mass, no music, no voice |
| 2026-07-27 | `attack-great-blade-gut-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.13s | 0.4 | one heavy blade opening a belly, deep soft wet split with no bone, no music, no voice |
| 2026-07-27 | `attack-great-blade-limb-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.05s | 0.4 | one heavy blade chopping into a shoulder joint, wet meat with a hard joint crunch, no music, no voice |
| 2026-07-27 | `attack-great-blade-limb-02.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.27s | 0.4 | one heavy blade cutting deep into a thigh, thick wet cut through heavy muscle, no music, no voice |
| 2026-07-27 | `attack-great-blade-extremity-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.48s | 0.4 | one heavy blade severing a forearm, quick wet cut with a thin bone snap, no music, no voice |
| 2026-07-27 | `attack-heavy-chopper-skull-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.48s | 0.4 | one broad heavy chopping blade crushing into a skull, blunt dark bone crack, no music, no voice |
| 2026-07-27 | `attack-heavy-chopper-skull-02.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.11s | 0.4 | one heavy chopping blade landing across a face, flat bone crunch with little cut, no music, no voice |
| 2026-07-27 | `attack-heavy-chopper-neck-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.18s | 0.4 | one broad chopping blade hacking through a neck, deep wet cleave ending in a low thud, no music, no voice |
| 2026-07-27 | `attack-heavy-chopper-ribcage-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.19s | 0.4 | one heavy chopping blade burying into a chest, thick wet crunch through ribs, no music, no voice |
| 2026-07-27 | `attack-heavy-chopper-gut-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.17s | 0.4 | one heavy chopping blade sinking into a belly, dull deep wet impact with no bone, no music, no voice |
| 2026-07-27 | `attack-heavy-chopper-limb-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.17s | 0.4 | one heavy chopping blade cleaving a shoulder, wet meat and a hard joint break, no music, no voice |
| 2026-07-27 | `attack-heavy-chopper-limb-02.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.05s | 0.4 | one heavy chopping blade landing on a knee, blunt joint crunch with a short wet edge, no music, no voice |
| 2026-07-27 | `attack-heavy-chopper-limb-03.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.08s | 0.4 | one heavy chopping blade hacking into a thigh, heavy wet chop into dense muscle, no music, no voice |
| 2026-07-27 | `attack-heavy-chopper-extremity-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.05s | 0.4 | one heavy chopping blade taking off a hand, quick wet chop with a small bone snap, no music, no voice |
| 2026-07-27 | `attack-thrusting-blade-skull-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.06s | 0.4 | one narrow blade punching into a face, tight puncture skidding across bone, no music, no voice |
| 2026-07-27 | `attack-thrusting-blade-neck-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.08s | 0.4 | one narrow blade thrust into a neck, tight wet entry through soft tissue, no music, no voice |
| 2026-07-27 | `attack-thrusting-blade-neck-02.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.25s | 0.4 | one narrow blade driven through a throat, deep wet puncture and a slow withdrawal, no music, no voice |
| 2026-07-27 | `attack-thrusting-blade-ribcage-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.07s | 0.4 | one narrow blade punching into a chest, wet entry with the point skidding on a rib, no music, no voice |
| 2026-07-27 | `attack-thrusting-blade-ribcage-02.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.11s | 0.4 | one narrow blade thrust deep between ribs, tight puncture into dense mass, no music, no voice |
| 2026-07-27 | `attack-thrusting-blade-gut-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.1s | 0.4 | one narrow blade sinking into a belly, soft deep wet entry with no bone, no music, no voice |
| 2026-07-27 | `attack-thrusting-blade-gut-02.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.21s | 0.4 | one narrow blade thrust into a gut and twisted, wet tearing inside the wound, no music, no voice |
| 2026-07-27 | `attack-thrusting-blade-gut-03.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.2s | 0.4 | one narrow blade punching through a belly, quick soft entry with a light metallic hiss, no music, no voice |
| 2026-07-27 | `attack-thrusting-blade-limb-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.09s | 0.4 | one narrow blade thrust into a thigh, tight wet puncture through heavy muscle, no music, no voice |
| 2026-07-27 | `attack-thrusting-blade-extremity-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.48s | 0.4 | one narrow blade punching through a hand, quick shallow puncture with a thin bone tick, no music, no voice |
| 2026-07-27 | `attack-work-blade-skull-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.22s | 0.4 | one short light blade slashing a face, shallow quick cut skidding across bone, no music, no voice |
| 2026-07-27 | `attack-work-blade-skull-02.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.48s | 0.4 | one small blade chopping at a skull, light bone tap with a thin wet edge, no music, no voice |
| 2026-07-27 | `attack-work-blade-neck-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.31s | 0.4 | one short blade slashing a neck, fast wet cut through soft tissue, no music, no voice |
| 2026-07-27 | `attack-work-blade-neck-02.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.13s | 0.4 | one small blade hacking at a neck, quick shallow wet chop, no music, no voice |
| 2026-07-27 | `attack-work-blade-ribcage-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.22s | 0.4 | one short blade cutting across a chest, shallow wet slice over bone, no music, no voice |
| 2026-07-27 | `attack-work-blade-gut-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.25s | 0.4 | one short blade slicing a belly, quick soft wet cut, no music, no voice |
| 2026-07-27 | `attack-work-blade-limb-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.48s | 0.4 | one short blade chopping into a shoulder, small dense wet hack, no music, no voice |
| 2026-07-27 | `attack-work-blade-extremity-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.24s | 0.4 | one short blade slashing a forearm, fast light cut with a thin bone tick, no music, no voice |
| 2026-07-27 | `attack-work-blade-extremity-02.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.09s | 0.4 | one small blade hacking at a hand, quick sharp chop with a small bone snap, no music, no voice |
| 2026-07-27 | `attack-work-blade-extremity-03.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.48s | 0.4 | one short blade striking a shin, light dry chop with thin flesh over bone, no music, no voice |
| 2026-07-27 | `death-01.wav` | `eleven_text_to_sound_v2` | 0.7s | 0.11s | 0.4 | a body dropping onto dry packed earth, dull heavy thud with cloth settling, no music, no voice |
| 2026-07-27 | `death-02.wav` | `eleven_text_to_sound_v2` | 0.7s | 0.24s | 0.4 | a body collapsing to the ground, heavy soft impact and gear scraping dirt, no music, no voice |
| 2026-07-27 | `death-03.wav` | `eleven_text_to_sound_v2` | 0.7s | 0.25s | 0.4 | a body falling knees first then flat, two stage thud on dry ground, no music, no voice |
| 2026-07-27 | `death-05.wav` | `eleven_text_to_sound_v2` | 0.7s | 0.18s | 0.4 | a body dropping heavily, dust and loose grit shifting under the weight, no music, no voice |
| 2026-07-27 | `death-06.wav` | `eleven_text_to_sound_v2` | 0.7s | 0.19s | 0.4 | a body slumping sideways onto packed soil, muffled thud with cloth dragging, no music, no voice |
| 2026-07-27 | `death-07.wav` | `eleven_text_to_sound_v2` | 0.7s | 0.14s | 0.4 | a body falling flat onto dry ground, sharp slap of flesh and cloth, no music, no voice |
| 2026-07-27 | `death-08.wav` | `eleven_text_to_sound_v2` | 0.7s | 0.25s | 0.4 | a body collapsing with a blade clattering down onto dirt beside it, no music, no voice |
| 2026-07-27 | `death-09.wav` | `eleven_text_to_sound_v2` | 0.7s | 0.18s | 0.4 | a body dropping onto dry grass and earth, soft crushed rustle under the weight, no music, no voice |
| 2026-07-27 | `victory-blue-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.36s | 0.4 | two soft glass notes rising a fifth, gentle bright interface notification, very short, clean decay, no reverb tail, no music, no voice |
| 2026-07-27 | `victory-blue-02.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.33s | 0.4 | two soft glass notes rising a fifth, gentle bright interface notification, very short, clean decay, no reverb tail, no music, no voice |
| 2026-07-27 | `victory-blue-03.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.48s | 0.4 | two soft glass notes rising a fifth, gentle bright interface notification, very short, clean decay, no reverb tail, no music, no voice |
| 2026-07-27 | `victory-red-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.48s | 0.4 | two soft mallet notes rising a fifth in a low warm register, gentle interface notification, very short, clean decay, no reverb tail, no music, no voice |
| 2026-07-27 | `victory-red-02.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.48s | 0.4 | two soft mallet notes rising a fifth in a low warm register, gentle interface notification, very short, clean decay, no reverb tail, no music, no voice |
| 2026-07-27 | `victory-red-03.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.34s | 0.4 | two soft mallet notes rising a fifth in a low warm register, gentle interface notification, very short, clean decay, no reverb tail, no music, no voice |
| 2026-07-27 | `draw-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.48s | 0.4 | two soft muted notes at the same pitch, flat and unresolved interface notification, very short, dry, no reverb tail, no music, no voice |
| 2026-07-27 | `draw-02.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.48s | 0.4 | two soft muted notes at the same pitch, flat and unresolved interface notification, very short, dry, no reverb tail, no music, no voice |
| 2026-07-27 | `draw-03.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.48s | 0.4 | two soft muted notes at the same pitch, flat and unresolved interface notification, very short, dry, no reverb tail, no music, no voice |
| 2026-07-27 | `ui-click-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.07s | 0.4 | one very short soft tap, subtle interface tick, tiny and clean, almost no tone, no reverb, no music, no voice |
| 2026-07-27 | `ui-click-02.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.07s | 0.4 | one very short soft tap, subtle interface tick, tiny and clean, almost no tone, no reverb, no music, no voice |
| 2026-07-27 | `ui-click-03.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.06s | 0.4 | one very short soft tap, subtle interface tick, tiny and clean, almost no tone, no reverb, no music, no voice |
| 2026-07-29 | `clash-shield-wasay-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.32s | 0.5 | one heavy axe head crashing into a large light wooden shield, blunt low crack with splitting wood fibres, dull dry plank break, dry packed earth, open air, very short, no ring, no metal, no reverb, no music, no voice |
| 2026-07-29 | `clash-shield-wasay-04.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.23s | 0.5 | one heavy axe head crashing into a large light wooden shield, blunt low crack with splitting wood fibres, dull dry plank break, dry packed earth, open air, very short, no ring, no metal, no reverb, no music, no voice |
| 2026-07-29 | `clash-shield-kampilan-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.15s | 0.5 | one heavy two-handed blade slamming flat into a large light wooden shield, deep hollow board thud with a shallow woody bite in front of it, dry rattan-bound plank, dry packed earth, open air, very short, no ring, no metal, no reverb, no music, no voice |
| 2026-07-29 | `clash-shield-kampilan-02.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.15s | 0.5 | one heavy two-handed blade slamming flat into a large light wooden shield, deep hollow board thud with a shallow woody bite in front of it, dry rattan-bound plank, dry packed earth, open air, very short, no ring, no metal, no reverb, no music, no voice |
| 2026-07-29 | `clash-shield-kampilan-04.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.14s | 0.5 | one heavy two-handed blade slamming flat into a large light wooden shield, deep hollow board thud with a shallow woody bite in front of it, dry rattan-bound plank, dry packed earth, open air, very short, no ring, no metal, no reverb, no music, no voice |
| 2026-07-29 | `clash-shield-kalis-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.08s | 0.5 | one narrow blade point punching into a light wooden shield, tight woody punch skidding off the board face with a thin rattan buzz, dry packed earth, open air, very short, no ring, no metal, no reverb, no music, no voice |
| 2026-07-29 | `clash-shield-kalis-02.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.11s | 0.5 | one narrow blade point punching into a light wooden shield, tight woody punch skidding off the board face with a thin rattan buzz, dry packed earth, open air, very short, no ring, no metal, no reverb, no music, no voice |
| 2026-07-29 | `clash-shield-kalis-03.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.07s | 0.5 | one narrow blade point punching into a light wooden shield, tight woody punch skidding off the board face with a thin rattan buzz, dry packed earth, open air, very short, no ring, no metal, no reverb, no music, no voice |
| 2026-07-29 | `clash-shield-kalis-04.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.07s | 0.5 | one narrow blade point punching into a light wooden shield, tight woody punch skidding off the board face with a thin rattan buzz, dry packed earth, open air, very short, no ring, no metal, no reverb, no music, no voice |
| 2026-07-29 | `clash-shield-itak-01.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.1s | 0.5 | one short light blade tapping a large light wooden shield, quick shallow dry woody clack on a thin plank, dry packed earth, open air, very short, no ring, no metal, no reverb, no music, no voice |
| 2026-07-29 | `clash-shield-itak-02.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.08s | 0.5 | one short light blade tapping a large light wooden shield, quick shallow dry woody clack on a thin plank, dry packed earth, open air, very short, no ring, no metal, no reverb, no music, no voice |
| 2026-07-29 | `clash-shield-itak-03.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.1s | 0.5 | one short light blade tapping a large light wooden shield, quick shallow dry woody clack on a thin plank, dry packed earth, open air, very short, no ring, no metal, no reverb, no music, no voice |
| 2026-07-29 | `clash-shield-itak-04.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.06s | 0.5 | one short light blade tapping a large light wooden shield, quick shallow dry woody clack on a thin plank, dry packed earth, open air, very short, no ring, no metal, no reverb, no music, no voice |
| 2026-07-29 | `clash-shield-wasay-02.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.12s | 0.4 | one heavy axe head slamming hard into a large light wooden shield, loud close impact, sharp blunt crack with splitting wood fibres, dry plank break, dry packed earth, open air, very short, no ring, no metal, no reverb, no music, no voice |
| 2026-07-29 | `clash-shield-wasay-03.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.16s | 0.4 | one heavy axe head slamming hard into a large light wooden shield, loud close impact, sharp blunt crack with splitting wood fibres, dry plank break, dry packed earth, open air, very short, no ring, no metal, no reverb, no music, no voice |
| 2026-07-29 | `clash-shield-kampilan-03.wav` | `eleven_text_to_sound_v2` | 0.5s | 0.15s | 0.4 | one heavy two-handed blade slamming hard into a large light wooden shield, loud close impact, deep board thud with a sharp woody bite in front of it, dry rattan-bound plank, dry packed earth, open air, very short, no ring, no metal, no reverb, no music, no voice |
