using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace AppCommander.W7_11.WPF.Core
{
    /// <summary>
    /// Sleduje a automaticky detekuje nové okná počas nahrávania
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
        public int MonitoringIntervalMs { get; set; } = 500; // Kontrola každých 500ms
        public bool TrackChildWindows { get; set; } = true;
        public bool TrackDialogs { get; set; } = true;
        public bool TrackMessageBoxes { get; set; } = true;
        public bool TrackOnlyTargetProcess { get; set; } = true; // Sleduj len okná z target procesu

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

        /// <summary>
        /// Začne sledovanie okien pre zadaný target proces
        /// </summary>
        public void StartTracking(IntPtr primaryTarget, string processName = "")
        {
            if (isTracking) return;

            System.Diagnostics.Debug.WriteLine("=== WINDOW TRACKER STARTED ===");

            primaryTargetWindow = primaryTarget;
            targetProcessName = processName;
            isTracking = true;

            // Inicializuj známe okná
            InitializeKnownWindows();

            // Spusti monitoring timer
            windowMonitorTimer.Change(MonitoringIntervalMs, MonitoringIntervalMs);

            System.Diagnostics.Debug.WriteLine($"Tracking started for process: {targetProcessName}");
            System.Diagnostics.Debug.WriteLine($"Primary target: {primaryTargetWindow}");
            System.Diagnostics.Debug.WriteLine($"Known windows: {knownWindows.Count}");
        }

        /// <summary>
        /// Zastaví sledovanie okien
        /// </summary>
        public void StopTracking()
        {
            if (!isTracking) return;

            windowMonitorTimer.Change(Timeout.Infinite, Timeout.Infinite);
            isTracking = false;

            System.Diagnostics.Debug.WriteLine("=== WINDOW TRACKER STOPPED ===");
            System.Diagnostics.Debug.WriteLine($"Tracked {trackedWindows.Count} additional windows during session");

            // Cleanup
            knownWindows.Clear();
            trackedWindows.Clear();
        }

        /// <summary>
        /// Manuálne pridá okno do tracking listu
        /// </summary>
        public void AddWindow(IntPtr windowHandle, string description = "")
        {
            if (windowHandle == IntPtr.Zero || knownWindows.Contains(windowHandle))
                return;

            var windowInfo = AnalyzeWindow(windowHandle);
            if (windowInfo != null)
            {
                knownWindows.Add(windowHandle);
                trackedWindows[windowHandle] = windowInfo;

                System.Diagnostics.Debug.WriteLine($"Manually added window: {windowInfo.Title} ({windowInfo.ProcessName})");

                // Trigger event
                NewWindowDetected?.Invoke(this, new NewWindowDetectedEventArgs
                {
                    WindowHandle = windowHandle,
                    WindowInfo = windowInfo,
                    DetectionMethod = "Manual",
                    Description = description
                });
            }
        }

        /// <summary>
        /// Získa aktuálne sledované okná
        /// </summary>
        public List<WindowTrackingInfo> GetTrackedWindows()
        {
            return trackedWindows.Values.ToList();
        }

        /// <summary>
        /// Kontroluje či je okno sledované
        /// </summary>
        public bool IsWindowTracked(IntPtr windowHandle)
        {
            return trackedWindows.ContainsKey(windowHandle);
        }

        /// <summary>
        /// Získa informácie o sledovanom okne
        /// </summary>
        public WindowTrackingInfo GetWindowInfo(IntPtr windowHandle)
        {
            return trackedWindows.TryGetValue(windowHandle, out var info) ? info : null;
        }

        /// <summary>
        /// Inicializuje zoznam známych okien
        /// </summary>
        private void InitializeKnownWindows()
        {
            try
            {
                // Pridaj primary target
                if (primaryTargetWindow != IntPtr.Zero)
                {
                    knownWindows.Add(primaryTargetWindow);
                    var primaryInfo = AnalyzeWindow(primaryTargetWindow);
                    if (primaryInfo != null)
                    {
                        trackedWindows[primaryTargetWindow] = primaryInfo;
                        primaryInfo.IsPrimaryTarget = true;
                    }
                }

                // Pridaj všetky aktuálne okná do known listu
                var allWindows = GetAllVisibleWindows();
                foreach (var window in allWindows)
                {
                    knownWindows.Add(window);
                }

                System.Diagnostics.Debug.WriteLine($"Initialized {knownWindows.Count} known windows");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing known windows: {ex.Message}");
            }
        }

        /// <summary>
        /// Timer callback pre monitoring okien
        /// </summary>
        private void MonitorWindows(object state)
        {
            if (!isTracking) return;

            try
            {
                var currentWindows = GetAllVisibleWindows();
                var newWindows = currentWindows.Where(w => !knownWindows.Contains(w)).ToList();

                foreach (var newWindow in newWindows)
                {
                    ProcessNewWindow(newWindow);
                }

                // Skontroluj zatvorené okná
                CheckForClosedWindows(currentWindows);

                // Skontroluj aktivované okná
                CheckForActivatedWindows();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in window monitoring: {ex.Message}");
            }
        }

        /// <summary>
        /// Spracuje novo objavené okno
        /// </summary>
        private void ProcessNewWindow(IntPtr windowHandle)
        {
            try
            {
                var windowInfo = AnalyzeWindow(windowHandle);
                if (windowInfo == null) return;

                // Rozhodni či sledovať toto okno
                if (ShouldTrackWindow(windowInfo))
                {
                    knownWindows.Add(windowHandle);
                    trackedWindows[windowHandle] = windowInfo;

                    string detectionMethod = DetermineDetectionMethod(windowInfo);

                    System.Diagnostics.Debug.WriteLine($"=== NEW WINDOW DETECTED ===");
                    System.Diagnostics.Debug.WriteLine($"Title: {windowInfo.Title}");
                    System.Diagnostics.Debug.WriteLine($"Process: {windowInfo.ProcessName}");
                    System.Diagnostics.Debug.WriteLine($"Class: {windowInfo.ClassName}");
                    System.Diagnostics.Debug.WriteLine($"Type: {windowInfo.WindowType}");
                    System.Diagnostics.Debug.WriteLine($"Method: {detectionMethod}");

                    // Trigger event
                    NewWindowDetected?.Invoke(this, new NewWindowDetectedEventArgs
                    {
                        WindowHandle = windowHandle,
                        WindowInfo = windowInfo,
                        DetectionMethod = detectionMethod,
                        Description = $"Auto-detected {windowInfo.WindowType}: {windowInfo.Title}"
                    });
                }
                else
                {
                    // Pridaj do known ale nesleduj
                    knownWindows.Add(windowHandle);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing new window: {ex.Message}");
            }
        }

        /// <summary>
        /// Rozhodne či sledovať okno na základe konfigurácie
        /// </summary>
        private bool ShouldTrackWindow(WindowTrackingInfo windowInfo)
        {
            // Ak sledujeme len target proces
            if (TrackOnlyTargetProcess && !string.IsNullOrEmpty(targetProcessName))
            {
                if (!windowInfo.ProcessName.Equals(targetProcessName, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Filter podľa typu okna
            switch (windowInfo.WindowType)
            {
                case WindowType.Dialog:
                    return TrackDialogs;
                case WindowType.MessageBox:
                    return TrackMessageBoxes;
                case WindowType.ChildWindow:
                    return TrackChildWindows;
                case WindowType.MainWindow:
                    return true; // Hlavné okná sleduj vždy
                default:
                    return false;
            }
        }

        /// <summary>
        /// Určí metódu detekcie okna
        /// </summary>
        private string DetermineDetectionMethod(WindowTrackingInfo windowInfo)
        {
            if (windowInfo.WindowType == WindowType.MessageBox)
                return "MessageBox Detection";
            if (windowInfo.WindowType == WindowType.Dialog)
                return "Dialog Detection";
            if (windowInfo.IsModal)
                return "Modal Window Detection";
            if (windowInfo.ProcessName.Equals(targetProcessName, StringComparison.OrdinalIgnoreCase))
                return "Target Process Window";

            return "General Window Detection";
        }

        /// <summary>
        /// Kontroluje zatvorené okná
        /// </summary>
        private void CheckForClosedWindows(List<IntPtr> currentWindows)
        {
            var closedWindows = trackedWindows.Keys.Where(w => !currentWindows.Contains(w)).ToList();

            foreach (var closedWindow in closedWindows)
            {
                var windowInfo = trackedWindows[closedWindow];
                trackedWindows.Remove(closedWindow);
                knownWindows.Remove(closedWindow);

                System.Diagnostics.Debug.WriteLine($"Window closed: {windowInfo.Title} ({windowInfo.ProcessName})");

                WindowClosed?.Invoke(this, new WindowClosedEventArgs
                {
                    WindowHandle = closedWindow,
                    WindowInfo = windowInfo
                });
            }
        }

        /// <summary>
        /// Kontroluje aktivované okná
        /// </summary>
        private void CheckForActivatedWindows()
        {
            try
            {
                IntPtr foregroundWindow = GetForegroundWindow();
                if (foregroundWindow != IntPtr.Zero && trackedWindows.ContainsKey(foregroundWindow))
                {
                    var windowInfo = trackedWindows[foregroundWindow];
                    if (!windowInfo.IsActive)
                    {
                        // Označ ako aktívne
                        windowInfo.IsActive = true;
                        windowInfo.LastActivated = DateTime.Now;

                        // Resetuj ostatné
                        foreach (var other in trackedWindows.Values.Where(w => w.WindowHandle != foregroundWindow))
                        {
                            other.IsActive = false;
                        }

                        System.Diagnostics.Debug.WriteLine($"Window activated: {windowInfo.Title}");

                        WindowActivated?.Invoke(this, new WindowActivatedEventArgs
                        {
                            WindowHandle = foregroundWindow,
                            WindowInfo = windowInfo
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking activated windows: {ex.Message}");
            }
        }

        /// <summary>
        /// Analyzuje okno a vytvorí WindowTrackingInfo
        /// </summary>
        private WindowTrackingInfo AnalyzeWindow(IntPtr windowHandle)
        {
            try
            {
                if (!IsWindow(windowHandle) || !IsWindowVisible(windowHandle))
                    return null;

                var info = new WindowTrackingInfo
                {
                    WindowHandle = windowHandle,
                    Title = GetWindowTitle(windowHandle),
                    ClassName = GetWindowClassName(windowHandle),
                    ProcessName = GetProcessName(windowHandle),
                    ProcessId = GetProcessId(windowHandle),
                    DetectedAt = DateTime.Now,
                    IsVisible = IsWindowVisible(windowHandle),
                    IsEnabled = IsWindowEnabled(windowHandle)
                };

                // Analyzuj typ okna
                info.WindowType = DetermineWindowType(info);
                info.IsModal = IsModalWindow(windowHandle);
                info.ParentWindow = GetParent(windowHandle);

                // Získaj rozmery
                if (GetWindowRect(windowHandle, out RECT rect))
                {
                    info.Left = rect.Left;
                    info.Top = rect.Top;
                    info.Width = rect.Right - rect.Left;
                    info.Height = rect.Bottom - rect.Top;
                }

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error analyzing window: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Určí typ okna
        /// </summary>
        private WindowType DetermineWindowType(WindowTrackingInfo info)
        {
            // MessageBox detection
            if (info.ClassName.Contains("MessageBox") ||
                info.ClassName == "#32770" || // Standard dialog class
                (info.Title.Contains("Error") || info.Title.Contains("Warning") ||
                 info.Title.Contains("Information") || info.Title.Contains("Confirm")))
            {
                return WindowType.MessageBox;
            }

            // Dialog detection
            if (info.ClassName.Contains("Dialog") ||
                info.ClassName == "#32770" ||
                info.Title.Contains("Dialog") ||
                info.Width < 600 && info.Height < 400) // Malé okná sú často dialógy
            {
                return WindowType.Dialog;
            }

            // Child window detection
            if (info.ParentWindow != IntPtr.Zero)
            {
                return WindowType.ChildWindow;
            }

            return WindowType.MainWindow;
        }

        /// <summary>
        /// Kontroluje či je okno modálne
        /// </summary>
        private bool IsModalWindow(IntPtr windowHandle)
        {
            try
            {
                // Modálne okná majú často nastavený WS_EX_DLGMODALFRAME štýl
                long exStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
                return (exStyle & WS_EX_DLGMODALFRAME) != 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Získa všetky viditeľné okná
        /// </summary>
        private List<IntPtr> GetAllVisibleWindows()
        {
            var windows = new List<IntPtr>();

            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    windows.Add(hWnd);
                }
                return true;
            }, IntPtr.Zero);

            return windows;
        }

        // Helper methods
        private string GetWindowTitle(IntPtr hWnd)
        {
            var title = new System.Text.StringBuilder(256);
            GetWindowText(hWnd, title, title.Capacity);
            return title.ToString();
        }

        private string GetWindowClassName(IntPtr hWnd)
        {
            var className = new System.Text.StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            return className.ToString();
        }

        private string GetProcessName(IntPtr hWnd)
        {
            try
            {
                GetWindowThreadProcessId(hWnd, out uint processId);
                using (var process = Process.GetProcessById((int)processId))
                {
                    return process.ProcessName;
                }
            }
            catch
            {
                return "Unknown";
            }
        }

        private int GetProcessId(IntPtr hWnd)
        {
            GetWindowThreadProcessId(hWnd, out uint processId);
            return (int)processId;
        }

        ~WindowTracker()
        {
            StopTracking();
            windowMonitorTimer?.Dispose();
        }

        // Windows API
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowEnabled(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern long GetWindowLong(IntPtr hWnd, int nIndex);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_DLGMODALFRAME = 0x00000001L;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }

    // Supporting classes and enums
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

    // Event argument classes
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
