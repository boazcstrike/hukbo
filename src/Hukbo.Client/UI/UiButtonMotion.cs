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
    private MotionIntensity _intensity;

    public float HoverAmount => _hover.Value;

    public float FocusAmount => _focus.Value;

    public float PressAmount => _press.Value;

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
        MotionIntensity intensity)
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
    }

    public void Reset()
    {
        _hover = default;
        _focus = default;
        _press = default;
        _intensity = MotionIntensity.Off;
    }
}
