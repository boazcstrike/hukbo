# Lethal blow legibility: plan

**Archived: reference only.** This is a finished plan. Its tasks were built and
merged, and the two smoke rows it existed to close, 92 and 94, were re-run by a
person on 2026-08-14 and both passed. Never execute it, never treat it as a live
task list, and never cite it as the reason to make a change. The live contract
for this project remains `CLAUDE.md` and `docs/development/smoke-checklist.md`.
Read "How this closed, 2026-08-14" at the foot of this document before assuming
every requirement in it was met — one was not.

Date: 2026-08-13
Design: [`2026-08-13-lethal-blow-legibility-design.md`](2026-08-13-lethal-blow-legibility-design.md)

Read the design before executing anything here. It records why a kill is hard to
see today, and section 4 records the evidence-based restraint this change
deliberately reverses.

Every task is in `Hukbo.Client` or its test project. Nothing here may touch
`Hukbo.Core`, `Hukbo.Shared.Core`, or either headless runner.

## Ownership

Two workstreams run in parallel on disjoint files. Neither may edit a file owned
by the other; if a task appears to need one, stop and report it instead.

| Workstream | Owns |
| --- | --- |
| **A — hit effect, pulse, hold** | `src/Hukbo.Client/Presentation/HitEffect.cs`, `HitEffectSystem.cs`, `AttackAnimation.cs`; `src/Hukbo.Client/Rendering/HitEffectGeometry.cs`, `HitEffectRenderer.cs`; `tests/Hukbo.Client.Tests/HitEffectSystemTests.cs`, `HitEffectGeometryTests.cs`, `PresentationCoordinatorTests.cs`, `Presentation/AttackAnimationSystemTests.cs`, `Presentation/DefenderReactionSystemTests.cs` |
| **B — blood, gore level** | `src/Hukbo.Client/Presentation/BloodEffect.cs`; `src/Hukbo.Client/Rendering/BloodGeometry.cs`, `BloodRenderer.cs`; `src/Hukbo.Client/Settings/ClientSettingsStore.cs`; `tests/Hukbo.Client.Tests/BloodGeometryTests.cs`, `BloodEffectSystemTests.cs`, `ClientSettingsStoreTests.cs` |

Documentation tasks D1 to D4 belong to the integrator and run after both
workstreams report.

## Workstream A — make the kill land on a body

**A1. Hold the pawn under its own death.** In
`src/Hukbo.Client/Presentation/AttackAnimation.cs`, raise `LethalHoldSeconds`
from `0.10f` to `0.34f`. Keep the doc comment's `PROVISIONAL` marker and extend
it: the hold now exists so the ring, the pulse, and the blood burst have a body
to play over, and it is still a hold, not a corpse layer.
*Verification:* `AttackAnimationSystemTests`, `DefenderReactionSystemTests`, and
`PresentationCoordinatorTests` all build green. Any test asserting the old
`0.10f` is recaptured against the new literal and its name updated to match.

**A2. Lengthen the lethal ring.** In
`src/Hukbo.Client/Presentation/HitEffect.cs`, raise the lethal lifetime from
`0.28f` to `0.50f`. Leave the ordinary `0.18f` alone.
*Verification:* `HitEffectSystemTests.Advance_ExpiresLethalAt280Milliseconds`
fails on the old literal; rename it to name 500 milliseconds and recapture both
boundary assertions. `Advance_ExpiresOrdinaryAt180Milliseconds` must stay green
untouched — if it moves, the ordinary tier was edited by mistake.

**A3. Give a killing blow its own pulse.** `HitEffectSystem` currently excludes
lethal effects from the hit pulse in two places. Replace the exclusion with a
lethal pulse of its own duration: add `LethalPulseSeconds = 0.30f` beside the
existing `PulseSeconds = 0.09f` and use it when the effect is lethal.
The invariant that makes this safe: **the lethal pulse must be strictly shorter
than `LethalHoldSeconds`**, because once the hold expires there is no pawn to
pulse.
*Verification:* `HitEffectSystemTests.GetPulseStrength_ReturnsPositiveOnlyForTheLivingHitWindow`
currently asserts `0f` for a lethal effect at tick 7. Recapture it: a lethal
effect now pulses, and the test's name must stop claiming otherwise. Add a new
test asserting the lethal pulse has fallen to `0f` at an age of `0.34f`
seconds — written against that literal, not against a constant read out of the
code under test.

**A4. Widen the lethal tier of the geometry.** In
`src/Hukbo.Client/Rendering/HitEffectGeometry.cs`, change only the lethal side
of each ternary:

| Value | From | To |
| --- | --- | --- |
| lethal shard count | `8` | `12` |
| lethal ring start radius | `8f` | `11f` |
| lethal ring travel | `18f` | `30f` |
| lethal ring thickness multiplier | `1.35f` | `2.1f` |
| lethal shard travel | `24f` | `38f` |
| lethal shard length | `8f` | `14f` |

`RingCount` stays `2` for lethal and `1` for ordinary. The ordinary side of
every ternary is unchanged, and the `MinimumApparentScale` / `MaximumApparentScale`
clamp is unchanged.
*Verification:* `HitEffectGeometryTests.Create_LethalEffectsHaveTwoRingsAndEightLongerShards`
fails on the shard count; recapture it to `12` and rename it so the name is not
a lie. Its three ordering assertions — lethal travel and length exceed
ordinary — must still hold after the change, and are the reason the ordinary
side may not be touched.

**A5. Separate the two tiers by colour.** In
`src/Hukbo.Client/Rendering/HitEffectRenderer.cs`, an ordinary hit draws
`(255, 244, 214)` and a lethal one draws `Color.White` — eleven units apart in
one channel. Move the lethal ring and shard colours to a hot, unmistakably
different tone rather than a brighter white, and give both a short comment
marking them as provisional legibility tuning under `CLAUDE.md` §7, not a
historical or evidentiary claim. Lethal ring segment count may rise from `24` to
`28`; the ordinary `18` stays.
*Verification:* build green. This task is visual and closes only under row 92.

**A6. The second copy of the lifetimes, found by the test run.** A1 through A5
left the suite with two failures, and one of them was a real defect rather than
a stale literal. `DefenderReactionSystem.cs` carries its own
`LifetimeSeconds => IsLethal ? 0.28f : 0.18f` — an independent copy of the pair
`HitEffect` used to hold. `IsLethalHoldActive` reads the reaction buffer and
returns false once the record has been dropped, and `Advance` drops a record at
`AgeSeconds >= LifetimeSeconds`. A lethal reaction therefore died at `0.28`
seconds and **the hold could never reach the `0.34` A1 declares**, which also
put the new `0.30` second pulse outside the window in which there is still a
pawn to pulse. A1 would have shipped as a smaller change than it claimed to be.
Raise the lethal side to `0.50f`, matching `HitEffect`, and record the ordering
that has to hold — reaction lifetime `0.50` > `LethalHoldSeconds` `0.34` >
`LethalPulseSeconds` `0.30` — in a doc comment and in a test written against
those literals.
*Verification:* `DefenderReactionSystemTests.Advance_ExpiresBoundedReactionsAndClearRemovesAllState`
passes again without being edited, which is the point: it was failing because
the code was wrong, not because it was.

## Workstream B — make the blood carry the kill

**B1. Lengthen the lethal blood.** In
`src/Hukbo.Client/Presentation/BloodEffect.cs`: `BloodBurst`'s
`LethalLifetimeSeconds` from `0.42f` to `0.62f`, `GroundMark`'s
`LethalLifetimeSeconds` from `5.2f` to `8f`, and `LethalSpurt`'s
`SpurtLifetimeSeconds` from `0.85f` to `1.1f`. Ordinary lifetimes and the
`DenseLifetimeMultiplier` are unchanged.
*Verification:* `BloodGeometryTests.CreateGroundMark_LethalMarkOutlivesThePairedBurst`
and `Create_LethalTierIsDistinctFromTheOrdinaryTier` assert orderings that must
still hold. `BloodEffectSystemTests.Advance_ExpiresBurstsBeforeTheGroundMarksTheyLeave`
advances by `5f` and expects the mark gone; recapture that literal against the
new lifetime and say in the test name what it now proves.

**B2. Widen the lethal spray.** In `src/Hukbo.Client/Rendering/BloodGeometry.cs`:

| Value | From | To |
| --- | --- | --- |
| `MaximumDropletCount` | `8` | `12` |
| `LethalDropletBonus` | `1` | `4` |
| `LethalTravelMultiplier` | `1.35f` | `1.75f` |
| `LethalThicknessMultiplier` | `1.25f` | `1.55f` |
| lethal ground-mark radius | `5.6f` | `8.4f` |
| lethal ground-mark alpha | `0.85f` | `0.95f` |
| `SpurtStrandCount` | `8` | `10` |

The clamp to `MaximumDropletCount`, the two `LowDetailScale` throttles, and the
ordinary tier all stay exactly as they are. Droplet counts stay hard-capped;
the cap rises, it does not disappear.
*Verification:* `Create_OverkillSeverityClampsToTheSeverityOneVisuals` and
`Create_LethalTierIsDistinctFromTheOrdinaryTier` stay green. Add one test
asserting a lethal, full-severity burst at ordinary detail produces no more than
`12` droplets, written against the literal.

**B3. Make `Full` the shipped default.** In
`src/Hukbo.Client/Settings/ClientSettingsStore.cs`, change
`DefaultGoreIntensity` from `GoreIntensity.Stylized` to `GoreIntensity.Full`.
Change nothing else: the enum's numeric values, `GoreIntensityManager`'s
`FallbackValue`, and `BloodEffectSystem`'s own field initialiser all stay as
they are, so a settings file that already records a level keeps resolving to
that same level.
*Verification:* `ClientSettingsStoreTests` currently asserts the default is
`Stylized`; recapture it to `Full`. Add a test proving a stored `Stylized` value
still loads as `Stylized`, so the change is provably a default and not an
override. `BloodEffectSystemTests.Intensity_RejectsUndefinedValues`, which
asserts the *system's* own initial value, must stay green and untouched — if it
turns red, B3 changed more than the default.

**B4. Rewrite the restraint, do not delete it.** `LethalSpurt`'s doc comment in
`BloodEffect.cs` says the Stylized default never produces a spurt "because a
sustained spurt carries an anatomical reading the evidence does not support".
After B3 that sentence is false. Replace it with one that records both halves:
what the restraint was, and that it was overridden on 2026-08-13 on the explicit
request of the person the presentation is for, as provisional legibility tuning
rather than an evidentiary claim. Name this design document in the comment.

## Documentation — integrator only

**D1. Record the smoke result.** Rows 90, 91, and 93 through 98 of
`## Tactical hit animations smoke` in `docs/development/smoke-checklist.md`
closed `PASS` on 2026-08-13. Lift them into a dated archive record titled
"Tactical hit animations smoke — closed 2026-08-13", written in the same shape
as the other records already archived under that date; name it by its title
rather than by its path, because no file outside the archive folder may write a
path into it. Row 92 stays in the
checklist, rewritten to carry the tester's verbatim observation and what was
changed in response, and stays `PENDING`. Row 94 is reopened by this change and
returns to the checklist as a re-run, with section 6 of the design as its
reason.

**D2. Recount, never estimate.** Update the header of the checklist from the
file itself after D1, not by arithmetic: section count, and the counts of
`PENDING`, `BLOCKED`, `FAIL`, and `DECLINED`. Extend the running tally of lifted
rows and the "families deleted whole" narrative, naming the new archive record
in prose. This family is not in the batch table, so there is no batch row to
remove — but the family now has two open rows and belongs there.

**D3. Index the plan.** Add this plan and its design to `docs/plans/README.md`
with their real state.

**D4. Record the gate.** Run `./scripts/verify.ps1` once, after both workstreams
have integrated, and paste the actual output into this document. It is not
delegated to any agent, and it does not close row 92 or row 94.

## What was actually run, 2026-08-13

The canonical gate was **not** run to a green verdict, and the reason is worth
recording rather than hiding. The working tree was shared with concurrent,
unrelated work on the armor flank bars — `PawnGeometry.cs` and
`PawnRenderer.cs` were both modified by another session while this change was
being built — and that work leaves
`PawnGeometryTests.GetArmorFlankBars_ReturnsSymmetricBarsInsideTheArmoredCapsule`
red at bar width 63 against an expected 62. `./scripts/verify.ps1` stops at its
test stage on that failure, so running it would have produced a red verdict that
said nothing about this change. Nothing in this plan touches pawn geometry.

What was run instead, with the real results:

| Stage | Command | Result |
| --- | --- | --- |
| Format verification | `./scripts/format.ps1 -Verify` | `[PASS] Formatting verification completed.` Formatted 0 of 752 files |
| Core tests | `./scripts/test.ps1 -Configuration Release` | `Test Run Successful. Total tests: 2492, Passed: 2492` |
| Client tests | same command, second suite | `Total tests: 3687, Passed: 3686, Failed: 1` — the single failure is the concurrent armor-bar work named above, not this change |
| Seed-1 determinism workload | `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1` | `[PASS] Headless workload completed: agents=200 ticks=10000 seed=1`, `outcome: Faction0Victory`, `stateHash 5460D13E3F7FD3E5`, `eventHash 8E18ED1437B2924B` |

Those two digests are identical to the recorded baseline for the shipped default
workload — combat preset 6, movement preset 4 — in
`docs/development/testing.md`. That is the automated
half of the guarantee that this change is presentation only. The interactive
half is smoke row 98, which a person already passed.

**The gate still owes a green run.** Once the armor-bar work has landed,
`./scripts/verify.ps1` must be run once on an uncontaminated tree and its output
recorded here, replacing this section rather than sitting beside it.

**Interim note, 2026-08-13.** Later the same day `./scripts/verify.ps1
-SkipBootstrap` was run and completed with `[PASS] Canonical repository
verification completed.` and exit code 0. That result is recorded here for what
it is and for nothing more. It does **not** satisfy the requirement above,
because the working tree it ran over was not an uncontaminated one: alongside
this change it carried the armor-bulk second fix, the corpse placeholder, the
projectile prop scale change, and the auto-camera centring change. A green
verdict over four other changes at once tells us that the five together do not
break the gate; it does not isolate this plan, which is the whole point of the
run the requirement asks for. **The requirement is therefore not yet
satisfied**, and this note stands beside it rather than replacing it until a
single-plan gate run exists to record.

## What none of this proves

Every task above can be green while a kill still fails to read on screen. Row 92
closes when the person who reported it says a kill is unmistakable, and row 94
closes when the same person confirms a crowded exchange stayed legible and
bounded. No test, no build, and no gate may flip either.

## How this closed, 2026-08-14

**Both rows passed.** A person at an interactive desktop re-ran row 92 and row
94 against the shipped build on 2026-08-14 and passed both, with no separate
observation recorded for either. That closed the tactical hit animations family
at 9 of 9, its section was deleted from the live checklist, and the two verdicts
are recorded in the 2026-08-14 archive titled **"Tactical hit animations smoke —
closed 2026-08-14"**, named here in prose rather than linked.

**The green single-plan gate run this document asks for was never obtained, and
this plan is being archived without it.** The requirement is the one stated
above: once the concurrent armor-bar work had landed, `./scripts/verify.ps1` was
to be run on an uncontaminated tree and its output recorded here. It was
attempted on 2026-08-14 and it failed, for a new reason rather than the old one.
The gate stopped at its Release build stage with ten instances of `error CS7036`
in `tests/Hukbo.Core.Tests/Movement/CohortDeploymentAssignmentTests.cs`,
reporting no argument for a required `spreadCohortsLaterally` parameter on
`CohortDeploymentAssignment.AssignForFaction`. That is a concurrent session's
in-progress cohort lateral spread work, which had added the parameter without
yet updating the test's call sites. Nothing in this plan touches cohort
deployment.

What was run instead on 2026-08-14, with the real results:

| Stage | Command | Result |
| --- | --- | --- |
| Canonical gate | `./scripts/verify.ps1 -SkipBootstrap` | **FAILED** at the Release build stage. `Build FAILED. 0 Warning(s) 10 Error(s)`, all ten `CS7036` in `CohortDeploymentAssignmentTests.cs`, from concurrent unrelated work |
| Format verification | `./scripts/format.ps1 -Verify` | `[PASS] Formatting verification completed.` Formatted 0 of 762 files |
| Client tests | `dotnet test tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj -c Release` | `Passed! - Failed: 0, Passed: 3791, Skipped: 0, Total: 3791` |

Every project this plan touches — `Hukbo.Client` and `Hukbo.Client.Tests` —
built and passed in that run. The determinism workload was not reached, because
the gate never got past its build stage.

**Read this as the debt it is.** The two rows above are closed on a person's
eyes, and this change is presentation-only by construction, so nothing here
reaches a state hash. But the requirement this plan wrote for itself — one green
gate run isolating this change — has still never been satisfied, and archiving
the plan does not satisfy it. If a future reader needs certainty that this
change alone leaves the gate green, that run has yet to happen.
