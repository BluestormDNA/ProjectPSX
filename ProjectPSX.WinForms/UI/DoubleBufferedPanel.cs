using System.Windows.Forms;

namespace ProjectPSX.Util {
    public class DoubleBufferedPanel : Panel {

        public DoubleBufferedPanel() {
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.UserPaint, false);

            UpdateStyles();
        }
    }
}
