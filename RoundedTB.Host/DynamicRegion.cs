using System;
using System.Runtime.InteropServices;

namespace TrayProbe
{
    internal static class DynamicRegion
    {
        internal static IntPtr Build(DynamicPlan plan, bool includePeek, AutoHideGeometry.Rect peek, out string error)
        {
            error = "";
            IntPtr result = IntPtr.Zero;

            foreach (Segment segment in plan.Segments)
            {
                IntPtr part = CreateRoundRectRgn(segment.Left, segment.Top, segment.Right, segment.Bottom,
                    segment.Radius, segment.Radius);
                if (part == IntPtr.Zero)
                {
                    error = "cannot create segment region";
                    DeleteObject(result);
                    return IntPtr.Zero;
                }

                if (result == IntPtr.Zero)
                {
                    result = part;
                    continue;
                }

                const int RgnOr = 2;
                int combined = CombineRgn(result, result, part, RgnOr);
                DeleteObject(part);
                if (combined <= 1)
                {
                    error = "cannot combine segment regions";
                    DeleteObject(result);
                    return IntPtr.Zero;
                }
            }

            if (!includePeek)
            {
                return result;
            }

            bool addedPeek = false;
            foreach (SegmentRange range in plan.PeekRanges)
            {
                int left = Math.Max(range.Left, peek.Left);
                int right = Math.Min(range.Right, peek.Right);
                if (right <= left || peek.Bottom <= peek.Top)
                {
                    continue;
                }

                if (!UnionRect(ref result, left, peek.Top, right, peek.Bottom))
                {
                    error = "cannot combine peek strip";
                    DeleteObject(result);
                    return IntPtr.Zero;
                }
                addedPeek = true;
            }

            if (!addedPeek && !plan.AnyEnabled
                && !UnionRect(ref result, peek.Left, peek.Top, peek.Right, peek.Bottom))
            {
                error = "cannot combine fallback peek strip";
                DeleteObject(result);
                return IntPtr.Zero;
            }

            return result;
        }

        private static bool UnionRect(ref IntPtr region, int left, int top, int right, int bottom)
        {
            if (right <= left || bottom <= top)
            {
                return true;
            }

            IntPtr strip = CreateRectRgn(left, top, right, bottom);
            if (strip == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                if (region == IntPtr.Zero)
                {
                    region = strip;
                    strip = IntPtr.Zero;
                    return true;
                }

                const int RgnOr = 2;
                return CombineRgn(region, region, strip, RgnOr) > 1;
            }
            finally
            {
                if (strip != IntPtr.Zero) DeleteObject(strip);
            }
        }

        internal static string TestHiddenWindow(IntPtr hwnd)
        {
            DynamicInput input = DynamicSelfTest.BaseInput();
            DynamicPlan plan = DynamicGeometry.Build(input);
            Require(plan.Segments.Count == 2, "test plan has app and tray segments");

            IntPtr scratch = CreateRectRgn(0, 0, 0, 0);
            Require(scratch != IntPtr.Zero, "allocate dynamic readback region");
            try
            {
                string error;
                IntPtr region = Build(plan, false, new AutoHideGeometry.Rect(), out error);
                Require(region != IntPtr.Zero, "build visible dynamic region");
                Require(SetWindowRgn(hwnd, region, false) != 0, "apply visible dynamic region");
                Require(GetWindowRgn(hwnd, scratch) > 1, "read back dynamic window region");
                Require(PtInRegion(scratch, 100, 24), "app segment is visible");
                Require(!PtInRegion(scratch, 1000, 24), "gap between segments is hidden");
                Require(PtInRegion(scratch, 1700, 24), "tray segment is visible");
                Require(!PtInRegion(scratch, 100, 3), "top margin is excluded");
                Require(!PtInRegion(scratch, 100, 0), "visible state has no peek strip");

                var peek = new AutoHideGeometry.Rect(0, 0, 1920, 2);
                region = Build(plan, true, peek, out error);
                Require(region != IntPtr.Zero, "build hidden dynamic region with peek");
                Require(SetWindowRgn(hwnd, region, false) != 0, "apply hidden dynamic region");
                Require(GetWindowRgn(hwnd, scratch) > 1, "read back hidden dynamic region");
                Require(PtInRegion(scratch, 100, 0) && PtInRegion(scratch, 1700, 0), "peek strip under both segments");
                Require(!PtInRegion(scratch, 1000, 0), "peek strip does not fill the gap");
                Require(!PtInRegion(scratch, 100, 2), "peek strip stays below the top margin");

                input = DynamicSelfTest.BaseInput();
                input.ShowSegmentsOnHover = true;
                input.AppListVisible = false;
                input.ShowTray = false;
                DynamicPlan hover = DynamicGeometry.Build(input);
                Require(hover.Segments.Count == 0 && hover.PeekRanges.Count > 0, "hover resting plan");
                region = Build(hover, true, peek, out error);
                Require(region != IntPtr.Zero, "build hover peek region");
                Require(SetWindowRgn(hwnd, region, false) != 0, "apply hover peek region");
                Require(GetWindowRgn(hwnd, scratch) > 1, "read back hover peek region");
                Require(PtInRegion(scratch, 100, 0) && PtInRegion(scratch, 1700, 0) && !PtInRegion(scratch, 1000, 0),
                    "hover peek strips stay under the enabled ranges");

                input.ShowAppList = false;
                input.ShowTray = false;
                input.ShowWidgets = false;
                input.ShowSegmentsOnHover = false;
                input.TrayHoverEnabled = false;
                DynamicPlan none = DynamicGeometry.Build(input);
                region = Build(none, true, peek, out error);
                Require(region != IntPtr.Zero, "build full-width fallback peek");
                Require(SetWindowRgn(hwnd, region, false) != 0, "apply full-width fallback peek");
                Require(GetWindowRgn(hwnd, scratch) > 1 && PtInRegion(scratch, 1000, 0), "fallback peek spans the taskbar");

                Require(SetWindowRgn(hwnd, IntPtr.Zero, false) != 0, "clear dynamic region before loop");
                uint before = GetGuiResources(GetCurrentProcess(), 0);
                for (int i = 0; i < 200; i++)
                {
                    region = Build(plan, (i & 1) == 0, peek, out error);
                    Require(region != IntPtr.Zero && SetWindowRgn(hwnd, region, false) != 0, "repeated dynamic apply");
                }
                Require(SetWindowRgn(hwnd, IntPtr.Zero, false) != 0, "clear dynamic region after loop");
                uint after = GetGuiResources(GetCurrentProcess(), 0);
                Require(before != 0 && before == after, "dynamic GDI object count before/after the loop");
                return "PASS: dynamic hit testing, multi-segment gaps, hover/fallback peek, and 200 apply cycles; GDI "
                    + before + " -> " + after;
            }
            finally
            {
                SetWindowRgn(hwnd, IntPtr.Zero, false);
                DeleteObject(scratch);
            }
        }

        private static void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException(check);
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("user32.dll")]
        private static extern uint GetGuiResources(IntPtr process, uint flags);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);

        [DllImport("user32.dll")]
        private static extern int GetWindowRgn(IntPtr hwnd, IntPtr region);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int ellipseWidth, int ellipseHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

        [DllImport("gdi32.dll")]
        private static extern int CombineRgn(IntPtr destination, IntPtr first, IntPtr second, int mode);

        [DllImport("gdi32.dll")]
        private static extern bool PtInRegion(IntPtr region, int x, int y);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr obj);
    }
}
