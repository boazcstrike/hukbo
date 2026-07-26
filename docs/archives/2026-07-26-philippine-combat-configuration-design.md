# Philippine Combat Configuration Vertical Slice Design

> **Archived: reference only.** This document is deprecated. Do not execute it, and do not treat its steps, versions, or tooling references as current. The live contract is `CLAUDE.md` plus the skills in `.claude/skills/`.

## Status

Approved planning scope: end-to-end vertical slice.

This document defines the design only. It does not authorize or contain runtime
implementation changes.

## Goal

Add one deterministic pre-colonial Philippine combat preset to Hukbo so every
warrior has an authoritative weapon and defensive loadout, every attack resolves
an explainable body-part target from configured weights, and the spectator UI
shows the same weapon and hit location used by the simulation.

## Current baseline

Hukbo currently has:

- one scalar combat configuration on `Scenario`;
- one shared damage, range, and cooldown value for every warrior;
- aggregate hit points with no body parts or wounds;
- one `Attack` event per attacker and one aggregated `Damage` event per target;
- deterministic simultaneous damage and death resolution;
- Client-only weapon visuals selected from `entityId % 5`; and
- no weapon, armor, shield, terrain, naval, or ruleset definitions in Core.

The vertical slice must preserve the existing tick order, aggregate hit points,
fixed damage, simultaneous damage resolution, allocation bounds, and same-seed
determinism.

## Scope

### Included

- A canonical body-part taxonomy.
- Four configured weapon targeting profiles derived from the supplied brief.
- One light-organic armor identity and tall-hardwood shield profile.
- A versioned Philippine combat preset with deterministic mixed loadouts.
- Stateless deterministic weighted hit-location selection.
- Authoritative weapon, armor, and shield identity on agents and views.
- Weapon and hit-location metadata on individual `Attack` events.
- State-hash and headless event-hash coverage.
- Client weapon silhouettes, labels, inspector details, and event-log details
  driven by authoritative Core data.
- Core, Headless, and Client regression tests.

### Deferred

- Terrain geometry, jungle visibility, ambush, flanking, and rice-field rules.
- Boats, naval posture, balance loss, and downward-strike rules.
- Directional shields, facing, shield durability, blocks, and parries.
- Per-part hit points, wounds, bleeding, disability, amputation, or death rules.
- Body-part-specific damage multipliers.
- Per-weapon damage, range, cooldown, hit chance, or animation timing.
- Firearms, bows, spears, artillery, ammunition, and projectiles.
- Scenario JSON, migrations, persistence, setup UI, and user-authored presets.
- A playable European comparison ruleset.

The European comparison informs why the Philippine preset differs; it is not a
second implementation target.

## Design decisions

### 1. Hit locations are authoritative explanations, not physiology

Every accepted attack resolves exactly one `BodyPart`. That value is included
in the authoritative `Attack` event and event hash. It does not change damage,
health capacity, cooldown, future actions, or death.

This produces the requested injury distribution without inventing unsupported
damage multipliers or introducing a second health model.

### 2. Canonical body-part taxonomy

Use these stable enum values in this order:

| Numeric ID | Body part | General weight |
| ---: | --- | ---: |
| 1 | WeaponArm | 10 |
| 2 | ShieldArm | 8 |
| 3 | Shoulder | 9 |
| 4 | Head | 9 |
| 5 | Neck | 9 |
| 6 | Face | 8 |
| 7 | Chest | 7 |
| 8 | Abdomen | 7 |
| 9 | Thigh | 8 |
| 10 | Knee | 7 |
| 11 | Shin | 7 |
| 12 | Hands | 8 |
| 13 | Feet | 2 |

Normalization rules:

- `torso` means `Chest` plus `Abdomen`;
- generic `arms` applies to both `WeaponArm` and `ShieldArm`;
- `collarbone` is included in `Shoulder` for this slice; and
- left/right anatomy is represented only by weapon-side and shield-side arms.

Zero is intentionally undefined. Every authoritative enum uses explicit numeric
values; declaration order alone is not a compatibility contract. Numeric IDs
and ascending numeric order are part of deterministic selection and must not
change without a combat-preset version change.

### 3. Weapon profiles override only named preferences

Unlisted parts inherit the general warrior weight. Listed values replace the
general value for that weapon:

| Stable weapon ID | Historically cautious display | Overrides |
| --- | --- | --- |
| GreatBlade | `Great Blade` | Head 10; Neck 10; Shoulder 9; WeaponArm 8; ShieldArm 8; Chest 8 |
| HeavyChopper | `Heavy Chopper` | Shoulder 10; Head 9; WeaponArm 9; ShieldArm 9 |
| ThrustingBlade | `Thrusting Blade` | Abdomen 10; Chest 9; Neck 8 |
| Bolo | `Bolo` | WeaponArm 10; ShieldArm 10; Hands 9; Neck 8; Face 8 |

Configuration metadata records the supplied comparative names—Kampilan,
Panabas, and Kris—as provisional reference profiles, not universal
sixteenth-century player-facing identifications. UI labels remain plain
descriptors and may show a separate `PROVISIONAL` evidence note.

### 4. Armor and shield behavior

`LightOrganic` armor identifies quilted cotton, bark, leather, or similar light
protection. The general warrior weights already encode the increased
shoulder/chest/abdomen exposure relative to plate-armored warfare, so this first
profile applies no additional multiplier.

`TallHardwood` shield applies a provisional targeting multiplier of:

- Chest: 500 basis points;
- Abdomen: 500 basis points; and
- every other body part: 1000 basis points.

The relative probability of arms, legs, head, neck, and face therefore rises
without inventing bonuses for those parts. The 50% reduction is a gameplay
starting value, not a historical measurement, and must be named as provisional
in configuration comments and tests.

No-shield loadouts use 1000 basis points for every part.

### 5. Deterministic loadouts

The preset contains an ordered four-entry roster cycle:

1. GreatBlade, LightOrganic, no shield.
2. HeavyChopper, LightOrganic, no shield.
3. ThrustingBlade, LightOrganic, TallHardwood.
4. Bolo, LightOrganic, TallHardwood.

Assignment first rejects `EntityId == 0`, then uses
`(EntityId - 1) % roster.Count`. It consumes no RNG and gives
both factions the same stable distribution. These combinations are provisional
gameplay loadouts, not claims that every region used identical equipment.

### 6. Weighted hit-location resolution

For each accepted attack:

1. Read the general warrior weight for each body part.
2. Apply the attacking weapon's explicit override when present.
3. Multiply by the defending loadout's shield multiplier in basis points.
4. Sum the checked nonnegative effective weights.
5. Derive one unsigned deterministic roll from:
   `(system tag, scenario seed, tick, source entity ID, target entity ID,
   weapon ID)`.
6. Select `roll % totalWeight`.
7. Walk body parts in enum order and choose the first cumulative interval that
   contains the roll.

The roll algorithm is fixed:

```csharp
private const ulong OffsetBasis = 14_695_981_039_346_656_037UL;
private const ulong Prime = 1_099_511_628_211UL;
private const ulong HitLocationTag = 0x484B424F5F484954UL;

var hash = OffsetBasis;
Add(ref hash, HitLocationTag);
Add(ref hash, seed);
Add(ref hash, unchecked((ulong)tick));
Add(ref hash, sourceEntityId);
Add(ref hash, targetEntityId);
Add(ref hash, (ulong)attacker.Weapon);
return hash;

static void Add(ref ulong hash, ulong value)
{
    for (var shift = 0; shift < 64; shift += 8)
    {
        hash ^= (byte)(value >> shift);
        hash = unchecked(hash * Prime);
    }
}
```

Fields are added in exactly the shown order. Each field is encoded as eight
little-endian bytes. Tick uses the unchecked two's-complement `long` to `ulong`
conversion, although the resolver rejects negative ticks. Multiplication wraps
in unchecked 64-bit arithmetic. Do not use `System.Random`, wall-clock time,
collection iteration order, mutable global RNG state, or presentation data.

Golden roll/location vectors:

| Seed | Tick | Source | Target | Weapon | Defender shield | Roll hash | Location |
| ---: | ---: | ---: | ---: | --- | --- | --- | --- |
| 1 | 1 | 1 | 2 | GreatBlade | None | `5AB2E78583A95197` | Shin |
| 1 | 1 | 2 | 1 | HeavyChopper | TallHardwood | `84BF4CC561D9E994` | ShieldArm |
| 42 | 17 | 7 | 12 | ThrustingBlade | TallHardwood | `2A18A5AFEF928686` | Knee |
| 42 | 17 | 12 | 7 | Bolo | None | `A98FBA5910945501` | Thigh |
| `0xDEADBEEF` | 99 | 199 | 200 | GreatBlade | TallHardwood | `56E9870A427F50A6` | Knee |
| 0 | 0 | 3 | 4 | ThrustingBlade | None | `295B7F1E45FC5AB1` | Chest |
| `0xFFFFFFFFFFFFFFFF` | `0x7FFFFFFFFFFFFFFF` | 4 | 3 | HeavyChopper | None | `4F91245EAE04F060` | Neck |
| 987654321 | 1234 | 88 | 17 | Bolo | TallHardwood | `7B598081E38B044F` | Thigh |

An attacker can propose at most one attack per tick, so the tuple uniquely
identifies the hit-location draw in the current combat model.

### 7. Data model

Add a small immutable Core configuration boundary:

```text
CombatPresetId
  -> CombatPresetRegistry
       -> CombatRuleset
       -> GeneralTargetWeights
       -> WeaponDefinition[]
       -> DefenseDefinition[]
       -> LoadoutDefinition[]
```

Recommended public/authoritative types:

```csharp
public enum WeaponId
{
    GreatBlade = 1,
    HeavyChopper = 2,
    ThrustingBlade = 3,
    Bolo = 4,
}

public enum ArmorId { LightOrganic = 1 }
public enum ShieldId { None = 1, TallHardwood = 2 }
public enum CombatPresetId { PrecolonialPhilippinesV1 = 1 }

public readonly record struct CombatLoadout(
    WeaponId Weapon,
    ArmorId Armor,
    ShieldId Shield);
```

`CombatRuleset` and weight profiles are immutable after construction. They
validate complete enum coverage, positive general weights, unique stable IDs,
valid overrides, valid multipliers, nonempty roster entries, and a nonzero
resolved total for every loadout pairing.

`Scenario` stores the stable `CombatPresetId`. `CombatPresetRegistry.Get`
resolves it through an exhaustive switch and throws for unregistered values;
`CombatPresetRegistry.IsRegistered` supports validation. Both
`Scenario.Validate` and `BattleSimulation.Create` use the registry rather than
assuming `Enum.IsDefined` implies a configuration exists. This avoids embedding
mutable collections in the scenario record.

`AgentState` and `AgentView` store the assigned `CombatLoadout`.

### 8. Event contract

Extend `BattleEvent` with nullable combat context and enforce it at construction:

```csharp
WeaponId? Weapon,
BodyPart? HitLocation
```

Rules:

- `Attack`: both values are required.
- `Move`, `Damage`, `Death`, and `Outcome`: both values are null.
- `Attack.Value` remains the attack's scalar damage.
- `Damage` remains one aggregated event per target per tick.

Use a non-positional record struct with an explicit validating constructor or
validated factories. `BattleEvent.Attack(...)` requires weapon and location.
The non-attack factory rejects either field. No public construction path may
create an `Attack` without both values or a non-attack event with either value.

Individual `Attack` events retain hit-location identity even when several
attacks are aggregated into one `Damage` event.

### 9. Hashing and compatibility

The state hash includes:

- combat preset ID;
- preset schema/content version or deterministic content hash;
- every agent's weapon, armor, and shield IDs; and
- all existing authoritative values.

The headless event hash includes nullable weapon and hit-location values with
documented sentinel encodings.

Preset `ContentHash` uses the same FNV-1a `Add` encoding defined for attack
rolls, starting at `OffsetBasis`, with fields in this canonical order:

1. preset ID and version;
2. body-part count, then `(body-part numeric ID, general weight)` in ascending
   numeric order;
3. weapon count, then each weapon ID and every
   `(body-part numeric ID, resolved weapon weight)` in ascending order;
4. armor count and armor IDs in ascending order;
5. shield count, then each shield ID and every
   `(body-part numeric ID, multiplier)` in ascending order; and
6. roster count, then each `(weapon ID, armor ID, shield ID)` in roster order.

Strings and evidence labels are presentation metadata and are excluded.
`HashCode`, runtime `GetHashCode`, reflection order, and dictionary order are
prohibited. Version 1's expected content hash is `59FB4CA563D87A49`.

Changing enum numeric values, enum ordering, roster order, target weights,
shield multipliers, or the hit-roll mixer requires a new preset version and new
golden expectations.

No save compatibility or migration is required because this slice does not add
scenario or snapshot persistence.

### 10. Client integration

`PawnAppearanceFactory` accepts authoritative `WeaponId` rather than selecting
a weapon from the entity ID. Entity ID continues to derive presentation-only
body, clothing, and head-treatment variation.

The Client maps Core weapon IDs to four procedural silhouettes. The inspector
shows:

- player-facing weapon label;
- armor label;
- shield label; and
- provisional evidence note where applicable.

The battle event log formats attacks as, for example:

```text
T00042  Blue #7 hit #12's shoulder with Great Blade for 10
```

Formatting lives in a pure Client presentation formatter so wording is covered
without GPU/UI rendering tests.

Existing damage effects continue to consume aggregated `Damage` events and are
not duplicated for each hit location.

## Error handling

- Invalid or incomplete rulesets fail during preset construction/validation.
- Unknown enum values fail validation rather than silently falling back.
- A resolved target profile with total weight zero throws before simulation.
- Checked arithmetic protects total weights and accumulated damage.
- Non-`Attack` events carrying weapon or hit-location context fail event
  validation in tests and construction helpers.

## Verification criteria

- The supplied general and weapon-specific weights are represented exactly.
- Shielded and unshielded targets produce different deterministic
  distributions, with chest/abdomen less common for shielded targets.
- Same scenario and seed produce identical loadouts, hit locations, events,
  hashes, and outcomes across repeated runs.
- Different stable attack tuples can select different locations.
- Every attack event has a valid weapon and body part.
- Aggregate damage, simultaneous death, cooldown, and victory behavior remain
  unchanged.
- Arena and inspector silhouettes match `AgentView.Loadout.Weapon`.
- Event logs show authoritative weapon and hit location.
- Focused tests, all repository tests, Release build, formatting, and canonical
  verification pass.

## Risks

- **Historical overclaiming:** keep named comparisons in evidence metadata and
  plain descriptors in primary UI labels.
- **Accidental replay drift:** version enum order, weight content, roster order,
  and mixer behavior; cover them with golden tests.
- **Event-constructor churn:** update all Core and Client fixtures in the same
  task as the event contract.
- **Presentation divergence:** remove entity-ID weapon selection and make the
  authoritative weapon a required factory input.
- **Scope creep into physiology:** preserve scalar HP and fixed damage; treat
  hit location as authoritative explanatory metadata only.
- **Working-tree overlap:** the repository already contains an in-progress
  Hukbo rename and spectator work. Stage only explicitly owned paths.

## Completion Record

This design was fully implemented and verified as part of the Philippine combat configuration vertical slice (Tasks 1–7).

### Implemented

An immutable versioned combat preset registry was added to `Hukbo.Core`, storing the Philippine combat configuration with four deterministic mixed loadouts assigned via `(EntityId - 1) % 4`. Hit-location resolution is stateless and weighted, with authoritative weapon and hit-location metadata carried on individual `Attack` events and included in state-hash and headless event-hash computations. Spectator presentation in `Hukbo.Client` is now driven by authoritative `WeaponId` rather than deriving visual roles from `entityId % 5`.

### Verification

The canonical gate was run in isolation after spectator-clarity work caused unrelated format drift:
- `dotnet build Hukbo.slnx -c Release` completed cleanly.
- `dotnet test Hukbo.slnx -c Release` ran 142 Core tests and 185 Client tests, all passing.
- Headless 200-agent / 10,000-tick / seed-1 workload reported:
  - `deterministic: true`
  - `firstMismatchTick: null`
  - `measuredTicks: 235`
  - `outcome: Faction1Victory`
  - `eventHash: 941377BD43C556FF`
  - `stateHash: 6EBB1EA63114F6CE`
- Preset `ContentHash` pinned at `0x59FB4CA563D87A49` (matches design §9).
- All eight design §6 golden roll/location vectors reproduced correctly.

### Not Passed: Full Gate

The complete `scripts/verify.ps1` gate did not finish, because its first stage (`format.ps1 -Verify`) fails on `src/Hukbo.Client/Settings/ClientSettingsStore.cs`, a file belonging to unrelated spectator and theming work. Gates 2–4 (Release build, Release tests, 200-agent determinism benchmark) all passed individually.

### Deferred

Terrain, jungle visibility, ambush and flanking; boats and naval posture; directional shields, facing, shield durability, blocks and parries; per-part hit points, wounds, bleeding, amputation; body-part damage multipliers; per-weapon damage/range/cooldown; firearms, bows, spears, projectiles; scenario JSON, persistence, migrations, setup UI; a playable European comparison ruleset. None of these were added and remain out of scope.

### Open Findings (Medium/Low, Recorded for Backlog)

- `HitLocationResolver` rebuilds effective weights twice per attack rather than using a precomputed cumulative table.
- `AgentView.Loadout` retains a `default` value whose zero-valued enums would throw in Client label switches if a snapshot/replay path ever produced one.
- `HeadlessRunner.RunReport` does not record `combatPresetId` or the preset content hash.
- `Scenario` and `BattleSimulation` resolve the ruleset via `CombatPresetRegistry.Get` on every state-hash computation instead of caching the resolved instance.
