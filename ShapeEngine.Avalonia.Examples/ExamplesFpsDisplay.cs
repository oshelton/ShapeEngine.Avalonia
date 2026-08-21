using System.Numerics;
using Raylib_cs;
using ShapeEngine.Color;
using ShapeEngine.Content;
using ShapeEngine.Core.GameDef;
using ShapeEngine.Core.Structs;
using ShapeEngine.Text;
using SeRect = ShapeEngine.Geometry.RectDef.Rect;

namespace AvaloniaExamples;

/// <summary>A frames per second readout pinned to the top right of the window, drawn by ShapeEngine.</summary>
/// <remarks>
/// Deliberately not an Avalonia control. Drawn from the game's <c>DrawUI</c> pass it lands over every
/// surface - screen textures composite before that pass - so the rate stays visible whatever the panels
/// are doing, and stays readable even if the Avalonia side stops presenting entirely.
/// </remarks>
public sealed class ExamplesFpsDisplay
{
    /// <summary>Font the readout is drawn with, embedded so no file has to sit next to the executable.</summary>
    /// <remarks>
    /// The same JetBrains Mono the Avalonia side uses (see <see cref="AvaloniaHost.ShapeEngineFont"/>), so
    /// the one piece of text raylib draws matches the rest. Reached through
    /// <see cref="System.Reflection.Assembly.GetManifestResourceStream(string)"/> rather than Avalonia's
    /// asset loader, hence the separate <c>EmbeddedResource</c> item alongside the <c>AvaloniaResource</c>
    /// one in the csproj - the same file, embedded twice, once for each loader.
    /// </remarks>
    private const string FontResourceName = "JetBrainsMono.ttf";

    /// <summary>Size the glyph atlas is rasterized at.</summary>
    /// <remarks>Comfortably above the largest size the readout is ever drawn at, so the trilinear filter
    /// is always scaling down rather than up.</remarks>
    private const int FontAtlasSize = 100;

    /// <summary>How long each measurement covers before the shown number is replaced.</summary>
    /// <remarks>Long enough that the number can be read rather than flickering through every frame's
    /// instantaneous rate, short enough that a stall still shows up as it happens.</remarks>
    private const float SampleInterval = 0.25f;

    /// <summary>Text height, as a fraction of the window's shorter side.</summary>
    private const float FontSizeFraction = 0.026f;

    /// <summary>Gap between the badge and the window edge, as a fraction of the window's shorter side.</summary>
    private const float MarginFraction = 0.014f;

    /// <summary>Space kept for the number, so the badge doesn't resize as the rate crosses 10 or 100.</summary>
    /// <remarks>
    /// Only a floor: a four digit rate widens the badge rather than overflowing it. The label is drawn
    /// right aligned within the badge, so the reserved space absorbs the change on the left and "FPS"
    /// itself stays put.
    /// </remarks>
    private const string WidthTemplate = "000 FPS";

    private const float BadgeRoundness = 0.4f;
    private const int BadgeSegments = 6;

    private static readonly ColorRgba BackgroundColorRgba = new(24, 24, 34, 220);
    private static readonly ColorRgba BorderColorRgba = new(90, 90, 130, 255);
    private static readonly ColorRgba TextColorRgba = new(235, 235, 245, 255);

    private TextFont? textFont;
    private Font font;

    private int frames;
    private float elapsed;
    private int fps;

    /// <summary>Loads the font. Call once the raylib window - and so the OpenGL context - exists.</summary>
    /// <remarks>A font that fails to load leaves the readout silently absent rather than taking the
    /// examples down with it, the same way a shader that fails to compile does.</remarks>
    public void Load()
    {
        if (textFont is not null) return;

        var data = ReadFontData();
        if (data is null) return;

        if (!ContentLoader.TryLoadFontFromMemory(".ttf", data, out font, FontAtlasSize, TextureFilter.Trilinear)) return;

        textFont = new TextFont(font, FontAtlasSize, 0f, 0f, TextColorRgba);
    }

    /// <summary>Releases the font. Call while the OpenGL context is still alive.</summary>
    public void Unload()
    {
        if (textFont is null) return;

        ContentLoader.UnloadFont(font);
        textFont = null;
    }

    /// <summary>Measures this frame and draws the current rate.</summary>
    public void Draw(ScreenInfo ui)
    {
        Sample();

        if (textFont is not { } text) return;

        // Everything is sized off the shorter side so the readout keeps its proportions at every window
        // size, and on a HighDPI display, where the UI screen is reported in device pixels.
        var reference = ui.Area.Size.Min();
        text.FontSize = reference * FontSizeFraction;

        var label = $"{fps} FPS";
        var labelSize = text.FontDimensions.GetTextSize(label);
        var reserved = Math.Max(labelSize.Width, text.FontDimensions.GetTextSize(WidthTemplate).Width);

        var paddingX = labelSize.Height * 0.55f;
        var paddingY = labelSize.Height * 0.3f;
        var badgeSize = new Size(reserved + paddingX * 2f, labelSize.Height + paddingY * 2f);

        // Centered in the nav bar strip rather than pinned to the very top, so it reads as part of the
        // same chrome as the nav buttons instead of sitting above them.
        var navBar = new SeRect(ui.Area.Left, ui.Area.Top, ui.Area.Width, ui.Area.Height * ExamplesLayout.NavHeight);
        var badge = new SeRect(new Vector2(navBar.Right - reference * MarginFraction, navBar.Center.Y), badgeSize, AnchorPoint.Right);

        badge.DrawRounded(BackgroundColorRgba, BadgeRoundness, BadgeSegments);
        badge.DrawLinesRounded(Math.Max(1f, reference * 0.0015f), BorderColorRgba, BadgeRoundness, BadgeSegments);

        text.DrawWord(label, new Vector2(badge.Right - paddingX, badge.Center.Y), AnchorPoint.Right);
    }

    /// <remarks>
    /// Counts frames over a fixed window rather than reading <see cref="Game.FramesPerSecond"/>, which is
    /// derived from the latest frame delta alone and so swings too far to read. Called from
    /// <see cref="Draw"/> so it counts drawn frames, whatever the update loop is doing.
    /// </remarks>
    private void Sample()
    {
        frames++;
        elapsed += (float)Game.Instance.FrameDelta;

        if (elapsed < SampleInterval) return;

        fps = (int)MathF.Round(frames / elapsed);
        frames = 0;
        elapsed = 0f;
    }

    private static byte[]? ReadFontData()
    {
        using var stream = typeof(ExamplesFpsDisplay).Assembly.GetManifestResourceStream(FontResourceName);
        if (stream is null) return null;

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return buffer.ToArray();
    }
}
