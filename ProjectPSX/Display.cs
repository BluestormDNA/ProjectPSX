using ProjectDMG;
using ProjectPSX.Devices;
using System;
using System.Drawing;
using System.Windows.Forms;
using static ProjectPSX.Devices.GPU;

namespace ProjectPSX {
    public class Display : Form {

        public DirectBitmap VRAM = new DirectBitmap(1024, 512);
        private PictureBox pictureBox;

        public Display() {
            this.Text = "ProjectPSX";
            this.AutoSize = true;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            FormClosing += Display_FormClosing;

            pictureBox = new PictureBox();
            pictureBox.Image = VRAM.Bitmap;
            pictureBox.SizeMode = PictureBoxSizeMode.AutoSize;
            pictureBox.Margin = new Padding(0);

            Controls.Add(pictureBox);

            //test
            initVRAM();
        }

        private void Display_FormClosing(object sender, FormClosingEventArgs e) {
            Environment.Exit(0);
            //Close();
        }

        public void update() {
            pictureBox.Refresh();
        }

        public void initVRAM() {
            for (int x = 0; x < 1024; x++) {
                for (int y = 0; y < 512; y++) {
                    VRAM.SetPixel(x, y, Color.Black);
                }
            }
        }



    }
}