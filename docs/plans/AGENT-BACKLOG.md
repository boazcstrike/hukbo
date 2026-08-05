# Agent backlog — movement V7 pressure interrupt, session handoff

Written 2026-08-01. This document is a continuation prompt. Read it in full
before touching anything, then execute from "What to do next".

---

## Where the work lives

All V7 work sits on the branch **`v7-pressure-interrupt`**, checked out at
`.claude/worktrees/v7-integration`, currently at **`42c119b`**. It has **not**
been merged to `main` and has **not** been pushed.

`main` moved twice while this work was in flight and is now at `08e4c61`. The
integration branch is based on `6824068` and does not contain either of those
commits.

**Merge `main` into the integration branch before doing anything else.** Merge,
do not rebase — this checkout is shared with concurrent sessions that move
`main` without warning, and the branch has eighteen merge commits worth of
history that a rebase would rewrite.

Two conflicts are expected and both are real:

| File | Why |
| --- | --- |
| `src/Hukbo.Client/ArenaGame.Rendering.cs` | Task D3 added one argument at the pawn draw call; `main`'s responsive-presentation work touched the same file |
| `docs/development/testing.md` | Tasks F1 and F3 both write here, and `main` has already edited it |

---

## What is done

Sixteen tasks merged, in this order. Each was verified against disk in the main
session — `git diff --name-only`, scope checks, and the pinned artifacts re-run
by hand — rather than accepted from the implementing agent's report.

| Task | What it landed | Merge |
| --- | --- | --- |
| A0, A1, A2, B1 | Baseline, shield ratification, stale-comment fixes, the version gate | already on `main` before this session |
| B2 | Per-row `PressureInterruptThresholdBasisPoints`, folded behind the gate | `681daa0` |
| B3 | `EquipmentRelativeFootworkV7` registered | `f0a8448` |
| B4 | Three `AgentState` fields and their state-hash gate | `5f9d7bb` |
| B5 | `ShouldPressureInterrupt` and the step 1a ladder branch | `c9a3d33` |
| B6 | The interrupt wired into the simulation | `3710e29` |
| B6a | `_pressureBasisPoints` derived scratch | `589fcb4` |
| B6b | One weighted-pressure evaluation per living agent per tick | `69a1292` |
| C1 | Predicate unit tests plus the living-agent invariant pin | `29f93f7` |
| C2 | State-hash and content-hash gate tests | `ff161eb` |
| C3 | Combo-chain coverage under the interrupt | `2539f16` |
| E0 | The hand-run calibration harness | `d9019f9` |
| E1 | Threshold unit corrected; the negative calibration result recorded | `52957c5` |
| E2 | V7's content hash and trajectory digest frozen | `0f9e297` |
| D1 | The interrupt projected onto `AgentView` | `64fa36f` |
| D2 | Inspector pressure row and break-off annotation | `d06481f` |
| D3 | Pawn break-off mark | `42c119b` |

**B6a and B6b are not in the original twenty-one-task plan.** B6a closed a gap
task B6 correctly reported as blocked; B6b restored a design property B6a traded
away. Task F3's archive step should record that the plan grew by two tasks.

Current test counts at `42c119b`: **Core 2611**, **Client 2848**. The canonical
gate has been run and passed green after every merge — eleven times in total,
the last at `42c119b`.

The twelve originally pinned artifacts never moved once across all sixteen
merges. No literal was re-pinned, no digest re-recorded, no fixture edited. V7
added a thirteenth and fourteenth: `ContentHash = 0x66F4FDF91F56AF1B` and
`tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-movement-v7-digest.json`.

---

## The finding that changes the plan's premise

**V7 does not meet the design section 2.1 termination bar, and no tuning of the
values task E1 owns can make it.** This is measured, not predicted.

Task E1 ran the full ten-cell matrix across six candidate tunings. Zero of ten
cells reached a decisive outcome within 6,000 ticks; all ten still draw at the
10,000-tick limit, exactly as V6 does.

The decisive measurement is candidate 2: registering the minimum legal threshold
of 1 on every row makes the predicate fire on every agent-tick it can ever fire
on. No tuning can fire more. That produced firing rates of 0.093% at 200 agents
and 0.105% at 500, and every cell still drew. Across the six candidates the
firing count ranged over a factor of 4.6 and no cell's terminal tick moved by a
single tick.

The mechanism is recorded in
`docs/plans/2026-07-31-movement-v7-baseline.md` lines 510 through 516. The
interrupt is gated to fire only from `FootworkPhase.Commit` or `Recover`, but
the standoff is a refusal to enter that lifecycle at all. The seed-1 200-agent
cell holds 1,140,221 `refuseAgentTicks` and 338,634 `regroupAgentTicks` against
2,216 `commitAgentTicks` and 2,017 `recoverAgentTicks` — about 349 to 1. The
interrupt's entire addressable population is roughly 0.3% of agent-ticks, and
the warriors holding the battle open are precisely the ones that never commit.

Design section 11 open question 3 asked whether the interrupt would be enough on
its own. It is not. The full search is recorded in
`docs/plans/2026-07-31-movement-v7-calibration-record.md`.

**Do not attempt to fix this by tuning weights or thresholds.** That search is
finished. Any future work on battle termination belongs upstream, in whatever
rule keeps warriors in `Refuse` and `Regroup`, and needs its own design document
under `CLAUDE.md` section 6.

---

## The second finding, and the open decision

**The Client cannot select V7, so no human can observe any of the three
spectator channels tasks D1, D2 and D3 just built.**

Verified directly: `grep -rn "MovementPreset *=" --include=*.cs src/Hukbo.Client/`
returns nothing. `ArenaGame.BuildScenario` is
`Scenario.CreateDefault(seed, …) with { RosterCounts = … }` and overrides
nothing else, so the Client always runs the shipped default
`PersistentContingentsV4`. Under that preset `AppliesPressureInterrupt` is
`false`, all three new `AgentView` members stay at their defaults, no mark is
ever drawn, and no pressure row ever renders. The only Client reference to the
preset is a debug-log gate at `src/Hukbo.Client/ArenaGame.cs:1490`.

This is not "rare because the interrupt fires 0.09% of the time". It is
unreachable by construction. Decision D6 only moves the default once the
termination bar passes, and task E1 proved V7 never will.

**The open decision, which must be taken before task F1 runs.** F1 creates
manual smoke rows for these channels. It cannot honestly write them `PENDING`,
because `PENDING` asserts a human has not run the check yet, and here no human
*can* run it.

1. **Write the rows `BLOCKED`**, with the reason recorded, noting they become
   executable the day any interrupt-applying preset is selectable. This was the
   recommendation at handoff. It records reality without inventing scope, and
   the channels are not wasted — they are built, unit-tested, and inherit to
   whatever preset eventually fixes the refuse loop.
2. **Add a Client preset selector first.** This is the only thing that makes the
   channels real, but it is a new feature outside this design's authorization
   and needs its own design document.
3. **Write them `PENDING` anyway.** Not recommended. It would assert something
   false about what a human can do.

---

## What to do next

In order. Four tasks remain from the plan, plus a documentation pass.

**Step 0.** Merge `main` into `v7-pressure-interrupt` and resolve the two
conflicts named above.

**Step 1 — task F0.** Determinism and logging neutrality under V7. Assert that
same seed plus same build gives identical state hash, event hash, winner, and
ordered event stream across repeated runs, and that the seed-1 workload under V7
with logging off and at `trc` produces identical results. Files:
`tests/Hukbo.Core.Tests/DeterminismTests.cs` and whichever file already owns the
logging-neutrality run.

**Step 2 — task F1.** The manual smoke rows, once the decision above is taken.
Model the section on the existing "Leader marker and inspector annotation smoke"
block in `docs/development/testing.md`, including its honest preamble naming
which automated tests prove what, and its legacy-regression row. File:
`docs/development/testing.md`.

**Step 3 — task F2.** Re-measure and record. The harness already has the
numbers — E1's full ten-cell matrix at final values is in the calibration
record. F2 compares against the A0 baseline and gives an explicit verdict per
criterion. Note that decision D2 makes a `p50Milliseconds` failure separate work
*only when the termination bar passes*; it does not, so both are plain failures.
Measured medians at E1's final values were 0.2020 ms at 200 agents against V4's
0.0607 (3.33×, ceiling 2.0×) and 0.9279 ms at 500 against V4's 0.2275 (4.08×,
ceiling 2.5×).

**Step 4 — the documentation pass.** Several live contract documents now assert
outcomes that measurement has falsified. Leaving them would mislead the next
agent exactly the way the three stale `ContentHash` comments did, which is what
task A2 existed to fix at the start of this plan.

**Annotate, do not rewrite.** Follow the rule task A1 used for the shield
research band: the design was a real decision record, and the amendment records
that measurement departed from it, not that the design was wrong to try.

| Document | What now reads false |
| --- | --- |
| `2026-07-31-movement-v7-pressure-interrupt-design.md` section 1 | Says the interrupt addresses a battle that never resolves. Resolution is unchanged |
| the same, section 11 question 3 | Written as open. It is answered, negatively |
| `2026-07-31-movement-v7-calibration-decisions.md` D2 | The termination bar is written as a target V7 would meet |
| the same, D6 | Says the default moves after the D2 bar passes. That condition can never be met by V7 |

**Step 5 — task F3.** Run `./scripts/verify.ps1` once on the integrated branch
and paste its real output. Then archive the finished plans into
`docs/archives/2026-08-01/movement/` with the "Archived: reference only" banner.
`Scenario.MovementPreset` does not move — decision D6 stands, now permanently as
far as V7 is concerned.

Then merge `v7-pressure-interrupt` to `main`.

---

## Rules that bound this work and still bind

- **The canonical gate is never delegated.** `./scripts/verify.ps1` runs in the
  main session and its real output is the evidence.
- **Never re-pin a literal, re-record a digest, or edit a fixture to go green.**
  Fourteen artifacts are pinned now. If one moves, the change is wrong.
- **Verify every agent claim against disk.** Reports were wrong in both
  directions repeatedly this session. Check `git status`, `git diff --name-only`,
  and re-run the pinned artifacts yourself.
- **Tool output is lossily compressed.** Reads of `WeaponMovementRules.cs` and
  `MovementPresetId.cs` came back mangled several times while the bytes on disk
  were correct. `sed` and `grep` through Bash returned them intact where the
  `Read` tool did not. Never `Write` reconstructed content over an existing
  file.
- **Line numbers in the design and task plan are stale**, by roughly 20 to 45
  lines in source files after sixteen tasks. Test-file numbers drifted much
  less. Match anchors by content.
- **Never clean up, commit, or merge a worktree you did not create.** Eighteen
  `v7-*` worktrees from this session are still present under
  `.claude/worktrees/`, plus several from other sessions. They can be removed
  once the branch is merged, but confirm before doing it.

---

## Useful facts established this session

- The weighted sum is a **true weighted average**, so it can never exceed its
  largest input. The interrupt can only fire when at least one signal
  individually reaches the threshold; weighting drags the maximum down and never
  lifts a combination up. The maximum reachable value for a surviving warrior is
  `2·w₁ + 10,000` basis points, strictly.
- `ShouldPressureInterrupt` and `ComputeWeightedPressure` carry **no argument
  guards, by decision**. Safety comes from the simulation calling them only on
  the living-agent path. `tests/Hukbo.Core.Tests/Movement/FootworkPressureInterruptTests.cs`
  pins the two facts that invariant rests on.
- `AgentState.BrokeOffUnderPressure` **persists** across the whole
  interrupt-produced `Disengage` rather than pulsing for one tick. Counting it
  per tick overcounts firings; it is also cleared within the same tick when lane
  clearance falls the phase back, so counting `false → true` transitions
  undercounts. The harness reads the simulation's per-tick scratch by reflection
  instead.
- The calibration harness is **not** a test. Its only `[Fact]` sits behind
  `#if HUKBO_CALIBRATION`, which no ordinary build defines, so the gate's test
  count is unaffected. Run it with
  `dotnet test tests/Hukbo.Core.Tests -c Release -p:DefineConstants=HUKBO_CALIBRATION --filter FullyQualifiedName~PressureInterruptCalibrationRun --logger "console;verbosity=detailed"`.
  A full ten-cell matrix takes about two minutes.
- The harness reproduces the recorded `PersistentContingentsV4` baseline exactly
  across all ten cells, which is its own correctness evidence.
- V7's tick-1 event fold is **identical** to V6's while its state hash differs.
  That is the correct shape: the ruleset content hash reaches the state hash but
  not the event fold.
