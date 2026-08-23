using System.Numerics;
using Raylib_cs;
using ShapeEngine.Color;
using ShapeEngine.Core.GameDef;
using ShapeEngine.Core.Structs;
using ShapeEngine.Geometry;
using ShapeEngine.Geometry.CircleDef;
using ShapeEngine.Geometry.SegmentDef;
using ShapeEngine.Input;

namespace AvaloniaExamples;

/// <summary>The game shell for the Avalonia examples - a single scene, no menu.</summary>
public sealed class AvaloniaExamplesGame(
    GameSettings gameSettings,
    WindowSettings windowSettings,
    FramerateSettings framerateSettings,
    InputSettings inputSettings)
    : Game(gameSettings, windowSettings, framerateSettings, inputSettings)
{
    private static readonly ColorRgba CursorColor = new(255, 190, 120, 255);

    private readonly ExamplesFpsDisplay fpsDisplay = new();

    /// <remarks>
    /// The earliest the base <see cref="Game"/> calls back into a subclass, and Avalonia setup - which the
    /// scene's activation triggers - needs the raylib window, and so the OpenGL context, to exist first.
    /// </remarks>
    protected override void BeginRun()
    {
        // Before the readout, which takes its font from an Avalonia asset and so needs the asset loader.
        // The scene calls this too; it only does the work once.
        AvaloniaHost.EnsureInitialized();

        fpsDisplay.Load();
        GoToScene(new AvaloniaExamplesScene());
    }

    /// <remarks>The mirror of <see cref="BeginRun"/>: the last callback before the window, and the OpenGL
    /// context the font texture lives in, goes away.</remarks>
    protected override void EndRun() => fpsDisplay.Unload();

    /// <summary>Handles the window-level shortcuts, which belong to the shell rather than to any one
    /// example - so they keep working whichever view the sidebar has showing.</summary>
    protected override void Update(GameTime time, ScreenInfo game, ScreenInfo gameUi, ScreenInfo ui)
    {
        if (IsFullscreenTogglePressed()) ToggleWindowMode();
    }

    /// <remarks>The UI pass runs after every screen texture has composited, so the readout sits over the
    /// Avalonia surfaces rather than under them.</remarks>
    protected override void DrawUI(ScreenInfo uiInfo) => fpsDisplay.Draw(uiInfo);

    /// <summary>Swaps between a normal window and a borderless fullscreen one.</summary>
    /// <remarks>
    /// Spelled out as two calls rather than <c>Window.ToggleBorderlessFullscreen</c> so the windowed half
    /// of the toggle is always the normal state: <see cref="GameWindow.RestoreWindow"/> also drops a
    /// maximized window back to its normal size, which a plain toggle would leave maximized.
    /// <para>
    /// Borderless is unaffected by the <c>FullscreenAutoRestoring</c> the examples set - that only governs
    /// exclusive fullscreen - so the window stays fullscreen when focus moves elsewhere.
    /// </para>
    /// </remarks>
    private void ToggleWindowMode()
    {
        if (Window.IsWindowBorderlessFullscreen()) Window.RestoreWindow();
        else Window.ActivateBorderlessFullscreen();
    }

    /// <remarks>
    /// Polled straight from raylib for the reason the scene polls Escape that way: a surface locks the
    /// keyboard device while a control is mid-edit (see <c>AvaloniaSurface.CaptureGameInput</c>), which
    /// zeroes out anything read through ShapeEngine's input system - the shortcut has to work whichever
    /// surface has focus, including with the caret sitting in a text box.
    /// </remarks>
    private static bool IsFullscreenTogglePressed()
        => (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter))
           && (Raylib.IsKeyDown(KeyboardKey.LeftAlt) || Raylib.IsKeyDown(KeyboardKey.RightAlt));

    /// <remarks>
    /// Kept hidden even over Avalonia content, rather than handed back to whichever surface has the
    /// pointer, so the drawn cursor reads the same over the game and the UI.
    /// </remarks>
    protected override void UpdateCursor(float dt, ScreenInfo gameInfo, ScreenInfo gameUiInfo, ScreenInfo uiInfo)
        => Window.MouseVisible = false;

    protected override void DrawCursorUi(ScreenInfo uiInfo)
    {
        var size = uiInfo.Area.Size.Min() * 0.02f;
        DrawRoundedCursor(uiInfo.MousePos, size);
    }

    private static void DrawRoundedCursor(Vector2 tip, float size)
    {
        var dir = Vector2.Normalize(new Vector2(1, 1));
        var circleCenter = tip + dir * size * 2f;
        var left = circleCenter + new Vector2(-1, 0) * size;
        var top = circleCenter + new Vector2(0, -1) * size;

        Segment.DrawSegment(tip, left, 2f, CursorColor, LineCapType.CappedExtended, 3);
        Segment.DrawSegment(tip, top, 2f, CursorColor, LineCapType.CappedExtended, 3);

        new Circle(circleCenter, size).DrawSectorLines(180, 270, 0f, 2f, CursorColor, 0.65f);
    }
}
