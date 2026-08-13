using Avalonia;
using Avalonia.Themes.Fluent;

namespace AvaloniaExamples;

/// <summary>
/// The Avalonia <see cref="Application"/> hosted inside the example. Built in code so this project needs
/// no XAML compilation; a real game would normally use an <c>App.axaml</c>.
/// </summary>
public sealed class AvaloniaExamplesApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
    }
}
