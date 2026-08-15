# In-fight evasion — design

Date: 2026-08-15

Author's note on evidence. Every claim in section 1 was checked against the
working tree at `main` (`cfe0c22`) rather than taken from an earlier document.
Where a fact came from a run rather than from a file, the run's parameters are
given so it can be repeated.

## 1. The problem, and the evidence for it

The complaint is that warriors do not move while fighting, and that the one
thing in the build called "evasion" makes a warrior leave the battle rather
than move inside it. Both halves are true, and the reasons are separable.

**The shipped preset runs no footwork at all.** The client's default movement
preset is `MovementPresetId.CohortLateralSpreadV13`, fixed at
`src/Hukbo.Client/Settings/ClientSettingsStore.cs:113-114`. That preset's
registered ruleset, at `src/Hukbo.Core/Movement/MovementPresetRegistry.cs:634-655`,
declares `usesEquipmentRelativeFootwork: false`, `appliesPressureInterrupt: false`,
and `loadoutMovementProfiles: ImmutableArray<LoadoutMovementProfile>.Empty`.
Only V6, V7, and V9 register the footwork flag true (`:283`, `:350`, `:511`).
Every consumer of that flag is therefore dead in the shipped game: the
posture-and-provisional-footwork stage is skipped
(`BattleSimulation.cs:861-867`), the equipment-relative route pipeline is
skipped (`:1966-1973`), the friendly-clearance conflict pass is skipped
(`:870-877`), and the attack-footwork and death-cleanup pass is skipped
(`:883-888`).

A gate-shaped headless run — 200 agents, 10,000 ticks, seed 1, movement preset
13 — reports every field of `movementMetrics` as zero: approach, engage,
commit, recover, refuse, disengage, regroup and pursue agent-ticks, and
`facingStepsTurned`, all zero, with `stateHash 4A0723BC9A1B924B`,
`eventHash E0CE32CF8830A864`, outcome `Faction1Victory`, exit 0. The whole
weapon-relative movement system — six loadout profiles, sixteen-sector facing,
eight footwork phases, six tactical postures — exists in the repository and is
unreachable from the shipped build.

**What the shipped preset actually does for movement is a straight line.**
`BuildMovementProposal` at `src/Hukbo.Core/Simulation/BattleSimulation.cs:5035-5087`
takes `delta = destination - agent`, scales it by a scenario-wide
`MovementSpeedRaw` (3072 raw units per tick) after the arrival taper, and clamps
to map bounds. There is no lateral component, no per-loadout pace, and no
reaction to anything the enemy just did. The single existing deviation from a
straight line is `BuildSidesteppingPursuitProposal` (`:3821`), and it is
reachable only after a warrior has been physically blocked for an unbroken 192
ticks (`FormationRules.StallEscapeStreakTicks = 192`) — nine and a half seconds
of standing still at a tick rate of 20. It is a deadlock escape, not footwork.

**The thing called evasion today is a retreat.** Two distinct mechanisms carry
that name and neither is in-fight movement. The first is
`AttackResolution.Evaded = 4` (`src/Hukbo.Core/Combat/AttackResolution.cs:51`),
produced by `ClashResolver.Resolve`'s interval walk at `ClashResolver.cs:135-139`.
It is a *dice outcome only*: it changes no position, no facing, and no phase,
and the blow simply carries no damage (`BattleSimulation.cs:4636-4640`). The
comment on the enum member says the defender "stepped off the line", but no code
anywhere moves the defender. The second is the ranged retreat rung at
`BattleSimulation.cs:2036-2057`, which reads `RangedRetreatRules.ThreatRadiusRaw`
and `RangedRetreatRules.IsThreatened`, sets `AgentIntent.BackingAway`, and walks
the warrior to the point reflected through itself away from the threat
(`TryBuildRetreatProposal`, `:5106-5131`). That rung is exactly the complaint:
it moves a warrior out of the fight. It is also correct for what it was built
for — a bowman with a swordsman on top of him — and this design does not touch
it.

**The renderer cannot show small movement even when it happens.** Three defects
compound. `GaitAnimationSystem.Advance`
(`src/Hukbo.Client/Presentation/GaitAnimationSystem.cs:226-231`) derives the
stride direction from `deltaX` alone and discards `deltaY` entirely; a warrior
stepping due north or south keeps its previous `DirectionSign`, which is `0f`
for an entry created that tick (`:162`). `PawnGeometry.CreateLegsAndFeet`
multiplies both leg offsets by that sign (`PawnGeometry.cs:1708-1711`), so a
zero sign zeroes both legs and the body slides with dead legs. Second,
`GaitGeometry.ResolveMode` returns `GaitMode.Stance` — every stride and lift
offset zeroed — for any per-tick displacement below
`CrawlThresholdRawPerTick = 60f` (`GaitGeometry.cs:57, 113-121`), so a genuinely
moving warrior can render as standing still. Third, the run lean at
`GaitGeometry.cs:203` is signed by direction of travel, so a warrior stepping
backwards leans into the retreat and reads as routing.

**The inspector shows nothing.** `FormatFacingLine`, `FormatPostureLine`, and
`FormatFootworkLine` (`src/Hukbo.Client/UI/AgentInspectorContent.cs:607, 648, 696`)
each return `null` at the `None` value that every legacy preset leaves forever,
so under the shipped preset those three rows draw nothing at all.
`GetFootworkLabel` (`:718-733`) and `GetPostureLabel` (`:653`) throw on their
default arm, which is why the null guards are load-bearing rather than cosmetic.

**Nothing measures any of this.** There is no metric anywhere in the repository
for displacement magnitude, rootedness, lateral travel, or net drift.
`MovementBehaviorMetrics` counts footwork phases and facing sectors, and reports
zero under every legacy preset by construction (`HeadlessRunner.cs:402-407`). So
the statement "warriors do not move enough" currently has no number behind it,
in either direction.

## 2. What ships, and what is explicitly deferred

**What ships.** One new movement preset, `EvasiveFootworkV14`, restating V13's
ruleset verbatim and adding four movement mechanics that fire only while a
warrior is engaged with an enemy it has not abandoned. One new authoritative
per-agent enum, `EvasiveAction`, folded into the state hash behind its own gate.
One new derived metric family measuring displacement and evasion, reconstructed
outside the simulation and never hashed. One renderer fix so a small step
animates instead of sliding. One inspector row. A sixth gate workload. Finally,
and separately, the client default flips to V14.

**What is explicitly deferred, and why.**

*The clash roll stays where it is.* `AttackResolution.Evaded` keeps its meaning
and its numbers. A warrior that leaps aside on the tick a projectile lands will
still be hit if the launch-tick roll said hit, because a projectile's clash and
hit location are resolved from its **launch** tick, not its arrival tick
(`BattleSimulation.cs:4413-4429`, and the comment at `:4363-4374` states this
deliberately). Coupling movement to the roll would require a new combat preset,
would move `DefenceAttributableShare`, and would put a gameplay-tuning number on
top of a table whose ranged cells are already marked provisional. This design
accepts the visible mismatch and documents it rather than hiding it. It is also
faithful to the source: Pigafetta records that the leaping and the shields did
*not* save the men at Mactan — bolts passed through shield and arm alike.

*No new `FootworkPhase` or `TacticalPosture` members.* `FootworkPhase` stays at
nine members and `TacticalPosture` at seven. Adding to either would break
`FootworkPhaseRulesTests`, the `FootworkPressureInterruptTests` sweep, and the
inspector's phase table, for a preset that does not resolve phases at all.

*`usesEquipmentRelativeFootwork` stays false on V14.* Turning it true would
activate the entire V6 route pipeline, change every proposal in the game at
once, and fold five more fields into the state hash. That is a different, much
larger feature, and it is the same pipeline whose refuse-to-commit behaviour
produced the standoff that V10 abandoned. V14's behaviour is gated on preset
identity at its own call sites, exactly as V10, V11, V12 and V13 already are.

*No reactive melee dodge.* A melee attack resolves entirely within one tick:
`ClashResolver.Resolve` (`:4608`) and the damage write (`:4728`) sit in the same
method on the same tick with no intervening state any other agent could read.
There is no interval during which a blow "is incoming", so a defender has
nothing to react to. Building one means authoritative pending-blow state
mirroring the projectile pattern — outcome rolled at commit, held N ticks,
revealed at contact — which is a new combat preset and new golden expectations.
Deferred, and named as deferred.

*No pathfinding, no steering behaviours, no continuous velocity.* Movement stays
one integer step per tick built by the existing `BuildMovementProposal`
arithmetic. Every mechanic here works by choosing a different **aim point** and
letting the unchanged paced, tapered, bounds-clamped step arithmetic do the rest
— the same technique `TryBuildRetreatProposal` and
`BuildSidesteppingPursuitProposal` already use.

*No shield-side asymmetry.* There is no source for a shield-side bias in the
research corpus — the survey found no evidence at all for a shield-side turn or
for circling to the shield side — and `CombatIdentity` carries no handedness.
Every lateral choice here is signed by a deterministic alternation rule, not by
equipment.

*No spear or bow footwork.* `Bangkaw` and `Busog` exist as `WeaponId` values but
appear in no shipped roster; `MovementRuleset.CanonicalLoadoutIndex` returns
`-1` for both and `ResolveLoadoutProfile` throws. Adding footwork for them is
two new canonical rows and a content-hash move, and is out of scope.

## 3. The V14 preset, and every gate it must join

`MovementPresetId.EvasiveFootworkV14 = 14`, appended after 13, never renumbered.
Its registered ruleset is a verbatim restatement of `CohortLateralSpreadV13Ruleset`'s
field values under its own `id` — the same convention V10 through V13 already
follow — with `usesEquipmentRelativeFootwork: false`,
`appliesPressureInterrupt: false`, and empty loadout profiles. Its `ContentHash`
differs from V13's automatically, because `ComputeContentHash` folds `Id` first
(`MovementRuleset.cs:628-632`).

The hazard this preset family carries is that **V10 through V13 are
behaviourally identical by ruleset and distinguished only by closed identity
patterns scattered through `BattleSimulation.cs`**. A new preset that is not
named at every one of them silently loses a behaviour with a green suite. Commit
`3163fbf` exists because this happened to V13. There are exactly three such
sites, and V14 must be admitted to all three:

1. `UsesBattlefieldRealism` at `BattleSimulation.cs:5214-5218` — a closed
   `is BattlefieldRealismV10 or LastStandEngagementV11 or ContingentShapeV12 or
   CohortLateralSpreadV13` pattern that gates cohort deployment (`:683`), the
   nearest-melee-threat scratch (`:271`, `:1260`, `:522`), and the ranged
   retreat rung (`:2017`, `:2031`).
2. `YieldsLastStandEngagement` at `BattleSimulation.cs:1532-1535` — the same
   closed pattern over V11, V12 and V13, gating the two last-stand regroup
   yields.
3. `spreadCohortsLaterally` at `BattleSimulation.cs:708-709` —
   `scenario.MovementPreset is MovementPresetId.CohortLateralSpreadV13`, a
   single-value test that decides whether `CohortDeploymentAssignment.AssignForFaction`
   walks contingent ids in lateral-riffle order or in size-descending order.
   **This site is not covered by either of the two named predicates and is the
   one a reader of the other two would miss.** V14 must be admitted here, or its
   armies deploy in a different shape from V13's and every comparison between the
   two becomes meaningless.

V14 must **not** be admitted to `FormationPlanner.ResolveContingentSizes`'s
authored-sizes branch (`FormationPlanner.cs:233`), which tests
`!= ContingentShapeV12` and therefore already excludes V14; V14 takes the
square-root sizing path V11 and V13 take. No edit is needed there.

The acceptance proof for this section is a differential run, not a reading: V14
before any evasive rung exists must produce, against V13 on the same seed and
roster, byte-identical final agent positions, an identical event fold, and an
identical terminal tick, while producing a **different** state hash — because
`StateHasher.Compute` folds `(int)scenario.MovementPreset` at `StateHasher.cs:121`,
so 14 cannot hash as 13. That pair of assertions is what proves the gating is
complete rather than merely present.

**That differential holds only until the first rung lands, and must be
superseded rather than weakened when it does.** See the plan's task 9.

## 4. `EvasiveAction`, and the state-hash gate

`EvasiveAction` is a new authoritative enum in `Hukbo.Core.Movement`, and a new
`AgentState` property appended **after** `BrokeOffUnderPressure`, which is the
last property in the current declaration order. Appending last is not stylistic:
`StateHasher` folds the five footwork fields in `AgentState` declaration order
under the `movementContentHash` gate and the three pressure fields in
declaration order under the `appliesPressureInterrupt` gate
(`StateHasher.cs:164-178`), and the frozen V6 and V7 digests pin both orders.

Members and numeric values, frozen once V14's digest ships:

| Value | Member | Meaning |
| --- | --- | --- |
| 0 | `None` | No evasive movement this tick. The value every preset from V1 to V13 leaves forever, and the value death cleanup writes. |
| 1 | `SlipLateral` | M2. The warrior wove while closing. |
| 2 | `DodgeIncoming` | M3. The warrior stepped off the line of an inbound missile. |
| 3 | `GiveGround` | M4. The warrior yielded a foot while pinned in contact. |
| 4 | `BreakOff` | M1. The warrior stepped off the line after an exchange that did not land. |
| 5 | `BreakOffArmed` | M1's carrier. An exchange against this warrior was intercepted on the tick just resolved; the break step is owed on the next tick. |

The fold is gated on a new boolean parameter, `foldsEvasiveAction`, appended
last to `StateHasher.Compute`'s signature and passed by
`BattleSimulation.ComputeStateHash` as
`Scenario.MovementPreset is MovementPresetId.EvasiveFootworkV14`. **It is a gate
of its own, and not a reuse of either existing gate**, for the same reason
`appliesPressureInterrupt` is a separate gate from `movementContentHash`: V6
already passes a non-null movement content hash, so folding inside that block
would move V6's per-agent byte layout and break the frozen V6 digest. When the
gate is false, nothing new is written anywhere — not even a zero — which is what
keeps every pinned hash from V1 to V13 exactly where it is.

The fold position is inside the per-agent `foreach`, immediately after the
`appliesPressureInterrupt` block closes and before the loop's closing brace, so
it cannot disturb either preceding layout. It must be inside the loop and not
after it: the global `hasRangedWeapon` block at `StateHasher.cs:181-196` folds
after the loop closes, and inserting a per-agent value there would interleave
agent data with projectile data.

`BrokeOffUnderPressure` is not reused for `BreakOffArmed`. That field is written
only under `AppliesPressureInterrupt`, which V14 registers false, and it means
something else — a blow abandoned by the attacker, not a blow intercepted
against the defender.

## 5. The four mechanics

Every constant below is a **provisional reconstruction — gameplay tuning under
`CLAUDE.md` section 7, not a historical measurement** — and must carry that
label in source. Sixteenth-century Philippine sources describe weapons, not
footwork intervals. Every rule is stated in integer raw fixed-point units, where
`FixedPoint.Scale = 1024`, one world unit is 1024 raw, the default body radius is
4352 raw (`CollisionRules.DefaultBodyRadiusRaw = (17 * FixedPoint.Scale) / 4`),
the body diameter is 8704 raw, attack range is 12,288 raw, full movement speed is
3072 raw per tick, and the tick rate is 20.

### 5.0 Where the mechanics run, and how they are committed

All four resolve in a single new private method, `ApplyEvasiveFootwork`, called
from the **end** of `GatherMovementProposals` after its main loop closes and
before `ResolveCollisions`, guarded by a single
`if (Scenario.MovementPreset is not MovementPresetId.EvasiveFootworkV14) return;`
on its first line. It cannot be a rung inside the existing loop: every existing
branch `continue`s out of the loop body, so there is no reachable "last rung".
Running as a post-pass over the already-built `_movementProposals` array also
means the entire existing path stays byte-identical for V1 through V13, proved by
a single early return rather than by reading fourteen branches.

The stage reads each agent's tick-start `EvasiveAction`, writes its decision into
a per-agent scratch array sized zero unless the preset is V14 (the precedent is
`_nearestMeleeThreatSquared` at `BattleSimulation.cs:271`), and commits the
scratch onto `AgentState` only after the loop closes. That is the "resolve once,
commit once" discipline the equipment posture stage already documents, and it is
what makes the stage order-independent: no agent can ever read another agent's
already-updated value.

The ladder is priority-ordered and mutually exclusive. Exactly one value is
written per living agent per tick. First match wins: `DodgeIncoming`, then
`BreakOff`, then `GiveGround`, then `SlipLateral`, then `None`.

Three guards apply to every rung without exception, and they are the clauses that
make this "movement during the fight" rather than another retreat:

- A rung fires only for a living agent whose tick-start `Intent` is `Moving` or
  `Attacking` **and** whose `TargetEntityId` names a living enemy. It never fires
  for `Regrouping`, `Holding`, `BackingAway`, `Idle`, or `Dead`. The ranged
  retreat rung and the last-stand regroup are therefore untouched by
  construction.
- No rung ever writes `Intent` or clears `TargetEntityId`. The warrior keeps the
  enemy it selected, keeps its combat intent, and keeps attacking on the same
  tick it moves — `GatherAndCommitAttacks` runs after `CommitMovement` and reads
  range at the post-move position.
- Every rung builds its proposal through the existing `BuildMovementProposal`
  overload that reports a bounds clamp. If the clamp changed either coordinate,
  the rung **yields**: the pre-existing proposal stands unchanged and `None` is
  written. This is the hazard-one convention `TryBuildRetreatProposal` already
  uses at `:5124-5128`, and it stops a warrior against a map edge from proposing
  a step that silently does nothing.

No rung draws from any random generator. `ApproachSidestep`'s hash is not
consulted and no `SplitMix64` stream is touched, so the random stream after this
stage is exactly what it was before it existed.

### 5.1 Shared arithmetic: the duty phase and the perpendicular offset

Both live in a new pure static class, `src/Hukbo.Core/Movement/EvasionRules.cs`,
whose methods read only their own arguments — no agent array, no simulation, no
tick pipeline — matching `RangedRetreatRules` and `MovementRouteRules`. Division
truncates toward zero everywhere and nothing touches floating point.

**The duty phase.** `FiresThisTick(long tick, ulong entityId, int periodTicks)`
returns true when `tick % periodTicks == (long)(entityId % (ulong)periodTicks)`.
This rate-limits a mechanic to once every `periodTicks` ticks per warrior with
**no timer state at all**, and staggers neighbours so a rank never steps in
unison. It is total, cheap, and trivially testable.

**The alternation sign.** `DutySign(long tick, int periodTicks)` returns `+1`
when `(tick / periodTicks) & 1` is zero and `-1` otherwise. Consecutive fires by
the same warrior therefore alternate direction, so lateral displacement over any
two consecutive fires sums to zero up to integer truncation and collision
refusal. **This is the property that makes the anti-drift bar in section 8
structural rather than hopeful.**

**The perpendicular offset.**
`PerpendicularOffset(long deltaX, long deltaY, long distanceRaw, int offsetRaw, int sign)`
returns `(-deltaY * offsetRaw * sign / max(1, distanceRaw), deltaX * offsetRaw * sign / max(1, distanceRaw))`
— the vector of length `offsetRaw` at right angles to `(deltaX, deltaY)`, in the
same widened-`long`-then-truncate shape `ApproachSidestep` uses. Returns `(0, 0)`
when `distanceRaw` is zero, which the callers treat as "the rung yields".

### 5.2 M1 — Break off the line after an intercepted exchange

**Evidence tier: provisional reconstruction.** That a fighter moves after a bind
or a blocked cut is a general property of contact weapons and is not attested for
any specific sixteenth-century Philippine engagement. The interval and the
distance are gameplay tuning.

**The rule.** In `GatherAndCommitAttacks`, immediately after a resolution is
known and buffered (`BattleSimulation.cs:4620-4622`), when the preset is V14 and
`resolution` is any of `ShieldBlocked`, `Parried`, `Deflected`, or `Evaded`, and
the defender is alive, and **the defender's current `EvasiveAction` is `None`**,
write `EvasiveAction.BreakOffArmed` onto the defender. The "only when `None`"
condition does three things at once: it prevents the arm from masking an evasive
movement the warrior actually executed this tick, which would corrupt the derived
metrics; it makes the write idempotent and confluent when several attackers
strike the same defender in one tick, since every one of them would write the
same value and only the first is observed; and it caps break-off at once every
other tick, because a warrior that executed `BreakOff` this tick holds a
non-`None` value when the attack stage runs.

On the next tick, the `BreakOff` rung fires when the agent's tick-start
`EvasiveAction` is `BreakOffArmed`. It offsets the **aim point** — not the
endpoint — perpendicular to the vector to its target by
`BreakOffOffsetRaw = 3 * BodyRadiusRaw` (13,056 at the default radius), signed by
`DutySign(tick, 1)` so consecutive break-offs alternate, and rebuilds the
proposal toward `target position + offset` with `stopShortRaw = 2 * BodyRadiusRaw`,
the same stop-short the ordinary enemy-closing overload uses at `:4992`. The
warrior therefore circles its enemy at contact distance rather than opening the
range.

**Numeric consequence.** At a typical contact distance of 8704 raw, an offset of
13,056 turns the heading by about 56 degrees, so the committed 3072-raw step
carries roughly 2500 raw of lateral motion and closes about 1700 raw. Both are
far above the 60-raw gait stance threshold, so the step animates.

### 5.3 M2 — Slip laterally while closing

**Evidence tier: documented in general form, provisional in its numbers.**
Pigafetta's account of Mactan describes men moving from side to side rather than
straight in. The angle, the interval, and the distance are gameplay tuning.

**The rule.** Fires when the agent's tick-start `Intent` is `Moving`, its target
is alive, the squared distance to the target is **greater than**
`CollisionGeometry.ContactSquaredDistance(BodyRadiusRaw)` — the same arrived-guard
the cohesion branch already uses at `:2124` — and **at most** `SlipRadiusRaw²`,
where `SlipRadiusRaw = agent.AttackRangeRaw * 20000 / 10000`, twice the warrior's
own reach (24,576 raw at the default), and
`FiresThisTick(tick, entityId, SlipPeriodTicks)` with `SlipPeriodTicks = 8` — one
slip every 0.4 seconds per warrior, with entity id spreading the phase across
eight consecutive ticks. The aim point is the target's position offset
perpendicular by `SlipOffsetRaw = 2 * BodyRadiusRaw` (8704), signed by
`DutySign(tick, SlipPeriodTicks)`, rebuilt with the ordinary
`stopShortRaw = 2 * BodyRadiusRaw`.

**Numeric consequence.** At 20,000 raw out, an 8704-raw offset turns the heading
by about 24 degrees, giving roughly 1240 raw of lateral motion out of the
3072-raw step. The convergence cost is the cosine: about 9 per cent slower
closing on slip ticks, one tick in eight, so under 1.2 per cent overall — which
is why the termination bar in section 8 is expected to hold.

### 5.4 M3 — Step off the line of an inbound missile

**Evidence tier: documented, with a documented outcome mismatch.** This is the
best-attested evasive movement in the corpus. Pigafetta records at Mactan that
the men would never stand still but leaped about under missile fire. Two
independent manuscripts carry it. The rule below is that behaviour; the interval
and the distance are gameplay tuning.

**The rule.** Fires when at least one live projectile in
`_projectiles[0.._projectileLiveCount]` names this agent as `TargetEntityId` with
`TicksRemaining <= DodgeImminenceTicks = 2`. Ties break on the lowest
`LaunchTick`, then the lowest `SourceEntityId`, matching every other multi-result
query in the file. The aim point is the agent's own position offset perpendicular
to the vector **from the projectile's origin to the agent** by
`DodgeOffsetRaw = 2 * BodyRadiusRaw`, signed by `DutySign(projectile.LaunchTick, 1)`
so the sign is a property of the shot rather than of the dodger, rebuilt through
the arbitrary-point overload with `stopShortRaw = 0` and the agent's existing
`TargetEntityId` preserved.

The projectile pool is readable at this point in the tick because flight countdown
and arrival are resolved in pass A0 of `GatherAndCommitAttacks` (`:4375-4487`),
which runs after movement. A projectile holding `TicksRemaining == 1` when the
movement stage reads it therefore arrives at the end of the same tick, and one
holding `2` arrives on the next.

**The accepted mismatch.** Per section 2, the arrival's `ClashResolver` call
folds `projectile.LaunchTick` (`:4421-4429`), so the outcome was fixed the instant
the shot left the bow. A warrior can visibly leap aside and still be recorded as
hit. This is deliberate, it keeps `DefenceAttributableShare` and every pinned
combat digest untouched, and it is what the source describes: the leaping did not
save them. It must be stated in the preset's own XML documentation so no later
reader treats it as a bug.

### 5.5 M4 — Give ground while pinned in contact

**Evidence tier: provisional reconstruction.** This is the one rung that moves a
warrior backwards, so its bounds matter more than its origin.

**The rule.** Fires when the agent's target is alive, the squared distance to it
is at or inside `CollisionGeometry.ContactSquaredDistance(BodyRadiusRaw)`, the
agent's tick-start `MovementResolution` is `Blocked` — an authoritative,
already-hashed per-agent field (`AgentState.cs:150`, folded at
`StateHasher.cs:149`) written by the collision stage, meaning the press of bodies
refused its last step — and `FiresThisTick(tick, entityId, GiveGroundPeriodTicks)`
with `GiveGroundPeriodTicks = 12`, at most once every 0.6 seconds per warrior.
The aim point is the agent's own position moved directly away from the target by
`GiveGroundStepRaw = 1024` — one world unit, one third of a full step, and 23.5
per cent of a body radius — rebuilt through the arbitrary-point overload with
`stopShortRaw = 0` and the target id preserved.

Using `MovementResolution == Blocked` as the pressure signal costs nothing: it is
already computed, already authoritative, already folded into the hash, and needs
no new O(n) neighbour scan, which at 200 to 1000 agents is the difference between
free and quadratic. It is labelled provisional because "the collision stage
refused my step" is a proxy for "I am pinned", not a measurement of enemy
pressure.

**Why it cannot read as a rout, in integers.** A warrior stops at
`2 * BodyRadiusRaw = 8704` from its target's centre, while attack range is
12,288, so there are 3584 raw of slack before it leaves its own reach. A 1024-raw
give-ground step consumes 28.6 per cent of that slack and the warrior stays in
reach. Three consecutive fires — 36 ticks, 1.8 seconds — total 3072 raw and still
leave 512 raw of slack. In practice the ordinary pursuit proposal closes the gap
again on the very next tick, because the taper only engages inside
`4 * BodyRadiusRaw` and the step is 3072 raw against a 1024-raw deficit.

**How it is distinguishable from the ranged retreat, mechanically and not merely
by intent.** The ranged retreat sets `AgentIntent.BackingAway` (`:2056`),
reflects the threat through the actor to a destination at the *full* threat
distance behind the warrior, and consumes an entire unbounded pursuit budget per
tick until the shooter is clear. M4 never writes `Intent`, moves a fixed 1024
raw, fires at most once every twelve ticks, keeps the enemy selected, and keeps
the warrior inside its own attack range. A spectator sees a fighter yielding a
foot in the press; the inspector says `Evasion: Giving ground`; the intent row
still says the warrior is fighting.

## 6. Determinism contract

`Hukbo.Core` remains the sole owner of gameplay truth, and nothing in this design
reads a clock, a renderer, a dictionary iteration order, or a floating-point
value.

**Tick stage order is unchanged.** The stages stay `DecrementCooldowns`,
`SelectTargetsAndIntents`, `ResolveContingentStates`, `GatherMovementProposals`,
`ResolveCollisions`, `CommitMovement`, `MeasureCollision`,
`GatherAndCommitAttacks`, `ResolveOutcome`, `UpdateViews`. `ApplyEvasiveFootwork`
is not a new stage; it is the tail of `GatherMovementProposals`, before any
proposal is resolved or committed, which is the same position
`ResolveFriendlyClearanceConflicts` occupies under V6.

**Total orders.** The evasive stage walks `_agentStates` in storage order, which
is spawn order and is stable. The projectile scan for M3 walks
`_projectiles[0.._projectileLiveCount]` in pool order, which pass A0 maintains by
order-preserving compaction (`:4369-4386`), and breaks ties on
`(LaunchTick, SourceEntityId)` ascending.

**Random-stream policy: no draws.** Not one rung consumes a `SplitMix64` value.
Lateral direction comes from `DutySign`, an integer parity, and lateral magnitude
from named constants. Nothing downstream shifts merely because this stage exists
— which is the same structural argument that let `ApproachSidestep` ship without
moving a recorded hash.

**Same-tick conflict rule.** Exactly one `EvasiveAction` per living agent per
tick, decided by the priority ladder, computed into scratch and committed once
after the loop. The one cross-stage write is the M1 arm in
`GatherAndCommitAttacks`, which is monotone (only `None` to `BreakOffArmed`) and
therefore confluent under any number of attackers striking the same defender in
the same tick.

**Read/write set.** The stage reads `_movementProposals`, `_agentStates`
(position, faction, alive, intent, target, attack range, movement resolution,
tick-start evasive action), the projectile pool, `Scenario` constants and the
tick. It writes `_movementProposals` and, after the loop,
`AgentState.EvasiveAction`. It writes nothing else.

**Hashing.** `EvasiveAction` folds as an `int` inside the per-agent block, after
the pressure-interrupt block, gated on preset identity 14. Every other fold is
untouched.

**A corpse must not carry a stale action into the hash, and the existing death
cleanup cannot be the thing that prevents it.** The obvious precedent —
`ApplyEquipmentAttackFootworkAndDeathCleanup`, which zeroes the footwork and
pressure fields of a dead agent — is gated in its entirety on
`UsesEquipmentRelativeFootwork` at `BattleSimulation.cs:890`, so it never runs
under V13 or under V14. Dead agents are nevertheless still folded, so relying on
that pass would leave a killed warrior's last evasive action in the state hash
forever.

`ApplyEvasiveFootwork` therefore owns the clearing itself. It walks every agent
rather than only the living ones, and writes `None` for any agent that is not
alive before considering a single rung. That is one branch in a pass that
already runs every tick under V14, it is idempotent for an agent that died many
ticks ago, and it keeps the invariant in the one place a reader will look for
it.

**What must stay byte-identical, and is the acceptance criterion for the whole
package.** The five recorded gate baselines: 6/4 `5460D13E3F7FD3E5` /
`8E18ED1437B2924B`; 5/8 `C8023D3B5BEB005E` / `F709A345E2F7370E`; 5/10
`7C145A9E05916E4C` / `77626E104234206C`; 5/11 `6225182B4A470F91` /
`C4DABE6AF98B6BEC`; 5/13 `4A0723BC9A1B924B` / `E0CE32CF8830A864`. The nine freeze
fixtures `Fixtures/seed-1-200-agents-movement-v{1..9}-digest.json` replayed by
`MovementPresetFreezeTests`. The trajectory pins at
`CohortLateralSpreadV13Tests.cs:612-613`, `ContingentShapeV12Tests.cs:259-260`
and `:279-280`, and `DeterminismTests.cs:58`, `:243-244`, `:311-312`. The V6 and
V7 fold literals at `MovementStateHashTests.cs:192-193`. **If any of these moves,
the change is wrong. None of them is ever rebaselined by this work.**

**Derived observability is excluded from all of it.** The new evasion metrics are
reconstructed in `HeadlessRunner` from consecutive `AgentView` diffs, exactly as
`MovementBehaviorMetrics` already is (`HeadlessRunner.cs:396-412, 605-700`). They
are never hashed, never snapshotted, never persisted, and cannot influence an
outcome. Two same-seed runs of the same build must produce identical values in
every field.

## 7. The nine acceptance questions of `SIMULATION-GAME-STANDARDS.md` section 10

**1. User-visible outcome.** Under the new preset, warriors weave on the final
approach, circle after a blow is turned aside, leap off the line of an arrow, and
yield a foot when the press pins them — while staying on the enemy they selected.
The battle line reads as a fight rather than as two blocks of statues, and the
one existing behaviour objected to, a warrior leaving the battle, is not extended
to melee.

**2. Tick stage and state read/written.** Written at the tail of
`GatherMovementProposals`, before collision resolution: `_movementProposals` and
`AgentState.EvasiveAction`. One additional write in `GatherAndCommitAttacks` sets
`BreakOffArmed` on an intercepted defender. Reads are listed in section 6. No
other stage's inputs or outputs change.

**3. Numeric units, bounds, and same-tick conflict rule.** All units are raw
fixed-point, 1024 per world unit; the constants run from 1024 to 13,056 raw
against a 4352-raw body radius, a 12,288-raw reach, and a 3072-raw per-tick
speed, with periods of 8 and 12 ticks at 20 ticks per second. Every step
magnitude is at least 384 raw, which is 6.4 times the 60-raw gait legibility
floor. The conflict rule is the priority ladder, one value per agent per tick,
scratch-then-commit.

**4. Total ordering and random-stream policy.** Storage order for agents, pool
order with `(LaunchTick, SourceEntityId)` tie-break for projectiles, and zero
random draws. Section 6 gives the full statement.

**5. Cache source and invalidation.** No cache. The one per-tick scratch array is
cleared at the top of the stage and sized zero under every preset but V14.
`_nearestMeleeThreatSquared` is read but not written by this feature.

**6. Save, event, and version effect.** One new folded field on `AgentState`
behind a new gate, so V14 gets its own state hash and V1 through V13 keep theirs.
One new projected field on `AgentView`, defaulted, so every existing construction
site compiles unchanged. **No new `BattleEvent` kind and no change to any event's
payload, so the event hash is untouched for every preset including V14.** No
client settings schema bump: the current `SupportedSchemaVersion = 12` covers a
default-value change, exactly as the V10-to-V11 and V11-to-V13 flips did without
a bump. `RunReport` gains one new metrics object, additively.

**7. Worst-case complexity and benchmark workload.** The stage is O(A) in living
agents plus O(A x P) worst case for the M3 projectile scan, where P is
`_projectileLiveCount` and is zero under every melee-only roster — so the shipped
melee workload pays one pass over the agent array per tick. The benchmark
workload is the canonical one: 200 agents, 10,000 ticks, seed 1, added to
`scripts/verify.ps1` as a sixth block pairing `PrecolonialPhilippinesV5` with
`EvasiveFootworkV14`, appended after the V13 block. The V13 block is **not**
repointed; it stays as the leak detector proving V14's rungs never reached the
preset every earlier build ran. A 500-agent result is reported alongside, per the
same section's closing requirement.

**8. Spectator explanation.** The agent inspector gains one row,
`Evasion: <label>`, whose formatter returns `null` at `EvasiveAction.None` so
that under every preset from V1 to V13 the panel is byte-identical and
`MaximumLowerRowCount = 46` is untouched. The renderer's contribution is the gait
fix: a step that used to slide now animates, and a give-ground step no longer
leans into its own retreat.

**9. Tests that fail before and pass after.** Per mechanic, a scenario test that
pins the exact proposed endpoint in raw units for a hand-built two-agent case and
fails against today's straight-line proposal. Plus: a gating differential test
proving V14 before any rung matches V13 position-for-position while hashing
differently; pinned `StateHasher` fold literals for the new gate in both its
states; a ladder-exclusivity property test; an anti-drift test proving two
consecutive fires cancel; the anti-goal harness of section 8; and the five
untouched baselines and nine untouched fixtures as the standing negative test.

## 8. Anti-goals, with numeric bars

Task zero measures every V13 value below before a single behaviour changes,
because none of these quantities has ever been measured (section 1). The bars are
stated as relationships to those measurements plus absolute bars where an
absolute one already exists. **A bar that fails is a redesign, not a bar to
widen.**

1. **Combat balance must not move.** `CombatMetrics.DefenceAttributableShare`
   stays inside `[0.25, 0.45]` across seeds 1 through 20 at 200 agents — the
   existing band at `PhilippineCombatIntegrationTests.cs:711-712`. The seed-1
   baseline measured for this work is 0.3124. V14 does not touch the clash roll,
   so a move here means a rung changed who is in range of whom, which is a
   defect.
2. **Battles must still end.** At least 19 of 20 seeds decisive before the
   5000-tick cap with a median at or below 5000. Against the recorded sweep for
   the shipped family — no seed at the cap, longest seed 7 at 3264, median 2253,
   faction split 11/9 — V14's median terminal tick must not exceed V13's measured
   median by more than **25 per cent**, and no seed may reach the cap. Evasion
   that delays decision by more than a quarter is a stalling mechanic.
3. **Nobody may leave the battle.** Mean net displacement, measured as the
   distance from each living agent's spawn point to its terminal position, must
   not exceed V13's measured mean by more than **15 per cent**. The
   alternating-sign rule of section 5.1 makes this structural for M1, M2 and M3;
   M4 is the only rung with a directional bias and it is capped at 1024 raw every
   twelve ticks.
4. **Engagement must be retained.** Mean agent-ticks spent with a living
   selected enemy inside the warrior's own `AttackRangeRaw`, centre to centre,
   must be at least **90 per cent** of V13's measured 385.88. This is the
   direct, numeric form of "movement during the battle, not away from it", and
   it is the single bar most likely to catch a mistuned rung.

   **This bar originally read `ContactSquaredDistance` and was vacuous.** Task 1
   measured it as exactly zero on all twenty seeds, across 3,170,540 agent-ticks
   in which a warrior held a living enemy target. The cause is structural rather
   than a measurement defect: `CollisionGeometry.Overlaps` uses a strict `<`, so
   the collision resolver guarantees no committed position ever sits below
   `(2 * BodyRadiusRaw)^2 = 75,759,616`, while a tangency-inclusive contact test
   is satisfied only at *exactly* that value. The closest any warrior came in
   the whole matrix was 75,759,617 — one squared raw unit above it, never on it.
   A bar comparing zero against ninety per cent of zero passes for any
   behaviour whatsoever, including a behaviour that empties the battlefield.
   Attack range is the non-degenerate form of the same question and is what the
   bar now uses.
5. **Movement must actually increase.** The rooted share — agent-ticks whose
   per-tick displacement is below the 60-raw gait threshold, divided by living
   agent-ticks — must fall **strictly below** V13's measured value. That is the
   whole point of the feature and it must be a pass/fail number, not an
   impression.
6. **But not into a marathon.** Total travel per living agent must not exceed
   V13's measured value by more than **30 per cent**. Weaving adds motion; it
   must not turn the battle into a footrace.
7. **Give ground must stay a minority behaviour.** `GiveGround` agent-ticks must
   be at most **10 per cent** of living agent-ticks, and `EvasiveAction != None`
   agent-ticks must be strictly greater than zero and at most **40 per cent**.
   The floor proves the feature is alive; the ceilings prove it has not taken
   over.
8. **Nothing already shipped may move.** The five gate baselines, the nine freeze
   fixtures, and all pinned trajectories of section 6 stay byte-identical.

## 8.1 The measured V13 baseline

Measured by `tests/Hukbo.Core.Tests/Movement/EvasionCalibrationHarness.cs` under
`CohortLateralSpreadV13` and `PrecolonialPhilippinesV5`, 200 agents, seeds 1
through 20, every other scenario field taken from `Scenario.CreateDefault`. The
harness compiles only under the `HUKBO_CALIBRATION` symbol, so the gate never
pays for it; it is re-run by hand for the V14 column at plan task 13.

| Quantity | Pooled V13 value | The bar written against it |
| --- | --- | --- |
| Rooted share | 0.6221 (per-seed range 0.5188 to 0.6974) | must fall strictly below |
| Travel per living agent | 559,764.96 raw | ceiling 727,694 raw (+30 per cent) |
| Mean net displacement | 354,300.93 raw | ceiling 407,446 raw (+15 per cent) |
| Reach retention | 385.88 agent-ticks per agent | floor 347.29 (90 per cent) |
| Median terminal tick | 2081 (mean 2251.9) | ceiling 2601 (+25 per cent) |
| Decisive seeds | 20 of 20, split 11 / 9 | at least 19 of 20, no seed at the cap |

Raw pooled accumulators, so a later run can confirm it measured the same thing:
`livingAgentTicks` 3,222,567; `rootedAgentTicks` 2,004,875; `totalTravelRaw`
2,239,059,836; `netDisplacementSumRaw` 1,417,203,729; `targetHeldAgentTicks`
3,170,540; `reachAgentTicks` 1,543,521; `spawnAgentSlots` 4000.

Two observations worth carrying forward. **Nearly five ticks in eight are
already rooted** under the shipped preset, which is the number behind the
complaint this work answers. And **V13 is already 20-of-20 decisive**, not
19-of-20, so the termination bar has no slack being spent before V14 starts —
any seed that fails to decide under V14 is a regression V14 caused.

## 9. Open questions

1. **Is the dual role of `EvasiveAction` acceptable?** The field carries both
   "what the warrior did" and, through `BreakOffArmed`, "what it owes next tick".
   The "arm only when `None`" rule makes this lossless for metrics and confluent
   under concurrent attackers, but it is one field doing two jobs. The
   alternative is a second folded field under the same gate, at the cost of one
   more integer per agent in the fold. This design chooses the single field; a
   reviewer who disagrees can take the second field without changing any rung.
2. **Is `MovementResolution == Blocked` the right pressure proxy for M4?** It is
   free and already authoritative, but it fires as readily on ally congestion as
   on enemy pressure. The alternative is a per-agent enemy count inside a
   pressure radius, which is an O(n^2) scan unless it borrows the collision
   uniform grid.
3. **Are the periods right?** Eight ticks for a slip and twelve for a give-ground
   are legibility guesses at 20 ticks per second. They are the first knobs
   calibration should turn, and they move no hash of any earlier preset.
4. **Should M3 fire at all under a melee-only roster?** It costs one comparison
   against a zero-length pool, so it is free, but it also means the rung ships
   almost entirely untested by the shipped workload. The sixth gate block pairs
   V14 with `PrecolonialPhilippinesV5` precisely so that a ranged roster
   exercises it.
5. **Should the give-ground lean suppression be a rendering rule or a pose
   rule?** Suppressing the torso lean when `EvasiveAction == GiveGround` fixes the
   "reads as rout" defect, but it puts a simulation enum into the pose resolver.
   The alternative is a dedicated presentation flag, which is one more thing to
   keep in sync.
6. **Do the four mechanics need per-loadout variation eventually?** A spearman
   and an axeman weave differently. Doing that properly means
   `usesEquipmentRelativeFootwork` and the whole V6 pipeline, which section 2
   defers. Nothing here forecloses it; V15 could be the preset that turns it on.
