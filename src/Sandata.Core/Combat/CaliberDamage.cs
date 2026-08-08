using Sandata.Core.Weapons;

namespace Sandata.Core.Combat;

/// <summary>
/// Raw per-hit damage, before <see cref="CoverRules"/>, keyed by
/// <see cref="CaliberFamily"/>. Task 79d-2b's replacement for the single
/// flat damage constant it deletes from <c>SandataSimulation</c>: every hit
/// previously dealt the same 25 points regardless of loadout, so
/// <see cref="FirearmDefinition.Caliber"/> was invisible to the damage model
/// even though the field already existed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every value below is PROVISIONAL.</b> No source in this repository or
/// its research documents supplies a measured or tuned per-caliber damage
/// figure — the only number that ever existed for this purpose was the
/// single flat 25 this table replaces. This task is not authorised to
/// present a researched number, and none of the eight values here is one.
/// </para>
/// <para>
/// What each value <i>is</i> allowed to encode, and does, is a defensible
/// <b>relative</b> ordering drawn from each caliber's publicly documented
/// class of muzzle energy, not from any per-round measurement this task
/// performed:
/// <list type="bullet">
/// <item>The two pistol calibers (<see cref="CaliberFamily.Cal58X21"/>,
/// <see cref="CaliberFamily.Cal9X19"/>) sit below every rifle caliber — a
/// compact pistol round versus any rifle round is not a close call.</item>
/// <item>Within rifle calibers, the smaller-bore intermediate group
/// (<see cref="CaliberFamily.Cal545X39"/>, <see cref="CaliberFamily.Cal556X45"/>,
/// <see cref="CaliberFamily.Cal58X42"/>) sits below <see cref="CaliberFamily.Cal762X39"/>
/// — 7.62×39 is widely reported to carry more energy than the 5.45/5.56/5.8
/// class it competes with.</item>
/// <item>The two full-power, battle-rifle-class rounds
/// (<see cref="CaliberFamily.Cal762X51"/>, <see cref="CaliberFamily.Cal68X51"/>)
/// sit above every intermediate round — "full-power" versus "intermediate"
/// is itself a documented military distinction, not this task's invention.
/// 6.8×51 sits above 7.62×51 specifically because it was designed,
/// publicly and deliberately, to exceed 7.62 NATO's pressure and velocity
/// envelope; that is a stated design goal, not a measurement this task
/// performed.</item>
/// </list>
/// </para>
/// <para>
/// None of this is a claim about lethality, penetration, or any other
/// combat property beyond the flat per-hit point value
/// <see cref="Sandata.Core.Simulation.SandataSimulation.ProposeFire"/>
/// consumes — it is the same "honest placeholder, not a tuned combat
/// value" the deleted flat constant already was, now split eight ways by
/// loadout instead of one flat number for every loadout.
/// </para>
/// </remarks>
public static class CaliberDamage
{
    /// <summary>
    /// The raw per-hit damage points for <paramref name="caliber"/>, before
    /// cover. <b>PROVISIONAL</b> for every case — see this type's remarks
    /// for what each value's relative position is and is not allowed to
    /// claim.
    /// </summary>
    public static int RawDamage(CaliberFamily caliber) => caliber switch
    {
        // PROVISIONAL. 7.62x39 (Soviet/AK intermediate): above the smaller-
        // bore intermediate group below, per widely reported muzzle energy.
        CaliberFamily.Cal762X39 => 25,

        // PROVISIONAL. 5.45x39 (Soviet/AK-74 intermediate): smallest-bore
        // member of the intermediate group.
        CaliberFamily.Cal545X39 => 20,

        // PROVISIONAL. 5.56x45 (NATO intermediate): same tier as 5.8x42.
        CaliberFamily.Cal556X45 => 22,

        // PROVISIONAL. 7.62x51 (NATO full-power/battle-rifle round): above
        // every intermediate round.
        CaliberFamily.Cal762X51 => 28,

        // PROVISIONAL. 6.8x51 (modern hybrid, designed to exceed 7.62x51's
        // pressure/velocity envelope): above 7.62x51 for that stated reason.
        CaliberFamily.Cal68X51 => 30,

        // PROVISIONAL. 5.8x42 (Chinese intermediate round): same tier as
        // 5.56x45.
        CaliberFamily.Cal58X42 => 22,

        // PROVISIONAL. 9x19 (standard NATO/service pistol round): above
        // 5.8x21, below every rifle caliber.
        CaliberFamily.Cal9X19 => 15,

        // PROVISIONAL. 5.8x21 (Chinese compact-pistol round): lowest of the
        // eight, below the full-size 9x19 service pistol round.
        CaliberFamily.Cal58X21 => 10,

        _ => throw new ArgumentOutOfRangeException(
            nameof(caliber), caliber, $"Unknown {nameof(CaliberFamily)} value."),
    };
}
