using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;

namespace AppCommander.W7_11.WPF.Core
{
    /// <summary>
    /// Nastavenia aplikácie ukladané do JSON v AppData
    /// </summary>
    public class AppSettings
    {
        #region Properties

        /// <summary>
        /// Aktuálny používateľský režim
        /// </summary>
        public UserMode CurrentMode { get; set; } = UserMode.Worker;

        /// <summary>
        /// Vybraná téma (Light, Dark, HighContrast, System)
        /// </summary>
        public string Theme { get; set; } = "System";

        /// <summary>
        /// Či bol Welcome Wizard dokončený
        /// </summary>
        public bool WelcomeWizardCompleted { get; set; } = false;

        /// <summary>
        /// Dátum posledného spustenia
        /// </summary>
        public DateTime LastRun { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Verzia aplikácie pri poslednom spustení (pre migráciu)
        /// </summary>
        public string LastVersion { get; set; } = "";

        #region Per-Mode Settings

        /// <summary>
        /// Nastavenia pre Accountant režim
        /// </summary>
        public AccountantModeSettings AccountantSettings { get; set; } = new AccountantModeSettings();

        /// <summary>
        /// Nastavenia pre Tester režim
        /// </summary>
        public TesterModeSettings TesterSettings { get; set; } = new TesterModeSettings();

        /// <summary>
        /// Nastavenia pre Worker režim
        /// </summary>
        public WorkerModeSettings WorkerSettings { get; set; } = new WorkerModeSettings();

        /// <summary>
        /// Nastavenia pre Developer režim
        /// </summary>
        public DeveloperModeSettings DeveloperSettings { get; set; } = new DeveloperModeSettings();

        #endregion

        #endregion

        #region Static Methods

        private static readonly string SettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Senaro");

        private static readonly string SettingsPath = Path.Combine(SettingsFolder, "settings.json");

        /// <summary>
        /// Načíta nastavenia zo súboru alebo vráti default
        /// </summary>
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);

                    if (settings != null)
                    {
                        Debug.WriteLine($"Settings loaded: Mode={settings.CurrentMode}, Theme={settings.Theme}");
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading settings: {ex.Message}");
            }

            Debug.WriteLine("Using default settings");
            return new AppSettings();
        }

        /// <summary>
        /// Uloží nastavenia do súboru
        /// </summary>
        public void Save()
        {
            try
            {
                // Vytvor priečinok ak neexistuje
                if (!Directory.Exists(SettingsFolder))
                {
                    Directory.CreateDirectory(SettingsFolder);
                }

                LastRun = DateTime.Now;

                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(SettingsPath, json);

                Debug.WriteLine($"Settings saved to: {SettingsPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Resetuje nastavenia na default
        /// </summary>
        public static AppSettings Reset()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    File.Delete(SettingsPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting settings file: {ex.Message}");
            }

            return new AppSettings();
        }

        /// <summary>
        /// Vráti cestu k settings súboru
        /// </summary>
        public static string GetSettingsPath() => SettingsPath;

        #endregion
    }

    #region Per-Mode Settings Classes

    /// <summary>
    /// Nastavenia pre Accountant režim
    /// </summary>
    public class AccountantModeSettings
    {
        /// <summary>
        /// Výstupný priečinok pre spracované dokumenty
        /// </summary>
        public string OutputFolder { get; set; } = "";

        /// <summary>
        /// Automaticky spracovať nové súbory
        /// </summary>
        public bool AutoProcessNewFiles { get; set; } = false;

        /// <summary>
        /// Preferovaný výstupný formát (CSV, Excel)
        /// </summary>
        public string OutputFormat { get; set; } = "Excel";
    }

    /// <summary>
    /// Nastavenia pre Tester režim
    /// </summary>
    public class TesterModeSettings
    {
        /// <summary>
        /// Povoliť UI Inspector
        /// </summary>
        public bool EnableUIInspector { get; set; } = true;

        /// <summary>
        /// Povoliť Debug Overlay
        /// </summary>
        public bool EnableDebugOverlay { get; set; } = false;

        /// <summary>
        /// Zobraziť realtime highlight
        /// </summary>
        public bool ShowRealtimeHighlight { get; set; } = true;
    }

    /// <summary>
    /// Nastavenia pre Worker režim
    /// </summary>
    public class WorkerModeSettings
    {
        /// <summary>
        /// Zobraziť odporúčané sekvencie
        /// </summary>
        public bool ShowRecommendedSequences { get; set; } = true;

        /// <summary>
        /// Posledný použitý priečinok so sekvenciami
        /// </summary>
        public string LastSequenceFolder { get; set; } = "";
    }

    /// <summary>
    /// Nastavenia pre Developer režim
    /// </summary>
    public class DeveloperModeSettings
    {
        /// <summary>
        /// Verbose logging
        /// </summary>
        public bool VerboseLogging { get; set; } = false;

        /// <summary>
        /// Povoliť debug moduly
        /// </summary>
        public bool EnableDebugModules { get; set; } = true;

        /// <summary>
        /// Automaticky otvoriť debug konzolu
        /// </summary>
        public bool AutoOpenDebugConsole { get; set; } = false;
    }

    #endregion
}
