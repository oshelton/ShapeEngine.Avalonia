using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Surfaces;
using Raylib_cs;

namespace ShapeEngine.Avalonia.Gpu;

/// <summary>
/// An offscreen OpenGL framebuffer that Avalonia renders into and raylib draws as a plain texture.
/// </summary>
/// <remarks>
/// Built by hand rather than with <c>Raylib.LoadRenderTexture</c>, which attaches a depth-only buffer
/// where Skia needs a stencil attachment for clipping and anti-aliasing. The colour attachment still
/// goes through rlgl, so raylib owns the texture and can draw it with the normal texture functions.
/// </remarks>
internal sealed class RaylibGlSurface : IGlPlatformSurface, IDisposable
{
    private readonly RaylibGlContext context;
    private readonly int depthStencilRenderbufferId;

    /// <summary>The colour attachment, described so raylib's draw functions can consume it.</summary>
    public Texture2D Texture { get; }

    /// <summary>Size of the surface in physical pixels.</summary>
    public PixelSize Size { get; }

    /// <summary>The scale factor Avalonia renders this surface at. May change without a resize.</summary>
    public double RenderScaling { get; set; }

    /// <summary>The OpenGL framebuffer Avalonia renders into.</summary>
    public int FramebufferId { get; }

    public bool IsDisposed { get; private set; }

    public unsafe RaylibGlSurface(RaylibGlContext context, PixelSize size, double renderScaling)
    {
        this.context = context;
        Size = new PixelSize(Math.Max(size.Width, 1), Math.Max(size.Height, 1));
        RenderScaling = renderScaling;

        var gl = context.GlInterface;

        var textureId = Rlgl.LoadTexture(null, Size.Width, Size.Height, PixelFormat.UncompressedR8G8B8A8, 1);
        if (textureId == 0)
            throw new InvalidOperationException("Couldn't create the OpenGL texture backing the Avalonia surface.");

        Texture = new Texture2D
        {
            Id = textureId,
            Width = Size.Width,
            Height = Size.Height,
            Mipmaps = 1,
            Format = PixelFormat.UncompressedR8G8B8A8
        };

        // Setup rather than rendering, but it still rebinds GL state raylib is relying on.
        using (RlglStateGuard.Enter(gl))
        {
            FramebufferId = gl.GenFramebuffer();
            gl.BindFramebuffer(GlConsts.GL_FRAMEBUFFER, FramebufferId);
            gl.FramebufferTexture2D(
                GlConsts.GL_FRAMEBUFFER,
                GlConsts.GL_COLOR_ATTACHMENT0,
                GlConsts.GL_TEXTURE_2D,
                (int)textureId,
                0);

            // A packed depth/stencil renderbuffer attached to both points is equivalent to
            // GL_DEPTH_STENCIL_ATTACHMENT, which Avalonia does not expose a constant for.
            depthStencilRenderbufferId = gl.GenRenderbuffer();
            gl.BindRenderbuffer(GlConsts.GL_RENDERBUFFER, depthStencilRenderbufferId);
            gl.RenderbufferStorage(
                GlConsts.GL_RENDERBUFFER,
                GlConsts.GL_DEPTH24_STENCIL8,
                Size.Width,
                Size.Height);
            gl.FramebufferRenderbuffer(
                GlConsts.GL_FRAMEBUFFER,
                GlConsts.GL_DEPTH_ATTACHMENT,
                GlConsts.GL_RENDERBUFFER,
                depthStencilRenderbufferId);
            gl.FramebufferRenderbuffer(
                GlConsts.GL_FRAMEBUFFER,
                GlConsts.GL_STENCIL_ATTACHMENT,
                GlConsts.GL_RENDERBUFFER,
                depthStencilRenderbufferId);

            var status = gl.CheckFramebufferStatus(GlConsts.GL_FRAMEBUFFER);
            if (status != GlConsts.GL_FRAMEBUFFER_COMPLETE)
            {
                DisposeGlResources(gl, FramebufferId, depthStencilRenderbufferId, textureId);
                throw new InvalidOperationException(
                    $"The Avalonia framebuffer is incomplete (OpenGL status 0x{status:X4}).");
            }
        }
    }

    /// <remarks>The parameter is ignored: there is only ever raylib's context.</remarks>
    public IGlPlatformSurfaceRenderTarget CreateGlRenderTarget(IGlContext glContext)
        => new RaylibGlRenderTarget(this, context);

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;

        var gl = context.GlInterface;

        using (RlglStateGuard.Enter(gl))
        {
            DisposeGlResources(gl, FramebufferId, depthStencilRenderbufferId, Texture.Id);
        }
    }

    private static void DisposeGlResources(GlInterface gl, int framebuffer, int renderbuffer, uint texture)
    {
        gl.BindFramebuffer(GlConsts.GL_FRAMEBUFFER, 0);
        gl.DeleteFramebuffer(framebuffer);
        gl.DeleteRenderbuffer(renderbuffer);
        Rlgl.UnloadTexture(texture);
    }
}
