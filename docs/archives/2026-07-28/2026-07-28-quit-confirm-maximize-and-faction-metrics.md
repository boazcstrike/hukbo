# Quit confirmation, maximize replacement, and Core faction metrics — plan

**Archived: reference only.** Implemented and verified on 2026-07-28; the
canonical gate passed with `stateHash A080E28DA7C79C20` and `eventHash
2B6FB3A9A9C1960D`, both unchanged as this workstream required. Do not execute
this document and do not cite it as justification for a change. Smoke rows 156
to 171 in [docs/development/testing.md](../../development/testing.md) remain
PENDING and still need a human at an interactive desktop.

**Design:** [`2026-07-28-quit-confirm-maximize-and-faction-metrics-design.md`](2026-07-28-quit-confirm-maximize-and-faction-metrics-design.md).
Read it first. This document carries only the ordered task list and the
verification criteria.

**Date:** 2026-07-28. **Branch:** to be created from `main` at `22836b6`.

Three user-approved changes:

| Goal | Layer | Hash requirement |
| --- | --- | --- |
| A. Confirm before quitting | `Hukbo.Client` | Hash-neutral |
| B. Replace the OS maximize button | `Hukbo.Client` | Hash-neutral |
| C. Faction totals from Core metrics | `Hukbo.Core` | **Must move no hash at all** |

**Goal C has no rebaseline.** Every recorded state hash and event hash must come
back byte-identical. `A080E28DA7C79C20` / `2B6FB3A9A9C1960D` at 200 agents,
seed 1. A hash that moves means a metric leaked into authoritative state; fix the
leak, do not regenerate a golden.

## Tasks

### Goal C — Core faction metrics (do this first; it is the risk)

- [x] **C1.** Give the per-tick combat metrics a faction dimension. In
  `src/Hukbo.Core/Simulation/CombatMetrics.cs`, add per-faction counts alongside
  the existing undivided totals. Two factions only, so a fixed pair rather than a
  collection — the record must stay a struct with no heap allocation per tick.
  *Verification:* builds; C5 asserts the sums agree.

- [x] **C2.** Populate them in `BattleSimulation.GatherAndCommitAttacks`, where
  `_lastTickCombat` is assigned (`src/Hukbo.Core/Simulation/BattleSimulation.cs`,
  around line 1332). The attacker is already in scope as `source` and
  `AgentState.FactionId` is already available, so split the existing local
  counters by `source.FactionId` rather than adding any new lookup or query to
  the tick.
  *Verification:* C5, C6.

- [x] **C3.** Promote `BattleSimulation.LastTickCombat` from `internal` to
  `public`. Promote `LastTickCollision` only if the report actually reads it; if
  it does not, leave it `internal` and say so.
  *Verification:* the Client compiles against it without an
  `InternalsVisibleTo`.

- [x] **C4.** Check whether widening `CombatMetrics` breaks the headless
  `RunReport` JSON shape or any digest fixture under
  `tests/Hukbo.Core.Tests/Fixtures/`. The record is serialized into the report
  the gate prints. Fix what breaks; report what changed about the report shape.
  *Verification:* full Core suite green.

- [x] **C5.** New test: for a seeded run, each per-faction count summed across
  factions equals the corresponding undivided total, every tick. This is the
  invariant that makes the split trustworthy.
  *Verification:* new test passes.

- [x] **C6.** Confirm `CombatMetrics_ReachesNeitherHash` passes **unmodified**.
  It may not be edited. If it fails, a metric reached authoritative state.
  *Verification:* existing test passes with no diff.

- [x] **C7.** Confirm the recorded hashes are unchanged by running the canonical
  200-agent seed-1 workload and comparing against `A080E28DA7C79C20` /
  `2B6FB3A9A9C1960D`. **Any movement stops the work.**
  *Verification:* real captured output pasted.

- [x] **C8.** `BattleReportAccumulator` stops deriving faction totals from
  `Attack` events and sums Core's per-faction counts instead. Per-unit rows keep
  their existing client-side derivation. Update the accumulator's doc comment so
  the two classes of number are distinguished at the symbol.
  *Verification:* C9.

- [x] **C9.** Update `tests/Hukbo.Client.Tests/BattleReportAccumulatorTests.cs`:
  faction totals now come from injected Core metrics, not from synthesised
  events. Keep every existing per-unit test — those paths are unchanged.
  *Verification:* Client suite green.

- [x] **C10.** `BattleReportPanel` must not present a derived per-unit figure as
  carrying the same authority as a Core faction total. Label the faction totals
  section so the distinction is visible without reading source.
  *Verification:* layout test plus manual smoke.

### Goal A — quit confirmation

- [x] **A1.** New `src/Hukbo.Client/UI/ConfirmationPrompt.cs`. Parameterised by
  message and the command issued on confirm, so it is reusable rather than
  quit-specific. Layout in a pure static helper following the established
  pattern — no `GraphicsDevice` in the geometry.
  *Verification:* A5.

- [x] **A2.** `ClientCommand`: append `RequestExit` (shows the prompt) and keep
  `Exit` as the action that actually quits. Append, never insert, so no existing
  ordinal moves.
  *Verification:* builds.

- [x] **A3.** Re-point both in-application quit paths at the prompt: the control
  bar's `Close` button and `MenuOverlay`'s `Exit Game` button now issue
  `RequestExit`. Only the prompt's confirm issues `Exit`.
  *Verification:* A5, plus manual smoke that neither path quits directly.

- [x] **A4.** Wire it in `ArenaGame`. The prompt takes the **top** of the pointer
  priority chain, above the menu overlay — the menu check currently sits around
  line 475, so the prompt is tested before it. While open it consumes every
  click, so a miss cannot fall through to the control bar, the arena, or agent
  selection; and it owns `Escape`, which must not also close the menu behind it.
  `Enter` confirms, `Escape` cancels, **cancel has default focus**.
  *Verification:* A5, plus manual smoke.

- [x] **A5.** New `tests/Hukbo.Client.Tests/ConfirmationPromptTests.cs`: layout
  containment, confirm issues the carried command, cancel issues none, cancel is
  the initially focused control.
  *Verification:* all pass.

### Goal B — maximize replacement

- [x] **B1.** `ClientCommand`: append `ToggleMaximize`.
  *Verification:* builds.

- [x] **B2.** `ArenaGame`: add `SDL_MaximizeWindow`, `SDL_RestoreWindow`, and
  `SDL_GetWindowFlags` via `[LibraryImport("SDL2")]` on `private static partial`
  methods, alongside the existing `SDL_MinimizeWindow`. **Never `[DllImport]`** —
  SYSLIB1054 is an error here and suppressing it is forbidden.
  *Verification:* builds with zero warnings.

- [x] **B3.** Handle `ToggleMaximize` by testing `SDL_GetWindowFlags` against
  `SDL_WINDOW_MAXIMIZED` (`0x00000080`) and calling maximize or restore
  accordingly. **Do not track a local boolean** — the user can maximize or
  restore outside the application and a tracked flag would invert the button.
  *Verification:* manual smoke.

- [x] **B4.** `ControlBar`: append a `Max` button and set `BarWidth` to **660**.
  Arithmetic: seven buttons at `ButtonWidth` 84 is 588, six gaps at `ButtonGap` 8
  is 48, first button at `Bounds.Left + 10`, 14 pixels of right padding —
  `10 + 636 + 14 = 660`. Every other metric unchanged.
  *Verification:* B5.

- [x] **B5.** Extend `tests/Hukbo.Client.Tests/ControlBarTests.cs`: all seven
  buttons' bounds lie inside the bar and none is clipped. This must fail at the
  old 568 width.
  *Verification:* fails at 568, passes at 660; report both observations.

## Verification criteria

**The canonical gate is not delegated.** `./scripts/verify.ps1` runs once after
integration and its real pasted output is the evidence.

- **Goal C moves no hash.** `A080E28DA7C79C20` / `2B6FB3A9A9C1960D` at 200
  agents, seed 1, byte-identical. `CombatMetrics_ReachesNeitherHash` passes
  unmodified. The SplitMix64 vectors and the three combat preset content hashes
  are untouched.
- **No preset version is cut** and no golden expectation is regenerated.
- **No analyzer suppression** anywhere in the diff: no `#pragma warning`, no
  `SuppressMessage`, no `NoWarn`.
- **Warm-tick allocation does not regress.** The widened metrics struct must not
  introduce a per-tick heap allocation; `coreAllocatedBytes` per agent per tick
  is the evidence.
- Core and Client suites fully green, zero warnings.

**Manual smoke rows 156 to 171 in `docs/development/testing.md`, all PENDING.** No
agent may flip one to `PASS`. The maximize and restore rows in particular cannot
be proven by a build: like `SDL_MinimizeWindow` before them, these are P/Invokes
that compile cleanly and have never executed.

| Step | Expected |
| --- | --- |
| Click `Close` | A confirmation prompt appears. The game does not quit. |
| Cancel the prompt | The prompt closes and the battle continues untouched. |
| Confirm the prompt | The game exits. |
| Press `Escape` with the prompt open | The prompt cancels, and the menu behind it does not also close. |
| Press `Enter` with the prompt open | Cancel is focused by default, so `Enter` cancels rather than quits. |
| Menu, then `Exit Game` | The same prompt appears; the menu path does not quit directly. |
| Press Alt+F4 | Quits immediately without a prompt, by design. |
| Click `Max` | The window maximizes. |
| Click `Max` again | The window restores to its previous size. |
| Maximize outside the app, then click `Max` | The button restores rather than re-maximizing — it read the real window state. |
| Check all seven control-bar buttons | All render fully inside the bar; none clipped at the right edge. |
| Open the battle report | Faction totals are visibly distinguished from derived per-unit rows. |
