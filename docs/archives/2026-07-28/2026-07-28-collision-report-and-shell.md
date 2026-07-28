# Collision firmness, battle report, and window shell — plan

**Archived: reference only.** Implemented and verified on 2026-07-28; the
canonical gate passed with `stateHash A080E28DA7C79C20` and `eventHash
2B6FB3A9A9C1960D`. Do not execute this document and do not cite it as
justification for a change. Smoke rows 134 to 148 in
[docs/development/testing.md](../../development/testing.md) remain PENDING and
still need a human at an interactive desktop.

**Design:** [`2026-07-28-collision-report-and-shell-design.md`](2026-07-28-collision-report-and-shell-design.md).
Read it first. This document carries only the ordered task list and the
verification criteria; every decision and its reasoning lives in the design.

**Date:** 2026-07-28.

**Branch:** `unit-collision-and-battle-report`, in the worktree
`.claude/worktrees/unit-collision-and-battle-report`.

**Four goals, requested together, otherwise independent:**

1. Increase unit collision — `Hukbo.Core`, moves both hashes.
2. Per-unit battle report — `Hukbo.Client`, hash-neutral.
3. Remove the OS exit, minimize, and maximize buttons — `Hukbo.Client`, hash-neutral.
4. Enlarge the unit setup menu — `Hukbo.Client`, hash-neutral.

## One decision that needs the user, before or alongside merge

`SIMULATION-GAME-STANDARDS.md` lines 650 to 653 currently read:

> Because `BodyRadiusRaw`, `CollisionPolicy`, and `MovementResolution` all reach the
> state hash, and because constraining movement changes where agents stand, both the
> state hash and the event hash moved for every seed when this contract shipped.
> Changing any of those three fields in future requires a new preset version and new
> golden expectations.

Read literally, that forbids goal 1 as scoped, because goal 1 changes `BodyRadiusRaw`
without cutting a preset version. Section 7.2 of the design argues the sentence is
imprecise — a combat preset version protects combat content and is identified by
`CombatRuleset.ContentHash`, which `BodyRadiusRaw` does not feed, so a preset V4 with
byte-identical combat content would create the appearance of protection while
providing none, and an old replay naming V3 would still replay at the new radius.

Task C7 amends that sentence. It is an edit to a live contract document and is
isolated as its own task so it can be dropped without unpicking anything else. If the
user prefers the literal reading, the correct response is to abandon goal 1 as scoped
and reopen it as a combined preset-and-collision change — **not** to cut a cosmetic V4.

## Ownership

Four workstreams with non-overlapping file sets. Two agents editing one file in
parallel is a merge conflict created on purpose.

| Owner | Files |
| --- | --- |
| `core-collision` | `src/Hukbo.Core/**`, `tests/Hukbo.Core.Tests/**`, `SIMULATION-GAME-STANDARDS.md` |
| `battle-report` | `src/Hukbo.Client/Presentation/BattleReport*.cs` (new), `src/Hukbo.Client/UI/BattleReport*.cs` (new), `src/Hukbo.Client/UI/MatchSummaryPanel.cs`, `tests/Hukbo.Client.Tests/BattleReport*.cs` (new) |
| `unit-setup-menu` | `src/Hukbo.Client/Content/Themes/ui-theme-standards.json`, `src/Hukbo.Client/Theming/UiThemeCatalogFallback.cs`, `tests/Hukbo.Client.Tests/ArmyComposition*.cs` |
| `window-chrome` | `src/Hukbo.Client/ArenaGame.cs`, `src/Hukbo.Client/UI/ControlBar.cs`, `src/Hukbo.Client/Presentation/ClientCommand.cs`, `src/Hukbo.Client/Presentation/PresentationCoordinator.cs`, `tests/Hukbo.Client.Tests/ControlBar*.cs`, `tests/Hukbo.Client.Tests/PresentationCoordinator*.cs` |

`window-chrome` is also the integrator. `ArenaGame.cs`, `ClientCommand.cs`, and
`PresentationCoordinator.cs` are shared wiring surfaces, so every edit to them belongs
to that workstream regardless of which goal needs it, and it runs last and alone.

## Tasks

### Goal 1 — collision firmness (`core-collision`)

- [x] **C1.** Change `CollisionRules.DefaultBodyRadiusRaw` in
  `src/Hukbo.Core/Simulation/CollisionRules.cs` from `4 * FixedPoint.Scale` to
  `(17 * FixedPoint.Scale) / 4`. `FixedPoint.Scale` is 1,024 so the division is exact
  and the constant stays tied to the scale rather than becoming a magic number.
  Update the doc comment to state 4,352 raw units, 4.25 world units, diameter 8.5 against
  the 12-unit attack range.
  *Verification:* `dotnet build Hukbo.slnx -c Release` succeeds.

- [x] **C2.** Add a test asserting `CombatRuleset` construction still passes its reach
  validation against the enlarged floor: `MinimumProfileReachRawExclusive` is now
  8.5 world units and the shortest weapon in the V3 preset has reach 10, so the strict
  `>` check must still hold. This is the guard that would have blocked a 5.0-unit
  radius, and it now has one and a half world units of margin.
  *Verification:* new test passes.

- [x] **C3.** Add a test asserting the canonical 200-agent, 1280 × 720 scenario still
  passes `Scenario.Validate()` at the new radius, covering the attack-range,
  movement-speed, map-dimension, and body-density guards in one place.
  *Verification:* new test passes.

- [x] **C4.** Assert the three combat preset content hashes are **unchanged**:
  V1 `0x59FB4CA563D87A49`, V2 `0x10AB1CC226AB3636`, V3 `0xCD790E489293B304`. If any
  moves, something was edited that should not have been. This is the test that proves
  the "no new preset version" claim rather than asserting it.
  *Verification:* existing or new content-hash test passes with the values above.

- [x] **C5.** Regenerate the golden expectations in
  `tests/Hukbo.Core.Tests/DeterminismTests.cs`. Every pinned literal must be replaced
  with a value a real run actually printed. Do not invent a number and do not edit a
  test to match a guess. Trace each fixture's provenance first — some come from direct
  `ComputeStateHash` calls and some from JSON digest fixtures — and record which is
  which, so a bulk regeneration does not silently bake in a value nobody verified.
  *Verification:* `dotnet test tests/Hukbo.Core.Tests -c Release` passes; every changed
  literal traceable to captured output.

- [x] **C6.** Confirm the SplitMix64 pinned vectors are **untouched**. They are pinned
  against the algorithm, not against any scenario value, so this change must not move
  them.
  *Verification:* existing determinism vector tests pass unmodified.

- [x] **C7.** Amend `SIMULATION-GAME-STANDARDS.md` lines 650 to 653 per the section
  above and design section 7.2: state that changing `BodyRadiusRaw`, `CollisionPolicy`,
  or `MovementResolution` invalidates every recorded golden expectation and requires a
  deliberate rebaseline recorded in the same commit, and add the explicit note that
  combat preset versioning does not and cannot cover scenario collision defaults.
  **Flagged for the user — this edits a live contract.**
  *Verification:* prose only; no code effect.

- [x] **C8.** Update the three further passages in `SIMULATION-GAME-STANDARDS.md` that
  state the old numbers as fact: the collision-rule table listing `BodyRadiusRaw` as
  4096 and body diameter as 8192; the contact-metric paragraph deriving a proximity
  band of 5632 raw per body and a broad-phase pairing distance of 11264, which become
  5888 and 11776 at the new radius; and the recorded 200- and 500-agent contact
  figures. The recorded contact counts are measurements of a past run, so mark them
  **superseded and re-measure** rather than editing the old numbers in place.
  *Verification:* prose; re-measured figures come from a real run or the row stays
  marked superseded and unfilled.

- [x] **C9.** Check whether any digest or control run pins the old radius implicitly,
  per design section 7.1, and pin it explicitly where one does, so a control run that
  is supposed to hold the radius constant does not silently drift with the default.
  *Verification:* named in the task report, with the file and line, or explicitly
  reported as not applicable.

### Goal 2 — battle report (`battle-report`)

- [x] **R1.** New `src/Hukbo.Client/Presentation/BattleReportAccumulator.cs`.
  `internal sealed class` with `Ingest(IReadOnlyList<BattleEvent> events)`, `Clear()`,
  and `Snapshot(long terminalTick)`. Internal state: a per-entity dictionary, a
  two-element faction array, and nullable first-blood and decisive-kill tuples.
  **It must fold events into running counters and never retain the event list** — the
  list is double-buffered by the simulation and reusing the reference across ticks is a
  correctness bug, not just a style issue.
  *Verification:* R6 tests.

- [x] **R2.** Implement the twelve statistics from design section 2. Kill credit goes
  to the last attacker to land a hit on the victim in the tick the victim died, ties
  broken on ascending `EntityId`. A `Death` with no preceding landed attack in that
  tick must leave the kill uncredited and must not throw. Document kill attribution at
  the symbol as a derived presentation heuristic, not a Core-guaranteed concept.
  *Verification:* R6 tests, including the uncredited-death edge case.

- [x] **R3.** New `src/Hukbo.Client/Presentation/BattleReport.cs` — immutable record
  plus `UnitReportRow` and `FactionReportTotals`. Leaderboard sorted by kills, then
  damage dealt, then ascending `EntityId`, capped at ten rows.
  *Verification:* R6 tests.

- [x] **R4.** New `src/Hukbo.Client/UI/BattleReportLayout.cs` — a pure static
  `Calculate(Rectangle)` returning a record of rectangles: header, close button,
  faction totals, highlights, leaderboard header, leaderboard list, scrollbar. No
  `GraphicsDevice`, no `SpriteBatch`. It must replicate `MatchSummaryPanel.Layout`'s
  clamping against the available bounds rather than assuming fixed constants, or it
  overflows at the smallest supported viewport.
  *Verification:* R7 tests.

- [x] **R5.** New `src/Hukbo.Client/UI/BattleReportPanel.cs` — 720 × 560, minimum width
  480. Four sections: header with close button; faction totals, two lines; highlights,
  up to four lines, each **omitted rather than blanked** when its field is null; and a
  scrolling kill leaderboard of up to ten rows. Reuse `BattleEventLogPanel`'s existing
  row-height and scrollbar-width constants and its visible-row-count clipping approach
  rather than inventing a second scroll mechanism. Every weapon name renders through
  `BattleEventFormatter.GetWeaponLabel(WeaponId, ShieldId)` — never a bare name, per
  `CLAUDE.md` section 7.
  *Verification:* R7 tests plus manual smoke.

- [x] **R6.** New `tests/Hukbo.Client.Tests/BattleReportAccumulatorTests.cs`: kill
  credited to the last landed attacker; simultaneous kills tie-broken by lowest
  `EntityId`; first blood recorded once and never overwritten; decisive kill
  overwritten on every death; faction totals accumulated; a death with no landed attack
  leaves the kill uncredited without throwing; `Clear` resets everything; leaderboard
  sorted and capped at ten.
  *Verification:* all pass.

- [x] **R7.** New `tests/Hukbo.Client.Tests/BattleReportLayoutTests.cs` — pure-helper
  layout assertions with no graphics device, following `BattleEventLogPanelTests`.
  *Verification:* all pass.

- [x] **R8.** `src/Hukbo.Client/UI/MatchSummaryPanel.cs`: `PreferredHeight` 310 → 368,
  adding a full-width "BATTLE REPORT" button below the existing two-button row. The
  added height is `ButtonHeight 44 + ButtonGap 14 = 58`. Existing `NextRound` and
  `OpenMenu` geometry is unchanged. Full width is `(ButtonWidth 198 × 2) + ButtonGap 14
  = 410`.
  *Verification:* updated `MatchSummaryPanelTests` assert the new height and the new
  command.

### Goal 4 — unit setup menu (`unit-setup-menu`)

- [x] **M1.** `src/Hukbo.Client/Content/Themes/ui-theme-standards.json` lines 107 to
  114: `panelWidth` 420 → 640, `stepperWidth` 260 → 148. `panelHeight` 648,
  `rowHeight` 44, `rowGap` 8, and `arrowWidth` 44 all unchanged.
  *Verification:* M3.

- [x] **M2.** `src/Hukbo.Client/Theming/UiThemeCatalogFallback.cs` lines 120 to 126:
  `UiArmyCompositionLayout(420, 648, 44, 8, 260, 44)` →
  `UiArmyCompositionLayout(640, 648, 44, 8, 148, 44)`. **M1 and M2 must land together.**
  An existing test asserts the JSON and the fallback are equal, so changing one alone
  fails the build — which is the desired behaviour. `UiThemeCatalog.cs:648` also
  constructs this record but reads through from the JSON and needs no edit.
  *Verification:* the existing equality test passes.

- [x] **M3.** Update the `TestArmyCompositionLayout` fixture in
  `tests/Hukbo.Client.Tests/ArmyCompositionPanelTests.cs` to the new values so the test
  fixture stops diverging from production theme data.
  *Verification:* `EveryLaidOutRowFitsInsideThePanel` passes unmodified — it checks
  vertical extents, which this change does not touch.

- [x] **M4.** Add the regression guard that should have existed: for every category row
  and the units-per-team row, assert
  `label.Length * UiFontRamp.GetApproximateAdvancePx(UiFontRole.Label) <= row.LabelBounds.Width`.
  This is the test whose absence let a 280-pixel overflow ship, and it is what stops a
  future roster addition with a longer name from quietly reintroducing it.
  *Verification:* fails against the old 420/260 geometry, passes against 640/148.

### Goal 3 and integration (`window-chrome`, runs last and alone)

- [x] **W1.** `src/Hukbo.Client/ArenaGame.cs`: add `Window.IsBorderless = true;`
  immediately after the existing `Window.AllowUserResizing = true;` at line 157.
  `AllowUserResizing` **stays true** — the window remains as resizable as it is today.
  *Verification:* build; manual smoke.

- [x] **W2.** `src/Hukbo.Client/Presentation/ClientCommand.cs`: add `Minimize` and
  `ToggleBattleReport` to the enum. Append them rather than inserting, so no existing
  member's ordinal moves.
  *Verification:* build.

- [x] **W3.** `src/Hukbo.Client/UI/ControlBar.cs`: `BarWidth` 384 → **568**, and append
  `new("Min", ClientCommand.Minimize)` and `new("Close", ClientCommand.Exit)` to the
  button array. 568 is `10 + 544 + 14`: `Layout` places the first button at
  `Bounds.Left + 10` (`ControlBar.cs:95`), six buttons at `ButtonWidth 84` plus five
  gaps at `ButtonGap 8` is 544 of content, and the existing bar keeps 14 pixels of
  right padding. **544 is wrong and would clip the Close button entirely.** All other
  button metrics unchanged.
  *Verification:* W7 test asserting every button's bounds sit inside the bar.

- [x] **W4.** `src/Hukbo.Client/ArenaGame.cs`: handle `ClientCommand.Minimize` in
  `ApplyClientCommand` by calling SDL2's `SDL_MinimizeWindow` with `Window.Handle`,
  which on DesktopGL is the underlying `SDL_Window*`. Use the source-generated form —
  `ArenaGame` is already `sealed partial class`, so the generator's requirement is met:

  ```csharp
  [System.Runtime.InteropServices.LibraryImport("SDL2")]
  private static partial void SDL_MinimizeWindow(nint window);
  ```

  A plain `[DllImport]` raises SYSLIB1054 under this repository's
  `TreatWarningsAsErrors`, and suppressing that analyzer to get green is forbidden by
  `CLAUDE.md` section 5. If the declaration cannot be made to build — for instance if
  the native library resolves under a different name — **report the failure**; do not
  silently drop the Min button and do not suppress the warning.
  *Verification:* build with zero warnings; manual smoke click watching the taskbar.

- [x] **W5.** `src/Hukbo.Client/Presentation/PresentationCoordinator.cs`: add a
  `BattleReportAccumulator` property alongside `EventFeed`, `HitEffects`, `Blood`,
  `Swings`, and `ClashEffects`. Call `Ingest` from `IngestTick` **with the raw per-tick
  event list**, next to the existing `EventFeed.Ingest(events)` — never through
  `EventFeed`, which would reintroduce the 200-event truncation into the report's data.
  Call `Clear()` from `ResetFor` next to the existing clears. Expose the snapshot from
  `ProcessTerminal` alongside `MatchSummaryFactory.Create`.
  *Verification:* W8 tests.

- [x] **W6.** `src/Hukbo.Client/ArenaGame.cs`: handle `ClientCommand.ToggleBattleReport`
  by flipping a private bool, mirroring the existing `ToggleSoundLog` handling, and
  draw `BattleReportPanel` only when the report is non-null and the flag is set — that
  is, strictly post-battle, matching `MatchSummaryPanel`'s own lifecycle. Instantiate
  the panel alongside the existing `MatchSummaryPanel`.
  *Verification:* build; manual smoke.

- [x] **W7.** New `tests/Hukbo.Client.Tests/ControlBarTests.cs`: every button's bounds
  lie inside the bar's bounds at the new width, and the two new commands are reachable.
  *Verification:* fails at `BarWidth = 544`, passes at 568.

- [x] **W8.** Update `tests/Hukbo.Client.Tests/PresentationCoordinatorTests.cs`:
  `IngestTick` forwards every batch to the accumulator; `ResetFor` clears it;
  `ProcessTerminal` sets the report alongside the summary. Mirror the existing
  forwarding and reset tests.
  *Verification:* all pass.

- [x] **W9.** Integration: apply any wiring the other three workstreams reported but
  could not make themselves, then get the whole solution building and its tests
  passing. Report every corrective edit made outside the owned file set.
  *Verification:* `dotnet build Hukbo.slnx -c Release` and
  `dotnet test Hukbo.slnx -c Release` both clean.

## Verification criteria

**The canonical gate is not delegated.** `./scripts/verify.ps1` runs once, after
integration, by the orchestrator, and its real pasted output is the evidence. No
sub-agent report substitutes for it. It runs prerequisites and locked restore, format
verification, Release build, Core and GPU-independent Client tests, then the 200-agent,
10,000-tick, seed-1 headless determinism workload.

**Hash movement is expected here, and that inverts the usual rule.** Goal 1 moves both
hashes by design. The criteria are therefore:

- Goals 2, 3, and 4 are hash-neutral. Any hash movement attributable to them is a
  defect, not a rebaseline.
- Every regenerated golden value is a number a real run printed, captured and recorded,
  never inferred.
- The three combat preset content hashes come back unchanged (task C4).
- The SplitMix64 pinned vectors come back unchanged (task C6).
- `CollisionMetrics.MaximumPenetrationRaw` stays 0 — the enlarged body must not
  produce an overlap the resolver failed to prevent.
- A 200-agent, 10,000-tick run completes without stall or deadlock.

**Re-record in `docs/development/testing.md`:** the seed-1 200-agent state and event
hash pair, and the agent-count sweep rows, all measured fresh. Mark superseded rows
explicitly rather than overwriting them silently.

**Manual smoke rows stay PENDING.** No agent may flip one to PASS, per `CLAUDE.md`
section 6 and the `hukbo-verify-and-record` skill. These need a human at an interactive
desktop:

| Row | What only a human can confirm |
| --- | --- |
| Collision readability | Tighter packing and firmer blocking are actually visible, and no unit is stranded |
| Borderless window | The OS title bar and its three buttons are gone |
| Minimize button | Clicking Min actually sends the window to the taskbar, and clicking the taskbar icon restores it |
| Close button and Alt+F4 | Both still quit |
| Unit setup menu | `Kalis — Thrusting Blade (shielded)` renders fully inside its row |
| Battle report | The panel opens from the summary, scrolls, and its numbers look right |

The minimize P/Invoke deserves particular suspicion: a build that compiles proves
nothing about whether the native call succeeds. It must be clicked, with the taskbar
watched. If it fails silently the button is dead with no visible error.

## Out of scope

Everything in design section 6, and in particular the hash-neutral collision
performance work in
[`2026-07-28-collision-resolution-scaling-design.md`](2026-07-28-collision-resolution-scaling-design.md),
which touches the same files with the opposite hash requirement and must not be
implemented alongside this.
