using System;
using System.Runtime.InteropServices;
using ProjectPSX.Interop.Gdi32;

using Gdi32 = ProjectPSX.Interop.Gdi32.NativeMethods;

namespace ProjectPSX
{
    internal class GdiBitmap : IDisposable
    {
        public readonly int BytesPerPixel = 4;

        public readonly IntPtr DeviceContext;
        public readonly IntPtr BitmapHandle;

        public readonly int Width;
        public readonly int Height;

        public readonly IntPtr BitmapData;

        private readonly IntPtr _oldObject;

        private bool _disposed = false;

        public GdiBitmap(int width, int height)
        {
            Width = width;
            Height = height;

            DeviceContext = Gdi32.CreateCompatibleDC(IntPtr.Zero);

            var bitmapHeader = new BitmapInfoHeader
            {
                biSize = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                biWidth = width,
                biHeight = -height, // negative, top-down bitmap
                biPlanes = 1,
                biBitCount = (ushort)(8 * BytesPerPixel),
                biCompression = BitmapCompression.BI_RGB,
            };

            var bitmapInfo = new BitmapInfo
            {
                bmiHeader = bitmapHeader,
            };

            BitmapHandle = Gdi32.CreateDIBSection(DeviceContext, in bitmapInfo, ColorUsage.DIB_RGB_COLORS,
                                                  out BitmapData, IntPtr.Zero, 0);

            _oldObject = Gdi32.SelectObject(DeviceContext, BitmapHandle);
        }

        public unsafe void DrawPixel(int x, int y, int color) {
            int* pixel = (int*)BitmapData;
            pixel += x + (y * Width);
            *pixel = color;
        }

        public unsafe int GetPixel(int x, int y) {
            int* pixel = (int*)BitmapData;
            pixel += x + (y * Width);
            return *pixel;
        }

        ~GdiBitmap()
        {
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                if (_oldObject != IntPtr.Zero)
                {
                    Gdi32.SelectObject(DeviceContext, _oldObject);
                }

                if (BitmapHandle != IntPtr.Zero)
                {
                    Gdi32.DeleteObject(BitmapHandle);
                }

                if (DeviceContext != IntPtr.Zero)
                {
                    Gdi32.DeleteDC(DeviceContext);
                }

                _disposed = true;
            }
        }
    }
}
