using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StartUI4Controls.Internal
{
    /// <summary>把 <see cref="Color"/> 依赖属性实时转成 <see cref="SolidColorBrush"/>，供引用式模板绑定使用（避免每次主题切换重建 Style）。</summary>
    internal sealed class ColorToBrushConverter : IValueConverter
    {
        public static readonly ColorToBrushConverter Instance = new ColorToBrushConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color c)
            {
                var b = new SolidColorBrush(c);
                b.Freeze();
                return b;
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
