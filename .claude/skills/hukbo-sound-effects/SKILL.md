---
name: hukbo-sound-effects
description: Generates Hukbo's game sound files with the ElevenLabs text-to-sound-effects API through scripts/sfx.ps1. Use when asked to create, generate, regenerate, or replace a sound effect, when a sound slot is missing or sounds wrong, when writing or tuning an ElevenLabs prompt for a hit, death, victory, draw, or UI click, or when a generated WAV fails to load. Covers the slot contract, the API key rule, the PCM WAV format requirement, and the fact that generation is an authoring step that never runs during a build or a battle.
---

# Generating Hukbo sound effects with ElevenLabs

## What this is and is not

`scripts/sfx.ps1` calls the ElevenLabs text-to-sound-effects API and writes an
uncompressed PCM WAV file into `src/Hukbo.Client/Content/Audio/`.

It is an authoring tool that a person runs deliberately. Nothing in the game,
the build, the tests, or the canonical gate calls it. The client only reads
whatever WAV files happen to be in the content folder at startup, so the game
stays fully offline and the simulation never touches the network. Do not add a
call to this script into any build step, test, or `verify.ps1` stage.

Generation is also not deterministic and does not need to be. Audio is
presentation, it never reaches a state hash, and running the script twice with
the same prompt gives two different files. That is expected.

## The slot contract

The game plays sounds for nine fixed slots declared in
`src/Hukbo.Client/Audio/SoundCatalog.cs`. A file with any other name is ignored,
and a slot with no file simply stays silent.

`scripts/sfx.ps1` parses the slot names straight out of `SoundCatalog.cs`, so
the catalog remains the single source of truth. If a slot is ever added to the
catalog, the script picks it up automatically, but it will have no default
prompt until one is added to the `$defaultPrompts` table in the script.

```powershell
./scripts/sfx.ps1 -List
```

That prints every slot, its built-in default prompt, and whether a file already
exists. Start here whenever the question is "which sounds are missing".

## Generating one

```powershell
./scripts/sfx.ps1 -Slot death
./scripts/sfx.ps1 -Slot attack-kampilan -Prompt 'single heavy steel blade cleaving flesh, wet impact, no music' -Duration 0.4
./scripts/sfx.ps1 -Slot ui-click -Force        # replace an existing file
./scripts/sfx.ps1 -Slot draw -DryRun           # resolve everything, send nothing
```

Useful parameters:

| Parameter | Meaning |
| --- | --- |
| `-Prompt` | Overrides the slot's default prompt. Omit it to use the default |
| `-Duration` | Seconds, 0.5 to 30. Defaults to the slot's recommended length |
| `-Trim` / `-NoTrim` | Overrides the slot's trimming default. Hits, death, and the UI click trim; the victory gongs and the draw cue keep their full decay |
| `-SilenceThreshold` | Percent of the file's own peak that counts as quiet, default 5. A generation has a room-tone floor of a few percent, so this is never measured against zero |
| `-AllowQuiet` | Keeps a take peaking below ten percent of full scale, which is rejected by default |
| `-PromptInfluence` | 0 to 1, default 0.4. Higher follows the prompt more literally |
| `-SampleRate` | 16000, 22050, 24000 (default), or 44100. 44100 needs an ElevenLabs Pro subscription |
| `-Force` | Replaces an existing file. Without it an existing file is never overwritten |
| `-DryRun` | Prints the resolved request and exits without calling the API |

Every successful run appends a row to
`src/Hukbo.Client/Content/Audio/GENERATED.md` recording the date, file, model,
duration, prompt influence, and prompt. Keep that log intact: it is how a sound
in the repository is traced back to the prompt that produced it.

## The API key

The key is read from `ELEVENLABS_API_KEY`, first from the environment and then
from the repository's `.env` file, which `.gitignore` excludes. The environment
wins, so a one-off override needs no file edit.

`.env` is untracked and must stay that way. Never move the key into a tracked
file, never commit it, never echo it into output, a log, a document, or a commit
message, and never pass it on a command line where it lands in shell history.
Do not read `.env` into your own output when investigating something else.

If a key is ever exposed, say so plainly and tell the owner to rotate it in the
ElevenLabs dashboard.

## Format rules that break playback if ignored

- MonoGame's `SoundEffect.FromStream` reads **uncompressed PCM WAV only**. MP3,
  Ogg, FLAC, and compressed-codec WAV files fail to load, and the in-game sound
  log reports them as `FAILED` rather than `MISSING`.
- The script requests raw PCM and writes the RIFF header itself, so the output
  is correct by construction. Do not "helpfully" swap the request to MP3.
- Raw PCM carries no header, so the script infers the channel count from the
  returned byte count against the requested duration. If it cannot decide, it
  fails and asks for an explicit `-Channels`. Do not guess past that error.
- Keep hits short, around a tenth to a third of a second, and normalise the set
  by ear. The game plays every cue at one fixed volume and never mixes or ducks.
- The API will not generate anything under 0.5 seconds. A combat hit is
  therefore generated at 0.5 seconds and trimmed back afterwards: the script
  cuts the quiet run at both ends, keeping five milliseconds before and ten
  after the audible part so neither cut can click. A real generation measured
  0.48 seconds in and 0.08 seconds out, which is the length the folder's README
  asks for. Slots whose decay is the point are not trimmed.

## Takes vary, so listen before moving on

The model is not deterministic and the quality of a take swings hard. One run of
the same `ui-click` prompt came back peaking at 93 percent of full scale, and
another came back at under 1 percent — valid audio, completely inaudible in a
battle.

The script rejects a take peaking below ten percent of full scale and writes
nothing, so a bad take can never replace a good file even with `-Force`. When
that happens, simply run the same command again for another take. `-AllowQuiet`
overrides the check, and it is almost never the right answer.

The peak check only catches silence. It cannot tell that a cue is the wrong
sound, has a musical tail, or contains a voice. Someone has to listen.

## Writing a good prompt

The sounds are diegetic combat feedback for a top-down battle with up to a few
hundred agents, so a cue is heard dozens of times per battle and layered over
other cues.

- Ask for one sound event, not a scene. "One heavy blade landing a cut", not
  "a battle with clashing swords and shouting".
- Always exclude music and voice explicitly. A stray musical tail or a human
  grunt ruins a cue that repeats this often.
- Say the surface and the space: dry packed earth, open air, no reverb.
- Weapon prompts use the player-facing descriptors required by the historical
  accuracy policy — Great Blade, Heavy Chopper, Thrusting Blade, Work Blade.
  Never put a specific cultural identification such as Kampilan or Panabas into
  a prompt or a file name.
- Do not chase realism claims. These are provisional sound designs, not
  reconstructions of a documented 1500s weapon sound, and nothing in the repo
  should imply otherwise.

## Verifying the result

Generation is not proof. To confirm a sound actually works:

1. `./scripts/run.ps1`
2. Press `F9`, or use the `Sounds` button on the control bar, to open the sound
   log.
3. Confirm the slot reports `READY` rather than `MISSING` or `FAILED`.
4. Watch a battle and listen for the cue.

Only a human at an interactive desktop may report that step as passing. Follow
the honesty protocol in `hukbo-verify-and-record`: compiling the client or
listing the file does not let you flip a smoke-test row in
`docs/development/testing.md` to `PASS`.

Adding a WAV file touches no C# code, so the canonical gate does not need to be
re-run for a sound change alone. Run it if any client code changed alongside it.
