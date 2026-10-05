using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VocabDesk.Converters
{
    /// <summary>bool → Visibility，用绑定控制浮层与分区，省掉 code-behind 里的 if 显示/隐藏。</summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool on = value is bool && (bool)value;
            bool invert = string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase);
            if (invert) on = !on;
            return on ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// 设计尺寸 × 缩放系数，上限钳到工作区。放大档下窗口下限必须跟着长，否则"已经是最小尺寸"的窗口
    /// 装不下按缩放后变宽的内容；上限取工作区是为了别让下限超过屏幕（窗口边看不见就没法拖回去）。
    /// 绑定值是 ZoomFactor，ConverterParameter 形如 W:900 / H:500。
    /// </summary>
    public class ZoomedSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var spec = parameter as string;
            // 参数写错必须回 UnsetValue（退回绑定源即"没有下限"），静默给个 0 等于把窗口下限变成 0
            if (string.IsNullOrEmpty(spec) || spec.Length < 3 || spec[1] != ':')
                return DependencyProperty.UnsetValue;

            double design;
            if (!double.TryParse(spec.Substring(2), NumberStyles.Number, CultureInfo.InvariantCulture, out design))
                return DependencyProperty.UnsetValue;

            double zoom = value is double && (double)value > 0 ? (double)value : 1.0;
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
}
