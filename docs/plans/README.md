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

**As of 2026-07-28, stage 1 (V2, plus its clash-integration follow-on) is
implemented and the game runs it. Stages 2 and 3 are design only.**

| Order | Document | Status |
| --- | --- | --- |
| 1 | [`docs/archives/2026-07-28/2026-07-27-weapon-identity-and-attributes-design.md`](../archives/2026-07-28/2026-07-27-weapon-identity-and-attributes-design.md) — preset V2 | Archived: implemented and complete |
| 1 | [`docs/archives/2026-07-28/2026-07-27-weapon-identity-and-attributes.md`](../archives/2026-07-28/2026-07-27-weapon-identity-and-attributes.md) — preset V2 task list | Archived: implemented and complete |
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

- The combat preset is `PrecolonialPhilippinesV2`. `PrecolonialPhilippinesV1`
  stays registered, frozen, and unmodified so its replays remain reproducible.
- There are four weapons, each with a Filipino pair-form player-facing label
  and a recorded evidence tier: `Kampilan — Great Blade`, `Wasay — War Axe`,
  `Kalis — Thrusting Blade`, `Itak — Work Blade`.
- Each weapon has its own damage, reach, and attack cooldown, split by grip.
  `Kampilan` and `Wasay` are two-handed (one profile each, no shield
  permitted); `Kalis` and `Itak` are one-handed (a solo profile and a
  shield-paired profile each). An accepted attack additionally resolves
  against a five-way `AttackResolution` — `Landed`, `ShieldBlocked`,
  `Parried`, `Deflected`, `Evaded` — from the weapon-clash system merged on
  top of V2, so a landed hit is no longer unconditional.
- The roster has six entries — one per weapon-and-grip combination — and the
  army composition panel has six rows.
- Shields are still `None` and `TallHardwood`, and a shield's targeting
  effect is still halving chest and abdomen targeting weight; per-shield
  attribute modification (beyond the existing paired-profile values) has not
  been built.
- There is still no attack combination and no fighter level — those are
  preset V3, design only, no plan document yet.
- Per-weapon balance (mean time to kill, per-faction win rate) has been
  measured but not tuned against; see "T32 — weapon balance measurement" in
  `docs/development/testing.md`. The attribute values above are provisional
  gameplay tuning, not settled.

## The performance hardening workstream

Two documents, per the table above:

| Document | Status |
| --- | --- |
| [`2026-07-28-arch-informed-performance-hardening-design.md`](2026-07-28-arch-informed-performance-hardening-design.md) | Design only. Does not authorize implementation. |
| [`2026-07-28-arch-informed-performance-hardening.md`](2026-07-28-arch-informed-performance-hardening.md) | The ordered task list, its verification criteria, and the Gate A verdict. |

This workstream is hash-neutral by construction: every task in the plan is
required to leave the seed-1 200-agent pair unchanged, and it is unchanged —
`stateHash 71211929A44A16CA`, `eventHash A2DC3ECA3F7345ED`. **No ECS, archetype
system, or chunk system was adopted, and no package was added.** `CLAUDE.md`
section 9's prohibition on a general-purpose ECS before a profiler demands one
stands exactly as it did before this workstream started.

What it delivered:

- A `Hukbo.Core`-only per-tick allocation figure, separated from the
  whole-loop harness total the headless runner already reported.
- A four-point agent-count scaling curve — 200, 500, 1,000, and 2,000 agents
  at seed 1 — recorded in `docs/development/testing.md`.
- A per-stage tick profile in
  [`docs/research/TICK-STAGE-PROFILE.md`](../research/TICK-STAGE-PROFILE.md),
  sampled from the unmodified Release headless seed-1 workload.
- Removal of per-tick allocations that were visible in the source without that
  profile.
- One previously undocumented invariant, the no-copy contract on
  `CollisionResolver.Grow<T>`, written down at the symbol and pinned by a
  test.
- A performance technique inventory added to `SIMULATION-GAME-STANDARDS.md`
  section 15, recording which of the techniques an external research pass
  found in the Arch library are usable in Hukbo as-is, which need a named
  discipline, and which are forbidden outright.

The plan's Gate A closed three of the four structural candidates it gated —
spatial acceleration for target selection, a dense identifier-to-index map in
place of `Dictionary<ulong,int>`, and the `AgentState` memory-layout question —
and authorized the fourth, an axis-delta rejection ahead of the existing
squared-distance check in target selection. The same stage profile that decided
those four verdicts points at collision resolution as the next candidate for
attention; that stage is explicitly out of scope for this plan and needs its own
design document before anyone touches it.

## Where the live contract lives

| Question | Source |
| --- | --- |
| How agents work in this repo | `CLAUDE.md` |
| Naming and logging, for non-Claude agents | `AGENTS.md` |
| Determinism, tick order, reviewer checklist | `SIMULATION-GAME-STANDARDS.md` |
| Verification and evidence | `docs/development/testing.md` |
| Task procedures | `.claude/skills/` |
| Why something was built this way | `docs/archives/` |
