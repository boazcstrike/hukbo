# Pawn sprite body — plan

**Archived: reference only.** This is a finished plan. All fourteen tasks were
built, tested, and merged to `main` in the feature commit `21e1abb`, and the
eight `SB` smoke rows it owed were run and passed by a person at an interactive
desktop on 2026-08-15. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this project
remains `CLAUDE.md`, `SIMULATION-GAME-STANDARDS.md`,
`docs/development/testing.md`, and `docs/development/smoke-checklist.md`. The
record of those eight rows is the archive titled "Pawn sprite body smoke",
alongside this file.

One thing survives the build and is not closed by it: the `B` toggle has no
on-screen announcement, because the menu panel is full. The design's section 9
records that as an open discoverability gap.

Date: 2026-08-15
Design: `2026-08-15-pawn-sprite-body-design.md` (binding; this plan does not
override it)
Game: Hukbo only

## Context

Hukbo's warriors are drawn entirely from flat untextured quads. That is what
made gait animation, the death collapse, swing poses, and ranged draw tension
cheap to build, and it is also a ceiling: a flat quad torso cannot carry a
face, a garment, a tattoo, or the value structure that makes a warrior read as a
person rather than a marker.

This package adds an authored body sprite for the head and torso, selected per
warrior from a catalog of fifty variants, behind a setting that defaults off.
It deliberately leaves the legs, arms, weapon, and shield procedural, because
those are gait-animated or directional and a single authored cell cannot carry
either. The design's section 3 has the reasoning and the part-by-part table.

## Task list

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| 1 | Generate fifty body cells and pack the atlas | `tools/gen_pawn_bodies.py`, `src/Hukbo.Client/Content/Textures/PawnBodies.png` | Atlas is 1120×1170, ten columns by five rows of 112×234 | — | `magick identify` reports the expected dimensions |
| 2 | Add the atlas to the content pipeline | `src/Hukbo.Client/Content/Content.mgcb` | Entry 26 built, parameters identical to `Textures/UiChrome.png` | 1 | `PawnBodies.xnb` appears under `Content/bin/DesktopGL/Content/Textures/` |
| 3 | Update the pinned content-pipeline lists and record the decision | `tests/Hukbo.Client.Tests/SourceHygieneTests.cs` | Both pinned arrays carry the new entry; doc comment records the decision and restates that a twenty-seventh entry still needs its own | 2 | `SourceHygieneTests` green |
| 4 | Add the settings enum and store handling | `src/Hukbo.Client/Settings/PawnVisualStyle.cs`, `ClientSettings.cs`, `ClientSettingsStore.cs` | Schema 12; a version 11 file still loads with the field defaulted | — | Task 9 |
| 5 | Add the variant-selection salt | `src/Hukbo.Client/Presentation/PresentationSalts.cs` | `PawnSpriteVariantSalt` declared and registered in `All` | — | Task 8 |
| 6 | Add the atlas geometry and selection type | `src/Hukbo.Client/Rendering/PawnSpriteAtlas.cs` | Cell bounds, variant selection, destination fitting, all pure | 5 | `PawnSpriteAtlasTests` |
| 7 | Draw the sprite in place of torso, head, and head treatment | `src/Hukbo.Client/Rendering/PawnRenderer.cs` | Exactly those three draws are replaced; every other draw and its order is untouched; the collapse transform carries the sprite | 6 | Task 10 |
| 8 | Repair the salt registry count pins | `tests/Hukbo.Client.Tests/PresentationSaltsTests.cs`, `VisualCatalogContractTests.cs` | Count assertions read fifteen; the new salt is covered like its siblings | 5 | Both suites green |
| 9 | Repair and extend the settings tests | `tests/Hukbo.Client.Tests/ClientSettingsStoreTests.cs` | Schema pin at 12; round trip, defaulting, out-of-range reset, and version 11 load all covered for the new field | 4 | Suite green |
| 10 | Extract the sprite gate to a pure predicate and test it | `src/Hukbo.Client/Rendering/PawnRenderer.cs`, `tests/Hukbo.Client.Tests/PawnSpriteBodyGateTests.cs` | Truth table covered without a graphics device | 7 | Suite green |
| 11 | Load the atlas and thread the style through the draw path | `src/Hukbo.Client/ArenaGame.cs`, `ArenaGame.Rendering.cs` | Atlas loaded once at startup; every pawn draw receives the style, the atlas, and its entity identifier | 4, 6, 7 | Task 13 |
| 12 | Add the live toggle | `src/Hukbo.Client/Presentation/ClientCommand.cs`, `src/Hukbo.Client/ArenaGame.cs` | `B` flips the style on the next frame and persists it | 11 | Task 13 |
| 13 | Run the canonical gate | — | `./scripts/verify.ps1` output recorded verbatim | 1–12 | The gate itself |
| 14 | Add smoke rows | `docs/development/smoke-checklist.md` | Rows added as `PENDING` | 13 | A human at an interactive desktop |

## What this plan deliberately does not do

- It does not give the mode a discoverable control. The menu panel is
  measurably full — design section 8 carries the arithmetic — so the toggle is
  a shortcut key and the feature is recorded as incomplete against
  `SIMULATION-GAME-STANDARDS.md` §10. Making room in the menu is its own
  design.
- It does not touch `Hukbo.Core`, any hash, any snapshot, or any outcome. Every
  file above is presentation.
- It does not touch Sandata.

## Verification

The canonical gate, run once after integration and never delegated:

```powershell
./scripts/verify.ps1 -Game Hukbo
```

Both suites matter here, not just one: a `ClientCommand` addition and a
`PawnRenderer` change can redden the Client suite while Core stays green.

Interactive behaviour is proven only by the manual checklist. Compilation, the
unit suites, and a window-opening probe do not let any row be flipped to
`PASS`.

## Smoke rows this package owes

| Row | What to check |
| --- | --- |
| SB-1 | Pressing `B` in a live battle switches every warrior's body on the next frame, and pressing it again switches back |
| SB-2 | The chosen style survives a restart |
| SB-3 | The two factions remain tellable apart at gameplay zoom in sprite mode |
| SB-4 | Warriors do not all share one body — visible variety across a full field |
| SB-5 | A dying warrior's sprite body rotates with the collapse rather than staying upright |
| SB-6 | Legs still animate under a walking warrior in sprite mode |
| SB-7 | The weapon arm still points at the target in sprite mode |
| SB-8 | Zooming out far enough to reach the Low detail tier falls back to the procedural body without flicker |
