"""Parametric Hukbo pawn sprite generator.

Emits deterministic layered SVG pawns in the Battle Brothers paperdoll shape:
one fixed draw order, gear and body parts swapped inside named layer groups.
Every visual trait derives from a single integer seed, so a pawn's appearance
is reproducible from its entity id.
"""

import random
import sys
from pathlib import Path

OUT = Path(__file__).parent / "variants"

# (base, shadow, highlight)
SKINS = [
    ("#c49c6d", "#9d784b", "#d8bb92"),
    ("#b0854f", "#8a6338", "#c8a375"),
    ("#a3743f", "#7d5530", "#bb9160"),
    ("#96693e", "#6f4a2c", "#ad7f52"),
    ("#82562f", "#5f3a1e", "#9c7047"),
    ("#6b451f", "#4b2f14", "#875c35"),
]

HAIRS = ["#1d1710", "#241b12", "#2e2318", "#151110", "#3a2c1d"]
BANDS = ["#8f3a2c", "#7a6a2e", "#2f4a52", "#6b3a5e", "#3f4a2c", "#8a6d43", "#a8853f"]
CLOTHS = ["#8c3a2c", "#6b5a2e", "#3f4a52", "#5e3a2c", "#7a3a5e", "#4a5a3a", "#6d4326"]
PAINTS = ["#c9c3b4", "#8f3a2c", "#2f3f4a", "#a8853f"]

JAWS = {
    "narrow": "M250 128 L256 176 L272 190 L288 176 L292 132 L278 112 L258 112 Z",
    "oval":   "M246 128 L252 178 L272 192 L292 178 L296 132 L280 110 L254 110 Z",
    "square": "M244 126 L248 180 L272 195 L296 180 L298 130 L282 107 L252 107 Z",
    "heavy":  "M243 130 L250 182 L272 198 L298 182 L300 134 L283 108 L252 108 Z",
}


def hair_back(kind, col):
    if kind == "long":
        return (f'<path d="M240 118 L246 156 L232 196 L226 208 L248 200 L258 160 L262 120 Z" fill="{col}"/>'
                f'<path d="M296 118 L304 150 L300 186 L288 180 L292 140 Z" fill="{col}" opacity="0.85"/>')
    if kind == "topknot":
        return (f'<ellipse cx="268" cy="86" rx="20" ry="16" fill="{col}"/>'
                f'<path d="M262 96 L278 94 L282 108 L258 108 Z" fill="{col}"/>')
    if kind == "tail":
        return f'<path d="M240 122 L232 160 L222 194 L240 190 L252 152 L256 124 Z" fill="{col}"/>'
    return ""  # crop


def hair_front(kind, col):
    # Hairline must stop above the brow (y=133) or the fringe reads as a blindfold.
    if kind == "crop":
        return f'<path d="M243 116 L301 112 L295 96 L270 85 L247 95 Z" fill="{col}"/>'
    if kind == "topknot":
        return f'<path d="M245 114 L301 110 L293 96 L268 89 L249 97 Z" fill="{col}"/>'
    return f'<path d="M242 117 L303 112 L296 93 L270 83 L247 94 Z" fill="{col}"/>'


def face(rng, skin, jaw_key):
    base, shade, hi = skin
    eye_x = 288 if jaw_key in ("narrow", "oval") else 291
    parts = [
        f'<path d="{JAWS[jaw_key]}" fill="{base}"/>',
        f'<path d="M252 168 L268 188 L292 180 L294 164 Z" fill="{shade}" opacity="0.55"/>',
        f'<path d="M276 118 L296 116 L298 132 L278 134 Z" fill="{hi}" opacity="0.45"/>',
    ]
    # brow
    brow = rng.choice(["flat", "angry", "raised"])
    if brow == "flat":
        parts.append(f'<path d="M280 136 L298 133 L298 139 L280 142 Z" fill="{shade}" opacity="0.85"/>')
    elif brow == "angry":
        parts.append(f'<path d="M279 132 L298 138 L297 144 L278 138 Z" fill="{shade}" opacity="0.9"/>')
    else:
        parts.append(f'<path d="M280 138 L298 130 L299 136 L281 144 Z" fill="{shade}" opacity="0.8"/>')
    parts.append(f'<ellipse cx="{eye_x}" cy="147" rx="4.2" ry="3.4" fill="#171310"/>')
    # nose
    parts.append(f'<path d="M292 150 L300 158 L292 162 Z" fill="{shade}" opacity="0.7"/>')
    # mouth
    parts.append(f'<path d="M280 168 L294 166 L293 171 L281 172 Z" fill="#4a2b1c" opacity="0.75"/>')
    # facial hair
    fh = rng.choice(["none", "none", "moustache", "beard", "chinstrap"])
    hcol = rng.choice(HAIRS)
    if fh == "moustache":
        parts.append(f'<path d="M278 162 L297 159 L297 166 L279 168 Z" fill="{hcol}" opacity="0.9"/>')
    elif fh == "beard":
        parts.append(f'<path d="M263 170 L295 164 L296 177 L281 189 L267 182 Z" fill="{hcol}" opacity="0.85"/>')
    elif fh == "chinstrap":
        parts.append(f'<path d="M254 158 L262 182 L284 190 L286 180 L266 172 L260 156 Z" fill="{hcol}" opacity="0.9"/>')
    # war paint
    if rng.random() < 0.34:
        pc = rng.choice(PAINTS)
        style = rng.choice(["band", "bars", "cheek"])
        if style == "band":
            # across the nose and cheekbone, clear of the eye at y=147
            parts.append(f'<path d="M252 154 L300 149 L301 163 L253 169 Z" fill="{pc}" opacity="0.5"/>')
        elif style == "bars":
            parts.append(f'<path d="M276 130 L282 129 L284 178 L278 179 Z" fill="{pc}" opacity="0.5"/>'
                         f'<path d="M288 128 L294 127 L296 174 L290 175 Z" fill="{pc}" opacity="0.5"/>')
        else:
            parts.append(f'<path d="M272 152 L292 148 L293 160 L273 164 Z" fill="{pc}" opacity="0.45"/>')
    # scar
    if rng.random() < 0.22:
        parts.append(f'<path d="M284 124 L290 122 L294 166 L288 168 Z" fill="#c9a184" opacity="0.4"/>')
    # ear ornament
    if rng.random() < 0.3:
        parts.append(f'<circle cx="262" cy="160" r="4" fill="#a8853f"/>')
    return "\n    ".join(parts)


def tattoos(rng, level):
    if level == 0:
        return ""
    rows = [
        '<path d="M240 200 L236 246 L244 292" />',
        '<path d="M250 196 L246 244 L254 290" />',
        '<path d="M298 194 L304 240 L298 288" />',
        '<path d="M258 214 L292 210" />',
        '<path d="M258 236 L294 232" />',
        '<path d="M260 262 L294 258" />',
    ]
    take = {1: 2, 2: 4, 3: 6}[level]
    body = "".join(rows[:take])
    return (f'<g stroke="#2b1d13" stroke-width="3" fill="none" opacity="0.5">{body}</g>')


def spear_loadout(rng, skin, cloth):
    base, shade, hi = skin
    wood = rng.choice(["#6d5133", "#5b452c", "#7a5c3a"])
    return {
        "arm_far": f'''<path d="M238 198 L214 190 L204 172 L220 164 L234 182 L252 190 Z" fill="{shade}"/>
    <path d="M204 172 L220 164 L214 146 L198 150 Z" fill="{base}"/>
    <ellipse cx="205" cy="151" rx="14" ry="12" fill="{hi}"/>''',
        "weapon": f'''<path d="M150 168 L444 96 L448 110 L154 182 Z" fill="{wood}" stroke="#2a1e12" stroke-width="2" stroke-opacity="0.6"/>
    <path d="M410 104 L440 98 L444 112 L414 118 Z" fill="#8a6d43"/>
    <path d="M444 96 L500 82 L506 96 L448 110 Z" fill="url(#gIron)" stroke="#242a30" stroke-width="1.6"/>
    <path d="M448 84 L502 80 L500 90 L446 94 Z" fill="#c3cad2" opacity="0.55"/>
    <path d="M152 176 L136 180 L134 168 L150 166 Z" fill="#8a6d43"/>''',
        "offhand": f'''<path d="M300 196 L376 206 L386 316 L370 424 L306 414 L294 306 Z" fill="url(#gWood)" stroke="#231a10" stroke-width="3" stroke-opacity="0.7"/>
    <path d="M316 200 L322 418" stroke="#41301d" stroke-width="4" fill="none" opacity="0.8"/>
    <path d="M340 203 L346 421" stroke="#41301d" stroke-width="4" fill="none" opacity="0.8"/>
    <path d="M362 205 L368 423" stroke="#41301d" stroke-width="4" fill="none" opacity="0.8"/>
    <path d="M296 244 L384 254" stroke="#96784a" stroke-width="7" fill="none" opacity="0.9"/>
    <path d="M298 368 L382 376" stroke="#96784a" stroke-width="7" fill="none" opacity="0.9"/>
    <ellipse cx="340" cy="310" rx="20" ry="24" fill="#5d4529" stroke="#2b2014" stroke-width="2.5"/>
    <path d="M300 200 L312 200 L322 412 L308 410 Z" fill="#ffffff" opacity="0.07"/>
    <path d="M366 208 L384 212 L374 420 L360 418 Z" fill="#000000" opacity="0.22"/>''',
        "arm_near": f'''<path d="M292 194 L316 216 L322 268 L302 276 L292 232 L274 206 Z" fill="{base}"/>
    <ellipse cx="311" cy="278" rx="16" ry="14" fill="{hi}"/>''',
    }


def kampilan_loadout(rng, skin, cloth):
    base, shade, hi = skin
    return {
        "arm_far": f'''<path d="M242 202 L226 184 L232 168 L248 172 L254 190 Z" fill="{shade}"/>
    <path d="M232 168 L246 156 L268 172 L258 184 Z" fill="{base}"/>
    <ellipse cx="264" cy="178" rx="13" ry="12" fill="{hi}"/>''',
        "weapon": '''<path d="M245 156 L251 149 L152 36 L136 50 Z" fill="url(#gBlade)" stroke="#20262c" stroke-width="2.2"/>
    <path d="M249 152 L150 40 L145 45 L246 155 Z" fill="#e4eaf1" opacity="0.4"/>
    <path d="M152 36 L168 32 L146 56 L136 50 Z" fill="#c3cad2" opacity="0.55"/>
    <path d="M240 160 L254 146 L268 160 L254 174 Z" fill="#9c7b3a" stroke="#4a3a1a" stroke-width="2"/>
    <path d="M250 174 L268 156 L292 178 L274 196 Z" fill="url(#gGrip)" stroke="#2c2013" stroke-width="2"/>
    <path d="M254 178 L272 196 M262 172 L280 190 M270 166 L288 184" stroke="#3b2b19" stroke-width="2.6" fill="none" opacity="0.85"/>
    <path d="M274 196 L292 178 L312 190 L300 202 L312 216 L292 216 Z" fill="#6a4c2c" stroke="#2c2013" stroke-width="2.2"/>
    <path d="M300 202 L312 190 L316 198 L306 206 Z" fill="#9c7b3a"/>''',
        "offhand": "",
        "arm_near": f'''<path d="M290 188 L310 194 L318 216 L302 224 L288 206 Z" fill="{base}"/>
    <path d="M302 224 L318 214 L300 190 L284 200 Z" fill="{hi}"/>
    <ellipse cx="288" cy="192" rx="14" ry="12" fill="{hi}"/>''',
    }


def blood(rng):
    if rng.random() < 0.35:
        return ('<path d="M256 214 L272 208 L280 232 L262 238 Z" fill="#6d1d18" opacity="0.55"/>'
                '<path d="M286 258 L296 254 L300 274 L288 276 Z" fill="#5a1512" opacity="0.5"/>')
    return ""


# Crop window for a body cell: head top through the bahag waist wrap, wide
# enough for the widest torso (bw 1.16) and the long-hair mass behind the
# shoulders. Arms, legs, weapon and shield are deliberately absent — the game
# keeps drawing those procedurally.
BODY_VIEWBOX = (200, 68, 130, 272)
BODY_CELL = (112, 234)


def build(seed, mode="full"):
    rng = random.Random(seed)
    skin = SKINS[rng.randrange(len(SKINS))]
    base, shade, hi = skin
    hair_col = rng.choice(HAIRS)
    hair_kind = rng.choice(["long", "long", "topknot", "tail", "crop"])
    jaw = rng.choice(list(JAWS))
    cloth = rng.choice(CLOTHS)
    band = rng.choice(BANDS)
    has_band = rng.random() < 0.75
    tat = rng.choice([0, 1, 1, 2, 2, 3])

    bw = round(rng.uniform(0.86, 1.16), 3)      # torso / shoulder width
    lw = round(rng.uniform(0.9, 1.12), 3)       # leg thickness
    hs = round(rng.uniform(0.9, 1.1), 3)        # head scale
    sc = round(rng.uniform(0.93, 1.07), 3)      # overall height
    load = spear_loadout(rng, skin, cloth) if rng.random() < 0.5 else kampilan_loadout(rng, skin, cloth)

    # Putong sits on the forehead between hairline (117) and brow (133).
    band_svg = f'<path d="M242 116 L306 111 L308 127 L244 132 Z" fill="{band}"/>' if has_band else ""

    if mode == "body":
        # Same parameters, same RNG draw order (torso before head), cropped to
        # the head+torso band. No arms, legs, weapon or shield: the renderer
        # keeps drawing all four procedurally, because they are directional or
        # gait-animated and a single authored cell cannot carry either.
        vx, vy, vw, vh = BODY_VIEWBOX
        cw, ch = BODY_CELL
        return f'''<svg xmlns="http://www.w3.org/2000/svg" width="{cw}" height="{ch}" viewBox="{vx} {vy} {vw} {vh}">
  <defs>
    <linearGradient id="gSkin" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="{shade}"/><stop offset="0.42" stop-color="{base}"/><stop offset="1" stop-color="{hi}"/>
    </linearGradient>
  </defs>
  <g id="hair-back" transform="translate(270,150) scale({hs},{hs}) translate(-270,-150)">
    {hair_back(hair_kind, hair_col)}
  </g>
  <g id="torso" transform="translate(266,250) scale({bw},1) translate(-266,-250)" stroke="#241a11" stroke-width="2.5" stroke-opacity="0.55">
    <path d="M234 190 L296 182 L310 236 L302 302 L238 310 L226 246 Z" fill="url(#gSkin)"/>
    <path d="M244 206 L268 202 L272 232 L246 236 Z" fill="{hi}" opacity="0.5" stroke-opacity="0"/>
    <path d="M274 200 L296 196 L300 228 L276 232 Z" fill="{hi}" opacity="0.5" stroke-opacity="0"/>
    <path d="M248 244 L296 238 L298 256 L250 262 Z" fill="{shade}" opacity="0.45" stroke-opacity="0"/>
    <path d="M248 268 L296 262 L298 280 L250 286 Z" fill="{shade}" opacity="0.45" stroke-opacity="0"/>
    <g stroke-opacity="0">{tattoos(rng, tat)}</g>
    <g stroke-opacity="0">{blood(rng)}</g>
    <path d="M230 302 L306 294 L316 324 L296 340 L238 336 L222 318 Z" fill="{cloth}" stroke-opacity="0.6"/>
    <path d="M236 320 L312 312 L310 328 L238 336 Z" fill="#000000" opacity="0.3" stroke-opacity="0"/>
  </g>
  <g id="head" transform="translate(270,150) scale({hs},{hs}) translate(-270,-150)" stroke="#241a11" stroke-width="2.5" stroke-opacity="0.5">
    <path d="M256 170 L290 166 L294 196 L258 200 Z" fill="{shade}"/>
    {face(rng, skin, jaw)}
    <g stroke-opacity="0">{hair_front(hair_kind, hair_col)}{band_svg}</g>
  </g>
</svg>
'''

    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512">
  <defs>
    <filter id="soft" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="7"/></filter>
    <linearGradient id="gSkin" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="{shade}"/><stop offset="0.42" stop-color="{base}"/><stop offset="1" stop-color="{hi}"/>
    </linearGradient>
    <linearGradient id="gWood" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="#4b3722"/><stop offset="0.5" stop-color="#6d5133"/><stop offset="1" stop-color="#523d26"/>
    </linearGradient>
    <linearGradient id="gIron" x1="0" y1="0" x2="0.3" y2="1">
      <stop offset="0" stop-color="#a7aeb6"/><stop offset="0.5" stop-color="#6a7079"/><stop offset="1" stop-color="#3d424a"/>
    </linearGradient>
    <linearGradient id="gBlade" x1="0" y1="1" x2="1" y2="0">
      <stop offset="0" stop-color="#3f454d"/><stop offset="0.45" stop-color="#79808a"/>
      <stop offset="0.72" stop-color="#c6ccd4"/><stop offset="1" stop-color="#868d97"/>
    </linearGradient>
    <linearGradient id="gGrip" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#5b4227"/><stop offset="1" stop-color="#836140"/>
    </linearGradient>
  </defs>

  <g id="shadow"><ellipse cx="260" cy="467" rx="{round(98 * bw)}" ry="19" fill="#0d0b09" opacity="0.45" filter="url(#soft)"/></g>

  <g id="pawn" transform="translate(256,468) scale({sc},{sc}) translate(-256,-468)">

  <g id="hair-back" transform="translate(270,150) scale({hs},{hs}) translate(-270,-150)">
    {hair_back(hair_kind, hair_col)}
  </g>

  <g id="leg-far" transform="translate(230,380) scale({lw},1) translate(-230,-380)" stroke="#241a11" stroke-width="2.5" stroke-opacity="0.55">
    <path d="M232 300 L214 366 L188 434 L212 450 L234 440 L242 366 L254 308 Z" fill="{shade}"/>
    <path d="M186 432 L214 428 L222 452 L180 456 Z" fill="#5e4028"/>
  </g>

  <g id="arm-far" stroke="#241a11" stroke-width="2.5" stroke-opacity="0.55">
    {load["arm_far"]}
  </g>

  <g id="weapon">
    {load["weapon"]}
  </g>

  <g id="torso" transform="translate(266,250) scale({bw},1) translate(-266,-250)" stroke="#241a11" stroke-width="2.5" stroke-opacity="0.55">
    <path d="M234 190 L296 182 L310 236 L302 302 L238 310 L226 246 Z" fill="url(#gSkin)"/>
    <path d="M244 206 L268 202 L272 232 L246 236 Z" fill="{hi}" opacity="0.5" stroke-opacity="0"/>
    <path d="M274 200 L296 196 L300 228 L276 232 Z" fill="{hi}" opacity="0.5" stroke-opacity="0"/>
    <path d="M248 244 L296 238 L298 256 L250 262 Z" fill="{shade}" opacity="0.45" stroke-opacity="0"/>
    <path d="M248 268 L296 262 L298 280 L250 286 Z" fill="{shade}" opacity="0.45" stroke-opacity="0"/>
    <g stroke-opacity="0">{tattoos(rng, tat)}</g>
    <g stroke-opacity="0">{blood(rng)}</g>
    <path d="M230 302 L306 294 L316 324 L296 348 L238 344 L222 318 Z" fill="{cloth}" stroke-opacity="0.6"/>
    <path d="M236 320 L312 312 L310 328 L238 336 Z" fill="#000000" opacity="0.3" stroke-opacity="0"/>
    <path d="M258 340 L288 336 L296 400 L272 406 L256 382 Z" fill="{cloth}" stroke-opacity="0.35"/>
  </g>

  <g id="leg-near" transform="translate(296,384) scale({lw},1) translate(-296,-384)" stroke="#241a11" stroke-width="2.5" stroke-opacity="0.55">
    <path d="M272 302 L310 308 L322 380 L326 446 L298 456 L288 382 L266 314 Z" fill="{base}"/>
    <path d="M292 446 L326 442 L336 464 L288 466 Z" fill="{shade}"/>
  </g>

  <g id="head" transform="translate(270,150) scale({hs},{hs}) translate(-270,-150)" stroke="#241a11" stroke-width="2.5" stroke-opacity="0.5">
    <path d="M256 170 L290 166 L294 196 L258 200 Z" fill="{shade}"/>
    {face(rng, skin, jaw)}
    <g stroke-opacity="0">{hair_front(hair_kind, hair_col)}{band_svg}</g>
  </g>

  <g id="offhand">
    {load["offhand"]}
  </g>

  <g id="arm-near" stroke="#241a11" stroke-width="2.5" stroke-opacity="0.55">
    {load["arm_near"]}
  </g>

  </g>
</svg>
'''


def main():
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 50
    mode = sys.argv[2] if len(sys.argv) > 2 else "full"
    out = OUT if mode == "full" else OUT.parent / "bodies"
    out.mkdir(exist_ok=True)
    stem = "pawn" if mode == "full" else "body"
    for i in range(n):
        (out / f"{stem}-{i:02d}.svg").write_text(
            build(i + 1, mode), encoding="utf-8")
    print(f"wrote {n} {mode} svg to {out}")


if __name__ == "__main__":
    main()
