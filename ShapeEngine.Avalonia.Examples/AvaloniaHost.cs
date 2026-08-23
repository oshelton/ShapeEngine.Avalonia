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
    /// <summary>The regular weight of the interface font, for anything outside Avalonia that wants it.</summary>
    /// <remarks>
    /// <see cref="ExamplesFpsDisplay"/> loads this and hands the bytes to raylib, so the one piece of text
    /// the game draws for itself is set in the same face as the interface over it.
    /// </remarks>
    public const string InterRegular = "avares://Avalonia.Fonts.Inter/Assets/Inter-Regular.ttf";

    private static bool initialized;

    public static void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        AppBuilder.Configure<AvaloniaExamplesApp>()
            .UseShapeEngine()
            // Registers the font collection the themes already name: they set a family of "Inter, then the
            // platform default" on every control, and without this the first half never resolves.
            .WithInterFont()
            .SetupWithoutStarting();
    }
}
