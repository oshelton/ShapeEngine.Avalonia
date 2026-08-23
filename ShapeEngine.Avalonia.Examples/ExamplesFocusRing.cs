using ShapeEngine.Color;
using ShapeEngine.Core.Structs;
using SeRect = ShapeEngine.Geometry.RectDef.Rect;

namespace AvaloniaExamples;

/// <summary>A soft glow drawn around an Avalonia control by ShapeEngine, animated by the game loop.</summary>
/// <remarks>
/// Drawn outside Avalonia for the reason <see cref="ExamplesFpsDisplay"/> is: the game's UI pass runs
/// after every surface has composited, so the glow lands over the panel instead of being clipped by the
/// button it surrounds. That is also what lets it glide - it is not tied to one control's bounds, it eases
/// from wherever it was to wherever focus went, which is the part a theme's ring cannot do.
/// <para>
/// It is fed a screen rectangle and a corner radius per frame and knows nothing about what it is ringing,
/// so the same glow works for any surface: map the control's bounds with <c>AvaloniaSurface.ToScreen</c>
/// and pass them in, or pass null for no focus.
/// </para>
/// </remarks>
public sealed class ExamplesFocusRing
{
    /// <summary>How quickly the glow closes the distance to a new target, as a decay rate per second.</summary>
    /// <remarks>Fast enough that a held direction still feels like a direct response, slow enough that the
    /// travel reads as a move from one button to the next rather than a cut.</remarks>
    private const float GlideSpeed = 22f;

    /// <summary>How quickly the glow fades in and out, as a fraction per second.</summary>
    private const float FadeSpeed = 7f;

    /// <summary>Pulses per second.</summary>
    private const float PulseRate = 0.8f;

    /// <summary>Strokes the glow is built from, drawn one outside the next.</summary>
    /// <remarks>
    /// A halo rather than a line: one thick translucent stroke reads as a fat border, where several thin
    /// ones falling off in alpha read as light coming off the control. Each follows the control's own
    /// corner radius, grown by its own distance out, so the whole thing stays concentric with the button
    /// instead of rounding at some radius of its own.
    /// </remarks>
    private const int Layers = 5;

    /// <summary>Alpha of the innermost stroke, which the rest fall away from.</summary>
    private const float InnerAlpha = 120f;

    // Everything below is a fraction of the window's shorter side, so the glow keeps its proportions at
    // every window size the way the rest of the examples' engine-drawn chrome does.

    /// <summary>Gap between the control's edge and the innermost stroke.</summary>
    private const float PaddingFraction = 0.002f;

    /// <summary>Extra padding the glow arrives with and settles out of.</summary>
    private const float ArrivalFraction = 0.010f;

    /// <summary>Distance between one stroke and the next, and how thick each is drawn.</summary>
    /// <remarks>Thickness runs over the spacing on purpose, so neighbouring strokes overlap and the
    /// falloff comes out smooth rather than as a set of concentric lines.</remarks>
    private const float StepFraction = 0.0022f;
    private const float ThicknessFraction = 0.0030f;

    /// <summary>How far the pulse pushes the glow out, on top of everything else.</summary>
    private const float BreathFraction = 0.0016f;

    /// <summary>Segments each rounded corner is drawn with.</summary>
    private const int RoundSegments = 12;

    /// <summary>The amber the drawn cursor uses, so the two pieces of engine-drawn chrome read as one set.</summary>
    private static readonly ColorRgba GlowColor = new(255, 170, 90, 255);

    private SeRect current;
    private float currentRadius;
    private float presence;
    private float time;

    /// <summary>Advances the glow towards <paramref name="target"/>, or fades it out when there is none.</summary>
    /// <param name="target">The focused control's rectangle in screen coordinates.</param>
    /// <param name="cornerRadius">The control's own corner radius, in the same screen units.</param>
    public void Update(float dt, SeRect? target, float cornerRadius = 0f)
    {
        time += dt;

        if (target is not { } rect)
        {
            presence = MathF.Max(0f, presence - dt * FadeSpeed);
            return;
        }

        // A glow that is not on screen has nothing to glide from: starting at the target makes the first
        // appearance a fade in where a glide would sweep across the grid from wherever focus last was.
        var glide = presence <= 0f ? 1f : 1f - MathF.Exp(-GlideSpeed * dt);

        current = current.Lerp(rect, glide);
        currentRadius += (cornerRadius - currentRadius) * glide;
        presence = MathF.Min(1f, presence + dt * FadeSpeed);
    }

    /// <summary>Forgets the glow, so the next focus fades in rather than gliding from an old position.</summary>
    public void Reset() => presence = 0f;

    /// <summary>Draws the glow. Call from the game's UI pass, which runs over the surfaces.</summary>
    public void Draw(ScreenInfo ui)
    {
        if (presence <= 0f) return;

        var reference = ui.Area.Size.Min();

        // Smoothstepped, so the fade eases off at both ends instead of stopping dead at full presence.
        var eased = presence * presence * (3f - 2f * presence);
        var pulse = 0.5f + 0.5f * MathF.Sin(time * MathF.Tau * PulseRate);

        var padding = reference * (PaddingFraction + BreathFraction * pulse + (1f - eased) * ArrivalFraction);
        var step = reference * StepFraction;
        var thickness = reference * ThicknessFraction;

        for (var layer = 0; layer < Layers; layer++)
        {
            // Squared falloff: the innermost stroke carries the glow and the outer ones only have to
            // suggest it, which is what keeps the edge soft instead of ending on a visible line.
            var falloff = 1f - layer / (float)Layers;
            var alpha = (int)(InnerAlpha * falloff * falloff * eased * (0.55f + 0.45f * pulse));

            if (alpha <= 0) continue;

            var offset = padding + step * layer;
            var rect = current.ChangeSize(offset * 2f, AnchorPoint.Center);

            rect.DrawLinesRounded(thickness, GlowColor.ChangeAlpha(alpha), Roundness(rect, currentRadius + offset), RoundSegments);
        }
    }

    /// <summary>Converts a corner radius in screen units to the normalized roundness raylib takes.</summary>
    /// <remarks>Raylib measures roundness against half the rectangle's shorter side, so the same radius is
    /// a different number on every rectangle - and anything past 1 is a capsule rather than a rounded
    /// rectangle, hence the clamp.</remarks>
    private static float Roundness(SeRect rect, float radius)
    {
        var shorter = MathF.Min(rect.Width, rect.Height);

        return shorter <= 0f ? 0f : MathF.Min(1f, radius * 2f / shorter);
    }
}
