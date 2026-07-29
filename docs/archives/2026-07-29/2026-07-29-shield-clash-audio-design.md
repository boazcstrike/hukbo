# Shield-clash audio — design

> **Archived: reference only.** This plan is finished and is kept so the
> decision can be traced to its reasoning. Do not execute it, do not treat its
> versions or file paths as current, and do not cite it as justification for a
> change. The live contract is `CLAUDE.md`, `SIMULATION-GAME-STANDARDS.md`, and
> `docs/development/testing.md`.


Date: 2026-07-30
Branch: `shield-clash-audio`
Task list: [2026-07-29-shield-clash-audio.md](2026-07-29-shield-clash-audio.md)

Revision 2. The owner's original choice of a single weapon-keyed slot rested on
a row count that was wrong; corrected, the two shapes cost 38 and 37 rendered
rows respectively, and the owner chose **four ordinary classless slots**. This
document is rewritten to that shape. It also carries two new findings that
change the panel decision, in section 5.

## Outcome

The result of this design, the commits that carried it, the canonical gate
output, and an explicit list of what remains unverified are recorded in the
`## Outcome` and `## What is not verified` sections of the task list,
[2026-07-29-shield-clash-audio.md](2026-07-29-shield-clash-audio.md).

## 1. What this fixes

`SoundCueMapper.Map` (`src/Hukbo.Client/Audio/SoundCueMapper.cs:24-31`) switches
on `BattleEventKind` alone. It never reads `BattleEvent.Resolution`. Searching
the whole audio layer for the word `Resolution` returns nothing at all.

The consequence is that a blow stopped dead by a shield plays exactly the same
sound as a blow that opened a man's neck. The simulation already knows the
difference — `AttackResolution.ShieldBlocked` has existed since the weapon-clash
work, and `BattleEvent` packs `Weapon`, `Shield`, and `Resolution` into
`_combatContext` on every attack event
(`src/Hukbo.Core/Simulation/BattleEvent.cs:136,153,172`). Five presentation
channels already branch on it: the event-log line, the clash cross, its colour,
its size, and the swing recoil. Audio is the one channel that does not, and it
is the channel a spectator receives without looking at anything.

This change adds four sound slots, one per attacking weapon. A `ShieldBlocked`
attack routes to the matching clash slot *instead of* the attacker's impact
slot: the weapon's flesh sound is replaced, not layered underneath.

```
GameSoundId.ClashShieldKampilan =  9  ->  "clash-shield-kampilan"
GameSoundId.ClashShieldWasay    = 10  ->  "clash-shield-wasay"
GameSoundId.ClashShieldKalis    = 11  ->  "clash-shield-kalis"
GameSoundId.ClashShieldItak     = 12  ->  "clash-shield-itak"
```

Files: `clash-shield-kampilan-01.wav` through `-04.wav`, and the same four
indices for the other three slots. Sixteen files. `SoundCatalog.AllSounds` goes
from nine entries to thirteen.

The precedent for resolution-aware presentation is already in the client:
`src/Hukbo.Client/Rendering/ClashEffectRenderer.cs:39` branches on
`effect.Resolution == AttackResolution.ShieldBlocked` to pick the clash cross's
colour. This change gives audio the same treatment.

## 2. Scope

`Hukbo.Core` is not opened. Everything the change needs already exists there:
`AttackResolution.Landed = 0` and `ShieldBlocked = 1`
(`src/Hukbo.Core/Combat/AttackResolution.cs:29,35`), and the `Weapon`, `Shield`,
and `Resolution` accessors on `BattleEvent`. There is no preset version bump, no
new golden expectation, no change to any hash, and no determinism risk. This is
a `Hukbo.Client` change plus content plus documentation.

## 3. Judgement call one — the variant key, now moot

**The question is closed by the four-slot shape: the key
`(GameSoundId, HitClass?)` stays exactly as it is, and nothing is rekeyed.** Four
classless slots ride the `GetSlotVariantPrefix` path that `death` already uses —
`SoundLibrary.BuildRawMatches` puts a classless slot under the key
`(sound, null)` with the prefix `<base>-`, and `FindNumberedMatches` then matches
`<base>-NN.wav` with exactly two digits, one-based and case-insensitive, which is
precisely `clash-shield-kampilan-01.wav`. No new keying mode is introduced, so
there is nothing to choose between.

For the record, had the one-slot shape survived, the argument would have been to
collapse to a single token-typed axis `(GameSoundId, string? VariantToken)`
rather than widen to two axes, because a widened key makes illegal combinations
representable and the panel's sub-rows label themselves through
`HitClassCatalog.GetToken`, so a kampilan row would have printed `skull`. That
reasoning is now unused and is recorded here only so it does not have to be
rediscovered if a future slot genuinely needs a second axis.

Untouched by this change, and it should stay that way:
`src/Hukbo.Client/Audio/ISoundPlayer.cs` and `SilentSoundPlayer`,
`MonoGameSoundPlayer`, `SoundLibrary`, `SoundVariantList`, `SoundVariantSelector`,
`HitClass.cs`, `HitClassCatalogTests.cs`, `SoundLogPanel.Layout.cs`'s
`BuildBindingRows`, and the `RecordingSoundPlayer` fake in `SoundDirectorTests`.
`SoundCatalog.IsHitLocationDriven` is **kept**, not deleted — it acquires a
second job in section 6.

`SoundCueBudget` sizes `_perSoundCounts` off `SoundCatalog.AllSounds.Count` and
absorbs four more slots with no edit. `SoundCueLog` absorbs them. Mute is
slot-agnostic. Each clash slot gets its own 16-cue-per-frame allowance and every
attack slot's demand falls, so no budget code changes.

## 4. Judgement call two — the fallback rule and the file names

### File names

`clash-shield-kampilan-01.wav` and its siblings. This matches the shipped
`attack-kampilan-skull-01.wav` convention, and it is safe under the historical
accuracy policy for the reason `SoundCatalog.cs:45-52` already records: these are
internal file-system keys, resolved by the loader and shown only in the sound
log's expected-files list, never as a player-facing label. The player-facing
labels are untouched and stay in the pair form the policy requires. The
`hukbo-sound-effects` skill says at lines 118-134 never to put a cultural
identification in "a prompt or a file name"; the file-name half of that sentence
is stale, contradicted by `GENERATED.md:15-27`, which documents the deliberate
rename of the attack slots from English descriptors to weapon identities. The
**prompt-text** half is binding and is honoured — none of the four generation
prompts in section 8 names a weapon, a shield, or a people.

### The fallback rule: no cross-weapon substitution, and now it is structural

A hit class with no take of its own falls back through
`HitClassCatalog.GetFallbackChain` and only then to the bare `<slot>.wav` single.
**There is no weapon analogue, and under the four-slot shape there cannot be
one:** each weapon is its own slot, and `SoundLibrary` never substitutes across
slots. A slot with no numbered take falls back to its own bare
`clash-shield-<weapon>.wav` single and, failing that, resolves `Missing`, is
logged `NO FILE` in the cue log, and is silent.

This is the behaviour the design wants, and the four-slot shape enforces it for
free rather than by rule. The hit-class chain is defensible because the classes
are acoustically graded toward ribcage as a neutral centre — that grading is why
ribcage is everyone's last stop. The four weapons have no such ordering. They are
deliberately distinct, and the recommended mix ordering — War Axe louder than
Great Blade louder than Thrusting Blade louder than Work Blade — is a legibility
ranking, not a similarity metric. Substituting the axe take for a missing
work-blade take would make the lightest, fastest, most frequent weapon in the
roster sound like the heaviest, which is worse than silence: a `Missing` cue is
visible as `NO FILE` in the cue log and `MISSING` on the slot's own panel row,
and a wrong-weapon substitution is invisible everywhere.

### No bare singles ship

The shipped set contains no `attack-kampilan.wav`, no `attack-wasay.wav`, and so
on — every attack slot ships class variants only, and the bare single exists in
the resolver purely as a documented last resort. The four clash slots follow the
same convention: four numbered takes each, no bare single. One consequence is
inherited: `./scripts/sfx.ps1 -List` probes the bare single
(`scripts/sfx.ps1:534`) and will report all four clash slots as MISSING even when
all sixteen takes are present, exactly as it already does for the four attack
slots.

## 5. Judgement call three — the panel, re-derived, with two findings that change the answer

### Rendered rows: 37, and the overflow line reads `+28 more`

`BuildBindingRows` (`src/Hukbo.Client/UI/SoundLogPanel.Layout.cs:190-215`) emits
one header row per slot plus one indented sub-row per hit class, and only the
four attack slots are hit-location driven. So:

```
 4 attack slots     x (1 header + 6 hit-class rows) = 28
 9 classless slots  x (1 header + 0)                =  9
                                              total = 37
```

Today the figure is `4 x 7 + 5 = 33`. Both are confirmed against what the panel
already prints. When the list overflows, `SoundLogPanel.cs:169-207` draws
`visibleRowCount - 1` rows and spends the last row on
`+{rows.Count - drawnRowCount} more (enlarge the panel)`. Today the panel shows
nine rows, draws eight, and prints **`+25 more`** — and `33 - 8 = 25`, matching
the live panel exactly. **The count is rows minus (visible minus one), not rows
minus visible**, because the last visible row is spent on the overflow line
itself. At the recommended height below, ten rows are visible, nine are drawn,
and the panel prints `+28 more`.

### Finding one: the binding list is capped at slot count, and no height ever showed a clash slot

This section describes the defect as it stands on `main`. **The owner has ruled
that this change fixes it**, by making the bindings list scrollable; the fix is
specified immediately below. The defect is documented first because the fix only
makes sense against it.


```csharp
var desiredBindingsHeight =
    SectionHeaderHeight + (SoundCatalog.AllSounds.Count * BindingRowHeight);
var bindingsHeight = Math.Min(
    desiredBindingsHeight,
    Math.Max(0, available - Gap - reservedCueHeight));
```

`bindingsHeight` can never exceed `desiredBindingsHeight`, so
`GetVisibleBindingRowCount` can never exceed `AllSounds.Count` — thirteen — no
matter how tall the window is. `SoundLogPanel.Draw` iterates the row list from
index 0 with no scroll offset; only the cue list has a scrollbar. Therefore
**rows 13 through 36 are unreachable at any window size, on any monitor.**

The four clash slots land at row indices 33, 34, 35, and 36, because `AllSounds`
order puts them after the nine existing slots. The thirteen rows a spectator
could see are the kampilan slot with all six of its class rows, the wasay slot
header, four of wasay's class rows, and the overflow line. **Before the fix, no
clash slot is visible at the default size and enlarging the window does not
reveal one.** This is not a new defect: the same cap has hidden twenty-four rows
since the hit-class variants landed, and the words "enlarge the panel" in the
overflow text have been misleading for exactly that long. Reordering `AllSounds`
to put the clash slots first was considered and rejected by the owner — it would
merely trade the clash rows for kampilan's class rows, which is the same defect
pointed at a different victim.

`PENDING-SOUNDS.md:33-35` says a tenth slot "overflows the panel and
**silently** hides itself". That was measuring `desiredBindingsHeight` rather
than what the panel draws. The overflow has never been silent — the panel prints
a count — and it has never been fixable by resizing.

### The fix for finding one: the bindings list scrolls

Approved scope growth, ruled by the owner after the defect was flagged. Two
scroll precedents already exist in this codebase and a third pattern must not be
invented.

**Precedent A — scroll state in the model.** `BattleEventLogPanel` keeps no
offset of its own; `BattleEventFeed.Scroll(rowDelta, visibleRowCount)` and
`GetScrollStart(visibleRowCount)` own it
(`src/Hukbo.Client/Presentation/BattleEventFeed.cs:124,143`), and the panel calls
them from `HandleWheelScroll` and `PageFromScrollbar`
(`src/Hukbo.Client/UI/BattleEventLogPanel.cs:96,248-289`). The sound log's **cue**
list already follows this precedent through `SoundCueLog.Scroll` and
`GetScrollStart` (`src/Hukbo.Client/Audio/SoundCueLog.cs:64-96`).

**Precedent B — scroll state in the panel, clamped against a model it does not
own.** `BattleReportPanel` holds a private `_scrollStart`, calls
`ClampScroll(totalRowCount, visibleRowCount)` from both `Update` and `Draw`
(`:60,113`), pages from the scrollbar on a click, and reuses
`BattleEventLogPanel.GetScrollbarThumb` for the thumb
(`src/Hukbo.Client/UI/BattleReportPanel.cs:60,71-91,113,135-162`).

**The bindings list follows precedent B.** Precedent A requires a mutable model
object to host the offset, and the bindings list has none: the rows are produced
by the pure static `BuildBindingRows` from `director.Player.Bindings`, which is an
`IReadOnlyList<SoundBinding>` owned by `ISoundPlayer`. Pushing view state into
`ISoundPlayer` would put a scroll offset in the audio layer, and it would force
edits to `MonoGameSoundPlayer` and `SilentSoundPlayer`, which nothing else in
this change touches. `BattleReport` is immutable in exactly the same way, which
is why `BattleReportPanel` owns its offset — the same reasoning applies here.
`SoundLogPanel` is already stateful (`_pointerPosition`, `Bounds`), so a private
`_bindingScrollStart` field adds no new kind of state. `ArenaGame` is not
involved and is not opened.

The specifics the implementer must honour:

- **A second scrollbar region.** `SoundLogPanelLayout`
  (`src/Hukbo.Client/UI/SoundLogPanelTypes.cs:10-18`) gains
  `BindingScrollbarTrackBounds`, **appended as the last positional member** so no
  existing argument position moves. The existing `ScrollbarTrackBounds` belongs to
  the cue list, keeps its name, and must keep working. `BindingRowsBounds` narrows
  by `ScrollbarWidth + 4`, mirroring how `CueRowsBounds` is already narrowed at
  `Layout.cs:141-150`, and the new track sits at the right edge of the bindings
  area on the same geometry. Narrowing costs each binding label about twelve
  pixels of text room through `GetMaximumCharacters`; that is the same trade the
  cue list already makes. `SoundLogPanelTests.Regions` at `:348-358` enumerates
  every region for the containment test and **must yield the new one**.
- **The wheel is scoped to the region under the pointer.** Today
  `SoundLogPanel.Update:46-50` applies `GetScrollRowDelta` to the cue log whenever
  the pointer is anywhere inside the panel, including over the bindings area and
  the header — a latent defect this change has to correct anyway. The new rule,
  the **innermost-scrollable-region rule**, is that the wheel scrolls the list
  under the pointer: over `BindingsBounds` it scrolls the bindings, over
  `CueListBounds` it scrolls the cues, and over the header, path, or mute button
  it keeps today's behaviour and scrolls the cues, so the wheel is never swallowed
  and there is no dead zone. This refines the `hukbo-client-ui` skill's rule "the
  wheel over a panel scrolls only that panel" one level inward; it does not
  conflict with it, because the wheel is still consumed by the sound log and still
  must not reach the camera. The routing decision is a pure helper —
  `GetWheelTarget(SoundLogPanelLayout layout, Point pointer)` returning a
  `SoundLogScrollTarget` — so it is testable with no input plumbing and no
  `GraphicsDevice`.
- **Clamping is a pure helper too.** `ClampBindingScroll(int scrollStart, int
  totalRowCount, int visibleRowCount)` in `SoundLogPanel.Layout.cs`, returning a
  value in `[0, max(0, totalRowCount - visibleRowCount)]` and therefore **zero
  whenever every row fits**, so a short list cannot scroll at all. It is called
  from both `Update` and `Draw`, as `BattleReportPanel.ClampScroll` is, because
  the viewport height can change between the two.
- **The overflow line is removed.** `SoundLogPanel.cs:169-170,201-207` currently
  spends the last visible row on `+{N} more (enlarge the panel)`. With a
  scrollable list that message is wrong twice over — enlarging is not the remedy,
  and it never was. Delete it and draw real content in all `visibleRowCount` rows,
  with the scrollbar thumb as the affordance. This matches the cue list in the
  same panel exactly, which draws no overflow line and relies on its thumb
  (`:259-275`), and it recovers one row of content. Nothing is lost: the header
  already reports `MISSING n/13`.
- **The `bindingsHeight` cap at `desiredBindingsHeight` stays**
  (`Layout.cs:110-117`). Stated explicitly rather than left implicit: it caps the
  **viewport**, not the list, which is exactly right for a scrollable list, and
  removing it would let the bindings section grow on a tall window until only the
  reserved three cue rows were left. Thirteen rows of viewport, thirty-seven rows
  reachable.
- **Scrollbar click-paging is not added.** The cue list has none either — its
  scrollbar is a drawn indicator and the wheel is the only input. Matching that
  keeps the two lists consistent; adding paging to both is a separate change.

### Finding two: what 480 pixels actually costs the battle event log

Tracing `BattleEventLogPanel.CalculateLayout` for a 420-wide panel of height `H`.
Constants: `Padding = 10`, `Gap = 6`, `HeaderHeight = 35`, `FilterRowHeight = 26`,
`ListHeaderHeight = 25`, `RowHeight = 30`, `DetailsHeaderHeight = 26`,
`DetailLineHeight = 24`, `DetailLineCount = 6`, `DetailsBottomPadding = 6`,
`MinimumDetailsHeight = 26 + 6*24 + 6 = 176`, `MaximumDetailsHeight = 188`.

The header and both filter rows are fixed height and fit at every height in
range, so `contentTop` is 115 in every case below and
`availableContentHeight = (H - 10) - 115`. Then
`detailsHeight = min(188, max(min(176, available), available / 3))`, and because
`detailsTop` lands on `contentTop` whenever the details pane wants everything
that is left, **the event list's row area is zero at every one of these
heights** — the details pane consumes the whole content area and the list is
squeezed to nothing.

| Sound log | Event log | Content area | Details pane | Detail lines of 6 | Event rows |
| ---: | ---: | ---: | ---: | ---: | ---: |
| hidden (default) | 640 | 515 | 176 | **6** | **10** |
| 396 (62%, today) | 234 | 109 | 109 | 3 | 0 |
| 416 (65%) | 214 | 89 | 89 | **2** | 0 |
| 422 (66%) | 208 | 83 | 83 | 2 | 0 |
| 448 (70%) | 182 | 57 | 57 | 1 | 0 |
| 473 (74%) | 157 | 32 | 32 | 0, header intact | 0 |
| 480 (75%) | 150 | 25 | 25 | **0, header clipped by 1px** | 0 |

Two things follow. The research finding inherited from revision 1 — "the event
list's row area is already zero today" — is **confirmed**, and so is "the real
cost is detail lines, not event rows". But the previous design's bill was one
detail line; at 480 the bill is the **entire selected-event detail pane**,
including its own "SELECTED EVENT" header row, which is clipped by one pixel. The
pane stops rendering anything a spectator can read.

Note also that `_isSoundLogVisible` defaults to `false`
(`src/Hukbo.Client/ArenaGame.cs:124`), so the event log owns the full 640 pixels
and shows ten event rows and all six detail lines until the spectator opens the
sound log. Everything in the table above is the cost of having the sound log
open.

### The 13-row requirement, re-derived

For `SoundLogPanel.CalculateLayout` at width 420 and height `H`:

```
verticalPadding = 10                 (H/8 exceeds 10 for any H >= 80)
inner.Bottom    = H - 10
header.Bottom   = 10 + 62            =  72
path.Bottom     = 74 + 20            =  94
contentTop      = 94 + Gap           = 100
available       = (H - 10) - 100     = H - 110
reservedCue     = 20 + 3*20          =  80
bindingsHeight  = min(20 + N*20, H - 196)
visibleRows     = (bindingsHeight - 20) / 20
```

Thirteen rows want `bindingsHeight >= 280`, so `H >= 476`. **The coordinator's
`H >= 476` is correct, and 480 is not the exact fit.** `640 * 74 / 100 = 473`
falls three pixels short and yields twelve rows; `640 * 75 / 100 = 480` clears
476 with four pixels of slack. Unlike the previous design there is no exactly
fitting percentage for thirteen rows — 75 is simply the smallest integer percent
that reaches 476, and it overshoots by four pixels that cannot form a fourth cue
row (the cue list gets 84 pixels at 480 and 80 at 476; both yield three rows).

### Decision: 65 percent, not 75, and retarget the layout test

**Recommendation: `SoundLogHeightPercent` becomes 65, giving a 416-pixel sound
log and a 214-pixel event log. 75 is the wrong call and I would not ship it.**

The only thing 75 buys over 65 is that it satisfies
`SoundLogPanelTests.CalculateLayout_ShowsEveryExpectedFileNameAtTheDefaultSize`,
which asserts `GetVisibleBindingRowCount(layout) >= SoundCatalog.AllSounds.Count`
without editing that test. Weighed against finding one, that is not worth having:

- **The test measures the viewport, not what a spectator can reach.** Its comment
  says "the panel is the documentation of what to name a file, so at the layout
  the client actually uses it must be able to list every slot." It does not list
  every slot at 480 and it never has: thirteen visible rows are kampilan's entire
  block plus most of wasay's. The test compares a slot count against a row list it
  does not describe. Once the list scrolls, reachability is the property that
  matters and the viewport height stops being the thing to defend, which removes
  the last reason to buy rows with event-log pixels.
- **The rows 75 buys are the wrong rows, and scrolling makes them free.** Going
  from ten visible rows to thirteen reveals wasay's `gut`, `limb`, and
  `extremity` class rows — which a spectator can now reach at any viewport size by
  scrolling. Paying eighty pixels of the selected-event pane for three rows that
  are one wheel detent away is a bad trade.
- **The bill at 75 is qualitatively different from the one already accepted.**
  Under the one-slot design the owner accepted losing one detail line of six. At
  480 the loss is all six lines and the pane header. The selected-event pane is
  the spectator's only route to an event's full evidence; the sound log is a
  diagnostic panel that is closed by default. Trading the primary readout for a
  diagnostic list is the wrong direction.
- **65 reproduces exactly the bill already accepted, and is an exact fit.** Three
  detail lines become two, no event rows are lost because there are none at either
  height, and the binding list grows from nine visible rows to ten. Ten rows need
  `H >= 416`, and `640 * 65 / 100 = 416` exactly under integer division, with zero
  slack — the same smallest-sufficient reasoning method the owner's decision 5
  used, landing on the same arithmetic property.

Final figures:

```
SoundLogHeightPercent  62 -> 65
sound log height      396 -> 416      (640 * 65 / 100 = 416 exactly)
event log height      234 -> 214
visible binding rows    9 -> 10
rendered rows          33 -> 37
overflow text     +25 more -> +28 more
detail lines of 6       3 -> 2
```

`SoundLogMinimumHeight = 236` (`ArenaGame.cs:63`) needs no change: it reserves a
single binding row and is independent of slot count.
`RightColumnSplitTests.cs` passes its own literals
(`soundLogMinimumHeight: 168, soundLogHeightPercent: 45`) and never reads the
`ArenaGame` constants, so every method there passes unchanged. Every other
`SoundLogPanelTests` method uses `PanelBounds = (900, 400, 420, 300)`, where the
cue-row cap dominates, and passes unchanged.

`CalculateLayout_ShowsEveryExpectedFileNameAtTheDefaultSize` is retargeted rather
than satisfied. It becomes two honest tests: a two-sided exact-fit test pinning
nine visible rows at height 415 and ten at 416, which is the same both-sides
technique the coordinator asked for, retargeted; and a new
`CalculateLayout_NeverShowsMoreRowsThanTheSlotCountRegardlessOfHeight` asserting
that at height 2000 the visible count is still `AllSounds.Count`, so that finding
one is pinned in the suite and nobody again believes a taller window reveals a
hidden slot.

**If the owner overrules and wants the existing test satisfied**, exactly three
things change and nothing else in this plan moves: `SoundLogHeightPercent`
becomes 75 instead of 65; the two-sided test targets 475 and 476 instead of 415
and 416; and smoke row 176 must say that the selected-event detail pane renders
nothing at all while the sound log is open. That is a deliberate, recorded trade
rather than a silent one.

## 6. The `SoundDirector` trap, and the mandatory fix

`SoundDirector.Ingest` (`src/Hukbo.Client/Audio/SoundDirector.cs:129-131`)
derives a hit class from `battleEvent.HitLocation` for **every** attack event:

```csharp
var hitClass = battleEvent.HitLocation is { } bodyPart
    ? HitClassCatalog.FromBodyPart(bodyPart)
    : (HitClass?)null;
```

A shield-blocked attack still carries a hit location. `BattleEvent.Attack`
(`src/Hukbo.Core/Simulation/BattleEvent.cs:223-234`) requires `hitLocation`
unconditionally, regardless of resolution. So an implementation that only taught
`SoundCueMapper` to return `ClashShieldKampilan` would have the director look the
slot up as `(ClashShieldKampilan, HitClass.Skull)`.
`MonoGameSoundPlayer.GetStatus` (`:78-81`) returns `Missing` on a key miss, and a
classless slot is registered only under `(sound, null)`. The result is a cue that
resolves `Missing` **forever**: no crash, no exception, no failing test, and —
because `SoundBinding.Status` for the slot would still read `Ready`, the four
files having been found — no complaint in the panel either. The only trace would
be a `NO FILE` row in the cue log that somebody happened to be watching.

`PENDING-SOUNDS.md:96-99` warns about this from the opposite direction: it tells
a future author that a classless slot must pass `null`. Nothing in the code
enforces it.

**The fix derives the hit class from the mapped slot rather than from the
event**, using machinery that already exists:

```csharp
if (SoundCueMapper.Map(battleEvent) is { } sound)
{
    var hitClass = SoundCatalog.IsHitLocationDriven(sound)
        ? HitClassCatalog.FromBodyPart(battleEvent.HitLocation!.Value)
        : (HitClass?)null;
    Resolve(sound, hitClass, battleEvent.Tick, battleEvent.SourceEntityId);
}
```

This is why `IsHitLocationDriven` is kept rather than deleted. It already answers
exactly the right question — the four attack slots are hit-location driven and
nothing else is — and it now answers it for the director as well as for the
loader, which puts the director and `SoundLibrary` on the same predicate. A slot
whose files are registered under `(sound, null)` is now looked up under
`(sound, null)` by construction, because one function decides both.

The director is a **mandatory** edit site with its own test. The regression is
silent by nature, so the test has to be explicit: a `ShieldBlocked` attack event
carrying a real, non-null `HitLocation` must reach the player with a `null` hit
class. Without that test, a future change that restores the event-derived
derivation reintroduces a permanently silent cue and nothing goes red.

Two further sites, both of which the four-slot shape happens to handle for free:

- `SoundCueFormatter.Format` calls `SoundCatalog.GetBaseName`, which **throws**
  `ArgumentOutOfRangeException` for an unlisted slot (`SoundCatalog.cs:65-69`).
  Omitting any of the four `GetBaseName` arms crashes the cue-log renderer, not
  merely the loader.
  `SoundCatalogTests.GetFileName_IsUniqueLowercaseKebabWavForEverySlot`
  enumerates the enum and catches it.
- `MonoGameSoundPlayer.HasAnyReadyVariant` (`:190-210`) already handles a
  classless slot correctly through its `!IsHitLocationDriven` branch and needs no
  change. Under the one-slot shape it would have wrongly downgraded the slot to
  `LoadFailed` at `:181-184`.

The debug-log payload is unchanged. `SoundDirector.Record` keeps writing
`"hitClass"`, and it will write `null` for a clash cue, which is now the correct
and informative value. No new `LogEvents` constant is needed. The
`assets.sound.scanned` line that `LogBindings` writes when real content replaces
the silent player will report thirteen slots. With the bindings list now
scrollable, the panel is the primary route for confirming that the four clash
slots resolved, and that log line is a useful secondary confirmation rather than
the only one.

## 7. Historical accuracy

Every claim below carries a tier per `CLAUDE.md` section 7 and
`docs/research/HISTORICAL_1500s_WEAPONS.md`.

### The shield channel

- No sixteenth-century source describing Philippine combat describes a
  blade-on-blade parry (`docs/research/WEAPON_CLASH_1500s.md:22-23`).
  **Documented**, as a corpus absence.
- The shield is the only defensive channel with sixteenth-century documentary
  support for its presence and use (`WEAPON_CLASH_1500s.md:34-35`).
  **Documented.** The *rate* at which shields intercept blows is explicitly
  disclaimed by the same source and is **Provisional reconstruction**; so are the
  sixteen weapon-intercept cells, which `WEAPON_CLASH_1500s.md:411-414` records as
  carrying "no evidentiary confidence". Nothing in this change makes any claim
  about how often a shield blocks.
- That a shield should reroute the defence to a different channel, producing a
  visible, spectator-discoverable difference (`WEAPON_CLASH_1500s.md:211-216`),
  is **Provisional reconstruction** — a design principle, not an attested fact.

### What the shield is made of — this corrects a shipped document

`src/Hukbo.Client/Content/Audio/PENDING-SOUNDS.md:56` describes the proposed
clash-shield sound as "Wood and hide, not metal." **Hide is wrong and is
corrected by this change.** Hide appears nowhere in the research for shields; it
appears only as carabao-hide *armor* (`WEAPON_CLASH_1500s.md:69`,
`HISTORICAL_1500s_WEAPONS.md:61-63`).

- Pigafetta, Mactan 1521: the shields were of **thin wood**. "Hardwood" is
  explicitly rejected as fact (`docs/research/movement/tall-hardwood-shield.md:94`,
  THS-01). **Documented.**
- Boxer Codex Cagayan plate, c. 1590–95: a tall curved silhouette, and nothing
  more (`tall-hardwood-shield.md:96`, THS-03). **Documented, form uncertain.**
- Junker 1999: rattan-strengthened and resin-coated
  (`WEAPON_CLASH_1500s.md:145-146`). **Documented, form uncertain.**
- "Roughly 50 by 150 cm in light fibrous wood" (`WEAPON_CLASH_1500s.md:148-153`)
  carries an explicit flag in its own source never to cite it as a measurement,
  at Low confidence. **Provisional reconstruction.**

The acoustic target that follows from the evidence is therefore: **a light
fibrous plank, bound with rattan and coated in resin. No boss, no metal facing,
no ring — in any of the four slots.** That much is grounded.

Two naming rules bind and are honoured. `TallHardwood` is a stable code
identity, not a historical conclusion (`tall-hardwood-shield.md:52-53,64-67`); no
document, prompt, or file name produced by this change states that the shield was
tall or hardwood. And the name `Kalasag` is expressly withheld pending vocabulary
verification, pinned by
`ShieldVisualCatalogTests.NoShieldEntryLabel_ContainsTheUnverifiedKalasagName`; it
appears in no label, no file name, and no prompt here. The shield's player-facing
label stays the plain descriptor `Tall Hardwood`
(`src/Hukbo.Client/UI/AgentInspectorContent.cs:788-797`), because unlike the four
weapons there is no verified Filipino name to put in the pair form.

### The four weapons

From `src/Hukbo.Core/Combat/CombatIdentity.cs`: Kampilan, a large cutting sword
(`:17-19`), **Documented, form uncertain**. Wasay, a hafted battle axe with a
broad metal head (`:24-27`), **Documented, form uncertain**. Kalis, recorded as
`calis` in 1521 and the best attested of the four (`:32-35`), **Documented**.
Itak, a Tagalog field and utility blade (`:38-43`), **Provisional
reconstruction**. Metallurgy is the weakest link in the whole set:
`WEAPON_CLASH_1500s.md:172-180` records that no metallurgical study of a dated
sixteenth-century Philippine blade was located, so nothing about how these blades
sounded can be derived from evidence at all.

### The per-slot acoustic differences are a gameplay choice, not evidence

**Stated plainly: the four slots sound different from one another for gameplay
legibility, not because any source distinguishes them.** The evidence supports
one thing — the material substrate, at **Documented, form uncertain** — and it is
the same substrate for all four. Everything below is a **Provisional
reconstruction** in the service of letting a spectator hear which weapon struck,
derived from the preset's own tuning figures in
`PhilippineCombatPresetV3.cs:132-186` rather than from any source:

| Weapon | Grip | Damage | Reach | Cooldown | Acoustic intent |
| --- | --- | ---: | ---: | ---: | --- |
| Kampilan — Great Blade | Two-handed | 15 | 16 | 7 | Deepest board note: a hollow low-mid thock with a shallow woody bite in front of it, resonance dying immediately |
| Wasay — War Axe | Two-handed | 18 | 13 | 8 | Heaviest and bluntest: a low crack plus splitting fibres. The only one with an audible split. Shortest, densest, no ring |
| Kalis — Thrusting Blade | One-handed | 11/10 | 13/12 | 5 | Tightest and highest: a compact woody punch that skids, with a thin rattan-binding buzz. Least body |
| Itak — Work Blade | One-handed | 9/8 | 11/10 | 4 | Lightest, driest, shortest: a quick shallow clack, and the quietest of the four |

Mix ordering, loudest first: War Axe, Great Blade, Thrusting Blade, Work Blade.
The Work Blade must be quietest because its cooldown of 4 is the fastest in the
roster, so it fires most often; a loud take there would dominate the mix on
frequency alone.

One trap to avoid in the Thrusting Blade take.
`WEAPON_CLASH_1500s.md:471-475` records that a thrust is not meaningfully harder
for a *shield* to cover, because a shield defends an area while a blade defends a
line. So the kalis take must land on the board — a compact punch that skids — and
must not sound like a glancing near-miss.

## 8. Generation

`scripts/sfx.ps1` is an authoring tool a person runs on purpose. It is the only
script in the repository that talks to a network service, it reads
`ELEVENLABS_API_KEY` from the environment or the untracked `.env`, and nothing in
the game, the build, the tests, or the canonical gate calls it. The key never
goes into a tracked file, an echoed line, or a command line.

### The four-slot shape removes two of the three blockers

Verified against disk at these exact lines:

- `scripts/sfx.ps1:94` — `-Class` carries
  `[ValidateSet('skull','neck','ribcage','gut','limb','extremity')]`.
  **No longer a blocker.** Four classless slots need no `-Class` at all.
- `scripts/sfx.ps1:601-603` — the hard throw when `-Class` is supplied for a slot
  whose name does not start with `attack-`. **No longer a blocker**, for the same
  reason, and it stays exactly as written: it will correctly reject
  `-Slot clash-shield-wasay -Class skull`, which is a mistake worth rejecting.
- `scripts/sfx.ps1:97-99` — `-Index` carries `[ValidateRange(1, 99)]` and is `0`
  when unbound, and `Get-SlotPath` (`:249-272`) appends `-{0:D2}` only when the
  index is greater than zero. So `-Index 1` through `-Index 4` produce
  `clash-shield-wasay-01.wav` through `-04.wav` with no script change at all.
- `scripts/sfx.ps1:179-233` — `$defaultPrompts` has no entry for any of the four
  slots, so `-Slot clash-shield-wasay` without `-Prompt` throws at `:559-565` and
  `-List` prints `(no default prompt)`. **This is the one remaining blocker**, and
  it is the whole of the script change: four new entries.

`Get-CatalogSlot` (`:241`) scrapes slot names out of `SoundCatalog.cs` with the
regex `GameSoundId\.\w+\s*=>\s*"([a-z0-9-]+)"`, so the four new `GetBaseName`
arms are picked up with no script edit — and here that is exactly the wanted
behaviour, because the four arms genuinely are slots. Under the one-slot shape
this was a hazard to design around; under this shape it is free.

Verified for the no-key verification path: `-DryRun` returns at `:622-625`, which
is **before** `Get-ApiKey` at `:627`. Every script assertion in the task list can
therefore be run with no key present.

### The four prompts

Generate at `-Duration 0.5` with trimming on and `-PromptInfluence 0.5`, above
the 0.4 default because the "no metal" negative is the entire point of these
slots. The API floor is 0.5 seconds, so a combat hit is generated long and
trimmed back to its audible part. None of these names a weapon, a shield type, or
a people. There is now exactly one prompt per slot, which is the shape
`$defaultPrompts` already has, so all four become defaults and `-Prompt` need not
be passed at all.

```text
# clash-shield-kampilan  (Great Blade)
one heavy two-handed blade slamming flat into a large light wooden shield, deep
hollow board thud with a shallow woody bite in front of it, dry rattan-bound
plank, dry packed earth, open air, very short, no ring, no metal, no reverb, no
music, no voice

# clash-shield-wasay  (War Axe)
one heavy axe head crashing into a large light wooden shield, blunt low crack
with splitting wood fibres, dull dry plank break, dry packed earth, open air,
very short, no ring, no metal, no reverb, no music, no voice

# clash-shield-kalis  (Thrusting Blade)
one narrow blade point punching into a light wooden shield, tight woody punch
skidding off the board face with a thin rattan buzz, dry packed earth, open air,
very short, no ring, no metal, no reverb, no music, no voice

# clash-shield-itak  (Work Blade)
one short light blade tapping a large light wooden shield, quick shallow dry
woody clack on a thin plank, dry packed earth, open air, very short, no ring, no
metal, no reverb, no music, no voice
```

Four takes per slot, sixteen files. Research argues three to five takes per
repeating unit before repetition becomes audible; the weapon is the repeating
unit here, and four sits in the middle of that range. `GENERATED.md` is written
by the script itself through `Add-ProvenanceRow` (`scripts/sfx.ps1:746-764`) and
must never be hand-edited.

One stale line worth not copying: the `hukbo-sound-effects` skill calls the wasay
descriptor "Heavy Chopper". The shipped descriptor is **Wasay — War Axe**
(`src/Hukbo.Client/Presentation/BattleEventFormatter.cs:99`), and
`GENERATED.md:29-32` records why preset V2 changed it. The prompts above say
"axe".

## 9. The nine questions

`SIMULATION-GAME-STANDARDS.md:318-330`.

**1. User-visible outcome.** A blow stopped by a shield sounds like a weapon
hitting a light wooden board instead of sounding like a weapon opening a body,
and which of the four weapons threw it is audible in the timbre.

**2. Tick stage and state read and written.** None. This is presentation only.
The client's sound director reads the per-tick `BattleEvent` buffer after
`BattleSimulation` has finished advancing, exactly as the battle log and the hit
effects already do, and writes nothing back. `Hukbo.Core` does not know the audio
layer exists.

**3. Numeric units, bounds, and the same-tick conflict rule.** The only numbers
are presentation constants: four takes per slot, `SoundLogHeightPercent` 62 to
65, and the frame budget unchanged at 16 cues per slot and 64 in total. Several
`ShieldBlocked` events on one tick each produce one cue, processed in the event
buffer's emission order; the per-slot budget caps them at 16 in a frame and
`SoundVoiceLedger` scales each cue's gain against the voices already sounding.
Splitting the shield cue across four slots quadruples the effective per-frame
headroom for shield blocks relative to a single slot — a small argument in the
four-slot shape's favour that nobody claimed at decision time.

**4. Total ordering and random-stream policy.** Cue order follows `BattleEvent`
emission order, which the simulation already totally orders. Variant selection
goes through `SoundVariantSelector.Select`, unchanged, stateless, and seeded from
`(tick, sourceEntityId)` through `SplitMix64` — deterministic, and drawing from
no simulation stream, so it cannot perturb the simulation's RNG.

**5. Cache source and invalidation.** No cache. `MonoGameSoundPlayer.Load` reads
the folder once at startup into a fixed dictionary; nothing is added afterwards
and nothing is invalidated. Discovery happens once per launch, which is already
documented behaviour.

**6. Save, event, and version effect.** Presentation only. No `Hukbo.Core` file
is opened, no preset version is bumped, no golden expectation changes, and
neither the state hash nor the event hash can move.

**7. Worst-case complexity and benchmark workload.** Unchanged and O(1) per
event. The total number of cues produced is identical to today; the change moves
a subset of attack events from four per-slot budget counters onto four new ones,
which *lowers* peak demand on each attack slot. `SoundCueBudget.GetIndex` walks
`AllSounds` linearly and now walks thirteen entries instead of nine — a per-cue
constant on a list walked at most 64 times a frame. The canonical gate's
200-agent, 10,000-tick, seed-1 headless workload constructs no audio player at
all and is unaffected.

**8. Spectator explanation.** Yes — and `ShieldBlocked` is already the
best-surfaced resolution in the game. The sound is a sixth channel, and it is the
only one besides the text log that also carries *which weapon* struck.

| Channel | Where |
| --- | --- |
| Event-log line "stopped by the shield" | `Presentation/BattleEventFormatter.cs:74-75`, pair form at `:95-101` |
| Clash cross fires | `Presentation/ClashEffect.cs:34-37` |
| Clash cross colour, `ShieldStrike` | `Rendering/ClashEffectRenderer.cs:39-41` |
| Clash cross size, `ShieldArmLength` | `Rendering/ClashEffectGeometry.cs:81-91` |
| Swing recoils instead of stopping on target | `Rendering/SwingGeometry.cs:230` |
| Blood spray and impact ring suppressed | `SIMULATION-GAME-STANDARDS.md:889-890` |
| Per-faction counter | `Hukbo.Core/Simulation/CombatMetrics.cs:37-52` |
| **Sound cue, one slot per attacking weapon** | **this change** |

**9. Tests that fail before and pass after.**
`SoundCatalogTests.AllSounds_ListsEveryDeclaredSlotExactlyOnce` and
`GetFileName_IsUniqueLowercaseKebabWavForEverySlot` fail the moment the four enum
members are added without the four catalog entries.
`IsHitLocationDriven_IsTrueOnlyForTheFourWeaponSlots` gains four `false` rows.
`SoundCueMapperTests.Map_RoutesAShieldBlockToTheMatchingClashSlot` and
`Map_KeepsTheWeaponSlotForEveryOtherResolution` are new and fail against today's
mapper.
`SoundDirectorTests.Ingest_UsesANullHitClassForAShieldBlockDespiteTheHitLocation`
is new and fails against today's director — it is the guard for the trap in
section 6. `SoundCatalogTests.EveryDefinedWeapon_HasAShieldClashSlot` is new and
fails for a weapon added later without a clash slot.
`SoundLogPanelTests.CalculateLayout_FitsExactlyTenBindingRowsAtFourHundredAndSixteen`
and `CalculateLayout_CapsTheBindingViewportAtTheSlotCountRegardlessOfHeight` are
new and pin the two panel findings. For the scroll fix:
`ClampBindingScroll_ReachesTheLastRow`,
`ClampBindingScroll_RefusesToScrollPastEitherEnd`,
`ClampBindingScroll_ReturnsZeroWhenEveryRowFits`,
`GetWheelTarget_RoutesTheWheelToTheListUnderThePointer`, and
`GetWheelTarget_FallsBackToTheCueListOutsideBothLists` are all new, all fail
before the fix, and are all pure-helper tests with no `GraphicsDevice` and no
`SpriteBatch`.

## 10. Contract text this change falsifies

`SIMULATION-GAME-STANDARDS.md:894-896` currently reads:

> `Evaded` is the weakest case: distinguished by one positive channel, the
> event-log line, and three absences. The three clash sound slots that would have
> given it a fourth channel are deferred by owner decision and are not part of
> this contract.

Shipping the shield cue un-defers one of those three, so the sentence is false
and must be edited. The replacement must state three things: that a shield block
now has a sound channel, carried by four slots keyed to the attacking weapon;
that the two blade-clash slots, `clash-blade-hard` and `clash-blade-soft`, remain
deferred by owner decision; and that `Evaded` still has no sound channel of its
own, so it remains the weakest case for the same reason as before.

The Spectator channels table at `:886-892` gains a **Sound cue** row, and that
row has to be honest about what the change does not do:

| Channel | `Landed` | `ShieldBlocked` | `Parried` | `Deflected` | `Evaded` |
| --- | --- | --- | --- | --- | --- |
| Sound cue | weapon impact | `clash-shield-<weapon>` | weapon impact | weapon impact | weapon impact |

The asymmetry that follows is worth naming plainly, because
`docs/research/WEAPON_CLASH_1500s.md:570-576` treats the four non-landed outcomes
as a set and this change treats one of them: `ShieldBlocked` gains a distinct
sound, and `Parried`, `Deflected`, and `Evaded` keep playing the attacker's
impact cue. An evaded blow still makes an impact sound today and will still make
one after this change.

## 11. Limitations

**Every rendered binding row is now reachable, but only ten are on screen at
once.** The bindings list scrolls, so all thirty-seven rows — including the four
clash slots at indices 33 to 36 — can be brought into view with the wheel at the
default window size. What the 62-to-65 bump buys is a ten-row viewport rather
than nine; it is not what makes the clash slots reachable, and 75 would not have
made them reachable either. The viewport cap at `AllSounds.Count` remains by
design, so a spectator who wants to read the whole list still scrolls through it
ten rows at a time rather than seeing it at once. There is no keyboard route to
the list and no scrollbar click-paging: the wheel is the only input, matching the
cue list beside it.

**The scroll fix is approved scope growth beyond the shield-clash feature.** It
is here because the feature exposed the defect, not because the feature needs it;
a reviewer should read the scroll task as a separate change that happens to share
a branch. It touches four files that the audio work does not.

**The recommendation in section 5 reverses the number in a locked decision.**
Decision 3 stands in method — panel space is bought from the battle event log by
raising `SoundLogHeightPercent` — but the value 75 that satisfies the existing
layout test costs the entire selected-event detail pane, and it buys rows that
are not the rows anyone wanted. 65 is recommended and the existing test is
retargeted rather than satisfied. If the owner overrules, section 5 names the
exact three edits that change.

**The sound channel separates `ShieldBlocked` from everything else, not the five
resolutions from each other.** See section 10. Four of the five still share one
cue, so an evaded blow makes an impact sound.

**Five things go to the smoke checklist as `PENDING` and nothing automated covers
them.** Whether the cue is heard as wood rather than as a cut; whether the axe
reads heavier than the work blade on the same shield; whether scrolling the
bindings list actually feels right and reaches the clash rows; whether the battle
event log still reads at 214 pixels with two detail lines; and whether the cue
density holds up in a 200-agent battle. The pure helpers prove the clamping and
the wheel routing, but nothing automated proves that a wheel detent over the
bindings area moves the right list on a real screen. Only a person at an
interactive Windows desktop may flip those rows
(`docs/development/testing.md:3167-3170`); no agent may, and compilation, a
passing test run, or a window-opening probe does not count.

**`./scripts/sfx.ps1 -List` will report all four clash slots MISSING even when
all sixteen takes exist**, because `-List` probes the bare `<slot>.wav` single.
This is pre-existing behaviour that already affects the four attack slots and it
is not fixed here.

**The event-log figures in section 5 are derived by hand, not asserted by a
test.** `BattleEventLogPanel`'s constants are private and `ArenaGame` is banned
from tests, so nothing in the suite pins the relationship between
`SoundLogHeightPercent` and the number of detail lines the event log can render.
Smoke row 176 is the only check. If it shows the pane unreadable at 214, 65
should be reconsidered downward before the change is kept.
