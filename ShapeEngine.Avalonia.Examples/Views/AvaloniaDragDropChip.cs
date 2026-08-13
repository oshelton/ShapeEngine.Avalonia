using System.Collections.ObjectModel;
using Avalonia.Input;
using Avalonia.Media;
using ShapeEngine.Color;

namespace AvaloniaExamples.Views;

/// <summary>The shapes a drag-and-drop chip can carry.</summary>
internal enum AvaloniaDragDropShape { Circle, Square, Triangle }

/// <summary>
/// The single source of truth for what a drag-and-drop chip looks like and what data it carries - shared
/// between <see cref="AvaloniaDragDropSourcePanel"/>, where a chip starts a drag, and
/// <see cref="AvaloniaDragDropTargetPanel"/>, which reads the dropped shape back out.
/// </summary>
internal static class AvaloniaDragDropChip
{
    // DataFormat<T> requires a reference type, so the shape crosses as its enum name rather than the
    // enum value itself - parsed back on the other side.
    public static readonly DataFormat<string> Format =
        DataFormat.CreateInProcessFormat<string>("shape-engine-avalonia-example.chip-shape");

    // No Name here: it would only ever be shape.ToString() - callers that need a label use that directly.
    public static (Color AvaloniaColor, ColorRgba ShapeColor) Info(AvaloniaDragDropShape shape) => shape switch
    {
        AvaloniaDragDropShape.Circle => (Color.FromRgb(120, 200, 255), new ColorRgba(120, 200, 255, 255)),
        AvaloniaDragDropShape.Square => (Color.FromRgb(160, 255, 170), new ColorRgba(160, 255, 170, 255)),
        AvaloniaDragDropShape.Triangle => (Color.FromRgb(255, 190, 120), new ColorRgba(255, 190, 120, 255)),
        _ => throw new ArgumentOutOfRangeException(nameof(shape))
    };

    /// <summary>Shared by both panels' activity logs - newest entry first.</summary>
    public static void AppendLog(this ObservableCollection<string> log, string entry) => log.Insert(0, entry);
}
