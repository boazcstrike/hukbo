# Contingent chief membership — task plan

Date: 2026-08-14
Status: plan document. The design pass it converts into tasks is
[`2026-08-14-contingent-chief-membership-design.md`](2026-08-14-contingent-chief-membership-design.md),
and that document's sections A through G are authoritative over the summaries
here.

**Implementation was authorized on 2026-08-14**, after the design was written
and after the user took the decision it was written to make. The decision, in
full:

> Take the design's section 7 recommendation — make `ContingentShapeV12`
> selectable in the client first, then accept option 1: ship chief-derived
> contingent count and drop the chief-per-contingent claim.

That closes section 6b of `docs/plans/2026-08-13-contingent-shape.md`, which
recorded three options and said none should be chosen under time pressure.

## 1. What is being delivered, and what is not

**Delivered.** Two things, in this order.

1. `MovementPresetId.ContingentShapeV12` becomes selectable by a spectator on
   the Army Composition panel, and the Client test that was supposed to catch
   its absence is strengthened so the next preset cannot go missing the same
   way.
2. The chief-per-contingent claim is withdrawn from the design document that
   makes it and from the plan that carries it, and both are corrected to say
   what V12 actually does.

**Not delivered, deliberately.**

- The client *default* is not touched by this package. It was
  `MovementPresetId.LastStandEngagementV11` when the design was written and is
  now `MovementPresetId.CohortLateralSpreadV13`
  (`src/Hukbo.Client/Settings/ClientSettingsStore.cs:91-92`), moved by another
  session's V13 work while this was in flight. Pointing it at V12 was never
  proposed and is not proposed now: the selector is a staged, player-driven
  choice, so observability does not depend on the default.
- `CohortDeploymentAssignment` is not modified. Options 2 and 3 of the design
  are not taken; the reasoning is design sections 2.2, 3, and 7.2.
- No smoke row is flipped. Two new rows are added and left `PENDING` for a
  person, which is the only thing an agent may do to that file.

## 2. Ordered task list

### Task 1 — add V12 to the player-facing selector

Append `MovementPresetId.ContingentShapeV12` to
`ArmyCompositionPanel.MovementPresetOptions` and the matching display name
`"V12 Contingent Shape"` to `ArmyCompositionPanel.MovementPresetNames`, keeping
both lists in enum order as their doc comments require.

- Files: `src/Hukbo.Client/UI/ArmyCompositionPanel.cs`.
- Verification: the Client suite is green, and `SettingsChoiceSelector`'s
  constructor invariant (equal, non-empty counts, at
  `src/Hukbo.Client/UI/SettingsChoiceSelector.cs:50-54`) still holds.
- Presentation only. No simulation code, no hash, no fixture.

### Task 2 — make the Client test actually enumerate the registry

`EveryRegisteredMovementPresetHasAMatchingDisplayName`
(`tests/Hukbo.Client.Tests/ArmyCompositionPanelTests.cs`) asserts only that the
two lists have equal length and that the option list has no duplicates. It never
consults `MovementPresetRegistry`, which is exactly how V12 shipped registered
but unselectable with a green suite. Strengthen it to enumerate every
`MovementPresetId` value, assert the registered ones are all present in the
option list, and assert the option list contains nothing unregistered.

- Files: `tests/Hukbo.Client.Tests/ArmyCompositionPanelTests.cs`.
- Verification: the strengthened test fails on the unmodified panel — confirm
  that by running it before task 1 is applied, or by reverting task 1 briefly —
  and passes after.
- Depends on: nothing, but it is only *green* once task 1 lands.

### Task 3 — repair the preset-cycling test

`ArrowKeysCycleTheDraftMovementPresetWhileFocusedOnItsRow` steps forward from
V11 and asserts it wraps to V1. That assertion encodes "V11 is the last entry",
so task 1 breaks it. Re-point it at the new last entry rather than deleting the
wrap coverage, which is the property worth keeping.

- Files: `tests/Hukbo.Client.Tests/ArmyCompositionPanelTests.cs`.
- Verification: the test still exercises a forward step and a backward step and
  still asserts the wrap.

### Task 4 — correct the design document

`docs/plans/2026-07-29-contingent-shape-design.md` section 4 proposes rank-aware
deployment and its acceptance answer 1 promises "each with a `Datu`-rank warrior
founding it". Both are now withdrawn. Correct them in place, marked
**Corrected 2026-08-14**, in the same style the document's eight existing
corrections use, and record why: `CohortDeploymentAssignment` partitions the
same warriors by weapon downstream, and a set cannot be partitioned by weapon
and by rank at once.

- Files: `docs/plans/2026-07-29-contingent-shape-design.md`.
- Verification: none needed; documentation correction.

### Task 5 — close section 6b of the contingent-shape plan

`docs/plans/2026-08-13-contingent-shape.md` section 6b lists three options and
says the decision is open. Record that option 1 was taken on 2026-08-14, name
the design document that priced the three, and state V12's honest description.
Task 7's own entry is updated to say what shipped instead of what was intended.

- Files: `docs/plans/2026-08-13-contingent-shape.md`.
- Verification: none needed; documentation correction.

### Task 6 — add the smoke rows

Two new rows, both `PENDING`, both for a person: one that V12 appears in the
Army Composition panel's movement-preset selector and applies on the next full
reset, and one for how a chief-derived deployment reads on screen against V11's
square-root split. The nearest existing rows are `BR-1` and `BR-4`.

- Files: `docs/development/smoke-checklist.md`.
- **No existing row may be flipped**, and the status counts at the top of that
  file are recounted at write time rather than adjusted from a remembered
  number — another session edits this file live.

### Task 7 — index the two new documents

Add the design and this plan to `docs/plans/README.md`.

## 3. Verification criteria

- `./scripts/verify.ps1 -SkipBootstrap` green, with its real output pasted. Run
  once, after integration, not delegated.
- Both suites, not just Core: this is a Client-only source change, and this
  repository has been caught by a Core change reddening Client tests and by
  Client tests pinning things nobody expected them to pin.
- Every V1–V11 frozen digest, deployment fixture, and pinned trajectory must be
  byte-identical. No simulation source is touched, so any movement is a defect
  and the change is reverted rather than re-pinned.
- The strengthened test in task 2 must be shown to fail before task 1 and pass
  after. A test that passes both ways proves nothing.
