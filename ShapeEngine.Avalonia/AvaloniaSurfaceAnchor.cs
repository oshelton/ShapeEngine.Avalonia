using System.Numerics;

namespace ShapeEngine.Avalonia;

/// <summary>
/// Where an <see cref="AvaloniaSurface"/> sits on screen, as a fraction of the window.
/// </summary>
/// <param name="Stretch">
/// Size as a fraction of the window. <c>(1, 1)</c> fills it; <c>(0.3f, 0.5f)</c> takes a third of the
/// width and half the height.
/// </param>
/// <param name="Position">
/// Where the surface is pinned, from <c>(0, 0)</c> at the top left to <c>(1, 1)</c> at the bottom
/// right. The same fraction is used as the surface's own origin, so <c>(0.5f, 0.5f)</c> centres it and
/// <c>(1, 1)</c> puts its bottom-right corner in the window's bottom-right corner.
/// </param>
/// <remarks>
/// Fractions rather than pixels, so a surface keeps its place and proportions as the window resizes.
/// </remarks>
public readonly record struct AvaloniaSurfaceAnchor(Vector2 Stretch, Vector2 Position)
{
    /// <summary>Covers the whole window.</summary>
    public static AvaloniaSurfaceAnchor FullScreen => new(Vector2.One, Vector2.Zero);

    /// <summary>A region of the given size, pinned at the given point.</summary>
    public AvaloniaSurfaceAnchor(float widthFraction, float heightFraction, float x, float y)
        : this(new Vector2(widthFraction, heightFraction), new Vector2(x, y)) { }
}
