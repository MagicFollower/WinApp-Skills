using System.Windows;
using System.Windows.Controls;
using StartUI4Controls;

namespace PromptFavorites.Helpers
{
    public static class DialogHelper
    {
        public static string ShowInputDialog(UIElement owner, string title, string prompt, string defaultValue)
        {
            var win = new Window
            {
                Title = title,
                Width = 380,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
            };
            // 用资源引用而不是 FindResource 取一次：取一次的话，对话框开着时切档就定在旧档上
            win.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty,
                "UI4.Brush.Surface");

            if (owner != null)
            {
                var ownerWin = GetWindow(owner);
                if (ownerWin != null)
                    win.Owner = ownerWin;
            }

            var grid = new System.Windows.Controls.Grid();
            grid.Margin = new Thickness(20);

            var rd0 = new RowDefinition { Height = GridLength.Auto };
            var rd1 = new RowDefinition { Height = GridLength.Auto };
            var rd2 = new RowDefinition { Height = GridLength.Auto };
            grid.RowDefinitions.Add(rd0);
            grid.RowDefinitions.Add(rd1);
            grid.RowDefinitions.Add(rd2);

            var label = new System.Windows.Controls.TextBlock
            {
                Text = prompt,
                Margin = new Thickness(0, 0, 0, 8)
            };
            // 原生 TextBlock 的默认前景是黑：这行不接令牌，夜景档就是黑字压深底（原生控件的观感问题要修在宿主）
            label.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty,
                "UI4.Brush.Text");
            System.Windows.Controls.Grid.SetRow(label, 0);
            grid.Children.Add(label);

            var textBox = new UI4TextBox
            {
                Text = defaultValue ?? string.Empty,
                Height = 32,
                VerticalAlignment = VerticalAlignment.Center
            };
            textBox.SelectAll();
            System.Windows.Controls.Grid.SetRow(textBox, 1);
            grid.Children.Add(textBox);

            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };
            System.Windows.Controls.Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            string result = null;

            var okBtn = new UI4Button
            {
                Content = "\u786E\u5B9A",
                Width = 70,
                Height = 30,
                Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true
            };
            // 底色挂令牌资源引用而不是本地色：这个对话框是纯代码建的窗口，本地赋色会在切档时留在旧档上
            okBtn.SetResourceReference(UI4Button.GradientStartProperty, "UI4.Color.Accent");
            okBtn.SetResourceReference(UI4Button.GradientEndProperty, "UI4.Color.Accent");
            okBtn.Click += (s, e) =>
            {
                result = textBox.Text;
                win.DialogResult = true;
            };
            buttonPanel.Children.Add(okBtn);

            var cancelBtn = new UI4Button
            {
                Content = "\u53D6\u6D88",
                Width = 70,
                Height = 30,
                IsCancel = true
            };
            cancelBtn.SetResourceReference(UI4Button.GradientStartProperty, "UI4.Color.OffBackground");
            cancelBtn.SetResourceReference(UI4Button.GradientEndProperty, "UI4.Color.OffBackground");
            cancelBtn.Click += (s, e) =>
            {
                win.DialogResult = false;
            };
            buttonPanel.Children.Add(cancelBtn);

            win.Content = grid;
            win.Loaded += (s, e) =>
            {
                textBox.Focus();
                textBox.SelectAll();
            };

            var dialogResult = win.ShowDialog();
            return dialogResult == true ? result : null;
        }

        private static Window GetWindow(DependencyObject element)
        {
            var parent = LogicalTreeHelper.GetParent(element);
            while (parent != null)
            {
                var w = parent as Window;
                if (w != null) return w;
                parent = LogicalTreeHelper.GetParent(parent);
            }
            return null;
        }
    }
}
