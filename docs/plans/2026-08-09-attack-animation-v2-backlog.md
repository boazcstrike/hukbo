# Attack animation V2 — backlog after the twelve-task plan

Date: 2026-08-09
Status: open. The twelve-task attack animation V2 plan is complete and merged,
and has been archived out of `docs/plans/`; this records what it left behind.

The implementation plan and its design are the context for everything below.
Read the design titled "Attack animation V2 — design" first — it is
authoritative over this document, and where the two disagree the design wins
and the discrepancy is worth reporting. That design shipped and was archived on
2026-08-15, so it is a record rather than an instruction; this backlog is the
live half of the pair.

**Audited against the code on disk on 2026-08-15.** Two engineering items
survive that audit — section 2, the unwired conservative cull, and section 3,
the collapsed contact that loses its whole bundle — plus one naming item,
section 5's fifth bullet about `SwingPose`, `SwingPhase`, and `SwingTrail`.
Everything else that this document describes as outstanding has since been
fixed, and each such item now carries the evidence for that in place rather than
having been deleted. Read the note attached to an item before acting on the item
itself.

## Status after the 2026-08-13 smoke run

**Section 6 of this document is spent, and section 2's premise is now contested.
Read this before acting on either.**

On 2026-08-13 a person at an interactive Windows desktop ran the whole of the
attack animation V2 family. **Every one of the twenty-four `AA` rows has now
passed**, and the family's section was deleted from
`docs/development/smoke-checklist.md` outright under that file's rule that a
wholly passing family is a record rather than a checklist. The record is the
2026-08-13 archive titled "Attack animation V2 smoke — closed 2026-08-13".

**Three of those rows closed without anything being fixed**, and that is the
single most important thing on this page for a later reader. `AA-22` passed
after failing, on an unchanged 500-agent density. `AA-23` passed after failing,
with both of its measured causes intact. `AA-24` passed against a feature that
was never built. None of the three is evidence that the item this backlog
describes was addressed.

What that run does to this document, section by section:

- **Section 6, "Smoke rows still unobserved", is spent.** It names AA-5, AA-11,
  AA-12, AA-13, AA-14, AA-15, AA-16, AA-18, AA-19, and AA-23. Nine of those ten
  passed, and `AA-23` passed on a second attempt later the same day. Section
  6's framing of these as "never toggled" or "unreachable on the shipped V4
  roster" is history, not a live task list. `AA-23`'s first attempt did fail,
  and the two causes measured against it are written up in
  the strike-while-moving legibility design: at the
  default camera fit a pawn resolves `PawnDetailTier.Low` and has no legs at
  all, and a closing attacker under the arrival taper advances its stride phase
  one cycle per 300 seconds. Neither was fixed before the row passed.
- **Section 1, "The readability failure (AA-22)", no longer has a failing row,
  and its hypothesis was borne out.** Section 1's own recommended next
  measurement was to watch a **200-agent** battle rather than the 500-agent
  default, on the grounds that nobody knew whether the chaos was the feature or
  the density. That measurement has now been made: `AA-20` and `AA-21`, the two
  200-warrior rows it says had never been run for that reason, were run on
  2026-08-13 and both passed — and `AA-22` itself then passed on a later
  attempt, with nothing about the density, the trail count, or the arm gating
  changed in between. The two contributors section 1 identifies, arms close to
  sub-pixel at fit zoom and trails multiplying at density, are still real and
  still undressed; what has changed is that no smoke row is waiting on them.
- **Section 2, "The conservative cull is not wired, so AA-24 has no
  implementation", disagrees with the checklist and must not be resolved by
  assumption.** `ConservativePawnCull` still has no production caller — the only
  references to it under `src/` are its own doc comments plus three comments in
  `src/Hukbo.Client/Rendering/PawnGeometry.cs`, at lines 925, 2243, and 2348 —
  so nothing this section describes has been built. A person nevertheless passed
  `AA-24` at the desktop on the same day. Both records are true as written: the
  row asks whether a weapon pops in or out at the panel edge, and the live
  pose-blind path may simply be wide enough in practice for what the tester
  watched. Do not read the passing row as evidence that the cull was wired, and
  do not read this section as grounds to reopen the row — the row is closed and
  its family is gone from the checklist. Section 2 remains an accurate
  description of the code and is the reason this document is still open.

Nothing above authorizes a change. It records which parts of this document a
later reader may still act on and which parts are now a record.

## What shipped

The contact-latched attack rig: an exhaustive weapon-motion catalog, a bounded
contact dispatcher, target-local geometry, articulated arms, per-outcome
defender reactions, shield overlays for legal paired loadouts, a motion-
intensity policy, and the retirement of the legacy swing systems.
`src/Hukbo.Core` is byte-for-byte untouched and both preset baselines reproduce
their pinned hashes.

Two interactive runs on 2026-08-09 confirmed six smoke rows, failed one, and
left the rest `PENDING`. The results are in `docs/development/testing.md` under
"Attack animation V2 smoke".

## 1. The readability failure (AA-22)

**This is the largest open item, and it is a design question rather than a
defect.**

A spectator watching a 500-agent battle reported that the animations overlap
and the battle reads as chaos — that they could not tell what was happening.
Individual choreography is correct and the four weapon families are
distinguishable; it is the composition at density that fails. The design's goal
was "make close- and medium-zoom attacks look excellent while keeping a battle
readable", and only the first half is met.

Two known contributors:

- **Arms are barely visible.** The observer reported them as present but "not
  significantly seen". They are gated off below roughly 1.35 zoom and drawn as
  strokes 1.6 units thick. At the zoom where a 500-agent battle fits on the
  panel they are close to sub-pixel — cost without payoff.
- **Trails multiply.** At 500 agents a few dozen fading arcs are on screen at
  once. Each is correct alone. Together they are exactly the "continuous noise"
  the design's section 10 warned about.

**The cheapest next measurement, and it should come before any tuning:** run a
**200-agent** battle. Both interactive runs used the shipped 500-agent default
scenario, which is the stress case, not the case the readability target was
written for. Until 200 is observed, nobody knows whether the chaos is the
feature or the density. `scripts/run.ps1` has no agent-count flag today, so this
needs either a small flag on that script or a scenario override.

Rows AA-20 and AA-21 (200 warriors, close zoom and default fit) have never been
run for the same reason.

## 2. The conservative cull is not wired, so AA-24 has no implementation

`ConservativePawnCull` has **no production caller**. Its own header says so:
GPU-016, the task that would have adopted it, was dropped on 2026-08-07. The
live pawn cull is `PawnGeometry.PoseBlindPrefix.PoseBlindVisualBounds`, built
with `default(SwingPose)` and an explicit empty arm rectangle.

Task 7 widened `ConservativePawnCull`'s radius from 27.2 to 38.8 units per unit
of apparent scale and proved by brute force that the new radius contains every
posed pawn, every heading, every resolution, and the largest defender reaction
lean. That proof is real and worth keeping, but it changes **nothing that is
drawn**. A warrior striking at the edge of the arena panel can still have its
weapon clipped, which is what smoke row AA-24 asks about.

Closing AA-24 means widening the **live** pose-blind path. That is a genuine
decision, not a mechanical change: `PawnGeometry`'s own remarks argue against a
pose-aware cull, because it would make the drawn set a function of presentation
animation phase. Decide the approach before writing code.

**Wiring `ConservativePawnCull` in cannot close AA-24, and this is not a gap
in the wiring — it is what the type's own header now says explicitly.** The
bound is a genuine superset of the live pose-blind test, never a replacement:
a caller that keeps today's exact test afterward, which any correct caller
must, draws exactly the same set of pawns the game draws now. So wiring it in
is a performance change at best and changes nothing about which weapon pops in
or out at the panel edge, which is what AA-24 asks about. Do not read "wire
it" as the outstanding task here. The type is being kept as a mirrored-
constants guard — `ConservativePawnCullTests` is what keeps its duplicated
`PawnGeometry` constants from drifting silently — and the decision of whether
to wire it into the live pawn loop at all has been handed to the thousand-unit
performance workstream, which weighs it as one candidate among several rather
than as a standalone fix for this row.

**Still open, confirmed on disk on 2026-08-15.** `ConservativePawnCull` is
declared at `src/Hukbo.Client/Rendering/ConservativePawnCull.cs:66` and still has
no production caller. The only references to it outside the test project are the
three comments in `src/Hukbo.Client/Rendering/PawnGeometry.cs`, at lines 925,
2243, and 2348, and every call site is in
`tests/Hukbo.Client.Tests/ConservativePawnCullTests.cs`. This section is an
accurate description of the code today.

## 3. A collapsed contact silently loses its whole bundle

When one attacker exceeds `MaximumPendingContactsPerAttacker` (five),
`AttackContactDispatcher.Add` calls `ReplacePending`, which overwrites the
newest pending bundle and writes one diagnostic line. Since tasks 5 and 6
narrowed every contact channel to bundle-driven, the discarded contact now
produces **no weapon cue, no death cue, no blood, no clash, and no defender
reaction** — where the older event route would still have fired each of them
independently.

Reachable during a 4x catch-up burst, which is the case the buffer exists for.

**It has never actually fired.** Two full 500-agent battles, including three
pause and resume cycles and a round transition, produced no
`render.attack.contact.collapsed` line at any level. So the capacity looks
correctly sized, and this is a latent path rather than an observed loss. Worth
either proving unreachable or making the loss visible.

**Still open, confirmed on disk on 2026-08-15.** `AttackContactDispatcher.Add`
still calls `ReplacePending` at
`src/Hukbo.Client/Presentation/AttackContactDispatcher.cs:237-239`, and
`ReplacePending` at `:277-284` still overwrites the stored bundle and does
nothing beyond calling `ReportCollapsed`. The discarded bundle is still lost
whole.

## 4. `AcknowledgeDraw` acknowledges contacts that were never drawn

`AttackFrameCoordinator.AcknowledgeDraw` documents itself as releasing "every
latch whose matching pose was present in the completed pawn pass", but it checks
only `AwaitingDrawAcknowledgement` and sequence equality — never whether the
pawn survived the cull. `ArenaGame` calls it unconditionally after `DrawPawns`.

So an attacker rejected by the cull, or one that is dead and outside its lethal
hold, has its contact frame acknowledged and its timeline advanced without ever
having been drawn. The "guaranteed contact draw" the entire latch mechanism
exists to provide does not hold in those cases.

**Fixed, confirmed on disk on 2026-08-15.** `AcknowledgeDraw` now consults
whether the pose was actually drawn: the release is gated on `WasDrawnThisFrame`
at `src/Hukbo.Client/Presentation/AttackFrameCoordinator.cs:210-216`, backed by
`RecordDrawn` at `:165-179` and by the `MaximumLatchFrames` bound at `:44-45`
that stops a never-drawn latch from being held forever. The draw path records
the pose through `src/Hukbo.Client/ArenaGame.Rendering.cs:1402`, by way of
`src/Hukbo.Client/Presentation/PresentationCoordinator.cs:363`. The section above
describes the old behaviour and is kept as the record of why the gate exists.

## 5. Smaller items

- **`RenderAttackContactCollapsed` logs at `warn`, once per collapsed contact.**
  The condition is bursty by construction, so when it fires at all it fires many
  times per frame. `CLAUDE.md`'s logging standard puts anything firing more than
  once a second at `dbg` or below.
  **Fixed, confirmed on disk on 2026-08-15:** the call site at
  `src/Hukbo.Client/Presentation/AttackContactDispatcher.cs:290` passes
  `LogLevel.Debug`.
- **`ReleaseForDraw` rebuilds its whole agent dictionary every frame** before it
  ever consults `Dispatcher.PendingCount` — one insert per agent, on frames that
  latch nothing. An early return when the pending count is zero is safe.
  **Fixed, confirmed on disk on 2026-08-15:** the early return is at
  `src/Hukbo.Client/Presentation/AttackFrameCoordinator.cs:106-109`, ahead of the
  dictionary rebuild.
- **Six `AttackPose` fields have no reader anywhere in `src/`:** `Forward`,
  `Right`, `SupportHand`, `ShieldHand`, `TrailStart`, `TrailEnd`. The support
  hand is re-derived from the weapon line in `PawnGeometry`, and the shield
  guard uses a fixed offset by design. Either consume them or drop them.
  **Fixed, confirmed on disk on 2026-08-15:** they were dropped.
  `src/Hukbo.Client/Rendering/AttackPoseResolver.cs:11-22` declares none of the
  six; the shape now carries `HasSupportHand`, `HasShield`, and `TrailStrength`.
- **`RecordPawnQuads` passes `gaitPose: null` while `DrawPawns` passes the real
  pose**, under a comment claiming the probe pass mirrors the draw path element
  for element. Pre-existing, but the two call sites now make it explicit.
  **Fixed, confirmed on disk on 2026-08-15:** `RecordPawnQuads` passes the real
  gait pose at `src/Hukbo.Client/ArenaGame.Rendering.cs:535-543`, and the parity
  of the two call sites is pinned by
  `tests/Hukbo.Client.Tests/PawnGaitQuadParityTests.cs:10-14`.
- **`SwingPose`, `SwingPhase` and `SwingTrail` keep their old names.** The
  legacy swing *systems* are deleted and only one attack path remains, so this is
  a naming cleanup rather than a second path.
  **Still open, confirmed on disk on 2026-08-15, and this item's original
  location claim was wrong.** The three types are not together in one file:
  `SwingPhase` is at `src/Hukbo.Client/Rendering/WeaponLinePose.cs:16` and
  `SwingPose` at `:56`, while `SwingTrail` is at
  `src/Hukbo.Client/Rendering/PawnGeometry.cs:44`. A rename has to touch both
  files.
- **`tools/Hukbo.Tools.RenderProbe/packages.lock.json` gained eight lines** — a
  stale `Hukbo.Shared.Core` project-reference entry that a build regenerated. No
  new external package, but it rode in on this branch unreviewed.

## 6. Smoke rows still unobserved

- **AA-5** (1x, 2x, 4x), **AA-11** (combo), **AA-12** (lethal contact),
  **AA-18** (pause during a catch-up burst), **AA-23** (striking while moving).
- **AA-16** (Motion Reduced and Off). The logs prove this was never toggled:
  `settings.changed` exists as an event and never appears, and `settings.loaded`
  reads `motion: Full` in both runs.
- **AA-19** is half done. Next Round was exercised and the second battle ran
  clean; Full Reset was never triggered.
- **AA-13 and AA-14** (shielded Kalis and Itak) are unreachable on the shipped
  V4 roster, which fields all four weapons solo. They need a registered V2
  replay.
- **AA-15** needs the three detail tiers compared against each other.

There is no zoom or camera event in `LogEvents`, so a debug log can never show
what zoom a run reached. Any row about detail tiers or apparent size has to be
answered by a person.

## 7. Verification notes for whoever picks this up

- The plan's Task 12 prescribes `10197eb..HEAD` as the review diff base.
  `10197eb` is **not an ancestor** of the branch; that range compares divergent
  lineages and reports a clean `src/Hukbo.Core` for the wrong reason. Use
  `0ac8fe0..HEAD`, or `main...HEAD` once main is merged in.
- The render probe measures a **paused** battle unless playback is started.
  `PlaybackController.IsPlaying` defaults to false, and before task 10 every
  station recorded zero attack poses and every agent alive.
  `ArenaGame.SetProbePlaybackStarted()` exists behind the `HUKBO_RENDER_PROBE`
  opt-in for exactly this reason. Check `activeAttackPosesMaximum` in any probe
  report before trusting its numbers as an attack budget.

## 8. Two of section 5's smaller items are fixed, 2026-08-14

`RenderAttackContactCollapsed` now logs at `dbg`. The condition is bursty by
construction, and the logging standard in `CLAUDE.md` section 5 puts anything
firing more than once a second at `dbg` or below. The line carried no `msg`
field, so nothing had to be removed when the level dropped — the payload is
still `attackerId`, `collapsedCount`, `sequence`, and `tick`.

`AttackContactDispatcherTests.Ingest_RetainsFivePerAttackerAndCoalescesTheSixthWholeBundle`
observed the line through a log configured at `Warning`, so it went red the
moment the level moved. Its threshold is now `Debug` and it additionally asserts
that the emitted `lvl` is `dbg`. Every other assertion in it is unchanged. The
test now pins the level deliberately rather than observing it by accident, which
is the difference between a test that survived the change and one that was made
to survive it.

`ReleaseForDraw` returns early when nothing is pending. It rebuilt its whole
agent dictionary before it ever consulted the pending count, one insert per
agent on frames that latch nothing. The returned span is unchanged, and the
reason is checkable rather than assumed: `AttackContactDispatcher.TryLatchNext`
already returns false immediately at a zero pending count, so the release loop
never ran and the span was already empty. The dictionary is read only through
`TryGetAgent`, whose single caller reads it only while iterating that span, so
an unrebuilt dictionary is never observed. A regression test covers the
zero-pending frame and proves that a later real latch still resolves its agent.

The other four items in section 5 are untouched and still open, as are sections
2, 3, and 4. The two fixed here were the two that needed no decision. Items 3
and 4 both do: whether a collapsed bundle should be made visible or proved
unreachable, and whether `AcknowledgeDraw` should consult cull survival. The six
unread `AttackPose` fields and the `RecordPawnQuads` `gaitPose` mismatch were
left alone for the same reason — dropping a field and changing what the probe
pass records are both choices, not cleanups.

**The paragraph above was true on 2026-08-14 and is no longer true.** The
2026-08-15 audit found that section 4, the six unread `AttackPose` fields, and
the `RecordPawnQuads` `gaitPose` mismatch have all since been resolved; the
evidence is recorded against each of them where they appear above. What remains
open is section 2, section 3, and the naming of `SwingPose`, `SwingPhase`, and
`SwingTrail`.
