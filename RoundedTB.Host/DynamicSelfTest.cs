using System;

namespace TrayProbe
{
    internal static class DynamicSelfTest
    {
        internal static string Run()
        {
            DynamicInput input = Base();
            DynamicPlan plan = DynamicGeometry.Build(input);
            Require(plan.AnyEnabled && plan.Segments.Count == 2 && plan.PeekRanges.Count == 1 + 1,
                "left-aligned app and tray segments");
            Require(Is(plan.Segments[0], 4, 4, 362, 49, 18), "app segment bounds");
            Require(Is(plan.Segments[1], 1614, 4, 1917, 49, 18), "tray segment bounds");
            Require(Is(plan.PeekRanges[0], 4, 362) && Is(plan.PeekRanges[1], 1614, 1917), "app and tray peek ranges");

            input = Base();
            input.IsCentred = true;
            input.AppListRight = 1280;
            plan = DynamicGeometry.Build(input);
            Require(Is(plan.Segments[0], 641, 4, 1279, 49, 18), "centred app segment bounds");

            input = Base();
            input.ShowAppList = false;
            plan = DynamicGeometry.Build(input);
            Require(plan.AnyEnabled && plan.Segments.Count == 1 && Is(plan.Segments[0], 1614, 4, 1917, 49, 18),
                "tray-only segment");

            input = Base();
            input.ShowAppList = false;
            input.ShowTray = false;
            plan = DynamicGeometry.Build(input);
            Require(!plan.AnyEnabled && plan.Segments.Count == 0 && plan.PeekRanges.Count == 0,
                "all segments disabled");

            input = Base();
            input.App.MarginTop = 0;
            input.Tray.MarginTop = 0;
            plan = DynamicGeometry.Build(input);
            Require(plan.Segments.Count == 2 && plan.PeekRanges.Count == 0, "zero top margins omit peek ranges");

            input = Base();
            input.ShowSegmentsOnHover = true;
            input.AppListVisible = false;
            input.TrayHoverEnabled = true;
            input.ShowTray = false;
            plan = DynamicGeometry.Build(input);
            Require(plan.AnyEnabled && plan.Segments.Count == 0 && plan.PeekRanges.Count == 2,
                "hover resting state keeps peek ranges but no segments");
            input.AppListVisible = true;
            input.ShowTray = true;
            plan = DynamicGeometry.Build(input);
            Require(plan.Segments.Count == 2, "hover visible segments");

            input = Base();
            input.ShowWidgets = true;
            plan = DynamicGeometry.Build(input);
            Require(plan.Segments.Count == 3 && Is(plan.Segments[2], 4, 4, 165, 49, 18)
                && Is(plan.PeekRanges[2], 4, 165), "widgets segment and peek range");

            input = Base();
            input.Dpi = 144;
            plan = DynamicGeometry.Build(input);
            Require(Is(plan.Segments[0], 6, 6, 361, 49, 27), "144 DPI app segment");
            Require(Is(plan.Segments[1], 1612, 6, 1915, 49, 27), "144 DPI tray segment");
            Require(Is(plan.PeekRanges[0], 6, 361) && Is(plan.PeekRanges[1], 1612, 1915), "144 DPI peek ranges");

            input = Base();
            input.Dpi = 0;
            plan = DynamicGeometry.Build(input);
            Require(!plan.AnyEnabled && plan.Segments.Count == 0, "invalid DPI rejected");
            input = Base();
            input.BarWidth = 0;
            plan = DynamicGeometry.Build(input);
            Require(!plan.AnyEnabled && plan.Segments.Count == 0, "invalid bar rejected");

            input = Base();
            input.AppListRight = 0;
            plan = DynamicGeometry.Build(input);
            Require(plan.AnyEnabled && plan.Segments.Count == 1 && plan.Segments[0].Left == 1614,
                "missing app extent does not create an inverted app segment");

            return "PASS: dynamic geometry at 96/144 DPI, app/tray/widgets, centred, margins, hover resting/visible, and invalid inputs";
        }

        internal static DynamicInput BaseInput()
        {
            return Base();
        }

        private static DynamicInput Base()
        {
            DynamicInput input = new DynamicInput();
            input.BarLeft = 0;
            input.BarWidth = 1920;
            input.BarHeight = 48;
            input.AppListRight = 363;
            input.TrayLeft = 1618;
            input.HasTray = true;
            input.Dpi = 96;
            input.Windows11 = true;
            input.ShowAppList = true;
            input.ShowTray = true;
            input.ShowSegmentsOnHover = false;
            input.TrayHoverEnabled = true;
            input.App = Layout(18, 4, 4, 0, 4);
            input.Tray = Layout(18, 4, 4, 0, 4);
            input.Widgets = Layout(18, 4, 4, 0, 4);
            return input;
        }

        private static SegmentLayout Layout(int radius, int top, int left, int bottom, int right)
        {
            SegmentLayout layout = new SegmentLayout();
            layout.CornerRadius = radius;
            layout.MarginTop = top;
            layout.MarginLeft = left;
            layout.MarginBottom = bottom;
            layout.MarginRight = right;
            return layout;
        }

        private static bool Is(Segment segment, int left, int top, int right, int bottom, int radius)
        {
            return segment.Left == left && segment.Top == top && segment.Right == right
                && segment.Bottom == bottom && segment.Radius == radius;
        }

        private static bool Is(SegmentRange range, int left, int right)
        {
            return range.Left == left && range.Right == right;
        }

        private static void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException(check);
        }
    }
}
