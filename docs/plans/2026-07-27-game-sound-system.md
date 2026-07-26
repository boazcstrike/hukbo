# Game Sound System — Plan

Date: 2026-07-27
Design: `2026-07-27-game-sound-system-design.md`

## Ordered tasks

1. [x] Add `Hukbo.Client/Audio/AudioTypes.cs`: `GameSoundId`, `SoundBindingStatus`,
   `SoundCueStatus`, `SoundBinding`, `SoundCue`.
2. [x] Add `Hukbo.Client/Audio/SoundCatalog.cs`: slot to canonical file name,
   ordered slot list, supported extension, missing-count helper.
3. [x] Add `Hukbo.Client/Audio/SoundLibrary.cs`: pure resolution of bindings from a
   supplied file-name list, plus a static directory-listing helper that tolerates
   a missing folder.
4. [x] Add `Hukbo.Client/Audio/SoundCueMapper.cs`: `BattleEvent` to slot.
5. [x] Add `Hukbo.Client/Audio/SoundCueBudget.cs`: per-slot and total caps per frame.
6. [x] Add `Hukbo.Client/Audio/SoundCueLog.cs`: bounded collapsing log with wheel
   scrolling.
7. [x] Add `Hukbo.Client/Audio/ISoundPlayer.cs` with `SilentSoundPlayer`.
8. [x] Add `Hukbo.Client/Audio/SoundDirector.cs`: mapper, budget, mute, log, player.
9. [x] Add `Hukbo.Client/Audio/MonoGameSoundPlayer.cs`: the only file that touches
   MonoGame audio types.
10. [x] Add `Hukbo.Client/UI/SoundLogPanel.cs` and
    `Hukbo.Client/UI/SoundLogPanel.Layout.cs` following the battle event log
    panel's pure-helper split.
11. [x] Wire the client: `ClientCommand.ToggleSoundLog`, the control-bar `Sounds`
    button, the `F9` shortcut, the split right-column layout, the pointer
    priority chain, the draw call, and the shortcut hint line.
12. [x] Add `Content/Audio/README.md` documenting every expected file name and the
    PCM WAV requirement, and copy `Content/Audio/**` to output in the csproj.
13. [x] Add client tests for the catalog, library, mapper, budget, log, director,
    and panel layout.
14. [x] Run the canonical gate and record the exact result in
    `docs/development/testing.md` under "2026-07-27 sound-system gate run". A
    first attempt failed in the Core test stage on an unfinished
    army-composition change in the same working tree; the rerun after that Core
    change compiled again passed all five stages.
15. [x] Add the interactive smoke rows for the sound panel as `PENDING`
    (rows 22 to 29).

## Verification criteria

- `./scripts/verify.ps1` passes: format verification, Release build with
  `TreatWarningsAsErrors`, Core and Client tests, and the 200-agent /
  10,000-tick / seed-1 determinism workload.
- The determinism workload's state hash and event hash match the recorded
  seed-1 baseline. Audio touches no Core code, so any movement here is a bug in
  this change.
- With `Content/Audio/` empty, the client runs silently, and the sound panel
  reports every slot as `Missing`.
- With the sound panel hidden — the default — the right-column layout is
  identical to the layout before this change.
- No test constructs `ArenaGame`, a `GraphicsDevice`, a `SpriteBatch`, a window,
  or an audio device.

## Result

`./scripts/verify.ps1 -SkipBootstrap` passed all five stages: formatting clean,
Release build with 0 warnings, 156/156 Core tests, 373/373 Client tests, and the
seed-1 200-agent 10,000-tick headless workload reporting state hash
`6EBB1EA63114F6CE`, event hash `941377BD43C556FF`, and `deterministic: true` —
both hashes unchanged from the recorded baseline, as required for a
presentation-only change. Recorded under "2026-07-27 sound-system gate run" in
`docs/development/testing.md`.

Interactive smoke rows 22 to 29 are `PENDING`; only a human at an interactive
desktop may flip them. Do not archive this plan until those rows are run.
