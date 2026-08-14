# Contingent cohesion before contact — design

Status: proposed, and **deliberately not yet planned**. The block this document
once carried has lifted — `CohortLateralSpreadV13` has landed, so nothing is
holding `src/Hukbo.Core/Simulation/BattleSimulation.cs` any more, and the preset
value this design would append is 14, which does not exist yet. See section 7.
Nothing here is built, and a design document does not authorize implementation,
so this one still authorizes nothing.

**The row that motivated this document has since closed, and none of this
document was built.** `BR-1` was re-run by a person at an interactive desktop on
2026-08-14 and passed, so the smoke family left the live checklist; its record
is the archived document titled "Battlefield realism cohort smoke — closed
2026-08-14". What changed between the failing run and the passing one was
`CohortLateralSpreadV13` becoming the client's default movement preset, which
changes how the army is laid out laterally, not how a contingent coheres. The
remedy R1 was checked against the code again on 2026-08-15 and is still absent:
`MovementRules.IsCohesionEligible` still applies the binary straggler test under
`ContingentState.Advance`, at `src/Hukbo.Core/Movement/MovementRules.cs:444-447`.
R2's and R3's premises did not survive that re-reading, and this document's plan
reshapes both around what is actually on disk rather than dropping them. The
blanket denial R2 would narrow is
already narrow — `src/Hukbo.Core/Simulation/BattleSimulation.cs:1803-1810` marks
a living slot only when `!TakesPartInCrossContingentScan(slot)`, and that
predicate already excludes exactly `Close` and `Break` — and the square R3 would
size to the contingent is already sized to it, because the margin at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:1783` is built from a jitter
radius computed from the slot's own living member count. This document therefore
stays live as a standing diagnosis of the one gate that has not moved — not as
work anybody is waiting on.

## 1. What a person reported

A tester ran the battlefield realism smoke family on 2026-08-14 and reported of
row `BR-1`:

> they visibly form up but not enough, some just charged and fought

Row `BR-1` asks whether a contingent reads as mostly carrying one weapon. The
weapon-grouping half of that is working — the tester saw the groups. What failed
is cohesion: the groups do not hold together long enough to read as groups,
because a large share of each contingent leaves immediately and engages
individually.

This is a different defect from the one the cohort lateral spread design
addresses. That document fixes *where a cohort's lane sits across the army's
frontage*, which is smoke rows 58 and 59. It says nothing about whether a
contingent's members stay with it, and its section 8 puts the within-contingent
rules out of scope. The two changes are complementary and neither subsumes the
other.

## 2. The mechanism, traced in source

A contingent resolves to one of four states each tick in
`MovementRules.ResolveContingentState`, and a member only walks toward its
contingent's aim point when `MovementRules.IsCohesionEligible` returns true.
That predicate has six gates, and two of them compose into the reported result.

**Gate 4 makes `Advance` almost a no-op.**

```
if (state == ContingentState.Advance && !straggling)
{
    return false;
}
```

Under `Advance`, only a *straggling* member — one outside three-quarters of the
cohesion radius from its leader — is pulled in. Every member already inside that
radius is denied a cohesion destination and falls through to the pursuit path in
`BattleSimulation.GatherMovementProposals`, which sends it at its own nearest
enemy. So a contingent in `Advance` does not advance as a body at all: its
stragglers close up while its core charges. That is precisely the mixed picture
the tester described, and it is why the forming-up is visible but partial.

`Hold` is the state that gathers the whole contingent. `Advance` was never
intended to.

**Gate 6, plus the narrowed scan, pins contingents in `Advance`.**

A contingent only reaches `Hold` when its spread is inside the cohesion radius,
its duty window is open, **and** the geometric gates pass:

```
var geometricGatesPass =
    _contingentSquareFitsMap[slot] && !_contingentSquareOverlapsAnother[slot];
```

`_contingentSquareOverlapsAnother` is set two ways. The first is the intended
pairwise same-faction overlap scan. The second is a blanket denial:

```
if (_contingentLivingCounts[slot] != 0 &&
    !TakesPartInCrossContingentScan(slot))
{
    _contingentSquareOverlapsAnother[slot] = true;
}
```

A living slot the narrowed scan excludes is marked as overlapping outright, and
the comment above it states the consequence plainly — the denial "resolves it to
Advance through transition rule 4". The exclusion set is not open-ended:
`TakesPartInCrossContingentScan` at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:1934-1943` returns true for every
slot under a preset that does not narrow the scan, and otherwise defers to
`MovementRules.ParticipatesInCrossContingentScan`
(`src/Hukbo.Core/Movement/MovementRules.cs:355-360`), which excludes exactly
`Close` and `Break`. So the contingents that deny themselves are the ones already
fighting, and their squares are then unavailable to relieve anyone else's overlap
either. That second-order effect is real; the blanket denial this section
originally described is not.

The cohesion square is not sized by a constant either. Its margin is
`_contingentMarginRaw[slot] = checked(jitterRaw + Scenario.BodyRadiusRaw)` at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:1783`, where `jitterRaw` comes
from `FormationRules.ComputeContingentJitterRaw` applied to the slot's own living
member count, so a large contingent already claims a larger square than a small
one. `CohesionRadiusMultiplier`, which is **24** body radii under the shipped
`LastStandEngagementV11`, sizes something else: the straggler radius that gate 4
compares against. Squares still overlap when several contingents deploy into one
half of the map, and every overlapping pair is denied `Hold`, resolves to
`Advance`, and under gate 4 gathers only stragglers — but the cause is deployment
density, not a fixed twenty-four-radius claim.

The composed result: contingents spend most of the approach in `Advance`, and in
`Advance` most members are charging individually. The cohesion machinery is
present, correct in isolation, and rarely reaching the state that would make it
visible.

## 3. What the historical evidence permits, and what it forbids

This matters more than usual here, because the obvious fix — have each contingent
halt and dress its ranks before advancing — is partly barred by the repository's
own research.

Reviewed against `docs/research/battles/03-deep-past-formations-and-tactics.md`,
`docs/research/battles/02-deep-past-forces-and-command.md`,
`docs/research/ARMY-COMPOSITION.md`, and
`docs/research/movement/tall-hardwood-shield.md`:

| Claim | Verdict |
| --- | --- |
| A field force arrives as several separately led groups | Documented. `ARMY-COMPOSITION.md` states the top-level abstraction is "several contingents that agreed to show up", and Mactan is recorded as three divisions |
| A group pauses or hesitates locally before contact | Plausible inference, which maps to **Provisional reconstruction**. The corpus describes "local advance, hesitation, and withdrawal rather than perfectly simultaneous army-wide motion" |
| A force could still be assembling when fighting began | Documented |
| Spacing inside a group is **irregular** | Plausible inference, and it is the *only* thing the corpus says about spacing |
| Regular files and ranks, fixed frontage, fixed depth, a shield wall, prearranged manoeuvres, command signals | **Unattested.** Named explicitly as not established |

Two consequences bind this design:

- **A per-contingent, independent, local pause is shippable as a Provisional
  reconstruction.** Each contingent decides for itself; nothing is synchronized
  across the army.
- **Tightening or dressing the group is not.** Regularizing spacing contradicts
  the corpus's only spacing finding and would build an unattested shape. The fix
  must make a contingent *stay together* without making it *neater*. Existing
  jitter and irregular spacing are preserved untouched, and smoke row 60 — which
  passed — asserts exactly that, so it is also the regression guard.

An army-wide halt is barred outright, both by the "no army-wide synchronized
motion" finding and by the absence of any command-signal evidence.

## 4. The proposed rule

A new movement preset. **No existing preset changes behaviour**, so every frozen
hash and both freeze suites stay as they are.

**R1 — `Advance` pulls in more than stragglers.** Replace gate 4's binary
straggler test with a proportional one: under `Advance`, a member is cohesion-
eligible while its distance from its leader — which is what
`SquaredDistance(agent, leader)` at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:3679` actually measures — exceeds
a *cohesion band* rather than while it exceeds three-quarters of the full radius.
The band is a ruleset field so it is tunable without a code change. This is the
single highest-value change and it alone would move the reported symptom. It is
also the only one of the three whose premise survived re-reading the code.

**R2 — the premise is false; what survives is a pin and two comment
corrections.** The premise was that a slot excluded from the scan for a reason
other than `Close` or `Break` was being denied for implementation convenience.
There is no such slot: `TakesPartInCrossContingentScan` excludes exactly `Close`
and `Break`, and both of those denials are the correct gameplay behaviour by this
design's own reasoning — a contingent that is genuinely fighting should not
gather. R2 therefore changes no executable statement. Its plan carries it as a
test pinning that excluded set, plus corrections to the two comments that read as
though the denial were broader than the two states.

**R3 — the premise is false; what survives is the stated purpose, by another
mechanism.** The premise was that a contingent of three and a contingent of forty
claim the same square. They do not: the margin is a jitter radius computed from
the slot's own living member count plus one body radius, so a large contingent
already claims a larger square. R3's purpose — making `Hold` reachable under a
realistic eight-contingent deployment — is not served by the mechanism this
section originally described, because that mechanism does not exist. Its plan
delivers the purpose instead as a ruleset-tunable scale on the claimed margin
alone, leaving the jitter that sets member spacing untouched, because scaling
spacing is what R4 and section 3 forbid.

**R4 — nothing about spacing, jitter, or slot geometry changes.** Section 3
forbids it. `FormationPlanner` is not edited by this design at all.

## 5. Versioning and determinism

R1 and R3 both move agent positions — R1 by widening which members gather, R3 by
scaling the claimed margin — so they land behind one new `MovementPresetId`
value, appended, never renumbered, which is 14, since `CohortLateralSpreadV13 = 13` at
`src/Hukbo.Core/Movement/MovementPresetId.cs:282` is the last member today. That
preset gets its own registry entry, its own registration test, and new golden
expectations. The shipped client default is flipped to it only after a person has
watched a battle and confirmed the effect, not as part of the implementation.

`SplitMix64` draw counts must not change: R1 reads state that already exists and
does not draw, R2 changes no executable statement, and R3 scales a margin that is
already computed. If an implementation finds itself needing a draw, that is a
design change and comes back here first.

Termination is the risk this change carries. A contingent that gathers more
eagerly closes with the enemy later, and the twenty-seed termination sweep that
the battlefield realism work established is the gate on it. A preset that
gathers beautifully and never resolves a battle is a regression, exactly as
movement preset V7 was.

## 6. The nine questions (`SIMULATION-GAME-STANDARDS.md` §10)

1. **What does it do?** Makes a contingent approach as a body instead of
   dissolving into individual pursuit.
2. **Can a spectator discover it without reading source?** Yes — this is the
   whole point, and its absence is what a tester reported unprompted. A person
   watching sees groups crossing the field together rather than a scatter.
   Smoke row `BR-1` is the check.
3. **Does it reach the state hash?** Yes. New preset, new golden expectations.
4. **Does it reach the event hash?** Yes, indirectly: different positions produce
   a different ordered engagement stream.
5. **What tick stage?** The movement proposal stage, unchanged in order.
6. **Total order?** Unchanged; contingent slots iterate ascending and ties break
   on `EntityId`.
7. **Historical claim?** Per-contingent local pause, labelled **Provisional
   reconstruction**. No claim of ranks, dressing, or signals. Section 3 is the
   authority and it constrains the rule rather than decorating it.
8. **Per-tick cost?** No new allocation and no new scan; R1 changes a comparison,
   R2 changes no executable statement, and R3 scales an existing margin.
9. **How verified?** Core suite, the twenty-seed termination sweep, the canonical
   gate, and then a person at a desktop for `BR-1`.

## 7. Why this was blocked, and why it no longer is

This design was blocked while `src/Hukbo.Core/Simulation/BattleSimulation.cs` was
held in the working tree by the cohort lateral spread workstream, which registers
`MovementPresetId.CohortLateralSpreadV13` and edits the same
`UsesBattlefieldRealism` region this design would touch. Implementing into that
file concurrently would have been a merge conflict created on purpose.

**`CohortLateralSpreadV13` has landed, so the block is gone.** It is
`MovementPresetId.CohortLateralSpreadV13 = 13` at
`src/Hukbo.Core/Movement/MovementPresetId.cs:282` and is the last member of the
enum; it is the shipped client default at
`src/Hukbo.Client/Settings/ClientSettingsStore.cs:113-114`; and the canonical gate
blocks on it at `scripts/verify.ps1:105-113`. This design's own preset value is
therefore 14, and 14 does not exist yet.

Nothing here is implemented, and lifting the block does not authorize
implementing it. A plan document under section 6 of `CLAUDE.md` comes first.

## 8. Out of scope

- `FormationPlanner` lane geometry and anchor rules. Owned by the cohort lateral
  spread design, which also declares them out of scope for itself.
- The within-contingent shield-forward rule, which was verified correct and is
  not what `BR-2` is failing on.
- Any change to spacing regularity. Forbidden by section 3.
- Flipping the shipped client default. That is a separate decision taken after a
  person watches a battle.
