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

        // NOVÝ konštruktor - prijíma UnifiedItem
        public EditCommandWindow(UnifiedItem item) : this()
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            _editedItem = item;
            LoadItemData(item);
        }

        public bool WasSaved { get; internal set; }

        /// <summary>
        /// Načíta dáta z UnifiedItem do formulára
        /// </summary>
        private void LoadItemData(UnifiedItem item)
        {
            try
            {
                // Základné informácie
                txtStepNumber.Text = item.StepNumber.ToString();
                txtType.Text = item.TypeDisplay;
                txtName.Text = item.Name;
                txtAction.Text = item.Action;
                txtValue.Text = item.Value ?? "";

                // UI Element detaily
                if (item.ElementX.HasValue)
                    txtElementX.Text = item.ElementX.Value.ToString();
                else
                    txtElementX.Text = "-";

                if (item.ElementY.HasValue)
                    txtElementY.Text = item.ElementY.Value.ToString();
                else
                    txtElementY.Text = "-";

                txtElementId.Text = item.ElementId ?? "-";
                txtClassName.Text = item.ClassName ?? "-";

                // Execution details (ak existujú tieto polia v XAML)
                if (txtRepeatCount != null)
                    txtRepeatCount.Text = item.RepeatCount.ToString();

                if (txtStatus != null)
                    txtStatus.Text = item.Status;

                if (txtTimestamp != null)
                    txtTimestamp.Text = item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

                // Ak je to SequenceReference, zobraz FilePath
                if (item.Type == UnifiedItem.ItemType.SequenceReference && txtFilePath != null)
                {
                    txtFilePath.Text = item.FilePath ?? "-";
                    grpElementDetails.Visibility = Visibility.Collapsed; // Skry element details pre sekvencie
                }
                else
                {
                    if (txtFilePath != null)
                        txtFilePath.Text = "-";
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
        public void UpdateUnifiedItem(UnifiedItem item)
        {
            if (item == null || !WasSaved)
                return;

            try
            {
                // Aktualizuj iba editovateľné polia
                item.Name = txtName.Text;
                item.Value = txtValue.Text;

                // Ak existuje pole pre RepeatCount a je editovateľné
                if (txtRepeatCount != null && int.TryParse(txtRepeatCount.Text, out int repeatCount))
                {
                    item.RepeatCount = repeatCount;
                }

                item.Timestamp = DateTime.Now; // Aktualizuj timestamp
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error updating item: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ButtonSaveCommands_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validácia
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show(
                        "Name cannot be empty.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

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

        private void ButtonCancelEdit_Click(object sender, RoutedEventArgs e)
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
            return txtName.Text != _editedItem.Name ||
                   txtValue.Text != (_editedItem.Value ?? "");
        }
    }
}
