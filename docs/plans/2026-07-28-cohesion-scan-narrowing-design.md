# Narrowing the cross-contingent cohesion scan

Status: implemented, and the measurement it was adopted for still fails.

This document records a design decision that was taken on evidence, the
implementation that followed it, and the measurement that showed the decision
did not have the effect it was expected to have. All three belong in the same
place, because the third is the reason the first is worth keeping on record at
all.

## 1. What was asked, and by whom

`PersistentContingentTests.CohesionCoverageIsNotPracticallyInertAcrossSeedsOneThroughTwenty`
is the multi-seed inertness bar that section 10.3 of the formation-movement
design requires. It exists to fail loudly if the cohesion feature is built,
fires briefly during deployment, and then never fires again for the rest of a
battle — a feature that is present in the code and absent from the game.

On the merge of `formation-movement-realism` into `main` the bar failed on one
faction-seed:

```
seed 11 faction 1: no cohering tick fell in the later half of the faction's
pre-Close window (length 138) — coverage looks like a burst confined to
deployment.
```

A prior investigation established the proximate cause. The default body radius
had moved from four world units to 4.25 on `main`, which moves every position
in the simulation and therefore reshuffles which faction-seeds sit on which
side of a knife-edge threshold. Pinning the radius back to four made all twenty
seeds pass. The bar was already marginal in both configurations: at radius 4
the lowest best-to-longest ratio across forty faction-seeds was 0.515 against a
0.5 line, and at 4.25 the two lowest were 0.507 and 0.493.

That investigation also traced the mechanism on the failing faction-seed. Over
ticks 69 to 138, contingents c0, c2 and c4 were in `Close` and so denied by
gate 1; c3 was in `Advance` with no straggler and so denied by gate 4; and c1,
the only contingent that could have cohered, was denied by the duty-cycle shut
window from tick 64 to about 105 and then by cross-contingent square overlap
from tick 110 to 138. Two denials tiled the entire later half of the window.

Section 3.5 of the design pre-analysed exactly this failure and named exactly
one remedy for it: narrow the cross-contingent scan to those contingents that
could actually be granted cohesion, excluding `Close` and `Break`. Section 3.5
declined to adopt the narrowing at the time, recorded why, and named it "the
first remedy if the inertness bar in section 10.3 fails". Section 13 question 8
reserved the ordering for the user.

The bar failed. The user answered question 8 in favour of narrowing, and chose
to land it as a new preset with the shipped default flipped to it.

## 2. Why a new preset rather than an edit

`PersistentContingentsV2` is behaviour-frozen by a digest fixture, and
`PersistentContingentsV3` has already shipped as `Scenario`'s default. CLAUDE.md
section 5 requires a new preset version plus new golden expectations for any
change that moves simulated behaviour, and quietly changing what a released
preset does is the specific failure that preset versioning exists to prevent.

`PersistentContingentsV4` therefore carries every one of V3's tunables at the
same value, and differs from it in exactly one field.

## 3. What the narrowing does

`MovementRuleset` gains `NarrowsCohesionScanToCohesionCapableContingents`,
registered `false` for `IndependentPursuitV1`, `PersistentContingentsV2` and
`PersistentContingentsV3`, and `true` for `PersistentContingentsV4` alone.
Adding the field moves every preset's `ContentHash`; all four literals in
`MovementPresetRegistryTests` were recomputed from the built code rather than
calculated by hand.

Under a narrowing preset, movement gate 6 skips any living slot whose contingent
carries `Close` or `Break` at the start of the tick. Such a contingent parks no
cohesion aim point anywhere, because gate 1 already sends every one of its
members to independent pursuit, so excluding it from the scan preserves the
combined aim-point density statement of section 3.5 exactly. What it removes is
the path by which a faction's leading contingents, having reached the enemy and
entered `Close`, went on denying their own rear contingents.

### 3.1 The state read is the previous tick's, and that has a consequence

Gate 6 is an input to the transition-rule stage through its `geometricGatesPass`
argument, so the gate cannot consult the state that stage is about to resolve
without the two becoming mutually dependent. The only reading available is the
contingent's state at the start of the tick, taken from its current leader
before the stage overwrites it.

That costs one tick of latency in both directions, and the second direction is
not harmless. A contingent that *leaves* `Close` on this tick was skipped by
the scan on the strength of a state it no longer holds; granting it cohesion
would park aim points inside a square that no pair ever measured, which is the
combined-density statement gate 6 exists to hold.

Exclusion from the scan is therefore also denial of the grant. A slot the
narrowed scan skips has its overlap flag forced true, which resolves it to
`Advance` through transition rule 4 and lets it rejoin the scan normally on the
following tick. Transition rules 1 and 3 are evaluated first and are unaffected,
so a `Break` stays `Break` and a latched `Close` stays `Close`; the only
contingent whose outcome moves is one that was leaving `Close` anyway, and it
spends one tick in `Advance` before it can be granted anything.

This hole was found while writing the Fact in section 4, not during design. It
would not have been visible from the decision table.

## 4. What is proved

`UnderTheNarrowedScanACloseContingentStopsDenyingItsNeighbours` builds the same
three-contingent geometry as the existing chain-denial Fact — A overlaps B, B
overlaps C, A disjoint from C, all distances computed from
`FormationRules.ComputeContingentJitterRaw` and `ComputeContingentTrailRaw`
rather than guessed — and starts the middle contingent B in `Close`.

Under `PersistentContingentsV2` the identical arrangement denies all three,
because B's square is scanned and denies both its neighbours. Under
`PersistentContingentsV4` B is excluded, A and C are measured only against each
other, find themselves disjoint, and are granted cohesion: A and C resolve to
`Hold` and B to `Advance`. B resolving to `Advance` rather than `Hold` is the
section 3.1 rule, asserted rather than incidental.

The narrowing does what section 3.5 said it would do.

## 5. What the measurement found

It does not fix the inertness bar. The bar was retargeted from
`PersistentContingentsV2` to the shipped default, and the failure message was
extended to report the latest cohering tick actually observed alongside the
threshold it missed — a diagnostic addition, not a threshold move. Two facts
came out of that.

**`PersistentContingentsV3`, the default that was already shipping, fails the
bar on eight faction-seeds.** The bar had been pointed at V2 throughout, so the
shipped preset had never been measured against it at all. The single failing row
on `main` was an artefact of measuring a superseded preset.

| preset | failing faction-seeds |
| --- | --- |
| `PersistentContingentsV2` (what the bar measured on `main`) | 1 |
| `PersistentContingentsV3` (what actually shipped) | 8 |
| `PersistentContingentsV4` (the narrowing) | 11, plus one persistence row |

**The narrowing left the latest cohering tick exactly where it was.** For every
faction-seed the V3 and V4 runs share, the value is identical:

| faction-seed | latest cohering tick | window under V3 | window under V4 |
| --- | --- | --- | --- |
| seed 1, faction 1 | 68 | 205 | 376 |
| seed 3, faction 0 | 108 | 274 | 409 |
| seed 8, faction 0 | 101 | 242 | 207 |
| seed 11, faction 1 | 68 | 243 | 298 |
| seed 15, faction 0 | 101 | 246 | 339 |
| seed 19, faction 0 | 102 | 236 | 253 |

Cohesion stops firing somewhere around ticks 68 to 128 under V2, V3 and V4
alike. What moved is the denominator. Pre-`Close` windows roughly doubled when
V3's contact-fraction close latch landed — seed 11 faction 1 went from 138 to
243 — and lengthened again under V4, because a faction that coheres more
advances more slowly and reaches contact later.

The spread clause compares the latest cohering tick against half of the
faction's *longest* contingent window. Lengthening windows moves that threshold
away from a numerator that does not move with it. The clause is not detecting
chain denial; it is detecting that the threshold drifted past the point where
cohesion was ever going to fire, and the narrowing pushes the threshold further
out than it pulls cohesion later.

## 6. The question this opens

Section 10.3 requires the cause to be established before any threshold moves.
It named chain denial as the first suspect. Chain denial has now been tested
directly, by removing it, and the clause failed anyway on more faction-seeds
than before. The cause is established, and it is a different one.

The open question is section 13 **question 7** — are the bar's three thresholds
the right bar — and not question 8, which is now closed. Question 7 already
recorded that the three thresholds are game-design inventions with no
measurement behind them, and that they may prove too strict on first
measurement. This is that first measurement, on the preset that ships.

Nothing in this document authorises moving a threshold. Answering question 7
needs one thing this document does not have.

## 7. The one thing not established

Why cohesion stops firing near tick 68 to 128 is not known.

The duty cycle shuts each slot's window somewhere in ticks 60 to 194 depending
on its phase offset, and reopens it sixty ticks later. A reopened window should
produce further grants and does not appear to. Whether the reopened window is
being denied by a gate, or whether the contingents have all entered `Close` or
`Break` by then, or whether something else closes it, has not been traced.

That trace is the next step, and it should come before any threshold is
touched. A threshold moved to accommodate a mechanism nobody has looked at is
the failure section 10.3 was written to prevent.

## 8. State of the tree when this was written

`main` carries pre-existing recapture debt from the `formation-movement-realism`
merge, unrelated to this work: two frozen-trajectory digests and two pinned
determinism hash pairs are stale against the 4.25 body radius, and one of the
pinned pairs is still the literal placeholder `RECAPTURE_STATE`. Those five
failures are not addressed here.

`main` also moved while this work was in progress. Commit `e197df7`,
"fix(test): derive packed-front line separation from body radius", landed
independently and fixed `CollisionRegressionTests.PackedFront`, which was one of
`main`'s six failures. That fix has nothing to do with the narrowing: the
packed-front rows asked for a nine-world-unit separation and called it one
diameter, which stopped being true when the body radius became 4.25. This branch
is cut from `e197df7`, so `PackedFront` passes here for that reason and not for
this one.

With this change in place, `Hukbo.Core.Tests` reports 720 passing and 5 failing.
`CohesionCoverage` now reports twelve rows where it reported one, for the
reasons in section 5.

`BattleSimulationTests.RepeatedCollisionTicksHaveBoundedAllocations` failed in
two of four full-suite runs and passed every time in isolation, and did not fail
on `main`. The new code path performs two lookups per slot per tick and
allocates nothing, so this is most likely GC-pressure flakiness under full-suite
load rather than a regression, but that has not been proved and the row should
be treated as open.

The canonical gate was not run. It cannot pass while the recapture debt stands,
and a gate failure that reports on unrelated stale fixtures says nothing about
this change. What was run: `dotnet build Hukbo.slnx -c Release` clean with zero
warnings under `TreatWarningsAsErrors`, `scripts/format.ps1 -Verify` passing,
`Hukbo.Client.Tests` at 2435 of 2435, and the `Hukbo.Core.Tests` results above.
