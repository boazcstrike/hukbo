# The Glock-pattern pistol

Reference only. See `docs/weapons/guns/README.md` for what this folder is
and is not.

## The two rows

`FirearmCatalog.Rows` (`src/Sandata.Core/Weapons/FirearmCatalog.cs`) carries
two Glock rows, both built by the `Pistol(...)` factory at its default
caliber of 9×19mm (`CaliberFamily.Cal9X19`):

| `FirearmId` | Caliber | Modes | Magazine capacity |
| --- | --- | --- | --- |
| `Glock17Gen5` (id 26) | 9×19mm | Single | 17 |
| `Glock19Gen5` (id 27) | 9×19mm | Single | 15 |

Both rows carry `FireModeSet.Single` alone — no `Safe` flag. `FirearmCatalog`'s
own remarks classify this as the "striker-fired pistols with no manual thumb
safety" group, as opposed to the hammer-fired and safety-lever pistols
elsewhere in the roster that also carry `Safe`. Both rows use
`MechanismGroup.Pistol`.

## The one rule that generates the game, and the pistol's exemption from it

`FirearmDefinition.ExemptFromLoweredRule` is `true` for every pistol row in
the roster, the Glock rows included (`FirearmCatalog.Pistol(...)`, which
always sets `ExemptFromLoweredRule: true`). The weapon-lowered rule forces a
weapon `Lowered` — re-imposing its `ReadyMs` — when its carrier crosses a
doorway or stands within the lowered-wall distance of a wall. A pistol never
takes that penalty. This is documented in the design document as "the one
conditional that generates the whole game" and as the mechanical reason a
pistol can beat a rifle in a doorway; the Glock rows are two of the fourteen
pistol rows that carry the exemption.

## Pistol-template timing and range bands

These constants live in `FirearmCatalog.cs` as `PistolReadyMs`,
`PistolAimBaseMs`, and so on, and apply to all fourteen pistol rows in the
roster, not just the two Glock ones:

| Field | Value | Meaning |
| --- | --- | --- |
| `ReadyMs` | 80 ms | Time to raise the weapon from `Lowered`. Not reachable in practice, since the Glock rows are exempt from ever being forced lowered — but every row still carries the field for structural uniformity. |
| `AimBaseMs` | 165 ms | Time to aim at a target already centred in the vision cone. The research document names a range of 150 to 180 ms for a pistol; `FirearmCatalog.cs` uses the midpoint. |
| `AimPerBamMs` | 3 ms | Additional aim time per 1024 Bam of off-centre offset. |
| `ResetMs` | 120 ms | Time required between one engagement's firing phase ending and the next aiming phase starting. |
| `TurnBamPerTick` | 4096 | Rotation rate of the turning phase, in raw Bam16 magnitude per tick — faster than the rifle template's 2048, since a pistol is lighter. |
| `AutoBandMaxWu` | 0 wu | Not invented: no pistol row's `Modes` ever carries `Auto`, so this field is never reached by the fire-mode band rule. Zero keeps that inertness visible in the data rather than implying an unresearched band boundary exists. |
| `BurstBandMaxWu` | 0 wu | Same reasoning as `AutoBandMaxWu`: no pistol row carries a burst mode. |
| `SingleBandMaxWu` | 320 wu | Range at or below which single fire is selected. Beyond this range the weapon produces no engagement at all — one quarter of the rifle template's 800 wu single-fire band. |
| `DispersionAtZeroWu` | 64 Bam | Angular dispersion of a shot fired at zero range. |
| `DispersionAtMaxWu` | 512 Bam | Angular dispersion of a shot fired at `MaxEffectiveWu` or beyond. |
| `MaxEffectiveWu` | 320 wu | Range at which dispersion interpolation clamps. |
| `ReloadMs` | 1600 ms | Time to complete a magazine change. |
| `CyclicRpm` | 600 (inert) | Populated for structural uniformity across every row, but never driven: no pistol row's `Modes` ever reaches `Auto` or `Burst`, so the per-round tick accumulator this field feeds is never exercised for a pistol. It is a documented placeholder, not a measured Glock mechanical cycle rate. |

As with the rifle template, `FirearmCatalog.cs`'s own remarks mark every
value beyond the two published class-level data points as a provisional
placeholder applied uniformly within the pistol class, not a per-weapon
measurement.

## Audio: what is generated

Both Glock rows share the same caliber family, `Cal9X19`, and
`SandataSoundCatalog.cs`'s `IsGeneratedGunReportRow` predicate raises exactly
that caliber's `CloseDry` and `IndoorTail` `FireMode.Single` rows from the
ordinary six declared variants to ten. Ten `.wav` files each are committed
under `src/Sandata.Client/Content/Audio/`:

- `gun-9x19-single-close-01.wav` through `-10.wav` — the Glock-pattern pistol
  fired in the open, at close-to-moderate range.
- `gun-9x19-single-indoor-01.wav` through `-10.wav` — the same pistol fired
  inside a room.

Because the sound catalog keys the gunshot report by caliber family rather
than by individual firearm, `Glock17Gen5` and `Glock19Gen5` — and every
other `Cal9X19` pistol row in the roster, generic-caliber or not — resolve
to the same twenty generated files. No other `Cal9X19` row (`OutdoorTail`,
`Distant`, `Suppressed`) has generated audio: a Glock shot fired beyond 200
world units in the open, or at 800 world units or more, or through a
suppressor, resolves to a declared row with no file on disk. See
`ShotSlotResolver.cs` for the exact environment-selection thresholds
(`CloseRangeMaxWu = 200`, `DistantRangeMinWu = 800`), also described in
`ak-pattern-rifle.md`.

No `Cal9X19` `GunLoop` or `GunTail` row has generated audio either, but this
is moot for these two rows specifically: neither Glock carries `Auto` in its
`Modes`, so the pistol never reaches the automatic-fire family at all.

The full generation provenance — prompts, dates, and the lesson about
prompt wording that cost real ElevenLabs credits to learn — is recorded in
`src/Sandata.Client/Content/Audio/README.md`, not duplicated here.
