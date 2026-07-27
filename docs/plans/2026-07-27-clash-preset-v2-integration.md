# Weapon clash on preset V2 — integration plan

Date: 2026-07-27
Design: [2026-07-27-clash-preset-v2-integration-design.md](2026-07-27-clash-preset-v2-integration-design.md)
Branch: `clash-integration`, based on `main` at `de19c57`.
Source branch: `worktree-weapon-clash`, tip `3cd4bc6`, true merge base `2d88b43`.

Decisions D1 through D8 referenced below are defined in the design document.
This plan does not restate their reasoning.

## Sequencing

The work funnels through a small number of shared seams — one tick stage, one
event type, one ruleset class — so most of it is serial by nature. Parallelism
is called out explicitly where the file sets are genuinely disjoint; everywhere
else, two agents in flight would be a merge conflict created on purpose.

| Phase | Contents | Parallel? |
| --- | --- | --- |
| 0 | Land the merge and make the tree compile | serial |
| 1 | Core structure: profile key, ruleset, event, simulation | serial |
| 2 | Preset tables and the ten new cells | serial |
| 3 | Client reconciliation | two disjoint groups |
| 4 | Tests and golden re-baseline | two disjoint groups |
| 5 | Measurement, gate, records | serial |

## Phase 0 — land the merge, restore compilation

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| T01 | Merge `worktree-weapon-clash` into `clash-integration`, resolving each of the eleven conflicts to the *union* of both sides wherever the design says both are wanted. Do not attempt to make it compile yet. | all eleven conflicted files | merge commit exists, no conflict markers remain in the tree | — | `git grep -n '^<<<<<<<\|^>>>>>>>'` returns nothing |
| T02 | Sweep the 41 files still referencing `WeaponId.GreatBlade`, `HeavyChopper`, `ThrustingBlade`, `Bolo` to the V2 names `Kampilan`, `Wasay`, `Kalis`, `Itak`. Mechanical rename only — no behavior change, no retuning. | 41 files: 17 `tests/Hukbo.Client.Tests`, 11 `tests/Hukbo.Core.Tests`, 3 `src/Hukbo.Client/Rendering`, 3 `src/Hukbo.Client/Presentation`, 2 `src/Hukbo.Core/Combat`, 2 `src/Hukbo.Client/Audio`, 2 `tools/`, 1 fixture | zero references to the four old symbols remain | T01 | `git grep -c 'GreatBlade\|HeavyChopper\|ThrustingBlade\|WeaponId.Bolo'` returns nothing |
| T03 | Record the tree as knowingly non-compiling and enumerate every remaining break, grouped by cause. This is the checklist Phase 1 works against. | none — a scratch note, not a repository file | a list exists naming every compile error and its owning task | T02 | `dotnet build` output captured and grouped |

## Phase 1 — Core structure

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| T10 | Re-key `_weaponIntercept` to `(WeaponId Defender, ShieldId DefenderShield, WeaponId Attacker)` and `_voidChannel` to `(WeaponId, ShieldId)` per D3. Leave the hard-share tables and the shield scalar alone. | `src/Hukbo.Core/Combat/ClashProfile.cs` | both resolvers take the shield half of the key; hard-share resolvers unchanged | T02 | `ClashProfile` compiles standalone |
| T11 | Replace enum-cross-product validation with range-and-consistency validation per D4. Rebuild `Neutral` as a profile resolving to zero for any key rather than a dense cross-product. | `src/Hukbo.Core/Combat/ClashProfile.cs` | `ValidateMatrix` no longer calls `Enum.GetValues<WeaponId>()`; `Neutral` needs no roster | T10 | new unit tests, T40 |
| T12 | Merge the two constructor tails: `CombatRuleset` takes both `weaponAttributes` and `clashProfile` as trailing optional parameters. Keep both member sets — `ResolveWeaponProfile`, `MaximumProfileDamagePerAttack`, `HasWeaponProfiles` from `main`; `ClashProfile`, `WithClashProfile`, `ValidateClashProfileCoversTheRoster` from clash. | `src/Hukbo.Core/Combat/CombatRuleset.cs` | both parameter sets present, every existing named-argument call site untouched | T11 | build |
| T13 | Make the clash fold conditional per D2, ordered after the weapon-attribute block. Document the fold order in the method. | `src/Hukbo.Core/Combat/CombatRuleset.cs` | `FoldClashProfile` runs only when a profile was supplied | T12 | T41 pins V1's hash unchanged |
| T14 | Extend `ValidateClashProfileCoversTheRoster` to probe the new key, naming defender weapon, defender shield, and attacker weapon in the failure message. | `src/Hukbo.Core/Combat/CombatRuleset.cs` | a missing cell throws with all three named | T13 | T40 |
| T15 | Add `ResolutionShift = 24` and fold `AttackResolution` into `_combatContext` per D5. Extend `BattleEvent.Attack` and the private constructor to take both `shield` and `resolution`. | `src/Hukbo.Core/Simulation/BattleEvent.cs` | event exposes `Weapon`, `Shield`, `HitLocation`, `Resolution`, all from one `int` | T02 | T42 |
| T16 | Extend `AddAttackEvent` to take both `shield` and `resolution`. Restore clash's `_attackProposals` tuple widening, the `ClashResolver.Resolve` call after `HitLocationResolver.Resolve`, damage conditional on `Landed`, and `CombatMetrics` accumulation — all against `main`'s shield-carrying call shape. | `src/Hukbo.Core/Simulation/BattleSimulation.cs` | `GatherAndCommitAttacks` resolves a clash per proposal and commits damage only for `Landed` | T15, T14 | T43, T44 |
| T17 | Keep clash's `StateHasher.Compute(..., ulong contentHash)` overload and the split `ComputeStateHash` seam. Confirm `main`'s untouched call site still resolves. | `src/Hukbo.Core/Determinism/StateHasher.cs`, `src/Hukbo.Core/Simulation/BattleSimulation.cs` | both overloads present, control-run seam intact | T16 | build, T45 |
| T18 | Fold `Resolution` into the headless event hash. | `src/Hukbo.Headless/HeadlessRunner.cs` | `AddEventToHash` includes the resolution byte | T15 | T46 |
| T19 | Surface `CombatMetrics` on `RunReport`. | `src/Hukbo.Headless/RunReport.cs`, `HeadlessRunner.cs` | report carries the six counters and the derived share | T16 | T47 |

## Phase 2 — preset tables

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| T20 | Revert the in-place version bump per D1: `PhilippineCombatPreset` returns to `Version = 1`, keeps `PrecolonialPhilippinesV1`, and is constructed with no clash profile. Remove `BuildClashProfile` from this file. | `src/Hukbo.Core/Combat/PhilippineCombatPreset.cs` | V1 declares no clash profile | T13 | T41 |
| T21 | Move the clash tables onto V2, re-keyed for the six-loadout roster. Carry the sixteen existing cells across by numeric weapon identity onto the *shielded* defender keys where the V1 roster implied a shield, and onto the bare keys for Kampilan and Wasay. | `src/Hukbo.Core/Combat/PhilippineCombatPresetV2.cs` | V2 constructs with a clash profile covering all six loadouts | T20 | T40 |
| T22 | Author the ten new cells per design section 5: four weapon-intercept cells for shieldless Kalis, four for shieldless Itak, and two void-channel entries. Each carries a code comment naming the band it was drawn from and the label **Provisional reconstruction**. | `src/Hukbo.Core/Combat/PhilippineCombatPresetV2.cs` | every roster pair resolves; every new value carries a labelled comment | T21 | T40, T60 |
| T23 | Bump `PhilippineCombatPresetV2.Version`, since its content changed. | `src/Hukbo.Core/Combat/PhilippineCombatPresetV2.cs` | version constant raised | T22 | T41 |

## Phase 3 — client

Two disjoint groups. 3A touches presentation and formatting; 3B touches
rendering geometry. They share no files.

### Group 3A — presentation

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| T30 | Merge `FormatAttack` per D6: `main`'s `GetWeaponLabel(weapon, shield)` pair form and grip suffix, substituted into clash's five-way resolution switch. Discard clash's bare English labels. | `src/Hukbo.Client/Presentation/BattleEventFormatter.cs` | all five branches render the pair label with grip | T15 | T50 |
| T31 | Restore the clash-only presentation files and wire them into `PresentationCoordinator`. | `Presentation/ClashEffect.cs`, `ClashEffectSystem.cs`, `SwingAnimation.cs`, `SwingAnimationSystem.cs`, `PresentationCoordinator.cs` | `Swings` and `ClashEffects` are reachable from the coordinator | T15 | T52 |
| T32 | Restore blood suppression for non-`Landed` attacks and the detail-panel value omission. | `Presentation/BloodEffectSystem.cs`, `Presentation/BattleEventFeed.cs`, `UI/BattleEventLogPanel.Details.cs` | no blood on a turned-aside blow | T15 | T51 |

### Group 3B — rendering

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| T35 | Reconcile `PawnLayout` per D7: keep `ShieldBounds` and `SwingTrail`. Keep `main`'s per-weapon geometry constants including the Wasay axe-head block and the `weaponEnd` parameter on `CreateSecondaryBounds`. Re-apply `ApplySwing` on top. | `src/Hukbo.Client/Rendering/PawnGeometry.cs` | layout carries both fields; `main`'s constants intact; swing rotates the merged weapon line | T02 | T53 |
| T36 | Merge `PawnRenderer.Draw`: keep `DrawShield` and `DrawSwingTrail`, and take `scaleMultiplier`, `hitPulseStrength`, and `swingPose`. | `src/Hukbo.Client/Rendering/PawnRenderer.cs` | both draw calls present in one `Draw` | T35 | T53 |
| T37 | Restore the clash-only rendering files and the `_swingPoses` field and per-frame resolve. | `Rendering/ClashEffectGeometry.cs`, `ClashEffectRenderer.cs`, `SwingGeometry.cs`, `SwingPoseResolver.cs`, `ArenaGame.cs`, `ArenaGame.Rendering.cs` | clash cross draws; swing pose reaches `DrawPawns`; `PawnAppearanceFactory.Create` uses `main`'s three-argument shape | T36 | T53 |

## Phase 4 — tests and goldens

Two disjoint groups, both starting once Phase 2 is complete.

### Group 4A — Core tests

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| T40 | Update the clash profile and ruleset tests for the new key and the roster-driven validation. Add a case proving a missing shieldless cell throws naming all three key parts. | `tests/Hukbo.Core.Tests/ClashResolverTests.cs`, `CombatConfigurationTests.cs`, `PhilippineCombatIntegrationTests.cs` | all clash tests exercise the three-part key | T23 | `dotnet test` |
| T41 | Pin V1's content hash at `0x59FB4CA563D87A49` **unchanged** as the D2 regression guard. Re-baseline V2's content hash only after T40 is green. | `tests/Hukbo.Core.Tests/DeterminismTests.cs` | V1 constant untouched and passing; V2 constant re-captured | T40 | `dotnet test` |
| T42 | Pin the packed-resolution reasoning from D5: the spare-byte range assumption, and that `Landed = 0` is unambiguous because the weapon field is non-zero for every attack. | `tests/Hukbo.Core.Tests/BattleEventTests.cs` | a test fails if a fifth field is packed without widening the reasoning | T15 | `dotnet test` |
| T43 | Revert the allocation ceiling to 900,000 and re-measure. If the merged event exceeds it, report the real figure rather than raising the ceiling silently. | `tests/Hukbo.Core.Tests/BattleSimulationTests.cs` | ceiling is 900,000 and the measured figure is recorded | T16 | `dotnet test` |
| T44 | Restore clash's simulation, termination, and zero-interception control tests against the merged shape. | `tests/Hukbo.Core.Tests/BattleSimulationTests.cs`, `DeterminismTests.cs` | control run still produces the pinned pre-clash digest | T16 | `dotnet test` |
| T45 | Re-verify that `CombatMetrics` reaches neither hash, capturing the before-and-after pair on the merged tree rather than trusting the clash branch's pair. | `tests/Hukbo.Core.Tests/DeterminismTests.cs` | both hashes byte-identical across accumulation | T44 | `dotnet test` |
| T46 | Re-baseline the seed-1 event and state hashes. | `tests/Hukbo.Core.Tests/DeterminismTests.cs`, `HeadlessRunnerTests.cs` | new pair captured and pinned | T45 | headless run |
| T47 | Assert `RunReport` carries the combat metrics. | `tests/Hukbo.Core.Tests/HeadlessRunnerTests.cs` | report fields asserted | T19 | `dotnet test` |

### Group 4B — Client tests

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| T50 | Merge the formatter tests: pair-form label and grip suffix from `main`, five-way resolution branching from clash, in one consistent set of expected strings. | `tests/Hukbo.Client.Tests/BattleEventFormatterTests.cs` | every resolution asserts the pair label | T30 | `dotnet test` |
| T51 | Restore the blood-suppression test. | `tests/Hukbo.Client.Tests/BloodEffectSystemTests.cs` | non-`Landed` produces no blood | T32 | `dotnet test` |
| T52 | Restore the coordinator, clash-effect, swing-system, and swing-resolver tests. | `PresentationCoordinatorTests.cs`, `ClashEffectSystemTests.cs`, `SwingAnimationSystemTests.cs`, `SwingPoseResolverTests.cs` | all four suites green | T31 | `dotnet test` |
| T53 | Update the geometry tests for the merged `PawnLayout` — both `ShieldBounds` and `SwingTrail`, against `main`'s constants. | `tests/Hukbo.Client.Tests/PawnGeometryTests.cs`, `PawnAppearanceFactoryTests.cs` | layout assertions cover both fields | T37 | `dotnet test` |
| T54 | Fix the five silently auto-merged call sites that never appeared as conflicts. | `BattleEventFeedTests.cs`, `HitEffectSystemTests.cs`, `SoundCatalogTests.cs`, `SoundCueMapperTests.cs`, `SoundDirectorTests.cs` | all five pass both `shield` and `resolution` | T15 | `dotnet test` |

## Phase 5 — measurement, gate, records

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| T60 | Retake the defence-attributable share across seeds 1 through 20 per D8. **Gate task, not a reporting task:** the merged share must fall inside 0.25 to 0.45. If it does not, the cells authored in T22 are retuned within their bands and this task repeats. | none — measurement | 20 seeds measured, all inside the band | T46, T53 | headless sweep output |
| T61 | Retake termination: at least 19 of 20 seeds decide before the tick cap, median decisive tick at or below 5,000. | none — measurement | both criteria met | T60 | headless sweep output |
| T62 | Run `./scripts/verify.ps1` once, on the integrated tree. Not delegated. | none | all five stages pass | T61 | pasted gate output |
| T63 | Record the merged results in `docs/development/testing.md`: the new gate run, the retaken acceptance figures, the allocation figure from T43, and the metrics hash-neutrality pair from T45. Move the clash branch's Phase 2 reference pair into a superseded section rather than deleting it. Keep `main`'s V2-1 through V2-10 smoke rows at `PENDING`. | `docs/development/testing.md` | all figures recorded, no smoke row flipped | T62 | review |
| T64 | Add `PENDING` smoke rows for the clash surfaces — the five event-log lines, blood suppression on a turned-aside blow, the clash cross, and the swing pose under D7's risk. These rows stay `PENDING`; only a human at an interactive desktop may flip them. | `docs/development/testing.md` | rows exist, all `PENDING` | T63 | review |
| T65 | Merge `clash-integration` into `main`. | — | fast-forward or merge commit on `main` | T64 | `git log` |
| T66 | Archive both plan documents to `docs/archives/` with the "Archived: reference only" banner. | `docs/plans/`, `docs/archives/` | both moved and bannered | T65 | review |

## Verification criteria for the change as a whole

1. `./scripts/verify.ps1` passes all five stages, output pasted, not summarized.
2. V1's content hash is still `0x59FB4CA563D87A49`.
3. The defence-attributable share is inside 0.25 to 0.45 across seeds 1 to 20.
4. At least 19 of 20 seeds decide before the tick cap; median decisive tick at
   or below 5,000.
5. The collision allocation figure is at or below 900,000 bytes, or the real
   figure is reported and the ceiling raise is justified in writing.
6. `CombatMetrics` reaches neither hash, proven on the merged tree.
7. Zero references remain to the four old `WeaponId` symbols.
8. Every new tuning value carries a **Provisional reconstruction** label and
   names the band it came from.
9. No manual smoke row was flipped by an agent.
