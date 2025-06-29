using AppCommander.W7_11.WPF.Core;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AppCommander.W7_11.WPF
{
    public partial class WindowSelectorDialog : Window
    {
        public WindowTrackingInfo SelectedWindow { get; private set; }

        private readonly ObservableCollection<WindowTrackingInfo> windows;

        public WindowSelectorDialog()
        {
            InitializeComponent();

            windows = new ObservableCollection<WindowTrackingInfo>();
            dgWindows.ItemsSource = windows;
            dgWindows.SelectionChanged += DgWindows_SelectionChanged;

            LoadWindows();
        }

        private void LoadWindows()
        {
            windows.Clear();

            try
            {
                // OPRAVENÉ: Použitie WindowTracker namiesto WindowTrackingInfo instance
                var windowTracker = new WindowTracker();
                var allWindowHandles = windowTracker.GetAllWindows();

                foreach (var windowHandle in allWindowHandles)
                {
                    try
                    {
                        var windowInfo = CreateWindowInfoFromHandle(windowHandle);

                        // Skip našu vlastnú aplikáciu
                        if (windowInfo.ProcessName.Equals("AppCommander", StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Skip okná bez títulu
                        if (string.IsNullOrWhiteSpace(windowInfo.Title))
                            continue;

                        windows.Add(windowInfo);
                    }
                    catch
                    {
                        // Skip procesy ktoré nemôžeme pristúpiť
                        continue;
                    }
                }

                // Ak sa nenašli žiadne okná, použij fallback
                if (windows.Count == 0)
                {
                    LoadWindowsFallback();
                }

                // Odstráň duplikáty a zoraď
                var uniqueWindows = windows
                    .GroupBy(w => w.WindowHandle)
                    .Select(g => g.First())
                    .OrderBy(w => w.ProcessName)
                    .ThenBy(w => w.Title)
                    .ToList();

                windows.Clear();
                foreach (var window in uniqueWindows)
                {
                    windows.Add(window);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading windows: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Warning);

                // Fallback ak hlavná metóda zlyhá
                LoadWindowsFallback();
            }
        }

        /// <summary>
        /// Vytvorí WindowTrackingInfo z window handle
        /// </summary>
        private WindowTrackingInfo CreateWindowInfoFromHandle(IntPtr windowHandle)
        {
            var windowInfo = new WindowTrackingInfo
            {
                WindowHandle = windowHandle,
                Title = GetWindowTitle(windowHandle),
                ProcessName = GetProcessName(windowHandle),
                ClassName = GetWindowClassName(windowHandle),
                DetectedAt = DateTime.Now,
                IsVisible = true,
                IsEnabled = true,
                WindowType = WindowType.MainWindow
            };

            // Získaj process ID
            GetWindowThreadProcessId(windowHandle, out uint processId);
            windowInfo.ProcessId = (int)processId;

            return windowInfo;
        }

        /// <summary>
        /// Fallback metóda ak WindowTracker zlyhá
        /// </summary>
        private void LoadWindowsFallback()
        {
            try
            {
                // Get all running processes with windows
                var processes = Process.GetProcesses()
                    .Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle))
                    .OrderBy(p => p.ProcessName)
                    .ToList();

                foreach (var process in processes)
                {
                    try
                    {
                        // Skip našu vlastnú aplikáciu
                        if (process.ProcessName.Equals("AppCommander", StringComparison.OrdinalIgnoreCase))
                            continue;

                        windows.Add(new WindowTrackingInfo
                        {
                            WindowHandle = process.MainWindowHandle,
                            Title = process.MainWindowTitle,
                            ProcessName = process.ProcessName,
                            ProcessId = process.Id,
                            ClassName = GetWindowClassName(process.MainWindowHandle),
                            DetectedAt = DateTime.Now,
                            IsVisible = true,
                            IsEnabled = true,
                            WindowType = WindowType.MainWindow
                        });
                    }
                    catch
                    {
                        // Skip procesy ktoré nemôžeme pristúpiť
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fallback window loading failed: {ex.Message}");
            }
        }

        private string GetWindowClassName(IntPtr handle)
        {
            try
            {
                var className = new System.Text.StringBuilder(256);
                GetClassName(handle, className, className.Capacity);
                return className.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }

        private string GetWindowTitle(IntPtr handle)
        {
            try
            {
                var title = new System.Text.StringBuilder(256);
                GetWindowText(handle, title, title.Capacity);
                return title.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }

        private string GetProcessName(IntPtr handle)
        {
            try
            {
                GetWindowThreadProcessId(handle, out uint processId);
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

        private void DgWindows_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = dgWindows.SelectedItem as WindowTrackingInfo;

            if (selected != null)
            {
                txtSelectedProcess.Text = selected.ProcessName;
                txtSelectedTitle.Text = selected.Title;
                txtSelectedClass.Text = selected.ClassName;
                txtSelectedHandle.Text = $"0x{selected.WindowHandle.ToString("X8")}";

                btnOK.IsEnabled = true;
            }
            else
            {
                txtSelectedProcess.Text = "-";
                txtSelectedTitle.Text = "-";
                txtSelectedClass.Text = "-";
                txtSelectedHandle.Text = "-";

                btnOK.IsEnabled = false;
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadWindows();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            SelectedWindow = dgWindows.SelectedItem as WindowTrackingInfo;

            if (SelectedWindow == null)
            {
                MessageBox.Show("Please select a window first.", "No Selection",
                               MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // PRIDANÉ: Debug informácie
            System.Diagnostics.Debug.WriteLine($"=== WindowSelectorDialog OK_Click ===");
            System.Diagnostics.Debug.WriteLine($"Selected Window Handle: 0x{SelectedWindow.WindowHandle:X8}");
            System.Diagnostics.Debug.WriteLine($"Selected Process: {SelectedWindow.ProcessName}");
            System.Diagnostics.Debug.WriteLine($"Selected Title: {SelectedWindow.Title}");
            System.Diagnostics.Debug.WriteLine($"Handle is Zero: {SelectedWindow.WindowHandle == IntPtr.Zero}");
            System.Diagnostics.Debug.WriteLine($"=====================================");

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // Windows API
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }
}
