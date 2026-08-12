namespace ShapeEngine.Avalonia.Controls;

/// <summary>
/// Displays ShapeEngine drawing that animates, redrawing continuously.
/// </summary>
/// <remarks>
/// Every redraw reads the texture back from the GPU, at a cost that scales with the control's area. Keep
/// the control small, or raise <see cref="RefreshInterval"/>. If the content does not actually animate,
/// <see cref="ShapeEngineStaticTextureView"/> costs nothing between redraws.
/// </remarks>
/// <example>
/// <code>
/// new ShapeEngineAnimatedTextureView
/// {
///     Height = 190,
///     DrawContent = bounds => new Circle(bounds.Center, radius).Draw(ColorRgba.White)
/// }
/// </code>
/// </example>
public sealed class ShapeEngineAnimatedTextureView : ShapeEngineTextureView
{
    private double refreshTimer;

    /// <summary>
    /// Minimum seconds between redraws. Zero redraws every frame; raise it to trade freshness for the
    /// cost of the read back.
    /// </summary>
    public double RefreshInterval { get; set; }

    /// <inheritdoc/>
    protected override bool ShouldRedraw(float deltaTime, bool contentIsDirty)
    {
        // A resize or an explicit invalidation still redraws immediately, so a long refresh interval
        // never leaves a stale image at the wrong size.
        if (contentIsDirty)
        {
            refreshTimer = RefreshInterval;
            return true;
        }

        if (RefreshInterval <= 0.0) return true;

        refreshTimer -= deltaTime;
        if (refreshTimer > 0.0) return false;

        refreshTimer = RefreshInterval;
        return true;
    }
}
