using Hukbo.Core.Combat;

namespace Hukbo.Core.Simulation;

/// <summary>
/// A ranged weapon's readable draw-and-loose cycle, projected each tick from
/// the same attack cooldown every weapon already counts down — never stored,
/// never hashed, never snapshotted. Section 8.1 of the ranged-units design
/// document is explicit that this is a deliberate divergence from the pose
/// research's recommendation of new
/// per-agent authoritative state: the cooldown is treated as a good-enough
/// proxy for a draw cycle, which is a bet risk item 2 of section 12 records.
/// If the phases ever read as arbitrary on screen, that bet was wrong and the
/// phase has to become real state with its own hash fold — a substantially
/// larger change than this one.
/// </summary>
/// <remarks>
/// <see cref="None"/> is <c>0</c> so <see langword="default"/> is the neutral
/// value, matching <see cref="Movement.FootworkPhase.None"/> and every other
/// zero-defaulted enum on <see cref="AgentView"/>.
/// </remarks>
public enum RangedPhase
{
    /// <summary>Not a ranged weapon, or no ranged action in progress.</summary>
    None = 0,

    /// <summary>Weapon carried, cooldown clear, waiting for a target in range.</summary>
    Ready = 1,

    /// <summary>Nock the arrow, bring the shaft to the hand, or pour and ram.</summary>
    Load = 2,

    /// <summary>Pull to anchor, cock the arm back, or shoulder and level.</summary>
    Draw = 3,

    /// <summary>The shot leaves the weapon.</summary>
    Release = 4,

    /// <summary>Return to <see cref="Ready"/>.</summary>
    Recover = 5,
}

/// <summary>
/// Derives <see cref="RangedPhase"/> and its remaining-tick count from the
/// pair <c>(AttackCooldownRemaining, AttackCooldownTicks)</c> the movement
/// stage has already produced for the tick — the projection design section
/// 8.1 describes. Called once per agent from
/// <see cref="BattleSimulation"/>'s view-update step; queries nothing beyond
/// the two values and the agent's own <see cref="WeaponId"/>, both of which
/// the tick already computed.
/// </summary>
internal static class RangedPhaseProjection
{
    /// <summary>
    /// Cooldown, in basis points of 10,000, each phase claims while a shot's
    /// cooldown counts down from full to zero. Order matches the direction
    /// the countdown runs: <see cref="RangedPhase.Release"/> first (the
    /// instant after a shot fires, when the cooldown is charged back to its
    /// full value), then <see cref="RangedPhase.Recover"/>,
    /// <see cref="RangedPhase.Load"/>, and <see cref="RangedPhase.Draw"/>,
    /// with <see cref="RangedPhase.Ready"/> reached and held at a cooldown of
    /// zero. PROVISIONAL gameplay tuning per CLAUDE.md section 7 — none of
    /// these fractions is a historical measurement, and RU-24's calibration
    /// pass may retune the cooldowns these fractions are taken of without
    /// touching this table.
    /// </summary>
    private readonly record struct PhaseShares(
        int ReleaseBasisPoints,
        int RecoverBasisPoints,
        int LoadBasisPoints,
        int DrawBasisPoints);

    private const int BasisPointScale = 10_000;

    /// <summary>
    /// Bangkaw — a thrown spear reused for several casts, so its cycle is
    /// the shortest of the three and its readable action is the cocking-back
    /// throw, not a lengthy preparation.
    /// </summary>
    private static readonly PhaseShares BangkawShares = new(
        ReleaseBasisPoints: 500,
        RecoverBasisPoints: 1_500,
        LoadBasisPoints: 2_000,
        DrawBasisPoints: 6_000);

    /// <summary>
    /// Busog — a bow, whose nock is quick and whose held draw tension is the
    /// legible part, per design section 8.1: "a bow most of its shorter one
    /// in Draw".
    /// </summary>
    private static readonly PhaseShares BusogShares = new(
        ReleaseBasisPoints: 500,
        RecoverBasisPoints: 1_000,
        LoadBasisPoints: 1_500,
        DrawBasisPoints: 7_000);

    /// <summary>
    /// Arquebus — a matchlock, whose multi-beat pour-and-ram dominates its
    /// very long interval, per design section 8.1: "an arquebus should spend
    /// most of its very long interval in Load".
    /// </summary>
    private static readonly PhaseShares ArquebusShares = new(
        ReleaseBasisPoints: 500,
        RecoverBasisPoints: 500,
        LoadBasisPoints: 8_000,
        DrawBasisPoints: 1_000);

    /// <summary>
    /// Derives the current phase and how many ticks remain in it, from the
    /// weapon carried and the cooldown pair the tick already produced.
    /// Returns <see cref="RangedPhase.None"/> and <c>0</c> for any weapon
    /// that is not one of the three ranged <see cref="WeaponId"/> members —
    /// including every melee weapon, at every cooldown value.
    /// </summary>
    internal static (RangedPhase Phase, int TicksRemaining) Derive(
        WeaponId weapon,
        int attackCooldownRemaining,
        int attackCooldownTicks)
    {
        if (!TryGetShares(weapon, out var shares))
        {
            return (RangedPhase.None, 0);
        }

        if (attackCooldownRemaining <= 0)
        {
            return (RangedPhase.Ready, 0);
        }

        // Boundaries counted up from zero, so Draw — the phase adjoining
        // Ready — absorbs the rounding remainder and the four spans always
        // sum to exactly attackCooldownTicks regardless of how the basis
        // points divide it.
        var drawTicks = ShareTicks(attackCooldownTicks, shares.DrawBasisPoints);
        var loadTicks = ShareTicks(attackCooldownTicks, shares.LoadBasisPoints);
        var recoverTicks = ShareTicks(attackCooldownTicks, shares.RecoverBasisPoints);
        var loadFloor = drawTicks;
        var recoverFloor = loadFloor + loadTicks;
        var releaseFloor = recoverFloor + recoverTicks;

        if (attackCooldownRemaining > releaseFloor)
        {
            return (RangedPhase.Release, attackCooldownRemaining - releaseFloor);
        }

        if (attackCooldownRemaining > recoverFloor)
        {
            return (RangedPhase.Recover, attackCooldownRemaining - recoverFloor);
        }

        if (attackCooldownRemaining > loadFloor)
        {
            return (RangedPhase.Load, attackCooldownRemaining - loadFloor);
        }

        return (RangedPhase.Draw, attackCooldownRemaining);
    }

    private static bool TryGetShares(WeaponId weapon, out PhaseShares shares)
    {
        switch (weapon)
        {
            case WeaponId.Bangkaw:
                shares = BangkawShares;
                return true;
            case WeaponId.Busog:
                shares = BusogShares;
                return true;
            case WeaponId.Arquebus:
                shares = ArquebusShares;
                return true;
            default:
                shares = default;
                return false;
        }
    }

    private static int ShareTicks(int attackCooldownTicks, int basisPoints) =>
        (int)((long)attackCooldownTicks * basisPoints / BasisPointScale);
}
