using System;

using User32 = ProjectPSX.Interop.User32.NativeMethods;

namespace ProjectPSX
{
    internal readonly ref struct GdiDeviceContext
    {
        private readonly IntPtr _handle;
        private readonly IntPtr _hdc;

        public GdiDeviceContext(IntPtr handle)
        {
            _handle = handle;
            _hdc = User32.GetDC(handle);
        }

        public void Dispose()
        {
            User32.ReleaseDC(_handle, _hdc);
        }

        public static implicit operator IntPtr(GdiDeviceContext dc)
        {
            return dc._hdc;
        }
    }
}
