using AppCommander.W7_11.WPF.Core;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace AppCommander.W7_11.WPF.Converters
{
    /// <summary>
    /// Konvertuje UserMode na Visibility
    /// Použitie v XAML: ConverterParameter="Developer,Tester" - Visible ak CurrentMode je Developer ALEBO Tester
    /// </summary>
    public class ModeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is UserMode currentMode))
                return Visibility.Collapsed;

            if (!(parameter is string allowedModes))
                return Visibility.Collapsed;

            // Parsuj povolené módy z parametra (napr. "Developer,Tester")
            var modes = allowedModes
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim())
                .ToList();

            // Skontroluj či aktuálny mód je v zozname
            foreach (var mode in modes)
            {
                if (string.Equals(mode, currentMode.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return Visibility.Visible;
                }
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Invertovaný ModeToVisibilityConverter
    /// Visible ak CurrentMode NIE JE v zozname
    /// </summary>
    public class ModeToVisibilityInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is UserMode currentMode))
                return Visibility.Visible;

            if (!(parameter is string excludedModes))
                return Visibility.Visible;

            var modes = excludedModes
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim())
                .ToList();

            foreach (var mode in modes)
            {
                if (string.Equals(mode, currentMode.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return Visibility.Collapsed;
                }
            }

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konvertuje UserMode na bool
    /// True ak CurrentMode je v zozname
    /// </summary>
    public class ModeToEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is UserMode currentMode))
                return false;

            if (!(parameter is string allowedModes))
                return false;

            var modes = allowedModes
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim())
                .ToList();

            foreach (var mode in modes)
            {
                if (string.Equals(mode, currentMode.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
