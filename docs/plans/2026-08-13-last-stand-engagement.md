# Last-stand engagement — plan

Written 2026-08-13, out of smoke row `LS-1` and
`docs/plans/2026-08-13-last-stand-engagement-design.md`. The design document's
section 6 open question is answered: **the endgame is meant to read as two small
bands colliding**, and the remedy adopted is the design's **remedy C**, the
state-dependent trail. Remedies A and B are rejected and are not revisited here.

## Decisions this plan takes that the design did not

The design assumed a new preset version was required but did not establish where
the last-stand behaviour is reachable from. It is reachable from nowhere: the
engagement gate is a `Scenario` field (`BattleSimulation.cs:1422`, defaulted at
`Scenario.cs:260`) and the geometry is `public const` on the static
`FormationRules` (`FormationRules.cs:110`, `:189`), read through static methods
that take only a body radius. `MovementRuleset` carries no rally field at all,
and `TryComputeRegroupingAimPoint` never reads `_movementRules`. The last-stand
code is therefore shared, unversioned, by every preset from V1 to V10.

Three consequences follow, and they set the shape of every task below.

1. **A new movement preset is the only safe carrier.** Changing the shared code
   unconditionally would move V1 through V10 at once — three gate baselines and
   nine frozen digest fixtures. The new behaviour is gated on preset identity,
   the convention `RangedStandoffV8`, `MonotoneAllyClearanceV9`, and
   `BattlefieldRealismV10` already use.
2. **The shipped client must be moved onto the new preset**, because the client
   is where the tester saw the defect. `ArenaGame.BuildScenario` selects
   `BattlefieldRealismV10` today (`ArenaGame.cs:1442`); it selects the new
   preset after this change. A fix the shipped build never runs is not a fix.
3. **The design was wrong about the gate.** Its section 5 states that all three
   stage-5 baselines need re-recording. They do not: every one of the three runs
   an existing preset, and every existing preset is byte-identical after this
   change. The real gate problem is the opposite one — with no new block the
   gate would never execute a line of the new preset, exactly as it would never
   have executed V10's retreat rung without the third block added for it. Task
   11 adds a fourth block rather than repointing the third, following the
   precedent the third block's own comment records.

The new preset is `MovementPresetId.LastStandEngagementV11 = 11`, and its
registered ruleset is a verbatim restatement of `BattlefieldRealismV10Ruleset`'s
field values under its own `id`. It carries no new ruleset field, because both
new behaviours are gated on preset identity at their own call sites.

## The behaviour being built

Two yields are added to the regroup override at `BattleSimulation.cs:1418-1429`,
and both are inert under every preset before V11.

**The leader-engaged yield (design remedy C).** While the rally agent is
travelling, followers trail at the current distance and nothing changes. Once
the rally agent is itself engaged, a follower is no longer marked `Regrouping`;
it keeps `Moving` and takes the ordinary pursuit path, which already aims it at
its own nearest enemy. This is implemented as a yield rather than as a second
aim-point branch inside `TryComputeRegroupingAimPoint` on purpose: the ordinary
pursuit path already resolves the nearest enemy through the existing total order
with ties broken on `EntityId` (`BattleSimulation.cs:1378-1385`), so no second
scan is written, no new ordering rule is introduced, and the standoff, cohesion,
and sidestep paths keep applying to a follower that is now fighting.

**The own-reach yield (design section 2).** A follower whose own selected enemy
is already inside its own weapon reach is not dragged onto the trail point. The
existing override yields only at body contact, because `Attacking` is assigned at
two body radii; this yields at reach, through `IsWithinAttackRange`, the single
approved reach test (`BattleSimulation.cs:5027-5039`).

**Deviation from the design's wording, stated deliberately.** The design says the
leader yield fires when the rally agent is "in contact with an enemy". This plan
fires it at the rally agent's own weapon reach, using that same single approved
reach test, rather than at body contact. Body contact is the very threshold the
design's own section 2 identifies as too late for the follower yield, and
applying two different thresholds to the two yields would be arbitrary.

**The determinism hazard this creates, and how it is closed.** The rally agent's
own intent for the current tick cannot be read while assigning a follower's
intent: `SelectTargetsAndIntents` is one forward pass over `_agentStates`, and
the rally agent may sit after the follower in that array, so the answer would
depend on array order. The engagement flag is therefore derived in its own pass
before any intent is assigned, beside `ComputeRallyAgents`, as a minimum over
squared distances — an order-independent reduction that needs no tie-break.

## Tasks

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| 1 | Write the failing tests for both yields under V11 and the inertness tests under V10, before any production change | `tests/Hukbo.Core.Tests/Movement/LastStandEngagementV11Tests.cs` | The file exists and every new test fails for the right reason (V11 unregistered, then behaviour absent) | — | `dotnet test` showing the expected failures |
| 2 | Add `LastStandEngagementV11 = 11` with the doc comment convention V8 to V10 use | `src/Hukbo.Core/Movement/MovementPresetId.cs` | The value exists and is documented as carrying V10's behaviour plus the two yields | — | Build |
| 3 | Register the ruleset as a verbatim restatement of V10's field values, and add the `IsRegistered` and `Get` arms | `src/Hukbo.Core/Movement/MovementPresetRegistry.cs` | `IsRegistered(LastStandEngagementV11)` is true and `Get` returns it | 2 | Task 1's registration test |
| 4 | Replace the six scattered `== BattlefieldRealismV10` identity gates with one predicate that admits V10 and V11, so V11 inherits every battlefield-realism behaviour instead of silently losing it | `src/Hukbo.Core/Simulation/BattleSimulation.cs` | All six sites read the predicate; no site tests the enum directly | 2 | Task 1's V11-inherits-V10 equivalence test |
| 5 | Derive the per-faction rally-engagement flag in an order-independent pass beside `ComputeRallyAgents` | `src/Hukbo.Core/Simulation/BattleSimulation.cs` | The flag is computed before any intent is assigned, from a minimum over squared distances, under the same perception filter target selection uses | 4 | Task 1's array-permutation test |
| 6 | Add both yields to the regroup override, gated on V11 | `src/Hukbo.Core/Simulation/BattleSimulation.cs` | A follower yields when its leader is engaged or its own enemy is in its own reach, and under no earlier preset | 5 | Task 1's two behaviour tests |
| 7 | Prove the new preset terminates rather than stalling, across a seed sweep, the way the existing last-stand regression tests do for the shared path | `tests/Hukbo.Core.Tests/Movement/LastStandEngagementV11Tests.cs` | A V11 last-stand sweep reaches a terminal outcome and leaves no warrior blocked past the recorded bound | 6 | `dotnet test` |
| 8 | Pin the existing last-stand suite's preset explicitly rather than letting it inherit `Scenario`'s default, so those tests freeze the input they were written against | `tests/Hukbo.Core.Tests/LastStandFormationTests.cs` | `CreateTestScenario` names `PersistentContingentsV4`; every existing assertion is unchanged and still passes | 6 | `dotnet test` |
| 9 | Ship the new preset from the client | `src/Hukbo.Client/ArenaGame.cs` | `BuildScenario` selects `LastStandEngagementV11` | 6 | `dotnet test` on both suites |
| 10 | State the new behaviour in the game-rule document's last-stand subsection | `SIMULATION-GAME-STANDARDS.md` | The subsection records both yields and names the preset they are gated on | 6 | Read-through |
| 11 | Add the fourth stage-5 benchmark block so the gate executes the shipped preset | `scripts/verify.ps1` | The gate runs a V11 workload in addition to the three it already ran | 9 | `./scripts/verify.ps1` |
| 12 | Reopen the smoke row | `docs/development/smoke-checklist.md` | `LS-1` reads `PENDING` with its original observation intact and one sentence naming what changed | 9 | Read-through |

## Verification criteria

- `./scripts/verify.ps1`, run once after integration, with its real output
  recorded. Not delegable.
- Stage 5's three existing baselines must be **unchanged**, and each named with
  its state hash and event hash. An existing baseline that moves means the gating
  leaked and the change is wrong.
- Both test suites run. A `Hukbo.Core` change has reddened `Hukbo.Client.Tests`
  in this repository before.
- `LS-1` stays unflipped. Only a person at an interactive desktop may pass it.
