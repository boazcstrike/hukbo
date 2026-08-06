# UI-52 — Secondary feedback: design

Date: 2026-08-07
Status: design only. This document does not authorize implementation. The
ordered task list lives in
[`2026-08-07-ui-52-secondary-feedback.md`](2026-08-07-ui-52-secondary-feedback.md).

## 1. What this closes

Phase 5 of [`docs/plans/UI/ui-ux-improvement-plan.md`](UI/ui-ux-improvement-plan.md)
has four items. UI-50 (the bounded transition primitive) and UI-51 (priority
interaction motion) are already merged on `main`. UI-53 is a prohibition list,
not work. UI-52 is the only remaining implementable item in the phase.

UI-52 as written in the improvement plan asks for five decorative surfaces:

| Surface | Improvement-plan wording | Target duration |
| --- | --- | ---: |
| A | new-event row emphasis in the battle event log | 160–220 ms |
| B | selected-agent accent in the agent inspector | 140–180 ms |
| C | selector arrow and active-marker interpolation | 90–140 ms |
| D | play/pause/speed active strip in the control bar | 100–140 ms |
| E | optional status-badge emphasis | 450–650 ms, non-looping |

Nothing else in the repository changes. No simulation state, no snapshot, no
hash, no headless output, and no persisted setting is touched.

## 2. What already exists

Read off disk on 2026-08-07 in this worktree.

`src/Hukbo.Client/UI/UiTransition.cs` is a struct with one public method,
`AdvanceTo(float targetValue, TimeSpan elapsed, TimeSpan duration, bool
isMotionEnabled)`, plus `Value` and `IsSettled`. It eases with `EaseOutCubic`,
clamps a large frame delta to the remaining duration, snaps immediately when
motion is disabled or the duration is not positive, and restarts from the
current value whenever the target changes. It owns no clock, no timer, and no
random source.

`src/Hukbo.Client/UI/UiButtonMotion.cs` is a sealed class holding three
`UiTransition` channels — hover, focus, press — with `HoverDuration` at 110 ms
and `PressDuration` at 60 ms. It exposes `DecorativePressInset`, which returns
one pixel only at `MotionIntensity.Full`, and `GetVisualBounds`, which applies
that inset without moving the hit rectangle. There is no active channel.

`src/Hukbo.Client/UI/UiEntranceMotion.cs` is the UI-51 entrance helper, with
four duration constants (110, 160, 200, 150 ms) and two opacity channels.

`src/Hukbo.Client/UI/UiMotionTheme.cs` exposes `WithOpacity(UiTheme, float)`,
which fades all 27 colour roles and returns the original instance unchanged when
the opacity is at or above 1, so a settled surface allocates nothing.

`src/Hukbo.Client/UI/UiButton.cs` already carries an `IsActive` property and
already draws a six-pixel active strip down the left edge of the button using
`theme.Colors.Selection`. Its motion-aware `Update` overload takes `TimeSpan
elapsed` and `MotionIntensity`, and a legacy overload delegates to it with
`TimeSpan.Zero` and `MotionIntensity.Off`.

`src/Hukbo.Client/UI/ControlBar.cs` already takes `TimeSpan elapsed` and
`MotionIntensity motionIntensity` on its motion-aware `Update` overload and
already forwards both to every button along with the `isActive` flag. Surface D
needs no new plumbing at all.

`src/Hukbo.Client/MenuOverlay.cs` already takes `TimeSpan elapsed` on `Update`
and already holds the active `MotionIntensity` as a parameter. It forwards both
to its buttons and to `UiEntranceMotion`. Surface C therefore needs no
`ArenaGame` change either.

`src/Hukbo.Client/UI/BattleEventLogPanel.cs` `Update` takes `(InputEdges,
BattleEventFeed, Rectangle)` — no elapsed, no intensity.
`src/Hukbo.Client/UI/AgentInspectorPanel.cs` `Update` takes `(InputEdges,
AgentView?, Rectangle)` — likewise. Both are called from
`src/Hukbo.Client/ArenaGame.cs`, which already has `gameTime.ElapsedGameTime`
and `_motionManager.Value` in scope at those call sites.

The status bar is drawn by the private `DrawStatus` method in
`src/Hukbo.Client/ArenaGame.Rendering.cs`, which fills the bar with
`theme.Colors.StatusSurface` and then draws two text lines. The first line is
built by `BuildStatusLine`, which ends with `_simulation.Outcome` and may append
a staged-composition notice.

`UiThemeColors` already defines the `NewEvent` and `Selection` roles, so no new
theme role is required and `ui-theme-standards.json` is untouched.

## 3. Governing constraints

These come from `CLAUDE.md` §5, `SIMULATION-GAME-STANDARDS.md`, the
`hukbo-client-ui` skill, and UI-53 in the improvement plan. Every one of them is
a hard boundary on this design, not a preference.

- Client-only. Nothing in UI-52 may reach `Hukbo.Core`, a snapshot, a state
  hash, an event hash, or headless output.
- `MotionIntensity.Off` snaps. `Reduced` may change colour and opacity only, with
  no positional change. `Full` may add at most integer-pixel decorative
  movement.
- Hit rectangles never move. Event rows never reorder or move because of motion.
- Every channel settles exactly on its target value.
- No allocation in a settled frame.
- No random source, no wall clock, no timer. Elapsed time is the unscaled
  `GameTime.ElapsedGameTime` the caller already holds.
- The status badge is one-shot over 450–650 ms and never loops.
- Client presentation tests never construct `ArenaGame`, a `GraphicsDevice`, a
  `SpriteBatch`, or a window. All logic lives in pure helpers that tests call
  directly.
- `TreatWarningsAsErrors` is on repo-wide with nullable enabled. No test,
  warning, or analyzer is weakened to get green.

### 3.1 The decision that follows from those constraints

**Every UI-52 surface is colour and opacity only, at every motion intensity.**
`Reduced` and `Full` therefore render UI-52 identically, and `Off` snaps.

This is a deliberate narrowing of what `Full` is permitted to do. UI-51 already
took the same position for entrance motion, and the improvement plan records the
reason in its own words: "Full motion's positional feedback remains limited to
the one-pixel button press." Extending that to UI-52 buys three things. It makes
"hit rectangles never move" and "event rows never move" true by construction
rather than by inspection. It removes the whole class of half-pixel and
rounding-drift bugs that the `current-ui-ux-audit.md` integer-pixel rule exists
to prevent. And it means the `Reduced`-versus-`Full` smoke rows for UI-52 have a
single expected observation instead of two.

The concrete casualty of this decision is the control-bar active strip. The
obvious "juicy" treatment would grow the strip's width from zero to six pixels.
That is rejected: the strip keeps its fixed six-pixel width and interpolates its
colour instead.

## 4. The shared abstraction

Three questions had to be answered here: does `UiButtonMotion` gain an active
channel, is there one shared accent helper for surfaces A, B, C and E, and where
do the duration constants live.

### 4.1 `UiButtonMotion` gains an active channel — yes

Surface D is the play/pause/sound-log active strip, and that strip is already
drawn by `UiButton` from its own `IsActive` flag. `ControlBar` already passes
`isActive` into `UiButton.Update`, which already forwards elapsed time and
intensity into `UiButtonMotion.Advance`. Adding a fourth `UiTransition` channel
next to hover, focus and press is a four-line change to a class that exists for
exactly this purpose, and it lands surface D without touching `ControlBar.cs`,
`MenuOverlay.cs`, or `ArenaGame.cs` at all.

Inventing a separate helper for one strip on one control would be worse in every
respect. `UiButtonMotion` gets `ActiveDuration` (120 ms) and an `ActiveAmount`
property, `UiButton.Update` passes `IsActive` into `Advance`, and
`UiButton.DrawBackgroundAndBorder` lerps the strip colour from
`theme.Colors.PanelBorder` toward `theme.Colors.Selection` by `ActiveAmount`
instead of painting `Selection` outright.

One consequence is worth stating because it is easy to misread as a bug. The
strip is currently drawn only inside `if (IsActive && IsEnabled)`. With a
transition, the strip must also be drawn while `IsActive` is false but
`ActiveAmount` is still above zero, otherwise deactivation snaps instead of
fading. The draw condition becomes "enabled, and either active or still
settling."

### 4.2 One shared one-shot pulse serves A, C and E — yes

Surfaces A (new event arrived), C (selector value changed) and E (status changed)
are all the same shape: something observable changed, emphasise it briefly, decay
to nothing, never repeat until it changes again. Surface B (a different agent is
now selected) is the same shape too.

`UiTransition` cannot express that on its own. It restarts from its *current*
value when the target changes, so "jump to full, then decay" is not reachable
through `AdvanceTo` alone. The primitive gains one small additive method:

```csharp
public void Restart(float value)
```

which snaps `_startValue`, `_value`, `_targetValue` and `_elapsedSeconds` to a
single value. It is four lines, it delegates to the existing private `SnapTo`,
and it changes no existing behaviour, so `UiTransitionTests` keeps every current
assertion and gains new ones.

On top of that sits a new struct, `UiEmphasisPulse`, in
`src/Hukbo.Client/UI/UiEmphasisPulse.cs`:

```csharp
internal struct UiEmphasisPulse
{
    private UiTransition _transition;

    public readonly float Amount { get; }
    public readonly bool IsSettled { get; }

    public void Trigger(bool isMotionEnabled);
    public void Advance(TimeSpan elapsed, TimeSpan duration, bool isMotionEnabled);
    public void Reset();
}
```

`Trigger` calls `Restart(1f)` when motion is enabled and does nothing when it is
not, so `MotionIntensity.Off` never produces a visible pulse at all rather than
producing one that snaps away a frame later. `Advance` always calls
`AdvanceTo(0f, ...)`. Because the target is unconditionally zero, the pulse is
non-looping by construction — there is no code path that raises it except an
explicit `Trigger` from an observed change. That is what makes the 450–650 ms
status badge safe: it cannot become an idle animation by accident.

A struct rather than a class, matching `UiTransition`, so that a panel holding
several pulses allocates nothing per pulse.

### 4.3 A shared `UiSelectorMotion` for surface C — yes, and it is not a selector refactor

There are five selector classes with near-identical `Update` and `Draw` bodies
(`UiThemeSelector`, `GoreIntensitySelector`, `MotionIntensitySelector`,
`AutoCameraModeSelector`, and the generic `SettingsChoiceSelector<T>`), and six
instances of them in `MenuOverlay`. The temptation is to collapse the five into
one. That is rejected for UI-52: it is a large refactor of five shipped controls
with five shipped test files, its risk is nothing to do with motion, and it would
make the UI-52 diff impossible to review as a motion change.

What is shared instead is the *motion*, not the selector. A new sealed class
`UiSelectorMotion` in `src/Hukbo.Client/UI/UiSelectorMotion.cs` owns:

- a `UiTransition` for the previous-arrow hover state,
- a `UiTransition` for the next-arrow hover state,
- a `UiEmphasisPulse` for the active marker, triggered when the displayed value
  changes,
- `PreviousArrowColor`, `NextArrowColor` and `MarkerColor` resolved as pure
  functions of a `UiThemeColors` and the three amounts.

Each of the five selectors gains one field, one `AdvanceMotion` call, and three
colour reads in `Draw`. Roughly twelve lines each, all mechanical, all identical.
The logic exists once and is tested once, in `UiSelectorMotionTests`.

**`AdvanceMotion` is a new method, not a change to `Update`.** This matters for a
reason specific to `MenuOverlay`: its selector chain *early-returns* the moment
one selector reports a selection. If motion advancement lived inside `Update`,
then on any frame where the theme selector reports a change, the five selectors
below it would never advance and their transitions would stall mid-flight. So
`MenuOverlay.Update` calls `AdvanceMotion` on all six instances in one pass
*before* the interaction chain begins, and the interaction chain is left exactly
as it is.

The value-changed detection lives inside `UiSelectorMotion`, which stores the
last displayed marker string it was shown and triggers its pulse when the new one
differs. A string comparison rather than a generic value comparison keeps
`UiSelectorMotion` non-generic, so `SettingsChoiceSelector<T>` and the four
concrete selectors share one type. The marker string is what the spectator
actually reads, so a change in it is exactly the event worth emphasising.

### 4.4 Duration constants live in one static class

Precedent is mixed: `UiButtonMotion` holds its own two durations,
`UiEntranceMotion` holds its own four. Following that literally for UI-52 would
scatter five constants across five files and make "is every UI-52 duration inside
its published band?" an unanswerable question.

Instead, a new `internal static class UiSecondaryMotion` in
`src/Hukbo.Client/UI/UiSecondaryMotion.cs` holds all five, each with the
improvement-plan band recorded in a comment beside it:

| Constant | Value | Band | Surface |
| --- | ---: | ---: | --- |
| `NewEventDuration` | 200 ms | 160–220 | A |
| `SelectionAccentDuration` | 160 ms | 140–180 | B |
| `SelectorMarkerDuration` | 120 ms | 90–140 | C |
| `ActiveStripDuration` | 120 ms | 100–140 | D |
| `StatusBadgeDuration` | 550 ms | 450–650 | E |

Each value is the midpoint of its band, rounded to a round number of
milliseconds, which is how UI-51 chose 110 and 60. The class also carries
`IsEnabled(MotionIntensity)`, which normalizes an out-of-range enum value to
`Off` exactly as `UiButtonMotion.Advance` and `UiEntranceMotion.Advance` already
do, so that normalization is written once instead of six more times.

A test asserts every constant is inside its published band. That is the guard
that keeps a later "just make it snappier" edit from silently leaving the range
the plan committed to.

`ActiveStripDuration` lives here rather than on `UiButtonMotion` even though its
consumer is `UiButtonMotion`, so that the band test can see all five in one
place. `UiButtonMotion` reads it.

## 5. How elapsed time reaches each surface

| Surface | Owner | Elapsed already available? | What changes |
| --- | --- | --- | --- |
| A — event log | `BattleEventLogPanel` | No | New `Update` overload takes `TimeSpan elapsed, MotionIntensity`; existing three-argument overload delegates with `TimeSpan.Zero, MotionIntensity.Off`. `ArenaGame.cs` call site switches to the new overload. |
| B — inspector | `AgentInspectorPanel` | No | Same pattern: new overload, legacy overload delegates, `ArenaGame.cs` call site switches. |
| C — selectors | `MenuOverlay` | **Yes** | `MenuOverlay.Update` already receives `TimeSpan elapsed` and the active `MotionIntensity`. New `AdvanceMotion` pass forwards both. No `ArenaGame` change. |
| D — control bar | `ControlBar` → `UiButton` | **Yes** | `ControlBar.Update` already forwards elapsed and intensity to every button, which already forwards to `UiButtonMotion.Advance`. No `ControlBar` change, no `ArenaGame` change. |
| E — status badge | `ArenaGame` | **Yes** | `ArenaGame.Update` already holds `gameTime.ElapsedGameTime` and `_motionManager.Value`; it advances a `UiStatusBadgeMotion` field and `DrawStatus` reads the amount. |

The legacy-overload pattern in rows A and B is the one UI-51 already established
on `ControlBar` and `UiButton`. It keeps every existing test call site compiling
unchanged, and it makes the behaviour of a test that does not opt into motion
explicit: it snaps.

Only surfaces A, B and E require an `ArenaGame` edit, and all three land in the
same two files. Those edits are gathered into a single task so that no two
parallel implementers ever hold `ArenaGame.cs` at once.

## 6. Surface-by-surface behaviour

### A — new-event row emphasis (200 ms)

`BattleEventLogPanel.Update` already computes `visibleEntries` for the frame.
The panel records the highest `Sequence` among them. When that value increases
between frames, the pulse triggers and the panel records the *previous* highest
sequence as an exclusive threshold. While the pulse is above zero, any drawn row
whose `Sequence` is above that threshold has its text colour blended from
`TextPrimary` toward `NewEvent` by the pulse amount.

This is O(1) state: one `long?` threshold and one pulse. It reads nothing new
from `BattleEventFeed`, so `src/Hukbo.Client/Presentation/BattleEventFeed.cs`
is untouched and the feed's 200-event retention cap is unaffected.

The blend is applied to text colour, not to a row fill, because the row fill is
already carrying selection and hover state and a third meaning on the same pixels
would be unreadable. Rows do not move, do not reorder, and do not change height.
The existing `LATEST +n` badge and the `[LIVE]` / `[INSPECTING]` status text are
unchanged, so the "new events arrived" fact is still stated in text and the
motion is genuinely secondary.

The pure helper is `internal static float GetRowEmphasis(long sequence, long?
emphasisThreshold, float pulseAmount)`, returning the pulse amount when the row
is newer than the threshold and zero otherwise. Tests call it directly.

### B — selected-agent accent (160 ms)

`AgentInspectorPanel.Draw` already paints a faction-coloured accent bar down the
left edge. The panel records the selected `EntityId`. When it changes — including
from null to a value — the pulse triggers. While the pulse is above zero, the
accent colour is blended from the faction colour toward `theme.Colors.Selection`
by the pulse amount, so a newly selected agent's accent brightens toward the
selection colour and settles back to its faction colour.

Deselection (`agent` becomes null) resets the pulse rather than triggering it;
there is no panel on screen to emphasise.

The accent bar's rectangle is unchanged. Its width, inset, and the text origin
that clears it are all untouched, so no inspector row moves.

The pure helper is `internal static Color GetAccentColor(Color factionColor,
Color selectionColor, float pulseAmount)`.

### C — selector arrow and active-marker interpolation (120 ms)

Two behaviours, both driven by `UiSelectorMotion`:

- Each `<` and `>` arrow interpolates from `TextSecondary` to `TextPrimary` on
  pointer hover. The arrow glyph does not move, and `PreviousBounds` /
  `NextBounds` are untouched, so the click targets are byte-identical.
- The `ACTIVE - n / m` marker line pulses from `Selection` toward `ActionFocus`
  when the marker string changes, then settles back to `Selection`.

The marker's text content is unchanged, so the control still states its level in
words and position and never relies on colour alone — the rule the existing
comment in `GoreIntensitySelector.Draw` records.

Applies to all five selector classes and all six `MenuOverlay` instances.

### D — control-bar active strip (120 ms)

Covered in §4.1. The strip keeps its fixed six-pixel width and interpolates its
colour from `PanelBorder` to `Selection` as `IsActive` turns on and back as it
turns off. Because `ControlBar` marks Play active while playing and Pause active
while paused, pressing Space produces one strip fading out and another fading in
over 120 ms, which is the play/pause feedback the improvement plan asked for.

The strip is drawn inside `visualBounds`, which already accounts for the UI-51
one-pixel press inset, so no new geometry rule is introduced.

### E — status-badge emphasis (550 ms, one-shot)

The status bar's most decision-relevant fact is the match outcome, which
`BuildStatusLine` already prints at the end of the line. A new pure helper class
`UiStatusBadgeMotion` in `src/Hukbo.Client/UI/UiStatusBadgeMotion.cs` records the
last status line it was shown and triggers its pulse when the line changes in a
way that matters: a different `BattleOutcome`, or a change in playing/paused
state. It does **not** trigger on a tick-count change or an alive-count change,
which change every frame and would turn the badge into a loop.

`ArenaGame.Update` calls `UiStatusBadgeMotion.Observe(outcome, isPlaying, elapsed,
intensity)` once per frame. `DrawStatus` blends the bar fill from
`theme.Colors.StatusSurface` toward `theme.Colors.StatusInfo` by the pulse
amount. The bar's rectangle, both text origins, and `CalculateStatusTextBounds`
are unchanged, so the status text does not move and `ClipStatusLine` behaves
identically.

The 550 ms duration is the longest in the repository by a wide margin, which is
exactly why the trigger condition is the narrowest. `Observe` takes plain values
— a `BattleOutcome` and a `bool` — so the whole trigger rule is unit-testable
without a game instance.

## 7. Out of scope, named

`ArmyCompositionPanel` draws its own stepper arrows in
`src/Hukbo.Client/UI/ArmyCompositionPanel.Presentation.cs` (`DrawArrow`, called
at lines 233 and 248). Those arrows are not covered by UI-52, whose bullet names
"selector arrow" and whose other four bullets are all HUD or menu-selector
surfaces. The composition steppers are a different control family — they clamp
rather than wrap, and they carry per-arrow disabled states that the menu
selectors do not — and `ArmyCompositionPanel.Update` receives no elapsed time
today. Bringing them in would add an `ArenaGame.cs` signature change for a
surface the plan did not ask for.

This leaves a real, visible inconsistency: menu selector arrows will respond to
hover and composition stepper arrows will not. It is recorded here rather than
quietly ignored, and it is the obvious follow-up item once UI-52 is on `main`.

## 8. Known limitation carried forward from UI-51

`ControlBar.Update`, `BattleEventLogPanel.Update` and `AgentInspectorPanel.Update`
are all called inside `ArenaGame`'s `if (!pointerConsumed)` chain. On a frame
where a higher-priority surface consumes the pointer, the lower surfaces do not
update, so their transitions do not advance that frame. The effect is a
transition that pauses and then resumes; it never jumps, never overshoots, and
always settles, because `UiTransition` accumulates elapsed time rather than
sampling a clock.

This is pre-existing UI-51 behaviour, not something UI-52 introduces, and fixing
it means restructuring the pointer chain into an advance pass and a hit-test
pass. That is a larger change than UI-52 and is deliberately not attempted here.

## 9. Testing approach

Every assertion is a pure-helper call. No test constructs `ArenaGame`, a
`GraphicsDevice`, a `SpriteBatch`, or a window, and the repository's current
count of zero such occurrences under `tests/` stays at zero.

| Area | Test file | What it proves |
| --- | --- | --- |
| Primitive | `UiTransitionTests` (existing) | `Restart` snaps all four fields; a `Restart(1f)` followed by `AdvanceTo(0f)` decays and settles exactly at 0 |
| Pulse | `UiEmphasisPulseTests` (new) | `Trigger` at `Off` produces no visible amount; `Advance` never raises the amount; a settled pulse stays settled without a further `Trigger`; a large frame delta settles exactly at 0 |
| Durations | `UiSecondaryMotionTests` (new) | Each of the five constants is inside its published band; `IsEnabled` normalizes an undefined enum value to disabled |
| A | `BattleEventLogPanelTests` (existing) | `GetRowEmphasis` returns zero for a row at or below the threshold and the pulse amount above it; the threshold only advances when the highest visible sequence advances |
| B | `AgentInspectorAccentMotionTests` (new) | `GetAccentColor` returns the faction colour at amount 0 and the selection colour at amount 1; selection change triggers, deselection resets |
| C | `UiSelectorMotionTests` (new) | Arrow hover amounts settle exactly; a marker-string change triggers exactly one pulse; an unchanged marker string triggers none; arrow colours resolve from theme roles |
| D | `UiButtonTests` (existing) | `ActiveAmount` rises while active and falls while inactive; at `Off` it snaps; the strip colour resolves from `PanelBorder` at 0 and `Selection` at 1 |
| E | `UiStatusBadgeMotionTests` (new) | An outcome change triggers; a playing/paused change triggers; a tick-only change does not; the pulse decays to exactly 0 and does not re-raise itself over many frames |

The non-looping guarantee for surface E is proved by advancing
`UiStatusBadgeMotion` for several seconds of simulated frame deltas with no
observed change and asserting the amount reaches and stays at exactly zero.

Interactive behaviour is proved only by the manual checklist in
`docs/development/testing.md`. UI-52 adds five rows to the existing "Responsive
menu, startup display, and UI motion smoke" table, numbered UI-12 through UI-16,
each left `PENDING`. No agent flips any of them.

## 10. The nine questions (`SIMULATION-GAME-STANDARDS.md` §10)

**1. User-visible outcome.** Five decorative surfaces acquire a short, bounded
colour transition: a newly arrived event row briefly reads in the new-event
colour, a newly selected agent's inspector accent briefly brightens toward the
selection colour, a hovered selector arrow brightens and a changed selector
marker briefly pulses, the control bar's active-mode strip fades between states
instead of snapping, and the status bar briefly tints when the match outcome or
the playing state changes. At `MotionIntensity.Off` every one of these is
instantaneous.

**2. Tick stage and state read/written.** None. UI-52 participates in no tick
stage. It reads only the presentation values the client already holds — the
visible event entries, the selected `AgentView`, the active selector values, the
button `IsActive` flags, the `BattleOutcome`, and the playing flag — and writes
only to private fields of client UI objects.

**3. Numeric units and bounds, same-tick conflict rule.** Every channel is a
`float` in `[0, 1]` clamped by `UiTransition.AdvanceTo`. Durations are the five
`TimeSpan` constants in `UiSecondaryMotion`, each inside its published band and
each asserted by test. Elapsed time is unscaled `GameTime.ElapsedGameTime`,
clamped per call to the remaining duration, so an arbitrarily large frame delta
settles rather than overshooting. There is no same-tick conflict rule because no
tick state is involved; within a frame each channel has exactly one writer.

**4. Total ordering and random-stream policy.** No randomness of any kind. No
ordering question arises: each channel belongs to exactly one control and is
advanced exactly once per frame by that control's owner. The one place ordering
could have mattered — `MenuOverlay`'s early-returning selector chain — is
resolved by advancing all six selectors in a single pass before the chain runs
(§4.3).

**5. Cache source and invalidation.** No cache. The only retained state is the
current value of each transition, plus three change-detection fields: the
event log's highest-seen sequence, the inspector's selected `EntityId`, and the
status badge's last observed outcome and playing flag. All are O(1), all are
overwritten in place each frame, and none is derived data that could go stale.

**6. Save, event, or version effect.** Presentation only. No snapshot field, no
event, no preset version, no golden expectation, no settings-schema change. The
`MotionIntensity` enum and its persisted numeric values are read, never modified.
`Hukbo.Core` is not referenced, edited, or rebuilt in any behavioural way, and
`Hukbo.Diagnostics` is not involved.

**7. Worst-case complexity and benchmark workload.** Per frame: one
`AdvanceTo` per live channel. The upper bound is 4 channels per control-bar
button (7 buttons) plus 4 per menu button plus 3 per selector (6 selectors) plus
one pulse each for the event log, the inspector and the status badge — under a
hundred float operations per frame, none of them allocating. A settled channel
returns from `AdvanceTo` after two comparisons, and `UiMotionTheme.WithOpacity`
already returns the original instance when settled, so a settled frame allocates
nothing. The canonical gate's 200-agent / 10,000-tick / seed-1 headless workload
does not execute a single line of this code, because `Hukbo.Headless` opens no
window; the determinism result is therefore unchanged by construction, which is
itself the evidence.

**8. Spectator explanation — can a spectator discover this without reading
source code?** Yes, and this is the question that shaped §3.1. Each surface makes
an event the spectator caused, or is watching for, briefly legible at the place
it happened: an event arrived here, this agent is now selected, this setting just
changed, this is the mode you are now in, the match just ended. Every one of
those facts is also stated in text that UI-52 does not touch — the `LATEST +n`
badge and the `[LIVE]` / `[INSPECTING]` label, the inspector's own agent rows,
the selector's `ACTIVE - n / m` marker line, the Play and Pause button labels,
and the outcome word in the status line. The motion draws attention to text that
already carries the meaning; it never carries meaning alone, and it never uses
colour alone. The Motion Intensity selector in the menu is where a spectator
discovers the whole system exists, and setting it to Off removes all of it
immediately without a restart.

**9. Tests that fail before and pass after.** The eight rows in §9. Each new test
file references a type or method that does not exist before implementation, so
each fails to compile against the current tree; the two existing files
(`UiTransitionTests`, `UiButtonTests`) gain assertions that fail against the
current behaviour.

## 11. Sources

- [`docs/plans/UI/ui-ux-improvement-plan.md`](UI/ui-ux-improvement-plan.md), Phase 5, UI-50 through UI-53
- [`docs/plans/UI/current-ui-ux-audit.md`](UI/current-ui-ux-audit.md), integer-pixel and hit-target rules
- `SIMULATION-GAME-STANDARDS.md` §10, feature and reviewer acceptance
- `CLAUDE.md` §5 non-negotiables and §6 workflow
- `.claude/skills/hukbo-client-ui/SKILL.md`, pure-helper testability and the 27 theme roles
- [W3C: Animation from Interactions](https://www.w3.org/WAI/WCAG22/Understanding/animation-from-interactions)
- [Microsoft: Timing and easing](https://learn.microsoft.com/en-us/windows/apps/design/motion/timing-and-easing)
