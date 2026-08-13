# Strike-while-moving legibility (`AA-23`) — design

Date: 2026-08-13
Status: **answered and implemented on 2026-08-13.** This document was design
only, and section 6's question blocked all work until it was answered. It has
since been answered — with a fourth option this document did not table — and
both causes below have been repaired. Section 6 records the decision and the
reasoning that rejected the three options originally offered. The ordered task
list lives in the gait default visibility plan.

**The row that prompted this document has since closed, and nothing was fixed.**
`AA-23` was re-attempted later on 2026-08-13 and passed, so the whole attack
animation V2 family is now `PASS` and its section has been deleted from
`docs/development/smoke-checklist.md`. **Both causes measured below are still
true of the build.** A passing row is a statement that a person could see what
the row asked about; it is not a statement that the default camera fit draws a
leg, because it does not.

That changes this document's standing but not its content. It is no longer
blocking a checklist row, so section 6's question is no longer urgent — but it
is also no longer being tracked anywhere else, which is precisely why this file
stays in `docs/plans/` rather than going to the archive. `AA-22`, which every
remedy here trades against, also closed `PASS` on the same day without a change
to the 500-agent density, so the constraint in section 6 is now a judgement
about what the game should look like rather than a conflict between two open
rows.

## 1. The observation

Smoke row `AA-23` read: *"Watch a warrior strike while moving — the attack
plants the stance and composes with the stride; the body does not jump between
two poses."* On its first attempt on 2026-08-13 a person at an interactive
Windows desktop reported that **no warrior was visibly striking while walking**.
That is the observation this document diagnoses. The row passed on a later
attempt the same day, presumably at a zoom above the default fit, which is
consistent with everything below rather than in tension with it.

That is a report about an absence, which is the hardest kind to act on: it does
not say the composition looked wrong, it says the thing the row asks about was
never on screen. There are three ways a row like that fails — the simulation
never produces the state, the client never draws it, or it is drawn too small to
see — and they need different fixes. All three were checked.

## 2. The simulation does produce the state, commonly

**Verdict: not the cause.** A warrior can move and attack on the same tick under
the shipped defaults, and by tick count that is the ordinary case rather than a
rare one.

The shipped client presets are `CombatPresetId.PrecolonialPhilippinesV5` and
`MovementPresetId.LastStandEngagementV11`
(`src/Hukbo.Client/ArenaGame.cs:1451-1452`). They are set by `BuildScenario`
overriding `Scenario.CreateDefault`, whose own record defaults —
`PrecolonialPhilippinesV6` and `PersistentContingentsV4`
(`src/Hukbo.Core/Simulation/Scenario.cs:117-118`) — are what the headless runner
and the gate use. **A statement about the client's behaviour that names V6 or
V10 is a statement about the wrong build.**

Within one tick, `CommitMovement` runs at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:858` and
`GatherAndCommitAttacks` at `:860`, unconditionally and in that order. The
attack precheck ladder at `:4483-4492` tests alive, has target, target alive,
`IsWithinAttackRange`, and cooldown — and tests nothing about intent, movement
resolution, or whether the agent moved this tick. `IsWithinAttackRange` (`:5179`)
is evaluated on the post-movement position.

This is deliberate and the code says so at `:1422-1430`: *"An agent keeps
advancing until its body meets the target's, even once the target is already
inside reach… One that strikes while still closing is re-marked Attacking by
attack gathering."* Agents close to body contact
(`stopShortRaw: 2 * BodyRadiusRaw`, `:4951-4958`) while attacks resolve at
weapon reach, which is wider. `Scenario.Validate` (`Scenario.cs:459`) throws
unless `2 * BodyRadiusRaw <= AttackRangeRaw`, so the band between contact and
reach is structurally guaranteed to exist.

The only two "if attacking, do not move" guards in the repository are both dead
on the shipped path: `BattleSimulation.cs:2417-2423` is reachable only under
`UsesEquipmentRelativeFootwork`, which `LastStandEngagementV11` sets to `false`
(`MovementPresetRegistry.cs:573`), so no agent is ever in `FootworkPhase.Commit`;
and `:2055` is the ranged standoff branch, gated on a nonzero
`StandoffDistanceRaw` that every melee weapon in V5 leaves at `0`.

## 3. Cause one — at the default camera fit a pawn has no legs

**This is the primary cause and it fully explains the report.**

`PawnGeometry.CreateLegsAndFeet` returns `default` — four empty rectangles — for
any pawn at `PawnDetailTier.Low`
(`src/Hukbo.Client/Rendering/PawnGeometry.cs:1591-1594`). The tier ladder is
`< MediumDetailScale` resolves `Low` (`PawnGeometry.cs:955-960`), with
`MediumDetailScale = 0.95f` and `ZoomScale = 1.35f` (`:224`, `:223`).

The default launch resolves below that threshold. At the default window of
1280 × 720 (`ArenaGame.cs:27-28`) with the default startup mode `Windowed`
(`ClientSettingsStore.cs:66-67`), `ComputeLayout` (`ArenaGame.cs:2224-2267`)
produces an arena panel of 826 × 640: the event panel takes
`min(420, 1280/3) = 420`, the arena's right edge lands at 838, and its left edge
at the 12-pixel margin. `SpectatorCamera.Fit` (`SpectatorCamera.cs:133-134`)
then takes the smaller of the two axis fits against the default map of
1280 × 720 (`Scenario.cs:25-26`):

```
horizontalZoom = 826 * 0.88 / 1280 = 0.5682   <- the minimum, so this wins
verticalZoom   = 640 * 0.80 /  720 = 0.7111
apparentScale  = 0.5682 * 1.35     = 0.7671
0.7671 < 0.95  ->  PawnDetailTier.Low  ->  legs and feet are Rectangle.Empty
```

A spectator at the default camera fit is therefore watching pawns that have no
legs to plant. `AA-23` cannot be observed there by anyone, and the tester's
report is an accurate description of the build.

Legs first exist at `cameraZoom >= 0.95 / 1.35 = 0.7037`, which is 1.24 times
the fit zoom — two notches of the mouse wheel at the 1.15 factor
(`SpectatorCamera.cs:12`).

**This is consistent with the rest of the family's results and is not
contradicted by them.** `AA-15`, which compares the three detail tiers, and
`AA-20`, which watches a 200-warrior battle at close zoom, both passed on the
same day. Neither is a default-fit observation of a leg.

## 4. Cause two — in the attack band the stride phase is effectively frozen

**This is a real second defect, and it would still bite after cause one is
fixed.** It is the reason zooming in may not be a sufficient answer on its own.

`GaitAnimationSystem` advances stride phase by **distance travelled**, never by
elapsed time: `PhaseTurns += distance / StrideCycleDistanceRaw` with
`StrideCycleDistanceRaw = 6000f`
(`src/Hukbo.Client/Presentation/GaitAnimationSystem.cs:75`, `:232-233`). That is
the correct model for a warrior crossing the field, and it is what keeps two
warriors moving identically from stepping in lockstep.

It breaks for exactly the warriors `AA-23` is about. A closing attacker inside
the reach-but-not-contact band is under the arrival taper:
`ComputeArrivalStepRaw` returns `Math.Max(1L, untapered * remaining / taper)`
(`src/Hukbo.Core/Movement/MovementRules.cs:504-518`), and `desiredMovement` is
itself floored at 1 (`BattleSimulation.cs:5014`). The step therefore decays
toward **1 raw unit per tick** and stays there for most of the approach.

At 1 raw unit per tick the phase advances `1/6000` of a turn per tick. At
Hukbo's 20 Hz that is a full stride cycle every **300 seconds**. The legs do not
swing; they sit at whatever fixed offset the entity's deterministic phase offset
gave them and stay there.

Meanwhile `GaitGeometry.ResolveMode` (`GaitGeometry.cs:84-101`) returns `Stance`
only at exactly zero displacement, so 1 raw unit classifies as `Walk`. The
warrior is reported as walking, is drawn with a walking pose, and does not
visibly walk. That is precisely the state `AA-23` asks a person to watch, and it
is the state in which the stride is least visible.

Two smaller hazards sit alongside it, recorded so they are not rediscovered:

- **`DirectionSign` starts at zero and only updates on a nonzero `deltaX`**
  (`GaitAnimationSystem.cs:162`, `:226-231`), and leg offsets are multiplied by
  it (`PawnGeometry.cs:1602-1604`). A warrior whose X never changes has zero leg
  swing at every tier while its feet still lift.
- **A blocked step commits an exactly-zero delta** (`BattleSimulation.cs:4174`),
  which resolves `Stance`. Crowded melee makes this common, and it is correct
  behaviour rather than a defect.

## 5. What the damping is doing, for completeness

`PlantStride` (`PawnGeometry.cs:1186-1202`) is **not** implicated. It damps the
gait's leg and foot ratios by `1 - (MaximumStancePlant * StanceWeight)` with
`MaximumStancePlant = 0.6f` (`:256`), so a fully committed attack keeps 40 per
cent of the stride rather than discarding it. Both poses do reach it: the draw
loop resolves them independently at `ArenaGame.Rendering.cs:1178-1191` and
composes them at `:1199-1203`. The one call site that passes `gaitPose: null`,
`ArenaGame.Rendering.cs:505-508`, feeds `RecordPawnQuads`, which is render-probe
metrics and draws nothing.

The composition is correct. It is worth noting only that at Medium tier with a
10-pixel leg, a planted walk stride is `0.32 * 0.4 * 10 = 1.28` pixels against
an unplanted `3.20`, so it rounds to 1 pixel against 3. There is a test pinning
a one-pixel floor for the **gait-only** case
(`tests/Hukbo.Client.Tests/PawnGeometryTests.cs:766`) and **none for the
composed case**; the only composed-case test runs at High tier
(`tests/Hukbo.Client.Tests/Rendering/AttackPoseRenderingTests.cs:277`) and
asserts only that the striking stride is smaller than the walking one, with no
pixel floor. That gap is worth closing whichever way section 6 is answered.

## 6. The question this document existed to ask, and its answer

**Should the default camera fit draw legs?**

**Answered on 2026-08-13: yes, and by none of the three options below.** The
client's default window was raised from 1280 × 720 to 1600 × 900, which moves
the default camera fit above the `Medium` threshold without touching the detail
tier ladder at all. The arena panel becomes 1146 × 820, the fit becomes
`1146 * 0.88 / 1280 = 0.7879`, and `apparentScale` becomes `1.0637`, clearing
`MediumDetailScale = 0.95`. The whole map still fits in the panel, `Low` stays
reachable by zooming out and so keeps doing the job it exists for, and the
minimum 1024 × 720 window still resolves `Low` unchanged.

Call it option D. It was not in the table below because the table assumed the
only lever was the tier ladder. The window size is the other end of the same
arithmetic, and it is the cheaper end.

Each of the three tabled options was rejected for a specific reason:

- **A** concedes that a spectator at the default view never sees a warrior walk,
  which abandons the gait feature's own stated outcome rather than delivering
  it.
- **B** contradicts smoke row `GA-7`, which exists precisely to check that legs
  and feet disappear cleanly at the lowest tier, and it spends quads at the tier
  a 500-warrior battle is watched at. `GA-7` closed `PASS` on 2026-08-14,
  recorded in the archived movement gait animation smoke section titled
  "Movement gait animation smoke — closed 2026-08-14"; the point above is
  retained because it is why the constraint exists, not because the row is
  still open.
- **C** looked cheapest and is the most damaging. `ResolveApparentScale` clamps
  its result to a floor of `0.72`, so moving `MediumDetailScale` below `0.767`
  would leave `Low` alive only across `[0.72, 0.767)` — roughly five per cent of
  the scale range. The tier that keeps a large battle readable would become
  nearly unreachable and `GA-7` nearly unattemptable. This document's own cost
  column did not catch that, because it reasoned about the threshold without
  reasoning about the clamp beneath it. `GA-7` closed `PASS` on 2026-08-14; the
  reasoning above is kept because it is why the constraint exists, not because
  the row is still waiting.

Cause two was answered separately, and this document was right that it is
separable. Displacement below a crawl threshold now resolves `Stance` rather
than `Walk`. The threshold is derived from the criterion it serves rather than
chosen by eye: a stride slower than one full cycle every five seconds is not one
a spectator can read as walking, which gives
`6000 / (5 * 20) = 60` raw units per tick. It clears the arrival-taper floor of
1 and stays well below the pinned walk magnitude of 400, so every existing walk
and run classification is unchanged. The alternative shape — advancing phase by
elapsed time whenever the mode is `Walk` — was rejected because it reintroduces
exactly the wall-clock dependence the gait design removed on purpose.

The composed-case pixel floor that section 5 identified as untested has also
been closed, with a `Medium`-tier test asserting the composed attack-and-walk
stride keeps at least one pixel of leg offset. It measures exactly one pixel, so
the assertion sits on the boundary rather than comfortably above it.

The original table follows, unchanged, as the record of what was considered.

Every remedy for cause one is a change to the detail-tier ladder, and `Low` is
the tier a 500-warrior battle is watched at. When this document was written that
was a direct conflict: `AA-22` was `FAIL` on a 500-warrior battle reading as
chaos, and adding leg and foot quads at `Low`, or moving the ladder so the
default fit resolves `Medium`, would have put more marks on the screen in
exactly the case that was already too busy.

`AA-22` has since passed, on the same unchanged density. That removes the
conflict but not the concern: the tier ladder still exists to keep a large
battle readable, and a change here still spends part of that budget. The
question is now a deliberate one about what the default view should show, rather
than a choice between two failing rows.

The three candidates, with what each costs:

| Option | What changes | Cost |
| --- | --- | --- |
| **A. Leave the ladder alone; reword `AA-23` to name a zoom station** | Nothing in code. The row becomes an explicit close-zoom or Medium-tier observation, as `AA-20` already is | Free, and honest, but it concedes that a spectator at the default fit never sees a warrior walk at all — which is a legibility claim worth making deliberately rather than by default |
| **B. Draw a reduced leg pair at `Low`** | `CreateLegsAndFeet` gains a `Low` branch | Adds quads per pawn at the tier used for 500-agent battles. Directly against `AA-22`. Needs a quad-budget measurement and a `PawnQuadCountTests` update before it is proposed, not after |
| **C. Move `MediumDetailScale` so the default fit resolves `Medium`** | One constant, from `0.95` to below `0.767` | Cheapest to write and the widest blast radius: it promotes every pawn at the default fit to the full rig, arms and all. `AA-22`'s own recorded contributor is that arms at density read as continuous noise |

Cause two is separable and does not depend on this answer, but it also is not
free of judgement: the fix is to make the stride visible for a warrior whose
body is crawling at 1 raw unit per tick, and the two obvious shapes — advancing
phase by time whenever the mode is `Walk`, or classifying a sub-threshold crawl
as `Stance` so the legs rest cleanly instead of freezing mid-swing — produce
visibly different games. The second is closer to the truth of what the body is
doing; the first is closer to what the row asks to see.

## 7. What is already established, so it is not re-litigated

- The simulation is not at fault and must not be changed for this row. Both
  causes are in `Hukbo.Client`, so any remedy is presentation-only and moves
  neither hash.
- `MaximumStancePlant`, `PlantStride`, and the two-channel composition are
  correct. Do not tune them for `AA-23`.
- The shipped presets are V5 and `LastStandEngagementV11`. Verify against
  `ArenaGame.cs:1451-1452` rather than against `Scenario.CreateDefault`.
- Whichever option is chosen, the missing composed-case pixel floor at Medium
  tier described in section 5 should be closed with it.
- **No `AA` row is waiting on this.** `AA-23` and `AA-22` both closed `PASS` on
  2026-08-13 and their family was deleted from the live checklist. Do not revive
  the closed ones.
- **The fourteen `GA` rows are waiting on it, and that is why the work was
  done.** The movement gait animation smoke section has never been run by
  anyone. Both causes above stood between a spectator and the rows that ask
  whether a warrior visibly walks at the default zoom, so both were repaired
  before the section was handed to a tester. Repairing them does not pass those
  rows and does not predict that they will pass.
