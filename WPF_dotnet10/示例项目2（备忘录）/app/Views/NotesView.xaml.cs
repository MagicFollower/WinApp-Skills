using System;
using System.Windows.Controls;
using MemoTask.ViewModels;

namespace MemoTask.Views
{
    public partial class NotesView : UserControl
    {
        private NotesViewModel _bound;

        public NotesView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (_bound != null)
            {
                _bound.SearchFocusRequested -= OnFocusSearch;
                _bound.TitleFocusRequested -= OnFocusTitle;
            }

            _bound = DataContext as NotesViewModel;
            if (_bound != null)
            {
                _bound.SearchFocusRequested += OnFocusSearch;
                _bound.TitleFocusRequested += OnFocusTitle;
            }
        }

        // 导航页可能刚从可视树摘掉又挂回来，聚焦要排到布局之后，否则 Focus() 直接失败。
        private void OnFocusSearch(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input, null);
        }

        private void OnFocusTitle(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                TitleBox.Focus();
            }), System.Windows.Threading.DispatcherPriority.Input, null);
        }
    }
}
