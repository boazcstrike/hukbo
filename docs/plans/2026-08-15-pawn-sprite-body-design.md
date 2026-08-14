# Pawn sprite body — design

Date: 2026-08-15
Status: accepted, implementation authorized
Game: Hukbo (this design does not touch Sandata)

## 1. Problem

Hukbo's pawns are drawn entirely from untextured quads. `PawnRenderer` submits
a stack of `pixel` rectangles for the torso, head, legs, feet, arms, weapon
line, shield, sash, armor, and marks, and every one of them is a flat colour.
That decision has carried the game a long way — it made gait animation, the
death collapse, swing poses, and ranged draw tension cheap to build, and it
left the client with exactly one non-font asset in its content pipeline.

It has also reached its ceiling. A flat quad torso cannot carry a face, a
garment, a tattoo, a headband, or the value structure that makes a pawn read as
a person rather than a marker. The reference points the player will compare
this against — Battle Brothers and Norland — both solve it the same way, with a
layered paperdoll sprite whose gear is composited from separate drawn parts.

## 2. What this design authorizes

A **body sprite**: one drawn cell covering the pawn's head and torso, selected
per warrior from a catalog of 50 authored variants, drawn in place of the
procedural head and torso quads when the player turns the mode on.

It authorizes nothing else. In particular it does not authorize replacing the
legs, the arms, the weapon line, the shield, or any mark.

## 3. The seam, and why it falls where it does

`PawnLayout` (`src/Hukbo.Client/Rendering/PawnGeometry.cs:163`) already
separates every part of a pawn into its own rectangle. That is the seam this
design uses, and the split is not arbitrary — it follows a property the pawn
model already has:

| Part | Directional? | Animated? | Verdict |
| --- | --- | --- | --- |
| Head, torso | No — the body is heading-less | No | **Sprite** |
| Legs, feet | No | Yes — gait | Procedural |
| Arms, weapon line | Yes — the weapon arm is the only directional part | Yes — swing, ranged draw | Procedural |
| Shield | Yes — posture rotation | Yes | Procedural |
| Marks, sash, armor | No | No | Procedural |

A heading-less part can be drawn from a single authored cell, which is exactly
what makes a 50-variant catalog affordable: 50 cells, not 50 × 16 facings. A
directional or animated part cannot, which is why the arms and legs stay where
they are. Drawing a static full-body sprite would have deleted the gait
animation and the per-limb collapse, and both of those are shipped features.

`PawnTransform` (`src/Hukbo.Client/Rendering/PawnTransform.cs`) is a rigid
transform applied to every quad a pawn submits. The sprite goes through the
same value, so the death collapse rotates the sprite body exactly as it
rotates the quads today, with no new code for it.

## 4. Variant selection

Selection reuses what the presentation layer already has and introduces no
second mechanism:

- `PresentationHash` (`src/Hukbo.Client/Rendering/PresentationHash.cs`) is the
  one integer mixer every per-instance presentation visual derives from, and
  its own documentation states that nothing in it reaches the simulation.
- A new `PawnSpriteVariantSalt` joins the registry in
  `src/Hukbo.Client/Presentation/PresentationSalts.cs`, which has a
  distinctness test over its entries.

The cell index is a pure function of `EntityId` and that salt. The same warrior
draws the same body every frame and in every replay of the same battle.

**This never touches the simulation.** It reads no state the client does not
already hold, moves no state hash, no event hash, no snapshot, and no outcome.
It is presentation, on the presentation side of the boundary
`DiagnosticLoggingBoundaryTests` and `SourceHygieneTests` already police.

## 5. Faction legibility

A sprite carries its own baked skin, garment, and headband colour. Left alone
that would destroy the one thing the flat quads did perfectly — telling the two
sides apart at a glance.

The sprite is therefore drawn tinted, with the faction wash applied at reduced
strength over the cell rather than as a full multiply, and the existing hit
pulse and dead-state blends applied to that tint unchanged. Both of those are
colour-only operations today, so they compose with a textured quad without
modification. If the tint cannot hold faction legibility at gameplay zoom, the
mode fails its own acceptance and the default stays where it is.

## 6. Detail tier

`PawnDetailTier` has three values, and at `Low` the procedural path already
drops the legs and feet entirely. The sprite is drawn at `Medium` and `High`
only; `Low` keeps the procedural body. A cell whose face is four pixels tall
buys nothing, and the 48px readability probe run during authoring showed that
face detail is the first thing to vanish.

## 7. Content pipeline — the reviewed decision

`SourceHygieneTests` pins `Content.mgcb` to exactly 25 entries, and its own
doc comment states that a twenty-sixth "still needs its own decision". This
section is that decision, recorded where the test asks for it.

`Textures/PawnBodies.png` becomes entry 26: a single atlas holding all 50 body
cells in a 10 × 5 grid. One atlas rather than 50 files, because 50 pipeline
entries would be 50 separate decisions and one texture switch per pawn.

The precedent is `Textures/UiChrome.png`, entry 25, which was itself added as a
reviewed change rather than an exception to the rule. Adding entry 26 does not
open the pipeline generally: a twenty-seventh entry still fails the test, and
still needs its own decision.

## 8. Player-facing control

`PawnVisualStyle { Procedural = 0, SpriteBody = 1 }` joins `ClientSettings`,
defaulting to `Procedural`. This mirrors
`UiChromeStyle { Procedural, NineSlice }` exactly — the same enum shape and the
same store handling — because that setting solved the identical problem for UI
chrome and the second instance of a pattern should not invent a second shape
for it.

Default off means nothing a player sees changes until they ask for it, and the
two styles can be compared live rather than across builds.

The settings schema version goes from 11 to 12, and a version 11 file still
loads through `AcceptedSchemaVersions` with the new field taking its default.

**The control is the `B` key, not a menu row, and that is a compromise rather
than a preference.** This section originally specified a menu selector beside
the chrome selector. It cannot have one. The menu panel's content budget is 657
pixels — `ResponsivePanelHeight` of 680 less the 23-pixel helper line — and
both of its columns already stand at exactly 634 pixels. One more selector
costs 104 pixels and one more button costs 52, so neither column can take
either, and because all six buttons are pinned to a single column no 3/3 split
of six selectors avoids the overflow. A third column would need a panel wider
than the 1024-pixel screens the responsive tests pin. The measurement is
reproducible from `MenuOverlay.CalculateContentBottomOffset`.

So the toggle is a shortcut key that flips the style live and persists it. What
that costs is recorded honestly in section 9 below. Making room in the menu —
a scrollable settings column, a second page, or a Visuals sub-panel on the
Army Composition pattern — is the proper long-term home and is its own design.

## 9. The nine questions (SIMULATION-GAME-STANDARDS.md §10)

The one that governs here: **can a spectator discover this effect without
reading source code?**

**No, and this feature is incomplete on that count.** Section 8 records why: the
menu panel is measurably full, so the toggle is the `B` key and nothing on
screen announces it. A spectator who is never told the key never finds the
mode. The effect *itself* is unmissable once switched — every warrior on the
field changes on the next frame, and the two styles are directly comparable
within one session against the same battle — but discovering that the switch
exists currently requires being told.

Recorded rather than argued away. Section 9 of `SIMULATION-GAME-STANDARDS.md`
says a feature that fails this question is incomplete, and this one does. The
mode ships default-off and developer-facing until it earns a discoverable
control, and the work that would close the gap is the menu-room design named at
the end of section 8.

## 10. Known limitations, stated rather than hidden

- The 50 cells are vector-authored flat art, not painted. They are expected to
  read as cleaner but flatter than the reference games, and this design does
  not claim they match Battle Brothers' fidelity.
- The sprite is authored at a fixed aspect while `TorsoBounds` varies with each
  warrior's stature and build multipliers. The cell is fitted preserving its
  own aspect and anchored at the neck, so a broad warrior is not stretched;
  the consequence is that build variation reads less strongly in sprite mode
  than in procedural mode.
- Faces do not survive gameplay zoom. They are visible in the agent inspector
  and when zoomed in, and that is the whole of their value.

## 11. Not authorized by this document

Facing sets, per-frame sprite animation, gear layer compositing at runtime,
replacing the weapon or shield with drawn art, a Sandata equivalent, and any
further content pipeline entry.
