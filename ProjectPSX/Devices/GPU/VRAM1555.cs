using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProjectPSX {
    public class VRAM1555 {
        public ushort[] Bits { get; private set; }
        public int Height;
        public int Width;

        protected GCHandle BitsHandle { get; private set; }

        public VRAM1555(int width, int height) {
            Height = height;
            Width = width;
            Bits = new ushort[Width * Height];
            BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPixel(int x, int y, ushort color) {
            int index = x + (y * Width);
            Bits[index] = color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort GetPixel(int x, int y) {
            int index = x + (y * Width);
            return Bits[index];
        }

    }
}
