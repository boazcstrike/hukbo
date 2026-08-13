# Projectile prop scale: plan — closed 2026-08-13

**Archived: reference only.** This is a finished plan. Every task in it was
built, verified, and merged. Never execute it, never treat it as a live task
list, and never cite it as the reason to make a change. The live contract for
this project remains `CLAUDE.md` and `docs/development/smoke-checklist.md`. One
manual smoke row, `PP-3`, is still open against the shipped change, and it is
tracked in the smoke checklist rather than here.

Date: 2026-08-13

This plan changes one expression in one file. It is written out at length anyway,
because the reason that expression is wrong is invisible from the line itself,
and because correcting it changes what a spectator sees at every camera zoom
above `2.40`.

Everything here lives in `Hukbo.Client` and its test project. Nothing in this
plan may touch `Hukbo.Core`, `Hukbo.Shared.Core`, or either headless runner, and
nothing here can move a state hash or an event hash.

## 1. The defect, in spectator terms

A Bangkaw in flight is a thrown spear, and at the default camera it reads as one:
it draws a little over half the height of the warrior who threw it. Zoom in and
it stops being a spear. At the tightest camera the spectator can reach, the same
shot draws roughly three and a half times a warrior's height — a fence rail
gliding over the battlefield, far longer than the men are tall. A Busog arrow at
that zoom draws about twice a warrior's height. The effect is worst exactly where
a spectator has zoomed in to watch one duel closely, which is the moment the
projectile is meant to be smallest relative to the bodies around it.

The cause is a mismatch between two scaling rules that were never compared. A
pawn does not scale by the camera zoom directly; it scales by a clamped and
saturating function of it. An in-flight projectile prop scales by the raw zoom.
Below zoom `2.40` the two rules happen to agree closely enough that nobody
noticed. Above it the pawn stops growing and the prop does not.

## 2. Evidence

Three lines carry the whole defect.

- `src/Hukbo.Client/Rendering/ProjectileGeometry.cs:183` reads
  `var scale = MathF.Max(cameraZoom, 0f);` — the raw camera zoom, floored only at
  zero, used directly as the multiplier for every drawn dimension of an in-flight
  prop.
- `src/Hukbo.Client/ArenaGame.Rendering.cs:820-824` is the only call site, and it
  passes `_camera.Zoom` straight through.
- `src/Hukbo.Client/Rendering/PawnGeometry.cs:236-240` is what every warrior
  scales by instead: `ResolveApparentScale` returns
  `Math.Clamp(cameraZoom * 1.35f, 0.72f, 2.40f)`, whose constants sit at
  `PawnGeometry.cs:221-223`. It saturates at `2.40`, so a pawn stops growing once
  the camera passes a zoom of about `1.78`.

The camera's own range is `0.05f` to `12f`
(`src/Hukbo.Client/SpectatorCamera.cs:9-10`), so a spectator can reach a zoom
nearly seven times higher than the point at which pawns stop growing.

The figures below are drawn-pixel arithmetic, computed from the shipped constants
rather than eyeballed on screen, and reproducible by hand: the Bangkaw prop's
total drawn length is the shaft plus the head, `13f + 3.5f = 16.5` units
(`ProjectileGeometry.cs:92-102`), multiplied by the scale in force; the pawn's
drawn height is its roughly 23 layout units of body multiplied by
`ResolveApparentScale`. Every one of these proportions is a **Provisional
reconstruction** — see section 7.

| Camera zoom | Prop scale in force | Pawn drawn height | Bangkaw prop drawn length | Prop as a fraction of the pawn |
| --- | --- | --- | --- | --- |
| 1 | 1.00 | 31 px | 16.5 px | 0.53x |
| 1.78 | 1.78 | 56 px | 29.4 px | 0.52x |
| 4 | 4.00 | 56 px | 66 px | 1.18x |
| 12 | 12.00 | 56 px | 198 px | 3.54x |

The pawn column stops at 56 px because `ResolveApparentScale` has saturated. The
prop column does not stop at all. The first two rows are the shape the prop is
supposed to hold — roughly half a warrior — and the last two are the defect.

A second symptom falls out of the same mismatch. When a shafted projectile lands
it stops being an in-flight prop and becomes an embedded one, and the embedded
path scales by the host's own apparent scale instead
(`ProjectileGeometry.cs:281`). At zoom 12 a Bangkaw therefore goes from 198 px in
the air to about 22 px standing in the wound — it loses 89% of its length in a
single frame. That pop is not a separate defect; it is the same one seen from the
other side.

Nothing in the current test suite relates the prop's drawn size to a pawn's drawn
size. `tests/Hukbo.Client.Tests/ProjectileGeometryTests.cs` pins centring,
distinctness, rotation, quad count, the low-zoom visibility floor, and monotonic
growth with zoom — but every one of those assertions is satisfied by a prop of
any size at all. The defect was never in reach of the suite.

## 3. Options considered

**Rejected — scale the prop by `PawnGeometry.ResolveApparentScale(cameraZoom)`
outright.** This is the obvious symmetry: make the prop obey exactly the rule the
pawns obey. It fixes the high-zoom end perfectly, and it fails at the other end
badly enough to trade one visible defect for another. `ResolveApparentScale`
clamps upward as well as downward, at a floor of `0.72`. At the camera's minimum
zoom of `0.05` a Bangkaw prop currently draws about 2 px, floored by
`MinimumDimension`; under the pawn rule it would draw about 11.9 px, a growth of
5.9x, against a pawn that is itself only about 13 px tall at that zoom. Every
shot in the air would read as nearly warrior-tall in precisely the view where 500
warriors are on screen at once. That view is what smoke rows PP-1 and PP-2 are
watched in, and the change would put both at risk to fix a problem neither row
covers. Rejected.

**Chosen — take the smaller of the two rules.** Replace the raw scale with
`MathF.Min(cameraZoom, PawnGeometry.ResolveApparentScale(cameraZoom))`. Below the
saturation point the raw zoom is already the smaller of the two, so the prop
keeps its existing behaviour exactly, including its continued shrinking toward
the 1 px floor at a pulled-out camera. Above a zoom of `2.40` the pawn's ceiling
becomes the smaller value and the prop stops growing with the warriors, which is
the entire fix. It is deliberately written as a call to `ResolveApparentScale`
rather than as the literal `2.40f` it currently evaluates to, so that if
`MaximumApparentScale` is ever retuned the prop's ceiling moves with the pawns
instead of silently drifting away from them again.

| Camera zoom | Prop scale after the change | Pawn drawn height | Bangkaw prop drawn length | Prop as a fraction of the pawn |
| --- | --- | --- | --- | --- |
| 0.05 | 0.05 | 13 px | 2 px (floored) | unchanged |
| 1 | 1.00 | 31 px | 16.5 px | 0.53x |
| 1.78 | 1.78 | 56 px | 29.4 px | 0.52x |
| 4 | 2.40 | 56 px | 39.6 px | 0.71x |
| 12 | 2.40 | 56 px | 39.6 px | 0.71x |

The three silhouettes stay mutually distinct at the cap, which is what PP-2 asks
for: spear 39.6 px, arrow 22.1 px, ball 5.3 px. The embedded pop shrinks with
it — a Bangkaw at zoom 12 now goes from 39.6 px in the air to about 22 px in the
wound, a loss of 45% rather than 89%.

## 4. Tasks

| Task | What | Files | Done when | Depends on | Verified by |
| --- | --- | --- | --- | --- | --- |
| PS-1 | Replace the raw-zoom scale in `Create` with the smaller of the raw zoom and the pawn's own apparent scale: `MathF.Min(cameraZoom, PawnGeometry.ResolveApparentScale(cameraZoom))`. One line changes. The embedded path keeps its own `layout.ApparentScale * EmbeddedLengthFraction` scale exactly as it is, and none of the base unit constants move. | `src/Hukbo.Client/Rendering/ProjectileGeometry.cs` (line 183) | The solution builds under `TreatWarningsAsErrors` with no new using directive, both types already living in `Hukbo.Client.Rendering`. The dropped `MathF.Max(cameraZoom, 0f)` floor leaves nothing unguarded: `Scaled` already floors every drawn dimension at `MinimumDimension`, and the camera cannot produce a zoom below `0.05f`. | — | `./scripts/build.ps1`. `Create_KeepsAProjectileVisibleAtAPulledOutCamera` (line 152) stays green untouched, because at zooms `0.01` and `0.2` the minimum still picks the raw zoom. `Create_ScalesWithTheCamera` (line 170) stays green untouched, because `2.40` at zoom 3 still exceeds `1.0` at zoom 1. The five zoom-1 tests at lines 24, 36, 71, 100, and 136 are arithmetically unaffected and must not be edited. |
| PS-2 | Add `Create_NeverOutgrowsThePawnAtHighZoom`, a `[Theory]` over camera zooms `4f` and `12f`, closing the coverage gap named in section 2. Build the denominator from the pawn's own layout at the same zoom, take the pawn's drawn height from it, and assert that a Bangkaw prop's total drawn length — `Primary.Length + Secondary.Length` — stays strictly below `0.9` of it. Print both pixel figures in the assertion message. The most generous available pawn bound is the correct denominator on purpose: a looser bound means the test cannot go red for a reason other than an oversized prop. | `tests/Hukbo.Client.Tests/ProjectileGeometryTests.cs` | The test passes at both zooms against PS-1's expression, and is confirmed to fail against the old one by restoring `MathF.Max(cameraZoom, 0f)` in the working copy, running the single test, and putting the new expression back before anything is committed. A test that has never been seen red proves nothing. | PS-1 | `./scripts/test.ps1 -Configuration Release`. The client suite gains exactly two test cases and loses none; the recorded red run shows roughly 198 px against 56 px, and the green run roughly 39.6 px against 56 px. |
| PS-3 | Document why the cap exists, on the `cameraZoom` parameter of `Create` and beside the changed line. The comment must say three things: that an in-flight prop is capped at the pawn's own apparent-scale ceiling so a spear can never draw longer than the warriors it flies past; that the floor deliberately is *not* borrowed from `ResolveApparentScale`, because the `0.72` floor would inflate the prop at a pulled-out camera and endanger PP-1 and PP-2; and that the resulting proportion is presentation tuning under `CLAUDE.md` section 7, not a measurement. Name this plan in the comment. | `src/Hukbo.Client/Rendering/ProjectileGeometry.cs` | The comment survives a reader who knows nothing of this plan and is deciding whether the `Min` is a typo for `Max`. The existing `Provisional reconstruction` remarks block on the class stays as it is; this adds to it rather than replacing it. | PS-1 | Build green. Reviewed by reading, not by a test. |

PS-2 and PS-3 both depend on PS-1 and touch different files, so they can run at
the same time as each other. All three are small enough that one person should
carry all three rather than splitting them between sessions.

Edits to `docs/development/smoke-checklist.md`, to the archive records, and to
`docs/plans/README.md` are deliberately not tasks here. They are handled outside
this plan by whoever is holding the checklist.

## 5. Verification criteria

In this order, and none of it delegated past the person who owns the change.

1. `./scripts/test.ps1 -Configuration Release`. Both suites green. The client
   suite must gain exactly the two cases PS-2 adds. If any other
   `ProjectileGeometryTests` case moves, PS-1 changed more than one line and the
   diff should be re-read before going further.
2. `./scripts/verify.ps1`, run once, with its real output pasted into this
   document. This change is presentation only, so both digests must be
   byte-identical to the recorded seed-1 baseline in
   `docs/development/testing.md`. A moved hash means something reached
   `Hukbo.Core`, and the change is reverted rather than explained.
3. Smoke row PP-3 re-run. **A human at an interactive desktop, and no agent may
   mark it.** The row asks that a shot stay visible while zooming from close in
   to fully zoomed out; this change alters exactly that behaviour at the close-in
   end, so the previous observation no longer covers the current code. The tester
   confirms both halves: the shot is still drawn at the most pulled-out camera,
   and at the tightest zoom a spear no longer reads as longer than the warriors
   around it. PP-1 and PP-2 are unchanged by the arithmetic above but are cheap
   to re-watch in the same sitting.

A green gate says the prop is capped where the code says it is. It does not say
the capped prop looks right, and only step 3 can.

### Recorded result, 2026-08-13

Steps 1 and 2 are done. Step 3 is not, and the row stays open until a person
runs it.

`Create_NeverOutgrowsThePawnAtHighZoom` was seen red before it was seen green.
Against the old raw-zoom expression it failed at both zooms, reporting a Bangkaw
prop of 66 px against a 72 px pawn at zoom 4 and 198 px against the same 72 px
pawn at zoom 12. Against the capped expression both cases pass. The pawn figure
is taken from `VisualBounds`, which includes the weapon line and is therefore a
more generous denominator than the body-only height quoted in section 2.

The full client suite is green at 3,728 passed, 0 failed, and the two new cases
are the only ones added.

`./scripts/verify.ps1` was run once and reported every stage `[PASS]`:
prerequisites, locked restore, formatting, the Release build, the Release test
run, and all four headless determinism workloads at 200 agents, 10,000 ticks,
seed 1. The seed-1 workload reported `deterministic: true`,
`firstMismatchTick: null`, `Faction0Victory` with 18 survivors,
`stateHash 6225182B4A470F91`, and `eventHash C4DABE6AF98B6BEC` under combat
preset 5 and movement preset 11 — byte-identical to the baseline recorded
earlier in `docs/development/testing.md`, which is what a presentation-only
change is required to produce.

One caveat belongs on this record. The working tree this gate ran against also
carried unrelated uncommitted work from a concurrent session, so the run proves
the tree as a whole is green rather than isolating this change. The digests
above are the evidence that this change in particular touched no simulation
state.

### Closure re-verification, 2026-08-13

The change shipped as commit `c772849`, "fix: stop a projectile outgrowing the
warrior who threw it at high zoom", which is now an ancestor of `main`. A
separate audit of that commit against section 4 confirmed that it changes
exactly three things and nothing else: the `cameraZoom` parameter's doc comment,
the scale expression with its rationale comment block, and one added test
method. The base unit constants, the embedded path, `PawnGeometry` and its
clamp constants, the single production call site in `ArenaGame.Rendering.cs`,
and the seven pre-existing tests this plan named as untouchable are all
byte-identical to what they were before.

Both verification steps were then re-run independently on the integrated tree
rather than on the working copy the original record describes, which retires the
contaminated-tree caveat noted above:

- `./scripts/test.ps1 -Configuration Release` reported `Test Run Successful`
  with 3,770 of 3,770 tests passed, exit code 0. The client suite is larger than
  the 3,728 recorded above because unrelated work merged in the meantime; both
  cases of `Create_NeverOutgrowsThePawnAtHighZoom` are among the passes.
- `./scripts/verify.ps1` reported `[PASS] Canonical repository verification
  completed`, exit code 0. The seed-1 workload under combat preset 5 and
  movement preset 11 reported `deterministic: true`, `firstMismatchTick: null`,
  `Faction0Victory` with 18 survivors, `stateHash 6225182B4A470F91`, and
  `eventHash C4DABE6AF98B6BEC` — byte-identical to the baseline, which is what a
  presentation-only change is required to produce.

This plan is therefore archived under the rule stated in `docs/plans/README.md`:
a plan is archived when the build is finished, not when its smoke rows are.
`PP-3` remains open and remains a human's to run.

## 6. What this plan does not do

- It does not retune the base unit constants at `ProjectileGeometry.cs:92-102`.
  The 13-unit spear, 7-unit arrow, and 2.2-unit ball keep their deliberately
  well-separated lengths; only the multiplier applied to them changes.
- It does not touch the embedded path at `ProjectileGeometry.cs:281`. A landed
  shaft already scales by its host's own apparent scale, which is the correct
  rule and the one this change borrows from. The reduced in-flight-to-embedded
  pop is a consequence of PS-1, not a separate edit.
- It does not detail-gate the in-flight prop, now or ever. PP-3 requires the shot
  to stay drawn at every zoom, and the `MinimumDimension` floor that keeps it
  drawn is untouched.
- It does not change the camera's zoom range, `PawnGeometry`'s clamp, or any
  constant in `PawnGeometry`. The prop is being made to follow the pawns; the
  pawns are not being moved.

## 7. Provisional-reconstruction labelling

Every drawn proportion named in this document — the 13-unit spear shaft and its
3.5-unit head, the 7-unit arrow and 2.2-unit fletch, the 2.2-unit ball, the
`0.55` embedded length fraction, the pawn's `1.35` zoom scale and its `0.72` and
`2.40` clamp, the `0.9` bound PS-2 asserts, and every pixel figure and ratio
computed from them — is a **Provisional reconstruction** under `CLAUDE.md`
section 7. None of it is a measurement, and none of it may be presented as one.
No source records how long a spear in flight should draw relative to the warriors
around it. These numbers exist so that three ranged weapons stay tellable apart
at a glance and none of them outgrows a man, and that is their entire
justification. The historical claims that do carry evidence tiers — Bangkaw and
Busog and their zero-year-gap 1521 attestation — live in `PawnAppearance` and are
untouched by anything here.
