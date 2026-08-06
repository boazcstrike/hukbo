# Leader character — plan

Design: `docs/plans/2026-08-07-leader-character-design.md`. Read it first; this
document assumes its decisions and does not re-argue them.

Branch: `leader-character`, based on `14f104b`.

Scope: `Hukbo.Client` presentation only. No file under `src/Hukbo.Core`,
`src/Hukbo.Headless`, or `tests/Hukbo.Core.Tests` is edited by any task below.

## 1. Task table

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| L1 | Add `AppearancePresetStatus` (`General`, `Leader`) as a property on `AppearancePresetEntry`, defaulting to `General`; mark `Vis15`, `Tag13`, and `Tag15` as `Leader`; add a `bool isLeader` parameter to `AppearancePresets.SelectPreset` and split the pre-built compatible pools into a general pool and a leader pool per `(block, isWasay)`; when the leader pool is empty, fall back to the general pool. No new salt. | `src/Hukbo.Client/Presentation/Catalogs/AppearancePresets.Levy.cs`, `src/Hukbo.Client/Presentation/Catalogs/AppearancePresets.Visayan.cs`, `src/Hukbo.Client/Presentation/Catalogs/AppearancePresets.Tagalog.cs`, `tests/Hukbo.Client.Tests/AppearancePresetTests.cs`, `tests/Hukbo.Client.Tests/AppearancePresetsVisayanTests.cs`, `tests/Hukbo.Client.Tests/AppearancePresetsTagalogTests.cs` | `SelectPreset(id, Visayan, weapon, isLeader: true)` returns `appearance.presetVisayan.vis15` for every probed entity id; `SelectPreset(id, Tagalog, weapon, isLeader: true)` returns `tag13` or `tag15` and nothing else; no `isLeader: false` call over a wide entity-id sweep returns any of those three ids; `SelectPreset(id, Cagayan, weapon, isLeader: true)` returns a Cagayan row rather than falling back to `Lev01`; the existing stability, empty-pool, and same-block tests still pass. | — | `./scripts/test.ps1 -Configuration Release` |
| L2 | Add `bool isLeader` to `PawnAppearanceFactory.Create` and forward it to `SelectPreset` only. Add `isLeader` to the `PawnAppearanceCache` key and to `Resolve`'s parameter list; rewrite the class remarks' "Invalidation" and "no key input can change during a battle" clauses to state that leadership can change mid-battle and correctness rests on the stored-key comparison. | `src/Hukbo.Client/Presentation/PawnAppearanceFactory.cs`, `src/Hukbo.Client/Presentation/PawnAppearanceCache.cs`, `tests/Hukbo.Client.Tests/PawnAppearanceFactoryTests.cs`, `tests/Hukbo.Client.Tests/PawnAppearanceCacheTests.cs` | A factory test shows the same entity id resolves a different `AppearancePresetId` as leader than as non-leader in the Visayan and Tagalog blocks, and an identical one in every other field (stature, build, skin, clothing, accent, head treatment, weapon tint, shield skin); a cache test shows that resolving ordinal *n* as non-leader and then as leader returns the leader appearance, records a miss rather than a hit, and does not increment `Fill` a second time. | L1 | `./scripts/test.ps1 -Configuration Release` |
| L3 | Pass the real leadership value at both call sites: `agent.IsLeader` into `PawnAppearances.Resolve`, and `selected.IsLeader` into the inspector panel's direct `PawnAppearanceFactory.Create`. Add a source-scan assertion that neither call site can regress to a literal `false`. | `src/Hukbo.Client/ArenaGame.Rendering.cs`, `src/Hukbo.Client/UI/AgentInspectorPanel.cs`, `tests/Hukbo.Client.Tests/SourceHygieneTests.cs` | Both call sites pass a value read from the agent; the source-scan test fails if either `PawnAppearances.Resolve` or the panel's `PawnAppearanceFactory.Create` invocation is missing an `IsLeader` argument. | L2 | `./scripts/test.ps1 -Configuration Release` |
| L4 | Reshape the on-sprite leader mark: widen `GetLeaderMarkBounds` to the full head width and to `max(2, headHeight / 4)`, and draw an upward chevron — base band plus two rising segments, three quads — inside that slot. Update the four pinned `InlineData` rows in the same diff. Leave `GetBreakOffMarkBounds`'s derivation from the leader slot intact. | `src/Hukbo.Client/Rendering/PawnRenderer.cs`, `tests/Hukbo.Client.Tests/PawnRendererTests.cs` | The four `GetLeaderMarkBounds_MatchesTheInlineArithmeticItReplaced` cases carry recomputed expectations and pass; every non-overlap test in the break-off/leader/selection suite still passes over the full head-bounds grid and over `EnumerateLayouts()`; a new test asserts the mark's height is strictly greater than one pixel for every head in the grid taller than four pixels. | — | `./scripts/test.ps1 -Configuration Release` |
| L5 | Add `bool isLeader = false` as a trailing optional parameter to `PawnQuadCount.Count` and count the leader mark's three quads when it is set. State the budget arithmetic in the commit message as `PawnQuadCountTests`' class summary requires. | `src/Hukbo.Client/Rendering/SubmissionCount.cs`, `tests/Hukbo.Client.Tests/PawnQuadCountTests.cs` | A new differential test asserts `Count(..., isLeader: true) == Count(..., isLeader: false) + 3` at all three detail tiers; the four absolute pins (Low 17, Medium 19, High 20, high-tier-loaded-selected 40) and the selection `+8` differential are unchanged because every existing call site omits the new argument. | L4 | `./scripts/test.ps1 -Configuration Release` |
| L6 | Emit a standalone leadership row from `AgentInspectorContent.BuildLowerLines` when `agent.IsLeader` is true and `FormatContingentLine` returned `null`. Keep the wording "leading"; do not use "chief" or "commander". Do not raise `MaximumLowerRowCount`. | `src/Hukbo.Client/UI/AgentInspectorContent.cs`, `tests/Hukbo.Client.Tests/AgentInspectorContentTests.cs` | A leading agent whose `ContingentState` is `None` produces a line stating it is leading; a leading agent with a real contingent state produces the existing `(leading)` suffix and no second row; `LowerLinesWithAContingentRowAndTheRankReconstructionNoteNeverExceedTheRowBudget` still asserts exactly `20`; `InspectorGeometry_AtOneHundredPercentPreservesBaselineValues` still asserts `857`. | — | `./scripts/test.ps1 -Configuration Release` |
| L7 | Add the leader-identification smoke checklist to the manual testing document, every row `PENDING`, plus the results section skeleton that will hold the gate output. | `docs/development/testing.md` | The checklist from section 5 of this plan is present, every `Status` cell reads `PENDING`, and no row claims a result. | L4, L6 | Human review of the diff; no agent may flip a row |
| L8 | Run the canonical gate on the integrated branch and record its real output in `docs/development/testing.md`, including the seed-1 state hash, event hash, and winner, and an explicit statement that the interactive rows remain `PENDING`. | `docs/development/testing.md` | The recorded seed-1 state hash, event hash, and winner match the existing baseline exactly; the gate output is pasted, not summarised. | L1–L7 | `./scripts/verify.ps1` |

## 2. Parallelism and file ownership

Every file each task may touch is named in full in the table above. No task
carries an "and related files" clause, and no file appears in two tasks that run
at the same time.

### Waves

| Wave | Tasks | Runs in parallel | Why |
| --- | --- | --- | --- |
| 1 | **L1**, **L4**, **L6** | Yes, all three at once | Their file sets are strictly disjoint: L1 owns the three catalog files and their three test files, L4 owns `PawnRenderer.cs` and `PawnRendererTests.cs`, L6 owns `AgentInspectorContent.cs` and `AgentInspectorContentTests.cs`. None reads a symbol another is changing. |
| 2 | **L2**, **L5** | Yes, both at once | L2 owns the two appearance-pipeline files and their tests; L5 owns `SubmissionCount.cs` and `PawnQuadCountTests.cs`. Disjoint. L2 must follow L1 because `SelectPreset`'s signature changes there; L5 must follow L4 because the quad count it pins is a consequence of the mark's new shape. |
| 3 | **L3** | Alone | It is the only task that may edit `ArenaGame.Rendering.cs` and `AgentInspectorPanel.cs`, and it cannot start until `Resolve`'s signature is settled by L2. |
| 4 | **L7** | Alone | Documentation. It must follow L4 and L6 because the smoke rows describe what those two shipped. |
| 5 | **L8** | Alone, and never delegated in the sense of substituting a report for output | The canonical gate runs once, after integration. |

### Serial constraints, stated explicitly

- **L1 → L2 → L3** is a hard chain through one signature. `SelectPreset` gains a
  parameter in L1; `PawnAppearanceFactory.Create` and `PawnAppearanceCache.Resolve`
  gain theirs in L2; the two call sites are updated in L3. Running any two of
  these together would put two agents on the same evolving signature.
- **L4 → L5** is serial because the number L5 pins (three quads) is decided by
  L4. They do not share a file, so the constraint is semantic rather than
  textual, and L5 must read L4's landed diff rather than this document's
  estimate. If L4 lands a different quad count, L5 pins that number and says so.
- **L4 and L5 must not be merged into one task even though both concern the
  mark.** `PawnRenderer.cs` and `SubmissionCount.cs` are separately owned in this
  repository's existing task history, and `PawnQuadCountTests`' class summary
  requires the pin update to arrive with its own budget arithmetic in the commit
  message.
- **Nothing in wave 1 may touch `PawnAppearanceFactory.cs`.** L1's change is
  confined to the catalog; the factory is L2's.
- **`docs/development/testing.md` is owned by L7 and then by L8, never by both at
  once.** L8 appends a results section below the checklist L7 added.

### Files no task may touch

`src/Hukbo.Core/**`, `src/Hukbo.Headless/**`, `src/Hukbo.Diagnostics/**`,
`tests/Hukbo.Core.Tests/**`, `Directory.Packages.props`, any `packages.lock.json`,
`global.json`, and `.github/**`. A task that believes it needs one of these has
found a scope error and must stop and report rather than edit.

## 3. Pinned values this plan moves

A pinned value is re-recorded only in the same diff as the change that moved it,
with the new arithmetic written into the test's own comment. Re-pinning to make
a red test go green is forbidden; if a pin moves in a way this section did not
predict, that is a finding to report, not a number to update.

### Moved by L4

`tests/Hukbo.Client.Tests/PawnRendererTests.cs:315-318`, the four `InlineData`
rows of `GetLeaderMarkBounds_MatchesTheInlineArithmeticItReplaced`. Old values,
in the order `(headX, headY, headWidth, headHeight, expectedX, expectedY,
expectedWidth, expectedHeight)`:

| Old row | Head | Old expected rectangle |
| --- | --- | --- |
| `[InlineData(0, 0, 12, 14, 3, -3, 6, 2)]` | `(0, 0, 12, 14)` | `(3, -3, 6, 2)` |
| `[InlineData(100, 250, 6, 7, 102, 248, 3, 1)]` | `(100, 250, 6, 7)` | `(102, 248, 3, 1)` |
| `[InlineData(-40, -120, 25, 30, -34, -128, 12, 5)]` | `(-40, -120, 25, 30)` | `(-34, -128, 12, 5)` |
| `[InlineData(0, 0, 1, 1, -1, -2, 2, 1)]` | `(0, 0, 1, 1)` | `(-1, -2, 2, 1)` |

The explanatory comment above them at `PawnRendererTests.cs:311-314` states the
old arithmetic — "width/2, height/6, gap height/8, floored at 2/1/1 and centred
on head" — and must be rewritten to state the new arithmetic rather than left
describing a formula the code no longer has.

The test method name itself contains the words "MatchesTheInlineArithmeticIt-
Replaced", which will no longer be true. L4 renames it to describe what it now
pins.

### Re-verified but not expected to move, by L4

`tests/Hukbo.Client.Tests/PawnRendererTests.cs:196-299`, the break-off versus
leader versus selection non-overlap suite. `GetBreakOffMarkBounds`
(`src/Hukbo.Client/Rendering/PawnRenderer.cs:1408-1419`) derives its rectangle
from `GetLeaderMarkBounds`, so widening the leader slot moves the break-off band
too. The suite asserts relationships rather than literals, so it should continue
to pass unchanged — but "should" is not evidence, and a failure here means the
new geometry has broken the structural non-collision guarantee and must be
reshaped, not that the assertion should be relaxed.

### Deliberately not moved, by L5

`tests/Hukbo.Client.Tests/PawnQuadCountTests.cs` keeps every existing pin:
Low `17` (`:38`), Medium `19` (`:50`), High `20` (`:62`),
high-tier-fully-loaded-selected `40` (`:103`), and the selection `+8` differential
(`:118`). They survive because `isLeader` is added as a *trailing optional*
parameter defaulting to `false`, so every existing call site — including
`tests/Hukbo.Client.Tests/RenderBudgetEstimateTests.cs:39`, which is outside
L5's file set and must not be edited — keeps its current meaning. L5's new
assertion is a differential rather than a fifth absolute pin.

The known hazard of a trailing optional parameter is a call site that silently
never passes it, so the feature never fires. That hazard is real for behaviour
code; here `PawnQuadCount.Count` is a measurement seam with no gameplay effect,
and the corresponding hazard for the *actual* feature is covered by L3's
source-scan assertion.

### Deliberately not moved, by L6

`tests/Hukbo.Client.Tests/AgentInspectorContentTests.cs:1452-1453` asserts the
deepest lower-line count equals `MaximumLowerRowCount` and that the constant is
`20`. The new leadership row appears only when the contingent row is suppressed,
so the two are mutually exclusive and the deepest count is unchanged.
`AgentInspectorContentTests.cs:230-233` asserts
`ComputeRequiredHeight(EvidenceReservedLineCount) == 857` at 100% scale; it
follows from `MaximumLowerRowCount` and therefore also does not move. If either
of these moves, L6 has added an unconditional row by mistake.

### Not touched anywhere in this plan

`tests/Hukbo.Client.Tests/PawnGeometryTests.cs`: `GetBounds` `(76, 36, 79, 72)`
at `:652`, `PlaceholderBounds` `(92, 74, 17, 17)` at `:756`, `ShieldBounds`
`(75, 71, 10, 26)` at `:872`, `TorsoBounds` `(92, 69, 17, 29)` and `HeadBounds`
`(92, 50, 17, 17)` at `:1046-1047`. `HeadBounds` feeds `GetLeaderMarkBounds`,
so a change to head geometry would move the leader mark — which is exactly why
no task in this plan touches `PawnGeometry`. `PawnGeometryTests.cs:213-214` is
the only whole-struct `Assert.Equal` on `PawnLayout`, and `PawnLayout` is an
internal readonly record struct with roughly twenty-three positional parameters,
so adding a field to it would be source-breaking for positional construction. No
task adds a field to `PawnLayout`.

Also untouched: `PresentationSalts` and `PresentationSaltsTests` — no new salt is
introduced — and every `MovementPresetRegistryTests` content hash and
`MovementPresetFreezeTests` trajectory digest, because no simulation file is
edited.

## 4. Verification

### The canonical gate

`./scripts/verify.ps1` is the canonical gate and the only thing that decides
whether this work is integrable. It runs once, on the integrated branch, as task
L8, and its real pasted output is the evidence. No sub-agent's report substitutes
for it. Its five stages are prerequisites and locked restore, format
verification, Release build, Core plus GPU-independent Client tests, and a
200-agent / 10,000-tick / seed-1 headless determinism workload.

Per-task verification during waves 1 through 4 is
`./scripts/test.ps1 -Configuration Release`, which is faster and sufficient to
decide whether a task is done. It is not the gate and no task may claim
otherwise.

### What the determinism stage must show

Because no file under `src/Hukbo.Core` is edited, the seed-1 state hash, event
hash, winner, and ordered event stream must be **identical to the existing
recorded baseline**. A moved hash is not a new baseline to record; it means
something in this work reached the simulation and the change is wrong. The
implementing agent that sees one stops and reports rather than re-recording.

### Formatting and warnings

`TreatWarningsAsErrors` is on repo-wide with nullable enabled. No task may
weaken a test, suppress a warning, or relax an analyzer to reach green.
`./scripts/format.ps1 -Verify` runs inside the gate; running it locally before
handing a task back saves a gate cycle.

### Rebase before the gate

`main` moves. Before running L8, rebase `leader-character` onto current `main`
and re-run. A red `Hukbo.Core` test on a Client-only change is a stale branch
base until proven otherwise.

### The 500-agent report

`SIMULATION-GAME-STANDARDS.md:332-333` asks for a reported 500-agent result.
Run `./scripts/benchmark.ps1 -Agents 500 -Ticks 10000 -Seed 1` once, after the
gate, and record the figure. Because no simulation code changed, the expected
finding is no measurable movement against the previous recorded run; any
movement is a signal to investigate rather than a number to accept.

## 5. Manual smoke rows to add

Task L7 adds the section below to `docs/development/testing.md`, verbatim, with
every `Status` cell reading `PENDING`.

**No agent may flip any row below to `PASS`.** Only a human at an interactive
Windows desktop, having actually watched the screen, may change a status.
Compilation, a passing test run, a window-opening probe, and synthetic input all
prove nothing about any row here. A row that cannot be reached is reported
`BLOCKED` with the reason; an untouched row stays `PENDING`.

### Leader identification smoke (Client presentation)

**No interactive run has been performed for this change. Every row below is
`PENDING`.** The automated tests prove the preset gating, the cache key, the
mark geometry, the quad accounting, and the inspector row. None of them prove
that a person watching a battle can pick the leaders out.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| LC-1 | Start a battle and watch at the default zoom without clicking anything | Roughly sixteen warriors carry a mark above the head whose shape differs from every other pawn's outline, not merely its colour | | PENDING |
| LC-2 | Zoom all the way out to the Low detail tier | The leader marks are still findable; they do not vanish into the mass or read as rendering noise | | PENDING |
| LC-3 | Zoom in on one marked warrior in a Visayan-block faction | It wears the datu kit — gold-edged head wrap, gold earrings and necklace, draped shoulder cloth, red waist sash — and its immediate neighbours do not | | PENDING |
| LC-4 | Zoom in on one marked warrior in a Tagalog-block faction | It wears a chief or leader kit; if it is the red-chinina row, the red jacket is the single clearest cue at that zoom | | PENDING |
| LC-5 | Zoom in on a marked warrior in a Northern Luzon or generic-levy faction | It looks like its neighbours, and the above-head mark plus the inspector are the only identification. This is the designed outcome, not a defect | | PENDING |
| LC-6 | Watch until a marked warrior dies | Exactly one other warrior in that contingent picks up the mark, and its appearance changes once, cleanly, without flickering back and forth on subsequent frames | | PENDING |
| LC-7 | Click a marked warrior | The inspector states it is leading, and further down names the appearance preset with its scope tag and evidence tier, for example "Visayan Datu", Visayan, Documented, form uncertain | | PENDING |
| LC-8 | Click a marked warrior, then hover a second, while a third is breaking off under pressure | The leader mark, the selection ring, and the break-off band are all visible and none overlaps another | | PENDING |
| LC-9 | Click a warrior in a battle running the frozen `IndependentPursuitV1` preset, where `ContingentState` is `None` | No leadership row appears, because no leader is elected under that preset — and if one somehow is, the row appears rather than being silently dropped | | PENDING |
| LC-10 | Watch a full battle to its end, then open the battle report | The report is unchanged; its "Leaderboard" is still a kills top-ten and makes no claim about contingent leadership | | PENDING |
| LC-11 | Run the same seed twice and compare the same warrior at the same tick in both runs | Identical appearance and identical leader marks; nothing about who leads or how they look differs between runs | | PENDING |

## 6. Known failure modes for the implementing agents

Six ways this work goes wrong quietly. Each one produces a passing test run and
a broken feature, which is the most expensive kind of mistake in this
repository.

1. **The feature never fires because a call site was missed.** There are exactly
   two: `src/Hukbo.Client/ArenaGame.Rendering.cs:903-907` and
   `src/Hukbo.Client/UI/AgentInspectorPanel.cs:143-146`. If either keeps passing
   the default, leaders still look like everyone else and every unit test still
   passes. L3's source-scan assertion exists solely to make this loud.

2. **The cache serves a stale appearance after a leadership change.** This
   happens if `isLeader` is added to `PawnAppearanceFactory.Create` but not to
   the `PawnAppearanceCache` key. The symptom is a dead leader's successor
   keeping the rank-and-file look for the rest of the battle, visible only in a
   long watch. L2 owns both halves for that reason, and its cache test must
   exercise a flip on the same ordinal.

3. **The line numbers in this plan drift.** Every `file:line` reference here was
   read at base `14f104b`. A task that lands ahead of yours moves them. Confirm
   the anchor text before editing, and never trust a line number over the symbol
   name it was supposed to point at.

4. **Tool output in this environment is lossily compressed.** A file can render
   as mangled prose or syntactically invalid C# when the bytes on disk are
   correct. Confirm numerically — line counts, regex match counts — before
   reporting a file as damaged, and confirm an exact anchor string before an
   `Edit`. Never `Write` reconstructed content over a file you have only read
   through the compressed view.

5. **Re-pinning to go green.** Section 3 lists exactly which pins may move and
   which may not. A pin that moves outside that list means the change is wrong.
   In particular, a moved seed-1 state hash or event hash means simulation code
   was touched, and the fix is to remove the change, not to record a new
   baseline.

6. **Widening scope into `Hukbo.Core`.** Nothing in this plan needs it. A task
   that concludes otherwise has almost certainly found the historical-accuracy
   boundary or the derived-leadership design rather than a real gap, and must
   stop and report instead of editing.

Two conventions that apply to every task: Conventional Commits
(`<type>: <description>`), and diffs scoped to the requested change. Coding
tasks run on Sonnet.

