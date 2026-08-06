namespace Hukbo.Client.UI;

/// <summary>
/// A bounded, client-only transition from one normalized value to another.
/// The caller supplies unscaled elapsed time; the transition owns no clock,
/// timer, random source, or simulation state.
/// </summary>
internal struct UiTransition
{
    private float _startValue;
    private float _value;
    private float _targetValue;
    private double _elapsedSeconds;

    public readonly float Value => _value;

    public readonly bool IsSettled => _value == _targetValue;

    public void AdvanceTo(
        float targetValue,
        TimeSpan elapsed,
        TimeSpan duration,
        bool isMotionEnabled)
    {
        var target = float.IsFinite(targetValue)
            ? Math.Clamp(targetValue, 0f, 1f)
            : 0f;
        if (!isMotionEnabled || duration <= TimeSpan.Zero)
        {
            SnapTo(target);
            return;
        }

        if (_targetValue != target)
        {
            _startValue = _value;
            _targetValue = target;
            _elapsedSeconds = 0d;
        }

        if (IsSettled || elapsed <= TimeSpan.Zero)
        {
            return;
        }

        var durationSeconds = duration.TotalSeconds;
        var elapsedSeconds = Math.Min(
            Math.Max(0d, elapsed.TotalSeconds),
            durationSeconds);
        _elapsedSeconds = Math.Min(
            durationSeconds,
            _elapsedSeconds + elapsedSeconds);

        if (_elapsedSeconds >= durationSeconds)
        {
            _value = _targetValue;
            return;
        }

        var progress = (float)(_elapsedSeconds / durationSeconds);
        var easedProgress = EaseOutCubic(progress);
        _value = _startValue +
            ((_targetValue - _startValue) * easedProgress);
    }

    /// <summary>
    /// Immediately sets the current value, the start value, and the target
    /// value to <paramref name="value"/>, discarding any in-progress
    /// transition. Used to jump a channel to a value and then transition
    /// away from it via a subsequent <see cref="AdvanceTo"/> call.
    /// </summary>
    public void Restart(float value)
    {
        var clamped = float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : 0f;
        SnapTo(clamped);
    }

    private static float EaseOutCubic(float progress)
    {
        var remaining = 1f - Math.Clamp(progress, 0f, 1f);
        return 1f - (remaining * remaining * remaining);
    }

    private void SnapTo(float target)
    {
        _startValue = target;
        _value = target;
        _targetValue = target;
        _elapsedSeconds = 0d;
    }
}
