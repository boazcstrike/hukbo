# Pawn Character Visuals Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace arena dots with original zoom-aware procedural pawns and matching inspector portraits for five deterministic cosmetic weapon roles.

**Architecture:** A pure presentation factory derives immutable appearance data from `EntityId`. Pure geometry computes zoom detail, body parts, and complete bounds from a foot anchor. One allocation-free MonoGame renderer consumes that data in both the arena and inspector without changing Core simulation or content assets.

**Tech Stack:** .NET 10, C# 14, MonoGame DesktopGL, xUnit, FluentAssertions

---

### Task 1: Deterministic cosmetic appearance

**Files:**
- Create: `src/Hukbo.Client/Presentation/PawnAppearance.cs`
- Create: `src/Hukbo.Client/Presentation/PawnAppearanceFactory.cs`
- Create: `tests/Hukbo.Client.Tests/PawnAppearanceFactoryTests.cs`

**Step 1: Write failing tests**

Cover:

- identical `EntityId` values produce identical descriptors;
- IDs `0..4` reach all five roles;
- every descriptor uses one allowed stature and build multiplier;
- the player-facing labels are exactly `Bangkaw - Long Spear`,
  `Hardened Javelin`, `Busog - War Bow`, `Broad Dagger`, and `Great Blade`;
- no label claims a definitive kampilan.

Use an internal `enum PawnWeaponRole` and immutable `readonly record struct
PawnAppearance`. Keep colors as stable presentation palette indices or MonoGame
`Color` values; do not add appearance to `AgentView`.

**Step 2: Verify failure**

Run:

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj --filter FullyQualifiedName~PawnAppearanceFactoryTests
```

Expected: compilation fails because the appearance types do not exist.

**Step 3: Implement the smallest pure factory**

Map weapon role from `entityId % 5`. Derive independent stature, build,
head-treatment, clothing, and material variants using integer mixing of
`EntityId`; do not construct `Random` or read simulation state.

Representative API:

```csharp
internal static class PawnAppearanceFactory
{
    public static PawnAppearance Create(ulong entityId);
}
```

**Step 4: Verify and commit**

Run the focused test command and expect PASS.

Commit:

```powershell
git add src/Hukbo.Client/Presentation/PawnAppearance.cs src/Hukbo.Client/Presentation/PawnAppearanceFactory.cs tests/Hukbo.Client.Tests/PawnAppearanceFactoryTests.cs
git commit -m "feat(ui): add deterministic pawn appearances"
```

### Task 2: Zoom policy and pure pawn geometry

**Files:**
- Create: `src/Hukbo.Client/Rendering/PawnGeometry.cs`
- Create: `tests/Hukbo.Client.Tests/PawnGeometryTests.cs`

**Step 1: Write failing tests**

Prove:

- apparent size is monotonic and clamped across camera zoom `0.05..12`;
- low, medium, and high detail tiers occur in order;
- the foot anchor is unchanged by stature/build;
- head scale stays stable while torso height/width changes;
- every weapon role extends beyond the torso;
- complete bounds contain ring, head, body, weapon, and selection padding.

Keep geometry GPU-independent by returning rectangles and endpoints expressed
with MonoGame value types only. Representative API:

```csharp
internal static class PawnGeometry
{
    public static PawnLayout Create(
        Vector2 footAnchor,
        float cameraZoom,
        PawnAppearance appearance,
        float scaleMultiplier = 1f);
}
```

**Step 2: Verify failure**

Run:

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj --filter FullyQualifiedName~PawnGeometryTests
```

Expected: compilation fails because geometry types do not exist.

**Step 3: Implement geometry**

Use a readable screen-space clamp and explicit detail thresholds. Return all
part rectangles/endpoints in a `readonly record struct PawnLayout`; compute
`VisualBounds` once without collections or heap allocations.

**Step 4: Verify and commit**

Run both pawn test classes and expect PASS.

Commit:

```powershell
git add src/Hukbo.Client/Rendering/PawnGeometry.cs tests/Hukbo.Client.Tests/PawnGeometryTests.cs
git commit -m "feat(ui): define zoom-aware pawn geometry"
```

### Task 3: Shared procedural renderer

**Files:**
- Create: `src/Hukbo.Client/Rendering/PawnRenderer.cs`
- Modify: `src/Hukbo.Client/UI/UiButton.cs` only if an existing primitive can
  be generalized without changing button behavior

**Step 1: Implement renderer over tested geometry**

Representative API:

```csharp
internal static class PawnRenderer
{
    public static Rectangle GetBounds(
        Vector2 footAnchor,
        float cameraZoom,
        PawnAppearance appearance,
        float scaleMultiplier = 1f);

    public static void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Vector2 footAnchor,
        float cameraZoom,
        PawnAppearance appearance,
        Color factionColor,
        PawnVisualState state,
        float scaleMultiplier = 1f);
}
```

Use rotated/scaled pixel rectangles for shafts and blades, layered rectangles
for torso/head/headcloth, and outline/base shapes for faction and selection.
Implement all five weapon silhouettes. Low detail omits secondary gear; medium
adds bundle/quiver/head treatment; high adds restrained material accents.

Do not allocate collections, format strings, cache per-frame objects, or create
textures in `Draw`.

**Step 2: Compile**

Run:

```powershell
dotnet build src/Hukbo.Client/Hukbo.Client.csproj --configuration Debug
```

Expected: build succeeds with zero warnings.

**Step 3: Commit**

```powershell
git add src/Hukbo.Client/Rendering/PawnRenderer.cs
git commit -m "feat(ui): render procedural weapon pawns"
```

### Task 4: Replace arena dots

**Files:**
- Modify: `src/Hukbo.Client/ArenaGame.cs`
- Test: `tests/Hukbo.Client.Tests/PawnAppearanceFactoryTests.cs`

**Step 1: Add or extend a failing integration-boundary test**

Prove the appearance requested for an entity is independent of draw context
and stable across repeated lookups/reset-equivalent calls.

**Step 2: Integrate `DrawArena`**

For every living agent:

1. compute the foot anchor with `WorldToScreen`;
2. create the stable appearance from `EntityId`;
3. obtain full weapon-inclusive bounds;
4. cull against `arenaBounds` using those bounds;
5. draw via `PawnRenderer` with normal, hovered, or selected state.

Delete dot-size/destination rendering. Preserve alive filtering, draw order,
selection IDs, hover IDs, camera behavior, and agent picking.

**Step 3: Verify**

Run:

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj
dotnet build src/Hukbo.Client/Hukbo.Client.csproj --configuration Release
```

Expected: both commands pass.

**Step 4: Commit**

```powershell
git add src/Hukbo.Client/ArenaGame.cs tests/Hukbo.Client.Tests/PawnAppearanceFactoryTests.cs
git commit -m "feat(ui): replace arena dots with pawns"
```

### Task 5: Matching inspector portrait and final verification

**Files:**
- Modify: `src/Hukbo.Client/UI/AgentInspectorPanel.cs`
- Modify: `src/Hukbo.Client/ArenaGame.cs` if inspector bounds require a small
  height adjustment
- Create: `tests/Hukbo.Client.Tests/AgentInspectorLayoutTests.cs` only if layout
  math is extracted as a pure helper

**Step 1: Add portrait layout**

Reserve a 48-56 pixel portrait frame below the inspector heading. Derive the
same `PawnAppearance` from selected `EntityId` and call `PawnRenderer` at a
fixed portrait scale. Keep all existing authoritative fields legible and add
`Visual role: <label>`. For dead selections, retain the matching appearance and
apply a clear subdued/marked visual state while preserving `DEAD` text.

Do not change inspector pointer consumption or selection lifetime.

**Step 2: Focused verification**

Run:

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj
```

Expected: all Client tests pass.

**Step 3: Repository verification**

Run:

```powershell
pwsh -NoLogo -NoProfile -File scripts/verify.ps1
```

Expected: formatting, build, and tests pass. Classify any failure as
implementation, environment, pre-existing, or unrelated before editing.

**Step 4: Manual visual smoke**

Run Hukbo and verify at 1280x720 and a resized window:

- fitted, minimum, medium, and maximum zoom;
- all five weapon roles are distinguishable at readable zoom;
- dense fights retain faction readability;
- hover/selection frames contain long weapons;
- arena pawn and inspector portrait match;
- selected dead agents retain the same portrait with a dead treatment;
- reset preserves appearance assignments;
- pan, play/pause, event-log scrolling, and empty-click clearing still work.

Record observed resolutions and any limitation. Do not claim the smoke passed if
the game could not be launched.

**Step 5: Final diff and commit**

Confirm `src/Hukbo.Core/**`, `StateHasher`, `Content.mgcb`, and unrelated dirty
files are untouched by this feature.

Commit only immediate implementation files:

```powershell
git add src/Hukbo.Client/UI/AgentInspectorPanel.cs src/Hukbo.Client/ArenaGame.cs tests/Hukbo.Client.Tests/AgentInspectorLayoutTests.cs
git commit -m "feat(ui): add matching pawn portraits"
```
