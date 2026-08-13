# Lethal blow legibility: design

Date: 2026-08-13
Status: proposed. This document does not authorize implementation; its plan,
`2026-08-13-lethal-blow-legibility.md`, does.

## 1. What prompted this

A person at an interactive desktop ran the tactical hit animations smoke family,
rows 90 through 98, on 2026-08-13. Eight of the nine rows passed. Row 92, "Tell
a lethal hit apart", did not close. The tester's words were: *"it's not
extremely clear, we need improve this so i can really see, more blood and
gore"*.

Row 92 asks whether a killing blow reads as clearly heavier than an ordinary
one. It passed the letter of its expected observation — a lethal blow does draw
two rings instead of one, and its shards are longer — while failing the thing
the row exists to prove, which is that a spectator can tell a kill from a graze
without being told to look for one.

This document is the diagnosis and the proposed change. The row stays open until
the same person re-runs it against the change.

## 2. Why a kill is hard to see today

Four separate mechanisms are involved, and three of them work against the
tester rather than for them. All four were confirmed by reading the code.

**The pawn is gone before most of the effect plays.** A dead agent is skipped by
the pawn draw loop unless a lethal hold is active
(`src/Hukbo.Client/ArenaGame.Rendering.cs`), and that hold is
`AttackAnimation.LethalHoldSeconds`, currently `0.10f`. The lethal ring lives
`0.28f` seconds, the lethal blood burst `0.42f`, and a lethal spurt, when one
exists at all, `0.85f`. So for roughly two thirds of the ring's life, four
fifths of the burst's, and almost the whole of a spurt's, the effect is drawing
over bare ground with no body under it. The most expensive frames of a kill are
spent on an empty patch of arena, which reads as scenery rather than as a death.

**The one cue that names the victim is switched off for kills.** An ordinary hit
pulses the struck pawn (`HitEffectSystem`, `PulseSeconds = 0.09f`). That pulse
is the only part of the effect anchored to a specific body. Lethal effects are
excluded from it. The reasoning is sound in isolation — there is soon no pawn to
pulse — but combined with the short hold above it means a killing blow is the
one blow that never marks its target.

**The margin between lethal and ordinary is narrower than it looks in source.**
Written out, lethal doubles the ring count and nearly doubles ring travel, 18
against 11. On screen those numbers pass through `apparentScale`, which is
clamped between `0.72` and `2.40`, and both tiers are drawn in near-identical
warm whites — `Color.White` for lethal against `(255, 244, 214)` for ordinary,
a difference of eleven units in one channel. Two rings of similar colour,
growing for a quarter of a second, over an area a couple of pawn-widths across,
is a small signal inside a two-hundred-agent melee.

**The gore that would carry the difference is off by default.** The shipped
default is `GoreIntensity.Stylized` (`ClientSettingsStore.DefaultGoreIntensity`).
`LethalSpurt` is constructed only when the level is `Full`
(`BloodEffectSystem`, gated on `isLethal && isDense`), and dense, longer-lived
ground marks likewise. The default presentation of a kill therefore has no
sustained blood at all — only a burst that outlives its own corpse by a third of
a second and a stain a little over one and a half times the ordinary radius.

## 3. What this change does

Four moves, in the order they matter.

**3.1 Keep the body under the blow.** Raise `LethalHoldSeconds` from `0.10f` so
that the pawn is still drawn while the ring and the burst are at their loudest.
The hold is already documented as provisional presentation timing. It does not
introduce a corpse layer and must not become one: the pawn is still removed, a
few tenths of a second later than before, and nothing persists.

**3.2 Give a kill its own pulse.** Lethal effects gain a pulse rather than being
excluded from one, with its own duration matched to the new hold rather than
reusing the ordinary `0.09f`. This is what marks *which* pawn died. It is
bounded by the hold: once the pawn stops being drawn there is nothing to pulse
and the pulse must already be over.

**3.3 Widen the lethal tier of the hit effect.** Longer lethal lifetime, larger
and thicker rings, more and longer shards, and a colour separation wide enough
to survive the warm palette. The point is not bigger numbers; it is that the two
tiers stop being neighbours on every axis at once.

**3.4 Make the default gore level `Full`.** This is what the tester asked for in
as many words, and it is the single change that puts sustained blood and dense,
long-lived ground marks into the shipped presentation of a kill. On top of it,
the lethal tier of the blood geometry itself is widened — droplet count, spray
reach, stain radius and stain opacity — so that even a spectator who turns gore
back down to `Stylized` gets a heavier kill than they get today.

## 4. The constraint this change deliberately reverses

`LethalSpurt`'s own doc comment states why the spurt is not in the default:

> Created only at `GoreIntensity.Full`; the Stylized default never produces one,
> because a sustained spurt carries an anatomical reading the evidence does not
> support.

Section 3.4 makes that spurt part of what a spectator sees on a fresh install.
That is a deliberate reversal of an evidence-based restraint, made because the
person the presentation exists for asked for it explicitly after watching the
current version.

Three things keep the reversal honest, and all three are binding on the plan:

1. **Nothing about the simulation changes.** A spurt is a client-side record
   with no authoritative counterpart. No event, no state field, no hash.
2. **The restraint is recorded, not deleted.** The doc comment is rewritten to
   say that the spurt is now in the default level and why, rather than being
   quietly removed. A later reader must be able to find the reasoning that used
   to hold and the decision that overrode it.
3. **It stays one constant.** `Off`, `Stylized`, and `Full` all keep their
   current numeric values and their current meanings, so the only thing that
   moved is which one a spectator gets when they have never chosen. Anyone who
   wants the old presentation selects `Stylized` in the menu, and reverting the
   decision is a one-line change to `DefaultGoreIntensity`.

Section 3.3's colour and size values, and section 3.1's hold, are provisional
gameplay-legibility tuning in the sense `CLAUDE.md` §7 uses the word. They are
not historical measurements and their comments must say so.

## 5. What this change must not do

- **No corpse layer.** The hold gets longer; it does not become permanent, and
  no new record outlives the effects that already exist.
- **No new buffer, no new cache, no unbounded growth.** The hit-effect buffer
  and the three blood buffers keep their existing fixed capacities and their
  existing replace-oldest overflow policy.
- **No reach into the simulation.** Everything here lives in `Hukbo.Client`.
  `Hukbo.Core` is not touched, no event is added or reinterpreted, and neither
  the state hash nor the event hash may move. Row 98 of the smoke family, which
  a person has already passed, is the interactive half of that guarantee and
  must not be invalidated.
- **No per-effect cost that scales with agent count.** Droplet and shard counts
  stay hard-capped per record. The caps rise; they do not disappear.

## 6. Cost, and the row it puts at risk

Raising `MaximumDropletCount`, the lethal droplet bonus, the shard count, and
the default gore level all raise the number of primitives drawn per kill. There
is no stated total-screen quad ceiling anywhere in the effect code — only the
per-record caps and two `LowDetailScale` throttles, at `0.95` for hit effects
and `0.76` for blood.

The honest consequence: **row 94, "Watch a crowded exchange", passed on
2026-08-13 against the current values and cannot be assumed to hold against
these.** It has to be re-run. This document does not get to declare it still
passing, and neither does any test. The plan carries it as an explicit
re-verification, not as a footnote.

## 7. The nine questions, `SIMULATION-GAME-STANDARDS.md` §10

1. **User-visible outcome.** A killing blow is distinguishable from an ordinary
   one at a glance, at every zoom the spectator can reach, without knowing in
   advance where to look.
2. **Tick stage and state read/written.** None. No tick stage is involved. The
   change reads `AgentView` and the dispatcher-owned contact bundle, both of
   which the client already holds, and writes only client-side effect buffers.
3. **Numeric units and bounds.** Presentation seconds and world units, all
   bounded: lifetimes are fixed constants, droplet and shard counts are clamped
   to their maxima, and `apparentScale` keeps its existing clamp. No same-tick
   conflict exists because no authoritative state is written.
4. **Total ordering and random stream.** Unchanged. Effect records keep their
   existing ordering by `Sequence`, and the geometry seed remains a pure
   function of `Sequence` and the target entity id. No new randomness, and
   nothing consumes the simulation's stream.
5. **Cache.** No cache. The existing fixed-capacity buffers keep their existing
   replace-oldest policy.
6. **Save, event, and version effect.** Presentation only, with one persisted
   field touched: `GoreIntensity`'s default when the settings file has no value.
   The enum's numeric values do not move, so an existing settings file keeps
   resolving to exactly the level it resolved to before.
7. **Worst-case complexity and benchmark workload.** Unchanged in order: cost is
   linear in effects alive, each with a capped primitive count. The canonical
   200-agent, 10,000-tick, seed-1 headless workload does not render and is
   therefore unaffected; the real cost check is the interactive re-run of row
   94.
8. **Spectator explanation.** The effect is itself the explanation, which is the
   whole point of the row. The gore level remains discoverable and changeable
   from the menu overlay's existing selector.
9. **Tests that fail before and pass after.** The pinned tuning tests listed in
   the plan fail on the old values and pass on the new ones, plus new tests for
   the lethal pulse and for the default gore level. None of them can prove row
   92; only the tester can.

## 8. What decides success

Row 92 is re-run by the person who reported it, and they say a kill is
unmistakable. Row 94 is re-run and the crowded exchange stays legible and
bounded. Nothing else counts, including a green gate.
