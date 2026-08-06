namespace Hukbo.Client.UI;

/// <summary>
/// A one-shot, non-looping emphasis channel. An observed change calls
/// <see cref="Trigger"/> to jump the amount to 1, then every frame calls
/// <see cref="Advance"/>, which always decays the amount toward 0. There is
/// no code path that raises the amount except an explicit
/// <see cref="Trigger"/>, so the pulse cannot become an idle animation.
/// </summary>
internal struct UiEmphasisPulse
{
    private UiTransition _transition;

    public readonly float Amount => _transition.Value;

    public readonly bool IsSettled => _transition.IsSettled;

    /// <summary>
    /// Jumps the amount to 1 when motion is enabled. Does nothing when
    /// motion is disabled, so <c>MotionIntensity.Off</c> never produces a
    /// pulse that would otherwise snap away on the next frame.
    /// </summary>
    public void Trigger(bool isMotionEnabled)
    {
        if (isMotionEnabled)
        {
            _transition.Restart(1f);
        }
    }

    /// <summary>
    /// Decays the amount toward 0 over <paramref name="duration"/>. The
    /// target is unconditionally 0, so this never raises the amount.
    /// </summary>
    public void Advance(TimeSpan elapsed, TimeSpan duration, bool isMotionEnabled)
    {
        _transition.AdvanceTo(0f, elapsed, duration, isMotionEnabled);
    }

    public void Reset()
    {
        _transition = default;
    }
}
