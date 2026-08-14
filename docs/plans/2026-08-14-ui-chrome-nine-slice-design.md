# UI chrome nine-slice sprite skin — design

Date: 2026-08-14
Status: design only. This document does not authorize implementation.
Anchors verified against the working tree at commit `8f2207f` on 2026-08-14.

## 1. Problem

Every panel in `Hukbo.Client` is drawn as flat rectangles of a single colour.
A panel is one filled rectangle for its surface, and its border is four more
one-pixel-thick rectangles emitted by `UiPrimitives.DrawBorder`
(`src/Hukbo.Client/UI/UiButton.cs:192-223`). There is no corner primitive, no
divider primitive, no rounded edge, and no texture. A repository-wide search
for nine-slice, border-radius, or texture-based chrome returns nothing.

This is a deliberate consequence of the client's origin: the entire game is
drawn with one white one-pixel texture created at `ArenaGame.cs:628`. That
choice has served the simulation well, but it puts a hard ceiling on how the
interface can look. Any visual treatment that is not a solid axis-aligned
rectangle is currently impossible to express.

## 2. Goal

Introduce a second, switchable way to draw panel chrome: a nine-slice sprite
skin sourced from a texture atlas, selected by a new persisted setting, with
the existing flat-rectangle look kept as the default.

The package is deliberately staged. Its first cut uses programmer art. The
point is to prove the primitive, the content pipeline, the toggle, and the
scale behaviour — not to ship a finished visual identity. Replacing the
placeholder atlas with real art must not require touching a single call site.

## 3. Non-goals

- **Pawn and battlefield sprites.** A separate package covers those, and it
  ships after this one.
- **Converting all twenty-four `DrawBorder` call sites.** The first cut wires a
  small, named subset. The rest keep the procedural path until a follow-up.
- **The arena edge border.** There is a second, unrelated `DrawBorder` at
  `src/Hukbo.Client/ArenaGame.Rendering.cs:1547`. It is private, it draws the
  map boundary rather than panel chrome, and it is out of scope. It must not be
  folded into this work.
- **New theme roles.** The existing twenty-seven are sufficient.
- **Any change to the simulation.** This package is presentation only.

## 4. The primitive

A new static class in `src/Hukbo.Client/UI/`, provisionally `UiNineSlice`,
holding one entry point:

```csharp
internal static void DrawPanel(
    SpriteBatch spriteBatch,
    Texture2D chromeAtlas,
    Rectangle bounds,
    Color surfaceTint,
    Color borderTint,
    int marginPixels)
```

It emits nine quads from one atlas region: four corners drawn at fixed size,
four edges stretched along one axis, and one centre stretched along both. The
rectangle arithmetic that produces those nine source and destination
rectangles is a pure function of `bounds` and `marginPixels`, and it is
extracted so it can be unit tested without a `GraphicsDevice`, following the
pattern documented on `tests/Hukbo.Client.Tests/PawnRendererTests.cs:14-24`.

`UiPrimitives.DrawBorder` is left untouched. It is called from roughly
twenty-four places, and adding a texture dependency to it would pull every one
of those call sites into this package's blast radius. The style branch lives at
the call site instead: when the setting is `Procedural`, the site calls the
existing fill plus `DrawBorder` exactly as it does today, so the default look is
byte-identical to the current build.

## 5. How tint preserves the twenty-seven theme roles

The atlas art is authored as white and grey geometry carrying shape only, no
colour. `SpriteBatch.Draw` multiplies by its `Color` argument, which is
precisely how the one-pixel texture is tinted today. A panel therefore passes
the same two theme colours it already resolves — `panelSurface` and
`panelBorder` from `UiThemeManager.ActiveTheme` — and every theme continues to
work with no new role and no per-theme atlas.

The role list is declared in
`src/Hukbo.Client/Content/Themes/ui-theme-standards.json:14-42` and backed by
the twenty-seven fields of `UiThemeColors` in
`src/Hukbo.Client/Theming/UiTheme.cs:11-38`.

## 6. Scale behaviour

Every chrome metric in the client passes through `UiScaleContext.Pixels`
(`src/Hukbo.Client/Theming/UiScaleContext.cs:23-27`), whose active percentage
is chosen by `UiScalePolicy.Resolve`
(`src/Hukbo.Client/Theming/UiScalePolicy.cs:12-41`) across four tiers: 100,
125, 150, and 200 percent.

A nine-slice margin baked at one fixed pixel width would therefore appear
proportionally thinner as the interface scales up. Two options exist, and this
design chooses the first:

1. **One atlas, scaled margins.** The margin passed to `DrawPanel` runs through
   `UiScaleContext.Pixels` like every other chrome metric, so corners grow with
   the interface. One asset, one load, and the corner art is magnified at the
   larger tiers.
2. **Four baked tiers.** Fonts already do this: `Content/Content.mgcb` carries
   twenty-four blocks, six roles across four scale tiers, loaded by
   `UiFontSet.Load` at `ArenaGame.cs:632` into a `SpriteFont[,]`
   (`src/Hukbo.Client/Theming/UiFontSet.cs:30-50`).

Option 1 is chosen for the first cut because the art is placeholder and
magnification artefacts do not yet matter. Option 2 is the known escape hatch,
its precedent is already in the repository, and the smoke rows are written to
detect the moment it becomes necessary.

## 7. Asset and pipeline

A new `#begin Textures/UiChrome.png` block in
`src/Hukbo.Client/Content/Content.mgcb`, using `TextureImporter` and
`TextureProcessor`.

This is the **first texture asset the repository has ever built**. Today
`Content.mgcb` contains font blocks and nothing else, and audio bypasses the
pipeline entirely by being copied verbatim. The build-time and gate-time cost
of adding a texture processor is therefore unmeasured, and the plan treats
proving it as its own first task rather than as a detail of a later one.

## 8. Sampler state

Panel chrome is drawn inside the interface `SpriteBatch` block, which begins
with `SamplerState.LinearClamp` (`src/Hukbo.Client/ArenaGame.Rendering.cs:672`).
The arena block above it uses `PointClamp` (`:651`).

Linear filtering on a pixel-authored nine-slice bleeds neighbouring texels
across slice seams, which shows up as a faint halo along the joins between
corner and edge cells. The mitigation is a nested `Begin`/`End` pair using
`PointClamp` around chrome draws, at the cost of breaking the interface batch
into three. Whether that cost is worth paying is left to measurement rather
than assertion: smoke row `CH-4` looks for the artefact directly, and the
nested batch is applied only if it appears.

## 9. The setting

A new enum `UiChromeStyle { Procedural = 0, NineSlice = 1 }` in
`src/Hukbo.Client/Settings/`, whose numeric values are part of the persisted
file contract and may never be renumbered.

It follows the **light** settings pattern — a raw
`SettingsChoiceSelector<UiChromeStyle>` constructed directly in `MenuOverlay`,
as `UiScale` and `StartupDisplayMode` already do. It deliberately does not add
a fourth hand-copied manager class; `docs/plans/TODO.md` already records the
three existing managers as debt, and this package must not deepen it.

Persistence bumps `ClientSettingsStore.SupportedSchemaVersion` from 10 to 11.
The change adds one independently defaulted field and does not alter the shape
of the file, so it is backward compatible on the same terms as the 3-to-4,
4-to-5, 7-to-8, and 8-to-9 bumps, and `AcceptedSchemaVersions` becomes
`[10, 11]`.

This was planned as a 9-to-10 bump against `[8, 9, 10]`. The calibrated army
composition landed on `main` first and took version 10 as a deliberate reset
that discards every older file, so this setting took version 11 instead and
the accepted window reopens only as far back as 10. Recorded on integration,
2026-08-14.

The setting takes effect live. Chrome style is read at draw time exactly as the
active theme is, so no restart and no explicit apply step is required.

## 10. `SettingsSelectorCount` reads 5 against six selectors, and that is correct

While planning, `SettingsSelectorCount = 5` at
`src/Hukbo.Client/MenuOverlay.cs:40` looked like it under-reported the settings
column, because six selectors are constructed at `MenuOverlay.cs:76-92`: theme,
gore, motion, auto-camera, interface scale, and display mode.

It does not. The menu has two columns, and the theme selector is not in the
settings one. `Layout` places `_themeSelector.Bounds` at `buttonLeft`
(`MenuOverlay.cs:592-596`), with the six buttons stacking beneath it from
`_themeSelector.Bounds.Bottom` (`:599-608`). Only gore, motion, auto-camera,
interface scale, and display mode are placed at `settingsLeft` (`:610-646`) —
exactly five.

`CalculateContentBottomOffset` (`:152-167`) matches that geometry. Its
button-column branch adds `selectorLayout.Height` as a standalone term, which
is the theme selector's row, on top of the button stack. Its settings-column
branch multiplies by `SettingsSelectorCount`. Raising the constant to 6 would
double-count the theme selector.

At 100 percent interface scale, with `selectorTopOffset` 122, selector height
96, `buttonHeight` 44, `buttonGap` 8, and the code's own `SettingsSelectorGap`
of 8:

| Column | Arithmetic | Height |
| --- | --- | --- |
| Button | `122 + 96 + 8 + (6 × 44) + (5 × 8)` | 530 |
| Settings, five selectors | `122 + (5 × 96) + (4 × 8)` | 634 |

The budget is `ResponsivePanelHeight` 680 minus `helperBottomOffset` 23, so 657.
The settings column governs, and it clears by 23 pixels.

## 10a. Adding a sixth settings selector overflows the panel

This is the finding that most affects the plan, and it was not anticipated when
the package was scoped.

Adding the chrome selector to the settings column means bumping
`SettingsSelectorCount` from 5 to 6 — not to 7, as double-counting the theme
selector would suggest. A sixth row costs one selector height plus one gap,
96 + 8 = 104 pixels:

```
settings column, six selectors = 122 + (6 × 96) + (5 × 8) = 738
budget                                                    = 657
overflow                                                  =  81
```

There are 23 pixels of headroom and the row needs 104. The menu panel cannot
hold a sixth settings selector as it stands, and
`MenuOverlayFocusTests.ThePanelIsTallEnoughForEveryMenuControl` will fail —
correctly — the moment one is added.

Three ways out, none of them free, and this design does not pick one because
the choice is a layout judgement rather than a technical one:

1. **Raise `ResponsivePanelHeight`** from 680 to at least 761. Simplest, but the
   constant exists to keep the menu usable at small window heights, so raising
   it trades one problem for another.
2. **Put the chrome selector in the button column** beneath the theme selector,
   where the two rendering-appearance controls arguably belong together. The
   button column has 127 pixels of slack against the settings column's 634, and
   a selector costs 104. It fits, barely, and it changes the focus-chain order.
3. **Shorten the selector row** for all six, or reduce `selectorTopOffset`.
   Touches every existing selector's appearance, so it is the widest change.

Option 2 is the cheapest and is the recommendation, but it is a visible change
to the menu's arrangement and the person the game is for should see it before
it is built.

## 11. The nine acceptance questions

Answered against `SIMULATION-GAME-STANDARDS.md` section 10.

1. **User-visible outcome.** Panels are drawn with a nine-slice sprite skin
   instead of flat rectangles when the spectator selects it.
2. **Tick stage and state read or written.** None. This package touches no tick
   stage and no simulation state.
3. **Numeric units, bounds, same-tick conflict rule.** Margins are interface
   pixels after `UiScaleContext.Pixels`. No same-tick conflict exists.
4. **Total ordering and random-stream policy.** No ordering and no randomness.
   Draw order is unchanged call order under `SpriteSortMode.Deferred`.
5. **Cache source and invalidation.** One texture loaded once at content load.
   No runtime cache is introduced.
6. **Save, event, and version effect.** No event. No simulation version. One
   client settings schema bump, 10 to 11, backward compatible. Neither the state
   hash nor the event hash can move, because no simulation code is touched.
7. **Worst-case complexity and benchmark workload.** Nine quads per panel
   instead of five, across roughly fifteen panels — a bounded constant increase
   in a layer the pawn quad budgets do not govern.
8. **Spectator explanation.** A labelled selector in the settings menu, reading
   `PANEL STYLE`, with an immediate and reversible visible effect. A spectator
   discovers the feature by opening the menu; no source reading is required.
9. **Tests failing before and passing after.** Nine-slice rectangle arithmetic
   tests, settings round-trip and schema-window tests, and the menu focus-chain
   and panel-height tests extended for the new control.

## 12. Open questions

1. Primitive name: `UiNineSlice`, `UiChromePrimitives`, or a method on
   `UiPrimitives`. Recommendation: a new static class, keeping `UiPrimitives`
   free of a texture dependency.
2. One shared atlas region tinted per role, or distinct corner art per surface
   role. Recommendation: one region, matching how flat rectangles already work.
3. Which call sites the first cut converts. Recommendation: two, the menu panel
   and the confirmation prompt, both of which a spectator can reach in seconds.
4. Whether `Procedural` falls through to the literal existing `DrawBorder` call
   or draws a flat atlas region. Recommendation: the literal call, so the
   default look cannot regress.
5. Who authors the placeholder atlas, and whether it is committed as a PNG or
   generated. This blocks three downstream tasks and is the package's critical
   path.
6. Whether the nested `PointClamp` batch is needed, which `CH-4` decides.
