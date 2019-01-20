using System;
using System.Windows.Forms;

namespace ProjectPSX {
    public partial class Form1 : Form {
        public Form1() {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e) {
            ProjectPSX psx = new ProjectPSX();
        }
    }
}
