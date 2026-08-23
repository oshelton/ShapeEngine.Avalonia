using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
using AvaloniaExamples.Shaders;
using AvaloniaExamples.Views;
using ShapeEngine.Avalonia;
using ShapeEngine.Avalonia.Input;
using ShapeEngine.Core.Structs;
using ShapeEngine.Screen;

namespace AvaloniaExamples;

/// <summary>One example, and everything needed to show and run it.</summary>
/// <param name="unload">Releases anything owned outside Avalonia - shaders here - on deactivation.</param>
/// <param name="activate">Called whenever the current view changes, with whether this is now the one showing.</param>
/// <param name="drawUi">Drawn by the engine over this view's surfaces while it is the one showing.</param>
public sealed class ExampleView(
    string label,
    AvaloniaSurface[] panes,
    Action<float> update,
    Action? unload = null,
    Action<bool>? activate = null,
    Action<ScreenInfo>? drawUi = null)
{
    public string Label { get; } = label;
    public AvaloniaSurface[] Panes { get; } = panes;
    public Action<float> Update { get; } = update;
    public Action? Unload { get; } = unload;
    public Action<bool>? Activate { get; } = activate;
    public Action<ScreenInfo>? DrawUi { get; } = drawUi;
}

/// <summary>Builds each of <see cref="AvaloniaExamplesScene"/>'s example views.</summary>
public static class ExampleViews
{
    /// <summary>Creates a surface and hands it to the game - supplied by the scene, which owns the surfaces.</summary>
    public delegate AvaloniaSurface CreateSurface(
        AvaloniaSurfaceAnchor anchor,
        bool scaleContent = false,
        Control? content = null,
        ShaderSupportType shaderSupport = ShaderSupportType.None);

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

    /// <summary>A <see cref="DockPanel"/>: a strip docked to each side, and a panel filling the centre.</summary>
    public static ExampleView BuildFullWindowView(CreateSurface createSurface)
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

        var content = new DockPanel { LastChildFill = true, Children = { top, bottom, left, right, panel } };

        var surface = createSurface(ExamplesLayout.Content(0f, 1f), content: content);

        return new ExampleView("Full Window", [surface], _ => panel.SetStatus(Status(surface)));
    }

    private static Control BuildDockStrip(string label, string description)
        => ExampleControls.Panel()
            .Child(
                new StackPanel()
                    .Spacing(2)
                    .Children(
                        new TextBlock().Text(label),
                        ExampleControls.Label(description)));

    public static ExampleView BuildGalleryView(CreateSurface createSurface)
    {
        var panel = new AvaloniaGalleryPanel();
        var surface = createSurface(ExamplesLayout.CenteredColumn(0.86f), scaleContent: true, content: panel);

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
    public static ExampleView BuildShaderViews(CreateSurface createSurface)
    {
        var panes = new AvaloniaSurface[ShaderDefs.Length];
        var entries = new (AvaloniaSurface Surface, AvaloniaShaderPanel Panel, ShapeShader? Shader, Action<ShapeShader, float, float, int, int> Update)[ShaderDefs.Length];

        for (var i = 0; i < ShaderDefs.Length; i++)
        {
            var (title, description, left, width, load, update) = ShaderDefs[i];

            var panel = new AvaloniaShaderPanel(title, description);
            var surface = createSurface(
                ExamplesLayout.Content(left, width),
                scaleContent: true,
                shaderSupport: ShaderSupportType.Multi,
                content: panel);
            var shader = load();

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
            // Shaders keep running from every surface regardless of which view is showing, so Enabled has
            // to be resynced to the toggle switch on activation - otherwise a hidden view's passes would
            // keep running on stale uniforms.
            activate: showing =>
            {
                foreach (var (_, panel, shader, _) in entries)
                {
                    if (shader is not null) shader.Enabled = showing && panel.ShaderEnabled;
                }
            });
    }

    /// <summary>Two surfaces - dragging between them crosses from one top level into another entirely.</summary>
    public static ExampleView BuildDragDropViews(CreateSurface createSurface)
    {
        var sourcePanel = new AvaloniaDragDropSourcePanel();
        var sourceSurface = createSurface(ExamplesLayout.Content(ExamplesLayout.Inset, 0.45f), scaleContent: true, content: sourcePanel);

        var targetPanel = new AvaloniaDragDropTargetPanel();
        var targetSurface = createSurface(ExamplesLayout.Content(0.52f, 0.45f), scaleContent: true, content: targetPanel);

        return new ExampleView("Drag & Drop", [sourceSurface, targetSurface],
            _ =>
            {
                sourcePanel.SetStatus(Status(sourceSurface));
                targetPanel.SetStatus(Status(targetSurface));
            });
    }

    /// <summary>A menu driven by direction rather than tab order, with no pointer involved.</summary>
    public static ExampleView BuildDirectionalNavView(CreateSurface createSurface)
    {
        var panel = new AvaloniaDirectionalNavPanel();
        var surface = createSurface(ExamplesLayout.CenteredColumn(0.8f), scaleContent: true, content: panel);

        surface.GamepadNavigation = GamepadNavigationMode.Directional;
        surface.KeyboardDrivenNavigation = true;

        var ring = new ExamplesFocusRing();

        return new ExampleView(
            "Directional Nav",
            [surface],
            dt =>
            {
                panel.SetStatus(Status(surface));

                if (panel.FocusedButton is not { } focus)
                {
                    ring.Update(dt, null);
                    return;
                }

                // The corner radius is in the panel's shrunken layout units, so it has to be scaled up by
                // the same factor the surface scaled the panel's bounds by.
                var bounds = surface.ToScreen(focus.Bounds);
                var scale = focus.Bounds.Width > 0 ? bounds.Width / (float)focus.Bounds.Width : 1f;

                ring.Update(dt, bounds, (float)focus.CornerRadius * scale);
            },
            activate: showing =>
            {
                if (showing) panel.FocusDefault();
                else ring.Reset();
            },
            drawUi: ring.Draw);
    }

    /// <summary>Kept short and non-wrapping: rewritten every frame, and a <c>ScaleContent</c> surface
    /// rescales its whole panel if the status line's height changes.</summary>
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
