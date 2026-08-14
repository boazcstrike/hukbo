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

## From the Sandata lowered-weapon and automatic-fire package (2026-08-14)

- **No Sandata determinism fixture has ever run against a real map.** Both
  golden replays, the recorded seed-1 baseline, and the gate's own headless
  workload build their grid through `HeadlessRunner.BuildOpenGrid`, which ends
  `Array.Fill(grid.Passability, NavCellFlags.Open)`. There are no walls, no
  doors, and no map file in any of them. That is why a pathfinder which ignored
  every wall on every map — `NavSearch` reads only the blocked span it is
  handed, and `SandataSimulation` allocated that span once and never wrote to
  it — survived a green gate and a green 1,113-test suite for the whole life of
  the project, and why fixing it on 2026-08-14 moved not one pinned digest. The
  standing consequence is that **the Sandata gate cannot detect a pathfinding
  change that only manifests around geometry.** Closing the gap means a
  determinism fixture over a real map, which is a new baseline with its own
  capture, its own recorded digests, and its own decision about which map is
  canonical; none of that belonged inside a smoke-row fix, so it was parked on
  2026-08-14 rather than attempted. Context: the 2026-08-14 Sandata
  lowered-weapon and automatic-fire design, section 6.
- **`PlaceholderOperatorHealth` is tuning, not a measurement.** It was raised
  from 100 to 300 on 2026-08-14 so that an automatic burst lasts long enough to
  hear — at 100, against 7.62x39's 25 damage, the fourth round killed and no
  burst could exceed 0.30 seconds. It lives in the client's scenario builder,
  reaches no hash, and costs no preset version, but it does mean every
  engagement on the placeholder map takes proportionally longer to resolve. The
  real fix is a scenario system that carries health per spawn rather than a
  single constant for every operator on the map.

## From the pawn visual fidelity package (2026-08-14)

- **The collapsed contact bundle's behavioural fix.** The deferred path has
  never been observed firing in a real run, and changing the tuning of a
  path that has never fired would be a change made without measurement. The
  precondition is the characterization test added on 2026-08-14,
  `Coalesce_SilentlyDropsEveryPresentationCueOfTheDiscardedBundle` in
  `tests/Hukbo.Client.Tests/Presentation/AttackContactDispatcherTests.cs`,
  which records that a discarded bundle costs its weapon cue, its death cue,
  its blood, its defender reaction, and its clash effect. The same
  characterization also recorded what the collapse diagnostic actually logs:
  it carries the REPLACING contact's sequence and tick, not the discarded
  contact's, at `AttackContactDispatcher.cs:283` and `:298`, so the log keeps
  no trace of what was thrown away. Context: task PV-12 of the pawn visual fidelity plan.
- **AA-22's first contributor, the sub-pixel arm question.** Deferred because
  the premise is false on disk. Arms are gated at `PawnDetailTier.Low`
  (`PawnGeometry.cs:1380`), not below a 1.35 camera zoom — the 1.35 the
  backlog points at is `ZoomScale` at `PawnGeometry.cs:234`, a different
  constant that happens to share the value. And
  `MathF.Max(ArmMinimumHalfWidthPixels, ArmHalfWidthUnits * scale)` at
  `PawnGeometry.cs:1398` (with the two constants defined at `:289` and
  `:286`) already floors a full arm stroke at 1.2 pixels, so it is never
  sub-pixel. Context: task PV-12 of the pawn visual fidelity plan.
- **The `ConservativePawnCull` wiring decision.** Wiring `ConservativePawnCull`
  into the pawn draw loop cannot close AA-24, because the type's own remarks
  say its bound is "a genuine superset, never a replacement" and that
  "nothing here may ever be used as the only cull" — wiring it draws exactly
  the same pawns the exact test already draws, so it is a performance change
  at best, never a correctness or visibility one. The wiring decision itself
  is handed to the thousand-unit performance plan, which owns the whole-screen
  submission-count question (97,968 quads measured at 200 agents, 106,068 at
  500) that wiring would have to be justified against. Context: task PV-5 of the pawn visual fidelity plan, handing off to
  [`2026-08-14-thousand-unit-performance.md`](2026-08-14-thousand-unit-performance.md).
- **Effect-pool capacity sizing.** The five presentation effect pools —
  hit-effect, blood burst, blood ground mark, blood spurt, and clash
  effect — all share one capacity, `PawnAppearanceCache.Capacity`, because
  `PresentationCoordinator` defaults every one of their capacity parameters
  to it and `ArenaGame` overrides only `projectileCapacity`, leaving the
  other four at that shared default. One consequence already measured: hit
  effects alone contribute 60,000 of the 97,968-quad whole-screen worst case
  at 200 units, because a lethal hit effect's fixed ring-segment count is
  multiplied by that shared capacity rather than by anything sized for
  effects. Whether each pool should carry its own capacity, independent of
  `PawnAppearanceCache.Capacity`, is not decided here. Also recorded:
  `BloodEffectSystem`'s own constructor defaults of 256, 384, and 32 are dead
  in production, because every caller that matters goes through
  `PresentationCoordinator`, which never uses them. Context: task PV-12 of the pawn visual fidelity plan.
