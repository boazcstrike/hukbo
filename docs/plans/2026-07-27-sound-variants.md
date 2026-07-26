# Sound Variants — Plan

Date: 2026-07-27
Design: `2026-07-27-sound-variant-matrix-design.md`

## Ordered tasks

### Generator (done before fan-out, because everything else depends on it)

1. [x] `scripts/sfx.ps1` accepts `-Class` and `-Index` and writes
   `<slot>[-<class>][-NN].wav`.
2. [x] `-Class` is rejected for a non-attack slot, because death and outcome
   events carry no hit location.
3. [x] Rate-limit and server errors back off and retry instead of losing a take.
4. [x] The provenance log appends under a file lock, so concurrent runs cannot
   interleave rows.

### Client variant support

5. [x] Add `Hukbo.Client/Audio/HitClass.cs`: the six acoustic classes and the
   `BodyPart` to `HitClass` map, with every one of the thirteen parts covered.
6. [x] `SoundCatalog` gains the class tokens, variant file-name construction,
   and the fixed fallback order.
7. [x] `SoundLibrary` resolves an ordered list per slot and class from a supplied
   file-name list, still tolerating a missing folder.
8. [x] `SoundBinding` holds several loaded effects; `SoundCatalog.CountUnavailable`
   keeps working against the new shape.
9. [x] `SoundDirector` picks the class from the event's hit location and the
   index within that class by hashing tick and entity id.
10. [x] `SoundLogPanel` reports the per-class variant counts.
11. [x] Client tests for the part-to-class map, list resolution, the fallback
    chain, selection spread, and the partially populated case.

### Audio content

12. [x] 40 attack variants, ten per weapon slot, allocated by that weapon's
    targeting bias.
13. [x] 10 death variants.
14. [ ] Notification candidates for `victory-blue`, `victory-red`, `draw`, and
    `ui-click`; 12 generated. Audition, keep one each, delete the rest —
    **blocked on a human listening pass.**

### Close-out

15. [x] Run the canonical gate and record the exact result in
    `docs/development/testing.md`.
16. [x] Add the interactive smoke rows for variant playback as `PENDING`.

## Parallelism

Tasks 5 to 11 are one agent, not several. The files form a single dependency
chain — `SoundLibrary` cannot be written against a `HitClass` map that does not
exist yet — so splitting them across agents would have them guessing at each
other's API and colliding in the same files.

Tasks 12 to 14 are six agents, one per slot group. They share no files: each
writes only its own `.wav` names, and the one file they all append to is now
lock-guarded.

The two groups touch disjoint file sets, so they run at the same time.

## Verification criteria

- `./scripts/verify.ps1` passes.
- The determinism workload's state hash and event hash match the recorded seed-1
  baseline. Audio touches no Core code, so any movement is a bug in this change.
- With an empty `Content/Audio/`, the client still runs silently and every slot
  reports as missing.
- With only some classes present, resolution falls back rather than going silent.
- No test constructs `ArenaGame`, a `GraphicsDevice`, a `SpriteBatch`, a window,
  or an audio device.
- Every generated file is uncompressed PCM WAV and peaks above ten percent of
  full scale.

## Result

`./scripts/verify.ps1 -SkipBootstrap` passed every stage on 2026-07-27:
formatting, the Release build with 0 warnings, 156/156 Core tests, 505/505 Client
tests, and the seed-1 200-agent workload ending in `Faction1Victory` at tick 235
with `deterministic: true`.

State hash `6EBB1EA63114F6CE` and event hash `941377BD43C556FF` are unchanged
from the recorded baseline, which is the expected outcome: this change lives
entirely in `Hukbo.Client`.

63 WAV files are on disk, every one matching the loader's naming contract and
every one carrying a provenance row.

**Nothing here establishes that a single sound has been heard.** Smoke rows 48 to
52 in `docs/development/testing.md` are `PENDING` and only a human at an
interactive desktop may change that. Two known risks are specifically listed
there for the listening pass: possible human vocalisation in the death takes, and
four cues that measured close to the quiet-rejection floor.

Two defects in `scripts/sfx.ps1`, both introduced by the concurrency work in
tasks 1 to 4 and both found by the generation agents rather than by review:

- `Add-ProvenanceRow` declared a mandatory `[string[]]`, which rejects the blank
  separator lines in its own header. It threw after every successful write, so
  the audio was correct but unlogged and the script exited 1 on success.
- `[Math]::Abs` on an `Int16` of exactly `-32768` throws. This discarded the
  loudest possible takes as errors and cost roughly ten wasted API calls.

Both are fixed and were verified against the exact inputs that crashed. The
lesson recorded for next time: a dry run does not exercise the write path, and
these would have surfaced in one real generation.
