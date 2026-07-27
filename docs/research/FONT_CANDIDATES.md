# Font Candidates

Research notes for the typeface used by the Hukbo user interface. Compiled
2026-07-27. Companion to `docs/archives/2026-07-27-font-text-quality-design.md`,
which covers the technical side of text rendering — the size ramp, the sampler
state, and whole-pixel positioning — independently of which typeface is chosen.

**Licensing status in this document is research, not legal clearance.** Prices
and tiers were read off public pages on the date above and can change. Confirm
directly with the foundry before shipping anything.

## Why this document exists

The game is about the pre-colonial and early-contact Philippines. A typeface
designed by a Filipino type designer, drawing on Philippine letterforms, is a
better fit for it than a typeface picked purely on legibility metrics — and the
designer deserves a credit in the product.

The technical work does not depend on this choice. The size ramp bakes a set of
`.spritefont` descriptors at fixed pixel sizes; swapping the typeface is a
`<FontName>` change in each descriptor plus a retune of the ramp's pixel values
against the new glyph metrics.

## Jad Maza

The strongest source of candidates. A type designer from Iloilo whose practice
is specifically about Latin and Baybayin typefaces in a Filipino context.
Branding designer at And A Half, Pasig City. Completed the Type West
postgraduate certificate at Letterform Archive, San Francisco, in 2022.

| | |
| --- | --- |
| Website | https://jadmaza.com |
| Email | kumusta@jadmaza.com |
| Instagram | [@jad.otf](https://instagram.com/jad.otf) |
| Behance | https://www.behance.net/jadmaza |
| LinkedIn | https://www.linkedin.com/in/jadmaza |
| Studio | And A Half, Pasig City, Philippines |
| Fonts In Use | https://fontsinuse.com/type_designers/8396/jad-maza |
| EULA | https://jadmaza.com/eula |

He states a philosophy of "simple licensing, fair pricing, and occasional free
fonts." Paid families are roughly $10–$40. An 80% student discount applies to
the Blaze Type releases.

### Dulungan — unavailable

The originally requested typeface. **Not released and not obtainable.**

Dulungan was developed at TypeWest Online at Letterform Archive under Juan
Villanueva, Gen Ramírez, and Lynne Yun, and received a merit award in the
[2024 Gerard Unger Scholarship](https://www.type-together.com/2024-gerard-unger-scholarship-results).
It does not appear in his published catalog on jadmaza.com, Future Fonts, or
Blaze Type.

Search results for the name mostly return **Dukungan**, an unrelated techno
display face by Adien Gunarta. Free-font aggregator sites offering a
"Dulungan" download are serving either that unrelated font or an unlicensed
copy. Neither is usable.

Worth an email to ask whether release is planned. The name is the Ilonggo word
for the Visayan wrinkled hornbill, a critically endangered bird endemic to
Panay — thematically apt for this project.

### Released catalog

| Typeface | Year | Styles | Price | Character | Suitability |
| --- | --- | --- | --- | --- | --- |
| **Bantayog** | 2020 | Light, Semilight, Regular | **From $0** | Rough all-caps sans based on characters repainted over worn cast-iron text on Philippine historical markers. Latin + Baybayin. 223 glyphs. | **Strong candidate for headers and the wordmark.** All-caps suits panel headers, which are already uppercase literals. The commemorative-marker origin is directly on theme. |
| **Bantayog Sans** | 2025 | 7 weights + italics | From $30 | Rounded, polished workhorse sans. Double-storey `a`, slab serifs on `I`, circled figures. Latin + Baybayin, 223 OpenType features. | **Strongest candidate overall.** Seven weights could carry body and display roles from one family. Designed as a text face, so it should survive small sizes. |
| **Bantayog Sharp** | 2025 | Variable | — | Sharp-terminal companion to Bantayog Sans. | Companion display option. |
| **Maragsâ** | 2020, updated 2026 | Medium, Narrow, Wide | From $40/style, **free tier reported** | Flared serif with sharp edges, hastily-flowing strokes, abrupt cuts. Named for the *maragsâ* stress pattern — simultaneous stress and glottal stop on the final syllable — and drawn from the *pakupyâ* accent mark. Filipino localized forms. | Display and headline use. Distinctive but a flared serif is risky at 12px. Pricing needs verification: the typeface page says from $40 per style while the site index lists it among free fonts. |
| **Amakan** | 2024 | 3 | **From $0** | Decorative face translating the diagonal, zigzag, and diamond weave of split-bamboo *amakan* wall cladding on traditional nipà huts into letterforms. Related to Nipa, Buri, Piña. | Decorative only. Unusable for 12–17px interface text. Possible accent use on a title screen. |
| **Makahiya** | 2023 | Variable | — | Named for the touch-sensitive *makahiya* plant. | Unassessed. |
| **Kawingan** | 2020/2021 | 3 | — | — | Unassessed. |
| **Ulalong** | 2021 | 2 | — | — | Unassessed. |

### Blaze Type releases

Published through [Blaze Type](https://blazetype.eu), an independent French
foundry founded 2016. Trial versions available; 80% student discount.

| Typeface | Year | Styles | Character | Suitability |
| --- | --- | --- | --- | --- |
| **Balete** | 2024 | 18 styles + variable, Thin–Black, Roman + Italic | Display face drawn from the balete tree, a hemiepiphyte held in folklore to house supernatural dwellers. Creeping, slender terminals emerging from twisting stems; creates "the illusion of fluid calligraphy without the presence of a single curved line." Latin, European, Vietnamese — 100+ languages. | Display only, explicitly not for extended body text. Would suit a title screen or a match-summary headline. The folkloric grounding fits the setting. Eighteen styles is far more than this project needs. |
| **Taklobo** | 2022 | 1 | Heavy unicase display serif with no ascenders, drawn from the giant clams off the Philippine coast and their vulnerable conservation status. Dense and majestic; "hard on the outside but soft on the inside." Latin, Cyrillic, Greek, Hiragana, Katakana. | Display only, and unicase — no case distinction at all. Very strong character for a wordmark; unusable for anything else. |

## Currently vendored — free, in use as placeholder

Both are already in `src/Hukbo.Client/Content/Fonts/`, taken from
`github.com/google/fonts` at commit `7ff85c87f93ea6cca5f41c69f2e4edcb90240f26`.
Neither is Filipino-designed; both were chosen on legibility grounds alone and
are placeholders.

| Typeface | Designer | License | Role | Notes |
| --- | --- | --- | --- | --- |
| **Rajdhani SemiBold** | Indian Type Foundry | SIL OFL 1.1 | Body, labels, numerals | Narrow, squared, technical. Unusually unambiguous numerals at small sizes. 381 KB — ships full Devanagari coverage, though only ASCII is baked. |
| **Bebas Neue Regular** | Dharma Type | SIL OFL 1.1 | Headers, wordmark | Capitals only by construction. 60 KB. |

## Other free Filipino-adjacent options, not yet assessed

Collected for completeness; none verified for license or quality.

- **Baybayin Neue** — John Abila, https://johnabila.gumroad.com/l/Baybayin-Neue
- [Luc Devroye's Filipino fonts index](https://luc.devroye.org/filipino.html) —
  the most comprehensive catalog of Filipino type designers and their releases
- [Type63](https://type63.carrd.co/) — Filipino type collective
- [CreatePhilippines: 5 Pinoy culture inspired fonts](https://www.createphilippines.com/article/5-pinoy-culture-inspired-fonts-to-download-now-151)
- [Canva: 20 free Filipino fonts](https://www.canva.com/learn/20-free-filipino-fonts/) —
  aggregator listing, licenses need individual verification

## Open licensing question — applies to every paid candidate

The Jad Maza EULA permits embedding: "You may embed the Fonts in static
documents (such as PDFs), in eBooks, digital products, installable
applications, broadcast, and other media produced by your organization." That
covers shipping a font inside the game.

It does **not** explicitly address rasterizing glyphs into a bitmap texture
atlas, and it restricts derivative works without written permission. A MonoGame
`SpriteFont` bake does exactly that: the content pipeline rasterizes each glyph
with FreeType at build time into a texture, and the compiled asset that ships
contains bitmaps rather than outlines.

This is routine for every game engine and is almost certainly intended to be
covered by the embedding clause, but it is ambiguous enough to confirm in
writing rather than assume. The same question applies to the Blaze Type EULA
for Balete and Taklobo, which has not been read yet.

Demo and trial versions are testing and personal use only. They cannot ship.

## Suggested next steps

1. Email kumusta@jadmaza.com. Ask three things: whether the commercial license
   covers rasterizing glyphs into a texture atlas shipped inside a game;
   whether Dulungan is planned for release; and how he would like to be
   credited. The last question is worth asking regardless of which typeface is
   chosen.
2. Try **Bantayog** first — it is free-tier, all-caps, and drawn from Philippine
   historical markers, which makes it a direct fit for panel headers and the
   wordmark with no purchase required.
3. Evaluate **Bantayog Sans** for body text. At $30 it is the cheapest path to a
   single-family solution, and being a workhorse text face it is the only
   candidate here likely to hold up at 12px.
4. Verify the Maragsâ pricing contradiction before assuming a free tier exists.
5. Whichever is chosen, add a credit line to the in-game menu or an about
   screen, and record the license in
   `src/Hukbo.Client/Content/Fonts/README.md` and
   `docs/dependency-risk-register.md`.
