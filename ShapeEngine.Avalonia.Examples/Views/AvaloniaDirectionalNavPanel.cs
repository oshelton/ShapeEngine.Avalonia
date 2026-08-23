using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
using Avalonia.VisualTree;

namespace AvaloniaExamples.Views;

/// <summary>
/// A grid of buttons navigated by direction - arrow keys or a gamepad D-pad - rather than by tab order.
/// </summary>
/// <remarks>
/// The buttons are deliberately added to the grid column by column, so the tab order runs <em>down</em>
/// each column while the layout reads across. Left and Right therefore cannot be explained by tab order
/// at all: only <c>XYFocus</c> moving focus geometrically produces them.
/// <para>
/// <c>XYFocus</c>'s navigation strategies are left at their default. Auto, Projection and
/// RectilinearDistance were tried and pick the same candidate for every move in a grid this regular -
/// only NavigationDirectionDistance differs, and it differs by ignoring alignment (Right from the middle
/// lands a row up), which is wrong here rather than interesting. A layout with staggered or unevenly
/// sized controls is where choosing between them would start to matter.
/// </para>
/// </remarks>
public sealed class AvaloniaDirectionalNavPanel : ViewBase
{
    private const int Columns = 4;
    private const int Rows = 3;

    private readonly Button[] buttons = new Button[Columns * Rows];

    private Button? focusedButton;

    private TextBlock navText = null!;
    private TextBlock statusText = null!;

    private string focused = "none";
    private string lastDirection = "none";
    private int activations;

    public AvaloniaDirectionalNavPanel()
    {
        Initialize();
    }

    /// <summary>Focuses the first button, so the view starts navigable with no pointer involved.</summary>
    /// <remarks>
    /// The surface only forwards navigation keys once something inside is focused, and nothing else here
    /// would establish it. The scene calls this on every show: the content stays attached for the scene's
    /// lifetime, so <c>AttachedToVisualTree</c> fires once and would leave later visits unfocused.
    /// <para>
    /// Directional rather than the default, so the starting focus is the same kind the arrow keys go on
    /// to produce - it sets :focus-visible where an unspecified method would only set :focus.
    /// </para>
    /// </remarks>
    public void FocusDefault() => buttons[0].Focus(NavigationMethod.Directional);

    /// <summary>The focused button's shape in the surface's client space, or null when focus is elsewhere.
    /// The scene maps it to the screen and glows around it - see <see cref="ExamplesFocusRing"/>.</summary>
    /// <remarks>
    /// Both corners are translated rather than the position alone, because the surface scales this panel
    /// to fit: the bounds a button reports are the ones it was laid out at, not the ones it is drawn at.
    /// The radius comes along so the glow can follow the button's corners rather than round at some
    /// radius of its own.
    /// </remarks>
    public (Rect Bounds, double CornerRadius)? FocusedButton
    {
        get
        {
            if (focusedButton is not { IsFocused: true } button) return null;
            if (TopLevel.GetTopLevel(button) is not { } root) return null;

            var size = button.Bounds.Size;

            if (button.TranslatePoint(default, root) is not { } topLeft) return null;
            if (button.TranslatePoint(new Point(size.Width, size.Height), root) is not { } bottomRight) return null;

            return (new Rect(topLeft, bottomRight), CornerRadiusOf(button));
        }
    }

    protected override object Build()
        => ExampleControls.Panel()
            .Width(560)
            .VerticalAlignment(VerticalAlignment.Top)
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        ExampleControls.Title("Directional navigation"),
                        ExampleControls.Body("Arrow keys or a gamepad D-pad move focus by direction. Tab runs down each column instead - the two disagree on purpose."),
                        BuildGrid(),
                        ExampleControls.Label("The right column wraps back to the left: an explicit XYFocus target, which overrides the automatic choice."),
                        ExampleControls.Status().Ref(out navText),
                        ExampleControls.Status().Ref(out statusText)));

    /// <summary>Shows the surface's live state, updated by the scene each frame.</summary>
    public void SetStatus(string status) => statusText.Text = status;

    private Control BuildGrid()
    {
        var grid = new Grid();

        // On the container, not the buttons: navigation looks for a search root among the focused
        // element's ancestors, so modes set only on the button that has focus find no root and nothing
        // moves at all. Verified against Avalonia 12.1.1 - buttons alone genuinely does nothing.
        XYFocus.SetNavigationModes(grid, XYFocusNavigationModes.Enabled);

        for (var column = 0; column < Columns; column++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var row = 0; row < Rows; row++) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        // Column-major insertion is what makes the point: tab order follows this loop, the layout does not.
        for (var column = 0; column < Columns; column++)
        {
            for (var row = 0; row < Rows; row++)
            {
                var button = BuildButton(row, column);

                Grid.SetColumn(button, column);
                Grid.SetRow(button, row);

                grid.Children.Add(button);
                buttons[row * Columns + column] = button;
            }
        }

        // Tunnelling, so the direction is recorded before XYFocus handles the key and stops it bubbling.
        grid.AddHandler(KeyDownEvent, OnGridKeyDown, RoutingStrategies.Tunnel);

        ApplyWrapAround();

        return grid;
    }

    private Button BuildButton(int row, int column)
    {
        var name = $"R{row + 1}C{column + 1}";

        // Classed rather than bare: an unclassed button is sized by its content, so a grid of them comes
        // out ragged. Every ShadUI button class fixes the height.
        var button = new Button()
            .Classes(ExamplesTheme.OutlineButton)
            .Margin(new Thickness(ExamplesTheme.PanelSpacing))
            .Content(name)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .HorizontalContentAlignment(HorizontalAlignment.Center);

        // The theme's ring is dropped in favour of the animated one the scene draws around whichever
        // button this records - two rings around one button is one too many. ShadUI sets the adorner
        // through a style, which a local value like this overrides.
        button.FocusAdorner = null;

        button.GotFocus += (_, _) =>
        {
            focusedButton = button;
            focused = name;

            UpdateNavText();
        };

        button.LostFocus += (_, _) =>
        {
            if (ReferenceEquals(focusedButton, button)) focusedButton = null;
        };

        button.Click += (_, _) =>
        {
            activations++;
            UpdateNavText();
        };

        return button;
    }

    /// <summary>The radius the button's corners are actually drawn with.</summary>
    /// <remarks>
    /// ShadUI can round the border inside the template rather than the button itself, in which case the
    /// button reports no radius at all - so the template's border is where the number comes from when the
    /// control has none of its own.
    /// </remarks>
    private static double CornerRadiusOf(Button button)
    {
        if (button.CornerRadius.TopLeft > 0) return button.CornerRadius.TopLeft;

        return button.GetVisualDescendants().OfType<Border>().FirstOrDefault()?.CornerRadius.TopLeft ?? 0;
    }

    /// <summary>Wraps each row horizontally, by naming the target instead of leaving it to the strategy.</summary>
    private void ApplyWrapAround()
    {
        for (var row = 0; row < Rows; row++)
        {
            var first = buttons[row * Columns];
            var last = buttons[row * Columns + Columns - 1];

            XYFocus.SetRight(last, first);
            XYFocus.SetLeft(first, last);
        }
    }

    private void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        lastDirection = e.Key switch
        {
            Key.Left => "Left",
            Key.Right => "Right",
            Key.Up => "Up",
            Key.Down => "Down",
            _ => lastDirection
        };

        UpdateNavText();
    }

    private void UpdateNavText()
        => navText.Text = $"Focus: {focused}   Last: {lastDirection}   Activated: {activations}";
}
