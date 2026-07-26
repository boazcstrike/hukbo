namespace Hukbo.Core.Simulation;

/// <summary>
/// The positional input that collision pair generation reads for one agent.
/// Coordinates are raw fixed-point units, matching
/// <see cref="AgentState.XRaw"/> and <see cref="AgentState.YRaw"/>.
/// </summary>
/// <remarks>
/// This is a narrow read-only projection rather than the agent itself, so
/// pair generation cannot reach combat state and cannot mutate an agent.
/// The body radius is common to every agent and lives on the immutable
/// scenario, so it is deliberately absent here.
/// </remarks>
internal readonly record struct CollisionBody(
    ulong EntityId,
    int XRaw,
    int YRaw,
    bool IsAlive);

/// <summary>
/// An unordered pair of two distinct entities, normalised so that the lower
/// entity ID is always <see cref="LowEntityId"/>. Normalisation is what makes
/// the pair stable: the same two entities produce one identical value however
/// the generator happened to visit them.
/// </summary>
/// <remarks>
/// The type carries a total order so that a generated pair set can be sorted
/// into a single canonical sequence. Determinism depends on that order being
/// total, which it is: entity IDs are unique, so no two distinct pairs compare
/// equal.
/// </remarks>
internal readonly record struct CollisionPair(ulong LowEntityId, ulong HighEntityId)
    : IComparable<CollisionPair>
{
    /// <summary>
    /// Creates a pair from two entity IDs in either argument order.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The two entity IDs are equal. An entity never collides with itself, so
    /// an equal pair is a generator defect rather than an empty result.
    /// </exception>
    internal static CollisionPair Create(ulong left, ulong right)
    {
        if (left == right)
        {
            throw new ArgumentException(
                "A collision pair requires two distinct entity IDs.",
                nameof(right));
        }

        return left < right
            ? new CollisionPair(left, right)
            : new CollisionPair(right, left);
    }

    /// <summary>
    /// Orders by ascending <see cref="LowEntityId"/>, then by ascending
    /// <see cref="HighEntityId"/>.
    /// </summary>
    public int CompareTo(CollisionPair other)
    {
        var lowComparison = LowEntityId.CompareTo(other.LowEntityId);

        return lowComparison != 0
            ? lowComparison
            : HighEntityId.CompareTo(other.HighEntityId);
    }
}
