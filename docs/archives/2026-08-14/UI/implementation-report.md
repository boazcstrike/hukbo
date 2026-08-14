# UI and UX Completion Report

**Status:** Implementation and automated verification complete

**Date:** 2026-07-31

**Archived: reference only.** This is the implementation record of the finished
2026-07-31 UI and UX package. Never execute it, never treat it as a live task
list, and never cite it as the reason to make a change. The live contract for
this project remains `CLAUDE.md` and `docs/development/testing.md`; the recorded
baselines and gate results live in the latter, not here. Archived 2026-08-14.

**Branch:** `codex/ui-ux-completion`

## Implemented

- Added persisted `UiScale` and `StartupDisplayMode` settings with schema-v7
  migration into schema v8. Existing preferences and stable theme IDs survive
  migration.
- Added startup-only 1280x720 windowed and soft-fullscreen policies. Windowed
  mode has a 1024x720 minimum client area; render probes remain explicitly
  windowed and resolution-stable.
- Added 100%, 125%, 150%, and 200% pre-baked font tiers. `Auto` resolves from
  the viewport, and explicit preferences are capped to the largest tier that
  safely fits.
- Reworked the menu into a contained two-column layout and scaled the HUD,
  selectors, logs, reports, inspector, confirmation, summary, and army
  composition chrome through the active UI scale.
- Added viewport diagnostics at startup and on client-size changes.
- Added bounded client-only button transitions. `Off` snaps, `Reduced` keeps
  color feedback without positional motion, and `Full` permits a one-pixel
  decorative press inset without moving hit targets.
- Added opacity-only entrance transitions for menu and prompt scrims/panels,
  match summaries, and battle reports. `Off` snaps; `Reduced` and `Full`
  preserve final layout and hit targets while the decoration fades in.
- Added the sixth `datu-court` theme. Its compact name is
  `Cebu 1521 — Provisional` and its adjacent selector label reads
  `PROVISIONAL RECONSTRUCTION`, so the new-user default is never presented as
  a bare recovered historical style. All existing stable theme IDs remain
  preserved.
  Historical status: **Provisional reconstruction**. Its regional/date scope
  and evidence limits remain documented in
  [historical-royal-visual-direction.md](historical-royal-visual-direction.md).

## Automated verification

### Focused client suite

Command:

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release --no-restore --nologo
```

Result:

```text
Passed: 2944
Failed: 0
Skipped: 0
```

### Canonical repository gate

Command:

```powershell
./scripts/verify.ps1
```

Final result:

```text
[PASS] Locked package restore completed.
[PASS] Formatting verification completed.
Build succeeded.
    0 Warning(s)
    0 Error(s)
[PASS] Release solution build completed.
Total tests: 2944
     Passed: 2944
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

### Real-window render smoke

Command:

```powershell
dotnet run --project tools/Hukbo.Tools.RenderProbe -c Release -- 200 1 30 artifacts/ui-ux-render-probe-2026-07-31.json
```

Result: the real client created its window and captured all three camera
stations through the `spritebatch-1x1` backend. All 90 sampled frames completed
without an exception; the generated local report is
`artifacts/ui-ux-render-probe-2026-07-31.json`. This is an automated runtime
smoke result, not a human judgement of visual quality.

The first gate attempt stopped at two formatting findings. Those exact
whitespace/import-order findings were corrected before the successful full
rerun above.

## Independent review

The first read-only review found no Critical issues and identified three High
and four Medium findings. Before the final gate:

- the historical default gained a compact provisional name plus an explicit
  `PROVISIONAL RECONSTRUCTION` marker;
- menu, prompt, summary, and report entrance fades completed the priority
  motion scope;
- status text gained a tested reserved region before the control bar;
- Shift+Tab gained backward traversal;
- viewport diagnostics gained explicit client, viewport, and back-buffer
  fields with a pinned payload test;
- independent setting writers moved onto one sibling-preserving update path
  with a cross-setting regression test; and
- the remaining unscaled UI chrome metrics were scaled.

The canonical gate and real-window probe recorded above were rerun after those
corrections.

## Manual verification

No interactive row was marked `PASS`. The typography, responsive menu, startup
display, UI-scale, motion-comfort, and historical-theme observations are
recorded as `PENDING` in
[testing.md](../../../development/testing.md). Per the repository testing
contract, compilation, tests, or a window-opening probe cannot substitute for
a human observation on an interactive Windows desktop.

## Boundary checks

- No `Hukbo.Core` source, snapshot, replay, battle hash, or headless behavior
  was changed.
- UI scale, display mode, theme, and motion remain client presentation state.
- No new texture, shader, font-generation runtime, UI framework, network
  dependency, or hosted CI workflow was added.
