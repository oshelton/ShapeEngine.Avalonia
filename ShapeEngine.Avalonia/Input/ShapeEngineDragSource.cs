using System.Numerics;
using Avalonia;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.Raw;
using Raylib_cs;
using ShapeEngine.Core.GameDef;
using ShapeEngine.Core.Structs;
using RlColor = Raylib_cs.Color;
using RlMouseButton = Raylib_cs.MouseButton;

namespace ShapeEngine.Avalonia.Input;

/// <summary>
/// Runs a drag-and-drop gesture through Avalonia's own managed <see cref="DragDropDevice"/>, with no
/// native platform drag loop behind it.
/// </summary>
/// <remarks>
/// Avalonia's built-in platform backends start a real OS drag loop - OLE on Windows, XDND on X11 - and
/// feed <see cref="DragDropDevice"/> from native drag callbacks as the OS reports them. There is no such
/// loop here: the game owns the message pump, not a platform drag source.
/// <para>
/// Worse, there is no single top level to ride events from either: a drag can start on one
/// <see cref="AvaloniaSurface"/> and land on a completely different one, each with its own
/// <c>ShapeEngineTopLevel</c> and its own independent per-frame input pump. So rather than subscribing to
/// one top level's routed pointer events, the drag is tracked here as plain state, and every surface
/// offers it a chance each frame via <see cref="Pump"/> - only the one currently under the pointer
/// actually does anything, feeding <see cref="DragDropDevice"/> the one raw event type it needs
/// (<see cref="RawDragEventType.DragOver"/> while held, <see cref="RawDragEventType.Drop"/> on release);
/// the device tracks which control was last hit itself and synthesizes DragEnter/DragLeave as that
/// changes, so nothing more is required.
/// </para>
/// <para>
/// Registered as <see cref="IPlatformDragSource"/> in <see cref="ShapeEnginePlatform"/>; without it,
/// <c>DragDrop.DoDragDropAsync</c> finds no service and silently resolves to
/// <see cref="DragDropEffects.None"/> - no exception, no drag, nothing to debug from.
/// </para>
/// </remarks>
internal sealed class ShapeEngineDragSource : Game.CustomEvent, IPlatformDragSource
{
    private const float IndicatorFontSize = 16f;

    // The default font never changes once raylib has one, so this is read once rather than every frame
    // the indicator draws.
    private static readonly Font IndicatorFont = Raylib.GetFontDefault();

    private sealed record ActiveDrag(
        IDataTransfer Data,
        DragDropEffects AllowedEffects,
        TaskCompletionSource<DragDropEffects> Completion,
        string IndicatorText,
        Vector2 IndicatorTextSize);

    private static ActiveDrag? active;

    /// <summary>
    /// Registered with an order past anything a game would reasonably give a surface, so every surface's
    /// own <see cref="Pump"/> - called from its own <c>PreHandleInput</c> - has already had first claim on
    /// this frame's release by the time <see cref="PreHandleInput"/> below runs its cleanup pass.
    /// </summary>
    public ShapeEngineDragSource() : base(order: Int32.MaxValue)
    {
    }

    protected override void PreHandleInput(GameTime time, Vector2 mousePosGame, Vector2 mousePosGameUi, Vector2 mousePosUi)
        => CancelIfReleasedOutsideEverySurface();

    /// <summary>
    /// Draws a small label near the cursor naming whatever is being dragged, while a drag is active.
    /// </summary>
    /// <remarks>
    /// A real platform drag source hands the OS a drag image for free; there is no OS drag loop here to
    /// do that, so this stands in for it. Drawn last among every custom event - see the constructor - so
    /// it lands on top of every surface and the game's own UI, regardless of which one, if any, the
    /// pointer happens to be over.
    /// </remarks>
    protected override void PostDrawUi(ScreenInfo info)
    {
        if (active is not { } drag) return;

        const float padding = 8f;

        var origin = Raylib.GetMousePosition() + new Vector2(18f, 18f);
        var boxSize = drag.IndicatorTextSize + new Vector2(padding * 2f, padding * 2f);

        Raylib.DrawRectangleRounded(new Rectangle(origin.X, origin.Y, boxSize.X, boxSize.Y), 0.3f, 6, new RlColor(20, 20, 30, 220));
        Raylib.DrawTextEx(IndicatorFont, drag.IndicatorText, origin + new Vector2(padding, padding), IndicatorFontSize, 1f, RlColor.White);
    }

    public Task<DragDropEffects> DoDragDropAsync(PointerPressedEventArgs triggerEvent, IDataTransfer data, DragDropEffects allowedEffects)
    {
        // Computed once here rather than every frame in PostDrawUi: neither the text nor its measured
        // size can change for the life of the drag.
        var text = data.TryGetText() ?? "Dragging";
        var textSize = Raylib.MeasureTextEx(IndicatorFont, text, IndicatorFontSize, 1f);

        var completion = new TaskCompletionSource<DragDropEffects>();
        active = new ActiveDrag(data, allowedEffects, completion, text, textSize);
        return completion.Task;
    }

    /// <summary>
    /// Feeds the active drag, if there is one, into <paramref name="surface"/> - but only when the
    /// pointer is actually over it this frame. Called from every surface's own <c>PreHandleInput</c>, so
    /// whichever one currently has the pointer is the one that reports it; the rest no-op.
    /// </summary>
    internal static void Pump(AvaloniaSurface surface)
    {
        if (active is not { } drag || !surface.WantsPointer) return;
        if (surface.TopLevel.Impl.InputRoot is not { } inputRoot) return;

        Report(drag, inputRoot, surface.GetPointerPosition(), Released());
    }

    /// <summary>
    /// Resolves a still-active drag released with the pointer over no surface at all - <see cref="Pump"/>
    /// alone never sees this case, since no surface's <c>WantsPointer</c> is ever true for it. Runs once
    /// per frame, after every surface's own <see cref="Pump"/> has had first claim - see the ordering
    /// remark on the constructor.
    /// </summary>
    private static void CancelIfReleasedOutsideEverySurface()
    {
        if (active is not { } drag || !Released()) return;

        active = null;
        drag.Completion.TrySetResult(DragDropEffects.None);
    }

    private static bool Released() => Raylib.IsMouseButtonReleased(RlMouseButton.Left);

    private static void Report(ActiveDrag drag, IInputRoot inputRoot, Point position, bool released)
    {
        var raw = new RawDragEvent(
            DragDropDevice.Instance,
            released ? RawDragEventType.Drop : RawDragEventType.DragOver,
            inputRoot,
            position,
            drag.Data,
            drag.AllowedEffects,
            KeyMap.GetModifiers());

        DragDropDevice.Instance.ProcessRawEvent(raw);

        if (!released) return;

        active = null;
        drag.Completion.TrySetResult(raw.Effects);
    }
}
