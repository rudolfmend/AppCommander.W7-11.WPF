using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace AppCommander.W7_11.WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        #region Single Instance Management

        private static RegistryKey _themeRegistryKey;
        private static string _lastKnownSystemTheme;

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
                if (!EnsureSingleInstance())
                {
                    Shutdown(0);
                    return;
                }

                InitializeDpiAwareness();

                TextOptions.TextFormattingModeProperty.OverrideMetadata(typeof(Control),
                    new FrameworkPropertyMetadata(TextFormattingMode.Display));
                TextOptions.TextRenderingModeProperty.OverrideMetadata(typeof(Control),
                    new FrameworkPropertyMetadata(TextRenderingMode.ClearType));
                RenderOptions.ProcessRenderMode = RenderMode.Default;
                FrameworkElement.UseLayoutRoundingProperty.OverrideMetadata(typeof(Control),
                    new FrameworkPropertyMetadata(true));

                SetupExceptionHandling();

                // NAJPRV volaj base.OnStartup (aby sa načítali resources z App.xaml)
                base.OnStartup(e);

                // POTOM načítaj tému (keď sú už základné resources načítané)
                LoadDefaultTheme();

                // Spusti sledovanie systémovej témy
                StartSystemThemeMonitoring();

                Debug.WriteLine(string.Format("AppCommander started successfully on {0}, DPI Scale: {1:F2}x",
                    GetWindowsVersion(), GetDpiScale()));
            }
            catch (Exception ex)
            {
                ReleaseMutex();
                MessageBox.Show(string.Format("Application startup failed: {0}", ex.Message), "Sercull Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        private void CreateFallbackResources()
        {
            try
            {
                var resources = Application.Current.Resources;

                // základné brushes ako fallback (použije Light theme farby)
                resources["WindowBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xDC, 0xEA, 0xF2)); // #dceaf2
                resources["PanelBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xF9, 0xFA));    // #F8F9FA
                resources["CardBackgroundBrush"] = new SolidColorBrush(Colors.White);                        // #FFFFFF
                resources["PrimaryTextBrush"] = new SolidColorBrush(Color.FromRgb(0x32, 0x31, 0x30));       // #323130
                resources["SecondaryTextBrush"] = new SolidColorBrush(Color.FromRgb(0x60, 0x5E, 0x5C));     // #605E5C
                resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(0xE1, 0xE1, 0xE1));            // #E1E1E1

                Debug.WriteLine("Fallback theme resources created");
            }
            catch (Exception fallbackEx)
            {
                Debug.WriteLine($"Even fallback resource creation failed: {fallbackEx.Message}");
            }
        }

        /// <summary>
        /// Bezpečne prepne aplikačnú tému (.NET Framework 4.8 kompatibilná verzia)
        /// </summary>
        /// <param name="themeName">Názov témy: "Light", "Dark", "HighContrast", "System"</param>
        /// <returns>True ak sa téma úspešne načítala</returns>
        public static bool SwitchTheme(string themeName)
        {
            try
            {
                // Nahradenie switch expression s if-else (pre .NET Framework 4.8)
                string themeFile;
                if (themeName == "Light")
                {
                    themeFile = "Themes/LightTheme.xaml";
                }
                else if (themeName == "Dark")
                {
                    themeFile = "Themes/DarkTheme.xaml";
                }
                else if (themeName == "HighContrast")
                {
                    themeFile = "Themes/HighContrastTheme.xaml";
                }
                else if (themeName == "System")
                {
                    themeFile = "Themes/SystemTheme.xaml";
                }
                else
                {
                    themeFile = "Themes/LightTheme.xaml"; // fallback
                }

                // Odstráň existujúce témy (okrem štandardných App.xaml resources)
                var mergedDictionaries = Application.Current.Resources.MergedDictionaries;

                // Nájdi a odstráň existujúce theme súbory
                for (int i = mergedDictionaries.Count - 1; i >= 0; i--)
                {
                    var dict = mergedDictionaries[i];
                    if (dict.Source != null && dict.Source.OriginalString.Contains("Theme.xaml"))
                    {
                        mergedDictionaries.RemoveAt(i);
                    }
                }

                // Načítaj novú tému
                var newTheme = new ResourceDictionary
                {
                    Source = new Uri(themeFile, UriKind.Relative)
                };

                mergedDictionaries.Add(newTheme);

                // Nahradenie string interpolation s String.Format (pre .NET Framework 4.8)
                Debug.WriteLine(string.Format("Successfully switched to {0} theme", themeName));
                return true;
            }
            catch (Exception ex)
            {
                // Nahradenie string interpolation s String.Format
                Debug.WriteLine(string.Format("Failed to switch to {0} theme: {1}", themeName, ex.Message));

                // V prípade chyby sa pokús načítať Light theme
                if (themeName != "Light")
                {
                    Debug.WriteLine("Attempting fallback to Light theme...");
                    return SwitchTheme("Light");
                }

                return false;
            }
        }

        /// <summary>
        /// Získa názov aktuálne načítanej témy (.NET Framework 4.8 kompatibilná verzia)
        /// </summary>
        /// <returns>Názov témy alebo "Unknown" ak sa nedá určiť</returns>
        public static string GetCurrentTheme()
        {
            try
            {
                var mergedDictionaries = Application.Current.Resources.MergedDictionaries;

                foreach (var dict in mergedDictionaries)
                {
                    if (dict.Source != null && dict.Source.OriginalString.Contains("Theme.xaml"))
                    {
                        var fileName = dict.Source.OriginalString;

                        if (fileName.Contains("LightTheme.xaml")) return "Light";
                        if (fileName.Contains("DarkTheme.xaml")) return "Dark";
                        if (fileName.Contains("HighContrastTheme.xaml")) return "HighContrast";
                        if (fileName.Contains("SystemTheme.xaml")) return "System";
                    }
                }

                return "Unknown";
            }
            catch (Exception ex)
            {
                // Nahradenie string interpolation s String.Format
                Debug.WriteLine(string.Format("Failed to determine current theme: {0}", ex.Message));
                return "Unknown";
            }
        }

        /// <summary>
        /// Detekuje systémovú tému a načíta príslušnú tému aplikácie
        /// </summary>
        /// <returns>Názov detekovanej témy</returns>
        public static string DetectAndLoadSystemTheme()
        {
            try
            {
                string detectedTheme = GetSystemTheme();

                // Pokús sa načítať detekovanú tému
                if (SwitchTheme(detectedTheme))
                {
                    Debug.WriteLine(string.Format("Successfully loaded system theme: {0}", detectedTheme));
                    return detectedTheme;
                }
                else
                {
                    // Fallback na Light ak sa systémová téma nepodarí načítať
                    Debug.WriteLine("Failed to load system theme, falling back to Light");
                    SwitchTheme("Light");
                    return "Light";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error detecting system theme: {0}", ex.Message));
                // Fallback na Light
                SwitchTheme("Light");
                return "Light";
            }
        }

        /// <summary>
        /// Získa aktuálnu systémovú tému z Windows Registry
        /// </summary>
        /// <returns>"Dark" alebo "Light"</returns>
        private static string GetSystemTheme()
        {
            try
            {
                // Windows 10/11 - kontrola dark mode v Registry
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        var appsUseLightTheme = key.GetValue("AppsUseLightTheme");
                        if (appsUseLightTheme != null)
                        {
                            // 0 = Dark mode, 1 = Light mode
                            bool isLightMode = Convert.ToInt32(appsUseLightTheme) == 1;
                            return isLightMode ? "Light" : "Dark";
                        }
                    }
                }

                // Fallback - ak sa registry kľúč nenájde, skús high contrast
                if (SystemParameters.HighContrast)
                {
                    return "HighContrast";
                }

                // Ak nič nevyšlo, vráť Light ako default
                return "Light";
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error reading system theme from registry: {0}", ex.Message));
                return "Light";
            }
        }

        /// <summary>
        /// LoadDefaultTheme metóda ktorá používa systémovú tému
        /// </summary>
        private void LoadDefaultTheme()
        {
            try
            {
                // NEZMAŽE všetky resources! Iba odstráni existujúce theme súbory
                var mergedDictionaries = Application.Current.Resources.MergedDictionaries;

                // Nájde a odstrání iba theme súbory (zachovaj App.xaml resources)
                for (int i = mergedDictionaries.Count - 1; i >= 0; i--)
                {
                    var dict = mergedDictionaries[i];
                    if (dict.Source != null && dict.Source.OriginalString.Contains("Theme.xaml"))
                    {
                        mergedDictionaries.RemoveAt(i);
                    }
                }

                // Detekuje a načíta systémovú tému
                string systemTheme = DetectAndLoadSystemTheme();

                Debug.WriteLine(string.Format("Default theme loaded: {0}", systemTheme));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Failed to load default theme: {0}", ex.Message));

                // Fallback - vytvorí minimálne potrebné zdroje
                CreateFallbackResources();
            }
        }

        /// <summary>
        /// Spustí sledovanie zmien systémovej témy
        /// </summary>
        public static void StartSystemThemeMonitoring()
        {
            try
            {
                _lastKnownSystemTheme = GetSystemTheme();

                // Registruj listener pre zmeny v Registry
                _themeRegistryKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

                if (_themeRegistryKey != null)
                {
                    // Vytvor timer na periodickú kontrolu (každých 5 sekúnd)
                    var timer = new System.Windows.Threading.DispatcherTimer();
                    timer.Interval = TimeSpan.FromSeconds(5);
                    timer.Tick += OnThemeCheckTimer;
                    timer.Start();

                    Debug.WriteLine("System theme monitoring started");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Failed to start theme monitoring: {0}", ex.Message));
            }
        }

        private static void OnThemeCheckTimer(object sender, EventArgs e)
        {
            try
            {
                string currentSystemTheme = GetSystemTheme();

                if (currentSystemTheme != _lastKnownSystemTheme)
                {
                    Debug.WriteLine(string.Format("System theme changed from {0} to {1}", _lastKnownSystemTheme, currentSystemTheme));

                    // Automaticky prepni tému aplikácie
                    SwitchTheme(currentSystemTheme);
                    _lastKnownSystemTheme = currentSystemTheme;

                    // Notify user (voliteľne)
                    Debug.WriteLine(string.Format("Application theme automatically switched to {0}", currentSystemTheme));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error checking system theme changes: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Zastaví sledovanie systémovej témy
        /// </summary>
        public static void StopSystemThemeMonitoring()
        {
            try
            {
                if (_themeRegistryKey != null)
                {
                    _themeRegistryKey.Close();
                    _themeRegistryKey = null;
                }

                Debug.WriteLine("System theme monitoring stopped");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error stopping theme monitoring: {0}", ex.Message));
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
                Debug.WriteLine(string.Format("Error checking for existing instance: {0}", ex.Message));
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
                Debug.WriteLine(string.Format("Failed to bring existing instance to foreground: {0}", ex.Message));
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
                        Debug.WriteLine(string.Format("DPI Awareness already set: {0}", ex.Message));
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
                Debug.WriteLine(string.Format("DPI awareness setup failed: {0}", ex.Message));
            }
        }

        private void SetupExceptionHandling()
        {
            this.DispatcherUnhandledException += (sender, e) =>
            {
                e.Handled = true;  

                LogException(e.Exception, "DispatcherUnhandledException");

#if DEBUG
                Debug.WriteLine("═══════════════════════════════════════════════════════");
                Debug.WriteLine("DEBUG - UNHANDLED EXCEPTION:");
                Debug.WriteLine(e.Exception.ToString());
                Debug.WriteLine("═══════════════════════════════════════════════════════");
#else
                MessageBox.Show(string.Format("An unexpected error occurred:\n{0}\n\nThe application will continue running.", e.Exception.Message),
                "Sercull Error", MessageBoxButton.OK, MessageBoxImage.Warning);
#endif

            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var exception = e.ExceptionObject as Exception;
                LogException(exception, "UnhandledException");

                if (e.IsTerminating)
                {
                    string message = exception?.Message ?? "Unknown critical error";
                    //#if DEBUG
                    //                    message = string.Format("DEBUG - Critical Error:\n{0}", exception);
                    //#endif

                    //                    MessageBox.Show(string.Format("A critical error occurred and the application must close:\n{0}", message),
                    //                        "AppCommander Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);

                #if DEBUG
                    Debug.WriteLine("═══════════════════════════════════════════════════════");
                    Debug.WriteLine("DEBUG - CRITICAL ERROR (TERMINATING):");
                    Debug.WriteLine(exception?.ToString() ?? "Unknown error");
                    Debug.WriteLine("═══════════════════════════════════════════════════════");
#else
                    try
                    {
                        MessageBox.Show(string.Format("A critical error occurred and the application must close:\n{0}", message),
                            "Sercull Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    catch
                    {
                        Debug.WriteLine(string.Format("Failed to show critical error: {0}", message));
                    }
#endif
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

                Debug.WriteLine(string.Format("[{0}] {1}:", timestamp, source));
                Debug.WriteLine(string.Format("  OS: {0}", osInfo));
                Debug.WriteLine(string.Format("  DPI Scale: {0:F2}x", dpiScale));
                Debug.WriteLine(string.Format("  Exception: {0}", exception));
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
                    return string.Format("Windows 11 (Build {0})", version.Build);
                }
                return string.Format("Windows 10 (Build {0})", version.Build);
            }

            if (version.Major == 6)
            {
                if (version.Minor == 3) return "Windows 8.1";
                if (version.Minor == 2) return "Windows 8";
                if (version.Minor == 1) return "Windows 7";
                if (version.Minor == 0) return "Windows Vista";
            }

            return string.Format("Windows {0}.{1} (Build {2})", version.Major, version.Minor, version.Build);
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
                Debug.WriteLine(string.Format("Error releasing mutex: {0}", ex.Message));
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                Debug.WriteLine(string.Format("AppCommander exiting gracefully (Exit Code: {0})", e.ApplicationExitCode));

                // Zastav sledovanie témy
                StopSystemThemeMonitoring();

                // Release the single instance mutex
                ReleaseMutex();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error during application exit: {0}", ex.Message));
            }

            base.OnExit(e);
        }
    }
}
