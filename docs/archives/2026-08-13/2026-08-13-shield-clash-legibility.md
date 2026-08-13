# Shield-clash audio legibility — plan

**Archived: reference only.** This is a finished plan whose tasks all landed.
Never execute it, never treat it as a live task list, and never cite it as the
reason to make a change. The live contract for this project remains `CLAUDE.md`
and `docs/development/smoke-checklist.md`; nothing in this file overrides
either of those. Archived 2026-08-13, with every task from `SC-1` to `SC-6`
merged and smoke row 173 closed.

Implements `docs/archives/2026-08-13/2026-08-13-shield-clash-legibility-design.md`,
which was archived beside this file on 2026-08-13 and which no longer sits in
`docs/plans/`. Opened
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

## How this actually closed, 2026-08-13

Everything above is the plan as written. This section is what happened, and it
differs from the plan in one important place. Read it before trusting the task
table.

`SC-1`, `SC-2`, `SC-3`, `SC-4`, and `SC-6` landed as written, in that order, and
are merged. `SC-5` did not happen as written, and verification criterion 3 was
overtaken by events.

**Row 173 did not stay open.** The plan said it must, and said that nothing in
the plan could close it. What closed it was not the plan but the tester: after
the fix merged, the person at the interactive desktop listened again and closed
row 173 `PASS` on their own judgement, in their own words "sounds are ok anyway,
so no worries for now, let's pass the test for this", declining a regeneration
of the takes. That was their call to make and it stands. What it means for a
later reader is that row 173's `PASS` rests on a judgement that the sounds are
acceptable, not on a demonstration that the four slots read as four weapons; the
2026-08-13 archive record titled "Shield-clash audio smoke — closed 2026-08-13"
says so plainly in its section "How row 173 closed", and that is the section to
read before citing the pass.

**The two fresh rows `SC-5` owed were not added when the family closed.** They
were added later the same day, as `SCL-1` and `SCL-2`, in a subsection of
`docs/development/smoke-checklist.md` titled "Shield-clash loudness re-check
(2026-08-13)". They existed because rows 172 and 175 passed against the loudness
this change replaced, so under the checklist's own rule they were owed fresh
rows rather than a revival of the lifted ones. `SCL-1` asked whether a block
still reads as wood rather than as a landed cut; `SCL-2` asked whether a
200-agent battle had become a wall of clash noise now that the previously
inaudible takes are audible.

**Both of them passed the same day**, at an interactive desktop, and the
subsection was deleted whole. Their record is the 2026-08-13 archive titled
"Shield-clash loudness re-check smoke — closed 2026-08-13". That closes every
row this change owed. What it does not close is the question row 173 originally
asked — whether a listener can tell the four melee clash slots apart by ear —
because `SCL-1` and `SCL-2` were never asked it. Row 173's own `PASS` rests on
the tester's judgement that the sounds are acceptable, and regenerating the
sixteen takes with consistent generation parameters remains the better answer to
that half of it, and remains unauthorised.

**Two acceptance clauses had no test until they were chased.** `SC-3` required
that the loudest clash cue the pipeline can now produce be below the loudest it
could produce before, and `SC-6` required that a non-clash slot's bytes be left
untouched. Both were true in the code and neither was asserted anywhere.
`SoundCatalog.IsMeleeShieldClash`, the gate that decides which slots are
normalised at all, had no test of any kind. Two tests were added on 2026-08-13
to close both clauses: `LoudestClashCue_PlaysQuieterThanTheFullScaleTakeDidBefore`
pins the product `0.85 × 1.00 × 0.65 = 0.5525` against the old `0.65` ceiling,
and `IsMeleeShieldClash_IsTrueForExactlyFourOfTheWholeCatalog` enumerates all
twenty-six catalog slots and requires exactly four of them to be normalised.

**Verification criteria 1 and 2 were met, and late.** The gate was not run when
the work merged. It was run on 2026-08-13 against the merged result and is
recorded in `docs/development/testing.md` as "Canonical gate result — Hukbo,
2026-08-13 (shield-clash audio legibility)": exit code 0, both suites green at
2,503 and 3,783 tests, and all four seed-1 workloads byte-identical to the
baseline they had before this change. No hash moved, which is what a
presentation-only change owes.

**One cleanup this document caused.** Six doc comments in
`src/Hukbo.Client/Audio/` cited the design document by path. Archiving the
design would have left six paths into `docs/archives/`, which the repository
forbids because that folder is pruned periodically, so those comments now name
the design in prose instead. The design document is archived under the title
"Shield-clash audio legibility — design".
