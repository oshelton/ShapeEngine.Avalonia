using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Surfaces;

namespace ShapeEngine.Avalonia.Gpu;

/// <summary>One Avalonia render pass into a <see cref="RaylibGlSurface"/>.</summary>
internal sealed class RaylibGlRenderingSession : IGlPlatformSurfaceRenderingSession
{
    private readonly RaylibGlSurface surface;
    private readonly RlglStateGuard stateGuard;

    public IGlContext Context { get; }

    public PixelSize Size => surface.Size;

    public double Scaling => surface.RenderScaling;

    /// <summary>
    /// Describes the framebuffer, not the desired output: the target uses OpenGL's bottom-left origin,
    /// and Avalonia compensates. raylib samples top-down, so leaving this false renders upside down.
    /// </summary>
    public bool IsYFlipped => true;

    public RaylibGlRenderingSession(RaylibGlContext context, RaylibGlSurface surface, RlglStateGuard stateGuard)
    {
        Context = context;
        this.surface = surface;
        this.stateGuard = stateGuard;
    }

    public void Dispose()
    {
        // Avalonia's queued work has to reach the texture before raylib samples it.
        Context.GlInterface.Flush();

        stateGuard.Dispose();
    }
}
