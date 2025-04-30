using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ConsoleApp20
{
    internal static class ConsoleExtensions
    {
        // interop message box
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int MessageBox(IntPtr hWnd, String text, String caption, int options);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        [DllImport("User32")]
        static extern int ShowWindow(IntPtr hwnd, int nCmdShow);

        static public void Hide(this IntPtr hwnd) => ShowWindow(hwnd, SW_HIDE);

        [DllImport("kernel32")]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out IntPtr ProcessId);

        public static Process GetWindowThreadProcess(this IntPtr hWnd)
        {
            GetWindowThreadProcessId(hWnd, out IntPtr ProcessId);
            if (ProcessId != IntPtr.Zero)
                return Process.GetProcessById((int)ProcessId);
            else
                return null;
        }
    }
}