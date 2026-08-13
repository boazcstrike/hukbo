# Pressure interrupt observability — design

**Archived: reference only.** This design was implemented, and the eleven smoke
rows it was written to unblock — `P-1` through `P-10` and `L-7` — were run by a
person on 2026-08-14 and all passed. Never execute it, never treat it as
current, and never cite it as the reason to make a change. The live contract for
this project remains `CLAUDE.md` and `docs/development/smoke-checklist.md`.

**Date:** 2026-08-13
**Status:** design. This document does not authorize implementation; the plan
document beside it does.

## 1. The problem, stated as it actually is

Ten smoke rows, `P-1` through `P-10`, cover the movement V7 footwork pressure
interrupt. Nine of them have stood `BLOCKED` since the day they were written,
and the tenth has stood `PENDING`. On 2026-08-13 a person at an interactive
desktop attempted them anyway and reported, row by row:

| Row | Tester's report |
| --- | --- |
| P-1 | "doesnt look like it, they just fight forever without significant positional movements" |
| P-2 | Passed |
| P-3 | "no breakoff mark" |
| P-4 | "no breakoff mark" |
| P-5 | "what's the mark supposed to be? i think unclear" |
| P-6 | Same cause as P-1 |
| P-7 | "check logs, otherwise i dont see this" |

Every one of those observations is the correct reading of a correctly built
feature that the player has no route to. The client's `BuildScenario` hardcodes
`MovementPresetId.BattlefieldRealismV10`
(`src/Hukbo.Client/ArenaGame.cs:1442`), and `BattlefieldRealismV10` declares
`appliesPressureInterrupt: false`
(`src/Hukbo.Core/Movement/MovementPresetRegistry.cs:560`). The only registered
preset that declares it `true` is `EquipmentRelativeFootworkV7`
(`MovementPresetRegistry.cs:405`), and no preset selector is exposed anywhere in
the client.

Under that default:

- All three pressure members of `AgentView` stay at their zero defaults forever
  (`src/Hukbo.Core/Simulation/AgentView.cs:157-159`), so no break-off mark can
  ever be drawn.
- The inspector's pressure row is suppressed by `FormatPressureLine`'s own
  `thresholdBasisPoints <= 0` guard
  (`src/Hukbo.Client/UI/AgentInspectorContent.cs:757-760`), so no pressure row
  can ever render.

"No breakoff mark" is therefore not a defect report. It is `P-10`'s expected
result — the row that proves the feature is gated — observed correctly and
recorded against the wrong nine rows.

**Two of the ten rows are additionally wrong as written**, and that is a defect
in this checklist rather than in the game. Both are corrected in section 5.

## 2. Decision

**Expose a movement-preset selector in the client.** Staged, not live: the
selection is written to the client settings store and picked up by
`BuildScenario` on the next Full Reset, exactly as the army composition itself
already behaves (`ArenaGame.cs:1350-1372`, `:2098-2114`). Its default is
`BattlefieldRealismV10`, the value the client hardcodes today, so a player who
never touches it sees no change of any kind.

That single change makes executable, for the first time:

- `P-1` through `P-9`, by selecting `EquipmentRelativeFootworkV7`.
- `L-7` — "launch under `IndependentPursuitV1`" — which was unreachable for the
  same reason and was never marked `BLOCKED` the way the `P` rows were.
- `P-10`, which becomes a real regression check — deliberately switching to a
  non-applying preset — instead of a restatement of what the default happens to
  be.

## 3. What was considered and rejected

**Flipping the shipped default to a pressure-applying preset.** Rejected.
Decision D6 of the V7 workstream moves the default only once the termination bar
passes, and the calibration record establishes that V7 never will.

**A new preset carrying `BattlefieldRealismV10`'s rules plus the pressure
interrupt.** This was the original recommendation in this workstream and it is
**withdrawn**, on three findings that were measured rather than assumed:

1. `BattlefieldRealismV10Ruleset` registers `usesEquipmentRelativeFootwork:
   false` and `loadoutMovementProfiles: ImmutableArray.Empty`
   (`MovementPresetRegistry.cs:556-559`). The pressure interrupt fires on
   `Commit` and `Recover` footwork phases that exist only when that flag is
   true, and every threshold it compares against lives on those profile rows.
   "V10 plus the flag" would register a feature that can never fire.
2. It is not even constructible. `ValidatePressureInterruptCoupling`
   (`MovementRuleset.cs:521-583`) throws whenever `appliesPressureInterrupt` is
   true and `usesEquipmentRelativeFootwork` is false. The rulesets are
   `static readonly` fields on one static class, so that throw would arrive at
   type-initializer time and fail every test in the assembly that touches the
   registry — not merely the new preset's own tests.
3. V8's, V9's, and V10's behaviours are gated on **preset identity at their call
   sites**, not on any `MovementRuleset` field: `BattleSimulation.cs:258`,
   `:509`, `:650`, `:1224`, `:1853-1854`, `:1868` for V10, `:2445` for V9, and
   `:1853` for V8. A new preset inherits none of them unless each site is
   edited by hand.

A preset that genuinely combined V10's battlefield behaviour with the interrupt
would therefore be V9's footwork ruleset, plus six threshold rows, plus V7's
three weights, plus an edit to nine identity gates in the authoritative
simulation — a combination nobody has measured, whose footwork half is the
recorded standoff root cause that V9 exists to address. **That is a calibration
workstream with its own design document and its own decision, and it is not
authorized here.** Section 6 records what it would owe.

**A debug keybinding instead of a settings control.** Rejected. A key that only
someone who read the source knows about fails the section 10 discoverability
question every feature in this repository has to answer.

**The main menu as the selector's home.** Rejected on arithmetic, not taste.
`MenuOverlay` already stacks five settings selectors, and
`CalculateContentBottomOffset` (`src/Hukbo.Client/MenuOverlay.cs:152-167`)
computes the stack against `SelectorTopOffset = 122`, `Selector.Height = 96`
and `SettingsSelectorGap = 8`. Five selectors need 634 px; a sixth needs 738 px
against a `ResponsivePanelHeight` of 680 (`MenuOverlay.cs:37`). It does not fit,
and both `ResponsivePanelContainsEveryControl` and
`ThePanelIsTallEnoughForEveryMenuControl` in
`tests/Hukbo.Client.Tests/MenuOverlayFocusTests.cs` would fail. The Army
Composition panel is the better home regardless: it is already the staged
scenario screen, and the movement preset is a `Scenario` field exactly as the
roster counts are.

## 4. Determinism

**This change adds no line to either simulation.** `src/Hukbo.Core` is not
touched, no enum value is added, no ruleset field changes, and no preset's
`ContentHash` moves. The client stops hardcoding one registered preset and reads
a persisted choice whose default is that same preset.

The obligations that remain are the ordinary ones:

- The canonical gate's 200-agent, 10,000-tick, seed-1 workload must report the
  same state hash, event hash, winner, and ordered event stream as before.
  Anything else means the client's default is no longer what it was.
- An unregistered or corrupt persisted value must fall back to the default
  rather than reaching `Scenario.Validate`, which throws
  `ArgumentOutOfRangeException` for an unregistered preset by deliberate design
  (`Scenario.cs:319-325`, `MovementPresetRegistry.cs:9-10`). The settings store
  already resolves every other enum field this way, per field and
  independently, and this field follows the same shape.

Note for anyone reading `Scenario` rather than the client:
`Scenario.CreateDefault()` returns `PersistentContingentsV4`
(`Scenario.cs:138-139`, pinned by
`ScenarioTests.CreateDefaultSelectsPersistentContingentsV4MovementPreset`).
`BattlefieldRealismV10` is the default only at the client's own construction
site. Both statements are true and they are about different things.

## 5. The two smoke rows that are wrong, and their corrections

**P-5 asks a question the checklist never answered.** The tester asked what the
mark is supposed to be, and nothing in the file said. The mark is one solid
rectangle in `RGB(232, 108, 40)` (`PawnRenderer.cs:76`), drawn in a single
`spriteBatch.Draw` call (`:484`), sized `max(3, headWidth * 2 / 3)` wide by
`max(1, headHeight / 6)` tall (`:1742-1743`), centred horizontally on the head
and placed one gap above the leader-mark slot (`:1744-1749`). That slot is
reserved whether or not a leader chevron is drawn (`:1756`), so the two marks
can never overlap. It scales with zoom, being a pure function of the head
bounds. The row must carry a plain-English description: **a solid orange-brown
bar directly above the leader-rank chevron slot, above the warrior's head.** A
row that asks a person to look for something has to say what it looks like.

**P-8 asks for six distinct thresholds. There are four values and two ties.**
The shipped V7 numbers (`MovementPresetRegistry.cs:392-403`):

| Weapon row | Threshold, basis points |
| --- | --- |
| Kampilan | 10,000 |
| Wasay | 10,000 |
| Kalis with a tall hardwood shield | 8,750 |
| Kalis | 7,500 |
| Itak with a tall hardwood shield | 7,500 |
| Itak | 6,250 |

The row's named endpoints hold: Kampilan and Wasay are the highest, Itak alone
is the lowest. But a tester asked to confirm that "each shows its own threshold"
in a strict six-step order would correctly fail a build behaving exactly as
designed. The row must be reworded to expect the ties by name.

The remaining rows keep their wording and move from `BLOCKED` to `PENDING`,
because the reason they were blocked has been removed.

## 6. What this does not fix, stated so no one reports it twice

`P-1`'s "they just fight forever without significant positional movements" is
the standoff. Its root cause is recorded and sits upstream of the interrupt:
`FootworkPhase.Refuse` is a clearance rejection rather than a decision, and no
tuning of the interrupt reaches it. `EquipmentRelativeFootworkV7` is the preset
that behaviour was measured on, and a tester who selects V7 to run these rows
will see a battle that does not terminate inside its tick limit.

That is expected, it is already recorded, and it must not be filed again as a
fresh defect. It also means two rows are judged differently from the other
seven:

- `P-3` through `P-9` ask whether marks and inspector rows are **legible**. A
  non-terminating battle does not prevent judging that; the interrupt fires on
  well under one per cent of agent-ticks, which across 200 agents is frequent in
  absolute terms.
- `P-1` and `P-2` ask whether a break-off reads at 1× speed **in the flow of a
  battle**. They are being watched on a battle that does not resolve, and a
  tester should say so in the row rather than passing or failing it silently.

The preset that would fix this — V9's footwork plus V10's realism plus the
interrupt — is described in section 3 and is not authorized. What it owes before
anyone builds it: a design document naming all nine identity gates, a
termination measurement against the section 2.1 bar that V7 failed, and new
golden expectations. Note that `MovementPresetRegistryTests` pins `ContentHash`
literals for V1 through V7 only, and digest fixtures exist for V1 through V9 but
not V10, so there is no single uniform obligation to copy — the new preset's
evidence has to be argued rather than pattern-matched.

## 7. Verification

1. `./scripts/verify.ps1`, full, real output recorded. The seed-1 baseline must
   be unmoved — see section 4.
2. Both test suites. A `scripts/` or Core-adjacent change has twice turned the
   Client suite red on this repository.
3. The nine rows move to `PENDING`. **No agent may flip one.** A person at an
   interactive desktop selects `EquipmentRelativeFootworkV7`, runs them, and
   only that closes them.

## 8. The nine questions

The one that matters here is section 10's discoverability question: *can a
spectator discover this effect without reading source code?* Before this change
the honest answer was no — the effect existed, was unit-tested, and was
unreachable from the game. After it, a spectator selects a preset from a panel
the menu already links to, and sees the mark and the two inspector rows. That is
the whole point of the change, and it is why a debug keybinding was not enough.
