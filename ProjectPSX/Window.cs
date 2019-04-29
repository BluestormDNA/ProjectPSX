using ProjectDMG;
using ProjectPSX.Devices;
using System;
using System.Drawing;
using System.Windows.Forms;
using static ProjectPSX.Devices.GPU;

namespace ProjectPSX {
    public class Window : Form {

        public DirectBitmap VRAM = new DirectBitmap(1024, 512);
        private PictureBox pictureBox;

        public Window() {
            this.Text = "ProjectPSX";
            this.AutoSize = true;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;

            pictureBox = new PictureBox();
            pictureBox.Image = VRAM.Bitmap;
            pictureBox.SizeMode = PictureBoxSizeMode.AutoSize;
            pictureBox.Margin = new Padding(0);

            Controls.Add(pictureBox);

            //test
            initVRAM();

            ProjectPSX psx = new ProjectPSX(this);
            psx.POWER_ON();
        }

        public void initVRAM() {
            for (int x = 0; x < 1024; x++) {
                for (int y = 0; y < 512; y++) {
                    VRAM.SetPixel(x, y, 0x0);
                }
            }
        }

        public void update() {
            pictureBox.Refresh();
        }


    }
}