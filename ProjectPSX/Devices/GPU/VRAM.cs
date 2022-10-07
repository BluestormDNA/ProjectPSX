using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProjectPSX {
    public class VRAM {
        public int[] Bits { get; private set; }
        public const int Height = 512;
        public const int Width = 1024;

        protected GCHandle BitsHandle { get; private set; }

        public VRAM() {
            Bits = new int[Width * Height];
            BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPixel(int x, int y, int color) {
            int index = x + (y * Width);

            ref int r0 = ref MemoryMarshal.GetArrayDataReference(Bits);
            Unsafe.Add(ref r0, (nint)index) = color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref int GetPixelRGB888(int x, int y) {
            int index = x + (y * Width);

            ref int r0 = ref MemoryMarshal.GetArrayDataReference(Bits);
            ref int ri = ref Unsafe.Add(ref r0, (nint)index);

            return ref ri;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort GetPixelBGR555(int x, int y) {
            int index = x + (y * Width);

            ref int r0 = ref MemoryMarshal.GetArrayDataReference(Bits);
            ref int color = ref Unsafe.Add(ref r0, (nint)index);

            byte m = (byte)((color & 0xFF000000) >> 24);
            byte r = (byte)((color & 0x00FF0000) >> 16 + 3);
            byte g = (byte)((color & 0x0000FF00) >> 8 + 3);
            byte b = (byte)((color & 0x000000FF) >> 3);

            return (ushort)(m << 15 | b << 10 | g << 5 | r);
        }

    }
}
