// WindowTracker.cs - OPRAVENÝ bez duplikátnych event args
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AppCommander.W7_11.WPF.Core;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace AppCommander.W7_11.WPF.Core
{
    public class WindowTracker : IDisposable
    {
        #region Win32 API Declarations

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        #endregion

        #region Private Fields

        private readonly HashSet<IntPtr> knownWindows;
        private readonly Dictionary<IntPtr, WindowTrackingInfo> trackedWindows;
        private readonly Timer windowMonitorTimer;
        private string targetProcessName;
        private bool isTracking;
        private bool disposed;

        #endregion

        #region Properties

        public int MonitoringIntervalMs { get; set; } = 1000;
        public bool TrackOnlyTargetProcess { get; set; } = false;

        #endregion

        #region Events

        public event EventHandler<NewWindowDetectedEventArgs> NewWindowDetected;
        public event EventHandler<WindowClosedEventArgs> WindowClosed;
        public event EventHandler<WindowActivatedEventArgs> WindowActivated;

        #endregion

        #region Constructor

        public WindowTracker()
        {
            knownWindows = new HashSet<IntPtr>();
            trackedWindows = new Dictionary<IntPtr, WindowTrackingInfo>();
            windowMonitorTimer = new Timer(MonitorWindows, null, Timeout.Infinite, Timeout.Infinite);
        }

        #endregion

        #region Public Methods

        public void StartTracking(string processName = "")
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(WindowTracker));

            if (isTracking) return;

            targetProcessName = processName;
            isTracking = true;

            try
            {
                windowMonitorTimer.Change(MonitoringIntervalMs, MonitoringIntervalMs);
                System.Diagnostics.Debug.WriteLine($"🔍 WindowTracker started for process: {processName}");
            }
            catch (ObjectDisposedException)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Timer already disposed");
            }
        }

        public void StopTracking()
        {
            if (!isTracking) return;

            isTracking = false;

            try
            {
                windowMonitorTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                System.Diagnostics.Debug.WriteLine("🛑 WindowTracker stopped");
            }
            catch (ObjectDisposedException)
            {
                // Timer už bol disposed, to je OK
            }
        }

        #endregion

        #region Private Methods

        private void MonitorWindows(object state)
        {
            if (!isTracking || disposed) return;

            try
            {
                var currentWindows = GetAllWindows();
                CheckForNewWindows(currentWindows);
                CheckForClosedWindows(currentWindows);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error monitoring windows: {ex.Message}");
            }
        }

        private List<IntPtr> GetAllWindows()
        {
            var windows = new List<IntPtr>();

            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd) && !IsSystemWindow(GetWindowClassName(hWnd)))
                {
                    if (TrackOnlyTargetProcess && !string.IsNullOrEmpty(targetProcessName))
                    {
                        var processName = GetProcessName(hWnd);
                        if (processName.Equals(targetProcessName, StringComparison.OrdinalIgnoreCase))
                        {
                            windows.Add(hWnd);
                        }
                    }
                    else
                    {
                        windows.Add(hWnd);
                    }
                }
                return true;
            }, IntPtr.Zero);

            return windows;
        }

        private void CheckForNewWindows(List<IntPtr> currentWindows)
        {
            foreach (var hWnd in currentWindows.Where(w => !knownWindows.Contains(w)))
            {
                knownWindows.Add(hWnd);
                var windowInfo = CreateWindowTrackingInfo(hWnd);
                trackedWindows[hWnd] = windowInfo;

                NewWindowDetected?.Invoke(this, new NewWindowDetectedEventArgs
                {
                    Window = windowInfo
                });
            }
        }

        private void CheckForClosedWindows(List<IntPtr> currentWindows)
        {
            var closedWindows = knownWindows.Where(w => !currentWindows.Contains(w)).ToList();

            foreach (var hWnd in closedWindows)
            {
                knownWindows.Remove(hWnd);
                if (trackedWindows.TryGetValue(hWnd, out var windowInfo))
                {
                    trackedWindows.Remove(hWnd);

                    WindowClosed?.Invoke(this, new WindowClosedEventArgs
                    {
                        Window = windowInfo
                    });
                }
            }
        }

        private WindowTrackingInfo CreateWindowTrackingInfo(IntPtr hWnd)
        {
            GetWindowThreadProcessId(hWnd, out uint processId);

            return new WindowTrackingInfo
            {
                WindowHandle = hWnd,
                Title = GetWindowTitle(hWnd),
                ProcessName = GetProcessName(hWnd),
                ClassName = GetWindowClassName(hWnd),
                WindowType = DetermineWindowType(hWnd),
                DetectedAt = DateTime.Now,
                ProcessId = processId,
                IsVisible = IsWindowVisible(hWnd),
                IsActive = false, // Bude aktualizované podľa potreby
                IsEnabled = true,
                IsModal = false,
                Width = 0, // Môže byť doplnené GetWindowRect API
                Height = 0,
                LastActivated = DateTime.Now
            };
        }

        private string GetWindowTitle(IntPtr hWnd)
        {
            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private string GetProcessName(IntPtr hWnd)
        {
            try
            {
                GetWindowThreadProcessId(hWnd, out uint processId);
                var process = Process.GetProcessById((int)processId);
                return process.ProcessName;
            }
            catch
            {
                return "Unknown";
            }
        }

        private string GetWindowClassName(IntPtr hWnd)
        {
            var sb = new StringBuilder(256);
            GetClassName(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private WindowType DetermineWindowType(IntPtr hWnd)
        {
            var className = GetWindowClassName(hWnd);

            if (className.Contains("Dialog"))
                return WindowType.Dialog;
            if (className.Contains("MessageBox"))
                return WindowType.MessageBox;
            if (className.Contains("Popup"))
                return WindowType.Popup;

            return WindowType.Standard;
        }

        private bool IsSystemWindow(string className)
        {
            var systemClasses = new[]
            {
                "Shell_TrayWnd", "DV2ControlHost", "MsgrIMEWindowClass",
                "SysShadow", "Button", "Progman", "Windows.UI.Core.CoreWindow"
            };

            return systemClasses.Any(sc => className.Contains(sc));
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    try
                    {
                        StopTracking();
                        windowMonitorTimer?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Error disposing WindowTracker: {ex.Message}");
                    }
                }

                disposed = true;
            }
        }

        ~WindowTracker()
        {
            System.Diagnostics.Debug.WriteLine("⚠️ WindowTracker finalizer called - object was not properly disposed");
        }

        #endregion
    }

    #region Supporting Classes

    public class WindowTrackingInfo
    {
        public IntPtr WindowHandle { get; set; }
        public string Title { get; set; }
        public string ProcessName { get; set; }
        public string ClassName { get; set; }
        public WindowType WindowType { get; set; }
        public DateTime DetectedAt { get; set; }

        // Dodatočné vlastnosti potrebné pre kód
        public bool IsModal { get; set; }
        public bool IsVisible { get; set; }
        public bool IsActive { get; set; }
        public bool IsEnabled { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public uint ProcessId { get; set; }
        public DateTime LastActivated { get; set; }

        // Eventy (pre kompatibilitu s existujúcim kódom)
        public event EventHandler<NewWindowDetectedEventArgs> NewWindowDetected;
        public event EventHandler<WindowActivatedEventArgs> WindowActivated;
        public event EventHandler<WindowClosedEventArgs> WindowClosed;

        // Metódy pre kompatibilitu
        public void StartTracking(string processName = "") { }
        public void StopTracking() { }
        public List<IntPtr> GetAllWindows() { return new List<IntPtr>(); }
        public WindowTrackingInfo SmartFindWindow(string criteria) { return this; }
        public void WaitForApplication(string processName) { }
        public bool TrackOnlyTargetProcess { get; set; }
    }

    // POZNÁMKA: Event args triedy sú teraz iba v EventArgs.cs
    // Odstránené duplikáty: WindowActivatedEventArgs, WindowClosedEventArgs

    public class NewWindowDetectedEventArgs : EventArgs
    {
        public WindowTrackingInfo Window { get; set; }
        public WindowTrackingInfo WindowInfo { get; set; }
        public string Description { get; set; }
    }

    public enum WindowType
    {
        Standard,
        Dialog,
        MessageBox,
        Popup,
        Child,
        Tool,
        ChildWindow,
        MainWindow
    }

    #endregion
}
