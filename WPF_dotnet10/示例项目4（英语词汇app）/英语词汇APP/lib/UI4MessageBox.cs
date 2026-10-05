using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using StartUI4Controls.Internal;

namespace StartUI4Controls
{
    /// <summary>
    /// 消息框按钮模式枚举。
    /// </summary>
    public enum UI4MessageBoxButtons
    {
        OK,
        OKCancel
    }

    /// <summary>
    /// 现代风格的消息框窗口，支持动画效果和自定义按钮。
    /// </summary>
    /// <remarks>
    /// <para>继承自 <see cref="System.Windows.Window"/>，提供现代化的消息提示界面，
    /// 支持淡入淡出动画和圆角边框。容器与文字颜色经资源引用跟随主题切换。</para>
    /// </remarks>
    public class UI4MessageBox : Window
    {
        private const double DefaultWidth = 460;
        private const double DefaultMinHeight = 160;
        private const double DefaultMaxHeight = 400;

        private static readonly FontFamily WindowFontFamily =
            new FontFamily("Segoe UI Variable Display, Segoe UI, sans-serif");
        private static readonly FontFamily IconFontFamily =
            new FontFamily("Segoe MDL2 Assets");
        private static readonly DropShadowEffect ContainerShadow;

        private bool _isClosingAnimating;
        private readonly UI4MessageBoxButtons _buttonMode;
        private TextBlock _iconText;
        private TextBlock _headingText;
        private TextBlock _messageText;
        private Border _mainContainer;

        static UI4MessageBox()
        {
            ContainerShadow = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 18,
                ShadowDepth = 6,
                Opacity = 0.3
            };
            ContainerShadow.Freeze();
        }

        /// <summary>
        /// 初始化消息框实例，构建完整视觉树。
        /// </summary>
        public UI4MessageBox(string title, string content, UI4MessageBoxButtons buttonMode = UI4MessageBoxButtons.OK)
        {
            _buttonMode = buttonMode;
            ConfigureWindowProperties(title);
            BuildMainContainer();
            BuildContentLayout(title, content);
            AttachDragAndResize();
            Loaded += Window_LoadedAnim;

            // 声明式跟随主题：容器底与三处文字都挂令牌，无需命令式刷新
            _mainContainer.SetResourceReference(Border.BackgroundProperty, "UI4.Brush.Surface");
            _headingText.SetResourceReference(TextBlock.ForegroundProperty, "UI4.Brush.TextForeground");
            _messageText.SetResourceReference(TextBlock.ForegroundProperty, "UI4.Brush.TextForeground");
            _iconText.SetResourceReference(TextBlock.ForegroundProperty, "UI4.Brush.Icon");
        }

        /// <summary>配置窗口基本属性（尺寸、样式、启动位置等）。</summary>
        private void ConfigureWindowProperties(string title)
        {
            Title = title ?? UI4MultiLanguage.Get(UI4LanguageKey.Notice);
            Width = DefaultWidth;
            MinHeight = DefaultMinHeight;
            MaxHeight = DefaultMaxHeight;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            FontFamily = WindowFontFamily;
            Background = Brushes.Transparent;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        }

        /// <summary>创建主容器 Border（含阴影和圆角）。</summary>
        private void BuildMainContainer()
        {
            _mainContainer = new Border
            {
                Margin = new Thickness(28),
                CornerRadius = new CornerRadius(10),
                Effect = ContainerShadow
            };
        }

        /// <summary>构建内容布局：标题栏、消息文本、按钮区域。</summary>
        private void BuildContentLayout(string title, string content)
        {
            Grid rootGrid = new Grid { Margin = new Thickness(20) };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 标题栏
            Grid headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _iconText = new TextBlock
            {
                FontFamily = IconFontFamily,
                FontSize = 28,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                Text = "\uE134"
            };
            Grid.SetColumn(_iconText, 0);
            headerGrid.Children.Add(_iconText);

            _headingText = new TextBlock
            {
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Text = title
            };
            Grid.SetColumn(_headingText, 1);
            headerGrid.Children.Add(_headingText);
            Grid.SetRow(headerGrid, 0);
            rootGrid.Children.Add(headerGrid);

            // 消息文本
            _messageText = new TextBlock
            {
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0),
                Opacity = 0.85,
                Text = content
            };
            Grid.SetRow(_messageText, 1);
            rootGrid.Children.Add(_messageText);

            // 按钮区域
            Grid buttonWrapper = new Grid();
            buttonWrapper.Margin = new Thickness(0, 12, 0, 0);
            Grid.SetRow(buttonWrapper, 2);
            rootGrid.Children.Add(buttonWrapper);

            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            buttonWrapper.Children.Add(buttonPanel);
            BuildButtons(buttonPanel);

            _mainContainer.Child = rootGrid;
        }

        /// <summary>创建 OK / Cancel 按钮并添加到面板。</summary>
        private void BuildButtons(StackPanel buttonPanel)
        {
            var okButton = new UI4Button
            {
                Content = UI4MultiLanguage.Get(UI4LanguageKey.OK),
                Width = 80,
                Height = 32,
                FontSize = 13,
                Cursor = Cursors.Hand,
                IsDefault = true
            };
            okButton.Click += OkButton_Click;
            buttonPanel.Children.Add(okButton);

            if (_buttonMode == UI4MessageBoxButtons.OKCancel)
            {
                var cancelButton = new UI4Button
                {
                    Content = UI4MultiLanguage.Get(UI4LanguageKey.Cancel),
                    Width = 80,
                    Height = 32,
                    FontSize = 13,
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(12, 0, 0, 0)
                };
                cancelButton.Click += CloseButton_Click;
                buttonPanel.Children.Add(cancelButton);
            }
        }

        /// <summary>附加拖拽移动和 resize 边框，设置窗口内容。</summary>
        private void AttachDragAndResize()
        {
            _mainContainer.MouseLeftButtonDown += (ss, ee) =>
            {
                if (ee.ClickCount == 1) DragMove();
            };

            Grid resizeGrid = new Grid();
            resizeGrid.Children.Add(_mainContainer);

            var resizeBehavior = new WindowResizeBehavior(this);
            resizeBehavior.Attach(resizeGrid);

            Border rootBorder = new Border
            {
                Margin = new Thickness(20),
                Background = Brushes.Transparent
            };
            rootBorder.Child = resizeGrid;
            Content = rootBorder;
        }

        /// <summary>窗口加载完成时播放打开动画。</summary>
        private void Window_LoadedAnim(object s, RoutedEventArgs e)
        {
            if (!(Content is Border rootBorder)) return;
            WindowAnimationHelper.PlayOpenAnimation(rootBorder);
        }

        /// <summary>播放关闭动画，完成后设置 DialogResult 并关闭窗口。</summary>
        private void CloseAnimation(bool dialogResult)
        {
            if (_isClosingAnimating) return;
            if (!(Content is Border rootBorder)) return;

            _isClosingAnimating = true;
            WindowAnimationHelper.PlayCloseAnimation(rootBorder, () =>
            {
                DialogResult = dialogResult;
                Close();
            });
        }

        private void OkButton_Click(object sender, RoutedEventArgs e) => CloseAnimation(true);

        private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseAnimation(false);

        /// <summary>
        /// 显示消息框并返回用户选择结果。
        /// </summary>
        /// <param name="content">消息内容文本。</param>
        /// <param name="title">窗口标题（为 null 时使用默认"注意"）。</param>
        /// <param name="buttons">按钮模式。</param>
        /// <param name="width">窗口宽度。</param>
        /// <param name="owner">父窗口（消息框居中于其上方；为 null 时居中于屏幕）。</param>
        /// <returns>DialogResult：OK 为 true，Cancel 为 false，关闭为 null。</returns>
        public static bool? Show(string content,
            string title = null,
            UI4MessageBoxButtons buttons = UI4MessageBoxButtons.OK,
            double width = DefaultWidth,
            Window owner = null)
        {
            var box = new UI4MessageBox(title ?? UI4MultiLanguage.Get(UI4LanguageKey.Notice), content, buttons);
            box.Width = width;
            if (owner != null)
            {
                box.Owner = owner;
            }
            else
            {
                box.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            return box.ShowDialog();
        }
    }
}
