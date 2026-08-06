# Vendored fonts

Both typefaces in this directory are vendored, not installed from the host
system. Vendoring closes the licensing exposure previously recorded at
`docs/dependency-risk-register.md` for a machine-installed Arial, and gives the
game a font asset it actually owns.

## Provenance

| File | Upstream path | Family | Style |
| --- | --- | --- | --- |
| `Rajdhani-SemiBold.ttf` | `ofl/rajdhani/Rajdhani-SemiBold.ttf` | Rajdhani | SemiBold |
| `BebasNeue-Regular.ttf` | `ofl/bebasneue/BebasNeue-Regular.ttf` | Bebas Neue | Regular |

Both files were retrieved from `github.com/google/fonts` at commit
`7ff85c87f93ea6cca5f41c69f2e4edcb90240f26`, on 2026-07-27.

## License

Both faces are licensed under the SIL Open Font License, Version 1.1. The full
license text for each family is vendored alongside the font file:

- `OFL-Rajdhani.txt`
- `OFL-BebasNeue.txt`

See `docs/archives/2026-07-27/2026-07-27-font-text-quality-design.md` §12 (risk register,
item R12) for the compliance rationale: the license permits bundling and
embedding, and baking either face into a texture atlas for the content pipeline
is a permitted use. Neither face is modified or renamed, so the reserved font
name clause of the OFL does not apply.

## Which rung uses which face

Rajdhani SemiBold carries every rung with mixed-case content: body text,
labels, and numerals. Bebas Neue Regular, which is capitals-only, carries panel
headers and the menu wordmark, where every routed string is already an
all-capitals literal in the source.

| Rung | Baked pixels | Face | Asset identifier |
| --- | --- | --- | --- |
| `Caption` | 12 | Rajdhani SemiBold | `Fonts/UiCaption` |
| `Body` | 14 | Rajdhani SemiBold | `Fonts/UiBody` |
| `Label` | 17 | Rajdhani SemiBold | `Fonts/UiLabel` |
| `Subtitle` | 20 | Rajdhani SemiBold | `Fonts/UiSubtitle` |
| `Title` | 22 | Bebas Neue Regular | `Fonts/UiTitle` |
| `Display` | 38 | Bebas Neue Regular | `Fonts/UiDisplay` |

The full rationale for the typeface choice, the SemiBold-over-Regular
decision, and the size ramp lives in
`docs/archives/2026-07-27/2026-07-27-font-text-quality-design.md`, sections 3
and 4.
