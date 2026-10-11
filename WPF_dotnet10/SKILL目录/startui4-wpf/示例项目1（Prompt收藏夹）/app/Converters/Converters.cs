using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PromptFavorites.Converters
{
    public class BoolToStarConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? "\u2605" : "\u2606";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b ? !b : value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b ? !b : value;
        }
    }

    public class UseCountDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
                return count + " \u6B21";
            return "0 \u6B21";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class DateTimeFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dt)
                return dt.ToString("yyyy-MM-dd HH:mm");
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// 设计尺寸 × 缩放系数，上限钳到工作区。放大档下窗口下限必须跟着长，否则"已经是最小尺寸"的窗口
    /// 装不下按缩放后变宽的三栏；上限取工作区是为了别让下限超过屏幕（窗口边看不见就没法拖回去）。
    /// 绑定值是 ZoomFactor，<c>ConverterParameter</c> 形如 <c>W:900</c> / <c>H:500</c>。
    /// </summary>
    public class ZoomedSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var spec = parameter as string;
            if (string.IsNullOrEmpty(spec) || spec.Length < 3 || spec[1] != ':')
                return DependencyProperty.UnsetValue;

            double design;
            if (!double.TryParse(spec.Substring(2), NumberStyles.Number, CultureInfo.InvariantCulture, out design))
                return DependencyProperty.UnsetValue;

            double zoom = value is double z && z > 0 ? z : 1.0;
            bool height = spec[0] == 'H' || spec[0] == 'h';
            var workArea = SystemParameters.WorkArea;
            double cap = height ? workArea.Height : workArea.Width;

            return Math.Min(design * zoom, cap);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class SortModeMatchConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is Models.SortMode current
                && values[1] is Models.SortMode target)
            {
                return current == target;
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
