using Raylib_cs;
using ShapeEngine.Color;
using ShapeEngine.Screen;

namespace AvaloniaExamples.Shaders;

/// <summary>
/// A hologram effect built from a travelling horizontal wobble, a chromatic split and scanlines.
/// </summary>
/// <remarks>
/// Written against the uniforms raylib binds for a screen-space pass, where <c>texture0</c> is the
/// surface exactly as Avalonia rendered it. Alpha is carried through from the centre sample, so the
/// panel keeps its translucency and the untouched parts of the surface stay transparent.
/// </remarks>
public static class AvaloniaHologramShader
{
    /// <summary>The colour the effect pulls the surface towards.</summary>
    public static readonly ColorRgba Tint = new(150, 235, 255, 255);

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

        void main()
        {
            vec2 uv = fragTexCoord;
            uv.x += sin(uv.y * 40.0 + time * 3.0) * 0.0015 * strength;

            float split = 0.0035 * strength;
            vec4 center = texture(texture0, uv);
            float r = texture(texture0, uv + vec2(split, 0.0)).r;
            float b = texture(texture0, uv - vec2(split, 0.0)).b;

            vec4 color = vec4(r, center.g, b, center.a);

            float scanline = 0.78 + 0.22 * sin(uv.y * resolution.y * 1.5 + time * 6.0);
            color.rgb *= mix(1.0, scanline, strength);
            color.rgb = mix(color.rgb, color.rgb * tintColor.rgb, strength);

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
