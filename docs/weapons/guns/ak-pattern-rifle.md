# The AK-pattern rifle

Reference only. See `docs/weapons/guns/README.md` for what this folder is
and is not.

## The six rows

`FirearmCatalog.Rows` (`src/Sandata.Core/Weapons/FirearmCatalog.cs`) carries
six rows built by the `Rifle(...)` factory with `MechanismGroup.Ak`. All six
carry a 30-round magazine and a cyclic rate of 600 rounds per minute:

| `FirearmId` | Caliber | Modes | Notes |
| --- | --- | --- | --- |
| `Ak47` (id 0) | 7.62×39mm (`Cal762X39`) | Safe, Single, Auto | AK-47. |
| `Akm` (id 1) | 7.62×39mm (`Cal762X39`) | Safe, Single, Auto | AKM. |
| `Ak74M` (id 2) | 5.45×39mm (`Cal545X39`) | Safe, Single, Auto | AK-74M. |
| `Ak12` (id 3) | 5.45×39mm (`Cal545X39`) | Safe, Single, Burst2, Auto | AK-12, 2018/2021 configuration. Distinct row from `Ak122023`: this one carries a two-round burst. |
| `Ak122023` (id 4) | 5.45×39mm (`Cal545X39`) | Safe, Single, Auto | AK-12, 2023 configuration. The 2023 model deletes the two-round burst. |
| `Ak15` (id 5) | 7.62×39mm (`Cal762X39`) | Safe, Single, Burst2, Auto | AK-15. |

Every field beyond the table above is sourced from `FirearmCatalog`'s
`Rifle(...)` factory, which applies one shared `RifleTemplate` set of
constants to every rifle row in the roster — the AK-pattern rows are not
individually tuned. `FirearmDefinition.ExemptFromLoweredRule` is `false` for
every row above: an AK-pattern rifle is subject to the weapon-lowered rule
(`src/Sandata.Core/Weapons/FirearmDefinition.cs`).

## Shared rifle-template timing and range bands

These constants live in `FirearmCatalog.cs` as `RifleReadyMs`,
`RifleAimBaseMs`, and so on, and apply to all 24 rifle rows in the roster,
not just the six AK-pattern ones:

| Field | Value | Meaning |
| --- | --- | --- |
| `ReadyMs` | 405 ms | Time to raise the weapon from `Lowered`. |
| `AimBaseMs` | 335 ms | Time to aim at a target already centred in the vision cone. |
| `AimPerBamMs` | 5 ms | Additional aim time per 1024 Bam of off-centre offset. |
| `ResetMs` | 150 ms | Time required between one engagement's firing phase ending and the next aiming phase starting. |
| `TurnBamPerTick` | 2048 | Rotation rate of the turning phase, in raw Bam16 magnitude per tick. |
| `AutoBandMaxWu` | 240 wu | Range at or below which fully automatic fire is selected, when the weapon's mode set has `Auto`. |
| `BurstBandMaxWu` | 320 wu | Range at or below which a burst mode is selected, beyond the auto band. Only reachable for `Ak12` and `Ak15`, the two AK rows carrying `Burst2`. |
| `SingleBandMaxWu` | 800 wu | Range at or below which single fire is selected, beyond the burst band. Beyond this range the weapon produces no engagement at all. |
| `DispersionAtZeroWu` | 32 Bam | Angular dispersion of a shot fired at zero range. |
| `DispersionAtMaxWu` | 256 Bam | Angular dispersion of a shot fired at `MaxEffectiveWu` or beyond. |
| `MaxEffectiveWu` | 800 wu | Range at which dispersion interpolation clamps. |
| `ReloadMs` | 2500 ms | Time to complete a magazine change. |

These values are documented in `FirearmCatalog.cs`'s own remarks as
provisional placeholders applied uniformly across the rifle class, not
per-weapon measurements — the research document backing this catalog
supplies only two published data points (`ReadyMs` and `AimBaseMs`) at the
class level, not a per-weapon ballistics table.

## Audio: what is generated and what is silent

Two of the six AK-pattern rows have real, committed audio; the other four do
not. This follows directly from `SandataSoundCatalog.cs`'s
`IsGeneratedGunReportRow` predicate, which raises a row's declared variant
count from the ordinary six to ten only for the `Cal762X39` and `Cal9X19`
caliber families, in the `CloseDry` and `IndoorTail` environments, for
`FireMode.Single`.

| Row | Caliber | Has generated audio? |
| --- | --- | --- |
| `Ak47`, `Akm`, `Ak15` | 7.62×39mm | **Yes** — these three rows share the `gun-762x39-single-close` and `gun-762x39-single-indoor` slots. |
| `Ak74M`, `Ak12`, `Ak122023` | 5.45×39mm | **No** — the sound catalog keys the gunshot report by caliber family, not by individual weapon, and no `545x39` slot has ever been generated. These rows resolve to the ordinary six-variant placeholder row, which declares files that do not exist on disk. |

For the two generated slots, ten `.wav` files each are committed under
`src/Sandata.Client/Content/Audio/`:

- `gun-762x39-single-close-01.wav` through `-10.wav` — the AK-pattern rifle
  fired in the open, at close-to-moderate range.
- `gun-762x39-single-indoor-01.wav` through `-10.wav` — the same rifle fired
  inside a room.

Both rows carry `FireMode.Single` only. Firing in `Auto` resolves to the
`GunLoop`/`GunTail` family instead (see `sound-catalog.md`), and no
`762x39` `GunLoop` or `GunTail` slot has ever been generated — an automatic
AK-pattern burst is silent regardless of caliber. Firing `Burst2` on `Ak12`
or `Ak15` resolves to a baked-burst `GunReport` row that declares four
variants and, like the automatic rows, has no file on disk.

`ShotSlotResolver.cs` picks the environment from range, indoor/outdoor
state, and suppressor state: an unsuppressed, non-distant, outdoor shot at
200 world units or less resolves to `CloseDry` (`CloseRangeMaxWu = 200`); an
indoor shot resolves to `IndoorTail` regardless of range unless it is
distant or suppressed; and a shot at 800 world units or beyond resolves to
`Distant` (`DistantRangeMinWu = 800`), which is one of the environments no
762x39 row has ever generated audio for.

The full generation provenance — prompts, dates, and the lesson about
prompt wording that cost real ElevenLabs credits to learn — is recorded in
`src/Sandata.Client/Content/Audio/README.md`, not duplicated here.
