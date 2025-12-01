using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AppCommander.W7_11.WPF.Core.Managers
{
    /// <summary>
    /// Manažér pre ovládanie loop/repeat funkcionality
    /// - Infinite loop checkbox
    /// - Repeat count textbox (mouse wheel, keyboard, validation)
    /// </summary>
    public class LoopControlsManager
    {
        #region Fields

        private readonly TextBox _repeatCountTextBox;
        private readonly CheckBox _infiniteLoopCheckBox;
        private readonly Action<string> _updateStatus;

        private bool _isUpdatingRepeatCount = false; // Flag to prevent recursion

        // Konstants
        private const int MIN_REPEAT_COUNT = 1;
        private const int MAX_REPEAT_COUNT = 9999;
        private const int CTRL_STEP = 10;
        private const int SHIFT_STEP = 5;

        #endregion

        #region Constructor

        /// <summary>
        /// Konštruktor
        /// </summary>
        /// <param name="repeatCountTextBox">TextBox pre repeat count</param>
        /// <param name="infiniteLoopCheckBox">CheckBox pre infinite loop</param>
        /// <param name="updateStatus">Callback pre update statusu</param>
        public LoopControlsManager(
            TextBox repeatCountTextBox,
            CheckBox infiniteLoopCheckBox,
            Action<string> updateStatus)
        {
            _repeatCountTextBox = repeatCountTextBox ?? throw new ArgumentNullException(nameof(repeatCountTextBox));
            _infiniteLoopCheckBox = infiniteLoopCheckBox ?? throw new ArgumentNullException(nameof(infiniteLoopCheckBox));
            _updateStatus = updateStatus ?? throw new ArgumentNullException(nameof(updateStatus));
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Vráti aktuálny repeat count (0 pre infinite)
        /// </summary>
        public int RepeatCount
        {
            get
            {
                if (IsInfiniteLoop)
                    return 0; // 0 = infinite

                if (int.TryParse(_repeatCountTextBox.Text, out int value))
                    return Math.Max(MIN_REPEAT_COUNT, Math.Min(MAX_REPEAT_COUNT, value));

                return MIN_REPEAT_COUNT;
            }
            set
            {
                if (value <= 0)
                {
                    // Set infinite loop
                    _infiniteLoopCheckBox.IsChecked = true;
                }
                else
                {
                    _infiniteLoopCheckBox.IsChecked = false;
                    _repeatCountTextBox.Text = Math.Max(MIN_REPEAT_COUNT, Math.Min(MAX_REPEAT_COUNT, value)).ToString();
                }
            }
        }

        /// <summary>
        /// Či je zapnutý infinite loop
        /// </summary>
        public bool IsInfiniteLoop => _infiniteLoopCheckBox?.IsChecked == true;

        #endregion

        #region Infinite Loop Handlers

        /// <summary>
        /// Handler pre zaškrtnutie infinite loop
        /// </summary>
        public void InfiniteLoop_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isUpdatingRepeatCount) return;

                _isUpdatingRepeatCount = true;

                _repeatCountTextBox.IsEnabled = false;
                _repeatCountTextBox.Text = "∞";
                _updateStatus("Infinite loop enabled");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error enabling infinite loop: {ex.Message}");
            }
            finally
            {
                _isUpdatingRepeatCount = false;
            }
        }

        /// <summary>
        /// Handler pre odškrtnutie infinite loop
        /// </summary>
        public void InfiniteLoop_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isUpdatingRepeatCount) return;

                _isUpdatingRepeatCount = true;

                _repeatCountTextBox.IsEnabled = true;
                _repeatCountTextBox.Text = "1";
                _updateStatus("Infinite loop disabled");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error disabling infinite loop: {ex.Message}");
            }
            finally
            {
                _isUpdatingRepeatCount = false;
            }
        }

        #endregion

        #region Repeat Count Handlers

        /// <summary>
        /// Handler pre Enter v repeat count textboxe
        /// </summary>
        public void RepeatCount_KeyDown(object sender, KeyEventArgs e, Action onEnterPressed)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                onEnterPressed?.Invoke();
            }
        }

        /// <summary>
        /// Handler pre zmenu textu v repeat count
        /// </summary>
        public void RepeatCount_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingRepeatCount) return;
                // Nerobíme nič špeciálne - len predchádzame rekurzii
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in RepeatCount_TextChanged: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler pre koliesko myši na repeat count textboxe
        /// </summary>
        public void RepeatCount_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                // Kontrola či je infinite loop zapnutý
                if (IsInfiniteLoop)
                {
                    e.Handled = true;
                    return;
                }

                var textBox = sender as TextBox;
                if (textBox == null || !textBox.IsEnabled)
                {
                    e.Handled = true;
                    return;
                }

                int currentValue = GetCurrentValue(textBox);
                int delta = CalculateDelta(e.Delta > 0);
                int newValue = ClampValue(currentValue + delta);

                textBox.Text = newValue.ToString();
                e.Handled = true;

                _updateStatus($"Repeat count changed to {newValue}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling mouse wheel: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler pre klávesy šípok na repeat count textboxe
        /// </summary>
        public void RepeatCount_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                // Kontrola či je infinite loop zapnutý
                if (IsInfiniteLoop)
                {
                    if (e.Key == Key.Up || e.Key == Key.Down)
                    {
                        e.Handled = true;
                    }
                    return;
                }

                var textBox = sender as TextBox;
                if (textBox == null || !textBox.IsEnabled)
                {
                    return;
                }

                // Spracovanie šípok hore/dole
                if (e.Key == Key.Up || e.Key == Key.Down)
                {
                    int currentValue = GetCurrentValue(textBox);
                    int delta = CalculateDelta(e.Key == Key.Up);
                    int newValue = ClampValue(currentValue + delta);

                    textBox.Text = newValue.ToString();
                    textBox.SelectAll();

                    e.Handled = true;
                    _updateStatus($"Repeat count changed to {newValue}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling key press: {ex.Message}");
            }
        }

        /// <summary>
        /// Validácia vstupu - povoliť len číslice
        /// </summary>
        public void RepeatCount_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            try
            {
                e.Handled = !IsTextNumeric(e.Text);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error validating input: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler pre stratenie fokusu - validácia hodnoty
        /// </summary>
        public void RepeatCount_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                var textBox = sender as TextBox;
                if (textBox == null) return;

                int value;
                if (!int.TryParse(textBox.Text, out value) || value < MIN_REPEAT_COUNT)
                {
                    textBox.Text = MIN_REPEAT_COUNT.ToString();
                    _updateStatus($"Repeat count reset to {MIN_REPEAT_COUNT}");
                }
                else if (value > MAX_REPEAT_COUNT)
                {
                    textBox.Text = MAX_REPEAT_COUNT.ToString();
                    _updateStatus($"Repeat count limited to {MAX_REPEAT_COUNT}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error validating repeat count: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Získa aktuálnu hodnotu z textboxu
        /// </summary>
        private int GetCurrentValue(TextBox textBox)
        {
            if (int.TryParse(textBox.Text, out int value))
                return value;
            return MIN_REPEAT_COUNT;
        }

        /// <summary>
        /// Vypočíta delta hodnotu na základe modifikátorov
        /// </summary>
        private int CalculateDelta(bool isIncrement)
        {
            int delta = isIncrement ? 1 : -1;

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                delta *= CTRL_STEP;
            }
            else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                delta *= SHIFT_STEP;
            }

            return delta;
        }

        /// <summary>
        /// Obmedzí hodnotu na povolený rozsah
        /// </summary>
        private int ClampValue(int value)
        {
            return Math.Max(MIN_REPEAT_COUNT, Math.Min(MAX_REPEAT_COUNT, value));
        }

        /// <summary>
        /// Kontrola či text obsahuje len číslice
        /// </summary>
        private bool IsTextNumeric(string text)
        {
            return Regex.IsMatch(text, "^[0-9]+$");
        }

        #endregion

        #region Attach Event Handlers

        /// <summary>
        /// Pripojí všetky event handlery na UI controls
        /// Volať po InitializeComponent()
        /// </summary>
        /// <param name="onEnterPressed">Akcia ktorá sa vykoná pri stlačení Enter (napr. spustenie playback)</param>
        public void AttachEventHandlers(Action onEnterPressed = null)
        {
            // Infinite loop checkbox
            _infiniteLoopCheckBox.Checked += InfiniteLoop_Checked;
            _infiniteLoopCheckBox.Unchecked += InfiniteLoop_Unchecked;

            // Repeat count textbox
            _repeatCountTextBox.PreviewMouseWheel += RepeatCount_PreviewMouseWheel;
            _repeatCountTextBox.PreviewKeyDown += (s, e) => RepeatCount_PreviewKeyDown(s, e);
            _repeatCountTextBox.PreviewTextInput += RepeatCount_PreviewTextInput;
            _repeatCountTextBox.LostFocus += RepeatCount_LostFocus;
            _repeatCountTextBox.TextChanged += RepeatCount_TextChanged;

            // Enter key handler
            if (onEnterPressed != null)
            {
                _repeatCountTextBox.KeyDown += (s, e) => RepeatCount_KeyDown(s, e, onEnterPressed);
            }
        }

        /// <summary>
        /// Odpojí všetky event handlery
        /// Volať pri zatváraní okna
        /// </summary>
        public void DetachEventHandlers()
        {
            _infiniteLoopCheckBox.Checked -= InfiniteLoop_Checked;
            _infiniteLoopCheckBox.Unchecked -= InfiniteLoop_Unchecked;

            // Pre ostatné handlery by sme potrebovali uložiť referencie
            // alebo použiť slabé referencie - pre jednoduchosť to nechávame
        }

        #endregion

        #region Reset

        /// <summary>
        /// Resetuje loop controls na predvolené hodnoty
        /// </summary>
        public void Reset()
        {
            _isUpdatingRepeatCount = true;
            try
            {
                _infiniteLoopCheckBox.IsChecked = false;
                _repeatCountTextBox.IsEnabled = true;
                _repeatCountTextBox.Text = "1";
            }
            finally
            {
                _isUpdatingRepeatCount = false;
            }
        }

        #endregion
    }
}
