using Avalonia.Input;
using Raylib_cs;
using RlMouseButton = Raylib_cs.MouseButton;
using AvKey = Avalonia.Input.Key;

namespace ShapeEngine.Avalonia.Input;

/// <summary>Translation between raylib's key codes and Avalonia's.</summary>
internal static class KeyMap
{
    /// <summary>
    /// Tab's own mapping, singled out so <see cref="AvaloniaInputPump"/> can forward it on its own - the
    /// one key that has to reach Avalonia before anything is focused, to establish focus at all - and so
    /// the gamepad D-pad's Tab/Shift+Tab translation shares the same mapping rather than repeating it.
    /// </summary>
    public static readonly (KeyboardKey Raylib, AvKey Key, PhysicalKey Physical) Tab =
        (KeyboardKey.Tab, AvKey.Tab, PhysicalKey.Tab);

    /// <summary>
    /// Every raylib key with an Avalonia equivalent, paired with its physical (layout independent)
    /// counterpart. The input pump polls exactly these each frame.
    /// </summary>
    public static readonly (KeyboardKey Raylib, AvKey Key, PhysicalKey Physical)[] Keys =
    [
        (KeyboardKey.Apostrophe, AvKey.OemQuotes, PhysicalKey.Quote),
        (KeyboardKey.Comma, AvKey.OemComma, PhysicalKey.Comma),
        (KeyboardKey.Minus, AvKey.OemMinus, PhysicalKey.Minus),
        (KeyboardKey.Period, AvKey.OemPeriod, PhysicalKey.Period),
        (KeyboardKey.Slash, AvKey.OemQuestion, PhysicalKey.Slash),
        (KeyboardKey.Zero, AvKey.D0, PhysicalKey.Digit0),
        (KeyboardKey.One, AvKey.D1, PhysicalKey.Digit1),
        (KeyboardKey.Two, AvKey.D2, PhysicalKey.Digit2),
        (KeyboardKey.Three, AvKey.D3, PhysicalKey.Digit3),
        (KeyboardKey.Four, AvKey.D4, PhysicalKey.Digit4),
        (KeyboardKey.Five, AvKey.D5, PhysicalKey.Digit5),
        (KeyboardKey.Six, AvKey.D6, PhysicalKey.Digit6),
        (KeyboardKey.Seven, AvKey.D7, PhysicalKey.Digit7),
        (KeyboardKey.Eight, AvKey.D8, PhysicalKey.Digit8),
        (KeyboardKey.Nine, AvKey.D9, PhysicalKey.Digit9),
        (KeyboardKey.Semicolon, AvKey.OemSemicolon, PhysicalKey.Semicolon),
        (KeyboardKey.Equal, AvKey.OemPlus, PhysicalKey.Equal),
        (KeyboardKey.A, AvKey.A, PhysicalKey.A),
        (KeyboardKey.B, AvKey.B, PhysicalKey.B),
        (KeyboardKey.C, AvKey.C, PhysicalKey.C),
        (KeyboardKey.D, AvKey.D, PhysicalKey.D),
        (KeyboardKey.E, AvKey.E, PhysicalKey.E),
        (KeyboardKey.F, AvKey.F, PhysicalKey.F),
        (KeyboardKey.G, AvKey.G, PhysicalKey.G),
        (KeyboardKey.H, AvKey.H, PhysicalKey.H),
        (KeyboardKey.I, AvKey.I, PhysicalKey.I),
        (KeyboardKey.J, AvKey.J, PhysicalKey.J),
        (KeyboardKey.K, AvKey.K, PhysicalKey.K),
        (KeyboardKey.L, AvKey.L, PhysicalKey.L),
        (KeyboardKey.M, AvKey.M, PhysicalKey.M),
        (KeyboardKey.N, AvKey.N, PhysicalKey.N),
        (KeyboardKey.O, AvKey.O, PhysicalKey.O),
        (KeyboardKey.P, AvKey.P, PhysicalKey.P),
        (KeyboardKey.Q, AvKey.Q, PhysicalKey.Q),
        (KeyboardKey.R, AvKey.R, PhysicalKey.R),
        (KeyboardKey.S, AvKey.S, PhysicalKey.S),
        (KeyboardKey.T, AvKey.T, PhysicalKey.T),
        (KeyboardKey.U, AvKey.U, PhysicalKey.U),
        (KeyboardKey.V, AvKey.V, PhysicalKey.V),
        (KeyboardKey.W, AvKey.W, PhysicalKey.W),
        (KeyboardKey.X, AvKey.X, PhysicalKey.X),
        (KeyboardKey.Y, AvKey.Y, PhysicalKey.Y),
        (KeyboardKey.Z, AvKey.Z, PhysicalKey.Z),
        (KeyboardKey.LeftBracket, AvKey.OemOpenBrackets, PhysicalKey.BracketLeft),
        (KeyboardKey.Backslash, AvKey.OemBackslash, PhysicalKey.Backslash),
        (KeyboardKey.RightBracket, AvKey.OemCloseBrackets, PhysicalKey.BracketRight),
        (KeyboardKey.Grave, AvKey.OemTilde, PhysicalKey.Backquote),
        (KeyboardKey.Space, AvKey.Space, PhysicalKey.Space),
        (KeyboardKey.Escape, AvKey.Escape, PhysicalKey.Escape),
        (KeyboardKey.Enter, AvKey.Enter, PhysicalKey.Enter),
        Tab,
        (KeyboardKey.Backspace, AvKey.Back, PhysicalKey.Backspace),
        (KeyboardKey.Insert, AvKey.Insert, PhysicalKey.Insert),
        (KeyboardKey.Delete, AvKey.Delete, PhysicalKey.Delete),
        (KeyboardKey.Right, AvKey.Right, PhysicalKey.ArrowRight),
        (KeyboardKey.Left, AvKey.Left, PhysicalKey.ArrowLeft),
        (KeyboardKey.Down, AvKey.Down, PhysicalKey.ArrowDown),
        (KeyboardKey.Up, AvKey.Up, PhysicalKey.ArrowUp),
        (KeyboardKey.PageUp, AvKey.PageUp, PhysicalKey.PageUp),
        (KeyboardKey.PageDown, AvKey.PageDown, PhysicalKey.PageDown),
        (KeyboardKey.Home, AvKey.Home, PhysicalKey.Home),
        (KeyboardKey.End, AvKey.End, PhysicalKey.End),
        (KeyboardKey.CapsLock, AvKey.CapsLock, PhysicalKey.CapsLock),
        (KeyboardKey.ScrollLock, AvKey.Scroll, PhysicalKey.ScrollLock),
        (KeyboardKey.NumLock, AvKey.NumLock, PhysicalKey.NumLock),
        (KeyboardKey.PrintScreen, AvKey.PrintScreen, PhysicalKey.PrintScreen),
        (KeyboardKey.Pause, AvKey.Pause, PhysicalKey.Pause),
        (KeyboardKey.F1, AvKey.F1, PhysicalKey.F1),
        (KeyboardKey.F2, AvKey.F2, PhysicalKey.F2),
        (KeyboardKey.F3, AvKey.F3, PhysicalKey.F3),
        (KeyboardKey.F4, AvKey.F4, PhysicalKey.F4),
        (KeyboardKey.F5, AvKey.F5, PhysicalKey.F5),
        (KeyboardKey.F6, AvKey.F6, PhysicalKey.F6),
        (KeyboardKey.F7, AvKey.F7, PhysicalKey.F7),
        (KeyboardKey.F8, AvKey.F8, PhysicalKey.F8),
        (KeyboardKey.F9, AvKey.F9, PhysicalKey.F9),
        (KeyboardKey.F10, AvKey.F10, PhysicalKey.F10),
        (KeyboardKey.F11, AvKey.F11, PhysicalKey.F11),
        (KeyboardKey.F12, AvKey.F12, PhysicalKey.F12),
        (KeyboardKey.LeftShift, AvKey.LeftShift, PhysicalKey.ShiftLeft),
        (KeyboardKey.LeftControl, AvKey.LeftCtrl, PhysicalKey.ControlLeft),
        (KeyboardKey.LeftAlt, AvKey.LeftAlt, PhysicalKey.AltLeft),
        (KeyboardKey.LeftSuper, AvKey.LWin, PhysicalKey.MetaLeft),
        (KeyboardKey.RightShift, AvKey.RightShift, PhysicalKey.ShiftRight),
        (KeyboardKey.RightControl, AvKey.RightCtrl, PhysicalKey.ControlRight),
        (KeyboardKey.RightAlt, AvKey.RightAlt, PhysicalKey.AltRight),
        (KeyboardKey.RightSuper, AvKey.RWin, PhysicalKey.MetaRight),
        (KeyboardKey.KeyboardMenu, AvKey.Apps, PhysicalKey.ContextMenu),
        (KeyboardKey.Kp0, AvKey.NumPad0, PhysicalKey.NumPad0),
        (KeyboardKey.Kp1, AvKey.NumPad1, PhysicalKey.NumPad1),
        (KeyboardKey.Kp2, AvKey.NumPad2, PhysicalKey.NumPad2),
        (KeyboardKey.Kp3, AvKey.NumPad3, PhysicalKey.NumPad3),
        (KeyboardKey.Kp4, AvKey.NumPad4, PhysicalKey.NumPad4),
        (KeyboardKey.Kp5, AvKey.NumPad5, PhysicalKey.NumPad5),
        (KeyboardKey.Kp6, AvKey.NumPad6, PhysicalKey.NumPad6),
        (KeyboardKey.Kp7, AvKey.NumPad7, PhysicalKey.NumPad7),
        (KeyboardKey.Kp8, AvKey.NumPad8, PhysicalKey.NumPad8),
        (KeyboardKey.Kp9, AvKey.NumPad9, PhysicalKey.NumPad9),
        (KeyboardKey.KpDecimal, AvKey.Decimal, PhysicalKey.NumPadDecimal),
        (KeyboardKey.KpDivide, AvKey.Divide, PhysicalKey.NumPadDivide),
        (KeyboardKey.KpMultiply, AvKey.Multiply, PhysicalKey.NumPadMultiply),
        (KeyboardKey.KpSubtract, AvKey.Subtract, PhysicalKey.NumPadSubtract),
        (KeyboardKey.KpAdd, AvKey.Add, PhysicalKey.NumPadAdd),
        (KeyboardKey.KpEnter, AvKey.Enter, PhysicalKey.NumPadEnter),
        (KeyboardKey.KpEqual, AvKey.OemPlus, PhysicalKey.NumPadEqual)
    ];

    /// <summary>Reads the currently held modifier keys and mouse buttons.</summary>
    public static RawInputModifiers GetModifiers()
    {
        var modifiers = RawInputModifiers.None;

        if (Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift))
            modifiers |= RawInputModifiers.Shift;
        if (Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl))
            modifiers |= RawInputModifiers.Control;
        if (Raylib.IsKeyDown(KeyboardKey.LeftAlt) || Raylib.IsKeyDown(KeyboardKey.RightAlt))
            modifiers |= RawInputModifiers.Alt;
        if (Raylib.IsKeyDown(KeyboardKey.LeftSuper) || Raylib.IsKeyDown(KeyboardKey.RightSuper))
            modifiers |= RawInputModifiers.Meta;

        if (Raylib.IsMouseButtonDown(RlMouseButton.Left)) modifiers |= RawInputModifiers.LeftMouseButton;
        if (Raylib.IsMouseButtonDown(RlMouseButton.Right)) modifiers |= RawInputModifiers.RightMouseButton;
        if (Raylib.IsMouseButtonDown(RlMouseButton.Middle)) modifiers |= RawInputModifiers.MiddleMouseButton;

        return modifiers;
    }
}
