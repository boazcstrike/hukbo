# Hukbo sound files

Drop audio files in this folder, name them exactly as listed below, and the game
uses them the next time it starts. A name that is not on the list is ignored, and
a name on the list with no file simply stays silent — nothing here can break a
battle.

Press `F9` in game, or use the `Sounds` button on the control bar, to open the
sound log. It shows this folder's resolved path, every name the game is looking
for, whether each one was found, and what happened to each cue. The
expected-files list shows ten rows at a time and scrolls with the mouse wheel
while the pointer is over it, so every slot below is reachable even though only
ten of them are on screen at once.

## File names

| File name | Plays when |
| --- | --- |
| `attack-kampilan.wav` | An agent attacks with a Kampilan — Great Blade |
| `attack-wasay.wav` | An agent attacks with a Wasay — War Axe |
| `attack-kalis.wav` | An agent attacks with a Kalis — Thrusting Blade |
| `attack-itak.wav` | An agent attacks with an Itak — Work Blade |
| `death.wav` | An agent dies |
| `victory-blue.wav` | The battle ends with Blue, faction 0, winning |
| `victory-red.wav` | The battle ends with Red, faction 1, winning |
| `draw.wav` | The battle ends in a draw |
| `ui-click.wav` | A control-bar, menu, or shortcut command is accepted |
| `clash-shield-kampilan.wav` | A blow from a Kampilan — Great Blade is stopped by a shield |
| `clash-shield-wasay.wav` | A blow from a Wasay — War Axe is stopped by a shield |
| `clash-shield-kalis.wav` | A blow from a Kalis — Thrusting Blade is stopped by a shield |
| `clash-shield-itak.wav` | A blow from an Itak — Work Blade is stopped by a shield |

The rows are in the order the sound log lists them, which is the order the
catalog declares.

Each of the four weapons has its own clash slot, and the game never substitutes
one for another. A shield block by a War Axe plays the War Axe slot or nothing
at all: if that slot has no file, the cue stays silent and the sound log reports
the slot as `MISSING` rather than reaching for another weapon's take. A wrong
weapon would be invisible in the log, whereas silence is not.

Matching ignores letter case, so `Death.WAV` works too.

## Variants

The bare name in the table above is a last resort rather than the usual case.
A slot normally holds several numbered takes, and the game picks between them so
that a repeated event does not sound identical every time.

A numbered take is named `<slot>-NN.wav`, where `NN` is exactly two digits and
counts upward from `01`. So `clash-shield-kampilan-01.wav` is the first take of
the Great Blade's shield-clash slot. One digit, three digits, and `00` are all
unrecognised, and a file named that way is ignored.

The four attack slots carry one extra token, because they also vary by where the
blow landed: `<slot>-<class>-NN.wav`, as in `attack-kampilan-skull-01.wav`. The
other nine slots, the four clash slots among them, have no hit class and use the
plain `<slot>-NN.wav` form.

Case is ignored here too, so `Clash-Shield-Kampilan-01.WAV` resolves to the same
take.

A bare `<slot>.wav` still works and is used when a slot has no numbered take at
all, but nothing in the shipped set relies on it. That is why
`./scripts/sfx.ps1 -List`, which probes only the bare name, reports a slot as
missing even when all of its numbered takes are present.

## Generating a file

If you do not have a recording for a slot, `scripts/sfx.ps1` generates one with
the ElevenLabs text-to-sound-effects API and writes it here in the right format
and under the right name.

```powershell
./scripts/sfx.ps1 -List                  # every slot and whether it has a file
./scripts/sfx.ps1 -Slot death            # generate with the built-in prompt
./scripts/sfx.ps1 -Slot ui-click -Prompt 'one very short dry wooden tick' -Force
```

The key comes from `ELEVENLABS_API_KEY`, read from the environment or from the
repository's untracked `.env` file.

This is an authoring step that a person runs on purpose. The game never calls
it, so building, testing, and playing Hukbo stay completely offline. Each run
appends the prompt it used to `GENERATED.md` in this folder.

Two things about the results are worth knowing before you generate a set:

- The model is not deterministic, and takes vary in quality. A take that peaks
  too low to hear is rejected without writing anything, so retrying is safe and
  is usually all that is needed. Everything else still has to be judged by ear.
- The API will not produce anything shorter than half a second, so a hit is
  generated long and then trimmed back to its audible part. Cues whose decay is
  the point — the victory gongs and the draw — are kept whole.

## Format

Files must be uncompressed PCM WAV. Ogg, MP3, FLAC, and WAV files using a
compressed codec cannot be loaded, and the sound log reports them as `FAILED`
rather than `MISSING` so a format problem is distinguishable from a naming one.

Short files work best. Keep a hit around a tenth of a second and normalise the
files against each other, because the game plays every cue at one fixed volume
and does not mix or duck anything.

## Which folder

Two locations work:

- This folder in the repository, `src/Hukbo.Client/Content/Audio/`. Files here
  are copied into the build output, so they survive a rebuild and can be checked
  in if you want them versioned.
- The folder the running game reads, `Content/Audio/` next to the executable.
  Dropping a file straight in there needs no rebuild, but a clean rebuild will
  not restore it.

Either way, discovery happens once during startup. Adding or renaming a file
takes effect the next time you launch the game.

## Rate limiting

A battle of two hundred agents can produce dozens of attacks in a single tick.
The game plays at most 16 cues of the same kind and 64 cues in total per
frame; the rest are recorded in the sound log as `LIMITED` and not played. This
is deliberate — playing all of them would be noise, not feedback.
