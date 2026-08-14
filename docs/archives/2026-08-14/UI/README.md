# Current UI and UX Improvement Research

**Status:** Implemented; automated verification complete, manual smoke pending

**Date:** 2026-07-31

**Archived: reference only.** This is the finished 2026-07-31 UI and UX package
— the audit, the visual direction, the plan, and the implementation report. It
was implemented and merged, its automated verification was complete, and the
manual smoke rows it was waiting on were run by a person and closed on
2026-08-13, after which their sections left `docs/development/smoke-checklist.md`
whole. Never execute anything in this folder, never treat any of it as a live
task list, and never cite it as the reason to make a change. The live contract
for this project remains `CLAUDE.md` and `docs/development/smoke-checklist.md`.
Archived 2026-08-14. `UI-33`, live fullscreen switching, was deferred by the
plan itself rather than left unfinished by it.

**Implementation authorization:** Approved by the user on 2026-07-31

**Implementation record:** [implementation-report.md](implementation-report.md)

## Goal

Define a safe, historically bounded path to improve Hukbo's current UI and UX,
with special attention to:

- crisp, readable text when the window is maximized or displayed at high DPI;
- a persisted startup choice between windowed and fullscreen presentation;
- a color and material direction grounded in elite Philippine contexts of the
  1500s;
- restrained UI animation that improves feedback without affecting simulation
  determinism or accessibility.

## Executive recommendation

Use the following order:

1. Measure the present behavior at common viewport and Windows DPI combinations.
2. Make the menu and fixed-width chrome fit the supported viewport range.
3. Introduce pre-baked, discrete UI scale tiers whose fonts are always drawn at
   `1.0` scale on whole pixels.
4. Add a persisted, startup-only display mode selector. Defer live fullscreen
   switching until graphics-device reset behavior has an explicit design and
   test path.
5. Add a sixth, opt-in historical theme based on a **Cebu 1521 chiefly court**
   direction. Retain all five existing themes and their stable IDs.
6. Add short, client-only transitions to buttons, overlays, summaries, and
   selection feedback. Respect the existing motion setting.

This sequence avoids polishing UI elements that do not currently fit the
default `1280x720` window and avoids treating scaled-up bitmap fonts as a
typography solution.

## Principal findings

- The renderer already separates point-sampled arena art from linear-sampled
  UI and draws each font role at scale `1.0`. The old single-atlas scaling
  problem is not the current implementation.
- The remaining text problem is a combination of fixed pixel sizes, fixed
  layout metrics, and an unmeasured Windows DPI path. It should not be diagnosed
  as "pixel art" until DPI measurements and screenshots exist.
- The menu panel is `912` pixels tall while the default window is `720` pixels
  tall. It is centered without viewport clamping, so parts of the current menu
  can be off-screen before another setting is added.
- Hukbo has maximize/restore behavior, not fullscreen behavior. Settings are
  loaded early enough to apply a startup display choice cleanly.
- The semantic theme-token system is a good seam for a historical theme. A
  singular pan-Filipino "royal" style is not historically supportable; the
  design should name a region, date, and evidence boundary.
- UI interaction state changes are currently immediate. Several bounded
  animations can improve legibility and feedback without entering
  `Hukbo.Core`, snapshots, hashes, or replay state.

## Implemented decisions

1. **Historical direction:** Cebu 1521 chiefly court, explicitly labelled a
   **Provisional reconstruction**, is the sixth theme.
2. **Display semantics:** startup-only soft fullscreen, applied on the next
   launch.
3. **Scale policy:** global UI scale choices `Auto`, `100%`, `125%`, `150%`,
   and `200%`, capped to the largest tier that safely fits the viewport.
4. **Minimum viewport:** 1024x720 in windowed mode, with a two-column menu that
   remains fully contained at that size.
5. **Default theme:** `datu-court` is the new-user default; existing saved
   stable theme IDs are preserved.

## Documents

- [Current implementation audit](current-ui-ux-audit.md)
- [Historically bounded visual directions](historical-royal-visual-direction.md)
- [Approval-gated improvement plan](ui-ux-improvement-plan.md)
- UI-52 secondary feedback plan — the follow-on pass that completed Phase 5 on
  2026-08-07, since archived out of `docs/plans/`

## Non-goals

- No campaign, simulation, balance, replay, or snapshot changes.
- No runtime font generation or new UI framework in the first pass.
- No pseudo-Baybayin, generic tattoo motifs, modern national symbols, or
  unsupported pan-Philippine ornament.
- No animations driven by simulation ticks, randomness, or wall-clock APIs.
- No claim that interactive behavior is verified; the existing manual rows
  remain `PENDING` until they are actually performed.
