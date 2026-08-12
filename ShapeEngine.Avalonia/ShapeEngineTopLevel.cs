using Avalonia.Controls.Embedding;
using Avalonia.Input;

namespace ShapeEngine.Avalonia;

/// <summary>
/// The Avalonia root that hosts the content drawn into a ShapeEngine game.
/// </summary>
/// <remarks>
/// Created for you by <see cref="AvaloniaSurface"/>; set <see cref="AvaloniaSurface.Content"/> rather
/// than constructing this directly.
/// </remarks>
public sealed class ShapeEngineTopLevel : EmbeddableControlRoot
{
    internal ShapeEngineTopLevelImpl Impl { get; }

    static ShapeEngineTopLevel()
    {
        // TopLevel defaults to Cycle, which traps tab focus inside the UI forever. Continue lets focus
        // run off the end so the game can take it back.
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<ShapeEngineTopLevel>(KeyboardNavigationMode.Continue);
    }

    internal ShapeEngineTopLevel(ShapeEngineTopLevelImpl impl) : base(impl) => Impl = impl;
}
