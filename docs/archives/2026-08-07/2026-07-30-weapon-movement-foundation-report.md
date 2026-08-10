# Weapon-relative movement — shared foundation report and weapon-session handoff

> **Archived: reference only.** This document is finished work, kept so the
> decision can be traced back to its reasoning. Do not execute it and do not
> cite it as the reason to change anything.

Date: 2026-07-30
Branch: `movement-foundation`, rebased onto `main` at `ab1aabe`.
Executes: [`2026-07-30-weapon-movement-foundation.md`](2026-07-30-weapon-movement-foundation.md)
and [`2026-07-30-weapon-movement-foundation-design.md`](2026-07-30-weapon-movement-foundation-design.md).

This is the required output of the shared foundation session described in
`docs/archives/2026-08-10/2026-07-30-weapon-movement-foundation.md`. The five weapon
sessions start from this document.

## 1. Preflight findings

1. **Preset number.** `MovementPresetId = 5` was already taken by
   `PersistentContingentsV5`, the rank-aware leader scan. The new preset is
   **`EquipmentRelativeFootworkV6 = 6`**, used consistently everywhere.
2. **Task T2 was obsolete and was not executed.** `Scenario.CombatPreset`
   already defaults to `CombatPresetId.PrecolonialPhilippinesV4`
   (`Scenario.cs`), two versions past the V2-to-V3 switch the shared plan
   described.
3. **Loadout-to-combat-preset table** — see section 8, item 2.
4. **`src/Hukbo.Core/Movement/` state at start** matched the prompt: only
   `MovementPresetId.cs`, `MovementPresetRegistry.cs`, `MovementRules.cs`,
   `MovementRuleset.cs`.
5. **A fact the plan did not anticipate:** `CombatLoadout` now carries a
   fourth `RankId` component, and combat preset V4 assigns four distinct
   ranks. The movement profile is therefore keyed on
   `(WeaponId, ArmorId, ShieldId)` and ignores rank; a dedicated test proves
   two loadouts differing only in rank resolve to the same profile row.

## 2. What landed

Thirteen commits on `movement-foundation`, each gate-relevant suite green at
every step:

| Commit | Change |
| --- | --- |
| `bfe7c7a` | docs: the design document and the ordered task list |
| `4d2d9dd` | T1 — V3, V4, and V5 trajectory digest fixtures (V1/V2 already existed); provenance now records `bodyRadiusRaw` |
| `b0c9be6` | T3 — `LoadoutMovementProfile`, `Facing16`, `FacingRules`, `IntegerSquareRoot` lifted into `FixedPoint` |
| `688e782` | T10 — the 21/21/231 scenario matrix generator and its self-tests |
| `c6be88d` | T4 — `EquipmentRelativeFootworkV6 = 6` registered opt-in with the six profile rows; one-time pinned-literal move |
| `a10a214` | T5 — bounded local context, pure query, naive O(n²) oracle, hook in `SelectTargetsAndIntents` |
| `c8426f9` | T4P — equipment-aware slot assignment at the deployment seam, zero extra SplitMix64 draws |
| `decc7c9` | T6 — `TacticalPosture`, `FootworkPhase`, pure resolvers, five inert `AgentState` fields |
| `a906cec` | T7a — conditional state hashing: `ulong? movementContentHash = null`, legacy path byte-for-byte |
| `8f0df83` | T7b — full pipeline integration: posture/phase stages, routes, clearance, conflict pass + pairwise oracle, pace, `AttackAcceptedThisTick`, commit entry, death cleanup |
| `a3ef7a7` | T8 — five trailing-default `AgentView` fields, inspector rows, row budget 15 → 19 |
| `f14db0e` | T9 — `MovementBehaviorMetrics`, trailing `RunReport` member, flat `sim.tick` fields |
| `678897c` | T12 — the V6 seed-1/200-agent trajectory digest fixture and freeze test |

## 3. Tests

- Core suite, Release: **Passed! - Failed: 0, Passed: 1297, Skipped: 0,
  Total: 1297** (run by the orchestrator after integration).
- Client suite, Release: **Passed! - Failed: 0, Passed: 2829, Skipped: 0,
  Total: 2829**.
- Every task ran its focused filters red-first (TDD); each task report is in
  the session record.
- Known pre-existing Debug-only failures: `RepeatedQuietV6TicksHaveBounded...`
  style allocation tests measure Debug-build allocation noise and pass in
  Release, the gate's configuration. Verified pre-existing via stash at
  `f14db0e`.

## 4. Gate

`./scripts/format.ps1 -Verify` → `[PASS] Formatting verification completed.`

`./scripts/verify.ps1`, run once by the orchestrator after integration and
rebase, **exit 0**. Tail of the actual output:

```
  "outcome": "Faction1Victory",
  "faction0Survivors": 0,
  "faction1Survivors": 6,
  "eventHash": "AC55684F24D39344",
  "stateHash": "1B73FC5923879AA0",
  "deterministic": true,
  "firstMismatchTick": null,
  ...
  "coreAllocatedBytes": 161168,
  "movementMetrics": {
    "approachAgentTicks": 0, "engageAgentTicks": 0, "commitAgentTicks": 0,
    "recoverAgentTicks": 0, "refuseAgentTicks": 0, "disengageAgentTicks": 0,
    "regroupAgentTicks": 0, "pursueAgentTicks": 0, "postureTransitions": 0,
    "facingStepsTurned": 0, "disengagementEntries": 0, "conflictDenials": 0
  }
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

The all-zero `movementMetrics` block under the shipped default is itself
evidence: no V6 stage runs unless the preset is selected.

## 5. Determinism

- **Moved, once, as anticipated:** the six `MovementRuleset.ContentHash`
  pinned literals in `MovementPresetRegistryTests.cs`, recomputed from built
  output, never by hand:
  V1 `0x5AFC8B9FBC247363` → `0x36BAA7847258E4E3`,
  V2 `0x3E29AE36A0FAF440` → `0x0E37E2FD11051440`,
  V3 `0x520DD48EE818A603` → `0x47E9B3788ACE6783`,
  V4 `0x443ECC578E1137B5` → `0x26302C0CF6885235`,
  V5 `0x1D27722140CB87F5` → `0x84FA62B037FAC275`,
  V6 (new) `0x0FFE5D202B324D25`.
  `MovementRuleset.ContentHash` does not reach the state hash, so no replay
  moved.
- **Did not move:** the V1–V5 trajectory digest fixtures (V3–V5 captured
  before the schema change and byte-identical after every task), the three
  `DeterminismTests` pinned hash pairs, and the seed-1 canonical workload
  hashes `1B73FC5923879AA0` / `AC55684F24D39344`, reproduced after every task
  and by the final gate above.
- V6 hashes its movement content hash after the combat content hash and its
  five agent fields after the conditional rank fold; the null path is proven
  byte-for-byte legacy in both `hasRankLevels` states, with recorded
  fold-order literals pinning the order.

## 6. Performance

Movement V4 versus V6, combat `PrecolonialPhilippinesV2` (explicit), 10,000
requested ticks, seeds 1/2/3/5/8, one discarded warm run per cell, same
machine as the T0 baseline (Windows 10.0.26200, .NET 10.0.10, X64, 20 cores):

| Preset | Agents | Median elapsed (ms) | Median measured ticks | Elapsed per measured tick (ms) |
| --- | ---: | ---: | ---: | ---: |
| V4 | 200 | 368.56 | 2,037 | 0.181 |
| V6 | 200 | 3,358.32 | 10,000 | 0.336 |
| V4 | 500 | 1,262.41 | 2,934 | 0.430 |
| V6 | 500 | 9,764.40 | 10,000 | 0.976 |

**Budget verdict — reported both ways, honestly:**

- As literally written (median elapsed ratio): **9.1× at 200 and 7.7× at 500 —
  outside the 2.0× / 2.5× ceilings.** However, every V6 run draws at the
  10,000-tick limit with 118–150 survivors, while every V4 run terminates
  around tick 2,000–4,400 with one side annihilated. The raw elapsed ratio is
  measuring battle length, not stage cost, and battle length under the
  provisional defaults is exactly what the weapon sessions' calibration task
  (T11) owns.
- Normalised per measured tick: **1.86× at 200 and 2.27× at 500 — inside the
  ceilings**, with the caveat that V6's ticks carry far more living agents on
  average, so this normalisation is generous to V4, not to V6.
- Warm-tick allocation: the quiet and crowded Release allocation tests pass
  with V6 scenarios inside the existing ceilings, and `coreAllocatedBytes` for
  a full V6 10,000-tick run (142,640 at 200 agents) is below V4's (154,976),
  so the new movement stages allocate nothing per tick.

**Consequence:** no bounded-query optimisation design is warranted by this
data — per-tick stage cost is within ratio and allocation is zero. The raw
elapsed failure is a termination/calibration property. The budget must be
re-measured by T11 after the weapon sessions calibrate their rows; if the
per-tick ratio then exceeds the ceilings on comparable run shapes, the stop
rule applies and a separate optimisation design is required before any tuning
workaround.

**Behavioural flag for calibration:** under the shipped provisional defaults
every tested V6 seed at both sizes ends in a standoff draw — a real battle
for the first ~2,000 ticks (roughly a quarter to a third of each side dead),
then mutual disengagement-and-clearance equilibrium. `conflictDenials`
~130,844 and `refuseAgentTicks` ~1,140,221 on seed 1 at 200 agents say the
clearance rules bind hard. This is calibration evidence, not a foundation
defect: the disengage thresholds and clearance radii are the provisional
values the weapon sessions own.

## 7. Left out, and why

- **T2** — obsolete; the combat default is already V4.
- **T11 and every matrix simulation run** — owned by the five weapon
  sessions; this branch ships the generator, the fixtures, and the metric
  definitions they consume.
- **Manual smoke checklist** — no interactive row was flipped. Everything
  interactive remains `PENDING`; nothing was `BLOCKED`.
- **Inspector pace denominator** — `AgentView` carries no movement speed, so
  the pace row takes a trailing defaulted `movementSpeedRaw` parameter that
  the Client call site does not yet pass (the Client cannot select V6 today).
  The activation task must pass `_scenario.MovementSpeedRaw` at the
  `ArenaGame.Rendering.cs` inspector call site.
- **Default activation of V6** — expressly out of scope.
  `Scenario.MovementPreset` remains `PersistentContingentsV4`; V6 is reachable
  only through `--movement-preset EquipmentRelativeFootworkV6`.

## 8. Handoff — for the five weapon sessions

1. **Preset:** `MovementPresetId.EquipmentRelativeFootworkV6 = 6`, opt-in via
   `--movement-preset EquipmentRelativeFootworkV6` (or `6`).

2. **Loadout-to-combat-preset table.** Select your combat preset explicitly in
   every scenario, test, and benchmark:

   | Combat preset | KP | WA | KA | IT | KS | IS | Note |
   | --- | --- | --- | --- | --- | --- | --- | --- |
   | `PrecolonialPhilippinesV1` | yes | yes | no | no | yes | yes | legacy |
   | `PrecolonialPhilippinesV2` | yes | yes | yes | yes | **yes** | **yes** | **the only preset fielding all six; every shielded scenario selects it** |
   | `PrecolonialPhilippinesV3` | yes | yes | yes | yes | no | no | solo only |
   | `PrecolonialPhilippinesV4` (default) | yes | yes | yes | yes | no | no | solo only, ranked |

3. **Profile files and owners** (all under `src/Hukbo.Core/Movement/Profiles/`):

   | File | Rows | Owning session |
   | --- | --- | --- |
   | `KampilanMovementProfile.cs` | KP | Kampilan |
   | `WasayMovementProfile.cs` | WA | Wasay |
   | `KalisMovementProfile.cs` | KA | Kalis |
   | `ItakMovementProfile.cs` | IT | Itak |
   | `TallHardwoodMovementProfiles.cs` | KS and IS | Tall Hardwood |

   You own your file's values for pinning and calibration. You do not edit the
   registry, the ruleset, the pipeline, the enums, or another session's file.
   Any value change moves `MovementPresetRegistryTests`' V6 content-hash
   literal — recompute it from built output and say so in your report.

4. **Shared symbols you may call but not edit** (namespace
   `Hukbo.Core.Movement` unless noted):
   - `LoadoutMovementProfile` — the immutable row type; 15 scalars plus the
     six-cell opponent-offset array; validation bounds in its constructor.
   - `MovementRuleset.ResolveLoadoutProfile(CombatLoadout)` — rank-ignoring
     resolution; throws on an unmapped key.
   - `Facing16` / `FacingRules` — `FromDelta`, `TurnToward`,
     `SectorSeparation`, direction-band pace caps.
   - `WeaponMovementRules` — `ResolveTacticalPosture`,
     `ResolveProvisionalFootwork`, `FinalizeFootwork`.
   - `MovementRouteRules` — `StepEndpoint`, oblique vectors, clearance and
     conflict-pass rules.
   - `MovementContextQuery` / `LocalMovementContext` /
     `LoadoutCompositionCounts` — the pure context query and its value types.
   - `MovementScenarioMatrix` (namespace `Hukbo.Core.Tests.Movement`, test
     infrastructure) — `CanonicalLoadouts`, `EnumerateOneVersusOnePairs` (21),
     `EnumerateTeamCompositions` (21), `EnumerateTeamMatchups` (231); your
     sessions run these cells.
   - `MovementBehaviorMetrics` (namespace `Hukbo.Core.Simulation`) — see item
     6.

5. **Boundary-equality conventions implemented** — your plans must assert
   these, not each other's variants:
   - *Entry distance:* a target at squared distance exactly equal to the
     effective preferred distance squared enters `Engage` (inclusive `<=`),
     and preferred distance is **not a stop line** — the agent keeps closing
     to the post-movement reach gate. Kampilan and Wasay plans must restate
     their stop-line acceptance rows.
   - *Effective preferred distance:* `PreferredDistanceBasisPoints +
     OpponentDistanceOffsetBasisPoints[opponent index]`, truncating basis
     points against combat reach. Flat reach multiples hold only where the
     offset is zero.
   - *Ally clearance:* a candidate lane is unsafe when its endpoint is
     **strictly closer** than the larger of the two profiles' clearance radii;
     **exact equality is clear**. The conflict pass accepts an endpoint **at
     or beyond** that radius from every already-accepted same-faction endpoint
     (equality accepts), ordered `Disengage, Recover, Commit, Regroup, Engage,
     Approach, Pursue`, then lower `EntityId`.
   - *Disengage entry:* `SupportEnemies * 10_000 >= SupportAllies *
     DisengageEnemyToAllyBasisPoints` — equality enters.
   - *Disengage release:* `SupportEnemies * 10_000 <= SupportAllies *
     ReengageEnemyToAllyBasisPoints` — equality leaves; strictly between the
     thresholds preserves the prior state; zero living enemies never enters or
     remains.
   - *Which counts:* the ratios read the **support radius** (6 body
     diameters), self counted on the ally side; the immediate radius (2.5)
     shapes route safety only. Context membership at exactly the radius is
     **inclusive** for both radii. One implementation nuance is pinned by
     test: enemy context observation is perception-gated (the accumulation
     sits behind the existing perception test), allies are not; the naive
     oracle encodes the same rule.
   - *Posture step 6 is unconditional:* every member of a `Withdraw`/`Yield`
     contingent takes phase `Disengage`; only routes differ per agent.
     Kampilan/Wasay "cannot force synchronized retreat" rows must be restated
     as route statements.
   - *Facing:* exact dot-product ties take the lower canonical sector;
     eight-step turn ties turn clockwise in canonical space; committed turn
     budget is one sector; shield rows carry **no** facing penalty (0.88 was
     deliberately not adopted — unrepresentable in 16 sectors).
   - *"Shared engaged-entry cap"* (referenced by three weapon plans) is not a
     field: it is the direction-band pace, clamped by
     `CommittedPaceBasisPoints` during `Commit`, capped at `MovementSpeedRaw`.
   - *Second threat:* with `ImmediateEnemies >= 2` the direct lane is omitted
     only when its endpoint is strictly closer to the second threat than the
     tick-start position — equality keeps direct; `Commit`'s lone direct
     candidate is exempt.
   - *Stale numbers not to copy:* shielded Kalis preferred distance is
     **13000** (the Kalis plan's 1.10 sentence is wrong); solo-to-shielded
     rows differ in **thirteen** fields, not eight — shielded Itak's reengage
     rises 10000 → 11000.

6. **Metric definitions you may assert against**
   (`MovementBehaviorMetrics`, reported in `RunReport.movementMetrics`):
   `ApproachAgentTicks`, `EngageAgentTicks`, `CommitAgentTicks`,
   `RecoverAgentTicks`, `RefuseAgentTicks`, `DisengageAgentTicks`,
   `RegroupAgentTicks`, `PursueAgentTicks`, `PostureTransitions`,
   `FacingStepsTurned`, `DisengagementEntries`, `ConflictDenials`. All derived
   from views plus the observability-only denial counter; none reaches either
   hash.

7. **Baseline and budget.** T0 medians (this machine, seeds 1/2/3/5/8):
   combat V2 — 376.51 ms at 200, 1288.79 ms at 500; combat V4 — 314.73 ms at
   200, 1207.07 ms at 500; ceilings 2.0× at 200 and 2.5× at 500 with zero
   warm-tick movement bytes. Section 6's verdict and its termination caveat
   apply: re-measure after calibration, on comparable run shapes.

8. **Not done by this session:** T2 (obsolete), T11 (yours), the matrix
   *runs* (yours — the generator self-tests only are green), default
   activation, the inspector pace denominator at the Client call site, and
   every interactive smoke row (all `PENDING`).

## 9. Definition-of-done check

- Preflight recorded — sections 1 and 8.2. ✓
- V1–V5 freeze fixtures exist, name both presets explicitly, byte-identical
  throughout. ✓
- V6 registered, opt-in, resolves exactly six loadouts, throws on a missing
  one, shipped default untouched. ✓
- Facing, pace, posture, phase, timer authoritative, hashed for V6 only,
  snapshotted, readable in the inspector in plain language. ✓
- Production context equals the naive oracle field-for-field over permuted
  spans. ✓
- Conflict pass matches its independent pairwise oracle exactly. ✓
- Matrix generator: 21 / 21 / 231, no omissions or duplicates, self-tests
  green. ✓
- Observability reaches neither hash; disabled logging allocates nothing. ✓
- Performance budget: per-tick cost and allocation inside; raw elapsed outside
  on account of non-termination — reported honestly in section 6, re-measure
  owned by T11. ⚠
- Gate output pasted, run by the orchestrator. ✓
- No smoke row flipped. ✓
