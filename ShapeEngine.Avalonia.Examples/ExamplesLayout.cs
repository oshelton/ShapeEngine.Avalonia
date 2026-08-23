using System.Numerics;
using ShapeEngine.Avalonia;

namespace AvaloniaExamples;

/// <summary>Fixed regions of the window: a navigation sidebar down the left, and the content band
/// beside it.</summary>
public static class ExamplesLayout
{
    /// <summary>Fraction of the window width the navigation sidebar occupies.</summary>
    /// <remarks>
    /// The sidebar is laid out at its real size rather than through <see cref="AvaloniaSurface.ScaleContent"/>,
    /// so this fraction is what decides how many device independent pixels its items get to lay out in -
    /// and with <c>HighDPI</c> on, that is the fraction of the window in physical pixels divided by the
    /// display scale. At the default window size it works out to a conventional sidebar width; shrunk to
    /// the 1024x640 minimum on a 175% display the whole window is only 585 DIP across, and the longest
    /// item label ellipsizes rather than fitting. Widening this to cover that case would leave the sidebar
    /// taking a quarter of the window at every ordinary size, which is the worse trade.
    /// </remarks>
    public const float SidebarWidth = 0.22f;

    /// <summary>Gap between the sidebar and the content beside it, so no panel sits flush against it.</summary>
    public const float SideInset = 0.02f;

    /// <summary>Left edge of the content band.</summary>
    public const float ContentLeft = SidebarWidth + SideInset;

    /// <summary>Right edge of the content band.</summary>
    public const float ContentRight = 1f - SideInset;

    /// <summary>Width of the content band.</summary>
    public const float ContentWidth = ContentRight - ContentLeft;

    /// <remarks>
    /// Short of the literal window edge on purpose: a maximized window's reported size doesn't always
    /// match its visible area (taskbar overlap), so content anchored flush to the bottom can end up partly
    /// hidden. The margin puts that discrepancy in empty space instead.
    /// </remarks>
    public const float BottomInset = 0.02f;

    /// <summary>Top edge of the content band, and of the sidebar beside it.</summary>
    public const float ContentTop = 0.02f;

    /// <summary>Bottom edge of the content band, and of the sidebar beside it.</summary>
    public const float ContentBottom = 1f - BottomInset;

    /// <summary>Height of the content band.</summary>
    public const float ContentHeight = ContentBottom - ContentTop;

    /// <summary>Keeps a panel off the edge of the content band.</summary>
    public const float Inset = 0.03f;

    /// <summary>The sidebar's own anchor: a column down the left, as tall as the content band.</summary>
    public static AvaloniaSurfaceAnchor Sidebar =>
        new(new Vector2(SidebarWidth, ContentHeight), new Vector2(0f, ContentTop / (1f - ContentHeight)));

    /// <summary>
    /// An anchor for the given rectangle within the content band, in fractions of the band itself
    /// measured from its top left - so <c>(0, 1)</c> is the whole band whatever the sidebar takes.
    /// </summary>
    /// <remarks>
    /// <see cref="AvaloniaSurfaceAnchor"/> pins by a fraction that doubles as the surface's own origin,
    /// which survives a resize but is hard to read back as a rectangle. An edge lands at
    /// <c>position * (1 - stretch)</c>, so the position is that edge over the space left around it.
    /// </remarks>
    public static AvaloniaSurfaceAnchor Content(float left, float width)
    {
        var stretch = width * ContentWidth;
        var edge = ContentLeft + left * ContentWidth;

        var x = stretch < 1f ? edge / (1f - stretch) : 0f;
        var y = ContentTop / (1f - ContentHeight);

        return new AvaloniaSurfaceAnchor(new Vector2(stretch, ContentHeight), new Vector2(x, y));
    }

    /// <summary>A column of the given width, centered within the content band.</summary>
    public static AvaloniaSurfaceAnchor CenteredColumn(float width) => Content((1f - width) / 2f, width);
}
