# Army Composition Settings — Design

> **Archived: reference only.** This document is deprecated. Do not execute it, and do not treat its steps, versions, or tooling references as current. The live contract is `CLAUDE.md` plus the skills in `.claude/skills/`.

Status: implemented on 2026-07-27 and verified by the canonical gate. The manual
smoke rows in `docs/development/testing.md` remain `PENDING` a human at an
interactive desktop. The companion plan document is
[2026-07-27-army-composition-settings.md](2026-07-27-army-composition-settings.md).

## 1. Problem

A spectator cannot currently change how many warriors fight, or what they carry.
The Client hardcodes `DefaultAgentCount = 200` in
[ArenaGame.cs:38](../../src/Hukbo.Client/ArenaGame.cs), and every warrior's
equipment is decided by a fixed round-robin walk over the four-entry preset
roster in
[CombatRuleset.ResolveLoadout](../../src/Hukbo.Core/Combat/CombatRuleset.cs),
which is `(entityId - 1) % 4`. At 200 total agents that always produces exactly
25 of each category per faction. There is no way to ask "what happens when one
side is mostly shield-bearers", because there is no way to express that question.

## 2. Proposed outcome

A new **Army Composition** panel, reached from the existing menu overlay, where
the spectator sets:

- **Units per team** — the total warriors each faction fields.
- **Four category counts** — Great Blade, Heavy Chopper, Thrusting Blade,
  Work Blade.

The four category counts must add up to exactly the units-per-team total. Both
factions receive the identical composition; there is no per-team divergence
control and no plan to add one.

Applying a composition does not disturb a battle that is already running. It is
staged and consumed by the next Full Reset, because a `Scenario` is frozen for
the life of a simulation and rewriting the roster mid-run would break the replay
contract in `SIMULATION-GAME-STANDARDS.md` §4.

## 3. Naming policy

Player-facing labels use the plain descriptors already established in
[PhilippineCombatPreset.cs](../../src/Hukbo.Core/Combat/PhilippineCombatPreset.cs):
**Great Blade**, **Heavy Chopper**, **Thrusting Blade**, and **Work Blade**. The
`WeaponId.Bolo` enum identity is an internal name, not a label. No cultural
weapon identification appears anywhere in this panel, per `CLAUDE.md` §7.

## 4. Where the data lives

Composition is **scenario data, not preset data**. The roster's contents, order,
and per-weapon target weights are untouched, so this does not require a new
combat preset version under `CLAUDE.md` §5. What changes is only which existing
roster entry a given warrior is assigned, which is the same category of
information as `AgentsPerFaction` and `Seed` — both of which already live on
`Scenario`.

```
Scenario.RosterCounts          per-battle instantiation data (new)
CombatRuleset.Roster           versioned, content-hashed preset data (unchanged)
RosterCountExpansion           pure function, no state (new)
AgentState.Loadout             the hashed source of truth (unchanged)
```

### The default must stay invisible

`Scenario` gains an init-only `ImmutableArray<int> RosterCounts` defaulting to
`ImmutableArray<int>.Empty`. When `RosterCounts.IsDefaultOrEmpty` is true, the
simulation takes today's round-robin path unchanged, so the recorded seed-1
oracle in [testing.md](../../development/testing.md) must not move:

| Quantity | Recorded value |
| --- | --- |
| State hash | `6EBB1EA63114F6CE` |
| Event hash | `941377BD43C556FF` |
| Outcome | `Faction1Victory` at tick 235 |

`.IsDefaultOrEmpty` is the check, not `== default` and not `.Length == 0` alone.
`default(ImmutableArray<int>)` and `ImmutableArray<int>.Empty` are different
values under `==`, and treating them differently would make an explicitly-empty
scenario fail the sum check that a compiler-default scenario skips.

### The equality trap

`Scenario` is a positional record, so the compiler synthesises `Equals` and
`GetHashCode` across all instance auto-properties — including the new one.
`ImmutableArray<T>.Equals` compares the **underlying array by reference**. Two
scenarios built independently with identical composition would therefore compare
unequal. `Scenario` must override `Equals(Scenario?)` and `GetHashCode()`
manually and compare `RosterCounts` element-wise. This is silent breakage, not a
compiler error, so it carries a dedicated regression test.

### Expansion is per-faction, not per-entity

`ResolveLoadout(entityId)` uses a **global, continuous** index: faction 1's first
warrior is `entityId = AgentsPerFaction + 1`, so its category offset continues
from wherever faction 0 stopped. A composition whose counts sum to
`AgentsPerFaction` describes one faction, so expansion must be indexed by the
**faction-local** loop variable already in scope in both spawn loops of
[BattleSimulation.Create](../../src/Hukbo.Core/Simulation/BattleSimulation.cs).
Reusing the global index would silently give the two factions different armies.

A new pure file `src/Hukbo.Core/Combat/RosterCountExpansion.cs` performs the
run-length expansion: roster index 0 repeated `counts[0]` times, then index 1,
and so on. It lives there rather than in `CombatRuleset` (preset data must stay
scenario-agnostic, or preset identity starts depending on per-battle config) and
rather than in `Scenario` (a passive record whose only behaviour today is
`Validate()`).

`CreateAgent` changes its parameter from `CombatRuleset rules` to the already
resolved `CombatLoadout loadout`. The branch stays inside `Create`.

## 5. Validation

Added to `Scenario.Validate()` immediately **after** the existing
`CombatPresetRegistry.IsRegistered` check, because reading `rules.Roster.Count`
requires a registered preset first. All three checks are skipped when
`RosterCounts.IsDefaultOrEmpty`.

| Rule | Exception |
| --- | --- |
| `RosterCounts.Length == rules.Roster.Count` | `ArgumentException` — a shape mismatch, not a range violation |
| each element within `[0, AgentsPerFaction]` | `ArgumentOutOfRangeException`, via the existing `ValidateInRange` helper |
| `checked` sum equals `AgentsPerFaction` | `ArgumentException` |

`Scenario.MaximumAgentsPerFaction` (10,000) remains the single authoritative
ceiling. The Client's own limit is a convenience clamp for a responsive UI and is
never the thing that decides validity.

## 6. Client behaviour

### Units-per-team range

The evidence-backed ceiling is **250 per team**. `benchmark.ps1 -Agents 500`
routes through `Scenario.CreateDefault(seed, totalAgents: 500)`, which halves to
`AgentsPerFaction = 250`; the recorded 500-agent stress result (deterministic,
tick 309) is therefore 250-per-team evidence. Offering 500 per team in the UI
would let a spectator reach 1,000 total agents, a workload this repository has
never measured. The floor is 4 per team, the smallest total that can hold one
warrior of each category.

Category steppers move by 1, or by 10 with Shift held. The units-per-team stepper
moves by 10, or by 50 with Shift held.

### The sum constraint

Category steppers **clamp, they do not wrap** — the opposite of
[UiThemeSelector.GetRelativeId](../../src/Hukbo.Client/UI/UiThemeSelector.cs),
whose modulo wrap is correct for a theme carousel and wrong here, where wrapping
a count from its maximum back to zero would silently destroy a deliberate
allocation.

An **Unassigned: N** readout shows `unitsPerTeam - sum(categories)` and may be
negative. **Apply** is enabled only when `N == 0` and the draft differs from the
saved composition. Raising the total leaves the difference unassigned rather than
guessing which category should absorb it; no category the spectator did not touch
ever changes on its own.

**Distribute Evenly** resolves the arithmetic in one action: it splits the total
across the four categories and gives the remainder to the earliest indices. This
is exactly the distribution the round-robin already produces for the same total,
which is why **Reset to Default** is defined as the same operation rather than a
separate rule.

### Panel states

| State | Trigger | What the spectator sees |
| --- | --- | --- |
| Loaded | Panel opens | Steppers show the saved composition; Apply disabled |
| Editing | Any stepper changes | Values and the Unassigned readout update live |
| At minimum | Category at 0, or total at 4 | The `-` arrow renders in `ActionDisabled` and its glyph dims |
| At maximum | Total at 250 | The `+` arrow renders in `ActionDisabled` and its glyph dims |
| Unbalanced | `Unassigned != 0` | Readout states the surplus or shortfall in text; Apply stays disabled |
| Apply disabled | Balanced but unchanged | Apply renders in `ActionDisabled` |
| Staged | Apply pressed | Composition persists; a banner reads that it takes effect on the next Full Reset; Apply returns to disabled |
| Recovered | Saved file is malformed or from schema v1 | Steppers show the built-in defaults and a one-line notice explains the reset |

Every state is distinguished by text or glyph as well as colour; colour alone
never carries the meaning.

### Focus and input

The menu overlay gains an **Army Composition** button between *Next Round* and
*Full Reset*, so the main chain becomes theme selector, Play, Pause, Next Round,
Army Composition, Full Reset, Exit.
[MenuOverlay.ResolveFocusedControlIndex](../../src/Hukbo.Client/MenuOverlay.cs)
is parameterised by `controlCount` and needs no signature change.

The panel keeps its own chain of nine controls — four category steppers, the
units-per-team stepper, Distribute Evenly, Reset to Default, Cancel, Apply —
and **wraps** on Up/Down, reusing `ResolveFocusedControlIndex` verbatim so the
overlay's navigation feels the same throughout. Value clamping and focus wrapping
are different axes and it is deliberate that they differ.

Left and Right adjust the focused stepper's value and do not move focus. Enter
and Space activate only the four action rows; on a stepper row they do nothing,
because a stepper has no unambiguous activation. Escape behaves as Cancel:
discard the draft and return to the menu with focus on the Army Composition
button.

### Persistence

`ClientSettings` gains the composition fields and its schema version moves from 1
to 2. `ClientSettingsStore.Load` treats a version-1 file exactly as it treats any
other version mismatch today — the whole file falls back to defaults. That
matches the existing code shape, needs no merge logic, and loses nothing, since a
version-1 file has no composition data to preserve. `TrySave` grows a composition
parameter, which ripples to its `UiThemeManager` call site. The existing
atomic temp-file-then-replace write is unchanged, so a failed save still leaves
the previous valid file in place.

## 7. Standards §10 answers

1. **User-visible outcome** — a spectator sets units per team and the count of
   each of the four weapon categories, and the next Full Reset fields exactly
   that army for both factions.
2. **Tick stage and state read/written** — none. Composition is consumed once at
   `BattleSimulation.Create`, before tick 0. No tick stage reads or writes it.
3. **Numeric units, bounds, same-tick conflict** — counts are whole warriors.
   Core bounds each count to `[0, AgentsPerFaction]` and the sum to exactly
   `AgentsPerFaction`, with `AgentsPerFaction` itself bounded by
   `MaximumAgentsPerFaction`. The Client offers 4 to 250 per team. There is no
   same-tick conflict, because nothing mutates during a tick.
4. **Total ordering and random stream** — expansion is a deterministic run-length
   walk in declared roster-index order and draws no random numbers. The
   `SplitMix64` call sequence in `Create` is untouched, so spawn positions are
   unaffected.
5. **Cache** — no cache. Expansion is recomputed once per `Create` call.
6. **Save, event, version effect** — `Hukbo.Core`: none; no event changes, no
   snapshot changes, and `StateHasher` is untouched, because composition already
   reaches the state hash through each agent's `Loadout` fields. Client settings
   move to schema version 2 with a documented fallback.
7. **Complexity and workload** — `O(AgentsPerFaction)` once at creation, against
   an existing `O(AgentsPerFaction)` spawn loop. Benchmark workload is unchanged:
   200 agents, 10,000 ticks, seed 1, plus the 500-agent stress report.
8. **Spectator explanation** — the composition is set in the panel, the resulting
   equipment is already visible per warrior in the agent inspector, and the
   staged banner explains why a change has not taken effect yet. A spectator can
   discover all of it without reading source code.
9. **Tests that fail first** — listed in the plan document, phase by phase. The
   load-bearing ones are the byte-identical default-path regression, the
   element-wise `Scenario` equality test, the per-faction independence test, and
   the Apply-gate tests.

## 8. Deliberately not doing

- Per-team asymmetric composition. The request is explicitly for equal teams.
- Live application to a running battle. It would break the frozen-scenario
  contract.
- A confirmation dialog on Cancel or Reset to Default. Nothing else in the
  overlay confirms, including Full Reset and Exit; adding one here would
  introduce a pattern the rest of the UI does not have.
- Generalising the panel beyond the four roster entries. There is exactly one
  registered preset. When a second one arrives with a different roster size, that
  is the moment to generalise, not before.
