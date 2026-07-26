# Spectator Clarity Implementation Plan

> **For Claude:** Work this plan task by task. Use the `hukbo-verify-and-record` skill to run the canonical gate and record evidence; use `hukbo-determinism-change` for any `Hukbo.Core` edit and `hukbo-client-ui` for any `Hukbo.Client` edit.

**Goal:** Make battles understandable through persistent agent selection, an
agent inspector, a bounded event log, always-visible Play/Pause/Menu controls,
and a deterministic end-of-match summary with same-seed replay.

**Architecture:** Keep `Hukbo.Core` completely authoritative and
unchanged unless source evidence proves a missing read-only value. Add
GPU-independent presentation state under the Client, cover it with a dedicated
Client test project, add small MonoGame UI components, and let `ArenaGame`
coordinate input, fixed simulation advancement, event ingestion, and rendering.

**Tech Stack:** .NET SDK 10.0.302, C# 14, MonoGame DesktopGL 3.8.5, xUnit
2.9.3, PowerShell 7, Windows x64.

---

## 1. Handoff contract

This document is intended to be executable by an orchestration agent in a new
Codex session. The approved design is:

`docs/plans/2026-07-26-spectator-clarity-design.md`

The design controls product behavior. This plan controls implementation order
and evidence. If the two appear to disagree:

1. stop the affected task;
2. verify the current source;
3. prefer the approved design;
4. record the plan correction before continuing; and
5. do not silently expand scope.

### Ready-to-paste instruction for the next session

```text
Execute docs/plans/2026-07-26-spectator-clarity.md task-by-task.
First read the linked approved design, AGENTS.md, README.md, and the current
source files named in Phase 0. Use the executing-plans skill. Act as the
orchestrator: create the minimum non-overlapping workers exactly as described,
retain ownership of integration files, and run every local evidence gate.
Do not create GitHub Actions or another hosted CI system. Preserve untracked
repository-owner files. Stop before any scope change that alters Core gameplay.
```

### Locked scope

Implement:

- click-to-select living arena agents;
- persistent selected-agent inspector;
- ordered, deduplicated, scrollable, 200-entry battle event feed;
- always-visible Play, Pause, and Menu buttons;
- terminal winner/survivor/duration/tick/seed summary;
- Replay Same Seed;
- automated presentation-state tests;
- direct Windows manual UI checklist and evidence;
- local-only build, test, benchmark, package, and review.

Do not implement:

- morale, retreat, squads, formations, terrain, or pathfinding;
- gameplay balance changes;
- save files, serialized replay, timeline scrubbing, or event export;
- networking, services, telemetry, or accounts;
- new visual-asset pipeline, animation framework, or UI framework;
- GitHub Actions or any other hosted CI;
- non-Windows packaging.

### Repository integrity rules

- Preserve `RESEARCHED.md` and `SIMULATION-GAME-STANDARDS.md` if they remain
  untracked or user-owned.
- Do not reset, clean, stash, or discard unrelated work.
- Use a new branch named `codex/spectator-clarity`.
- If workers write concurrently, give each an isolated worktree or branch.
- One writable file has one owner at a time.
- Do not rewrite Core event behavior to make the UI easier.
- Do not claim manual behavior passed from compilation or synthetic input.
- Do not weaken deterministic hashes or existing tests.

## 2. Objective success criteria

The implementation is done only if all statements are true:

1. A primary click selects the nearest living agent within the pick radius.
2. Exact-distance selection ties resolve to the lower entity ID.
3. An empty-arena click clears selection.
4. UI clicks never click through to the arena.
5. Selection persists when the selected agent dies.
6. Reset/replay clears selection.
7. The inspector displays ID, faction, alive/dead, health, intent, target, and
   position from the current authoritative `AgentView`.
8. Every published `LastEvents` item is ingested after every advanced tick,
   including frames that advance multiple ticks.
9. The event feed is sequence ordered, deduplicated, capped at 200, and
   scrollable.
10. Event-log wheel input does not zoom the arena.
11. Play, Pause, and Menu are always visible and share one playback command
    boundary with keyboard and modal actions.
12. Opening Menu pauses; modal Play resumes/closes; modal Pause stays
    open/paused; Exit Game closes once.
13. A terminal outcome pauses and shows correct winner, survivors, tick,
    simulated duration, and seed.
14. Replay Same Seed creates a fresh paused match, clears disposable UI state,
    and preserves the deterministic outcome and hashes.
15. All focused tests, `./scripts/verify.ps1`, Windows packaging, direct manual
    smoke, and independent Critical/High review gates pass.
16. No hosted-CI workflow or hosted-CI completion gate is added.

## 3. Known baseline

Verify these facts against the current source before editing:

- solution: `Hukbo.slnx`;
- Client integration: `src/Hukbo.Client/ArenaGame.cs`;
- input edges: locate the class named `InputEdges` under
  `src/Hukbo.Client/`;
- current modal: `src/Hukbo.Client/MenuOverlay.cs` and
  `src/Hukbo.Client/MenuButton.cs`;
- camera transforms: locate the class named `SpectatorCamera` under Client;
- authoritative views: `src/Hukbo.Core/Simulation/AgentView.cs`;
- authoritative events:
  `src/Hukbo.Core/Simulation/BattleEvent.cs`;
- fixed scheduler and `LastEvents`:
  `src/Hukbo.Core/Simulation/BattleSimulation.cs`;
- scenario seed/tick rate:
  `src/Hukbo.Core/Simulation/Scenario.cs`;
- existing tests: `tests/Hukbo.Core.Tests/`;
- local gates: `scripts/test.ps1`, `scripts/verify.ps1`,
  `scripts/package.ps1`;
- manual checklist: `docs/development/testing.md`.

The last recorded seed-1, 200-agent baseline was:

| Value | Baseline |
| --- | --- |
| Outcome | Faction 1 victory |
| Terminal tick | 235 |
| State hash | `210C5EF8E7BE4D48` |
| Event hash | `CE35EDA4B2A4E5A4` |
| Existing tests | 42 passing |

Timing is machine-dependent. Hashes, outcome, tick, and test count are objective
baseline values. The total test count must increase after Client tests are
added.

## 4. Orchestrator–worker topology

Use three workers plus the orchestrator only while their work is independent.
Do not create more workers merely to increase concurrency.

### Worker A — presentation state and focused tests

```text
Agent: Presentation State owner
Objective: Implement GPU-independent selection, event-feed, summary, and
playback state with focused tests.
Inputs: Approved design, AgentView, BattleEvent, BattleOutcome, Scenario.
Owned files or subsystem:
  src/Hukbo.Client/Presentation/**
  tests/Hukbo.Client.Tests/**
Expected output: Tested pure presentation contracts with no window creation.
Success condition: Focused Client tests pass; no Core behavior changes.
Dependencies: Orchestrator first creates the test project and grants ownership.
Prohibited scope: ArenaGame.cs, UI rendering classes, Core, scripts, root files,
docs, menu files.
```

### Worker B — MonoGame UI components

```text
Agent: Spectator UI owner
Objective: Implement reusable button behavior, control bar, inspector, event
panel, and summary panel.
Inputs: Approved design, existing MenuOverlay/MenuButton, presentation contracts
from Worker A.
Owned files or subsystem:
  src/Hukbo.Client/UI/**
  src/Hukbo.Client/MenuOverlay.cs
  src/Hukbo.Client/MenuButton.cs
Expected output: Rendering/input components that return commands and do not
advance or mutate Core.
Success condition: Client compiles; pointer consumption and panel bounds are
explicit; modal behavior is preserved.
Dependencies: May scaffold layout/button code early; command/data integration
waits for Worker A contracts.
Prohibited scope: ArenaGame.cs, Presentation/**, tests, Core, scripts, root
files, docs.
```

### Worker C — QA and documentation evidence

```text
Agent: QA and Evidence owner
Objective: Prepare and later complete the manual test record, operating docs,
and traceability matrix.
Inputs: Approved design, this plan, current testing/readiness docs, final
verification output supplied by the orchestrator.
Owned files or subsystem:
  docs/development/**
  docs/agents/**
  docs/repository-readiness-report.md
Expected output: Exact controls, smoke checklist, observed results, unresolved
limitations, and evidence mapping.
Success condition: Docs distinguish automated, manual, and unrun checks; every
acceptance criterion maps to evidence.
Dependencies: Can draft before integration; final statuses wait for actual
orchestrator results and user-performed manual interaction.
Prohibited scope: Source, tests, scripts, root files, CI, plan/design files.
```

### Orchestrator ownership

The orchestrator exclusively owns:

- `Hukbo.slnx`;
- `Directory.Build.props`;
- `Directory.Packages.props`;
- `scripts/**`;
- `src/Hukbo.Client/ArenaGame.cs`;
- any Client assembly-visibility file;
- worker integration/cherry-picks;
- final verification;
- final diff and independent review;
- README and plan corrections.

The orchestrator may temporarily transfer a file only by recording the transfer
and ensuring the previous owner is idle.

### Dependency sequence

```text
Phase 0 baseline
   |
   v
Phase 1 test project (orchestrator)
   |
   +--------> Worker A presentation state --------+
   |                                              |
   +--------> Worker B UI scaffolding ------------+--> Phase 5 integration
   |
   +--------> Worker C checklist draft -----------+
                                                  |
                                                  v
                                      Verification and review
                                                  |
                                                  v
                                         Evidence finalization
```

## 5. Phase 0 — bootstrap and baseline evidence

### Task 0.1: Create the implementation branch

**Owner:** Orchestrator

Run:

```powershell
git status --short
git branch --show-current
git switch -c codex/spectator-clarity
```

Expected:

- only known user-owned/untracked files appear before work;
- the new branch starts from the committed plan/local-policy baseline;
- no file is staged by the branch command.

If the branch already exists, inspect it rather than force-resetting it.

Commit: none.

### Task 0.2: Read the controlling files

**Owner:** Orchestrator and all workers

Read in this order:

1. `AGENTS.md`;
2. `docs/plans/2026-07-26-spectator-clarity-design.md`;
3. this plan;
4. `README.md`;
5. the source files listed in Section 3;
6. `docs/development/testing.md`;
7. `SIMULATION-GAME-STANDARDS.md` only as an owner-provided reference, never
   as permission to expand this phase.

Use the repository knowledge graph for symbol discovery, then verify current
source before editing.

### Task 0.3: Run the clean baseline

**Owner:** Orchestrator

Run:

```powershell
./scripts/verify.ps1
git diff --check
git status --short
```

Expected:

- locked restore succeeds;
- formatting succeeds;
- Release solution build succeeds with 0 warnings/errors;
- 42 Core tests pass;
- 200-agent deterministic workload reports the recorded outcome/tick/hashes;
- no generated output becomes a tracked diff.

If a baseline fails, classify it before implementation. Do not ask a UI worker
to compensate for an environment, restore, or pre-existing failure.

Record the exact output in a temporary orchestrator note, not a committed debug
file.

### Task 0.4: Establish isolated ownership

**Owner:** Orchestrator

Create worker branches/worktrees only after the baseline passes. Give each
worker the ownership block in Section 4 verbatim. Require each worker to:

- report files changed;
- report exact commands/results;
- avoid commits touching another owner's files;
- stop on contract ambiguity;
- leave user-owned files untouched.

No implementation commit yet.

## 6. Phase 1 — Client test surface and contracts

### Task 1.1: Add the Client test project

**Owner:** Orchestrator

**Files:**

- Create:
  `tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj`
- Create:
  `src/Hukbo.Client/Properties/AssemblyInfo.cs`
- Modify: `Hukbo.slnx`
- Modify: `scripts/test.ps1`

The test project should:

- inherit `net10.0`;
- set `IsPackable` to false and `IsTestProject` to true;
- use `RuntimeIdentifier` `win-x64` if required by the Client project reference;
- reference the Client project;
- use the already-centralized `Microsoft.NET.Test.Sdk`, `xunit`, and
  `xunit.runner.visualstudio` packages;
- include the same runner `IncludeAssets`/`PrivateAssets` policy as Core.Tests;
- include a global xUnit using;
- never instantiate `ArenaGame`, `GraphicsDevice`, `SpriteBatch`, or a window.

Expose Client internals only to `Hukbo.Client.Tests`:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Hukbo.Client.Tests")]
```

Update `scripts/test.ps1` to discover/run both committed test projects. Prefer
an explicit array so the local contract remains reviewable:

```powershell
$testProjects = @(
    'tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj'
    'tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj'
)
```

Restore/build each when `-NoBuild` is absent, then test each with
`--no-build --no-restore`. End with a message that says repository tests, not
only Core tests.

### Task 1.2: Prove the empty test project runs

Run:

```powershell
dotnet restore tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj --locked-mode
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release
./scripts/test.ps1 -Configuration Release
```

Expected:

- a lock file is generated and committed for the new test project;
- the empty/foundation Client test assembly loads without opening a window;
- all existing Core tests still pass.

If project-reference RID compatibility fails, add the minimum matching
`RuntimeIdentifier` to the test project. Do not split presentation state into
Core to work around a Client test configuration issue.

### Task 1.3: Commit the test surface

Inspect:

```powershell
git diff --check
git diff -- Hukbo.slnx scripts/test.ps1 `
  src/Hukbo.Client/Properties/AssemblyInfo.cs `
  tests/Hukbo.Client.Tests
```

Commit:

```powershell
git add Hukbo.slnx scripts/test.ps1 `
  src/Hukbo.Client/Properties/AssemblyInfo.cs `
  tests/Hukbo.Client.Tests
git commit -m "test(client): add presentation test surface"
```

## 7. Phase 2 — presentation state, test first

Worker A owns every file in this phase.

### Task 2.1: Define shared presentation contracts

**Create:**

- `src/Hukbo.Client/Presentation/ClientCommand.cs`
- `src/Hukbo.Client/Presentation/PlaybackController.cs`
- `tests/Hukbo.Client.Tests/PlaybackControllerTests.cs`

Use one command enum across compact controls, summary, and modal translation:

```csharp
internal enum ClientCommand
{
    None,
    Play,
    Pause,
    OpenMenu,
    ReplaySameSeed,
    Exit,
}
```

`PlaybackController` owns only logical Play/Pause state. It must provide
explicit `Play`, `Pause`, and `Toggle` operations. The orchestrator remains
responsible for clearing the simulation accumulator whenever the state changes.
Do not put `Game`, menu visibility, or simulation advancement in this class.

Write failing tests first:

- initial state is paused;
- Play makes it playing;
- Pause makes it paused;
- Toggle changes state exactly once;
- applying the same state is idempotent.

Run before implementation:

```powershell
dotnet test tests/Hukbo.Client.Tests -c Release `
  --filter FullyQualifiedName~PlaybackControllerTests
```

Expected before code: compilation/test failure for missing contracts.

Implement minimally, rerun, expect all focused tests passed.

### Task 2.2: Implement deterministic agent selection

**Create:**

- `src/Hukbo.Client/Presentation/AgentSelection.cs`
- `tests/Hukbo.Client.Tests/AgentSelectionTests.cs`

Recommended contract:

```csharp
internal sealed class AgentSelection
{
    public ulong? SelectedEntityId { get; }

    public void SelectNearest(
        IReadOnlyList<AgentView> agents,
        int pointerXRaw,
        int pointerYRaw,
        long maximumDistanceSquared);

    public AgentView? Resolve(IReadOnlyList<AgentView> agents);
    public void Clear();
}
```

Behavior:

- candidates for a new click must be alive;
- use checked `long` deltas and squared distance;
- accept candidates at exactly the radius;
- lower squared distance wins;
- lower entity ID breaks exact ties;
- no candidate clears selection;
- `Resolve` must search all views, including dead agents;
- `Resolve` returns null only if no ID is selected or the ID is absent;
- `Clear` is idempotent.

Write these failing tests:

1. `SelectNearest_SelectsOnlyCandidateWithinRadius`
2. `SelectNearest_SelectsClosestCandidate`
3. `SelectNearest_UsesEntityIdAsDistanceTieBreaker`
4. `SelectNearest_IgnoresDeadCandidates`
5. `SelectNearest_ClearsSelectionForEmptyClick`
6. `Resolve_ReturnsSelectedAgentAfterDeath`
7. `Clear_RemovesSelection`

Use directly constructed `AgentView` records. No camera or MonoGame type belongs
in these tests.

Run focused tests, implement, rerun.

### Task 2.3: Implement the bounded event feed

**Create:**

- `src/Hukbo.Client/Presentation/BattleEventFeed.cs`
- `tests/Hukbo.Client.Tests/BattleEventFeedTests.cs`

Required API behavior:

- constructor accepts capacity; default caller uses 200;
- reject capacity less than 1;
- `Ingest(IReadOnlyList<BattleEvent>)` consumes increasing authoritative
  sequences;
- repeated `LastEvents` input does not duplicate entries;
- older already-seen sequence numbers are ignored;
- retained entries remain increasing by sequence;
- evict exactly the oldest entries when capacity is exceeded;
- expose an immutable/read-only entry view;
- track whether the view is pinned to the bottom;
- expose a clamped scroll start/window for a supplied visible-row count;
- `Clear` removes entries, resets last-sequence tracking, and pins to bottom.

Write failing tests:

1. `Constructor_RejectsNonPositiveCapacity`
2. `Ingest_PreservesSequenceOrder`
3. `Ingest_DeduplicatesRepeatedLatestTick`
4. `Ingest_EvictsOldestBeyondCapacity`
5. `Ingest_MultipleTicksRetainsEveryPublishedEvent`
6. `Scroll_ClampsAtOldestAndNewest`
7. `Ingest_StaysAtBottomWhenPinned`
8. `Ingest_DoesNotStealPositionWhenScrolledUp`
9. `Clear_ResetsHistoryAndSequence`

Do not fabricate synthetic sequence numbers. Test events should use explicit
monotonic sequences and ticks.

### Task 2.4: Implement match-summary derivation

**Create:**

- `src/Hukbo.Client/Presentation/MatchSummary.cs`
- `src/Hukbo.Client/Presentation/MatchSummaryFactory.cs`
- `tests/Hukbo.Client.Tests/MatchSummaryFactoryTests.cs`

Recommended immutable display model:

```csharp
internal sealed record MatchSummary(
    string WinnerLabel,
    int BlueSurvivors,
    int RedSurvivors,
    long TerminalTick,
    double SimulatedDurationSeconds,
    ulong Seed);
```

Factory requirements:

- reject `BattleOutcome.Ongoing`;
- reject non-positive tick rate;
- count only alive agents;
- faction 0 label is `Blue`;
- faction 1 label is `Red`;
- draw label is `Draw`;
- duration is `(double)tick / tickRate`;
- do not read wall clock;
- do not mutate or cache Core state.

Write failing tests:

1. Faction 0 victory label and counts;
2. Faction 1 victory label and counts;
3. draw label;
4. exact duration calculation;
5. seed/tick passthrough;
6. ongoing outcome rejection;
7. invalid tick-rate rejection.

### Task 2.5: Worker A verification and handoff

Run:

```powershell
dotnet format Hukbo.slnx --verify-no-changes
dotnet test tests/Hukbo.Client.Tests -c Release
./scripts/test.ps1 -Configuration Release
git diff --check
```

Commit only Worker A files:

```powershell
git add src/Hukbo.Client/Presentation `
  tests/Hukbo.Client.Tests
git commit -m "feat(client): add spectator presentation state"
```

Handoff must state:

- commit ID;
- contracts added;
- focused and combined test totals;
- any contract decision that differs from the recommendation;
- confirmation that no window was created and no Core file changed.

## 8. Phase 3 — MonoGame UI components

Worker B owns every file in this phase. It may start non-contract scaffolding in
parallel with Phase 2, but it must rebase/integrate the final Worker A contracts
before completion.

### Task 3.1: Consolidate button behavior

**Create or move:**

- `src/Hukbo.Client/UI/UiButton.cs`

**Modify as needed:**

- `src/Hukbo.Client/MenuOverlay.cs`
- `src/Hukbo.Client/MenuButton.cs`

Prefer replacing `MenuButton` with a reusable `UiButton` if that produces a
smaller total implementation. A button must hold:

- label;
- `ClientCommand`;
- bounds;
- enabled, hovered, focused, and pressed visual state.

Keep pointer edge detection in `InputEdges`. A press fires only on the left
button's transition to down while the pointer is inside an enabled button.
Holding the mouse must not issue repeated commands.

Preserve keyboard focus and modal behavior. Translate existing modal actions to
`ClientCommand` or migrate the modal directly; do not maintain two parallel
action enums after integration.

### Task 3.2: Add the always-visible control bar

**Create:**

- `src/Hukbo.Client/UI/ControlBar.cs`

Required behavior:

- lays out Play, Pause, and Menu from current viewport bounds;
- exposes its occupied rectangle;
- returns at most one `ClientCommand` per update;
- visually distinguishes the active Play/Pause state;
- marks hover/press state with existing palette conventions;
- returns whether it consumed the pointer;
- does not mutate playback or menu state;
- draws with existing pixel texture and SpriteFont;
- remains visible below the modal backdrop when the menu is open, although the
  modal receives input first.

### Task 3.3: Add selected-agent inspector

**Create:**

- `src/Hukbo.Client/UI/AgentInspectorPanel.cs`

Required behavior:

- accepts `AgentView?`, never a mutable simulation;
- exposes panel bounds;
- displays `No agent selected` when null;
- displays ID, Blue/Red faction, ALIVE/DEAD, HP, intent, target, and position;
- formats raw position using `FixedPoint.Scale`;
- uses faction color for a narrow accent, not unreadable full-panel text;
- consumes pointer input within its bounds so an empty panel click cannot clear
  arena selection.

### Task 3.4: Add battle event log panel

**Create:**

- `src/Hukbo.Client/UI/BattleEventLogPanel.cs`
- optionally
  `src/Hukbo.Client/Presentation/BattleEventFormatter.cs`
  if Worker A and the orchestrator explicitly transfer ownership of that new
  file.

Required behavior:

- right-side panel with title and clipped logical row window;
- reads `BattleEventFeed`, never `BattleSimulation`;
- newest events appear at the bottom;
- formats each event kind without exposing record syntax;
- displays tick and meaningful source/target/value/faction;
- wheel over panel calls feed scroll and reports input consumed;
- wheel outside panel is not consumed;
- no event-row allocation inside the draw hot loop if a simple cached/formatted
  representation avoids it;
- panel remains usable at 1280x720.

Suggested text patterns:

```text
T00042  Blue #17 moved toward #133
T00057  Blue #17 hit Red #133 for 10
T00057  Red #133 took 10 damage
T00057  Red #133 died
T00235  Red wins
```

Movement events may be omitted from the visible feed only if the product owner
approves that change. The retained feed must still ingest them so authoritative
event continuity is preserved.

### Task 3.5: Add match summary panel

**Create:**

- `src/Hukbo.Client/UI/MatchSummaryPanel.cs`

Required behavior:

- hidden for null summary;
- centered over the arena content rectangle, not the full window including log;
- displays all summary fields;
- maps buttons to Replay Same Seed and Menu;
- reports pointer consumption;
- does not create simulations or mutate playback;
- renders above regular HUD panels and below the modal menu.

### Task 3.6: Worker B build and handoff

Run:

```powershell
dotnet build src/Hukbo.Client/Hukbo.Client.csproj `
  -c Release --no-restore
dotnet format Hukbo.slnx --verify-no-changes
git diff --check
```

Commit only owned UI/menu files:

```powershell
git add src/Hukbo.Client/UI `
  src/Hukbo.Client/MenuOverlay.cs `
  src/Hukbo.Client/MenuButton.cs
git commit -m "feat(ui): add spectator panels and controls"
```

If `MenuButton.cs` becomes obsolete, delete it in this commit. Do not leave an
unused duplicate.

Handoff must state:

- commit ID;
- components and returned command/input-consumption contracts;
- Release build result;
- confirmation that `ArenaGame.cs` and Core were not changed.

## 9. Phase 4 — QA checklist draft

Worker C may do this concurrently with Phases 2–3.

### Task 4.1: Expand the manual checklist

**Modify:**

- `docs/development/testing.md`

Add a `Spectator clarity smoke` subsection with a result table:

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |

Include every manual item in the approved design. Leave `Actual` as `Not run`
and `Status` as `PENDING` until direct interaction occurs.

Document:

- local-only verification policy;
- focused Client-test command;
- how wheel ownership differs over the event log and arena;
- how to identify same-seed replay success;
- that Exit Game is in the modal.

### Task 4.2: Create phase evidence report

**Create:**

- `docs/agents/18-spectator-clarity.md`

Required sections:

- Scope;
- Inputs inspected;
- Architecture boundary;
- Work completed by component;
- Automated verification;
- Manual verification;
- Deterministic regression result;
- Packaging result;
- Independent review findings;
- Status;
- Limitations;
- Next action.

Initial status is `IN PROGRESS`. Worker C must not claim results not yet
supplied by the orchestrator.

Do not commit final evidence yet. A draft commit is optional only if it clearly
contains `PENDING` placeholders.

## 10. Phase 5 — orchestrator integration in ArenaGame

Only begin after Worker A and Worker B handoffs pass their focused checks.

### Task 5.1: Integrate worker commits and run narrow checks

**Owner:** Orchestrator

Integrate Worker A, then Worker B. Resolve contract names at the boundary rather
than duplicating adapters.

Run after each integration:

```powershell
dotnet test tests/Hukbo.Client.Tests -c Release
dotnet build src/Hukbo.Client/Hukbo.Client.csproj `
  -c Release --no-restore
git diff --check
```

Reject worker changes that:

- touch prohibited files;
- add Core mutation;
- introduce a second playback state;
- bypass fixed-step scheduling;
- retain unbounded events;
- add packages without an approved need.

### Task 5.2: Add presentation and UI fields

**Modify:**

- `src/Hukbo.Client/ArenaGame.cs`

Add one instance each of:

- `PlaybackController`;
- `AgentSelection`;
- `BattleEventFeed` with capacity 200;
- `ControlBar`;
- `AgentInspectorPanel`;
- `BattleEventLogPanel`;
- `MatchSummaryPanel`.

Add nullable `MatchSummary` state. Remove `_isPlaying` after all call sites use
the playback controller. Keep `_simulationAccumulator`, `_speedMultiplier`,
`_exitRequested`, scenario, simulation, and camera ownership in `ArenaGame`.

The initial match must be paused. This makes the new control bar immediately
understandable and matches same-seed replay semantics.

### Task 5.3: Define viewport layout and input priority

Add one small layout calculation that returns:

- full viewport bounds;
- top control/status bar bounds;
- right event-log bounds;
- remaining arena content bounds;
- inspector bounds;
- summary bounds.

Input priority, highest first:

1. modal menu;
2. terminal summary;
3. control bar;
4. event log;
5. inspector;
6. arena selection;
7. camera.

Only the first layer that consumes a pointer action handles it.

Keyboard priority:

- Escape toggles menu at all non-exiting times;
- modal keyboard navigation owns arrows/W/S while visible;
- Space toggles playback only when modal is closed;
- speed/reset/camera shortcuts work only when modal is closed;
- `R` means reset/replay same scenario and must clear presentation state.

Pass the arena content rectangle to camera transforms. If the existing camera
only accepts `Viewport`, add the smallest rectangle-aware overload or translate
the coordinate origin at the ArenaGame boundary. Verify panning and fit behavior
after the right panel reduces arena width.

### Task 5.4: Integrate click selection

On a left-click not consumed by UI:

1. convert pointer screen location to arena world coordinates;
2. convert to fixed-point raw coordinates with checked/clamped arithmetic;
3. compute pick radius from zoom;
4. call `AgentSelection.SelectNearest`;
5. resolve the selected `AgentView` every frame for current inspector data.

Render:

- hover outline for the current nearest living agent;
- a stronger persistent outline for the selected agent while alive;
- if selected agent is dead, keep inspector data but do not draw a live dot.

Remove the current transient hover-detail line after the inspector covers the
same fields. Do not scan agents more times than necessary: a 200-agent linear
pick/resolve is acceptable, but avoid repeated full scans per panel.

### Task 5.5: Ingest events after every tick

Change the fixed-step loop from:

```csharp
_simulation.AdvanceOneTick();
```

to the logical sequence:

```csharp
_simulation.AdvanceOneTick();
_eventFeed.Ingest(_simulation.LastEvents);
```

This call must remain inside the `while` loop so a high speed multiplier or slow
rendered frame cannot drop intermediate ticks.

After ingestion:

- if outcome is terminal, pause;
- clear the accumulator;
- create summary once;
- leave final events and selection visible.

Do not ingest `LastEvents` again elsewhere unless feed deduplication is treated
as a safety net rather than the primary control flow.

### Task 5.6: Unify commands

Add one `ApplyClientCommand(ClientCommand command)` switch in `ArenaGame`:

- `None`: no action;
- `Play`: play if outcome is ongoing; reset accumulator; close modal;
- `Pause`: pause; reset accumulator; retain current modal visibility;
- `OpenMenu`: pause; reset accumulator; open modal;
- `ReplaySameSeed`: call the reset path and remain paused;
- `Exit`: call guarded `RequestExit`;
- default: throw `ArgumentOutOfRangeException`.

Escape-close behavior:

- closing the modal does not silently resume; it stays paused;
- the spectator must press Play/Space explicitly.

`ToggleMenu` should open with `OpenMenu`; closing should only close.

### Task 5.7: Make reset/replay atomic

Replace the current reset body with one method that:

1. creates a new simulation from the same `_scenario`;
2. pauses playback;
3. clears the accumulator;
4. clears selection;
5. clears the event feed and scroll position;
6. clears the match summary;
7. closes the modal only if the initiating command requires it;
8. preserves speed multiplier unless the approved design is amended;
9. preserves the current documented camera reset behavior.

There must be no frame where new Core state is displayed with old event/summary
state.

### Task 5.8: Render in stable layer order

Recommended order:

1. background;
2. arena/map;
3. agents and selection/hover outlines;
4. status/control bar;
5. inspector;
6. event panel;
7. terminal summary;
8. modal menu.

Keep one `SpriteBatch.Begin`/`End` unless clipping requires a second pass. If
using `GraphicsDevice.ScissorRectangle`, restore rasterizer/scissor state before
the next layer. Prefer logical row clipping over new GPU state for the first
implementation.

### Task 5.9: Add integration-level pure tests

**Modify/Create under:**

- `tests/Hukbo.Client.Tests/`

Add tests for the atomic presentation reset through a small GPU-independent
coordinator if one naturally exists. At minimum prove:

- terminal summary cannot be created for ongoing outcome;
- replay/reset clears selection and feed;
- playback returns paused;
- repeated terminal processing does not duplicate final events/summary.

Do not instantiate `ArenaGame` merely to reach these behaviors. If integration
logic cannot be tested without a window, extract only the smallest pure
coordinator; do not build a second application architecture.

### Task 5.10: Integration commit

Run:

```powershell
dotnet test tests/Hukbo.Client.Tests -c Release
dotnet build Hukbo.slnx -c Release --no-restore
dotnet format Hukbo.slnx --verify-no-changes
git diff --check
```

Inspect `ArenaGame.cs` as a whole for stale `_isPlaying`, duplicated hover text,
missed event-ingestion locations, and UI click-through.

Commit:

```powershell
git add src/Hukbo.Client tests/Hukbo.Client.Tests
git commit -m "feat(client): integrate spectator clarity"
```

## 11. Phase 6 — automated integration verification

### Task 6.1: Run focused tests

**Owner:** Orchestrator

```powershell
dotnet test tests/Hukbo.Client.Tests -c Release `
  --logger "console;verbosity=normal"
./scripts/test.ps1 -Configuration Release
```

Expected:

- every Client presentation test passes;
- all 42 pre-existing Core tests pass;
- no test creates a window;
- total test count equals 42 plus the exact committed Client-test count.

### Task 6.2: Run canonical local verification

```powershell
./scripts/verify.ps1
```

Expected:

- locked restore passes;
- format passes;
- Release build has 0 warnings/errors;
- both test projects pass;
- 200-agent seed-1 run still reports:
  - Faction 1 victory;
  - tick 235;
  - state hash `210C5EF8E7BE4D48`;
  - event hash `CE35EDA4B2A4E5A4`.

If hashes change, classify as an implementation defect unless an independently
reviewed Core change was explicitly approved. The spectator phase itself has no
reason to change them.

### Task 6.3: Run stress and package

```powershell
./scripts/benchmark.ps1 -Agents 500 -Ticks 10000 -Seed 1
./scripts/package.ps1 -Runtime win-x64
```

Expected:

- 500-agent run completes deterministically;
- self-contained package is rebuilt from a clean staging directory controlled
  by the script;
- executable exists at
  `artifacts/packages/client-win-x64/Hukbo.Client.exe`.

Do not commit artifacts.

### Task 6.4: Inspect repository integrity

```powershell
git diff --check
git status --short
git diff --stat
git diff
```

Verify:

- no `.github/workflows` file returned;
- user-owned untracked files remain untouched;
- no package/version change occurred without need;
- no Core gameplay file changed;
- no temporary logging, disabled check, placeholder, or generated artifact is
  tracked;
- every changed line maps to the approved phase.

## 12. Phase 7 — direct manual Windows verification

### Task 7.1: Launch from source

**Owner:** Orchestrator plus repository owner for direct interaction

Run:

```powershell
./scripts/run.ps1
```

Record actual observations; do not infer them.

### Task 7.2: Execute control checks

Perform:

1. Confirm initial state is paused.
2. Click Play and watch tick increase.
3. Click Pause and confirm tick stops.
4. Press Space twice and confirm one toggle per press.
5. Click Menu and confirm simulation pauses/modal opens.
6. In modal click Pause; confirm modal remains and tick remains unchanged.
7. In modal click Play; confirm modal closes and tick resumes.
8. Press Escape open/close; confirm closing stays paused.
9. Reopen menu and click Exit Game; confirm process returns exit code 0.
10. Relaunch and verify the window close button returns exit code 0.

### Task 7.3: Execute selection checks

Perform:

1. Click a living Blue agent.
2. Move pointer away; inspector must remain.
3. Compare inspector ID/faction/HP/intent/target to visible state.
4. Select a different Red agent; inspector updates once.
5. Click empty arena; inspector clears.
6. Select an agent likely to die and let it die; inspector shows DEAD and
   retains its final state.
7. Click inside inspector, control bar, log, and summary; selection must not
   unexpectedly change.

### Task 7.4: Execute event-log checks

Perform:

1. Play at 1x; event rows arrive in increasing tick/sequence order.
2. Switch to 4x; no visible ordering jump or duplicate rows appears.
3. Hover event log and scroll upward; arena zoom remains unchanged.
4. While scrolled up, let new events arrive; view does not jump to bottom.
5. Scroll back down; newest events are visible.
6. Hover arena and use wheel; camera zoom works.
7. Run long enough to exceed 200 events; UI remains responsive and oldest
   entries roll off.

### Task 7.5: Execute summary/replay checks

Perform:

1. Run to terminal outcome.
2. Confirm ticks stop.
3. Compare summary winner and survivor counts to the status line/arena.
4. Confirm terminal tick and seed.
5. Confirm duration equals terminal tick divided by tick rate.
6. Click Replay Same Seed.
7. Confirm selection, event log, and summary clear.
8. Confirm replay begins paused.
9. Click Play and run to completion.
10. Confirm the same winner, terminal tick, state hash, and event hash through
    the headless regression evidence.

### Task 7.6: Record manual evidence

Worker C updates the table in `docs/development/testing.md` and
`docs/agents/18-spectator-clarity.md` with:

- date;
- machine/platform;
- source commit;
- package/source launch path;
- actual result per row;
- pass/fail;
- any screenshot paths if the owner chooses to record them.

If the user cannot perform direct interaction in the session, keep manual gates
`PENDING` and repository status `CONDITIONALLY READY`. Do not block automated
work, but do not claim full completion.

## 13. Phase 8 — independent review

### Task 8.1: Assign a fresh reviewer

**Owner:** Orchestrator

Use a worker who did not author implementation. Review:

- approved design;
- full diff from the phase baseline;
- presentation contracts/tests;
- `ArenaGame` update/input ordering;
- UI pointer consumption;
- reset/replay atomicity;
- local verification and manual evidence.

Reviewer must classify findings:

- Critical;
- High;
- Medium;
- Low.

Required focus:

- missed events during multi-tick frames;
- duplicated playback authority;
- UI clicks reaching arena;
- dead selection disappearing;
- unbounded memory;
- incorrect terminal duration/counts;
- replay resuming unexpectedly;
- Core determinism change;
- window/input resource errors;
- stale menu semantics;
- unsupported readiness claims.

### Task 8.2: Resolve blocking findings

Resolve every Critical and High finding. Run the narrowest relevant test after
each fix, then rerun the full local gate.

Medium/Low:

- fix only if within immediate scope and low-risk;
- otherwise record in `docs/agents/18-spectator-clarity.md`;
- do not use review as permission for unrelated refactoring.

Use at most three cycles for the same failure mode. After three, change approach
or document the genuine blocker.

### Task 8.3: Reviewer sign-off

Require:

- final severity list;
- explicit statement whether any Critical/High remains;
- exact checks reviewed;
- any unverified manual behavior.

No implementation is `COMPLETE` while Critical/High findings remain.

## 14. Phase 9 — evidence, docs, and completion commit

### Task 9.1: Finalize operating documentation

**Owner:** Worker C, using orchestrator-supplied evidence

**Modify:**

- `README.md`
- `docs/development/testing.md`
- `docs/repository-readiness-report.md`
- `docs/agents/18-spectator-clarity.md`

README must document:

- Play/Pause/Menu bar;
- modal Exit Game;
- click selection;
- event-log scrolling behavior;
- summary/replay;
- exact run and local verification commands;
- local-only policy.

Readiness:

- use `READY` only if automated, package, direct manual, and review gates pass;
- use `CONDITIONALLY READY` if manual interaction remains pending;
- license remains a public-distribution limitation, not a local-development
  build failure;
- hosted CI is neither required nor pending.

### Task 9.2: Update evidence index

**Modify:**

- `docs/agents/README.md`

Add report 18 with its scope and final status.

### Task 9.3: Documentation verification

Run:

```powershell
rg -n -i "github actions|hosted ci|workflow_dispatch|\\.github/workflows" `
  README.md docs
git diff --check
./scripts/verify.ps1 -SkipBootstrap
```

Interpretation:

- historical foundation plans may retain clearly historical references;
- active README, testing, readiness, and agent 18 docs must not require hosted
  CI;
- all local checks must pass after doc integration.

### Task 9.4: Commit evidence

```powershell
git add README.md docs/development docs/repository-readiness-report.md `
  docs/agents
git commit -m "docs(qa): record spectator clarity verification"
```

Do not commit the manual table as passed unless direct interaction actually
occurred.

## 15. Final orchestrator checklist

Before reporting completion, inspect the final diff as one unit:

### Requirements

- [ ] Persistent click selection works.
- [ ] Dead selection remains inspectable.
- [ ] Empty click clears selection.
- [ ] Inspector fields are complete.
- [ ] Event feed is ordered/deduplicated/bounded.
- [ ] Every advanced tick feeds events.
- [ ] Event panel scroll ownership works.
- [ ] Control bar is always visible.
- [ ] Modal Exit remains available.
- [ ] Terminal summary is accurate.
- [ ] Replay Same Seed is paused and atomic.

### Architecture

- [ ] Core remains authoritative.
- [ ] No Core gameplay behavior changed.
- [ ] Presentation classes do not require a GPU/window.
- [ ] One playback state exists.
- [ ] One command boundary exists.
- [ ] UI layers consume pointer input in order.
- [ ] No UI framework or speculative abstraction was added.

### Verification

- [ ] Focused Client tests pass.
- [ ] Existing 42 Core tests pass.
- [ ] Canonical local verification passes.
- [ ] Baseline hashes/outcome/tick remain exact.
- [ ] 500-agent stress passes.
- [ ] Self-contained Windows package passes.
- [ ] Direct manual smoke is passed or honestly pending.
- [ ] Independent reviewer has no unresolved Critical/High finding.
- [ ] `git diff --check` passes.

### Scope and repository safety

- [ ] No GitHub Actions workflow exists.
- [ ] No hosted-CI gate is pending.
- [ ] User-owned files are preserved.
- [ ] No artifacts/debug output are tracked.
- [ ] No unrelated refactor is present.
- [ ] All obsolete menu/action types created by this change are removed.
- [ ] Documentation matches actual controls and evidence.

## 16. Expected commit sequence

Keep commits reviewable and in this order:

1. `test(client): add presentation test surface`
2. `feat(client): add spectator presentation state`
3. `feat(ui): add spectator panels and controls`
4. `feat(client): integrate spectator clarity`
5. optional narrow `fix(...)` commits for review findings
6. `docs(qa): record spectator clarity verification`

Do not squash away the test-first and component ownership boundaries before
review. The repository owner may squash later.

## 17. Completion report template

Use this exact structure for the next-session final report:

```text
Implemented:
- Persistent selection and selected-agent inspector.
- Bounded battle event log.
- Always-visible Play/Pause/Menu controls.
- Terminal summary and same-seed replay.

Verification:
- Client tests: [passed count / failed count].
- Core tests: [passed count / failed count].
- Canonical verify: [passed/failed and hashes].
- 500-agent stress: [passed/failed].
- win-x64 package: [passed/failed and exact path].
- Manual smoke: [passed/pending/failed, with evidence path].
- Independent review: [remaining findings by severity].

Key decisions:
- Core gameplay remained unchanged.
- Event history is bounded Client presentation state.
- Replay recreates the same scenario and begins paused.
- Verification remains local-only; hosted CI is deferred by owner decision.

Files changed:
- Presentation state: [paths].
- UI and integration: [paths].
- Tests: [paths].
- Documentation: [paths].

Unresolved:
- [None, or exact blocker/pre-existing failure/manual item].
```

If direct manual interaction is still pending, say the automated implementation
is complete but repository readiness remains conditional. Do not word that as a
fully verified game release.
