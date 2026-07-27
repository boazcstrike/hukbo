# Font and Text Quality — Plan

Design document: `docs/plans/2026-07-27-font-text-quality-design.md`. Read it
first; it carries the root-cause analysis, the typeface rationale, the size
ramp, the architecture, and the nine standards answers.

Scope boundary, enforced as an acceptance criterion: zero files change under
`src/Hukbo.Core`, `src/Hukbo.Headless`, `tests/Hukbo.Core.Tests`, or `scripts/`.

In the **Verified by** column, `GATE` means `./scripts/verify.ps1` proves it and
`MANUAL` means only a human at an interactive Windows desktop may flip the
corresponding row in `docs/development/testing.md` to `PASS`.

## Task list

| ID | Title | Files | What changes | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- | --- |
| T01 | Design document | `docs/plans/2026-07-27-font-text-quality-design.md` | Records the ramp, the two faces, the SemiBold justification, the nine standards answers, and the em-dash defect. | Document exists and answers all nine questions. | — | review |
| T02 | Plan document | `docs/plans/2026-07-27-font-text-quality.md` | This task list plus the verification strategy. | Document exists with an ordered task list and an explicit verification section. | T01 | review |
| T03 | Vendor the typefaces | `src/Hukbo.Client/Content/Fonts/{Rajdhani-SemiBold.ttf, BebasNeue-Regular.ttf, OFL-Rajdhani.txt, OFL-BebasNeue.txt, README.md}` | Copy from `github.com/google/fonts` at commit `7ff85c87f93ea6cca5f41c69f2e4edcb90240f26`, paths `ofl/rajdhani/` and `ofl/bebasneue/`. The README records upstream repository, path, commit, and retrieval date. | Both fonts have a valid TrueType signature; both license files carry the full OFL 1.1 text with the correct copyright line; the README names the commit. | — | review |
| T04 | Git binary handling | `.gitattributes` | Add `*.ttf binary` and `*.spritefont text eol=lf`. | `git check-attr binary` reports `set` for a vendored font; a fresh clone reproduces byte-identical files. | T03 | GATE — a corrupted font fails the content build loudly |
| T05 | Verify Bebas Neue glyph coverage | none — investigation only | Bake one Bebas descriptor and dump the compiled character map. | Recorded in the design document as verified or failed: whether all ninety-five ASCII glyphs are present, and whether lowercase codepoints yield capitals or `.notdef`. | T03 | GATE plus recorded evidence |
| T06 | Add six descriptors, rewire the content project | `src/Hukbo.Client/Content/Fonts/Ui*.spritefont` (six new), `src/Hukbo.Client/Content/Content.mgcb` | Six descriptors, each with an explicit `DefaultCharacter` and a second character region covering en dash and em dash. Six matching build blocks. The existing `Default.spritefont` block is retained for now so the current load path still works. | The Release build emits all six compiled assets, each with ninety-seven characters and a non-null default character. | T03, T05 | GATE |
| T07 | The pure ramp | `src/Hukbo.Client/Theming/UiFontRamp.cs` (new) | Role enumeration, asset identifier per role, pixel size per role, role name parser, conservative per-character advance estimate. | Compiles, and no MonoGame type appears in any signature. | T06 | GATE |
| T08 | Ramp tests | `tests/Hukbo.Client.Tests/UiFontRampTests.cs` (new), `tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj` | Link the content project file into the test output the same way the theme JSON is linked. Assert distinct asset identifiers, strictly increasing pixel sizes, parser round-trip and rejection, ramp-to-content parity in both directions, and a positive monotonic advance estimate. | All new tests pass. No graphics device, sprite batch, or game symbol appears in the file. | T07 | GATE |
| T09 | The font set | `src/Hukbo.Client/Theming/UiFontSet.cs` (new), `src/Hukbo.Client/ArenaGame.cs` | `Load` takes a loader delegate and populates a fixed array indexed by role. `ArenaGame` gains the set and keeps the existing single font for now. | Release build green; the game launches; all six roles resolve. | T08 | GATE |
| T10 | Theme JSON, additive phase | `src/Hukbo.Client/Content/Themes/ui-theme-standards.json` | Add a role-to-asset map and a slot-to-role map alongside the existing single font identifier and float scales. Extend the allowed asset identifiers to the six new ones plus the existing one. | The catalog loads. | T07 | GATE |
| T11 | Catalog plumbing, additive phase | `Theming/UiThemeCatalogDocuments.cs`, `Theming/UiThemeCatalog.cs`, `Theming/UiThemeCatalogFallback.cs`, `Theming/UiTheme.cs` | New transfer objects and records for both maps; mapping in the standards factory; a mirrored code fallback; validation that every role is present, every asset identifier is allowed, and every slot's role name parses. | Existing catalog tests still pass. New validation rejects a missing role, an unknown asset identifier, and an unknown role name. | T10 | GATE |
| T12 | Catalog tests, additive phase | `tests/Hukbo.Client.Tests/UiThemeCatalogTests.cs` | Add coverage for both new maps and the three rejection cases. The existing single-font assertion remains valid during this phase. | New tests pass; no existing test weakened. | T11 | GATE |
| T13 | Whole-pixel geometry and scale-free primitives | `src/Hukbo.Client/UI/UiTextGeometry.cs` (new), `src/Hukbo.Client/UI/UiButton.cs`, `tests/Hukbo.Client.Tests/UiTextGeometryTests.cs` (new) | Add the geometry helper. Add a top-left draw primitive and a scale-free centred primitive; keep the scaled overload temporarily. | Geometry tests prove integral output for fractional input and for both odd and even measured widths. | T09 | GATE |
| T14 | Migrate status bar and control bar | `ArenaGame.Rendering.cs`, `UI/ControlBar.cs`, `UI/UiButton.cs` | Button draw takes a caller-chosen font and no scale. Status line and shortcut hint move to their rungs. The user interface layer switches to `SamplerState.LinearClamp`. | Compiles; no direct `DrawString` remains in these files. | T13 | GATE and MANUAL |
| T15 | Migrate the agent inspector | `UI/AgentInspectorPanel.cs`, `UI/AgentInspectorContent.cs` | Delete the local detail scale. Title to `Title`, rows to `Body`. The wrap measurement delegate drops its multiplier. Raise the line height to clear the new bake. Rewrite the stale comment naming Arial 18 and the 0.64 scale. | Compiles; the derived inspector height recomputes without a manual edit. | T13 | GATE and MANUAL |
| T16 | Harden the evidence-wrap budget | `tests/Hukbo.Client.Tests/AgentInspectorContentTests.cs` | Convert the reserved-line tests from a single hardcoded six-pixel-per-character measurement to a theory across five, six, seven, and eight. | The theory passes at every width, or the reserved line count is raised until it does. | T15 | GATE and MANUAL |
| T17 | Migrate the battle event log | `UI/BattleEventLogPanel.cs`, `.List.cs`, `.Details.cs`, `.Filters.cs` | Rungs per the design mapping. Replace the three hardcoded `/ 7` divisors with the ramp's advance estimate. Recheck the detail line height and the chip offset against the new bakes. | Compiles; no literal `/ 7` remains; existing panel tests pass. | T13 | GATE and MANUAL |
| T18 | Migrate the sound log | `UI/SoundLogPanel.cs`, `UI/SoundLogPanel.Layout.cs` | Delete the four local scale constants. Rows to `Caption`, title to `Title`. Replace the hardcoded character width estimate with the ramp's. Grow the row, header, section, and path heights to clear the new bakes. | Compiles; existing sound log tests, which reference the constants symbolically, still pass. | T13 | GATE and MANUAL |
| T19 | Migrate the remaining panels | `UI/MatchSummaryPanel.cs`, `UI/ArmyCompositionPanel.Presentation.cs` | Rungs per the design mapping. | Compiles; no direct `DrawString` remains. | T13 | GATE and MANUAL |
| T20 | Migrate menu and selectors | `MenuOverlay.cs`, `UI/UiThemeSelector.cs`, `UI/GoreIntensitySelector.cs` | Replace the float scale record with a resolved role per slot read from the shared standards. The wordmark moves to `Display`. | Compiles; no float scale reference remains outside the theming folder. | T11, T13 | GATE and MANUAL |
| T21 | Delete the float-scale path | `Theming/*`, `Content/Themes/ui-theme-standards.json`, `Content/Content.mgcb`, `Content/Default.spritefont` (deleted), `ArenaGame.cs`, `UI/UiButton.cs` | Remove the scale record and its transfer object, the scale metric range, the single font identifier, the legacy allowed identifier, the legacy content block and descriptor, the legacy font field, and the scaled primitive overload. | Release build green under warnings-as-errors. Exactly two `DrawString` calls remain in the client, both inside the primitives. No text-scale identifier remains anywhere in `src/` or `tests/`. | T14–T20 | GATE |
| T22 | Update the pinned catalog tests | `tests/Hukbo.Client.Tests/UiThemeCatalogTests.cs` | Replace the single-font assertion with one that resolves every role to an allowed identifier. The minimum target size assertion is unaffected. | Suite green. | T21 | GATE |
| T23 | Retune vertical layout | `Content/Themes/ui-theme-standards.json`, `Theming/UiThemeCatalogFallback.cs`, `tests/Hukbo.Client.Tests/UiThemeCatalogTests.cs` if pinned values move | Adjust menu title, subtitle, and selector offsets, the menu panel height, the selector label, name, marker offsets and height, and the army composition row height. Mirror every change in the code fallback. | JSON and fallback agree, asserted by a test. The wordmark does not overlap the subtitle. | T20 | GATE for parity, MANUAL for the visual |
| T24 | Prove the em-dash defect is fixed | `docs/development/testing.md` | Add a smoke row for staging an army composition change. Additionally assert at the byte level that a compiled character map contains `U+2014`. | Row added as `PENDING`; byte assertion passes. | T06, T14 | GATE for the byte check, MANUAL for the row |
| T25 | Licensing and provenance | `docs/dependency-risk-register.md`, `docs/agents/11-content-asset-pipeline.md`, `src/Hukbo.Client/Hukbo.Client.csproj` | Rewrite the register row: the control becomes the two vendored OFL faces with provenance recorded in the content README, and the remaining action becomes none. Rewrite the agent document's verification, limitations, and next-action sections. Add a copy item so the license texts reach the package output. | Neither document mentions Arial or an unresolved font license. Both license texts appear in the packaged output. | T03, T21 | GATE plus a package check |
| T26 | Reconcile the five-themes plan — **complete, no further action** | `docs/archives/2026-07-26/2026-07-26-five-ui-themes-design.md`, reference-only; not to be edited again | Already done, before that document was archived: a dated amendment note was appended to it instead of editing the historical rationale. The document has since moved to `docs/archives/`, where it is deprecated and unmaintained, so no future implementer may edit it to satisfy this row. | Satisfied. The "Amendment — 2026-07-27, font and text quality change" section is present and dated in the archived document, and it records that typography remains shared across all five themes while the identity and count of the shared asset changed. Reopening this task is not possible without editing an archived file, which `docs/archives/README.md` and `CLAUDE.md` section 6 forbid. | T21 | review — verified by inspection of the archived document |
| T27 | Smoke checklist rows | `docs/development/testing.md` | Add a typography smoke subsection with the fourteen rows listed below, all `PENDING`. | Rows present, none flipped to `PASS` by this workstream. | T21 | MANUAL |
| T28 | Run the canonical gate and record | `docs/development/testing.md` | `./scripts/verify.ps1`. Record the exact five-stage output, the test counts, and the seed-1 hashes verbatim. | Both recorded hashes unchanged. Output pasted, not paraphrased. | T21–T27 | GATE |
| T29 | Display scaling — measure only | none; the diagnostic is reverted | Draw viewport width against client bounds width, launch at 100% and at 150% Windows scaling, record both integers each time, revert. | Four integers recorded. | T28 | MANUAL |
| T30 | Display scaling — act, gated on T29 | `src/Hukbo.Client/app.manifest` or `Program.cs`, `docs/development/testing.md` | Executed only if the measurement shows the process is DPI-unaware *and* a chosen remedy measures clean. A remedy that changes nothing is reverted. | Post-fix measurement recorded, or the task is closed as declined with the limitation documented. | T29 | MANUAL |
| T31 | Archive | move both plan documents to `docs/archives/` with the "Archived: reference only" banner | Per `CLAUDE.md` section 6. | Both files moved and bannered. | T30 | review |

## Execution order

```
T01 → T02                        design, then plan
T03 → T04 → T05 → T06            assets, git handling, coverage proof, content build
T07 → T08 → T09                  ramp, ramp tests, font set
T10 → T11 → T12                  theme schema, additive phase
T13                              geometry and scale-free primitives
T14, T15→T16, T17, T18, T19      panel migration — these five run in parallel
T20                              menu and selectors
T21 → T22 → T23                  delete the float path, fix pinned tests, retune layout
T24 → T25 → T26 → T27            defect proof, licensing, reconciliation, smoke rows
T28                              canonical gate, recorded verbatim
T29 → T30                        display scaling: measure, then act only if justified
T31                              archive
```

T14, T15, T17, T18, and T19 touch disjoint files and may run concurrently once
T13 lands. The serialisation points are T13, which every panel depends on; T11,
which T20 depends on; T21, which must follow every panel migration; T28, which
must follow every code and document change; and T29, which must follow T28 so
that a blurry scaled display is never mistaken for a failure of the ramp.

## Verification

### Covered by the canonical gate

| Claim | Proving stage |
| --- | --- |
| Every descriptor compiles and every font name resolves | Release build, which runs the content builder |
| Every vendored font survived checkout intact | Release build — a corrupt font fails the import loudly |
| No stale float scale survives | Release build under warnings-as-errors, once T21 removes the overload |
| The ramp is internally consistent and matches the content project | Ramp tests |
| The catalog validates the new schema and rejects malformed input | Catalog tests |
| The theme JSON and the code fallback agree | Parity test |
| Text positions round to whole pixels | Geometry tests |
| The evidence-wrap budget survives the metric change | Inspector content theory |
| Existing panel geometry is unbroken | Event log, sound log, army composition, menu focus, and column split tests |
| The simulation is untouched | The seed-1 workload reproduces both recorded hashes byte-identically |
| The em dash now has a glyph | Byte assertion on a compiled asset — scriptable, not visual |
| License texts reach the package | Package the client and inspect the output content folder |

### Visual only — smoke rows

Per `CLAUDE.md` section 6, only a human at an interactive Windows desktop may
flip one of these. Compilation, unit tests, and a window-opening probe do not.

1. Glyph crispness at the smallest rung — event log and sound log rows have
   solid stems and clean edges, with no grey mush and no ragged stair-stepping.
2. Glyph crispness at the largest rung — the wordmark is sharp at every edge
   with no fringing.
3. Wordmark hierarchy — the wordmark is unmistakably larger and heavier than
   the subtitle beneath it.
4. Header face renders as capitals — every panel header renders fully and
   unclipped inside its header strip.
5. Mixed-case strings stay on the body face — theme names, gore levels, the
   controls label, the winner line, the distribute action, and every inspector
   line render with real lowercase letters.
6. No vertical clipping — no descender is cut off in any panel at any rung.
7. No horizontal overflow — no label spills past its panel, button, chip, or
   column, and no ellipsis appears where text previously fit.
8. Row alignment — event log columns, sound log rows, and inspector rows sit on
   consistent baselines with no drift down the list.
9. Agent inspector evidence note — the longest evidence note wraps fully inside
   the panel with nothing cut off.
10. Em-dash regression — staging an army composition change renders the notice
    with a real em dash and does not crash.
11. Theme cycling — all five themes render text at the same sizes with correct
    contrast, and no theme reveals a clipped or misaligned label the others hide.
12. Window resize — resizing between small and maximised keeps text pixel size
    constant and re-lays out panels without clipping.
13. Subpixel blur is gone — panning, zooming, and pausing produce no shimmering
    or swimming text.
14. Display scaling — record the appearance at 100% and at 150% Windows
    scaling. Feeds T29.

## Risk register

| # | Item | Status | Detail and mitigation |
| --- | --- | --- | --- |
| R1 | Drawing the composition notice throws | verified | The compiled character map carries ninety-five ASCII characters and a null default; the em-dash byte sequence is absent. Fixed by T06's default character and dash region, proven by T24. |
| R2 | The simulation must not move | verified by design, confirmed by T28 | No file under `src/Hukbo.Core` appears anywhere in this plan. |
| R3 | Bebas Neue has no lowercase | assumed, closed by construction | T05 verifies coverage; the design routes only all-capitals literals to the two Bebas rungs. |
| R4 | Larger rungs overflow layouts tuned for smaller text | assumed, high likelihood | Concrete targets are the sound log row and header heights, the inspector line height, the event log detail line height, and the menu and selector offsets. Owned by T15, T17, T18, and T23. Rajdhani being condensed means the pressure is vertical, not horizontal. |
| R5 | Four hardcoded characters-per-pixel divisors clip wrongly | verified present | Three `/ 7` expressions in the event log and one width estimate constant in the sound log. Centralised into the ramp by T17 and T18. |
| R6 | The evidence reserved-line budget becomes wrong | verified untested at the real metric | The existing tests measure at a single hardcoded legacy width, so the suite would stay green while the live interface wraps to an extra line. T16 converts it to a range theory. The panel's bounds guard means the worst case is a dropped line, never an overflow. |
| R7 | Font name resolution relative to the descriptor directory | assumed | Falsified immediately by the T06 build if wrong; the fallback is an explicit relative path. |
| R8 | Line-ending normalisation corrupts a vendored font | assumed low | T04 adds an explicit binary attribute rather than relying on the heuristic. |
| R9 | A DPI-unaware window blurs text regardless | unknown | Explicitly not assumed either way. Measured by T29 before T30 acts. |
| R10 | Six atlases cost frame time or memory | assumed negligible | Ninety-seven characters at thirty-eight pixels or less, well inside the profile's texture limit. Deferred sorting already batches per texture. If the gate's allocation figures or a render workload disagree, merge rungs. |
| R11 | A magic offset in the event log chip is tuned to Arial metrics | verified present | Included in T17's retune. |
| R12 | Open Font License compliance | assumed satisfied | The license permits bundling and embedding; baking to a texture atlas is a permitted derivative. The reserved font name clause binds only modified-and-renamed derivatives, which these are not. T03 vendors both license texts and T25 ships them. |
| R13 | A future non-ASCII string reintroduces R1 | mitigated, not eliminated | An explicit default character converts a future crash into a visible question mark. A wrong glyph is a bug report; a thrown exception is a dead game. |
| R14 | Conflict with the shared-typography constraint recorded in the now-archived five-themes plan | verified, narrow | Typography stays shared across themes; only the asset count and identity change. Resolved by the dated amendment T26 appended to that design document before it was archived. The document is now reference-only and is not edited again. |

## Measured line spacing — use these, do not estimate

Measured on 2026-07-27 by baking the real descriptors through the same
`FontDescriptionImporter` and `FontDescriptionProcessor` pair that
`Content.mgcb` uses, and reading `SpriteFontContent.VerticalLineSpacing`
directly. These are substantially larger than the usual "pixel size times 1.2
to 1.3" rule of thumb, which underestimates every rung.

| Rung | Face and bake | Real vertical line spacing |
| --- | --- | --- |
| `Caption` | Rajdhani SemiBold 12 | 20 |
| `Body` | Rajdhani SemiBold 14 | 24 |
| `Label` | Rajdhani SemiBold 17 | 29 |
| `Subtitle` | Rajdhani SemiBold 20 | 34 |
| `Title` | Bebas Neue 22 | 35 |
| `Display` | Bebas Neue 38 | 61 |

`Subtitle` and `Display` were read directly out of the compiled `UiSubtitle.xnb`
and `UiDisplay.xnb` assets in a later pass. `Label` was measured the same way
during the vertical-constant audit follow-up (baking `UiLabel.spritefont`
through the same importer/processor pair). All six rungs are now measured; no
constant carrying any of them may still be set by estimate.

Note that every glyph's cropping height in these bakes equals the font's line
spacing, so `MeasureString` returns the full line spacing as its height for any
single-line string. Centring maths therefore works against the line box, not
against the visible ink.

### Known consequence — the menu wordmark overlaps the subtitle

With the wordmark at `Display` and the current theme offsets
(`menu.titleTopOffset` 42, `menu.subtitleTopOffset` 72), the rendered boxes
collide by **18 pixels**: the title box runs from y=12 to y=73, and the subtitle
box starts at y=55.

T23 must raise `menu.subtitleTopOffset` from 72 to at least 90 for a zero gap,
and realistically to 96–100 for a visible gap, then cascade `selectorTopOffset`
(94) and `panelHeight` (660) by the same delta. `MenuOverlay`'s
`CalculateGoreSelectorTopOffset` derives every downstream offset from
`selectorTopOffset` alone, so that single bump cascades correctly with no code
change.

**Any vertical layout constant that carries a rung must be at least that rung's
line spacing.** A smaller value clips descenders, and no automated test in this
repository catches it — the panel geometry tests assert row counts and bounds
containment, not glyph legibility. Constants that carry two different rungs must
use the larger of the two.

## Appendix — rung assignment for every draw site

The design document carries the mapping for the eight theme-catalog scale slots.
This is the mapping for every remaining draw site, including the six literal
multipliers that were never part of the theme catalog. "Today" is the effective
pixel size under the current `18 × scale` arrangement.

| File and line | Content | Today | Rung |
| --- | --- | --- | --- |
| `ArenaGame.Rendering.cs:306` | status line | 14.04 | `Label` |
| `ArenaGame.Rendering.cs:316` | shortcut hint line | 11.16 | `Body` |
| `UI/ControlBar.cs:74` | control bar button labels | 14.04 | `Label` |
| `UI/AgentInspectorPanel.cs:65` | `"AGENT INSPECTOR"` | 14.04 | `Title` |
| `UI/AgentInspectorPanel.cs:183` | detail and evidence rows | 11.52 | `Body` |
| `UI/ArmyCompositionPanel.Presentation.cs:117` | `"ARMY COMPOSITION"` | 18.00 | `Title` |
| `UI/ArmyCompositionPanel.Presentation.cs:159` | unassigned count | 18.00 | `Body` |
| `UI/ArmyCompositionPanel.Presentation.cs:217` | category label | 18.00 | `Label` |
| `UI/ArmyCompositionPanel.Presentation.cs:234` | stepper value | 18.00 | `Label` |
| `UI/ArmyCompositionPanel.Presentation.cs:278` | plus and minus glyphs | 18.00 | `Subtitle` |
| `UI/ArmyCompositionPanel.Presentation.cs:311` | action label | 18.00 | `Label` |
| `UI/BattleEventLogPanel.cs:353` | `"BATTLE EVENTS"` | 12.96 | `Title` |
| `UI/BattleEventLogPanel.cs:363` | count text | 10.80 | `Caption` |
| `UI/BattleEventLogPanel.cs:390` | latest chip | 9.36 | `Caption` |
| `UI/BattleEventLogPanel.cs:402`, `:434`, `:444` | chip default | 9.90 | `Caption` |
| `UI/BattleEventLogPanel.Filters.cs:70` | filter chip | 9.00 | `Caption` |
| `UI/BattleEventLogPanel.Filters.cs:122` | filter chip | 10.08 | `Caption` |
| `UI/BattleEventLogPanel.Details.cs:26` | empty-state prose | 9.72 | `Caption` |
| `UI/BattleEventLogPanel.Details.cs:57` | `"SELECTED EVENT"` | 9.54 | `Caption` |
| `UI/BattleEventLogPanel.Details.cs:91` first row | detail head | 10.98 | `Body` |
| `UI/BattleEventLogPanel.Details.cs:91` later rows | detail rows | 9.90 | `Caption` |
| `UI/BattleEventLogPanel.List.cs:105` | `"EVENT STREAM"` | 9.54 | `Caption` |
| `UI/BattleEventLogPanel.List.cs:119` | live and inspecting badges | 9.36 | `Caption` |
| `UI/BattleEventLogPanel.List.cs:212` | tick column | 9.00 | `Caption` |
| `UI/BattleEventLogPanel.List.cs:222` | actor column | 9.90 | `Caption` |
| `UI/BattleEventLogPanel.List.cs:234` | action column | 9.90 | `Caption` |
| `UI/BattleEventLogPanel.List.cs:261` | empty-state title | 10.44 | `Body` |
| `UI/BattleEventLogPanel.List.cs:268` | empty-state hint | 8.64 | `Caption` |
| `UI/MatchSummaryPanel.cs:75` | winner or draw line | 18.90 | `Subtitle` |
| `UI/MatchSummaryPanel.cs:84` | `"MATCH COMPLETE"` | 12.96 | `Title` |
| `UI/MatchSummaryPanel.cs:105` | summary buttons | 12.96 | `Label` |
| `UI/MatchSummaryPanel.cs:110` | summary detail lines | 13.68 | `Body` |
| `UI/SoundLogPanel.cs:101` | `"SOUND LOG"` | 11.16 | `Title` |
| `UI/SoundLogPanel.cs:112`, `:159`, `:227` | section headers | 9.36 | `Caption` |
| `UI/SoundLogPanel.cs:127` | mute and muted labels | 9.00 | `Caption` |
| `UI/SoundLogPanel.cs:143`, `:185`, `:194`, `:209`, `:244`, `:261` | rows and path tail | 8.64 | `Caption` |

Two assignments deserve a note. `ArmyCompositionPanel` currently draws its
category labels, stepper values, and action labels at the unscaled baked size of
eighteen pixels, which is why they drop to `Label` at seventeen rather than
rising. And `MenuOverlay.cs:250`, the wordmark, currently draws at exactly the
same size as body text; that is the single most visible typographic failure in
the product and is the reason the `Display` rung exists.

Only strings that are already all-capitals literals in the source may be routed
to `Title` or `Display`, because those rungs use Bebas Neue. Empirical
verification during T05 established that Bebas Neue's lowercase codepoints do
not fall back to a missing-glyph box — they rasterize to glyphs pixel-identical
to their uppercase counterparts. A mixed-case string routed to those rungs would
therefore be silently uppercased rather than visibly broken, which makes this a
correctness rule that no test will catch for you.

## Follow-ups, deliberately not done here

- Ban `SpriteBatch.DrawString` outside the primitives with an analyzer. This
  needs a new package reference and a lock file regeneration, which is a
  reviewed dependency change.
- Subset the vendored fonts to Latin. Rajdhani SemiBold ships full Devanagari
  coverage and is 381 kilobytes as a result. Only ASCII is baked, so this costs
  repository weight rather than runtime cost, and subsetting would add a
  tooling dependency.
- Per-theme typography. Explicitly out of scope. The constraint that typography
  is shared across all five themes rather than chosen per theme still holds in
  the user interface layer; it was first recorded by the five-themes design
  document, which is now archived and reference-only, and it is restated in the
  dated amendment appended to that document.
