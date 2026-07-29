using System.Collections.Immutable;

using Hukbo.Core.Combat;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// Deterministic combinatorial generator over the six canonical loadouts of
/// the weapon-relative movement design (design section 17 of
/// <c>docs/plans/2026-07-30-weapon-movement-foundation-design.md</c>). This
/// is test infrastructure, not simulation code: it never constructs a
/// simulation, never draws from an RNG, and never runs a battle. The matchup
/// <em>runs</em> — mirrored starts, reversed caller input, termination and
/// progress invariants, crowded variants — belong to the five weapon
/// sessions and consume these cells.
/// </summary>
/// <remarks>
/// The canonical order <c>KP, WA, KA, IT, KS, IS</c> is binding on every
/// table, array index, and fold order in the design, and every enumeration
/// here follows it with nested indices <c>i &lt;= j</c>. Any cell containing
/// a shielded loadout must name
/// <see cref="CombatPresetId.PrecolonialPhilippinesV2"/> — the only preset
/// fielding all six loadouts — which the
/// <c>RequiresPrecolonialPhilippinesV2</c> members encode.
/// </remarks>
internal static class MovementScenarioMatrix
{
    /// <summary>The number of canonical loadouts.</summary>
    internal const int CanonicalLoadoutCount = 6;

    /// <summary>
    /// The combat preset every cell containing a shielded loadout must name:
    /// <see cref="CombatPresetId.PrecolonialPhilippinesV2"/> is the only
    /// preset fielding all six canonical loadouts.
    /// </summary>
    internal const CombatPresetId ShieldedCellCombatPreset =
        CombatPresetId.PrecolonialPhilippinesV2;

    /// <summary>
    /// The six canonical loadouts in canonical order — <c>KP</c> Kampilan
    /// solo, <c>WA</c> Wasay solo, <c>KA</c> Kalis solo, <c>IT</c> Itak solo,
    /// <c>KS</c> Kalis plus TallHardwood, <c>IS</c> Itak plus TallHardwood —
    /// each at the default rank.
    /// </summary>
    internal static ImmutableArray<CombatLoadout> CanonicalLoadouts { get; } =
    [
        new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None),
        new(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None),
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None),
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None),
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood),
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood),
    ];

    /// <summary>
    /// The design abbreviations for the canonical loadouts, index-aligned
    /// with <see cref="CanonicalLoadouts"/>.
    /// </summary>
    internal static ImmutableArray<string> CanonicalLoadoutCodes { get; } =
        ["KP", "WA", "KA", "IT", "KS", "IS"];

    /// <summary>
    /// Enumerates the 21 unordered 1v1 pairs — fifteen distinct pairs plus
    /// six mirror cells — with nested indices <c>i &lt;= j</c> over the
    /// canonical order.
    /// </summary>
    internal static ImmutableArray<OneVersusOnePair> EnumerateOneVersusOnePairs()
    {
        var builder = ImmutableArray.CreateBuilder<OneVersusOnePair>(
            UnorderedPairCount(CanonicalLoadoutCount));
        for (var i = 0; i < CanonicalLoadoutCount; i++)
        {
            for (var j = i; j < CanonicalLoadoutCount; j++)
            {
                builder.Add(new OneVersusOnePair(i, j));
            }
        }

        return builder.MoveToImmutable();
    }

    /// <summary>
    /// Enumerates the 21 unordered two-member team compositions — the same
    /// <c>i &lt;= j</c> enumeration as the 1v1 pairs, read as teams.
    /// </summary>
    internal static ImmutableArray<TeamComposition> EnumerateTeamCompositions()
    {
        var builder = ImmutableArray.CreateBuilder<TeamComposition>(
            UnorderedPairCount(CanonicalLoadoutCount));
        for (var i = 0; i < CanonicalLoadoutCount; i++)
        {
            for (var j = i; j < CanonicalLoadoutCount; j++)
            {
                builder.Add(new TeamComposition(i, j));
            }
        }

        return builder.MoveToImmutable();
    }

    /// <summary>
    /// Enumerates the 231 team-versus-team matchups — 210 distinct matchups
    /// plus 21 team mirrors — crossing the canonical team compositions with
    /// team indices <c>i &lt;= j</c>.
    /// </summary>
    internal static ImmutableArray<TeamMatchup> EnumerateTeamMatchups()
    {
        var teams = EnumerateTeamCompositions();
        var builder = ImmutableArray.CreateBuilder<TeamMatchup>(
            UnorderedPairCount(teams.Length));
        for (var i = 0; i < teams.Length; i++)
        {
            for (var j = i; j < teams.Length; j++)
            {
                builder.Add(new TeamMatchup(teams[i], teams[j]));
            }
        }

        return builder.MoveToImmutable();
    }

    /// <summary>
    /// The number of unordered pairs with repetition over <paramref name="count"/>
    /// members: <c>count * (count + 1) / 2</c>.
    /// </summary>
    private static int UnorderedPairCount(int count) => count * (count + 1) / 2;

    private static bool IsShielded(int loadoutIndex) =>
        CanonicalLoadouts[loadoutIndex].Shield != ShieldId.None;

    /// <summary>
    /// One unordered 1v1 cell, identified by two canonical loadout indices
    /// with <see cref="FirstLoadoutIndex"/> &lt;= <see cref="SecondLoadoutIndex"/>.
    /// </summary>
    internal readonly record struct OneVersusOnePair(
        int FirstLoadoutIndex,
        int SecondLoadoutIndex)
    {
        /// <summary>The loadout at <see cref="FirstLoadoutIndex"/>.</summary>
        internal CombatLoadout FirstLoadout => CanonicalLoadouts[FirstLoadoutIndex];

        /// <summary>The loadout at <see cref="SecondLoadoutIndex"/>.</summary>
        internal CombatLoadout SecondLoadout => CanonicalLoadouts[SecondLoadoutIndex];

        /// <summary>Whether both sides field the same loadout.</summary>
        internal bool IsMirror => FirstLoadoutIndex == SecondLoadoutIndex;

        /// <summary>
        /// Whether this cell contains a shielded loadout and therefore must
        /// name <see cref="ShieldedCellCombatPreset"/> when it is ever run.
        /// </summary>
        internal bool RequiresPrecolonialPhilippinesV2 =>
            IsShielded(FirstLoadoutIndex) || IsShielded(SecondLoadoutIndex);
    }

    /// <summary>
    /// One unordered two-member team, identified by two canonical loadout
    /// indices with <see cref="FirstMemberIndex"/> &lt;= <see cref="SecondMemberIndex"/>.
    /// </summary>
    internal readonly record struct TeamComposition(
        int FirstMemberIndex,
        int SecondMemberIndex)
    {
        /// <summary>The loadout at <see cref="FirstMemberIndex"/>.</summary>
        internal CombatLoadout FirstMember => CanonicalLoadouts[FirstMemberIndex];

        /// <summary>The loadout at <see cref="SecondMemberIndex"/>.</summary>
        internal CombatLoadout SecondMember => CanonicalLoadouts[SecondMemberIndex];

        /// <summary>Whether either member carries a shield.</summary>
        internal bool ContainsShieldedLoadout =>
            IsShielded(FirstMemberIndex) || IsShielded(SecondMemberIndex);
    }

    /// <summary>
    /// One unordered team-versus-team cell over two canonical team
    /// compositions, with the first team at or before the second in
    /// canonical team enumeration order.
    /// </summary>
    internal readonly record struct TeamMatchup(
        TeamComposition FirstTeam,
        TeamComposition SecondTeam)
    {
        /// <summary>Whether both sides field the same team composition.</summary>
        internal bool IsMirror => FirstTeam == SecondTeam;

        /// <summary>
        /// Whether this cell contains a shielded loadout and therefore must
        /// name <see cref="ShieldedCellCombatPreset"/> when it is ever run.
        /// </summary>
        internal bool RequiresPrecolonialPhilippinesV2 =>
            FirstTeam.ContainsShieldedLoadout || SecondTeam.ContainsShieldedLoadout;
    }
}
