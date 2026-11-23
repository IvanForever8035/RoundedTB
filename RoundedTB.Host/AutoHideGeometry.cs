using System;

namespace TrayProbe
{
    internal static class AutoHideGeometry
    {
        internal enum State { Invalid, Visible, Hidden, Transitioning, OffScreen }
        internal enum Edge { Bottom, Top, Left, Right }

        internal struct Rect
        {
            internal readonly int Left, Top, Right, Bottom;

            internal Rect(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }
        }

        internal struct Peek
        {
            internal Edge Side;
            internal Rect Bounds;
        }

        internal static State Classify(Rect window, Rect monitor, uint dpi, out Peek peek)
        {
            peek = new Peek();
            if (dpi == 0 || window.Right <= window.Left || window.Bottom <= window.Top ||
                monitor.Right <= monitor.Left || monitor.Bottom <= monitor.Top)
            {
                return State.Invalid;
            }

            int bottom = window.Bottom - monitor.Bottom;
            int top = monitor.Top - window.Top;
            int left = monitor.Left - window.Left;
            int right = window.Right - monitor.Right;
            int overhang = Math.Max(Math.Max(bottom, top), Math.Max(left, right));
            if (overhang <= 2)
            {
                return State.Visible;
            }

            Rect intersection = new Rect(
                Math.Max(window.Left, monitor.Left), Math.Max(window.Top, monitor.Top),
                Math.Min(window.Right, monitor.Right), Math.Min(window.Bottom, monitor.Bottom));
            if (intersection.Right <= intersection.Left || intersection.Bottom <= intersection.Top)
            {
                return State.OffScreen;
            }

            // Match the original dock-edge priority for equal overhangs.
            peek.Side = overhang == bottom ? Edge.Bottom : overhang == top ? Edge.Top :
                overhang == left ? Edge.Left : Edge.Right;
            peek.Bounds = new Rect(intersection.Left - window.Left, intersection.Top - window.Top,
                intersection.Right - window.Left, intersection.Bottom - window.Top);
            int thickness = peek.Side == Edge.Bottom || peek.Side == Edge.Top
                ? intersection.Bottom - intersection.Top : intersection.Right - intersection.Left;
            int threshold = Math.Max(3, Convert.ToInt32(4 * (dpi / 96.0)));
            return thickness <= threshold ? State.Hidden : State.Transitioning;
        }

        internal static bool HasInsetAtPeekEdge(SimpleRegion.Bounds region, Peek peek)
        {
            switch (peek.Side)
            {
                case Edge.Bottom: return region.Top > peek.Bounds.Top;
                case Edge.Top: return region.Bottom < peek.Bounds.Bottom;
                case Edge.Left: return region.Right < peek.Bounds.Right;
                case Edge.Right: return region.Left > peek.Bounds.Left;
                default: return false;
            }
        }
    }
}
