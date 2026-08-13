# Contingent cohesion before contact — design

Status: proposed, and **deliberately not yet planned**. A design document does
not authorize implementation, and this one is additionally blocked: it edits
files another workstream is holding. See section 7.

**The row that motivated this document has since closed, and none of this
document was built.** `BR-1` was re-run by a person at an interactive desktop on
2026-08-14 and passed, so the smoke family left the live checklist; its record
is the archived document titled "Battlefield realism cohort smoke — closed
2026-08-14". What changed between the failing run and the passing one was
`CohortLateralSpreadV13` becoming the client's default movement preset, which
changes how the army is laid out laterally, not how a contingent coheres. The
two remedies below, R1 and R2, were checked against the code on 2026-08-14 and
are both still absent: `MovementRules.IsCohesionEligible` still applies the
binary straggler test under `ContingentState.Advance`, and `BattleSimulation`
still marks every excluded slot as overlapping regardless of state. This
document therefore stays live as a standing diagnosis of gates that have not
moved — not as work anybody is waiting on.

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

Any living slot the narrowed scan excludes is marked as overlapping outright,
and the comment above it states the consequence plainly — the denial "resolves it
to Advance through transition rule 4". Contingents in `Close` or `Break` are
excluded from the scan, so they deny themselves, and their squares are then
unavailable to relieve anyone else's overlap either.

The cohesion square is sized by `CohesionRadiusMultiplier`, which is **24** body
radii under the shipped `LastStandEngagementV11`. Eight contingents each claiming
a square of that size inside one half of the map overlap readily. Every
overlapping pair is denied `Hold`, resolves to `Advance`, and under gate 4
`Advance` gathers only stragglers.

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
eligible while its distance from the contingent's aim point exceeds a *cohesion
band* rather than while it exceeds three-quarters of the full radius. The band is
a ruleset field so it is tunable without a code change. This is the single
highest-value change and it alone would move the reported symptom.

**R2 — the blanket narrowed-scan denial is narrowed.** A slot excluded from the
scan because it is in `Close` or `Break` is genuinely fighting and should not
gather; that denial is correct. A slot excluded for any other reason is denied
today for an implementation convenience rather than a gameplay reason. Restrict
the blanket marking to the `Close` and `Break` cases and let the rest be tested
normally.

**R3 — the cohesion square is sized to the contingent, not to a constant.** A
24-body-radius square is claimed by a contingent of three and a contingent of
forty alike, which is what makes overlap the common case rather than the
exception. Size the square from the contingent's own living member count so that
small contingents stop denying their neighbours. This is the change that makes
`Hold` reachable at all under a realistic eight-contingent deployment.

**R4 — nothing about spacing, jitter, or slot geometry changes.** Section 3
forbids it. `FormationPlanner` is not edited by this design at all.

## 5. Versioning and determinism

Any of R1 through R3 moves agent positions, so all three land behind one new
`MovementPresetId` value, appended, never renumbered. That preset gets its own
registry entry, its own registration test, and new golden expectations. The
shipped client default is flipped to it only after a person has watched a battle
and confirmed the effect, not as part of the implementation.

`SplitMix64` draw counts must not change: R1 through R3 all read state that
already exists and none of them draws. If an implementation finds itself needing
a draw, that is a design change and comes back here first.

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
8. **Per-tick cost?** No new allocation and no new scan; R3 changes an existing
   square's dimensions, R2 removes work, R1 changes a comparison.
9. **How verified?** Core suite, the twenty-seed termination sweep, the canonical
   gate, and then a person at a desktop for `BR-1`.

## 7. Why this is blocked, and on what

`src/Hukbo.Core/Simulation/BattleSimulation.cs` is currently modified in the
working tree by the cohort lateral spread workstream, which registers
`MovementPresetId.CohortLateralSpreadV13` and edits the same
`UsesBattlefieldRealism` region this design would touch. Implementing R2 and R3
into that file concurrently is a merge conflict created on purpose.

**This design waits until `CohortLateralSpreadV13` has landed.** Its own preset
value must then be appended after 13, not assigned now.

## 8. Out of scope

- `FormationPlanner` lane geometry and anchor rules. Owned by the cohort lateral
  spread design, which also declares them out of scope for itself.
- The within-contingent shield-forward rule, which was verified correct and is
  not what `BR-2` is failing on.
- Any change to spacing regularity. Forbidden by section 3.
- Flipping the shipped client default. That is a separate decision taken after a
  person watches a battle.
