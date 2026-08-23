using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
using Avalonia.Media;
using AvaloniaExamples.Shaders;
using AvaloniaExamples.Views;
using ShapeEngine.Avalonia;
using ShapeEngine.Avalonia.Input;
using ShapeEngine.Color;
using ShapeEngine.Core;
using ShapeEngine.Core.GameDef;
using ShapeEngine.Core.Structs;
using ShapeEngine.Geometry.CircleDef;
using ShapeEngine.Screen;
using SeRect = ShapeEngine.Geometry.RectDef.Rect;

namespace AvaloniaExamples;

/// <summary>
/// A single scene hosting all of the Avalonia example views at once, switched between with a sidebar
/// rather than each living on its own menu-selectable scene.
/// </summary>
/// <remarks>
/// Each view is built once, at activation, into an <see cref="ExampleView"/> owning its surfaces, their
/// content and its per-frame work, and the sidebar is built from that same list - so adding a view means
/// adding one <c>Build</c> method and nothing else. Surfaces stay alive for the scene's lifetime rather
/// than being disposed and recreated on every click.
/// </remarks>
public sealed class AvaloniaExamplesScene : Scene
{
    /// <summary>One example, and everything needed to show and run it.</summary>
    /// <param name="panes">
    /// The surfaces this view shows. Shaders and Drag &amp; Drop need more than one to make their point -
    /// independently post-processed regions, and a drag crossing between top levels.
    /// <para>
    /// Each keeps its content for the scene's lifetime and is shown and hidden rather than filled and
    /// emptied, so a view switched away from and back to still has its scroll positions, focus and
    /// animation state.
    /// </para>
    /// </param>
    /// <param name="unload">Releases anything owned outside Avalonia - shaders here - on deactivation.</param>
    /// <param name="activate">
    /// Called on every view whenever the current one changes, with whether this is now the one showing.
    /// For work that has to stop while the view is away: hiding a surface takes care of the surface
    /// itself, but not of anything hanging off its screen texture.
    /// </param>
    private sealed class ExampleView(
        string label,
        AvaloniaSurface[] panes,
        Action<float> update,
        Action? unload = null,
        Action<bool>? activate = null)
    {
        public string Label { get; } = label;
        public AvaloniaSurface[] Panes { get; } = panes;
        public Action<float> Update { get; } = update;
        public Action? Unload { get; } = unload;
        public Action<bool>? Activate { get; } = activate;
    }

    private static readonly (string Title, string Description, float Left, float Width, Func<ShapeShader?> Load, Action<ShapeShader, float, float, int, int> Update)[] ShaderDefs =
    [
        ("Hologram", "A travelling wobble, chromatic split and scanlines.",
            ExamplesLayout.Inset, 0.30f, AvaloniaHologramShader.Load, AvaloniaHologramShader.Update),
        ("CRT", "Barrel distortion, a vignette and phosphor scanlines - the corners fall outside the curved glass.",
            0.35f, 0.30f, AvaloniaCrtShader.Load, AvaloniaCrtShader.Update),
        ("Glow", "Blooms the brighter parts of the panel outward into a soft halo, with a slow pulse.",
            0.67f, 0.30f, AvaloniaGlowShader.Load, AvaloniaGlowShader.Update)
    ];

    private const float GalleryTransitionInterval = 1.6f;

    /// <summary>Gap between the sidebar's items, which pad themselves but do not space themselves.</summary>
    private const double SidebarItemSpacing = 4;

    private readonly List<AvaloniaSurface> surfaces = [];
    private readonly List<ExampleView> views = [];
    private readonly List<(Vector2 Position, Vector2 Velocity, float Radius, ColorRgba Color)> circles = [];

    private ExampleView? currentView;

    protected override void OnActivate(Scene oldScene)
    {
        AvaloniaHost.EnsureInitialized();

        views.Add(BuildFullWindowView());
        views.Add(BuildGalleryView());
        views.Add(BuildShaderViews());
        views.Add(BuildDragDropViews());
        views.Add(BuildDirectionalNavView());

        // Before the sidebar, so its items can check themselves against the view already showing.
        ShowView(views[0]);
        BuildNav();

        SpawnCircles();
    }

    protected override void OnDeactivate()
    {
        foreach (var view in views) view.Unload?.Invoke();

        views.Clear();
        currentView = null;

        foreach (var surface in surfaces)
        {
            Game.Instance.RemoveCustomEvent(surface);
            surface.Dispose();
        }

        surfaces.Clear();
    }

    /// <remarks>
    /// Escape is polled straight from raylib because a surface locks the keyboard device while a control
    /// is mid-edit (see <see cref="AvaloniaSurface.CaptureGameInput"/>), which zeroes out anything read
    /// through ShapeEngine's input system - quitting has to work whichever surface has focus.
    /// </remarks>
    protected override void OnUpdate(GameTime time, ScreenInfo game, ScreenInfo gameUi, ScreenInfo ui)
    {
        if (Raylib_cs.Raylib.IsKeyPressed(Raylib_cs.KeyboardKey.Escape)) Game.Instance.Quit();

        UpdateCircles(time.Delta, game.Area);

        currentView?.Update(time.Delta);
    }

    /// <summary>Draws behind every surface, proof that raylib keeps rendering correctly after Skia has
    /// had the OpenGL context.</summary>
    protected override void OnDrawGame(ScreenInfo game)
    {
        foreach (var (position, _, radius, color) in circles)
        {
            new Circle(position, radius).Draw(color, 0.9f);
        }
    }

    /// <summary>Shows the given view's pane(s) and hides every other view's.</summary>
    /// <remarks>
    /// Hiding is what keeps the views that are not showing free rather than merely cheap: a hidden surface
    /// comes off the game's screen texture list, so the engine does not size, render or composite it at
    /// all (see <see cref="AvaloniaSurface.Hide"/>). Nine surfaces are built here against one view showing
    /// at a time, and their areas add up to something over three times the window - so this is the
    /// difference between paying for the interface three times over every frame and paying for it once.
    /// </remarks>
    private void ShowView(ExampleView view)
    {
        currentView = view;

        foreach (var candidate in views)
        {
            var showing = ReferenceEquals(candidate, view);

            foreach (var surface in candidate.Panes)
            {
                if (showing) surface.Show();
                else surface.Hide();
            }

            candidate.Activate?.Invoke(showing);
        }
    }

    /// <summary>Creates a surface and hands it to the game, so no caller has to remember to do both.</summary>
    /// <param name="shaderSupport">
    /// Left at <see cref="ShaderSupportType.None"/> by default, against the surface's own <c>Multi</c>:
    /// only the shader views post-process anything, and support costs a second render texture the size of
    /// the surface whether or not a shader is ever added. Across the surfaces built here that buffer would
    /// otherwise be the larger half of the scene's render target memory.
    /// </param>
    private AvaloniaSurface CreateSurface(
        AvaloniaSurfaceAnchor anchor,
        bool scaleContent = false,
        Control? content = null,
        ShaderSupportType shaderSupport = ShaderSupportType.None)
    {
        var surface = new AvaloniaSurface(
            content: content,
            anchor: anchor,
            scaleContent: scaleContent,
            shaderSupport: shaderSupport);

        surfaces.Add(surface);
        Game.Instance.AddCustomEvent(surface);

        return surface;
    }

    /// <summary>Builds the sidebar the views are switched between with.</summary>
    /// <remarks>
    /// Laid out at its real size rather than through <see cref="AvaloniaSurface.ScaleContent"/>, so the
    /// labels stay crisp and the sidebar reads the same at every window size instead of growing and
    /// shrinking with it. That trades away the Viewbox's automatic fit, so the items have to fit the
    /// column unaided - which is what <see cref="ExamplesLayout.SidebarWidth"/> is picked for, and why the
    /// labels are given as ellipsizing text blocks for the sizes where the fraction is not enough.
    /// <para>
    /// Nothing here sets a group name or wires the items to one another: a <c>SidebarItem</c> is a
    /// <c>RadioButton</c> that takes its group from the <c>Sidebar</c> it sits in, so picking one clears
    /// the rest on its own. The bottom of the sidebar is left empty for the frames per second readout,
    /// which <see cref="ExamplesFpsDisplay"/> draws over it rather than into it.
    /// </para>
    /// </remarks>
    private void BuildNav()
    {
        var items = new Control[views.Count + 1];
        items[0] = ExampleControls.NavigationLabel("Views");

        for (var i = 0; i < views.Count; i++)
        {
            var view = views[i];
            var item = ExampleControls.NavigationItem(view.Label);

            item.IsChecked = ReferenceEquals(view, currentView);
            item.IsCheckedChanged += (_, _) =>
            {
                if (item.IsChecked != true) return;
                ShowView(view);
            };

            items[i + 1] = item;
        }

        var sidebar = ExampleControls.NavigationSidebar();

        sidebar.Header = ExampleControls.Title("Examples");
        sidebar.Content = new StackPanel().Spacing(SidebarItemSpacing).Children(items);

        CreateSurface(ExamplesLayout.Sidebar, scaleContent: false, content: sidebar);
    }

    /// <summary>A surface covering the content band, laid out with a <see cref="DockPanel"/>: a strip
    /// docked to each side, and a full panel filling whatever space is left in the centre.</summary>
    private ExampleView BuildFullWindowView()
    {
        var top = BuildDockStrip("Top", "DockPanel.Dock = Top");
        var bottom = BuildDockStrip("Bottom", "DockPanel.Dock = Bottom");
        var left = BuildDockStrip("Left", "DockPanel.Dock = Left").Width(150);
        var right = BuildDockStrip("Right", "DockPanel.Dock = Right").Width(150);

        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        DockPanel.SetDock(left, Dock.Left);
        DockPanel.SetDock(right, Dock.Right);

        var panel = new AvaloniaDemoPanel(
            "Full window view",
            "The centre of a DockPanel - whatever space the four docked strips around it leave behind.")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // Order matters: each side is carved off in turn, and only what's left goes to the last child.
        var content = new DockPanel { LastChildFill = true, Children = { top, bottom, left, right, panel } };

        var surface = CreateSurface(ExamplesLayout.Content(0f, 1f), content: content);

        return new ExampleView("Full Window", [surface], _ => panel.SetStatus(Status(surface)));
    }

    /// <summary>A small labeled strip for one side of the dock, styled the same regardless of which.</summary>
    private static Control BuildDockStrip(string label, string description)
        => ExampleControls.Panel()
            .Child(
                new StackPanel()
                    .Spacing(2)
                    .Children(
                        new TextBlock().Text(label),
                        ExampleControls.Label(description)));

    private ExampleView BuildGalleryView()
    {
        var panel = new AvaloniaGalleryPanel();
        var surface = CreateSurface(ExamplesLayout.CenteredColumn(0.86f), scaleContent: true, content: panel);

        var transitionTimer = 0f;

        return new ExampleView("Gallery", [surface], dt =>
        {
            panel.Advance(dt);

            transitionTimer += dt;
            if (transitionTimer >= GalleryTransitionInterval)
            {
                transitionTimer -= GalleryTransitionInterval;
                panel.AdvanceTransitions();
            }

            panel.SetStatus(Status(surface));
        });
    }

    /// <summary>Three fragment shaders, each post-processing its own surface within the content band.</summary>
    private ExampleView BuildShaderViews()
    {
        var panes = new AvaloniaSurface[ShaderDefs.Length];
        var entries = new (AvaloniaSurface Surface, AvaloniaShaderPanel Panel, ShapeShader? Shader, Action<ShapeShader, float, float, int, int> Update)[ShaderDefs.Length];

        for (var i = 0; i < ShaderDefs.Length; i++)
        {
            var (title, description, left, width, load, update) = ShaderDefs[i];

            var panel = new AvaloniaShaderPanel(title, description);
            var surface = CreateSurface(
                ExamplesLayout.Content(left, width),
                scaleContent: true,
                shaderSupport: ShaderSupportType.Multi,
                content: panel);
            var shader = load();

            // These are the surfaces asking for shader support above, so the container is there to add to.
            if (shader is not null) surface.PlacementTexture.Shaders?.Add(shader);

            panes[i] = surface;
            entries[i] = (surface, panel, shader, update);
        }

        var elapsed = 0f;

        return new ExampleView("Shaders", panes,
            dt =>
            {
                elapsed += dt;

                foreach (var (surface, panel, shader, update) in entries)
                {
                    if (shader is not null)
                    {
                        shader.Enabled = panel.ShaderEnabled;
                        update(shader, elapsed, panel.Strength, surface.PlacementTexture.Width, surface.PlacementTexture.Height);
                    }

                    var state = shader switch
                    {
                        null => "shader failed to compile",
                        { Enabled: false } => "shader off",
                        _ => $"shader on at {panel.Strength:0.00}"
                    };

                    panel.SetStatus($"{state}\n{Status(surface)}");
                }
            },
            unload: () =>
            {
                foreach (var (_, _, shader, _) in entries) shader?.Unload();
            },
            // A shader is enabled on the shader itself, not on the surface, so emptying these three
            // surfaces does not stop their passes: the screen texture still runs every active shader over
            // itself each frame, and the update above - which is the only thing that syncs Enabled to the
            // toggle switch - only runs while this view is the one showing. Left alone, the three passes
            // would keep running from every other view, on uniforms that stopped being updated.
            activate: showing =>
            {
                foreach (var (_, panel, shader, _) in entries)
                {
                    if (shader is not null) shader.Enabled = showing && panel.ShaderEnabled;
                }
            });
    }

    /// <summary>Two surfaces - dragging between them crosses from one top level into a completely
    /// different one's, which is what <c>ShapeEngineDragSource</c> in <c>ShapeEngine.Avalonia</c> exists
    /// to track.</summary>
    private ExampleView BuildDragDropViews()
    {
        var sourcePanel = new AvaloniaDragDropSourcePanel();
        var sourceSurface = CreateSurface(ExamplesLayout.Content(ExamplesLayout.Inset, 0.45f), scaleContent: true, content: sourcePanel);

        var targetPanel = new AvaloniaDragDropTargetPanel();
        var targetSurface = CreateSurface(ExamplesLayout.Content(0.52f, 0.45f), scaleContent: true, content: targetPanel);

        return new ExampleView("Drag & Drop", [sourceSurface, targetSurface],
            _ =>
            {
                sourcePanel.SetStatus(Status(sourceSurface));
                targetPanel.SetStatus(Status(targetSurface));
            });
    }

    /// <summary>A menu driven by direction rather than tab order, with no pointer involved.</summary>
    /// <remarks>
    /// The only surface here that opts into either navigation setting: <c>Directional</c> turns the D-pad
    /// into arrow keys for <c>XYFocus</c> to act on, and <c>KeyboardDrivenNavigation</c> keeps those keys
    /// flowing with the pointer somewhere else entirely, which is the whole point of a gamepad menu.
    /// </remarks>
    private ExampleView BuildDirectionalNavView()
    {
        var panel = new AvaloniaDirectionalNavPanel();
        var surface = CreateSurface(ExamplesLayout.CenteredColumn(0.8f), scaleContent: true, content: panel);

        surface.GamepadNavigation = GamepadNavigationMode.Directional;
        surface.KeyboardDrivenNavigation = true;

        return new ExampleView("Directional Nav", [surface], _ => panel.SetStatus(Status(surface)));
    }

    private void UpdateCircles(float dt, SeRect bounds)
    {
        for (var i = 0; i < circles.Count; i++)
        {
            var (position, velocity, radius, color) = circles[i];

            position += velocity * dt;

            if (position.X - radius < bounds.Left || position.X + radius > bounds.Right) velocity.X = -velocity.X;
            if (position.Y - radius < bounds.Top || position.Y + radius > bounds.Bottom) velocity.Y = -velocity.Y;

            position = new Vector2(
                Math.Clamp(position.X, bounds.Left + radius, bounds.Right - radius),
                Math.Clamp(position.Y, bounds.Top + radius, bounds.Bottom - radius));

            circles[i] = (position, velocity, radius, color);
        }
    }

    private void SpawnCircles()
    {
        circles.Clear();

        var random = new Random(42);
        var area = Game.Instance.GameScreenInfo.Area;

        for (var i = 0; i < 24; i++)
        {
            var radius = random.Next(14, 46);
            circles.Add((
                new Vector2(
                    area.Left + radius + random.NextSingle() * Math.Max(area.Width - radius * 2f, 1f),
                    area.Top + radius + random.NextSingle() * Math.Max(area.Height - radius * 2f, 1f)),
                new Vector2(random.NextSingle() - 0.5f, random.NextSingle() - 0.5f) * 400f,
                radius,
                new ColorRgba(random.Next(60, 220), random.Next(60, 220), random.Next(120, 255), 200)));
        }
    }

    /// <remarks>
    /// Kept short on purpose: this is rewritten every frame, and on a <c>ScaleContent</c> surface a status
    /// line long enough to wrap changes the panel's height as the values change, which rescales the whole
    /// panel through its <c>Viewbox</c>. The panels pair this with a non-wrapping status line.
    /// </remarks>
    private static string Status(AvaloniaSurface surface)
    {
        var rect = surface.DestinationRect;
        return
            $"""
             Drawn at {rect.Width:0}x{rect.Height:0}
             Pointer: {surface.WantsPointer}   Keyboard: {surface.WantsKeyboard}
             """;
    }
}
