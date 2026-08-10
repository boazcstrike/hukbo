# Leader rank — design

> **Archived: reference only.** This document is finished work, kept so the
> decision can be traced back to its reasoning. Do not execute it and do not
> cite it as the reason to change anything.

Date: 2026-07-29
Status: design only. This document does not authorize implementation.
Evidence base: [`docs/research/ARMY-COMPOSITION.md`](../../research/ARMY-COMPOSITION.md),
[`docs/research/HISTORICAL_1500s_RANKS.md`](../../research/HISTORICAL_1500s_RANKS.md)
Parent design: [`2026-07-29-warrior-standing-design.md`](2026-07-29-warrior-standing-design.md)
§6.3, which this document expands into the full leadership account.

This document commissions the leadership-first work named in
`docs/archives/2026-08-10/2026-07-29-leader-standing-orchestration.md`: leadership is a
first-class deliverable, not an optional tail on rank. It answers the nine
`SIMULATION-GAME-STANDARDS.md` §10 acceptance questions for leadership on its
own, separately from the rank ladder's own answers in the parent design.

## 1. What a leader is, and what it is not

`docs/research/ARMY-COMPOSITION.md` §2 and §7 are unambiguous on the
structural point: a sixteenth-century Philippine force was **a coalition of
independently commanded followings**, and there was no single commander over
it. Rajah Sulayman said so directly, quoted in the 1572 relation: "there is
no king and no sole authority in this land; but everyone holds his own view
and opinion, and does as he prefers." The one attested rule resembling
command between chiefs is sponsorship — whichever chief offered the
*magaanito* took half the booty — and sponsorship bought a larger share, not
obedience. Followers, for their part, could leave a leader who failed them:
Loarca records the Visayan timagua as "free to pass from the service of one
chief to that of another ... whenever they so desire."

A leader in Hukbo is therefore **the head of one contingent**, and a faction
fields several of them, exactly as many as it has contingents. There is no
army commander, no chain of command spanning contingents, and this design
does not add one. This is the same conclusion `docs/research/ARMY-COMPOSITION.md`
§11.4 states directly: "no signal, order, or command-radius system presented
as historical," because the corpus is silent on one.

A leader is identified, not appointed: it is whichever living member of a
contingent has the highest rank (lowest `RankId` numeric value), ties broken
by lowest entity id, recomputed every tick by
`MovementRules.ScanContingentLeadersAndLivingCounts` under the new
`PersistentContingentsV5` movement preset. There is no persistent "leader"
flag stored on an agent across ticks; leadership is a derived fact of the
current living roster, exactly as it already is today under the
lowest-entity-id rule V1 through V4 use. This design changes *which*
comparison decides the leader, not the fact that it is recomputed every tick
from scratch.

## 2. Leader identity already drives movement — what changes and why it is bigger than a comparator swap

The contingent leader concept already exists and already has real behavioral
consequences under `PersistentContingentsV4`; this design changes who is
selected as leader, and that selection reaches further into the simulation
than the leader-scan function itself. Verified against the current code:

- **The leader never receives a cohesion destination.**
  `MovementRules.IsCohesionEligible` (`MovementRules.cs:403`) returns `false`
  immediately whenever `isLeader` is `true`. Every non-leader member of a
  contingent that is `Hold`ing or `Advance`-stragglinging rallies toward the
  leader; the leader itself moves independently. Changing who leads a
  contingent therefore changes the movement resolution of at least two
  agents on the tick leadership changes — the former leader newly starts
  receiving a cohesion destination, and the new leader newly stops receiving
  one — not one agent's tie-break outcome.
- **Cohesion squares are centred on the leader's position.**
  `FormationRules.IsCohesionSquareWithinBounds` and
  `FormationRules.DoCohesionSquaresOverlap` both compute their square from
  the current leader's location. A different leader is a different square
  origin, which can change the map-edge and cross-contingent geometric gates
  for the *entire* contingent, not just for the leader.
- **Rally direction and the rally trail are computed from the leader's
  position** (`BattleSimulation.ComputeRallyDirection` and its callers). Both
  move with a leadership change.
- **`PersistentContingentsV4`'s narrowed cross-contingent scan reads
  `leader.ContingentState`** (`BattleSimulation.cs`, near line 1135). Under
  `PersistentContingentsV5` this means the state hash moves through
  deployment geometry — which agent ends up adjacent to which contingent's
  leader at spawn — and not only through the rank-then-entity-id tie-break
  comparator itself.

The practical consequence: `PersistentContingentsV5` needs its own seed-1
state-hash, event-hash, and outcome goldens, verified with the full
determinism procedure in the `hukbo-determinism-change` skill, not a
narrow unit test of the comparator alone. This is why the parent design keeps
phase B as its own sequenced phase, moving the state hash for one reason at a
time, and it is why this document exists separately from the parent design's
own leadership summary in its section 6.3: the blast radius above is the
thing an implementer must actually verify, and it deserves its own detailed
account rather than a paragraph.

## 3. Leader loss: an extension of `ContingentState`, not a new value

`ContingentState.Break` already means "this group has lost too many members
to act as one; cohesion is off permanently"
(`src/Hukbo.Core/Simulation/ContingentState.cs`). `MovementRules.ResolveContingentState`
computes it from a casualty ratio and a living-member floor
(`checked(livingCount * 4) <= initialCount || livingCount < minimumCohesiveMembers`)
and takes no leader-specific input at all today.

This design makes **no change to that function and adds no new input to
it.** A leader's death is not specially weighted, does not have its own
threshold, and does not trigger `Break` on its own. It counts toward the
casualty ratio exactly as any other member's death does, because the leader
is a living member of the contingent like any other, and nothing here treats
that membership as more or less costly to lose.

What does change is *who leads next*: the moment a contingent's ranking
member dies, `ScanContingentLeadersAndLivingCounts` picks the next-ranking
survivor on the very next tick, because leadership is recomputed from
scratch every tick rather than stored. That reassignment, and its downstream
effect on cohesion-square placement, rally direction, and the cross-
contingent scan described in section 2, **is** the entire mechanical
expression of "the group feels the loss of its leader" that this design
implements. There is no separate morale value, no fear stat, no combat
penalty applied to a contingent that just lost its chief, and no bonus
applied to one that still has him. `CLAUDE.md` §9 defers morale and rout
until the gate authorizes them, and this design does not smuggle either in
under the name "leadership."

The succession rule itself — highest surviving rank becomes the new leader —
is labelled honestly in the parent design (§6.3, Decisions item 3): it is a
**Provisional reconstruction**, not a Documented rule, because
`docs/research/ARMY-COMPOSITION.md` §7 lists "a rule for replacing a fallen
leader" explicitly among the things the corpus does not establish. Morga
describes authority accreting to a chief with a stronger war record over
time, not a battlefield succession procedure. This design keeps the rule
because it is the least-invented option — rank order is at least attested —
but the inspector and code comments must say "provisional reconstruction,"
never "documented," when this rule is described.

## 4. Leader marker in the client

There is currently **no client-visible leader indication anywhere in
Hukbo.** The `isLeader` boolean the simulation computes today
(`BattleSimulation.cs:1282`, inside
`TryResolveContingentCohesionAimPoint`) is a private local used only to
decide cohesion eligibility; it is never exposed on `AgentView`, never read
by `Hukbo.Client`, and never drawn. This is new client-facing work, not a
matter of wiring up something that already renders.

This design adds:

- `AgentView.IsLeader` — a new `bool`, defaulted to `false` following the
  same convention `Level`, `ContingentId`, and `ContingentState` already use
  on `AgentView` so presentation tests written before this field existed
  keep compiling without naming it. `BattleSimulation.ToView` (or its
  per-agent view-construction path) sets it from the same leader-entity-id
  comparison the cohesion computation already performs, so no new
  simulation-side computation is introduced — only a new place the existing
  fact is written.
- A leader marker in the battle view — a small, unambiguous glyph or outline
  drawn on the leader's sprite, following the existing theme-role convention
  (`hukbo-client-ui` skill) rather than a new ad hoc color. This is the
  primary way a spectator discovers, mid-battle and without opening the
  inspector, which warrior a contingent is currently rallying around, and it
  is the visible correlate of the state-hash-moving change described in
  section 2: when the marker jumps to a different warrior, that is the
  leadership change actually happening in simulation state, not a
  presentation-only flourish.
- The inspector's `Rank:` line (parent design §2) already names a warrior's
  rank; this design adds a second inspector line, or an annotation on the
  existing one, stating whether this specific warrior is the contingent's
  current leader — a derived, per-tick fact, not stored state, exactly
  matching how `AgentView.IsLeader` itself is derived rather than persisted.

No new theme role beyond what `hukbo-client-ui` already documents is
required in principle, but if none of the 27 existing semantic roles fits an
"is leading" indicator, defining one is in scope for the client-presentation
task in the plan document — it is presentation-only and does not touch
`Hukbo.Core`.

## 5. Explicitly out of scope

These are named because the evidence or the repository's own deferred list
rules them out, not because they were merely deprioritized:

- **Booty, ransom, and any reward economy.** `docs/research/ARMY-COMPOSITION.md`
  §7 documents the sponsorship-and-half-booty rule in real detail, and §11.4
  is explicit that none of it belongs in `Hukbo.Core`. It is a future
  campaign-layer concern that consumes `BattleOutcome`; the battle core never
  learns what a barangay is, let alone what a raid's proceeds are worth.
- **Command signal systems of any kind** — a shouted order, a horn, gong,
  drum, or flag code, a messenger role. `docs/research/ARMY-COMPOSITION.md`
  §7 records this as unsupported in the source set: "nothing in this set
  establishes a shouted command vocabulary, a signal code by horn, gong,
  drum, or flag, a messenger organization." Presenting one as historical
  would be an invention with a historical label, which `CLAUDE.md` §7
  forbids outright.
- **Mid-battle allegiance switching.** Loarca's free-exit passage
  (`docs/research/ARMY-COMPOSITION.md` §7, §11.2) describes a follower who
  could leave a chief who failed to defend him, but that is a description of
  peacetime and inter-raid social mobility, not a mid-fight defection
  mechanic, and nothing in the corpus describes a warrior changing sides
  during an engagement already underway. This design keeps `FactionId` and
  `ContingentId` immutable for the duration of a battle, exactly as they are
  today.
- **Morale, fear, and rout**, in any form, under any name. `CLAUDE.md` §9
  defers all three explicitly. Section 3 above is the specific place this
  design could plausibly have smuggled a morale value in under the name
  "leadership," and it does not.
- **A new `ContingentState` value or a new leader-specific field on
  `ContingentState`.** Section 3 above is explicit that no change to
  `ResolveContingentState` or its inputs is proposed.

## 6. Answers to the nine acceptance questions

1. **User-visible outcome.** A leader marker on the leading warrior's sprite
   in the battle view, visible without opening any panel; an inspector
   annotation naming whether the inspected warrior currently leads its
   contingent; and, indirectly, the observable fact that a contingent's
   rally point and cohesion-square origin move with the marker rather than
   with an arbitrary warrior.
2. **Tick stage and state read/written.** Leadership is recomputed every
   tick by `ScanContingentLeadersAndLivingCounts`, which already runs at the
   start of the movement stage under every existing preset; this design
   changes its comparator under `PersistentContingentsV5` only.
   `AgentView.IsLeader` is written at view-construction time, read-only,
   every tick, from that same computation — no tick stage mutates stored
   agent state to record leadership, because none is stored.
3. **Numeric units and bounds.** No new numeric quantity. The comparator
   uses `RankId`'s five pinned values (parent design §4) and `EntityId`,
   both already bounded and already validated elsewhere.
4. **Total ordering and random-stream policy.** No new random stream. The
   leader scan's total order is `(RankId ascending, EntityId ascending)`,
   total because entity ids are unique within a match. No other query in
   this design produces more than one result to order.
5. **Cache source and invalidation.** No cache. `AgentView.IsLeader` is
   derived fresh from `_contingentLeaderEntityIds`, itself rebuilt from
   scratch every tick by the leader scan; there is nothing to invalidate
   because nothing here is retained across ticks beyond what the leader scan
   already retains today.
6. **Save, event, and version effect.** New movement preset id
   `PersistentContingentsV5`. `AgentView.IsLeader` is a presentation-facing
   derived field, not new authoritative state beyond the rank data the
   parent design already adds to the state hash; no new event type, because
   a leadership change is not an event in the existing 200-event-deep battle
   feed — it is discoverable continuously through the marker, not through a
   log line. If a future task decides a leadership change deserves an
   explicit event, that is a separate proposal against the existing event
   budget, not part of this design.
7. **Worst-case complexity and benchmark workload.** O(1) added per agent at
   view-construction time; the leader scan itself is unchanged in asymptotic
   cost, already a single linear pass over all agents. Verified against the
   canonical 200-agent, 10,000-tick, seed-1 workload, with a 500-agent result
   reported as `SIMULATION-GAME-STANDARDS.md` §10 requires.
8. **Spectator explanation.** *Can a spectator discover this effect without
   reading source code?* Yes, and this is the question this whole document
   answers most directly: a leadership change is not an inferred fact a
   spectator has to reconstruct from movement — it is drawn, directly, as a
   marker that visibly moves from one warrior to another the tick a chief
   falls and a new one is recognized. That marker is this design's entire
   discoverability answer; it does not depend on the composition panel
   (parent design §2) or on any log.
9. **Tests that fail before and pass after.** A movement-preset freeze test
   proving `PersistentContingentsV1` through `V4` produce an unmoved leader
   selection under hand-placed rank data (they must ignore rank entirely,
   since only V5 reads it); a new unit test building a contingent with
   hand-placed ranks and asserting the selected leader for: a single chief
   present, several chiefs present (lowest entity id wins among equals), no
   chief present (ranking survivor wins), and the chief dead mid-battle
   (leadership passes to the next-ranking survivor the following tick); a
   new seed-1 state-hash, event-hash, and outcome golden for V5 paired with
   combat preset V4; and a client test asserting `AgentView.IsLeader` is
   `true` for exactly one living member per contingent and that the
   inspector's leadership annotation reads correctly for both a leading and
   a non-leading warrior.
