using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MemoTask.Helpers;
using MemoTask.ViewModels;
using StartUI4Controls;

namespace MemoTask.Views
{
    public partial class SettingsView : UserControl
    {
        private readonly Dictionary<string, UI4Button> _chips =
            new Dictionary<string, UI4Button>(StringComparer.OrdinalIgnoreCase);

        public SettingsView()
        {
            InitializeComponent();
            DataContextChanged += OnViewModelChanged;
        }

        private void OnViewModelChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SettingsViewModel old = e.OldValue as SettingsViewModel;
            if (old != null) old.PropertyChanged -= OnSettingsPropertyChanged;

            SettingsViewModel vm = e.NewValue as SettingsViewModel;
            if (vm == null) return;

            BuildPalette(vm);
            vm.PropertyChanged += OnSettingsPropertyChanged;
            MarkSelected(vm.AccentHex);
        }

        private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "AccentHex") MarkSelected(((SettingsViewModel)sender).AccentHex);
        }

        private void BuildPalette(SettingsViewModel vm)
        {
            _chips.Clear();
            AccentPalette.Children.Clear();

            foreach (AccentSwatch swatch in vm.AccentSwatches)
            {
                Color color = swatch.Color;
                SolidColorBrush glyph = new SolidColorBrush(Theme.OnAccentFor(color));
                glyph.Freeze();

                UI4Button chip = new UI4Button
                {
                    Width = 44,
                    Height = 30,
                    Margin = new Thickness(0, 0, 8, 0),
                    CornerRadius = new CornerRadius(6),
                    FontSize = 13d,
                    GradientStart = color,
                    GradientEnd = color,
                    HoverBackground = new SolidColorBrush(Theme.Mix(color, Colors.Black, 0.15f)),
                    Foreground = glyph,
                    HoverForeground = glyph,
                    Command = vm.SetAccentCommand,
                    CommandParameter = swatch.Key,
                };
                // 选中描边要跟正文色走，所以这两处保留资源引用；本地值只在 DataTemplate 里会被顶掉。
                chip.SetResourceReference(BorderBrushProperty, "UI4.Brush.Text");
                chip.SetResourceReference(UI4Button.HoverBorderBrushProperty, "UI4.Brush.Text");

                _chips[swatch.Key] = chip;
                AccentPalette.Children.Add(chip);
            }
        }

        private void MarkSelected(string accentHex)
        {
            foreach (KeyValuePair<string, UI4Button> pair in _chips)
            {
                bool on = !string.IsNullOrEmpty(accentHex)
                          && string.Equals(pair.Key, accentHex, StringComparison.OrdinalIgnoreCase);
                pair.Value.Content = on ? "✓" : null;
                pair.Value.BorderThickness = new Thickness(on ? 2 : 0);
            }
        }
    }
}
