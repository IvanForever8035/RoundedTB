using System;
using System.Collections.Generic;

namespace TrayProbe
{
    internal enum DockEdge { Bottom, Top, Left, Right }

    internal struct MonitorInfo
    {
        internal readonly AutoHideGeometry.Rect Bounds;
        internal readonly uint Dpi;

        internal MonitorInfo(AutoHideGeometry.Rect bounds, uint dpi)
        {
            Bounds = bounds;
            Dpi = dpi;
        }
    }

    /// <summary>
    /// Pure taskbar/monitor geometry shared by discovery, hover routing and
    /// region building. No OS calls: everything is exercised by
    /// <see cref="TaskbarModelSelfTest"/>.
    /// </summary>
    internal static class TaskbarModel
    {
        /// <summary>
        /// Which monitor edge a taskbar window is docked to, from its rect and
        /// its monitor rect. Works for visible, hidden and mid-animation bars;
        /// degenerate inputs fall back to Bottom.
        /// </summary>
        internal static DockEdge DetectDockEdge(AutoHideGeometry.Rect window, AutoHideGeometry.Rect monitor)
        {
            int width = window.Right - window.Left;
            int height = window.Bottom - window.Top;
            int monitorWidth = monitor.Right - monitor.Left;
            int monitorHeight = monitor.Bottom - monitor.Top;
            if (width <= 0 || height <= 0 || monitorWidth <= 0 || monitorHeight <= 0)
            {
                return DockEdge.Bottom;
            }

            if (width >= height)
            {
                // Horizontal bar: upper or lower half of the monitor decides.
                return window.Bottom > monitor.Top + monitorHeight / 2 ? DockEdge.Bottom : DockEdge.Top;
            }
            // Vertical bar: left or right half of the monitor decides.
            return window.Right > monitor.Left + monitorWidth / 2 ? DockEdge.Right : DockEdge.Left;
        }

        /// <summary>
        /// The screen-edge strip whose pointer arrival should wake a taskbar
        /// docked at <paramref name="edge"/>.
        /// </summary>
        internal static AutoHideGeometry.Rect EdgeTrigger(AutoHideGeometry.Rect monitor, DockEdge edge, int thickness)
        {
            if (thickness < 1) thickness = 1;
            switch (edge)
            {
                case DockEdge.Top:
                    return new AutoHideGeometry.Rect(monitor.Left, monitor.Top, monitor.Right, monitor.Top + thickness);
                case DockEdge.Left:
                    return new AutoHideGeometry.Rect(monitor.Left, monitor.Top, monitor.Left + thickness, monitor.Bottom);
                case DockEdge.Right:
                    return new AutoHideGeometry.Rect(monitor.Right - thickness, monitor.Top, monitor.Right, monitor.Bottom);
                default:
                    return new AutoHideGeometry.Rect(monitor.Left, monitor.Bottom - thickness, monitor.Right, monitor.Bottom);
            }
        }

        internal static bool Contains(AutoHideGeometry.Rect rect, int x, int y)
        {
            return x >= rect.Left && x < rect.Right && y >= rect.Top && y < rect.Bottom;
        }

        /// <summary>
        /// Finds the monitor whose edge strip contains the pointer, or -1.
        /// A point inside a monitor but away from every edge returns -1, so
        /// only genuine edge arrivals route to a taskbar.
        /// </summary>
        internal static int FindEdgeTrigger(IList<MonitorInfo> monitors, int x, int y, int thickness, out DockEdge edge)
        {
            edge = DockEdge.Bottom;
            if (monitors == null || monitors.Count == 0) return -1;

            for (int i = 0; i < monitors.Count; i++)
            {
                AutoHideGeometry.Rect m = monitors[i].Bounds;
                if (!Contains(m, x, y)) continue;

                if (y >= m.Bottom - thickness) { edge = DockEdge.Bottom; return i; }
                if (y < m.Top + thickness) { edge = DockEdge.Top; return i; }
                if (x < m.Left + thickness) { edge = DockEdge.Left; return i; }
                if (x >= m.Right - thickness) { edge = DockEdge.Right; return i; }
                return -1;
            }
            return -1;
        }

        /// <summary>
        /// Set difference over taskbar handles, de-duplicated. Used to decide
        /// which taskbars to add, keep and forget after a discovery pass.
        /// </summary>
        internal static void Reconcile(IList<IntPtr> before, IList<IntPtr> after,
            out List<IntPtr> added, out List<IntPtr> removed, out List<IntPtr> retained)
        {
            added = new List<IntPtr>();
            removed = new List<IntPtr>();
            retained = new List<IntPtr>();
            var seen = new HashSet<IntPtr>();

            if (after != null)
            {
                foreach (IntPtr handle in after)
                {
                    if (handle == IntPtr.Zero || !seen.Add(handle)) continue;
                    if (before == null || !Contains(before, handle)) added.Add(handle);
                    else retained.Add(handle);
                }
            }
            if (before != null)
            {
                foreach (IntPtr handle in before)
                {
                    if (handle == IntPtr.Zero) continue;
                    if (after == null || !Contains(after, handle)) removed.Add(handle);
                }
            }
        }

        private static bool Contains(IList<IntPtr> handles, IntPtr target)
        {
            for (int i = 0; i < handles.Count; i++)
            {
                if (handles[i] == target) return true;
            }
            return false;
        }
    }
}
