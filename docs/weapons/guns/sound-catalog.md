# The Sandata sound catalog — reference

Reference only. See `docs/weapons/guns/README.md` for what this folder is
and is not. **This document describes a catalog, not a plan.** Declaring a
row in `SandataSoundCatalog.cs` costs nothing and generates no file; only
running `./scripts/sfx.ps1` against ElevenLabs does either of those things,
and that script is not authorized to run against this catalog beyond the
narrow slice `CLAUDE.md` section 9 already names.

## Size: 114 slots, 572 variant files

`SandataSoundCatalog.Rows` (`src/Sandata.Client/Audio/SandataSoundCatalog.cs`)
declares 114 rows, built by nine `Add...` methods. Summed by family:

| Family | How the rows are built | Rows | Variants per row | Variant total |
| --- | --- | --- | --- | --- |
| `GunReport`, single-shot | 8 caliber families × 5 real environments | 40 | 6, except the 4 generated rows at 10 | 256 |
| `GunReport`, baked 3-round burst | 3 near environments, `Cal556X45` only | 3 | 4 | 12 |
| `GunReport`, baked 2-round burst | 2 burst-capable calibers × 3 near environments | 6 | 4 | 24 |
| `GunLoop` | 8 caliber families × 2 automatic-fire environments | 16 | 4 | 64 |
| `GunTail` | 8 caliber families × 2 automatic-fire environments | 16 | 4 | 64 |
| `Mechanism`, selector | 4 mechanism groups | 4 | 4 | 16 |
| `Mechanism`, action (magazine out, magazine in, bolt rack) | 4 mechanism groups × 3 actions | 12 | 4 | 48 |
| `Dry` fire | 8 caliber families | 8 | 3 | 24 |
| `Impact` | 5 impact surfaces | 5 | 8 | 40 |
| `Casing` | 2 weapon classes × 2 landing surfaces | 4 | 6 | 24 |
| **Total** | | **114** | | **572** |

This is the current catalog as of 2026-08-14. It was 106 slots and 540
variants until that date, when `AddAutomaticLoopAndTail` was widened from
the six rifle caliber families to all eight — closing a `KeyNotFoundException`
that an automatic-capable pistol (`Cal9X19` or `Cal58X21`) would otherwise
have hit on its first automatic round, since `ShotSlotResolver.FindWithFallback`'s
last resort is `SandataSoundCatalog.Find`, which throws when no row is
declared for a tuple. That widening declared 16 new rows and 64 new
variants; it generated no file.

## What is actually generated versus merely declared

Of the 572 declared variants, exactly 40 have a real `.wav` file committed
to this repository, all under `src/Sandata.Client/Content/Audio/`:

- `gun-762x39-single-close-01.wav` through `-10.wav`
- `gun-762x39-single-indoor-01.wav` through `-10.wav`
- `gun-9x19-single-close-01.wav` through `-10.wav`
- `gun-9x19-single-indoor-01.wav` through `-10.wav`

That is four rows out of 114, all in the `GunReport` family, all
`FireMode.Single`, all in the `CloseDry` or `IndoorTail` environment, for
the two calibers the shipped `angle-house` mission's two firearms actually
fire: 7.62×39mm (the AK-pattern rifle) and 9×19mm (the Glock-pattern
pistol). See `ak-pattern-rifle.md` and `glock-pattern-pistol.md` for the
per-weapon detail.

**The other 532 declared variants have no file on disk.** A missing file
plays as silence through the negative-cache path in
`MonoGameSandataSoundOutput` (per `SandataSoundCatalog.cs`'s own comments) —
it does not throw and does not substitute another sound. Declaring a
catalog row is purely a data-table entry; it never generates a file and
never spends a credit. Generation happens only through `./scripts/sfx.ps1`,
which is the one script in this repository that talks to a network service.

## Three acoustic environments are entirely empty

`SoundEnvironment.cs` declares five real environments plus the `None`
sentinel: `CloseDry`, `IndoorTail`, `OutdoorTail`, `Distant`, and
`Suppressed`. Of those five, only `CloseDry` and `IndoorTail` have any
generated audio at all, and only for the two calibers above. The other
three environments — `outdoor` (`OutdoorTail`), `distant` (`Distant`), and
`suppressed` (`Suppressed`) — have **zero** generated files across the
entire catalog, for every caliber, every mechanism, and every fire mode.
Per `src/Sandata.Client/Content/Audio/README.md`: "that is a known gap, not
a defect to re-report." A shot that resolves to one of those three
environments — by range, or because a suppressor is fitted — is silent.

## No `GunLoop` or `GunTail` file exists at all

The `GunLoop` and `GunTail` families together declare 32 rows and 128
variants — the looping body of sustained automatic fire, and the tail that
follows it once the trigger releases. None of those 128 variants has ever
been generated, for any caliber, in any environment. A shooter who fires in
`FireMode.Auto` therefore produces no sound today, regardless of weapon or
caliber, because `ShotSlotResolver.cs` resolves `Auto` to the `GunLoop`
family (`ResolveFamily`), and that family has no audio anywhere in the
catalog.

## The unauthorized remaining spend

ElevenLabs bills sound-effect generation at 200 credits per generation. At
572 declared variants, generating the entire catalog at zero rejects costs
roughly **114,400 credits** (`572 × 200`). Per `CLAUDE.md` section 9, that
spend is **not authorized**. The only spend the user has authorized is the
40-file slice already generated and committed, described above; every other
row in the catalog remains wholly ungenerated by design, not by oversight.

The design document's own cost analysis (section 10 of
`docs/plans/2026-08-07-sandata-scaffold-design.md`) computed a real-world
estimate against the catalog's earlier, smaller totals — 106 slots and 524
or 540 variant files, depending on which correction in that section's
history is read — and found that ElevenLabs' own measured take-quality
variance (one run peaking at 93 percent usable audio, another under 1
percent) means a realistic 30 to 50 percent reject rate, pushing the actual
credit cost well above the zero-reject figure. That analysis predates the
2026-08-14 catalog expansion to 114 slots and 572 variants and has not been
recomputed against the current totals; treat its dollar figures as
directionally informative for the older, smaller catalog rather than as a
current estimate.

## Producing a dry-run manifest without spending anything

`scripts/sfx-manifest.ps1` is a network-free script. Per its own header
comment, it calls no network service, generates no audio file, and stops
after printing. It works by running the one xunit fact that materialises
the manifest — `SoundManifestTests.WriteManifestArtifact` in
`tests/Sandata.Client.Tests` — via `dotnet test --filter`, then reading the
CSV that fact writes, then printing the row count, the variant count, and
the credit and dollar estimate for a full generation run. Nothing in that
script or in the test it runs makes an HTTP call of any kind, and nothing
references the third-party sound-generation vendor `scripts/sfx.ps1` talks
to. This is the script design section 15 requires the remaining catalog
spend to stay behind, and it is safe to run at any time to get a current,
accurate count directly from the compiled catalog rather than from a
document like this one that can drift out of date.
