using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Presentation;

/// <summary>
/// Fixed-capacity, oldest-replaced population of projectiles left standing in
/// the warriors and shields they struck
/// (docs/plans/2026-08-11-projectile-props.md, task B1). Client-only: never
/// persisted, never snapshotted, never read by the simulation, and cleared
/// only when the scenario resets.
/// </summary>
/// <remarks>
/// <para>
/// <strong>This is a bounded population, not a cache.</strong> <c>CLAUDE.md</c>
/// section 9 forbids adding an unbounded cache, and "arrows currently stuck in
/// people" is the obvious way to violate it — a 10,000-tick battle lands tens
/// of thousands of shots. Nothing is recomputed from this store and nothing
/// else derives from it; it is exactly the shape
/// <see cref="TrampleMarkSystem"/> already is, and its eviction rule is the
/// same plain insertion-order ring for the same reason: an embedded projectile
/// carries no age to compare, because it never fades (design section 8,
/// decision 2).
/// </para>
/// <para>
/// Eviction is visible and that is accepted. An arrow that has stood in a
/// warrior's shield for two hundred ticks vanishing while the camera is
/// elsewhere costs nothing; what matters is that the most recent hits are the
/// ones still shown.
/// </para>
/// <para>
/// <strong>Nothing here knows whether a host is alive.</strong> The draw is a
/// step inside the pawn loop, so a host that loop skips draws no embedded
/// projectiles and its slots are reclaimed by ordinary oldest-first eviction.
/// A dead warrior is drawn for
/// <see cref="DefenderReaction.LethalHoldSeconds"/> — a tenth of a second —
/// and then leaves the field entirely, so a clear-on-death path would buy a
/// tenth of a second of difference and would be dead code. See the plan's
/// section 3.
/// </para>
/// </remarks>
internal sealed class EmbeddedProjectileSystem
{
    /// <summary>
    /// Hard cap on embedded projectiles, fixed rather than scaled with the
    /// agent count (design section 8, decision 3). A flat cap is the easiest
    /// thing to prove bounded, and it is what the quad budget in
    /// <see cref="RenderBudgetEstimate"/> is stated against.
    /// </summary>
    public const int Capacity = 256;

    private readonly EmbeddedProjectile[] _projectiles;
    private int _count;
    private int _oldestIndex;

    public EmbeddedProjectileSystem(int capacity = Capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _projectiles = new EmbeddedProjectile[capacity];
    }

    public ReadOnlySpan<EmbeddedProjectile> ActiveProjectiles =>
        _projectiles.AsSpan(0, _count);

    /// <summary>
    /// Embeds one projectile for a contact that left one, and does nothing for
    /// a contact that did not.
    /// </summary>
    /// <remarks>
    /// Driven from the attack-contact dispatcher rather than from the raw
    /// event stream, which is the boundary <c>HitEffects</c>, <c>Blood</c>,
    /// <c>ClashEffects</c>, and <c>DefenderReactions</c> were all migrated onto
    /// by attack-animation-v2. Feeding this from <c>IngestTick</c> instead
    /// would embed every arrow one animation early, at the tick the event was
    /// emitted rather than at the frame the blow lands.
    /// </remarks>
    public void StartContact(AttackContactBundle contact)
    {
        if (!Embeds(contact.Weapon, contact.Resolution))
        {
            return;
        }

        var onShield = contact.Resolution == AttackResolution.ShieldBlocked;

        Add(new EmbeddedProjectile(
            contact.Sequence,
            contact.DefenderEntityId,
            contact.AttackerEntityId,
            contact.Weapon,
            // A shield-blocked attack still carries a hit location, and using
            // it would put the arrow in the warrior rather than in the board
            // that stopped it. Dropped deliberately in that case, never
            // inferred back from OnShield.
            onShield ? null : contact.HitLocation,
            onShield));
    }

    public void Clear()
    {
        Array.Clear(_projectiles, 0, _count);
        _count = 0;
        _oldestIndex = 0;
    }

    /// <summary>
    /// Whether a contact leaves a projectile standing in what it hit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A shaft embeds; a lead ball does not. That asymmetry is why the design
    /// splits the in-flight prop from the embedded pool rather than treating
    /// "projectile visuals" as one feature: the arquebus contributes to the
    /// first and nothing at all to the second.
    /// </para>
    /// <para>
    /// Exhaustive over <see cref="WeaponId"/> rather than falling through, so
    /// a weapon added later cannot silently start or stop embedding. The four
    /// melee weapons return false because they are melee, not because they
    /// were forgotten.
    /// </para>
    /// </remarks>
    internal static bool Embeds(WeaponId weapon, AttackResolution resolution)
    {
        // Only a blow that arrived leaves anything behind. A parry or an evade
        // put the shot somewhere other than in the defender, and neither this
        // feature nor any other draws where that somewhere was.
        if (resolution is not (AttackResolution.Landed or AttackResolution.ShieldBlocked))
        {
            return false;
        }

        return weapon switch
        {
            WeaponId.Bangkaw or WeaponId.Busog => true,
            WeaponId.Arquebus => false,
            WeaponId.Kampilan or
                WeaponId.Wasay or
                WeaponId.Kalis or
                WeaponId.Itak => false,
            _ => throw new ArgumentOutOfRangeException(
                nameof(weapon),
                weapon,
                null),
        };
    }

    /// <summary>
    /// Appends while the pool has room; once full, always overwrites the
    /// single oldest slot and advances the ring cursor.
    /// </summary>
    private void Add(EmbeddedProjectile projectile)
    {
        if (_count < _projectiles.Length)
        {
            _projectiles[_count] = projectile;
            _count++;
            return;
        }

        _projectiles[_oldestIndex] = projectile;
        _oldestIndex = (_oldestIndex + 1) % _projectiles.Length;
    }
}
