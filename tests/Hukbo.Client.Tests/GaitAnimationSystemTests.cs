using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

/// <summary>
/// Covers <see cref="GaitAnimationSystem"/>'s store shape: phase advanced by
/// distance travelled per ingested tick, idle easing back toward a neutral
/// phase, the per-entity deterministic phase offset, entity drop on death or
/// absence, and the fixed-capacity, zero-growth array.
/// </summary>
public sealed class GaitAnimationSystemTests
{
    [Fact]
    public void NoIngestCall_LeavesNoEntries()
    {
        var system = new GaitAnimationSystem(capacity: 8);

        Assert.Empty(system.ActiveEntries.ToArray());
        Assert.False(system.TryGetEntry(1, out _));
    }

    [Fact]
    public void Ingest_FirstSightingOfAWarriorResolvesStance()
    {
        var system = new GaitAnimationSystem(capacity: 8);

        system.Ingest([Agent(2, 0, 0)]);

        Assert.True(system.TryGetEntry(2, out var entry));
        Assert.Equal(GaitMode.Stance, entry.Mode);
    }

    [Fact]
    public void Ingest_WalkMagnitudeDisplacementResolvesWalkMode()
    {
        var system = new GaitAnimationSystem(capacity: 8);
        system.Ingest([Agent(2, 0, 0)]);

        system.Ingest([Agent(2, 400, 0)]);

        Assert.True(system.TryGetEntry(2, out var entry));
        Assert.Equal(GaitMode.Walk, entry.Mode);
        Assert.True(entry.PhaseTurns > 0f);
    }

    [Fact]
    public void Ingest_RunMagnitudeDisplacementResolvesRunMode()
    {
        var system = new GaitAnimationSystem(capacity: 8);
        system.Ingest([Agent(2, 0, 0)]);

        system.Ingest([Agent(2, 2000, 0)]);

        Assert.True(system.TryGetEntry(2, out var entry));
        Assert.Equal(GaitMode.Run, entry.Mode);
    }

    [Fact]
    public void Ingest_TwoTicksAtIdenticalPositionsEasesPhaseTowardNeutralAndStaysStance()
    {
        var system = new GaitAnimationSystem(capacity: 8);
        system.Ingest([Agent(2, 0, 0)]);
        system.Ingest([Agent(2, 400, 0)]);
        Assert.True(system.TryGetEntry(2, out var moving));
        var phaseWhileMoving = moving.PhaseTurns;

        system.Ingest([Agent(2, 400, 0)]);
        system.Ingest([Agent(2, 400, 0)]);

        Assert.True(system.TryGetEntry(2, out var idle));
        Assert.Equal(GaitMode.Stance, idle.Mode);
        // Eased toward the nearer neutral point (a multiple of half a turn),
        // not left exactly where it stopped.
        var nearestNeutral = MathF.Round(phaseWhileMoving * 2f) / 2f;
        Assert.True(
            MathF.Abs(idle.PhaseTurns - nearestNeutral) <
            MathF.Abs(phaseWhileMoving - nearestNeutral));
    }

    [Fact]
    public void Ingest_NoTickAfterTheFirstAdvancesNoPhase()
    {
        var system = new GaitAnimationSystem(capacity: 8);
        system.Ingest([Agent(2, 0, 0)]);
        system.TryGetEntry(2, out var before);

        // No further Ingest call: state cannot change without one.
        system.TryGetEntry(2, out var after);

        Assert.Equal(before, after);
    }

    [Fact]
    public void Ingest_TwoEntitiesMovingIdenticallyHoldDifferentPhases()
    {
        var system = new GaitAnimationSystem(capacity: 8);
        system.Ingest([Agent(2, 0, 0), Agent(3, 0, 0)]);

        system.Ingest([Agent(2, 400, 0), Agent(3, 400, 0)]);

        Assert.True(system.TryGetEntry(2, out var entryTwo));
        Assert.True(system.TryGetEntry(3, out var entryThree));
        Assert.NotEqual(entryTwo.PhaseTurns, entryThree.PhaseTurns);
    }

    [Fact]
    public void Ingest_DropsAWarriorThatDies()
    {
        var system = new GaitAnimationSystem(capacity: 8);
        system.Ingest([Agent(2, 0, 0)]);
        Assert.True(system.TryGetEntry(2, out _));

        system.Ingest([Agent(2, 0, 0, isAlive: false)]);

        Assert.False(system.TryGetEntry(2, out _));
    }

    [Fact]
    public void Ingest_DropsAWarriorAbsentFromTheViews()
    {
        var system = new GaitAnimationSystem(capacity: 8);
        system.Ingest([Agent(2, 0, 0), Agent(3, 0, 0)]);

        system.Ingest([Agent(3, 0, 0)]);

        Assert.False(system.TryGetEntry(2, out _));
        Assert.True(system.TryGetEntry(3, out _));
    }

    [Fact]
    public void Ingest_NeverExceedsCapacity()
    {
        const int Capacity = 2;
        var system = new GaitAnimationSystem(Capacity);

        system.Ingest([Agent(1, 0, 0), Agent(2, 0, 0), Agent(3, 0, 0), Agent(4, 0, 0)]);

        Assert.True(system.ActiveEntries.Length <= Capacity);
    }

    [Fact]
    public void Clear_ResetsEverything()
    {
        var system = new GaitAnimationSystem(capacity: 8);
        system.Ingest([Agent(2, 0, 0)]);

        system.Clear();

        Assert.Empty(system.ActiveEntries.ToArray());
        Assert.False(system.TryGetEntry(2, out _));
    }

    private static AgentView Agent(
        ulong entityId,
        int xRaw,
        int yRaw,
        bool isAlive = true) =>
        new(
            entityId,
            FactionId: 0,
            xRaw,
            yRaw,
            HitPoints: 100,
            MaximumHitPoints: 100,
            TargetEntityId: null,
            Intent: AgentIntent.Idle,
            IsAlive: isAlive,
            Loadout: new CombatLoadout(
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.TallHardwood));
}
