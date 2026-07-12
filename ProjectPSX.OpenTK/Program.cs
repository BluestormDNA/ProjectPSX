using System;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace ProjectPSX.OpenTK {
    static class Program {
        /// <summary>
        ///  The main entry point for the application.
        ///  A bin/cue/exe can be passed as an argument, otherwise drop one on the window.
        /// </summary>
        static void Main(string[] args) {
            string bootFile = null;
            foreach (string arg in args) {
                if (Window.IsPsxFile(arg)) {
                    bootFile = arg;
                    break;
                }
            }

            GameWindowSettings settings = new GameWindowSettings();
            settings.UpdateFrequency = 60;

            NativeWindowSettings nativeWindow = new NativeWindowSettings();
            nativeWindow.API = ContextAPI.OpenGL;
            nativeWindow.APIVersion = new Version(3, 3);
            nativeWindow.Profile = ContextProfile.Core;
            nativeWindow.Flags = ContextFlags.ForwardCompatible; //Required by macOS
            nativeWindow.ClientSize = new Vector2i(800, 600);
            nativeWindow.Title = "ProjectPSX";

            using Window window = new Window(settings, nativeWindow, bootFile);
            window.VSync = VSyncMode.On;
            window.Run();
        }
    }
}
