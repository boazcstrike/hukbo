# Ranged units — session handoff, 2026-08-08

> **Archived: reference only.** Finished work, kept so a past decision can be
> traced to its reasoning. Never execute it, never treat it as current, and never
> cite it as justification for a change. The live contract is `CLAUDE.md`,
> `SIMULATION-GAME-STANDARDS.md`, `docs/development/testing.md`, and `docs/plans/`.

> **Superseded on 2026-08-09 by
> [`2026-08-09-ranged-units-handoff.md`](../../plans/2026-08-09-ranged-units-handoff.md).**
> This document is frozen at wave 4. Every count, commit, and status in it is
> stale, and the package has since reached its goal. Read it only for durable
> reasoning about decisions that were taken here.

This document exists so that a fresh agent with no memory of the previous session
can resume the ranged-units package without re-deriving anything. Read it before
executing any row of `docs/plans/2026-08-07-ranged-units.md`, because several of
that plan's own rows contain errors that were found by measurement and corrected
here, and following those rows literally would reintroduce mistakes that have
already been paid for once.

## Goal

The ranged-units package adds three ranged weapons — the Bangkaw, the Busog, and
an imported arquebus — to Hukbo's tactical battle simulation, together with
projectile flight, a five-phase draw cycle, thirteen new sound slots, and the
visual and inspector work that lets a spectator see what is happening. It also
carries two movement fixes, labelled F-A and F-B in the plan, that exist to
address the standoff: under the shipped equipment-relative footwork presets every
battle ran to a ten-thousand-tick draw, and the package treats that as a defect to
be measured and fixed rather than tuned around.

The work was started because the user authorized it on 2026-08-07, lifting the
long-standing deferral on projectiles and projectile flight time while explicitly
keeping ammunition, terrain, cover, pathfinding, and morale deferred.

## State on disk

The work is on branch `ranged-units`, in the worktree
`C:\Users\boazs\webdev\autonomous-arena\.claude\worktrees\ranged-units`, at commit
`6700d14`.

**Updated 2026-08-08, after wave 4.** Everything below reflects the state after
wave 4 was dispatched, verified, integrated, and recorded. Waves 1 through 4 are
merged.

`git status --short` immediately before this update was written reported a clean
tree:

```
```

That empty output is accurate — every wave-1 through wave-4 branch has been merged
into `ranged-units` and committed, and the plan document's wave 4 record is
committed with them.

The last ten commits on the branch:

```
6700d14 docs(plans): record the wave 4 result, add RU-40, and correct RU-20's dependency
33e2b64 Merge branch 'ru-17' into ranged-units
a5ee8bb Merge branch 'ru-20' into ranged-units
291d60c Merge branch 'ru-18' into ranged-units
ba59a1f Merge branch 'ru-19' into ranged-units
46f51fd Merge branch 'ru-38' into ranged-units
a9a54c1 feat: add the pooled ranged-attack projectile (RU-17)
e2c73d4 test(tools): match the mix harness to the 26-slot client mapping and re-measure at 500 agents
b622c76 test: derive the ranged-fields fact from weapon identity, not its own fields
37620c4 feat(client): add the ranged draw-pose resolver and its geometry
```

One blemish in that history is worth knowing about rather than discovering. The
commit `5d13249`, message `wip: stage RU-12 notes`, violates the Conventional
Commits rule that `CLAUDE.md` section 5 requires. It exists because the plan
document was staged immediately before merging `ru-12`, and the merge commit
`2f785f1` was then amended to carry the plan updates. The amend preserved both
parents, so `ru-12` is still a genuine ancestor and RU-12's authorship is intact —
that was checked with `git cat-file -p HEAD` and `git merge-base --is-ancestor`.
The messy message was left alone deliberately, on the grounds that preserving
merge parentage and attribution matters more than a tidy subject line. If this
branch is to be tidied before it approaches `main`, that is the commit to reword,
and rewording it means rewriting the merge, so it is a decision for the user
rather than something to do casually.

Every per-task worktree from waves 1 through 4 still exists under
`.claude/worktrees/` — `ru-01` through `ru-20`, plus `ru-34`, `ru-35`, `ru-37`,
`ru-38`, and `ru-39`. They are all fully merged and are safe to remove, but the
wave 1 through 3 ones belong to an earlier session, so confirm with the user before
sweeping any of them. No worktrees exist yet for wave 5.

One untracked artifact is worth knowing about rather than discovering: RU-20's
measurement run left roughly 75 MB of rendered WAV files in `mix-output/` at the
root of the `ru-20` worktree. They are covered by `.gitignore:28`, so they were
never at risk of being committed, and they regenerate on demand. Delete them or
leave them; they are disposable either way.

## What is done

Waves 1 through 4 are complete and merged: **twenty-five of the plan's forty task
rows.** The plan gained RU-40 during wave 4, which is why the denominator moved
from thirty-nine to forty.

**Wave 1 — foundations.** RU-01 corrected two stale documentation figures, most
importantly a per-tick allocation ceiling recorded as 900,000 bytes when the
enforced figure is 16,384 bytes per 1,000 warm ticks with a 4,096-byte growth
tolerance, and amended three deferral lists in `SIMULATION-GAME-STANDARDS.md`,
`CLAUDE.md`, and `AGENTS.md`. RU-02 confirmed and corrected a historical
attribution in `docs/research/HISTORICAL_1500s_WEAPONS.md`. RU-03 appended
`WeaponId.Bangkaw`, `Busog`, and `Arquebus`, the `PrecolonialPhilippinesV5` combat
preset identity, and the `RangedStandoffV8` and `MonotoneAllyClearanceV9` movement
preset identities. RU-04 appended `AgentIntent.Holding`, `BattleEventKind.Release`,
and `BattleEventKind.Miss`. RU-05 echoed the chosen presets into
`Hukbo.Headless.RunReport`.

**Wave 2 — surfaces.** RU-06 split `refuseAgentTicks` into four rejection-reason
counters on `MovementBehaviorMetrics` and `BattleSimulation`, which is F-A. RU-07
added `ProjectileSpeedRaw`, `StandoffDistanceRaw`, and `FlightTickCeiling` to
`WeaponProfile` with validation in `CombatRuleset`. RU-08 added `Release` and
`Miss` cases to `BattleEventFormatter`. RU-09 added thirteen `GameSoundId` members
and their `SoundCatalog` entries, bringing the catalog to twenty-six slots. RU-10
added three `PawnWeaponRole` members and their `WeaponVisualCatalog` entries.
RU-11 created `ProjectileFlightSystem` and `ProjectileFlight`, a fixed-capacity
tick-advanced presentation store. RU-34, which did not exist in the original plan,
made three Core test suites sweep each ruleset's own roster rather than the
`WeaponId` enum.

**Wave 3 — behaviour and routing.** RU-12 created
`src/Hukbo.Core/Combat/PhilippineCombatPresetV5.cs` with seven roster rows and
registered it in both `CombatPresetRegistry` switches. RU-13 added
`AgentView.RangedPhase` and `AgentView.RangedPhaseTicksRemaining` as derived
projections, with the derivation in a new `RangedPhaseProjection.Derive`. RU-14
added the ranged arms to `SoundCueMapper.MapWeapon` and `MapShieldClash`, a
`MapMiss` covering the ranged `Evaded` case, and a ranged row in the
spectator-channel table. RU-15 added thirteen default prompts and a nested
per-hit-class prompt table to `scripts/sfx.ps1` and fixed its `-List` counting.
RU-16 made `AgentIntent.Holding` render as a distinct inspector reason code.
RU-35 added the three ranged arms to `PawnAppearanceFactory.ToWeaponRole`. RU-37
wired F-A's counters through `HeadlessRunner`.

**Wave 4 — the projectile, its poses, its sound, and two independents.** Six tasks
were dispatched at once and all six merged without a conflict.

RU-17 created `src/Hukbo.Core/Simulation/Projectile.cs`, a `readonly record struct`
of integers and small enums only, held in a flat array sized once from a new
`Scenario.MaximumProjectilesInFlight`. It added the A0 pass at the head of
`GatherAndCommitAttacks` that advances every countdown and resolves arrivals while
folding the launch tick rather than the impact tick, the gather-pass branch that
launches instead of resolving, the pool on `BattleSnapshot`, and the
capability-gated tail fold in `StateHasher.Compute`. This is the first task in the
package to move a hash, and it moved the right one — see the determinism section.

RU-18 added `RangedPose`, `RangedGeometry`, and `RangedPoseResolver` as five new
files in `src/Hukbo.Client/Rendering` and `tests/Hukbo.Client.Tests`, in the same
pure-helper shape `SwingPoseResolver` uses, including the early-out that
`GaitPoseResolver` omits. The resolver is **not yet wired into the draw loop**;
that is RU-25's in wave 6.

RU-19 changed `SoundDirector.Ingest` to take the agent view list alongside the
events, so a classless `Release` event resolves its weapon from
`AgentView.Loadout` through RU-14's `MapRelease(WeaponId?)` hook, and updated the
single production call site at `ArenaGame.cs:1549`. It also confirmed in code that
`UpdateViews` writes a view for every agent including the dead, so a launcher killed
on the same tick still resolves.

RU-20 rewrote the mix-analysis harness's replica mapping to match the client's
twenty-six slots and re-ran it at 500 agents. It is **only partly done** — see the
next section.

RU-38 passed the agent roster through at `PresentationCoordinator.cs:140`, so
RU-16's per-faction `HoldingCount` stops reading zero in the running game. RU-39
rescoped `WeaponProfileTests`'s ranged-fields fact, taking Core from two red to one.

## What is not done

Fifteen rows remain. Two of them — RU-20's re-run and RU-40 — exist because wave 4
found problems that the plan as written did not cover.

**Carried forward from wave 4, and both should land early.**

- **RU-20, the second half.** The harness parity work is merged, but the
  measurement the task exists to produce does not exist. All three ranged release
  slots measured zero cues and −∞ dBFS, because on the base it ran against nothing
  emitted a `Release` event — RU-17 had not landed on that branch — and no ranged
  sound files exist. **Re-run it now that RU-17 has merged.** Until it produces a
  real release-cue concentration figure, **RU-31 is not cleared to spend money on
  sixty ElevenLabs generations.** The plan's dependency column for RU-20 named only
  RU-14; RU-17 has been added.
- **RU-40** — delete the public one-argument `SoundDirector.Ingest(events)` overload
  RU-19 left behind and migrate the twenty-seven test call sites to pass a view
  list. Files: `src/Hukbo.Client/Audio/SoundDirector.cs`,
  `tests/Hukbo.Client.Tests/SoundDirectorTests.cs`.

**Blocking on a user decision, and it is now the tightest constraint.**

- **RU-36** — fold the ranged tuning fields into the preset content hash.
  `CombatRuleset.AddProfile` folds only `DamagePerAttack`, `AttackRangeRaw`, and
  `AttackCooldownTicks`, so RU-07's three ranged fields never reach the content
  hash. Because RU-24 calibrates those values and RU-26 pins V5's content hash, a
  calibration pass would change how the game plays while the hash stayed
  byte-identical, and a replay recorded under the old tuning would be accepted and
  then diverge. The naive fix breaks a frozen invariant, since folding three more
  values unconditionally moves V1 through V4's content hashes. The candidate
  resolution is to fold the ranged values only when a profile declares any of them
  non-zero. **The user has not ruled on this and it must not be decided
  unilaterally.** Files: `src/Hukbo.Core/Combat/CombatRuleset.cs`,
  `tests/Hukbo.Core.Tests/DeterminismTests.cs`.

**Wave 5 is next, and both of its tasks are unblocked.**

- **RU-21** — the hold arm and the `RangedStandoffV8` movement preset, restated
  verbatim from `PersistentContingentsV4` plus one rule, in the legacy body of
  `GatherMovementProposals` and not in the equipment-relative pipeline. It is the
  fourth owner of `BattleSimulation.cs`, so it must not run in parallel with
  anything else touching that file. Registering V8 is half of what closes the last
  red Core test.
- **RU-22** — `PawnGeometry` learns the three new weapon roles and the third pose,
  across four `switch` expressions. It owns both `PawnGeometryTests.cs` and
  `ConservativePawnCullTests.cs`, which together are all twenty-one remaining
  Client failures.

**Waves 6 through 10, unchanged from the plan.** Wave 6 is RU-23, RU-24, and
RU-25; wave 7 is RU-26, RU-27, RU-28, and RU-29; wave 8 is RU-30 alone, which is
F-B; wave 9 is RU-31, which a human runs and which spends money, together with
RU-32; wave 10 is RU-33, the canonical gate, which the orchestrator runs and never
delegates.

Note that RU-22's scope was widened during the previous session: it now owns
`tests/Hukbo.Client.Tests/ConservativePawnCullTests.cs`, which was moved off RU-35
because those failures throw from `PawnGeometry.CreateWeaponLayout` as well as from
`PawnAppearanceFactory.ToWeaponRole`, so RU-35 could never have made that file
green. RU-16's scope was likewise widened to include three arms in
`AgentInspectorContent.GetLaterOrProvisionalForms`, and that part is already done.

## Verification status

**The canonical gate has not been run at any point in this work.**
`./scripts/verify.ps1` has never been executed on this branch, and no gate output
exists to quote. That is by design: the plan's RU-33 runs it once, after
integration, and the tree is deliberately red until the tasks that add the missing
registry and catalog arms have all landed.

What has actually been observed, in `Release` configuration, on the integration
branch at the wave 4 merge commit `33e2b64`:

```
Core:   Failed: 1, Passed: 2647, Total: 2648
Client: Failed: 21, Passed: 3262, Total: 3283
format: [PASS] Formatting verification completed.
```

Core is down from two red to one. **The single remaining Core failure is**
`BattleSimulationTests.ExactlyOneLivingLeaderPerNonEmptyContingentAcrossEveryRegisteredMovementPreset`,
which closes when RU-21 and RU-30 register the V8 and V9 movement presets. The
twenty-one Client failures are `PawnGeometryTests` (eleven) and
`ConservativePawnCullTests` (ten), both of which are RU-22's in wave 5. No failure
appeared outside that list at any point in the wave.

Configuration matters when comparing these numbers. A `Debug` run adds two
allocation-budget failures,
`MovementContextObservationTests.RepeatedQuietV6TicksHaveBoundedAllocations` and
`MovementPipelineIntegrationTests.RepeatedVSixCollisionTicksHaveBoundedAllocations`,
which pass under `Release` and have nothing to do with this package. A red count
quoted without its configuration is not evidence.

Every manual smoke-checklist row in `docs/development/testing.md` remains
`PENDING`. No row has been flipped, and no agent may flip one. RU-32 adds this
package's rows and they ship `PENDING` too.

## Determinism impact

Nothing merged so far reaches either hash. The seed-1, 200-agent, 10,000-tick
shipped-default workload reports `stateHash 1B73FC5923879AA0` and
`eventHash AC55684F24D39344` at every integration point in the plan's baseline
table, unchanged from the recorded baseline, and that was verified by running
`./scripts/benchmark.ps1` directly rather than by trusting a sub-agent's report.

This holds for specific reasons worth preserving. RU-03 and RU-04 appended enum
members without moving an existing numeric value. RU-06 and RU-37's counters are
derived observability that reach neither hash. RU-13's ranged phase is a projection
onto `AgentView`, with nothing added to `AgentState`, `Scenario`, `BattleSnapshot`,
or `StateHasher`. RU-12 registered a new preset without touching V1 through V4, and
`DeterminismTests` passes in full with V5 registered, so no pinned golden moved.

**That changed in wave 4, and it changed correctly.** RU-17 emits events and moves
projectiles, so it changes end-of-tick state for any ruleset that fields a ranged
weapon. The capability gate in `StateHasher.Compute` means a ruleset with no ranged
entry folds nothing at all, not even a zero, so the frozen presets cannot drift.
Both halves of that were measured on the integration branch:

| Preset | `stateHash` | `eventHash` |
| --- | --- | --- |
| V4, before and after RU-17 | `1B73FC5923879AA0` | `AC55684F24D39344` |
| V5, before RU-17 | `1B2524B9DFEB7FDB` | `673EF3076D2B2EC9` |
| V5, after RU-17 | `CA230133F128B1A9` | `6953A1C982A3014C` |

The V5 row moving is not a problem to be fixed — it is the proof that projectiles
actually fly. Given this package's history of features that were structurally
complete and functionally dead, an unmoved V5 hash would have meant nothing was
launching. Run both benchmarks, not just the default one, whenever a change could
touch the ranged path.

**The V5 hashes above are not goldens and must not be pinned yet.** RU-24's
calibration will move them again, and RU-26 is the row that pins them afterwards.

RU-30's F-B still changes end-of-tick positions and still ships as
`MonotoneAllyClearanceV9` with its own frozen digest. V1 through V4, V6, and V7
keep their existing content hashes and digests throughout. Never re-pin an existing
baseline to make a test pass; a moved hash on a preset that should not have moved
is a real defect, and `hukbo-determinism-change` is the skill that covers
diagnosing it.

## Open questions and risks

**RU-36 is an open decision, stated above, and it gates RU-26's pins.**

**The plan document contains errors that measurement exposed, and its rows are not
trustworthy on their own.** Five separate rows were wrong in ways that would have
cost real work:

- RU-06's acceptance number, 1,140,221 refuse agent-ticks, reproduces on no
  current preset. It came from an archived V7 baseline dated 2026-07-31, which
  `CLAUDE.md` section 6 forbids citing at all, and the tree has drifted since. The
  measured V6 figure is **692,750**, and V7 today is 1,092,119.
- RU-05's row named `Program.cs` when the preset options already existed in
  `HeadlessRunner.cs`.
- RU-06's row named `RunReport.cs` when the population site is
  `HeadlessRunner.cs:520`, which is why RU-37 had to exist.
- RU-02's row asked for the volume-III date span `1569-1576` to be "corrected"
  when that span is accurate; the agent correctly refused that clause.
- Section 3 predicted the known-red window would be two tests. It was
  twenty-nine, and fourteen of those had no owner in the original thirty-three
  tasks, which is why RU-34 and RU-35 were added.

Section 3 and section 9 of the plan now carry corrections for all of these. **Read
those two sections before executing any row.**

**F-A's result is stronger evidence than the plan anticipated, and it should shape
how F-B is judged.** With RU-37's wiring in place, the four counters on a V6
seed-1 workload read: `routeRefusalLaneNotClear` 692,700,
`routeRefusalDirectCandidateOmitted` 50, and the other two zero. The standoff is
a single-cause failure and the cause is exactly the predicate RU-30 rewrites, so
RU-30's "collapse" is a collapse in a counter whose baseline is now known exactly.
A V9 run that does not move 692,700 substantially has falsified the diagnosis
rather than under-delivered on a tuning target. Recomputing the root-cause
document's route-search failure rate on today's numbers gives 94.85% against the
95.61% it claimed, so the diagnosis stands and only the arithmetic needed fixing.
Be careful not to over-read the figure: these are agent-ticks in a refused state,
not distinct decisions, so one warrior stuck for a thousand ticks contributes a
thousand.

**A `Release` event cannot name its own weapon, and RU-19 must handle that.**
`BattleEvent.NonAttack` takes no weapon parameter, so `SoundCueMapper.Map` returns
null for `Release` and `Miss` and will keep doing so however the mapper is written.
RU-14 exposed `MapRelease(WeaponId?)` as `internal` specifically so RU-19 can call
it after resolving the launching weapon from `AgentView.Loadout` at the call site.
**Do not "fix" this by adding a weapon parameter to `NonAttack`** — that would
relax the guarantee RU-04's pinned tests hold, and the standards document treats a
non-attack event carrying combat context as a contract violation.

**Two features are structurally complete and functionally dead**, and both got
that way through the same mechanism: an optional trailing parameter that let the
code compile and the tests pass while the behaviour never ran. RU-16's
`HoldingCount` is one, addressed by RU-38. RU-14's release and miss routing is the
other, addressed by RU-19. Treat an optional trailing parameter in this package as
a smell worth checking rather than a convenience.

**RU-13's projection is a recorded bet, not a settled design.** It derives the
five-phase draw cycle from the existing attack cooldown rather than storing real
per-agent state, which is a deliberate divergence from what the pose research
asked for. If the phases read as arbitrary on screen — a warrior appearing to draw
when nothing is happening, or `Release` not lining up with the projectile leaving —
then the research was right and the correct fix is real per-agent state with its
own hash fold, which is a substantially larger change. The bet is recorded in the
`RangedPhase` enum's doc comment and in design section 8.1.

**RU-13's end-to-end path was checked in wave 4 and it holds.**
`RangedPhaseProjection.Derive` is called unconditionally for every agent at
`BattleSimulation.cs:4483`, with no gate and no optional parameter, so it runs every
tick for every agent, and a live V5 battle was driven through the headless runner
for the first time. What remains unproven is whether the phases *read* correctly on
screen, which is a wave 6 question for RU-25 rather than a correctness question
about the derivation. The bet recorded above is not yet settled either way.

**All tuning values in V5 are provisional and RU-24 owns them.** Reach is 48, 80,
and 112 world units against the Kampilan's 16; standoff is 36, 60, and 84;
cooldowns are 25, 45, and 240 ticks. None of these is a historical measurement and
none may be cited as one. All three ranged rows carry `RankId.Timawa` uniformly
because the design document states that no source ties these weapons to a social
rank, so a differentiated hierarchy would be invented.

**Two things that looked wrong and were left alone.** The pre-existing combo
fields on `WeaponProfile` are not folded into the content hash either; that gap
predates this package and is explicitly out of RU-36's scope. And
`docs/research/HISTORICAL_1500s_WEAPONS.md` line 42 cites a Luzon account of
palm-wood lances that belongs to a 1571-72 document in the same volume rather than
to Artieda; it is accurate as written but the source entry does not enumerate it.

**The shipped audio mix already clips, and it is not this package's fault.** RU-20
measured a melee-only 500-agent battle under the shipped policy — sixteen per slot,
sixty-four total, `CueVolume` 0.65 — at **+0.9 dBFS with 8 clipped samples**,
against the −0.2 dBFS and zero clipped samples recorded in section 7.2a of
`docs/research/SOUND-CAPACITY-MEASUREMENTS.md`. Peak concurrent voices rose from
forty-one to fifty-four. The cause is `Hukbo.Core` combat drift since that
2026-07-27 measurement. The mix is therefore over full scale before a single ranged
cue exists, and this package intends to add thirteen more slots on top of it. That
is a `CueVolume` and gain question belonging to whoever owns the audio mix, it is
out of the ranged package's authority, and **it should be settled before RU-31 is
paid for.** It is recorded in section 7.2b of the research document and in section
9 of the plan rather than silently absorbed.

The per-slot cap of sixteen does **not** bind — zero suppressions out of 6,302 cues
— so the raised `DefaultMaximumPerSound` that RU-20's row anticipated is not
indicated, and `SoundCueBudgetTests.cs:59-79` should not move.

**A test that re-derives its own premise from the data under test is not a weak
test, it is not a test.** RU-39's first attempt derived "is this profile ranged"
from the same three fields it then asserted on. Because
`WeaponProfile.ValidateRangedFields` (`WeaponProfile.cs:140`) already forces every
profile that constructs into all-zero-or-all-non-zero using the identical
predicate, the fact could not fail in any direction, and the suite was green. Its
negative proof looked convincing because it exercised a copy of the branch logic
rather than the fact itself. The accepted version cross-checks against
`RangedPhaseProjection.Derive`, which switches on `WeaponId` alone, so the two
declarations are independent. Watch for this shape in RU-26's pins especially,
where the temptation to assert a value against itself is highest.

**Verifying agent reports against the disk caught real problems in this wave, in
both directions.** RU-20 reported work it had not committed and reported that
`.gitignore` did not cover `mix-output/` when `.gitignore:28` does. RU-39 reported
a green suite for a worthless test. RU-19 honestly self-reported a deviation that
did need an orchestrator ruling. Read the diff, not the summary.

## How to resume

The work is not isolated in a per-task worktree at this point — the integration
branch itself is a worktree. Enter it and confirm the base before anything else:

```powershell
cd C:\Users\boazs\webdev\autonomous-arena\.claude\worktrees\ranged-units
git branch --show-current      # expect: ranged-units
git log --oneline -3
git status --short
```

Then re-establish the baseline yourself rather than trusting the numbers in this
document, because a stale baseline is how a real regression hides among known
failures:

```powershell
dotnet build Hukbo.slnx -c Release
dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release --no-build
dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release --no-build
pwsh -NoProfile -Command "./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1"
pwsh -NoProfile -Command "./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1 -Preset PrecolonialPhilippinesV5"
```

Expect Core **1** failed of 2648, Client 21 failed of 3248 before any new tests are
added, the V4 run matching `1B73FC5923879AA0` and `AC55684F24D39344`, and the V5
run matching `CA230133F128B1A9` and `6953A1C982A3014C`.

Run the V5 benchmark as well as the default one from now on. The default workload
alone cannot see a ranged regression, because V4 fields no ranged weapon and
therefore folds nothing into the hash — a completely broken projectile path would
leave the default run byte-identical and green.

Read, in this order: `docs/plans/2026-08-07-ranged-units.md` sections 3 and 9 for
the corrections, then the rows for the tasks being dispatched, then
`docs/plans/2026-08-07-ranged-units-design.md` for the sections those rows cite.

Ask the user for the RU-36 decision before wave 6 begins, and do not decide it by
default. It was put to the user at the end of wave 4 with three options and a
recommendation — the conditional fold — and had not been answered when this
document was updated. Two other questions were put to the user at the same time and
are also unanswered: whether to fix the clipping mix before RU-31, and whether to
sweep the merged per-task worktrees.

Wave 5 is RU-21 and RU-22, both unblocked. RU-20's re-run and RU-40 are small,
independent of both, and can go in the same wave.

Orchestration follows `CLAUDE.md` section 10 and the `hukbo-orchestrate` skill:
one worktree per task branched from the current integration commit, an explicit
non-overlapping file list per agent, at most eight agents at once, coding tasks on
Sonnet, every prompt naming its evidence and its return format and stating the
current known-red baseline with its configuration. Verify each agent's report
against the disk rather than accepting it, because reports in this package have
been wrong in both directions. Integrate and re-run both suites after every wave;
that practice is what caught the `AgentIntent` pin failure, which no individual
branch could see.
