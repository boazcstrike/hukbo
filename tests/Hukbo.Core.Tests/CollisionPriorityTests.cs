using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// The contested-ground priority contract: a strict total order that reshuffles
/// every tick, derived by hash rather than drawn from a stream, so that no agent
/// and therefore no faction holds a standing advantage.
/// </summary>
public sealed class CollisionPriorityTests
{
    /// <summary>
    /// Golden vectors for the mixer, in the manner of
    /// <see cref="HitLocationResolverTests"/>. Without them a reordered
    /// <c>Fnv1a.Add</c> sequence or an altered domain tag would silently change
    /// every contested-ground decision in the game and be caught only by a hash
    /// recorded in a Markdown file. These values are the algorithm's output, not
    /// a snapshot of convenient behaviour: if one fails, the mixer changed, and
    /// changing it retires every recorded oracle.
    /// </summary>
    [Theory]
    [InlineData(1UL, 1L, 1UL, 0x520586212978E040UL)]
    [InlineData(1UL, 1L, 2UL, 0xAEF5DB3C4A46BEA3UL)]
    [InlineData(0UL, 0L, 1UL, 0x29AF1994D3CDB260UL)]
    [InlineData(7UL, 42L, 13UL, 0xAF5B6B132961EA61UL)]
    [InlineData(0xDEADBEEFUL, 9_999L, 20_000UL, 0x950512FA59C45F9BUL)]
    public void MixMatchesItsGoldenVectors(
        ulong seed,
        long tick,
        ulong entityId,
        ulong expected) =>
        Assert.Equal(expected, CollisionPriority.Mix(seed, tick, entityId));

    [Fact]
    public void TheKeyIsAPureFunctionOfSeedTickAndEntity()
    {
        var first = CollisionPriority.Resolve(seed: 7, tick: 42, entityId: 13);
        var second = CollisionPriority.Resolve(seed: 7, tick: 42, entityId: 13);

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(7UL, 42L, 13UL, 8UL, 42L, 13UL)]
    [InlineData(7UL, 42L, 13UL, 7UL, 43L, 13UL)]
    [InlineData(7UL, 42L, 13UL, 7UL, 42L, 14UL)]
    public void ChangingAnyInputChangesTheKey(
        ulong firstSeed,
        long firstTick,
        ulong firstEntityId,
        ulong secondSeed,
        long secondTick,
        ulong secondEntityId)
    {
        var first = CollisionPriority.Resolve(firstSeed, firstTick, firstEntityId);
        var second = CollisionPriority.Resolve(secondSeed, secondTick, secondEntityId);

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// The low half carries the entity ID, which is what keeps the order strict
    /// even when two mixes collide in their top 32 bits. Without it the resolver
    /// would depend on how its sort happens to treat equal keys.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(200UL)]
    [InlineData(20_000UL)]
    public void TheLowHalfOfTheKeyIsTheEntityId(ulong entityId)
    {
        var key = CollisionPriority.Resolve(seed: 3, tick: 9, entityId);

        Assert.Equal(entityId, key & 0xFFFF_FFFFUL);
    }

    [Fact]
    public void EveryKeyInATickIsDistinct()
    {
        var keys = new HashSet<ulong>();

        for (var entityId = 1UL; entityId <= 2_000UL; entityId++)
        {
            Assert.True(
                keys.Add(CollisionPriority.Resolve(seed: 1, tick: 5, entityId)),
                $"Entity {entityId} produced a duplicate priority key.");
        }
    }

    /// <summary>
    /// The point of the whole change. Over a run of ticks, neither the low half
    /// of the entity range nor the high half — which is exactly how the two
    /// factions are numbered — may win contested ground consistently.
    /// </summary>
    [Fact]
    public void NeitherFactionsIdRangeWinsPriorityConsistently()
    {
        const int perFaction = 100;
        const int ticks = 200;
        var faction0Wins = 0;
        var faction1Wins = 0;

        for (var tick = 1L; tick <= ticks; tick++)
        {
            for (var localIndex = 0; localIndex < perFaction; localIndex++)
            {
                var faction0Key = CollisionPriority.Resolve(
                    seed: 1,
                    tick,
                    entityId: (ulong)localIndex + 1);
                var faction1Key = CollisionPriority.Resolve(
                    seed: 1,
                    tick,
                    entityId: (ulong)(perFaction + localIndex) + 1);

                if (faction0Key < faction1Key)
                {
                    faction0Wins++;
                }
                else
                {
                    faction1Wins++;
                }
            }
        }

        var total = faction0Wins + faction1Wins;
        Assert.Equal(perFaction * ticks, total);

        // A standing advantage looks like 100% for one side; the old rule scored
        // exactly that. A 45-to-55 band is wide enough that hash noise cannot
        // fail it and narrow enough that any systematic advantage would.
        Assert.InRange(faction0Wins, (total * 45) / 100, (total * 55) / 100);
    }

    /// <summary>
    /// One agent's priority relative to another must change across ticks.
    /// A key that reshuffled only between battles would still let one agent
    /// shoulder through the same opponent for the whole of one.
    /// </summary>
    [Fact]
    public void PriorityBetweenTwoAgentsChangesAcrossTicks()
    {
        var firstLeads = 0;
        var secondLeads = 0;

        for (var tick = 1L; tick <= 100L; tick++)
        {
            if (CollisionPriority.Resolve(seed: 1, tick, entityId: 1) <
                CollisionPriority.Resolve(seed: 1, tick, entityId: 101))
            {
                firstLeads++;
            }
            else
            {
                secondLeads++;
            }
        }

        Assert.True(firstLeads > 0, "Entity 1 never led entity 101.");
        Assert.True(secondLeads > 0, "Entity 101 never led entity 1.");
    }

    /// <summary>
    /// The seam test: the key the battle actually resolves with must be built
    /// from the tick being resolved, not from the seed alone. Nothing else in
    /// the suite would notice <c>Tick</c> being replaced by a constant, and the
    /// per-tick reshuffle is the entire acceptance criterion of the change.
    /// </summary>
    /// <remarks>
    /// Two allies converge on one distant enemy along paths that cross, so they
    /// contest ground repeatedly. The recorded sequence is the resolution of
    /// entity 1 over the first twelve ticks; it yields on exactly the ticks
    /// where the shuffle put entity 2 first. A tick-invariant key produces a
    /// different sequence, because the winner would then be the same on every
    /// tick. If a deliberate movement-rule change moves this sequence,
    /// re-record it and say which rule moved it — do not adjust it to match new
    /// output otherwise.
    /// </remarks>
    [Fact]
    public void TheContestSequenceFollowsThePerTickShuffle()
    {
        MovementResolution[] expected =
        [
            MovementResolution.Slid,
            MovementResolution.Moved,
            MovementResolution.Slid,
            MovementResolution.Moved,
            MovementResolution.Moved,
            MovementResolution.Slid,
            MovementResolution.Moved,
            MovementResolution.Moved,
            MovementResolution.Moved,
            MovementResolution.Moved,
            MovementResolution.Moved,
            MovementResolution.Moved,
        ];
        var scenario = new Scenario(
            Seed: 3,
            MapWidth: 200,
            MapHeight: 200,
            AgentsPerFaction: 2,
            TickRate: 20,
            TickLimit: 1_000);
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            Agent(1, factionId: 0, 60 * FixedPoint.Scale, 46 * FixedPoint.Scale, scenario),
            Agent(2, factionId: 0, 60 * FixedPoint.Scale, 54 * FixedPoint.Scale, scenario),
            Agent(3, factionId: 1, 190 * FixedPoint.Scale, 50 * FixedPoint.Scale, scenario));

        var actual = new List<MovementResolution>(expected.Length);
        var shuffleFavouredTheSecondAlly = 0;

        for (var index = 0; index < expected.Length; index++)
        {
            simulation.AdvanceOneTick();
            actual.Add(simulation.Agents[0].MovementResolution);

            if (CollisionPriority.Resolve(scenario.Seed, simulation.Tick, 2) <
                CollisionPriority.Resolve(scenario.Seed, simulation.Tick, 1))
            {
                shuffleFavouredTheSecondAlly++;
            }
        }

        Assert.Equal(expected, actual);

        // The window must contain both orders, or the sequence above would be
        // consistent with a key that never looks at the tick.
        Assert.InRange(shuffleFavouredTheSecondAlly, 1, expected.Length - 1);
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData((ulong)uint.MaxValue + 1)]
    public void RejectsAnEntityIdThatCannotFitTheLowHalf(ulong entityId) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CollisionPriority.Resolve(seed: 1, tick: 1, entityId));

    private static AgentState Agent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario) =>
        new(
            entityId,
            factionId,
            xRaw,
            yRaw,
            scenario.MaximumHitPoints,
            scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            scenario.AttackRangeRaw,
            scenario.DamagePerAttack,
            scenario.AttackCooldownTicks,
            new CombatLoadout(
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.None));

    [Fact]
    public void RejectsANegativeTick() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CollisionPriority.Resolve(seed: 1, tick: -1, entityId: 1));
}
