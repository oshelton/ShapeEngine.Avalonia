using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
using Avalonia.Media;
using ShapeEngine.Avalonia.Controls;
using ShapeEngine.Color;
using ShapeEngine.Core.Structs;
using ShapeEngine.Geometry.CircleDef;
using ShapeEngine.Geometry.TriangleDef;
using SeRect = ShapeEngine.Geometry.RectDef.Rect;
using SeSize = ShapeEngine.Core.Structs.Size;
using SeVec2 = System.Numerics.Vector2;

namespace AvaloniaExamples.Views;

/// <summary>
/// The rectangle a chip is dropped onto, drawn entirely by ShapeEngine, on its own surface separate from
/// <see cref="AvaloniaDragDropSourcePanel"/>.
/// </summary>
/// <remarks>
/// Avalonia doesn't care that the content inside the drop target is raylib drawing rather than Avalonia
/// content - hit-testing, drag tracking and the routed <c>DragDrop</c> events all go through the
/// <see cref="ShapeEngineDirectView"/> exactly as they would for any other control.
/// </remarks>
public sealed class AvaloniaDragDropTargetPanel : ViewBase
{
    private readonly ObservableCollection<string> log = [];

    private TextBlock statusText = null!;
    private ShapeEngineDirectView dropTarget = null!;

    private AvaloniaDragDropShape? droppedShape;
    private bool isDragOver;
    private int dropCount;

    public AvaloniaDragDropTargetPanel() => Initialize();

    protected override object Build()
        => AvaloniaDragDropPanelChrome.Build(
            "Drop target",
            "This rectangle is drawn by ShapeEngine, not Avalonia - only the hit testing and drag events are Avalonia's.",
            BuildDropTargetView(),
            log,
            out statusText);

    /// <summary>Shows the surface's live state, updated by the scene each frame.</summary>
    public void SetStatus(string status) => statusText.Text = status;

    private Control BuildDropTargetView()
    {
        var view = new ShapeEngineDirectView { DrawContent = DrawDropTarget }.Height(140);

        DragDrop.SetAllowDrop(view, true);
        DragDrop.AddDragEnterHandler(view, OnDragOver);
        DragDrop.AddDragOverHandler(view, OnDragOver);
        DragDrop.AddDragLeaveHandler(view, OnDragLeave);
        DragDrop.AddDropHandler(view, OnDrop);

        dropTarget = view;
        return view;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var accepted = e.DataTransfer.Contains(AvaloniaDragDropChip.Format);
        e.DragEffects = accepted ? DragDropEffects.Copy : DragDropEffects.None;

        if (isDragOver == accepted) return;

        isDragOver = accepted;
        dropTarget.InvalidateVisual();
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        isDragOver = false;
        dropTarget.InvalidateVisual();
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        isDragOver = false;

        if (e.DataTransfer.TryGetValue(AvaloniaDragDropChip.Format) is { } name && Enum.TryParse<AvaloniaDragDropShape>(name, out var shape))
        {
            droppedShape = shape;
            dropCount++;
            log.AppendLog($"Dropped -> {shape} (#{dropCount})");
        }

        dropTarget.InvalidateVisual();
    }

    /// <summary>
    /// The rectangle itself - empty placeholder, dropped fill and glyph, and the highlighted outline
    /// while something is being dragged over it, all raylib draw calls rather than Avalonia content.
    /// </summary>
    private void DrawDropTarget(SeRect bounds)
    {
        const float border = 3f;
        var inset = new SeRect(bounds.X + border, bounds.Y + border, bounds.Width - border * 2f, bounds.Height - border * 2f);

        var fill = droppedShape is { } shape ? AvaloniaDragDropChip.Info(shape).ShapeColor : new ColorRgba(60, 60, 78, 255);
        inset.Draw(fill);

        if (droppedShape is { } drawn) DrawGlyph(inset, drawn);

        var outline = isDragOver ? new ColorRgba(150, 255, 255, 255) : new ColorRgba(100, 100, 140, 255);
        inset.DrawLines(border, outline);
    }

    private static void DrawGlyph(SeRect bounds, AvaloniaDragDropShape shape)
    {
        var center = bounds.Center;
        var unit = Math.Min(bounds.Width, bounds.Height);
        var white = new ColorRgba(255, 255, 255, 220);

        switch (shape)
        {
            case AvaloniaDragDropShape.Circle:
                new Circle(center, unit * 0.28f).Draw(white, 0.9f);
                break;

            case AvaloniaDragDropShape.Square:
                new SeRect(center, new SeSize(unit * 0.5f), new AnchorPoint(0.5f)).Draw(white);
                break;

            case AvaloniaDragDropShape.Triangle:
                var radius = unit * 0.3f;
                const float top = -MathF.PI / 2f;
                SeVec2 Point(float angle) => center + new SeVec2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                new Triangle(Point(top), Point(top + MathF.Tau / 3f), Point(top + 2f * MathF.Tau / 3f)).Draw(white);
                break;
        }
    }
}
