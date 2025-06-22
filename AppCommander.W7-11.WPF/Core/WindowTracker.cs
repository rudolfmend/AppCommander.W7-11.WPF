using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;

namespace AppCommander.W7_11.WPF.Core
{
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

        public WindowTrackingInfo()
        {
            knownWindows = new HashSet<IntPtr>();
            trackedWindows = new Dictionary<IntPtr, WindowTrackingInfo>();
            windowMonitorTimer = new Timer(MonitorWindows, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Získa všetky viditeľné okná v systéme
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
                            WindowType = DetermineWindowTypeStatic(title, className, hWnd)
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
        /// Nájde okno podľa názvu procesu a časti titulku
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
        /// Nájde okno podľa class name a titulku
        /// </summary>
        public static IntPtr FindWindowByClassAndTitle(string className, string windowTitle = null)
        {
            return FindWindow(className, windowTitle);
        }

        /// <summary>
        /// Nájde všetky okná patriace procesu
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
                                    WindowType = DetermineWindowTypeStatic(title, className, hWnd)
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
        /// Inteligentné hľadanie okna s fallback možnosťami
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

        // === EXISTUJÚCE METÓDY (zachované zo starého WindowTracker) ===

        public void StartTracking(IntPtr primaryWindow, string processName = "")
        {
            primaryTargetWindow = primaryWindow;
            targetProcessName = processName;
            isTracking = true;

            // Inicializuj známe okná
            InitializeKnownWindows();

            windowMonitorTimer.Change(MonitoringIntervalMs, MonitoringIntervalMs);
            System.Diagnostics.Debug.WriteLine($"🔍 WindowTracker started for process: {processName}");
        }

        public void StopTracking()
        {
            isTracking = false;
            windowMonitorTimer.Change(Timeout.Infinite, Timeout.Infinite);
            System.Diagnostics.Debug.WriteLine("🛑 WindowTracker stopped");
        }

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

        public List<WindowTrackingInfo> GetTrackedWindows()
        {
            return trackedWindows.Values.ToList();
        }

        public bool IsWindowTracked(IntPtr windowHandle)
        {
            return trackedWindows.ContainsKey(windowHandle);
        }

        public WindowTrackingInfo GetWindowInfo(IntPtr windowHandle)
        {
            return trackedWindows.TryGetValue(windowHandle, out var info) ? info : null;
        }

        // === PRIVATE METÓDY ===

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
                    return true;
                default:
                    return false;
            }
        }

        private string DetermineDetectionMethod(WindowTrackingInfo windowInfo)
        {
            if (windowInfo.WindowType == WindowType.Dialog)
                return "Dialog Detection";
            if (windowInfo.WindowType == WindowType.MessageBox)
                return "MessageBox Detection";
            if (windowInfo.ParentWindow != IntPtr.Zero)
                return "Child Window Detection";
            return "Standard Window Detection";
        }

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
                    ClassName = GetClassName(windowHandle),
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

        private WindowType DetermineWindowType(WindowTrackingInfo info)
        {
            return DetermineWindowTypeStatic(info.Title, info.ClassName, info.WindowHandle);
        }

        private void CheckForClosedWindows(List<IntPtr> currentWindows)
        {
            var closedWindows = trackedWindows.Keys.Where(w => !currentWindows.Contains(w)).ToList();

            foreach (var closedWindow in closedWindows)
            {
                var windowInfo = trackedWindows[closedWindow];
                trackedWindows.Remove(closedWindow);
                knownWindows.Remove(closedWindow);

                System.Diagnostics.Debug.WriteLine($"Window closed: {windowInfo.Title}");

                WindowClosed?.Invoke(this, new WindowClosedEventArgs
                {
                    WindowHandle = closedWindow,
                    WindowInfo = windowInfo
                });
            }
        }

        private void CheckForActivatedWindows()
        {
            try
            {
                var foregroundWindow = GetForegroundWindow();
                if (foregroundWindow != IntPtr.Zero && trackedWindows.ContainsKey(foregroundWindow))
                {
                    var windowInfo = trackedWindows[foregroundWindow];
                    if (!windowInfo.IsActive)
                    {
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

        private bool IsModalWindow(IntPtr windowHandle)
        {
            try
            {
                long exStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
                return (exStyle & WS_EX_DLGMODALFRAME) != 0;
            }
            catch
            {
                return false;
            }
        }

        // === STATICKÉ HELPER METÓDY ===

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
        /// Určí typ okna na základe vlastností (statická verzia)
        /// </summary>
        private static WindowType DetermineWindowTypeStatic(string title, string className, IntPtr handle)
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

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

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

        ~WindowTrackingInfo()
        {
            StopTracking();
            windowMonitorTimer?.Dispose();
        }

        // === HELPER METÓDY ===

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

        internal static IntPtr WaitForApplication(string targetProcessName, int maxWaitTimeSeconds)
        {
            throw new NotImplementedException();
        }
    }

    // === SUPPORTING CLASSES ===

    /// <summary>
    /// Výsledok inteligentného hľadania okna
    /// </summary>
    public class WindowSearchResult
    {
    public IntPtr Handle { get; set; } = IntPtr.Zero;
    public string MatchMethod { get; set; } = "";
    public double Confidence { get; set; } = 0.0;
    public bool IsSuccess => Handle != IntPtr.Zero;

        public bool IsValid { get; internal set; }
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



    // Event argument classes (existujúce, zachované)
    public class NewWindowDetectedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public WindowTrackingInfo WindowInfo { get; set; }
        public string DetectionMethod { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
