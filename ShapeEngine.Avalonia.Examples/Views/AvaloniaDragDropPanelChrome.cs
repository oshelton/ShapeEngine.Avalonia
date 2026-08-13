using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
using Avalonia.Media;

namespace AvaloniaExamples.Views;

/// <summary>
/// The card shell both drag-and-drop panels sit in - title, description, their own distinct middle
/// content, an activity log and a status line - so neither <see cref="AvaloniaDragDropSourcePanel"/> nor
/// <see cref="AvaloniaDragDropTargetPanel"/> has to repeat it.
/// </summary>
internal static class AvaloniaDragDropPanelChrome
{
    public static Border Build(string title, string description, Control content, ObservableCollection<string> log, out TextBlock statusText)
        => new Border()
            .Width(320)
            .Background(new SolidColorBrush(Color.FromArgb(220, 24, 24, 34)))
            .BorderBrush(new SolidColorBrush(Color.FromArgb(255, 90, 90, 130)))
            .BorderThickness(new Thickness(1))
            .CornerRadius(new CornerRadius(12))
            .Padding(new Thickness(18))
            // Part of the scaled content rather than the surface's anchor, so the gap keeps its
            // proportion to the card at any window size instead of shrinking away as the card grows.
            .Margin(new Thickness(0, 16, 0, 0))
            .VerticalAlignment(VerticalAlignment.Top)
            .Child(
                new StackPanel()
                    .Spacing(12)
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
                        content,
                        new ListBox()
                            .ItemsSource(log)
                            .Height(110),
                        // Deliberately not wrapping: the status text changes every frame, and this is the
                        // narrowest panel, so a line that wraps only sometimes would change the card's
                        // height and rescale the whole thing through the surface's Viewbox.
                        new TextBlock()
                            .Ref(out statusText)
                            .TextWrapping(TextWrapping.NoWrap)
                            .Foreground(Brushes.Gainsboro)));
}
