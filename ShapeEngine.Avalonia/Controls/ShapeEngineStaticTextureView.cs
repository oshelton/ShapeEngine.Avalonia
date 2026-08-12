namespace ShapeEngine.Avalonia.Controls;

/// <summary>
/// Displays ShapeEngine drawing that does not animate, redrawing only when it has to.
/// </summary>
/// <remarks>
/// Draws when it first gets a size, again whenever it is resized, and otherwise only when
/// <see cref="ShapeEngineTextureView.InvalidateContent"/> is called - so call that after changing
/// anything the drawing depends on, or the old image stays on screen. Between redraws it costs nothing
/// per frame.
/// </remarks>
/// <example>
/// <code>
/// var view = new ShapeEngineStaticTextureView
/// {
///     Height = 120,
///     DrawContent = bounds => new Circle(bounds.Center, 40f).Draw(ColorRgba.White)
/// };
///
/// // later, when the data behind the drawing changes
/// view.InvalidateContent();
/// </code>
/// </example>
public sealed class ShapeEngineStaticTextureView : ShapeEngineTextureView
{
    /// <inheritdoc/>
    protected override bool ShouldRedraw(float deltaTime, bool contentIsDirty) => contentIsDirty;
}
