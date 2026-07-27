# Pawn Character Visuals Design

> **Archived: reference only.** This document is deprecated. Do not execute it, and do not treat its steps, versions, or tooling references as current. The live contract is `CLAUDE.md` plus the skills in `.claude/skills/`.

## Goal

Replace Hukbo's arena dots with original, zoom-aware procedural pawn characters
and add matching inspector portraits with historically grounded weapon
silhouettes.

This first pass is presentation-only. Weapon roles and body variation do not
change hitboxes, movement, attacks, targeting, health, simulation hashes, AI, or
battle outcomes.

## Approved scope

> **Correction (2026-07-27):** The roster below was revised before the feature
> shipped. The implementation carries **four** cosmetic weapon roles, not five,
> and the weapon role is taken from the authoritative Core loadout rather than
> being derived from the agent's entity ID. The shipped behavior lives in
> `src/Hukbo.Client/Presentation/PawnAppearance.cs` and
> `src/Hukbo.Client/Presentation/PawnAppearanceFactory.cs`.

The cosmetic roster uses four weapon roles grounded in
`docs/research/HISTORICAL_1500s_WEAPONS.md`. Each one is presented to the player
under the plain descriptor required by `CLAUDE.md` section 7:

1. Great Blade
2. Heavy Chopper
3. Thrusting Blade
4. Work Blade

No player-facing label may claim a definitive kampilan, panabas, or kris. Those
comparative identifications appear only in the descriptor's evidence note, and
always carry a `PROVISIONAL` prefix. The weapon role comes from the agent's
authoritative Core loadout; the entity ID drives stature, build, head treatment,
clothing, and skin variation only, and must never influence weapon identity.
Appearance is derived deterministically without consuming simulation randomness.

## Architecture

Add an immutable presentation descriptor that records a pawn's weapon role,
stature, build, head treatment, and material colors. A pure factory derives the
descriptor from the agent's stable entity ID. Arena and inspector code request
the same descriptor, so a selected pawn and its portrait cannot drift.

Add one allocation-free procedural renderer that draws from the existing white
pixel texture. It owns geometry, zoom detail policy, complete visual bounds,
faction ground rings, and hover/selection marks. It accepts a foot anchor and a
render scale so the same drawing language works in the world and in a fixed
48-56 pixel inspector portrait.

No Core simulation types, state hashing, battle events, combat settings, or
content-pipeline assets change.

## Visual language

Every readable pawn contains:

- a foot-anchored torso capsule;
- a fixed-size head disk;
- a hair or headcloth wedge;
- a weapon silhouette extending beyond the body;
- a faction-colored ground ring or base mark; and
- a non-color-only hover or selection indicator.

Clothing uses natural material colors from the research palette. Faction color
stays concentrated in the ground ring and small cloth or shield accents so
weapon and body silhouettes remain legible.

Body variation is cosmetic:

- stature multipliers: `0.90`, `1.00`, and `1.10`;
- build multipliers: `0.86`, `1.00`, and `1.18`;
- stable head size; and
- stable reach per weapon class.

Weapon silhouettes provide the primary role cue. The four entries below were
written against the superseded five-role roster and describe the proposed
silhouette language rather than the shipped one; the shipped roles are Great
Blade, Heavy Chopper, Thrusting Blade, and Work Blade.

- Long Spear: the longest diagonal shaft with a leaf-shaped iron point.
- Hardened Javelin: a shorter warm-brown shaft with a charred point and rear
  bundle at higher detail.
- War Bow: a tall bow arc plus quiver and pale reed arrows.
- Broad Dagger: a compact stance with a wide leaf-shaped blade.
- Great Blade: a long, forward-heavy single-edged silhouette with a widening
  tip.

## Zoom behavior

Pawn size follows camera zoom but is clamped to a readable screen-space range.
Detail is divided into discrete tiers:

- low: faction base, head, torso, and primary weapon silhouette;
- medium: headcloth or hair, weapon material separation, quiver or javelin
  bundle; and
- high: restrained clothing, grip, blade, and armor accents.

At extreme strategic zoom, the renderer prioritizes faction and weapon-class
silhouettes rather than promising portrait-level detail. Full weapon-inclusive
bounds are used for culling and selection framing so long weapons do not pop or
clip at arena edges.

## Inspector portrait

The agent inspector reserves a 48-56 pixel portrait frame. It calls the same
appearance factory and renderer as the arena at a fixed portrait scale.

The existing authoritative fields remain visible:

- entity ID;
- faction;
- alive/dead state;
- hit points;
- intent;
- target; and
- position.

The cosmetic weapon-role label may be shown as a visual role, without implying
different combat behavior. Dead selections retain the matching appearance and
receive a clear shape or value treatment in addition to the `DEAD` text.

## Performance and determinism

The draw loop must not allocate strings, arrays, lists, or random generators per
pawn. Appearance generation is a pure stable mapping and may be computed on
demand or cached only if measurement justifies it.

The implementation must support the existing default 200-agent scenario and
avoid architectural assumptions that fail at the documented high agent counts.
Presentation tests prove stable assignment without modifying authoritative
determinism tests or expected hashes.

## Testing

GPU-independent Client tests cover:

- deterministic appearance for a stable identity;
- reachability of all four weapon roles;
- allowed stature and build values;
- monotonic zoom scaling and screen-size clamps;
- stable foot anchoring;
- full bounds containing the head, body, selection mark, and weapon; and
- identical appearance lookup for arena and inspector contexts.

Integration verification includes the focused Client tests, repository
verification script, and a manual Windows smoke pass at fitted, minimum, and
maximum zoom. The smoke pass checks pan, hover, selection, inspector matching,
selected-agent death, reset stability, window resize, and dense-agent
readability.

## Risks and mitigations

- Dense fights can become noisy. Low-detail rendering suppresses secondary
  equipment and keeps the faction base dominant.
- Long weapons can be clipped. Culling and framing use complete procedural
  bounds.
- Arena and portrait art can diverge. Both consume the same descriptor and
  renderer.
- Cosmetic roles can be mistaken for balance roles. Labels use visual-role
  language and the implementation remains outside Core.
- The repository contains an uncommitted Hukbo rename and spectator UI work.
  Implementation touches only the immediate Client rendering, inspector, and
  focused test surface and never stages unrelated changes.

## Success criteria

- Every living arena agent is drawn as an original zoom-aware pawn rather than
  a square dot.
- The four cosmetic weapon roles are visually distinct at readable zoom.
- Selecting an agent shows a matching weapon-bearing portrait.
- Hover, selection, culling, and inspector interaction still work.
- Existing simulation outcomes and hashes remain unchanged.
- Focused and repository-level verification passes with no unrelated diff.
