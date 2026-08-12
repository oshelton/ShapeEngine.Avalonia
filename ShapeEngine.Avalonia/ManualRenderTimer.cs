using Avalonia.Rendering;

namespace ShapeEngine.Avalonia;

/// <summary>
/// An <see cref="IRenderTimer"/> that only ticks when the game loop tells it to.
/// </summary>
/// <remarks>
/// Avalonia normally drives rendering from its own timer on a render thread, but here the game loop owns
/// the frame rate and the OpenGL context.
/// </remarks>
internal sealed class ManualRenderTimer : IRenderTimer
{
    public Action<TimeSpan>? Tick { get; set; }

    bool IRenderTimer.RunsInBackground => false;

    public void TriggerTick(TimeSpan elapsed) => Tick?.Invoke(elapsed);
}
