using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace AppCommander.W7_11.WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        #region Single Instance Management

        private static Mutex _instanceMutex;
        private const string MUTEX_NAME = "AppCommander_W7-11_WPF_SingleInstance_D920F8A2-F7EF-472C-B0E6-F58AA4F1CAB9";

        // Win32 API for window management
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        #endregion

        #region DPI Awareness Win32 API

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(ProcessDpiAwareness value);

        [DllImport("shcore.dll")]
        private static extern int GetProcessDpiAwareness(IntPtr hprocess, out ProcessDpiAwareness value);

        // Windows 10 version 1703+ API for PerMonitorV2
        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        // DPI Awareness Context values
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        private enum ProcessDpiAwareness
        {
            ProcessDpiUnaware = 0,
            ProcessSystemDpiAware = 1,
            ProcessPerMonitorDpiAware = 2
        }

        #endregion

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Check for single instance BEFORE any UI initialization
                if (!EnsureSingleInstance())
                {
                    // Another instance is already running
                    Debug.WriteLine("Another instance of AppCommander is already running. Exiting...");
                    Shutdown(0);
                    return;
                }

                // Initialize DPI awareness for better scaling
                InitializeDpiAwareness();

                // Enable better text rendering globally
                TextOptions.TextFormattingModeProperty.OverrideMetadata(typeof(Control),
                    new FrameworkPropertyMetadata(TextFormattingMode.Display));
                TextOptions.TextRenderingModeProperty.OverrideMetadata(typeof(Control),
                    new FrameworkPropertyMetadata(TextRenderingMode.ClearType));

                // Set WPF to use the hardware rendering tier
                RenderOptions.ProcessRenderMode = RenderMode.Default;

                // Enable layout rounding globally for crisp edges
                FrameworkElement.UseLayoutRoundingProperty.OverrideMetadata(typeof(Control),
                    new FrameworkPropertyMetadata(true));

                // Handle unhandled exceptions
                SetupExceptionHandling();

                base.OnStartup(e);

                // Log successful startup
                Debug.WriteLine($"AppCommander started successfully on {GetWindowsVersion()}, DPI Scale: {GetDpiScale():F2}x");
            }
            catch (Exception ex)
            {
                ReleaseMutex(); // Clean up mutex on startup failure
                MessageBox.Show($"Application startup failed: {ex.Message}", "AppCommander Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Ensures only one instance of the application is running
        /// </summary>
        /// <returns>True if this is the first instance, False if another instance already exists</returns>
        private bool EnsureSingleInstance()
        {
            bool isNewInstance = false;

            try
            {
                // Try to create a named mutex
                _instanceMutex = new Mutex(true, MUTEX_NAME, out isNewInstance);

                if (!isNewInstance)
                {
                    // Another instance is running, try to bring it to foreground
                    BringExistingInstanceToForeground();
                    return false;
                }

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                // Mutex exists but we don't have access (different user/elevation level)
                Debug.WriteLine("Another instance may be running under different user context");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking for existing instance: {ex.Message}");
                // If we can't determine, allow this instance to run
                return true;
            }
        }

        /// <summary>
        /// Attempts to bring the existing application instance to the foreground
        /// </summary>
        private void BringExistingInstanceToForeground()
        {
            try
            {
                // Find existing AppCommander process
                var currentProcess = Process.GetCurrentProcess();
                var processes = Process.GetProcessesByName(currentProcess.ProcessName);

                foreach (var process in processes)
                {
                    // Skip current process
                    if (process.Id == currentProcess.Id)
                        continue;

                    // Try to bring the window to foreground
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        // If minimized, restore it
                        if (IsIconic(process.MainWindowHandle))
                        {
                            ShowWindow(process.MainWindowHandle, SW_RESTORE);
                        }
                        else
                        {
                            ShowWindow(process.MainWindowHandle, SW_SHOW);
                        }

                        // Bring to foreground
                        SetForegroundWindow(process.MainWindowHandle);
                        Debug.WriteLine("Brought existing instance to foreground");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to bring existing instance to foreground: {ex.Message}");
            }
        }

        /// <summary>
        /// Alternative method using named pipes for more advanced inter-instance communication
        /// </summary>
        private bool EnsureSingleInstanceWithPipes()
        {
            // This is a more advanced approach that allows passing command line arguments
            // to the existing instance. Uncomment and modify if needed.

            /*
            try
            {
                var pipeClient = new NamedPipeClientStream(".", "AppCommander_Pipe", PipeDirection.Out);
                pipeClient.Connect(1000); // 1 second timeout

                // If we can connect, another instance is running
                var args = Environment.GetCommandLineArgs();
                if (args.Length > 1)
                {
                    // Send command line arguments to existing instance
                    using (var writer = new StreamWriter(pipeClient))
                    {
                        writer.WriteLine(string.Join(" ", args.Skip(1)));
                    }
                }
                
                pipeClient.Close();
                return false; // Another instance exists
            }
            catch (TimeoutException)
            {
                // No existing instance found, this is the first one
                SetupNamedPipeServer();
                return true;
            }
            */

            return true;
        }

        private void InitializeDpiAwareness()
        {
            try
            {
                var osVersion = Environment.OSVersion.Version;
                bool dpiAwarenessSet = false;

                // Windows 10 version 1703 (Creators Update) and later - supports PerMonitorV2
                if (osVersion >= new Version(10, 0, 15063))
                {
                    try
                    {
                        if (SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
                        {
                            Debug.WriteLine("DPI Awareness: PerMonitorV2 (Windows 10 1703+)");
                            dpiAwarenessSet = true;
                        }
                    }
                    catch (EntryPointNotFoundException)
                    {
                        Debug.WriteLine("PerMonitorV2 API not available, falling back...");
                    }
                }

                // For Windows 8.1 and later, try to set per-monitor DPI awareness
                if (!dpiAwarenessSet && osVersion >= new Version(6, 3))
                {
                    try
                    {
                        var result = SetProcessDpiAwareness(ProcessDpiAwareness.ProcessPerMonitorDpiAware);
                        if (result == 0)
                        {
                            Debug.WriteLine("DPI Awareness: PerMonitor (Windows 8.1+)");
                            dpiAwarenessSet = true;
                        }
                    }
                    catch (EntryPointNotFoundException)
                    {
                        Debug.WriteLine("Shcore.dll not available, falling back to legacy method...");
                    }
                    catch (COMException ex)
                    {
                        Debug.WriteLine($"DPI Awareness already set: {ex.Message}");
                        dpiAwarenessSet = true;
                    }
                }

                // For Windows Vista and later, set system DPI awareness
                if (!dpiAwarenessSet && osVersion >= new Version(6, 0))
                {
                    try
                    {
                        if (SetProcessDPIAware())
                        {
                            Debug.WriteLine("DPI Awareness: System (Windows Vista+)");
                            dpiAwarenessSet = true;
                        }
                    }
                    catch (EntryPointNotFoundException)
                    {
                        Debug.WriteLine("SetProcessDPIAware not available");
                    }
                }

                if (!dpiAwarenessSet)
                {
                    Debug.WriteLine("DPI Awareness: None (will rely on manifest settings)");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DPI awareness setup failed: {ex.Message}");
            }
        }

        private void SetupExceptionHandling()
        {
            this.DispatcherUnhandledException += (sender, e) =>
            {
                LogException(e.Exception, "DispatcherUnhandledException");

#if DEBUG
                MessageBox.Show($"DEBUG - Unhandled Exception:\n{e.Exception}\n\nThe application will continue running.",
                    "AppCommander Debug Error", MessageBoxButton.OK, MessageBoxImage.Warning);
#else
                MessageBox.Show($"An unexpected error occurred:\n{e.Exception.Message}\n\nThe application will continue running.",
                    "AppCommander Error", MessageBoxButton.OK, MessageBoxImage.Warning);
#endif

                e.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var exception = e.ExceptionObject as Exception;
                LogException(exception, "UnhandledException");

                if (e.IsTerminating)
                {
                    string message = exception?.Message ?? "Unknown critical error";
#if DEBUG
                    message = $"DEBUG - Critical Error:\n{exception}";
#endif

                    MessageBox.Show($"A critical error occurred and the application must close:\n{message}",
                        "AppCommander Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
        }

        private void LogException(Exception exception, string source)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string osInfo = GetWindowsVersion();
                double dpiScale = GetDpiScale();

                Debug.WriteLine($"[{timestamp}] {source}:");
                Debug.WriteLine($"  OS: {osInfo}");
                Debug.WriteLine($"  DPI Scale: {dpiScale:F2}x");
                Debug.WriteLine($"  Exception: {exception}");
                Debug.WriteLine(new string('-', 80));
            }
            catch
            {
                // Ignore logging errors
            }
        }

        /// <summary>
        /// Gets the current DPI scaling factor
        /// </summary>
        public static double GetDpiScale()
        {
            try
            {
                if (Current?.MainWindow != null)
                {
                    var source = PresentationSource.FromVisual(Current.MainWindow);
                    if (source?.CompositionTarget != null)
                    {
                        return source.CompositionTarget.TransformToDevice.M11;
                    }
                }

                using (var graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
                {
                    return graphics.DpiX / 96.0;
                }
            }
            catch
            {
                return 1.0;
            }
        }

        /// <summary>
        /// Gets Windows version information
        /// </summary>
        public static string GetWindowsVersion()
        {
            var version = Environment.OSVersion.Version;

            if (version.Major == 10)
            {
                if (version.Build >= 22000)
                {
                    return $"Windows 11 (Build {version.Build})";
                }
                return $"Windows 10 (Build {version.Build})";
            }

            if (version.Major == 6)
            {
                if (version.Minor == 3) return "Windows 8.1";
                if (version.Minor == 2) return "Windows 8";
                if (version.Minor == 1) return "Windows 7";
                if (version.Minor == 0) return "Windows Vista";
            }

            return $"Windows {version.Major}.{version.Minor} (Build {version.Build})";
        }

        private void ReleaseMutex()
        {
            try
            {
                if (_instanceMutex != null)
                {
                    try
                    {
                        _instanceMutex.ReleaseMutex();
                    }
                    catch (ApplicationException)
                    {
                        // Mutex was not owned by current thread - this is expected in some scenarios
                        Debug.WriteLine("Mutex was already released or not owned by current thread");
                    }

                    _instanceMutex.Dispose();
                    _instanceMutex = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error releasing mutex: {ex.Message}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                Debug.WriteLine($"AppCommander exiting gracefully (Exit Code: {e.ApplicationExitCode})");

                // Release the single instance mutex
                ReleaseMutex();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during application exit: {ex.Message}");
            }

            base.OnExit(e);
        }
    }
}
