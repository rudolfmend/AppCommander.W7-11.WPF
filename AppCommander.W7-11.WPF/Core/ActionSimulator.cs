using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace AppCommander.W7_11.WPF.Core
{
    public class ActionSimulator
    {
        // Windows API constants for mouse events
        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const int MOUSEEVENTF_RIGHTUP = 0x10;
        private const int MOUSEEVENTF_MOVE = 0x01;
        private const int MOUSEEVENTF_ABSOLUTE = 0x8000;

        // Windows API constants for keyboard events
        private const int KEYEVENTF_KEYUP = 0x02;
        private const int KEYEVENTF_UNICODE = 0x04;

        // Default delays
        public int ClickDelay { get; set; } = 50; // ms between mouse down and up
        public int KeyDelay { get; set; } = 50; // ms between key down and up
        public int ActionDelay { get; set; } = 10; // ms between actions

        /// <summary>
        /// Simulates a left mouse click at the specified coordinates
        /// </summary>
        public void ClickAt(int x, int y)
        {
            MoveTo(x, y);
            Thread.Sleep(ActionDelay);

            mouse_event(MOUSEEVENTF_LEFTDOWN, x, y, 0, 0);
            Thread.Sleep(ClickDelay);
            mouse_event(MOUSEEVENTF_LEFTUP, x, y, 0, 0);
        }

        /// <summary>
        /// Simulates a right mouse click at the specified coordinates
        /// </summary>
        public void RightClickAt(int x, int y)
        {
            MoveTo(x, y);
            Thread.Sleep(ActionDelay);

            mouse_event(MOUSEEVENTF_RIGHTDOWN, x, y, 0, 0);
            Thread.Sleep(ClickDelay);
            mouse_event(MOUSEEVENTF_RIGHTUP, x, y, 0, 0);
        }

        /// <summary>
        /// Simulates a double click at the specified coordinates
        /// </summary>
        public void DoubleClickAt(int x, int y)
        {
            ClickAt(x, y);
            Thread.Sleep(50); // Small delay between clicks
            ClickAt(x, y);
        }

        /// <summary>
        /// Moves mouse cursor to the specified coordinates
        /// </summary>
        public void MoveTo(int x, int y)
        {
            // Convert to absolute coordinates (0-65535 range)
            int absoluteX = (x * 65536) / GetSystemMetrics(0); // SM_CXSCREEN
            int absoluteY = (y * 65536) / GetSystemMetrics(1); // SM_CYSCREEN

            mouse_event(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE, absoluteX, absoluteY, 0, 0);
        }

        /// <summary>
        /// Simulates pressing and releasing a key
        /// </summary>
        public void SendKey(Keys key)
        {
            byte vkCode = (byte)key;

            System.Diagnostics.Debug.WriteLine($"ActionSimulator.SendKey: {key} (VK: {vkCode})");

            keybd_event(vkCode, 0, 0, 0); // Key down
            Thread.Sleep(KeyDelay);
            keybd_event(vkCode, 0, KEYEVENTF_KEYUP, 0); // Key up

            System.Diagnostics.Debug.WriteLine($"Key sent: {key}");
        }

        /// <summary>
        /// Simulates key combination (e.g., Ctrl+C)
        /// </summary>
        public void SendKeyCombo(Keys modifierKey, Keys key)
        {
            byte modifierVk = (byte)modifierKey;
            byte keyVk = (byte)key;

            // Press modifier
            keybd_event(modifierVk, 0, 0, 0);
            Thread.Sleep(10);

            // Press key
            keybd_event(keyVk, 0, 0, 0);
            Thread.Sleep(KeyDelay);
            keybd_event(keyVk, 0, KEYEVENTF_KEYUP, 0);

            Thread.Sleep(10);
            // Release modifier
            keybd_event(modifierVk, 0, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Sends text by simulating individual character key presses
        /// </summary>
        public void SendText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            foreach (char c in text)
            {
                SendCharacter(c);
                Thread.Sleep(10); // Small delay between characters
            }
        }

        /// <summary>
        /// Sends a single character using Unicode input
        /// </summary>
        public void SendCharacter(char character)
        {
            // Use SendInput for Unicode support
            INPUT[] inputs = new INPUT[2];

            // Key down
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

            // Key up
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

        /// <summary>
        /// Clears current text selection and types new text
        /// </summary>
        public void ReplaceText(string newText)
        {
            // Select all current text
            SendKeyCombo(Keys.Control, Keys.A);
            Thread.Sleep(50);

            // Type new text
            SendText(newText);
        }

        /// <summary>
        /// Simulates pressing Enter key
        /// </summary>
        public void PressEnter()
        {
            SendKey(Keys.Enter);
        }

        /// <summary>
        /// Simulates pressing Tab key
        /// </summary>
        public void PressTab()
        {
            SendKey(Keys.Tab);
        }

        /// <summary>
        /// Simulates pressing Escape key
        /// </summary>
        public void PressEscape()
        {
            SendKey(Keys.Escape);
        }

        /// <summary>
        /// Waits for specified milliseconds
        /// </summary>
        public void Wait(int milliseconds)
        {
            Thread.Sleep(milliseconds);
        }

        /// <summary>
        /// Gets current mouse cursor position
        /// </summary>
        public System.Drawing.Point GetCursorPosition()
        {
            POINT point;
            GetCursorPos(out point);
            return new System.Drawing.Point(point.x, point.y);
        }

        /// <summary>
        /// Checks if a point is within screen bounds
        /// </summary>
        public bool IsPointOnScreen(int x, int y)
        {
            try
            {
                // Get primary screen bounds
                var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                int screenWidth = primaryScreen.Bounds.Width;
                int screenHeight = primaryScreen.Bounds.Height;

                System.Diagnostics.Debug.WriteLine($"Screen validation: Point({x}, {y}) vs Screen({screenWidth}x{screenHeight})");

                // Basic bounds check with some tolerance for multi-monitor setups
                bool isValid = x >= -100 && x <= (screenWidth + 100) && y >= -100 && y <= (screenHeight + 100);

                if (!isValid)
                {
                    // Check if point is on any screen (multi-monitor support)
                    foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                    {
                        if (screen.Bounds.Contains(x, y))
                        {
                            System.Diagnostics.Debug.WriteLine($"Point is valid on secondary screen: {screen.Bounds}");
                            return true;
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"Point ({x}, {y}) is outside all screen bounds");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking screen bounds: {ex.Message}");
                // If we can't determine screen bounds, assume point is valid
                return true;
            }
        }

        /// <summary>
        /// Gets current screen information for debugging
        /// </summary>
        public string GetScreenInfo()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Screen Information:");

                var primary = System.Windows.Forms.Screen.PrimaryScreen;
                sb.AppendLine($"Primary: {primary.Bounds.Width}x{primary.Bounds.Height} at ({primary.Bounds.X}, {primary.Bounds.Y})");

                foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                {
                    if (!screen.Primary)
                    {
                        sb.AppendLine($"Secondary: {screen.Bounds.Width}x{screen.Bounds.Height} at ({screen.Bounds.X}, {screen.Bounds.Y})");
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting screen info: {ex.Message}";
            }
        }

        /// <summary>
        /// Simulates mouse drag from one point to another
        /// </summary>
        public void DragFromTo(int startX, int startY, int endX, int endY)
        {
            MoveTo(startX, startY);
            Thread.Sleep(ActionDelay);

            // Mouse down
            mouse_event(MOUSEEVENTF_LEFTDOWN, startX, startY, 0, 0);
            Thread.Sleep(100);

            // Move to end position
            MoveTo(endX, endY);
            Thread.Sleep(100);

            // Mouse up
            mouse_event(MOUSEEVENTF_LEFTUP, endX, endY, 0, 0);
        }

        /// <summary>
        /// Simulates mouse wheel scroll
        /// </summary>
        public void ScrollWheel(int scrollAmount)
        {
            const int MOUSEEVENTF_WHEEL = 0x800;
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, scrollAmount * 120, 0);
        }

        // Windows API imports
        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern IntPtr GetMessageExtraInfo();

        // Structures for SendInput
        private const int INPUT_KEYBOARD = 1;

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

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }
    }
}
