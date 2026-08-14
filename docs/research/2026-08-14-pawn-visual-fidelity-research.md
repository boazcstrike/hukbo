# Pawn visual fidelity — consolidated research

Date: 2026-08-14

This note consolidates six parallel research passes run on 2026-08-14 for a
visual-fidelity package covering four subsystems: gait animation, attack
animation V2, projectile props and embedded projectiles, and lethal blow
legibility. Two passes surveyed the public record — player and developer
discussion of animation legibility in mass-battle games, and published
procedural-animation technique. Four passes read this repository: gait code,
attack-animation code, projectile and lethal-blow code, and the binding
documents.

Everything in section 2 was re-verified against the files on disk by the
integrator before being written here. Where a research pass and the disk
disagreed, the disk won and the disagreement is recorded.

Out of scope throughout, because another session owns them: death collapse and
the prone corpse, UI chrome nine-slice, armor accent and trample marks,
last-stand engagement, cohort lateral spread, inspector row wrapping, and every
part of Sandata.

## 1. What the four subsystems actually are today

All four shipped. Every smoke row belonging to them has closed `PASS`, and each
family's section was deleted from `docs/development/smoke-checklist.md` under
that file's rule that a wholly passing family is a record rather than a
checklist. The sixteen rows still open in that file belong to GPU render,
calibrated army composition, death collapse, and UI chrome — none of them ours.

A green checklist is therefore not the question. The question is the engineering
debt the documents themselves admit to, listed in section 3.

## 2. Verified facts about the code

### 2.1 Gait

Stride phase is driven by distance travelled, not by elapsed time.
`GaitAnimationSystem.Advance` computes the frame's displacement from the agent's
previous raw position and advances phase by `distance / StrideCycleDistanceRaw`,
where `StrideCycleDistanceRaw = 6000f` at `GaitAnimationSystem.cs:75` and the
advance itself is at `GaitAnimationSystem.cs:233`. There is no clock anywhere in
the file.

**The "one stride cycle per three hundred seconds" defect is already fixed.** It
was one of the two causes measured against the strike-while-moving smoke row.
Displacement below `CrawlThresholdRawPerTick = 60f`, at `GaitGeometry.cs:57` and
gated at `GaitGeometry.cs:113`, now resolves to a neutral stance that eases
toward rest instead of advancing phase, and a test pins that behaviour. A plan
must not spend a task re-fixing it.

The detail tiers are `Low`, `Medium`, and `High`, selected at `0.95` and `1.80`
apparent scale. Those two thresholds are duplicated on purpose in
`PawnGeometry.cs:235-236` and again in `DetailTierGate.cs:23-24`, and the second
copy's own remarks explain why the two are deliberately not shared. Do not
"fix" that by consolidating them.

Legs and feet cost between zero and four quads per pawn, counted at
`SubmissionCount.cs:105-106`. At `Low` they cost nothing at all, because
`PawnGeometry` returns empty bounds for them at that tier — the lowest tier
draws no legs by construction.

All thirty-three gait tests are GPU-free.

### 2.2 Attack animation

**There is no windup phase, and its absence is deliberate.** The doc comment at
`AttackAnimation.cs:6-9` states it outright: Core's attack event is the contact
authority, so the animation has nothing to anticipate with. Only
`RecoveryProgress` is a normalized zero-to-one value, at
`AttackAnimation.cs:87-102`. What the `Phase` property calls windup and contact
are boolean, age-gated branches at `AttackAnimation.cs:62-80`.

`ConservativePawnCull` has **zero production callers**. The only references
under `src/` are the class's own doc comments and three comments in
`PawnGeometry.cs`, at lines 925, 2243, and 2348 — the integrator confirmed this
by grep. The class's own doc comment says "Not adopted, and deliberately so",
recording that the GPU-016 task was dropped on 2026-08-07. The live cull is
`PoseBlindPrefix.PoseBlindVisualBounds` instead. Smoke row AA-24 passed at a
desktop against a feature that was never wired.

Note for anyone carrying an older note forward: the line numbers previously
circulated for those references, 2136 and 2241, are stale. The real ones are
above.

Combo side is not a per-weapon property. Each weapon's `LateralBias` in
`AttackMotionCatalog.cs:15-141` is a non-negative magnitude, and the sign comes
from `(position & 1)` in `ResolveComboSide` at `AttackGeometry.cs:135-136`,
applied uniformly to every motion family.

Recovery durations are per weapon: Kampilan 300 ms, Wasay 360 ms, Kalis 200 ms,
Itak 170 ms, Bangkaw 280 ms, Busog 220 ms, Arquebus 450 ms.

The type in `SubmissionCount.cs` is actually declared `PawnQuadCount`. A
Low-tier unshielded, unarmored pawn is pinned at seventeen quads.

### 2.3 Projectiles and lethal blow

Lethal blow legibility is genuinely wired. Five channels fire only on a killing
blow — the blood spurt and mark tier in `BloodGeometry.cs:214-320`, the hit-ring
and shard tier in `HitEffectGeometry.cs:84-114`, the defender reaction hold and
scale in `DefenderReactionSystem.cs:29,42,87`, the post-draw animation hold at
`AttackAnimation.cs:60`, and the death-collapse timing gate at
`DeathCollapseSystem.cs:119`. All five fan out from a single `MarkLethal` at
`AttackContactDispatcher.cs:303-323`, and all have live callers. There is no
orphan channel here.

Their durations are deliberately ordered: the defender reaction lasts 0.50 s,
the animation hold 0.34 s, and the hit pulse 0.30 s, and
`DefenderReactionSystem.cs:32-40` documents that ordering as a contract.

Projectiles already have a minimum-size floor, `MinimumDimension = 1f` at
`ProjectileGeometry.cs:110`, applied through `Scaled` at
`ProjectileGeometry.cs:473-474` and pinned by a test. Prop scale is capped at
the pawn's own apparent scale, which is the fix that stopped a spear drawing
three and a half times a warrior's height at maximum zoom.

Embedded projectiles never fade. They carry no age at all; removal is
eviction-only from a fixed ring buffer of `Capacity = 256` at
`EmbeddedProjectileSystem.cs:50`. That capacity is global, not per pawn.

The quad-table double-count that an older note flagged was in the design
document, and the correction is already written into the code at
`SubmissionCount.cs:562-593`. It is not a live defect.

**A real gap the code comment does not cover:** the enforced budget assertion in
`RenderBudgetEstimateTests.cs:77-121` sums per-pawn quads, backdrop quads, and
in-flight projectile quads, and never adds the embedded pool's contribution at
all. The integrator confirmed by grep that `EmbeddedProjectileQuadsPerProjectile`
appears nowhere in that test file. The embedded pool's 256 × 2 = 512 quads are
claimed in a comment and asserted by nothing.

There is no hit stop, no screen shake, and no freeze frame anywhere in
`src/Hukbo.Client`. An exhaustive grep returned no matches.

## 3. Admitted debt, from the repository's own documents

- `ConservativePawnCull` is neither wired nor deleted, and AA-24 passed against
  it anyway.
- A collapsed contact bundle — more than five pending contacts for one attacker
  — silently drops the weapon cue, the death cue, blood, clash, and the defender
  reaction. The backlog calls this a latent path rather than an observed loss.
- `AcknowledgeDraw` releases its latch for contact frames that were cull-rejected
  and never actually drawn.
- Six `AttackPose` fields are never read, and `RecordPawnQuads` passes a null
  gait pose where the real draw path passes a real one.
- The lethal blow package's own plan required one isolated green gate proving
  that change alone left the gate green. It was never obtained: the attempt
  failed at the build stage on unrelated concurrent work, and the later green run
  bundled this change with cohort lateral spread and other uncommitted work.
- No total-screen quad ceiling was ever decided for the effect system, and gore
  now defaults to `Full`.
- AA-22's two measured contributors — arms close to sub-pixel at fit zoom, and
  trails multiplying with density — are, in the backlog's own words, "still real
  and still undressed".

## 4. What the public record says about legibility in mass battle

Sources are cited inline. Claims are marked by strength where the underlying
pass distinguished consensus from a single voice.

**The first thing lost as density rises is causal attribution, not motion.**
Players report seeing that combat is happening while being unable to say which
unit killed which. In a Total War: Warhammer II thread the complaint is put
plainly — "sometimes I don't even know which unit killed which because the
animations are so off" — alongside "no recoil from hits most of the time"
(https://steamcommunity.com/app/594570/discussions/0/1696045708641638708/). The
same complaint appears in Battle Brothers at a far lower unit count, which
suggests density is not the only variable
(https://steamcommunity.com/app/365360/discussions/0/1698294337762557431/).

**Missing hit reaction reads as lifeless more strongly than a missing attack
animation.** In the same thread, a poster who has already criticized the attack
animations singles out reaction as the worst part: "the most jarring thing is
when there's no reaction to attacks whatsoever".

**Readability survives distance in a fixed order: silhouette, then value, then
color and saturation, then detail** — and even good silhouettes stop reading
when placed next to each other, which is precisely the density case
(https://medium.com/@xavierck/character-readability-in-team-fortress-2-and-overwatch-68c41d454465).
No source supports the intuition that color is the primary distance cue.

**A body that simply drops does not read as a kill.** Total War: Attila players
coined "spontaneous heart failure" for it, and the contrast case they name is a
death with an attributable signature
(https://steamcommunity.com/app/325610/discussions/0/617328415062918251). The
rule that follows, stated across several developer sources, is that the hit
signal and the kill signal must never be the same effect. Hukbo already obeys
this.

**Projectile visibility fails on two axes: zoom and terrain contrast.** A Total
War arrow-trail mod thread contains both failures at once — players unable to see
arrows "at all unless totally zoomed in", and trails that become "almost
impossible to see on desert and ice maps" because a single trail color cannot
survive every background
(https://steamcommunity.com/sharedfiles/filedetails/comments/1164592788). The
documented fix from shmup design, where projectile density is the whole game, is
a double outline of one bright and one dark color, so that some part of the
projectile contrasts against any background
(https://www.slynyrd.com/blog/2020/12/14/pixelblog-31-shmup-sprite-design).

**Motion effects have to ship as a graduated intensity control, not a toggle.**
Xbox Accessibility Guideline 117 cites a shipped example letting players set the
intensity of screen shake and related effects individually on a nought-to-one-
hundred-per-cent scale
(https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/117).
A Mechabellum thread shows why it matters: six separate posters asked for the
control, one called the shake a purchase blocker, and one who is not motion-sick
still wanted it reduced
(https://steamcommunity.com/app/669330/discussions/0/6274121610020084879).
Partial toggles that leave residual motion are a documented failure.

**Effect density scales with the number of concurrent actors, and crossing the
line is expensive.** The Overwatch complaint is that an entire team's effects
become "a multicolored wall" through which nothing can be seen
(https://us.forums.blizzard.com/en/overwatch/t/can-we-please-tone-down-the-particle-effects/624512).

**Every game in the survey that solved legibility solved it with a layer that is
not animation** — a combat log, class icons, NATO symbols, colored movement
paths. Hukbo already has the two-hundred-event feed and the agent inspector.

**One question the public record cannot answer:** no source gives an on-screen
pixel height at which leg motion stops being worth drawing. The blog most likely
to contain it was fetched and does not. Any threshold we use has to be our own
measurement.

## 5. What the public record says about technique

**Hit stop was never a global pause in the games that invented it.** Capcom's
beat-'em-ups freeze only the attacker and the attackee; every other object keeps
updating
(https://shane-sicienski.com/blog/blog-post-title-one-55pmn). Smash states the
same rule: hitlag "only affects the object that deals the damage; all other game
elements are uninterrupted" (https://www.ssbwiki.com/Hitlag). Measured
durations from shipped arcade titles cluster between 67 ms and 183 ms, and every
Smash game caps the effect — twenty frames in Melee, thirty from Brawl onward.

Three rules come with it. The freeze must not be a still frame: shipped
implementations vibrate the frozen actor. The duration must be capped rather
than scaled without bound. And a global time-scale freeze reads to players as
the game lagging rather than as an effect — Luftrausers is the cited
counterexample.

Hukbo's `LethalHoldSeconds = 0.34f` is already a per-pawn hit stop of exactly
this kind, sitting inside the published cap, applied only to killing blows.

**Recovery dominates real attack timing.** Published frame data for a shipped
fighter puts windup between five and thirty per cent of a move, contact between
six and twelve, and recovery between sixty-four and eighty-four
(https://ultimateframedata.com/mario). The intuition that windup carries the
anticipation budget is wrong.

**Do not interpolate linearly out of recovery into idle.** Holding the extreme
pose and then popping is what makes an attack readable; a smooth ramp makes it
"impossible for the player to know when they can act"
(https://www.rivalslib.com/workshop_guide/art/anticipation_action_recovery.html).

**Every chain starting and arriving on one shared normalized timer is the named
amateur tell.** Overlap wants per-chain phase offsets
(https://www.gameanim.com/2019/05/15/the-12-principles-of-animation-in-video-games/).

**Anticipation and overshoot have published constants.** The Penner back family
uses `c1 = 1.70158`; solving the published formula gives roughly ten per cent
overshoot at about fifty-eight per cent through an ease-out, and the mirrored
ten per cent back-up at about forty-two per cent through an ease-in
(https://github.com/ai/easings.net/blob/master/src/easings/easingsFunctions.ts).

**Squash and stretch has a working band of twenty-five to fifty per cent**,
beyond which it reads as ridiculous, and it must be anchored so the body stays
planted rather than sinking or floating
(https://www.joshwcomeau.com/animation/squash-and-stretch/). For rigs without
bone scaling — which includes line-segment limbs — the documented substitute is
extending limb length rather than scaling
(https://www.gameanim.com/2019/05/15/the-12-principles-of-animation-in-video-games/).

**Trail cost is overdraw, not geometry.** A pixel drawn ten times through
overlapping translucent layers costs ten times, and the published levers are
capping ribbon length, capping generated segment count, and culling
(https://morevfxacademy.com/complete-guide-to-niagara-vfx-optimization-in-unreal-engine/).
One trail component per actor does not batch. No source publishes a hard cap on
concurrent trails. Ghosts are the worst-scaling option, because each is a full
duplicate draw.

**Screen shake has a full published constant set**, built on a trauma model with
a squared response because a linear one "doesn't feel punchy", Perlin noise
rather than random so the camera stays continuous, and — the part that matters
for a deterministic sim — the shaken transform is stashed and restored so that
game logic never observes it
(https://bevy.org/examples/camera/2d-screen-shake/).

**Distance-driven gait is a first-class published technique, and its named
failure is at zero speed** — stride scale collapses toward zero, feet gather
under the body, and distance-remaining matching never resolves because the actor
never fully stops. The published guard is a low-speed threshold that gates the
warping entirely
(https://dev.epicgames.com/documentation/unreal-engine/pose-warping-in-unreal-engine).
Hukbo's `CrawlThresholdRawPerTick` is already exactly this guard.

**On animation level of detail, nobody publishes a pixel cut point.** The only
measured statement found is that above roughly one hundred and twenty-eight to
two hundred and fifty-five actors, throttled animation becomes noticeable
(https://www.coconutlizard.co.uk/blog/animation-budget-allocator/). Hukbo runs
at up to a thousand.

## 6. Anti-patterns to avoid, with the source that names each

- A global time-scale freeze for hit stop. Reads as a hitch, not an effect.
- A hit stop that is a true still frame. Shipped implementations add motion.
- An uncapped hit stop scaled from damage.
- Linear interpolation out of recovery into idle.
- Every limb chain sharing one normalized timer.
- Stride or play-rate scaling with no low-speed gate.
- Deformation past roughly fifty per cent on a non-cartoon pawn.
- One trail component per actor, or long overlapping translucent ribbons.
- Skinned ghosts on an ordinary swing.
- A linear trauma-to-shake mapping, or white random noise instead of continuous
  noise.
- Letting a shaken transform reach anything but rendering.
- Trusting an actor-count level-of-detail scheme without measuring where it
  visibly breaks.

## 7. Questions this research leaves open for the design

1. Hukbo has hit stop for killing blows only. Should ordinary contacts get a
   shorter hold, given that the published range starts around 67 ms and that
   the repository's own reason for the lethal hold was legibility?
2. Screen shake does not exist. The public record says it is both the strongest
   impact channel and the most common accessibility complaint, and that it must
   ship with a graduated intensity control. Hukbo already has a motion-intensity
   setting to hang it on. Does a spectator-only game want it at all?
3. Projectiles have a size floor but no contrast treatment. Does the double
   outline apply to a top-down battlefield whose ground is cogon olive-gold?
4. `ConservativePawnCull` must be either wired or deleted. Leaving a tested,
   uncalled type in the tree is the state that let a smoke row pass against
   nothing.
5. The embedded pool's quad claim needs an assertion, not a comment.
6. What is the on-screen pixel height below which leg motion stops earning its
   quads? The public record cannot answer it; only a measurement here can.
