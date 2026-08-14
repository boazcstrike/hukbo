# Sandata guns — reference

This folder is a **reference for Sandata's firearm roster and its sound
catalog**. It documents what already exists in the source tree: the 38-row
weapon table in `src/Sandata.Core/Weapons/FirearmCatalog.cs` and the 114-slot
sound catalog in `src/Sandata.Client/Audio/SandataSoundCatalog.cs`.

**This folder is not a task list, and it authorises nothing.** It records
facts about code that is already committed. Generating a new sound file,
adding a weapon row, or changing a range band is a change to that source
code, governed by the workflow in the repository's own `CLAUDE.md` (design
document, then plan document, then implementation, then the canonical gate).
Nothing written here substitutes for that process, and nothing written here
grants permission to run `./scripts/sfx.ps1` beyond the slice `CLAUDE.md`
section 9 already authorises.

## User decision, 2026-08-14

The wider gun and sound-effect work that had been tracked as an open item
under `docs/plans/` is not active work. The user's decision on that date was
to narrow near-term focus to the two firearms the shipped `angle-house`
mission actually uses — the AK-pattern rifle and the Glock-pattern pistol —
and to move the surrounding reference material out of `docs/plans/` and into
this folder, where it reads as documentation of what is built rather than as
a backlog.

## What is in this folder

| Document | Covers |
| --- | --- |
| [`ak-pattern-rifle.md`](./ak-pattern-rifle.md) | The six AK-mechanism rifle rows in `FirearmCatalog`, their shared rifle-template timing and range bands, and the generated audio that already ships for them. |
| [`glock-pattern-pistol.md`](./glock-pattern-pistol.md) | The Glock 17 Gen5 and Glock 19 Gen5 rows, the pistol template, the weapon-lowered exemption, and the generated audio that already ships for them. |
| [`sound-catalog.md`](./sound-catalog.md) | The full 114-slot / 572-variant catalog as reference: what is generated, what is declared but silent, the unauthorised remaining spend, and how to produce a network-free dry-run manifest. |

## What is authoritative

The C# source is the ground truth for every number in this folder:

- `src/Sandata.Core/Weapons/FirearmCatalog.cs` and
  `src/Sandata.Core/Weapons/FirearmDefinition.cs` for the weapon roster.
- `src/Sandata.Client/Audio/SandataSoundCatalog.cs` for the sound catalog's
  114 declared slots and their variant counts.
- `src/Sandata.Client/Audio/ShotSlotResolver.cs` for how a fired shot is
  resolved to a family, an environment, and a variant.
- `src/Sandata.Client/Content/Audio/README.md` for the exact provenance of
  the 40 `.wav` files that are actually committed to this repository.

If a number in this folder ever disagrees with one of those files, the
source file wins; the disagreement is a documentation bug in this folder,
not a fact about the game.

## Repository conventions this folder follows

Per the repository's `CLAUDE.md`: this folder never links into
`docs/archives/`, since that folder is pruned periodically and any link into
it would eventually break. Where an archived document is worth naming, it is
named in prose instead. This folder is also not itself an archive — it
documents current, committed behaviour, not a closed or superseded plan.
