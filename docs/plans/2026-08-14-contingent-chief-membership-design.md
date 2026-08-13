# Contingent chief membership — design

Date: 2026-08-14
Status: **design only. This document does not authorize implementation**, under
`CLAUDE.md` section 6. It exists to make one decision decidable, not to take it.

The decision it serves is section 6b of `docs/plans/2026-08-13-contingent-shape.md`
— task 7 of that plan, "chief present in every contingent", which is not
delivered and which that section says needs a person. Three options were
recorded there. This document establishes what each one actually costs against
the code on disk, and it adds a fourth question that turns out to outrank all
three.

Everything below is checked against the working tree at `7dc1ddf`. Where the
briefing that commissioned this document disagrees with disk, disk wins and the
disagreement is tabled in section 9.

**Amended 2026-08-14, after `CohortLateralSpreadV13` landed mid-flight.** While
this document was being written, another session shipped
`MovementPresetId.CohortLateralSpreadV13` and made it the client's default
(`src/Hukbo.Client/Settings/ClientSettingsStore.cs:91-92`). Three things follow,
and none of them changes a conclusion here:

- Every reference below to V11 as "the client's default" now names V13 instead.
  V11 remains the preset the measurements in section 5.2 were taken against, and
  those numbers stand as recorded.
- **The section 5.4 defect repeated itself immediately.** V13 was appended to
  `ArmyCompositionPanel.MovementPresetOptions` while V12 was still missing from
  it, so the shipped selector went from skipping one registered preset to
  skipping one out of thirteen — with a green suite, because the test that
  should have caught it still only compared the two lists against each other.
  That is the strongest possible argument for the strengthening in section 7.1,
  and it is why that work was done first rather than alongside.
- The recommendation is unaffected. Nothing about V13 bears on whether a chief
  belongs in every contingent.

---

## 0. The question that comes first: is this feature reachable at all?

**Answer, in three sentences.** `MovementPresetId.ContingentShapeV12` is
reachable and observable today through the headless runner — `--movement-preset`
accepts it (`src/Hukbo.Headless/HeadlessRunner.cs:259-268`), and a real seed-1
run under it produces a different battle from V11, terminating 87 ticks later
with a visibly tighter army (front width 564,438 raw against V11's 728,288). It
is **not** reachable in the shipped client, because the player-facing selector
`ArmyCompositionPanel.MovementPresetOptions` stops at V11
(`src/Hukbo.Client/UI/ArmyCompositionPanel.cs:111-125`) and the default is pinned
to V11 at `src/Hukbo.Client/Settings/ClientSettingsStore.cs:85` — since amended
to V13 at `:91-92`, with V12 still absent from the option list. The selector
already exists and already works, so the prerequisite is not "build a preset
selector" — it is appending one entry to a list and one display string beside
it, which is far cheaper than any of the three options in section 6b and should
be done before any of them.

That answer is expanded, with its measurements, in section 5.

---

## 1. Section A — exactly where and how the chief distribution is destroyed

### 1.1 The distribution exists before the pass runs

`FormationPlanner.PlanFactionDeployment` produces one contingent per fielded
chief under V12 (`src/Hukbo.Core/Simulation/FormationPlanner.cs:289-299`), and
its dealing loop (`:155-175`) advances one contingent per warrior and wraps.
When the chiefs occupy the lowest faction-local indices — true whenever
`RosterCounts` is supplied and the Datu row is roster row zero, which it is for
every shipped combat preset (`PhilippineCombatPresetV4.cs:219`,
`PhilippineCombatPresetV5.cs:312`, `PhilippineCombatPresetV6.cs:253`) — the
first `fieldedChiefCount` indices land on every contingent id exactly once. That
is pinned at `tests/Hukbo.Core.Tests/FormationPlannerTests.cs:641-668`, and the
test's own summary is careful to say it proves the property at the planner's
output and nowhere else.

### 1.2 The line that destroys it

`src/Hukbo.Core/Movement/CohortDeploymentAssignment.cs:170`:

```csharp
newContingentIdByWarrior[cohortOrderedWarriors[cursor]] = contingentId;
```

That single assignment is the whole defect. It overwrites every warrior's
contingent membership with a value derived only from the warrior's position in
`cohortOrderedWarriors`, which is a weapon ordering. Nothing that produced
`cohortOrderedWarriors` has read a rank. The planner's per-contingent chief
spread is not merged with the weapon grouping, it is discarded and replaced.

### 1.3 The ordering keys, quoted

Two sorts decide what `cohortOrderedWarriors` and `contingentOrder` contain.
Neither mentions `RankId`.

**Contingents, ranked** (`CohortDeploymentAssignment.cs:128-134`):

```csharp
Array.Sort(contingentOrder, (left, right) =>
{
    // Slot count descending, then contingent id ascending (design
    // section 4.4).
    var bySize = contingentSizes[right].CompareTo(contingentSizes[left]);
    return bySize != 0 ? bySize : left.CompareTo(right);
});
```

**Warriors, ranked** (`CohortDeploymentAssignment.cs:142-155`):

```csharp
Array.Sort(cohortOrderedWarriors, (left, right) =>
{
    // Cohort member count descending, then cohort key ascending,
    // then faction-local index ascending (design section 4.4).
    var bySize = cohortSizes[cohortKeys[right]]
        .CompareTo(cohortSizes[cohortKeys[left]]);
    if (bySize != 0)
    {
        return bySize;
    }

    var byKey = cohortKeys[left].CompareTo(cohortKeys[right]);
    return byKey != 0 ? byKey : left.CompareTo(right);
});
```

A cohort key is the warrior's row index inside `CombatRuleset.Roster`, resolved
by exact structural match at `:322-337`. The loop at `:165-173` then lays the
cohort-ordered list end to end and cuts it into contiguous runs sized to each
contingent's own original slot count. A cohort smaller than one contingent's run
therefore lands wholly inside a single run — one contingent — whatever the
planner intended.

### 1.4 Why the shipped rosters make this near-total, not partial

The Datu row is exactly one roster row in every shipped preset, so "the chiefs"
are exactly one cohort. Worked through for the configuration the fourth gate
workload most resembles — `PrecolonialPhilippinesV5`, 200 agents, no authored
`RosterCounts`, so loadouts resolve by `CombatRuleset.ResolveLoadout` at
`src/Hukbo.Core/Combat/CombatRuleset.cs:517-529`, which is a plain round robin
over the nine-row roster:

- Faction-local indices 0..99 map to roster rows `index % 9`. Row 0 (Datu)
  therefore takes indices 0, 9, 18, … 99 — **twelve chiefs**; the other eight
  rows take eleven each.
- `ResolveContingentSizesByChiefCount` clamps 12 to `MaximumContingents = 8`
  (`FormationPlanner.cs:293-296`), and `SplitEvenly` gives sizes
  `[13, 13, 13, 13, 12, 12, 12, 12]`.
- Chief residues mod 8 are 0, 1, 2, 3, 4, 5, 6, 7, 0, … so the planner does seat
  a chief in all eight contingents.
- Cohort ranking then puts the twelve-member Datu cohort first (largest cohort,
  ties broken by key), and the first run is contingent 0's thirteen slots. All
  twelve chiefs land in contingent 0. Seven contingents get none.

This derivation follows the code paths cited; it is arithmetic on those rules
rather than an instrumented count, and an implementer should re-derive it rather
than take it on trust. The general shape of it is already pinned independently,
at a different roster, by
`tests/Hukbo.Core.Tests/Movement/ContingentShapeV12Tests.cs:153-176`
(`CohortDeploymentAssignmentCanConcentrateEveryFieldedChiefIntoOneContingent`),
which asserts `Assert.Single(chiefContingents)` and
`Assert.Equal(7, chiefContingents[0])` for `RosterCounts = [8, 30, 31, 31]`.

That test is honestly named and asserts what happens rather than what was
wanted. It must not be deleted or weakened. Options 2 and 3 below make its
assertion false, and both must therefore rename and invert it deliberately.

### 1.5 One thing the pass already has, and does not use

`AssignForFaction` receives `ReadOnlySpan<CombatLoadout> loadoutsByFactionLocalIndex`
(`CohortDeploymentAssignment.cs:69-72`), and `CombatLoadout` carries `RankId Rank`
(`src/Hukbo.Core/Combat/CombatIdentity.cs:275-279`). **Rank is already in hand at
the exact line that destroys the distribution.** Option 2 needs no new plumbing
from `BattleSimulation` — only a preset discriminator, since the method
currently takes no preset argument.

---

## 2. Section B — can option 2 reserve a chief slot without displacing a shield bearer?

**Answer: yes for slot depth, no for slot distribution — and the "no" is the part
that costs something.** The two halves have to be separated, because the pass
runs in two passes and only one of them decides depth.

### 2.1 Depth: no chief displaces a shield bearer, and cannot

Within-contingent slot pairing is `AssignWithinContingent`
(`CohortDeploymentAssignment.cs:196-245`). Warriors are ordered shield-bearing
first (`:226-239`, via `ShieldRank` at `:252`), slots are ordered by depth —
canonical `XRaw` descending, toward the enemy for both factions (`:208-223`) —
and the two orders are zipped at `:241-244`.

**Proof that a chief never takes a forward slot from a shield bearer under any
shipped roster:** every Datu roster row declares `ShieldId.None` —
`PhilippineCombatPresetV4.cs:219`, `PhilippineCombatPresetV5.cs:312`,
`PhilippineCombatPresetV6.cs:253`, and V5 is the only shipped roster with
shielded rows at all, at `:319-320`, both of which are `RankId.Timawa` and
`RankId.AlipingNamamahay`. `shieldBearing[index]` is computed as
`loadout.Shield != ShieldId.None` at `CohortDeploymentAssignment.cs:100`, so
every chief scores `ShieldRank(false) = 1` and sorts behind every shield bearer
in its contingent. A chief added to a contingent is appended after that
contingent's shield block, never inserted into it.

This is also exactly what section 0 of `docs/plans/2026-08-13-contingent-shape.md`
already decided — the chief is present but not privileged in placement — so
option 2 does not have to reopen that decision as long as it confines itself to
pass 1 (membership) and leaves pass 2 (depth) alone. The plan's section 6b says
option 2 "reopens the placement decision"; on the evidence above, a
membership-only reservation does not.

### 2.2 Distribution: what is actually lost

Pass 1 is where the cost lands. Reserving one chief per contingent means cutting
the cohort-ordered list into runs of `size - 1` instead of `size`, with the
reserved chief filling the remaining slot. Every boundary in that cut moves by
one per contingent visited. Since shield-bearing cohorts are contiguous runs in
the cohort ordering, moving the boundaries moves shield bearers across
contingent lines.

Global shield-bearer count is unchanged — it is a permutation, not a
substitution. What changes is the per-contingent count: a contingent that
receives a chief loses one member to make room, and if that member was a shield
bearer, that contingent's forward rank has one fewer shield in it. Nothing in
the pass prevents a contingent from ending with zero shield bearers where it
previously had one; the cut is arithmetic on run lengths and has no shield-aware
term.

So the honest statement of the cost is: **option 2 does not push any chief in
front of a shield bearer, but it does perturb how shield bearers are distributed
between contingents, by up to one per contingent boundary.** Whether that is
visible on screen is untested and is a smoke-checklist question, not a unit-test
one.

### 2.3 The case option 2 cannot serve

If a faction fields fewer chiefs than it has contingents, some contingent gets
no chief regardless. Under V12 that is nearly impossible by construction —
contingent count *is* chief count, floored at one and capped at eight
(`FormationPlanner.cs:293-296`) — with one exception: a faction fielding zero
chiefs gets one contingent and no chief in it. `docs/plans/2026-08-13-contingent-shape.md`
section 3.2 establishes that a spectator can legally field a chiefless faction
through the client's own composition sliders, so this case is reachable and any
option-2 implementation must handle it without throwing.

---

## 3. Section C — which options keep V12 a strict superset of V11

"Strict superset" has a precise, already-pinned meaning here: V12 must reproduce
V11's full trajectory when the contingent-shaping input is neutralised. That is
what `ContingentShapeV12Tests.cs:104-126` asserts, through a control run that
authors `ContingentSizes = [20, 20, 20, 20, 20]` at `:326-329` precisely so the
chief-derived count cannot diverge from V11's square-root split.

| Option | Superset preserved? | Why |
| --- | --- | --- |
| 1 — accept | **Yes, unchanged.** No code moves at all. | Nothing is edited; the two existing byte-identity tests keep passing as they do today. |
| 2 — rank-aware cohort pass under V12 | **Yes, for the pinned control run; conditionally in general.** | The control run uses `CombatPresetId.PrecolonialPhilippinesV2`, whose roster rows (`PhilippineCombatPresetV2.cs:217-222`) declare no rank and so default to `RankId.Timawa` (`CombatIdentity.cs:279`). Zero chiefs means a chief reservation is a no-op, so the control run stays byte-identical. For any roster that does field chiefs, V12 deliberately diverges from V11 — which is the point of the feature, not a violation. |
| 3 — exclude V12 from cohort deployment | **No.** | `UsesBattlefieldRealism` (`src/Hukbo.Core/Simulation/BattleSimulation.cs:5202-5205`) gates three behaviours at once, not one: the cohort deployment at `:683-712`, the nearest-melee-threat scratch at `:271` and `:522`, and the ranged retreat rung at `:2009-2024`. Dropping V12 from that predicate loses all three. Keeping the other two means splitting the predicate — creating a *third* closed `preset is MovementPresetId…` gate in this file, which is the exact failure mode this package has already hit twice. |

Option 3 also breaks the pinned superset test directly: with cohort deployment
removed, V12's control run stops matching V11's trajectory, and
`ContingentShapeV12ProducesAByteIdenticalFullBattleToLastStandEngagementV11`
goes red with no legitimate way to re-pin it.

---

## 4. Section D — what each option costs in frozen digests

### 4.1 What is frozen, and where

- Nine trajectory digests, V1 through V9, in
  `tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-movement-v1-digest.json`
  through `…-v9-digest.json`, consumed by `MovementPresetFreezeTests.cs:124-330`.
- The pre-clash digest, `seed-1-200-agents-preclash-digest.json`.
- The deployment freeze fixture,
  `formation-deployment-freeze-digest.json`, five cases — `Default200`,
  `MinimumMap`, `HalfNarrowerThanOneBody`, `DenseBlockFallback`,
  `EightContingentCeiling` — consumed by `FormationDeploymentFreezeTests.cs:51`.
- V10 and V11 now **do** have pinned full-battle trajectories, added by this
  package: `ContingentShapeV12Tests.cs:250-261` and `:270-281`, pinning tick
  count, outcome, state hash and event fold as C# literals. The claim in
  `docs/plans/2026-08-13-contingent-shape.md` section 4 that they have no frozen
  digest at all was true when that plan was written and is now stale.

### 4.2 The proof that V1–V11 cannot move

It is structural, not argued, and it holds for all three options **provided
every new branch is gated on preset identity**:

- `ResolveContingentSizes` returns the square-root split for any preset other
  than V12 before reading any other input (`FormationPlanner.cs:231-236`), so no
  V1–V11 scenario can reach a chief-derived or authored size whatever it puts in
  `Scenario.ContingentSizes`.
- `UsesBattlefieldRealism` and `YieldsLastStandEngagement` are closed
  `is … or …` patterns (`BattleSimulation.cs:5202-5205`, `:1526-1528`).
  Appending or removing a disjunct that names V12 cannot change the boolean
  returned for a V10 or V11 input.
- `CohortDeploymentAssignment` never draws from the SplitMix64 stream — stated
  at `CohortDeploymentAssignment.cs:12-14` and at the call site,
  `BattleSimulation.cs:690-692` — so no option that edits it can shift the draw
  count that `docs/plans/2026-08-13-contingent-shape.md` section 1.1 identifies
  as the real determinism hazard.

| Option | V1–V11 digests | V10/V11 pinned trajectories | V12's own pinned assertions |
| --- | --- | --- | --- |
| 1 | Unmoved — nothing is edited. | Unmoved. | Unmoved. |
| 2 | Unmoved, provably, by the three points above. | Unmoved: a V10 or V11 input never reaches a V12-gated branch. | `CohortDeploymentAssignmentCanConcentrateEveryFieldedChiefIntoOneContingent` becomes false and must be renamed and inverted. The two byte-identity tests survive (section 3). |
| 3 | **Cannot be promised without splitting `UsesBattlefieldRealism`.** Splitting a shared predicate is an edit to code V10 and V11 execute, so the "closed pattern" argument no longer covers it and the V10/V11 pinned trajectories become real evidence rather than a formality. | At risk until re-run. | The byte-identity tests go red and cannot legitimately be re-pinned. |

Under no option may a frozen digest, a fixture, or a pinned hash be re-recorded
to get green. A moved digest under options 1 or 2 means the change is wrong;
revert and report.

---

## 5. Section E — is any of this observable by a spectator?

### 5.1 The briefing's premise is half right

It is true that nothing in `src/` selects V12: the only non-comment references
are the registry (`MovementPresetId.cs:255`,
`MovementPresetRegistry.cs:603-604`, `:640`, `:658`), the two gates
(`BattleSimulation.cs:1528`, `:5205`), and the planner's own check
(`FormationPlanner.cs:233`). The client's `BuildScenario`
(`src/Hukbo.Client/ArenaGame.cs:1468`) receives whatever preset the settings
carry, and that default is `LastStandEngagementV11`
(`src/Hukbo.Client/Settings/ClientSettingsStore.cs:85`), since amended to
`CohortLateralSpreadV13` at `:91-92`.

It is **not** true that the canonical gate reports `movementPreset: 11` for its
workloads. `scripts/verify.ps1` runs four headless workloads: the first
(`:37-43`) passes no preset at all and therefore runs `Scenario.CreateDefault`'s
values — `PrecolonialPhilippinesV6` and `PersistentContingentsV4`
(`src/Hukbo.Core/Simulation/Scenario.cs:117-118`, `:138-139`) — and the other
three (`:54-63`, `:70-79`, `:86-95`) pin V5 against V8, V10 and V11
respectively. Only the fourth reports 11. The conclusion the briefing draws is
still correct: **no gate workload executes a line of V12.**

### 5.2 But V12 already runs, and it already looks different

The runner accepts the preset by name or number
(`src/Hukbo.Headless/HeadlessRunner.cs:316-338`, applied at `:356-358`), and
`scripts/benchmark.ps1:32` and `:74-75` forward it. Two runs were made for this
document, both `-Agents 200 -Ticks 10000 -Seed 1 -Preset PrecolonialPhilippinesV5`:

| Field | `LastStandEngagementV11` | `ContingentShapeV12` |
| --- | --- | --- |
| `measuredTicks` | 2037 | 2124 |
| `outcome` | `Faction0Victory` | `Faction0Victory` |
| survivors | 18 / 0 | 18 / 0 |
| `stateHash` | `6225182B4A470F91` | `20554CA2CD9F16E8` |
| `eventHash` | `C4DABE6AF98B6BEC` | `8F03518F98EDE502` |
| `deterministic` | `true` | `true` |
| `maximumFrontWidthRaw` | 728,288 | 564,438 |
| `maximumFrontDepthRaw` | 1,116,748 | 815,919 |
| `candidatePairs` | 161,415 | 182,850 |
| `longestBlockedStreakTicks` | 220 | 447 |
| `allocatedBytes` | 652,152 | 650,336 |

Both runs exited `[PASS]`, both self-verified deterministic across the runner's
paired simulations. V12 is not vapour: it produces a different, reproducible
battle, and the deployment it produces is 22% narrower and 27% shallower,
because the chief-derived rule gives it eight contingents where V11's square
root gives five.

### 5.3 What a spectator can and cannot see

**Can see, once the preset is selectable:** contingent count and contingent size
at deployment, directly, with no panel — this is `ARMY-COMPOSITION.md` §11.5's
own discoverability claim and the front-width numbers above corroborate it.
Contingent membership per agent is already in the agent inspector
(`src/Hukbo.Client/UI/AgentInspectorContent.cs:214`, with its gameplay-model
tier note at `:225-226`), and rank is on the adjacent row at `:235`.

**Cannot see, under any of the three options:** whether the contingent a given
warrior belongs to contains a chief. Answering that costs one click per agent
and a mental join across the inspector's contingent row and rank row. Neither
the HUD nor the event feed states it. This matters for the recommendation:
option 2 buys a property that is currently only discoverable by manual survey,
so the case for it is weaker than its cost suggests until something surfaces the
property.

### 5.4 The prerequisite, and why it is small

The selector is not missing. It exists, it is player-facing, it stages a choice
consumed on the next full reset, and it lists eleven presets in enum order
(`ArmyCompositionPanel.cs:105-125`) with eleven display names beside it
(`:127-145`). V12 is absent from both lists and from nowhere else.

Nothing catches that absence. The test that sounds like it would,
`EveryRegisteredMovementPresetHasAMatchingDisplayName`
(`tests/Hukbo.Client.Tests/ArmyCompositionPanelTests.cs:344-353`), asserts only
that the two lists are the same length and that the option list has no
duplicates. It never consults `MovementPresetRegistry`, so a registered preset
missing from both lists passes it. The test's name promises registry coverage
its body does not deliver, and that is precisely how V12 shipped registered but
unselectable.

The work to make V12 reachable in the client is therefore: one entry appended to
`MovementPresetOptions`, one display string appended to `MovementPresetNames`,
and — separately worth doing — strengthening that Client test to actually
enumerate the registry. Whether the client *default* should move to V12
is a distinct and larger decision, involving `ClientSettingsStore.cs` and a
new gate workload in `scripts/verify.ps1`; it is not required to make the
feature observable, since the selector is a staged, player-driven choice.

### 5.5 Plain answer to the briefing's question

Solving task 7 before something selects V12 is **not** worth doing, and the
reason is not that the preset is unreachable — it is reachable from the headless
runner today. It is that task 7 chooses between three groupings whose only
difference a spectator could ever notice is which warriors stand beside which,
and nobody has yet watched V12 deploy on a screen. Option 2 in particular
reshuffles shield-bearer distribution between contingents (section 2.2) with no
evidence about how the current arrangement reads. Making V12 selectable is two
list entries; watching it is one smoke session; and both are prerequisites to
choosing well between options that differ mainly in how they look.

---

## 6. Section F — the nine acceptance questions

Answered for the change this document would authorize if it authorized anything,
which is the chief-membership rule itself. Where an answer differs by option it
is split.

1. **User-visible outcome.** Under option 1: none beyond what V12 already does —
   contingent count follows fielded chiefs, and authored sizes are honoured.
   Under option 2: additionally, each contingent contains at least one
   `Datu`-rank warrior at spawn, discoverable today only through the agent
   inspector's contingent and rank rows
   (`src/Hukbo.Client/UI/AgentInspectorContent.cs:214`, `:235`). Under option 3:
   contingents stop being weapon-homogeneous under V12, which is a large and
   immediately visible change, and a regression against a shipped behaviour.

2. **Tick stage and state read/written.** No tick stage. All three options act
   once, inside `BattleSimulation.Create`, between the planner call at
   `BattleSimulation.cs:649-652` and the spawn loops at `:714-728`. The state
   written is `AgentState.ContingentId`, set once at spawn and never mutated
   afterwards; that invariant is unchanged by every option.

3. **Numeric units and bounds, same-tick conflict rule.** Contingent ids are
   integers in `[0, MaximumContingents)` with `MaximumContingents = 8`
   (`FormationPlanner.cs:63`); contingent sizes are positive integers summing to
   `AgentsPerFaction`, validated at `src/Hukbo.Core/Simulation/Scenario.cs:382-408`.
   No same-tick conflict rule is needed: deployment resolves before tick zero.

4. **Total ordering and random-stream policy.** No new random stream and, more
   importantly, no new draw: `CohortDeploymentAssignment` never consults the
   stream (`CohortDeploymentAssignment.cs:12-14`, `BattleSimulation.cs:690-692`),
   so no option can shift the draw count that
   `docs/plans/2026-08-13-contingent-shape.md` section 1.1 identifies as the
   hazard. Option 2 needs one new total order — which chief founds which
   contingent — and it should reuse the tie-break already decided in that plan's
   section 0: faction-local index ascending, then `EntityId` ascending, matching
   the leader election at `src/Hukbo.Core/Movement/MovementRules.cs:96-140`.
   Every existing sort in the pass already ends in a distinct index
   (`CohortDeploymentAssignment.cs:38-45`), and any new one must too.

5. **Cache source and invalidation.** No cache. Membership is computed once and
   written to authoritative state; nothing derived is retained.

6. **Save, event, and version effect.** No new event type, no new snapshot
   field — deployment already reaches the snapshot through agent position and
   `ContingentId`. Options 2 and 3 change what V12 produces; since V12 has
   already shipped registered, and since `MovementPresetId` values are
   append-only, the strictly correct route for a behaviour change to a released
   preset is a new preset id (V13). Whether V12 counts as "released" when
   nothing in the client can select it is a judgement call and should be made
   explicitly rather than assumed.

7. **Worst-case complexity and benchmark workload.** Option 2 adds one linear
   scan for chiefs and one bounded reservation per contingent, on top of the
   existing sorts, so the pass stays `O(n log n)` in warriors per faction with
   `n ≤ AgentsPerFaction`. The workload is the one measured in section 5.2 —
   200 agents, seed 1, 10,000-tick cap. A 500-agent result is still owed for
   V12 under `SIMULATION-GAME-STANDARDS.md` section 10 and has not been taken;
   the V12 200-agent run allocated 650,336 bytes against V11's 652,152, so the
   pass is not currently an allocation regression.

8. **Spectator explanation.** This is where every option is weak, and section
   5.3 states it plainly: nothing names the chief-per-contingent property on
   screen. The inspector shows a warrior's contingent and its rank on adjacent
   rows, so the property is derivable by manual survey and by nothing faster.
   If option 2 is taken, it is incomplete until either a HUD or inspector
   affordance surfaces it, or the claim is dropped from the design.

9. **Tests that fail before and pass after.** Under option 1: none — the change
   is documentary, and the correct evidence is that the existing suite is
   untouched. Under option 2: `CohortDeploymentAssignmentCanConcentrateEveryFieldedChiefIntoOneContingent`
   (`ContingentShapeV12Tests.cs:153`) inverted and renamed, plus a new test that
   every contingent contains a `Datu` after the cohort pass for the
   `RosterCounts = [8, 30, 31, 31]` case that currently fails it, plus a
   chiefless-faction test that proves no throw, plus the unchanged byte-identity
   pair at `:104-126` proving V11 is unmoved. Under option 3: the byte-identity
   pair fails and cannot be legitimately re-pinned, which is itself the argument
   against the option.

---

## 7. Section G — recommendation

**This is a recommendation, not a decision. The decision is the user's.**

### 7.1 Do this first, before choosing between the three options

Make V12 selectable in the client and watch it. Concretely: append
`MovementPresetId.ContingentShapeV12` to `ArmyCompositionPanel.MovementPresetOptions`
(`src/Hukbo.Client/UI/ArmyCompositionPanel.cs:124`) and a matching display name
to `MovementPresetNames` (`:144`), strengthen
`EveryRegisteredMovementPresetHasAMatchingDisplayName`
(`tests/Hukbo.Client.Tests/ArmyCompositionPanelTests.cs:344`) to enumerate
`MovementPresetRegistry` so the next preset cannot go missing the same way, and
add a `PENDING` smoke row beside the then-open `BR-1` and `BR-4` for how a
chief-derived deployment reads on screen. That row is a person's to flip, never
an agent's. It became `CS-1` and `CS-2`, and a person ran and passed both on
2026-08-14; `BR-1` and `BR-4` closed the same day.

This is a small, self-contained package with no determinism surface, and it
converts an unobservable feature into an observable one, which
`CLAUDE.md` section 6 question 9 requires before the feature can be called
complete at all.

### 7.2 Then take option 1

**Recommendation: accept.** Ship chief-derived contingent count, drop the
chief-per-contingent claim, correct design section 4 and its answer to
acceptance question 1, and close plan section 6b as "accepted".

The single strongest piece of evidence is section 2.2 read against section 5.3.
Option 2 is technically clean — rank is already in hand at the destroying line
(`CohortDeploymentAssignment.cs:69-72` and `CombatIdentity.cs:275-279`), it
needs no new plumbing, it cannot move a V1–V11 digest, and it does not push any
chief in front of a shield bearer because every shipped Datu row is
`ShieldId.None`. But what it buys is a property no spectator can observe without
clicking through agents one at a time, and what it costs is a perturbation of
shield-bearer distribution between contingents — which *is* visible, and which
two separate fixes in this package went to trouble to get right. Paying a
visible cost for an invisible benefit is the wrong trade, and it stays the wrong
trade until something surfaces the benefit.

Option 3 is rejected outright: it loses two behaviours it never meant to touch
because `UsesBattlefieldRealism` gates three at once, it breaks the pinned
superset tests with no honest re-pin available, and avoiding that means creating
a third closed preset gate in `BattleSimulation.cs` — the exact duplication that
has already produced two missed call sites in this package.

If option 2 is nonetheless wanted later, section 7.1's work is still the right
first step, and the reservation must be confined to pass 1. Confining it there
is what keeps section 0 of `docs/plans/2026-08-13-contingent-shape.md` — chief
present but not privileged — intact rather than reopened.

---

## 8. What this document does not do

It does not implement anything, it does not edit the design document it
criticises, and it flips no row in `docs/development/smoke-checklist.md`. It
takes no decision; it establishes what each decision costs. Under `CLAUDE.md`
section 6 the next artefact, if any option is chosen, is a plan document with an
ordered task list — not a diff.

---

## 9. Corrections to the briefing that commissioned this document

Every claim in the commissioning brief was checked against disk. Six were wrong
or imprecise.

| Briefing claim | What disk shows |
| --- | --- |
| "`UsesBattlefieldRealism` (~:5175)" | The predicate is at `BattleSimulation.cs:5202-5205`. The doc comment starts at `:5188`. |
| "`YieldsLastStandEngagement` (~:1500)" | The predicate is at `BattleSimulation.cs:1526-1528`. |
| "`CohortDeploymentAssignment.AssignForFaction` (…:47)" | `:47` is the `internal static class` declaration. `AssignForFaction` is declared at `:69`. |
| "The canonical gate's headless workloads report `movementPreset: 11`" | Only the fourth of four does. `verify.ps1:37-43` passes no preset and runs `PrecolonialPhilippinesV6` / `PersistentContingentsV4`; `:54-63` and `:70-79` run V8 and V10. The conclusion — that no workload runs V12 — holds. |
| "NOTHING selects V12 … a preset selector / default flip is the real blocking prerequisite" | A player-facing preset selector already exists and works (`ArmyCompositionPanel.cs:105-145`); V12 is simply absent from its two lists. The headless runner already selects V12 by name or number today, and a real run under it is recorded in section 5.2. The prerequisite is two list entries, not a selector. |
| "V10 and V11 have no frozen trajectory digest" (carried from `docs/plans/2026-08-13-contingent-shape.md` section 4) | True when that plan was written, stale now: `ContingentShapeV12Tests.cs:250-261` and `:270-281` pin V10's and V11's full-battle tick count, outcome, state hash and event fold as C# literals. |

Two briefing claims were checked and found **correct**: `CohortDeploymentAssignment`
does reassign membership downstream of the planner and undo the chief spread,
and `FormationPlanner`'s dealing loop at `FormationPlanner.cs:155-175` does spin
forever rather than throw if `sum(contingentSizes) < warriorCount`, protected
only by `scenario.Validate()` at `BattleSimulation.cs:608`.
