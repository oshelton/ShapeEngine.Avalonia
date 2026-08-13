using Raylib_cs;
using ShapeEngine.Color;
using ShapeEngine.Screen;

namespace AvaloniaExamples.Shaders;

/// <summary>
/// A CRT monitor effect: barrel distortion curves the surface like old curved glass, a vignette darkens
/// the edges, and phosphor scanlines run across it.
/// </summary>
/// <remarks>
/// Written against the uniforms raylib binds for a screen-space pass, where <c>texture0</c> is the
/// surface exactly as Avalonia rendered it. Distorted UVs that land outside the surface sample nothing and
/// go fully transparent, which is what gives the curved-glass silhouette at the corners.
/// </remarks>
public static class AvaloniaCrtShader
{
    /// <summary>The colour the phosphor tint pulls the surface towards.</summary>
    public static readonly ColorRgba Tint = new(150, 255, 190, 255);

    private const string Source =
        """
        #version 330

        in vec2 fragTexCoord;
        in vec4 fragColor;

        out vec4 finalColor;

        uniform sampler2D texture0;
        uniform vec4 colDiffuse;

        uniform float strength;
        uniform vec2 resolution;
        uniform vec4 tintColor;

        void main()
        {
            vec2 centered = fragTexCoord * 2.0 - 1.0;

            float curve = 0.18 * strength;
            vec2 distorted = centered * (1.0 + curve * dot(centered, centered));
            vec2 uv = distorted * 0.5 + 0.5;

            if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
            {
                finalColor = vec4(0.0);
                return;
            }

            vec4 color = texture(texture0, uv);

            float scanline = 0.7 + 0.3 * sin(uv.y * resolution.y * 3.14159265);
            color.rgb *= mix(1.0, scanline, 0.65 * strength);

            float vignette = 1.0 - dot(centered, centered) * 0.5 * strength;
            color.rgb *= vignette;

            color.rgb = mix(color.rgb, color.rgb * tintColor.rgb, 0.45 * strength);

            finalColor = color * colDiffuse * fragColor;
        }
        """;

    /// <summary>Compiles the shader, returning null if the driver rejects it.</summary>
    public static ShapeShader? Load()
    {
        var shader = Raylib.LoadShaderFromMemory(null, Source);

        return Raylib.IsShaderValid(shader) ? new ShapeShader(shader) : null;
    }

    /// <summary>Feeds the shader this frame's strength and surface size. The effect itself doesn't animate.</summary>
    public static void Update(ShapeShader shader, float elapsed, float strength, int width, int height)
    {
        ShapeShader.SetValueFloat(shader.Shader, "strength", strength);
        ShapeShader.SetValueVector2(shader.Shader, "resolution", width, height);
        ShapeShader.SetValueColor(shader.Shader, "tintColor", Tint);
    }
}
