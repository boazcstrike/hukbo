# Shield size against projectile size — design

Date: 2026-08-15
Game: **Hukbo** (this document does not touch Sandata)
Status: design only. A design document does not authorize implementation; the
plan document `2026-08-15-shield-projectile-block.md` carries the ordered task
list.

## 1. What the change is for

Today a shield is a single binary fact about a warrior. `ShieldId` has two
members, `None = 1` and `TallHardwood = 2`, and the shield contributes exactly
one number to combat: a flat 2,400 basis-point interception chance that applies
identically to a spear thrust, a swung blade, a loosed arrow, and an arquebus
ball. A projectile has no size at all — `Projectile` carries a source, a target,
a launch tick, a flight countdown, an origin, a weapon, and a damage figure, and
nothing about its physical bulk.

The result is that the shield is a stat rather than an object. A spectator
watching a battle cannot tell a shield-bearer's tactical situation apart from
any other warrior's, and the player composing an army has no reason to prefer
one shield over another because there is only one.

This change introduces a size axis on both sides of the interception and a
movement cost that pays for it:

- A **larger shield** is easier to hide behind. It stops a greater share of
  incoming attacks, and it stops small, light projectiles very reliably.
- A **larger shield is slower to carry.** Its bearer's movement pace is lower
  than a smaller shield's bearer, which is in turn lower than a warrior
  carrying no shield at all.
- A **larger projectile** is harder for any shield to stop, and the difficulty
  falls hardest on the small shield. A small shield loses more of its
  interception against a heavy incoming attack than a large one does.
- **Blocking briefly costs movement.** A warrior who has just taken a blow on
  the shield is knocked off their pace for a short, deliberately quick window
  before recovering.

That set of statements is the whole feature. Everything below is how it is
expressed without breaking determinism, without moving a recorded baseline that
should not move, and without inventing history.

## 2. The nine questions

`SIMULATION-GAME-STANDARDS.md` §10 requires every feature proposal to answer
these. Answers here are binding on the plan.

**1. What is the user-visible outcome?**
Three shield states exist instead of two. A spectator sees warriors carrying
visibly different shields — a broad body-length board and a narrow breast-high
board — moving at visibly different speeds, and sees arrows stopped by the broad
shield that get through the narrow one. A warrior who blocks visibly checks
their pace for a fraction of a second before resuming.

**2. Which tick stage reads and writes the state?**
Interception is decided inside stage 10, `GatherAndCommitAttacks`, in
`ClashResolver.Resolve`, for melee and for projectile arrival alike — projectile
arrival already routes through the same resolver with the defender's shield as
an argument (`BattleSimulation.cs:4421`). The block-recovery counter is
**written** in stage 10 when a block resolves, **decremented** in stage 1
`DecrementCooldowns`, and **read** in stage 5 `GatherMovementProposals` when the
pace cap is chosen. A block therefore takes effect on the tick after the block,
which is stated here so nobody later reads the one-tick offset as a bug.

**3. What are the numeric units and bounds, and what is the same-tick conflict
rule?**
Every new quantity is an integer. Shield span and projectile bulk are
fixed-point world-unit raws on the same scale as weapon reach. Interception is
basis points, clamped to `[0, 10_000]`. Block recovery is a tick count, bounded
by an authored per-shield constant. Two blocks on the same defender in the same
tick set the counter to the larger of the two resulting durations rather than
summing, so the effect cannot stack into a stun-lock; ties are irrelevant
because the value is a maximum.

**4. What is the total ordering and the random-stream policy?**
No new random stream. Interception continues to be decided by the existing
per-attack `HKBO_CLS` FNV-1a roll in `ClashResolver`, which already folds the
defender's shield. The new terms change the width of the shield interval, not
how the roll is drawn. No new iteration over a hash-ordered collection is
introduced; the new per-shield and per-weapon tables are flat arrays indexed by
enum value.

**5. What is the cache source and invalidation, or is there no cache?**
No cache. Interception is recomputed per attack from ruleset constants, which
are immutable for the lifetime of a battle.

**6. What is the save, event, and version effect?**
The block-recovery counter is authoritative agent state: it is snapshotted and
folded into the state hash, and it is gated so that it folds nothing under a
preset that does not apply it. Interception width changes the resolution of
attacks and therefore reaches the event hash. Both effects require a new preset
version rather than an edit in place, so this change ships
`CombatPresetId.PrecolonialPhilippinesV7` and a new `MovementPresetId` member,
and leaves V1–V6 and movement V1–V13 byte-identical.

**7. What is the worst-case complexity and the benchmark workload?**
Interception adds two integer multiplications and one integer division per
resolved attack — no new loop, no new allocation, no change to the per-tick
complexity class. The block-recovery counter adds one integer decrement per live
agent per tick, folded into the existing cooldown pass. The benchmark workload
is the standard 200-agent / 10,000-tick / seed-1 headless run.

**8. How does a spectator discover the effect without reading source code?**
This is the question that decides whether the feature is finished, and it is
answered in four places, all required:
- The two shields **draw at different widths**, so the difference is visible
  before anything happens.
- The **agent inspector** names the shield, states its evidence tier, and shows
  the interception the warrior currently has against the attack types in play,
  plus a live indicator while the block-recovery window is open.
- The **battle event log** already says "stopped by the shield"; it gains the
  shield's name so a reader can tell which shield stopped it.
- The **battle report** already counts shield-blocked attacks per faction and
  continues to do so.

**9. Which tests fail before implementation and pass afterward?**
Every task in the plan names its own. At minimum: a test asserting the larger
shield's interception strictly exceeds the smaller shield's against every
projectile weapon; a test asserting the interception gap between the two shields
widens as projectile bulk rises; a test asserting the pace ordering
solo > narrow > tall; a test asserting the recovery counter opens on a block and
closes after exactly its authored duration; and a test asserting every state
hash and event hash recorded for movement presets V1–V13 and combat presets
V1–V6 is unmoved.

## 3. The shield size axis

### 3.1 A third `ShieldId` member, appended

`ShieldId` gains one member. Enum values are append-only, so the existing two
keep their numbers and every recorded hash that folds a shield identity is
undisturbed.

```csharp
public enum ShieldId
{
    None = 1,
    TallHardwood = 2,
    NarrowBreastHigh = 3,
}
```

The alternative — adding a size field to `CombatLoadout` — was rejected. The
repository already keys its clash tables, its void tables, its target-weight
profiles, and its movement profile rows on `ShieldId`, so a new member slots
into machinery that exists, while a new struct field would have to be threaded
through all of it in parallel with the enum that is already there.

### 3.2 What the two shields are, historically

`CLAUDE.md` §7 and `docs/research/HISTORICAL_1500s_ARMOR.md` bind this section.
Every claim is labelled.

| Shield | Evidence tier | Basis |
| --- | --- | --- |
| Narrow breast-high board | **Documented, form uncertain** | A shield reaching to the bearer's chest and a little more than half a *vara* wide, roughly 42 centimetres, is described in the period record (`HISTORICAL_1500s_ARMOR.md:682`, `:780-781`). |
| Body-length board (`TallHardwood`) | **Provisional reconstruction** | Body-length shields appear in a source Scott read only in an optical-character-recognition text (`HISTORICAL_1500s_ARMOR.md:681`); large shields carried with spears and blades are Documented, form uncertain from the Boxer Codex (`:684`). The existing in-game shield is a reconstruction and keeps that label. |

Naming in player-facing text follows the pair-form rule
(`HISTORICAL_1500s_WEAPONS.md:77-81`). The existing shield's inspector label is
`"Tall Hardwood"`, a plain English descriptor carrying no cultural name, and it
stays. The new shield takes a plain English descriptor on the same footing:
**`Narrow Breast-High`**. No new Filipino name is introduced for it. That is
deliberate: `kalasag` is Documented for 1521 as a general word for shields
(`HISTORICAL_1500s_ARMOR.md:757-758`), not as a term distinguishing one shield
size from another, and *palisay*, *pakil*, *batung-batung*, and *habay-habay*
are all recorded OPEN in that document and are not cleared for use. Attaching
any of them to a specific size would be inventing a distinction the sources do
not make.

The Boxer Codex Cagayan shield width figure is explicitly **not** quoted
(`HISTORICAL_1500s_ARMOR.md:784-790`).

### 3.3 Span, and why it is not the drawn width

Each shield carries a **span**, a fixed-point world-unit raw describing the
width of face it presents to an incoming attack. Span is a gameplay quantity. It
is proportioned from the historical record — the narrow shield is roughly half
the broad one, matching a breast-high 42-centimetre board against a body-length
one — but the absolute numbers are **provisional tuning**, marked as such in
code comments and tests, and are never presented as a measurement.

Span drives interception. The client's drawn shield width is derived
independently in `PawnGeometry` and must be brought into visual agreement with
span, but the renderer never feeds the simulation.

## 4. The projectile size axis

### 4.1 One number, not two

Each weapon gains a **shield-defeat bulk**: a fixed-point world-unit raw
standing for how hard that weapon's incoming attack is for a wooden board to
stop. Melee weapons carry zero.

Bulk is deliberately a single number that folds together two physically distinct
things — the projectile's cross-section and its ability to punch through the
board. Splitting them into two authored constants was considered and rejected:
it doubles the tuning surface, and the two terms are never observed separately
by a spectator, who sees only whether the shield stopped the shot. The doc
comment on the constant records that it is an abstraction, so a later reader
does not mistake it for a caliber.

### 4.2 Authored values

All **provisional tuning**, marked in code and in tests.

| Weapon | Bulk | Reasoning |
| --- | --- | --- |
| All melee weapons | 0 | A melee blow is met by the shield's base interception with no size term. Keeping bulk at zero is what makes melee interception under V7 reduce exactly to the V6 arithmetic for the existing shield. |
| Busog (bow) | small | An arrow is the case the sources actually describe being caught: warriors "received them on their shields" (`HISTORICAL_1500s_ARMOR.md:676`, `:720-725`). |
| Bangkaw (spear) | medium | A thrown spear carries far more mass into the board than an arrow. |
| Arquebus | large | The period record is unambiguous that shot went through: "the shots only passed through the shields, which were made" of thin wood (`HISTORICAL_1500s_ARMOR.md:675`, `:711-715`). Bulk is the axis that expresses this, which is why bulk is defined as *resistance defeated* rather than as physical cross-section — an arquebus ball is the smallest projectile in the game and must be the least blockable. |

This is the one place where the design's single-axis simplification earns its
keep: a pure cross-section model would have made the arquebus the *easiest*
thing to block, contradicting the clearest documented statement in the source
material.

## 5. The interception rule

`ClashProfile` today exposes one flat `ShieldInterceptBasisPoints` and resolves
it with `ResolveShieldIntercept(ShieldId)`, returning zero for `ShieldId.None`.
V7 adds a size-aware resolution taking the attacking weapon as well:

```
interceptBasisPoints(shield, attackerWeapon) =
    shield == None
        ? 0
        : baseIntercept(shield) * span(shield)
            / (span(shield) + bulk(attackerWeapon))
```

All integer arithmetic, evaluated in `long` and clamped to `[0, 10_000]`. The
division is exact-truncating and `span(shield)` is strictly positive for every
shield other than `None`, so there is no division by zero and no epsilon
anywhere.

The rule has the four properties the feature needs, and each is a test:

1. **Melee is unchanged in form.** Bulk is zero for melee, the ratio is exactly
   one, and interception reduces to `baseIntercept(shield)`. Under V7 with the
   existing shield's base kept at 2,400, melee interception against
   `TallHardwood` is numerically identical to V6.
2. **A bigger shield always blocks better.** `baseIntercept` and `span` are both
   larger for the broad shield, so its interception exceeds the narrow shield's
   against every weapon.
3. **A bigger projectile is always harder to stop.** Interception is strictly
   decreasing in bulk for a fixed shield.
4. **The small shield suffers more from bulk.** The ratio `span / (span + bulk)`
   falls faster for a smaller span, so the proportional loss the narrow shield
   takes against a heavy attack exceeds the broad shield's. This is the precise
   sense in which "smaller shield, harder to block larger projectiles" is true,
   and it is the property worth pinning in a test because it is the one an
   innocent-looking retune can silently destroy.

### 5.1 Keeping V1–V6 byte-identical

The existing `ShieldInterceptBasisPoints` property and the existing
`ResolveShieldIntercept(ShieldId)` overload stay exactly as they are, and
`CombatRuleset.ContentHash` keeps folding that single value in its current
position. The per-shield base table, the span table, and the bulk table are
**optional and fold nothing when unset**, following the precedent
`appliesPressureInterrupt` set in `StateHasher.cs:52-68` and the trailing
optional-parameter precedent in `LoadoutMovementProfile.cs:278-285`. A preset
that does not author them behaves and hashes exactly as it does today.

This is the load-bearing constraint of the whole package. A new field that folds
unconditionally would move the content hash of every preset and invalidate all
five recorded gate baselines for no behavioural reason.

## 6. The movement cost

### 6.1 Pace, through a per-shield speed scale

**This section was rewritten on 2026-08-15, during implementation, because its
original premise was false.** What follows first is the corrected design; the
superseded reasoning is kept below it, because the mistake is instructive and
because the machinery it describes was built and tested before the error was
found.

The premise that failed: the design assumed the shipped movement preset used
the equipment-relative footwork pipeline, and that shield pace could therefore
be expressed by authoring `LoadoutMovementProfile` rows. It does not.
`CohortLateralSpreadV13`, the preset the client actually ships, registers
`usesEquipmentRelativeFootwork: false` with
`loadoutMovementProfiles: ImmutableArray<LoadoutMovementProfile>.Empty`
(`MovementPresetRegistry.cs:648`, `:651`). There are no per-loadout rows in the
shipped pipeline at all.

Building the new preset as equipment-relative did not merely fail to express
shield pace — it crashed. `CanonicalLoadoutIndex` maps no key for a ranged
weapon, so the first Bangkaw warrior threw
`No movement profile is registered for this loadout`. The isolation is exact:
combat preset V7 with movement preset 13 runs clean, and combat preset V5 with
movement preset 14 crashes, so the movement preset alone was at fault.

**The corrected rule.** Shield encumbrance scales the agent's movement speed
once, at agent creation:

```
movementSpeedRaw = scenario.MovementSpeedRaw
    * ResolveShieldPaceBasisPoints(loadout.Shield) / 10_000
```

with `None` at 10,000 basis points, `NarrowBreastHigh` at 9,600, and
`TallHardwood` at 9,000 — all provisional tuning. The resolver returns 10,000
for every shield under a preset that does not apply the effect, so a flag-off
preset multiplies by exactly one and its state hash cannot move.
`MovementSpeedRaw` is assigned once per agent and is already hashed state, so
this needs no new field and no per-tick cost, and it yields the required
ordering solo > narrow > tall directly.

The new movement preset is therefore `CohortLateralSpreadV13` restated exactly,
plus the shield-encumbrance scale and the block-recovery window. It does not
change the footwork pipeline, and that is the point.

The narrow-shield loadout rows, the two new canonical indices, and the
six-or-eight-row validation described below were all built and tested before
the error surfaced. They are kept rather than reverted: they are correct on
their own terms, they are unreachable under any preset that does not declare
them, and the first equipment-relative preset to field a narrow shield will
need them.

#### Superseded: pace through authored rows rather than a multiplier

`MovementRuleset` resolves a `LoadoutMovementProfile` from the
`(WeaponId, ArmorId, ShieldId)` key through `CanonicalLoadoutIndex`, which today
maps exactly six rows and returns `-1` for anything else. The shielded rows are
hand-authored in `TallHardwoodMovementProfiles.cs` rather than derived from the
solo rows by a multiplier, and a test — `NoDynamicShieldMultiplierReachesEither
ShieldRow` — pins that.

The design keeps that convention. Two rows are appended to the canonical order,
Kalis and Itak with the narrow shield, at indices 6 and 7. A preset that does
not author them keeps six rows, and `ResolveLoadoutProfile` already throws for
an index past the end, so an old preset asked for a narrow-shield loadout fails
loudly rather than silently inheriting another row's footwork — which is what
the existing doc comment says the throw is for.

`MovementRuleset`'s construction-time validation currently demands *exactly*
six rows. It becomes: a preset with equipment-relative footwork carries either
the six canonical rows or all eight, each once, in canonical order. Six and
eight are the only legal counts; a preset carrying seven is a missing row and
must still fail.

The authored paces must satisfy, and are tested to satisfy:

```
solo  >  narrow-shield  >  tall-shield
```

for the forward pace cap of both Kalis and Itak. The existing test
`NeitherShieldRowGrantsASpeedBonusOverItsSoloCounterpart` already forbids the
left-hand inequality from inverting; the new middle term is what makes shield
size a real choice rather than a free upgrade.

Making the broad shield strictly slower than it is today changes behaviour, so
it happens **only in the new movement preset**. V13 and everything before it are
untouched.

### 6.2 The block-recovery window

A new authoritative field on `AgentState`:

```csharp
internal int ShieldBlockRecoveryTicksRemaining { get; set; }
```

- **Set** in stage 10 when a resolved attack against this agent returns
  `AttackResolution.ShieldBlocked`, to the authored duration for the shield the
  defender carries, taking the maximum against any value already present this
  tick.
- **Decremented**, floored at zero, in stage 1 alongside the existing cooldowns.
- **Read** in stage 5: while it is above zero the agent's pace cap is clamped to
  an authored basis-point ceiling.
- **Hashed** in `StateHasher`, gated on the ruleset flag so a preset that does
  not apply the effect folds nothing.
- **Snapshotted**, because it is authoritative state and a resumed battle that
  forgot it would diverge.

The durations are short by intent — the user requirement is an effect that reads
as a quick check to the warrior's stride, not a stagger the spectator would
describe as a stun. At Hukbo's 20 Hz tick, a handful of ticks is on the order of
a fifth of a second. The broad shield's window is the longer of the two: a
heavier board takes longer to bring back into guard, which is the second reason
to prefer the narrow shield and the mechanism that stops the broad shield from
being a strict upgrade once the pace difference alone is judged too small.

Existing precedent for a temporary movement penalty is `FootworkPhase.Commit`
transitioning to `FootworkPhase.Recover` with `FootworkTicksRemaining`
(`BattleSimulation.cs:3560-3564`). This design deliberately does **not** reuse
that field or add a member to `FootworkPhase`. Adding an enum member changes
enum ordering, which `CLAUDE.md` §5 makes a new-preset event on its own, and
overloading the existing counter would make an attack commitment and a block
indistinguishable in state — two different causes writing one field is exactly
the kind of ambiguity that makes a determinism divergence hard to read later.

## 7. Presets

| Preset | New member | Contents |
| --- | --- | --- |
| Combat | `CombatPresetId.PrecolonialPhilippinesV7 = 7` | V6 carried across unchanged, plus the `NarrowBreastHigh` roster entries, its weapon-intercept and void table rows, its target-weight profile, the per-shield interception bases, the per-shield spans, and the per-weapon shield-defeat bulks. |
| Movement | next free `MovementPresetId` member after `CohortLateralSpreadV13 = 13` | V13 carried across, plus the two narrow-shield rows, the slowed broad-shield rows, and the block-recovery gate with its per-shield durations and pace ceiling. |

Neither existing preset is edited. Both new presets are appended, both content
hashes are recomputed from the built code rather than hand-calculated, per the
rule recorded at `MovementPresetRegistry.cs:27-30`.

## 8. What the spectator sees

Question 8 of §10 is not satisfied by simulation correctness, so these are plan
tasks with the same standing as the Core work.

**Drawn shield width.** `PawnGeometry` computes shield width as
`max(2, 4 × scale + widthDelta)` with no shield-identity term. It gains one, so
the narrow shield draws visibly narrower than the broad one at every detail
tier, with the existing minimum-width floor still respected. `PawnShieldRole`,
`AttackMotionCatalog`, and `AttackPoseResolver` each switch exhaustively on
`ShieldId` and must gain the new member — the C# compiler will find these, since
`TreatWarningsAsErrors` is on.

**Inspector.** `AgentInspectorContent` already renders a `Shield:` row, a shield
label, and an evidence-tier line. It gains the new label, the new tier, and two
new lines: the shield's span, and a live block-recovery indicator while the
window is open. The block-recovery value must be surfaced on `AgentView` to be
readable. Note the known trap recorded for this repository: `AgentView`'s
movement fields read zero under the shipped preset, so the new field must be
confirmed non-zero in a real run rather than assumed to be populated.

**Event log.** `BattleEventFormatter` renders "stopped by the shield" and
elsewhere renders a shield as the bare words "solo" or "shielded". With three
shield states those words no longer identify anything, so the block line names
the shield.

**Army composition.** The default composition in `ArenaGame.cs:1699-1707`
fields nine rows, two of them shielded. It gains narrow-shield rows for Kalis
and Itak, because a shield nobody carries cannot be discovered by watching. This
is Client settings and moves no simulation hash.

**Shipped defaults.** The client hardcodes
`CombatPresetId.PrecolonialPhilippinesV5` at `ArenaGame.cs:1585` and defaults
movement to `CohortLateralSpreadV13` in `ClientSettingsStore.cs:113-114`. Both
move to the new presets, or the feature ships switched off for the only person
who would ever see it.

## 9. What this change must not move

The five recorded gate baselines in `docs/development/testing.md` cover the
default workload at combat 6 / movement 4, ranged standoff at 5/8, battlefield
realism at 5/10, last-stand at 5/11, and cohort lateral spread at 5/13. **None
of those five preset pairs is edited by this change**, so all ten recorded
hashes must come back unmoved. A moved baseline is not a result to re-record
here; it is evidence that a new field folded unconditionally somewhere it should
have been gated, and the fix is the gate, not the number.

## 10. Deliberately out of scope

- **Shield durability, breakage, or wear.** The sources describe shields being
  penetrated, which the bulk axis already expresses as a failed interception.
  A hit-point pool on a shield is a stock-and-consumption model and is not
  authorized.
- **Directional shield facing.** Hukbo's pawn body is heading-less; only the
  weapon arm is directional. A shield that only protects an arc would need a
  body facing the game does not have.
- **A third or fourth shield size.** Two shields plus none is enough to express
  the axis. Each additional member costs a full row of every clash table.
- **Any change to Sandata.** Sandata has no shields and shares only the four
  tier-1 determinism primitives, none of which this change touches.
