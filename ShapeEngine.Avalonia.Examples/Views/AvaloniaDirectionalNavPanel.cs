using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
using Avalonia.Media;

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

    private TextBlock navText = null!;
    private TextBlock statusText = null!;

    private string focused = "none";
    private string lastDirection = "none";
    private int activations;

    public AvaloniaDirectionalNavPanel()
    {
        Initialize();

        // Focus has to start somewhere: the surface only forwards navigation keys once something inside
        // is focused, and with no pointer involved nothing else would ever establish it. Fires on every
        // show, because the scene swaps the content out when the view is hidden.
        //
        // Directional rather than the default: focus obtained without a navigation method gets :focus but
        // not :focus-visible, which is the one the theme draws the focus ring from - so the starting button
        // would hold focus while looking no different from the rest.
        AttachedToVisualTree += (_, _) =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => buttons[0].Focus(NavigationMethod.Directional));
    }

    protected override object Build()
        => new Border()
            .Width(560)
            .Background(new SolidColorBrush(Color.FromArgb(220, 24, 24, 34)))
            .BorderBrush(new SolidColorBrush(Color.FromArgb(255, 90, 90, 130)))
            .BorderThickness(new Thickness(1))
            .CornerRadius(new CornerRadius(12))
            .Padding(new Thickness(18))
            .VerticalAlignment(VerticalAlignment.Top)
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        new TextBlock()
                            .Text("Directional navigation")
                            .FontSize(22)
                            .FontWeight(FontWeight.SemiBold)
                            .Foreground(Brushes.White),
                        new TextBlock()
                            .Text("Arrow keys or a gamepad D-pad move focus by direction. Tab runs down each column instead - the two disagree on purpose.")
                            .TextWrapping(TextWrapping.Wrap)
                            .Foreground(Brushes.DarkGray),
                        BuildGrid(),
                        new TextBlock()
                            .Text("The right column wraps back to the left: an explicit XYFocus target, which overrides the automatic choice.")
                            .TextWrapping(TextWrapping.Wrap)
                            .FontSize(11)
                            .Foreground(Brushes.DarkGray),
                        new TextBlock()
                            .Ref(out navText)
                            .TextWrapping(TextWrapping.NoWrap)
                            .Foreground(Brushes.Gainsboro),
                        new TextBlock()
                            .Ref(out statusText)
                            .TextWrapping(TextWrapping.NoWrap)
                            .Foreground(Brushes.Gainsboro)));

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

        var button = new Button()
            .Content(name)
            .Margin(new Thickness(4))
            .Padding(new Thickness(10, 14))
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .HorizontalContentAlignment(HorizontalAlignment.Center);

        button.GotFocus += (_, _) =>
        {
            focused = name;
            UpdateNavText();
        };

        button.Click += (_, _) =>
        {
            activations++;
            UpdateNavText();
        };

        return button;
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
