using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;

namespace AvaloniaExamples.Views;

/// <summary>
/// The shell both drag-and-drop panels sit in - title, description, their own distinct middle content, an
/// activity log and a status line - so neither <see cref="AvaloniaDragDropSourcePanel"/> nor
/// <see cref="AvaloniaDragDropTargetPanel"/> has to repeat it.
/// </summary>
internal static class AvaloniaDragDropPanelChrome
{
    public static Control Build(string title, string description, Control content, ObservableCollection<string> log, out TextBlock statusText)
        => ExampleControls.Panel()
            .Width(320)
            .VerticalAlignment(VerticalAlignment.Top)
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        ExampleControls.Title(title),
                        ExampleControls.Body(description),
                        content,
                        new ListBox()
                            .ItemsSource(log)
                            .Height(110),
                        ExampleControls.Status().Ref(out statusText)));
}
