using System;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.Drawing;

namespace ProjectPSX {
    class Emu {
        private GameWindow window;

        public Emu(GameWindow window) {
            this.window = window;
            window.Load += Window_Load;
            window.RenderFrame += Window_RenderFrame;
            window.UpdateFrame += Window_UpdateFrame;
            window.Closing += Window_Closing;
        }

        private void Window_Load(object sender, EventArgs e) {
            GL.ClearColor(Color.FromArgb(100, 105, 125));
            ProjectPSX psx = new ProjectPSX();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            //
        }

        private void Window_UpdateFrame(object sender, FrameEventArgs e) {
            //Console.WriteLine("Update");
        }

        private void Window_RenderFrame(object sender, FrameEventArgs e) {
            GL.Clear(ClearBufferMask.ColorBufferBit);
            //
            //GL.Enable(EnableCap.Blend);
            //GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            GL.Begin(PrimitiveType.Triangles);

            GL.Color3(Color.Red);
            GL.Vertex2(-0.5, -0.5);
            GL.Color3(Color.Blue);
            GL.Vertex2(0.5, -0.5);
            GL.Color3(Color.Green);
            GL.Vertex2(0, 0.5);

            GL.End();
            //
            GL.Flush();
            window.SwapBuffers();
        }

    }
}
