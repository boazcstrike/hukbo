using System;
using System.Linq;
using Hukbo.Core.Mathematics;
using Sandata.Client.UI;
using Sandata.Core.Combat;
using Sandata.Core.Mathematics;
using Sandata.Core.Navigation;
using Sandata.Core.Weapons;

namespace Sandata.Client.Tests;

/// <summary>
/// Task 38's done-when bar for the operator inspector: every listed field is
/// present in <see cref="OperatorInspector.BuildLines"/>'s output, including
/// design section 16's three order-layer rows and design section 5's
/// decision/resolution position pair. No member of this class constructs a
/// <c>GraphicsDevice</c>, a <c>SpriteBatch</c>, or a <c>SandataGame</c>.
/// </summary>
public sealed class OperatorInspectorTests
{
    private static OperatorInspector.InspectorContent CreateContent(
        int? slotIndex = 3,
        ulong? activeOrderId = 7,
        int? orderNodeIndex = 2,
        int? orderClearReasonCode = 1,
        FirearmId firearm = FirearmId.Ak47,
        bool weaponLowered = false) =>
        new(
            Intent: 2,
            ReasonCode: PathReasonCode.PathValid,
            ChainPhase: WeaponChainPhase.Aiming,
            ChainRemainingTicks: 5,
            Cover: new CoverState(
                InCover: true,
                ArcCentreBam: new Bam16(16_384),
                ArcHalfBam: 8_192,
                Posture: CoverPosture.Crouched),
            GroupId: 42UL,
            SlotIndex: slotIndex,
            DecisionPositionX: FixedPoint.FromWhole(10),
            DecisionPositionY: FixedPoint.FromWhole(20),
            ResolutionPositionX: FixedPoint.FromWhole(11),
            ResolutionPositionY: FixedPoint.FromWhole(21),
            ActiveOrderId: activeOrderId,
            OrderNodeIndex: orderNodeIndex,
            OrderClearReasonCode: orderClearReasonCode,
            Firearm: firearm,
            WeaponLowered: weaponLowered);

    /// <summary>
    /// Decision D2 of the 2026-08-14 lowered-weapon design: the inspector
    /// names the weapon, so a tester can tell which of two operators walking
    /// the same route is the rifle carrier whose weapon is supposed to lower.
    /// </summary>
    [Fact]
    public void BuildLines_NamesTheFirearmAndItsClass()
    {
        var lines = OperatorInspector.BuildLines(CreateContent(firearm: FirearmId.Ak47));

        var weaponLine = Assert.Single(lines, line => line.StartsWith("Weapon:", StringComparison.Ordinal));
        Assert.Contains(
            WeaponNameSets.GetName(FirearmId.Ak47, OperatorInspector.NameSet),
            weaponLine,
            StringComparison.Ordinal);
        Assert.Contains(WeaponClass.Rifle.ToString(), weaponLine, StringComparison.Ordinal);
    }

    /// <summary>
    /// The pistol carrier's row names a pistol, which is the other half of
    /// the comparison smoke row <c>SD-4</c> asks a person to make.
    /// </summary>
    [Fact]
    public void BuildLines_NamesAPistolAsAPistol()
    {
        var lines = OperatorInspector.BuildLines(CreateContent(firearm: FirearmId.Glock17Gen5));

        var weaponLine = Assert.Single(lines, line => line.StartsWith("Weapon:", StringComparison.Ordinal));
        Assert.Contains(WeaponClass.Pistol.ToString(), weaponLine, StringComparison.Ordinal);
    }

    /// <summary>
    /// The weapon-state row reads the flag straight through, both ways round.
    /// It is the row that makes a half-second transition legible on a paused,
    /// single-stepped run.
    /// </summary>
    [Theory]
    [InlineData(true, "Weapon state: lowered")]
    [InlineData(false, "Weapon state: raised")]
    public void BuildLines_ReportsTheWeaponState(bool weaponLowered, string expected)
    {
        var lines = OperatorInspector.BuildLines(CreateContent(weaponLowered: weaponLowered));

        Assert.Contains(expected, lines);
    }

    [Fact]
    public void BuildLines_ReturnsExactlyLineCountEntries()
    {
        var lines = OperatorInspector.BuildLines(CreateContent());

        Assert.Equal(OperatorInspector.LineCount, lines.Count);
    }

    [Fact]
    public void BuildLines_IncludesIntent()
    {
        var lines = OperatorInspector.BuildLines(CreateContent());

        Assert.Contains("Intent: 2", lines);
    }

    [Fact]
    public void BuildLines_IncludesReasonCode()
    {
        var lines = OperatorInspector.BuildLines(CreateContent());

        Assert.Contains("Reason: PathValid", lines);
    }

    [Fact]
    public void BuildLines_IncludesChainPhaseAndRemainingTicks()
    {
        var lines = OperatorInspector.BuildLines(CreateContent());

        Assert.Contains("Chain: Aiming (5t)", lines);
    }

    [Fact]
    public void BuildLines_IncludesCoverStateAndArc()
    {
        var lines = OperatorInspector.BuildLines(CreateContent());

        Assert.Contains("Cover: Crouched arc 16384±8192", lines);
    }

    [Fact]
    public void BuildLines_ReportsNoCoverWhenNotAffiliated()
    {
        var content = CreateContent() with { Cover = CoverState.NotInCover };

        var lines = OperatorInspector.BuildLines(content);

        Assert.Contains("Cover: none", lines);
    }

    [Fact]
    public void BuildLines_IncludesGroupId()
    {
        var lines = OperatorInspector.BuildLines(CreateContent());

        Assert.Contains("Group: 42", lines);
    }

    [Fact]
    public void BuildLines_IncludesSlotIndexWhenPresent()
    {
        var lines = OperatorInspector.BuildLines(CreateContent(slotIndex: 3));

        Assert.Contains("Slot: 3", lines);
    }

    [Fact]
    public void BuildLines_ReportsNoSlotWhenTheOperatorBelongsToNoGroup()
    {
        var lines = OperatorInspector.BuildLines(CreateContent(slotIndex: null));

        Assert.Contains("Slot: -", lines);
    }

    [Fact]
    public void BuildLines_IncludesBothTheDecisionAndResolutionPositions()
    {
        var lines = OperatorInspector.BuildLines(CreateContent());

        Assert.Contains("Decision pos: (10.00, 20.00)", lines);
        Assert.Contains("Resolution pos: (11.00, 21.00)", lines);
    }

    [Fact]
    public void BuildLines_DecisionAndResolutionPositionsCanDiffer()
    {
        var content = CreateContent();

        Assert.NotEqual(content.DecisionPositionX, content.ResolutionPositionX);
        Assert.NotEqual(content.DecisionPositionY, content.ResolutionPositionY);
    }

    [Fact]
    public void BuildLines_IncludesTheActiveOrderId()
    {
        var lines = OperatorInspector.BuildLines(CreateContent(activeOrderId: 7));

        Assert.Contains("Order: 7", lines);
    }

    [Fact]
    public void BuildLines_ReportsNoOrderWhenNoneIsActive()
    {
        var lines = OperatorInspector.BuildLines(CreateContent(activeOrderId: null));

        Assert.Contains("Order: none", lines);
    }

    [Fact]
    public void BuildLines_IncludesTheOrderNodeIndexBeingWalked()
    {
        var lines = OperatorInspector.BuildLines(CreateContent(orderNodeIndex: 2));

        Assert.Contains("Node: 2", lines);
    }

    [Fact]
    public void BuildLines_ReportsNoNodeWhenNoOrderIsActive()
    {
        var lines = OperatorInspector.BuildLines(CreateContent(orderNodeIndex: null));

        Assert.Contains("Node: -", lines);
    }

    [Fact]
    public void BuildLines_IncludesTheReasonCodeThatClearedTheLastAssignment()
    {
        var lines = OperatorInspector.BuildLines(CreateContent(orderClearReasonCode: 1));

        Assert.Contains("Order cleared: 1", lines);
    }

    [Fact]
    public void BuildLines_ReportsNoClearReasonWhenNoAssignmentHasEverBeenCleared()
    {
        var lines = OperatorInspector.BuildLines(CreateContent(orderClearReasonCode: null));

        Assert.Contains("Order cleared: -", lines);
    }
}
