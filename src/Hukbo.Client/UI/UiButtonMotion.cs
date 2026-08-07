using Hukbo.Client.Settings;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.UI;

/// <summary>
/// Owns the bounded color channels used by a button. Positional feedback
/// is deliberately restricted to a one-pixel pressed inset at full motion.
/// </summary>
internal sealed class UiButtonMotion
{
    internal static readonly TimeSpan HoverDuration =
        TimeSpan.FromMilliseconds(110);
    internal static readonly TimeSpan PressDuration =
        TimeSpan.FromMilliseconds(60);

    private UiTransition _hover;
    private UiTransition _focus;
    private UiTransition _press;
    private UiTransition _active;
    private MotionIntensity _intensity;

    public float HoverAmount => _hover.Value;

    public float FocusAmount => _focus.Value;

    public float PressAmount => _press.Value;

    public float ActiveAmount => _active.Value;

    /// <summary>
    /// True once the active-strip channel has reached its target. Used to
    /// widen the control-bar strip's draw condition so a deactivation fades
    /// out over <see cref="UiSecondaryMotion.ActiveStripDuration"/> instead
    /// of disappearing on the frame <c>IsActive</c> turns false.
    /// </summary>
    public bool IsActiveSettled => _active.IsSettled;

    public int DecorativePressInset =>
        _intensity == MotionIntensity.Full && PressAmount > 0f
            ? 1
            : 0;

    public Rectangle GetVisualBounds(Rectangle hitBounds)
    {
        var inset = DecorativePressInset;
        return inset == 0
            ? hitBounds
            : new Rectangle(
                hitBounds.Left,
                hitBounds.Top + inset,
                hitBounds.Width,
                Math.Max(0, hitBounds.Height - inset));
    }

    public void Advance(
        bool isHovered,
        bool isFocused,
        bool isPressed,
        TimeSpan elapsed,
        MotionIntensity intensity,
        bool isActive = false)
    {
        _intensity = Enum.IsDefined(intensity)
            ? intensity
            : MotionIntensity.Off;
        var isMotionEnabled = _intensity != MotionIntensity.Off;

        _hover.AdvanceTo(
            isHovered ? 1f : 0f,
            elapsed,
            HoverDuration,
            isMotionEnabled);
        _focus.AdvanceTo(
            isFocused ? 1f : 0f,
            elapsed,
            HoverDuration,
            isMotionEnabled);
        _press.AdvanceTo(
            isPressed ? 1f : 0f,
            elapsed,
            PressDuration,
            isMotionEnabled);
        _active.AdvanceTo(
            isActive ? 1f : 0f,
            elapsed,
            UiSecondaryMotion.ActiveStripDuration,
            isMotionEnabled);
    }

    public void Reset()
    {
        _hover = default;
        _focus = default;
        _press = default;
        _active = default;
        _intensity = MotionIntensity.Off;
    }
}
