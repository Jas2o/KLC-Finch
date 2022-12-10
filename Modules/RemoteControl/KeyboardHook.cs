using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace KLC_Finch {

    //https://github.com/rvknth043/Global-Low-Level-Key-Board-And-Mouse-Hook/blob/master/GlobalLowLevelHooks/KeyboardHook.cs

    //This is entirely optional unless the Windows key needs to be captured.

    /// <summary>
    /// Class for intercepting low level keyboard hooks
    /// </summary>
    public class KeyboardHook {

        private const bool captureAll = false; //For testing keys that aren't handled

        /// <summary>
        /// Virtual Keys that will be captured, this should match KeycodeV3.ModifiersJS
        /// </summary>
        public enum VKeys {
            SHIFT = 0x10,       // SHIFT key
            LSHIFT = 0xA0,      // Left SHIFT key
            RSHIFT = 0xA1,      // Right SHIFT key
            //
            CONTROL = 0x11,     // CTRL key
            LCONTROL = 0xA2,    // Left CONTROL key
            RCONTROL = 0xA3,    // Right CONTROL key
            //
            MENU = 0x12,        // ALT key
            LMENU = 0xA4,       // Left MENU/Alt key
            RMENU = 0xA5,       // Right MENU/Alt key
            //
            LWIN = 0x5B,        // Left Windows key (Microsoft Natural keyboard)
            RWIN = 0x5C        // Right Windows key (Natural keyboard)
        }

        /// <summary>
        /// Internal callback processing function
        /// </summary>
        private delegate IntPtr KeyboardHookHandler(int nCode, IntPtr wParam, IntPtr lParam);
        private KeyboardHookHandler hookHandler;

        /// <summary>
        /// Function that will be called when defined events occur
        /// </summary>
        /// <param name="key">VKeys</param>
        public delegate void KeyboardHookCallback(VKeys key);

        #region Events
        public event KeyboardHookCallback KeyDown;
        public event KeyboardHookCallback KeyUp;
        #endregion

        /// <summary>
        /// Hook ID
        /// </summary>
        private IntPtr hookID = IntPtr.Zero;

        /// <summary>
        /// Install low level keyboard hook
        /// </summary>
        public void Install() {
            hookHandler = HookFunc;
            hookID = SetHook(hookHandler);
            IsActive = true;

#if (DEBUG)
            Console.WriteLine("KeyHook added.");
#endif
        }

        /// <summary>
        /// Remove low level keyboard hook
        /// </summary>
        public void Uninstall() {
            IsActive = false;
            UnhookWindowsHookEx(hookID);

#if (DEBUG)
            Console.WriteLine("KeyHook removed.");
#endif
        }

        public bool IsActive { get; private set; }

        /// <summary>
        /// Registers hook with Windows API
        /// </summary>
        /// <param name="proc">Callback function</param>
        /// <returns>Hook ID</returns>
        private IntPtr SetHook(KeyboardHookHandler proc) {
            using (ProcessModule module = Process.GetCurrentProcess().MainModule)
                return SetWindowsHookEx(13, proc, GetModuleHandle(module.ModuleName), 0);
        }

        /// <summary>
        /// Default hook call, which analyses pressed keys
        /// </summary>
        private IntPtr HookFunc(int nCode, IntPtr wParam, IntPtr lParam) {
            if (nCode >= 0) {
                int iwParam = wParam.ToInt32();

                if ((iwParam == WM_KEYDOWN || iwParam == WM_SYSKEYDOWN))
                    if (KeyDown != null) {
                        VKeys vk = (VKeys)Marshal.ReadInt32(lParam);
                        if (Enum.IsDefined(typeof(VKeys), vk) || captureAll) {
                            KeyDown(vk);
                            return (IntPtr)1;
                        }
                    }
                if ((iwParam == WM_KEYUP || iwParam == WM_SYSKEYUP))
                    if (KeyUp != null) {
                        VKeys vk = (VKeys)Marshal.ReadInt32(lParam);
                        if(Enum.IsDefined(typeof(VKeys), vk) || captureAll) {
                            KeyUp(vk);
                            return (IntPtr)1;
                        }
                    }
            }

            return CallNextHookEx(hookID, nCode, wParam, lParam);
        }

        /// <summary>
        /// Destructor. Unhook current hook
        /// </summary>
        ~KeyboardHook() {
            Uninstall();
        }

        /// <summary>
        /// Low-Level function declarations
        /// </summary>
        #region WinAPI
        private const int WM_KEYDOWN = 0x100;
        private const int WM_SYSKEYDOWN = 0x104;
        private const int WM_KEYUP = 0x101;
        private const int WM_SYSKEYUP = 0x105;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, KeyboardHookHandler lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        #endregion
    }
}
