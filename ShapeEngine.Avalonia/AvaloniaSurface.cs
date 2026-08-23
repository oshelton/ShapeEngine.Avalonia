using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Raylib_cs;
using ShapeEngine.Avalonia.Input;
using ShapeEngine.Core.GameDef;
using ShapeEngine.Core.Structs;
using ShapeEngine.Input;
using ShapeEngine.Screen;
using AvControl = Avalonia.Controls.Control;
using RlColor = Raylib_cs.Color;
using SeRect = ShapeEngine.Geometry.RectDef.Rect;

namespace ShapeEngine.Avalonia;

/// <summary>
/// Hosts Avalonia UI inside a ShapeEngine game, rendered onto the game's OpenGL surface.
/// </summary>
/// <remarks>
/// Register it with <c>Game.AddCustomEvent</c> and dispose it when done; Avalonia must already be
/// configured with <see cref="AppBuilderExtensions.UseShapeEngine"/>.
/// <para>
/// The surface owns the <see cref="ScreenTexture"/> it renders through, so the texture's
/// <see cref="ScreenTexture.Shaders"/> post-process the interface. Screen textures composite before the
/// game's <c>DrawUI</c>, so anything the game draws there covers it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// //App Setup
/// AppBuilder.Configure&lt;MyApp&gt;().UseShapeEngine().SetupWithoutStarting();
///
/// //Begin display and interaction.
/// Game.Instance.AddCustomEvent(new AvaloniaSurface(new MyMenuView()));
///
/// // Cleanup when done with the surface.
/// Game.Instance.RemoveCustomEvent(surface);
/// surface.Dispose();
/// </code>
/// </example>
public sealed class AvaloniaSurface : Game.CustomEvent, IDisposable
{
    private readonly ShapeEngineTopLevelImpl impl;
    private readonly AvaloniaInputPump inputPump;
    private readonly ScreenTexture placement;

    /// <summary>Wraps <see cref="content"/> to scale it; only built when <see cref="ScaleContent"/> asks for it.</summary>
    private readonly Viewbox? scaleBox;

    /// <summary>Drives Avalonia's animation clock, independent of the game's own time scaling.</summary>
    private readonly Stopwatch renderClock = Stopwatch.StartNew();

    private AvControl? content;
    private MouseCursor currentCursor = MouseCursor.Default;
    private bool hasLockedMouse;
    private bool hasLockedKeyboard;
    private bool wantsExclusiveKeyboard;
    private bool isDisposed;

    /// <summary>Creates a surface and the screen texture it renders through.</summary>
    /// <param name="content">The Avalonia control tree to display.</param>
    /// <param name="anchor">Where the surface sits on screen. Defaults to the whole window.</param>
    /// <param name="scaleContent">
    /// Scales the content to fit the surface instead of laying it out at the surface's size.
    /// </param>
    /// <param name="order">
    /// Execution order relative to other custom events. Lower values run - and draw - first.
    /// </param>
    /// <param name="shaderSupport">
    /// How many shaders <see cref="PlacementTexture"/> can post-process the interface with. Anything
    /// other than <see cref="ShaderSupportType.None"/> costs a second render texture of the surface's
    /// size, so a surface that will never carry a shader is worth declaring as such.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Avalonia has not been configured with <see cref="AppBuilderExtensions.UseShapeEngine"/>.
    /// </exception>
    public AvaloniaSurface(
        AvControl? content = null,
        AvaloniaSurfaceAnchor? anchor = null,
        bool scaleContent = false,
        int order = 0,
        ShaderSupportType shaderSupport = ShaderSupportType.Multi)
        : base(order)
    {
        if (!ShapeEnginePlatform.IsInitialized)
        {
            throw new InvalidOperationException(
                $"Avalonia isn't set up yet. Call AppBuilder.Configure<...>().{nameof(AppBuilderExtensions.UseShapeEngine)}().SetupWithoutStarting() before creating an AvaloniaSurface.");
        }

        var placementAnchor = anchor ?? AvaloniaSurfaceAnchor.FullScreen;

        // Multi by default, so post-processing the interface never means rebuilding it - at the price of
        // the shader buffer texture, which is why the caller can opt out.
        placement = new ScreenTexture(placementAnchor.Stretch, placementAnchor.Position, shaderSupport);
        placement.Initialize(Game.Instance.Window.CurScreenSize, Raylib.GetMousePosition());
        placement.OnDrawGame += OnPlacementDraw;

        Game.Instance.AddScreenTexture(placement);

        impl = new ShapeEngineTopLevelImpl(
            ShapeEnginePlatform.PlatformGraphics,
            new ShapeEngineClipboard(),
            ShapeEnginePlatform.Compositor);

        impl.CursorChanged += OnCursorChanged;

        SyncSize();

        TopLevel = new ShapeEngineTopLevel(impl)
        {
            // No background: it would hide the game and hit test, stealing the pointer from it.
            Background = null,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent, WindowTransparencyLevel.None]
        };

        inputPump = new AvaloniaInputPump(impl);

        ScaleContent = scaleContent;
        scaleBox = scaleContent ? new Viewbox { Stretch = Stretch.Uniform } : null;
        this.content = content;

        if (content is not null)
            ApplyContent();

        TopLevel.Prepare();
        TopLevel.StartRendering();
    }

    /// <summary>The Avalonia root hosting <see cref="Content"/>.</summary>
    public ShapeEngineTopLevel TopLevel { get; }

    /// <summary>The Avalonia control tree drawn over the game.</summary>
    public AvControl? Content
    {
        get => content;
        set
        {
            if (ReferenceEquals(content, value)) return;

            content = value;
            ApplyContent();
        }
    }

    /// <summary>
    /// Whether the content is scaled to fit the surface rather than laid out at the surface's size.
    /// Set once, at construction - a surface wanting the other behavior is cheap enough to just create.
    /// </summary>
    /// <remarks>
    /// Off, a larger surface gives controls more room and text keeps its size. On, everything grows
    /// together - scaled through the visual tree, so text stays crisp and hit testing follows.
    /// <para>
    /// Scaled content is measured unconstrained, so give it an intrinsic size - usually a <c>Width</c> on
    /// the root control. Without one, wrapping text never wraps and the runaway natural width scales
    /// everything down to nothing.
    /// </para>
    /// </remarks>
    public bool ScaleContent { get; }

    /// <summary>
    /// The screen texture this surface renders through, for attaching shaders or changing the draw order.
    /// Owned by the surface and unloaded with it.
    /// </summary>
    public ScreenTexture PlacementTexture => placement;

    /// <summary>The area of the window the UI is drawn into, in screen coordinates.</summary>
    public SeRect DestinationRect => placement.GetDestinationRect();

    /// <summary>Maps a point in Avalonia's client space to screen coordinates.</summary>
    /// <remarks>
    /// The way back out of the interface, and the mirror of what the pointer goes through coming in - so
    /// the engine can draw over a control at the size and place it actually appears, through the display
    /// scale and wherever the anchor put the surface.
    /// </remarks>
    public Vector2 ToScreen(Point client)
    {
        var destination = DestinationRect;

        // Client space is device independent and the texture is in physical pixels, so the render scale
        // goes on first; the texture then composites into its destination rectangle, which is the rest.
        var scale = (float)impl.RenderScaling;
        var x = (float)client.X * scale * (placement.Width > 0 ? destination.Width / placement.Width : 1f);
        var y = (float)client.Y * scale * (placement.Height > 0 ? destination.Height / placement.Height : 1f);

        return destination.TopLeft + new Vector2(x, y);
    }

    /// <summary>Maps a rectangle in Avalonia's client space to screen coordinates.</summary>
    /// <remarks>Both corners are mapped, so a <see cref="ScaleContent"/> surface reports the size a
    /// control is drawn at rather than the size it was laid out at.</remarks>
    public SeRect ToScreen(Rect client)
    {
        var topLeft = ToScreen(client.TopLeft);
        var bottomRight = ToScreen(client.BottomRight);

        return new SeRect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }

    /// <summary>Whether the surface is currently on the game. Surfaces start shown.</summary>
    /// <seealso cref="Show"/>
    /// <seealso cref="Hide"/>
    public bool IsVisible { get; private set; } = true;

    /// <summary>Whether the cursor is currently over a hit-testable Avalonia control.</summary>
    public bool WantsPointer { get; private set; }

    /// <summary>
    /// Whether the UI currently needs the keyboard - a control is actively accepting text, or the
    /// pointer is over the surface and something inside it is focused.
    /// </summary>
    public bool WantsKeyboard { get; private set; }

    /// <summary>
    /// Whether ShapeEngine's own input devices are locked while the UI has capture. Default is true.
    /// </summary>
    /// <remarks>
    /// Turn this off to route input yourself - <see cref="WantsPointer"/> and <see cref="WantsKeyboard"/>
    /// stay accurate either way. Locking takes effect on the next <c>InputSystem</c> update, leaving one
    /// frame of overlap per transition.
    /// <para>
    /// The keyboard locks only for active text editing, and at device level
    /// (<see cref="KeyboardDevice.Lock"/>) - that is what stops ShapeEngine's own polling from draining
    /// raylib's character queue out from under a control mid-edit. Nothing can be exempted once engaged, so
    /// an always-on binding like quit should poll its key directly rather than go through an
    /// <c>InputAction</c>. Navigation keys are unaffected either way: <see cref="AvaloniaInputPump"/> reads
    /// those straight from raylib rather than through the locked device.
    /// </para>
    /// <para>
    /// Keep <see cref="WindowSettings.HighDPI"/> off until a ShapeEngine release carries the
    /// <c>GameWindow.MoveMouse</c> fix (<see href="https://github.com/DaveGreen-Games/ShapeEngine/pull/180"/>):
    /// before it, any keypress makes the keyboard the active device, after which the engine repositions the
    /// mouse every frame and walks it into a corner.
    /// </para>
    /// </remarks>
    public bool CaptureGameInput { get; set; } = true;

    /// <summary>How a gamepad's D-pad moves focus in this surface. Linear by default.</summary>
    /// <remarks>
    /// <see cref="GamepadNavigationMode.Directional"/> only does anything for content that has opted into
    /// <c>XYFocus</c> - it sends arrow keys, which move nothing on their own.
    /// </remarks>
    public GamepadNavigationMode GamepadNavigation { get; set; } = GamepadNavigationMode.Linear;

    /// <summary>
    /// Routes navigation keys while anything inside is focused, rather than only while the pointer is
    /// also over the surface. Off by default.
    /// </summary>
    /// <remarks>
    /// For a menu meant to be driven entirely by keyboard or gamepad, where expecting the pointer to rest
    /// over the surface defeats the point. Focus still has to start somewhere - nothing here can create
    /// it, so the content should focus a control itself when it appears.
    /// <para>
    /// The keys are forwarded, not taken: this does not lock ShapeEngine's keyboard (see
    /// <see cref="CaptureGameInput"/>), so a game binding the arrow keys itself will see them too while
    /// this surface has focus.
    /// </para>
    /// </remarks>
    public bool KeyboardDrivenNavigation { get; set; }

    #region Showing and hiding

    /// <summary>Puts the surface back on the game, resizing it first if the window changed while it was
    /// away.</summary>
    /// <remarks>
    /// A hidden surface is off the game's screen texture list entirely, which is what makes it free -
    /// the engine neither updates, renders nor composites it. The cost is that it also stops tracking
    /// the window, so this catches it up before the first frame back.
    /// <para>
    /// Deliberately caught up here rather than tracked while hidden: <see cref="ScreenTexture.Update"/>
    /// only reloads the render texture when the size it is given differs from the size it holds, so a
    /// surface that outlives several resizes still reallocates once, on the way back in, rather than
    /// once per resize it sat through.
    /// </para>
    /// <para>
    /// Safe to call from input handling or from a scene's update - a control's click handler and a menu's
    /// update are both fine. The engine sizes screen textures before either and draws them after, so a
    /// surface shown there draws the same frame, at the size set here rather than at whatever the engine's
    /// sizing pass left behind before this surface rejoined.
    /// </para>
    /// </remarks>
    public void Show()
    {
        if (isDisposed || IsVisible) return;

        IsVisible = true;

        // Before the texture goes back on the game, so the frame it rejoins is already the right size.
        placement.Update(0f, Game.Instance.Window.CurScreenSize, Raylib.GetMousePosition(), Game.Instance.Paused);
        SyncSize();

        Game.Instance.AddScreenTexture(placement);
    }

    /// <summary>Takes the surface off the game, so it costs nothing until it is shown again.</summary>
    /// <remarks>
    /// The Avalonia content is left intact and keeps its state - scroll positions, focus, animation
    /// clocks - so showing the surface again picks up where it left off. To drop the content instead,
    /// set <see cref="Content"/> to <c>null</c>.
    /// <para>
    /// Input locks are released on the way out: a hidden surface must not leave the game's mouse or
    /// keyboard locked to a UI that is no longer on screen.
    /// </para>
    /// </remarks>
    public void Hide()
    {
        if (isDisposed || !IsVisible) return;

        IsVisible = false;

        Game.Instance.RemoveScreenTexture(placement);

        ReleaseInputLocks();
        WantsPointer = false;
        WantsKeyboard = false;
        wantsExclusiveKeyboard = false;
    }

    #endregion

    #region Game loop hooks

    /// <inheritdoc/>
    /// <remarks>
    /// Runs after the engine has updated the screen textures, so the placement texture's size and scaled
    /// mouse position are already current.
    /// <para>
    /// A hidden surface reads no input at all: it is not on screen, so there is nothing for the pointer
    /// to be over, and hit testing it would let it take the pointer away from whatever is.
    /// </para>
    /// </remarks>
    protected override void PreHandleInput(GameTime time, Vector2 mousePosGame, Vector2 mousePosGameUi, Vector2 mousePosUi)
    {
        if (isDisposed || !IsVisible) return;

        SyncSize();
        UpdateCapture();
        inputPump.Pump(
            GetPointerPosition(),
            WantsPointer || hasLockedMouse,
            WantsKeyboard || hasLockedKeyboard,
            wantsExclusiveKeyboard || hasLockedKeyboard,
            GamepadNavigation);
        ApplyInputLocks();

        // Harmless to call from every surface every frame: a no-op once nothing is dragging, or once
        // whichever surface actually has the pointer this frame has already reported it.
        ShapeEngineDragSource.Pump(this);
    }

    /// <summary>
    /// Renders the UI and blits it into the placement texture, from inside the texture's draw pass.
    /// </summary>
    /// <remarks>
    /// The game pass rather than the UI one, because the texture applies its shaders between the two -
    /// drawing in <c>OnDrawUI</c> would put the interface past them. Running the Skia pass inside the
    /// texture's render target is safe because <c>RlglStateGuard</c> restores whichever framebuffer was
    /// bound.
    /// <para>
    /// A surface with no content skips both steps rather than rasterizing an empty tree and blitting the
    /// result. The texture has already been cleared by the time this runs, so what composites is a
    /// transparent surface rather than the last frame the content did draw. <see cref="Hide"/> is the
    /// cheaper way to put a surface away - it costs nothing at all rather than a clear and a composite -
    /// but this keeps an emptied surface honest either way.
    /// </para>
    /// <para>
    /// The dispatcher is pumped from here, so only surfaces that are shown and have content pump it.
    /// Surfaces share one dispatcher and one render tick, so any one of them drawing keeps Avalonia
    /// running for all of them - but hide or empty every surface at once and Avalonia's timers and
    /// animations stop advancing until one comes back.
    /// </para>
    /// </remarks>
    private void OnPlacementDraw(ScreenInfo info, ScreenTexture texture)
    {
        if (isDisposed || content is null) return;

        RenderAvalonia();
        Present(new Rectangle(0, 0, texture.Width, texture.Height));
    }

    /// <summary>Advances Avalonia by one frame and rasterizes it into the surface framebuffer.</summary>
    /// <remarks>
    /// The order matters: draining the jobs the tick queues before painting keeps layout and animation
    /// changes in this frame rather than the next.
    /// </remarks>
    private void RenderAvalonia()
    {
        ShapeEnginePlatform.PumpDispatcher();
        ShapeEnginePlatform.TriggerRenderTick(renderClock.Elapsed);
        Dispatcher.UIThread.RunJobs();

        impl.OnDraw(new Rect(impl.ClientSize));
    }

    /// <summary>Draws the rendered UI into the currently bound render target.</summary>
    private void Present(Rectangle destination)
    {
        if (impl.TryGetSurface() is not { IsDisposed: false } surface) return;

        // Avalonia's output is premultiplied; raylib's default alpha blending would darken the edges.
        Raylib.BeginBlendMode(BlendMode.AlphaPremultiply);
        Raylib.DrawTexturePro(
            surface.Texture,
            new Rectangle(0, 0, surface.Texture.Width, surface.Texture.Height),
            destination,
            Vector2.Zero,
            0f,
            RlColor.White);
        Raylib.EndBlendMode();
    }

    #endregion

    #region Content, sizing, input arbitration and cursor

    /// <summary>Puts the content into the top level, wrapped for scaling when asked for.</summary>
    private void ApplyContent()
    {
        if (scaleBox is null)
        {
            TopLevel.Content = content;
            return;
        }

        scaleBox.Child = content;
        TopLevel.Content = scaleBox;
    }

    /// <summary>Matches the surface framebuffer to the placement texture.</summary>
    /// <remarks>
    /// The texture is sized in physical pixels, so the DPI scale is what leaves Avalonia laying out in
    /// device independent pixels while rasterizing at full resolution.
    /// </remarks>
    private void SyncSize()
    {
        var size = new PixelSize(Math.Max(placement.Width, 1), Math.Max(placement.Height, 1));

        var scaling = Raylib.GetWindowScaleDPI().X;
        if (scaling <= 0f || Single.IsNaN(scaling)) scaling = 1f;

        impl.SetRenderSize(size, scaling);
    }

    /// <summary>The cursor position in Avalonia's client coordinate space.</summary>
    /// <remarks>
    /// The engine has already mapped the mouse into the texture's pixel space for its anchor, so all that
    /// remains is the conversion to device independent pixels. Internal rather than private: also used by
    /// <see cref="ShapeEngineDragSource"/>, which needs a surface's own local pointer position to feed a
    /// drag into it once the drag has moved onto this surface.
    /// </remarks>
    internal Point GetPointerPosition()
    {
        var position = placement.GameUiScreenInfo.MousePos;
        var scaling = impl.RenderScaling;

        return new Point(position.X / scaling, position.Y / scaling);
    }

    private void UpdateCapture()
    {
        // A hit result of the top level itself means the cursor is over empty space rather than over
        // actual UI, so the game should keep the pointer.
        var hit = TopLevel.InputHitTest(GetPointerPosition());
        WantsPointer = hit is not null && !ReferenceEquals(hit, TopLevel);

        // Text input being active always wants the keys, even with the pointer elsewhere - a control
        // mid-edit doesn't stop wanting keystrokes just because the mouse moved off it.
        wantsExclusiveKeyboard = impl.TextInputMethod.IsActive;

        // Focus alone is still a bad signal for locking the game out - see ApplyInputLocks. It is a
        // fine signal for routing, though: while the pointer is over the UI, whatever it focused - Tab
        // included - needs the keys forwarded to move on to the next control. A keyboard driven surface
        // drops the pointer half of that, since requiring it would defeat the point.
        //
        // The top level of the focused element has to be checked, not just that something is focused:
        // GetFocusedElement reports the focused element of the whole application, not of the top level it
        // is asked of, so every surface would otherwise believe it holds focus whenever any one of them
        // does. Two surfaces forwarding the same key then moves focus twice per press.
        var focused = TopLevel.FocusManager?.GetFocusedElement();
        var hasFocus = focused is Visual visual
                       && !ReferenceEquals(focused, TopLevel)
                       && ReferenceEquals(global::Avalonia.Controls.TopLevel.GetTopLevel(visual), TopLevel);

        WantsKeyboard = wantsExclusiveKeyboard || (hasFocus && (WantsPointer || KeyboardDrivenNavigation));
    }

    /// <remarks>
    /// The keyboard locks for exclusive text editing only, not for <see cref="WantsKeyboard"/> at large -
    /// a focused button still routes Tab and Enter to Avalonia (see <see cref="UpdateCapture"/>), but it
    /// should not stop the game seeing its own keys just because the pointer happens to be over it. See
    /// <see cref="CaptureGameInput"/>'s remarks for the trade-off that leaves on the table.
    /// </remarks>
    private void ApplyInputLocks()
    {
        var input = Game.Instance.Input;
        SetLock(input.Mouse, CaptureGameInput && WantsPointer, ref hasLockedMouse);
        SetLock(input.Keyboard, CaptureGameInput && wantsExclusiveKeyboard, ref hasLockedKeyboard);
    }

    private void ReleaseInputLocks()
    {
        var input = Game.Instance.Input;
        SetLock(input.Mouse, false, ref hasLockedMouse);
        SetLock(input.Keyboard, false, ref hasLockedKeyboard);
    }

    /// <remarks>
    /// Tracked per surface, so several surfaces can each lock and release without unlocking on another's
    /// behalf.
    /// </remarks>
    private static void SetLock(InputDevice device, bool locked, ref bool isLocked)
    {
        if (locked == isLocked) return;

        isLocked = locked;
        if (locked) device.Lock();
        else device.Unlock();
    }

    private void OnCursorChanged(MouseCursor cursor)
    {
        currentCursor = cursor;
        Raylib.SetMouseCursor(cursor);
    }

    #endregion

    /// <summary>Tears down the Avalonia top level and the screen texture, releasing their GPU resources.</summary>
    /// <remarks>Remove the surface from the game with <c>Game.RemoveCustomEvent</c> first.</remarks>
    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        ReleaseInputLocks();

        if (currentCursor != MouseCursor.Default) Raylib.SetMouseCursor(MouseCursor.Default);

        placement.OnDrawGame -= OnPlacementDraw;
        Game.Instance.RemoveScreenTexture(placement);
        placement.Unload();

        impl.CursorChanged -= OnCursorChanged;
        TopLevel.StopRendering();
        TopLevel.Dispose();
        impl.Dispose();
    }
}
