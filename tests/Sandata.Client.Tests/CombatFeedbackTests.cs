using System;
using System.Collections.Immutable;
using System.Linq;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Microsoft.Xna.Framework;
using Sandata.Client.Rendering;
using Sandata.Core.Events;
using Sandata.Core.Mathematics;
using Sandata.Core.Simulation;
using Sandata.Core.Weapons;

namespace Sandata.Client.Tests;

/// <summary>
/// <see cref="CombatFeedback"/>'s bar: a fired shot produces a mark, a shot
/// fired on some other tick does not, a wounded operator produces an impact,
/// and the marks expire.
/// </summary>
/// <remarks>
/// <para>
/// Every test here is a pure function call. Nothing constructs a
/// <c>SandataGame</c>, a <c>GraphicsDevice</c>, a <c>SpriteBatch</c>, or a
/// window, and nothing depends on GPU, audio, focus, network, or the wall
/// clock — the client-test rule in <c>CLAUDE.md</c> section 5.
/// </para>
/// <para>
/// <b>What this file does not bind.</b> It says nothing about where a mark is
/// drawn on the screen, what colour it is, or whether a person can see it.
/// Those are questions for the smoke checklist in
/// <c>docs/development/testing.md</c> and no test here may be read as having
/// answered them. What it does bind is that the marks are produced at all,
/// which is precisely what was broken.
/// </para>
/// </remarks>
public sealed class CombatFeedbackTests
{
    private const ulong ShooterId = 1UL;
    private const ulong TargetId = 2UL;

    private static OperatorState BuildOperator(
        ulong entityId,
        int positionXWu = 0,
        int positionYWu = 0,
        int health = 100,
        Facing16? facing = null) => new(
            EntityId: entityId,
            PositionX: FixedPoint.FromWhole(positionXWu),
            PositionY: FixedPoint.FromWhole(positionYWu),
            Facing: facing ?? Facing16.East,
            AimAngle: Bam16.FromFacing16(facing ?? Facing16.East),
            Health: health,
            Faction: 0,
            Intent: 0,
            IsCrouched: false,
            WeaponLowered: false,
            WeaponChainPhase: 0,
            WeaponChainRemainingTicks: 0,
            MagazineRounds: 30,
            CyclicFireAccumulator: 0,
            SuppressionCounter: 0);

    private static MissionEventFeed FeedWithShot(long tick, ulong shooterEntityId, long sequence = 0) =>
        MissionEventFeed.Empty.Append(MissionEvent.ShotFired(sequence, tick, shooterEntityId));

    // ---- The shot produces marks ----------------------------------------

    [Fact]
    public void ObserveTick_AShotFiredThisTick_ProducesAMuzzleFlashAndATracerForTheShooter()
    {
        var shooter = BuildOperator(ShooterId, positionXWu: 40, positionYWu: 60);
        var operators = ImmutableArray.Create(shooter);

        var effects = CombatFeedback.ObserveTick(
            FeedWithShot(tick: 7, shooterEntityId: ShooterId),
            operators,
            CombatFeedback.CaptureHealth(operators),
            executedTick: 7);

        var flash = Assert.Single(effects.Where(e => e.Kind == CombatEffectKind.MuzzleFlash));
        Assert.Equal(ShooterId, flash.EntityId);
        Assert.Equal(new Vector2(40f, 60f), flash.StartWu);

        var tracer = Assert.Single(effects.Where(e => e.Kind == CombatEffectKind.Tracer));
        Assert.Equal(ShooterId, tracer.EntityId);
        Assert.Equal(new Vector2(40f, 60f), tracer.StartWu);

        // Facing16.East is +X, so the tracer ends a full TracerLengthWu to the
        // right of the shooter. Asserted as a distance and a direction, not as
        // "the endpoint differs from the start" — a zero-length tracer would
        // satisfy the weaker claim and draw nothing.
        Assert.Equal(40f + CombatFeedback.TracerLengthWu, tracer.EndWu.X, 3);
        Assert.Equal(60f, tracer.EndWu.Y, 3);
    }

    /// <summary>
    /// The decisive one for the retained feed.
    /// <see cref="MissionEventFeed"/> keeps the last 200 events, so without a
    /// tick filter every shot ever fired would re-flash on every subsequent
    /// tick for the rest of the run.
    /// </summary>
    [Fact]
    public void ObserveTick_AShotFiredOnAnEarlierTick_ProducesNothing()
    {
        var operators = ImmutableArray.Create(BuildOperator(ShooterId));

        var effects = CombatFeedback.ObserveTick(
            FeedWithShot(tick: 7, shooterEntityId: ShooterId),
            operators,
            CombatFeedback.CaptureHealth(operators),
            executedTick: 8);

        Assert.Empty(effects);
    }

    /// <summary>
    /// The tick filter above is only meaningful because
    /// <c>SandataSimulation.RunTick</c> writes <c>MissionState.Tick</c>. Until
    /// 2026-08-11 every event carried tick 0, so this filter would have
    /// matched every shot in the feed on tick 0 and none of them ever again.
    /// This test states that dependency out loud so a future change that
    /// re-breaks the tick cannot quietly turn the feature off.
    /// </summary>
    [Fact]
    public void ObserveTick_EventsStampedZeroWhileTheRunIsElsewhere_ProduceNothing()
    {
        var operators = ImmutableArray.Create(BuildOperator(ShooterId));

        var effects = CombatFeedback.ObserveTick(
            FeedWithShot(tick: 0, shooterEntityId: ShooterId),
            operators,
            CombatFeedback.CaptureHealth(operators),
            executedTick: 459);

        Assert.Empty(effects);
    }

    [Fact]
    public void ObserveTick_AShotByAnEntityNotOnTheMap_ProducesNothing()
    {
        var operators = ImmutableArray.Create(BuildOperator(ShooterId));

        var effects = CombatFeedback.ObserveTick(
            FeedWithShot(tick: 3, shooterEntityId: 9_999UL),
            operators,
            CombatFeedback.CaptureHealth(operators),
            executedTick: 3);

        Assert.Empty(effects);
    }

    // ---- The wound produces a mark --------------------------------------

    [Fact]
    public void ObserveTick_AnOperatorThatLostHealth_ProducesAnImpactAtItsPosition()
    {
        var before = ImmutableArray.Create(
            BuildOperator(ShooterId, health: 100),
            BuildOperator(TargetId, positionXWu: 90, positionYWu: 12, health: 100));
        var healthBefore = CombatFeedback.CaptureHealth(before);

        var after = ImmutableArray.Create(
            BuildOperator(ShooterId, health: 100),
            BuildOperator(TargetId, positionXWu: 90, positionYWu: 12, health: 64));

        var effects = CombatFeedback.ObserveTick(
            MissionEventFeed.Empty, after, healthBefore, executedTick: 5);

        var impact = Assert.Single(effects.Where(e => e.Kind == CombatEffectKind.Impact));
        Assert.Equal(TargetId, impact.EntityId);
        Assert.Equal(new Vector2(90f, 12f), impact.StartWu);
    }

    [Fact]
    public void ObserveTick_NobodyLostHealth_ProducesNoImpact()
    {
        var operators = ImmutableArray.Create(
            BuildOperator(ShooterId), BuildOperator(TargetId, positionXWu: 90));

        var effects = CombatFeedback.ObserveTick(
            MissionEventFeed.Empty,
            operators,
            CombatFeedback.CaptureHealth(operators),
            executedTick: 5);

        Assert.Empty(effects);
    }

    /// <summary>
    /// Healing, or any upward health change, is not an impact. Asserted
    /// because <c>op.Health &gt;= before</c> and <c>op.Health != before</c>
    /// are one keystroke apart and only one of them is right.
    /// </summary>
    [Fact]
    public void ObserveTick_AnOperatorThatGainedHealth_ProducesNoImpact()
    {
        var before = ImmutableArray.Create(BuildOperator(TargetId, health: 40));
        var healthBefore = CombatFeedback.CaptureHealth(before);
        var after = ImmutableArray.Create(BuildOperator(TargetId, health: 80));

        var effects = CombatFeedback.ObserveTick(
            MissionEventFeed.Empty, after, healthBefore, executedTick: 5);

        Assert.Empty(effects);
    }

    // ---- The always-false flag this class replaced -----------------------

    /// <summary>
    /// Why <see cref="CombatFeedback"/> exists at all, stated as an
    /// executable claim rather than a comment: an
    /// <see cref="OperatorState"/> that <c>WeaponChain.Advance</c> has just
    /// resolved a shot for does not hold
    /// <see cref="WeaponChainPhase.Firing"/>, so the old
    /// <c>WeaponChainPhase == Firing</c> gate on the muzzle-flash layer could
    /// never be true. Anyone who reintroduces that gate fails here.
    /// </summary>
    [Fact]
    public void WeaponChainAdvance_NeverReturnsFiringAsThePhaseToHold()
    {
        var phases = Enum.GetValues<WeaponChainPhase>();

        // Firing is a real member of the enum -- the claim is not that it does
        // not exist, but that it is never a phase Advance hands back to hold.
        Assert.Contains(WeaponChainPhase.Firing, phases);

        var swept = 0;
        foreach (var phase in phases)
        {
            foreach (var remainingTicks in new[] { 0, 1, 5 })
            {
                foreach (var forceLowered in new[] { false, true })
                {
                    foreach (var raiseRequested in new[] { false, true })
                    {
                        foreach (var arcWithinTolerance in new[] { false, true })
                        {
                            var resolved = WeaponChain.Advance(
                                phase,
                                remainingTicks,
                                forceLowered,
                                raiseRequested,
                                arcWithinTolerance,
                                readyTicks: 3,
                                aimTicks: 4,
                                resetTicks: 4);

                            Assert.NotEqual(WeaponChainPhase.Firing, resolved.Phase);
                            swept++;
                        }
                    }
                }
            }
        }

        // The sweep has to have actually run, or the assertion above proves
        // nothing: every enum member, three tick counts, three flags.
        Assert.Equal(phases.Length * 3 * 2 * 2 * 2, swept);
        Assert.True(swept >= 72, $"the sweep covered only {swept} combinations");
    }

    // ---- Lifetime and capacity -------------------------------------------

    [Fact]
    public void Age_DecrementsEveryEffectAndDropsTheOnesThatExpire()
    {
        var effects = ImmutableArray.Create(
            new CombatEffect(CombatEffectKind.Tracer, ShooterId, Vector2.Zero, Vector2.One, 1),
            new CombatEffect(CombatEffectKind.Impact, TargetId, Vector2.Zero, Vector2.Zero, 3));

        var aged = CombatFeedback.Age(effects);

        var survivor = Assert.Single(aged);
        Assert.Equal(CombatEffectKind.Impact, survivor.Kind);
        Assert.Equal(2, survivor.RemainingFrames);
    }

    /// <summary>
    /// An effect with one frame left is drawn on the frame it was created and
    /// gone on the next, so a tracer authored with
    /// <see cref="CombatFeedback.TracerFrames"/> frames is visible for exactly
    /// that many.
    /// </summary>
    [Fact]
    public void Age_AnEffectSurvivesExactlyItsAuthoredFrameCount()
    {
        var effects = ImmutableArray.Create(
            new CombatEffect(
                CombatEffectKind.Tracer, ShooterId, Vector2.Zero, Vector2.One,
                CombatFeedback.TracerFrames));

        for (var frame = 1; frame < CombatFeedback.TracerFrames; frame++)
        {
            effects = CombatFeedback.Age(effects);
            Assert.False(effects.IsEmpty, $"the tracer vanished after {frame} frames");
        }

        Assert.Empty(CombatFeedback.Age(effects));
    }

    [Fact]
    public void Append_CapsTheLiveSetAndDropsTheOldestFirst()
    {
        var existing = ImmutableArray.CreateRange(
            Enumerable.Range(0, CombatFeedback.MaximumLiveEffects)
                .Select(i => new CombatEffect(
                    CombatEffectKind.Impact, (ulong)i, Vector2.Zero, Vector2.Zero, 9)));

        var added = ImmutableArray.Create(
            new CombatEffect(CombatEffectKind.Tracer, 777UL, Vector2.Zero, Vector2.One, 9));

        var result = CombatFeedback.Append(existing, added);

        Assert.Equal(CombatFeedback.MaximumLiveEffects, result.Length);
        // The newest survived and the oldest is the one that went.
        Assert.Equal(777UL, result[^1].EntityId);
        Assert.DoesNotContain(result, e => e.EntityId == 0UL && e.Kind == CombatEffectKind.Impact);
    }

    [Fact]
    public void IsFiring_IsTrueOnlyForAnEntityHoldingALiveMuzzleFlash()
    {
        var effects = ImmutableArray.Create(
            new CombatEffect(CombatEffectKind.MuzzleFlash, ShooterId, Vector2.Zero, Vector2.Zero, 2),
            new CombatEffect(CombatEffectKind.Impact, TargetId, Vector2.Zero, Vector2.Zero, 9));

        Assert.True(CombatFeedback.IsFiring(effects, ShooterId));
        // TargetId has an effect, but not a muzzle flash: being shot is not
        // firing, and a kind-blind lookup would get this wrong.
        Assert.False(CombatFeedback.IsFiring(effects, TargetId));
        Assert.False(CombatFeedback.IsFiring(ImmutableArray<CombatEffect>.Empty, ShooterId));
    }

    [Fact]
    public void AimDirection_UsesTheSameConventionAsTheOperatorGeometry()
    {
        var east = CombatFeedback.AimDirection(Bam16.FromFacing16(Facing16.East));
        Assert.Equal(1f, east.X, 3);
        Assert.Equal(0f, east.Y, 3);

        // Y increases downward, so South is +Y.
        var south = CombatFeedback.AimDirection(Bam16.FromFacing16(Facing16.South));
        Assert.Equal(0f, south.X, 3);
        Assert.Equal(1f, south.Y, 3);
    }
}
