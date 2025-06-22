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
                var allWindows = WindowTrackingInfo.GetAllWindows();

                foreach (var window in allWindows)
                {
                    try
                    {
                        // Skip our own process
                        if (window.ProcessName.Equals("AppCommander", StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Skip windows without titles
                        if (string.IsNullOrWhiteSpace(window.Title))
                            continue;

                        // Pridaj priamo WindowTrackingInfo (už nepotrebujeme konverziu)
                        windows.Add(window);
                    }
                    catch
                    {
                        // Skip processes we can't access
                        continue;
                    }
                }

                // Ak WindowTracker.GetAllWindows() vráti prázdny zoznam, použij fallback
                if (windows.Count == 0)
                {
                    LoadWindowsFallback();
                }

                // Remove duplicates and sort
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
                        // Skip our own process
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
                        // Skip processes we can't access
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
    }
}
