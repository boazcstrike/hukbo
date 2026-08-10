# Movement gait animation — design

Status: design only. This document does not authorize implementation. The
ordered task list lived in the movement gait animation plan, now archived out
of `docs/plans/`.

## 1. What the spectator asked for

A warrior that is moving should look like it is moving. Today it does not. A
pawn is a ground ring, a torso capsule, a head, an optional head treatment, an
optional shield block, and a weapon line
(`src/Hukbo.Client/Rendering/PawnGeometry.cs:74`). It has no legs and no feet.
The only animation any pawn plays is the weapon swing arc, driven by
`SwingAnimationSystem` and `SwingGeometry`. A warrior crossing fifty units of
ground and a warrior standing still in a shield wall are drawn identically,
frame for frame, except that one of them is at a different screen position.

This feature adds drawn legs and feet, and animates them so that a moving
warrior takes visible steps. The step cadence and stride length follow how fast
the warrior is actually travelling, so a spectator can tell walking from
running without opening the HUD.

## 2. Scope boundary

This is a presentation feature and nothing else. `Hukbo.Core` is not touched.
No simulation field is added, no tick stage changes, no event is emitted, and
the state hash, the event hash, and every golden expectation stay byte for byte
what they are today. The canonical gate's determinism workload runs in
`Hukbo.Headless`, which never constructs a pawn layout, so it cannot observe
this feature at all.

The client already respects the rule that it may not decide targeting, damage,
retreat, or victory. Gait respects the same boundary from the other side: the
simulation decides where a warrior is, and the client decides what that looks
like.

## 3. The signal problem, and the decision that follows from it

`AgentView` already carries movement description — `MovementPaceRaw`, `Facing`,
`TacticalPosture`, `FootworkPhase`, `FootworkTicksRemaining`
(`src/Hukbo.Core/Simulation/AgentView.cs:119`). It would be the obvious input.
It cannot be used, and the reason is decisive.

Every one of those fields is populated only under a movement preset whose
`UsesEquipmentRelativeFootwork` is `true`, which means the V6 and V7
equipment-relative footwork presets. The shipped default is
`MovementPresetId.PersistentContingentsV4`
(`src/Hukbo.Core/Simulation/Scenario.cs:88`). Under that preset,
`MovementPaceRaw` is `0` for every warrior on every tick, `Facing` is
`Facing16.None`, and `FootworkPhase` is `FootworkPhase.None`. A gait system
reading those fields would animate nothing in the game as it actually ships,
and would spring to life only if somebody flipped the default preset — a flip
that is currently blocked on calibration work unrelated to rendering.

**Decision.** The gait system derives motion from the one signal that is always
present: the change in a warrior's authoritative position between one ingested
tick and the next. `XRaw` and `YRaw` are populated under every preset, for
every living warrior, always. The client keeps the previous tick's position per
entity in its own presentation store and takes the difference.

This is a strictly client-side derivation of a client-side quantity. It reads
authoritative state the client already holds and writes nothing back.

## 4. Phase is advanced by distance, not by time

The stride phase advances in proportion to the ground the warrior has covered,
not in proportion to elapsed seconds. One full stride cycle is completed per
fixed distance travelled.

Four problems disappear at once because of this choice.

- **Foot sliding.** A time-driven cycle desynchronizes from actual travel the
  moment a warrior speeds up or is blocked, and the feet visibly skate. A
  distance-driven cycle cannot, because the phase and the position advance from
  the same number.
- **Playback speed.** The spectator can run the battle at 1x, 2x, or 4x.
  `SwingAnimationSystem` had to solve this by advancing on speed-scaled
  presentation seconds (`src/Hukbo.Client/Presentation/SwingAnimationSystem.cs:10`).
  A distance-driven phase needs no scaling at all: at 4x the warrior covers
  ground four times as fast and takes steps four times as fast, which is
  correct.
- **Pause.** A paused battle ingests no ticks, so no distance accumulates, so
  the phase freezes exactly where it was. No special case is written.
- **Wall clock.** Nothing in the gait path reads elapsed seconds, so nothing in
  it can drift with frame rate.

Idle is the one case that needs a time-like term, and it does not get one. A
warrior whose displacement is zero for a tick has its phase eased back toward
the neutral standing stance by a fixed fraction per ingested tick. Ticks, not
seconds. A paused game therefore also freezes the return to stance, which is
the consistent behaviour.

## 5. Walk and run are different poses, not one pose at two speeds

The acceptance criterion is that a spectator can distinguish walking from
running. Playing the same cycle faster does not achieve that at a glance,
especially at the pawn sizes this game draws.

Two gait modes are resolved from the per-tick displacement, against one
provisional threshold expressed in raw units per tick:

| Mode | Selected when | Reads as |
| --- | --- | --- |
| `Stance` | displacement is zero | Feet planted, legs vertical, no motion |
| `Walk` | displacement is below the run threshold | Moderate stride, low foot lift, upright torso |
| `Run` | displacement is at or above the run threshold | Longer stride, higher foot lift, forward torso lean |

`Run` differs from `Walk` in three channels simultaneously — stride length,
foot lift height, and a small forward lean of the body anchor — so the
difference survives being three pixels tall. The lean reuses the existing
`SwingPose.TorsoLeanX`/`TorsoLeanY` mechanism conceptually but is carried on
the gait pose, not on the swing pose, and the two are summed at the one place
the body anchor is computed (`PawnGeometry.CreateBodyAnchor`, line 765).

The threshold is a **provisional tuning value**, marked as such in code, and is
not presented as a measurement of anything historical.

## 6. Warriors must not march in lockstep

A phase derived only from distance travelled would put every warrior in a
contingent that advanced together on exactly the same foot at exactly the same
moment. Two hundred warriors stepping in perfect unison reads as a rendering
bug, not as an army.

Each warrior receives a fixed phase offset derived deterministically from its
`EntityId`, in the same spirit as the existing presentation salts
(`src/Hukbo.Client/Presentation/PresentationSalts.cs`). The offset is stable for
the life of the entity, contains no randomness, and never reaches simulation
state.

## 7. Geometry

`PawnLayout` gains four rectangles: a left and a right leg, and a left and a
right foot. They are built in `PawnGeometry` from the pose-invariant
proportions plus the gait pose, exactly the way the weapon line is built from
the swing pose.

Placement rules:

- **The torso is shortened to make room.** This is the part of the change that
  restructures the body rather than adding to it. The torso capsule was twelve
  layout units tall and ran down to within one unit of the foot anchor, which
  left no band for a leg at all — the first implementation squeezed a leg into
  that one-unit gap and produced a limb that was two pixels tall and rounded to
  zero height at some zooms. The torso is now eight units, and the leg band
  takes the four units below it, so a warrior reads as head, torso, and legs.
  The leg-plus-foot span is roughly a third of the head-top-to-ground-ring
  height.
- Legs hang from the bottom of the shortened torso capsule down toward the foot
  anchor. The torso's bottom edge is computed from the leg band rather than
  from a fixed one-unit gap (`PawnGeometry.CreateTorso`).
- **The head, shield, armor, sash, and adornment accents move up with the
  torso's new top edge.** Their own sizes and formulas are untouched; they are
  positioned against the torso and the torso moved. Several pinned regression
  rectangles move as a direct consequence, and that is a deliberate outcome of
  this change rather than a defect to be re-pinned around. The overall
  silhouette height is preserved to within a pixel.
- Feet sit at the bottom of the legs, at or just above the ground ring.
- The ground ring stays exactly where it is, on the planted foot anchor. It is
  not replaced by the feet and it is not moved by the gait pose. It remains the
  pose-invariant footprint that the shield's left edge and the cull rectangle
  are measured from.
- One leg swings forward while the other swings back, along the screen-space
  direction of travel. The lifted foot rises; the planted foot does not.

Feet are drawn **bare**. The research is explicit that no footwear is
documented for these warriors, and this is the strongest evidence tier in play
here: bare feet are **Documented**
(`docs/research/improve-visuals/warrior-appearance-historical-research.md`,
footwear entry, and its prohibited-combination rule that no preset gets shoes).
No sandal, wrap, or boot variant is added, now or later, without new evidence.
Leg tone follows the existing appearance layer that already governs the
loincloth and skin tone; this feature adds moving geometry, it does not invent
a new garment.

The stride amplitude, foot lift, leg width, and leg length are **provisional
reconstruction** as drawing choices, marked provisional in code comments, and
are never presented as measurements.

## 8. The cull rectangle stays pose-blind

`PoseBlindVisualBounds` exists so that the set of pawns the renderer draws is
not a function of animation phase — otherwise the same tick would produce a
different draw list depending on where each animation clock happened to sit
(`PawnGeometry.cs:124`, `PawnRenderer.GetBounds`). Legs and feet move, so they
threaten that rule directly.

They do not break it. The pose-blind path folds in the legs and feet at the
**neutral stance**, passing `default(GaitPose)` through the same
`CreateLegsAndFeet` the posed path uses, exactly as it already passes a default
swing pose through `CreateBodyAnchor` and `CreateWeaponLayout`. The rectangle
therefore stays a pure function of apparent scale and remains bit-identical to
what `PawnRenderer.GetBounds` returns.

This was implemented in preference to the maximum-stride envelope this section
originally specified, and the reason is worth recording. `GetBounds` is frozen
and takes no gait pose, and four existing tests require `PoseBlindVisualBounds`
to equal it across the whole input grid. An envelope larger than the neutral
stance would break that pre-existing contract to buy a guarantee the renderer
does not need, because the legs are inscribed inside the ground ring's
horizontal span and never reach below its bottom edge — both properties are
tested. The accepted residue is the same one the swinging weapon already
carries: a limb at maximum extension, on a pawn sitting exactly on the cull
boundary, can in principle be culled a pixel before it would have left the
screen.

`ConservativePawnCull`'s documented radius is re-derived if, and only if, the
envelope extends past the ground ring's current extent. Legs and feet sit
inside the existing footprint by construction, so the expectation is that it
does not move; the task list requires this to be checked and stated rather than
assumed.

## 9. Detail tiers and motion settings

| Tier | Threshold | Legs and feet |
| --- | --- | --- |
| `Low` | apparent scale below `0.95` | Not drawn at all. The pawn is a handful of pixels tall; the ground ring remains the footprint. |
| `Medium` | below `1.80` | Drawn and animated. |
| `High` | at or above `1.80` | Drawn and animated, with the foot rectangles separated from the leg rectangles. |

This follows the tier discipline the armor and sash layers already use — armor
contributes tone from Low up but a separate silhouette only from Medium up
(`PawnGeometry.cs:1084`), the sash is Medium and up (line 1115), and the
adornment accents are High only (line 1156). The shield is the deliberate
exception that draws at every tier, because a shield changes what a warrior
*is*; gait describes what a warrior is *doing*, which is the transient case, so
it is tier-gated rather than always on.

`MotionIntensity` is honoured as follows.

| Setting | Behaviour | Precedent |
| --- | --- | --- |
| `Full` | Full stride amplitude. | — |
| `Reduced` | Legs and feet still drawn, still animated, at reduced stride amplitude and foot lift. | `GrassSway`'s reduced amplitude factor. |
| `Off` | Legs and feet drawn in the static neutral stance. No phase advances. | `GrassSway` zeroing amplitude; `DustEffectSystem` suppressing spawns. |

The feature is a legibility feature, not an ambient flourish, so `Off`
suppresses the motion and keeps the limbs. A spectator who has turned motion
off should still see a warrior with legs.

## 10. Dead warriors

No special case is needed and none is added. The arena draw loop skips every
agent whose `IsAlive` is false before it builds any geometry
(`src/Hukbo.Client/ArenaGame.Rendering.cs:885`, and again at line 430 in the
probe pass). A corpse is not drawn as a pawn at all, so it cannot run in place.
The gait store drops any entity that is absent from, or not alive in, the views
it ingests, so a dead warrior's phase does not linger for a later round.

## 11. The nine questions

Quoted from `SIMULATION-GAME-STANDARDS.md` §10, lines 322 to 330.

**1. User-visible outcome.** A moving warrior visibly takes steps: legs swing
and feet lift and plant, at a cadence and stride that track how fast it is
actually crossing the ground. Walking and running are distinguishable at a
glance, without the HUD, without selecting anything, and without reading source
code.

**2. Tick stage and state read/written.** No tick stage. The simulation is not
modified. On the client, the gait store ingests the same completed-tick
`AgentView` list that `SwingAnimationSystem`, `DustEffectSystem`, and
`TrampleMarkSystem` already ingest, and the draw path resolves one pose per
pawn. State written: the client's own per-entity previous position and stride
phase. State read: `EntityId`, `XRaw`, `YRaw`, `IsAlive`.

**3. Numeric units and bounds, and the same-tick conflict rule.** Displacement
is in raw fixed-point units per tick, the same unit `XRaw`/`YRaw` already use.
Phase is a bounded fraction of one stride cycle, wrapped into `[0, 1)`. Stride
amplitude and foot lift are in the same layout units every other
`PawnGeometry` offset uses, multiplied by apparent scale. No same-tick conflict
exists: the value is a pure read of already-resolved authoritative state, and
two warriors cannot contend for it.

**4. Total ordering and random-stream policy.** No random stream. No
`System.Random`, no `SplitMix64` draw, no RNG of any kind. The per-warrior
phase offset is a deterministic function of `EntityId`. The store is iterated in
the order the views are supplied, which is the stable agent-array order, and no
gait value is order-dependent in any case.

**5. Cache source and invalidation, or "no cache".** The previous-position and
phase store is presentation state with a fixed capacity, not a cache of a
derivable value: it is the record of a quantity that no longer exists once the
next tick overwrites the views. It is cleared on round reset alongside the other
presentation systems. No cache is added, and nothing derived is saved.

**6. Save, event, or version effect, or "presentation only".** Presentation
only. No save field, no event, no preset version, no golden expectation, no
change to the state hash or the event hash.

**7. Worst-case complexity and benchmark workload.** O(1) per living warrior
per ingested tick for the store, and O(1) per drawn pawn per frame for the pose
lookup and the four rectangles. No heap allocation on the draw path, matching
the rule the swing resolver already states. The relevant workload is the
200-agent benchmark, and the render budget estimate's 200-unit and 500-unit
quad figures must be updated in the same change, because four more rectangles
per drawn pawn is a real cost that belongs in the budget rather than in a
surprise.

**8. Spectator explanation.** The animation is the explanation. Movement is
already visible as position change; this makes the *manner* of that movement
legible. No reason code, event, or inspector field is added, because no new
autonomous decision is being made.

**9. Tests that fail before implementation and pass afterward.** A standing
warrior resolves the neutral stance; a warrior displaced by a walk-magnitude
step resolves a walk pose with nonzero stride; a warrior displaced by a
run-magnitude step resolves a run pose with a longer stride and a forward lean;
two warriors with different `EntityId`s moving identically resolve different
phases; ingesting no ticks advances no phase; `MotionIntensity.Off` resolves
the neutral stance at every displacement; a Low-tier layout produces empty leg
and foot rectangles; the pose-blind bounds are identical for two different gait
phases at the same position.

## 12. Non-goals

- No change to `Hukbo.Core`, including no new `AgentView` field.
- No pathfinding, terrain, footprint decals beyond the trample marks that
  already exist, or foot-to-ground physical contact solving.
- No per-limb hit detection or per-limb damage. Hit location remains metadata
  only, exactly as `src/Hukbo.Core/Combat/BodyPart.cs:8` states.
- No facing decision moved into or out of the simulation.
- No footwear.
- No new `MotionIntensity` level and no settings-file schema change.
- No dependence on `MovementPaceRaw`, `Facing`, or `FootworkPhase`. If the
  default preset later flips to one that populates them, this design still
  works unchanged, and using them then would be a separate, evidence-backed
  refinement.

## 13. Risks

1. **Readability at scale.** Four more moving rectangles per pawn at several
   hundred pawns can turn a formation into noise. Mitigated by the Low-tier
   gate, which removes them entirely at exactly the zoom where whole formations
   are being watched.
2. **Quad budget.** The per-pawn quad count rises. The pinned counts and the
   render budget estimate must move in the same change, with the arithmetic
   stated in the commit message.
3. **Silhouette confusion.** A swinging leg must not read as a second weapon or
   cross the shield block. Mitigated by drawing legs beneath the torso, before
   the shield and weapon, and by bounding the stride inside the ground ring's
   width.
4. **Pinned layout regressions.** Several tests pin exact rectangles for the
   torso, head, shield, and visual bounds. Legs must not move any of them; if a
   pinned rectangle changes, that is a defect in the change, not a test to
   re-pin.
5. **The lossy-tool-output hazard.** `PawnGeometry.cs` is long and heavily
   commented, and reads of it come back with words dropped. Every edit to it
   must confirm its anchor string against the file rather than against a
   remembered rendering of it.

## 14. Open questions

1. Should the run threshold be expressed as an absolute raw-units-per-tick
   constant, or as a fraction of the fastest loadout profile's step? The
   absolute constant is simpler and is what this design assumes; the fractional
   form would self-adjust if movement tuning changes. **Recommendation:**
   absolute constant, marked provisional, revisited only if it visibly
   mis-classifies.
2. At `High` tier, should the foot rectangles be tinted separately from the
   legs, or share the tone? **Recommendation:** share the tone initially; a
   separate skin tone for bare feet is a one-line follow-up once the motion
   itself is proven on screen.
