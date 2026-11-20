using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Panseo.Helpers
{
    internal static class ColorPicker
    {
        internal static Color? Choose(Window owner, Color current)
        {
            var dialog = new PickerWindow(current) { Owner = owner };
            return dialog.ShowDialog() == true ? dialog.SelectedColor : (Color?)null;
        }

        internal static Color FromHsv(double hue, double saturation, double value)
        {
            double chroma = value * saturation;
            double x = chroma * (1 - Math.Abs((hue / 60) % 2 - 1));
            double m = value - chroma;
            double r = 0, g = 0, b = 0;
            if (hue < 60) { r = chroma; g = x; }
            else if (hue < 120) { r = x; g = chroma; }
            else if (hue < 180) { g = chroma; b = x; }
            else if (hue < 240) { g = x; b = chroma; }
            else if (hue < 300) { r = x; b = chroma; }
            else { r = chroma; b = x; }
            return Color.FromRgb((byte)Math.Round((r + m) * 255),
                (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
        }

        private static void ToHsv(Color color, out double hue, out double saturation, out double value)
        {
            double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;
            hue = 0;
            if (delta > 0)
            {
                if (max == r) hue = 60 * (((g - b) / delta) % 6);
                else if (max == g) hue = 60 * ((b - r) / delta + 2);
                else hue = 60 * ((r - g) / delta + 4);
            }
            if (hue < 0) hue += 360;
            saturation = max == 0 ? 0 : delta / max;
            value = max;
        }

        private sealed class PickerWindow : Window
        {
            private readonly Grid _field;
            private readonly Ellipse _marker;
            private readonly Border _preview;
            private readonly TextBox _hex;
            private readonly Slider _hueSlider;
            private double _hue, _saturation, _value;
            private bool _updating;
            internal Color SelectedColor { get; private set; }

            internal PickerWindow(Color initial)
            {
                Title = "색상 선택";
                Width = 350;
                SizeToContent = SizeToContent.Height;
                ResizeMode = ResizeMode.NoResize;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                Topmost = true;
                Background = Solid("#202124");
                Foreground = Solid("#F1F3F4");
                FontFamily = new FontFamily("Malgun Gothic, Segoe UI");
                ToHsv(initial, out _hue, out _saturation, out _value);
                SelectedColor = initial;

                var root = new StackPanel { Margin = new Thickness(16) };
                root.Children.Add(new TextBlock { Text = "색상 선택", FontSize = 19,
                    FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });

                _field = new Grid { Height = 190, Cursor = Cursors.Cross, ClipToBounds = true };
                _field.Children.Add(new Rectangle { Fill = new LinearGradientBrush {
                    StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5),
                    GradientStops = new GradientStopCollection {
                        new GradientStop(Colors.White, 0),
                        new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)
                    } }, IsHitTestVisible = false });
                _field.Children.Add(new Rectangle { Fill = new LinearGradientBrush {
                    StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1),
                    GradientStops = new GradientStopCollection {
                        new GradientStop(Color.FromArgb(0, 0, 0, 0), 0),
                        new GradientStop(Colors.Black, 1)
                    } }, IsHitTestVisible = false });
                var markerLayer = new Canvas { IsHitTestVisible = false };
                _marker = new Ellipse { Width = 16, Height = 16, Stroke = Brushes.White, StrokeThickness = 2,
                    Fill = Brushes.Transparent, Effect = new System.Windows.Media.Effects.DropShadowEffect
                    { Color = Colors.Black, BlurRadius = 3, ShadowDepth = 0 } };
                markerLayer.Children.Add(_marker);
                _field.Children.Add(markerLayer);
                _field.MouseLeftButtonDown += (s, e) => { _field.CaptureMouse(); SelectAt(e.GetPosition(_field)); };
                _field.MouseMove += (s, e) => { if (_field.IsMouseCaptured) SelectAt(e.GetPosition(_field)); };
                _field.MouseLeftButtonUp += (s, e) => _field.ReleaseMouseCapture();
                _field.SizeChanged += (s, e) => Refresh();
                root.Children.Add(new Border { Child = _field, CornerRadius = new CornerRadius(10), ClipToBounds = true,
                    BorderBrush = Solid("#5F6368"), BorderThickness = new Thickness(1) });

                root.Children.Add(new TextBlock { Text = "색조", Foreground = Solid("#BDC1C6"),
                    Margin = new Thickness(0, 12, 0, 2) });
                var hueRow = new Grid();
                hueRow.Children.Add(new Border { Height = 12, CornerRadius = new CornerRadius(6),
                    Background = new LinearGradientBrush {
                        StartPoint = new Point(0, 0), EndPoint = new Point(1, 0),
                        GradientStops = new GradientStopCollection {
                            new GradientStop(Colors.Red, 0), new GradientStop(Colors.Yellow, 1.0 / 6),
                            new GradientStop(Colors.Lime, 2.0 / 6), new GradientStop(Colors.Cyan, 3.0 / 6),
                            new GradientStop(Colors.Blue, 4.0 / 6), new GradientStop(Colors.Magenta, 5.0 / 6),
                            new GradientStop(Colors.Red, 1)
                        } } });
                _hueSlider = new Slider { Minimum = 0, Maximum = 359.9, Value = _hue,
                    Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center };
                _hueSlider.ValueChanged += (s, e) => { if (!_updating) { _hue = e.NewValue; Refresh(); } };
                hueRow.Children.Add(_hueSlider);
                root.Children.Add(hueRow);

                var colorRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 10) };
                _preview = new Border { Width = 52, Height = 36, CornerRadius = new CornerRadius(8),
                    BorderBrush = Solid("#5F6368"), BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 12, 0) };
                colorRow.Children.Add(_preview);
                colorRow.Children.Add(new TextBlock { Text = "HEX", VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Solid("#BDC1C6"), Margin = new Thickness(0, 0, 8, 0) });
                _hex = new TextBox { Width = 105, Height = 30, VerticalContentAlignment = VerticalAlignment.Center,
                    Background = Solid("#3C4043"), Foreground = Foreground, BorderBrush = Solid("#5F6368"),
                    Padding = new Thickness(6, 2, 6, 2) };
                _hex.KeyDown += (s, e) => { if (e.Key == Key.Enter && ApplyHex()) e.Handled = true; };
                _hex.LostFocus += (s, e) => ApplyHex();
                colorRow.Children.Add(_hex);
                root.Children.Add(colorRow);

                var actions = new StackPanel { Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right };
                var cancel = ActionButton("취소", false);
                cancel.Click += (s, e) => Close();
                var apply = ActionButton("적용", true);
                apply.Click += (s, e) => { if (ApplyHex()) DialogResult = true; };
                actions.Children.Add(cancel);
                actions.Children.Add(apply);
                root.Children.Add(actions);
                Content = root;
                PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
                Refresh();
            }

            private void SelectAt(Point point)
            {
                if (_field.ActualWidth <= 0 || _field.ActualHeight <= 0) return;
                _saturation = Math.Max(0, Math.Min(1, point.X / _field.ActualWidth));
                _value = 1 - Math.Max(0, Math.Min(1, point.Y / _field.ActualHeight));
                Refresh();
            }

            private void Refresh()
            {
                if (_field == null || _preview == null || _hex == null) return;
                _field.Background = new SolidColorBrush(FromHsv(_hue, 1, 1));
                SelectedColor = FromHsv(_hue, _saturation, _value);
                _preview.Background = new SolidColorBrush(SelectedColor);
                Canvas.SetLeft(_marker, _saturation * _field.ActualWidth - 8);
                Canvas.SetTop(_marker, (1 - _value) * _field.ActualHeight - 8);
                _hex.Text = string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}",
                    SelectedColor.R, SelectedColor.G, SelectedColor.B);
                _hex.BorderBrush = Solid("#5F6368");
            }

            private bool ApplyHex()
            {
                string value = _hex.Text.Trim();
                if (!Regex.IsMatch(value, @"^#?[0-9a-fA-F]{6}$"))
                {
                    _hex.BorderBrush = Solid("#EA4335");
                    _hex.ToolTip = "#RRGGBB 형식으로 입력하세요.";
                    return false;
                }
                if (value[0] == '#') value = value.Substring(1);
                uint rgb = uint.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                SelectedColor = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
                ToHsv(SelectedColor, out _hue, out _saturation, out _value);
                _updating = true;
                _hueSlider.Value = _hue;
                _updating = false;
                Refresh();
                return true;
            }

            private static Button ActionButton(string text, bool primary)
            {
                var button = new Button { Content = text, Width = 70, Height = 34,
                    Margin = new Thickness(6, 0, 0, 0), Cursor = Cursors.Hand,
                    Background = Solid(primary ? "#8AB4F8" : "#3C4043"),
                    Foreground = Solid(primary ? "#202124" : "#F1F3F4"),
                    BorderBrush = Solid(primary ? "#8AB4F8" : "#5F6368") };
                var border = new FrameworkElementFactory(typeof(Border));
                border.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
                border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
                border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
                border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
                var content = new FrameworkElementFactory(typeof(ContentPresenter));
                content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                border.AppendChild(content);
                button.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
                return button;
            }

            private static Brush Solid(string hex)
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            }
        }
    }
}
