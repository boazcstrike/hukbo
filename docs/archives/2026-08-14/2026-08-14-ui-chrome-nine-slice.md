# UI chrome nine-slice sprite skin — plan

**Archived: reference only.** All nine tasks were executed and the canonical
gate was green on the rebased branch, with zero `[FAIL]` lines and
`Hukbo.Client.Tests` at 3,867. Of the six `CH` smoke rows this plan wrote, five
were run by a person on 2026-08-14 and passed, and they were removed from the
checklist rather than left sitting green. The sixth, `CH-4`, was rescoped and
remains `PENDING` there, because as first written it asked for four
interface-scale tiers and only two are reachable on a 1080p display. Its
sampler question — whether linear filtering bleeds across the nine-slice
seams — is still open, and the design document, which stays live in
`docs/plans/`, is where that question is set out.

Never execute this plan, never treat it as a live task list, and never cite it
as the reason to make a change. The live contract for this project is
`CLAUDE.md`, `SIMULATION-GAME-STANDARDS.md`, `docs/development/testing.md`,
`docs/development/smoke-checklist.md`, and `.claude/skills/`.

Date: 2026-08-14
Design: "UI chrome nine-slice sprite skin — design", which stays live in
`docs/plans/`.
Anchors verified against the working tree at commit `8f2207f` on 2026-08-14.

The design document governs. Where this plan and the design disagree, the
design wins and the plan is wrong.

## Before starting

Task CH-T1 is the critical path. The placeholder atlas does not exist, and
three tasks cannot begin until it does. Do not schedule CH-T5, CH-T7, or CH-T8
as if the asset were a background detail.

Work in a git worktree branched from local `main`. The interface, theming,
settings, and content directories were clean of other sessions' changes when
this plan was written, but `src/Hukbo.Client/ArenaGame.Rendering.cs`,
`ArenaGame.cs`, and the pawn rendering files were not — confirm before assuming
a conflict is yours.

## What was run, 2026-08-14

Every task except CH-T9 is done, and the package is on branch
`ui-chrome-nine-slice` branched from `main` at `8f2207f`. CH-T9's six rows are
in `docs/development/smoke-checklist.md` in the main working tree rather than on
this branch, because that file was being rewritten concurrently by the
death-collapse package and forking it would have built a conflict for nothing.

The canonical gate was run by the integrator on the integrated branch, not by
any sub-agent:

```
[PASS] Required prerequisites and repository configuration are present.
[PASS] Locked package restore completed.
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
[PASS] Release repository tests completed.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.   (x5)
[PASS] Canonical repository verification completed.
```

`Hukbo.Client.Tests` went from 3,786 to 3,867. None of that is evidence for a
single `CH` smoke row: what it proves is that the tile arithmetic, the settings
round-trip, the schema window, and the focus chain behave as specified. Whether
a nine-slice panel reads as an improvement is not a property a test holds an
opinion about.

Two things were found rather than planned, and both are recorded where they
happened rather than only here. CH-T6 found that `SettingsSelectorCount = 5` is
correct and that a sixth settings-column selector would overflow the panel by
81 pixels, which is why the chrome selector went into the button column
instead — design sections 10 and 10a. CH-T5 found that `SourceHygieneTests`
pinned `Content.mgcb` to exactly 24 spritefonts under R-W6.18 and OD-4, so the
first texture this repository has ever built required re-recording that pin;
the list stays an exact match and a twenty-sixth entry still fails.

## Tasks

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| CH-T1 | Author a placeholder nine-slice atlas: white and grey shape only, no colour, with corner, edge, and centre regions on a documented grid. Record the slice margin in a comment beside the asset. | `src/Hukbo.Client/Content/Textures/UiChrome.png` (new), `src/Hukbo.Client/Content/Textures/README.md` (new) | The PNG exists and its slice grid is written down | none | File present, grid documented. **SERIAL — blocks CH-T5, CH-T7, CH-T8** |
| CH-T2 | Add the `UiChromeStyle` enum, doc-commented that its numeric values are part of the persisted file contract. | `src/Hukbo.Client/Settings/UiChromeStyle.cs` (new) | Compiles, matches the shape of `UiScale.cs` | none | Release build. **PARALLEL** |
| CH-T3 | Add the field to `ClientSettings`, bump `SupportedSchemaVersion` 9 to 10, extend `AcceptedSchemaVersions` to `[8, 9, 10]`, add the default constant, the `Resolve` clamp, the raw nullable field, and thread it through `Load`, `TrySave`, `TryUpdate`, and `Default`. Append a paragraph to the schema doc comment following the 3-to-4 bump's wording. | `src/Hukbo.Client/Settings/ClientSettings.cs`, `src/Hukbo.Client/Settings/ClientSettingsStore.cs` | A version 9 file loads with the field defaulting; a version 10 file round-trips | CH-T2 | `ClientSettingsStoreTests`, extended. **PARALLEL with CH-T4, CH-T6** |
| CH-T4 | Extract the nine-slice rectangle arithmetic as a pure function returning the nine source and destination rectangle pairs from `bounds` and `marginPixels`, then build `DrawPanel` on top of it. | `src/Hukbo.Client/UI/UiNineSlice.cs` (new) | Nine rectangles tile `bounds` exactly with no gap and no overlap, at every margin from 1 to 16 and at degenerate bounds smaller than twice the margin | none | `tests/Hukbo.Client.Tests/UiNineSliceTests.cs` (new). **PARALLEL** |
| CH-T5 | Add the `#begin Textures/UiChrome.png` block with `TextureImporter` and `TextureProcessor`, then load the atlas beside the font load. | `src/Hukbo.Client/Content/Content.mgcb`, `src/Hukbo.Client/ArenaGame.cs` | `./scripts/verify.ps1` is green with the texture in the pipeline, and the load call returns a non-null texture | CH-T1 | Gate output pasted. **SERIAL after CH-T1 — this is the first texture the repository has ever built; run the full gate on this task alone before anything depends on it** |
| CH-T6 | ~~Investigate `SettingsSelectorCount = 5` against the six selectors constructed at `MenuOverlay.cs:76-92`.~~ **Done 2026-08-14.** Verdict: the constant is correct. The theme selector sits in the button column at `buttonLeft` (`MenuOverlay.cs:592-596`) and its height is already reserved by the button-column branch of `CalculateContentBottomOffset`; the settings column holds exactly five. A doc comment on the constant now says so, and `SettingsColumnFormulaMatchesActualSettingsColumnGeometry` fails if the accounting drifts. See design section 10. | `src/Hukbo.Client/MenuOverlay.cs`, `tests/Hukbo.Client.Tests/MenuOverlayFocusTests.cs` | Done | none | Verified independently against disk by the integrator, not taken from the agent's report |
| CH-T7 | Add the selector to `MenuOverlay`: field, construction, the appended terminal control index, `ControlCount`, motion advance, hover bounds in stacking order, the update branch, the draw call, layout bounds, `GetControlBounds`, the `MenuInteraction` field, and the new parameter on `Update` and `Draw`. Thread the new positional argument through all seven `MenuInteraction` construction sites. **Blocked on a decision, not on code — see below.** | `src/Hukbo.Client/MenuOverlay.cs`, `src/Hukbo.Client/UI/` selector wiring | The selector is focusable, wraps correctly, the focus chain stays contiguous, and the panel still holds every control | CH-T2, CH-T6, **and the panel-overflow decision** | `MenuOverlayFocusTests`, extended. **SERIAL after CH-T6 — same file** |
| CH-T8 | Wire the style through `ArenaGame`: hold the value, persist on change, pass it to `MenuOverlay.Update` and `Draw`, and branch two named call sites — the menu panel and the confirmation prompt — between `UiNineSlice.DrawPanel` and the existing fill plus `DrawBorder`. | `src/Hukbo.Client/ArenaGame.cs`, `src/Hukbo.Client/UI/ConfirmationPrompt.cs`, `src/Hukbo.Client/MenuOverlay.cs` draw site | Toggling the selector changes both panels live, and `Procedural` is visually identical to the pre-change build | CH-T3, CH-T4, CH-T5, CH-T7 | `./scripts/verify.ps1` green, then smoke rows `CH-1` to `CH-6`. **SERIAL, last** |
| CH-T9 | Add the six `CH` smoke rows to the checklist as `PENDING`, with the exact manual steps below. | `docs/development/smoke-checklist.md` | Six rows present, all `PENDING` | none | Row count. **PARALLEL. No agent may flip a row** |

## Smoke rows

The prefix `CH` was checked against every prefix in use and in the archives and
collides with none.

| Row | Steps | Expected |
| --- | --- | --- |
| `CH-1` | Launch the game and open the settings menu. | A `PANEL STYLE` selector is present, reads `Procedural`, and every panel looks exactly as it did before this package. |
| `CH-2` | With the menu open, cycle `PANEL STYLE` to `NineSlice`. | The menu panel and the confirmation prompt switch to the sprite skin immediately, with no restart, no flicker, and no crash. |
| `CH-3` | Cycle `PANEL STYLE` back to `Procedural`. | Both panels revert to the flat-rectangle look, identical to `CH-1`. |
| `CH-4` | With `NineSlice` active, cycle interface scale through all four tiers and look closely at the joins between corner and edge cells. | Corners and margins grow with the interface. Record whether a bleed halo appears at any tier — this row decides whether the nested `PointClamp` batch is needed. |
| `CH-5` | With `NineSlice` active, cycle through every theme. | Chrome recolours with each theme, and no theme produces an invisible or illegible border. |
| `CH-6` | Set `NineSlice`, quit, and relaunch. | The setting persisted and the sprite skin is active on launch. |

Only a person at an interactive desktop may flip one of these rows. Compilation,
unit tests, and a window-opening probe do not make a row pass. A row nobody
attempted stays `PENDING`; a row that cannot be attempted is `BLOCKED` with the
reason recorded.

## Verification

The canonical gate is `./scripts/verify.ps1`, run once after integration by the
integrator, never delegated to a sub-agent, and never reported as green without
its actual output.

The eighteen interface, theme, and layout test files must stay green
throughout. The ones most likely to move are `MenuOverlayFocusTests`,
`UiThemeCatalogTests`, `UiButtonTests`, `UiScaledChromeTests`, and
`ArenaGameResponsiveChromeTests`.

Client tests may not construct `ArenaGame`, a graphics device, a sprite batch,
or a window. The only part of this package that is unit testable is CH-T4's
rectangle arithmetic and CH-T3's settings round-trip. Everything visual is a
smoke row, and it stays `PENDING` until a human runs it.

## Risks

| Risk | Mitigation |
| --- | --- |
| The content pipeline has never built a texture in this repository. Build cost, lock-file effects, and gate impact are unmeasured. | CH-T5 runs the full gate on its own before any task depends on it. |
| The placeholder atlas does not exist and blocks three tasks. | CH-T1 is scheduled first and named as the critical path. |
| Linear filtering bleeds across slice seams in the interface batch. | `CH-4` looks for it directly; the nested `PointClamp` batch is applied only if the artefact appears. |
| Someone unifies the arena-edge `DrawBorder` at `ArenaGame.Rendering.cs:1547` into this work. | Named as a non-goal in the design document's section 3. |
| `MenuInteraction` is a positional record with seven construction sites, all of which break at once. | CH-T7 owns every one of them in a single task rather than splitting them. |
| ~~Bumping `SettingsSelectorCount` without understanding why it reads 5 hides a real layout bug.~~ **Retired 2026-08-14.** CH-T6 answered it: the constant is correct, and the doc comment on it now says why. | — |
| **The menu panel cannot hold a sixth settings selector.** Five selectors bring the settings column to 634 pixels against a 657-pixel budget, and a sixth costs 104. CH-T7 overflows by 81 pixels and `ThePanelIsTallEnoughForEveryMenuControl` will fail, correctly. | Blocked on a layout decision, not on code — design section 10a sets out the three options and recommends putting the chrome selector in the button column beneath the theme selector. The person the game is for should choose before CH-T7 runs. |
| Worktree agents branch from a commit, so uncommitted plan documents and assets are invisible to them. Two of the three CH agents reported the design and plan documents as missing and worked from their prompts instead. | Commit the documents and the atlas before dispatching CH-T5, CH-T7, or CH-T8. |
