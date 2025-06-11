using AppCommander.W7_11.WPF.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AppCommander.W7_11.WPF
{
    public partial class WindowSelectorDialog : Window
    {
        public WindowInfo SelectedWindow { get; private set; }

        private readonly ObservableCollection<WindowInfo> windows;

        public WindowSelectorDialog()
        {
            InitializeComponent();

            windows = new ObservableCollection<WindowInfo>();
            dgWindows.ItemsSource = windows;
            dgWindows.SelectionChanged += DgWindows_SelectionChanged;

            LoadWindows();
        }

        private void LoadWindows()
        {
            windows.Clear();

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

                        // Get additional windows for this process
                        var processWindows = WindowFinder.GetProcessWindows(process.ProcessName);

                        if (processWindows.Any())
                        {
                            foreach (var window in processWindows)
                            {
                                // Skip windows without titles
                                if (string.IsNullOrWhiteSpace(window.Title))
                                    continue;

                                windows.Add(window);
                            }
                        }
                        else
                        {
                            // Fallback: add main window
                            windows.Add(new WindowInfo
                            {
                                Handle = process.MainWindowHandle,
                                Title = process.MainWindowTitle,
                                ProcessName = process.ProcessName,
                                ProcessId = process.Id,
                                ClassName = GetWindowClassName(process.MainWindowHandle)
                            });
                        }
                    }
                    catch
                    {
                        // Skip processes we can't access
                        continue;
                    }
                }

                // Remove duplicates and sort
                var uniqueWindows = windows
                    .GroupBy(w => w.Handle)
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
            var selected = dgWindows.SelectedItem as WindowInfo;

            if (selected != null)
            {
                txtSelectedProcess.Text = selected.ProcessName;
                txtSelectedTitle.Text = selected.Title;
                txtSelectedClass.Text = selected.ClassName;
                txtSelectedHandle.Text = $"0x{selected.Handle.ToString("X8")}";

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
            SelectedWindow = dgWindows.SelectedItem as WindowInfo;

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
