using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Panseo.Helpers;
using Panseo.Native;

namespace Panseo.Views
{
    public sealed class RegionCaptureWindow : Window
    {
        private readonly Grid _surface;
        private readonly Path _shade;
        private readonly Rectangle _outline;
        private readonly Rectangle[] _handles = new Rectangle[8];
        private readonly Button _confirm;
        private readonly TextBlock _instruction;
        private Point _start;
        private Rect _selection;
        private Rect _dragOriginal;
        private int _edgeX;
        private int _edgeY;
        private bool _moving;
        private bool _selecting;
        private bool _automaticSelection;
        private bool _initialSelectionApplied;
        public BitmapSource Selection { get; private set; }

        public RegionCaptureWindow(BitmapSource snapshot, System.Drawing.Rectangle bounds)
            : this(snapshot, bounds, System.Drawing.Rectangle.Empty, System.Windows.Forms.Cursor.Position) { }

        public RegionCaptureWindow(BitmapSource snapshot, System.Drawing.Rectangle bounds,
            System.Drawing.Rectangle suggested, System.Drawing.Point cursor)
        {
            Title = "Panseo 영역 선택";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            ShowInTaskbar = false;
            Cursor = Cursors.Cross;
            Background = Brushes.Black;
            _surface = new Grid();
            _surface.Children.Add(new Image { Source = snapshot, Stretch = Stretch.Fill });
            _shade = new Path { Fill = new SolidColorBrush(Color.FromArgb(130, 0, 0, 0)), IsHitTestVisible = false };
            _surface.Children.Add(_shade);
            var overlay = new Canvas { IsHitTestVisible = false };
            _outline = new Rectangle { Stroke = Brushes.DeepSkyBlue, StrokeThickness = 2, Visibility = Visibility.Collapsed };
            overlay.Children.Add(_outline);
            for (int i = 0; i < _handles.Length; i++)
            {
                _handles[i] = new Rectangle { Width = 10, Height = 10, Fill = Brushes.White,
                    Stroke = Brushes.DeepSkyBlue, StrokeThickness = 2, Visibility = Visibility.Collapsed };
                overlay.Children.Add(_handles[i]);
            }
            _surface.Children.Add(overlay);
            _instruction = new TextBlock
            {
                Text = "자동 선택된 영역을 확정하거나 드래그로 다시 선택하세요 · Enter: 확정 · Esc: 취소",
                Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromArgb(220, 24, 24, 38)),
                Padding = new Thickness(16, 10, 16, 10), FontSize = 16,
                TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false
            };
            _surface.Children.Add(_instruction);
            _confirm = new Button { Content = "캡처 확정 (Enter)", Width = 160, Height = 40,
                IsEnabled = false, Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Cursor = Cursors.Hand };
            _confirm.Click += (s, e) => Confirm(snapshot);
            _surface.Children.Add(_confirm);
            Content = _surface;
            Loaded += (s, e) => Win32.SetWindowPos(new WindowInteropHelper(this).Handle,
                Win32.HWND_TOPMOST, bounds.X, bounds.Y, bounds.Width, bounds.Height, Win32.SWP_SHOWWINDOW);
            _surface.SizeChanged += (s, e) =>
            {
                UpdateSelection(_selection);
                PositionInstruction(bounds, cursor);
            };
            ContentRendered += (s, e) =>
            {
                if (_initialSelectionApplied) return;
                _initialSelectionApplied = true;
                var region = System.Drawing.Rectangle.Intersect(bounds, suggested);
                if (region.Width < 24 || region.Height < 24) return;
                _automaticSelection = true;
                UpdateSelection(MapToViewport(region, bounds, _surface.RenderSize));
            };
            _surface.MouseLeftButtonDown += (s, e) =>
            {
                if (_confirm.IsMouseOver) return;
                BeginSelectionAt(e.GetPosition(_surface));
                _selecting = true;
                _surface.CaptureMouse();
            };
            _surface.MouseMove += (s, e) =>
            {
                var point = e.GetPosition(_surface);
                if (_selecting) UpdateDrag(point);
                else _surface.Cursor = CursorAt(point);
            };
            _surface.MouseLeftButtonUp += (s, e) =>
            {
                if (!_selecting) return;
                UpdateDrag(e.GetPosition(_surface));
                _selecting = false;
                _surface.ReleaseMouseCapture();
                _confirm.IsEnabled = _selection.Width >= 3 && _selection.Height >= 3;
            };
            MouseRightButtonUp += (s, e) => Close();
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) Close();
                else if (e.Key == Key.Enter) { Confirm(snapshot); e.Handled = true; }
            };
        }

        internal static Rect MapToViewport(System.Drawing.Rectangle selection, System.Drawing.Rectangle desktop, Size viewport)
        {
            var region = System.Drawing.Rectangle.Intersect(desktop, selection);
            if (region.IsEmpty || desktop.Width <= 0 || desktop.Height <= 0) return Rect.Empty;
            return new Rect((region.Left - desktop.Left) * viewport.Width / desktop.Width,
                (region.Top - desktop.Top) * viewport.Height / desktop.Height,
                region.Width * viewport.Width / desktop.Width, region.Height * viewport.Height / desktop.Height);
        }

        private void BeginSelectionAt(Point point)
        {
            _start = point;
            _dragOriginal = _selection;
            _edgeX = _edgeY = 0;
            _moving = false;
            if (_selection.Width >= 3 && _selection.Height >= 3 &&
                new Rect(_selection.Left - 8, _selection.Top - 8, _selection.Width + 16, _selection.Height + 16).Contains(_start))
            {
                _edgeX = Math.Abs(_start.X - _selection.Left) <= 8 ? -1 : Math.Abs(_start.X - _selection.Right) <= 8 ? 1 : 0;
                _edgeY = Math.Abs(_start.Y - _selection.Top) <= 8 ? -1 : Math.Abs(_start.Y - _selection.Bottom) <= 8 ? 1 : 0;
                _moving = _edgeX == 0 && _edgeY == 0 && _selection.Contains(_start);
            }
            if (_automaticSelection && _edgeX == 0 && _edgeY == 0) _moving = false;
            _automaticSelection = false;
            if (!_moving && _edgeX == 0 && _edgeY == 0) UpdateSelection(new Rect(_start, _start));
        }

        private void PositionInstruction(System.Drawing.Rectangle bounds, System.Drawing.Point cursor)
        {
            var monitor = System.Windows.Forms.Screen.FromPoint(cursor).Bounds;
            double scale = _surface.ActualWidth / bounds.Width;
            double monitorLeft = (monitor.Left - bounds.Left) * scale;
            double monitorWidth = monitor.Width * scale;
            _instruction.Width = Math.Max(120, Math.Min(720, monitorWidth - 24));
            _instruction.Margin = new Thickness(monitorLeft + (monitorWidth - _instruction.Width) / 2, 12, 0, 0);
        }

        private void Confirm(BitmapSource snapshot)
        {
            if (_selection.Width < 3 || _selection.Height < 3) return;
            Selection = Crop(snapshot, _selection, _surface.RenderSize);
            DialogResult = true;
        }

        private Cursor CursorAt(Point point)
        {
            if (_selection.Width < 3 || _selection.Height < 3) return Cursors.Cross;
            if (!new Rect(_selection.Left - 8, _selection.Top - 8, _selection.Width + 16, _selection.Height + 16).Contains(point)) return Cursors.Cross;
            bool left = Math.Abs(point.X - _selection.Left) <= 8;
            bool right = Math.Abs(point.X - _selection.Right) <= 8;
            bool top = Math.Abs(point.Y - _selection.Top) <= 8;
            bool bottom = Math.Abs(point.Y - _selection.Bottom) <= 8;
            if ((left && top) || (right && bottom)) return Cursors.SizeNWSE;
            if ((right && top) || (left && bottom)) return Cursors.SizeNESW;
            if (left || right) return Cursors.SizeWE;
            if (top || bottom) return Cursors.SizeNS;
            return _selection.Contains(point) ? Cursors.SizeAll : Cursors.Cross;
        }

        private void UpdateDrag(Point point)
        {
            double width = _surface.ActualWidth, height = _surface.ActualHeight;
            if (_moving)
            {
                double x = Math.Max(0, Math.Min(width - _dragOriginal.Width, _dragOriginal.Left + point.X - _start.X));
                double y = Math.Max(0, Math.Min(height - _dragOriginal.Height, _dragOriginal.Top + point.Y - _start.Y));
                UpdateSelection(new Rect(x, y, _dragOriginal.Width, _dragOriginal.Height));
            }
            else if (_edgeX != 0 || _edgeY != 0)
            {
                double left = _dragOriginal.Left, top = _dragOriginal.Top, right = _dragOriginal.Right, bottom = _dragOriginal.Bottom;
                if (_edgeX < 0) left = Math.Max(0, Math.Min(right - 3, point.X));
                if (_edgeX > 0) right = Math.Min(width, Math.Max(left + 3, point.X));
                if (_edgeY < 0) top = Math.Max(0, Math.Min(bottom - 3, point.Y));
                if (_edgeY > 0) bottom = Math.Min(height, Math.Max(top + 3, point.Y));
                UpdateSelection(new Rect(left, top, right - left, bottom - top));
            }
            else UpdateSelection(SelectionRect(point));
        }

        private Rect SelectionRect(Point point)
        {
            return DrawingHelper.GetBoundingRect(_start, new Point(
                Math.Max(0, Math.Min(_surface.ActualWidth, point.X)),
                Math.Max(0, Math.Min(_surface.ActualHeight, point.Y))));
        }

        private void UpdateSelection(Rect rect)
        {
            _selection = rect;
            var geometry = new GeometryGroup { FillRule = FillRule.EvenOdd };
            geometry.Children.Add(new RectangleGeometry(new Rect(_surface.RenderSize)));
            geometry.Children.Add(new RectangleGeometry(rect));
            _shade.Data = geometry;
            _outline.Visibility = rect.Width > 0 ? Visibility.Visible : Visibility.Collapsed;
            _outline.Width = rect.Width;
            _outline.Height = rect.Height;
            Canvas.SetLeft(_outline, rect.Left);
            Canvas.SetTop(_outline, rect.Top);
            bool valid = rect.Width >= 3 && rect.Height >= 3;
            double[] xs = { rect.Left, rect.Left + rect.Width / 2, rect.Right };
            double[] ys = { rect.Top, rect.Top + rect.Height / 2, rect.Bottom };
            int[,] positions = { { 0, 0 }, { 1, 0 }, { 2, 0 }, { 0, 1 }, { 2, 1 }, { 0, 2 }, { 1, 2 }, { 2, 2 } };
            for (int i = 0; i < _handles.Length; i++)
            {
                _handles[i].Visibility = valid ? Visibility.Visible : Visibility.Collapsed;
                Canvas.SetLeft(_handles[i], xs[positions[i, 0]] - 5);
                Canvas.SetTop(_handles[i], ys[positions[i, 1]] - 5);
            }
            if (_confirm != null)
            {
                _confirm.IsEnabled = valid;
                _confirm.Visibility = valid ? Visibility.Visible : Visibility.Collapsed;
                if (valid)
                {
                    double x, y;
                    if (rect.Right + 170 <= _surface.ActualWidth) { x = rect.Right + 10; y = rect.Top; }
                    else if (rect.Left >= 170) { x = rect.Left - 170; y = rect.Top; }
                    else if (rect.Bottom + 50 <= _surface.ActualHeight) { x = rect.Left; y = rect.Bottom + 10; }
                    else { x = rect.Left; y = rect.Top - 50; }
                    _confirm.Margin = new Thickness(
                        Math.Max(0, Math.Min(_surface.ActualWidth - 160, x)),
                        Math.Max(0, Math.Min(_surface.ActualHeight - 40, y)), 0, 0);
                }
            }
        }

        internal static BitmapSource Crop(BitmapSource source, Rect selection, Size viewport)
        {
            double scaleX = source.PixelWidth / viewport.Width;
            double scaleY = source.PixelHeight / viewport.Height;
            int left = Math.Max(0, Math.Min(source.PixelWidth - 1, (int)Math.Floor(selection.Left * scaleX)));
            int top = Math.Max(0, Math.Min(source.PixelHeight - 1, (int)Math.Floor(selection.Top * scaleY)));
            int right = Math.Max(left + 1, Math.Min(source.PixelWidth, (int)Math.Ceiling(selection.Right * scaleX)));
            int bottom = Math.Max(top + 1, Math.Min(source.PixelHeight, (int)Math.Ceiling(selection.Bottom * scaleY)));
            var cropped = new CroppedBitmap(source, new Int32Rect(left, top, right - left, bottom - top));
            cropped.Freeze();
            return cropped;
        }
    }
}
