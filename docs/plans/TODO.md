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

- **Projectile props and embedded projectiles — shipped, no longer parked.**
  The entry that stood here described work that has since been built. The
  per-weapon in-flight prop and the embedded projectile both shipped at
  `3ec5523` on 2026-08-11, the five open decisions its design left were all
  answered by its plan, and all eight `PP-*` smoke rows closed `PASS`, the last
  of them on 2026-08-13. Both documents are archived; find them by their titles,
  "Projectile props and embedded projectiles — design" and "Projectile props and
  embedded projectiles — plan". The quad-budget warning the entry carried is not
  lost with it: it lives in `src/Hukbo.Client/Rendering/SubmissionCount.cs` by
  name, which is where a future feature wanting a per-pawn quad will meet it.

## From the unit test cleanup (2026-08-14)

- **Three settings managers with no shared type behind them.**
  `MotionIntensityManager`, `GoreIntensityManager`, and `AutoCameraModeManager`
  are still three independently copied classes, and their twenty test methods
  are three copies of one suite. The cleanup plan's bucket D closed for the
  three *selectors*, which now delegate to `SettingsChoiceSelector<T>`, and the
  managers were left alone deliberately, on that plan's own criterion: folding
  tests for a source duplication nobody has consolidated deletes real coverage
  for nothing. Consolidating the managers is therefore the prerequisite, and it
  is a source change rather than a test change. `UiThemeSelector` is not part of
  this — it does not delegate to the generic selector at all, it carries its own
  bounds math, swatch rendering, and provisional-reconstruction label, and its
  five tests hold behaviour nothing else asserts. Context: the archived unit
  test cleanup plan, "Unit test cleanup — what can be removed, and what must
  not be", section 12.
