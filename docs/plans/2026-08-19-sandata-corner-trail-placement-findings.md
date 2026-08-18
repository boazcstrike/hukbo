# Sandata — corner trail placement: three refuted variants and what the geometry actually says

Status: FINDINGS. This document authorizes nothing and lists no tasks. It exists
so that a fifth attempt at the column about-face starts from measurement rather
than from the same three ideas again.

Date: 2026-08-19.

It amends `docs/plans/2026-08-15-sandata-column-about-face-design.md`, whose
section 13 puts `SlotTargets.ComputeTarget` out of scope. That exclusion was
tested here and the result is recorded below: bringing it into scope does not
help either, and the reason is geometric rather than a matter of getting the
placement rule right.

## 1. What was already known

Decision 1 of the parent design — order the column by projected arclength along
the published path, furthest along leads — is implemented on branch
`sandata-about-face-attempt-4` at `4c74336`. It does not fix the deadlock; it
relocates it from the retarget hairpin to the path's own built-in hairpin, and
it costs two tests that passed before. The evidence is in that commit message
and is not repeated here.

## 2. The corner

The published polyline for the seeded objective request on `angle-house.hkmap`
is:

```
(298,690) → (354,466) → (110,110) → (478,122) → (502,122)
```

The third vertex is a corner of roughly 145 degrees. The incoming leg arrives
heading north-west along the unit direction `(-0.566, -0.826)`; the outgoing leg
leaves heading almost due east, `(+1.0, +0.033)`.

`CollisionBodyRadiusRaw` is 4.25 world units, so two centres need 8.5 world
units of separation.

## 3. Three variants, all refuted

Each was implemented in full and run against both `SlotTargetsTests` and the
angle-house acceptance tests.

### Variant 1 — trail offset as an exact straight-line distance

Sample the centreline at `leaderArclength - trailOffset` as today, then move the
sampled point along the line joining it to the leader's anchor until it is
exactly `trailOffset` from that anchor.

**Refuted by `ComputeTarget_OnRightAngleCorner_StaysInCorridor_WhereRigidWorldSpaceOffsetWouldNot`.**
On that fixture the leader stands at `(100,100)` having walked a right-angle
corridor and the follower's correct target is `(50,-15)`. The straight-line
distance between them is 111.8 against a trail offset of 150, so the variant
pushes the target out to `(33,-34)` — through the corridor wall. The test
asserts corridor containment explicitly, and `SlotTargets`' own class
documentation calls that containment "the whole trick".

The lesson is that a follower rounding a corner is *legitimately* closer to the
leader in straight-line terms than its trail offset. Shortness is not the
defect.

### Variant 2 — trail offset as a floor, enforced after the lateral offset

The same idea weakened to a minimum, and applied to the final point rather than
the pre-lateral one, so that a straight path stays byte-identical. Applying it
before the lateral offset is pointless: the lateral offset then spends the
separation that was just enforced.

**Refuted the same way, for the same reason.** The right-angle case is short by
construction, so a floor fires there exactly as an exact distance did.

### Variant 3 — a follower is never ahead of the leader

Drop distance entirely. Project the target onto the leader's own direction of
travel and, if the along-heading component is positive, remove exactly that
component, sliding the target back to the leader's shoulder line while keeping
its perpendicular offset.

This one is clean where the others were not. It is the identity on every
straight path, because the sample is already `trailOffset` back along the
direction of travel and the lateral offset is perpendicular to it and so
contributes nothing to the projection. **All three `SlotTargetsTests` failures
disappear, including the right-angle containment test.**

**It still does not free angle-house.** The squad freezes at `(114.26,118.20)`
instead of `(113.83,118.58)`. The reason is visible in what the clamp does: it
pulls the follower to be *abeam* of the leader, and abeam is the worst available
position. With the along-heading component removed, the whole separation is the
perpendicular component, which is about 8.5 world units — the body diameter
exactly. The pair is in permanent contact and the leader's next step is refused
forever.

## 4. What the geometry actually says

At a 145-degree corner, a follower correctly placed on the incoming centreline
is **ahead of the leader along the outgoing direction, by construction**. With
the leader at the apex `(112.5,110.1)` and a trail offset of about 10 world
units, the follower's centreline position is

```
(112.5, 110.1) + 10 × (0.566, 0.826) = (118.2, 118.4)
```

which is 5.7 world units *east* of the leader that is about to walk east. This
is not a placement error. It is what "ten units back along a path that has just
turned 145 degrees" means.

The margins involved are the reason four attempts have each moved the freeze a
few world units without clearing it:

| Run | Follower's offset from the outgoing line | Outcome |
| --- | --- | --- |
| Before decision 1 | 9.0 wu | rounds the corner |
| After decision 1 | 8.5 wu | jams |
| Body diameter | 8.5 wu | — |

The pre-existing build rounds this corner with roughly half a world unit of
slack. It is not robust; it is lucky. The same map's other hairpin — the
retarget that reverses the path completely — has negative slack, which is the
original bug.

## 5. What this rules in and out

**Ruled out: any rule that only decides where a follower's target point goes.**
Variants 1, 2, and 3 span the plausible space — exact distance, minimum
distance, and directional clamping — and the geometry above says the correct
target is in the leader's way regardless. A target is advisory in any case:
stage 9 proposes and stage 10 refuses, and refusing is precisely the failure.

**Ruled in, and needing its own design: the column has to be able to yield.**
Either the follower gives way and the leader passes, or the leader waits for the
follower to clear the apex before committing its turn. Both are behaviours, not
placements, and neither exists today. The relevant existing machinery is the
collision resolver's commit order, which the stage 0 audit already established is
keyed on `(GroupId, SlotIndex, EntityId)` — the leader already commits first, and
committing first does not help when the blocker simply has not moved yet.

Note that decision 1 remains worth keeping when that design exists. Ordering by
progress is what makes the *labels* correct through a reversal, and every
yield behaviour needs to know who is supposed to be in front before it can decide
who gives way. It is necessary and it is not sufficient.

## 6. What was not tried

Recorded so a fifth attempt does not assume these were eliminated:

- Letting the follower halt while the leader clears the apex, with the halt
  falling out of an existing rule rather than a new timer.
- Allowing a temporary formation collapse at a corner, in the way
  `FormationCollapse` already collapses lateral offsets in a doorway.
- Widening the corridor or re-smoothing the path so that no corner sharper than
  some angle survives into a published polyline. This trades a simulation
  problem for a map and navigation one and may be the cheapest real answer; it
  is untested.
