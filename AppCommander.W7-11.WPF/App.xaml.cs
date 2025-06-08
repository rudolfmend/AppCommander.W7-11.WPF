using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
        #region DPI Awareness Win32 API

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(ProcessDpiAwareness value);

        [DllImport("shcore.dll")]
        private static extern int GetProcessDpiAwareness(IntPtr hprocess, out ProcessDpiAwareness value);

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
                // Initialize DPI awareness for better scaling
                InitializeDpiAwareness();

                // Enable better text rendering globally
                TextOptions.TextFormattingModeProperty.OverrideMetadata(typeof(Control),
                    new FrameworkPropertyMetadata(TextFormattingMode.Display));
                TextOptions.TextRenderingModeProperty.OverrideMetadata(typeof(Control),
                    new FrameworkPropertyMetadata(TextRenderingMode.ClearType));

                // Set WPF to use the hardware rendering tier
                RenderOptions.ProcessRenderMode = RenderMode.Default;

                // Handle unhandled exceptions
                SetupExceptionHandling();

                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Application startup failed: {ex.Message}", "AppCommander Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        private void InitializeDpiAwareness()
        {
            try
            {
                // For Windows 8.1 and later, try to set per-monitor DPI awareness
                if (Environment.OSVersion.Version >= new Version(6, 3)) // Windows 8.1
                {
                    try
                    {
                        SetProcessDpiAwareness(ProcessDpiAwareness.ProcessPerMonitorDpiAware);
                        return;
                    }
                    catch (EntryPointNotFoundException)
                    {
                        // Shcore.dll not available, fall back to older method
                    }
                }

                // For Windows Vista and later, set system DPI awareness
                if (Environment.OSVersion.Version >= new Version(6, 0)) // Windows Vista
                {
                    SetProcessDPIAware();
                }
            }
            catch (Exception ex)
            {
                // DPI awareness failed, but continue - app will work without it
                Debug.WriteLine($"DPI awareness setup failed: {ex.Message}");
            }
        }

        private void SetupExceptionHandling()
        {
            // Handle WPF unhandled exceptions
            this.DispatcherUnhandledException += (sender, e) =>
            {
                LogException(e.Exception, "DispatcherUnhandledException");

                MessageBox.Show($"An unexpected error occurred:\n{e.Exception.Message}\n\nThe application will continue running.",
                    "AppCommander Error", MessageBoxButton.OK, MessageBoxImage.Warning);

                e.Handled = true;
            };

            // Handle non-WPF unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var exception = e.ExceptionObject as Exception;
                LogException(exception, "UnhandledException");

                if (e.IsTerminating)
                {
                    MessageBox.Show($"A critical error occurred and the application must close:\n{exception?.Message}",
                        "AppCommander Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
        }

        private void LogException(Exception exception, string source)
        {
            try
            {
                Debug.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {exception}");

                // TODO: In future versions, log to file
                // var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                //     "AppCommander", "logs", $"error_{DateTime.Now:yyyyMMdd}.log");
            }
            catch
            {
                // Ignore logging errors to prevent cascading failures
            }
        }

        /// <summary>
        /// Gets the current DPI scaling factor
        /// </summary>
        public static double GetDpiScale()
        {
            try
            {
                var source = PresentationSource.FromVisual(Current.MainWindow);
                if (source?.CompositionTarget != null)
                {
                    return source.CompositionTarget.TransformToDevice.M11;
                }
            }
            catch
            {
                // Fallback if DPI detection fails
            }

            return 1.0;
        }

        /// <summary>
        /// Gets Windows version information for compatibility checks
        /// </summary>
        public static string GetWindowsVersion()
        {
            var version = Environment.OSVersion.Version;

            if (version.Major == 10) return "Windows 10/11";
            if (version.Major == 6)
            {
                if (version.Minor == 3) return "Windows 8.1";
                if (version.Minor == 2) return "Windows 8";
                if (version.Minor == 1) return "Windows 7";
                if (version.Minor == 0) return "Windows Vista";
            }
            if (version.Major == 5 && version.Minor == 1) return "Windows XP";

            return $"Windows {version.Major}.{version.Minor}";
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // TODO: Save application state/settings before exit
                Debug.WriteLine("AppCommander exiting gracefully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during application exit: {ex.Message}");
            }

            base.OnExit(e);
        }
    }
}
