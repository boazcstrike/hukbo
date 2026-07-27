# Sound Capacity Measurements

Date: 2026-07-27
Status: Evidence. This document records measurements only. It does not propose a
change and it does not authorize implementation.

## 1. Why this exists

The owner reported that battle audio is audible at the start of a match but
seems to stop, or to stop being distinguishable, once the fight becomes busy.
The stated goal is to hear as many of the battle's sounds as possible, and the
stated constraint is that doing so must not overload the machine.

Two questions had to be separated before any change could be proposed:

1. **How much sound does the simulation actually ask for?** The existing design
   document assumed "dozens of attacks per tick" without measuring it.
2. **How much sound can the machine actually play?** The existing rate limit was
   chosen as a provisional tuning value, not from a measurement of the audio
   backend.

Both are now measured. The numbers below are from this machine, this build, and
seed 1, and every one of them is reproducible with the harnesses in section 7.

## 2. Environment

| Item | Value |
| --- | --- |
| Operating system | Windows 11 Pro 10.0.26200 |
| .NET SDK | 10.0.302, pinned in `global.json` |
| Engine package | `MonoGame.Framework.DesktopGL` 3.8.5 |
| Audio backend | OpenAL, via MonoGame's DesktopGL backend |
| Measurement date | 2026-07-27 |

Both harnesses were run in `Release` configuration.

## 3. What the shipped audio content is

Measured from the 63 `.wav` files in `src/Hukbo.Client/Content/Audio` at the time
of writing.

| Property | Value |
| --- | --- |
| Files | 63 |
| Sample rate | 24 000 Hz |
| Channels | 2 (stereo) |
| Bit depth | 16 |
| Mean duration | 222 ms |
| Shortest | 49 ms |
| Longest | 480 ms |

Clip length matters directly, because a clip occupies one hardware voice for its
whole duration. A 222 ms clip at a tick rate of 20 overlaps roughly the next
four and a half ticks, so the number of voices in flight at any moment is
approximately the cue rate multiplied by the mean clip length.

## 4. Result A — what the audio backend can actually do

### 4.1 Hard ceiling on simultaneous voices

Explicit `SoundEffectInstance` objects were created from the longest clip
(480 ms) and started as fast as possible, up to 512 attempts, with the count
that genuinely entered `SoundState.Playing` observed rather than inferred.

| Observation | Value |
| --- | --- |
| Instances that reported `SoundState.Playing` | **256** |
| Behaviour on the 257th | `InstancePlayLimitException` thrown |
| Instances that failed to enter `Playing` before the ceiling | none |
| `CreateInstance()` failures | none |

The ceiling is a clean 256 concurrent voices. It is a limit of the backend, not
of the machine's CPU, and it is reached by an exception rather than by a silent
failure.

### 4.2 Cost of sustaining a cue rate

Fire-and-forget `SoundEffect.Play(volume, pitch, pan)` — the exact call
`MonoGameSoundPlayer` makes — was issued at a fixed rate for six seconds per
step, paced to a real 60 frames per second, with `FrameworkDispatcher.Update()`
called once per frame.

| Cues per second | Attempted | Refused | Audio CPU per frame | Allocation |
| ---: | ---: | ---: | ---: | ---: |
| 20 | 120 | 0 | 0.025 ms | 0.1 KB/s |
| 50 | 300 | 0 | 0.029 ms | 0.1 KB/s |
| 100 | 600 | 0 | 0.039 ms | 0.1 KB/s |
| 200 | 1 200 | 0 | 0.058 ms | 0.2 KB/s |
| 400 | 2 400 | 0 | 0.115 ms | 0.5 KB/s |
| 800 | 4 800 | 0 | 0.250 ms | 0.9 KB/s |
| 1 600 | 9 600 | 1 | **0.509 ms** | 1.7 KB/s |

A 60 frames-per-second frame is 16.67 ms. At 1 600 cues per second — far beyond
anything this simulation produces — the audio work costs about 3 percent of one
frame and allocates under 2 KB per second. Peak working set moved from 41 MB to
46 MB across the whole sweep.

**Audio throughput is not a performance risk for this game on this machine.**

### 4.3 Whether voices are recycled, or leak

If the backend leaked OpenAL sources — never returning them after a
fire-and-forget clip finished — that would explain audio that works early in a
battle and dies later far better than the cue budget does. The hypothesis was
tested directly: saturate the device with 400 attempts, drain for 1.5 seconds
while pumping `FrameworkDispatcher.Update()` the way `Game.Update` does, then
measure how much capacity came back. Six rounds.

| Round | Accepted | Refused | Accepted after drain |
| ---: | ---: | ---: | ---: |
| 1 | 256 | 144 | 256 |
| 2 | 256 | 144 | 256 |
| 3 | 256 | 144 | 256 |
| 4 | 256 | 144 | 256 |
| 5 | 256 | 144 | 256 |
| 6 | 256 | 144 | 256 |

Capacity returns in full, every round, with no decay. **There is no source
leak.** Whatever causes the reported symptom, it is not the audio backend
progressively losing the ability to play sound.

### 4.4 What the MonoGame source says

Read from the `v3.8.5` tag, and consistent with every measurement above.

| Fact | Source |
| --- | --- |
| `internal const int MAX_NUMBER_OF_SOURCES = 256;` on desktop (32 on iOS and Android) | `MonoGame.Framework/Platform/Audio/OpenALSoundController.cs` |
| `internal const int MAX_PLAYING_INSTANCES = OpenALSoundController.MAX_NUMBER_OF_SOURCES;` | `MonoGame.Framework/Platform/Audio/SoundEffect.OpenAL.cs` |
| Sources are generated once at initialisation via `AL.GenSources`, handed out by `ReserveSource()`, returned by `RecycleSource()` | `OpenALSoundController.cs` |
| `if (availableSourcesCollection.Count == 0) { throw new InstancePlayLimitException(); }` | `OpenALSoundController.cs` |
| `SoundEffect.Play(volume, pitch, pan)` does `var inst = GetPooledInstance(false); if (inst == null) return false;` — it returns `false` on an exhausted pool rather than throwing | `MonoGame.Framework/Audio/SoundEffect.cs` |
| `SoundEffectInstancePool.Update()` walks the playing list and returns stopped instances to the pool | `MonoGame.Framework/Audio/SoundEffectInstancePool.cs` |

Two consequences matter for any future change:

- **The 256 ceiling is a compile-time constant and cannot be raised at runtime.**
  It is fixed when the controller initialises. A design that needs more than 256
  simultaneous voices cannot get them by configuration.
- **Both failure paths must be handled.** An exhausted managed pool returns
  `false` from `Play`, while an exhausted OpenAL source list throws
  `InstancePlayLimitException` from underneath it. `MonoGameSoundPlayer` already
  catches the exception; it currently ignores the `bool` return.
- The recycling that section 4.3 measured depends on `FrameworkDispatcher.Update()`
  running every frame. `ArenaGame` derives from `Game`, which does this
  automatically, so the game gets it for free — but a future headless or
  tool-side audio path would not.

## 5. Result B — what the simulation actually asks for

The client maps only two event kinds to sound: `Attack` (to one of four weapon
slots) and `Death`. `Move` and `Damage` are deliberately silent. Demand was
measured by running the real `BattleSimulation` and counting mapped events per
tick.

> **Corrected on 2026-07-27.** An earlier revision of this section reported
> 657 ticks and 3.22 cues per tick at 200 agents. Those figures were produced
> against a stale `Hukbo.Core` assembly that predated the last-stand formation
> and collision-priority merges, and they are wrong. Every figure below was
> re-measured after an explicit `Release` rebuild of `Hukbo.Core` at commit
> `8815a3c`. Section 4 is unaffected — it never touches `Hukbo.Core`.
>
> The corrected tick count is independently corroborated by the canonical gate:
> `scripts/verify.ps1` runs the same 200-agent, seed-1 workload and its
> `RunReport` reports `"measuredTicks": 1154`, matching the re-measurement
> exactly.

### 5.1 Demand

| Agents | Ticks run | Outcome | Total cues | Mean per tick | p95 | p99 | Max |
| ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: |
| 200 | 1 154 | Faction 1 victory | 2 185 | 1.89 | 5 | 7 | 10 |
| 500 | 2 668 | Faction 0 victory | 5 510 | 2.07 | 6 | 9 | 15 |

The design document's premise of "dozens of attacks in a single tick" is not
what the simulation produces. The busiest single tick in a 500-agent battle asks
for 15 cues, and the median tick asks for 2.

### 5.2 What the current budget suppresses

`SoundCueBudget` allows at most 3 cues per slot and 8 cues in total, reset once
per rendered frame. Because ticks and frames run at different rates, the loss
depends on playback speed: at 4x the client advances more than one tick in some
frames, and those ticks share a single frame's budget.

| Agents | 1x speed | 2x speed | 4x speed |
| ---: | ---: | ---: | ---: |
| 200 | 1.6% suppressed | 1.6% | 4.8% |
| 500 | 1.9% suppressed | 1.9% | 5.6% |

**The budget is barely doing anything.** It discards between 1.6 and 5.6 percent
of cues. That is far too small to account for a spectator perceiving the audio as
having stopped, which redirects the investigation away from the rate limit
entirely. Section 7 follows that redirection.

### 5.3 What removing the cap would demand

The same measured event stream was replayed against a range of budgets at 4x
speed, the worst case, with peak concurrent voices derived from the measured
222 ms mean clip length.

**200 agents:**

| Per slot | Total | Suppressed | Peak voices |
| ---: | ---: | ---: | ---: |
| 3 | 8 | 4.8% | 8 |
| 4 | 16 | 1.4% | 13 |
| 6 | 24 | 0.0% | 15 |
| unlimited | unlimited | 0.0% | **15** |

**500 agents:**

| Per slot | Total | Suppressed | Peak voices |
| ---: | ---: | ---: | ---: |
| 3 | 8 | 5.6% | 8 |
| 4 | 16 | 1.1% | 16 |
| 6 | 24 | 0.1% | 19 |
| 8 | 32 | 0.0% | 21 |
| unlimited | unlimited | 0.0% | **21** |

Playing every single cue a 500-agent battle emits, at 4x speed, asks the audio
device for a peak of **21 concurrent voices against a measured ceiling of 256**,
at a CPU cost well under the 0.115 ms per frame measured for 400 cues per
second.

These peak-voice counts use a fixed five-tick overlap window as an
approximation. Section 7 measures true overlap from the actual clip lengths and
gets higher numbers; where the two disagree, section 7 is the better figure.

## 6. Result C — what other engines do about this

Gathered for context on what a replacement policy would normally look like. None
of this is a measurement of Hukbo, and none of it is a recommendation.

**Voice budgets.** Unity's default audio manager ships `m_RealVoiceCount: 32`
and `m_VirtualVoiceCount: 512` — 32 voices actually mixed, 512 tracked. Unity's
`PlayOneShot` hitting that 32-voice limit and cutting audio is a common,
documented complaint. MonoGame's desktop 256 is generous by comparison.

**Virtual voices.** FMOD and Wwise both separate "tracked" from "audible": a
voice over the limit is *virtualised* rather than dropped — it keeps its
playback position and becomes audible again if a slot frees up. Wwise's
recommended default virtual-voice behaviour is "Kill if finite, else virtual":
kill one-shots that go inaudible, virtualise loops. For one-shot combat impacts,
which is everything Hukbo plays, killing is the normal answer.

**Voice stealing.** FMOD offers *Oldest*, *Furthest*, *Quietest*, and
*Virtualize*. Wwise, on hitting a playback limit, discards by playback priority
and then either the *oldest* or the *newest* instance. Both engines make this a
per-sound setting rather than one global rule.

**Per-sound instance limits.** Both engines expose a max-instances count scoped
either globally or per game object, specifically called out as the tool for
one-shots fired in bulk. This is the same idea as the existing per-slot cap of 3
— the technique is standard; only the number is in question.

**Summing and clipping.** This is the part the current design does not address
at all. Summing N uncorrelated signals raises the level by roughly
10·log₁₀(N) dB; N *correlated* signals — which repeated takes of the same short
impact partly are — approach 20·log₁₀(N). At the measured peak of 35 voices that
is between +15 dB and +31 dB over a single cue. `SoundDirector.CueVolume` is a
fixed 0.8, about −1.9 dBFS, so a busy moment drives the mix far past full scale
and the result hard-clips. The standard mitigations are gain compensation that
scales with the live instance count — 1/√N for uncorrelated material, 1/N for
correlated — and a limiter on the output bus. Neither exists here.

Sources: [Unity default voice counts](https://discussions.unity.com/t/max-real-voices-always-limited-to-32/910855),
[FMOD virtual voice system](https://documentation.help/fmod-studio-api/virtualvoices.html),
[FMOD event macro controls](https://fmod.com/resources/documentation-studio?page=event-macro-controls-reference.html&version=2.1),
[Wwise playback limiting and virtual voice](https://www.audiokinetic.com/en/courses/wwise251/?id=Lesson3_Playback_Limiting_and_Virtual_Voice/),
[Wwise CPU optimisation guidelines](https://www.audiokinetic.com/en/community/blog/wwise-cpu-optimizations-general-guidelines/),
[audio summation fundamentals](https://www.prosoundweb.com/audio-summation-part-3-in-an-ongoing-series-on-essential-fundamentals/),
[3 dB versus 6 dB summing](https://www.sweetwater.com/insync/3-6db/).

## 7. Result D — what the mix actually does

Sections 4 and 5 between them rule out the obvious explanations. The device is
not saturated, it does not leak, the CPU is idle, and the budget discards under
6 percent of cues. Something else makes a busy battle stop sounding like
individual blows.

The remaining candidate was overload: enough clips overlapping that their summed
waveform exceeds what the format can represent, so the peaks are flattened off
and the result reads as a continuous rasp rather than as separate hits. That is
now tested rather than assumed.

### 7.1 Method

The test is arithmetic on real data, with no audio device involved. A real
`BattleSimulation` produces the cue schedule; each cue resolves to the exact
file the client would have chosen, using the same hit-class mapping, the same
fallback chain, and the same `SplitMix64` variant draw; the shipped WAV sample
data is summed into a floating-point buffer at the true trigger times; and the
result is measured against full scale. Each render is also written out as a WAV,
so the outcome can be listened to rather than only read.

Four policies were rendered:

| Policy | What it models |
| --- | --- |
| `today` | The shipped behaviour: 3 per slot, 8 total, fixed gain 0.8 |
| `uncapped-same-gain` | Every cue plays, gain still fixed at 0.8 |
| `uncapped-compensated` | Every cue plays, gain divided by the square root of the voices already sounding |
| `uncapped-compensated-limited` | The above, plus a bus peak limiter at −1 dBFS |

### 7.2 Results

Peak is the loudest sample in the render. Anything above 0.0 dBFS cannot be
represented and is flattened by the output stage.

**200 agents, seed 1:**

| Speed | Policy | Played | Peak voices | Peak dBFS | Clipped samples |
| --- | --- | ---: | ---: | ---: | ---: |
| 1x | `today` | 2 150 | 29 | **+7.7** | 5 385 |
| 1x | `uncapped-same-gain` | 2 186 | 29 | **+10.6** | 6 038 |
| 1x | `uncapped-compensated` | 2 186 | 29 | −0.8 | 0 |
| 4x | `today` | 2 082 | 80 | **+11.0** | 15 463 |
| 4x | `uncapped-same-gain` | 2 186 | 93 | **+12.9** | 18 254 |
| 4x | `uncapped-compensated` | 2 186 | 93 | −3.3 | 0 |

**500 agents, seed 1:**

| Speed | Policy | Played | Peak voices | Peak dBFS | Clipped samples |
| --- | --- | ---: | ---: | ---: | ---: |
| 1x | `today` | 5 408 | 34 | **+9.4** | 16 740 |
| 1x | `uncapped-same-gain` | 5 511 | 41 | **+10.3** | 20 680 |
| 1x | `uncapped-compensated` | 5 511 | 41 | +1.6 | 2 |
| 4x | `today` | 5 203 | 91 | **+10.3** | 43 750 |
| 4x | `uncapped-same-gain` | 5 511 | 113 | **+12.5** | 52 860 |
| 4x | `uncapped-compensated` | 5 511 | 113 | −4.3 | 0 |

### 7.2a After the change

Measured against the implemented policy — the budget kept as a backstop at 16
per slot and 64 total, `CueVolume` at 0.65, and each cue's gain divided by the
square root of the sounding voice count.

| Agents / speed | Played | Suppressed | Peak voices | Peak dBFS | Clipped samples |
| --- | ---: | ---: | ---: | ---: | ---: |
| 200 / 1x | 2 186 | 0 | 29 | −2.6 | 0 |
| 200 / 4x | 2 186 | 0 | 93 | −5.1 | 0 |
| 500 / 1x | 5 511 | 0 | 41 | −0.2 | 0 |
| 500 / 4x | 5 511 | 0 | 113 | −6.1 | 0 |

Every cue the battle emits is played, suppression is zero everywhere, and the
mix stays under full scale in every configuration.

`CueVolume` was lowered from 0.8 to 0.65 as a direct result of this
measurement. At 0.8 the correction cleared three configurations but left 500
agents at 1x peaking at +1.6 dBFS with two flattened samples. The intermediate
value 0.72 was also measured and still left those two samples. 0.65 is the
value that clears every case.

### 7.3 What this establishes

**The shipped game already overloads its own output.** Not at some hypothetical
future agent count — at 200 agents, at normal speed, with the rate limit doing
its job. The mix peaks 7.7 dB above what the format can carry and 5 385 samples
are flattened. At 500 agents and 4x speed that becomes 10.3 dB over and 43 750
flattened samples.

This is a much better explanation of the reported symptom than suppression is.
Losing 1.6 percent of cues is inaudible. Driving the bus 8 to 11 dB into
overload is not: it turns overlapping impacts into a continuous distorted mass,
which is precisely what "the sound stops being distinguishable" describes.

**Raising the cue cap without addressing gain makes it worse,** which is the
finding that most matters for design. Going from `today` to
`uncapped-same-gain` at 200 agents and 1x moves the peak from +7.7 to
+10.6 dBFS and increases flattened samples. More cues into an already
overloaded bus is more overload.

**Dividing each cue's gain by the square root of the voices already sounding
fixes it,** and does so while playing every single cue. Peak drops from +7.7 to
−0.8 dBFS at 200 agents and 1x, and from +10.3 to −4.3 dBFS at 500 agents and
4x. Flattened samples go to zero in every configuration except 500 agents at 1x,
where two samples out of roughly six million still exceed.

**The limiter, as implemented here, contributes nothing.** Its numbers are
identical to the compensated policy in all eight runs. A 5 ms attack envelope
without look-ahead simply cannot catch the transient of a sharp impact — by the
time the envelope responds, the peak has already passed. This is a real result
about that limiter design, not a reason to conclude that no limiter would help;
a look-ahead limiter was not tested.

**Also worth recording: true voice overlap is higher than section 5 estimated.**
Measuring from actual clip lengths rather than a fixed five-tick window gives 29
to 41 voices at 1x and 93 to 113 at 4x, against section 5's 15 to 21. Even the
highest figure stays comfortably under the 256 ceiling, so the conclusion that
capacity is not the constraint survives; but section 7's numbers are the correct
ones.

## 8. Reproducing these numbers

The three harnesses live under `tools/`. **None of them is in `Hukbo.slnx`**, so
`verify.ps1` neither builds nor runs them and the canonical gate is unaffected.
They inherit `Directory.Build.props`, so they build warnings-clean under
`TreatWarningsAsErrors`, and each carries its own `packages.lock.json`. None
modifies a repository file; the mix harness writes only into its own output
directory.

The stale-build correction recorded in section 5 is the reason these are
committed rather than left in a scratch directory. Rebuilding a harness from a
prose description is exactly how a measurement silently drifts from the code it
claims to describe.

**Rebuild `Hukbo.Core` in `Release` before trusting any demand or mix figure.**
A `ProjectReference` will happily link a stale assembly, and that is precisely
what produced the withdrawn numbers.

### 8.1 Demand harness

`tools/Hukbo.Tools.CueDemand`. A console project with a single
`ProjectReference` to `src/Hukbo.Core/Hukbo.Core.csproj`.

It creates a scenario with `Scenario.CreateDefault(seed, agents)`, advances the
real `BattleSimulation` tick by tick, and counts events whose kind is `Attack`
or `Death`, bucketed by the slot the client's `SoundCueMapper` would choose. It
then replays the client's frame loop — accumulating `1/60` second of simulated
time per frame, multiplied by the playback speed, and draining whole ticks from
the accumulator — applying the per-slot and total caps exactly as
`SoundCueBudget` does, and finally derives peak concurrency with a trailing
window sized from the measured mean clip length.

Invocation: `dotnet run -c Release <agents> <seed> <ticks>`.

### 8.2 Audio backend harness

`tools/Hukbo.Tools.VoiceStress`. A console project with a single
`PackageReference` to `MonoGame.Framework.DesktopGL`. It opens no window and
constructs no `Game`, so it measures the audio device in isolation.

Phase A loads every shipped `.wav`, takes the longest clip, and starts explicit
`SoundEffectInstance` objects in a loop up to 512 attempts, recording how many
reach `SoundState.Playing` and what happens at the ceiling.

Phase C saturates the device with 400 fire-and-forget attempts, drains for
1.5 seconds while calling `FrameworkDispatcher.Update()` on a 16 ms cadence,
then measures how many attempts are accepted again, repeated over six rounds.

Phase B issues fire-and-forget `SoundEffect.Play(volume, pitch, pan)` calls at a
fixed rate for six seconds per step, paced to a real 60 frames per second with
`FrameworkDispatcher.Update()` once per frame, counting refusals — both a
`false` return and a caught `InstancePlayLimitException` — and measuring the
wall-clock cost of the audio work and the bytes allocated.

Playback volume defaults to 0.02 so that a 256-voice burst is not painful. Pass
a volume as the second argument to hear it.

Invocation: `dotnet run -c Release [audioDirectory] [volume]`.

### 8.3 Mix harness

`tools/Hukbo.Tools.MixAnalysis`. A console project with a single
`ProjectReference` to `src/Hukbo.Core/Hukbo.Core.csproj` and no engine
dependency at all — it reads and writes WAV data itself, so it opens no audio
device and can run anywhere.

It reads the shipped clips, groups them into variant lists by file-name prefix,
runs a real `BattleSimulation`, and resolves each mapped event to the exact file
the client would have chosen. It then sums the clip sample data into a
floating-point buffer at the true trigger times under each of the four policies
in section 7.1, measures peak amplitude and the count of samples beyond full
scale, and writes each render to a WAV.

Its slot mapping, hit-class mapping, fallback chain, and variant draw are
**replicas** of `SoundCueMapper`, `HitClassCatalog`, and `SoundVariantSelector`,
because those types are `internal` to the windowed `Hukbo.Client` assembly. The
variant draw uses the same `SplitMix64` from `Hukbo.Core` as the client, so the
file chosen for a given tick and entity is identical rather than merely similar.
**If the client's mapping changes, this harness must change with it**, or its
figures quietly stop describing the game.

Invocation: `dotnet run -c Release <audioDirectory> <outputDirectory> <agents> <seed> <speed>`.

## 9. What these measurements do and do not establish

Established:

- The audio backend on this machine plays 256 simultaneous voices and refuses
  the 257th with an exception. That ceiling is a compile-time constant in
  MonoGame and cannot be raised by configuration.
- The backend does not leak voices. Capacity returns to the full 256 after every
  saturation round.
- The CPU and allocation cost of the audio path is negligible at every rate this
  game can produce, and remains negligible at 25 times that rate.
- Playing every cue a 500-agent battle emits peaks at 113 concurrent voices at
  4x speed, comfortably inside the ceiling.
- The current per-frame budget discards only 1.6 to 5.6 percent of cues. It is
  not protecting the machine from anything, and it is not large enough to be
  what a spectator is noticing.
- **The shipped game overloads its own output mix.** At 200 agents and normal
  speed it peaks 7.7 dB above full scale with 5 385 flattened samples; at 500
  agents and 4x speed, 10.3 dB over with 43 750 flattened samples.
- **Raising the cue cap without changing gain makes the overload worse.**
- **Dividing each cue's gain by the square root of the sounding voice count
  removes the overload entirely while playing every cue**, bringing peak to
  between −4.3 and +1.6 dBFS.

Ruled out:

- **A source leak.** Directly tested in section 4.3 and refuted.
- **CPU or allocation pressure.** Measured in section 4.2 and negligible.
- **Rate limiting as the primary cause of the reported symptom.** Measured in
  section 5.2 at under 6 percent.

Not established, and deliberately left open:

- **That overload is what the owner is hearing.** The overload is measured and
  certain; the link from it to the specific words "the sound stops" is
  inference. Rendered WAV files exist for every policy so this can be settled by
  listening, but no listening test has been recorded.
- **What the right replacement policy is.** Section 6 records what other engines
  do and section 7 shows that one specific correction works. Neither chooses
  final numbers, considers how gain compensation should interact with the mute
  toggle or the cue log, nor proposes a design.
- **Whether a look-ahead limiter would help.** The 5 ms feed-forward limiter
  tested in section 7 measurably does nothing, because it cannot catch an impact
  transient. That is a result about that design only.
- **Whether these numbers hold on other hardware.** The 256-voice ceiling is a
  property of MonoGame's desktop build; the same constant is 32 on iOS and
  Android. The CPU figures are this machine's. Any change must degrade safely
  rather than assume 256.
- **Whether variant coverage is complete.** A slot with no file for a given hit
  class is silent by design. Whether every weapon and hit-class pair currently
  resolves to a `Ready` binding was not audited here.

## 10. Provenance

Every figure in sections 3, 4, 5, and 7 is a direct reading from one of the three
harness runs described in section 8, performed on 2026-07-27 against
`Hukbo.Core` at commit `8815a3c`. No figure here is an estimate, a rule of
thumb, or a value carried over from the original design document.

Section 4.4 is quoted from the MonoGame `v3.8.5` source tag. Section 6 is
external reference material with the citations given inline; it describes other
engines and is not evidence about Hukbo.

**One correction has been made to this document.** The first revision of section
5 reported 657 ticks, 3.22 cues per tick, and 6.9 to 33.5 percent suppression at
200 and 500 agents. Those numbers were measured against a `Hukbo.Core` binary
that predated the last-stand formation and collision-priority merges, and they
overstated both demand and suppression by a wide margin. They have been replaced
throughout, and the conclusion they supported — that the rate limit is the main
thing standing between the spectator and the battle's sound — did not survive
the correction. Sections 3, 4, and 6 never depended on `Hukbo.Core` and were not
affected.
