using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace Panseo.Helpers
{
    internal static class ScreenCapture
    {
        public static BitmapSource Take(Rectangle bounds)
        {
            using (var bitmap = new Bitmap(bounds.Width, bounds.Height))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    // The virtual desktop may contain empty gaps when monitors have different heights.
                    graphics.Clear(Color.White);
                    foreach (var screen in Screen.AllScreens)
                    {
                        var visible = Rectangle.Intersect(bounds, screen.Bounds);
                        if (visible.IsEmpty) continue;
                        graphics.CopyFromScreen(visible.Left, visible.Top, visible.Left - bounds.Left,
                            visible.Top - bounds.Top, visible.Size, CopyPixelOperation.SourceCopy);
                    }
                }
                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, ImageFormat.Png);
                    stream.Position = 0;
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
            }
        }
    }
}
