using AppCommander.W7_11.WPF.Core;
using AppCommander.W7_11.WPF.Core.Managers;
using System.Windows;
using System.Windows.Controls;

namespace AppCommander.W7_11.WPF.Controls
{
    /// <summary>
    /// UserControl pre prepínanie používateľských režimov
    /// Zobrazuje 4 tlačidlá (Accountant, Tester, Worker, Developer) + aktuálny režim
    /// </summary>
    public partial class UserModeRibbon : UserControl
    {
        #region Fields

        private readonly UserModeManager _modeManager;

        #endregion

        #region Constructor

        public UserModeRibbon()
        {
            InitializeComponent();

            _modeManager = UserModeManager.Instance;
            _modeManager.ModeChanged += OnModeChanged;

            UpdateButtonStates();
        }

        #endregion

        #region Event Handlers

        private void BtnAccountant_Click(object sender, RoutedEventArgs e)
        {
            _modeManager.SetMode(UserMode.Accountant);
        }

        private void BtnTester_Click(object sender, RoutedEventArgs e)
        {
            _modeManager.SetMode(UserMode.Tester);
        }

        private void BtnWorker_Click(object sender, RoutedEventArgs e)
        {
            _modeManager.SetMode(UserMode.Worker);
        }

        private void BtnDeveloper_Click(object sender, RoutedEventArgs e)
        {
            _modeManager.SetMode(UserMode.Developer);
        }

        private void OnModeChanged(object sender, UserMode newMode)
        {
            Dispatcher.Invoke(() => UpdateButtonStates());
        }

        #endregion

        #region Private Methods

        private void UpdateButtonStates()
        {
            var currentMode = _modeManager.CurrentMode;

            // Reset všetkých tlačidiel
            SetButtonActive(BtnAccountant, currentMode == UserMode.Accountant);
            SetButtonActive(BtnTester, currentMode == UserMode.Tester);
            SetButtonActive(BtnWorker, currentMode == UserMode.Worker);
            SetButtonActive(BtnDeveloper, currentMode == UserMode.Developer);

            // Update label
            TxtCurrentMode.Text = currentMode.GetDisplayName();
        }

        private void SetButtonActive(Button button, bool isActive)
        {
            if (isActive)
            {
                button.Style = (Style)FindResource("ActiveModeButtonStyle");
            }
            else
            {
                button.Style = (Style)FindResource("ModeButtonStyle");
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Volané pri odstránení kontrolu
        /// </summary>
        public void Cleanup()
        {
            _modeManager.ModeChanged -= OnModeChanged;
        }

        #endregion
    }
}
