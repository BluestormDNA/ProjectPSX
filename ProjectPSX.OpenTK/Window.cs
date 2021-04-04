using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL;
using System.Collections.Generic;
using ProjectPSX.Devices.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace ProjectPSX.OpenTK {
    public class Window : GameWindow, IHostWindow {

        const int PSX_MHZ = 33868800;
        const int SYNC_CYCLES = 100;
        const int MIPS_UNDERCLOCK = 2;

        private ProjectPSX psx;
        private int[] displayBuffer;
        private Dictionary<Keys, GamepadInputsEnum> _gamepadKeyMap;
        private AudioPlayer audioPlayer = new AudioPlayer();
        private int vSyncCounter;
        private int cpuCyclesCounter;

        public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings) {
            MakeCurrent();
        }

        private void Window_FileDrop(FileDropEventArgs fileDrop) {
            string[] files = fileDrop.FileNames;
            string file = files[0];
            if(file.EndsWith(".bin") || file.EndsWith(".cue") || file.EndsWith(".exe")) {
                psx = new ProjectPSX(this, file);
            }
        }

        protected override void OnLoad() {
            _gamepadKeyMap = new Dictionary<Keys, GamepadInputsEnum>() {
                { Keys.Space, GamepadInputsEnum.Space},
                { Keys.Z , GamepadInputsEnum.Z },
                { Keys.C , GamepadInputsEnum.C },
                { Keys.Enter , GamepadInputsEnum.Enter },
                { Keys.Up , GamepadInputsEnum.Up },
                { Keys.Right , GamepadInputsEnum.Right },
                { Keys.Down , GamepadInputsEnum.Down },
                { Keys.Left , GamepadInputsEnum.Left },
                { Keys.F1 , GamepadInputsEnum.D1 },
                { Keys.F3 , GamepadInputsEnum.D3 },
                { Keys.Q , GamepadInputsEnum.Q },
                { Keys.E , GamepadInputsEnum.E },
                { Keys.W , GamepadInputsEnum.W },
                { Keys.D , GamepadInputsEnum.D },
                { Keys.S , GamepadInputsEnum.S },
                { Keys.A , GamepadInputsEnum.A },
            };

            FileDrop += Window_FileDrop;

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.Enable(EnableCap.Texture2D);
            GL.ClearColor(1, 1, 1, 1);
        }

        protected override void OnRenderFrame(FrameEventArgs args) {
            //Console.WriteLine(this.RenderFrequency);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            int id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, id);

            GL.TexImage2D(TextureTarget.Texture2D, 0,
                PixelInternalFormat.Rgb,
                1024, 512, 0,
                PixelFormat.Bgra,
                PixelType.UnsignedByte,
                displayBuffer);

            GL.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GL.Begin(PrimitiveType.Quads);
            GL.TexCoord2(0, 1); GL.Vertex2(-1, -1);
            GL.TexCoord2(1, 1); GL.Vertex2(1, -1);
            GL.TexCoord2(1, 0); GL.Vertex2(1, 1);
            GL.TexCoord2(0, 0); GL.Vertex2(-1, 1);
            GL.End();

            GL.DeleteTexture(id);
            SwapBuffers();
            //Console.WriteLine("painting");
        }

        protected override void OnUpdateFrame(FrameEventArgs args) {
            base.OnUpdateFrame(args);
            psx?.RunFrame();
        }

        protected override void OnKeyDown(KeyboardKeyEventArgs e) {
            GamepadInputsEnum? button = GetGamepadButton(e.Key);
            if (button != null)
                psx.JoyPadDown(button.Value);
        }

        protected override void OnKeyUp(KeyboardKeyEventArgs e) {
            GamepadInputsEnum? button = GetGamepadButton(e.Key);
            if (button != null)
                psx.JoyPadUp(button.Value);
        }

        private GamepadInputsEnum? GetGamepadButton(Keys keyCode) {
            if (_gamepadKeyMap.TryGetValue(keyCode, out GamepadInputsEnum gamepadButtonValue))
                return gamepadButtonValue;
            return null;
        }

        public void Render(int[] vram) {
            vSyncCounter++;
            displayBuffer = vram;
        }

        public int GetVPS() {
            int fps = vSyncCounter;
            vSyncCounter = 0;
            return fps;
        }

        public void SetDisplayMode(int horizontalRes, int verticalRes, bool is24BitDepth) {
            //throw new System.NotImplementedException();
        }

        public void SetHorizontalRange(ushort displayX1, ushort displayX2) {
            //throw new System.NotImplementedException();
        }

        public void SetVRAMStart(ushort displayVRAMXStart, ushort displayVRAMYStart) {
            //throw new System.NotImplementedException();
        }

        public void SetVerticalRange(ushort displayY1, ushort displayY2) {
            //throw new System.NotImplementedException();
        }

        public void Play(byte[] samples) {
            audioPlayer.UpdateAudio(samples);
        }
    }
}
