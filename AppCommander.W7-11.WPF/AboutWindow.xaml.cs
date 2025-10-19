using Castle.Components.DictionaryAdapter.Xml;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace AppCommander.W7_11.WPF
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        // URL pre Privacy Policy - zmeňte na váš skutočný link
        private const string PrivacyPolicyUrl = "https://your-website.com/privacy-policy";

        public AboutWindow()
        {
            InitializeComponent();
            LoadSystemInformation();
        }

        private void LoadSystemInformation()
        {
            try
            {
                // Get OS Version
                TxtOSVersion.Text = GetWindowsVersion();

                // Get .NET Version
                TxtDotNetVersion.Text = GetDotNetVersion();

                // Get Architecture
                TxtArchitecture.Text = Environment.Is64BitOperatingSystem ? "x64" : "x86";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading system information: {ex.Message}");
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occurred while trying to close the About window: " + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }

        /// <summary>
        /// Otvorí Privacy Policy link v predvolenom prehliadači
        /// </summary>
        private void LearnMorePrivacy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Otvorí predvolený webový prehliadač s URL
                //Process.Start(new ProcessStartInfo
                //{
                //    FileName = PrivacyPolicyUrl,
                //    UseShellExecute = true
                //});

                //PrivacyPolicyWindow - nová implementácia - namiesto WebBrowser otvorí vlastné okno PrivacyPolicyWindow v rámci aplikácie
        //        < Window x: Class = "AppCommander.W7_11.WPF.PrivacyPolicyWindow"
        //xmlns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        //xmlns: x = "http://schemas.microsoft.com/winfx/2006/xaml"
        //Title = "Privacy Policy - AppCommander"
        //Height = "700" Width = "850"
        //MinHeight = "600" MinWidth = "750"

                PrivacyPolicyWindow privacyWindow = new PrivacyPolicyWindow();
                privacyWindow.Owner = this; // Nastaví rodiča okna
                privacyWindow.ShowDialog(); // Zobrazí okno ako modálne



            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occurred while trying to open the privacy policy window: " + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);


                // Ak zlyhá otvorenie prehliadača, zobraz MessageBox s URL
                //MessageBox.Show(
                //    $"Unable to open the privacy policy page.\n\nPlease visit:\n{PrivacyPolicyUrl}",
                //    "Information",
                //    MessageBoxButton.OK,
                //    MessageBoxImage.Information);

                //System.Diagnostics.Debug.WriteLine($"Error opening privacy policy URL: {ex.Message}");
            }
        }
    }
}