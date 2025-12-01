using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace AppCommander.W7_11.WPF.Core.Managers
{
    /// <summary>
    /// Spravuje témovanie aplikácie (Light, Dark, HighContrast, System)
    /// </summary>
    public class ThemeManager
    {
        #region Win32 API Imports

        [DllImport("UxTheme.dll", SetLastError = true, EntryPoint = "#138")]
        private static extern bool ShouldSystemUseDarkMode();

        [DllImport("UxTheme.dll", SetLastError = true, EntryPoint = "#137")]
        private static extern bool ShouldAppsUseDarkMode();

        #endregion

        #region Public Methods

        /// <summary>
        /// Aplikuje zvolenú tému na aplikáciu
        /// </summary>
        /// <param name="theme">Light, Dark, HighContrast alebo System</param>
        public void SetTheme(string theme)
        {
            try
            {
                ResourceDictionary themeDict = new ResourceDictionary();
                Debug.WriteLine($"SetTheme: {theme}");

                switch (theme)
                {
                    case "Light":
                        themeDict.Source = new Uri("Themes/LightTheme.xaml", UriKind.Relative);
                        break;
                    case "Dark":
                        themeDict.Source = new Uri("Themes/DarkTheme.xaml", UriKind.Relative);
                        break;
                    case "HighContrast":
                        themeDict.Source = new Uri("Themes/HighContrastTheme.xaml", UriKind.Relative);
                        break;
                    case "System":
                    default:
                        var isSystemDark = IsSystemInDarkMode();
                        themeDict.Source = isSystemDark ?
                            new Uri("Themes/DarkTheme.xaml", UriKind.Relative) :
                            new Uri("Themes/LightTheme.xaml", UriKind.Relative);
                        break;
                }

                // Odstráň existujúce theme dictionaries
                var existingDictionaries = Application.Current.Resources.MergedDictionaries
                    .Where(d => d.Source != null &&
                               (d.Source.OriginalString.Contains("LightTheme.xaml") ||
                                d.Source.OriginalString.Contains("DarkTheme.xaml") ||
                                d.Source.OriginalString.Contains("HighContrastTheme.xaml")))
                    .ToList();

                foreach (var dict in existingDictionaries)
                {
                    Application.Current.Resources.MergedDictionaries.Remove(dict);
                }

                // Pridaj nový theme dictionary
                Application.Current.Resources.MergedDictionaries.Add(themeDict);

                Debug.WriteLine($"Theme changed to: {theme}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying theme '{theme}': {ex.Message}");
            }
        }

        /// <summary>
        /// Zistí či je systém v dark mode pomocou Windows Registry
        /// </summary>
        /// <returns>True ak je dark mode aktívny, false ak light mode</returns>
        public bool IsSystemInDarkMode()
        {
            try
            {
                // Skús najprv registry metódu (najspoľahlivejšia)
                const string registryKeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
                const string appsValueName = "AppsUseLightTheme";
                const string systemValueName = "SystemUsesLightTheme";

                // Skontroluj apps theme preference
                var appsValue = Registry.GetValue(registryKeyPath, appsValueName, null);
                if (appsValue is int appsTheme)
                {
                    Debug.WriteLine($"Apps theme from registry: {(appsTheme == 0 ? "Dark" : "Light")}");
                    return appsTheme == 0; // 0 = dark, 1 = light
                }

                // Fallback na system theme
                var systemValue = Registry.GetValue(registryKeyPath, systemValueName, null);
                if (systemValue is int sysTheme)
                {
                    Debug.WriteLine($"System theme from registry: {(sysTheme == 0 ? "Dark" : "Light")}");
                    return sysTheme == 0;
                }

                // Ak registry values neexistujú, skús Win32 API
                return TryWin32DarkModeDetection();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Registry dark mode detection failed: {ex.Message}");
                return TryWin32DarkModeDetection();
            }
        }

        /// <summary>
        /// Zistí či je systém vo Windows 10 build 1809+ pre plnú dark mode podporu
        /// </summary>
        public bool IsSystemInDarkModeExtended()
        {
            try
            {
                // 1. Skontroluj Windows verziu
                var osVersion = Environment.OSVersion.Version;
                if (osVersion.Major < 10)
                {
                    Debug.WriteLine("Windows version < 10, no dark mode support");
                    return false; // Windows 8.1 a staršie nemajú dark mode
                }

                // 2. Pre Windows 10 build 1809+ (17763+)
                if (osVersion.Major == 10 && osVersion.Build >= 17763)
                {
                    return IsSystemInDarkMode(); // Použij plnú detekciu
                }

                // 3. Pre staršie Windows 10 buildy
                if (osVersion.Major == 10)
                {
                    // Len registry-based detection
                    const string registryKeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
                    var appsValue = Registry.GetValue(registryKeyPath, "AppsUseLightTheme", null);

                    if (appsValue is int theme)
                    {
                        return theme == 0;
                    }
                }

                return false; // Default light mode
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Extended dark mode detection failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Zapíše debug informácie o theme detection
        /// </summary>
        public void LogThemeDetectionInfo()
        {
            try
            {
                Debug.WriteLine("=== Theme Detection Debug Info ===");
                Debug.WriteLine($"OS Version: {Environment.OSVersion.VersionString}");
                Debug.WriteLine($"OS Build: {Environment.OSVersion.Version.Build}");

                const string regPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
                var appsValue = Registry.GetValue(regPath, "AppsUseLightTheme", "Not Found");
                var systemValue = Registry.GetValue(regPath, "SystemUsesLightTheme", "Not Found");

                Debug.WriteLine($"Registry AppsUseLightTheme: {appsValue}");
                Debug.WriteLine($"Registry SystemUsesLightTheme: {systemValue}");

                try
                {
                    bool shouldUseDark = ShouldAppsUseDarkMode();
                    Debug.WriteLine($"Win32 API ShouldAppsUseDarkMode: {shouldUseDark}");
                }
                catch
                {
                    Debug.WriteLine("Win32 API not available");
                }

                Debug.WriteLine("=================================");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to log theme detection info: {ex.Message}");
            }
        }

        #endregion

        #region Private Methods

        private bool TryWin32DarkModeDetection()
        {
            try
            {
                // Skús použiť UxTheme API
                bool shouldUseDark = ShouldAppsUseDarkMode();
                Debug.WriteLine($"Win32 API dark mode detection: {shouldUseDark}");
                return shouldUseDark;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Win32 API dark mode detection failed: {ex.Message}");

                // Posledný fallback - skús high contrast mode
                return DetectHighContrastMode();
            }
        }

        private bool DetectHighContrastMode()
        {
            try
            {
                const string hcKeyPath = @"HKEY_CURRENT_USER\Control Panel\Accessibility\HighContrast";
                const string flagsValueName = "Flags";

                var hcValue = Registry.GetValue(hcKeyPath, flagsValueName, null);
                if (hcValue is string hcFlags)
                {
                    // High contrast flag "1" znamená že je aktívny
                    bool isHighContrast = hcFlags.Contains("1");
                    if (isHighContrast)
                    {
                        Debug.WriteLine("High contrast mode detected, treating as dark theme");
                        return true; // High contrast často používa tmavé farby
                    }
                }

                Debug.WriteLine("No theme detection successful, defaulting to light mode");
                return false; // Default light mode
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"High contrast detection failed: {ex.Message}");
                return false; // Ultimate fallback - light mode
            }
        }

        #endregion
    }
}
