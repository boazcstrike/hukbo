# Contingent shape — design (Phase C)

Date: 2026-07-29
Status: design only. This document does not authorize implementation. It is
Phase C of the sequence set out in
[`2026-07-29-warrior-standing-design.md`](2026-07-29-warrior-standing-design.md)
§9, and it is explicitly **not** implemented in the same pass as Phase A or
Phase B. A future task-planning pass against this document is required before
any of the code below is touched.
Evidence base: [`docs/research/ARMY-COMPOSITION.md`](../research/ARMY-COMPOSITION.md)
§11.1, §3, §5

## 1. What is being proposed, and what is not

`FormationPlanner.ResolveContingentSizes`
(`src/Hukbo.Core/Simulation/FormationPlanner.cs:162`) currently derives a
faction's contingents from headcount alone:

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

`docs/research/ARMY-COMPOSITION.md` §11.1 sets out the evidence-backed
alternative, and this document designs it. It does **not** implement it: the
function above underlies deployment geometry, "the most heavily tested
surface in the repository" in the words of the parent design's §6.4, and this
document's job is to make the change safe to plan, not to make it.

Two things are proposed:

1. **Contingent count is set by how many chiefs joined, not by total
   headcount.** Mactan's three divisions and Bangkusay's twenty to thirty
   boats (`ARMY-COMPOSITION.md` §5) are both consistent with a small number
   of separately led groups, and §2's "no army, a coalition of followings"
   finding is the structural reason: a contingent is a chief's following, and
   a faction has as many contingents as it has chiefs willing to commit one.
2. **Contingent sizes are unequal**, because barangays ranged from under
   thirty to a hundred houses (`ARMY-COMPOSITION.md` §3) and chiefs differed
   in wealth and standing (`HISTORICAL_1500s_RANKS.md`, Morga's
   more-courageous-chief passage, also cited in the leadership design).

Both proposals are scenario input, not derived state, and that is what makes
them affordable in determinism terms: an author-supplied list of contingent
sizes costs nothing to validate and nothing to hash beyond what the existing
roster and deployment fields already cost.

## 2. Contingent count from chief count

The rank ladder Phase A adds (`RankId.Datu = 1`) makes "how many chiefs
joined" a countable, authoritative fact for the first time: it is the number
of `Datu`-rank warriors the scenario's `RosterCounts` fields. The natural
rule is **one contingent per fielded chief**, capped at `MaximumContingents`
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
§3 and §11.1 are explicit that barangay size varied with the individual
chief's wealth and standing, not with a fixed rule tied to a title, so a
single `Datu`-rank "capacity" constant would overstate what the evidence
supports in exactly the way `CLAUDE.md` §7 forbids. Expressing follower
count as scenario-authored contingent size, rather than as a rank-level
number, keeps every claim inside what section 3's evidence tier can bear and
avoids inventing a second numeric ladder alongside the rank ladder itself.

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
5. **Cache source and invalidation.** No cache. `Scenario.ContingentSizes`,
   if adopted, is immutable scenario input validated once at construction,
   the same pattern the existing optional `CombatRuleset` dictionaries use.
6. **Save, event, and version effect.** A new optional `Scenario` field, not
   a new preset id, per section 6's determinism note. `BattleSnapshot`
   already records deployment positions; no new snapshot field is implied
   beyond what deployment already contributes. No new event type.
7. **Worst-case complexity and benchmark workload.** Bounded by
   `MaximumContingents` and the existing O(warriorCount) deployment pass;
   no asymptotic change expected, to be confirmed against the canonical
   200-agent workload and a reported 500-agent result once implemented.
8. **Spectator explanation.** Directly visible at deployment: unequal
   contingent sizes and a chief visibly present in each one, with no panel
   or log required, matching `ARMY-COMPOSITION.md` §11.5's own answer to this
   question.
9. **Tests that fail before and pass after.** A freeze test proving that a
   scenario without `Scenario.ContingentSizes` produces byte-identical
   deployment to today, for every existing scenario fixture; a new test
   asserting the chief-per-contingent invariant when the input is supplied;
   a new test for the too-many-chiefs and zero-chiefs fallbacks named in
   section 2. All of this is scoped to the future task-planning pass this
   document feeds, not authorized here.

## 8. What this document deliberately leaves open

- The exact roster-order tie-break for "the first `MaximumContingents` chiefs"
  when a roster fields more chiefs than there are contingent slots.
- Where inside a contingent's lattice cell its founding chief is placed, and
  whether that placement is privileged in any way.
- Whether `Scenario.ContingentSizes`'s validation belongs on `Scenario`
  itself or on a new value type, and how it interacts with
  `Scenario.RosterCounts`'s existing roster-length validation.
- The exact mechanism by which `Scenario`-level configuration does or does
  not enter `CombatRuleset.ComputeContentHash`, which section 6 flags as
  needing confirmation before any content-hash claim in this document is
  treated as settled.

These are deliberately not resolved here. Resolving them under time pressure
during an implementation pass is exactly the failure mode `CLAUDE.md`'s
"a task that finds itself guessing has hit a missing decision and must stop"
rule exists to prevent; a task-planning pass against this document, informed
by whatever the leadership work in Phase B teaches about the actual cost of
touching `FormationPlanner`-adjacent code, is the correct next step, not an
immediate implementation task list.
