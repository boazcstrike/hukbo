# Death collapse and the prone body — plan

**Archived: reference only.** This is a finished plan. All nine `DC` tasks were
built, tested, and merged to `main` in the feature commit `0d4b34e`, which the
canonical gate passed on 2026-08-14. Never execute it, never treat it as a live
task list, and never cite it as the reason to make a change. The live contract
for this project remains `CLAUDE.md`, `SIMULATION-GAME-STANDARDS.md`,
`docs/development/testing.md`, and `docs/development/smoke-checklist.md`.

The ten `DC` smoke rows this plan created are still `PENDING` in
`docs/development/smoke-checklist.md` and stay there: nobody has yet watched a
body fall over, and only a person at an interactive desktop may close them. Read
"How this closed, 2026-08-14" at the foot of this document before assuming
anything here is still accurate.

Date: 2026-08-14. Design: `2026-08-14-death-collapse-design.md`, alongside this
file in the same archive folder, which outranks this document wherever the two
disagree.

Task prefix `DC`. Every task names the files it owns; no two tasks own the same
file.

## Tasks

### DC-1 — `PawnTransform`

**File:** `src/Hukbo.Client/Rendering/PawnTransform.cs` (new).

A rigid plane transform stored as an angle plus a translation
(`p ↦ rot(θ)·p + t`), per design section 4. Members: `Identity`, `IsIdentity`,
`AboutPivot(pivot, radians)` returning `Identity` for a zero angle,
`Then(outer)` composing two, and `Apply(point)`.

**Verification:** unit tests — identity is a no-op; `AboutPivot` by π/2 about the
origin maps (1,0) to (0,1); composing two rotations about different pivots agrees
with applying them one after the other, to within float tolerance; composing with
identity in either position is the other value.

### DC-2 — `CollapsePose`

**File:** `src/Hukbo.Client/Rendering/CollapsePose.cs` (new).

The pure collapse curve of design section 3: `CollapseSeconds` (0.45),
`ImpactShare` (0.82), `SettleOvershootRadians` (0.10), `ProneRotationRadians`
(π/2), `FallJitterRadians` (0.14), all PROVISIONAL and documented as such.
`Resolve(ageSeconds, finalRotationRadians)` returns the angle;
`ResolveFinalRotation(fallSign, entityId)` returns `±(π/2 + jitter)`.

**Verification:** unit tests — zero at age zero; exactly `finalRotation` at and
after `CollapseSeconds`; strictly increasing in magnitude through the fall
segment; peak magnitude exceeds `|finalRotation|` by at most
`SettleOvershootRadians`; jitter magnitude never exceeds `FallJitterRadians`;
sign follows `fallSign`; the same entity id always yields the same final angle.

### DC-3 — the fall-direction salt

**File:** `src/Hukbo.Client/Presentation/PresentationSalts.cs`.

Add `DeathFallJitterSalt` with a value distinct from every existing salt, and its
entry in `All`. Consumed only by DC-2.

**Verification:** the existing pairwise-distinctness test covers it.

### DC-4 — `DeathCollapseSystem`

**File:** `src/Hukbo.Client/Presentation/DeathCollapseSystem.cs` (new).

Ordinal-indexed store per design section 7, with the entity id stored beside each
entry and compared on read. `Observe(agents, defenderReactions)`,
`Advance(elapsedSeconds)`, `TryGetPose(ordinal, entityId, out …)`, `Clear()`.

**Verification:** unit tests — a living agent registers nothing; an agent inside
its lethal hold registers nothing; an agent past its hold registers once and its
age advances; a re-`Observe` never restarts a registered collapse; a lookup with
a mismatched entity id misses; the fall sign follows
`DefenderReaction.DirectionX` and falls back to the entity-id low bit when the
direction is absent or vertical; `Clear` empties it.

### DC-5 — the layout carries the transform

**File:** `src/Hukbo.Client/Rendering/PawnGeometry.cs`.

Add `PawnTransform Collapse` to `PawnLayout`, defaulted to `Identity` on every
existing construction path. Thread an optional collapse rotation through
`Create`, `CompletePosedLayout`, and `CompleteAttackPosedLayout`. Add the prone
cull envelope of design section 5 to `PoseBlindPrefix` as a separate member, so a
caller asks for it explicitly rather than getting it by default.

**Verification:** unit tests — every existing construction path still returns
`Identity`; the prone envelope contains the standing bounds rotated by every
angle in a sampled sweep about the foot anchor.

### DC-6 — the renderer routes every quad

**File:** `src/Hukbo.Client/Rendering/PawnRenderer.cs`.

Add `DrawQuad(spriteBatch, pixel, rectangle, color, transform)` taking the
existing axis-aligned overload when the transform is the identity. Route every
quad and every line endpoint through it, threading `layout.Collapse` down. The
ground ring is the one documented exception (design section 4). Compose the
shield's posture rotation with the collapse through `PawnTransform.Then`. Soften
`ApplyState`'s blend to 0.40 and gate `DrawDeadMark` to `PawnDetailTier.Low`
(design section 6).

**Verification:** unit tests over the pure helpers; the whole `Hukbo.Client.Tests`
suite for regressions on living pawns.

### DC-7 — the submission count follows

**File:** `src/Hukbo.Client/Rendering/SubmissionCount.cs`.

`CountStateMark` takes the detail tier and returns `DeadMarkQuadCount` for a dead
pawn at Low only.

**Verification:** `PawnQuadCountTests` — a Medium or High corpse counts two fewer
than today; a Low corpse is unchanged; no living state moves.

### DC-8 — wiring

**Files:** `src/Hukbo.Client/Presentation/PresentationCoordinator.cs`,
`src/Hukbo.Client/ArenaGame.cs`, `src/Hukbo.Client/ArenaGame.Rendering.cs`.

Own `DeathCollapse` on the coordinator; advance it inside the `advanceContacts`
group; clear it in `ResetFor`; expose `ObserveDeaths(agents)`. Call that from
`ArenaGame.Update` immediately after `ReleaseAttackContactsForDraw`. In
`DrawPawnPass`, use the prone cull envelope for a dead agent and pass the
resolved collapse rotation into the posed layout.

**Verification:** `PresentationCoordinatorTests` — the coordinator constructs,
advances, and clears the new system; the whole Client suite for regressions.

### DC-9 — gate and evidence

Run `./scripts/verify.ps1` and paste the real output into this document's "What
was run" section. Add the DC smoke rows to
`docs/development/smoke-checklist.md` as `PENDING`. No agent flips one.

## Smoke rows

| Row | What a person checks |
| --- | --- |
| DC-1 | A warrior that dies visibly topples over rather than changing colour in place |
| DC-2 | The fallen body ends flat on the ground — horizontal, not leaning or tilted |
| DC-3 | The body falls away from the blow that killed it, not toward it |
| DC-4 | A field of corpses reads as bodies rather than as a stamped repeating pattern |
| DC-5 | Pausing mid-fall freezes the body mid-fall; resuming continues it |
| DC-6 | Corpses do not occlude fights in progress |
| DC-7 | A corpse at the arena panel's edge stays drawn until it is genuinely off screen |
| DC-8 | At 500 per team the corpse field is readable rather than visual noise |
| DC-9 | At minimum zoom (Low tier) a dead warrior is still distinguishable from a living one |
| DC-10 | The softened desaturation still reads as dead, not as a differently-dyed living warrior |

## What was run

All nine tasks are done. The canonical gate was run on 2026-08-14 with
`./scripts/verify.ps1 -SkipBootstrap` and printed one verdict per stage:

```
[PASS] Formatting verification completed.
[PASS] Release solution build completed.
[PASS] Release repository tests completed.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Headless workload completed: agents=200 ticks=10000 seed=1.
[PASS] Canonical repository verification completed.
```

A prior full run of `./scripts/verify.ps1` on the same tree printed the headless
report, whose digests confirm the simulation was not touched:

```
"outcome": "Faction1Victory",
"faction0Survivors": 0,
"faction1Survivors": 27,
"eventHash": "E0CE32CF8830A864",
"stateHash": "4A0723BC9A1B924B",
"deterministic": true,
"firstMismatchTick": null,
"combatPreset": 5,
"movementPreset": 13
```

`Hukbo.Client.Tests` on its own:

```
Passed!  - Failed:     0, Passed:  3848, Skipped:     0, Total:  3848
```

That suite stood at 3,785 tests before this work, of which three failed once the
change landed and were then corrected rather than weakened, all three for
reasons the change makes true:

- `PresentationSaltsTests.RegistryListsAllThirteenSalts` and
  `VisualCatalogContractTests.PresentationSalts_RegistryHasThirteenEntries`
  counted thirteen salts. `DeathFallJitterSalt` is the fourteenth, so both now
  assert fourteen, and a new test pins the registry's value against the literal
  `CollapsePose` actually mixes.
- `PawnQuadCountTests.Count_AddsExactlyTwoForTheDeadMark` asserted the dead mark
  at High tier. The mark is now Low-tier only, so that test became
  `Count_AddsExactlyTwoForTheDeadMarkAtLowTier`, joined by
  `Count_AddsNothingForADeadPawnAboveLowTier` for the other side of the rule and
  by `Count_IsUnchangedByTheCollapseRotation`, which pins that a rotated quad is
  still one quad.

New test files: `PawnTransformTests`, `CollapsePoseTests`,
`DeathCollapseSystemTests`, `ProneEnvelopeTests`, plus three additions to
`PresentationCoordinatorTests` covering the store's advance gating, its clearing
on reset, and that a living roster registers no bodies.

`./scripts/format.ps1 -Verify` printed `Formatted 0 of 770 files.` and
`[PASS] Formatting verification completed.`

**No smoke row was flipped.** The ten `DC` rows are in
`docs/development/smoke-checklist.md` as `PENDING`. Nobody has watched a body
fall over.

### One thing a reader should know about this run

An unrelated `Sandata.Client.exe` from another session held its `Debug` output
DLLs open throughout, which made `dotnet build Hukbo.slnx --no-incremental` fail
on file copies (MSB3027) rather than on compilation. That process was left alone.
The gate builds `Release`, writes to a different output path, and was unaffected;
the copy failures never touched a Hukbo project.

## How this closed, 2026-08-14

The work was merged to `main` in the feature commit `0d4b34e`, "feat: collapse a
slain warrior onto the ground instead of removing it", which carries all twelve
source files and all eight test files this plan names — 2,525 insertions across
twenty files. It reached `main` through the merge `7e6dc2d`. An earlier session
note that recorded this work as landing uncommitted was stale; it is committed.

Before archiving, each task was re-read against the shipped code rather than
against the plan's own claim of completion. Eight of the nine are complete as
written. The findings worth keeping:

- **DC-3 inlines its salt rather than referencing it.** `CollapsePose`
  hardcodes `0x7F2B95E0C4A16D38UL` instead of reading
  `PresentationSalts.DeathFallJitterSalt`. This is deliberate and matches the
  established convention — `PawnAppearanceFactory` inlines its own three salts
  the same way, and the registry exists so the pairwise-distinctness test can
  see every salt beside every other, not so consumers take a dependency on it.
  DC-3 as written only required the salt be distinct and listed in `All`, and
  both hold.
- **The salt-pinning test is weaker than it looks.**
  `DeathFallJitterSaltMatchesTheCollapsePoseValue` asserts that the registry
  constant equals its own literal, which cannot fail if `CollapsePose`'s inlined
  copy drifts away from it. The test does not compare the two. This is a small
  standing gap, not a defect in shipped behaviour, and it is recorded here
  rather than fixed because fixing it is a change to a test that is currently
  green for the wrong reason and deserves its own scoped work.
- **DC-5 did not thread the collapse through `CompletePosedLayout`.** The plan
  asked for the optional rotation on `Create`, `CompletePosedLayout`, and
  `CompleteAttackPosedLayout`; it shipped on the first and third. This has no
  observable effect. `CompletePosedLayout` is reached only through
  `CreateWithPoseBlindBounds`, which has no production caller at all — the live
  renderer draws through `PawnGeometry.Create` and `CompleteAttackPosedLayout`,
  both of which carry the parameter. No corpse is ever drawn through the
  untouched path, and DC-5's own stated verification criterion, that every
  existing construction path still returns `PawnTransform.Identity`, is
  satisfied exactly by its not threading. The parameter was deliberately not
  added during archiving: an unused parameter on a dead path is worse than the
  gap it closes.

Naming drifted from this document in two places, both harmless. The plan writes
`TryGetPose`; the code ships `TryGetCollapse`. The plan writes
`ResolveFinalRotation(fallSign, entityId)`; the code ships
`ResolveFinalRotation(fallsRight, entityId)`.

Everything in section 2, "What this is not", remains deliberately out of scope
and unbuilt. Corpse decay, pooling blood, a dropped weapon, a distinct read for
a fallen leader, corpse stacking, and any change to how the simulation decides a
warrior is dead each still need their own design before anyone builds them.
