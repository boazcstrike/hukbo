# Five UI Themes Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add five validated, immediately switchable, locally persisted visual
themes to Hukbo without changing its shared UI layout or deterministic
simulation.

**Architecture:** A shipped JSON catalog is parsed once into immutable typed
themes. `UiThemeManager` owns the active theme, `ClientSettingsStore` persists
only its stable ID, and existing SpriteBatch UI components consume semantic
theme values supplied by `ArenaGame`. The menu contains one keyboard- and
pointer-operable theme selector that previews and activates all five themes.

**Tech Stack:** .NET 10, C# 14, MonoGame DesktopGL 3.8.5, `System.Text.Json`,
xUnit

---

## Working-tree constraint

The Hukbo rename is currently present as a large unstaged/untracked change.
Never stage or commit existing renamed source files as part of this work. Inspect
and verify the exact theme diff, then leave source changes unstaged for the user
unless the rename is committed separately.

### Task 1: Theme contract and catalog validation

**Files:**

- Create: `src/Hukbo.Client/Theming/UiTheme.cs`
- Create: `src/Hukbo.Client/Theming/UiThemeCatalog.cs`
- Create: `src/Hukbo.Client/Content/Themes/ui-theme-standards.json`
- Modify: `src/Hukbo.Client/Hukbo.Client.csproj`
- Modify: `tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj`
- Create: `tests/Hukbo.Client.Tests/UiThemeCatalogTests.cs`

**Step 1: Write catalog contract tests**

Cover:

```csharp
[Fact]
public void BuiltInCatalogContainsExactlyFiveUniqueThemes()
{
    var catalog = UiThemeCatalog.Load(BuiltInCatalogPath);

    Assert.Equal(5, catalog.Themes.Count);
    Assert.Equal(5, catalog.Themes.Select(theme => theme.Id).Distinct().Count());
    Assert.Contains(catalog.Themes, theme => theme.Id == catalog.DefaultThemeId);
}

[Theory]
[InlineData("command")]
[InlineData("field-manual")]
[InlineData("signal")]
[InlineData("broadcast")]
[InlineData("high-contrast")]
public void EveryBuiltInThemeIsCompleteAndAccessible(string id)
{
    var catalog = UiThemeCatalog.Load(BuiltInCatalogPath);

    var errors = UiThemeCatalog.ValidateTheme(catalog.GetRequired(id));

    Assert.Empty(errors);
}
```

Also test duplicate IDs, missing semantic colors, invalid `#RRGGBBAA` values,
unsupported schema versions, and text/focus contrast failures.

**Step 2: Run the focused tests and confirm the expected failure**

Run:

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj `
  --filter FullyQualifiedName~UiThemeCatalogTests
```

Expected: compilation failure because the theming types do not exist.

**Step 3: Implement immutable runtime models**

Use typed semantic roles rather than a dictionary in draw paths:

```csharp
internal sealed record UiTheme(
    string Id,
    string DisplayName,
    UiThemeColors Colors,
    UiThemeMetrics Metrics);

internal sealed record UiThemeColors(
    Color CanvasBackground,
    Color ArenaSurface,
    Color ArenaBorder,
    Color StatusSurface,
    Color OverlayScrim,
    Color PanelSurface,
    Color PanelAlternate,
    Color PanelBorder,
    Color TextPrimary,
    Color TextSecondary,
    Color TextDisabled,
    Color TextInverse,
    Color ActionDefault,
    Color ActionHover,
    Color ActionFocus,
    Color ActionPressed,
    Color ActionActive,
    Color ActionDisabled,
    Color StatusInfo,
    Color StatusSuccess,
    Color StatusWarning,
    Color StatusDanger,
    Color TeamA,
    Color TeamB,
    Color OtherFaction,
    Color Selection,
    Color NewEvent);
```

Keep shared layout behavior intact. Metrics may vary only within the approved
visual contract, such as border thickness or shadow offset.

**Step 4: Implement catalog DTO parsing and validation**

- Deserialize with `System.Text.Json`.
- Accept only schema version `1`.
- Parse `#RRGGBB` and `#RRGGBBAA` strings explicitly.
- Reject missing/duplicate/unknown theme definitions.
- Compute sRGB relative luminance and contrast.
- Composite translucent foreground/background colors before contrast checks.
- Parse and validate once, never from `Draw`.
- Provide a compiled Command fallback for an unexpectedly invalid shipped file.

**Step 5: Add five complete definitions**

Add `command`, `field-manual`, `signal`, `broadcast`, and `high-contrast`.
Make their surfaces, borders, text hierarchy, action states, and accent colors
visibly distinct while every required contrast pair passes.

Configure the client and test outputs to copy the JSON:

```xml
<None Update="Content/Themes/ui-theme-standards.json"
      CopyToOutputDirectory="PreserveNewest" />
```

The test project should link the same source file rather than duplicate it.

**Step 6: Run focused tests**

Expected: all `UiThemeCatalogTests` pass.

### Task 2: Local settings persistence

**Files:**

- Create: `src/Hukbo.Client/Settings/ClientSettings.cs`
- Create: `src/Hukbo.Client/Settings/ClientSettingsStore.cs`
- Create: `tests/Hukbo.Client.Tests/ClientSettingsStoreTests.cs`

**Step 1: Write failing persistence tests**

Use a temporary directory injected into the store. Cover:

- missing file returns the provided default;
- valid saved ID round-trips;
- malformed JSON returns the default;
- unsupported settings schema returns the default;
- unknown theme ID is rejected by the manager;
- saving replaces the previous file;
- a failed save leaves the previous valid file and no orphan temporary file.

Representative contract:

```csharp
internal sealed record ClientSettings(int SchemaVersion, string SelectedThemeId);

internal sealed class ClientSettingsStore
{
    public ClientSettingsStore(string settingsPath);
    public ClientSettings Load(string defaultThemeId);
    public bool TrySave(string selectedThemeId);
}
```

**Step 2: Run the focused tests and confirm failure**

Run:

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj `
  --filter FullyQualifiedName~ClientSettingsStoreTests
```

Expected: compilation failure because the settings types do not exist.

**Step 3: Implement the settings store**

- Production path:
  `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hukbo", "settings.json")`.
- Persist only `schemaVersion` and `selectedThemeId`.
- Create the directory when needed.
- Write UTF-8 JSON to a sibling temporary file.
- Flush the stream before replacing the destination.
- Catch expected filesystem/JSON failures and return the safe default or `false`.
- Never allow settings failure to block client launch.

**Step 4: Run focused tests**

Expected: all `ClientSettingsStoreTests` pass.

### Task 3: Central theme manager

**Files:**

- Create: `src/Hukbo.Client/Theming/UiThemeManager.cs`
- Create: `tests/Hukbo.Client.Tests/UiThemeManagerTests.cs`

**Step 1: Write failing manager tests**

Cover:

```csharp
[Fact]
public void SelectChangesThemeImmediatelyAndPersistsStableId()
{
    var manager = CreateManager(defaultId: "command");

    var changed = manager.TrySelect("signal");

    Assert.True(changed);
    Assert.Equal("signal", manager.ActiveTheme.Id);
    Assert.Equal("signal", SavedThemeId);
}
```

Also prove that selecting the current ID does not rewrite settings and that an
unknown ID leaves the current valid theme unchanged.

**Step 2: Run the focused tests and confirm failure**

Expected: compilation failure because `UiThemeManager` does not exist.

**Step 3: Implement the manager**

The manager owns:

- the immutable catalog;
- the active immutable theme reference;
- ordered theme traversal for previous/next selection; and
- persistence after a successful explicit change.

Do not introduce events unless a consumer actually needs them. `ArenaGame` can
read `ActiveTheme` each update/draw frame.

**Step 4: Run focused tests**

Expected: all manager tests pass.

### Task 4: Theme selector UI

**Files:**

- Create: `src/Hukbo.Client/UI/UiThemeSelector.cs`
- Modify: `src/Hukbo.Client/UI/UiButton.cs`
- Modify: `src/Hukbo.Client/MenuOverlay.cs`
- Create: `tests/Hukbo.Client.Tests/UiThemeSelectorTests.cs`

**Step 1: Write failing selector behavior tests**

Test pure selection/navigation behavior separately from SpriteBatch rendering:

- five ordered names are available;
- previous/next wraps at both ends;
- Enter/Space advances while focused;
- Left/Right selects the adjacent theme;
- pointer activation selects the displayed target;
- hover never changes or persists a theme;
- selected state is represented by text/marker data.

**Step 2: Run focused tests and confirm failure**

Expected: compilation failure because the selector does not exist.

**Step 3: Implement one shared selector**

Use a compact carousel/control inside the existing overlay:

- label: `VISUAL THEME`;
- active theme display name;
- `previous` and `next` hit targets;
- `n / 5` position indicator;
- compact semantic-color swatches;
- visible focus border and selected marker;
- Left/Right and pointer support;
- Enter/Space advances.

Reduce existing menu button spacing only as required to fit the selector. The
same bounds and controls are used by every theme.

Return the stable theme ID separately from `ClientCommand`; do not create five
gameplay commands.

**Step 4: Apply the theme only after activation**

`MenuOverlay.Update` returns a UI result containing either a client command or
an explicitly selected theme ID. Hover and focus movement consume input but do
not change the active theme.

**Step 5: Run focused selector tests**

Expected: all selector tests pass.

### Task 5: Migrate rendering to semantic tokens

**Files:**

- Modify: `src/Hukbo.Client/Program.cs`
- Modify: `src/Hukbo.Client/ArenaGame.cs`
- Modify: `src/Hukbo.Client/MenuOverlay.cs`
- Modify: `src/Hukbo.Client/UI/UiButton.cs`
- Modify: `src/Hukbo.Client/UI/ControlBar.cs`
- Modify: `src/Hukbo.Client/UI/AgentInspectorPanel.cs`
- Modify: `src/Hukbo.Client/UI/BattleEventLogPanel.cs`
- Modify: `src/Hukbo.Client/UI/MatchSummaryPanel.cs`

**Step 1: Compose theming at startup**

- Resolve the shipped catalog path from `AppContext.BaseDirectory`.
- Load settings before the first draw.
- Construct `UiThemeManager` with the resolved initial ID.
- Keep the manager in `ArenaGame`; do not place it in `Hukbo.Core`.

**Step 2: Pass the active theme through draw paths**

Prefer explicit parameters:

```csharp
public void Draw(
    SpriteBatch spriteBatch,
    Texture2D pixel,
    SpriteFont font,
    UiTheme theme);
```

Do not use global mutable theme state.

**Step 3: Replace UI palette constants**

Migrate every UI-owned color in:

- canvas and map background/border;
- status bar and diagnostic text;
- menu overlay and helper text;
- buttons and all interaction states;
- control bar;
- inspector;
- event log;
- match summary.

Keep `PawnRenderer` and `PawnAppearanceFactory` colors unchanged. Faction color
roles used by UI chrome may be themed; pawn appearance remains stable.

**Step 4: Wire explicit selection**

When the menu returns a theme ID:

- call `UiThemeManager.TrySelect`;
- leave the menu open so the user sees the live result;
- do not reset or advance simulation;
- update the selected marker immediately.

**Step 5: Audit remaining raw UI colors**

Run:

```powershell
rg -n "new Color|Color\\." `
  src/Hukbo.Client/ArenaGame.cs `
  src/Hukbo.Client/MenuOverlay.cs `
  src/Hukbo.Client/UI
```

Expected: only intentional primitives/fallback construction remain; all
component palette choices come from `UiTheme`.

### Task 6: Focused and integration verification

**Files:**

- Modify only files required to correct verified failures.

**Step 1: Run client tests**

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj
```

Expected: pass.

**Step 2: Run the solution build**

```powershell
dotnet build Hukbo.slnx
```

Expected: pass with no new warnings.

**Step 3: Run broader tests when targeted checks pass**

```powershell
dotnet test Hukbo.slnx
```

Expected: pass.

**Step 4: Perform runtime visual verification**

- Launch Hukbo.
- Pause at a fixed seed/tick.
- Switch through all five themes using keyboard and pointer.
- Confirm every surface changes on the next frame.
- Confirm the same layout, controls, simulation tick, agents, and outcome remain.
- Restart and confirm the last theme is restored.
- Replace settings with malformed JSON and confirm Command loads safely.
- Capture one screenshot for every theme at the same seed/tick.

**Step 5: Inspect the complete diff**

- Confirm no `Hukbo.Core`, headless, pawn-renderer, or pawn-appearance behavior
  changed.
- Confirm no unrelated rename files were staged.
- Confirm no temporary files or debug code remain.
- Classify any failures as implementation, test, environment, pre-existing, or
  unrelated before changing code.

### Task 7: Independent review

Ask a read-only reviewer to inspect:

- semantic token completeness;
- contrast and alpha-compositing correctness;
- settings replacement/fallback safety;
- keyboard and pointer behavior;
- separation from deterministic simulation;
- final diff scope.

Resolve all Critical and High findings. Re-run focused tests after every fix and
the full relevant checks before completion.
