# Attack animation V2 — design

Date: 2026-08-08
Status: approved. The companion implementation plan is
`docs/plans/2026-08-08-attack-animation-v2.md`; code changes begin only by
executing that plan.

## 1. Goal

Make close- and medium-zoom attacks look excellent while keeping a 200-warrior
battle readable. Every weapon must have a recognizable procedural attack
family, every blow must point toward its actual target, and the weapon,
defender reaction, blood or clash, impact mark, and sound must agree on one
contact moment.

This is presentation work. The simulation continues to decide whether and when
an attack happens, which target it names, which body part it reaches, how much
damage it deals, and how the defender resolves it.

## 2. Known facts

- `BattleEventKind.Attack` is emitted after the attack and defensive resolution
  have been committed. It is the authoritative contact instant, not a request
  to begin a wind-up.
- `SwingAnimationSystem` currently creates a new animation at age zero from
  that event. `SwingGeometry` does not reach contact until 56 per cent of its
  250-millisecond duration, so the weapon reaches the victim 140 milliseconds
  after the blood, hit, clash, and sound channels have already fired at 1x.
- The animation clock is speed-scaled but the wound and clash effects are not.
  The mismatch therefore changes at 2x and 4x.
- Attack poses are resolved before the current frame advances and ingests
  simulation events. A newly ingested swing cannot contribute a pose until the
  following update.
- Direction is reduced to the sign of the target's x offset. Vertical and
  diagonal targets influence torso translation but not the weapon's true aim.
- All four weapon identities share one timing table, arc, extension, torso
  lean, trail, and recovery. `SwingAnimation` does not retain `WeaponId`.
- Kampilan and Wasay are configured as two-handed weapons. Kalis and Itak are
  one-handed and have both solo and shield-paired attribute rows, even when a
  particular roster version fields only the solo loadout.
- The renderer is procedural and allocation-conscious. A Medium/High swing
  trail is already bounded to six segments, and presentation stores are fixed
  capacity.
- A lethal victim stops participating in the ordinary live-pawn draw before a
  delayed swing could reach it. Contact must therefore be immediate, and the
  presentation needs a bounded lethal-contact hold if the defender is to remain
  visible through the blow.

## 3. Material assumptions

1. "Excellent" prioritizes close- and medium-zoom motion quality. Low detail
   still communicates direction and outcome, but omits small limb and trail
   detail.
2. The attack event remains the only contact authority. The client will not
   predict damage or create a new authoritative wind-up event.
3. Motion choreography is **Provisional reconstruction** and presentation
   tuning. Weapon names, silhouettes, grip configuration, and combat profiles
   constrain it, but the exact movements are not claimed as documented
   sixteenth-century technique.
4. The first attack cannot show a literal pre-event wind-up without buffering
   the whole presentation or changing combat timing. A combat-ready pose will
   carry the approach; the event itself begins at contact, followed by recovery
   and readiness for a possible next blow.

## 4. Approaches considered

### 4.1 Directional patch

Aim the existing weapon line with `atan2`, add easing, and leave the rest of
the body and timing model largely intact.

This is the smallest change and would fix a conspicuous defect. It would not
give the four weapons different weight, stance, hand use, or outcome motion,
so it does not meet the quality target by itself.

### 4.2 Procedural attack rig — selected

Build a target-local attack basis, a weapon-motion catalog, a composed action
stance, articulated arms at visible detail tiers, a contact latch, and distinct
resolution branches. This extends the renderer Hukbo already has, preserves
appearance variants, and needs no external animation assets.

### 4.3 Authored directional sprite sheets

Eight-direction or sixteen-direction sheets have the highest authored-art
ceiling, but would introduce an atlas and asset pipeline, appearance
combinatorics, licensing/provenance work, and substantially more memory. They
are not the first step while the existing procedural renderer remains capable
of a large improvement.

## 5. Timing contract: the event is contact

An ingested `Attack` event produces an immediate contact pose. The same draw
must also show the event-driven blood or clash, hit accent, defender response,
and sound cue. There is no post-event anticipation phase.

The client timeline is:

1. **Contact latch** — held until it has contributed to at least one draw.
2. **Impact response** — compression, recoil, redirection, or follow-through,
   selected by `AttackResolution`.
3. **Recovery** — eased return from the committed pose.
4. **Readiness** — a weapon-specific guarded pose while the warrior remains in
   combat, which can read as preparation for the next blow without predicting
   that a blow will happen.

Pose resolution moves after simulation advancement and event ingestion, before
the draw. Large elapsed times may advance recovery aggressively but may never
consume a newly latched contact before it has been presented.

Multiple contacts produced during one catch-up update use a bounded pending
contact buffer of **five bundles per warrior**. The normal 20 Hz client can
accumulate at most ten ticks under its 0.5-second clamp; the fastest registered
V4 combo cadence is two ticks, so five preserves every default-preset contact
through the maximum catch-up window. A later contact from the same warrior does
not reset the pose to anticipation; it installs a new contact keyframe and
continues the weapon's combo choreography.

The queue owns a complete `AttackContactBundle`, not only a weapon pose. The
bundle carries the attack context, related hit/blood or clash data, defender
reaction, lethal-contact hold, and contact sound request. A bundle is released
to all of those channels together. Event-feed and battle-report ingestion may
remain immediate because they are semantic records rather than audiovisual
contact feedback.

Each `Attack` event already carries its own resolution and damage value, so its
hit, blood, clash, reaction, and weapon sound come from that event alone. The
later `Damage` event is an aggregate semantic record and does not spawn another
contact effect. When a same-tick `Death` follows several landed attacks against
one target, the lethal hold and death sound attach exactly once to the
highest-sequence landed attack bundle for that target. Earlier contributing
attacks remain ordinary landed contacts. This rule also applies independently
per target for mutual deaths and is stable across catch-up updates.

The five-bundle limit is fixed and proportional to the scenario's agent
capacity; it is not a growing queue. A custom tick rate or future combat preset
that produces a sixth pending contact for one warrior deterministically
coalesces it into that warrior's newest pending bundle, preserving the latest
event context and incrementing a diagnostic collapse count. The intermediate
pose, wound/clash effect, defender response, and sound are collapsed together;
no orphan effect or late sound may survive without its contact pose.

At pause, active attack poses, pending bundles, defender reactions, hit effects,
blood, clashes, and other battlefield-contact transient clocks freeze together.
No pending sound request is released while paused. An already-started one-shot
sound is allowed to finish rather than being stopped mid-sample; that exception
is explicit and tested so it cannot silently expand to queued sounds. Ambient
grass may continue on its existing unscaled clock. Resume continues from the
same pose without duplicating a contact. Next Round and Full Reset clear active
attacks, pending bundles, reactions, transient contact effects, and one-shot
latches.

## 6. Target-local procedural rig

Every attack resolves a normalized forward vector from attacker to target and
its perpendicular lateral vector. All weapon, hand, arm, torso, and stance
offsets are expressed in that basis. `atan2(DirectionY, DirectionX)` determines
the actual contact heading; no left/right-only mirror remains.

The active pose composes these channels:

- weapon base angle, angular travel, reach, and trail envelope;
- planted-foot/action-stance weight and gait suppression;
- torso translation and counter-rotation;
- head counter-motion kept smaller than the torso motion;
- weapon-hand and support-hand positions;
- two-segment arms at Medium and High detail;
- off-hand shield guard for legal shielded loadouts;
- attacker impact response and bounded defender reaction.

Low detail keeps the true target-facing weapon endpoint and a readable contact
snap, but draws no articulated arms and no trail. Medium and High draw the full
rig. Neutral geometry must remain bit-for-bit identical when no attack pose is
active.

All interpolation uses named easing functions rather than phase-linear `Lerp`
alone. Strike/contact should arrive sharply, weight should settle rather than
stop mechanically, and recovery must return exactly to the neutral pose.
Easing functions are pure static math over structs and allocate nothing.

## 7. Weapon-motion taxonomy

The stable `WeaponId` remains the authority for weapon identity. The client
maps it exhaustively to an `AttackMotionFamily` and an immutable
`AttackMotionProfile`. An unknown value fails visibly in tests rather than
silently reusing another weapon's movement.

The motion-family names below are internal presentation language, not new
player-facing weapon names and not historical claims.

| Weapon identity | Evidence boundary | Grip/loadout class | Motion family | Procedural signature |
| --- | --- | --- | --- | --- |
| Kampilan — Great Blade | **Documented, form uncertain** | Two-handed; shield forbidden | `CommittedCleaver` | Broadest target-relative diagonal/overhead cut; long visible commitment; both hands drive the blade; planted weight transfer; strong but controlled recovery; a two-step combo changes the return side rather than replaying the same arc. |
| Wasay — War Axe | **Documented, form uncertain** | Two-handed; shield forbidden | `HeadWeightedChop` | Long preparation followed by late head-led acceleration; downward/oblique contact; shorter, denser trail than the Kampilan; hardest stop or rebound; support hand visibly anchors the haft; longest recovery. |
| Kalis — Thrusting Blade | **Documented** name and class; form kept conservative | One-handed; solo or shield-paired | `LinearThrustCut` | Primarily linear extension toward trunk targets, with a restrained cut on recovery; replaces a broad arc with a short linear afterimage; fastest direct return. Solo stance permits more torso reach; paired stance keeps the shield between defender and weapon line and reduces body rotation. |
| Itak — Work Blade | **Provisional reconstruction** for this name/form pairing | One-handed; solo or shield-paired | `CompactChopSlash` | Short near-target chop/slash; smallest reach envelope; quickest recovery and clearest multi-step alternation, selected from authoritative combo position rather than random variation. Solo stance uses the free hand for balance; paired stance keeps the shield readable and compresses the weapon-side motion. |

Each profile owns only presentation quantities: contact angle offsets, recovery
duration, extension envelope, lean, stance weight, arm targets, trail width and
opacity, and combo-side alternation. It must not copy damage, reach, hit chance,
or other gameplay quantities into the client.

The shipped default V4 roster fields all four weapons solo. Registered V2
replays may still field shielded Kalis and shielded Itak, so those paired poses
are compatibility requirements rather than speculative new loadouts. They are
shield overlays on the Kalis and Itak families, not fifth and sixth weapon
families.

The catalog shape is one exhaustive `AttackMotionProfile` per `WeaponId` plus a
`ShieldMotionOverlay` for legal paired loadouts. Appearance tints and catalog
silhouettes do not change a weapon's motion family. No profile may name an
invented guard, stance, school, or technique, and combat damage or cooldown
must not be presented as a measured historical property of weapon mass.

The attack event's `Weapon`, attacker `Shield`, `Resolution`, and
`ComboPosition` select the profile and variant. This information is already
authoritative event context and is currently discarded by the swing store.

## 8. Resolution-specific choreography

Every weapon family supplies five contact branches while sharing one semantic
meaning:

- `Landed`: brief local compression at contact, then a weighted settle.
- `ShieldBlocked`: hard, short rebound away from the shield surface; the
  defender shield braces into the contact.
- `Parried`: lateral redirection with attacker hands and weapon displaced off
  line; the defender weapon reaction opposes it.
- `Deflected`: shallower glancing continuation rather than the parry's hard
  reversal.
- `Evaded`: full follow-through and controlled over-rotation, with no blood,
  clash cross, or contact recoil.

The exact magnitudes vary by family. The semantic distinction does not: a
Kalis parry and a Wasay parry may move differently, but both must read as a
weapon meeting and redirecting a weapon.

## 9. Presentation architecture

The current `Swing*` names describe only angular cuts and do not fit a Kalis
thrust. The implementation may rename this bounded client subsystem to
`AttackAnimation`, `AttackAnimationSystem`, `AttackPose`, `AttackGeometry`, and
`AttackPoseResolver`. This is an internal client refactor, not a Core API.

The intended data flow is:

```text
BattleSimulation Attack event
  -> PresentationCoordinator.IngestTick
  -> fixed-capacity AttackContactDispatcher (whole contact bundle)
  -> fixed-capacity AttackAnimationSystem + contact effects + sound request
  -> AttackMotionCatalog(WeaponId, ShieldId)
  -> AttackGeometry(event context, profile, age/contact latch)
  -> AttackPoseResolver
  -> PawnGeometry + PawnRenderer
```

A bounded defender-reaction record captures the target ID, contact position,
resolution, and lethal flag. A lethal target remains renderable through the
contact/reaction window using the existing post-tick `AgentView`; it may not
vanish before the weapon reaches the guaranteed contact pose. This hold is
presentation-only and expires or clears deterministically within the client.

## 10. Accessibility and density policy

`MotionIntensity` must reach attack resolution:

- **Full:** complete stance, torso/head counter-motion, arms, trail, and outcome
  response.
- **Reduced:** true direction and outcome remain; lean, recoil, arm travel, and
  trail strength are reduced.
- **Off:** essential direction, contact, and outcome silhouette remain; trails,
  repeated overshoot, and nonessential body exaggeration are removed.

Gore settings continue to own blood quantity and appearance. Motion Off must
not hide whether a blow landed, was blocked, was parried, was deflected, or
missed.

No global hit-stop, simulation pause, full-screen flash, or routine camera
shake is introduced. At hundreds of warriors those effects turn individual
impact into continuous noise. Impact emphasis remains local to attacker,
defender, weapon, and bounded contact effects.

## 11. Performance and bounds

- One active timeline per warrior plus a fixed pending-contact/reaction budget.
- No per-frame heap allocation, splines, closures, or growing collections.
- Existing six-segment trail ceiling remains; profiles change its envelope, not
  its maximum segment count.
- Articulated arms add at most four quads per active Medium/High pawn.
- All rotated weapon, secondary axe-head, arm, trail, and reaction bounds must
  be included in conservative culling.
- Exact active-pawn quad counts and 200/500-warrior worst-case arithmetic are
  pinned in tests and recorded in the render budget.

## 12. Verification criteria

Automated verification must prove:

1. An ingested attack produces a contact pose in the same presentation frame.
2. Contact cannot be skipped at 30, 60, or 120 Hz under 1x, 2x, or 4x updates.
3. All eight cardinal/intercardinal headings point toward the target by an
   endpoint-to-target dot-product assertion, not a left/right sign assertion.
4. Every `WeaponId` resolves to its declared family, and each family produces
   objectively distinct timing and geometry.
5. Solo and shield-paired Kalis/Itak poses keep their legal off-hand equipment
   readable.
6. Landed, blocked, parried, deflected, and evaded poses are distinct.
7. Combo contacts install new contact keys without restarting anticipation.
8. Pause/resume and both reset paths preserve their lifecycle contracts.
9. Full, Reduced, and Off retain combat meaning while changing nonessential
   motion.
10. Neutral layouts are unchanged, culling contains every active element, and
    active-path quad budgets stay pinned.
11. Five contacts per warrior survive the maximum default 20 Hz catch-up
    window in order; a sixth under a custom stress fixture coalesces the whole
    contact bundle and records the collapse without orphan effects or sound.
12. Client presentation changes do not alter Core state, event ordering, state
    hash, event hash, winner, or terminal tick. Before implementation, the plan
    records fixed normal-attack and combo seed/preset headless reports. Final
    verification compares their ordered events and report fields byte for byte,
    in addition to requiring a zero `src/Hukbo.Core` diff and the canonical
    determinism workload.

Manual rows remain `PENDING` until a person observes them. They cover each
weapon and resolution at close zoom, eight headings, 1x/2x/4x, pause/resume,
gait composition, motion/gore settings, 200-warrior default fit, and 500-warrior
stress. A passing build is not visual approval.

The canonical final gate is `./scripts/verify.ps1`, run by the orchestrator in
this worktree with its real output recorded.

## 13. Ordered delivery shape

1. Rename/extract the generic attack presentation types and lock the
   event-is-contact timing contract.
2. Add the exhaustive weapon-motion catalog and profile tests.
3. Implement true target-local direction and contact-safe pose resolution.
4. Implement the complete Kampilan family as the vertical slice.
5. Add attacker arms, stance/gait composition, defender reaction, and lethal
   contact hold.
6. Add Wasay, Kalis solo/paired, and Itak solo/paired profiles.
7. Wire resolution branches, motion policy, culling, and render budgets.
8. Run focused tests, the broader client suite, manual visual review where
   available, independent review, and the canonical gate.

## 14. Risks and blockers

- **First-blow anticipation:** no pre-contact event exists. The design uses a
  ready pose and never lies by predicting a hit. A full fixed-latency render
  buffer is a separate future design if literal pre-contact telegraphing is
  later judged essential.
- **Catch-up overload:** several ticks may ingest in one update. Five bundles
  per warrior cover the default 20 Hz maximum window and fastest registered V4
  combo cadence. Higher-rate/custom overflow coalesces the complete newest
  bundle and is diagnosed; the plan must pin the byte and quad cost of this
  capacity before implementation.
- **Gait conflict:** independent stride and attack offsets can tear the body.
  Attack stance must blend gait toward planted feet and restore it through
  recovery.
- **Shield confusion:** paired Kalis/Itak cannot reuse solo arm targets without
  obscuring or crossing the shield.
- **Lethal disappearance:** the live-pawn draw currently drops a victim before
  delayed animation could meet it. Contact is immediate and the bounded lethal
  hold is required before the attack can be called visually complete.
- **Formation noise:** arms and trails must remain tiered; the low-detail
  silhouette and the active-quad budget are release gates.
- **Historical overclaim:** exact choreography remains labelled Provisional
  reconstruction. It may not be presented as documented martial technique.

## 15. Explicitly out of scope

- Changes to attack cadence, cooldowns, damage, reach, hit chance, targeting,
  clash resolution, movement, or AI.
- New Core wind-up state or authoritative events.
- Sprite-sheet or skeletal-animation asset pipelines.
- Rigid-body physics, ragdolls, projectile ammunition, durability, or per-limb
  damage.
- Global hit-stop, routine screen shake, or full-screen flashes.
