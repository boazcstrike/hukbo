# Plans — active work

Everything in this folder is **live**. Finished plans move to
[`docs/archives/`](../archives/README.md) with an "Archived: reference only"
banner, so a document still sitting here has not been retired.

"Live" does not mean "implemented." A design document in this folder may be
approved, argued over, and complete while no line of code exists for it. Check
the `Status:` line at the top of a design before assuming the game behaves the
way it describes.

Two document kinds, per `CLAUDE.md` section 6:

| Suffix | What it is | Authorizes code? |
| --- | --- | --- |
| `-design.md` | The reasoning, the alternatives, the rejected options | **No** |
| no suffix | The ordered task list and verification criteria | Yes |

## The combat preset chain

Four documents describe one dependent sequence of changes to
`Hukbo.Core.Combat`. They are the most likely thing in this folder to be
misread, because each one describes a game that does not exist yet, and each
depends on the one before it.

**As of 2026-07-27, none of them are implemented. The game runs preset V1.**

| Order | Document | Status |
| --- | --- | --- |
| 1 | [`2026-07-27-weapon-identity-and-attributes-design.md`](2026-07-27-weapon-identity-and-attributes-design.md) — preset V2 | Design complete |
| 1 | [`2026-07-27-weapon-identity-and-attributes.md`](2026-07-27-weapon-identity-and-attributes.md) — preset V2 task list | Plan complete, no code |
| 2 | [`2026-07-27-combat-preset-v3-combos-design.md`](2026-07-27-combat-preset-v3-combos-design.md) — preset V3 | Design complete, no plan document |
| 3 | [`SHIELDS.md`](SHIELDS.md) — shields as a stat-variant layer | Design complete, no plan document |

What each stage does:

- **V2** renames the four weapons to Filipino pair-form labels and gives every
  weapon its own damage, reach, and cooldown, split by grip — a one-handed
  weapon fought solo is mechanically distinct from the same weapon fought with a
  shield.
- **V3** adds attack combinations: a chance to open a chain, a chance to
  continue it, and a maximum length governed by a placeholder fighter level.
- **Shields** replaces the current flat targeting multiplier with a per-shield
  modification of a weapon's paired profile.

The order is not a preference. V3 builds on the `WeaponProfile` record that V2
introduces, and the shield work modifies the paired profiles that V2 authors and
the combination fields that V3 adds. Implementing them out of order means
rewriting the same three files in `Hukbo.Core/Combat` three times.

### What is true of the game today

So that nobody reads the four documents above and reports the current build
incorrectly:

- The combat preset is `PrecolonialPhilippinesV1`, and it is the only registered
  preset.
- There are four weapons. Player-facing labels are the plain descriptors
  `Great Blade`, `Heavy Chopper`, `Thrusting Blade`, and `Work Blade`. No
  Filipino weapon name appears in any player-facing string.
- Weapons differ **only** in which body part they tend to hit. Damage, reach,
  and cooldown are global values on `Scenario` — 10 hit points, 12 world units,
  5 ticks — shared by every warrior on the field.
- There is no grip concept, no weapon profile, no attack combination, and no
  fighter level.
- Shields are `None` and `TallHardwood`. A shield's only effect is halving chest
  and abdomen targeting weight.
- The roster has four entries, and the army composition panel has four rows.

## Where the live contract lives

| Question | Source |
| --- | --- |
| How agents work in this repo | `CLAUDE.md` |
| Naming and logging, for non-Claude agents | `AGENTS.md` |
| Determinism, tick order, reviewer checklist | `SIMULATION-GAME-STANDARDS.md` |
| Verification and evidence | `docs/development/testing.md` |
| Task procedures | `.claude/skills/` |
| Why something was built this way | `docs/archives/` |
