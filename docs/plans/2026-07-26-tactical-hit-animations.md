# Tactical Hit Animations Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add short tactical hit pulses and deterministic procedural impact bursts to Hukbo without changing simulation behavior.

**Architecture:** Keep all new state in the Client presentation layer. `PresentationCoordinator` owns a fixed-capacity `HitEffectSystem`, ingests every completed tick beside the event feed, advances effects using unscaled presentation time, and clears them on both reset commands. Pure rendering geometry derives rings and shards from stable event data; `ArenaGame` draws the effects inside an arena-only scissor batch and passes pulse strength to living pawns.

**Tech Stack:** C# 14, .NET 10, MonoGame DesktopGL 3.8.5, xUnit 2.9.3, PowerShell repository scripts.

---

## Guardrails and success criteria

- Use `Hukbo`/`Hukbo.*` names only.
- Do not modify `src/Hukbo.Core/**` or `tests/Hukbo.Core.Tests/**`.
- Do not add camera shake, hit-stop, audio, damage numbers, knockback,
  weapon-specific effects, content assets, or authoritative randomness.
- Do not use `System.Random`, wall-clock time, or per-shard heap objects.
- Do not stage unrelated rename or spectator-UI work. Every `git add` below must
  use the exact listed paths.
- One aggregated `Damage` event creates one effect. `Attack` and `Death` events
  do not create additional effects.
- A same-batch `Death` marks the corresponding damage effect lethal.
- Every tick processed in a multi-tick update is ingested.
- Effects expire, remain capacity-bounded, and clear on Next Round and Full
  Reset.
- Focused tests, Release build, formatting, and `scripts/verify.ps1` pass before
  handoff.

### Task 1: Add the fixed-capacity hit-effect model and lifecycle

**Files:**
- Create: `src/Hukbo.Client/Presentation/HitEffect.cs`
- Create: `src/Hukbo.Client/Presentation/HitEffectSystem.cs`
- Create: `tests/Hukbo.Client.Tests/HitEffectSystemTests.cs`

**Step 1: Write the failing ingestion tests**

Create tests with these exact behaviors:

```csharp
[Fact]
public void Ingest_CreatesOneEffectOnlyForEachDamageEvent()
{
    var system = new HitEffectSystem(capacity: 8);
    AgentView[] agents = [Agent(7, xRaw: 1200, yRaw: 3400, isAlive: true)];
    BattleEvent[] events =
    [
        Event(1, BattleEventKind.Attack, source: 2, target: 7),
        Event(2, BattleEventKind.Damage, source: 7, target: 7, value: 18),
        Event(3, BattleEventKind.Move, source: 7, target: null),
    ];

    system.Ingest(events, agents);

    var effect = Assert.Single(system.ActiveEffects.ToArray());
    Assert.Equal(2, effect.Sequence);
    Assert.Equal(7UL, effect.TargetEntityId);
    Assert.Equal(1200, effect.XRaw);
    Assert.Equal(3400, effect.YRaw);
    Assert.Equal(18, effect.Damage);
    Assert.False(effect.IsLethal);
}

[Fact]
public void Ingest_ClassifiesLethalDamageAndCapturesDeadAgentPosition()
{
    var system = new HitEffectSystem(capacity: 8);
    AgentView[] agents = [Agent(7, 1200, 3400, isAlive: false)];

    system.Ingest(
    [
        Event(10, BattleEventKind.Damage, 7, 7, value: 40),
        Event(11, BattleEventKind.Death, 7, null),
    ], agents);

    var effect = Assert.Single(system.ActiveEffects.ToArray());
    Assert.True(effect.IsLethal);
    Assert.Equal((1200, 3400), (effect.XRaw, effect.YRaw));
}

[Fact]
public void Ingest_ConsecutiveTickBatchesRetainsEveryDamage()
{
    var system = new HitEffectSystem(capacity: 8);
    AgentView[] agents = [Agent(7, 100, 200, true), Agent(8, 300, 400, true)];

    system.Ingest([Event(1, BattleEventKind.Damage, 7, 7, 5)], agents);
    system.Ingest([Event(2, BattleEventKind.Damage, 8, 8, 6)], agents);

    Assert.Equal([1L, 2L], system.ActiveEffects.ToArray().Select(x => x.Sequence));
}
```

Use private `Agent(...)` and `Event(...)` helpers that construct current
`AgentView` and `BattleEvent` records. Add null-argument and non-positive
capacity tests.

**Step 2: Run the focused tests and verify they fail**

Run:

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release `
  --filter FullyQualifiedName~HitEffectSystemTests
```

Expected: FAIL because `HitEffect` and `HitEffectSystem` do not exist.

**Step 3: Implement the minimal model and ingestion**

Use this public surface:

```csharp
internal readonly record struct HitEffect(
    long Sequence,
    ulong TargetEntityId,
    int XRaw,
    int YRaw,
    int Damage,
    bool IsLethal,
    float AgeSeconds)
{
    public float LifetimeSeconds => IsLethal ? 0.28f : 0.18f;
}

internal sealed class HitEffectSystem
{
    private readonly HitEffect[] _effects;
    private int _count;

    public HitEffectSystem(int capacity);
    public ReadOnlySpan<HitEffect> ActiveEffects => _effects.AsSpan(0, _count);
    public void Ingest(
        IReadOnlyList<BattleEvent> events,
        IReadOnlyList<AgentView> agents);
    public void Advance(float elapsedSeconds);
    public float GetPulseStrength(ulong entityId);
    public void Clear();
}
```

In `Ingest`, first scan the batch for `Death` source IDs. Then scan only
`Damage` events, resolve `TargetEntityId` against the post-tick agent views,
capture raw position, and append exactly one effect. Do not create an effect if
the target ID or view is missing. When full, replace the effect with the
greatest age; break equal-age ties by the lowest sequence.

**Step 4: Add failing lifecycle tests**

Add:

- `Advance_ExpiresOrdinaryAt180Milliseconds`
- `Advance_ExpiresLethalAt280Milliseconds`
- `Advance_RejectsNegativeOrNonFiniteElapsedTime`
- `Ingest_WhenFull_ReplacesOldestEffect`
- `GetPulseStrength_ReturnsPositiveOnlyForLivingHitWindow`
- `Clear_RemovesEffectsAndAllowsRestartedSequences`

Use `Advance(0.179f)`/`Advance(0.001f)` and
`Advance(0.279f)`/`Advance(0.001f)` boundaries. Pulse strength must be zero for
lethal effects and after ordinary expiry.

**Step 5: Implement lifecycle behavior and rerun**

Advance ages in place, compact unexpired values without allocation, and compute
ordinary pulse as:

```csharp
const float PulseSeconds = 0.09f;
var strength = Math.Clamp(1f - (effect.AgeSeconds / PulseSeconds), 0f, 1f);
```

For multiple effects on one entity, return the maximum current strength.

Run the focused command from Step 2.

Expected: PASS for all `HitEffectSystemTests`.

**Step 6: Commit**

```powershell
git add -- `
  src/Hukbo.Client/Presentation/HitEffect.cs `
  src/Hukbo.Client/Presentation/HitEffectSystem.cs `
  tests/Hukbo.Client.Tests/HitEffectSystemTests.cs
git commit -m "feat(presentation): add tactical hit effect lifecycle"
```

### Task 2: Make presentation coordination own ingestion and reset

**Files:**
- Modify: `src/Hukbo.Client/Presentation/PresentationCoordinator.cs:5-54`
- Modify: `tests/Hukbo.Client.Tests/PresentationCoordinatorTests.cs:1-108`

**Step 1: Write failing coordinator tests**

Extend the existing reset theory to seed one hit and assert
`coordinator.HitEffects.ActiveEffects` is empty after both Next Round and Full
Reset. Add a consecutive-batch test:

```csharp
[Fact]
public void IngestTick_ForwardsEveryBatchToFeedAndHitEffects()
{
    var coordinator = new PresentationCoordinator(
        eventCapacity: 5,
        hitEffectCapacity: 5);
    AgentView[] agents = [CreateAgent(1)];

    coordinator.IngestTick([DamageEvent(1, 1)], agents);
    coordinator.IngestTick([DamageEvent(2, 1)], agents);

    Assert.Equal(2, coordinator.EventFeed.Entries.Count);
    Assert.Equal(2, coordinator.HitEffects.ActiveEffects.Length);
}
```

**Step 2: Run tests and verify failure**

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release `
  --filter FullyQualifiedName~PresentationCoordinatorTests
```

Expected: FAIL because the new coordinator API does not exist.

**Step 3: Implement the minimal coordinator API**

```csharp
public PresentationCoordinator(int eventCapacity, int hitEffectCapacity = 256)
{
    EventFeed = new BattleEventFeed(eventCapacity);
    HitEffects = new HitEffectSystem(hitEffectCapacity);
}

public HitEffectSystem HitEffects { get; }

public void IngestTick(
    IReadOnlyList<BattleEvent> events,
    IReadOnlyList<AgentView> agents)
{
    EventFeed.Ingest(events);
    HitEffects.Ingest(events, agents);
}

public void AdvanceEffects(float elapsedSeconds) =>
    HitEffects.Advance(elapsedSeconds);
```

Add `HitEffects.Clear()` to `ResetFor`.

**Step 4: Rerun and commit**

Expected: all `PresentationCoordinatorTests` PASS.

```powershell
git add -- `
  src/Hukbo.Client/Presentation/PresentationCoordinator.cs `
  tests/Hukbo.Client.Tests/PresentationCoordinatorTests.cs
git commit -m "feat(presentation): coordinate hit effects per tick"
```

### Task 3: Add deterministic GPU-independent impact geometry

**Files:**
- Create: `src/Hukbo.Client/Rendering/HitEffectGeometry.cs`
- Create: `tests/Hukbo.Client.Tests/HitEffectGeometryTests.cs`

**Step 1: Write failing geometry tests**

Cover:

- repeated calls for the same effect return identical ring and shard geometry;
- changing sequence or target ID changes the starting angle;
- ordinary effects have 1 ring and 4–6 shards;
- lethal effects have 2 rings and exactly 8 longer shards;
- scale is monotonic and clamped at camera zoom 0.05 and 12;
- low detail suppresses secondary ordinary shards but never the primary ring.

Use an API shaped like:

```csharp
var layout = HitEffectGeometry.Create(effect, cameraZoom: 1f);
Assert.InRange(layout.ShardCount, 4, 6);
Assert.Equal(layout, HitEffectGeometry.Create(effect, 1f));
Assert.Equal(8, HitEffectGeometry.Create(effect with { IsLethal = true }, 1f).ShardCount);
```

**Step 2: Run tests and verify failure**

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release `
  --filter FullyQualifiedName~HitEffectGeometryTests
```

Expected: FAIL because `HitEffectGeometry` does not exist.

**Step 3: Implement deterministic geometry**

Create value-only records for layout and shard segments. Derive a stable seed
from `Sequence` and `TargetEntityId` with a private integer mixer; never use
`System.Random`. Use:

- ordinary: one ring, `4 + seed % 3` shards;
- lethal: two rings, eight shards;
- ordinary travel/lifetime smaller than lethal;
- angle `baseAngle + (Tau * index / shardCount)`;
- normalized progress `AgeSeconds / LifetimeSeconds`;
- clamped apparent scale, following `PawnGeometry`'s zoom policy;
- alpha and pulse values clamped to `[0, 1]`.

Keep geometry in screen-relative offsets. The renderer will add the camera-
converted captured position.

**Step 4: Rerun and commit**

Expected: all `HitEffectGeometryTests` PASS.

```powershell
git add -- `
  src/Hukbo.Client/Rendering/HitEffectGeometry.cs `
  tests/Hukbo.Client.Tests/HitEffectGeometryTests.cs
git commit -m "feat(rendering): add deterministic impact geometry"
```

### Task 4: Render pulses, rings, and shards procedurally

**Files:**
- Create: `src/Hukbo.Client/Rendering/HitEffectRenderer.cs`
- Modify: `src/Hukbo.Client/Rendering/PawnRenderer.cs:38-121`
- Modify: `src/Hukbo.Client/ArenaGame.cs:38-68`
- Modify: `src/Hukbo.Client/ArenaGame.cs:98-228`
- Modify: `src/Hukbo.Client/ArenaGame.cs:346-488`

**Step 1: Add the renderer**

`HitEffectRenderer.Draw` accepts `ReadOnlySpan<HitEffect>`, the camera, arena
bounds, zoom, sprite batch, and existing white pixel. For each effect:

1. Convert captured raw coordinates through `FixedPoint.Scale` and
   `SpectatorCamera.WorldToScreen`.
2. Ask `HitEffectGeometry` for layout.
3. Draw a thin warm-white expanding ring; draw a second offset ring only for
   lethal hits.
4. Draw deterministic radial shard line segments.
5. Fade every primitive from geometry age.

Reuse a local line helper equivalent to `PawnRenderer.DrawLine`; do not add
textures or content-pipeline files.

**Step 2: Add a pulse parameter to pawn rendering**

Extend `PawnRenderer.Draw` with `float hitPulseStrength = 0f`, validate it is
finite and in `[0, 1]`, and blend clothing, accent, skin, head treatment, and
faction colors toward a restrained warm white before drawing:

```csharp
private static Color ApplyHitPulse(Color color, float strength) =>
    Color.Lerp(color, new Color(255, 244, 214), strength * 0.55f);
```

Do not mutate `PawnAppearance`; compute displayed colors per draw.

**Step 3: Integrate unscaled timing and per-tick ingestion**

At the start of the regular presentation update, before
`AdvanceSimulation(...)`, call:

```csharp
_presentation.AdvanceEffects(
    (float)gameTime.ElapsedGameTime.TotalSeconds);
```

Inside the existing `while` loop, immediately after `AdvanceOneTick()`, replace
the direct event-feed call with:

```csharp
_presentation.IngestTick(
    _simulation.LastEvents,
    _simulation.Agents);
```

This call must remain inside the loop. Do not multiply effect elapsed time by
`_speedMultiplier`.

**Step 4: Integrate clipped drawing**

Split the current single SpriteBatch into:

- an arena batch using a reusable `RasterizerState` with
  `ScissorTestEnable = true` and `GraphicsDevice.ScissorRectangle =
  layout.ArenaBounds`;
- a normal UI batch after the arena batch ends.

Dispose the reusable rasterizer state in `UnloadContent`.

In `DrawArena`, pass
`_presentation.HitEffects.GetPulseStrength(agent.EntityId)` to each living
pawn. After the pawn loop, call `HitEffectRenderer.Draw` so bursts remain
visible at captured positions after lethal targets disappear.

**Step 5: Run focused tests and build**

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release
dotnet build src/Hukbo.Client/Hukbo.Client.csproj -c Release --no-restore
```

Expected: all Client tests PASS; build succeeds with 0 warnings and 0 errors.

**Step 6: Commit**

```powershell
git add -- `
  src/Hukbo.Client/Rendering/HitEffectRenderer.cs `
  src/Hukbo.Client/Rendering/PawnRenderer.cs `
  src/Hukbo.Client/ArenaGame.cs
git commit -m "feat(client): render tactical hit animations"
```

### Task 5: Run integration verification and review the final diff

**Files:**
- Review only: all files changed in Tasks 1–4

**Step 1: Run narrow verification**

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release `
  --filter "FullyQualifiedName~HitEffectSystemTests|FullyQualifiedName~HitEffectGeometryTests|FullyQualifiedName~PresentationCoordinatorTests"
dotnet format Hukbo.slnx --verify-no-changes
dotnet build Hukbo.slnx -c Release --no-restore
```

Expected: focused tests PASS; formatting reports no changes; build succeeds with
0 warnings and 0 errors.

**Step 2: Run the canonical repository gate**

```powershell
./scripts/verify.ps1
```

Expected: prerequisite/locked restore, formatting, Release build, both test
projects, and deterministic 200-agent seed-1 workload PASS, ending with:

```text
[PASS] Canonical repository verification completed.
```

If a check fails, classify it before editing. Use @systematic-debugging after a
repeated failure, change only the demonstrated cause, and rerun the narrowest
affected check.

**Step 3: Review the complete diff**

```powershell
git diff --check
git status --short
git diff -- `
  src/Hukbo.Client/Presentation/HitEffect.cs `
  src/Hukbo.Client/Presentation/HitEffectSystem.cs `
  src/Hukbo.Client/Presentation/PresentationCoordinator.cs `
  src/Hukbo.Client/Rendering/HitEffectGeometry.cs `
  src/Hukbo.Client/Rendering/HitEffectRenderer.cs `
  src/Hukbo.Client/Rendering/PawnRenderer.cs `
  src/Hukbo.Client/ArenaGame.cs `
  tests/Hukbo.Client.Tests/HitEffectSystemTests.cs `
  tests/Hukbo.Client.Tests/HitEffectGeometryTests.cs `
  tests/Hukbo.Client.Tests/PresentationCoordinatorTests.cs
```

Expected: no whitespace errors; no Core, asset, package, or unrelated changes;
no `System.Random`; no debug code; every changed line supports this feature.
Resolve all Critical and High review findings before handoff.

### Task 6: Hand the build to the user for manual smoke testing

**Files:**
- No file changes required

**Step 1: Launch the source build**

```powershell
./scripts/run.ps1
```

**Step 2: Give the user this checklist**

- At 1x, ordinary hits show a brief pawn pulse, one thin ring, and a restrained
  4–6 shard burst.
- At 4x, hits from consecutive simulation ticks remain visible rather than only
  the final tick in each frame.
- Lethal hits show a larger double ring and eight longer shards after the pawn
  disappears.
- At fitted, minimum, and maximum zoom, the primary ring remains readable;
  low-detail suppression reduces clutter without removing the ring.
- In crowded exchanges, effects remain bounded and do not cause persistent
  trails or colors.
- Pause allows existing effects to finish while simulation ticks stop; resume
  creates new effects normally.
- Next Round (`R`) and Full Reset (`Shift+R`) immediately clear every active
  pulse and burst.
- Resize and zoom near each arena edge; no ring or shard draws into the status,
  inspector, event log, summary, or menu UI.
- Reach a terminal result and confirm effects expire normally without changing
  outcome, tick count, state hash, or event hash.

Do not claim this manual pass succeeded. Hand over the exact commit, automated
verification result, launch command, and checklist so the user can record
PASS/FAIL observations.
