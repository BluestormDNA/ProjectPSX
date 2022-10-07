using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProjectPSX {
    public class VRAM1555 {
        public ushort[] Bits { get; private set; }
        public const int Height = 512;
        public const int Width = 1024;

        protected GCHandle BitsHandle { get; private set; }

        public VRAM1555() {
            Bits = new ushort[Width * Height];
            BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPixel(int x, int y, ushort color) {
            int index = x + (y * Width);

            ref ushort r0 = ref MemoryMarshal.GetArrayDataReference(Bits);
            Unsafe.Add(ref r0, (nint)index) = color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref ushort GetPixel(int x, int y) {
            int index = x + (y * Width);

            ref ushort r0 = ref MemoryMarshal.GetArrayDataReference(Bits);
            ref ushort ri = ref Unsafe.Add(ref r0, (nint)index);

            return ref ri;
        }

    }
}
