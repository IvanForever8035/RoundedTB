using System;
using System.Collections.Generic;

namespace TrayProbe
{
    internal struct SegmentLayout
    {
        internal int CornerRadius;
        internal int MarginTop;
        internal int MarginLeft;
        internal int MarginBottom;
        internal int MarginRight;
    }

    internal struct DynamicInput
    {
        internal int BarLeft;
        internal int BarWidth;
        internal int BarHeight;
        internal int AppListRight;
        internal int TrayLeft;
        internal bool HasTray;
        internal uint Dpi;
        internal bool Windows11;
        internal bool IsCentred;
        internal bool ShowAppList;
        internal bool ShowTray;
        internal bool ShowWidgets;
        internal bool ShowSegmentsOnHover;
        internal bool AppListVisible;
        internal bool TrayHoverEnabled;
        /// <summary>
        /// True when every enabled hover target has measured geometry. While
        /// false the host must not lock the hidden edge with an empty region.
        /// </summary>
        internal bool GeometryReady;
        internal SegmentLayout App;
        internal SegmentLayout Tray;
        internal SegmentLayout Widgets;
    }

    internal struct Segment
    {
        internal int Left, Top, Right, Bottom, Radius;

        internal Segment(int left, int top, int right, int bottom, int radius)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
            Radius = radius;
        }
    }

    internal struct SegmentRange
    {
        internal int Left, Right;

        internal SegmentRange(int left, int right)
        {
            Left = left;
            Right = right;
        }
    }

    internal sealed class DynamicPlan
    {
        internal readonly List<Segment> Segments = new List<Segment>();
        internal readonly List<SegmentRange> PeekRanges = new List<SegmentRange>();
        internal bool AnyEnabled;
    }

    internal static class DynamicGeometry
    {
        internal static DynamicPlan Build(DynamicInput input)
        {
            return Build(input, new DynamicPlan());
        }

        internal static DynamicPlan Build(DynamicInput input, DynamicPlan plan)
        {
            plan.Segments.Clear();
            plan.PeekRanges.Clear();
            plan.AnyEnabled = false;
            if (input.Dpi == 0 || input.BarWidth <= 0 || input.BarHeight <= 0)
            {
                return plan;
            }

            double scale = input.Dpi / 96.0;
            int appTop = Round(input.App.MarginTop * scale);
            int appLeft = Round(input.App.MarginLeft * scale);
            int appWidth = Round(input.BarWidth - input.App.MarginRight * scale) + 1;
            int appHeight = Round(input.BarHeight - input.App.MarginBottom * scale) + 1;
            int appRadius = Round(input.App.CornerRadius * scale);
            int centredLeft = Round(input.App.MarginRight * scale) - 1;

            int trayTop = Round(input.Tray.MarginTop * scale);
            int trayLeft = Round(input.Tray.MarginLeft * scale);
            int trayWidth = Round(input.BarWidth - input.Tray.MarginRight * scale) + 1;
            int trayHeight = Round(input.BarHeight - input.Tray.MarginBottom * scale) + 1;
            int trayRadius = Round(input.Tray.CornerRadius * scale);

            int widgetsTop = Round(input.Widgets.MarginTop * scale);
            int widgetsLeft = Round(input.Widgets.MarginLeft * scale);
            int widgetsWidth = Round(168 * scale - input.Widgets.MarginRight * scale) + 1;
            int widgetsHeight = Round(input.BarHeight - input.Widgets.MarginBottom * scale) + 1;
            int widgetsRadius = Round(input.Widgets.CornerRadius * scale);

            int centredDistance = input.BarLeft + input.BarWidth - input.AppListRight - Round(2 * scale);
            if (!input.Windows11)
            {
                centredDistance -= Round(20 * scale);
            }

            bool appEnabled = input.ShowAppList;
            bool appVisible = appEnabled && (!input.ShowSegmentsOnHover || input.AppListVisible);
            int appX1;
            int appX2;
            if (input.IsCentred)
            {
                appX1 = centredDistance + centredLeft;
                appX2 = appWidth - centredDistance;
            }
            else
            {
                appX1 = appLeft;
                appX2 = appWidth - centredDistance;
            }

            if (appVisible)
            {
                AddSegment(plan, appX1, appTop, appX2, appHeight, appRadius);
            }

            bool traySettingEnabled = input.ShowSegmentsOnHover
                ? (input.TrayHoverEnabled || input.ShowTray)
                : input.ShowTray;
            bool trayAvailable = input.HasTray && input.TrayLeft != 0 && traySettingEnabled;
            int trayX1 = (input.TrayLeft - input.BarLeft) - trayLeft;
            int trayX2 = trayWidth;
            if (input.ShowTray && input.HasTray && input.TrayLeft != 0)
            {
                AddSegment(plan, trayX1, trayTop, trayX2, trayHeight, trayRadius);
            }

            if (input.ShowWidgets)
            {
                AddSegment(plan, widgetsLeft, widgetsTop, widgetsWidth, widgetsHeight, widgetsRadius);
            }

            plan.AnyEnabled = appEnabled || trayAvailable || input.ShowWidgets;

            // Peek ranges describe where a segment could appear, even while
            // hover mode is currently hiding it.
            if (appEnabled && appTop > 0)
            {
                AddRange(plan, appX1, appX2);
            }
            if (trayAvailable && trayTop > 0)
            {
                AddRange(plan, trayX1, trayX2);
            }
            if (input.ShowWidgets && widgetsTop > 0)
            {
                AddRange(plan, widgetsLeft, widgetsWidth);
            }

            return plan;
        }

        private static void AddSegment(DynamicPlan plan, int left, int top, int right, int bottom, int radius)
        {
            if (right <= left || bottom <= top || radius < 0)
            {
                return;
            }
            plan.Segments.Add(new Segment(left, top, right, bottom, radius));
        }

        private static void AddRange(DynamicPlan plan, int left, int right)
        {
            if (right > left)
            {
                plan.PeekRanges.Add(new SegmentRange(left, right));
            }
        }

        private static int Round(double value)
        {
            return Convert.ToInt32(value);
        }
    }
}
