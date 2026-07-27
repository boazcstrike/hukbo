# Sound slots not yet decided

This file lists sounds the weapon-clash work *could* use but that nobody has
committed to yet. It is a decision list, not a naming contract — the naming
contract is [README.md](README.md), and a slot only becomes real once it is added
to `SoundCatalog.AllSounds`.

Nothing here exists in code today. `GameSoundId` has nine members and the catalog
lists the same nine. A clash currently makes no sound at all, which is the
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

**The second is a hard constraint that has to be solved first.** The sound log
panel is the documentation of what to name a file, and it has **no room left**.
At the real `420x396` the client produces at a default window, the expected-files
section caps at 200 pixels and the nine current slots need exactly 200 — zero
slack. Row height is derived from the measured baked line spacing of the Caption
font rung (20 pixels), so it cannot simply be made smaller without clipping
descenders; the font work that landed on `main` documents that the naive smaller
estimate undershot.

So **any tenth slot overflows the panel and silently hides itself**, which is the
one failure the panel exists to prevent. `SoundLogPanelTests.CalculateLayout_ShowsEveryExpectedFileNameAtTheDefaultSize`
catches it. Adding any slot below therefore requires one of:

- a taller sound log panel, which takes height from the battle event log beside
  it and means touching `RightColumnSplit`;
- a scrollable expected-files list, which is the largest change and makes a slot
  you have to scroll to weaker documentation than one you can see;
- or dropping the cue log's three reserved rows, which trades away the evidence
  surface and is almost certainly the wrong trade.

That decision is not made. It should be made once, for however many slots end up
being wanted, rather than repeatedly.

## Clash slots

These are the three the weapon-clash design assumed. The simulation distinguishes
the outcomes today; only the audio is missing.

| Proposed file name | Would play when | Notes |
| --- | --- | --- |
| `clash-blade-hard.wav` | A blow is caught and arrested blade-on-blade | The loud one. Research puts hard clashes at roughly 35 per cent of weapon interceptions, concentrated in the heavy pairings — a Great Blade defending against a Heavy Chopper is the extreme at about 0.46 |
| `clash-blade-soft.wav` | A blow is brushed or redirected rather than caught | Roughly 65 per cent of weapon interceptions. Should be shorter, drier, and much less present than the hard variant — this is the common case and it is what will drown the mix if it is too loud |
| `clash-shield.wav` | A shield takes the blow | Wood and hide, not metal. Distinct timbre from both blade variants |

Design research suggests 3 to 5 numbered takes per slot before repetition becomes
audible, and that at 200 agents individual identity dissolves into texture above
roughly 4 to 6 concurrent impacts anyway — so effort is better spent on making
the three *materials* distinguishable than on deepening any one slot.

## Swing slots — open question, no design yet

Whether an attack should make a sound as the weapon travels, separately from the
sound of it landing, is undecided.

The argument for: it is the only audio cue that would distinguish an attack that
was evaded from no attack at all, since a void currently produces silence and its
discoverability rests on the event log alone.

The argument against: every living agent in reach attacks on its cooldown, so a
swing cue fires far more often than any impact cue. At 200 agents this is the
single most likely sound in the game to become continuous noise, and the frame
budget (`SoundCueBudget`, 3 per slot and 8 per frame) would spend most of itself
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

1. Decide the panel-space question above. It gates every slot on this page.
2. Append the member to `GameSoundId` **and** its entry to
   `SoundCatalog.AllSounds` in the same change — `SoundCatalogTests.AllSounds_ListsEveryDeclaredSlotExactlyOnce`
   enumerates the enum, so adding one without the other fails the build.
3. Map it in `SoundCueMapper`. Classless slots must pass a `null` hit class:
   `MonoGameSoundPlayer.GetStatus` keys on `(sound, hitClass)` and registers
   classless slots only under `(sound, null)`, so passing a non-null class makes
   the cue resolve `Missing` and never play.
4. Add the row to [README.md](README.md), which is the actual contract.
5. Generate takes with `./scripts/sfx.ps1 -Slot <name>` and let it write the
   provenance row in [GENERATED.md](GENERATED.md).
6. Listen to it in a real 200-agent battle before keeping it.
