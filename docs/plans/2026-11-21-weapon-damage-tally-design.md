# Weapon damage tally — design

**Status: design.** The plan document `docs/plans/2026-11-21-wmao-optimizations.md`
carries the ordered task list. The user authorized execution of this package on
2026-11-21, after the documents are written.

**Baseline: `main` at `0ccc0d3`, 2026-11-21.** Hukbo client only.

---

## 1. The question this document answers

Which weapons are winning this battle?

Hukbo has spent months giving each weapon its own reach, cadence, shield
behaviour, and armour interaction. A spectator can see a battle end, and the
terminal battle report lists the top ten units. During the battle, though,
nothing shows whether the kampilan line or the archers are doing the work.
Section 10 of `SIMULATION-GAME-STANDARDS.md` asks whether a spectator can
discover an effect without reading the source. For weapon identity the honest
answer today is "only by staring".

We March as One answers the same question with a damage card per side. It
lists unit types sorted by damage dealt, with a bar and a share, refreshed four
times a second. Hovering a row rings every unit of that type on screen
(`DamageHUD.cs`, `DamageBreakdownCard.cs`, and `UnitSpotlight.cs` in its
decompiled source). This design adapts that idea to Hukbo's weapons and labels.

## 2. What the spectator sees

- **Toggle.** The `T` key toggles a **damage tally** card. It starts hidden,
  so a first-time spectator's view does not change.
- **Sections.** The card has one section per faction, in faction colour from
  the theme.
- **Rows.** Each row is one weapon, labelled with the paired name the client
  already uses, for example `Kampilan — Great Blade`. A shielded variant is its
  own row, labelled by the same helper.
- **Row contents:**
  - a bar, plus damage dealt as a number and a share of the faction's total;
  - blows landed;
  - living count, for example `12 / 40 alive`.
- **Order.** Rows sort by damage dealt, highest first. Equal values fall back to
  label order, so rows do not swap places at random.
- **Refresh.** The card redraws its numbers every 0.25 s of wall time, so values
  stay readable at 4×.
- **Hover spotlight.** Hovering a row draws a ring around every **living** agent
  of that faction and weapon. The ring answers "where are they?" without a
  click.
- **Reset.** The tally clears on reset and full reset.

## 3. Where the numbers come from

All the data already reaches the client. No `Hukbo.Core` change is needed.

- `PresentationCoordinator.IngestTick` (`PresentationCoordinator.cs:230-259`)
  receives every event of every tick. The tally must read that list, never the
  200-event feed, which drops events. `BattleReportAccumulator.cs:9-13`
  records the same rule.
- A `BattleEventKind.Attack` event carries `SourceEntityId`, `TargetEntityId`,
  `FactionId`, `Weapon`, `Shield`, `Resolution`, `HitLocation`, and `Value`
  (`BattleEvent.cs:72-241`). `Value` is the attacker's nominal damage when the
  blow lands and 0 otherwise (`BattleSimulation.cs:5466-5478`).
- Ranged impacts arrive as ordinary `Attack` events at arrival
  (`BattleSimulation.cs:5146-5161`). The tally counts an arrow when it lands,
  not when it is loosed.
- A `BattleEventKind.Damage` event carries the target's total damage after
  armour for the tick, with **no attacker** (`BattleSimulation.cs:5481-5497`).
  `Death` events carry no killer either, so kills cannot be attributed honestly
  and the card does not show them.

### 3.1 Before or after armour

The nominal `Value` is damage **before armour**. Showing it as "damage dealt"
would overstate weapons that hit armoured targets. That undoes the armour work,
which is the opposite of what the card is for.

`CombatRuleset.ApplyArmorMitigation` is public (`CombatRuleset.cs:383`). If the
client can apply it to each landed blow, using the target's armour from its
`AgentView.Loadout` and the event's `HitLocation`, and the per-target sums equal
each tick's `Damage` events, then the card shows true damage after armour.

The first implementation task establishes that equality with a test against a
real 500-agent battle under the shipped presets:

- **If the sums match on every tick,** the card shows **damage dealt** after
  armour.
- **If they do not match,** the card shows **damage before armour**, labelled
  exactly that way, and the mismatch is recorded in this document. The client
  never invents a mitigation rule of its own.

**DT-1 result, 2026-11-21: the sums match on every tick, so the card shows
damage after armour.** `WeaponDamageArmorEqualityTests` runs a real 500-agent
`BattleSimulation` (250 per team, seed 1, the default army and armour
compositions) under combat preset V8 and movement preset V14, the shipped
presets. The battle ran to its end at tick 2,755. On every one of those ticks,
for every target, the sum of `ApplyArmorMitigation(attack.Value, target armour,
HitLocation)` over that tick's landed `Attack` events equalled the target's
`Damage` event value, with 0 mismatching ticks. The run covered 4,571 landed
blows, 752 of which met armour that reduced them, and 4,383 `Damage` events,
so the check is not vacuous: a before-armour sum would have failed on those 752
blows. The test also asserts at least 500 landed blows and at least one
armour-reduced blow. `WeaponDamageTally` therefore applies
`ApplyArmorMitigation` and the "before armour" fallback is not needed.

Either way, damage includes overkill: the part of a blow beyond the target's
remaining health. Removing it would need the target's health before each blow
in tick order, which the event stream does not carry. The card says
"includes overkill" in its footer.

## 4. Code shape

The tally follows the pure-helper pattern in `.claude/skills/hukbo-client-ui/SKILL.md`.

| Piece | File | Role |
| --- | --- | --- |
| `WeaponDamageTally` | `src/Hukbo.Client/Presentation/WeaponDamageTally.cs` | Pure accumulator, fed from `IngestTick`. Fixed arrays indexed by faction, weapon, and shield. No dictionary iteration decides any order. |
| `DamageTallyLayout` | `src/Hukbo.Client/UI/DamageTallyLayout.cs` | Pure `Calculate` that returns row rectangles, modelled on `BattleReportLayout.Calculate` (`BattleReportLayout.cs:72`) |
| `DamageTallyPanel` | `src/Hukbo.Client/UI/DamageTallyPanel.cs` | `Update` for hover and `Draw`, modelled on `BattleReportPanel` |
| Spotlight ring | `src/Hukbo.Client/Rendering/PawnRenderer.cs` | One new mark, adapted from `DrawSelectionMark` (`PawnRenderer.cs:494-512`). `Rendering/SubmissionCount.cs` gains its quad count. |
| Command | `src/Hukbo.Client/Presentation/ClientCommand.cs` | Append `ToggleDamageTally`. The enum is append-only. |
| Key | `ArenaGame.GetSpectatorKeyboardCommand` (`ArenaGame.cs:1319-1360`) | Map `T` |

Rules the implementation must keep:

- **Labels come from one helper.** Use `BattleEventFormatter.GetWeaponLabel`.
  The comment at `BattleEventFormatter.cs:150-159` records a second
  weapon-label switch crashing the client on the first arquebus attack. Do not
  write another.
- **Colours come from the theme.** Use the 27 semantic roles in
  `Theming/UiTheme.cs`, never a literal `Color`.
- **Pointer priority.** The card takes the pointer before the arena and after
  the inspector, in the chain at `ArenaGame.cs:1043-1114`.
- **The menu panel is full** (657 px budget, both columns at 634). The toggle is
  a key, not a menu row.
- **Placement.** The card anchors to the top-right of the arena. The layout
  helper clamps it inside the viewport at every supported window size, and its
  tests cover 1280 × 720, 1600 × 900, and 1920 × 1080.

## 5. Tests and smoke rows

- `WeaponDamageTallyTests`:
  - a landed blow adds damage and a blow;
  - a non-landed resolution adds neither;
  - ranged impacts count at arrival;
  - reset clears everything;
  - equal damage sorts by label;
  - every weapon in the shipped roster produces a label without throwing.
- **Armour equality test** (section 3.1), against a real `BattleSimulation`
  in `Hukbo.Client.Tests`, using the shipped combat and movement presets.
- `DamageTallyLayoutTests`: the card fits inside the viewport at the three
  window sizes, and rows never overlap.
- `SubmissionCount` test update for the spotlight ring.
- New smoke rows, all `PENDING`:
  - `T` shows and hides the card;
  - rows reorder as a battle unfolds;
  - hovering a row rings the right agents;
  - the card does not block clicks on the arena outside its rectangle.

## 6. What this design does not do

- It does not attribute kills. The event stream has no killer.
- It does not change `Hukbo.Core`, a hash, or the terminal battle report.
- It does not persist the toggle. The settings schema stays at 14.
