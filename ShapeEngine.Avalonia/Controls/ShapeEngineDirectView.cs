using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.OpenGL;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Raylib_cs;
using ShapeEngine.Avalonia.Gpu;
using ShapeEngine.Core.GameDef;
using ShapeEngine.Core.Structs;
using SeRect = ShapeEngine.Geometry.RectDef.Rect;
using SkMatrix = SkiaSharp.SKMatrix;
using SkRect = SkiaSharp.SKRectI;

namespace ShapeEngine.Avalonia.Controls;

/// <summary>
/// Draws ShapeEngine content straight into Avalonia's framebuffer, with no intermediate texture.
/// </summary>
/// <remarks>
/// Where <see cref="ShapeEngineTextureView"/> copies its result back through system memory, this hands
/// raylib the framebuffer Avalonia is already rendering into - no texture, no read back, no frame of
/// latency. It works because Avalonia's Skia backend runs on raylib's own OpenGL context.
/// <para>
/// The trade is that raylib draws outside Skia's knowledge. The control's transform and clip are read
/// off the canvas and applied to rlgl, but Avalonia's opacity and render effects are Skia compositing
/// steps this bypasses, so a fading parent will not fade it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// new ShapeEngineDirectView
/// {
///     Height = 190,
///     DrawContent = bounds => new Circle(bounds.Center, 40f).Draw(ColorRgba.White)
/// }
/// </code>
/// </example>
public sealed class ShapeEngineDirectView : Control
{
    private readonly FramePump pump;

    public ShapeEngineDirectView() => pump = new FramePump(this);

    /// <summary>
    /// Draws the content, in the control's own coordinate space. Called during Avalonia's render pass,
    /// so ShapeEngine's drawing functions can be used directly.
    /// </summary>
    /// <remarks>
    /// Coordinates run from the origin to the control's width and height; a parent <c>Viewbox</c>'s
    /// scale, the window's DPI and the control's position on screen are all applied for you.
    /// </remarks>
    public Action<SeRect>? DrawContent { get; set; }

    /// <summary>
    /// Whether the view redraws itself every frame, so live content keeps animating. On by default, so
    /// direct content animates with no setup - the view reports itself dirty each frame, which is what tells
    /// the host <see cref="AvaloniaSurface"/> to keep drawing it.
    /// </summary>
    /// <remarks>
    /// Turn it off for content that only changes occasionally and call <see cref="InvalidateContent"/> when
    /// it does; the host surface then rests between changes instead of rasterizing this view every frame.
    /// </remarks>
    public bool RedrawContinuously { get; set; } = true;

    /// <summary>Marks the content out of date, so it is drawn again on the next frame.</summary>
    /// <remarks>Needed only with <see cref="RedrawContinuously"/> off; posted, so it is safe to call from
    /// anywhere.</remarks>
    public void InvalidateContent() => Dispatcher.UIThread.Post(InvalidateVisual);

    public override void Render(DrawingContext context)
    {
        if (DrawContent is not { } drawContent) return;
        if (Bounds.Width <= 0.0 || Bounds.Height <= 0.0) return;

        context.Custom(new DrawOperation(new Rect(Bounds.Size), drawContent));
    }

    /// <remarks>
    /// Posted rather than called directly: attach can run from inside the engine's own iteration over its
    /// custom events, and registering synchronously there would mutate that set mid-enumeration.
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
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Invalidates the view once per frame so live content keeps reporting itself dirty.</summary>
    /// <remarks>
    /// Only marks dirty - unlike <see cref="ShapeEngineTextureView"/> the view draws directly in
    /// <see cref="Render"/>, so there is nothing to render here. <c>PreDrawUi</c> runs once per frame.
    /// </remarks>
    private sealed class FramePump : Game.CustomEvent
    {
        private readonly ShapeEngineDirectView view;

        public FramePump(ShapeEngineDirectView view) => this.view = view;

        protected override void PreDrawUi(ScreenInfo info)
        {
            if (view.RedrawContinuously) view.InvalidateVisual();
        }
    }

    /// <summary>Hands the OpenGL context to raylib for the duration of Avalonia's render pass.</summary>
    private sealed class DrawOperation : ICustomDrawOperation
    {
        private const int GlStencilTest = 0x0B71;
        private const int GlBlend = 0x0BE2;

        private readonly Action<SeRect> drawContent;

        public DrawOperation(Rect bounds, Action<SeRect> drawContent)
        {
            Bounds = bounds;
            this.drawContent = drawContent;
        }

        public Rect Bounds { get; }

        public bool HitTest(Point p) => Bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            if (context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) is not ISkiaSharpApiLeaseFeature feature) return;

            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;

            var clip = canvas.DeviceClipBounds;
            if (clip.Width <= 0 || clip.Height <= 0) return;

            // Skia's queued work has to land before raylib starts issuing calls into the same target.
            canvas.Flush();

            var gl = ShapeEnginePlatform.PlatformGraphics.GetSharedContext().GlInterface;

            using (var guard = RlglStateGuard.Enter(gl))
            {
                PrepareRaylibState(gl);

                // Saved and reassigned rather than pushed and popped: pushing in modelview mode redirects
                // rlgl's stack to its internal transform, so a pop leaks the projection into every later
                // raylib frame.
                var savedProjection = Rlgl.GetMatrixProjection();
                var savedModelview = Rlgl.GetMatrixModelview();

                // Avalonia's target is bottom-origin, so Skia's device coordinates and OpenGL's already
                // agree - hence the plain bottom-left projection and the unflipped scissor below. Both
                // matrices are transposed because raylib's Matrix is column-vector where
                // System.Numerics is row-vector, and Raylib-cs copies the memory straight across.
                Rlgl.SetMatrixProjection(Matrix4x4.Transpose(
                    Matrix4x4.CreateOrthographicOffCenter(0f, guard.ViewportWidth, 0f, guard.ViewportHeight, -1f, 1f)));
                Rlgl.SetMatrixModelView(Matrix4x4.Transpose(ToMatrix(canvas.TotalMatrix)));

                SetClip(clip);
                drawContent(new SeRect(0, 0, (float)Bounds.Width, (float)Bounds.Height));

                // Flush while our matrices and scissor are still in force, then hand raylib its own back.
                Rlgl.DrawRenderBatchActive();

                Rlgl.DisableScissorTest();
                Rlgl.SetMatrixProjection(savedProjection);
                Rlgl.SetMatrixModelView(savedModelview);
            }

            // raylib has changed program, buffers and blend state behind Skia's back.
            lease.GrContext.ResetContext();
        }

        /// <summary>
        /// Puts the OpenGL state into the shape raylib assumes before handing it the framebuffer.
        /// </summary>
        /// <remarks>
        /// The stencil test is the one that matters: Skia clips with it and leaves it enabled, so
        /// raylib's geometry is silently rejected - no error, no output, nothing to debug from.
        /// </remarks>
        private static void PrepareRaylibState(GlInterface gl)
        {
            gl.Disable(GlStencilTest);
            gl.Disable(GlConsts.GL_DEPTH_TEST);
            gl.Disable(GlConsts.GL_CULL_FACE);
            gl.Enable(GlBlend);

            RlglStateGuard.ForceAlphaBlendMode();
        }

        /// <summary>Converts Skia's 2D canvas transform into the 4x4 rlgl expects.</summary>
        /// <remarks>
        /// This is what places and scales the control - the canvas transform already folds together its
        /// position, the DPI scale, any <c>Viewbox</c> scale and any render transform.
        /// </remarks>
        private static Matrix4x4 ToMatrix(SkMatrix matrix)
            => new(
                matrix.ScaleX, matrix.SkewY, 0f, 0f,
                matrix.SkewX, matrix.ScaleY, 0f, 0f,
                0f, 0f, 1f, 0f,
                matrix.TransX, matrix.TransY, 0f, 1f);

        /// <summary>Turns Avalonia's clip into a GL scissor box.</summary>
        /// <remarks>
        /// The clip lives in Skia and does not constrain raylib's calls, so without this the drawing
        /// spills over whatever else the surface is showing.
        /// </remarks>
        private static void SetClip(SkRect clip)
        {
            Rlgl.EnableScissorTest();
            Rlgl.Scissor(clip.Left, clip.Top, clip.Width, clip.Height);
        }

        public void Dispose() { }
    }
}
