using System;
using System.Collections.Generic;

namespace TrayProbe
{
    internal static class TaskbarModelSelfTest
    {
        internal static string Run()
        {
            var monitor = new AutoHideGeometry.Rect(0, 0, 1920, 1080);
            var leftMonitor = new AutoHideGeometry.Rect(-1920, 0, 0, 1080);
            var rightMonitor = new AutoHideGeometry.Rect(1920, 0, 3840, 1080);

            // Dock edge detection across all four edges, visible and hidden.
            Require(TaskbarModel.DetectDockEdge(new AutoHideGeometry.Rect(0, 1032, 1920, 1080), monitor) == DockEdge.Bottom,
                "visible bottom dock");
            Require(TaskbarModel.DetectDockEdge(new AutoHideGeometry.Rect(0, 1080, 1920, 1128), monitor) == DockEdge.Bottom,
                "hidden bottom dock");
            Require(TaskbarModel.DetectDockEdge(new AutoHideGeometry.Rect(0, 0, 1920, 48), monitor) == DockEdge.Top,
                "visible top dock");
            Require(TaskbarModel.DetectDockEdge(new AutoHideGeometry.Rect(0, -48, 1920, 0), monitor) == DockEdge.Top,
                "hidden top dock");
            Require(TaskbarModel.DetectDockEdge(new AutoHideGeometry.Rect(0, 0, 48, 1080), monitor) == DockEdge.Left,
                "visible left dock");
            Require(TaskbarModel.DetectDockEdge(new AutoHideGeometry.Rect(-48, 0, 0, 1080), monitor) == DockEdge.Left,
                "hidden left dock");
            Require(TaskbarModel.DetectDockEdge(new AutoHideGeometry.Rect(1872, 0, 1920, 1080), monitor) == DockEdge.Right,
                "visible right dock");
            Require(TaskbarModel.DetectDockEdge(new AutoHideGeometry.Rect(1920, 0, 1968, 1080), monitor) == DockEdge.Right,
                "hidden right dock");

            // Negative-coordinate monitor keeps left/right correct.
            Require(TaskbarModel.DetectDockEdge(new AutoHideGeometry.Rect(-1920, 0, -1872, 1080), leftMonitor) == DockEdge.Left,
                "left dock on negative monitor");
            Require(TaskbarModel.DetectDockEdge(new AutoHideGeometry.Rect(-48, 0, 0, 1080), leftMonitor) == DockEdge.Right,
                "right dock on negative monitor");

            // Consistency with the auto-hide classifier's peek edge. The
            // hidden examples keep a two-pixel peek strip, matching how the
            // classifier reports a Hidden bar's dock edge.
            var hiddenBottom = new AutoHideGeometry.Rect(0, 1078, 1920, 1126);
            var hiddenTop = new AutoHideGeometry.Rect(0, -46, 1920, 2);
            var hiddenLeft = new AutoHideGeometry.Rect(-48, 0, 2, 1080);
            var hiddenRight = new AutoHideGeometry.Rect(1918, 0, 1968, 1080);
            Require(ClassifyEdge(hiddenBottom, monitor) == TaskbarModel.DetectDockEdge(hiddenBottom, monitor),
                "bottom dock matches classifier");
            Require(ClassifyEdge(hiddenTop, monitor) == TaskbarModel.DetectDockEdge(hiddenTop, monitor),
                "top dock matches classifier");
            Require(ClassifyEdge(hiddenLeft, monitor) == TaskbarModel.DetectDockEdge(hiddenLeft, monitor),
                "left dock matches classifier");
            Require(ClassifyEdge(hiddenRight, monitor) == TaskbarModel.DetectDockEdge(hiddenRight, monitor),
                "right dock matches classifier");

            // Edge trigger strips.
            var bottomStrip = TaskbarModel.EdgeTrigger(monitor, DockEdge.Bottom, 2);
            Require(bottomStrip.Left == 0 && bottomStrip.Top == 1078 && bottomStrip.Right == 1920 && bottomStrip.Bottom == 1080,
                "bottom trigger strip");
            var topStrip = TaskbarModel.EdgeTrigger(monitor, DockEdge.Top, 2);
            Require(topStrip.Left == 0 && topStrip.Top == 0 && topStrip.Right == 1920 && topStrip.Bottom == 2,
                "top trigger strip");
            var leftStrip = TaskbarModel.EdgeTrigger(monitor, DockEdge.Left, 2);
            Require(leftStrip.Left == 0 && leftStrip.Top == 0 && leftStrip.Right == 2 && leftStrip.Bottom == 1080,
                "left trigger strip");
            var rightStrip = TaskbarModel.EdgeTrigger(monitor, DockEdge.Right, 2);
            Require(rightStrip.Left == 1918 && rightStrip.Top == 0 && rightStrip.Right == 1920 && rightStrip.Bottom == 1080,
                "right trigger strip");

            // Pointer routing across a two-monitor desktop.
            var monitors = new List<MonitorInfo>
            {
                new MonitorInfo(leftMonitor, 96),
                new MonitorInfo(monitor, 144),
                new MonitorInfo(rightMonitor, 96),
            };
            DockEdge edge;
            Require(TaskbarModel.FindEdgeTrigger(monitors, 960, 1079, 2, out edge) == 1 && edge == DockEdge.Bottom,
                "route to centre monitor bottom edge");
            Require(TaskbarModel.FindEdgeTrigger(monitors, -960, 1079, 2, out edge) == 0 && edge == DockEdge.Bottom,
                "route to left monitor bottom edge");
            Require(TaskbarModel.FindEdgeTrigger(monitors, 2880, 1, 2, out edge) == 2 && edge == DockEdge.Top,
                "route to right monitor top edge");
            Require(TaskbarModel.FindEdgeTrigger(monitors, 960, 500, 2, out edge) == -1,
                "interior point routes nowhere");
            Require(TaskbarModel.FindEdgeTrigger(monitors, 0, 500, 2, out edge) == 1 && edge == DockEdge.Left,
                "shared left edge belongs to the centre monitor");
            Require(TaskbarModel.FindEdgeTrigger(monitors, 1920, 500, 2, out edge) == 2 && edge == DockEdge.Left,
                "shared right edge belongs to the right monitor");

            // Handle reconciliation.
            var before = new List<IntPtr> { new IntPtr(1), new IntPtr(2) };
            var after = new List<IntPtr> { new IntPtr(2), new IntPtr(3), new IntPtr(3) };
            List<IntPtr> added, removed, retained;
            TaskbarModel.Reconcile(before, after, out added, out removed, out retained);
            Require(Single(added, 3) && Single(removed, 1) && Single(retained, 2), "reconcile add/remove/retain");
            TaskbarModel.Reconcile(null, after, out added, out removed, out retained);
            Require(added.Count == 2 && removed.Count == 0 && retained.Count == 0, "reconcile from empty state");

            return "PASS: dock edges (four sides, negative monitors), edge strips, pointer routing, and handle reconciliation";
        }

        private static DockEdge ClassifyEdge(AutoHideGeometry.Rect window, AutoHideGeometry.Rect monitor)
        {
            AutoHideGeometry.Peek peek;
            AutoHideGeometry.Classify(window, monitor, 96, out peek);
            switch (peek.Side)
            {
                case AutoHideGeometry.Edge.Top: return DockEdge.Top;
                case AutoHideGeometry.Edge.Left: return DockEdge.Left;
                case AutoHideGeometry.Edge.Right: return DockEdge.Right;
                default: return DockEdge.Bottom;
            }
        }

        private static bool Single(List<IntPtr> list, long value)
        {
            return list.Count == 1 && list[0].ToInt64() == value;
        }

        private static void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException(check);
        }
    }
}
