using OpenToolkit.Graphics.OpenGL;
using OpenToolkit.Mathematics;
using OpenToolkit.Windowing.Desktop;
using System;

namespace ProjectPSX.OpenTK {
    static class Program {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            GameWindowSettings settings = new GameWindowSettings();
            settings.RenderFrequency = 60;
            settings.UpdateFrequency = 60;
            NativeWindowSettings nativeWindow = new NativeWindowSettings();
            nativeWindow.API = OpenToolkit.Windowing.Common.ContextAPI.OpenGL;
            nativeWindow.APIVersion = new Version(3, 2);
            nativeWindow.Size = new Vector2i(1024, 512);
            nativeWindow.Title = "ProjectPSX";
            nativeWindow.Profile = OpenToolkit.Windowing.Common.ContextProfile.Compatability;

            Window window = new Window(settings, nativeWindow);
            window.VSync = OpenToolkit.Windowing.Common.VSyncMode.On;
            window.Run();
        }
    }
}
