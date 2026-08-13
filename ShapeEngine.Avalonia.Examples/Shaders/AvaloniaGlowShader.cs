using Raylib_cs;
using ShapeEngine.Color;
using ShapeEngine.Screen;

namespace AvaloniaExamples.Shaders;

/// <summary>
/// Blooms the brighter parts of the surface outward into a soft, slowly pulsing halo.
/// </summary>
/// <remarks>
/// Written against the uniforms raylib binds for a screen-space pass, where <c>texture0</c> is the
/// surface exactly as Avalonia rendered it. Avalonia's output is premultiplied, so the blur is taken over
/// premultiplied samples and the halo raises alpha as well as colour - without that it would have nothing
/// to show up against where the panel is transparent.
/// <para>
/// Sampled over a golden-angle disc rather than concentric rings. Against thin, high-contrast text a ring
/// of a dozen taps reads as that many offset copies rather than as a blur, and no amount of weighting
/// hides it - the taps have to not share an angle in the first place.
/// </para>
/// </remarks>
public static class AvaloniaGlowShader
{
    /// <summary>The colour the halo is tinted towards.</summary>
    public static readonly ColorRgba Tint = new(255, 205, 130, 255);

    private const string Source =
        """
        #version 330

        in vec2 fragTexCoord;
        in vec4 fragColor;

        out vec4 finalColor;

        uniform sampler2D texture0;
        uniform vec4 colDiffuse;

        uniform float time;
        uniform float strength;
        uniform vec2 resolution;
        uniform vec4 tintColor;

        const int TAPS = 48;

        // Consecutive taps land 137.5 degrees apart, which never repeats into rings or spokes.
        const float GOLDEN_ANGLE = 2.39996323;

        void main()
        {
            vec4 base = texture(texture0, fragTexCoord);

            float radius = 16.0 * strength * (0.85 + 0.15 * sin(time * 2.0));
            vec2 texel = radius / resolution;

            vec3 bloom = vec3(0.0);
            float bloomAlpha = 0.0;
            float total = 0.0;

            for (int i = 0; i < TAPS; i++)
            {
                float t = (float(i) + 0.5) / float(TAPS);

                // sqrt spreads the taps evenly over the disc's area rather than bunching them at the
                // centre, so the falloff comes from the weight below instead of from tap density.
                float angle = float(i) * GOLDEN_ANGLE;
                vec2 offset = vec2(cos(angle), sin(angle)) * sqrt(t);

                vec4 s = texture(texture0, fragTexCoord + offset * texel);
                float weight = exp(-2.5 * t);

                // Thresholded per tap rather than once over the blurred result: blurring first drags the
                // card's dark fill into the halo and muddies it.
                float luma = dot(s.rgb, vec3(0.299, 0.587, 0.114));
                float highlight = smoothstep(0.25, 0.9, luma);

                bloom += s.rgb * highlight * weight;
                bloomAlpha += s.a * highlight * weight;
                total += weight;
            }

            bloom /= total;
            bloomAlpha /= total;

            vec3 halo = tintColor.rgb * bloom * strength * 2.4;

            vec4 color = vec4(
                min(base.rgb + halo, vec3(1.0)),
                min(base.a + bloomAlpha * strength, 1.0));

            finalColor = color * colDiffuse * fragColor;
        }
        """;

    /// <summary>Compiles the shader, returning null if the driver rejects it.</summary>
    public static ShapeShader? Load()
    {
        var shader = Raylib.LoadShaderFromMemory(null, Source);

        return Raylib.IsShaderValid(shader) ? new ShapeShader(shader) : null;
    }

    /// <summary>Feeds the shader this frame's animation clock and settings.</summary>
    public static void Update(ShapeShader shader, float elapsed, float strength, int width, int height)
    {
        ShapeShader.SetValueFloat(shader.Shader, "time", elapsed);
        ShapeShader.SetValueFloat(shader.Shader, "strength", strength);
        ShapeShader.SetValueVector2(shader.Shader, "resolution", width, height);
        ShapeShader.SetValueColor(shader.Shader, "tintColor", Tint);
    }
}
