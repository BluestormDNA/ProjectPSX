using ProjectDMG;
using ProjectPSX.Util;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ProjectPSX {
    public class Window : Form {

        public readonly DirectBitmap VRAM = new DirectBitmap(1024, 512);
        private readonly DirectBitmap buffer = new DirectBitmap(1024, 512);
        private readonly DoubleBufferedPanel screen = new DoubleBufferedPanel();

        ProjectPSX psx;

        public Window() {
            this.Text = "ProjectPSX";
            this.AutoSize = true;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;

            screen.BackgroundImage = buffer.Bitmap;// TESTING
            screen.Size = new Size(1024, 512);
            screen.Margin = new Padding(0);

            Controls.Add(screen);

            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            this.SetStyle(ControlStyles.UserPaint, false);
            
            this.UpdateStyles();

            //test
            //initVRAM();

            psx = new ProjectPSX(this);
            psx.POWER_ON();
        }

        public void initVRAM() {
            for (int x = 0; x < 1024; x++) {
                for (int y = 0; y < 512; y++) {
                    VRAM.SetPixel(x, y, 0x00FFFFFF);
                }
            }
        }

        public void update() {
            //Array.Copy(VRAM.Bits, buffer.Bits, 0x80000); // tests needed to determine fastest
            Buffer.BlockCopy(VRAM.Bits, 0, buffer.Bits, 0, 0x200000);
            screen.Invalidate();
        }
    }
}