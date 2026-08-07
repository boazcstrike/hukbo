# Sandata scaffold — design

Date: 2026-08-07
Status: design, not yet authorized for implementation
Branch: `sandata-scaffold`, based on `main` at `8743e8b`
Input: [docs/research/2026-08-07-sandata-research-consolidated.md](../research/2026-08-07-sandata-research-consolidated.md)

Sandata is the working name for a second game in this repository: a modern-era,
top-down tactical shooter in the shape of Door Kickers 2. It shares an engine
spine with Hukbo but owns its own simulation, its own content, and its own hash
contract. Hukbo is finished at v0.1 and this work must leave it byte-identical.

Every claim about the current repository in this document is either quoted from
the research consolidation with its `file:line` citation intact, or re-checked
directly against the working tree while writing. Where this document departs
from the research consolidation, it says so in the text.

---

## 1. Goal and non-goals

### The goal of v0.1

Version 0.1 of Sandata is a deterministic, offline, single-player tactical
firefight that runs headlessly and renders under a spectator camera. It is the
Sandata equivalent of what Hukbo's battle simulation already is: an
authoritative simulation layer with a thin presentation shell on top, proven by
a repeatable seed workload rather than by watching it.

Concretely, v0.1 must be able to do all of the following and nothing beyond it:

- Load a hand-authored map from a line-oriented integer text file and derive
  every piece of navigation data from it at load time.
- Place two factions of autonomous operators on that map from spawn records.
- Have those operators group themselves, path themselves, move as squads
  through doorways and around angled walls, see through a frontal vision cone,
  hear gunfire and breaking glass, take cover directionally, run a firearm
  timing chain, shoot, and die.
- Produce an ordered event stream, a state hash, and an event hash that are
  identical for the same seed, the same build, and the same commands.
- Render all of the above with a procedural operator pawn, a fire cone, a
  contact list, and a small HUD, under Sandata's own theme.

### Explicit non-goals for v0.1

None of the following is in scope, and a task that starts one of them is out of
scope by definition rather than by judgement:

- **A campaign layer.** No mission chaining, no persistent squad roster, no
  equipment economy, no between-mission state of any kind. The same rule that
  keeps a barangay out of `Hukbo.Core` keeps a deployment out of `Sandata.Core`.
- **Player order issuing as the primary loop.** The order types, the multi-select
  state, the drag-capture pointer state, and the undo stack are scaffolded as
  types and UI. The simulation does not require an order to run and does not
  degrade without one. See section 2.
- **A wounding layer.** No bleedout, no downed state, no revive, no medic. Death
  is instant. The research consolidation records this as a Door Kickers 2
  simplification worth preserving, and it is preserved.
- **Grenades, breaching charges, shotguns, submachine guns, light machine guns,
  and launchers.** Rifles and pistols only, matching the weapon research scope.
- **Wall destruction at runtime.** A wall segment may be *tagged* breachable in
  the map format so the geometry is authored once, but nothing in v0.1 removes a
  wall segment during a mission.
- **A navmesh.** Grid A\* plus a funnel string-pull, for the reasons the research
  consolidation records. The navmesh is written down as the upgrade path and is
  not built.
- **Hierarchical A\*, flow fields, and waypoint graphs.** Rejected with reasons
  recorded in the research consolidation, and not relitigated here.
- **RVO, ORCA, and boids.** Rejected. Local avoidance is propose, prioritise,
  commit — never a force and never an impulse.
- **Three-dimensional weapon or character meshes.** The client is entirely
  procedural 2D. This is an assumption, flagged in section 15.
- **A map editor.** Maps are hand-written `.hkmap` files in v0.1.
- **Multiplayer, mod APIs, persistence migrations, and hosted CI.** Same
  prohibitions as Hukbo, for the same reasons.
- **Generating any audio.** The audio work in v0.1 stops at a dry-run manifest.
  Nothing calls ElevenLabs without the user reviewing that manifest and
  authorising the spend.

### The name is deferred

`Sandata` is a working name. It appears in this document, in the plan document,
and in the project, namespace, and file names both documents propose, because a
plan cannot be written against a blank identifier. It is trivial to change
before the first commit and expensive to change after, so the decision belongs
to the user before implementation starts, not to this document. It is question 3
in section 15.

---

## 2. Requirement decision

### The conflict, restated

The research consolidation records the conflict in its section 1, and this
section restates it rather than re-deriving it.

The user asked for Door Kickers 2 gameplay in which "the bots should be able to
automatically create the pathway" and "all the bots are automatically finding
pathways and automatically grouped together."

Door Kickers 2 does not work that way. Player troopers do not auto-path. The
player drags a polyline by hand, node by node. There are no formations and no
group move order — every trooper is pathed individually, and that is the
defining structural choice of the game. The only grouping primitives are `sync`,
which pace-matches one trooper to another, and go-codes, which assign a letter
to waypoints on several troopers so one keypress releases them all. Only the
enemy AI pathfinds.

So the request describes two different games at once. Taken literally as "Door
Kickers 2", the bots must *not* auto-path, because hand-pathing is the game.
Taken literally as "the bots automatically find pathways and group themselves",
it is not Door Kickers 2 at all — it is closer to what this repository already
is.

### The decision

The reading adopted here is the one the research consolidation proposes:

> Keep Hukbo's autonomous-agent spine. Replace melee resolution with Door
> Kickers 2's gunfight geometry, cover model, and aperture-driven level design.
> Bots path and group themselves. Scaffold the player order layer as types and
> UI, but do not make it the primary loop.

### Why

Four reasons, in descending weight.

**It is the reading that keeps the repository's existing proof.** Hukbo already
runs two autonomous factions under a spectator camera with a deterministic
20 Hz tick, a hashed state, and a 200-agent workload that passes. That spine is
the asset. A literal Door Kickers clone throws it away and replaces it with an
order queue and a hand-drawn polyline editor, which is a different and much
larger first milestone with none of the existing verification carried forward.

**It is the reading in which the hard parts are the interesting parts.** Under
this reading the difficult work is squad grouping, shared pathing, funnel
string-pulling around angled geometry, the doorway collapse, and the timing
chain. Under the literal reading the difficult work is a polyline editor and an
undo stack. The first list is what makes the product distinct; the second is
table stakes that Door Kickers 2 already ships.

**It makes determinism a feature rather than an obstacle.** Door Kickers 2's
enemy AI is explicitly non-deterministic run to run. A game whose *both* sides
are autonomous and byte-deterministic is a real differentiator, and it is only
possible if the autonomous side is the primary loop.

**The order layer stays reachable.** Scaffolding the order types and the
multi-select UI now, and letting the simulation consume an empty order queue,
means the literal Door Kickers reading remains one milestone away rather than a
rewrite. Tick stage 1 in section 5 applies orders ordered by
`(targetTick, orderSequence)` and is a no-op in v0.1. That stage exists on
purpose.

### This is an assumption awaiting confirmation

**This decision is the single largest assumption in the plan.** It is question 1
in section 15 and it is question 1 in the research consolidation's own open
questions. If the user confirms the literal Door Kickers reading instead, the
following change materially and the plan must be re-cut before implementation:

- Sections 8 (squad model) and 7 (navigation) shrink to enemy-AI-only.
- The order layer moves from "scaffold" to "primary loop", which promotes
  multi-select, drag capture, the undo stack, sync, and go-codes from scaffolded
  types to first-class tasks with their own verification.
- The tick pipeline in section 5 keeps its shape but stage 1 becomes the busiest
  stage rather than the emptiest.

A second, smaller assumption, also from the research consolidation and also
awaiting confirmation: **"generate the model for the guns" is read as
two-dimensional weapon geometry, not 3D meshes.** The client is entirely
procedural 2D and has no sprite or mesh asset pipeline; the content pipeline
ships fonts only. If 3D meshes were meant, this plan does not cover them and
does not cost them.

### Answered 2026-08-07: both readings, and the order layer is promoted

The question was put to the user before wave 5 started, as three options —
autonomous bots, a literal Door Kickers 2 in which the player draws every path,
or both. **The answer was both.** The game gets autonomous bots that path and
group themselves, and a player order layer that can override them with
hand-drawn paths.

That answer is additive rather than corrective, which is the cheapest of the
three outcomes and the reason asking before wave 5 was worth the message. Every
autonomous mechanism specified in sections 5 through 13 stands unchanged: squad
grouping, the shared group path, arclength slot targeting, the doorway collapse,
propose-prioritise-commit avoidance, the whole weapon and cover model. Nothing
in waves 1 through 5 is re-cut and nothing already merged is invalidated.

What changes is the status of the order layer. It stops being types and UI kept
warm for a later milestone and becomes a first-class subsystem with its own
authoritative state, its own work in tick stage 1, its own tests, and its own
place in the determinism contract. Three consequences are worth stating here
rather than leaving them to be discovered:

- **Tick stage 1 is no longer empty.** The table in section 5 says of stage 1
  "Empty in v0.1". Under this answer it applies a real order queue, ordered by
  `(targetTick, orderSequence)`. The stage's position and its ordering rule do
  not change; only its emptiness does.
- **A player's drawn polyline is authoritative input, not a derived path.**
  Section 4 makes published path polylines derived and recomputed on resume,
  and that rule is correct for a path a search produced. It is wrong for a path
  a person drew, because recomputing it would let the nav bake state at resume
  time silently rewrite the player's intent. The two path kinds are therefore
  governed by two different rules, and section 16 writes both down.
- **Precedence between an order and autonomy has to be a total written rule.**
  "The order usually wins" is not a rule a determinism test can assert. Section
  16 gives the per-tick movement-source rule and the exact conditions under
  which an assignment is cleared and autonomy resumes.

The full specification of the order layer is section 16, added the same day.

---

## 3. Project layout

### The decision

**Shared code is extracted into new `Hukbo.Shared.*` projects. `Sandata.Core`
does not reference `Hukbo.Core`.**

The extraction is done in two tiers. Tier 1 happens in v0.1 and is small,
mechanical, and hash-neutral. Tier 2 is written down here so the boundary is
known, and is explicitly deferred.

### The projects after v0.1

```
src/Hukbo.Shared.Core     engine primitives shared by both games: fixed point,
                          SplitMix64, FNV-1a, Facing16, FacingRules
src/Hukbo.Core            Hukbo's authoritative melee simulation  (unchanged in behaviour)
src/Hukbo.Client          Hukbo's MonoGame shell                  (unchanged)
src/Hukbo.Headless        Hukbo's determinism runner              (unchanged)
src/Hukbo.Diagnostics     JSON Lines debug log, shared by both games' shells
src/Sandata.Core          Sandata's authoritative shooter simulation
src/Sandata.Client        Sandata's MonoGame shell
src/Sandata.Headless      Sandata's determinism runner
tests/Hukbo.Core.Tests    unchanged, plus the new shared-primitive tests
tests/Hukbo.Client.Tests  unchanged, plus the widened build-gate facts
tests/Sandata.Core.Tests
tests/Sandata.Client.Tests
```

### The reference graph

| Project | References |
| --- | --- |
| `Hukbo.Shared.Core` | nothing |
| `Hukbo.Diagnostics` | nothing |
| `Hukbo.Core` | `Hukbo.Shared.Core` |
| `Hukbo.Client` | `Hukbo.Core`, `Hukbo.Diagnostics`, MonoGame |
| `Hukbo.Headless` | `Hukbo.Core`, `Hukbo.Diagnostics` |
| `Sandata.Core` | `Hukbo.Shared.Core` |
| `Sandata.Client` | `Sandata.Core`, `Hukbo.Diagnostics`, MonoGame |
| `Sandata.Headless` | `Sandata.Core`, `Hukbo.Diagnostics` |
| `Hukbo.Core.Tests` | `Hukbo.Core`, `Hukbo.Headless` (unchanged) |
| `Hukbo.Client.Tests` | `Hukbo.Client` (unchanged) |
| `Sandata.Core.Tests` | `Sandata.Core`, `Sandata.Headless` |
| `Sandata.Client.Tests` | `Sandata.Client` |

`Sandata.Core` references neither MonoGame nor `Hukbo.Diagnostics` nor
`Hukbo.Core`. `Sandata.Client` decides nothing about targeting, damage, cover,
or mission outcome. Both prohibitions mirror Hukbo's and both are asserted by
tests, not by intent — see section 13.

### Tier 1 extraction: exactly four files

`src/Hukbo.Shared.Core` receives, by `git mv`, exactly these files and nothing
else:

| File today | Accessibility today | Why it moves |
| --- | --- | --- |
| `src/Hukbo.Core/Mathematics/FixedPoint.cs` | `public` type, `internal static long IntegerSquareRoot` | Both games need Q22.10 and the exact bitwise square root |
| `src/Hukbo.Core/Determinism/SplitMix64.cs` | `public struct` | The pinned RNG; `System.Random` is banned in both games |
| `src/Hukbo.Core/Determinism/Fnv1a.cs` | `internal static class` | Both content hashes and both map hashes fold through it |
| `src/Hukbo.Core/Movement/Facing16.cs` | `public enum` | Pinned append-only sixteen-sector facing, reused by Sandata's coarse facing |

`src/Hukbo.Core/Determinism/StateHasher.cs` does **not** move. It hashes
Hukbo's agent state and is melee-shaped; Sandata gets its own hasher over its
own state, folding through the same `Fnv1a`.

#### Amendment, 2026-08-07: why `FacingRules.cs` does not move

This section originally listed `src/Hukbo.Core/Movement/FacingRules.cs` as a
fifth moved file. Implementation of task 1 proved that wrong and the file stays
in `Hukbo.Core`.

`FacingRules.DirectionBandPaceCapBasisPoints` takes a `LoadoutMovementProfile`
parameter (`FacingRules.cs:159-161`), and `LoadoutMovementProfile` depends in
turn on `Hukbo.Core.Combat.CombatLoadout`. Because both types live in the same
namespace as `FacingRules`, the dependency carries no `using` directive and was
invisible to a `using`-based coupling scan — which is exactly how it survived
into the design. Moving the file produces `CS0246` on that parameter type, and
the only ways out are to drag the melee loadout types into the shared assembly,
which inverts the dependency this design explicitly rejects, or to split the
class, which breaks the pure-rename bar that makes tier 1 safe.

Nothing is lost. Sandata never needed `FacingRules`: section 6 of this document
already records that its sector vectors are not unit length, so Sandata declares
its own pinned `ConeBoundaryTable` (plan task 11) rather than reusing them.
`Facing16` — the enum alone — carries no coupling at all and still moves, which
is all `Bam16.FromFacing16` requires.

The general lesson is recorded here because it will recur in tier 2: a
same-namespace type dependency is invisible to a `using` scan, so any future
extraction candidate must be checked by compiling it in isolation, not by
reading its import list.

### Why the move costs nothing at the call sites

`Hukbo.Shared.Core.csproj` sets `<RootNamespace>Hukbo.Core</RootNamespace>` and
the moved files keep their existing namespaces (`Hukbo.Core.Mathematics`,
`Hukbo.Core.Determinism`, `Hukbo.Core.Movement`). C# does not require a
namespace to match its assembly name, and this repository's `.editorconfig`
configures no namespace-folder rule, so nothing enforces the coupling.

The consequence is the whole reason this option was chosen: **not one `using`
directive anywhere in `Hukbo.Core`, `Hukbo.Client`, `Hukbo.Headless`, or either
Hukbo test project changes.** The research consolidation identifies call sites,
not moved bytes, as the real extraction risk — "every `using` must update, and
`TreatWarningsAsErrors` will surface a missed reference as a hard error". Pinning
the root namespace removes that risk class entirely rather than mitigating it.

Two consequences that do need handling:

- `Fnv1a` and `FixedPoint.IntegerSquareRoot` are `internal`. `Hukbo.Shared.Core`
  therefore carries `[assembly: InternalsVisibleTo("Hukbo.Core")]`,
  `"Hukbo.Core.Tests"`, `"Sandata.Core"`, and `"Sandata.Core.Tests"`. No member's
  accessibility is widened to `public`, so no new API surface is created.
- Type identity moves assemblies. The only place in the repository that reasons
  about assembly identity is
  `tests/Hukbo.Core.Tests/DiagnosticLoggingBoundaryTests.cs:24`, which is
  hardcoded to `typeof(Scenario).Assembly`. `Scenario` stays in `Hukbo.Core`, so
  that fact is unaffected. It is widened for a different reason in section 13.

### Tier 2 extraction, deferred with the boundary written down

The research consolidation lists a second, much larger set as "reusable after
extraction": the whole collision module (`CollisionGeometry`, `CollisionRules`,
`CollisionResolver`, `CollisionUniformGrid`, `CollisionPair`,
`CollisionPriority`, `CollisionMetrics`, `CollisionScratch`), the whole theming
package, the audio machinery minus its melee mapping, `UiButton` and the motion
helpers, `BattleOutcome`, and the settings store.

**None of that is extracted in v0.1.** Three reasons:

- Every one of those files lives in `src/Hukbo.Core/Simulation/` or
  `src/Hukbo.Client/`, in the same folder and namespace as `BattleSimulation`,
  `AgentState`, `Scenario`, and `FormationPlanner`, and every one of them is
  `internal`. Moving them is not the five-file mechanical move tier 1 is; it is a
  namespace reorganisation of the finished game.
- `CollisionScratch` needs its `Scenario` constructor parameter replaced by
  plain integers. That is a signature change inside the finished game before a
  single line of the new game exists.
- Sandata's collision needs are not yet known well enough to extract against.
  Eight to sixteen indoor operators is a different workload from 200 agents in
  an open arena, and extracting a shared abstraction before the second consumer
  exists is exactly the "general abstraction added without an accepted need" the
  reviewer checklist in `SIMULATION-GAME-STANDARDS.md` section 10 rejects.

Sandata v0.1 therefore writes its own uniform grid, its own pair emission, and
its own resolver in `Sandata.Core`, following the same three-phase shape
(propose without seeing other proposals, prioritise by a total order, commit
sequentially) rather than sharing the code. That is deliberate duplication with
a written expiry condition: **when Sandata's collision has run a full gate and
its needs are known, tier 2 extraction becomes its own design document.** Until
then the duplication is cheaper and far less risky than the abstraction.

### Why not the alternative

The rejected option was `Sandata.Core` taking a `ProjectReference` on
`Hukbo.Core` directly. It was rejected on four grounds:

- **It is not actually zero-risk.** Nearly everything Sandata would want from
  `Hukbo.Core` is `internal`: the collision module, `CollisionPriority`,
  `FormationPlanner`, `AgentState`. Consuming them means either widening them to
  `public` or adding `InternalsVisibleTo("Sandata.Core")`. The first changes
  Hukbo's API surface; the second gives the new game unrestricted access to the
  finished game's private state. Both are larger interventions than moving five
  self-contained files.
- **It inverts the dependency.** Sandata would depend on Hukbo's melee ruleset,
  weapon enums, movement presets, and content hashes transitively. A change to
  the kampilan's clash multiplier would sit in Sandata's dependency closure. That
  is the wrong direction: the shooter has nothing to learn from a shield bearer.
- **It puts a melee content hash in the shooter's build.** `CombatRuleset` and
  `MovementRuleset` content hashes would be reachable from `Sandata.Core`,
  inviting exactly the accident the determinism contract exists to prevent.
- **It makes the eventual split impossible.** If Sandata ever leaves this
  repository, a `Hukbo.Shared.Core` package travels; a `ProjectReference` on the
  whole of `Hukbo.Core` does not.

The chosen option touches five files in Hukbo, changes zero call sites, widens
zero accessibility, and leaves both games able to move independently. That is
the trade the research consolidation frames as "extraction is cleaner but touches
Hukbo" versus "direct reference is zero-risk but dirty", resolved by finding the
extraction shape that touches Hukbo almost not at all.

### Seven new csproj files, all warning-clean from line one

`Directory.Build.props` applies `net10.0`, `LangVersion 14.0`, nullable,
`ImplicitUsings`, `TreatWarningsAsErrors`, `Deterministic`, `EnableNETAnalyzers`,
`EnforceCodeStyleInBuild`, `RestorePackagesWithLockFile`, and NuGet audit to
every project in the tree automatically, with no visible opt-out. Nothing in the
new projects may weaken any of it.

| New project | Shape |
| --- | --- |
| `src/Hukbo.Shared.Core/Hukbo.Shared.Core.csproj` | Library. `RootNamespace` pinned to `Hukbo.Core`. `RuntimeIdentifiers win-x64`. Four `InternalsVisibleTo`. No package references. |
| `src/Sandata.Core/Sandata.Core.csproj` | Library. `RuntimeIdentifiers win-x64`. `InternalsVisibleTo` for `Sandata.Core.Tests`. Project reference to `Hukbo.Shared.Core`. No package references. |
| `src/Sandata.Client/Sandata.Client.csproj` | `WinExe`, `RuntimeIdentifier win-x64`. MonoGame package references, its own `Content/Content.mgcb`, its own theme JSON and audio glob. `AllowUnsafeBlocks` only if it adopts the same SDL P/Invoke Hukbo's `ArenaGame` uses; otherwise omitted. |
| `src/Sandata.Headless/Sandata.Headless.csproj` | `Exe`. Project references to `Sandata.Core` and `Hukbo.Diagnostics`. `InternalsVisibleTo Sandata.Core.Tests`, mirroring `Hukbo.Headless`. |
| `tests/Sandata.Core.Tests/Sandata.Core.Tests.csproj` | xunit. References `Sandata.Core` and `Sandata.Headless`. `Fixtures/**` copied to output, mirroring `Hukbo.Core.Tests`, because the golden `.hkmap` must be readable at run time. |
| `tests/Sandata.Client.Tests/Sandata.Client.Tests.csproj` | xunit, `RuntimeIdentifier win-x64`. References `Sandata.Client`. Links Sandata's theme JSON and `.mgcb` into output the way `Hukbo.Client.Tests` does. |

All seven go into `Hukbo.slnx` explicitly; a project absent from the solution is
never seen by `build.ps1`, `format.ps1`, or the gate.

**No new NuGet package.** `SourceHygieneTests.PinnedPackageNames`
(`tests/Hukbo.Client.Tests/SourceHygieneTests.cs:165`) is compared for exact
equality against `Directory.Packages.props`, so any new package fails the gate
and is a reviewed dependency change with lock-file regeneration. Sandata uses the
same five packages Hukbo uses. The navigation code is hand-ported precisely
because, as the research consolidation records, the intersection of "proven" and
"deterministic" is empty for .NET navigation libraries.

Every project's `packages.lock.json` is generated by the restore that follows,
and all of them are committed.

---

## 4. Determinism contract

Sandata inherits `SIMULATION-GAME-STANDARDS.md` section 4 and `CLAUDE.md`
section 5 unchanged. This section states only what is specific to Sandata.

### Units, and why they are chosen

| Quantity | Unit | Value |
| --- | --- | --- |
| Distance | world unit (`wu`), stored as `FixedPoint` raw at `Scale` 1024 | 1 metre = 16 wu |
| Time | integer tick | `TickRate` 50, so one tick is exactly 20 ms |
| Angle, coarse | `Facing16` | 16 sectors, pinned, append-only, reused unchanged |
| Angle, fine | `Bam16` | binary angular measurement, `ushort`, 65,536 to the turn |
| Body radius | world unit | 4.25 wu, `CollisionRules.DefaultBodyRadiusRaw` unchanged, which is 0.266 m — a 0.53 m human footprint |

The metre scale is chosen so that every published Door Kickers figure lands on
an integer: 15 m is 240 wu, 20 m is 320 wu, 50 m is 800 wu, the 4.5 m bolt-cutter
sound radius is 72 wu, the 25 m breacher-shotgun radius is 400 wu. A 40 m by
45 m building is 640 by 720 wu, and at raw scale that is 655,360 by 737,280 —
comfortably inside `int` with room for the `long` intermediates every predicate
uses.

The tick rate is 50 Hz rather than Hukbo's 20 Hz because the gunfight is a
timing chain measured in tens of milliseconds. At 20 Hz a pistol's 80 ms ready
time is 1.6 ticks and the chain quantises into meaninglessness; at 50 Hz it is
exactly 4 ticks and the finest published distinction, the 150 ms versus 180 ms
pistol aim time, is 1.5 ticks apart. Fifty hertz also keeps the collision
invariant `MovementSpeedRaw <= BodyRadiusRaw` comfortable: a 5 m/s sprint is
80 wu per second, which is 1.6 wu per tick against a 4.25 wu radius.

### Milliseconds are authored; ticks are derived

Every published weapon timing is an integer millisecond count. Those integers
are what the weapon table stores. They are converted to ticks exactly once, at
ruleset bake time, by one pinned rule:

```
ticks = (milliseconds * TickRate + 500) / 1000        // integer, half away from zero
```

The rule identifier and the tick rate both fold into
`SandataRuleset.ContentHash`, so changing either is a new preset version with new
golden expectations, exactly as `CLAUDE.md` section 5 requires. The alternative —
authoring tick counts directly — bakes the tick rate into every data row and
makes a future rate change a 38-row hand edit with no signal if one row is
missed.

### The two hashes

Sandata carries two independent hashes, matching Hukbo's discipline:

- **State hash.** FNV-1a over the authoritative state listed below, in a fixed
  field order, at a cadence recorded in the mission record.
- **Event hash.** FNV-1a over the ordered authoritative event stream.

They are independent on purpose: a bug that moves state without emitting an
event moves one and not the other.

### What is authoritative and hashed

Everything in this list is in the snapshot and in the state hash:

- `Tick`, `Phase`, `Winner`, `NextEntityId`, `NextEventSequence`.
- Per operator: position (two `FixedPoint`), `Facing16`, `Bam16` aim angle,
  health, faction, intent, posture (standing or crouched),
  `WeaponLowered` flag, weapon chain phase and remaining ticks, magazine rounds,
  cyclic-fire accumulator, and suppression counter.
- Per operator: contact memory — for each remembered enemy, the last known cell,
  the contact tier, and the tick it was last seen.
- Per faction: alert level, one of `Calm`, `Raised`, `Breach`.
- Per door: open or closed, and the tick it last changed.
- Per group: the destination record and the outstanding path request
  `(groupId, startCellIndex, goalCellIndex, requestTick)`.
- Per RNG stream: algorithm identifier, root seed, and stream state.
- `MissionContentHash` and `SandataRuleset.ContentHash`.

**Correction, 2026-08-07: the squad slot index was on this list and should not
have been.** Section 8 states plainly that group id, leader, membership, and
slot index are all derived each tick from positions and entity ids, and stores
nothing per group. This list contradicted that by naming the slot index as a
hashed per-operator field, and task 17 implemented the list rather than the
section, so `OperatorState` carries a `SquadSlotIndex` that nothing derives from
and nothing may trust.

Section 8 is the reasoned statement and it wins. The slot index is derived, and
it is removed from this list and from the operator record. Being derived does
not stop it ordering the movement commit: `(groupId, slotIndex, entityId)` stays
the commit key, because a derived value computed identically on every run orders
just as totally as a stored one.

Task 28 found this from the inside — it was told to assert that no group state is
stored, and could not honestly assert it about a field it was forbidden to
touch — and reported it rather than quietly widening its own scope.

### What is derived and never hashed, never snapshotted

Everything in this list is rebuilt from authoritative state and is excluded from
the snapshot, from the state hash, and from the event hash. This is
`SIMULATION-GAME-STANDARDS.md` section 10's "derived caches are excluded from
saves and rebuild without drift", applied by name:

- The nav grid, including wall rasterisation and body-radius inflation.
- The clearance field.
- The wall bucket index and the cell-to-wall-segment lists.
- A\* scratch: the open set, the closed set, `gScore`, `came-from`, and the
  visited stamps.
- **Published path polylines and their cumulative arclengths.**
- Line-of-sight results and vision-cone membership for the current tick.
- The collision uniform grid and its pair list.
- The read-only render snapshot, every render metric, and every audio cue.

The path polyline being derived is the one entry that needs a written rule,
because it visibly steers movement. The rule is:

> A path is a pure function of the nav data, the start cell, and the goal cell.
> The *request* is authoritative and snapshotted; the *result* is not. On resume,
> every outstanding and every published path is recomputed from its stored
> request record before the first tick executes.

That works only because the request stores the start *cell index at request
time*, not the position at resume time. A save-resume equivalence test asserts
identical state and event hashes across a mid-mission snapshot, and it is the
only thing that proves the rule holds.

### Ordering and randomness

- Total order for every multi-result query, breaking on `EntityId` last.
- The movement commit order is `(groupId, slotIndex, entityId)`.
- The A\* open-set order is `(f, h, nodeIndex)`. This is total, so any correct
  heap gives one answer. `Array.Sort` is introsort and priority queues are never
  stable, so the comparator carries the totality, not the sort.
- Search state lives in flat arrays indexed by node index. No dictionary and no
  hash set may reach gameplay, because enumeration order changes with capacity
  growth.
- Heuristics are the integer octile form `10 * (max - min) + 14 * min`. No float
  heuristic, ever.
- `Math.Sqrt`, `Math.Atan2`, `float`, and `double` are banned from
  `Sandata.Core` for the same reason `System.Random` is banned: they are
  `double` transcendentals with no cross-version guarantee. `FixedPoint.Sqrt`,
  `Cordic.Atan2`, and the pinned sine table replace them.
- Path amortisation is by **fixed latency**, never by per-tick budget: a path
  requested at tick `t` becomes valid at tick `t + PathLatencyTicks` regardless
  of how many searches the machine actually completed. A budget makes arrival
  depend on how many groups happened to request that tick, which is harmless
  until someone adds a "no path yet, move at the goal directly" fallback, at
  which point the simulation branches on scheduling.
- Random streams derive from `(missionSeed, systemTag, entityId or eventId)` so
  adding a draw in one system cannot shift an unrelated outcome. The system tags
  in v0.1 are `Accuracy`, `Reaction`, `Sidestep`, and `SpawnJitter`.
- Single-threaded authoritative schedule. No parallel flood fill, ever: visit
  order becomes thread-schedule-dependent and that is a guaranteed desync.
- C# integer division truncates toward zero, so every world-to-cell conversion
  uses explicit floor division. The map format cannot express a negative
  coordinate at all (section 12), which removes the trap inside map space, but
  the helper exists because relative offsets are signed.
- The diagonal-corner tie in the grid ray is resolved by a written rule — step X
  first — not by a float comparison.
- Path smoothing compares an exact integer cross product against exactly zero.
  There is no epsilon anywhere in `Sandata.Core`.

### The preset

`SandataPresetId.ModernTacticalV1 = 1`, append-only, numeric values pinned by a
test. Changing an enum's numeric value, an enum's order, the roster order, a
weapon weight, the tick rate, the millisecond conversion rule, or a hash mixer
requires a new preset value plus new golden expectations. That rule is inherited
verbatim from `CLAUDE.md` section 5 and is not softened for a new game.

---

## 5. The Sandata tick pipeline

### A note on the source list

`SIMULATION-GAME-STANDARDS.md` section 4 documents a **twelve**-stage general
contract, not thirteen: apply commands, apply spawns and despawns, rebuild the
spatial index, find eligible hostiles and select targets, select intent, compute
movement proposals, commit movement, create hitscan attack proposals, apply
damage simultaneously, resolve death and victory, emit events, produce the hash
and render snapshot. Section 13 of that document records the longer order the
battle simulation actually executes today, including its collision stages. The
count is noted here because the plan brief describes it as thirteen; the design
below is written against the twelve-stage contract as it actually reads.

### The asymmetry, and the decision

The general contract has a real seam in it. Stage 3 rebuilds the spatial index
from authoritative positions, and stages 4 and 5 — target selection and intent —
read that index, which reflects positions as of the *start* of the tick. Stage 7
commits movement, and every stage after it reads state committed *this* tick. So
sensing looks at last tick's world while resolution looks at this tick's world,
and the contract does not say which side a new stage belongs on.

For a melee game the seam is nearly invisible, because agents in contact barely
move between ticks. For a shooter it is the whole game: whether a shot fired this
tick resolves against the target's pre-move or post-move position decides
whether a runner can be hit, and whether a unit can be seen deciding to shoot at
a position it can no longer see.

**Decision: the seam sits immediately before the movement commit. Every sensing
stage reads the frozen tick-start view. Every resolution stage reads state
committed this tick.**

Why this way round, and not the other:

- **Sensing must be simultaneous, or entity order decides who sees whom.** If
  vision ran against committed positions, operator 3 would evaluate its cone
  against operator 7's *new* position while operator 7 evaluated its cone against
  operator 3's *old* one, purely because 3 sorts first. That is exactly the
  "incidental call order decides an outcome" failure the determinism contract
  forbids. Freezing the view makes sensing order-independent by construction,
  which is a property a test can actually assert: permuting the processing order
  of stages 5 through 9 must not change a single output.
- **Resolution must see the truth it is resolving against.** Movement commit is
  already sequential under a total order and already reads the collision grid as
  it fills. Damage applied against stale positions would let two operators
  occupy the same cell and both be hit by a shot that geometrically passed
  between them.
- **The seam is observable, which section 10 question 8 requires.** The visible
  artifact is that an operator decides to shoot at where the target was at the
  start of the tick, and the shot resolves against where it is now — so a
  sprinting target crossing a doorway can be missed by a shot that was correctly
  aimed 20 ms earlier. That is a legible, explainable behaviour, not a bug, and
  the agent inspector shows both the decision position and the resolution
  position.

### The fourteen stages

Every stage below declares which world it reads. New systems declare their stage;
incidental call order never decides an outcome.

| # | Stage | Reads | Notes |
| --- | --- | --- | --- |
| 1 | Apply orders, ordered by `(targetTick, orderSequence)` | authoritative | Empty in v0.1. The stage exists so the order layer is one milestone away, not a rewrite. |
| 2 | Apply queued spawns and despawns | authoritative | |
| 3 | Rebuild the collision uniform grid and freeze the tick-start view | authoritative | This is where the frozen view is taken. |
| 4 | Apply door state changes and rebake the affected nav and clearance cells | authoritative | Doors are the only runtime nav mutation in v0.1. Rebake is local, not global. |
| 5 | Sensing: line of sight, vision cone, contact tier, hearing | **tick-start view** | Order-independent by construction. |
| 6 | Squad grouping: union-find over the sorted pair list; derive group id and leader | **tick-start view** | No stored group state. See section 8. |
| 7 | Path service: publish paths whose latency elapsed, enqueue new requests, run at most one A\* per group | **tick-start view** | Fixed latency, never a budget. |
| 8 | Select intent: hold, advance, breach, engage, reposition, dead | **tick-start view** | |
| 9 | Compute movement proposals from the shared polyline and the slot arclength offset | **tick-start view** | No unit sees another's proposal. |
| 10 | Commit movement in `(groupId, slotIndex, entityId)` order against the collision grid | **committed** | Blocked units try one 22.5-degree sidestep, then wait a tick. |
| 11 | Advance every weapon timing chain by one tick; evaluate the weapon-lowered rule | **committed** | The lowered test is against the position just committed. |
| 12 | Create hitscan fire proposals; resolve line of sight, cover arc, and accuracy | **committed** | |
| 13 | Apply accumulated damage simultaneously; resolve death and mission outcome | **committed** | |
| 14 | Emit ordered events; produce the scheduled state hash and the read-only render snapshot | **committed** | |

Two rules bind the table:

- **Nothing between stages 5 and 9 may write authoritative state that another
  unit in the same stage range then reads.** Proposals accumulate into a
  write-only buffer that stage 10 consumes. This is the property the
  order-independence test asserts.
- **Nothing at or after stage 10 may consult the tick-start view.** The frozen
  view is released at the end of stage 9, and a test asserts that no type
  reachable from stages 10 to 14 holds a reference to it.

---

## 6. Math additions

Every function below is integer or fixed-point, has no `float` or `double`
anywhere in its body or signature, and is a pure function of its arguments. The
"golden vectors" column means a test that pins named input-output pairs as
literal constants, which is the discipline the pinned SplitMix64 vectors already
follow.

Every geometry signature below takes flat `long` coordinate pairs — never a
`Point` or a `Box` value type. This table originally wrote several geometry
signatures against a `Point` and a `Box` that this document had not defined
and that no task ever created. Task 4 faced the decision first, implementing
`ClassifySegments` against flat coordinates rather than guessing at an
undefined struct, and tasks 6, 11, and 20 each followed the same convention
independently rather than each guessing differently. Reconciled 2026-08-07 by
task 56: flat `long` coordinates are confirmed as the house style, because a
`Point` or `Box` value type would need its own equality, hashing, and
ordering rules to stay deterministic, and none of that machinery earns its
cost when every call site already carries the coordinates as separate
scalars. The signatures below are corrected to match what was actually
implemented.

| Name | Signature | Algorithm | Lives in | Golden vectors |
| --- | --- | --- | --- | --- |
| `FixedPoint.operator *` | `static FixedPoint operator *(FixedPoint left, FixedPoint right)` | `checked((int)((long)left.RawValue * right.RawValue / Scale))`. Truncation toward zero is the documented behavioural contract, not an accident. | `Hukbo.Shared.Core/Mathematics/FixedPoint.cs` | Yes — including both signs, the exact half case, and the overflow boundary |
| `FixedPoint.operator /` | `static FixedPoint operator /(FixedPoint left, FixedPoint right)` | `checked((int)(((long)left.RawValue * Scale) / right.RawValue))`. Division by zero throws `DivideByZeroException`; it is never silently clamped. | same | Yes |
| `FixedPoint.Sqrt` | `static FixedPoint Sqrt(FixedPoint value)` | `FromRaw(checked((int)IntegerSquareRoot((long)value.RawValue * Scale)))`. Public wrapper over the existing exact bitwise root; pre-multiplies by `Scale` so the result is in the same representation. Rejects a negative input. | same | Yes — perfect squares, non-squares, zero, and the largest representable input |
| `Bam16` | `readonly record struct Bam16(ushort Raw)` | Binary angular measurement, 65,536 to the turn. Wraparound is free because `ushort` arithmetic wraps. | `Sandata.Core/Mathematics/Bam16.cs` | Yes |
| `Bam16.ShortestArc` | `static short ShortestArc(Bam16 from, Bam16 to)` | `(short)(to.Raw - from.Raw)`. The signed shortest arc is exactly the `short` cast of the unsigned difference — no branch, no modulus, no special case at the wrap. | same | Yes — the four quadrant crossings and both wrap directions |
| `Bam16.FromFacing16` | `static Bam16 FromFacing16(Facing16 facing)` | `sector * 4096`. Sixteen sectors into 65,536 divides exactly. | same | Yes, all sixteen |
| `Bam16.ToFacing16` | `static Facing16 ToFacing16(Bam16 angle)` | `(Facing16)(((angle.Raw + 2048) >> 12) & 15)`. Round-to-nearest sector with the half case pinned upward. | same | Yes, including both half boundaries |
| `Trig.Sin` / `Trig.Cos` | `static int Sin(Bam16 angle)` returning raw at scale 65,536 | 257-entry quarter-wave table with integer linear interpolation between entries; quadrant reflection by index arithmetic. The table is pinned literal data and is hash contract. | `Sandata.Core/Mathematics/Trig.cs` | Yes — the whole table is the vector, plus the four quadrant boundaries and two interpolated mid-entries |
| `Cordic.Atan2` | `static Bam16 Atan2(long y, long x)` | Integer CORDIC in vectoring mode, sixteen iterations, shifts and adds only, sixteen pinned arctangent constants. Accuracy about 0.0055 degrees. Replaces `Math.Atan2` entirely. | `Sandata.Core/Mathematics/Cordic.cs` | Yes — the eight axis and diagonal cases exactly, plus a swept comparison against a pinned expected table |
| `IntegerMath.FloorDiv` / `FloorMod` | `static int FloorDiv(int value, int divisor)` | Branchless floor division. C# truncates toward zero, which merges cells across the origin unless corrected. | `Sandata.Core/Mathematics/IntegerMath.cs` | Yes — negatives, exact multiples, and the divisor-of-one case |
| `ExactPredicates.Orient` | `static int Orient(long ax, long ay, long bx, long by, long cx, long cy)` returning `-1`, `0`, or `1` | `(bx - ax) * (cy - ay) - (by - ay) * (cx - ax)` in `long`, sign only. The single geometric primitive the whole engine rests on. | `Sandata.Core/Geometry/ExactPredicates.cs` | Yes — collinear, both turn directions, and the magnitude boundary where the product approaches `long` range |
| `ExactPredicates.ClassifySegments` | `static SegmentRelation ClassifySegments(long ax, long ay, long bx, long by, long cx, long cy, long dx, long dy)` | Four orientation tests. `Disjoint`, `Crossing`, `Touching`, and `CollinearOverlap` are four **named** results, each carrying a written rule. There is no epsilon and no "close enough" branch. | same | Yes — one vector per named result, plus the shared-endpoint and T-junction cases |
| `RayBox.Intersects` | `static bool Intersects(long originX, long originY, long directionX, long directionY, long boxMinX, long boxMinY, long boxMaxX, long boxMaxY)` | Slab method **without division**: each parametric bound is kept as a rational `(numerator, denominator)` pair and compared by cross-multiplication with the sign of the denominators carried explicitly. | `Sandata.Core/Geometry/RayBox.cs` | Yes — axis-parallel rays, corner grazes, and origin-inside |
| `Polygon.Contains` | `static bool Contains(ReadOnlySpan<long> vertexXs, ReadOnlySpan<long> vertexYs, long pointX, long pointY)` | Crossing number with a half-open edge rule (`y1 <= py < y2`), which removes every degenerate case without special-casing a vertex hit or a horizontal edge. | `Sandata.Core/Geometry/Polygon.cs` | Yes — vertex hit, edge hit, horizontal edge, and the classic "ray exits through a vertex" case |
| `VisionCone.Contains` | `static bool Contains(Bam16 centre, ushort halfWidth, long rangeSquared, long dx, long dy)` | A range check against `rangeSquared`, then two half-plane cross products against boundary vectors read from a pinned table. **Never a cosine comparison.** Every term stays inside `long`; there is no normalisation and no length assumption. The `rangeSquared` parameter was added by task 11 beyond what this row originally specified, because a vision cone with no distance limit is not the shape a sensing system needs. | `Sandata.Core/Geometry/VisionCone.cs` | Yes — on-boundary in both directions, behind the apex, a reflex cone, and the range cutoff |
| `NavHeuristic.Octile` | `static int Estimate(int dx, int dy)` | `10 * (max - min) + 14 * min`, with the orthogonal step cost fixed at 10 and the diagonal at 14. Admissible, integer, and identical on every platform. | `Sandata.Core/Navigation/NavHeuristic.cs` | Yes |
| `ClearanceField.Build` | `static void Build(ReadOnlySpan<byte> passability, Span<int> clearance, int width, int height)` | Two-pass integer chamfer distance transform with the same `(10, 14)` weights as the heuristic, so a clearance value is directly comparable to a formation half-width in the same units. | `Sandata.Core/Navigation/ClearanceField.cs` | Yes — a hand-computed 8×8 fixture asserted cell by cell |
| `GridRay.Traverse` | `static int Traverse(long originX, long originY, long targetX, long targetY, Span<int> cells, NavGrid grid)` | Amanatides-Woo digital differential analyser, rewritten division-free: parametric values stay rational and are compared by cross-multiplication. The diagonal-corner tie steps X first, by written rule. Enumerates cells in strict order for the line-of-sight narrow phase. | `Sandata.Core/Navigation/GridRay.cs` | Yes — axis-parallel, exact diagonal through a corner, and a shallow 18.4-degree line |
| `Funnel.StringPull` | `static int StringPull(ReadOnlySpan<int> corridor, Span<long> outputX, Span<long> outputY, NavGrid grid)` | Recast's simple stupid funnel algorithm in integers, ported from DotRecast (zlib, attribution only). Snaps a grid corridor to the real vector wall geometry using `Orient` alone. | `Sandata.Core/Navigation/Funnel.cs` | Yes — a straight corridor collapsing to two points, an L corridor to three, and an 18.4-degree corridor whose output is a single straight segment |

### The two verified gaps this table closes

Both were checked directly against the working tree by the research
consolidation and re-checked while writing this section.

**`FixedPoint` has no multiply and no divide.** It declares only `+`, `-`, and
the four comparison operators (`src/Hukbo.Core/Mathematics/FixedPoint.cs:90-106`).
Every multiply in the codebase today routes through `MultiplyRatio` or raw `long`
arithmetic outside the type. `IntegerSquareRoot` is `internal` and takes and
returns `long`, not `FixedPoint` (`FixedPoint.cs:61`). `Scale` is 1024, so the
representation is Q22.10 (`FixedPoint.cs:8`).

Adding `*`, `/`, and `Sqrt` to `FixedPoint` after it moves to
`Hukbo.Shared.Core` is **pure addition**. No existing member changes, no existing
call site changes, and no existing behaviour changes, so it cannot move a Hukbo
hash. The gate proves that: the seed-1 workload's state and event hashes must be
identical before and after.

**The facing sector vectors are not unit length.** From
`src/Hukbo.Core/Movement/FacingRules.cs:29-45` the sector components include 946,
724, and 392 at a scale of 1024. But 946² + 392² is 1,048,580 against 1024²'s
1,048,576 — off by four — and 724² doubled is 1,048,352, off by 224. The error
differs per sector, so any cone test written as a cosine comparison that assumes
`|f| == Scale` produces a subtly different cone shape depending on which way the
unit faces. `Facing16` is pinned append-only and cannot be widened
(`src/Hukbo.Core/Movement/Facing16.cs:8-10`).

This is exactly why `VisionCone.Contains` uses two half-plane cross products
against boundary vectors from a pinned table and never a cosine. The consequence
is that widening a cone becomes a data change with a visible diff rather than a
constant nudged in a comparison. **`Facing16` is not widened and its table is not
edited.** `Bam16` is a new, separate type; `FromFacing16` and `ToFacing16` are the
only bridges between them.

---

## 7. Navigation

### Shape

Uniform integer grid A\* decides topology; a funnel string-pull snaps the
resulting corridor to the real vector wall geometry using exact integer
orientation predicates.

The grid contains no geometry predicate at all, so its entire determinism
surface collapses to one comparator that is unit tested in isolation. The
string-pull is what buys the angles: a unit crossing an 18.4-degree corridor
walks a straight 18.4-degree line rather than a staircase. The result is
navmesh-quality output from grid-quality input, with zero authoring.

### Nav cell size

**One nav cell is 4 world units, which is a quarter of the 16 world unit visual
tile and a quarter of a metre.**

Consequences, all of them deliberate:

- The cell size is a power of two, so the world-to-cell conversion is a shift
  rather than a division. The map format asserts this (section 12).
- A 4.25 wu body radius inflates to `ceil(4.25 / 4) = 2` cells, so a body fits
  where the grid says it fits.
- A 640 by 720 wu map is 160 by 180 cells, which is 28,800 nodes. The maximum
  supported map, 2048 by 2048 wu, is 512 by 512 cells, or 262,144 nodes — four
  flat `int` arrays of that length is 4 MB, allocated once at load.
- A standard 0.9 m doorway is 14.4 wu, which is 3.6 cells, so a doorway is
  three passable cells wide before inflation and one after. That single cell is
  what forces the single-file collapse in section 8, and it falls out of the
  geometry rather than being special-cased.

### Data structures: flat arrays, no dictionaries

Every per-node array is allocated once at load, sized `width * height`, and
indexed by `nodeIndex = y * width + x`. No `Dictionary`, no `HashSet`, no
`SortedSet`, and no `PriorityQueue<TElement, TPriority>` reaches gameplay,
because dictionary enumeration order changes with capacity growth and
`PriorityQueue` is not stable.

| Array | Type | Lifetime |
| --- | --- | --- |
| `passability` | `byte[]` — 0 blocked, 1 open, 2 door (passable to the planner at high cost, impassable to the mover until opened) | built at load, mutated only by stage 4 |
| `clearance` | `int[]` in chamfer units | built at load, rebuilt locally on a door change |
| `wallBucketStart`, `wallBucketItems` | `int[]` — a compressed-sparse-row index from cell to wall segment | built at load, immutable |
| `gScore` | `int[]` | A\* scratch |
| `cameFrom` | `int[]` | A\* scratch |
| `visitStamp` | `int[]` — a monotonically increasing search id, so the arrays are never cleared between searches | A\* scratch |
| `openHeap` | `int[]` binary heap of node indices | A\* scratch |

The `visitStamp` trick matters for determinism as well as speed: clearing a
262,144-entry array per search is the kind of cost that later tempts someone to
add a budget, and a budget is what section 4 forbids.

### The comparator

The open set is ordered by the total key `(f, h, nodeIndex)`, compared in that
order:

```
compare(a, b) = f[a] - f[b], else h[a] - h[b], else a - b
```

Because `nodeIndex` is unique, the key is total, so any correct heap
implementation produces the same expansion order. That is the point: the
comparator carries the determinism, not the container. `Array.Sort` is introsort
and is not stable; `PriorityQueue` makes no stability promise. Neither fact can
hurt a total comparator.

`f` uses the integer octile heuristic, `h`, with the orthogonal step cost 10 and
the diagonal 14. Neighbour enumeration reads one pinned static offset table in
one pinned order — east, south-east, south, south-west, west, north-west, north,
north-east — and diagonal moves are rejected when either orthogonal neighbour is
blocked, so a unit never cuts a wall corner.

### Fixed-latency path amortisation

A path requested at tick `t` becomes valid at tick `t + PathLatencyTicks`,
`PathLatencyTicks` being a ruleset constant folded into the content hash. The
search itself may execute on any tick in that window, and how many searches the
machine actually completed is invisible to the simulation.

Until a path is valid, the group's units hold their current intent. There is no
"no path yet, move directly at the goal" fallback, because that is precisely the
branch that would make the simulation depend on scheduling. A group with no valid
path and no current path holds position and emits an inspectable reason code,
which is what `SIMULATION-GAME-STANDARDS.md` section 10 question 8 requires.

At most one A\* runs per group per tick, and a group with an outstanding request
does not enqueue a second one. With eight groups and a 160 by 180 grid this is
comfortably inside a 20 ms tick, and the benchmark workload measures it rather
than assuming it.

### The clearance field

A two-pass integer chamfer distance transform over `passability`, weights
`(10, 14)` matching the heuristic, producing for each open cell the chamfer
distance to the nearest blocked cell. Pass one sweeps top-left to bottom-right
reading the four already-visited neighbours; pass two sweeps bottom-right to
top-left reading the other four. It is exact for these weights, it is
deterministic by construction, and it is `O(n)`.

Two consumers:

- **The single-file collapse.** Section 8. When the clearance under a group's
  leader drops below the formation half-width, every slot's lateral offset goes
  to zero.
- **Path cost shaping.** A cell whose clearance is below the body diameter costs
  more to traverse, so a squad prefers the corridor it fits down. This is a cost
  term, not a hard constraint, so a narrow corridor is still routable when it is
  the only way through.

A door change rebakes only the cells within the chamfer's radius of influence of
the changed cells, which is bounded and, importantly, is recomputed identically
from scratch state on resume.

### Line of sight, two phase

Phase one is `GridRay.Traverse`, the division-free Amanatides-Woo walk, which
enumerates in strict order the few cells whose wall buckets need checking. Phase
two is `ExactPredicates.ClassifySegments` against the real wall list in those
buckets, which answers authoritatively.

Supercover rasterisation alone is insufficient. It answers "which cells does
this line touch", which is only a proxy for "does this line cross a wall", and on
an 18.4-degree wall the two answers visibly disagree with the drawn geometry. The
player sees the wall, not the grid, so the grid may only ever be a broad phase.

### The ported algorithms, and the dependency question

Repository policy prefers a proven library over hand-rolled code. The honest
finding recorded in the research consolidation is that **the intersection of
"proven" and "deterministic" is empty for .NET navigation code**: DotRecast and
Roy-T.AStar are float-based and make no determinism claim, SharpNav is
float-based and ships a `System.Random` field that `CLAUDE.md` section 5 bans by
name, GoRogue drags two transitive numeric dependencies, and every fixed-point
option is experimental, Unity-coupled, or introduces a second fixed-point type
that would fork the hash contract.

The policy therefore resolves not to "hand-roll everything" but to **port two
specific, extremely well-proven algorithms in integer form**:

- **Recast's funnel string-pull**, from DotRecast, zlib licence, attribution
  only. Small, already integer-friendly, decades of production use.
- **Recursive shadowcasting field of view**, from GoRogue, MIT licence. Used for
  the fog-of-war cell visibility layer that sits behind the per-unit cone.

Both are ported with a licence header naming the source and the licence, and
neither adds a NuGet package, so `PinnedPackageNames` stays exact.

### Amendment, 2026-08-07: a grid corridor needs line-of-sight smoothing, and the funnel alone cannot deliver the promise above

The paragraph above says the string-pull is what buys the angles, and that a unit
crossing a shallow-angle corridor moves in a straight line rather than a
staircase. Implementation measured that claim and it does not hold as stated.
The reasoning was sound and the port is correct; the mismatch is between the
algorithm and the shape of the data it was given here.

**The measurement.** Task 65 published a path across a fully open ten-by-four
cell region with no walls at all, from cell `(0,0)` to cell `(9,3)`. The taut
path across empty ground is the single segment `(2,2)` to `(38,14)` in world
units. What the funnel produced was `(2,2)`, `(4,4)`, `(8,8)`, `(12,12)`,
`(38,14)` — five points, deviating from the straight line by about 6.7 world
units, roughly one and three quarter cells, at its worst. Task 26 had already
reached the same conclusion analytically from the other direction, by
hand-tracing all nine portals of its own fixture before writing any code.

**Why.** Recast's funnel operates on navmesh portals, and a navmesh portal is as
wide as the polygons that share it. A grid A\* corridor is a chain of single
cells, so every portal is one cell edge wide, and the funnel's freedom to pull
the string taut is bounded by that width everywhere along the path. It removes
the staircase steps it has room to remove, and it cannot straighten what the
corridor never gave it room to straighten. This is a property of the input, not
a defect in the port, and no amount of tuning changes it.

**The fix: greedy line-of-sight smoothing, over the corridor, using the wall
bucket index this design already builds.** Anchor at the first point; advance a
probe to the furthest corridor point still visible from the anchor; emit that
point and make it the new anchor; repeat to the goal. The visibility test is
`LineOfSight.IsVisible`, which is the exact-predicate, epsilon-free test the
shooting model already uses, so smoothing inherits the determinism of a
subsystem that is already pinned rather than introducing a second geometry
convention. On open ground it yields exactly two points, which is the taut path;
around an obstacle it yields the minimum vertices that keep every segment clear.

**What happens to the funnel port.** `Funnel.StringPull` stays in the tree with
its licence header and its tests, and it is documented as not being on v0.1's
publish path. It is the right algorithm the moment the corridor is wider than
one cell — a navmesh, or a widened corridor built from the passable cells
around the path — and both are plausible later milestones. Keeping unused code
has a real cost and this is a deliberate choice rather than an oversight: the
alternative considered was widening the corridor at publish time so the funnel
had room to work, which is strictly more machinery than the line-of-sight pass
for the same result in v0.1.

The prose above this amendment is left as written so the reasoning that produced
the funnel decision stays legible. This amendment, not that prose, is the
current rule.

---

## 8. Squad model

This is the mechanism that answers the "automatically grouped together" half of
the requirement.

### Grouping is derived, not stored

Groups form by deterministic union-find over the pair list the collision uniform
grid already emits, normalised so each pair is `(lower, higher)` and sorted
ascending before the union pass. Two operators of the same faction within
`GroupCohesionRadius` are unioned.

- **Group identity is the minimum entity id in the component.**
- **The leader is the lowest living entity id in the component.**

Both are derived rather than stored. That single decision buys three properties
at once:

- They survive snapshot and resume with no extra state, because there is no
  extra state.
- They re-derive on death with no leader-election tick, no timer, and no
  interregnum in which a squad has no leader.
- They cannot desync, because the only inputs are positions and entity ids,
  which are already hashed.

`ContingentId` from Hukbo's `FormationPlanner` is the existing precedent for
persistent contingent identity and its *pattern* is followed. The type itself is
not shared, because it lives in `Hukbo.Core/Simulation/` with the melee
simulation and tier 2 extraction is deferred (section 3).

### One A\* per group, never per unit

A group runs one search per destination. Eight searches instead of sixty-four is
the smaller reason. The larger reason is that squadmates cannot select
topologically different routes around the same pillar — which is the most common
way a squad visibly falls apart in this genre, and no amount of per-unit tuning
fixes it, because the two units are each individually correct.

### Arclength slot offsets

The shared search result is a polyline carrying precomputed cumulative integer
arclength at each vertex. Each unit's target is then a pure function of one
scalar: its own slot offset along that arclength.

```
targetPoint(slot) = pointAtArclength(leaderArclength - slot.TrailOffset)
                  + lateralNormal * slot.LateralOffset
```

Followers are literally standing on the leader's past path, so they cut the same
corners automatically. Rigid lateral offsets in world space would push the
outside file into the wall on every corner; this is the whole trick and it is why
the arclength is precomputed rather than recovered per tick.

Slot assignment within a group is by ascending entity id, so it is total and
stable, and a death re-packs the slots deterministically on the next tick.

### Doorway collapse falls out of the clearance field

When the clearance at the leader's cell drops below the formation half-width,
`slot.LateralOffset` goes to zero for every slot in the group and the squad
becomes a single file. On the far side, clearance rises and the offsets return.

There is no state, no timer, and no special case inside the pathfinder. The
collapse is a pure function of a baked field and a constant, which means it is
free on resume and identical on replay.

### Local avoidance: propose, prioritise, commit

Three ordered rules, matching the shape already in the repository:

1. **Propose** without seeing any other proposal. Stage 9.
2. **Prioritise** by the total order `(groupId, slotIndex, entityId)`.
3. **Commit** sequentially against the collision grid. Stage 10. A blocked unit
   first tries a single 22.5-degree sidestep, choosing the side by a rule pinned
   on `entityId` parity so it is total; if that is also blocked, it waits a tick.

**Never a force, never an impulse, never a push-apart.** Boids are force
accumulation, which is rigid-body physics under another name and is banned by
`CLAUDE.md` section 9; they also fan out in corridors and mill in doorways. RVO
and ORCA can be made fixed-point — Klotho is the existence proof — but every
degenerate case in the linear program becomes a tie requiring a written total
order, and constraint insertion order changes the solution even when the
constraint set does not. That is a large, subtle determinism surface for
something eight indoor units do not need.

### State and per-tick cost

**Stored per group: nothing.** Group id, leader, membership, and slot index are
all derived each tick from positions and entity ids.

**Stored per group destination:** the destination cell and the outstanding path
request `(groupId, startCellIndex, goalCellIndex, requestTick)`. These are
authoritative because a path is recomputed from them on resume.

**Stored per operator:** the slot's trail offset and lateral offset are constants
of the formation shape, not state. Nothing else.

Per-tick cost, for `n` operators, `p` collision pairs, and `g` groups:

| Stage | Cost | At `n = 16`, `g = 4` |
| --- | --- | --- |
| Pair emission | `O(n)` amortised over the grid | trivial |
| Pair normalise and sort | `O(p log p)` | `p` is under 40; trivial |
| Union-find with path compression and union by size | `O(p α(n))` | effectively linear |
| Group id and leader derivation | `O(n)` | trivial |
| A\* | `O(g)` searches, each `O(k log k)` in expanded nodes | measured, not assumed — the benchmark reports p50/p95/p99 per search |
| Slot target evaluation | `O(n)` with a binary search into the arclength array, `O(log v)` in vertices | trivial |

The A\* row is the only one that can hurt, and it is the row the benchmark
workload exists to measure. `SIMULATION-GAME-STANDARDS.md` section 11 requires a
fixed benchmark matrix naming map density, changed-cell count, concurrent
seekers, query distance, replanning rate, target hardware, and p50/p95/p99 query
and tick-stage time. That matrix is a task, not a promise.

---

## 9. Weapon model

### Everything is an integer and everything is tick-denominated

No `float`, no `double`, no percentage stored as a fraction. Timings are authored
in milliseconds and converted once at ruleset bake by the pinned rule in
section 4. Angles are `Bam16`. Distances are world units. Probabilities are
integer numerator-over-65536 draws from a named RNG stream.

### The definition record

```csharp
public readonly record struct FirearmDefinition(
    FirearmId Id,                    // append-only enum, numeric values pinned
    WeaponClass Class,               // Rifle | Pistol
    CaliberFamily Caliber,           // one of eight; selects the report sample
    MechanismGroup Mechanism,        // Ak | Ar | Bullpup | Pistol; selects mechanism samples
    FireModeSet Modes,               // [Flags] Safe | Single | Burst2 | Burst3 | Auto

    // Timing chain, milliseconds as authored
    int ReadyMs,                     // raise from lowered
    int AimBaseMs,                   // aim at a centred target
    int AimPerBamMs,                 // added per 1024 Bam of off-centre offset
    int ResetMs,                     // between engagements
    int TurnBamPerTick,              // rotation rate; heavier weapons turn slower

    // Range bands, world units
    int AutoBandMaxWu,               // 0 .. here selects Auto if the weapon has it
    int BurstBandMaxWu,              // .. here selects Burst3 or Burst2
    int SingleBandMaxWu,             // .. here selects Single; beyond it, no engagement

    // Accuracy, dispersion in Bam
    int DispersionAtZeroWu,
    int DispersionAtMaxWu,
    int MaxEffectiveWu,

    // Magazine and cycling
    int MagazineCapacity,
    int ReloadMs,
    int CyclicRpm,

    // The one rule that generates the game
    bool ExemptFromLoweredRule);     // true for pistols only
```

### The timing chain

Resolution order per engagement, straight from the research consolidation:
`readyTime` (raise the weapon), then turn or rotate, then `aimTime`, then fire,
then `resetTime` before the next engagement. Whoever completes the chain first
wins, and under roughly 14 m the time-to-kill is effectively instant, so the
fight is decided by the chain and not by damage numbers.

Published figures the table is seeded from: rifle ready around 405 ms against
roughly 80 ms for a 1911; aim time around 350 ms under 14 m, 150 to 180 ms for a
pistol, around 335 ms for a rifle, 500 ms and up for a designated marksman rifle.
Heavier weapons rotate more slowly, and a target near the edge of the vision cone
takes longer to engage than a centred one — which is what `AimPerBamMs` encodes.

The chain is a state machine on the operator, advanced exactly one tick per tick
in stage 11:

```
Lowered → Raising(ReadyTicks) → Turning(until |ShortestArc| <= AimToleranceBam)
        → Aiming(AimTicks) → Firing → Resetting(ResetTicks) → Aiming
```

Every phase carries a remaining-tick counter in the hashed state. Any transition
that would leave a counter at zero resolves in the same tick, in one written
order, so a zero-tick phase cannot swallow a tick or double-advance.

### Fire mode by range band

Published bands: full-auto 0 to 15 m, burst 16 to 20 m, single 21 to 50 m,
varying per weapon and ammunition, with lower recoil widening the bands. In world
units at 16 wu per metre those defaults are 240, 320, and 800.

Selection is a total, ordered rule with no ties:

```
if range <= AutoBandMaxWu   and Modes has Auto              -> Auto
else if range <= BurstBandMaxWu and Modes has Burst3        -> Burst3
else if range <= BurstBandMaxWu and Modes has Burst2        -> Burst2
else if range <= SingleBandMaxWu and Modes has Single       -> Single
else                                                        -> no engagement
```

`Burst3` is tested before `Burst2` so a weapon carrying both is deterministic;
in the current roster no weapon carries both, and a test asserts that so the
ordering rule never becomes load-bearing silently.

This is also the hook that ties the simulation to the audio library: the
simulation picks the mode, and the mode picks the sound slot. Section 10.

### Accuracy interpolation

Dispersion is linear in range between two authored `Bam` values:

```
dispersionBam = DispersionAtZeroWu
              + (DispersionAtMaxWu - DispersionAtZeroWu) * min(range, MaxEffectiveWu)
                / MaxEffectiveWu
```

Integer, truncating, clamped at `MaxEffectiveWu`. The shot's angular error is
then a draw from the `Accuracy` stream in `[-dispersionBam, +dispersionBam]`,
and the hit test is the exact segment predicate against the target's body circle
approximated as its axis-aligned box, resolved with `RayBox.Intersects`.

Cover applies afterwards, not inside the dispersion term: flat 50 percent damage
and hit reduction, applied only within the arc the cover object actually faces
unless the object is declared 360 degrees. Two operators behind the same car do
not both get it — only the one inside the arc. Fire from the flank or rear
ignores cover entirely. Crouching behind cover is near-total protection but
forbids firing, which makes it a survive-the-magazine button rather than a
fighting stance.

### Magazine and cycling

`CyclicRpm` becomes a per-round tick interval that cannot drift. At 50 Hz an
800 rpm weapon fires every 75 ms, which is 3.75 ticks, so the state carries an
integer accumulator:

```
accumulator += 1000 * TickRate                  // ticks-worth of milliseconds, scaled
while (accumulator >= 60_000_000 / CyclicRpm) { fire(); accumulator -= 60_000_000 / CyclicRpm; }
```

The result is a deterministic 4, 4, 4, 3 pattern rather than an accumulating
rounding drift. The accumulator is hashed state. Automatic fire stops when the
magazine empties, the target leaves the cone, or the intent changes; there is no
"burst length" random draw.

### The weapon-lowered rule

One conditional generates the whole game. Crossing a doorway, or standing within
`LoweredWallDistanceWu` of a wall, forces the weapon **lowered**, which re-imposes
`ReadyMs` when it must come back up. Pistols are exempt via
`ExemptFromLoweredRule`. This is why a pistol beats a rifle in a doorway, and it
is the mechanical core of the product.

Evaluated in stage 11 against the position just committed, using the wall bucket
index for the proximity query and the door cell tag for the doorway test. The
lowered flag is hashed state, and the transition into it emits an authoritative
event so the spectator can see the cause rather than only the effect.

### How the 38 weapons are stored: a code table, not a data file

**Decision: the roster is a `static readonly FirearmDefinition[]` in
`Sandata.Core/Weapons/FirearmCatalog.cs`, in `FirearmId` order.**

Justification, weighed against a data file:

- **The roster is hash contract.** `SandataRuleset.ContentHash` folds FNV-1a over
  the field stream of every row. A code table gets that for free in a
  `static` constructor; a data file needs a loader, a validator, an error path,
  and a canonicalisation pass before it can be hashed — all of which already
  exist for the map format and would have to exist twice.
- **A data file buys extensibility nobody has asked for.** There is no modding
  requirement in v0.1 — `CLAUDE.md` section 9 explicitly defers mod APIs — and
  the roster is authored by the same people who compile the game.
- **`TreatWarningsAsErrors` and the analysers work on a code table.** A missing
  field, a duplicated `FirearmId`, or a mode set that does not exist is a
  compile error rather than a load error discovered by a test.
- **There is no culture surface.** A code table has no parser, so the class of
  bug where an integer parses differently under two cultures cannot occur.
  Section 12 spends real design effort making the map format immune to that; the
  weapon table gets immunity by not being a file.
- **The diff is legible where reviewers already look.** Changing the M4A1's aim
  time shows up in a `.cs` diff next to the comment explaining why, rather than
  in a data blob nobody reviews.
- **At 38 rows the cost of a data file is all cost.** A data file starts paying
  for itself somewhere in the hundreds of rows with non-programmer authors. This
  is neither.

Two supporting rules:

- Tests assert row count, `FirearmId` uniqueness and density, that every row's
  `Modes` is one of the five distinct sets the research records, and that the
  content hash is stable against a pinned expected value.
- The one thing a data file would genuinely have bought — swappable display
  names for the trademark question — is bought instead by a single configurable
  field. `WeaponNameSetId { Manufacturer, Generic }` selects between two parallel
  string tables in `Sandata.Core/Weapons/WeaponNameSets.cs`. Real names stay in
  the data and the documentation; the shipped display set is one field.

### The mode sets, which drive both simulation and audio

`M4` and `M4A1` are separate rows on purpose: M4 is `Safe | Single | Burst3`,
M4A1 is `Safe | Single | Auto`. They are different weapons to the simulation and
to the audio library. Likewise AK-12 (2018/2021) and AK-15 carry `Burst2` while
the AK-12 2023 model deletes it.

The five distinct sets in the roster:

| Set | Covers |
| --- | --- |
| `Safe \| Single \| Auto` | nineteen rifles |
| `Safe \| Single \| Burst3` | M16A4, M4 |
| `Safe \| Single \| Burst2 \| Auto` | AK-12 (2018/2021), AK-15, G36 |
| `Single` | striker-fired pistols with no manual safety |
| `Safe \| Single` | the remaining pistols |

Two audio consequences that must not be lost, carried forward into section 10:

- A burst must be a **baked** asset, not an automatic loop trimmed to three
  rounds. The mechanical burst cam produces an uneven cadence a loop cannot
  reproduce.
- The Steyr AUG has no rotary selector, only a cross-bolt push-button safety and
  a progressive trigger. Its mode-change sound is a button thunk and must not
  share the AK or AR selector sample.

---

## 10. Audio architecture

### A data-table catalog, and why the enum cannot survive

Hukbo's `SoundCatalog` maps a `GameSoundId` enum member to a base file name
through a `switch`, and its variant axis is `HitClass` — skull, neck, ribcage,
gut, limb, extremity — hardcoded melee body parts
(`src/Hukbo.Client/Audio/SoundCatalog.cs:103-113`). Gunfire has no hit location,
so under that shape every weapon and fire-mode combination would need its own
enum member plus a switch arm (`SoundCatalog.cs:32-47,57-77`). At Sandata's scale
that is unacceptable enum bloat.

**Sandata gets a data-table catalog. The melee catalog stays exactly as it is,
untouched.** Nothing in `Hukbo.Client/Audio` is edited by this plan.

The Sandata catalog is a `static readonly SoundSlot[]` in
`Sandata.Client/Audio/SandataSoundCatalog.cs`, each row a value record:

```csharp
internal readonly record struct SoundSlot(
    SoundFamily Family,          // GunReport | GunLoop | GunTail | Mechanism | Dry | Impact | Casing | Ui
    int FamilyKey,               // CaliberFamily, MechanismGroup, ImpactSurface, ... by ordinal
    FireMode Mode,               // None for non-gun families
    SoundEnvironment Environment,// CloseDry | IndoorTail | OutdoorTail | Distant | Suppressed
    byte VariantCount,           // 1..99
    int TailTicks);              // how long an instance is held; see the pool rule below
```

Slot lookup is a pure function from `(Family, FamilyKey, Mode, Environment)` to
a row index through a precomputed flat index array — not a dictionary, so nothing
about enumeration order can reach anything.

### Slot naming: weapon by fire mode by environment by variant

The base file name is built by one pure function, and it is the whole contract
between the audio folder and the game:

```
<family>-<key>-<mode>-<environment>-<NN>.wav
```

| Family | Example | Meaning |
| --- | --- | --- |
| `gun` | `gun-556x45-single-indoor-03.wav` | single report, 5.56×45, indoor tail, variant 3 |
| `gun` | `gun-545x39-burst2-close-01.wav` | baked two-round burst, 5.45×39, close-dry |
| `gunloop` | `gunloop-762x39-auto-outdoor-02.wav` | automatic loop body |
| `guntail` | `guntail-762x39-auto-outdoor-02.wav` | the matching automatic tail |
| `mech` | `mech-ar-selector-none-01.wav` | AR-pattern selector detent |
| `mech` | `mech-bullpup-selector-none-01.wav` | the AUG's push-button thunk, a different sample by construction |
| `dry` | `dry-9x19-none-none-02.wav` | dry fire |
| `impact` | `impact-concrete-none-none-04.wav` | |
| `casing` | `casing-rifle-concrete-none-03.wav` | |

Note that the caliber, not the weapon, keys the report. Six report families cover
all 24 rifles — 7.62×39, 5.45×39, 5.56×45, 7.62×51, 6.8×51, 5.8×42 — and two more
cover the pistols — 9×19 and 5.8×21. Eight families in total, not 38 weapons.
Per-weapon character comes from the mechanism sounds layered on top of the family
report. This is what makes a 500-file library tractable rather than absurd.

Environments are `CloseDry`, `IndoorTail`, `OutdoorTail`, `Distant`, and
`Suppressed`. Mechanism groups are `Ak`, `Ar`, `Bullpup`, and `Pistol`.

### How the simulation's mode choice selects the slot

The simulation decides the fire mode by range band in stage 12 and emits an
authoritative `ShotFired` event carrying `(FirearmId, FireMode, ShooterEntityId,
RangeWu, Tick)`. The client, in its presentation layer only, resolves that to a
slot:

```
family      = mode is Auto ? GunLoop : GunReport
familyKey   = FirearmCatalog[firearm].Caliber
environment = f(shooter is indoors, range band, suppressor fitted)
slot        = SandataSoundCatalog.Find(family, familyKey, mode, environment)
variant     = SoundVariantSelector.Select(tick, shooterEntityId, slot.VariantCount)
```

Two properties are load-bearing:

- **The simulation never names a file.** It names a mode. The mapping from mode
  to slot lives entirely in `Sandata.Client`, so adding a sound cannot move a
  hash and a missing sound file cannot change a fight.
- **Variant selection reuses the existing selector unchanged.**
  `SoundVariantSelector` seeds from `tick * MixConstant XOR sourceEntityId`
  through SplitMix64 (`src/Hukbo.Client/Audio/SoundVariantSelector.cs:20-32`).
  It is already game-agnostic and it is copied by reference, not by fork —
  `Sandata.Client` calls the same helper, which is one of the few `Hukbo.Client`
  types that carries no melee coupling. If tier 2 extraction happens, it moves;
  until then Sandata takes an `InternalsVisibleTo` on it or a two-line local
  wrapper, and that choice is a task-level detail, not a design decision.

### The 99-variant filename cap

The filename format allows two variant digits, capping variants at 99 per slot
(`src/Hukbo.Client/Audio/SoundCatalog.cs:26`). Sandata keeps the same
`{0:D2}` format and the same cap, and stays far under it: the manifest in the
research consolidation declares at most 6 variants per slot.

Two tests enforce it rather than trusting it:

- No `SoundSlot` may declare `VariantCount` above 99 or below 1.
- The generated manifest's largest per-slot variant count must equal the
  catalog's declared count for that slot, so the folder and the table cannot
  drift apart silently.

### The MonoGame instance pool is the real ceiling

`SoundCueBudget` allows 64 cues per frame and 16 per sound
(`src/Hukbo.Client/Audio/SoundCueBudget.cs:27-28`), and eight shooters at 800
rounds per minute is only about 1.8 shots per frame at 60 fps, which is
comfortable. The cue budget is not the problem.

`MonoGameSoundPlayer.Play` catches `InstancePlayLimitException`
(`src/Hukbo.Client/Audio/MonoGameSoundPlayer.cs:107-126`), and gunshot tails hold
an instance three to five times longer than the measured 0.191-second melee mean
across the 70 shipped WAVs. The existing budget was tuned against a melee
measurement of 21 cues per frame and has never seen sustained automatic fire.

Three rules follow:

- **`TailTicks` is declared per slot** and the Sandata budget holds a slot's
  reservation for `TailTicks` rather than for one frame. A one-frame budget
  against a five-frame tail is exactly how a pool exhausts without the budget
  ever reporting a rejection.
- **Automatic fire plays one loop instance plus one tail instance per shooter,
  not one instance per round.** A shooter holding the trigger holds two
  instances, not thirty-two. This is the single largest pool saving available and
  it is why `GunLoop` and `GunTail` are separate families.
- **The ceiling is measured before shipping, not guessed.** A hand-run harness
  under `tools/` — not in `Hukbo.slnx`, not in the gate, matching how the
  existing measurement harnesses are treated — sustains automatic fire from the
  maximum operator count and records the instance count at which
  `InstancePlayLimitException` first fires. That number, on named hardware, goes
  into `docs/development/testing.md`, and only then does the budget get its
  constants. Until then the constants are marked provisional in code comments,
  exactly as `SoundCueBudget`'s current ones are.

### Trim threshold

`scripts/sfx.ps1` defaults to a 5 percent-of-peak trim (`scripts/sfx.ps1:128`),
and the script already solved this exact problem once for a different sound
class: tonal user-interface cues use 2.0 because "a pitched tone decays smoothly
and 5 percent audibly chops the tail" (`scripts/sfx.ps1:230-233`). Gunshot
variants carrying reverb or echo need 1 to 2 percent, or no trimming at all.
Sandata's slot families therefore declare their own trim threshold the way the UI
class already does, rather than inheriting the melee default.

### Generation is gated, and the gate is a hard stop

ElevenLabs bills sound effects at 200 credits per generation. The 484-slot matrix
is 96,800 credits at zero rejects. The Creator tier at 22 USD per month provides
121,000 credits. The project's own skill documentation records real take-quality
variance, with one run peaking at 93 percent usable and another under 1 percent,
so a realistic run with a 30 to 50 percent reject rate needs 650 to 750
generations, or 130,000 to 150,000 credits, which overruns Creator and requires
the Pro tier at 99 USD. **Realistic cost: 22 USD best case, 99 USD likely.**
Whether credits scale with requested duration could not be confirmed and is
**UNVERIFIED**.

At 0.6 to 1.0 seconds each in the current 24 kHz stereo 16-bit PCM format, 500
files is 27.5 to 46 MB, a roughly 25-fold increase in this repository's tracked
audio — the 70 shipped WAVs total 1,289,596 bytes — that every clone pays for,
and there is no Git LFS configuration in this repository. That is a risk-register
row, not a footnote.

Therefore, and without exception:

> **The audio work in v0.1 produces a dry-run manifest and stops. No task in the
> plan document calls ElevenLabs. Nothing is generated until the user reviews the
> manifest and authorises the spend.**

---

## 11. Client and UI

### Sandata gets its own theme record

`UiThemeColors` declares 27 roles (`src/Hukbo.Client/Theming/UiTheme.cs:11-38`)
and `UiThemeCatalog.ValidateDocument` rejects any unknown role
(`src/Hukbo.Client/Theming/UiThemeCatalog.cs:272-279`).

**Decision: Sandata declares its own colour record in
`Sandata.Client/Theming/SandataThemeColors.cs`. The five existing melee themes
and the 27-role record stay untouched.** Bolting tactical roles onto the shared
record would force every melee theme to author meaningless colours or break the
catalog's exact-role-count invariant.

The Sandata record has **39 roles**, derived as follows.

**23 kept unchanged** from the existing 27: `CanvasBackground`, `ArenaSurface`,
`ArenaBorder`, `StatusSurface`, `OverlayScrim`, `PanelSurface`, `PanelAlternate`,
`PanelBorder`, `TextPrimary`, `TextSecondary`, `TextDisabled`, `TextInverse`,
`ActionDefault`, `ActionHover`, `ActionFocus`, `ActionPressed`, `ActionActive`,
`ActionDisabled`, `StatusInfo`, `StatusSuccess`, `StatusWarning`, `StatusDanger`,
`NewEvent`.

**4 repurposed:** `TeamA` becomes `Friendly`, `TeamB` becomes `Hostile`,
`OtherFaction` becomes `UnknownContact`, `Selection` becomes `SelectedTrooper`.

**12 added:** `Suppressed`, `Downed`, `OrderPath`, `Waypoint`, `CoverGood`,
`CoverNone`, `BreachPoint`, `FireConeFill`, `FireConeEdge`, `AlertCalm`,
`AlertRaised`, `AlertBreach`.

Note that `Downed` is a *presentation* role for the death animation frame, not a
gameplay state — there is no downed state in the simulation. Note also that alert
is three roles rather than a boolean, because the simulation carries three alert
levels.

**A discrepancy worth recording.** The research consolidation's prose says a
tactical shooter needs "roughly 35" roles, but its own arithmetic — keep 23,
repurpose 4, add 12 — sums to 39, and 27 plus 12 additions is 39 by inspection.
This design uses **39** and treats the "roughly 35" figure as an estimate the
enumeration superseded. `ArenaSurface` and `ArenaBorder` keep their names despite
now describing a building interior, because renaming them buys nothing and costs
a diff in every consumer.

Three test-enforced rules carry over from the existing discipline:

- Every new role clears the same contrast-pair checks the existing roles face in
  `UiThemeCatalog.GetRequiredRenderedContrastPairs`.
- `Friendly`, `Hostile`, and `UnknownContact` are theme-independent constants,
  matching the existing discipline in `FactionColorPalette`. A theme may not make
  friend and foe similar.
- Every state change conveys through **shape as well as colour**, never colour
  alone. Suppression adds a bracket, not a tint.

### The operator pawn is a procedural extension, not new technology

There are no textures anywhere in the pawn path. Every element is a draw of a
shared one-by-one pixel texture into a rectangle, a rotated block, or a thick
line, composed across fifteen layers in `PawnRenderer.DrawLayout`
(`src/Hukbo.Client/Rendering/PawnRenderer.cs:267-458`).

Two facts make the modern operator a direct extension:

- `DrawRotatedBlock` (`PawnRenderer.cs:1032-1058`) already performs arbitrary
  continuous rotation about a pivot, which is exactly what a continuously-aimed
  rifle needs and is strictly harder than the discrete swing arcs Hukbo requires.
- `layout.WeaponEnd` (`src/Hukbo.Client/Rendering/PawnGeometry.cs:82`) is already
  the tip of the weapon line, which is the muzzle flash anchor.

**Decision: stay procedural.** There is no sprite asset pipeline to stand up —
the content pipeline ships fonts only — and a sprite sheet would need its own
pure helper layer for frame selection anyway, which is the same discipline the
geometry already has.

`Sandata.Client/Rendering/OperatorGeometry.cs` mirrors `PawnGeometry`'s shape: a
pure `Create` function returning an `OperatorLayout` record of rectangles,
points, and angles, with every layer `Rectangle.Empty` when it contributes
nothing. It is testable without a `GraphicsDevice`, which is the whole reason
`PawnGeometry` is split from `PawnRenderer` and is non-negotiable here too.

**The one genuinely new requirement is a persistent facing angle.** Today's
`SwingPose.WeaponAngleRadians` is a transient swing-only pose that springs back
to neutral; an operator's weapon must track its aim continuously.
`OperatorLayout` therefore carries `WeaponAimBam` sourced from the simulation's
hashed `Bam16` aim angle, plus a presentation-only smoothing term that is
explicitly excluded from the render snapshot's equality and never fed back.

Layers, in composition order: ground ring, boots, legs, torso, plate carrier,
arms, weapon body (rotated block about the grip anchor), weapon foregrip, head,
helmet, night-vision mount, muzzle flash (at `WeaponEnd`, one frame), sling,
suppression bracket, selection ring.

### HUD element list

| Element | Anchored | Shows | Status in v0.1 |
| --- | --- | --- | --- |
| Roster strip | bottom-left | one tile per friendly operator: health, weapon, magazine, chain phase, alive or dead | **built** |
| Contact list | right column | every remembered enemy: tier (unknown, question-mark, identified), last-known cell age in ticks | **built** |
| Alert indicator | top-centre | `Calm`, `Raised`, `Breach`, with shape as well as colour | **built** |
| Mission clock and tick counter | top-right | integer tick, derived seconds | **built** |
| Event log | right column, below contacts | ordered authoritative events, at most 200 retained | **built** |
| Operator inspector | left panel on selection | intent, reason code, chain phase and remaining ticks, cover state and arc, group id, slot index, decision position and resolution position | **built** — this is the "spectator can discover the effect" requirement, so it is not optional |
| Spectator control bar | bottom-centre | pause, step one tick, speed, restart | **built**, reusing Hukbo's control-bar shape |
| Fire cone overlay | in-world | per-operator vision cone, `FireConeFill` and `FireConeEdge`, at every detail tier | **built** |
| Order path overlay | in-world | the polyline and waypoints for a selected group | **scaffolded** — renders a path when one exists, has no editor |
| Breach-point marker | in-world | map-declared breachable wall faces | **scaffolded** — drawn, not interactive |
| Minimap | top-left | none exists anywhere in the client today | **scaffolded** — a bordered panel drawing the nav grid's passability at one pixel per cell, no interaction |
| Multi-select marquee | in-world | drag rectangle over friendlies | **scaffolded** — `AgentSelection` is single-entity only today, so Sandata gets its own multi-select state as a pure record with tests, and the pointer path that drives it |
| Undo stack | — | `ConfirmationPrompt` guards destructive exit only and is not a general undo | **scaffolded** — a typed stack with push, pop, and depth limit, and no producers in v0.1 |

"Scaffolded" means the types exist, are unit tested as pure helpers, and are
reachable from the UI, but nothing in v0.1 requires them to be driven. That is
the honest reading of the section 2 decision, and a plan task that promotes one
to "built" is out of scope.

### Pointer priority

Sandata inherits the client's existing pointer priority discipline: the topmost
consuming element wins, a consumed event does not fall through, and the in-world
layer is last. Marquee drag capture is a new state in that chain and sits above
the in-world layer and below every panel, so a drag starting on a panel never
becomes a marquee.

---

## 12. Map format

### The decision

Line-oriented text, integers only, extension `.hkmap`. Not JSON and not TOML.

Three reasons, the third decisive:

- A line-oriented record format makes one semantic change equal one changed line
  in a diff.
- It is typeable by hand without a schema, which matters because v0.1 has no map
  editor.
- **It has no syntax capable of expressing a float.** The class of bug where a
  fraction parses differently under two cultures is structurally impossible
  rather than merely tested against.

### Parsing rules

- Parsing uses `NumberStyles.None` with `CultureInfo.InvariantCulture`, which
  rejects signs, decimal points, group separators, and surrounding whitespace.
- **A consequence worth naming: the format cannot express a negative number at
  all.** Every coordinate is therefore non-negative, the map origin is `(0, 0)`
  at the top-left, and the floor-division trap on negative coordinates cannot
  occur inside map space. `IntegerMath.FloorDiv` still exists because relative
  offsets are signed; it is simply not needed for the world-to-cell conversion of
  a map coordinate.
- A malformed line is a **hard load error**, never a skipped line. So is an
  unknown record kind, a wrong token count, a non-integer token, an out-of-range
  value, and a duplicate record.
- Blank lines and lines whose first character is `#` are removed before parsing.
  Comments do not survive into the canonical form and therefore do not reach the
  hash.
- Tokens are separated by exactly one space. An empty token is an error.
- Records are sorted canonically before baking, so file line order cannot reach
  the nav data. A duplicate record is a load error, which is what makes the
  canonical comparator total.

### Record kinds

Header records appear once each, in this order, before any body record.

| Kind | Fields | Validation |
| --- | --- | --- |
| `HKMAP` | `version` | Must be line 1. Must equal `1`. |
| `NAME` | `id` | `[a-z0-9-]{1,32}`. Exactly one. |
| `GRID` | `widthWu heightWu cellWu` | Exactly one. `cellWu` must be a power of two. `widthWu` and `heightWu` must be positive multiples of `cellWu`. `widthWu / cellWu` and `heightWu / cellWu` must each be at most 512. |

Body records, each of which may appear many times.

| Kind | Fields | Validation |
| --- | --- | --- |
| `WALL` | `x1 y1 x2 y2 material` | Endpoints differ. Both inside `[0, width] × [0, height]`. `material` in `0..3`: 0 glass, 1 solid, 2 partition, 3 breachable. Canonical endpoint order is lexicographic ascending. |
| `DOOR` | `x1 y1 x2 y2 hinge state` | Endpoints differ and are axis-aligned. Inside bounds. `hinge` in `0..1`. `state` in `0..1`, 0 closed and 1 open. Must not be collinear-overlapping with any `WALL`. |
| `COVER` | `minX minY maxX maxY arcCentreBam arcHalfBam height` | `minX < maxX`, `minY < maxY`, inside bounds. `arcCentreBam` in `0..65535`. `arcHalfBam` in `1..32768`, where `32768` means 360 degrees. `height` in `0..2`. |
| `SPAWN` | `faction x y facingBam` | `faction` in `0..1`. Inside bounds. The cell must be passable after body-radius inflation. |
| `OBJECTIVE` | `index x y radiusWu` | `index` dense from 0. Inside bounds. `radiusWu` positive. |
| `END` | none | Exactly one, and it is the last line. |

Cross-record validation, all of it a hard error:

- At least one `SPAWN` for faction 0 and at least one for faction 1.
- No two `SPAWN` records closer than one body diameter.
- The map must be fully enclosed: a flood fill from outside the bounding box must
  not reach any spawn cell.
- Every spawn of faction 0 must be able to reach every objective, checked by the
  same A\* the game uses, with all doors treated as passable.
- No duplicate record of any kind.

### Canonical form and the content hash

Canonicalisation sorts body records by `(kindOrdinal, field1, field2, …)`
ascending, with `WALL = 1`, `DOOR = 2`, `COVER = 3`, `SPAWN = 4`,
`OBJECTIVE = 5`. Wall and door endpoints are first normalised to lexicographic
ascending order, so `WALL 640 0 0 0 1` and `WALL 0 0 640 0 1` are the same record
and the second occurrence is the duplicate error.

`MapContentHash` is FNV-1a over the **canonicalised record stream**, not over the
file text: the kind ordinal as one byte followed by each integer field as four
big-endian bytes. Whitespace, comments, and line order therefore cannot move the
hash, and a single coordinate change always does.

`MapContentHash` folds into the mission content hash alongside
`SandataRuleset.ContentHash`. **This is load-bearing.** Editing one wall
coordinate moves the state hash, which forces new golden expectations exactly as
`CLAUDE.md` section 5 requires. Without it, a map edit silently invalidates every
recorded replay with no signal at all.

### What is derived at load and never stored

Rasterise walls and closed doors into the nav grid; inflate by body radius so the
grid encodes "a body fits here" rather than "a point fits here"; build the
clearance field by integer chamfer; tag door cells as high-cost-but-passable to
the planner and impassable to the mover until opened; and bucket wall segments
into the same uniform grid for the line-of-sight narrow phase. None of it is
stored in the file and none of it is snapshotted.

### Worked example: `angle-house.hkmap`

The angle-dense test map. It is 640 by 720 world units — a 40 m by 45 m plot
holding a 40 m by 40 m building with a 5 m staging strip to the south — on a
4 wu nav cell, so 160 by 180 cells.

It satisfies every property the research consolidation asks of an angle-dense
map: many small apertures with overlapping interior fields of fire; hard corners
with return angles; cover objects whose protected arcs face away from the natural
entry; a non-obvious wall-breach face that flanks the prepared angle; and a
closet that creates a rear angle requiring a clear.

The file is shown **already in canonical order**, so it is simultaneously the
example and the expected canonicalisation output.

```
HKMAP 1
NAME angle-house
GRID 640 720 4
WALL 0 0 0 720 1
WALL 0 0 640 0 1
WALL 0 640 300 640 1
WALL 0 720 640 720 1
WALL 60 260 200 340 2
WALL 60 460 60 580 1
WALL 60 460 100 460 1
WALL 60 580 180 580 1
WALL 120 120 320 220 2
WALL 140 460 180 460 1
WALL 160 400 340 520 2
WALL 180 460 180 580 1
WALL 320 220 520 160 2
WALL 340 640 640 640 1
WALL 380 380 560 300 2
WALL 420 60 420 120 1
WALL 420 60 600 60 1
WALL 420 160 420 200 1
WALL 420 200 600 200 3
WALL 600 60 600 200 1
WALL 640 0 640 720 1
DOOR 100 460 140 460 0 0
DOOR 300 640 340 640 0 0
DOOR 420 120 420 160 1 1
COVER 200 200 260 240 49152 8192 1
COVER 260 100 340 140 16384 8192 2
COVER 440 440 520 500 49152 8192 1
COVER 500 540 560 600 0 32768 1
SPAWN 0 296 690 49152
SPAWN 0 320 690 49152
SPAWN 1 120 520 49152
SPAWN 1 500 120 16384
OBJECTIVE 0 500 120 48
OBJECTIVE 1 120 520 48
END
```

**Angle convention.** `Bam16` 0 is `+X` (east) and the value increases toward
`+Y`, which is screen-down, matching `FacingRules`' existing convention where
sector 0 is `(1024, 0)` and sector 4, `South`, is `(0, 1024)`. So 16384 is south,
32768 is west, and 49152 is north.

**The five non-90-degree walls**, which are the reason the map exists:

| Wall | Run | Angle from horizontal |
| --- | --- | --- |
| `WALL 60 260 200 340 2` | `+140, +80` | 29.74° |
| `WALL 120 120 320 220 2` | `+200, +100` | 26.57° |
| `WALL 160 400 340 520 2` | `+180, +120` | 33.69° |
| `WALL 320 220 520 160 2` | `+200, −60` | −16.70° |
| `WALL 380 380 560 300 2` | `+180, −80` | −23.96° |

A grid-only path across any of these staircases visibly; the funnel string-pull
is what makes an operator walk the 26.57-degree line. That is the property this
map exists to regression-test.

**Doors in both states.** `DOOR 300 640 340 640 0 0` is the closed entry in the
building's south wall — the natural entry, and the doorway that forces the
weapon-lowered rule on the way through. `DOOR 100 460 140 460 0 0` is the closed
closet door in the south-west, which creates the rear angle that has to be
cleared. `DOOR 420 120 420 160 1 1` is the **open** door into the north-east
objective room, held by the defender at `SPAWN 1 500 120 16384`.

**Cover arcs that face away from the natural entry.** Attackers enter from the
south at `Bam` 49152 heading north. `COVER 200 200 260 240 49152 8192 1` and
`COVER 440 440 520 500 49152 8192 1` both protect against fire arriving from the
**north** (`arcCentreBam` 49152, half-width 8192, which is 45 degrees). An
attacker pushing north is therefore *not* protected by either of them — their
arcs face away from the entry — while anyone already inside and facing the entry
is. `COVER 260 100 340 140 16384 8192 2` is the mirror case: a tall object with a
southern arc, real cover for the defender shooting at the entrants.
`COVER 500 540 560 600 0 32768 1` is the one 360-degree object, a concrete
planter, so the flank-and-rear rule has something that does not obey it.

**The breach face.** `WALL 420 200 600 200 3` is material 3, breachable. It is
the north-east room's south wall. The room's natural entry is the open door at
`(420, 120)–(420, 160)`, which the defender is holding; the breachable south face
flanks that prepared angle. It is not on the building's outer shell, so breaching
it leads somewhere rather than outside the map, and it is not visible from the
natural approach.

**The content hash is recorded, not asserted here.** `MapContentHash` for this
file is whatever FNV-1a over the canonicalised record stream produces, and the
implementer records the measured value as the golden expectation when the loader
first runs. This design does not state a digest it has not computed.

---

## 13. Test strategy

### What is unit tested

Everything pure, which after the geometry decisions in this document is most of
the simulation. In `Sandata.Core.Tests`:

- Every function in the section 6 math table.
- The map tokenizer's rejection of every malformed input class, one test per
  named rule.
- Canonicalisation: an input file in scrambled line order producing the byte
  stream of the canonical example, and a duplicate record producing a load error.
- Nav grid rasterisation and body-radius inflation against a hand-computed 8 by 8
  fixture asserted cell by cell.
- The clearance field against the same fixture.
- The A\* comparator's totality: a property test over random `(f, h, index)`
  triples asserting antisymmetry, transitivity, and totality.
- Fire-mode-by-range-band selection across every band boundary, both sides.
- Accuracy interpolation at 0, `MaxEffectiveWu`, and beyond it.
- The timing-chain state machine, including every zero-tick transition.
- The cyclic accumulator producing the exact 4, 4, 4, 3 pattern at 800 rpm.
- The weapon-lowered rule at the exact `LoweredWallDistanceWu` boundary, on both
  sides, and its pistol exemption.
- Cover arc containment at the arc boundary, and the flank and rear bypass.
- Union-find grouping: a chain, a ring, a split on death, and the derived leader
  after the current leader dies.
- Arclength slot targeting on a corner, asserting that the follower cuts the same
  corner rather than crossing the wall.
- The single-file collapse at the clearance threshold, both directions.

In `Sandata.Client.Tests`, following the pure-helper pattern that keeps
`GraphicsDevice` and `SpriteBatch` out of tests:

- `OperatorGeometry.Create` layer bounds at every detail tier, including the
  empty-layer convention.
- Persistent aim: the weapon angle tracking a changing `Bam16` without springing
  back.
- Theme role count, role name set, and every contrast pair.
- Faction colour constancy across all themes.
- Multi-select state transitions and the marquee's inclusion predicate.
- Sound slot lookup: every `(family, key, mode, environment)` tuple in the
  catalog resolves, `VariantCount` is within 1 to 99, and the mode-to-slot
  mapping matches the simulation's band rule.

### What needs golden vectors

Pinned literal input-output pairs, in the style of the existing SplitMix64
vectors, for: `FixedPoint` multiply, divide, and square root; every `Bam16`
conversion; the whole 257-entry sine table; the sixteen CORDIC arctangent
constants and eight exact-axis results; the octile heuristic; the chamfer
fixture; `GridRay` traversal on an axis-parallel, an exact-diagonal, and an
18.4-degree line; and the funnel's output on the three corridor shapes.

Golden vectors are **not** derived from the implementation. Where a value is
computable by hand it is computed by hand and the arithmetic is shown in the test
comment. Where it is not — the sine table, the CORDIC constants — the value is
pinned from its published mathematical definition, and a separate test asserts
the table's internal consistency (monotonicity in the first quadrant, the exact
endpoints 0 and 65536) so a transcription error cannot hide behind a
self-confirming pin.

### What needs a determinism replay test

- **Same-seed repeat.** The seed-1 mission run twice in one process and once in a
  fresh process must produce identical state hash, event hash, winner, and
  ordered event stream.
- **Save and resume equivalence.** Snapshot mid-mission, resume, and continue —
  identical hashes to the uninterrupted run. This is the only test that proves
  the "paths are derived and recomputed from request records" rule in section 4.
- **Cold-cache equivalence.** Load the map, discard every derived structure,
  rebuild, and run — identical hashes. This is what proves the derived list is
  actually derived.
- **Logging cannot change the simulation.** The Sandata workload run with logging
  off and at `trc` must produce identical hashes, outcome, and event stream. This
  mirrors the rule `CLAUDE.md` already enforces for Hukbo.
- **Golden replay.** A pinned seed-1 mission at a fixed tick count with its state
  hash and event hash recorded as expected constants, so any hash movement is a
  test failure naming the first mismatch tick.

### What can only be checked by a human at a desktop

These rows go into `docs/development/testing.md` as `PENDING` and stay `PENDING`
until a person runs them. Compilation, unit tests, and a window-opening probe do
not let anyone flip one to `PASS`, and no agent may flip one at all.

- The window opens, the map draws, and the operators are legible at every zoom.
- The funnel path visibly follows the 26.57-degree wall rather than a staircase.
- A squad visibly collapses to single file at the entry door and re-expands
  inside.
- The weapon-lowered rule is visible: an operator crossing a doorway lowers and
  re-raises, and a pistol operator does not.
- Automatic fire sounds continuous rather than machine-gun-stuttered, and no
  audio drops out under sustained fire from the maximum operator count.
- The fire cone reads at every detail tier and does not fade with zoom.
- Friendly, hostile, and unknown contacts are distinguishable at a glance in
  every theme, and distinguishable by shape with colour removed.
- The operator inspector explains a held position: reason code, path state, and
  chain phase.

### New tests required by the build gates

The research consolidation verified all three of these directly, and each is a
named task.

**The console ban already auto-covers Sandata.** `SourceHygieneTests` scans
`Path.Combine(root, "src")` recursively
(`tests/Hukbo.Client.Tests/SourceHygieneTests.cs:35`), so it fails the moment any
new file outside the named entry points contains `Console.`. This is good news
with one required edit: `src/Sandata.Client/Program.cs` and
`src/Sandata.Headless/Program.cs` must be added to the `ConsoleOwners` array, or
the gate goes red the moment the headless runner prints anything.

**The Diagnostics boundary test does not cover Sandata and must be widened.** It
is hardcoded to `typeof(Scenario).Assembly`
(`tests/Hukbo.Core.Tests/DiagnosticLoggingBoundaryTests.cs:24`) and its positive
control is hardcoded to `typeof(HeadlessRunner)` (`:40`). Without a parallel
fact, `Sandata.Core` could silently gain a `Hukbo.Diagnostics` reference with
nothing to catch it. Two new facts are required: `Sandata.Core`'s assembly must
not reference `Hukbo.Diagnostics`, and `Sandata.Headless`'s must, as the positive
control that proves the assertion can fail. A third fact extends the source scan
to `src/Sandata.Core` for the string `Hukbo.Diagnostics`, so the failure names a
line rather than only an assembly. `Hukbo.Shared.Core` gets the same negative
fact, since both games depend on it.

**The pinned package list is asserted exactly.** `PinnedPackageNames`
(`SourceHygieneTests.cs:165`) is compared for exact equality (`:309-310`). Sandata
adds no package, so this test needs no edit — and that is the point: it is the
gate that makes "no new dependency" enforceable rather than aspirational.

**One further gate test this design adds:** a fact asserting that
`src/Sandata.Core` contains no occurrence of `float`, `double`, `System.Random`,
`Math.Sqrt`, `Math.Atan2`, `Dictionary<`, `HashSet<`, or `PriorityQueue<` outside
a doc comment. Every one of those is banned by section 4, and a text scan is the
cheapest enforcement that actually holds. Doc-comment mentions are excluded
exactly the way `PresentationVariationCodeDoesNotUseSystemRandom` already
excludes them, so the scan cannot fail on the sentence documenting the rule.

---

## 14. Scripts

### The constraint

Every script hardcodes Hukbo. Verified: `scripts/benchmark.ps1:42` names
`src/Hukbo.Headless`; `scripts/run.ps1:21` and `scripts/package.ps1:12` name
`src/Hukbo.Client`; `scripts/test.ps1:15-16` names both test projects;
`scripts/build.ps1:18,21`, `scripts/format.ps1:13-14`, and
`scripts/bootstrap.ps1:44` name `Hukbo.slnx`; `scripts/doctor.ps1:82-85` carries
a fixed lock-file list. Only `scripts/_common.ps1` and `scripts/verify.ps1` are
genuinely game-agnostic.

### The decision

**Each game-specific script gains a `-Game` parameter with
`[ValidateSet('Hukbo', 'Sandata')]` and a default of `'Hukbo'`. The project paths
move out of the script bodies into one shared table.**

A new `scripts/_gametargets.ps1` holds the table and nothing else:

```powershell
function Get-GameTarget {
    param([ValidateSet('Hukbo', 'Sandata')][string] $Game = 'Hukbo')

    switch ($Game) {
        'Hukbo'   { @{ Client = 'src/Hukbo.Client/Hukbo.Client.csproj'
                       Headless = 'src/Hukbo.Headless/Hukbo.Headless.csproj'
                       Tests = @('tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj',
                                 'tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj')
                       LogPrefix = 'hukbo' } }
        'Sandata' { @{ Client = 'src/Sandata.Client/Sandata.Client.csproj'
                       Headless = 'src/Sandata.Headless/Sandata.Headless.csproj'
                       Tests = @('tests/Sandata.Core.Tests/Sandata.Core.Tests.csproj',
                                 'tests/Sandata.Client.Tests/Sandata.Client.Tests.csproj')
                       LogPrefix = 'sandata' } }
    }
}
```

Scripts that gain `-Game`: `run.ps1`, `test.ps1`, `benchmark.ps1`, `package.ps1`,
`doctor.ps1`.

Scripts that do **not** gain `-Game`, because they operate on the solution and
are already game-agnostic once the new projects are in `Hukbo.slnx`:
`build.ps1`, `format.ps1`, `bootstrap.ps1`. `doctor.ps1`'s lock-file list becomes
the union of both games' lock files rather than a `-Game` switch, because a
doctor that only checks half the repository is worse than useless.

`verify.ps1` gains `-Game` and passes it through to `test.ps1` and
`benchmark.ps1`.

### The default must be byte-identical, and here is how that is proven

**`./scripts/verify.ps1` with no arguments must run exactly the command sequence
it runs today, against exactly the projects it runs against today, and produce
the same output.** This is not a goal; it is the acceptance criterion for the
whole scripts task.

Three layers of proof, in increasing strength:

1. **A text assertion in `Hukbo.Client.Tests`.** A fact reads
   `scripts/_gametargets.ps1` as text and asserts that the `Hukbo` branch
   contains the exact literal paths the current scripts hardcode:
   `src/Hukbo.Client/Hukbo.Client.csproj`,
   `src/Hukbo.Headless/Hukbo.Headless.csproj`,
   `tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj`, and
   `tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj`. Cheap, and it catches a
   typo in the table.
2. **A fact asserting no script body still hardcodes a project path.** The
   inverse check, so the table cannot be added and then bypassed.
3. **The gate itself.** `./scripts/verify.ps1` runs once after integration and
   its real output is the evidence. A 200-agent, 10,000-tick, seed-1 Hukbo
   determinism workload producing the recorded baseline hashes is the only proof
   that matters, and it is not delegated.

### When `verify.ps1` starts running both games

Not in v0.1's early waves. The default gate keeps running the Hukbo workload
alone until Sandata's headless runner exists and has a recorded seed-1 baseline.
Only then does `verify.ps1` gain a second benchmark invocation, and that change
is its own task with its own gate run, so a red Sandata workload can never be
confused with a red Hukbo one.

`-Game Sandata` exists from the moment `Sandata.Headless` does, so a person can
run the Sandata gate deliberately long before it becomes part of the default.

### The debug log

`Sandata.Client` and `Sandata.Headless` write through `Hukbo.Diagnostics` exactly
as their Hukbo counterparts do, to `artifacts/logs/sandata-<utc>-<pid>.jsonl`.
Same six leading fields in the same order, same five levels, same channel
filtering, same `HUKBO_LOG_LEVEL` / `HUKBO_LOG_CHANNELS` / `HUKBO_LOG_DIR`
environment variables — the variable names are not forked, because they configure
the logger and the logger is shared. New `ev` identifiers are `const` members on
`LogEvents` under a `sandata.` prefix, and `Sandata.Core` never references
`Hukbo.Diagnostics`.

### `sfx.ps1`

`sfx.ps1` gains a batch mode and per-family trim thresholds so a 484-slot run is
not 484 process launches, and it gains nothing else. It remains the only script
that talks to a network service, it remains an authoring tool outside every
pipeline, and it still reads `ELEVENLABS_API_KEY` from the environment or the
untracked `.env` file. The key never belongs in a tracked file, in output, or in
a commit message. The game, the build, the tests, and the gate remain fully
offline.

The manifest generator is a **separate** script, `scripts/sfx-manifest.ps1`, with
no network code in it at all, so "produce the manifest" and "spend money" cannot
be confused for one another by a tired person or an over-eager agent.

---

## 15. Open questions

Ten, of which the first five are carried directly from the research
consolidation's own list.

1. **Autonomous bots versus player orders. ANSWERED 2026-08-07: both.**
   Sections 2 and 16. The user was given three options — autonomous bots, a
   literal Door Kickers 2 with hand-drawn paths only, or both — and chose both.
   The autonomous side is unchanged; the order layer is promoted from scaffold
   to a first-class subsystem, specified in section 16. This was the largest
   single assumption in the document and it is now a decision.
2. **Two-dimensional weapon geometry versus 3D meshes.** Section 2. The plan
   assumes 2D throughout and costs nothing for meshes.
3. **The product name `Sandata`.** Trivial to change before the first commit,
   expensive after. It appears in seven project names, every namespace, and every
   file path in the plan document.
4. **Real weapon names versus generic aliases in shipped display strings.**
   Section 9. Glock, Heckler & Koch, Beretta, SIG Sauer, FN Herstal, Steyr, and
   IWI are all rated high risk, with Heckler & Koch and Glock the most aggressive
   enforcers, and Glock and Steyr have separately asserted trade dress claims, so
   a silhouette carries risk independent of the name. Numeric designations issued
   by a government are materially safer: M4, Mk 18, L85, QBZ-191, M7, MP-443. The
   design puts the choice behind one field; the choice itself is the user's.
5. **Authorisation to spend on the audio generation run.** Section 10. 22 USD
   best case, 99 USD likely. Nothing runs until a dry-run manifest is reviewed.
6. **The 50 Hz tick rate and the 16-world-units-per-metre scale.** Section 4.
   Both are chosen here with reasons, and both are pinned into the content hash
   the moment the first golden vector exists. Changing either afterwards is a new
   preset version. Worth a look before wave 2 starts, not after.
7. **Whether `verify.ps1` should eventually run both games by default.** Section
   14. Doing so roughly doubles gate wall-clock time, on a repository whose gate
   is already the slowest thing in the workflow and is run before every
   integration.
8. **Whether the shared assemblies keep the `Hukbo.` prefix.** Section 3 uses
   `Hukbo.Shared.Core` because `CLAUDE.md` section 2 mandates the `Hukbo.*`
   prefix for projects and namespaces. But an engine spine shared by two games is
   neither game, and if a third name is wanted, before the first commit is the
   only cheap moment. Note that the chosen root-namespace pin means the *file*
   namespaces stay `Hukbo.Core.*` regardless, so this question is about the
   assembly name only.
9. **Map authoring beyond hand-written `.hkmap` files.** v0.1 has no editor and
   does not want one. The question is whether the second and third maps are
   expected to be hand-written too, because if not, the editor is the next
   milestone and the format should be reviewed with that in mind now.
10. **Whether Sandata stays in this repository long-term.** The extraction shape
    in section 3 keeps a split cheap, and the `.gitignore`, `.gitattributes`,
    `Directory.Build.props`, and `global.json` are all shared today. A 27 to
    46 MB audio library that every Hukbo clone pays for is the first real cost of
    staying, and it lands the moment question 5 is answered yes.

---

## 16. The order layer

Added 2026-08-07, after the user answered question 1 with "both". This section
is the specification the promoted order layer is built against. Everything it
adds is additive: no rule stated earlier in this document is withdrawn, and the
autonomous mechanisms in sections 7 and 8 are untouched.

### The two path sources, and the rule that keeps them from fighting

Every operator's movement, on every tick, comes from exactly one of two sources.
Which one is not a preference and not a heuristic. It is derived from a single
authoritative field:

> An operator whose `OrderAssignment` is present follows the authored polyline
> that assignment names. An operator with no assignment follows its squad slot
> target along its group's autonomous polyline, exactly as section 8 describes.
> There is no third case and no blend of the two.

An assignment is present only because an order created it, and it is cleared
only by one of four written conditions:

1. The operator reached the polyline's final node.
2. A `Cancel` order addressed to that operator was applied.
3. The operator died.
4. The polyline became untraversable — a door closed across it, or a nav rebake
   blocked a cell the polyline crosses.

Case 4 is the one that needs a stated behaviour rather than an implied one. The
assignment is cleared with an inspectable reason code and autonomy resumes on
the same tick. **The polyline is never silently repaired, re-routed, or
re-smoothed.** A path a person drew is that person's decision, and a simulation
that quietly redraws it is lying to the player about what it was told to do.

Squad grouping itself is unaffected. Section 8's union-find derivation still runs
every tick over the same proximity pair list, and a group id still exists for
every operator whether or not it is under orders. The group id remains the truth
for cohesion display and for the roster strip. What an assignment suppresses is
narrower and exact: an operator under orders is excluded from slot targeting for
that tick, and the path service does not enqueue a request on its behalf.

### Order records and the queue

An order is an immutable record carrying an identity, a schedule, an addressee
set, a kind, and that kind's payload:

- `OrderId` — dense, ascending, assigned at submission.
- `OrderSequence` — the submission counter, unique and never reused.
- `TargetTick` — the tick at which the order takes effect.
- `FactionId` — orders address one faction's operators only.
- `Addressees` — entity ids in ascending order, so the set has one written form.
- `Kind` and that kind's payload.

The order kinds in v0.1 are `MoveAlongPath`, `Hold`, `Breach`, `Sync`,
`GoCodeRelease`, and `Cancel`. Stage 1 applies the queue in
`(TargetTick, OrderSequence)` order, which is the ordering the tick pipeline
table already pinned before the order layer existed; it did not need changing.

The queue is authoritative state. It is snapshotted and it folds into the state
hash, in ascending `(TargetTick, OrderSequence)`, after every field the hasher
already covers. Appending rather than interleaving keeps the existing field
order intact, which matters because it means the addition does not disturb any
hash recorded before it.

The node-count cap on an authored polyline is a `const` on the order type, not a
field on `SandataRuleset`. It is a structural limit that exists so a malformed
input cannot allocate without bound, not a tuning value a designer would ever
sweep, and putting it on the ruleset would move `ContentHash` for a constant
that never varies.

### An authored polyline is authoritative, not derived

Section 4 states that a published path polyline is derived, excluded from the
snapshot, and recomputed from its stored request on resume. That rule stands for
every path a search produced. The companion rule for the other source is:

> An authored polyline is player input. It is stored verbatim in the snapshot
> and folds into the state hash. It is never recomputed, never re-smoothed, and
> never replaced by a search result. On resume it is restored exactly as it was
> drawn.

The asymmetry is deliberate and it is the whole reason both rules have to be
written down. A derived path recomputed on resume is a correctness property; an
authored path recomputed on resume is a defect that would let the nav bake state
at load time rewrite a decision the player made an hour earlier.

The save-resume equivalence test therefore has to cover both kinds, and a mission
with only autonomous paths does not exercise this rule at all.

### Validation happens at submission, and rejection is observable

An order is validated when it is submitted, not when it is applied, so the player
learns immediately rather than several ticks later. A `MoveAlongPath` order is
rejected when any of the following holds:

- A node lies outside the map bounds.
- A node lies in a cell that is `Blocked` in the current nav bake.
- A segment between consecutive nodes crosses a wall segment, tested with
  `ExactPredicates.ClassifySegments` against the wall bucket index. This is an
  exact integer test with no epsilon, the same predicate line of sight uses.
- The node count exceeds the cap, or is below two.

A rejected order emits an authoritative event carrying the order id and a reason
code. It is not silently dropped. Section 10's question 8 asks whether a
spectator can discover an effect without reading source code, and an order that
vanishes with no explanation fails that test outright.

Door cells are deliberately not a rejection condition. A path through a door is
the ordinary case in this game, and the mover's own door handling already governs
whether it can pass yet.

### Sync sets and go-codes

Both of Door Kickers 2's grouping primitives are expressible as orders, which is
why they cost so little under this answer.

**Sync** pace-matches a set of operators. Each member that reaches its polyline's
final node holds there. When every living member of the set is holding, all of
them release on the same tick. The evaluation runs in stage 8 against the frozen
tick-start view, so it is order-independent by construction, and the set is keyed
by its lowest entity id so that two sets releasing on the same tick have a total
order between them.

**A go-code** assigns a letter to waypoints across several operators, and
releasing that letter is itself an order — a `GoCodeRelease` with its own
`TargetTick` and `OrderSequence`. A keypress therefore enters the same queue as
everything else and gets the same determinism guarantee for free. No separate
input path reaches the simulation.

### Undo, multi-select, and drag capture stay presentation-only

The undo stack, the multi-select state, and the drag capture layer edit orders
**before** submission. Nothing about them crosses into `Sandata.Core`: they are
not snapshotted, they do not fold into any hash, and undoing a drawn node that
was never submitted is invisible to the simulation. The only thing that crosses
the boundary is a submitted order.

A test asserts that no type in `Sandata.Core` is reachable from the undo stack,
for the same reason a test asserts `Hukbo.Core` never references
`Hukbo.Diagnostics`: the boundary is worth more as a checked fact than as an
intention.

### What this does to the determinism contract

- **The replay contract gains the order stream.** The guarantee becomes: same
  seed, same build, same ordered order stream, identical state hash, event hash,
  outcome, and event stream. An order stream is part of a replay in exactly the
  way a seed already is.
- **The golden replay needs two baselines**, not one: a mission with an empty
  order stream, which is the pure autonomous case, and a mission with a recorded
  non-empty one. A single empty-stream baseline would prove nothing about the
  subsystem this section adds.
- **`MissionState`, `MissionSnapshot`, and `SandataStateHasher` gain the queue
  and the per-operator assignment.** No golden mission hash exists yet, so this
  costs no re-pinning provided it lands before the golden replay task records
  one.
- **`SandataRuleset.ContentHash` does not move.** The order layer introduces no
  tuning value, so it adds no ruleset field.

### What a spectator sees

Section 10's question 8, answered explicitly. A drawn path renders as a polyline
overlay with waypoint markers, at every detail tier, alongside the tactical
overlays. The operator inspector gains three rows: the active order id, the node
index currently being walked, and the reason code that cleared the last
assignment. An operator that stopped following a drawn path can therefore be
asked why it stopped, and it answers.
