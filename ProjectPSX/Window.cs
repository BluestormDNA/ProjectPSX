using ProjectPSX.Util;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ProjectPSX {
    public class Window : Form {

        private readonly DirectBitmap buffer = new DirectBitmap();
        private readonly DoubleBufferedPanel screen = new DoubleBufferedPanel();

        private ProjectPSX psx;

        public Window() {
            this.Text = "ProjectPSX";
            this.AutoSize = true;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;

            screen.BackgroundImage = buffer.Bitmap;// TESTING
            screen.Size = new Size(1024, 512);
            screen.Margin = new Padding(0);

            Controls.Add(screen);

            psx = new ProjectPSX(this);
            psx.POWER_ON();
        }

        public void update(int[] vramBits) {
            Buffer.BlockCopy(vramBits, 0, buffer.Bits, 0, 0x200000);
            screen.Invalidate();
        }

    }
}