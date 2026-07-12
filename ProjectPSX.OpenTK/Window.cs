using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using ProjectPSX.Devices.Input;

namespace ProjectPSX.OpenTK {
    public class Window : GameWindow, IHostWindow {

        private const string VertexShaderSource = @"
            #version 330 core
            uniform vec2 uUvScale;
            out vec2 vUv;
            void main() {
                vec2 corner = vec2(gl_VertexID & 1, gl_VertexID >> 1);
                gl_Position = vec4(corner * 2.0 - 1.0, 0.0, 1.0);
                vUv = vec2(corner.x, 1.0 - corner.y) * uUvScale;
            }";

        private const string FragmentShaderSource = @"
            #version 330 core
            uniform sampler2D uDisplay;
            in vec2 vUv;
            out vec4 fragColor;
            void main() {
                fragColor = vec4(texture(uDisplay, vUv).rgb, 1.0);
            }";

        private ProjectPSX psx;
        private readonly string bootFile;

        private readonly int[] displayBuffer = new int[1024 * 512];
        private readonly AudioPlayer audioPlayer = new AudioPlayer();

        private readonly Dictionary<Keys, GamepadInputsEnum> _gamepadKeyMap = new Dictionary<Keys, GamepadInputsEnum>() {
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

        private int vSyncCounter;
        private double titleElapsed;

        private bool isVramViewer;
        private bool isFastForward;

        private int horizontalRes = 320;
        private int verticalRes = 240;
        private bool is24BitDepth;
        private int displayVRAMXStart;
        private int displayVRAMYStart;
        private int displayX1;
        private int displayX2;
        private int displayY1;
        private int displayY2;

        private int shaderProgram;
        private int uvScaleLocation;
        private int vao;
        private int texture;

        public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings, string bootFile)
            : base(gameWindowSettings, nativeWindowSettings) {
            this.bootFile = bootFile;
        }

        public static bool IsPsxFile(string file) {
            return file.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".cue", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        }

        private void Window_FileDrop(FileDropEventArgs fileDrop) {
            string file = fileDrop.FileNames[0];
            if (IsPsxFile(file)) {
                psx = new ProjectPSX(this, file);
            }
        }

        protected override void OnLoad() {
            base.OnLoad();

            FileDrop += Window_FileDrop;

            shaderProgram = LinkProgram(
                CompileShader(ShaderType.VertexShader, VertexShaderSource),
                CompileShader(ShaderType.FragmentShader, FragmentShaderSource));
            uvScaleLocation = GL.GetUniformLocation(shaderProgram, "uUvScale");

            //A VAO is mandatory on core profile even if the quad is generated from gl_VertexID
            vao = GL.GenVertexArray();

            texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8,
                1024, 512, 0, PixelFormat.Bgra, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            GL.ClearColor(0, 0, 0, 1);
            UpdateViewport();

            if (bootFile != null) {
                psx = new ProjectPSX(this, bootFile);
            } else {
                Title = "ProjectPSX | Drop a .bin, .cue or .exe file to boot";
            }
        }

        protected override void OnUnload() {
            GL.DeleteTexture(texture);
            GL.DeleteVertexArray(vao);
            GL.DeleteProgram(shaderProgram);
            audioPlayer.Dispose();
            base.OnUnload();
        }

        protected override void OnUpdateFrame(FrameEventArgs args) {
            base.OnUpdateFrame(args);
            psx?.RunFrame();

            titleElapsed += args.Time;
            if (titleElapsed >= 1 && psx != null) {
                titleElapsed = 0;
                Title = $"ProjectPSX | Vps {GetVPS()}{(isFastForward ? " | FF" : "")}";
            }
        }

        protected override void OnRenderFrame(FrameEventArgs args) {
            base.OnRenderFrame(args);

            GL.Clear(ClearBufferMask.ColorBufferBit);

            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0,
                1024, 512, PixelFormat.Bgra, PixelType.UnsignedByte, displayBuffer);

            float uvScaleX = isVramViewer ? 1f : horizontalRes / 1024f;
            float uvScaleY = isVramViewer ? 1f : verticalRes / 512f;

            GL.UseProgram(shaderProgram);
            GL.Uniform2(uvScaleLocation, uvScaleX, uvScaleY);
            GL.BindVertexArray(vao);
            GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

            SwapBuffers();
        }

        protected override void OnFramebufferResize(FramebufferResizeEventArgs e) {
            base.OnFramebufferResize(e);
            UpdateViewport();
        }

        //Letterboxes the viewport so the output keeps its aspect ratio on any window size
        private void UpdateViewport() {
            float targetAspect = isVramViewer ? 1024f / 512f : 4f / 3f;
            int width = FramebufferSize.X;
            int height = FramebufferSize.Y;
            if (height == 0) return;

            int viewportWidth = width;
            int viewportHeight = (int)(width / targetAspect);
            if (viewportHeight > height) {
                viewportHeight = height;
                viewportWidth = (int)(height * targetAspect);
            }
            GL.Viewport((width - viewportWidth) / 2, (height - viewportHeight) / 2, viewportWidth, viewportHeight);
        }

        protected override void OnKeyDown(KeyboardKeyEventArgs e) {
            base.OnKeyDown(e);
            if (e.IsRepeat) return;

            switch (e.Key) {
                case Keys.Tab:
                    isVramViewer = !isVramViewer;
                    Array.Clear(displayBuffer, 0, displayBuffer.Length);
                    UpdateViewport();
                    return;
                case Keys.F2:
                    isFastForward = !isFastForward;
                    audioPlayer.fastForward = isFastForward;
                    UpdateFrequency = isFastForward ? 0 : 60;
                    VSync = isFastForward ? VSyncMode.Off : VSyncMode.On;
                    return;
            }

            GamepadInputsEnum? button = GetGamepadButton(e.Key);
            if (button != null)
                psx?.JoyPadDown(button.Value);
        }

        protected override void OnKeyUp(KeyboardKeyEventArgs e) {
            base.OnKeyUp(e);
            GamepadInputsEnum? button = GetGamepadButton(e.Key);
            if (button != null)
                psx?.JoyPadUp(button.Value);
        }

        private GamepadInputsEnum? GetGamepadButton(Keys keyCode) {
            if (_gamepadKeyMap.TryGetValue(keyCode, out GamepadInputsEnum gamepadButtonValue))
                return gamepadButtonValue;
            return null;
        }

        public void Render(int[] vram) {
            vSyncCounter++;

            if (isVramViewer) {
                Array.Copy(vram, displayBuffer, displayBuffer.Length);
            } else if (is24BitDepth) {
                DisplayBlitter.Blit24bpp(vram, displayBuffer, horizontalRes, verticalRes,
                    displayVRAMXStart, displayVRAMYStart, displayY1, displayY2);
            } else {
                DisplayBlitter.Blit16bpp(vram, displayBuffer, horizontalRes, verticalRes,
                    displayVRAMXStart, displayVRAMYStart, displayY1, displayY2);
            }
        }

        public int GetVPS() {
            int fps = vSyncCounter;
            vSyncCounter = 0;
            return fps;
        }

        public void SetDisplayMode(int horizontalRes, int verticalRes, bool is24BitDepth) {
            this.is24BitDepth = is24BitDepth;

            if (horizontalRes != this.horizontalRes || verticalRes != this.verticalRes) {
                this.horizontalRes = horizontalRes;
                this.verticalRes = verticalRes;

                Array.Clear(displayBuffer, 0, displayBuffer.Length);
            }
        }

        public void SetVRAMStart(ushort displayVRAMXStart, ushort displayVRAMYStart) {
            this.displayVRAMXStart = displayVRAMXStart;
            this.displayVRAMYStart = displayVRAMYStart;
        }

        public void SetHorizontalRange(ushort displayX1, ushort displayX2) {
            this.displayX1 = displayX1;
            this.displayX2 = displayX2;
        }

        public void SetVerticalRange(ushort displayY1, ushort displayY2) {
            this.displayY1 = displayY1;
            this.displayY2 = displayY2;
        }

        public void Play(byte[] samples) {
            audioPlayer.UpdateAudio(samples);
        }

        private static int CompileShader(ShaderType type, string source) {
            int shader = GL.CreateShader(type);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
            if (status == 0) {
                throw new Exception($"{type} compilation failed: {GL.GetShaderInfoLog(shader)}");
            }
            return shader;
        }

        private static int LinkProgram(int vertexShader, int fragmentShader) {
            int program = GL.CreateProgram();
            GL.AttachShader(program, vertexShader);
            GL.AttachShader(program, fragmentShader);
            GL.LinkProgram(program);
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int status);
            if (status == 0) {
                throw new Exception($"Shader program link failed: {GL.GetProgramInfoLog(program)}");
            }
            GL.DetachShader(program, vertexShader);
            GL.DetachShader(program, fragmentShader);
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);
            return program;
        }
    }
}
