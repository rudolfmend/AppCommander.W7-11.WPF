// WindowTracker.cs - ROZŠÍRENÝ s funkciami z WindowFinder
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace AppCommander.W7_11.WPF.Core
{
    /// <summary>
    /// Sleduje a automaticky detekuje nové okná počas nahrávania
    /// ROZŠÍRENÉ: Obsahuje aj funkcie z WindowFinder
    /// </summary>
    public class WindowTracker
    {
        private readonly Timer windowMonitorTimer;
        private readonly HashSet<IntPtr> knownWindows;
        private readonly Dictionary<IntPtr, WindowTrackingInfo> trackedWindows;
        private bool isTracking = false;
        private IntPtr primaryTargetWindow = IntPtr.Zero;
        private string targetProcessName = string.Empty;

        // Konfigurácia
        public int MonitoringIntervalMs { get; set; } = 500;
        public bool TrackChildWindows { get; set; } = true;
        public bool TrackDialogs { get; set; } = true;
        public bool TrackMessageBoxes { get; set; } = true;
        public bool TrackOnlyTargetProcess { get; set; } = true;

        // Events
        public event EventHandler<NewWindowDetectedEventArgs> NewWindowDetected;
        public event EventHandler<WindowClosedEventArgs> WindowClosed;
        public event EventHandler<WindowActivatedEventArgs> WindowActivated;

        public WindowTracker()
        {
            knownWindows = new HashSet<IntPtr>();
            trackedWindows = new Dictionary<IntPtr, WindowTrackingInfo>();
            windowMonitorTimer = new Timer(MonitorWindows, null, Timeout.Infinite, Timeout.Infinite);
        }

        // === NOVÉ STATICKÉ METÓDY (z WindowFinder) ===

        /// <summary>
        /// NOVÉ: Získa všetky viditeľné okná v systéme (nahradí WindowFinder.GetAllWindows)
        /// </summary>
        public static List<WindowTrackingInfo> GetAllWindows()
        {
            var windows = new List<WindowTrackingInfo>();

            try
            {
                EnumWindows((hWnd, lParam) =>
                {
                    try
                    {
                        // Kontroluj či je okno viditeľné
                        if (!IsWindowVisible(hWnd))
                            return true;

                        // Získaj informácie o okne
                        string title = GetWindowTitle(hWnd);
                        string className = GetClassName(hWnd);

                        // Skip okná bez titulku a určité systémové okná
                        if (string.IsNullOrWhiteSpace(title))
                            return true;

                        // Skip určité systémové triedy
                        if (IsSystemWindow(className))
                            return true;

                        // Získaj informácie o procese
                        GetWindowThreadProcessId(hWnd, out uint processId);
                        string processName = "";

                        try
                        {
                            using (var process = Process.GetProcessById((int)processId))
                            {
                                processName = process.ProcessName;
                            }
                        }
                        catch
                        {
                            // Proces už neexistuje alebo nemáme prístup
                            return true;
                        }

                        // Skip prázdne process names
                        if (string.IsNullOrEmpty(processName))
                            return true;

                        // Vytvor WindowTrackingInfo objekt
                        var windowInfo = new WindowTrackingInfo
                        {
                            WindowHandle = hWnd,
                            Title = title,
                            ClassName = className,
                            ProcessName = processName,
                            ProcessId = (int)processId,
                            DetectedAt = DateTime.Now,
                            IsVisible = IsWindowVisible(hWnd),
                            IsEnabled = IsWindowEnabled(hWnd),
                            WindowType = DetermineWindowType(title, className, hWnd)
                        };

                        // Získaj rozmery
                        if (GetWindowRect(hWnd, out RECT rect))
                        {
                            windowInfo.Left = rect.Left;
                            windowInfo.Top = rect.Top;
                            windowInfo.Width = rect.Right - rect.Left;
                            windowInfo.Height = rect.Bottom - rect.Top;
                        }

                        windows.Add(windowInfo);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error processing window {hWnd}: {ex.Message}");
                    }

                    return true; // Pokračuj v enumerácii
                }, IntPtr.Zero);

                // Zoraď windows podľa procesu a titulku
                return windows
                    .OrderBy(w => w.ProcessName)
                    .ThenBy(w => w.Title)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enumerating windows: {ex.Message}");
                return new List<WindowTrackingInfo>();
            }
        }

        /// <summary>
        /// NOVÉ: Nájde okno podľa názvu procesu a časti titulku (nahradí WindowFinder)
        /// </summary>
        public static IntPtr FindWindowByProcessAndTitle(string processName, string partialTitle = null)
        {
            var processes = Process.GetProcessesByName(processName);

            foreach (var process in processes)
            {
                try
                {
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        string windowTitle = GetWindowTitle(process.MainWindowHandle);

                        if (string.IsNullOrEmpty(partialTitle) ||
                            windowTitle.IndexOf(partialTitle, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return process.MainWindowHandle;
                        }
                    }
                }
                catch
                {
                    // Proces možno už neexistuje
                    continue;
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// NOVÉ: Nájde okno podľa class name a titulku (nahradí WindowFinder)
        /// </summary>
        public static IntPtr FindWindowByClassAndTitle(string className, string windowTitle = null)
        {
            return FindWindow(className, windowTitle);
        }

        /// <summary>
        /// NOVÉ: Nájde všetky okná patriace procesu (nahradí WindowFinder)
        /// </summary>
        public static List<WindowTrackingInfo> GetProcessWindows(string processName)
        {
            var windows = new List<WindowTrackingInfo>();
            var processes = Process.GetProcessesByName(processName);

            foreach (var process in processes)
            {
                try
                {
                    EnumWindows((hWnd, lParam) =>
                    {
                        GetWindowThreadProcessId(hWnd, out uint processId);

                        if (processId == process.Id && IsWindowVisible(hWnd))
                        {
                            string title = GetWindowTitle(hWnd);
                            string className = GetClassName(hWnd);

                            if (!string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(className))
                            {
                                windows.Add(new WindowTrackingInfo
                                {
                                    WindowHandle = hWnd,
                                    Title = title,
                                    ClassName = className,
                                    ProcessId = (int)processId,
                                    ProcessName = processName,
                                    DetectedAt = DateTime.Now,
                                    IsVisible = true,
                                    IsEnabled = IsWindowEnabled(hWnd),
                                    WindowType = DetermineWindowType(title, className, hWnd)
                                });
                            }
                        }

                        return true; // Pokračuj v enumerácii
                    }, IntPtr.Zero);
                }
                catch
                {
                    continue;
                }
            }

            return windows;
        }

        /// <summary>
        /// NOVÉ: Inteligentné hľadanie okna s fallback možnosťami (nahradí WindowFinder)
        /// </summary>
        public static WindowSearchResult SmartFindWindow(string processName, string windowTitle = null, string className = null)
        {
            var result = new WindowSearchResult();

            // Pokus 1: Presný match podľa procesu a titulku
            if (!string.IsNullOrEmpty(processName) && !string.IsNullOrEmpty(windowTitle))
            {
                result.Handle = FindWindowByProcessAndTitle(processName, windowTitle);
                if (result.Handle != IntPtr.Zero)
                {
                    result.MatchMethod = "ProcessName + WindowTitle";
                    result.Confidence = 0.95;
                    return result;
                }
            }

            // Pokus 2: Iba názov procesu (prvé hlavné okno)
            if (!string.IsNullOrEmpty(processName))
            {
                result.Handle = FindWindowByProcessAndTitle(processName);
                if (result.Handle != IntPtr.Zero)
                {
                    result.MatchMethod = "ProcessName only";
                    result.Confidence = 0.80;
                    return result;
                }
            }

            // Pokus 3: Class name a titulok
            if (!string.IsNullOrEmpty(className))
            {
                result.Handle = FindWindowByClassAndTitle(className, windowTitle);
                if (result.Handle != IntPtr.Zero)
                {
                    result.MatchMethod = "ClassName + WindowTitle";
                    result.Confidence = 0.70;
                    return result;
                }
            }

            result.MatchMethod = "No match found";
            result.Confidence = 0.0;
            return result;
        }

        // === HELPER METÓDY ===

        /// <summary>
        /// Kontroluje či je okno systémové (ktoré chceme preskočiť)
        /// </summary>
        private static bool IsSystemWindow(string className)
        {
            var systemClasses = new[]
            {
                "Shell_TrayWnd", "DV2ControlHost", "MsgrIMEWindowClass", "SysShadow",
                "Button", "Progman", "WorkerW", "Desktop", "ForegroundStaging",
                "ApplicationManager_DesktopShellWindow", "Windows.UI.Core.CoreWindow"
            };

            return systemClasses.Contains(className);
        }

        /// <summary>
        /// Určí typ okna na základe vlastností
        /// </summary>
        private static WindowType DetermineWindowType(string title, string className, IntPtr handle)
        {
            // MessageBox detection
            if (className.Contains("MessageBox") || className.Contains("#32770"))
                return WindowType.MessageBox;

            // Dialog detection
            if (className.Contains("Dialog") || title.Contains("Dialog") ||
                className.Contains("Window") && GetParent(handle) != IntPtr.Zero)
                return WindowType.Dialog;

            // Child window detection
            if (GetParent(handle) != IntPtr.Zero)
                return WindowType.ChildWindow;

            return WindowType.MainWindow;
        }

        // === EXISTUJÚCE METÓDY (zachované) ===

        public void StartTracking(IntPtr primaryWindow, string processName = "")
        {
            primaryTargetWindow = primaryWindow;
            targetProcessName = processName;
            isTracking = true;

            windowMonitorTimer.Change(MonitoringIntervalMs, MonitoringIntervalMs);
            System.Diagnostics.Debug.WriteLine($"🔍 WindowTracker started for process: {processName}");
        }

        public void StopTracking()
        {
            isTracking = false;
            windowMonitorTimer.Change(Timeout.Infinite, Timeout.Infinite);
            System.Diagnostics.Debug.WriteLine("🛑 WindowTracker stopped");
        }

        // ... zvyšok existujúcich metód zostáva rovnaký ...

        // === STATIC HELPER METÓDY ===

        private static string GetWindowTitle(IntPtr hWnd)
        {
            var title = new System.Text.StringBuilder(256);
            GetWindowText(hWnd, title, title.Capacity);
            return title.ToString();
        }

        private static string GetClassName(IntPtr hWnd)
        {
            var className = new System.Text.StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            return className.ToString();
        }

        // === WIN32 API ===

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowEnabled(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        ~WindowTracker()
        {
            StopTracking();
            windowMonitorTimer?.Dispose();
        }
    }

    // === SUPPORTING CLASSES ===

    /// <summary>
    /// Výsledok inteligentného hľadania okna (nahradí WindowFinder)
    /// </summary>
    public class WindowSearchResult
    {
        public IntPtr Handle { get; set; } = IntPtr.Zero;
        public string MatchMethod { get; set; } = "";
        public double Confidence { get; set; } = 0.0;
        public bool IsSuccess => Handle != IntPtr.Zero;
    }

    // Supporting classes and enums (existujúce, zachované)
    public enum WindowType
    {
        MainWindow,
        Dialog,
        MessageBox,
        ChildWindow,
        Unknown
    }

    public class WindowTrackingInfo
    {
        public IntPtr WindowHandle { get; set; }
        public string Title { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public int ProcessId { get; set; }
        public WindowType WindowType { get; set; }
        public bool IsModal { get; set; }
        public bool IsPrimaryTarget { get; set; }
        public bool IsActive { get; set; }
        public bool IsVisible { get; set; }
        public bool IsEnabled { get; set; }
        public IntPtr ParentWindow { get; set; }
        public DateTime DetectedAt { get; set; }
        public DateTime LastActivated { get; set; }
        public int Left { get; set; }
        public int Top { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public override string ToString()
        {
            return $"{WindowType}: {Title} ({ProcessName})";
        }
    }

    // Event argument classes (existujúce, zachované)
    public class NewWindowDetectedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public WindowTrackingInfo WindowInfo { get; set; }
        public string DetectionMethod { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class WindowClosedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public WindowTrackingInfo WindowInfo { get; set; }
    }

    public class WindowActivatedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public WindowTrackingInfo WindowInfo { get; set; }
    }
}
