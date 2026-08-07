# The battle-termination standoff: root cause and candidate fixes

Research note, 2026-08-07. Written for the ranged units package, which has just
taken battle termination into scope. Read-only investigation. Nothing here
authorizes implementation; a design document and a plan document have to come
first, exactly as `CLAUDE.md` section 6 requires.

**Discovery-tool note.** `CLAUDE.md` section 8 requires the `tokensave` MCP
tools for code discovery, and forbids Explore agents. Those tools were not
exposed to the process that wrote this note — a tool search for
`tokensave|search_graph|get_code_snippet|query_graph|search_code` returned no
matches — so discovery fell back to `Read` and `Grep`, which the same rule
permits for reading a file before quoting it. No Explore agent was spawned.
Every line reference below comes from a direct read of the file named.

**Archive-provenance note.** The V7 measurement corpus lives under
`docs/archives/2026-08-06/movement/`. `CLAUDE.md` section 6 makes that directory
deprecated by definition. This note reads it only to recover **measured
numbers** and the **history of what was tried**, and labels every such figure as
historical. No archived plan is cited as authority for any decision below.

## Sections

1. [The termination criterion, as the standards define it](#1-the-termination-criterion-as-the-standards-define-it)
2. [The measured baseline](#2-the-measured-baseline)
3. [What V7 did, and why it failed](#3-what-v7-did-and-why-it-failed)
4. [Root cause](#4-root-cause)
5. [The defensive resolution contract's contribution](#5-the-defensive-resolution-contracts-contribution)
6. [Candidate fixes](#6-candidate-fixes)
7. [Interaction with ranged units](#7-interaction-with-ranged-units)
8. [The measurement harness](#8-the-measurement-harness)

---

## 1. The termination criterion, as the standards define it

`SIMULATION-GAME-STANDARDS.md` states the criterion in section 14, under the
heading `### The termination criterion` at **line 863**. The body is lines 865
through 868, quoted here exactly:

> At least 19 of 20 seeds must reach a decisive outcome before the 5,000-tick
> cap, with a median decisive tick at or below 5,000. Preset V2's shipped tables
> satisfy both the share band and the termination criterion; the recorded
> figures are in
> [docs/development/testing.md](docs/development/testing.md).

That is the only place in the standards that states a numeric termination bar,
and it is stated as a property of the **defensive resolution contract** — it
sits inside section 14, immediately after the acceptance band on
`CombatMetrics.DefenceAttributableShare` at lines 855 through 861, and it is
written against preset V2's clash tables rather than against any movement
preset. It is worth being explicit about that framing, because it is the reason
the standards contain no movement-side termination bar at all: when the
criterion was written, movement was not a candidate cause.

### What counts as a decided battle

"Decisive outcome" is not defined in prose in the standards; it is defined in
code. `BattleOutcome` (`src/Hukbo.Core/Simulation/BattleOutcome.cs:3-9`) has
four members — `Ongoing = 0`, `Faction0Victory = 1`, `Faction1Victory = 2`, and
`Draw = 3`. `ResolveOutcome` (`src/Hukbo.Core/Simulation/BattleSimulation.cs:3981`)
selects between them on a tuple of "faction 0 has a living agent" and "faction 1
has a living agent":

- `(false, false)` — mutual annihilation — is `Draw`
  (`BattleSimulation.cs:4005`).
- `(true, false)` is `Faction0Victory` (`BattleSimulation.cs:4006`).
- `(false, true)` is `Faction1Victory` (`BattleSimulation.cs:4007`).
- Both sides still alive **and** `Tick >= Scenario.TickLimit` is `Draw`
  (`BattleSimulation.cs:4008`).

So a decided battle is one in which exactly one faction still has a living
agent. There is no points victory, no attrition threshold, no timeout winner,
and no partial credit for a faction that is ahead on survivors. **The only way a
battle ends before the tick limit is for one entire faction to be killed to the
last warrior.** This matters for every candidate fix in section 6: any change
that reduces the number of blows required to empty a roster shortens a battle,
and any change that does not is decorative.

Two further standards clauses bind the shape of a fix:

- `SIMULATION-GAME-STANDARDS.md:562` — "No separate anti-stall or fairness
  escape rule is added, because being blocked does not remove an agent from
  combat: contact happens at eight world units while attack reach is twelve, so
  a blocked agent is still attacking. `TickLimit` remains the terminal
  backstop." The tick limit is deliberately a backstop, not a decider.
- `SIMULATION-GAME-STANDARDS.md:485-490` — "**`AgentIntent.Attacking` means the
  agent has arrived.**" Intent selection describes arrival; attack gathering
  describes striking; the two stages do not overlap.

### The bar the V7 workstream actually measured against

The V7 workstream did not use the 5,000-tick standards bar. It used a
6,000-tick bar drawn from its own design section 2.1, recorded in
`docs/archives/2026-08-06/movement/AGENT-BACKLOG.md:88-90` (historical): "Zero
of ten cells reached a decisive outcome within 6,000 ticks; all ten still draw
at the 10,000-tick limit." The discrepancy does not change any conclusion below,
because the measured result is a `Draw` at 10,000 ticks in every cell, which
fails both bars by a wide margin. It is recorded here so that a later reader who
finds "6,000" in an archived document knows it is not the standards figure.

`Scenario.CreateDefault` sets `TickLimit: 10_000`
(`src/Hukbo.Core/Simulation/Scenario.cs:206`), and the canonical gate's
determinism workload is 200 agents / 10,000 ticks / seed 1, so 10,000 is the
number every measured run in this note was capped at.

---

## 2. The measured baseline

### 2.1 The first correction to the brief

The task that commissioned this note says "Hukbo battles do not terminate. Every
battle under the candidate movement preset runs to a 10000-tick standoff draw."
The second sentence is exactly right and the first is not, and the distinction
is the single most useful fact in this document.

**The shipped default movement preset terminates. Every cell. The standoff is a
property of the candidate preset only.**

`Scenario`'s shipped default is `MovementPresetId.PersistentContingentsV4`,
confirmed by `MovementPresetId.cs:88` (the V4 member) and by the V6 member's own
remark at `src/Hukbo.Core/Movement/MovementPresetId.cs:111-113` — "It is
reachable only through explicit selection — the shipped default stays
`PersistentContingentsV4`". The V7 member repeats it at
`MovementPresetId.cs:138-140`.

### 2.2 Recorded terminal ticks, per seed, both presets

All twenty cells below are historical measurements recorded in
`docs/archives/2026-08-06/movement/2026-07-31-movement-v7-baseline.md`, section
9.3, lines 543 through 562. Each row of that block carries, in order, the cell
name, `measuredTicks`, `outcome`, both survivor counts, timings, allocation, the
determinism flag, and both hashes. The figures were produced by the headless
runner at 200 and 500 agents on seeds 1, 2, 3, 5, and 8, with a discarded warm
run per cell.

| Movement preset | Agents | Seed | Terminal tick | Outcome | Source |
| --- | --- | --- | --- | --- | --- |
| `PersistentContingentsV4` | 200 | 1 | **1,279** | `Faction0Victory` | `2026-07-31-movement-v7-baseline.md:553` |
| `PersistentContingentsV4` | 200 | 2 | **1,439** | `Faction0Victory` | `:554` |
| `PersistentContingentsV4` | 200 | 3 | **2,037** | `Faction1Victory` | `:555` |
| `PersistentContingentsV4` | 200 | 5 | **2,230** | `Faction1Victory` | `:556` |
| `PersistentContingentsV4` | 200 | 8 | **2,284** | `Faction0Victory` | `:557` |
| `PersistentContingentsV4` | 500 | 1 | **2,934** | `Faction0Victory` | `:558` |
| `PersistentContingentsV4` | 500 | 2 | **2,551** | `Faction0Victory` | `:559` |
| `PersistentContingentsV4` | 500 | 3 | **4,085** | `Faction0Victory` | `:560` |
| `PersistentContingentsV4` | 500 | 5 | **2,568** | `Faction0Victory` | `:561` |
| `PersistentContingentsV4` | 500 | 8 | **4,405** | `Faction1Victory` | `:562` |
| `EquipmentRelativeFootworkV6` | 200 | 1 | **10,000** | `Draw` (78 v 73) | `:543` |
| `EquipmentRelativeFootworkV6` | 200 | 2 | **10,000** | `Draw` (66 v 68) | `:544` |
| `EquipmentRelativeFootworkV6` | 200 | 3 | **10,000** | `Draw` (50 v 46) | `:545` |
| `EquipmentRelativeFootworkV6` | 200 | 5 | **10,000** | `Draw` (76 v 70) | `:546` |
| `EquipmentRelativeFootworkV6` | 200 | 8 | **10,000** | `Draw` (40 v 49) | `:547` |
| `EquipmentRelativeFootworkV6` | 500 | 1 | **10,000** | `Draw` (137 v 142) | `:548` |
| `EquipmentRelativeFootworkV6` | 500 | 2 | **10,000** | `Draw` (156 v 153) | `:549` |
| `EquipmentRelativeFootworkV6` | 500 | 3 | **10,000** | `Draw` (120 v 124) | `:550` |
| `EquipmentRelativeFootworkV6` | 500 | 5 | **10,000** | `Draw` (128 v 125) | `:551` |
| `EquipmentRelativeFootworkV6` | 500 | 8 | **10,000** | `Draw` (106 v 111) | `:552` |

Two verdicts against the section 1 criterion, applied to these five-seed groups
rather than to the twenty seeds the criterion names:

- **`PersistentContingentsV4` passes.** Ten of ten cells decisive; the longest
  is 4,405 ticks, inside the 5,000-tick cap; the 200-agent median is 2,037 and
  the 500-agent median 2,934, both at or below 5,000.
- **`EquipmentRelativeFootworkV6` fails absolutely.** Zero of ten cells decisive.
  Between 43.4% and 75.5% of the starting roster is still standing at the limit
  (`2026-07-31-movement-v7-baseline.md:287-303`), and the two sides finish within
  nine warriors of each other in every cell. As that document puts it at line
  304, "This is not a battle that was nearly decided and ran a little long. It
  is a standoff."

The equivalent V7 result is the same shape: `AGENT-BACKLOG.md:88-90`
(historical) records "Zero of ten cells reached a decisive outcome within 6,000
ticks; all ten still draw at the 10,000-tick limit, exactly as V6 does", across
six candidate tunings, and adds at lines 93 to 96 that "no cell's terminal tick
moved by a single tick."

### 2.3 The refuse-to-commit ratio: what it is a ratio of, and how it was measured

The figure quoted in the brief as "around 349:1" appears verbatim in four
archived documents. Its primary statement is
`docs/archives/2026-08-06/movement/2026-07-31-movement-v7-baseline.md:307-315`,
and it is restated as a table at
`docs/archives/2026-08-06/movement/2026-07-31-movement-v7-calibration-record.md:551-560`.

It is a ratio of **agent-ticks in one set of footwork phases to agent-ticks in
another**, taken from a single cell: `EquipmentRelativeFootworkV6`, 200 agents,
seed 1, over the full 10,000 ticks. The four inputs are fields of the
`movementMetrics` block of that cell's headless `RunReport`, pasted verbatim at
`2026-07-31-movement-v7-baseline.md:518-531`:

| `movementMetrics` field | Value | Line |
| --- | --- | --- |
| `approachAgentTicks` | 52,158 | `:519` |
| `engageAgentTicks` | 204 | `:520` |
| `commitAgentTicks` | 2,216 | `:521` |
| `recoverAgentTicks` | 2,017 | `:522` |
| `refuseAgentTicks` | 1,140,221 | `:523` |
| `disengageAgentTicks` | 29,043 | `:524` |
| `regroupAgentTicks` | 338,634 | `:525` |
| `pursueAgentTicks` | 0 | `:526` |
| `postureTransitions` | 16,108 | `:527` |
| `facingStepsTurned` | 14,558 | `:528` |
| `disengagementEntries` | 339 | `:529` |
| `conflictDenials` | 130,844 | `:530` |

The ratio is

```
(refuseAgentTicks + regroupAgentTicks) / (commitAgentTicks + recoverAgentTicks)
= (1,140,221 + 338,634) / (2,216 + 2,017)
= 1,478,855 / 4,233
= 349.36
```

Each unit is one living agent on one tick, counted by the phase that agent's
`AgentState.FootworkPhase` finalised to on that tick. The counter definitions
are on `MovementBehaviorMetrics` — `RefuseAgentTicks` is documented at
`src/Hukbo.Core/Simulation/MovementBehaviorMetrics.cs:42-43` as "Total
agent-ticks spent in `FootworkPhase.Refuse`", and the whole record is derived
observability that reaches neither hash.

So the ratio is **not** a ratio of refusals to attacks, and **not** a per-agent
statistic. It says: for every one agent-tick spent inside the attack lifecycle,
349 agent-ticks are spent either refusing to move or regrouping.

**The ratio is a symptom, and the "refuse" half of it is the load-bearing half.**
Section 4 shows that `FootworkPhase.Refuse` is not a tactical decision at all —
it is the name the code gives to "this agent's movement route was rejected" —
and that reframing is what makes the cause locatable.

### 2.4 The consequence that actually decides the battle

The phase counters are indirect. The direct measurement of why nobody dies is
the combat and collision blocks of the same two cells.

| Metric | V6, 200 agents, seed 1, 10,000 ticks | V4, 200 agents, seed 1, 1,279 ticks |
| --- | --- | --- |
| `acceptedAttacks` | **851** (`baseline.md:509`) | **2,612** (`baseline.md:312-315`) |
| `landedAttacks` | **566** (`baseline.md:510`) | **1,778** (`baseline.md:312-315`) |
| `attackCapableAgentTicks` | **3,072** (`baseline.md:502`) | not recorded in this matrix |
| `blockedAgentTicks` | 82,499 (`baseline.md:501`) | not recorded in this matrix |
| `contactPairs` | 1,439 (`baseline.md:499`) | not recorded in this matrix |
| `defenceAttributableShare` | 0.33490011750881316 (`baseline.md:515`) | not recorded in this matrix |
| Deaths over the run | 200 - 151 = **49** (`baseline.md:289`) | 200 - 15 = **185** (`baseline.md:553`) |

Reduced to rates:

```
V4 accepted attacks per tick = 2,612 / 1,279 = 2.042
V6 accepted attacks per tick =   851 / 10,000 = 0.0851
ratio = 24.0x fewer attacks per tick under V6

V4 deaths per tick = 185 / 1,279  = 0.1447
V6 deaths per tick =  49 / 10,000 = 0.0049
ratio = 29.5x fewer deaths per tick under V6
```

`attackCapableAgentTicks` is defined in `docs/development/testing.md:3249` as
"One unit per agent per tick in which that agent held a target inside attack
reach at its resolved position." Under V6 that counter is **3,072** across a run
that holds between 151 and 200 living agents for 10,000 ticks — on the order of
1.56 million living agent-ticks, computed from the phase counters in section 2.3,
which sum to 1,564,493. So a V6 warrior has an enemy inside weapon reach on
roughly **0.2%** of the ticks it is alive.

For scale, `docs/development/testing.md` records `attackCapableAgentTicks` at
200 agents for several shipped, terminating configurations: 9,231 at
`testing.md:2557`, 9,248 at `:2747`, 9,283 at `:2898`, and 8,945 at `:3131` —
all over runs of a few thousand ticks rather than 10,000. Those are three times
V6's total in roughly a fifth of the duration.

**The V6 standoff is a contact failure, not a lethality failure.** Warriors do
not fail to kill each other; they fail to stand next to each other. Section 5
does the arithmetic that rules out the lethality hypothesis explicitly.

### 2.5 Numbers this note could not find

- No recorded twenty-seed run exists for any preset. Every matrix in the corpus
  is the five-seed set `{1, 2, 3, 5, 8}` at two agent counts. The section 1
  criterion's "19 of 20 seeds" has therefore never been evaluated as written for
  a movement preset.
- `attackCapableAgentTicks`, `blockedAgentTicks`, and `contactPairs` were **not
  recorded for the `PersistentContingentsV4` cells** of the V7 baseline matrix,
  so the direct V4-versus-V6 comparison in the table above is incomplete on
  three rows. The `testing.md` figures cited are from different runs at
  different commits and are quoted as scale, not as a matched control.
- No per-reason breakdown of `refuseAgentTicks` exists. `MovementBehaviorMetrics`
  counts the phase, not the predicate that produced it. Section 6 treats adding
  that breakdown as its own candidate.

---

## 3. What V7 did, and why it failed

### 3.1 The commits

The workstream is on `main` today. `git log --oneline --grep="pressure"` from
this worktree returns the following spine, oldest last:

| Commit | Subject |
| --- | --- |
| `1798f82` | `Merge branch 'v7-pressure-interrupt'` — the package landing on `main` |
| `0c227f2` | `docs(movement): archive the V7 workstream and record the canonical gate` |
| `bec359c` | `docs(movement): record the V7 smoke rows, the re-measurement, and the verdict` |
| `9cc75c7` | `test(movement): assert determinism and logging neutrality under V7` |
| `9323834` | `test(movement): add the hand-run pressure-interrupt calibration harness` |
| `0e9cb2a` | `feat(core): project the pressure interrupt onto the agent view` |
| `0bd4bb5` | `feat(ui): show the pressure reading and the break-off cause in the inspector` |
| `99eb001` | `perf(core): weigh the pressure signals once per living agent per tick` |
| `72eec61` | `feat(core): expose the weighted pressure as derived scratch for the inspector` |
| `5110dc1` | `feat(core): wire the pressure interrupt into the footwork and cleanup stages` |
| `665df47` | `feat(movement): add the pressure-interrupt predicate and its step 1a branch` |
| `2559db8` | `feat(core): fold the three pressure-interrupt agent fields behind a new gate` |
| `c010cc9` | `feat(movement): register EquipmentRelativeFootworkV7 with the pressure interrupt` |
| `f62b8da` | `feat(movement): add the per-row pressure-interrupt threshold behind the gate` |
| `3a904c2` | `feat(movement): add the pressure-interrupt version gate to MovementRuleset` |
| `b3ab856` | `docs: record the pre-V7 movement baseline across twenty cells` |
| `20577e7` | `fix(movement): correct the V7 threshold unit and record that tuning cannot pass` |
| `9ea79d4` | `test(movement): pin the V7 content hash and trajectory digest` |

`665df47`'s own message states the design intent and, unusually, states the
failure mode the guard exists to prevent: "The transition-only guard is
load-bearing rather than cosmetic. Without it the interrupt fires on every tick
the pressure holds, including every tick the warrior is already disengaging,
re-charging the cooldown each time; the warrior would never attack again, which
is a worse standoff than the one V7 exists to fix." That guard is the reason the
mechanic could not reach the standoff, and the commit that introduced it says so
in advance.

### 3.2 The mechanic

`ShouldPressureInterrupt`
(`src/Hukbo.Core/Movement/WeaponMovementRules.cs:254-292`) is a pure predicate.
It computes a weighted average of three saturating basis-point signals and
compares it against the actor's row threshold with `>=`. Two guards come first:

```csharp
// src/Hukbo.Core/Movement/WeaponMovementRules.cs:268-272
if (priorPhase != FootworkPhase.Commit &&
    priorPhase != FootworkPhase.Recover)
{
    return false;
}
```

and a threshold of zero or less never fires
(`WeaponMovementRules.cs:276-279`). The three signals, per the parameter
documentation at `WeaponMovementRules.cs:218-256`, are support pressure
(`supportEnemies` against `supportAllies`, where `supportAllies` includes the
actor), incoming damage taken on the previous tick as a fraction of maximum hit
points, and ally collapse (supporting allies lost since the previous tick).

When it fires, `ResolveProvisionalFootwork` short-circuits to `Disengage` at
step 1a:

```csharp
// src/Hukbo.Core/Movement/WeaponMovementRules.cs:598-601
if (pressureInterruptFired)
{
    return (FootworkPhase.Disengage, 0);
}
```

and the caller charges a cost: a full attack cooldown reset
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:1783`), a cleared combo chain
(`BattleSimulation.cs:1794-1795`), and the spectator flag
`agent.BrokeOffUnderPressure = true` (`BattleSimulation.cs:1803`).

### 3.3 The parameters as shipped

Registered on `EquipmentRelativeFootworkV7` in
`src/Hukbo.Core/Movement/MovementPresetRegistry.cs`:

| Parameter | Value | Line |
| --- | --- | --- |
| `supportPressureWeightBasisPoints` | 5,000 | `:428` |
| `incomingDamageWeightBasisPoints` | 3,000 | `:429` |
| `allyCollapseWeightBasisPoints` | 2,000 | `:430` |
| Kampilan threshold | 10,000 | `:393` |
| Wasay threshold | 10,000 | `:395` |
| Kalis threshold | 7,500 | `:397` |
| Itak threshold | 6,250 | `:399` |
| Tall Hardwood + Kalis threshold | 8,750 | `:401` |
| Tall Hardwood + Itak threshold | 7,500 | `:403` |

The registry comment at `MovementPresetRegistry.cs:362-367` records the
derivation: each threshold is that row's own `DisengageEnemyToAllyBasisPoints`
scaled by `supportPressureWeightBasisPoints / 10,000`. The comment at
`:373-379` records a ceiling that matters — because the weighted sum is a true
average, it "can never exceed `2 * SupportPressureWeightBasisPoints + 10,000`
for a warrior that survives the tick — 20,000 here". Two alternative weight
splits, 7,000/2,000/1,000 and 3,000/5,000/2,000, were measured and both drew
every cell (`MovementPresetRegistry.cs:419-425`).

### 3.4 The tick stage it landed in

`AdvanceOneTick` (`src/Hukbo.Core/Simulation/BattleSimulation.cs:599-650`) runs,
in order: `DecrementCooldowns`, `SelectTargetsAndIntents`,
`ResolveContingentStates`, then — under
`_movementRules.UsesEquipmentRelativeFootwork` only —
`ResolveEquipmentPosturesAndProvisionalFootwork` (`:619-625`), then
`GatherMovementProposals`, then — again under the flag —
`ResolveFriendlyClearanceConflicts` (`:628-635`), then `ResolveCollisions`,
`CommitMovement`, `MeasureCollision`, `GatherAndCommitAttacks`, then
`ApplyEquipmentAttackFootworkAndDeathCleanup` (`:641-647`), then
`ResolveOutcome`.

The interrupt is evaluated inside
`ResolveEquipmentPosturesAndProvisionalFootwork`, at
`BattleSimulation.cs:1748-1753`, before the provisional footwork call at
`:1807-1825`. It therefore lands **between the contingent-state stage and
movement proposal gathering** — upstream of movement, downstream of targeting.

Note that the tick-stage list published in `SIMULATION-GAME-STANDARDS.md:513-523`
does not name either equipment-relative stage. That list describes the shipped
default preset's pipeline; the two conditional stages are additions the
equipment-relative presets make. This is a documentation gap, not a defect.

### 3.5 What it measured, and why it failed

`AGENT-BACKLOG.md:82-114` (historical) is the load-bearing record. Its heading
is "The finding that changes the plan's premise", and its first sentence is:
"**V7 does not meet the design section 2.1 termination bar, and no tuning of the
values task E1 owns can make it.** This is measured, not predicted."

The three measurements that close the search:

1. **Zero of ten cells decisive within 6,000 ticks; all ten drew at 10,000**
   (`AGENT-BACKLOG.md:88-90`).
2. **A minimum-threshold probe bounds the mechanic from above.** Registering the
   minimum legal threshold of 1 on every row "makes the predicate fire on every
   agent-tick it can ever fire on. No tuning can fire more"
   (`AGENT-BACKLOG.md:91-93`). That produced firing rates of 0.093% at 200
   agents and 0.105% at 500, and every cell still drew (`:93-95`). The
   calibration record puts the same probe in absolute terms at
   `2026-07-31-movement-v7-calibration-record.md:565-568`: 3,491 firings across
   three 200-agent cells against 3.74 million living agent-ticks, and 6,274
   across two 500-agent cells against 6.00 million.
3. **Across six candidate tunings the firing count ranged over a factor of 4.6
   and no cell's terminal tick moved by a single tick**
   (`AGENT-BACKLOG.md:95-96`; restated at
   `2026-07-31-movement-v7-calibration-record.md:570-575`).

The mechanism of the failure is stated at `AGENT-BACKLOG.md:99-105`: "The
interrupt is gated to fire only from `FootworkPhase.Commit` or `Recover`, but
the standoff is a refusal to enter that lifecycle at all… The interrupt's entire
addressable population is roughly 0.3% of agent-ticks, and the warriors holding
the battle open are precisely the ones that never commit."

The calibration record then names four suspects and explicitly declines to
choose between them
(`2026-07-31-movement-v7-calibration-record.md:585-591` and `:764-768`): "which
of the refuse conditions, the regroup cycle, the cohesion duty window, or the
approach-sidestep rules is responsible is a separate investigation." Section 4
of this note is that investigation.

Two secondary V7 findings are worth carrying forward because they constrain any
fix:

- **The Client cannot select V7, or V6.** `AGENT-BACKLOG.md:120-134`
  (historical) records that `ArenaGame.BuildScenario` is
  `Scenario.CreateDefault(seed, …) with { RosterCounts = … }` and overrides no
  movement preset, so "the Client always runs the shipped default
  `PersistentContingentsV4`". A spectator therefore cannot see the standoff on
  screen today, and cannot see a fix to it either, until either the default
  moves or a Client preset selector exists.
- **V7's own cost overruns its budget independently of the standoff.**
  Measured medians at final values were 0.2020 ms at 200 agents against V4's
  0.0607 (3.33x, ceiling 2.0x) and 0.9279 ms at 500 against V4's 0.2275 (4.08x,
  ceiling 2.5x) (`AGENT-BACKLOG.md:179-181`). The calibration record attributes
  almost all of that to V6 rather than to the interrupt
  (`2026-07-31-movement-v7-calibration-record.md:530-532`).

---

## 4. Root cause

### 4.1 The single sentence

`FootworkPhase.Refuse` is not a tactical decision. It is the name the code gives
to "this warrior's movement route was rejected by the friendly-clearance test",
and that test demands a separation from every living ally that is 1.15 to 1.75
times the body diameter the collision contract permits and actively produces —
so a warrior standing next to an ally at body contact has no reachable endpoint
that satisfies it, refuses to move, thereby zeroes its own retained pace, and
becomes even less able to move on the next tick.

The deciding line is `src/Hukbo.Core/Simulation/BattleSimulation.cs:2453`:

```csharp
if (separation < required)
{
    return false;
}
```

inside `IsLaneClearOfAllies` (`BattleSimulation.cs:2428-2460`).

### 4.2 The decision chain, in order, from "target is in range" to "attack resolves"

This walks the whole path for one living agent on one tick under
`EquipmentRelativeFootworkV6` or `V7`. Every predicate and early return is
named with its file and line.

**Stage A — targeting.** `SelectTargetsAndIntents`
(`BattleSimulation.cs:952`) picks a target and an intent.
`SIMULATION-GAME-STANDARDS.md:485-487` binds the meaning:
"Intent selection marks an agent `Attacking` only when its squared distance to
its target is at or inside the contact distance; an agent that is still closing
is `Moving` even when it is already inside weapon reach."

**Stage B — posture and provisional footwork.**
`ResolveEquipmentPosturesAndProvisionalFootwork`
(`BattleSimulation.cs:1613`) calls `ResolveProvisionalFootwork`
(`src/Hukbo.Core/Movement/WeaponMovementRules.cs:566-668`) per living agent. It
is a ten-step ladder with an inserted step 1a, and the first match wins:

| Step | Predicate | Line | Result |
| --- | --- | --- | --- |
| 1 | `!isAlive` | `:581` | `None` |
| 1a | `pressureInterruptFired` (V7 only) | `:598` | `Disengage` |
| 2 | `priorPhase == Commit` | `:605` | `Commit` (decrement) or `Recover` |
| 3 | `priorPhase == Recover && ticks > 1` | `:614` | `Recover` |
| 4 | `priorPhase == Disengage && enemiesScaled > allies * ReengageBp` | `:625-628` | `Disengage` |
| 5 | `enemiesScaled >= allies * DisengageBp` | `:633-635` | `Disengage` |
| 6 | `posture is Withdraw or Yield` | `:640` | `Disengage` |
| 7 | `posture == Regroup` | `:646` | **`Regroup`** |
| 8 | `hasTarget && targetAtOrInsidePreferredDistance` | `:653` | `Engage` |
| 9 | `hasTarget` | `:659` | `Approach` |
| 10 | otherwise | `:665-667` | `Pursue` or `None` |

Steps 5 and 7 are the two that divert a warrior away from the enemy before the
approach steps are ever reached. Step 5's ratio is
`supportEnemies / supportAllies` measured over the support radius, which V6/V7
register at `supportRadiusBodyDiametersBasisPoints: 60_000`
(`src/Hukbo.Core/Movement/MovementPresetRegistry.cs:352`) — six body diameters,
`6 * 8704 = 52,224` raw, about 51 world units. The per-row entry thresholds are
`disengageEnemyToAllyBasisPoints` 12,500 (Itak,
`Profiles/ItakMovementProfile.cs:40`), 15,000 (Kalis, `:42` of
`KalisMovementProfile.cs`; and the shielded Itak row,
`TallHardwoodMovementProfiles.cs:70`), 17,500 (shielded Kalis row,
`TallHardwoodMovementProfiles.cs:42`), and 20,000 (Kampilan,
`KampilanMovementProfile.cs:38`; Wasay, `WasayMovementProfile.cs:38`).

**Stage C — route search.** `GatherOneEquipmentProposal`
(`BattleSimulation.cs:1869-1998`) routes the agent through a precedence chain.
Only one arm of that chain can produce a refusal:

| Arm | Condition | Line | `phaseSurvives` |
| --- | --- | --- | --- |
| Body-contact hold | `agent.Intent == AgentIntent.Attacking` | `:1877` | forced `true` (`:1883`) |
| Last-stand rally | `agent.Intent == AgentIntent.Regrouping` | `:1885` | forced `true` (`:1906`) |
| Disengage route | `provisionalPhase == Disengage` | `:1908` | route search at `:1910`, but `Disengage` is not refusable |
| Contingent cohesion | `Moving && target != null && cohesion aim resolves` | `:1913` | forced `true` (`:1947`) |
| **Route phases** | `provisionalPhase is Approach or Engage or Commit or Recover or Regroup or Pursue` | `:1949` | **route search at `:1956`** |
| Pursuit floor | `Moving && threat != null` | `:1959` | forced `true` (`:1975`) |
| Fallback | otherwise | else-arm | forced `true` (`:1980`) |

**Stage D — finalisation.** `FinalizeFootwork`
(`src/Hukbo.Core/Movement/WeaponMovementRules.cs:694-709`):

```csharp
// WeaponMovementRules.cs:700-706
if (!hasSurvivingCandidate &&
    provisionalPhase is FootworkPhase.Approach
        or FootworkPhase.Engage
        or FootworkPhase.Pursue)
{
    return (FootworkPhase.Refuse, 0);
}
```

**`Refuse` has exactly one producer in the entire codebase.** A grep for
`FootworkPhase.Refuse` across `src/Hukbo.Core/` returns one enum declaration
(`Movement/FootworkPhase.cs:47`), one return statement
(`Movement/WeaponMovementRules.cs:705`), and the rest documentation comments and
the metrics counter. There is no other assignment. So every one of the 1,140,221
refused agent-ticks in the seed-1 cell is an Approach, Engage, or Pursue whose
route search returned `false`, and `pursueAgentTicks` is 0.

**Stage E — the route search itself.** `TryProposeEquipmentRoute`
(`BattleSimulation.cs:1998-2080`) builds up to three candidates and takes the
first that survives four tests. It returns `false` at `:2079` only if every
candidate fails. The four rejection points, in order:

1. `BuildEquipmentRouteCandidates` returns `0` — for `Approach`/`Engage` this
   happens when `threat is null` or the delta is zero, the two `return 0`
   statements in that arm, the second at `:2113`.
2. `MovementRouteRules.StepEndpoint(...) is not { } endpoint` → `continue`
   (`:2045-2057`). Returns null only on a zero delta
   (`src/Hukbo.Core/Movement/MovementRouteRules.cs:65-68`).
3. `candidate.SubjectToSecondThreatOmission && ShouldOmitDirectCandidate(...)`
   → `continue` (`:2059-2063`). Applies to the direct candidate only:
   `SubjectToSecondThreatOmission: true` is set on `direct` at `:2123` and
   `false` on both obliques.
4. `!IsLaneClearOfAllies(index, agent, endpoint, actorClearanceSquared)` →
   `continue` (`:2065-2069`). Applies to **all three** candidates.

Test 4 is the only one that can reject all three candidates in a battle that is
otherwise proceeding normally. Tests 1 and 2 need a dead or coincident target;
test 3 exempts the obliques by construction.

**Stage F — the second clearance gate.** A proposal that survives stage E still
faces `ResolveFriendlyClearanceConflicts` (`BattleSimulation.cs:2718-2765`),
which runs per faction between gathering and collision resolution and nulls a
losing proposal outright (`:2757-2761`), incrementing `_movementConflictDenials`.
This one does not change the phase, so a warrior denied here still reports
`Approach` while standing still.

**Stage G — collision.** `ResolveCollisions` (`:3386`) applies the candidate
ladder of `SIMULATION-GAME-STANDARDS.md:564-573`, and `CommitMovement`
(`:3425`) writes the result. Critically, at `:3453-3465`, a move that produced
zero displacement zeroes the retained pace:

```csharp
// BattleSimulation.cs:3455-3464
if (deltaX == 0 && deltaY == 0)
{
    if (commitsRetainedPace)
    {
        // Design section 6.5: a blocked, rejected, or refused
        // move leaves zero retained pace rather than fictitious
        // momentum.
        agent.MovementPaceRaw = 0;
    }
    continue;
}
```

`commitsRetainedPace` is `_movementRules.UsesEquipmentRelativeFootwork`
(`:3429`), so this applies to V6 and V7 and to no earlier preset.

**Stage H — attack.** `GatherAndCommitAttacks` (`:3579`) runs after movement and
reads resolved positions. **It reads no footwork phase.** The reach test is
centre-to-centre against `AttackRangeRaw` per
`SIMULATION-GAME-STANDARDS.md:456-466`, and the cooldown gate is
`AttackCooldownRemaining`. A refusing warrior standing at reach would still
attack. `FootworkPhase.Commit` is *set by* attack acceptance, in
`ApplyEquipmentAttackFootworkAndDeathCleanup` at `BattleSimulation.cs:2824`
(`agent.FootworkPhase = FootworkPhase.Commit;`), documented at `:2769-2771` as
"surviving accepted attackers enter `FootworkPhase.Commit`".

That last point closes the loop on the refuse-to-commit ratio. `Commit` is a
**consequence** of attacking, never a precondition for it. So the 349:1 ratio is
not describing a gate that blocks attacks; it is describing the fact that
attacks almost never happen, because the warriors are never adjacent.

### 4.3 The condition that is false most of the time, named

**`IsLaneClearOfAllies` (`BattleSimulation.cs:2428-2460`), on all three
candidates.**

The predicate scans every living same-faction agent and requires the candidate
endpoint to sit at squared distance at least
`max(actor clearance, ally clearance)` squared from that ally's **tick-start**
position (`:2447-2456`). The clearance radius is
`MovementRouteRules.ClearanceRadiusRaw(bodyRadiusRaw, bodyDiametersBasisPoints)`
= `2 * bodyRadiusRaw * bp / 10000`
(`src/Hukbo.Core/Movement/MovementRouteRules.cs:262-273`), reached through
`SquaredClearanceRadius` (`BattleSimulation.cs:2693-2697`).

At the default `BodyRadiusRaw = 4352` — body diameter 8,704 raw, 8.5 world units
(`SIMULATION-GAME-STANDARDS.md:428-433`) — the six registered rows are:

| Loadout row | `allyClearanceBodyDiametersBasisPoints` | Declared at | Clearance, raw | World units | Multiple of the body diameter |
| --- | --- | --- | --- | --- | --- |
| Itak | 11,500 | `Profiles/ItakMovementProfile.cs:39` | 10,009 | 9.77 | 1.15x |
| Kalis | 12,000 | `Profiles/KalisMovementProfile.cs:41` | 10,444 | 10.20 | 1.20x |
| Tall Hardwood + Itak | 13,500 | `Profiles/TallHardwoodMovementProfiles.cs:69` | 11,750 | 11.47 | 1.35x |
| Tall Hardwood + Kalis | 14,000 | `Profiles/TallHardwoodMovementProfiles.cs:41` | 12,185 | 11.90 | 1.40x |
| Kampilan | 15,000 | `Profiles/KampilanMovementProfile.cs:37` | 13,056 | 12.75 | 1.50x |
| Wasay | 17,500 | `Profiles/WasayMovementProfile.cs:37` | 15,232 | 14.88 | 1.75x |

Two of those six exceed `AttackRangeRaw = 12,288`. A Wasay warrior is required
to keep almost fifteen world units from its nearest ally while its own weapon
reaches twelve.

The collision contract, meanwhile, permits and produces ally separations of
exactly 8,704 raw. `SIMULATION-GAME-STANDARDS.md:435-437` — "**Tangent contact
is clearance, not collision.** Two bodies exactly touching is an accepted
resting position." And `:496` — "Living agent — living ally | Solid. Zero
overlap. Identical to the enemy rule."

**These two rules disagree by a factor of 1.15 to 1.75.** The collision resolver
packs allies to 8,704 raw and the movement rule then refuses to let them move.

### 4.4 Why the disagreement is an absorbing state rather than a transient

Three compounding facts make the configuration a fixed point.

**Fact 1: the actor's own position is never tested.** `IsLaneClearOfAllies`
tests the *endpoint* against ally positions (`:2451-2452`). Standing still is
always legal. So a configuration that violates the clearance rule is stable: the
rule punishes movement out of it, not the state itself.

**Fact 2: one step cannot cross the gap, for four of the six rows.** Displacement
per tick is bounded by the pace, and `MovementRouteRules.DesiredPaceRaw` returns
`min(movementSpeedRaw, scaled)` (`MovementRouteRules.cs:204`), so the ceiling is
`MovementSpeedRaw = 3072`. After a refusal the retained pace is zero (stage G
above), and the next tick's pace is one acceleration step,
`max(1, MovementSpeedRaw * accelerationBasisPointsPerTick / 10000)`
(`MovementRouteRules.cs:213-222`):

| Row | `accelerationBasisPointsPerTick` | Declared at | First-step pace, raw | Best reachable separation from an ally at 8,704 | Required | Escapes? |
| --- | --- | --- | --- | --- | --- | --- |
| Itak | 7,000 | `ItakMovementProfile.cs:35` | 2,150 | 10,854 | 10,009 | yes |
| Kalis | 6,000 | `KalisMovementProfile.cs:37` | 1,843 | 10,547 | 10,444 | yes, by 103 raw |
| Tall Hardwood + Itak | 6,500 | `TallHardwoodMovementProfiles.cs:65` | 1,996 | 10,700 | 11,750 | **no** |
| Tall Hardwood + Kalis | 5,600 | `TallHardwoodMovementProfiles.cs:37` | 1,720 | 10,424 | 12,185 | **no** |
| Kampilan | 5,000 | `KampilanMovementProfile.cs:33` | 1,536 | 10,240 | 13,056 | **no** |
| Wasay | 4,000 | `WasayMovementProfile.cs:33` | 1,228 | 9,932 | 15,232 | **no** |

Even at the absolute pace ceiling of 3,072, which a refusing warrior can never
reach because refusing resets it, the reachable separation is 11,776 raw. That
still fails Kampilan's 13,056, the shielded Kalis row's 12,185, and Wasay's
15,232, and clears the shielded Itak row's 11,750 by twenty-six raw units.

**These figures are arithmetic on values read from source, not measurements.**
They are labelled as such. The measured consequence is section 2.4's 95.6%
route-search failure rate, computed below.

**Fact 3: the three candidates all point the same way.** The candidate set for
`Approach` and `Engage` is the direct vector to the threat plus two obliques
(`BattleSimulation.cs:2119-2140`), and the oblique is a rotation of 22.5 degrees
— `ObliqueCosine = 946`, `ObliqueSine = 392` at `ObliqueScale = 1024`
(`MovementRouteRules.cs:31-37`), documented as "Cosine of 22.5 degrees at scale
1024". So the three candidates span a 45-degree arc, all of it aimed at the
enemy. The direction "directly away from the crowding ally" is not in the
candidate set unless that ally happens to lie behind the actor within the
complementary arc. A warrior in a packed line, whose allies are beside and
behind it, has three candidates that all move it *toward* the enemy and
therefore laterally past its neighbours — the worst available direction for
satisfying an ally-separation constraint.

Together: the state is not escaped, the step is too short to escape it for four
of six rows, and the three directions offered do not point out of it.

### 4.5 The measured failure rate of the route search

**Superseded arithmetic, corrected 2026-08-07 during RU-06.** The numbers in the
block below come from
`docs/archives/2026-08-06/movement/2026-07-31-movement-v7-baseline.md:523`, which
is an archived document recording a **V7** cell on 2026-07-31. Two things are
wrong with using it here: `CLAUDE.md` section 6 forbids citing an archived file as
justification, and the tree has drifted since it was written. Measured directly on
the current tree at 200 agents, seed 1, 10,000 ticks:

| Preset | `refuseAgentTicks` |
| --- | --- |
| `EquipmentRelativeFootworkV6` | 692,750 |
| `EquipmentRelativeFootworkV7` | 1,092,119 |

The 1,140,221 below reproduces on neither. Recomputed against today's V6 block —
`refuseAgentTicks` 692,750, `approachAgentTicks` 37,414, `engageAgentTicks` 198,
`pursueAgentTicks` 0:

```
route-search failure rate >= 692,750 / 730,362 = 94.85%
```

**The conclusion of this section is unchanged.** Route-search refusal still
dominates by an overwhelming margin, so the diagnosis this document draws and the
F-B intervention it motivates both stand. Only the figures needed correcting. The
original derivation is preserved below rather than rewritten, so the reasoning can
still be traced.

From the seed-1 200-agent `movementMetrics` block in section 2.3:

```
provisional Approach/Engage/Pursue agent-ticks that reached stage E
  <= refuseAgentTicks + approachAgentTicks + engageAgentTicks + pursueAgentTicks
  =  1,140,221      + 52,158            + 204              + 0
  =  1,192,583

route-search failure rate >= 1,140,221 / 1,192,583 = 95.61%
```

The inequality is the honest form. `approachAgentTicks` also counts Approach
agent-ticks that were satisfied by the contingent-cohesion arm or the pursuit
floor without ever running the route search, so 1,192,583 is an upper bound on
stage-E entries and **95.61% is a lower bound on the route search's failure
rate**.

The same block gives an approximate decomposition of the roughly 1.56 million
living agent-ticks in that run. The counters have different denominators and do
not form an exact partition, so these are proportions rather than a sum:

| Outcome | Counter | Value | Share of ~1,564,493 living agent-ticks |
| --- | --- | --- | --- |
| Route search rejected every candidate | `refuseAgentTicks` | 1,140,221 | 72.9% |
| Proposal nulled by the friendly-clearance conflict pass | `conflictDenials` | 130,844 | 8.4% |
| Proposal reached the collision resolver and was blocked | `blockedAgentTicks` | 82,499 | 5.3% |
| Proposal actually displaced the agent | `acceptedMoves` | 117,518 | 7.5% |

`acceptedMoves` is defined at `docs/development/testing.md:3246` as "Movement
proposals that resolved to a destination other than the agent's tick-start
position, summed over ticks", and `blockedAgentTicks` at `:3248`.

Note the second line. **More proposals were destroyed by the friendly-clearance
conflict pass than survived to move an agent** — 130,844 against 117,518. The
same ally-separation policy is applied twice, once as a filter on candidates and
once as a tournament between proposals, and between them they remove more than
nine tenths of all intended movement.

### 4.6 Is this one cause, or several? The ranking

The evidence identifies **one dominant cause and two contributing ones**, and I
can separate them by magnitude but not by a controlled experiment, because no
measurement in the corpus varies one of them while holding the others fixed.

**Rank 1 — the ally-clearance radius exceeds the body diameter the collision
contract produces. Dominant. Confidence: high.**

Evidence: `Refuse` has exactly one producer
(`WeaponMovementRules.cs:705`); that producer requires the route search to have
failed; the route search's only test that can reject all three candidates is
`IsLaneClearOfAllies`; the measured failure rate is at least 95.61%; the six
clearance radii are 1.15x to 1.75x the body diameter the collision contract
calls a legal resting position; and four of six rows cannot cross that gap in
one step even at maximum pace. Every link is either a source line or a measured
counter.

**Rank 2 — the friendly-clearance conflict pass applies the same policy a second
time. Contributing. Confidence: high that it happens, medium on its share.**

Evidence: `conflictDenials` 130,844 measured, against `acceptedMoves` 117,518.
This one cannot cause `Refuse` — the phase is already final — but it removes a
comparable volume of movement, and it removes it from the agents that *passed*
the first filter, which are precisely the ones that were about to close.

**Rank 3 — steps 5 and 7 of the footwork ladder divert warriors before the
approach steps are reached. Contributing. Confidence: medium.**

Evidence: `regroupAgentTicks` 338,634 (21.6% of living agent-ticks) is produced
only by step 7 (`WeaponMovementRules.cs:646-649`), which fires unconditionally
on `TacticalPosture.Regroup`; `disengageAgentTicks` 29,043 plus
`disengagementEntries` 339 come from steps 4, 5, and 6. Twenty-one percent of
the battle is warriors walking toward a regroup point rather than toward an
enemy. This is real and large, but it is one third the size of the refuse
population and it is the mechanism the design intended, whereas rank 1 is a
mechanism nobody designed.

**Explicitly not the cause, with evidence:**

- **Not the defensive resolution contract.** Section 5 does the arithmetic. The
  short form: the shipped default preset terminates in 1,279 to 4,405 ticks
  using the *same* clash tables.
- **Not the collision resolver.** `maximumPenetrationRaw` is 0
  (`2026-07-31-movement-v7-baseline.md:506`), the resolver is behaving to
  contract, and `SIMULATION-GAME-STANDARDS.md:560-562` records that being
  blocked does not remove an agent from combat. `blockedAgentTicks` at 82,499 is
  5.3% of agent-ticks, an order of magnitude below the refuse population.
- **Not the pressure interrupt.** Measured and closed in section 3.5.
- **Not the approach-sidestep rules,** one of the four suspects the calibration
  record named. `ShouldOmitDirectCandidate` (`BattleSimulation.cs:2394-2417`)
  exempts both obliques by construction — `SubjectToSecondThreatOmission` is set
  `true` only on the direct candidate (`BattleSimulation.cs:2115`) — so it can
  never reject all three. It can only reorder which one is taken.
- **The cohesion duty window,** the remaining suspect from that list, is
  **not ruled out** and is not separately measured. Its arm at
  `BattleSimulation.cs:1909-1941` forces `phaseSurvives = true`, so a cohesion
  grant cannot cause a refusal; but a cohesion *denial* returns the agent to the
  route-phase arm where the refusal happens, so the duty cycle modulates how
  many agents are exposed to rank 1 without being a cause in its own right.

---

## 5. The defensive resolution contract's contribution

The hypothesis under test is real and deserves a real answer: a battle that
cannot end might be an expected-damage problem rather than a movement problem.
This section does the arithmetic and rejects it.

### 5.1 The five outcomes and the composition rule, as configured

`AttackResolution` (`src/Hukbo.Core/Combat/AttackResolution.cs`) has the five
pinned members `Landed = 0`, `ShieldBlocked = 1`, `Parried = 2`, `Deflected = 3`,
`Evaded = 4`, tabulated in `SIMULATION-GAME-STANDARDS.md:810-816`. Only `Landed`
applies damage (`:818`), and the other four are "mutually exclusive, jointly
exhaustive alternatives to a landed blow, never summed on top of a separate base
probability" (`:818-819`).

The composition rule is `SIMULATION-GAME-STANDARDS.md:842-853`. The roll walks a
fixed cumulative interval in the order shield, hard (parry), soft (deflect),
void, landed; each width is basis points out of `ClashProfile.BasisPointScale`
= 10,000 (`src/Hukbo.Core/Combat/ClashProfile.cs:47`); and if the summed shield,
weapon, and void channels exceed `MaximumInterceptionBasisPoints`, all three are
rescaled proportionally, with the truncation residue becoming additional
`Landed` probability.

Preset V2 registers `maximumInterceptionBasisPoints: 5_500`
(`src/Hukbo.Core/Combat/PhilippineCombatPresetV2.cs:350`). That is the hard
ceiling on the whole defence: **at most 55% of accepted attacks can be
intercepted, so at least 45% must land, by construction, whatever the per-cell
tables say.**

The enforced acceptance band is `SIMULATION-GAME-STANDARDS.md:855-861`: the
defence-attributable share must land inside 0.25 to 0.45 across seeds 1 through
20 at 200 agents. So the *permitted* land rate is bounded below by 0.55 and
above by 0.75.

### 5.2 The measured outcome distribution

From the V6 seed-1 200-agent `combatMetrics` block
(`2026-07-31-movement-v7-baseline.md:508-516`):

| Outcome | Count | Share of 851 accepted |
| --- | --- | --- |
| `Landed` | 566 | 66.51% |
| `ShieldBlocked` | 86 | 10.11% |
| `Parried` | 14 | 1.65% |
| `Deflected` | 72 | 8.46% |
| `Evaded` | 113 | 13.28% |
| Defence-attributable total | 285 | **33.49%** |

`(86 + 14 + 72 + 113) / 851 = 285 / 851 = 0.33490011750881316`, which reproduces
the reported `defenceAttributableShare` to the last digit and sits comfortably
inside the 0.25-to-0.45 band. **The defence is behaving exactly to contract.**

### 5.3 Lethality arithmetic

`Scenario.MaximumHitPoints` is 100 (`src/Hukbo.Core/Simulation/Scenario.cs:28`).
Damage is flat per weapon — `_damageTotals[targetIndex] += source.DamagePerAttack`
at `BattleSimulation.cs:3700-3701`, with no hit-location multiplier applied to
the damage total. Preset V2's registered damages
(`PhilippineCombatPresetV2.cs:134, 144, 154, 162, 172, 180`):

| Weapon / loadout | Damage | Blows to kill from full health | Cooldown ticks |
| --- | --- | --- | --- |
| Wasay | 18 | 6 | 8 |
| Kampilan | 15 | 7 | 7 |
| Kalis (solo) | 11 | 10 | 5 |
| Kalis + shield | 10 | 10 | 5 |
| Itak (solo) | 9 | 12 | 4 |
| Itak + shield | 8 | 13 | 4 |

So between 6 and 13 landed blows kill one warrior from full health. Divided by
the measured land rate of 0.6651, that is **9 to 20 accepted attacks per kill**
in the idealised case where every blow falls on the same target.

The measured figures bracket that range, which is the check that the model is
right:

```
V6, seed 1, 200 agents:  566 landed / 49 deaths  = 11.55 landed per death
                         851 accepted / 49 deaths = 17.37 accepted per death
V4, seed 1, 200 agents: 1,778 landed / 185 deaths =  9.61 landed per death
                        2,612 accepted / 185 deaths = 14.12 accepted per death
```

The two presets agree to within 20% on landed-blows-per-death. **Lethality per
blow is not the variable that differs between a battle that ends and one that
does not.**

### 5.4 Can a battle end in 10,000 ticks at the configured probabilities?

Two answers, because the question has two readings.

**Reading A — with the shipped default preset's attack volume: yes, measured.**
All ten `PersistentContingentsV4` cells decided, in 1,279 to 4,405 ticks, using
these exact clash tables and these exact damage values (section 2.2). No
arithmetic is needed; it is measured twenty times over.

**Reading B — with `EquipmentRelativeFootworkV6`'s attack volume: no.** Here is
the number.

A battle is decided only when one entire faction is dead (section 1). At 200
agents that is 100 warriors.

```
Measured V6 kill rate, seed 1, 200 agents:
    49 deaths / 10,000 ticks = 0.00490 deaths per tick, both factions combined

Most favourable possible accounting — every death falls on one faction:
    100 / 0.00490 = 20,408 ticks

Measured per-side accounting — faction 1 went 100 -> 73, so 27 deaths:
    27 / 10,000 = 0.00270 deaths per tick
    100 / 0.00270 = 37,037 ticks
```

**A V6 battle needs an estimated 20,408 ticks in the most generous accounting
and 37,037 ticks at its own measured per-side rate. The tick limit is 10,000 and
the standards cap is 5,000.** Against the standards cap that is a shortfall
factor of **4.1x** on the generous accounting and **7.4x** on the measured one.
These two figures are linear extrapolations from measured counters and are
labelled **estimates**; the measured facts they rest on are the 49 deaths, the
10,000 ticks, and the 100-per-faction roster.

The required throughput, stated the other way round:

```
To empty one 100-warrior faction inside the 5,000-tick standards cap:
    100 deaths x 11.55 landed blows per death = 1,155 landed blows
    1,155 / 5,000 = 0.231 landed blows per tick
    at the measured 66.51% land rate: 0.347 accepted attacks per tick

V6 delivers   851 / 10,000 = 0.0851 accepted attacks per tick  (4.08x short)
V4 delivers 2,612 /  1,279 = 2.042  accepted attacks per tick  (5.9x surplus)
```

### 5.5 The decisive test: can the composition rule fix it?

Give the defence the most generous possible help. Set every interception channel
to zero, so `defenceAttributableShare` becomes 0.00 and every accepted attack
lands. This is not a legal configuration — it violates the 0.25 lower bound at
`SIMULATION-GAME-STANDARDS.md:859` — and it is the absolute upper bound on what
retuning the clash tables can achieve.

```
Landed blows would rise from 566 to 851 over 10,000 ticks: a factor of 1.504.
Deaths would rise from 49 to an estimated 851 / 11.55 = 73.7 over 10,000 ticks.
Split roughly evenly, as every measured cell splits: about 37 per faction.
The battle ends at the tick limit with roughly 63 warriors alive per side.
Still a Draw.
```

**Zeroing the entire defensive resolution contract does not terminate a V6
battle.** It closes about a quarter of the gap and leaves an estimated 2.7x
shortfall against the 10,000-tick limit, and about 5.4x against the 5,000-tick
standards cap. This is an estimate by linear extrapolation and is labelled as
such; it ignores the second-order feedback that fewer living agents means both
fewer attackers and less crowding, and that feedback could push the result in
either direction. It does not plausibly close a 2.7x gap.

Going the other way — keeping the interception shares and raising damage —
would need each landed blow to remove roughly 11.55 times more health than it
does, which means a one-shot kill at 100 damage against 100 hit points. At that
point shield, parry, deflect, and evade stop being a defensive contract and
become a coin flip on instant death, and the historical-boundary language at
`SIMULATION-GAME-STANDARDS.md:907-917` about these being gameplay tuning values
does not stretch to cover a change of that magnitude without a new design.

### 5.6 Verdict on the hypothesis

**Rejected.** The defensive resolution contract is not a contributing cause of
the standoff, on three independent grounds:

1. The same clash tables and the same damage values produce twenty decisive
   battles under the shipped default movement preset (section 2.2, measured).
2. The measured `defenceAttributableShare` of 0.3349 sits mid-band, and
   landed-blows-per-death agrees to within 20% between the terminating and the
   non-terminating preset (section 5.3, measured).
3. Zeroing the entire contract — more than any legal retune could achieve —
   leaves the battle undecided by an estimated factor of 2.7 (section 5.5,
   estimated).

The variable that differs is attack **volume**, 24x lower under V6, and section
4 locates why.

---

## 6. Candidate fixes

Six candidates. Five are fixes and one is a prerequisite. They are genuinely
different in kind — one changes the *shape* of the constraint, one changes its
*value* and adds a structural invariant, one changes the *search*, one changes
the *candidate set*, and one changes the *definition of a decided battle* and
touches no movement code at all.

**A shared cost baseline, so every estimate below is anchored.** The lane scan
is `O(agents x candidates x agents)`: `TryProposeEquipmentRoute` evaluates up to
three candidates (`BattleSimulation.cs:2005-2006`, a `stackalloc` of 3) and each
calls `IsLaneClearOfAllies`, which walks the whole agent array
(`BattleSimulation.cs:2434`). At 500 agents that is up to
`500 x 3 x 500 = 750,000` iterations per tick, and each iteration calls
`_movementRules.ResolveLoadoutProfile(ally.Loadout)` inside the innermost loop
(`BattleSimulation.cs:2450`) rather than hoisting a per-loadout table.
`ResolveFriendlyClearanceConflicts` adds a second per-faction pass
(`:2718-2765`). The measured consequence is
`EquipmentRelativeFootworkV6`'s 500-agent median `p50Milliseconds` of **0.8666**
against `PersistentContingentsV4`'s **0.2275** — a **3.81x** overrun against a
2.5x ceiling (`2026-07-31-movement-v7-baseline.md:395-400`, and the ceiling at
`AGENT-BACKLOG.md:179-181`). **The movement stage is already over budget before
any fix is applied.** Every per-tick cost figure below is an estimate scaled
from that 0.8666 ms measurement and the iteration count, and is labelled as an
estimate.

**A shared determinism note.** Every candidate that changes which position an
agent occupies at the end of a tick changes `StateHasher.Compute`'s input and
therefore the state hash, and any that changes which `Move` events are emitted
changes the event hash too. Per `CLAUDE.md` section 5 and
`SIMULATION-GAME-STANDARDS.md:129-151`, that requires a **new movement preset
value** — the next after `EquipmentRelativeFootworkV7 = 7`
(`src/Hukbo.Core/Movement/MovementPresetId.cs:145`) — plus a new registry entry
and new golden expectations. It does **not** require touching
`PersistentContingentsV4`, which stays the shipped default and whose pinned
artifacts must not move. Candidate F-A is the only one that changes no hash.

---

### F-A. Split `refuseAgentTicks` by rejection reason (prerequisite, not a fix)

**Mechanism.** `MovementBehaviorMetrics` counts the phase, not the predicate.
Add four derived counters alongside `RefuseAgentTicks` — no candidates built,
step endpoint rejected, direct candidate omitted, lane not clear — incremented
in `TryProposeEquipmentRoute` at the four `continue`/`return 0` sites
(`BattleSimulation.cs:2056`, `:2062`, `:2068`, `:2079`). Section 4.6 ranks the
causes from source reading and a lower-bound rate; this turns the ranking into a
measurement.

**Files.** `src/Hukbo.Core/Simulation/MovementBehaviorMetrics.cs`,
`src/Hukbo.Core/Simulation/BattleSimulation.cs`, the headless `RunReport`
projection in `src/Hukbo.Headless/`, and `tests/Hukbo.Core.Tests/Movement/MovementBehaviorMetricsTests.cs`.

**State hash.** **No.** `MovementBehaviorMetrics` is derived observability that
reaches neither hash, on the same footing as `CollisionMetrics` and
`CombatMetrics` (`SIMULATION-GAME-STANDARDS.md:881-882` for the combat
equivalent; `:543-544` for `MeasureCollision`). No new preset version. This is
the only candidate that can be verified against the existing pinned artifacts
without moving one.

**Cost per tick at 500 agents.** Four integer increments on paths already taken.
**Estimate: unmeasurable, well under 1 microsecond.**

**What it would break.** Nothing in the simulation. It adds four fields to a
public record, which is a source-compatible but not binary-compatible change to
`Hukbo.Core`'s surface, and the Client's `RunReport` reader would need the new
fields tolerated.

**How to measure whether it worked.** Run the section 8 command on
`EquipmentRelativeFootworkV6` at 200 agents, seed 1, and check that the four new
counters sum to 1,140,221, reproducing the recorded `refuseAgentTicks` exactly.
That sum is the correctness test and the answer at the same time.

---

### F-B. Make ally clearance a monotonicity constraint instead of a state constraint

**Mechanism.** `IsLaneClearOfAllies` currently rejects an endpoint on its
absolute distance to every ally (`BattleSimulation.cs:2451-2456`), which makes an
already-violating configuration absorbing (section 4.4, fact 1). Change the
predicate so a candidate is rejected only when it moves the actor **closer** to
an ally it is already too close to:

```
reject if separation < required AND separation < currentSeparationToThatAlly
```

This is not an invented pattern. `ShouldOmitDirectCandidate` already uses
exactly this shape twelve lines earlier — `return endpointSquared < startSquared;`
at `BattleSimulation.cs:2416` — with the documented convention "Exact equality
keeps the direct candidate" (`:2392`).

The result: a warrior in a crowd may always move, provided it does not tighten
any violation. Ally clearance still shapes the line at normal density, because
at normal density the absolute test and the monotone test agree; it stops being
a trap only in the crowded case, which is the only case where it is currently
absorbing.

**Files.** `src/Hukbo.Core/Simulation/BattleSimulation.cs` (`IsLaneClearOfAllies`
only), a new `MovementPresetId` member and registry row in
`src/Hukbo.Core/Movement/MovementPresetId.cs` and
`src/Hukbo.Core/Movement/MovementPresetRegistry.cs`, and tests in
`tests/Hukbo.Core.Tests/Movement/`.

**State hash.** **Yes, under the new preset only.** V6 and V7 keep their frozen
content hashes and trajectory digests, including
`ContentHash = 0x66F4FDF91F56AF1B` and
`tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-movement-v7-digest.json`
(`AGENT-BACKLOG.md:77-79`, historical).

**Cost per tick at 500 agents.** One extra squared-distance computation per
`(candidate, ally)` pair — the actor's own tick-start separation — which can be
hoisted out of the candidate loop entirely, since it does not depend on the
candidate. Hoisted, the added work is 500 x 500 = 250,000 squared distances per
tick against the existing 750,000. **Estimate: +0.10 to +0.15 ms at 500 agents
on top of the measured 0.8666 ms.** Naively unhoisted it would be +0.29 ms.

**What it would break.** The "free lane" concept in the weapon-relative movement
research becomes advisory rather than guaranteed: a warrior may end a tick
inside another warrior's declared weapon-clearance radius. The six equipment
research documents in `docs/research/movement/` treat the free lane as a
constraint the design promises (`docs/research/movement/README.md:231`, "Free
lane: Space in which the warrior can move or commit without intersecting a
teammate's body or required weapon clearance"). Any weapon-differentiation claim
resting on clearance alone weakens. Every existing `IsLaneClearOfAllies` unit
test asserting the absolute rule would need a preset-scoped sibling rather than
an edit — the existing assertions stay true for V6 and V7.

**How to measure whether it worked.** F-A's four counters first, to confirm the
lane-clear rejection count collapses; then the ten-cell matrix, checking
`measuredTicks`, `outcome`, `acceptedAttacks`, and `attackCapableAgentTicks`
against section 2's baseline. The bar is `SIMULATION-GAME-STANDARDS.md:865-866`.

---

### F-C. Reconcile the clearance radius with the collision contact distance, and make the disagreement unrepresentable

**Mechanism.** Two parts, and the second is what distinguishes this from a
tuning pass.

Part one: cap the effective ally-clearance radius at the body diameter the
collision contract calls a legal resting position, either by clamping in
`MovementRouteRules.ClearanceRadiusRaw` (`MovementRouteRules.cs:262-273`) or by
lowering the six `allyClearanceBodyDiametersBasisPoints` values (section 4.3
table) to at most 10,000.

Part two, and the durable half: add a validation rule that rejects the
disagreement at construction, in the same shape as the rule already at
`SIMULATION-GAME-STANDARDS.md:464-466` — "`Scenario.Validate` rejects any
configuration where `2 * BodyRadiusRaw > AttackRangeRaw`, because that
combination produces bodies that can never reach each other." The new rule would
be that no registered `allyClearanceBodyDiametersBasisPoints` may exceed
`10,000 + (MovementSpeedRaw * 10,000 / (2 * BodyRadiusRaw))`, which is the
condition under which a warrior at ally contact can still find a legal endpoint
in one step. At the defaults that ceiling is
`10,000 + 3,072 * 10,000 / 8,704 = 13,529` basis points — which Wasay's 17,500
and Kampilan's 15,000 both violate today, and which is exactly the
"cannot escape" column of section 4.4's table, restated as an invariant.

**Files.** `src/Hukbo.Core/Movement/MovementRouteRules.cs` or the six files under
`src/Hukbo.Core/Movement/Profiles/`; `src/Hukbo.Core/Movement/MovementRuleset.cs`
for the construction validation; new preset member and registry row; tests.

**State hash.** **Yes, under the new preset only.** If the clamp is applied
inside `ClearanceRadiusRaw` it would also change V6 and V7 and move their frozen
artifacts, which is forbidden — so it has to be registry-scoped, i.e. new profile
rows on a new preset, not an edit to the shared helper.

**Cost per tick at 500 agents.** Zero added work; a smaller clearance radius
means fewer rejections and therefore *more* proposals reaching the collision
stage, which is where the real cost is
(`2026-07-31-movement-v7-calibration-record.md:742-745` flags `ResolveCollisions`
at 58% to 77% of tick time, citing `docs/research/TICK-STAGE-PROFILE.md`).
**Estimate: net neutral to +0.15 ms at 500 agents**, the increase coming from
more work downstream rather than more work here.

**What it would break.** This is the candidate with the largest documentation
blast radius. The six clearance values are the single most visible output of the
six weapon movement research sessions, and flattening them to a common ceiling
erases the ordering those sessions recorded — Wasay widest, Itak tightest. That
ordering is labelled a provisional reconstruction for gameplay tuning
(`MovementPresetRegistry.cs:355-359` for the analogous threshold note), so
nothing historical is lost, but a design document owes an explicit answer for
why the differentiation moves to another axis instead.

**How to measure whether it worked.** Same as F-B, plus one specific check: the
per-loadout `refuseAgentTicks` share, to confirm Wasay and Kampilan stop being
over-represented. The calibration record's open question 2 — the shielded Kalis
row spending 162 of 400 ticks in `Refuse` and not reaching its first `Commit`
until tick 259 (`2026-07-31-movement-v7-calibration-record.md:580-583`) — is a
ready-made regression case.

---

### F-D. A never-refuse route ladder, mirroring the collision resolver's own

**Mechanism.** Today the route search is a three-way filter that can return
nothing. The collision resolver, one stage later, faces the same problem and
solves it with a ladder that always terminates:
`SIMULATION-GAME-STANDARDS.md:564-573` — full step, X-only slide, Y-only slide,
a truncation ladder at `m >> 1, m >> 2, ... 1`, and finally hold position. Give
`TryProposeEquipmentRoute` the same structure: after all three directional
candidates fail the lane test, retry the preferred direction at halved lengths
down to one raw unit, and only refuse if even a one-unit step fails.

This is different in kind from F-B and F-C: it does not change the constraint at
all. It changes the *search* from "find a legal full step or give up" to "find
the longest legal step", which is exactly the discipline the collision stage
already follows, and it removes the pace-reset feedback of section 4.4 fact 2 by
letting a refusing warrior keep some retained pace.

**Files.** `src/Hukbo.Core/Simulation/BattleSimulation.cs`
(`TryProposeEquipmentRoute` and the candidate span size),
`src/Hukbo.Core/Movement/MovementRouteRules.cs` if the truncation helper is
shared with the collision ladder; new preset member and registry row; tests.

**State hash.** **Yes, under the new preset only.**

**Cost per tick at 500 agents.** This is the expensive one. A truncation ladder
from a 3,072-raw step down to 1 is eleven rungs (`3072 >> 1` through `>> 11`),
and each rung costs a full lane scan. Worst case the per-tick iteration count
goes from 750,000 to `500 x (3 + 11) x 500 = 3,500,000`, a 4.67x increase on the
dominant term. **Estimate: +2.0 to +2.5 ms at 500 agents**, taking the movement
stage from 3.81x over its ceiling to something near 15x. A depth cap of two or
three rungs would bring it to an estimated +0.3 to +0.6 ms; the full ladder is
not affordable. **This candidate is only viable in a truncated form, and it must
be paired with hoisting the `ResolveLoadoutProfile` call out of the innermost
loop (`BattleSimulation.cs:2450`).**

**What it would break.** Warriors would creep forward one raw unit at a time in
a crush rather than standing still, which changes the visual character of a
packed line and interacts with the gait animation system: `GaitGeometry.ResolveMode`
treats exactly zero displacement as `Stance` and anything below 1,600 raw per
tick as `Walk` (`docs/research/ranged/2026-08-07-RANGED-POSE-MECHANICS.md:288-293`,
citing `src/Hukbo.Client/Rendering/GaitGeometry.cs:84-101`), so a whole battle
line would switch from standing to walking on the spot. That is arguably an
improvement in readability and arguably a shimmer; it is a spectator question,
not a correctness one.

**How to measure whether it worked.** The ten-cell matrix plus
`p50Milliseconds`, and the two must be read together — this is the candidate
most likely to fix termination and fail the performance ceiling in the same run.

---

### F-E. Widen the candidate arc and add an explicit separation candidate

**Mechanism.** Section 4.4 fact 3: the three candidates span 45 degrees, all
aimed at the enemy, so "away from the crowding ally" is not in the set. Add a
fourth and fifth candidate — the unit vector directly away from the nearest
violating ally, and its mirror — and optionally widen the obliques from 22.5
degrees to 45. The oblique constants are one line each:
`ObliqueCosine = 946` and `ObliqueSine = 392` at `ObliqueScale = 1024`
(`MovementRouteRules.cs:34-37`), documented as cosine and sine of 22.5 degrees;
45 degrees at the same scale is 724 and 724.

This is different in kind again: the constraint is unchanged, the search order is
unchanged, only the *set of directions offered* grows.

**Files.** `src/Hukbo.Core/Movement/MovementRouteRules.cs` (a new rotation
helper; the existing constants must not be edited, because V6 and V7 read them),
`src/Hukbo.Core/Simulation/BattleSimulation.cs`
(`BuildEquipmentRouteCandidates`, and the `stackalloc EquipmentRouteCandidate[3]`
at `:2005-2006` grows to 5); new preset member and registry row; tests in
`tests/Hukbo.Core.Tests/Movement/MovementRouteRulesTests.cs`.

**State hash.** **Yes, under the new preset only.**

**Cost per tick at 500 agents.** Five candidates instead of three is a 67%
increase on the dominant term: 1,250,000 iterations per tick against 750,000.
Finding the nearest violating ally is one extra `O(agents)` scan per agent,
250,000 iterations, and it can share the scan that already runs. **Estimate:
+0.6 to +0.8 ms at 500 agents.** Also over budget, though less badly than F-D.

**What it would break.** A warrior that steps sideways away from an ally is
stepping out of the battle line, so the emergent front would loosen. It also
interacts badly with F-B: if the constraint is already monotone, the extra
candidates are mostly redundant. These two should not be built together without
measuring each alone first.

**How to measure whether it worked.** F-A's counters, then the matrix, plus
`maximumFrontWidthRaw` and `maximumFrontDepthRaw`
(`docs/development/testing.md:3251-3252`) to detect the line loosening.

---

### F-F. Change what counts as a decided battle

**Mechanism.** Section 1 establishes that only annihilation decides a battle:
`ResolveOutcome` (`BattleSimulation.cs:3981-4010`) returns a victory only when
one faction has no living agent, and returns `Draw` at the tick limit otherwise
(`:4008`). Add a decisive rule that does not require annihilation — for example,
one faction falling below a fixed fraction of the other's living strength for a
sustained number of ticks, or a survivor-count decision at the tick limit.

This is the only candidate that touches no movement code, and it is included
because it is genuinely different in kind and because a reader should see the
argument against it stated rather than implied.

**Files.** `src/Hukbo.Core/Simulation/BattleOutcome.cs` (possibly a new member),
`src/Hukbo.Core/Simulation/BattleSimulation.cs` (`ResolveOutcome`),
`src/Hukbo.Core/Simulation/BattleSnapshot.cs` if the new state is persisted,
plus every golden expectation in `tests/Hukbo.Core.Tests/`.

**State hash and event hash.** **Yes, and worse than the others.** A new
`BattleOutcome` member changes an enum's numeric surface, which
`CLAUDE.md` section 5 flags explicitly; a sustained-strength counter is new
authoritative state that `StateHasher.Compute` must observe; and the outcome
reaches the event stream. This moves hashes for **every** preset, not just a new
one, unless the whole rule is version-gated.

**Cost per tick at 500 agents.** Two integer comparisons on counts
`ResolveOutcome` already computes. **Estimate: negligible, under 1 microsecond.**

**What it would break — and this is why it ranks last.** Three things.

1. It contradicts a standing decision. `SIMULATION-GAME-STANDARDS.md:560-562`:
   "No separate anti-stall or fairness escape rule is added… `TickLimit` remains
   the terminal backstop." Overturning that is a decision record, not an
   implementation detail.
2. **It would decide battles on noise.** Every measured V6 draw finishes within
   nine warriors on 200 agents and within five on 500 (section 2.2, and
   `2026-07-31-movement-v7-baseline.md:300-303`). A survivor-count tiebreak on a
   137-versus-142 finish is a coin flip dressed as a result.
3. It hides the defect rather than removing it. The warriors would still be
   standing still for 73% of the battle; the spectator would still see a
   stalemate; only the scoreboard would change. That fails the
   `SIMULATION-GAME-STANDARDS.md` section 10 question `CLAUDE.md` section 6
   makes mandatory: can a spectator discover this effect without reading source
   code?

**How to measure whether it worked.** It would pass the section 1 criterion by
construction, which is precisely the problem. There is no honest measurement
that distinguishes "the battle was decided" from "the tiebreak fired".

---

### The ranking

| Rank | Candidate | Kind of change | Fixes the root cause? | Affordable? |
| --- | --- | --- | --- | --- |
| 0 | **F-A** instrument the refusal reason | observability | no, prerequisite | yes, free |
| 1 | **F-B** monotone clearance constraint | shape of the constraint | yes, directly | yes, +0.1 ms |
| 2 | **F-C** reconcile clearance with contact distance | value, plus a structural invariant | yes, directly | yes, neutral |
| 3 | **F-E** widen the candidate arc | candidate set | partially | marginal, +0.7 ms |
| 4 | **F-D** never-refuse truncation ladder | search strategy | yes, but | no, not at full depth |
| 5 | **F-F** change the decisive-outcome rule | definition of termination | no, masks it | yes, but forbidden |

**I would do F-A first, then F-B.**

F-A first because it costs nothing, moves no hash, and converts section 4.6's
ranking from a source-reading argument into a measured one. The entire V7
workstream — sixteen tasks, eleven gate runs, two measurement sessions — ended by
naming four suspects and declining to choose between them
(`2026-07-31-movement-v7-calibration-record.md:585-591`). Four counters would
have decided that question in an afternoon. Building the next fix without them
risks repeating exactly that outcome.

F-B second because it is the smallest change that removes the absorbing state.
It touches one method, it reuses a comparison pattern already present twelve
lines away in the same file (`ShouldOmitDirectCandidate`, `:2416`), it does not
delete the weapon-clearance differentiation the six research sessions produced,
its cost is the only one of the four movement candidates that fits inside a
stage already 3.81x over budget, and its failure mode is legible: if F-A's
lane-clear counter does not collapse after F-B lands, the diagnosis in section
4.3 was wrong and the next candidate is F-C.

F-C is the right second move if F-B alone is insufficient, and its part two — the
construction-time invariant — is worth landing regardless of which fix wins,
because it is what stops a future preset re-registering a clearance radius the
collision contract cannot honour.

---

## 7. Interaction with ranged units

### 7.1 What the ranged package will actually change

Attack reach is already per-weapon. `CreateAgent`
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:857-891`) reads
`profile.AttackRangeRaw` off the resolved `WeaponProfile` and stores it on
`AgentState.AttackRangeRaw` (`src/Hukbo.Core/Simulation/AgentState.cs:55, 82`),
falling back to the scenario-wide value only for a preset with no weapon
profiles. Preset V2's registered reaches are 16 world units for the Kampilan, 13
for the Wasay and the solo Kalis, 12 for the shielded Kalis, 11 for the solo
Itak, and 10 for the shielded Itak
(`src/Hukbo.Core/Combat/PhilippineCombatPresetV2.cs:135, 145, 155, 163, 173, 181`).
The single reach test is `IsWithinAttackRange`
(`BattleSimulation.cs:4132-4139`), centre to centre, squared, against that
per-agent value.

So a bow, a blowgun, a thrown spear, or an arquebus needs no new reach
machinery. It needs a large `AttackRangeRaw` on its weapon profile, and
everything downstream already works.

**What does not already work is stopping.**
`SIMULATION-GAME-STANDARDS.md:471-483` is explicit: an advancing agent's
movement target is "body contact at one body diameter, currently 8.5 world
units, not attack range at twelve. An agent already inside reach keeps walking
in." Under the shipped default preset, a bowman would walk to body contact with
its target and shoot from there. Under `EquipmentRelativeFootworkV6` the
situation is no better: the profile carries a `preferredDistanceBasisPoints`
(10,000 to 13,000 across the six rows, section 4.2), but the candidate builder's
own comment at `BattleSimulation.cs:2116-2118` says "The preferred distance is
not a stop line: both phases continue toward the target's centre so the existing
post-movement reach test stays authoritative."

**Neither shipped movement preset has a stand-off distance.** That is a required
new rule for the ranged package independent of anything in this note, and it is
the single largest interaction between the two workstreams.

### 7.2 Do ranged units make the standoff better, worse, or unchanged?

The honest answer is different for the two presets, and the difference matters.

**Under the shipped default `PersistentContingentsV4`: better, and probably
noticeably so.** V4 has no refusal mechanism at all — `FootworkPhase` is not
resolved under that preset, because `ResolveEquipmentPosturesAndProvisionalFootwork`
runs only under `UsesEquipmentRelativeFootwork` (`BattleSimulation.cs:619-625`).
Warriors close to body contact and fight. Giving some of them a reach of, say,
60 world units instead of 12 raises `attackCapableAgentTicks` for those agents
enormously, because attack gathering reads resolved positions and nothing else.
More attacks per tick means shorter battles, and V4 already decides in 1,279 to
4,405 ticks. Note that `CLAUDE.md` section 9 forbids projectile ammunition
before an authorizing gate, so the default assumption is unlimited fire, which
is the maximally-shortening case. **Estimate, not a measurement:** the risk under
V4 is battles ending *too fast*, not too slow.

**Under `EquipmentRelativeFootworkV6` or `V7`: worse, in the specific and
dangerous sense that ranged units would partially conceal the defect without
removing it.**

The chain is this. Step 8 of the footwork ladder yields `Engage` when
`hasTarget && targetAtOrInsidePreferredDistance`
(`src/Hukbo.Core/Movement/WeaponMovementRules.cs:653-656`), and preferred
distance is `attackRangeRaw * (PreferredDistanceBasisPoints + offset) / 10000`
(`src/Hukbo.Core/Movement/MovementRouteRules.cs:280-290`). A ranged weapon with
a large `AttackRangeRaw` makes that predicate true from a very long way off. So
a ranged warrior would sit in `Engage` for almost the whole battle rather than
`Approach`.

`Engage` is one of the three refusable phases
(`WeaponMovementRules.cs:700-703`). So a ranged warrior would be *more* exposed
to the lane-clearance trap of section 4, not less.

But — and this is the trap — **a refusing warrior can still attack.** Section
4.2 stage H: `GatherAndCommitAttacks` reads no footwork phase, only resolved
position and cooldown. A ranged warrior frozen in `Refuse` at 60 world units
from the enemy line is inside its own reach and will shoot every time its
cooldown clears.

The result is a battle in which two lines stand almost perfectly still for
10,000 ticks and exchange projectiles. Deaths would rise, possibly enough to
decide some cells. **And it would look intentional.** A spectator watching two
armies hold position and shoot at each other cannot tell that 73% of those
warriors are standing still because their movement proposals were rejected. The
defect stops being visible without stopping being present, and the melee
warriors in the same battle remain exactly as frozen as they are today.

That is the honest verdict: **ranged units make the measured symptom better and
the underlying defect harder to find.** For a project whose feature-acceptance
question is "can a spectator discover this effect without reading source code?"
(`CLAUDE.md` section 6, citing `SIMULATION-GAME-STANDARDS.md` section 10), that
is the worse of the two failure modes.

### 7.3 A skirmisher refusing to commit is not the same bug — and today nothing can tell them apart

The brief is right to insist on this distinction, and the current code cannot
draw it.

Both cases present identically to every observer the simulation has:

| | A ranged skirmisher correctly holding distance | A melee warrior trapped by the lane-clearance rule |
| --- | --- | --- |
| Position over time | unchanged | unchanged |
| `AgentIntent` | `Moving` (never `Attacking`, since it is not at contact) | `Moving` |
| `FootworkPhase` | would be `Engage` if a route survived, `Refuse` if not | `Refuse` |
| Movement proposal | none, or one it does not want | none |
| `MovementBehaviorMetrics` | `refuseAgentTicks` or `engageAgentTicks` | `refuseAgentTicks` |
| Inspector | shows the phase | shows the phase |

Because preferred distance is not a stop line (section 7.1), a skirmisher that
*wants* to hold has no way to express it: it either walks in, or it is refused.
So today a correctly-behaving skirmisher and a bugged melee warrior are
literally the same state.

Two things are needed to separate them, and neither is a tuning value:

1. **An explicit hold arm in the route chain.** `GatherOneEquipmentProposal`
   already has the right shape — the body-contact `Attacking` hold at
   `BattleSimulation.cs:1876-1882` proposes no movement and forces
   `phaseSurvives = true`, so the agent stands still *without* being marked
   refused. A ranged hold would be the same arm with a different predicate:
   at or inside the weapon's stand-off band, propose nothing, keep the phase.
2. **F-A's rejection-reason counters.** "Held by choice" and "refused by the
   lane test" have to be distinguishable in the report, or no measurement can
   tell a working skirmisher from a broken warrior. This is a second,
   independent argument for building F-A first.

### 7.4 Which candidate fixes conflict with a stand-off warrior

| Candidate | Conflict with a warrior whose correct behaviour is to hold distance |
| --- | --- |
| **F-A** instrument | **None.** It is the thing that makes the distinction measurable at all. |
| **F-B** monotone constraint | **None.** It only permits movement that was previously forbidden; a holding warrior simply does not use the permission. It does not, however, *give* a skirmisher a way to hold — that is the separate rule in 7.3. |
| **F-C** reconcile clearance with contact distance | **Real, and worth naming.** A ranged warrior plausibly wants *more* lateral spacing than a melee one, to keep a clear shooting lane past its own front rank. F-C caps every row's ally clearance at the body diameter, which flattens exactly the axis a ranged loadout would most want to differentiate on. If F-C ships, the ranged rows need a different differentiation axis, or the cap needs a documented ranged exception with its own invariant. |
| **F-D** never-refuse ladder | **Direct conflict.** "Always take the longest legal step" is precisely the wrong rule for a skirmisher at ideal range. F-D would need the hold arm from 7.3 to run *before* the ladder, or a bowman would creep forward one raw unit per tick into the melee it exists to avoid. |
| **F-E** widen the candidate arc | **None, and mildly helpful.** A lateral candidate is what a skirmisher needs to reposition without closing. |
| **F-F** change the decisive-outcome rule | **The worst combination in the document.** A ranged stalemate is the most likely thing to produce two near-equal survivor counts at the tick limit, and F-F would score that as a victory. The fix that hides the bug, applied to the feature most likely to produce it. |

### 7.5 The recommendation for the ranged package

Do not treat termination as a sub-task of the ranged work, and do not let ranged
units be the thing that makes the numbers look better. In order:

1. **F-A**, because it is free, moves no hash, and is a prerequisite for both
   workstreams.
2. **The stand-off hold arm from 7.3**, because the ranged package needs it
   regardless and it is what makes "chose not to close" a first-class state
   rather than an indistinguishable one.
3. **F-B**, measured on melee-only rosters first, so its effect is not confounded
   with the ranged change.
4. Only then measure a mixed roster.

---

## 8. The measurement harness

### 8.1 What exists today

Three things, in decreasing order of usefulness for this question. A fourth —
the automated termination test that is already in the canonical gate — is
treated separately in 8.2, because what it does not cover is itself a finding.

**1. `./scripts/benchmark.ps1` — the supported entry point, and the tool that
produced every number in section 2.** Its parameters are declared at
`scripts/benchmark.ps1:1-35`: `-Agents` (default 200), `-Ticks` (default 10000),
`-Seed` (default 1), `-Output`, `-LogLevel`, `-LogChannels`, `-LogDirectory`,
`-Preset` (a `CombatPresetId`), `-MovementPreset` (a `MovementPresetId`), and
`-NoBuild`. It restores, builds `src/Hukbo.Headless` in Release, and runs it
(`:44-60`).

**`-MovementPreset` is the important one, and it is easy to miss.** The comment
at `scripts/benchmark.ps1:28-31` records that it is "Passed straight through to
the headless runner's `--movement-preset` argument as a `MovementPresetId`
member name (for example `IndependentPursuitV1`) or its numeric value." So the
V6 standoff is reproducible from a supported script with no test-project
involvement at all. There is **no** roster-composition flag — `grep -in roster
src/Hukbo.Headless/HeadlessRunner.cs` returns nothing — which is the real
limitation, and it is what forced the calibration harness into the test project.

**2. `src/Hukbo.Headless/HeadlessRunner.cs` — the runner.** Its usage string is
at `:39-46`:

```
Usage: --agents <positive-even-count> --ticks <positive-count> --seed <unsigned-integer>
       [--output <json-path>] [--log-level off|err|warn|inf|dbg|trc]
       [--log-channels all|<comma-separated>] [--log-dir <directory>]
       [--preset <CombatPresetId name or number>]
       [--movement-preset <MovementPresetId name or number>]
```

Exit codes, read from the source rather than from documentation: **2** on an
argument error (`:47`), **1** on an unhandled exception (`:93`), **3** when the
run completed but `report.Deterministic` was false, and **0** otherwise — both
from the single expression `return report.Deterministic ? 0 : 3;` at `:88`.

It emits one `RunReport` as camel-cased indented JSON on standard output
(`:63-71`), and additionally to `--output` when given (`:73-86`). The fields
this investigation needs are `measuredTicks`, `outcome`, `faction0Survivors`,
`faction1Survivors`, `collisionMetrics`, `combatMetrics`, and `movementMetrics`
— all visible in the verbatim report at
`2026-07-31-movement-v7-baseline.md:470-532`. `movementMetrics` is all zeroes for
any preset that does not use equipment-relative footwork
(`2026-07-31-movement-v7-baseline.md:317-321`), which is why the V4 rows in
section 2.2 carry no phase counters.

**3. `tests/Hukbo.Core.Tests/Movement/PressureInterruptCalibrationHarness.cs` —
894 lines, hand-run, not a test.** Its own header
(`PressureInterruptCalibrationHarness.cs:12-58`) records four facts worth
carrying:

- It runs the ten-cell matrix — seeds `{1, 2, 3, 5, 8}` at 200 and 500 agents
  (`:63-69`), 10,000 requested ticks (`:75`) — and reports terminal tick,
  outcome, both survivor counts, `p50`, a phase-flip percentage over ticks 101
  to 400, and per-row interrupt firing counts.
- **It measures; it does not tune and it does not assert** (`:22-28`). Nothing
  in it passes or fails; it prints a block for a person to read.
- **It is not in the gate.** Its only entry point is behind
  `#if HUKBO_CALIBRATION`, "which no ordinary build, no script in `scripts/`,
  and no gate stage defines" (`:31-41`).
- **It lives in the test project because it has to** (`:43-49`): "the headless
  runner exposes no movement-preset selection to a caller in process, and the
  state this harness reads is `internal` to `Hukbo.Core`, which only the test
  assembly may see." It also reaches `BattleSimulation._pressureInterruptFired`
  by reflection (`:50-58`), and throws by name if that field is renamed.

Note the precise scope of that second reason. The headless runner does expose
`--movement-preset` on the *command line*; what it does not expose is
programmatic selection to an in-process caller, plus access to `internal` state.
For reproducing terminal ticks and outcomes, the command line is enough.

### 8.2 The automated termination test that does exist, and its blind spot

**4. `tests/Hukbo.Core.Tests/BattleSimulationTests.cs:566` —
`SeedsOneThroughTwentyProduceVictoriesForBothFactions`.** This is a real `[Fact]`
in the canonical gate, and it encodes the section 1 criterion almost exactly. It
loops seeds 1 through 20 (`:577`), builds `Scenario.CreateDefault(seed,
totalAgents: 200)` (`:579`), advances to the tick limit (`:585-589`), and
asserts three clauses:

| Clause | Constant | Assertion |
| --- | --- | --- |
| Each faction wins at least 4 seeds | `MinimumVictoriesPerFaction = 4` (`:571`) | `:615-620` |
| At least 19 of 20 seeds decided | `MinimumDecisiveSeeds = 19` (`:569`) | `:622-625` |
| Median decisive tick at or below 5,000 | `MedianDecisiveTickLimit = 5_000` (`:570`) | `:629-634` |

Its own failure message already points at this investigation's conclusion:
"Interception multiplies a stall rather than causing one, so examine the attack
rate and the damage per landed blow before the clash tables" (`:632-634`), and
the remark above it at `:560-563` says the same. Section 5 is the arithmetic
that confirms that instruction was right.

**Its blind spot is the whole problem.** It calls `Scenario.CreateDefault`, so
it measures `PersistentContingentsV4` and nothing else. `EquipmentRelativeFootworkV6`
and `V7` are registered, reachable, and completely unguarded by it. A preset can
draw all twenty seeds and the gate stays green.

**A second, smaller defect worth recording.** The `decisiveTicks` list is
appended for `Faction0Victory`, `Faction1Victory`, **and `Draw`** (`:607-612`),
so a drawn seed counts toward the "at least 19 of 20 decided" clause. Twenty
draws would satisfy that clause outright. The test still catches a total
standoff, because the per-faction-victory clause would read 0 against a required
4 and the median would read 10,000 against a required 5,000 — but the clause
that reads as the termination check is not the one doing the work, and its
failure message would be misleading.

### 8.3 What does not exist

- No termination test over any preset other than the shipped default. Extending
  `SeedsOneThroughTwentyProduceVictoriesForBothFactions` to take a
  `MovementPresetId` would fail immediately on V6 and V7, which is a decision
  about what the gate is allowed to know, not an oversight to fix silently.
- No twenty-seed *recorded run* for any movement preset (section 2.5). The test
  above runs twenty seeds but records nothing; every archived matrix is five
  seeds.
- No per-reason breakdown of refusals (candidate F-A).
- No roster-composition flag on the headless runner, so a melee-only or
  ranged-only measurement cannot be requested from a script today.

### 8.4 What a person would type to reproduce the baseline

From the repository root, in PowerShell 7. Each command prints one `RunReport`
as JSON.

The single most informative run — the cell every number in sections 2, 4, and 5
comes from:

```powershell
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1 `
  -MovementPreset EquipmentRelativeFootworkV6 `
  -Output artifacts/standoff/v6-200-seed1.json
```

Expect `measuredTicks: 10000`, `outcome: "Draw"`, `faction0Survivors: 78`,
`faction1Survivors: 73`, `stateHash: "66320AD76023759B"`,
`eventHash: "2531D81886469344"`, and the `movementMetrics` block of section 2.3.

The terminating control, same seed and size:

```powershell
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1 `
  -MovementPreset PersistentContingentsV4 `
  -Output artifacts/standoff/v4-200-seed1.json
```

Expect `measuredTicks: 1279`, `outcome: "Faction0Victory"`,
`stateHash: "2410DD94F26C82E2"`, `eventHash: "56F66BBC10E69F0E"`. Omitting
`-MovementPreset` entirely gives the same run, because V4 is the shipped
default.

The full twenty-cell matrix of section 2.2 — both presets, both sizes, five
seeds. Add `-NoBuild` after the first invocation so the build runs once:

```powershell
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path artifacts/standoff | Out-Null
$first = $true
foreach ($preset in 'PersistentContingentsV4', 'EquipmentRelativeFootworkV6') {
  foreach ($agents in 200, 500) {
    foreach ($seed in 1, 2, 3, 5, 8) {
      $out = "artifacts/standoff/$preset-$agents-seed$seed.json"
      if ($first) {
        ./scripts/benchmark.ps1 -Agents $agents -Ticks 10000 -Seed $seed `
          -MovementPreset $preset -Output $out | Out-Null
        $first = $false
      } else {
        ./scripts/benchmark.ps1 -Agents $agents -Ticks 10000 -Seed $seed `
          -MovementPreset $preset -Output $out -NoBuild | Out-Null
      }
      $r = Get-Content $out | ConvertFrom-Json
      '{0,-32} {1,5} seed {2}  ticks={3,5}  {4}' -f `
        $preset, $agents, $seed, $r.measuredTicks, $r.outcome
    }
  }
}
```

The V7 cells are the same commands with
`-MovementPreset EquipmentRelativeFootworkV7`.

Extracting the phase counters and the 349:1 ratio from a saved report:

```powershell
$r = Get-Content artifacts/standoff/EquipmentRelativeFootworkV6-200-seed1.json |
  ConvertFrom-Json
$m = $r.movementMetrics
$m
'refuse+regroup / commit+recover = {0:N2}' -f `
  (($m.refuseAgentTicks + $m.regroupAgentTicks) /
   ($m.commitAgentTicks + $m.recoverAgentTicks))
'route-search failure rate >= {0:P2}' -f `
  ($m.refuseAgentTicks /
   ($m.refuseAgentTicks + $m.approachAgentTicks +
    $m.engageAgentTicks + $m.pursueAgentTicks))
```

Against the recorded seed-1 200-agent cell these print `349.36` and `95.61%`,
which are the two headline numbers of sections 2.3 and 4.5.

The V7 calibration harness, if per-row interrupt firing counts are ever wanted
again. This is the command recorded at `AGENT-BACKLOG.md:250-254` (historical),
and a full ten-cell matrix takes about two minutes:

```powershell
dotnet test tests/Hukbo.Core.Tests -c Release `
  -p:DefineConstants=HUKBO_CALIBRATION `
  --filter FullyQualifiedName~PressureInterruptCalibrationRun `
  --logger "console;verbosity=detailed"
```

Finally, the canonical gate is unaffected by any of the above and remains the
only integration evidence:

```powershell
./scripts/verify.ps1
```

**Nothing in this note has been run.** Every figure quoted is either read from a
source file at this worktree's commit or recovered from a recorded measurement,
and every derived figure is labelled an estimate. Per `CLAUDE.md` section 5 and
the verification-honesty rule, the commands above are reproduction instructions,
not evidence that they were executed in this session.
