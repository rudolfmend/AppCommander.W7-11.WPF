using AppCommander.W7_11.WPF.Core;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AppCommander.W7_11.WPF
{
    public partial class ExecutionSpeedControl : UserControl
    {
        public static readonly DependencyProperty ExecutionSpeedProperty =
            DependencyProperty.Register("ExecutionSpeed", typeof(ExecutionSpeed),
                typeof(ExecutionSpeedControl), new PropertyMetadata(ExecutionSpeed.Normal, OnExecutionSpeedChanged));

        public static readonly DependencyProperty ShowAdvancedOptionsProperty =
            DependencyProperty.Register("ShowAdvancedOptions", typeof(bool),
                typeof(ExecutionSpeedControl), new PropertyMetadata(false));

        public ExecutionSpeed ExecutionSpeed
        {
            get => (ExecutionSpeed)GetValue(ExecutionSpeedProperty);
            set => SetValue(ExecutionSpeedProperty, value);
        }

        public bool ShowAdvancedOptions
        {
            get => (bool)GetValue(ShowAdvancedOptionsProperty);
            set => SetValue(ShowAdvancedOptionsProperty, value);
        }

        public event EventHandler<ExecutionSpeedChangedEventArgs> ExecutionSpeedChanged;

        private CommandExecutionManager executionManager;

        public ExecutionSpeedControl()
        {
            InitializeComponent();
            DataContext = this;
            UpdateSpeedLabel();
        }

        public void Initialize(CommandExecutionManager executionManager)
        {
            this.executionManager = executionManager;
            LoadCurrentSettings();
        }

        private static void OnExecutionSpeedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ExecutionSpeedControl control)
            {
                control.ApplySpeedSettings((ExecutionSpeed)e.NewValue);
                control.UpdateSpeedLabel();
            }
        }

        private void UpdateSpeedLabel()
        {
            if (speedLabel == null) return;

            string description;
            string statusText;

            switch (ExecutionSpeed)
            {
                case ExecutionSpeed.VerySlow:
                    description = "Pre veľmi pomalé aplikácie alebo nestabilné systémy";
                    statusText = "Veľmi pomalá";
                    break;
                case ExecutionSpeed.Slow:
                    description = "Pre pomalšie aplikácie alebo komplexné UI";
                    statusText = "Pomalá";
                    break;
                case ExecutionSpeed.Normal:
                    description = "Štandardná rýchlosť pre väčšinu aplikácií";
                    statusText = "Normálna";
                    break;
                case ExecutionSpeed.Fast:
                    description = "Pre rýchle aplikácie a jednoduchý UI";
                    statusText = "Rýchla";
                    break;
                case ExecutionSpeed.VeryFast:
                    description = "Maximálna rýchlosť - len pre najrýchlejšie systémy";
                    statusText = "Veľmi rýchla";
                    break;
                case ExecutionSpeed.Custom:
                    description = "Vlastné nastavenie parametrov";
                    statusText = "Vlastná";
                    break;
                default:
                    description = "Neznáme nastavenie";
                    statusText = "Neznáma";
                    break;
            }

            speedLabel.Text = description;

            // Aktualizuj aj status label ak existuje
            if (statusLabel != null)
            {
                statusLabel.Text = $"Aktuálna rýchlosť: {statusText}";
            }

            // Aktualizuj indikátor farbu
            UpdateStatusIndicator();
        }

        private void UpdateStatusIndicator()
        {
            if (statusIndicator == null) return;

            switch (ExecutionSpeed)
            {
                case ExecutionSpeed.VerySlow:
                case ExecutionSpeed.Slow:
                    statusIndicator.Fill = new SolidColorBrush(Colors.Orange);
                    break;
                case ExecutionSpeed.Normal:
                    statusIndicator.Fill = new SolidColorBrush(Colors.Green);
                    break;
                case ExecutionSpeed.Fast:
                case ExecutionSpeed.VeryFast:
                    statusIndicator.Fill = new SolidColorBrush(Colors.Red);
                    break;
                case ExecutionSpeed.Custom:
                    statusIndicator.Fill = new SolidColorBrush(Colors.Blue);
                    break;
            }
        }

        private void ApplySpeedSettings(ExecutionSpeed speed)
        {
            if (executionManager == null) return;

            var settings = executionManager.GetSettings();

            switch (speed)
            {
                case ExecutionSpeed.VerySlow:
                    settings.DefaultDelayMs = 2000;
                    settings.MaxWaitForElementMs = 10000;
                    settings.MaxWaitForStateChangeMs = 8000;
                    break;
                case ExecutionSpeed.Slow:
                    settings.DefaultDelayMs = 1200;
                    settings.MaxWaitForElementMs = 7000;
                    settings.MaxWaitForStateChangeMs = 5000;
                    break;
                case ExecutionSpeed.Normal:
                    settings.DefaultDelayMs = 500;
                    settings.MaxWaitForElementMs = 5000;
                    settings.MaxWaitForStateChangeMs = 3000;
                    break;
                case ExecutionSpeed.Fast:
                    settings.DefaultDelayMs = 200;
                    settings.MaxWaitForElementMs = 3000;
                    settings.MaxWaitForStateChangeMs = 2000;
                    break;
                case ExecutionSpeed.VeryFast:
                    settings.DefaultDelayMs = 100;
                    settings.MaxWaitForElementMs = 2000;
                    settings.MaxWaitForStateChangeMs = 1000;
                    break;
                case ExecutionSpeed.Custom:
                    // Nastavenia sa berú z advanced controls
                    break;
            }

            executionManager.UpdateSettings(settings);
            ExecutionSpeedChanged?.Invoke(this, new ExecutionSpeedChangedEventArgs(speed, settings));
        }

        private void LoadCurrentSettings()
        {
            if (executionManager == null) return;

            var settings = executionManager.GetSettings();

            // Nastav UI elementy podľa aktuálnych nastavení
            if (defaultDelaySlider != null) defaultDelaySlider.Value = settings.DefaultDelayMs;
            if (elementWaitSlider != null) elementWaitSlider.Value = settings.MaxWaitForElementMs;
            if (stateWaitSlider != null) stateWaitSlider.Value = settings.MaxWaitForStateChangeMs;

            if (waitForCompletionCheckBox != null) waitForCompletionCheckBox.IsChecked = settings.WaitForPreviousCommandCompletion;
            if (adaptiveDelayCheckBox != null) adaptiveDelayCheckBox.IsChecked = settings.UseAdaptiveDelay;
            if (stateVerificationCheckBox != null) stateVerificationCheckBox.IsChecked = settings.EnableStateVerification;
        }

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Pre hlavný speed slider
            if (sender == speedSlider)
            {
                var speedValue = (int)Math.Round(e.NewValue);
                ExecutionSpeed = (ExecutionSpeed)speedValue;
            }
            // Pre pokročilé nastavenia - prepni na Custom ak sa mení
            else if (sender == defaultDelaySlider || sender == elementWaitSlider || sender == stateWaitSlider)
            {
                if (speedSlider != null && (int)speedSlider.Value != 5)
                {
                    speedSlider.Value = 5; // Custom
                }
                ExecutionSpeed = ExecutionSpeed.Custom;
                ApplyCustomSettings();
            }
        }

        private void ApplyCustomSettings()
        {
            if (executionManager == null) return;

            var settings = executionManager.GetSettings();

            if (defaultDelaySlider != null) settings.DefaultDelayMs = (int)defaultDelaySlider.Value;
            if (elementWaitSlider != null) settings.MaxWaitForElementMs = (int)elementWaitSlider.Value;
            if (stateWaitSlider != null) settings.MaxWaitForStateChangeMs = (int)stateWaitSlider.Value;

            if (waitForCompletionCheckBox != null) settings.WaitForPreviousCommandCompletion = waitForCompletionCheckBox.IsChecked == true;
            if (adaptiveDelayCheckBox != null) settings.UseAdaptiveDelay = adaptiveDelayCheckBox.IsChecked == true;
            if (stateVerificationCheckBox != null) settings.EnableStateVerification = stateVerificationCheckBox.IsChecked == true;

            executionManager.UpdateSettings(settings);
            ExecutionSpeedChanged?.Invoke(this, new ExecutionSpeedChangedEventArgs(ExecutionSpeed.Custom, settings));
        }

        private void AdvancedCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (ExecutionSpeed == ExecutionSpeed.Custom || sender != speedSlider)
            {
                ApplyCustomSettings();
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ExecutionSpeed = ExecutionSpeed.Normal;
            if (speedSlider != null) speedSlider.Value = 2;
            ApplySpeedSettings(ExecutionSpeed.Normal);
            LoadCurrentSettings();
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            TestCurrentSpeed();
        }

        private async void TestCurrentSpeed()
        {
            if (testButton != null) testButton.IsEnabled = false;
            if (testResultLabel != null)
            {
                testResultLabel.Content = "Testujem rýchlosť...";
                testResultLabel.Visibility = Visibility.Visible;
            }

            try
            {
                var startTime = DateTime.Now;
                var settings = executionManager?.GetSettings();

                if (settings != null)
                {
                    // Simuluje 3 príkazy s aktuálnymi nastaveniami
                    for (int i = 0; i < 3; i++)
                    {
                        await Task.Delay(settings.DefaultDelayMs);
                    }
                }

                var totalTime = DateTime.Now.Subtract(startTime);
                if (testResultLabel != null)
                {
                    testResultLabel.Content = $"Test dokončený za {totalTime.TotalSeconds:F1}s (3 príkazy)";
                    testResultLabel.Foreground = new SolidColorBrush(Colors.Green);
                }
            }
            catch (Exception ex)
            {
                if (testResultLabel != null)
                {
                    testResultLabel.Content = $"Test zlyhal: {ex.Message}";
                    testResultLabel.Foreground = new SolidColorBrush(Colors.Red);
                }
            }
            finally
            {
                if (testButton != null) testButton.IsEnabled = true;
            }
        }
    }

    public enum ExecutionSpeed
    {
        VerySlow = 0,
        Slow = 1,
        Normal = 2,
        Fast = 3,
        VeryFast = 4,
        Custom = 5
    }

    public class ExecutionSpeedChangedEventArgs : EventArgs
    {
        public ExecutionSpeed Speed { get; }
        public ExecutionSettings Settings { get; }

        public ExecutionSpeedChangedEventArgs(ExecutionSpeed speed, ExecutionSettings settings)
        {
            Speed = speed;
            Settings = settings;
        }
    }
}
