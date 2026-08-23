using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Declarative;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;

namespace AvaloniaExamples;

/// <summary>
/// The handful of controls every example view builds itself out of, in the shapes ShadUI expects.
/// </summary>
/// <remarks>
/// The examples use stock Avalonia controls only - none of ShadUI's own controls appear anywhere - and
/// leave the theme to style them, so these are thin: a class or two from <see cref="ExamplesTheme"/> and
/// little else. <see cref="Panel"/> is the exception, and only because ShadUI paints its backdrop on a
/// <c>Window</c> that a surface does not have.
/// </remarks>
public static class ExampleControls
{
    // Text sizes, reached only through the helpers below.
    private const string HeadingClass = "h4";
    private const string BodyClass = "p";
    private const string MutedClass = "Muted";

    // What ShadUI's Window paints itself with. A surface has no window, so each panel wears it instead.
    private const string WindowBackgroundColor = "WindowBackgroundColor";
    private const string WindowBorderColor = "BorderColor60";
    private const string WindowCornerRadius = "LgCornerRadius";

    /// <summary>How opaque a panel is - the one place it departs from the opaque <c>Window</c> it
    /// borrows from, because the game has to stay visible behind it.</summary>
    private const byte PanelAlpha = 232;

    /// <summary>The backdrop every panel sits on: ShadUI's window background and border.</summary>
    /// <remarks>
    /// ShadUI's controls expect an opaque surface behind them - its <c>BackgroundColor</c> is
    /// <c>Transparent</c> in the dark palette because the <c>Window</c> is what paints the backdrop.
    /// Without this the panels would be text and controls floating on the game.
    /// </remarks>
    public static Border Panel()
        => new Border()
            .ThemeBrush(Border.BackgroundProperty, WindowBackgroundColor, PanelAlpha)
            .ThemeBrush(Border.BorderBrushProperty, WindowBorderColor)
            .BorderThickness(new Thickness(1))
            .ThemeResource(Border.CornerRadiusProperty, WindowCornerRadius)
            .Padding(new Thickness(ExamplesTheme.PanelSpacing))
            .Margin(new Thickness(ExamplesTheme.PanelSpacing));

    /// <summary>A panel's heading.</summary>
    public static TextBlock Title(string text)
        => new TextBlock().Text(text).Classes(HeadingClass);

    /// <summary>The paragraph under a heading, or any other supporting text.</summary>
    public static TextBlock Body(string text) => Dimmed(BodyClass).Text(text);

    /// <summary>A label above a control, or any other small print.</summary>
    public static TextBlock Label(string text) => Dimmed(ExamplesTheme.CaptionClass).Text(text);

    /// <summary>The live status readout each panel keeps in its bottom corner.</summary>
    /// <remarks>
    /// Deliberately not wrapping: the text is rewritten every frame, and on a <c>ScaleContent</c> surface
    /// a line that reflows as the values change resizes the panel and rescales the whole thing through
    /// its <c>Viewbox</c>.
    /// </remarks>
    public static TextBlock Status()
        => Dimmed(ExamplesTheme.CaptionClass).TextWrapping(TextWrapping.NoWrap);

    private static TextBlock Dimmed(string sizeClass)
        => new TextBlock().Classes(sizeClass).Classes(MutedClass).TextWrapping(TextWrapping.Wrap);

    /// <summary>Binds a brush property to one of the theme's <see cref="Color"/> resources.</summary>
    /// <param name="alpha">Replaces the theme colour's own alpha.</param>
    private static T ThemeBrush<T>(this T control, AvaloniaProperty property, string key, byte alpha = Byte.MaxValue)
        where T : StyledElement
    {
        control.Bind(property, control.GetResourceObservable(key, value => value is Color color
            ? new ImmutableSolidColorBrush(alpha == Byte.MaxValue ? color : new Color(alpha, color.R, color.G, color.B))
            : value));

        return control;
    }

    /// <summary>Binds any property to a theme resource of a matching type.</summary>
    private static T ThemeResource<T>(this T control, AvaloniaProperty property, string key) where T : StyledElement
    {
        control.Bind(property, control.GetResourceObservable(key));
        return control;
    }
}
