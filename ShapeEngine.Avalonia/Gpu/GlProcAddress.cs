using System.Runtime.InteropServices;

namespace ShapeEngine.Avalonia.Gpu;

/// <summary>
/// Resolves OpenGL entry points from the context raylib has already made current on the calling thread.
/// </summary>
/// <remarks>
/// The only platform specific piece of the integration. Adding Linux (<c>glXGetProcAddressARB</c>) or
/// macOS (<c>dlsym</c>) support means extending this class and nothing else.
/// </remarks>
internal static class GlProcAddress
{
    private const string Opengl32 = "opengl32.dll";

    private static IntPtr openglModule;

    [DllImport(Opengl32, EntryPoint = "wglGetProcAddress", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern IntPtr WglGetProcAddress(string name);

    /// <summary>
    /// Looks up <paramref name="name"/>, returning <see cref="IntPtr.Zero"/> when it is not available.
    /// </summary>
    /// <remarks>
    /// A GL context must be current on the calling thread, so call this only after
    /// <c>Raylib.InitWindow</c> and only from the thread running the game loop.
    /// </remarks>
    public static IntPtr Get(string name)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                $"ShapeEngine.Avalonia currently supports Windows only. Extend {nameof(GlProcAddress)} to add another platform.");
        }

        var address = WglGetProcAddress(name);

        // wglGetProcAddress reports failure as 0, 1, 2, 3 or -1, and never resolves the OpenGL 1.1
        // functions exported by opengl32.dll itself. Both cases fall through to the module export.
        if (address != IntPtr.Zero
            && address != 1
            && address != 2
            && address != 3
            && address != -1)
        {
            return address;
        }

        if (openglModule == IntPtr.Zero) openglModule = NativeLibrary.Load(Opengl32);

        return NativeLibrary.TryGetExport(openglModule, name, out var fallback) ? fallback : IntPtr.Zero;
    }
}
