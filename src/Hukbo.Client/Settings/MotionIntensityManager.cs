namespace Hukbo.Client.Settings;

/// <summary>
/// Owns the spectator's chosen ambient-motion level and persists a change the
/// moment it is made. Mirrors <see cref="GoreIntensityManager"/>'s shape: the
/// caller supplies the initial value and a persist delegate, so the type is
/// fully testable without touching the filesystem.
/// </summary>
internal sealed class MotionIntensityManager
{
    private const MotionIntensity FallbackValue = MotionIntensity.Full;

    private readonly Func<MotionIntensity, bool> _persist;

    internal MotionIntensityManager(
        MotionIntensity initialValue,
        Func<MotionIntensity, bool> persist)
    {
        ArgumentNullException.ThrowIfNull(persist);
        _persist = persist;
        Value = Enum.IsDefined(initialValue) ? initialValue : FallbackValue;
    }

    public MotionIntensity Value { get; private set; }

    /// <summary>
    /// Applies <paramref name="value"/> and persists it. Returns false, without
    /// persisting, when the value is already active or is not a defined level.
    /// A failed save does not roll the active value back: the spectator's
    /// choice stays visible for this session even if the settings file is
    /// unwritable.
    /// </summary>
    public bool TrySelect(MotionIntensity value)
    {
        if (!Enum.IsDefined(value) || value == Value)
        {
            return false;
        }

        Value = value;
        _persist(value);
        return true;
    }
}
