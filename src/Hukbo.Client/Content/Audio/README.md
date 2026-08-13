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
| `release-bangkaw.wav` | A Bangkaw — Long Spear leaves the thrower's hand |
| `release-busog.wav` | An arrow leaves a Busog — War Bow |
| `release-arquebus.wav` | An Imported Arquebus fires |
| `attack-bangkaw.wav` | A thrown Bangkaw — Long Spear lands on its target |
| `attack-busog.wav` | An arrow from a Busog — War Bow lands on its target |
| `attack-arquebus.wav` | A shot from an Imported Arquebus lands on its target |
| `clash-shield-bangkaw.wav` | A blow from a Bangkaw — Long Spear is stopped by a shield |
| `clash-shield-busog.wav` | A blow from a Busog — War Bow is stopped by a shield |
| `clash-shield-arquebus.wav` | A blow from an Imported Arquebus is stopped by a shield |
| `miss-bangkaw.wav` | A thrown Bangkaw — Long Spear misses its target |
| `miss-busog.wav` | An arrow from a Busog — War Bow misses its target |
| `miss-arquebus.wav` | A shot from an Imported Arquebus misses its target |
| `misfire-arquebus.wav` | An Imported Arquebus fails to fire |

The rows are in the order the sound log lists them, which is the order the
catalog declares.

Each of the seven attack weapons and each of the seven clash-shield weapons has
its own slot, and the game never substitutes one for another. A shield block by
a War Axe plays the War Axe slot or nothing at all: if that slot has no file,
the cue stays silent and the sound log reports the slot as `MISSING` rather than
reaching for another weapon's take. A wrong weapon would be invisible in the
log, whereas silence is not.

The three ranged weapons — the Bangkaw, the Busog, and the Arquebus — add a
release slot for the moment a shot leaves the weapon and, for the Arquebus
alone, a misfire slot for the weapon failing to fire at all. A miss slot plays
when a ranged attack is evaded; the four melee weapons have no miss slot and
keep using their shared weapon-impact sound for an evaded blow, which is a
known, deliberate limitation rather than an oversight.

Matching ignores letter case, so `Death.WAV` works too.

## Variants

The bare name in the table above is a last resort rather than the usual case.
A slot normally holds several numbered takes, and the game picks between them so
that a repeated event does not sound identical every time.

A numbered take is named `<slot>-NN.wav`, where `NN` is exactly two digits and
counts upward from `01`. So `clash-shield-kampilan-01.wav` is the first take of
the Great Blade's shield-clash slot. One digit, three digits, and `00` are all
unrecognised, and a file named that way is ignored.

The seven attack slots carry one extra token, because they also vary by where
the blow landed: `<slot>-<class>-NN.wav`, as in `attack-kampilan-skull-01.wav`
and `attack-bangkaw-ribcage-01.wav`. The other nineteen slots, the seven clash
slots among them, have no hit class and use the plain `<slot>-NN.wav` form.

Case is ignored here too, so `Clash-Shield-Kampilan-01.WAV` resolves to the same
take.

A bare `<slot>.wav` still works and is used when a slot has no numbered take at
all, but nothing in the shipped set relies on it. `./scripts/sfx.ps1 -List`
counts a slot as present under the same rule the game uses — a bare file or any
numbered take, class-scoped or not — so it reports a slot present as soon as one
real take exists, whichever form it takes.

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

## Shield-clash normalisation

The sixteen takes behind the four melee shield-clash slots —
`clash-shield-kampilan`, `clash-shield-wasay`, `clash-shield-kalis`, and
`clash-shield-itak`, four numbered takes each — were never level-matched
against one another. Measuring the peak amplitude of each take directly from
its WAV samples gave the following, on a scale where `1.000` is full scale:

| Slot | Take 01 | Take 02 | Take 03 | Take 04 |
| --- | --- | --- | --- | --- |
| `clash-shield-kampilan` | 0.207 | 0.449 | 1.000 | 0.302 |
| `clash-shield-wasay` | 0.096 | 1.000 | 0.160 | 0.200 |
| `clash-shield-kalis` | 0.926 | 0.168 | 0.882 | 0.717 |
| `clash-shield-itak` | 0.189 | 1.000 | 0.393 | 1.000 |

The spread within a single slot is larger than the spread between the four
slots, so which take a block happened to draw mattered more than which weapon
struck. A tester reported this directly on 2026-08-13: unable to tell the four
slots apart by ear.

The game now corrects this at load rather than by re-recording anything.
Every clash take's peak is read once when the file loads, using the same
`WavePeak` reader documented in `src/Hukbo.Client/Audio/WavePeak.cs`, and
turned into a per-take gain multiplier that brings that take to a common
reference peak of `0.85`. On top of that, the four slots carry a small,
deliberately provisional voicing table — a relative level and a pitch offset
each — so that once the takes are level-matched, the four weapons still read
as different from one another: the Wasay plays heaviest and lowest, the
Kampilan close behind it, the Kalis in the middle, and the Itak lightest,
highest, and quietest. See `src/Hukbo.Client/Audio/SoundVoicing.cs` for the
exact values and their reasoning.

No sound file was regenerated to make this change, and no ElevenLabs credit
was spent producing it. The sixteen files on disk are exactly the ones that
were already there; only how loudly and at what pitch the game plays them
changed. Every other slot in the catalog is unaffected and still plays at the
flat volume and zero pitch offset described elsewhere on this page.

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
