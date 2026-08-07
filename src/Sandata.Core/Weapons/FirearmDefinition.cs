namespace Sandata.Core.Weapons;

/// <summary>
/// One row of the 38-weapon roster: everything the simulation and the audio
/// library need to know about a firearm, expressed entirely in integers.
/// Design section 9. No <c>float</c>, no <c>double</c>, no percentage stored
/// as a fraction — timings are authored here in <b>milliseconds</b>, and task
/// 23's <c>TickConversion</c> is the only place that turns a millisecond
/// field into a tick count. This type performs no conversion of its own.
/// </summary>
/// <remarks>
/// Field order is part of the replay contract: <see cref="FirearmRuleset"/>
/// folds every field of every row, in this declaration order, into
/// <see cref="FirearmRuleset.ContentHash"/>. Reordering a field, adding one,
/// or removing one is a new preset version with new golden expectations, the
/// same rule <c>CLAUDE.md</c> section 5 and <see cref="SandataRuleset"/>
/// (Rules/SandataRuleset.cs) already follow.
/// </remarks>
/// <param name="Id">
/// The dense, append-only <see cref="FirearmId"/> this row describes. A row's
/// position in <see cref="FirearmCatalog.Rows"/> always equals this value's
/// numeric ordinal — <c>FirearmCatalogTests</c> asserts it.
/// </param>
/// <param name="Class"><see cref="WeaponClass.Rifle"/> or <see cref="WeaponClass.Pistol"/>.</param>
/// <param name="Caliber">
/// One of the eight caliber families. Selects the gunshot report sample —
/// design section 10 — independently of the specific weapon.
/// </param>
/// <param name="Mechanism">
/// AK, AR, bullpup, or pistol. Selects the mechanism sound layer — the
/// selector detent, the bolt, the button thunk — on top of the caliber
/// report.
/// </param>
/// <param name="Modes">
/// The fire-mode flag set this weapon's selector offers. Design section 9
/// records exactly five distinct sets across the 38-row roster; every row
/// carries one of them, and no row carries both <see cref="FireModeSet.Burst2"/>
/// and <see cref="FireModeSet.Burst3"/>.
/// </param>
/// <param name="ReadyMs">
/// Milliseconds to raise the weapon from <c>Lowered</c>. Design section 9's
/// timing chain: <c>Lowered -&gt; Raising(ReadyTicks) -&gt; Turning -&gt; Aiming
/// -&gt; Firing -&gt; Resetting -&gt; Aiming</c>.
/// </param>
/// <param name="AimBaseMs">Milliseconds to aim at a target already centred in the vision cone.</param>
/// <param name="AimPerBamMs">
/// Additional milliseconds added to <see cref="AimBaseMs"/> per 1024 Bam of
/// off-centre offset the operator must turn through. A target near the edge
/// of the cone takes longer to engage than a centred one.
/// </param>
/// <param name="ResetMs">Milliseconds required between one engagement's <c>Firing</c> phase ending and the next <c>Aiming</c> phase starting.</param>
/// <param name="TurnBamPerTick">
/// Rotation rate, in raw Bam16 magnitude per tick, of the <c>Turning</c>
/// phase. Heavier weapons turn more slowly.
/// </param>
/// <param name="AutoBandMaxWu">
/// The range, in world units, at or below which fully automatic fire is
/// selected when <see cref="Modes"/> has <see cref="FireModeSet.Auto"/>. Design
/// section 9's ordered, total fire-mode band-selection rule.
/// </param>
/// <param name="BurstBandMaxWu">
/// The range, in world units, at or below which a burst mode
/// (<see cref="FireModeSet.Burst3"/> tested before <see cref="FireModeSet.Burst2"/>)
/// is selected, beyond <see cref="AutoBandMaxWu"/>.
/// </param>
/// <param name="SingleBandMaxWu">
/// The range, in world units, at or below which single fire is selected,
/// beyond <see cref="BurstBandMaxWu"/>. Beyond this range the weapon produces
/// no engagement at all.
/// </param>
/// <param name="DispersionAtZeroWu">The angular dispersion, in Bam, of a shot fired at zero range.</param>
/// <param name="DispersionAtMaxWu">
/// The angular dispersion, in Bam, of a shot fired at
/// <see cref="MaxEffectiveWu"/> or beyond. Design section 9's linear integer
/// interpolation runs between this value and <see cref="DispersionAtZeroWu"/>.
/// </param>
/// <param name="MaxEffectiveWu">The range, in world units, at which dispersion interpolation clamps.</param>
/// <param name="MagazineCapacity">Rounds carried per magazine.</param>
/// <param name="ReloadMs">Milliseconds to complete a magazine change.</param>
/// <param name="CyclicRpm">
/// The mechanical cyclic rate in rounds per minute. Design section 9's
/// per-round tick accumulator converts this into the deterministic firing
/// cadence. Present on every row for structural uniformity even where
/// <see cref="Modes"/> never reaches an automatic or burst mode — the
/// accumulator is simply never driven for those rows.
/// </param>
/// <param name="ExemptFromLoweredRule">
/// <see langword="true"/> for pistols only. Design section 9: "the one
/// conditional that generates the whole game" never forces a pistol lowered
/// near a wall or in a doorway.
/// </param>
public readonly record struct FirearmDefinition(
    FirearmId Id,
    WeaponClass Class,
    CaliberFamily Caliber,
    MechanismGroup Mechanism,
    FireModeSet Modes,

    // Timing chain, milliseconds as authored. Task 23 owns the ms-to-tick
    // conversion; nothing in this record performs it.
    int ReadyMs,
    int AimBaseMs,
    int AimPerBamMs,
    int ResetMs,
    int TurnBamPerTick,

    // Range bands, world units.
    int AutoBandMaxWu,
    int BurstBandMaxWu,
    int SingleBandMaxWu,

    // Accuracy, dispersion in Bam.
    int DispersionAtZeroWu,
    int DispersionAtMaxWu,
    int MaxEffectiveWu,

    // Magazine and cycling.
    int MagazineCapacity,
    int ReloadMs,
    int CyclicRpm,

    // The one rule that generates the game.
    bool ExemptFromLoweredRule);
