using Sandata.Core.Determinism;

namespace Sandata.Core.Rules;

/// <summary>
/// Identifies the rounding rule a <see cref="SandataRuleset"/> uses to
/// convert an authored millisecond timing into an integer tick count at
/// ruleset bake time. Design section 4. <b>Append-only</b> for the same
/// reason <see cref="SandataPresetId"/> is: the identifier folds into
/// <see cref="SandataRuleset.ContentHash"/>, so a saved replay or a golden
/// expectation names the rule by this numeric value.
/// </summary>
public enum MsToTickConversionRule
{
    /// <summary>
    /// <c>ticks = (milliseconds * TickRate + 500) / 1000</c>, integer
    /// division, half away from zero. The only conversion rule Sandata
    /// defines in v0.1. Design section 4.
    /// </summary>
    HalfAwayFromZero = 1,
}

/// <summary>
/// Sandata's ruleset shell: the tick rate, the millisecond-to-tick
/// conversion rule, and the handful of v0.1 tuning constants every later
/// task reads rather than hard-coding — the path amortisation latency, the
/// squad-cohesion radius, the weapon-lowered proximity distance, and the
/// weapon-chain aim tolerance. This is the root of Sandata's determinism
/// contract: everything that later folds a value through
/// <see cref="ContentHash"/> folds through the fixed order declared here.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TickRate"/> and <see cref="MsToTickConversionRuleId"/> are
/// pinned by design section 4 — 50 Hz and the half-away-from-zero rule —
/// and are not gameplay tuning. <see cref="PathLatencyTicks"/>,
/// <see cref="GroupCohesionRadius"/>, <see cref="LoweredWallDistanceWu"/>,
/// and <see cref="AimToleranceBam"/> are provisional reconstructions of
/// gameplay tuning, not measurements — design sections 4, 7, 8, and 9 name
/// the mechanism each one drives and record that its exact value is
/// measured, not derived, by a later task (paths: task 27's benchmark;
/// cohesion: task 28's grouping; wall distance: task 32's lowered rule; aim
/// tolerance: task 23's timing chain). Changing any one of the six fields
/// below is a new preset value under <see cref="SandataPresetId"/>, per
/// design section 4 and <c>CLAUDE.md</c> section 5.
/// </para>
/// <para>
/// <see cref="AimToleranceBam"/> is a raw Bam16 magnitude — <c>ushort</c>,
/// 65,536 to the full turn — rather than the <c>Bam16</c> type itself.
/// <c>Bam16</c> is declared by a separate, parallel task with no dependency
/// relationship to this one; this shell holds the raw value so it depends
/// on nothing but the tier-1 extraction, and a caller that has <c>Bam16</c>
/// wraps this value in it.
/// </para>
/// </remarks>
public sealed class SandataRuleset
{
    /// <summary>
    /// The one ruleset instance v0.1 ships,
    /// <see cref="Sandata.Core.Rules.SandataPresetId.ModernTacticalV1"/>'s
    /// concrete values. <c>SandataRulesetTests</c> pins its
    /// <see cref="ContentHash"/> to a recorded value.
    /// </summary>
    public static SandataRuleset ModernTacticalV1 { get; } = new(
        tickRate: 50,
        msToTickConversionRuleId: MsToTickConversionRule.HalfAwayFromZero,
        pathLatencyTicks: 10,
        groupCohesionRadius: 96,
        loweredWallDistanceWu: 24,
        aimToleranceBam: 1024);

    public SandataRuleset(
        int tickRate,
        MsToTickConversionRule msToTickConversionRuleId,
        int pathLatencyTicks,
        int groupCohesionRadius,
        int loweredWallDistanceWu,
        ushort aimToleranceBam)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(tickRate, 0);

        // MsToTickConversionRuleId is not validated against Enum.IsDefined
        // here. Design section 4 defines exactly one rule in v0.1 and
        // nothing yet dispatches behaviour on this identifier's value — the
        // one conversion formula task 23 implements is fixed, not selected
        // by this field. The identifier exists so a future second rule is a
        // new preset value with a distinct, hash-visible identifier rather
        // than an undetectable behavioural change. Whichever future task
        // adds a second rule and a dispatcher on this value is the task that
        // adds the defined-ness check.
        ArgumentOutOfRangeException.ThrowIfNegative(pathLatencyTicks);
        ArgumentOutOfRangeException.ThrowIfNegative(groupCohesionRadius);
        ArgumentOutOfRangeException.ThrowIfNegative(loweredWallDistanceWu);

        TickRate = tickRate;
        MsToTickConversionRuleId = msToTickConversionRuleId;
        PathLatencyTicks = pathLatencyTicks;
        GroupCohesionRadius = groupCohesionRadius;
        LoweredWallDistanceWu = loweredWallDistanceWu;
        AimToleranceBam = aimToleranceBam;
        ContentHash = ComputeContentHash();
    }

    /// <summary>
    /// Ticks per second. 50 in every preset today, so one tick is exactly
    /// 20 ms. Design section 4 records why 50 Hz rather than Hukbo's 20 Hz:
    /// at 50 Hz the finest published weapon-timing distinction in the
    /// roster, 150 ms versus 180 ms, is 1.5 ticks apart instead of
    /// quantising away.
    /// </summary>
    public int TickRate { get; }

    /// <summary>
    /// The rule identifier used to convert every authored millisecond timing
    /// into an integer tick count at ruleset bake time. Design section 4.
    /// </summary>
    public MsToTickConversionRule MsToTickConversionRuleId { get; }

    /// <summary>
    /// The fixed number of ticks between a path request and that path
    /// becoming valid, regardless of how many searches the machine actually
    /// completed. Never a per-tick budget — design section 4 and section 7
    /// record why a budget is the branch that lets scheduling decide an
    /// outcome. A provisional reconstruction of gameplay tuning; task 27's
    /// path service measures and, if needed, revises it.
    /// </summary>
    public int PathLatencyTicks { get; }

    /// <summary>
    /// The distance, in world units, within which two operators of the same
    /// faction are unioned into one squad by the union-find grouping in
    /// design section 8. A provisional reconstruction of gameplay tuning;
    /// task 28's squad grouping measures and, if needed, revises it.
    /// </summary>
    public int GroupCohesionRadius { get; }

    /// <summary>
    /// The distance, in world units, from a wall segment within which an
    /// operator's weapon is forced lowered, unless the weapon is exempt.
    /// Design section 9 calls this the one conditional that generates the
    /// whole product. A provisional reconstruction of gameplay tuning; task
    /// 32's weapon-lowered rule measures and, if needed, revises it.
    /// </summary>
    public int LoweredWallDistanceWu { get; }

    /// <summary>
    /// The raw Bam16 magnitude — <c>ushort</c>, 65,536 to the full turn —
    /// within which the weapon-chain <c>Turning</c> phase in design section 9
    /// considers a target's shortest arc close enough to the aim point to
    /// advance to <c>Aiming</c>. A provisional reconstruction of gameplay
    /// tuning; task 23's timing chain measures and, if needed, revises it.
    /// </summary>
    public ushort AimToleranceBam { get; }

    /// <summary>
    /// FNV-1a over exactly the six fields above, folded through
    /// <see cref="SandataHash"/> in the fixed declaration order
    /// <see cref="TickRate"/>, <see cref="MsToTickConversionRuleId"/>,
    /// <see cref="PathLatencyTicks"/>, <see cref="GroupCohesionRadius"/>,
    /// <see cref="LoweredWallDistanceWu"/>, <see cref="AimToleranceBam"/>.
    /// Changing that order, adding a field, or removing one is a new preset
    /// version with new golden expectations — the same rule
    /// <c>MovementRuleset.ContentHash</c> follows and
    /// <c>CLAUDE.md</c> section 5 states.
    /// </summary>
    public ulong ContentHash { get; }

    private ulong ComputeContentHash()
    {
        var hash = SandataHash.Begin();
        SandataHash.Fold(ref hash, TickRate);
        SandataHash.Fold(ref hash, (long)MsToTickConversionRuleId);
        SandataHash.Fold(ref hash, PathLatencyTicks);
        SandataHash.Fold(ref hash, GroupCohesionRadius);
        SandataHash.Fold(ref hash, LoweredWallDistanceWu);
        SandataHash.Fold(ref hash, AimToleranceBam);
        return hash;
    }
}
