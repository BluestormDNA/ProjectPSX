using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security;

namespace ProjectPSX.Interop.User32
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Message
    {
        public IntPtr hWnd;
        public uint msg;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public Point p;
    }

    [SuppressUnmanagedCodeSecurity]
    internal static class NativeMethods
    {
        [DllImport(ExternDll.User32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PeekMessage(out Message msg, IntPtr hWnd, uint messageFilterMin,
                                                uint messageFilterMax, uint flags);

        [DllImport(ExternDll.User32)]
        internal static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport(ExternDll.User32)]
        internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    }
}
