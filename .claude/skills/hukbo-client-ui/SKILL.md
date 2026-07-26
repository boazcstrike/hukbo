---
name: hukbo-client-ui
description: Conventions for building and testing MonoGame UI in Hukbo.Client — panels, HUD, control bar, menu overlay, agent inspector, event log, themes, and pointer handling. Use when adding or changing anything drawn on screen, when writing Client tests, when picking colors or metrics, or when deciding how a click or scroll should be consumed. Covers the pure-helper testability pattern that keeps GraphicsDevice and SpriteBatch out of tests, the 27 semantic theme roles, and the pointer priority chain.
---

# Hukbo.Client UI conventions

## Testability: pure helpers, untestable Draw

Client presentation tests must never construct `ArenaGame`, a `GraphicsDevice`,
a `SpriteBatch`, or a window, and must not depend on GPU, audio, window focus,
network, wall clock, `System.Random`, or platform input types.

The repo achieves this with a hard split, and it currently holds: there are
**zero** occurrences of `SpriteBatch`, `GraphicsDevice`, or `ArenaGame` anywhere
under `tests/`. Keep it that way.

- **Pure `internal static` helpers** take plain values (`Rectangle`, `Point`,
  counts, state records) and return plain values. These carry all the logic and
  all the tests.
- **`Draw` methods** take the `SpriteBatch` and only paint what the helpers
  already decided. They are not unit tested.

Follow the shape already established in `src/Hukbo.Client/UI/BattleEventLogPanel.cs`:

```csharp
internal static BattleEventPanelLayout CalculateLayout(Rectangle bounds)
internal static int HitTestVisibleRow(...)
internal static BattleEventFilterTarget HitTestFilter(...)
internal static Rectangle GetScrollbarThumb(...)
internal static BattleEventPanelState GetPanelState(...)
internal static BattleEventKeyboardFocusTarget GetKeyboardFocusTarget(...)
internal static bool ShouldReleaseKeyboardFocus(...)
```

`MenuOverlay.ResolveFocusedControlIndex` is the same pattern for focus movement.

Tests assert containment, hit targets, ordering, and state transitions — never
pixels. A new panel that cannot be tested this way is designed wrong; extract the
decision before writing the drawing code.

## Themes: pick a role, never a color

Never hardcode a `Color` in a panel. `UiThemeColors` in
`src/Hukbo.Client/Theming/UiTheme.cs` defines 27 semantic roles, and
`UiThemeCatalog` validates that every theme supplies every role and rejects
unknown roles outright.

| Family | Roles |
| --- | --- |
| Canvas and arena | `CanvasBackground`, `ArenaSurface`, `ArenaBorder` |
| Surfaces | `StatusSurface`, `OverlayScrim`, `PanelSurface`, `PanelAlternate`, `PanelBorder` |
| Text | `TextPrimary`, `TextSecondary`, `TextDisabled`, `TextInverse` |
| Actions | `ActionDefault`, `ActionHover`, `ActionFocus`, `ActionPressed`, `ActionActive`, `ActionDisabled` |
| Status | `StatusInfo`, `StatusSuccess`, `StatusWarning`, `StatusDanger` |
| Domain | `TeamA`, `TeamB`, `OtherFaction`, `Selection`, `NewEvent` |

Metrics come from `UiThemeMetrics`: `BorderThickness` (1-4), `FocusThickness`
(2-5), `ShadowOffset` (0-6). The catalog enforces those ranges.

Catalog rules worth knowing before you touch theme data:

- Exactly five themes, IDs `command`, `field-manual`, `signal`, `broadcast`,
  `high-contrast`; schema version 1; the default ID must name a catalog theme.
- Colors are `#RRGGBB` or `#RRGGBBAA`.
- Contrast pairs are validated after alpha compositing over `CanvasBackground`;
  a theme failing a declared minimum ratio throws.
- `LoadOrFallback` degrades to a built-in catalog on IO or JSON failure rather
  than crashing the game.

Theme data ships as content at
`src/Hukbo.Client/Content/Themes/ui-theme-standards.json` and is linked into the
test project with `CopyToOutputDirectory`. New content the tests read must be
linked the same way, or the tests fail only at runtime.

Team mapping is fixed: Team A = Blue = faction 0, Team B = Red = faction 1.

Convey state through text or shape **in addition to** color, never color alone.

## Pointer and keyboard rules

Consumption priority, highest first: match summary → controls → event inspector
→ agent inspector → arena.

- A click consumed by UI must not click through to agent selection.
- The wheel over a panel scrolls only that panel and must not zoom the camera.
- The wheel over the arena zooms.
- A click on empty arena clears selection; selection is persistent and survives
  the selected agent's death, showing its final authoritative state.
- New events must not steal an upward scroll position; returning to the bottom
  reveals the newest events.

## Boundaries

- The Client never decides targeting, damage, retreat, or victory. It reads
  completed-tick snapshots and authoritative events.
- Presentation effects (`HitEffectSystem`) advance on unscaled presentation
  time, not ticks. Never let a visual effect gate, pause, or reorder simulation
  advancement — that includes hit-stop and knockback ideas from generic game-feel
  advice.
- Keep the draw path allocation-free; per-frame or per-row heap allocation shows
  up in the gate's `allocatedBytes`.
- The battle event feed retains at most 200 ordered events.
