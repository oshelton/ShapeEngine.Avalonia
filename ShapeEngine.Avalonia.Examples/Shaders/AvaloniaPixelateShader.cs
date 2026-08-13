using Raylib_cs;
using ShapeEngine.Screen;

namespace AvaloniaExamples.Shaders;

/// <summary>
/// Snaps the surface onto a blocky pixel grid - a static effect, so <see cref="Update"/> only ever feeds
/// it strength and size, never an animation clock.
/// </summary>
/// <remarks>
/// Written against the uniforms raylib binds for a screen-space pass, where <c>texture0</c> is the
/// surface exactly as Avalonia rendered it. Strength controls the block size rather than a blend amount:
/// at zero, every texel is its own block, so the render is untouched.
/// </remarks>
public static class AvaloniaPixelateShader
{
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

        void main()
        {
            float blockSize = mix(1.0, 22.0, strength);
            vec2 blocks = max(resolution / blockSize, vec2(1.0));
            vec2 uv = (floor(fragTexCoord * blocks) + 0.5) / blocks;

            finalColor = texture(texture0, uv) * colDiffuse * fragColor;
        }
        """;

    /// <summary>Compiles the shader, returning null if the driver rejects it.</summary>
    public static ShapeShader? Load()
    {
        var shader = Raylib.LoadShaderFromMemory(null, Source);

        return Raylib.IsShaderValid(shader) ? new ShapeShader(shader) : null;
    }

    /// <summary>Feeds the shader this frame's strength and surface size.</summary>
    public static void Update(ShapeShader shader, float elapsed, float strength, int width, int height)
    {
        ShapeShader.SetValueFloat(shader.Shader, "strength", strength);
        ShapeShader.SetValueVector2(shader.Shader, "resolution", width, height);
    }
}
