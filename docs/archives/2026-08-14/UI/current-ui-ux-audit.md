# Current UI and UX Audit

**Status:** Current-code research

**Date:** 2026-07-31

**Archived: reference only.** This is the audit half of the finished 2026-07-31
UI and UX package. It describes the client as it stood on that date, and the
code it points at has moved since. Never treat it as a live description, and
never cite it as the reason to make a change. The live contract for this project
remains `CLAUDE.md` and `docs/development/smoke-checklist.md`. Archived
2026-08-14.

## Scope and method

This audit reads the live `Hukbo.*` source as authoritative. The repository
knowledge graph was used only as an initial index because it does not contain
the complete current client. No repository reindex was performed.

The audit covers typography, viewport responsiveness, display mode, theme
architecture, interaction feedback, and deterministic boundaries. It does not
change code or mark any manual test as passed.

## Current architecture

### Typography and sampling

The current font ramp is already role-based:

| Role | Nominal size |
|---|---:|
| Caption | 12 px |
| Body | 14 px |
| Label | 17 px |
| Subtitle | 20 px |
| Title | 22 px |
| Display | 38 px |

[`UiFontRamp`](../../../../src/Hukbo.Client/Theming/UiFontRamp.cs) requires each
font to be drawn at `1.0` scale. [`UiButton`](../../../../src/Hukbo.Client/UI/UiButton.cs)
uses the shared text primitives at that scale, and
[`UiTextGeometry`](../../../../src/Hukbo.Client/UI/UiTextGeometry.cs) snaps text to
whole pixels.

[`ArenaGame.Rendering`](../../../../src/Hukbo.Client/ArenaGame.Rendering.cs)
uses `PointClamp` for the arena and `LinearClamp` for UI. This is the correct
separation for pixel-art units and anti-aliased UI glyphs.

**Conclusion:** enlarging the existing atlas during drawing is not the right
fix. The renderer needs larger pre-baked font tiers and correspondingly scaled
layout metrics. Any remaining blur caused by Windows DPI virtualization must be
measured separately.

### Responsive layout

The ordinary startup viewport is `1280x720`; the window is resizable and
borderless. Several UI regions still use fixed pixel dimensions:

- status bar: `68` px;
- inspector: `310` px;
- right column: up to `420` px;
- control bar: approximately `660` px across seven fixed-width buttons;
- menu panel: `360x912` px.

The theme standard defines the `912` px menu panel, and
[`MenuOverlay`](../../../../src/Hukbo.Client/MenuOverlay.cs) centers it without
clamping it to the viewport. At `720` px client height, the panel begins at
`-96` and ends at `816`. Lower controls and help text can therefore be outside
the default window.

Current tests establish internal focus order and panel geometry, but do not
prove containment inside the default or a narrow resized viewport.

**Priority finding:** menu containment is a prerequisite for adding a display
mode or UI scale selector.

### Window state and fullscreen

[`ArenaGame`](../../../../src/Hukbo.Client/ArenaGame.cs) currently implements
maximize and restore through the SDL window. Repository searches found no
fullscreen state, `ToggleFullScreen`, `HardwareModeSwitch`, or persisted
display-mode setting. "Max" must therefore not be described as fullscreen.

[`ClientSettings`](../../../../src/Hukbo.Client/Settings/ClientSettings.cs)
persists theme, composition, gore, motion, and auto-camera values. Settings are
loaded before the graphics manager is configured, which provides a clean seam
for a startup display choice.

The smallest safe first version is:

- `Windowed` — retain the current borderless, resizable window;
- `Fullscreen` — use soft/borderless fullscreen at startup;
- clearly label the setting as applying on the next launch.

MonoGame exposes `GraphicsDeviceManager.IsFullScreen`,
`HardwareModeSwitch`, preferred back-buffer dimensions, and `ApplyChanges`.
It also exposes `GameWindow.ClientSizeChanged`. Live switching would need
explicit device-reset, render-target, viewport, input, and layout validation;
that path does not exist in the current client.

Authoritative API references:

- [MonoGame GraphicsDeviceManager](https://docs.monogame.net/api/Microsoft.Xna.Framework.GraphicsDeviceManager.html)
- [MonoGame GameWindow](https://docs.monogame.net/api/Microsoft.Xna.Framework.GameWindow.html)

### Theme system

[`ui-theme-standards.json`](../../../../src/Hukbo.Client/Content/Themes/ui-theme-standards.json)
contains five themes:

- `command`;
- `field-manual`;
- `signal`;
- `broadcast`;
- `high-contrast`.

Each theme maps the same semantic color roles and metrics. This is a strong
extension seam: a historical theme can change visual tokens while retaining
layout, controls, state meaning, and input behavior.

Theme IDs and the exact five-theme set are present in fallbacks and tests. The
least disruptive approach is to add a sixth stable ID rather than silently
retuning a saved user's current choice.

### Motion and feedback

Most UI state changes are immediate:

- buttons switch fill and border colors with no elapsed transition;
- menus and confirmation prompts appear or disappear in one frame;
- event selection and agent selection update immediately;
- summary and report surfaces have no entrance hierarchy.

`ArenaGame.Update` already receives unscaled frame elapsed time. Central client
command handling, menu/prompt lifecycle methods, event-feed counters, and agent
selection changes provide bounded trigger points. No simulation change is
required.

The existing motion intensity setting is the appropriate user-facing control:

| Motion setting | Proposed UI behavior |
|---|---|
| Off | All UI states snap immediately |
| Reduced | Opacity and color transitions only |
| Full | Opacity/color plus restrained integer-pixel decorative movement |

Enum values need not change, preserving saved settings.

## Problem classification

| Area | Current issue | Root-cause confidence | Needed evidence |
|---|---|---:|---|
| Maximized text | Text stays physically small; possible DPI blur is unmeasured | High for fixed sizing, unknown for DPI | Screenshots and client/viewport/DPI measurements |
| Menu | Taller than default viewport and unclamped | Confirmed in source | Default and minimum-viewport render captures |
| Fullscreen | No fullscreen mode or persisted choice | Confirmed in source | Startup smoke tests after implementation |
| Historical theme | Current default is modern navy/cyan | Confirmed in theme data | User chooses a bounded visual direction |
| UI motion | Immediate state changes | Confirmed in source | Interaction prototypes and reduced-motion review |

## Measurement matrix

Before selecting scale breakpoints, capture the same UI in every combination
that is available on the test machine:

| Client state | Resolution examples | Windows display scale |
|---|---|---|
| Default window | 1280x720 | 100%, 125%, 150%, 200% |
| Resized window | 1024x720 and the proposed minimum | 100%, 150% |
| Maximized | Native desktop resolution | 100%, 125%, 150%, 200% |
| Soft fullscreen | Native desktop resolution | 100%, 150% |

For each case record:

- logical client bounds;
- graphics viewport and back-buffer dimensions;
- desktop display mode;
- chosen UI scale tier;
- screenshot of menu, battle HUD, inspector, and report;
- whether glyph edges are crisp and whether every control is visible;
- input hit-testing alignment.

The existing typography rows in
[`docs/development/testing.md`](../../../development/testing.md) remain `PENDING`.
Their prior expectation that text stays at a constant pixel size should be
revised only after an approved scaling design.

## Recommended scaling model

Use a global UI scale, not a text-only multiplier:

- `Auto`;
- `100%`;
- `125%`;
- `150%`;
- `200%`.

Each tier should select pre-baked font assets and discrete UI metrics. Fonts
remain at draw scale `1.0`; coordinates and hit targets remain integral. `Auto`
chooses the largest tier that satisfies the supported layout constraints, not
merely the viewport-to-`1280x720` ratio.

Rejected for the first pass:

- scaling the current font atlases at draw time, because it softens glyphs;
- rendering the complete UI to a small fixed render target and enlarging it;
- runtime vector, SDF, or MSDF font generation, because it adds a new resource
  and dependency lifecycle before the simpler approach is measured.

## Deterministic and architectural constraints

- UI scale, window mode, theme, and animation state stay in `Hukbo.Client`.
- No UI transition enters a snapshot, state hash, replay, or headless runner.
- Animations use frame elapsed time, never authoritative simulation ticks.
- Transition collections are fixed and bounded; no entity-keyed unbounded
  cache is introduced.
- Text is never animated by fractional translation or scale.
- Input uses settled control rectangles, so a transition cannot move a target
  away from the pointer.
