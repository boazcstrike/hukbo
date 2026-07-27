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
  `docs/plans/improve-visuals/warrior-appearance-design.md` and requirement
  R-W3.10 in `docs/agents/improve-visuals/requirements.md`.
- **Shape-redundant faction marker (was OD-7).** A non-hue faction channel
  (shape or position) for color-blind spectators, beyond the current
  no-regression floor. Deferred by user decision on 2026-07-28. Context:
  requirement R-X (color-blind readability) in
  `docs/agents/improve-visuals/requirements.md` and the accessibility
  section of `docs/plans/improve-visuals/visual-system-integration-design.md`.
- **Jungle and plains ground texture exploration (follow-up to OD-6).** The
  approved pass shifts the default theme's ground toward cogon olive-gold.
  The user additionally wants a look at jungle and plains ground treatments
  as distinct battlefield looks. Any such work stays procedural under the
  package's boundary 4 (no textures, no content-pipeline additions) unless a
  future design revisits that boundary. Context:
  `docs/plans/improve-visuals/battlefield-environment-design.md`.
