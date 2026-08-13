# Contingent shape — design (Phase C)

Date: 2026-07-29
Status: design only. This document does not authorize implementation. It is
Phase C of the sequence set out in the warrior standing design
§9, and it is explicitly **not** implemented in the same pass as Phase A or
Phase B. The task-planning pass this document required was run on 2026-08-13 and
is [`2026-08-13-contingent-shape.md`](2026-08-13-contingent-shape.md). That
document, not this one, is where the ordered task list and the current blocking
decisions live.

**Corrected on 2026-08-13.** A research pass against this document's own
evidence base and against the code found four citations that misstated their
sources, two claims that the code refutes outright, one stale line reference,
and one determinism hazard this document did not mention at all. Every
correction is marked inline below with a **Corrected 2026-08-13** note. Three of
the four questions section 8 leaves open are now closed, and are marked there.

Evidence base: [`docs/research/ARMY-COMPOSITION.md`](../research/ARMY-COMPOSITION.md)
§11.1, §3, §5, §11.5, §2, §7, and
[`docs/research/HISTORICAL_1500s_RANKS.md`](../research/HISTORICAL_1500s_RANKS.md).
The original header listed only the first three; the body leans on all of them.

## 1. What is being proposed, and what is not

`FormationPlanner.ResolveContingentSizes`
(`src/Hukbo.Core/Simulation/FormationPlanner.cs:174`, called from `:94`)
currently derives a faction's contingents from headcount alone:

**Corrected 2026-08-13.** This document originally cited line 162; the function
is at 174. The quoted snippet below is accurate, and the body on disk is still
byte-for-byte what is described here — nothing has been implemented in the
intervening fifteen days.

```csharp
var contingentCount = Math.Clamp(
    IntegerSquareRoot(warriorCount) / 2,
    1,
    Math.Min(MaximumContingents, warriorCount));
```

followed by an equal split, remainder to the earliest contingents. This is a
lattice-packing convenience — it guarantees a count that grows sublinearly
with army size and stays inside `MaximumContingents = 8` — and it has no
historical content: no source says a force divides by square root of its
headcount, and no source says its parts are equal.

`docs/research/ARMY-COMPOSITION.md` §11.1 sets out the alternative, and this
document designs it. It does **not** implement it: the
function above underlies deployment geometry, "the most heavily tested
surface in the repository" in the words of the parent design's §6.4, and this
document's job is to make the change safe to plan, not to make it.

**Corrected 2026-08-13.** This paragraph originally called §11.1 "the
evidence-backed alternative". That overstates it. §11.1 carries no evidence tier
label at all, and its two bullets are introduced as inference — "The evidence
suggests" — rather than as documented fact. §10 of the same research document is
explicit that "Any fixed number of fighters per leader, per boat, or per
settlement" is among the things the sources do **not** establish. Both proposals
below are therefore **Provisional reconstruction** in the sense of `CLAUDE.md`
§7, and neither was given a tier when this document was written. What *is*
Documented is the layer underneath them: that chiefs led their own followings
and that a force was a coalition of those followings (§2, and
`HISTORICAL_1500s_RANKS.md`).

Two things are proposed, both **Provisional reconstruction**:

1. **Contingent count is set by how many chiefs joined, not by total
   headcount.** Mactan's three divisions and Bangkusay's twenty to thirty
   boats (`ARMY-COMPOSITION.md` §5) are both consistent with a small number
   of separately led groups, and §2's "no army, a coalition of followings"
   finding is the structural reason: a contingent is a chief's following, and
   a faction has as many contingents as it has chiefs willing to commit one.

   **Corrected 2026-08-13.** Neither figure constrains contingent count
   numerically, and this bullet inherited §11.1's gloss without §5's own
   disclaimer. §5 states that three divisions "is not evidence of a standing
   three-part organization, a fixed division size, or a name for such a body",
   and §6 makes the boat rather than the chief the organizing unit at
   Bangkusay. Twenty to thirty is also not a small number, and it exceeds
   `MaximumContingents = 8` outright. The figures motivate the proposal; they
   do not evidence it.
2. **Contingent sizes are unequal**, because barangays ranged from under
   thirty to a hundred houses (`ARMY-COMPOSITION.md` §3) and chiefs differed
   in wealth and standing (`HISTORICAL_1500s_RANKS.md`, Morga's
   more-courageous-chief passage, also cited in the leadership design).

   **Corrected 2026-08-13.** What is Documented here is variation — between
   barangays, by Plasencia's thirty-to-a-hundred-houses band, and between
   chiefs, by Morga's passage that a chief "more courageous than others in war
   … enjoyed more followers and men". What is not documented is any rule that
   the contingents of one force were unequal, or by how much:
   `HISTORICAL_1500s_RANKS.md` states plainly that "No source gives a muster
   roll, a force size by class, or a ratio of chiefs to freemen to dependents
   in a fighting force". Unequal sizes are a well-motivated reconstruction, not
   a sourced rule.

Both proposals are scenario input, not derived state, and that is what makes
them affordable in determinism terms: an author-supplied list of contingent
sizes costs nothing to validate and nothing to hash beyond what the existing
roster and deployment fields already cost.

## 2. Contingent count from chief count

The rank ladder Phase A adds (`RankId.Datu = 1`) makes "how many chiefs
joined" a countable, authoritative fact for the first time: it is the number
of `Datu`-rank warriors the scenario's `RosterCounts` fields. The proposed
rule is **one contingent per fielded chief** — a **Provisional
reconstruction**, not a sourced rule, since no source states a one-to-one
mapping and §11.1 says only that count follows chiefs joined rather than
headcount — capped at `MaximumContingents`
the same way the current derivation is capped, with the two failure modes
named explicitly so a future implementer does not have to invent an answer
under deadline:

- **Zero chiefs fielded.** A roster that fields no `Datu`-rank warriors (the
  parent design's Decisions item 4 permits a three-entry roster without a
  Datu at all only if a future preset chooses to build one that way; the
  shipped V4 roster always fields at least one) has no chief to found a
  contingent around. The current derivation's floor of one contingent
  (`Math.Clamp(..., 1, ...)`) is the safe fallback: a chiefless faction
  becomes exactly one contingent, exactly as a very small faction does today.

  **Corrected 2026-08-13: this case is reachable in the shipped game, not
  hypothetical.** The client does not run V4. `ArenaGame.BuildScenario`
  (`src/Hukbo.Client/ArenaGame.cs:1449-1453`) overrides the `Scenario` defaults
  with `CombatPresetId.PrecolonialPhilippinesV5` and
  `MovementPresetId.LastStandEngagementV11`, then supplies spectator-authored
  `RosterCounts` at `:1456-1461`. Those counts are validated only for
  non-negativity and sum-equality, so a spectator can legally field a faction
  whose `DatuCount` is zero. The fallback proposed here is still the right
  answer, but it needs a real test rather than a remark.
- **More chiefs than `MaximumContingents`.** A roster with, for example,
  twelve `Datu`-rank warriors cannot found twelve contingents inside the
  fixed sixteen-slot leader-array budget
  (`FormationPlanner.MaximumContingents = 8`, and the slot arithmetic
  `FactionId * MaximumContingents + ContingentId` this bounds). The proposed
  rule is that the first `MaximumContingents` chiefs by roster order found a
  contingent each, and every chief beyond that count is dealt into an
  existing contingent as an ordinary member — a contingent may then have more
  than one `Datu`-rank warrior, and its leader is still resolved by the
  existing rank-then-entity-id scan (`2026-07-29-leader-rank-design.md`), so
  the "extra" chief is simply outranked by whichever chief the scan already
  favors, or becomes the new leader if the founding chief falls. This needs
  its own review against `ARMY-COMPOSITION.md` before it is treated as
  settled; it is recorded here as the leading candidate, not as a decision.

This is an **open sub-question for the task-planning pass**, not resolved by
this document: exactly how roster order picks "the first `MaximumContingents`
chiefs" needs to be a total order over roster indices and entity ids, the
same discipline every other multi-result query in the codebase already
follows, and it should be specified precisely before implementation starts.

## 3. Contingent sizes: scenario input, not derived arithmetic

The proposal is a new, optional scenario-level input —
`Scenario.ContingentSizes` or equivalent — an explicit list of per-contingent
warrior counts, one entry per founding chief, drawn by the scenario author
from the documented thirty-to-a-hundred-houses band in `ARMY-COMPOSITION.md`
§3 (`docs/research/ARMY-COMPOSITION.md` is explicit that this band converts
to "roughly 30 to 100 fighters" as a **Provisional reconstruction**, not a
documented number, and any inspector or preset comment that cites it must
carry that label).

Validated the same way the existing optional dictionaries on `CombatRuleset`
already are (defensive copy, explicit bounds check, at-construction
validation): every size at least 1, the list length at most
`MaximumContingents`, and the sum equal to the total warrior count the
roster fields — the same invariant `Scenario.RosterCounts` already enforces
against roster length today.

When this input is **absent**, `ResolveContingentSizes` runs exactly as it
does today, byte-identical, so every existing scenario and every existing
golden stays unmoved. This mirrors the gating pattern the parent design uses
for rank in the content hash and the state hash (`2026-07-29-warrior-standing-design.md`
§7): a new capability that contributes nothing unless a scenario opts into
it.

## 4. Rank-aware deployment

The parent design's §6.4 names this directly as the obvious next step it
deliberately does not take: making `FormationPlanner.PlanFactionDeployment`
deal a chief into every contingent, rather than dealing warriors into
contingents without regard to rank. Once contingent count is chief-count-
derived (section 2 above), this is close to automatic — each contingent is
already founded around one specific `Datu`-rank agent — but the deployment
placement itself (which lattice cell a contingent's founding chief occupies,
and whether the chief is placed at a privileged position such as the
contingent's geometric center) is a distinct question from *whether* a chief
is present, and needs its own review of `FormationPlanner`'s existing lattice
and spacing invariants before any placement rule is written. This document
does not propose a specific placement rule; it records that one is needed.

## 5. Follower capacity, revisited

The parent design's Decisions item 5 deferred a fourth per-rank attribute —
"how many followers a rank can hold" — to this document, rather than
building it as a standalone numeric field on `RankId`. Section 3 above
supplies the better answer: follower capacity is not a property of a rank in
the abstract, it is the **authored size of the specific contingent** a
specific chief was given in a specific scenario. `docs/research/ARMY-COMPOSITION.md`
§7's Morga passage and `HISTORICAL_1500s_RANKS.md`'s citation of it are the
evidence that following size varied with the individual chief's standing,
earned by war record, rather than with a fixed rule tied to a title, so a
single `Datu`-rank "capacity" constant would overstate what the evidence
supports in exactly the way `CLAUDE.md` §7 forbids. Expressing follower
count as scenario-authored contingent size, rather than as a rank-level
number, keeps every claim inside what section 3's evidence tier can bear and
avoids inventing a second numeric ladder alongside the rank ladder itself.

**Corrected 2026-08-13.** This paragraph originally cited "§3 and §11.1" as
explicit that barangay size varied with the chief's *wealth and standing*. §3
says nothing about wealth or standing at all — it gives Plasencia's house band
and the fighter conversion only. The documented root is Morga, in §7 and in
`HISTORICAL_1500s_RANKS.md`, neither of which the original text cited. The
conclusion the paragraph draws is unaffected and remains correct.

## 6. Determinism impact

**Content hash.** No effect if `ResolveContingentSizes`'s inputs
(`warriorCount` and, if adopted, `Scenario.ContingentSizes`) are treated as
scenario configuration rather than ruleset content; this needs confirming
against how `Scenario` values currently do or do not enter
`CombatRuleset.ComputeContentHash` before a task plan is written, because
that boundary is not the same one Phase A's rank fold uses.

**State hash.** Deployment positions already enter the state hash today
through initial agent placement. A scenario that supplies
`Scenario.ContingentSizes` produces a different deployment than the same
warrior count without it, which is expected and is exactly the point — it is
new scenario input, not a silent change to existing scenarios. A scenario
that does **not** supply it must produce byte-identical deployment to today,
which is the load-bearing determinism guarantee this design depends on and
which any implementation must prove with a freeze test before adding
anything else.

**Ordering.** Section 2's "first `MaximumContingents` chiefs by roster
order" rule must be specified as a total order (roster index, then entity
id, or whatever the eventual task plan settles on) before implementation,
per the open sub-question recorded there.

**New preset or version needed.** Because `FormationPlanner` is not gated
behind a `MovementPresetId` today — it runs unconditionally at
`BattleSimulation.Create` regardless of which movement preset a scenario
selects — this change cannot reuse the preset-version pattern Phase A and
Phase B use. The opt-in `Scenario.ContingentSizes` field described in section
3 is the proposed gate instead, and the task-planning pass must confirm this
is sufficient before implementation starts.

**Corrected 2026-08-13: the premise holds but the conclusion is refuted.**
`PlanFactionDeployment` is indeed called unconditionally, at
`BattleSimulation.cs:618-620`, before the movement ruleset is even fetched at
`:644`, and no `MovementPresetId` appears anywhere in `FormationPlanner.cs`.
But it does not follow that the preset-version pattern is unavailable. The
deployment *pipeline* is already preset-gated three times immediately
downstream of that ungated call: `:645-662` reassigns slots when the ruleset
declares equipment-relative footwork, and `:663-692` reassigns contingent
membership through `CohortDeploymentAssignment` when the preset is
`BattlefieldRealismV10` or `LastStandEngagementV11`, tested at `:5170-5172`.
Changing deployment behind a preset gate is shipped practice. `FormationPlanner`
also already receives the whole `Scenario` (`FormationPlanner.cs:84-86`), so it
could gate on `scenario.MovementPreset` directly.

Both routes are open, and this document presented only one while calling the
other impossible. That matters because `ARMY-COMPOSITION.md` §11.1 — this
document's own cited source — asks for the route it rules out: "Any such change
is a **new movement preset version** with new golden expectations, under the
rules in `SIMULATION-GAME-STANDARDS.md` §4." Which gate to use is an open
decision, recorded as task 1 of the planning pass.

**The determinism hazard this document did not name.** `PlanFactionDeployment`
draws exactly two jitter values per warrior, in ascending faction-local index
order (`FormationPlanner.cs:116-131`, `:325-326`). `NextJitter` returns
*without drawing at all* when its limit is not positive (`:360-364`). The
lattice is built from `contingentSizes[0]` — the first contingent's size — at
`:96`. So an unequal split changes lattice geometry, which can change
`JitterLimit`, which changes whether a draw happens, which shifts the
SplitMix64 stream for every warrior placed afterwards.

The consequence is that section 3's claim below — that an absent input leaves
deployment "byte-identical" — is necessary but not sufficient as a verification
target. An implementation must prove the *draw count* is unchanged, not only
the positions. This is what `SIMULATION-GAME-STANDARDS.md` §4's rule that
"adding a draw in one system cannot shift unrelated outcomes" is protecting.

## 7. Answers to the nine acceptance questions

These are answered at the level of detail this design supports; several are
marked as open sub-questions above and are repeated here rather than
answered with an invented specific, because inventing one now would be
guessing ahead of the task-planning pass this document exists to feed.

1. **User-visible outcome.** Contingents of visibly different size on
   deployment, each with a `Datu`-rank warrior founding it, replacing the
   current uniform lattice of equal-sized groups. Visible on screen the
   moment deployment is drawn, per `ARMY-COMPOSITION.md` §11.5's own
   discoverability note.
2. **Tick stage and state read/written.** Resolved once, at
   `BattleSimulation.Create`, before the first tick — the same point
   `ResolveContingentSizes` already runs at today. No tick stage mutates
   contingent membership after deployment; that invariant is unchanged.
3. **Numeric units and bounds.** Contingent sizes are positive integers
   summing to the roster's total warrior count, bounded by
   `MaximumContingents` entries. No same-tick conflict, because deployment is
   resolved once before any tick runs.
4. **Total ordering and random-stream policy.** No new random stream.
   Section 2's chief-selection-when-there-are-too-many-chiefs rule must be a
   total order; the exact key is an open sub-question, not resolved here.

   **Corrected 2026-08-13.** "No new random stream" is true and beside the
   point. The hazard is the *draw count* on the existing stream: a changed
   lattice can change `JitterLimit`, and `NextJitter` skips its draw entirely
   when that limit is not positive, so an unequal split can shift the
   SplitMix64 sequence for every warrior placed afterwards. See the
   determinism-hazard note added to section 6. The total-order key is answered
   in the planning pass as faction-local roster index ascending, then
   `EntityId` ascending — the same discipline the leader election already uses
   at `src/Hukbo.Core/Movement/MovementRules.cs:96-140` — and awaits sign-off.
5. **Cache source and invalidation.** No cache. `Scenario.ContingentSizes`,
   if adopted, is immutable scenario input validated once at construction,
   the same pattern the existing optional `CombatRuleset` dictionaries use.

   **Corrected 2026-08-13.** The closer precedent is `Scenario.RosterCounts`
   (`src/Hukbo.Core/Simulation/Scenario.cs:151-162`, validated at `:327-356`),
   which is an `ImmutableArray<int>` defaulting empty, on the same type, with
   the same sum-equals-`AgentsPerFaction` invariant this field needs. Copy that
   rather than the `CombatRuleset` dictionaries.
6. **Save, event, and version effect.** A new optional `Scenario` field, not
   a new preset id, per section 6's determinism note. `BattleSnapshot`
   already records deployment positions; no new snapshot field is implied
   beyond what deployment already contributes. No new event type.

   **Corrected 2026-08-13.** "Not a new preset id" was asserted on the strength
   of section 6's final paragraph, which is refuted there. No repository rule
   forbids the opt-in-field route — `CLAUDE.md` §5's enumerated preset-version
   triggers are enum values, enum order, roster order, weights, and hash
   mixers, none of which a scenario field touches, and `RosterCounts` shipped
   this way — but `ARMY-COMPOSITION.md` §11.1 asks for the preset route, and
   both routes are open. This is a decision, not a settled answer.
7. **Worst-case complexity and benchmark workload.** Bounded by
   `MaximumContingents` and the existing O(warriorCount) deployment pass;
   no asymptotic change expected, to be confirmed against the canonical
   200-agent workload and a reported 500-agent result once implemented.
8. **Spectator explanation.** Directly visible at deployment: unequal
   contingent sizes and a chief visibly present in each one, with no panel
   or log required, matching `ARMY-COMPOSITION.md` §11.5's own answer to this
   question.

   **Corrected 2026-08-13.** Only the first half is §11.5's. That section
   claims unequal contingent sizes are visible the moment they are drawn, and
   that a leader whose death breaks a contingent is visible. It makes no claim
   about a chief being visibly present in each contingent — that is this
   document's own addition, and it needs its own justification rather than an
   attribution.
9. **Tests that fail before and pass after.** A freeze test proving that a
   scenario without `Scenario.ContingentSizes` produces byte-identical
   deployment to today, for every existing scenario fixture; a new test
   asserting the chief-per-contingent invariant when the input is supplied;
   a new test for the too-many-chiefs and zero-chiefs fallbacks named in
   section 2. All of this is scoped to the future task-planning pass this
   document feeds, not authorized here.

## 8. What this document deliberately leaves open

**Updated 2026-08-13 by the planning pass.** Three of these four are now
closed. Each is left in place below with its answer, rather than deleted, so
that a reader can see what was open and what settled it.

- **CLOSED.** The exact roster-order tie-break for "the first
  `MaximumContingents` chiefs" when a roster fields more chiefs than there are
  contingent slots. *Answer: faction-local roster index ascending, then
  `EntityId` ascending — the discipline the leader election already uses at
  `src/Hukbo.Core/Movement/MovementRules.cs:96-140`. Proposed, not yet signed
  off, since it is still a rule about who leads.*
- **STILL OPEN, and harder than when this was written.** Where inside a
  contingent's lattice cell its founding chief is placed, and whether that
  placement is privileged in any way. *`CohortDeploymentAssignment.AssignForFaction`
  (`src/Hukbo.Core/Movement/CohortDeploymentAssignment.cs:47`) now owns
  intra-contingent slot ordering under `BattlefieldRealismV10` and
  `LastStandEngagementV11`, putting shield bearers on the forward-most slots. A
  chief-placement rule would be a second claim on that same ordering, and the
  two have to be reconciled rather than merely composed. Nothing reads `RankId`
  at deployment time today. This is a gameplay decision, not a research
  finding.*
- **CLOSED.** Whether `Scenario.ContingentSizes`'s validation belongs on
  `Scenario` itself or on a new value type, and how it interacts with
  `Scenario.RosterCounts`'s existing roster-length validation. *Answer: on
  `Scenario`, copying `RosterCounts` exactly — `ImmutableArray<int>` defaulting
  empty, defensively copied, validated in the constructor alongside the
  existing roster validation at `Scenario.cs:327-356`. No new value type.*
- **CLOSED.** The exact mechanism by which `Scenario`-level configuration does
  or does not enter `CombatRuleset.ComputeContentHash`. *Answer: it does not,
  and cannot, by type signature. The `CombatRuleset` constructor
  (`src/Hukbo.Core/Combat/CombatRuleset.cs:57-67`) takes ten parameters and none
  of them is a `Scenario`. The only coupling is that `scenario.CombatPreset`
  selects which ruleset is fetched, at `BattleSimulation.cs:579`, and what is
  folded is that ruleset's own `Id` at `CombatRuleset.cs:761`. Section 6's
  content-hash claim is therefore confirmed, and confirmed unconditionally
  rather than subject to its own hedge.*

The one remaining open question is deliberately not resolved here. Resolving it
under time pressure during an implementation pass is exactly the failure mode
`CLAUDE.md`'s "a task that finds itself guessing has hit a missing decision and
must stop" rule exists to prevent. The task-planning pass this section
originally called for has now been written — it is
[`2026-08-13-contingent-shape.md`](2026-08-13-contingent-shape.md) — and it
carries the ordered task list, the blast radius across the test surface, and
the two decisions that still block implementation.
