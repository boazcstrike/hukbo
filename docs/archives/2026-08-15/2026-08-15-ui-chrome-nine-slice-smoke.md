# UI chrome nine-slice smoke — `CH-4`, and the family closing in full — closed 2026-08-15

**Archived: reference only.** This is a finished record of manual testing that
has already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this
project remains `CLAUDE.md`, `SIMULATION-GAME-STANDARDS.md`,
`docs/development/testing.md`, and `docs/development/smoke-checklist.md`.

**This family has now closed in full.** Six rows were written on 2026-08-14 and
a person ran all six that evening; five passed and were lifted then, and `CH-4`
was rewritten rather than closed. It was run and passed by a person at an
interactive Windows desktop on 2026-08-15, so the last row and the section left
the live checklist together.

| Field | Value |
| --- | --- |
| Rows in family | 6 — five closed 2026-08-14, `CH-4` closed 2026-08-15 |
| Rows closed `PASS` and lifted here | 1 — `CH-4` |
| Rows still open in the live checklist | 0 — the section was deleted |
| Prior state | `CH-4` was rewritten on 2026-08-14 because it was unrunnable as first written |
| Closed and lifted | 2026-08-15 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-15 |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path | `./scripts/run.ps1` on an interactive Windows desktop |
| Optional screenshot paths | None recorded |

## What the row was for

Panel chrome draws inside the interface batch, which uses
`SamplerState.LinearClamp`, while the arena block above it uses `PointClamp`.
Linear filtering on a pixel-authored atlas can bleed neighbouring texels across
the joins between corner and edge cells. Whether that artefact is actually
visible was a question for eyes, and the answer decides whether the
implementation needs a nested `PointClamp` batch around chrome draws. The only
mitigation on the table was a nested `Begin`/`End` pair using `PointClamp`
around the chrome draws, at the cost of splitting the interface batch into
three. That cost was to be paid only if a tester reported the artefact.

The row as first written asked for all four interface-scale tiers. That was
unrunnable: `UiScalePolicy.Resolve` caps the reachable tier by viewport, so 125
per cent needs 1920x1080, 150 per cent needs 2560x1440, and 200 per cent needs
3840x2160. On a 1080p display 150 and 200 both resolve back to 125, which is
pre-existing behaviour and not a chrome defect. The row was rescoped on
2026-08-14 to the tiers a tester can actually reach, and it is the rescoped form
that ran.

The atlas is placeholder programmer art. It makes no historical claim, and "it
looks crude" was never a finding against this row.

## How the row closed

The tester reported the row passing and recorded no separate observation. The
`Actual` column below says exactly that and no more.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| CH-4 | With `NineSlice` active, set interface scale to 100 per cent and then to 125 per cent. At each, look closely at the four points where a rounded corner meets the straight edge of the menu panel | No pale seam, halo, or one-pixel smear at any of those joins. Record which tiers were actually reachable on the display used, and whether a seam appeared at either | 2026-08-15, tester at an interactive desktop. Passed, with no separate note recorded | PASS |

## What a later reader should be careful of

- **The seam question is settled only in the direction the pass points.** The
  row asked which tiers were reachable on the display used and whether a seam
  appeared at either; neither answer was written down. What is recorded is that
  a person looked and passed the row, so the nested `PointClamp` batch was not
  bought. **No agent may enrich this cell later.**
- **A pass at 100 and 125 per cent says nothing about 150 or 200.** Those tiers
  need a larger display than the row could reach, and a tester on a 2560x1440 or
  3840x2160 panel is looking at joins nobody has inspected.
