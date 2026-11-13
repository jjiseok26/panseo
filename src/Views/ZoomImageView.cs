using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Panseo.Views
{
    public sealed class ZoomImageView : Grid
    {
        private readonly BitmapSource _source;
        private readonly ScaleTransform _scale = new ScaleTransform();
        private readonly TranslateTransform _offset = new TranslateTransform();
        private readonly bool _leftDragPans;
        private double _fit = 1;
        private bool _panning;
        private Point _last;
        public event EventHandler ZoomChanged;
        public double ZoomRatio { get { return _scale.ScaleX / _fit; } }

        public ZoomImageView(BitmapSource source, bool leftDragPans)
        {
            _source = source;
            _leftDragPans = leftDragPans;
            ClipToBounds = true;
            Background = Brushes.Black;
            var transforms = new TransformGroup();
            transforms.Children.Add(_scale);
            transforms.Children.Add(_offset);
            Children.Add(new Image { Source = source, Width = source.PixelWidth, Height = source.PixelHeight,
                Stretch = Stretch.Fill, HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top, RenderTransform = transforms });
            SizeChanged += (s, e) => Reset();
            PreviewMouseWheel += (s, e) => { ZoomAt(Math.Pow(1.2, e.Delta / 120.0), e.GetPosition(this)); e.Handled = true; };
            PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2) { Reset(); e.Handled = true; return; }
                if (!_leftDragPans && (Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
                _last = e.GetPosition(this);
                _panning = true;
                CaptureMouse();
                e.Handled = true;
            };
            MouseMove += (s, e) =>
            {
                if (!_panning || e.LeftButton != MouseButtonState.Pressed) return;
                var point = e.GetPosition(this);
                PanBy(point - _last);
                _last = point;
            };
            PreviewMouseLeftButtonUp += (s, e) =>
            {
                if (!_panning) return;
                _panning = false;
                ReleaseMouseCapture();
                e.Handled = true;
            };
            LostMouseCapture += (s, e) => _panning = false;
        }

        public void Reset()
        {
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            _fit = Math.Min(ActualWidth / _source.PixelWidth, ActualHeight / _source.PixelHeight);
            _scale.ScaleX = _scale.ScaleY = _fit;
            _offset.X = (ActualWidth - _source.PixelWidth * _fit) / 2;
            _offset.Y = (ActualHeight - _source.PixelHeight * _fit) / 2;
            NotifyZoomChanged();
        }

        public void ZoomAt(double factor, Point anchor)
        {
            if (ActualWidth <= 0 || ActualHeight <= 0 || double.IsNaN(factor) || double.IsInfinity(factor) || factor <= 0) return;
            double previous = _scale.ScaleX;
            double next = _fit * Math.Max(0.25, Math.Min(8, ZoomRatio * factor));
            _offset.X = anchor.X - (anchor.X - _offset.X) * next / previous;
            _offset.Y = anchor.Y - (anchor.Y - _offset.Y) * next / previous;
            _scale.ScaleX = _scale.ScaleY = next;
            ClampOffset();
            NotifyZoomChanged();
        }

        public void PanBy(Vector delta)
        {
            _offset.X += delta.X;
            _offset.Y += delta.Y;
            ClampOffset();
        }

        private void ClampOffset()
        {
            double width = _source.PixelWidth * _scale.ScaleX;
            double height = _source.PixelHeight * _scale.ScaleY;
            _offset.X = width <= ActualWidth ? (ActualWidth - width) / 2 : Math.Max(ActualWidth - width, Math.Min(0, _offset.X));
            _offset.Y = height <= ActualHeight ? (ActualHeight - height) / 2 : Math.Max(ActualHeight - height, Math.Min(0, _offset.Y));
        }

        private void NotifyZoomChanged()
        {
            if (ZoomChanged != null) ZoomChanged(this, EventArgs.Empty);
        }
    }
}
