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
    private bool EnsureTexture()
    {
        if (Bounds.Width <= 0.0 || Bounds.Height <= 0.0) return false;

        var scaling = (VisualRoot as TopLevel)?.RenderScaling ?? 1.0;
        var size = new PixelSize(
            Math.Max((int)Math.Round(Bounds.Width * scaling), 1),
            Math.Max((int)Math.Round(Bounds.Height * scaling), 1));

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
