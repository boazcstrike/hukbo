# Weapon sprite — plan

Date: 2026-08-15
Design: `2026-08-15-weapon-sprite-design.md` (binding; this plan does not
override it)
Game: Hukbo only

## Context

Every weapon in Hukbo is three colinear lines and every shield is a small stack
of quads. Seven roles differ only in two numbers and a tint, so a Kampilan and a
Kalis read as the same object at slightly different angles. The tint work of
VIS-010 and VIS-011 pushed colour as far as colour goes; the remaining distance
is shape.

This package adds an authored sprite atlas of eighty cells — ten variants for
each of the seven weapon roles and ten for the tall hardwood shield — drawn in
place of `DrawBlade` and `DrawShield` behind a setting that defaults off. It
does not touch `PawnGeometry`, it does not touch the arms or the swing trail,
and it leaves the bowstring procedural because the bowstring is the one weapon
element that genuinely deforms. The design's sections 3, 4, and 9 carry the
reasoning.

## Where the work happens

Branch `weapon-sprites`, in the worktree at
`.claude/worktrees/weapon-sprites`, based on `main` at `9f794ce`. That worktree
was verified clean at the start of this package, with `Content.mgcb` at 25
entries, `PresentationSalts.All` at 14, and `SupportedSchemaVersion` at 11.

**The main checkout is not this branch and must not be edited by this package.**
It carries a large uncommitted pawn *body* sprite package — `PawnSpriteAtlas.cs`,
`PawnVisualStyle.cs`, `PawnBodies.png`, `tools/gen_pawn_bodies.py`, a `B` key
toggle, and edits to `PawnRenderer.cs`, `ArenaGame.cs`,
`ArenaGame.Rendering.cs`, `ClientCommand.cs`, `PresentationSalts.cs`,
`ClientSettings.cs`, `ClientSettingsStore.cs`, `Content.mgcb`, and three test
files. That work belongs to another session. Anything you see in the main
checkout that this plan does not name is not yours; do not revert it, do not
build on it, and do not assume it will land before this branch does.

## Task list

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| 1 | Scaffold the SVG generator and the reproducible atlas packer. Shared palette, shared cell-frame helper, a role registry the per-role modules append to, and a packer that rasterises each SVG and montages the grid | `tools/weapon_sprites/__init__.py`, `tools/weapon_sprites/palette.py`, `tools/weapon_sprites/frame.py`, `tools/gen_weapon_sprites.py`, `tools/pack_weapon_atlas.py` | Running the generator with no roles registered emits nothing, and the packer produces a fully transparent 1120×2048 8-bit RGBA PNG from an empty cell set | — | `magick identify -format "%wx%h %[depth] %[channels]"` reports `1120x2048 8 srgba` |
| 2 | Author the ten Kampilan cells | `tools/weapon_sprites/kampilan.py` | Ten SVGs emitted; K1 form only — uniform blade width, no chain-mail guard, no bifurcated pommel, no tip spikelet, no tassel, no creature motif; each variant's departure from the base recorded in a module comment | 1 | Task 10, plus a reviewer reading the module against design section 6 |
| 3 | Author the ten Wasay cells | `tools/weapon_sprites/wasay.py` | Ten SVGs emitted; broad iron head on a short hardwood haft only; no Cordilleran head-axe form anywhere | 1 | Task 10, plus design section 6 |
| 4 | Author the ten Kalis cells | `tools/weapon_sprites/kalis.py` | Ten SVGs emitted, longest line in the row at 19.1 units of authored envelope | 1 | Task 10, plus design section 6 |
| 5 | Author the ten Itak cells | `tools/weapon_sprites/itak.py` | Ten SVGs emitted; variation carried by wear, grip, and lashing because the catalog closes off form variation hardest here; the module comment says so | 1 | Task 10, plus design section 6 |
| 6 | Author the ten Bangkaw cells | `tools/weapon_sprites/bangkaw.py` | Ten SVGs emitted; one documented form, steel point, variation in haft finish and ferrule lashing only | 1 | Task 10, plus design section 6 |
| 7 | Author the ten Busog cells | `tools/weapon_sprites/busog.py` | Ten SVGs emitted; **stave only, no bowstring drawn**; arrows pale reed with hardwood points, never iron; back quiver visible | 1 | Task 10, plus design sections 6 and 9 |
| 8 | Author the ten Arquebus cells | `tools/weapon_sprites/arquebus.py` | Ten SVGs emitted; no cultural badge, ornament, or regional marking of any kind | 1 | Task 10, plus design section 6 |
| 9 | Author the ten tall hardwood shield cells | `tools/weapon_sprites/shield.py` | Ten SVGs emitted into the row's own 90×248 content box at true 4:11 proportion; thin-plank reading, straight rectangular outline unless the skin says otherwise; no skin changes what the block covers | 1 | Task 10, plus design sections 4 and 6 |
| 10 | Generate and commit the atlas, and record the real ink coverage | `src/Hukbo.Client/Content/Textures/WeaponSprites.png`, `docs/plans/2026-08-15-weapon-sprite-design.md` (section 4 estimate replaced with the measurement) | Atlas is exactly 1120×2048, 8-bit RGBA; regenerating from a clean checkout reproduces it byte for byte; per-row coverage percentages recorded | 2–9 | `magick identify` for the shape; a second clean run of tasks 1 and 10 for reproducibility |
| 10a | Render a review contact sheet and look at it. The atlas alone cannot be judged by any test in this repository, and "beautiful designs" is the stated goal of the package, so the art gets an explicit visual gate rather than an implied one | `tools/pack_weapon_atlas.py` (a `--contact-sheet` mode), `artifacts/weapon-sprites/contact-sheet.png` (untracked) | A single PNG shows all eighty cells on a mid-grey checkerboard with row and variant labels, plus one strip per role showing that role's ten cells at true gameplay size (46 pixels on the long axis) beside the procedural line they replace. The integrating session opens the image and judges it. A row that reads as ten copies of one drawing, or that vanishes at 46 pixels, goes back to its authoring task | 10 | The integrating session viewing the image directly, and recording the verdict per role in this document |
| 11 | Add the atlas to the content pipeline | `src/Hukbo.Client/Content/Content.mgcb` | A new stanza whose parameters are byte-identical to `Textures/UiChrome.png`'s — `TextureImporter`, `TextureProcessor`, `ColorKeyColor=255,0,255,255`, `ColorKeyEnabled=False`, `GenerateMipmaps=False`, `PremultiplyAlpha=True`, `ResizeToPowerOfTwo=False`, `MakeSquare=False`, `TextureFormat=Color` — appended at the end, never inserted | 10 | `WeaponSprites.xnb` appears under `Content/bin/DesktopGL/Content/Textures/` after a build |
| 12 | Update the pinned content-pipeline lists and record the reviewed decision | `tests/Hukbo.Client.Tests/SourceHygieneTests.cs` | `PinnedContentPipelineEntries` and `PinnedNonFontContentPipelineEntries` both carry `Textures/WeaponSprites.png`, appended; the test's doc comment gains its own paragraph naming this design document as the authorizing decision and restating that the *next* entry still needs a decision of its own | 11 | `SourceHygieneTests` green |
| 13 | Add the settings enum and store handling | `src/Hukbo.Client/Settings/WeaponVisualStyle.cs`, `src/Hukbo.Client/Settings/ClientSettings.cs`, `src/Hukbo.Client/Settings/ClientSettingsStore.cs` | `WeaponVisualStyle { Procedural = 0, Sprite = 1 }`; `SupportedSchemaVersion` 12; `AcceptedSchemaVersions` still accepts 11 with the new field defaulted; out-of-range values reset rather than throw | — | Task 22 |
| 14 | Add the variant-selection salt | `src/Hukbo.Client/Presentation/PresentationSalts.cs` | `WeaponSpriteVariantSalt` declared with a value distinct from all fourteen existing salts and registered in `All`; no `Dictionary<` or `HashSet<` introduced | — | Task 21 |
| 15 | Add the atlas geometry and selection type | `src/Hukbo.Client/Rendering/WeaponSpriteAtlas.cs` | Cell width 112, height 256, 10 columns, 8 rows, 4-pixel gutter, 104×248 weapon content box, 90×248 shield content box; role-to-row map; variant index from `EntityId` and the salt; role-to-catalog-id binding table; every member pure, no `GraphicsDevice`, no `SpriteBatch` | 14 | Task 16 |
| 16 | Test the atlas type | `tests/Hukbo.Client.Tests/WeaponSpriteAtlasTests.cs` | Every cell rectangle asserted in-bounds and non-overlapping; both content boxes asserted; row map asserted for all seven roles and the shield; selection asserted stable for a fixed entity id and spread across all ten variants; out-of-range index throws; no `ArenaGame`, `GraphicsDevice`, `SpriteBatch`, GPU, audio, focus, network, or wall clock touched | 15 | Suite green |
| 17 | Draw the weapon sprite | `src/Hukbo.Client/Rendering/PawnRenderer.cs` | `DrawWeapon` takes the style and, in sprite mode at Medium tier and above, submits one textured quad rotated about the grip end onto the `WeaponStart`–`WeaponEnd` line and composed with `layout.Collapse`; the Busog's two `DrawBowstring` segments still draw over it; `DrawBlade` is untouched and still used at Low and in procedural mode; no other draw and no draw order changes | 15 | Task 25 |
| 18 | Draw the shield sprite | `src/Hukbo.Client/Rendering/PawnRenderer.cs` | `DrawShield` takes the style and, in sprite mode at Medium tier and above, submits one textured quad into `ShieldBounds` through the existing `AboutPivot(centre, ShieldPostureRotationRadians).Then(Collapse)` transform, sourced from the row's 90×248 content box; the procedural face, seam, curvature, and edge tones remain the Low-tier and procedural-mode path | 17 | Task 25 |
| 19 | Teach the quad-count model about sprite mode | `src/Hukbo.Client/Rendering/SubmissionCount.cs` | `WeaponQuadCount` and `CountShield` take the style; sprite mode yields 1 for a weapon, 3 for a Busog, 1 for a shield; the `RenderBudgetEstimate` per-feature delta comments are updated in the same edit | 17, 18 | Task 20 |
| 20 | Repair and extend the quad-count tests | `tests/Hukbo.Client.Tests/PawnQuadCountTests.cs` | Both modes covered for all seven roles, both shield skins, and all three tiers; the reduction claimed in design section 11 is asserted rather than assumed | 19 | Suite green |
| 21 | Repair the salt registry count pins | `tests/Hukbo.Client.Tests/PresentationSaltsTests.cs`, `tests/Hukbo.Client.Tests/VisualCatalogContractTests.cs` | Count assertions read fifteen; the new salt is covered like its siblings; the shipped-entry tally assertion still reads 60 and a comment records that the atlas deliberately adds no catalog rows | 14 | Both suites green |
| 22 | Repair and extend the settings tests | `tests/Hukbo.Client.Tests/ClientSettingsStoreTests.cs` | Schema pin at 12; round trip, defaulting, out-of-range reset, and a version 11 load all covered for the new field | 13 | Suite green |
| 23 | Load the atlas and thread the style through the draw path | `src/Hukbo.Client/ArenaGame.cs`, `src/Hukbo.Client/ArenaGame.Rendering.cs` | Atlas loaded once at startup; every pawn draw receives the style, the atlas, and the entity identifier; nothing loads per frame | 11, 13, 15, 17, 18 | Task 25 |
| 24 | Add the live toggle | `src/Hukbo.Client/Presentation/ClientCommand.cs`, `src/Hukbo.Client/ArenaGame.cs` | `V` flips the style on the next frame and persists it through `ClientSettingsStore`; no existing binding changes | 23 | Task 25 |
| 25 | Run the canonical gate | — | `./scripts/verify.ps1 -Game Hukbo` output recorded verbatim in this document | 1–24 | The gate itself |
| 26 | Add smoke rows | `docs/development/smoke-checklist.md` | The rows below added as `PENDING`, status column recounted at write time | 25 | A human at an interactive desktop |
| 27 | Reconcile with the pawn body package at merge time | `Content.mgcb`, `SourceHygieneTests.cs`, `ClientSettings.cs`, `ClientSettingsStore.cs`, `PresentationSalts.cs`, `ClientCommand.cs`, `ArenaGame.cs`, `ArenaGame.Rendering.cs`, `PawnRenderer.cs`, `PresentationSaltsTests.cs`, `VisualCatalogContractTests.cs` | Whichever package lands second re-derives its own numbers against the merged tip and re-runs the gate; no conflict resolved by deleting the other package's line | 25 | A second full gate run after the merge |

## Rules for running this list

- **Tasks 2 through 9 are parallel-safe.** Each owns exactly one module under
  `tools/weapon_sprites/` and touches nothing else. They can run eight-wide
  after task 1, which is the ceiling anyway.
- **Tasks 2 and 3 run first, as a two-agent probe wave, before the other six
  start.** They are the two cells that test the geometry at its limits: the
  Kampilan is tied for the longest weapon line at 19.1 units, and the Wasay is
  the widest, its axe head sitting inside a lateral envelope inferred from a
  `weaponPadding` of 4.4 units. That padding is a bounds-inflation constant, not
  a measurement of an axe head, so the 104-pixel content box is an inference and
  not a fact. If either cell does not fit, the cell size changes — and changing
  it after six more modules have been authored against it means re-authoring all
  six. The probe costs one wave; getting it wrong costs the package.
- **Tasks 13, 14, and 15 are parallel-safe** with each other — `Settings/`,
  `Presentation/PresentationSalts.cs`, and `Rendering/WeaponSpriteAtlas.cs` are
  disjoint — and task 15 must start after 14 because it consumes the salt.
- **Tasks 16, 20, 21, and 22 are parallel-safe** once their producers land; they
  own four different test files.
- **`PawnRenderer.cs` is a funnel. Tasks 17 and 18 run strictly in that order,
  one agent at a time.** They are the two largest edits in the package and they
  sit in the same file.
- **`ArenaGame.cs` is a funnel. Tasks 23 and 24 run strictly in that order.**
- No agent commits. Stage by pathspec from the integrating session; the main
  checkout is shared with another session and `git add -A` will sweep work that
  is not ours.
- Code discovery normally goes through the `tokensave` MCP tools rather than
  Grep or Glob. **In the session that wrote this plan those tools were not
  registered at all**, so the work fell back to `Read`, `Grep`, and `Glob`.
  Check whether the tools exist before writing "use tokensave" into a sub-agent
  brief; an agent told to use a tool it does not have will stall rather than
  fall back. An Explore agent is not the fallback and is never the answer here.

## Tooling notes the authoring tasks need

These are verified properties of this machine, not guesses, and every one of
them has already cost a session.

- **Python runs through `uv`.** `uv run python tools/gen_weapon_sprites.py`.
  Never bare `python`, never `py`, never a hunted interpreter path.
- **`rsvg-convert` is not installed.** ImageMagick lists an rsvg delegate but the
  binary is absent from `PATH`, so rasterisation falls to ImageMagick's own
  internal MSVG renderer, and the two traps below follow directly from that.
- **Eight-digit hex is silently wrong.** MSVG reads `#0A1B2C80` as an opaque
  colour and throws the alpha byte away. Use `fill-opacity` and
  `stroke-opacity` attributes instead. The failure mode is an opaque navy block
  where a soft shadow should be, and it does not error.
- **`-background none` is mandatory** on every rasterise call, or the cell comes
  out on white.
- **`-depth 8` is mandatory.** ImageMagick 7.1.1-45 is a Q16 build and will
  otherwise write a 16-bit PNG. `PawnBodies.png` is 8-bit and this atlas must
  match.
- The proven shape is `magick -background none -depth 8 cell.svg cell.png`
  followed by `magick montage <cells> -tile 10x8 -geometry 112x256+0+0
  -background none -depth 8 WeaponSprites.png`.
- **The packer ships in the repository.** The pawn body package's own packing
  step was done by hand outside version control and its atlas cannot be
  regenerated from source. That is the defect this plan's task 1 and task 10
  exist to avoid repeating: after this package lands, deleting
  `WeaponSprites.png` and re-running two commands must reproduce it.

## The merge collision, named in advance

The pawn body package and this package touch the same append sites with
different values. Neither branch is wrong; whichever lands second must
re-derive, not overwrite.

| Site | Body package | This package | After both |
| --- | --- | --- | --- |
| `Content.mgcb` entries | 25 → 26 | 25 → 26 | 27, both stanzas present |
| `SourceHygieneTests` pinned lists | adds `PawnBodies.png` | adds `WeaponSprites.png` | both, and the doc comment carries both decisions |
| `SupportedSchemaVersion` | 11 → 12 | 11 → 12 | 13, with 11 and 12 both accepted |
| `PresentationSalts.All` count | 14 → 15 | 14 → 15 | 16 |
| Toggle key | `B` | `V` | both, no overlap |
| `PawnRenderer.cs` | adds `DrawSpriteBody` | changes `DrawWeapon` and `DrawShield` | disjoint methods, textual conflict only |
| `ArenaGame` load and thread | body atlas | weapon atlas | two atlases, two style fields |

Task 27 owns this. It is not optional and it is not a merge-conflict cleanup:
the second package to land runs the full gate again afterwards, because a
schema number and a pinned count are exactly the kind of thing a textual merge
resolves plausibly and wrongly.

## What this plan deliberately does not do

- It does not give the mode a discoverable control. The menu panel is measurably
  full — design section 12 carries the arithmetic — so the toggle is a shortcut
  key and the feature is recorded as incomplete against
  `SIMULATION-GAME-STANDARDS.md` §10. Making room in the menu is its own design.
- It does not flip the default to on. Nobody in this package can verify eighty
  authored cells at gameplay zoom, and a manual smoke row is the only thing that
  could. Design section 13.
- It does not add a single `VisualCatalogEntry` row, and the shipped-entry tally
  stays at 60. Design section 8.
- It does not touch `PawnGeometry.cs`, so no endpoint, bound, or cull envelope
  moves.
- It does not draw the bowstring, the swing trail, the arms, or the body.
- It does not touch `Hukbo.Core`, any hash, any snapshot, or any outcome. Every
  source file above is presentation.
- It does not touch Sandata.

## Verification

The canonical gate, run once after integration and never delegated:

```powershell
./scripts/verify.ps1 -Game Hukbo
```

Both suites matter here, not just one: a `ClientCommand` addition, a
`PawnRenderer` change, and a `SubmissionCount` signature change can all redden
the Client suite while Core stays green.

Interactive behaviour is proven only by the manual checklist. Compilation, the
unit suites, and a window-opening probe do not let any row be flipped to
`PASS`.

## Smoke rows this package owes

| Row | What to check |
| --- | --- |
| WS-1 | Pressing `V` in a live battle switches every armed warrior's weapon on the next frame, and pressing it again switches back |
| WS-2 | The chosen style survives a restart |
| WS-3 | A swinging warrior's sprite weapon tracks the swing arc and stays anchored at the hand rather than sliding or wandering |
| WS-4 | A dying warrior's sprite weapon rotates with the collapse rather than staying upright |
| WS-5 | Warriors of the same role do not all carry the same weapon — visible variety across a full field |
| WS-6 | The seven roles are tellable apart at gameplay zoom in sprite mode, and more easily than in procedural mode |
| WS-7 | A drawing archer's bowstring still pulls back, still meets both stave tips, and does not float off the drawn stave |
| WS-8 | Sprite shields still occupy exactly the block the procedural shield occupied — no overlap onto the ground ring, the head, or the weapon line |
| WS-9 | Zooming out far enough to reach the Low detail tier falls back to the procedural weapon and shield without flicker at the boundary |
| WS-10 | Faction colours still read on sprite weapons and shields, and the hit pulse and dead-state fade still land on them |
| WS-11 | A leftward swing is inspected specifically, and the wrong-edge artefact recorded in design section 15 is judged acceptable or not |

## What was run

To be filled in by the integrating session with the verbatim gate output from
task 25. Leave empty rather than summarised until then.
