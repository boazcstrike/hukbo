# Responsive menu, startup display, and UI motion smoke — closed 2026-08-13

**Archived: reference only.** This is a finished record of a smoke-testing
family that is now fully closed. Do not execute anything described here, and
do not cite this file as the reason to do anything going forward. The live
contract for what interactive verification is required, and how it must be
recorded, is `CLAUDE.md` together with `docs/development/smoke-checklist.md`;
this file only preserves how one family of rows reached `PASS`.

This file is the closing record for the sixteen-row "Responsive menu, startup
display, and UI motion smoke" family added by the UI/UX completion work. All
sixteen rows are now closed. Thirteen of them — `UI-1`, `UI-3`, `UI-5`, and
`UI-7` through `UI-16` — were run by a person and closed on 2026-08-11. The
remaining three — `UI-2`, `UI-4`, and `UI-6` — were also run on 2026-08-11 and
failed, all three for the same single cause described in finding 1 below. That
cause was fixed the same day, 2026-08-11, and the three rows were left
`PENDING` a re-run rather than closed on the strength of the fix alone. A
person at an interactive Windows desktop re-ran all three on 2026-08-13 and
reported all three passing, which closes the family in full.

## Evidence — 2026-08-11 run

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-11 |
| Machine/platform | Microsoft Windows 10.0.26200 (Windows 11 Pro) x64, NVIDIA GeForce RTX 4070 SUPER, 2560x1440 display at 125% Windows scaling (`AppliedDPI` 120) |
| Source commit | Not captured by the tester. `main` was at `ae64485` when these results were transcribed, and every commit between that and the run was documentation-only, so the binary is unchanged. |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

## Evidence — 2026-08-13 re-run

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-13 |
| Machine/platform | Not separately recorded beyond "an interactive Windows desktop"; the re-run was performed by a person at the keyboard, not measured through the headless pipeline. |
| Source commit | Not captured by the tester. |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

## Check table

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| UI-2. Common landscape and maximised layouts | At 1280x720, 1920x1080, and the maximised desktop size, the menu stays centred and balanced, the arena HUD remains readable, and no panel covers an unrelated control. | 2026-08-11, tester at the desktop. Layout held: the menu stayed centred and balanced and no panel covered an unrelated control at any of the three sizes. The row also asks that the arena HUD remain **readable**, and at the maximised desktop size it does not — every glyph is visibly pixelated. The layout half passed and the readability half failed, and a row is a single status, so the row fails. Cause in finding 1. **Fixed the same day** by the DPI awareness declaration; a logged run now reports a 2560x1440 viewport where it reported the virtualised size before. Back to `PENDING` because only a person can say the glyphs read as crisp. **Closed 2026-08-13**, re-run by a person at an interactive desktop reported passing. | PASS |
| UI-4. Preferred UI scales and safety cap | Select Auto, 100%, 125%, 150%, and 200%. The selected preference persists after restart; when the viewport is too small for it, the active tier is safely capped while the preferred value remains selected in the menu. | 2026-08-11, tester at the desktop. Selection, persistence across a restart, and the safety cap all behaved as written. But no tier renders crisply once the window fills the screen, and the tier the policy selects at that size is itself wrong: on this 2560x1440 display the game is handed a virtualised 2048x1152 viewport, which clears `UiScalePolicy`'s 1920x1080 bar but not its 2560x1440 one, so Auto resolves to 125% where the real screen deserves 150%. Cause in finding 1. **Fixed the same day**: the viewport is now real, so Auto resolves correctly with no change to `UiScalePolicy` itself. Back to `PENDING` for a re-run. **Set UI Scale to Auto before re-running** — the saved preference on the reporting machine is an explicit `100`, left over from this row's own sweep, and an explicit preference is honoured rather than overridden, so a re-run that skips this step measures the 100% tier and learns nothing about Auto. **Closed 2026-08-13**, re-run by a person at an interactive desktop reported passing. | PASS |
| UI-6. Fullscreen startup | Select Fullscreen, close the game fully, and relaunch. It opens in soft fullscreen at the current desktop resolution. Select Windowed, restart again, and confirm normal windowed startup returns. | 2026-08-11, tester at the desktop. The mode round-trip worked: Fullscreen persisted across a full close and relaunch, opened in soft fullscreen, and selecting Windowed restored normal windowed startup. It does not open at "the current desktop resolution" — it opens at the virtualised 2048x1152 the OS reports instead of the true 2560x1440 — and the text is pixelated throughout. Cause in finding 1. **Fixed the same day**: a logged fullscreen run now reports `client` and `viewport` both at the display's true 2560x1440. Back to `PENDING` because the row's own wording — that it opens at the current desktop resolution — is now satisfied in the log but has not been seen by a person. **Closed 2026-08-13**, re-run by a person at an interactive desktop reported passing. | PASS |

## Findings from the 2026-08-11 UI run

**1. Text is pixelated whenever the window fills the screen, and the cause is
that the process never declares DPI awareness.** This is the single cause behind
all three failures — `UI-2`, `UI-4`, and `UI-6` — and it is not a defect in the
font ramp.

The typography pipeline is doing exactly what it was designed to do.
`UiFontRamp` bakes twenty-four separate `SpriteFont` atlases, one per role per
tier, and `UiPrimitives.DrawText` and `UiPrimitives.DrawCenteredText` both draw
at a hardcoded scale of `1f` from a whole-pixel origin snapped by
`UiTextGeometry.SnapToPixel`. There is no render target, no float resampling,
and no scale multiplier anywhere on the text path. Every glyph is crisp when it
leaves the game.

What resamples it is Windows. Nothing in the repository declares a DPI
awareness level: `src/Hukbo.Client/Hukbo.Client.csproj` has no
`ApplicationManifest`, there is no `app.manifest` anywhere in the tree, no code
calls `SetProcessDpiAwarenessContext`, and neither the client nor its launch
script sets SDL's `SDL_WINDOWS_DPI_AWARENESS` hint. A process that says nothing
is treated as DPI-unaware, so Windows reports a virtualised desktop size, lets
the application render at that size, and then bitmap-stretches the finished
frame up to the real panel. On the machine this run was performed on, the
stretch factor is 1.25 and non-integer, which is precisely what a pixelated
glyph looks like.

The machine's numbers: the display is 2560x1440 and Windows display scaling is
125%, read from `HKCU:\Control Panel\Desktop\WindowMetrics\AppliedDPI`, which is
`120`. A DPI-unaware process on that machine is told the desktop is 2048x1152.

That mis-report has a second consequence, which is why `UI-4` fails as well as
looking bad. `UiScalePolicy.Resolve` picks a tier from the viewport in pixels:
2048x1152 clears its 1920x1080 threshold but not its 2560x1440 one, so Auto
resolves to `Percent125` on a display that should be getting `Percent150`. The
tier is chosen from a number the operating system fabricated.

The remedy is to declare per-monitor awareness once, before the graphics device
exists — a `SetProcessDpiAwarenessContext(PER_MONITOR_AWARE_V2)` call at the top
of `Program.Main`, which matches the `LibraryImport` P/Invoke pattern
`ArenaGame` already uses for its SDL window-chrome calls, or an
`ApplicationManifest` declaring `PerMonitorV2`. Either one makes
`GraphicsAdapter.DefaultAdapter.CurrentDisplayMode` report 2560x1440, removes
the OS stretch entirely, and lets `UiScalePolicy` select the 150% bake it was
always meant to select at that size.

**This has been recorded once before and was deliberately deferred.** Row 75 of
the typography section, "Display scaling", is the gated measurement task this
finding is the other half of; it is marked `DECLINED` because the 150% Windows
scaling reading was declined on 2026-07-28. That decision is what left the
awareness declaration unbuilt, and the defect stayed latent until somebody ran
the game on a scaled display. Row 75 stays `DECLINED`: it asked for a
measurement to justify building this, and the justification arrived instead as
three failed rows, which is the better evidence.

**Fixed on 2026-08-11.** `ProcessDpiAwareness.Apply` declares per-monitor v2
awareness from `Program.Main`, before `ArenaGame` builds its
`GraphicsDeviceManager` and before SDL creates a window, which is the ordering
the declaration requires. The design, the rejected manifest alternative, and
the reason the P/Invoke itself carries no test are in
[`../../plans/2026-08-11-display-dpi-awareness-design.md`](../../plans/2026-08-11-display-dpi-awareness-design.md).
`UiScalePolicy` is unchanged — it was never wrong, only fed a fabricated
number.

**The measurement this finding originally asked for was taken, after the fix
rather than before it.** A logged run on the reporting machine now writes
`boot.dpi.awareness` with `state` `applied`, and the `render.viewport.changed`
line that follows reports `client` and `viewport` both at **2560x1440** — the
display's true resolution, where an unaware process would have reported
2048x1152. The pre-fix line was never captured and now cannot be, since the
build that produced it no longer exists; the registry reading and the policy
threshold arithmetic are what stand behind the 2048x1152 figure.

**A re-run needs one setup step.** The saved `uiScale` preference on the
reporting machine is an explicit `100`, left behind by `UI-4`'s own sweep
through every tier, and an explicit preference is honoured rather than
overridden — the logged run above resolved `Percent100` at 2560x1440 for
exactly that reason, correctly. Set UI Scale back to Auto before re-running, or
the re-run measures the 100% tier and says nothing about the fix.

**The fix is now verified twice: once by measurement, once by a person.** The
2026-08-11 logged run verified the mechanism — real viewport, correctly
resolved scale tier. The 2026-08-13 re-run verified the thing a log cannot say
for itself: a person at an interactive desktop reported all three of `UI-2`,
`UI-4`, and `UI-6` passing, including the readability and crispness judgments
that only a person watching the screen can make.

**2. The `Cebu 1521 — Provisional` theme is disliked, and that is not what
`UI-11` measures.** Every criterion the row states was met — the label, the
palette's reading, and the legibility of text and faction signals — so the row
is `PASS`. Separately, the tester does not like how the theme looks. That is a
real report and worth acting on, but it is a design preference rather than a
failure of any stated criterion, and folding it into `UI-11`'s status would
leave a row nobody could ever close without agreeing on taste.

Acting on it needs the preference turned into a criterion first: which of the
five palette anchors is wrong, and wrong against what. The theme is a
**Provisional reconstruction** under the historical accuracy policy in section
7 of `CLAUDE.md`, so a change to it is a change to a labelled provisional
interpretation and needs the evidence tier restated alongside it, not just a
new set of colours. Until that is written down, no row here covers the
complaint.

**This complaint is still open.** Closing the sixteen-row family on
2026-08-13 does not resolve it: `UI-11` passed on its own stated criteria, and
that status stands, but the design complaint above was never a row and remains
unaddressed. What is still needed is exactly as stated when this finding was
first recorded — a written statement of which of the five palette anchors is
wrong, wrong against what, and the evidence tier for the replacement restated
per section 7 of `CLAUDE.md` — and none of that was produced by this smoke
family or its 2026-08-13 re-run.
