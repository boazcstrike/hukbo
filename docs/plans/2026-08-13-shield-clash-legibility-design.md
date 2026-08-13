# Shield-clash audio legibility — design

Written 2026-08-13, after row 173 of the shield-clash audio family failed at an
interactive desktop. This document does not authorize implementation; the plan
document beside it does.

## 1. What failed

Row 173 asks a person to compare the four melee shield-clash slots by ear:

> The War Axe reads heavier and blunter than the Work Blade against the same
> shield, and the Work Blade is the quietest of the four.

The tester's verdict on 2026-08-13 was "i cannot distinguish, sounds the same
for most". The other four rows of that family — 172, 174, 175, and 176 — passed
in the same session, so the slots resolve, load, and play; the failure is that
the four of them do not read as four different weapons.

## 2. The measured cause

The sixteen melee clash takes on disk are not level-matched, and the spread
between takes inside one slot is larger than the spread between the four slots.
Peak amplitude, measured from the WAV samples in
`src/Hukbo.Client/Content/Audio/`:

| Slot | Take 01 | Take 02 | Take 03 | Take 04 |
| --- | --- | --- | --- | --- |
| `clash-shield-kampilan` | 0.207 | 0.449 | 1.000 | 0.302 |
| `clash-shield-wasay` | 0.096 | 1.000 | 0.160 | 0.200 |
| `clash-shield-kalis` | 0.926 | 0.168 | 0.882 | 0.717 |
| `clash-shield-itak` | 0.189 | 1.000 | 0.393 | 1.000 |

`SoundVariantSelector.Select` picks a take uniformly across whichever takes
exist, deterministically from the tick and the source entity identifier. So the
loudness of a shield block is decided by which take was drawn, not by which
weapon struck: a Wasay block can arrive at a tenth of the amplitude of an Itak
block, which is the exact inversion of what row 173 asks the listener to hear.

Nothing in the playback path can correct this. `SoundDirector.CueVolume` is one
shared constant of `0.65` for every slot in the catalog, `SoundVoiceLedger`
divides it by the square root of the voices already sounding, and
`MonoGameSoundPlayer.Play` passes `pitch: 0f` and `pan: 0f`. There is no
per-slot gain, no per-file correction, and no timbral control of any kind.
`src/Hukbo.Client/Content/Audio/README.md` already states that individual files
are expected to be normalised by whoever authored them; the measurements above
say that was never done for these sixteen.

The clash cue is not being throttled. `SoundCueBudget` allows 16 cues per sound
and 64 in total against measured peaks of 21 cues per frame at 500 agents, and
row 175 passed with no `LIMITED` or `REFUSED` row for any clash slot, so the
budget is not a contributor.

## 3. What this change does

Two presentation-only mechanisms, both inside `Hukbo.Client`.

**Per-take loudness normalisation, in the sample domain.** Each clash take's
peak amplitude is read from its own WAV samples once, at load, and every sample
is then scaled so the take peaks at a common reference level. The take-to-take
spread disappears, so a slot sounds the same from one block to the next and the
listener is comparing weapons rather than takes.

It has to be done to the samples rather than to the playback volume, and that
is not a stylistic preference. `SoundEffectInstance` takes a volume in `[0, 1]`,
so a volume multiplier can only ever attenuate: the quietest take on disk peaks
at 0.096, and no multiplier applied at `Play` can raise it, because the product
clamps at full volume long before it gets there. Scaling the samples and
building the `SoundEffect` from the scaled buffer is the only way to lift a
quiet take, and lifting the quiet takes is most of what this change is for.

**A per-slot voicing table.** Each of the four melee clash slots gets a relative
level and a pitch offset, so that after normalisation the four are deliberately
different from one another in the direction row 173 describes: the Wasay
heaviest and lowest, the Kampilan close behind it, the Kalis in the middle, and
the Itak the lightest, highest, and quietest of the four. Pitch is the one
timbral control the shipped backend already offers — `SoundEffect.Play` takes it
and the code currently passes zero — and lowering the pitch of a short wooden
impact is what makes it read as a heavier object hitting the same board.

Every value in that table is a provisional tuning value and is marked as one in
code. None of it is a measurement of a real weapon, and none of it is a
historical claim.

## 4. Why this cannot make the mix worse

The reference peak is `0.85`, the highest per-slot level is `1.0`, and the
shared `CueVolume` is `0.65`, so the loudest clash cue this change can produce
is `0.85 × 1.0 × 0.65 = 0.55` before the voice ledger scales it down. Today a
clash cue that draws a full-scale take plays at `1.0 × 0.65 = 0.65`. The change
therefore lowers the loudest clash cue rather than raising it, which is what
keeps it clear of the flattening that
`docs/research/SOUND-CAPACITY-MEASUREMENTS.md` records at higher gains. The
scale factor is clamped so that a near-silent take cannot be amplified into
noise, and scaling never clips, because the target is derived from the take's
own measured peak.

What does rise is the quiet end. A take that plays at 0.096 today plays at
0.55 afterwards, which is the whole point: those are the blocks a listener
currently does not register as blocks at all. Simultaneous clash cues are
therefore louder in aggregate than they were, and that is what the second of
the two fresh smoke rows exists to check.

## 5. Scope, and what is deliberately not in it

**Only the four melee shield-clash slots are voiced.** Every other slot keeps
identity voicing — multiplier `1.0`, pitch `0` — and plays exactly as it plays
today. The same take-to-take spread almost certainly exists in the attack and
death families, but normalising those would change the loudness of cues whose
smoke rows have already passed and would move the measured mix headroom that
the sound gain compensation work established. That is a separate question with
its own evidence, and this change does not answer it.

**No sound file is regenerated.** Regenerating the sixteen clash takes with
consistent generation parameters is a real option and a better long-term
answer to the timbral half of row 173, but it spends ElevenLabs credits, so it
stays unauthorised and is not part of this change.

**No simulation code is touched.** Nothing here reaches `Hukbo.Core`, no state
or event hash moves, and the canonical gate's seed-1 baselines must be
unchanged. If either hash moves, this change is wrong.

## 6. The nine questions, in short

The effect a spectator discovers without reading source code is that a shield
block sounds like the weapon that made it: an axe blow into a board reads
heavier and lower than a work blade tapping the same board, and repeated blocks
from one weapon sound like one weapon rather than like four different events.
That is the whole point of the change, and row 173 is the only thing that can
certify it.

## 7. Verification

The gate proves nothing about this beyond the absence of regression. What the
automated tests can prove is the peak reader against synthetic WAV bytes, the
voicing table's ordering and clamps as pure functions, and the fact that the
director multiplies the gain and passes the pitch through to the player. What
they cannot prove is that a person hears four weapons, which is row 173.

Rows 172 and 175 passed on 2026-08-13 against the levels this change replaces.
Under the live checklist's own rule, a change that touches what a closed row
tested needs fresh rows rather than a revival of the lifted ones, so this
change owes two: one that a shield block still reads as wood rather than as a
landed cut, and one that a full 200-agent battle has not become a wall of
clash noise now that the quiet takes are audible.
