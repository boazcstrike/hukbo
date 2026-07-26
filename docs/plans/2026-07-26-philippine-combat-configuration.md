# Philippine Combat Configuration Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a deterministic, end-to-end pre-colonial Philippine combat configuration vertical slice with authoritative loadouts, weighted hit locations, explainable events, hashes, and matching spectator presentation.

**Architecture:** Add an immutable, versioned combat-ruleset registry to Hukbo.Core and store only a stable preset ID on `Scenario`. Assign deterministic loadouts at spawn, resolve one stateless weighted body-part target for each accepted attack, and carry the weapon and location through authoritative events and hashes. Keep scalar damage and HP unchanged, then make the Client consume authoritative weapon identities instead of deriving visual roles independently.

**Tech Stack:** C# 14, .NET 10, MonoGame DesktopGL 3.8.5, xUnit 2.9.3, PowerShell repository scripts.

---

## Guardrails and success criteria

- Use `Hukbo`, `hukbo`, and `Hukbo.*` names only.
- Treat `docs/plans/2026-07-26-philippine-combat-configuration-design.md`
  as the approved design contract.
- Do not implement terrain, naval combat, directional defense, per-part health,
  wounds, damage multipliers, persistence, a setup UI, or a European ruleset.
- Preserve scalar `DamagePerAttack`, aggregate HP, cooldown, simultaneous
  damage, death, and victory behavior.
- Do not use `System.Random`, wall-clock time, dictionary iteration order, or a
  mutable global RNG for hit locations.
- Do not let Client-only appearance state influence Core.
- Do not stage unrelated rename, spectator, or tactical-hit-effect changes.
- A preset content or ordering change requires a preset version change.
- All Critical and High review findings must be resolved before handoff.

### Task 1: Add immutable combat configuration definitions

**Files:**

- Create: `src/Hukbo.Core/Combat/BodyPart.cs`
- Create: `src/Hukbo.Core/Combat/CombatIdentity.cs`
- Create: `src/Hukbo.Core/Combat/TargetWeightProfile.cs`
- Create: `src/Hukbo.Core/Combat/CombatRuleset.cs`
- Create: `src/Hukbo.Core/Combat/PhilippineCombatPreset.cs`
- Create: `tests/Hukbo.Core.Tests/CombatConfigurationTests.cs`

**Step 1: Write failing taxonomy and preset tests**

Cover these exact behaviors:

```csharp
[Fact]
public void PhilippinePreset_UsesApprovedGeneralWeights()
{
    var rules = PhilippineCombatPreset.Rules;

    Assert.Equal(10, rules.GeneralTargets[BodyPart.WeaponArm]);
    Assert.Equal(8, rules.GeneralTargets[BodyPart.ShieldArm]);
    Assert.Equal(9, rules.GeneralTargets[BodyPart.Shoulder]);
    Assert.Equal(9, rules.GeneralTargets[BodyPart.Head]);
    Assert.Equal(9, rules.GeneralTargets[BodyPart.Neck]);
    Assert.Equal(8, rules.GeneralTargets[BodyPart.Face]);
    Assert.Equal(7, rules.GeneralTargets[BodyPart.Chest]);
    Assert.Equal(7, rules.GeneralTargets[BodyPart.Abdomen]);
    Assert.Equal(8, rules.GeneralTargets[BodyPart.Thigh]);
    Assert.Equal(7, rules.GeneralTargets[BodyPart.Knee]);
    Assert.Equal(7, rules.GeneralTargets[BodyPart.Shin]);
    Assert.Equal(8, rules.GeneralTargets[BodyPart.Hands]);
    Assert.Equal(2, rules.GeneralTargets[BodyPart.Feet]);
}

[Theory]
[InlineData(WeaponId.GreatBlade, BodyPart.Head, 10)]
[InlineData(WeaponId.GreatBlade, BodyPart.Chest, 8)]
[InlineData(WeaponId.HeavyChopper, BodyPart.Shoulder, 10)]
[InlineData(WeaponId.ThrustingBlade, BodyPart.Abdomen, 10)]
[InlineData(WeaponId.Bolo, BodyPart.WeaponArm, 10)]
[InlineData(WeaponId.Bolo, BodyPart.Hands, 9)]
public void PhilippinePreset_UsesApprovedWeaponOverrides(
    WeaponId weapon,
    BodyPart bodyPart,
    int expected)
{
    Assert.Equal(
        expected,
        PhilippineCombatPreset.Rules.ResolveWeaponWeight(weapon, bodyPart));
}
```

Also test:

- every `BodyPart` has one positive general weight;
- unlisted weapon parts inherit the general weight;
- generic arm preferences are present on both arm roles;
- the Panabas collarbone preference maps to `Shoulder`;
- `TallHardwood` uses 500 basis points for chest/abdomen and 1000 elsewhere;
- no-shield uses 1000 basis points everywhere;
- the four-entry roster order matches the design;
- IDs, definitions, and loadout entries are unique and valid;
- empty profiles, missing enum values, negative values, duplicate IDs, unknown
  references, and zero-total resolved profiles are rejected; and
- evidence metadata marks GreatBlade, HeavyChopper, and ThrustingBlade
  comparisons provisional.

**Step 2: Run the focused test and verify failure**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release `
  --filter FullyQualifiedName~CombatConfigurationTests
```

Expected: FAIL because the `Hukbo.Core.Combat` types do not exist.

**Step 3: Implement the smallest immutable surface**

Use stable IDs:

```csharp
public enum BodyPart
{
    WeaponArm,
    ShieldArm,
    Shoulder,
    Head,
    Neck,
    Face,
    Chest,
    Abdomen,
    Thigh,
    Knee,
    Shin,
    Hands,
    Feet,
}

public enum WeaponId { GreatBlade, HeavyChopper, ThrustingBlade, Bolo }
public enum ArmorId { LightOrganic }
public enum ShieldId { None, TallHardwood }
public enum CombatPresetId { PrecolonialPhilippinesV1 }

public readonly record struct CombatLoadout(
    WeaponId Weapon,
    ArmorId Armor,
    ShieldId Shield);
```

`TargetWeightProfile` owns copied fixed-size arrays indexed only after validating
the enum. Expose indexed reads, not mutable collections.

`CombatRuleset` exposes:

```csharp
public CombatPresetId Id { get; }
public int Version { get; }
public ulong ContentHash { get; }
public TargetWeightProfile GeneralTargets { get; }
public IReadOnlyList<CombatLoadout> Roster { get; }

public int ResolveWeaponWeight(WeaponId weapon, BodyPart bodyPart);
public int ResolveDefenseMultiplier(ShieldId shield, BodyPart bodyPart);
public CombatLoadout ResolveLoadout(ulong entityId);
public void Validate();
```

Use explicit arrays in `PhilippineCombatPreset`; do not deserialize JSON or add
a generic plugin/configuration framework.

**Step 4: Rerun and commit**

Expected: all `CombatConfigurationTests` PASS.

```powershell
git add -- `
  src/Hukbo.Core/Combat/BodyPart.cs `
  src/Hukbo.Core/Combat/CombatIdentity.cs `
  src/Hukbo.Core/Combat/TargetWeightProfile.cs `
  src/Hukbo.Core/Combat/CombatRuleset.cs `
  src/Hukbo.Core/Combat/PhilippineCombatPreset.cs `
  tests/Hukbo.Core.Tests/CombatConfigurationTests.cs
git commit -m "feat(combat): add Philippine combat configuration"
```

### Task 2: Make the preset and loadout authoritative simulation state

**Files:**

- Modify: `src/Hukbo.Core/Simulation/Scenario.cs`
- Modify: `src/Hukbo.Core/Simulation/AgentState.cs`
- Modify: `src/Hukbo.Core/Simulation/AgentView.cs`
- Modify: `src/Hukbo.Core/Simulation/BattleSimulation.cs`
- Modify: `tests/Hukbo.Core.Tests/ScenarioTests.cs`
- Modify: `tests/Hukbo.Core.Tests/BattleSimulationTests.cs`

**Step 1: Write failing scenario and assignment tests**

Add assertions that:

- `Scenario.CreateDefault().CombatPreset` is
  `PrecolonialPhilippinesV1`;
- scenario validation rejects an unknown preset enum;
- entities 1–4 receive the ordered four loadouts;
- entity 5 wraps to the first loadout;
- both factions use the same entity-ID rule;
- `AgentView.Loadout` exactly matches `AgentState.Loadout`; and
- repeated creation with the same scenario produces identical loadouts.

Use the public surface:

```csharp
public CombatPresetId CombatPreset { get; init; } =
    CombatPresetId.PrecolonialPhilippinesV1;

public readonly record struct AgentView(
    // existing fields,
    CombatLoadout Loadout);
```

Add `CombatLoadout loadout` to the internal `AgentState` constructor.

**Step 2: Run focused tests and verify failure**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release `
  --filter "FullyQualifiedName~ScenarioTests|FullyQualifiedName~BattleSimulationTests"
```

Expected: FAIL because scenario and agents do not expose combat configuration.

**Step 3: Implement minimal preset resolution and assignment**

- Resolve the preset once during `BattleSimulation.Create`.
- Validate the preset from `Scenario.Validate`.
- Assign each new agent with `rules.ResolveLoadout(entityId)`.
- Carry the loadout unchanged into `AgentView`.
- Update `CreateForTesting` helpers and every `AgentState` fixture explicitly.
- Do not derive any weapon identity in Client code during this task.

**Step 4: Rerun and commit**

Expected: focused tests PASS with existing movement and damage tests unchanged.

```powershell
git add -- `
  src/Hukbo.Core/Simulation/Scenario.cs `
  src/Hukbo.Core/Simulation/AgentState.cs `
  src/Hukbo.Core/Simulation/AgentView.cs `
  src/Hukbo.Core/Simulation/BattleSimulation.cs `
  tests/Hukbo.Core.Tests/ScenarioTests.cs `
  tests/Hukbo.Core.Tests/BattleSimulationTests.cs
git commit -m "feat(simulation): assign authoritative combat loadouts"
```

### Task 3: Add stateless deterministic hit-location selection

**Files:**

- Create: `src/Hukbo.Core/Combat/HitLocationResolver.cs`
- Create: `tests/Hukbo.Core.Tests/HitLocationResolverTests.cs`

**Step 1: Write failing resolver tests**

Cover:

- identical inputs always return the same body part;
- all returned values are defined `BodyPart` values;
- every body part is reachable across a fixed tuple matrix;
- weapon overrides alter cumulative intervals as configured;
- TallHardwood halves chest/abdomen effective weight before selection;
- no-shield leaves weights unchanged;
- a zero total is rejected;
- total-weight arithmetic is checked; and
- changing seed, tick, source, or target changes the roll for at least one
  vector in a table, without asserting that every single change must differ.

Use:

```csharp
public static BodyPart Resolve(
    CombatRuleset rules,
    CombatLoadout attacker,
    CombatLoadout defender,
    ulong seed,
    long tick,
    ulong sourceEntityId,
    ulong targetEntityId);
```

Add a golden vector table with at least eight fixed tuples and exact expected
body parts. These vectors pin enum order, mixer, modulo, and cumulative
selection behavior.

**Step 2: Run and verify failure**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release `
  --filter FullyQualifiedName~HitLocationResolverTests
```

Expected: FAIL because `HitLocationResolver` does not exist.

**Step 3: Implement the resolver**

Use checked integer arithmetic and this selection shape:

```csharp
var total = 0UL;
foreach (var part in Enum.GetValues<BodyPart>())
{
    var weight = rules.ResolveWeaponWeight(attacker.Weapon, part);
    var defense = rules.ResolveDefenseMultiplier(defender.Shield, part);
    total = checked(total + checked((ulong)weight * (ulong)defense));
}

var remaining = MixAttack(seed, tick, sourceEntityId, targetEntityId) % total;
foreach (var part in Enum.GetValues<BodyPart>())
{
    var effective = checked(
        (ulong)rules.ResolveWeaponWeight(attacker.Weapon, part) *
        (ulong)rules.ResolveDefenseMultiplier(defender.Shield, part));
    if (remaining < effective)
    {
        return part;
    }

    remaining -= effective;
}
```

Use a fixed integer system tag inside `MixAttack`. Keep the mixer private and
stateless. Do not allocate per attack; cache the stable body-part order in a
private array or use an allocation-free fixed loop.

**Step 4: Rerun and commit**

Expected: all resolver tests PASS.

```powershell
git add -- `
  src/Hukbo.Core/Combat/HitLocationResolver.cs `
  tests/Hukbo.Core.Tests/HitLocationResolverTests.cs
git commit -m "feat(combat): resolve deterministic hit locations"
```

### Task 4: Attach weapon and body part to authoritative attack events

**Files:**

- Modify: `src/Hukbo.Core/Simulation/BattleEvent.cs`
- Modify: `src/Hukbo.Core/Simulation/BattleSimulation.cs`
- Modify: `tests/Hukbo.Core.Tests/BattleSimulationTests.cs`
- Modify: `tests/Hukbo.Core.Tests/DeterminismTests.cs`
- Modify: `tests/Hukbo.Client.Tests/BattleEventFeedTests.cs`
- Modify: `tests/Hukbo.Client.Tests/HitEffectSystemTests.cs`
- Modify: `tests/Hukbo.Client.Tests/PresentationCoordinatorTests.cs`

**Step 1: Write failing attack-event tests**

Assert:

- every accepted `Attack` contains source weapon and resolved body part;
- two attackers damaging one target retain two individual hit locations;
- the target still receives one aggregated `Damage` event;
- scalar HP loss equals the old accumulated damage;
- same-tick mutual death still resolves before victory;
- non-attack events have null weapon and hit location; and
- repeated simulations produce identical event sequences and locations.

Extend the record:

```csharp
public readonly record struct BattleEvent(
    long Sequence,
    long Tick,
    BattleEventKind Kind,
    ulong SourceEntityId,
    ulong? TargetEntityId,
    int Value,
    int? FactionId,
    WeaponId? Weapon,
    BodyPart? HitLocation);
```

Update all Client test event helpers with null defaults so tactical hit effects
continue to use aggregated `Damage` events.

**Step 2: Run focused tests and verify failure**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release `
  --filter "FullyQualifiedName~BattleSimulationTests|FullyQualifiedName~DeterminismTests"
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release `
  --filter "FullyQualifiedName~BattleEventFeedTests|FullyQualifiedName~HitEffectSystemTests|FullyQualifiedName~PresentationCoordinatorTests"
```

Expected: FAIL until the event contract and constructors are updated.

**Step 3: Resolve hit locations before attack aggregation**

Inside `GatherAndCommitAttacks`:

- resolve the defender and both loadouts after range/cooldown checks;
- compute the body part before appending the proposal;
- extend the proposal buffer to retain `BodyPart`;
- emit `Attack` with source weapon and proposal body part;
- keep `_damageTotals[targetIndex] += source.DamagePerAttack` unchanged; and
- emit all other events with null combat context.

Do not move damage application or death resolution earlier in the tick.

**Step 4: Rerun and commit**

Expected: focused Core and Client compatibility tests PASS.

```powershell
git add -- `
  src/Hukbo.Core/Simulation/BattleEvent.cs `
  src/Hukbo.Core/Simulation/BattleSimulation.cs `
  tests/Hukbo.Core.Tests/BattleSimulationTests.cs `
  tests/Hukbo.Core.Tests/DeterminismTests.cs `
  tests/Hukbo.Client.Tests/BattleEventFeedTests.cs `
  tests/Hukbo.Client.Tests/HitEffectSystemTests.cs `
  tests/Hukbo.Client.Tests/PresentationCoordinatorTests.cs
git commit -m "feat(combat): explain attacks with hit locations"
```

### Task 5: Hash all new authoritative combat state

**Files:**

- Modify: `src/Hukbo.Core/Determinism/StateHasher.cs`
- Modify: `src/Hukbo.Headless/HeadlessRunner.cs`
- Modify: `tests/Hukbo.Core.Tests/DeterminismTests.cs`
- Modify: `tests/Hukbo.Core.Tests/HeadlessRunnerTests.cs`

**Step 1: Write failing hash-sensitivity tests**

Prove:

- changing preset ID/content changes state hash;
- changing any agent weapon, armor, or shield changes state hash;
- changing an event's weapon or hit location changes headless event hash;
- null combat context uses stable, distinct sentinels; and
- two identical runs still produce identical state and event hashes.

Avoid asserting one opaque full-run hash except for one documented golden
scenario. Prefer pairwise sensitivity assertions for individual fields.

**Step 2: Run and verify failure**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release `
  --filter "FullyQualifiedName~DeterminismTests|FullyQualifiedName~HeadlessRunnerTests"
```

Expected: FAIL because the new fields are not hashed.

**Step 3: Extend hashes**

- Add scenario preset ID and immutable ruleset content hash.
- Add every agent loadout enum value in fixed order.
- Add event weapon and body part in `HeadlessRunner.AddEventToHash`.
- Encode null enum values with `ulong.MaxValue`; do not collide with zero-valued
  enums.
- Update the documented golden value only after independently checking the
  implementation and event ordering.

**Step 4: Rerun and commit**

Expected: determinism and headless tests PASS.

```powershell
git add -- `
  src/Hukbo.Core/Determinism/StateHasher.cs `
  src/Hukbo.Headless/HeadlessRunner.cs `
  tests/Hukbo.Core.Tests/DeterminismTests.cs `
  tests/Hukbo.Core.Tests/HeadlessRunnerTests.cs
git commit -m "test(determinism): hash combat configuration and locations"
```

### Task 6: Make spectator presentation consume authoritative weapons

**Files:**

- Modify: `src/Hukbo.Client/Presentation/PawnAppearance.cs`
- Modify: `src/Hukbo.Client/Presentation/PawnAppearanceFactory.cs`
- Modify: `src/Hukbo.Client/Rendering/PawnGeometry.cs`
- Modify: `src/Hukbo.Client/Rendering/PawnRenderer.cs`
- Modify: `src/Hukbo.Client/UI/AgentInspectorPanel.cs`
- Modify: `src/Hukbo.Client/UI/BattleEventLogPanel.cs`
- Modify: `src/Hukbo.Client/ArenaGame.cs`
- Modify: `tests/Hukbo.Client.Tests/PawnAppearanceFactoryTests.cs`
- Modify: `tests/Hukbo.Client.Tests/PawnGeometryTests.cs`
- Modify: `tests/Hukbo.Client.Tests/AgentSelectionTests.cs`
- Modify: `tests/Hukbo.Client.Tests/BattleEventFeedTests.cs`

**Step 1: Write failing authoritative-presentation tests**

Assert:

- the same entity ID with different Core weapon IDs produces the same body and
  clothing but a different weapon role;
- all four Core weapon IDs map to one explicit silhouette;
- no factory call chooses a weapon from `entityId % n`;
- the inspector shows weapon, armor, shield, and evidence label from the
  authoritative loadout;
- an `Attack` event formats weapon and body part;
- non-attack events retain their current formatting;
- tactical `Damage` event behavior remains unchanged; and
- all four procedural weapon layouts remain inside calculated visual bounds at
  every detail tier.

Use:

```csharp
public static PawnAppearance Create(
    ulong entityId,
    WeaponId weapon);
```

Keep entity ID as the source of stature, build, clothing, skin, and head
treatment only.

**Step 2: Run focused Client tests and verify failure**

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release `
  --filter "FullyQualifiedName~PawnAppearanceFactoryTests|FullyQualifiedName~PawnGeometryTests|FullyQualifiedName~AgentInspector|FullyQualifiedName~BattleEventFeedTests"
```

Expected: FAIL until authoritative weapon data reaches presentation.

**Step 3: Implement explicit mappings**

- Replace the presentation-only five-role cycle with four roles corresponding
  to Core weapon IDs.
- Keep plain primary labels: `Great Blade`, `Heavy Chopper`,
  `Thrusting Blade`, and `Bolo`.
- Show comparative names only as a separate provisional evidence line.
- Add procedural chopper and thrusting-blade geometry without assets.
- Pass `agent.Loadout.Weapon` at every arena and inspector factory call.
- Format attacks with `Weapon` and `HitLocation`.
- Do not change `HitEffectSystem`; it continues to ingest `Damage`.

**Step 4: Rerun and commit**

Expected: focused Client tests PASS and displayed weapons match agent state.

```powershell
git add -- `
  src/Hukbo.Client/Presentation/PawnAppearance.cs `
  src/Hukbo.Client/Presentation/PawnAppearanceFactory.cs `
  src/Hukbo.Client/Rendering/PawnGeometry.cs `
  src/Hukbo.Client/Rendering/PawnRenderer.cs `
  src/Hukbo.Client/UI/AgentInspectorPanel.cs `
  src/Hukbo.Client/UI/BattleEventLogPanel.cs `
  src/Hukbo.Client/ArenaGame.cs `
  tests/Hukbo.Client.Tests/PawnAppearanceFactoryTests.cs `
  tests/Hukbo.Client.Tests/PawnGeometryTests.cs `
  tests/Hukbo.Client.Tests/AgentSelectionTests.cs `
  tests/Hukbo.Client.Tests/BattleEventFeedTests.cs
git commit -m "feat(presentation): show authoritative combat loadouts"
```

### Task 7: Run integration, statistical, and final review gates

**Files:**

- Create: `tests/Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs`
- Modify only if required by evidence from a failed check: files owned by
  Tasks 1–6.

**Step 1: Add end-to-end integration tests**

Add:

- a fixed-seed battle proving every `Attack` has a configured weapon/location;
- a same-seed repeated battle proving identical loadouts, locations, events,
  state hashes, event hashes, outcome, and tick;
- a large deterministic sample proving shielded chest/abdomen frequency is
  lower than the same tuple matrix without shields;
- a sample proving the four weapon profiles produce distinct target
  distributions; and
- a regression proving total damage, same-tick mutual death, and outcome rules
  remain unchanged.

Statistical tests must use a large fixed tuple matrix, compare broad inequalities
with comfortable margins, and print counts on failure. Do not assert exact
random-looking percentages.

**Step 2: Run focused integration tests**

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release `
  --filter FullyQualifiedName~PhilippineCombatIntegrationTests
```

Expected: PASS.

**Step 3: Run repository verification**

```powershell
dotnet test Hukbo.slnx -c Release --no-restore
dotnet build Hukbo.slnx -c Release --no-restore
.\scripts\format.ps1 -Verify
.\scripts\verify.ps1 -SkipBootstrap
git diff --check
```

Expected:

- all Core and Client tests PASS;
- Release build PASS;
- formatting verification PASS;
- canonical deterministic 200-agent workload PASS;
- no whitespace errors.

Classify failures before editing. Use up to three evaluator–optimizer cycles for
one failure mode, then choose a materially different approach or report the
blocker.

**Step 4: Inspect the complete task-scoped diff**

```powershell
git status --short
git diff -- `
  src/Hukbo.Core/Combat `
  src/Hukbo.Core/Simulation `
  src/Hukbo.Core/Determinism/StateHasher.cs `
  src/Hukbo.Headless/HeadlessRunner.cs `
  src/Hukbo.Client/Presentation `
  src/Hukbo.Client/Rendering `
  src/Hukbo.Client/UI `
  src/Hukbo.Client/ArenaGame.cs `
  tests/Hukbo.Core.Tests `
  tests/Hukbo.Client.Tests
```

Verify:

- no `AutonomousArena` names;
- no `System.Random` in authoritative combat;
- no terrain/naval/physiology/persistence additions;
- no debug code, disabled tests, placeholder branches, or mutable exposed
  configuration arrays;
- all new fields are validated and hashed; and
- every changed line supports the approved vertical slice.

**Step 5: Independent review**

Request a read-only reviewer to classify findings as Critical, High, Medium, or
Low. Resolve all Critical and High findings and rerun the narrowest affected
tests plus repository verification.

**Step 6: Commit integration coverage**

```powershell
git add -- tests/Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs
git commit -m "test(combat): verify Philippine combat vertical slice"
```

## Implementation handoff

Plan complete. Execute it only after the repository's current rename and
spectator changes have a stable baseline, or in an isolated worktree created
from that baseline.

Execution options:

1. **Orchestrated in this task:** use
   `docs/plans/2026-07-26-philippine-combat-orchestration.md`, with review after
   each dependency boundary.
2. **Separate execution task:** open a clean worktree and use the
   `executing-plans` skill task-by-task with checkpoints after Tasks 2, 5, and
   7.
