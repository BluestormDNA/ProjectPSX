using System;

namespace ProjectPSX {
    //Converts the GPU raw VRAM (1024x512 int backbuffer) to the visible display frame.
    //The display output buffer is expected to keep a 1024 int stride per scanline.
    public static class DisplayBlitter {

        public static void Blit16bpp(ReadOnlySpan<int> vram, Span<int> display, int horizontalRes, int verticalRes,
            int displayVRAMXStart, int displayVRAMYStart, int displayY1, int displayY2) {

            int yRangeOffset = (240 - (displayY2 - displayY1)) >> (verticalRes == 480 ? 0 : 1);
            if (yRangeOffset < 0) yRangeOffset = 0;

            for (int y = yRangeOffset; y < verticalRes - yRangeOffset; y++) {
                var from = vram.Slice(displayVRAMXStart + ((y - yRangeOffset + displayVRAMYStart) * 1024), horizontalRes);
                var to = display.Slice(y * 1024);
                from.CopyTo(to);
            }
        }

        public static void Blit24bpp(ReadOnlySpan<int> vram, Span<int> display, int horizontalRes, int verticalRes,
            int displayVRAMXStart, int displayVRAMYStart, int displayY1, int displayY2) {

            int yRangeOffset = (240 - (displayY2 - displayY1)) >> (verticalRes == 480 ? 0 : 1);
            if (yRangeOffset < 0) yRangeOffset = 0;

            Span<int> scanLine = stackalloc int[horizontalRes];

            for (int y = yRangeOffset; y < verticalRes - yRangeOffset; y++) {
                int offset = 0;
                var startXYPosition = displayVRAMXStart + ((y - yRangeOffset + displayVRAMYStart) * 1024);
                for (int x = 0; x < horizontalRes; x += 2) {
                    int p0rgb = vram[startXYPosition + offset++];
                    int p1rgb = vram[startXYPosition + offset++];
                    int p2rgb = vram[startXYPosition + offset++];

                    ushort p0bgr555 = GetPixelBGR555(p0rgb);
                    ushort p1bgr555 = GetPixelBGR555(p1rgb);
                    ushort p2bgr555 = GetPixelBGR555(p2rgb);

                    //[(G0R0][R1)(B0][B1G1)]
                    //   RG    B - R   GB

                    int p0R = p0bgr555 & 0xFF;
                    int p0G = (p0bgr555 >> 8) & 0xFF;
                    int p0B = p1bgr555 & 0xFF;
                    int p1R = (p1bgr555 >> 8) & 0xFF;
                    int p1G = p2bgr555 & 0xFF;
                    int p1B = (p2bgr555 >> 8) & 0xFF;

                    int p0rgb24bpp = p0R << 16 | p0G << 8 | p0B;
                    int p1rgb24bpp = p1R << 16 | p1G << 8 | p1B;

                    scanLine[x] = p0rgb24bpp;
                    scanLine[x + 1] = p1rgb24bpp;
                }
                scanLine.CopyTo(display.Slice(y * 1024));
            }
        }

        private static ushort GetPixelBGR555(int color) {
            byte m = (byte)((color & 0xFF000000) >> 24);
            byte r = (byte)((color & 0x00FF0000) >> 16 + 3);
            byte g = (byte)((color & 0x0000FF00) >> 8 + 3);
            byte b = (byte)((color & 0x000000FF) >> 3);

            return (ushort)(m << 15 | b << 10 | g << 5 | r);
        }
    }
}
