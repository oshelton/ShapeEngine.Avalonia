using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
using Avalonia.Media;
using Avalonia.Styling;
using ShadUI;
using ShapeEngine.Avalonia.Controls;
using ShapeEngine.Color;
using ShapeEngine.Core.Structs;
using ShapeEngine.Geometry.CircleDef;
using ShapeEngine.Geometry.PolygonDef;
using ShapeEngine.Geometry.TriangleDef;
using AvSlider = Avalonia.Controls.Slider;
using SeRect = ShapeEngine.Geometry.RectDef.Rect;
using SeSize = ShapeEngine.Core.Structs.Size;
using SeVec2 = System.Numerics.Vector2;

namespace AvaloniaExamples.Views;

/// <summary>
/// A single panel mixing plain Avalonia controls, buttons with ShapeEngine-rendered icons, ShapeEngine
/// content used as plain images, and Avalonia's own animation system - four tabs, one surface.
/// </summary>
/// <remarks>
/// Exists to stress the integration rather than to demonstrate any one feature: standard controls share
/// the surface with static, animated and direct ShapeEngine views all at once, each hit-testable or not
/// exactly as its plain Avalonia counterpart would be. The Animation tab is the odd one out - keyframes,
/// transitions and cross-fades that are Avalonia's own, driven by the same render tick as everything else.
/// </remarks>
public sealed class AvaloniaGalleryPanel : ViewBase
{
    private enum IconShape { Circle, Square, Triangle, Star, Ring }

    private static readonly IconShape[] IconShapes =
        [IconShape.Circle, IconShape.Square, IconShape.Triangle, IconShape.Star, IconShape.Ring];

    /// <summary>One fixed colour per icon shape, so a shape reads the same wherever it appears.</summary>
    private static readonly ColorRgba[] IconColors =
    [
        new(120, 200, 255, 255),
        new(160, 255, 170, 255),
        new(255, 190, 120, 255),
        new(230, 130, 200, 255),
        new(255, 230, 120, 255)
    ];

    /// <summary>Colours the radio group chooses between, applied to the gallery's hero and direct tiles.</summary>
    private static readonly (string Name, ColorRgba Color)[] Accents =
    [
        ("Blue", new ColorRgba(120, 200, 255, 255)),
        ("Green", new ColorRgba(160, 255, 170, 255)),
        ("Amber", new ColorRgba(255, 190, 120, 255))
    ];

    private static readonly string[] RotatingMessages =
    [
        "Cross-fading content",
        "Driven by the game loop",
        "Skia on raylib's context",
        "No Avalonia render thread"
    ];

    /// <summary>Fixed across all tabs, so switching tabs doesn't resize the panel.</summary>
    private const float TabContentHeight = 420f;

    /// <summary>Size a ShapeEngine glyph is drawn at when it is the whole of a control's content.</summary>
    private const double GlyphSize = 28;

    /// <summary>Size it is drawn at in a button's icon slot, matching that slot's Viewbox width.</summary>
    /// <remarks>The Viewbox scales it to the row, which the direct view follows correctly because it
    /// draws through Skia's accumulated transform rather than its own bounds alone.</remarks>
    private const double IconSlotSize = 24;

    private readonly ObservableCollection<string> log = [];
    private readonly List<Button> iconButtons = [];
    private readonly HashSet<int> favoriteIcons = [];
    private readonly HashSet<int> includedIcons = [..Enumerable.Range(0, IconShapes.Length)];

    private TextBlock statusText = null!;
    private TextBlock selectionText = null!;
    private AvSlider speedSlider = null!;
    private CheckBox highlightCheckBox = null!;
    private ToggleSwitch orbitRingsToggle = null!;
    private ComboBox gallerySeedCombo = null!;
    private TabControl tabControl = null!;
    private ShapeEngineStaticTextureView heroView = null!;
    private ShapeEngineStaticTextureView emblemView = null!;
    private ProgressBar transitionedBar = null!;
    private Border pulsingPanel = null!;
    private TransitioningContentControl rotatingContent = null!;

    private int selectedIcon = -1;
    private int pickedIcon = -1;
    private int accentIndex;
    private IconShape gallerySeed = IconShape.Star;
    private int emblemSeed = 1;
    private int messageIndex;
    private bool highlighted;
    private float elapsed;

    public AvaloniaGalleryPanel()
    {
        Initialize();

        RefreshSelectionText();

        gallerySeedCombo.SelectionChanged += (_, _) =>
        {
            var index = gallerySeedCombo.SelectedIndex;
            if (index < 0) return;

            gallerySeed = IconShapes[index];
            heroView.InvalidateContent();
            AppendLog($"ShapeEngine hero shape -> {gallerySeed}");
        };
    }

    private double AnimationSpeed => speedSlider.Value;

    /// <summary>Whether the animated tile's orbit paths are drawn behind the moving dots.</summary>
    private bool ShowOrbitRings => orbitRingsToggle.IsChecked == true;

    protected override object Build()
        => ExampleControls.Panel()
            .Width(680)
            .VerticalAlignment(VerticalAlignment.Top)
            .Child(
                new StackPanel()
                    .Spacing(10)
                    .Children(
                        ExampleControls.Title("Gallery"),
                        ExampleControls.Body("Native controls, buttons with ShapeEngine icons, ShapeEngine content used as plain images, and Avalonia's own animations - all on one surface."),
                        BuildTabs(),
                        ExampleControls.Status().Ref(out statusText)));

    /// <summary>Advances the gallery's animated and direct content. Called by the scene each frame.</summary>
    public void Advance(float deltaTime) => elapsed += deltaTime * (float)AnimationSpeed;

    /// <summary>Shows the surface's live state, updated by the scene each frame.</summary>
    public void SetStatus(string status) => statusText.Text = status;

    private Control BuildTabs()
    {
        var tabs = new List<TabItem>
        {
            new() { Header = "Controls", Content = Scrollable(BuildControlsTab()) },
            new() { Header = "Icon buttons", Content = Scrollable(BuildIconButtonsTab()) },
            new() { Header = "ShapeEngine", Content = Scrollable(BuildShapeEngineTab()) },
            new() { Header = "Animation", Content = Scrollable(BuildAnimationTab()) }
        };

        return new TabControl().Ref(out tabControl).ItemsSource(tabs);
    }

    /// <summary>Wraps a tab's content so it scrolls rather than pushing the panel taller.</summary>
    private static Control Scrollable(Control content) => new ScrollViewer().Height(TabContentHeight).Content(content);

    /// <summary>Plain Avalonia controls only - nothing on this tab is drawn by ShapeEngine.</summary>
    private Control BuildControlsTab()
        => new StackPanel()
            .Spacing(10)
            .Children(
                new TextBox()
                    .PlaceholderText("Notes - the game stops seeing the keyboard"),
                ExampleControls.Label("ShapeEngine hero shape"),
                new ComboBox()
                    .Ref(out gallerySeedCombo)
                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                    .ItemsSource(IconShapes.Select(shape => shape.ToString()).ToArray())
                    .SelectedIndex(Array.IndexOf(IconShapes, gallerySeed)),
                ExampleControls.Label("Accent"),
                BuildAccentRow(),
                new CheckBox()
                    .Ref(out highlightCheckBox)
                    .Content("Highlight selected icon")
                    .IsChecked(true)
                    .OnIsCheckedChanged(_ => RefreshHighlights()),
                ExampleControls.Label("ShapeEngine animation speed"),
                new AvSlider()
                    .Ref(out speedSlider)
                    .Minimum(0)
                    .Maximum(3)
                    .Value(1),
                new ToggleSwitch()
                    .Ref(out orbitRingsToggle)
                    .Content("Orbit paths")
                    .IsChecked(true),
                new ListBox()
                    .ItemsSource(log)
                    .Height(110),
                new Expander()
                    .Header("About this tab")
                    .Content(
                ExampleControls.Body("Every control here is a plain Avalonia control - nothing on this tab is drawn by ShapeEngine.")));

    private Control BuildAccentRow()
    {
        var buttons = new Control[Accents.Length];

        for (var i = 0; i < Accents.Length; i++)
        {
            var index = i;
            buttons[i] = new RadioButton()
                .GroupName("accent")
                .Content(Accents[index].Name)
                .IsChecked(index == accentIndex)
                .OnIsCheckedChanged(e =>
                {
                    if (((RadioButton)e.Source!).IsChecked != true) return;

                    accentIndex = index;
                    heroView.InvalidateContent();
                    emblemView.InvalidateContent();
                    AppendLog($"Accent -> {Accents[index].Name}");
                });
        }

        return new StackPanel().Orientation(Orientation.Horizontal).Spacing(12).Children(buttons);
    }

    /// <summary>
    /// Four different Avalonia controls, each with a small ShapeEngine render as part of its content
    /// rather than an icon font or bitmap asset, and each keeping its own selection model.
    /// </summary>
    /// <remarks>
    /// Every icon is drawn directly rather than through a texture view - at this size there is no texture
    /// allocation or read back worth avoiding, and a control's icon has to redraw on every hover and
    /// press regardless of which control is hosting it.
    /// </remarks>
    private Control BuildIconButtonsTab()
    {
        iconButtons.Clear();

        var buttons = new Control[IconShapes.Length];

        for (var i = 0; i < IconShapes.Length; i++)
        {
            var index = i;
            var shape = IconShapes[i];

            // Into ShadUI's icon slot rather than the content: its template keeps a Viewbox ahead of the
            // label for this, and its button classes pin Height to 36 and clip to it, so taller content
            // loses its bottom line.
            var button = new Button()
                .Classes(ExamplesTheme.OutlineButton)
                .Margin(new Thickness(ExamplesTheme.PanelSpacing))
                .Content(shape.ToString())
                .OnClick(_ => SelectIcon(index));

            ButtonAssist.SetIcon(button, BuildIconGlyph(shape, index, IconSlotSize));

            iconButtons.Add(button);
            buttons[i] = button;
        }

        return new StackPanel()
            .Spacing(10)
            .Children(
                ExampleControls.Body("Each control below carries a small ShapeEngine render, not an image asset."),
                ExampleControls.Label("Button - click to select"),
                new WrapPanel().Children(buttons),
                ExampleControls.Label("RadioButton - pick exactly one"),
                BuildRadioButtonsRow(),
                ExampleControls.Label("ToggleButton - any number of favorites"),
                BuildToggleButtonsRow(),
                ExampleControls.Label("CheckBox - included in the set"),
                BuildCheckBoxRow(),
                new TextBlock()
                    .Ref(out selectionText)
                    .TextWrapping(TextWrapping.Wrap));
    }

    /// <summary>
    /// The small ShapeEngine render every control on this tab uses as part of its content: the plain icon
    /// plus a breathing halo and an orbiting highlight, so it reads as alive rather than static clip art.
    /// A direct view redraws every frame regardless, so the flourish costs nothing extra to add.
    /// </summary>
    private Control BuildIconGlyph(IconShape shape, int index, double size = GlyphSize)
        => new ShapeEngineDirectView { DrawContent = bounds => DrawIconGlyph(bounds, shape, IconColors[index], index) }
            .Width(size)
            .Height(size);

    /// <summary>Layers a breathing halo and an orbiting highlight dot over <see cref="DrawIcon"/>'s plain shape.</summary>
    /// <param name="phaseOffset">
    /// The icon's index in <see cref="IconShapes"/>, so five glyphs side by side don't all breathe and
    /// orbit in lockstep.
    /// </param>
    /// <remarks>
    /// The halo is a filled, low-alpha circle rather than an outline: <c>Circle.DrawLines</c> strokes
    /// straddling its radius, so its true outer edge is <c>Radius + 2 * lineThickness</c>, not
    /// <c>Radius</c> - easy to overshoot a 28x28 box without noticing. A fill's edge is exactly its
    /// radius, so the numbers below are the actual on-screen extents.
    /// </remarks>
    private void DrawIconGlyph(SeRect bounds, IconShape shape, ColorRgba color, int phaseOffset)
    {
        var center = bounds.Center;
        var unit = Math.Min(bounds.Width, bounds.Height);

        var phase = elapsed * 1.6f + phaseOffset * MathF.Tau / IconShapes.Length;
        var breathe = 0.5f + 0.5f * MathF.Sin(phase);

        // Stays within 0.46 * unit at its largest, leaving a safety margin inside the 0.5 * unit the
        // control's bounds actually allow.
        var glowRadius = unit * (0.40f + breathe * 0.06f);
        new Circle(center, glowRadius).Draw(color.SetAlpha((byte)(30 + breathe * 40)), 0.9f);

        // Shrunk so the icon always sits inside the glow, whichever shape it is.
        DrawIcon(bounds, shape, color, scale: 0.8f);

        var orbit = phase * 1.3f;
        var highlightPos = center + new SeVec2(MathF.Cos(orbit), MathF.Sin(orbit)) * unit * 0.4f;
        new Circle(highlightPos, unit * 0.045f).Draw(color.Lerp(ColorRgba.White, 0.6f), 0.9f);
    }

    private Control BuildRadioButtonsRow()
    {
        var radios = new Control[IconShapes.Length];

        for (var i = 0; i < IconShapes.Length; i++)
        {
            var index = i;
            var shape = IconShapes[i];

            radios[i] = new RadioButton()
                .GroupName("iconPick")
                .Content(
                    new StackPanel()
                        .Spacing(4)
                        .Children(
                            BuildIconGlyph(shape, index),
                            new TextBlock()
                                .Text(shape.ToString())
                                .Classes(ExamplesTheme.CaptionClass)
                                .HorizontalAlignment(HorizontalAlignment.Center)))
                .OnIsCheckedChanged(e =>
                {
                    if (((RadioButton)e.Source!).IsChecked != true) return;

                    pickedIcon = index;
                    RefreshSelectionText();
                    AppendLog($"Picked -> {shape}");
                });
        }

        return new WrapPanel().Children(radios);
    }

    private Control BuildToggleButtonsRow()
    {
        var toggles = new Control[IconShapes.Length];

        for (var i = 0; i < IconShapes.Length; i++)
        {
            var index = i;
            var shape = IconShapes[i];

            // Classed because ShadUI's bare ToggleButton theme carries no padding - it comes with the
            // variant. Its RadioButton and CheckBox themes pad themselves, so only this row needed it.
            toggles[i] = new ToggleButton()
                .Classes(ExamplesTheme.DefaultToggle)
                .Margin(new Thickness(ExamplesTheme.PanelSpacing))
                .Content(
                    new StackPanel()
                        .Spacing(4)
                        .Children(
                            BuildIconGlyph(shape, index),
                            new TextBlock()
                                .Text(shape.ToString())
                                .Classes(ExamplesTheme.CaptionClass)
                                .HorizontalAlignment(HorizontalAlignment.Center)))
                .OnIsCheckedChanged(e => ToggleFavorite(index, ((ToggleButton)e.Source!).IsChecked == true));
        }

        return new WrapPanel().Children(toggles);
    }

    private void ToggleFavorite(int index, bool isFavorite)
    {
        if (isFavorite) favoriteIcons.Add(index);
        else favoriteIcons.Remove(index);

        RefreshSelectionText();
        AppendLog($"Favorite {IconShapes[index]} -> {isFavorite}");
    }

    private Control BuildCheckBoxRow()
    {
        var checks = new Control[IconShapes.Length];

        for (var i = 0; i < IconShapes.Length; i++)
        {
            var index = i;
            var shape = IconShapes[i];

            checks[i] = new CheckBox()
                .Content(
                    new StackPanel()
                        .Orientation(Orientation.Horizontal)
                        .Spacing(6)
                        .Children(
                            BuildIconGlyph(shape, index),
                            new TextBlock()
                                .Text(shape.ToString())
                                .Classes(ExamplesTheme.CaptionClass)
                                .VerticalAlignment(VerticalAlignment.Center)))
                .IsChecked(true)
                .OnIsCheckedChanged(e => ToggleIncluded(index, ((CheckBox)e.Source!).IsChecked == true));
        }

        return new StackPanel().Spacing(6).Children(checks);
    }

    private void ToggleIncluded(int index, bool included)
    {
        if (included) includedIcons.Add(index);
        else includedIcons.Remove(index);

        RefreshSelectionText();
        AppendLog($"Included {IconShapes[index]} -> {included}");
    }

    private void SelectIcon(int index)
    {
        selectedIcon = index;
        RefreshHighlights();
        RefreshSelectionText();
        AppendLog($"Icon clicked -> {IconShapes[index]}");
    }

    /// <summary>Marks the selected button, when the checkbox on the Controls tab asks for it.</summary>
    /// <remarks>
    /// Swapped between two classes rather than toggling one on and off: ShadUI's carry sizing as well as
    /// colour, so a class worn only while selected would resize the button on every click. Outline and
    /// Secondary are the same size, leaving the click changing nothing but the fill.
    /// </remarks>
    private void RefreshHighlights()
    {
        var highlightOn = highlightCheckBox.IsChecked == true;

        for (var i = 0; i < iconButtons.Count; i++)
        {
            var marked = highlightOn && i == selectedIcon;

            iconButtons[i].Classes.Set(ExamplesTheme.OutlineButton, !marked);
            iconButtons[i].Classes.Set(ExamplesTheme.SecondaryButton, marked);
        }
    }

    /// <summary>Summarizes all four controls' state - each has its own selection model, worth seeing at once.</summary>
    private void RefreshSelectionText()
    {
        var selected = selectedIcon >= 0 ? IconShapes[selectedIcon].ToString() : "none";
        var picked = pickedIcon >= 0 ? IconShapes[pickedIcon].ToString() : "none";
        var favorites = favoriteIcons.Count == 0
            ? "none"
            : String.Join(", ", favoriteIcons.Order().Select(i => IconShapes[i]));
        var included = includedIcons.Count == IconShapes.Length
            ? "all"
            : String.Join(", ", includedIcons.Order().Select(i => IconShapes[i]));

        selectionText.Text =
            $"""
             Button selected: {selected}
             RadioButton picked: {picked}
             ToggleButton favorites: {favorites}
             CheckBox included: {included}
             """;
    }

    /// <summary>ShapeEngine content displayed as plain images - none of it is a button.</summary>
    private Control BuildShapeEngineTab()
        => new StackPanel()
            .Spacing(12)
            .Children(
                ExampleControls.Body("Everything below is drawn by ShapeEngine and shown as a plain image."),
                BuildHero(),
                ExampleControls.Label("One static view per shape, drawn once"),
                BuildShapeRow(),
                ExampleControls.Label("Animated view, redrawn continuously - toggle orbit paths on the Controls tab"),
                BuildAnimatedTile(),
                ExampleControls.Label("Direct view - no texture, rotated"),
                BuildDirectTile(),
                ExampleControls.Label("Random emblem - regenerate on click"),
                BuildEmblem(),
                new Button()
                    .Classes(ExamplesTheme.PrimaryButton)
                    .Content("Regenerate emblem")
                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                    .HorizontalContentAlignment(HorizontalAlignment.Center)
                    .OnClick(_ => RegenerateEmblem()));

    /// <summary>Reflects the Controls tab's combo box and radio group - the cross-tab proof point.</summary>
    private Control BuildHero()
        => new Border()
            .Height(120)
            .ClipToBounds(true)
            .Child(
                new ShapeEngineStaticTextureView { DrawContent = bounds => DrawIcon(bounds, gallerySeed, Accents[accentIndex].Color) }
                    .Ref(out heroView));

    private static Control BuildShapeRow()
    {
        var tiles = new Control[IconShapes.Length];

        for (var i = 0; i < IconShapes.Length; i++)
        {
            var shape = IconShapes[i];
            var color = IconColors[i];

            tiles[i] = new Border()
                .Width(48)
                .Height(48)
                .Child(new ShapeEngineStaticTextureView { DrawContent = bounds => DrawIcon(bounds, shape, color) });
        }

        return new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).Children(tiles);
    }

    private Control BuildAnimatedTile()
        => new Border()
            .Height(160)
            .ClipToBounds(true)
            .Child(new ShapeEngineAnimatedTextureView { DrawContent = DrawAnimatedTile });

    /// <summary>
    /// Concentric rings of orbiting dots, each ring spinning at its own speed and, every other ring, its
    /// own direction. <see cref="ShowOrbitRings"/> toggles the paths behind them; a pulsing core keeps
    /// something moving even with the rings turned off.
    /// </summary>
    private void DrawAnimatedTile(SeRect bounds)
    {
        var center = bounds.Center;
        var unit = Math.Min(bounds.Width, bounds.Height);

        for (var ring = 0; ring < IconColors.Length; ring++)
        {
            var radius = unit * (0.14f + ring * 0.09f);
            var color = IconColors[ring];

            if (ShowOrbitRings)
            {
                new Circle(center, radius).DrawLines(2f, color.SetAlpha(150), 0.9f);
            }

            // Each ring turns a little slower than the one inside it, and every other one reverses.
            var direction = ring % 2 == 0 ? 1f : -1f;
            var angle = elapsed * direction * (1.6f - ring * 0.28f);

            for (var i = 0; i < 3; i++)
            {
                var offset = angle + i * MathF.Tau / 3f;
                var position = center + new SeVec2(MathF.Cos(offset), MathF.Sin(offset)) * radius;

                new Circle(position, unit * (0.045f - ring * 0.005f)).Draw(color, 0.9f);
            }
        }

        var pulse = 0.5f + 0.5f * MathF.Sin(elapsed * 2.4f);
        new Circle(center, unit * (0.05f + pulse * 0.03f)).Draw(new ColorRgba(255, 255, 255, 200), 0.9f);
    }

    private Control BuildDirectTile()
        => new Border()
            .Height(90)
            .ClipToBounds(true)
            .Child(
                new ShapeEngineDirectView
                {
                    DrawContent = DrawDirectTile,
                    RenderTransform = new RotateTransform(-6)
                });

    /// <summary>A row of animated bars in the current accent colour, drawn straight into Avalonia's framebuffer.</summary>
    private void DrawDirectTile(SeRect bounds)
    {
        const int barCount = 14;

        var barWidth = bounds.Width / (barCount * 1.6f);
        var accent = Accents[accentIndex].Color;

        for (var i = 0; i < barCount; i++)
        {
            var phase = elapsed * 2f + i * 0.45f;
            var height = bounds.Height * (0.25f + 0.35f * (0.5f + 0.5f * MathF.Sin(phase)));
            var x = bounds.X + bounds.Width * (i + 0.5f) / barCount;

            new SeRect(x - barWidth * 0.5f, bounds.Bottom - height, barWidth, height).Draw(accent.SetAlpha(220));
        }
    }

    private Control BuildEmblem()
        => new Border()
            .Height(110)
            .ClipToBounds(true)
            .Child(new ShapeEngineStaticTextureView { DrawContent = DrawEmblem }.Ref(out emblemView));

    /// <summary>Picks a new emblem seed and asks the static view to redraw.</summary>
    private void RegenerateEmblem()
    {
        emblemSeed++;
        emblemView.InvalidateContent();
        AppendLog("Emblem regenerated");
    }

    /// <summary>
    /// A fixed scatter of shapes from the current seed, in the current accent colour. Nothing here reads
    /// the animation clock, so the result only changes when the seed does or the accent does - the case a
    /// static view is for.
    /// </summary>
    private void DrawEmblem(SeRect bounds)
    {
        var random = new Random(emblemSeed);
        var center = bounds.Center;
        var unit = Math.Min(bounds.Width, bounds.Height);
        var accent = Accents[accentIndex].Color;

        for (var i = 0; i < 9; i++)
        {
            var angle = (float)random.NextDouble() * MathF.Tau;
            var distance = unit * (0.1f + (float)random.NextDouble() * 0.55f);
            var position = center + new SeVec2(MathF.Cos(angle), MathF.Sin(angle)) * distance;

            new Circle(position, unit * (0.05f + (float)random.NextDouble() * 0.1f)).Draw(accent.SetAlpha(190), 0.9f);
        }

        new Circle(center, unit * 0.36f).DrawLines(2f, new ColorRgba(255, 255, 255, 120), 0.9f);
    }

    /// <summary>
    /// Avalonia's own animation system rather than ShapeEngine drawing: keyframe animations on render
    /// transforms, property transitions triggered by state changes, and the built-in animations of
    /// <see cref="ProgressBar"/> and <see cref="TransitioningContentControl"/>.
    /// </summary>
    /// <remarks>
    /// Avalonia's animation clock is advanced by the surface's render tick, so everything here tests that
    /// plumbing - if the clock stalls or jumps, the motion stutters visibly.
    /// </remarks>
    private Control BuildAnimationTab()
        => new StackPanel()
            .Spacing(12)
            .Children(
                ExampleControls.Body("Every animation below is advanced by the surface's render tick, not by an Avalonia render thread."),
                BuildTransformRow(),
                ExampleControls.Label("Keyframe animations: rotate, pulse, slide"),
                new ProgressBar()
                    .IsIndeterminate(true),
                ExampleControls.Label("Property transitions, triggered every 1.6s by the scene"),
                BuildTransitionedBar(),
                BuildPulsingPanel(),
                BuildRotatingContent());

    /// <summary>Advances the state that the transition-based animations react to.</summary>
    /// <remarks>
    /// Called by the scene on a timer rather than a timer inside the view, so the transitions are visibly
    /// driven by the game rather than by Avalonia running independently.
    /// </remarks>
    public void AdvanceTransitions()
    {
        highlighted = !highlighted;

        transitionedBar.Value = highlighted ? 100 : 0;
        pulsingPanel.Opacity = highlighted ? 1.0 : 0.35;
        pulsingPanel.Background = new SolidColorBrush(
            highlighted ? Color.FromRgb(90, 140, 220) : Color.FromRgb(60, 60, 90));

        messageIndex = (messageIndex + 1) % RotatingMessages.Length;
        rotatingContent.Content = new TextBlock()
            .Text(RotatingMessages[messageIndex])
            .HorizontalAlignment(HorizontalAlignment.Center);
    }

    private static Control BuildTransformRow()
    {
        var rotating = CreateShape(Color.FromRgb(120, 200, 255), new CornerRadius(8), new RotateTransform());
        var pulsing = CreateShape(Color.FromRgb(160, 255, 170), new CornerRadius(24), new ScaleTransform());
        var sliding = CreateShape(Color.FromRgb(255, 190, 120), new CornerRadius(4), new TranslateTransform());

        Loop(rotating, TimeSpan.FromSeconds(3), new LinearEasing(), PlaybackDirection.Normal,
            (RotateTransform.AngleProperty, 0d, 360d));

        Loop(pulsing, TimeSpan.FromSeconds(1.2), new CubicEaseInOut(), PlaybackDirection.Alternate,
            (ScaleTransform.ScaleXProperty, 0.55d, 1d),
            (ScaleTransform.ScaleYProperty, 0.55d, 1d));

        Loop(sliding, TimeSpan.FromSeconds(1.8), new SineEaseInOut(), PlaybackDirection.Alternate,
            (TranslateTransform.XProperty, -26d, 26d));

        return new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Spacing(28)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .Height(64)
            .Children(rotating, pulsing, sliding);
    }

    private static Border CreateShape(Color color, CornerRadius cornerRadius, ITransform transform)
        => new Border()
            .Width(46)
            .Height(46)
            .VerticalAlignment(VerticalAlignment.Center)
            .Background(new SolidColorBrush(color))
            .CornerRadius(cornerRadius)
            .RenderTransform(transform);

    private Control BuildTransitionedBar()
        => new ProgressBar()
            .Ref(out transitionedBar)
            .Minimum(0)
            .Maximum(100)
            .Value(0)
            .Transitions(
            [
                new DoubleTransition
                {
                    Property = RangeBase.ValueProperty,
                    Duration = TimeSpan.FromSeconds(0.9),
                    Easing = new CubicEaseInOut()
                }
            ]);

    private Control BuildPulsingPanel()
        => new Border()
            .Ref(out pulsingPanel)
            .Height(44)
            .Opacity(0.35)
            .Background(new SolidColorBrush(Color.FromRgb(60, 60, 90)))
            .Transitions(
            [
                new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromSeconds(0.6) },
                new BrushTransition { Property = Border.BackgroundProperty, Duration = TimeSpan.FromSeconds(0.6) }
            ])
            .Child(
                new TextBlock()
                    .Text("Opacity and brush transitions")
                    .Classes(ExamplesTheme.CaptionClass)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Center));

    private Control BuildRotatingContent()
        => new TransitioningContentControl()
            .Ref(out rotatingContent)
            .Height(34)
            .HorizontalContentAlignment(HorizontalAlignment.Center)
            .VerticalContentAlignment(VerticalAlignment.Center)
            .PageTransition(new CrossFade(TimeSpan.FromSeconds(0.5)))
            .Content(
                new TextBlock()
                    .Text(RotatingMessages[0])
                    .HorizontalAlignment(HorizontalAlignment.Center));

    /// <summary>Runs a looping keyframe animation between the given start and end values.</summary>
    /// <remarks>
    /// Transform properties are animated against the control, not its <c>RenderTransform</c>: Avalonia's
    /// transform animator resolves the transform itself, and handing it one directly throws.
    /// </remarks>
    private static void Loop(
        Animatable target,
        TimeSpan duration,
        Easing easing,
        PlaybackDirection direction,
        params (AvaloniaProperty Property, double From, double To)[] properties)
    {
        var start = new KeyFrame { Cue = new Cue(0d) };
        var end = new KeyFrame { Cue = new Cue(1d) };

        foreach (var (property, from, to) in properties)
        {
            start.Setters.Add(new Setter(property, from));
            end.Setters.Add(new Setter(property, to));
        }

        var animation = new Animation
        {
            Duration = duration,
            Easing = easing,
            PlaybackDirection = direction,
            IterationCount = IterationCount.Infinite,
            Children = { start, end }
        };

        // Not awaited: an infinite animation never completes, and it stops when the surface is disposed.
        _ = animation.RunAsync(target);
    }

    private void AppendLog(string entry) => log.Insert(0, entry);

    /// <param name="scale">
    /// Shrinks the icon within <paramref name="bounds"/> without shrinking the bounds themselves - used by
    /// <see cref="DrawIconGlyph"/> to leave room around the icon for its halo.
    /// </param>
    private static void DrawIcon(SeRect bounds, IconShape shape, ColorRgba color, float scale = 1f)
    {
        var center = bounds.Center;
        var unit = Math.Min(bounds.Width, bounds.Height) * scale;

        switch (shape)
        {
            case IconShape.Circle:
                new Circle(center, unit * 0.42f).Draw(color, 0.9f);
                break;

            case IconShape.Square:
                new SeRect(center, new SeSize(unit * 0.72f), new AnchorPoint(0.5f)).Draw(color);
                break;

            case IconShape.Triangle:
                DrawTriangle(center, unit * 0.46f, color);
                break;

            case IconShape.Star:
                MakeStar(center, unit * 0.46f, unit * 0.2f, 5).Draw(color);
                break;

            case IconShape.Ring:
                // DrawLines straddles Radius + lineThickness with a stroke width of lineThickness * 2, so
                // the true outer edge is Radius + 2 * lineThickness - accounted for here, not Radius alone.
                new Circle(center, unit * 0.32f).DrawLines(unit * 0.07f, color, 0.9f);
                break;
        }
    }

    private static void DrawTriangle(SeVec2 center, float radius, ColorRgba color)
    {
        SeVec2 Point(float angle) => center + new SeVec2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

        const float top = -MathF.PI / 2f;
        new Triangle(Point(top), Point(top + MathF.Tau / 3f), Point(top + 2f * MathF.Tau / 3f)).Draw(color);
    }

    /// <summary>Builds an n-pointed star as a polygon, alternating between the outer and inner radius.</summary>
    private static Polygon MakeStar(SeVec2 center, float outerRadius, float innerRadius, int points)
    {
        var vertices = new List<SeVec2>(points * 2);
        var step = MathF.PI / points;
        var angle = -MathF.PI / 2f;

        for (var i = 0; i < points * 2; i++)
        {
            var radius = i % 2 == 0 ? outerRadius : innerRadius;
            vertices.Add(center + new SeVec2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
            angle += step;
        }

        return new Polygon(vertices);
    }
}
