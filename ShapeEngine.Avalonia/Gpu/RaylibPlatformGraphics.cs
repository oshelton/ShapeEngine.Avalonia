using Avalonia.Platform;

namespace ShapeEngine.Avalonia.Gpu;

/// <summary>raylib OpenGL based <see cref="IPlatformGraphics"/> implementation.</summary>
/// <remarks>
/// There is exactly one OpenGL context - raylib's - so every Avalonia top level shares it, and
/// Avalonia's Skia backend builds its <c>GRContext</c> on top of it. The reference count releases the
/// wrapper with the last top level.
/// </remarks>
internal sealed class RaylibPlatformGraphics : IPlatformGraphics, IDisposable
{
    private RaylibGlContext? context;
    private int refCount;

    bool IPlatformGraphics.UsesSharedContext => true;

    public RaylibGlContext GetSharedContext()
    {
        ObjectDisposedException.ThrowIf(refCount == 0, this);

        return context ??= new RaylibGlContext();
    }

    // raylib's context can't be duplicated, so there is nothing to create on demand.
    IPlatformGraphicsContext IPlatformGraphics.CreateContext() => throw new NotSupportedException();

    IPlatformGraphicsContext IPlatformGraphics.GetSharedContext() => GetSharedContext();

    // Only ever touched from the game loop thread - same invariant as the rest of this integration -
    // so a plain counter is enough; no atomics needed.
    public void AddRef() => refCount++;

    public void Release()
    {
        if (--refCount == 0) Dispose();
    }

    public void Dispose()
    {
        context?.Dispose();
        context = null;
    }
}
