# Font and Text Quality — Design

> **Archived: reference only.** This plan is complete and deprecated. Do not execute it, and do not treat its steps, versions, or tooling references as current. The live contract is `CLAUDE.md` plus the skills in `.claude/skills/`.

Status: design only. This document does not authorize implementation. The
ordered task list lives in `docs/archives/2026-07-27-font-text-quality.md`.

Scope: `src/Hukbo.Client` presentation only. `src/Hukbo.Core`,
`src/Hukbo.Headless`, `tests/Hukbo.Core.Tests`, and `scripts/` are untouched —
zero files in each. This is a hard acceptance criterion, not an aspiration.

## 1. The problem

Every string the spectator reads is drawn badly, for three independent reasons
that compound.

**The user interface layer samples glyphs with nearest-neighbour filtering.**
`ArenaGame.Rendering.cs:97` begins the user interface sprite batch with
`SamplerState.PointClamp`. MonoGame's own default for `SpriteBatch` is
`SamplerState.LinearClamp`; `PointClamp` exists so that pixel art can be
magnified without softening. A `SpriteFont` atlas is not pixel art. It is a
FreeType rasterization with antialiased edges, and drawing it through a
nearest-neighbour sampler at any scale other than exactly 1.0 discards the
intermediate coverage values that make a glyph edge look like an edge. The
result is the stair-stepping the spectator sees.

The arena layer at `ArenaGame.Rendering.cs:76` uses the same sampler, but it
draws only a one-by-one white texture stretched into rectangles, so the sampler
choice there has no observable effect. That line stays as it is.

**One font is baked at one size and then resampled to nine different sizes.**
`Content/Default.spritefont` describes Arial at `Size 18`. Nothing is ever drawn
at eighteen pixels except by coincidence. The theme catalog carries eight float
multipliers in `Content/Themes/ui-theme-standards.json:94-103`, ranging from
`0.56` to `1.15`, and individual panels add six more literal multipliers of
their own. The smallest text in the product is the sound log at
`18 × 0.48 = 8.64` effective pixels, reconstructed by resampling an eighteen
pixel bitmap down to less than half its baked size. No sampler state rescues
that. The information needed to draw a nine pixel letterform was never in the
atlas.

**Text lands on fractional pixel coordinates.** `UiPrimitives.DrawCenteredText`
at `UiButton.cs:167` centres text by computing `font.MeasureString(text) * scale`
and halving it. That produces a fractional offset for any string whose measured
width is odd, and a fractional offset means the glyph's texels no longer align
with screen pixels. Under linear filtering this reads as a soft, swimming blur;
under point filtering it reads as inconsistent stem weights between adjacent
letters.

There is a fourth problem that is aesthetic rather than technical. Arial is the
default system typeface of the platform the game happens to be developed on. It
was never chosen. It is not distributed with the game, so the glyphs the
spectator sees depend on which fonts their machine happens to have installed,
and `docs/agents/11-content-asset-pipeline.md:41-47` already records the
resulting licensing exposure as unresolved.

## 2. A latent crash, found while designing this change

`ArenaGame.cs:43-44` declares:

```csharp
private const string CompositionStagedNotice =
    "Army composition staged — takes effect on next Full Reset";
```

The dash in that string is `U+2014 EM DASH`. The notice is appended to the
status line at `ArenaGame.Rendering.cs:352`, and the status line is drawn every
frame at `ArenaGame.Rendering.cs:306`. The flag that enables it is set at
`ArenaGame.cs:461`, when a spectator applies an army composition change.

`Content/Default.spritefont:9-14` declares exactly one character region,
`U+0020` through `U+007E`, and declares no `DefaultCharacter`. Dumping the
compiled `Content/bin/DesktopGL/Content/Default.xnb` confirms this at the byte
level: the character map length prefix is `0x5F`, ninety-five characters, and
the trailing `DefaultCharacter` nullable byte is `0x00`. The UTF-8 sequence
`E2 80 94` appears nowhere in the asset.

When a `SpriteFont` is asked to draw a character it has no glyph for and has no
default character to substitute, MonoGame throws `ArgumentException`. Staging an
army composition change therefore kills the game. Every interactive row in
`docs/development/testing.md` is currently `PENDING`, which is consistent with
this never having been observed by a human.

The window title at `ArenaGame.cs:658-663` also contains em dashes, but that
string goes to `Window.Title` and is rendered by the operating system, not by a
`SpriteFont`. It is unaffected.

This design fixes the crash as a side effect of replacing the font assets, and
hardens against a recurrence by giving every new descriptor an explicit
`DefaultCharacter`. A future non-ASCII string will then render a visible
question mark, which is a bug report, rather than throwing, which is a dead
game.

## 3. Typeface selection

**Rajdhani SemiBold** carries body text, labels, and all numerals.
**Bebas Neue Regular** carries panel headers and the menu wordmark.

Both are licensed under the SIL Open Font License 1.1 and are vendored into the
repository from `github.com/google/fonts` at commit
`7ff85c87f93ea6cca5f41c69f2e4edcb90240f26`, with their license texts alongside.
This closes the open provenance row at `docs/dependency-risk-register.md:10`
rather than adding to it — the current dependency on a machine-installed Arial
is the unresolved item, and a vendored OFL face is exactly the "project-owned
font" that `docs/agents/11-content-asset-pipeline.md:47` asks for.

Rajdhani is a narrow, squared, technical face whose numerals are unusually
unambiguous at small sizes, which matters because most of Hukbo's smallest text
is statistics: hit points, tick numbers, kill counts, entity identifiers. Its
condensed proportions also loosen every horizontal budget in the existing
layout rather than tightening it.

Bebas Neue is capitals-only by construction. That is a constraint, and it is
also the reason it suits panel headers: every string routed to it is already an
uppercase literal in the source (`"BATTLE EVENTS"`, `"AGENT INSPECTOR"`,
`"SOUND LOG"`, `"MATCH COMPLETE"`, `"ARMY COMPOSITION"`). Section 6 records how
mixed-case strings are kept away from it.

### Why SemiBold rather than Regular

Rajdhani Regular has a nominal stem weight near `0.055 em`. At this design's
largest Rajdhani size, twenty pixels, that is roughly a `1.1` pixel stem; at the
smallest, twelve pixels, roughly `0.66` pixels. A `SpriteFont` bake has no
hinting, no grid fitting, and no subpixel rendering, so a sub-pixel stem does
not become a thin dark line — it becomes a uniform grey smear spread across two
rows of texels. That is precisely the washed-out mushiness this work exists to
remove.

SemiBold sits near `0.085 em`, giving a `1.0` to `1.7` pixel stem across the
same range: still visually light, because Rajdhani is a narrow technical face,
but with an actual dark core surviving antialiasing.

Because every Rajdhani size in the ramp is at or below twenty pixels, there is
no size at which Regular would be the better choice. Vendoring one weight
instead of two removes a binary, a license file, and an axis of future drift. If
a later pass shows twenty pixel SemiBold reading too heavy for running prose,
adding the Regular weight is a one-file change. It is not built for in advance.

## 4. The size ramp

Six named rungs. Every string is drawn at scale `1.0` against a bake at its own
pixel size. No float resampling survives this change.

| Rung | Baked pixels | Face | Asset identifier |
| --- | --- | --- | --- |
| `Caption` | 12 | Rajdhani SemiBold | `Fonts/UiCaption` |
| `Body` | 14 | Rajdhani SemiBold | `Fonts/UiBody` |
| `Label` | 17 | Rajdhani SemiBold | `Fonts/UiLabel` |
| `Subtitle` | 20 | Rajdhani SemiBold | `Fonts/UiSubtitle` |
| `Title` | 22 | Bebas Neue Regular | `Fonts/UiTitle` |
| `Display` | 38 | Bebas Neue Regular | `Fonts/UiDisplay` |

The smallest text in the product rises from an effective `8.64` pixels to `12`
pixels. The menu wordmark rises from `18` pixels — the same size as body text,
which is the single most visible typographic failure in the product today — to
`38` pixels in a display face.

### Mapping the existing scale slots

Current effective size is `18 × scale`.

| Theme slot | Scale | Today | Rung | New |
| --- | --- | --- | --- | --- |
| `menuTitle` | 1.00 | 18.00 | `Display` | 38 |
| `menuSubtitle` | 1.00 | 18.00 | `Subtitle` | 20 |
| `menuButton` | 0.78 | 14.04 | `Label` | 17 |
| `menuHelper` | 0.58 | 10.44 | `Caption` | 12 |
| `selectorArrow` | 1.15 | 20.70 | `Subtitle` | 20 |
| `selectorLabel` | 0.58 | 10.44 | `Caption` | 12 |
| `selectorName` | 0.82 | 14.76 | `Label` | 17 |
| `selectorMarker` | 0.56 | 10.08 | `Caption` | 12 |

The plan document carries the equivalent mapping for all thirty-six remaining
draw sites, including the six literal multipliers that were never part of the
theme catalog at all: `AgentInspectorPanel.cs:19` and `:72`,
`ArenaGame.Rendering.cs:313` and `:323`, `ControlBar.cs:74`,
`MatchSummaryPanel.cs:105`, and the four constants at `SoundLogPanel.cs:16-19`.

## 5. Architecture

The repository rule recorded in `.claude/skills/hukbo-client-ui` is that logic
lives in pure helpers and `Draw` methods only paint, so that Client tests never
construct `ArenaGame`, a `GraphicsDevice`, a `SpriteBatch`, or a window. A
`SpriteFont` cannot exist without a graphics device, so the ramp is split from
the fonts.

**`Theming/UiFontRamp.cs`** is pure and static, with no MonoGame type in any
signature. It owns the role enumeration, the asset identifier for each role, the
pixel size for each role, the role name parser used by the theme JSON, and a
conservative per-character advance estimate. Everything that decides anything
lives here, and everything here is unit-testable without a graphics device.

**`Theming/UiFontSet.cs`** is a thin holder. `Load` takes a
`Func<string, SpriteFont>` and calls it once per role, so `ArenaGame.LoadContent`
passes `Content.Load<SpriteFont>` and nothing else in the codebase knows that a
`ContentManager` exists. It stores a fixed-size array indexed by the enum, not a
dictionary, so there is no hash iteration order and no allocation on the draw
path.

**`UI/UiTextGeometry.cs`** is pure geometry: snap a position to whole pixels,
and derive a whole-pixel top-left corner from a measured size and a desired
centre. `Vector2` is a MonoGame type but a pure value struct with no device
dependency, which is consistent with the existing `PawnGeometry`,
`HitEffectGeometry`, and `BloodGeometry` test files.

Panels stop taking a `SpriteFont` and take a `UiFontSet`, asking for a role
rather than supplying a multiplier.

### Making the rounding impossible to forget

`UiPrimitives.DrawCenteredText` loses its `scale` parameter entirely, and a new
`UiPrimitives.DrawText` handles top-left draws. Both route through
`UiTextGeometry` and both call `DrawString` with `scale: 1f`. Every direct
`spriteBatch.DrawString` in panel code is removed; after the migration the only
two `DrawString` calls in `src/Hukbo.Client` are the two inside `UiPrimitives`.

Dropping the `scale` parameter is deliberate sequencing. With
`TreatWarningsAsErrors` enabled repository-wide, the compiler enumerates every
stale call site, so no call site can be silently missed.

A stronger guard — banning `SpriteBatch.DrawString` outright with
`Microsoft.CodeAnalysis.BannedApiAnalyzers` — would require a new package
reference and a `packages.lock.json` regeneration, which is a reviewed
dependency change. It is recorded as a follow-up, not done here.

## 6. Keeping mixed-case strings away from Bebas Neue

Bebas Neue has no lowercase letterforms. Every string routed to the `Title` or
`Display` rungs is an all-capitals literal in the source. Everything with real
lowercase content stays on Rajdhani: theme display names, gore intensity names,
`"Simulation controls"`, `"{Team} wins"`, `"Distribute Evenly"`, the shortcut
hint line, the status line, every agent inspector row, and every event actor and
action label.

Whether Bebas Neue's lowercase codepoints map to capital glyphs or to `.notdef`
is verified empirically before any migration work begins, by baking one
descriptor and dumping its character map. The result is recorded here rather
than assumed.

### Verified 2026-07-27

A single descriptor was baked directly through
`FontDescriptionImporter`/`FontDescriptionProcessor` (the same importer and
processor the real `Content.mgcb` build uses) against
`Content/Fonts/BebasNeue-Regular.ttf` at size 22, with the two character
regions this design specifies (`U+0020`-`U+007E` and `U+2013`-`U+2014`) and
`DefaultCharacter` set to `?`.

The compiled `SpriteFontContent` carries a `CharacterMap` of exactly 97
entries. All 95 ASCII codepoints from `U+0020` through `U+007E` are present —
zero are missing — and both `U+2013` (en dash) and `U+2014` (em dash) are
present. `DefaultCharacter` resolved to `'?'` (`U+003F`), non-null, matching
the descriptor.

Lowercase codepoints are not dropped and do not fall back to `.notdef` or to
the default character. Pixel comparison of the rasterized glyph regions for
five representative pairs — `A`/`a`, `B`/`b`, `M`/`m`, `S`/`s`, `Z`/`z` —
shows each lowercase glyph is byte-for-byte pixel-identical to its uppercase
counterpart (same glyph rectangle dimensions, same cropping metrics, same
rasterized pixels). Bebas Neue Regular maps every lowercase ASCII codepoint to
the same glyph outline as its uppercase form. A mixed-case string routed to
`Title` or `Display` would render in full-height capitals rather than
crashing or showing a placeholder box, but section 6's rule — route only
all-capitals literals to these two rungs — still stands, because a lowercase
letter silently rendering as a capital is a readability defect the ramp is
built to avoid, not a safety net to lean on.

This closes risk register item R3 as verified rather than assumed-by-design:
Bebas Neue does not lack lowercase glyphs; it duplicates the uppercase glyph
at every lowercase codepoint.

## 7. Display scaling is measured, not assumed

MonoGame DesktopGL's DPI awareness on Windows is unreliable. Upstream issues
7181, 7004, and 5784 report the `app.manifest` route failing, with no confirmed
fix in 3.8.5. If the process is DPI-unaware, Windows bitmap-stretches the
rendered surface to fill a scaled display, blurring every glyph *after*
everything in this design has already done its job correctly.

This is orthogonal to the rest of the work and is handled as a separate, gated
task: measure `GraphicsDevice.Viewport.Width` against `Window.ClientBounds.Width`
at 100% and at 150% Windows scaling, then act only if the measurement justifies
it, then measure again to confirm the remedy did anything. A manifest that does
nothing is reverted rather than left in place. If no remedy measures clean, the
limitation is documented honestly and the task is closed as declined.

The measurement runs *after* the canonical gate, specifically so that a blurry
scaled display is never mistaken for a failure of the size ramp.

### Closed, 2026-07-28 — declined before the 150% reading

The 100% reading was taken: `GraphicsDevice.Viewport.Width`/`Height` and
`Window.ClientBounds.Width`/`Height` both read `1280`×`720`, equal, as expected
at unscaled DPI. That reading alone cannot show whether the process is
DPI-unaware, because 100% scaling never exercises the bitmap-stretch path this
section is worried about. The user declined to change Windows display scaling
to 150% to take the second reading, having no use for the remedy this task
would gate. The task (T29 in the plan document) is closed as declined rather
than left `PENDING` indefinitely; the diagnostic added to capture the readings
was reverted, and no `app.manifest` or `Program.cs` change was made. This is a
known, documented limitation, not a silently dropped one.

## 8. Relationship to the archived five-themes plan

`docs/archives/2026-07-26-five-ui-themes-design.md` has been archived and is
reference-only, so nothing in it is an instruction to this work. It is described
here because the shared-typography constraint it recorded is a real property of
the user interface layer that this design had to keep intact, not because the
archived document governs anything. That document states at `:166-167` that
"version one keeps the existing packaged font and shared typography metrics",
and at `:174` that all themes "share the current layout, controls, and font
asset".

The conflict is narrower than it looks. This design does not introduce
per-theme fonts. All five themes continue to share one font set; that set simply
has six members instead of one, and typography remains shared rather than
per-theme, which is the constraint that section was protecting. What genuinely
changes is the identity of the packaged font and the singular word "asset".

The historical rationale was correct when it was written and was not rewritten.
A dated amendment note was appended to that document instead, before it was
archived, and it survives there as the "Amendment — 2026-07-27, font and text
quality change" section. That note is now historical record rather than an open
item: the archived document is frozen, and no further edit to it is expected or
permitted.

## 9. The nine questions from `SIMULATION-GAME-STANDARDS.md` §10

**1. User-visible outcome.** Every string is drawn from a font baked at the size
it is drawn, in a vendored typeface chosen for the product, with linear
filtering and whole-pixel positioning. The spectator sees crisp text instead of
resampled Arial, a real wordmark on the menu instead of body-sized text, and
panel headers in a face distinct from their contents. The smallest text in the
product grows by roughly forty percent. A crash reachable by staging an army
composition change is removed.

**2. Tick stage and state read or written.** None. This feature participates in
no tick stage. It reads nothing from `BattleSimulation` beyond the
completed-tick snapshot and agent views the user interface already reads, and
writes nothing back.

**3. Numeric units, bounds, and same-tick conflict rule.** Units are screen
pixels, integers throughout. The ramp is bounded to six values in the closed
range twelve to thirty-eight. Text positions are constrained to whole pixels by
`UiTextGeometry`. There is no same-tick conflict rule because there is no tick
participation; two panels drawing in the same frame cannot interact.

**4. Total ordering and random stream policy.** No random stream is consulted.
`UiFontSet` indexes a fixed array by enum value rather than a dictionary, so no
hash iteration order exists that could influence anything. Draw order is the
existing fixed sequence in `ArenaGame.Rendering.cs` and is unchanged.

**5. Cache source and invalidation.** Six `SpriteFont` objects are loaded once
in `LoadContent` and never invalidated. They are immutable content, not a cache.
No new cache is introduced. The existing formatted-row clip cache in
`BattleEventLogPanel.List.cs` is unchanged in lifetime and bound; only its width
divisor moves from a hardcoded literal to the ramp's advance estimate.

**6. Save, event, and version effect.** Presentation only. No snapshot field, no
battle event field, no state hash input, no event hash input, no preset version
bump. Client settings are untouched, because the ramp is not a user preference.
The theme catalog schema does change, but `schemaVersion` stays at `1` because
that document is shipped content validated at load with a code fallback, not a
persisted user artifact.

**7. Worst-case complexity and benchmark workload.** Draw path complexity is
unchanged: the same number of `DrawString` calls per frame, each with one added
rounding operation on two floats. Six atlases replace one, costing well under
two megabytes of video memory and at most six extra texture binds per frame in
the worst case where all six rungs appear on screen at once;
`SpriteSortMode.Deferred` already batches per texture. The relevant workload is
the 500-agent render scenario at 1080p, and the honest expectation is no
measurable change in ninety-fifth percentile frame time. The 200-agent seed-1
gate workload is headless and is used here solely to prove the simulation did
not move.

**8. Can a spectator discover this effect without reading source code?** Yes,
immediately and without instruction. This is the rare feature whose entire
effect is the thing the spectator is looking at. Larger, sharper, better-shaped
text is visible the moment the window opens; the wordmark is visible on the menu
before a battle starts; the header face is itself the hierarchy signal. The
removed crash is discoverable as "changing army composition no longer kills the
game". No reason code, inspector field, or event is required, because the change
*is* the presentation layer rather than a rule whose consequences need
explaining.

**9. Tests that fail before and pass after.** Ramp consistency, ramp-to-content
parity, strictly increasing pixel sizes, whole-pixel geometry, theme catalog
coverage of every role, rejection of an unknown role name, the evidence-wrap
budget proven across a range of per-character widths rather than at one legacy
point, and a byte-level assertion that the compiled character map contains
`U+2014`. Each is enumerated with its failure reason in the plan document.

## 10. Risks

The full register with verification status lives in the plan document. The four
that shape this design:

**Layouts are tuned for eight-to-fourteen pixel text and will overflow.** This
is expected, not hypothetical. Row heights in the sound log, the agent
inspector's line height, the event log's detail line height, and the menu and
selector vertical offsets in the theme JSON all need retuning against the new
bakes. Rajdhani being condensed means horizontal budgets get looser, so the
pressure is vertical.

**Four hardcoded characters-per-pixel divisors exist** — three `/ 7` expressions
in the event log and a `CharacterWidthEstimate = 6` in the sound log — all tuned
for Arial at a scale that will no longer exist. They are centralised into the
ramp.

**The agent inspector's evidence-wrap budget is untested at its real metric.**
Its tests measure with a hardcoded six-pixels-per-character function derived
from Arial 18 at scale 0.64, so the suite would stay green while the live
interface wraps to an extra line. The panel's bounds guard means the worst case
is a dropped line rather than an overflow, but the tests are converted to a
range theory so the budget is proven across the plausible Rajdhani 14 range.

**Display scaling may blur everything regardless.** Handled by measurement, as
described in section 7.
