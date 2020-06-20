using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ProjectPSX.Util;

namespace ProjectPSX {
    public class Display : GPUDisplay, IDisposable {
        public Bitmap Bitmap { get; set; }
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

        public void Dispose() {
            if (Disposed) return;
            Disposed = true;
            Bitmap.Dispose();
            BitsHandle.Free();
        }
    }
}
