using ProjectPSX.Util;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ProjectPSX {
    public class Window : Form {

        //private readonly DirectBitmap buffer = new DirectBitmap();
        private readonly DoubleBufferedPanel screen = new DoubleBufferedPanel();

        private Display display = new Display(640, 480);

        private ProjectPSX psx;
        private int fps;
        private bool vramViewer;

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
            this.Text = "ProjectPSX";
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.vramViewerToggle);

            screen.BackgroundImage = display.Bitmap;// TESTING
            screen.Size = new Size(640, 480);
            screen.Margin = new Padding(0);

            Controls.Add(screen);

            psx = new ProjectPSX(this);
            psx.POWER_ON();
        }

        public void update(int[] vramBits) {

            if (vramViewer) {
                Buffer.BlockCopy(vramBits, 0, display.Bits, 0, 0x200000);
            } else if (is24BitDepth) { //pretty much hacked from our current limitations on vram...
                blit24bpp(vramBits);
            } else {
                blit16bpp(vramBits);
            }

            fps++;
            screen.Invalidate();
        }

        private void blit24bpp(int[] vramBits) {
            for (int y = 0; y < verticalRes; y++) {
                int offset = 0;
                for (int x = 0; x < horizontalRes; x += 2) {
                    int p0rgb = vramBits[(offset++ + displayVRAMXStart) + ((y + displayVRAMYStart) * 1024)];
                    int p1rgb = vramBits[(offset++ + displayVRAMXStart) + ((y + displayVRAMYStart) * 1024)];
                    int p2rgb = vramBits[(offset++ + displayVRAMXStart) + ((y + displayVRAMYStart) * 1024)];

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

                    display.Bits[x + (y * horizontalRes)] = p0rgb24bpp;
                    display.Bits[x + 1 + (y * horizontalRes)] = p1rgb24bpp;
                }
            }
        }

        private void blit16bpp(int[] vramBits) {
            for (int y = 0; y < display.Height; y++) {
                for (int x = 0; x < display.Width; x++) {
                    int pixel = vramBits[(x + displayVRAMXStart) + ((y + displayVRAMYStart) * 1024)];
                    display.Bits[x + (y * horizontalRes)] = pixel;
                    //Console.WriteLine(y + " " + x);
                }
            }
        }

        public ushort GetPixelBGR555(int color) {
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

                if (!vramViewer) {
                    display = new Display(horizontalRes, verticalRes);
                    screen.BackgroundImage = display.Bitmap;
                }
            }
        }

        internal void setVRAMStart(ushort displayVRAMXStart, ushort displayVRAMYStart) {
            if (vramViewer) return;

            this.displayVRAMXStart = displayVRAMXStart;
            this.displayVRAMYStart = displayVRAMYStart;

            //Console.WriteLine("Vram Start " + displayVRAMXStart + " " + displayVRAMYStart);
        }

        internal void setVerticalRange(ushort displayY1, ushort displayY2) {
            if (vramViewer) return;

            this.displayY1 = displayY1;
            this.displayY2 = displayY2;

            //Console.WriteLine("Vertical Range " + displayY1 + " " + displayY2);
        }

        internal void setHorizontalRange(ushort displayX1, ushort displayX2) {
            if (vramViewer) return;

            this.displayX1 = displayX1;
            this.displayX2 = displayX2;

            //Console.WriteLine("Horizontal Range " + displayX1 + " " + displayX2);
        }

        private void vramViewerToggle(object sender, KeyEventArgs e) { //this is very buggy but its only for debug purposes maybe disable it when unneded?
            if(e.KeyCode == Keys.Tab) {
                if (!vramViewer) {
                    display = new Display(1024, 512);
                    screen.Size = new Size(1024, 512);
                } else {
                    display = new Display(horizontalRes, verticalRes);
                    screen.Size = new Size(640, 480);
                }
                vramViewer = !vramViewer;
                screen.BackgroundImage = display.Bitmap;
            }
        }
    }
}