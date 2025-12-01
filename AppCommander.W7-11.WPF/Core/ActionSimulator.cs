using AppCommander.W7_11.WPF.Core;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Automation;

namespace AppCommander.W7_11.WPF.Core
{
    public class ActionSimulator
    {
        // Windows API constants - UNIFIED
        private const int MOUSEEVENTF_MOVE = 0x01;
        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const int MOUSEEVENTF_RIGHTUP = 0x10;
        private const int MOUSEEVENTF_WHEEL = 0x800;
        private const int MOUSEEVENTF_ABSOLUTE = 0x8000;

        private const int KEYEVENTF_KEYUP = 0x02;
        private const int KEYEVENTF_UNICODE = 0x04;
        private const int INPUT_KEYBOARD = 1;

        // Default delays
        public int ClickDelay { get; set; } = 50;
        public int KeyDelay { get; set; } = 50;
        public int ActionDelay { get; set; } = 10;

        private WindowTracker _windowTracker;

        public ActionSimulator(WindowTracker windowTracker = null)
        {
            _windowTracker = windowTracker ?? new WindowTracker();
        }

        // === EXISTUJÚCE METÓDY ===

        public void ClickAt(int x, int y)
        {
            MoveTo(x, y);
            Thread.Sleep(ActionDelay);
            mouse_event(MOUSEEVENTF_LEFTDOWN, x, y, 0, 0);
            Thread.Sleep(ClickDelay);
            mouse_event(MOUSEEVENTF_LEFTUP, x, y, 0, 0);
        }

        public void RightClickAt(int x, int y)
        {
            MoveTo(x, y);
            Thread.Sleep(ActionDelay);
            mouse_event(MOUSEEVENTF_RIGHTDOWN, x, y, 0, 0);
            Thread.Sleep(ClickDelay);
            mouse_event(MOUSEEVENTF_RIGHTUP, x, y, 0, 0);
        }

        public void DoubleClickAt(int x, int y)
        {
            ClickAt(x, y);
            Thread.Sleep(50);
            ClickAt(x, y);
        }

        public void MoveTo(int x, int y)
        {
            int absoluteX = (x * 65536) / GetSystemMetrics(0);
            int absoluteY = (y * 65536) / GetSystemMetrics(1);
            mouse_event(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE, absoluteX, absoluteY, 0, 0);
        }

        public void SendKey(Keys key)
        {
            byte vkCode = (byte)key;
            keybd_event(vkCode, 0, 0, 0);
            Thread.Sleep(KeyDelay);
            keybd_event(vkCode, 0, KEYEVENTF_KEYUP, 0);
        }

        public void SendKeyCombo(Keys modifierKey, Keys key)
        {
            byte modifierVk = (byte)modifierKey;
            byte keyVk = (byte)key;
            keybd_event(modifierVk, 0, 0, 0);
            Thread.Sleep(10);
            keybd_event(keyVk, 0, 0, 0);
            Thread.Sleep(KeyDelay);
            keybd_event(keyVk, 0, KEYEVENTF_KEYUP, 0);
            Thread.Sleep(10);
            keybd_event(modifierVk, 0, KEYEVENTF_KEYUP, 0);
        }

        public void SendText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (char c in text)
            {
                SendCharacter(c);
                Thread.Sleep(10);
            }
        }

        public void SendCharacter(char character)
        {
            INPUT[] inputs = new INPUT[2];

            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = character,
                        dwFlags = KEYEVENTF_UNICODE,
                        dwExtraInfo = GetMessageExtraInfo()
                    }
                }
            };

            inputs[1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = character,
                        dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                        dwExtraInfo = GetMessageExtraInfo()
                    }
                }
            };

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public void ReplaceText(string newText)
        {
            SendKeyCombo(Keys.Control, Keys.A);
            Thread.Sleep(50);
            SendText(newText);
        }

        public void PressEnter() => SendKey(Keys.Enter);
        public void PressTab() => SendKey(Keys.Tab);
        public void PressEscape() => SendKey(Keys.Escape);
        public void Wait(int milliseconds) => Thread.Sleep(milliseconds);

        public System.Drawing.Point GetCursorPosition()
        {
            POINT point;
            GetCursorPos(out point);
            return new System.Drawing.Point(point.X, point.Y);
        }

        public bool IsPointOnScreen(int x, int y)
        {
            try
            {
                var primaryScreen = Screen.PrimaryScreen;
                int screenWidth = primaryScreen.Bounds.Width;
                int screenHeight = primaryScreen.Bounds.Height;

                bool isValid = x >= -100 && x <= (screenWidth + 100) &&
                               y >= -100 && y <= (screenHeight + 100);

                if (!isValid)
                {
                    foreach (var screen in Screen.AllScreens)
                    {
                        if (screen.Bounds.Contains(x, y))
                            return true;
                    }
                    return false;
                }
                return true;
            }
            catch
            {
                return true;
            }
        }

        public string GetScreenInfo()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Screen Information:");
                var primary = Screen.PrimaryScreen;
                sb.AppendLine($"Primary: {primary.Bounds.Width}x{primary.Bounds.Height} at ({primary.Bounds.X}, {primary.Bounds.Y})");
                foreach (var screen in Screen.AllScreens)
                {
                    if (!screen.Primary)
                        sb.AppendLine($"Secondary: {screen.Bounds.Width}x{screen.Bounds.Height} at ({screen.Bounds.X}, {screen.Bounds.Y})");
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        public void DragFromTo(int startX, int startY, int endX, int endY)
        {
            MoveTo(startX, startY);
            Thread.Sleep(ActionDelay);
            mouse_event(MOUSEEVENTF_LEFTDOWN, startX, startY, 0, 0);
            Thread.Sleep(100);
            MoveTo(endX, endY);
            Thread.Sleep(100);
            mouse_event(MOUSEEVENTF_LEFTUP, endX, endY, 0, 0);
        }

        public void ScrollWheel(int scrollAmount)
        {
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, scrollAmount * 120, 0);
        }

        // === MULTI-APP FALLBACK ===

        public void ExecuteCommand(Command cmd)
        {
            // manuálny parsing - .NET 4.8 nemá IntPtr.TryParse 
            IntPtr targetHwnd = IntPtr.Zero;
            try
            {
                if (string.IsNullOrEmpty(cmd.TargetWindow))
                {
                    LogError("Target window is null or empty");
                    return;
                }

                // Parsovanie hex formátu (napr. "0x12345") alebo decimálneho
                if (cmd.TargetWindow.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    targetHwnd = new IntPtr(Convert.ToInt64(cmd.TargetWindow.Substring(2), 16));
                }
                else
                {
                    targetHwnd = new IntPtr(Convert.ToInt64(cmd.TargetWindow));
                }

                if (targetHwnd == IntPtr.Zero)
                {
                    LogError($"Invalid target window handle: {cmd.TargetWindow}");
                    return;
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to parse window handle '{cmd.TargetWindow}': {ex.Message}");
                return;
            }

            var element = _windowTracker.GetTrackedElement(targetHwnd);

            if (element == null)
            {
                element = SafeGetElement(targetHwnd);
                if (element != null)
                    _windowTracker.TrackWindow(targetHwnd);
            }

            if (element == null)
            {
                SimulateWithWin32(cmd, targetHwnd);
                return;
            }

            try
            {
                ExecuteWithAutomation(element, cmd);
            }
            catch (COMException)
            {
                SimulateWithWin32(cmd, targetHwnd);
            }
        }

        private AutomationElement SafeGetElement(IntPtr hwnd)
        {
            try
            {
                var element = AutomationElement.FromHandle(hwnd);
                if (element != null)
                {
                    try
                    {
                        var _ = element.Current.Name;
                        return element;
                    }
                    catch (ElementNotAvailableException)
                    {
                        return null;
                    }
                }
            }
            catch (COMException)
            {
                return null;
            }
            return null;
        }

        private void ExecuteWithAutomation(AutomationElement element, Command cmd)
        {
            // UI Automation - zatiaľ nie je implementované, fallback na Win32
            throw new NotImplementedException("UI Automation not yet implemented");
        }

        private void SimulateWithWin32(Command cmd, IntPtr targetHwnd)
        {
            try
            {
                SetForegroundWindow(targetHwnd);
                Thread.Sleep(50);

                switch (cmd.Type)
                {
                    case CommandType.Click:
                    case CommandType.MouseClick:
                        SimulateWin32Click(cmd, targetHwnd);
                        break;
                    case CommandType.SetText:
                    case CommandType.TypeText:
                        SimulateWin32Type(cmd, targetHwnd);
                        break;
                    case CommandType.KeyPress:
                        SimulateWin32KeyPress(cmd, targetHwnd);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogError($"Win32 fallback failed: {ex.Message}");
            }
        }

        private void SimulateWin32Click(Command cmd, IntPtr targetHwnd)
        {
            if (cmd.ElementX > 0 && cmd.ElementY > 0)
            {
                POINT pt = new POINT { X = cmd.ElementX, Y = cmd.ElementY };
                ClientToScreen(targetHwnd, ref pt);
                SetCursorPos(pt.X, pt.Y);
                Thread.Sleep(10);
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                Thread.Sleep(10);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
            }
        }

        private void SimulateWin32Type(Command cmd, IntPtr targetHwnd)
        {
            if (!string.IsNullOrEmpty(cmd.Value))
            {
                SetFocus(targetHwnd);
                Thread.Sleep(50);
                SendText(cmd.Value);
            }
        }

        private void SimulateWin32KeyPress(Command cmd, IntPtr targetHwnd)
        {
            if (cmd.KeyCode > 0)
            {
                byte vk = (byte)cmd.KeyCode;
                keybd_event(vk, 0, 0, UIntPtr.Zero);
                Thread.Sleep(10);
                keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
        }

        private byte GetVirtualKeyCode(string keyName)
        {
            var keyMap = new Dictionary<string, byte>
            {
                {"Enter", 0x0D}, {"Return", 0x0D}, {"Tab", 0x09},
                {"Escape", 0x1B}, {"Esc", 0x1B}, {"Space", 0x20},
                {"Backspace", 0x08}, {"Delete", 0x2E}, {"Del", 0x2E},
                {"Home", 0x24}, {"End", 0x23},
                {"PageUp", 0x21}, {"PageDown", 0x22},
                {"Left", 0x25}, {"ArrowLeft", 0x25},
                {"Up", 0x26}, {"ArrowUp", 0x26},
                {"Right", 0x27}, {"ArrowRight", 0x27},
                {"Down", 0x28}, {"ArrowDown", 0x28},
                {"F1", 0x70}, {"F2", 0x71}, {"F3", 0x72}, {"F4", 0x73},
                {"F5", 0x74}, {"F6", 0x75}, {"F7", 0x76}, {"F8", 0x77},
                {"F9", 0x78}, {"F10", 0x79}, {"F11", 0x7A}, {"F12", 0x7B}
            };
            return keyMap.TryGetValue(keyName, out byte vk) ? vk : (byte)0;
        }

        private void LogError(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[ActionSimulator] {message}");
        }

        // === WIN32 API - UNIFIED ===

        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern IntPtr GetMessageExtraInfo();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        // === STRUCTURES - UNIFIED ===

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
    }
}
