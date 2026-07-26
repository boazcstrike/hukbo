using Microsoft.Xna.Framework;

namespace AutonomousArena.Client;

internal sealed class MenuButton
{
    public MenuButton(string label, MenuAction action)
    {
        Label = label;
        Action = action;
    }

    public string Label { get; }

    public MenuAction Action { get; }

    public Rectangle Bounds { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool IsHovered { get; set; }

    public bool IsFocused { get; set; }

    public bool IsPressed { get; set; }
}

internal enum MenuAction
{
    None,
    Play,
    Pause,
    Exit,
}
