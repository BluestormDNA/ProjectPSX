using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;


namespace ProjectDMG {
    public class DirectBitmap : IDisposable {
        public Bitmap Bitmap { get; private set; }
        public Int32[] Bits { get; private set; }
        public bool Disposed { get; private set; }
        public int Height { get; private set; }
        public int Width { get; private set; }

        protected GCHandle BitsHandle { get; private set; }

        public DirectBitmap(int width, int height) {
            Width = width;
            Height = height;
            Bits = new Int32[width * height];
            BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
            Bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, BitsHandle.AddrOfPinnedObject());
        }

        public void SetPixel(int x, int y, Color colour) {
            int index = x + (y * Width);
            int col = colour.ToArgb();

            Bits[index] = col;
        }

        public Color GetPixel(int x, int y) {
            int index = x + (y * Width);
            int col = Bits[index];
            Color result = Color.FromArgb(col);

            return result;
        }

        public ushort GetRawPixelValues(int x, int y) {
            int index = x + (y * Width);
            uint color = (uint)Bits[index];


            byte r = (byte)((color & 0xFF0000) >> 16 + 3 );
            byte g = (byte)((color & 0x00FF00) >> 8 + 3);
            byte b = (byte)((color & 0x0000FF) >> 2);

            return (ushort)((b << 10) | (g << 5) | r);

            /*
            byte r = (byte)((val & 0x1F) << 3);
            byte g = (byte)(((val >> 5) & 0x1F) << 3);
            byte b = (byte)((val >> 10) << 3);

            return Color.FromArgb(r, g, b);
            */
            //return p1; //todo add p2
        }

        public void Dispose() {
            if (Disposed) return;
            Disposed = true;
            Bitmap.Dispose();
            BitsHandle.Free();
        }
    }
}