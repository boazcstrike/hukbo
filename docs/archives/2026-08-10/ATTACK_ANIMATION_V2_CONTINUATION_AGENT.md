# Attack Animation V2 — continuation agent prompt

> **Archived: reference only.** Finished work, kept so a past decision can be
> traced to its reasoning. Never execute it, never treat it as current, and never
> cite it as justification for a change. The live contract is `CLAUDE.md`,
> `SIMULATION-GAME-STANDARDS.md`, `docs/development/testing.md`, and `docs/plans/`.

Paste everything below the line into a fresh agent session. The session may be
started in either `C:\Users\boazs\webdev\autonomous-arena` or the feature
worktree; the prompt tells the agent where the work happens.

---

You are continuing **Attack Animation V2** in the Hukbo repository. Tasks 1
through 6 of a twelve-task approved plan are implemented and committed. Your job
is to execute Tasks 7 through 12 and bring the feature to a verified, reviewed,
integrable state.

## Where the work happens

All work happens in the existing worktree:

```
C:\Users\boazs\webdev\autonomous-arena-attack-animation-v2
```

on branch `codex/attack-animation-v2`, whose tip is `a0f233d
fix(combat): draw contacts in their ingestion frame`. This is a sibling
directory of the main checkout, not one of the worktrees under
`.claude/worktrees/`. Do not do this work in the parent checkout, and do not
create a new worktree for it.

## Read these first, in this order. Do not re-derive their contents.

1. `CLAUDE.md` — the repository contract. Sections 3, 4, 5, 6, 8, 9, and 10 bind
   you. Section 5's determinism rules and the "no CI" rule are absolute.
2. `docs/plans/2026-08-08-attack-animation-v2-design.md` — the design.
   Authoritative over any prompt, including this one. If the design and this
   document disagree, the design wins and you report the discrepancy rather than
   silently choosing.
3. `docs/archives/2026-08-10/2026-08-08-attack-animation-v2.md` — the implementation plan. Your
   task list is Tasks 7 through 12, plus the "Objective definition of done"
   section at the end. Read the whole file; Tasks 1 through 6 explain the
   contracts your remaining work must not break.
4. `.claude/skills/hukbo-verify-and-record` and
   `.claude/skills/hukbo-client-ui` — the gate protocol and the Client testing
   conventions you are expected to follow.

## Verified current state

Confirmed by direct inspection of the worktree, not taken from a prior report:

| Fact | Value |
| --- | --- |
| Worktree branch | `codex/attack-animation-v2` |
| Branch tip | `a0f233d` |
| Working tree | clean, nothing staged or untracked |
| Position relative to `main` | 3 commits ahead, 8 commits behind |
| Merge base with `main` | `b24aa66` |
| `src/Hukbo.Core` diff since the plan commit `0ac8fe0` | empty |
| Total diff since `0ac8fe0` | 33 files, 3,713 insertions, 86 deletions |

The six task commits, oldest first:

```
7b3a1fa feat(combat): classify procedural weapon motion        (Task 1)
95ed5b8 feat(combat): dispatch bounded attack contacts         (Task 2)
1ae63fe feat(combat): anchor attack motion at contact          (Task 3)
b24aa66 fix(combat): latch contacts per attacker               (Task 3 follow-up)
c398cf4 feat(combat): add target-local attack geometry         (Task 4)
04a32e5 feat(combat): prepare atomic contact feedback          (Task 5)
a0f233d fix(combat): draw contacts in their ingestion frame    (Task 6)
```

These files already exist and are the foundation you build on. Do not recreate
them:

- `src/Hukbo.Client/Presentation/` — `AttackMotionFamily.cs`,
  `AttackMotionProfile.cs`, `AttackMotionCatalog.cs`, `AttackContactBundle.cs`,
  `AttackContactDispatcher.cs`, `AttackAnimation.cs`,
  `AttackAnimationSystem.cs`, `AttackFrameCoordinator.cs`,
  `DefenderReactionSystem.cs`
- `src/Hukbo.Client/Rendering/` — `AttackGeometry.cs`, `AttackPoseResolver.cs`

The baseline oracle artifacts are present and are local ignored files:
`artifacts/attack-animation-v2/baseline-v4-seed1.json` and
`baseline-v3-seed1.json`.

I did not re-run the build, the Client test suite, or the gate while preparing
this handoff. Treat the branch as unverified until you run something yourself,
and say so in your report if you rely on a prior claim.

## Four corrections to the plan document. Apply these; do not follow the plan text where it conflicts.

**1. The diff base in Task 12 is wrong.** Task 12 Steps 1 and 3 use
`10197eb..HEAD`. Commit `10197eb` is *not an ancestor of HEAD* on this branch, so
that range compares two divergent lineages and produces a misleading answer —
including a `src/Hukbo.Core` result that looks clean for the wrong reason. Use
`0ac8fe0..HEAD` instead. `0ac8fe0` is the plan commit `docs(plans): plan
weapon-specific attack animations` and is the real start of implementation on
this branch. Every `10197eb` in Task 12 becomes `0ac8fe0`.

**2. Part of this feature is already on `main`.** The merge base with `main` is
`b24aa66`, which means commits `7b3a1fa` through `b24aa66` — Tasks 1, 2, 3 and
the per-attacker latch fix — are already ancestors of `main`.
`src/Hukbo.Client/Presentation/AttackMotionCatalog.cs` exists on `main` today;
`AttackFrameCoordinator.cs` does not. Only `c398cf4`, `04a32e5`, and `a0f233d`
are exclusive to this branch. Account for this when you review "the complete
diff" and when you eventually propose integration: `git diff main...HEAD` shows
three commits, not the whole feature.

**3. The branch is eight commits behind `main`.** Those eight are Sandata work
and documentation. Bring the branch up to date with `main` **before** you run the
canonical gate, so that a failure you see is your failure and not a stale base.
Rebase or merge is your call; state which you did. If a Hukbo test fails on this
branch and passes on `main`, suspect the stale base first.

**4. Task 11's removal list is incomplete.** The plan lists four source files and
three test files to delete. A search of the worktree shows three further live
consumers of the swing types that the plan does not mention:

```
src/Hukbo.Client/Presentation/GaitAnimationSystem.cs
src/Hukbo.Client/Rendering/GaitGeometry.cs
src/Hukbo.Client/Rendering/GaitPoseResolver.cs
tests/Hukbo.Client.Tests/GaitPoseResolverTests.cs
```

Gait animation landed on `main` after this plan was written and it references the
swing system. You must migrate these to the attack-pose types before deleting
anything, and the plan's `rg` search in Task 11 Step 1 will surface them. Do not
delete a swing file while a gait file still consumes it, and do not leave two
parallel attack systems alive to avoid the migration.

## What to execute

Tasks 7 through 12 of `docs/archives/2026-08-10/2026-08-08-attack-animation-v2.md`, in order,
exactly as written apart from the four corrections above.

| Task | Subject | Commit subject the plan specifies |
| --- | --- | --- |
| 7 | Composed stance, articulated arms, weapon trails | `feat(rendering): articulate procedural weapon attacks` |
| 8 | Distinct outcomes and lethal reactions | `feat(combat): add defender contact reactions` |
| 9 | Tune all four families and shield overlays | `feat(combat): tune weapon-specific attack motion` |
| 10 | Motion policy, quality tiers, render budgets | `perf(rendering): bound procedural attack detail` |
| 11 | Complete migration, document human visual checks | `refactor(combat): retire legacy swing presentation` |
| 12 | Prove neutrality, review the diff, run the gate | no commit unless a review finding requires one |

Tasks 7 through 11 are sequential: 8 tunes what 7 renders, 9 tunes what 8
established, 10 budgets all of it, and 11 cannot remove the legacy system until
10 has migrated the last render path. Do not attempt them in parallel.

## How to work each task

Follow the red-green-refactor loop the plan's execution contract requires, per
task:

1. Write the focused failing test the task's Step 1 describes.
2. Run the exact `dotnet test ... --filter` command the task gives, and confirm
   it fails for the stated reason. A test that passes before you implement
   anything is a test that proves nothing — fix the test, do not proceed.
3. Implement the smallest complete change that satisfies the task.
4. Rerun the same filtered command until green.
5. Commit with the Conventional Commits subject the task specifies, staging only
   the files the task names.

Never weaken a test, a warning, or an analyzer to reach green.
`TreatWarningsAsErrors` is on repo-wide. If a pinned value has to move, that is a
finding to report, not a number to edit.

Stop and report to the user, rather than proceeding, if any of these happen:

- a task requires editing a file outside its named file list;
- a determinism hash moves;
- the design document contradicts the plan on something load-bearing;
- a focused test cannot be made to fail before implementation.

## Hard prohibitions

- **Never edit `src/Hukbo.Core/**` or `tests/Hukbo.Core.Tests/**`.** The
  emptiness of that diff is a stated acceptance criterion. This is a Client
  presentation feature end to end.
- **Never change event order, event payloads, simulation cadence, state hashing,
  or event hashing.**
- **Never add a GitHub Actions workflow or any hosted CI.** There is no CI in
  this repository by deliberate choice. Verification is local only.
- **Never integrate into `main`.** Tasks 7 through 12 end with a clean, verified
  feature branch. Merging is the user's decision, made after they read your
  report. Do not merge, do not push, do not open a pull request.
- **Never flip a manual visual checklist row to `PASS`.** Only a human at an
  interactive desktop may do that. Rows you add in Task 11 stay `PENDING`.
- **Never write to the console.** Use `Hukbo.Diagnostics.DiagnosticLog`; a test
  scans `src/` and fails the build otherwise. A disabled log call must allocate
  nothing.
- **No unrelated cleanup.** No opportunistic refactors, no reformatting of files
  the tasks do not name, no fixing of Medium or Low review findings outside the
  feature. Keep the diff scoped to the requested change.
- **No target caches, no unbounded collections, no render state in snapshots, no
  wall-clock time, no rigid-body physics, no global hit-stop or camera shake.**
- **Do not commit the ignored artifacts** under `artifacts/attack-animation-v2/`.

## Verification that is not optional

**The render probe (Task 10 Step 4).** Build the tool and launch the apphost, not
`dotnet run` — matrix mode re-invokes `Environment.ProcessPath` and the apphost
launch is required for that to work:

```powershell
dotnet build tools/Hukbo.Tools.RenderProbe/Hukbo.Tools.RenderProbe.csproj -c Release
& 'tools/Hukbo.Tools.RenderProbe/bin/Release/net10.0/win-x64/Hukbo.Tools.RenderProbe.exe' --matrix 1 120 artifacts/attack-animation-v2/render-matrix.json
```

Fail the run if any station reports zero active-attack samples. Record 200- and
500-agent frame percentiles, maximum quads, active-attack samples, and managed
bytes. A regression is blocking until it is attributed.

**The deterministic comparison (Task 12 Step 2).** Regenerate both reports and
compare them against the captured baselines, excluding only documented volatile
timing and path fields. The expected stable values are:

| Preset | Outcome | Ticks | Event hash | State hash |
| --- | --- | ---: | --- | --- |
| `PrecolonialPhilippinesV4` | `Faction1Victory` | 981 | `AC55684F24D39344` | `1B73FC5923879AA0` |
| `PrecolonialPhilippinesV3` | `Faction0Victory` | 1097 | `082F98C214611DCF` | `8EA60CC41625DA6E` |

Any deviation is a blocking defect in your Client changes, not a baseline to
update.

**Independent review (Task 12 Step 4).** Request a review of the complete diff.
The reviewer classifies findings Critical, High, Medium, or Low. Resolve every
Critical and High, rerun the affected focused tests, and request re-review. Leave
Medium and Low unaddressed unless they fall inside the feature's own files.

**The canonical gate (Task 12 Step 5).** Run it yourself, once, after everything
else:

```powershell
./scripts/verify.ps1
```

It is never delegated to a sub-agent and no sub-agent report substitutes for its
output. Paste the actual output into your completion report. A build that
compiles is not a passing gate, and a passing gate is not a visual check.

## Definition of done

The plan's "Objective definition of done" section is the contract. In addition,
your session is finished only when all of the following hold:

- Tasks 7 through 11 are each committed with the specified Conventional Commits
  subject, and the worktree is clean.
- `git diff --name-only 0ac8fe0..HEAD -- src/Hukbo.Core tests/Hukbo.Core.Tests`
  prints nothing.
- Both benchmark reports reproduce the pinned hashes, ticks, and outcomes above.
- The render probe matrix ran, every station recorded at least one active-attack
  sample, and the numbers are in your report.
- `./scripts/verify.ps1` passed, with its real output pasted.
- Every Critical and High review finding is resolved and re-reviewed.
- The new rows in `docs/development/testing.md` are present and `PENDING`.
- `main` is untouched and nothing is pushed.

## How to report

Report honestly and specifically. If a gate fails, paste the failing output —
the shortest decisive lines, not the whole log. If a task is blocked, finish
every other task that does not depend on it, then say plainly what you left out
and why. If a smoke row cannot be verified by a human at a desktop, it stays
`PENDING`; never describe a compile or a unit test as a visual confirmation.

State which of the four plan corrections you applied and what you found when you
applied them, particularly the gait-system migration in Task 11 — that one is the
most likely place for this handoff's information to turn out incomplete.
