# Sandata sound files

Every file in this folder is generated, not recorded. They are committed so
that a checkout can hear the game without an API key and without spending
anything, exactly as Hukbo's own `Content/Audio` folder works.

They are raw PCM WAV files read at run time from
`AppContext.BaseDirectory/Content/Audio`. They do **not** go through the
MonoGame content pipeline — `Content.mgcb` bakes fonts and sprites only.

## What is here, and what is deliberately not

The full Sandata sound catalog is 106 slots expanding to 524 variant files.
Generating all of it is roughly 104,800 ElevenLabs credits and that spend has
never been authorized. What was authorized, on 2026-08-11, is a narrow slice:
the two firearms a spectator actually meets on the shipped `angle-house`
mission, in the two acoustic environments an interior map reaches.

| Slot | Files | Weapon it serves |
| --- | --- | --- |
| `gun-762x39-single-close` | 6 | AK-pattern rifle, 7.62x39mm, fired in the open |
| `gun-762x39-single-indoor` | 6 | the same rifle, fired inside the house |
| `gun-9x19-single-close` | 6 | Glock-pattern pistol, 9x19mm, fired in the open |
| `gun-9x19-single-indoor` | 6 | the same pistol, fired inside the house |

Twenty-four files. Six variants per slot is not a preference — it is what
`SandataSoundCatalog` declares for these rows, and `ShotSlotResolver` picks a
variant uniformly across all six. Shipping fewer than six would leave a
proportion of shots resolving a filename that does not exist, which plays as
silence rather than as an error.

**The other three environments are still missing on purpose.** `outdoor`,
`distant`, and `suppressed` resolve to files that are not here, so a shot that
lands in one of those environments is silent. That is a known gap, not a
defect to re-report.

## How they were made

`./scripts/sfx.ps1 -Batch` against a four-row manifest, at 1.0 second, 24 kHz,
16-bit PCM, prompt influence 0.6, trimmed below 1.5 per cent of peak. The
prompts were:

- `loud gunshot, AK-47 assault rifle firing one round outdoors, close microphone, sharp dry crack, punchy, no music, no voice`
- `loud gunshot, AK-47 assault rifle firing one round inside a bare concrete room, close microphone, sharp crack with a tight hard slapback off the walls, no music, no voice`
- `loud gunshot, Glock 9mm pistol firing one round outdoors, close microphone, flat sharp snap with a metallic slide clack, no music, no voice`
- `loud gunshot, Glock 9mm pistol firing one round inside a bare concrete room, close microphone, flat snap with a tight hard slapback off the walls, no music, no voice`

**Prompt wording decides whether a take is audible at all, and this cost real
credits to learn.** The first prompts written for this run described the sound
the way the existing manifest does — "one single gunshot crack from a 7.62x39mm
firearm, close range in open air, no reverb, no music, no voice". Three takes
in a row came back peaking at between 0 and 1.3 per cent of full scale, which
`sfx.ps1` correctly refuses to write. Rewriting the same request to lead with
`loud gunshot`, to name the weapon in plain language rather than by cartridge
alone, to say `close microphone` instead of a distance in metres, and to carry
fewer trailing negations produced a take at 100 per cent of full scale on the
first attempt. Anyone extending this folder should start from the wording above
rather than from the manifest's default prompts.

Quiet takes still happen at roughly the rate the manifest tooling assumes, and
`sfx.ps1 -Batch` stops the whole run on the first one. Because it skips files
that already exist, re-running the same command resumes where it stopped; this
slice took four passes to fill all twenty-four.

## Regenerating

`./scripts/sfx.ps1` is an authoring tool. It is the only script here that
talks to a network service, it runs only when a person asks for a sound, and
it is not part of the build, the tests, or the canonical gate. It reads
`ELEVENLABS_API_KEY` from the environment or from the untracked `.env` file;
that key never belongs in a tracked file, in output, or in a commit message.

To replace a file, delete it and re-run the batch, or pass `-Force`.
