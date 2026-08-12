using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Raylib_cs;

namespace ShapeEngine.Avalonia;

/// <summary>An <see cref="ICursorFactory"/> that maps Avalonia cursors onto raylib's cursor set.</summary>
internal sealed class ShapeEngineCursorFactory : ICursorFactory
{
    public ICursorImpl GetCursor(StandardCursorType cursorType)
        => new ShapeEngineStandardCursorImpl(ToRaylibCursor(cursorType));

    /// <remarks>
    /// raylib cannot supply cursor bitmaps, so this falls back to the arrow rather than throwing - a
    /// control asking for a custom cursor should still be usable.
    /// </remarks>
    public ICursorImpl CreateCursor(Bitmap cursor, PixelPoint hotSpot)
        => new ShapeEngineStandardCursorImpl(MouseCursor.Default);

    private static MouseCursor ToRaylibCursor(StandardCursorType cursorType)
        => cursorType switch
        {
            StandardCursorType.Arrow => MouseCursor.Default,
            StandardCursorType.Ibeam => MouseCursor.IBeam,
            StandardCursorType.Cross => MouseCursor.Crosshair,
            StandardCursorType.Hand => MouseCursor.PointingHand,
            StandardCursorType.No => MouseCursor.NotAllowed,
            StandardCursorType.SizeAll or StandardCursorType.DragMove => MouseCursor.ResizeAll,
            StandardCursorType.SizeWestEast
                or StandardCursorType.LeftSide
                or StandardCursorType.RightSide => MouseCursor.ResizeEw,
            StandardCursorType.SizeNorthSouth
                or StandardCursorType.TopSide
                or StandardCursorType.BottomSide => MouseCursor.ResizeNs,
            StandardCursorType.TopLeftCorner or StandardCursorType.BottomRightCorner => MouseCursor.ResizeNwse,
            StandardCursorType.TopRightCorner or StandardCursorType.BottomLeftCorner => MouseCursor.ResizeNesw,

            // Wait, AppStarting, Help, UpArrow, DragCopy, DragLink and None have no raylib equivalent.
            _ => MouseCursor.Default
        };
}

/// <summary>A cursor identified by the raylib cursor it maps to.</summary>
internal sealed class ShapeEngineStandardCursorImpl : ICursorImpl
{
    public MouseCursor Cursor { get; }

    public ShapeEngineStandardCursorImpl(MouseCursor cursor) => Cursor = cursor;

    public void Dispose() { }
}
