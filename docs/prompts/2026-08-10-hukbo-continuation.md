# Continuation prompt — Hukbo, 2026-08-10

Paste everything below the line into a fresh agent session started in
`C:\Users\boazs\webdev\autonomous-arena`.

This document lives in `docs/prompts/` because the user asked for it by name.
The `handoff` skill would normally place it in `docs/plans/`; that rule is
overridden by an explicit path, exactly as `CLAUDE.md` section 6 allows.

---

## Goal

The ranged-units package is finished as a build effort and merged. What remains
is not code: three items need a person at an interactive desktop, one needs a
design decision from the user, and one is a designed feature parked in the
backlog. The previous session re-established the verification baseline from
scratch for both games, confirmed that RU-31's sound generation had in fact been
run, and then cleaned up the plan and documentation tree so that a future session
can tell live work from finished work without opening seventeen files.

Your job is to pick up from a verified baseline, not to re-derive it. Read this
document, then confirm the state on disk before doing anything else.

## State on disk

Branch `main`, at `57760ea`. Not a worktree — the work is in the primary
checkout at `C:\Users\boazs\webdev\autonomous-arena`.

```
57760ea docs: record tasks 89, 88 and 90, and correct the figures they moved
0c20186 Merge branch 'sandata-t90-resume'
e22a542 fix: recompute a published group path on resume instead of losing it
0fb1310 Merge branch 'sandata-t88-allocators'
c1acbc7 perf: give stage 5 caller-owned scratch for line of sight and contact memory
9daa271 Merge branch 'ranged-units'
```

`9daa271` is the ranged-units merge. The tag `pre-main-merge-2026-08-09` marks
the pre-merge state if a rollback is ever needed.

**The working tree is not clean, and its contents come from two different
sessions.** This is the single most important fact in this document.

```
 M SIMULATION-GAME-STANDARDS.md
 M docs/archives/2026-08-07/2026-07-29-leader-rank-design.md
 M docs/archives/2026-08-07/2026-07-30-tall-hardwood-shield-movement-report.md
 M docs/archives/2026-08-07/2026-07-30-weapon-movement-foundation-report.md
RM docs/prompts/2026-07-29-leader-standing-orchestration.md -> docs/archives/2026-08-10/2026-07-29-leader-standing-orchestration.md
RM docs/prompts/2026-07-30-weapon-movement-foundation.md -> docs/archives/2026-08-10/2026-07-30-weapon-movement-foundation.md
RM docs/prompts/2026-07-30-weapon-movement-weapon-template.md -> docs/archives/2026-08-10/2026-07-30-weapon-movement-weapon-template.md
RM docs/plans/2026-08-07-movement-gait-animation.md -> docs/archives/2026-08-10/2026-08-07-movement-gait-animation.md
RM docs/plans/2026-08-07-sandata-continuation-prompt.md -> docs/archives/2026-08-10/2026-08-07-sandata-continuation-prompt.md
RM docs/plans/2026-08-08-attack-animation-v2.md -> docs/archives/2026-08-10/2026-08-08-attack-animation-v2.md
RM docs/plans/2026-08-08-ranged-units-handoff.md -> docs/archives/2026-08-10/2026-08-08-ranged-units-handoff.md
RM docs/agents/ATTACK_ANIMATION_V2_CONTINUATION_AGENT.md -> docs/archives/2026-08-10/ATTACK_ANIMATION_V2_CONTINUATION_AGENT.md
 M docs/archives/README.md
 M docs/development/testing.md
 M docs/plans/2026-08-07-movement-gait-animation-design.md
 M docs/plans/2026-08-07-ranged-units.md
 M docs/plans/2026-08-07-sandata-scaffold-design.md
 M docs/plans/2026-08-07-sandata-scaffold.md
 M docs/plans/2026-08-08-attack-animation-v2-design.md
 M docs/plans/2026-08-09-attack-animation-v2-backlog.md
 M docs/plans/2026-08-09-ranged-units-handoff.md
 M docs/research/FORMATION_AND_COLLISION_MECHANICS.md
 M docs/research/movement/README.md
 M docs/research/movement/tall-hardwood-shield.md
 M docs/research/ranged/2026-08-07-STANDOFF-ROOT-CAUSE.md
 M scripts/run.ps1
 M src/Hukbo.Diagnostics/LogEvents.cs
 M src/Sandata.Client/SandataGame.cs
 M tools/Hukbo.Tools.MixAnalysis/CueSchedule.cs
?? docs/plans/2026-08-10-sandata-playable-client.md
?? docs/plans/README.md
?? src/Sandata.Client/Simulation/
?? tests/Sandata.Client.Tests/InitialSquadGroupsTests.cs
?? tests/Sandata.Client.Tests/TickPacingTests.cs
```

**Six of those paths belong to another live session** working on a playable
Sandata client, and appeared in the tree partway through the previous session:

```
docs/development/testing.md
docs/plans/2026-08-07-sandata-scaffold-design.md
docs/plans/2026-08-10-sandata-playable-client.md
scripts/run.ps1
src/Hukbo.Diagnostics/LogEvents.cs
src/Sandata.Client/SandataGame.cs
src/Sandata.Client/Simulation/
tests/Sandata.Client.Tests/InitialSquadGroupsTests.cs
tests/Sandata.Client.Tests/TickPacingTests.cs
```

Never stash, reset, check out over, or commit those. Never run `git add -A` or
`git commit -a` in this checkout while they are present — that would sweep
another session's unfinished work into your commit. Stage by explicit pathspec,
always.

Note also that their `scripts/run.ps1` edit is exactly the coupling that can turn
the **C# Client suite** red: `tests/Hukbo.Client.Tests/ScriptDefaultsTests.cs`
reads the shell scripts as text and pins their shape. If a Client test fails on
a script-shaped assertion, that is the cause, and it is not yours to fix.

There are 50 worktrees under `.claude/worktrees/`. The `ru-*` ones are sweepable
now that `ranged-units` has landed, but other sessions own some of the rest.
Confirm with the user before removing any worktree.

## What is done

### The baseline, re-established from scratch on 2026-08-10

Both games, Release only, run by the orchestrator and never delegated. Every
figure below is real output, not a summary.

`./scripts/verify.ps1 -SkipBootstrap` — `[PASS]` in full:

```
Formatted 0 of 715 files.        [PASS] Formatting verification completed.
[PASS] Release solution build completed.
Total tests: 2433   Passed: 2433     (Hukbo.Core.Tests)
Total tests: 3499   Passed: 3499     (Hukbo.Client.Tests)
stateHash 1B73FC5923879AA0  eventHash AC55684F24D39344  combatPreset 4  movementPreset 4
stateHash C8023D3B5BEB005E  eventHash F709A345E2F7370E  combatPreset 5  movementPreset 8
[PASS] Canonical repository verification completed.
```

`./scripts/verify.ps1 -SkipBootstrap -Game Sandata` — `[PASS]` in full:

```
Formatted 0 of 715 files.        [PASS] Formatting verification completed.
Total tests: 1113   Passed: 1113     (Sandata.Core.Tests)
Total tests:  199   Passed:  199     (Sandata.Client.Tests)
stateHash BDD56EBD06F76674  eventHash 7C1B37876769DEC7   deterministic: true
```

Sandata's Core suite is **1113**, not the 1108 an earlier handoff recorded; the
five extra tests arrived with Sandata tasks 88, 89 and 90.

The five registered preset combinations the gate does not run were measured at
200 agents, 10,000 ticks, seed 1, and all reproduced their recorded pairs:

| combat / movement | stateHash | eventHash |
| --- | --- | --- |
| V5 / V4 | `47EDD2F7515E291D` | `656D132F9F211D54` |
| V4 / V6 | `24EA6F2183A3D05B` | `2B8DE43B3CAAEF92` |
| V4 / V7 | `B6B0AB6C575D2FE6` | `3298D40F15FC43DE` |
| V4 / V8 | `43458DD43FA3F564` | `AC55684F24D39344` |
| V4 / V9 | `1FC6DAA01656C908` | `246D6E9328CEB12D` |

**Always use named parameters** — `./scripts/benchmark.ps1 -Agents 200 -Ticks
10000 -Seed 1 -Preset <id> -MovementPreset <id>`. An agent once bound them
positionally, silently benchmarked the wrong preset, and reported a green result
proving nothing; the giveaway was a stray untracked file named
`PrecolonialPhilippinesV5` in the repository root, because the value had been
consumed as `-Output`. Confirm each run by reading the `combatPreset` and
`movementPreset` fields the report echoes back, and check `git status` for a
stray file afterwards. A ranged roster runs under V4 or V8 movement and nothing
else — V6, V7 and V9 register no movement profile for a ranged loadout and will
throw.

### RU-31's generation is confirmed done

`./scripts/sfx.ps1 -List` reports `[PRESENT]` on all twenty-six slots, zero
missing. The thirteen new ranged slots hold exactly sixty takes, and all sixty
are committed — `src/Hukbo.Client/Content/Audio` carries 130 tracked `.wav`
files, the seventy that predate the package plus these:

```
release-bangkaw  5   attack-bangkaw  9   clash-shield-bangkaw  3   miss-bangkaw  3
release-busog    6   attack-busog    8   clash-shield-busog    3   miss-busog    3
release-arquebus 7   attack-arquebus 6   clash-shield-arquebus 3   miss-arquebus 2
                                                                   misfire-arquebus 2
```

An earlier handoff said these files were uncommitted. That statement was true
when written and is now false; it has been corrected in place.

### The documentation cleanup, uncommitted

Eight finished documents were moved with `git mv` into
`docs/archives/2026-08-10/`, each given the "Archived: reference only" banner
required by `docs/archives/README.md`:

| Document | Why it was archived |
| --- | --- |
| `2026-08-08-attack-animation-v2.md` | Twelve-task plan complete and merged; the 2026-08-09 backlog carries what it left behind |
| `ATTACK_ANIMATION_V2_CONTINUATION_AGENT.md` | Continuation prompt for that finished package |
| `2026-08-07-movement-gait-animation.md` | All eight tasks Done; `c107539` confirmed an ancestor of `main` |
| `2026-08-08-ranged-units-handoff.md` | Already banner-marked superseded, frozen at wave 4 |
| `2026-08-07-sandata-continuation-prompt.md` | Prompt for wave 5; every wave through 12 has shipped |
| `2026-07-29-leader-standing-orchestration.md` | One-off orchestration prompt, package shipped |
| `2026-07-30-weapon-movement-foundation.md` | One-off orchestration prompt, package shipped |
| `2026-07-30-weapon-movement-weapon-template.md` | One-off prompt template, package shipped |

`docs/agents/` and `docs/prompts/` were emptied by those moves. This file
re-creates `docs/prompts/`.

Three stale status claims in live documents were corrected: the 2026-08-09
ranged handoff no longer says the merge to `main` is undone and no longer says
the WAV files are uncommitted, and `2026-08-07-sandata-scaffold.md` no longer
says "plan, not yet authorized for implementation" when all twelve of its waves
are on `main`.

A new `docs/plans/README.md` indexes every live plan with what it is and what
state it is in, split Hukbo from Sandata, and says where the archived batch went.
Read it before opening anything else in that folder.

Link integrity was checked mechanically across every relative Markdown link in
`docs/` and the repository root. Twenty-two were broken, all of them
pre-existing rather than caused by the move. Eleven were repaired — `FixedPoint`
had moved to `Hukbo.Shared.Core`, the standoff research document had the wrong
relative depth, and the rest were casualties of the 2026-08-07 archive prune,
rewritten into prose the way that prune's other 159 citations were. Nine
survivors sit inside `docs/archives/`, which that folder's README declares
deliberately unmaintained. The last two are in `docs/development/testing.md` and
were left alone only because the other live session has that file open:

```
../agents/17-technical-review-handoff.md
../plans/2026-07-28-formation-movement-realism.md
```

Both targets were deleted in the 2026-08-07 prune. Fixing them means naming the
document in prose, not repointing the path — but coordinate first.

## What is not done

Nothing on this list is code an agent may simply go and write. Three items
require a person, one requires a decision, and one requires authorization.

1. **RU-31 is not closed.** Its acceptance criterion is not that the files
   exist — it is that a person has heard at least one take from each of the
   thirteen new slots, and that has not been recorded. **No agent generates
   sounds; the user ran every command.** If more takes are wanted,
   `./scripts/sfx-ranged.ps1` drives them: it sends nothing without `-Execute`,
   skips files that already exist, retries only a quiet-guard rejection, and
   never passes `-AllowQuiet`. Roughly one take in four comes back inaudible and
   that is normal. A slot that stays quiet across every attempt is a prompt
   problem rather than a threshold problem — the words "thin", "soft",
   "grazing" and "shallow" read to the model as instructions to be quiet.

2. **The eleven `RG-*` smoke rows** in `docs/development/testing.md` are all
   `PENDING`. They need a human at an interactive desktop. **No agent may flip
   one, for any reason, including a passing test.** They became attemptable for
   the first time on 2026-08-09, because the sound files now exist and the game
   plays a ranged battle without crashing.

3. **The V9 termination gap.** V9 resolves 14 of 20 seeds against a bar of 19.
   It is opt-in, `PersistentContingentsV4` remains the shipped default, and the
   user accepted the gap with it recorded. A second cause exists and is
   unidentified. Do not retune to chase it — the refusal counters at
   `BattleSimulation.cs:437` are the instrument for a fresh investigation.

4. **The shipped game does not play the battle that was calibrated.** Every band
   was measured at a 25 per cent ranged share. `ArmyComposition.Default` splits
   evenly across the four ranks and expands under V5 to
   `[63, 63, 14, 31, 16, 11, 8, 13, 31]`, a 14 per cent share. Every plan band
   still passes at 14 — measured, not assumed. This is a design decision, not a
   defect. If the user takes it, the calibrated rank counts are
   `250 × [19, 19, 44, 18] / 100`, roughly Datu 48, Maharlika 47, Timawa 110,
   Aliping Namamahay 45. Note 44, not 54 — the plan said 54 and was wrong;
   corrected at `dd43281`.

5. **Projectile props and embedded projectiles.** Backlogged in
   `docs/plans/TODO.md`, designed in
   `docs/plans/2026-08-09-projectile-props-design.md`. A shot currently draws as
   a stretched pixel from the launch point to its current position, so it reads
   as a line growing behind the thrower. The design splits the work in two. The
   in-flight prop is the small half: it fixes the reported complaint on its own,
   costs about 1,024 quads against 1,956 of headroom, and needs none of the five
   open decisions answered. The embedded half needs those decisions and a
   render-probe measurement, because `SubmissionCount.cs` warns about this
   feature by name — the 500-unit margin fell from 3,468 to 1,956 across RU-23
   and RU-42. **A design document does not authorize implementation.** Ask
   before planning it.

Two smaller threads are also open and are worth knowing about: the fourteen
`GA-*` gait smoke rows and the UI package's manual rows are all still `PENDING`
in `docs/development/testing.md`, and `docs/plans/2026-08-07-unit-test-cleanup.md`
has T6 and T7 unexecuted as a separate scope.

## Verification status

The canonical gate was run twice on 2026-08-10 against `main` at `57760ea` with
a clean tree, once per game, and both passed in full. The output is quoted above.

**That gate result predates the uncommitted documentation changes** described in
this document, and it predates the other session's Sandata edits. The
documentation cleanup touches no C# in the solution — its single source-file
change is a comment path in `tools/Hukbo.Tools.MixAnalysis/CueSchedule.cs`, and
`tools/` is in neither `Hukbo.slnx` nor the gate — so the baselines still stand
for the documentation work. They say nothing about the other session's changes
to `scripts/run.ps1`, `LogEvents.cs`, and `SandataGame.cs`.

Every manual smoke row in `docs/development/testing.md` remains `PENDING`. No
agent flipped one and no agent may.

## Determinism impact

**No simulation state was touched.** The documentation cleanup changes only
Markdown files and one comment in a tool outside the solution. No preset version
needs bumping, no golden expectation moves, and no hash changes. That is
confirmed rather than assumed: both gates were green before the cleanup and the
cleanup contains no change to `src/Hukbo.Core`, `src/Sandata.Core`, or
`src/Hukbo.Shared.Core`.

## Open questions and risks

- **Should the documentation cleanup be committed, and by whom?** It is
  uncommitted and interleaved with another session's work. Nothing was committed
  because the user did not ask for a commit. The staging command that takes only
  the cleanup and none of the other session's files is in "How to resume" below.
- **`HUKBO_AUTOPLAY=1` has been proposed three times and never authorized.**
  `PlaybackController.IsPlaying` defaults to `false` and the only producer of
  `Play()` is `ClientCommand.Play` at `ArenaGame.cs:1259`, reachable only from
  input, so a scripted agent-driven run renders a paused battle with `simTicks`
  at 0 forever. An opt-in read once at construction, exactly as
  `HUKBO_RENDER_PROBE` already is at `ArenaGame.cs:248`, plus a `-AutoPlay`
  switch on `run.ps1`, would let a scripted Debug run drive a battle to
  completion and surface the next presentation-layer crash without a person in
  the loop. It would not let an agent flip a smoke row — only a person may — it
  would only mean the agent finds the crash first. **Ask; do not assume.**
  Related: `ArenaGame.SetProbePlaybackStarted()` already exists behind the
  render-probe opt-in for the same reason, which is worth reading before
  designing anything new.
- **The gate is structurally blind to the client presentation layer.** It never
  formats a battle event, never opens the agent inspector, and never draws
  blood. On 2026-08-09 the first play session that reached a ranged blow crashed
  four times — every one an `ArgumentOutOfRangeException (Parameter 'weapon')`
  with actual value `Arquebus`, every one at tick 66 — while `verify.ps1` stayed
  green in full throughout. The four were `BattleEventFormatter.GetWeaponLabel`,
  `CombatPresetRegistry.TryResolveGrip` underneath it,
  `BloodGeometry.GetSprayProfile`, and `PawnGeometry.ToSwingPose`. Two were
  invisible until the one above was fixed, so the count was not knowable in
  advance. After any change that adds an enum member reaching the client, sweep
  every weapon-keyed and role-keyed switch in `Hukbo.Client` — anchor on both
  `Itak =>` and `Kampilan =>` arms, and again at file level, because one file
  can carry a complete switch and a stale one. `PawnGeometry`'s secondary bounds
  falls through to `Rectangle.Empty` on purpose; that is not a defect. Then
  still have a person play it.
- **Reports are wrong in both directions.** This package produced a green
  benchmark that measured the wrong preset, a band reported `BLOCKED` when it
  was structurally unmeasurable, a control test reported done that proved
  nothing, and a calibration harness printing `FAIL` for a configuration meeting
  every criterion the plan states. Read the diff and re-run the measurement;
  never accept a summary, including a summary in this document.
- **Dead features and dead tests were produced eight times in this package** —
  an optional trailing parameter, a public overload nobody called, an early
  `continue` in a shared loop, an unowned exhaustive switch three separate
  times, pose channels read by nothing, and a test that pinned counts while
  asserting nothing about the values it existed to protect. For every branch a
  task newly consumes, demand a test that fails when the consumption is deleted,
  then prove it by deleting the thing and watching it go red — and verify the
  mutation actually landed, because a pattern that silently matched nothing once
  made a live pin look dead.
- **Tool output in this environment is lossily compressed.** A rendered file can
  come back with words dropped, and code can look syntactically invalid when the
  bytes on disk are fine. Confirm numerically — line counts, regex match counts,
  digit-separated printing — before reporting damage, and never write
  reconstructed content over real prose.
- **Every task row in an old plan is a hypothesis.** Section 3 and section 9 of
  `docs/plans/2026-08-07-ranged-units.md` carry corrections to fifteen
  known-wrong rows; a row followed literally reintroduces a mistake already paid
  for. Resolve file paths and line numbers on disk yourself before handing them
  to anyone.

## How to resume

Run these first, in this order, and read the output rather than assuming it:

```powershell
cd C:\Users\boazs\webdev\autonomous-arena
git status --short          # confirm whose work is in the tree before touching anything
git log --oneline -5
```

Then read, in this order:

1. `docs/plans/README.md` — the index of what is live.
2. `docs/plans/2026-08-09-ranged-units-handoff.md` — the ranged package's
   current status document.
3. `docs/plans/2026-08-07-ranged-units.md` section 9 — the real record, with the
   measurements and the fifteen corrections.

Re-verify only if something changed since `57760ea`, and if you do, run both:

```powershell
./scripts/verify.ps1 -SkipBootstrap
./scripts/verify.ps1 -SkipBootstrap -Game Sandata
```

A green default gate is **no evidence at all** about Sandata; without
`-Game Sandata` the gate never built or ran a line of it, and the two must never
be reported as one result.

To commit the documentation cleanup without touching the other session's work:

```powershell
git add SIMULATION-GAME-STANDARDS.md docs/archives docs/plans docs/research docs/prompts tools/Hukbo.Tools.MixAnalysis/CueSchedule.cs
git restore --staged docs/development/testing.md docs/plans/2026-08-07-sandata-scaffold-design.md docs/plans/2026-08-10-sandata-playable-client.md
git status --short          # confirm nothing of theirs is staged
git commit -m "docs: archive eight finished plans and index the live ones"
```

For anything non-trivial, follow `CLAUDE.md` section 10 and the
`hukbo-orchestrate` skill: one worktree per task off the current integration
commit, non-overlapping file lists, coding tasks on Sonnet, every prompt naming
its evidence and its return format and the current baseline with its
configuration. Pre-resolve symbol maps for any agent entering a large file —
`BattleSimulation.cs` is roughly 4,800 lines and `ArenaGame.cs` roughly 2,050,
and an agent has already died to the 600-second stall watchdog inside one of
them. Tell every agent to commit after each step.

The canonical gate is run once, by the orchestrator, never delegated, and its
real output is the only evidence that counts.
