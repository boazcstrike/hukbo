# Prompt — Weapon-relative movement, shared foundation session

Date: 2026-07-30

Run this session **first and alone**. The five weapon sessions described in
[`2026-07-30-weapon-movement-weapon-template.md`](2026-07-30-weapon-movement-weapon-template.md)
all depend on what this one lands, and they run in parallel only after this
session's branch is merged into `main`.

Hand this file to the agent verbatim, or paste the body below the horizontal
rule. It is written to be self-contained: an agent that reads only this prompt
and the files it names has everything it needs.

---

## Task

Build the shared foundation for Hukbo's weapon-relative movement program, as
code, in a dedicated git worktree. The end state is a new, opt-in movement
preset that resolves an immutable movement profile for each of the six
implemented loadouts, carries authoritative facing, retained pace, tactical
posture, and footwork phase through the tick pipeline, hashes them, shows them
in the inspector, and is exercised by a generated scenario matrix — with every
existing movement preset still reproducing its recorded replays byte for byte.

You are **not** calibrating any individual weapon. You land each weapon's row
with the initial values the shared plan already fixed, and the weapon sessions
that follow own the tuning.

Your governing documents, in precedence order:

1. `docs/plans/movement/README.md` — the shared architecture, the exact
   configuration types, the content-hash fold order, the facing table, the
   route and clearance contract, the precedence chain, and the task graph
   T0–T12. This is your plan.
2. `docs/research/movement/README.md` — evidence labels, the six-loadout
   matrix, the count and composition model, and the numerical tuning contract.
3. The five weapon research files under `docs/research/movement/` — read for
   the boundary-equality conventions each one assumes, so the shared rules you
   write satisfy all five rather than one.

## Preflight — do this before planning anything

The plan documents were written on 2026-07-29 and the repository has moved
since. Verify each of the following yourself, in the worktree, and record what
you found in your report. Where a plan document disagrees with the code, the
code wins.

1. **Preset number.** Read `src/Hukbo.Core/Movement/MovementPresetId.cs`. The
   shared plan calls the new preset `EquipmentRelativeFootworkV5 = 5`. That
   value is already taken by `PersistentContingentsV5`, the rank-aware leader
   scan. Allocate the next unused value — expected to be
   `EquipmentRelativeFootworkV6 = 6` — and use that name consistently
   everywhere, including in the handoff you write for the weapon sessions.
   Never renumber or reorder an existing value. The plan anticipated exactly
   this case and authorises the rename.
2. **The combat default has already moved.** `Scenario.CombatPreset` defaults
   to `CombatPresetId.PrecolonialPhilippinesV4`, not V2. **Task T2 of the
   shared plan — "switch only the combat default" from V2 to V3 — is therefore
   obsolete. Do not execute it.** Say so in your report.
3. **Which combat preset fields which loadout.** Read the registered combat
   preset sources and establish, as a table, which presets field each of the
   six loadouts `KP, WA, KA, IT, KS, IS`, with particular care for the two
   tall-hardwood-shield rows. Every scenario, test, and benchmark you write
   names its combat preset **explicitly** rather than relying on the default.
   This table is a required output: the weapon sessions consume it.
4. **What already exists.** Confirm the current state of
   `src/Hukbo.Core/Movement/`. At the time this prompt was written it held only
   `MovementPresetId.cs`, `MovementPresetRegistry.cs`, `MovementRules.cs`, and
   `MovementRuleset.cs` — no profile type, no facing type, no `Profiles/`
   directory, no context query, no weapon movement rules.
5. **Baseline.** Execute shared task T0 in full: confirm the V4 baseline, run
   `./scripts/verify.ps1` on the untouched worktree, and record the benchmark
   medians, hashes, allocations, and machine identity that the performance
   budget in T0 step 6 is measured against. A gate that was already red is not
   yours to be blamed for and not yours to hide.

## Worktree

Work in isolation. This checkout is shared by other live sessions and by
`dotnet test` runs that fight over `obj/`.

```powershell
git worktree add .claude/worktrees/movement-foundation -b movement-foundation main
```

Branch off **local** `main` and confirm the worktree sits on the commit you
expect before doing anything:

```powershell
git -C .claude/worktrees/movement-foundation log -1 --oneline
```

Other directories under `.claude/worktrees/` and untracked files appearing in
`docs/` usually belong to another session. Leave them alone. Rebase onto `main`
before you run the final gate.

## Scope

You own every shared file the plan's ownership table assigns to "Shared
foundation" and to "Shared integration/reviewer", which is to say:

- `MovementPresetId.cs`, `MovementRuleset.cs`, `MovementPresetRegistry.cs`
- new `LoadoutMovementProfile.cs`, `Facing16.cs`, `FacingRules.cs`,
  `WeaponMovementRules.cs`, `LoadoutCompositionCounts.cs`,
  `LocalMovementContext.cs`, `MovementContextQuery.cs`,
  `TacticalPosture.cs`, `FootworkPhase.cs`
- `AgentState.cs`, `AgentView.cs`, `StateHasher.cs`, `BattleSimulation.cs`,
  `FormationPlanner.cs`
- `AgentInspectorContent.cs`, `AgentInspectorPanel.cs`
- `MovementBehaviorMetrics.cs`, `RunReport.cs`, `HeadlessRunner.cs`
- the shared test surface: freeze fixtures, the naive oracles, the registry and
  profile tests, and `tests/Hukbo.Core.Tests/Movement/MovementScenarioMatrix.cs`
- all six files under `src/Hukbo.Core/Movement/Profiles/`

### One deliberate deviation from the plan's task T4

The plan has each weapon owner create its own profile row and the shared owner
compose them afterwards. That ordering cannot work here, because the weapon
sessions run **after** you and **in parallel with each other**, and composing
rows later would mean five sessions editing `MovementPresetRegistry.cs` at
once.

So: **you create all six profile rows yourself**, using the exact initial
values in the shared plan's defaults table and opponent-offset table, and you
wire them into the V6 registry. Each row file carries XML documentation
marking its values as *Provisional reconstruction: gameplay tuning; no
historical measurement*, and naming the weapon session that owns it from then
on. After your branch merges, those six files belong to the weapon sessions for
pinning and calibration; the registry, the ruleset, and the shared rules stay
frozen against them.

## Execution order

Follow the shared plan's task graph, with T2 removed and the preset renumbered:

- **T0** — verify the V4 baseline and record the performance budget.
- **T1** — freeze existing movement-preset trajectories before any structural
  edit. Extend this beyond the plan's "V3 and V4": every preset registered
  today, V1 through V5, gets an explicit combat-preset-named freeze fixture, so
  the rank-aware V5 that landed after the plan was written is protected too.
- **T3** — `LoadoutMovementProfile`, `Facing16`, `FacingRules`, with the
  16-vector table and the faction-canonical tie rules exactly as specified,
  plus the source-hygiene assertion banning `Math.Atan*`, `MathF`, and `double`.
- **T4** — the six profile rows and the opt-in V6 registration, including the
  content-hash fold in the exact declared order. Recompute pinned
  `MovementRuleset.ContentHash` literals from built output, never by hand.
- **T4P** — V6-only equipment-aware assignment of warriors to existing
  formation slots, consuming no additional random draw.
- **T5** — the bounded local context, the pure query, and the independent
  naive O(n²) oracle they are tested against.
- **T6** — `TacticalPosture`, `FootworkPhase`, the new `AgentState` fields, and
  the pure resolvers.
- **T7** — the conditional state hashing first, then the pipeline integration:
  route selection, the clearance scan, the deterministic proposal-conflict
  pass, pace application, and the `AttackAcceptedThisTick` marking that leaves
  every existing combat gate untouched.
- **T8** — snapshot, view, and inspector rows.
- **T9** — derived observability that reaches neither hash.
- **T10** — the matrix generator and its self-tests only. The 21-cell and
  231-cell **runs** are weapon-session work; you deliver the harness they call.
- **T12** — freeze the new preset, capture its digest with provenance, and run
  the full verification.

Task T11's calibration belongs to the weapon sessions. Deliver the fixtures and
metric definitions it needs; do not tune anyone's row.

## How to run it

Invoke the `hukbo-orchestrate` skill and follow it. Do not restate the pipeline
from memory, and do not use the generic `/flow:*` commands — inside this
repository they do not know about worktree isolation, the `tokensave`-only
discovery rule, or the canonical gate.

**Stage 1 — research, in parallel, read-only.** Two groups in one message, at
most eight agents across the whole fan-out:

- *Requirements and evidence*: what the shared plan pins exactly — the profile
  property list and bounds, the content-hash fold order, the facing table and
  tie rules, the posture branch order, the footwork transition order, the route
  candidate order, the precedence chain, and every boundary-equality
  convention the five weapon research files assume. Return tables, not prose.
- *Existing code*: the current movement surface, the tick-stage order in
  `BattleSimulation.AdvanceOneTick`, how `StateHasher` folds today, how
  `FormationPlanner` places and mirrors, what the inspector and `AgentView`
  already carry, and every test that asserts preset registration, hash
  stability, or a golden fixture. Return file, symbol, line, and what each task
  would have to change.

Code discovery goes through the `tokensave` MCP tools. Never an Explore agent,
and carry that constraint into every sub-agent prompt you write.

**Stage 2 — planning, one agent.** One planner reads both research outputs and
produces one ordered task list with the columns: task, what, files, done when,
depends on, verified by. It writes no code. Audit its output yourself before
spawning anyone: file sets genuinely disjoint, every determinism-touching task
naming its preset value and its goldens, dependency order actually holding, and
nothing from the deferred list smuggled in.

**Stage 3 — implementation.** Only this stage writes files. TDD as each task
specifies: failing focused test first, minimum code, re-run. Go serial where
the work funnels through one shared seam — `BattleSimulation`, `StateHasher`,
and the ruleset are single-writer files, and parallelism there buys nothing.

## Hard constraints

**Determinism.** Load the `hukbo-determinism-change` skill before touching
simulation code. Integer and fixed-point math only in `Hukbo.Core`; no `float`,
no `double`, no trigonometry in anything that reaches state. `System.Random` is
banned — `SplitMix64` only, and never edit its pinned vectors. Movement presets
V1 through V5 are replay contracts: their behaviour, outcomes, ordered events,
state hashes, event hashes, and trajectory fixtures stay byte-identical. The
one-time `MovementRuleset.ContentHash` literal update that extending the
ruleset schema causes is expected and anticipated by the source comments; it
must be recomputed from built output and called out explicitly in your report.

**Do not activate.** `Scenario.MovementPreset` stays at
`PersistentContingentsV4`. The new preset is opt-in through the existing
`--movement-preset` option only. Activation is a separate, separately approved
task with its own golden expectations.

**Core boundary.** `Hukbo.Core` may not reference MonoGame, the filesystem, the
network, windowing, audio, the wall clock, or `Hukbo.Diagnostics`. No unbounded
cache, no target cache, no spatial grid in this slice, and no derived data in a
snapshot. `Hukbo.Client` may not decide targeting, damage, retreat, or victory.

**Scope discipline.** No change to damage, reach, cooldown, combos, clash, hit
location, or shield interception. No directional attack, defence, shield arc,
parry, or friendly damage. No velocity vector, acceleration engine, physics,
terrain, pathfinding, morale, panic, or rout. Facing and retained scalar pace
are authoritative state; they are a one-dimensional pace memory, not a velocity
system.

**Performance.** The T0 budget binds: no more than 2.0× the V4 median elapsed
at 200 agents, 2.5× at 500, and zero warm-tick bytes allocated by the new
movement stages. If the budget fails, stop and write a separate optimisation
design that compares a bounded query against the naive oracle. Do not fold an
optimisation into tuning, and do not relax the budget.

**Historical accuracy.** `AGENTS.md` §7 binds. Every load-bearing claim carries
an evidence label. Every tuning number in code carries the *Provisional
reconstruction: gameplay tuning; no historical measurement* marker in its XML
documentation. Player-facing cultural identifications appear only in pair form
— Filipino name, em dash, plain English descriptor.

**Logging.** New instrumentation goes through `Hukbo.Diagnostics.DiagnosticLog`
with a `const` identifier on `LogEvents`. Never `Console.Write*`. Per-tick and
per-frame lines are `trc`. A disabled call allocates nothing. `Hukbo.Core`
never references `Hukbo.Diagnostics`.

**Documentation.** Full, normal English. Never run a compression pass over
repository documentation. Any new design or plan document goes under
`docs/plans/`, never the repository root.

**Naming.** `Hukbo` and `Hukbo.*` only. Never reintroduce `AutonomousArena`.

## Verification

Focused tests throughout:

```powershell
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release --filter "FullyQualifiedName~Movement"
```

After integrating everything and rebasing onto `main`, run the canonical gate
**yourself, once**:

```powershell
./scripts/format.ps1 -Verify
./scripts/verify.ps1
```

The gate is never delegated to a sub-agent, and no sub-agent report substitutes
for its output. Paste the real output. A build that compiles is not a passing
test run, and a test run is not a manual smoke check. Load
`hukbo-verify-and-record` for what counts as evidence.

Benchmarks, naming both presets explicitly, against the combat preset your
preflight table selected:

```powershell
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1 -Preset <combat preset> -MovementPreset EquipmentRelativeFootworkV6
./scripts/benchmark.ps1 -Agents 500 -Ticks 10000 -Seed 1 -Preset <combat preset> -MovementPreset EquipmentRelativeFootworkV6
```

and the same two runs against `PersistentContingentsV4` for the ratio.

## Definition of done

- Preflight recorded: preset value allocated, T2 declared obsolete, the
  loadout-to-combat-preset table produced, and the T0 baseline measured.
- V1–V5 freeze fixtures exist, name their combat and movement presets
  explicitly, and are byte-identical before and after every structural change.
- The new preset is registered, opt-in, resolves exactly six complete loadouts,
  throws on a missing or unsupported loadout, and the shipped default is
  untouched.
- Facing, retained pace, posture, footwork phase, and the phase timer are
  authoritative, deterministic, hashed for the new preset only, snapshotted,
  and readable in the inspector in plain language.
- Production local context equals the naive oracle field for field over
  explicitly permuted candidate spans.
- The proposal-conflict pass matches its independent pairwise oracle exactly.
- The scenario matrix generator produces the 21 unordered pairs and the 231
  team matchups with no omissions or duplicates, and its self-tests pass.
- Derived observability reaches neither hash, and disabled logging allocates
  nothing.
- The performance budget holds, with the measured medians recorded.
- `./scripts/verify.ps1` output pasted in full, by you.
- No manual smoke-checklist row flipped to `PASS` by any agent. Untested rows
  stay `PENDING`; blocked ones are reported as `BLOCKED` honestly.

## Handoff — required output for the weapon sessions

The five weapon sessions start from your branch. Write them a handoff section
in your report containing exactly this:

1. The allocated preset name and numeric value.
2. The loadout-to-combat-preset table from preflight, naming which preset each
   weapon session must select explicitly.
3. The six profile file paths, and which weapon session owns each.
4. The shared symbols a weapon session may call but not edit:
   the profile type, the resolver, `WeaponMovementRules`, the facing rules, the
   context query, and the scenario matrix, each with its namespace and its
   entry points.
5. The exact boundary-equality conventions you implemented — entry distance,
   ally clearance, disengage entry, disengage release — since the weapon plans
   each assert them and must match yours rather than each other.
6. The metric definitions a weapon session may assert against, from
   `MovementBehaviorMetrics`.
7. The V4 baseline numbers and the performance budget derived from them.
8. Anything in the shared plan you did not do, and why.

## Report back in this shape

1. **Preflight** — the five findings above.
2. **What landed** — files created or changed, one line each, with commit
   hashes.
3. **Tests** — the filters you ran and their real results.
4. **Gate** — pasted `verify.ps1` output.
5. **Determinism** — which content-hash literals moved, recomputed from what
   build, and proof that V1–V5 behaviour did not.
6. **Performance** — measured medians against the T0 baseline.
7. **Handoff** — the eight items above.
8. **Left out** — anything in the plan you did not do, and why. Scaling this
   work down is the user's call, not yours: finish everything else in full and
   say plainly what is missing.

Commits are Conventional Commits, normal English, one logical change each.

## What to do if you disagree with the scope

Say so in a sentence or two, then build it anyway under stated assumptions. If
a constraint above turns out to be genuinely impossible rather than merely
inconvenient, stop on that one item, finish everything else in full, and report
exactly what you left out and why.
