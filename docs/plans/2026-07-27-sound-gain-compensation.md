# Sound Gain Compensation — Implementation Plan

Date: 2026-07-27
Design: [`2026-07-27-sound-gain-compensation-design.md`](2026-07-27-sound-gain-compensation-design.md)
Evidence: [`docs/research/SOUND-CAPACITY-MEASUREMENTS.md`](../research/SOUND-CAPACITY-MEASUREMENTS.md)

**Goal:** Stop the client overloading its own audio output, and play every cue
the battle emits, without touching `Hukbo.Core` or either hash.

**Architecture:** All new state is presentation state in `Hukbo.Client`. The
sound director gains a voice ledger and applies a gain correction derived from
it. `MonoGameSoundPlayer` stays the only file that references MonoGame audio.

If this plan and the design appear to disagree, stop the affected task and
resolve it in the design first.

## Task list

| # | Task | Files | Depends on | Done when |
| --- | --- | --- | --- | --- |
| T1 | Add `SoundVoiceLedger`: records a cue's end time, expires by presentation seconds, reports the live count, clears | `Audio/SoundVoiceLedger.cs` | — | Type exists, is pure, and is constructible with no MonoGame type |
| T2 | Tests for T1 — expiry boundaries, count, clear, ceiling | `tests/.../SoundVoiceLedgerTests.cs` | T1 | Tests fail before T1's logic, pass after |
| T3 | Extend `ISoundPlayer`: `Play` returns `bool`; add a clip-duration query | `Audio/ISoundPlayer.cs`, `Audio/MonoGameSoundPlayer.cs`, `SilentSoundPlayer` | — | Both implementations compile; `MonoGameSoundPlayer` returns the real `SoundEffect.Play` result and the real `SoundEffect.Duration` |
| T4 | Add `SoundCueStatus.Refused` and its formatter and label | `Audio/AudioTypes.cs`, `Audio/SoundCueFormatter.cs` | — | New status renders like the existing ones |
| T5 | Wire the ledger and the gain correction into `SoundDirector`; `BeginFrame` takes elapsed seconds; record `Refused` | `Audio/SoundDirector.cs` | T1, T3, T4 | Director applies `CueVolume / sqrt(voices + 1)` and logs refusals |
| T6 | Raise `SoundCueBudget` to 16 per slot and 64 total; update the type's own documentation to say it is a backstop, not a throttle | `Audio/SoundCueBudget.cs` | — | Constants changed, comment no longer claims to prevent noise |
| T7 | Update `ArenaGame` to pass the frame's elapsed seconds to `BeginFrame` | `ArenaGame.cs` | T5 | Compiles; the value comes from `GameTime`, not a new clock |
| T8 | Show `VOICES n` and `GAIN 0.nn` in the sound panel header | `UI/SoundLogPanel*.cs` | T5 | Pure layout helpers, no `GraphicsDevice` in tests |
| T9 | Tests for T5, T6, T8 — gain falls with load and recovers, refused cues log correctly, new caps, header rendering | `tests/Hukbo.Client.Tests/...` | T5, T6, T8 | All fail before, pass after |
| T10 | Re-render before and after with the mix harness; record peak dBFS and flattened-sample counts in the evidence document | `docs/research/SOUND-CAPACITY-MEASUREMENTS.md`, `tools/` | T5–T9 | Post-change peak is at or below 0.0 dBFS at 200 and 500 agents, 1x and 4x |
| T11 | Run the canonical gate and paste the actual output into `docs/development/testing.md` | `docs/development/testing.md` | T10 | Five `[PASS]` stages, both hashes unchanged from `D379B60B2E30FFFC` / `5BEBA7A68F69BE0D` |
| T12 | Add the new smoke rows for the audible change, left `PENDING` | `docs/development/testing.md` | T11 | Rows exist and are `PENDING`; no row is flipped by an agent |
| T13 | Review the complete diff | — | T12 | No `Hukbo.Core` change, no hash change, no new dependency |

## Verification criteria

The change is complete only when all of the following hold.

1. `./scripts/verify.ps1 -SkipBootstrap` passes all five stages, with the actual
   output pasted into `docs/development/testing.md`.
2. `eventHash` and `stateHash` for the 200-agent, seed-1, 10 000-tick workload
   are **unchanged**: `D379B60B2E30FFFC` and `5BEBA7A68F69BE0D`. A moved hash
   means something reached `Hukbo.Core` and the change is wrong.
3. A 500-agent run is reported.
4. `tools/Hukbo.Tools.MixAnalysis` reports peak at or below 0.0 dBFS for 200 and
   500 agents at 1x and 4x, against the recorded pre-change peaks of +7.7 to
   +12.9 dBFS.
5. Measured cue suppression is zero at both agent counts and every speed.
6. No test, no headless run, and no gate stage opens an audio device.
7. This work introduces no settings-schema change: `ClientSettings` stays at
   whatever schema version it already carried when the work started, which is
   version 2 (`ClientSettingsStore.SupportedSchemaVersion`). Nothing under
   `src/Hukbo.Client/Settings/` is modified, and no migration is added.

## Explicitly out of scope

Carried from the design so it cannot drift in during implementation:

- Clip length, weight, and cue density. The owner's separate observation that
  the attack clips are short — several under 100 ms, shortest 49 ms — and fire
  roughly every 25 ms is a real, measured, **content and pacing** problem. It
  needs its own design. Nothing in this plan addresses it and nothing in this
  plan should pretend to.
- The seven clips that are exactly 480 ms and look generation-capped.
- An output bus limiter.
- Anything in `Hukbo.Core`, `Hukbo.Headless`, or `ClientSettings`.

## Archiving

When the work is integrated **and** the human smoke run for the new rows is
complete, move this plan and its design document to `docs/archives/` with the
"Archived: reference only" banner. Not before: every smoke row in
`docs/development/testing.md` is currently `PENDING`, and only a person at an
interactive desktop may change that.
