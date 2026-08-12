using Avalonia.Input;

namespace ShapeEngine.Avalonia.Input;

/// <summary>
/// The Avalonia input devices every raylib-hosted top level reports events against. raylib exposes one
/// system keyboard and one system mouse, so a single instance of each is shared.
/// </summary>
internal static class ShapeEngineDevices
{
    public static readonly KeyboardDevice Keyboard = new();

    public static readonly MouseDevice Mouse = new(new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true));
}
