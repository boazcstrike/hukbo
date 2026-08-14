# Thousand-unit performance — plan

Design document: `docs/plans/2026-08-14-thousand-unit-performance-design.md`.
Read it first; it carries the measurements, the source reading, and the reasons
several obvious-looking optimisations are not here.

**This plan is not authorized.** It exists so that the decision to build is a
decision taken against a task list rather than against a vague intention.
Section 6 of `CLAUDE.md` says how authorization is given.

**Baseline for every task below: `main` at `dc8e901`, 2026-08-14.**

---

## The shape of the work

Four phases, and the first one can end the workstream.

| Phase | What it does | Can it be skipped |
| --- | --- | --- |
| 0 | Measure the 1,000-unit frame on today's build and attribute the cost | No. Everything after it is conditional on what it finds |
| 1 | Remove hash-neutral cost from `Hukbo.Core`'s per-tick scans | Yes, if Phase 0 says the simulation is not the overrun |
| 2 | Remove hash-neutral cost from the client's per-frame path | Yes, if Phase 0 says the draw path is not the overrun |
| 3 | Verify, record, and run the two smoke rows this whole thing exists for | No |

**Phase 0 has a real stop condition.** If the re-measurement shows the
1,000-unit frame already inside budget at all three camera stations, the correct
action is to run `GR-3` and `GR-5` and close the workstream having written no
code. Task TU-4 is that decision point and it is a genuine one.

---

## Phase 0 — measure and attribute

| Task | What it does | Files | Verification | Depends on |
| --- | --- | --- | --- | --- |
| TU-1 | **Re-run the render matrix on today's build.** Build `tools/Hukbo.Tools.RenderProbe` in Release and run its `--matrix` mode at seed 1, 120 frames per station, vertical retrace disabled, from the built apphost rather than `dotnet run` (the matrix mode re-invokes `Environment.ProcessPath` and fails otherwise). Commit the JSON. The 2026-07-29 matrix predates the corpse layer, gait legs and feet, projectile props, embedded projectiles, armor accents, blood, clash effects, lethal-blow legibility, and leader marks, so it describes a build that no longer exists | `docs/development/render-baselines/render-matrix-<date>.json` (new), `docs/development/testing.md` | The JSON exists, carries all three agent counts and all three stations, and its fingerprint records `verticalRetraceSynchronized: false`. The tables are transcribed into `testing.md` beside the recorded 2026-07-29 figures | — |
| TU-2 | **Attribute the simulation tick cost by stage at 1,000 agents.** Profile `Hukbo.Headless` at 1,000 agents / 10,000 ticks / seed 1 under the shipped presets with the `dotnet-diag` plugin's `dotnet-trace`, and record the per-method share for `SelectTargetsAndIntents`, `IsLaneClearOfAllies`, `HasAllyWithinPursuitSupport`, `FindNearestMeleeThreatPosition`, `GatherMovementProposals`, `ResolveCollisions`, and `GatherAndCommitAttacks`. **No instrumentation may be added to `Hukbo.Core` for this** — the profiler is external and the source does not change | `docs/development/measurement-history.md` | A recorded table of per-method inclusive and exclusive share, with the trace command quoted, and no diff under `src/` | — |
| TU-3 | **Size the client's per-tick presentation ingest.** Measure `PresentationCoordinator.IngestTick` at 1,000 agents in the same trace, separated into `Gait.Ingest`, `Trample.Ingest`, `Dust.Ingest`, `Projectiles.Ingest`, and the two accumulators. This is the one per-frame cost that scales with the playback speed multiplier, so record it per tick and per frame at 4x | `docs/development/measurement-history.md` | The seven figures are recorded, with the 4x per-frame arithmetic shown | TU-1 |
| TU-4 | **The stop-or-continue decision.** Re-run the two-clause trigger the closed GPU render workstream stated — 1,000-unit default-fit `Draw` p95 against 8.0 ms, and Tier 1 `submitMicroseconds` p95 against 50 percent of that frame — against TU-1's figures, and state in writing which of Phase 1 and Phase 2 the evidence authorizes. **If the frame holds at all three stations, stop here**, go straight to TU-16, and record the workstream as closed with no code written | `docs/development/testing.md`, this file | A written verdict quoting both clause figures, and an explicit statement of which later phases are live | TU-1, TU-2, TU-3 |

---

## Phase 1 — hash-neutral cost removal in `Hukbo.Core`

Every task in this phase is hash-neutral **by construction**: same values, same
order, same results. If a digest moves, the task is wrong. There is no case in
this phase where recapturing a golden expectation is the correct response to a
red test, and any agent that proposes one has misunderstood the phase.

Tasks TU-5 through TU-7 are ordered cheapest-and-safest first, and each is
independently shippable. Do not start TU-9 or TU-10 before TU-8 has been
measured — they are the two that could still be unnecessary.

| Task | What it does | Files | Verification | Depends on |
| --- | --- | --- | --- | --- |
| TU-5 | **Precompute each agent's squared ally-clearance radius once, at construction.** `IsLaneClearOfAllies` currently calls `_movementRules.ResolveLoadoutProfile(ally.Loadout)` and `SquaredClearanceRadius` for every ally it visits, on every route candidate, on every tick. `AgentState.Loadout` is get-only and assigned once in the constructor, and `Scenario.BodyRadiusRaw` does not change during a battle, so the value is a battle constant. Store it in a flat `Int128[]` (or `long[]`, see TU-9) indexed the same way `_agentStates` is, filled once, and read it in the loop | `src/Hukbo.Core/Simulation/BattleSimulation.cs` | All five gate digests and the two large-agent digests in the design's section 6 unchanged; both `BattleSimulationTests` allocation windows unchanged; a new unit test asserting the precomputed row equals `SquaredClearanceRadius(ResolveLoadoutProfile(agent.Loadout))` for every agent in a fielded scenario | TU-4 |
| TU-6 | **Precompute each agent's squared pursuit-support radius the same way.** `HasAllyWithinPursuitSupport` derives `supportSquared` from the actor's own profile and the body radius on every call. Same argument, same fix, separate array because the basis-point field differs | `src/Hukbo.Core/Simulation/BattleSimulation.cs` | As TU-5, with the equivalent per-agent equality test | TU-5 |
| TU-7 | **Add an axis-aligned rejection to the three bounded scans.** `IsLaneClearOfAllies`, `HasAllyWithinPursuitSupport`, and `FindNearestMeleeThreatPosition` are all radius-bounded, unlike target selection. Reject on `\|dx\|` and `\|dy\|` against the maximum relevant unsquared radius before any multiply, written as two comparisons without negation, exactly as `SelectTargetsAndIntents` already does. **The comment on the new branch must state which radius bounds it and why the rejection is exact** — a candidate rejected here must be one the squared test would also have rejected, or the scan's result changes | `src/Hukbo.Core/Simulation/BattleSimulation.cs` | As TU-5. Additionally, a test that constructs a candidate exactly on the radius and asserts it survives the rejection, because an off-by-one on an inclusive boundary is the one way this task silently changes a result | TU-6 |
| TU-8 | **Give `SelectTargetsAndIntents` a struct-of-arrays hot slice.** `_agentStates` is `AgentState[]` — an array of references to a sealed class — and the inner scan dereferences six fields per pair, one million times per tick at 1,000 agents. Refresh flat arrays of alive, faction, `XRaw`, `YRaw`, and `EntityId` once per tick before the scan, allocated once at construction and never per tick, and read those in the inner loop; touch the heap object only for the candidates that pass the perception test. Do not remove the existing axis-aligned rejection even though the design's section 4.1 shows it never fires under the default scenario — it fires for any scenario whose perception range is smaller than the map, and removing it would be a behaviour change for those | `src/Hukbo.Core/Simulation/BattleSimulation.cs` | As TU-5. Additionally, the allocation windows are the load-bearing check here: an array refreshed per tick rather than filled in place fails the 8,192-byte warm-tick ceiling and that failure is the intended one | TU-7 |
| TU-9 | **Narrow `Int128` to `long` only where the bound is provable.** The clearance comparisons use `Int128` throughout. On a validated map, a squared separation fits in `long` with room, but **the argument must come from `Scenario`'s validated bounds, written out in the doc comment, not from the values a run happens to produce.** Leave `Int128` wherever the bound is not provable. If the bound cannot be written down in two lines, skip this task; it is the smallest win in the phase | `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `src/Hukbo.Core/Simulation/CollisionGeometry.cs` | As TU-5, plus an overflow test at the validated extremes of `MapWidth`, `MapHeight`, and `BodyRadiusRaw` | TU-8, and a re-measurement showing TU-5 through TU-8 did not already clear the tail |
| TU-10 | **Conditional: answer the ally-clearance query through a uniform grid.** `CollisionUniformGrid` already exists, is rebuilt each tick for collision, and answers bounded-radius questions. The ally-clearance and pursuit-support queries are bounded in a way target selection is not, so the grid genuinely applies. This is the largest and riskiest task in the phase — the grid's traversal order must produce the same *decision*, and the scans it replaces short-circuit on the first violating ally, so the replacement must preserve that or prove the decision is order-independent. **Start it only if a re-measurement after TU-8 shows these scans still dominate the tail** | `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `src/Hukbo.Core/Simulation/CollisionUniformGrid.cs` | As TU-5, plus a differential test running both the scan and the grid query over a fielded 1,000-agent scenario for 200 ticks and asserting an identical decision for every agent on every tick | TU-9, and a re-measurement |

---

## Phase 2 — the client's per-frame path

Nothing in this phase may change which pawns are drawn, in what order, or with
what appearance. The client has no say in simulation state, so these tasks
cannot move a digest; the risk they carry is visual, and `GR-4`'s standard —
"no visible difference; any visible difference is a defect, not a new baseline" —
is the one that applies.

| Task | What it does | Files | Verification | Depends on |
| --- | --- | --- | --- | --- |
| TU-11 | **Walk the roster once per frame instead of twice.** `DrawPawns` calls `DrawPawnPass` twice and each call walks all 1,000 agents, resolving `PawnVisualState` for every one and skipping those belonging to the other pass. Partition into two index lists in one walk, then draw each list. The resolved state must still decide pass membership and the drawn appearance from the same value, which is the invariant the current two-pass shape exists to guarantee; keep it explicit | `src/Hukbo.Client/ArenaGame.Rendering.cs` | `Hukbo.Client.Tests` green; the probe's `pawnGeometryInvocations` for the draw path unchanged at every station; the drawn set and draw order provably identical, asserted by a test over a fielded roster containing both living and dead agents | TU-4 |
| TU-12 | **Re-check `ConservativePawnCull` adoption at 1,000 units.** The type is written, is proven a strict superset of `PawnRenderer.GetBounds` by a brute-force test over the full catalog cross-product, and nothing calls it. Adoption was dropped on 2026-08-07 because the saving measured zero at minimum zoom and at default fit — at 500 units. Re-measure at 1,000 and either adopt it ahead of the appearance resolution in the pawn loop or record, in `ConservativePawnCull`'s own remarks, that the rejection was re-taken at 1,000 units and still holds | `src/Hukbo.Client/ArenaGame.Rendering.cs`, `src/Hukbo.Client/Rendering/ConservativePawnCull.cs` | Either the drawn set is unchanged with the appearance resolution skipped for rejected pawns, asserted over the catalog cross-product, or a recorded measurement and an updated remark with no code change | TU-11 |
| TU-13 | **Act on TU-3's ingest figures, or record that no action is warranted.** `PresentationCoordinator.IngestTick` runs inside the tick loop, so it costs up to four times per frame at 4x. `GaitAnimationSystem` maintains a `Dictionary<ulong, AgentView>` refreshed over all 1,000 agents per tick. If TU-3 shows this is small, write that down and close the task; do not optimise it on suspicion | `src/Hukbo.Client/Presentation/GaitAnimationSystem.cs`, `src/Hukbo.Client/Presentation/PresentationCoordinator.cs` | Either a measured improvement with `Hukbo.Client.Tests` green and the gait pose output unchanged over a fielded roster, or a recorded no-action decision quoting TU-3's figure | TU-3, TU-11 |

---

## Phase 3 — verify, record, and close

| Task | What it does | Files | Verification | Depends on |
| --- | --- | --- | --- | --- |
| TU-14 | **Run the canonical gate and record it.** `./scripts/verify.ps1` with no arguments, from a clean worktree at the integration commit. Paste the real output. All five workload digests must match the design's section 6 table exactly | `docs/development/testing.md` | A recorded gate result with every stage's verdict and all five state and event hashes quoted. **Not delegable** — no sub-agent report substitutes for the gate's own output | Every live task above |
| TU-15 | **Re-run both measurements and record the delta.** The 500- and 1,000-agent headless points under the shipped presets, whose digests must equal `9486F45B5BC59B80` / `B2D66B025BD1BBD3` and `01F9FD533AE0F018` / `9B25A4FA432E4CE8`, and the render matrix from TU-1. Record before and after side by side, including max and p99, not only p50 — the design's section 3 states the tail is the target | `docs/development/measurement-history.md`, `docs/development/render-baselines/`, `docs/development/testing.md` | Both digest pairs byte-identical, `maximumPenetrationRaw 0` at every point, and a before-and-after table covering p50, p95, p99, and max | TU-14 |
| TU-16 | **Run `GR-3` and `GR-5`.** Set `Units Per Team` to 500 for both teams, start the 1,000-unit battle, and watch one full engagement at all three camera stations; then watch hit pulses in the dense melee. **A person at the desktop, watching. No agent may flip either row**, and neither compilation, nor a green gate, nor a window-opening probe is evidence for them. Report `BLOCKED` honestly if they cannot be run | `docs/development/smoke-checklist.md` | The two rows carry a real `Actual` column written by the person who watched, and a status of `PASS`, `FAIL`, or `BLOCKED` | TU-15, or TU-4 directly if Phase 0 stopped the workstream |

---

## Verification criteria

The workstream is complete when all of the following hold, and not before.

1. The canonical gate is green and all five workload digests match the design's
   section 6 table byte for byte.
2. The 500- and 1,000-agent seed-1 digests under the shipped presets are
   unchanged from the values recorded in the design's section 2.1.
3. Both `BattleSimulationTests` allocation windows are unchanged, and
   `maximumPenetrationRaw` is 0 at every measured point.
4. A before-and-after table covering p50, p95, p99, and max exists for the
   simulation at 500 and 1,000 agents, and for the render matrix at all three
   stations and all three agent counts.
5. `GR-3` and `GR-5` carry a status written by a person who watched a 1,000-unit
   battle.
6. Every claim of improvement in the recorded documentation is backed by the
   output of the command that produced it.

A stop at TU-4 satisfies criteria 1, 4, 5, and 6 and closes the workstream
legitimately.

---

## What this plan does not do

- It does not change the outcome of any battle, and it introduces no preset
  version. A moved hash means a task was implemented wrongly, not that a
  baseline needs recapturing.
- It does not add a spatial index to target selection. The design's section 4.1
  shows that query has no locality to exploit under the shipped scenario.
- It does not build an instanced rendering backend. The verdict that closed that
  question is recorded in the design's section 2.4, and TU-4 re-runs its trigger
  as a measurement rather than reopening it.
- It does not parallelise the tick, cache a target, or add a cache of any kind.
- It does not raise `ArmyCompositionStepper.MaximumUnitsPerTeam` above 500, scale
  the map with the unit count, or change any presentation effect capacity. Those
  are the design's section 7 open questions and each needs a decision this plan
  is not entitled to take.
- It does not touch `Sandata`. A green `./scripts/verify.ps1` says nothing about
  that game, and no task here claims otherwise.
