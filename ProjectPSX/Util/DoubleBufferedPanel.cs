using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ProjectPSX.Util {
    public class DoubleBufferedPanel : Panel {

        public DoubleBufferedPanel() {
            if (Environment.OSVersion.Platform != PlatformID.Unix) {
                this.DoubleBuffered = true;
                this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            }
            this.BackgroundImageLayout = ImageLayout.Stretch;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            //this.SetStyle(ControlStyles.UserPaint, true);
            this.UpdateStyles();
        }
    }
}
