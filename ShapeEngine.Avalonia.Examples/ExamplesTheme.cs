namespace AvaloniaExamples;

/// <summary>
/// The names the examples reach into ShadUI's theme by - its style classes, and the resource keys it
/// publishes its palette under.
/// </summary>
/// <remarks>
/// ShadUI leads with style classes rather than control themes, so most of this is plain strings handed to
/// <c>Classes</c>. Anything that builds a control out of them lives in <see cref="ExampleControls"/>.
/// </remarks>
public static class ExamplesTheme
{
    /// <summary>Small print - labels above a control, and the per-frame status lines.</summary>
    /// <remarks>
    /// ShadUI's text scale runs h1 (36pt ExtraBold) down through h4, then p, Large, Small and Caption,
    /// with Muted and Error as colour-only classes that combine with the rest. This is the only size the
    /// views name for themselves; the others are reached through <see cref="ExampleControls"/>.
    /// </remarks>
    public const string CaptionClass = "Caption";

    // Buttons. ShadUI's variants, in the vocabulary shadcn uses.

    /// <summary>The call to action - a near-white fill with dark text in the dark palette.</summary>
    public const string PrimaryButton = "Primary";

    /// <summary>A quieter filled button.</summary>
    public const string SecondaryButton = "Secondary";

    /// <summary>Outlined rather than filled.</summary>
    public const string OutlineButton = "Outline";

    /// <summary>A <see cref="Avalonia.Controls.Primitives.ToggleButton"/>'s plain variant, and what
    /// ShadUI's own examples give one.</summary>
    /// <remarks>Unfilled until checked. ShadUI offers a toggle only this and <c>Outline</c>, and one of
    /// them has to be set: the bare theme carries no padding.</remarks>
    public const string DefaultToggle = "Default";

    /// <summary>The tree-wide text colour, which ShadUI sets on its <c>Window</c> rather than on a
    /// control theme - see <see cref="AvaloniaExamplesApp"/>.</summary>
    public const string ForegroundColor = "ForegroundColor";

    /// <summary>Breathing room around a panel's edge, used as both its padding and its margin.</summary>
    public const double PanelSpacing = 4;
}
