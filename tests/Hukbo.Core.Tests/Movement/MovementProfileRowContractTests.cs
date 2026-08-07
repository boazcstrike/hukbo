using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Movement.Profiles;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The contract every equipment-relative movement row satisfies, asserted once
/// per row rather than once per weapon session. Six rows in canonical order —
/// <c>KP, WA, KA, IT, KS, IS</c> — each exported by the profile file its
/// session owns, each composed into
/// <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/> at its canonical
/// index, each keyed by equipment alone, each rank-independent, each carrying
/// disengagement hysteresis, and each applying its six signed opponent-distance
/// offset cells through
/// <see cref="MovementRouteRules.EffectivePreferredDistanceRaw"/>.
/// </summary>
/// <remarks>
/// <para>
/// The five per-weapon profile suites each wrote these same invariants against
/// their own row or rows. Fourteen properties restated five ways is fourteen
/// properties that a seventh loadout would have to be hand-written into again,
/// so they are parameterized over the row table here instead. What stays in
/// the per-weapon files is what is genuinely per-weapon: the approved
/// calibration ranges of the Wasay table, and the relational assertions that
/// compare a shielded row against its solo counterpart.
/// </para>
/// <para>
/// The literal field values of all six rows are pinned exhaustively by
/// <see cref="MovementProfileRegistrationTests"/>, which reads them through the
/// registered ruleset. The per-weapon suites additionally pinned the same
/// literals through the exported statics, on the argument that a row rebuilt
/// with equal values but a different instance would otherwise slip through.
/// <see cref="EveryExportedRowIsTheInstanceTheRegistryComposes"/> below closes
/// that gap directly and by reference identity, which is strictly stronger than
/// re-asserting the values: once the export and the registered row are proven
/// to be the same object, a value pin on one is a value pin on the other. The
/// duplicated literal blocks were removed on that basis.
/// </para>
/// <para>
/// Every number reachable through this file is a <strong>provisional
/// reconstruction: gameplay tuning, no historical measurement</strong>. The
/// evidence ledgers live under <c>docs/research/movement/</c>.
/// </para>
/// </remarks>
public sealed class MovementProfileRowContractTests
{
    /// <summary>
    /// The canonical scenario attack range of five world units, matching the
    /// movement pipeline fixtures and every per-weapon suite that computed an
    /// effective preferred distance by hand.
    /// </summary>
    private const int AttackRangeRaw = 5 * FixedPoint.Scale; // 5120

    /// <summary>
    /// The five ranks. Rank is social standing and carries no movement
    /// meaning, so every one of them must resolve the same row instance.
    /// </summary>
    private static readonly RankId[] EveryRank =
    [
        RankId.Datu,
        RankId.Maharlika,
        RankId.Timawa,
        RankId.AlipingNamamahay,
        RankId.Ayuey,
    ];

    private static MovementRuleset V6 =>
        MovementPresetRegistry.Get(MovementPresetId.EquipmentRelativeFootworkV6);

    /// <summary>
    /// The row each profile file exports, indexed by canonical loadout order.
    /// This is the side of the identity link the registry is checked against.
    /// </summary>
    private static LoadoutMovementProfile ExportedRow(int canonicalIndex) =>
        canonicalIndex switch
        {
            0 => KampilanMovementProfile.Row,
            1 => WasayMovementProfile.Row,
            2 => KalisMovementProfile.Row,
            3 => ItakMovementProfile.Row,
            4 => TallHardwoodMovementProfiles.KalisRow,
            5 => TallHardwoodMovementProfiles.ItakRow,
            _ => throw new ArgumentOutOfRangeException(nameof(canonicalIndex)),
        };

    private static CombatLoadout CanonicalKey(
        int canonicalIndex, RankId rank = RankId.Timawa) =>
        canonicalIndex switch
        {
            0 => new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None, rank),
            1 => new(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None, rank),
            2 => new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None, rank),
            3 => new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None, rank),
            4 => new(
                WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood, rank),
            5 => new(
                WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood, rank),
            _ => throw new ArgumentOutOfRangeException(nameof(canonicalIndex)),
        };

    public static TheoryData<int> EveryCanonicalRow()
    {
        var data = new TheoryData<int>();
        for (var row = 0; row < MovementRuleset.CanonicalLoadoutCount; row++)
        {
            data.Add(row);
        }

        return data;
    }

    public static TheoryData<int, RankId> EveryRowAndRank()
    {
        var data = new TheoryData<int, RankId>();
        for (var row = 0; row < MovementRuleset.CanonicalLoadoutCount; row++)
        {
            foreach (var rank in EveryRank)
            {
                data.Add(row, rank);
            }
        }

        return data;
    }

    /// <summary>
    /// Every row against every canonical opponent column, at the canonical
    /// five-unit attack range: 36 cells, each expected value computed by hand
    /// from the design section 13 row as
    /// <c>5120 * (preferred + offset) / 10000</c> with integer truncation, so
    /// the assertion runs against an arithmetic oracle rather than against the
    /// production expression restated.
    /// </summary>
    public static TheoryData<int, int, long> EveryRowAndOpponentColumn()
    {
        var data = new TheoryData<int, int, long>();
        long[][] expected =
        [
            // KP: preferred 11,500, offsets [0, 0, 250, 500, 250, 500]
            [5_888, 5_888, 6_016, 6_144, 6_016, 6_144],

            // WA: preferred 10,800, offsets [500, 0, 250, 500, 250, 500].
            // Four of these six truncate: 5120 * 11300 / 10000 is 5785.6.
            [5_785, 5_529, 5_657, 5_785, 5_657, 5_785],

            // KA: preferred 12,000, offsets [-500, -250, 0, 250, 250, 500]
            [5_888, 6_016, 6_144, 6_272, 6_272, 6_400],

            // IT: preferred 11,000, offsets [-750, -500, -250, 0, 0, 250]
            [5_248, 5_376, 5_504, 5_632, 5_632, 5_760],

            // KS: preferred 13,000, offsets [-250, 0, 250, 500, 0, 250]
            [6_528, 6_656, 6_784, 6_912, 6_656, 6_784],

            // IS: preferred 10,000, offsets [-500, -250, 0, 250, -250, 0]
            [4_864, 4_992, 5_120, 5_248, 4_992, 5_120],
        ];

        for (var row = 0; row < expected.Length; row++)
        {
            for (var column = 0; column < expected[row].Length; column++)
            {
                data.Add(row, column, expected[row][column]);
            }
        }

        return data;
    }

    // ----- Identity: export, registry slot, and resolver agree -----

    /// <summary>
    /// The registry composes the owned profile files rather than private
    /// copies of them. For every canonical row the exported static, the
    /// registered slot at that index, and the value the resolver returns for
    /// that row's key are one and the same object. This is the assertion that
    /// lets <see cref="MovementProfileRegistrationTests"/>'s value pins stand
    /// for the exports as well.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryCanonicalRow))]
    public void EveryExportedRowIsTheInstanceTheRegistryComposes(
        int canonicalIndex)
    {
        var exported = ExportedRow(canonicalIndex);

        Assert.Same(exported, V6.LoadoutMovementProfiles[canonicalIndex]);
        Assert.Same(
            exported, V6.ResolveLoadoutProfile(CanonicalKey(canonicalIndex)));
    }

    /// <summary>
    /// Design 4.1: the profile key is equipment only. Every exported row
    /// carries the complete equipment triple of its canonical position, and
    /// the stored key always reads <see cref="RankId.Timawa"/> — the default —
    /// whatever rank a caller later supplies.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryCanonicalRow))]
    public void EveryRowIsKeyedByEquipmentAloneAtTheDefaultRank(
        int canonicalIndex)
    {
        var key = ExportedRow(canonicalIndex).Loadout;
        var expected = CanonicalKey(canonicalIndex);

        Assert.Equal(expected, key);
        Assert.Equal(expected.Weapon, key.Weapon);
        Assert.Equal(expected.Armor, key.Armor);
        Assert.Equal(expected.Shield, key.Shield);
        Assert.Equal(RankId.Timawa, key.Rank);
    }

    /// <summary>
    /// Rank is social standing with no movement meaning, so a warrior of any
    /// rank resolves the very same row instance — not merely an equal copy of
    /// it — on every canonical row including both shielded ones.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryRowAndRank))]
    public void EveryRankResolvesTheSameRowInstance(
        int canonicalIndex, RankId rank)
    {
        Assert.Same(
            ExportedRow(canonicalIndex),
            V6.ResolveLoadoutProfile(CanonicalKey(canonicalIndex, rank)));
    }

    // ----- The validator envelope every row satisfies -----

    /// <summary>
    /// Hysteresis exists on every row: the release threshold is strictly below
    /// the entry threshold, so no pair of counts can enter and leave
    /// disengagement on the same tick. The constructor enforces this; the
    /// assertion states which side of the boundary the shipped rows chose.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryCanonicalRow))]
    public void EveryRowKeepsItsReleaseStrictlyBelowItsEntry(int canonicalIndex)
    {
        var row = ExportedRow(canonicalIndex);

        Assert.True(
            row.ReengageEnemyToAllyBasisPoints <
                row.DisengageEnemyToAllyBasisPoints,
            $"{row.Loadout} has no disengagement hysteresis.");
    }

    /// <summary>
    /// The committed pace never exceeds the forward pace on any row, so
    /// committing can only ever slow a warrior down.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryCanonicalRow))]
    public void CommittingNeverRaisesThePaceCeilingOnAnyRow(int canonicalIndex)
    {
        var row = ExportedRow(canonicalIndex);

        Assert.True(
            row.CommittedPaceBasisPoints <= row.ForwardPaceBasisPoints,
            $"{row.Loadout} speeds up while committed.");
    }

    /// <summary>
    /// The six offset cells never cancel the preferred distance: on every row,
    /// against every canonical opponent, the adjusted basis points stay
    /// strictly positive and every cell stays inside the shared
    /// <c>[-2000, 2000]</c> envelope.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryCanonicalRow))]
    public void EveryOpponentOffsetLeavesAPositivePreferredDistance(
        int canonicalIndex)
    {
        var row = ExportedRow(canonicalIndex);

        Assert.Equal(
            LoadoutMovementProfile.OpponentDistanceOffsetCount,
            row.OpponentDistanceOffsetBasisPoints.Length);

        foreach (var cell in row.OpponentDistanceOffsetBasisPoints)
        {
            Assert.InRange(cell, -2_000, 2_000);
            Assert.True(
                row.PreferredDistanceBasisPoints + cell > 0,
                $"{row.Loadout} cancels its preferred distance.");
        }
    }

    // ----- Effective preferred distance (design 4.4) -----

    /// <summary>
    /// Every row against every canonical opponent column, at the canonical
    /// five-unit attack range. The expected values are hand-computed in
    /// <see cref="EveryRowAndOpponentColumn"/>; four of the Wasay row's six
    /// columns truncate, which is the behaviour the integer division is
    /// specified to have and the reason the oracle is written out rather than
    /// recomputed. Provisional reconstruction: gameplay tuning; no historical
    /// measurement.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryRowAndOpponentColumn))]
    public void TheEffectivePreferredDistanceCoversEveryOpponentColumn(
        int canonicalIndex, int opponentCanonicalIndex, long expectedRaw)
    {
        Assert.Equal(
            expectedRaw,
            MovementRouteRules.EffectivePreferredDistanceRaw(
                AttackRangeRaw,
                ExportedRow(canonicalIndex),
                opponentCanonicalIndex));
    }
}
