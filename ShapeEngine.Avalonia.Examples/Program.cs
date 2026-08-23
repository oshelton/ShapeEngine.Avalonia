using ShapeEngine.Core;
using ShapeEngine.Core.Structs;
using ShapeEngine.Input;

namespace AvaloniaExamples;

public static class Program
{
    // STAThread is required if you deploy using NativeAOT on Windows - See https://github.com/raylib-cs/raylib-cs/issues/301
    [STAThread]
    public static void Main(string[] args)
    {
        // ShapeEngine.Avalonia resolves OpenGL entry points through WGL, so it is Windows only for now.
        var gameSettings = GameSettings.StretchMode("ShapeEngine.Avalonia Examples");

        var windowSettings = new WindowSettings
        {
            Title = "ShapeEngine.Avalonia Examples",
            Topmost = false,
            FullscreenAutoRestoring = true,
            WindowBorder = WindowBorder.Resizabled,
            WindowMinSize = new(1024, 640),
            WindowSize = new(1280, 800),
            Monitor = 0,
            Vsync = VsyncMode.Disabled,
            WindowOpacity = 1f,
            MouseEnabled = true,
            MouseVisible = true,
            Msaa4x = true,
            // Off until a ShapeEngine release carries the GameWindow.MoveMouse fix: before that, a HighDPI
            // window walks the cursor into the corner whenever the keyboard drives Avalonia focus.
            // See https://github.com/DaveGreen-Games/ShapeEngine/pull/180
            HighDPI = true,
            FramebufferTransparent = false
        };

        // Fully unlocked: every cap the engine can apply is switched off. The window's Vsync is already
        // Disabled above, so nothing else is holding the loop back.
        var framerateSettings = new FramerateSettings(
            frameRateLimit: 0,                  // 0 = no global cap (Default caps at 60)
            fixedFramerate: 0,                  // no fixed-timestep update loop
            minFrameRate: 0,                    // no lower bound clamping the values above
            maxFrameRate: 0,                    // no upper bound - Default's 120 lived here
            unfocusedFrameRateLimit: 0,         // do not drop to 30 when the window loses focus
            idleFrameRateLimit: 0,              // do not drop to 30 after a spell without input
            idleTimeThreshold: 0f,              // and do not track idleness at all
            adaptiveFpsLimiterSettings: AdaptiveFpsLimiter.Settings.Disabled,
            maxDeltaTime: 0.25,                 // delta clamping and substepping do not cap the rate;
            minDynamicSubsteppingFramerate: 30, // they only bound how large a single update step gets
            maxDynamicSubsteps: 6);

        var inputSettings = new InputSettings
        (
            new InputSettings.MouseSettings(25, 3, 2, 0.5f, 1f, 0.25f),
            new InputSettings.KeyboardSettings(2, 0.5f, 1f, 2f),
            new InputSettings.GamepadSettings()
        );

        var game = new AvaloniaExamplesGame(gameSettings, windowSettings, framerateSettings, inputSettings);
        game.Run(args);
    }
}
