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
            return Find(cursor, desktop, true);
        }

        internal static Rectangle DetectWindow(Point cursor, Rectangle desktop)
        {
            return Find(cursor, desktop, false);
        }

        private static Rectangle Find(Point cursor, Rectangle desktop, bool preferElement)
        {
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
                if (region.Contains(cursor) && region.Width >= 24 && region.Height >= 24)
                {
                    if (preferElement)
                    {
                        var pointed = ElementFromPoint(cursor, region, ownProcess);
                        if (!pointed.IsEmpty) return pointed;
                        var element = ElementAt(window, cursor, desktop);
                        if (!element.IsEmpty) return element;
                    }
                    return region;
                }
            }
            return Rectangle.Intersect(desktop, Screen.FromPoint(cursor).Bounds);
        }

        private static Rectangle ElementFromPoint(Point cursor, Rectangle window, uint ownProcess)
        {
            try
            {
                var element = AutomationElement.FromPoint(new System.Windows.Point(cursor.X, cursor.Y));
                if (element == null || element.Current.ProcessId == ownProcess) return Rectangle.Empty;
                var box = element.Current.BoundingRectangle;
                if (box.IsEmpty || double.IsNaN(box.Left) || double.IsNaN(box.Top) ||
                    double.IsNaN(box.Right) || double.IsNaN(box.Bottom) ||
                    double.IsInfinity(box.Left) || double.IsInfinity(box.Top) ||
                    double.IsInfinity(box.Right) || double.IsInfinity(box.Bottom) ||
                    box.Left < int.MinValue || box.Top < int.MinValue ||
                    box.Right > int.MaxValue || box.Bottom > int.MaxValue) return Rectangle.Empty;
                var region = Rectangle.Intersect(window, Rectangle.FromLTRB((int)Math.Floor(box.Left),
                    (int)Math.Floor(box.Top), (int)Math.Ceiling(box.Right), (int)Math.Ceiling(box.Bottom)));
                return region.Contains(cursor) && region.Width >= 8 && region.Height >= 8 ? region : Rectangle.Empty;
            }
            catch (ElementNotAvailableException) { }
            catch (COMException) { }
            catch (InvalidOperationException) { }
            catch (Win32Exception) { }
            catch (UnauthorizedAccessException) { }
            return Rectangle.Empty;
        }

        private static Rectangle ElementAt(IntPtr window, Point cursor, Rectangle desktop)
        {
            try
            {
                var parent = AutomationElement.FromHandle(window);
                var walker = TreeWalker.RawViewWalker;
                var selected = Rectangle.Empty;
                for (int depth = 0; parent != null && depth < 20; depth++)
                {
                    AutomationElement best = null;
                    var bestRegion = Rectangle.Empty;
                    long bestArea = long.MaxValue;
                    var child = walker.GetFirstChild(parent);
                    for (int sibling = 0; child != null && sibling < 150; sibling++, child = walker.GetNextSibling(child))
                    {
                        var box = child.Current.BoundingRectangle;
                        if (box.IsEmpty || box.Width < 24 || box.Height < 24 ||
                            double.IsNaN(box.Left) || double.IsNaN(box.Top) ||
                            double.IsNaN(box.Right) || double.IsNaN(box.Bottom) ||
                            double.IsInfinity(box.Left) || double.IsInfinity(box.Top) ||
                            double.IsInfinity(box.Right) || double.IsInfinity(box.Bottom) ||
                            box.Left < int.MinValue || box.Top < int.MinValue ||
                            box.Right > int.MaxValue || box.Bottom > int.MaxValue) continue;
                        var region = Rectangle.Intersect(desktop, Rectangle.FromLTRB((int)Math.Floor(box.Left),
                            (int)Math.Floor(box.Top), (int)Math.Ceiling(box.Right), (int)Math.Ceiling(box.Bottom)));
                        long area = (long)region.Width * region.Height;
                        if (region.Contains(cursor) && region.Width >= 24 && region.Height >= 24 && area < bestArea)
                        {
                            best = child;
                            bestRegion = region;
                            bestArea = area;
                        }
                    }
                    if (best == null) break;
                    selected = bestRegion;
                    parent = best;
                }
                return selected;
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
