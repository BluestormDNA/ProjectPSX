using System;
using System.Runtime.CompilerServices;
namespace ProjectPSX.Util
{
    public class GPUDisplay 
    {
        public Int32[] Bits { get; protected set; }
        public int Height;
        public int Width;

        public GPUDisplay()
        {
        }

        public GPUDisplay(int width, int height)
        {
            Height = height;
            Width = width;
            Bits = new Int32[Width * Height];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPixel(int x, int y, int color)
        {
            int index = x + (y * Width);
            Bits[index] = color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetPixelRGB888(int x, int y)
        {
            int index = x + (y * Width);
            return Bits[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort GetPixelBGR555(int x, int y)
        {
            int index = x + (y * Width);
            int color = Bits[index];

            byte m = (byte)((color & 0xFF000000) >> 24);
            byte r = (byte)((color & 0x00FF0000) >> 16 + 3);
            byte g = (byte)((color & 0x0000FF00) >> 8 + 3);
            byte b = (byte)((color & 0x000000FF) >> 3);

            return (ushort)(m << 15 | b << 10 | g << 5 | r);
        }
    }
}
