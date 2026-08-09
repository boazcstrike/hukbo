# Ranged pose mechanics — what the procedural pose system can and cannot do today

Research note, 2026-08-07. Read-only survey of the existing procedural pose
system in `Hukbo.Client`, written so that a planner can specify twenty ranged
animation phases (five each for the Bangkaw, the Busog, the Sumpit, and the
imported arquebus) against machinery that already exists rather than against
machinery that has to be invented.

Nothing in this document authorizes implementation. It records what is on disk
at the commit this worktree is checked out at, with a file and line reference
for every claim. Where a claim could not be grounded, the document says "not
found" rather than filling the gap.

**Discovery-tool note.** `CLAUDE.md` section 8 requires the `tokensave` MCP
tools for code discovery. Those tools were not exposed to the process that wrote
this note (a tool search for `tokensave|codebase.?memory|search_graph` returned
no matches), so discovery fell back to `Read` and `Grep`, which the same rule
permits for reading a file before quoting it. Every line reference below comes
from a direct read of the file named.

## Sections

1. [The pawn skeleton as built today](#1-the-pawn-skeleton-as-built-today)
2. [SwingAnimationSystem, end to end](#2-swinganimationsystem-end-to-end)
3. [GaitAnimationSystem, end to end](#3-gaitanimationsystem-end-to-end)
4. [The pure-helper testability pattern](#4-the-pure-helper-testability-pattern)
5. [What the Client can actually read from the simulation](#5-what-the-client-can-actually-read-from-the-simulation)
6. [Allocation and the draw path](#6-allocation-and-the-draw-path)
7. [A recommended shape for a ranged pose resolver](#7-a-recommended-shape-for-a-ranged-pose-resolver)
8. [Every Client test that constrains this work](#8-every-client-test-that-constrains-this-work)

---

## 1. The pawn skeleton as built today

Every drawn part of a pawn is a `Rectangle` or a line segment computed by
`PawnGeometry` and returned on a single `PawnLayout` value
(`src/Hukbo.Client/Rendering/PawnGeometry.cs:81-109`). There is no sprite, no
texture atlas, no bone hierarchy, and no interpolation between authored frames.
The renderer draws a 1x1 white pixel texture stretched into each rectangle. That
is the whole of the "skeleton".

The layout is built in two halves. `CreateProportions`
(`PawnGeometry.cs:630-725`) computes everything a pose cannot change — the
apparent scale, the detail tier, the ground ring, and every whole-pixel size.
`CreateLayout` (`PawnGeometry.cs:731-857`) then positions those sizes using the
two poses it is handed. The split exists so that the cull rectangle and the
posed layout share one arithmetic pass rather than two
(`PawnGeometry.cs:470-505`).

### Scale and tier, the two numbers everything else is measured in

| Quantity | Value | Declared at |
| --- | --- | --- |
| `apparentScale` | `clamp(cameraZoom * 1.35, 0.72, 2.40) * scaleMultiplier` | `PawnGeometry.cs:661-664`, constants at `:148-150` |
| `PawnDetailTier.Low` | `apparentScale < 0.95` | `PawnGeometry.cs:665-670`, constant at `:151` |
| `PawnDetailTier.Medium` | `0.95 <= apparentScale < 1.80` | same |
| `PawnDetailTier.High` | `apparentScale >= 1.80` | same, constant at `:152` |

Every dimension below is quoted in **layout units at unit scale**. The drawn
pixel size is the unit figure multiplied by `apparentScale` and then passed
through `ToSize`, which rounds and applies a one-pixel floor
(`PawnGeometry.cs:1651-1652`).

### The body parts

| Part | Layout field | Size in units | Anchor / pin | Declared at |
| --- | --- | --- | --- | --- |
| Ground ring | `GroundRingBounds` | 13 wide by 4 tall | Centred on `footAnchor.X`, its own vertical centre half a ring-height above `footAnchor.Y`; never moved by any pose | `PawnGeometry.cs:672-678` |
| Torso | `TorsoBounds` | `TorsoHeightUnits` = **8** tall (times `StatureMultiplier`), 7 wide (times `BuildMultiplier`) | Horizontally centred on `bodyAnchor.X`; its bottom edge sits `TorsoBottomGap` above `bodyAnchor.Y` | height constant `PawnGeometry.cs:319`; width `:683`; placement `CreateTorso` `:954-966` |
| Torso bottom gap | `PawnProportions.TorsoBottomGap` | `max(1, apparentScale)` at Low tier; the **full scaled `legLength`** at Medium and High | Reserves the band the legs occupy | `PawnGeometry.cs:701-703` |
| Head | `HeadBounds` | 7 by 7 (square) | Horizontally centred on `bodyAnchor.X`; its bottom edge sits `HeadGap` above `TorsoBounds.Top` | size `:684`, gap `:685`, placement `CreateHead` `:968-976` |
| Head gap | `PawnProportions.HeadGap` | `ToSize(apparentScale)` — 1 unit | Between head bottom and torso top | `PawnGeometry.cs:685` |
| Head treatment | `HeadTreatmentBounds` | Head width by `max(1, 2.6 * scale)` tall | Pinned to the head's top-left corner | `:686`, `CreateHeadTreatment` `:978-985` |
| Left leg | `LeftLegBounds` | `LegWidthUnits` = **1.6** wide, `legLength - footHeight` tall | Centre X = `bodyAnchor.X - LegGap + (LeftLegOffsetRatio * DirectionSign * legLength)`; top = `TorsoBounds.Bottom - round(LeftFootLiftRatio * legLength)` | constants `:334`, `:321`, `:340`; `CreateLegsAndFeet` `:1011-1051`; `BuildLeg` `:1058-1071` |
| Right leg | `RightLegBounds` | same | Centre X = `bodyAnchor.X + LegGap + (RightLegOffsetRatio * DirectionSign * legLength)` | same |
| Leg length | `LegLengthUnits` | **7.5** — the full leg-plus-foot vertical span | Torso bottom down to the ground ring's bottom edge at neutral stance | `PawnGeometry.cs:334` |
| Leg gap | `LegGapUnits` | **1.5** each side of the body anchor | Horizontal separation of each leg's neutral centre from `bodyAnchor.X` | `PawnGeometry.cs:340` |
| Left foot | `LeftFootBounds` | `FootWidthUnits` = **2.2** wide, `FootHeightUnits` = **2** tall | Centred on the leg's own `Center.X`, top edge on `leg.Bottom` | constants `:342`, `:354`; `BuildFoot` `:1077-1085` |
| Right foot | `RightFootBounds` | same | same | same |
| Weapon line | `WeaponStart` -> `WeaponEnd` | Per-role start and end offsets from `bodyAnchor` (table below) | `WeaponStart` is the grip and is also republished as `WeaponGripAnchor` | `CreateWeaponLayout` `:1484-1539`; grip republished at `:841` |
| Weapon thickness | `WeaponThickness` | `max(1, roleFactor * scale)` — Itak 2.2, Kampilan 2.8, Wasay 1.9, Kalis 1.6 | Stroke width only; not a rectangle, so it never reaches the bounding union | `CreateWeaponThickness` `:1547-1562` |
| Weapon secondary | `SecondaryEquipmentBounds` | Itak: an off-hand line from `(-2,-4)` to `(-6,-11)` with 2-unit padding. Wasay: a `5 x 5.2` axe head centred on the weapon end | Empty at Low tier for every role except the Wasay | `CreateSecondaryBounds` `:1591-1612`; tier rule `:1529-1532` |
| Shield | `ShieldBounds` | 4 wide by 11 tall, plus per-skin deltas | Left edge `round(footAnchor.X - 7 * scale) - width + postureOffset`, measured from the **planted foot anchor**, so no pose moves it; top = `TorsoBounds.Top + TopOffset` | `CreateShieldBlock` `:1208-1254`; `CreateShieldRectangle` `:1260-1267` |
| Shield anchor | `ShieldAnchor` | — | The shield rectangle's own centre, computed even for an unshielded warrior | `CreateShield` `:1306-1316` |
| Shield posture | `ShieldPostureRotationRadians` | fixed `0.15` rad, offset `1` unit toward the torso, both zeroed at Low tier | Rotation applied by the renderer about the shield rectangle's own centre | constants `:264`, `:275`; offset applied `:1248-1251` |
| Armor capsule | `ArmorBounds` | Torso width times `armorWidthFactor`, torso height unchanged | Centred on the torso's own centre; empty when unarmored or at Low tier | `CreateArmor` `:1372-1391` |
| Sash | `SashBounds` | `torsoWidth - 2` wide, `round(apparentScale)` tall | Inset one pixel inside the torso, at roughly torso mid-height; empty at Low tier | `CreateSash` `:1403-1423` |
| Adornment accents | `AdornmentAccentPrimaryBounds`, `AdornmentAccentSecondaryBounds` | At most `MaxAccentPixelSizeAtApparentScale1` square | Primary inscribed at the head's right edge, secondary at the torso's top centre; High tier only | `CreateAdornmentAccents` `:1444-1482` |
| Diagnostic placeholder | `PlaceholderBounds` | `min(torsoWidth, torsoHeight)` square | Inscribed centrally inside the torso by integer arithmetic | `PawnGeometry.cs:779-784` |
| Selection halo | `SelectionBounds` | The rendered union inflated by `max(3, ceil(3 * apparentScale))` | — | `CreateSelectionBounds` `:1144-1149` |
| Swing trail | `SwingTrail` | Pivot at `WeaponStart`, radius = weapon reach length, span `0.85 * TrailStrength` rad | Omitted entirely at Low tier | `CreateSwingTrail` `:1156-1188`, constants `:163`, `:166` |

The **torso shrank from 12 units to 8** and every pin below it moved as a
consequence. The comment block at `PawnGeometry.cs:277-309` records the change
and its reason: the old `LegLengthUnits` of `1f` rounded a drawn leg to zero
height at almost every scale, so `LegLengthUnits` was raised to `7.5` and the
torso was cut to `8` to make room without growing the silhouette. One artefact
of that history is still visible in the repository: `ConservativePawnCull`'s
derivation comment at `src/Hukbo.Client/Rendering/ConservativePawnCull.cs:83-85`
still says "`12 * 1.10` units of tallest torso". That comment is stale relative
to `TorsoHeightUnits = 8f`. It does not make the cull wrong — the cull is
conservative and the weapon line, not the head stack, is what actually sets the
radius — but a planner reading that file for skeleton numbers would get the old
figure. **`PawnGeometry.cs:319` is the authority.**

### The two anchors a pose may move

- **`footAnchor`** — where the camera puts the agent's world position on screen
  (`src/Hukbo.Client/ArenaGame.Rendering.cs:895-900`). Nothing moves it.
- **`bodyAnchor`** — `footAnchor + ((swingPose.TorsoLeanX + gaitPose.TorsoLeanX)
  * apparentScale, (swingPose.TorsoLeanY + gaitPose.TorsoLeanY) *
  apparentScale)` (`CreateBodyAnchor`, `PawnGeometry.cs:945-952`). This is the
  **only** channel by which either pose moves the body, and the two poses'
  contributions are **summed**, not switched between. That is the existing,
  proven precedent for composing a third pose type.

There is no arm, no hand, no shoulder, no elbow, and no neck. The weapon line
*is* the arm: a warrior's reach is drawn as a segment from a per-role grip point
to a per-role tip point, and a swing rotates that segment about the grip. Any
ranged pose that wants "the bow hand is here and the string hand is there" has
to either invent a second line or reuse the existing weapon line plus the
existing `SecondaryEquipmentBounds` slot.

### Per-role weapon geometry (offsets from `bodyAnchor`, in units)

| Role | Grip (`Start`) | Tip (`End`) | Bounds padding | Thickness |
| --- | --- | --- | --- | --- |
| Itak | `(1, -7)` | `(9, -15)` | `2.8 * scale` | `2.2 * scale` |
| Kampilan | `(1, -6)` | `(15, -19)` | `4.2 * scale` | `2.8 * scale` |
| Wasay | `(1, -5)` | `(12, -18)` | `4.4 * scale` | `1.9 * scale` |
| Kalis | `(1, -7)` | `(14, -21)` | `3.2 * scale` | `1.6 * scale` |

Source: `PawnGeometry.cs:1496-1522` for start, end, and padding;
`:1547-1562` for thickness. Note the `switch` arms throw
`ArgumentOutOfRangeException` on an unrecognized role — adding a fifth
`PawnWeaponRole` member without adding an arm to all four of these switches is a
runtime throw, not a compile error.

---

## 2. `SwingAnimationSystem`, end to end

**Step 1 — a swing starts from a battle event, retrospectively.**
`SwingAnimationSystem.Ingest`
(`src/Hukbo.Client/Presentation/SwingAnimationSystem.cs:37-76`) walks the tick's
`BattleEvent` list and starts a swing for every event whose `Kind` is
`BattleEventKind.Attack` and whose attacker and target are both present in the
supplied `AgentView` list (`:51-63`). The direction is the unit vector from
attacker to target, computed from the two agents' `XRaw`/`YRaw`
(`ResolveDirection`, `:142-150`); two agents sharing a position give the zero
vector rather than a direction toward the origin.

This is the single most important fact for ranged work: **the attack event
already carries its `AttackResolution`** (`SwingAnimationSystem.cs:56`, and
`src/Hukbo.Core/Simulation/BattleEvent.cs:14-22`), because the simulation
resolves the whole attack in one tick stage
(`src/Hukbo.Core/Simulation/BattleSimulation.cs:3653-3678`). The client therefore
learns about a blow *after it has already landed*, and the entire four-phase
animation — anticipation included — is played backwards-in-meaning, after the
fact. There is no wind-up signal, and no state that says "an attack is coming".

**Step 2 — the store holds at most one swing per attacker.** `Upsert`
(`SwingAnimationSystem.cs:159-193`) overwrites an attacker's existing slot rather
than appending, so a warrior cannot accumulate swings. The array is
fixed-capacity, sized at construction (`:24-28`), and a full store evicts the
oldest swing, breaking an age tie on the lowest sequence (`:179-192`).

**Step 3 — phase is driven by a speed-scaled clock.** `Advance`
(`SwingAnimationSystem.cs:82-107`) adds elapsed seconds to each swing's
`AgeSeconds` and compacts out anything that has reached
`SwingAnimation.TotalSeconds`. That constant is `0.25f`
(`src/Hukbo.Client/Presentation/SwingAnimation.cs:35`), budgeted at one attack
cooldown at the default tick rate. `SwingAnimation.Progress` is
`clamp(AgeSeconds / TotalSeconds, 0, 1)` (`SwingAnimation.cs:42-43`). The clock
is fed speed-scaled seconds from `PresentationCoordinator.AdvanceEffects`, called
at `src/Hukbo.Client/ArenaGame.cs:667-669` with
`gameTime.ElapsedGameTime.TotalSeconds` and `_speedMultiplier`; without that
scaling a 4x battle would show every warrior permanently mid-swing
(`SwingAnimationSystem.cs:10-17`).

**Step 4 — progress maps to a phase and then to a pose.**
`SwingGeometry.ResolvePhase` (`src/Hukbo.Client/Rendering/SwingGeometry.cs:131-145`)
partitions `[0,1)` into four phases by cumulative share:

| Phase | Share | Cumulative end | Constant |
| --- | --- | --- | --- |
| `Anticipation` | 0.36 | 0.36 | `SwingGeometry.cs:86` |
| `Strike` | 0.20 | 0.56 | `:89` |
| `ImpactHold` | 0.20 | 0.76 | `:94` |
| `Recovery` | 0.24 | 1.00 | `:97` |

`SwingGeometry.ResolvePose` (`SwingGeometry.cs:150-199`) then linearly
interpolates four channels — weapon angle, extension ratio, torso lean, and
trail strength — between per-phase keyframes, using `ResolvePhaseProgress`
(`:201-216`) to renormalize progress within the current phase. The `ImpactHold`
and `Recovery` keyframes branch on the attack's `AttackResolution` through
`ResolveHoldKeyframe` (`:222-244`): a landed blow holds contact, a blocked,
parried, or deflected blow recoils, and an evaded blow follows through. Finally
the swing direction is folded in: the angle is multiplied by
`facing = DirectionX >= 0 ? 1 : -1`, and the lean is multiplied by `DirectionX`
and `DirectionY` separately (`:189-198`).

The output is a `SwingPose` record struct with seven fields
(`SwingGeometry.cs:49-56`). `default(SwingPose)` is documented as the neutral
standing pose (`:24-27`).

**Step 5 — pose maps to drawn geometry.** Two channels only:

- `TorsoLeanX`/`TorsoLeanY` shift `bodyAnchor`, which moves the torso, head,
  head treatment, legs, feet, armor, sash, accents, placeholder, and weapon grip
  with it (`PawnGeometry.cs:945-952`). It does **not** move the ground ring or
  the shield, both of which are measured from the planted `footAnchor`.
- `WeaponAngleRadians` and `ExtensionRatio` are consumed by `ApplySwing`
  (`PawnGeometry.cs:1576-1589`), which rotates the reach vector about the grip
  and scales it by `1 + (ExtensionRatio * ExtensionReach)` where
  `ExtensionReach = 0.35f` (`:158`).
- `TrailStrength` and the sign of `WeaponAngleRadians` build the arc trail
  (`CreateSwingTrail`, `:1156-1188`).

`Phase` and `PhaseProgress` are carried on the pose but **are not read by
`PawnGeometry` at all** — a grep for them inside the geometry finds nothing.
They exist for tests and for future consumers.

**Step 6 — resolution and lookup on the draw path.**
`SwingPoseResolver.Resolve`
(`src/Hukbo.Client/Rendering/SwingPoseResolver.cs:39-68`) is called once per
frame from `ArenaGame.Update` (`src/Hukbo.Client/ArenaGame.cs:670-673`), filling
the caller-owned `_swingPoses` dictionary (`ArenaGame.cs:149`). It early-outs
when `ActiveSwings.Length == 0` (`SwingPoseResolver.cs:50-54`), and an agent with
no swing gets **no entry**, not a neutral one (`:59-62`). The per-pawn draw loop
then calls `SwingPoseResolver.TryGetPose`
(`src/Hukbo.Client/ArenaGame.Rendering.cs:961-966`), converting a miss to a
`(SwingPose?)null`, and hands the nullable straight to
`pawnPrefix.CompletePosedLayout(swingPose, gaitPose)`
(`ArenaGame.Rendering.cs:980`).

**Step 7 — it ends by expiry.** There is no explicit stop. `Advance` drops any
swing whose `AgeSeconds` has reached `TotalSeconds`
(`SwingAnimationSystem.cs:96-99`); the next `Resolve` call finds no swing for
that entity, writes no entry, and the pawn draws neutral. `Clear`
(`:129-134`) exists for a battle reset.

---

## 3. `GaitAnimationSystem`, end to end

The gait system deliberately mirrors the swing system's shape while inverting
its driver: nothing here reads a clock.

**Step 1 — it starts on ingest, not on an event.** `GaitAnimationSystem.Ingest`
(`src/Hukbo.Client/Presentation/GaitAnimationSystem.cs:115-172`) takes only the
`AgentView` list. It rebuilds an id-to-view dictionary of **living** agents
(`:119-127`), compacts the existing entry array by advancing every entry whose
warrior is still present and dropping the rest (`:129-142`), and then creates a
fresh entry for every living warrior it has not seen before, up to capacity
(`:146-164`). A new entry starts at `GaitMode.Stance`, `DirectionSign: 0f`, and
a deterministic per-entity phase offset (`:156-162`).

That phase offset is a SplitMix64 finalizer over `EntityId ^ PhaseOffsetSalt`,
taking 24 bits (`ResolvePhaseOffsetTurns`, `:272-288`, salt at `:95`). It carries
no randomness: the same `EntityId` always resolves the same offset. Its purpose
is to stop warriors marching in lockstep.

**Step 2 — the motion signal is a position delta, not a state field.**
`Advance` (`GaitAnimationSystem.cs:208-243`) computes
`distance = sqrt(dx^2 + dy^2)` from `agent.XRaw - entry.PreviousXRaw` and
`agent.YRaw - entry.PreviousYRaw` (`:210-212`), classifies it with
`GaitGeometry.ResolveMode` (`:213`), and stores the new raw position back on the
entry. This is the "derive motion from position delta" behaviour, and section 5
below establishes exactly why it exists.

**Step 3 — phase advances by distance covered.** A moving warrior advances
`PhaseTurns` by `distance / StrideCycleDistanceRaw` where
`StrideCycleDistanceRaw = 6000f` (`GaitAnimationSystem.cs:75`, applied at
`:232-233`), wrapped into `[0,1)` by `WrapTurns` (`:258-262`). A warrior standing
still does not advance at all; instead its stored phase eases toward the nearest
multiple of half a turn by `IdleEasePerTick = 0.2f` (`:83`, applied in
`EaseTowardNeutral`, `:251-256`), so that motion resuming after a long idle does
not snap the legs to an arbitrary mid-stride extension.

Because the phase is distance-driven, pause and playback speed need no handling
anywhere in the gait path — the design note on `GaitPose`
(`src/Hukbo.Client/Rendering/GaitPose.cs:40-48`) states this explicitly, and
`ArenaGame` confirms it by never scaling the gait resolve by `_speedMultiplier`
(`src/Hukbo.Client/ArenaGame.cs:151-159`).

**Step 4 — mode classification.** `GaitGeometry.ResolveMode`
(`src/Hukbo.Client/Rendering/GaitGeometry.cs:84-101`): exactly zero displacement
is `Stance`; below `RunThresholdRawPerTick = 1600f` (`:39`) is `Walk`; at or
above it is `Run`. The threshold sits below the default
`Scenario.MovementSpeedRaw` cap of 3072 raw units per tick, so a warrior near top
speed reads as running.

**Step 5 — mode plus phase maps to a pose.** `GaitGeometry.ResolvePose`
(`GaitGeometry.cs:129-195`) validates its four arguments, short-circuits to
`default(GaitPose)` when `MotionIntensity.Off` (`:160-163`), scales amplitudes by
`GrassSway.ReducedAmplitudeFactor` when `Reduced` (`:165-167`), and then builds
every channel from one sine wave:

```
angle      = tau * phaseTurns
sine       = sin(angle)
leftOffset = sine * strideRatio * amplitudeFactor
rightOffset= -leftOffset
leftLift   = max(0, sine)  * liftRatio * amplitudeFactor
rightLift  = max(0, -sine) * liftRatio * amplitudeFactor
leanX      = leanRatio * amplitudeFactor * directionSign
```

(`GaitGeometry.cs:177-183`.) The per-mode amplitudes are `Walk` stride 0.32 /
lift 0.15 / lean 0, and `Run` stride 0.60 / lift 0.38 / lean 0.18
(`:45`, `:52`, `:55`, `:62`, `:70`, selected at `:169-175`). `TorsoLeanY` is
always zero and the field exists only so the type can gain a vertical channel
later (`GaitPose.cs:88-92`).

**Step 6 — pose maps to drawn geometry.** `CreateLegsAndFeet`
(`PawnGeometry.cs:1011-1051`) returns `default` — four empty rectangles — at
`PawnDetailTier.Low` (`:1018-1021`). Otherwise it applies
`LeftLegOffsetRatio * DirectionSign * legLength` horizontally and
`LeftFootLiftRatio * legLength` vertically (`:1028-1033`). `DirectionSign` is
applied **here**, in the geometry, not on the pose — the pose's offsets are
documented as direction-agnostic (`PawnGeometry.cs:1005-1009`,
`GaitPose.cs:57-62`). `TorsoLeanX` reaches the body anchor through the same
summed channel the swing pose uses (`PawnGeometry.cs:945-952`).

**Step 7 — resolution, lookup, and end.** `GaitPoseResolver.Resolve`
(`src/Hukbo.Client/Rendering/GaitPoseResolver.cs:41-77`) is called once per frame
from `ArenaGame.Update` (`ArenaGame.cs:674-678`) with the current
`MotionIntensity`, filling the caller-owned `_gaitPoses` dictionary
(`ArenaGame.cs:159`). The draw loop calls `GaitPoseResolver.TryGetPose`
(`ArenaGame.Rendering.cs:967-972`). A gait entry ends when its warrior stops
appearing alive in the views: `Ingest`'s compaction drops it on that same call
(`GaitAnimationSystem.cs:133-138`), so a corpse's phase never lingers.

Note the one asymmetry with the swing resolver: `GaitPoseResolver` has **no**
early-out equivalent to `SwingPoseResolver`'s `ActiveSwings.Length == 0` check.
It walks every agent every frame and calls `gait.TryGetEntry`, which is itself a
linear scan of the entry array (`GaitAnimationSystem.cs:177-192`). At 500 agents
that is a 250,000-comparison inner loop per frame. Section 6 revisits the cost.

---

## 4. The pure-helper testability pattern

### Why the resolver is a separate static class

`SwingPoseResolver`'s own doc comment says it outright
(`src/Hukbo.Client/Rendering/SwingPoseResolver.cs:10-16`):

> This exists so the per-pawn pose resolution does not live in `ArenaGame`,
> which is banned from tests and therefore untestable by construction. The
> lookup is pinned here as well as the mapping, because the lookup is the part
> that lands in the untestable file.

`ArenaGame` derives from MonoGame's `Game`, holds a `GraphicsDevice` and a
`SpriteBatch`, and opens a window. Constructing one in a test would require a
GPU and a display. So the repository draws a hard line: everything that
*decides* lives in an `internal static` class over plain values, and everything
that *paints* lives in a method that takes a `SpriteBatch` and is never unit
tested. `.claude/skills/hukbo-client-ui/SKILL.md:8-40` states the rule, and
`:14-16` records that it currently holds absolutely — zero occurrences of
`SpriteBatch`, `GraphicsDevice`, or `ArenaGame` anywhere under `tests/`.

The rule is also a repository non-negotiable: `CLAUDE.md` section 5 states that
"Client presentation tests must never construct `ArenaGame`, a graphics device, a
sprite batch, or a window, and must not depend on GPU, audio, focus, network, or
the wall clock."

### What a Client test may and may not construct

**May construct:** `AgentView`, `BattleEvent` (through its `Attack` / `NonAttack`
factories), `SwingAnimationSystem`, `GaitAnimationSystem`, `PawnAppearance` (via
`PawnAppearanceFactory.Create`), `Vector2`, `Rectangle`, `Color`, plain
`Dictionary<ulong, TPose>` buffers, and every `*Geometry` / `*Resolver` static
class. `Microsoft.Xna.Framework` value types such as `Vector2`, `Rectangle`, and
`Color` are fine — they are pure structs with no device behind them, and
`PawnQuadCountTests` uses all three (`tests/Hukbo.Client.Tests/PawnQuadCountTests.cs:5`,
`:34`, `:109`).

**May not construct:** `ArenaGame`, `SpriteBatch`, `GraphicsDevice`, `Texture2D`,
a window, or anything that reads the wall clock, the network, audio, or focus.
`PawnQuadCountTests`'s own doc comment records how the repository works around
the last of these: "every expected value below was derived by walking that
renderer method by method, not by running the renderer itself (it needs a
graphics device)" (`PawnQuadCountTests.cs:9-17`). The renderer's cost is pinned
by a *parallel pure counter* (`PawnQuadCount.Count`,
`src/Hukbo.Client/Rendering/SubmissionCount.cs:91`) rather than by running the
renderer.

`SourceHygieneTests` additionally bans `System.Random`, `GetHashCode`-based
selection, and wall-clock reads anywhere under `src/Hukbo.Client/Presentation`,
`src/Hukbo.Client/Rendering`, and `src/Hukbo.Client/Settings`
(`tests/Hukbo.Client.Tests/SourceHygieneTests.cs:76-78`, tests at `:181`, `:203`,
`:222`), and bans dictionary or hash-set iteration order from the narrower
variant-selection surface (`:249`).

### The exact shape a new resolver must take

Read off `SwingPoseResolver` and `GaitPoseResolver`, which are deliberately
identical in structure:

1. **`internal static class`** in `namespace Hukbo.Client.Rendering`. No
   instance, no field, no constructor.
2. **A `Resolve` method** that takes (a) the store, (b) `IReadOnlyList<AgentView>`,
   (c) any extra spectator setting the pose needs, and (d) a **caller-owned
   `Dictionary<ulong, TPose> destination`**. It null-checks every reference
   argument with `ArgumentNullException.ThrowIfNull`, validates any enum with
   `Enum.IsDefined`, calls `destination.Clear()`, fills it, and **returns the
   same instance** as `IReadOnlyDictionary<ulong, TPose>`. It must never allocate
   a dictionary of its own — see `SwingPoseResolver.cs:17-21` and
   `GaitPoseResolver.cs:15-20`.
3. **An agent with no state gets no entry**, never a neutral one, "so a caller
   cannot confuse 'standing still' with 'not drawn'"
   (`SwingPoseResolver.cs:26-29`, `GaitPoseResolver.cs:23-29`).
4. **A `TryGetPose(IReadOnlyDictionary<ulong, TPose>, ulong, out TPose)` method**
   that does nothing but a `TryGetValue`. This exists solely so the draw loop's
   lookup is covered: "Pinned by a test so the shipped draw loop is covered
   rather than a method with no caller" (`SwingPoseResolver.cs:70-73`,
   `GaitPoseResolver.cs:79-82`).
5. **A separate `*Geometry` static class** holding the actual keyframe or
   waveform mathematics over value types only, so it can be tested without the
   store at all (`SwingGeometry`, `GaitGeometry`). `GaitGeometry`'s doc comment
   names the requirement: "a static class over value types only, with no store,
   no clock, and no dependency on anything that can fail to be present in a unit
   test" (`GaitGeometry.cs:5-10`).
6. **A `readonly record struct` pose type** whose `default` is the neutral pose
   (`SwingGeometry.cs:24-27`, `GaitPose.cs:32-39`), so `PawnGeometry` can accept
   `TPose?` and treat `null` and `default` identically.

### The template test

`SwingPoseResolverTests.TryGetPose_ReturnsTheSamePoseTheDrawLoopWouldFetchForOneEntity`
(`tests/Hukbo.Client.Tests/SwingPoseResolverTests.cs:61-98`) is the one to copy.
It builds the whole pipeline from plain values, drives it, and then asserts that
the draw loop's lookup agrees with the geometry for every agent:

```csharp
[Fact]
public void TryGetPose_ReturnsTheSamePoseTheDrawLoopWouldFetchForOneEntity()
{
    var swings = new SwingAnimationSystem(capacity: 8);
    AgentView[] agents = [Agent(2, 0, 0), Agent(7, 300, 0), Agent(9, 300, 300)];
    swings.Ingest(
        [
            AttackEvent(1, source: 2, target: 7, AttackResolution.Parried),
            AttackEvent(2, source: 9, target: 7, AttackResolution.Evaded),
        ],
        agents);
    swings.Advance(SwingAnimation.TotalSeconds * 0.7f);
    var poses = SwingPoseResolver.Resolve(
        swings,
        agents,
        new Dictionary<ulong, SwingPose>());

    foreach (var agent in agents)
    {
        var found = SwingPoseResolver.TryGetPose(
            poses,
            agent.EntityId,
            out var pose);

        if (!swings.TryGetSwing(agent.EntityId, out var swing))
        {
            Assert.False(found);
            Assert.Equal(default, pose);
            continue;
        }

        Assert.True(found);
        Assert.Equal(SwingGeometry.ResolvePose(swing), pose);
    }

    Assert.False(SwingPoseResolver.TryGetPose(poses, 404, out var missing));
    Assert.Equal(default, missing);
}
```

Two supporting details worth copying with it. The `Agent` factory
(`SwingPoseResolverTests.cs:100-114`) constructs an `AgentView` naming only the
ten required positional members and letting the fourteen optional ones default —
which is exactly why those fourteen are defaulted in the first place
(`src/Hukbo.Core/Simulation/AgentView.cs:6-11`). And
`Resolve_ReturnsOnePosePerActiveSwing` (`:31-59`) additionally pins the
buffer-reuse contract by resolving twice into the same dictionary and asserting
the second result is empty rather than accumulated (`:53-58`).

---

## 5. What the Client can actually read from the simulation

### Every `AgentView` field

`src/Hukbo.Core/Simulation/AgentView.cs:119-143` declares a
`readonly record struct` of twenty-four positional members. The "shipped preset"
column below means: the `Scenario` a battle is created with by default, whose
`MovementPreset` is `MovementPresetId.PersistentContingentsV4`
(`src/Hukbo.Core/Simulation/Scenario.cs:88-89`).

| # | Field | Type | Populated under the shipped preset? |
| --- | --- | --- | --- |
| 1 | `EntityId` | `ulong` | Yes |
| 2 | `FactionId` | `int` | Yes |
| 3 | `XRaw` | `int` | Yes — raw fixed-point world X |
| 4 | `YRaw` | `int` | Yes — raw fixed-point world Y |
| 5 | `HitPoints` | `int` | Yes |
| 6 | `MaximumHitPoints` | `int` | Yes |
| 7 | `TargetEntityId` | `ulong?` | Yes |
| 8 | `Intent` | `AgentIntent` | Yes — `Idle`, `Moving`, `Attacking`, `Dead`, `Regrouping` |
| 9 | `IsAlive` | `bool` | Yes |
| 10 | `Loadout` | `CombatLoadout` | Yes — `(WeaponId, ArmorId, ShieldId, RankId)` |
| 11 | `MovementResolution` | `MovementResolution` | **Yes** — written unconditionally in `CommitMovement` (`BattleSimulation.cs:3436`, `:3448`) |
| 12 | `Level` | `int` | Yes |
| 13 | `ContingentId` | `int` | Yes |
| 14 | `ContingentState` | `ContingentState` | Yes under V2 and above; `None` under V1 |
| 15 | `Rank` | `RankId` | Yes |
| 16 | `IsLeader` | `bool` | Yes — recomputed per tick (`BattleSimulation.cs:4269`) |
| 17 | `Facing` | `Facing16` | **No — always `Facing16.None`** |
| 18 | `MovementPaceRaw` | `int` | **No — always 0** |
| 19 | `TacticalPosture` | `TacticalPosture` | **No — always `None`** |
| 20 | `FootworkPhase` | `FootworkPhase` | **No — always `None`** |
| 21 | `FootworkTicksRemaining` | `int` | **No — always 0** |
| 22 | `BrokeOffUnderPressure` | `bool` | **No — always `false`** |
| 23 | `PressureBasisPoints` | `int` | **No — always 0** |
| 24 | `PressureThresholdBasisPoints` | `int` | **No — always 0** |

### The known trap: CONFIRMED, with one correction

The claim under investigation was: *"the `AgentView` movement fields all read
zero under the shipped preset, so the gait system derives motion from position
delta instead."* That is **confirmed for fields 17 through 24**, and here is the
chain of evidence:

1. `Scenario.MovementPreset` defaults to `MovementPresetId.PersistentContingentsV4`
   (`src/Hukbo.Core/Simulation/Scenario.cs:88-89`).
2. `PersistentContingentsV4Ruleset` registers
   `usesEquipmentRelativeFootwork: false` and `appliesPressureInterrupt: false`
   (`src/Hukbo.Core/Movement/MovementPresetRegistry.cs:171-192`, specifically
   `:185` and `:189`).
3. `AgentView`'s own doc comments state that `Facing`, `MovementPaceRaw`,
   `TacticalPosture`, `FootworkPhase`, and `FootworkTicksRemaining` are `None` or
   `0` "forever under every other preset"
   (`AgentView.cs:49-83`), and that the three pressure fields are zero for every
   preset whose `AppliesPressureInterrupt` is false (`:84-118`).
4. The code backs the comments. `agent.Facing` is written only inside
   `if (usesFootwork)` (`BattleSimulation.cs:231-245`), the scratch arrays
   `_provisionalFootworkPhases`, `_proposedPaceRaw`, `_contingentPostures`,
   `_pressureBasisPoints` and friends are all allocated zero-length unless the
   preset opts in (`BattleSimulation.cs:175-197`), and `UpdateViews` applies the
   three pressure fields only when `appliesPressureInterrupt && agent.IsAlive`
   (`BattleSimulation.cs:4260-4292`).
5. Nothing in `Hukbo.Client` ever sets `Scenario.MovementPreset` — the only
   Client reference to it is a read (`src/Hukbo.Client/ArenaGame.cs:1713-1714`).

**The correction:** `MovementResolution` (field 11) is *not* in that set. It is
written unconditionally for every agent every tick, alive or dead
(`BattleSimulation.cs:3436` for a corpse, `:3448` for a living agent), so it is
genuinely populated under the shipped preset. A planner should not lump it in
with the V6/V7 fields.

### How `GaitAnimationSystem` actually gets its motion signal

Position delta, computed by the Client itself, from `XRaw` and `YRaw`:

```csharp
var deltaX = (float)(agent.XRaw - entry.PreviousXRaw);
var deltaY = (float)(agent.YRaw - entry.PreviousYRaw);
var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
var mode = GaitGeometry.ResolveMode(distance);
```

`src/Hukbo.Client/Presentation/GaitAnimationSystem.cs:210-213`. The previous
position is stored on the client-side `GaitEntry` (`:13-17`) and rewritten every
ingest (`:218-219`, `:236-237`). The direction sign likewise comes from the sign
of `deltaX`, falling back to the previously stored sign when `deltaX` is exactly
zero (`:226-231`).

So the gait system does **not** read `MovementPaceRaw` (always 0),
`Facing` (always `None`), or `Intent`. It reads position, twice, and subtracts.
`GaitGeometry.ResolveMode`'s own doc comment confirms the unit: "in the same raw
fixed-point units `AgentView.XRaw`/`YRaw` already use"
(`src/Hukbo.Client/Rendering/GaitGeometry.cs:78-83`).

### The critical question: what signal says "this agent is drawing a bow"?

**None. No such signal exists today, at any fidelity, and none can be
synthesized from what the Client currently receives.**

Here is the complete inventory of what does exist, and why each one falls short:

| Candidate signal | What it actually is | Why it cannot carry a five-phase ranged pose |
| --- | --- | --- |
| `AgentView.Intent == AgentIntent.Attacking` | Set at `BattleSimulation.cs:3653`, on the tick the attack resolves | It is a *post-hoc* state, set at the same instant the blow is fully resolved. It carries no sub-tick phase, no "started drawing at tick N", and it does not distinguish a nocked arrow from a mid-flight one. It is also sticky: nothing resets it to `Idle` until the targeting stage runs again (`:1103`, `:1112`). |
| `BattleEventKind.Attack` event | Emitted with `Weapon`, `Shield`, `HitLocation`, and `Resolution` already packed (`BattleEvent.cs:14-22`, produced at `BattleSimulation.cs:3653-3678`) | Same problem, and worse: the event *is* the resolution. There is no separate "attack begins" event. `SwingAnimationSystem` works around this by playing anticipation, strike, hold, and recovery *after* the blow, which is acceptable at 0.25 seconds for a melee cut and is not acceptable for a bow whose draw is the readable part. |
| `AgentView.MovementResolution` | Why collision left the agent where it did | Says nothing about the arms. |
| `AgentView.FootworkPhase` (`Commit`, `Recover`, `Disengage`) | The closest thing in the codebase to an attack-phase channel | **Always `FootworkPhase.None` under the shipped preset** — see the table above. Even under V6/V7 it describes footwork pacing, not weapon handling, and it has three phases where a ranged sequence needs five. |
| `AgentView.TacticalPosture` | Contingent-level stance | Always `None` under the shipped preset. |
| `AgentView.Loadout.Weapon` | `Kampilan`, `Wasay`, `Kalis`, `Itak` (`src/Hukbo.Core/Combat/CombatIdentity.cs:14-45`) | Tells you *which* weapon, and there are no ranged entries in the enum at all. It is static per warrior and carries no phase. |
| `AgentView.TargetEntityId` | Currently selected target | Present, and useful for aiming direction, but it is a target selection, not an action phase. |
| Position delta (the gait trick) | Distance travelled per tick | A drawing archer is standing still. Position delta reads `GaitMode.Stance` for the whole draw, which is correct for the legs and useless for the arms. |

There is one more consideration that rules out any client-side reconstruction.
`SwingAnimationSystem` can fake a swing because the simulation gives it a
discrete, timestamped, per-attacker event with a resolution attached. A five-phase
ranged sequence needs a *duration* the simulation has agreed to — a draw that
takes several ticks, during which the warrior is committed and not moving. The
simulation has no such concept: `AttackCooldownRemaining` gates when the next
attack may be issued (`BattleSimulation.cs:3644-3647`), but the attack itself
resolves entirely within one tick.

### What Core would have to expose

Three things, in rough order of how load-bearing they are:

1. **A ranged weapon identity.** `WeaponId` has exactly four members
   (`CombatIdentity.cs:14-45`) and no ranged entry. Adding one is a determinism
   change under `CLAUDE.md` section 5 (enum values are pinned and feed the
   content hash and the state hash) and requires a new combat preset version plus
   new golden expectations. It also has to clear the historical-evidence bar in
   `CLAUDE.md` section 7 — the same bar that excluded the panabas — with a
   recorded evidence tier per weapon. Downstream, the Client's own
   `PawnWeaponRole` enum (`src/Hukbo.Client/Presentation/PawnAppearance.cs:5-11`)
   and the four `switch` expressions in `PawnGeometry.CreateWeaponLayout` /
   `CreateWeaponThickness` / `CreateSecondaryBounds`
   (`PawnGeometry.cs:1496-1522`, `:1547-1562`, `:1591-1612`) all throw
   `ArgumentOutOfRangeException` on an unrecognized role, so each needs a new arm
   in the same change.

2. **A per-agent ranged action phase with a tick budget.** The minimum viable
   shape is two new `AgentView` members, defaulted exactly as the fourteen
   existing optional ones are (`AgentView.cs:130-143`): a phase enum
   (`RangedPhase.None | Ready | Load | Aim | Release | Recover`, or whatever the
   design names) and an `int` ticks-remaining counter, mirroring the existing
   `FootworkPhase` / `FootworkTicksRemaining` pair (`AgentView.cs:72-83`) which is
   the precedent for exactly this shape. Without a phase the Client cannot know
   the warrior is drawing; without a tick budget it cannot know how far through
   the draw it is, and a client-side clock would drift against playback speed and
   pause the way `SwingAnimation` has to be speed-scaled to compensate
   (`SwingAnimationSystem.cs:10-17`).

3. **An aim direction that survives a standing agent.** A drawing archer does not
   move, so the gait system's `DirectionSign` is stale or zero
   (`GaitAnimationSystem.cs:226-231`). `Facing16` already exists as a type
   (`src/Hukbo.Core/Movement/Facing16.cs`, referenced at `AgentView.cs:136`) and
   is exactly the right shape, but it is `None` under every preset that is not
   equipment-relative. Either the shipped preset gains facing, or the ranged
   phase carries its own direction. A weaker fallback exists — the Client can
   derive a direction from `TargetEntityId` plus the two agents' positions, the
   same way `SwingAnimationSystem.ResolveDirection` already does
   (`SwingAnimationSystem.cs:142-150`) — and that fallback is probably good
   enough for a first pass, at the cost of aiming at the currently selected
   target rather than at where the arrow will actually go.

A fourth item is optional but would materially improve the result: **a distinct
`BattleEventKind` for the release**, or at least a flag on the attack event, so
the Client can trigger a projectile effect at the right instant rather than
inferring it from a phase transition it polls once per tick.

---

## 6. Allocation and the draw path

### The rule

`.claude/skills/hukbo-client-ui/SKILL.md:101-102`: "Keep the draw path
allocation-free; per-frame or per-row heap allocation shows up in the gate's
`allocatedBytes`." `RenderMetricsSnapshot.ManagedBytesAllocated` is the Tier 1
figure that measurement is reported against
(`src/Hukbo.Client/Rendering/RenderMetrics.cs:91-97`, `:227-229`), and the
recorder itself is built from plain mutable fields specifically so that recording
never contributes to the number it measures (`RenderMetrics.cs:44-50`,
`:475-479`).

### How `Resolve` fills a caller-owned buffer

Both resolvers take a `Dictionary<ulong, TPose> destination` argument, clear it,
fill it, and return it as an `IReadOnlyDictionary`
(`SwingPoseResolver.cs:39-68`, `GaitPoseResolver.cs:41-77`). The two dictionaries
are constructed once as `ArenaGame` fields — `_swingPoses`
(`src/Hukbo.Client/ArenaGame.cs:149`) and `_gaitPoses` (`:159`) — with doc
comments that say "Reused each frame so the draw path allocates nothing"
(`:143-148`). `SwingPoseResolverTests.Resolve_ReturnsOnePosePerActiveSwing`
pins the reuse contract by resolving twice into one buffer
(`SwingPoseResolverTests.cs:53-58`).

Two allocation-avoidance details are worth copying. Both stores are
**fixed-capacity arrays sized at construction** and neither can grow
(`SwingAnimationSystem.cs:24-28`, `GaitAnimationSystem.cs:101-105`); a full store
evicts (swing) or silently skips (gait) rather than resizing
(`SwingAnimationSystem.cs:179-192`, `GaitAnimationSystem.cs:146-154`). And both
pose types are `readonly record struct`s, so `entry with { ... }` copies on the
stack (`GaitAnimationSystem.cs:217-223`); the same technique is used inside Core
at `BattleSimulation.cs:4252-4258`, which explicitly notes that a `with`
expression on a readonly record struct "copies on the stack and allocates
nothing."

### The detail-tier gate

Two mechanisms, deliberately kept separate.

`PawnGeometry` classifies the tier internally from `apparentScale`
(`PawnGeometry.cs:665-670`), and each layer's helper decides for itself whether
to return an empty rectangle at that tier: legs and feet at Low
(`:1018-1021`), the swing trail at Low (`:1162-1165`), armor and sash at Low
(`:1377-1380`, `:1409-1412`), adornment accents below High (`:1451-1454`), weapon
secondaries at Low except for the Wasay (`:1529-1532`).

`DetailTierGate.ShouldDraw(apparentScale, minimumDetailTier)`
(`src/Hukbo.Client/Rendering/DetailTierGate.cs:31-32`) is the catalog-facing
version, mirroring the same 0.95 and 1.80 thresholds as read-only ground truth
(`:23-24`) rather than sharing state with `PawnGeometry`. Its doc comment is
explicit that `PawnGeometry` "is never edited to expose them" (`:16-19`).

An empty rectangle costs nothing downstream: `PawnQuadCount` returns 0 for an
empty rectangle at every layer (`SubmissionCount.cs:144-154`, `:193`, `:216`,
`:218`), and `CreateRenderedBounds` skips empty rectangles when building the
union (`PawnGeometry.cs:1119-1139`).

### The cull

Two culls exist; only one is in the shipped path.

**In the path:** `PawnGeometry.PoseBlindPrefix.Create` builds the pose-invariant
proportions and the pose-blind visual bounds for every living agent
(`ArenaGame.Rendering.cs:923-926`), and the loop skips any agent whose
`PoseBlindVisualBounds` does not intersect the arena panel (`:941-944`). It is
pose-blind on purpose: "a pose-aware cull would make the set of drawn pawns a
function of presentation animation phase, so the same tick would render a
different draw list depending on where each swing clock sat"
(`ArenaGame.Rendering.cs:915-922`). An agent that survives finishes the same
construction with `CompletePosedLayout` (`:980`) — one construction split in two
stages, not two constructions (`PawnGeometry.cs:485-496`).

**Not in the path:** `ConservativePawnCull` is dead code, kept only so its test
catches its mirrored constants drifting. Its own doc comment says so: "**Not
adopted, and deliberately so.** GPU-016, the task that would have moved this
bound ahead of appearance resolution in the pawn loop, was dropped on 2026-08-07
and nothing calls this type today"
(`src/Hukbo.Client/Rendering/ConservativePawnCull.cs:10-21`). Its radius is
`27.2 * apparentScale + 5` pixels (`:111`, `:128`, `:156-158`), sized by the
Kalis weapon line's upward reach plus selection padding (`:77-109`). **A ranged
weapon that reaches further than the Kalis's 24.2 units would invalidate that
radius**, and `ConservativePawnCullTests` proves containment by brute force over
the full catalog cross-product, so it would fail — correctly — rather than
silently mis-cull.

### What 500 pawns each holding a five-phase ranged pose would cost

**Memory, per frame: zero new heap allocation**, if the shape in section 7 is
followed. The additions are one more fixed-capacity array of `readonly record
struct` entries allocated once at construction, one more `Dictionary<ulong,
RangedPose>` allocated once as an `ArenaGame` field, and one more nullable
struct parameter threaded through `CompletePosedLayout`. Nothing per frame,
nothing per pawn.

**Memory, once at startup:** a `RangedPose` of, say, eight `float`s plus two
small enums is roughly 40 bytes; a `RangedEntry` similar. At 500 capacity that is
under 50 KB for both the store and the pose dictionary combined — noise against a
process that already holds two 500-entry stores and a 500-entry appearance cache.

**Quads.** This is the binding constraint, and it has real but limited headroom.
The measured per-pawn High-tier baseline is **24 quads**
(`PawnQuadCountTests.cs:57-69`), and the arithmetic recorded in
`src/Hukbo.Client/Rendering/SubmissionCount.cs:424-447` gives:

```
(24 quads/pawn x 500 units) + 4,032 backdrop = 16,032 quads
ceiling                                      = 20,000 quads
headroom                                     =  3,968 quads
```

Divided across 500 pawns that is **7.9 quads per pawn of headroom** before
`RenderBudgetEstimateTests.WholeFrameWorstCaseArithmetic_FitsWithinTheEstimateAt200And500Units`
(`tests/Hukbo.Client.Tests/RenderBudgetEstimateTests.cs:32-63`) fails.

Whether a ranged pose spends any of that depends entirely on whether it draws new
primitives or only *moves existing ones*. A pose that rotates the existing weapon
line and shifts the body anchor costs **zero extra quads** — `PawnQuadCount`
counts rectangles, not phases, and the weapon is a flat `WeaponQuadCount = 3`
regardless of how it is posed (`SubmissionCount.cs:43`, applied at `:119`).

One caveat on that, because the 24-quad baseline hides it: the swing pose is not
entirely free. Its arc trail costs `SwingTrailSegments = 6` stroked quads
whenever `SwingTrail` is non-empty (`SubmissionCount.cs:36`, `:312-313`), and the
pinned 24-quad baseline is measured with `swingPose: null`
(`PawnQuadCountTests.cs:61`), so the trail sits *outside* it. Five hundred pawns
all mid-swing at High tier would be `(24 + 6) * 500 + 4,032 = 19,032` quads —
inside the 20,000 ceiling, but with only 968 quads of slack. A ranged pose that
adds its own trail-equivalent on top of that has almost nothing to spend.

A pose that adds a bow stave as a second
line, an arrow as a third, and a drawn string as a fourth would cost roughly
three `DrawLine` calls, and `DrawBlade` already shows that a weapon line is three
quads (`PawnQuadCountTests.cs:173-196`, `SubmissionCount.cs:295-298` for the
disk equivalent), so a naive bow-plus-arrow-plus-string could easily be 6 to 9
quads and would breach the budget on its own.

**The practical recommendation is therefore: reuse the existing weapon line and
the existing `SecondaryEquipmentBounds` slot, and add at most one new rectangle.**
The Wasay already proves the pattern — its axe head is a single
`SecondaryEquipmentBounds` rectangle that survives Low tier because it is what
makes an axe an axe (`PawnGeometry.cs:1526-1532`, `:1603-1609`). A bow's stave
can be the weapon line; the nocked arrow can be the secondary rectangle. Anything
beyond that has to be gated to `PawnDetailTier.High` and paid for with a
deliberate, documented budget revision — the "anti-density-creep rule" in
`SubmissionCount.cs:412-421` forbids silently rewriting the ceiling to match a
measurement.

**CPU.** The resolve pass is `O(agents)` with a `TryGetEntry` linear scan inside
it, which is `O(agents^2)` in the worst case — 250,000 comparisons at 500 agents
for the gait system today, and a ranged system copying the pattern would add the
same again. This is measured, not budgeted: it lands in
`RenderMetricsSnapshot.ArenaGeometryMicroseconds`
(`RenderMetrics.cs:131-139`). Two cheap mitigations are already demonstrated in
the codebase: `SwingPoseResolver`'s early-out when the store is empty
(`SwingPoseResolver.cs:50-54`), which for ranged units would skip the whole pass
in a battle with no archers, and a straight index dictionary rather than a scan.
A ranged store should copy the early-out at minimum, since most battles under
most rosters will have no ranged warriors at all.

---

## 7. A recommended shape for a ranged pose resolver

This is a recommendation from a read-only survey. It is not a design document
and it does not authorize implementation; `CLAUDE.md` section 6 requires a
design document and then a plan document before any of this is written.

### The types, and which file each lands in

| Piece | Kind | File |
| --- | --- | --- |
| `RangedPhase` | `internal enum` | `src/Hukbo.Client/Rendering/RangedPose.cs` (new) |
| `RangedPose` | `internal readonly record struct` | same file |
| `RangedGeometry` | `internal static class` — keyframe mathematics only | `src/Hukbo.Client/Rendering/RangedGeometry.cs` (new) |
| `RangedEntry` | `internal readonly record struct` | `src/Hukbo.Client/Presentation/RangedAnimationSystem.cs` (new) |
| `RangedAnimationSystem` | `internal sealed class` — fixed-capacity store | same file |
| `RangedPoseResolver` | `internal static class` — `Resolve` + `TryGetPose` | `src/Hukbo.Client/Rendering/RangedPoseResolver.cs` (new) |
| Pose plumbing | new optional parameter on `Create`, `CreateWithPoseBlindBounds`, `CompletePosedLayout`, `CreateLayout` | `src/Hukbo.Client/Rendering/PawnGeometry.cs` (edit) |
| New drawn rectangles | new `PawnLayout` members plus their builder | `src/Hukbo.Client/Rendering/PawnGeometry.cs` (edit) |
| Draw calls | new `Draw*` private methods | `src/Hukbo.Client/Rendering/PawnRenderer.cs` (edit) |
| Quad accounting | new `Count*` private methods | `src/Hukbo.Client/Rendering/SubmissionCount.cs` (edit) |
| Store ownership | a `Ranged` property beside `Swings` (`:67`) and `Gait` (`:102`), constructed at `:31`/`:35`, ingested at `:143`/`:150`, cleared at `:232`/`:236` | `src/Hukbo.Client/Presentation/PresentationCoordinator.cs` (edit) |
| Per-frame buffer and resolve call | `_rangedPoses` field, `RangedPoseResolver.Resolve` call | `src/Hukbo.Client/ArenaGame.cs` (edit, beside `:149`/`:159` and `:670-678`) |
| Draw-loop lookup | `RangedPoseResolver.TryGetPose` call | `src/Hukbo.Client/ArenaGame.Rendering.cs` (edit, beside `:961-972`) |
| New weapon roles | new `PawnWeaponRole` members | `src/Hukbo.Client/Presentation/PawnAppearance.cs` (edit, `:5-11`) |

### The phase enum

Five phases, matching the brief, with a sixth `None` member at zero so that
`default(RangedPose)` is the neutral standing pose exactly as
`default(SwingPose)` and `default(GaitPose)` already are
(`SwingGeometry.cs:24-27`, `GaitPose.cs:32-39`):

```
None = 0     no ranged action in flight; the pawn stands as it does today
Ready        weapon carried, not yet loaded
Load         nock, insert dart, or pour and ram
Draw         pull to anchor, raise to lips, or shoulder and level
Release      loose, blow, or fire
Recover      return to Ready
```

`GaitMode`'s doc comment records the reason the zero member matters: "Numeric
values are not part of any persisted contract; they exist only so
`GaitPose.Mode`'s default, `Stance`, lines up with `default(GaitPose)` being the
neutral standing pose" (`GaitPose.cs:3-10`). Nothing here is hashed, so nothing
here is pinned by the determinism contract — but the enum must stay in
`Hukbo.Client`, not `Hukbo.Core`, unless Core is the thing driving the phase (see
section 5, item 2).

### The pose record

Mirror `SwingPose`'s channel discipline: every field is a scalar the geometry
multiplies into a position, with the direction already folded in so a draw loop
has nothing to reconstruct (`SwingGeometry.cs:71-76`). A workable set:

```
RangedPhase Phase           which phase
float PhaseProgress         progress within it, [0,1]
float WeaponAngleRadians    rotation of the primary line about the grip, signed
float ExtensionRatio        travel of the tip along the reach
float TorsoLeanX            torso offset, pawn units, direction already applied
float TorsoLeanY            torso offset, vertical
float SecondaryAngleRadians rotation of the second line (string hand / arrow)
float SecondaryExtension    its travel
float DrawTension           0 at rest, 1 at full draw — drives any bend or glow
```

### How it composes with gait

**Additively, through the existing summed lean channel.** `CreateBodyAnchor`
already sums two independent lean contributions:

```csharp
footAnchor + new Vector2(
    (pose.TorsoLeanX + gaitPose.TorsoLeanX) * apparentScale,
    (pose.TorsoLeanY + gaitPose.TorsoLeanY) * apparentScale);
```

(`PawnGeometry.cs:945-952`.) Adding `rangedPose.TorsoLeanX` to that sum is a
one-line change and is exactly the precedent the gait work established. A warrior
can therefore walk and reload at the same time with no mutual exclusion at all —
the gait pose owns the legs and feet, the ranged pose owns the arms and the
weapon line, and the torso lean is the single shared channel they both write.

That is not an accident of the code; it is the shape the gait design chose. Legs
and feet are computed only from `GaitPose` (`CreateLegsAndFeet`,
`PawnGeometry.cs:1011-1051`), and the weapon line is computed only from
`SwingPose` (`ApplySwing`, `:1576-1589`). The two never touch.

### How it composes with swing

**Mutual exclusion is required, and the cheapest correct rule is: a ranged pose
suppresses the swing pose for that pawn on that frame.**

The reason is mechanical, not stylistic. Both poses write the same two channels —
`WeaponAngleRadians` and `ExtensionRatio` — into the same `ApplySwing` call
(`PawnGeometry.cs:1576-1589`), which rotates one line about one grip. There is
only one weapon line per pawn. Summing two rotations would produce a meaningless
angle; passing both and letting the geometry pick would put the decision inside
`PawnGeometry`, where it is harder to test than in a resolver.

The clean form is to decide it in the draw loop, in the same place the two
existing `TryGetPose` calls already sit (`ArenaGame.Rendering.cs:961-972`): if a
ranged pose is present, pass `swingPose: null`. That keeps `PawnGeometry`
unchanged in this respect, keeps the decision one line long, and — critically —
can still be pinned by a test, because the same three-line expression can be
lifted into a pure helper on `RangedPoseResolver` and tested there, exactly the
way `TryGetPose` itself was lifted out of the untestable file
(`SwingPoseResolver.cs:70-73`).

In practice the exclusion may never fire: an archer and a swordsman are different
loadouts, and a warrior holding a bow has no melee attack event to animate. But
the rule should be explicit rather than assumed, because `SwingAnimationSystem`
ingests an attack event for *any* attacker (`SwingAnimationSystem.cs:51-63`), and
a ranged loose that is emitted as a `BattleEventKind.Attack` would start a swing
for the archer as a side effect.

### Where it plugs into the draw loop

Four insertion points, all beside existing lines:

1. **Store advance** — `PresentationCoordinator.AdvanceEffects`, called from
   `ArenaGame.Update` at `src/Hukbo.Client/ArenaGame.cs:667-669`. If the ranged
   phase is tick-driven from Core (recommended), the store's `Ingest` belongs
   wherever the gait store's `Ingest` already runs, on completed-tick views, and
   there is no clock to advance at all.
2. **Resolve** — immediately after `GaitPoseResolver.Resolve`
   (`ArenaGame.cs:674-678`), filling a new `_rangedPoses` field declared beside
   `_gaitPoses` (`:159`).
3. **Lookup** — immediately after the `gaitPose` lookup
   (`ArenaGame.Rendering.cs:967-972`), producing a `(RangedPose?)null` on a miss.
4. **Layout** — a third argument on `pawnPrefix.CompletePosedLayout`
   (`ArenaGame.Rendering.cs:980`).

The cull needs no change and must not get one: `PoseBlindPrefix.Create` takes no
pose by design (`PawnGeometry.cs:548-553`), and
`CreatePoseBlindVisualBounds` passes `default` through the same helpers the posed
path uses so the two are identical by construction rather than by argument
(`:869-885`). A ranged pose is passed as `default` there, same as the other two.

**But the pose-blind bound has to still contain the ranged pose.** That is the
one real hazard. `PawnGeometryTests.CreateWithPoseBlindBounds_KeepsTheCullRectangleBlindToTheSwing`
(`tests/Hukbo.Client.Tests/PawnGeometryTests.cs:2089`) and
`PoseBlindPrefix_CompletesOneCullRectangleUnderEveryPose` (`:2338`) pin that the
rectangle does not move with the pose; if a ranged pose extends the weapon line
further than any swing pose can, the drawn pawn can escape its own cull
rectangle and be clipped at the arena panel edge. Whatever maximum extension the
ranged pose can reach has to fit inside the envelope the existing weapon-line
padding already allows, or the pose-blind bound needs a documented, tested
widening.

### How the four weapons differ

All four are drawn from the same two-line-plus-lean vocabulary, differentiated by
which line moves in which phase. Historical grounding for each silhouette comes
from `docs/research/HISTORICAL_1500s_WEAPONS.md:41-47` and the role table at
`:110-118`; every pose value below is a **provisional reconstruction** for
gameplay legibility, not a measurement, and must be commented as such in the
same way every constant in `SwingGeometry` and `GaitGeometry` already is.

**Bangkaw — Long Spear (thrown).** The research doc calls for "very long dark
palm or rattan shaft, oversized leaf-shaped steel point, carried diagonally
beyond the body" and gives the archetype "longest diagonal line"
(`HISTORICAL_1500s_WEAPONS.md:41`, `:112`). This is the closest of the four to
the existing swing vocabulary: one long line rotating about a grip. `Ready`
carries it diagonally across the body; `Load` shifts the grip back along the
shaft; `Draw` cocks the arm and rotates the line steeply back past the shoulder,
with the torso leaning *away* from the target — the negative-lean keyframe
`SwingGeometry.PullBackLean = -0.9f` (`SwingGeometry.cs:121`) is the exact
precedent; `Release` sweeps forward past neutral with the largest `ExtensionRatio`
of the four; `Recover` returns to an empty hand and, once thrown, the weapon line
should shorten or vanish for the rest of the recovery. That last part has no
precedent in the codebase and is the one genuinely new drawing behaviour this
weapon needs.

**Busog — War Bow.** "Tall bow arc outside the torso silhouette, pale reed
arrows, dark points, clearly visible back quiver", archetype "bow arc and quiver"
(`HISTORICAL_1500s_WEAPONS.md:43`, `:113`). This is the one that genuinely wants
two lines: a near-vertical stave held out from the body, and a short string-hand
line drawn back to the cheek. The stave barely rotates across the whole sequence
— it is the *reference* the other motions read against — while
`SecondaryAngleRadians` and `SecondaryExtension` carry the draw. `DrawTension`
rises through `Draw`, holds through the top of `Aim`, and snaps to zero on
`Release`; a single-frame snap is the readable moment. The torso lean is small
throughout; an archer at full draw is upright and still. The quiver is a
time-invariant appearance layer, not a pose channel — it belongs with the sash
and adornment layers (`PawnGeometry.cs:1343-1352`), which are explicitly
documented as never reading a pose.

**Sumpit — Blowgun.** "Long, straight, narrow tube held horizontally, with a
small dart bundle", archetype "blowgunner" grouped with archers under "small or
no shield" (`HISTORICAL_1500s_WEAPONS.md:59`, and the blowgun row in the same
table). The distinguishing pose fact is that the weapon comes **to the face**,
not to the shoulder or the cheek. `Ready` holds the tube low and near-horizontal;
`Load` dips the muzzle and brings the dart hand to it; `Draw` raises the whole
line so its grip end meets the head rectangle — this is the only one of the four
where the weapon's *start* point moves substantially, and it is the reason the
recommended pose record carries an extension channel on both lines rather than
only on the tip. `Release` is a sharp forward torso pulse with almost no rotation
at all — the puff, not a sweep — and is the single hardest of the twenty phases to
make readable at battle scale, because nothing about the silhouette changes.
Expect it to need an accompanying effect (dust, a dart line) rather than pose
alone. `Recover` drops the tube back to horizontal.

**Imported Arquebus.** "Long timber stock, dark iron barrel, horizontal pose,
small glowing matchcord, and an `IMPORTED` badge", archetype "long horizontal
stock and barrel", and the doc is explicit that "it should be rare"
(`HISTORICAL_1500s_WEAPONS.md:47`, `:118`). The pose sequence is the longest and
the least like the other three: `Load` is a multi-beat business of pouring and
ramming that reads as the ramrod line moving *along* the barrel rather than
about a grip; `Draw` is the shoulder-and-level, which is a small rotation to near
horizontal plus a body-anchor shift; `Release` is the one phase in all twenty
that should hold for longer than an instant, because the muzzle flash and the
recoil pulse are the readable content. Two constraints follow. The horizontal
barrel is the flattest silhouette of the four and will be hardest to distinguish
from a Kampilan at Low tier, so this weapon most needs its `SecondaryEquipment`
rectangle to survive Low tier — the Wasay's axe head is the established
precedent for exactly that exception (`PawnGeometry.cs:1526-1532`). And the
matchcord and the `IMPORTED` badge are appearance layers, not pose channels;
the badge in particular is UI, not geometry.

### One structural warning for the planner

`PawnWeaponRole` has four members and `PawnGeometry` has **three** `switch`
expressions over it that each throw `ArgumentOutOfRangeException` on an
unrecognized value — `CreateWeaponLayout`'s start, end, and padding switches
(`PawnGeometry.cs:1496-1522`), `CreateWeaponThickness` (`:1547-1562`), and
`CreateSecondaryBounds` (`:1591-1612`, which falls through to
`Rectangle.Empty` rather than throwing). Adding four ranged roles means touching
every one of them in the same change, and `PawnAppearanceFactory` plus the
weapon-visual catalog have to grow entries too or the appearance resolution
throws before the geometry is ever reached.

---

## 8. Every Client test that constrains this work

Line numbers are the `[Fact]` or `[Theory]` method's declaration line.

### Pose resolvers — the shape a new resolver must match

| Test | Constraint |
| --- | --- |
| `tests/Hukbo.Client.Tests/SwingPoseResolverTests.cs:17` `Resolve_ReturnsNoPoseForAnAgentWithNoActiveSwing` | An agent with no in-flight action gets no dictionary entry, not a neutral one. |
| `SwingPoseResolverTests.cs:31` `Resolve_ReturnsOnePosePerActiveSwing` | One pose per active entry, count agrees with the store, and a second resolve into the same buffer replaces rather than accumulates. |
| `SwingPoseResolverTests.cs:61` `TryGetPose_ReturnsTheSamePoseTheDrawLoopWouldFetchForOneEntity` | The draw-loop lookup returns exactly what the geometry would produce, and a miss yields `default`. This is the template test. |
| `tests/Hukbo.Client.Tests/GaitPoseResolverTests.cs:17` `Resolve_ReturnsNoPoseForAnAgentTheStoreHasNotIngested` | Same "no entry rather than neutral" rule, keyed on the store rather than on an event. |
| `GaitPoseResolverTests.cs:30` `Resolve_ReturnsOnePosePerTrackedAgent` | One pose per tracked agent. |
| `GaitPoseResolverTests.cs:48` `Resolve_OffResolvesTheNeutralPoseForEveryTrackedAgent` | A spectator motion setting of `Off` resolves neutral for every agent, through the resolver rather than by a caller branch. |
| `GaitPoseResolverTests.cs:63` `TryGetPose_ReturnsTheSamePoseTheDrawLoopWouldFetchForOneEntity` | Same draw-loop lookup pin. |
| `GaitPoseResolverTests.cs:82` `Resolve_RejectsAnUndefinedMotionIntensity` | An undefined enum argument throws rather than being silently coerced. |

### Pose geometry — the mathematics a new `*Geometry` must match

| Test | Constraint |
| --- | --- |
| `tests/Hukbo.Client.Tests/SwingGeometryTests.cs:37` `ResolvePhase_VisitsTheFourPhasesInOrder` | Phase boundaries are pinned against the share constants; a phase order change is a test change. |
| `SwingGeometryTests.cs:66` `ResolvePose_SwingsTowardTheTarget` | The direction is folded into the pose; a leftward attacker mirrors. |
| `SwingGeometryTests.cs:103` `ResolvePose_RecoilsOnAContactOutcome` | Blocked, parried, and deflected outcomes recoil. |
| `SwingGeometryTests.cs:134` `ResolvePose_StopsOnTheTargetForALandedBlow` | A landed blow holds the contact keyframe. |
| `SwingGeometryTests.cs:159` `ResolvePose_FollowsThroughPastTheTargetForAVoid` | An evaded blow overshoots. |
| `SwingGeometryTests.cs:189` `ResolvePose_IsContinuousAcrossEveryPhaseBoundary` | **The one a ranged resolver most has to satisfy**: every channel is continuous across every phase boundary, checked numerically rather than asserted. Five phases means four boundaries to keep continuous. |
| `tests/Hukbo.Client.Tests/GaitGeometryTests.cs:14` `ResolveMode_ZeroDisplacementIsStance` | Exactly zero is the neutral classification. |
| `GaitGeometryTests.cs:20` `ResolveMode_BelowRunThresholdIsWalk` | Threshold semantics are pinned. |
| `GaitGeometryTests.cs:26` `ResolveMode_AtOrAboveRunThresholdIsRun` | The threshold is inclusive at the top. |
| `GaitGeometryTests.cs:33` `ResolveMode_RejectsANegativeDisplacement` | Out-of-range input throws. |
| `GaitGeometryTests.cs:40` `ResolvePose_StanceResolvesTheNeutralPose` | The neutral mode produces `default`. |
| `GaitGeometryTests.cs:58` `ResolvePose_WalkProducesANonzeroStride` | A moving mode actually moves something. |
| `GaitGeometryTests.cs:76` `ResolvePose_RunHasALongerStrideAndALeanThanWalk` | Modes are distinguishable in more than one channel. |
| `GaitGeometryTests.cs:99` `ResolvePose_LeanFollowsTheDirectionSign` | Direction sign flips the lean. |
| `GaitGeometryTests.cs:117` `ResolvePose_OffResolvesTheNeutralPoseAtEveryDisplacement` | The motion-intensity short-circuit is unconditional. |
| `GaitGeometryTests.cs:132` `ResolvePose_ReducedAmplitudeIsStrictlyLessThanFull` | Reduced amplitude is strictly smaller, not merely different. |
| `GaitGeometryTests.cs:152` `ResolvePose_RejectsAPhaseOutsideTheHalfOpenUnitRange` | Phase must be in `[0,1)`. |
| `GaitGeometryTests.cs:169` `ResolvePose_RejectsADirectionSignOutsideUnitRange` | Direction sign must be in `[-1,1]`. |

### Animation stores — the fixed-capacity discipline

| Test | Constraint |
| --- | --- |
| `tests/Hukbo.Client.Tests/SwingAnimationSystemTests.cs:16` `Ingest_CreatesOneSwingPerAttacker` | One entry per actor, never one per event. |
| `SwingAnimationSystemTests.cs:48` `Ingest_ReplacesAnInFlightSwingForTheSameAgent` | A repeat action overwrites in place. |
| `SwingAnimationSystemTests.cs:74` `Advance_ExpiresASwingAfterItsTotalDuration` | An action ends by expiry, with no explicit stop call. |
| `SwingAnimationSystemTests.cs:102` `Ingest_StaysBoundedUnderAFloodOfAttacks` | The store never grows past capacity under load. |
| `SwingAnimationSystemTests.cs:138` `Ingest_IgnoresAnAttackWhoseAttackerOrTargetIsNotInTheAgentViews` | An event naming an absent entity is dropped, not tolerated. |
| `tests/Hukbo.Client.Tests/GaitAnimationSystemTests.cs:17` `NoIngestCall_LeavesNoEntries` | An un-ingested store is empty, not neutral. |
| `GaitAnimationSystemTests.cs:26` `Ingest_FirstSightingOfAWarriorResolvesStance` | First sighting is the neutral mode. |
| `GaitAnimationSystemTests.cs:37` / `:50` `Ingest_WalkMagnitudeDisplacementResolvesWalkMode` / `RunMagnitude...` | The store's classification agrees with the geometry's. |
| `GaitAnimationSystemTests.cs:62` `Ingest_TwoTicksAtIdenticalPositionsEasesPhaseTowardNeutralAndStaysStance` | Idle easing does not change the resolved mode. |
| `GaitAnimationSystemTests.cs:84` `Ingest_NoTickAfterTheFirstAdvancesNoPhase` | Phase is distance-driven; a tick with no movement advances nothing. |
| `GaitAnimationSystemTests.cs:97` `Ingest_TwoEntitiesMovingIdenticallyHoldDifferentPhases` | The per-entity offset is real and deterministic. |
| `GaitAnimationSystemTests.cs:110` / `:122` `Ingest_DropsAWarriorThatDies` / `...AbsentFromTheViews` | A dead or absent warrior's entry is dropped on the same ingest. |
| `GaitAnimationSystemTests.cs:134` `Ingest_NeverExceedsCapacity` | Capacity is a hard bound. |
| `GaitAnimationSystemTests.cs:145` `Clear_ResetsEverything` | A reset leaves nothing behind. |

### Pawn geometry — what a new pose channel must not break

| Test | Constraint |
| --- | --- |
| `tests/Hukbo.Client.Tests/PawnGeometryTests.cs:45` `Create_PreservesFootAnchorAcrossBodyVariation` | The foot anchor is invariant. |
| `PawnGeometryTests.cs:109` `Create_VisualBoundsContainEveryRenderedPartAndSelectionPadding` | **Every drawn rectangle must be inside `VisualBounds`** — a new ranged rectangle that escapes it fails here. |
| `PawnGeometryTests.cs:190` `Create_WithoutASwingPose_MatchesTheStaticLayout` | A null pose is bit-identical to no pose. A ranged pose needs the same guarantee. |
| `PawnGeometryTests.cs:229` `Create_WithASwingPose_RotatesTheWeaponAndLeansTheTorso` | A pose's two channels have their documented effects. |
| `PawnGeometryTests.cs:274` `Create_WithoutAGaitPose_MatchesTheStaticLayout` | Same null-pose identity for the second pose type. |
| `PawnGeometryTests.cs:305` `Create_LegAndFootBoundsAreEmptyAtLowTierAndNonEmptyAtMediumAndHigh` | The detail-tier gate on an optional layer is pinned at all three tiers. |
| `PawnGeometryTests.cs:339` `CreateWithPoseBlindBounds_KeepsThePoseBlindBoundsIdenticalAcrossDifferentGaitPhases` | The cull rectangle does not move with the pose. |
| `PawnGeometryTests.cs:399` `Create_WithAGaitPose_MirrorsTheLegOffsetWhenDirectionSignFlips` | Direction sign is applied in the geometry, not on the pose. |
| `PawnGeometryTests.cs:504` `Create_LegBandIsRoughlyAThirdOfTheSilhouetteHeight` | The proportion relationship among body parts is pinned across the zoom range. |
| `PawnGeometryTests.cs:538` `Create_LegAndFootHeightNeverRoundsToZeroAcrossTheApparentScaleRange` | **The whole-pixel floor trap**: a new small rectangle must not round to zero at any apparent scale. This is the test that caught the original one-unit leg. |
| `PawnGeometryTests.cs:576` `Create_WithAGaitPose_StrideAndFootLiftAreAtLeastOnePixelAtMediumTier` | A pose channel must produce at least one whole pixel of visible displacement, or it is invisible work. |
| `PawnGeometryTests.cs:624` `Create_ExposesTheSwingTrailOnTheLayoutRatherThanRequiringTheRendererToRecomputeIt` | Derived pose geometry lives on the layout, never in the renderer. |
| `PawnGeometryTests.cs:661` `Create_OmitsTheSwingTrailAtTheLowDetailTier` | An expensive pose decoration is gated off at Low tier. |
| `PawnGeometryTests.cs:703` `Create_WeaponGripAnchorMatchesTheWeaponsDrawnStart` | The grip anchor equals the drawn start for every role — a new role must satisfy this. |
| `PawnGeometryTests.cs:725` `Create_WeaponGripAnchorStaysAtTheSwingPivotUnderAPose` | The grip is the pivot and does not drift under a pose. |
| `PawnGeometryTests.cs:988` `GetBounds_MatchesThePinnedRegressionRectangle` | An exact pinned rectangle. Any skeleton change moves it and must move it deliberately. |
| `PawnGeometryTests.cs:1245` `Create_ShieldBoundsAndPostureRotationAreIndependentOfSwingPoseAtZeroTorsoLeanY` | The shield is pose-invariant. A ranged pose must not move it either. |
| `PawnGeometryTests.cs:1754` `Create_ArmorSashAndAdornmentBoundsAreIndependentOfSwingPoseAtZeroTorsoLean` | The composed appearance layers are time-invariant and must stay so. |
| `PawnGeometryTests.cs:1932` `CreateWithPoseBlindBounds_MatchesCreateAndGetBoundsAcrossTheInputGrid` | The combined call agrees with the separate calls over a full input grid. |
| `PawnGeometryTests.cs:2089` `CreateWithPoseBlindBounds_KeepsTheCullRectangleBlindToTheSwing` | The cull rectangle is pose-blind. |
| `PawnGeometryTests.cs:2132` `CreateWithPoseBlindBounds_RejectsWhateverCreateRejects` | Argument validation is identical across entry points. |
| `PawnGeometryTests.cs:2188` `PoseBlindPrefix_MatchesCreateAndGetBoundsAcrossTheInputGrid` | The two-stage form agrees with the one-stage form. |
| `PawnGeometryTests.cs:2338` `PoseBlindPrefix_CompletesOneCullRectangleUnderEveryPose` | One prefix finishes correctly under any pose — a new pose parameter must not break this. |
| `PawnGeometryTests.cs:2407` `PoseBlindPrefix_Create_RejectsWhateverCreateRejects` | Same validation parity for the prefix. |

### Quad budget — the density ceiling

| Test | Constraint |
| --- | --- |
| `tests/Hukbo.Client.Tests/PawnQuadCountTests.cs:31` `Count_PinsTheLowTierUnshieldedUnarmoredNormalPawn` | Low tier is exactly 17 quads. |
| `PawnQuadCountTests.cs:43` `Count_PinsTheMediumTierUnshieldedUnarmoredNormalPawn` | Medium tier is exactly 23 quads. |
| `PawnQuadCountTests.cs:58` `Count_PinsTheHighTierUnshieldedUnarmoredNormalPawn` | High tier is exactly 24 quads. **Any new drawn rectangle changes this pin and must change it deliberately, with the budget arithmetic in the commit message** (`PawnQuadCountTests.cs:9-17`). |
| `PawnQuadCountTests.cs:78` `Count_LegsAndFeetContributeNothingAtLowTier` | A Low-tier gate is proved by empty rectangles, not by a renderer branch. |
| `PawnQuadCountTests.cs:104` `Count_PinsTheHighTierFullyLoadedSelectedPawn` | The combinatorial worst case is exactly 44 quads. |
| `PawnQuadCountTests.cs:184` `Count_TheWeaponAlwaysContributesTheSameQuadsRegardlessOfRole` | **Every weapon role costs the same three quads.** A ranged role that draws a second line breaks this and needs the pin updated. |
| `tests/Hukbo.Client.Tests/RenderBudgetEstimateTests.cs:32` `WholeFrameWorstCaseArithmetic_FitsWithinTheEstimateAt200And500Units` | The whole-frame arithmetic must fit 12,000 quads at 200 units and 20,000 at 500. This is the hard ceiling from section 6. |

### Cull and tier

| Test | Constraint |
| --- | --- |
| `tests/Hukbo.Client.Tests/DetailTierGateTests.cs` (60 lines, whole file) | `DetailTierGate`'s thresholds match `PawnGeometry`'s exactly. |
| `tests/Hukbo.Client.Tests/DetailTierBoundaryTests.cs:133` `ShouldDraw_AtBothSidesOfTheEntrysOwnMinimumDetailTierThreshold` | Every catalog entry draws correctly on both sides of its own tier threshold. |
| `DetailTierBoundaryTests.cs:161` `AllCatalogEntryIds_CoversEveryShippedEntryAcrossEveryCatalog` | **The tier sweep must cover every shipped catalog entry** — new ranged entries are pulled into this test automatically and must classify. |
| `tests/Hukbo.Client.Tests/ConservativePawnCullTests.cs` (562 lines) | Proves by brute force over the full catalog cross-product that the conservative radius contains every pawn's real bounds, and that its mirrored `PawnGeometry` constants have not drifted. **A ranged weapon reaching further than the Kalis's 24.2 units fails this**, even though nothing calls the cull. |

### Structural and hygiene

| Test | Constraint |
| --- | --- |
| `tests/Hukbo.Client.Tests/PresentationNeutralityTests.cs:70` `TheCoreAssemblyDoesNotReferenceClientOrDiagnostics` | `Hukbo.Core` may never name a `Hukbo.Client` type. A ranged pose enum in Core cannot be a Client type. |
| `PresentationNeutralityTests.cs:87` `TheClientAssemblyDoesReferenceCore` | The positive control for the above. |
| `tests/Hukbo.Client.Tests/SourceHygieneTests.cs:181` `PresentationVariationCodeDoesNotUseSystemRandom` | No `System.Random` anywhere under `Presentation`, `Rendering`, or `Settings`. |
| `SourceHygieneTests.cs:203` `PresentationVariationCodeDoesNotUseGetHashCodeForSelection` | No `GetHashCode`-based selection. Use a SplitMix64 finalizer over `EntityId`, as `GaitAnimationSystem.ResolvePhaseOffsetTurns` does. |
| `SourceHygieneTests.cs:222` `PresentationVariationCodeDoesNotReadTheWallClock` | No wall-clock reads in presentation code. |
| `SourceHygieneTests.cs:249` `VariantSelectionSurfaceDoesNotDependOnDictionaryOrHashSetIterationOrder` | No dictionary or hash-set iteration order in variant selection. |
| `SourceHygieneTests.cs:28` `OnlyTheEntryPointsWriteDirectlyToTheConsole` | No `Console.Write*` outside the two `Program.cs` files. |
| `tests/Hukbo.Client.Tests/PawnRendererTests.cs` (639 lines) | The renderer's pure helpers — colors, glyphs, state marks — are tested without a graphics device; a new `Draw*` method's decisions must be extracted the same way. |
| `tests/Hukbo.Client.Tests/WeaponVisualCatalogTests.cs:262` `SelectTint_NeverFallsThroughToTheModelCategoryDefaultForAnyDefinedWeapon` | **Every defined weapon must have a cataloged tint.** A new ranged role without catalog entries fails here before the geometry is ever reached. |
| `WeaponVisualCatalogTests.cs:289` `GetTints_IsNonEmptyForEveryDefinedWeaponAsOfVIS011` | Same requirement, stated as a non-empty tint list per weapon. |
| `WeaponVisualCatalogTests.cs:224` `PawnSilhouette_ReturnsEveryWeaponsOwnSilhouetteAsOfVIS011` | Every weapon resolves its own pawn silhouette entry. |
| `WeaponVisualCatalogTests.cs:141` and `:589`-`:601` (per-weapon evidence-tier facts) | Every catalog entry carries a defined evidence tier and a non-empty note, and every label uses the unchanged pair form (`:185`, `:609`). This is where `CLAUDE.md` section 7's naming policy is mechanically enforced for the four new weapons. |
| `tests/Hukbo.Client.Tests/AppearanceRosterContractTests.cs:70` `All_RosterCountMeetsTheFiftyPresetFloorAndPinsAtFiftyThree` | The appearance roster is pinned at 53 presets; adding presets for ranged warriors moves this pin. |
| `AppearanceRosterContractTests.cs:388` `SatisfiesDifferentiation_HoldsForEveryPairWithinEachRegionalBlockAcrossTheFullRoster` | Every pair of presets within a regional block must remain visually differentiable — new archer or arquebusier presets are held to it too. |


