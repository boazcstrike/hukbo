# Shield-clash audio legibility — plan

Implements `docs/plans/2026-08-13-shield-clash-legibility-design.md`. Opened
2026-08-13 after row 173 of the shield-clash audio family failed at an
interactive desktop with the verdict "i cannot distinguish, sounds the same for
most".

Presentation-only. Nothing here touches `Hukbo.Core`, and the canonical gate's
seed-1 state hash, event hash, winner, and event stream must all be unchanged
when it is finished. If a hash moves, the change is wrong and is reverted rather
than re-pinned.

Branch and worktree: `shield-clash-legibility`, based on `main` at `8da5d92`.

## Tasks

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| SC-1 | A pure helper that reads the peak sample amplitude of an uncompressed PCM WAV from its bytes. Supports 16-bit mono and stereo, which is what `scripts/sfx.ps1` writes. Returns `false` for anything it does not understand — a compressed WAV, a bit depth other than 16, a truncated file — rather than guessing. Parses the RIFF chunk list properly instead of assuming `data` sits at a fixed offset. | `src/Hukbo.Client/Audio/WavePeak.cs` (new), `tests/Hukbo.Client.Tests/WavePeakTests.cs` (new) | Tests build a synthetic WAV byte array in memory and assert the peak, including a full-scale sample, a half-scale sample, silence, a stereo file, an unsupported bit depth, and a truncated buffer. No file on disk is read by any test. | — | `./scripts/test.ps1 -Configuration Release` |
| SC-2 | A pure voicing table giving each of the four melee shield-clash slots a relative level and a pitch offset, and identity voicing — level `1.0`, pitch `0` — to every other slot in the catalog. Values are provisional tuning values and the doc comment says so. The ordering the table encodes: Wasay heaviest and lowest, Kampilan just behind it, Kalis in the middle, Itak the lightest, highest, and quietest of the four. | `src/Hukbo.Client/Audio/SoundVoicing.cs` (new), `tests/Hukbo.Client.Tests/SoundVoicingTests.cs` (new) | Tests assert the four clash slots' relative order against literal expected values rather than against the constants under test, assert that every non-clash slot receives identity voicing, and assert pitch stays inside MonoGame's `[-1, 1]`. | — | `./scripts/test.ps1 -Configuration Release` |
| SC-3 | Wire both into playback. `ISoundPlayer.Play` gains a `pitch` parameter; `MonoGameSoundPlayer` stores each loaded variant's peak-derived normalisation multiplier at load and applies it together with the slot's voicing level, and passes the voicing pitch through to `SoundEffectInstance` instead of the hardcoded `0f`. The normalisation target is a reference peak of `0.85`, and the multiplier is clamped so a near-silent take cannot be amplified into noise. `SilentSoundPlayer` and every test double follow the interface change. | `src/Hukbo.Client/Audio/ISoundPlayer.cs`, `src/Hukbo.Client/Audio/MonoGameSoundPlayer.cs`, `src/Hukbo.Client/Audio/SoundDirector.cs`, `tests/Hukbo.Client.Tests/` test doubles and `SoundDirectorTests` | The Client suite is green with no warning suppressed and no test weakened. A test proves the director passes the slot's voicing pitch to the player and scales the gain by the slot's voicing level. The loudest clash cue the pipeline can now produce is below the loudest it could produce before. | SC-1, SC-2 | `./scripts/test.ps1 -Configuration Release` |
| SC-4 | Record what changed and why in the audio content documentation: that the sixteen clash takes were never level-matched, what the measured peaks were, and that the game now normalises them at load rather than the files being re-authored. | `src/Hukbo.Client/Content/Audio/README.md` | The note names the measured peaks and states plainly that no sound file was regenerated. | SC-3 | Reading it |
| SC-6 | Correct SC-3. Normalisation applied as a playback volume multiplier can only attenuate, because `SoundEffectInstance` takes a volume in `[0, 1]` and the quietest take on disk peaks at 0.096, so no multiplier can lift it. Normalise the 16-bit samples themselves at load for the four melee shield-clash slots and build the `SoundEffect` from the scaled buffer; every other slot loads byte-identical to today. | `src/Hukbo.Client/Audio/WavePeak.cs`, `src/Hukbo.Client/Audio/MonoGameSoundPlayer.cs`, `tests/Hukbo.Client.Tests/WavePeakTests.cs` | A test proves a quiet synthetic buffer is raised to the reference peak, a full-scale one is attenuated, the scale factor clamps at both ends, no sample can wrap around, and a non-clash slot's bytes are untouched. Exactly one normalisation stage exists. | SC-3 | `./scripts/test.ps1 -Configuration Release` |
| SC-5 | Reopen row 173 to `PENDING` keeping its `FAIL` observation, and add the two fresh rows the change owes: one that a shield block still reads as wood rather than as a landed cut, and one that a full 200-agent battle has not become a wall of clash noise now that the quiet takes are audible. | `docs/development/smoke-checklist.md` | Row 173 is `PENDING` with its 2026-08-13 observation intact; two new rows are `PENDING`; the file's own totals are recounted from its status column. | SC-3 | A person at an interactive desktop; no agent may flip any of the three |

## Verification criteria

1. `./scripts/verify.ps1` passes, and its seed-1 determinism workload reports
   the same state hash, event hash, and winner as before the change. A
   presentation-only change owes exactly that.
2. Both test suites are green — the Core suite and the GPU-independent Client
   suite — because a Client audio change has reddened the Core-adjacent suites
   before through shared test doubles.
3. Row 173 stays open. Nothing in this plan may close it. The change is a fix
   offered to the next interactive run, not evidence that the run will pass.

## What this plan deliberately does not do

It does not normalise any slot outside the four melee shield-clash slots, it
does not regenerate a single sound file, and it does not spend an ElevenLabs
credit. Regenerating the sixteen clash takes with consistent generation
parameters remains the better answer to the timbral half of row 173 and remains
unauthorised.
