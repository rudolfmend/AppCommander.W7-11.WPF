using System;
using System.Windows;
using AppCommander.W7_11.WPF.Core;

namespace AppCommander.W7_11.WPF
{
    /// <summary>
    /// Interaction logic for EditCommandWindow.xaml
    /// </summary>
    public partial class EditCommandWindow : Window
    {
        private UnifiedItem _editedItem;

        // Prázdny konštruktor (pre XAML designer)
        public EditCommandWindow()
        {
            InitializeComponent();
            WasSaved = false;
        }

        // konštruktor - prijíma UnifiedItem
        public EditCommandWindow(UnifiedItem item) : this()
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            _editedItem = item;
            LoadItemData(item);
        }

        public bool WasSaved { get; internal set; }

        // property to expose the edited item
        public UnifiedItem EditedItem
        {
            get { return _editedItem; }
        }

        /// <summary>
        /// Načíta dáta z UnifiedItem do formulára
        /// </summary>
        private void LoadItemData(UnifiedItem item)
        {
            try
            {
                // Základné informácie
                AppCommander_TxtStepNumber.Text = item.StepNumber.ToString();
                AppCommander_TxtType.Text = item.TypeDisplay;
                AppCommander_TxtName.Text = item.Name;
                AppCommander_TxtAction.Text = item.Action;
                AppCommander_TxtValue.Text = item.Value ?? "";

                // UI Element detaily
                if (item.ElementX.HasValue)
                    AppCommander_TxtElementX.Text = item.ElementX.Value.ToString();
                else
                    AppCommander_TxtElementX.Text = "-";

                if (item.ElementY.HasValue)
                    AppCommander_TxtElementY.Text = item.ElementY.Value.ToString();
                else
                    AppCommander_TxtElementY.Text = "-";

                AppCommander_TxtElementId.Text = item.ElementId ?? "-";
                AppCommander_TxtClassName.Text = item.ClassName ?? "-";

                // Execution details (ak existujú tieto polia v XAML)
                if (AppCommander_TxtRepeatCount != null)
                    AppCommander_TxtRepeatCount.Text = item.RepeatCount.ToString();

                if (AppCommander_TxtStatus != null)
                    AppCommander_TxtStatus.Text = item.Status;

                if (AppCommander_TxtTimestamp != null)
                    AppCommander_TxtTimestamp.Text = item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

                // Ak je to SequenceReference, zobraz FilePath
                if (item.Type == UnifiedItem.ItemType.SequenceReference && AppCommander_TxtFilePath != null)
                {
                    AppCommander_TxtFilePath.Text = item.FilePath ?? "-";
                    AppCommander_GrpElementDetails.Visibility = Visibility.Collapsed; // Skry element details pre sekvencie
                }
                else
                {
                    if (AppCommander_TxtFilePath != null)
                        AppCommander_TxtFilePath.Text = "-";
                }

                // Nastav titulok okna
                this.Title = $"Edit: {item.Name}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading item data: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Aktualizuje UnifiedItem s editovanými údajmi
        /// </summary>
        //public void UpdateUnifiedItem(UnifiedItem item)
        //{
        //    if (item == null || !WasSaved)
        //        return;

        //    try
        //    {
        //        // Aktualizuj iba editovateľné polia
        //        item.Name = AppCommander_TxtName.Text;
        //        item.Value = AppCommander_TxtValue.Text;

        //        // Ak existuje pole pre RepeatCount a je editovateľné
        //        if (AppCommander_TxtRepeatCount != null && int.TryParse(AppCommander_TxtRepeatCount.Text, out int repeatCount))
        //        {
        //            item.RepeatCount = repeatCount;
        //        }

        //        item.Timestamp = DateTime.Now; // Aktualizuj timestamp
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show(
        //            $"Error updating item: {ex.Message}",
        //            "Error",
        //            MessageBoxButton.OK,
        //            MessageBoxImage.Error);
        //    }
        //}

        private void AppCommander_ButtonSaveCommands_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validácia
                if (string.IsNullOrWhiteSpace(AppCommander_TxtName.Text))
                {
                    MessageBox.Show(
                        "Name cannot be empty.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Aktualizuj _editedItem s novými hodnotami
                _editedItem.Name = AppCommander_TxtName.Text;
                _editedItem.Value = AppCommander_TxtValue.Text;

                // Ak existuje pole pre RepeatCount a je editovateľné
                if (AppCommander_TxtRepeatCount != null && int.TryParse(AppCommander_TxtRepeatCount.Text, out int repeatCount))
                {
                    _editedItem.RepeatCount = repeatCount;
                }

                _editedItem.Timestamp = DateTime.Now;

                // Označíme že boli zmeny uložené
                WasSaved = true;
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error saving changes: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void AppCommander_ButtonCancelEdit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Potvrdenie ak boli vykonané zmeny
                if (IsDataModified())
                {
                    var result = MessageBox.Show(
                        "You have unsaved changes. Are you sure you want to cancel?",
                        "Unsaved Changes",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.No)
                        return;
                }

                WasSaved = false;
                this.DialogResult = false;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error canceling edit: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Skontroluje či boli vykonané zmeny v dátach
        /// </summary>
        private bool IsDataModified()
        {
            if (_editedItem == null)
                return false;

            // Jednoduchá kontrola či sa zmenili hlavné polia
            return AppCommander_TxtName.Text != _editedItem.Name ||
                   AppCommander_TxtValue.Text != (_editedItem.Value ?? "");
        }
    }
}
