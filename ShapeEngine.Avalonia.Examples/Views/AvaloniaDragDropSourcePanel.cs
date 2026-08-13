using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
using Avalonia.Media;

namespace AvaloniaExamples.Views;

/// <summary>
/// The chips a drag starts from - a plain Avalonia panel with nothing ShapeEngine-specific about it.
/// </summary>
/// <remarks>
/// Lives on its own surface, separate from <see cref="AvaloniaDragDropTargetPanel"/> - dragging from here
/// crosses from one <c>AvaloniaSurface</c>'s top level into a completely different one's, which is exactly
/// what <c>ShapeEngineDragSource</c> exists to track.
/// </remarks>
public sealed class AvaloniaDragDropSourcePanel : ViewBase
{
    private readonly ObservableCollection<string> log = [];

    private TextBlock statusText = null!;

    public AvaloniaDragDropSourcePanel() => Initialize();

    protected override object Build()
        => AvaloniaDragDropPanelChrome.Build(
            "Drag source",
            "Drag a chip onto the rectangle in the other panel. These chips are plain Avalonia controls.",
            BuildChipRow(),
            log,
            out statusText);

    /// <summary>Shows the surface's live state, updated by the scene each frame.</summary>
    public void SetStatus(string status) => statusText.Text = status;

    private Control BuildChipRow()
    {
        var chips = Enum.GetValues<AvaloniaDragDropShape>().Select(BuildChip).ToArray();
        return new StackPanel().Orientation(Orientation.Horizontal).Spacing(10).Children(chips);
    }

    /// <summary>A small coloured, labeled control that starts a drag carrying its own shape as data.</summary>
    private Border BuildChip(AvaloniaDragDropShape shape)
    {
        var (avaloniaColor, _) = AvaloniaDragDropChip.Info(shape);

        var chip = new Border()
            .Width(90)
            .Height(60)
            .Background(new SolidColorBrush(avaloniaColor))
            .CornerRadius(new CornerRadius(8))
            .Child(
                new TextBlock()
                    .Text(shape.ToString())
                    .FontSize(13)
                    .FontWeight(FontWeight.SemiBold)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Foreground(Brushes.Black));

        chip.PointerPressed += (_, e) => StartDrag(e, shape);

        return chip;
    }

    /// <remarks>
    /// Fire-and-forget from the caller's perspective: <c>DoDragDropAsync</c> only resolves once the
    /// pointer is released, which a synchronous <c>PointerPressed</c> handler can't wait around for.
    /// </remarks>
    private async void StartDrag(PointerPressedEventArgs e, AvaloniaDragDropShape shape)
    {
        var name = shape.ToString();

        // Carries the shape itself for the target to act on, and a plain text fallback so anything
        // generic - like the drag indicator following the cursor - has something readable too.
        var item = new DataTransferItem();
        item.Set(AvaloniaDragDropChip.Format, name);
        item.Set(DataFormat.Text, name);

        var transfer = new DataTransfer();
        transfer.Add(item);

        log.AppendLog($"Dragging {name}...");

        var result = await DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Copy);

        log.AppendLog(result == DragDropEffects.Copy ? $"{name} landed on the target" : $"{name} wasn't dropped on the target");
    }
}
