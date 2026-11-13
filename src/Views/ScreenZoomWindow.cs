using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Panseo.Native;

namespace Panseo.Views
{
    public sealed class ScreenZoomWindow : Window
    {
        public ScreenZoomWindow(BitmapSource snapshot, System.Drawing.Rectangle bounds)
        {
            Title = "판서 화면 확대/축소";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            ShowInTaskbar = false;
            Background = Brushes.Black;
            var grid = new Grid();
            var view = new ZoomImageView(snapshot, true) { Cursor = Cursors.SizeAll };
            grid.Children.Add(view);
            var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top, Background = new SolidColorBrush(Color.FromArgb(235, 26, 26, 38)), Margin = new Thickness(12) };
            var status = new TextBlock { Foreground = Brushes.White, FontSize = 14, Padding = new Thickness(12, 8, 12, 8) };
            view.ZoomChanged += (s, e) => status.Text = "현재 화면 " + view.ZoomRatio.ToString("P0") + " · 휠: 확대/축소 · 드래그: 이동";
            bar.Children.Add(status);
            var reset = new Button { Content = "100%", Margin = new Thickness(4), Padding = new Thickness(8, 3, 8, 3) };
            reset.Click += (s, e) => view.Reset();
            bar.Children.Add(reset);
            var close = new Button { Content = "닫기 (Esc)", Margin = new Thickness(4), Padding = new Thickness(8, 3, 8, 3) };
            close.Click += (s, e) => Close();
            bar.Children.Add(close);
            grid.Children.Add(bar);
            Content = grid;
            Loaded += (s, e) => Win32.SetWindowPos(new WindowInteropHelper(this).Handle,
                Win32.HWND_TOPMOST, bounds.X, bounds.Y, bounds.Width, bounds.Height, Win32.SWP_SHOWWINDOW);
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) { Close(); e.Handled = true; }
                else if (e.Key == Key.D0 || e.Key == Key.NumPad0) { view.Reset(); e.Handled = true; }
            };
        }
    }
}
