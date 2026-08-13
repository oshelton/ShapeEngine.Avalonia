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
            HighDPI = false,
            FramebufferTransparent = false
        };

        var framerateSettings = FramerateSettings.Default;

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
