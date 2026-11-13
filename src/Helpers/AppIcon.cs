using System.Drawing;

namespace Panseo.Helpers
{
    internal static class AppIcon
    {
        public static Icon Load()
        {
            using (var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("Panseo.TrayIcon.ico"))
            using (var icon = new Icon(stream, 32, 32)) return (Icon)icon.Clone();
        }
    }
}
