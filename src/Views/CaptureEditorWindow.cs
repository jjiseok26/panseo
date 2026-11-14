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
    public class CaptureEditorWindow : Window
    {
        private readonly Canvas _canvas;
        private sealed class DrawingChange
        {
            public UIElement Element;
            public int Index;
            public bool Added;
        }
        private readonly Stack<DrawingChange> _undo = new Stack<DrawingChange>();
        private readonly Stack<DrawingChange> _redo = new Stack<DrawingChange>();
        private DrawingTool _lastDrawingTool = DrawingTool.Pen;
        public ZoomImageView ZoomView { get; private set; }
        public DrawingTool CurrentTool { get { return _tool; } }
        private readonly Dictionary<DrawingTool, RadioButton> _tools = new Dictionary<DrawingTool, RadioButton>();
        private readonly TextBlock _status;
        private DrawingTool _tool = DrawingTool.Pen;
        private Point _start;
        private UIElement _preview;
        private TextBox _textEditor;
        private bool _drawing;
        private long _nextNumber = 1;
        private readonly TextBox _startNumberInput;

        public CaptureEditorWindow(BitmapSource image) : this(image, false) { }

        protected CaptureEditorWindow(BitmapSource image, bool screenZoom)
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
            AddTool(tools, "형광펜 Ctrl+H", DrawingTool.Highlighter);
            AddTool(tools, "직선 Ctrl+L", DrawingTool.Line);
            AddTool(tools, "지우개 Ctrl+E", DrawingTool.Eraser);
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
            AddButton(numberControls, "작게 −", () => ChangeNumberSize(-0.25));
            AddButton(numberControls, "크게 +", () => ChangeNumberSize(0.25));
            tools.Children.Add(numberControls);
            AddButton(tools, "되돌리기 Ctrl+Z", Undo);
            AddButton(tools, "다시 실행 Ctrl+Y", Redo);
            AddButton(tools, "저장 Ctrl+F12", SaveCapture);
            AddButton(tools, "화면 고정 Ctrl+F11", PinCapture);
            AddButton(tools, "전체 지우기", ClearAll);
            if (screenZoom)
            {
                AddButton(tools, "100%", () => ZoomView.Reset());
                AddButton(tools, "닫기 Esc", Close);
            }
            root.Children.Add(tools);
            _canvas = new Canvas
            {
                Width = image.PixelWidth, Height = image.PixelHeight, ClipToBounds = true,
                Background = new ImageBrush(image) { Stretch = Stretch.Fill }, Cursor = Cursors.Pen
            };
            FrameworkElement viewport;
            if (screenZoom)
            {
                ZoomView = new ZoomImageView(image, true, _canvas);
                viewport = ZoomView;
                ZoomView.ZoomChanged += (s, e) => { CommitText(true); FinishDrawing(); };
            }
            else viewport = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
                Child = _canvas, Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(viewport, 1);
            root.Children.Add(viewport);
            _status = new TextBlock { Text = "도구를 선택해 캡처 위에 그린 뒤 저장하거나 화면에 고정하세요.",
                Margin = new Thickness(12, 4, 12, 10), TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightSteelBlue };
            Grid.SetRow(_status, 2);
            root.Children.Add(_status);
            Content = root;
            _canvas.MouseLeftButtonDown += CanvasMouseDown;
            _canvas.MouseMove += CanvasMouseMove;
            _canvas.MouseLeftButtonUp += (s, e) => FinishDrawing();
            _canvas.LostMouseCapture += (s, e) => FinishDrawing();
            KeyDown += EditorKeyDown;
            if (screenZoom)
            {
                SelectTool(DrawingTool.None);
                _status.Text = "휠: 확대/축소 · 이동 도구 또는 Ctrl+드래그: 화면 이동 · 도구 선택: 판서 · 확대 화면의 판서는 저장/고정 후 닫으세요.";
            }
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
            if (tool != DrawingTool.None) _lastDrawingTool = tool;
            if (ZoomView != null) ZoomView.LeftDragPans = tool == DrawingTool.None;
            _tools[tool].IsChecked = true;
            _canvas.Cursor = tool == DrawingTool.None ? Cursors.SizeAll : tool == DrawingTool.Text ? Cursors.IBeam : Cursors.Cross;
        }

        private void CanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_textEditor != null && _textEditor.IsMouseOver) return;
            CommitText(true);
            _start = e.GetPosition(_canvas);
            if (_tool == DrawingTool.None) { if (ZoomView == null) Win32.TryDragMove(this); return; }
            if (_tool == DrawingTool.Eraser) { EraseAtPoint(_start); e.Handled = true; return; }
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
            SetStartNumber(number);
        }

        public void SetStartNumber(int number)
        {
            if (number < 1) throw new ArgumentOutOfRangeException("number");
            if (ZoomView != null) AppSettings.Instance.SetStartNumber(number);
            else _nextNumber = number;
            _startNumberInput.Text = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            SelectTool(DrawingTool.Number);
            _status.Text = "다음 클릭부터 " + number + "번으로 시작합니다.";
            _tools[DrawingTool.Number].Focus();
        }

        public void ResetNumbering()
        {
            _nextNumber = 1;
            if (ZoomView != null) AppSettings.Instance.ResetNumbering();
            _startNumberInput.Text = "1";
            _status.Text = "다음 클릭부터 1번으로 시작합니다.";
        }

        private void ChangeNumberSize(double change)
        {
            AppSettings.Instance.NumberSize += change;
            _status.Text = "번호 크기: " + (int)(AppSettings.Instance.NumberSize * 100) + "% · 다음 번호부터 적용됩니다.";
        }

        private void StampNumber(Point point)
        {
            long number = ZoomView != null ? AppSettings.Instance.TakeNextNumber() : _nextNumber++;
            AddAnnotation(DrawingHelper.CreateNumberStamp(number, point, AppSettings.Instance.DrawingColor, AppSettings.Instance.StrokeThickness, AppSettings.Instance.NumberSize));
        }

        private UIElement CreateShape(DrawingTool tool, Point start, Point end)
        {
            var brush = new SolidColorBrush(AppSettings.Instance.DrawingColor);
            double thickness = AppSettings.Instance.StrokeThickness;
            if (tool == DrawingTool.Highlighter) { brush.Opacity = 0.38; thickness *= 3.5; }
            if (tool == DrawingTool.Line) return new Line { X1 = start.X, Y1 = start.Y, X2 = end.X, Y2 = end.Y, Stroke = brush, StrokeThickness = thickness, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, IsHitTestVisible = false };
            if (tool == DrawingTool.Pen || tool == DrawingTool.Highlighter)
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
            if (_drawing && _preview != null)
            {
                _undo.Push(new DrawingChange { Element = _preview, Index = _canvas.Children.IndexOf(_preview), Added = true });
                _redo.Clear();
            }
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
            AddAnnotation(text);
        }

        private void AddAnnotation(UIElement element)
        {
            _undo.Push(new DrawingChange { Element = element, Index = _canvas.Children.Count, Added = true });
            _canvas.Children.Add(element);
            _redo.Clear();
        }

        private void EraseAtPoint(Point point)
        {
            for (int i = _canvas.Children.Count - 1; i >= 0; i--)
            {
                var child = _canvas.Children[i];
                if (!VisualTreeHelper.GetDescendantBounds(child).Contains(_canvas.TranslatePoint(point, child))) continue;
                _canvas.Children.RemoveAt(i);
                _undo.Push(new DrawingChange { Element = child, Index = i, Added = false });
                _redo.Clear();
                break;
            }
        }

        private void ApplyChange(DrawingChange change, bool forward)
        {
            if (change.Added == forward) _canvas.Children.Insert(Math.Min(change.Index, _canvas.Children.Count), change.Element);
            else _canvas.Children.Remove(change.Element);
        }

        public void Undo()
        {
            CommitText(true);
            FinishDrawing();
            if (_undo.Count == 0) return;
            var change = _undo.Pop();
            ApplyChange(change, false);
            _redo.Push(change);
        }

        public void Redo()
        {
            CommitText(true);
            FinishDrawing();
            if (_redo.Count == 0) return;
            var change = _redo.Pop();
            ApplyChange(change, true);
            _undo.Push(change);
        }

        public void ClearAll()
        {
            CommitText(false);
            FinishDrawing();
            _canvas.Children.Clear();
            _undo.Clear();
            _redo.Clear();
            ResetNumbering();
        }

        public void ToggleDrawingMode() { SelectTool(_tool == DrawingTool.None ? _lastDrawingTool : DrawingTool.None); }

        private void EditorKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control || Keyboard.FocusedElement is TextBox) return;
            switch (e.Key)
            {
                case Key.Z: Undo(); break;
                case Key.Y: Redo(); break;
                case Key.M: SelectTool(DrawingTool.None); break;
                case Key.P: SelectTool(DrawingTool.Pen); break;
                case Key.H: SelectTool(DrawingTool.Highlighter); break;
                case Key.L: SelectTool(DrawingTool.Line); break;
                case Key.E: SelectTool(DrawingTool.Eraser); break;
                case Key.R: SelectTool(DrawingTool.Rectangle); break;
                case Key.O: SelectTool(DrawingTool.Ellipse); break;
                case Key.A: SelectTool(DrawingTool.Arrow); break;
                case Key.T: SelectTool(DrawingTool.Text); break;
                case Key.N: SelectTool(DrawingTool.Number); break;
                case Key.F12: SaveCapture(); break;
                case Key.F11: PinCapture(); break;
                case Key.Delete: ClearAll(); break;
                case Key.D1: AppSettings.Instance.DrawingColor = Colors.Red; break;
                case Key.D2: AppSettings.Instance.DrawingColor = Colors.Yellow; break;
                case Key.D3: AppSettings.Instance.DrawingColor = Colors.LimeGreen; break;
                case Key.D4: AppSettings.Instance.DrawingColor = Colors.DeepSkyBlue; break;
                case Key.D5: AppSettings.Instance.DrawingColor = Colors.White; break;
                case Key.D6: AppSettings.Instance.DrawingColor = Colors.Black; break;
                default: return;
            }
            e.Handled = true;
        }

        public BitmapSource RenderCapture()
        {
            CommitText(true);
            FinishDrawing();
            var transform = _canvas.RenderTransform;
            try
            {
                // Export the entire annotated image, independent of the viewing zoom and pan.
                _canvas.RenderTransform = Transform.Identity;
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
            finally { _canvas.RenderTransform = transform; }
        }

        public void SaveCapture()
        {
            if (CaptureFiles.Save(this, RenderCapture())) _status.Text = "PNG 파일을 저장했습니다.";
        }

        public void PinCapture()
        {
            new PinnedCaptureWindow(RenderCapture()).Show();
            _status.Text = "화면에 고정했습니다. 고정 화면 우클릭 → 저장하기 / 복사하기 / 닫기";
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
                ToolTip = "휠: 확대/축소 · Ctrl+드래그: 이미지 이동 · 드래그: 창 이동 · 더블클릭: 기본 크기 · 우클릭: 저장/복사/닫기" };
            var menu = new ContextMenu();
            var save = new MenuItem { Header = "저장하기" };
            save.Click += (s, e) => CaptureFiles.Save(this, _image);
            var copy = new MenuItem { Header = "복사하기" };
            copy.Click += (s, e) =>
            {
                try { Clipboard.SetImage(_image); }
                catch (Exception ex) { MessageBox.Show(this, "이미지를 복사할 수 없습니다: " + ex.Message, "Panseo", MessageBoxButton.OK, MessageBoxImage.Error); }
            };
            var close = new MenuItem { Header = "닫기" };
            close.Click += (s, e) => Close();
            menu.Items.Add(save);
            menu.Items.Add(copy);
            menu.Items.Add(close);
            border.ContextMenu = menu;
            border.MouseLeftButtonDown += (s, e) => Win32.TryDragMove(this);
            Content = border;
            KeyDown += (s, e) => { if (e.Key == Key.D0 || e.Key == Key.NumPad0) ((ZoomImageView)border.Child).Reset(); };
        }
    }
}
