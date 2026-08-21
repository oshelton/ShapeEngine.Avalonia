using System.Numerics;
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
        fpsDisplay.Load();
        GoToScene(new AvaloniaExamplesScene());
    }

    /// <remarks>The mirror of <see cref="BeginRun"/>: the last callback before the window, and the OpenGL
    /// context the font texture lives in, goes away.</remarks>
    protected override void EndRun() => fpsDisplay.Unload();

    /// <remarks>The UI pass runs after every screen texture has composited, so the readout sits over the
    /// Avalonia surfaces rather than under them.</remarks>
    protected override void DrawUI(ScreenInfo uiInfo) => fpsDisplay.Draw(uiInfo);

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
