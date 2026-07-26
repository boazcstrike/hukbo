using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace AutonomousArena.Client;

internal sealed class InputEdges
{
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;

    public KeyboardState Keyboard { get; private set; }

    public MouseState Mouse { get; private set; }

    public Point MousePosition => Mouse.Position;

    public bool IsLeftMouseDown => Mouse.LeftButton == ButtonState.Pressed;

    public int ScrollWheelDelta => Mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;

    public void Update()
    {
        _previousKeyboard = Keyboard;
        _previousMouse = Mouse;
        Keyboard = Microsoft.Xna.Framework.Input.Keyboard.GetState();
        Mouse = Microsoft.Xna.Framework.Input.Mouse.GetState();
    }

    public bool IsDown(Keys key) => Keyboard.IsKeyDown(key);

    public bool WasPressed(Keys key) =>
        Keyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);

    public bool WasLeftMousePressed() =>
        Mouse.LeftButton == ButtonState.Pressed &&
        _previousMouse.LeftButton == ButtonState.Released;
}
