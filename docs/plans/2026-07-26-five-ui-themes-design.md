# Five UI Themes Design

## Goal

Give Hukbo five visibly distinct, immediately switchable UI themes while every
theme retains the same layout, controls, information hierarchy, and gameplay
behavior. Persist the selected theme as a local user preference without allowing
presentation choices to affect deterministic simulation state.

## Approved scope

- Exactly five built-in themes.
- One shared layout and control model.
- Live switching from the existing menu by keyboard or pointer.
- Automatic restoration of the selected theme on the next launch.
- Theme configuration and user settings remain inside `Hukbo.Client`.
- Pawn artwork, combat rules, simulation hashes, seeds, replays, and headless
  output remain unchanged.

The initial themes are:

1. **Command** — the existing navy tactical treatment, retained as the safe
   default.
2. **Field Manual** — warm parchment, olive, charcoal, and stamped-document
   contrast.
3. **Signal** — near-black instrumentation with cyan and amber signal accents.
4. **Broadcast** — bright neutral surfaces with crisp blue/red sports-broadcast
   accents.
5. **High Contrast** — black, white, and yellow with the strongest focus and
   boundary treatment.

These names and values are configuration data, not separate UI implementations.

## Architecture

Hukbo will ship one versioned JSON theme catalog. The catalog defines shared
standards and five complete semantic theme definitions. A typed loader parses and
validates that catalog once during client startup, producing immutable
`UiTheme` objects. Rendering code receives the active `UiTheme`; it never reads
JSON or performs token lookup during a draw call.

`UiThemeManager` owns the active immutable theme reference. It resolves stable
theme IDs, applies an explicit user selection immediately, and asks the settings
store to persist the selected ID. Theme selection is presentation state and is
not represented as a simulation command.

`ClientSettingsStore` reads and writes a small versioned JSON document in
`%LOCALAPPDATA%\Hukbo\settings.json`. The document contains only the selected
theme ID. Writes use a sibling temporary file followed by replacement so a
failed write cannot corrupt the last valid settings file.

## Theme contract

The shared standards section defines:

- catalog schema version and default theme ID;
- required theme count;
- shared layout metrics already used by the UI;
- required semantic color roles;
- required interaction states;
- contrast pairs and minimum ratios; and
- allowed font asset IDs and text scales.

Each complete theme supplies semantic roles rather than component-specific
colors:

- canvas, arena surface, arena border, status surface;
- overlay scrim, panel surface, panel alternate surface, panel border;
- primary, secondary, disabled, and inverse text;
- default, hover, focus, pressed, active, and disabled actions;
- information, success, warning, and danger statuses;
- team A, team B, other-faction, selection, and new-event accents; and
- border thickness and supported shadow treatment.

Version one does not support inheritance, user-authored themes, network
downloads, per-theme layouts, or runtime hot reload. Five complete definitions
are easier to validate and safer to fall back from.

## Components and integration

- `UiThemeCatalog` loads, validates, and exposes exactly five themes by stable
  ID.
- `UiThemeManager` exposes the active theme and changes it after explicit
  selection.
- `ClientSettingsStore` loads and atomically saves the selected theme ID.
- `ArenaGame` composes the catalog, manager, settings store, and UI consumers.
- `MenuOverlay` presents a five-choice theme selector while retaining its
  existing navigation and activation behavior.
- `UiButton`, `ControlBar`, `AgentInspectorPanel`, `BattleEventLogPanel`, and
  `MatchSummaryPanel` consume semantic theme values.
- Arena background, map surface, map border, status bar, UI faction accents, and
  status text move behind the same theme contract.
- `PawnRenderer` and `PawnAppearanceFactory` remain outside the theme boundary.

The selected item is communicated by text and a visible outline or marker, not
by color alone.

## Data flow

1. The client loads and validates the shipped catalog.
2. The settings store loads the saved stable theme ID.
3. The manager resolves that ID or selects `command` as the safe default.
4. `ArenaGame` passes the active immutable theme to every themed draw path.
5. The user focuses and activates a theme choice in the existing menu.
6. The manager swaps the active reference immediately.
7. The settings store atomically saves the selected ID.
8. The next frame redraws all themed surfaces with the new values.

Hovering a theme choice does not apply or save it. Theme switching does not
reset, pause, advance, or otherwise modify the simulation.

## Validation and failure handling

Startup validation rejects duplicate IDs, an invalid default ID, a theme count
other than five, missing semantic roles, invalid color values, unsupported
schema versions, incomplete interaction states, and contrast pairs below the
configured threshold.

The shipped catalog is part of the application and is verified by tests and the
build. If it is unexpectedly invalid at runtime, the client uses a compiled
equivalent of the Command theme so launch remains possible.

Missing, malformed, unreadable, or unknown user settings never prevent launch.
They select the default theme. A failed settings save leaves the theme active
for the current session and preserves the previous valid file.

## Accessibility baseline

Every theme, not only High Contrast, must provide:

- at least 4.5:1 contrast for normal text;
- at least 3:1 contrast for large text and meaningful component boundaries;
- a clearly visible keyboard focus indicator;
- keyboard and pointer access to every theme choice;
- interaction states that do not rely on color alone; and
- faction and selection cues reinforced by labels, outlines, or geometry.

Contrast tests use composited colors for translucent surfaces.

This is a visual and input baseline. The current SpriteBatch UI does not expose
assistive-technology semantics, so this work does not claim complete WCAG
conformance.

## Testing

- Catalog tests prove exactly five unique, complete themes and a valid default.
- Validation tests cover missing roles, malformed colors, invalid schema
  versions, duplicate IDs, and failed contrast pairs.
- Settings tests cover missing and malformed files, unknown IDs, round trips,
  atomic replacement, and deterministic fallback without writing to the real
  user profile.
- Manager tests prove immediate switching and persistence only after explicit
  activation.
- Menu tests cover keyboard and pointer selection and visible selected state.
- A source audit confirms UI-owned raw palette values have moved behind the
  theme boundary while pawn-art colors remain unchanged.
- Client tests and the solution build must pass.
- Visual verification captures the same seed and simulation tick in all five
  themes and confirms that switching or restarting does not affect simulation
  state.

## Risks

- Partial color migration could produce mixed-theme screens.
- Alpha compositing can invalidate otherwise acceptable contrast values.
- Adding per-theme fonts would expand the content-pipeline and packaging risk;
  version one keeps the existing packaged font and shared typography metrics.
- The current working tree contains an in-progress product rename, so theme work
  must not stage, overwrite, or duplicate unrelated rename changes.

## Acceptance criteria

- Exactly five built-in visual themes load from one versioned catalog.
- All themes share the current layout, controls, and font asset.
- Every themed surface updates on the next frame after explicit selection.
- Keyboard and pointer selection both work and visibly identify the active
  choice.
- The selected theme survives restart.
- Invalid settings fall back safely to Command.
- All five themes pass completeness, state, and contrast validation.
- Theme switching cannot affect deterministic simulation behavior.
- Focused tests and `dotnet build Hukbo.slnx` pass.
- The final diff contains no unrelated changes.
