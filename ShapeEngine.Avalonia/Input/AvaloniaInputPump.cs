using System.Numerics;
using Avalonia;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Raylib_cs;
using ShapeEngine.Core.GameDef;
using ShapeEngine.Input;
using AvKey = Avalonia.Input.Key;
using RlMouseButton = Raylib_cs.MouseButton;

namespace ShapeEngine.Avalonia.Input;

/// <summary>Feeds raylib's input state into an Avalonia top level once per frame.</summary>
/// <remarks>
/// Input is read straight from raylib rather than from ShapeEngine's <c>InputSystem</c>, which matters
/// for typed characters: <c>Raylib.GetCharPressed</c> drains a queue that ShapeEngine's
/// <c>KeyboardDevice</c> also drains, but only while unlocked. <see cref="AvaloniaSurface"/> locks the
/// devices whenever the UI has capture, leaving the queue intact and suppressing game input at once.
/// <para>
/// A gamepad is not a keyboard, but Avalonia already knows how to navigate and activate controls with
/// one - it just does not read one, so the D-pad and a face button are translated into the same Tab,
/// Shift+Tab and Space it would otherwise get from a keyboard. Which gamepad is "the" gamepad is read
/// from <see cref="GamepadDeviceManager.LastUsedGamepad"/>, ShapeEngine's own notion of whichever
/// controller the player is currently using.
/// </para>
/// </remarks>
internal sealed class AvaloniaInputPump
{
    private readonly ShapeEngineTopLevelImpl impl;

    private Point lastPointerPosition = new(Double.NaN, Double.NaN);
    private bool pointerWasInside;

    public AvaloniaInputPump(ShapeEngineTopLevelImpl impl) => this.impl = impl;

    /// <summary>Translates this frame's raylib input into Avalonia raw input events.</summary>
    /// <param name="pointerPosition">
    /// The cursor in Avalonia's client coordinate space. Mapped by the caller, because only the
    /// placement texture knows an anchored surface's coordinate space.
    /// </param>
    /// <param name="pointerEnabled">Whether pointer events should reach Avalonia at all.</param>
    /// <param name="keyNavigationEnabled">
    /// Whether key-down/up events (Tab, arrows, Space/Enter, ...) should reach Avalonia - needed by any
    /// focused control, not just one accepting text.
    /// </param>
    /// <param name="textInputEnabled">
    /// Whether typed characters should reach Avalonia. Narrower than
    /// <paramref name="keyNavigationEnabled"/>: draining raylib's character queue is the one thing here
    /// that competes with ShapeEngine's own <c>KeyboardDevice</c> (see the type remarks), so it only
    /// happens while a control is actually consuming text.
    /// </param>
    /// <param name="gamepadNavigation">How the D-pad is translated - see <see cref="GamepadNavigationMode"/>.</param>
    public void Pump(
        Point pointerPosition,
        bool pointerEnabled,
        bool keyNavigationEnabled,
        bool textInputEnabled,
        GamepadNavigationMode gamepadNavigation = GamepadNavigationMode.Linear)
    {
        var timestamp = (ulong)Environment.TickCount64;
        var modifiers = KeyMap.GetModifiers();

        if (pointerEnabled) PumpPointer(pointerPosition, timestamp, modifiers);
        else if (pointerWasInside)
        {
            pointerWasInside = false;

            // Forgotten too, so re-entering sends a fresh move even from the exact position it left.
            lastPointerPosition = new Point(Double.NaN, Double.NaN);
            impl.OnPointerLeft(timestamp);
        }

        if (keyNavigationEnabled)
        {
            foreach (var entry in KeyMap.Keys) PumpKey(entry, timestamp, modifiers);
            PumpGamepadNavigation(timestamp, modifiers, gamepadNavigation, includeActivation: true);
        }

        // Tab still reaches Avalonia while the broader keyboard gate is shut, as long as the pointer is
        // over the surface - otherwise nothing is ever focused yet to open that gate, and Tab could
        // never be the thing that focuses the first control. The D-pad is this surface's equivalent:
        // there is nothing to activate yet, so only navigation is forwarded here.
        else if (pointerEnabled)
        {
            PumpKey(KeyMap.Tab, timestamp, modifiers);
            PumpGamepadNavigation(timestamp, modifiers, gamepadNavigation, includeActivation: false);
        }

        if (textInputEnabled) PumpTextInput(timestamp);
    }

    private void PumpPointer(Point point, ulong timestamp, RawInputModifiers modifiers)
    {
        // Compared in client space, so a surface moving under a stationary cursor still reports a move.
        if (point != lastPointerPosition)
        {
            lastPointerPosition = point;
            pointerWasInside = true;
            impl.OnPointerMoved(point, modifiers, timestamp);
        }

        PumpButton(RlMouseButton.Left, RawPointerEventType.LeftButtonDown, RawPointerEventType.LeftButtonUp, point, modifiers, timestamp);
        PumpButton(RlMouseButton.Right, RawPointerEventType.RightButtonDown, RawPointerEventType.RightButtonUp, point, modifiers, timestamp);
        PumpButton(RlMouseButton.Middle, RawPointerEventType.MiddleButtonDown, RawPointerEventType.MiddleButtonUp, point, modifiers, timestamp);
        PumpButton(RlMouseButton.Side, RawPointerEventType.XButton1Down, RawPointerEventType.XButton1Up, point, modifiers, timestamp);
        PumpButton(RlMouseButton.Extra, RawPointerEventType.XButton2Down, RawPointerEventType.XButton2Up, point, modifiers, timestamp);

        var wheel = Raylib.GetMouseWheelMoveV();
        if (wheel != Vector2.Zero)
        {
            impl.OnPointerWheel(point, new global::Avalonia.Vector(wheel.X, wheel.Y), modifiers, timestamp);
        }
    }

    private void PumpButton(
        RlMouseButton button,
        RawPointerEventType downType,
        RawPointerEventType upType,
        Point point,
        RawInputModifiers modifiers,
        ulong timestamp)
    {
        if (Raylib.IsMouseButtonPressed(button)) impl.OnPointerButton(downType, point, modifiers, timestamp);
        if (Raylib.IsMouseButtonReleased(button)) impl.OnPointerButton(upType, point, modifiers, timestamp);
    }

    private void PumpTextInput(ulong timestamp)
    {
        var unicode = Raylib.GetCharPressed();
        while (unicode > 0)
        {
            impl.OnTextInput(Char.ConvertFromUtf32(unicode), timestamp);
            unicode = Raylib.GetCharPressed();
        }
    }

    private void PumpKey((KeyboardKey Raylib, AvKey Key, PhysicalKey Physical) entry, ulong timestamp, RawInputModifiers modifiers)
    {
        var (raylibKey, key, physicalKey) = entry;

        // IsKeyPressedRepeat covers the auto-repeat text editing and list navigation rely on, and
        // never fires for the initial press - the two are complementary.
        if (Raylib.IsKeyPressed(raylibKey) || Raylib.IsKeyPressedRepeat(raylibKey))
        {
            impl.OnKey(RawKeyEventType.KeyDown, key, physicalKey, modifiers, null, timestamp);
        }

        if (Raylib.IsKeyReleased(raylibKey))
        {
            impl.OnKey(RawKeyEventType.KeyUp, key, physicalKey, modifiers, null, timestamp);
        }
    }

    /// <summary>Translates the D-pad, and optionally a face button, into the keys Avalonia already handles.</summary>
    /// <param name="includeActivation">
    /// Whether the face button that "clicks" the focused control should be forwarded too - left out
    /// while nothing is focused yet, since there would be nothing for it to activate.
    /// </param>
    /// <remarks>
    /// No auto-repeat on holding the D-pad, unlike keyboard navigation - raylib has no gamepad
    /// equivalent of <c>IsKeyPressedRepeat</c> to drive one from, so for now each direction moves focus
    /// once per press.
    /// </remarks>
    private void PumpGamepadNavigation(
        ulong timestamp,
        RawInputModifiers modifiers,
        GamepadNavigationMode navigation,
        bool includeActivation)
    {
        var gamepadIndex = Game.Instance.Input.GamepadManager.LastUsedGamepad?.Index;
        if (gamepadIndex is not { } index || !Raylib.IsGamepadAvailable(index)) return;

        if (navigation == GamepadNavigationMode.Directional)
        {
            PumpGamepadKey(index, GamepadButton.LeftFaceRight, AvKey.Right, PhysicalKey.ArrowRight, modifiers, timestamp);
            PumpGamepadKey(index, GamepadButton.LeftFaceLeft, AvKey.Left, PhysicalKey.ArrowLeft, modifiers, timestamp);
            PumpGamepadKey(index, GamepadButton.LeftFaceDown, AvKey.Down, PhysicalKey.ArrowDown, modifiers, timestamp);
            PumpGamepadKey(index, GamepadButton.LeftFaceUp, AvKey.Up, PhysicalKey.ArrowUp, modifiers, timestamp);
        }
        else
        {
            // Right and down both move forward, left and up both move back - the tab order is one
            // dimensional, so both axes have to collapse onto it.
            PumpGamepadKey(index, GamepadButton.LeftFaceRight, KeyMap.Tab.Key, KeyMap.Tab.Physical, modifiers, timestamp);
            PumpGamepadKey(index, GamepadButton.LeftFaceDown, KeyMap.Tab.Key, KeyMap.Tab.Physical, modifiers, timestamp);
            PumpGamepadKey(index, GamepadButton.LeftFaceLeft, KeyMap.Tab.Key, KeyMap.Tab.Physical, modifiers | RawInputModifiers.Shift, timestamp);
            PumpGamepadKey(index, GamepadButton.LeftFaceUp, KeyMap.Tab.Key, KeyMap.Tab.Physical, modifiers | RawInputModifiers.Shift, timestamp);
        }

        if (includeActivation)
        {
            PumpGamepadKey(index, GamepadButton.RightFaceDown, AvKey.Space, PhysicalKey.Space, modifiers, timestamp);
        }
    }

    private void PumpGamepadKey(int gamepadIndex, GamepadButton button, AvKey key, PhysicalKey physicalKey, RawInputModifiers modifiers, ulong timestamp)
    {
        if (Raylib.IsGamepadButtonPressed(gamepadIndex, button))
        {
            impl.OnKey(RawKeyEventType.KeyDown, key, physicalKey, modifiers, null, timestamp);
        }

        if (Raylib.IsGamepadButtonReleased(gamepadIndex, button))
        {
            impl.OnKey(RawKeyEventType.KeyUp, key, physicalKey, modifiers, null, timestamp);
        }
    }
}
