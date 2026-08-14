# Hide the battle event log by default — plan

Date: 2026-08-15
Branch: `hukbo-hide-event-log`
Base: `9f794ce`
Game: **Hukbo** only. No Sandata file is touched.

## Goal

The battle event log occupies the right-hand column of the Hukbo client on
every launch. A spectator who wants to watch the battle rather than read it has
no way to reclaim that width. This change makes the log hidden on launch and
gives it a toggle, so the arena starts at full width and the log returns on
demand.

## Decisions taken before implementation

1. **Not persisted.** The visibility flag lives in `ArenaGame` as a field and
   resets to hidden on every launch, exactly as `_isSoundLogVisible` already
   does. No `ClientSettings` field, no schema version bump, no settings-store
   migration. The precedent is the sound log, which has behaved this way since
   the sound system landed and has never needed persistence.
2. **A toggle ships with the change.** Hiding a panel with no way to show it is
   a regression, not a default. The toggle is both a control-bar button and a
   key, matching the sound log on both counts.
3. **The key is F8.** F9 is the sound log. F8 is unclaimed, and sitting the two
   log toggles next to each other on the function row is the discoverable
   arrangement.
4. **The button is labelled "Events" and sits immediately before "Sounds"**, so
   the two log toggles are adjacent in the bar. Button width is computed from
   the array length, so growing the bar from seven buttons to eight needs no
   width constant changed.
5. **The arena reclaims the column.** With both logs hidden the right column
   collapses to zero width and the arena extends to the right margin. This is
   the point of the change; a hidden log that leaves a blank gutter behind
   would deliver nothing.

## The layout contract

`RightColumnSplit.Split` gains a second flag. Its full truth table:

| `isEventLogVisible` | `isSoundLogVisible` | `EventBounds` | `SoundLogBounds` |
| --- | --- | --- | --- |
| true | true | top share of the column | bottom share, as today |
| true | false | the whole column | `Rectangle.Empty` |
| false | true | `Rectangle.Empty` | the whole column |
| false | false | `Rectangle.Empty` | `Rectangle.Empty` |

New signature:

```csharp
public static ColumnBounds Split(
    Rectangle columnBounds,
    bool isEventLogVisible,
    bool isSoundLogVisible,
    int soundLogMinimumHeight,
    int soundLogHeightPercent,
    int gap)
```

`ArenaGame.ComputeLayout` must not derive the arena's right edge from
`EventBounds` any more. With both logs hidden `EventBounds` is
`Rectangle.Empty`, whose `Left` is zero, and the existing
`arenaRight = eventBounds.Left - layoutGap` would collapse the arena to nothing.
The arena's right edge is derived from the *column rectangle* instead:

```csharp
var columnWidth = isEventLogVisible || isSoundLogVisible ? eventWidth : 0;
var columnRect = new Rectangle(
    Math.Max(screenBounds.Left, screenBounds.Right - columnWidth - layoutMargin),
    contentTop,
    columnWidth,
    contentHeight);
var arenaRight = Math.Max(
    screenBounds.Left + layoutMargin,
    columnWidth == 0
        ? screenBounds.Right - layoutMargin
        : columnRect.Left - layoutGap);
```

## Tasks

Three implementation tasks over non-overlapping file sets, run in parallel
against the contract above.

### Task A — geometry

Files: `src/Hukbo.Client/UI/RightColumnSplit.cs`,
`tests/Hukbo.Client.Tests/RightColumnSplitTests.cs`

- Add the `isEventLogVisible` parameter in the position shown above and
  implement the four-row truth table.
- Update the class doc comment: the "sound log hidden means the battle log
  keeps the whole column" sentence is now one row of four.
- Update every existing test call site for the new arity, preserving what each
  case asserted.
- Add cases for the two new rows: event hidden with sound visible, and both
  hidden.

### Task B — toggle plumbing

Files: `src/Hukbo.Client/Presentation/ClientCommand.cs`,
`src/Hukbo.Client/UI/ControlBar.cs`,
`tests/Hukbo.Client.Tests/ControlBarTests.cs`

- Append `ToggleEventLog` to `ClientCommand`, at the end, never inserted — the
  file's own comments record that no existing member's ordinal may move.
- Add `new("Events", ClientCommand.ToggleEventLog)` immediately before the
  `"Sounds"` entry.
- Add `bool isEventLogVisible` immediately after `bool isSoundLogVisible` in
  `Update`, `Draw`, `SynchronizeVisualState`, and `IsButtonActive`, and map
  `ClientCommand.ToggleEventLog => isEventLogVisible` in the active-state
  switch.
- Update existing test call sites for the new arity; add a case proving the
  Events button reports active only when the flag is set.

### Task C — client wiring

Files: `src/Hukbo.Client/ArenaGame.cs`,
`src/Hukbo.Client/ArenaGame.Rendering.cs`

- Add `private bool _isEventLogVisible;` beside `_isSoundLogVisible`. The
  default `false` is the whole feature.
- Bind F8 through `LogKeyCommand("F8", ClientCommand.ToggleEventLog)`, matching
  the F9 block.
- Add the `case ClientCommand.ToggleEventLog:` arm that flips the field.
- Thread the flag through `GetLayout`, `ComputeLayout`, and the
  `RightColumnSplit.Split` call, and rewrite the arena's right edge as shown in
  the layout contract.
- Guard `_eventLogPanel.Draw` behind the flag in `ArenaGame.Rendering.cs`, and
  pass the flag to `_controlBar.Draw` and `_controlBar.Update`.
- Skip the event log's pointer, focus, and escape handling while hidden, so a
  hidden panel can never consume a click or hold keyboard focus.

## Verification

- `./scripts/verify.ps1 -Game Hukbo`, output pasted into this document.
- Both test suites, since a Client change is involved.
- Smoke rows added to `docs/development/smoke-checklist.md` as `PENDING`: the
  log is absent on launch and the arena spans to the right margin; F8 shows it;
  the Events button shows it; with the log hidden and the sound log shown
  (F9), the sound log occupies the column alone.

## What was run

`./scripts/verify.ps1 -Game Hukbo -SkipBootstrap`, on branch
`hukbo-hide-event-log` at base `9f794ce`, exit code 0:

```
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
[PASS] Release repository tests completed.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

Test totals from the same run: 2,568 tests in one suite and 3,966 in the other,
with no failures. The five headless workloads are the gate's five seed-1
baselines, all `deterministic: true`; the change is presentation-only and moves
no simulation hash.

Rows `HEL-1` through `HEL-5` were added to
`docs/development/smoke-checklist.md` as `PENDING`. None of them may be closed
by anything short of a person watching a live battle at an interactive desktop.

## Deviation from the plan as first written

The layout contract above originally dropped the
`Math.Max(screenBounds.Left + layoutMargin, ...)` floor that the existing code
applied to the arena's right edge. The implementer flagged it rather than
silently matching the old behaviour, and the floor was restored in both the
code and the contract before the gate ran. The arena's width was already
clamped at zero downstream, so no shipped behaviour depended on the difference,
but the invariant is the one the file had before this change and it stays.
