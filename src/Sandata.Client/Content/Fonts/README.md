# Vendored font

This directory vendors one typeface, not installed from the host system.
Vendoring closes the same licensing exposure Hukbo's own font folder
(`src/Hukbo.Client/Content/Fonts/README.md`) records for a machine-installed
Arial, and gives Sandata a font asset it actually owns rather than a reference
into `Hukbo.Client`'s copy — `Hukbo.Client` is Hukbo's own shell and Sandata
may not reference it (`CLAUDE.md` section 3, section 9).

## Provenance

| File | Upstream path | Family | Style |
| --- | --- | --- | --- |
| `Rajdhani-SemiBold.ttf` | `ofl/rajdhani/Rajdhani-SemiBold.ttf` | Rajdhani | SemiBold |

Retrieved from `github.com/google/fonts` at commit
`7ff85c87f93ea6cca5f41c69f2e4edcb90240f26`, on 2026-07-27 — the same commit
Hukbo's copy was retrieved from, since both are the same upstream file taken at
the same time.

## License

Rajdhani is licensed under the SIL Open Font License, Version 1.1. The full
license text is vendored alongside the font file as `OFL-Rajdhani.txt`. The
license permits bundling and embedding, and baking the face into a texture
atlas for the content pipeline is a permitted use. The face is not modified or
renamed, so the reserved font name clause of the OFL does not apply.

## Why one face, not two

Hukbo carries a second face, Bebas Neue, for capitals-only panel headers and
its menu wordmark. Sandata's HUD has no such element yet: every row and header
string in `src/Sandata.Client/UI` (`OperatorInspector`, `ContactList`,
`GoCodePanel`, `OrderQueueView`, and the rest) is ordinary mixed-case content —
operator names, coordinates, reason codes, order ids — not a branded display
headline. Rajdhani SemiBold, which Hukbo already uses for every mixed-case
rung, carries both of Sandata's roles. A second face is a real content-pipeline
cost (one more `.ttf`, one more license file, more atlases) that nothing in
Sandata's HUD currently asks for; adding Bebas Neue back in is a one-line
change to `SandataFontRamp.GetFontFileName` plus a new descriptor and `.mgcb`
block, the day a display-scale all-caps element actually ships.

## Which rung uses which size

`SandataFontRamp` (`src/Sandata.Client/Theming/SandataFontRamp.cs`) is the
source of truth; this table exists so a reader does not have to open the code
to see the shape of the decision.

| Role | Baked pixels | Face | Asset identifier | Matches |
| --- | --- | --- | --- | --- |
| `Body` | 14 | Rajdhani SemiBold | `Fonts/SandataBody` | `OperatorInspector.LineHeight` (18px) and `OrderQueueView.RowHeight` (18px) |
| `Label` | 17 | Rajdhani SemiBold | `Fonts/SandataLabel` | `ContactList.HeaderHeight` (24px), `GoCodePanel.HeaderHeight` / `OrderQueueView.HeaderHeight` (20px) |

Both sizes are the same pixel values Hukbo's own `UiFontRamp` bakes for its
`Body` and `Label` rungs — not copied blindly, but because they land inside
Sandata's own already-built row and header heights with the same margin Hukbo
found comfortable, and there was no reason to pick different numbers than a
value already proven to read well at this game's UI scale.
