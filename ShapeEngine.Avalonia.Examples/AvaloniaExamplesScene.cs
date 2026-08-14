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
/// A single scene hosting all of the Avalonia example views at once, switched between with a nav bar
/// rather than each living on its own menu-selectable scene.
/// </summary>
/// <remarks>
/// Each view is built once, at activation, into an <see cref="ExampleView"/> owning its surfaces, their
/// content and its per-frame work, and the nav bar is built from that same list - so adding a view means
/// adding one <c>Build</c> method and nothing else. Surfaces stay alive for the scene's lifetime rather
/// than being disposed and recreated on every click.
/// </remarks>
public sealed class AvaloniaExamplesScene : Scene
{
    /// <summary>One example, and everything needed to show and run it.</summary>
    /// <param name="panes">
    /// Surfaces and the content each shows while current. Shaders and Drag &amp; Drop need more than one to
    /// make their point - independently post-processed regions, and a drag crossing between top levels.
    /// </param>
    /// <param name="unload">Releases anything owned outside Avalonia - shaders here - on deactivation.</param>
    private sealed class ExampleView(
        string label,
        (AvaloniaSurface Surface, Control Content)[] panes,
        Action<float> update,
        Action? unload = null)
    {
        public string Label { get; } = label;
        public (AvaloniaSurface Surface, Control Content)[] Panes { get; } = panes;
        public Action<float> Update { get; } = update;
        public Action? Unload { get; } = unload;
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

        // Before the nav bar, so its buttons can check themselves against the view already showing.
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

    /// <summary>Shows the given view's pane(s) and hides every other view's, by swapping their content.</summary>
    private void ShowView(ExampleView view)
    {
        currentView = view;

        foreach (var candidate in views)
        {
            var showing = ReferenceEquals(candidate, view);
            foreach (var (surface, content) in candidate.Panes) surface.Content = showing ? content : null;
        }
    }

    /// <summary>Creates a surface and hands it to the game, so no caller has to remember to do both.</summary>
    private AvaloniaSurface CreateSurface(AvaloniaSurfaceAnchor anchor, bool scaleContent = false, Control? content = null)
    {
        var surface = new AvaloniaSurface(content: content, anchor: anchor, scaleContent: scaleContent);

        surfaces.Add(surface);
        Game.Instance.AddCustomEvent(surface);

        return surface;
    }

    /// <remarks>
    /// Relies on <see cref="AvaloniaSurface.ScaleContent"/> to shrink the row into a narrow window rather
    /// than wrapping or clipping it. No intrinsic <c>Width</c> needed, unlike a panel with wrapping text -
    /// a row of non-wrapping buttons already has a natural size to scale from.
    /// </remarks>
    private void BuildNav()
    {
        var buttons = new Control[views.Count];

        for (var i = 0; i < views.Count; i++)
        {
            var view = views[i];

            buttons[i] = new RadioButton()
                .GroupName("exampleView")
                .Content(view.Label)
                .Padding(new Thickness(14, 8))
                .IsChecked(ReferenceEquals(view, currentView))
                .OnIsCheckedChanged(e =>
                {
                    if (((RadioButton)e.Source!).IsChecked != true) return;
                    ShowView(view);
                });
        }

        var content = new Border()
            .Background(new SolidColorBrush(Color.FromArgb(230, 20, 20, 28)))
            .BorderBrush(new SolidColorBrush(Color.FromArgb(255, 90, 90, 130)))
            .BorderThickness(new Thickness(0, 0, 0, 1))
            .Padding(new Thickness(16, 10))
            .Child(
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Spacing(8)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Children(buttons));

        CreateSurface(ExamplesLayout.NavBar, scaleContent: true, content: content);
    }

    /// <summary>A surface covering the content band, laid out with a <see cref="DockPanel"/>: a strip
    /// docked to each side, and a full panel filling whatever space is left in the centre.</summary>
    private ExampleView BuildFullWindowView()
    {
        var top = BuildDockStrip("Top", "DockPanel.Dock = Top", Color.FromRgb(255, 140, 200)).Height(44);
        var bottom = BuildDockStrip("Bottom", "DockPanel.Dock = Bottom", Color.FromRgb(160, 255, 170)).Height(44);
        var left = BuildDockStrip("Left", "DockPanel.Dock = Left", Color.FromRgb(120, 200, 255)).Width(130);
        var right = BuildDockStrip("Right", "DockPanel.Dock = Right", Color.FromRgb(255, 190, 120)).Width(130);

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

        var surface = CreateSurface(ExamplesLayout.Content(0f, 1f));

        return new ExampleView("Full Window", [(surface, content)], _ => panel.SetStatus(Status(surface)));
    }

    /// <summary>A small labeled strip for one side of the dock, styled the same regardless of which.</summary>
    private static Border BuildDockStrip(string label, string description, Color accent)
        => new Border()
            .Background(new SolidColorBrush(Color.FromArgb(200, 24, 24, 34)))
            .BorderBrush(new SolidColorBrush(accent))
            .BorderThickness(new Thickness(1))
            .CornerRadius(new CornerRadius(8))
            .Padding(new Thickness(10))
            .Margin(new Thickness(3))
            .Child(
                new StackPanel()
                    .Spacing(2)
                    .Children(
                        new TextBlock()
                            .Text(label)
                            .FontSize(13)
                            .FontWeight(FontWeight.SemiBold)
                            .Foreground(Brushes.White),
                        new TextBlock()
                            .Text(description)
                            .FontSize(10)
                            .TextWrapping(TextWrapping.Wrap)
                            .Foreground(Brushes.DarkGray)));

    private ExampleView BuildGalleryView()
    {
        var panel = new AvaloniaGalleryPanel();
        var surface = CreateSurface(ExamplesLayout.CenteredColumn(0.86f), scaleContent: true);

        var transitionTimer = 0f;

        return new ExampleView("Gallery", [(surface, panel)], dt =>
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
        var panes = new (AvaloniaSurface Surface, Control Content)[ShaderDefs.Length];
        var entries = new (AvaloniaSurface Surface, AvaloniaShaderPanel Panel, ShapeShader? Shader, Action<ShapeShader, float, float, int, int> Update)[ShaderDefs.Length];

        for (var i = 0; i < ShaderDefs.Length; i++)
        {
            var (title, description, left, width, load, update) = ShaderDefs[i];

            var panel = new AvaloniaShaderPanel(title, description);
            var surface = CreateSurface(ExamplesLayout.Content(left, width), scaleContent: true);
            var shader = load();

            // The surface always creates its texture with shader support, so there is nothing to configure.
            if (shader is not null) surface.PlacementTexture.Shaders?.Add(shader);

            panes[i] = (surface, panel);
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
            () =>
            {
                foreach (var (_, _, shader, _) in entries) shader?.Unload();
            });
    }

    /// <summary>Two surfaces - dragging between them crosses from one top level into a completely
    /// different one's, which is what <c>ShapeEngineDragSource</c> in <c>ShapeEngine.Avalonia</c> exists
    /// to track.</summary>
    private ExampleView BuildDragDropViews()
    {
        var sourcePanel = new AvaloniaDragDropSourcePanel();
        var sourceSurface = CreateSurface(ExamplesLayout.Content(ExamplesLayout.Inset, 0.45f), scaleContent: true);

        var targetPanel = new AvaloniaDragDropTargetPanel();
        var targetSurface = CreateSurface(ExamplesLayout.Content(0.52f, 0.45f), scaleContent: true);

        return new ExampleView("Drag & Drop", [(sourceSurface, sourcePanel), (targetSurface, targetPanel)],
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
        var surface = CreateSurface(ExamplesLayout.CenteredColumn(0.62f), scaleContent: true);

        surface.GamepadNavigation = GamepadNavigationMode.Directional;
        surface.KeyboardDrivenNavigation = true;

        return new ExampleView("Directional Nav", [(surface, panel)], _ => panel.SetStatus(Status(surface)));
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
