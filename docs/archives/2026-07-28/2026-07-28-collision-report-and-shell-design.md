# Collision firmness, the per-unit battle report, and the window shell — design

**Archived: reference only.** Implemented and verified on 2026-07-28; the
canonical gate passed with `stateHash A080E28DA7C79C20` and `eventHash
2B6FB3A9A9C1960D`. Do not execute this document and do not cite it as
justification for a change. Smoke rows 134 to 148 in
[docs/development/testing.md](../../development/testing.md) remain PENDING and
still need a human at an interactive desktop.

**Status:** Design complete. Per `CLAUDE.md` section 6 and
[`docs/plans/README.md`](README.md), a `-design.md` document does not authorize
implementation on its own. **However, the user explicitly asked in this session
for all four of these changes to be implemented**, and the plan document that
does authorize the work —
[`2026-07-28-collision-report-and-shell.md`](2026-07-28-collision-report-and-shell.md)
— follows immediately and is written against this design. Read the plan for the
ordered task list; read this document for why each decision is the one being
implemented.

**Date:** 2026-07-28.

**Branch:** `unit-collision-and-battle-report`.

**Scope:** four independent user requests bundled into one branch because they
share one verification pass. Exactly one of them touches `Hukbo.Core` and
therefore moves the recorded hashes; the other three are presentation-only.

| # | Request, in the user's words | Layer | Moves a hash? |
| --- | --- | --- | --- |
| 1 | "increase unit collission" | `Hukbo.Core` | **Yes** |
| 2 | "add more details on summary per unit, like a battle report (top unit kills, and many more for you to suggest and add)" | `Hukbo.Client` | No |
| 3 | "remove windows default exit, minimize, maximize" | `Hukbo.Client` | No |
| 4 | "bigger menu specifically for unit setup (text is overshooting)" | `Hukbo.Client` theme data | No |

**Not in scope, and deliberately so:** the separate collision *performance*
workstream described in
[`2026-07-28-collision-resolution-scaling-design.md`](2026-07-28-collision-resolution-scaling-design.md).
That document proposes indexing pending movers in a second uniform grid so the
collision stage stops scanning two linear lists. It is a hash-neutral traversal
change and it is design-only with no plan behind it. This document is about how
*firm* collision feels, not how *fast* it resolves. The two must not be
confused, and nothing here authorizes any part of that other design.

---

## 1. Goal 1 — collision firmness

### 1.1 The problem, stated precisely

The user asked to "increase unit collission". Read as a request to *add* missing
collision, that would describe a defect which does not exist: solid-disc
collision is already authoritative and already exact.
`CollisionPolicy.Solid` is the only approved policy,
`CollisionResolver` never commits a position that strictly overlaps an already
committed body, and `CollisionMetrics.MaximumPenetrationRaw` is asserted at zero
on every recorded workload. No agent has ever passed through another agent.

What the user is actually seeing is that the enforced personal space is small
relative to the drawn pawn, so a melee line reads as loose. Two agents at
exactly one diameter apart are legally clear, and at the default radius that
diameter is eight world units, which at default zoom leaves a visible gap
between sprites that are pressed as close as the rules allow. The battle looks
like a crowd that is politely not touching, rather than a shield wall.

So the change is a tuning change, not a bug fix, and the honest framing matters:
we are not adding collision, we are enlarging the body that collision already
enforces.

### 1.2 What we considered

**Increase `CollisionRules.DefaultBodyRadiusRaw` from 4.0 to 4.25 world units.**
Chosen. It is one constant, it is the exact quantity the user is complaining
about, every derived value in the simulation already computes itself from it,
and every guard that could reject it has been checked by hand and still passes
with margin.

**Increase it to 5.0 world units instead.** Rejected, and the reason is a hard
guard rather than taste. `CombatRuleset.MinimumProfileReachRawExclusive` is
defined as `2 * CollisionRules.DefaultBodyRadiusRaw`, and
`CombatRuleset`'s constructor rejects any weapon profile whose
`AttackRangeRaw` is less than or equal to that floor. The shortest profile in
both presets V2 and V3 is the Itak's shield-paired profile at ten world units.
At a 4.25-unit radius the floor becomes 8.5 world units and the Itak clears it
by one and a half units. At a 5.0-unit radius the floor becomes exactly ten world units and
the comparison is `<=`, so the Itak paired profile would be rejected and both
presets would fail to construct. Going to 5.0 therefore requires a coordinated
edit to `PhilippineCombatPresetV2` and `PhilippineCombatPresetV3` reach values,
which is a combat-balance change wearing a collision change's clothes and
belongs in its own document with its own balance measurement.

**Reduce `MovementSpeedRaw` instead.** Rejected. A smaller step produces
finer-grained blocking, but it does not enlarge the enforced personal space at
all. Two agents would still be legally clear at eight world units apart; they
would simply arrive there in smaller increments. It changes movement feel, which
the user did not ask about, and leaves the actual complaint untouched.

**Raise `MaximumTruncationRungs`, reorder the candidate ladder, or change the
collision priority key.** Rejected. Each of these changes *which* candidate is
accepted rather than *how much space* a body claims. They move both hashes for
no gain against the stated goal, and the candidate ladder is a tuned, tested
structure that nothing in the user's request argues with.

**Shrink the map or raise the agent count to force density.** Rejected. That is
a scenario-composition change, not a per-unit collision change. It would
conflate this goal with army-size balance and would change the meaning of every
recorded benchmark point.

### 1.3 The decision

Change `CollisionRules.DefaultBodyRadiusRaw` in
`src/Hukbo.Core/Simulation/CollisionRules.cs` from

```csharp
public const int DefaultBodyRadiusRaw = 4 * FixedPoint.Scale;   // 4 096 raw
```

to

```csharp
public const int DefaultBodyRadiusRaw = (17 * FixedPoint.Scale) / 4;   // 4 352 raw
```

That is 4.25 world units, a 6.25 per cent increase in radius and therefore a
12.9 per cent increase in claimed area. `FixedPoint.Scale` is 1,024, so the
division is exact and the constant stays an integer expression tied to the
scale rather than a magic number.

**No new combat preset version is required.** A preset version covers combat
content — the weapon roster, the attribute profiles, the target weight tables,
the clash tables — and its identity is `CombatRuleset.ContentHash`. Body radius
is scenario and collision data, not preset data, and it is not folded into any
preset content hash. The pinned content hashes for V1 (`0x59FB4CA563D87A49`),
V2 (`0x10AB1CC226AB3636`), and V3 (`0xCD790E489293B304`) must all come back
unchanged; if any of them moves, something was edited that should not have been.

**A golden-expectation rebaseline is required.** `Scenario.BodyRadiusRaw` is
folded into the state hash by `StateHasher.Compute`, and the larger body changes
which candidate positions are legal, which changes committed positions, which
changes `MovementResolution` values, which changes contact distances, which
changes who is in range of whom, which changes the whole battle. Both hashes
move, the winner may move, the terminal tick will move, and every golden
expectation recorded against the old default becomes non-reproducible. This is
expected and legitimate, not a defect — but it must be regenerated deliberately
and recorded in the same commit, never folded quietly into an unrelated change.

### 1.4 Every guard that could have rejected this, checked

These were all verified by reading the source rather than by running the build,
so the plan re-verifies each one with a test.

| Guard | Where | At radius 4,352 |
| --- | --- | --- |
| Body diameter must not exceed attack range | `Scenario.ValidateCollisionConfiguration` | 8,704 ≤ 12,288. Passes. |
| Movement speed must not exceed body radius | `Scenario.ValidateCollisionConfiguration` | 3,072 ≤ 4,352. Passes. |
| Body diameter must fit the map | same | 8,704 ≤ 1,310,720. Passes. |
| Population must be placeable under square packing | `Scenario.ValidateBodyDensity` | Placeable ceiling falls from 14,400 bodies to 11,377 on the default 1,280 × 720 map. The canonical 200-agent workload and the 2,000-agent stress point both still fit. |
| Rally jitter span must not overflow `Int32` | `FormationRules.IsBodyRadiusWithinJitterSpanRange` | 2 × 6 × 4,352 + 1 is nowhere near `int.MaxValue`. Passes. |
| Minimum weapon profile reach | `CombatRuleset` constructor | Floor rises to 8,704 raw. Shortest profile is the Itak paired at 10,240 raw. Passes by one and a half world units. |
| Grid cell must never be narrower than a diameter | `CollisionUniformGrid.ValidateBodyRadius` | Cell size is derived from the diameter, so it grows with it. Passes by construction. |

Two derived values change as a consequence and are correct to change:
`FormationRules.ComputeRallyJitterRaw` returns 6 × radius, so the rally jitter
radius moves from 24 world units to 27; and the collision grid becomes coarser,
160 × 90 cells becoming 142 × 80, which is a performance detail with no effect
on outcomes because the neighbourhood-sufficiency argument depends on the cell
being at least one diameter wide, not on any particular count.

### 1.5 The nine questions, for goal 1

Per `SIMULATION-GAME-STANDARDS.md` section 10.

1. **User-visible outcome.** Melee lines pack visibly tighter. Warriors stop
   further from each other in absolute terms but read as pressed together
   because the enforced gap is now a larger fraction of the drawn body. More
   movement proposals are refused, so more agents report `Blocked` or
   `Truncated` in the inspector during a press.
2. **Tick stage and state read/written.** No new stage and no new field. The
   collision stage reads `Scenario.BodyRadiusRaw` exactly where it reads it
   today; the value it reads is larger. The formation planner, the rally
   corridor, the contact-distance check, and the collision grid cell size all
   already derive from the same field.
3. **Numeric units, bounds, same-tick conflict rule.** Raw fixed-point integer
   units throughout, 4,352 raw = 4.25 world units at `FixedPoint.Scale` = 1,024.
   Bounds are the seven guards tabulated above. The same-tick conflict rule —
   stationary bodies commit first in ascending entity ID, movers in ascending
   priority key, the first legal candidate on a fixed ladder takes the ground —
   is unchanged.
4. **Total ordering and random-stream policy.** Unchanged. The collision
   priority key, its entity-ID low half, and the sort that consumes it are
   untouched. No random stream is consulted by the collision stage before or
   after. The rally jitter draw consumes the same number of values from the same
   stream; only the span it is drawn across widens, which is a value change, not
   a stream-shape change.
5. **Cache source and invalidation.** No cache is added. The two existing
   derived uniform grids rebuild per tick from the new radius exactly as they
   rebuilt from the old one.
6. **Save, event, and version effect.** No persisted field, no event field, no
   enum value, and **no preset version**. `Scenario.BodyRadiusRaw` already
   participates in the state hash and in `Scenario`'s own equality and hash
   code, so an old snapshot taken at the old radius is correctly detected as a
   different scenario rather than silently resumed under new rules.
7. **Worst-case complexity and benchmark workload.** Complexity is unchanged in
   form. The constant factor rises slightly, because a larger body makes the
   grid filter pass more often and makes more candidates fail, pushing movers
   further down the truncation ladder. The workload is the canonical 200-agent /
   10,000-tick / seed-1 headless run plus the recorded T2 four-point sweep, so
   the cost is measured rather than assumed.
8. **Spectator explanation.** Already present and already authoritative. Every
   agent carries a `MovementResolution` written by the collision stage and shown
   in the agent inspector. A spectator who wonders why a warrior is not advancing
   clicks it and reads `Blocked` or `Truncated`. Nothing new needs to be
   surfaced; the existing reason code becomes more frequently interesting.
9. **Can a spectator discover the effect without reading source code?** Yes, and
   this is the whole point of the change. The tighter line is visible at default
   zoom with no UI at all, and the reason code in the inspector explains it on
   demand. This is the opposite of the collision *performance* design, where
   discoverability would have been a defect.
10. **Tests that fail before and pass afterward.** `ScenarioTests`' assertion
    that the default radius is four world units fails before the constant moves
    and passes after it is updated. `FormationRulesTests`' assertion that the
    rally jitter is 24 world units fails before and passes at 27.
    `DeterminismTests.PresetV3_SeedOneStateAndEventHashArePinned` fails on both
    hashes before the goldens are regenerated. A new `Scenario.Validate`
    coverage test for the canonical 200-agent / 1,280 × 720 configuration and a
    new `CombatRuleset` construction test for both presets pass before and after
    — they are guards proving nothing broke, and the plan says so rather than
    pretending they are new-behaviour tests.

---

## 2. Goal 2 — the per-unit battle report

### 2.1 The problem

`MatchSummaryPanel` currently shows six numbers: the winner, the two survivor
counts, the terminal tick, the simulated duration, and the seed. A spectator who
watched an interesting battle has no way to find out who fought well in it. The
user asked for "top unit kills" and invited more.

### 2.2 What we considered

**Accumulate the report in `Hukbo.Client` from the authoritative per-tick event
stream.** Chosen. Every statistic worth reporting is already in the events the
client is handed every tick; nothing has to be invented and nothing has to be
added to `Hukbo.Core`.

**Add per-agent kill and damage counters to `Hukbo.Core` and read them at the
end.** Rejected. It adds persisted, hashed per-agent state whose only consumer
is a presentation panel, it moves both hashes, it forces a golden rebaseline for
a cosmetic feature, and it violates the rule that the client must not add
gameplay state to the simulation. The events already carry the information.

**Compute the report from `BattleEventFeed`.** Rejected, and this is the single
most important structural decision in this goal. `BattleEventFeed` retains at
most 200 ordered events by contract. A 1,710-tick battle emits far more than
200 events, so a report computed from the feed would silently describe only the
last two hundred events of the battle. The accumulator must be wired to the raw
per-tick `events` list that `PresentationCoordinator.IngestTick` already
receives — ultimately `BattleSimulation.LastEvents` — and never to the feed.

**Post-battle only, versus a live mid-battle report.** Post-battle only, for
this pass. The entry point is a button on the match summary panel, and the match
summary panel only exists once the battle is terminal. A live report is a larger
question about whether spectators should see running statistics during play, and
it can be added later without changing the accumulator at all, because the
accumulator's `Snapshot()` is already valid at any point.

### 2.3 The decision

A new `BattleReportAccumulator` in `src/Hukbo.Client/Presentation/`, owned by
`PresentationCoordinator`, fed from `IngestTick`, cleared by `ResetFor`, and
snapshotted into an immutable `BattleReport` record at `ProcessTerminal`. A new
`BattleReportPanel` in `src/Hukbo.Client/UI/` draws it, with all geometry
computed by a static pure helper `BattleReportLayout.Calculate` so the tests
never touch a graphics device.

**Twelve statistics.** Per unit: kills, damage dealt, damage taken, attacks
made, attacks landed, accuracy, and death tick. Per faction: total kills, total
damage dealt, accuracy, and survivors. Across the battle: the top killer of each
faction, first blood, the decisive kill, and the longest survivor.

**The kill-credit rule, stated exactly, because it is inferred rather than
authoritative.** `Hukbo.Core` does not record who killed whom. A `Death` event
carries only the victim. Within one tick the simulation emits every `Attack`
event first, then every `Damage` event, then every `Death` event, and each
`Attack` event carries its attacker as `SourceEntityId`, its victim as
`TargetEntityId`, its attacker's faction as `FactionId`, and the damage it dealt
as `Value` — zero unless `Resolution` is `Landed`. So on each `Death`, the
credited killer is the attacker of the highest-`Value` landed `Attack` event
against that same victim in that same tick, with ties broken on the lowest
attacker `EntityId`. If no landed attack against that victim was seen in the
tick, no kill is credited to anyone, the faction total is not incremented, and
nothing throws. That last clause is not expected to fire under the current
damage-and-death coupling in `BattleSimulation`, but the accumulator fails safe
rather than assuming unreachability, and the plan requires an explicit test for
it.

This heuristic must be documented as a heuristic in the accumulator's own
comments and hinted at in the panel, so that a future reader does not mistake a
client-side inference for a Core guarantee.

**Unit identity.** The accumulator captures each unit's faction, loadout, and
level once, on the first `IngestTick` call, by walking the `agents` roster it is
already handed. That gives a weapon label even to a warrior who died without
ever swinging, and it costs one pass over the initial roster per battle.

**Determinism and ordering.** Every accumulator output has a total order.
The leaderboard sorts on kills descending, then damage dealt descending, then
`EntityId` ascending. Each faction's top killer is the highest kill count in
that faction with the lowest `EntityId` breaking ties. The longest survivor is
the largest recorded death tick with the lowest `EntityId` breaking ties. First
blood is set once, on the first landed attack ever ingested, and never
overwritten. The decisive kill is overwritten on every credited kill, so it ends
as the last one of the battle. No dictionary iteration order may reach any
output; sorting happens in `Snapshot()` over a materialized, ordered sequence.

**Leaderboard size.** A fixed top ten. The panel is a fixed height and ten rows
is what fits legibly; scaling the cap with roster size would either overflow the
panel at 2,000 agents or waste it at 20. This resolves the design's open
question rather than leaving it to the implementer.

**Row fields.** Rank, entity identifier, faction, weapon in pair form, kills,
damage dealt, damage taken, and accuracy. `Level` is deliberately left out even
though `AgentView` carries it, to keep the row width bounded; adding it later
does not require a design change.

### 2.4 Determinism impact

None. `Hukbo.Client` is not authoritative, the accumulator reads events and
never writes to the simulation, nothing it computes is fed back, and no field
reaches the state hash or the event hash. The one determinism-adjacent
requirement is internal: the report itself must be reproducible, so every
ordering above is total and every tie breaks on `EntityId`.

### 2.5 The nine questions, for goal 2

1. **User-visible outcome.** After a battle ends, the match summary panel offers
   a Battle Report button. It opens a panel showing faction totals, four battle
   highlights, and a top-ten warrior leaderboard.
2. **Tick stage and state read/written.** No simulation stage at all. The
   accumulator reads the same per-tick event list and agent view list that the
   presentation coordinator already receives, and writes only to its own
   presentation-layer state.
3. **Numeric units, bounds, same-tick conflict rule.** Kills and attack counts
   are plain integers; damage is in the simulation's own damage units; ticks are
   integer ticks; accuracy is derived at snapshot time and never accumulated as
   a float. The same-tick conflict rule is the kill-credit rule stated in 2.3.
4. **Total ordering and random-stream policy.** Every ordering is total and
   documented in 2.3. No random stream is consulted.
5. **Cache source and invalidation.** The accumulator is a derived, bounded,
   presentation-only accumulation over the battle's events. It is invalidated
   wholesale by `ResetFor`, which already runs on both Next Round and Full
   Reset. It is never persisted and never snapshotted.
6. **Save, event, and version effect.** Presentation only. No persisted field,
   no event field, no enum value except the two new `ClientCommand` members,
   which are client input plumbing and reach nothing authoritative.
7. **Worst-case complexity and benchmark workload.** `Ingest` is linear in the
   tick's event count with a small dictionary lookup per event. `Snapshot` sorts
   the roster once, at the terminal tick only. Memory is bounded by the roster
   size, which is bounded by the scenario. The workload for evidence is the
   canonical 200-agent run plus a manual smoke pass on a real battle.
8. **Spectator explanation.** The panel is itself the explanation. Every number
   on it is labelled, and the kill-credit inference is disclosed on the panel so
   that a spectator counting kills by eye and getting a different answer knows
   why.
9. **Can a spectator discover the effect without reading source code?** Yes. A
   button appears on a panel they already see at the end of every battle.
10. **Tests that fail before and pass afterward.** Nine new accumulator tests,
    three new layout tests, three extended coordinator tests, and a match
    summary panel test. All of them fail to compile before `BattleReport`,
    `BattleReportAccumulator`, and `BattleReportLayout` exist, which is the
    strongest possible form of failing first.

---

## 3. Goal 3 — removing the operating-system window buttons

### 3.1 The problem

The user asked to remove the default Windows exit, minimize, and maximize
buttons. Those buttons belong to the operating-system title bar, which MonoGame
does not draw and cannot selectively edit. The whole bar is either present or
absent.

### 3.2 What we considered

**Set `Window.IsBorderless = true` and provide in-game replacements.** Chosen.
It is the only way MonoGame DesktopGL exposes to remove the title bar, it is one
line, and the control bar already exists as the natural home for the
replacements.

**Leave the title bar and try to disable individual buttons.** Rejected.
MonoGame exposes no such control, and reaching around it into raw Win32 window
styles would be a platform-specific hack in a project that has no
platform-specific code today.

**Remove the buttons and provide no replacement.** Rejected outright. Quitting
would then require Alt+F4 or the deep Exit Game item in the menu overlay, and
minimizing would become impossible. Taking away an affordance and giving nothing
back is a regression dressed as a feature.

### 3.3 The decision

Three coordinated edits.

`Window.IsBorderless = true;` immediately after the existing
`Window.AllowUserResizing = true;` in `ArenaGame`'s constructor.

Two new buttons appended to `ControlBar`'s button array: **Min**, issuing a new
`ClientCommand.Minimize`, and **Close**, issuing the existing
`ClientCommand.Exit`. The bar keeps its existing button metrics — 84 wide, 34
high, 8 apart — so the new buttons look like the four already there.

`ClientCommand.Minimize` handled in `ArenaGame.ApplyClientCommand` by a call
into SDL2's `SDL_MinimizeWindow`, passing `Window.Handle`, which on DesktopGL is
the underlying `SDL_Window*`.

**Two corrections to the settled design, both arithmetic or toolchain
constraints rather than taste.**

First, the control bar width. The design said 544, computed as six buttons at 84
plus five gaps at 8. That figure omits the bar's own padding: `ControlBar.Layout`
places the first button at `Bounds.Left + 10`, so with four buttons the content
occupies pixels 10 through 370 of a 384-wide bar, leaving 14 on the right. Six
buttons occupy 544 pixels of content starting at 10, ending at 554. A 544-wide
bar would clip the Close button entirely. **The correct width is 568**, which is
10 + 544 + 14 and preserves the existing asymmetric padding exactly.

Second, the interop declaration. The design specified `[DllImport("SDL2.dll")]`.
This repository builds with `TreatWarningsAsErrors` and `EnableNETAnalyzers`, and
`DllImport` on .NET 10 raises SYSLIB1054 recommending the source-generated
`LibraryImport`. Suppressing that warning would be weakening an analyzer to get
green, which `CLAUDE.md` section 5 forbids. `ArenaGame` is already
`sealed partial class`, so the source generator's requirement is already met and
the declaration becomes:

```csharp
[System.Runtime.InteropServices.LibraryImport("SDL2")]
private static partial void SDL_MinimizeWindow(nint window);
```

If that declaration cannot be made to build — for example if the DesktopGL
native library resolves under a different name on this machine — the implementer
must report the failure rather than silently dropping the Min button or
suppressing the warning.

### 3.4 Determinism impact

None whatsoever. Window chrome, window state, and the control bar are pure
presentation. `ClientCommand.Minimize` reaches no simulation state, consumes no
random value, and cannot occur inside a tick.

### 3.5 The cost the user should weigh

A borderless SDL2 window generally cannot be dragged by a title bar to move it,
and edge-drag resizing on a borderless window is not something SDL provides
without an application-supplied hit test. `Window.AllowUserResizing = true` is
very likely to become inert the moment the border is removed. In practice the
spectator ends up with a fixed 1,280 × 720 window that cannot be moved or
resized, plus in-game Min and Close buttons.

That may be exactly what the user wants — many games ship precisely this shell.
It is nonetheless a real loss of two affordances beyond the three buttons that
were asked about, so it is written down here, it is raised in the plan's flagged
list, and the manual smoke checklist asks a human to confirm what actually
happens on this machine rather than trusting this paragraph.

No confirmation prompt is added to Close. `RequestExit` has never had one, and
adding a confirmation dialog is a separate UI decision the user has not asked
for. It is recorded as an open question rather than decided silently.

---

## 4. Goal 4 — the army composition panel is too narrow for its own labels

### 4.1 The problem, measured

The user reported overshooting text in the unit setup menu. The arithmetic
confirms it and quantifies it.

`ArmyCompositionPanel.CalculateLayout` derives each row's label box as the row
width minus the stepper width. With the shipped theme metrics — panel width 420,
row gap 8 giving a margin of 16 on each side, stepper width 260 — the row is
420 − 32 = 388 wide and the label box is 388 − 260 = **128 pixels**.

Category labels draw at the `Label` font rung.
`UiFontRamp.GetApproximateAdvancePx(UiFontRole.Label)` is
`ceil(17 × 0.65)` = **12 pixels per character**. The longest category label is
`Kalis — Thrusting Blade (shielded)` at 34 characters, which needs about
**408 pixels**.

408 into 128 is an overrun of more than three times the available box. Every
one-handed weapon row overflows badly, and this has been invisible to the test
suite because the only layout test,
`EveryLaidOutRowFitsInsideThePanel`, is a purely vertical invariant.

### 4.2 What we considered

**Widen the panel and narrow the stepper.** Chosen. It fixes the labels without
touching the font ramp, the label text, the row height, or the panel height, and
it needs no code change at all — only theme data.

**Shorten the labels.** Rejected on policy grounds. `CLAUDE.md` section 7
requires the pair form — Filipino name, em dash, plain English descriptor — and
`ArmyCompositionPanelTests` asserts both the pair form and the `(solo)` /
`(shielded)` disambiguation. Truncating a cultural identification to fit a box
is exactly what the historical accuracy policy forbids.

**Drop the label to the `Body` or `Caption` rung.** Rejected. `Body` at 14 pixels
gives a 10-pixel advance and still needs 340 pixels, so it does not fix the
overflow on its own, and it makes the panel's primary text smaller than the
panel's secondary text.

**Wrap or ellipsize the label.** Rejected. Wrapping needs a taller row and a
taller panel; ellipsizing hides the grip suffix, which is the one part of the
label that distinguishes two otherwise identical rows.

### 4.3 The decision

Change the shared army composition metrics from
`(420, 648, 44, 8, 260, 44)` to `(640, 648, 44, 8, 148, 44)`. Only the panel
width and the stepper width move. Panel height, row height, row gap, and arrow
width all stay exactly as they are.

The new label box is 640 − 32 − 148 = **460 pixels**, which clears the 408-pixel
worst-case label with 52 pixels of headroom. The new value box between the two
arrows is 148 − 88 = **60 pixels**, which holds a four-digit count at 48 pixels
with room to spare. The arrow width is untouched, so it stays inside the theme's
minimum-target-size range and the existing catalog assertion on that range
continues to hold.

The metrics live in two places that must move together: the shipped
`src/Hukbo.Client/Content/Themes/ui-theme-standards.json`, which is what the
game actually loads, and `UiThemeCatalogFallback.cs`, which is what loads when
the JSON is missing. A test already asserts the two are equal, so changing one
without the other fails the build — which is the desired behaviour, not a
hazard.

### 4.4 Determinism impact

None. `UiArmyCompositionLayout`, panel geometry, and font metrics are pure
presentation constants in `Hukbo.Client`. None of them reaches `Hukbo.Core`, the
state hash, the event hash, or any random stream. The army *composition* itself
— the roster counts the panel edits — does reach the simulation, but this change
does not touch a single count; it only changes the size of the box those counts
are edited in.

### 4.5 The test that should have existed

The reason a three-times overflow shipped is that no test ever measured a label
against its box. The plan adds
`LabelBoundsFitTheLongestCategoryLabelUnderTheConservativeAdvanceEstimate`,
which multiplies the longest entry in `ArmyCompositionPanel.CategoryLabels` by
`UiFontRamp.GetApproximateAdvancePx(UiFontRole.Label)` and asserts the product
fits `LabelBounds.Width` on every laid-out row. It fails on the current metrics
and passes on the new ones, so it is a genuine failing-first test rather than a
guard, and it will keep failing if anyone adds a longer weapon name later.

---

## 5. Cross-goal conflicts, and how they are resolved

Four designers worked independently. Three of their outputs collide, and the
resolutions are recorded here rather than left for the implementers to
discover.

**`src/Hukbo.Client/Presentation/ClientCommand.cs` is needed by two goals.** The
battle report needs `ToggleBattleReport`; the window shell needs `Minimize`.
Resolution: the `window-chrome` workstream owns the file and adds **both**
members in a single first task, before either goal's other work starts.
Everything else depends on that task. The final enum is
`None, Play, Pause, OpenMenu, NextRound, FullReset, Exit, Minimize,
ToggleSoundLog, OpenArmyComposition, ToggleBattleReport`. New members are
appended and existing members keep their positions, so nothing that switches on
the enum changes meaning.

**`src/Hukbo.Client/ArenaGame.cs` is needed by two goals.** The window shell
needs the borderless flag, the interop declaration, and the `Minimize` case; the
battle report needs a panel field, a visibility flag, a place in the pointer
priority chain, and a draw call. Resolution: `window-chrome` is the integrator
and owns every edit to `ArenaGame.cs` and `ArenaGame.Rendering.cs`, including
the ones the battle report needs. The `battle-report` workstream delivers a
panel with a self-contained public surface and never opens either file.

**`tests/Hukbo.Client.Tests/UiThemeCatalogTests.cs` hardcodes the old army
composition numbers.** Its `StandardsExposeTheArmyCompositionLayout` asserts
`420` and `260` directly, so the goal 4 change breaks it. The file's name does
not begin with `ArmyComposition`, so the stated ownership rule does not cover
it. Resolution: it is assigned to `unit-setup-menu`, which is the only
workstream with a reason to touch it, and no other workstream may edit it.

**The control bar grows and moves left.** At 568 wide, anchored ten pixels from
the right edge of a 1,280-pixel window, the bar now spans x = 702 to 1,270,
where it previously spanned 886 to 1,270. Nothing else is anchored to the top
edge except the status readout on the left, so no overlap is expected — but this
is a geometric claim about a running window, so the manual smoke checklist asks
a human to confirm it rather than asserting it here.

**The stale hash citation.** The collision-scaling design document, and two
sections of `docs/development/testing.md`, still describe
`71211929A44A16CA` / `A2DC3ECA3F7345ED` as the recorded baseline. The current
baseline, recorded at the top of `docs/development/testing.md`, is
**`stateHash A883926A3B93792E`, `eventHash 2A9F2D7054CD1805`**, at 1,710
measured ticks with a `Faction1Victory` and survivor counts of 0 and 2. Goal 1
moves that pair, and the plan's documentation task states the new pair against
the correct old one rather than against the stale one.

---

## 6. What this design does not decide

- Whether the battle report should be reachable mid-battle while paused. Scoped
  out; the accumulator supports it whenever someone wants it.
- Whether Close should ask for confirmation. Not added; recorded as a question.
- Whether an explicit maximize or fullscreen toggle should replace the one the
  operating system used to provide. Not added; recorded as a question, and it
  becomes more pressing if the borderless window turns out not to be resizable.
- Whether 4.25 world units is the final body radius. It is **not** the largest
  value that clears the weapon reach floor — 4.5 does that too — but it is the
  largest value tested that does not deadlock the simulation. See section 7.3.
  If the packed line still reads as loose after a real playtest, the next step
  is a coordinated radius-and-reach change in its own document, with the
  last-stand sweep rerun as part of it, not a quiet second bump here.
- Anything at all about the collision *performance* workstream, which remains
  design-only.

---

## 7. Two further consequences of goal 1, found while writing the plan

Sections 1.3 and 1.4 were written from a reading of the collision and combat
sources. Reading the test suite and the standards document afterwards turned up
two more consequences that neither of those sections covers. Both are recorded
here rather than left as surprises for the implementer, because both are the
kind of thing that looks like a broken test and gets "fixed" the wrong way.

### 7.1 The pre-clash digest control run must pin the old radius explicitly

`DeterminismTests` carries two Facts —
`ZeroInterceptionProfile_ReproducesThePreClashDigest` and
`ZeroInterceptionProfile_ReproducesTheRecordedStateHash` — that replay a
200-agent seed-1 control run against
`tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-preclash-digest.json`, a
fixture captured from a build that predates the weapon-clash system. Their whole
purpose is to prove that the clash change is neutral when the interception
probability is zero. The comparand is a recorded artefact of an older build, not
another run of this one.

Their helper, `CreateZeroInterceptionControlRun`, builds its scenario with
`Scenario.CreateDefault(seed: 1, totalAgents: 200)`, which takes
`BodyRadiusRaw` from `CollisionRules.DefaultBodyRadiusRaw`. Enlarging that
constant therefore changes the control run's geometry and both Facts fail.

**Regenerating that fixture against the new build would destroy the only
evidence the two Facts exist to carry**, because a fixture captured from the
current build compared against the current build proves nothing about the
pre-change behaviour. The correct resolution is the one the helper already uses
for exactly this problem one field away: it pins
`CombatPreset = CombatPresetId.PrecolonialPhilippinesV1` explicitly, with a
comment explaining that the fixture was captured before `Scenario.CombatPreset`
defaulted to V2. The body radius gets the same treatment —
`BodyRadiusRaw = 4 * FixedPoint.Scale`, with a comment saying the fixture was
captured before the default moved to 4.25 world units. The two Facts then keep
comparing like against like and keep passing unchanged.

The same reasoning applies to any other test that pins a value recorded from an
earlier build. It does not apply to `PresetV3_SeedOneStateAndEventHashArePinned`,
whose two hashes are a golden expectation of *current* behaviour and are meant to
be regenerated whenever current behaviour legitimately changes.

More broadly: `Scenario.CreateDefault` appears about a hundred times across ten
Core test files, so the implementer cannot assume the failures listed in section
1.4 are the complete set. The plan therefore runs the whole Core suite and
triages, under one rule — a test whose assertion is *about the default radius*
gets its expected value updated, and a test that fails for any other reason is
reported rather than adjusted.

### 7.2 The standards document currently forbids this change in so many words

`SIMULATION-GAME-STANDARDS.md`, in "Hashing, persistence, and observability"
under the collision contract, ends with:

> Because `BodyRadiusRaw`, `CollisionPolicy`, and `MovementResolution` all reach
> the state hash, and because constraining movement changes where agents stand,
> both the state hash and the event hash moved for every seed when this contract
> shipped. Changing any of those three fields in future requires a new preset
> version and new golden expectations.

Read literally, that sentence says goal 1 requires a new combat preset version,
which directly contradicts section 1.3 of this document. The contradiction is
real and has to be resolved rather than ignored, because
`SIMULATION-GAME-STANDARDS.md` is a live contract.

The resolution is that the sentence is imprecise, and cutting a preset V4 would
not do what the sentence is reaching for. A combat preset version protects
*combat content* — roster, attribute profiles, target weight tables, clash
tables — and its identity is `CombatRuleset.ContentHash`. `BodyRadiusRaw` is a
`Scenario` field with a default supplied by `CollisionRules`. A preset V4 whose
combat content is byte-identical to V3 would carry a content hash that is either
identical to V3's or perturbed on purpose to look different, and either way an
old replay naming V3 would still be replayed at the new radius, because the
radius does not come from the preset. Versioning the preset would create the
appearance of protection while providing none.

What the sentence is actually reaching for is the part that does hold: any change
to one of those three fields invalidates every recorded golden expectation, and
the rebaseline has to be deliberate and recorded in the same commit. So the plan
amends the sentence to say that, and adds the explicit note that preset
versioning does not and cannot cover scenario collision defaults.

**This is an edit to a live contract document, and it is flagged for the user
rather than treated as a routine documentation update.** If the user prefers the
literal reading, the alternative is to abandon goal 1 as scoped and reopen it as
a combined preset-and-collision change — not to cut a cosmetic V4. The plan
carries the amendment as its own task so that it can be dropped without
unpicking anything else.

Three further passages in the same document state the old numbers as fact and go
stale the moment the constant moves: the collision-rule table that lists
`BodyRadiusRaw` as `4096` / 4 world units and the body diameter as `8192` / 8;
the contact-metric paragraph that derives a proximity band of `5632` raw units
per body and a broad-phase pairing distance of `11264`; and the recorded contact
figures for the 200-agent and 500-agent workloads. At 4,352 the band becomes
`4352 + 1536 = 5888` per body and `11776` between centres. The recorded contact
counts are measurements of a past run, so they are marked as superseded and
re-measured rather than edited in place.

### 7.3 The deadlock finding: why the radius is 4.25 and not 4.5

This section is written after implementation, because the constraint it records
was discovered by running the code rather than by reading it. It is the most
important result this workstream produced.

Section 1.3 originally chose 4.5 world units, on the reasoning that it was the
largest value clearing every validation guard without a coupled edit to a combat
preset. Section 1.4 tabulated seven guards and confirmed each one passes with
margin. That table is correct, and it was not enough.

**A radius of 4.5 world units deadlocks the simulation.** With the constant set
to `(9 * FixedPoint.Scale) / 2`, this pre-existing test fails:

```
tests/Hukbo.Core.Tests/LastStandFormationTests.cs
NoLastStandBattleStallsAtTheTickLimitAcrossSeedsOneThroughTwenty

seed 12: stalled at tick 10000 of 10000, outcome Draw,
living counts [9, 9], longest blocked streak 9976 ticks.
```

Nine agents per side, alive, unable to move, for 9,976 consecutive ticks. That
test is not incidental — its own documentation calls it a load-bearing
regression lock for the follower-trailing fix, guarding against the case where
"two factions doing this at once deadlocked the whole battle at the tick limit
with zero casualties". The enlarged body reintroduced exactly that class of bug
at the tightest last-stand packing, where `LastStandThresholdAgents` equals
`FormationRules.MaximumLastStandThresholdAgents`.

The failure is deterministic and reproduces identically on every run. Reverting
the radius to 4.0 makes it pass; restoring 4.5 makes it fail again.

**The measured cliff.** The same test was run at three radii on 2026-07-28:

| Radius | Last-stand sweep, seeds 1–20 |
| --- | --- |
| 4.5 world units | **Fails** — seed 12 stalls at the tick limit |
| 4.25 world units | Passes |
| 4.125 world units | Passes |

So the boundary lies somewhere in the interval between 4.25 and 4.5, and the
shipped value sits immediately below it. That is a far narrower margin than the
guard table in section 1.4 suggests, and the gap between those two numbers is
the honest measure of how much headroom this change actually has.

**Three things follow, and none of them should be lost.**

First, **no static guard catches this.** Every one of the seven guards in
section 1.4 still passes at 4.5. They are all arithmetic checks on a single
configuration; a deadlock is a dynamic property of many agents interacting over
thousands of ticks. Checking the guards is necessary and is nowhere near
sufficient, and section 1.4 should be read with that caveat permanently attached.

Second, **the evidence for 4.25 is real but narrow.** The last-stand sweep covers
seeds 1 through 20 at an 18-agent configuration. Passing it means 4.25 does not
deadlock in those twenty specific battles. It is not a proof that 4.25 is safe in
general, and it should not be quoted as one. The canonical 200-agent seed-1
workload also completes normally at 4.25, reaching a decisive outcome in 1,677
ticks with `maximumPenetrationRaw 0`, which is further evidence and still not a
proof.

Third, **any future increase must rerun this sweep.** A change that only
re-checks the guard table will pass review and deadlock the game. The constant
in `CollisionRules.cs` carries a remark saying so, and that remark is the
mechanism by which this finding survives being forgotten.

**On the process.** The implementing agent that hit this failure did not weaken
the test, adjust its tolerance, or drop seed 12 — it stopped and reported the
stall as a real behavioural regression. That was the correct call, and it is the
reason the deadlock is documented here rather than shipped silently behind a
loosened assertion.
