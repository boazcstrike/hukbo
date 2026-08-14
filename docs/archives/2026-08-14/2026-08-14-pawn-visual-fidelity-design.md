# Pawn visual fidelity — design

**Archived: reference only.** The design this document describes was built on 2026-08-14, on branch `pawn-visual-fidelity`, with one item — the `Medium`-tier swing trail removal — dropped by user decision and screen shake declined outright. **At the time of archiving that branch had not been merged into `main`.** Never execute it, never treat it as a live design, and never cite it as the reason to make a change.

Date: 2026-08-14
Status: design only. **This document authorizes nothing.** Under section 6 of
`CLAUDE.md` a design document does not authorize implementation; the plan
document beside it, `2026-08-14-pawn-visual-fidelity.md`, carries the ordered
tasks and is itself unauthorized until a person says otherwise.

Scope: four subsystems that have all shipped and all closed their smoke rows —
gait animation, attack animation V2, projectile props and embedded projectiles,
and lethal blow legibility. This document decides what remains worth building in
them, what does not, and what only the user can decide.

Out of scope throughout, because another session owns the files: death collapse
and the prone corpse, the UI chrome nine-slice, armor accent and trample marks,
last-stand engagement, cohort lateral spread, agent inspector row wrapping, and
every part of Sandata. No item below touches `DeathCollapseSystem.cs` or
`CollapsePose.cs`.

The consolidated research note
[`../research/2026-08-14-pawn-visual-fidelity-research.md`](../research/2026-08-14-pawn-visual-fidelity-research.md)
is the primary evidence for this design. Every code fact in its section 2 was
re-verified against disk by its own integrator, and where anything below
disagrees with it, it wins and the disagreement is recorded in section 2.4 of
this document.

## 1. Goal, and the retirement condition

The goal is not "more animation". All four subsystems already draw what they
were designed to draw, and every smoke row belonging to them closed `PASS` on or
before 2026-08-13. The goal is to retire the engineering debt those four
packages admitted to in their own documents, so that each subsystem can be
closed rather than carried.

A subsystem is retired when a reader can say the following about it without
opening source.

**Gait animation is retired when** there is a recorded measurement of the
on-screen pixel height of a drawn leg at each of the three detail tiers, and a
person has said, at an interactive desktop, at which of those heights the leg
motion still reads as motion rather than as flicker. The public record cannot
answer that question — the research note's section 4 says so outright, and the
blog most likely to contain a number was fetched and does not have one — so the
threshold has to be our own measurement or it does not exist. Until then, any
change to `PawnDetailTier`'s leg behaviour would be tuning against a number
nobody has.

**Attack animation V2 is retired when** the four items its backlog left open with
a decision attached are each decided on the record and either built or parked
with a reason: the unwired `ConservativePawnCull`, the collapsed contact bundle,
`AcknowledgeDraw` releasing latches for frames that were never drawn, and the six
`AttackPose` fields nothing reads. "Decided" means written down here; three of
the four are decided in section 5 and one is deferred in section 3 with its
precondition named.

**Projectile props and embedded projectiles are retired when** the quad claim in
the embedded pool's own comment is asserted by a test rather than by prose, and
when the three colours a projectile is drawn in have been measured against every
shipped theme's ground shade using the contrast machinery this repository
already owns. Section 5.1 records that measurement; it fails, and that failure
is the strongest single finding in this document.

**Lethal blow legibility is retired when** the isolated green
`./scripts/verify.ps1` receipt its own plan required has actually been obtained
against that change alone. It never was: the attempt failed at the build stage on
unrelated concurrent work, and the later green run bundled the change with cohort
lateral spread and other uncommitted work. That debt is recorded in the archived
plan itself and in `docs/plans/README.md`; nothing else about the subsystem is
open, because all five of its channels are wired and all five have live callers.

## 2. Known facts

Everything in this section is cited to the research note and, where a task
depends on it, to a file and line that was re-read on disk at commit `8ee5a51`
while this design was written.

### 2.1 Gait

Stride phase is driven by distance travelled, not by elapsed time.
`GaitAnimationSystem.Advance` advances phase by `distance / StrideCycleDistanceRaw`,
where `StrideCycleDistanceRaw = 6000f` at
`src/Hukbo.Client/Presentation/GaitAnimationSystem.cs:75` and the advance is at
line 233. There is no clock in the file.

The "one stride cycle per three hundred seconds" defect is **already fixed**.
Displacement below `CrawlThresholdRawPerTick = 60f`, at
`src/Hukbo.Client/Rendering/GaitGeometry.cs:57` and gated at line 113, resolves
to a neutral stance that eases toward rest instead of advancing phase, and a
test pins it. No task in the plan may re-fix it.

`WalkStrideRatio = 0.32f` and `RunStrideRatio = 0.60f` at `GaitGeometry.cs:63`
and `:70`; `WalkFootLiftRatio = 0.15f` and `RunFootLiftRatio = 0.38f` at `:73`
and `:80`. All four are marked `PROVISIONAL` in their own doc comments, which is
what section 7 of `CLAUDE.md` requires of a gameplay tuning value.

`LegLengthUnits = 7.5f` at `src/Hukbo.Client/Rendering/PawnGeometry.cs:482`, and
the drawn leg length in pixels is `ToSize(LegLengthUnits * apparentScale)` at
line 1077. `LegWidthUnits = 1.6f` at line 470.

The detail tiers are `Low`, `Medium`, and `High`, selected at `0.95` and `1.80`
apparent scale. Those thresholds are duplicated **on purpose** at
`PawnGeometry.cs:235-236` and at
`src/Hukbo.Client/Rendering/DetailTierGate.cs:23-24`, and the second copy's own
remarks explain why they are deliberately not shared. Consolidating them is
forbidden.

Legs and feet cost between zero and four quads per pawn, counted at
`src/Hukbo.Client/Rendering/SubmissionCount.cs:105-106`. At `Low` they cost
nothing, because `PawnGeometry` returns empty bounds for them at that tier.

### 2.2 Attack animation

There is no windup phase and its absence is deliberate: Core's attack event is
the contact authority, so the animation has nothing to anticipate with. Only
`RecoveryProgress` is a normalized zero-to-one value, at
`src/Hukbo.Client/Presentation/AttackAnimation.cs:87-102`.

`ConservativePawnCull` has **zero production callers**. The only references
under `src/` are the class's own doc comments and three comments in
`PawnGeometry.cs`, at lines 925, 2243, and 2348 — re-confirmed by grep at
`8ee5a51`. Its radius is `RadiusUnitsPerApparentScale = 38.8f` plus
`RadiusConstantPixels = 5f`, at
`src/Hukbo.Client/Rendering/ConservativePawnCull.cs:136` and `:153`.

Arms are gated off at `PawnDetailTier.Low` and at
`AttackAnimationPhase.Readiness`, at `PawnGeometry.cs:1380-1394`, and their
half-width is `MathF.Max(ArmMinimumHalfWidthPixels, ArmHalfWidthUnits * scale)`
at line 1398, where `ArmHalfWidthUnits = 0.8f` and
`ArmMinimumHalfWidthPixels = 0.6f` at lines 286 and 289.

Swing trails are gated off at `PawnDetailTier.Low` and at zero trail strength, at
`PawnGeometry.cs:1848-1851`. A drawn trail is exactly `SwingTrailSegments = 6`
stroked quads, counted at `SubmissionCount.cs:36` and `:338-339` and drawn at
`src/Hukbo.Client/Rendering/PawnRenderer.cs:427` and `:1372-1401`. Arms cost up
to four quads, counted at `SubmissionCount.cs:122` and `:348`.

`MaximumPendingContactsPerAttacker = 5` at
`src/Hukbo.Client/Presentation/AttackContactDispatcher.cs:16`; the overwrite is
`ReplacePending` at line 277, reached from line 237. The diagnostic identifier is
`LogEvents.RenderAttackContactCollapsed`, whose value is
`"render.attackContactCollapsed"`, at `src/Hukbo.Diagnostics/LogEvents.cs:132-133`.
It now logs at `dbg`, fixed on 2026-08-14 and recorded in section 8 of the attack
animation V2 backlog.

`AttackFrameCoordinator.AcknowledgeDraw` is at
`src/Hukbo.Client/Presentation/AttackFrameCoordinator.cs:114`, delegates to
`AttackAnimationSystem.AcknowledgeDraw` at
`src/Hukbo.Client/Presentation/AttackAnimationSystem.cs:151`, is exposed as
`PresentationCoordinator.AcknowledgeAttackDraw` at
`src/Hukbo.Client/Presentation/PresentationCoordinator.cs:356`, and is called
unconditionally one line after `DrawPawns` at
`src/Hukbo.Client/ArenaGame.Rendering.cs:788-789`.

`AttackPose` is declared at
`src/Hukbo.Client/Rendering/AttackPoseResolver.cs:11-27`. Six of its members —
`Forward`, `Right`, `SupportHand`, `ShieldHand`, `TrailStart`, `TrailEnd` — have
no reader anywhere under `src/` outside the resolver that fills them. A grep for
`.Right` returns fifty-nine hits, every one of them `Rectangle.Right` on a bounds
value rather than this member; the other five return zero.

`RecordPawnQuads` passes `gaitPose: null` at
`src/Hukbo.Client/ArenaGame.Rendering.cs:442-543`, under a comment claiming the
probe pass mirrors the draw path element for element, while `DrawPawns` at line
1072 passes the real pose.

### 2.3 Projectiles and lethal blow

Lethal blow legibility is genuinely wired. Five channels fire only on a killing
blow, all fanning out from a single `MarkLethal` at
`src/Hukbo.Client/Presentation/AttackContactDispatcher.cs:303`, and all have live
callers. Their durations are deliberately ordered — the defender reaction lasts
0.50 s, the animation hold `LethalHoldSeconds = 0.34f` at
`src/Hukbo.Client/Presentation/AttackAnimation.cs:60`, and the hit pulse 0.30 s —
and `src/Hukbo.Client/Presentation/DefenderReactionSystem.cs:32-40` documents that
ordering as a contract. There is no orphan channel here.

Projectiles have a minimum-size floor, `MinimumDimension = 1f` at
`src/Hukbo.Client/Rendering/ProjectileGeometry.cs:110`, applied through `Scaled`
at line 474 and pinned by a test. They have **no contrast treatment**: the three
colours are private fields on the renderer at
`src/Hukbo.Client/ArenaGame.Rendering.cs:43`, `:51`, and `:58`, and are selected
by element kind at line 982.

Embedded projectiles never fade; removal is eviction-only from a fixed ring
buffer of `Capacity = 256` at
`src/Hukbo.Client/Presentation/EmbeddedProjectileSystem.cs:50`, and that capacity
is global rather than per pawn. Each costs
`RenderBudgetEstimate.EmbeddedProjectileQuadsPerProjectile = 2` at
`src/Hukbo.Client/Rendering/SubmissionCount.cs:644`.

The enforced budget assertions in
`tests/Hukbo.Client.Tests/RenderBudgetEstimateTests.cs` sum per-pawn quads,
backdrop quads, and in-flight projectile quads, and **never add the embedded
pool's contribution at all**. `EmbeddedProjectileQuadsPerProjectile` appears in
`tests/Hukbo.Client.Tests/ProjectileGeometryTests.cs:408` and `:411` and nowhere
in the budget test file. The pool's 512 quads are claimed in a comment and
asserted by nothing.

The ceilings are `ArenaBatchQuadsAt200UnitsEstimate = 12_000` and
`ArenaBatchQuadsAt500UnitsEstimate = 20_000`, at `SubmissionCount.cs:626` and
`:629`.

There is no hit stop, no screen shake, and no freeze frame anywhere in
`src/Hukbo.Client` beyond the lethal hold. An exhaustive grep returned no
matches.

`GoreIntensity` defaults to `Full` — `DefaultGoreIntensity` at
`src/Hukbo.Client/Settings/ClientSettingsStore.cs:80`.

### 2.4 Where this design's own reading disagrees with an inherited note

Two claims that reached this design through the attack animation V2 backlog, and
that the research note repeats, do not survive a reading of the file.

**Arms are not gated by zoom and are not sub-pixel.** The backlog's section 1
says arms "are gated off below roughly 1.35 zoom and drawn as strokes 1.6 units
thick", and the research note carries that forward as "arms close to sub-pixel at
fit zoom". On disk the gate is `detailTier == PawnDetailTier.Low`
(`PawnGeometry.cs:1380`), not a zoom number; `1.35f` is `ZoomScale`, the
zoom-to-apparent-scale coefficient at `PawnGeometry.cs:234`, which is a different
constant that happens to share the value. And the stroke has a floor: half-width
is `MathF.Max(0.6f, 0.8f * apparentScale)`, so a full arm stroke is never
narrower than 1.2 pixels, and at the `Medium` boundary of 0.95 apparent scale it
is 1.52 pixels. An arm at fit zoom is thin. It is not sub-pixel, and the floor
that prevents it is already in the code. This changes the verdict on AA-22's
first contributor; see section 4.

**The collapsed-contact log identifier is not what the backlog spells.** Section
3 of the backlog looks for a `render.attack.contact.collapsed` line. The constant
is `LogEvents.RenderAttackContactCollapsed` and its value is
`render.attackContactCollapsed` (`src/Hukbo.Diagnostics/LogEvents.cs:133`).
Anyone grepping a log for the backlog's spelling finds nothing and could conclude
the path never fired when they simply searched for a string that does not exist.
The backlog's conclusion happens to be right for a different reason — two full
500-agent battles produced no such line at any level — but the grep as written
proves nothing.

## 3. What is not being built, and why

**No sprite-frame animation, and nothing that leads toward it.** Everything in
this document is procedural. `docs/plans/TODO.md` parks the sprite-frame pipeline
by user decision of 2026-08-07, and `SIMULATION-GAME-STANDARDS.md` line 18 chose
dots as the v0.1 visual identity specifically to avoid an asset and animation
pipeline before the simulation is fun. A design that reaches for authored frames
is revisiting that decision, and that is its own document.

**No change to `Hukbo.Core`.** Every item here is presentation. No task may edit
a file under `src/Hukbo.Core` or `src/Hukbo.Shared.Core`, no task moves the state
hash or the event hash, and no task lets the client decide targeting, damage,
retreat, or victory. If an implementer finds that a task as written would move a
hash, the correct action is to stop and say so, not to re-pin a golden value.

**No windup phase.** `AttackAnimation.cs:6-9` records that Core's attack event is
the contact authority and the animation has nothing to anticipate with. The
published frame data in the research note's section 5 says recovery dominates
anyway — between sixty-four and eighty-four per cent of a shipped fighter's move
against five to thirty for windup — so the intuition that windup carries the
anticipation budget is wrong on its own terms. Adding one would mean the client
predicting a contact the simulation has not yet declared, which is the client
deciding an outcome.

**No consolidation of the duplicated tier thresholds.** `DetailTierGate`'s own
remarks argue for the duplication. Section 2.1 records the two locations so that
nobody "fixes" them.

**No fix for the collapsed contact bundle's behaviour, yet.** Section 4 defers it
with a precondition: the loss has never been observed in two full 500-agent
battles including three pause-and-resume cycles and a round transition, so it is
a latent path. Changing what a never-fired path does, before anything has fired
it, is tuning without a measurement. What this package builds instead is the
characterization test that says exactly which cues a collapse costs, so the next
person who sees the line knows what they lost.

**No trail-count cap ordered by distance from the camera centre.** It was
considered and rejected. It needs a per-frame sort of the active attack
population, which allocates and which makes the drawn set depend on camera
position in a way that is awkward to test without a graphics device. The tier
gate in section 5.6 achieves the same overdraw reduction with a pure per-pawn
predicate the existing `DetailTierGate` already provides.

**No screen shake, no ordinary-hit hit stop, and no projectile double outline —
pending the user.** All three are in section 4 as `NEEDS USER DECISION`, with a
recommendation on the record for each.

## 4. Per-item decision table

| Item | Verdict | Reason |
| --- | --- | --- |
| Projectile contrast against shipped ground shades | **IN** | Measured failure, not a hypothesis. All three projectile colours fall inside `ContrastEnvelope.MinimumGroundDistance = 60f` of at least one shipped theme's ground shade — shaft 28.2 from Field Manual, head 47.8 from Field Manual, fletch 29.9 from Broadcast. The repository already owns the metric and already applies it to weapon tints and shield tones; projectiles were simply never put through it. Section 5.1 |
| The embedded pool's quads enter the enforced budget assertion | **IN** | 512 quads claimed in a comment and asserted by nothing, against ceilings of 12,000 and 20,000 that a test does enforce for every other term. Section 5.2 |
| A whole-screen effects-quad ceiling | **IN**, narrowed to an assertion | No ceiling was ever decided and `GoreIntensity` now defaults to `Full`. Every effect population is already a fixed-capacity pool, so the total is already bounded — what is missing is the arithmetic that says by what. Building a new runtime ceiling would add a governor to something already governed; asserting the sum finds out whether the existing bound is the one we want. Section 5.3 |
| `AcknowledgeDraw` releasing latches for undrawn frames | **IN** | The guaranteed-contact-draw property the whole latch mechanism exists to provide does not hold for a culled or dead attacker. This is the one item on the list that silently loses the frame a spectator most needs to see. Section 5.4 |
| `RecordPawnQuads` passing a null gait pose | **IN** | The probe pass under-counts by up to four quads per pawn — up to 2,000 at 500 units — against ceilings measured in thousands, while its own comment claims it mirrors the draw path element for element. A budget probe that under-reports is worse than none. Section 5.5 |
| Swing trails multiplying at density (AA-22, second contributor) | **IN** | Six stroked quads per attacking pawn at `Medium`, which is the tier the default camera fit now resolves, is exactly the overlapping-translucent-ribbon case the published optimisation guidance names as an overdraw problem. Gating to `High` with a lethal exemption is a pure predicate change and a net quad saving. Section 5.6 |
| Six unread `AttackPose` fields | **IN**, as deletion | Confirmed on disk: zero readers under `src/` for all six. They cost a wider record struct on every posed pawn and they invite a future reader to believe the draw path consumes them. The two that could plausibly be consumed are re-derived by design — the support hand from the weapon line, the shield guard from a fixed offset. Section 5.7 |
| Leg-motion pixel-height threshold | **IN**, as measurement plus one `PENDING` smoke row | The public record has no number and the research note says so explicitly. A measurement can be computed without a GPU; the judgement of whether that height reads cannot be, and no agent may make it. Section 5.8 |
| `ConservativePawnCull` wired or deleted | **IN**, as neither — re-documented and handed on | Its own doc comment says the bound "is a genuine superset, never a replacement" and that "nothing here may ever be used as the only cull". Wiring it therefore draws exactly the same pawns it draws today and cannot close a clipping question; its purpose was a pre-appearance-resolution filter whose measured saving is zero at minimum zoom and zero at default fit. That is a 1,000-unit performance concern and `docs/plans/2026-08-14-thousand-unit-performance-design.md` already owns that territory. Deleting it would destroy the only guard on three mirrored `PawnGeometry` constants. Section 5.9 |
| Collapsed contact bundle dropping every cue | **IN** for a characterization test; **DEFERRED** for the behavioural fix | Never observed firing. The test makes the cost explicit and is the precondition for any later fix; the fix itself waits on an observation. Section 5.10, and the deferral goes to `docs/plans/TODO.md` |
| The lethal blow package's isolated gate receipt | **IN** | Its own plan required it, it was never obtained, and the debt is recorded in two places. It is one command and a paste. Section 5.11 |
| AA-22's first contributor, sub-pixel arms at fit zoom | **DEFERRED** | The premise is false on disk. Arms are gated by detail tier, not by zoom, and a 1.2-pixel floor already prevents a sub-pixel stroke; at the `Medium` boundary the stroke is 1.52 pixels. Whether a 1.2-to-1.5-pixel arm reads at density is a question for a person's eyes, and the smoke row that would have asked it, AA-22, is closed. Reopening it needs a new row and a reason, and this package has no measurement that justifies one. Recorded in `docs/plans/TODO.md` |
| Trail cap ordered by camera-centre distance | **DEFERRED**, superseded | Section 3 gives the reason; the tier gate does the same job without a per-frame sort |
| Ordinary-hit hit stop | **NEEDS USER DECISION** — recommendation: no | The published range starts around 67 ms and the technique is sound, but the repository's lethal hold earns its legibility precisely by being the only hold. The research note's own strongest cross-source rule is that the hit signal and the kill signal must never be the same effect; a hold on every contact spends the distinction that `LethalHoldSeconds` was created to buy. Section 8 |
| Screen shake | **NEEDS USER DECISION** — recommendation: no | Three concrete objections in section 8, one of which is mechanical rather than aesthetic: Hukbo's camera is read by pointer picking, so a shaken camera transform would move click targets unless it is stashed and restored, and the published guidance is explicit that the shaken transform must reach nothing but rendering |
| Projectile double outline | **NEEDS USER DECISION**, and only as a fallback | Section 5.1 recommends a colour retune that clears all six ground shades at zero quad cost. An outline adds a second stroked element per projectile element, which is a quad delta against ceilings this package is already tightening. Section 8 |
| What to do if section 5.2 or 5.3 breaches a ceiling | **NEEDS USER DECISION** | Raising a ceiling and shrinking a pool are both real answers and neither is an agent's to pick. Section 8 |

## 5. The design of each `IN` item

### 5.1 Projectile contrast against every shipped ground shade

**Mechanism.** This repository already owns the metric. `ContrastEnvelope`
(`src/Hukbo.Client/Presentation/ContrastEnvelope.cs`) is a plain Euclidean
channel distance over red, green, and blue, with three named `PROVISIONAL`
bounds: `MinimumGroundDistance = 60f` at line 25, `MinimumClothingDistance = 60f`,
and `MinimumFactionDyeDistance = 80f`. Weapon tints, shield tones, and garment
dyes are all validated against it —
`tests/Hukbo.Client.Tests/WeaponVisualCatalogTests.cs:31-46` holds the six
shipped themes' worst-case ground shades, lerped to
`PlainsBackdropGeometry.MaximumBackdropInterpolation` of 0.22, and asserts every
weapon tint clears the bound against all six. Projectiles were never put through
the same gate.

**The measurement.** Computed at commit `8ee5a51` from the three projectile
colours at `ArenaGame.Rendering.cs:43,51,58` against the six ground shades at
`WeaponVisualCatalogTests.cs:31-36`. Distances are Euclidean over the three
channels, the same metric `ContrastEnvelope.IsWithinEnvelope` applies.

| Projectile element | RGB | Command | Field Manual | Signal | Broadcast | High Contrast | Datu Court | Minimum |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Shaft | 214, 178, 122 | 219.2 | **28.2** | 244.2 | 107.4 | 210.2 | 150.0 | **28.2 — fails** |
| Head | 168, 172, 178 | 203.3 | **47.8** | 233.4 | 76.8 | 202.2 | 151.8 | **47.8 — fails** |
| Fletch | 236, 228, 208 | 294.8 | 83.2 | 323.9 | **29.9** | 291.7 | 236.4 | **29.9 — fails** |

All three fail. An arrow shaft on the Field Manual theme is 28.2 channel units
from the ground it flies over, against a bound of 60 that every weapon tint in
the game already clears; a fletch on Broadcast is 29.9. This is the arrow-
invisibility failure the research note's section 4 found in the public record —
"almost impossible to see on desert and ice maps", because a single trail colour
cannot survive every background — reproduced here as a number rather than as a
complaint.

**The change.** Retune the three colours until each clears
`ContrastEnvelope.MinimumGroundDistance` against all six shades, and add a test
that pins it the same way `WeaponVisualCatalogTests` pins the weapon tints. The
retune has room: the failures are against the two pale themes, so darkening the
shaft and the fletch and cooling the head moves all three away from Field Manual
and Broadcast without moving them toward Command, Signal, High Contrast, or Datu
Court, which are all more than 76 units away already. The implementer picks the
values by search against the metric rather than by eye, and records the resulting
table in the same shape as the one above.

**Constants and units.** Colour channel distance, dimensionless, over `[0, 255]`
per channel. The bound is `60f`, unchanged, and stays `PROVISIONAL` — section 7
of `CLAUDE.md` requires a gameplay tuning value to be labelled provisional in
code comments and never presented as a measurement, and a projectile's colour is
a legibility choice, not a claim about what a pre-colonial arrow looked like. The
existing doc comments at `ArenaGame.Rendering.cs:47-58` already say "Provisional
gameplay"; the retuned values keep that wording.

**Quad delta: zero.** This is a colour change. The fallback treatment — the
double outline of one bright and one dark colour the research note found in shmup
design — is *not* built, because it would add a stroked element per projectile
element and this package is tightening the very budget it would spend. It is in
section 8 as the fallback if the retune cannot clear all six.

**Tier behaviour: unchanged.** In-flight projectiles are not detail-gated; the
embedded prop is gated at `Medium` at `ArenaGame.Rendering.cs:944`. Neither
gate moves.

**Historical accuracy.** No evidence tier changes. The colours are legibility
tuning, marked provisional, and no claim is made that a shaft was this shade.

### 5.2 The embedded pool's quads enter the enforced budget assertion

**Mechanism.** `RenderBudgetEstimateTests` already sums per-pawn quads, worst-case
backdrop quads, and `Scenario.MaximumProjectilesInFlight *
ProjectileQuadsPerProjectile`, and asserts the total against 12,000 and 20,000.
Add one term: `EmbeddedProjectileSystem.Capacity *
RenderBudgetEstimate.EmbeddedProjectileQuadsPerProjectile`, read from the
production constants rather than written as `512`, so the test cannot drift from
the values the code enforces.

**Constants.** `Capacity = 256` projectiles, global not per pawn;
`EmbeddedProjectileQuadsPerProjectile = 2` quads. The term is 512 quads,
independent of unit count, because the pool is global.

**Quad delta: zero drawn.** The pool already draws what it draws. What changes is
that the budget arithmetic stops omitting it.

**Stop condition, and it is a real one.** If adding 512 pushes either total past
its ceiling, the implementer stops and reports the arithmetic. Raising a ceiling
and shrinking the pool are both legitimate answers and neither is an agent's to
choose — see section 8. Weakening the assertion to get green is forbidden by
section 5 of `CLAUDE.md`.

**Tier behaviour.** The embedded prop is `Medium`-gated in the draw path, so the
512 is a bound rather than a per-frame cost at fit zoom. The assertion uses the
bound, which is what a budget term is for; the existing in-flight term is written
the same way.

### 5.3 A whole-screen effects-quad ceiling, expressed as an assertion

**Mechanism.** No ceiling was ever decided for the effect system and gore now
defaults to `Full`. But every effect population in the client is already a
fixed-capacity pool — blood, hit effects, clash effects, dust, trample marks,
embedded projectiles — so the whole-screen total is already bounded. What is
missing is the arithmetic that says by what, and a place where a future feature
adding a pool meets that number.

The change is one test that sums every effect pool's capacity times its
quads-per-item, adds the per-pawn and backdrop worst cases the existing tests
already build, and asserts the result against a named constant. Where a pool's
quads-per-item is not already a `RenderBudgetEstimate` constant, one is added
beside the existing `ProjectileQuadsPerProjectile` and
`EmbeddedProjectileQuadsPerProjectile`, derived from the renderer that draws it
rather than chosen.

**Constants.** The new ceiling constant is not invented here. The implementer
computes the sum first and reports it; the constant is then set from the measured
worst case with headroom stated, in the same style as the existing two ceilings,
and the number goes in the plan's verification record. Setting a ceiling below
the measured worst case to force a failure, or above it by an unstated margin to
avoid one, are both wrong.

**Quad delta: zero drawn.** Assertion only.

**Why not a runtime governor.** A runtime cap that starts dropping effects when a
count is exceeded would make the drawn set a function of how many effects
happened to be alive, which is a legibility regression exactly when the screen is
busiest — the case the research note's Overwatch source describes as "a
multicolored wall". The pools already bound the total. If the assertion shows the
bound is too high, the fix is a smaller pool, decided with a number in hand.

### 5.4 `AcknowledgeDraw` releases only latches whose pawn was drawn

**Mechanism.** `AttackFrameCoordinator.AcknowledgeDraw` documents itself as
releasing "every latch whose matching pose was present in the completed pawn
pass", but it checks only `AwaitingDrawAcknowledgement` and sequence equality,
never whether the pawn survived the cull. `ArenaGame` calls it unconditionally at
`ArenaGame.Rendering.cs:789`, one line after `DrawPawns`. So an attacker rejected
by the cull, or one that is dead and outside its lethal hold, has its contact
frame acknowledged and its timeline advanced without ever having been drawn.

The change gives `AcknowledgeDraw` the set of attacker entity ids the pawn pass
actually drew. `DrawPawns` already iterates the drawn pawns; it appends each
drawn attacker's entity id to a buffer owned by the coordinator and reused across
frames — a pre-sized array with a count, never a growing collection, because
section 9 of `CLAUDE.md` forbids an unbounded cache and the draw path must not
allocate per frame. `AcknowledgeDraw` then releases a latch only if its attacker
appears in that buffer.

**The bound, which is the part that makes this safe.** A latch that is only ever
released on a draw is a latch an off-screen attacker keeps forever. The design
therefore pairs the check with an expiry: a latch also releases when its
animation's age exceeds a stated bound, so the mechanism degrades to today's
behaviour rather than to a leak. The bound is `MaximumLatchFrames`, a small
whole number of frames rather than a wall-clock duration, because the client's
frame rate is not a simulation quantity and a frame count is what the latch
mechanism already counts in. The implementer sets it from the existing latch
tests' longest legitimate hold and states the value; the expiry path is
distinguishable from the drawn path in the returned count so a test can tell them
apart.

**Constants and units.** `MaximumLatchFrames`, in frames. The buffer is sized at
the same attacker capacity `AttackContactDispatcher`'s constructor already uses
at `AttackContactDispatcher.cs:35`, so it cannot be exceeded by construction.

**Quad delta: zero.** A culled pawn is still culled. What changes is that its
contact frame is drawn when it re-enters view, within the expiry bound, rather
than being consumed while invisible.

**Tier behaviour: none.** The latch is independent of detail tier.

**Spectator effect.** A warrior who lands a killing blow at the edge of the arena
panel, then pans into view, shows the contact frame the latch was created to
guarantee. Today that frame can be spent while the warrior is off screen.

### 5.5 `RecordPawnQuads` passes the real gait pose

**Mechanism.** `RecordPawnQuads` at `ArenaGame.Rendering.cs:442` passes
`gaitPose: null` while `DrawPawns` at line 1072 passes the real pose, under a
comment at lines 434-441 claiming the probe pass mirrors the draw path element
for element. With a null gait pose `PawnGeometry` returns empty leg and foot
bounds, so `PawnQuadCount` scores zero for the two entries counted at
`SubmissionCount.cs:105-106`, which cost up to four quads each pawn in the real
draw. At 500 units that is up to 2,000 quads the probe never sees, against a
ceiling of 20,000.

The change resolves the same gait pose the draw path resolves and passes it. The
probe pass runs once per frame and already resolves an appearance per agent; the
gait pose is a lookup into `GaitAnimationSystem`'s existing store, so the added
cost is a lookup per agent on a pass that is already per agent.

**Constants: none new.**

**Quad delta: zero drawn, up to +4 per pawn recorded.** That is the point. The
probe's reported number rises to what is actually submitted.

**Verification is the interesting part.** A test that compares the probe's
recorded count against the draw path's is the honest oracle only if it does not
compare the new code with itself. Asserting that `RecordPawnQuads` and
`DrawPawns` agree, when the change makes them call the same helper, proves
nothing. The test instead pins the recorded count for a pawn with a known gait
pose against `PawnQuadCount.Count` called directly on the layout built from that
pose — an independently constructed expectation — and additionally asserts that
the count is strictly greater than the same pawn's count with a null pose, which
is the property the change exists to create.

### 5.6 Swing trails gate to `High`, and a lethal trail stays at `Medium`

**Mechanism.** A drawn trail is six stroked, translucent, overlapping quads
(`SubmissionCount.cs:36`, drawn at `PawnRenderer.cs:1384-1401`). It is already
gated off at `Low` (`PawnGeometry.cs:1848`). The default camera fit now resolves
`Medium`, so at 500 units a few dozen six-segment translucent arcs are on screen
at once — the composition AA-22's observer described as chaos, and precisely the
overdraw case the published optimisation guidance names: a pixel drawn ten times
through overlapping translucent layers costs ten times, and the levers are
capping length, capping segment count, and culling.

The change raises the trail's gate from `Medium` to `High`, with one exemption:
a trail whose `AttackPose` is lethal keeps drawing at `Medium`. The exemption is
not decoration. The research note's strongest cross-source finding is that the
hit signal and the kill signal must never be the same effect, and this
repository's lethal channel already carries `LethalTrailEmphasis = 1.35f` at
`PawnGeometry.cs:309`. Gating every trail equally would delete the kill signature
along with the noise; gating ordinary trails and keeping lethal ones makes the
remaining trails mean something.

**Constants and units.** No new numeric constant. `High` is 1.80 apparent scale
and `Medium` is 0.95, both already defined in the two deliberately duplicated
places recorded in section 2.1. The gate is a tier comparison, the same predicate
`DetailTierGate.ShouldDraw` already provides.

**Quad delta: a saving, bounded and stated.** At `Medium` an attacking pawn loses
six quads unless its blow was lethal. At 500 units with, say, forty simultaneous
non-lethal attacks that is 240 quads of translucent overdraw removed per frame;
the exact figure depends on how many attacks are live and the plan's verification
records the measured `PawnQuadCount` change rather than this estimate. No pawn
gains a quad at any tier, so no ceiling can be breached by this change.

**Tier behaviour, stated plainly.** `Low`: no trail, as today. `Medium`: lethal
trails only. `High`: every trail, as today. A spectator at close zoom sees no
change at all; a spectator at fit zoom sees fewer arcs, and the arcs that remain
are kills.

**Risk, and what would falsify it.** If a person at a desktop reports that fit
zoom now reads as *less* alive rather than clearer, the gate is wrong and the
answer is a middle treatment — a shorter trail at `Medium` rather than none. That
is why section 5.8's smoke row is written to cover the trail change as well as
the leg question: one launch answers both.

### 5.7 The six unread `AttackPose` fields are dropped

**Mechanism.** `Forward`, `Right`, `SupportHand`, `ShieldHand`, `TrailStart`, and
`TrailEnd` have no reader under `src/` outside `AttackPoseResolver`, which fills
them. They are removed from the record's parameter list and from the resolver's
construction; any local arithmetic that existed only to produce them goes with
them, and any that is still needed to produce a surviving member stays.

The two that a reader might expect to survive are re-derived by design and the
backlog says so: the support hand is re-derived from the weapon line in
`PawnGeometry`, and the shield guard uses a fixed offset. Dropping them is not
losing information; it is deleting a second, unread copy of information the draw
path computes itself.

**Why this is worth a task at all.** A struct member with no reader is an
invitation to a future implementer to believe the draw path consumes it. That
belief is exactly the failure mode this repository has already been bitten by
twice — a tested type with no caller, and a smoke row that passed against a
feature that was never wired.

**Quad delta: zero.** No drawn output changes. A test must prove that: the
existing `AttackPoseResolver` and `PawnGeometry` tests must pass unchanged, and
the plan names the specific ones.

**Risk.** If removing a member turns out to change a computed value — because the
resolver's arithmetic was shared — the diff is wrong and the correct action is to
keep the intermediate as a local rather than to accept a changed pose.

### 5.8 The leg-motion pixel-height measurement

**Mechanism.** The question is: below what on-screen pixel height does leg motion
stop earning its quads? The research note says no public source answers it, and
that the blog most likely to contain a number was fetched and does not. So the
number has to be ours, and it has two halves that must not be confused.

The half a machine can answer is the geometry. `LegLengthUnits = 7.5f` and the
drawn leg length is `ToSize(LegLengthUnits * apparentScale)`
(`PawnGeometry.cs:482`, `:1077`), the stride offset is
`strideRatio * legLength` with `WalkStrideRatio = 0.32f` and
`RunStrideRatio = 0.60f`, and the foot lift is `liftRatio * legLength` with
`WalkFootLiftRatio = 0.15f` and `RunFootLiftRatio = 0.38f`. A GPU-free test can
therefore produce, for each detail tier boundary and each of the three camera
stations, the drawn leg height in pixels, the peak foot travel in pixels, and the
peak foot lift in pixels. That table is a measurement, it is deterministic, and
it belongs in `docs/development/testing.md`.

The half a machine cannot answer is whether those pixel counts read as walking.
That is a person at a desktop, and no agent may decide it. So this item ships a
measured table plus exactly one new `PENDING` smoke row, written under the
smoke checklist's rules: a person launches, watches the same battle at the three
camera stations, and says at which of the measured heights the legs still read as
legs.

**Constants: none changed.** This item deliberately tunes nothing. Tuning the
`Low`-tier leg behaviour before the row is answered would be tuning against a
number nobody has, which is the failure this item exists to prevent.

**Quad delta: zero.** Measurement only.

**What the row is worth.** If it comes back saying the legs read at every
reachable station, the gait subsystem retires with no further work. If it comes
back saying they do not read at fit zoom, the next package has a number to design
against instead of an intuition.

### 5.9 `ConservativePawnCull` is re-documented, and its wiring decision is handed on

**The finding.** The class's own remarks settle the question the backlog framed
as open. The bound "is a genuine superset, never a replacement"; a caller that
keeps today's exact test afterward "draws exactly the same set of pawns it draws
now"; and "nothing here may ever be used as the only cull"
(`ConservativePawnCull.cs:32-37`). Wiring it therefore cannot widen what is
drawn and cannot address weapon clipping at the panel edge. Its actual purpose
was to run before appearance resolution so that a battle at maximum zoom stops
resolving an appearance for every agent on the field — a per-frame cost
reduction, whose measured saving is zero at minimum zoom and zero at default fit
and which only moves a station that already renders ten times inside budget
(`ConservativePawnCull.cs:12-20`).

**The decision.** Neither wire nor delete in this package. Wiring is a 1,000-unit
performance change and
[`2026-08-14-thousand-unit-performance-design.md`](2026-08-14-thousand-unit-performance-design.md)
with its plan
[`2026-08-14-thousand-unit-performance.md`](2026-08-14-thousand-unit-performance.md)
already owns that territory, including a genuine stop condition if re-measurement
shows the frame is already inside budget. Deleting would destroy the only guard
on three mirrored constants: `ConservativePawnCullTests.ApparentScale_MatchesPawnGeometry`
is what would catch `ZoomScale`, `MinimumApparentScale`, and
`MaximumApparentScale` drifting from `PawnGeometry`'s private originals.

**The change.** Documentation only, in three places. The class doc names the
thousand-unit performance design as the owner of the wiring decision and states
plainly that the type is a constants guard today. Section 2 of the attack
animation V2 backlog is corrected to record that wiring cannot close AA-24,
because the bound is a superset by construction. `docs/plans/TODO.md` gains an
entry so that the parked decision is findable from the backlog rather than only
from a class comment.

**Quad delta: zero.** No code changes.

**Why this is not dodging the question.** The question "wired or deleted" assumed
wiring would change what is drawn. It would not. Recording that, with the line
that says so, is the answer — and it removes the trap that let a person read
AA-24's `PASS` as evidence the cull existed.

### 5.10 A characterization test for the collapsed contact bundle

**Mechanism.** When one attacker exceeds `MaximumPendingContactsPerAttacker`
(five), `Add` calls `ReplacePending`, which overwrites the newest pending bundle
and writes one `dbg` line. Since the contact channels were narrowed to
bundle-driven, the discarded contact produces no weapon cue, no death cue, no
blood, no clash, and no defender reaction.

The change is a test, not a fix. It drives a sixth contact for one attacker and
asserts, one by one, which channels the collapse costs, so the loss is written
down in a form that fails if it silently changes. It also asserts that the
`render.attackContactCollapsed` line is emitted with the payload the backlog
records — `attackerId`, `collapsedCount`, `sequence`, `tick` — at `dbg`, which
pins the identifier that section 2.4 found the backlog spelling wrongly.

**Constants: none.** Source is untouched.

**Quad delta: zero.**

**Why the behavioural fix waits.** Two full 500-agent battles, including three
pause-and-resume cycles and a round transition, produced no collapsed line at any
level. Changing what a never-fired path does is tuning without a measurement. The
deferral, with this precondition, goes to `docs/plans/TODO.md`.

### 5.11 The lethal blow package's isolated gate receipt

**Mechanism.** Run `./scripts/verify.ps1` at the pawn-visual-fidelity branch's
base commit `8ee5a51` with nothing else in the working tree, and paste the real
output. That is the receipt the archived lethal blow plan's own task table asked
for and never got: the attempt on 2026-08-14 failed at the build stage on
unrelated concurrent cohort work, and the later green run bundled the change with
cohort lateral spread and other uncommitted work.

**Why it belongs in this package.** It is the last thing standing between the
lethal blow subsystem and retirement, it is one command, and this package already
owns the worktree that makes an isolated run possible. The debt is recorded in
`docs/plans/README.md` and in the archived plan itself; the record is updated
when the receipt exists.

**Honesty rule.** The receipt is the pasted output of the command or it does not
exist. Section 5 of `CLAUDE.md` and the verification-honesty rule both forbid
reporting a change as verified without the real output, and a build that compiles
is not a gate run. If the gate is red at `8ee5a51` for a reason unrelated to the
lethal blow change, that is itself the finding and it is reported as such rather
than worked around.

## 6. The nine questions from `SIMULATION-GAME-STANDARDS.md` section 10

**1. User-visible outcome.** Projectiles become visible against every shipped
theme's ground instead of vanishing on two of the six (5.1). A contact frame that
the latch promised is actually drawn, including for an attacker who was off
screen when it landed (5.4). Fit zoom shows fewer overlapping trails, and the
trails that remain are killing blows (5.6). Nothing else in this package is
visible: 5.2, 5.3, 5.5, 5.7, 5.9, 5.10, and 5.11 are assertions, deletions,
documentation, and a receipt, and this document says so rather than dressing
them up.

**2. Tick stage and state read or written.** None. Every item is presentation.
No task edits `src/Hukbo.Core` or `src/Hukbo.Shared.Core`, no task adds a tick
stage, and no task reads simulation state the client does not already hold. The
client continues to decide nothing about targeting, damage, retreat, or victory.

**3. Numeric units and bounds, and the same-tick conflict rule.** Colour channel
distance is dimensionless over `[0, 255]` per channel, bound `60f`, unchanged
(5.1). Quad counts are whole quads against ceilings of 12,000 at 200 units and
20,000 at 500 (5.2, 5.3). `MaximumLatchFrames` is a whole number of frames, and
its value is set by the implementer from the existing latch tests and stated
(5.4). Apparent-scale tier boundaries are 0.95 and 1.80, unchanged (5.6). Leg
geometry is in presentation units scaled to whole pixels, from
`LegLengthUnits = 7.5f` (5.8). There is no same-tick conflict rule to state
because nothing here executes in a tick.

**4. Total ordering and random-stream policy.** No new ordering and no random
stream. The drawn-attacker buffer in 5.4 is filled in the pawn pass's existing
order and consulted by membership rather than by position, so its contents order
nothing. Nothing in this package calls `System.Random`, which is banned, or any
RNG at all.

**5. Cache source and invalidation, or "no cache".** No cache. The drawn-attacker
buffer in 5.4 is a per-frame scratch array of fixed capacity, cleared at the
start of each pawn pass and never read across frames; it is not a cache and it
is not unbounded, which section 9 of `CLAUDE.md` forbids. Nothing derived is
added to a snapshot.

**6. Save, event, and version effect.** Presentation only. No preset version, no
settings schema version, no snapshot field, no new event, and no change to either
hash. The state hash and the event hash are untouched by construction, and the
plan's gate stage is what proves it rather than this sentence.

**7. Worst-case complexity and benchmark workload.** 5.4 adds one append per
drawn pawn and one membership test per pending latch, both against arrays sized
at the attacker capacity — linear in drawn pawns, with no allocation. 5.5 adds
one store lookup per agent on a pass that is already per agent. 5.6 removes work.
5.1, 5.2, 5.3, 5.7, 5.9, 5.10 and 5.11 add no runtime work at all. The workload
that matters is the render probe at 500 units and at 1,000, and the canonical
gate's 200-agent / 10,000-tick / seed-1 headless determinism run, which must
reproduce its pinned baseline unchanged.

**8. Spectator explanation — can a spectator discover this without reading
source?** Answered honestly per item, because this is the question that fails
features.

- 5.1 projectile contrast: **yes.** A spectator on the Field Manual theme can see
  an arrow they could not see before. This is the item with the clearest
  spectator answer in the package.
- 5.4 undrawn-latch fix: **yes, but only in the negative.** A spectator cannot
  see a mechanism; they can see that a killing blow at the panel edge shows its
  contact frame when they pan to it. Nobody will attribute that to a latch, and
  they do not need to.
- 5.6 trail gating: **yes.** At fit zoom the screen has fewer arcs and the arcs
  are kills. Whether that reads as *better* is a person's judgement and the smoke
  row in 5.8 asks it.
- 5.8 leg measurement: **not by itself** — the measurement is a document. The
  smoke row it ships is exactly how a spectator's judgement enters the record.
- 5.2, 5.3, 5.5, 5.7, 5.9, 5.10, 5.11: **no**, and none of them claims to be a
  feature. They are budget assertions, a probe correction, a dead-field deletion,
  documentation, a characterization test, and a gate receipt. Under section 6 of
  `CLAUDE.md` a feature a spectator cannot discover is incomplete; none of these
  seven is proposed as a feature, and calling them one would be the dishonest
  move.

**9. Tests that fail before and pass after.** Named per task in the plan
document, with the specific test method or command. Every item has one:
5.1 a new contrast test that fails against today's three colours (the table in
5.1 is what it would print); 5.2 and 5.3 budget assertions that do not exist
today; 5.4 a coordinator test that a culled attacker's latch survives and a test
that the expiry releases it; 5.5 a probe test asserting a strictly greater count
with a real gait pose; 5.6 a `PawnQuadCount` test that a non-lethal `Medium` pawn
loses six quads and a lethal one does not; 5.7 the existing resolver and geometry
suites unchanged; 5.10 the characterization test itself. 5.9 and 5.11 change no
code and are verified by the documents and by the pasted gate output
respectively, which this document states rather than pretending otherwise.

## 7. Risks, and what would falsify each choice

**5.1, the retune.** Risk: no set of three colours clears 60 against all six
shades while remaining recognisable as wood, iron, and feather. Falsified by the
search failing — in which case the fallback is the double outline, which is in
section 8 as a user decision because it costs quads. Second risk: the six ground
shades in `WeaponVisualCatalogTests` are a mirror of the theme JSON, not the
theme JSON itself, so a theme edit could silently invalidate the table. Mitigated
by the new test reading the same mirrored set the weapon test already uses, so
both go stale together and both are fixed together, which is the convention that
file already documents.

**5.2 and 5.3, the budget assertions.** Risk: the sum breaches a ceiling and the
package stalls on a user decision. That is the intended behaviour, not a failure
— an unasserted 512 quads is worse than a stalled task. Falsified only by
discovering that a pool's capacity is not actually a bound, which would be a
bigger finding than this package.

**5.4, the drawn-attacker set.** Two risks. A latch that never releases is a
leak; the expiry bound in 5.4 exists for it, and the falsification test is a
latch left pending past `MaximumLatchFrames` that must release. And a per-frame
buffer that grows is an allocation in the draw path; the falsification is the
array being sized anywhere but at the attacker capacity the dispatcher already
uses. If profiling shows the membership test measurably costs frame time at
1,000 units, the design is wrong and the answer is a flag on the animation entry
rather than a set.

**5.5, the real gait pose in the probe.** Risk: the test compares the new code
with itself and proves nothing, which this repository has been bitten by before —
a delegating overload is not an oracle. The design names the independent
expectation for exactly that reason. Falsified if the recorded count does not
rise for a pawn with a known non-null gait pose, which would mean the null pose
was not the cause.

**5.6, the trail gate.** Risk: fit zoom reads as dead rather than clear. Falsified
by the smoke row; the remedy is a shortened `Medium` trail rather than none, and
that remedy is named here so a later reader does not have to re-derive it. Second
risk: a lethal trail at `Medium` is itself lost in the crush, in which case the
exemption bought nothing — the same row answers it.

**5.7, the field deletion.** Risk: a member was load-bearing through shared
arithmetic in the resolver. Falsified by any existing resolver or geometry test
changing its result, which is why those suites must pass unchanged rather than be
updated.

**5.8, the measurement.** Risk: the table is computed correctly and the row still
cannot be judged, because the three camera stations do not span the interesting
range. Falsified by a tester saying so; the row's `Actual` column is where that
is recorded, honestly, rather than by flipping it to `PASS`.

**5.9, the documentation-only verdict.** Risk: a later reader disagrees and wires
the cull anyway, on the belief that it widens the drawn set. Mitigated by
quoting the line that says it does not, in all three places the item touches.

**5.10, the characterization test.** Risk: it pins today's loss so firmly that a
later fix looks like a regression. Mitigated by the test's name and comment
saying what it is — a record of a known loss, not a specification of desired
behaviour.

**5.11, the gate receipt.** Risk: the gate is red at `8ee5a51` for an unrelated
reason. That is a finding to report, not a reason to skip the receipt or to run
it at a different commit without saying so.

**Package-level risk.** Another session shares this working tree and has been
observed adding foreign uncommitted files mid-task. Every commit in the plan
stages by pathspec; `git add -A` is forbidden.

## 8. Open questions requiring the user

**Answered on 2026-08-14. Read this before the questions below, which are kept
as the reasoning that produced the answers rather than as live questions.**

- **Swing trails at `Medium` (PV-3): dropped, and not to be revived.** The user
  declined it on a general principle worth carrying into later work — this
  package exists to improve what is drawn, not to thin it, and a quad saving
  does not justify taking motion off the screen. `Medium` is the tier the
  default camera fit resolves, so the change would have been visible on every
  launch.
- **Screen shake: declined.** Not wanted, and none was built.
- **An ordinary-hit hit stop: still undecided, and the recommendation below
  deserves challenge when it is next taken up.** Section 5 recommends against it
  on the grounds that it would spend the distinction `LethalHoldSeconds` buys.
  The published record cuts the other way: shipped fighters scale hit stop with
  the power of the blow *in order to* build a hierarchy of impact, and every one
  of them caps it. A short ordinary hold set against the existing 340
  millisecond lethal hold would therefore sharpen the difference between an
  ordinary blow and a killing one rather than blur it. If it is ever built it
  needs its own smoke row asking whether kills still read as distinct.
- **A projectile double outline: undecided, and now unnecessary.** PV-7 cleared
  all eighteen colour-to-shade distances by retuning alone. Revisit only if a
  future ground shade fails the envelope.

1. **Screen shake — should it exist at all?** It does not today; an exhaustive
   grep of `src/Hukbo.Client` returned nothing. The public record says it is both
   the strongest impact channel and the most common accessibility complaint, and
   that it must ship with a graduated intensity control rather than a toggle. My
   recommendation is **no**, for three reasons, one of which is mechanical rather
   than aesthetic. Hukbo has no player avatar and no player camera — the
   spectator pans and zooms themselves, and the auto-camera moves the view on its
   own, so a shake competes with two motions the user already owns. The published
   guidance is explicit that the shaken transform must be stashed and restored so
   that nothing but rendering observes it, and Hukbo's camera *is* observed by
   more than rendering: pointer picking selects a warrior through it, so a naive
   shake would move click targets. And the existing `MotionIntensity` setting is
   a three-level enum, not the nought-to-one-hundred scale the accessibility
   guideline cites, so shipping it properly means a new settings shape and a
   settings schema version. If the user wants it anyway, it is its own design
   document, not a task in this plan.

2. **Ordinary-hit hit stop — should a non-lethal contact hold too?** Hukbo
   already has a per-pawn hit stop of exactly the published kind for killing
   blows: `LethalHoldSeconds = 0.34f`, applied to the attacker and defender only,
   inside the published cap. The published range for ordinary hits starts around
   67 ms. My recommendation is **no**: the lethal hold's legibility value comes
   from being the only hold, and the strongest cross-source rule in the research
   is that the hit signal and the kill signal must never be the same effect. If
   the user wants it, the honest shape is a shorter hold in the 67-to-100 ms band
   with the lethal hold left where it is, and it needs its own smoke row asking
   whether kills still read as distinct.

3. **The projectile double outline, if the retune fails.** Section 5.1
   recommends retuning three colours at zero quad cost. If no set clears all six
   ground shades, the fallback from shmup design is a double outline of one
   bright and one dark colour so that some part of the projectile contrasts
   against any background. That costs additional stroked elements per projectile
   against ceilings this package is tightening, so it is the user's call, not an
   implementer's.

4. **If a budget assertion breaches a ceiling, raise the ceiling or shrink the
   pool?** Section 5.2 adds 512 quads to the enforced sum and section 5.3 adds
   every effect pool. Either could breach 12,000 or 20,000. Raising the ceiling
   and shrinking a pool are both legitimate, they have different visible
   consequences — a shrunk embedded pool means arrows stop staying in bodies
   sooner — and neither is an agent's decision. The plan's task stops and reports
   the arithmetic rather than picking.

5. **Does AA-22's first contributor deserve a new smoke row?** Section 2.4 shows
   the sub-pixel-arms premise is false: arms are tier-gated, not zoom-gated, and
   a 1.2-pixel floor already exists. The remaining question — whether a
   1.2-to-1.5-pixel arm reads at density — is a person's, and AA-22 is closed.
   Writing a new `PENDING` row to ask it is cheap; writing rows nobody runs is
   how a checklist rots. The recommendation is to fold the question into the
   single row section 5.8 already ships rather than to open a second one, but
   the user may want it named separately.

6. **Is the gait subsystem allowed to retire with a measurement and no tuning?**
   Section 5.8 deliberately changes no gait constant. If the user expects this
   package to make the legs look better rather than to find out whether they
   need to, that expectation should be corrected now rather than at the end.
