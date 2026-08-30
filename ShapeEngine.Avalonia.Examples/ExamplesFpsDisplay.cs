using System.Numerics;
using Avalonia.Platform;
using Raylib_cs;
using ShapeEngine.Color;
using ShapeEngine.Content;
using ShapeEngine.Core.GameDef;
using ShapeEngine.Core.Structs;
using ShapeEngine.Text;
using SeRect = ShapeEngine.Geometry.RectDef.Rect;

namespace AvaloniaExamples;

/// <summary>A frames per second readout pinned to the foot of the navigation sidebar, drawn by
/// ShapeEngine.</summary>
/// <remarks>
/// Deliberately not an Avalonia control, and so not in the sidebar's own footer slot either. Drawn from
/// the game's <c>DrawUI</c> pass it lands over every surface - screen textures composite before that pass
/// - so the rate stays visible whatever the panels are doing, and stays readable even if the Avalonia side
/// stops presenting entirely. It only borrows the sidebar's geometry from <see cref="ExamplesLayout"/>,
/// landing on the empty space the nav items leave below them.
/// </remarks>
public sealed class ExamplesFpsDisplay
{
    /// <summary>Font the readout is drawn with.</summary>
    /// <remarks>
    /// The same face the interface is set in: the themes ask for Inter, and <see cref="AvaloniaHost"/>
    /// registers the collection that supplies it, so reading the regular weight straight out of that same
    /// package is what keeps the one piece of text raylib draws matching the Avalonia content over it.
    /// <para>
    /// Inter is proportional where the readout used to be monospace, so the label changes width as its
    /// digits do. It is centred in the sidebar column, which splits that change evenly across both ends
    /// rather than letting one edge walk.
    /// </para>
    /// </remarks>
    private const string FontAssetUri = AvaloniaHost.InterRegular;

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

    /// <summary>Gap between the readout and the foot of the sidebar, as a fraction of the window's
    /// shorter side.</summary>
    private const float MarginFraction = 0.014f;

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

        // Sat at the foot of the navigation sidebar's column rather than in a corner of the window, so it
        // reads as part of the same chrome as the nav items above it. Drawn over the sidebar rather than
        // placed in its footer slot, which is what keeps it independent of Avalonia - see the remarks on
        // the class.
        var sidebar = new SeRect(
            ui.Area.Left,
            ui.Area.Top + ui.Area.Height * ExamplesLayout.ContentTop,
            ui.Area.Width * ExamplesLayout.SidebarWidth,
            ui.Area.Height * ExamplesLayout.ContentHeight);

        text.DrawWord(
            $"{fps} FPS",
            new Vector2(sidebar.Center.X, sidebar.Bottom - reference * MarginFraction),
            AnchorPoint.BottomCenter);
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

    /// <remarks>
    /// Through Avalonia.s asset loader, so this only works once Avalonia has been set up - which is why
    /// <see cref="AvaloniaExamplesGame"/> initializes the host before loading the readout.
    /// </remarks>
    private static byte[]? ReadFontData()
    {
        if (!AssetLoader.Exists(new Uri(FontAssetUri))) return null;

        using var stream = AssetLoader.Open(new Uri(FontAssetUri));

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return buffer.ToArray();
    }
}
