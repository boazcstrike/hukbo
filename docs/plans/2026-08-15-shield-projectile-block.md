# Shield size against projectile size — plan

Date: 2026-08-15
Game: **Hukbo**
Design document: `2026-08-15-shield-projectile-block-design.md`, which outranks
this file wherever the two disagree.
Branch: `shield-block`, worktree `.claude/worktrees/shield-block`, based on
`main` at `cfe0c22`.

Baseline before any task: `./scripts/verify.ps1 -Game Hukbo` exited `0` on this
worktree at `cfe0c22`, with the seed-1 200-agent / 10,000-tick workload
reporting `shieldBlockedAttacks: 202`.

## Ordered tasks

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| **T1** | Shield identity, size-aware interception, projectile bulk, combat preset V7 | `src/Hukbo.Core/Combat/CombatIdentity.cs`, `ClashProfile.cs`, `WeaponProfile.cs`, `CombatRuleset.cs`, `CombatPresetRegistry.cs`, `PhilippineCombatPresetV7.cs` (new); `tests/Hukbo.Core.Tests/ShieldSizeInterceptionTests.cs` (new) | `ShieldId.NarrowBreastHigh = 3` exists; V7 registered; size-aware interception implemented and gated; V1–V6 content hashes unmoved | — | `dotnet build src/Hukbo.Core`; new test file green; `CombatRulesetTests` green |
| **T2** | Narrow-shield movement rows, slowed broad-shield rows, new movement preset | `src/Hukbo.Core/Movement/MovementRuleset.cs`, `MovementPresetId.cs`, `MovementPresetRegistry.cs`, `Profiles/TallHardwoodMovementProfiles.cs`, `Profiles/NarrowBreastHighMovementProfiles.cs` (new); `tests/Hukbo.Core.Tests/Movement/ShieldSizeMovementTests.cs` (new) | Eight-row presets legal, six still legal, seven still rejected; pace ordering solo > narrow > tall pinned; V1–V13 content hashes unmoved | T1 | New test file green; existing `TallHardwoodMovementProfileTests` and `MovementPresetRegistryTests` green |
| **T3** | Block-recovery window in authoritative state | `src/Hukbo.Core/Simulation/AgentState.cs`, `BattleSimulation.cs`, `AgentView.cs`, `BattleSnapshot.cs`, `src/Hukbo.Core/Determinism/StateHasher.cs`; `tests/Hukbo.Core.Tests/ShieldBlockRecoveryTests.cs` (new) | Counter opens on a block, decrements to zero over its authored duration, clamps pace while open, folds nothing under a preset that does not apply it | T1, T2 | New test file green; full `Hukbo.Core.Tests` green |
| **T4** | Shield drawn at its own width; exhaustive client switches gain the member | `src/Hukbo.Client/Rendering/PawnGeometry.cs`, `PawnRenderer.cs`, `AttackPoseResolver.cs`, `src/Hukbo.Client/Presentation/PawnAppearanceFactory.cs`, `AttackMotionCatalog.cs`; `tests/Hukbo.Client.Tests/ShieldSizeGeometryTests.cs` (new) | Narrow shield draws strictly narrower than broad at every detail tier, floor respected; solution builds | T1 | `dotnet build`; new test file green; existing `PawnGeometryTests` and `ShieldVisualCatalogTests` green |
| **T5** | Inspector lines and event-log wording | `src/Hukbo.Client/UI/AgentInspectorContent.cs`, `AgentInspectorPanel.cs`, `src/Hukbo.Client/Presentation/BattleEventFormatter.cs`; `tests/Hukbo.Client.Tests/ShieldSizePresentationTests.cs` (new) | Label, evidence tier, span line, live block-recovery line; block log line names the shield | T1, T3 | New test file green; existing `AgentInspectorContentTests` and `BattleEventFormatterTests` green |
| **T6** | Shipped defaults and default army composition | `src/Hukbo.Client/ArenaGame.cs`, `src/Hukbo.Client/Settings/ClientSettingsStore.cs`; `tests/Hukbo.Client.Tests/ShieldSizeDefaultsTests.cs` (new) | Client ships combat V7 and the new movement preset; default composition fields narrow-shield Kalis and Itak | T1, T2 | New test file green; existing `ClientSettingsStoreTests` and `ArmyComposition` tests green |
| **T7** | Integration, canonical gate, documentation | run by the integrator, not delegated | `./scripts/verify.ps1` output pasted; five baselines confirmed unmoved; smoke rows added as `PENDING` | T1–T6 | `./scripts/verify.ps1` exit code and printed hashes |

## Execution waves

```
Wave A:  T1
Wave B:  T2   T4         (parallel, disjoint files)
Wave C:  T3   T6         (parallel, disjoint files)
Wave D:  T5
Wave E:  T7               (integrator only)
```

The solution does not build between T1 and T4: appending a `ShieldId` member
breaks every exhaustive switch in `Hukbo.Client` that lacks a discard arm, and
`TreatWarningsAsErrors` turns that into a build failure. This is expected and is
the reason T4 sits in the first wave after T1. T1's agent verifies
`dotnet build src/Hukbo.Core` only, and reports the client files the compiler
named so T4 has the list.

## Rules binding every implementation agent

- Work in `.claude/worktrees/shield-block`. Never touch the main checkout.
- **Never run `git add`, `git commit`, `git stash`, or `git checkout`.** The
  integrator stages by pathspec. Parallel agents racing on one git index has
  broken this repository's working tree before.
- Touch only the files listed for the task. A file that appears in another
  task's row is not yours.
- Every new tuning constant carries a code comment marking it **provisional**,
  per `CLAUDE.md` §7.
- No `float`, no `double`, no `System.Random`, no wall-clock time in
  `Hukbo.Core`.
- A new field that reaches a hash must fold nothing when the preset does not
  author it. This is the single constraint most likely to be got wrong, and the
  five recorded gate baselines are how it will be caught.
- Assert against literals, not against the constant under test — a threshold
  read out of the constant it is checking moves with it and proves nothing.

## Verification criteria for the package

1. `./scripts/verify.ps1 -Game Hukbo` exits `0`.
2. All five recorded seed-1 baselines in `docs/development/testing.md` are
   unmoved: default `5460D13E3F7FD3E5` / `8E18ED1437B2924B`, ranged standoff
   `C8023D3B5BEB005E` / `F709A345E2F7370E`, battlefield realism
   `7C145A9E05916E4C` / `77626E104234206C`, last-stand
   `6225182B4A470F91` / `C4DABE6AF98B6BEC`, cohort lateral spread
   `4A0723BC9A1B924B` / `E0CE32CF8830A864`.
3. Interception against the broad shield strictly exceeds the narrow shield's
   for every weapon, and the gap widens with bulk.
4. Pace ordering solo > narrow > tall holds for both Kalis and Itak under the
   new movement preset.
5. The block-recovery counter opens on a block and closes after exactly its
   authored duration.
6. Smoke rows for the interactive claims are added to
   `docs/development/smoke-checklist.md` as `PENDING`. No agent and no
   automated run may flip one to `PASS`; only a human at an interactive desktop
   may.
