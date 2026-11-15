using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Panseo.Native;

namespace Panseo.Views
{
    public sealed class ScreenZoomWindow : CaptureEditorWindow
    {
        private bool _initialZoomApplied;
        public ScreenZoomWindow(BitmapSource snapshot, System.Drawing.Rectangle bounds)
            : this(snapshot, bounds, new System.Drawing.Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2)) { }

        public ScreenZoomWindow(BitmapSource snapshot, System.Drawing.Rectangle bounds, System.Drawing.Point cursor) : base(snapshot, true)
        {
            Title = "판서 화면 확대/축소 및 그리기";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Loaded += (s, e) => Win32.SetWindowPos(new WindowInteropHelper(this).Handle,
                Win32.HWND_TOPMOST, bounds.X, bounds.Y, bounds.Width, bounds.Height, Win32.SWP_SHOWWINDOW);
            ContentRendered += (s, e) =>
            {
                if (_initialZoomApplied) return;
                _initialZoomApplied = true;
                var point = ZoomView.PointFromScreen(new Point(cursor.X, cursor.Y));
                ZoomView.ZoomAt(2, new Point(
                    System.Math.Max(0, System.Math.Min(ZoomView.ActualWidth, point.X)),
                    System.Math.Max(0, System.Math.Min(ZoomView.ActualHeight, point.Y))));
            };
            KeyDown += (s, e) =>
            {
                if (Keyboard.FocusedElement is TextBox) return;
                if (e.Key == Key.Escape) { Close(); e.Handled = true; }
                else if (e.Key == Key.D0 || e.Key == Key.NumPad0) { ZoomView.Reset(); e.Handled = true; }
            };
        }
    }
}
