# Plans backlog

Deferred work the user has explicitly parked. Each entry names the decision
that deferred it and the document that holds the full context. An entry here
is not authorized work; it is a reminder that the question was decided
"later", not "never".

## From the visual improvement package (2026-07-28)

- **Earned red putong insignia (was OD-5).** A red head-wrapping earned by
  kills during a battle, which would require bounded client-side kill
  tracking from `Death` events as new presentation state. Excluded from the
  visual improvement pass by user decision on 2026-07-28. Context:
  the warrior appearance design and requirement
  R-W3.10 in the improve-visuals requirements note.
- **Shape-redundant faction marker (was OD-7).** A non-hue faction channel
  (shape or position) for color-blind spectators, beyond the current
  no-regression floor. Deferred by user decision on 2026-07-28. Context:
  requirement R-X (color-blind readability) in
  the improve-visuals requirements note and the accessibility
  section of the visual system integration design.
- **Jungle and plains ground texture exploration (follow-up to OD-6).** The
  approved pass shifts the default theme's ground toward cogon olive-gold.
  The user additionally wants a look at jungle and plains ground treatments
  as distinct battlefield looks. Any such work stays procedural under the
  package's boundary 4 (no textures, no content-pipeline additions) unless a
  future design revisits that boundary. Context:
  the battlefield environment design.

## From the second-round lag report (2026-07-30)

- **Formation blocking at 500 agents.** Warriors spend long stretches unable to
  move in the crush: 33 330 blocked agent-ticks against 27 882 attack-capable
  ones in the reported round, with a longest unbroken blocked streak of 168
  ticks — 8.4 seconds of a warrior standing still. Parked by user decision on
  2026-07-30 after the same session's lag report was traced to this rather than
  to the frame loop. The full measured baseline, both seeds, and what a future
  change has to beat are in
  [`2026-07-30-formation-blocking-baseline.md`](2026-07-30-formation-blocking-baseline.md).
