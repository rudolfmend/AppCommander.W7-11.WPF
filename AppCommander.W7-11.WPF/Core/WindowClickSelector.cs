using AppCommander.W7_11.WPF.Core;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace AppCommander.W7_11.WPF
{
    /// <summary>
    /// Umožňuje výber cieľového okna jednoduchým kliknutím naň
    /// </summary>
    public class WindowClickSelector : IDisposable
    {
        #region Win32 API

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const uint GA_ROOT = 2;
        private const int VK_LBUTTON = 0x01;
        private const int VK_ESCAPE = 0x1B;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        #endregion

        #region Events

        public event EventHandler<WindowSelectedEventArgs> WindowSelected;
        public event EventHandler SelectionCancelled;
        public event EventHandler<string> StatusChanged;

        #endregion

        #region Private Fields

        private bool _isSelecting;
        private CancellationTokenSource _cancellationTokenSource;
        private WindowClickOverlay _overlay;
        private DispatcherTimer _updateTimer;
        private IntPtr _lastHighlightedWindow = IntPtr.Zero;
        private bool _disposed = false;

        #endregion

        #region Public Properties

        public bool IsSelecting => _isSelecting;

        #endregion

        #region Public Methods

        public async Task<WindowTrackingInfo> StartWindowSelectionAsync()
        {
            if (_isSelecting)
            {
                throw new InvalidOperationException("Selection is already in progress");
            }

            try
            {
                _isSelecting = true;
                _cancellationTokenSource = new CancellationTokenSource();

                OnStatusChanged("Click on any window to select it. Press ESC to cancel.");

                _overlay = new WindowClickOverlay();
                _overlay.Show();
                _overlay.Topmost = true;

                _updateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(50)
                };
                _updateTimer.Tick += UpdateHighlight;
                _updateTimer.Start();

                var result = await WaitForWindowSelectionAsync(_cancellationTokenSource.Token);
                return result;
            }
            catch (OperationCanceledException)
            {
                OnSelectionCancelled();
                return null;
            }
            finally
            {
                StopSelection();
            }
        }

        public void CancelSelection()
        {
            if (_isSelecting)
            {
                _cancellationTokenSource?.Cancel();
            }
        }

        #endregion

        #region Private Methods

        private async Task<WindowTrackingInfo> WaitForWindowSelectionAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(50, cancellationToken);

                if ((GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0)
                {
                    while ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0)
                    {
                        await Task.Delay(10, cancellationToken);
                    }

                    var selectedWindow = GetWindowUnderCursor();
                    if (selectedWindow != IntPtr.Zero)
                    {
                        var windowInfo = CreateWindowInfo(selectedWindow);
                        if (windowInfo != null)
                        {
                            OnWindowSelected(windowInfo);
                            return windowInfo;
                        }
                    }
                }
            }

            return null;
        }

        private void UpdateHighlight(object sender, EventArgs e)
        {
            if (!_isSelecting) return;

            try
            {
                var windowUnderCursor = GetWindowUnderCursor();

                if (windowUnderCursor != _lastHighlightedWindow)
                {
                    _lastHighlightedWindow = windowUnderCursor;

                    if (windowUnderCursor != IntPtr.Zero)
                    {
                        var windowInfo = CreateWindowInfo(windowUnderCursor);
                        if (windowInfo != null)
                        {
                            _overlay?.UpdateHighlight(windowInfo);
                            OnStatusChanged($"Hover: {windowInfo.ProcessName} - {windowInfo.Title}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating highlight: {ex.Message}");
            }
        }

        private IntPtr GetWindowUnderCursor()
        {
            try
            {
                if (GetCursorPos(out POINT cursorPos))
                {
                    var window = WindowFromPoint(cursorPos);
                    var rootWindow = GetAncestor(window, GA_ROOT);

                    if (IsOurOverlay(rootWindow))
                    {
                        return IntPtr.Zero;
                    }

                    return rootWindow;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting window under cursor: {ex.Message}");
            }

            return IntPtr.Zero;
        }

        private bool IsOurOverlay(IntPtr window)
        {
            if (_overlay == null) return false;

            try
            {
                var overlayHandle = new System.Windows.Interop.WindowInteropHelper(_overlay).Handle;
                return window == overlayHandle;
            }
            catch
            {
                return false;
            }
        }

        private WindowTrackingInfo CreateWindowInfo(IntPtr windowHandle)
        {
            try
            {
                var title = GetWindowTitle(windowHandle);
                var className = GetWindowClassName(windowHandle);
                var processInfo = GetProcessInfo(windowHandle);

                if (processInfo.processName.Equals("AppCommander", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return new WindowTrackingInfo
                {
                    WindowHandle = windowHandle,
                    Title = title,
                    ClassName = className,
                    ProcessName = processInfo.processName,
                    ProcessId = processInfo.processId,
                    DetectedAt = DateTime.Now,
                    IsVisible = IsWindowVisible(windowHandle),
                    IsEnabled = true,
                    WindowType = WindowType.MainWindow
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating window info: {ex.Message}");
                return null;
            }
        }

        private string GetWindowTitle(IntPtr window)
        {
            try
            {
                var title = new System.Text.StringBuilder(256);
                GetWindowText(window, title, title.Capacity);
                return title.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }

        private string GetWindowClassName(IntPtr window)
        {
            try
            {
                var className = new System.Text.StringBuilder(256);
                GetClassName(window, className, className.Capacity);
                return className.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }

        private (string processName, int processId) GetProcessInfo(IntPtr window)
        {
            try
            {
                GetWindowThreadProcessId(window, out uint processId);
                using (var process = Process.GetProcessById((int)processId))
                {
                    return (process.ProcessName, (int)processId);
                }
            }
            catch
            {
                return ("Unknown", 0);
            }
        }

        private void StopSelection()
        {
            _isSelecting = false;
            _updateTimer?.Stop();
            _updateTimer = null;
            _overlay?.Close();
            _overlay = null;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _lastHighlightedWindow = IntPtr.Zero;
        }

        #endregion

        #region Event Handlers

        protected virtual void OnWindowSelected(WindowTrackingInfo windowInfo)
        {
            var args = new WindowSelectedEventArgs(windowInfo);
            WindowSelected?.Invoke(this, args);
        }

        protected virtual void OnSelectionCancelled()
        {
            SelectionCancelled?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnStatusChanged(string status)
        {
            StatusChanged?.Invoke(this, status);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_isSelecting)
                    {
                        CancelSelection();
                    }
                    StopSelection();
                }
                _disposed = true;
            }
        }

        #endregion
    }

    #region Event Args

    public class WindowSelectedEventArgs : EventArgs
    {
        public WindowTrackingInfo SelectedWindow { get; }

        public WindowSelectedEventArgs(WindowTrackingInfo selectedWindow)
        {
            SelectedWindow = selectedWindow;
        }
    }

    #endregion
}
