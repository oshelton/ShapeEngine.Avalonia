using Avalonia;
using Avalonia.Media;
using ShapeEngine.Avalonia;

namespace AvaloniaExamples;

/// <summary>One-time Avalonia setup for the example scene.</summary>
/// <remarks>
/// Avalonia can only be configured once per process, and needs the OpenGL context to exist first - hence
/// deferring setup until after ShapeEngine has created its window.
/// </remarks>
public static class AvaloniaHost
{
    /// <summary>JetBrains Mono - the font ShapeEngine's own examples use for their game text.</summary>
    /// <remarks>
    /// Embedded through the <c>AvaloniaResource</c> item in the csproj, so the <c>avares</c> URI resolves
    /// without a font file sitting next to the executable.
    /// </remarks>
    public const string ShapeEngineFont =
        "avares://ShapeEngine.Avalonia.Examples/Resources/Fonts/JetBrainsMono.ttf#JetBrains Mono";

    /// <summary>Inter, from the <c>Avalonia.Fonts.Inter</c> package, kept only as a fallback.</summary>
    /// <remarks>
    /// Referenced by URI rather than through <c>WithInterFont()</c> on purpose: that helper also rewrites
    /// the Fluent theme's <c>ContentControlThemeFontFamily</c> into a composite that puts Inter ahead of
    /// the default family, which would quietly win over everything set here.
    /// </remarks>
    private const string FallbackFont = "avares://Avalonia.Fonts.Inter/Assets#Inter";

    private static bool initialized;

    public static void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        AppBuilder.Configure<AvaloniaExamplesApp>()
            .UseShapeEngine()
            // DefaultFamilyName is what the `$Default` family every control starts out with resolves to, so
            // this alone re-fonts the whole tree. Inter only fills in codepoints JetBrains Mono lacks.
            .With(new FontManagerOptions
            {
                DefaultFamilyName = ShapeEngineFont,
                FontFallbacks = [new FontFallback { FontFamily = new FontFamily(FallbackFont) }]
            })
            .SetupWithoutStarting();
    }
}
