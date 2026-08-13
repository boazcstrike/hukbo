# Auto camera centring — design

**Date:** 2026-08-12
**Scope:** `src/Hukbo.Client` only. Presentation. Nothing here reaches a state
hash, an event, or a snapshot.

## 1. What was observed

The auto camera modes smoke family was run by a person at an interactive
desktop on 2026-08-12. All seven of its rows passed, including row 149, whose
own criterion is that the camera stays put through a small skirmish rather than
lurching toward the main battle between blows.

The tester reported a separate defect while passing that row:

> yes, this is good; but usually it is not centered; and fighting usually stays
> at the corner of the screens; we need to fix to center, not when a battle
> happens, only when panning from an empty on-fight screen.

Two distinct statements are packed into that. The first is the defect: when the
assistant does travel, it stops with the fight at the edge of the screen rather
than under the camera. The second is a constraint on the fix: the camera must
not start re-centring a fight that the spectator can already see. Only a pan
that began from a screen with no fighting on it is allowed to end centred.

## 2. Cause

`ArenaAutoPan.SettleFraction`, at `0.7`, is doing two unrelated jobs.

As Follow mode's `OnScreenFraction` it is correct. It is the band inside which
a fight counts as already visible, and setting it below one is what makes
Follow re-engage on a melee drifting toward an edge. Smoke row 154, which is
about exactly that behaviour, passed.

As `ArenaAutoPanController.ContinuePan`'s settle gate it is wrong. A pan ends
the moment any fighting agent falls inside seventy per cent of the visible
half-extents — which, measured from the centre, is most of the way to a corner.
The pan therefore terminates as soon as the melee crosses into frame, wherever
that happens to be, and the camera never closes the remaining distance. That is
precisely what the tester saw.

The two jobs happen to have wanted the same number when the constant was
written, and nothing since then has separated them.

## 3. Change

Split the constant.

- `SettleFraction` is renamed `FollowOnScreenFraction`. Its value, `0.7`, does
  not change, and its only remaining caller is `GetTuning`'s Follow arm. Its
  documentation now says outright that it has no part in ending a pan, so the
  next reader does not re-conflate the two.
- A new `CenteredFraction`, at `0.2`, is the band a pan has to bring the melee
  inside before the pan is finished. `ContinuePan` reads that instead.

Nothing else moves. The pan-start gate, the idle grace, the dwell, the retarget
interval, `IsWorthTravelling`, and the `MaximumPanSeconds` ceiling are all
untouched.

## 4. Why the constraint in section 1 is satisfied by construction

A pan can only begin when `canSeeFighting` has been false for the whole idle
grace and the candidate clears `IsWorthTravelling`. That gate is not being
changed, so a fight the spectator can already see still never starts a pan, and
the camera still never nudges itself while a battle is on screen. The only
thing this change touches is where an already-running pan is allowed to stop.

## 5. Why `0.2` rather than `0`

Arrival is asymptotic — `AdvanceCenter` eases out on the remaining distance —
and a live melee drifts while the camera travels. A settle gate demanding the
exact centre would rarely be met, so every pan would run to the six-second
`MaximumPanSeconds` ceiling and settle wherever it happened to be, which is the
present defect with extra motion in front of it. A fifth of the way to an edge
reads as centred and is reachable within a frame or two of the ease-out.

The number is a presentation tuning value, not a measurement, and is labelled
that way in its doc comment.

## 6. The nine questions

1. **User-visible outcome.** When the camera assistant travels to a fight the
   spectator could not see, the fight ends up near the middle of the screen
   instead of at an edge or a corner.
2. **Tick stage and state read/written.** None. `ArenaAutoPanController` runs on
   unscaled frame time, reads completed-tick `AgentView` values, and writes only
   the camera centre.
3. **Numeric units, bounds, same-tick conflict rule.** `CenteredFraction` is a
   dimensionless fraction of a visible half-extent, in `(0, 1)`, at `0.2`. There
   is no same-tick conflict: one controller owns the camera centre and manual
   pan input already pre-empts it through `ManualOverrideSeconds`.
4. **Total ordering and random-stream policy.** Unchanged. `TryResolveTarget`
   still breaks anchor ties on the lower `EntityId`, and no random stream is
   involved anywhere in this file.
5. **Cache.** No cache. Every value is recomputed per frame from the agent list.
6. **Save, event, version effect.** Presentation only. No preset version, no
   event, no snapshot field, and no settings-schema change — `AutoCameraMode`
   already persists and its three values are untouched.
7. **Worst-case complexity and benchmark workload.** Unchanged: `ContinuePan`
   already called `HasFighterInside` once per frame and still does, at O(agents).
   The pan may now run for more frames before settling, bounded above by the
   existing `MaximumPanSeconds` ceiling.
8. **Spectator explanation.** The effect is the explanation — a spectator sees
   the camera arrive with the fight under it. The `AUTO CAMERA` selector in the
   menu already exposes the three modes, and turning the assistant `Off` still
   stops all of it.
9. **Tests that fail before and pass after.**
   `Controller_EndsAPanWithTheFightNearTheCentreNotTheEdge`. Run against the old
   `0.7` band it fails with `pan stopped 13.782501 from the fight, outside the 5
   centred band` — the pan settled 13.78 world units from the melee on a
   20-unit half-extent, which is sixty-nine per cent of the way to the edge and
   is the corner the tester described. Against `0.2` it passes.

   The first version of that test did not discriminate. It asserted against
   `HalfExtents.X * ArenaAutoPan.CenteredFraction`, so the threshold moved with
   the constant the test exists to guard, and it passed at `0.7` as readily as
   at `0.2`. The band is now a literal `0.25f` declared inside the test, with a
   remark saying why, because a test that derives its expectation from the
   value under test is comparing the code with itself.

## 7. What this design does not authorise

It does not authorise a zoom change, a camera lead or look-ahead, a smoothing
curve change, or any re-centring of a fight that is already on screen. Each of
those is a separate proposal.
