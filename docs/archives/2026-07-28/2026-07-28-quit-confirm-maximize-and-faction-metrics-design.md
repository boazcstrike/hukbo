# Quit confirmation, maximize replacement, and Core faction metrics — design

**Archived: reference only.** Implemented and verified on 2026-07-28; the
canonical gate passed with `stateHash A080E28DA7C79C20` and `eventHash
2B6FB3A9A9C1960D`, both unchanged as this workstream required. Do not execute
this document and do not cite it as justification for a change. Smoke rows 156
to 171 in [docs/development/testing.md](../../development/testing.md) remain
PENDING and still need a human at an interactive desktop.

**Status:** Design complete. Per `CLAUDE.md` section 6 a `-design.md` document
does not authorize implementation on its own; the plan document
[`2026-07-28-quit-confirm-maximize-and-faction-metrics.md`](2026-07-28-quit-confirm-maximize-and-faction-metrics.md)
carries the ordered task list. The user approved all three changes on 2026-07-28
in answer to the three open questions left by
[`docs/archives/2026-07-28/2026-07-28-collision-report-and-shell-design.md`](../archives/2026-07-28/2026-07-28-collision-report-and-shell-design.md),
so the plan follows immediately.

**Date:** 2026-07-28.

**Origin:** the previous workstream shipped a borderless window with replacement
Min and Close controls, and a battle report that derived its faction totals from
the event stream. It recorded three questions it deliberately did not answer. The
user answered all three:

| Question left open | Answer |
| --- | --- |
| Should Close confirm, now that quitting is one click? | **Yes.** |
| Should anything replace the OS maximize button? | **Yes, replace it.** |
| Should `CombatMetrics` / `CollisionMetrics` go public so faction totals come from Core rather than being re-derived? | **Yes.** |

| Goal | Layer | Moves the state hash? |
| --- | --- | --- |
| A. Confirm before quitting | `Hukbo.Client` | No |
| B. Replace the OS maximize button | `Hukbo.Client` | No |
| C. Faction totals from Core metrics | `Hukbo.Core` | **Must not** |

Goal C is the one with teeth. It touches the simulation, and unlike the previous
workstream's collision change it is **required to be hash-neutral**: metrics are
observability, and a metric that reaches the state hash or the event hash is a
defect. `CombatMetrics_ReachesNeitherHash` already exists to catch exactly that
and must keep passing unmodified.

## 1. Goal A — confirm before quitting

### The problem

Quitting is now a single click on the `Close` button in the control bar, added by
the previous workstream precisely so a spectator who lost the OS close button
would not have to walk a three-step menu path. That fixed discoverability and
created a new hazard: one stray click on a bar the spectator uses constantly for
Play, Pause, Menu, Sounds, and Min destroys an in-progress battle with no
recourse. There is no save, so a quit is unrecoverable.

### What the codebase does not have

There is no confirmation dialog, no modal, and no blocking prompt anywhere in
`Hukbo.Client`. Searching for a confirm pattern returns only unrelated prose in
the appearance catalogs. This is the first one, which is why it gets a named,
reusable type rather than a boolean bolted onto `ArenaGame`.

### Options considered

**A reusable `ConfirmationPrompt` overlay (chosen).** A small panel with a
message, a confirm button, and a cancel button, drawn above everything else and
consuming pointer and keyboard input while open. It is parameterised by the
message and the command it will issue on confirm, so the next destructive action
that wants a prompt reuses it instead of inventing a second pattern.

**A press-and-hold Close button.** Rejected. It is undiscoverable — nothing on
screen says a button must be held — and it fails the standing test in
`CLAUDE.md` section 6 that a spectator should be able to discover an effect
without reading source.

**A second click within a timeout ("click again to confirm").** Rejected. It
relabels a button the user is already looking at, which reads as a glitch, and it
silently arms a destructive action for a period with no visible state.

**Confirm only on `Close`, not in the menu.** Rejected. Both in-application quit
paths are one deliberate action away from the same unrecoverable outcome, so both
get the same treatment. Diverging them would mean the menu's `Exit Game` is the
*less* safe path, which is backwards.

### The decision

A `ConfirmationPrompt` overlay, shown by both in-application quit paths — the
control bar's `Close` button and the menu overlay's `Exit Game` button. Confirm
performs the quit; cancel dismisses and changes nothing. `Escape` cancels and
`Enter` confirms, matching the focus conventions the menu overlay already uses.

Cancel is the default focus. For a destructive, unrecoverable action the safe
option is what a reflexive `Enter` should hit, and defaulting to confirm would
reintroduce the single-keystroke hazard the prompt exists to remove.

**Alt+F4 is deliberately left alone.** `GameWindow.AllowAltF4` stays true and the
operating system's own quit path stays immediate and unconfirmed. Two reasons.
First, an application that refuses Alt+F4 is worse behaved than one that does
not, and the borderless window has no title bar, so Alt+F4 is the user's
guaranteed escape hatch if anything about the in-game chrome misbehaves —
including the untested `SDL_MinimizeWindow` call. Second, intercepting it would
mean disabling `AllowAltF4` and reimplementing the shortcut, which trades a
working OS behaviour for a hand-rolled one. This is a judgement call and it is
recorded here so it can be revisited: the cost is that the fastest quit path is
the one that does not confirm.

### Interaction with the pointer priority chain

The prompt sits at the top of the pointer priority chain, above the menu overlay,
and must consume every click while open so a click that misses its buttons cannot
fall through to the control bar, the arena, or an agent selection underneath. The
same applies to keyboard input: while the prompt is open, `Escape` belongs to the
prompt and must not also close the menu behind it.

## 2. Goal B — replace the OS maximize button

### The problem

The window is borderless with `AllowUserResizing` still true, so it can be
resized by dragging an edge but there is no longer any maximize affordance and no
title bar to double-click. The user asked for the button to be replaced.

### Options considered

**A `Max` button toggling maximize and restore (chosen).** Sits in the control
bar beside `Min` and `Close`, mirroring the OS button it replaces and reusing the
SDL interop the previous workstream already established for minimize.

**A borderless-fullscreen toggle.** Rejected. Fullscreen is a different
behaviour from maximize — it changes the presentation mode rather than the window
geometry — and the previous workstream already rejected forcing fullscreen as
overreach. Replacing a maximize button with a fullscreen toggle would not be
replacing it.

**Nothing, on the grounds that edge-drag resizing exists.** Rejected: the user
asked for it explicitly.

### The decision

A seventh control-bar button, `Max`, issuing a new `ClientCommand.ToggleMaximize`
handled through SDL:

- `SDL_MaximizeWindow(Window.Handle)` when the window is not maximized.
- `SDL_RestoreWindow(Window.Handle)` when it is.

Both are declared the same way the existing minimize call is, with
`[LibraryImport("SDL2")]` on a `private static partial` method. A plain
`DllImport` raises SYSLIB1054 under this repository's `TreatWarningsAsErrors`,
and suppressing an analyzer to get green is forbidden.

**Reading the current state, rather than tracking it.** The button must not keep
its own boolean guess at whether the window is maximized, because the user can
change that outside the application — by dragging an edge, or through a Windows
snap shortcut — and a tracked flag would desynchronise and invert the button.
`SDL_GetWindowFlags` returns the authoritative flags and
`SDL_WINDOW_MAXIMIZED` (`0x00000080`) is the bit to test. The button label and
behaviour derive from that call, so the control cannot get out of step with
reality.

**Control bar geometry.** Seven buttons at the existing `ButtonWidth` of 84 plus
six gaps at `ButtonGap` 8 is `588 + 48 = 636` of content. `Layout` places the
first button at `Bounds.Left + 10` and the existing bar keeps 14 pixels of right
padding, so `BarWidth` becomes `10 + 636 + 14 = 660`, up from 568. Getting this
wrong clips the rightmost button, which is exactly what happened when 544 was
proposed for six buttons, so the plan pins it with a test that every button's
bounds lie inside the bar.

**The bar is getting wide.** At 660 pixels the control bar occupies a little over
half the width of a 1,280-pixel window. That is a real cost and it is recorded
rather than hidden; if it becomes a problem the answer is a narrower window-chrome
button group, not a silent reduction in the padding this design just computed.

## 3. Goal C — faction totals from Core metrics

### What is actually true today, which is not what the question assumed

The open question was phrased as making `CombatMetrics` and `CollisionMetrics`
"go public". **Both records are already `public`.** What is `internal` is the
machinery around them, and what is missing is the data itself:

| Symbol | Visibility today | Note |
| --- | --- | --- |
| `CombatMetrics` (record struct) | `public` | Already public |
| `CollisionMetrics` (record struct) | `public` | Already public |
| `CombatMetricsAccumulator` | `internal` | Instantiated only in `HeadlessRunner` |
| `CollisionMetricsAccumulator` | `internal` | Same |
| `BattleSimulation.LastTickCombat` | `internal` | The per-tick value the Client would need |
| `BattleSimulation.LastTickCollision` | `internal` | Same |

And the decisive fact: **neither metrics type carries any faction dimension at
all.** A search for "faction" across both files returns nothing.
`CombatMetrics` counts `AcceptedAttacks`, `LandedAttacks`,
`ShieldBlockedAttacks`, `ParriedAttacks`, `DeflectedAttacks`, and
`EvadedAttacks` for the whole battle, undivided.

So a visibility change alone delivers nothing the report can use. The faction
split has to be built.

### Options considered

**Add a faction dimension to the per-tick combat metrics, make the per-tick
accessor public, and let the Client sum across ticks (chosen).** Core becomes the
authoritative source of every count; the Client's remaining arithmetic is
addition, not inference. Core gains no cumulative state.

**Have `BattleSimulation` carry cumulative per-faction run totals.** Rejected,
though it is the most literal reading of the request. It adds mutable
run-scoped state to the simulation whose only purpose is observability, and that
state would have to be excluded from the state hash, excluded from the snapshot,
and reset correctly on every path — `CLAUDE.md` section 9 explicitly forbids
saving derived metrics into a snapshot, and every one of those exclusions is a
place a determinism bug can hide. The chosen option gets the same authoritative
numbers with none of that surface, because a per-tick value that the caller sums
is not simulation state.

**Leave the report deriving totals from events.** Rejected — this is what the
user asked to change, and the derivation carries a documented kill-credit
heuristic that the authoritative counters do not need.

### The decision

Three changes, all in `Hukbo.Core` except the last:

1. Give the per-tick combat metrics a faction dimension, so each resolution
   count is available per faction as well as in total. The attacker's faction is
   already known where `_lastTickCombat` is constructed, so no new lookup is
   introduced into the tick.
2. Promote `BattleSimulation.LastTickCombat` from `internal` to `public`, so the
   Client can read the authoritative per-tick counts it already computes.
   `LastTickCollision` is promoted on the same reasoning where the report needs
   it, and left alone where it does not.
3. `BattleReportAccumulator` stops deriving faction totals from `Attack` events
   and instead sums the per-faction counts Core reports each tick.

**What this does and does not remove from the Client.** Faction-level totals —
accepted, landed, blocked, parried, deflected, evaded, and accuracy — become
authoritative. **Per-unit statistics stay client-side and stay derived**, because
Core does not track per-entity counters and this design does not propose adding
them. Kill attribution therefore remains the documented presentation-side
heuristic it already is. The report will end up with two classes of number, and
the panel must not present a derived per-unit figure as though it carried the
same authority as a Core faction total.

### The determinism contract, which is the whole risk

Adding fields to a public record struct that the simulation writes every tick is
exactly the kind of change that quietly reaches a hash. The constraints:

- **`CombatMetrics_ReachesNeitherHash` must pass unmodified.** It compares a bare
  `BattleSimulation`, which builds no accumulator, against the full headless
  pipeline, which builds one every tick. If metrics leaked into authoritative
  state the two would diverge. That test is the guard and it may not be edited.
- **Every recorded state hash and event hash must come back byte-identical.**
  Unlike the previous workstream, there is no rebaseline here. A hash that moves
  means a metric reached the simulation, and the correct response is to fix the
  leak, not to regenerate a golden.
- **No metric may enter a snapshot**, per `CLAUDE.md` section 9.
- **The per-tick allocation budget must not regress.** The metrics record is a
  struct assigned once per tick; widening it must not introduce a per-tick heap
  allocation.

### The consequence nobody asked about: the report JSON shape changes

`CombatMetrics` is serialized into the headless `RunReport`, which is what the
gate prints and what the JSON digest fixtures under
`tests/Hukbo.Core.Tests/Fixtures/` compare against. Widening the record changes
that JSON's shape. This is not a hash change and it is not a determinism
problem, but it will break any fixture or assertion that matches the report
structure, and the plan has to check for that explicitly rather than discover it
as a mystery test failure. The gate's own printed output will also gain fields.

## 4. The nine questions

`SIMULATION-GAME-STANDARDS.md` section 10. Goals A and B are presentation-only
and are covered above; these answers are for goal C, the only one touching the
simulation.

1. **User-visible outcome.** The battle report's faction totals become
   authoritative counts rather than figures re-derived from the event stream. A
   spectator sees numbers that cannot disagree with the simulation.
2. **Tick stage and state read or written.** No new stage and no new
   authoritative state. The per-tick metrics record that `GatherAndCommitAttacks`
   already writes gains a faction dimension.
3. **Numeric units, bounds, and same-tick conflict rule.** Plain integer counts,
   bounded by the number of attacks resolvable in one tick. No conflict rule
   applies; counters only increment.
4. **Total ordering and random-stream policy.** Unchanged. Counting is
   order-independent, and no random stream is consulted.
5. **Cache source and invalidation.** The per-tick record is overwritten every
   tick and never accumulated inside Core. The Client's running sums are
   presentation state cleared through the existing `ResetFor` hook.
6. **Save, event, and version effect.** No persisted field, no event field, no
   enum value, and **no preset version** — this change is required to move no
   hash at all. The report JSON shape does change; see section 3.
7. **Worst-case complexity and benchmark workload.** Unchanged per tick; the
   counts already being computed simply land in more fields. Workload: the
   canonical seed-1, 200-agent, 10,000-tick headless run.
8. **Spectator explanation.** The faction totals section of the battle report is
   the explanation, and it is reachable in one click from the match summary.
9. **Tests that fail before and pass after.** A new test asserting the
   per-faction counts sum to the existing undivided totals, and a new test
   asserting the report's faction totals equal Core's counts rather than an
   event-derived figure. The hash tests are the inverse case: they pass before
   *and* after, and their entire value is in the "after".

## 5. Out of scope

- Per-entity counters in `Hukbo.Core`. Per-unit report rows stay client-side and
  stay derived; kill attribution stays a documented heuristic.
- Intercepting Alt+F4. Section 1 records why.
- A fullscreen toggle. Section 2 records why maximize is not fullscreen.
- Cumulative run totals held inside `BattleSimulation`. Section 3 records why.
- Making `CombatMetricsAccumulator` or `CollisionMetricsAccumulator` public.
  Nothing outside the headless runner needs to aggregate, and the Client sums
  what it reads.
- Any rebaseline of a recorded hash. This workstream must move none.
