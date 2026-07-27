# Weapon clash on preset V2 — integration plan

Date: 2026-07-27
Design: [2026-07-27-clash-preset-v2-integration-design.md](2026-07-27-clash-preset-v2-integration-design.md)
Branch: `clash-integration`, based on `main` at `de19c57`.
Source branch: `worktree-weapon-clash`, tip `3cd4bc6`, true merge base `2d88b43`.

Decisions D1 through D8 referenced below are defined in the design document.
This plan does not restate their reasoning.

## Progress

Per-task status, the working state, the deleted V1 tuning values, and the traps
that have already bitten live in
[2026-07-27-clash-preset-v2-integration-handoff.md](2026-07-27-clash-preset-v2-integration-handoff.md).
Read that before picking this plan up mid-flight.

Done so far: T01, T02, T03, T12, T13, T15, T20, T30, T35, T36, T37. Partly done
or unverified: T16, T17, T18, T19, T31, T32. Next up: T10, then T13A.

Tasks T13A, T41A, T44A, and T67 through T71 were added on 2026-07-27 by a review
pass over this plan and its design. None has been started. T13A closes a silent
determinism hole and is the most urgent of them; it belongs immediately after
T10 and T13 rather than at the end.

The feature is inert in the current tree — V1 no longer carries the clash tables
and V2 does not carry them yet, so every attack lands. No measurement taken
before T22 means anything.

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

**The retune loop runs backwards through Phase 4.** T60 is a gate, and the plan
says it repeats if the share falls outside the band. What that sentence does not
say, and what will be missed, is that retuning a single cell in T22 changes
preset V2's content hash and therefore the seed-1 pair as well. **Any T22 retune
invalidates T23, T41, and T46 and they must be re-run before T60 is retaken**,
or the goldens on the tree describe a preset that no longer exists. Re-record
the version bump and both hashes on each pass rather than only on the last one;
a pass whose goldens were never updated looks identical in `git status` to one
that needed no change.

## Phase 0 — land the merge, restore compilation

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| T01 | Merge `worktree-weapon-clash` into `clash-integration`, resolving each of the eleven conflicts to the *union* of both sides wherever the design says both are wanted. Do not attempt to make it compile yet. | all eleven conflicted files | merge commit exists, no conflict markers remain in the tree | — | `git grep -n '^<<<<<<<\|^>>>>>>>'` returns nothing |
| T02 | Sweep the 41 files still referencing `WeaponId.GreatBlade`, `HeavyChopper`, `ThrustingBlade`, `Bolo` to the V2 names `Kampilan`, `Wasay`, `Kalis`, `Itak`. Mechanical rename only — no behavior change, no retuning. **Scope the sweep to `src tests tools`. Never include `docs`:** documentation legitimately carries the old names where it records what a past branch did, and a sweep across it produces sentences like "renamed `Kampilan` to `Kampilan`". | 41 files: 17 `tests/Hukbo.Client.Tests`, 11 `tests/Hukbo.Core.Tests`, 3 `src/Hukbo.Client/Rendering`, 3 `src/Hukbo.Client/Presentation`, 2 `src/Hukbo.Core/Combat`, 2 `src/Hukbo.Client/Audio`, 2 `tools/`, 1 fixture | zero references to the four old symbols remain | T01 | `git grep -c 'GreatBlade\|HeavyChopper\|ThrustingBlade\|WeaponId.Bolo'` returns nothing |
| T03 | Record the tree as knowingly non-compiling and enumerate every remaining break, grouped by cause. This is the checklist Phase 1 works against. | none — a scratch note, not a repository file | a list exists naming every compile error and its owning task | T02 | `dotnet build` output captured and grouped |

## Phase 1 — Core structure

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| T10 | Re-key `_weaponIntercept` to `(WeaponId Defender, ShieldId DefenderShield, WeaponId Attacker)` and `_voidChannel` to `(WeaponId, ShieldId)` per D3. Leave the hard-share tables and the shield scalar alone. | `src/Hukbo.Core/Combat/ClashProfile.cs` | both resolvers take the shield half of the key; hard-share resolvers unchanged | T02 | `ClashProfile` compiles standalone |
| T11 | Replace enum-cross-product validation with range-and-consistency validation per D4. Rebuild `Neutral` as a profile resolving to zero for any key rather than a dense cross-product. | `src/Hukbo.Core/Combat/ClashProfile.cs` | `ValidateMatrix` no longer calls `Enum.GetValues<WeaponId>()`; `Neutral` needs no roster | T10 | new unit tests, T40 |
| T12 | Merge the two constructor tails: `CombatRuleset` takes both `weaponAttributes` and `clashProfile` as trailing optional parameters. Keep both member sets — `ResolveWeaponProfile`, `MaximumProfileDamagePerAttack`, `HasWeaponProfiles` from `main`; `ClashProfile`, `WithClashProfile`, `ValidateClashProfileCoversTheRoster` from clash. | `src/Hukbo.Core/Combat/CombatRuleset.cs` | both parameter sets present, every existing named-argument call site untouched | T11 | build |
| T13 | Make the clash fold conditional per D2, ordered after the weapon-attribute block. Document the fold order in the method. | `src/Hukbo.Core/Combat/CombatRuleset.cs` | `FoldClashProfile` runs only when a profile was supplied | T12 | T41 pins V1's hash unchanged |
| T13A | Carry the D3 re-key through to the content-hash fold and the two ordered accessors it reads, per D3.1. `OrderedWeaponIntercepts` orders on all three key parts and `FoldClashProfile` folds the defender shield alongside the defender and attacker weapons. The void channel separates out of `OrderedWeaponRows` into its own ordered accessor and its own fold block; the hard-share rows keep their existing weapon-keyed shape. **This is the task that stops the re-key from opening a silent determinism hole**: as the fold stands today it reduces the key to `(Defender, Attacker)`, so two profiles differing only in whether a cell describes a shielded or a bare defender hash identically, and a replay would accept one configuration as the other. | `src/Hukbo.Core/Combat/ClashProfile.cs`, `src/Hukbo.Core/Combat/CombatRuleset.cs` | the shield reaches both the ordering and the folded bytes; the void channel folds separately from the hard-share rows | T10, T13 | T41A |
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
| T41A | Prove the D3.1 fold. Two profiles identical except that one cell describes a shielded defender and the other a bare defender must produce different content hashes, and the fold must stay independent of dictionary insertion order for the three-part key. Without this case the T13A hole reopens the first time somebody simplifies the comparator. | `tests/Hukbo.Core.Tests/CombatConfigurationTests.cs` | a shield-only difference moves the hash; insertion order does not | T13A | `dotnet test` |
| T44 | Restore clash's simulation, termination, and zero-interception control tests against the merged shape. | `tests/Hukbo.Core.Tests/BattleSimulationTests.cs`, `DeterminismTests.cs` | control run still produces the pinned pre-clash digest | T16 | `dotnet test` |
| T44A | **Settle the pre-clash digest fixture before anything is built on it.** Design section 6 records the fixture as "Kept", which is an assumption nobody has executed. The fixture was captured at `7abf8fc` against preset V1, the four-loadout roster, and a content hash of `0x59FB4CA563D87A49`; the merged tree defaults to V2 with six loadouts and per-weapon damage, reach, and cooldown. The fixture is only still valid if the control run is pinned to V1, and V1's behaviour is genuinely unchanged. Run `ZeroInterceptionProfile_ReproducesThePreClashDigest` and `ZeroInterceptionProfile_ReproducesTheRecordedStateHash` first, before T45 and T46 depend on them. **If either fails, do not edit the fixture or the golden**: recapture the fixture from `de19c57` using the harness embedded in its own `provenance.harnessSource`, record the new base commit in the provenance header, and state in the plan that the control run now proves neutrality against preset V2 rather than V1. | `tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-preclash-digest.json`, `tests/Hukbo.Core.Tests/DeterminismTests.cs` | both control cases pass, and the fixture's provenance names the commit its rows actually came from | T16 | `dotnet test`, and the recapture procedure if it fires |
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
| T64 | Add `PENDING` smoke rows for the clash surfaces. At least eleven rows: the five event-log lines are distinguishable; a turned-aside blow shows no blood and no impact ring; the clash cross appears for the three contact outcomes and for neither a landed blow nor a void; **a void is distinguishable from a block**; **a void is distinguishable from a landed blow**; weapons visibly swing rather than sitting static; a swing reads as one countable action at 1x and does not smear at 4x; a clashed blow visibly recoils, a landed blow stops on the target, and a void follows through past it; the swing arc trail is visible at high zoom and absent at low; **a weapon tip is or is not visibly clipped at the arena panel edge while panning**, which is the accepted cost of the pose-blind frustum cull; and the merged silhouette under D7 reads correctly in motion with both the shield block and the swing pose present. The two void rows and the D7 row are the ones that decide something rather than confirm it: the void-versus-landed row is what settles whether the fifth outcome earns its place, and its disposition if it returns FAIL is recorded in the weapon-clash design section 3.8. These rows stay `PENDING`; only a human at an interactive desktop may flip them. | `docs/development/testing.md` | rows exist, all `PENDING` | T63 | review |
| T65 | Merge `clash-integration` into `main`. | — | fast-forward or merge commit on `main` | T64, T67, T68, T69, T70 | `git log` |
| T66 | Archive this plan, this design, and the handoff to `docs/archives/` with the "Archived: reference only" banner. | `docs/plans/`, `docs/archives/` | all three moved and bannered | T65 | review |
| T67 | Add the defensive resolution contract as `SIMULATION-GAME-STANDARDS.md` section 14, following the shape of the section 13 collision contract. It states the tick stage, the five outcomes with their pinned numeric values, the `HKBO_CLS` domain tag, the composition rule, the single enforced acceptance band, the termination criterion, the hashed fields, and the spectator channels, plus a historical boundary paragraph stating that no value in it is a measurement. **This was T67 of the weapon-clash plan and was dropped when that plan was superseded**; the standards document currently ends at section 13 and would otherwise carry no record of a shipped authoritative mechanic. | `SIMULATION-GAME-STANDARDS.md` | section 14 exists and names all eight items | T63 | review |
| T68 | Update `.claude/skills/hukbo-determinism-change/SKILL.md`: the T46 pair becomes the live baseline, the current live pair `C669281B67CF8871` / `CF8C3EDBC59C3319` moves into the superseded table with a one-line reason, and the table of hashed fields gains the resolution byte and the clash tuning values. The skill is the file an agent reads *before* touching Core, so a stale baseline there is worse than a stale one in `testing.md`. | `.claude/skills/hukbo-determinism-change/SKILL.md` | live baseline matches `docs/development/testing.md` exactly | T63 | review |
| T69 | Retire the three superseded weapon-clash documents. `docs/plans/2026-07-27-weapon-clash.md`, `-design.md`, and `-handoff.md` are still live in `docs/plans/`, and the plan's Phase 4 tasks T60 through T68 are superseded by this plan's Phase 5. An agent picking that file up would run a gate, re-record hashes, and archive documents against a branch that no longer exists. Move all three to `docs/archives/` with the banner, and add one line under each title naming this plan as its successor. | `docs/plans/`, `docs/archives/` | none of the three remains in `docs/plans/`, each names its successor | T63 | `Get-ChildItem docs/plans -Filter '*weapon-clash*'` returns nothing |
| T70 | Build the two harnesses under `tools/`. They reference `BattleEvent` — `Hukbo.Tools.MixAnalysis/CueSchedule.cs` at lines 89 to 91 and `Hukbo.Tools.CueDemand/Program.cs` at 21 and 24 — and they are deliberately outside `Hukbo.slnx` and outside the gate, so nothing else in this plan would notice them breaking on the widened factory. Fix the call sites; do not extend their behaviour. | `tools/Hukbo.Tools.MixAnalysis/`, `tools/Hukbo.Tools.CueDemand/` | both projects build | T16 | `dotnet build` on each project |
| T71 | Run the report-only 500-agent stress workload, `./scripts/benchmark.ps1 -Agents 500 -Ticks 10000 -Seed 1`, and record its pair alongside the 200-agent one. Reported, never budgeted. Both prior features produced this figure and the milestone gates in the standards expect it; dropping it here would leave the clash unmeasured at the scale the standards actually gate on. | `docs/development/testing.md` | run completes, `deterministic: true`, pair recorded | T62 | benchmark output |

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
10. A profile differing only in the defender's shield produces a different
    content hash, proving the D3.1 fold carries all three key parts.
11. The pre-clash digest fixture's provenance header names the commit its rows
    were actually captured from, and both zero-interception control cases pass
    against it.
12. `SIMULATION-GAME-STANDARDS.md` has a section 14, and the determinism skill's
    live baseline matches `docs/development/testing.md` exactly.
13. No `2026-07-27-weapon-clash*` document remains in `docs/plans/`.
14. Both `tools/` harnesses build.
15. The 500-agent workload completed and reported `deterministic: true`.
