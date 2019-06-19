using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;


namespace ProjectPSX {
    public class DirectBitmap : IDisposable {
        public Bitmap Bitmap { get; set; }
        public Int32[] Bits { get; private set; }
        public bool Disposed { get; private set; }
        private const int Height = 512;
        private const int Width = 1024;

        protected GCHandle BitsHandle { get; private set; }

        public DirectBitmap() {
            Bits = new Int32[Width * Height];
            BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);

            Bitmap = new Bitmap(Width, Height, Width * 4, PixelFormat.Format32bppRgb, BitsHandle.AddrOfPinnedObject());
        }

        public void SetPixel(int x, int y, int color) {
            int index = x + (y * Width);
            Bits[index] = color;
        }

        public int GetPixel(int x, int y) {
            int index = x + (y * Width);
            return Bits[index];
        }

        public ushort GetPixel16(int x, int y) {
            int index = x + (y * Width);
            int color = Bits[index];

            byte m = (byte)((color & 0xFF000000) >> 24);
            byte r = (byte)((color & 0x00FF0000) >> 16 + 3);
            byte g = (byte)((color & 0x0000FF00) >> 8 + 3);
            byte b = (byte)((color & 0x000000FF) >> 3);

            return (ushort)(m << 15 | b << 10 | g << 5 | r);
        }

        public void Dispose() {
            if (Disposed) return;
            Disposed = true;
            Bitmap.Dispose();
            BitsHandle.Free();
        }
    }
}