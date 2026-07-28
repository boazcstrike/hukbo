# Leader rank — implementation plan

> **Archived: reference only.** Completed on 2026-07-29. Tasks L1 through L6
> all landed and the canonical gate passed. Do not execute this plan; its task
> list and verification steps are historical. The live design is
> `docs/plans/2026-07-29-leader-rank-design.md`.

Date: 2026-07-29
Design: [`2026-07-29-leader-rank-design.md`](2026-07-29-leader-rank-design.md)
Parent design: [`2026-07-29-warrior-standing-design.md`](2026-07-29-warrior-standing-design.md)
§6.3, Decisions items 1 and 3
Evidence: [`docs/research/ARMY-COMPOSITION.md`](../research/ARMY-COMPOSITION.md)
§2, §7, §11.4

This plan is scoped to leadership only: the rank-aware leader scan, its reach
into cohesion geometry and rally direction, the client-visible marker, and
the inspector annotation. It is not the rank ladder itself — `RankId`, the
`CombatLoadout.Rank` field, `AgentState.Rank`/`AgentView.Rank`, the
per-rank level table, and `CombatPresetId.PrecolonialPhilippinesV4` — which
is `docs/plans/2026-07-29-warrior-standing.md`'s Phase A. Every task below
reads `AgentState.Rank` or a `RankId` value that Phase A must have already
landed and gated for this plan's tasks to compile.

**Blocking finding, recorded here because it changes the order of work
rather than because it is this plan's task to fix:**
`docs/plans/2026-07-29-warrior-standing.md` (the Phase A task list) still
names `StandingId`, `CombatLoadout.Standing`, and `AgentState.Standing`
throughout. `docs/plans/2026-07-29-warrior-standing-design.md`'s own
Decisions section (added the same day) renamed Standing to Rank everywhere
and says so explicitly: "This document is amended, not left contradicting
the code it describes." The task list was not amended to match. An
implementer who executes `warrior-standing.md` as written today will build
`StandingId`, which is the wrong type name and directly contradicts the
design it is supposed to implement. That plan document needs the same
mechanical rename pass (`StandingId` → `RankId`, `Standing` → `Rank`
wherever it names a field, plus the three collision reword sites the
Decisions section item 1 lists: `ContingentState.cs:9`,
`FormationPlanner.cs:20`) before or as the first step of implementing it.
This plan assumes that rename has happened; every symbol name below already
uses the amended, correct naming.

## Phase 1 — leader selection reaches rank

### L1. `MovementPresetId.PersistentContingentsV5` and the ruleset flag

**Files:** `src/Hukbo.Core/Movement/MovementPresetId.cs`,
`src/Hukbo.Core/Movement/MovementRuleset.cs`,
`src/Hukbo.Core/Movement/MovementPresetRegistry.cs`

Add `PersistentContingentsV5 = 5` to the enum, with an XML doc in the shape
`PersistentContingentsV4`'s already uses: identical to V4 in every cohesion
tunable, the one difference being that the leader scan orders by
`(RankId ascending, EntityId ascending)` instead of `EntityId` alone.

`MovementRuleset` gains a new required constructor parameter — name it
`selectsLeaderByRank`, exposed as `SelectsLeaderByRank { get; }` — following
exactly the shape `narrowsCohesionScanToCohesionCapableContingents` /
`NarrowsCohesionScanToCohesionCapableContingents` already uses: a plain
`bool`, folded into `ComputeContentHash` immediately after the existing
`NarrowsCohesionScanToCohesionCapableContingents` fold
(`Fnv1a.Add(ref hash, SelectsLeaderByRank ? 1UL : 0UL);`).

Every existing ruleset field (`IndependentPursuitV1Ruleset`,
`PersistentContingentsV2Ruleset`, `PersistentContingentsV3Ruleset`,
`PersistentContingentsV4Ruleset`) gets `selectsLeaderByRank: false` added
mechanically. `PersistentContingentsV5Ruleset` restates every V4 tunable
verbatim with `selectsLeaderByRank: true`, following the "restate, do not
reference" convention V4 already uses against V3. `IsRegistered` and `Get`
both gain the V5 switch arm.

**Depends on:** nothing else in this plan; blocked procedurally on
`AgentState.Rank`/`RankId` existing (Phase A).

**Verification:** adding the constructor parameter moves the pinned
`ContentHash` literals in `MovementPresetRegistryTests.cs` for **all four**
existing presets (V1 through V4), not only V5's new one — this is expected
and is exactly the situation `MovementRuleset.cs`'s own remarks block
predicts: every literal must be recomputed from the built code, never
calculated by hand, and this move is safe precisely because
`MovementRuleset.ContentHash` never reaches `StateHasher.Compute`
(`BattleSimulation` folds `_movementRules.ContentHash` nowhere into the state
hash; confirm this remains true by reading the fold list before touching
it). Because of that, `MovementPresetFreezeTests.cs`'s existing
`IndependentPursuitV1_ReproducesTheFrozenTrajectoryDigest` and
`PersistentContingentsV2_ReproducesTheFrozenTrajectoryDigest` must pass
**unchanged** — if either digest moves, this task has leaked behavior
change through a field meant to be inert for V1 through V4, and the work
stops. Add
`PersistentContingentsV5IsRegistered`/`PersistentContingentsV5ContentHashMatchesThePinnedLiteral`
following the existing per-preset test pairs.

### L2. Rank-aware comparator in the leader scan

**Files:** `src/Hukbo.Core/Movement/MovementRules.cs`,
`src/Hukbo.Core/Simulation/BattleSimulation.cs`

`ScanContingentLeadersAndLivingCounts` gains a `bool selectByRank` parameter.
When `false` the comparator is unchanged: lowest living `EntityId` replaces
the stored leader. When `true`, an agent replaces the stored leader when its
`Rank` is strictly lower-numbered, or tied on `Rank` with a lower
`EntityId` — total order, because entity ids are unique within a match,
exactly as the design's acceptance answer 4 states.

The call site in `BattleSimulation.ResolveContingentStates()` (currently
`MovementRules.ScanContingentLeadersAndLivingCounts(_agentStates,
_contingentLeaderEntityIds, _contingentLivingCounts);`) passes
`_movementRules.SelectsLeaderByRank` as the new argument.

Two sentences in the existing XML doc on `ScanContingentLeadersAndLivingCounts`
are now false and must be reworded rather than left stale: "the leader (the
lowest living `AgentState.EntityId` among its members)" and "the comparison
is against `AgentState.EntityId` explicitly" both need to say the comparison
depends on `selectByRank`.

**Depends on:** L1; procedurally on `AgentState.Rank` existing.

**Verification:** new hand-built unit tests in
`tests/Hukbo.Core.Tests/ContingentStateMachineTests.cs`, in the same section
as the existing `TheLeaderIsTheLowestLivingEntityIdInItsContingent`,
`LeaderSelectionIsUnchangedByAgentArrayPermutation`, and
`TheLeaderIsPromotedToTheNextLowestLivingEntityIdOnDeath` (do not create a
new test file; this is the established home for unit-level leader-scan
coverage per that file's own header comment). Cover exactly the four
scenarios the design's acceptance answer 9 names: a single chief present; several
chiefs present, lowest entity id wins among the tied chiefs; no chief
present, the highest-ranking (lowest-numbered) survivor wins; and the chief
dead mid-battle, leadership passing to the next-ranking survivor on the
following scan. A companion test asserts that with `selectByRank: false`,
hand-placed `Rank` data on the same agents is entirely ignored and the
`EntityId`-only result is unchanged — the "V1 through V4 produce an unmoved
leader selection under hand-placed rank data" proof the design names, since
under those presets `MovementRuleset.SelectsLeaderByRank` is `false` and
this parameter is never `true` for them.

## Phase 2 — the leader marker and the inspector annotation

### L3. `AgentView.IsLeader`

**Files:** `src/Hukbo.Core/Simulation/AgentView.cs`,
`src/Hukbo.Core/Simulation/AgentState.cs`,
`src/Hukbo.Core/Simulation/BattleSimulation.cs`

Add `bool IsLeader = false` as a new trailing defaulted parameter on the
`AgentView` record struct, following the same convention already documented
for `Level`, `ContingentId`, and `ContingentState`: defaulted so
presentation tests written before this field existed keep compiling without
naming it.

`AgentState.ToView()` currently takes no arguments and is called from
`BattleSimulation.UpdateViews()` in a loop with no leader context
(`_agentViews[index] = _agentStates[index].ToView();`). `ToView` gains a
`bool isLeader` parameter, and `UpdateViews()` computes it per agent by
comparing `agent.EntityId` against `_contingentLeaderEntityIds[slot]` for
that agent's `(FactionId, ContingentId)` slot — the same array
`ResolveContingentStates()` and `TryResolveContingentCohesionAimPoint`
already read. No new simulation-side computation, only a new read of an
existing fact, exactly as the design specifies.

Verified, not merely assumed: under `MovementPresetId.IndependentPursuitV1`,
`ResolveContingentStates()` returns before the leader scan ever runs, so
`_contingentLeaderEntityIds` stays at its constructor-time value of all
zeros for the whole battle. `0` is never a valid `EntityId` (the scan's own
doc says so), so `agent.EntityId == _contingentLeaderEntityIds[slot]` is
`false` for every real agent under V1 with no extra guard needed — confirm
this stays true rather than adding a redundant preset check, since an
unnecessary branch here is exactly the kind of query CLAUDE.md's logging
rule and this repository's general style discourage.

**Depends on:** L2.

**Verification:** a new `BattleSimulationTests` assertion, run across every
registered movement preset including `IndependentPursuitV1`, that at every
sampled tick exactly one living member per non-empty contingent has
`IsLeader == true` and every other living member (and every agent under
`IndependentPursuitV1`) has `IsLeader == false`.

### L4. Leader marker on the pawn

**Files:** `src/Hukbo.Client/Rendering/PawnRenderer.cs`,
`src/Hukbo.Client/ArenaGame.Rendering.cs`

`PawnRenderer.Draw` gains a new trailing defaulted parameter
`bool isLeader = false`, following the exact convention `contingentId = 0`
and `contingentState = ContingentState.None` already use on the same method
signature.

Add a new private static `DrawLeaderMark`, modeled directly on
`DrawSelectionMark` and `DrawDeadMark` — both are hand-drawn primitives over
the pawn's existing `layout` bounds using the shared `pixel` texture, and
neither needed a new `PawnGeometry` entry, so this should not add one
either. There is no `PawnRendererTests.cs` today and this task does not
create one; `Draw` is GPU-bound and untestable per the client presentation
test rule, matching the existing, already-untested selection and dead
marks.

Add a new dedicated color field near the top of the file, beside
`DeadColor` and `HoverColor`. Do not reuse `DyePalette.GoldAccent` — that
tone already draws the adornment accent on the same pawn, and reusing it
would make the leader mark visually indistinguishable from existing detail
at typical camera zoom.

`Draw`'s existing selection/hover/dead branch (`if (state is
PawnVisualState.Hovered or PawnVisualState.Selected) {...} else if (isDead)
{...}`) is mutually exclusive by construction. Decide explicitly, and record
the decision in a comment at that branch, whether the leader mark can
coexist with a selection ring (a spectator can select the leader) or a dead
mark (the tick a leader falls, before succession recomputes on the next
scan, its view may still carry `IsLeader == true`) — do not let this be an
accidental consequence of `if`/`else if` ordering that nobody chose on
purpose.

The one call site that must pass `isLeader: agent.IsLeader` is
`ArenaGame.Rendering.cs`'s battlefield draw call, which already threads
`contingentId: agent.ContingentId` and `contingentState:
agent.ContingentState` from the same `AgentView agent`.
`AgentInspectorPanel.cs`'s portrait call site already carries a comment
explaining why it deliberately omits `contingentId`/`contingentState` — a
fixed close-up portrait has no ground plane and no neighboring pawns for
that context to read against. The same reasoning applies to `isLeader`;
leave that call site at the parameter's default and extend the existing
comment to say so explicitly rather than relying on a future reader to
infer it.

**Depends on:** L3.

**Verification:** no automated test — see above. The manual smoke checklist
in `docs/development/testing.md` gains one new row: a leader marker is
visible on exactly one warrior per visible contingent, and it visibly moves
to a different warrior the tick a contingent's ranking member dies. Leave
that row `PENDING` until a person actually observes it at an interactive
desktop, per `CLAUDE.md` section 6.

### L5. Inspector leadership annotation

**Files:** `src/Hukbo.Client/UI/AgentInspectorContent.cs`

Extend `FormatContingentLine` to accept `bool isLeader` and append a
leadership suffix to the existing `"Contingent: {contingentId} —
{label}"` string when true (for example `"Contingent: 2 — Advance
(leading)"`), rather than adding an unconditional fourteenth row and
touching `MaximumLowerRowCount`. This choice is made here explicitly,
rather than left open the way the design's section 4 permits either option
— a task that finds itself guessing between the two at implementation time
has hit exactly the situation `CLAUDE.md`'s workflow rules forbid.

Update the call site in `BuildLowerLines` to pass `agent.IsLeader` into
`FormatContingentLine`.

The succession rule this annotation reflects is a **Provisional
reconstruction**, not a documented historical fact (design section 3;
parent design Decisions item 3). The annotation's wording must not claim
more than that: "leading" is fine; "chief" or "commander" is not, because
either would present an unearned rank claim the historical accuracy policy
(`CLAUDE.md` section 7) does not license for a rule the evidence base
itself says the sources do not establish.

**Depends on:** L3.

**Verification:** new `AgentInspectorContentTests.cs` assertions: the
contingent line carries the leadership suffix when `agent.IsLeader` is
`true` and does not when it is `false`, checked for both a `Hold` and an
`Advance` contingent state; the line stays `null` (omitted entirely) exactly
when it already does today — `ContingentState.None` with `IsLeader` at its
default `false`.

## Phase 3 — gate

### L6. Run the canonical gate

**Files:** none

```powershell
./scripts/verify.ps1
```

Run once, after integration. Paste the real output. No sub-agent report and
no partial run substitutes for it. Report the 500-agent stress result
`SIMULATION-GAME-STANDARDS.md` §10 requires, and leave every manual
smoke-checklist row in `docs/development/testing.md` at its honest value —
`PENDING` for L4's new row unless a person actually watched it.

**Depends on:** L1 through L5.

## What this plan deliberately does not do

- **No morale, fear, or rout**, in any form, under any name. `CLAUDE.md`
  section 9 defers all three. A contingent's reaction to losing its leader
  is expressed entirely through `ContingentState` recomputing and cohesion
  geometry moving with the new leader — nothing here adds a value that
  degrades combat performance because a leader died.
- **No command-signal system.** No shout, horn, gong, drum, flag code, or
  messenger role. `docs/research/ARMY-COMPOSITION.md` §7 records this as
  unsupported.
- **No booty, ransom, or reward economy.** Deferred to the future campaign
  layer that consumes `BattleOutcome`; `Hukbo.Core` never learns what a
  barangay is, and this plan does not change that.
- **No mid-battle allegiance switching.** `FactionId` and `ContingentId`
  stay immutable for the duration of a battle.
- **No new `ContingentState` value**, and no change to
  `ResolveContingentState`'s inputs. Leadership reassignment rides the
  existing state machine; it does not extend it.
- **No persistent "leader" flag stored on an agent.** Leadership is a
  derived fact of the current living roster, recomputed from scratch every
  tick by the leader scan, exactly as it is today under the
  lowest-entity-id rule — this plan changes the comparator, not the
  recompute-every-tick contract.
- **No follower-capacity number, and no contingent-shape change.** Deferred
  to `docs/plans/2026-07-29-contingent-shape-design.md`, named there as a
  separate, larger, and separately evidenced piece of work against
  `FormationPlanner`.
- **No CI workflow.** Verification here is local and deliberate, per
  `CLAUDE.md` section 4.
