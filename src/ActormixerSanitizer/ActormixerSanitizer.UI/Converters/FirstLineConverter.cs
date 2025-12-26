using System;
using System.Globalization;
using System.Windows.Data;

namespace ActormixerSanitizer.UI.Converters
{
    public class FirstLineConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text && !string.IsNullOrEmpty(text))
            {
                int nextLine = text.IndexOf('\n');
                return nextLine == -1 ? text : text.Substring(0, nextLine);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
