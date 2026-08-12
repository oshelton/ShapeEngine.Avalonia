using Avalonia.OpenGL;

namespace ShapeEngine.Avalonia.Gpu;

/// <summary>
/// Presents the OpenGL context raylib created to Avalonia as an <see cref="IGlContext"/>.
/// </summary>
/// <remarks>
/// There is no context management to do: raylib keeps its context current on the game loop thread for
/// the window's lifetime, and Avalonia is only ever driven from that same thread.
/// </remarks>
internal sealed class RaylibGlContext : IGlContext
{
    /// <summary>raylib targets OpenGL 3.3 core on desktop platforms.</summary>
    public GlVersion Version { get; } = new(GlProfileType.OpenGL, 3, 3);

    public GlInterface GlInterface { get; }

    /// <summary>No multisampling: the surface is rendered at native resolution and composited 1:1.</summary>
    public int SampleCount => 1;

    /// <summary>Matches the packed depth24/stencil8 renderbuffer <see cref="RaylibGlSurface"/> attaches.</summary>
    public int StencilSize => 8;

    public bool CanCreateSharedContext => false;

    public bool IsLost => false;

    public IDisposable MakeCurrent() => NoOpDisposable.Instance;

    public IDisposable EnsureCurrent() => NoOpDisposable.Instance;

    public bool IsSharedWith(IGlContext context) => ReferenceEquals(this, context);

    public IGlContext CreateSharedContext(IEnumerable<GlVersion>? preferredVersions = null)
        => throw new NotSupportedException("raylib's OpenGL context cannot be shared.");

    public object? TryGetFeature(Type featureType) => null;

    public RaylibGlContext()
    {
        GlInterface = new GlInterface(Version, GlProcAddress.Get);
    }

    /// <summary>Does nothing: raylib owns the context and destroys it when the window closes.</summary>
    public void Dispose() { }

    /// <summary>Stands in for a context-switch handle, since there is no context to switch.</summary>
    private sealed class NoOpDisposable : IDisposable
    {
        public static readonly NoOpDisposable Instance = new();
        public void Dispose() { }
    }
}
