using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace KLC_Finch {

    //This is supposed to fix the windows opening behind other apps
    //https://weblog.west-wind.com/posts/2020/Oct/12/Window-Activation-Headaches-in-WPF

    public static class WindowUtilities {

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        /// <summary>
        /// Activates a WPF window even if the window is activated on a separate thread
        /// </summary>
        /// <param name="window"></param>
        public static void ActivateWindow(Window window) {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();

            var threadId1 = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
            var threadId2 = GetWindowThreadProcessId(hwnd, IntPtr.Zero);

            if (threadId1 != threadId2) {
                AttachThreadInput(threadId1, threadId2, true);
                SetForegroundWindow(hwnd);
                AttachThreadInput(threadId1, threadId2, false);
            } else
                SetForegroundWindow(hwnd);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    }
}
