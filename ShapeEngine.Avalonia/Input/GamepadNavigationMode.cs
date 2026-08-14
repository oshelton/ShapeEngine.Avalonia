namespace ShapeEngine.Avalonia.Input;

/// <summary>How a gamepad's D-pad is translated into the keys Avalonia navigates focus with.</summary>
public enum GamepadNavigationMode
{
    /// <summary>
    /// The D-pad moves through the focus order, forward and back. Suits a column or row of controls,
    /// where the tab order already runs the way the layout reads.
    /// </summary>
    Linear,

    /// <summary>
    /// The D-pad sends arrow keys, so focus moves by direction rather than by order. Needs the content to
    /// opt into <c>XYFocus</c>, which is what turns those arrows into spatial movement - without it,
    /// arrow keys move nothing and the D-pad goes dead.
    /// </summary>
    Directional
}
