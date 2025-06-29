using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using AppCommander.W7_11.WPF.Core;

namespace AppCommander.W7_11.WPF.Core
{
    /// <summary>
    /// Testovací nástroj pre automatickú detekciu okien
    /// </summary>
    public static class WindowDetectionTester
    {
        /// <summary>
        /// Spustí test automatickej detekcie okien
        /// </summary>
        public static void RunDetectionTest(IntPtr primaryTarget, string processName = "")
        {
            Console.WriteLine("=== WINDOW DETECTION TEST ===");
            Console.WriteLine($"Primary Target: {primaryTarget}");
            Console.WriteLine($"Process Name: {processName}");
            Console.WriteLine($"Start Time: {DateTime.Now}");
            Console.WriteLine();

            // OPRAVENÉ: Používa WindowTracker namiesto WindowTrackingInfo
            var tracker = new WindowTracker();
            var detectedWindows = new List<WindowTrackingInfo>();

            // Subscribe to events
            tracker.NewWindowDetected += (sender, e) =>
            {
                var windowInfo = CreateWindowInfoFromEvent(e);
                detectedWindows.Add(windowInfo);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] NEW WINDOW: {e.WindowTitle}");
                Console.WriteLine($"    Type: {e.WindowType}");
                Console.WriteLine($"    Process: {e.ProcessName}");
                Console.WriteLine();
            };

            tracker.WindowActivated += (sender, e) =>
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ACTIVATED: {e.WindowTitle}");
            };

            tracker.WindowClosed += (sender, e) =>
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] CLOSED: {e.WindowTitle}");
            };

            // Štart tracking
            tracker.StartTracking(primaryTarget, processName);

            Console.WriteLine("Window tracking started. Testing scenarios:");
            Console.WriteLine("1. Open dialogs and message boxes in the target application");
            Console.WriteLine("2. Switch between windows");
            Console.WriteLine("3. Press any key to stop test...");
            Console.WriteLine();

            // Čakaj na input
            Console.ReadKey();

            // Stop tracking
            tracker.StopTracking();

            // Report results
            Console.WriteLine();
            Console.WriteLine("=== TEST RESULTS ===");
            Console.WriteLine($"Total windows detected: {detectedWindows.Count}");
            Console.WriteLine($"Test duration: {DateTime.Now}");
            Console.WriteLine();

            if (detectedWindows.Any())
            {
                Console.WriteLine("Detected windows:");
                foreach (var window in detectedWindows)
                {
                    Console.WriteLine($"  - {window.WindowType}: {window.Title} ({window.ProcessName})");
                }
            }
            else
            {
                Console.WriteLine("No new windows were detected during the test.");
            }

            // Cleanup
            tracker.Dispose();
        }

        /// <summary>
        /// Vytvorí WindowTrackingInfo z event args
        /// </summary>
        private static WindowTrackingInfo CreateWindowInfoFromEvent(NewWindowDetectedEventArgs e)
        {
            return new WindowTrackingInfo
            {
                WindowHandle = e.WindowHandle,
                Title = e.WindowTitle,
                ProcessName = e.ProcessName,
                WindowType = e.WindowType,
                DetectedAt = e.Timestamp,
                IsVisible = true,
                IsActive = false
            };
        }

        /// <summary>
        /// Testuje detekciu rôznych typov okien
        /// </summary>
        public static void TestWindowTypeDetection()
        {
            Console.WriteLine("=== WINDOW TYPE DETECTION TEST ===");

            var allWindows = GetTestWindows();

            foreach (var window in allWindows)
            {
                var info = AnalyzeTestWindow(window);
                Console.WriteLine($"Window: {info.Title}");
                Console.WriteLine($"  Process: {info.ProcessName}");
                Console.WriteLine($"  Class: {info.ClassName}");
                Console.WriteLine($"  Detected Type: {info.WindowType}");
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Simuluje scenáre pre testovanie
        /// </summary>
        public static void SimulateTestScenarios()
        {
            Console.WriteLine("=== SIMULATION TEST ===");
            Console.WriteLine("This will demonstrate various window detection scenarios:");
            Console.WriteLine();

            // Scenario 1: MessageBox
            Console.WriteLine("Scenario 1: Message Box Detection");
            Console.WriteLine("Opening message box...");
            MessageBox.Show("This is a test message box for window detection.",
                          "Test MessageBox",
                          MessageBoxButtons.OK,
                          MessageBoxIcon.Information);

            // Scenario 2: Error Dialog
            Console.WriteLine("Scenario 2: Error Dialog Detection");
            Console.WriteLine("Opening error dialog...");
            MessageBox.Show("This is a test error message for window detection.",
                          "Test Error",
                          MessageBoxButtons.YesNo,
                          MessageBoxIcon.Error);

            // Scenario 3: Custom Dialog
            Console.WriteLine("Scenario 3: Custom Dialog Detection");
            Console.WriteLine("Opening custom dialog...");
            ShowCustomDialog();

            Console.WriteLine("Simulation completed.");
        }

        /// <summary>
        /// Vytvorí custom dialog pre testovanie
        /// </summary>
        private static void ShowCustomDialog()
        {
            var dialog = new Form()
            {
                Text = "Custom Test Dialog",
                Width = 400,
                Height = 200,
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var label = new Label()
            {
                Text = "This is a custom dialog for testing window detection.",
                AutoSize = true,
                Location = new System.Drawing.Point(20, 20)
            };

            var button = new Button()
            {
                Text = "Close",
                Location = new System.Drawing.Point(150, 100),
                DialogResult = DialogResult.OK
            };

            dialog.Controls.Add(label);
            dialog.Controls.Add(button);
            dialog.ShowDialog();
        }

        /// <summary>
        /// Získa zoznam okien pre testovanie
        /// </summary>
        public static List<IntPtr> GetTestWindows()
        {
            var windows = new List<IntPtr>();

            try
            {
                EnumWindows((hWnd, lParam) =>
                {
                    if (IsWindowVisible(hWnd))
                    {
                        string title = GetWindowTitle(hWnd);
                        if (!string.IsNullOrEmpty(title) && title.Length > 3)
                        {
                            windows.Add(hWnd);
                        }
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting test windows: {ex.Message}");
            }

            return windows.Take(10).ToList(); // Limit to 10 for testing
        }

        /// <summary>
        /// Analyzuje test window
        /// </summary>
        private static WindowTrackingInfo AnalyzeTestWindow(IntPtr windowHandle)
        {
            var info = new WindowTrackingInfo
            {
                WindowHandle = windowHandle,
                Title = GetWindowTitle(windowHandle),
                ClassName = GetWindowClassName(windowHandle),
                ProcessName = GetProcessName(windowHandle),
                DetectedAt = DateTime.Now
            };

            // Determine window type - použite static helper metódu
            info.WindowType = DetermineWindowTypeStatic(info.Title, info.ClassName, windowHandle);

            // Get dimensions
            if (GetWindowRect(windowHandle, out RECT rect))
            {
                // Môžeme pridať Width a Height properties do WindowTrackingInfo ak je potrebné
            }

            return info;
        }

        /// <summary>
        /// Určí typ okna pre testovanie - static helper metóda
        /// </summary>
        private static WindowType DetermineWindowTypeStatic(string title, string className, IntPtr handle)
        {
            // MessageBox detection
            if (className.Contains("MessageBox") ||
                className == "#32770" ||
                title.Contains("Error") || title.Contains("Warning") ||
                title.Contains("Information") || title.Contains("Confirm"))
            {
                return WindowType.MessageBox;
            }

            // Dialog detection
            if (className.Contains("Dialog") ||
                className == "#32770" ||
                title.Contains("Dialog"))
            {
                return WindowType.Dialog;
            }

            // Child window detection
            if (GetParent(handle) != IntPtr.Zero)
            {
                return WindowType.ChildWindow;
            }

            return WindowType.MainWindow;
        }

        /// <summary>
        /// Spustí interaktívny test
        /// </summary>
        public static void RunInteractiveTest()
        {
            Console.WriteLine("=== INTERACTIVE WINDOW DETECTION TEST ===");
            Console.WriteLine();
            Console.WriteLine("This test will help you verify automatic window detection.");
            Console.WriteLine("Follow the instructions below:");
            Console.WriteLine();

            Console.WriteLine("Step 1: Select a target application");
            Console.Write("Enter process name (e.g., 'notepad', 'calculator'): ");
            string processName = Console.ReadLine();

            if (string.IsNullOrEmpty(processName))
            {
                Console.WriteLine("No process name provided. Using global detection mode.");
                processName = "";
            }

            // Find target window - použite static metódy
            IntPtr targetWindow = IntPtr.Zero;
            if (!string.IsNullOrEmpty(processName))
            {
                var allWindows = GetAllWindowsStatic();
                var processWindows = allWindows.Where(w =>
                    w.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase)).ToList();

                if (processWindows.Any())
                {
                    targetWindow = processWindows.First().WindowHandle;
                    Console.WriteLine($"Found target window: {processWindows.First().Title}");
                }
                else
                {
                    Console.WriteLine($"Could not find window for process '{processName}'");
                    Console.WriteLine("Please start the application and try again, or use global mode.");
                    return;
                }
            }

            Console.WriteLine();
            Console.WriteLine("Step 2: Start window tracking");
            Console.WriteLine("Press ENTER to start tracking...");
            Console.ReadLine();

            // Start tracking test
            RunDetectionTest(targetWindow, processName);

            Console.WriteLine();
            Console.WriteLine("Step 3: Test completed");
            Console.WriteLine("Check the output above for detected windows.");
            Console.WriteLine();
        }

        /// <summary>
        /// Získa všetky okná - static verzia
        /// </summary>
        private static List<WindowTrackingInfo> GetAllWindowsStatic()
        {
            var windows = new List<WindowTrackingInfo>();

            try
            {
                EnumWindows((hWnd, lParam) =>
                {
                    try
                    {
                        if (!IsWindowVisible(hWnd))
                            return true;

                        string title = GetWindowTitle(hWnd);
                        string className = GetWindowClassName(hWnd);
                        string processName = GetProcessName(hWnd);

                        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrEmpty(processName))
                            return true;

                        var windowInfo = new WindowTrackingInfo
                        {
                            WindowHandle = hWnd,
                            Title = title,
                            ClassName = className,
                            ProcessName = processName,
                            DetectedAt = DateTime.Now,
                            IsVisible = true,
                            WindowType = DetermineWindowTypeStatic(title, className, hWnd)
                        };

                        windows.Add(windowInfo);
                    }
                    catch
                    {
                        // Skip problematic windows
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting all windows: {ex.Message}");
            }

            return windows;
        }

        /// <summary>
        /// Exportuje test report
        /// </summary>
        public static void ExportTestReport(List<WindowTrackingInfo> detectedWindows, string filePath)
        {
            try
            {
                var report = new System.Text.StringBuilder();

                report.AppendLine("=== WINDOW DETECTION TEST REPORT ===");
                report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine($"Total Windows Detected: {detectedWindows.Count}");
                report.AppendLine();

                // Summary by type
                var typeGroups = detectedWindows.GroupBy(w => w.WindowType);
                report.AppendLine("Detection Summary by Type:");
                foreach (var group in typeGroups)
                {
                    report.AppendLine($"  {group.Key}: {group.Count()} windows");
                }
                report.AppendLine();

                // Detailed list
                report.AppendLine("Detailed Window List:");
                foreach (var window in detectedWindows.OrderBy(w => w.DetectedAt))
                {
                    report.AppendLine($"[{window.DetectedAt:HH:mm:ss}] {window.WindowType}: {window.Title}");
                    report.AppendLine($"    Process: {window.ProcessName}");
                    report.AppendLine($"    Class: {window.ClassName}");
                    report.AppendLine();
                }

                // Recommendations
                report.AppendLine("=== RECOMMENDATIONS ===");
                if (detectedWindows.Any(w => w.WindowType == WindowType.MessageBox))
                {
                    report.AppendLine("✓ MessageBox detection is working");
                }
                if (detectedWindows.Any(w => w.WindowType == WindowType.Dialog))
                {
                    report.AppendLine("✓ Dialog detection is working");
                }
                if (!detectedWindows.Any())
                {
                    report.AppendLine("⚠ No windows were detected during the test");
                    report.AppendLine("  - Try opening dialogs or message boxes");
                    report.AppendLine("  - Check if target application is running");
                    report.AppendLine("  - Verify window tracking configuration");
                }

                System.IO.File.WriteAllText(filePath, report.ToString());
                Console.WriteLine($"Test report exported to: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting test report: {ex.Message}");
            }
        }

        // Helper methods
        private static string GetWindowTitle(IntPtr hWnd)
        {
            var title = new System.Text.StringBuilder(256);
            GetWindowText(hWnd, title, title.Capacity);
            return title.ToString();
        }

        private static string GetWindowClassName(IntPtr hWnd)
        {
            var className = new System.Text.StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            return className.ToString();
        }

        private static string GetProcessName(IntPtr hWnd)
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

        // Windows API
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
