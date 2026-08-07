# Historically Bounded Elite UI Visual Directions

**Status:** Direction A selected and implemented as a provisional reconstruction

**Date:** 2026-07-31

## Terminology and evidence boundary

"Filipino royal design" is too broad for a single historically defensible
visual language. Communities in the archipelago had different polities,
materials, dress, rank terms, and contact histories. This packet therefore uses
**chiefly/elite court** and always attaches a region and date.

Do not use *maharlika* as a generic synonym for royalty. The repository's rank
research does not support that meaning.

Evidence labels in this document follow Hukbo's historical-accuracy contract:

- **Documented:** directly supported by an identified source.
- **Documented, form uncertain:** the material or color class is supported, but
  its exact visual treatment is not recoverable.
- **Provisional reconstruction:** a game-art synthesis that is compatible with
  the evidence but is not itself documented.

## Shared documented material

- **Documented:** Pigafetta's 1521 account describes the queen at Cebu wearing
  black-and-white cloth, a silk scarf with gold stripes, and sitting on a
  silk-embroidered cushion.
- **Documented:** the same account describes a Butuan/Calagan ruler with a silk
  head covering, large gold earrings, silk-embroidered cotton clothing, and a
  gold-hafted dagger in a carved-wood scabbard.
- **Documented:** gold personal ornament and silk-embroidered cotton appear in
  descriptions of chiefs.
- **Documented:** the late-1500s Boxer Codex uses tempera and gold leaf and
  depicts Philippine groups in saturated clothing colors. Its page borders
  belong to a colonial, cross-cultural manuscript.
- **Documented:** Baybayin is attested in the 16th century.
- **Documented, form uncertain:** surviving sources support color classes such
  as black, white, red, purple, orange, pink, blue, and gold, but not modern
  digital hex values or one universal elite palette.
- **Documented, form uncertain:** woven cloth, embroidery, carved wood, gold,
  bamboo, and other organic materials are supported; exact repeat patterns for
  UI borders generally are not.

Primary and institutional references:

- [Pigafetta, *The Philippine Islands, 1493–1803*, volume 33](https://www.gutenberg.org/files/42884/42884-h/42884-h.htm)
- [Cultural Center of the Philippines: Boxer Codex](https://epa.culturalcenter.gov.ph/3/82/2152/)
- [University of Michigan: Boxer Codex exhibit](https://apps.lib.umich.edu/online-exhibits/items/show/9337)
- [National Museum of the Philippines: Baybayin](https://www.nationalmuseum.gov.ph/exhibitions/anthropology/baybayin/)
- [Library of Congress: *Doctrina Christiana*](https://www.loc.gov/item/48031307/)
- [National Museum of the Philippines: Ornaments](https://www.nationalmuseum.gov.ph/our-collections/archaeology/ornaments/)

Repository research that constrains interpretation:

- [`HISTORICAL_1500s_RANKS.md`](../../research/HISTORICAL_1500s_RANKS.md)
- [`warrior-appearance-historical-research.md`](../../research/improve-visuals/warrior-appearance-historical-research.md)

## Direction A — Cebu 1521 chiefly court

**Recommendation:** use this as the first historical UI theme.

### Basis

- **Documented:** black-and-white cloth, silk, embroidery, gold striping, and
  elite presentation are present in the Cebu account.
- **Documented, form uncertain:** those materials do not establish a complete
  screen palette, border system, or universal Cebu court style.
- **Provisional reconstruction:** translate them into dark wood/black-brown
  panels, warm cloth-colored text, restrained gold focus and active states, and
  rare deep-red emphasis.

### Character

Quiet, high-contrast, material, and ceremonial. Gold should identify focus,
selection, and special status, not cover every surface. Cloth and wood texture
should be subtle enough that text contrast remains measurable.

## Direction B — Manila, circa 1590 illuminated manuscript

### Basis

- **Documented:** Tagalog figures in the Boxer Codex use bright red, purple,
  and gold; the manuscript uses colorful borders and gold leaf.
- **Documented, form uncertain:** the manuscript's borders are not proof of an
  indigenous Tagalog UI ornament system.
- **Provisional reconstruction:** warm paper, dark ink, red/purple/indigo
  accents, and restrained gold-leaf highlights.

### Character

More colorful and manuscript-like than Direction A. It is visually distinctive
but carries a larger interpretation risk: it must be described as
Boxer-Codex-inspired presentation, never as a recovered indigenous border
design.

## Direction C — Butuan/Calagan gold and carved wood

### Basis

- **Documented:** silk, embroidered cotton, gold ornament, a gold-hafted dagger,
  and a carved-wood scabbard appear in Pigafetta's description.
- **Documented, form uncertain:** the source does not specify an interface
  pattern, exact carving language, or digital palette.
- **Provisional reconstruction:** charred wood, cream cloth, indigo, aged gold,
  and fine carved or embroidered edge treatments.

### Character

The richest material direction, suitable if Hukbo wants a gold-and-wood visual
identity. It requires the most original art and the strictest review against
unsupported motifs.

## Provisional palette for Direction A

Every hex value below is a **Provisional reconstruction**. It is a
contrast-feasible starting point for theme-token prototyping, not a historical
claim.

| Semantic use | Candidate |
|---|---|
| Canvas | `#120D0A` |
| Arena | `#4A5138` |
| Arena border | `#C3A35A` |
| Panel | `#251914` |
| Alternate panel | `#32231C` |
| Status surface | `#1D1410` |
| Primary text | `#F3E6C8` |
| Secondary text | `#D9C7A7` |
| Disabled text | `#C7B79E` |
| Inverse text | `#1A110D` |
| Border | `#B58A3D` |
| Default action / selection | `#D0A64A` |
| Hover action | `#E2BE68` |
| Pressed action | `#B88939` |
| Active action | `#A7B58C` |
| Disabled action | `#493A31` |
| Focus / warning | `#E3BC62` |
| Information | `#84AFC2` |
| Success | `#91B27A` |
| Danger | `#E67864` |
| Team A | `#76B8DA` |
| Team B | `#EF7768` |
| Other | `#E3BC62` |
| New event | `#F2CD75` |
| Overlay scrim | `#120D0AE6` |

Every palette value, including the overlay scrim, remains a **Provisional
reconstruction**. The implemented catalog passes all 27 raw contrast-pair
checks. Rendered text, opacity, hover/pressed/focus states, team distinction,
and color-vision use still require the manual visual validation recorded in
`docs/development/testing.md`.

The arena and combatant colors should remain semantically legible. A UI-theme
choice must not turn a historical material accent into a new combat identity or
change simulation behavior.

## Typography and ornament rules

- Keep the current readable Latin type ramp for controls and data.
- Use Baybayin only for a verified, region-appropriate word with an explicit
  transliteration and source review. Do not substitute glyph-like decoration
  or invented text.
- Use small material cues—woven grain, embroidery-like line weight, carved
  edge depth, gold glints—rather than covering panels in motifs.
- If a geometric repeat is created, label it **Provisional reconstruction** and
  do not name it after a culture unless a source supports that identification.
- Keep texture contrast low behind text and provide a flat high-contrast mode.

## Explicit exclusions

- modern Philippine flag sun, stars, eagle, or national color symbolism;
- a universal red "royal" color;
- *maharlika* presented as royalty;
- generic okir, sarimanok, bakunawa, or tattoo motifs detached from region and
  source;
- funerary gold masks or grave goods turned into everyday palace decoration;
- the red velvet chair reported as a European gift;
- gibberish or decorative pseudo-Baybayin;
- treating Boxer Codex borders as documented indigenous ornament.

## Selection record

The user approved implementation on 2026-07-31 and directed the project to
choose the most historically accurate available option:

1. **Region/date:** Cebu, 1521.
2. **Default:** new-user default; existing stable theme selections remain
   unchanged.
3. **Allowed cues:** the flat semantic palette derived above from documented
   material/color classes.
4. **Prohibited motifs:** every item in the explicit exclusions above.
5. **Review boundary:** the current theme is visibly labelled **Provisional
   reconstruction**. A specialist review is still required before adding any
   culturally identified motif, script, texture, or claim beyond this bounded
   flat palette.
