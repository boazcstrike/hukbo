# Sound slots not yet decided

This file lists sounds the weapon-clash work *could* use but that nobody has
committed to yet. It is a decision list, not a naming contract — the naming
contract is [README.md](README.md), and a slot only becomes real once it is added
to `SoundCatalog.AllSounds`.

Nothing still listed here exists in code today. `GameSoundId` has thirteen
members and the catalog lists the same thirteen. Of the clash outcomes, only a
shield block has a sound of its own: the four `clash-shield-<weapon>` slots
shipped on 2026-07-30 and have been taken off this list. A blow caught or
brushed aside by another weapon still makes no sound at all, which is the
intended behaviour for now: the simulation resolves the clash, the battle event
log names it, and the audio layer stays silent.

## Why these are deferred rather than authored

Two separate reasons, and it is worth keeping them apart.

**The first is a deliberate choice.** Each of these sounds changes how a
200-agent battle reads, and at that density audio decisions are easy to get
wrong in a way that is hard to undo — a clash cue that seems crisp in isolation
becomes a wall of noise when forty of them land in one second. Each slot below
should be decided on its own, listened to in a real battle, and kept only if it
earns its place.

**The second was a hard constraint, and it has now been solved.** The sound log
panel is the documentation of what to name a file, and until 2026-07-30 it had no
room left. Two separate things were wrong with it, and both are fixed.

The smaller problem was the panel's height. At the `420x396` the client used to
produce at a default window, the expected-files section capped at 200 pixels and
the nine slots of the day needed exactly 200 — zero slack. Row height is derived
from the measured baked line spacing of the Caption font rung (20 pixels), so it
cannot simply be made smaller without clipping descenders; the font work that
landed on `main` documents that the naive smaller estimate undershot. The panel
now takes 65 per cent of its column rather than 62, which is a real height of 416
and a viewport of ten rows.

The larger problem was not height at all, and this page used to describe it
wrongly. It claimed that a tenth slot would overflow the panel and vanish without
warning, which was untrue on two counts. The panel already drew a `+N more` line,
so nothing ever went missing without a trace. And the real constraint was
structural rather than a matter of a few pixels: the expected-files viewport was
capped at `SoundCatalog.AllSounds.Count` rows while `BuildBindingRows` emits
thirty-seven of them, so twenty-four rows were out of reach at any window size at
all, and no percentage could ever have changed that.

The expected-files list now scrolls. The mouse wheel moves it while the pointer
is over it, every rendered row can be reached, and the `+N more` line is gone
because the only remedy it named — a bigger panel — was never one. So the
panel-space question this page said "gates every slot on this page" is answered,
and it gates nothing further. Whether to add a slot is now only the first
question above: whether the sound earns its place in a real battle.

## Clash slots

The weapon-clash design assumed three of these. The shield cue is no longer one
of them, so two are left. The simulation distinguishes the outcomes today; only
the audio is missing.

| Proposed file name | Would play when | Notes |
| --- | --- | --- |
| `clash-blade-hard.wav` | A blow is caught and arrested blade-on-blade | The loud one. Research puts hard clashes at roughly 35 per cent of weapon interceptions, concentrated in the heavy pairings — a Great Blade defending against a Heavy Chopper is the extreme at about 0.46 |
| `clash-blade-soft.wav` | A blow is brushed or redirected rather than caught | Roughly 65 per cent of weapon interceptions. Should be shorter, drier, and much less present than the hard variant — this is the common case and it is what will drown the mix if it is too loud |

Design research suggests 3 to 5 numbered takes per slot before repetition becomes
audible, and that at 200 agents individual identity dissolves into texture above
roughly 4 to 6 concurrent impacts anyway — so effort is better spent on making
the hard and the soft variant clearly distinct from each other than on deepening
either one of them.

### The shield cue shipped, in a different shape

This page proposed one classless `clash-shield.wav` single, on the reasoning that
the shield is a third *material* alongside the two blade cases. That is not what
shipped. On 2026-07-30 the shield cue landed as **four** classless slots keyed to
the attacking weapon — `clash-shield-kampilan`, `clash-shield-wasay`,
`clash-shield-kalis`, and `clash-shield-itak` — so that a spectator can hear
which weapon was stopped rather than only that something was. Each weapon is its
own slot, and nothing substitutes across them: a slot with no take is silent and
reports `MISSING`.

Two things are worth carrying forward from that decision. The material the four
takes aim at is a light fibrous plank, bound with rattan and coated in resin,
with no boss, no metal facing, and no ring — **Documented, form uncertain**. That
the four sound different from one another is a gameplay-legibility choice and
rests on no evidence whatsoever; no source distinguishes them, and the design
document says so in as many words.

The full reasoning, the evidence tier behind every claim, and the panel
arithmetic are in
[docs/archives/2026-07-29/2026-07-29-shield-clash-audio-design.md](../../../../docs/archives/2026-07-29/2026-07-29-shield-clash-audio-design.md).

## Swing slots — open question, no design yet

Whether an attack should make a sound as the weapon travels, separately from the
sound of it landing, is undecided.

The argument for: it is the only audio cue that would distinguish an attack that
was evaded from no attack at all, since a void currently produces silence and its
discoverability rests on the event log alone.

The argument against: every living agent in reach attacks on its cooldown, so a
swing cue fires far more often than any impact cue. At 200 agents this is the
single most likely sound in the game to become continuous noise, and the frame
budget (`SoundCueBudget`, 16 per slot and 64 per frame) would spend most of itself
on swings before a death cue could be heard.

| Proposed file name | Would play when | Status |
| --- | --- | --- |
| `swing-great-blade.wav` | A Great Blade attack begins, before it resolves | Not designed |
| `swing-heavy-chopper.wav` | A Heavy Chopper attack begins | Not designed |
| `swing-thrusting-blade.wav` | A Thrusting Blade attack begins | Not designed |
| `swing-work-blade.wav` | A Work Blade attack begins | Not designed |

If swings are ever added, the budget question has to be answered first: whether
they get their own reservation, whether they are suppressed by camera zoom, and
whether they outrank or yield to impact cues. A swing cue that starves a death
cue is an inverted priority.

## What to do when a slot is wanted

1. Append the member to `GameSoundId` **and** its entry to
   `SoundCatalog.AllSounds` in the same change — `SoundCatalogTests.AllSounds_ListsEveryDeclaredSlotExactlyOnce`
   enumerates the enum, so adding one without the other fails the build.
2. Map it in `SoundCueMapper`. A classless slot has to reach the player under
   `(sound, null)`: `MonoGameSoundPlayer.GetStatus` keys on `(sound, hitClass)`
   and registers classless slots only under `(sound, null)`, so a non-null class
   makes the cue resolve `Missing` and never play. Since 2026-07-30
   `SoundDirector` derives the hit class from
   `SoundCatalog.IsHitLocationDriven(sound)` rather than from the event, so a new
   classless slot needs no change in the director at all. Do not change that
   derivation back to reading the event: doing so would silence every classless
   slot whose event happens to carry a hit location, with no crash and no
   complaint anywhere except
   `SoundDirectorTests.Ingest_UsesANullHitClassForAShieldBlockDespiteTheHitLocation`,
   which exists to catch exactly that.
3. Add the row to [README.md](README.md), which is the actual contract.
4. Generate takes with `./scripts/sfx.ps1 -Slot <name>` and let it write the
   provenance row in [GENERATED.md](GENERATED.md).
5. Listen to it in a real 200-agent battle before keeping it.
