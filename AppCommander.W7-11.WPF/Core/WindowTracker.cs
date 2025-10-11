using AppCommander.W7_11.WPF.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Automation;

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

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        #endregion

        #region Private Fields

        private readonly HashSet<IntPtr> knownWindows;
        private readonly Dictionary<IntPtr, WindowTrackingInfo> trackedWindows;
        private readonly Timer windowMonitorTimer;
        private string targetProcessName;
        private bool isTracking;
        private bool disposed;
        private IntPtr lastActiveWindow = IntPtr.Zero;

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
            if (isTracking) return;

            targetProcessName = processName;
            isTracking = true;

            // Inicializácia so súčasnými oknami
            var currentWindows = GetAllWindows();
            foreach (var hWnd in currentWindows)
            {
                knownWindows.Add(hWnd);
                var windowInfo = CreateWindowTrackingInfo(hWnd);
                trackedWindows[hWnd] = windowInfo;
            }

            // Spusti sledovanie
            windowMonitorTimer.Change(0, MonitoringIntervalMs);
            System.Diagnostics.Debug.WriteLine($"🎯 WindowTracker started - Target: '{processName}'");
        }

        public void StartTracking(IntPtr primaryWindow, string processName = "")
        {
            StartTracking(processName);

            // Ak je poskytnuté primárne okno, pridaj ho do sledovania
            if (primaryWindow != IntPtr.Zero && !knownWindows.Contains(primaryWindow))
            {
                knownWindows.Add(primaryWindow);
                var windowInfo = CreateWindowTrackingInfo(primaryWindow);
                trackedWindows[primaryWindow] = windowInfo;
            }
        }

        public void StopTracking()
        {
            if (!isTracking) return;

            isTracking = false;
            windowMonitorTimer.Change(Timeout.Infinite, Timeout.Infinite);
            System.Diagnostics.Debug.WriteLine("🛑 WindowTracker stopped");
        }

        public List<IntPtr> GetAllWindows()
        {
            var windows = new List<IntPtr>();
            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    if (string.IsNullOrEmpty(targetProcessName) || !TrackOnlyTargetProcess)
                    {
                        windows.Add(hWnd);
                    }
                    else
                    {
                        var processName = GetProcessName(hWnd);
                        if (processName.IndexOf(targetProcessName, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            windows.Add(hWnd);
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);

            return windows;
        }

        public WindowTrackingInfo SmartFindWindow(string criteria)
        {
            var windows = GetAllWindows();
            foreach (var hWnd in windows)
            {
                var windowInfo = CreateWindowTrackingInfo(hWnd);
                if ((windowInfo.Title?.IndexOf(criteria, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (windowInfo.ProcessName?.IndexOf(criteria, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (windowInfo.ClassName?.IndexOf(criteria, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return windowInfo;
                }
            }
            return null;
        }

        public void WaitForApplication(string processName)
        {
            while (isTracking)
            {
                var windows = GetAllWindows();
                foreach (var hWnd in windows)
                {
                    var windowProcessName = GetProcessName(hWnd);
                    if (windowProcessName.IndexOf(processName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return; // Application found
                    }
                }
                Thread.Sleep(500);
            }
        }

        #endregion

        #region Private Methods

        private void MonitorWindows(object state)
        {
            if (!isTracking) return;

            try
            {
                var currentWindows = GetAllWindows();

                // Kontrola nových okien
                CheckForNewWindows(currentWindows);

                // Kontrola zatvorených okien
                CheckForClosedWindows(currentWindows);

                // Kontrola aktivovaných okien
                CheckForActivatedWindow();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in MonitorWindows: {ex.Message}");
            }
        }

        private void CheckForNewWindows(List<IntPtr> currentWindows)
        {
            foreach (var hWnd in currentWindows.Where(w => !knownWindows.Contains(w)))
            {
                knownWindows.Add(hWnd);
                var windowInfo = CreateWindowTrackingInfo(hWnd);
                trackedWindows[hWnd] = windowInfo;

                // : Použitie správneho konštruktora
                var eventArgs = new NewWindowDetectedEventArgs(
                    windowInfo.WindowHandle,
                    windowInfo.Title,
                    windowInfo.ProcessName,
                    windowInfo.WindowType);

                NewWindowDetected?.Invoke(this, eventArgs);

                System.Diagnostics.Debug.WriteLine($"🆕 New window: {windowInfo.Title} ({windowInfo.ProcessName})");
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

                    // : Použitie správneho konštruktora
                    var eventArgs = new WindowClosedEventArgs(windowInfo);

                    WindowClosed?.Invoke(this, eventArgs);

                    System.Diagnostics.Debug.WriteLine($"🗑️ Window closed: {windowInfo.Title}");
                }
            }
        }

        private void CheckForActivatedWindow()
        {
            var currentActiveWindow = GetForegroundWindow();
            if (currentActiveWindow != IntPtr.Zero && currentActiveWindow != lastActiveWindow)
            {
                lastActiveWindow = currentActiveWindow;

                if (trackedWindows.TryGetValue(currentActiveWindow, out var windowInfo))
                {
                    windowInfo.LastActivated = DateTime.Now;
                    windowInfo.IsActive = true;

                    // Označiť ostatné okná ako neaktívne
                    foreach (var other in trackedWindows.Values.Where(w => w.WindowHandle != currentActiveWindow))
                    {
                        other.IsActive = false;
                    }

                    // : Použitie správneho konštruktora
                    var eventArgs = new WindowActivatedEventArgs(windowInfo);
                    WindowActivated?.Invoke(this, eventArgs);

                    System.Diagnostics.Debug.WriteLine($"🎯 Window activated: {windowInfo.Title}");
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
                ProcessId = (int)processId,
                IsVisible = IsWindowVisible(hWnd),
                IsActive = false,
                LastActivated = DateTime.Now
            };
        }

        private string GetWindowTitle(IntPtr hWnd)
        {
            var title = new StringBuilder(256);
            GetWindowText(hWnd, title, title.Capacity);
            return title.ToString();
        }

        private string GetWindowClassName(IntPtr hWnd)
        {
            var className = new StringBuilder(256);
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

        private WindowType DetermineWindowType(IntPtr hWnd)
        {
            var className = GetWindowClassName(hWnd);
            var title = GetWindowTitle(hWnd);

            // : Použitie správnych enum hodnôt
            if (className.Contains("Dialog") || title.Contains("Dialog"))
                return WindowType.Dialog;
            if (className.Contains("MessageBox") || className.Equals("#32770"))
                return WindowType.MessageBox;
            if (className.Contains("Popup"))
                return WindowType.PopupWindow;

            return WindowType.MainWindow;
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
                    StopTracking();
                    windowMonitorTimer?.Dispose();
                    knownWindows.Clear();
                    trackedWindows.Clear();
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
}
