# Philippine Combat Configuration Vertical Slice Design

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

| Body part | General weight |
| --- | ---: |
| WeaponArm | 10 |
| ShieldArm | 8 |
| Shoulder | 9 |
| Head | 9 |
| Neck | 9 |
| Face | 8 |
| Chest | 7 |
| Abdomen | 7 |
| Thigh | 8 |
| Knee | 7 |
| Shin | 7 |
| Hands | 8 |
| Feet | 2 |

Normalization rules:

- `torso` means `Chest` plus `Abdomen`;
- generic `arms` applies to both `WeaponArm` and `ShieldArm`;
- `collarbone` is included in `Shoulder` for this slice; and
- left/right anatomy is represented only by weapon-side and shield-side arms.

The enum order is part of deterministic weighted selection and must not be
reordered without a combat-preset version change.

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

Assignment uses `(EntityId - 1) % roster.Count`. It consumes no RNG and gives
both factions the same stable distribution. These combinations are provisional
gameplay loadouts, not claims that every region used identical equipment.

### 6. Weighted hit-location resolution

For each accepted attack:

1. Read the general warrior weight for each body part.
2. Apply the attacking weapon's explicit override when present.
3. Multiply by the defending loadout's shield multiplier in basis points.
4. Sum the checked nonnegative effective weights.
5. Derive one unsigned deterministic roll from:
   `(scenario seed, tick, source entity ID, target entity ID, fixed system tag)`.
6. Select `roll % totalWeight`.
7. Walk body parts in enum order and choose the first cumulative interval that
   contains the roll.

Use a stateless integer mixer. Do not use `System.Random`, wall-clock time,
collection iteration order, mutable global RNG state, or presentation data.

An attacker can propose at most one attack per tick, so the tuple uniquely
identifies the hit-location draw in the current combat model.

### 7. Data model

Add a small immutable Core configuration boundary:

```text
CombatPresetId
  -> CombatRuleset
       -> GeneralTargetWeights
       -> WeaponDefinition[]
       -> DefenseDefinition[]
       -> LoadoutDefinition[]
```

Recommended public/authoritative types:

```csharp
public enum BodyPart { ... }
public enum WeaponId { GreatBlade, HeavyChopper, ThrustingBlade, Bolo }
public enum ArmorId { LightOrganic }
public enum ShieldId { None, TallHardwood }
public enum CombatPresetId { PrecolonialPhilippinesV1 }

public readonly record struct CombatLoadout(
    WeaponId Weapon,
    ArmorId Armor,
    ShieldId Shield);
```

`CombatRuleset` and weight profiles are immutable after construction. They
validate complete enum coverage, positive general weights, unique stable IDs,
valid overrides, valid multipliers, nonempty roster entries, and a nonzero
resolved total for every loadout pairing.

`Scenario` stores the stable `CombatPresetId`; the registry resolves that ID to
the immutable ruleset. This avoids embedding mutable collections in the
scenario record.

`AgentState` and `AgentView` store the assigned `CombatLoadout`.

### 8. Event contract

Extend `BattleEvent` with nullable combat context:

```csharp
WeaponId? Weapon,
BodyPart? HitLocation
```

Rules:

- `Attack`: both values are required.
- `Move`, `Damage`, `Death`, and `Outcome`: both values are null.
- `Attack.Value` remains the attack's scalar damage.
- `Damage` remains one aggregated event per target per tick.

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
