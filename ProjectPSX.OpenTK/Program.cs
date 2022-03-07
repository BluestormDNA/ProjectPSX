using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common;
using System;
using ProjectPSX.Util;

namespace ProjectPSX.OpenTK {
    static class Program {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args) {
            GameWindowSettings settings = new GameWindowSettings();
            settings.RenderFrequency = 60;
            settings.UpdateFrequency = 60;
            NativeWindowSettings nativeWindow = new NativeWindowSettings();
            nativeWindow.API = ContextAPI.OpenGL;
            nativeWindow.Size = new Vector2i(1024, 512);
            nativeWindow.Title = "ProjectPSX";
            nativeWindow.Profile = ContextProfile.Compatability;

            Window window = new Window(settings, nativeWindow);
            window.VSync = VSyncMode.On;

            if (Storage.TryGetExecutable(args, out var path))
            {
                window.SetExecutable(path);
            }

            window.Run();
        }
    }
}
