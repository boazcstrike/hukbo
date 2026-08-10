# UI and UX Improvement Plan

**Status:** Implemented; automated verification complete, manual smoke pending

**Date:** 2026-07-31

**Implementation authorization:** Approved by the user on 2026-07-31

**Completion evidence:** [implementation-report.md](implementation-report.md)

## Goal

Deliver a responsive and legible current UI, a persisted startup display mode,
one historically bounded elite theme, and restrained interaction animation
without changing deterministic battle behavior.

## Known facts

- UI fonts already use role-specific assets, whole-pixel placement, and draw
  scale `1.0`.
- The default client is `1280x720`, while the current menu is `912` px tall.
- Maximize/restore exists; fullscreen and its persisted setting do not.
- Theme roles are centralized and shared by five stable themes.
- The client has an existing three-level motion setting and an unscaled frame
  time source.
- Existing interactive UI checks are still `PENDING`.

## Material assumptions

- A global UI scale is more usable than changing text without its controls and
  hit targets.
- The first fullscreen release can apply on startup/next launch.
- Existing theme IDs remain stable; the historical direction is a sixth theme.
- Cebu 1521 chiefly court was selected as a **Provisional reconstruction**
  because it is the most historically bounded direction available in this
  plan.

## Affected areas

- `src/Hukbo.Client/ArenaGame*`
- `src/Hukbo.Client/UI/*`
- `src/Hukbo.Client/Theming/*`
- `src/Hukbo.Client/Settings/*`
- `src/Hukbo.Client/Content/Content.mgcb`
- `src/Hukbo.Client/Content/Fonts/*`
- `src/Hukbo.Client/Content/Themes/ui-theme-standards.json`
- corresponding GPU-independent client tests
- `docs/development/testing.md`

No `Hukbo.Core`, snapshot, replay, battle hash, or headless change is expected.

## Success criteria

- At every supported viewport, menu controls remain visible and operable.
- At `100%` through the supported Windows DPI values, each chosen font tier is
  drawn at `1.0` scale on whole pixels and is visibly crisp.
- The user's UI scale and startup display mode survive a clean restart.
- Startup windowed and soft-fullscreen modes both produce correct layout and
  aligned hit targets.
- Existing theme IDs continue to load; the new historical theme meets the
  contrast contract and its evidence labels remain documented.
- With motion off, all UI transitions snap. With reduced motion, no positional
  UI animation occurs.
- Same battle seed and commands continue to produce identical deterministic
  outputs.

## Phase 0 — Baseline evidence

### UI-00: capture a viewport and DPI baseline

Capture menu, battle HUD, inspector, event feed, and report using the matrix in
[`current-ui-ux-audit.md`](current-ui-ux-audit.md).

**Verification:** screenshots and recorded client/back-buffer dimensions exist
for every available combination; each result is marked observed, unavailable,
or blocked.

### UI-01: add diagnostic dimensions

Plan a client-only diagnostic event that records client bounds, viewport,
back-buffer, desktop display mode, chosen scale tier, and window mode at startup
and on size changes. Follow the existing `DiagnosticLog` contract.

**Verification:** Debug logs contain the dimensions without writing from
`Hukbo.Core`; disabled logging remains allocation-free.

## Phase 1 — Responsive shell

This phase must precede new settings controls.

### UI-10: define viewport and safe-area policy

Decide the minimum supported client area and safe margins. Define which regions
are fixed, which can shrink, and which use surplus space.

Recommended baseline:

- preserve the arena as the primary flexible region;
- use discrete chrome metrics;
- keep all control hit targets above the existing minimum;
- use a sectioned or scrollable menu when content cannot fit.

### UI-11: make the menu fit

Replace the fixed `360x912` assumption with a viewport-contained layout.
Separate sections such as Match, Presentation, Accessibility, and Display so
adding settings does not produce one unbounded vertical column.

### UI-12: make fixed-width bars responsive

Define wrapping, compact labels, or overflow behavior for the control bar,
status bar, right column, and inspector. Avoid fractional geometry.

**Verification for Phase 1:**

- pure layout tests cover the default, proposed minimum, `1920x1080`, and a tall
  aspect ratio;
- every rectangle is inside the safe area and non-overlapping;
- focus order matches visual order after sections or scrolling;
- mouse and keyboard operation are verified manually.

## Phase 2 — Crisp, scalable typography and chrome

### UI-20: add pre-baked asset tiers

Build Rajdhani/Bebas font assets for the approved scale tiers. Keep each tier's
draw scale at `1.0`; do not enlarge an existing atlas during drawing.

### UI-21: centralize scale policy

Introduce a pure client-side `UiScalePolicy` that chooses:

- `Auto`;
- `100%`;
- `125%`;
- `150%`;
- `200%`.

The result selects both a font set and integer layout metrics. `Auto` chooses
the largest tier that still passes layout constraints.

### UI-22: persist the UI scale

Add the selected UI scale to client settings with a backward-compatible schema
migration. Existing settings should default to `Auto`, not reset wholesale.

Every whole-record settings writer must carry the new scale and display-mode
fields forward. This includes theme changes, gore, motion, auto-camera, and
army-composition saves. Add cross-setting regression tests proving that
changing any one preference preserves every other persisted preference.

**Verification for Phase 2:**

- every role and tier resolves a valid font asset;
- `Content.mgcb`, `UiFontRamp`, and the font-set loader remain in exact parity;
- text draw commands remain scale `1.0`;
- geometry is integral;
- clipping and containment tests pass at each tier;
- manual checks cover windowed, maximized, and each available Windows DPI value;
- screenshots show no compositor stretch or texture-softened glyphs.

## Phase 3 — Startup display mode

### UI-30: define and persist display mode

Add a stable startup display enum with `Windowed` and `Fullscreen`. Treat the
current behavior as the backward-compatible default for existing settings.
Increment the settings schema while accepting the prior schema.

As with UI scale, every existing whole-record save path must preserve the
display mode. The cross-setting round-trip matrix must include changes made
through theme, composition, gore, motion, and auto-camera controls.

### UI-31: apply mode before graphics initialization

For fullscreen, use MonoGame soft/borderless fullscreen
(`HardwareModeSwitch = false`) and configure the back buffer for the current
display. Apply the selected value during startup.

### UI-32: expose the selector

Place the selector in the responsive Display section and label it
`Applies next launch`. Do not present the existing Max button as fullscreen.

### UI-33: defer live switching

Do not add F11 or immediate switching in this phase. A later proposal must
cover `ApplyChanges`, device-reset/resource recreation, transient input state,
restoring prior window dimensions, and the behavior of Max while fullscreen.

**Verification for Phase 3:**

- settings round-trip and prior-schema migration tests pass;
- every existing setting writer preserves UI scale and display mode;
- two cold starts in each mode reproduce the saved choice;
- viewport and hit-testing remain aligned;
- soft fullscreen returns safely to windowed mode after changing the setting
  and restarting;
- resource/device logs contain no reset failure.

## Phase 4 — Historically bounded theme

### UI-40: approve one direction

Choose one region/date direction from
[`historical-royal-visual-direction.md`](historical-royal-visual-direction.md).
Record the evidence labels, allowed materials, and exclusions.

### UI-41: add a sixth theme

Recommended stable ID: `datu-court`.

Add it to the standards data, fallback definitions, and exact-set tests without
renaming or retuning the five existing IDs. Decide separately whether it is the
new-user default.

### UI-42: prototype material cues

Apply subtle, presentation-only cloth, wood, or gold cues. Reuse semantic roles
for focus, selection, warnings, teams, and disabled states. Do not encode combat
identity or history into simulation data.

**Verification for Phase 4:**

- required raw contrast pairs pass;
- rendered alpha/texture states pass visual contrast review;
- team and severity colors remain distinct;
- high-contrast mode remains flat and fully usable;
- all claims and original motifs carry the correct evidence label;
- a cultural review is recorded if required by the selection gate.

## Phase 5 — Restrained UI motion

### UI-50: add a bounded transition primitive

Create a client-only transition value with:

- elapsed time from `GameTime.ElapsedGameTime`;
- clamped progress and safe handling of large frame deltas;
- pure easing functions;
- no random source, timer, allocation in settled frames, or unbounded map;
- immediate completion when motion is off.

### UI-51: priority interactions

| Surface | Full motion | Reduced motion | Target duration |
|---|---|---|---:|
| Button hover | fill/border interpolation | same | 90–120 ms |
| Button press | 1 px decorative inset plus color | color only | 40–70 ms |
| Menu/prompt scrim | opacity | opacity | 100–120 ms |
| Menu/prompt panel | opacity | opacity | 140–180 ms |
| Match summary/report | opacity with surface-specific duration | opacity | 120–220 ms |

Focus state and input availability are immediate even while decoration settles.
The implemented first pass deliberately keeps entrance motion opacity-only:
this avoids layout movement, preserves final hit geometry throughout the
transition, and gives Reduced and Full a comfortable common baseline. Full
motion's positional feedback remains limited to the one-pixel button press.

### UI-52: secondary feedback

**Implemented on 2026-08-07.** Every surface below now has bounded, client-only
motion. The task breakdown, the design decisions, and the gate output are in
the UI-52 secondary feedback plan and its design document, both of which have
since been archived out of `docs/plans/`. The stepper arrows in the army composition panel were
added to the same pass, because leaving them static would have made one set of
arrows in the menu animate while an adjacent set snapped.

After priority motion is stable:

- new-event row accent, without moving or reordering rows: `160–220` ms;
- selected-agent inspector border accent: `140–180` ms;
- selector arrow/active marker interpolation: `90–140` ms;
- play/pause/speed active strip: `100–140` ms;
- optional status-badge emphasis: `450–650` ms, non-looping.

### UI-53: avoid harmful motion

Do not implement:

- text scaling, bouncing, or fractional translation;
- elastic or cartoon overshoot;
- screen shake, hit-stop, camera punch, or flashing as UI feedback;
- animated list reflow before focus and reading order are proven;
- motion keyed to simulation ticks;
- close animations that block the next user action.

The motion policy follows WCAG's requirement to allow disabling non-essential
motion triggered by interaction:

- [W3C: Animation from Interactions](https://www.w3.org/WAI/WCAG22/Understanding/animation-from-interactions)
- [Microsoft: Timing and easing](https://learn.microsoft.com/en-us/windows/apps/design/motion/timing-and-easing)

**Verification for Phase 5:**

- pure progression and large-delta tests pass;
- all states settle exactly on target;
- hit rectangles do not move;
- Off snaps, Reduced removes positional changes, and Full remains restrained;
- no transition state enters Core, snapshots, hashes, or headless output;
- keyboard focus remains clear throughout every transition.

## Phase 6 — Integration and review

1. Run focused client tests after each phase.
2. Run formatting and Release client tests after targeted checks pass.
3. Run `./scripts/verify.ps1` and record its actual output.
4. Execute the approved rows in `docs/development/testing.md`; leave any
   unperformed row `PENDING` and any unavailable environment `BLOCKED`.
5. Compare deterministic state hash, event hash, winner, and ordered event
   stream against the same seed/commands.
6. Inspect the complete diff for accidental theme-ID, settings-reset, Core, or
   unrelated visual changes.
7. Obtain an independent review for DPI behavior, accessibility, settings
   migration, and historical labeling.

## Risks and mitigations

| Risk | Mitigation |
|---|---|
| Windows DPI virtualization is mistaken for font scaling | Measure client, viewport, and screenshots before selecting tiers |
| Enlarged UI no longer fits | Complete responsive layout first; `Auto` obeys constraints |
| Settings migration resets preferences | Accept the prior schema and default only new fields |
| Live fullscreen causes device/resource failures | Startup-only first; design reset handling separately |
| "Royal" theme becomes an ahistorical composite | Require region/date selection and evidence labels |
| Motion impairs accessibility or input | Respect Off/Reduced; animate decoration, not hit targets |
| Animation affects determinism | Keep all state bounded and client-only |

## Unresolved approval gates

- historical direction and whether it becomes the default;
- minimum supported viewport;
- final scale-tier asset sizes and `Auto` thresholds after measurement;
- startup-only versus live fullscreen;
- whether textures are included in the first theme release;
- whether specialist historical/cultural review is required.
