using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace AppCommander.W7_11.WPF
{
    /// <summary>
    /// Interaction logic for UserGuideWindow.xaml
    /// </summary>
    public partial class UserGuideWindow : Window
    {
        public UserGuideWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occurred while trying to close the User Guide window: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetWindowsVersion()
        {
            try
            {
                var version = Environment.OSVersion.Version;

                if (version.Major == 10)
                {
                    if (version.Build >= 22000)
                    {
                        return $"Windows 11 (Build {version.Build})";
                    }
                    return $"Windows 10 (Build {version.Build})";
                }

                if (version.Major == 6)
                {
                    if (version.Minor == 3) return "Windows 8.1";
                    if (version.Minor == 2) return "Windows 8";
                    if (version.Minor == 1) return "Windows 7";
                    if (version.Minor == 0) return "Windows Vista";
                }

                return $"Windows {version.Major}.{version.Minor} (Build {version.Build})";
            }
            catch
            {
                return "Unknown Windows Version";
            }
        }

        private string GetDotNetVersion()
        {
            try
            {
                // Get the .NET Framework version
                var frameworkVersion = Environment.Version;
                return $".NET Framework {frameworkVersion.Major}.{frameworkVersion.Minor}.{frameworkVersion.Build}";
            }
            catch
            {
                return ".NET Framework (Unknown Version)";
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }
    }
}
