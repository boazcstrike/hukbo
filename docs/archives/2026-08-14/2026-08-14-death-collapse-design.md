# Death collapse and the prone body — design

**Archived: reference only.** The design this document describes was built and
merged to `main` in the feature commit `0d4b34e` on 2026-08-14. Never execute it,
never treat it as a live design, and never cite it as the reason to make a
change. The live contract for this project remains `CLAUDE.md`,
`SIMULATION-GAME-STANDARDS.md`, `docs/development/testing.md`, and
`docs/development/smoke-checklist.md`.

Its plan document, "Death collapse and the prone body — plan", sits beside this
file in the same archive folder and records what shipped, what drifted, and the
one item that did not land. Section 2 here, "What this is not", lists work that
was deliberately excluded and is still unbuilt: each of those needs its own
design before anyone starts it.

Date: 2026-08-14. Scope: `Hukbo.Client` presentation and rendering only. No
simulation type, no tick stage, no event, and no state or event hash is touched
by anything in this document.

## 1. What is wrong today

A warrior that dies is held in its struck pose for `LethalHoldSeconds` (0.34s),
and then it turns grey and keeps standing up. That is the whole of what a
spectator sees, and it is what the corpse placeholder shipped on 2026-08-13
deliberately settled for — its own section 2 says so in as many words: "This is
a placeholder, and the name is meant literally... A real casualty layer — bodies
that fall in a direction, that decay, that pool, that thin out under a cap, that
read differently for a leader — is a separate piece of work with its own design
document and its own smoke rows."

This is the first half of that separate piece of work. Two specific complaints
are in scope and nothing else:

1. **There is no death animation.** The lethal hold is a freeze, not a motion.
   Nothing about the transition from fighting to dead is animated; the pawn's
   colours simply change between one frame and the next.
2. **A corpse stands up.** A dead warrior is drawn in the identical standing
   silhouette it fought in, only desaturated and crossed out. From any distance
   at which the desaturation is hard to judge — which is most of a battle, at
   most camera stations — a field of corpses reads as a field of warriors.

The body must end the sequence **lying flat on the ground**, not leaning, not
tilted, not slumped. A tilt is what the standing silhouette looks like when it
is rotated part of the way; the requirement is the full quarter turn, so the
body's long axis is horizontal and its head rests on the ground plane at a
body's length from the feet.

## 2. What this is not

Still not a casualty system. Out of scope, explicitly, and each of these stays
out until it gets its own design:

- Decay, fading, or any lifetime cap on corpses. A body persists for the rest of
  the battle, exactly as the placeholder already made it.
- Pooling blood beneath a corpse, or any interaction between a corpse and the
  ground layers (`TrampleMarkSystem`, `GrassRenderer`).
- A dropped weapon. The weapon stays in the warrior's hand and turns with the
  body. A weapon that leaves the hand needs its own position, its own ground
  layer, and its own budget line, and none of the three exists.
- A distinct read for a fallen leader or a fallen chief.
- Corpses stacking, sorting among themselves, or displacing the living.
- Any change to how or when the simulation decides a warrior is dead.

## 3. The three-phase sequence

One warrior's death is three phases on the presentation clock. Phase 1 already
exists and is not touched.

| Phase | Duration | What is drawn | Owner |
| --- | --- | --- | --- |
| **Lethal hold** | 0.34s (`DefenderReaction.LethalHoldSeconds`) | The struck pose, undesaturated and unmarked, exactly as today | `DefenderReactionSystem` (unchanged) |
| **Collapse** | 0.45s (`CollapsePose.CollapseSeconds`, PROVISIONAL) | The whole pawn rotating about its own foot anchor from upright to prone, overshooting slightly at the impact and settling back | `DeathCollapseSystem` + `CollapsePose` (new) |
| **Prone** | rest of the battle | The same pawn held at its final prone rotation, motionless | the same two types |

The collapse begins on the first frame the agent resolves to
`PawnVisualState.Dead` — that is, the first frame after its lethal hold expires
— and not on the tick `IsAlive` goes false. Those two moments are different by
design and by up to a third of a second: the lethal hold exists so a kill reads
as a kill, and starting the fall inside it would consume the window that the
2026-08-13 lethal-blow legibility work bought. `PawnVisualStateResolver` already
draws exactly this line and does not change.

### The collapse curve

`CollapsePose.Resolve(ageSeconds, finalRotationRadians)` is a pure function of
the two, returning one angle. It has two segments:

- **Fall**, from age 0 to `ImpactShare` (0.82) of the duration. The angle runs
  from zero to `finalRotation + overshoot`, eased **in**, as `t * t`. A falling
  body accelerates; an ease-out here would read as a controlled lie-down.
- **Settle**, from `ImpactShare` to the end. The angle runs from
  `finalRotation + overshoot` back to exactly `finalRotation`, eased out. This
  is the body's mass arriving on the ground.

`SettleOvershootRadians` is 0.10 (about 5.7 degrees) and is PROVISIONAL
presentation choreography, not a measured quantity. At and after the full
duration the function returns exactly `finalRotation` and never moves again, so
the prone phase needs no separate code path — it is the collapse evaluated past
its own end.

### Which way a body falls

`finalRotation` is `±(π/2 + jitter)`:

- **The quarter turn is not negotiable.** π/2 is what puts the body flat. This
  is the requirement in section 1 and the reason `SettleOvershootRadians` is
  small: an overshoot large enough to be read as a bounce would also be large
  enough to be read as a body lying at an angle.
- **The sign** is the direction of the killing blow across the screen. A warrior
  struck from its left falls to its right. `DefenderReaction.DirectionX` carries
  that, in screen axes, and the reaction outlives the lethal hold by 0.16s
  (0.50s against 0.34s), so it is still present at the exact frame the collapse
  registers. When it is absent or vertical — a death with no surviving reaction,
  or a blow arriving straight up or down the screen — the sign falls back to the
  low bit of the entity id.
- **The jitter** is at most `FallJitterRadians` (0.14, about 8 degrees) derived
  from the entity id through the same presentation salt every other per-entity
  presentation variation uses. Without it a field of dead reads as a stamped
  pattern of identical bodies. With it the bodies still all lie flat: 8 degrees
  is inside what a body on uneven ground looks like and outside what reads as a
  lean.

Both the sign and the jitter are captured once, when the collapse registers, and
never recomputed. A corpse that changed which way it was lying between frames
would be worse than one that never fell.

## 4. How a rotated pawn is drawn

`PawnRenderer` draws about thirty axis-aligned `Rectangle` quads and a handful of
`Vector2` line segments per pawn, all in one shared `SpriteBatch` batch with
every other pawn on the field. A batch cannot change its transform matrix
mid-batch, and beginning a second batch for corpses would break the single-batch
property the render budget is stated against. So the rotation is applied per
quad.

That machinery already exists and is already proven: `DrawRotatedBlock`, added
for the shield's fixed active-posture rotation (VIS-015, S12), draws one
rectangle rotated about an arbitrary pivot using the `SpriteBatch.Draw` overload
that takes a rotation, an origin, and a scale. The change generalises it.

**`PawnTransform`** (new, `src/Hukbo.Client/Rendering/PawnTransform.cs`) is a
rigid plane transform stored as an angle plus a translation, so
`p ↦ rot(θ)·p + t`. It is stored in that form, rather than as an angle plus a
pivot, for one reason: two rotations about two different pivots compose into
exactly one value of this shape, and the shield needs precisely that. A shield on
a collapsing body is rotated by its posture angle about its own centre and then
by the collapse angle about the foot anchor, and `PawnTransform.Then` produces
the single transform that does both. An angle-and-pivot representation cannot
hold the result.

- `PawnTransform.Identity` is the neutral value, and `IsIdentity` is true for it.
- `AboutPivot(pivot, radians)` returns `Identity` when `radians` is zero.
- Every drawing helper in `PawnRenderer` takes one `PawnTransform` and routes
  every quad through a single new `DrawQuad`, which **takes the existing
  axis-aligned `SpriteBatch.Draw(texture, rectangle, color)` path unchanged
  whenever the transform is the identity** and the rotation overload otherwise.

That last property is what makes this change safe. A living pawn's transform is
`Identity` at every call site, so a living pawn's draw sequence is
byte-for-byte the sequence it is today — same overload, same arguments, same
pixels — and every pinned geometry test, quad count, and render-budget figure
that describes a living pawn is unaffected by construction rather than by
re-measurement.

### What does not rotate

**The ground ring does not rotate.** It is the faction-tinted footprint on the
ground plane, not part of the body: it marks where the warrior is standing, and
where a corpse fell. It is nearly square and centred on the foot anchor, so
rotating it would move almost nothing and mean something wrong. It is the single
documented exception, and it is stated at its call site.

Everything else on the pawn rotates: legs, feet, secondary equipment, torso,
diagnostic placeholder, armour, sash, shield, head, head treatment, adornments,
arms, swing trail, weapon, and the three marks. The arms and the swing trail are
always empty for a corpse — a corpse carries no attack pose — but they are routed
through the transform anyway rather than special-cased, because a layer that is
"always empty here" is exactly the layer that is not empty after the next change.

## 5. Culling a body that lies down

`ArenaGame.Rendering.cs` culls each pawn against `PoseBlindVisualBounds`, a
rectangle built from the **standing** silhouette. It is pose-blind deliberately:
a pose-aware cull would make the set of drawn pawns a function of animation
phase, so the same tick would draw a different list depending on where each
clock happened to sit.

A prone body does not fit inside its standing rectangle. The standing rectangle
is tall and narrow; rotated a quarter turn it is short and wide, and it reaches
roughly a body's height sideways from the foot anchor. Culled against the
standing rectangle, a corpse near the panel edge would vanish while most of it
was still on screen — which is the exact failure ("no visible casualty") that
the corpse placeholder was written to fix.

The fix keeps the property that matters. For an agent resolving to
`PawnVisualState.Dead`, the cull rectangle becomes the **square centred on the
foot anchor whose half-side is the greatest distance from the foot anchor to any
corner of the standing rectangle**. That square contains the standing rectangle
under *every* rotation about that anchor, so it covers the whole collapse and the
final prone pose at once, and it does not depend on the collapse clock, the fall
sign, or the jitter. The drawn set therefore still does not vary with animation
phase; it varies with aliveness, which the two-pass corpse draw order already
depends on.

Living pawns keep the exact rectangle they use today. The cost is confined to
corpses near the panel edge, where a slightly generous rectangle admits a pawn
whose quads then fall outside the scissor and cost nothing to clip.

## 6. Colour, and the mark

Two changes, both small, both stated so neither is mistaken for a side effect.

**The corpse desaturation is softened, from a 0.68 blend toward the dead grey to
0.40.** The point of the original blend was that a standing pawn had nothing but
its colour to say it was dead. A body lying flat on the ground says it in the
silhouette, so the colour no longer has to carry the whole message, and at 0.68 a
field of dead reads as a field of grey furniture rather than of fallen warriors.
0.40 is PROVISIONAL and is the tuning knob if the manual smoke rows say the read
is wrong in either direction.

**The crossed-out dead mark is now drawn at `PawnDetailTier.Low` only.** At
Medium and High the prone silhouette carries the read and an X over a body is a
user-interface marker painted on a battlefield. At Low the pawn is a handful of
pixels, the quarter turn is not resolvable, and the mark is the only signal
left — the same argument by which `DrawHeadTreatment` is already gated off at
Low and `DrawShield` is deliberately kept at every tier.

`SubmissionCount.CountStateMark` gains the detail tier so its count keeps
matching what the renderer actually submits. A Medium or High corpse costs two
quads **fewer** than it does today; a Low corpse is unchanged. Nothing in this
change adds a quad to any pawn in any state — a rotated quad is one quad — so the
arena batch's pinned totals move down or stay level, never up.

## 7. Where the state lives

**`DeathCollapseSystem`** (`src/Hukbo.Client/Presentation/`), owned by
`PresentationCoordinator` alongside every other system with a one-battle
lifetime, and cleared by `ResetFor` with them.

Storage is an array indexed by the agent's **ordinal position in the roster**,
with the entity id stored beside each entry and compared on every read — the
same shape and the same validity rule as `PawnAppearanceCache`, and for the same
reason. `BattleSimulation.Agents` is a fixed-size array filled element for
element every tick; death clears `IsAlive` in place and never removes,
compacts, or reorders, so ordinal *i* names the same warrior for the whole
battle. That makes every lookup a single indexed read with no hashing, no
allocation, and no per-frame scan, which matters because at a thousand units a
linear scan per pawn per frame is a million comparisons.

Three calls, all from paths that already exist:

- **`Observe(agents, defenderReactions)`** once per frame from `ArenaGame`'s
  update, immediately after `ReleaseAttackContactsForDraw`, so a lethal contact
  released this frame has already registered its reaction and the fall direction
  is readable. It registers any agent that is not alive, is not inside a lethal
  hold, and has no entry yet.
- **`Advance(elapsedSeconds)`** from `PresentationCoordinator.AdvanceEffects`,
  inside the `advanceContacts` group. A collapse is contact presentation and
  must freeze when playback is paused, exactly as the lethal hold does. A
  spectator who pauses mid-fall sees a body mid-fall, and that is correct.
- **`TryGetPose(ordinal, entityId, out CollapsePose)`** from the pawn draw loop.

The system never reads the wall clock, never touches the simulation, and
computes nothing that is stored, hashed, or snapshotted.

## 8. The nine questions (SIMULATION-GAME-STANDARDS.md §10)

**Can a spectator discover this effect without reading source code?** Yes, and
this is the whole point of the change. A warrior is struck, holds for a third of
a second, topples over in the direction it was struck, and stays on the ground
for the rest of the battle. Nothing about it needs explaining.

**What does it change in the simulation?** Nothing. No type, no stage, no event,
no hash. `Hukbo.Core` is not opened.

**What is the determinism impact?** None. Every value here is presentation-side
and driven by the unscaled frame clock, which nothing authoritative reads. The
gate's headless workload does not construct a client.

**Is it bounded?** Yes. Storage is one array of roster length, allocated once per
battle. Quad cost per corpse is level or two quads lower than today.

**Historical accuracy?** Nothing here is a historical claim. A body falling over
is not a cultural identification, no new weapon or garment is drawn, and every
tuning number in this document is labelled PROVISIONAL.

**What can be tested automatically?** `CollapsePose`'s curve (monotone through
the fall, exact final angle at and after the end, overshoot bounded), the fall
direction rule, `PawnTransform`'s algebra (identity, composition, that a quarter
turn about the foot anchor takes the head to the ground plane), the prone cull
envelope's containment, `DeathCollapseSystem`'s registration and its ordinal
validity check, and `SubmissionCount`'s tier-gated mark.

**What can only be tested by a person?** Whether the fall reads as a death.
Whether bodies at Medium and High tier are legible as bodies. Whether the
softened desaturation is right. Whether a field of corpses at 500 per team is
readable or is visual noise. These become the DC smoke rows and none of them may
be flipped by an agent.

**What is the rollback?** Delete the `Observe` call. Every transform becomes
`Identity`, every pawn draws the axis-aligned path it draws today, and the build
is the corpse placeholder again.

**What is deliberately left for later?** Everything in section 2.
