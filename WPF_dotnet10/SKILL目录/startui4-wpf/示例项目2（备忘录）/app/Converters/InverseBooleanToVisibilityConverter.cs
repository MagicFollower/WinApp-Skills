using System;
using System.Windows;
using System.Windows.Data;

namespace MemoTask.Converters
{
    /// <summary>false / null → Collapsed。配合 WPF 自带的 BooleanToVisibilityConverter 用。</summary>
    public sealed class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool b = value is bool && (bool)value;
            return b ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is Visibility && (Visibility)value == Visibility.Collapsed;
        }
    }
}
