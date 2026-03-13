using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace UUPDumpWPF.Converters
{
    public class ArchitectureColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string arch)
            {
                return arch.ToLower() switch
                {
                    "amd64" => new SolidColorBrush(Color.FromRgb(255, 68, 68)), // Red
                    "arm64" => new SolidColorBrush(Color.FromRgb(46, 204, 113)), // Green
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
