using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Embedding;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Styling;
using ShadUI;

namespace AvaloniaExamples;

/// <summary>
/// The Avalonia <see cref="Application"/> hosted inside the example. Built in code so this project needs
/// no XAML compilation; a real game would normally use an <c>App.axaml</c>.
/// </summary>
/// <remarks>
/// <see cref="ShadTheme"/> is the whole of the styling: a plain <c>Styles</c> collection carrying
/// ShadUI's resources, its Light and Dark palettes, and the control styles - which begin with a
/// <c>SimpleTheme</c> of their own. Adding it here does what <c>&lt;shadui:ShadTheme /&gt;</c> does in a
/// XAML app. The style classes the views ask for are collected in <see cref="ExamplesTheme"/>.
/// </remarks>
public sealed class AvaloniaExamplesApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new ShadTheme());

        // The palettes are plain theme dictionaries with nothing precomputed off the variant, so setting
        // it on the application is enough.
        RequestedThemeVariant = ThemeVariant.Dark;

        Styles.Add(RootTextColour());
    }

    /// <summary>Puts the text colour ShadUI sets on its <c>Window</c> onto the surfaces' own root.</summary>
    /// <remarks>
    /// ShadUI establishes it with a <c>TextElement.Foreground</c> setter on the <c>Window</c> rather than
    /// on a base control theme - its <c>TextBlock</c> theme sets no foreground at all. A surface's root is
    /// an <see cref="EmbeddableControlRoot"/>, so without this the text falls back to the SimpleTheme
    /// underneath. Only the foreground carries over: the rest of that theme is window chrome, or the opaque
    /// backdrop a surface must not have if the game is to show through.
    /// </remarks>
    private static Style RootTextColour()
        => new(x => x.Is<EmbeddableControlRoot>())
        {
            Setters =
            {
                new Setter(TextElement.ForegroundProperty, new DynamicResourceExtension(ExamplesTheme.ForegroundColor))
            }
        };
}
