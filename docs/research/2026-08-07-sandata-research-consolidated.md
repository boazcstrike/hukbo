# Sandata research consolidation

Date: 2026-08-07
Status: research complete, design not yet written
Branch: `sandata-scaffold`, based on `main` at `8743e8b`

This document consolidates six parallel research passes into the single input for the
Sandata design document. Sandata is the working name for a second game in this
repository: a modern-era, top-down tactical shooter in the shape of Door Kickers 2,
sharing an engine spine with Hukbo but owning its own simulation, content, and hash
contract.

Everything below is either quoted from the repository with a `file:line` citation, or
cited to an external source. Claims that could not be verified are marked
**UNVERIFIED** and must not be treated as settled.

---

## 1. Scope and the requirement conflict

The request, in the user's own words, asked for Door Kickers 2 gameplay in which
"the bots should be able to automatically create the pathway" and "all the bots are
automatically finding pathways and automatically grouped together."

Door Kickers 2 does not work this way. Player troopers do not auto-path; the player
drags a polyline by hand, node by node. There are no formations and no group move
order — every trooper is pathed individually, and this is the defining structural
choice of the game. The only grouping primitives are `sync` (pace-match one trooper
to another) and go-codes (assign a letter to waypoints on several troopers, then
release them all with one keypress). Only the enemy AI pathfinds.

The requirement is therefore not a literal Door Kickers clone. It is closer to what
this repository already is: Hukbo runs two autonomous factions under a spectator
camera, with a `FormationPlanner` that already assigns persistent contingent
identity. The reading adopted here, pending explicit confirmation from the user, is:

> Keep Hukbo's autonomous-agent spine. Replace melee resolution with Door Kickers 2's
> gunfight geometry, cover model, and aperture-driven level design. Bots path and
> group themselves. Scaffold the player order layer as types and UI, but do not make
> it the primary loop.

This decision is load-bearing for the entire plan and is flagged for the user rather
than buried.

**Explicit assumption, also flagged:** "generate the model for the guns" is read as
two-dimensional weapon geometry, not 3D meshes. The client is entirely procedural 2D
(see section 5). If 3D meshes were meant, this plan does not cover them.

---

## 2. Door Kickers 2 mechanics worth implementing

Sources: KillHouse Games map editor guide, the community "Door Kicking 101" guide,
and the game's own moddable XML data files.

### The gunfight is a timing chain, not a dice roll

Resolution order per engagement: `readyTime` (raise weapon) then turn/rotate then
`aimTime` then fire then `resetTime` before the next engagement. Published figures:
rifle ready around 405 ms against roughly 80 ms for a 1911; aim time around 350 ms
under 14 m, 150-180 ms for a pistol, around 335 ms for a rifle, 500 ms and up for a
designated marksman rifle. Heavier weapons rotate more slowly. A target near the edge
of the vision cone takes longer to engage than a centred one.

Every one of these is an integer millisecond count and converts directly to ticks.
Whoever completes the chain first wins, and under roughly 14 m time-to-kill is
effectively instant, so the fight is decided by the chain and not by damage numbers.

### One rule generates the whole game

Crossing a doorway, or standing within a short distance of a wall, forces the weapon
**lowered**, which re-imposes `readyTime` when it must come back up. Pistols are
exempt. This single conditional is why a pistol beats a rifle in a doorway, and it is
the mechanical core of the product.

### Fire mode is selected by range band

Published bands: full-auto 0-15 m, burst 16-20 m, single 21-50 m, varying per weapon
and ammunition, with lower recoil widening the bands. This is the hook that ties the
simulation to the audio library: the simulation picks the mode, and the mode picks the
sound slot.

### Cover is directional and graded

Flat 50 percent damage and hit reduction, applied only within the arc the cover object
actually faces, unless the object is explicitly 360 degrees. Two troopers behind the
same car do not both get it — only the one whose arc indicator is displayed. Fire from
the flank or rear ignores cover entirely. Crouching behind cover is near-total
protection but forbids firing, making it a survive-the-magazine button rather than a
fighting stance.

### Vision, contact tiers, and sound as a second sense

Per-unit frontal cone, no peripheral vision, no 360-degree awareness. A unit fires
automatically at an armed enemy inside its cone and never at anything outside it. Fog
of war is per-cone and world state is remembered rather than live: a door you cannot
see plays its opening animation only when you next see it, and enemies leave ghosts at
their last known position. Three contact tiers: unknown, then a question mark meaning
"something is there but beyond identify range and not shootable", then identified.

Sound is a parallel sense with published radii: bolt cutters 4.5 m, smoke 10 m, hammer
and crowbar 12 m, breacher shotgun 25 m. Breaking glass is louder than gunfire. Death
screams propagate and pull investigators.

### Angles are the level design

The map is a graph of apertures — doors, windows, blown wall holes, hallway mouths,
corner returns — and each aperture is an angle you either own or expose yourself to.
Slicing the pie (holding aim on a fixed point while arcing at distance) buys exposure
to one enemy at a time. Crossing a door straight on exposes you to every angle at once
with the weapon lowered.

An angle-dense test map therefore needs: many small apertures with overlapping interior
fields of fire; hard corners with return angles; cover objects whose protected arcs
face away from the natural entry; at least one non-obvious wall-breach face that flanks
the prepared angle; and closets or stalls that create rear angles requiring a clear.

### What Sandata does better

Door Kickers 2's enemy AI is explicitly non-deterministic run to run. This repository
mandates byte-determinism. Sandata keeps determinism: same seed, same build, same
fight, every time.

### No wounding layer

No bleedout, no revive, no medic, no downed state during a mission. Death is instant.
Incapacitated and killed-in-action are campaign-layer states. This simplifies the
simulation considerably and should be preserved.

---

## 3. Weapons

24 rifles and 14 pistols, each row sourced to a manufacturer sheet, a Small Arms Survey
identification sheet, or procurement reporting. Shotguns, submachine guns, light
machine guns, and launchers are out of scope by instruction.

Covering the families named in the request: AK-47, AKM, AK-74M, AK-12 in both the
2018/2021 and 2023 configurations, AK-15; M16A4, M4, M4A1, Mk 18 Mod 1, M7, XM8;
Beretta 92FS/M9, Beretta APX A1, Beretta ARX160. Expanded with HK416 A5 and HK416F,
G36, SCAR-L and SCAR-H, Steyr AUG A3, Tavor X95, QBZ-191 and QBZ-95-1, L85A3, CZ BREN
2; and for pistols Glock 17 and 19 Gen5, SIG M17 and M18, SIG P226, S&W M&P9 M2.0, HK
VP9, HK USP, CZ P-10 C, Walther PDP, MP-443 Grach, QSZ-92.

### Fire mode sets drive both simulation and audio

`M4` and `M4A1` are separate rows on purpose: M4 is `safe/single/burst3`, M4A1 is
`safe/single/auto`. They are different weapons to the simulation and to the audio
library. Likewise AK-12 (2018/2021) and AK-15 carry `burst2` while the AK-12 2023
model deletes it.

Distinct sets in the roster: `{safe, single, auto}` covers nineteen rifles;
`{safe, single, burst3}` covers M16A4 and M4; `{safe, single, burst2, auto}` covers
AK-12 (2018/2021), AK-15, and G36; `{single}` covers striker-fired pistols with no
manual safety; `{safe, single}` covers the rest of the pistols.

Two audio consequences that must not be lost:

- A burst must be a **baked** asset, not an automatic loop trimmed to three rounds.
  The mechanical burst cam produces an uneven cadence a loop cannot reproduce.
- The Steyr AUG has no rotary selector, only a cross-bolt push-button safety and a
  progressive trigger. Its mode-change sound is a button thunk and must not share the
  AK or AR selector sample.

### Caliber, not weapon, drives the report sample

Six report families cover all 24 rifles: 7.62x39, 5.45x39, 5.56x45, 7.62x51, 6.8x51,
5.8x42. Two more cover the pistols: 9x19 and 5.8x21. Eight families in total, not 38
weapons. Per-weapon character then comes from mechanism sounds layered on top of the
family report. This is what makes a 500-file library tractable rather than absurd.

### Trademark

Glock, Heckler & Koch, Beretta, SIG Sauer, FN Herstal, Steyr, and IWI are all rated
high risk, with Heckler & Koch and Glock the most aggressive enforcers. Glock and
Steyr have separately asserted **trade dress** claims, so a silhouette carries risk
independent of the name.

Numeric designations issued by a government are materially safer: M4, Mk 18, L85,
QBZ-191, M7, MP-443. The recommendation carried into the design is to keep real names
in data files and documentation, and to place shipped display names behind a single
configurable field so the alias set can be swapped without touching the data.

---

## 4. Determinism, pathfinding, and the math gaps

### The two verified gaps

Both were checked directly against the working tree, not taken on report.

**`FixedPoint` has no multiply and no divide.** It declares only `+`, `-`, and four
comparison operators (`src/Hukbo.Core/Mathematics/FixedPoint.cs:90-106`). Every
multiply in the codebase today routes through `MultiplyRatio` or raw `long` arithmetic
outside the type. `IntegerSquareRoot` is `internal` and takes and returns `long`, not
`FixedPoint` (`FixedPoint.cs:61`). `Scale` is 1024, so the representation is Q22.10
(`FixedPoint.cs:8`).

**The facing sector vectors are not unit length.** From
`src/Hukbo.Core/Movement/FacingRules.cs:29-45`, the sector components include 946, 724,
and 392 at a scale of 1024. But 946 squared plus 392 squared is 1,048,580 against
1024 squared of 1,048,576 — off by four. And 724 squared doubled is 1,048,352 — off by
224. The error differs per sector, so any cone test written as a cosine comparison that
assumes `|f| == Scale` produces a subtly different cone shape depending on which way
the unit faces. `Facing16` is pinned append-only and cannot be widened
(`src/Hukbo.Core/Movement/Facing16.cs:8-10`).

### Navigation architecture

Recommended: **uniform integer grid A\* for topology, then a funnel string-pull that
snaps the resulting polyline to the real vector wall geometry** using exact integer
orientation predicates. Nav cell is one quarter of a visual tile.

The grid decides which side of a wall you pass and contains no geometry predicate at
all, so its entire determinism surface collapses to one comparator that can be unit
tested in isolation. The string-pull is what buys the angles: a unit crossing an 18.4
degree corridor walks a straight 18.4 degree line rather than a staircase. The result
is navmesh-quality output from grid-quality input, with zero authoring.

Rejected, with reasons recorded so they are not relitigated:

- **Navmesh** for version one. Constrained Delaunay triangulation in exact integers is
  possible, but cocircular and collinear degeneracies are where hand-rolled
  implementations actually break. Noted as the upgrade path once map count justifies it.
- **Flow fields.** Built for many units sharing few goals. Eight squads with eight
  different destinations is their worst case and A*'s best.
- **Waypoint graphs.** Makes authoring the bottleneck and silently invalidates links
  when a wall is edited.
- **Hierarchical A\*.** Pure performance optimisation that changes path output, so
  adopting it later would be a preset-version change rather than a free swap. Deferred.

### Determinism traps to write into the design

Ranked by how easily they slip through: open-set tie-breaking must use the total order
`(f, h, nodeIndex)` so any correct heap gives one answer; priority queues are never
stable and `Array.Sort` is introsort, so the comparator must be total rather than the
sort stable; float heuristics must be replaced by the integer octile form
`10 * (max - min) + 14 * min`; `Math.Sqrt` and `atan2` are `double` transcendentals and
are banned by the same argument that bans `System.Random`; search state must live in
flat arrays indexed by node index rather than dictionaries, because dictionary
enumeration order changes with capacity growth; parallel flood fill makes visit order
thread-schedule-dependent and is a guaranteed desync; path smoothing must compare an
exact integer cross product against exactly zero rather than against a float epsilon;
C# integer division truncates toward zero so negative coordinates merge cells unless
floor division is used; neighbour enumeration needs one pinned static offset table;
and path budgeting must be expressed in ticks rather than wall-clock or frame count.

That last one deserves emphasis. Amortise pathfinding by **fixed latency**, not by
per-tick budget: a path requested at tick `t` becomes valid at tick
`t + PathLatencyTicks` regardless of how many searches the machine actually completed.
A budget scheme makes arrival depend on how many groups happened to request that tick,
which is harmless right up until someone adds a "no path yet, move directly at the
goal" fallback, at which point the simulation branches on scheduling.

### Math to write

- `operator *` and `operator /` on `FixedPoint`, both `checked`, both documenting
  truncation toward zero as a behavioural contract.
- A public `Sqrt(FixedPoint)` wrapper over the existing exact bitwise
  `IntegerSquareRoot`, pre-multiplying by `Scale` before the root.
- A new fine angle type. Binary angular measurement in a `ushort`, 65536 to the turn,
  is recommended: wraparound is free, and shortest-arc difference is exactly the
  `short` cast of the difference, with no branches. **Do not widen `Facing16`.**
- Integer CORDIC in vectoring mode, sixteen iterations, for fine `atan2`. Shifts and
  adds only, sixteen pinned arctangent constants, accuracy around 0.0055 degrees.
- A 257-entry quarter-wave sine table at scale 65536 with integer linear interpolation.
  Pinned literal data, treated as hash contract.
- Exact segment intersection via the orientation predicate
  `(bx-ax)*(cy-ay) - (by-ay)*(cx-ax)` in `long`, with collinear and touching as a
  separately named case carrying a written rule rather than an epsilon.
- Ray versus axis-aligned bounding box by the slab method **without division**, keeping
  each parametric value as a rational pair and comparing by cross-multiplication.
- Point-in-polygon by crossing number with a half-open edge rule that removes every
  degenerate case without special-casing.
- A two-pass integer chamfer distance transform producing the clearance field.

### Line of sight

Two-phase. Integer Amanatides-Woo digital differential analyser over the nav grid
enumerates, in strict order, the few cells whose wall buckets need checking. The exact
segment predicate then answers authoritatively against the real wall list. The standard
Amanatides-Woo formulation accumulates in floats and must be rewritten division-free,
keeping the parametric values as rationals compared by cross-multiplication. The
diagonal-corner tie is resolved by a written rule (step X first), not a float compare.

Supercover rasterisation alone is insufficient: it answers "which cells does this line
touch", which is only a proxy for "does this line cross a wall", and on an 18.4 degree
wall the two answers visibly disagree with the drawn geometry.

Vision cones must **not** use a cosine comparison, for the unit-length reason in
section 4. Use two half-plane cross products against boundary vectors drawn from the
same pinned table. Every term stays inside `long`, there is no normalisation and no
length assumption, and widening a cone becomes a data change with a visible diff.

### Squad grouping and shared paths

This is the mechanism that answers the "automatically grouped together" requirement.

Groups form by deterministic union-find over the pair list that
`CollisionUniformGrid` already emits, normalised and sorted. Group identity is the
minimum entity id in the component; leader is the lowest living entity id. Both are
derived rather than stored, so they survive snapshot and resume with no extra state and
re-derive on death with no leader-election tick. `ContingentId` from `FormationPlanner`
already exists and should be reused as squad identity rather than inventing a parallel
concept.

One A* per group per destination, never per unit. Eight searches instead of sixty-four,
and — more importantly — squadmates cannot select topologically different routes around
the same pillar, which is the most common way a squad visibly falls apart in this genre.

The shared result is a polyline carrying precomputed cumulative integer arclength. Each
unit's target is then a pure function of one scalar: its own slot offset along that
arclength. Followers are literally standing on the leader's past path, so they cut the
same corners automatically. Rigid lateral offsets would push the outside file into the
wall on every corner; this is the whole trick.

Doorway collapse falls out of the baked clearance field. When corridor clearance drops
below formation width, lateral offset goes to zero for every slot and the squad becomes
a single file, re-expanding on the far side. No state, no timer, no special case inside
the pathfinder.

Local avoidance uses three ordered rules matching the shape already in the repository:
propose without seeing other proposals, prioritise by the total order
`(groupId, slotIndex, entityId)`, then commit sequentially against the collision grid,
with a blocked unit first trying a 22.5 degree sidestep and otherwise waiting a tick.
Never a force, never an impulse, never a push-apart.

**Rejected with reasons.** Boids are force accumulation, which is rigid-body physics
under another name and is banned by `CLAUDE.md` section 9; they also fan out in
corridors and mill in doorways. RVO and ORCA can be made fixed-point — Klotho is the
existence proof — but every degenerate case in the linear program becomes a tie
requiring a written total order, and constraint insertion order changes the solution
even when the constraint set does not. That is a large, subtle determinism surface for
something eight indoor units do not need.

### The dependency question, answered honestly

Repository policy prefers a proven library over hand-rolled code. The honest finding is
that **the intersection of "proven" and "deterministic" is empty for .NET navigation
code**. DotRecast and Roy-T.AStar are float-based and make no determinism claim at all.
SharpNav is float-based and ships a `System.Random` field, which `CLAUDE.md` section 5
bans by name. GoRogue drags two transitive numeric dependencies. Every fixed-point
option is experimental (Klotho self-labels as not production-ready), Unity-coupled, or
would introduce a second fixed-point type alongside Q22.10 and fork the hash contract.

The policy therefore resolves not to "hand-roll everything" but to **port two
specific, extremely well-proven algorithms in integer form**: Recast's funnel
string-pull (from DotRecast, zlib, attribution only) and recursive shadowcasting field
of view (from GoRogue, MIT). Both are small, both are already integer-friendly, both
carry decades of production use, and neither puts a float into the state hash.

### Map format

Line-oriented text, integers only, extension `.hkmap`. Not JSON and not TOML, because a
line-oriented record format makes one semantic change equal one changed line in a diff,
is typeable by hand without a schema, and — decisively — has **no syntax capable of
expressing a float**, so the class of bug where a fraction parses differently under two
cultures is structurally impossible rather than merely tested against.

Parsing uses `NumberStyles.None` with the invariant culture, which rejects signs,
decimal points, separators, and surrounding whitespace. A malformed line is a hard load
error, never a skipped line. Records are sorted canonically before baking so file line
order cannot reach the nav data, and a duplicate record is a load error, which makes
the comparator total.

All navigation data is derived at load and never stored: rasterise walls and closed
doors into the nav grid, inflate by body radius so the grid encodes "a body fits here"
rather than "a point fits here", build the clearance field by integer chamfer, tag door
cells as high-cost-but-passable to the planner and impassable to the mover until opened,
and bucket wall segments into the same uniform grid for the line-of-sight narrow phase.

Finally, FNV-1a over the canonicalised record stream folds into the scenario content
hash. This is load-bearing: editing one wall coordinate then moves the state hash, which
forces new golden expectations exactly as `CLAUDE.md` section 5 requires. Without it, a
map edit silently invalidates every recorded replay with no signal at all.

---

## 5. Client, UI, and pawns

### The pawn renderer is fully procedural, and that is good news

There are no textures anywhere in the pawn path. Every element is a draw of a shared
one-by-one pixel texture into a rectangle, a rotated block, or a thick line. The
composition runs fifteen layers in `PawnRenderer.DrawLayout`
(`src/Hukbo.Client/Rendering/PawnRenderer.cs:267-458`).

Two facts make the modern operator a direct extension rather than new technology.
`DrawRotatedBlock` (`PawnRenderer.cs:1032-1058`) already performs arbitrary continuous
rotation about a pivot, which is exactly what a continuously-aimed rifle needs and is
strictly harder than the discrete swing arcs Hukbo requires. And `layout.WeaponEnd`
(`src/Hukbo.Client/Rendering/PawnGeometry.cs:82`) is already the tip of the weapon
line, which is the muzzle flash anchor.

The recommendation is to stay procedural. There is no sprite asset pipeline to stand up
— the content pipeline ships fonts only — and a sprite sheet would need its own pure
helper layer for frame selection anyway, which is the same discipline the geometry
already has.

The one genuinely new requirement is a persistent facing angle. Today's
`SwingPose.WeaponAngleRadians` is a transient swing-only pose that springs back to
neutral; an operator's weapon must track its aim continuously.

### Theme roles

`UiThemeColors` declares 27 roles (`src/Hukbo.Client/Theming/UiTheme.cs:11-38`), and
`UiThemeCatalog.ValidateDocument` rejects any unknown role
(`src/Hukbo.Client/Theming/UiThemeCatalog.cs:272-279`).

A tactical shooter needs roughly 35: keep 23 unchanged, repurpose 4 (TeamA to Friendly,
TeamB to Hostile, OtherFaction to UnknownContact, Selection to SelectedTrooper), and add
12 (Suppressed, Downed, OrderPath, Waypoint, CoverGood, CoverNone, BreachPoint,
FireConeFill, FireConeEdge, AlertCalm, AlertRaised, AlertBreach). Note that alert is
three states rather than a boolean.

**Decision:** Sandata gets its own colour record. Bolting twelve tactical roles onto the
shared 27-role record would force all five existing melee themes to author meaningless
colours or break the catalog's exact-role-count invariant. Melee themes stay untouched.

### Gaps with no existing implementation

None of these exist anywhere in the client and all must be built: multi-select
(`AgentSelection` is single-entity only), continuous drag-capture pointer state, an undo
stack (`ConfirmationPrompt` guards destructive exit only, and is not a general undo), a
minimap, and fire-cone geometry.

### Readability rules worth enforcing as tests

Friendly, Hostile, and Unknown colours must be theme-independent constants, matching the
existing discipline in `FactionColorPalette`. Every state change must convey through
shape as well as colour, never colour alone. Tactical decision geometry — fire cones and
order paths — must render at every detail tier rather than fading with zoom the way
decorative layers do. New theme roles must clear the same contrast pair checks the
existing roles already face in `UiThemeCatalog.GetRequiredRenderedContrastPairs`.

---

## 6. Audio

### The existing pipeline, measured

The 70 shipped WAVs total 1,289,596 bytes, mean 18.0 KB, all 24 kHz stereo 16-bit PCM,
mean duration 0.191 seconds. They are copied verbatim to output by a glob in
`src/Hukbo.Client/Hukbo.Client.csproj:27-34` and never touch the MonoGame content
pipeline, so adding files costs nothing at build time. They are tracked directly in git;
there is no Git LFS configuration in this repository.

Variant selection is deterministic, seeded from `tick * MixConstant XOR sourceEntityId`
through SplitMix64 (`src/Hukbo.Client/Audio/SoundVariantSelector.cs:20-32`). The
filename format allows two variant digits, capping variants at 99 per slot
(`src/Hukbo.Client/Audio/SoundCatalog.cs:26`).

### Three findings that change the design

**The catalog cannot express weapon by fire mode.** The variant axis is `HitClass` —
skull, neck, ribcage, gut, limb, extremity — hardcoded melee body parts
(`SoundCatalog.cs:103-113`). Gunfire has no hit location, so every weapon and fire-mode
combination would need its own enum member plus a switch arm
(`SoundCatalog.cs:32-47,57-77`). At this scale that is unacceptable enum bloat. Sandata
needs a data-table catalog. The melee catalog stays exactly as it is.

**MonoGame's instance pool is the real ceiling, not the cue budget.** `SoundCueBudget`
allows 64 cues per frame and 16 per sound
(`src/Hukbo.Client/Audio/SoundCueBudget.cs:27-28`), and eight shooters at 800 rounds per
minute is only about 1.8 shots per frame at 60 fps, which is comfortable. But
`MonoGameSoundPlayer.Play` catches `InstancePlayLimitException`
(`src/Hukbo.Client/Audio/MonoGameSoundPlayer.cs:107-126`), and gunshot tails hold an
instance three to five times longer than the measured 0.19-second melee mean. The
existing budget was tuned against a melee measurement of 21 cues per frame and has never
seen sustained automatic fire. This needs its own measurement pass before shipping.

**The 5 percent trim threshold will chop gunshot tails.** The default is 5 percent of
peak (`scripts/sfx.ps1:128`). The script already solved this exact problem once for a
different sound class: tonal user-interface cues use 2.0 because "a pitched tone decays
smoothly and 5 percent audibly chops the tail" (`scripts/sfx.ps1:230-233`). Gunshot
variants carrying reverb or echo need 1 to 2 percent, or no trimming at all.

### Cost, and what it means

ElevenLabs bills sound effects at 200 credits per generation. 500 files is 100,000
credits. The Creator tier at 22 USD per month provides 121,000 credits, leaving about
21,000 of headroom and no margin for retries. The project's own skill documentation
records real take-quality variance, with one run peaking at 93 percent usable and
another under 1 percent. A realistic run with a 30 to 50 percent reject rate needs 650
to 750 generations, or 130,000 to 150,000 credits, which overruns Creator and requires
the Pro tier at 99 USD.

**Realistic cost: 22 USD best case, 99 USD likely.** Whether credits scale with
requested duration could not be confirmed and is **UNVERIFIED**; if they do, sub-second
gunshots may cost less than the flat figure.

`scripts/sfx.ps1` generates one slot per process launch and has no batch mode, so 500
files means 500 launches, roughly 42 to 125 minutes of wall clock before rate limiting.
Retry and backoff already exist at `scripts/sfx.ps1:164-166` — six attempts at 2, 4, 8,
16, 30, 30 seconds. ElevenLabs concurrency limits on lower tiers are **UNVERIFIED**.

At 0.6 to 1.0 seconds each in the current 24 kHz stereo format, 500 files is 27.5 to 46
MB, a roughly 25-fold increase in this repository's tracked audio that every clone pays
for.

### The matrix, rebuilt for rifles and pistols only

Eight report families rather than 38 weapons is what makes this tractable.

| Category | Breakdown | Count |
| --- | --- | --- |
| Single-shot report | 8 families x 5 environments x 6 variants | 240 |
| Baked burst3 | AR 5.56 only x 3 environments x 4 | 12 |
| Baked burst2 | AK-12 5.45 and G36 5.56 x 3 environments x 4 | 24 |
| Automatic loop and tail | 6 rifle families x 2 environments x 4 | 48 |
| Selector and safety | 4 rifle sets and 2 pistol sets x 4 | 24 |
| Dry fire | 8 x 3 | 24 |
| Magazine out, in, bolt | 3 actions x 4 mechanism groups x 4 | 48 |
| Impacts | concrete, metal, wood, flesh, ricochet x 8 | 40 |
| Casings | rifle and pistol x concrete and dirt x 6 | 24 |
| **Total** | | **484** |

**Correction, 2026-08-07.** This total is wrong against the catalog that was
actually built. `SandataSoundCatalog` declares 106 slot rows expanding to 524
individual variant files. Task 40 measured both numbers from the compiled
catalog and traced the 484 back to this table, which predates the
implementation. The catalog is authoritative; this table is kept as written so
the error stays traceable.

Environments are close-dry, indoor tail, outdoor tail, distant, and suppressed.
Mechanism groups are AK, AR, bullpup, and pistol.

**No generation runs until the user reviews a dry-run manifest and authorises the
spend.**

---

## 7. Reuse map

229 source files were classified. The material conclusions:

**Reusable verbatim** — `SplitMix64`, `Fnv1a`, `FixedPoint`, the whole of
`Hukbo.Diagnostics`, `InputEdges`, `SpectatorCamera`, `RenderMetrics`,
`FrameTimingAggregator`, `UiTextGeometry`, `UiEmphasisPulse`, `UiTransition`,
`SettingsChoiceSelector<T>`, `RightColumnSplit`, and the display and scale settings
enums. None of these import `Hukbo.Core.Combat` or `Hukbo.Core.Movement`.

**Reusable after extraction into a shared assembly** — the entire collision module
(`CollisionGeometry`, `CollisionRules`, `CollisionResolver`, `CollisionUniformGrid`,
`CollisionPair`, `CollisionPriority`, `CollisionMetrics`, `CollisionScratch`); the whole
theming package; the audio machinery minus its melee mapping; `Facing16` and
`FacingRules`; `UiButton` and every motion helper; `BattleOutcome`; the settings store.
`CollisionScratch` needs its `Scenario` constructor parameter replaced by plain integers
— a signature change, not a behaviour change.

**Fork** — the tick orchestrator, agent state, event types, snapshot shape, formation
planner, all effect systems, all renderers, and the panel layouts. The shapes are right;
the content is melee.

**Hukbo-only, do not port** — everything under `Combat` keyed on `WeaponId` or
`BodyPart`, every movement profile, the historical appearance catalogs, dye palettes,
rank labels, warrior names, evidence tiers, and clash effects.

### Determinism risk in the extraction

Files proposed for extraction are hash-safe because they are pure code motion with no
behaviour change and none participate in `CombatRuleset.ContentHash` or
`MovementRuleset.ContentHash`. The real risk is not in the moved bytes but in call
sites: every `using` must update, and `TreatWarningsAsErrors` will surface a missed
reference as a hard error rather than a silent fallback.

Anything folded into a content hash stays in `Hukbo.Core` permanently. Only its
*pattern* forks into a parallel Sandata ruleset with its own independent content hash.

### Build gates, verified directly

**The console ban auto-covers Sandata.** It scans `Path.Combine(root, "src")`
recursively (`tests/Hukbo.Client.Tests/SourceHygieneTests.cs:35`), so it will fail the
moment any new file outside the named entry points contains `Console.`. Sandata's two
`Program.cs` files must be added to the `ConsoleOwners` array.

**The Diagnostics boundary test does not cover Sandata.** It is hardcoded to
`typeof(Scenario).Assembly`
(`tests/Hukbo.Core.Tests/DiagnosticLoggingBoundaryTests.cs:24`), and its positive
control is hardcoded to `typeof(HeadlessRunner)` (`:40`). Parallel facts are required or
`Sandata.Core` silently gains a Diagnostics reference with nothing to catch it.

**The pinned package list is asserted exactly.** `PinnedPackageNames`
(`SourceHygieneTests.cs:165`) is compared for exact equality (`:309-310`). Any new NuGet
package fails the gate and is a reviewed dependency change with lock-file regeneration.

**Every script hardcodes Hukbo.** Verified: `scripts/benchmark.ps1:42` names
`src/Hukbo.Headless`; `scripts/run.ps1:21` and `scripts/package.ps1:12` name
`src/Hukbo.Client`; `scripts/test.ps1:15-16` names both test projects;
`scripts/build.ps1:18,21`, `scripts/format.ps1:13-14`, and `scripts/bootstrap.ps1:44`
name `Hukbo.slnx`; `scripts/doctor.ps1:82-85` carries a fixed lock-file list. Only
`scripts/_common.ps1` and `scripts/verify.ps1` are genuinely game-agnostic. Each script
needs a game-target parameter defaulting to Hukbo so existing behaviour is unchanged.

`Directory.Build.props:1-18` applies `net10.0`, nullable, `TreatWarningsAsErrors`,
analysers, lock files, and NuGet audit to every project automatically, with no visible
opt-out. Sandata must be warning-clean from the first line. New projects must be added
to `Hukbo.slnx` explicitly or the solution build never sees them.

---

## 8. Open questions for the user

1. **Autonomous bots versus player orders.** Section 1. The plan assumes autonomous
   bots as primary with the order layer scaffolded. This is the largest single
   assumption in the document.
2. **Two-dimensional weapon geometry versus 3D meshes.** Section 1. The plan assumes 2D.
3. **The product name `Sandata`.** Trivial to change before the first commit.
4. **Real weapon names versus generic aliases in shipped display strings.** Section 3.
5. **Authorisation to spend on 500 audio generations.** Section 6. Nothing runs until a
   dry-run manifest is reviewed.
