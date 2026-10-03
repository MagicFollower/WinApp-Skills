using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MemoTask.ViewModels;

namespace MemoTask.Views
{
    public partial class TodosView : UserControl
    {
        public TodosView()
        {
            InitializeComponent();
            AddBox.PreviewKeyDown += OnAddKeyDown;
        }

        private void OnAddKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Return) return;
            if (Keyboard.Modifiers != ModifierKeys.None) return;
            TodosViewModel vm = DataContext as TodosViewModel;
            if (vm == null) return;
            vm.AddCommand.Execute(null);
            AddBox.Focus();
            e.Handled = true;
        }
    }
}
