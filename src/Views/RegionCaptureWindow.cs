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
        private Point _start;
        private bool _selecting;
        public BitmapSource Selection { get; private set; }

        public RegionCaptureWindow(BitmapSource snapshot, System.Drawing.Rectangle bounds)
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
            _surface.Children.Add(overlay);
            _surface.Children.Add(new TextBlock
            {
                Text = "마우스로 저장할 영역을 드래그하세요 · Esc / 우클릭: 취소",
                Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromArgb(220, 24, 24, 38)),
                Padding = new Thickness(16, 10, 16, 10), FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(12), IsHitTestVisible = false
            });
            Content = _surface;
            Loaded += (s, e) => Win32.SetWindowPos(new WindowInteropHelper(this).Handle,
                Win32.HWND_TOPMOST, bounds.X, bounds.Y, bounds.Width, bounds.Height, Win32.SWP_SHOWWINDOW);
            _surface.SizeChanged += (s, e) => UpdateSelection(new Rect());
            _surface.MouseLeftButtonDown += (s, e) =>
            {
                _start = e.GetPosition(_surface);
                _selecting = true;
                _surface.CaptureMouse();
                UpdateSelection(new Rect(_start, _start));
            };
            _surface.MouseMove += (s, e) =>
            {
                if (_selecting) UpdateSelection(SelectionRect(e.GetPosition(_surface)));
            };
            _surface.MouseLeftButtonUp += (s, e) =>
            {
                if (!_selecting) return;
                _selecting = false;
                _surface.ReleaseMouseCapture();
                var rect = SelectionRect(e.GetPosition(_surface));
                if (rect.Width < 3 || rect.Height < 3) return;
                Selection = Crop(snapshot, rect, _surface.RenderSize);
                DialogResult = true;
            };
            MouseRightButtonUp += (s, e) => Close();
            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
        }

        private Rect SelectionRect(Point point)
        {
            return DrawingHelper.GetBoundingRect(_start, new Point(
                Math.Max(0, Math.Min(_surface.ActualWidth, point.X)),
                Math.Max(0, Math.Min(_surface.ActualHeight, point.Y))));
        }

        private void UpdateSelection(Rect rect)
        {
            var geometry = new GeometryGroup { FillRule = FillRule.EvenOdd };
            geometry.Children.Add(new RectangleGeometry(new Rect(_surface.RenderSize)));
            geometry.Children.Add(new RectangleGeometry(rect));
            _shade.Data = geometry;
            _outline.Visibility = rect.Width > 0 ? Visibility.Visible : Visibility.Collapsed;
            _outline.Width = rect.Width;
            _outline.Height = rect.Height;
            Canvas.SetLeft(_outline, rect.Left);
            Canvas.SetTop(_outline, rect.Top);
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
