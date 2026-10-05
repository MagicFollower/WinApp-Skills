using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace VocabDesk.Helpers
{
    /// <summary>
    /// 字号 / 字体族 / 缩放的单源：默认值、允许区间、以及"基准 → 各层级"的阶梯换算都在这里。
    ///
    /// 为什么不把所有字号都绑成同一个基准：界面里的字号是层级关系（胶囊、标签、次要、正文、图标）。
    /// 全拉平成一档会把层级抹掉。所以用户调的是基准，其余层级按固定差值跟随，并各自有下限防止缩到不可读。
    ///
    /// 库那边只有 UI4.Font.Size.Base / Code / Family 三个键（见 lib/README.md §4.2.1），
    /// 层级键 App.Font.Size.* 与固定件尺寸键 App.Size.* / App.Radius.* 是宿主自己发布在同一处的扩展。
    /// </summary>
    public static class Typography
    {
        /// <summary>正文基准默认值，与库里的 UI4Theme.DefaultFontSizeBase 同值。</summary>
        public const double DefaultBaseSize = 15;

        /// <summary>下限 12：再小中文正文就不可读了（库里历史默认 15，WPF 裸默认 12）。</summary>
        public const double MinBaseSize = 12;
        public const double MaxBaseSize = 28;

        public const double DefaultZoomPercent = 100;
        public const double MinZoomPercent = 50;
        public const double MaxZoomPercent = 200;

        /// <summary>出厂字体族：带中文回退。不要在别处再写一遍这个串。</summary>
        public const string DefaultFontFamilySource = "Segoe UI, Microsoft YaHei UI, sans-serif";

        public static double ClampBase(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return DefaultBaseSize;
            return Math.Max(MinBaseSize, Math.Min(MaxBaseSize, value));
        }

        public static double ClampZoom(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return DefaultZoomPercent;
            return Math.Max(MinZoomPercent, Math.Min(MaxZoomPercent, value));
        }

        /// <summary>某个层级在给定基准下的字号（带下限）。写错的层级名退回基准本身而不是崩。</summary>
        public static double SizeOf(string role, double baseSize)
        {
            switch (role)
            {
                case "Caption": return Math.Max(10, baseSize - 4);
                case "Small": return Math.Max(11, baseSize - 3);
                case "Medium": return Math.Max(12, baseSize - 2);
                case "Lead": return Math.Max(12, baseSize - 1);
                case "Icon": return Math.Max(13, baseSize + 1);
                case "Code": return Math.Max(12, baseSize - 1);
                default: return baseSize;
            }
        }

        /// <summary>圆形图标钮的默认直径，也是胶囊高度的默认值来源。</summary>
        public const double DefaultRoundButtonSize = 28;
        public const double DefaultChipHeight = 24;

        /// <summary>
        /// 圆形图标钮（「+」这类）的直径：按图标字号放大到能装下字身为止。
        /// 为什么不能只钉死 28×28——UI4Button 的默认 Padding 是 10,0,10,0，内容区只剩 8 px 宽，
        /// 字号一大就被 arrange 成 8 px、文字从左上角起画，看起来是"内容偏到右下"（不是没居中）。
        /// 基准 15 时这里恰好还是 28，所以默认观感不变。
        /// </summary>
        public static double RoundButtonSize(double baseSize)
        {
            return Math.Max(DefaultRoundButtonSize, Math.Round(SizeOf("Icon", baseSize) * 1.5));
        }

        /// <summary>胶囊 / 小按钮的高度，口径同上：基准 15 时仍是原来的 24。</summary>
        public static double ChipHeight(double baseSize)
        {
            return Math.Max(DefaultChipHeight, Math.Round(SizeOf("Caption", baseSize) * 1.5));
        }

        /// <summary>把字体族、字号阶梯与"随字号长高的固定件尺寸"发布到应用资源根。</summary>
        public static void Publish(Application app, string familySource, double baseSize)
        {
            if (app == null) return;

            var res = app.Resources;
            res["UI4.Font.Family"] = SafeFamily(familySource);
            res["UI4.Font.Size.Base"] = baseSize;
            res["UI4.Font.Size.Code"] = SizeOf("Code", baseSize);
            res["App.Font.Size.Caption"] = SizeOf("Caption", baseSize);
            res["App.Font.Size.Small"] = SizeOf("Small", baseSize);
            res["App.Font.Size.Medium"] = SizeOf("Medium", baseSize);
            res["App.Font.Size.Lead"] = SizeOf("Lead", baseSize);
            res["App.Font.Size.Icon"] = SizeOf("Icon", baseSize);

            // 圆角半径要的是 CornerRadius 而不是 double：DynamicResource 不做类型转换，
            // 往 CornerRadius 依赖属性上挂一个 double 资源会在运行期炸。
            double round = RoundButtonSize(baseSize);
            double chip = ChipHeight(baseSize);
            res["App.Size.RoundButton"] = round;
            res["App.Radius.RoundButton"] = new CornerRadius(round / 2);
            res["App.Size.Chip"] = chip;
            res["App.Radius.Chip"] = new CornerRadius(chip / 2);
        }

        /// <summary>设置里存的字体名可能被手改或系统里已不存在，构造失败就回退出厂族。</summary>
        public static FontFamily SafeFamily(string familySource)
        {
            if (string.IsNullOrWhiteSpace(familySource))
                return new FontFamily(DefaultFontFamilySource);

            try
            {
                return new FontFamily(familySource);
            }
            catch (Exception)
            {
                return new FontFamily(DefaultFontFamilySource);
            }
        }

        /// <summary>
        /// 字体下拉的候选表：出厂字体栈固定排在第 0 位，其余系统字体族按名排序。
        /// 出厂项必须在列表里是结构性的而非巧合——WPF 的 ComboBox 挂了 ItemsSource 之后，
        /// 把 SelectedItem 设成列表外的值会被静默清空，"恢复默认后下拉框不跟着变"就是这么来的。
        /// </summary>
        public static List<string> BuildFamilyChoices(IEnumerable<string> systemFamilySources)
        {
            var choices = new List<string> { DefaultFontFamilySource };
            if (systemFamilySources == null) return choices;

            var seen = new HashSet<string>(StringComparer.Ordinal) { DefaultFontFamilySource };

            foreach (var source in systemFamilySources)
            {
                if (string.IsNullOrWhiteSpace(source)) continue;
                if (seen.Add(source)) choices.Add(source);
            }

            // 出厂项固定在第 0 位，其余部分排序（不能对整表排序，那样它会跑到中间去）
            choices.Sort(1, choices.Count - 1, StringComparer.Ordinal);
            return choices;
        }
    }
}
