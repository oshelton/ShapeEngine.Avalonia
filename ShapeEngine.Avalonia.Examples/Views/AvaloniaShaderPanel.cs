using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
using Avalonia.Media;
using AvSlider = Avalonia.Controls.Slider;

namespace AvaloniaExamples.Views;

/// <summary>
/// Avalonia controls driving the shader that post-processes the surface they are drawn on.
/// </summary>
/// <remarks>
/// The panel is deliberately busy - fine text, thin borders, a slider track - because that is where a
/// post-process effect shows up most clearly. Turning the shader off with the toggle is the check that it
/// is really running: everything snaps back to a clean render.
/// </remarks>
public sealed class AvaloniaShaderPanel : ViewBase
{
    private readonly string title;
    private readonly string description;

    private TextBlock statusText = null!;
    private AvSlider strengthSlider = null!;
    private ToggleSwitch enabledToggle = null!;

    public AvaloniaShaderPanel(string title, string description)
    {
        this.title = title;
        this.description = description;

        Initialize();
    }

    /// <summary>Whether the shader should run at all.</summary>
    public bool ShaderEnabled
    {
        get => enabledToggle.IsChecked == true;
        set => enabledToggle.IsChecked = value;
    }

    /// <summary>How strongly the effect is applied, 0 being an untouched render.</summary>
    public float Strength => (float)strengthSlider.Value;

    protected override object Build()
        => new Border()
            .Width(340)
            .Background(new SolidColorBrush(Color.FromArgb(220, 24, 24, 34)))
            .BorderBrush(new SolidColorBrush(Color.FromArgb(255, 90, 90, 130)))
            .BorderThickness(new Thickness(1))
            .CornerRadius(new CornerRadius(12))
            .Padding(new Thickness(18))
            .VerticalAlignment(VerticalAlignment.Top)
            .Child(
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
                        new ToggleSwitch()
                            .Ref(out enabledToggle)
                            .Content("Shader")
                            .OffContent("Off - clean render")
                            .OnContent("On - post-processing active")
                            .IsChecked(true),
                        new TextBlock()
                            .Text("Strength")
                            .FontSize(12)
                            .Foreground(Brushes.DarkGray),
                        new AvSlider()
                            .Ref(out strengthSlider)
                            .Minimum(0)
                            .Maximum(1)
                            .Value(0.7),
                        new TextBlock()
                            .Text("Fine detail like this line shows the effect most clearly")
                            .FontSize(12)
                            .TextWrapping(TextWrapping.Wrap)
                            .Foreground(Brushes.Gainsboro),
                        new Border()
                            .Height(2)
                            .Background(new SolidColorBrush(Color.FromArgb(255, 120, 220, 255))),
                        new Button()
                            .Content("Still fully interactive")
                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                            .HorizontalContentAlignment(HorizontalAlignment.Center),
                        new TextBox()
                            .PlaceholderText("type here - input is unaffected"),
                        // Not wrapping, so the per-frame status can't change the panel's height and
                        // rescale it through the surface's Viewbox.
                        new TextBlock()
                            .Ref(out statusText)
                            .TextWrapping(TextWrapping.NoWrap)
                            .Foreground(Brushes.Gainsboro)));

    /// <summary>Shows the surface's live state, updated by the scene each frame.</summary>
    public void SetStatus(string status) => statusText.Text = status;
}
