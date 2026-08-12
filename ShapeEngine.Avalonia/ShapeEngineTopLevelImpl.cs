using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.Raw;
using Avalonia.Input.TextInput;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Platform.Surfaces;
using Raylib_cs;
using ShapeEngine.Avalonia.Gpu;
using ShapeEngine.Avalonia.Input;
using ShapeEngine.Avalonia.Storage;
using AvCompositor = Avalonia.Rendering.Composition.Compositor;

namespace ShapeEngine.Avalonia;

/// <summary>
/// An <see cref="ITopLevelImpl"/> that renders into an offscreen OpenGL surface raylib can draw.
/// </summary>
/// <remarks>
/// The surface is recreated only when the pixel size changes, because the framebuffer, its colour
/// texture and its stencil attachment are all fixed size. A DPI-scaling-only change updates the
/// surface in place instead, since none of those need reallocating just because scaling changed.
/// </remarks>
internal sealed class ShapeEngineTopLevelImpl : ITopLevelImpl
{
    private readonly RaylibPlatformGraphics platformGraphics;
    private readonly IClipboard clipboard;
    private readonly IStorageProvider storageProvider = new ShapeEngineStorageProvider();

    /// <summary>Reports whether the focused control currently wants typed characters.</summary>
    public ShapeEngineTextInputMethod TextInputMethod { get; } = new();

    private RaylibGlSurface? surface;
    private WindowTransparencyLevel transparencyLevel = WindowTransparencyLevel.Transparent;
    private PixelSize renderSize;
    private IInputRoot? inputRoot;
    private MouseCursor cursor = MouseCursor.Default;
    private bool isDisposed;

    public ShapeEngineTopLevelImpl(RaylibPlatformGraphics platformGraphics, IClipboard clipboard, AvCompositor compositor)
    {
        this.platformGraphics = platformGraphics;
        this.clipboard = clipboard;
        Compositor = compositor;

        platformGraphics.AddRef();
    }

    #region ITopLevelImpl surface and sizing

    public AvCompositor Compositor { get; }

    public Size ClientSize { get; private set; }

    public double RenderScaling { get; private set; } = 1.0;

    double ITopLevelImpl.DesktopScaling => 1.0;

    IPlatformHandle? ITopLevelImpl.Handle => null;

    IPlatformRenderSurface[] ITopLevelImpl.Surfaces => [GetOrCreateSurface()];

    AcrylicPlatformCompensationLevels ITopLevelImpl.AcrylicCompensationLevels => new(1.0, 1.0, 1.0);

    /// <summary>The surface raylib presents, or <c>null</c> before the first frame.</summary>
    public RaylibGlSurface? TryGetSurface() => surface;

    private RaylibGlSurface GetOrCreateSurface() => surface ??= CreateSurface();

    private RaylibGlSurface CreateSurface()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        return new RaylibGlSurface(platformGraphics.GetSharedContext(), renderSize, RenderScaling);
    }

    /// <summary>Resizes the top level, recreating the render surface only if the pixel size changed.</summary>
    /// <remarks>
    /// Scaling can change every frame while the framebuffer stays the same size, so a scaling-only
    /// change updates the surface in place rather than reallocating its texture and renderbuffer.
    /// </remarks>
    public void SetRenderSize(PixelSize newRenderSize, double newRenderScaling)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator - only an exact match avoids work
        var hasScalingChanged = RenderScaling != newRenderScaling;
        if (renderSize == newRenderSize && !hasScalingChanged) return;

        var oldClientSize = ClientSize;
        var unclampedClientSize = newRenderSize.ToSize(newRenderScaling);

        ClientSize = new Size(Math.Max(unclampedClientSize.Width, 0.0), Math.Max(unclampedClientSize.Height, 0.0));
        RenderScaling = newRenderScaling;

        if (renderSize != newRenderSize)
        {
            renderSize = newRenderSize;

            surface?.Dispose();
            surface = isDisposed ? null : CreateSurface();
        }
        else if (surface is not null)
        {
            surface.RenderScaling = RenderScaling;
        }

        if (hasScalingChanged) ScalingChanged?.Invoke(RenderScaling);

        if (oldClientSize != ClientSize)
        {
            Resized?.Invoke(ClientSize, hasScalingChanged ? WindowResizeReason.DpiChange : WindowResizeReason.Unspecified);
        }
    }

    #endregion

    #region ITopLevelImpl callbacks

    public Action<Rect>? Paint { get; set; }

    public Action<Size, WindowResizeReason>? Resized { get; set; }

    public Action? Closed { get; set; }

    public Action<RawInputEventArgs>? Input { get; set; }

    public Action? LostFocus { get; set; }

    public Action<double>? ScalingChanged { get; set; }

    public Action<WindowTransparencyLevel>? TransparencyLevelChanged { get; set; }

    /// <summary>Raised when Avalonia asks for a different mouse cursor.</summary>
    public Action<MouseCursor>? CursorChanged { get; set; }

    public WindowTransparencyLevel TransparencyLevel
    {
        get => transparencyLevel;
        private set
        {
            if (transparencyLevel.Equals(value)) return;

            transparencyLevel = value;
            TransparencyLevelChanged?.Invoke(value);
        }
    }

    #endregion

    #region Input

    void ITopLevelImpl.SetInputRoot(IInputRoot root) => inputRoot = root;

    /// <summary>The input root Avalonia handed this top level, for raw input other than the kinds above.</summary>
    /// <remarks>Used by <see cref="ShapeEngineDragSource"/>, which needs one to construct its own raw drag events.</remarks>
    internal IInputRoot? InputRoot => inputRoot;

    private bool Send(RawInputEventArgs args)
    {
        if (Input is not { } input) return false;

        input(args);
        return args.Handled;
    }

    public bool OnPointerMoved(Point position, RawInputModifiers modifiers, ulong timestamp)
        => inputRoot is { } root
           && Send(new RawPointerEventArgs(ShapeEngineDevices.Mouse, timestamp, root, RawPointerEventType.Move, position, modifiers));

    public bool OnPointerButton(RawPointerEventType type, Point position, RawInputModifiers modifiers, ulong timestamp)
        => inputRoot is { } root
           && Send(new RawPointerEventArgs(ShapeEngineDevices.Mouse, timestamp, root, type, position, modifiers));

    public bool OnPointerWheel(Point position, Vector delta, RawInputModifiers modifiers, ulong timestamp)
        => inputRoot is { } root
           && Send(new RawMouseWheelEventArgs(ShapeEngineDevices.Mouse, timestamp, root, position, delta, modifiers));

    public bool OnPointerLeft(ulong timestamp)
        => inputRoot is { } root
           && Send(new RawPointerEventArgs(
               ShapeEngineDevices.Mouse, timestamp, root, RawPointerEventType.LeaveWindow, new Point(-1, -1), RawInputModifiers.None));

    public bool OnKey(RawKeyEventType type, Key key, PhysicalKey physicalKey, RawInputModifiers modifiers, string? keySymbol, ulong timestamp)
        => inputRoot is { } root
           && Send(new RawKeyEventArgs(ShapeEngineDevices.Keyboard, timestamp, root, type, key, modifiers, physicalKey, keySymbol));

    public bool OnTextInput(string text, ulong timestamp)
        => inputRoot is { } root
           && Send(new RawTextInputEventArgs(ShapeEngineDevices.Keyboard, timestamp, root, text));

    /// <summary>Asks Avalonia to render the given area now.</summary>
    /// <remarks>
    /// This is what drives rendering: Avalonia renders in response to <see cref="Paint"/>, and with no
    /// OS window there are no paint messages to raise it.
    /// </remarks>
    public void OnDraw(Rect rect)
    {
        if (isDisposed) return;

        Paint?.Invoke(rect);
    }

    #endregion

    #region ITopLevelImpl coordinate space and services

    // Only correct for a surface covering the window; an anchored one is also offset by its destination
    // rectangle. Avalonia wants real screen coordinates, which a backend with no OS window cannot give.
    Point ITopLevelImpl.PointToClient(PixelPoint point) => point.ToPoint(RenderScaling);

    PixelPoint ITopLevelImpl.PointToScreen(Point point) => PixelPoint.FromPoint(point, RenderScaling);

    void ITopLevelImpl.SetCursor(ICursorImpl? newCursor)
    {
        var raylibCursor = (newCursor as ShapeEngineStandardCursorImpl)?.Cursor ?? MouseCursor.Default;
        if (cursor == raylibCursor) return;

        cursor = raylibCursor;
        CursorChanged?.Invoke(raylibCursor);
    }

    /// <remarks>
    /// <c>null</c> makes Avalonia host menus, tooltips and combo box drop-downs as overlays inside this
    /// top level rather than asking for real OS windows.
    /// </remarks>
    IPopupImpl? ITopLevelImpl.CreatePopup() => null;

    void ITopLevelImpl.SetTransparencyLevelHint(IReadOnlyList<WindowTransparencyLevel> transparencyLevels)
    {
        // The surface is always composited over the game, so it is always transparent regardless of
        // what the application asks for. Without this the theme draws an opaque fallback background.
        TransparencyLevel = WindowTransparencyLevel.Transparent;
    }

    void ITopLevelImpl.SetFrameThemeVariant(PlatformThemeVariant? themeVariant) { }

    object? IOptionalFeatureProvider.TryGetFeature(Type featureType)
    {
        if (featureType == typeof(IClipboard)) return clipboard;
        if (featureType == typeof(IStorageProvider)) return storageProvider;
        if (featureType == typeof(ITextInputMethodImpl)) return TextInputMethod;

        return null;
    }

    #endregion

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        surface?.Dispose();
        surface = null;

        Closed?.Invoke();

        platformGraphics.Release();
    }
}
