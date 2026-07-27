# Sound Gain Compensation — Design

Date: 2026-07-27
Status: Design. This document does not authorize implementation on its own; the
companion plan is `2026-07-27-sound-gain-compensation.md`.

Evidence: [`docs/research/SOUND-CAPACITY-MEASUREMENTS.md`](../research/SOUND-CAPACITY-MEASUREMENTS.md).
Every number quoted below is measured there, not estimated here.

## 1. Problem

The game overloads its own audio output. At 200 agents and normal speed the
summed mix peaks 7.7 dB above digital full scale and 5 385 samples are flattened
by the output stage. At 500 agents and 4x speed it is 10.3 dB over with 43 750
flattened samples. Flattened peaks stop reading as separate blows and start
reading as a continuous distorted mass.

The cause is that every cue plays at a fixed gain of 0.8 — about 1.9 dB below
full scale — with no account taken of how many other cues are already sounding.
Summing N uncorrelated signals raises the level by roughly 10·log₁₀(N) dB, and
the mix routinely carries 29 to 113 simultaneous voices.

Two things the measurements ruled out, so that this design does not chase them:

- **The rate limit is not the problem.** `SoundCueBudget` discards only 1.6 to
  5.6 percent of cues.
- **The device is not the problem.** The backend ceiling is 256 voices, it does
  not leak, and audio CPU cost is under 0.51 ms per frame even at 25 times the
  rate this game produces.

## 2. Scope

In scope:

- Per-cue gain that scales with the number of voices currently sounding.
- Voice bookkeeping in the sound director so that count exists.
- Raising the frame budget from a throttle to a safety ceiling.
- Handling the `bool` that `SoundEffect.Play` returns and currently ignores.
- Showing the live voice count and applied gain in the sound panel.
- Client tests for every pure part of the above.

Out of scope, deliberately:

- **Clip length, weight, and cue density.** The owner separately observed that
  the attack clips are short — several under 100 ms, the shortest 49 ms — and
  fire roughly every 25 ms. That is a real and measured problem, and it is a
  content and pacing problem that no gain change addresses. It is not in this
  design and must not be smuggled into it.
- The seven clips that are exactly 480 ms long and look generation-capped.
- An output bus limiter. The 5 ms feed-forward limiter that was measured
  contributed nothing, and gain compensation alone removes the overload.
- Music, ambience, spatialisation, volume sliders, persisted audio settings.
- Any change to `Hukbo.Core`, to `Hukbo.Headless`, or to `ClientSettings`.

## 3. Design

### 3.1 Where the correction goes

`SoundDirector` already decides what plays. It gains a voice ledger and applies
the correction immediately before handing a cue to the player. `ISoundPlayer`
gains one query so the director can know how long a cue occupies a voice.

```
BattleEvent  ->  SoundCueMapper  ->  budget  ->  gain correction  ->  ISoundPlayer.Play
                                                       ^
                                                  voice ledger
```

`MonoGameSoundPlayer` remains the only file that touches MonoGame audio types.
Everything above the interface stays pure and directly constructible in
`Hukbo.Client.Tests`.

### 3.2 The voice ledger

The director keeps, for each cue it has started, the presentation time at which
that cue's clip ends. Before each new cue it drops the entries that have expired
and counts what remains.

The director must not read a clock. `BeginFrame` takes the frame's elapsed
seconds, which `ArenaGame` already has from `GameTime`, and advances an internal
presentation clock. This keeps the director testable without a wall clock and
keeps time-dependence explicit rather than ambient.

Playback speed needs no special handling: a clip occupies a real voice for a
real duration regardless of how fast the simulation is advancing, and the
presentation clock is real time. At 4x the same wall second carries four times
the cues, the ledger naturally holds more of them, and the correction gets
correspondingly stronger — which is exactly the measured requirement.

The ledger is bounded by the frame budget, so it cannot grow without limit.

### 3.3 The correction

```
gain = CueVolume / sqrt(soundingVoices + 1)
```

Square root, not linear division, because impacts from different agents are
largely uncorrelated. This is the standard correction for summing uncorrelated
material, and section 7 of the evidence document measures it removing the
overload entirely in every configuration tested — peak between −4.3 and
+1.6 dBFS, with zero flattened samples except two out of roughly six million at
500 agents and 1x.

Deliberately no floor on the resulting gain. A floor would reintroduce exactly
the overload this exists to remove. At the worst measured load, 113 voices, the
gain lands near 0.06 — quiet per cue, which is correct, because 113 things are
sounding at once.

`CueVolume` moves from 0.8 to **0.65**. This was not planned; it came out of
measuring the implemented policy. At 0.8 the correction fixed three of the four
tested configurations but left 500 agents at normal speed peaking at +1.6 dBFS
with two flattened samples, which fails this plan's own acceptance criterion of
at or below 0.0 dBFS. At 0.65 every configuration passes, from −6.1 to
−0.2 dBFS with nothing flattened. The cost is that everything is about 1.8 dB
quieter than before.

`CueVolume` and the square-root exponent remain provisional tuning values and
are marked as such in code, matching how the existing constants are marked.

### 3.4 The budget becomes a ceiling

The current caps of 3 per slot and 8 total were set to protect the device from
an event volume that the measurements show does not occur. They stay, because a
hard ceiling is still the right defence against a pathological scenario, but
they move to values that sit above real demand rather than inside it.

Uncapped demand peaks at 15 cues in a frame at 200 agents and 21 at 500. The
proposed ceiling is **16 per slot and 64 total**, which is above every measured
peak with room to spare, and still far below the backend's 256.

At those values the measured suppression is zero, so in practice the ceiling
never fires. That is the intent: it is a backstop, not a throttle. A cue it does
drop is still logged as `Suppressed`, so the sound panel shows it.

### 3.5 The ignored return value

`SoundEffect.Play` returns `false` when MonoGame's instance pool is exhausted;
the OpenAL layer beneath it throws `InstancePlayLimitException` when sources run
out. `MonoGameSoundPlayer` catches the exception and ignores the `bool`, so one
of the two ways a cue can fail is currently invisible.

`ISoundPlayer.Play` returns a `bool`, and the director records a refused cue in
the log rather than reporting it as `Played`. This adds a `Refused` status,
which is the honest outcome and is what would have made the original
investigation shorter.

### 3.6 Spectator explanation

Requirement 8 of the standards, and the "can a spectator discover this without
reading source" test, are met in the sound panel:

- The header gains `VOICES n` and `GAIN 0.nn`, both live.
- The new `Refused` status appears in the cue log with the same collapsing
  behaviour as the existing statuses.

A spectator watching a busy fight sees the voice count climb and the gain fall,
which explains the loudness change on screen instead of leaving it as an
unexplained artefact.

## 4. The nine acceptance answers

1. **User-visible outcome.** Busy fights stop distorting. Every cue the battle
   emits is heard, and the mix stays under full scale.
2. **Tick stage and state read/written.** No tick stage. Presentation only; the
   director reads the per-tick event buffer and writes only its own log and
   ledger.
3. **Numeric units and bounds.** Gain is dimensionless in [0, 0.8]. The ledger
   holds presentation seconds. Voice count is bounded by the 64-cue frame
   ceiling. Same-tick conflicts do not arise — cues are processed in emission
   order and each takes the ledger state left by the one before it.
4. **Total ordering and random stream.** Cues are consumed in `Sequence` order,
   unchanged. Variant selection keeps using `SoundVariantSelector` and its
   existing `SplitMix64` draw. No new random stream.
5. **Cache.** No cache. The ledger is live state that expires by presentation
   time and is cleared on reset.
6. **Save, event, version effect.** Presentation only. `Hukbo.Core` untouched,
   both hashes untouched, nothing persisted, `ClientSettings` stays at schema
   version 1.
7. **Worst-case complexity and benchmark.** Per cue, one expiry sweep of a list
   bounded at 64, so O(64) worst case per cue and O(1) amortised. Verified by
   the canonical 200-agent, 10 000-tick, seed-1 gate plus a reported 500-agent
   run.
8. **Spectator explanation.** `VOICES` and `GAIN` in the sound panel header, and
   the `Refused` cue status. Section 3.6.
9. **Tests that fail before and pass after.** Section 5.

## 5. Testability

Everything except `MonoGameSoundPlayer` stays free of MonoGame audio types.

New or changed tests, all of which fail against the current code:

- `SoundVoiceLedger` — expiry by presentation time, count correctness, clearing,
  and that it never exceeds the ceiling.
- `SoundDirector` — gain falls as voices accumulate; gain returns to `CueVolume`
  once the ledger drains; the recording fake player receives the corrected gain,
  not the raw one.
- `SoundDirector` — a player that refuses playback produces a `Refused` row and
  no `Played` row.
- `SoundCueBudget` — the new ceiling values, at and past both limits.
- `SoundLogPanel` — the header renders the voice count and gain, and the
  `Refused` status renders and collapses like the others.

`MonoGameSoundPlayer` is still constructed only from `LoadContent`, so no test
and no headless run opens an audio device.

The measured proof is reproducible outside the test suite:
`tools/Hukbo.Tools.MixAnalysis` renders the before and after mixes and reports
peak level and flattened-sample count for each.

## 6. Rejected alternatives

- **Just raise the cue cap.** Measured: it makes the overload worse, moving the
  peak from +7.7 to +10.6 dBFS at 200 agents and 1x. More cues into an already
  overloaded bus is more overload.
- **A bus limiter instead of gain correction.** The 5 ms feed-forward limiter
  that was measured did nothing at all — identical numbers across all eight
  runs — because it cannot catch an impact transient without look-ahead. A
  look-ahead limiter would mean buffering the output, which MonoGame's
  fire-and-forget API does not expose.
- **Lowering `CueVolume` to a fixed smaller value.** Would fix the peak only by
  making quiet moments inaudible, since the overload is load-dependent and a
  fixed value cannot be right at both one voice and 113.
- **Normalising the source files instead.** Same objection: the problem is how
  many play together, not how loud each one is on its own.
- **Doing this in `Hukbo.Core`.** Presentation concern; would put wall-clock
  dependence into the authoritative layer and risk the hashes.

## 7. Risks

| Risk | Mitigation |
| --- | --- |
| Square root over-corrects and busy fights feel too quiet | The exponent is a marked provisional constant; `tools/Hukbo.Tools.MixAnalysis` can render alternatives for comparison before changing code |
| The ledger's presentation clock drifts from real playback | Clip durations come from the player, which reads them from the loaded `SoundEffect`, so the ledger uses the real length rather than an assumed one |
| Raising the ceiling exposes a scenario that reaches 256 voices | The ceiling of 64 cues per frame is well under 256, and a refusal is now logged rather than silently swallowed |
| Gain becoming visible in the panel implies it is authoritative | It is presentation state and is documented as such; nothing about it is persisted or hashed |
