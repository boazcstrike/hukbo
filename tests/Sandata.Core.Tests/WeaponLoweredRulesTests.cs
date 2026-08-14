using Sandata.Core.Combat;
using Sandata.Core.Navigation;
using Sandata.Core.Weapons;

namespace Sandata.Core.Tests;

/// <summary>
/// Proves design section 9's weapon-lowered rule: forced lowered within
/// <c>LoweredWallDistanceWu</c> of a wall or inside a closed door's cell,
/// exempt for pistols, and — once clear — the raise path re-imposes the full
/// <c>ReadyTicks</c> wait before the chain can reach <c>Aiming</c>. Mirrors
/// <c>LineOfSightTests</c>' fixture style: small <see cref="NavGrid"/>
/// instances and plain <c>long</c> wall coordinate arrays.
/// </summary>
public sealed class WeaponLoweredRulesTests
{
    // 32 by 32 cells at CellSizeWu = 4 is 128 by 128 world units — plenty of
    // room for a wall segment, its exact-threshold neighbourhood, and a
    // separate door cell, with no risk of any of this file's coordinates
    // running off the grid's edge.
    private static NavGrid NewGrid() => new(width: 32, height: 32);

    [Fact]
    public void PositionExactlyAtLoweredWallDistance_IsForcedLowered()
    {
        var grid = NewGrid();

        // A vertical wall at x = 50, spanning y in [0, 100]. At y = 60 (well
        // inside the span, away from either endpoint) the perpendicular
        // distance from a query point to this wall is simply |x - 50|.
        var wallBuckets = WallBuckets.Build(grid, [50], [0], [50], [100]);
        const int LoweredWallDistanceWu = 8;

        // x = 42: distance to the wall is exactly 8, the inclusive threshold.
        var isLowered = WeaponLoweredRules.IsForcedLowered(
            positionX: 42, positionY: 60, grid, wallBuckets, LoweredWallDistanceWu,
            exemptFromLoweredRule: false, engagingIdentifiedHostile: false);

        Assert.True(isLowered);
    }

    [Fact]
    public void PositionOneWorldUnitBeyondLoweredWallDistance_IsNotForcedLowered()
    {
        var grid = NewGrid();
        var wallBuckets = WallBuckets.Build(grid, [50], [0], [50], [100]);
        const int LoweredWallDistanceWu = 8;

        // x = 41: distance to the wall is 9, one world unit past the
        // threshold that made the previous test's x = 42 lowered.
        var isLowered = WeaponLoweredRules.IsForcedLowered(
            positionX: 41, positionY: 60, grid, wallBuckets, LoweredWallDistanceWu,
            exemptFromLoweredRule: false, engagingIdentifiedHostile: false);

        Assert.False(isLowered);
    }

    [Fact]
    public void PositionWithinLoweredWallDistance_EngagingIdentifiedHostile_IsNotForcedLowered()
    {
        var grid = NewGrid();
        var wallBuckets = WallBuckets.Build(grid, [50], [0], [50], [100]);
        const int LoweredWallDistanceWu = 8;

        // The exact same position as the exact-threshold test above — proven
        // lowered when not engaging — but this time engaging an identified
        // hostile. Only the new flag differs.
        var isLoweredWhileEngaging = WeaponLoweredRules.IsForcedLowered(
            positionX: 42, positionY: 60, grid, wallBuckets, LoweredWallDistanceWu,
            exemptFromLoweredRule: false, engagingIdentifiedHostile: true);

        Assert.False(isLoweredWhileEngaging);
    }

    [Fact]
    public void PositionInsideDoorCell_IsForcedLowered()
    {
        var grid = NewGrid();

        // No walls at all: this test isolates the door-cell branch of the
        // rule from the wall-distance branch.
        var wallBuckets = WallBuckets.Build(grid, [], [], [], []);

        // Cell (5, 5) is tagged Door, exactly as NavBake.Bake would tag a
        // closed door's rasterised footprint.
        var doorCellIndex = grid.CellIndex(5, 5);
        grid.Passability[doorCellIndex] = NavCellFlags.Door;

        // (21, 21) falls inside cell (5, 5): 21 >> 2 == 5.
        var isLowered = WeaponLoweredRules.IsForcedLowered(
            positionX: 21, positionY: 21, grid, wallBuckets, loweredWallDistanceWu: 8,
            exemptFromLoweredRule: false, engagingIdentifiedHostile: false);

        Assert.True(isLowered);
    }

    [Fact]
    public void PositionInsideDoorCell_EngagingIdentifiedHostile_IsNotForcedLowered()
    {
        var grid = NewGrid();
        var wallBuckets = WallBuckets.Build(grid, [], [], [], []);

        var doorCellIndex = grid.CellIndex(5, 5);
        grid.Passability[doorCellIndex] = NavCellFlags.Door;

        // The exact same door-cell position as the test above — proven
        // lowered when not engaging — but this time engaging an identified
        // hostile. Only the new flag differs.
        var isLoweredWhileEngaging = WeaponLoweredRules.IsForcedLowered(
            positionX: 21, positionY: 21, grid, wallBuckets, loweredWallDistanceWu: 8,
            exemptFromLoweredRule: false, engagingIdentifiedHostile: true);

        Assert.False(isLoweredWhileEngaging);
    }

    [Fact]
    public void PositionInCellAdjacentToDoorCell_IsNotForcedLowered()
    {
        var grid = NewGrid();
        var wallBuckets = WallBuckets.Build(grid, [], [], [], []);

        var doorCellIndex = grid.CellIndex(5, 5);
        grid.Passability[doorCellIndex] = NavCellFlags.Door;

        // (25, 21) falls inside cell (6, 5) — the immediate neighbour of the
        // door cell along X — which was never tagged and defaults to
        // NavCellFlags.Blocked, not Door.
        var neighbourCellIndex = grid.CellIndex(6, 5);
        Assert.Equal(NavCellFlags.Blocked, grid.Passability[neighbourCellIndex]);

        var isLowered = WeaponLoweredRules.IsForcedLowered(
            positionX: 25, positionY: 21, grid, wallBuckets, loweredWallDistanceWu: 8,
            exemptFromLoweredRule: false, engagingIdentifiedHostile: false);

        Assert.False(isLowered);
    }

    [Fact]
    public void ExemptWeapon_IsNeverForcedLowered()
    {
        var grid = NewGrid();
        var wallBuckets = WallBuckets.Build(grid, [50], [0], [50], [100]);

        // Reuses the first test's exact-threshold position — the one
        // position in this file already proven to force a non-exempt
        // weapon lowered — so the only variable here is the exemption flag.
        var isLowered = WeaponLoweredRules.IsForcedLowered(
            positionX: 42, positionY: 60, grid, wallBuckets, loweredWallDistanceWu: 8,
            exemptFromLoweredRule: true, engagingIdentifiedHostile: false);

        Assert.False(isLowered);

        // Also true standing inside a door cell — a pistol carrier ignores
        // the doorway half of the rule too.
        var doorCellIndex = grid.CellIndex(5, 5);
        grid.Passability[doorCellIndex] = NavCellFlags.Door;

        var isLoweredInDoorway = WeaponLoweredRules.IsForcedLowered(
            positionX: 21, positionY: 21, grid, wallBuckets, loweredWallDistanceWu: 8,
            exemptFromLoweredRule: true, engagingIdentifiedHostile: false);

        Assert.False(isLoweredInDoorway);
    }

    [Fact]
    public void ExemptWeapon_EngagingIdentifiedHostile_IsStillNeverForcedLowered()
    {
        // Both early-out flags true at once: still false, and still without
        // ever evaluating the wall or door geometry — proven the same way
        // ExemptWeapon_IsNeverForcedLowered proves the exemption alone, by
        // reusing the exact-threshold position that a non-exempt,
        // non-engaging call forces lowered.
        var grid = NewGrid();
        var wallBuckets = WallBuckets.Build(grid, [50], [0], [50], [100]);

        var isLowered = WeaponLoweredRules.IsForcedLowered(
            positionX: 42, positionY: 60, grid, wallBuckets, loweredWallDistanceWu: 8,
            exemptFromLoweredRule: true, engagingIdentifiedHostile: true);

        Assert.False(isLowered);
    }

    [Fact]
    public void RaisePath_CostsExactlyReadyTicks_BeforeTheChainReachesAiming()
    {
        // ReadyMs drawn from design section 9's own published rifle figure —
        // 405 ms at 50 Hz — the same conversion WeaponChainTests pins for the
        // same figure.
        const int TickRate = 50;
        var readyTicks = TickConversion.ToTicks(milliseconds: 405, TickRate);
        Assert.Equal(20, readyTicks);

        const int AimTicks = 5;
        const int ResetTicks = 5;

        var grid = NewGrid();
        var wallBuckets = WallBuckets.Build(grid, [50], [0], [50], [100]);
        const int LoweredWallDistanceWu = 8;
        const int NearWallX = 42; // distance 8: forced lowered, per the first test above.
        const int FarFromWallX = 0; // distance 50: nowhere near the threshold.
        const int PositionY = 60;

        var forcedNearWall = WeaponLoweredRules.IsForcedLowered(
            NearWallX, PositionY, grid, wallBuckets, LoweredWallDistanceWu,
            exemptFromLoweredRule: false, engagingIdentifiedHostile: false);
        Assert.True(forcedNearWall);

        var forcedFarFromWall = WeaponLoweredRules.IsForcedLowered(
            FarFromWallX, PositionY, grid, wallBuckets, LoweredWallDistanceWu,
            exemptFromLoweredRule: false, engagingIdentifiedHostile: false);
        Assert.False(forcedFarFromWall);

        // While standing in the wall's zone, the rule holds the chain
        // Lowered even though a raise is being requested every tick.
        var confirmStillLowered = WeaponChain.Advance(
            WeaponChainPhase.Lowered, remainingTicks: 0,
            forceLowered: forcedNearWall, raiseRequested: true, arcWithinTolerance: true,
            readyTicks, aimTicks: AimTicks, resetTicks: ResetTicks);
        Assert.Equal(WeaponChainPhase.Lowered, confirmStillLowered.Phase);

        // The operator steps clear of the wall. forceLowered is now false,
        // so this call — the tick the raise is requested — enters Raising
        // with its counter freshly charged from readyTicks (per
        // WeaponChainTests' own precedent: the entering call is not itself
        // a charged tick).
        var entered = WeaponChain.Advance(
            WeaponChainPhase.Lowered, remainingTicks: 0,
            forceLowered: forcedFarFromWall, raiseRequested: true, arcWithinTolerance: true,
            readyTicks, aimTicks: AimTicks, resetTicks: ResetTicks);
        Assert.Equal(WeaponChainPhase.Raising, entered.Phase);
        Assert.Equal(readyTicks, entered.RemainingTicks);
        Assert.False(entered.Fired);

        // From here, arcWithinTolerance is already true, so Turning costs no
        // extra tick of its own: every further tick this loop spends before
        // Aiming is spent charging Raising's counter down from readyTicks to
        // zero — the raise's entire cost, and nothing else.
        var phase = entered.Phase;
        var remainingTicks = entered.RemainingTicks;
        var ticksSpent = 0;

        while (phase != WeaponChainPhase.Aiming)
        {
            var result = WeaponChain.Advance(
                phase, remainingTicks,
                forceLowered: false, raiseRequested: true, arcWithinTolerance: true,
                readyTicks, aimTicks: AimTicks, resetTicks: ResetTicks);

            phase = result.Phase;
            remainingTicks = result.RemainingTicks;
            ticksSpent++;

            // Bounds the loop so a defect in the chain cannot spin this test
            // forever instead of failing it.
            Assert.True(ticksSpent <= readyTicks + 1);
        }

        Assert.Equal(readyTicks, ticksSpent);
        Assert.Equal(AimTicks, remainingTicks);
    }

    [Fact]
    public void NullGrid_Throws()
    {
        var grid = NewGrid();
        var wallBuckets = WallBuckets.Build(grid, [], [], [], []);

        Assert.Throws<ArgumentNullException>(() =>
            WeaponLoweredRules.IsForcedLowered(
                0, 0, null!, wallBuckets, loweredWallDistanceWu: 8,
                exemptFromLoweredRule: false, engagingIdentifiedHostile: false));
    }

    [Fact]
    public void NullWallBuckets_Throws()
    {
        var grid = NewGrid();

        Assert.Throws<ArgumentNullException>(() =>
            WeaponLoweredRules.IsForcedLowered(
                0, 0, grid, null!, loweredWallDistanceWu: 8,
                exemptFromLoweredRule: false, engagingIdentifiedHostile: false));
    }

    [Fact]
    public void NegativeLoweredWallDistance_Throws()
    {
        var grid = NewGrid();
        var wallBuckets = WallBuckets.Build(grid, [], [], [], []);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WeaponLoweredRules.IsForcedLowered(
                0, 0, grid, wallBuckets, loweredWallDistanceWu: -1,
                exemptFromLoweredRule: false, engagingIdentifiedHostile: false));
    }
}
