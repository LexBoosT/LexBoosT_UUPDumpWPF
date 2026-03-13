using System;
using System.Globalization;
using System.Windows.Data;

namespace UUPDumpWPF.Converters
{
    public class ArchitectureTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string arch)
            {
                return $"[{arch.ToUpper()}]";
            }
            return "[UNKNOWN]";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
