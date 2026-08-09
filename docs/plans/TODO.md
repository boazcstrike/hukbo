# Plans backlog

Deferred work the user has explicitly parked. Each entry names the decision
that deferred it and the document that holds the full context. An entry here
is not authorized work; it is a reminder that the question was decided
"later", not "never".

## From the visual improvement package (2026-07-28)

- **Earned red putong insignia (was OD-5).** A red head-wrapping earned by
  kills during a battle, which would require bounded client-side kill
  tracking from `Death` events as new presentation state. Excluded from the
  visual improvement pass by user decision on 2026-07-28. Context:
  the warrior appearance design and requirement
  R-W3.10 in the improve-visuals requirements note.
- **Shape-redundant faction marker (was OD-7).** A non-hue faction channel
  (shape or position) for color-blind spectators, beyond the current
  no-regression floor. Deferred by user decision on 2026-07-28. Context:
  requirement R-X (color-blind readability) in
  the improve-visuals requirements note and the accessibility
  section of the visual system integration design.
- **Jungle and plains ground texture exploration (follow-up to OD-6).** The
  approved pass shifts the default theme's ground toward cogon olive-gold.
  The user additionally wants a look at jungle and plains ground treatments
  as distinct battlefield looks. Any such work stays procedural under the
  package's boundary 4 (no textures, no content-pipeline additions) unless a
  future design revisits that boundary. Context:
  the battlefield environment design.

## From the second-round lag report (2026-07-30)

- **Formation blocking at 500 agents.** Warriors spend long stretches unable to
  move in the crush: 33 330 blocked agent-ticks against 27 882 attack-capable
  ones in the reported round, with a longest unbroken blocked streak of 168
  ticks — 8.4 seconds of a warrior standing still. Parked by user decision on
  2026-07-30 after the same session's lag report was traced to this rather than
  to the frame loop. The full measured baseline, both seeds, and what a future
  change has to beat are in
  [`2026-07-30-formation-blocking-baseline.md`](2026-07-30-formation-blocking-baseline.md).

## From the ranged units package (2026-08-07)

- **Sprite-frame animation pipeline.** Hand-authored image frames per action,
  built through `Content.mgcb` into `.xnb` and played back as an index into an
  array, as an alternative to the procedural pose resolvers the client uses
  today. This would be a genuinely new asset pipeline: an art budget, a build
  step, one frame set per weapon and per facing, and a renderer that draws
  textured quads rather than the vector limb geometry in
  `src/Hukbo.Client/Rendering/PawnGeometry.cs`. It also runs against the v0.1
  guidance in `SIMULATION-GAME-STANDARDS.md` line 18, which chose dots
  specifically to avoid an asset and animation pipeline before the simulation
  is fun. Deferred by user decision on 2026-08-07 when the ranged unit package
  was scoped: the four ranged weapons get procedural pose resolvers in the
  shape of `src/Hukbo.Client/Rendering/SwingPoseResolver.cs` instead. Revisit
  only as its own design document, never as a sub-task of a gameplay feature.

- **Projectile props and embedded projectiles.** A ranged shot currently draws
  as a stretched pixel from the launch point to the projectile's current
  position, so it reads as a line growing behind the thrower rather than as an
  object in the air. The proposal replaces that with a per-weapon in-flight
  prop, and then leaves the projectile embedded in the body part or shield it
  struck so that it rides with the pawn. Requested by the user on 2026-08-09
  after the first successful ranged battle, and parked the same day: the
  package's own goal had been reached, and this is a new feature rather than a
  fix to one. The full design, including the two features' separation, the
  quad-budget arithmetic that constrains both, the bounded ring buffer that
  keeps the embedded population from becoming the unbounded cache `CLAUDE.md`
  section 9 forbids, and five open decisions that must be answered before a
  plan is written, is in
  [`2026-08-09-projectile-props-design.md`](2026-08-09-projectile-props-design.md).
  Two things a future session should not have to rediscover. The in-flight prop
  is the small half — it fixes the reported complaint on its own, costs roughly
  1,024 quads against 1,956 of headroom, and needs none of the open decisions
  answered. And this is the feature `src/Hukbo.Client/Rendering/SubmissionCount.cs`
  warns about by name: the 500-unit margin fell from 3,468 to 1,956 across
  RU-23 and RU-42, and the next feature wanting a per-pawn quad owes a fresh
  measurement rather than an assumption. `HUKBO_RENDER_PROBE=1` with
  `tools/Hukbo.Tools.RenderProbe` is how that measurement gets taken.
