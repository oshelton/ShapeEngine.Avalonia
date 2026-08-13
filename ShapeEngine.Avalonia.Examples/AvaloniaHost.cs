using Avalonia;
using ShapeEngine.Avalonia;

namespace AvaloniaExamples;

/// <summary>One-time Avalonia setup for the example scene.</summary>
/// <remarks>
/// Avalonia can only be configured once per process, and needs the OpenGL context to exist first - hence
/// deferring setup until after ShapeEngine has created its window.
/// </remarks>
public static class AvaloniaHost
{
    private static bool initialized;

    public static void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        AppBuilder.Configure<AvaloniaExamplesApp>()
            .UseShapeEngine()
            .WithInterFont()
            .SetupWithoutStarting();
    }
}
