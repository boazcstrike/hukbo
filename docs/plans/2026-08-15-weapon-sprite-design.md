# Weapon sprite — design

Date: 2026-08-15
Status: accepted, implementation authorized
Game: Hukbo (this design does not touch Sandata)

## 1. Problem

Every weapon in Hukbo is three colinear lines. `PawnRenderer.DrawWeapon`
(`src/Hukbo.Client/Rendering/PawnRenderer.cs:1573`) dispatches on
`PawnWeaponRole` and calls `DrawBlade`
(`src/Hukbo.Client/Rendering/PawnRenderer.cs:1686`), which draws a grip line, a
blade line, and a highlight line between `WeaponStart` and `WeaponEnd`. The
seven roles differ only in two numbers — `gripEnd` and `widthMultiplier` — and
in the tints the visual catalog resolves for them. The shield is the same story
told with rectangles: `DrawShield`
(`src/Hukbo.Client/Rendering/PawnRenderer.cs:1241`) draws a face quad, an
optional seam or binding accent, and two edge-tone quads.

That has been enough to carry the game a long way. It made the swing pose, the
ranged draw, the death collapse, and the shield posture rotation cheap, and it
kept the client honest about what it was claiming: a line is obviously a
gameplay marker and never pretends to be a photograph of an object.

It has also reached its ceiling. A three-line blade cannot carry a lashing, a
resin sheen, a chipped edge, a wrapped grip, or a pommel. A Kampilan and a
Kalis read as the same object at slightly different angles, which is precisely
the thing a spectator is supposed to be able to tell apart. The tint work of
VIS-010 and VIS-011 pushed colour as far as colour can go, and the remaining
distance is shape.

## 2. What this design authorizes

An **authored weapon sprite**: one drawn cell per weapon, selected per warrior
from ten variants of that warrior's own role, drawn in place of the three
`DrawBlade` lines when the player turns the mode on. The tall hardwood shield
is included on the same terms and in the same atlas, drawn in place of the
`DrawShield` quads.

Eight rows of ten cells:

| Row | Role | Cells |
| --- | --- | --- |
| 0 | Kampilan | 10 |
| 1 | Wasay | 10 |
| 2 | Kalis | 10 |
| 3 | Itak | 10 |
| 4 | Bangkaw | 10 |
| 5 | Busog | 10 |
| 6 | Arquebus | 10 |
| 7 | Tall hardwood shield | 10 |

Eighty cells in total. It authorizes nothing else. In particular it does not
authorize replacing the arms, the swing trail, the bowstring, the body, the
legs, or any mark, and it does not authorize a second content pipeline entry.

## 3. The seam, and why it falls where it does

The weapon seam is narrower and cleaner than the body seam was, and the reason
is a property `PawnGeometry` already has.

`PawnGeometry.ApplySwing`
(`src/Hukbo.Client/Rendering/PawnGeometry.cs:2480`) resolves every pose in the
game to exactly this:

```
start + rotate(end - start, angle) * extension
```

It is a rotation of one fixed vector about the pivot `start`, followed by a
uniform scale. Nothing else happens to a weapon. Every path that can pose a
weapon funnels into those two scalars — `AttackPoseResolver.Resolve` produces
`WeaponAngleRadians` and `ExtensionRatio`, `PawnGeometry.ToSwingPose` produces
the same two, and `RangedGeometry` produces the same two for the three ranged
roles. There is no shear, no non-uniform stretch, no per-vertex deformation
anywhere in the weapon path.

A rigid rotation plus a uniform scale about a fixed pivot is exactly what
`SpriteBatch.Draw` expresses natively, and it is exactly what `PawnTransform`
(`src/Hukbo.Client/Rendering/PawnTransform.cs:44`) already carries for the
death collapse. So the sprite substitution is a substitution of *what is drawn*
between two points, not a change to *where those two points are*. `PawnLayout`
still computes `WeaponStart`, `WeaponEnd`, `WeaponThickness`, and `Collapse`
exactly as it does today; the geometry file is not touched at all.

There is one exception, and it is a real one. `DrawBowstring`
(`src/Hukbo.Client/Rendering/PawnRenderer.cs:1755`) and its pure helper
`GetBowstringLine` do **not** obey the rigid rule. The string's midpoint is
pulled off the stave by `RangedDrawTension`, so the string genuinely deforms
between frames. A baked cell cannot carry it. Section 9 records what happens to
it instead.

The precedent for the substitution already exists in the same file:
`DrawSpriteBody` (`src/Hukbo.Client/Rendering/PawnRenderer.cs:690`) draws an
authored cell through the same `Collapse` transform, taking an axis-aligned
fast path when the collapse is the identity. The weapon sprite uses the same
shape of code, with the weapon's own rotation composed in rather than assumed
away.

## 4. Atlas geometry — the arithmetic

One weapon cell has to hold two very differently shaped objects: the longest
weapon line in the game, and the shield block.

The longest line, measured from the offsets in
`PawnGeometry.CreateWeaponBounds` (`src/Hukbo.Client/Rendering/PawnGeometry.cs`
lines 2354 to 2392), is a tie between the Kalis, which runs from `(1, -7)` to
`(14, -21)`, and the Kampilan, which runs from `(1, -6)` to `(15, -19)`. Both
are `sqrt(365)`, about **19.1 layout units**. The shortest is the Arquebus at
about 9.5 units. The widest lateral excursion is the Wasay, whose
`weaponPadding` of 4.4 units reserves room on both sides of its line for the
axe head, so its lateral envelope is about 8.8 units. The shield block is
`4 × 11` units before stature deltas (`CreateShieldBlock`,
`src/Hukbo.Client/Rendering/PawnGeometry.cs:1935`).

An authoring envelope of **9 units laterally by 22 units along the weapon
axis** covers all of that with margin. The cell:

| Constant | Value | Where it comes from |
| --- | --- | --- |
| `CellWidth` | 112 | 9 units lateral at about 11.6 px per unit, rounded to a multiple of eight |
| `CellHeight` | 256 | 22 units along the axis at the same density |
| `Columns` | 10 | one column per variant |
| `Rows` | 8 | one row per role, seven weapons and the shield |
| `AtlasWidth` | 1120 | 10 × 112 |
| `AtlasHeight` | 2048 | 8 × 256 |

1120 by 2048. The larger dimension lands exactly on the 2048 limit, which is
deliberate and has a consequence stated plainly: **an eleventh variant or a
ninth row does not fit, and adding one is a new decision about the atlas
geometry rather than an append.** That is the same shape of rule the content
pipeline already runs on, and it is the reason this design does not leave slack
it would then have to police.

Every cell carries a four-pixel transparent gutter on all sides, because
`GenerateMipmaps=False` and linear filtering will sample across a cell boundary
otherwise. The drawn content box is therefore **104 × 248** for the seven
weapon rows.

The shield row is the one place where the cell's own aspect is wrong. A shield
is `4 × 11`, or 1 to 2.75, while the cell content box is 104 to 248, or about
1 to 2.38. Rather than letterbox the shield inside a weapon-shaped box and then
stretch the whole box, row 7 declares its own centred content sub-rectangle of
**90 × 248** — 248 × 4 ÷ 11 rounds to 90 — and the renderer uses that
rectangle as its source. That is three lines of pure arithmetic in the atlas
type and it keeps the shield's authored proportions honest.

**The wasted space, stated rather than buried.** The atlas is 1120 × 2048 =
2,293,760 pixels, or 8.75 MiB of video memory at 32 bits per pixel, against
5.0 MiB for the existing body atlas. Most of it is transparent. A straight
blade drawn along a 248-pixel axis in a 104-pixel-wide box will cover something
on the order of a fifth of its cell; the Wasay's broad head will cover more;
the shield row is the densest at about 78 percent of its cell. Those coverage
figures are estimates and the plan requires task 10 to record the real ones
once the atlas exists. The cause of the waste is a single decision — one cell
size for eight roles of very different shapes — and the alternative, a packed
atlas with a per-role rectangle table, was rejected because it makes the image
unreadable by eye, makes the packer's output depend on packing order, and adds
a table that must be kept in step with a binary file. Transparent pixels in a
9 MiB texture are not a measured frame cost. An unreadable atlas is a permanent
review cost.

**Authoring orientation, pinned so that the art and the renderer agree.** The
cell is taller than it is wide, so every weapon is authored **pointing up**:
the grip sits at the bottom-centre of the content box and the tip runs toward
the top edge. The shield is authored upright, standing the way it stands on the
field.

This matters because the renderer's existing convention is the opposite one.
`DrawLine` draws along positive X, with its origin at the left edge and its
rotation taken straight from `atan2(end - start)`. A weapon sprite authored
pointing up is therefore a quarter turn away from that, and the renderer adds a
single documented constant of `-π/2` to the line's angle before drawing. The
shield adds nothing, because it is not drawn along the weapon line at all — it
goes through `AboutPivot(centre, ShieldPostureRotationRadians)` exactly as it
does today.

The alternative was to author along positive X instead, which would have made
the weapon's rotation byte-identical to `DrawLine`'s and removed the constant.
It was rejected for two reasons. The cell would have to be 256 wide by 112 tall,
which transposes the atlas to eight columns of roles by ten rows of variants —
the same 1120 by 2048 image, only sideways and harder to read. And the shield
would then be authored lying on its side while every weapon lay flat too, so an
atlas that a reviewer has to mentally rotate to judge. One constant in one draw
call is cheaper than eighty cells that cannot be read at a glance.

**Authoring density, against what the camera can actually show.** One layout
unit is one screen pixel at apparent scale 1, and the apparent scale is clamped
to a maximum of 2.40 (`ConservativePawnCull`,
`src/Hukbo.Client/Rendering/ConservativePawnCull.cs:79`). The longest weapon on
screen is therefore about 19.1 × 2.40 ≈ 46 pixels, and a 248-pixel cell
minified into it is roughly a 5.4× supersample. That is deliberately less
extravagant than the body atlas, whose 234-pixel cell covers a head and torso
that can never exceed about 28 screen pixels — an 8.5× supersample. Five times
is comfortably enough headroom for a downsample; more would only cost memory
and sharpen the aliasing that comes with having no mipmaps.

## 5. Variant selection

Selection reuses the presentation layer's existing mechanism and introduces no
second one. A new `WeaponSpriteVariantSalt` joins the registry in
`src/Hukbo.Client/Presentation/PresentationSalts.cs`, whose entries already
have a distinctness test, and the variant index within a role's row is a pure
function of `EntityId` and that salt through `PresentationHash`.

The row is not chosen — it is read from `PawnAppearance.WeaponRole` and
`PawnAppearance.ShieldRole`, both of which are resolved from the authoritative
loadout. A spearman never draws a sword cell.

**This never touches the simulation.** It reads no state the client does not
already hold, and it moves no state hash, no event hash, no snapshot, and no
outcome.

**Classification invariance is a constraint on the art, not only on the code.**
VIS-010 and VIS-011 guarantee that a spectator can never read a mechanical
difference from a tint. The atlas extends that surface: ten cells now differ in
*shape* within a single role. The guarantee is preserved by requiring that the
ten variants of a row differ only in wear, proportion within the attested
envelope, lashing, and grip treatment — never in anything a spectator could map
onto a simulation value, because no simulation value varies within a role in
the first place. A test asserts that the variant index derives from the entity
identifier and the salt alone and reads no stat, which is the mechanical half
of the same promise.

## 6. Historical constraint — what the art may and may not draw

CLAUDE.md section 7 and `docs/research/HISTORICAL_1500s_WEAPONS.md` bind this
work, and `WeaponVisualCatalog` already records the specific exclusions. They
are repeated here because they are the binding brief for eighty drawings:

- **Kampilan.** The pawn-scale silhouette is K1: uniform blade width, no
  ornament. Chain-mail guards, bifurcated pommels, tip spikelets, hair tassels,
  and pommel creature motifs are documented only on eighteenth- and
  nineteenth-century objects and may **never** appear at pawn scale
  (`WeaponVisualCatalog.cs:364`). The widening profile and truncated tip are
  documented on later objects and are deferred, not licensed
  (`WeaponVisualCatalog.cs:340`). The K2 ornamented entry exists for the
  inspector and the armory card and is not drawn on the field
  (`WeaponVisualCatalog.cs:355`).
- **Wasay.** The Cordilleran head axe is excluded from the roster entirely and
  carries no catalog identifier at all (`WeaponVisualCatalog.cs:456`). The only
  drawable form is a broad iron head on a short hardwood haft.
- **Bangkaw.** One documented form, one catalog entry, no alternates
  (`WeaponVisualCatalog.cs:682`). The point stays steel.
- **Busog.** The arrows are pale reed with **hardwood** points, not iron, and
  the quiver is visible at the back (`WeaponVisualCatalog.cs:755`). The stave is
  a tall arc that leaves the torso silhouette.
- **Itak.** One pawn-scale entry, and the entry's own text closes off form
  variation more tightly than any other row (`WeaponVisualCatalog.cs:572`).
- **Arquebus.** No local name is located anywhere in the sources, so the
  drawing carries no cultural badge, no ornament, and no regional marking
  (`PawnAppearance.cs:138`).
- **Shield.** Pigafetta describes the wood as thin, and the catalog's own note
  says the "hardwood" identifier slightly overstates that
  (`ShieldVisualCatalog.cs:136`). A shield skin "may only ever change how the
  block looks, never what it covers" (`ShieldVisualCatalog.cs:11`), and the
  sprite inherits that rule unchanged: a sprite shield occupies exactly
  `ShieldBounds` and covers exactly what the quad covered.
- The Boxer Codex guides silhouette and colour only, never technical
  cataloguing.
- Every gameplay-legibility choice in the art — and there will be many, because
  a drawing has to decide things the sources do not — is labelled provisional in
  the generator's own comments, in the same voice `CreateWeaponBounds` already
  uses for the three ranged lines.

## 7. The central tension, and how it resolves

Two pieces of guidance point in opposite directions and both of them are
correct.

The art guidance says that ten cells which share one silhouette and differ only
in colour are close to wasted work, and that the money is in three or four
genuinely distinct silhouette families per weapon class. Battle Brothers, the
nearest reference, gets its variety exactly that way, and it deliberately
exaggerates blade thickness, haft width, and guard size because a realistically
thin blade "couldn't be recognised from afar".

The historical policy says that inventing distinct silhouette families is
precisely the thing forbidden. A new Kampilan silhouette is a new claim about
what a Kampilan was, and the sources do not support one. That is not a
technicality: it is the same rule that excluded the panabas and the Cordilleran
head axe.

**The resolution is to vary the outline inside the attested envelope rather
than across it.** For each role, the ten cells differ along axes that change
what is drawn without changing what is claimed:

- proportion — blade length and width, and haft length, moved within the range
  the documented form permits;
- edge condition — chips, nicks, a rolled or blunted tip, a resharpened bevel;
- lashing — the count, spacing, and placement of the bindings at the ferrule or
  the tang;
- grip treatment — how far a wrap runs, whether it is present at all, whether
  the pommel is plain;
- surface — resin sheen, patina, soot, the difference between a new blade and
  one carried for a season.

Exaggeration is still applied, and is applied uniformly across a row rather
than as a per-variant difference, so it never becomes a classification signal.

Each cell is drawn silhouette-first with two or three value bands per part —
base, shadow, edge highlight — and roughly six to ten colours, with the
brightest highlight kept strictly on the cutting edge.

**This will not read equally well across all eight rows, and the plan says so
rather than promising otherwise.** The Kampilan has the widest documented room
and its ten cells will look genuinely different from one another. The Itak has
the least: its catalog entry closes off form variation harder than any other
row, so its ten cells will differ mostly in wear and grip and will read as one
weapon in ten conditions rather than ten weapons. Pure recolour is permitted
only in the tail slots of a row, where the earlier axes are exhausted.

**One deliberate divergence from the reference.** Battle Brothers never rotates
a master cell; each weapon is an authored bitmap fixed at one orientation,
because rotating pixel art boils and blurs. Hukbo rotates at runtime and always
will, because `ApplySwing` produces a continuous angle rather than a set of
poses. That is safe here for a reason specific to this project: the cells are
vector-authored and downsampled roughly five times, not pixel art authored at
final size, so linear-filtered rotation costs sharpness rather than integrity.
The pivot is still snapped to a consistent point — the grip end of the content
box — so a rotating weapon turns about the hand rather than wandering.

## 8. Catalog rows — the decision not to add any

Eighty cells do not become eighty `VisualCatalogEntry` rows, and the pinned
tally in `VisualCatalogContractTests` stays at 60.

A `VisualCatalogEntry` is not an inventory slot. It is a player-facing evidence
record: an identifier, a pair-form display label, an evidence tier, and the
inspector text that justifies the tier. Adding eighty of them would assert to
the inspector that there are eighty distinct historical identifications behind
the art. There are eight, and section 6 lists them. Manufacturing eighty
evidence strings for cells that carry no new claim is exactly the failure mode
CLAUDE.md section 7 exists to prevent, and it would put the tally test in the
position of pinning fiction.

So the atlas mirrors `PawnSpriteAtlas`, which added no catalog rows either.
Each atlas *row* is bound to the existing catalog entry for its role by a pinned
table inside `WeaponSpriteAtlas`, the inspector keeps showing the same
pair-form label and the same evidence tier it shows today, and the per-variant
authoring intent lives in this design document and in the generator's comments,
where a reader can find it without it becoming a claim the game makes on
screen.

## 9. Bowstring, trail, and everything that stays procedural

- **The bowstring stays procedural.** It is the one weapon element that
  genuinely deforms — `GetBowstringLine` pulls the midpoint off the stave by
  `RangedDrawTension` — so no baked cell can carry it. The Busog cells therefore
  draw the **stave only, with no string**, and `DrawBowstring` continues to draw
  its two segments over the sprite exactly as it does today. A Busog in sprite
  mode is one textured quad plus two line quads.
- **The swing trail stays procedural.** `DrawSwingTrail` strokes an arc into six
  segments and has nothing to do with the weapon's own image.
- **The arms, hand, body, legs, and every mark stay procedural.** This design
  touches `DrawWeapon` and `DrawShield` and nothing else in the pawn stack.
- **`PawnGeometry` is not edited.** Every rectangle and every endpoint the
  renderer consumes is unchanged, which is what keeps `ConservativePawnCull`,
  the swing envelope, and the bounds union correct without re-deriving any of
  them.

## 10. Detail tier

`DrawWeapon` fires at every tier today; it is the one pawn element that is
never gated. The sprite path does not inherit that.

The sprite draws at **Medium and High only** — `DetailTierGate.ShouldDraw`
with a minimum of `VisualDetailTier.Medium`, the same thresholds every other
catalog consumer uses (below 0.95 is Low, below 1.80 is Medium, at or above
1.80 is High). At Low, a weapon is about fourteen screen pixels long and a
248-pixel cell minified eighteen times without mipmaps shimmers; the
three-line procedural blade is crisp at any size and stays. The shield sprite
gates the same way, which also matches the procedural shield, whose seam,
curvature, and edge tones already degrade away at Low.

The honest consequence: a spectator zoomed far enough out sees no difference
between the two modes at all.

## 11. Quad-count accounting

`PawnQuadCount` is a pinned model of what the renderer submits
(`src/Hukbo.Client/Rendering/SubmissionCount.cs`), and sprite mode changes the
answer, so the counting method has to learn about the mode. Both call sites
already have the style in hand.

| Element | Procedural today | Sprite mode |
| --- | --- | --- |
| Weapon, six roles | 3 | 1 |
| Weapon, Busog | 5 (3 blade + 2 bowstring) | 3 (1 sprite + 2 bowstring) |
| Shield | 1 to 6, depending on skin and tier | 1 |

`WeaponQuadCount` gains a style parameter and `CountShield` gains one too. The
`RenderBudgetEstimate` comments that track quad deltas per feature are updated
in the same change.

**Sprite mode strictly reduces the submission count**, by two per weapon and by
up to five per shield. That is a finding rather than a goal — nothing here was
done for performance — but it means the mode cannot be blamed for a budget
regression, and the plan requires the updated counts to be asserted rather than
assumed.

## 12. Player-facing control

A new `WeaponVisualStyle { Procedural = 0, Sprite = 1 }` joins `ClientSettings`,
defaulting to `Procedural`, and the schema version goes from 11 to 12 with the
version 11 file still loading through `AcceptedSchemaVersions` and the new field
taking its default.

It is a **separate** setting from the pawn body package's `PawnVisualStyle`,
for two reasons. The packages sit on independent branches and neither can
assume the other has landed. And the two are genuinely separable during
evaluation: comparing a sprite weapon against a procedural body is a thing
someone will want to do, and folding them into one enum would make that
impossible without a code change.

**The shield shares the weapon's setting and the weapon's atlas.** It shares the
atlas because only one new content pipeline entry is authorized, and it shares
the setting because a sprite blade held beside a flat quad shield in the same
hand-space reads as a rendering bug rather than as a choice. One switch, one
consistent look.

**The control is the `V` key, not a menu row, and that is a compromise rather
than a preference.** The menu panel's content budget is 657 pixels —
`ResponsivePanelHeight` of 680 less the 23-pixel helper line — and both of its
columns already stand at exactly 634 pixels. One more selector costs 104 pixels
and one more button costs 52, so neither column can take either, and because
all six buttons are pinned to a single column no 3-and-3 split of six selectors
avoids the overflow. A third column would need a panel wider than the
1024-pixel screens the responsive tests pin. The measurement is reproducible
from `MenuOverlay.CalculateContentBottomOffset`.

`V` was chosen after checking every key the client already binds in this branch
— A, B is unbound here but claimed by the pawn body package on its own branch,
D, the digit and numpad rows, R, S, W, Tab, Space, Enter, Escape, Home, End,
F9, Back, the arrows, and both shifts. `W` would have been the mnemonic choice
and is camera panning. `V` for visuals is free on both branches, which also
means the two packages do not collide on input when they merge.

## 13. Default on or off

The mode ships **default off**, and the reason is a rule rather than a taste.

Interactive behaviour in this repository is proven only by the manual smoke
checklist, and no agent may flip a row to `PASS`. Nobody implementing this
package can verify that eighty authored cells read correctly at gameplay zoom,
that a rotating sprite tracks its target, or that the two factions stay
tellable apart. Shipping default-on would put unverified art in front of every
player on the strength of a compiler and a unit suite.

So `Procedural` is the default, the smoke rows in the plan are recorded
`PENDING`, and **flipping the default is a separate change that belongs to
whoever runs those rows and finds them good.** This design does not
pre-authorize it.

## 14. The nine questions (SIMULATION-GAME-STANDARDS.md §10)

The one that governs here: **can a spectator discover this effect without
reading source code?**

**No, and this feature is incomplete on that count.** Section 12 records why.
The menu panel is measurably full, so the toggle is the `V` key and nothing on
screen announces it. A spectator who is never told the key never finds the
mode, and because the mode ships default-off, a spectator who never finds the
key never sees the art at all.

The effect *itself* is unmissable once switched — every armed warrior on the
field changes on the next frame, and the two styles are directly comparable
within one session against the same battle. Discovering that the switch exists
currently requires being told.

Recorded rather than argued away. Section 9 of `SIMULATION-GAME-STANDARDS.md`
says a feature that fails this question is incomplete, and this one does. The
mode ships default-off and developer-facing until it earns a discoverable
control, and the work that would close the gap is the menu-room design named at
the end of section 12.

## 15. Known limitations, stated rather than hidden

- **The pawn is not mirrored, so a leftward swing shows the wrong edge.**
  `ApplySwing`'s own remarks record that the silhouette is not mirrored for a
  warrior striking to its left, and `DrawLine` submits `SpriteEffects.None`
  throughout. Today that costs nothing, because three colinear lines have no
  edge side. A drawn single-edged blade does. In sprite mode a leftward swing
  will show the cutting edge on the trailing side of the arc. Mirroring would
  need a facing the pose does not carry, and inventing one is outside this
  design. The mitigation is to keep the edge highlight strong but the spine and
  edge silhouettes close enough that the error reads as a lighting oddity rather
  than as a reversed weapon — a mitigation, not a fix.
- **The grip and blade lose their separate hues.** `DrawBlade` tints the grip,
  the blade, and the highlight as three independent catalog colours. A single
  tinted sprite draw cannot. The cells bake the grip-versus-blade *value*
  structure and take one tint, so a grip reads as a darker band of the blade's
  hue rather than as its own colour. Drawing two masked layers per weapon would
  restore it at the cost of doubling both the atlas and the submission count,
  and that is not authorized here. The per-weapon tint distinction that VIS-010
  and VIS-011 shipped survives intact; only the within-weapon hue split is lost.
- **Ten Itak cells will read less distinctly than ten Kampilan cells.** Section
  7 explains why, and no amount of authoring effort changes it.
- **The atlas is mostly transparent.** Section 4 gives the arithmetic and the
  reason.
- **Nothing changes at the Low detail tier.** Section 10.
- **The art is vector-authored flat colour, not painted.** It is expected to
  read as cleaner and flatter than the reference games, and this design makes no
  claim to match their fidelity.

## 16. Not authorized by this document

Per-frame weapon animation, mirrored or facing-specific cells, runtime layer
compositing, drawn arms or hands, a drawn bowstring, replacing the swing trail,
a second content pipeline entry, a Sandata equivalent, new catalog rows, and
flipping the default to on.
