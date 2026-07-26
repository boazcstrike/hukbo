# Battle Events Inspector Design

## Goal

Turn Hukbo's right-side Battle Events debug feed into an interactive inspector
that makes dense combat history easy to filter, navigate, select, and examine
without disrupting the live battle view.

## Current State

The panel is a fixed-width, newest-at-bottom feed retaining up to 200 events.
Mouse-wheel scrolling and bottom pinning work, but the list has no visible
scroll position, filters, selection, event details, or explicit way to resume
following the latest events. Rows have little visual hierarchy, which makes
busy battles difficult to scan.

## Approved Interaction Model

Use a split inspector:

- The upper region contains controls and the scrollable event list.
- The lower region shows details for the selected event.
- Selecting a historical event preserves the user's selection and scroll
  position while new events arrive.
- A clear `Latest` action returns the list to live-follow mode.

Expandable rows were rejected because opening details would destabilize the
scroll position. A slide-over details view was rejected because it would hide
the surrounding event context and slow comparison.

## Layout and Components

The panel keeps the existing tactical debug visual language and right-side
placement. It gains:

1. A header containing the title, visible filtered/total event count, and
   live-follow status.
2. Compact filter controls for event type and actor/team, plus text search and
   a one-action reset when filters are active.
3. A scrollable event list with a visible scrollbar, selected-row treatment,
   event-type cues, and distinct timestamp, actor, and action hierarchy.
4. A contextual `Latest` action when the user is not following the bottom.
5. A fixed details region showing every available field for the selected
   event.
6. Explicit empty states for no battle events and no filter matches.

The panel must remain usable within its current width. Long values are clipped
or wrapped within panel bounds rather than drawing over the arena.

## Navigation

- Mouse wheel continues to scroll the list.
- Clicking a row selects it.
- Up and Down move selection through the filtered list.
- Home and End select the first and last filtered event.
- The `Latest` action selects the newest matching event, scrolls to the bottom,
  and restores live-follow mode.
- New events append normally while live-follow is active.
- New events do not steal focus, selection, or scroll position while the user
  is inspecting history.

Keyboard interaction applies only while the inspector has focus so it does not
interfere with existing game controls.

## Filtering and Data Flow

The inspector derives a filtered view from the existing retained event
collection. Filtering never changes simulation state or deletes retained
events.

Filters combine using AND semantics:

- event type;
- actor/team when those values are available;
- case-insensitive text search across the human-readable event data.

Selection is identified against the underlying retained event rather than only
its current filtered index. If the selected event no longer matches the
filters or leaves the retained 200-event window, the details region clears and
shows a neutral selection prompt. Clearing filters restores the full retained
view.

## Visual Direction

Use a compact tactical-console aesthetic consistent with Hukbo:

- restrained dark surfaces and existing palette tokens;
- event-type accent marks instead of full-row color flooding;
- strong selected and keyboard-focus states;
- compact labels with readable values;
- no decorative animation that competes with the battle;
- status changes conveyed by text and shape as well as color.

## Edge Cases

- No events: explain that events will appear when the battle begins.
- No filter matches: show the active-filter empty state and a reset action.
- Selected event expires from retention: clear details safely.
- Available panel height is small: preserve the controls and a usable minimum
  for both list and details, clipping detail overflow within panel bounds.
- A filter removes the newest event: `Latest` targets the newest matching
  event.

## Verification

Focused tests should prove:

- combined filters return the expected events;
- reset restores the full retained list;
- selection and details stay stable when new events arrive;
- live-follow resumes through `Latest`;
- keyboard navigation respects filtered ordering and boundaries;
- selection clears safely when filtered out or evicted;
- empty and no-match states render without exceptions;
- text and interaction bounds remain inside the panel.

Run the relevant client tests first, then the repository's formatting, build,
and broader test scripts. Visually inspect the running panel at normal and
reduced window sizes, with an active battle generating enough events to test
history navigation.

## Scope

This change is limited to the Battle Events debug interface and directly
required tests/documentation. It does not change battle simulation behavior,
event retention capacity, event generation, replay data, or unrelated HUD
panels.
