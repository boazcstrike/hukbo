# Tall Hardwood Shield Movement Implementation Plan

> **For Codex:** REQUIRED SUB-SKILL: Use `executing-plans` to implement this plan task-by-task.

**Goal:** Materialize deterministic Kalis + Tall Hardwood and Itak + Tall
Hardwood movement profiles that preserve active shield-bearing movement,
distinct weapon roles, independent ally lanes, and count-aware tactical
disengagement without adding directional defense or changing combat rules.

**Architecture:** This is the shield-owned equipment delta to
[`README.md`](README.md). The shared plan owns the versioned movement preset,
profile schema, count/composition observation, posture and lifecycle rules,
facing, retained scalar pace, collision integration, hashing, snapshots,
metrics, and scenario generator. This plan owns the two already-composed
shielded profile rows in
`src/Hukbo.Core/Movement/Profiles/TallHardwoodMovementProfiles.cs` and their
focused tests. Runtime code resolves each complete `CombatLoadout`; it never
applies a shield multiplier to a solo weapon profile.

**Tech Stack:** .NET 10 / C#, xUnit, Hukbo fixed-point integer math, immutable
movement rules, deterministic headless scenarios, and repository-local
PowerShell verification scripts.

---

**Status:** Implementation plan only. It does not authorize code changes,
movement-default activation, or combat-rule changes.

## Dependencies and ownership

Read this plan with:

- the [shared movement architecture](README.md);
- the [movement research program](../../research/movement/README.md);
- the [Tall Hardwood Shield research PRD](../../research/movement/tall-hardwood-shield.md);
- the [Kalis movement plan](kalis.md); and
- the [Itak movement plan](itak.md).

Do not begin profile work until shared Tasks T0–T3 have reconciled V4, frozen
legacy movement, added `LoadoutMovementProfile`, and added `Facing16`.

This plan is the sole owner of:

```text
src/Hukbo.Core/Movement/Profiles/TallHardwoodMovementProfiles.cs
tests/Hukbo.Core.Tests/Movement/TallHardwoodMovementProfileTests.cs
```

The Kalis and Itak owners provide their solo rows and focused solo tests. The
shared owner alone composes all six rows in `MovementPresetRegistry`, changes
the tick pipeline, or edits the movement enums. If an implementation needs to
change a shared file, return the finding to the shared owner rather than
creating a shield-specific controller.

Combat V2 is the current live default and contains both shielded loadouts.
The separately approved shared task switches the default to the already
registered, shieldless combat V3. After that switch, every shield acceptance
scenario must select combat V2 explicitly. Do not add shielded loadouts to V3.

## Current behavior

- `ShieldId.TallHardwood` currently affects shield interception through the
  combat rules; it has no movement behavior.
- Shielded Kalis and shielded Itak move with the same human baseline and
  pursuit logic as every other loadout.
- Movement has no authoritative facing, retained pace, commitment/recovery
  phase, equipment clearance, or local composition response.
- Collision proposals resolve simultaneously and deterministically. This plan
  preserves that boundary.
- Existing movement presets V1–V4 and explicit combat presets V1–V3 are
  versioned contracts. The new rows are inert unless the new opt-in movement
  preset is selected.

## Product behavior

The shield should read as protected deliberation with modest handling costs,
not as a slow tank or a shared defensive wall.

Both shielded profiles:

- retain most unobstructed forward travel;
- accelerate, reverse, and redirect less freely than their solo counterpart;
- approach through a free lane and preserve an exit;
- use the larger of their own and a nearby ally's clearance;
- keep independent lanes when another shield bearer is present;
- disengage toward perceived support when local pressure crosses the profile
  threshold;
- pursue only while support remains nearby; and
- expose facing as locomotion posture only.

Kalis + Tall Hardwood is the longer-spacing, lane-control pairing. It preserves
more distance, accepts smaller corrections, and remains locally committed
under somewhat greater pressure.

Itak + Tall Hardwood is the closer, repositioning pairing. It retains more
lateral/turning freedom than shielded Kalis, but breaks off earlier when
pressure grows and requires nearby support to pursue.

## Historical evidence boundary

| Research finding | Allowed implementation inference | Prohibited claim |
| --- | --- | --- |
| Pigafetta documents thin-wood shields and side-to-side approach at Mactan. **Documented**, encounter-specific. | Permit active approach and lateral route correction. | Do not identify the shield as tall hardwood or define a universal dodge. |
| The Boxer Codex depicts a tall curved Cagayan shield. **Documented, form uncertain**. | Preserve the game's tall-shield silhouette as one evidence-bounded visual anchor. | Do not infer mass, grip, material, step pattern, or shield wall. |
| Cole records active shield presentation and forward/backward movement centuries later. **Provisional reconstruction**. | Test deliberate advance, tactical yielding, and facing readability. | Do not name a 1500s technique or claim continuity. |
| Experimental shield/load studies show that active presentation and asymmetric loads can affect movement. **Provisional reconstruction**. | Calibrate modest turn, acceleration, and clearance differences. | Do not copy measured modern values into historical claims. |
| No located source supplies shielded Kalis/Itak footwork or count thresholds. **Unknown or unsupported**. | Keep every numeric value explicitly provisional and reject bad gameplay through tests. | Do not present the two roles as historical schools. |

Class-level comments on the profile file must link the shield research PRD and
state: **Provisional reconstruction:** gameplay tuning with no historical
measurement.

## Exact materialized profiles

Every value in this table is **Provisional reconstruction:** gameplay tuning
with no historical measurement.

The complete keys are:

```text
(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood)
(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood)
```

Unsupported armor, shield, or weapon combinations throw through the shared
profile resolver. They never fall back to a solo row.

| `LoadoutMovementProfile` field | Kalis + Tall Hardwood | Itak + Tall Hardwood |
| --- | ---: | ---: |
| `ForwardPaceBasisPoints` | 9,400 | 9,700 |
| `LateralPaceBasisPoints` | 8,400 | 8,700 |
| `BackwardPaceBasisPoints` | 6,700 | 7,100 |
| `CommittedPaceBasisPoints` | 3,000 | 3,500 |
| `PreferredDistanceBasisPoints` | 13,000 | 10,000 |
| `OpponentDistanceOffsetBasisPoints` (`KP, WA, KA, IT, KS, IS`) | `[-250, 0, 250, 500, 0, 250]` | `[-500, -250, 0, 250, -250, 0]` |
| `MaximumFacingStepsPerTick` | 2 | 2 |
| `CommittedFacingStepsPerTick` | 1 | 1 |
| `AccelerationBasisPointsPerTick` | 5,600 | 6,500 |
| `DecelerationBasisPointsPerTick` | 6,000 | 7,000 |
| `CommitmentTicks` | 3 | 3 |
| `RecoveryTicks` | 3 | 3 |
| `AllyClearanceBodyDiametersBasisPoints` | 14,000 | 13,500 |
| `DisengageEnemyToAllyBasisPoints` | 17,500 | 15,000 |
| `ReengageEnemyToAllyBasisPoints` | 11,000 | 11,000 |
| `PursuitSupportBodyDiametersBasisPoints` | 10,000 | 8,000 |

These numbers are candidate defaults, not activation values. They materialize
the research envelope rather than multiplying at runtime:

The six opponent-distance offsets are new **Provisional reconstruction**
planning hypotheses derived from the matchup questions; no source supplies
those numbers. Calibration may change or remove them without changing combat
reach.

| Comparison with paired solo row | Kalis shield result | Itak shield result |
| --- | ---: | ---: |
| Forward pace | `9,700 → 9,400` | `10,000 → 9,700` |
| Lateral pace | `8,900 → 8,400` | `9,300 → 8,700` |
| Backward pace | `7,600 → 6,700` | `8,100 → 7,100` |
| Acceleration | `6,000 → 5,600` | `7,000 → 6,500` |
| Ally clearance | `12,000 → 14,000` | `11,500 → 13,500` |
| Preferred distance | `12,000 → 13,000` | `11,000 → 10,000` |
| Enter disadvantage | `15,000 → 17,500` | `12,500 → 15,000` |
| Recovery | `2 → 3` ticks | `2 → 3` ticks |

No shield value may exceed `10_000` physical pace or the shared human speed.
Counts and composition choose posture, route, and willingness; they never
increase a pace field.

## Equality, lifecycle, and composition requirements

1. The actor counts as one support ally. Dead and out-of-radius agents do not
   count.
2. Zero enemies never enters or retains disengagement.
3. Kalis + shield enters at
   `enemyCount * 4 >= allyCount * 7`; exact `7:4` enters. It leaves at
   `enemyCount * 10 <= allyCount * 11`; exact `11:10` leaves.
4. Itak + shield enters at
   `enemyCount * 2 >= allyCount * 3`; exact `3:2` enters. It leaves at
   `enemyCount * 10 <= allyCount * 11`; exact `11:10` leaves.
5. Values between entry and release thresholds preserve the current
   disengagement state.
6. An attack accepted by the unchanged post-movement combat gates enters
   `Commit` for three ticks under the shared entry-tick convention, followed
   by three recovery ticks.
7. Normal facing changes by at most two `Facing16` steps per tick and committed
   facing by one. Shield facing has no attack or defense effect.
8. At exact preferred distance, ordinary approach becomes `Engage`. Until it
   reaches the existing post-movement attack gate, the bearer may cross the
   remaining distance through a free lane at the shared engaged-entry cap.
   Preferred distance never changes combat reach.
9. A living ally exactly on the effective clearance radius is clear; one raw
   unit inside is unsafe. Effective clearance is the larger of the actor's and
   ally's profile radii.
10. The selected opponent's complete loadout indexes the declared spacing
    offset. Nearby ally composition affects lane clearance through that ally's
    resolved profile. Global loadout composition affects only the shared
    contested-posture role-coverage tie-break.
11. Equal route candidates use the shared stable key and `EntityId` rules.
    Storage order, visual shield bearing, and PRNG state cannot decide a tie.
12. Collision-denied movement clamps retained scalar pace to actual movement;
    facing may still turn in place.

## Matchup and scale expectations

These are movement-shape assertions, not winner requirements.

| Case | Kalis + Tall Hardwood | Itak + Tall Hardwood |
| --- | --- | --- |
| vs Kampilan | Preserve the longer shielded-Kalis band, use a shallow lane, and stop pursuit before support is lost. | Use closer oblique entry after commitment; do not assume the shield negates reach. |
| vs Wasay | Avoid the planted commitment line and retain reverse clearance. | Reposition during recovery without circling indefinitely. |
| vs solo Kalis | Hold a distinct lane and avoid stacking behind another target. | Close through a free line, then restore an exit. |
| vs shielded Kalis | Mirror ties remain deterministic; no wall or front-lock state. | Respect the longer Kalis band and use the higher lateral cap. |
| vs solo Itak | Deny a free close entry without backing through allies. | Use shielded recovery and spacing without a speed bonus. |
| vs shielded Itak | Preserve greater separation and lane control. | Mirror probes terminate without a bespoke deadlock rule. |
| 1v2 | Enter both profiles' disadvantage thresholds. | Preserve an exit; no reverse-speed bonus. |
| 2v3 | Kalis shield does not enter on counts alone; Itak shield enters at exact equality. | Geometry, divided bearings, and global posture may still cause refusal or withdrawal. |
| 3v5 | Kalis shield does not enter on count alone (`5 × 4 < 3 × 7`); Itak shield enters (`5 × 2 >= 3 × 3`). | Shared `Yield`/`Withdraw`, free-lane refusal, and threat geometry still apply to both. |

Required group cases:

- two identical shield bearers choosing the same lane;
- mixed Kalis-shield/Itak-shield pair;
- each shield bearer with Kampilan, Wasay, solo Kalis, and solo Itak;
- shield pair against two long-clearance weapons;
- ally death during commitment and during recovery;
- global role-coverage advantage with a locally unsafe pocket;
- global disadvantage with a locally supported pocket;
- 4v4, 5v5, and 8v8 homogeneous and mixed groups;
- explicit-combat-V2 100v100 and 250v250 runs.

Reject any candidate that produces a rigid line, exact side-locking, universal
shield dominance, no viable shieldless entry, permanent reverse kiting,
threshold oscillation, or shield-bearing decisions that depend on visual
orientation.

## Dependency-ordered TDD tasks

### Task H1: Pin the two shield-owned rows

**Files:**

- Create `tests/Hukbo.Core.Tests/Movement/TallHardwoodMovementProfileTests.cs`
- Create `src/Hukbo.Core/Movement/Profiles/TallHardwoodMovementProfiles.cs`

1. Write failing literal-value tests for both complete loadouts and every field
   in the table, including six spacing offsets.
2. Add rejection tests for unsupported weapon, armor, or shield keys and
   assert there is no solo fallback.
3. Run:

   ```powershell
   dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release `
     --filter "FullyQualifiedName~TallHardwoodMovementProfileTests"
   ```

   Expected: FAIL because the profile file does not exist.
4. Add the two immutable rows only. Do not edit the registry or runtime rules.
5. Re-run. Expected: PASS.
6. Commit: `feat(movement): add tall hardwood movement profiles`.

### Task H2: Enforce the materialized shield envelope

**Files:** Modify only the two H1-owned files.

1. Add comparison tests resolving the solo Kalis/Itak rows and proving the
   eight paired differences in the comparison table.
2. Assert both shield rows remain within the shared profile bounds, preserve
   the human speed ceiling, add exactly `2_000` clearance basis points, and
   recover for one extra tick.
3. Assert the shield rows are distinct from one another; do not accept a
   generic cloned shield row.
4. Run the H1 filter. Expected: PASS after solo-row dependencies land.
5. Commit: `test(movement): pin shield profile envelopes`.

### Task H3: Pin equality, facing, pace, and collision behavior

**Files:**

- Create `tests/Hukbo.Core.Tests/Movement/TallHardwoodMovementTests.cs`
- Modify shared rules only through a shared-owner handoff if a common defect
  is demonstrated.

1. Add count cases immediately below, at, and above `7:4`, `3:2`, and the
   shared shield release threshold `11:10`, plus zero-enemy and self-only
   cases.
2. Add preferred-distance `-1/equal/+1` cases for both rows and all six
   opponent offset indices.
3. Add normal/committed facing caps, acceleration/deceleration, three-tick
   commitment/recovery, and no-instant-reverse tests.
4. Add collision-denial tests proving retained pace is clamped while facing
   may turn.
5. Permute candidate spans passed to the extracted pure query and assert
   identical decisions. Separately reverse caller input through
   `CreateForTesting` and assert canonicalization, then run mirrored positions
   and assert symmetry-normalized directions/distances and invariants; raw
   coordinates/facing make its state hash intentionally different.
6. Run the focused test and shared `WeaponMovementRulesTests`.
7. Commit: `test(movement): pin tall hardwood transitions`.

### Task H4: Prove independent shield lanes and composition effects

**Files:** Modify `TallHardwoodMovementTests.cs`; shared integration files
remain shared-owner territory.

1. Construct exact-clearance, one-unit-inside, and one-unit-outside cases for
   shield/shield, shield/Kampilan, shield/Wasay, shield/Kalis, and shield/Itak
   ally pairs.
2. Prove the larger profile clearance controls each pair and that the first
   oblique choice follows stable parity.
3. Prove selected-opponent loadout changes only preferred spacing through the
   profile offset; target selection and combat reach remain identical.
4. Prove global role coverage can break only a contested posture tie and never
   changes headcount or pace.
5. Assert two shield bearers choose distinct viable lanes when geometry
   permits and refuse rather than overlap when it does not.
6. Commit: `test(movement): verify shield lane composition`.

### Task H5: Cover shield-relevant 1v1 and 2v2 cells

**Files:** Shared owner creates/updates
`tests/Hukbo.Core.Tests/Movement/WeaponMovementMatchupTests.cs`; this plan
supplies shield acceptance cases.

1. Run the mechanically generated 21 unordered 1v1 cells and 231 unordered
   2v2 team matchups from shared Task T10.
2. Assert every cell containing `KS` or `IS` executes; verify exact case count,
   uniqueness, mirrors, and canonical ordering before simulation assertions.
3. Add the focused geometries in the matchup table and the required group
   pairs above.
4. Record distance, facing steps, phase occupancy, lane refusals, ally
   obstruction, disengagement, isolation, and no-progress streaks.
5. Treat outcomes as calibration evidence. Do not demand equal win rates.
6. Commit: `test(movement): cover tall hardwood matchup matrix`.

### Task H6: Preserve explicit combat V2 after the V3 default switch

**Files:** Shared integration tests only.

1. Change the existing default assertion in shared Task T2 and prove it fails
   before the V2-to-V3 initializer edit.
2. Add explicit combat-V2 scenarios with shielded Kalis and shielded Itak,
   nonzero roster counts, and the opt-in movement preset.
3. Assert each resolves the correct shield profile and repeats with identical
   ordered events, event hash, state hash, and outcome.
4. Assert default combat V3 still contains only the four solo rows.
5. Run Scenario, combat registry, profile, and shield scenario filters.
6. Commit: `test(movement): preserve shield scenarios on combat v2`.

### Task H7: Calibrate group and mass behavior

**Files:** Shared calibration artifacts/tests only; update this document later
only with measured, reviewed results.

1. Run 1v2, 2v3, 3v5, 4v4, 5v5, and 8v8 shield cases at the shared seed set.
2. Run explicit-combat-V2 100v100 and 250v250 at seeds `1, 2, 3, 5, 8`.
3. Record entry distance, facing demand, commitment/recovery, refusal,
   disengagement, ally conflicts, wall-like geometry duration, pursuit
   separation, global/local posture differences, hashes, runtime, and warm
   allocations.
4. Compare shielded and solo counterparts using identical seed and geometry.
5. Reject or tune an owned row if it violates a criterion in this plan. Change
   one field at a time and rerun the complete shield matrix.
6. Run `./scripts/verify.ps1`; retain the actual output in the shared
   implementation record.
7. Leave manual spectator checks `PENDING` unless personally observed.

## Completion and activation gates

This shield delta is ready for integration only when:

- both exact complete-loadout rows resolve and unsupported keys throw;
- neither runtime nor profile construction dynamically applies a shield
  multiplier;
- every equality and opponent-offset index has a focused test;
- shield facing affects movement only;
- independent lanes emerge without fixed rank or wall state;
- explicit combat V2 keeps both shielded loadouts after combat V3 becomes
  default;
- all shield-relevant generated duel/pair cells and asymmetric/group/mass
  scenarios execute deterministically;
- no count or composition increases physical speed;
- V1–V4 movement fixtures remain byte-identical;
- no Critical or High review finding remains; and
- the canonical gate and manual status are recorded honestly.

The new movement preset remains opt-in. This plan does not authorize changing
`Scenario.MovementPreset`. A later activation task requires explicit approval
after all five equipment plans and shared gates pass. Rollback selects
`PersistentContingentsV4`; it does not delete profile rows, renumber presets,
rewrite fixtures, or change combat V2/V3.
