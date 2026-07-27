# Measurement tools

Three console programs that measure things about Hukbo which are otherwise
argued about rather than known. They exist to produce the evidence in
[`docs/research/SOUND-CAPACITY-MEASUREMENTS.md`](../docs/research/SOUND-CAPACITY-MEASUREMENTS.md)
and to let those numbers be reproduced later.

## They are not part of the build

**None of these projects is listed in `Hukbo.slnx`.** `scripts/verify.ps1`
builds and tests the solution, so it does not see them: the canonical gate is
neither slowed nor put at risk by anything in this folder. They are built and
run by hand, deliberately.

They still inherit `Directory.Build.props` and `Directory.Packages.props`, so
they compile warnings-clean under `TreatWarningsAsErrors`, take package versions
from central management, and each carries its own `packages.lock.json`.

Nothing here writes to a repository file. `Hukbo.Tools.MixAnalysis` writes WAV
renders into an output directory you name; `tools/mix-output/` is gitignored for
that purpose.

## Rebuild `Hukbo.Core` first

```powershell
dotnet build src/Hukbo.Core/Hukbo.Core.csproj -c Release
```

`Hukbo.Tools.CueDemand` and `Hukbo.Tools.MixAnalysis` take a `ProjectReference`
on `Hukbo.Core`, and a stale assembly will link without complaint. That is not
hypothetical: the first revision of the research document reported demand
figures that were wrong by a factor of two because they were measured against a
`Hukbo.Core` binary from before the last-stand formation merge. Rebuild, then
measure.

## The tools

### `Hukbo.Tools.CueDemand`

How much sound a battle asks for, and how much of it the client's rate limit
throws away.

Runs a real `BattleSimulation`, counts the events that map to a sound slot,
replays the client's per-frame budget over them at 1x, 2x, and 4x, and sweeps a
range of candidate budgets.

```powershell
dotnet run --project tools/Hukbo.Tools.CueDemand -c Release -- 200 1 10000
```

Arguments: agents, seed, tick limit.

### `Hukbo.Tools.VoiceStress`

What the audio backend on this machine can actually do.

Opens no window and constructs no `Game`, so it measures the audio device
alone. Phase A finds the hard concurrent-voice ceiling with explicit
`SoundEffectInstance` objects. Phase B sustains fire-and-forget playback at
rates from 20 to 1 600 cues per second and reports refusals, CPU per frame, and
allocation. Phase C saturates and drains the device six times to prove that
voices are recycled rather than leaked.

```powershell
dotnet run --project tools/Hukbo.Tools.VoiceStress -c Release
```

Arguments: audio directory, playback volume. Both optional; the directory
defaults to the client's shipped `Content/Audio` and the volume defaults to
0.02, quiet enough that a 256-voice burst is not painful. **Raise the volume
only if you intend to listen to it.**

### `Hukbo.Tools.MixAnalysis`

What a battle actually sounds like, and whether the mix overloads.

Sums the shipped clip sample data at the trigger times a real battle produces,
under four playback policies, and reports peak level against full scale and the
count of samples beyond it. Writes each render to a WAV so the result can be
heard.

```powershell
dotnet run --project tools/Hukbo.Tools.MixAnalysis -c Release -- src/Hukbo.Client/Content/Audio tools/mix-output 200 1 1
```

Arguments: audio directory, output directory, agents, seed, speed multiplier.

> **This harness replicates client logic it cannot reference.** `SoundCueMapper`,
> `HitClassCatalog`, and `SoundVariantSelector` are `internal` to the windowed
> `Hukbo.Client` assembly, so `CueSchedule.cs` mirrors them by hand. The variant
> draw uses the same `SplitMix64` from `Hukbo.Core` the client uses, so the file
> chosen for a given tick and entity is identical rather than merely similar.
> **If the client's slot mapping, hit-class mapping, fallback chain, or variant
> selection changes, `CueSchedule.cs` must change with it** — otherwise it keeps
> reporting confident numbers about a game that no longer exists.
