# Sandata — handoff into wave 11

> **Archived: reference only.** Finished work, kept so a past decision can be
> traced to its reasoning. Never execute it, never treat it as current, and never
> cite it as justification for a change. The live contract is `CLAUDE.md`,
> `SIMULATION-GAME-STANDARDS.md`, `docs/development/testing.md`, and `docs/plans/`.
>
> Section 1's outstanding commit was made long ago, and every wave through 12
> closed on 2026-08-09. Nothing in this document is still to be done.

Written 2026-08-08 at the end of the wave-10 session. Read this before doing
anything. Do not re-derive what is below.

---

## 1. Do this first, before any other work

**A shell append and a commit are outstanding.** Wave 10 is merged and gated, but
its plan record was never committed, because Git Bash broke at the very end of
the session and stayed broken across a dozen attempts in the foreground, in the
background, and in a fresh sub-agent. Every command including a bare `true`
failed with:

```
/usr/bin/bash: -c: line 77: unexpected EOF while looking for matching `''
```

There is no `~/.bashrc`, so the fault is in the wrapper the tool composes, not in
any command. This repository has recorded the same failure before and it clears
on its own.

The prepared record is at:

```
C:\Users\boazs\AppData\Local\Temp\claude\C--Users-boazs-webdev-autonomous-arena\eafda784-039e-479a-901a-096b03fc91ec\scratchpad\wave10-record.md
```

If that scratchpad has been swept, the content is lost and section 4 below is the
summary to rewrite from.

Append it to the END of
`docs/plans/2026-08-07-sandata-scaffold.md` in the `sandata-wave10` worktree,
then commit with exactly:

```
docs: record wave 10 and the four tasks it created
```

**Use a shell append (`>>`). Do not use the Write tool on that plan file.** It is
roughly 1850 lines, and CLAUDE.md section 6 is explicit that reconstructing a
long document from a read would overwrite real prose with a lossy copy.

This handoff document itself is also uncommitted. Commit it in the same change.

---

## 2. Where the code is

- `main` is at `40e5b59` and holds Sandata waves 1 through 9. It is clean and its
  gate was green when it was written.
- **Wave 10 lives on the branch `sandata-wave10` and has not been merged into
  `main`.** Its worktree is
  `C:\Users\boazs\webdev\autonomous-arena\.claude\worktrees\sandata-wave10`.
  Work there, not in the parent checkout.
- Nothing has been pushed. There is no remote work in flight.

Verified on the merged wave-10 branch immediately before the shell died:

```
./scripts/test.ps1 -Configuration Release -Game Sandata
Total tests: 1042      (Sandata.Core)
Total tests: 195       (Sandata.Client)

./scripts/verify.ps1   — all five stages [PASS]
  "stateHash": "1B73FC5923879AA0"
  "eventHash": "AC55684F24D39344"
```

Ten waves of a second game have moved no Hukbo hash. Keep it that way.

Pinned values that must not move without a stated reason:

| Value | What |
| --- | --- |
| `8955292433887190872` | `SandataRuleset.ContentHash` — did **not** move in wave 10 |
| `12611003062847309889` | `FirearmRuleset.ContentHash` |
| `11909359227906322716` | `angle-house.hkmap` content hash |
| `5550901129500655850` | `PreTask61BaselineHash` — proves the order folds were appended, not interleaved |

Six merged wave-10 worktrees are sweepable: `sandata-t49-tickpipeline`,
`sandata-t50-navbench`, `sandata-t72-queuedoor`, `sandata-t73-pathsubmit`,
`sandata-t74-rulesetwiring`, `sandata-t75-wallboundary`. Twenty-two non-Sandata
worktrees (`ru-*`, `ranged-units`, `font-text-quality-t29-31`) belong to other
work — leave them alone. Two unregistered directories, `hit-animations` and
`rank-basecheck`, sit under `.claude/worktrees/` and are not in
`git worktree list`; ask before deleting them.

---

## 3. What to read, in order

1. `CLAUDE.md` — the repository contract. Sections 4, 5, 6, 9, and 10 bind you.
2. `docs/plans/2026-08-07-sandata-scaffold.md` — the task plan. **The task table
   near the top is stale from wave 5 onward.** Fifteen trailing amendment
   sections sit below it and each is more current than the table. Read the last
   two in full: "The wave-10 audit, run before dispatch" and, once you have
   committed it, "Wave 10 complete".
3. `docs/plans/2026-08-07-sandata-scaffold-design.md` — authoritative over the
   plan and over this document. Section 5 is the fourteen-stage pipeline,
   section 16 is the order layer, sections 7 and 8 are navigation and squads.
4. `.claude/skills/hukbo-orchestrate`, `hukbo-verify-and-record`,
   `hukbo-client-ui`, `hukbo-determinism-change` — prefer these over generic
   advice.

---

## 4. What wave 10 actually delivered, and what only looks delivered

Sandata now has a fourteen-stage tick pipeline, `SandataSimulation.cs`, 1292
lines, building warning-clean. **Five stages are honestly degenerate** and were
marked as such by their author at the site and in the report:

- **Stage 7 never calls `PathService.RequestPath`.** No destination-request
  source exists, so no group ever holds a live path.
- **Stage 9's autonomous branch holds position**, for the same reason. Formation
  collapse is therefore structurally unreachable rather than absent — the code is
  right and its input is missing.
- **Stage 11 hardcodes `FirearmId.Ak47`.** `OperatorState` has no loadout field.
- **Stage 12 does not resolve fire.** Every shot hits, damage is an invented flat
  `ProvisionalDamagePerHitPoints = 25`, cover is always `NotInCover`, and
  `AccuracyRules.DrawAngularErrorBam`'s result is discarded outright.
- **Stage 14 emits no events.** The state hash is real and computed on cadence.
  The event half is unimplemented because no event type exists in `Sandata.Core`.

**A stage that runs is not a stage that works.** Do not read the pipeline's
existence as the game being playable.

### The most important finding: task 74 did not close what it appeared to

Task 74 added a cohesion-radius gate to `SquadGrouping.Compute` with boundary
tests on both sides and a test proving that changing the radius changes the
grouping. Every acceptance criterion passed. Task 49c then tested the constant
*through the pipeline* and proved it does nothing. Two compounding causes:

- **A unit mismatch.** `SandataRuleset.GroupCohesionRadius` is documented "in
  world units"; `SquadGrouping.Compute`'s parameter is `groupCohesionRadiusRaw`
  and treated as raw fixed-point. `SandataSimulation.cs:1063` passes one into the
  other, so a default of 96 world units behaves as roughly 0.094.
- **The gate is in the wrong place.** `view.Pairs` comes from
  `SandataCollisionGrid.Rebuild(bodies, bodyRadiusRaw)`, already filtered to
  physical contact. A downstream gate can only narrow a candidate list, never
  widen it. Even with units fixed, operators fifty world units apart never reach
  the comparison.

Task 74's tests passed because the fixtures supplied the candidate pair list
directly instead of going through the collision grid. **A criterion a fixture can
satisfy without exercising the production call chain is not a criterion.** The
previous session's integrating thread wrote those criteria and approved that
shape. Task 77 fixes it.

### Score on wave 9's "all four ruleset constants are read by nothing"

Half closed, not closed.

- `AimToleranceBam` — **proven load-bearing** through the pipeline.
- `LoweredWallDistanceWu` — **proven load-bearing**, inclusive at the threshold.
- `PathLatencyTicks` — **blocked**, correctly reported. Nothing calls
  `RequestPath`, so there is nothing to observe. The test written instead proves
  inertness, and deliberately compares full record equality rather than the state
  hash, because `ContentHash` folds `PathLatencyTicks` and a hash comparison
  would have diverged for the wrong reason and looked like success.
- `GroupCohesionRadius` — **proven not to work**, as above.

### Other verified findings

- **`Sandata.Core` has no event type at all.** This now blocks design section 5's
  stage 14, design section 16's rejection event, and design section 11's event
  log which is marked built. Task 76.
- **Two `ulong` to `int` narrowings** at subsystem boundaries, both bridged with
  `unchecked((int)...)`, both inert only because no group holds a path and no
  shot resolves: `SquadSlot.GroupId` into `PathService`, and
  `OperatorState.EntityId` into `AccuracyRules.DrawAngularErrorBam`. Task 64's
  identifier-widening pass did not reach every consumer. Task 78.
- **A boundary-wall crash, fixed.** `GridRay.Traverse` threw whenever a ray's
  origin cell lay outside the grid, and `angle-house.hkmap` authors perimeter
  walls exactly on the map edge, so `WallBuckets.Build` threw on the project's
  own fixture. It had been latent since task 20. Task 75 fixed it by clamping
  only the broad phase, leaving `GridRay`'s guard intact and the exact narrow
  phase receiving true unclamped coordinates.

---

## 5. Wave 11

Four tasks created by wave 10, whose rows are in the pending plan record:

- **Task 76 — an authoritative event feed for `Sandata.Core`.** Declare the event
  record and ordered feed, retain at most 200 (matching CLAUDE.md section 5's
  Hukbo rule), fold it into the state hash after every field already covered, and
  give stage 14 and `OrderQueue`'s rejection path a real destination.
- **Task 77 — make the cohesion radius govern grouping where candidates are
  formed.** Move the decision to the candidate source or give grouping its own,
  and resolve the unit mismatch in the same change by putting the unit in the
  field's name as `LoweredWallDistanceWu` already does. Inverting task 49c's
  `RunTick_TwoSameFactionOperatorsFiftyWorldUnitsApart_AreNotGroupedDespiteDocumentedRadius`
  is the acceptance test, reached through `RunTick` and not a hand-supplied pair
  list.
- **Task 78 — widen the two remaining identifier narrowings**, and assert no
  `unchecked((int)` cast of an entity or group id remains, by source scan rather
  than inspection.
- **Task 79 — give stage 7 a destination source and stage 12 a hit test.**
  **This row must be split before dispatch.** It is at least three tasks and is
  written as one only so the shared cause stays visible.

Still outstanding from the original plan: **51** (Sandata headless determinism
runner — note `benchmark.ps1 -Game Sandata` still fails with
`Unsupported argument '--agents'`, which is expected and is task 51's to fix),
**52** (determinism equivalence suite and golden replay), **53** (run the
benchmark matrix and the audio harness on named hardware), **54**
(documentation — including writing task 50's measured percentiles into
`docs/development/testing.md`, and adding a `tools/README.md` row for
`Sandata.Tools.AudioPool` which still has none), **55** (the canonical gate).

Task 76 and 78 are disjoint and can run together. **Task 77 must not run beside
anything that calls squad grouping.** Task 79's split parts depend on 77.

---

## 6. How to work — lessons this project paid for

**Audit both directions before dispatching a wave, not after.** No file claimed
twice, *and* every step named in a "What" column claimed exactly once. The
file-level half is easy and catches little. The wave-10 audit found three things
the file check could not see, including that task 50 had been given no
`Program.cs` and so could not reach its own acceptance criterion.

**Ask which surfaces a task moves, not just which files it owns.** Task 49 could
not run beside 72 and 74 despite disjoint file sets, because it is the first
caller of the surfaces they change.

**A finding and its remedy are separate claims.** Wave 9 correctly found all four
ruleset constants unread. The inference that all four were defects was wrong and
survived into a task row — `PathService` and `WeaponLoweredRules` take their
constant as a parameter on purpose. Read the code before writing the remedy.

**Supply an agent's call surface; do not make it discover one.** Task 49 stalled
seven times on a 600-second watchdog, every stall in a read phase and none while
writing. A single grep producing a 121-line `path:line:declaration` index,
handed over as a scratch file the agent deletes before committing, unblocked it
every time. For any task calling many subsystems, do this at dispatch.

**Split coarse rows.** Task 49 cost eight agent runs as one task and completed as
three. The plan's granularity rule is not satisfied by a row that fits in a
table; it is satisfied by a row an agent can finish.

**Tell agents to commit as they go.** Long tasks survive stalls only if partial
work is committed. This saved real work twice.

**Verify every report against disk.** Reports get the file set and the pass/fail
right and the counts wrong, consistently — five instances across waves 7 to 10.
Quote `git diff --stat` against the merge base and the runner's own totals, never
a report's figures. Re-derive test counts from the merged tree.

**Watch for bypassable call sites, not just missing ones.** Three found in three
waves: `OrderQueue.Submit` left public beside `SubmitValidated`, `OrderQueue.Orders`
with a public `init` on a record, and a legacy `SquadGrouping.Compute` overload
with the radius disabled. All three passed every criterion. When a task adds a
validating or ordering entry point, ask what else can reach the same state — a
second constructor, an `init` accessor, a `with` expression, a public collection.

**Infrastructure failures are recoverable.** This session had three
`ConnectionRefused` at launch, one server error mid-edit, seven watchdog stalls,
and a dead shell. Every agent failure recovered by resuming from transcript with
the on-disk state named — never by re-spawning, which loses the transcript. Check
the worktree before assuming work was lost.

**The canonical gate is never delegated.** Run `./scripts/verify.ps1` yourself
after integration and paste the real output. No sub-agent report substitutes, and
no agent may flip a manual smoke-checklist row.

---

## 7. Open decisions the user has not answered

None of these blocks wave 11. Do not decide them unilaterally.

- **The name "Sandata" itself.**
- **Real weapon names versus generic aliases** in shipped display strings. The
  `WeaponNameSetId` field already switches between them.
- **Whether `Hukbo.Client` should grant `InternalsVisibleTo` to `Sandata.Client`,
  or whether a `Hukbo.Shared.Client` tier-2 extraction should happen instead.**
  Four Sandata client tasks have now been forced to copy a Hukbo client internal.
  The grant is one line; the extraction is cleaner but carries the tier-1 hazard
  in full.
- **The alert ladder.** Task 68 shipped a fixed-ceiling model in which a friendly
  death reaches `Breach` directly from `Calm`; design section 5 says `Raised`
  becomes `Breach`. The literal reading was checked and is degenerate — triggers
  fire per tick, so a first death could never breach. **The code is the deviation
  until the user confirms it.** Do not silently "fix" either side; if amended,
  amend both in the same change.
- **Intent selection is undesigned.** Design names the six intents and zero
  trigger conditions. Task 44 invented the entire cascade, including a
  `SuppressionRepositionThreshold` whose input nothing computes. It is documented
  as a decision rather than presented as derived, but it is a gameplay decision
  made by an implementer and wants review before the pipeline makes it
  load-bearing.

---

## 8. Do not

- Run `scripts/sfx.ps1`. It calls ElevenLabs and costs real money — the catalog
  is 106 slots expanding to 524 variant files, roughly 104,800 credits at a zero
  reject rate. Audio spend is unauthorised. The manifest already exists at
  `artifacts/audio/sandata-sound-manifest.csv`.
- Move any Hukbo hash. Ten waves have not.
- Add a hosted CI workflow. Verification is local-only and deliberate.
- Claim anything verified without pasting the real command output.
- Flip a manual smoke-checklist row to `PASS`. Only a human at an interactive
  desktop may do that.
- Run a prose-compression pass over any repository document.

---

## 9. If you are asked to run the game

```powershell
./scripts/run.ps1                    # Hukbo — a real, playable battle
./scripts/run.ps1 -Game Sandata      # Sandata
```

Sandata opens a window and draws its HUD, and as of wave 9 you can draw a path
with the pointer and submit it. But `Program.cs` constructs `SandataGame` with no
map records, and the constructor treats an empty array as a valid empty world, so
there are no walls, no doors, and no operators — operators spawn from map records
that are not supplied. Nothing ticks either, because nothing constructs
`SandataSimulation`.

**The cheapest path to something that looks like a game** is to load
`tests/Sandata.Core.Tests/Fixtures/angle-house.hkmap` at startup and pass its
records to that constructor, then construct `SandataSimulation` and call
`RunTick`. The constructor parameter already exists and everything downstream
already consumes it. Task 75 removed the crash that would have stopped this.
Neither step is in any task row yet — propose it before doing it.
