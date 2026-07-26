# Battle Events Inspector Implementation Plan

> **For Claude:** Work this plan task by task. Use the `hukbo-verify-and-record` skill to run the canonical gate and record evidence; use `hukbo-determinism-change` for any `Hukbo.Core` edit and `hukbo-client-ui` for any `Hukbo.Client` edit.

**Goal:** Replace Hukbo's wheel-only Battle Events feed with a filterable split inspector that supports stable selection, event details, keyboard and mouse navigation, and explicit live-follow recovery.

**Architecture:** Keep simulation events immutable and presentation-only. Extend the bounded `BattleEventFeed` with deterministic filtered-view, selection, and live-follow state; keep drawing and hit testing in `BattleEventLogPanel`; route only the required input and bounds through `ArenaGame`. Reuse existing immediate-mode UI primitives and preserve the current pointer-consumption contract.

**Tech Stack:** .NET 10, C# 14, MonoGame DesktopGL 3.8.5, xUnit 2.9.3, repository PowerShell build/test scripts

---

### Task 1: Add Filtered Inspector State

**Files:**
- Modify: `src/Hukbo.Client/Presentation/BattleEventFeed.cs`
- Test: `tests/Hukbo.Client.Tests/BattleEventFeedTests.cs`

**Step 1: Write failing filter tests**

Add tests covering:

- no filters returns all retained entries in sequence order;
- kind, faction/actor, and case-insensitive text filters combine with AND
  semantics;
- clearing filters restores all entries;
- a no-match filter returns an empty view without changing retained entries.

Construct `BattleEvent` values directly and assert exact returned sequences.

**Step 2: Run the focused tests and verify failure**

Run:

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj --filter "FullyQualifiedName~BattleEventFeedTests"
```

Expected: FAIL because filter state and filtered entries do not exist.

**Step 3: Implement the smallest filter model**

Add presentation-layer filter state for:

- optional `BattleEventKind`;
- optional source faction or actor identifier;
- normalized text query.

Expose the retained entries and a filtered read-only view without mutating or
copying simulation events. Keep matching deterministic and presentation-only.
Use the panel's existing event formatting vocabulary for text matching through
a shared presentation formatter rather than duplicating strings.

**Step 4: Run the focused tests**

Run the same command.

Expected: PASS.

**Step 5: Commit**

```powershell
git add src/Hukbo.Client/Presentation/BattleEventFeed.cs tests/Hukbo.Client.Tests/BattleEventFeedTests.cs
git commit -m "feat(ui): add battle event filters"
```

### Task 2: Add Stable Selection and Live-Follow Navigation

**Files:**
- Modify: `src/Hukbo.Client/Presentation/BattleEventFeed.cs`
- Test: `tests/Hukbo.Client.Tests/BattleEventFeedTests.cs`

**Step 1: Write failing navigation tests**

Cover:

- selecting an event by underlying sequence;
- moving previous/next and first/last within filtered order;
- navigation clamps at boundaries;
- ingest while inspecting history preserves selection and scroll position;
- returning to latest selects the newest matching event and pins the list;
- filtering out or evicting the selection clears it safely;
- a no-match filter makes navigation a no-op.

**Step 2: Run the focused tests and verify failure**

Use the Task 1 focused test command.

Expected: FAIL because selection and inspector navigation do not exist.

**Step 3: Implement selection/navigation**

Store selection by event sequence, not filtered index. Add narrow operations for
selecting a visible event, moving selection, selecting the filtered endpoints,
and returning to latest. Preserve the existing retained capacity,
deduplication, scroll clamping, and bottom-pinning behavior.

**Step 4: Run the focused tests**

Expected: PASS.

**Step 5: Commit**

```powershell
git add src/Hukbo.Client/Presentation/BattleEventFeed.cs tests/Hukbo.Client.Tests/BattleEventFeedTests.cs
git commit -m "feat(ui): add battle event selection"
```

### Task 3: Build the Split Inspector Panel

**Files:**
- Modify: `src/Hukbo.Client/UI/BattleEventLogPanel.cs`
- Modify if shared formatting is extracted: `src/Hukbo.Client/Presentation/BattleEventFormatter.cs`
- Test: `tests/Hukbo.Client.Tests/BattleEventLogPanelTests.cs`

**Step 1: Extract testable layout and interaction calculations**

Write failing tests for pure/internal panel helpers:

- list and details rectangles remain within panel bounds at 1280x720 and the
  repository's smallest supported client size;
- row hit testing maps only visible list rows;
- scrollbar thumb reflects retained/visible range and remains usable;
- narrow values clip or wrap inside the details bounds;
- empty and no-match modes select the correct presentation state.

Avoid screenshot-pixel assertions. Test layout, hit targets, labels, and state.

**Step 2: Run panel tests and verify failure**

Run:

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj --filter "FullyQualifiedName~BattleEventLogPanelTests"
```

Expected: FAIL because the split layout/helpers are not implemented.

**Step 3: Implement the tactical split layout**

Update the panel to draw:

- title, filtered/total count, and live/inspecting status;
- compact kind, faction/actor, and search controls;
- a reset control only when filters are active;
- event rows with type accent, timestamp, actor, action, selected, hover, and
  keyboard-focus states;
- visible scrollbar track and thumb;
- contextual `Latest` control with new-event count/status;
- fixed lower details region with all available `BattleEvent` fields;
- no-events, no-match, and no-selection messages.

Reuse `UiButton` and `UiPrimitives` where practical. Keep colors aligned with
existing Hukbo panels, use text/shape in addition to color, and avoid
per-frame/per-row heap allocation in the draw path.

**Step 4: Implement panel interaction**

Preserve hover-only wheel ownership. Add row selection and panel-local keyboard
focus. Support Up, Down, Home, End, Escape/reset where compatible, clickable
filter cycling/reset/Latest controls, and bounded text-query entry with
Backspace. Do not consume game/camera input when the panel lacks focus.

**Step 5: Run focused feed and panel tests**

Run:

```powershell
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj --filter "FullyQualifiedName~BattleEvent"
```

Expected: PASS.

**Step 6: Commit**

```powershell
git add src/Hukbo.Client/UI/BattleEventLogPanel.cs src/Hukbo.Client/Presentation/BattleEventFormatter.cs tests/Hukbo.Client.Tests/BattleEventLogPanelTests.cs
git commit -m "feat(ui): build battle event inspector"
```

Only include `BattleEventFormatter.cs` if it was created.

### Task 4: Integrate Input and Responsive Bounds

**Files:**
- Modify: `src/Hukbo.Client/ArenaGame.cs`
- Modify if required: `src/Hukbo.Client/InputEdges.cs`
- Test: `tests/Hukbo.Client.Tests/BattleEventLogPanelTests.cs`

**Step 1: Write failing integration-boundary tests**

Cover any newly extracted panel-width/layout policy and input-focus transition.
Prove the panel remains inside the viewport and pointer/keyboard ownership is
released when focus leaves the inspector.

**Step 2: Run focused tests and verify failure**

Run the panel-test command from Task 3.

Expected: FAIL for the new integration expectations.

**Step 3: Wire the panel into the game loop**

Pass only the input edges/state required by the inspector. Preserve the
existing UI priority (summary, controls, event inspector, agent inspector),
camera wheel suppression when panel scrolling is consumed, right-side arena
separation, and reset behavior. Update help text only where it would otherwise
misdescribe the controls.

**Step 4: Run focused tests**

Expected: PASS.

**Step 5: Commit**

```powershell
git add src/Hukbo.Client/ArenaGame.cs src/Hukbo.Client/InputEdges.cs tests/Hukbo.Client.Tests/BattleEventLogPanelTests.cs
git commit -m "feat(client): integrate battle event inspector"
```

Only stage files actually changed.

### Task 5: Verify, Document, and Review

**Files:**
- Modify: `docs/agents/18-spectator-clarity.md`

**Step 1: Run repository verification**

Run:

```powershell
.\scripts\format.ps1 -Verify
.\scripts\build.ps1
.\scripts\test.ps1
```

Expected: all commands exit successfully. Classify any failure before changing
implementation and do not modify unrelated rename work to hide failures.

**Step 2: Run the game and visually inspect**

Run:

```powershell
.\scripts\run.ps1
```

Verify:

- filters can be combined and reset;
- click and keyboard selection update details;
- selection and scroll remain stable as new events arrive;
- `Latest` visibly restores live-follow;
- scrollbar and empty states are readable;
- pointer ownership prevents accidental camera zoom;
- panel content stays inside bounds at normal and reduced window sizes.

Record manual verification honestly; do not mark it passed if it cannot be
performed in the environment.

**Step 3: Update operating documentation**

Document the new Battle Events controls and replace only stale acceptance
statements directly affected by the implementation. Preserve the existing
Hukbo rename and unrelated documentation edits.

**Step 4: Inspect the final diff**

Confirm every changed line supports the inspector, required tests, or its
documentation. Ensure there are no Core/simulation changes, no retention
changes, no placeholder code, and no accidental staging of the dirty worktree.

**Step 5: Independent review**

Have a separate Review Agent classify findings as Critical, High, Medium, or
Low. Resolve every Critical and High finding and rerun affected checks.

**Step 6: Commit documentation/fixes**

```powershell
git add docs/agents/18-spectator-clarity.md
git commit -m "docs(ui): document battle event inspector"
```

Stage only files changed by this task. The Review Agent performs the final code
commits or confirms the implementation commits are clean.
