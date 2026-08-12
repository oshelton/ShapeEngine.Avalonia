using Avalonia.OpenGL;
using Raylib_cs;

namespace ShapeEngine.Avalonia.Gpu;

/// <summary>
/// Hands the OpenGL context over to Skia for the duration of a <c>using</c> block and puts raylib's
/// state back afterwards.
/// </summary>
/// <remarks>
/// raylib and Skia both assume they own the GL context. rlgl caches its state and only re-applies what
/// it believes changed, so anything Skia touches has to be restored by hand.
/// </remarks>
internal readonly struct RlglStateGuard : IDisposable
{
    private const int GlViewport = 0x0BA2;

    private readonly GlInterface gl;
    private readonly int previousFramebuffer;
    private readonly int viewportX;
    private readonly int viewportY;
    private readonly int viewportWidth;
    private readonly int viewportHeight;

    private RlglStateGuard(GlInterface gl, int previousFramebuffer, int viewportX, int viewportY, int viewportWidth, int viewportHeight)
    {
        this.gl = gl;
        this.previousFramebuffer = previousFramebuffer;
        this.viewportX = viewportX;
        this.viewportY = viewportY;
        this.viewportWidth = viewportWidth;
        this.viewportHeight = viewportHeight;
    }

    /// <summary>Width of the render target that was bound when the guard was entered.</summary>
    public int ViewportWidth => viewportWidth;

    /// <summary>Height of the render target that was bound when the guard was entered.</summary>
    public int ViewportHeight => viewportHeight;

    /// <summary>
    /// Flushes raylib's pending geometry and records the state that has to survive the Skia pass.
    /// </summary>
    /// <remarks>
    /// The framebuffer and viewport are read back from OpenGL rather than from rlgl, whose
    /// <c>GetFramebufferWidth</c> keeps reporting the last render texture's size after that texture's
    /// draw pass has ended - restoring from it shrinks everything drawn afterwards into a corner.
    /// </remarks>
    public static RlglStateGuard Enter(GlInterface gl)
    {
        // Anything still queued would otherwise be drawn with Skia's shader, blend function and
        // framebuffer binding rather than raylib's.
        Rlgl.DrawRenderBatchActive();

        int framebuffer;
        gl.GetIntegerv(GlConsts.GL_FRAMEBUFFER_BINDING, out framebuffer);

        // GL_VIEWPORT writes four integers, so the query needs room for all of them. Stack-allocated
        // rather than a heap array: the guard only needs the values, not the buffer, once this returns.
        Span<int> viewport = stackalloc int[4];
        gl.GetIntegerv(GlViewport, out viewport[0]);

        return new RlglStateGuard(gl, framebuffer, viewport[0], viewport[1], viewport[2], viewport[3]);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // The shader, VAO and textures are re-bound on the next batch draw. Everything below is state
        // raylib either caches or never sets defensively.
        gl.BindFramebuffer(GlConsts.GL_FRAMEBUFFER, previousFramebuffer);
        gl.Viewport(viewportX, viewportY, viewportWidth, viewportHeight);

        Rlgl.DisableScissorTest();
        Rlgl.DisableDepthTest();
        Rlgl.DisableTexture();
        Rlgl.EnableColorBlend();

        ForceAlphaBlendMode();
    }

    /// <summary>Makes rlgl re-issue <c>glBlendFunc</c> for its normal alpha blend mode.</summary>
    /// <remarks>
    /// <c>rlSetBlendMode</c> short-circuits when the requested mode equals the cached one, so switching
    /// straight to <see cref="BlendMode.Alpha"/> is a no-op whenever that is already rlgl's cached mode -
    /// which it usually is, since it is raylib's default. Skia changes the actual <c>glBlendFunc</c>
    /// behind rlgl's back without rlgl's cache knowing, so a real change is forced first.
    /// </remarks>
    public static void ForceAlphaBlendMode()
    {
        Rlgl.SetBlendMode(BlendMode.Additive);
        Rlgl.SetBlendMode(BlendMode.Alpha);
    }
}
