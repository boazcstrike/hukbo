# Ranged Attacks — What `Hukbo.Core` Actually Does Today

**Date:** 2026-08-07
**Status:** Research. Read-only survey of the existing simulation. This document
does not authorize implementation and does not propose a design.
**Scope:** Everything a planner needs in order to design projectile flight time,
line of sight, and friendly fire without guessing at the current mechanics.

All file paths are relative to the repository root. Line numbers are from the
`ranged-units` worktree at
`C:\Users\boazs\webdev\autonomous-arena\.claude\worktrees\ranged-units`, whose
`src/` tree matches `main` at the time of writing.

> **Discovery-tool note.** `CLAUDE.md` section 8 requires the `tokensave` MCP
> tools for code discovery. Neither the `tokensave` server nor
> `codebase-memory-mcp` was exposed to this research session's tool set; two
> explicit tool searches returned nothing. Every claim below therefore comes
> from reading the files directly with `Read` and `Grep`, and every claim
> carries a `file:line` citation so it can be checked.

## Contents

1. [Tick stage order](#1-tick-stage-order)
2. [Attack resolution, end to end](#2-attack-resolution-end-to-end)
3. [How reach works today](#3-how-reach-works-today)
4. [The defensive resolution contract as implemented](#4-the-defensive-resolution-contract-as-implemented)
5. [Determinism surface](#5-determinism-surface)
6. [Spatial query — the uniform grid](#6-spatial-query--the-uniform-grid)
7. [What does not exist that a ranged weapon needs](#7-what-does-not-exist-that-a-ranged-weapon-needs)
8. [Per-tick allocation budget](#8-per-tick-allocation-budget)
9. [Existing tests that constrain a ranged change](#9-existing-tests-that-constrain-a-ranged-change)

## 1. Tick stage order

There is exactly one tick entry point: `BattleSimulation.AdvanceOneTick()` at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:599`. It returns immediately
without advancing anything once `Outcome != BattleOutcome.Ongoing`
(`BattleSimulation.cs:601-605`), increments the integer tick with `checked`
arithmetic at `BattleSimulation.cs:607`, then selects one of two event buffers
so a caller still holding the previous tick's `LastEvents` keeps seeing
unchanged data (`BattleSimulation.cs:613-614`).

The stages then run in this fixed order. Three of them are conditional on
`_movementRules.UsesEquipmentRelativeFootwork`, which is the V6 and later
movement profile flag; under the older presets those three stages do not run at
all.

| # | Stage | Runs when | Called at | Method defined at | What it does |
| --- | --- | --- | --- | --- | --- |
| 1 | `DecrementCooldowns` | always | `BattleSimulation.cs:616` | `BattleSimulation.cs:941` | Decrements every living agent's `AttackCooldownTicks` by one, floored at zero. |
| 2 | `SelectTargetsAndIntents` | always | `BattleSimulation.cs:617` | `BattleSimulation.cs:952` | Picks each agent's target and sets `AgentIntent`. This is the stage that decides *who* an agent will attack or approach. |
| 3 | `ResolveContingentStates` | always | `BattleSimulation.cs:618` | `BattleSimulation.cs:1221` | Advances per-contingent formation state (forming, advancing, engaged, regrouping). |
| 4 | `ResolveEquipmentPosturesAndProvisionalFootwork` | `UsesEquipmentRelativeFootwork` | `BattleSimulation.cs:624` | `BattleSimulation.cs:1613` | Equipment posture plus the provisional footwork phase, inserted between contingent state and proposal gathering. |
| 5 | `GatherMovementProposals` | always | `BattleSimulation.cs:627` | `BattleSimulation.cs:1500` | Every agent proposes a destination. Nothing is committed here; the stage documents a "no peeking" invariant so no proposal may read another agent's proposal. |
| 6 | `ResolveFriendlyClearanceConflicts` | `UsesEquipmentRelativeFootwork` | `BattleSimulation.cs:634` | `BattleSimulation.cs:2718` | Runs after every proposal exists and before anything is committed, so the no-peeking invariant of stage 5 survives. |
| 7 | `ResolveCollisions` | always | `BattleSimulation.cs:637` | `BattleSimulation.cs:3386` | Hands the proposals to `CollisionResolver`, which rebuilds the uniform grid and produces final positions. |
| 8 | `CommitMovement` | always | `BattleSimulation.cs:638` | `BattleSimulation.cs:3425` | Writes the resolved positions into agent state and emits movement-related events. |
| 9 | `MeasureCollision` | always | `BattleSimulation.cs:639` | `BattleSimulation.cs:3494` | Collects `CollisionMetrics` for diagnostics. Metrics are derived and never enter a snapshot. |
| 10 | `GatherAndCommitAttacks` | always | `BattleSimulation.cs:640` | `BattleSimulation.cs:3579` | The whole of attack resolution: gather, defend, damage, kill, emit. Walked in full in section 2. |
| 11 | `ApplyEquipmentAttackFootworkAndDeathCleanup` | `UsesEquipmentRelativeFootwork` | `BattleSimulation.cs:646` | `BattleSimulation.cs:2797` | Surviving accepted attackers enter Commit posture; agents killed by the exchange just gathered take death cleanup. Runs before any outcome, hash, or snapshot work. |
| 12 | `ResolveOutcome` | always | `BattleSimulation.cs:649` | `BattleSimulation.cs:3981` | Decides victory, draw, or ongoing, and emits the terminal event. |
| 13 | `UpdateViews` | always | `BattleSimulation.cs:651` | `BattleSimulation.cs:4260` | Projects `AgentState` into the read-only `AgentView` array the Client consumes. |

After stage 13 the tick swaps the event buffer if anything was emitted
(`BattleSimulation.cs:652-660`); a tick with no events exposes the shared
`EmptyEvents` instance instead, so an empty tick allocates nothing.

The two significant facts for a ranged design are that **movement is fully
committed before any attack is gathered** (stage 8 precedes stage 10), and that
**there is no post-attack stage other than footwork cleanup and outcome**. A
projectile that has to persist across ticks has no stage of its own today, and
any stage added for it changes the fixed order that section 4 of
`SIMULATION-GAME-STANDARDS.md` pins.

## 2. Attack resolution, end to end

Attack resolution begins two stages before the attack stage itself, because
target selection is stage 2 and attack gathering is stage 10. Nothing between
them re-selects a target. The full walk, in call order, is below.

### 2.1 Target selection — `SelectTargetsAndIntents`

1. `BattleSimulation.SelectTargetsAndIntents` — `BattleSimulation.cs:952`. It
   first calls `ComputeRallyAgents` (`BattleSimulation.cs:954`, defined at
   `BattleSimulation.cs:1140`) so the per-faction living count and the lowest
   living entity ID are known before any intent is assigned.
2. The outer loop walks `_agentStates` by index (`BattleSimulation.cs:979`). A
   dead agent has its target cleared and its intent set to `AgentIntent.Dead`
   (`BattleSimulation.cs:982-986`).
3. Perception is squared once per agent from `agent.PerceptionRangeRaw`
   (`BattleSimulation.cs:1010-1011`).
4. The inner loop walks the whole `_agentStates` array again
   (`BattleSimulation.cs:1013`). **This is an O(n²) scan; there is no spatial
   acceleration on target selection at all.** Same-faction and dead candidates
   are skipped at `BattleSimulation.cs:1015`.
5. Two cheap axis-aligned rejections on `deltaX` and `deltaY` run before the
   squared-distance test (`BattleSimulation.cs:1050-1061`), then
   `SquaredDistance` (`BattleSimulation.cs:1063`, defined at
   `BattleSimulation.cs:4141`) and the perception comparison
   (`BattleSimulation.cs:1064`).
6. The nearest surviving candidate wins; ties break on the lower `EntityId`
   (`BattleSimulation.cs:1082-1089`). This is the total order that
   `SIMULATION-GAME-STANDARDS.md` section 4 requires.
7. The chosen ID is written to `agent.TargetEntityId`
   (`BattleSimulation.cs:1092`). No target is ever cached across ticks — a
   fresh scan runs every tick, which is what CLAUDE.md section 9 means by "do
   not cache targets".
8. Intent is `Attacking` when the target is already inside
   `CollisionGeometry.ContactSquaredDistance(Scenario.BodyRadiusRaw)`, and
   `Moving` otherwise (`BattleSimulation.cs:1112-1115`). Note that this is a
   **contact** test on body radii, not a reach test: an agent keeps advancing
   until bodies touch even when the target is already within weapon reach. The
   comment at `BattleSimulation.cs:1107-1111` states this deliberately.
9. `Regrouping` may override `Moving` under the last-stand threshold
   (`BattleSimulation.cs:1121-1128`).

### 2.2 Attack gathering, defence, damage, death — `GatherAndCommitAttacks`

`BattleSimulation.GatherAndCommitAttacks(List<BattleEvent> events)` is defined
at `BattleSimulation.cs:3579` and is called from the tick at
`BattleSimulation.cs:640`. It runs four sequential passes over agent-indexed
arrays. The passes are separate on purpose: every attack in a tick is resolved
against the pre-tick hit points, so two attackers who together kill a defender
both get their blow recorded.

**Pass A — gather (`BattleSimulation.cs:3610-3727`).**

| Order | What happens | Line |
| --- | --- | --- |
| A1 | `Array.Clear(_damageTotals)` | `BattleSimulation.cs:3581` |
| A2 | Under V6+, `Array.Clear(_attackAcceptedThisTick)` — reusable scratch, never hashed or snapshotted | `BattleSimulation.cs:3590` |
| A3 | Six `Span<int>` counters are `stackalloc`-ed, indexed by faction | `BattleSimulation.cs:3603-3608` |
| A4 | Dead source skipped | `BattleSimulation.cs:3615-3618` |
| A5 | No target ⇒ `ClearActiveComboChain(source)` (defined at `BattleSimulation.cs:930`) and skip | `BattleSimulation.cs:3625-3629` |
| A6 | Target dead ⇒ clear chain and skip | `BattleSimulation.cs:3631-3636` |
| A7 | `IsWithinAttackRange(source, target)` fails ⇒ clear chain and skip | `BattleSimulation.cs:3638-3642` |
| A8 | `source.AttackCooldownRemaining != 0` ⇒ skip, chain intact | `BattleSimulation.cs:3644-3647` |
| A9 | `source.Intent = AgentIntent.Attacking` | `BattleSimulation.cs:3653` |
| A10 | `HitLocationResolver.Resolve(...)` returns the `BodyPart` | `BattleSimulation.cs:3655-3662`, defined at `src/Hukbo.Core/Combat/HitLocationResolver.cs:25` |
| A11 | `ClashResolver.Resolve(...)` returns the `AttackResolution` | `BattleSimulation.cs:3670-3678`, defined at `src/Hukbo.Core/Combat/ClashResolver.cs:85` |
| A12 | `ResolveComboTransition(source, target, resolution)` mutates the chain state and writes the cooldown this blow earns, returning the chain position or `null` | `BattleSimulation.cs:3680`, defined at `BattleSimulation.cs:3852` |
| A13 | The five-tuple `(sourceIndex, targetIndex, hitLocation, resolution, comboPosition)` is buffered into `_attackProposals` | `BattleSimulation.cs:3682-3684` |
| A14 | Under V6+, `_attackAcceptedThisTick[sourceIndex] = true` | `BattleSimulation.cs:3692` |
| A15 | Only `AttackResolution.Landed` accumulates `source.DamagePerAttack` into `_damageTotals[targetIndex]`, with `checked` arithmetic | `BattleSimulation.cs:3698-3702` |
| A16 | The six per-faction counters are incremented, one branch per resolution | `BattleSimulation.cs:3706-3726` |

The precheck order at A5 through A8 is load-bearing and documented as such at
`BattleSimulation.cs:3620-3624`: the chain checks run **ahead** of the cooldown
check so an attacker still on cooldown discovers a dead or out-of-reach target
on the tick that becomes true.

**Metrics fold (`BattleSimulation.cs:3729-3759`).** `_lastTickCombatByFaction`
is built as two `CombatMetrics` values, `_lastTickCombat` is derived from their
sum, and a mismatch against `proposalCount` throws
`InvalidOperationException`. These are derived observability values, never
snapshotted.

**Pass B — attack events (`BattleSimulation.cs:3761-3779`).** One
`AddAttackEvent` call per buffered proposal, in `_attackProposals` order, which
is source-index order. Damage carried on the event is `source.DamagePerAttack`
for a `Landed` blow and `0` for every other resolution
(`BattleSimulation.cs:3770-3772`). `AddAttackEvent` is defined at
`BattleSimulation.cs:4183` and increments `_eventSequence` with `checked`
arithmetic before constructing `BattleEvent.Attack`.

**Pass C — damage application (`BattleSimulation.cs:3781-3798`).** Walks
`_damageTotals` by index, skips zeros, applies
`target.HitPoints = Math.Max(0, target.HitPoints - damage)`
(`BattleSimulation.cs:3790`), and emits one `BattleEventKind.Damage` event per
damaged agent through `AddEvent` (defined at `BattleSimulation.cs:4151`). Note
that a damage event's source and target are **both** the victim's entity ID
(`BattleSimulation.cs:3794-3795`), so the damage event does not identify who
dealt the blow; that information lives only on the attack events.

**Pass D — death (`BattleSimulation.cs:3800-3817`).** Walks the same array,
skips any agent that took no damage or is still alive, clears
`TargetEntityId`, sets `AgentIntent.Dead`, and emits `BattleEventKind.Death`.

### 2.3 Where randomness enters

Exactly two draws per accepted attack, both stateless FNV-1a mixes over the
same tuple with different domain tags, neither of which touches `SplitMix64`:

- `HitLocationResolver.MixAttack` folds `HKBO_HIT` (`0x484B424F5F484954`), the
  seed, the tick, the source ID, the target ID, and the attacker's weapon —
  five words after the tag (`HitLocationResolver.cs:87-102`).
- `ClashResolver.MixClash` folds `HKBO_CLS` (`0x484B424F5F434C53`), the seed,
  the tick, the source ID, the target ID, the attacker weapon, the defender
  weapon, and the defender shield — seven words after the tag
  (`ClashResolver.cs:53-72`).

Because both are pure functions of the tuple, **nothing about attack resolution
consumes a sequential RNG stream**. There is no cursor to advance and no
ordering hazard between attacks in the same tick. That is the single most
important property for a ranged design: a projectile resolved on a later tick
than it was launched can reproduce the same roll by folding the launch tick
rather than the impact tick, with no stream bookkeeping at all.

## 3. How reach works today

### 3.1 The single approved test

`BattleSimulation.IsWithinAttackRange` at `BattleSimulation.cs:4132` carries the
doc comment "The single approved reach test. Attack range is measured centre to
centre, never surface to surface, so intent selection and attack gathering
cannot disagree about who can strike whom."

```csharp
private static bool IsWithinAttackRange(
    AgentState source,
    long squaredDistance) =>
    squaredDistance <= checked(
        (long)source.AttackRangeRaw * source.AttackRangeRaw);
```

That is `BattleSimulation.cs:4135-4139`. The comparison is `<=`, so a target
at exactly reach is inside it. `SquaredDistance` at `BattleSimulation.cs:4141`
is a plain integer difference of the two raw centre coordinates, squared and
summed under `checked`. **No body radius is subtracted anywhere in the reach
test.** This matches `SIMULATION-GAME-STANDARDS.md` section 13's "Attack reach
stays centre-to-centre" clause.

Only the **attacker's** `AttackRangeRaw` matters. The defender's reach is never
read by the test, so reach is not symmetric and a longer-reach weapon can strike
a shorter-reach one that cannot answer.

### 3.2 Units

`AttackRangeRaw` is a raw fixed-point value: world units multiplied by
`FixedPoint.Scale`, which is `1_024`
(`src/Hukbo.Core/Mathematics/FixedPoint.cs:8`, and the parameter documentation
at `src/Hukbo.Core/Combat/WeaponProfile.cs:25-31`). The scenario default is
`12 * FixedPoint.Scale`, that is twelve world units
(`src/Hukbo.Core/Simulation/Scenario.cs:32`). The scenario's default perception
range is `2_048 * FixedPoint.Scale`
(`src/Hukbo.Core/Simulation/Scenario.cs:34`), roughly 170 times reach, which is
why every agent finds a target on tick 1 in practice.

The Client renders reach in whole world units by dividing the raw value by the
scale: `src/Hukbo.Client/UI/AgentInspectorContent.cs:637` shows
`profile.AttackRangeRaw / FixedPoint.Scale` followed by the word `reach`.

### 3.3 Where reach comes from at agent-construction time

`BattleSimulation.CreateAgent` at `BattleSimulation.cs:857` takes both the
scenario-wide `scenario.AttackRangeRaw` (`BattleSimulation.cs:871`) and the
per-weapon `profile.AttackRangeRaw` (`BattleSimulation.cs:886`), and stores the
resolved value on `AgentState.AttackRangeRaw`
(`src/Hukbo.Core/Simulation/AgentState.cs:55`, property at
`src/Hukbo.Core/Simulation/AgentState.cs:82`). The property is `internal`, which
is why measurement harnesses have to live inside the test project rather than in
`Hukbo.Headless`.

### 3.4 Validation, and what the minimum means

Three separate validations bound reach.

**Per-profile positivity.** `WeaponProfile.Validate` rejects a non-positive
`AttackRangeRaw` at `src/Hukbo.Core/Combat/WeaponProfile.cs:80-82`.

**The reach floor.** `CombatRuleset.MinimumProfileReachRawExclusive` is declared
at `src/Hukbo.Core/Combat/CombatRuleset.cs:54-55` as
`2 * CollisionRules.DefaultBodyRadiusRaw`. `ValidateProfileReach`
(`src/Hukbo.Core/Combat/CombatRuleset.cs:487`) throws
`ArgumentOutOfRangeException` when `profile.AttackRangeRaw <=
MinimumProfileReachRawExclusive` (`src/Hukbo.Core/Combat/CombatRuleset.cs:492`).
It is called once for the solo profile
(`src/Hukbo.Core/Combat/CombatRuleset.cs:461`) and once for the paired profile
of a one-handed weapon (`src/Hukbo.Core/Combat/CombatRuleset.cs:482`).

The doc comment at `src/Hukbo.Core/Combat/CombatRuleset.cs:43-53` explains the
meaning precisely: `BuildMovementProposal` stops an advancing warrior at two
body radii so opposing front ranks make body contact rather than halting at
weapon reach. A profile whose reach is at or below that distance therefore
produces a warrior who advances into contact and can then never satisfy
`IsWithinAttackRange`. The floor is a **liveness** guarantee, not a balance
knob. It is asserted per profile rather than per weapon because every one-handed
weapon's paired reach is shorter than its solo reach.

Nothing anywhere imposes a reach **ceiling**. A ranged weapon that declared, say,
`400 * FixedPoint.Scale` would pass every existing validation and would simply
work, striking through allies and through the enemy front rank, because no code
looks at what lies between the two centres.

**The scenario-level relations.** `Scenario.Validate` requires
`PerceptionRangeRaw >= AttackRangeRaw`
(`src/Hukbo.Core/Simulation/Scenario.cs:306-309`) and requires the body diameter
not to exceed `AttackRangeRaw` (`src/Hukbo.Core/Simulation/Scenario.cs:401`),
plus generic raw-world-value bounds on both
(`src/Hukbo.Core/Simulation/Scenario.cs:243-244`). A ranged weapon with reach
above the scenario perception range would still never fire, because target
selection filters on perception first (`BattleSimulation.cs:1064`) and attack
gathering only ever considers `source.TargetEntityId`
(`BattleSimulation.cs:3625`).

## 4. The defensive resolution contract as implemented

### 4.1 Where it runs

`SIMULATION-GAME-STANDARDS.md:798-803` states the stage: defensive resolution
runs inside `GatherAndCommitAttacks`, immediately after an attack has passed the
reach and cooldown gates and after `HitLocationResolver.Resolve` has chosen the
struck body part, and before damage is applied. The code matches: the clash call
is at `BattleSimulation.cs:3670`, the hit-location call at
`BattleSimulation.cs:3655`, the reach gate at `BattleSimulation.cs:3638` and the
cooldown gate at `BattleSimulation.cs:3644`, with damage accumulation only at
`BattleSimulation.cs:3698-3702`.

### 4.2 The five outcomes

`AttackResolution` is declared at `src/Hukbo.Core/Combat/AttackResolution.cs:24`
with five pinned numeric values.

| Value | Name | Declared at | Decided at | Damage? |
| --- | --- | --- | --- | --- |
| `0` | `Landed` | `AttackResolution.cs:29` | `ClashResolver.cs:141` — the fall-through return after all four intervals are stepped over | yes, `BattleSimulation.cs:3700-3701` |
| `1` | `ShieldBlocked` | `AttackResolution.cs:35` | `ClashResolver.cs:120`, when `roll < shield` | no |
| `2` | `Parried` | `AttackResolution.cs:40` | `ClashResolver.cs:126`, when `roll < shield + hard` | no |
| `3` | `Deflected` | `AttackResolution.cs:45` | `ClashResolver.cs:132`, when `roll < shield + hard + soft` | no |
| `4` | `Evaded` | `AttackResolution.cs:51` | `ClashResolver.cs:138`, when `roll < shield + hard + soft + void` | no |

The interval walk is at `ClashResolver.cs:117-141`. Every comparison is strictly
lower-exclusive so that a zero-width channel is stepped over rather than
selected; the comment at `ClashResolver.cs:111-116` gives the concrete failure
that motivates this — with `ShieldId.None` the shield interval is `[0, 0)`, and
a `roll <= cumulative` form would block a roll of zero with a shield the warrior
does not carry. `SIMULATION-GAME-STANDARDS.md:852-853` states the same rule.

The type's own doc comment at `AttackResolution.cs:10-14` is explicit that the
numeric values are part of the deterministic replay contract, because the
resolution is carried on every attack event and folded into the headless event
hash: renumbering or reordering requires a new combat preset version plus new
golden expectations.

### 4.3 Channel composition

`ClashResolver.ComputeChannels` at `ClashResolver.cs:198` produces the three
channels plus the hard/soft split, in six documented steps:

1. `profile.ResolveShieldIntercept(defenderShield)` — `ClashResolver.cs:207`.
2. `profile.ResolveWeaponIntercept(defenderWeapon, defenderShield,
   attackerWeapon)` — `ClashResolver.cs:208`. The three-part key is documented at
   `src/Hukbo.Core/Combat/ClashProfile.cs:59-68`; the defender's shield joined
   the key at preset V2 because Kalis and Itak each field both a solo and a
   shield-paired loadout.
3. `profile.ResolveVoid(defenderWeapon, defenderShield)` — `ClashResolver.cs:209`.
4. If the three sum above `profile.MaximumInterceptionBasisPoints`, each is
   rescaled independently, and each division truncates toward zero, so the
   residue becomes additional `Landed` probability
   (`ClashResolver.cs:211-223`).
5. and 6. `SplitWeaponChannel` (`ClashResolver.cs:156`) computes the clamped hard
   share from a per-attacker base times a per-defender multiplier over
   `ClashProfile.HardShareMultiplierScale` (declared `1_000` at
   `ClashProfile.cs:53`), clamped between the profile's minimum and maximum
   (`ClashResolver.cs:165-172`), then takes `soft` as the remainder rather than a
   second product so `hard + soft` is exactly the channel
   (`ClashResolver.cs:177-178`).

`ClashProfile.BasisPointScale` is `10_000` (`ClashProfile.cs:47`). All arithmetic
is integer basis points with `long` intermediates; the type remarks at
`ClashResolver.cs:14-18` state that no fixed-point and no floating-point value
enters this path.

### 4.4 How the roll is drawn and ordered

`ClashResolver.MixClash` at `ClashResolver.cs:53` is a pure FNV-1a fold, not a
draw from a stream. Eight words are folded in a fixed order
(`ClashResolver.cs:62-71`):

| Order | Word | Line |
| --- | --- | --- |
| 1 | `ClashTag` = `0x484B424F5F434C53` (ASCII `HKBO_CLS`) | `ClashResolver.cs:29`, folded at `:63` |
| 2 | `seed` | `ClashResolver.cs:64` |
| 3 | `tick` (unchecked cast to `ulong`) | `ClashResolver.cs:65` |
| 4 | `sourceEntityId` | `ClashResolver.cs:66` |
| 5 | `targetEntityId` | `ClashResolver.cs:67` |
| 6 | `attackerWeapon` as its declared numeric value | `ClashResolver.cs:68` |
| 7 | `defenderWeapon` | `ClashResolver.cs:69` |
| 8 | `defenderShield` | `ClashResolver.cs:70` |

The result is `hash % ClashProfile.BasisPointScale`
(`ClashResolver.cs:71`), so the roll lives in `[0, 10_000)`. The comment at
`ClashResolver.cs:44-52` records that seven single-word isolation cases pin the
dependence on each word, because dropping or reordering a word is invisible to
any distribution test.

**There is no ordering between attacks.** Because the roll is a pure function of
the tuple, the order in which attacks are gathered inside a tick cannot change
any resolution, and adding a new draw elsewhere in the simulation cannot shift
this one. `SIMULATION-GAME-STANDARDS.md:828-831` records the evidence: a
zero-interception control run reproduced the pre-change event stream and state
hash tick for tick.

`Hukbo.Core/Determinism/SplitMix64.cs` — 45 lines — is **not** used by any part
of attack resolution. Its callers are elsewhere; the clash and hit-location paths
use `Fnv1a` (`src/Hukbo.Core/Determinism/Fnv1a.cs`, 30 lines) only.

### 4.5 The domain-tag inventory

`SIMULATION-GAME-STANDARDS.md:833-840` is explicit that every domain tag is a
fresh, distinct 64-bit ASCII constant folded first into its own keyed roll, and
that the paragraph is "the inventory a new domain tag is checked against before
it is minted". The tags it lists are `HKBO_CLS`, `HKBO_HIT`, the last-stand
jitter's `LastStandTag` (`0x484B424F5F4C5354`), the collision-priority key's own
tag, and `HKBO_CTG` (`0x484B424F5F435447`) for `ContingentOffset.Compute`.

**A ranged design that needs its own roll — a flight-deviation roll, a
friendly-fire interception roll, a miss-scatter roll — must mint a new tag and
add it to that paragraph.** Reusing `HKBO_CLS` would correlate the two draws.

### 4.6 The two enforced acceptance bands

`SIMULATION-GAME-STANDARDS.md:855-861` names the only enforced band on the
defensive tables: `CombatMetrics.DefenceAttributableShare`, that is
`(ShieldBlocked + Parried + Deflected + Evaded) / AcceptedAttacks`, must land
inside 0.25 to 0.45 across seeds 1 through 20 at 200 agents. The type is
`src/Hukbo.Core/Simulation/CombatMetrics.cs`.

`SIMULATION-GAME-STANDARDS.md:863-868` names the termination criterion: at least
19 of 20 seeds must reach a decisive outcome before the 5,000-tick cap, with a
median decisive tick at or below 5,000.

Both bands are aggregate over accepted attacks. **A ranged weapon changes the
denominator of the first band**, because every projectile that reaches a
defender is an accepted attack, and it changes the numerator too if a projectile
can be blocked by a shield. A ranged design that leaves these bands unexamined
has skipped the only quantitative gate the combat contract enforces.

## 5. Determinism surface

### 5.1 The frozen, numbered enums

Each of these carries an explicit doc comment saying its numeric values are part
of the deterministic replay or content-hash contract.

| Enum | File and line | Values today | Note |
| --- | --- | --- | --- |
| `WeaponId` | `src/Hukbo.Core/Combat/CombatIdentity.cs:14` | `Kampilan = 1`, `Wasay = 2`, `Kalis = 3`, `Itak = 4` | Numbering starts at 1 deliberately; `BattleEvent` relies on a nonzero weapon byte to distinguish "has combat context" from "has none" (`src/Hukbo.Core/Simulation/BattleEvent.cs:98-105`). |
| `WeaponGrip` | `CombatIdentity.cs:52` | `TwoHanded = 1`, `OneHanded = 2` | Static configuration; never drawn from, never written to agent state. |
| `ArmorId` | `CombatIdentity.cs:72` | `LightOrganic = 1` | One value only. |
| `ShieldId` | `CombatIdentity.cs:81` | `None = 1`, `TallHardwood = 2` | `None` is `1`, not `0`. |
| `CombatPresetId` | `CombatIdentity.cs:93` | V1 through V4 | Doc comment: "A new ruleset requires a new value plus a new `CombatPresetRegistry` entry." |
| `RankId` | `CombatIdentity.cs:136` | `Datu = 1` through `Ayuey = 5` | |
| `AttackResolution` | `src/Hukbo.Core/Combat/AttackResolution.cs:24` | `Landed = 0` through `Evaded = 4` | |
| `BattleEventKind` | `src/Hukbo.Core/Simulation/BattleEvent.cs:5` | `Move = 0`, `Attack = 1`, `Damage = 2`, `Death = 3`, `Outcome = 4` | Folded into the event hash as its numeric value. |
| `BodyPart` | `src/Hukbo.Core/Combat/BodyPart.cs` | thirteen parts, walked in `BodyPartCatalog.Ordered` order | The walk order at `HitLocationResolver.cs:69-76` is the total order. |
| `AgentIntent` | `src/Hukbo.Core/Simulation/AgentIntent.cs` | folded per agent at `StateHasher.cs:125` | |
| `BattleOutcome` | `src/Hukbo.Core/Simulation/BattleOutcome.cs` | folded at `StateHasher.cs:106` | |

### 5.2 What the state hash covers

`StateHasher.Compute` at `src/Hukbo.Core/Determinism/StateHasher.cs:70` is a
single FNV-1a fold. The reachable entry point is
`BattleSimulation.ComputeStateHash` at `BattleSimulation.cs:663` and its internal
overload at `BattleSimulation.cs:677`.

**Scenario section, in order (`StateHasher.cs:82-103`):** seed, map width, map
height, agents per faction, tick rate, tick limit, maximum hit points, damage per
attack, `AttackRangeRaw`, `PerceptionRangeRaw`, `MovementSpeedRaw`, attack
cooldown ticks, `BodyRadiusRaw`, collision policy, last-stand threshold, combat
preset ID, movement preset ID, the ruleset content hash, and — only when the
movement preset uses equipment-relative footwork — the movement content hash.

**Then (`StateHasher.cs:105-108`):** tick, outcome, event sequence, agent count.

**Per agent, in `_agentStates` storage order (`StateHasher.cs:110-156`):** entity
ID, faction ID, `XRaw`, `YRaw`, hit points, maximum hit points,
`MovementSpeedRaw`, `PerceptionRangeRaw`, `AttackRangeRaw`, `DamagePerAttack`,
`AttackCooldownTicks`, `AttackCooldownRemaining`, `TargetEntityId ?? 0`, intent,
movement resolution, loadout weapon, loadout armor, loadout shield, level, combo
steps remaining, combo target entity ID, contingent ID, contingent state. Then
three conditional tails: rank when `hasRankLevels`; the five footwork fields when
`movementContentHash` is non-null; the three pressure-interrupt fields when
`appliesPressureInterrupt`.

The conditional-tail pattern is the codebase's established mechanism for adding
state without moving an older preset's frozen digest, and the doc comments at
`StateHasher.cs:22-31`, `:32-51`, and `:52-69` all spell out the reasoning. A
ranged design that adds per-agent state (an ammunition counter, a nocked-arrow
flag, a draw timer) should follow this same pattern: a new gate, not a reuse of
an existing one. `StateHasher.cs:52-59` is explicit that reusing an existing gate
would move an older preset's per-agent byte layout.

**What the state hash does not cover.** Nothing in `_attackProposals`,
`_damageTotals`, `_attackAcceptedThisTick`, `_pressureBasisPoints`,
`_localMovementContexts`, `CombatMetrics`, `FactionCombatMetrics`, or
`CollisionMetrics`. Those are derived scratch. `DeterminismTests.cs:415`
(`CombatMetrics_ReachesNeitherHash`) pins that for the metrics.

### 5.3 What the event hash covers

The event hash lives in the headless runner, not in Core:
`HeadlessRunner.AddEventToHash` at `src/Hukbo.Headless/HeadlessRunner.cs:819`.
It folds twelve words per event, in order (`HeadlessRunner.cs:823-873`):
sequence, tick, kind, source entity ID, `TargetEntityId ?? 0`, value, faction ID,
weapon, shield, hit location, resolution, combo position. The last five use
"absent means `ulong.MaxValue`" as the sentinel, so a non-attack event stays
distinct from any defined value. The running hash starts at `Fnv1a.OffsetBasis`
(`HeadlessRunner.cs:383`) and each event is folded at
`HeadlessRunner.cs:511`.

`BattleEvent` itself packs five of those fields into one `long`
(`BattleEvent.cs:117`), at shifts `ResolutionShift = 24`, `WeaponShift = 16`,
`ShieldShift = 8`, hit location at bits 0-7, and `ComboPositionShift = 32`
(`BattleEvent.cs:63-74`). The remarks at `BattleEvent.cs:84-90` state why: five
separate nullable fields would cost eight bytes each and "would be the bulk of
per-tick allocation, which `RepeatedCollisionTicksHaveBoundedAllocations`
budgets." **Bits 40 through 63 of `_combatContext` are unused today**, which is
where a small ranged field could go without growing the event.

`BattleEvent.Attack` (`BattleEvent.cs:223`) validates that the weapon, shield,
hit location, and resolution are all `Enum.IsDefined`
(`BattleEvent.cs:244-274`). `BattleEvent.NonAttack` (`BattleEvent.cs:309`)
refuses `BattleEventKind.Attack` outright (`BattleEvent.cs:318-323`) and forces
all combat context to null.

### 5.4 Where a ranged change would break a golden expectation

Every item below is a real pinned assertion in the test suite.

| Change | What breaks | Where |
| --- | --- | --- |
| Add a `WeaponId` value | Nothing by itself — the enum is appended-only and no existing fold changes. But a preset that **fields** it changes that preset's roster, and therefore its content hash. | `CombatIdentity.cs:14` |
| Add a `BattleEventKind` value | Nothing by itself. Emitting one changes the event stream and the event hash for every seed on which it fires. | `BattleEvent.cs:5` |
| Add an `AttackResolution` value | Renumbering breaks; appending a `5` does not move any existing fold, but the packed byte at bits 24-31 has room and the interval walk at `ClashResolver.cs:117-141` would need a new interval, which moves every roll's outcome. | `AttackResolution.cs:24` |
| Change any V1 table | `PhilippinePresetV1_ContentHashStaysAtTheFrozenGoldenValue` fails: `0x59FB4CA563D87A49` | `tests/Hukbo.Core.Tests/CombatConfigurationTests.cs:167,176` |
| Change any V2 table | `PresetV2ContentHash_IsPinnedAndDistinctFromV1` fails: `0x10AB1CC226AB3636` | `tests/Hukbo.Core.Tests/DeterminismTests.cs:139,154` |
| Change any V3 table | `PresetV3ContentHash_IsPinnedAndDistinctFromV1AndV2` fails: `0xCD790E489293B304` | `DeterminismTests.cs:171,177` |
| Change any V4 table | `PresetV4ContentHash_IsPinnedAndDistinctFromV1V2AndV3` fails: `0x4E3E4F8C0A3822E0` | `DeterminismTests.cs:192,199` |
| Change any per-agent simulation behaviour | `PresetV4_SeedOneStateAndEventHashArePinned` fails: state hash `2BBEDD668CC38FD6`, event hash `228818712E5AE6C6`, at 20 agents / 200 ticks / seed 1 | `DeterminismTests.cs:215,242-243` |
| The same for V3 | `PresetV3_SeedOneStateAndEventHashArePinned` | `DeterminismTests.cs:260` |
| The same for the persistent-contingent movement preset | `PersistentContingentsV2_SeedOneStateAndEventHashArePinned` | `DeterminismTests.cs:343` |
| Any change to the fold that moves a metric into a hash | `CombatMetrics_ReachesNeitherHash` fails | `DeterminismTests.cs:415` |
| A movement-preset digest move | `MovementPresetFreezeTests` | `tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs` |
| A hash-neutrality claim about a new stage | `ZeroInterceptionProfile_ReproducesThePreClashDigest` and `ZeroInterceptionProfile_ReproducesTheRecordedStateHash` — the pattern for proving a new stage adds nothing when disabled | `DeterminismTests.cs:927`, `:997`, fixture `tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-preclash-digest.json` |

`DeterminismTests.cs:24` also holds `PreClashContentHash =
0x59FB4CA563D87A49UL` and `DeterminismTests.cs:57` holds
`PreClashTerminalStateHash = 0xAE3BEC9EE7BCEDFCUL`, with a comment block at
`DeterminismTests.cs:30-56` recording three superseded values and the reason each
moved. That comment block is the template for documenting a legitimate hash
move.

### 5.5 What a "new preset version" actually requires — the V3-to-V4 example

Preset V4 is the most recent real instance and shows the full cost. It added a
`RankId` to each of the four solo roster entries and a per-rank fighter-level
table.

1. **A new enum value.** `CombatPresetId.PrecolonialPhilippinesV4 = 4`
   (`CombatIdentity.cs:122`), with a doc comment naming exactly what it changes
   relative to V3 and stating that the earlier versions stay registered and
   unmodified so their replays remain reproducible.
2. **A new registry arm, in two switches.**
   `CombatPresetRegistry.IsRegistered` and `CombatPresetRegistry.Get`
   (`src/Hukbo.Core/Combat/CombatPresetRegistry.cs`), which throws
   `ArgumentOutOfRangeException` for an unregistered value rather than falling
   back.
3. **A whole new preset file, with every value restated rather than
   referenced.** `src/Hukbo.Core/Combat/PhilippineCombatPresetV4.cs`, 347 lines.
   The remarks at `PhilippineCombatPresetV4.cs:14-17` state the rule directly:
   "Version 4 is a frozen snapshot in the same sense versions 1 through 3 are:
   version 3's values are restated here rather than referenced, so retuning
   version 4 can never reach back and move a hash version 3's replays depend
   on." The comment at `:75-76` confirms the target weights are "restated
   exactly from `PhilippineCombatPresetV3`."
4. **A new pinned content hash test.**
   `PresetV4ContentHash_IsPinnedAndDistinctFromV1V2AndV3`
   (`DeterminismTests.cs:192-203`) asserts the literal `0x4E3E4F8C0A3822E0` and
   asserts distinctness from all three predecessors.
5. **New pinned state and event hashes from a real captured run.**
   `PresetV4_SeedOneStateAndEventHashArePinned` (`DeterminismTests.cs:215-244`)
   runs the headless runner in-process at 20 agents / 200 ticks / seed 1, and
   the comment at `DeterminismTests.cs:238-241` records the exact command whose
   output was captured.
6. **A conditional hash fold, gated on a preset capability rather than on the
   preset ID.** V4 is the first preset to pass a non-null `rankLevels`, so
   `CombatRuleset.HasRankLevels` becomes true and `StateHasher.Compute` folds
   `agent.Rank` at `StateHasher.cs:136-139`. V1 through V3 fold nothing there,
   not even a constant, which is what keeps their hashes exactly where they
   were.
7. **A construction-time validation for the new data.**
   `CombatRuleset` requires a declared level for every rank the roster actually
   fields (`src/Hukbo.Core/Combat/CombatRuleset.cs:442-453`).

A ranged weapon would repeat every one of these seven steps as
`PrecolonialPhilippinesV5`.

## 6. Spatial query — the uniform grid

### 6.1 The file, and the three live instances

`CollisionUniformGrid` is `src/Hukbo.Core/Simulation/CollisionUniformGrid.cs`,
645 lines, declared `internal sealed class` at
`CollisionUniformGrid.cs:45`. Three instances exist in a running battle:

| Instance | Owner | Cell size | Purpose |
| --- | --- | --- | --- |
| `_committedGrid` | `CollisionResolver` | `2 * bodyRadiusRaw` — one body diameter | Bodies already committed this tick, queried as each mover is placed | `src/Hukbo.Core/Simulation/CollisionResolver.cs:163`, constructed at `:227` |
| `_pendingGrid` | `CollisionResolver` | `2 * bodyRadiusRaw` | Bodies not yet moved this tick | `CollisionResolver.cs:177`, constructed at `:228` |
| `CollisionScratch.Grid` | `BattleSimulation` metrics | `2 * ContactBandRadiusRaw` — a wider cell for the observability band | Contact-pair counting in `MeasureCollision` | `src/Hukbo.Core/Simulation/CollisionScratch.cs:92`, property at `:108` |

There is a fourth, transient instance built at spawn time only:
`BattleSimulation.cs:747` constructs one with cell size `stepRaw` for
`ResolveSpawnPlacement`.

### 6.2 Rebuild cadence

Every tick, from scratch. `CollisionResolver.Resolve` runs from
`BattleSimulation.ResolveCollisions` (`BattleSimulation.cs:3417`), and
`CollisionScratch.Grid.Rebuild` runs from `BattleSimulation.MeasureCollision`
(`BattleSimulation.cs:3507-3509`) against a body list rebuilt from committed
positions on the same lines (`BattleSimulation.cs:3496-3505`).

`CollisionUniformGrid.Rebuild` (`CollisionUniformGrid.cs:153`) clears, inserts
every living body, and generates pairs. `Clear`
(`CollisionUniformGrid.cs:172-178`) empties the dictionary and the pair list but
keeps every allocated buffer, so a warm tick allocates nothing — the class
remarks at `CollisionUniformGrid.cs:40-43` state this, and
`SIMULATION-GAME-STANDARDS.md:609-610` states it as contract.

Dead bodies are skipped on the way in (`CollisionUniformGrid.cs:196-199`), so
corpses cost nothing.

### 6.3 The queries it answers today

There are exactly four, all of them **point queries about one circular body at
one position**:

| Method | Line | Predicate | Semantics |
| --- | --- | --- | --- |
| `AnyContact` | `CollisionUniformGrid.cs:314` | `CollisionGeometry.IsContact` (inclusive; exact tangency counts) | Would a body of this radius at (x, y) touch or penetrate any indexed body other than the excluded one? |
| `AnyOverlap` | `CollisionUniformGrid.cs:379` | `CollisionGeometry.Overlaps` (strict; tangency is false) | Would it strictly penetrate? |
| `AnyOverlapUnchecked` | `CollisionUniformGrid.cs:402` | same, no argument validation | The resolver's inner loop, described at `CollisionUniformGrid.cs:391-395` as running "tens of millions of times a tick". |
| `AnyCoincident` / `AnyCoincidentUnchecked` | `CollisionUniformGrid.cs:452` / `:463` | `CollisionGeometry.IsCoincident` | Is any other body at exactly this point? |

Plus the bulk product, `Pairs` / `PairsList`
(`CollisionUniformGrid.cs:126`, `:135`): every unordered pair of living bodies
in contact after the last `Rebuild`, sorted by `CollisionPair.CompareTo`, each
pair present exactly once.

Every one of these walks the same fixed three-by-three neighbourhood,
`NeighbourOffsets` at `CollisionUniformGrid.cs:60-71`, in ascending offset Y then
ascending offset X. The comment at `CollisionUniformGrid.cs:52-59` explains why
three-by-three is sufficient: `ValidateBodyRadius` enforces a cell size of at
least one body diameter, so bodies two cells apart on an axis are separated by
strictly more than one diameter. `AnyCoincident`'s remarks at
`CollisionUniformGrid.cs:441-448` note that a coincident body maps to the same
cell so the centre cell alone would answer, but the full neighbourhood is walked
anyway "so that every query on this type shares one traversal shape and cannot
drift apart from the others."

### 6.4 Can it answer "is anything on the segment between A and B"?

**No, not as written.** Nothing on the type takes two endpoints. Every query
takes one centre and one radius, and the traversal is a fixed three-by-three
block around that one centre. A segment query needs a different traversal: the
cells the segment crosses, which for a long segment is not a bounded
neighbourhood.

Two ways it could be made to answer that question, with honest costs.

**Option A — a supercover / DDA cell walk.** Add a method that walks the cells
the segment `A→B` passes through, in a fixed deterministic order (ascending
parameter along the segment, ties broken by the same ascending-Y-then-X rule the
type already uses), and tests each indexed body in each visited cell against a
point-to-segment distance predicate. The cell size is one body diameter, `2 *
CollisionRules.DefaultBodyRadiusRaw = 8_704` raw units, that is 8.5 world units
(`src/Hukbo.Core/Simulation/CollisionRules.cs`, `DefaultBodyRadiusRaw = (17 *
FixedPoint.Scale) / 4`). A shot of `L` world units therefore crosses roughly
`L / 8.5` cells along its axis, plus the perpendicular spill of the supercover,
so call it `1.5 * L / 8.5` cells visited. At 200 agents over a 2,048-unit map the
occupancy is well under one body per cell on average, but a shot fired into a
melee crosses the densest cells in the battle, where occupancy is several bodies
per cell.

Concretely: a 100-world-unit shot visits on the order of 18 cells. If each holds
two bodies that is 36 distance tests per shot, each test being a
point-to-segment squared distance — roughly the same arithmetic cost as the
existing `IsContact` test plus one projection. For one archer per tick that is
nothing. For 100 archers all firing on the same tick, 3,600 tests, still cheap
compared with the resolver's own inner loop.

The real costs are not arithmetic:

- **A new deterministic traversal order to specify and test.** The type's whole
  contract (`CollisionUniformGrid.cs:12-14`) is that it produces exactly what an
  O(n²) scan would produce, in exactly one order, and that equivalence is the
  acceptance test (`SIMULATION-GAME-STANDARDS.md:600-602`). A segment query
  needs its own naive reference implementation in the test project alongside
  `NaiveCollisionPairs.cs`, `NaiveCollisionResolution.cs`, and
  `NaiveClashResolution.cs`.
- **A new "nearest along the segment" tie-break.** Line of sight usually wants
  the *first* blocker, not *any* blocker. Ordering by distance along the segment
  needs a total order with `EntityId` as the final tie-break, exactly as
  `SIMULATION-GAME-STANDARDS.md:132-133` requires.
- **Which grid, and what the grid's contract actually forbids.** The governing
  section is "The uniform grid is a derived oracle",
  `SIMULATION-GAME-STANDARDS.md:595-611`. It imposes exactly three things: the
  grid is a derived accelerator and never authoritative state, so it is **not
  hashed, not snapshotted, and not persisted** (`:597-598`); its only contract is
  that it **produces exactly what an O(n²) scan over the same bodies would
  produce, in exactly one order**, and that equivalence is the acceptance test
  (`:600-607`); and all its storage is preallocated and reused so a warm tick
  allocates nothing (`:609-610`). **It does not forbid a gameplay rule from
  reading the grid.** The collision resolver is a gameplay rule and queries the
  grid on every tick — that is what the grid is for. A line-of-sight query
  reading a grid is therefore permitted; what is not permitted is a grid result
  that is hashed, snapshotted, or produced in an order that is not total and
  deterministic.

  (An earlier draft of this document cited `SIMULATION-GAME-STANDARDS.md:626-629`
  — "The band is derived observability only. No rule consults it" — as a
  prohibition on consulting the grid. That sentence belongs to the following
  section, "Contact is measured over a proximity band" (`:612-629`), and it is
  about the **proximity band**, a metrics-only distance threshold, not about the
  grid. The prohibition it states is real but it applies to the band alone.)

  That still leaves a real question of which instance to use. `_committedGrid`
  and `_pendingGrid` are private to `CollisionResolver` and hold a partition of
  the bodies mid-resolution, so neither has a complete, settled body set at attack
  time. `CollisionScratch.Grid` is rebuilt in `MeasureCollision` (stage 9),
  immediately before `GatherAndCommitAttacks` (stage 10), and holds every living
  body at its committed position, so its contents are correct when an attack is
  gathered. Its cell size is `2 * ContactBandRadiusRaw`
  (`src/Hukbo.Core/Simulation/CollisionScratch.cs:90-92`), wider than a body
  diameter; that is legal for a query at body radius, since
  `ValidateBodyRadius` only requires the query diameter not to exceed the cell
  size, but a wider cell holds more bodies and so costs more per visited cell.
  Reusing it means one structure answering both an observability question and a
  gameplay question, which is a coupling worth naming in a design doc — if the
  metrics band is ever retuned, the cell size a line-of-sight query walks moves
  with it. A dedicated fourth grid avoids that coupling at the cost of a fourth
  rebuild per tick.

**Option B — no grid at all.** For a small number of shots per tick, an O(n)
scan over `_agentStates` with a point-to-segment test, cut down by the cheap
axis-aligned rejection already used at `BattleSimulation.cs:1050-1061`, is
simpler, needs no new spatial structure, needs no new traversal-order proof, and
costs 200 tests per shot at 200 agents. Target selection is already O(n²) over
the same array every single tick (`BattleSimulation.cs:1013`), so one extra O(n)
pass per shot is a rounding error against work the tick already does.

The recommendation rests on cost and on implementation risk, not on any
prohibition — as the corrected note in Option A records, a gameplay rule reading
the grid is permitted. The cost case is that
`docs/research/TICK-STAGE-PROFILE.md:107-121` puts `GatherAndCommitAttacks` at
2.35 % of the tick at 200 agents and 0.44 % at 2,000, while
`SelectTargetsAndIntents` — an O(n²) scan over the same array — costs 5.04 % and
16.67 % at those same counts. An O(n) segment scan per shot is a small fraction
of a stage that is already a small fraction of the tick, and it can be measured
against the existing benchmark without building anything new. The risk case is
that Option A owes the repository a second artifact before it can ship: a naive
segment oracle in the test project to sit beside `NaiveCollisionPairs.cs`,
`NaiveCollisionResolution.cs`, and `NaiveClashResolution.cs`, because
`SIMULATION-GAME-STANDARDS.md:600-602` makes O(n²) equivalence in exactly one
order the acceptance test for every grid query. Option B needs that oracle too —
it *is* the oracle — which is precisely why it is cheaper to start there. Reach
for the grid only if measurement on a real archer roster says the O(n) scan
costs enough to matter.

## 7. What does not exist that a ranged weapon needs

`SIMULATION-GAME-STANDARDS.md:183-184` says it plainly, for the weapon
definition: "Weapon definition: stable ID, range, cooldown ticks, integer
damage, optional integer hit chance. If hit chance is omitted, v0.1 attacks
always hit. **There is no ammunition or projectile model.**" That is still
true. CLAUDE.md section 9 also lists "projectile ammunition" among the things
not to start before the gate that authorizes them.

The list below is what a ranged weapon actually needs and does not have. For
each, the nearest existing thing to extend, and the file it lives in.

| # | Missing capability | Nearest existing thing | File to host it |
| --- | --- | --- | --- |
| 1 | **A projectile entity.** No type in `Hukbo.Core` represents anything that is not an agent. There is no second entity array, no non-agent `EntityId` allocator, and no lifecycle for a thing that spawns and expires. `_agentStates`, `_agentIndexes`, and `_agentViews` are all agent-shaped and sized once at construction. | `AgentState` is the only per-entity mutable class; `CollisionBody` (`readonly record struct` in the collision code) is the only lightweight positional value. | New type beside `src/Hukbo.Core/Simulation/AgentState.cs`; storage beside the arrays declared in `src/Hukbo.Core/Simulation/BattleSimulation.cs` |
| 2 | **Flight time.** Attack resolution is instantaneous within one tick: gather, resolve, damage, event, all in `GatherAndCommitAttacks` (`BattleSimulation.cs:3579-3818`). Nothing carries an effect from one tick to a later one. | `AgentState.AttackCooldownRemaining` (`AgentState.cs:90`) is the only per-agent tick countdown, and `FootworkTicksRemaining` (`AgentState.cs:203`) is the only phase timer. Both decrement in a dedicated stage. | A new tick stage in `BattleSimulation.AdvanceOneTick` (`BattleSimulation.cs:599`), plus the countdown field on whatever type item 1 introduces |
| 3 | **Line of sight.** Nothing anywhere asks what lies between two points. Every spatial predicate in the codebase is about one circle at one position — see section 6.3. | `CollisionUniformGrid.AnyOverlapUnchecked` (`CollisionUniformGrid.cs:402`) is the closest query shape; `CollisionGeometry.SquaredDistance` / `IsContact` / `Overlaps` are the primitives. | `src/Hukbo.Core/Simulation/CollisionGeometry.cs` for a point-to-segment predicate; `src/Hukbo.Core/Simulation/CollisionUniformGrid.cs` if a segment traversal is added |
| 4 | **Friendly fire.** The target scan hard-excludes same-faction candidates at `BattleSimulation.cs:1015`, and attack gathering only ever resolves against `source.TargetEntityId`. Nothing in the pipeline can damage an ally, and `_damageTotals` is indexed by agent but only ever written from a cross-faction attack. | `_damageTotals` (cleared at `BattleSimulation.cs:3581`, applied at `:3781-3798`) is already faction-blind — it is keyed by target index only. The damage event at `:3791-3797` carries the victim's faction. | `src/Hukbo.Core/Simulation/BattleSimulation.cs`, `GatherAndCommitAttacks` |
| 5 | **Ammunition.** No counter, no reserve, no resupply, no "out of ammo" state, and no intent for a warrior who has run dry. `AgentIntent` has five values (`src/Hukbo.Core/Simulation/AgentIntent.cs:12-24`) and none of them is "cannot attack". | `AgentState.AttackCooldownRemaining` is the only per-agent depleting integer, and `WeaponProfile.AttackCooldownTicks` is the only per-weapon budget. | Field on `src/Hukbo.Core/Simulation/AgentState.cs`, declared **after** `BrokeOffUnderPressure` so the V6 and V7 fold orders stay frozen; profile value on `src/Hukbo.Core/Combat/WeaponProfile.cs` |
| 6 | **A `WeaponGrip` (or equivalent) value that says "ranged".** `WeaponGrip` has two values, both about hands (`CombatIdentity.cs:52-66`), and `CombatRuleset.ValidateWeaponAttribute` (`CombatRuleset.cs:456`) branches only on those two. There is no flag anywhere that separates a melee weapon from a ranged one. | `WeaponGrip` itself; or a new `bool` / enum on `WeaponAttributes` (`src/Hukbo.Core/Combat/WeaponProfile.cs:142`). | `src/Hukbo.Core/Combat/CombatIdentity.cs` and/or `src/Hukbo.Core/Combat/WeaponProfile.cs` |
| 7 | **A reach ceiling, and an approach rule that stops short.** `BuildMovementProposal` (`BattleSimulation.cs:4039-4125`) always closes to body contact; `SIMULATION-GAME-STANDARDS.md:471-483` makes that normative. An archer that walks into the enemy front rank is not an archer. There is no "stand off at range X" movement behaviour and no kiting. | `BuildMovementProposal`'s three overloads at `BattleSimulation.cs:4039`, `:4065`, `:4077`; the contingent state machine at `BattleSimulation.cs:1221`; `MovementRuleset` in `src/Hukbo.Core/Movement/`. | `src/Hukbo.Core/Simulation/BattleSimulation.cs` plus a new movement preset in `src/Hukbo.Core/Movement/` |
| 8 | **A defensive resolution for a projectile.** `ClashResolver.Resolve` (`ClashResolver.cs:85`) is keyed on `(attackerWeapon, defenderWeapon, defenderShield)` and its whole model is weapon-on-weapon interception. A shield stopping an arrow and a shield stopping a kampilan are the same code path today, with no way to declare a different table for a missile. | `ClashProfile`'s three-part weapon-intercept key (`src/Hukbo.Core/Combat/ClashProfile.cs:59-68`); `AttackResolution`'s five members. | `src/Hukbo.Core/Combat/ClashProfile.cs`, `src/Hukbo.Core/Combat/ClashResolver.cs`, possibly a sixth `AttackResolution` value in `src/Hukbo.Core/Combat/AttackResolution.cs` |
| 9 | **An event kind for a launch, a flight, or a miss.** `BattleEventKind` has five values (`BattleEvent.cs:5-12`). A shot that misses produces no event at all today, because a miss is not a resolution — every accepted attack has a target it reached. | `BattleEventKind.Attack` and the `_combatContext` packing; **bits 40-63 of `_combatContext` are unused** (`BattleEvent.cs:74-76`). | `src/Hukbo.Core/Simulation/BattleEvent.cs`; the fold in `src/Hukbo.Headless/HeadlessRunner.cs:819` must gain a matching word |
| 10 | **A moving, non-agent thing for the Client to draw.** `AgentView` (`src/Hukbo.Core/Simulation/AgentView.cs`) is the only projection Core exposes, and `UpdateViews` (`BattleSimulation.cs:4260`) fills a fixed array one per agent. `BattleSnapshot` (`src/Hukbo.Core/Simulation/BattleSnapshot.cs`, 8 lines) carries agents and events and nothing else. | `AgentView`; `BattleSnapshot`. | New view type beside `src/Hukbo.Core/Simulation/AgentView.cs`; snapshot change in `src/Hukbo.Core/Simulation/BattleSnapshot.cs` |
| 11 | **A per-projectile damage attribution.** The damage event's source and target are both the victim (`BattleSimulation.cs:3794-3795`), so the feed cannot say who shot whom on the damage line. For a melee blow that is fine because the attack event on the same tick names the attacker; for a projectile launched five ticks earlier it is not. | `AddEvent` at `BattleSimulation.cs:4151` and its `BattleEventKind.Damage` call. | `src/Hukbo.Core/Simulation/BattleSimulation.cs` |
| 12 | **A separate domain tag for any new roll.** `SIMULATION-GAME-STANDARDS.md:833-840` is the tag inventory. Reusing `HKBO_CLS` or `HKBO_HIT` would correlate the draws. | `ClashResolver.ClashTag` (`ClashResolver.cs:29`), `HitLocationResolver.HitLocationTag` (`HitLocationResolver.cs:13`). | Wherever the new resolver lands, under `src/Hukbo.Core/Combat/`; plus a paragraph edit in `SIMULATION-GAME-STANDARDS.md` |
| 13 | **A projectile in the snapshot / save story.** `BattleSnapshot` is authoritative state and `SIMULATION-GAME-STANDARDS.md:228` forbids saving caches, render data, or metrics. An in-flight projectile is authoritative state and must be hashed and snapshotted, unlike every other new structure added recently, all of which were derived scratch. | `BattleSnapshot`; `StateHasher.Compute` (`src/Hukbo.Core/Determinism/StateHasher.cs:70`). | `src/Hukbo.Core/Simulation/BattleSnapshot.cs`, `src/Hukbo.Core/Determinism/StateHasher.cs` |
| 14 | **A sound and visual channel for a ranged blow.** `SIMULATION-GAME-STANDARDS.md:884-905` is the spectator-channel table for the five resolutions; there is no row for a projectile in flight, a launch, or a miss. Every existing sound slot is a melee impact or a shield clash. | The slot list served by `scripts/sfx.ps1 -List`; the channel table in `SIMULATION-GAME-STANDARDS.md`. | `SIMULATION-GAME-STANDARDS.md` section 14, `src/Hukbo.Client/`, `scripts/sfx.ps1` |
| 15 | **A perception range large enough to matter, and a scenario relation that permits it.** `Scenario.Validate` requires `PerceptionRangeRaw >= AttackRangeRaw` (`src/Hukbo.Core/Simulation/Scenario.cs:306-309`) but nothing requires the reverse, so a ranged weapon whose reach exceeds the scenario perception range simply never fires — it can never acquire a target that far away. | `Scenario.PerceptionRangeRaw` (`Scenario.cs:34`) and the validation at `Scenario.cs:306`. | `src/Hukbo.Core/Simulation/Scenario.cs`, `src/Hukbo.Core/Combat/CombatRuleset.cs` |
| 16 | **A roster-composition concept.** `CombatRuleset.ResolveLoadout` assigns a loadout by `(entityId - 1) % rosterCount` (`CombatRuleset.cs:514`) and `RosterCountExpansion` (`src/Hukbo.Core/Combat/RosterCountExpansion.cs`) expands per-entry counts. There is no notion of "this contingent is archers and stands behind that one". | `Scenario.RosterCounts` and `FormationPlanner.PlanFactionDeployment` (`src/Hukbo.Core/Simulation/FormationPlanner.cs`). | `src/Hukbo.Core/Simulation/FormationPlanner.cs`, `src/Hukbo.Core/Simulation/Scenario.cs` |

### Things that do exist and would be reused unchanged

Worth naming so a planner does not budget for them: fixed-point arithmetic
including an exact `IntegerSquareRoot` (`src/Hukbo.Core/Mathematics/FixedPoint.cs:61`),
the FNV-1a mixer (`src/Hukbo.Core/Determinism/Fnv1a.cs`), `SplitMix64`
(`src/Hukbo.Core/Determinism/SplitMix64.cs`), the deterministic per-tick
`CollisionPriority` key (`src/Hukbo.Core/Simulation/CollisionPriority.cs`), the
double-buffered event feed (`BattleSimulation.cs:609-614`), preallocated scratch
arrays throughout, and the `checked`-everywhere overflow discipline.

## 8. Per-tick allocation budget

### 8.1 Where it is stated and what the number is

**The enforced number is 16,384 bytes per 1,000 ticks, with a 4,096-byte
warm-window growth tolerance, at 12 agents per faction.**
`BattleSimulationTests.RepeatedCollisionTicksHaveBoundedAllocations` at
`tests/Hukbo.Core.Tests/BattleSimulationTests.cs:340`; the two constants are
declared at `BattleSimulationTests.cs:393-395`:

```csharp
const long maximumAllocatedBytes = 16_384;
const long warmWindowGrowthTolerance = 4_096;
const int agentsPerFaction = 12;
```

The measurement runs 32 warm-up ticks, then samples
`GC.GetAllocatedBytesForCurrentThread()` across 1,000 measured ticks
(`BattleSimulationTests.cs:319-336`).

The comment block at `BattleSimulationTests.cs:344-392` carries the whole
history and is worth reading in full before touching it. The essentials:

- The ceiling was once 500,000 bytes, then 900,000. The 900,000 figure is what
  `SIMULATION-GAME-STANDARDS.md:876-877` still records ("the collision
  allocation ceiling stays at 900,000") and what
  `docs/development/testing.md:1997` repeats. **That number is stale.** On the
  900,000-byte tree the window measured 815,312 bytes, all of it per-tick event
  traffic.
- T7 of the arch-informed performance hardening workstream removed that traffic:
  the simulation now owns its event buffers and reuses them, so no event-bearing
  tick allocates a list. Both measured windows now sit between 0 and 2,064 bytes
  over 1,000 ticks, "observed across thirteen full-suite runs"
  (`BattleSimulationTests.cs:352-356`).
- The 16,384 ceiling was chosen against that: reinstating a per-tick event list
  in the 24-agent scenario would allocate `24 * 2 * 72 = 3,456` bytes a tick,
  3,456,000 across a window, 210 times the ceiling; "even a single boxed
  enumerator per tick would allocate roughly 46,000 across a window, nearly
  three times it" (`BattleSimulationTests.cs:373-377`).

`docs/development/testing.md:1202-1214` records the real passing output of that
filtered test run.

### 8.2 The whole-run measured figures

From `docs/research/TICK-STAGE-PROFILE.md:342-347`, the T8 rule-3-rewrite
benchmark table. `allocatedBytes` is whole-process; `coreAllocatedBytes` is
summed strictly across `BattleSimulation.AdvanceOneTick()` calls inside the
headless tick loop (`TICK-STAGE-PROFILE.md:351-356`).

| Workload | Preset | Measured ticks | Mean ms/tick | p95 (ms) | p99 (ms) | `allocatedBytes` | `coreAllocatedBytes` |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 200 agents | V3 | 1,334 | 0.3040 | 1.4553 | 2.2121 | 461,888 | 118,896 |
| 200 agents | V2 | 1,064 | 0.3758 | 1.4664 | 2.1596 | 422,720 | 125,088 |
| 500 agents | V3 | 2,664 | 0.6254 | 1.5533 | 3.7820 | 966,288 | 259,376 |
| 500 agents | V2 | 3,391 | 0.6779 | 1.6671 | 3.7067 | 1,088,448 | 259,376 |

The load-bearing observation is at `TICK-STAGE-PROFILE.md:378-386`: at 500
agents `coreAllocatedBytes` is **byte-identical between the two presets, 259,376
bytes**, despite one run measuring 2,664 ticks and the other 3,391 — 727 more
ticks for zero additional bytes. That is direct evidence the tick loop's own
allocation does not scale with tick count. Everything in `coreAllocatedBytes` is
warm-up and one-time cost.

`docs/development/testing.md:1219-1235` adds the necessary caveat:
`RunReport.CoreAllocatedBytes` varied between `118896` and `125088` on two
repeated runs of an identical command with identical hashes and outcome. It is
JIT and tiered-compilation jitter in the counter, and it is **not part of any
test assertion in this repository**. The enforced budget is the 16,384-byte test
in 8.1 and nothing else.

A separate 500-agent figure is recorded at `docs/development/testing.md:2200-2209`:
`allocatedBytes` 316,682,016 for a 10,000-tick run, with tick p95 / p99 / max of
2.8523 / 4.8983 / 15.306 ms.

`docs/research/LARGE-SCALE-SIMULATION-ARCHITECTURE.md:420` carries a scaling
table with a "Harness allocation" column, and `:436-439` notes that "the
allocation curve is almost perfectly linear because each active" — the sentence
continues into the battle-event allocation packing plan reference. That curve is
about *event* allocation, which is the thing T7 subsequently removed from the
tick loop.

### 8.3 What a per-projectile allocation would do

**A per-projectile heap allocation on the launch tick would blow the enforced
budget by two to three orders of magnitude, and it is entirely avoidable.**

The arithmetic, using the same shape the test's own comment uses. Take a
200-agent battle in which a quarter of the roster is archers, so 50 archers, and
a cooldown that lets each fire roughly every 20 ticks: about 2.5 launches per
tick. A projectile object with a header, entity ID, two coordinates, a velocity
pair, a source ID, a target ID, a damage value, and a remaining-ticks counter is
comfortably 64 to 72 bytes with .NET object overhead — the same order as
`BattleEvent`'s 72 bytes. That is roughly 180 bytes per tick, 180,000 bytes over
a 1,000-tick window, **eleven times the 16,384-byte ceiling**. Scale the archer
share to half the roster and shorten the cooldown and it is 40 to 50 times over.

Worse, if the projectiles are held in a `List<T>` that is created per tick, or if
any per-tick enumeration boxes an `IEnumerator<T>`, the test's own comment says a
single boxed enumerator per tick is already "roughly 46,000 across a window,
nearly three times" the ceiling (`BattleSimulationTests.cs:376-377`).

The repository already knows the answer, and it is used everywhere:

- **Preallocate and reuse.** `SIMULATION-GAME-STANDARDS.md:609-610`: "All grid,
  pair, proposal, and resolution storage is preallocated and reused, growing only
  when capacity is insufficient, so a warm collision tick allocates nothing."
  `CollisionUniformGrid.Clear` (`CollisionUniformGrid.cs:172-178`) empties without
  releasing.
- **Use a `readonly record struct` in a flat pooled array with a free list**, not
  a class. `CollisionBody`, `CollisionPair`, and `CollisionMoveRequest` are all
  value types held in reused arrays. A projectile pool sized once at construction
  from a declared per-scenario ceiling (`maxProjectilesInFlight`), with a
  deterministic slot-allocation order and a stable `EntityId` tie-break, allocates
  nothing on a warm tick.
- **A declared ceiling is required anyway.** CLAUDE.md section 9 forbids
  unbounded caches, and `SIMULATION-GAME-STANDARDS.md:214-215` requires every
  cache to declare "source, key/value, size bound, lifetime, invalidation,
  counters, and a cold-cache equivalence test". A projectile pool is authoritative
  state rather than a cache, but the size bound is not optional either way: an
  unbounded in-flight list is a per-tick allocation waiting to happen and an
  unbounded state-hash fold.
- **Do not widen `BattleEvent`.** `BattleEvent.cs:84-90` records that packing
  five fields into one `long` kept the event at 72 bytes; the note at
  `docs/development/testing.md:2214-2221` records that merely adding the
  attacker's shield to the event pushed the budget from 900,000 to 982,744 bytes
  before the packing fixed it. Bits 40-63 of `_combatContext` are free
  (`BattleEvent.cs:74-76`); use them rather than adding a field.

The performance side is comparatively cheap. `TICK-STAGE-PROFILE.md:107-121`
gives the per-stage inclusive share: `ResolveCollisions` is 63.11 % of the tick
at 200 agents rising to 74.77 % at 2,000, `SelectTargetsAndIntents` is 5.04 % at
200 agents rising to 16.67 % at 2,000, and **`GatherAndCommitAttacks` is
2.35 % at 200 agents and 0.44 % at 2,000**. There is a great deal of headroom
inside the attack stage specifically. A per-shot O(n) line-of-sight scan, at the
same cost shape as the target scan that already costs 5 % to 17 % of the tick,
is affordable; a per-shot heap allocation is not.

## 9. Existing tests that constrain a ranged change

All paths under `tests/`. Grouped by what they constrain rather than by file, so
a planner can read down the column that matters.

### 9.1 Pinned hashes — anything that moves a hash fails here

| file:line | Constraint |
| --- | --- |
| `Hukbo.Core.Tests/CombatConfigurationTests.cs:167` | Preset V1's content hash is frozen at `0x59FB4CA563D87A49`; any V1 table edit fails. |
| `Hukbo.Core.Tests/DeterminismTests.cs:139` | Preset V2's content hash is pinned at `0x10AB1CC226AB3636` and must differ from V1's. |
| `Hukbo.Core.Tests/DeterminismTests.cs:171` | Preset V3's content hash is pinned at `0xCD790E489293B304`. |
| `Hukbo.Core.Tests/DeterminismTests.cs:192` | Preset V4's content hash is pinned at `0x4E3E4F8C0A3822E0`. A new V5 must add a fifth assertion of the same shape. |
| `Hukbo.Core.Tests/DeterminismTests.cs:215` | V4's seed-1 state hash `2BBEDD668CC38FD6` and event hash `228818712E5AE6C6` at 20 agents / 200 ticks, through the real headless path. Any behavioural change to the tick fails this. |
| `Hukbo.Core.Tests/DeterminismTests.cs:260` | Same for V3. |
| `Hukbo.Core.Tests/DeterminismTests.cs:343` | Same for the `PersistentContingentsV2` movement preset. |
| `Hukbo.Core.Tests/DeterminismTests.cs:415` | `CombatMetrics` reaches neither hash. A ranged metric added to `CombatMetrics` must stay out of both. |
| `Hukbo.Core.Tests/DeterminismTests.cs:927` and `:997` | The zero-interception control: with every clash channel at zero, the pre-clash digest and state hash reproduce exactly. This is the template for proving a new stage is inert when disabled — a ranged design should build the equivalent. |
| `Hukbo.Core.Tests/MovementPresetFreezeTests.cs` | The movement-preset digests are frozen. A stand-off approach rule needs a new movement preset, not an edit to an existing one. |
| `Hukbo.Core.Tests/Movement/MovementStateHashTests.cs` | The conditional per-agent fold order for the V6 footwork fields. New `AgentState` fields must be declared after the existing ones and folded under their own gate. |

### 9.2 Reach and weapon-profile validation — a long-reach weapon runs into these

| file:line | Constraint |
| --- | --- |
| `Hukbo.Core.Tests/WeaponProfileTests.cs:192` | Every profile of every registered preset clears the reach floor. A ranged preset's profiles are checked here automatically. |
| `Hukbo.Core.Tests/WeaponProfileTests.cs:224` | An enlarged collision body raises the reach floor, and the Itak paired profile still clears it by one and a half world units. Pins the floor-to-body-radius relationship. |
| `Hukbo.Core.Tests/WeaponProfileTests.cs:308` | A profile exactly at the reach floor throws at construction. |
| `Hukbo.Core.Tests/WeaponProfileTests.cs:173` | Dropping the shield buys damage and reach and costs no cadence. A ranged weapon that is one-handed must declare a paired profile obeying this shape, or the test's parameterisation needs revisiting. |
| `Hukbo.Core.Tests/WeaponProfileTests.cs:252`, `:272`, `:290` | The grip invariants: a two-handed weapon in the roster with a shield throws; a one-handed weapon missing its paired profile throws; a two-handed weapon declaring a paired profile throws. A bow is presumably two-handed, so it may not declare a paired profile and may not be rostered with a shield. |
| `Hukbo.Core.Tests/WeaponProfileTests.cs:32` | Every `CombatPresetId` value is registered. Adding `PrecolonialPhilippinesV5` without a registry arm fails immediately. |
| `Hukbo.Core.Tests/WeaponProfileTests.cs:20` | `WeaponId` numeric values are pinned across a rename. A new ranged weapon appends; it must not renumber. |
| `Hukbo.Core.Tests/ScenarioTests.cs:103` | `Scenario.Validate` accepts bodies that exactly fill the attack range. |
| `Hukbo.Core.Tests/ScenarioTests.cs:119` | `Scenario.Validate` rejects bodies wider than the attack range. |
| `Hukbo.Core.Tests/ScenarioTests.cs:209` | An unregistered combat preset is rejected. |
| `Hukbo.Core.Tests/CombatConfigurationTests.cs:121` | The roster is the approved four-entry configuration. Adding a fifth roster entry to an existing preset fails here. |
| `Hukbo.Core.Tests/CombatConfigurationTests.cs:148` | `ResolveLoadout` wraps through the roster by entity ID. A ranged roster entry changes which entity gets which loadout for every entity after it. |

### 9.3 Targeting, reach, and ordering behaviour

| file:line | Constraint |
| --- | --- |
| `Hukbo.Core.Tests/BattleSimulationTests.cs:23` | Nearest target with `EntityId` breaking distance ties. Any change to the scan must preserve this total order. |
| `Hukbo.Core.Tests/BattleSimulationTests.cs:79` | Agents at *exact* range attack and respect cooldown. Pins the inclusive `<=` in `IsWithinAttackRange`. |
| `Hukbo.Core.Tests/BattleSimulationTests.cs:41` | An agent approaches its target by the configured fixed step. Any stand-off movement rule must not change this under the existing presets. |
| `Hukbo.Core.Tests/BattleSimulationTests.cs:241` | Dead agents never select targets, move, or attack. A dead archer must not have a projectile in flight resolve as though it were alive — or if it does, that is a deliberate design decision this test's neighbourhood will surface. |
| `Hukbo.Core.Tests/BattleSimulationTests.cs:966` | A crowded target resolves identically under every storage order. Any new per-tick loop must be order-independent. |
| `Hukbo.Core.Tests/DeterminismTests.cs:690` | Input array order cannot change ordered results. |
| `Hukbo.Core.Tests/BattleSimulationTests.cs:1339` through `:1502` | The loadout-assignment rules: entities 1-4 receive the roster in order, wrap-around, both factions use the same entity-ID rule, roster counts do not change the spawn draw sequence. A ranged roster entry touches every one of these. |

### 9.4 Damage, defence, and the acceptance bands

| file:line | Constraint |
| --- | --- |
| `Hukbo.Core.Tests/BattleSimulationTests.cs:215` | Damage is accumulated before mutual-death resolution. The four-pass structure of `GatherAndCommitAttacks` is behaviour, not an implementation detail. A projectile that applies damage in a different stage breaks the simultaneity guarantee. |
| `Hukbo.Core.Tests/BattleSimulationTests.cs:800` | A non-landed attack emits a value of zero and no damage event. |
| `Hukbo.Core.Tests/BattleSimulationTests.cs:840` | A non-landed attack still resets the attacker's cooldown. |
| `Hukbo.Core.Tests/BattleSimulationTests.cs:882` | Mixed resolutions on one target aggregate only the landed damage. |
| `Hukbo.Core.Tests/BattleSimulationTests.cs:777` | A shielded defender takes less damage than an unshielded one at the same seed. |
| `Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs:687` | **The defence-attributable share stays inside the 0.25-0.45 acceptance band across seeds 1-20.** This is the enforced gate of `SIMULATION-GAME-STANDARDS.md:855-861` and the single most likely test for a ranged change to break, because every projectile that reaches a defender changes both the numerator and the denominator. |
| `Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs:797` | Shielded roster entries absorb more blows before dying than shieldless ones across seeds 1-20. If a shield does not stop arrows, this relationship weakens as the archer share rises. |
| `Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs:382` | Aggregate damage per target per tick equals the sum of the individual attack values, across a full battle. A projectile whose damage lands without a matching attack event on the same tick breaks this outright. |
| `Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs:516` | Attack cooldown gaps remain at least the configured cooldown ticks across a full battle. |
| `Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs:50` | Every accepted attack in a fixed-seed full battle has a configured weapon and a resolved hit location. A projectile impact is an accepted attack and must carry both. |
| `Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs:465` | Same-tick mutual-death events precede the outcome event in emission order. |
| `Hukbo.Core.Tests/PhilippineCombatIntegrationTests.cs:615` | A target driven to zero by the aggregate still emits every contributing attack. |
| `Hukbo.Core.Tests/CombatMetricsTests.cs:170` | Per-faction attack counts partition the undivided total on every tick. `GatherAndCommitAttacks` throws at `BattleSimulation.cs:3752-3759` if they do not; a projectile impact resolved outside that loop would have to be credited explicitly. |

### 9.5 The clash resolver's pinned vectors and reference implementation

| file:line | Constraint |
| --- | --- |
| `Hukbo.Core.Tests/ClashResolverTests.cs:114` | `MixClash` matches every pinned vector. These vectors may never be edited to go green. |
| `Hukbo.Core.Tests/ClashResolverTests.cs:153` through `:237` | Seven single-word isolation cases: the roll changes when only the seed, only the tick, only the source ID, only the target ID, only the attacker weapon, only the defender weapon, or only the defender shield changes. Adding a word to the fold requires an eighth. |
| `Hukbo.Core.Tests/ClashResolverTests.cs:259` | `Resolve` matches the naive reference across the whole roster matrix (`Hukbo.Core.Tests/NaiveClashResolution.cs`). A new channel needs a matching change in the reference. |
| `Hukbo.Core.Tests/ClashResolverTests.cs:307` | The outcome is selected correctly at every interval edge. Inserting a sixth interval moves every edge. |
| `Hukbo.Core.Tests/ClashResolverTests.cs:498` | A zero-width channel is never selected. |
| `Hukbo.Core.Tests/ClashResolverTests.cs:788` | The resolver never blocks without a shield. |
| `Hukbo.Core.Tests/CombatConfigurationTests.cs:20` | `AttackResolution` numeric values are pinned. |
| `Hukbo.Core.Tests/HitLocationResolverTests.cs:33` | Golden vectors for `MixAttack` and the resolved body part. |
| `Hukbo.Core.Tests/HitLocationResolverTests.cs:206` | `MixAttack` changes when any single field changes. |

### 9.6 The event and its packing

| file:line | Constraint |
| --- | --- |
| `Hukbo.Core.Tests/BattleEventTests.cs:9` | Every packed combat-context enum fits the byte it is packed into. A ranged field must fit in the free bits 40-63, or this fails. |
| `Hukbo.Core.Tests/BattleEventTests.cs:42` | `Landed` is numeric zero and the weapon byte is never zero, so "combat context absent" stays unambiguous. A new event kind that carries partial combat context would break this reasoning. |
| `Hukbo.Core.Tests/BattleEventTests.cs:86` | `Attack` round-trips every combination of packed combat context. |
| `Hukbo.Core.Tests/BattleEventTests.cs:288` | `NonAttack` allows every non-attack kind. A new `BattleEventKind` value is covered here automatically and must be constructible through `NonAttack`. |
| `Hukbo.Core.Tests/BattleEventTests.cs:303` | `NonAttack` rejects the `Attack` kind. |
| `Hukbo.Core.Tests/BattleSimulationTests.cs:1661` | Non-attack events never carry a weapon or a hit location. |
| `Hukbo.Core.Tests/BattleSimulationTests.cs:114` and `:171` | `LastEvents` is a completed-tick snapshot and a retained reference is not valid past the producing tick. The double-buffer scheme is load-bearing; a projectile stage that emits after the buffer swap would violate it. |
| Client side: `tests/Hukbo.Client.Tests/BattleEventFormatterTests.cs`, `BattleEventFeedTests.cs`, `BattleEventLogPanelTests.cs` | The feed must render every event kind and every resolution. A new kind or resolution with no formatter case surfaces here. |

### 9.7 Allocation, performance, and termination

| file:line | Constraint |
| --- | --- |
| `Hukbo.Core.Tests/BattleSimulationTests.cs:340` | **16,384 bytes per 1,000 warm ticks, 4,096-byte warm-window growth tolerance, 12 agents per faction.** The hard budget of section 8. |
| `Hukbo.Core.Tests/BattleSimulationTests.cs:296` | The same bound for quiet ticks. |
| `Hukbo.Core.Tests/BattleSimulationTests.cs:489` | Collision ticks actually exercise the resolver, so the allocation test above is measuring real work. |
| `Hukbo.Core.Tests/BattleSimulationTests.cs:566` | Seeds 1-20 produce victories for both factions. A ranged advantage that always favours the faction holding the low entity IDs fails here. |
| `Hukbo.Core.Tests/BattleSimulationTests.cs:645` | The canonical 200-agent battle terminates within the tick limit. |
| `Hukbo.Core.Tests/LastStandFormationTests.cs` — `NoLastStandBattleStallsAtTheTickLimitAcrossSeedsOneThroughTwoHundred` | 200 seeds must not stall. Cited by `src/Hukbo.Core/Simulation/CollisionRules.cs` as the regression lock for the body-radius deadlock class. An archer that refuses to close is a new way to produce a stall. |
| `Hukbo.Core.Tests/ContingentDeadlockTests.cs` | The contingent-level deadlock guards. |

### 9.8 Structural boundaries

| file:line | Constraint |
| --- | --- |
| `Hukbo.Core.Tests/DiagnosticLoggingBoundaryTests.cs:22` | `Hukbo.Core` does not reference the `Hukbo.Diagnostics` assembly. A projectile stage may not log from Core. |
| `Hukbo.Core.Tests/DiagnosticLoggingBoundaryTests.cs:53` and `:106` | Full trace logging does not change the simulation result, under the default preset and under V7. Any instrumentation added for projectiles is checked here. |
| `Hukbo.Core.Tests/CollisionUniformGridTests.cs:102` through `:275` | Every grid query matches a naive O(n²) oracle, in a fixed order, for every input permutation, repeatably. **A new segment query must bring its own oracle to this file** — see `Hukbo.Core.Tests/NaiveCollisionPairs.cs`, `NaiveCollisionResolution.cs`, and `NaiveClashResolution.cs` for the three existing reference implementations. |
| `Hukbo.Core.Tests/CollisionUniformGridTests.cs:49` and `:58` | A cell smaller than one body diameter is rejected; exactly one diameter is accepted. This is what makes the three-by-three neighbourhood sufficient, and a segment traversal must not assume it. |
| `Hukbo.Core.Tests/HeadlessRunnerTests.cs` | The headless report contract, including the fields the gate reads. |

### 9.9 The manual checklist

`docs/development/testing.md` holds the interactive smoke checklist. Per
CLAUDE.md section 6 rule 4, no compilation, unit test, or window-opening probe
may flip a row to `PASS`; only a human at an interactive desktop may. A ranged
weapon adds spectator-visible behaviour — a projectile in flight, a launch
sound, a miss — and every one of those needs a new `PENDING` row rather than a
claim.

