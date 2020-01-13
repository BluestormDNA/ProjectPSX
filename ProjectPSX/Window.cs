using ProjectPSX.Util;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace ProjectPSX {
    public class Window : Form {

        private Size vramSize = new Size(1024, 512);
        private Size _640x480 = new Size(640, 480);
        //private readonly DirectBitmap buffer = new DirectBitmap();
        private readonly DoubleBufferedPanel screen = new DoubleBufferedPanel();

        private Display display = new Display(640, 480);
        private Display vramViewer = new Display(1024, 512);

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

        public Window() {
            Text = "ProjectPSX";
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            KeyUp += new KeyEventHandler(vramViewerToggle);

            screen.BackgroundImage = display.Bitmap;// TESTING
            screen.Size = _640x480;
            screen.Margin = new Padding(0);

            Controls.Add(screen);

            psx = new ProjectPSX(this);
            psx.POWER_ON();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void update(int[] vramBits) {

            if (isVramViewer) {
                Buffer.BlockCopy(vramBits, 0, display.Bits, 0, 0x200000);
            } else if (is24BitDepth) {
                blit24bpp(vramBits);
            } else {
                blit16bpp(vramBits);
            }

            fps++;
            screen.Invalidate();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void blit24bpp(int[] vramBits) {
            int range = (240 - (displayY2 - displayY1)) / 2;

            int yRangeOffset;
            if (range < 0) yRangeOffset = 0;
            else yRangeOffset = range;

            for (int y = yRangeOffset; y < verticalRes - yRangeOffset; y++) {
                int offset = 0;
                for (int x = 0; x < horizontalRes; x += 2) {
                    int p0rgb = vramBits[(offset++ + displayVRAMXStart) + ((y - yRangeOffset  + displayVRAMYStart) * 1024)];
                    int p1rgb = vramBits[(offset++ + displayVRAMXStart) + ((y - yRangeOffset  + displayVRAMYStart) * 1024)];
                    int p2rgb = vramBits[(offset++ + displayVRAMXStart) + ((y - yRangeOffset  + displayVRAMYStart) * 1024)];

                    ushort p0bgr555 = GetPixelBGR555(p0rgb);
                    ushort p1bgr555 = GetPixelBGR555(p1rgb);
                    ushort p2bgr555 = GetPixelBGR555(p2rgb);

                    //[(G0R0][R1)(B0][B1G1)]
                    //   RG    B - R   GB

                    int p0R = p0bgr555 & 0xFF;
                    int p0G = (p0bgr555 >> 8) & 0xFF;
                    int p0B = p1bgr555 & 0xFF;
                    int p1R = (p1bgr555 >> 8) & 0xFF;
                    int p1G = p2bgr555 & 0xFF;
                    int p1B = (p2bgr555 >> 8) & 0xFF;

                    int p0rgb24bpp = p0R << 16 | p0G << 8 | p0B;
                    int p1rgb24bpp = p1R << 16 | p1G << 8 | p1B;

                    display.Bits[x + (y  * horizontalRes)] = p0rgb24bpp;
                    display.Bits[x + 1 + (y * horizontalRes)] = p1rgb24bpp;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void blit16bpp(int[] vramBits) {
            //Console.WriteLine($"x1 {displayX1} x2 {displayX2} y1 {displayY1} y2 {displayY2}");
            //Console.WriteLine($"Display Height {display.Height}  Width {display.Width}");
            int range = (240 - (displayY2 - displayY1)) / 2;

            int yRangeOffset;
            if (range < 0) yRangeOffset = 0;
            else yRangeOffset = range;

            for (int y = yRangeOffset; y < verticalRes - yRangeOffset; y++) {
                for (int x = 0; x < display.Width; x++) {
                    int pixel = vramBits[(x + displayVRAMXStart) + ((y - yRangeOffset + displayVRAMYStart) * 1024)];
                    display.Bits[x + (y * horizontalRes)] = pixel;
                    //Console.WriteLine(y + " " + x);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort GetPixelBGR555(int color) {
            byte m = (byte)((color & 0xFF000000) >> 24);
            byte r = (byte)((color & 0x00FF0000) >> 16 + 3);
            byte g = (byte)((color & 0x0000FF00) >> 8 + 3);
            byte b = (byte)((color & 0x000000FF) >> 3);

            return (ushort)(m << 15 | b << 10 | g << 5 | r);
        }

        public DoubleBufferedPanel getScreen() {
            return screen;
        }

        internal int getFPS() {
            int currentFps = fps;
            fps = 0;
            return currentFps;
        }

        internal void setDisplayMode(int horizontalRes, int verticalRes, bool is24BitDepth) {
            this.is24BitDepth = is24BitDepth;

            if (horizontalRes != this.horizontalRes || verticalRes != this.verticalRes) {
                this.horizontalRes = horizontalRes;
                this.verticalRes = verticalRes;

                //Console.WriteLine($"setDisplayMode {horizontalRes} {verticalRes} {is24BitDepth}");

                if (!isVramViewer) {
                    display = new Display(horizontalRes, verticalRes);
                    screen.BackgroundImage = display.Bitmap;
                }
            }

        }

        internal void setVRAMStart(ushort displayVRAMXStart, ushort displayVRAMYStart) {
            //if (isVramViewer) return;

            this.displayVRAMXStart = displayVRAMXStart;
            this.displayVRAMYStart = displayVRAMYStart;

            //Console.WriteLine($"Vram Start {displayVRAMXStart} {displayVRAMYStart}");
        }

        internal void setVerticalRange(ushort displayY1, ushort displayY2) {
            //if (isVramViewer) return;

            this.displayY1 = displayY1;
            this.displayY2 = displayY2;

            //Console.WriteLine($"Vertical Range {displayY1} {displayY2}");
        }

        internal void setHorizontalRange(ushort displayX1, ushort displayX2) {
            //if (isVramViewer) return;

            this.displayX1 = displayX1;
            this.displayX2 = displayX2;

            //Console.WriteLine($"Horizontal Range {displayX1} {displayX2}");
        }

        private void vramViewerToggle(object sender, KeyEventArgs e) { //this is very buggy but its only for debug purposes maybe disable it when unneded?
            if(e.KeyCode == Keys.Tab) {
                if (!isVramViewer) {
                    display = vramViewer;
                    screen.Size = vramSize;
                } else {
                    display = new Display(horizontalRes, verticalRes);
                    screen.Size = _640x480;
                }
                isVramViewer = !isVramViewer;
                screen.BackgroundImage = display.Bitmap;
            }
        }
    }
}
