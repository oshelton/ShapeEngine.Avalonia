using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Declarative;
using Avalonia.Media;
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
/// Each view is built once, at activation, by an <see cref="ExampleViews"/> <c>Build</c> method into an
/// <see cref="ExampleView"/> owning its surfaces, content and per-frame work. Surfaces stay alive for the
/// scene's lifetime rather than being disposed and recreated on every click.
/// </remarks>
public sealed class AvaloniaExamplesScene : Scene
{
    /// <summary>Gap between the sidebar's items, which pad themselves but do not space themselves.</summary>
    private const double SidebarItemSpacing = 4;

    private readonly List<AvaloniaSurface> surfaces = [];
    private readonly List<ExampleView> views = [];
    private readonly List<(Vector2 Position, Vector2 Velocity, float Radius, ColorRgba Color)> circles = [];

    private ExampleView? currentView;

    protected override void OnActivate(Scene oldScene)
    {
        AvaloniaHost.EnsureInitialized();

        views.Add(ExampleViews.BuildFullWindowView(CreateSurface));
        views.Add(ExampleViews.BuildGalleryView(CreateSurface));
        views.Add(ExampleViews.BuildShaderViews(CreateSurface));
        views.Add(ExampleViews.BuildDragDropViews(CreateSurface));
        views.Add(ExampleViews.BuildDirectionalNavView(CreateSurface));

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
    /// Escape is polled straight from raylib: a surface locks the keyboard device while a control is
    /// mid-edit, which zeroes out anything read through ShapeEngine's input system.
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

    /// <summary>Draws whatever the current view puts over its own surfaces.</summary>
    protected override void OnDrawUI(ScreenInfo ui) => currentView?.DrawUi?.Invoke(ui);

    /// <summary>Shows the given view's pane(s) and hides every other view's.</summary>
    /// <remarks>
    /// A hidden surface comes off the game's screen texture list, so the engine does not size, render or
    /// composite it at all - the views that aren't showing cost nothing, not just less.
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
    /// Left at <see cref="ShaderSupportType.None"/> by default: it costs a second render texture the size
    /// of the surface, and only the shader views need it.
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
    /// labels stay crisp instead of growing and shrinking with the window. A <c>SidebarItem</c> is a
    /// <c>RadioButton</c> that takes its group from the <c>Sidebar</c> it sits in, so nothing here needs to
    /// wire the items to one another.
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
}
