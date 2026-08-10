# Projectile props and embedded projectiles — plan

Design: [2026-08-09-projectile-props-design.md](2026-08-09-projectile-props-design.md).
That document remains the reference for intent and for the nine questions; this
one carries the ordered task list, the decisions that were taken to authorize
it, and the corrections that measurement and code reading have since forced on
the design's own arithmetic.

Status: **authorized for implementation** by the user on 2026-08-11, both
features together, on branch `projectile-props` off `main` at `5a35f67`.

## 1. Decisions taken

The design's section 8 parked five decisions. All five now have answers.

| # | Decision | Answer |
| --- | --- | --- |
| 1 | Does an embedded projectile survive the pawn's death? | Survives — but see the correction in section 3, which makes this almost entirely moot |
| 2 | Fade, or persist until evicted? | Persist until evicted. No per-slot age term |
| 3 | 256 slots, or scale with agent count? | Fixed 256 |
| 4 | Detail-tier gate the embedded pool? | Yes. The in-flight prop stays ungated |
| 5 | Measure the ceiling first, or design to it? | Measured first — section 2 |

## 2. The measurement, and what it corrects

`tools/Hukbo.Tools.RenderProbe`, 500 units, seed 1, Release, vertical retrace
disabled, 150 frames per station, run on 2026-08-11. Report at
`docs/development/render-baselines/render-baseline-projectile-props-500-2026-08-11.json`.

That path is a tracked one on purpose. The probe writes to `artifacts/`, which
`.gitignore` excludes, so the report as written existed on one machine and
nowhere else — the same defect
`SourceHygieneTests.EveryCitedRenderBaselineArtifactExistsInTheRepository` was
written to catch for the earlier baselines. It was copied into the tracked
baselines directory before the worktree holding it was removed.

| Station | quads (max) | triangles (max) | frame p50 | frame p95 |
| --- | --- | --- | --- | --- |
| minimum-zoom | 9,245 | 18,490 | 0.72 ms | 0.94 ms |
| default-fit | 9,246 | 18,492 | 0.75 ms | 0.97 ms |
| maximum-zoom | 1,547 | 3,094 | 0.32 ms | 0.45 ms |

The worst real frame at 500 units is 9,246 quads against a ceiling of 20,000.
The design's section 3 figure of 18,044 is a stacked worst case — every visible
unit a High-tier Busog, every backdrop population at its own hard cap at the
same moment, and all 512 projectile slots live at once — and no real frame
approaches it.

**This measurement does not move the ceiling.** `RenderBudgetEstimate`'s own doc
comment forbids rewriting a budget to match a measurement (the
anti-density-creep rule, R-W6.14), and a ceiling exists to bound the worst case
rather than to describe the median one.
`ArenaBatchQuadsAt500UnitsEstimate` stays at 20,000.

What the measurement does buy is the answer to the question the RU-42 note
actually asked. That note said the next feature wanting a per-pawn quad owed a
fresh measurement rather than an assumption. The measurement is now on record,
and it says the estimated headroom is not a real risk.

**The design's budget table also contains an arithmetic error.** It adds the
full 1,024 quads for the in-flight prop on top of the 512 already charged for
the existing shafts, rather than the delta between them. The existing draw is
already one quad per live flight and is already counted. Corrected:

```
  17,532  pawns and backdrop, worst case (27 quads/pawn x 500) + 4,032
+  1,024  in-flight props, 2 quads x 512 flights   (was 512, so +512)
+    512  embedded projectiles, 2 quads x 256 slots
= 19,068  against a ceiling of 20,000  ->  932 quads of headroom
```

Not the 420 the design states. With the embedded pool detail-gated as decision
4 requires, a 500-unit frame is pulled far enough out to drop those 512
entirely, leaving 18,556 and 1,444 quads of headroom.

## 3. Correction to decision 1: there is no corpse

`ArenaGame.Rendering.cs:967` skips any agent that is not alive unless a lethal
hold is still active, and `AttackAnimation.LethalHoldSeconds` is `0.10f`. A
killed warrior is therefore drawn for one tenth of a second after it dies and
is then gone from the field entirely. There is no corpse layer in this renderer.

So the design's "corpse bristling with arrows" image cannot be produced by
either answer to decision 1, and building the corpse layer that would make it
possible is a separate feature with its own budget and is not in this scope.

The plan therefore implements **no death handling at all**, which is the honest
form of the answer that was given:

- An embedded projectile rides its host through the host's own death animation,
  because the host is still drawn for that 100 ms window.
- When the host stops being drawn, its embedded projectiles stop being drawn,
  because the draw is a per-pawn step inside the pawn loop that a skipped pawn
  never reaches.
- The slot is reclaimed by ordinary oldest-first eviction, exactly as a slot
  belonging to a living host is.

No death event is consumed, no clear-on-death path exists, and nothing tracks
whether a host is alive. Adding any of those would be dead code — the failure
mode this repository has produced eight times in the ranged package alone.

## 4. Task list

Each task names its files, its verification, and what it depends on. Tasks A1
through A4 are Feature A and are independent of B. Tasks B1 through B5 are
Feature B and depend on A1.

### A1 — carry the launching weapon on the flight record

**Files:** `src/Hukbo.Client/Rendering/ProjectileFlight.cs`,
`src/Hukbo.Client/Presentation/ProjectileFlightSystem.cs`,
`tests/Hukbo.Client.Tests/ProjectileFlightSystemTests.cs`.

Add a `WeaponId Weapon` member to `ProjectileFlight`, resolved once in
`TryAdd` from the source agent's `Loadout.Weapon` — the same view lookup the
record already performs for the origin, so it costs nothing extra and no second
dictionary pass.

Resolving at launch rather than at draw closes the hole the design's section 5
worries about by construction. A launcher that dies mid-flight cannot fail to
resolve, because it was resolved before it died and the value is on the record.
The design's proposed arrow-silhouette fallback is therefore **not**
implemented: it would be an unreachable branch, and an unreachable branch is
the dead code this plan's section 3 already refuses once.

**Verification.** A test asserting the weapon of an ingested flight equals the
source agent's weapon, over all three ranged weapons; and a test asserting a
flight whose launcher is removed from the view list on a later tick still
reports the weapon it launched with.

### A2 — the projectile prop geometry helper

**Files:** new `src/Hukbo.Client/Rendering/ProjectileGeometry.cs`, new
`tests/Hukbo.Client.Tests/ProjectileGeometryTests.cs`.

A pure static helper, no `GraphicsDevice` and no `SpriteBatch`, following the
established pure-helper testability pattern. It takes the weapon role, the
screen-space current position, the travel direction, and the camera zoom, and
returns a `ProjectilePropLayout` describing at most two stroked segments
centred on the current position and rotated to the direction of travel.

Silhouettes, every one a **Provisional reconstruction** under `CLAUDE.md`
section 7 and commented as such — no source gives the drawn proportions of a
projectile, and none of these may be presented as a measurement:

| Weapon | Shaft | Second element |
| --- | --- | --- |
| Bangkaw — Long Spear | The longest of the three | A head at the leading end |
| Busog — War Bow | Short and thin | A fletch at the trailing end |
| Imported Arquebus | None | A small round ball, one element only |

The four melee roles are unreachable here — a melee weapon emits no `Release`
event — but the switch is exhaustive and throws on them rather than falling
through, matching `PawnAppearanceFactory.ToWeaponRole`.

**Verification.** A test per weapon asserting the three silhouettes are
mutually distinct in element count and length; a rotation test asserting the
layout's angle equals the travel direction over the four cardinal directions
and two diagonals; a test asserting the prop is centred on the current position
rather than anchored at either end, which is the whole defect being fixed; and a
test asserting no layout exceeds two elements, which is what the budget term in
A4 is stated against.

### A3 — draw the prop

**Files:** `src/Hukbo.Client/ArenaGame.Rendering.cs`.

Replace `DrawProjectiles`'s launch-anchored `DrawProjectileShaft` call with a
draw from the A2 layout. Direction comes from `Destination - Origin`, which is
fixed for the flight's whole life, so the prop does not swing as the shot
travels. A flight whose destination equals its origin — a release with no
resolved target — draws unrotated rather than not at all.

The prop stays ungated by detail tier. `DrawProjectiles`'s existing doc comment
records why, and that reason is unchanged: at low detail this may be the only
thing telling a spectator a ranged unit exists.

**Verification.** The gate, plus the smoke rows in section 5. This task edits a
method that needs a `SpriteBatch`, so its correctness rests on A2's tests, which
is the point of the pure-helper split.

### A4 — restate the budget

**Files:** `src/Hukbo.Client/Rendering/SubmissionCount.cs`,
`tests/Hukbo.Client.Tests/RenderBudgetEstimateTests.cs`.

Raise `ProjectileQuadsPerProjectile` from 1 to 2 and rewrite the arithmetic
block above it with section 2's corrected figures, the measurement, and the
embedded term from B4. The two ceiling constants do not move.

**Verification.** `RenderBudgetEstimateTests` recomputes the per-pawn term from
`PawnQuadCount.Count` every run rather than repeating a literal; extend it with
the projectile and embedded terms in the same style, so the worst case is
re-derived rather than pinned as a number that can drift.

### B1 — the embedded projectile record and its system

**Files:** new `src/Hukbo.Client/Rendering/EmbeddedProjectile.cs`, new
`src/Hukbo.Client/Presentation/EmbeddedProjectileSystem.cs`, new
`tests/Hukbo.Client.Tests/EmbeddedProjectileSystemTests.cs`.

A fixed 256-slot ring buffer allocated once at construction, oldest evicted
first, with a `Clear` for the round reset. It is a bounded presentation
population, not a cache: nothing is recomputed from it and nothing derives from
it, exactly as `TrampleMarkSystem` already is.

It is fed from `StartContact(AttackContactBundle)`, not from the raw event
stream. That is the same boundary `HitEffects`, `Blood`, `ClashEffects`, and
`DefenderReactions` were all migrated onto by attack-animation-v2, and driving
this one from `IngestTick` instead would embed every arrow one animation early.

A contact embeds when its weapon is `Bangkaw` or `Busog` and its resolution is
`Landed` or `ShieldBlocked`. `Arquebus` embeds nothing — a lead ball does not
stand out of a wound. An evaded or parried shot embeds nothing.

**Verification.** Capacity never exceeded across 1,000 contacts; eviction is
oldest-first; an Arquebus contact adds no slot; an evaded contact adds no slot;
a `ShieldBlocked` contact records the shield rather than a body part; a
`Landed` contact records the bundle's own `HitLocation`. Each proven to fail by
deleting the thing it protects.

### B2 — the attachment geometry

**Files:** `src/Hukbo.Client/Rendering/ProjectileGeometry.cs` (extended), tests
in `tests/Hukbo.Client.Tests/ProjectileGeometryTests.cs`.

A pure function from a `PawnLayout` plus a `BodyPart` (or the shield flag) to
the anchor point the projectile attaches at, reusing the rectangles the layout
already computes — `HeadBounds` for the head group, `TorsoBounds` for the trunk
group, the leg rectangles for the leg group, and `ShieldBounds` for a shield
attachment. It derives no offsets of its own from `WeaponStart` or
`ShieldBounds`, per the composed-layer rule in `PawnLayout`'s doc comment.

Per-projectile angle jitter derives from the event sequence and the two entity
identifiers through the same mixing `BloodGeometry.CreateBurstSeed` uses. No new
random stream and never `System.Random`.

**Verification.** Every one of the thirteen `BodyPart` values resolves to an
anchor inside the pawn's own `VisualBounds`; a shield attachment resolves inside
`ShieldBounds`; the same inputs produce the same angle on repeat calls.

### B3 — draw the embedded projectiles

**Files:** `src/Hukbo.Client/ArenaGame.Rendering.cs`,
`src/Hukbo.Client/Presentation/PresentationCoordinator.cs`.

Wire the system into the coordinator alongside the others — constructed with
its capacity, cleared in `ResetFor`, and started from
`ReleaseAttackContactsForDraw` next to `HitEffects.StartContact`. Draw inside
the existing pawn loop, after `PawnRenderer.DrawLayout`, gated on
`DetailTierGate.ShouldDraw(apparentScale, VisualDetailTier.Medium)`.

Because the draw is a step inside the pawn loop, a pawn the loop skips — dead
past its lethal hold, or culled — draws no embedded projectiles, which is
section 3's behaviour with no extra code to produce it.

**Verification.** The gate, plus the smoke rows. Correctness rests on B1 and B2.

### B4 — the embedded budget term

Folded into A4 rather than done twice: one edit to the arithmetic block,
carrying both new terms.

### B5 — the sweep the gate cannot do

**Files:** none necessarily; this is an audit with a written result.

`Hukbo.Client` carries weapon-keyed and role-keyed switches that a new drawn
element can leave stale, and the gate is structurally blind to every one of
them — on 2026-08-09 four separate `ArgumentOutOfRangeException` crashes on
`Arquebus` hid behind a fully green gate. This change adds two new
weapon-keyed switches (A2's silhouette switch, B1's embed predicate).

Anchor the sweep on both `Itak =>` and `Kampilan =>` arms and again at file
level, because one file can carry a complete switch and a stale one. Record
what was swept and what was found.

## 5. Verification criteria

1. `./scripts/verify.ps1 -SkipBootstrap` green, with real output pasted.
2. Both Hukbo hashes byte-identical to the recorded seed-1 baseline —
   `stateHash 1B73FC5923879AA0`, `eventHash AC55684F24D39344`. This change is
   presentation-only; a moved hash means something reached the simulation and
   the change is wrong, not that the baseline needs updating.
3. `./scripts/verify.ps1 -SkipBootstrap -Game Sandata` green. Sandata is
   untouched, but the two games share `scripts/` and the Client suite pins the
   shell scripts, so both gates are run rather than one inferred from the other.
4. New smoke rows added to `docs/development/testing.md` as `PENDING`, for a
   person at an interactive desktop. **No agent may flip one.** The rows are:
   a spear in flight reads as a travelling object rather than a growing line;
   an arrow in flight is visibly shorter and fletched; an arquebus shot is a
   ball with no shaft; an arrow stands out of the body part it struck; an arrow
   stands out of a shield that blocked it; an embedded arrow rides its host as
   the host walks; embedded arrows disappear when the camera pulls out.

## 6. Out of scope

Unchanged from the design's section 7, and restated because each is a boundary
a task could drift across: no physics, nothing ricochets or falls; no
projectiles on the ground, which is a separate bounded population with its own
budget; no ammunition of any kind; no change to flight timing, accuracy, or
damage; and no corpse layer, per section 3.
