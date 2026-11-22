using System.Runtime.CompilerServices;

namespace Hukbo.Core.Simulation;

/// <summary>
/// Entity ID to agent-array-index lookup. Production IDs are dense (the
/// entity ID is the index plus one), so a flat array answers a lookup with one
/// bounds check and one load. IDs that are too sparse for an array of
/// reasonable size fall back to a dictionary, so a test that builds agents
/// with arbitrary IDs behaves exactly as a plain dictionary would: an unknown
/// ID (including 0) is "not found" and the indexer-style lookup throws
/// <see cref="KeyNotFoundException"/>.
/// </summary>
internal readonly struct AgentIndexMap
{
    // An array is used only while its length stays within this multiple of the
    // agent count, plus a constant slack for tiny scenarios.
    private const int DenseSlotsPerAgent = 4;
    private const int DenseSlotSlack = 16;
    private const int UnusedSlot = -1;

    private readonly int[]? _dense;
    private readonly Dictionary<ulong, int>? _sparse;

    private AgentIndexMap(int[]? dense, Dictionary<ulong, int>? sparse)
    {
        _dense = dense;
        _sparse = sparse;
    }

    /// <summary>True when the flat array is in use rather than the dictionary.</summary>
    internal bool IsDense => _dense is not null;

    /// <summary>
    /// Builds the map for <paramref name="entityIds"/>, where the ID at
    /// position <c>i</c> belongs to agent index <c>i</c>.
    /// </summary>
    /// <exception cref="ArgumentException">An ID appears twice.</exception>
    internal static AgentIndexMap Create(ReadOnlySpan<ulong> entityIds)
    {
        ulong maxId = 0;
        foreach (var id in entityIds)
        {
            maxId = Math.Max(maxId, id);
        }

        var denseLimit =
            ((ulong)entityIds.Length * DenseSlotsPerAgent) + DenseSlotSlack;
        if (maxId > denseLimit)
        {
            return CreateSparse(entityIds);
        }

        var dense = new int[(int)maxId + 1];
        Array.Fill(dense, UnusedSlot);
        for (var index = 0; index < entityIds.Length; index++)
        {
            ref var slot = ref dense[(int)entityIds[index]];
            if (slot != UnusedSlot)
            {
                throw DuplicateId(entityIds[index]);
            }

            slot = index;
        }

        return new AgentIndexMap(dense, null);
    }

    private static AgentIndexMap CreateSparse(ReadOnlySpan<ulong> entityIds)
    {
        var sparse = new Dictionary<ulong, int>(entityIds.Length);
        for (var index = 0; index < entityIds.Length; index++)
        {
            if (!sparse.TryAdd(entityIds[index], index))
            {
                throw DuplicateId(entityIds[index]);
            }
        }

        return new AgentIndexMap(null, sparse);
    }

    private static ArgumentException DuplicateId(ulong entityId) =>
        new($"Duplicate entity ID {entityId}.", "agents");

    /// <summary>Same contract as <see cref="Dictionary{TKey, TValue}.TryGetValue"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetIndex(ulong entityId, out int index)
    {
        var dense = _dense;
        if (dense is not null)
        {
            if (entityId < (ulong)dense.Length)
            {
                index = dense[(int)entityId];
                if (index != UnusedSlot)
                {
                    return true;
                }
            }

            index = 0;
            return false;
        }

        return _sparse!.TryGetValue(entityId, out index);
    }

    /// <summary>Same contract as the <see cref="Dictionary{TKey, TValue}"/> indexer.</summary>
    /// <exception cref="KeyNotFoundException">The ID is unknown.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int IndexOf(ulong entityId)
    {
        if (TryGetIndex(entityId, out var index))
        {
            return index;
        }

        throw new KeyNotFoundException(
            $"Entity ID {entityId} is not in the simulation.");
    }
}
