using AppCommander.W7_11.WPF.Core;
using AppCommander.W7_11.WPF.Core.Managers;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AppCommander.W7_11.WPF
{
    /// <summary>
    /// Welcome Wizard - 3-kroková úvodná obrazovka
    /// Krok 1: Výber profilu (Accountant, Tester, Worker, Developer)
    /// Krok 2: Nastavenia špecifické pre profil
    /// Krok 3: Potvrdenie a spustenie
    /// </summary>
    public partial class WelcomeWizard : Window
    {
        #region Fields

        private readonly UserModeManager _modeManager;
        private int _currentStep = 1;
        private UserMode _selectedMode = UserMode.Worker;

        #endregion

        #region Constructor

        public WelcomeWizard()
        {
            InitializeComponent();
            _modeManager = UserModeManager.Instance;

            // Ak je už nastavený režim, použijeme ho ako default
            _selectedMode = _modeManager.CurrentMode;

            UpdateStepDisplay();
        }

        #endregion

        #region Mode Selection (Step 1)

        private void BtnSelectAccountant_Click(object sender, RoutedEventArgs e)
        {
            SelectMode(UserMode.Accountant);
        }

        private void BtnSelectTester_Click(object sender, RoutedEventArgs e)
        {
            SelectMode(UserMode.Tester);
        }

        private void BtnSelectWorker_Click(object sender, RoutedEventArgs e)
        {
            SelectMode(UserMode.Worker);
        }

        private void BtnSelectDeveloper_Click(object sender, RoutedEventArgs e)
        {
            SelectMode(UserMode.Developer);
        }

        private void SelectMode(UserMode mode)
        {
            _selectedMode = mode;

            // Vizuálne zvýraznenie vybraného tlačidla
            HighlightSelectedModeButton();

            // Aktualizuj popis
            TxtModeDescription.Text = mode.GetDescription();

            // Povoliť Next tlačidlo
            BtnNext.IsEnabled = true;
        }

        private void HighlightSelectedModeButton()
        {
            Brush accentBrush;
            Brush defaultBrush;

            try
            {
                accentBrush = (Brush)FindResource("AccentBrush");
                defaultBrush = (Brush)FindResource("BorderBrush");
            }
            catch
            {
                accentBrush = Brushes.DodgerBlue;
                defaultBrush = Brushes.Gray;
            }

            BtnSelectAccountant.BorderBrush = _selectedMode == UserMode.Accountant ? accentBrush : defaultBrush;
            BtnSelectTester.BorderBrush = _selectedMode == UserMode.Tester ? accentBrush : defaultBrush;
            BtnSelectWorker.BorderBrush = _selectedMode == UserMode.Worker ? accentBrush : defaultBrush;
            BtnSelectDeveloper.BorderBrush = _selectedMode == UserMode.Developer ? accentBrush : defaultBrush;
        }

        #endregion

        #region Navigation

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep < 3)
            {
                _currentStep++;
                UpdateStepDisplay();
            }
            else
            {
                FinishWizard();
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 1)
            {
                _currentStep--;
                UpdateStepDisplay();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            // Ak používateľ zavrie wizard, ponechaj aktuálny režim
            DialogResult = false;
            Close();
        }

        #endregion

        #region Step Management

        private void UpdateStepDisplay()
        {
            // Skry všetky kroky
            Step1_ModeSelection.Visibility = Visibility.Collapsed;
            Step2_Settings.Visibility = Visibility.Collapsed;
            Step3_Finish.Visibility = Visibility.Collapsed;

            // Zobraz aktuálny krok
            switch (_currentStep)
            {
                case 1:
                    Step1_ModeSelection.Visibility = Visibility.Visible;
                    TxtSubtitle.Text = "Krok 1 z 3 - Vyberte váš profil";
                    BtnBack.Visibility = Visibility.Collapsed;
                    BtnNext.Content = "Ďalej →";
                    BtnNext.IsEnabled = false; // Dokiaľ nevyberie režim
                    break;

                case 2:
                    Step2_Settings.Visibility = Visibility.Visible;
                    TxtSubtitle.Text = "Krok 2 z 3 - Nastavenia";
                    BtnBack.Visibility = Visibility.Visible;
                    BtnNext.Content = "Ďalej →";
                    BtnNext.IsEnabled = true;
                    ShowModeSpecificSettings();
                    break;

                case 3:
                    Step3_Finish.Visibility = Visibility.Visible;
                    TxtSubtitle.Text = "Krok 3 z 3 - Hotovo";
                    BtnBack.Visibility = Visibility.Visible;
                    BtnNext.Content = "Spustiť Senaro ✨";
                    BtnNext.IsEnabled = true;
                    UpdateFinishSummary();
                    break;
            }

            // Aktualizuj indikátory
            UpdateStepIndicators();
        }

        private void ShowModeSpecificSettings()
        {
            // Skry všetky settings panely
            AccountantSettings.Visibility = Visibility.Collapsed;
            TesterSettings.Visibility = Visibility.Collapsed;
            WorkerSettings.Visibility = Visibility.Collapsed;
            DeveloperSettings.Visibility = Visibility.Collapsed;

            TxtStep2Title.Text = $"Nastavenia pre {_selectedMode.GetIcon()} {_selectedMode.GetDisplayName()}";

            // Zobraz príslušný panel
            switch (_selectedMode)
            {
                case UserMode.Accountant:
                    AccountantSettings.Visibility = Visibility.Visible;
                    LoadAccountantSettings();
                    break;
                case UserMode.Tester:
                    TesterSettings.Visibility = Visibility.Visible;
                    LoadTesterSettings();
                    break;
                case UserMode.Worker:
                    WorkerSettings.Visibility = Visibility.Visible;
                    LoadWorkerSettings();
                    break;
                case UserMode.Developer:
                    DeveloperSettings.Visibility = Visibility.Visible;
                    LoadDeveloperSettings();
                    break;
            }
        }

        private void LoadAccountantSettings()
        {
            var settings = _modeManager.Settings.AccountantSettings;
            ChkAutoProcess.IsChecked = settings.AutoProcessNewFiles;
            CmbOutputFormat.SelectedIndex = settings.OutputFormat == "CSV" ? 1 : 0;
        }

        private void LoadTesterSettings()
        {
            var settings = _modeManager.Settings.TesterSettings;
            ChkEnableInspector.IsChecked = settings.EnableUIInspector;
            ChkDebugOverlay.IsChecked = settings.EnableDebugOverlay;
            ChkRealtimeHighlight.IsChecked = settings.ShowRealtimeHighlight;
        }

        private void LoadWorkerSettings()
        {
            var settings = _modeManager.Settings.WorkerSettings;
            ChkShowRecommended.IsChecked = settings.ShowRecommendedSequences;
        }

        private void LoadDeveloperSettings()
        {
            var settings = _modeManager.Settings.DeveloperSettings;
            ChkVerboseLogging.IsChecked = settings.VerboseLogging;
            ChkDebugModules.IsChecked = settings.EnableDebugModules;
            ChkAutoDebugConsole.IsChecked = settings.AutoOpenDebugConsole;
        }

        private void SaveModeSpecificSettings()
        {
            switch (_selectedMode)
            {
                case UserMode.Accountant:
                    _modeManager.Settings.AccountantSettings.AutoProcessNewFiles = ChkAutoProcess.IsChecked == true;
                    _modeManager.Settings.AccountantSettings.OutputFormat =
                        CmbOutputFormat.SelectedIndex == 1 ? "CSV" : "Excel";
                    break;

                case UserMode.Tester:
                    _modeManager.Settings.TesterSettings.EnableUIInspector = ChkEnableInspector.IsChecked == true;
                    _modeManager.Settings.TesterSettings.EnableDebugOverlay = ChkDebugOverlay.IsChecked == true;
                    _modeManager.Settings.TesterSettings.ShowRealtimeHighlight = ChkRealtimeHighlight.IsChecked == true;
                    break;

                case UserMode.Worker:
                    _modeManager.Settings.WorkerSettings.ShowRecommendedSequences = ChkShowRecommended.IsChecked == true;
                    break;

                case UserMode.Developer:
                    _modeManager.Settings.DeveloperSettings.VerboseLogging = ChkVerboseLogging.IsChecked == true;
                    _modeManager.Settings.DeveloperSettings.EnableDebugModules = ChkDebugModules.IsChecked == true;
                    _modeManager.Settings.DeveloperSettings.AutoOpenDebugConsole = ChkAutoDebugConsole.IsChecked == true;
                    break;
            }
        }

        private void UpdateStepIndicators()
        {
            Brush accentBrush;
            Brush inactiveBrush;

            try
            {
                accentBrush = (Brush)FindResource("AccentBrush");
                inactiveBrush = (Brush)FindResource("BorderBrush");
            }
            catch
            {
                accentBrush = Brushes.DodgerBlue;
                inactiveBrush = Brushes.Gray;
            }

            Step1Indicator.Fill = _currentStep >= 1 ? accentBrush : inactiveBrush;
            Step2Indicator.Fill = _currentStep >= 2 ? accentBrush : inactiveBrush;
            Step3Indicator.Fill = _currentStep >= 3 ? accentBrush : inactiveBrush;
        }

        private void UpdateFinishSummary()
        {
            TxtFinishSummary.Text = $"Profil: {_selectedMode.GetIcon()} {_selectedMode.GetDisplayName()}";

            // Dočasne nastavíme režim pre získanie features
            _modeManager.SetMode(_selectedMode);

            var features = _modeManager.GetEnabledFeatures();
            TxtFinishFeatures.Text = $"Aktívne funkcie: {string.Join(", ", features)}";
        }

        #endregion

        #region Finish

        private void FinishWizard()
        {
            // Uložíme nastavenia špecifické pre režim
            SaveModeSpecificSettings();

            // Nastavíme vybraný režim
            _modeManager.SetMode(_selectedMode);

            // Označíme wizard ako dokončený
            _modeManager.CompleteWelcomeWizard();

            // Uložíme všetko
            _modeManager.SaveSettings();

            DialogResult = true;
            Close();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Zobrazí wizard a vráti vybraný režim
        /// </summary>
        public static UserMode? ShowWizard(Window owner = null)
        {
            var wizard = new WelcomeWizard();

            if (owner != null)
            {
                wizard.Owner = owner;
            }

            var result = wizard.ShowDialog();

            if (result == true)
            {
                return UserModeManager.Instance.CurrentMode;
            }

            return null;
        }

        #endregion
    }
}
