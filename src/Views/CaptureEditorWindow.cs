using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using Panseo.Helpers;
using Panseo.Models;
using Panseo.Native;

namespace Panseo.Views
{
    public sealed class CaptureEditorWindow : Window
    {
        private readonly Canvas _canvas;
        private readonly Stack<UIElement> _redo = new Stack<UIElement>();
        private readonly Dictionary<DrawingTool, RadioButton> _tools = new Dictionary<DrawingTool, RadioButton>();
        private readonly TextBlock _status;
        private DrawingTool _tool = DrawingTool.Pen;
        private Point _start;
        private UIElement _preview;
        private TextBox _textEditor;
        private bool _drawing;
        private long _nextNumber = 1;
        private readonly TextBox _startNumberInput;

        public CaptureEditorWindow(BitmapSource image)
        {
            Title = "Panseo 캡처 편집";
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Width = Math.Min(SystemParameters.WorkArea.Width - 30, Math.Max(600, image.PixelWidth + 30));
            Height = Math.Min(SystemParameters.WorkArea.Height - 30, Math.Max(400, image.PixelHeight + 160));
            MinWidth = 320;
            MinHeight = 240;
            Background = new SolidColorBrush(Color.FromRgb(26, 26, 38));
            Foreground = Brushes.White;
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var tools = new WrapPanel { Margin = new Thickness(8) };
            AddTool(tools, "이동 Ctrl+M", DrawingTool.None);
            AddTool(tools, "펜 Ctrl+P", DrawingTool.Pen);
            AddTool(tools, "사각형 Ctrl+R", DrawingTool.Rectangle);
            AddTool(tools, "원 Ctrl+O", DrawingTool.Ellipse);
            AddTool(tools, "화살표 Ctrl+A", DrawingTool.Arrow);
            AddTool(tools, "글자 Ctrl+T", DrawingTool.Text);
            AddTool(tools, "번호 Ctrl+N", DrawingTool.Number);
            AddButton(tools, "번호 1부터", ResetNumbering);
            var numberControls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(3) };
            numberControls.Children.Add(new TextBlock { Text = "시작번호", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,5,0) });
            _startNumberInput = new TextBox { Text = "1", Width = 82, Padding = new Thickness(4), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "1~2147483647 입력 후 적용 또는 Enter" };
            _startNumberInput.KeyDown += (s, e) => { if (e.Key == Key.Enter) { ApplyStartNumber(); e.Handled = true; } };
            numberControls.Children.Add(_startNumberInput);
            AddButton(numberControls, "번호 적용", ApplyStartNumber);
            tools.Children.Add(numberControls);
            AddButton(tools, "되돌리기 Ctrl+Z", Undo);
            AddButton(tools, "다시 실행 Ctrl+Y", Redo);
            AddButton(tools, "저장 Ctrl+F10", SaveCapture);
            AddButton(tools, "화면 고정 Ctrl+F11", PinCapture);
            root.Children.Add(tools);
            _canvas = new Canvas
            {
                Width = image.PixelWidth, Height = image.PixelHeight, ClipToBounds = true,
                Background = new ImageBrush(image) { Stretch = Stretch.Fill }, Cursor = Cursors.Pen
            };
            var viewbox = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
                Child = _canvas, Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(viewbox, 1);
            root.Children.Add(viewbox);
            _status = new TextBlock { Text = "도구를 선택해 캡처 위에 그린 뒤 저장하거나 화면에 고정하세요.",
                Margin = new Thickness(12, 4, 12, 10), TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightSteelBlue };
            Grid.SetRow(_status, 2);
            root.Children.Add(_status);
            Content = root;
            _canvas.MouseLeftButtonDown += CanvasMouseDown;
            _canvas.MouseMove += CanvasMouseMove;
            _canvas.MouseLeftButtonUp += (s, e) => FinishDrawing();
            _canvas.LostMouseCapture += (s, e) => { _drawing = false; _preview = null; };
            KeyDown += EditorKeyDown;
        }

        private void AddTool(Panel panel, string label, DrawingTool tool)
        {
            var button = new RadioButton { Content = label, GroupName = "CaptureTools", Margin = new Thickness(4),
                Foreground = Brushes.White, Padding = new Thickness(4), IsChecked = tool == _tool };
            button.Click += (s, e) => SelectTool(tool);
            _tools.Add(tool, button);
            panel.Children.Add(button);
        }

        private static void AddButton(Panel panel, string label, Action action)
        {
            var button = new Button { Content = label, Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(3) };
            button.Click += (s, e) => action();
            panel.Children.Add(button);
        }

        public void SelectTool(DrawingTool tool)
        {
            CommitText(true);
            FinishDrawing();
            _tool = tool;
            _tools[tool].IsChecked = true;
            _canvas.Cursor = tool == DrawingTool.None ? Cursors.SizeAll : tool == DrawingTool.Text ? Cursors.IBeam : Cursors.Cross;
        }

        private void CanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_textEditor != null && _textEditor.IsMouseOver) return;
            CommitText(true);
            _start = e.GetPosition(_canvas);
            if (_tool == DrawingTool.None) { Win32.TryDragMove(this); return; }
            if (_tool == DrawingTool.Text) { BeginText(_start); e.Handled = true; return; }
            if (_tool == DrawingTool.Number) { StampNumber(_start); e.Handled = true; return; }
            _drawing = true;
            _preview = CreateShape(_tool, _start, _start);
            _canvas.Children.Add(_preview);
            _redo.Clear();
            _canvas.CaptureMouse();
            e.Handled = true;
        }

        private void CanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (!_drawing) return;
            var point = e.GetPosition(_canvas);
            point = new Point(Math.Max(0, Math.Min(_canvas.Width, point.X)), Math.Max(0, Math.Min(_canvas.Height, point.Y)));
            var pen = _preview as Polyline;
            if (pen != null) pen.Points.Add(point);
            else
            {
                _canvas.Children.Remove(_preview);
                _preview = CreateShape(_tool, _start, point);
                _canvas.Children.Add(_preview);
            }
        }

        private void ApplyStartNumber()
        {
            int number;
            if (!AppSettings.TryParseStartNumber(_startNumberInput.Text, out number))
            {
                _status.Text = "시작번호는 1~2147483647 사이의 정수를 입력하세요.";
                _startNumberInput.Focus();
                _startNumberInput.SelectAll();
                return;
            }
            _nextNumber = number;
            SelectTool(DrawingTool.Number);
            _status.Text = "다음 클릭부터 " + number + "번으로 시작합니다.";
            _tools[DrawingTool.Number].Focus();
        }

        private void ResetNumbering()
        {
            _nextNumber = 1;
            _startNumberInput.Text = "1";
            _status.Text = "다음 클릭부터 1번으로 시작합니다.";
        }

        private void StampNumber(Point point)
        {
            _canvas.Children.Add(DrawingHelper.CreateNumberStamp(_nextNumber++, point, AppSettings.Instance.DrawingColor, AppSettings.Instance.StrokeThickness));
            _redo.Clear();
        }

        private UIElement CreateShape(DrawingTool tool, Point start, Point end)
        {
            var brush = new SolidColorBrush(AppSettings.Instance.DrawingColor);
            double thickness = AppSettings.Instance.StrokeThickness;
            if (tool == DrawingTool.Pen)
                return new Polyline { Stroke = brush, StrokeThickness = thickness, StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, IsHitTestVisible = false,
                    Points = new PointCollection { start, end } };
            if (tool == DrawingTool.Arrow)
            {
                var arrow = DrawingHelper.CreateArrowPath(start, end, brush, brush, thickness);
                arrow.IsHitTestVisible = false;
                return arrow;
            }
            var rect = DrawingHelper.GetBoundingRect(start, end);
            Shape shape = tool == DrawingTool.Rectangle ? (Shape)new Rectangle() : new Ellipse();
            shape.Width = rect.Width;
            shape.Height = rect.Height;
            shape.Stroke = brush;
            shape.StrokeThickness = thickness;
            shape.IsHitTestVisible = false;
            Canvas.SetLeft(shape, rect.Left);
            Canvas.SetTop(shape, rect.Top);
            return shape;
        }

        private void FinishDrawing()
        {
            _drawing = false;
            _preview = null;
            if (_canvas.IsMouseCaptured) _canvas.ReleaseMouseCapture();
        }

        private void BeginText(Point point)
        {
            _textEditor = new TextBox { FontSize = 24, Foreground = new SolidColorBrush(AppSettings.Instance.DrawingColor),
                Background = Brushes.White, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                Width = Math.Min(240, Math.Max(1, _canvas.Width - point.X)) };
            Canvas.SetLeft(_textEditor, point.X);
            Canvas.SetTop(_textEditor, point.Y);
            _canvas.Children.Add(_textEditor);
            _textEditor.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) { CommitText(false); e.Handled = true; }
                else if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift) { CommitText(true); e.Handled = true; }
            };
            _textEditor.Focus();
        }

        private void CommitText(bool accept)
        {
            if (_textEditor == null) return;
            var editor = _textEditor;
            _textEditor = null;
            _canvas.Children.Remove(editor);
            if (!accept || string.IsNullOrWhiteSpace(editor.Text)) return;
            var text = new TextBlock { Text = editor.Text, FontSize = 24, Foreground = editor.Foreground,
                MaxWidth = editor.Width, TextWrapping = TextWrapping.Wrap, IsHitTestVisible = false };
            Canvas.SetLeft(text, Canvas.GetLeft(editor));
            Canvas.SetTop(text, Canvas.GetTop(editor));
            _canvas.Children.Add(text);
            _redo.Clear();
        }

        public void Undo()
        {
            CommitText(true);
            FinishDrawing();
            if (_canvas.Children.Count == 0) return;
            var last = _canvas.Children[_canvas.Children.Count - 1];
            _canvas.Children.Remove(last);
            _redo.Push(last);
        }

        public void Redo()
        {
            CommitText(true);
            FinishDrawing();
            if (_redo.Count > 0) _canvas.Children.Add(_redo.Pop());
        }

        private void EditorKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control || Keyboard.FocusedElement is TextBox) return;
            switch (e.Key)
            {
                case Key.Z: Undo(); break;
                case Key.Y: Redo(); break;
                case Key.M: SelectTool(DrawingTool.None); break;
                case Key.P: SelectTool(DrawingTool.Pen); break;
                case Key.R: SelectTool(DrawingTool.Rectangle); break;
                case Key.O: SelectTool(DrawingTool.Ellipse); break;
                case Key.A: SelectTool(DrawingTool.Arrow); break;
                case Key.T: SelectTool(DrawingTool.Text); break;
                case Key.N: SelectTool(DrawingTool.Number); break;
                case Key.F10: SaveCapture(); break;
                case Key.F11: PinCapture(); break;
                case Key.Delete: CommitText(false); _canvas.Children.Clear(); _redo.Clear(); ResetNumbering(); break;
                default: return;
            }
            e.Handled = true;
        }

        public BitmapSource RenderCapture()
        {
            CommitText(true);
            FinishDrawing();
            _canvas.Measure(new Size(_canvas.Width, _canvas.Height));
            _canvas.Arrange(new Rect(0, 0, _canvas.Width, _canvas.Height));
            _canvas.UpdateLayout();
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
                context.DrawRectangle(new VisualBrush(_canvas) { Stretch = Stretch.Fill }, null, new Rect(0, 0, _canvas.Width, _canvas.Height));
            var bitmap = new RenderTargetBitmap((int)_canvas.Width, (int)_canvas.Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        public void SaveCapture()
        {
            if (CaptureFiles.Save(this, RenderCapture())) _status.Text = "PNG 파일을 저장했습니다.";
        }

        public void PinCapture()
        {
            new PinnedCaptureWindow(RenderCapture()).Show();
            _status.Text = "화면에 고정했습니다. 고정 화면 우클릭 → 저장하기 / 닫기";
        }
    }

    internal static class CaptureFiles
    {
        public static bool Save(Window owner, BitmapSource bitmap)
        {
            try
            {
                string folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PanseoScreenshots");
                Directory.CreateDirectory(folder);
                var dialog = new SaveFileDialog { Title = "캡처 저장", Filter = "PNG 이미지 (*.png)|*.png", DefaultExt = ".png",
                    AddExtension = true, InitialDirectory = folder, FileName = "Panseo_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png" };
                if (dialog.ShowDialog(owner) != true) return false;
                WritePng(bitmap, dialog.FileName);
                try { Clipboard.SetImage(bitmap); }
                catch (System.Runtime.InteropServices.ExternalException) { /* 파일 저장은 이미 완료됨 */ }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, "캡처 저장 중 오류가 발생했습니다: " + ex.Message, "Panseo", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        internal static void WritePng(BitmapSource bitmap, string path)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);
        }
    }

    public sealed class PinnedCaptureWindow : Window
    {
        private readonly BitmapSource _image;
        public PinnedCaptureWindow(BitmapSource image)
        {
            _image = image;
            Title = "Panseo 고정 캡처";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            double scale = Math.Min(1, Math.Min((SystemParameters.WorkArea.Width - 40) / image.PixelWidth,
                (SystemParameters.WorkArea.Height - 40) / image.PixelHeight));
            Width = image.PixelWidth * scale + 2;
            Height = image.PixelHeight * scale + 2;
            var border = new Border { BorderBrush = Brushes.DeepSkyBlue, BorderThickness = new Thickness(1),
                Child = new ZoomImageView(image, false), Cursor = Cursors.SizeAll,
                ToolTip = "휠: 확대/축소 · Ctrl+드래그: 이미지 이동 · 드래그: 창 이동 · 더블클릭: 기본 크기 · 우클릭: 저장/닫기" };
            var menu = new ContextMenu();
            var save = new MenuItem { Header = "저장하기" };
            save.Click += (s, e) => CaptureFiles.Save(this, _image);
            var close = new MenuItem { Header = "닫기" };
            close.Click += (s, e) => Close();
            menu.Items.Add(save);
            menu.Items.Add(close);
            border.ContextMenu = menu;
            border.MouseLeftButtonDown += (s, e) => Win32.TryDragMove(this);
            Content = border;
            KeyDown += (s, e) => { if (e.Key == Key.D0 || e.Key == Key.NumPad0) ((ZoomImageView)border.Child).Reset(); };
        }
    }
}
