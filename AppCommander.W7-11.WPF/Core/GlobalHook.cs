using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AppCommander.W7_11.WPF.Core
{
    public class GlobalHook
    {
        // Windows API constants
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;

        // Hook handles
        private IntPtr keyboardHookID = IntPtr.Zero;
        private IntPtr mouseHookID = IntPtr.Zero;

        // Delegates for hooks
        private LowLevelKeyboardProc keyboardProc;
        private LowLevelMouseProc mouseProc;

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        // Events
        public event EventHandler<KeyPressedEventArgs> KeyPressed;
        public event EventHandler<MouseClickedEventArgs> MouseClicked;

        public GlobalHook()
        {
            keyboardProc = HookKeyboardCallback;
            mouseProc = HookMouseCallback;
        }

        public void StartHooking()
        {
            keyboardHookID = SetHook(keyboardProc, WH_KEYBOARD_LL);
            mouseHookID = SetHook(mouseProc, WH_MOUSE_LL);
        }

        public void StopHooking()
        {
            if (keyboardHookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(keyboardHookID);
                keyboardHookID = IntPtr.Zero;
            }

            if (mouseHookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(mouseHookID);
                mouseHookID = IntPtr.Zero;
            }
        }

        private IntPtr SetHook(Delegate proc, int hookType)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(
                    hookType,
                    Marshal.GetFunctionPointerForDelegate(proc),
                    GetModuleHandle(curModule.ModuleName),
                    0);
            }
        }

        private IntPtr HookKeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                if (wParam == (IntPtr)WM_KEYDOWN)
                {
                    int vkCode = Marshal.ReadInt32(lParam);

                    // Get foreground window info
                    IntPtr foregroundWindow = GetForegroundWindow();
                    string windowTitle = GetWindowTitle(foregroundWindow);
                    string processName = GetProcessName(foregroundWindow);

                    KeyPressed?.Invoke(this, new KeyPressedEventArgs
                    {
                        Key = (Keys)vkCode,
                        WindowHandle = foregroundWindow,
                        WindowTitle = windowTitle,
                        ProcessName = processName,
                        Timestamp = DateTime.Now
                    });
                }
            }

            return CallNextHookEx(keyboardHookID, nCode, wParam, lParam);
        }

        private IntPtr HookMouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                if (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN)
                {
                    POINT mouseData = Marshal.PtrToStructure<POINT>(lParam);

                    // Get foreground window info
                    IntPtr foregroundWindow = GetForegroundWindow();
                    string windowTitle = GetWindowTitle(foregroundWindow);
                    string processName = GetProcessName(foregroundWindow);

                    // Try to get UI element at cursor position
                    var uiElement = UIElementDetector.GetElementAtPoint(mouseData.x, mouseData.y);

                    MouseClicked?.Invoke(this, new MouseClickedEventArgs
                    {
                        X = mouseData.x,
                        Y = mouseData.y,
                        Button = wParam == (IntPtr)WM_LBUTTONDOWN ? MouseButtons.Left : MouseButtons.Right,
                        WindowHandle = foregroundWindow,
                        WindowTitle = windowTitle,
                        ProcessName = processName,
                        UIElement = uiElement,
                        Timestamp = DateTime.Now
                    });
                }
            }

            return CallNextHookEx(mouseHookID, nCode, wParam, lParam);
        }

        private string GetWindowTitle(IntPtr hWnd)
        {
            const int nChars = 256;
            System.Text.StringBuilder buffer = new System.Text.StringBuilder(nChars);

            if (GetWindowText(hWnd, buffer, nChars) > 0)
                return buffer.ToString();

            return string.Empty;
        }

        private string GetProcessName(IntPtr hWnd)
        {
            try
            {
                GetWindowThreadProcessId(hWnd, out uint processId);
                using (Process process = Process.GetProcessById((int)processId))
                {
                    return process.ProcessName;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        // Windows API imports
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            IntPtr lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        ~GlobalHook()
        {
            StopHooking();
        }
    }

    // Event argument classes
    public class KeyPressedEventArgs : EventArgs
    {
        public Keys Key { get; set; }
        public IntPtr WindowHandle { get; set; }
        public string WindowTitle { get; set; }
        public string ProcessName { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class MouseClickedEventArgs : EventArgs
    {
        public int X { get; set; }
        public int Y { get; set; }
        public MouseButtons Button { get; set; }
        public IntPtr WindowHandle { get; set; }
        public string WindowTitle { get; set; }
        public string ProcessName { get; set; }
        public UIElementInfo UIElement { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
