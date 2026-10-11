using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using StartUI4Controls.Internal;

namespace StartUI4Controls
{
    /// <summary>
    /// 现代风格的颜色选择器窗口，支持 HSV 色彩模型、拖拽调整和透明度控制。
    /// </summary>
    /// <remarks>
    /// <para>继承自 <see cref="System.Windows.Window"/>，提供完整的颜色选择界面，
    /// 包括色彩区域、色相滑块和透明度滑块。</para>
    /// </remarks>
    public class UI4ColorPicker : Window
    {
        private bool _isClosingAnimating;
        private bool _isUpdating;
        private Border _colorMapBorder;

        private double _hue;
        private double _saturation;
        private double _value;
        private Color _selectedColor;
        private bool _isDraggingColorMap;
        private bool _isDraggingHue;

        private Border _mainContainer;
        private Image _colorMapImage;
        private WriteableBitmap _colorMapBitmap;
        private Rectangle _hueBarRect;
        private Rectangle _previewRect;
        private UI4TextBox _hexTextBox;
        private UI4TextBox _aTextBox;
        private UI4TextBox _rTextBox;
        private UI4TextBox _gTextBox;
        private UI4TextBox _bTextBox;
        private TextBlock _labelA;
        private TextBlock _labelR;
        private TextBlock _labelG;
        private TextBlock _labelB;
        private UI4ComboBox _colorModeComboBox;
        private bool _isHsvMode;
        private UI4Button _okButton;
        private UI4Button _cancelButton;

        public Color SelectedColor => _selectedColor;

        public UI4ColorPicker(string title = null, Color? defaultColor = null)
        {
            Title = title ?? UI4MultiLanguage.Get(UI4LanguageKey.ColorPicker);
            Width = 480;
            MinHeight = 300;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI, sans-serif");
            Background = Brushes.Transparent;
            SetResourceReference(ForegroundProperty, "UI4.Brush.TextForeground");
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            _hue = 0;
            _saturation = 1;
            _value = 1;
            if (defaultColor.HasValue)
            {
                RgbToHsv(defaultColor.Value, out _hue, out _saturation, out _value);
                _selectedColor = defaultColor.Value;
            }
            else
            {
                _selectedColor = Colors.Red;
                RgbToHsv(_selectedColor, out _hue, out _saturation, out _value);
            }

            _mainContainer = new Border
            {
                Margin = new Thickness(28),
                CornerRadius = new CornerRadius(10),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 18,
                    ShadowDepth = 6,
                    Opacity = 0.3
                }
            };
            _mainContainer.SetResourceReference(Border.BackgroundProperty, "UI4.Brush.Surface");

            Grid rootGrid = new Grid { Margin = new Thickness(20) };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock iconText = new TextBlock
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 28,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                Text = "\uE790"
            };
            Grid.SetColumn(iconText, 0);
            headerGrid.Children.Add(iconText);

            TextBlock headingText = new TextBlock
            {
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Text = title
            };
            Grid.SetColumn(headingText, 1);
            headerGrid.Children.Add(headingText);
            Grid.SetRow(headerGrid, 0);
            rootGrid.Children.Add(headerGrid);

            Grid pickerGrid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            pickerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pickerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _colorMapImage = new Image
            {
                Width = 280,
                Height = 200,
                Stretch = Stretch.Fill,
                Cursor = Cursors.Cross
            };
            _colorMapBitmap = new WriteableBitmap(280, 200, 96, 96, PixelFormats.Bgr32, null);
            _colorMapImage.Source = _colorMapBitmap;

            _colorMapBorder = new Border
            {
                Width = 280,
                Height = 200,
                CornerRadius = new CornerRadius(8),
                ClipToBounds = true,
                BorderThickness = new Thickness(1),
                Child = _colorMapImage
            };
            _colorMapBorder.SetResourceReference(Border.BorderBrushProperty, "UI4.Brush.BorderNormal");
            UpdateColorMap();

            _colorMapImage.MouseDown += ColorMap_MouseDown;
            _colorMapImage.MouseMove += ColorMap_MouseMove;
            _colorMapImage.MouseUp += ColorMap_MouseUp;

            Grid.SetColumn(_colorMapBorder, 0);
            Grid.SetRow(_colorMapBorder, 0);
            pickerGrid.Children.Add(_colorMapBorder);

            _hueBarRect = new Rectangle
            {
                Width = 20,
                Height = 200,
                Margin = new Thickness(8, 0, 0, 0),
                Cursor = Cursors.Hand,
                Stroke = Brushes.Black,
                StrokeThickness = 0.5,
                RadiusX = 4,
                RadiusY = 4
            };
            UpdateHueBar();

            _hueBarRect.MouseDown += HueBar_MouseDown;
            _hueBarRect.MouseMove += HueBar_MouseMove;
            _hueBarRect.MouseUp += HueBar_MouseUp;

            Grid.SetColumn(_hueBarRect, 1);
            Grid.SetRow(_hueBarRect, 0);
            pickerGrid.Children.Add(_hueBarRect);

            Grid.SetRow(pickerGrid, 1);
            rootGrid.Children.Add(pickerGrid);

            Grid previewGrid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            previewGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            previewGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var hexRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            _previewRect = new Rectangle
            {
                Width = 40,
                Height = 28,
                Fill = new SolidColorBrush(_selectedColor),
                Stroke = Brushes.Gray,
                StrokeThickness = 0.5,
                RadiusX = 6,
                RadiusY = 6,
                VerticalAlignment = VerticalAlignment.Center
            };
            hexRow.Children.Add(_previewRect);

            _hexTextBox = new UI4TextBox
            {
                Width = 120,
                Height = 28,
                FontSize = 13,
                Text = GetHexString(_selectedColor),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _hexTextBox.TextChanged += HexTextBox_TextChanged;
            hexRow.Children.Add(_hexTextBox);

            Grid.SetColumn(hexRow, 0);
            Grid.SetColumnSpan(hexRow, 2);
            previewGrid.Children.Add(hexRow);

            var rgbRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 8, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(rgbRow, 1);
            Grid.SetColumnSpan(rgbRow, 2);

            var labelA = new TextBlock  // _labelA
            {
                Text = "A",
                Width = 16,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 130)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            };
            _labelA = labelA;
            _aTextBox = new UI4TextBox
            {
                Width = 50,
                Height = 24,
                FontSize = 12,
                Text = _selectedColor.A.ToString(),
                VerticalAlignment = VerticalAlignment.Center
            };
            _aTextBox.TextChanged += RgbTextBox_TextChanged;

            var labelR = new TextBlock  // _labelR
            {
                Text = "R",
                Width = 16,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 60, 60)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 4, 0)
            };
            _labelR = labelR;
            _rTextBox = new UI4TextBox
            {
                Width = 50,
                Height = 24,
                FontSize = 12,
                Text = _selectedColor.R.ToString(),
                VerticalAlignment = VerticalAlignment.Center
            };
            _rTextBox.TextChanged += RgbTextBox_TextChanged;

            var labelG = new TextBlock  // _labelG
            {
                Text = "G",
                Width = 16,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(60, 150, 60)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 4, 0)
            };
            _labelG = labelG;
            _gTextBox = new UI4TextBox
            {
                Width = 50,
                Height = 24,
                FontSize = 12,
                Text = _selectedColor.G.ToString(),
                VerticalAlignment = VerticalAlignment.Center
            };
            _gTextBox.TextChanged += RgbTextBox_TextChanged;

            var labelB = new TextBlock  // _labelB
            {
                Text = "B",
                Width = 16,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(60, 90, 180)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 4, 0)
            };
            _labelB = labelB;
            _bTextBox = new UI4TextBox
            {
                Width = 50,
                Height = 24,
                FontSize = 12,
                Text = _selectedColor.B.ToString(),
                VerticalAlignment = VerticalAlignment.Center
            };
            _bTextBox.TextChanged += RgbTextBox_TextChanged;

            rgbRow.Children.Add(labelA);
            rgbRow.Children.Add(_aTextBox);
            rgbRow.Children.Add(labelR);
            rgbRow.Children.Add(_rTextBox);
            rgbRow.Children.Add(labelG);
            rgbRow.Children.Add(_gTextBox);
            rgbRow.Children.Add(labelB);
            rgbRow.Children.Add(_bTextBox);
            previewGrid.Children.Add(rgbRow);

            Grid.SetRow(previewGrid, 2);
            rootGrid.Children.Add(previewGrid);

            Grid buttonWrapper = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            buttonWrapper.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            buttonWrapper.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonWrapper.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(buttonWrapper, 3);
            rootGrid.Children.Add(buttonWrapper);

            _colorModeComboBox = new UI4ComboBox
            {
                Width = 110,
                Height = 35,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                SelectedIndex = 0
            };
            _colorModeComboBox.Items.Add(new ComboBoxItem { Content = "RGB" });
            _colorModeComboBox.Items.Add(new ComboBoxItem { Content = "HSV" });
            _colorModeComboBox.SelectionChanged += ColorModeComboBox_SelectionChanged;
            Grid.SetColumn(_colorModeComboBox, 0);
            buttonWrapper.Children.Add(_colorModeComboBox);

            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            buttonWrapper.Children.Add(buttonPanel);

            Grid.SetColumn(buttonPanel, 2);

            _okButton = new UI4Button
            {
                Content = UI4MultiLanguage.Get(UI4LanguageKey.OK),
                Width = 80,
                Height = 32,
                FontSize = 13,
                Cursor = Cursors.Hand,
                IsDefault = true
            };
            _okButton.Click += OkButton_Click;
            buttonPanel.Children.Add(_okButton);

            _cancelButton = new UI4Button
            {
                Content = UI4MultiLanguage.Get(UI4LanguageKey.Cancel),
                Width = 80,
                Height = 32,
                FontSize = 13,
                Cursor = Cursors.Hand,
                Margin = new Thickness(12, 0, 0, 0)
            };
            _cancelButton.Click += CancelButton_Click;
            buttonPanel.Children.Add(_cancelButton);

            _mainContainer.Child = rootGrid;

            _mainContainer.MouseLeftButtonDown += (s, e) =>
            {
                var pos = e.GetPosition(_mainContainer);
                if (pos.Y < 50 && e.ClickCount == 1) DragMove();
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
            this.Content = rootBorder;
            this.Loaded += Window_LoadedAnim;
        }

        private static void RgbToHsv(Color color, out double h, out double s, out double v)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            v = max;
            double delta = max - min;
            if (delta < 0.0001)
            {
                h = 0;
                s = 0;
                return;
            }
            s = delta / max;
            if (max == r)
                h = 60 * ((g - b) / delta % 6);
            else if (max == g)
                h = 60 * ((b - r) / delta + 2);
            else
                h = 60 * ((r - g) / delta + 4);
            if (h < 0) h += 360;
        }

        private static Color HsvToRgb(double h, double s, double v)
        {
            double r, g, b;
            if (s == 0)
            {
                r = g = b = v;
            }
            else
            {
                h = h / 60;
                int i = (int)Math.Floor(h);
                double f = h - i;
                double p = v * (1 - s);
                double q = v * (1 - s * f);
                double t = v * (1 - s * (1 - f));
                switch (i)
                {
                    case 0: r = v; g = t; b = p; break;
                    case 1: r = q; g = v; b = p; break;
                    case 2: r = p; g = v; b = t; break;
                    case 3: r = p; g = q; b = v; break;
                    case 4: r = t; g = p; b = v; break;
                    default: r = v; g = p; b = q; break;
                }
            }
            return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }

        private static string GetHexString(Color color) =>
            $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

        private void UpdateColorMap()
        {
            if (_colorMapBitmap == null) return;
            int width = _colorMapBitmap.PixelWidth;
            int height = _colorMapBitmap.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            for (int y = 0; y < height; y++)
            {
                double value = 1.0 - (double)y / (height - 1);
                for (int x = 0; x < width; x++)
                {
                    double sat = (double)x / (width - 1);
                    Color color = HsvToRgb(_hue, sat, value);
                    int idx = y * stride + x * 4;
                    pixels[idx] = color.B;
                    pixels[idx + 1] = color.G;
                    pixels[idx + 2] = color.R;
                    pixels[idx + 3] = 255;
                }
            }
            _colorMapBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
        }

        private void UpdateHueBar()
        {
            if (_hueBarRect == null) return;
            var gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            gradient.GradientStops.Add(new GradientStop(Colors.Red, 0.0));
            gradient.GradientStops.Add(new GradientStop(Colors.Yellow, 1.0 / 6));
            gradient.GradientStops.Add(new GradientStop(Colors.Lime, 2.0 / 6));
            gradient.GradientStops.Add(new GradientStop(Colors.Cyan, 3.0 / 6));
            gradient.GradientStops.Add(new GradientStop(Colors.Blue, 4.0 / 6));
            gradient.GradientStops.Add(new GradientStop(Colors.Magenta, 5.0 / 6));
            gradient.GradientStops.Add(new GradientStop(Colors.Red, 1.0));
            _hueBarRect.Fill = gradient;
        }

        private void UpdatePreview()
        {
            byte alpha = _selectedColor.A;
            _selectedColor = HsvToRgb(_hue, _saturation, _value);
            _selectedColor = Color.FromArgb(alpha, _selectedColor.R, _selectedColor.G, _selectedColor.B);
            _previewRect.Fill = new SolidColorBrush(_selectedColor);

            _isUpdating = true;
            _hexTextBox.Text = GetHexString(_selectedColor);
            if (_isHsvMode)
            {
                _aTextBox.Text = ((int)Math.Round(_hue)).ToString();
                _rTextBox.Text = ((int)Math.Round(_saturation * 100)).ToString();
                _gTextBox.Text = ((int)Math.Round(_value * 100)).ToString();
            }
            else
            {
                _aTextBox.Text = _selectedColor.A.ToString();
                _rTextBox.Text = _selectedColor.R.ToString();
                _gTextBox.Text = _selectedColor.G.ToString();
                _bTextBox.Text = _selectedColor.B.ToString();
            }
            _isUpdating = false;
        }

        private void HexTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;

            string hex = _hexTextBox.Text.Trim();
            if (hex.StartsWith("#")) hex = hex.Substring(1);

            byte a = 255, r, g, b;
            bool valid;

            if (hex.Length == 8)
                valid = TryParseHex(hex, out a, out r, out g, out b);
            else if (hex.Length == 6)
                valid = TryParseHexRgb(hex, out r, out g, out b);
            else
                return;

            if (valid)
            {
                var color = Color.FromArgb(a, r, g, b);
                RgbToHsv(color, out _hue, out _saturation, out _value);
                UpdateColorMap();
                _isUpdating = true;
                if (_isHsvMode)
                {
                    _aTextBox.Text = ((int)Math.Round(_hue)).ToString();
                    _rTextBox.Text = ((int)Math.Round(_saturation * 100)).ToString();
                    _gTextBox.Text = ((int)Math.Round(_value * 100)).ToString();
                }
                else
                {
                    _aTextBox.Text = a.ToString();
                    _rTextBox.Text = r.ToString();
                    _gTextBox.Text = g.ToString();
                    _bTextBox.Text = b.ToString();
                }
                _previewRect.Fill = new SolidColorBrush(color);
                _selectedColor = color;
                _isUpdating = false;
            }
        }

        private void RgbTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;

            if (_isHsvMode)
            {
                if (double.TryParse(_aTextBox.Text, out double h) &&
                    double.TryParse(_rTextBox.Text, out double s) &&
                    double.TryParse(_gTextBox.Text, out double v))
                {
                    _hue = Math.Max(0.0, Math.Min(360.0, h));
                    _saturation = Math.Max(0.0, Math.Min(1.0, s / 100.0));
                    _value = Math.Max(0.0, Math.Min(1.0, v / 100.0));
                    UpdateColorMap();
                    byte alpha = _selectedColor.A;
                    _selectedColor = HsvToRgb(_hue, _saturation, _value);
                    _selectedColor = Color.FromArgb(alpha, _selectedColor.R, _selectedColor.G, _selectedColor.B);
                    _isUpdating = true;
                    _hexTextBox.Text = GetHexString(_selectedColor);
                    _previewRect.Fill = new SolidColorBrush(_selectedColor);
                    _isUpdating = false;
                }
            }
            else
            {
                if (byte.TryParse(_aTextBox.Text, out byte a) &&
                    byte.TryParse(_rTextBox.Text, out byte r) &&
                    byte.TryParse(_gTextBox.Text, out byte g) &&
                    byte.TryParse(_bTextBox.Text, out byte b))
                {
                    var color = Color.FromArgb(a, r, g, b);
                    RgbToHsv(color, out _hue, out _saturation, out _value);
                    UpdateColorMap();
                    _isUpdating = true;
                    _hexTextBox.Text = GetHexString(color);
                    _previewRect.Fill = new SolidColorBrush(color);
                    _selectedColor = color;
                    _isUpdating = false;
                }
            }
        }

        private void ColorModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_colorModeComboBox.SelectedIndex == 1)
            {
                _isHsvMode = true;
                _labelA.Text = "H";
                _labelA.Foreground = new SolidColorBrush(Color.FromRgb(200, 80, 40));
                _labelR.Text = "S";
                _labelR.Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 130));
                _labelG.Text = "V";
                _labelG.Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 130));
                _labelB.Visibility = Visibility.Collapsed;
                _bTextBox.Visibility = Visibility.Collapsed;

                _aTextBox.Text = ((int)Math.Round(_hue)).ToString();
                _rTextBox.Text = ((int)Math.Round(_saturation * 100)).ToString();
                _gTextBox.Text = ((int)Math.Round(_value * 100)).ToString();
            }
            else
            {
                _isHsvMode = false;
                _labelA.Text = "A";
                _labelA.Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 130));
                _labelR.Text = "R";
                _labelR.Foreground = new SolidColorBrush(Color.FromRgb(180, 60, 60));
                _labelG.Text = "G";
                _labelG.Foreground = new SolidColorBrush(Color.FromRgb(60, 150, 60));
                _labelB.Visibility = Visibility.Visible;
                _bTextBox.Visibility = Visibility.Visible;

                _aTextBox.Text = _selectedColor.A.ToString();
                _rTextBox.Text = _selectedColor.R.ToString();
                _gTextBox.Text = _selectedColor.G.ToString();
                _bTextBox.Text = _selectedColor.B.ToString();
            }
        }

        private static bool TryParseHex(string hex, out byte a, out byte r, out byte g, out byte b)
        {
            a = r = g = b = 0;
            if (hex.Length != 8) return false;
            if (!byte.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out a)) return false;
            if (!byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out r)) return false;
            if (!byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out g)) return false;
            if (!byte.TryParse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber, null, out b)) return false;
            return true;
        }

        private static bool TryParseHexRgb(string hex, out byte r, out byte g, out byte b)
        {
            r = g = b = 0;
            if (hex.Length != 6) return false;
            if (!byte.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out r)) return false;
            if (!byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out g)) return false;
            if (!byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out b)) return false;
            return true;
        }

        private void ColorMap_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isDraggingColorMap = true;
                Mouse.Capture(_colorMapImage);
                UpdateColorFromMap(e.GetPosition(_colorMapImage));
            }
        }

        private void ColorMap_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingColorMap && e.LeftButton == MouseButtonState.Pressed)
                UpdateColorFromMap(e.GetPosition(_colorMapImage));
        }

        private void ColorMap_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingColorMap)
            {
                _isDraggingColorMap = false;
                Mouse.Capture(null);
            }
        }

        private void HueBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isDraggingHue = true;
                Mouse.Capture(_hueBarRect);
                UpdateHueFromBar(e.GetPosition(_hueBarRect));
            }
        }

        private void HueBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingHue && e.LeftButton == MouseButtonState.Pressed)
                UpdateHueFromBar(e.GetPosition(_hueBarRect));
        }

        private void HueBar_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingHue)
            {
                _isDraggingHue = false;
                Mouse.Capture(null);
            }
        }

        private void UpdateColorFromMap(Point pos)
        {
            double x = pos.X / _colorMapImage.ActualWidth;
            double y = pos.Y / _colorMapImage.ActualHeight;
            x = Math.Max(0.0, Math.Min(1.0, x));
            y = Math.Max(0.0, Math.Min(1.0, y));
            _saturation = x;
            _value = 1 - y;
            UpdatePreview();
        }

        private void UpdateHueFromBar(Point pos)
        {
            double y = pos.Y / _hueBarRect.ActualHeight;
            y = Math.Max(0.0, Math.Min(1.0, y));
            _hue = y * 360;
            UpdateColorMap();
            UpdatePreview();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e) => CloseAnimation(true);
        private void CancelButton_Click(object sender, RoutedEventArgs e) => CloseAnimation(false);

        private void Window_LoadedAnim(object s, RoutedEventArgs e)
        {
            if (!(this.Content is Border rootBorder)) return;
            WindowAnimationHelper.PlayOpenAnimation(rootBorder);
        }

        private void CloseAnimation(bool dialogResult)
        {
            if (_isClosingAnimating) return;
            if (!(this.Content is Border rootBorder)) return;

            _isClosingAnimating = true;
            WindowAnimationHelper.PlayCloseAnimation(rootBorder, () =>
            {
                DialogResult = dialogResult;
                Close();
            });
        }

        public static Color? ShowDialog(string title = null, Color? defaultColor = null, Window owner = null)
        {
            var picker = new UI4ColorPicker(title, defaultColor);
            if (owner != null) picker.Owner = owner;
            bool? result = ((Window)picker).ShowDialog();
            return result == true ? picker._selectedColor : (Color?)null;
        }

        public bool? Show(Window owner = null)
        {
            if (owner != null) Owner = owner;
            return base.ShowDialog();
        }
    }
}
