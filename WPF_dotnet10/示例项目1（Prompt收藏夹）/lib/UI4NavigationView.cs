using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using StartUI4Controls.Internal;

namespace StartUI4Controls
{

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value != null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    internal class NavigationColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is Color color ? new SolidColorBrush(color) : Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class UI4NavigationViewItem : ContentControl
    {
        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(string), typeof(UI4NavigationViewItem));

        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register("ImageSource", typeof(ImageSource), typeof(UI4NavigationViewItem));

        public ImageSource ImageSource
        {
            get { return (ImageSource)GetValue(ImageSourceProperty); }
            set { SetValue(ImageSourceProperty, value); }
        }

        public static readonly DependencyProperty TextIconProperty =
            DependencyProperty.Register("TextIcon", typeof(string), typeof(UI4NavigationViewItem),
                new FrameworkPropertyMetadata(null));

        public string TextIcon
        {
            get { return (string)GetValue(TextIconProperty); }
            set { SetValue(TextIconProperty, value); }
        }

        public static readonly DependencyProperty TextIconFontFamilyProperty =
            DependencyProperty.Register("TextIconFontFamily", typeof(FontFamily), typeof(UI4NavigationViewItem),
                new FrameworkPropertyMetadata(null));

        public FontFamily TextIconFontFamily
        {
            get { return (FontFamily)GetValue(TextIconFontFamilyProperty); }
            set { SetValue(TextIconFontFamilyProperty, value); }
        }
    }

    public class UI4NavigationViewBottomItem : UI4NavigationViewItem
    {
    }

    public class UI4NavigationView : ItemsControl
    {

        private const string PartLeftPanel = "PART_LeftPanel";
        private const string PartMenuButton = "PART_MenuButton";
        private const string PartTitleText = "PART_TitleText";
        private const string PartListBox = "PART_ListBox";
        private const string PartBottomListBox = "PART_BottomListBox";
        private const string PartSelectionIndicator = "PART_SelectionIndicator";
        private const string PartBottomSelectionIndicator = "PART_BottomSelectionIndicator";
        private const string PartContentPresenter = "PART_ContentPresenter";

        private Grid _leftPanel;
        private UI4ListBox _listBox;
        private UI4ListBox _bottomListBox;
        private Border _selectionIndicator;
        private Border _bottomSelectionIndicator;
        private TranslateTransform _selectionIndicatorTransform;
        private TranslateTransform _bottomSelectionIndicatorTransform;
        private ContentPresenter _contentPresenter;
        private TranslateTransform _contentTransform;
        private readonly ObservableCollection<UI4NavigationViewItem> _regularItems = new ObservableCollection<UI4NavigationViewItem>();
        private readonly ObservableCollection<UI4NavigationViewBottomItem> _bottomItems = new ObservableCollection<UI4NavigationViewBottomItem>();

        public static readonly DependencyProperty LeftPanelBackgroundProperty =
            DependencyProperty.Register("LeftPanelBackground", typeof(Brush), typeof(UI4NavigationView),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(10, 0, 0, 0)),
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnLeftPanelBackgroundChanged));

        public Brush LeftPanelBackground
        {
            get { return (Brush)GetValue(LeftPanelBackgroundProperty); }
            set { SetValue(LeftPanelBackgroundProperty, value); }
        }

        private static void OnLeftPanelBackgroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (UI4NavigationView)d;
            ctrl.OnLeftPanelBackgroundChanged((Brush)e.OldValue, (Brush)e.NewValue);
        }

        private void OnLeftPanelBackgroundChanged(Brush oldValue, Brush newValue)
        {
            if (_leftPanel != null)
            {
                _leftPanel.Background = newValue;
            }
        }

        public static readonly DependencyProperty LeftPanelWidthProperty =
            DependencyProperty.Register("LeftPanelWidth", typeof(double), typeof(UI4NavigationView),
                new FrameworkPropertyMetadata(double.NaN,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsMeasure,
                    OnLeftPanelWidthChanged));

        public double LeftPanelWidth
        {
            get { return (double)GetValue(LeftPanelWidthProperty); }
            set { SetValue(LeftPanelWidthProperty, value); }
        }

        private static void OnLeftPanelWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (UI4NavigationView)d;
            ctrl.OnLeftPanelWidthChanged((double)e.OldValue, (double)e.NewValue);
        }

        private void OnLeftPanelWidthChanged(double oldValue, double newValue)
        {
            if (_leftPanel != null)
            {
                _leftPanel.Width = newValue;
            }
        }

        public static readonly DependencyProperty ItemHoverColorProperty =
            DependencyProperty.Register("ItemHoverColor", typeof(Color), typeof(UI4NavigationView),
                new FrameworkPropertyMetadata(Color.FromArgb(10,0,0,0),
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnItemHoverColorChanged));

        public Color ItemHoverColor
        {
            get { return (Color)GetValue(ItemHoverColorProperty); }
            set { SetValue(ItemHoverColorProperty, value); }
        }

        private static void OnItemHoverColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (UI4NavigationView)d;
            ctrl.OnItemHoverColorChanged((Color)e.OldValue, (Color)e.NewValue);
        }

        private void OnItemHoverColorChanged(Color oldValue, Color newValue)
        {
            ForEachListBox(delegate (UI4ListBox lb) { lb.HoverBackground = newValue; });
        }

        public static readonly DependencyProperty ItemBackgroundProperty =
            DependencyProperty.Register("ItemBackground", typeof(Brush), typeof(UI4NavigationView),
                new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush ItemBackground
        {
            get { return (Brush)GetValue(ItemBackgroundProperty); }
            set { SetValue(ItemBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ItemPressedBackgroundProperty =
            DependencyProperty.Register("ItemPressedBackground", typeof(Color), typeof(UI4NavigationView),
                new FrameworkPropertyMetadata(Colors.White,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnItemPressedBackgroundChanged));

        public Color ItemPressedBackground
        {
            get { return (Color)GetValue(ItemPressedBackgroundProperty); }
            set { SetValue(ItemPressedBackgroundProperty, value); }
        }

        private static void OnItemPressedBackgroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (UI4NavigationView)d;
            ctrl.OnItemPressedBackgroundChanged((Color)e.OldValue, (Color)e.NewValue);
        }

        private void OnItemPressedBackgroundChanged(Color oldValue, Color newValue)
        {
            ForEachListBox(delegate (UI4ListBox lb) { lb.PressedBackground = newValue; });
        }

        public static readonly DependencyProperty ItemPressedForegroundProperty =
            DependencyProperty.Register("ItemPressedForeground", typeof(Color), typeof(UI4NavigationView),
                new FrameworkPropertyMetadata(Colors.Black,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnItemPressedForegroundChanged));

        public Color ItemPressedForeground
        {
            get { return (Color)GetValue(ItemPressedForegroundProperty); }
            set { SetValue(ItemPressedForegroundProperty, value); }
        }

        private static void OnItemPressedForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (UI4NavigationView)d;
            ctrl.OnItemPressedForegroundChanged((Color)e.OldValue, (Color)e.NewValue);
        }

        private void OnItemPressedForegroundChanged(Color oldValue, Color newValue)
        {
            ForEachListBox(delegate (UI4ListBox lb) { lb.PressedForeground = newValue; });
        }

        public static readonly DependencyProperty ItemHoverForegroundProperty =
            DependencyProperty.Register("ItemHoverForeground", typeof(Color), typeof(UI4NavigationView),
                new FrameworkPropertyMetadata(Colors.Black,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnItemHoverForegroundChanged));

        public Color ItemHoverForeground
        {
            get { return (Color)GetValue(ItemHoverForegroundProperty); }
            set { SetValue(ItemHoverForegroundProperty, value); }
        }

        private static void OnItemHoverForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (UI4NavigationView)d;
            ctrl.OnItemHoverForegroundChanged((Color)e.OldValue, (Color)e.NewValue);
        }

        private void OnItemHoverForegroundChanged(Color oldValue, Color newValue)
        {
            ForEachListBox(delegate (UI4ListBox lb) { lb.HoverForeground = newValue; });
        }

        public static readonly DependencyProperty ItemForegroundProperty =
            DependencyProperty.Register("ItemForeground", typeof(Brush), typeof(UI4NavigationView),
                new FrameworkPropertyMetadata(Brushes.Black,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnItemForegroundChanged));

        public Brush ItemForeground
        {
            get { return (Brush)GetValue(ItemForegroundProperty); }
            set { SetValue(ItemForegroundProperty, value); }
        }

        private static void OnItemForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (UI4NavigationView)d;
            ctrl.OnItemForegroundChanged((Brush)e.OldValue, (Brush)e.NewValue);
        }

        private void OnItemForegroundChanged(Brush oldValue, Brush newValue)
        {
            ForEachListBox(delegate (UI4ListBox lb) { lb.Foreground = newValue; });
        }

        /// <summary>
        /// 五个 Item* 回调都要同时喂常规列表和底部列表：<c>_bottomListBox</c> 只在 <see cref="OnApplyTemplate"/>
        /// 里配过一次，不补就会冻在首次套模板时那套主题的配色（浅色档下底部项直接看不见）。
        /// </summary>
        private void ForEachListBox(Action<UI4ListBox> apply)
        {
            if (_listBox != null) apply(_listBox);
            if (_bottomListBox != null) apply(_bottomListBox);
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(string), typeof(UI4NavigationView),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty SelectedItemBackgroundProperty =
            DependencyProperty.Register("SelectedItemBackground", typeof(Brush), typeof(UI4NavigationView),
                new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush SelectedItemBackground
        {
            get { return (Brush)GetValue(SelectedItemBackgroundProperty); }
            set { SetValue(SelectedItemBackgroundProperty, value); }
        }

        public static readonly DependencyProperty SelectionIndicatorBrushProperty =
            DependencyProperty.Register("SelectionIndicatorBrush", typeof(Brush), typeof(UI4NavigationView),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>选中指示条画刷，默认跟随主题强调色。</summary>
        public Brush SelectionIndicatorBrush
        {
            get { return (Brush)GetValue(SelectionIndicatorBrushProperty); }
            set { SetValue(SelectionIndicatorBrushProperty, value); }
        }

        // ItemFontSize 的 DP 登记在本控件上（ownerType = UI4NavigationView），左栏模板里
        // 也是按 FindAncestor(UI4NavigationView) 取它，所以 CLR 包装必须待在这个类里；
        // 它原先被写在 UI4NavigationViewItem 内，导致 <ui:UI4NavigationView ItemFontSize="…">
        // 在编译期报 MC3072「属性不存在」。
        public static readonly DependencyProperty ItemFontSizeProperty =
            DependencyProperty.Register("ItemFontSize", typeof(double), typeof(UI4NavigationView),
                new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>左栏项标题字号，默认 10。</summary>
        public double ItemFontSize
        {
            get { return (double)GetValue(ItemFontSizeProperty); }
            set { SetValue(ItemFontSizeProperty, value); }
        }

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register("SelectedItem", typeof(UI4NavigationViewItem), typeof(UI4NavigationView),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

        public UI4NavigationViewItem SelectedItem
        {
            get { return (UI4NavigationViewItem)GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (UI4NavigationView)d;
            ctrl.UpdateListBoxSelection(e.NewValue as UI4NavigationViewItem);
            ctrl.OnSelectedItemChangedInternal(e.OldValue as UI4NavigationViewItem, e.NewValue as UI4NavigationViewItem);
        }

        private void OnSelectedItemChangedInternal(UI4NavigationViewItem oldItem, UI4NavigationViewItem newItem)
        {
            if (oldItem == newItem) return;

            int oldIndex = -1, newIndex = -1;
            int oldBottomIndex = -1, newBottomIndex = -1;
            if (oldItem != null)
            {
                oldIndex = _regularItems.IndexOf(oldItem);
                if (oldItem is UI4NavigationViewBottomItem oldBottomItem)
                    oldBottomIndex = _bottomItems.IndexOf(oldBottomItem);
            }
            if (newItem != null)
            {
                newIndex = _regularItems.IndexOf(newItem);
                if (newItem is UI4NavigationViewBottomItem newBottomItem)
                    newBottomIndex = _bottomItems.IndexOf(newBottomItem);
            }

            bool slideFromTop;
            if (oldItem == null)
            {
                slideFromTop = false;
            }
            else
            {
                int oldSelectionIndex = oldIndex >= 0 ? oldIndex : oldBottomIndex;
                int newSelectionIndex = newIndex >= 0 ? newIndex : newBottomIndex;
                slideFromTop = (newSelectionIndex < oldSelectionIndex);
            }

            AnimateContentSlide(slideFromTop);
            AnimateSelectionIndicator(newIndex, oldItem == null);
            AnimateBottomSelectionIndicator(newBottomIndex, oldItem == null);
        }

        private void AnimateSelectionIndicator(int newIndex, bool isInitialSelection)
        {
            if (_selectionIndicatorTransform == null || _selectionIndicator == null) return;

            if (newIndex < 0)
            {
                _selectionIndicator.Visibility = Visibility.Collapsed;
                return;
            }

            _selectionIndicator.Visibility = Visibility.Visible;

            double targetY = newIndex * 70.0 + 20.0;
            if (isInitialSelection)
            {
                _selectionIndicatorTransform.Y = targetY;
                return;
            }

            var animation = new DoubleAnimation
            {
                To = targetY,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            _selectionIndicatorTransform.BeginAnimation(TranslateTransform.YProperty, animation);
        }

        private void AnimateBottomSelectionIndicator(int newIndex, bool isInitialSelection)
        {
            if (_bottomSelectionIndicatorTransform == null || _bottomSelectionIndicator == null) return;

            if (newIndex < 0)
            {
                _bottomSelectionIndicator.Visibility = Visibility.Collapsed;
                return;
            }

            _bottomSelectionIndicator.Visibility = Visibility.Visible;

            double targetY = newIndex * 70.0 + 20.0;
            if (isInitialSelection)
            {
                _bottomSelectionIndicatorTransform.Y = targetY;
                return;
            }

            var animation = new DoubleAnimation
            {
                To = targetY,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            _bottomSelectionIndicatorTransform.BeginAnimation(TranslateTransform.YProperty, animation);
        }

        private static DataTemplate CreateItemDataTemplate()
        {
            var dataTemplate = new DataTemplate();
            var stackPanel = new FrameworkElementFactory(typeof(StackPanel));
            stackPanel.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            // 60×60 是"项标题字号 = 默认 10"时量出来的尺寸，写成 Width/Height 就成了钉死值：
            // 宿主把 ItemFontSize 调大后，标签会被挤成一个字、图标行高也不够。
            // 改成 MinWidth/MinHeight 既保住默认观感，又允许随字号长。
            stackPanel.SetValue(FrameworkElement.MinWidthProperty, 60.0);
            stackPanel.SetValue(FrameworkElement.MinHeightProperty, 60.0);
            stackPanel.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            stackPanel.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            var iconGrid = new FrameworkElementFactory(typeof(Grid));
            iconGrid.SetValue(Grid.WidthProperty, 28.0);
            iconGrid.SetValue(Grid.HeightProperty, 26.0);
            iconGrid.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            iconGrid.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 8, 0, 4));

            var image = new FrameworkElementFactory(typeof(Image));
            image.SetValue(Image.WidthProperty, 24.0);
            image.SetValue(Image.HeightProperty, 24.0);
            image.SetValue(Image.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            image.SetValue(Image.VerticalAlignmentProperty, VerticalAlignment.Center);
            var imageBinding = new Binding("ImageSource");
            image.SetBinding(Image.SourceProperty, imageBinding);
            var imageVisBinding = new Binding("ImageSource")
            {
                Converter = new NullToVisibilityConverter()
            };
            image.SetBinding(UIElement.VisibilityProperty, imageVisBinding);
            iconGrid.AppendChild(image);

            var textIconBlock = new FrameworkElementFactory(typeof(TextBlock));
            textIconBlock.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            textIconBlock.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            textIconBlock.SetValue(TextBlock.FontSizeProperty, 18.0);

            var textIconBinding = new Binding("TextIcon");
            textIconBlock.SetBinding(TextBlock.TextProperty, textIconBinding);

            var fontFamilyBinding = new Binding("TextIconFontFamily");
            textIconBlock.SetBinding(TextBlock.FontFamilyProperty, fontFamilyBinding);

            var textIconVisBinding = new Binding("ImageSource")
            {
                Converter = new InverseNullToVisibilityConverter()
            };
            textIconBlock.SetBinding(UIElement.VisibilityProperty, textIconVisBinding);
            iconGrid.AppendChild(textIconBlock);

            stackPanel.AppendChild(iconGrid);

            var textBlock = new FrameworkElementFactory(typeof(TextBlock));
            textBlock.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            textBlock.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            textBlock.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            // 这里原本钉死 MaxWidth=76（配 60 宽的项容器与默认 10 号项字号量的），
            // 项字号一大就只剩一个字。宽度上限交给左栏：LeftPanelWidth 是宿主设的，
            // 装不下时由上面的 TextTrimming 出省略号——裁剪点由宿主决定，而不是模板里写死一个数。
            var fontSizeBinding = new Binding("ItemFontSize")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UI4NavigationView), 1)
            };
            textBlock.SetBinding(TextBlock.FontSizeProperty, fontSizeBinding);
            var headerTextBinding = new Binding("Header");
            textBlock.SetBinding(TextBlock.TextProperty, headerTextBinding);
            stackPanel.AppendChild(textBlock);

            dataTemplate.VisualTree = stackPanel;
            return dataTemplate;
        }

        static UI4NavigationView()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(UI4NavigationView),
                new FrameworkPropertyMetadata(typeof(UI4NavigationView)));

            var template = new ControlTemplate(typeof(UI4NavigationView));

            var rootGrid = new FrameworkElementFactory(typeof(Grid));
            rootGrid.Name = "RootGrid";

            var bgBinding = new Binding("Background")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            };
            rootGrid.SetBinding(Grid.BackgroundProperty, bgBinding);

            var colDef1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            colDef1.SetValue(ColumnDefinition.WidthProperty, new GridLength(0, GridUnitType.Auto));
            var colDef2 = new FrameworkElementFactory(typeof(ColumnDefinition));
            colDef2.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            rootGrid.AppendChild(colDef1);
            rootGrid.AppendChild(colDef2);

            var leftPanel = new FrameworkElementFactory(typeof(Grid));
            leftPanel.Name = PartLeftPanel;
            leftPanel.SetValue(Grid.ColumnProperty, 0);

            var leftRowDef1 = new FrameworkElementFactory(typeof(RowDefinition));
            leftRowDef1.SetValue(RowDefinition.HeightProperty, new GridLength(1, GridUnitType.Star));
            var leftRowDef2 = new FrameworkElementFactory(typeof(RowDefinition));
            leftRowDef2.SetValue(RowDefinition.HeightProperty, new GridLength(0, GridUnitType.Auto));
            leftPanel.AppendChild(leftRowDef1);
            leftPanel.AppendChild(leftRowDef2);

            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Grid.RowProperty, 0);
            border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(0x0C, 0x00, 0x00, 0x00)));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 1, 0));

            var scrollContentGrid = new FrameworkElementFactory(typeof(Grid));
            scrollContentGrid.SetValue(Panel.ZIndexProperty, 0);

            var listBox = new FrameworkElementFactory(typeof(UI4ListBox));
            listBox.Name = PartListBox;
            listBox.SetValue(Panel.ZIndexProperty, 0);
            listBox.SetValue(UI4ListBox.BackgroundProperty, Brushes.Transparent);
            listBox.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            listBox.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            listBox.SetValue(ItemsControl.ItemsSourceProperty, new Binding("RegularItems") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });

            var dataTemplate = CreateItemDataTemplate();
            listBox.SetValue(ItemsControl.ItemTemplateProperty, dataTemplate);

            scrollContentGrid.AppendChild(listBox);

            var selectionIndicator = new FrameworkElementFactory(typeof(Border));
            selectionIndicator.Name = PartSelectionIndicator;
            selectionIndicator.SetValue(Panel.ZIndexProperty, 1);
            selectionIndicator.SetValue(FrameworkElement.WidthProperty, 4.0);
            selectionIndicator.SetValue(FrameworkElement.HeightProperty, 30.0);
            selectionIndicator.SetValue(FrameworkElement.MarginProperty, new Thickness(6, 4, 0, 0));
            selectionIndicator.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            selectionIndicator.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
            selectionIndicator.SetBinding(Border.BackgroundProperty, new Binding(nameof(SelectionIndicatorBrush)) { RelativeSource = RelativeSource.TemplatedParent });
            scrollContentGrid.AppendChild(selectionIndicator);

            var scrollViewer = new FrameworkElementFactory(typeof(UI4ScrollViewer));
            scrollViewer.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            scrollViewer.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            scrollViewer.AppendChild(scrollContentGrid);

            border.AppendChild(scrollViewer);
            leftPanel.AppendChild(border);

            var bottomGrid = new FrameworkElementFactory(typeof(Grid));
            bottomGrid.SetValue(Grid.RowProperty, 1);

            var bottomListBox = new FrameworkElementFactory(typeof(UI4ListBox));
            bottomListBox.Name = PartBottomListBox;
            bottomListBox.SetValue(Panel.ZIndexProperty, 0);
            bottomListBox.SetValue(UI4ListBox.BackgroundProperty, Brushes.Transparent);
            bottomListBox.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            bottomListBox.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("BottomItems") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            bottomListBox.SetValue(ItemsControl.ItemTemplateProperty, dataTemplate);
            bottomGrid.AppendChild(bottomListBox);

            var bottomSelectionIndicator = new FrameworkElementFactory(typeof(Border));
            bottomSelectionIndicator.Name = PartBottomSelectionIndicator;
            bottomSelectionIndicator.SetValue(Panel.ZIndexProperty, 1);
            bottomSelectionIndicator.SetValue(FrameworkElement.WidthProperty, 4.0);
            bottomSelectionIndicator.SetValue(FrameworkElement.HeightProperty, 30.0);
            bottomSelectionIndicator.SetValue(FrameworkElement.MarginProperty, new Thickness(6, 4, 0, 0));
            bottomSelectionIndicator.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            bottomSelectionIndicator.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
            bottomSelectionIndicator.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            bottomSelectionIndicator.SetBinding(Border.BackgroundProperty, new Binding(nameof(SelectionIndicatorBrush)) { RelativeSource = RelativeSource.TemplatedParent });
            bottomGrid.AppendChild(bottomSelectionIndicator);

            leftPanel.AppendChild(bottomGrid);
            rootGrid.AppendChild(leftPanel);

            var rightBorder = new FrameworkElementFactory(typeof(Border));
            rightBorder.SetValue(Grid.ColumnProperty, 1);
            rightBorder.SetValue(Border.ClipToBoundsProperty, true);

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.Name = PartContentPresenter;
            var contentBinding = new Binding("SelectedItem.Content")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            };
            contentPresenter.SetBinding(ContentPresenter.ContentProperty, contentBinding);
            rightBorder.AppendChild(contentPresenter);
            rootGrid.AppendChild(rightBorder);

            template.VisualTree = rootGrid;
            template.Seal();

            Control.TemplateProperty.OverrideMetadata(typeof(UI4NavigationView),
                new FrameworkPropertyMetadata(template));
        }

        public UI4NavigationView()
        {
            // 声明式跟随主题：整体底/正文/左栏/选中底/指示条都挂令牌
            SetResourceReference(BackgroundProperty, "UI4.Brush.Background");
            SetResourceReference(ForegroundProperty, "UI4.Brush.TextForeground");
            SetResourceReference(LeftPanelBackgroundProperty, "UI4.Brush.Surface");
            SetResourceReference(SelectedItemBackgroundProperty, "UI4.Brush.Surface");
            SetResourceReference(SelectionIndicatorBrushProperty, "UI4.Brush.Accent");
        }

        /// <summary>
        /// 本控件的模板里没有 ItemsPresenter：项是被内部那两个 UI4ListBox 重新承载的。
        /// 默认的 ItemsControlAutomationPeer 只按"自己的项容器"枚举子节点，这里一个也找不到，
        /// 并且它会顶掉默认的可视子枚举——结果是左栏导航项与右栏整块内容从 UIA 树上一起消失
        /// （实测：读屏/自动化在窗口里枚举不到任何 ListItem）。
        /// 换成 FrameworkElementAutomationPeer 走可视树，两栏都能被读到。
        /// </summary>
        protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
        {
            return new System.Windows.Automation.Peers.FrameworkElementAutomationPeer(this);
        }

        public ObservableCollection<UI4NavigationViewItem> RegularItems => _regularItems;
        public ObservableCollection<UI4NavigationViewBottomItem> BottomItems => _bottomItems;

        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnItemsChanged(e);
            _regularItems.Clear();
            _bottomItems.Clear();
            foreach (var item in Items)
            {
                if (item is UI4NavigationViewBottomItem bottomItem)
                    _bottomItems.Add(bottomItem);
                else if (item is UI4NavigationViewItem regularItem)
                    _regularItems.Add(regularItem);
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _leftPanel = GetTemplateChild(PartLeftPanel) as Grid;
            _listBox = GetTemplateChild(PartListBox) as UI4ListBox;
            _bottomListBox = GetTemplateChild(PartBottomListBox) as UI4ListBox;
            _selectionIndicator = GetTemplateChild(PartSelectionIndicator) as Border;
            _bottomSelectionIndicator = GetTemplateChild(PartBottomSelectionIndicator) as Border;
            _selectionIndicatorTransform = new TranslateTransform();
            if (_selectionIndicator != null)
            {
                _selectionIndicator.RenderTransform = _selectionIndicatorTransform;
            }
            _bottomSelectionIndicatorTransform = new TranslateTransform();
            if (_bottomSelectionIndicator != null)
            {
                _bottomSelectionIndicator.RenderTransform = _bottomSelectionIndicatorTransform;
            }
            _contentPresenter = GetTemplateChild(PartContentPresenter) as ContentPresenter;

            if (_leftPanel == null || _listBox == null || _bottomListBox == null || _contentPresenter == null)
                throw new InvalidOperationException("Missing template parts.");

            _leftPanel.Background = LeftPanelBackground;

            ConfigureListBox(_listBox);
            ConfigureListBox(_bottomListBox);

            _contentTransform = new TranslateTransform();
            _contentPresenter.RenderTransform = _contentTransform;

            _leftPanel.Width = LeftPanelWidth;

            if (SelectedItem == null && _listBox.SelectedItem == null && Items.Count > 0)
            {
                SelectedItem = Items[0] as UI4NavigationViewItem;
            }
            else if (SelectedItem != null)
            {
                UpdateListBoxSelection(SelectedItem);
            }
            else if (_listBox.SelectedItem is UI4NavigationViewItem first)
            {
                SelectedItem = first;
            }
            else if (_bottomListBox.SelectedItem is UI4NavigationViewItem bottomFirst)
            {
                SelectedItem = bottomFirst;
            }

            if (SelectedItem != null)
            {
                int regularIndex = _regularItems.IndexOf(SelectedItem);
                int bottomIndex = SelectedItem is UI4NavigationViewBottomItem bottomItem ? _bottomItems.IndexOf(bottomItem) : -1;
                AnimateSelectionIndicator(regularIndex, true);
                AnimateBottomSelectionIndicator(bottomIndex, true);
            }
        }

        private void ConfigureListBox(UI4ListBox listBox)
        {
            listBox.Foreground = ItemForeground;
            listBox.PressedForeground = ItemPressedForeground;
            listBox.PressedBackground = ItemPressedBackground;
            listBox.HoverBackground = ItemHoverColor;
            listBox.HoverForeground = ItemHoverForeground;
            listBox.ItemPadding = new Thickness(0);
            listBox.ItemContainerStyle = CreateNavigationItemContainerStyle(listBox.ItemContainerStyle);
            listBox.SelectionChanged -= ListBox_SelectionChanged;
            listBox.SelectionChanged += ListBox_SelectionChanged;
            listBox.PreviewMouseWheel -= ListBox_PreviewMouseWheel;
            listBox.PreviewMouseWheel += ListBox_PreviewMouseWheel;
        }

        private void ListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var listBox = sender as UI4ListBox;
            if (listBox == null) return;

            var parent = VisualTreeHelper.GetParent(listBox);
            while (parent != null)
            {
                if (parent is UI4ScrollViewer scrollViewer)
                {
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta / 3.0);
                    e.Handled = true;
                    return;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
        }

        private static Style CreateNavigationItemContainerStyle(Style baseStyle)
        {
            var style = new Style(typeof(ListBoxItem), baseStyle);
            style.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
            // 这里原本钉死 70×70（70 是配默认 ItemFontSize=10 量出来的）。宿主把项字号调大后，
            // 标签会被切成一个字，而且外层 LeftPanelWidth 给多宽都没用——容器自己就是 70。
            // 宽度改为跟随左栏：宿主设了 LeftPanelWidth 就铺满，没设（NaN）时退回按内容自适应；
            // 高度只留下限 70，字号大了自然长高。
            style.Setters.Add(new Setter(FrameworkElement.WidthProperty,
                new Binding(nameof(LeftPanelWidth))
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UI4NavigationView), 1)
                }));
            style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 70.0));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));

            var itemTemplate = new ControlTemplate(typeof(ListBoxItem));
            var itemBorder = new FrameworkElementFactory(typeof(Border));
            itemBorder.Name = "PART_ItemBorder";
            itemBorder.SetBinding(Border.BackgroundProperty, new Binding(nameof(ItemBackground))
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UI4NavigationView), 1)
            });
            itemBorder.SetValue(Border.MarginProperty, new Thickness(2, 0, 0, 0));
            itemBorder.SetBinding(Border.CornerRadiusProperty, new Binding(nameof(UI4ListBox.ItemCornerRadius))
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UI4ListBox), 1)
            });

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetBinding(ContentPresenter.ContentProperty, new Binding("Content") { RelativeSource = RelativeSource.TemplatedParent });
            contentPresenter.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding("ContentTemplate") { RelativeSource = RelativeSource.TemplatedParent });
            contentPresenter.SetBinding(System.Windows.Documents.TextElement.ForegroundProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ListBoxItem), 1) });
            contentPresenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            itemBorder.AppendChild(contentPresenter);
            itemTemplate.VisualTree = itemBorder;

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new Binding(nameof(UI4ListBox.HoverBackground))
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UI4ListBox), 1),
                Converter = new NavigationColorToBrushConverter()
            }) { TargetName = "PART_ItemBorder" });
            itemTemplate.Triggers.Add(hoverTrigger);

            var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new Binding(nameof(SelectedItemBackground))
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UI4NavigationView), 1)
            }) { TargetName = "PART_ItemBorder" });
            itemTemplate.Triggers.Add(selectedTrigger);

            style.Setters.Add(new Setter(Control.TemplateProperty, itemTemplate));
            return style;
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var senderListBox = sender as UI4ListBox;
            if (senderListBox == null) return;

            if (senderListBox.SelectedItem is UI4NavigationViewItem selected)
            {
                if (SelectedItem != selected)
                    SelectedItem = selected;
            }
            else
            {
                SelectedItem = null;
            }
        }

        private void UpdateListBoxSelection(UI4NavigationViewItem item)
        {
            if (_listBox == null || _bottomListBox == null) return;

            _listBox.SelectionChanged -= ListBox_SelectionChanged;
            _bottomListBox.SelectionChanged -= ListBox_SelectionChanged;

            if (item != null)
            {
                if (_regularItems.Contains(item))
                {
                    _listBox.SelectedItem = item;
                    _bottomListBox.SelectedItem = null;
                }
                else
                {
                    _bottomListBox.SelectedItem = item;
                    _listBox.SelectedItem = null;
                }
            }
            else
            {
                _listBox.SelectedItem = null;
                _bottomListBox.SelectedItem = null;
            }

            _listBox.SelectionChanged += ListBox_SelectionChanged;
            _bottomListBox.SelectionChanged += ListBox_SelectionChanged;
        }

        private void AnimateContentSlide(bool slideFromTop)
        {
            if (_contentTransform == null) return;

            _contentTransform.BeginAnimation(TranslateTransform.YProperty, null);

            double startY = slideFromTop ? -20 : 20;
            _contentTransform.Y = startY;

            var anim = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromSeconds(0.2),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            _contentTransform.BeginAnimation(TranslateTransform.YProperty, anim);
        }
    }
}
