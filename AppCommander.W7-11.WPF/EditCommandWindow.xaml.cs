using System;
using System.CodeDom;
using System.Windows;
using AppCommander.W7_11.WPF.Core;

namespace AppCommander.W7_11.WPF
{
    /// <summary>
    /// Interaction logic for EditCommandWindow.xaml
    /// </summary>
    public partial class EditCommandWindow : Window
    {
        public EditCommandWindow()
        {
            InitializeComponent();
        }

        public bool WasSaved { get; internal set; }

        private void ButtonSaveCommands_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Commands cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;  // Skončí BEZ nastavenia WasSaved = true
                }

                // ✅ OPRAVENÉ: WasSaved sa nastaví IBA ak validácia prešla
                WasSaved = true;
                this.DialogResult = true;  // Nastaví výsledok dialógu
                this.Close();  // Zatvorí okno
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving commands: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                // WasSaved zostane false pri chybe
            }
        }

        private void ButtonCancelEdit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (WasSaved)
                {
                    MessageBox.Show("You have already saved the commands. Close the window or cancel to discard changes.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // ✅ OPRAVENÉ: WasSaved sa nastaví na false IBA keď Cancel skutočne prebehne
                WasSaved = false;
                this.DialogResult = false;  // Nastaví výsledok dialógu
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error cancelling edit: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
