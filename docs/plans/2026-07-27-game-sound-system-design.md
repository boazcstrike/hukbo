# Game Sound System — Design

Date: 2026-07-27
Status: Design. This document does not authorize implementation on its own; the
companion plan is `2026-07-27-game-sound-system.md`.

## 1. Problem

Hukbo currently renders a battle silently. The owner wants to be able to drop
audio files into a folder, name them according to a documented contract, and
have the game use them automatically — with no content-pipeline edit, no code
change, and no rebuild. Files that are absent must simply stay silent rather
than crash or log an error.

The owner also wants the sound activity to be observable in its own log view,
separate from the battle event log, and hidden by default so it does not
compete with the battle log for screen space.

## 2. Scope

In scope:

- A fixed roster of sound slots, each with one canonical file name.
- Runtime discovery of audio files from a folder in the client output.
- Mapping of authoritative `BattleEvent` values to sound slots.
- Rate limiting, because a 200-agent battle can emit dozens of attacks per tick.
- A bounded sound cue log with its own panel, hidden by default.
- Session-only mute.
- Client tests for every pure part of the above.

Out of scope, deliberately:

- Music, ambience, looping beds, and positional or spatialised audio.
- Volume sliders, per-slot volume, or persisted audio settings. `ClientSettings`
  keeps schema version 1 and is not touched, so no settings migration is needed.
- Sound in `Hukbo.Headless`. The headless runner stays silent and unchanged.
- Any change to `Hukbo.Core`.

## 3. Where this sits in the architecture

The simulation stays authoritative and untouched. Audio is a second consumer of
the same per-tick presentation event buffer that already feeds the battle log
and the hit-effect system:

```
Deterministic simulation  (Hukbo.Core)
        |  BattleEvent (ordered, per tick)
        v
Presentation coordinator  (Hukbo.Client)
        |                        |                        |
        v                        v                        v
Battle event feed         Hit effect system         Sound director
(persistent-ish view)     (ephemeral visuals)       (ephemeral audio)
                                                          |
                                                          v
                                                   Sound cue log
                                                   (bounded, own panel)
```

Constraints this design must respect, from `CLAUDE.md` and
`SIMULATION-GAME-STANDARDS.md`:

- `Hukbo.Core` gains nothing: no audio types, no filesystem access, no new
  events. The state hash and the event hash cannot move, so the recorded seed-1
  baseline stays valid.
- Audio reads the event stream and never writes to it. Nothing in the audio path
  can decide targeting, damage, retreat, or victory.
- Only one file is allowed to reference MonoGame audio types. Every decision —
  which slot, whether it is throttled, what the log shows — lives in pure code
  that the client test project can construct without a `GraphicsDevice`, a
  `SpriteBatch`, an audio device, or a window.
- The effect must be discoverable by a spectator without reading source code.
  The sound panel lists every expected file name and its current status, so the
  panel itself is the documentation of what to name a file.

## 4. Sound roster

Nine slots. Each slot maps to exactly one canonical base file name, and the
loader looks for that base name plus `.wav`.

| Slot | File name | Trigger |
| --- | --- | --- |
| `AttackGreatBlade` | `attack-great-blade.wav` | `Attack` event whose weapon is `GreatBlade` |
| `AttackHeavyChopper` | `attack-heavy-chopper.wav` | `Attack` event whose weapon is `HeavyChopper` |
| `AttackThrustingBlade` | `attack-thrusting-blade.wav` | `Attack` event whose weapon is `ThrustingBlade` |
| `AttackWorkBlade` | `attack-work-blade.wav` | `Attack` event whose weapon is `Bolo` |
| `Death` | `death.wav` | `Death` event |
| `VictoryBlue` | `victory-blue.wav` | `Outcome` event, faction 0 |
| `VictoryRed` | `victory-red.wav` | `Outcome` event, faction 1 |
| `Draw` | `draw.wav` | `Outcome` event, any other faction value |
| `UiClick` | `ui-click.wav` | A control-bar, menu, or summary button command was accepted |

`Move` events are not mapped: they fire for most living agents on most ticks and
carry no moment worth hearing. `Damage` events are not mapped either, because
every `Damage` event this simulation emits is already accompanied by the
`Attack` event that caused it, so mapping both would double every hit.

Slot names follow the player-facing weapon descriptors required by the
historical accuracy policy — `Work Blade` rather than `Bolo` in the file name,
even though the `WeaponId` enum member is `Bolo`.

### File format

`SoundEffect.FromStream` accepts uncompressed PCM WAV data. It does not accept
Ogg, MP3, or FLAC. A file with an unusable format is treated exactly like a
corrupt file: the slot is marked `LoadFailed`, the panel says so, and the game
continues silently. This is documented in the folder's own README so the owner
does not have to guess why a file did nothing.

## 5. Folder and discovery

The audio folder is `Content/Audio/` beneath `AppContext.BaseDirectory`, which
mirrors how `Content/Themes/ui-theme-standards.json` is already located and
copied. The folder ships with a `README.md` that documents every expected file
name and the format requirement, which also guarantees the directory exists in
the build output for the owner to drop files into.

Discovery happens once, during `LoadContent`:

1. List the file names present in the folder. A missing folder yields an empty
   list rather than an exception.
2. For each slot, look for `<base-name>.wav`, matched case-insensitively so
   `Death.WAV` works on a case-sensitive filesystem too.
3. Load each match. A successful load produces a `Ready` binding; a failed load
   produces a `LoadFailed` binding; no match produces a `Missing` binding.

Discovery is deliberately one-shot rather than a filesystem watcher: adding a
file takes effect on the next launch. A watcher would add a background thread
and a reload race for no real benefit in a game that starts in under a second.

## 6. Rate limiting

Two hundred agents can produce dozens of `Attack` events in a single tick, and
the client can advance several ticks in one frame at 4x speed. Playing every
cue would be noise, and would also exhaust the platform's concurrent voice
budget.

The budget is applied per frame — reset by an explicit `BeginFrame` call at the
top of `Update`, then shared across however many ticks that frame advances:

- At most 3 cues per slot.
- At most 8 cues in total.

Cues are considered in `Sequence` order, which is the order the simulation
emitted them, so the surviving cues are the earliest ones rather than an
arbitrary subset. Suppressed cues are counted, not dropped silently: they appear
in the log as a `Suppressed` row with a count.

These numbers are tuning values with no historical or physical meaning, and they
are marked as such in code.

## 7. Sound cue log

A cue is one line of evidence: tick, slot, status, and a repeat count.

| Status | Meaning |
| --- | --- |
| `Played` | The file exists, loaded, and playback was requested |
| `Missing` | No file with that name is in the folder |
| `LoadFailed` | A file is present but could not be loaded as PCM WAV |
| `Muted` | Mute is on, so playback was skipped |
| `Suppressed` | The rate limit dropped this cue |

Consecutive cues that share tick, slot, and status collapse into one row whose
count increments. Without collapsing, a single tick of 40 suppressed attacks
would flush the whole log. The log retains at most 200 rows, matching the
battle event feed's documented retention.

The status precedence for one candidate cue is: `Missing` or `LoadFailed` first,
because a broken binding is the thing the owner most needs to see; then `Muted`;
then `Suppressed`; then `Played`. In particular, a missing file is reported even
while muted, so the panel is still useful for setting the folder up with the
sound off.

## 8. Panel and visibility

The sound log panel is hidden by default. It becomes visible through either of:

- A `Sounds` button on the always-visible control bar, which widens from three
  buttons to four.
- The `F9` key, gated by the same spectator input gate that already guards
  `Space` and the speed keys, so typing in the event log's search box cannot
  toggle it.

The shortcut hint line gains `F9: sound log` so the shortcut is discoverable
without reading source code.

When hidden, the layout is byte-for-byte what it is today: the battle event log
owns the full right column. When visible, the right column splits vertically —
battle events on top, sound log beneath, taking 45 percent of the column height
with a 168-pixel minimum. Nothing else on screen moves.

Inside the panel, the expected-files list is served before the cue log: it takes
the height it needs for all nine rows, capped only by the three cue rows the log
must still be able to show. At the default window size that yields the complete
file list plus a short live cue log, which is the right trade for a view whose
main job is telling the owner what to name a file.

The panel has three parts:

1. A header with the resolved folder path (clipped to fit), a
   `MISSING n/9` counter, and a `MUTE` toggle.
2. A binding list: one row per slot showing the expected file name and its
   status. This is the part that answers "what do I name the file?".
3. The live cue log, newest last, scrolled with the wheel while the pointer is
   over the panel.

The panel consumes pointer input when the pointer is inside it, and is inserted
into the existing pointer priority chain after the battle event log and before
the inspector.

## 9. Testability

Everything except `MonoGameSoundPlayer` is free of MonoGame audio types and is
directly constructible in `Hukbo.Client.Tests`:

- `SoundCatalog` — slot to file name mapping; tested for completeness and for
  file-name uniqueness across every enum member.
- `SoundLibrary` — resolution from a supplied list of file names; tested for
  match, case-insensitive match, extension rejection, and absence.
- `SoundCueMapper` — `BattleEvent` to slot; tested for every event kind and
  every weapon.
- `SoundCueBudget` — per-slot and total caps; tested at and past both limits.
- `SoundCueLog` — collapsing, retention, and scrolling.
- `SoundDirector` — the whole decision path against a recording fake player;
  tested for mute, missing bindings, suppression, and the fact that it never
  asks the player to play an unbound slot.
- `SoundLogPanel` — pure layout and hit testing only, following the same
  partial-class split the battle event log panel already uses.

`MonoGameSoundPlayer` is constructed only from `LoadContent` and disposed from
`UnloadContent`, so no test and no headless run ever touches an audio device.

## 10. Feature proposal questions

1. **What does the player observe?** Battle hits, deaths, and outcomes make
   sound if the owner has supplied files; the sound panel shows what is playing,
   what is missing, and what was rate limited.
2. **Can a spectator discover it without reading source?** Yes — the `Sounds`
   button, the `F9` hint in the shortcut line, and the panel's binding list with
   expected file names and statuses.
3. **Does it touch determinism?** No. `Hukbo.Core` is unchanged, both hashes are
   unchanged, and audio never feeds back into simulation state.
4. **Does it touch the snapshot?** No. Nothing here is saved.
5. **What is the per-tick cost?** One pass over the tick's events, no allocation
   in the steady state beyond log rows, and at most 8 playback calls per frame.
6. **What happens with no content?** Every slot is `Missing`, the game is
   silent, and the panel says which file names it wants.
7. **What is provisional?** The rate-limit constants and the fixed cue volume.
   Both are marked as tuning values in code.
8. **Does it add a dependency?** No. `MonoGame.Framework.DesktopGL` already
   ships the audio API.
9. **What does it forbid later?** Nothing. Music, ambience, and a volume slider
   can all be added on top; a volume slider is the one that would require a
   `ClientSettings` schema bump.

## 11. Rejected alternatives

- **MGCB content pipeline entries.** Adding sounds to `Content.mgcb` would
  require the owner to edit the pipeline file and rebuild content for every new
  file. That directly contradicts the requirement to just rename a file in a
  folder.
- **A filesystem watcher for hot reload.** Extra thread, reload races, and
  disposal hazards during a frame, in exchange for saving one relaunch.
- **Emitting audio cues from `Hukbo.Core`.** Would put presentation concerns in
  the authoritative layer and risk the hashes. The event stream already carries
  everything audio needs.
- **Folding sound rows into the battle event log.** The owner explicitly asked
  for a separate view, and mixing them would make the battle log unreadable
  during a busy tick.
