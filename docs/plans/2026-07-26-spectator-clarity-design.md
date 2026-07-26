# Spectator Clarity Design

**Status:** Approved for planning on 2026-07-26

**Decision owner:** Repository owner

**Supersedes:** Hosted-CI requirements in the initial execution prompt and
foundation delivery plans. Local verification scripts are authoritative until
the repository owner explicitly re-enables hosted CI.

## Goal

Make a running battle understandable without changing combat rules or adding a
new gameplay system. A spectator should be able to:

1. select an agent and keep following its state;
2. understand important battle events as they happen;
3. see whether the simulation is playing or paused and control it without
   opening a modal;
4. understand how the battle ended; and
5. replay the same seeded scenario.

The result remains a deliberately simple first playable. It is a clarity pass,
not a visual-polish, progression, replay-storage, or combat-expansion phase.

## Locked delivery policy

- Remove `.github/workflows/ci.yml`.
- Do not require or consume GitHub Actions minutes.
- Keep `./scripts/verify.ps1` as the canonical non-graphical verification gate.
- Keep packaging local through
  `./scripts/package.ps1 -Runtime win-x64`.
- Keep interactive UI verification as an explicit manual Windows smoke test.
- Do not add another hosted automation provider as a substitute.
- Reconsider hosted CI only through a later explicit repository-owner decision.

Historical plans and reports may describe the workflow that existed during the
foundation phase. This design is the current decision record.

## Current baseline

The existing MonoGame client already:

- renders the authoritative Core simulation;
- supports fixed-step Play/Pause, `1x`/`2x`/`4x`, same-seed reset, pan, and zoom;
- pauses when the Escape menu opens;
- exposes Play, Pause, and Exit Game actions in that modal menu;
- shows a transient hover line for entity ID, faction, hit points, intent, and
  target;
- automatically stops advancing when `BattleSimulation.Outcome` becomes
  terminal.

Core already exposes the data needed for this phase:

- `AgentView` with stable entity ID, faction, location, health, target, intent,
  and alive/dead status;
- `BattleEvent` with stable sequence, tick, kind, source, target, value, and
  faction;
- `BattleSimulation.LastEvents` for the events committed by the latest tick;
- `BattleSimulation.Tick` and `BattleSimulation.Outcome`;
- `Scenario.Seed` and `Scenario.TickRate`.

The important gaps are:

- hover state disappears when the pointer moves;
- there is no click selection or persistent inspector;
- the client does not retain a bounded presentation history of authoritative
  events;
- there is no terminal match-summary view;
- Play, Pause, and Menu are not always visible;
- direct manual Play/Pause/Exit interaction has not yet been recorded.

## Product behavior

### Persistent selection

- A primary mouse click on an agent selects it.
- Selection uses the same camera transformation and hit radius as hover.
- If multiple agents fall within the radius, the closest screen-space distance
  wins; stable entity ID breaks an exact tie.
- Clicking empty arena space clears selection.
- Clicking a UI panel or button must not select or clear an arena agent.
- A selected agent remains selected after it dies so the spectator can
  understand the death and its last state.
- Reset or replay clears selection because it creates a new match instance.
- Hover may still provide a lightweight highlight, but the persistent inspector
  is driven only by selection.

### Agent inspector

The selected-agent panel remains visible until selection is cleared or the
match resets. It shows:

- entity ID;
- faction;
- alive/dead status;
- current and maximum hit points;
- current intent;
- current target entity ID or `none`;
- current arena position.

The panel consumes the current authoritative `AgentView`; it never owns or
mutates gameplay values. A selected dead agent is still present in
`BattleSimulation.Agents` and should display `DEAD` with its final view.

### Battle event log

The right-side event panel shows a scrollable, newest-at-bottom feed of
authoritative battle events. Each row contains:

- tick;
- readable event kind;
- source and target when applicable;
- value or faction when it improves understanding.

The client retains at most 200 entries. It consumes `LastEvents` after each
advanced tick, deduplicates by event sequence, and maintains increasing
`(Tick, Sequence)` order. This is presentation history only:

- do not add an unbounded log to Core;
- do not change Core event order or contents;
- do not serialize the feed;
- do not call this a replay recording.

Mouse-wheel input scrolls the log only while the pointer is over it. Otherwise
the wheel continues to zoom the arena. When already at the bottom, new events
keep the view pinned to the newest row. If the spectator scrolls upward, new
events do not steal the scroll position; a small `new events` cue may be shown.

### Always-visible controls

A compact control bar is always visible above the arena:

- **Play** resumes logical advancement;
- **Pause** pauses logical advancement;
- **Menu** opens the existing modal menu and pauses logical advancement.

The active Play/Pause state is visually distinct. Keyboard shortcuts remain:
Space toggles Play/Pause and Escape toggles the menu.

The modal menu remains the only location for **Exit Game** in this phase. Its
actions retain the current semantics:

- Play resumes and closes the menu;
- Pause leaves the menu visible and the simulation paused;
- Exit Game requests one clean shutdown.

The compact bar and modal must issue the same commands rather than maintain
separate playback state.

### Match summary

When Core changes from `Ongoing` to a terminal outcome, the client:

1. pauses;
2. ingests the terminal tick's events;
3. creates a summary from the final authoritative state; and
4. shows the summary above the arena without discarding the event log or
   selected-agent context.

The summary contains:

- winner (`Blue`, `Red`, or `Draw`);
- surviving count for each faction;
- terminal tick;
- simulated duration calculated as `Tick / TickRate`;
- seed;
- **Replay Same Seed** button;
- **Menu** button.

Replay Same Seed creates a fresh `BattleSimulation` from the existing scenario,
clears presentation history and selection, resets the camera only if current
reset behavior already does so, and begins in a paused state. The user chooses
Play when ready. Replaying the same seed must preserve the existing deterministic
outcome, event hash, and state hash.

## Architecture

### Authority boundary

```text
Scenario + BattleSimulation (Core, authoritative)
        |
        | AgentView / LastEvents / Tick / Outcome
        v
Presentation state (Client, bounded and disposable)
        |
        | immutable display models and UI commands
        v
MonoGame panels and input integration (Client)
```

Core remains the only gameplay authority. The client may:

- remember a selected entity ID;
- retain a bounded copy of already-published events;
- derive display text and final counts;
- map clicks to UI commands.

The client may not:

- alter an `AgentView`;
- fabricate gameplay events;
- infer and write targets or intents;
- advance Core outside the existing fixed scheduler;
- influence simulation behavior because an entity is selected.

### Presentation-state components

Add small, GPU-independent client classes:

| Component | Responsibility |
| --- | --- |
| `AgentSelection` | Store selected entity ID, resolve click candidates deterministically, and clear on reset |
| `BattleEventFeed` | Ingest ordered events, deduplicate by sequence, cap at 200, and expose scroll-window data |
| `MatchSummaryFactory` | Derive immutable final summary data from outcome, tick, tick rate, seed, and agent views |
| `PlaybackController` | Provide one Play/Pause/Menu command boundary for keyboard, compact bar, and modal |

These classes belong under
`src/AutonomousArena.Client/Presentation/`. They must not reference
`GraphicsDevice`, `SpriteBatch`, or platform input types. A small
`tests/AutonomousArena.Client.Tests` xUnit project will verify them without
opening a game window.

### MonoGame UI components

Add or extract narrowly scoped render/input components:

| Component | Responsibility |
| --- | --- |
| `UiButton` | Shared button bounds, hover/press detection, and drawing used by the bar, summary, and modal where practical |
| `ControlBar` | Draw and map Play/Pause/Menu buttons |
| `AgentInspectorPanel` | Draw selected-agent state |
| `BattleEventLogPanel` | Draw the visible event window and manage pointer-local scrolling |
| `MatchSummaryPanel` | Draw terminal result and map Replay/Menu actions |

The exact class split may be reduced if the result is simpler, but
`ArenaGame.cs` must not absorb all layout, rendering, formatting, hit testing,
and scroll behavior. Reuse the current `MenuButton`/`MenuOverlay` behavior where
that prevents duplication; do not introduce a general-purpose UI framework.

### State ownership

| State | Owner | Reset condition |
| --- | --- | --- |
| Agents, tick, events, outcome | Core `BattleSimulation` | New simulation |
| Seed, tick rate, map and roster | Core `Scenario` | New scenario |
| Playing/paused | Client playback controller | Explicit command or terminal outcome |
| Modal visibility | Existing client menu | Toggle, Play, or Exit |
| Selected entity ID | Client selection | Empty-arena click, reset, or replay |
| Bounded event history | Client event feed | Reset or replay |
| Event-log scroll offset | Client event panel/feed | Reset/replay or user scroll |
| Match summary | Client presentation | Reset or replay |
| Camera | Existing client camera | Existing reset policy |

### Update order

Each client frame should follow this logical order:

1. update input snapshots;
2. route pointer input to visible UI from highest layer to lowest;
3. apply one resulting UI/playback command;
4. handle arena selection and camera only if no UI consumed the pointer;
5. advance zero or more fixed simulation ticks;
6. after each `AdvanceOneTick`, ingest that tick's `LastEvents`;
7. after a terminal outcome, pause and create the summary once;
8. update non-authoritative presentation state;
9. render arena, HUD panels, control bar, summary, then modal menu.

This order prevents clicks through panels, missed multi-tick events, and a
one-frame-late terminal summary.

## Layout

The reference window remains 1280x720:

- top: compact status/control bar;
- center-left: arena viewport;
- right: fixed-width event log;
- lower-left or left overlay: selected-agent inspector;
- center overlay at terminal outcome: match summary;
- full-window top layer: existing modal menu.

The layout must remain usable at the current minimum client window size. Panel
bounds are recalculated from the current viewport on resize. The arena camera's
screen transform must use the arena content rectangle rather than assume that
the entire window is drawable arena.

No new textures, icons, animation system, or external font package is required.
Use the current SpriteFont, rectangles, text, faction colors, and clear
selection/hover outlines.

## Testing strategy

### Automated

Add GPU-independent tests for:

- nearest candidate and entity-ID tie-breaking;
- empty-click clearing;
- dead-agent selection persistence;
- selection reset;
- event feed ordering, deduplication, 200-entry cap, and reset;
- multi-tick ingestion so no tick's events are lost;
- event-log scroll clamping and bottom pinning;
- summary winner labels, survivor counts, terminal tick, duration, and seed;
- replay returning to paused state and clearing disposable presentation state;
- Play/Pause/Menu commands sharing one playback state.

Keep all existing Core determinism tests unchanged. Run the canonical local
gate after focused client tests.

### Manual Windows smoke

Record a manual pass that proves:

1. the game opens and starts paused;
2. Play advances ticks;
3. Pause stops ticks;
4. Menu pauses and opens the modal;
5. modal Play, Pause, Escape, and Exit Game behave as documented;
6. clicking an agent pins its inspector;
7. moving the pointer away does not clear selection;
8. selecting an agent does not change simulation behavior;
9. the event log updates and scrolls without zooming the arena;
10. terminal summary values match the final status;
11. Replay Same Seed clears UI state and reproduces the same terminal result;
12. closing the window exits with code 0.

Manual interaction is not replaced by synthetic keyboard injection because SDL
did not receive the attempted injected input during the foundation smoke.

## Acceptance criteria

This phase is complete only when:

- an agent can be clicked and persistently inspected;
- a selected dead agent remains explainable until reset/replay;
- the event log is ordered, deduplicated, bounded, and scrollable;
- no events are skipped when one rendered frame advances multiple ticks;
- Play, Pause, and Menu are always visible and share the existing semantics;
- Exit Game remains available in the modal and exits cleanly;
- terminal summary winner, survivors, duration, tick, and seed are correct;
- Replay Same Seed starts paused with cleared presentation state;
- same-seed Core hashes and outcome are unchanged;
- focused client tests and the canonical local verification gate pass;
- the Windows package succeeds;
- the direct manual smoke checklist is recorded;
- no GitHub Actions workflow or hosted-CI completion requirement remains;
- no Critical or High review finding remains unresolved.

## Explicitly deferred

- morale, retreat, squads, formations, or command hierarchy;
- terrain, obstacles, navigation meshes, or pathfinding;
- save files, serialized replays, timeline scrubbing, or event export;
- multiplayer, networking, accounts, or services;
- audio, final art, particles, animation framework, or theme overhaul;
- event filters, search, bookmarks, or analytics dashboards;
- controller/touch support and formal accessibility framework;
- store packaging and non-Windows releases;
- hosted CI.

The recommended phase after spectator clarity is a separate design for morale,
retreat, and squad behavior. It must not begin until the clarity pass is
complete and its deterministic baseline is recorded.
