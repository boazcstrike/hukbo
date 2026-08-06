# UI-52 — Secondary feedback: plan

Date: 2026-08-07
Design: [`2026-08-07-ui-52-secondary-feedback-design.md`](2026-08-07-ui-52-secondary-feedback-design.md)

Read the design document before executing any task here. It records the
decisions this list assumes: that every UI-52 surface is colour and opacity only
at every motion intensity, that `UiButtonMotion` gains a fourth channel rather
than a new helper being invented for it, that the five selector classes are not
refactored into one, and that the five duration constants live together in a
single class so their published bands can be asserted in one test.

Working directory for every task:
`C:/Users/boazs/webdev/autonomous-arena/.claude/worktrees/ui-52-secondary-feedback`

## Ordered task list

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| T0 | Shared seam. Add `UiTransition.Restart(float value)` delegating to the existing private `SnapTo`. Add the `UiEmphasisPulse` struct (`Trigger`, `Advance`, `Reset`, `Amount`, `IsSettled`) wrapping one `UiTransition`, where `Trigger` calls `Restart(1f)` only when motion is enabled and `Advance` always targets `0f`. Add `internal static class UiSecondaryMotion` holding the five duration constants and `IsEnabled(MotionIntensity)` with undefined-value normalization. | `src/Hukbo.Client/UI/UiTransition.cs`; `src/Hukbo.Client/UI/UiEmphasisPulse.cs` (new); `src/Hukbo.Client/UI/UiSecondaryMotion.cs` (new); `tests/Hukbo.Client.Tests/UiTransitionTests.cs`; `tests/Hukbo.Client.Tests/UiEmphasisPulseTests.cs` (new); `tests/Hukbo.Client.Tests/UiSecondaryMotionTests.cs` (new) | `Restart` snaps value, start, target and elapsed together; `Restart(1f)` then `AdvanceTo(0f)` decays and settles exactly at `0f`; a pulse triggered at `MotionIntensity.Off` never shows a non-zero amount; every one of the five constants is asserted inside its published band; every pre-existing `UiTransitionTests` assertion still passes unmodified | — | `./scripts/test.ps1 -Configuration Release`; `./scripts/format.ps1 -Verify` |
| T1 | Surface D — control-bar active strip (120 ms). Add a fourth `UiTransition` channel to `UiButtonMotion` reading `UiSecondaryMotion.ActiveStripDuration`, expose `ActiveAmount`, and pass `IsActive` into `Advance` from `UiButton.Update`. In `UiButton.DrawBackgroundAndBorder`, lerp the six-pixel strip colour from `PanelBorder` to `Selection` by `ActiveAmount`, and widen the draw condition from `IsActive && IsEnabled` to "enabled, and either active or not yet settled" so deactivation fades instead of snapping. Strip width stays fixed at six pixels. | `src/Hukbo.Client/UI/UiButtonMotion.cs`; `src/Hukbo.Client/UI/UiButton.cs`; `tests/Hukbo.Client.Tests/UiButtonTests.cs` | `ActiveAmount` rises toward 1 while active and falls to exactly 0 while inactive; at `MotionIntensity.Off` it snaps in a single call; the strip colour helper returns `PanelBorder` at amount 0 and `Selection` at amount 1; `Bounds` and `GetVisualBounds` are unchanged for every existing case; `ControlBarTests` passes untouched | T0 | `./scripts/test.ps1 -Configuration Release`; `./scripts/format.ps1 -Verify` |
| T2 | Surface A — new-event row emphasis (200 ms). Add a motion-aware `BattleEventLogPanel.Update(InputEdges, BattleEventFeed, Rectangle, TimeSpan, MotionIntensity)` overload; keep the existing three-argument overload delegating with `TimeSpan.Zero, MotionIntensity.Off`. Track the highest `Sequence` among the already-computed `visibleEntries`, trigger a `UiEmphasisPulse` when it increases, and record the previous highest as an exclusive threshold. Add pure `internal static float GetRowEmphasis(long sequence, long? emphasisThreshold, float pulseAmount)`. In `DrawRow`, blend row text colour from `TextPrimary` toward `NewEvent` by that amount. Do not touch row fill, row height, row order, or `BattleEventFeed`. | `src/Hukbo.Client/UI/BattleEventLogPanel.cs`; `src/Hukbo.Client/UI/BattleEventLogPanel.List.cs`; `tests/Hukbo.Client.Tests/BattleEventLogPanelTests.cs` | `GetRowEmphasis` returns 0 at or below the threshold and the pulse amount above it, and 0 when the threshold is null; the threshold advances only when the highest visible sequence advances; a frame with no new events leaves the pulse at exactly 0; `src/Hukbo.Client/Presentation/BattleEventFeed.cs` is byte-identical; every pre-existing `BattleEventLogPanelTests` assertion passes unmodified | T0 | `./scripts/test.ps1 -Configuration Release`; `./scripts/format.ps1 -Verify` |
| T3 | Surface B — selected-agent accent (160 ms). Add a motion-aware `AgentInspectorPanel.Update(InputEdges, AgentView?, Rectangle, TimeSpan, MotionIntensity)` overload; keep the existing three-argument overload delegating with `TimeSpan.Zero, MotionIntensity.Off`. Track the selected `EntityId`, trigger a `UiEmphasisPulse` when it changes (including null to value), and reset it when the selection clears. Add pure `internal static Color GetAccentColor(Color factionColor, Color selectionColor, float pulseAmount)` and use it for the existing left accent rectangle in `Draw`. The accent rectangle's geometry and every text origin stay exactly as they are. | `src/Hukbo.Client/UI/AgentInspectorPanel.cs`; `tests/Hukbo.Client.Tests/AgentInspectorAccentMotionTests.cs` (new) | `GetAccentColor` returns the faction colour at amount 0 and the selection colour at amount 1; selecting a different agent triggers exactly one pulse; re-observing the same agent triggers none; deselection returns the amount to exactly 0; `AgentInspectorContentTests` and `AgentSelectionTests` pass untouched | T0 | `./scripts/test.ps1 -Configuration Release`; `./scripts/format.ps1 -Verify` |
| T4 | Surface C part 1 — the shared selector motion type, new files only. Add `internal sealed class UiSelectorMotion` with two arrow-hover `UiTransition` channels, one `UiEmphasisPulse` for the marker, an `AdvanceMotion(Point pointer, Rectangle previousBounds, Rectangle nextBounds, string markerText, TimeSpan elapsed, MotionIntensity intensity)` method that triggers the pulse when `markerText` differs from the last observed value, and pure `PreviousArrowColor` / `NextArrowColor` / `MarkerColor` resolvers over `UiThemeColors`. Durations come from `UiSecondaryMotion.SelectorMarkerDuration`. Touch no existing file. | `src/Hukbo.Client/UI/UiSelectorMotion.cs` (new); `tests/Hukbo.Client.Tests/UiSelectorMotionTests.cs` (new) | Arrow amounts rise on pointer-inside and settle exactly at 0 and 1; a marker-string change triggers exactly one pulse and an unchanged string triggers none; the first observation of a marker string does not pulse; arrow colours resolve from `TextSecondary` at 0 and `TextPrimary` at 1; marker colour resolves from `Selection` at 0 toward `ActionFocus` at 1; no existing file is modified | T0 | `./scripts/test.ps1 -Configuration Release`; `./scripts/format.ps1 -Verify` |
| T5 | Surface C part 2 — wire the five selector classes. Give each one a `private readonly UiSelectorMotion _motion = new();`, a public `AdvanceMotion(InputEdges input, TimeSpan elapsed, MotionIntensity intensity, <current value>)` method that forwards `PreviousBounds`, `NextBounds` and `GetSelectedMarkerText(current)`, and three colour reads in `Draw` replacing the literal `TextPrimary` arrow colours and the literal `Selection` marker colour. Do **not** change any `Update` signature. In `MenuOverlay.Update`, call `AdvanceMotion` on all six selector instances in one pass immediately after `_entrance.Advance` and **before** the early-returning interaction chain. In `UiThemeSelector` change only the two arrow reads and the marker read — the `colors.Selection` at the end of the `swatches` array is a theme-preview swatch and must stay literal. | `src/Hukbo.Client/UI/UiThemeSelector.cs`; `src/Hukbo.Client/UI/GoreIntensitySelector.cs`; `src/Hukbo.Client/UI/MotionIntensitySelector.cs`; `src/Hukbo.Client/UI/AutoCameraModeSelector.cs`; `src/Hukbo.Client/UI/SettingsChoiceSelector.cs`; `src/Hukbo.Client/MenuOverlay.cs` | All six instances advance on every visible-menu frame regardless of which selector reports a selection; no `Update` signature changed; `PreviousBounds`, `NextBounds`, `GetPrevious`, `GetNext`, `GetIndex`, `GetPositionText` and `GetSelectedMarkerText` are behaviourally unchanged; `UiThemeSelectorTests`, `GoreIntensitySelectorTests`, `MotionIntensitySelectorTests`, `AutoCameraModeSelectorTests`, `SettingsChoiceSelectorTests`, `MenuOverlayFocusTests` and `MenuOverlayArmyCompositionTests` all pass untouched | T4 | `./scripts/test.ps1 -Configuration Release`; `./scripts/format.ps1 -Verify` |
| T6 | Surface E part 1 — the status badge helper, new files only. Add `internal sealed class UiStatusBadgeMotion` with `Observe(BattleOutcome outcome, bool isPlaying, TimeSpan elapsed, MotionIntensity intensity)`, one `UiEmphasisPulse` on `UiSecondaryMotion.StatusBadgeDuration`, an `Amount` property, and pure `internal static Color GetBarColor(Color statusSurface, Color statusInfo, float pulseAmount)`. Trigger only on a changed `BattleOutcome` (`Hukbo.Core.Simulation.BattleOutcome`) or a changed playing flag. The first observation seeds the recorded state without pulsing. Touch no existing file. | `src/Hukbo.Client/UI/UiStatusBadgeMotion.cs` (new); `tests/Hukbo.Client.Tests/UiStatusBadgeMotionTests.cs` (new) | An outcome change triggers; a playing/paused change triggers; repeated identical observations never trigger; the first observation never triggers; after a trigger, roughly five seconds of simulated 16 ms frames with no change leave the amount at exactly 0 and it never rises again (the non-looping proof); at `MotionIntensity.Off` the amount is always 0; no existing file is modified | T0 | `./scripts/test.ps1 -Configuration Release`; `./scripts/format.ps1 -Verify` |
| T7 | Integration — the single owner of `ArenaGame`. Switch the `_eventLogPanel.Update` call site to the motion-aware overload and the `_inspectorPanel.Update` call site to the motion-aware overload, both passing `gameTime.ElapsedGameTime` and `_motionManager.Value`. Add a `UiStatusBadgeMotion` field, call `Observe` once per frame in `Update` with `_simulation.Outcome`, `_presentation.Playback.IsPlaying`, the elapsed time and the intensity. In `DrawStatus`, replace the bare `theme.Colors.StatusSurface` fill with `UiStatusBadgeMotion.GetBarColor(...)`. Change no rectangle, no text origin, and not `CalculateStatusTextBounds`. | `src/Hukbo.Client/ArenaGame.cs`; `src/Hukbo.Client/ArenaGame.Rendering.cs` | Both panel call sites pass real elapsed time and the live intensity; `Observe` is called exactly once per frame and on every frame, including frames where the pointer was consumed elsewhere; `DrawStatus` fills with the blended colour; `CalculateStatusTextBounds`, `ClipStatusLine` and `BuildStatusLine` are behaviourally unchanged; `ArenaGameResponsiveChromeTests` and `SourceHygieneTests` pass untouched | T2, T3, T6 | `./scripts/test.ps1 -Configuration Release`; `./scripts/format.ps1 -Verify` |
| T8 | Manual smoke rows. Append five rows, UI-12 through UI-16, to the existing "Responsive menu, startup display, and UI motion smoke" table in `docs/development/testing.md`, one per surface, each with `Not run` / `PENDING`. Do not touch UI-1 through UI-11 and do not flip any existing row. | `docs/development/testing.md` | Five new rows exist, all `PENDING`, each naming the surface, the action that triggers it, and the expected observation at Off, Reduced and Full; no existing row's status changed | T0 | Row-count and status inspection of the edited table |
| T9 | Canonical gate. Run `./scripts/verify.ps1` once on the integrated branch and paste its real output into the plan's results section. Not delegated to any agent. | none | The gate's actual output is recorded verbatim, including the headless determinism result for the 200-agent / 10,000-tick / seed-1 workload | T1, T5, T7, T8 | `./scripts/verify.ps1` |

## Execution order

1. **T0 alone.** Nothing else starts until the shared seam exists.
2. **T1, T2, T3, T4, T6 in parallel.** Five implementers, fully disjoint file
   sets, all depending only on T0.
3. **T5 and T7 in parallel.** T5 needs T4; T7 needs T2, T3 and T6. They share no
   file.
4. **T8** may run at any point after T0; it touches only `docs/development/testing.md`.
5. **T9 after everything**, run once, by hand, not by an agent.

Every implementation agent is dispatched on Sonnet, per `CLAUDE.md` §10.

## File-ownership map

Every path below is owned by exactly one task. No path appears twice.

| Task | Files owned |
| --- | --- |
| T0 | `src/Hukbo.Client/UI/UiTransition.cs`<br>`src/Hukbo.Client/UI/UiEmphasisPulse.cs` (new)<br>`src/Hukbo.Client/UI/UiSecondaryMotion.cs` (new)<br>`tests/Hukbo.Client.Tests/UiTransitionTests.cs`<br>`tests/Hukbo.Client.Tests/UiEmphasisPulseTests.cs` (new)<br>`tests/Hukbo.Client.Tests/UiSecondaryMotionTests.cs` (new) |
| T1 | `src/Hukbo.Client/UI/UiButtonMotion.cs`<br>`src/Hukbo.Client/UI/UiButton.cs`<br>`tests/Hukbo.Client.Tests/UiButtonTests.cs` |
| T2 | `src/Hukbo.Client/UI/BattleEventLogPanel.cs`<br>`src/Hukbo.Client/UI/BattleEventLogPanel.List.cs`<br>`tests/Hukbo.Client.Tests/BattleEventLogPanelTests.cs` |
| T3 | `src/Hukbo.Client/UI/AgentInspectorPanel.cs`<br>`tests/Hukbo.Client.Tests/AgentInspectorAccentMotionTests.cs` (new) |
| T4 | `src/Hukbo.Client/UI/UiSelectorMotion.cs` (new)<br>`tests/Hukbo.Client.Tests/UiSelectorMotionTests.cs` (new) |
| T5 | `src/Hukbo.Client/UI/UiThemeSelector.cs`<br>`src/Hukbo.Client/UI/GoreIntensitySelector.cs`<br>`src/Hukbo.Client/UI/MotionIntensitySelector.cs`<br>`src/Hukbo.Client/UI/AutoCameraModeSelector.cs`<br>`src/Hukbo.Client/UI/SettingsChoiceSelector.cs`<br>`src/Hukbo.Client/MenuOverlay.cs` |
| T6 | `src/Hukbo.Client/UI/UiStatusBadgeMotion.cs` (new)<br>`tests/Hukbo.Client.Tests/UiStatusBadgeMotionTests.cs` (new) |
| T7 | `src/Hukbo.Client/ArenaGame.cs`<br>`src/Hukbo.Client/ArenaGame.Rendering.cs` |
| T8 | `docs/development/testing.md` |
| T9 | none |

Disjointness notes worth checking before dispatch:

- `src/Hukbo.Client/ArenaGame.cs` and `src/Hukbo.Client/ArenaGame.Rendering.cs`
  are held by **T7 only**. Surfaces A, B and E all need an `ArenaGame` edit, and
  gathering all three into one task is the reason T2, T3 and T6 can safely run in
  parallel.
- `src/Hukbo.Client/MenuOverlay.cs` is held by **T5 only**.
- `src/Hukbo.Client/UI/UiButton.cs` is held by **T1 only**, even though
  `ControlBar` and `MenuOverlay` both consume it. Neither consumer file is edited
  by T1.
- `src/Hukbo.Client/UI/ControlBar.cs` is edited by **no task**. It already
  forwards elapsed time and intensity to every button.
- `src/Hukbo.Client/Presentation/BattleEventFeed.cs` is edited by **no task**.
  T2 reads the highest sequence from the `visibleEntries` list the panel already
  computes.
- `src/Hukbo.Client/Content/Themes/ui-theme-standards.json` is edited by **no
  task**. Every colour used is an existing role.
- No `.csproj` change is needed. `tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj`
  has no explicit `Compile` items, so new test files are picked up by the SDK
  glob.
- Nothing under `src/Hukbo.Core`, `src/Hukbo.Headless`, `src/Hukbo.Diagnostics`,
  or `tests/Hukbo.Core.Tests` is touched by any task.

## Per-task prompt requirements

Every implementation prompt must carry, in addition to its row above:

- The worktree path, and the instruction that files on disk win over any summary.
- The design document path, and the instruction to read §3 (constraints) and §3.1
  (colour-and-opacity-only decision) before writing code.
- The exact file list from the ownership map, with "do not create, edit, or delete
  any file outside this list" stated as a hard rule.
- The rule that no test, warning, or analyzer may be weakened; `TreatWarningsAsErrors`
  is on repo-wide with nullable enabled.
- The rule that no test may construct `ArenaGame`, a `GraphicsDevice`, a
  `SpriteBatch`, or a window.
- The return format: the diff summary per file, the new public or internal
  members added, the test names added, and the actual output of
  `./scripts/test.ps1 -Configuration Release` and `./scripts/format.ps1 -Verify`.
- Caveman compression, per `CLAUDE.md` §10. The design document, this plan, and
  every repository file stay in full English.

## Results

Run on 2026-08-07 from the `ui-52-secondary-feedback` worktree, branched from
`main` at `b144b7d`. The gate was run by the orchestrating session, not by any
implementation agent.

Command:

```powershell
./scripts/verify.ps1
```

Result:

```text
[PASS] Formatting verification completed.
    0 Warning(s)
    0 Error(s)
[PASS] Release solution build completed.
Total tests: 2614
     Passed: 2614
Total tests: 3049
     Passed: 3049
[PASS] Release repository tests completed.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

The deterministic headless result was:

```text
outcome: Faction1Victory
eventHash: AC55684F24D39344
stateHash: 1B73FC5923879AA0
deterministic: true
firstMismatchTick: null
```

All three values are identical to the baseline recorded in
[docs/plans/UI/implementation-report.md](UI/implementation-report.md), which is
the evidence that this work remained client-presentation-only: no simulation
state, event stream, or outcome moved.

The Client test count rose from 2944 to 3049 as the new motion tests landed.

### Corrections made during integration

Four defects were found by the build and the test run after the implementation
agents reported, and were fixed by the orchestrating session:

- `UiStatusBadgeMotion.Observe` dereferenced `_lastIsPlaying.Value` under a
  guard the compiler could not connect to it (CS8629). Replaced with a lifted
  nullable comparison, which preserves the first-observation-does-not-pulse
  semantics exactly.
- `UiEmphasisPulseTests` asserted that a freshly triggered pulse is unsettled.
  It is not: `Restart(1f)` snaps value and target together, so the decay is in
  flight only after the first `Advance`. The assertion was moved after that
  call rather than changing the primitive.
- `SettingsChoiceSelector` and `UiThemeSelector` gained `MotionIntensity`
  parameters without the `Hukbo.Client.Settings` using directive.
- `ArmyCompositionArrowMotionTests` referenced `ArmyComposition` ambiguously.
  Two distinct types carry that name — the `UI` record struct the panel takes
  and the `Settings` record — so the test now qualifies the UI one.

### Manual verification

No interactive row was flipped. Rows UI-12 through UI-16 in
[docs/development/testing.md](../development/testing.md) are recorded `Not run`
/ `PENDING`. The canonical gate, the test suite, and the unchanged determinism
hashes prove the code is correct and inert with respect to the simulation; they
do not prove that any of the five effects looks right on screen. Only a person
at an interactive Windows desktop can make that call.
