using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
using Avalonia.Media;

namespace AvaloniaExamples.Views;

/// <summary>
/// A panel of real Avalonia controls, used as the centre content of the "Full Window" view.
/// </summary>
/// <remarks>
/// Every control here covers a part of the integration that is easy to get wrong: a translucent
/// background (premultiplied alpha), rounded corners (Skia's stencil buffer), a <see cref="TextBox"/>
/// (text input, focus arbitration, I-beam cursor), a <see cref="ComboBox"/> (overlay popups) and an
/// indeterminate <see cref="ProgressBar"/> (the animation clock).
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
        => new Border()
            .Background(new SolidColorBrush(Color.FromArgb(220, 24, 24, 34)))
            .BorderBrush(new SolidColorBrush(Color.FromArgb(255, 90, 90, 130)))
            .BorderThickness(new Thickness(1))
            .CornerRadius(new CornerRadius(12))
            .Padding(new Thickness(18))
            .Child(
                new ScrollViewer()
                    .Content(
                        new StackPanel()
                            .Spacing(10)
                            .Children(
                                new TextBlock()
                                    .Text(title)
                                    .FontSize(22)
                                    .FontWeight(FontWeight.SemiBold)
                                    .TextWrapping(TextWrapping.Wrap)
                                    .Foreground(Brushes.White),
                                new TextBlock()
                                    .Text(description)
                                    .TextWrapping(TextWrapping.Wrap)
                                    .Foreground(Brushes.DarkGray),
                                new Button()
                                    .Content("Click me")
                                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                                    .HorizontalContentAlignment(HorizontalAlignment.Center)
                                    .OnClick(_ => RegisterClick()),
                                new TextBlock()
                                    .Ref(out clickCountText)
                                    .Text("Not clicked yet")
                                    .Foreground(Brushes.Gainsboro),
                                new TextBox()
                                    .PlaceholderText("Type here - the game stops seeing the keyboard"),
                                new ComboBox()
                                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                                    .PlaceholderText("Pick a shape (overlay popup)")
                                    .ItemsSource(new[] { "Circle", "Rect", "Triangle", "Polygon", "Polyline", "Segment" }),
                                new ProgressBar()
                                    .IsIndeterminate(true),
                                new TextBlock()
                                    .Ref(out statusText)
                                    .TextWrapping(TextWrapping.Wrap)
                                    .Foreground(Brushes.Gainsboro))));

    /// <summary>Shows the surface's live state, updated by the scene each frame.</summary>
    public void SetStatus(string status) => statusText.Text = status;

    private void RegisterClick()
    {
        clickCount++;
        clickCountText.Text = $"Clicked {clickCount} time{(clickCount == 1 ? String.Empty : "s")}";
    }
}
