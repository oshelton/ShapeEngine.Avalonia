using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
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
        => ExampleControls.Panel()
            .Width(340)
            .VerticalAlignment(VerticalAlignment.Top)
            .Child(
                new StackPanel()
                    .Spacing(10)
                    .Children(
                        ExampleControls.Title(title),
                        ExampleControls.Body(description),
                        // Single words, as ShadUI's own preview uses: the label sits beside the switch
                        // rather than under it, and a 1.1 render transform paints the control wider than
                        // it measures, so a phrase overhangs the panel. The status line carries the detail.
                        new ToggleSwitch()
                            .Ref(out enabledToggle)
                            .Content("Shader")
                            .OffContent("Off")
                            .OnContent("On")
                            .IsChecked(true),
                        ExampleControls.Label("Strength"),
                        new AvSlider()
                            .Ref(out strengthSlider)
                            .Minimum(0)
                            .Maximum(1)
                            .Value(0.7),
                        ExampleControls.Label("Fine detail like this line shows the effect most clearly"),
                        new Button()
                            .Classes(ExamplesTheme.PrimaryButton)
                            .Content("Still fully interactive")
                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                            .HorizontalContentAlignment(HorizontalAlignment.Center),
                        new TextBox()
                            .PlaceholderText("type here - input is unaffected"),
                        ExampleControls.Status().Ref(out statusText)));

    /// <summary>Shows the surface's live state, updated by the scene each frame.</summary>
    public void SetStatus(string status) => statusText.Text = status;
}
