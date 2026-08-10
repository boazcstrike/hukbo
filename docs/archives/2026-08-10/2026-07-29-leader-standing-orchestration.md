# Prompt — Leaders, standing, and research-backed army composition

> **Archived: reference only.** Finished work, kept so a past decision can be
> traced to its reasoning. Never execute it, never treat it as current, and never
> cite it as justification for a change. The live contract is `CLAUDE.md`,
> `SIMULATION-GAME-STANDARDS.md`, `docs/development/testing.md`, and `docs/plans/`.

Date: 2026-07-29
Hand this file to the agent verbatim, or paste the body below the horizontal
rule. It is written to be self-contained: an agent that reads only this prompt
and the files it names has everything it needs.

---

## Task

Prepare `Hukbo.Core` to field a research-backed army composition, and build the
leadership layer first. The end state of this piece of work is that a battle is
fought by **named social standings under identified leaders**, not by an
undifferentiated pile of warriors, and that every standing carries its own
attributes rather than being a cosmetic label.

You are running this through the repository's agent orchestration pipeline. Do
not do it as one long single-threaded edit session.

## Before anything else: this work is already partly designed

Three uncommitted documents in this checkout already cover most of the ground.
**Read all three before you plan anything.** Restarting from scratch, or
producing a second parallel design, is a failure of this task.

| File | What it already settles |
| --- | --- |
| `docs/research/HISTORICAL_1500s_RANKS.md` | The rank/standing evidence base, confidence tiers, cleared and excluded terms |
| `docs/archives/2026-08-07/2026-07-29-warrior-standing-design.md` | The design: `StandingId`, the five-value ladder, why standing is not a damage multiplier, determinism impact, nine acceptance answers, four open questions |
| the warrior standing plan | The task plan: Phase A (A1-A13), Phase B (B1-B3), and an explicit exclusion list |

A fourth document is **new** and has not been folded into that design yet:

| File | What it adds that the design does not have |
| --- | --- |
| `docs/research/ARMY-COMPOSITION.md` | Force sizes from primary sources, the coalition structure of a force, contingent count and size, the boat as an organizational unit, reward structure, and an explicit list of what the sources do not establish |

Your first real job is to reconcile these: the standing design describes **what
a warrior is**, and the army-composition research describes **how warriors are
grouped and who they follow**. The second has not been designed against yet.

### The four open questions block everything

`docs/archives/2026-08-07/2026-07-29-warrior-standing-design.md` §10 lists four questions the
user must answer before implementation starts, and task A1 of the plan exists to
record those answers. Ask them, get answers, record them. Do not guess. A task
that finds itself guessing has hit a missing decision and must stop.

Carry a **fifth** question forward from this prompt, described in the next
section.

## What this prompt adds to the existing design

### 1. Leaders, explicitly

The existing design reaches leadership only in Phase B, through a
standing-aware contingent leader scan, and Phase B is gated on open question 3.
This task makes leadership the **first-class deliverable**, not the optional
tail.

Evidence anchor, from `docs/research/ARMY-COMPOSITION.md` §2 and §7:

- A force was a coalition of independently commanded followings. There was no
  single commander. Rajah Sulayman, quoted 1572: "there is no king and no sole
  authority in this land."
- The one attested rule resembling command is sponsorship: whichever chief
  offered the *magaanito* took half the booty. Sponsorship bought share, not
  obedience.
- Followers could leave a leader who failed them. Loarca records timaguas as
  "free to pass from the service of one chief to that of another."

So a leader in Hukbo is **the head of one contingent**, and a faction has
several of them. There is no army commander, and you must not add one.

### 2. Per-standing attributes

The user wants each standing to carry its own attributes. The existing design is
explicit (§3) that standing must **not** become a raw damage or hit-point
multiplier, because no sixteenth-century source grades fighting ability by
social class, and it routes standing through equipment, level, and leadership
instead.

Those two things are compatible, and the resolution is the fifth open question
you must put to the user:

> **Open question 5.** Per-standing attributes: express them the way the design
> already does — loadout eligibility, level and therefore combo depth,
> leadership eligibility, and how many followers a standing can hold — or add a
> direct combat-strength difference per standing as an admitted gameplay
> invention?

State the recommendation with the question: **the first option**, because it
gives visibly different warriors without claiming a historical fact the sources
deny. If the user picks the second, that is their call — implement it in full,
and require the code comment and the inspector to mark it as a provisional
gameplay value, exactly as the tall-hardwood shield multiplier already is.

### 3. Contingent shape from evidence, not from a square root

`FormationPlanner.ResolveContingentSizes` currently splits a faction into
`clamp(isqrt(warriorCount) / 2, 1, MaximumContingents)` **equal** contingents,
remainder to the earliest. That is a lattice-packing convenience with no
historical content.

`docs/research/ARMY-COMPOSITION.md` §11.1 sets out the evidence-backed
alternative: contingent count is set by **how many chiefs joined**, not by total
headcount, and contingent sizes are **unequal**, because barangays ran from
under thirty to a hundred houses. Recorded engagements support a few hundred per
committed leader, with thousand-plus figures being coalitions.

This is a Phase C candidate. Design it; do not assume it ships in the same pass
as standing.

## Hard constraints

These are not negotiable and every sub-agent prompt you write must carry the
ones relevant to its scope.

**Determinism.** Anything that reaches the state hash needs a new preset
version plus new golden expectations. The standing work is already scoped to a
new `CombatPresetId.PrecolonialPhilippinesV4`; leader selection is already
scoped to a new `MovementPresetId.PersistentContingentsV5`. Existing presets
stay registered and byte-identical so their replays still reproduce. Load the
`hukbo-determinism-change` skill before touching simulation code. Never edit the
pinned SplitMix64 vectors. `System.Random` is banned.

**Core boundary.** `Hukbo.Core` may not reference MonoGame, the filesystem, the
network, the wall clock, or `Hukbo.Diagnostics`. No campaign, economy,
diplomacy, or polity state goes into it. The booty, ransom, and reward material
in `ARMY-COMPOSITION.md` §7 is genuinely interesting and belongs to the future
campaign layer that consumes `BattleOutcome` — it does not go in the battle
core. `Hukbo.Client` may not decide targeting, damage, retreat, or victory.

**Deferred features.** `CLAUDE.md` §9 defers morale, rout, terrain,
pathfinding, and persistence until the gate authorizes them. A leader whose loss
degrades a contingent's cohesion is an **extension of the existing
`ContingentState` machinery**, which already has a `Break` value meaning the
group has stopped acting as one. That is in scope. Introducing a morale value,
a fear stat, or a rout state is not. If the design starts to need one, stop and
say so rather than smuggling it in under another name.

**Historical accuracy policy.** `AGENTS.md` §7 binds. Every term gets an
evidence tier in metadata. Player-facing cultural identifications appear only in
pair form — Filipino name, em dash, plain English descriptor. Do not invent
military ranks: the sources establish no rank below chief, and
`ARMY-COMPOSITION.md` §10 lists this explicitly. No sergeant, captain,
lieutenant, squad, company, or regiment, in code or in UI. Do not present
Tagalog *maharlika* and Visayan *timawa* as two grades of one ladder; the
existing design already handles this and its reasoning must survive.

**Naming.** `Hukbo` and `Hukbo.*` only. Never reintroduce `AutonomousArena`.
The existing design chose **standing** for type names and reserves "Rank" for
the player-facing inspector line, because `ContingentState`'s documentation
already uses "rank" in the formation sense. Keep that decision unless the user
overturns it under open question 1.

**Logging.** Any new instrumentation goes through
`Hukbo.Diagnostics.DiagnosticLog` with a `const` event identifier on
`LogEvents`. Never `Console.Write*`. Per-tick lines are `trc`. A disabled call
allocates nothing. `Hukbo.Core` never references `Hukbo.Diagnostics`.

**Discovery.** Code discovery goes through `tokensave` MCP tools first, never
an Explore agent. This applies inside sub-agent prompts as well as outside them.

**Documentation.** Written in full, normal English. Never run a compression
pass over repository documentation. Design goes in
`docs/plans/YYYY-MM-DD-<slug>-design.md`, task list in
`docs/plans/YYYY-MM-DD-<slug>.md`.

**Verification.** `./scripts/verify.ps1` runs once, after integration, by you,
and the real output is the evidence. No sub-agent report substitutes for it. No
agent may flip a manual smoke-checklist row. There is no CI and none is to be
proposed.

## How to run it: orchestration

Invoke the `hukbo-orchestrate` skill and follow it. Do not restate the pipeline
from memory and do not use the generic `/flow:*` commands — inside this
repository they do not know about worktree isolation, the `tokensave`-only
discovery rule, or the canonical gate.

The shape, with the specific assignments for this task:

### Stage 1 — research, in parallel, read-only

Two groups, run at the same time, at most eight agents total across the whole
fan-out. Research agents write no files; a research agent that wants an edit
produces a task, not a diff.

**Group A — requirements and evidence.** What the change has to do, and what
the sources allow.

- Reconcile `docs/research/ARMY-COMPOSITION.md` against
  `docs/research/HISTORICAL_1500s_RANKS.md`. Report every place they agree,
  every place they differ, and every claim one makes that the other would rate
  at a different confidence tier. Return a table: claim, RANKS tier,
  ARMY-COMPOSITION tier, resolution.
- Extract the leadership requirement. From `ARMY-COMPOSITION.md` §2, §4, §7,
  and §11, list every attested obligation running between a leader and a
  follower, with the source quote and tier for each. Return a table: obligation,
  direction, source, tier, whether it is simulable inside a battle.
- Audit the four open questions in the existing design plus open question 5 from
  this prompt. For each, state what the evidence constrains and what is a free
  design choice. Return a numbered list; do not answer them.

**Group B — existing code.** What the repository already does.

- Map the standing surface named in the warrior standing plan
  tasks A2-A8: `CombatIdentity.cs`, `CombatLoadout`, `AgentState`, `AgentView`,
  `CombatRuleset`, the content hash, `StateHasher`, and the preset registries.
  Return file, symbol, line, and what each task would have to change.
- Map the contingent and leadership surface: `FormationPlanner`,
  `ContingentState`, `ContingentOffset`, `RallyOffset`, `FormationRules`, and
  every movement preset gate that reads a contingent. Return file, symbol, line,
  and which of them a leader concept would have to touch.
- Map the presentation surface: `AgentInspectorPanel`, `AgentInspectorContent`,
  `ArmyCompositionPanel` and its parts, `BattleEventLogPanel`, and the theme
  roles. Return file, symbol, line, and where a standing line and a leader
  marker would go.
- Map the test surface. Which existing tests assert contingent sizing, roster
  resolution, hash stability, or golden expectations, and which of them a new
  preset version would move. Return file, test name, and expected effect.

Every prompt in this stage names the files, the symbols, and the exact shape of
the answer expected back. An agent that returns prose costs more than it saves.

### Stage 2 — planning, one agent

A single planner reads both research groups and produces one ordered task list.
It writes the design and plan documents. It writes no code.

The planner's specific job here is **not** to rewrite
`docs/archives/2026-08-07/2026-07-29-warrior-standing-design.md`. It is to:

1. Amend that design with a Decisions section recording the five answered
   questions (this is existing task A1).
2. Amend it with the leadership-first resequencing: what moves out of Phase B
   into Phase A, and what the per-standing attribute set is once question 5 is
   answered.
3. Write a **new** design document for the contingent-shape work described in
   §3 of this prompt, as Phase C, with its own determinism section and its own
   answers to the nine acceptance questions in
   `SIMULATION-GAME-STANDARDS.md` §10 — including the one that decides whether
   the feature is finished: *can a spectator discover this effect without
   reading the source?*
4. Produce the merged task list with dependencies and non-overlapping file
   ownership named per task.

Audit the planner's output before implementation starts. Check that every task
names its files, that no two tasks own the same file, that determinism-touching
tasks name their preset version and their goldens, and that nothing on the
deferred list has crept in.

### Stage 3 — implementation, scoped

Only this stage writes code. Give each implementer an explicit, non-overlapping
set of files. Two agents editing one file in parallel is a merge conflict you
created on purpose.

Suggested split, to be confirmed against the planner's actual task list:

- **Core identity and state** — `CombatIdentity.cs`, `CombatRuleset.cs`,
  `AgentState.cs`, `AgentView.cs`
- **Hashing and presets** — the content hash, `StateHasher.cs`, the preset
  registries and the new preset files
- **Formation and leadership** — `FormationPlanner.cs`, `FormationRules.cs`,
  and the movement preset that reads a leader
- **Client presentation** — the inspector, the composition panel, the event log
- **Tests and goldens** — the test projects only

Run the gate yourself after integration. Paste the real output.

## Definition of done

- The five open questions are answered by the user and recorded in the design
  document.
- `docs/research/ARMY-COMPOSITION.md` and `docs/research/HISTORICAL_1500s_RANKS.md`
  are cross-referenced from each other and from whatever design documents cite
  them, with any tier disagreements resolved in writing.
- Standing exists on the warrior, is authoritative, is in both hashes, and is
  visible in the inspector in pair form with its evidence tier.
- Every contingent has an identified leader, chosen deterministically, and the
  spectator can tell which warrior it is without reading source.
- Each standing carries its own attributes, in whichever form question 5
  settled on, and the inspector shows them.
- The contingent-shape work is designed as Phase C with its own document, and
  it is **not** implemented in the same pass unless the plan says it is.
- `./scripts/verify.ps1` output is pasted, in full, by you.
- No smoke-checklist row is flipped to PASS by any agent. Rows that were not
  exercised stay `PENDING`; anything blocked is reported as `BLOCKED` honestly.

## What to do if you disagree with the scope

Say so in a sentence or two, then build it anyway under stated assumptions. If
a constraint above turns out to be genuinely impossible rather than merely
inconvenient, stop on that one item, finish everything else in full, and report
exactly what you left out and why. Scaling this work down is the user's call,
not yours.
