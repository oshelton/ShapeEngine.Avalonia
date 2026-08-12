using Avalonia;
using Avalonia.Input.TextInput;

namespace ShapeEngine.Avalonia.Input;

/// <summary>
/// Tracks whether the focused Avalonia control is currently asking for text input.
/// </summary>
/// <remarks>
/// There is no IME to drive. This exists because Avalonia calls <see cref="SetClient"/> with a non-null
/// client exactly when a control wants typed characters, which is a far better signal for "the UI needs
/// the keyboard" than focus - a focused button should not stop the game from seeing WASD.
/// </remarks>
internal sealed class ShapeEngineTextInputMethod : ITextInputMethodImpl
{
    /// <summary>Whether a control is currently accepting text input.</summary>
    public bool IsActive { get; private set; }

    public void SetClient(TextInputMethodClient? client) => IsActive = client is not null;

    public void SetCursorRect(Rect rect) { }

    public void SetOptions(TextInputOptions options) { }

    public void Reset() { }
}
