using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


namespace ProjectPSX {
    public class Display : IDisposable {
        public Bitmap Bitmap { get; set; }
        public Int32[] Bits { get; private set; }
        public bool Disposed { get; private set; }
        public int Height;
        public int Width;

        protected GCHandle BitsHandle { get; private set; }

        public Display(int width, int height) {
            Height = height;
            Width = width;
            Bits = new Int32[Width * Height];
            BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);

            Bitmap = new Bitmap(Width, Height, Width * 4, PixelFormat.Format32bppRgb, BitsHandle.AddrOfPinnedObject());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPixel(int x, int y, int color) {
            int index = x + (y * Width);
            Bits[index] = color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetPixelRGB888(int x, int y) {
            int index = x + (y * Width);
            return Bits[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort GetPixelBGR555(int x, int y) {
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