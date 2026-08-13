using System.Numerics;
using ShapeEngine.Avalonia;

namespace AvaloniaExamples;

/// <summary>Fixed regions of the window: a nav bar strip across the top, and the content band below it.</summary>
public static class ExamplesLayout
{
    /// <summary>Fraction of the window height the nav bar occupies.</summary>
    public const float NavHeight = 0.09f;

    /// <summary>Top edge of the content band, directly below the nav bar.</summary>
    public const float ContentTop = NavHeight;

    /// <remarks>
    /// Short of the literal window edge on purpose: a maximized window's reported size doesn't always
    /// match its visible area (taskbar overlap), so content anchored flush to the bottom can end up partly
    /// hidden. The margin puts that discrepancy in empty space instead.
    /// </remarks>
    public const float BottomInset = 0.02f;

    /// <summary>Bottom edge of the content band.</summary>
    public const float ContentBottom = 1f - BottomInset;

    /// <summary>Height of the content band.</summary>
    public const float ContentHeight = ContentBottom - ContentTop;

    /// <summary>Keeps a panel off the window edge.</summary>
    public const float Inset = 0.03f;

    /// <summary>The nav bar's own anchor: the full window width, pinned to the top.</summary>
    public static AvaloniaSurfaceAnchor NavBar => new(new Vector2(1f, NavHeight), Vector2.Zero);

    /// <summary>
    /// An anchor for the given rectangle within the content band, in fractions of the window measured
    /// from the top left.
    /// </summary>
    /// <remarks>
    /// <see cref="AvaloniaSurfaceAnchor"/> pins by a fraction that doubles as the surface's own origin,
    /// which survives a resize but is hard to read back as a rectangle. An edge lands at
    /// <c>position * (1 - stretch)</c>, so the position is that edge over the space left around it.
    /// </remarks>
    public static AvaloniaSurfaceAnchor Content(float left, float width)
    {
        var x = width < 1f ? left / (1f - width) : 0f;
        var y = ContentTop / (1f - ContentHeight);

        return new AvaloniaSurfaceAnchor(new Vector2(width, ContentHeight), new Vector2(x, y));
    }

    /// <summary>A column filling the content band, centered horizontally in the window.</summary>
    public static AvaloniaSurfaceAnchor CenteredColumn(float width) => Content((1f - width) / 2f, width);
}
