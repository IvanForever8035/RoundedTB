using System;
using System.Runtime.InteropServices;

namespace TrayProbe
{
    internal static class SimpleRegion
    {
        internal struct Bounds
        {
            internal int Left, Top, Right, Bottom, Radius;
        }

        internal static bool TryCalculate(int width, int height, uint dpi,
            int left, int top, int right, int bottom, int radius, out Bounds bounds)
        {
            bounds = new Bounds();
            if (width <= 0 || height <= 0 || dpi == 0 || radius < 0)
            {
                return false;
            }

            double scale = dpi / 96.0;
            try
            {
                bounds.Left = Convert.ToInt32(left * scale);
                bounds.Top = Convert.ToInt32(top * scale);
                // Match RoundedTB's DPI rounding and inclusive edge adjustment.
                bounds.Right = checked(Convert.ToInt32(width - right * scale) + 1);
                bounds.Bottom = checked(Convert.ToInt32(height - bottom * scale) + 1);
                bounds.Radius = Convert.ToInt32(radius * scale);
            }
            catch (OverflowException)
            {
                return false;
            }

            return bounds.Right > bounds.Left && bounds.Bottom > bounds.Top;
        }

        // The prototype may only change its own window. It must not take
        // ownership of the real taskbar or interfere with the running app.
        private static bool IsOwnWindow(IntPtr hwnd)
        {
            uint processId;
            return hwnd != IntPtr.Zero && GetWindowThreadProcessId(hwnd, out processId) != 0
                && processId == GetCurrentProcessId();
        }

        internal static IntPtr BuildRegion(Bounds bounds, bool includePeek, AutoHideGeometry.Rect peek)
        {
            if (bounds.Right <= bounds.Left || bounds.Bottom <= bounds.Top || bounds.Radius < 0)
            {
                return IntPtr.Zero;
            }

            IntPtr region = CreateRoundRectRgn(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom,
                bounds.Radius, bounds.Radius);
            if (region == IntPtr.Zero || !includePeek)
            {
                return region;
            }

            IntPtr strip = CreateRectRgn(peek.Left, peek.Top, peek.Right, peek.Bottom);
            if (strip == IntPtr.Zero)
            {
                DeleteObject(region);
                return IntPtr.Zero;
            }

            try
            {
                const int RgnOr = 2;
                if (CombineRgn(region, region, strip, RgnOr) <= 1)
                {
                    DeleteObject(region);
                    return IntPtr.Zero;
                }
                return region;
            }
            finally
            {
                DeleteObject(strip);
            }
        }

        private static bool TryApply(IntPtr hwnd, Bounds bounds)
        {
            return TryApply(hwnd, bounds, false, new AutoHideGeometry.Rect());
        }

        private static bool TryApply(IntPtr hwnd, Bounds bounds, bool includePeek, AutoHideGeometry.Rect peek)
        {
            if (!IsOwnWindow(hwnd) || bounds.Right <= bounds.Left || bounds.Bottom <= bounds.Top || bounds.Radius < 0)
            {
                return false;
            }

            IntPtr region = CreateRoundRectRgn(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom,
                bounds.Radius, bounds.Radius);
            if (region == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                if (includePeek)
                {
                    IntPtr strip = CreateRectRgn(peek.Left, peek.Top, peek.Right, peek.Bottom);
                    if (strip == IntPtr.Zero)
                    {
                        return false;
                    }
                    try
                    {
                        const int RgnOr = 2;
                        if (CombineRgn(region, region, strip, RgnOr) <= 1)
                        {
                            return false;
                        }
                    }
                    finally
                    {
                        DeleteObject(strip);
                    }
                }

                if (SetWindowRgn(hwnd, region, false) == 0)
                {
                    return false;
                }

                region = IntPtr.Zero; // Windows now owns the region.
                return true;
            }
            finally
            {
                if (region != IntPtr.Zero) DeleteObject(region);
            }
        }

        private enum RegionUpdate { Preserved, Applied, Failed }

        private static RegionUpdate ApplyForPosition(IntPtr hwnd, Bounds bounds, AutoHideGeometry.Rect window,
            AutoHideGeometry.Rect monitor, uint dpi)
        {
            AutoHideGeometry.Peek peek;
            AutoHideGeometry.State state = AutoHideGeometry.Classify(window, monitor, dpi, out peek);
            if (state != AutoHideGeometry.State.Visible && state != AutoHideGeometry.State.Hidden)
            {
                return RegionUpdate.Preserved;
            }

            bool includePeek = state == AutoHideGeometry.State.Hidden && AutoHideGeometry.HasInsetAtPeekEdge(bounds, peek);
            return TryApply(hwnd, bounds, includePeek, peek.Bounds) ? RegionUpdate.Applied : RegionUpdate.Failed;
        }

        private static bool TryClear(IntPtr hwnd)
        {
            return IsOwnWindow(hwnd) && SetWindowRgn(hwnd, IntPtr.Zero, false) != 0;
        }

        internal static string TestHiddenWindow(IntPtr hwnd)
        {
            Bounds bounds;
            Require(TryCalculate(800, 48, 96, 5, 4, 7, 3, 18, out bounds)
                && Matches(bounds, 5, 4, 794, 46, 18), "96 DPI bounds");
            Require(TryCalculate(800, 60, 120, 2, 2, 2, 2, 2, out bounds)
                && Matches(bounds, 2, 2, 799, 59, 2), "120 DPI midpoint rounding");
            Require(TryCalculate(800, 60, 144, -2, -2, -4, -4, 18, out bounds)
                && Matches(bounds, -3, -3, 807, 67, 27), "negative margins at 144 DPI");
            Require(!TryCalculate(10, 10, 96, 8, 0, 8, 0, 2, out bounds), "empty region rejection");
            Require(!TryCalculate(800, 60, 0, 0, 0, 0, 0, 0, out bounds), "invalid DPI rejection");
            Require(!TryCalculate(int.MaxValue, 60, 96, 0, 0, 0, 0, 0, out bounds), "coordinate overflow rejection");
            Require(TryCalculate(800, 48, 96, 5, 4, 7, 3, 18, out bounds), "test window bounds");
            Require(!TryApply(IntPtr.Zero, bounds), "invalid window rejection");

            IntPtr scratch = CreateRectRgn(0, 0, 0, 0);
            Require(scratch != IntPtr.Zero, "allocate readback region");
            try
            {
                Require(TryApply(hwnd, bounds), "apply round region");
                Require(GetWindowRgn(hwnd, scratch) > 1, "read back nonempty window region");
                Require(PtInRegion(scratch, 400, 24), "centre stays visible");
                Require(!PtInRegion(scratch, 0, 24), "left margin is excluded");
                Require(!PtInRegion(scratch, 5, 4), "rounded corner is excluded");
                Require(!TryApply(hwnd, new Bounds()), "empty region was not applied");
                Require(GetWindowRgn(hwnd, scratch) > 1 && PtInRegion(scratch, 400, 24), "invalid input preserves current region");
                Require(TryClear(hwnd) && GetWindowRgn(hwnd, scratch) == 0, "clearing restores the default window shape");

                uint before = GetGuiResources(GetCurrentProcess(), 0);
                for (int i = 0; i < 200; i++)
                {
                    Require(TryApply(hwnd, bounds) && TryClear(hwnd), "repeat apply and clear");
                }
                uint after = GetGuiResources(GetCurrentProcess(), 0);
                Require(before != 0 && before == after, "GDI object count before/after the apply-clear loop");
                return "PASS: geometry at 96/120/144 DPI, region readback, reset, and 200 apply-clear cycles; GDI "
                    + before + " -> " + after;
            }
            finally
            {
                TryClear(hwnd);
                DeleteObject(scratch);
            }
        }

        internal static string TestAutoHide(IntPtr hwnd)
        {
            var screen = new AutoHideGeometry.Rect(0, 0, 800, 600);
            var visible = new AutoHideGeometry.Rect(0, 552, 800, 600);
            var hidden = new AutoHideGeometry.Rect(0, 598, 800, 646);
            var sliding = new AutoHideGeometry.Rect(0, 580, 800, 628);
            var offScreen = new AutoHideGeometry.Rect(0, 600, 800, 648);
            AutoHideGeometry.Peek peek;
            Require(AutoHideGeometry.Classify(visible, screen, 96, out peek) == AutoHideGeometry.State.Visible,
                "visible taskbar classification");
            Require(AutoHideGeometry.Classify(new AutoHideGeometry.Rect(-2, 552, 802, 600), screen, 96, out peek)
                == AutoHideGeometry.State.Visible, "two-pixel screen tolerance");
            Require(AutoHideGeometry.Classify(hidden, screen, 96, out peek) == AutoHideGeometry.State.Hidden
                && PeekMatches(peek, AutoHideGeometry.Edge.Bottom, 0, 0, 800, 2), "bottom peek coordinates");
            Require(AutoHideGeometry.Classify(new AutoHideGeometry.Rect(0, -46, 800, 2), screen, 96, out peek)
                == AutoHideGeometry.State.Hidden && PeekMatches(peek, AutoHideGeometry.Edge.Top, 0, 46, 800, 48),
                "top peek coordinates");
            Require(AutoHideGeometry.Classify(new AutoHideGeometry.Rect(-1966, -190, -1918, 870),
                new AutoHideGeometry.Rect(-1920, -200, 0, 880), 96, out peek) == AutoHideGeometry.State.Hidden
                && PeekMatches(peek, AutoHideGeometry.Edge.Left, 46, 0, 48, 1060), "left peek on a negative-coordinate monitor");
            Require(AutoHideGeometry.Classify(new AutoHideGeometry.Rect(798, 0, 846, 600), screen, 96, out peek)
                == AutoHideGeometry.State.Hidden && PeekMatches(peek, AutoHideGeometry.Edge.Right, 0, 0, 2, 600),
                "right peek coordinates");
            Require(AutoHideGeometry.Classify(new AutoHideGeometry.Rect(0, 596, 800, 644), screen, 96, out peek)
                == AutoHideGeometry.State.Hidden, "96 DPI peek threshold is four pixels");
            var sixPixels = new AutoHideGeometry.Rect(0, 594, 800, 642);
            Require(AutoHideGeometry.Classify(sixPixels, screen, 96, out peek) == AutoHideGeometry.State.Transitioning
                && AutoHideGeometry.Classify(sixPixels, screen, 144, out peek) == AutoHideGeometry.State.Hidden,
                "peek threshold follows DPI");
            Require(AutoHideGeometry.Classify(offScreen, screen, 96, out peek) == AutoHideGeometry.State.OffScreen,
                "fully off-screen classification");
            Require(AutoHideGeometry.Classify(hidden, new AutoHideGeometry.Rect(), 96, out peek)
                == AutoHideGeometry.State.Invalid, "invalid monitor classification");

            Bounds inset;
            Bounds noInset;
            Require(TryCalculate(800, 48, 96, 5, 4, 7, 3, 18, out inset), "inset region bounds");
            Require(TryCalculate(800, 48, 96, 5, 0, 7, 3, 18, out noInset), "zero top inset bounds");
            IntPtr scratch = CreateRectRgn(0, 0, 0, 0);
            Require(scratch != IntPtr.Zero, "allocate auto-hide readback region");
            try
            {
                Require(ApplyForPosition(hwnd, inset, visible, screen, 96) == RegionUpdate.Applied
                    && GetWindowRgn(hwnd, scratch) > 1 && !PtInRegion(scratch, 1, 0), "visible region has no peek strip");
                Require(ApplyForPosition(hwnd, inset, sliding, screen, 96) == RegionUpdate.Preserved
                    && GetWindowRgn(hwnd, scratch) > 1 && !PtInRegion(scratch, 1, 0), "sliding out leaves the visible region alone");
                Require(ApplyForPosition(hwnd, inset, hidden, screen, 96) == RegionUpdate.Applied
                    && GetWindowRgn(hwnd, scratch) > 1 && PtInRegion(scratch, 1, 0), "hidden inset region has hit-test pixels");
                Require(!PtInRegion(scratch, 400, 2) && PtInRegion(scratch, 400, 24), "peek strip does not fill the gap");
                Require(ApplyForPosition(hwnd, inset, sliding, screen, 96) == RegionUpdate.Preserved
                    && ApplyForPosition(hwnd, inset, offScreen, screen, 96) == RegionUpdate.Preserved
                    && GetWindowRgn(hwnd, scratch) > 1 && PtInRegion(scratch, 1, 0), "slide in and off-screen keep the peek region");
                Require(ApplyForPosition(hwnd, noInset, hidden, screen, 96) == RegionUpdate.Applied
                    && GetWindowRgn(hwnd, scratch) > 1 && PtInRegion(scratch, 400, 0)
                    && !PtInRegion(scratch, 1, 0), "zero top inset uses the rounded region's own edge");
                Require(TryClear(hwnd), "restore default shape before the loop");

                uint before = GetGuiResources(GetCurrentProcess(), 0);
                for (int i = 0; i < 200; i++)
                {
                    Require(ApplyForPosition(hwnd, inset, hidden, screen, 96) == RegionUpdate.Applied
                        && ApplyForPosition(hwnd, inset, visible, screen, 96) == RegionUpdate.Applied
                        && TryClear(hwnd), "repeated peek merge and reset");
                }
                uint after = GetGuiResources(GetCurrentProcess(), 0);
                Require(before != 0 && before == after, "auto-hide GDI object count before/after the loop");
                return "PASS: auto-hide positions, four edges, DPI, native peek hit tests, and 200 merge/reset cycles; GDI "
                    + before + " -> " + after;
            }
            finally
            {
                TryClear(hwnd);
                DeleteObject(scratch);
            }
        }

        private static bool PeekMatches(AutoHideGeometry.Peek peek, AutoHideGeometry.Edge side,
            int left, int top, int right, int bottom)
        {
            return peek.Side == side && peek.Bounds.Left == left && peek.Bounds.Top == top
                && peek.Bounds.Right == right && peek.Bounds.Bottom == bottom;
        }

        private static bool Matches(Bounds b, int left, int top, int right, int bottom, int radius)
        {
            return b.Left == left && b.Top == top && b.Right == right && b.Bottom == bottom && b.Radius == radius;
        }

        private static void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException(check);
        }

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);

        [DllImport("user32.dll")]
        private static extern int GetWindowRgn(IntPtr hwnd, IntPtr region);

        [DllImport("user32.dll")]
        private static extern uint GetGuiResources(IntPtr process, uint flags);

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
