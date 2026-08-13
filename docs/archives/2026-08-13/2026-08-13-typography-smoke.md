# Typography smoke — closed 2026-08-13

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this
project remains `CLAUDE.md` and `docs/development/smoke-checklist.md`; nothing
in this file overrides either of those.

This record exists because `docs/development/smoke-checklist.md` holds open
work only, not a history of what has already been tested. Once a family has
no open row left, that file's own rule is to delete the family from it
outright rather than let it accumulate closed rows. The "## Typography smoke"
family closed in full on this date, and this file is where its fourteen rows,
62 through 75, went when they were lifted out.

## What these rows were for

Added by the font and text quality change. **Not performed.** The automated
gate proves the ramp is internally consistent, the theme catalog resolves
every role, and text positions round to whole pixels; none of that proves the
resulting text reads as crisp, correctly sized, or correctly hierarchical to a
person watching it, which is the only thing these rows are for.

**Correction — there is no automated em-dash check.** An earlier revision of
this section claimed a "compiled em-dash byte assertion passes". No such
assertion exists. Searching `tests/` for `.xnb`, `CharacterMap`, `2014`,
`8212`, or `em-dash` returns nothing. The only thing backing the em dash is the
second `CharacterRegion` in each of the 24 `.spritefont` files under
`src/Hukbo.Client/Content/Fonts/`, which spans `&#8211;` to `&#8212;` and so
asks the content builder to include the glyph. Whether the builder actually
produced it, and whether the running game draws it instead of throwing, was
verified by row 71 below and by nothing else.

Per `CLAUDE.md` section 6, only a human at an interactive Windows desktop may
flip one of these rows to `PASS`. Compilation, unit tests, and a
window-opening probe do not.

| Field | Value |
| --- | --- |
| Rows | 14 |
| Source family | 1 |
| Lifted on | 2026-08-13 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-13 |
| Machine/platform | Not captured by the tester. The reporting machine for this repository's recent runs is Windows 11 Pro 10.0.26200 x64 with an NVIDIA GeForce RTX 4070 SUPER on a 2560x1440 display at 125% Windows scaling |
| Source commit | Not captured by the tester. `main` was at `8da5d92` when these results were transcribed |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

## Typography smoke

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| 62. Glyph crispness at the smallest rung | Event log and sound log rows have solid stems and clean edges, with no grey mush and no ragged stair-stepping. | The tester at the desktop reported: "passed." | PASS |
| 63. Glyph crispness at the largest rung | The wordmark is sharp at every edge with no fringing. | The tester at the desktop reported: "passed." | PASS |
| 64. Wordmark hierarchy | The wordmark is unmistakably larger and heavier than the subtitle beneath it. | The tester at the desktop reported: "passed." | PASS |
| 65. Header face renders as capitals | Every panel header renders fully and unclipped inside its header strip. | The tester at the desktop reported: "passed." | PASS |
| 66. Mixed-case strings stay on the body face | Theme names, gore levels, the controls label, the winner line, the distribute action, and every inspector line render with real lowercase letters. | The tester at the desktop reported: "passed." | PASS |
| 67. No vertical clipping | No descender is cut off in any panel at any rung. | The tester at the desktop reported: "passed." | PASS |
| 68. No horizontal overflow | No label spills past its panel, button, chip, or column, and no ellipsis appears where text previously fit. | The tester at the desktop reported: "passed." | PASS |
| 69. Row alignment | Event log columns, sound log rows, and inspector rows sit on consistent baselines with no drift down the list. | The tester at the desktop reported: "passed." | PASS |
| 70. Agent inspector evidence note | The longest evidence note wraps fully inside the panel with nothing cut off. | The tester at the desktop reported: "passed." | PASS |
| 71. Em-dash regression | Staging an army composition change renders the notice with a real em dash and does not crash. | The tester at the desktop reported: "passed." | PASS |
| 72. Theme cycling | All six themes render text at the active UI scale with correct contrast, and no theme reveals a clipped or misaligned label the others hide. | The tester at the desktop reported: "passed." | PASS |
| 73. Window resize and automatic scale tiers | With UI Scale set to Auto, resizing selects 100% at 1280x720, 125% at 1920x1080, 150% at 2560x1440, and 200% at 3840x2160. Each tier stays crisp, re-lays out without clipping, and keeps every menu control visible. | The tester at the desktop reported: "passed." | PASS |
| 74. Subpixel blur is gone | Panning, zooming, and pausing produce no shimmering or swimming text. | The tester at the desktop reported: "passed." | PASS |
| 75. Display scaling | Record the appearance at 100% and at 150% Windows scaling. Fed the separate, gated display-scaling measurement task. | The 100% reading was taken during implementation (viewport 1280×720, client bounds 1280×720, equal). The user declined the 150% reading on 2026-07-28, having no use for the display-scaling remedy this row was gating. The 2026-08-13 batch report named row 75, but this row asked for the 150 percent Windows-scaling reading declined on 2026-07-28, and that reading was never re-taken; it is carried as `DECLINED` unless a tester confirms otherwise. | DECLINED |

## What this row family stopped gating

Row 75 gated a separate, standalone display-scaling measurement task: it
asked for a 150 percent Windows-scaling reading so that whatever remedy the
measurement pointed to could be scoped and built. That remedy was built
anyway, on 2026-08-11, when the game gained a per-monitor DPI awareness
declaration on Windows, made before any window or graphics device exists.
That declaration was written to fix a different, already-observed defect —
three rows of the "Responsive menu, startup display, and UI motion smoke"
family, `UI-2`, `UI-4`, and `UI-6`, had failed against a display running at
125 percent scaling because the process was being handed a virtualised,
bitmap-stretched viewport rather than its true pixel size. The same fix
removes the reason row 75 was asking for a 150 percent reading in the first
place, so the row closes as `DECLINED` rather than waiting on a measurement
nobody still needs. The family it shared a smoke run with, "Responsive menu,
startup display, and UI motion smoke", also closed on 2026-08-13 and has its
own archive record of that title; this file does not link to it.
