using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AppCommander.W7_11.WPF.Core.Helpers
{
    public static class Win32DialogHelper
    {
        private const uint BM_CLICK = 0x00F5;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string windowTitle);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

        internal delegate bool EnumChildProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxLength);

        [DllImport("user32.dll")]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder text, int maxLength);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);


        /// <summary>
        /// Detects Win32 MessageBox windows (#32770)
        /// </summary>
        public static IntPtr FindDialogWindow(string expectedTitle = null)
        {
            IntPtr dlg = FindWindow("#32770", expectedTitle);

            if (dlg != IntPtr.Zero)
                return dlg;

            // fallback: look for ANY messagebox
            return FindWindow("#32770", null);
        }


        /// <summary>
        /// Finds a button inside a Win32 dialog by its caption (OK, Cancel, Yes, No…)
        /// </summary>
        public static IntPtr FindDialogButton(IntPtr dialog, string[] possibleCaptions)
        {
            IntPtr foundButton = IntPtr.Zero;

            EnumChildWindows(dialog, (hwnd, l) =>
            {
                StringBuilder classNameSb = new StringBuilder(128);
                GetClassName(hwnd, classNameSb, classNameSb.Capacity);

                if (classNameSb.ToString() == "Button")
                {
                    StringBuilder titleSb = new StringBuilder(256);
                    GetWindowText(hwnd, titleSb, titleSb.Capacity);
                    string caption = titleSb.ToString().Trim();

                    foreach (string match in possibleCaptions)
                    {
                        if (!string.IsNullOrEmpty(caption) && caption.Equals(match, StringComparison.OrdinalIgnoreCase))
                        {
                            foundButton = hwnd;
                            return false; // stop enumeration
                        }
                    }
                }

                return true; // continue
            }, IntPtr.Zero);

            return foundButton;
        }


        /// <summary>
        /// Performs a BM_CLICK on the given Win32 button
        /// </summary>
        public static bool ClickDialogButton(IntPtr dialog, string[] captions)
        {
            var button = FindDialogButton(dialog, captions);

            if (button == IntPtr.Zero)
                return false;

            SendMessage(button, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            return true;
        }
    }
}

