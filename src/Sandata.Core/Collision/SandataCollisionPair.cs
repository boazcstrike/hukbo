namespace Sandata.Core.Collision;

/// <summary>
/// An unordered pair of two distinct entities, normalised so that the lower
/// entity ID is always <see cref="LowEntityId"/>. Normalisation is what makes
/// the pair stable: the same two entities produce one identical value however
/// the emitter happened to visit them, which is what lets a permuted
/// insertion order into <see cref="SandataCollisionGrid"/> still emit the
/// same pair list.
/// </summary>
/// <remarks>
/// The type carries a total order so that a generated pair set can be sorted
/// into a single canonical sequence. Determinism depends on that order being
/// total, which it is: entity IDs are unique, so no two distinct pairs compare
/// equal.
/// </remarks>
internal readonly record struct SandataCollisionPair(ulong LowEntityId, ulong HighEntityId)
    : IComparable<SandataCollisionPair>
{
    /// <summary>
    /// Creates a pair from two entity IDs in either argument order.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The two entity IDs are equal. An entity never collides with itself, so
    /// an equal pair is an emitter defect rather than an empty result.
    /// </exception>
    internal static SandataCollisionPair Create(ulong left, ulong right)
    {
        if (left == right)
        {
            throw new ArgumentException(
                "A collision pair requires two distinct entity IDs.",
                nameof(right));
        }

        return left < right
            ? new SandataCollisionPair(left, right)
            : new SandataCollisionPair(right, left);
    }

    /// <summary>
    /// Orders by ascending <see cref="LowEntityId"/>, then by ascending
    /// <see cref="HighEntityId"/>.
    /// </summary>
    public int CompareTo(SandataCollisionPair other)
    {
        var lowComparison = LowEntityId.CompareTo(other.LowEntityId);

        return lowComparison != 0
            ? lowComparison
            : HighEntityId.CompareTo(other.HighEntityId);
    }
}
