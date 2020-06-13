namespace ProjectPSX {
    public interface IHostWindow {
        void Update(int[] bits);
        int GetFPS();
        void SetWindowText(string newText);
        void SetDisplayMode(int horizontalRes, int verticalRes, bool is24BitDepth);
        void SetHorizontalRange(ushort displayX1, ushort displayX2);
        void SetVRAMStart(ushort displayVRAMXStart, ushort displayVRAMYStart);
        void SetVerticalRange(ushort displayY1, ushort displayY2);
    }
}
