# Warrior standing — design

Date: 2026-07-29
Status: design only. This document does not authorize implementation.
Evidence base: [`docs/research/HISTORICAL_1500s_RANKS.md`](../research/HISTORICAL_1500s_RANKS.md)

## 1. What is being proposed, and what is not

A warrior in Hukbo currently has a weapon, an armor, a shield, and a global
placeholder level that every warrior in the battle shares. This design adds
one authoritative per-warrior property — **standing** — that says what that
warrior's social position was in the force that assembled, and it wires that
property to exactly four things the historical record actually supports:
who leads, who follows whom, what equipment a warrior carries, and how the
spectator is told about it.

**It does not add a strength multiplier per class.** The research document is
unambiguous that no sixteenth-century source grades fighting ability by
social class, and section 3 below explains why expressing standing through
equipment and following is both the honest reading and the better game.

**It does not add morale, rout, campaign state, promotion, or experience.**
`CLAUDE.md` section 9 defers morale until the gate authorizes it, and the
4X layer must stay out of `Hukbo.Core` entirely. Standing here is static
roster data set once at spawn, exactly as `CombatLoadout` already is.

### A naming problem, decided up front

The word *rank* is already taken in this codebase. `ContingentState`'s
documentation uses it in the formation sense — "no agent is ever assigned to
a rank, a file, or a named formation slot" — and that sentence is a
load-bearing statement about the collision contract, not an aside. Naming a
social-status enum `RankId` would make that sentence ambiguous the day it
ships.

This design therefore uses **standing** for the type names (`StandingId`,
`AgentState.Standing`, `WarriorStanding`) and reserves the player-facing word
"Rank" for the inspector line, where there is no formation to confuse it
with. If the user prefers a single word everywhere, the alternative is to
rename the formation sense instead, which is a larger and more invasive
change and is not proposed here.

## 2. User-visible outcome

A spectator who opens the agent inspector on any warrior sees a new line:

```
Standing: Datu — Chief (Documented, Tagalog)
```

and, on a warrior who is currently leading a contingent, the existing leader
indication now correlates with that line rather than with an invisible
entity-id ordering. A spectator who opens the army composition panel sees the
roster categories labelled by standing as well as by weapon, and can field a
force of, for example, two chiefs, forty sworn freemen, and fifty-eight
householders per faction.

The discoverability question in `SIMULATION-GAME-STANDARDS.md` section 10 —
*can a spectator discover this effect without reading source code?* — is
answered by three independent surfaces: the inspector line, the composition
panel category names, and the observable fact that a contingent re-forms
around its chief rather than around whichever warrior happens to hold the
lowest entity id.

## 3. Why standing does not become a damage multiplier

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
Letting standing decide *which* loadout a warrior carries produces a real,
visible strength difference between a chief and a householder without
inventing a single new number, and it routes that difference through values
that are already labelled provisional and already covered by tests.

So: **standing selects equipment and leadership; equipment and leadership
produce the strength difference.**

## 4. The standing ladder

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
be described in UI as one being above the other.

`Ayuey` is declared but not rostered, on the same principle preset V3 already
uses for its unreachable paired weapon profiles: the value exists so the
catalog is complete and so a later preset can field it, and the fact that no
roster entry resolves it today is stated in the source comment. Fielding a
dependent class in a battle line is an inference the sources do not make, and
the research document requires the inspector to say so if it is ever done.

## 5. Where standing lives

**Authoritative, in `Hukbo.Core`:**

- `StandingId` — a new enum in `Combat/CombatIdentity.cs`, beside `WeaponId`,
  `ArmorId`, and `ShieldId`, carrying the same "do not renumber or reorder"
  contract and the same per-value evidence commentary those enums already
  carry.
- `CombatLoadout` gains a `Standing` field, making a roster entry
  (weapon, armor, shield, standing). This keeps standing on the same object
  the simulation already threads from roster to agent to view, and it makes
  `Scenario.RosterCounts` a standing-aware composition control at zero extra
  cost, because the roster index already selects the whole tuple.
- `AgentState.Standing` — written once at spawn from the resolved loadout,
  never mutated, exactly like `Loadout` and `ContingentId`.
- `AgentView.Standing` — so the client can read it without reaching into
  simulation internals.

**Presentation, in `Hukbo.Client`:**

- A standing label catalog beside the existing weapon and shield catalogs,
  holding the pair-form label, the region scope, and the evidence tier. The
  tier vocabulary (`VisualEvidenceTier`) and the inspector formatting
  helpers already exist and are reused rather than duplicated.

**Nowhere:** no new derived cache, no new spatial structure, no per-standing
lookup on a hot path. Standing is read at spawn and at inspector draw time.

## 6. What standing actually changes

### 6.1 Equipment, through the roster — combat preset V4

A new preset `PrecolonialPhilippinesV4` fields a roster in which standing and
loadout are paired. The values below are copied unchanged from V3 for every
weapon; the only new authored data is which standing carries which weapon.

| Roster index | Standing | Weapon | Shield |
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
terms preset V3 already uses for its own attribute values.

Preset V4 also restates every V3 value rather than referencing it, following
the freeze convention V2 and V3 already established. V1, V2, and V3 stay
registered and byte-identical so their replays keep reproducing.

### 6.2 Combo depth, through level — combat preset V4

`Scenario.PlaceholderFighterLevel` is today a single value applied to every
warrior, and its only effect is to bound the maximum length of an attack
combination alongside `WeaponProfile.ComboMaxSteps`. Preset V4 replaces the
global value with a per-standing level, resolved at spawn.

Proposed: Datu 3, Maharlika 2, Timawa 2, Namamahay 1, Ayuey 1.

This is the one place standing touches a combat number directly, and it needs
to be described accurately. The historical justification is **not** that a
chief swings harder; it is that the combo chain is already the game's
representation of a fighter pressing an advantage, and giving the man who is
distinguished in war a longer chain is the least invented way to make that
distinction visible. The numbers are provisional tuning values with no
evidentiary standing whatsoever, they may not be cited back into the research
document, and the inspector must not present them as historical.

`Scenario.PlaceholderFighterLevel` is kept, unchanged in meaning, for
presets that declare no per-standing levels — that is, V1 through V3 — so
that nothing about those presets moves.

### 6.3 Leadership, through the contingent leader scan — movement preset V5

`MovementRules.ScanContingentLeadersAndLivingCounts` currently picks the
living member with the lowest entity id as a contingent's leader. Under a new
movement preset `PersistentContingentsV5`, it picks the living member with
the **highest standing** — that is, the lowest `StandingId` numeric value —
breaking ties on the lowest entity id exactly as today.

This is the direct implementation of the Morga passage, and it is the change
with the largest visible effect: a contingent forms up around its chief, and
when the chief falls, leadership passes to the ranking survivor rather than
to an arbitrary one.

It is also the change with the highest determinism cost, because it moves the
state hash for every seed. **It is therefore proposed as a separate,
sequenced phase**, so that the roster and inspector work can be verified
against a hash that moves for one reason at a time. See section 9.

`PersistentContingentsV4` and every earlier movement preset stay registered
and unmodified.

### 6.4 Deployment — explicitly out of scope

Making `FormationPlanner.PlanFactionDeployment` deal a chief into every
contingent, rather than dealing warriors into contingents without regard to
standing, is the obvious next step and is deliberately **not** in this
design. It changes deployment geometry, which is the most heavily tested
surface in the repository, and it should be its own design with its own
evidence review. Until then a contingent may have several chiefs or none,
and the leader scan simply picks the ranking survivor of whoever is present.

## 7. Determinism impact

Two independent hashes are affected, and the
`hukbo-determinism-change` skill governs the procedure.

**Content hash.** `CombatRuleset.ComputeContentHash` folds each roster entry
as (weapon, armor, shield). It gains a fourth fold for standing, and a new
optional block for per-standing levels, both placed *after* every existing
block and both contributed only by a preset that declares them — the same
technique that already keeps V1's hash intact when V2 added weapon
attributes and V3 added a clash profile. V1, V2, and V3 must produce their
currently pinned content hashes after this change, and the existing freeze
tests are what proves it.

**State hash.** `StateHasher` folds agent state. Standing is authoritative
per-agent state and must be folded. This moves the state hash for every seed
under the new preset and requires new golden expectations for V4. Under V1
through V3 the fold must contribute nothing at all, so their goldens do not
move; the cleanest way to guarantee that is for those presets to resolve
every warrior to a single `StandingId` value and for the fold to be gated on
the preset declaring standing, mirroring the content-hash treatment above.

**Ordering and randomness.** No new random draw. No new multi-result query.
The one new ordered comparison — the leader scan's standing-then-entity-id
tie-break in phase 2 — is a total order over a set that is already
deterministic.

**Pinned vectors.** `SplitMix64`'s test vectors are untouched. No mixer
changes.

## 8. Answers to the nine acceptance questions

1. **User-visible outcome.** Inspector standing line with pair-form label,
   region, and evidence tier; standing-labelled composition categories;
   contingents that re-form around their chief (phase 2).
2. **Tick stage and state read/written.** Written once at agent construction
   in `BattleSimulation.Create`, from the resolved roster entry. Read by the
   contingent leader scan at the start of the movement stage (phase 2) and by
   the client at draw time. No tick stage mutates it.
3. **Numeric units and bounds.** `StandingId` is an enum with five pinned
   values, 1 through 5. Per-standing level is an integer of at least 1,
   validated at preset construction on the same terms
   `Scenario.PlaceholderFighterLevel` is validated today. No same-tick
   conflict is possible on an immutable field.
4. **Total ordering and random-stream policy.** No new stream. The leader
   scan orders by `(StandingId ascending, EntityId ascending)`, a total order
   because entity ids are unique.
5. **Cache source and invalidation.** No cache. The per-standing level table
   is immutable preset data built once at construction, on the same terms as
   the existing effective-weight tables, which the ruleset documentation
   already distinguishes from a runtime cache.
6. **Save, event, and version effect.** New preset ids
   `PrecolonialPhilippinesV4` and, in phase 2, `PersistentContingentsV5`.
   `BattleSnapshot` gains standing per agent. No new event type: standing
   does not fire events, it conditions who leads and what they carry.
7. **Worst-case complexity and benchmark workload.** O(1) per agent at spawn;
   O(1) added to the existing per-agent leader scan, which is already a
   single linear pass. The canonical 200-agent, 10,000-tick, seed-1 workload
   is the benchmark, with a 500-agent result reported as section 10 requires.
8. **Spectator explanation.** The inspector line is the primary surface. The
   leader change is observable directly in the battle view. The composition
   panel makes the roster itself legible before the battle starts.
9. **Tests that fail before and pass after.** Listed per task in the plan
   document; the load-bearing ones are the V1/V2/V3 content-hash freeze
   tests (which must keep passing untouched), a new V4 content-hash golden, a
   new seed-1 state-hash golden for V4, a leader-selection test that pins the
   standing-then-id tie-break, and a client test asserting the inspector line
   for each of the five standings.

## 9. Sequencing

Three phases, each independently verifiable, each moving at most one hash for
one reason.

**Phase A — standing exists and is visible.** `StandingId`, the
`CombatLoadout` field, `AgentState`/`AgentView`, preset V4 with the
standing-paired roster and per-standing levels, the client label catalog,
the inspector line, and the composition panel labels. Content hash moves for
V4 only; state hash moves for V4 only; V1 through V3 are proven frozen.

**Phase B — standing decides who leads.** Movement preset V5 and the leader
scan change. State hash moves again, for one reason.

**Phase C — deferred, not designed here.** Standing-aware deployment,
standing-aware appearance selection (the client already has elite and leader
appearance rows chosen by a rarity weight, which standing could replace with
something authoritative), and any Visayan or Tagalog faction character.

## 10. Open questions for the user

1. **Naming.** `Standing` for the type and "Rank" for the player-facing
   label, as proposed — or a single word everywhere, at the cost of renaming
   the formation sense of "rank"?
2. **Weapon-to-standing assignment.** The section 6.1 table is the minimal
   defensible guess. Any other assignment is equally unhistorical and
   equally legitimate as a gameplay choice; if there is a preference, it
   costs nothing to set it now and a preset version to change later.
3. **Should phase B ship at all?** It is the most historically grounded part
   of the design and the most expensive in determinism terms. It can be
   dropped without affecting phase A.
4. **Does the default roster field a householder class at all?** Fielding
   `Namamahay` requires the inspector to state that a dependent class in a
   battle line is a reconstruction. Dropping it leaves a three-entry roster
   of chief, sworn freeman, and bound freeman, which claims less.
