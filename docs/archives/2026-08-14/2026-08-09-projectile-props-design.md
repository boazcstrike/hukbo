# Projectile props and embedded projectiles — design

**Archived: reference only.** This is a finished design. It shipped at
`3ec5523` on 2026-08-11, its plan answered all five of the open decisions in
section 8 and corrected this document's own quad arithmetic, and all eight of
its smoke rows closed `PASS`, the last of them `PP-3` on 2026-08-13. Never
execute it, never treat it as a live task list, and never cite it as the reason
to make a change. The live contract for this project remains `CLAUDE.md` and
`docs/development/smoke-checklist.md`; nothing in this file overrides either of
those. Archived 2026-08-14. **The status line below is the state this document
was written in and is no longer true** — read section 8's decisions as questions
that have since been answered elsewhere, not as open ones.

Status: **design only.** A design document does not authorize implementation
(`CLAUDE.md` section 6). Nothing here has been built, and the open decisions in
section 8 need answers before a plan document is written.

## 1. What a spectator sees today, and why it reads wrong

`ArenaGame.DrawProjectiles` (`ArenaGame.Rendering.cs:764`) draws one stretched
pixel per live flight, from the flight's **launch point** to its current
interpolated position:

```csharp
DrawProjectileShaft(spriteBatch, pixel, origin, current);
```

`origin` is `flight.OriginXRaw / OriginYRaw`, which never changes for the life of
the flight. So what the spectator sees is not a projectile travelling through the
air. It is a line anchored at the thrower that grows longer every tick until the
shot lands, then vanishes. At the moment of impact it is at its longest, which is
exactly backwards from how a missile reads: a real one is small, and the eye
tracks its position rather than its trail.

That is the whole of the observed complaint. The line is not a placeholder for a
projectile — it is a different object drawn correctly.

## 2. What this design proposes

Two changes, related but separable, and worth keeping separable because their
costs are very different.

**A. An in-flight prop.** Replace the launch-anchored line with a short shaft of
fixed world length, centred on the projectile's current position and rotated to
its direction of travel. Three silhouettes, one per ranged weapon:

| Weapon | Silhouette | Evidence tier |
| --- | --- | --- |
| Bangkaw — Long Spear | A long shaft, the longest of the three, with a visible head | Provisional reconstruction |
| Busog — War Bow | A short thin shaft with a fletched tail | Provisional reconstruction |
| Imported Arquebus | A small round ball, no shaft | Provisional reconstruction |

Every silhouette is a **Provisional reconstruction** under `CLAUDE.md` section 7.
The weapon classes themselves are Documented or Documented, form uncertain — the
evidence notes in `PawnAppearance` already carry that — but no source gives the
proportions of a drawn projectile, and none of these shapes may be presented as a
measurement.

**B. Embedded projectiles.** When a shot connects, leave the projectile stuck in
what it hit, and let it ride with that pawn as it moves. Two attachment targets,
and the simulation already distinguishes them:

- **A body part.** `BattleEvent.HitLocation` carries the resolved `BodyPart`, and
  `PawnGeometry` already computes an anchor for each part in order to draw the
  pawn. An arrow in the chest attaches to the chest anchor.
- **A shield.** `AttackResolution.ShieldBlocked` says the board took it, and the
  pawn's `ShieldId` says there is a board to take it. An arrow in the shield
  attaches to the shield face rather than to a body part.

An arquebus ball embeds nothing — a lead ball does not stand out of a wound — so
the Arquebus contributes to A and not to B. That asymmetry is the point of
separating the two features rather than treating "projectile visuals" as one
thing.

## 3. The constraint that decides the shape of this: the quad budget

This is the part that must be settled before anything is written, because it is
already tight and this repository has recorded a warning about exactly this
feature.

`SubmissionCount.cs:466-493` carries the accounting. At 500 visible units:

```
(27 quads/pawn x 500 units) + 4,032 backdrop  = 17,532 quads
17,532 + (512 x 1 projectile quad)            = 18,044 quads
ceiling ArenaBatchQuadsAt500UnitsEstimate     = 20,000 quads
headroom                                      =  1,956 quads
```

And the note directly above it, written when RU-42 landed:

> the 500-unit margin has fallen from 3,468 to 1,956 across RU-23 and RU-42, so
> the next feature that wants a per-pawn quad owes a fresh measurement rather
> than an assumption.

**This feature is that next feature.** It wants per-projectile quads *and*
per-pawn quads, which is the more expensive of the two shapes the note
anticipated.

A naive implementation does not fit. An arrow drawn as shaft, head and fletching
is three quads; at the 512-projectile cap that is 1,536 quads for feature A alone,
leaving 420 for feature B across 500 pawns. Feature B at even one stuck arrow per
pawn would need 500 to 1,000 quads on its own.

So the budget has to be designed rather than discovered:

| Term | Proposed | Quads at 500 units |
| --- | --- | --- |
| In-flight prop | 2 quads per flight (shaft plus head or fletch), 512 cap | 1,024 |
| Embedded projectiles | Global bounded pool, 256 slots, 2 quads each | 512 |
| **Added total** | | **1,536** |
| Remaining headroom | | **420** |

420 quads of headroom at 500 units is thin enough that it should not be accepted
on this arithmetic alone. Two ways out, and they are not exclusive:

1. **Measure and raise the ceiling.** `ArenaBatchQuadsAt500UnitsEstimate` is an
   estimate, and `tools/Hukbo.Tools.RenderProbe` with `HUKBO_RENDER_PROBE=1`
   exists precisely to measure the real cost. If the real batch cost at 500 units
   is comfortably under 20,000, the ceiling can move on measured evidence.
2. **Gate the embedded pool behind the detail tier.** `DetailTierGate` already
   exists and already drops work at low detail. Embedded projectiles are the most
   droppable thing in this proposal: they are decoration on a pawn that is already
   drawn. At 500 units the camera is far enough out that a stuck arrow is a few
   pixels.

The in-flight prop must **not** be detail-gated. `DrawProjectiles` documents why:
at low detail that line may be the only thing telling a spectator a ranged unit
exists at all.

## 4. The bounded pool, and why it is not a cache

`CLAUDE.md` section 9 forbids adding any unbounded cache. Embedded projectiles are
the obvious way to violate that: a 10,000-tick battle lands tens of thousands of
shots, and a naive list of "arrows currently stuck in people" grows without limit.

The proposal is a fixed-capacity ring buffer of 256 embedded projectiles,
allocated once at construction, oldest evicted first. It is not a cache — nothing
is recomputed from it and nothing else is derived from it. It is a bounded
presentation population, exactly like `BackdropQuadCount.TrampleMarks` and
`.Decals`, which the budget comment already names as the precedent for "one quad
per live instance".

Eviction is visible and that is acceptable: an arrow that has been in a warrior's
shield for two hundred ticks disappearing while the camera is elsewhere costs
nothing. What matters is that the most recent hits are the ones still shown.

## 5. Where the data comes from, and what Core must not learn

Everything this feature needs already exists on the client side of the boundary.

| Need | Source | Already exists |
| --- | --- | --- |
| That a shot was fired | `BattleEventKind.Release` | Yes — `ProjectileFlightSystem.Ingest` |
| The launching weapon | The source agent's `AgentView.Loadout.Weapon` | Yes — the pattern `SoundDirector.ResolveReleaseSound` already uses, because a Release event cannot name its own weapon |
| Where the shot is now | `ProjectileFlight.CurrentXRaw/CurrentYRaw` | Yes |
| That it connected | `BattleEventKind.Attack` with a ranged weapon | Yes |
| What it hit on the body | `BattleEvent.HitLocation` | Yes |
| That the shield took it | `AttackResolution.ShieldBlocked` | Yes |
| Where that body part is on screen | `PawnGeometry` anchors | Yes |

**`Hukbo.Core` gains nothing.** No new event, no new field, no simulation state,
no knowledge that a projectile has a shape or that anything is stuck in anyone.
`ProjectileFlight` needs a `WeaponId`, and that is a client-side record in
`Hukbo.Client/Rendering`, resolved from a view the client already holds.

One consequence worth stating plainly: because the weapon is resolved from the
launcher's current view rather than from the event, a launcher that dies during
its own projectile's flight cannot be looked up. The existing sound path already
has this hole and answers it by returning no cue. The projectile prop should
answer it by falling back to the arrow silhouette rather than by disappearing,
because a shot in the air that vanishes mid-flight is a worse artefact than a shot
drawn with the wrong shaft.

## 6. The nine questions (`SIMULATION-GAME-STANDARDS.md` section 10)

1. **User-visible outcome.** A ranged shot reads as an object travelling through
   the air rather than as a line stretching from the thrower, and a landed shot
   leaves a visible projectile in the body part or shield it struck, which rides
   with the pawn.
2. **Tick stage and state read/written.** None. No tick stage is touched. The
   presentation layer reads `LastEvents` and `Agents` after the tick, exactly as
   `SoundDirector` and `ProjectileFlightSystem` already do.
3. **Numeric units and same-tick conflict rule.** Prop lengths are in world units
   converted through the existing camera transform. Two shots landing on the same
   body part on the same tick both embed; the ring buffer orders them by event
   sequence, which is already totally ordered.
4. **Total ordering and random stream.** Any per-projectile visual jitter derives
   from the event sequence and entity identifiers through the same mixing
   `BloodGeometry.CreateBurstSeed` uses. No new random stream, and never
   `System.Random`.
5. **Cache.** No cache. A fixed-capacity ring buffer allocated once, evicting
   oldest first — see section 4.
6. **Save, event, version effect.** Presentation only. No snapshot field, no event
   change, no preset version bump. Both state hashes and both event hashes must be
   byte-identical before and after.
7. **Worst-case complexity and benchmark workload.** Linear in live flights
   (capped at 512) plus embedded slots (capped at 256), per frame. The workload is
   the 500-agent V5/V8 case, reported through `HUKBO_RENDER_PROBE=1` against the
   budget in section 3.
8. **Spectator explanation.** This *is* the spectator explanation — it is the
   answer to "can a spectator discover this effect without reading source code?"
   for the whole ranged package. A spectator currently cannot tell an arrow from a
   thrown spear from a lead ball. Afterwards they can.
9. **Tests that fail before and pass after.** A prop geometry test per weapon
   asserting distinct silhouettes and correct rotation; an embedding test
   asserting a shield-blocked hit attaches to the shield and a landed hit attaches
   to the named body part; a ring-buffer test asserting capacity is never exceeded
   and eviction is oldest-first; a budget test extending
   `RenderBudgetEstimateTests` with the new terms. Each must fail when the thing it
   protects is deleted, and be proven to by deleting it.

## 7. What this design deliberately does not do

- **No physics.** `CLAUDE.md` section 9 forbids rigid bodies. Nothing ricochets,
  falls, or collides. An embedded projectile is attached to an anchor, not
  simulated.
- **No projectiles on the ground.** A miss currently plays a `miss-` cue and
  nothing else. Ground litter is a separate bounded population with its own budget
  and is not proposed here.
- **No ammunition.** `CLAUDE.md` section 9 defers quiver sizes and any
  stock-and-consumption model. Drawing an arrow does not imply counting them.
- **No change to flight timing, accuracy, or damage.** This is entirely
  presentation.

## 8. Open decisions — these need answers before a plan is written

1. **Does an embedded projectile survive the pawn's death?** A corpse bristling
   with arrows is the more striking image and costs nothing extra, since the ring
   buffer is capacity-bound either way. The alternative is clearing a pawn's
   embedded projectiles when it dies, which frees slots for living targets.
2. **Do embedded projectiles fade, or persist until evicted?** A fade is one more
   term per slot and makes eviction invisible. Persisting is simpler and makes a
   long battle accumulate visible damage.
3. **Is 256 embedded slots the right number,** or should it scale with the agent
   count the way the projectile cap does through `MaximumProjectilesInFlight`?
4. **Detail-tier gating for the embedded pool: yes or no?** Section 3 argues yes
   and that it is the cheapest way to buy back headroom. It means stuck arrows
   disappear when the camera pulls out.
5. **Ceiling measurement first, or design to the current ceiling?** Running
   `HUKBO_RENDER_PROBE` at 500 units before writing anything would replace the
   arithmetic in section 3 with a measurement, which is what the RU-42 note asks
   for. It costs one measurement run and would change the budget table.

## 9. Recommendation

Take feature A and feature B as two sequenced pieces of work rather than one.

Feature A is small, self-contained, fixes the actual complaint, costs 1,024 quads
against 1,956 of headroom, and needs none of the open decisions in section 8
answered. It could be planned immediately.

Feature B is where the design questions live, where the budget gets tight, and
where the bounded-pool discipline matters. It should follow, after a measurement
run settles question 5.
