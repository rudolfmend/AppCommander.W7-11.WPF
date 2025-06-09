using System;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace AppCommander.W7_11.WPF.Core
{
    public static class UIElementDetector
    {
        public static UIElementInfo GetElementAtPoint(int x, int y)
        {
            try
            {
                // Use UI Automation to find element at point
                AutomationElement element = AutomationElement.FromPoint(new System.Windows.Point(x, y));

                if (element != null)
                {
                    return new UIElementInfo
                    {
                        Name = GetElementProperty(element, AutomationElement.NameProperty),
                        AutomationId = GetElementProperty(element, AutomationElement.AutomationIdProperty),
                        ClassName = GetElementProperty(element, AutomationElement.ClassNameProperty),
                        ControlType = element.Current.ControlType?.LocalizedControlType ?? "Unknown",
                        X = x,
                        Y = y,
                        BoundingRectangle = element.Current.BoundingRectangle,
                        IsEnabled = element.Current.IsEnabled,
                        IsVisible = !element.Current.IsOffscreen,
                        ProcessId = element.Current.ProcessId,
                        WindowHandle = new IntPtr(element.Current.NativeWindowHandle)
                    };
                }
            }
            catch (Exception ex)
            {
                // Log error or handle gracefully
                System.Diagnostics.Debug.WriteLine($"Error detecting UI element: {ex.Message}");
            }

            // Fallback - get basic window info
            return GetBasicWindowInfo(x, y);
        }

        private static UIElementInfo GetBasicWindowInfo(int x, int y)
        {
            IntPtr hwnd = WindowFromPoint(new POINT { x = x, y = y });

            if (hwnd != IntPtr.Zero)
            {
                string className = GetClassName(hwnd);
                string windowText = GetWindowText(hwnd);
                RECT rect;
                GetWindowRect(hwnd, out rect);

                return new UIElementInfo
                {
                    Name = windowText,
                    ClassName = className,
                    ControlType = "Window",
                    X = x,
                    Y = y,
                    BoundingRectangle = new System.Windows.Rect(rect.Left, rect.Top,
                        rect.Right - rect.Left, rect.Bottom - rect.Top),
                    WindowHandle = hwnd,
                    IsEnabled = IsWindowEnabled(hwnd),
                    IsVisible = IsWindowVisible(hwnd)
                };
            }

            return null;
        }

        private static string GetElementProperty(AutomationElement element, AutomationProperty property)
        {
            try
            {
                object value = element.GetCurrentPropertyValue(property);
                return value?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static UIElementInfo FindElementByName(string name, IntPtr windowHandle)
        {
            try
            {
                AutomationElement window = AutomationElement.FromHandle(windowHandle);

                if (window != null)
                {
                    var condition = new PropertyCondition(AutomationElement.NameProperty, name);
                    AutomationElement element = window.FindFirst(TreeScope.Descendants, condition);

                    if (element != null)
                    {
                        var rect = element.Current.BoundingRectangle;
                        return new UIElementInfo
                        {
                            Name = element.Current.Name,
                            AutomationId = GetElementProperty(element, AutomationElement.AutomationIdProperty),
                            ClassName = GetElementProperty(element, AutomationElement.ClassNameProperty),
                            ControlType = element.Current.ControlType?.LocalizedControlType ?? "Unknown",
                            X = (int)rect.X + (int)rect.Width / 2,
                            Y = (int)rect.Y + (int)rect.Height / 2,
                            BoundingRectangle = rect,
                            IsEnabled = element.Current.IsEnabled,
                            IsVisible = !element.Current.IsOffscreen,
                            ProcessId = element.Current.ProcessId,
                            WindowHandle = new IntPtr(element.Current.NativeWindowHandle),
                            AutomationElement = element
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finding element by name: {ex.Message}");
            }

            return null;
        }

        public static UIElementInfo FindElementByAutomationId(string automationId, IntPtr windowHandle)
        {
            try
            {
                AutomationElement window = AutomationElement.FromHandle(windowHandle);

                if (window != null)
                {
                    var condition = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
                    AutomationElement element = window.FindFirst(TreeScope.Descendants, condition);

                    if (element != null)
                    {
                        var rect = element.Current.BoundingRectangle;
                        return new UIElementInfo
                        {
                            Name = element.Current.Name,
                            AutomationId = automationId,
                            ClassName = GetElementProperty(element, AutomationElement.ClassNameProperty),
                            ControlType = element.Current.ControlType?.LocalizedControlType ?? "Unknown",
                            X = (int)rect.X + (int)rect.Width / 2,
                            Y = (int)rect.Y + (int)rect.Height / 2,
                            BoundingRectangle = rect,
                            IsEnabled = element.Current.IsEnabled,
                            IsVisible = !element.Current.IsOffscreen,
                            ProcessId = element.Current.ProcessId,
                            WindowHandle = new IntPtr(element.Current.NativeWindowHandle),
                            AutomationElement = element
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finding element by automation ID: {ex.Message}");
            }

            return null;
        }

        // Windows API functions
        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowEnabled(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private static string GetClassName(IntPtr hWnd)
        {
            System.Text.StringBuilder className = new System.Text.StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            return className.ToString();
        }

        private static string GetWindowText(IntPtr hWnd)
        {
            System.Text.StringBuilder windowText = new System.Text.StringBuilder(256);
            GetWindowText(hWnd, windowText, windowText.Capacity);
            return windowText.ToString();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }

    public class UIElementInfo
    {
        public string Name { get; set; } = string.Empty;
        public string AutomationId { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string ControlType { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public System.Windows.Rect BoundingRectangle { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsVisible { get; set; }
        public int ProcessId { get; set; }
        public IntPtr WindowHandle { get; set; }
        public AutomationElement AutomationElement { get; set; }

        public string GetUniqueIdentifier()
        {
            // Try to create unique identifier for the element
            if (!string.IsNullOrEmpty(AutomationId))
                return $"AutoId_{AutomationId}";

            if (!string.IsNullOrEmpty(Name))
                return $"Name_{Name}";

            return $"Class_{ClassName}_Pos_{X}_{Y}";
        }

        public override string ToString()
        {
            return $"{ControlType}: {Name} ({ClassName}) at ({X}, {Y})";
        }
    }
}
