# Battlefield realism — design

Status: design. A design document does not authorize implementation; the
battlefield realism plan document does. That plan was archived out of
`docs/plans/` on 2026-08-11 once its nineteen tasks merged; this design stays
live because source and tests cite it by path.

Date: 2026-08-11. Branch: `battlefield-realism`. Base commit: `c13b696`.

## 1. What this changes and why

A spectator watching a Hukbo battle today sees two clouds of warriors walk into
each other. The groups are real — the formation planner deals every warrior into
one of up to eight contingents, and the persistent-contingent movement presets
keep that membership alive for the whole battle — but nothing about a group is
legible as a *kind* of group. Every contingent holds a proportional slice of
every weapon in the roster, because membership is dealt round-robin. Shield
bearers are scattered through the depth of a contingent at random. And a warrior
carrying a bow, a javelin, or an arquebus stands exactly where it is while a man
with a great blade walks up to it and kills it, because the ranged standoff rule
is a one-sided distance test that asks only whether the shooter's own target is
far away, never whether anything dangerous is close.

The user asked for three things, in these words:

- **A.** Group warriors by weapon, so that a body of warriors reads as a body of
  one weapon rather than as a mixed slice of the whole army.
- **B.** Put shield bearers at the front.
- **C.** Make ranged warriors run away from melee that is closing on them.

All three are gameplay legibility changes. None of them is a historical claim,
and section 2 is entirely about keeping that distinction honest, because two of
the three run directly against negative findings this repository's own research
already recorded.

### 1.1 Decisions the user has already taken

These were settled before this document was written. They are recorded here so
that a later reader does not reopen them, and they are not up for revision
inside this design.

1. **Framing is a labelled gameplay model.** All three behaviours ship as
   explicit *Provisional reconstruction / gameplay model*, labelled in code
   comments, in the agent inspector, and in the documentation. The research
   documents are amended to record a deliberate divergence rather than being
   softened. The following words are forbidden everywhere in the shipped
   artifact — code, comments, UI strings, and documentation — because
   `docs/research/ARMY-COMPOSITION.md` lines 515 to 518 forbid them: *shield
   wall*, *phalanx*, *shield line*, *front rank* used as the name of a thing,
   *squad*, *platoon*, *captain*, *sergeant*, *company*, *regiment*.
2. **The ranged retreat trigger is an enemy melee fighter inside a threat
   radius.** The shooter back-pedals directly away and resumes shooting once
   the threat is clear. It is not a role-specific skirmish-and-retire doctrine,
   and it is not a withdrawal through friendly lines.
3. **Shield depth is resolved within each contingent, not army-wide.** Shield
   bearers take the forward-most slots *of their own contingent*. There is no
   army-wide front band, and ask A survives intact: weapon grouping decides
   which contingent a warrior joins, and shield depth decides where inside that
   contingent it stands.

### 1.2 What this design deliberately does not do

- It does not change the shipped default. `PersistentContingentsV4` remains the
  default movement preset and `PrecolonialPhilippinesV4` the default combat
  preset, so the recorded canonical-gate baseline stays byte-identical.
- It does not add a field to `WeaponProfile`, a new `CombatPresetId`, or a new
  field to `MovementRuleset`. Section 6 shows why none of the three is needed
  and what each would have cost.
- It does not touch `FormationPlanner`'s dealing loop, its lattice, its anchor
  arithmetic, or either of its two jitter draws. Section 6.1 is the argument
  that this is both possible and necessary.
- It does not fix the contingent-count derivation that
  `docs/research/ARMY-COMPOSITION.md` lines 548 to 578 already flags as
  historically wrong — the equal split across contingents where the evidence
  describes unequal, chief-driven followings. That is a real defect and it is
  explicitly **out of scope** here; touching it would move every deployment
  under every preset. It is recorded in section 12 as an open item.
- It does not introduce morale, terrain, pathfinding, ammunition, or any of the
  other gated systems in `CLAUDE.md` section 9. The retreat rule in section 5
  is a movement rule inside the existing proposal stage, not a morale model: it
  reads a distance and nothing else, it carries no accumulated state, and no
  warrior ever becomes permanently unwilling to fight.

## 2. Historical position

`CLAUDE.md` section 7 and `docs/research/HISTORICAL_1500s_WEAPONS.md` bind this
section. Every claim below carries its evidence tier.

### 2.1 What the evidence actually says

**Weapon-homogeneous bodies are not attested.** The ranged tactics evidence
review states, in its list of things the corpus does not establish, that there
is no "distinction, anywhere, between a missile specialist and a close fighter
within the same following. The specialists in the record are separate peoples
supplying allied contingents"
(`docs/research/ranged/2026-08-07-RANGED-TACTICS-EVIDENCE.md` lines 1138 to
1140). **Unknown or unsupported.**

**Shield walls and rigid ranks are not attested.** Three separate research
documents record this as a negative finding:
`docs/research/movement/tall-hardwood-shield.md` line 104, claim THS-08 — "No
reviewed source establishes a shield wall, fixed interval, or Kalis/Itak shield
school", tiered **Unknown or unsupported**, with the stated implication
"Prohibit rigid ranks and historical technique names";
`docs/research/movement/README.md` line 166, listing "a shield wall" among the
things not to build; and
`docs/research/battles/03-deep-past-formations-and-tactics.md` line 65, listing
"regular files or ranks", "fixed frontage and depth", "a shield wall", "a spear
block", and "a bow or javelin screen" together as unattested.

**Shields are ordinary equipment, not a specialist role.** Morga records that
the weapons "generally used throughout the islands are moderate-sized spears
with well-made points; and certain shields of light wood ... These cover them
from top to toe, and are called *carasas* [*kalasag*]", with the bow explicitly
the provincial case and spear-and-shield the general one
(`docs/research/ranged/2026-08-07-RANGED-TACTICS-EVIDENCE.md` lines 555 to 558).
**Documented.** This matters for ask B: a shield bearer is not a distinct kind
of soldier, so the game must not present the forward slots as a specialist
formation.

**Ranged fighters breaking and running is documented; a doctrine of skirmish
and retire is not.** The same review records archers and javelin throwers
breaking under pressure at Bombon in 1570, at Bangkusay in 1571, and through a
prepared passage at Lubang, all tiered **Documented** (lines 836 to 847). It
separately records "A protected withdrawal through friendly close fighters" as
**not attested** (lines 863 to 864), and the shooter who stays and fights as the
better-attested behaviour (lines 804 to 855). The existing ruling in
`docs/plans/2026-08-07-ranged-units-design.md` lines 1226 to 1239 is "No
fall-back when an enemy closes", and this design overturns that ruling
deliberately and says so.

**The skirmisher-screen citation is poisoned and must never be reused.** The
review at lines 277 to 296 traces a widely repeated "skirmisher screen" claim to
a misreading: the source describes Limahong's *Chinese* force, not a Filipino
one. No document produced by this work may cite it.

### 2.2 The divergence, stated plainly

This design knowingly builds three things the evidence does not support, for
gameplay legibility, and labels all three. The label is not decoration: it is
the mechanism by which a future reader can tell what the game invented from what
the sources say.

| Behaviour | Evidence position | What the game does | Tier as shipped |
| --- | --- | --- | --- |
| Weapon-homogeneous contingents | Not attested; specialists in the record are separate allied peoples | A contingent is filled from one weapon cohort where the arithmetic allows | **Provisional reconstruction — gameplay model** |
| Shield bearers forward inside a contingent | Shield walls, ranks, and fixed depth explicitly unsupported; shields are ordinary equipment | Shield bearers take the forward-most slots of their own contingent only | **Gameplay model — no evidence tier at all** |
| Ranged warriors back away from close melee | Breaking and running is documented; a skirmish-and-retire role is not | A shooter with a melee enemy inside a threat radius steps directly away and resumes shooting when clear | **Provisional reconstruction — gameplay model** |

**The second row's tier was corrected on 2026-08-14, and the correction matters
more than it looks.** It previously read "Provisional reconstruction — gameplay
model", which joins an evidence tier to the statement that there is no evidence
tier. `docs/research/movement/README.md` is explicit that forward placement "is
none of Documented, Documented (form uncertain), or Provisional reconstruction —
it is a gameplay model, adopted for legibility", and `docs/research/battles/03-deep-past-formations-and-tactics.md`
and `docs/research/movement/tall-hardwood-shield.md` say the same. Under
`CLAUDE.md` section 7 the research documents are the historical authority and
this design is not, so the table now records what they record. A **Provisional
reconstruction** is a claim the evidence underdetermines; a **gameplay model** is
a shape adopted for legibility that the evidence does not support at all. Writing
the two joined by an em dash let the second borrow the first's credibility, which
is exactly the flattening this policy exists to prevent.

The distinction the third row rests on is worth stating in full, because it is
the one a careless reading would flatten. The sources record men with bows and
javelins *breaking* — losing the fight and running. They do not record a
recognised battlefield role whose job was to shoot, fall back in good order, and
shoot again. What this design implements is closer to the second than the first,
and calling it "documented because they ran at Bangkusay" would be dishonest.
It is a gameplay model, it is labelled as one, and the research document keeps
its negative finding unchanged.

### 2.3 An unmet obligation this change sharpens

`docs/research/ARMY-COMPOSITION.md` lines 503 to 513 record that whether a
household dependent — the *aliping namamahay* — was ever put into a battle line
is unresolved, that nothing in the corpus rules it in or out, and that a roster
fielding that class must say in the inspector that doing so is a reconstruction
rather than an attested fact. `PhilippineCombatPresetV5.cs` line 320 fields
`Itak + TallHardwood` at `RankId.AlipingNamamahay`, so the shipped ranged roster
already carries that obligation unmet.

Ask B makes it worse in a specific and visible way: a shield-bearing
*namamahay* is exactly the warrior this design moves to the forward-most slot of
its contingent. The game would be putting a class whose combat role is
unresolved at the visible front of a body of troops. The inspector note the
research document asked for therefore becomes part of this package rather than
staying deferred, and the plan carries a task for it.

## 3. The preset

Everything in this design is gated behind one new movement preset.

```
BattlefieldRealismV10 = 10
```

appended to `src/Hukbo.Core/Movement/MovementPresetId.cs` after
`MonotoneAllyClearanceV9 = 9`, and registered in
`src/Hukbo.Core/Movement/MovementPresetRegistry.cs` as a **verbatim restatement
of `RangedStandoffV8Ruleset`'s field values** with its own `id`. V8's values are
themselves V4's, so V10 inherits V4's cohesion tuning, `offsetUnit`, and arrival
taper, with `usesEquipmentRelativeFootwork: false`, `appliesPressureInterrupt:
false`, and an empty `loadoutMovementProfiles`.

### 3.1 Why a new preset rather than an edit

`CLAUDE.md` section 5 and the doc comment on `MovementPresetId` itself both say
it: changing enum numeric values, enum order, roster order, weights, or a hash
mixer requires a new preset version plus new golden expectations. All three
behaviours change simulated positions and therefore both hashes. There is no
version of this work that edits an existing preset.

### 3.2 Why the behaviour is gated on preset identity, not on a ruleset field

`RangedStandoffV8` and `MonotoneAllyClearanceV9` both do it this way already,
and the registry says why in V9's own doc comment: V9's single behavioural
difference "is gated entirely on preset identity inside `IsLaneClearOfAllies`
itself, not on any field this ruleset carries, exactly the way
`RangedStandoffV8Ruleset`'s own standoff behaviour is gated on preset identity
rather than a field."

Following that precedent has a concrete payoff. `MovementRuleset.ComputeContentHash`
(`src/Hukbo.Core/Movement/MovementRuleset.cs` lines 628 to 720) folds every
field it knows about. Adding a field and folding it unconditionally would move
the content hash of **every** registered preset, including the four with pinned
literals at `MovementPresetRegistryTests.cs` lines 33, 42, 51, 60, 69, 79, and
106, and including V6, whose content hash reaches the state hash. Adding a field
and folding it under a version gate — the pattern the pressure-interrupt block
at lines 647 to 663 uses — would work, but it buys nothing that a named constant
does not, and it adds a field that eight presets must declare zero for. So V10
carries no new field, and its tuning constants live in a pure static helper
described in section 5.3.

### 3.3 What V10 inherits from V8

V10 keeps V8's ranged standoff behaviour whole: a warrior whose selected target
lies beyond its weapon's `StandoffDistanceRaw` pursues with that distance as its
stop-short radius, and a warrior whose target lies at or inside it proposes no
movement and is assigned `AgentIntent.Holding`. That rule is currently written
as an equality test against `MovementPresetId.RangedStandoffV8` at
`src/Hukbo.Core/Simulation/BattleSimulation.cs` line 1725. It becomes a
two-value predicate. Widening a predicate from `p == V8` to `p == V8 || p == V10`
cannot change V8's behaviour, and the V8 frozen trajectory digest
(`tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-movement-v8-digest.json`) is
the proof that it did not.

### 3.4 Where the gate is wired

`Enum.GetValues<MovementPresetId>()` is enumerated by
`BattleSimulationTests.ExactlyOneLivingLeaderPerNonEmptyContingentAcrossEveryRegisteredMovementPreset`
(`tests/Hukbo.Core.Tests/BattleSimulationTests.cs` line 1733), which asserts
`MovementPresetRegistry.IsRegistered(preset)` for every enum value and then runs
a 200-agent battle on it with `CombatPresetId.PrecolonialPhilippinesV4` — a
melee-only roster. So V10 must be registered in the same change that adds the
enum value, and it must behave sanely with a roster that has no ranged weapon in
it at all. It does: with no ranged weapon, no warrior has a non-zero
`StandoffDistanceRaw`, so neither the standoff hold nor the retreat rule ever
fires, and only the deployment permutation of section 4 is observable.

## 4. Asks A and B — cohort deployment assignment

Asks A and B are one mechanism. Both decide *which warrior stands on which
already-planned slot*, and neither invents a position.

### 4.1 The shape, and why it is this shape

`FormationPlanner.PlanFactionDeployment`
(`src/Hukbo.Core/Simulation/FormationPlanner.cs` line 78) returns one array of
`(XRaw, YRaw, ContingentId)` indexed by faction-local warrior index. It draws
from the caller's `SplitMix64` stream exactly twice per warrior, inside
`PlaceMember` at lines 313 and 314, in ascending faction-local index order. The
deal that decides membership is one line — `localIndex % contingentSizes.Length`
at line 106 — and its comment at lines 100 to 103 states the reason it is a deal
rather than a contiguous run: `RosterCounts` groups one weapon category into a
contiguous run of faction-local indices, so contiguous contingents would come
out weapon-homogeneous, which the comment calls "a stronger claim than the
evidence supports". Ask A is a decision to make exactly that claim, deliberately
and with a label.

The planner is not modified. Instead, a new pure permutation runs after it, on
the array the planner already produced. This is precisely the shape
`EquipmentDeploymentAssignment` already uses for V6
(`src/Hukbo.Core/Movement/EquipmentDeploymentAssignment.cs`), whose own summary
says it best: "contingent membership, slot coordinates, the lattice, the jitter,
and the SplitMix64 draw count all stay exactly as planned — this is a pure
permutation of values the caller already holds, and it never draws."

Two consequences follow, and both are load-bearing.

- **The random stream is untouched.** The number of draws, their order, and
  their consumed values are identical to what they are today, for every preset
  including V10. `BattleSimulationTests.RosterCountsDoNotChangeTheRandomDrawSequenceForSpawnPositions`
  keeps its meaning.
- **`ContingentId` travels with the slot.** A slot carries the contingent it was
  planned into. Moving a warrior onto a different contingent's slot therefore
  moves the warrior into that contingent, which is what ask A needs, and it
  needs no change to how membership is stored or hashed. This is the one place
  where the new permutation is stronger than V6's, which deliberately permutes
  only within a contingent.

### 4.2 The new type

`src/Hukbo.Core/Movement/CohortDeploymentAssignment.cs`, an `internal static`
class in the mould of `EquipmentDeploymentAssignment`, exposing one method:

```csharp
internal static (int XRaw, int YRaw, int ContingentId)[] AssignForFaction(
    (int XRaw, int YRaw, int ContingentId)[] canonicalDeployment,
    ReadOnlySpan<CombatLoadout> loadoutsByFactionLocalIndex,
    CombatRuleset rules)
```

It never draws, never mutates its input, and returns a new array in which
element `i` is the slot the warrior at faction-local index `i` deploys on. It is
called once per faction from `BattleSimulation.Create`, on the canonical
unmirrored deployment, before the faction-1 X reflection at line 625 — the same
call site and the same ordering discipline V6 uses at lines 584 to 604.

### 4.3 Step 1 — the cohort key

Each warrior's **cohort key** is the index of its resolved loadout inside
`CombatRuleset.Roster`, found by the first exact match scanning ascending. The
roster is an ordered immutable array, so the key is a stable small integer and
its order is the canonical roster order. Two warriors carrying the same weapon,
armour, and shield share a cohort.

The resolved loadout comes from the caller's existing `ResolveSpawnLoadout`
local function (`BattleSimulation.cs` line 571), exactly as V6's assignment
does, so the ranking and the spawn can never disagree about a warrior's
equipment. Under an empty `RosterCounts` that is `rules.ResolveLoadout(entityId)`,
which is `(entityId - 1) % roster.Count` (`CombatRuleset.cs` line 517); under a
populated `RosterCounts` it is `rules.Roster[expandedRosterIndices[localIndex]]`,
whose runs `RosterCountExpansion.Expand` already made contiguous.

Grouping is by full loadout, not by weapon alone. Two warriors with the same
weapon and different shields are different cohorts, which is what makes ask B
resolvable inside a contingent rather than fighting ask A across contingents.

### 4.4 Step 2 — cohorts meet contingents

Two orderings, then a positional pairing.

- **Cohorts** are ordered by member count descending, then cohort key ascending.
- **Contingents** are ordered by slot count descending, then contingent id
  ascending.

Then the cohort-ordered warrior list — warriors sorted by their cohort's rank,
then by faction-local index ascending inside a cohort — is paired positionally
against the contingent-ordered slot list. Each contingent therefore receives one
contiguous run of the cohort-ordered list.

This is the assignment that minimises the number of cohorts split across
contingents, which is the honest way to state what ask A can actually deliver.
When a cohort is at least as large as a contingent, that contingent comes out
purely of one weapon. When the arithmetic does not divide — and with the shipped
ranged roster it does not; `Scenario.CreateDefault(seed, totalAgents: 200)`
gives 100 warriors per faction dealt into five contingents of twenty, against a
roster whose row count does not divide 100 — each contingent comes out dominated
by one cohort with a minority tail, and at most `contingentCount - 1` cohorts
are split anywhere in the army.

**This is a real limitation and it must not be described as anything else.** The
smoke row for ask A therefore asks whether each group *reads as* a body of one
weapon, not whether it is one. Making it exact would require the contingent
count to follow the cohort count, which means changing
`FormationPlanner.ResolveContingentSizes`, which changes every planned position
under every preset. That is out of scope here and is recorded in section 12.

### 4.5 Step 3 — shield bearers forward, inside the contingent

Once a contingent's warrior set is fixed by step 2, the warriors are paired
against that contingent's own slots.

- **Slots** are ordered by depth: `XRaw` descending, then `YRaw` ascending, then
  original slot index ascending.
- **Warriors** are ordered by shield-bearing first — a warrior whose loadout
  declares a shield sorts ahead of one that does not — then by cohort key
  ascending, then by faction-local index ascending.

Pairing those two orders positionally puts the shield bearers on the
forward-most slots of their own contingent, and leaves everything else in a
stable, fully determined order.

`XRaw` descending is forward for *both* factions, which is worth spelling out
because it is not obvious. The planner produces one canonical deployment in the
left half of the map; faction 0 spawns on it directly and faction 1 spawns on
`mapWidthRaw - leftXRaw` (`BattleSimulation.cs` line 625). A canonical slot with
a large `XRaw` is near the centre line for faction 0 and, after reflection, also
near the centre line for faction 1. The centre line is where the enemy is. So
ranking canonical slots by `XRaw` descending is "toward the enemy" on both
sides, and the rule is mirror-safe by construction with no faction-specific
branch anywhere in the assignment.

### 4.6 Determinism of the sorts

Every sort key chain ends in a distinct integer — the original slot index for
slots, the faction-local index for warriors — so no two elements ever compare
equal and the unstable `Array.Sort` is deterministic. This is the identical
argument `EquipmentDeploymentAssignment` records in its own remarks, and the
identical failure mode it guards: an unstable sort with a tie is a hash that
depends on the runtime's sorting implementation.

There is no `Dictionary`, no `HashSet`, and no iteration over an unordered
collection anywhere in the assignment. Cohort membership is counted into a flat
array indexed by cohort key, which is bounded by `rules.Roster.Length`.

### 4.7 What is observable, and what is not

The permutation changes `AgentState.XRaw`, `AgentState.YRaw`, and
`AgentState.ContingentId` at spawn. All three are already hashed fields written
by `CreateAgent` (`BattleSimulation.cs` line 1004). No new state is introduced,
no field is added to `AgentState`, and the state-hash field order is unchanged.
Only the values differ, and only under V10.

### 4.8 The spawn repair pass is unchanged and stays safe

`ResolveSpawnPlacement` (`BattleSimulation.cs` line 884) and
`TryRelocateSpawn` (line 937) run after both spawn loops and resolve any
overlapping bodies first-come-wins over ascending `EntityId`. The permutation
moves warriors between slots but never creates a slot, so the set of occupied
coordinates is exactly the set the planner produced and
`FormationPlannerTests.NoTwoBodiesComeWithinContactBeforeTheFirstTick` remains
true by construction. The repair pass stays a no-op under V10 for the same
reason it is one today.

## 5. Ask C — the ranged retreat

### 5.1 What is there today

`GatherMovementProposals` (`BattleSimulation.cs` line 1678) is the only stage
that writes a movement proposal, and `CommitMovement` (line 3830) is the only
stage that moves an agent. The V8 standoff block sits at lines 1725 to 1760 and
is a one-sided ladder on a single already-computed distance:

- target at or inside the shooter's `StandoffDistanceRaw` — assign
  `AgentIntent.Holding`, write no proposal, `continue`;
- target beyond it — pursue with the standoff distance as the stop-short
  radius, or sidestep if the collision stage has raised a stall generation.

Nothing in that ladder asks whether anything dangerous is close. A shooter whose
target is a distant archer will stand and shoot while a man with a kampilan
walks into its body.

### 5.2 The insertion

One rung is added below the existing hold, turning the two-way ladder into a
three-way one at the same site, on the same already-computed geometry:

1. **a melee enemy inside the threat radius** — back away, assign the new
   `AgentIntent.BackingAway`, write a retreat proposal;
2. **otherwise, target at or inside the standoff distance** — `Holding`, no
   proposal. Unchanged from V8;
3. **otherwise** — pursue with stop-short, or sidestep. Unchanged from V8.

Rungs 2 and 3 keep their exact current code, so a V10 battle in which no shooter
is ever threatened is behaviourally identical to the same battle under V8. That
is a testable statement and the plan carries a test for it.

### 5.3 What "a melee enemy inside the threat radius" means, exactly

**Which enemy.** Not the shooter's selected target. The whole point of ask C is
that the thing about to kill the shooter is usually *not* the thing the shooter
is aiming at. The trigger reads the nearest living enemy whose weapon is melee,
regardless of whether it is the selected target.

**Where that comes from without adding a query.** `SelectTargetsAndIntents`
(line 1130) already walks every agent for every agent, and already computes the
squared distance to each surviving perception-passing candidate at line 1241.
V6's local-context accumulation is fused into that same loop at exactly that
point, "so the already-computed squared distance is reused". The retreat threat
query is fused in beside it: when the *actor* carries a ranged weapon and the
*candidate* carries a melee one, the candidate's squared distance is minimised
into a per-agent scratch row. Zero new scans, zero new distance computations,
zero new allocations per tick.

**Where it is stored.** A new scratch array on `BattleSimulation`, alongside
`_localMovementContexts`, holding one `long` per agent — the squared distance to
the nearest closing melee enemy, or `long.MaxValue` for none. It is allocated
once in the constructor, **sized zero under every preset except V10**, cleared
and overwritten every tick by `SelectTargetsAndIntents`, and it is **never
hashed and never snapshotted**. It is derived data in exactly the sense
`_localMovementContexts` is, and it carries the same guarantee: the stage that
writes it runs before the stage that reads it in every tick, including the first
tick after a resume, so there is no path on which a stale or absent value is
read.

**The radius.** Derived from the shooter's own `StandoffDistanceRaw`, scaled by
a provisional basis-point constant. No new `WeaponProfile` field and no new
`MovementRuleset` field — section 6.2 is the full argument. The arithmetic lives
in a new pure static helper, `src/Hukbo.Core/Movement/RangedRetreatRules.cs`, in
the mould of `MovementRules` and `MovementRouteRules`, so it is unit-testable
without a simulation:

```csharp
internal static int ThreatRadiusRaw(int standoffDistanceRaw);
internal static bool IsThreatened(long nearestMeleeSquared, int threatRadiusRaw);
```

`ThreatRadiusBasisPoints` is a named `const` on that class, marked in its own
doc comment as a provisional gameplay-tuning value under `CLAUDE.md` section 7
and not a historical measurement. The initial value is half the standoff
distance — `5_000` basis points — which places the trigger comfortably inside
the distance at which the shooter is still able to shoot, so a shooter that
backs off resumes firing immediately rather than having to walk back.

**Why no hysteresis band and no memory.** The decision is a pure, monotone
function of the current distance, so it cannot oscillate on its own: a shooter
inside the radius steps away, and if that step clears the radius it stops
stepping and shoots. If the pursuer keeps closing, the shooter keeps stepping.
That is exactly the "back-pedal, resume shooting when clear" behaviour decision
2 asks for, and it needs no remembered state. This matters more than it looks:
a remembered "was retreating" bit would be authoritative state, would have to be
added to `AgentState`, would move the state-hash field layout, and would have to
be snapshotted and restored. Not needing it is why this change adds no field to
`AgentState` at all.

### 5.4 The retreat proposal

`BuildMovementProposal` (line 4667) only builds a step *toward* a point. A
retreat is built by reflecting the threat through the actor: the destination is
`(2 * agentX - threatX, 2 * agentY - threatY)`, which is the point directly
opposite the threat at the same distance, and the same overload then produces a
correctly-paced, correctly-tapered, correctly-clamped step toward it. There is
no new movement arithmetic and no new clamping path.

The retreat is **strictly away**. There is no lateral component, no strafing,
and no route search. A shooter that cannot go straight back does not go
sideways.

### 5.5 The three hazards, and the rule for each

**Hazard one — the map edge silently swallows the retreat.**
`CollisionGeometry.ClampCenterToBounds` (called at lines 4706 and 4710) clamps
the proposed centre into the arena. A cornered shooter would therefore propose a
step that clamps to where it already is, and would appear to be standing there
doing nothing while carrying a "backing away" intent — a lie the spectator can
see. **Rule: when the clamp bites, the shooter stops retreating.** The retreat
builder compares the clamped result against the unclamped one; if they differ on
either axis, no proposal is written and the warrior is assigned `Holding`
instead of `BackingAway`. It stands and shoots. This is a deliberate, visible
behaviour — "a cornered warrior turns and fights" — and it has its own smoke row.

**Hazard two — a retreat re-enters the blocked-streak and stall-escape path.**
A held warrior writes no proposal and the collision stage resolves it to
`MovementResolution.None`, which is why the V8 comment at lines 1734 to 1743 can
say "the blocked streak never starts". A retreating warrior does write a
proposal, so a blocked retreat starts a blocked streak, and the stall-escape
branch at line 1745 would then build a *sidestepping pursuit* proposal — that
is, it would send the shooter toward the very thing it is running from. **Rule:
the stall generation is not consulted on the retreat rung.** A shooter whose
retreat cannot be committed simply does not retreat that tick; on the next tick
the same pure distance test runs again from wherever it actually is. The
sidestep path stays reachable only from rung 3, exactly as it is under V8.

**Hazard three — the infinite kite.** Section 8 is entirely about this.

### 5.6 The new intent

`AgentIntent.BackingAway = 6`, appended after `Holding = 5` in
`src/Hukbo.Core/Simulation/AgentIntent.cs`. The enum is hashed through
`StateHasher`, so the value is append-only and
`BattleSimulationTests.AgentIntentNumericValuesArePinned` is **extended with the
new value, never renumbered**.

`AgentIntent.Holding` is not reused, and this is not a stylistic preference. Its
doc comment carries an explicit contract: exactly one producer in the whole
codebase, the ranged standoff hold arm, and it "may never be written by a
rejection, a collision, a blocked proposal, or a failed route search", because
conflating "chose not to advance" with "could not advance" is the verified cause
of the V6/V7 standoff. A warrior that is running away has chosen a third thing,
and it needs a third value. `BackingAway` gets its own single-producer contract
in its own doc comment: the retreat rung of the V10 ladder, and nothing else.

Two existing mechanisms are deliberately **not** reused, for reasons the ranged
design already recorded at lines 1233 to 1236: `FootworkPhase.Disengage` and
`TacticalPosture.Withdraw` are unreachable under V8, and both aim at the nearest
*ally* — they mean "fall back toward friends", not "get away from that man".
`RangedPhase` is presentation-only, derived in `UpdateViews` and never hashed,
so it cannot carry a decision at all.

## 6. Determinism impact

### 6.1 The random stream does not move

The `SplitMix64` stream is opened once at `BattleSimulation.Create` line 553 and
is consumed only by `FormationPlanner.PlanFactionDeployment`, twice per warrior,
in ascending faction-local index order. This design does not touch the planner,
its dealing loop, its lattice fit, its anchor arithmetic, or either jitter draw.
The cohort assignment of section 4 runs after the stream is finished with and
never receives a `ref SplitMix64` at all — it cannot draw, because it has
nothing to draw from. The retreat rule of section 5 runs inside the tick
pipeline, which has no random stream.

**This is an acceptance condition on every implementation task, not a hope.**
The plan states it as such, and the existing test
`BattleSimulationTests.RosterCountsDoNotChangeTheRandomDrawSequenceForSpawnPositions`
plus the nine frozen trajectory digests are what would catch a violation.

### 6.2 `CombatRuleset.ContentHash` does not move, and no new combat preset is needed

This was an open question in the brief that produced this design, and it was
resolved by reading the code rather than by preference.

`CombatRuleset.AddProfile` (`src/Hukbo.Core/Combat/CombatRuleset.cs` lines 654
to 687) folds `DamagePerAttack`, `AttackRangeRaw`, and `AttackCooldownTicks`
unconditionally, and then folds `ProjectileSpeedRaw`, `StandoffDistanceRaw`, and
`FlightTickCeiling` **only inside an `isRangedDeclaration` branch**. Its comment
explains exactly why: folding them unconditionally "would move every one of
those presets the moment a ranged tuning value changed, even though none of them
reference it."

That gives three possible shapes for a per-weapon retreat radius, and each was
followed to its consequence.

- **Add a field to `WeaponProfile` and do not fold it.** The content hash would
  not move — the fold enumerates fields explicitly and would simply ignore the
  new one. This is the *worst* option, not the cheapest: two rulesets differing
  only in that field would hash identically, so a replay or a save would accept
  a configuration that produces different battles. That is precisely the hole
  the content hash exists to close.
- **Add a field and fold it under a `!= 0` version gate**, in the ranged style.
  Correct, but the only weapons that would declare a non-zero retreat radius are
  the ranged weapons of `PrecolonialPhilippinesV5`, so **V5's content hash
  moves**. That forces a new `PrecolonialPhilippinesV6`, a new registry entry, a
  new pinned content-hash literal, a re-pointed client pairing, a re-pointed
  gate block, and new golden expectations for every test that names V5 —
  including `PhilippineCombatIntegrationTests.ShieldedRosterEntriesAbsorbMoreBlowsBeforeDyingThanShieldlessOnesAcrossSeedsOneThroughTwenty`.
  A large, risky change bought for one tuning number.
- **Derive the radius from the existing `StandoffDistanceRaw`.** No new field,
  no fold change, no content-hash movement, no new combat preset, and the value
  is already per-weapon and already in the shooter's hand at the call site.

**Verdict: derive from `StandoffDistanceRaw`.** `WeaponProfile`
(`src/Hukbo.Core/Combat/WeaponProfile.cs` lines 86 to 96, with its ranged
invariants at lines 140 to 184) is not modified by this work, `CombatRuleset` is
not modified, and there is no `PrecolonialPhilippinesV6`.

The one thing this costs is expressiveness: every ranged weapon's threat radius
is the same fraction of its own standoff distance, so a bow and an arquebus
cannot be tuned to flinch at different multiples. If per-weapon tuning is ever
genuinely needed, the second option above is the route and it is a new combat
preset — recorded in section 12 as an open item rather than smuggled in here.

### 6.3 `MovementRuleset.ContentHash` does not move for any existing preset

V10 adds no field to `MovementRuleset`, so `ComputeContentHash` is not modified
and every existing preset folds the identical byte sequence it folds today. The
four pinned content-hash literals in `MovementPresetRegistryTests.cs` at lines
33, 42, 51, 60, 69, 79, and 106 stay exactly as they are. V8 and V9 carry no
content-hash pin there; V10 follows them and adds none, so that the pinning
convention is not silently changed by this work.

### 6.4 The nine frozen digests are the leak detector

`tests/Hukbo.Core.Tests/Fixtures/` holds nine trajectory digests, v1 through v9,
plus the pre-clash digest, replayed by `MovementPresetFreezeTests`. Every one of
them must stay green through this entire package. They are the mechanism that
proves the V10 gates leak nowhere: if a single `if` is written in the wrong
place — outside a preset check, or on a shared helper without a gate — one of
those nine digests goes red and names the preset it leaked into.

**No frozen digest may be recaptured in this work.** The capture siblings at
`MovementPresetFreezeTests.cs` lines 603 and 627 exist for creating a *new*
preset's digest, not for re-baselining an existing one. A red v1-through-v9
digest is a defect in this change, never a fixture that needs updating.

A tenth digest for V10 is **not** created. V10's behaviour is being introduced,
not frozen, and freezing a digest in the same change that invents the behaviour
pins nothing more than "the code does what the code does". The evidence for V10
is the headless gate workload of section 9 and the measured termination sweep of
section 8, both of which have a stated pass bar rather than a self-referential
one.

### 6.5 What does move

Under V10 and only under V10: agent spawn positions, agent `ContingentId`
values, the resulting state hash, the resulting event hash, the ordered event
stream, and the battle outcome. That is the entire point of a new preset
version, and `CLAUDE.md` section 5 authorises it precisely because the version
is new.

## 7. The mirror

`BattleSimulation.Create` plans one deployment and mirrors it: faction 1 spawns
at `mapWidthRaw - leftXRaw` with the same `YRaw` and the same `ContingentId`
(lines 622 to 646). `FormationPlannerTests.BothFactionsDeployAsExactMirrorsAcrossTheVerticalCentreLine`
asserts that per-index equality directly on `simulation.Agents`.

### 7.1 The decision

**The cohort assignment is mirror-safe by construction, and it preserves the
exact per-index mirror whenever the two factions resolve the same loadout
sequence by faction-local index. Under the default rotating roster it does not,
and that is accepted rather than worked around.**

Three facts make this the right answer rather than a shrug.

**First, the assignment itself has no faction in it.** It ranks the canonical,
unmirrored slots, and section 4.5 showed that `XRaw` descending means "toward
the enemy" for both factions after the reflection. Both factions rank the same
slots the same way. The only input that can differ between the two is the
loadout sequence.

**Second, when the loadout sequences match, the permutation matches exactly, so
the mirror is exact.** A scenario with a populated `RosterCounts` always
produces matching sequences, because `RosterCountExpansion.Expand` is a pure
function of the counts and the spawn loops index it by *faction-local* index for
both factions — the comment at `BattleSimulation.cs` lines 629 to 635 records
that this is deliberate, so that the two factions never get different armies.

**Third, under an empty `RosterCounts` the sequences already differ today, and
V6 already accepted exactly this.** The default resolution is
`(entityId - 1) % roster.Count`, and faction 1's entity ids are offset by
`AgentsPerFaction`, so the two factions' faction-local loadout sequences are
rotations of each other and coincide only when the roster length divides the
per-faction count. `EquipmentDeploymentAssignment` records the identical
position in its own remarks at lines 16 to 23: equal faction-local loadout
multisets keep the exact mirror, and "default round-robin rosters, whose
faction-local loadout multisets can differ, are not required to mirror."

### 7.2 What this means for the existing assertions

**No existing mirror assertion breaks.**
`BothFactionsDeployAsExactMirrorsAcrossTheVerticalCentreLine` builds its
scenario with `Scenario.CreateDefault(seed, totalAgents)` and never overrides
the movement preset, so it runs on `PersistentContingentsV4` and never reaches
V10 at all. `EquipmentFormationAssignmentTests.V4SpawnPositionsMatchThePlannedDeploymentIdentically`
is likewise about V4. Both stay green, untouched, and neither is weakened.

The plan therefore adds two **new** V10 assertions rather than editing an
existing one:

- under V10 with a populated `RosterCounts`, the two factions are exact mirrors
  per faction-local index — the same assertion, on the preset that needed
  proving;
- under V10 with the default rotating roster, the two factions are **not**
  required to mirror, and the test records that as the expected, documented
  outcome with a comment pointing at this section, so that a later reader cannot
  mistake it for an undiscovered bug.

The second of those needs care in the writing. A test that asserts "these are
not equal" passes for the wrong reason if the permutation silently stops running
altogether. It is therefore written as a *positional-equivalence* assertion
instead: every occupied coordinate on one side has a mirrored counterpart on the
other, and each faction's own contingents are internally weapon-grouped and
shield-forward — so the test fails if the assignment stops working, and passes
only where the sides genuinely differ in which entity id stands where.

## 8. Termination, and the anti-kite bound

### 8.1 Why this is the highest risk in the package

`docs/plans/2026-08-07-ranged-units-design.md` lines 1237 to 1239 name a ranged
fall-back rule as "the single most likely thing to break the termination bar",
and the archived ranged units plan lines 475 to 477 demand measured
termination numbers rather than a green gate. A shooter that retreats faster
than a melee warrior advances is never caught; two armies of them never meet;
the battle runs to the tick cap and ends in a draw that looks like a bug because
it is one.

### 8.2 The four bounds, all memory-free

1. **The retreat is strictly away, never lateral.** A shooter cannot orbit a
   pursuer or slide along a wall, so its path away from a given pursuer is
   monotone and the arena is finite.
2. **The cornered rule of section 5.5.** When the bounds clamp bites, the
   shooter stops retreating and stands. A shooter driven into an edge or a
   corner fights there; it does not vibrate against the boundary.
3. **A blocked retreat is not retried through the stall path.** Section 5.5,
   hazard two. A shooter that cannot back away stands and shoots this tick.
4. **The threat radius is a fraction of the standoff distance, so it is small.**
   The shooter is still inside its own weapon's reach when it triggers, so
   backing off restores its ability to shoot rather than requiring it to run to
   a new firing position. The retreat is a step, not a flight.

Note that none of the four bounds the *total* distance retreated over a battle,
and none of them needs to: the arena is bounded and the retreat is monotone away
from the pursuer, so a pursued shooter reaches an edge in a bounded number of
ticks and then bound 2 fires.

### 8.3 The measured bar, and the number that means failure

A green gate is not evidence here. The plan carries an explicit measurement
task, run through `scripts/benchmark.ps1` against V10 with the ranged combat
preset, over **seeds 1 through 20**, 200 agents, a 10,000-tick cap, recording
`measuredTicks`, `outcome`, and both hashes per seed into
`docs/development/testing.md`.

**Failure is any of the following.** Each is a number, so the result cannot be
argued about.

- **Any** seed reaching the 10,000-tick cap without a terminal outcome.
- The seed-1 measured tick count exceeding **1,962** ticks, twice the 981-tick
  figure the recorded canonical baseline gives for the existing workload
  (`docs/development/testing.md` lines 87 to 112). A retreat rule that doubles
  the length of a battle is a kite even when it terminates.
- The **median** measured tick count across the twenty seeds exceeding **3,000**.
- Fewer than one victory for **each** faction across the twenty seeds — the same
  bar `RangedTerminationTests.SeedsOneThroughTwentyProduceVictoriesForBothFactionsUnderRangedStandoff`
  already applies to V8. A preset where one side always wins is a broken preset
  even if every battle ends.

If the bar fails, the response is to tune `ThreatRadiusBasisPoints` downward and
re-measure, and — if that does not carry it — to report the failure and stop.
**The bar is not to be moved to fit the measurement.** Weakening a stated
termination bar to make a change land is the exact failure mode the V6/V7
standoff work was written up to prevent.

## 9. Making the change visible, and the gate

### 9.1 The client pairing

`src/Hukbo.Client/ArenaGame.cs` lines 1437 and 1438 hardcode the running game's
scenario to `CombatPresetId.PrecolonialPhilippinesV5` and
`MovementPresetId.RangedStandoffV8`, with a reasoning comment at lines 1414 to
1416 stating that V5 is only ever paired with V8. Those two lines are what makes
this work visible to a person who runs `./scripts/run.ps1`, and the comment
above them must be updated in the same edit — a stale comment claiming V5 pairs
only with V8, sitting directly above a line pairing it with V10, is worse than
no comment.

`ArenaGame.cs` is a shared seam that other sessions touch. It is its own task
with its own file set and it is never handed to a parallel agent.

### 9.2 The gate: a third block, not a repointed one

`scripts/verify.ps1` runs **two** headless workloads today, not one (lines 37 to
62): the unconditional default block, which is the workload behind the recorded
baseline `stateHash 1B73FC5923879AA0 / eventHash AC55684F24D39344`, and a
Hukbo-guarded ranged block pinned to `PrecolonialPhilippinesV5` +
`RangedStandoffV8`. The comment at lines 45 to 51 says why the second exists:
without it, "a completely broken ranged path ... would leave this gate green."

Two options were considered.

- **(a) Keep the V8 block and add a third block for V10.** Both existing pieces
  of evidence survive. Costs one more 200-agent / 10,000-tick run in every gate.
- **(b) Repoint the V8 block to V10.** No added gate cost, but it retires the V8
  evidence entirely and needs a newly recorded baseline in place of the old one.

**Decision: (a), add a third block.** The reasoning is the one the existing
comment already makes for the second block. V8 remains a registered, reachable,
frozen preset whose digest fixture this package must not move; keeping its gate
block means a regression that breaks V8 is caught by the gate and not only by a
unit test. And under (a), the V8 frozen digest and the V8 gate workload together
stay the leak detector for the V10 gates — which is the whole safety argument of
section 6.4. The cost is one additional headless run whose seed-1 battle, on the
existing evidence, terminates in the low thousands of ticks.

`tests/Hukbo.Client.Tests/ScriptDefaultsTests.cs` string-pins the contents of
**`verify.ps1`** — not `run.ps1`, which contains no preset string at all — and
three of its assertions move under option (a): the benchmark-invocation count at
line 31 goes from 2 to 3, the `Game = $Game` pass-through count at line 97 goes
from 3 to 4, and the class summary and the ranged-block assertions at lines 45
to 48 gain the V10 block. That is one task, and because a `scripts/*.ps1` edit
can turn the C# client suite red, both suites are run after it.

### 9.3 The recorded baseline

`docs/development/testing.md` gains a new recorded result section for the V10
workload — measured ticks, outcome, survivor counts, both hashes, and the
determinism verdict — in the same form as the existing "Canonical gate result —
Hukbo, 2026-08-09" section at lines 87 to 112. The existing baseline is **not**
edited: the default block still runs `PersistentContingentsV4` and its numbers
must come back byte-identical, which is itself a result worth recording as
unchanged.

## 10. Spectator discoverability

`CLAUDE.md` section 6 and `SIMULATION-GAME-STANDARDS.md` section 10 both require
that a spectator can discover an effect without reading source code. Each of the
three asks is answered separately, because they fail differently.

**Ask A — weapon grouping — is discoverable from two cues that already exist.**
The eight per-contingent ground tints already distinguish one body from another
at the default camera fit, and the weapon silhouette grammar of
`docs/research/HISTORICAL_1500s_WEAPONS.md` lines 109 to 120 already makes a
kampilan pawn distinguishable from a busog pawn without clicking either. Put
together, a body of one weapon standing on one tint is exactly the thing the
spectator sees change. The inspector's existing `Contingent: <n> — <state>` row
confirms membership on click.

**Ask B — shields forward — is discoverable from the shield pawn's own
silhouette.** A shield bearer draws a tall solid block beside its torso
(`HISTORICAL_1500s_WEAPONS.md` line 119), which is the single most legible
element on a pawn at the default fit. The forward edge of a contingent reading
as a row of those blocks is visible without any new rendering. A second,
stronger cue arrives on its own a few seconds later: shield bearers take the
first blows, so the first casualties are visibly at the front. That is a smoke
row of its own.

**Ask C — the retreat — needs new presentation, because intent is invisible.**
Three additions, all of them presentation only:

1. **A distinct inspector string.** `AgentInspectorContent` maps
   `AgentIntent.Holding` to "Holding at range" (line 412). `BackingAway` maps to
   its own wording — "Backing away from close fighters" — and must not collapse
   into the default arm at line 430, which returns "Holding" for unmapped
   values. A retreating warrior reading as "Holding" in the inspector would be
   the exact conflation section 5.6 exists to prevent, surfaced to the player.
2. **Visible backwards motion, checked rather than built.** The client has no
   pose mapping keyed on `AgentIntent` — the only non-inspector consumer is
   `ArenaAutoPan.cs` line 149, which tests for `Attacking` — and the pawn's gait
   is driven by its position delta rather than by any simulation movement field.
   A backing-away warrior therefore animates as moving without any new code.
   What is *not* known in advance is whether it reads as backing away or as
   turning and fleeing, which depends on how facing is derived. This design does
   **not** add a pose: it adds a smoke row that asks a person to judge it, and
   if the answer is that it reads wrong, that becomes a follow-up with its own
   evidence rather than speculative work done here.
3. **A per-faction count in the battle report.** `BattleReport` already carries
   `HoldingCount` and `BattleReportAccumulator.UpdateHoldingCounts` already
   recomputes it every ingest without retaining it across ticks. A
   `BackingAwayCount` follows that exact pattern, so the HUD can show that a
   faction's shooters are being driven back as a number rather than only as a
   thing to notice.

**And the label.** Decision 1 requires that all three read as a gameplay model.
The inspector gains an evidence-tier badge and a plain-language note on the rows
that carry these behaviours — the contingent row and the intent row — reading as
a provisional reconstruction rather than an attested practice. The badge
mechanism already exists for weapon variants (smoke row 119 exercises "the
selected variant's evidence tier, and its note"), so this is a new consumer of
an existing mechanism rather than a new mechanism.

None of the three presentation additions may decide anything. The client never
decides targeting, damage, retreat, or victory — `CLAUDE.md` section 3 — and the
retreat decision is taken wholly inside `GatherMovementProposals`.

## 11. The nine acceptance questions

Answered in the order `SIMULATION-GAME-STANDARDS.md` section 10 asks them.

**1. User-visible outcome.** Under the new preset, and in the running game after
the client pairing is repointed: each side deploys as several bodies each
dominated by one weapon rather than as evenly-mixed slices; shield bearers stand
at the forward edge of their own body and take the first blows; and a warrior
with a bow, javelin, or arquebus steps directly back when a melee enemy closes
inside its threat radius, then resumes shooting once the threat is clear or once
it is cornered.

**2. Tick stage and state read/written.** Deployment: `BattleSimulation.Create`,
before the first tick, writing `XRaw`, `YRaw`, and `ContingentId` — all existing
hashed fields. Threat observation: `SelectTargetsAndIntents`, stage 2 of the
tick, writing one derived scratch row per agent, reading positions and loadouts
it already reads. Retreat decision: `GatherMovementProposals`, writing
`AgentIntent` — an existing hashed field — and one movement proposal. Movement
itself: `CommitMovement`, unchanged. No new tick stage, and no change to stage
order.

**3. Numeric units, bounds, and same-tick conflict rule.** All distances are
`FixedPoint` raw units; all comparisons are on squared raw distances in `long`
arithmetic with `checked` products, exactly as the existing standoff test at
lines 1731 to 1732 does. The threat radius is
`StandoffDistanceRaw * ThreatRadiusBasisPoints / 10_000`, bounded below by zero
and above by the standoff distance itself. Same-tick conflicts are resolved by
the existing collision stage, which this change does not modify; a retreat
proposal competes for space on exactly the same terms as a pursuit proposal, and
a retreat that loses is dropped for that tick rather than retried.

**4. Total ordering and random-stream policy.** Every sort in the cohort
assignment terminates in a distinct index (section 4.6). The threat scan
minimises over candidates in ascending agent-array order and ties break on the
lower `EntityId`, which is the repository-wide rule. The random stream is not
touched at all (section 6.1).

**5. Cache source and invalidation.** One derived scratch array, one `long` per
agent, sized zero under every preset but V10, overwritten in full every tick by
the stage that owns it, never hashed and never snapshotted. It is not a cache in
the sense `CLAUDE.md` section 9 forbids — nothing is retained across ticks and
nothing grows — and it rebuilds identically from authoritative state on the
first tick after a resume.

**6. Save, event, and version effect.** New preset version
`BattlefieldRealismV10 = 10`. New append-only enum value
`AgentIntent.BackingAway = 6`, hashed through `StateHasher`, with its pin
extended rather than renumbered. No new snapshot field, no new event type, and
no change to the event schema. Existing saves are unaffected because they record
their own preset id and V1 through V9 are untouched.

**7. Worst-case complexity and benchmark workload.** The cohort assignment is
`O(n log n)` in warriors per faction, once per battle, on 100 elements at the
canonical workload — negligible against the planner it follows. The threat
observation adds a constant number of integer comparisons inside a scan the
simulation already performs, so the per-tick asymptotic cost is unchanged and
the added constant is measurable only against the existing 200-agent workload.
The benchmark workload is the canonical one — 200 agents, 10,000 ticks, seed 1 —
plus the twenty-seed termination sweep of section 8.3.

**8. Spectator explanation.** Section 10 in full: contingent tint plus weapon
silhouette for ask A, the shield block silhouette and first-casualty pattern for
ask B, and a distinct intent string, a distinct pose, and a HUD count for ask C,
with an evidence-tier badge and a gameplay-model note on both affected inspector
rows.

**9. Tests that fail before and pass after.** Named per task in the plan
document. The load-bearing ones: the cohort assignment produces
weapon-dominated contingents and shield-forward depth under V10 and the exact
identity permutation under every other preset; the draw sequence and draw count
are unchanged under V10; a V10 battle with no threatened shooter is
event-identical to the same battle under V8; a shooter with a melee enemy inside
the threat radius ends the tick further from that enemy than it began it; a
cornered shooter is assigned `Holding` and not `BackingAway`; a retreating
shooter never accumulates a blocked streak; the twenty-seed termination sweep
meets the section 8.3 bar; and all nine frozen digests plus every existing
preset content-hash pin stay green.

## 12. Open items and things deliberately left alone

- **Contingent count and size derivation.** `docs/research/ARMY-COMPOSITION.md`
  lines 548 to 578 already flag the equal split across contingents as
  historically wrong against evidence describing unequal, chief-driven
  followings. Fixing it would change every planned position under every preset
  and is out of scope here. It is also the change that would make ask A exact
  rather than approximate, which makes it the natural successor to this work.
- **Per-weapon retreat tuning.** Section 6.2 records why every ranged weapon
  shares one fraction of its own standoff distance, and what a per-weapon field
  would cost.
- **`ThreatRadiusBasisPoints` is not calibrated.** It is a provisional starting
  value chosen to sit inside the standoff distance. Section 8.3's sweep is what
  decides whether it survives.
- **Shield-forward depth interacts with the ally-clearance work.** V10 does not
  set `usesEquipmentRelativeFootwork`, so none of the V6 through V9 footwork
  machinery runs under it and the interaction is untested by construction. A
  future preset combining the two is a separate design.
- **The `aliping namamahay` battle-role question** is not resolved by this work
  and cannot be; section 2.3 only requires that the inspector stop being silent
  about it.

## 13. Corrections to the research this design was handed

The brief that commissioned this design was assembled from earlier agent
reports. Everything in it was re-read against the code at commit `c13b696`
before being used. Two things were wrong, and both are corrected above rather
than carried forward.

1. **The preset string is pinned in `verify.ps1`, not `run.ps1`.** All three
   tests in `ScriptDefaultsTests` read `verify.ps1`; `run.ps1` contains no
   preset string at all. Section 9.2 is written against the real file.
2. **The gate runs two headless workloads, not one.** The recorded baseline
   `1B73FC5923879AA0 / AC55684F24D39344` belongs to the first, unconditional
   block only; the second, Hukbo-guarded block already runs
   `PrecolonialPhilippinesV5` + `RangedStandoffV8`. Section 9.2 states which of
   the two available responses this design takes and why.

Everything else in the brief was confirmed against the code, including the two
jitter draws per warrior, the round-robin deal and its comment, the V6
zero-draw permutation and its sort-key structure, the single-producer contract
on `AgentIntent.Holding`, the one-sided standoff compare, the
`ClampCenterToBounds` hazard, the unreachability of `FootworkPhase.Disengage`
under V8, the presentation-only status of `RangedPhase`, the nine frozen
digests, the seven pinned movement content hashes, and the absence of a
content-hash pin for V8 and V9. Two smaller points are worth recording precisely
because they change what a task has to do:

- **No existing mirror assertion breaks**, because both mirror tests build
  their scenarios from `Scenario.CreateDefault` and never reach V10. Section 7.2
  replaces "update the assertion" with "add two new ones".
- **A new preset must be registered in the same change that adds the enum
  value**, because
  `BattleSimulationTests.ExactlyOneLivingLeaderPerNonEmptyContingentAcrossEveryRegisteredMovementPreset`
  enumerates `Enum.GetValues<MovementPresetId>()` and asserts every value is
  registered. Splitting the enum value and the registration across two tasks
  leaves the suite red in between.

