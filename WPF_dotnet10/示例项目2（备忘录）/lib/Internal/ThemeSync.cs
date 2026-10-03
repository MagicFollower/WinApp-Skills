using System.Collections.Generic;
using System.Windows;

namespace StartUI4Controls.Internal
{
    /// <summary>
    /// 让依赖属性“跟随主题、但尊重用户显式改动”。
    /// 记录本控件上一次由主题写入的值：仅当属性当前值仍等于上次写入值（或从未写入）时才应用新的主题值；
    /// 一旦用户把它改成别的值，即停止跟随。避免用 <c>ReadLocalValue == UnsetValue</c> 判断时，
    /// 主题同步自身写入的本地值在下次切换时被误判为“用户已设置”，从而冻结在构造时的主题。
    /// </summary>
    internal static class ThemeSync
    {
        public static void Apply(
            DependencyObject obj,
            DependencyProperty dp,
            Dictionary<DependencyProperty, object> applied,
            object themeValue)
        {
            object last;
            bool had = applied.TryGetValue(dp, out last);
            object current = obj.GetValue(dp);
            if (!had || Equals(current, last))
            {
                applied[dp] = themeValue;
                obj.SetValue(dp, themeValue);
            }
        }
    }
}
