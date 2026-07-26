namespace AutonomousArena.Core.Determinism;

/// <summary>
/// A project-owned SplitMix64 generator with a stable, versioned algorithm.
/// </summary>
public struct SplitMix64
{
    public const string AlgorithmId = "splitmix64-v1";

    private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
    private ulong _state;

    public SplitMix64(ulong seed)
    {
        _state = seed;
    }

    public readonly ulong State => _state;

    public ulong NextUInt64()
    {
        _state = unchecked(_state + GoldenGamma);
        var value = _state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    public int NextInt(int exclusiveUpperBound)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveUpperBound);

        var bound = (ulong)exclusiveUpperBound;
        var rejectionThreshold = unchecked(0UL - bound) % bound;
        ulong value;

        do
        {
            value = NextUInt64();
        }
        while (value < rejectionThreshold);

        return (int)(value % bound);
    }
}
