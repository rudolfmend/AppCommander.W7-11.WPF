using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;

namespace AppCommander.W7_11.WPF.Core.Managers
{
    /// <summary>
    /// Spravuje používateľské režimy a viditeľnosť UI elementov
    /// Implementuje INotifyPropertyChanged pre XAML binding
    /// </summary>
    public class UserModeManager : INotifyPropertyChanged
    {
        #region Singleton

        private static UserModeManager _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Singleton inštancia
        /// </summary>
        public static UserModeManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new UserModeManager();
                        }
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Fields

        private AppSettings _settings;
        private UserMode _currentMode;

        #endregion

        #region Properties

        /// <summary>
        /// Aktuálny používateľský režim
        /// </summary>
        public UserMode CurrentMode
        {
            get => _currentMode;
            private set
            {
                if (_currentMode != value)
                {
                    _currentMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CurrentModeIcon));
                    OnPropertyChanged(nameof(CurrentModeDisplayName));
                    OnPropertyChanged(nameof(IsAccountantMode));
                    OnPropertyChanged(nameof(IsTesterMode));
                    OnPropertyChanged(nameof(IsWorkerMode));
                    OnPropertyChanged(nameof(IsDeveloperMode));

                    // Visibility properties
                    OnPropertyChanged(nameof(ShowRecording));
                    OnPropertyChanged(nameof(ShowPlayback));
                    OnPropertyChanged(nameof(ShowUIInspector));
                    OnPropertyChanged(nameof(ShowElementStats));
                    OnPropertyChanged(nameof(ShowDebugPanel));
                    OnPropertyChanged(nameof(ShowDocumentProcessing));
                    OnPropertyChanged(nameof(ShowSequenceList));
                    OnPropertyChanged(nameof(ShowAdvancedTools));

                    ModeChanged?.Invoke(this, value);
                }
            }
        }

        /// <summary>
        /// Ikona aktuálneho režimu
        /// </summary>
        public string CurrentModeIcon => CurrentMode.GetIcon();

        /// <summary>
        /// Názov aktuálneho režimu
        /// </summary>
        public string CurrentModeDisplayName => CurrentMode.GetDisplayName();

        /// <summary>
        /// Prístup k nastaveniam
        /// </summary>
        public AppSettings Settings => _settings;

        #region Mode Checks

        public bool IsAccountantMode => CurrentMode == UserMode.Accountant;
        public bool IsTesterMode => CurrentMode == UserMode.Tester;
        public bool IsWorkerMode => CurrentMode == UserMode.Worker;
        public bool IsDeveloperMode => CurrentMode == UserMode.Developer;

        #endregion

        #region UI Visibility Properties (for XAML binding)

        /// <summary>
        /// Zobraziť Recording funkcie
        /// Viditeľné pre: Tester, Developer
        /// </summary>
        public Visibility ShowRecording =>
            (IsTesterMode || IsDeveloperMode) ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Zobraziť Playback funkcie
        /// Viditeľné pre: Worker, Tester, Developer
        /// </summary>
        public Visibility ShowPlayback =>
            (IsWorkerMode || IsTesterMode || IsDeveloperMode) ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Zobraziť UI Inspector
        /// Viditeľné pre: Tester, Developer
        /// </summary>
        public Visibility ShowUIInspector =>
            (IsTesterMode || IsDeveloperMode) ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Zobraziť Element Statistics
        /// Viditeľné pre: Tester, Developer
        /// </summary>
        public Visibility ShowElementStats =>
            (IsTesterMode || IsDeveloperMode) ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Zobraziť Debug Panel
        /// Viditeľné pre: Developer
        /// </summary>
        public Visibility ShowDebugPanel =>
            IsDeveloperMode ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Zobraziť Document Processing (drag & drop)
        /// Viditeľné pre: Accountant, Developer
        /// </summary>
        public Visibility ShowDocumentProcessing =>
            (IsAccountantMode || IsDeveloperMode) ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Zobraziť Sequence List
        /// Viditeľné pre: Worker, Tester, Developer
        /// </summary>
        public Visibility ShowSequenceList =>
            (IsWorkerMode || IsTesterMode || IsDeveloperMode) ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Zobraziť Advanced Tools (menu items, etc.)
        /// Viditeľné pre: Tester, Developer
        /// </summary>
        public Visibility ShowAdvancedTools =>
            (IsTesterMode || IsDeveloperMode) ? Visibility.Visible : Visibility.Collapsed;

        #endregion

        #endregion

        #region Events

        /// <summary>
        /// Event pri zmene režimu
        /// </summary>
        public event EventHandler<UserMode> ModeChanged;

        /// <summary>
        /// INotifyPropertyChanged event
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Constructor

        private UserModeManager()
        {
            _settings = AppSettings.Load();
            _currentMode = _settings.CurrentMode;

            Debug.WriteLine($"UserModeManager initialized: Mode={_currentMode}");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Nastaví nový používateľský režim
        /// </summary>
        public void SetMode(UserMode mode)
        {
            if (CurrentMode == mode) return;

            Debug.WriteLine($"Changing mode from {CurrentMode} to {mode}");

            CurrentMode = mode;
            _settings.CurrentMode = mode;
            _settings.Save();
        }

        /// <summary>
        /// Prepne na ďalší režim (cyklicky)
        /// </summary>
        public void CycleMode()
        {
            var modes = Enum.GetValues(typeof(UserMode));
            int currentIndex = (int)CurrentMode;
            int nextIndex = (currentIndex + 1) % modes.Length;

            SetMode((UserMode)nextIndex);
        }

        /// <summary>
        /// Označí Welcome Wizard ako dokončený
        /// </summary>
        public void CompleteWelcomeWizard()
        {
            _settings.WelcomeWizardCompleted = true;
            _settings.Save();
        }

        /// <summary>
        /// Zistí či treba zobraziť Welcome Wizard
        /// </summary>
        public bool ShouldShowWelcomeWizard()
        {
            return !_settings.WelcomeWizardCompleted;
        }

        /// <summary>
        /// Resetuje nastavenia a zobrazí Welcome Wizard pri ďalšom štarte
        /// </summary>
        public void ResetWelcomeWizard()
        {
            _settings.WelcomeWizardCompleted = false;
            _settings.Save();
        }

        /// <summary>
        /// Skontroluje či je daná feature povolená pre aktuálny režim
        /// </summary>
        public bool IsFeatureEnabled(string featureName)
        {
            var lowerFeature = featureName.ToLower();
            if (lowerFeature == "recording")
                return IsTesterMode || IsDeveloperMode;
            if (lowerFeature == "playback")
                return IsWorkerMode || IsTesterMode || IsDeveloperMode;
            if (lowerFeature == "uiinspector")
                return IsTesterMode || IsDeveloperMode;
            if (lowerFeature == "elementstats")
                return IsTesterMode || IsDeveloperMode;
            if (lowerFeature == "debug")
                return IsDeveloperMode;
            if (lowerFeature == "documentprocessing")
                return IsAccountantMode || IsDeveloperMode;
            if (lowerFeature == "sequencelist")
                return IsWorkerMode || IsTesterMode || IsDeveloperMode;
            if (lowerFeature == "advancedtools")
                return IsTesterMode || IsDeveloperMode;
            // Developer má všetko
            return IsDeveloperMode;
        }

        /// <summary>
        /// Vráti zoznam povolených features pre aktuálny režim
        /// </summary>
        public List<string> GetEnabledFeatures()
        {
            var features = new List<string>();

            if (IsFeatureEnabled("recording")) features.Add("Recording");
            if (IsFeatureEnabled("playback")) features.Add("Playback");
            if (IsFeatureEnabled("uiinspector")) features.Add("UI Inspector");
            if (IsFeatureEnabled("elementstats")) features.Add("Element Statistics");
            if (IsFeatureEnabled("debug")) features.Add("Debug Panel");
            if (IsFeatureEnabled("documentprocessing")) features.Add("Document Processing");
            if (IsFeatureEnabled("sequencelist")) features.Add("Sequence List");
            if (IsFeatureEnabled("advancedtools")) features.Add("Advanced Tools");

            return features;
        }

        /// <summary>
        /// Uloží aktuálne nastavenia
        /// </summary>
        public void SaveSettings()
        {
            _settings.Save();
        }

        /// <summary>
        /// Načíta nastavenia znovu zo súboru
        /// </summary>
        public void ReloadSettings()
        {
            _settings = AppSettings.Load();
            CurrentMode = _settings.CurrentMode;
        }

        #endregion

        #region INotifyPropertyChanged

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
