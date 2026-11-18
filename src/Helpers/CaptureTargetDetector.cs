using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Forms;
using Panseo.Native;

namespace Panseo.Helpers
{
    internal static class CaptureTargetDetector
    {
        internal static Rectangle Detect(Point cursor, Rectangle desktop)
        {
            var image = ImageAt(cursor, desktop);
            if (!image.IsEmpty) return image;

            var point = new Win32.POINT { X = cursor.X, Y = cursor.Y };
            IntPtr window = Win32.GetAncestor(Win32.WindowFromPoint(point), 2); // GA_ROOT
            uint ownProcess = (uint)Process.GetCurrentProcess().Id;
            for (int i = 0; window != IntPtr.Zero && i < 100; i++, window = Win32.GetWindow(window, 2)) // GW_HWNDNEXT
            {
                if (!Win32.IsWindowVisible(window)) continue;
                uint processId;
                Win32.GetWindowThreadProcessId(window, out processId);
                if (processId == ownProcess) continue;
                Win32.RECT rect;
                if (!Win32.GetWindowRect(window, out rect)) continue;
                var region = Rectangle.Intersect(desktop, Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom));
                if (region.Contains(cursor) && region.Width >= 24 && region.Height >= 24) return region;
            }
            return Rectangle.Intersect(desktop, Screen.FromPoint(cursor).Bounds);
        }

        private static Rectangle ImageAt(Point cursor, Rectangle desktop)
        {
            try
            {
                var element = AutomationElement.FromPoint(new System.Windows.Point(cursor.X, cursor.Y));
                for (int i = 0; element != null && i < 20; i++)
                {
                    if (element.Current.ControlType == ControlType.Image &&
                        element.Current.ProcessId != Process.GetCurrentProcess().Id)
                    {
                        var box = element.Current.BoundingRectangle;
                        if (box.IsEmpty || box.Width < 24 || box.Height < 24 ||
                            double.IsNaN(box.Left) || double.IsNaN(box.Top) ||
                            double.IsInfinity(box.Left) || double.IsInfinity(box.Top))
                        {
                            element = TreeWalker.RawViewWalker.GetParent(element);
                            continue;
                        }
                        var region = Rectangle.Intersect(desktop, Rectangle.FromLTRB((int)Math.Floor(box.Left),
                            (int)Math.Floor(box.Top), (int)Math.Ceiling(box.Right), (int)Math.Ceiling(box.Bottom)));
                        if (region.Contains(cursor) && region.Width >= 24 && region.Height >= 24) return region;
                    }
                    element = TreeWalker.RawViewWalker.GetParent(element);
                }
            }
            catch (ElementNotAvailableException) { }
            catch (COMException) { }
            catch (InvalidOperationException) { }
            catch (Win32Exception) { }
            catch (UnauthorizedAccessException) { }
            return Rectangle.Empty;
        }
    }
}
