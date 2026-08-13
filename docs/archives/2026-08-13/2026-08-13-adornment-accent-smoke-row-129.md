# Adornment accent legibility, smoke row 129 — closed 2026-08-13

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this
project remains `CLAUDE.md` and `docs/development/smoke-checklist.md`;
nothing in this file overrides either of those.

This record lifts a single row, not a whole family. `docs/development/smoke-checklist.md`
holds a section titled "Visual improvement smoke — the three open rows" with
rows 128, 129, and 131, all part of the `VIS-043` family and all reopened on
2026-08-11 against the same fix. On 2026-08-13 a person at an interactive
Windows desktop re-ran all three. Only row 129 passed. Rows 128 and 131 are
still open in the live checklist; this file records nothing new about their
status beyond that, and no guess is made here about what a future re-run of
either will show.

## Row 129's history

Row 129, "Adornment accents visible at maximum zoom without breaking any
read," was written as part of the `VIS-043` improve-visuals family and stood
`PENDING` from the time it was written until 2026-08-11.

On 2026-08-11, a tester at the desktop ran it and recorded `FAIL` — not
clear. The investigation that followed found the cause in
`PawnGeometry.CreateAdornmentAccents`, which sized an accent mark as
`min(2, round(2 × apparentScale))`. Because the constant `2` appeared on both
sides of the `min`, the second term could never win, so an accent mark was
two pixels at every apparent scale, including at the maximum-zoom station the
row asks a tester to observe from. The row was reopened `PENDING` against a
fix.

The fix is recorded in `docs/plans/2026-08-11-armor-accent-trample-legibility-design.md`.
That design document confirms the specific claim carried into this record:
the accent cap was changed from an absolute two-pixel ceiling to a
scale-relative one, computed as `max(1, round(MaxAccentPixelSizeAtApparentScale1
× apparentScale))`. The document states this directly — "two pixels at
apparent scale 1 exactly as the requirement states, and five at the `2.40`
clamp ceiling" — so an accent mark now grows from two pixels at apparent
scale 1 up to five pixels at the apparent-scale clamp ceiling, rather than
being pinned at two pixels everywhere.

On 2026-08-13, the row was re-run at the desktop and passed.

## Evidence — 2026-08-13 run

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-13 |
| Machine/platform | Microsoft Windows 11 Pro 10.0.26200 x64 |
| Source commit | `8da5d92`, the repository head at the time of the run; the working tree also carried uncommitted changes from a parallel session |
| Launch path (`source` or package path) | `source`; `./scripts/run.ps1` is the only supported source launch path, and was not separately recorded by the tester |
| Optional screenshot paths | None recorded |

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 129. Adornment accents visible at maximum zoom without breaking any read | At the maximum-zoom station, default theme, close in on a pawn wearing adornment accents (gold accents I4/I5, or the C3 gold-edged putong). The accents are visible without breaking weapon-role, faction, or equipment recognition. | 2026-08-13, tester desktop: PASS. Re-run of the 2026-08-11 accent-size fix. | PASS |

If a later change touches accent sizing, the accent area cap, or the
apparent-scale clamp, write a fresh row in the live checklist rather than
reviving this one.
