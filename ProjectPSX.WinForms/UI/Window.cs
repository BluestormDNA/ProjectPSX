using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using NAudio.Wave;
using ProjectPSX.Devices.Input;
using ProjectPSX.Interop.Gdi32;
using ProjectPSX.Util;
using Gdi32 = ProjectPSX.Interop.Gdi32.NativeMethods;

namespace ProjectPSX {
    public class Window : Form, IHostWindow {

        private const int PSX_MHZ = 33868800;
        private const int SYNC_CYCLES = 100;
        private const int MIPS_UNDERCLOCK = 3;

        private const int cyclesPerFrame = PSX_MHZ / 60;
        private const int syncLoops = (cyclesPerFrame / (SYNC_CYCLES * MIPS_UNDERCLOCK)) + 1;
        private const int cycles = syncLoops * SYNC_CYCLES;

        private Size vramSize = new Size(1024, 512);
        private Size _640x480 = new Size(640, 480);
        private readonly DoubleBufferedPanel screen = new DoubleBufferedPanel();

        private GdiBitmap display = new GdiBitmap(1024, 512);

        private ProjectPSX psx;
        private int fps;
        private bool isVramViewer;

        private int horizontalRes;
        private int verticalRes;

        private int displayVRAMXStart;
        private int displayVRAMYStart;

        private bool is24BitDepth;

        private int displayX1;
        private int displayX2;
        private int displayY1;
        private int displayY2;

        private long cpuCyclesCounter;

        Dictionary<Keys, GamepadInputsEnum> _gamepadKeyMap;

        private WaveOutEvent waveOutEvent = new WaveOutEvent();
        private BufferedWaveProvider bufferedWaveProvider = new BufferedWaveProvider(new WaveFormat());

        public Window() {
            Text = "ProjectPSX";
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            KeyUp += new KeyEventHandler(vramViewerToggle);

            screen.Size = _640x480;
            screen.Margin = new Padding(0);
            screen.MouseDoubleClick += new MouseEventHandler(toggleDebug);

            Controls.Add(screen);
            // Debug will crash if not:
            CheckForIllegalCrossThreadCalls = false;

            KeyDown += new KeyEventHandler(handleJoyPadDown);
            KeyUp += new KeyEventHandler(handleJoyPadUp);

            _gamepadKeyMap = new Dictionary<Keys, GamepadInputsEnum>() {
                { Keys.Space, GamepadInputsEnum.Space},
                { Keys.Z , GamepadInputsEnum.Z },
                { Keys.C , GamepadInputsEnum.C },
                { Keys.Enter , GamepadInputsEnum.Enter },
                { Keys.Up , GamepadInputsEnum.Up },
                { Keys.Right , GamepadInputsEnum.Right },
                { Keys.Down , GamepadInputsEnum.Down },
                { Keys.Left , GamepadInputsEnum.Left },
                { Keys.D1 , GamepadInputsEnum.D1 },
                { Keys.D3 , GamepadInputsEnum.D3 },
                { Keys.Q , GamepadInputsEnum.Q },
                { Keys.E , GamepadInputsEnum.E },
                { Keys.W , GamepadInputsEnum.W },
                { Keys.D , GamepadInputsEnum.D },
                { Keys.S , GamepadInputsEnum.S },
                { Keys.A , GamepadInputsEnum.A },
            };

            bufferedWaveProvider.DiscardOnBufferOverflow = true;
            bufferedWaveProvider.BufferDuration = new TimeSpan(0, 0, 0, 0, 300);
            waveOutEvent.Init(bufferedWaveProvider);

            string diskFilename = GetDiskFilename();
            psx = new ProjectPSX(this, diskFilename);

            var timer = new System.Timers.Timer(1000);
            timer.Elapsed += OnTimedEvent;
            timer.Enabled = true;

            RunUncapped();
        }

        private string GetDiskFilename() {
            var cla = Environment.GetCommandLineArgs();
            if (cla.Any(s => s.EndsWith(".bin") || s.EndsWith(".cue") || s.EndsWith(".exe"))) {
                string filename = cla.First(s => s.EndsWith(".bin") || s.EndsWith(".cue") || s.EndsWith(".exe"));
                return filename;
            } else {
                //Show the user a dialog so they can pick the bin they want to load.
                var fileDialog = new OpenFileDialog();
                fileDialog.Filter = "BIN/CUE files or PSXEXEs(*.bin, *.cue, *.exe)|*.bin;*.cue;*.exe";
                fileDialog.ShowDialog();

                string file = fileDialog.FileName;
                return file;
            }
        }

        private void handleJoyPadUp(object sender, KeyEventArgs e) {
            GamepadInputsEnum? button = GetGamepadButton(e.KeyCode);
            if(button != null)
                psx.JoyPadUp(button.Value);
        }

        private GamepadInputsEnum? GetGamepadButton(Keys keyCode) {
            if (_gamepadKeyMap.TryGetValue(keyCode, out GamepadInputsEnum gamepadButtonValue))
                return gamepadButtonValue;
            return null;
        }

        private void handleJoyPadDown(object sender, KeyEventArgs e) {
            GamepadInputsEnum? button = GetGamepadButton(e.KeyCode);
            if (button != null)
                psx.JoyPadDown(button.Value);
        }

        private void toggleDebug(object sender, MouseEventArgs e) => psx.toggleDebug();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Render(int[] vram) {
            //Console.WriteLine($"x1 {displayX1} x2 {displayX2} y1 {displayY1} y2 {displayY2}");

            int horizontalEnd = horizontalRes;
            int verticalEnd = verticalRes;

            if (isVramViewer) {
                horizontalEnd = 1024;
                verticalEnd = 512;

                Marshal.Copy(vram, 0, display.BitmapData, 0x80000);
            } else if (is24BitDepth) {
                DisplayBlitter.Blit24bpp(vram, GetDisplaySpan(), horizontalRes, verticalRes,
                    displayVRAMXStart, displayVRAMYStart, displayY1, displayY2);
            } else {
                DisplayBlitter.Blit16bpp(vram, GetDisplaySpan(), horizontalRes, verticalRes,
                    displayVRAMXStart, displayVRAMYStart, displayY1, displayY2);
            }

            fps++;

            using var deviceContext = new GdiDeviceContext(screen.Handle);

            Gdi32.StretchBlt(deviceContext, 0, 0, screen.Width, screen.Height,
                     display.DeviceContext, 0, 0, horizontalEnd, verticalEnd,
                     RasterOp.SRCCOPY);
        }

        private unsafe Span<int> GetDisplaySpan() => new Span<int>(display.BitmapData.ToPointer(), 0x80000);

        public int GetVPS() {
            int currentFps = fps;
            fps = 0;
            return currentFps;
        }

        public void SetDisplayMode(int horizontalRes, int verticalRes, bool is24BitDepth) {
            this.is24BitDepth = is24BitDepth;

            if (horizontalRes != this.horizontalRes || verticalRes != this.verticalRes) {
                this.horizontalRes = horizontalRes;
                this.verticalRes = verticalRes;

                clearDisplay();
                //Console.WriteLine($"setDisplayMode {horizontalRes} {verticalRes} {is24BitDepth}");
            }

        }

        public void SetVRAMStart(ushort displayVRAMXStart, ushort displayVRAMYStart) {
            this.displayVRAMXStart = displayVRAMXStart;
            this.displayVRAMYStart = displayVRAMYStart;

            //Console.WriteLine($"Vram Start {displayVRAMXStart} {displayVRAMYStart}");
        }

        public void SetVerticalRange(ushort displayY1, ushort displayY2) {
            this.displayY1 = displayY1;
            this.displayY2 = displayY2;

            //Console.WriteLine($"Vertical Range {displayY1} {displayY2}");
        }

        public void SetHorizontalRange(ushort displayX1, ushort displayX2) {
            this.displayX1 = displayX1;
            this.displayX2 = displayX2;

            //Console.WriteLine($"Horizontal Range {displayX1} {displayX2}");
        }

        private void vramViewerToggle(object sender, KeyEventArgs e) {
            if(e.KeyCode == Keys.Tab) {
                if (!isVramViewer) {
                    screen.Size = vramSize;
                } else {
                    screen.Size = _640x480;
                }
                isVramViewer = !isVramViewer;
                clearDisplay();
            }
        }

        private unsafe void clearDisplay() {
            Span<uint> span = new Span<uint>(display.BitmapData.ToPointer(), 0x80000);
            span.Clear();
        }

        public void Play(byte[] samples) {
            bufferedWaveProvider.AddSamples(samples, 0, samples.Length);

            if (waveOutEvent.PlaybackState != PlaybackState.Playing) {
                waveOutEvent.Play();
            }
        }

        private void OnTimedEvent(object sender, ElapsedEventArgs e) {
            Text = $"ProjectPSX | Cpu {(int)((float)cpuCyclesCounter / (PSX_MHZ / MIPS_UNDERCLOCK) * SYNC_CYCLES)}% | Vps {GetVPS()}";
            cpuCyclesCounter = 0;
        }

        public void RunUncapped() {
            Task t = Task.Factory.StartNew(EXECUTE, TaskCreationOptions.LongRunning);
        }

        private void EXECUTE() {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;

            try {
                while (true) {
                    psx.RunFrame();
                    cpuCyclesCounter += cycles;
                }
            } catch (Exception e) {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
