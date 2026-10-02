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
                ShowInTaskbar = false,
                Background = (System.Windows.Media.Brush)Application.Current.FindResource("UI4.Brush.Surface")
                    ?? System.Windows.Media.Brushes.White
            };

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
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("UI4.Brush.Text")
                    ?? System.Windows.Media.Brushes.Black
            };
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
                IsDefault = true,
                GradientStart = Theme.Accent,
                GradientEnd = Theme.Accent,
                HoverBackground = Theme.AccentHoverBrush
            };
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
                IsCancel = true,
                GradientStart = Theme.Neutral,
                GradientEnd = Theme.Neutral,
                HoverBackground = Theme.NeutralHoverBrush
            };
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
