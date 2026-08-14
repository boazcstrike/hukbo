# Contingent shape — task plan (Phase C)

**Archived: reference only.** The package is finished: tasks 1 through 6 and 8
and 9 shipped, `MovementPresetId.ContingentShapeV12` is registered and selectable
in the client, task 7 is closed as accepted rather than delivered per section
6b.1, and the canonical gate ran green with all four stage-5 baselines recorded
in section 5's output block. The two smoke rows that stood beside it, `CS-1` and
`CS-2`, were run by a person and closed `PASS` on 2026-08-14. Never execute it,
never treat it as a live task list, and never cite it as the reason to make a
change. The live contract for this project remains `CLAUDE.md`,
`docs/development/testing.md`, and `docs/development/smoke-checklist.md`; nothing
in this file overrides any of them. Archived 2026-08-14.

Date: 2026-08-13
Status: plan document. This is the task-planning pass that
`docs/plans/2026-07-29-contingent-shape-design.md` asks for in its closing
paragraph, and which that document made a precondition of any implementation.

**Implementation was authorized on 2026-08-13**, after this plan was written and
after the three blocking decisions below were taken by the user. The plan as
originally written did not authorize it; section 5.0 records what changed.

What this document does is convert the design's four deliberately-open questions
into three answered ones and one that needed a person, correct four places where
the design misstates its own evidence base, and record a determinism hazard the
design does not mention at all.

## 0. Decisions taken, 2026-08-13

| Question | Decision |
| --- | --- |
| The gate — opt-in field, new preset, or both | **A new preset.** `MovementPresetId.ContingentShapeV12 = 12`. This is the route `ARMY-COMPOSITION.md` §11.1 asks for, and it is the safer of the two: every preset from V1 to V11 keeps its behaviour, its content hash, and its frozen trajectory digest byte-identical by construction rather than by argument |
| Where the founding chief stands | **Present in every contingent, but not privileged in placement.** The chief is dealt into its contingent as an ordinary member. Privileging it means the forward-most slot, which collides head-on with `CohortDeploymentAssignment`'s shield-bearers-forward ordering and would seat every leader in the highest-casualty position. This is a deliberate choice against the more elaborate option, not a deferral |
| The roster-order tie-break | **Faction-local roster index ascending, then `EntityId` ascending** — as recommended in section 2.3, matching the leader election at `src/Hukbo.Core/Movement/MovementRules.cs:96-140` |

The second decision is the one worth revisiting if the result reads wrong on
screen. It is a gameplay judgement, not a determinism constraint, and reversing
it later costs a new preset version rather than a rewrite.

## 1. What the research pass established

Four read-only research passes were run against the design on 2026-08-13: the
evidence base, the code surface, the test surface, and the hash boundary. Their
findings are folded into the sections below rather than reproduced separately.

The single most important finding is negative, and it is the reason this
document exists: **`FormationPlanner.ResolveContingentSizes` is completely
untouched.** Its body at `src/Hukbo.Core/Simulation/FormationPlanner.cs:174-190`
is still the headcount square-root split the design quotes, and
`Scenario.ContingentSizes` does not exist anywhere in `src/` or `tests/`. The
design's Phase A dependency is however satisfied: `RankId.Datu = 1` is shipped
at `src/Hukbo.Core/Combat/CombatIdentity.cs:212`.

The design's own line reference is stale. It cites `ResolveContingentSizes` at
`FormationPlanner.cs:162`; the function is at `:174` and its call site is at
`:94`. The quoted `Math.Clamp` snippet itself is accurate.

### 1.1 The determinism hazard the design does not name

This is the finding that most changes the shape of the work.

`PlanFactionDeployment` draws exactly two jitter values per warrior, in
ascending faction-local index order (`FormationPlanner.cs:116-131` and
`:325-326`). But `NextJitter` returns without drawing at all when its limit is
not positive:

```csharp
private static long NextJitter(long jitterLimit, ref SplitMix64 random)
{
    if (jitterLimit <= 0)
    {
        return 0;
    }
    ...
}
```

That is `FormationPlanner.cs:360-364`. The lattice is built from
`contingentSizes[0]` — the *first* contingent's size — at `:96`, so an unequal
split changes the lattice geometry, which can change `JitterLimit`, which
changes *whether a draw happens at all*, which shifts the SplitMix64 stream for
every warrior placed afterwards.

The consequence is that "unequal sizes only change where bodies stand" is false.
An unequal split can move the entire downstream random stream, and therefore the
whole battle, in ways that have nothing to do with deployment geometry. Any
implementation has to prove the draw count is unchanged for a scenario that does
not opt in, not merely that the positions are unchanged. This is also what
`SIMULATION-GAME-STANDARDS.md` §4's rule about adding a draw in one system not
shifting unrelated outcomes is protecting, at `:147-148`.

### 1.2 The design misstates its evidence in four places

Each of these must be corrected in the design document before any player-facing
text or code comment cites it, under the historical accuracy policy in
`CLAUDE.md` §7.

| Design lines | The claim | What the source actually says |
| --- | --- | --- |
| 31-35 | `ARMY-COMPOSITION.md` §11.1 is "the evidence-backed alternative" | §11.1 carries no evidence tier label at all, and §10 at `:528` lists "Any fixed number of fighters per leader, per boat, or per settlement" among what the sources do **not** establish |
| 55-62 | "one contingent per fielded chief" is "the natural rule" | §11.1 at `:567` says only that count follows chiefs joined rather than headcount. The strict one-to-one numeric rule appears in no source, and the design assigns it no tier |
| 140-143 | "§3 and §11.1 are explicit that barangay size varied with the individual chief's wealth and standing" | §3 at `:138-164` says nothing about wealth or standing. The documented root is Morga, at `ARMY-COMPOSITION.md:384-395` and `HISTORICAL_1500s_RANKS.md:219-228`, and the design cites neither |
| 216-218 | §11.5 answers the spectator question with "unequal sizes and a chief visibly present in each one" | §11.5 at `:632-639` claims unequal sizes are visible and that a leader whose death breaks a contingent is visible. It makes no claim about a chief being visibly present in each contingent |

Three of the design's citations are accurate and need no change: the
thirty-to-a-hundred-fighters band with its `Provisional reconstruction` label
(design lines 96-102, matching `ARMY-COMPOSITION.md:158-164` including that
document's own instruction not to present it as a historical measurement), the
argument against a per-rank follower capacity constant (design lines 143-145),
and the observation that the current square-root derivation has no historical
content (design lines 26-29).

The Mactan and Bangkusay figures (design lines 41-44) are reproduced faithfully
from §11.1, but §11.1's gloss that they are "consistent with a small number of
separately led groups" drops §5's own disclaimer at `:304-308` that three
divisions is "not evidence of a standing three-part organization, a fixed
division size, or a name for such a body". Twenty to thirty boats is also not a
small number, and exceeds `MaximumContingents = 8` outright.

## 2. The design's four open questions, answered

The design's section 8 lists four questions it deliberately leaves open. Three
are now closed by evidence. One is not, and it is not closable by research.

### 2.1 The content-hash boundary — CLOSED

The design's section 6 hedges that a content-hash claim needs confirming, and
section 8 lists the mechanism as an open item. It is answerable in one sentence:
**no `Scenario` field can enter `CombatRuleset.ComputeContentHash`, by type
signature.** The constructor at `src/Hukbo.Core/Combat/CombatRuleset.cs:57-67`
takes ten parameters and none of them is a `Scenario`. The only coupling is that
`scenario.CombatPreset` selects which ruleset is fetched, at
`BattleSimulation.cs:579`, and what is folded is that ruleset's own `Id` at
`CombatRuleset.cs:761`.

The design's state-hash claim is likewise confirmed, and is stronger than the
design realised. Deployment positions reach the state hash through
`FormationPlanner` output, `BattleSimulation.cs:618`, `CreateAgent` at
`:1092-1126`, `AgentState.cs:49-50`, and `StateHasher.cs:137-138`. The design
does not mention that `ContingentId` itself is also folded, at
`StateHasher.cs:156`.

### 2.2 Where the validation belongs — CLOSED by precedent

The design asks whether `ContingentSizes` validation belongs on `Scenario` or on
a new value type. `Scenario.RosterCounts` at
`src/Hukbo.Core/Simulation/Scenario.cs:151-162` is exactly the shape proposed —
an `ImmutableArray<int>` that defaults empty, changes what is deployed when
supplied, and is validated in the constructor at `:327-356` against a preset
roster count and a sum-equals-`AgentsPerFaction` invariant. It shipped with no
new preset id and is not folded into `StateHasher.Compute` directly at all.

Copy that pattern exactly. No new value type.

### 2.3 The roster-order tie-break — ANSWERED, needs sign-off

The design asks how "the first `MaximumContingents` chiefs by roster order" is
made a total order. The answer that matches every other multi-result query in
the codebase is **faction-local roster index ascending, then `EntityId`
ascending**. This is the same discipline the leader election already uses at
`src/Hukbo.Core/Movement/MovementRules.cs:96-140`, which scans rank ascending
then `EntityId` ascending.

This is a proposal, not a decision already taken. It is cheap and it is
conventional, but it is still a rule about who leads, so it wants a person's
nod before it is written.

### 2.4 Where the founding chief stands — STILL OPEN, and now harder

The design asks where inside a contingent's lattice cell its founding chief is
placed, and whether that placement is privileged. This cannot be answered by
research, and it has become harder to answer since the design was written.

`CohortDeploymentAssignment.AssignForFaction`
(`src/Hukbo.Core/Movement/CohortDeploymentAssignment.cs:47`) already owns
intra-contingent slot ordering under `BattlefieldRealismV10` and
`LastStandEngagementV11`: it reassigns contingent membership by weapon cohort
and pairs warriors against slots so that shield bearers take the forward-most
positions. A chief-placement rule would be a second claim on the same ordering,
and the two would have to be reconciled rather than merely composed.

Nothing here reads `RankId` at deployment time. The only deployment-time rank
read is at `BattleSimulation.cs:1110`, and it resolves a fighter level, not a
position.

**This blocks task 6 below.** It is a gameplay decision, not a research finding.

## 3. Two claims in the design that are refuted

### 3.1 "This change cannot reuse the preset-version pattern" — REFUTED

Design section 6's final paragraph argues that because `FormationPlanner` is not
gated behind a `MovementPresetId`, the change cannot use the preset-version
pattern and the opt-in field must be the gate instead.

The premise is true: `PlanFactionDeployment` is called unconditionally at
`BattleSimulation.cs:618-620`, before the movement ruleset is even fetched at
`:644`, and no `MovementPresetId` appears anywhere in `FormationPlanner.cs`.

The conclusion does not follow. The deployment *pipeline* is already
preset-gated three times immediately downstream of that ungated call:
`BattleSimulation.cs:645-662` reassigns slots when the ruleset declares
equipment-relative footwork, and `:663-692` reassigns contingent membership
through `CohortDeploymentAssignment` when the preset is V10 or V11, tested at
`:5170-5172`. Changing deployment behind a preset gate is shipped practice, not
a novelty. `PlanFactionDeployment` also already receives the whole `Scenario`
(`FormationPlanner.cs:84-86`), so it could gate on `scenario.MovementPreset`
directly if that were wanted.

Both gating routes are open. The design presents only one and calls the other
impossible. This matters because `ARMY-COMPOSITION.md:576-578` — the design's
own cited source — says the opposite of the design: "Any such change is a **new
movement preset version** with new golden expectations, under the rules in
`SIMULATION-GAME-STANDARDS.md` §4."

Which gate to use is a decision. See task 1.

### 3.2 "The shipped V4 roster always fields at least one Datu" — REFUTED

Design section 2 treats the zero-chiefs case as hypothetical on the grounds that
the shipped V4 roster always fields a Datu.

The client does not ship V4. `ArenaGame.BuildScenario` at
`src/Hukbo.Client/ArenaGame.cs:1449-1453` overrides the `Scenario` defaults with
`CombatPresetId.PrecolonialPhilippinesV5` and
`MovementPresetId.LastStandEngagementV11`, then supplies spectator-authored
`RosterCounts` at `:1456-1461`. Those counts validate only for non-negativity
and sum-equality, so a spectator can legally field a faction with a `DatuCount`
of zero.

**The zero-chiefs fallback is reachable in the shipped game, not hypothetical.**
The design's proposed fallback — a chiefless faction becomes exactly one
contingent — is still the right answer, but it needs a real test rather than a
remark.

## 4. What the test surface costs

Thirteen tests live in `tests/Hukbo.Core.Tests/FormationPlannerTests.cs`. Nine
of them break if `ResolveContingentSizes` changes its output unconditionally;
three pin it directly:

| Test | Line | Pins |
| --- | --- | --- |
| `ADefaultArmyOpensAsFiveSeparatedContingentsOfEqualSize` | `:131` | five contingents of twenty for two hundred agents |
| `ALargeArmyStopsAtEightContingents` | `:154` | saturation at eight, sizes equal |
| `MembershipDealsRoundRobinAcrossContingentsOnBothPlacementPaths` | `:338` | round-robin `ContingentId` assignment |

Beyond those, nine frozen trajectory digests in
`tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs` would move, along with the
`ZeroInterceptionProfile` fixtures in `DeterminismTests.cs`, and the Client-side
contingent row test at `tests/Hukbo.Client.Tests/AgentInspectorContentTests.cs:447`
would redden too — a Core change reddening a Client test, which this repository
has been caught by before.

**None of that happens under the opt-in gate.** A scenario that does not supply
`ContingentSizes` deploys identically, so those tests do not move. The blast
radius above is the argument *for* the opt-in, and it is why task 2 comes before
everything else.

There is one gap worth naming plainly. The nine freeze tests cover V1 through
V9. **`BattlefieldRealismV10` and `LastStandEngagementV11` have no frozen
trajectory digest at all**, and neither has a pinned content-hash literal. V11
is what the client actually ships, and both are the presets under which
`CohortDeploymentAssignment` rewrites contingent membership. The deployment path
is unfrozen precisely where a contingent-shape change would land.

## 5. Ordered task list

Each task names its files, its verification, and what it depends on. Tasks 1, 6,
and 7 are blocked on decisions and are marked so.

### Task 5.0 — note on execution order

Tasks 1, 6, and 7 were unblocked by the decisions in section 0 and are no longer
marked blocked below. Execution began on 2026-08-13 on the branch
`contingent-shape`, in a worktree taken from `653d3fa` — deliberately from the
clean commit rather than from the working tree, which carries an unrelated
session's uncommitted lethal-blow work.

### Task 1 — register `ContingentShapeV12` — DECIDED, gate only

Append `ContingentShapeV12 = 12` to `MovementPresetId`, and register it in both
switches of `MovementPresetRegistry`. Its ruleset is a verbatim restatement of
V11's field values under its own id, following the convention V8 through V11
already use: the behaviour is gated on preset identity at its own call site, so
the preset carries no new `MovementRuleset` field.

- Files: `src/Hukbo.Core/Movement/MovementPresetId.cs`,
  `src/Hukbo.Core/Movement/MovementPresetRegistry.cs`,
  `tests/Hukbo.Core.Tests/Movement/ContingentShapeV12Tests.cs`.
- Verification: the enum value is pinned against the literal `12`, not against
  the constant under test. `BattleSimulationTests.cs:1734-1736` asserts registry
  completeness over the whole enum and will fail if either switch is missed.
  Every V1-V11 content hash and frozen digest must stay byte-identical.
- Blocks: task 4.

### Task 2 — freeze the current deployment before touching it

Add a test that pins the exact deployment output — positions and
`ContingentId`s, and **the SplitMix64 stream state after planning** — for the
default two-hundred-agent scenario, for each of the shipped movement presets
including V10 and V11, which have no freeze coverage today.

The stream-state assertion is the point. Section 1.1 shows that positions alone
do not prove the draw count is unchanged, and the draw count is what shifts the
whole battle.

- Files: `tests/Hukbo.Core.Tests/FormationPlannerTests.cs`, or a new
  `tests/Hukbo.Core.Tests/FormationDeploymentFreezeTests.cs`.
- Verification: the test passes on unmodified `main`, and the digest it records
  is captured from a real run rather than hand-written.
- Depends on: nothing. **This task is unblocked and can start immediately.**

### Task 3 — add the optional `Scenario.ContingentSizes` field

Copy the `RosterCounts` pattern exactly: `ImmutableArray<int>`, defaulting
empty, defensively copied, validated in the constructor alongside the existing
roster validation at `Scenario.cs:327-356`. Validation rules, from design
section 3: every entry at least one, length at most `MaximumContingents`, and
the sum equal to `AgentsPerFaction`.

- Files: `src/Hukbo.Core/Simulation/Scenario.cs`,
  `tests/Hukbo.Core.Tests/ScenarioTests.cs`.
- Verification: the field exists and validates; task 2's freeze test still
  passes untouched, proving the field changes nothing when absent.

### Task 4 — consume the field in `ResolveContingentSizes`

Gated on `scenario.MovementPreset == ContingentShapeV12`. Under any other
preset, return exactly what the function returns today, by the same code path,
with the same number of random draws. Under V12, honour the field when it is
supplied.

The preset gate is what makes the "byte-identical" claim structural rather than
argued: a V1-V11 scenario cannot reach the new branch at all, whatever it puts
in the new field.

- Files: `src/Hukbo.Core/Simulation/FormationPlanner.cs`.
- Verification: task 2's freeze test passes unchanged. A new test supplies
  unequal sizes and asserts the resulting membership counts match.

### Task 5 — prove the random stream is untouched under V1 through V11

The distinct verification that section 1.1 requires, and the one most likely to
fail. Assert that the number of `SplitMix64` draws consumed by
`PlanFactionDeployment` is identical, across the map sizes that drive
`JitterLimit` to zero — the minimum map, the narrow half, and the crowded
dense-block fallback all already have tests at `FormationPlannerTests.cs:223`,
`:248`, and `:294` that establish those regimes.

The mechanism is cheaper than it looked when this plan was written.
`SplitMix64` exposes `public readonly ulong State` at
`src/Hukbo.Shared.Core/Determinism/SplitMix64.cs:18`, and the state advances by
a fixed gamma per draw, so the post-call `State` uniquely encodes the draw count
for a known seed. The assertion is a single equality on `random.State` after
`PlanFactionDeployment` returns — no counter, no instrumentation, and nothing
added to the production path.

- Files: `tests/Hukbo.Core.Tests/FormationPlannerTests.cs`.
- Verification: post-call `State` equal. If it is not, tasks 3 and 4 are wrong
  and stop.

### Task 6 — chief-derived contingent count

Design section 2's proposal, now gated behind V12: one contingent per fielded
`Datu`, floored at one for a chiefless faction, capped at `MaximumContingents`
with surplus chiefs dealt in as ordinary members. Section 2.3's tie-break
applies — faction-local roster index ascending, then `EntityId` ascending.
Section 3.2 shows the chiefless case is reachable in the shipped game and needs
a real test rather than a remark.

This is separable from tasks 3 through 5 and should not be bundled with them.

### Task 7 — chief present in every contingent, placement unprivileged

Design section 4, resolved by the second decision in section 0. Each contingent
is founded around one `Datu`-rank agent, so a chief is present in every one.
The chief is **not** given a privileged slot: it is dealt in as an ordinary
member and takes whatever position the existing ordering gives it.

That leaves `CohortDeploymentAssignment.AssignForFaction`
(`src/Hukbo.Core/Movement/CohortDeploymentAssignment.cs:47`) as the sole owner
of intra-contingent slot ordering, with its shield-bearers-forward rule intact
and unamended. Nothing in this task competes with it.

- Verification: under V12, every contingent contains at least one `Datu`-rank
  agent whenever the roster fields at least as many chiefs as contingents. Under
  V1-V11, membership is unchanged.
- **CLOSED AS ACCEPTED, 2026-08-14. Not delivered, and deliberately not.** The
  verification above holds at `FormationPlanner.PlanFactionDeployment`'s own
  output and is undone downstream by `CohortDeploymentAssignment`; section 6b.1
  records the decision and its reasoning. This task is not reopened by a future
  session without a new decision.

### Task 6 — chief-derived contingent count — BLOCKED, needs section 2.4 decided

Design section 2's proposal: one contingent per fielded `Datu`, floored at one
for a chiefless faction, capped at `MaximumContingents` with surplus chiefs
dealt in as ordinary members. Section 2.3's tie-break applies. Section 3.2 shows
the chiefless case is reachable in the shipped game and needs a real test.

This is separable from tasks 3 through 5 and should not be bundled with them.

- Blocked by: the placement question in section 2.4, and by task 1.

### Task 7 — rank-aware deployment — BLOCKED, needs section 2.4 decided

Design section 4. Cannot start until it is decided how a chief-placement rule
reconciles with `CohortDeploymentAssignment`'s existing claim on intra-contingent
slot ordering.

### Task 4a — the dealing rule, found during implementation

Not in the original task list, and required. The warrior-dealing loop at
`FormationPlanner.cs:116-131` dealt with `localIndex % contingentSizes.Length`,
which reproduces the square-root split's near-equal counts and silently ignores
an authored array. It was replaced with a cursor that still advances one
contingent per warrior and wraps, but skips any contingent already at its own
declared size.

Two things make this safe rather than alarming. For the square-root split no
contingent is ever full ahead of its own natural turn — the remainder goes to
the earliest contingents, so the skip never fires — which makes the new rule
byte-identical to the old one for V1 through V11. And the round-robin property
the original comment defends, that warriors are not dealt in contiguous runs, is
preserved.

**Known constraint, recorded rather than fixed.** The skip loop has no
defensive bound. If `sum(contingentSizes) < warriorCount` it spins forever
instead of throwing. That is unreachable through the production path —
`BattleSimulation.Create` calls `scenario.Validate()` at `:608` before
`PlanFactionDeployment` at `:618`, and validation enforces the sum — but
`PlanFactionDeployment` now carries an unstated precondition that its
`Scenario` has been validated. A hang is a worse failure than a throw, and a
future caller that skips validation would find that out the hard way.

### Task 8 — correct the design document

Fix the four evidence misstatements in section 1.2, the stale `:162` line
reference, the refuted "cannot reuse the preset-version pattern" paragraph, and
the refuted "shipped V4 roster" claim. Add the section 1.1 determinism hazard,
which the design does not mention. Close section 8's three answered questions
and leave the fourth open.

- Files: `docs/plans/2026-07-29-contingent-shape-design.md`.
- Verification: none needed; this is a documentation correction.
- **DONE, 2026-08-13.** Eight corrections applied inline, each marked
  **Corrected 2026-08-13**, and section 8's three answered questions closed in
  place rather than deleted.

### Task 9 — fix the stale cross-reference in the backlog document

`docs/plans/2026-07-30-formation-blocking-baseline.md:116-120` says four related
documents are "already in `docs/plans/`". Two of them —
`2026-07-28-collision-resolution-scaling-design.md` and
`2026-07-29-approach-sidestep-design.md` — were archived and are no longer
there. They are named in backticked prose rather than linked, so this is not an
archive-link violation, only a false location claim.

- Files: `docs/plans/2026-07-30-formation-blocking-baseline.md`.
- **DONE, 2026-08-13.** The section now names only the two documents still in
  `docs/plans/`, says in prose that the other two were archived, and its
  "fifth parallel account" count was corrected to "third".

## 6. Verification criteria for the package as a whole

- `./scripts/verify.ps1` green, with its real output pasted, before anything is
  integrated. The gate is not delegated to a sub-agent.
- Both suites run, not just Core. A `Hukbo.Core` change has reddened
  `Hukbo.Client.Tests` in this repository before, and
  `AgentInspectorContentTests.cs:447` reads contingent state.
- Task 2's freeze test passing after tasks 3 and 4 is the load-bearing evidence
  that existing scenarios are unmoved. Task 5's draw-count assertion is what
  makes that claim honest rather than merely positional.
- No smoke row may be flipped by any agent. If this work reaches the screen it
  earns new rows in `docs/development/smoke-checklist.md`, left `PENDING` for a
  person. The nearest rows were `BR-1` and `BR-4`, both about how a contingent
  reads on deployment; both were run by a person and closed `PASS` on
  2026-08-14, and their family has left the checklist.

## 6a. Result, 2026-08-13

`./scripts/verify.ps1 -SkipBootstrap`, run **twice**. The first run was against a
branch base that had gone forty commits stale while the work proceeded, so it was
not evidence about what would actually be merged. The branch was rebased onto
`8033410`, the `docs/plans/README.md` conflict resolved in favour of main — which
had meanwhile archived the auto camera centring pair, and had already picked up
this plan's own index row — and the gate was then run again. The second run is
the evidence, and it is what merged as `52b5f0b`:

```
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
[PASS] Release repository tests completed.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

Suites: `Hukbo.Core.Tests` 2,539 passed, `Hukbo.Client.Tests` 3,724 passed, no
failures and no skips. Every V1-V11 frozen trajectory digest and deployment
fixture is byte-identical; `git diff --stat -- tests/Hukbo.Core.Tests/Fixtures`
is empty across the whole branch.

**What the gate did not do, stated plainly.** Its headless workloads report
`"combatPreset": 5, "movementPreset": 11`. The gate never executed a single line
of `ContingentShapeV12`'s new behaviour. All V12 coverage comes from the unit
suites. A green gate here is evidence that V1-V11 are unmoved — which is the
thing most worth proving — and is not evidence that V12 is correct.

## 6b. Task 7 is not delivered, and the reason is structural

Contingent **count** is chief-derived under V12, and that works. **A chief in
every contingent does not**, and no amount of care in `FormationPlanner` will
make it, because two shipped features want opposite groupings:

- `CohortDeploymentAssignment` groups a contingent by **weapon cohort**. V12
  inherits it from V10 and V11.
- Chief-per-contingent groups by **rank**.

A set of warriors cannot be simultaneously partitioned by weapon and by rank.
`FormationPlanner.PlanFactionDeployment` does place one chief per contingent,
and then `CohortDeploymentAssignment` reassigns membership by weapon immediately
downstream and undoes it. With the shipped rosters the chiefs largely share a
weapon, so they land together.

This is pinned by a deliberately, honestly named regression test,
`CohortDeploymentAssignmentCanConcentrateEveryFieldedChiefIntoOneContingent` in
`tests/Hukbo.Core.Tests/Movement/ContingentShapeV12Tests.cs:153`. It asserts the
behaviour that actually happens, not the behaviour that was wanted. **The
guarantee holds only at `PlanFactionDeployment`'s own output, not at spawned
agent state.**

Three ways forward, none of them free, all needing a decision:

1. **Accept it.** Ship chief-derived contingent *count* and drop the
   chief-per-contingent claim. Cheapest, and it makes the design's section 4
   and its answer to acceptance question 1 wrong — both would need correcting.
2. **Make `CohortDeploymentAssignment` rank-aware** under V12, reserving one
   slot per contingent for a chief before cohort grouping runs. This reopens
   the placement question that section 0 decided, and it competes with the
   shield-bearers-forward rule for the same slots.
3. **Exclude V12 from cohort deployment.** Then V12 stops being a superset of
   V11 and loses a shipped behaviour, which is the thing two separate fixes
   this session went to some trouble to establish.

Nothing here should be chosen under time pressure. Until it is, V12's honest
description is "contingent count follows fielded chiefs, and sizes may be
authored" — not "every contingent has a chief".

### 6b.1 Resolved 2026-08-14: option 1, accept

The three options were priced against the code in
[`2026-08-14-contingent-chief-membership-design.md`](2026-08-14-contingent-chief-membership-design.md),
and the user took **option 1** on 2026-08-14. Task 7 is closed as accepted, not
delivered: V12 ships chief-derived contingent *count*, the chief-per-contingent
claim is withdrawn, and `docs/plans/2026-07-29-contingent-shape-design.md`
section 4 and its acceptance answer 1 were corrected to say so.

The two findings that decided it:

- **Option 2 pays a visible cost for an invisible benefit.** Reserving a chief
  slot before weapon grouping never pushes a chief in front of a shield bearer
  — every shipped `Datu` roster row is `ShieldId.None`, so `ShieldRank`
  (`src/Hukbo.Core/Movement/CohortDeploymentAssignment.cs:252`) already sorts
  chiefs behind every shield bearer in their contingent. But it does move the
  cohort cut boundaries by one per contingent, redistributing shield bearers
  between contingents, and that *is* on screen. What it buys is discoverable
  only by clicking agents one at a time and joining the inspector's contingent
  row against its rank row.
- **Option 3 loses two behaviours it never meant to touch.**
  `UsesBattlefieldRealism` (`src/Hukbo.Core/Simulation/BattleSimulation.cs:5202-5205`)
  gates three behaviours at once, and avoiding that means a third closed preset
  gate in the same file — the duplication that already produced two missed call
  sites in this package.

**The prerequisite that was done first.** V12 was registered but absent from
the client's player-facing preset selector, so no spectator could reach it.
That was two list entries in `src/Hukbo.Client/UI/ArmyCompositionPanel.cs`, and
the Client test that should have caught the absence —
`EveryRegisteredMovementPresetHasAMatchingDisplayName` — never consulted
`MovementPresetRegistry` and now does. That work is recorded in the archived
plan titled "Contingent chief membership — task plan", named here in prose
because nothing outside `docs/archives/` may link into it.

## 7. Status

All three blocking decisions were taken on 2026-08-13 and are recorded in
section 0. Nothing in this plan is blocked any more.

**Updated 2026-08-14.** Task 7 is closed as accepted rather than delivered, per
section 6b.1. Tasks 1 through 6 shipped. The package is finished, and V12's
honest description is settled: contingent count follows fielded chiefs, sizes
may be authored, and no claim is made about a chief being present in every
contingent.

Tasks 8 and 9 are done, in the main checkout, because both are documentation
edits. Tasks 1 through 7 are executing on the branch `contingent-shape`, in a
worktree taken from `653d3fa`.

**Nothing here is verified until `./scripts/verify.ps1` has run green on the
integrated branch and its real output is recorded.** A per-task `dotnet test` is
not that. The gate is run once, after integration, and it is not delegated to a
sub-agent.

One thing this plan still does not do: it flips no smoke row. If the unequal
contingents and the chief-per-contingent reach the screen, they earn new rows
left `PENDING` for a person. The rows that stood beside this work, `BR-1` and
`BR-4`, were run and closed `PASS` on 2026-08-14 and are no longer live.
