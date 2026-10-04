using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PromptFavorites.Models
{
    /// <summary>
    /// 模块条目。<see cref="Name"/> 与 <see cref="EntryCount"/> 会被就地改（改名、刷新计数），
    /// 所以这两个属性必须播报变化——否则 <c>RefreshModuleCounts</c> 写进对象但列表不重绘，
    /// 表现为"改了不生效"，而任何一次整表重建都会把它掩盖掉。
    /// </summary>
    public class PromptModule : INotifyPropertyChanged
    {
        private string _name;
        private int _entryCount;

        public string Name
        {
            get { return _name; }
            set { if (!Equals(_name, value)) { _name = value; OnPropertyChanged(); } }
        }

        public string FullPath { get; set; }

        public int EntryCount
        {
            get { return _entryCount; }
            set { if (_entryCount != value) { _entryCount = value; OnPropertyChanged(); } }
        }

        public System.DateTime CreatedAt { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
