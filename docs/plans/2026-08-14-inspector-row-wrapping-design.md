# Agent inspector row wrapping — design

Status: **shipped and closed.** The six tasks below were built and merged at
`b566f88`, and smoke row `BR-10` was re-run by a person at an interactive
desktop on 2026-08-14 and passed. The plan document that authorized the work is
archived under the title "Agent inspector row wrapping — plan", named here in
prose because nothing outside `docs/archives/` may link into it. This design
stays live only because `tests/Hukbo.Client.Tests/AgentInspectorContentTests.cs`
cites it by name; the open vertical question in section 6 is still open.

## 1. The defect

Smoke row `BR-10` was run at an interactive desktop on 2026-08-14 and did not
pass. The tester's observation was that the agent inspector "does render, but the
width of the texts overextends the current small width of the info panel".

The row was written to catch a *vertical* fault — a 953-pixel panel running off
the bottom of the smallest supported 1024 × 720 window. The fault a person
actually found is horizontal, and the row fails as written, because its expected
observation requires the panel to fit "without clipping against the window edge"
without naming an axis.

## 2. Why it happens

The panel is 310 pixels wide (`InspectorWidth`, `src/Hukbo.Client/ArenaGame.cs`),
which `AgentInspectorContent.ComputeContentWidthBudget` reduces to a 277-pixel
text budget at 100 per cent UI scale — the panel width less twice the 14-pixel
padding and less the 5-pixel accent bar.

That budget is honoured by exactly five kinds of content, all of them long prose:
the evidence note, the warrior-name provenance block, the weapon-variant block,
the shield-variant block, and the appearance-preset block. Each is routed through
`AgentInspectorContent.WrapText` with the body font's own measure delegate.

Every other row is not. The four top-detail rows and the roughly twenty-six rows
`BuildLowerLines` produces are handed to the panel's private `DrawLine` as
finished single-line strings, and `DrawLine` ends at
`UiPrimitives.DrawText`, which is a plain `SpriteBatch.DrawString` with no bounds
test of any kind. `DrawLine` does test `xPosition >= Bounds.Right`, but that
compares the row's *starting* pen position against the panel edge; it never
measures the string, so a row that starts inside the panel and runs past its
right edge is drawn in full, over whatever is behind it.

**There is no horizontal clip anywhere in this panel.** The two bounds tests in
`AgentInspectorPanel.Draw` both compare a row's bottom against a maximum row
bottom, so the panel guards its vertical extent and nothing else.

The rows that overflow are not exotic. Measured in characters, against a budget
that holds roughly 46 characters of Rajdhani SemiBold at the baked 14-pixel body
size:

| Row | Longest form | Characters |
| --- | --- | --- |
| Combo attributes | `99.99% combo open / 99.99% combo continue / 99 max steps / 999 tick combo cooldown`, indented eight spaces | 99 |
| Footwork under pressure | `Footwork: Disengaging (broke off under pressure)` | 50 |
| Pressure | `Pressure: 9999 of 9999 basis points to break off` | 50 |
| Attributes | `15 dmg / 999 reach / 999 tick recovery`, indented eight spaces | 47 |
| Intent, backing away | `Intent: Backing away from close fighters` | 44 |

The combo-attributes row is more than twice the budget in its worst form and
overflows in its ordinary form as well. This is why the tester saw the defect
without hunting for it.

`BR-9` passed in the same session, and there is no contradiction: `BR-9` asks
whether the two intent strings are legible and distinct from each other, which
they are. It does not ask whether they stay inside the panel.

## 3. What is being changed

**D1 — every row is wrapped.** No string reaches `DrawText` without having been
measured against `ComputeContentWidthBudget` first. The four top-detail rows and
every row from `BuildLowerLines` are routed through `WrapText` with the same body
font measure delegate the five prose blocks already use. This is the whole of the
fix for the reported defect: after it, no row can be wider than the budget,
because no row is drawn without having been split to fit it.

**D2 — a continuation line is indented.** A row that wraps draws its second and
later lines with a hanging indent, so `Footwork: Disengaging (broke off under
pressure)` reads as one row that ran on rather than as two unrelated rows. The
indent is applied by the wrapping helper, not by the caller, so a caller cannot
forget it.

**D3 — the two pathological rows are split at the source.** The combo-attributes
and attributes rows are single strings that pack three and four values behind an
eight-space indent. Wrapping them alone would produce three ragged lines. They
are instead emitted as one row per value group, which is shorter, reads better,
and costs the panel fewer lines than wrapping the packed form would.

**D4 — the panel keeps its 310-pixel width.** Widening it is the obvious
alternative and is rejected. The reported defect is overflow, not shortage; the
panel already competes with the arena for horizontal room at the 1024 × 720
minimum window, and a width change moves every geometry test in
`AgentInspectorContentTests` for a fault that wrapping fixes outright. The
budget arithmetic, the accent bar, and the padding are all unchanged.

**D5 — the vertical guard is retained exactly as it is.** A wrapped row that
would fall past the panel's bottom is refused, never drawn over the edge. The
existing contract in `AgentInspectorContent`'s own remarks already states this —
an under-estimate of reserved lines "can only drop a line, never overflow the
panel" — and this change does not weaken it.

That has a consequence worth stating plainly rather than burying: wrapping raises
the number of lines a fully-loaded warrior needs, so at the smallest supported
window such a warrior will drop more of its trailing provenance rows than it does
today. **This change does not fix that, and does not claim to.** The panel needed
a scroll affordance or a shorter default row set before this change and still
does afterwards. It is recorded in section 6 as the open question it is, and the
`BR-10` row is not closed by this work — a person still has to look.

**D6 — the missing test is added.** The suite pins the width budget arithmetic
and pins that *wrapped* content fits it. Nothing pins that an *unwrapped* row
fits it, which is the exact gap this defect fell through. The new test takes
every string the row formatters can produce at their longest realistic values,
wraps each at the 277-pixel budget across the same 5, 6, 7 and 8 pixels-per-
character theory range the existing wrap tests use, and asserts no returned line
exceeds the budget.

## 4. What is not being changed

No simulation code, no `Hukbo.Core` file, no state or event hash, no preset, and
no movement rule. This is a presentation-only change inside `Hukbo.Client/UI`,
and the canonical gate's determinism workload is untouched by it.

No theme role, no colour, and no font rung changes. The row set a warrior can
produce is unchanged except for D3, which splits two existing rows and adds no
new fact to the panel.

## 5. The nine questions

`SIMULATION-GAME-STANDARDS.md` section 10 requires all nine to be answered. The
load-bearing one for this change is the discoverability question.

1. **What does it do?** Keeps inspector text inside the inspector.
2. **Can a spectator discover the effect without reading source?** Yes, and this
   is the entire point: today a spectator sees text spilling out of the panel
   over the arena behind it. After the change the text stops at the panel edge.
   The defect was discovered exactly this way, by a person looking at the screen.
3. **Does it reach the state hash?** No. Presentation only.
4. **Does it reach the event hash?** No.
5. **What tick stage does it land in?** None; it is drawn, not simulated.
6. **What is the total order?** Row order is unchanged and is the order
   `BuildLowerLines` returns.
7. **What is the historical claim?** None. No row's wording changes except the
   two D3 splits, which restate existing values and make no new claim.
8. **What does it cost per frame?** One measure pass per row per frame, over
   roughly thirty short rows, on the selected warrior only — the same per-row
   measure cost the five prose blocks already pay. No allocation is added on the
   unselected path, because the panel draws nothing when nothing is selected.
9. **How is it verified?** The client suite, plus the smoke row, which only a
   person may flip.

## 6. Open questions, not resolved here

- **The panel does not fit vertically at the minimum window and never did.** A
  953-pixel panel in a 720-pixel window drops rows by design. Whether the answer
  is a scroll affordance, a collapsible provenance section, or a shorter default
  row set is a separate decision that needs its own design document. This change
  makes it slightly more visible and does not address it.
- **Whether the provenance blocks belong in this panel at all.** Five prose
  blocks — weapon variant, shield variant, appearance preset, warrior name, and
  evidence — account for most of the panel's height. They may belong behind a
  disclosure rather than in the default view.
