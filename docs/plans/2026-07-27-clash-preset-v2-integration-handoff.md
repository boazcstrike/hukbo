# Weapon clash on preset V2 — session handoff

Date: 2026-07-27
Design: [2026-07-27-clash-preset-v2-integration-design.md](2026-07-27-clash-preset-v2-integration-design.md)
Plan: [2026-07-27-clash-preset-v2-integration.md](2026-07-27-clash-preset-v2-integration.md)

Written so the next agent can continue without reconstructing anything. Read
the design document first; this file assumes decisions D1 through D8 and does
not restate their reasoning.

## 1. Where this stands in one paragraph

The weapon-clash branch is merged into `clash-integration` and every conflict is
resolved. All production code compiles. Only test call sites still fail. **The
clash feature is currently inert:** preset V1 no longer carries the tables and
preset V2 does not carry them yet, so nothing resolves a clash and every attack
lands. That is the expected state between Phase 1 and Phase 2, not a defect, but
it means no measurement taken right now means anything.

## 2. Working state

| Item | Value |
| --- | --- |
| Branch | `clash-integration` |
| Worktree | `.claude/worktrees/clash-integration` |
| Based on | `main` at `de19c57` |
| Merged in | `worktree-weapon-clash` at `3cd4bc6` |
| True merge base | `2d88b43` — **not** `7abf8fc`, see design section 1 |
| Working tree | clean |
| Builds | `src/*` yes; `tests/*` no |

Commits added on top of the merged history:

| Commit | Task | What |
| --- | --- | --- |
| `11c1760` | — | Design and plan documents |
| `c8e1492` | T01 | The merge, all eleven conflicts resolved |
| `b41312f` | T02 | Rename sweep onto the V2 weapon names |
| `e72f07d` | T20 | Preset V1 frozen without a clash profile |

The `worktree-weapon-clash` branch ref still exists and its worktree was removed
this session. Its content is still readable with
`git show worktree-weapon-clash:<path>`.

## 3. Task status

Phase and task IDs are from the plan document.

| Task | Status | Note |
| --- | --- | --- |
| T01 | **done** | `c8e1492` |
| T02 | **done** | `b41312f`, zero old symbols remain in `src`, `tests`, `tools` |
| T03 | **done** | The error list is reproduced in section 5 below |
| T10 | **not started** | The D3 re-key. This is the next task. |
| T11 | **not started** | Roster-driven validation, `Neutral` rebuilt |
| T12 | **done** | Landed inside `c8e1492`; both trailing parameters present |
| T13 | **done** | Landed inside `c8e1492`; the fold is conditional on `_declaredClashProfile` |
| T14 | **not started** | Needs T10's key shape first |
| T15 | **done** | Landed inside `c8e1492`; `ResolutionShift = 24` |
| T16 | **partly done** | The call shape is merged and compiles. The `ClashResolver.Resolve` call, the widened proposal tuple, the `Landed`-conditional damage, and metrics accumulation all came across from the clash branch and are present. Re-read it against the merged ruleset before trusting it. |
| T17 | **needs checking** | Both overloads merged cleanly and compile; nobody has verified the control-run seam still does what it claims |
| T18 | **needs checking** | Came across from the clash branch; confirm the resolution byte still reaches the event hash after the packing change in T15 |
| T19 | **needs checking** | Same |
| T20 | **done** | `e72f07d` |
| T21 | **not started** | The 32 values are reproduced verbatim in section 4 |
| T22 | **not started** | The ten new cells |
| T23 | **not started** | |
| T30 | **done** | Landed inside `c8e1492` |
| T31, T32 | **needs checking** | Files came across from the clash branch; not re-read against the merged event |
| T35, T36, T37 | **done** | Landed inside `c8e1492`; the shield is deliberately left unposed |
| T40 – T54 | **not started** | Section 5 lists what already fails to compile |
| T60 – T66 | **not started** | |

## 4. The preset V1 clash tables, verbatim

`T20` deleted these from `PhilippineCombatPreset.cs`. They are recoverable with
`git show b41312f:src/Hukbo.Core/Combat/PhilippineCombatPreset.cs`, and they are
reproduced here so T21 does not need git archaeology.

Every value below is **Provisional reconstruction**. The type's own remarks state
that all sixteen intercept cells have no evidentiary confidence and only their
relative ordering is argued, weakly.

Weapon intercept, basis points out of 10,000, defender against attacker:

| Defender | vs Kampilan | vs Wasay | vs Kalis | vs Itak |
| --- | --- | --- | --- | --- |
| Kampilan | 2,200 | 1,900 | 1,600 | 2,000 |
| Wasay | 1,500 | 1,300 | 1,100 | 1,400 |
| Kalis | 500 | 400 | 600 | 600 |
| Itak | 400 | 300 | 500 | 500 |

Per-weapon rows:

| Weapon | voidChannel | hardShareBase (as attacker) | hardShareMultiplier (as defender) |
| --- | --- | --- | --- |
| Kampilan | 1,000 | 3,300 | 1,150 |
| Wasay | 900 | 4,000 | 1,050 |
| Kalis | 1,000 | 1,200 | 750 |
| Itak | 1,100 | 1,800 | 700 |

Scalars:

| Name | Value | Note |
| --- | --- | --- |
| `shieldIntercept` | 2,400 | Flat across every attacker. The research states the per-attacker spread it suggests has no source behind it. |
| `minimumHardShareBasisPoints` | 500 | Guard only; does not bind. The hard-share product spans 840 to 4,600. |
| `maximumHardShareBasisPoints` | 6,000 | Guard only. |
| `maximumInterceptionBasisPoints` | 5,500 | Guard only. Largest total these tables produce is 4,000, so the rescale branch is unreachable in production. |

**The critical reading.** The Kalis and Itak rows are low — 300 to 600 — *because
under the V1 roster those two weapons always carried a `TallHardwood` shield and
the flat 2,400 shield channel was doing their defensive work*. Carry these
sixteen cells onto the **shielded** defender keys only. The Kampilan and Wasay
rows were authored for a shieldless defender and carry onto the bare keys. The
eight shieldless Kalis and Itak cells do not exist yet and are T22.

## 5. Exactly what fails to compile

Production code: nothing. `dotnet build src/Hukbo.Core/Hukbo.Core.csproj -c Debug`
is clean, and so is the whole of `src`.

Tests, all one root cause — clash-branch call sites pass
`weapon, hitLocation, resolution` and the merged factory wants
`weapon, shield, hitLocation, resolution`:

| File | Lines |
| --- | --- |
| `tests/Hukbo.Core.Tests/BattleEventTests.cs` | 89, 118, 135 |
| `tests/Hukbo.Core.Tests/HeadlessRunnerTests.cs` | 284 |
| `tests/Hukbo.Client.Tests/BattleEventFormatterTests.cs` | 163, 208, 225 |
| `tests/Hukbo.Client.Tests/ClashEffectSystemTests.cs` | 170 |
| `tests/Hukbo.Client.Tests/SwingAnimationSystemTests.cs` | 184 |
| `tests/Hukbo.Client.Tests/SwingPoseResolverTests.cs` | 129 |

Separately, `tests/Hukbo.Client.Tests/PawnGeometryTests.cs` at 145, 184, 225, and
262 calls `PawnAppearanceFactory.Create` without the `shield` argument preset V2
added.

The fix is mechanical — insert the shield argument — but note that the five files
the design flagged as *silently* auto-merging are a different set from these.
Those five already carry `main`'s shield-bearing shape and will compile without
being touched; they are wrong only in that they never exercise a non-`Landed`
resolution. T54 covers giving them one.

## 6. Traps

Three already bit this session and were fixed. They are recorded because each one
is the kind that comes back.

1. **Positional arguments into the wrong optional slot.** `CombatRuleset` now
   ends with two optional parameters, `weaponAttributes` then `clashProfile`.
   `WithClashProfile` passed its profile positionally and would have landed it in
   the attribute slot, returning a copy that had silently lost every per-weapon
   damage, reach, and cooldown value. Both trailing arguments are now passed by
   name everywhere. **Pass them by name in any new call site.**
2. **A rename sweep that matches documentation.** The T02 sweep was first run
   with `docs` in its path list and rewrote prose in thirteen files, including
   both of this feature's own plan documents and
   `docs/research/WEAPON_CLASH_1500s.md`, producing sentences like "renamed
   `Kampilan` to `Kampilan`". It was reverted with `git checkout -- docs` and
   re-run scoped to `src tests tools`. **Documentation legitimately contains the
   old names where it records what a past branch did. Never sweep `docs`.**
3. **Reasoning from the wrong merge base.** `7abf8fc` is titled "Merge main into
   the weapon-clash worktree" and is not an ancestor of `main`. Using it as the
   base makes `AttackResolution` look pre-existing and hides that both branches
   independently extended a two-field `BattleEvent`. The base is `2d88b43`.

One that has not bitten yet:

4. **`ValidateMatrix` demands an enum cross-product.** It requires exactly
   `Enum.GetValues<WeaponId>().Length` squared cells. Under D3's key that is
   wrong in both directions — it would demand a cell for a two-handed Kampilan
   carrying a shield, which is not a legal loadout. T11 moves this to
   range-and-consistency validation and leaves roster coverage where it already
   lives, in `CombatRuleset.ValidateClashProfileCoversTheRoster`, which is the
   only place that knows the roster.

## 7. The next three tasks, in order

**T10 — re-key `ClashProfile`.** `_weaponIntercept` becomes
`(WeaponId Defender, ShieldId DefenderShield, WeaponId Attacker)`, `_voidChannel`
becomes `(WeaponId, ShieldId)`. Leave `_hardShareBases`, `_hardShareMultipliers`,
and `ShieldInterceptBasisPoints` exactly as they are; the research supports all
three staying weapon-keyed or shield-keyed as they already are.
`ClashResolver.Resolve` already receives `defenderShield` and already folds it
into its mix, so its signature does not change — only the lookups inside it.

**T11 — validation and `Neutral`.** As in trap 4 above.

**T21 and T22 — the tables.** Carry section 4's sixteen cells onto V2 per the
critical reading, then author the ten new cells: four weapon-intercept cells for
shieldless Kalis, four for shieldless Itak, two void entries. Bands from design
section 5 — weapon intercept roughly 0.10 to 0.18, void roughly 0.11 to 0.19.
Every new value carries a code comment naming its band and the label
**Provisional reconstruction**. None is presented as a measurement.

## 8. What finished looks like

The nine criteria at the end of the plan document. The two that are easiest to
get wrong:

- **V1's content hash must still be `0x59FB4CA563D87A49`.** It is a regression
  guard, not a re-baseline target. If it moves, D2's conditional fold is broken.
- **The 20-seed defence-attributable share is a gate, not a report.** It must
  land inside 0.25 to 0.45. The clash branch measured 0.3137 to 0.3478 against
  the four-loadout roster; that figure does not carry over, because two
  shieldless loadouts entering the roster changes the mix on its own before any
  new cell value is considered.

And the rule that outranks both: `./scripts/verify.ps1` is run once, by the
integrating agent, and its real output is the evidence. No sub-agent report
substitutes for it, and no agent may flip a manual smoke row in
`docs/development/testing.md`.
