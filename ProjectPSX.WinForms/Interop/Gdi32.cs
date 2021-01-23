using System;
using System.Runtime.InteropServices;
using System.Security;

namespace ProjectPSX.Interop.Gdi32
{
    internal enum BitmapCompression : uint
    {
        BI_RGB = 0,
        BI_RLE8 = 1,
        BI_RLE4 = 2,
        BI_BITFIELDS = 3,
        BI_JPEG = 4,
        BI_PNG = 5,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfoHeader
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public BitmapCompression biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RgbQuad
    {
        public byte rgbBlue;
        public byte rgbGreen;
        public byte rgbRed;
        public byte rgbReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfo
    {
        public BitmapInfoHeader bmiHeader;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] bmiColors;
    }

    internal enum ColorUsage : uint
    {
        DIB_RGB_COLORS = 0, /* color table in RGBs */
        DIB_PAL_COLORS = 1, /* color table in palette indices */
    }

    internal enum RasterOp : uint
    {
        SRCCOPY = 0x00CC0020, // dest = source
        SRCPAINT = 0x00EE0086, // dest = source OR dest
        SRCAND = 0x008800C6, // dest = source AND dest
        SRCINVERT = 0x00660046, // dest = source XOR dest
        SRCERASE = 0x00440328, // dest = source AND (NOT dest )
        NOTSRCCOPY = 0x00330008, // dest = (NOT source)
        NOTSRCERASE = 0x001100A6, // dest = (NOT src) AND (NOT dest
        MERGECOPY = 0x00C000CA, // dest = (source AND pattern)
        MERGEPAINT = 0x00BB0226, // dest = (NOT source) OR dest
        PATCOPY = 0x00F00021, // dest = pattern
        PATPAINT = 0x00FB0A09, // dest = DPSnoo
        PATINVERT = 0x005A0049, // dest = pattern XOR dest
        DSTINVERT = 0x00550009, // dest = (NOT dest)
        BLACKNESS = 0x00000042, // dest = BLACK
        WHITENESS = 0x00FF0062, // dest = WHITE
        NOMIRRORBITMAP = 0x80000000, // Do not Mirror the bitmap in this call
        CAPTUREBLT = 0x40000000, // Include layered windows
    }

    internal enum BltMode : uint {
        STRETCH_ANDSCANS = 0x01,
        STRETCH_ORSCANS = 0x02,
        STRETCH_DELETESCANS = 0x03,
        STRETCH_HALFTONE = 0x04,
    }

    [SuppressUnmanagedCodeSecurity]
    internal static class NativeMethods
    {
        [DllImport(ExternDll.Gdi32)]
        internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport(ExternDll.Gdi32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteDC(IntPtr hdc);

        [DllImport(ExternDll.Gdi32)]
        internal static extern IntPtr CreateDIBSection(IntPtr hdc, [In] in BitmapInfo pbmi, ColorUsage usage,
                                                       out IntPtr ppvBits, IntPtr hSection, uint offset);

        [DllImport(ExternDll.Gdi32)]
        internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

        [DllImport(ExternDll.Gdi32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr ho);

        [DllImport(ExternDll.Gdi32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool StretchBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
                                               IntPtr hdcSrc, int xSrc, int ySrc, int wSrc, int hSrc,
                                               RasterOp rop);

        [DllImport(ExternDll.Gdi32)]
        internal static extern int SetStretchBltMode(IntPtr hdc, BltMode mode);

        [DllImport(ExternDll.Gdi32)]
        internal static extern int StretchDIBits(IntPtr hdc, int xDest, int yDest, int DestWidth, int DestHeight,
                                                 int xSrc, int ySrc, int SrcWidth, int SrcHeight, IntPtr lpBits,
                                                 [In] ref BitmapInfo lpbmi, ColorUsage iUsage, RasterOp rop);
    }
}

