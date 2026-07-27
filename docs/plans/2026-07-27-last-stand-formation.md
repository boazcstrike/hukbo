# Last-Stand Formation — Plan

Date: 2026-07-27
Design document: `docs/plans/2026-07-27-last-stand-formation-design.md`
Branch: `worktree-last-stand-formation`

The design document is the contract. This file is the ordered task list and the
verification criteria. Read the design document first; the numeric contract, the
historical boundary, and the nine standard answers are recorded there and are
not repeated here.

## Outcome

All eight tasks are complete, plus three corrective tasks that the plan did not
foresee. The canonical gate passes.

Testing invalidated two of the plan's numbers and one of its arguments, so the
task descriptions below no longer match the constants that shipped. The
authoritative values are in `FormationRules` and in the corrections section at
the top of the design document. In summary:

- `RallyJitterRadiusMultiplier` is 6, not the 4 written below.
- `MaximumLastStandThresholdAgents` is 9, not the 16 written below. Sixteen was
  the bias square's full packing capacity, which is not a safe headcount; the
  ceiling now carries a fourfold area margin.
- Two constants the plan never anticipated were added:
  `RallyTrailRadiusMultiplier` (12) and `RallyCorridorHalfWidthMultiplier` (2).
- Followers trail behind the rally agent and give way when they stand in its
  path. Neither rule was in the original plan, and without them the feature
  deadlocked: a leader blocked by its own followers, both factions frozen, and a
  no-casualty draw at the tick limit.

The corrective work is recorded in commits `a1415a6`
(`fix(simulation): give the rally jitter square a fourfold packing margin`),
`ca80518` (`fix(simulation): trail regrouping survivors behind their rally
agent`), and `244bf22` (`fix(simulation): make regrouping survivors give way to
their rally agent`).

## Task order

Tasks 1, 2, and 3 are independent of one another and may run in parallel. Tasks
4 through 7 are strictly serial. Tasks 5 and 6 both modify
`BattleSimulation.cs`; they must not be parallelised.

---

### Task 1 — Add the formation constants

**Files:** create `src/Hukbo.Core/Simulation/FormationRules.cs`; create
`tests/Hukbo.Core.Tests/FormationRulesTests.cs`.

**RED tests:**

- `DefaultLastStandThresholdIsSix` — asserts
  `FormationRules.DefaultLastStandThresholdAgents == 6`.
- `MaximumLastStandThresholdLeavesAFourfoldAreaMarginUnderTheJitterSquaresCapacity`
  — asserts `MaximumLastStandThresholdAgents == 9` and independently recomputes
  it from the derivation, so the constant and its derivation cannot drift apart.
  The bias square has side `12R` and a body square has side `2R`, so the ratio is
  `RallyJitterRadiusMultiplier` and the square's full packing capacity is that
  ratio squared. The approved ceiling is that capacity divided by
  `RallyPackingMargin`. Full packing capacity is deliberately not the ceiling:
  the margin keeps three quarters of the square empty so the collision resolver
  always has room to separate the gathered bodies.
- `RallyJitterRadiusForTheDefaultBodyIsTwentyFourWorldUnits` — asserts
  `FormationRules.ComputeRallyJitterRaw(CollisionRules.DefaultBodyRadiusRaw)`
  equals `24 * FixedPoint.Scale`.
- `ComputeRallyJitterRawRejectsARadiusWhoseSpanOverflowsAnInt32` — asserts the
  helper throws for a body radius of `268435456`.

**GREEN:** create `FormationRules` as a `public static class` mirroring
`CollisionRules.cs`, holding `DefaultLastStandThresholdAgents = 6`,
`RallyJitterRadiusMultiplier = 6`, `RallyPackingMargin = 4`, a
`MaximumLastStandThresholdAgents` derived as
`RallyJitterRadiusMultiplier * RallyJitterRadiusMultiplier / RallyPackingMargin`,
which is 9, and a
`ComputeRallyJitterRaw(int bodyRadiusRaw)` helper doing the `checked` multiply.
Document the packing derivation in the doc comment, and state that these values
are game-design inventions, not measurements.

**Verify:**
`dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj --filter FullyQualifiedName~FormationRulesTests`

**Commit:** `feat(simulation): add last-stand formation constants`

---

### Task 2 — Add the scenario field, validation, equality, and hash coverage

**Files:** modify `src/Hukbo.Core/Simulation/Scenario.cs`,
`src/Hukbo.Core/Determinism/StateHasher.cs`; modify
`tests/Hukbo.Core.Tests/ScenarioTests.cs`,
`tests/Hukbo.Core.Tests/DeterminismTests.cs`.

**RED tests:**

- `ValidateAcceptsAZeroLastStandThresholdAsDisabled`
- `ValidateRejectsALastStandThresholdAboveTheApprovedMaximum` —
  `FormationRules.MaximumLastStandThresholdAgents + 1`, which is 10, throws
  `ArgumentOutOfRangeException`.
- `ValidateRejectsANegativeLastStandThreshold`
- `ValidateRejectsABodyRadiusWhoseJitterSpanOverflowsWhenTheLastStandIsEnabled`
  — a body radius of `268435456` with threshold 6 throws; the same radius with
  threshold 0 does not.
- `CreateDefaultEnablesTheLastStandAtTheApprovedThreshold`
- `ScenariosDifferingOnlyInLastStandThresholdAreNotEqual` — and their
  `GetHashCode` values differ.
- `ScenariosDifferingOnlyInBodyRadiusAreNotEqual` — closes the pre-existing gap
  recorded as R1 in the design document.
- In `DeterminismTests`: `StateHashChangesWhenTheLastStandThresholdChanges`.

**GREEN:** add `public int LastStandThresholdAgents { get; init; }` next to the
other collision-related init properties. Add the two range checks and the
overflow check to `Validate()`, placed after the existing collision validation
so the body radius is already bounded. Add `LastStandThresholdAgents` to the
manual `Equals` and the manual `GetHashCode` — **and add the missing
`BodyRadiusRaw` and `CollisionPolicy` to both while there.** Set
`LastStandThresholdAgents = FormationRules.DefaultLastStandThresholdAgents` in
the object initializer inside `CreateDefault`. In `StateHasher.Compute`, add the
new field to the scenario block immediately after the collision policy.

**Note for the implementer:** the property default is `0`, not `6`. Every
production scenario is built by `Scenario.CreateDefault`, so the feature reaches
the game and the gate, while hand-built test scenarios keep today's behaviour
unless they opt in. This is deliberate and keeps the blast radius small.

**Verify:**
`dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj --filter "FullyQualifiedName~ScenarioTests|FullyQualifiedName~DeterminismTests"`

**Commit:** `feat(simulation): add the last-stand threshold to the scenario`

---

### Task 3 — Add the rally offset

**Files:** create `src/Hukbo.Core/Simulation/RallyOffset.cs`; create
`tests/Hukbo.Core.Tests/RallyOffsetTests.cs`.

**RED tests:**

- `OffsetIsStableAcrossRepeatedCallsForTheSameSeedAndEntity`
- `OffsetDoesNotDependOnTheTick` — the method takes no tick parameter; assert by
  calling it against a running simulation at ticks 1, 50, and 200 and getting
  identical results.
- `EveryOffsetInASweepOfTenThousandEntitiesStaysInsideTheJitterSquare` — both
  axes within `[-J, +J]` inclusive.
- `ASweepOfOneThousandEntitiesProducesAtLeastNineHundredDistinctOffsets` —
  guards against a degenerate mixer.
- `DifferentSeedsProduceDifferentOffsetsForTheSameEntity`
- `OffsetsAreSymmetricallyDistributedAboutZeroWithinATolerance` — the sum of a
  10,000-entity sweep on each axis stays within a stated fraction of the span,
  guarding against the low-corner bias a naive modulo would introduce.

**GREEN:** create `internal static class RallyOffset` with
`internal static (int XRaw, int YRaw) Compute(ulong seed, ulong entityId, int bodyRadiusRaw)`.
Mix through `Fnv1a` with `LastStandTag = 0x484B424F5F4C5354UL`, construct a
fresh `SplitMix64` from the mixed value, and draw two `NextInt(spanRaw)` values
shifted down by the jitter radius. All `checked`. Document in the doc comment
that the key deliberately excludes the tick, and why.

**Verify:**
`dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj --filter FullyQualifiedName~RallyOffsetTests`

**Commit:** `feat(simulation): add the deterministic rally offset`

---

### Task 4 — Append the regrouping intent

**Files:** modify `src/Hukbo.Core/Simulation/AgentIntent.cs`; modify
`tests/Hukbo.Core.Tests/BattleSimulationTests.cs`.

**RED test:** `AgentIntentNumericValuesArePinned` — asserts `Idle == 0`,
`Moving == 1`, `Attacking == 2`, `Dead == 3`, `Regrouping == 4`, and that the
enum has exactly five values.

**GREEN:** append `Regrouping = 4,` after `Dead = 3,`. Add a comment stating
that the values are pinned and append-only because they enter the state hash,
and that `Regrouping` sits after `Dead` because reordering is forbidden, not
because it is conceptually terminal.

**Verification note:** confirm no `Hukbo.Client` file needs changing. The agent
inspector interpolates the enum's `ToString()`, and the camera auto-pan compares
only against `Attacking`. Run `dotnet build -c Release` to confirm no exhaustive
switch warning fires under `TreatWarningsAsErrors`.

**Commit:** `feat(simulation): append the regrouping agent intent`

---

### Task 5 — Select the rally agent and assign the regrouping intent

**Files:** modify `src/Hukbo.Core/Simulation/BattleSimulation.cs`; create
`tests/Hukbo.Core.Tests/LastStandFormationTests.cs`.

**RED tests:**

- `TheLowestLivingEntityIdIsTheRallyAgentForItsFaction`
- `ADeadAgentIsNeverTheRallyAgent`
- `TheRallyAgentKeepsOrdinaryNearestEnemyIntent` — the leader is `Moving` or
  `Attacking`, never `Regrouping`.
- `AFollowerBelowTheThresholdIsMarkedRegrouping`
- `AFollowerWithinContactOfItsEnemyIsMarkedAttackingRatherThanRegrouping`
- `AFactionAboveTheThresholdIsUnaffected`
- `EachFactionTriggersIndependently`
- `AZeroThresholdDisablesTheFormationEntirely`
- `RallyAgentSelectionIsUnchangedByAgentArrayPermutation` — build the same
  warriors in several `CreateForTesting` orderings and assert identical intents
  and identical state hashes.
- `ASingleSurvivorIsItsOwnRallyAgentAndBehavesExactlyAsBefore`

**GREEN:** add two two-element arrays allocated once in the constructor for the
per-faction living count and rally entity ID. Add a private
`ComputeRallyAgents()` called at the very top of `SelectTargetsAndIntents`,
doing one forward scan and selecting the minimum living `EntityId` per faction
with an explicit `EntityId` comparison, never relying on array order. Add the
`Regrouping` branch in the main loop, after target selection and after the
existing contact test, guarded by `Scenario.LastStandThresholdAgents > 0`, the
living count being at or below the threshold, and the agent not being the rally
agent.

**Verify:**
`dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj --filter FullyQualifiedName~LastStandFormationTests`

**Commit:** `feat(simulation): rally the last survivors on their lowest-ID comrade`

---

### Task 6 — Aim regrouping movement at the rally point

**Files:** modify `src/Hukbo.Core/Simulation/BattleSimulation.cs`; modify
`tests/Hukbo.Core.Tests/LastStandFormationTests.cs`.

**RED tests:**

- `ARegroupingFollowerMovesTowardTheRallyAgentPlusItsOffset` — assert the
  committed position moved along the line from the follower toward the rally
  position plus its offset, not toward its enemy.
- `ARegroupingFollowerAlreadyAtItsAimPointProposesNoMovementAndEmitsNoMoveEvent`
  — assert zero `Move` events for that entity and `MovementResolution.None`.
- `AMoveEventFromARegroupingFollowerNamesTheRallyAgentAsItsTarget`
- `ARegroupingFollowerStillAttacksAnEnemyInsideReach` — place a follower next to
  an enemy but far from the rally agent; assert an `Attack` event and that the
  attack stage re-marked it `Attacking`.
- `AnAimPointOutsideTheMapIsClampedInsideTheBounds`
- `LastStandRallyDrawsDoNotChangeSpawnPositions` — two `Create` calls, one with
  threshold 0 and one with threshold 6, produce identical tick-zero positions.

**GREEN:** extend the movement-proposal guard to admit `Regrouping`. For a
regrouping warrior, compute the aim point in `long`, saturate to `int`, clamp
with `CollisionGeometry.ClampCenterToBounds`, and skip the proposal when the
squared distance from the warrior to the aim point is at or inside
`CollisionGeometry.ContactSquaredDistance(Scenario.BodyRadiusRaw)`. Otherwise
call a point-taking overload of `BuildMovementProposal` and set the proposal's
target ID to the rally agent's `EntityId`. Refactor `BuildMovementProposal` so
the existing agent-taking form delegates to a point-taking form; do not
duplicate the normalisation.

**Verify:**
`dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj --filter FullyQualifiedName~LastStandFormationTests`

**Commit:** `feat(simulation): move regrouping survivors toward their rally point`

---

### Task 7 — Lock liveness, stall, and determinism regressions

**Files:** modify `tests/Hukbo.Core.Tests/LastStandFormationTests.cs`,
`tests/Hukbo.Core.Tests/BattleSimulationTests.cs`,
`tests/Hukbo.Core.Tests/DeterminismTests.cs`.

**RED tests:**

- `BothFactionsInASixVersusSixLastStandReachATerminalOutcome` — twelve warriors,
  both factions triggered; asserts a decisive or drawn outcome well inside the
  tick limit and records the terminal tick in the failure message.
- `LivingCountsNeverIncreaseAcrossAWholeBattle` — proves the trigger cannot flap.
- `RallyAgentDeathPromotesTheNextLowestLivingEntityId`
- `AMaximumSizedLastStandNeverLeavesAWarriorBlockedTooLongAcrossSeedsOneThroughTwenty`
  — the threshold set to `FormationRules.MaximumLastStandThresholdAgents`, which
  is 9, in a battle of thirty-two warriors. The assertion message must state that
  a failure means the cluster packs tighter than the resolver permits.
- `TheSameSeedProducesIdenticalHashesAndEventsWithTheLastStandActive` — two
  independent runs to a terminal outcome, comparing every tick's state hash and
  full ordered event stream.

The existing multi-seed victory test and the canonical 200-agent termination
test **must still pass unmodified.** If either fails, that is a design signal
about the threshold, not a test to weaken.

**Verify:** the filtered Release run must pass on three consecutive invocations.

**Commit:** `test(simulation): lock last-stand liveness and determinism`

---

### Task 8 — Run the gate and re-record the oracle once

**Files:** modify `docs/development/testing.md`,
`.claude/skills/hukbo-determinism-change/SKILL.md`,
`SIMULATION-GAME-STANDARDS.md`.

1. `./scripts/verify.ps1` — must pass all five stages, with the exact output
   pasted into this plan.
2. `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1`, and again with
   `-Agents 500`.
3. From **one single final verified run**, update in the same commit:
   - `docs/development/testing.md`, "Latest non-interactive result": new
     outcome, terminal tick, state hash, event hash, allocated bytes, both suite
     counts, both workloads, and a note that this supersedes the previous
     figures.
   - `.claude/skills/hukbo-determinism-change/SKILL.md`: replace the recorded
     baseline table, move the superseded hash pair into the dead-values table
     with a one-line reason, and add `Scenario.LastStandThresholdAgents` and
     `AgentIntent.Regrouping` to the table of hashed fields.
   - `SIMULATION-GAME-STANDARDS.md`: record the last-stand contract, preserving
     the historical warning against named formations.
4. Append the smoke rows below to `docs/development/testing.md`, all `PENDING`.

**Commit:** `docs(simulation): record the last-stand formation contract and oracle`

---

## Smoke checklist rows

Append to `docs/development/testing.md` as a new section after the camera
auto-pan section, numbered from the current highest row.

The automated tests prove the trigger, the rally-agent choice, the offset, and
that a last stand still resolves. None of them prove the resulting endgame reads
as a converging last stand rather than as warriors wandering. That is the only
thing these rows are for. They may only be flipped to `PASS` by a human running
`./scripts/run.ps1` on an interactive Windows desktop.

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| Watch the endgame converge | Let a full 200-agent battle run to its final handful of warriors on each side. As each side thins out, its survivors visibly turn toward one another and gather instead of continuing to spread across the map. | Not run | PENDING |
| Confirm the cluster is irregular | The gathered survivors form a ragged clump. They do not form a ring, a grid, a line, an arc, or any shape that looks placed. No warrior sits at an obviously exact distance from the one it gathered on. | Not run | PENDING |
| Confirm the cluster advances as a body | The gathered survivors travel toward the enemy together rather than one at a time. The group arrives roughly at once, and the fight that follows is a group fight rather than a sequence of separate duels. | Not run | PENDING |
| Watch a leader fall | When the warrior the group has gathered on is killed, the group re-forms on another warrior within a moment. The re-form is a short, small adjustment, not a sudden jump across the screen or a scatter. | Not run | PENDING |
| Inspect a regrouping warrior | Selecting a survivor that is closing on its comrades shows `Intent: Regrouping` in the inspector, and the battle event log shows its movement naming the warrior it is closing on rather than an enemy. The intent changes to `Attacking` once it is actually swinging at an enemy. | Not run | PENDING |
| Confirm regrouping never stops the fight | A warrior that is regrouping still strikes any enemy it passes within reach. The final engagement is not delayed by warriors refusing to fight while they are still gathering, and the match reaches a terminal outcome rather than two clusters standing apart. | Not run | PENDING |

## Verification criteria

The feature is complete when all of the following hold.

- Every task's tests pass in Release.
- `./scripts/verify.ps1` reports
  `[PASS] Canonical repository verification completed.`
- The 200-agent, 10,000-tick, seed-1 headless workload reports
  `deterministic: true` with no `firstMismatchTick`.
- The state hash and event hash have moved, the move is explained in the commit
  message as an authoritative movement change, and the new values are recorded
  in both `docs/development/testing.md` and the determinism skill file from the
  same run.
- The existing multi-seed victory test and the canonical termination test pass
  unmodified.
- The smoke rows above are present and `PENDING`.
