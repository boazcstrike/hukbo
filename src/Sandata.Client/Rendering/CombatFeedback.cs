using System;
using System.Collections.Immutable;
using Microsoft.Xna.Framework;
using Sandata.Core.Events;
using Sandata.Core.Mathematics;
using Sandata.Core.Simulation;

namespace Sandata.Client.Rendering;

/// <summary>
/// What kind of short-lived combat mark a <see cref="CombatEffect"/> is.
/// </summary>
internal enum CombatEffectKind
{
    /// <summary>
    /// A one-shot flash at the shooter's muzzle. Carries the shooter's
    /// <see cref="OperatorState.EntityId"/> in
    /// <see cref="CombatEffect.EntityId"/> so the operator draw loop can ask
    /// "is this operator firing right now" without a position comparison.
    /// </summary>
    MuzzleFlash,

    /// <summary>A line from the shooter's position along its aim angle.</summary>
    Tracer,

    /// <summary>A mark at an operator that lost health this tick.</summary>
    Impact,
}

/// <summary>
/// One short-lived combat mark, in world units, with a frame countdown.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="RemainingFrames"/> counts <em>frames</em> rather than mission
/// ticks, and that is deliberate. Sandata runs at 50 Hz, so one tick is 20
/// milliseconds — at a 60 Hz display that is less than one rendered frame, and
/// at quadruple speed four ticks can resolve inside a single frame. An effect
/// measured in ticks would therefore be invisible at exactly the speeds a
/// person is most likely to be watching. These marks are presentation only:
/// nothing here reaches a state hash, an event, or a snapshot, so a
/// frame-based lifetime costs nothing and is the only one that reads.
/// </para>
/// <para>
/// For <see cref="CombatEffectKind.MuzzleFlash"/> and
/// <see cref="CombatEffectKind.Impact"/>, <see cref="EndWu"/> equals
/// <see cref="StartWu"/> and carries no meaning.
/// </para>
/// </remarks>
internal readonly record struct CombatEffect(
    CombatEffectKind Kind,
    ulong EntityId,
    Vector2 StartWu,
    Vector2 EndWu,
    int RemainingFrames);

/// <summary>
/// Turns what the simulation did on a tick into the short-lived marks that
/// make a firefight visible. Pure: every method takes the state the caller
/// already holds and returns a new array, and nothing here touches a
/// <c>GraphicsDevice</c>, a <c>SpriteBatch</c>, or the wall clock, so all of
/// it is testable without a window.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this class exists.</b> Before 2026-08-11 a Sandata firefight
/// rendered nothing whatsoever. <c>OperatorGeometry</c> has had a muzzle-flash
/// layer since it was written, gated on an <c>isFiring</c> flag that
/// <c>SandataGame</c> supplied as
/// <c>operatorState.WeaponChainPhase == (int)WeaponChainPhase.Firing</c>.
/// That comparison is always false. <c>WeaponChain</c>'s own remarks state
/// that <see cref="Sandata.Core.Weapons.WeaponChainPhase.Firing"/> "is not a
/// wait: entering it always records one resolved shot and moves on to
/// <c>Resetting</c> within the same pass, so it is never the phase this method
/// returns" — the phase stored on an <see cref="OperatorState"/> can therefore
/// never hold it. The flash layer was real, tested, and unreachable. Two
/// operators would close, one would silently stop moving, and the first person
/// to watch a run read it as melee combat, which is the correct reading of
/// what was on the screen.
/// </para>
/// <para>
/// <b>Why the shooter comes from the event feed and the casualty does not.</b>
/// <see cref="MissionEventKind.ShotFired"/> carries the shooter in
/// <see cref="MissionEvent.SubjectId"/>, so a muzzle flash and a tracer are
/// exactly what that event can support. It carries no target and no impact
/// point — <see cref="MissionEventKind.ShotHit"/>'s subject is the shooter
/// too — so an impact mark cannot be derived from the feed at all. It is
/// derived instead from a health drop between the state before the tick and
/// the state after it, which the client already holds both halves of. That
/// keeps the whole of this file presentation-only: widening
/// <see cref="MissionEvent"/> with a target id would move Sandata's event
/// hash, and nothing here is worth a preset conversation.
/// </para>
/// <para>
/// <b>What the tracer does not claim.</b> It is drawn from the shooter along
/// its own aim angle for <see cref="TracerLengthWu"/> world units. It is not
/// a ballistic path, it does not stop at what it hit, and it is not aimed at
/// the operator the impact mark appears on. Pairing a specific shot with a
/// specific casualty is not possible from the event feed as it stands, and
/// guessing the pairing would draw a confident lie on ticks where two
/// operators fire at once.
/// </para>
/// </remarks>
internal static class CombatFeedback
{
    /// <summary>
    /// Frames a tracer stays on screen. Short: a tracer that lingers reads as
    /// a laser sight rather than a shot.
    /// </summary>
    internal const int TracerFrames = 4;

    /// <summary>Frames a muzzle flash stays on screen.</summary>
    internal const int MuzzleFlashFrames = 3;

    /// <summary>
    /// Frames an impact mark stays on screen. Longer than the other two
    /// because a casualty is the thing a viewer most needs time to notice,
    /// and because it is the only mark that explains why an operator stopped.
    /// </summary>
    internal const int ImpactFrames = 14;

    /// <summary>
    /// How far a tracer is drawn, in world units. 320 wu is 20 metres at
    /// Sandata's 16-world-units-per-metre scale — long enough to read as a
    /// direction at the zoom tiers the map is played at, short enough not to
    /// cross the whole 640-by-720 wu map from any position.
    /// </summary>
    internal const float TracerLengthWu = 320f;

    /// <summary>
    /// A total cap on live effects. A mark is presentation, and a frame that
    /// tried to draw thousands of them would cost real time on a busy tick
    /// without telling a viewer anything the first few had not. Oldest are
    /// dropped first, because the newest marks are the ones describing what
    /// is happening now.
    /// </summary>
    internal const int MaximumLiveEffects = 256;

    /// <summary>
    /// Converts an aim angle to a unit direction vector, using the same
    /// convention <see cref="OperatorGeometry"/> already uses: raw
    /// <see cref="Bam16"/> units over <see cref="Bam16.UnitsPerTurn"/> of a
    /// full turn, with +X at zero and Y increasing downward.
    /// </summary>
    internal static Vector2 AimDirection(Bam16 aim)
    {
        var radians = aim.Raw / (float)Bam16.UnitsPerTurn * MathF.Tau;
        return new Vector2(MathF.Cos(radians), MathF.Sin(radians));
    }

    private static Vector2 PositionWu(OperatorState state) => new(
        (float)state.PositionX.ToDouble(),
        (float)state.PositionY.ToDouble());

    /// <summary>
    /// The marks earned by one executed tick: a muzzle flash and a tracer for
    /// every <see cref="MissionEventKind.ShotFired"/> stamped with
    /// <paramref name="executedTick"/>, and an impact for every operator whose
    /// health is lower in <paramref name="operatorsAfter"/> than the value
    /// <paramref name="healthBefore"/> recorded for the same entity id.
    /// </summary>
    /// <param name="eventFeed">
    /// The feed as it stands after the tick ran. Filtered by
    /// <see cref="MissionEvent.Tick"/>, which is only meaningful because
    /// <c>SandataSimulation.RunTick</c> writes <c>MissionState.Tick</c> — until
    /// 2026-08-11 every event carried tick 0 and this filter would have
    /// matched every shot ever fired, on every tick, forever.
    /// </param>
    /// <param name="operatorsAfter">Operator states after the tick ran.</param>
    /// <param name="healthBefore">
    /// Health by entity id, captured before the tick ran. An entity absent
    /// from this map is treated as having taken no damage, which is the right
    /// answer for an operator that did not exist before the tick.
    /// </param>
    /// <param name="executedTick">The tick that was just executed.</param>
    internal static ImmutableArray<CombatEffect> ObserveTick(
        MissionEventFeed eventFeed,
        ImmutableArray<OperatorState> operatorsAfter,
        ImmutableDictionary<ulong, int> healthBefore,
        long executedTick)
    {
        if (operatorsAfter.IsDefaultOrEmpty)
        {
            return ImmutableArray<CombatEffect>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<CombatEffect>();

        foreach (var missionEvent in eventFeed.Events)
        {
            if (missionEvent.Kind != MissionEventKind.ShotFired ||
                missionEvent.Tick != executedTick)
            {
                continue;
            }

            foreach (var shooter in operatorsAfter)
            {
                if ((long)shooter.EntityId != missionEvent.SubjectId)
                {
                    continue;
                }

                var origin = PositionWu(shooter);
                var direction = AimDirection(shooter.AimAngle);

                builder.Add(new CombatEffect(
                    CombatEffectKind.MuzzleFlash, shooter.EntityId, origin, origin, MuzzleFlashFrames));
                builder.Add(new CombatEffect(
                    CombatEffectKind.Tracer,
                    shooter.EntityId,
                    origin,
                    origin + (direction * TracerLengthWu),
                    TracerFrames));
                break;
            }
        }

        foreach (var op in operatorsAfter)
        {
            if (!healthBefore.TryGetValue(op.EntityId, out var before) || op.Health >= before)
            {
                continue;
            }

            var position = PositionWu(op);
            builder.Add(new CombatEffect(
                CombatEffectKind.Impact, op.EntityId, position, position, ImpactFrames));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Captures the health of every operator by entity id, for the next call
    /// to <see cref="ObserveTick"/> to diff against.
    /// </summary>
    internal static ImmutableDictionary<ulong, int> CaptureHealth(
        ImmutableArray<OperatorState> operators)
    {
        if (operators.IsDefaultOrEmpty)
        {
            return ImmutableDictionary<ulong, int>.Empty;
        }

        var builder = ImmutableDictionary.CreateBuilder<ulong, int>();
        foreach (var op in operators)
        {
            builder[op.EntityId] = op.Health;
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Advances every live effect by one frame and drops the expired ones.
    /// An effect with one frame left is drawn this frame and gone next.
    /// </summary>
    internal static ImmutableArray<CombatEffect> Age(ImmutableArray<CombatEffect> effects)
    {
        if (effects.IsDefaultOrEmpty)
        {
            return ImmutableArray<CombatEffect>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<CombatEffect>(effects.Length);
        foreach (var effect in effects)
        {
            if (effect.RemainingFrames > 1)
            {
                builder.Add(effect with { RemainingFrames = effect.RemainingFrames - 1 });
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Appends <paramref name="added"/> to <paramref name="existing"/>, capped
    /// at <see cref="MaximumLiveEffects"/> by dropping oldest first.
    /// </summary>
    internal static ImmutableArray<CombatEffect> Append(
        ImmutableArray<CombatEffect> existing,
        ImmutableArray<CombatEffect> added)
    {
        if (added.IsDefaultOrEmpty)
        {
            return existing.IsDefault ? ImmutableArray<CombatEffect>.Empty : existing;
        }

        var current = existing.IsDefault ? ImmutableArray<CombatEffect>.Empty : existing;
        var total = current.Length + added.Length;
        var skipFromExisting = Math.Max(0, total - MaximumLiveEffects);

        var builder = ImmutableArray.CreateBuilder<CombatEffect>(Math.Min(total, MaximumLiveEffects));
        for (var i = skipFromExisting; i < current.Length; i++)
        {
            builder.Add(current[i]);
        }

        // added is never itself longer than MaximumLiveEffects in any real
        // frame, but if it were, the same oldest-first rule applies to it.
        var skipFromAdded = Math.Max(0, added.Length - MaximumLiveEffects);
        for (var i = skipFromAdded; i < added.Length; i++)
        {
            builder.Add(added[i]);
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Whether <paramref name="entityId"/> has a live muzzle flash — the
    /// replacement for the always-false
    /// <c>WeaponChainPhase == Firing</c> test that used to gate
    /// <see cref="OperatorGeometry"/>'s flash layer.
    /// </summary>
    internal static bool IsFiring(ImmutableArray<CombatEffect> effects, ulong entityId)
    {
        if (effects.IsDefaultOrEmpty)
        {
            return false;
        }

        foreach (var effect in effects)
        {
            if (effect.Kind == CombatEffectKind.MuzzleFlash && effect.EntityId == entityId)
            {
                return true;
            }
        }

        return false;
    }
}
