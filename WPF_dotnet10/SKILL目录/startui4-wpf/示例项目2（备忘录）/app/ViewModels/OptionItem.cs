namespace MemoTask.ViewModels
{
    /// <summary>下拉框的一项：Key 是落盘用的稳定契约，Label 只用于展示。</summary>
    internal sealed class OptionItem
    {
        public OptionItem(string key, string label)
        {
            Key = key;
            Label = label;
        }

        public string Key { get; private set; }

        public string Label { get; private set; }

        /// <summary>
        /// UI4ComboBox 闭合态不走 DisplayMemberPath（实测显示成类型名截断），
        /// 兜底就只剩 ToString()，所以这里必须返回 Label。
        /// </summary>
        public override string ToString()
        {
            return Label;
        }
    }
}
