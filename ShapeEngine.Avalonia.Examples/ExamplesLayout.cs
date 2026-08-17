using System.Numerics;
using ShapeEngine.Avalonia;

namespace AvaloniaExamples;

/// <summary>Fixed regions of the window: a nav bar strip across the top, and the content band below it.</summary>
public static class ExamplesLayout
{
    /// <summary>Fraction of the window height the nav bar occupies.</summary>
    /// <remarks>
    /// Sized for the smallest window rather than the nicest looking one. The nav bar is laid out at its
    /// real size, and its row of buttons has a floor of roughly 39 device independent pixels that no
    /// amount of restyling gets under - Fluent's own metrics for a RadioButton. At the 1024x640 minimum
    /// this fraction has to cover that floor <em>after</em> <c>HighDPI</c> has divided the strip by the
    /// display scale, which is what makes 0.09 - fine at 100% - clip the bar in half on a 175% display.
    /// <para>
    /// The strip draws nothing itself, so the headroom this leaves at larger window sizes costs only
    /// content band height, not a visible gap.
    /// </para>
    /// </remarks>
    public const float NavHeight = 0.14f;

    /// <summary>Gap between the nav bar and the content below it, so no panel sits flush against it.</summary>
    public const float TopInset = 0.02f;

    /// <summary>Top edge of the content band.</summary>
    public const float ContentTop = NavHeight + TopInset;

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
