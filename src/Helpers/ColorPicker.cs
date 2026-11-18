using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace Panseo.Helpers
{
    internal static class ColorPicker
    {
        private sealed class WindowOwner : Forms.IWin32Window
        {
            public IntPtr Handle { get; private set; }
            public WindowOwner(IntPtr handle) { Handle = handle; }
        }

        internal static Color? Choose(Window owner, Color current)
        {
            using (var dialog = new Forms.ColorDialog
            {
                Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B),
                FullOpen = true,
                AnyColor = true
            })
            {
                if (dialog.ShowDialog(new WindowOwner(new WindowInteropHelper(owner).Handle)) != Forms.DialogResult.OK)
                    return null;
                return Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
            }
        }
    }
}
