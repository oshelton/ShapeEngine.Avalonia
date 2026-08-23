using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;

namespace AvaloniaExamples.Views;

/// <summary>
/// A panel of real Avalonia controls, used as the centre content of the "Full Window" view.
/// </summary>
/// <remarks>
/// Every control here covers a part of the integration that is easy to get wrong: rounded corners
/// (Skia's stencil buffer), a <see cref="TextBox"/> (text input, focus arbitration, I-beam cursor), a
/// <see cref="ComboBox"/> (overlay popups) and an indeterminate <see cref="ProgressBar"/> (the animation
/// clock). All of it plain Avalonia, wearing ShadUI's look purely because the theme is loaded - the panel
/// sets no background, border or padding of its own.
/// <para>
/// The <see cref="ScrollViewer"/> matters: the DockPanel centre this sits in can end up shorter than the
/// content wants, and a bare <see cref="StackPanel"/> neither scrolls nor clips - it just overflows.
/// </para>
/// </remarks>
public sealed class AvaloniaDemoPanel : ViewBase
{
    private readonly string title;
    private readonly string description;

    private TextBlock clickCountText = null!;
    private TextBlock statusText = null!;
    private int clickCount;

    public AvaloniaDemoPanel(string title, string description)
    {
        this.title = title;
        this.description = description;

        Initialize();
    }

    protected override object Build()
        => ExampleControls.Panel()
            .Child(
                new ScrollViewer()
                    .Content(
                        new StackPanel()
                            .Spacing(10)
                            .Children(
                                ExampleControls.Title(title),
                                ExampleControls.Body(description),
                                new Button()
                                    .Classes(ExamplesTheme.PrimaryButton)
                                    .Content("Click me")
                                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                                    .HorizontalContentAlignment(HorizontalAlignment.Center)
                                    .OnClick(_ => RegisterClick()),
                                new TextBlock()
                                    .Ref(out clickCountText)
                                    .Text("Not clicked yet"),
                                new TextBox()
                                    .PlaceholderText("Type here - the game stops seeing the keyboard"),
                                new ComboBox()
                                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                                    .PlaceholderText("Pick a shape (overlay popup)")
                                    .ItemsSource(new[] { "Circle", "Rect", "Triangle", "Polygon", "Polyline", "Segment" }),
                                new ProgressBar()
                                    .IsIndeterminate(true),
                                ExampleControls.Status().Ref(out statusText))));

    /// <summary>Shows the surface's live state, updated by the scene each frame.</summary>
    public void SetStatus(string status) => statusText.Text = status;

    private void RegisterClick()
    {
        clickCount++;
        clickCountText.Text = $"Clicked {clickCount} time{(clickCount == 1 ? String.Empty : "s")}";
    }
}
