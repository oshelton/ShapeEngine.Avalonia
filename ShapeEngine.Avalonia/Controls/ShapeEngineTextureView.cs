using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Raylib_cs;
using ShapeEngine.Color;
using ShapeEngine.Core.GameDef;
using ShapeEngine.Core.Structs;
using AvPixelFormat = Avalonia.Platform.PixelFormat;
using SeRect = ShapeEngine.Geometry.RectDef.Rect;

namespace ShapeEngine.Avalonia.Controls;

/// <summary>
/// Base for Avalonia controls that display content drawn with ShapeEngine's own drawing functions.
/// </summary>
/// <remarks>
/// The content is rendered into a private raylib render texture during the game's frame, then copied
/// into a bitmap the control draws. Nothing hands the OpenGL context between raylib and Skia, so the
/// two renderers cannot corrupt each other's state - the price is a texture and a read back that stalls
/// the pipeline, scaling with the control's area. How often that happens is the whole difference between
/// <see cref="ShapeEngineStaticTextureView"/> and <see cref="ShapeEngineAnimatedTextureView"/>; for
/// content large enough that it matters, <see cref="ShapeEngineDirectView"/> avoids it entirely.
/// <para>
/// The redraw happens after a surface has already composited, so the control shows what was drawn last
/// frame. Expect one frame of latency.
/// </para>
/// </remarks>
public abstract class ShapeEngineTextureView : Control
{
    /// <summary>Caps the texture a very large scale can ask for - read back cost grows with its area.</summary>
    private const int MaxTextureSize = 4096;

    private const double MinRenderScale = 0.25;
    private const double MaxRenderScale = 4.0;

    private readonly FramePump pump;

    private RenderTexture2D renderTexture;
    private WriteableBitmap? bitmap;
    private PixelSize textureSize;
    private bool hasTexture;
    private bool isDirty = true;

    protected ShapeEngineTextureView() => pump = new FramePump(this);

    /// <summary>
    /// Draws the content, in texture pixel coordinates. Called during the game's frame, so ShapeEngine's
    /// drawing functions can be used directly.
    /// </summary>
    public Action<SeRect>? DrawContent { get; set; }

    /// <summary>The colour the texture is cleared to before each draw. Transparent by default.</summary>
    public ColorRgba ClearColor { get; set; } = ColorRgba.Transparent;

    /// <summary>Marks the content as out of date, so it is drawn again on the next frame.</summary>
    public void InvalidateContent() => isDirty = true;

    /// <summary>Whether the content should be drawn again this frame.</summary>
    /// <param name="deltaTime">Seconds since the previous frame.</param>
    /// <param name="contentIsDirty">
    /// Whether the texture was just created or resized, or <see cref="InvalidateContent"/> was called.
    /// </param>
    protected abstract bool ShouldRedraw(float deltaTime, bool contentIsDirty);

    /// <remarks>
    /// Posted rather than called directly. Attach and detach can happen from deep inside Avalonia's own
    /// internals - reparenting a control mid-frame, say - which can itself be running from inside the
    /// engine's own foreach over its <c>CustomEvent</c> set. Registering synchronously there would mutate
    /// that same set while it is being enumerated. Posting defers the call to <c>PumpDispatcher</c>, which
    /// runs outside that loop.
    /// </remarks>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(() => Game.Instance.AddCustomEvent(pump));
    }

    /// <remarks>See <see cref="OnAttachedToVisualTree"/> for why this is posted rather than immediate.</remarks>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Dispatcher.UIThread.Post(() => Game.Instance.RemoveCustomEvent(pump));
        Release();

        base.OnDetachedFromVisualTree(e);
    }

    public override void Render(DrawingContext context)
    {
        if (bitmap is not null) context.DrawImage(bitmap, new Rect(Bounds.Size));
    }

    /// <summary>Redraws the texture and copies it into the bitmap, if the view wants a redraw.</summary>
    private void RenderFrame(float deltaTime)
    {
        if (DrawContent is not { } drawContent) return;
        if (!EnsureTexture()) return;
        if (!ShouldRedraw(deltaTime, isDirty)) return;

        isDirty = false;

        Raylib.BeginTextureMode(renderTexture);
        Raylib.ClearBackground(ClearColor.ToRayColor());
        drawContent(new SeRect(0, 0, textureSize.Width, textureSize.Height));
        Raylib.EndTextureMode();

        CopyToBitmap();
        InvalidateVisual();
    }

    /// <summary>Creates or resizes the render texture to match the control's size in physical pixels.</summary>
    /// <remarks>
    /// Reallocating on every distinct size is fine because the sizes arrive settled rather than streamed:
    /// the engine polls the window size once per frame, and dragging a window border on Windows blocks
    /// inside the platform's modal resize loop, so no frames run until the drag ends.
    /// </remarks>
    private bool EnsureTexture()
    {
        if (Bounds.Width <= 0.0 || Bounds.Height <= 0.0) return false;

        var (scaleX, scaleY) = GetRenderScale();
        var size = new PixelSize(
            Math.Clamp((int)Math.Round(Bounds.Width * scaleX), 1, MaxTextureSize),
            Math.Clamp((int)Math.Round(Bounds.Height * scaleY), 1, MaxTextureSize));

        if (hasTexture && size == textureSize) return true;

        Release();

        renderTexture = Raylib.LoadRenderTexture(size.Width, size.Height);
        textureSize = size;
        hasTexture = true;

        bitmap = new WriteableBitmap(size, new Vector(96, 96), AvPixelFormat.Rgba8888, AlphaFormat.Unpremul);

        // A fresh texture holds nothing, so it needs a draw whatever the redraw policy says.
        isDirty = true;
        return true;
    }

    /// <summary>The scale the control is actually rasterized at, per axis.</summary>
    /// <remarks>
    /// The window's DPI is only half of it: <see cref="Visual.Bounds"/> is in the control's own coordinate
    /// space, so a <c>Viewbox</c> - or any other ancestor scale, which is what
    /// <see cref="AvaloniaSurface.ScaleContent"/> uses - leaves the control reporting its unscaled size
    /// while being drawn much larger. Sizing the texture from bounds alone therefore renders at the small
    /// size and lets Skia upscale the result, which is exactly where these views used to go soft.
    /// </remarks>
    private (double X, double Y) GetRenderScale()
    {
        var scaling = (VisualRoot as TopLevel)?.RenderScaling ?? 1.0;

        var x = scaling;
        var y = scaling;

        // Column lengths rather than M11/M22 alone, so a rotation between here and the root still gives
        // the scale it contributes rather than its cosine.
        if (VisualRoot is Visual root && this.TransformToVisual(root) is { } transform)
        {
            x *= Math.Sqrt(transform.M11 * transform.M11 + transform.M12 * transform.M12);
            y *= Math.Sqrt(transform.M21 * transform.M21 + transform.M22 * transform.M22);
        }

        // Clamped rather than rounded to a step: the scale is used exactly, since read back cost grows
        // with the texture's area and rounding up would pay for resolution nothing asked for.
        return (Math.Clamp(x, MinRenderScale, MaxRenderScale), Math.Clamp(y, MinRenderScale, MaxRenderScale));
    }

    private unsafe void CopyToBitmap()
    {
        if (bitmap is null) return;

        var image = Raylib.LoadImageFromTexture(renderTexture.Texture);

        using (var locked = bitmap.Lock())
        {
            var rowBytes = textureSize.Width * 4;
            var source = (byte*)image.Data;
            var destination = (byte*)locked.Address;

            // Render textures come back bottom-up because that is how OpenGL stores them, so this reads
            // source rows back to front rather than flipping the buffer first - one pass instead of two.
            for (var y = 0; y < textureSize.Height; y++)
            {
                var sourceRow = textureSize.Height - 1 - y;
                Buffer.MemoryCopy(source + sourceRow * rowBytes, destination + y * locked.RowBytes, rowBytes, rowBytes);
            }
        }

        Raylib.UnloadImage(image);
    }

    private void Release()
    {
        if (hasTexture)
        {
            Raylib.UnloadRenderTexture(renderTexture);
            hasTexture = false;
        }

        bitmap?.Dispose();
        bitmap = null;
    }

    /// <summary>Drives <see cref="RenderFrame"/> once per frame from the game loop.</summary>
    /// <remarks>
    /// <c>PreDrawUi</c> is the one drawing hook that runs exactly once per frame and is not already
    /// inside a render target, so the view's texture is never bound inside another one.
    /// </remarks>
    private sealed class FramePump : Game.CustomEvent
    {
        private readonly ShapeEngineTextureView view;

        public FramePump(ShapeEngineTextureView view) => this.view = view;

        protected override void PreDrawUi(ScreenInfo info) => view.RenderFrame(Game.Instance.Time.Delta);
    }
}
