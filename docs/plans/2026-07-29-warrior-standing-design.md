# Warrior rank — design

Date: 2026-07-29
Amended: 2026-07-29 (Decisions section added; renamed Standing to Rank
throughout; see the Decisions section for the full record)
Status: design only. This document does not authorize implementation.
Evidence base: [`docs/research/HISTORICAL_1500s_RANKS.md`](../research/HISTORICAL_1500s_RANKS.md),
[`docs/research/ARMY-COMPOSITION.md`](../research/ARMY-COMPOSITION.md)

Leadership specifics — the leader-follower relationship, the leader marker,
and the full nine-question acceptance answer for leadership on its own — are
written out in
[`2026-07-29-leader-rank-design.md`](2026-07-29-leader-rank-design.md). This
document keeps its own leadership section (6.3) as a summary and a
cross-reference rather than duplicating that material.

## Decisions (2026-07-29)

Five open questions blocked this design. All five are answered. Every later
task, and every other document in this set, assumes these answers; a task
that finds itself guessing has hit a question these decisions do not cover
and must stop rather than pick.

**1. Naming.** Use **Rank** everywhere: `RankId` is the enum, `AgentState.Rank`
and `AgentView.Rank` are the fields, `CombatLoadout.Rank` is the roster
tuple's fourth element, and the inspector line reads `Rank:`. This supersedes
the `StandingId` / "Standing" naming this design used when it was written,
throughout this document, not just in the code. The document is amended, not
left contradicting the code it describes.

This overturns the naming decision §1 originally made, for a reason worth
recording: the word "rank" was already in use in the formation sense
(`ContingentState`'s documentation), and this design chose "standing" for the
type layer specifically to avoid colliding with it. The user's answer to open
question 1 was that a single word everywhere is worth the cost of reworking
the formation-sense sentences, rather than carrying two words for one
English concept through the codebase indefinitely. That reworking is in
scope and is three known, explicit collisions, not an open-ended rename:

a) `src/Hukbo.Core/Simulation/ContingentState.cs:9` — "no agent is ever
   assigned to a rank, a file, or a named formation slot" is a load-bearing
   sentence about the collision contract, not an aside. It is reworded to say
   *formation rank* explicitly, so it stays true and unambiguous once
   `RankId` exists: "no agent is ever assigned to a formation rank, a file,
   or a named formation slot."

b) `src/Hukbo.Core/Simulation/FormationPlanner.cs:20` — "Regular files,
   ranks, fixed frontage, depth doctrine, shield walls and named formations
   are explicitly *not attested* and are not claimed here." The word "ranks"
   here means the same formation sense as (a) and is reworded the same way:
   "Regular files, formation ranks, fixed frontage, ...".

c) `src/Hukbo.Client/Presentation/BattleReport.cs:14` already declares an
   `int Rank` field meaning leaderboard placement (also
   `BattleReportAccumulator.cs:146` and `BattleReportPanel.cs:373`). This is
   a different type in a different part of the codebase and does not collide
   at compile time with `RankId`, but the word now has two live meanings in
   the same solution — social rank and leaderboard rank — and every doc
   comment or UI string near either one must say which it means. The battle
   report's `Rank` keeps its existing name; no code changes there. This
   design's inspector line reads `Rank:` for social rank and is drawn nowhere
   near the battle report panel, so the two never appear adjacent on screen,
   but the collision is recorded here so a future reader does not assume they
   are the same field.

**2. Weapon-to-rank assignment.** The table in section 6.1 stands exactly as
written: Datu carries the Kampilan, Maharlika the Wasay, Timawa the Kalis,
Namamahay the Itak. No other assignment was preferred. Every place this
document said "weapon-to-standing" now reads "weapon-to-rank"; the content of
the table is unchanged.

**3. Phase B ships.** Standing-aware, now rank-aware, leadership selection is
in scope, not dropped. Its determinism cost is larger than section 6.3
originally described — see the amendment to that section below and the full
account in `2026-07-29-leader-rank-design.md` — and it remains its own
sequenced phase for exactly that reason: one hash move, one cause, verified
on its own.

**4. Default roster fields the householder class.** The default roster is
four entries: Datu, Maharlika, Timawa, Namamahay. `Ayuey` stays declared and
not rostered, on the same principle preset V3 already uses for its
unreachable paired weapon profiles. Fielding `Namamahay` means the inspector
must state, wherever a `Namamahay` warrior is shown, that a dependent class
standing in the battle line is `docs/research/HISTORICAL_1500s_RANKS.md`'s
own caveat made concrete: Plasencia records maritime and agricultural
service for the *aliping namamahay* and gives no explicit war obligation for
them the way he does for the *maharlica*. This is why row four of the section
4 table already carries "Yes, flagged as a reconstruction" rather than a bare
"Yes", and that flag stays.

**5. Per-rank attributes.** Expressed only through systems this design
already has: loadout eligibility (section 6.1), level and therefore combo
depth (section 6.2), and leadership eligibility (section 6.3). There is **no**
direct combat-strength multiplier and no per-rank damage or hit-point number
— section 3's argument against a multiplier stands, unchanged, as the reason.

A fourth candidate attribute was on the table: how many followers a rank can
hold, i.e. a follower-capacity number attached to `Datu`. That is **not**
being built now. It depends on the contingent-shape work —
`docs/research/ARMY-COMPOSITION.md` §11.1's finding that contingent count and
size should be evidence-backed rather than a square-root split — which is a
larger, separately evidenced change to the most heavily tested surface in the
repository (`FormationPlanner`). Follower capacity is deferred to
[`2026-07-29-contingent-shape-design.md`](2026-07-29-contingent-shape-design.md)
as a design question there, and it is explicitly **not** scheduled for
implementation in this pass.

## 1. What is being proposed, and what is not

A warrior in Hukbo currently has a weapon, an armor, a shield, and a global
placeholder level that every warrior in the battle shares. This design adds
one authoritative per-warrior property — **rank** — that says what that
warrior's social position was in the force that assembled, and it wires that
property to exactly four things the historical record actually supports:
who leads, who follows whom, what equipment a warrior carries, and how the
spectator is told about it.

**It does not add a strength multiplier per class.** The research document is
unambiguous that no sixteenth-century source grades fighting ability by
social class, and section 3 below explains why expressing rank through
equipment and following is both the honest reading and the better game.

**It does not add morale, rout, campaign state, promotion, or experience.**
`CLAUDE.md` section 9 defers morale until the gate authorizes it, and the
4X layer must stay out of `Hukbo.Core` entirely. Rank here is static roster
data set once at spawn, exactly as `CombatLoadout` already is.

### A naming problem, and the decision actually taken

The word *rank* was already in use in this codebase when this design was
first written. `ContingentState`'s documentation used it in the formation
sense — "no agent is ever assigned to a rank, a file, or a named formation
slot" — and that sentence is a load-bearing statement about the collision
contract, not an aside. This design originally avoided the collision by
naming the social-status enum `StandingId` and reserving "Rank" only for the
player-facing inspector line.

The user's answer to open question 1, recorded above in the Decisions
section, overturned that: use `RankId` and "Rank" for both the type layer and
the player-facing label, and rework the three sentences that used "rank" in
the formation sense to say "formation rank" instead. That rework is scoped
and explicit — items (a) through (c) in the Decisions section above — not an
open-ended search-and-replace across the repository. `BattleReport.Rank`
(leaderboard placement) is a distinct type that is not renamed; the two
meanings of "rank" now coexist in the solution and every place they could be
confused is named above.

## 2. User-visible outcome

A spectator who opens the agent inspector on any warrior sees a new line:

```
Rank: Datu — Chief (Documented, Tagalog)
```

and, on a warrior who is currently leading a contingent, the existing leader
indication now correlates with that line rather than with an invisible
entity-id ordering (once phase B, described in section 6.3, ships).

A third surface was originally proposed here: army composition panel
categories labelled by rank as well as by weapon. That surface is **not**
part of this pass. `src/Hukbo.Client/UI/ArmyCompositionPanel.CategoryLabels`
is a hardcoded six-entry string array shaped for `PrecolonialPhilippinesV2`'s
roster specifically — one label per V2 roster entry, including the
solo/shielded duplication V2 uses for its one-handed weapons — and
`ArmyCompositionPanelTests.cs` pins both the count and the content to V2.
`Scenario.CombatPreset` keeps its current default in this pass (section 6.1),
so the panel is drawing V2's roster regardless of what V4 declares; making it
rank-aware would mean either switching the shipped default to V4 first
(explicitly deferred, section 6.1) or generalizing `CategoryLabels` and its
sibling constants (`ArmyCompositionStepper.CategoryCount`,
`Hukbo.Client.Settings.ArmyComposition.CategoryCount`) to be derived from
whichever preset is active instead of hardcoded to one. Both are real, scoped
pieces of work that belong to a later task once V4 is a candidate default,
not a byproduct of adding a field to `CombatLoadout`. This is recorded here as
a decided scope cut, not a silent drop: the promise from the original design
is deferred, not broken, and the follow-up work is named so it is not lost.

The discoverability question in `SIMULATION-GAME-STANDARDS.md` section 10 —
*can a spectator discover this effect without reading source code?* — is
still answered without the panel: the inspector line is present for every
warrior in phase A, and once phase B ships, the observable fact that a
contingent re-forms around its chief rather than around whichever warrior
happens to hold the lowest entity id is visible directly in the battle view,
with no panel required. Section 8's answer to acceptance question 8 records
this explicitly.

## 3. Why rank does not become a damage multiplier

The temptation is a table of the form "chief: 1.3× damage, freeman: 1.0×,
householder: 0.8×". Three reasons not to.

**It is not in the sources.** Section "What the sources do not say" of the
research document lists the absence explicitly. A multiplier would be a
gameplay invention wearing a historical label, which is precisely what
`CLAUDE.md` section 7 forbids.

**The evidence points somewhere better.** Morga's chief who is "more
courageous than others in war… enjoyed more followers and men; and the
others were under his leadership, even if they were chiefs" describes
distinction converting into *following*, not into personal lethality. The
mechanic the record supports is about the shape of the force, which is
exactly the layer Hukbo already simulates through contingents.

**The differentiation already exists and is already tuned.** Preset V3 gives
the four weapons distinct damage, reach, cooldown, combo, and clash values.
Letting rank decide *which* loadout a warrior carries produces a real,
visible strength difference between a chief and a householder without
inventing a single new number, and it routes that difference through values
that are already labelled provisional and already covered by tests.

So: **rank selects equipment and leadership; equipment and leadership
produce the strength difference.**

## 4. The rank ladder

Five values, region-scoped, each carrying the tier the research document
assigns. Numeric values are pinned from the day they ship because they enter
the content hash and the state hash.

| Value | Pair-form label | Region | Tier | Fielded by default |
| --- | ---: | --- | --- | --- |
| `Datu = 1` | Datu — Chief | Tagalog and Visayan | Documented | Yes, one or two per faction |
| `Maharlika = 2` | Maharlika — Sworn Freeman | Tagalog | Documented | Yes |
| `Timawa = 3` | Timawa — Bound Freeman | Visayan | Documented | Yes |
| `Namamahay = 4` | Namamahay — Householder | Tagalog | Documented | Yes, flagged as a reconstruction |
| `Ayuey = 5` | Ayuey — Household Dependent | Visayan | Documented, form uncertain | No — declared, not rostered |

Two notes carried straight from the research.

`Maharlika` and `Timawa` are **not** two grades of one ladder. They are the
Tagalog and Visayan words for comparable positions in two different regional
systems, and the sources disagree about what the second one means. They
occupy separate enum values so that a faction can be given a coherent
regional character later without the enum having to change; they must never
be described in UI as one being above the other. (See the "timawa trap" in
`docs/research/ARMY-COMPOSITION.md` §4.3, cross-referenced from
`docs/research/HISTORICAL_1500s_RANKS.md` as part of the documentation
reconciliation task in the plan document.)

`Ayuey` is declared but not rostered, on the same principle preset V3 already
uses for its unreachable paired weapon profiles: the value exists so the
catalog is complete and so a later preset can field it, and the fact that no
roster entry resolves it today is stated in the source comment. Fielding a
dependent class in a battle line is an inference the sources do not make, and
the research document requires the inspector to say so if it is ever done.

## 5. Where rank lives

**Authoritative, in `Hukbo.Core`:**

- `RankId` — a new enum in `Combat/CombatIdentity.cs`, beside `WeaponId`,
  `ArmorId`, and `ShieldId`, carrying the same "do not renumber or reorder"
  contract and the same per-value evidence commentary those enums already
  carry.
- `CombatLoadout` gains a `Rank` field, making a roster entry
  (weapon, armor, shield, rank). This keeps rank on the same object
  the simulation already threads from roster to agent to view, and it makes
  `Scenario.RosterCounts` a rank-aware composition control at zero extra
  cost, because the roster index already selects the whole tuple.
- `AgentState.Rank` — written once at spawn from the resolved loadout,
  never mutated, exactly like `Loadout` and `ContingentId`.
- `AgentView.Rank` — so the client can read it without reaching into
  simulation internals.

**Presentation, in `Hukbo.Client`:**

- A rank label catalog beside the existing weapon and shield catalogs,
  holding the pair-form label, the region scope, and the evidence tier. The
  tier vocabulary (`VisualEvidenceTier`) and the inspector formatting
  helpers already exist and are reused rather than duplicated.

**Nowhere:** no new derived cache, no new spatial structure, no per-rank
lookup on a hot path. Rank is read at spawn and at inspector draw time.

## 6. What rank actually changes

### 6.1 Equipment, through the roster — combat preset V4

A new preset `PrecolonialPhilippinesV4` fields a roster in which rank and
loadout are paired. The values below are copied unchanged from V3 for every
weapon; the only new authored data is which rank carries which weapon.

| Roster index | Rank | Weapon | Shield |
| ---: | --- | --- | --- |
| 0 | Datu | Kampilan | None |
| 1 | Maharlika | Wasay | None |
| 2 | Timawa | Kalis | None |
| 3 | Namamahay | Itak | None |

This is the minimal, defensible starting assignment: the longest and most
prestigious blade goes to the chief, the field-and-utility blade to the
householder, and the two freeman classes take the middle. It is a **gameplay
tuning choice, not a historical claim** — no source assigns a weapon to a
class — and it must be commented as such in the preset source, in the same
terms preset V3 already uses for its own attribute values. The Decisions
section above records that this assignment is the one shipped; no alternative
was preferred.

Preset V4 also restates every V3 value rather than referencing it, following
the freeze convention V2 and V3 already established. V1, V2, and V3 stay
registered and byte-identical so their replays keep reproducing.

`CombatLoadout` gaining a required fourth field is a wide, mechanical edit:
every construction site the compiler names must state a rank rather than
inherit a default. This touches roughly 24 files across `Hukbo.Core`,
`Hukbo.Client`, and both test projects — the three existing preset sources
(`PhilippineCombatPreset.cs`, `PhilippineCombatPresetV2.cs`,
`PhilippineCombatPresetV3.cs`), and test files including
`HitLocationResolverTests.cs`, `BattleSimulationTests.cs`, and
`DeterminismTests.cs`. That edit is deliberately **one serial task** in the
plan document, not split across parallel implementers, because a partially
migrated `CombatLoadout` does not compile and there is no safe midpoint to
divide the work at. Presets V1, V2, and V3 declare `RankId.Timawa` on every
roster entry — a single value across the whole roster, so those presets carry
no rank differentiation at all and their hashes are provably unmoved once the
content-hash and state-hash folds are gated on the active preset declaring
rank data.

### 6.2 Combo depth, through level — combat preset V4

`Scenario.PlaceholderFighterLevel` is today a single value applied to every
warrior, and its only effect is to bound the maximum length of an attack
combination alongside `WeaponProfile.ComboMaxSteps`. Preset V4 replaces the
global value with a per-rank level, resolved at spawn.

Proposed: Datu 3, Maharlika 2, Timawa 2, Namamahay 1, Ayuey 1.

This is the one place rank touches a combat number directly, and it needs
to be described accurately. The historical justification is **not** that a
chief swings harder; it is that the combo chain is already the game's
representation of a fighter pressing an advantage, and giving the man who is
distinguished in war a longer chain is the least invented way to make that
distinction visible. The numbers are provisional tuning values with no
evidentiary standing whatsoever, they may not be cited back into the research
document, and the inspector must not present them as historical.

`Scenario.PlaceholderFighterLevel` is kept, unchanged in meaning, for
presets that declare no per-rank levels — that is, V1 through V3 — so
that nothing about those presets moves.

### 6.3 Leadership, through the contingent leader scan — movement preset V5

`MovementRules.ScanContingentLeadersAndLivingCounts` currently picks the
living member with the lowest entity id as a contingent's leader. Under a new
movement preset `PersistentContingentsV5`, it picks the living member with
the **highest rank** — that is, the lowest `RankId` numeric value — breaking
ties on the lowest entity id exactly as today.

This is presented as the direct implementation of the Morga passage in the
original version of this design. That framing is corrected here: Morga
describes authority *accreting* to a chief with a stronger war record over
time — "such a one enjoyed more followers and men" — not a rule for who takes
over when the leading chief falls mid-battle. `docs/research/ARMY-COMPOSITION.md`
§7 lists "a rule for replacing a fallen leader" explicitly among the things
this corpus does **not** establish. The highest-rank-survivor rule this
section describes is therefore labelled **Provisional reconstruction**, not
Documented: it is the most conservative, least-invented option available —
rank order is at least attested even though the succession rule built on top
of it is not — and it stays the shipped behavior for that reason, but it must
not be presented in code comments or in the inspector as a directly attested
historical rule.

This change also has a materially larger determinism blast radius than a
comparator swap in the leader scan. Verified against the current code:

- `MovementRules.cs:403` — the leader never receives a cohesion destination.
  Changing who leads a contingent therefore changes the movement resolution
  of at least two agents on the tick leadership changes: the former leader,
  who newly receives a cohesion destination, and the new leader, who newly
  stops receiving one.
- Cohesion squares are centred on the leader's position
  (`FormationRules.IsCohesionSquareWithinBounds`,
  `FormationRules.DoCohesionSquaresOverlap`), so a different leader means a
  different square origin for legality checks affecting the whole contingent,
  not just the leader.
- Rally direction and the rally trail are computed from the leader's
  position, so both move with a leadership change.
- `PersistentContingentsV4`'s narrowed cross-contingent scan reads
  `leader.ContingentState` (`BattleSimulation.cs`, near line 1135). Under V5
  this means the state hash moves through deployment geometry — which agent
  ends up adjacent to which contingent's leader — and not only through the
  leader-selection tie-break itself.

In short: this is not "swap the comparator, move the hash once." It is a
change that reaches into cohesion geometry, rally computation, and the
narrowed cross-contingent scan already shipped under V4. It remains its own
sequenced phase for that reason, verified in full on its own, with its own
goldens. The full leadership design — the leader-follower relationship this
implements, the leader marker, and the acceptance answers for leadership on
its own — is in
[`2026-07-29-leader-rank-design.md`](2026-07-29-leader-rank-design.md).

`PersistentContingentsV4` and every earlier movement preset stay registered
and unmodified.

### 6.4 Deployment — explicitly out of scope

Making `FormationPlanner.PlanFactionDeployment` deal a chief into every
contingent, rather than dealing warriors into contingents without regard to
rank, is the obvious next step and is deliberately **not** in this design. It
changes deployment geometry, which is the most heavily tested surface in the
repository, and it should be its own design with its own evidence review.
Until then a contingent may have several chiefs or none, and the leader scan
simply picks the ranking survivor of whoever is present. See
`docs/plans/2026-07-29-contingent-shape-design.md` for how this connects to
the broader Phase C question of contingent shape.

## 7. Determinism impact

Two independent hashes are affected, and the
`hukbo-determinism-change` skill governs the procedure.

**Content hash.** `CombatRuleset.ComputeContentHash` folds each roster entry
as (weapon, armor, shield). It gains a fourth fold for rank, and a new
optional block for per-rank levels, both placed *after* every existing
block and both contributed only by a preset that declares them — the same
technique that already keeps V1's hash intact when V2 added weapon
attributes and V3 added a clash profile. V1, V2, and V3 must produce their
currently pinned content hashes after this change, and the existing freeze
tests are what proves it.

**State hash.** `StateHasher` folds agent state. Rank is authoritative
per-agent state and must be folded. This moves the state hash for every seed
under the new preset and requires new golden expectations for V4. Under V1
through V3 the fold must contribute nothing at all, so their goldens do not
move; the cleanest way to guarantee that is for those presets to resolve
every warrior to a single `RankId` value and for the fold to be gated on
the preset declaring rank, mirroring the content-hash treatment above.

Phase B moves the state hash a second time, for the reasons enumerated in
section 6.3 above — not merely because the leader-selection comparator
changed, but because leader identity now drives cohesion-square placement,
rally computation, and the V4 cross-contingent scan's read of
`leader.ContingentState`.

**Ordering and randomness.** No new random draw. No new multi-result query.
The one new ordered comparison in phase A — none, since phase A adds no new
comparator — and phase B's leader scan's rank-then-entity-id tie-break is a
total order over a set that is already deterministic.

**Pinned vectors.** `SplitMix64`'s test vectors are untouched. No mixer
changes.

## 8. Answers to the nine acceptance questions

1. **User-visible outcome.** Inspector rank line with pair-form label,
   region, and evidence tier; contingents that re-form around their chief
   (phase B). The composition-panel surface originally proposed here is
   deferred — see section 2 — so the answer to this question rests on the
   inspector line and, once phase B ships, on the observable leader-change
   behavior in the battle view; both hold without the panel.
2. **Tick stage and state read/written.** Written once at agent construction
   in `BattleSimulation.Create`, from the resolved roster entry. Read by the
   contingent leader scan at the start of the movement stage (phase B) and by
   the client at draw time. No tick stage mutates it.
3. **Numeric units and bounds.** `RankId` is an enum with five pinned
   values, 1 through 5. Per-rank level is an integer of at least 1,
   validated at preset construction on the same terms
   `Scenario.PlaceholderFighterLevel` is validated today. No same-tick
   conflict is possible on an immutable field.
4. **Total ordering and random-stream policy.** No new stream. The leader
   scan orders by `(RankId ascending, EntityId ascending)`, a total order
   because entity ids are unique.
5. **Cache source and invalidation.** No cache. The per-rank level table
   is immutable preset data built once at construction, on the same terms as
   the existing effective-weight tables, which the ruleset documentation
   already distinguishes from a runtime cache.
6. **Save, event, and version effect.** New preset ids
   `PrecolonialPhilippinesV4` and, in phase B, `PersistentContingentsV5`.
   `BattleSnapshot` gains rank per agent. No new event type: rank
   does not fire events, it conditions who leads and what they carry.
7. **Worst-case complexity and benchmark workload.** O(1) per agent at spawn;
   O(1) added to the existing per-agent leader scan, which is already a
   single linear pass. The canonical 200-agent, 10,000-tick, seed-1 workload
   is the benchmark, with a 500-agent result reported as section 10 requires.
8. **Spectator explanation.** The inspector line is the primary surface, and
   it exists on its own without needing the composition panel. The leader
   change (phase B) is observable directly in the battle view.
9. **Tests that fail before and pass after.** Listed per task in the plan
   document; the load-bearing ones are the V1/V2/V3 content-hash freeze
   tests (which must keep passing untouched), a new V4 content-hash golden, a
   new seed-1 state-hash golden for V4, a leader-selection test that pins the
   rank-then-id tie-break, and a client test asserting the inspector line
   for each of the five ranks.

## 9. Sequencing

Three phases, each independently verifiable, each moving at most one hash for
one reason.

**Phase A — rank exists and is visible.** `RankId`, the
`CombatLoadout` field, `AgentState`/`AgentView`, preset V4 with the
rank-paired roster and per-rank levels, the client label catalog, and the
inspector line. Content hash moves for V4 only; state hash moves for V4
only; V1 through V3 are proven frozen. The composition panel is explicitly
out of phase A — see section 2.

**Phase B — rank decides who leads.** Movement preset V5 and the leader
scan change, with the wider blast radius recorded in section 6.3 and in
`2026-07-29-leader-rank-design.md`. State hash moves again, for reasons
enumerated there.

**Phase C — deferred, not designed here.** Standing-aware, now rank-aware,
deployment; contingent shape sized from evidence rather than a square root;
follower capacity (Decisions, item 5); rank-aware appearance selection (the
client already has elite and leader appearance rows chosen by a rarity
weight, which rank could replace with something authoritative); any Visayan
or Tagalog faction character. Its own document is
[`2026-07-29-contingent-shape-design.md`](2026-07-29-contingent-shape-design.md).

## 10. Resolved questions

The four open questions this design originally posed, plus the fifth carried
in from the commissioning prompt, are answered in full in the **Decisions**
section at the top of this document, dated 2026-07-29. This section is kept,
renamed from "Open questions", so that a reader following an old link to
"section 10" lands somewhere that still explains what happened to the
question that used to be there, rather than a section that has vanished.

1. **Naming** — resolved. See Decisions, item 1.
2. **Weapon-to-rank assignment** — resolved. See Decisions, item 2.
3. **Should phase B ship at all?** — resolved. See Decisions, item 3.
4. **Does the default roster field a householder class at all?** — resolved.
   See Decisions, item 4.
5. **Per-rank attributes** (carried in from the commissioning prompt, not
   originally numbered in this design) — resolved. See Decisions, item 5.
