using System;

namespace TrayProbe
{
    internal static class HoverGeometry
    {
        internal struct HitInput
        {
            internal int Left, Top, Right, Bottom;
            internal int AppListRight;
            internal int TrayLeft;
            internal int WidgetsRight;
            internal bool HasTray;
        }

        internal struct HitResult
        {
            internal bool AppList, Tray, Widgets;
        }

        internal static HitResult Evaluate(HitInput input, int cursorX, int cursorY)
        {
            HitResult result = new HitResult();
            if (cursorX < input.Left || cursorX >= input.Right || cursorY < input.Top || cursorY >= input.Bottom)
            {
                return result;
            }

            if (input.AppListRight > input.Left && cursorX < input.AppListRight)
            {
                result.AppList = true;
            }
            if (input.HasTray && input.TrayLeft >= input.Left && input.TrayLeft < input.Right
                && cursorX >= input.TrayLeft)
            {
                result.Tray = true;
            }
            if (input.WidgetsRight > input.Left && cursorX < input.WidgetsRight)
            {
                result.Widgets = true;
            }
            return result;
        }

        internal static bool HasEnabledTarget(HitResult result, bool showAppList, bool trayHoverEnabled)
        {
            return (showAppList && (result.AppList || result.Widgets))
                || (trayHoverEnabled && result.Tray);
        }

        /// <summary>
        /// Hover mode with an enabled target whose window geometry is still
        /// unknown keeps Explorer's native edge instead of applying an empty
        /// region. Preferring a working reveal over a partial mask.
        /// </summary>
        internal static bool ShouldUseNativeEdgeFallback(bool hover, bool geometryReady)
        {
            return hover && !geometryReady;
        }

        internal static string Run()
        {
            HitInput input = new HitInput();
            input.Left = 0;
            input.Top = 0;
            input.Right = 1920;
            input.Bottom = 48;
            input.AppListRight = 451;
            input.TrayLeft = 1618;
            input.WidgetsRight = 168;
            input.HasTray = true;

            HitResult hit = Evaluate(input, 200, 24);
            Require(hit.AppList && !hit.Tray && !hit.Widgets, "app-only hover");
            Require(!HasEnabledTarget(hit, false, true), "disabled app list does not trigger hover");
            Require(HasEnabledTarget(hit, true, true), "enabled app list triggers hover");
            hit = Evaluate(input, 100, 24);
            Require(hit.AppList && hit.Widgets && !hit.Tray, "app and widgets overlap on hover");
            hit = Evaluate(input, 1000, 24);
            Require(!hit.AppList && !hit.Tray && !hit.Widgets, "gap has no hover target");
            hit = Evaluate(input, 1700, 24);
            Require(!hit.AppList && hit.Tray && !hit.Widgets, "tray hover");
            Require(HasEnabledTarget(hit, false, true), "tray remains independently hoverable when app list is disabled");
            Require(!HasEnabledTarget(hit, false, false), "disabled tray hover does not trigger");
            HitInput belowStrip = input;
            belowStrip.Bottom = 2;
            hit = Evaluate(belowStrip, 100, 3);
            Require(!hit.AppList && !hit.Tray && !hit.Widgets, "cursor below the hidden strip has no target");
            hit = Evaluate(input, 451, 24);
            Require(!hit.AppList, "app right edge is exclusive");
            hit = Evaluate(input, 1618, 24);
            Require(hit.Tray, "tray left edge is inclusive");
            hit = Evaluate(input, 1919, 24);
            Require(hit.Tray, "tray right edge is inside");
            hit = Evaluate(input, 1920, 24);
            Require(!hit.Tray, "right edge is exclusive");

            HitInput strip = input;
            strip.Bottom = 2;
            hit = Evaluate(strip, 100, 0);
            Require(hit.AppList && hit.Widgets, "hidden peek strip keeps app/widgets hittable");
            hit = Evaluate(strip, 1700, 0);
            Require(hit.Tray, "hidden peek strip keeps tray hittable");
            hit = Evaluate(strip, 1000, 0);
            Require(!hit.AppList && !hit.Tray && !hit.Widgets, "hidden strip gap stays empty");

            input.HasTray = false;
            hit = Evaluate(input, 1700, 24);
            Require(!hit.Tray, "no tray window means no tray target");

            Require(TaskbarTakeover.ShouldIncludePeek(AutoHideGeometry.State.Hidden,
                false, false, false), "static mode keeps its existing hidden peek");
            Require(ShouldUseNativeEdgeFallback(true, false),
                "unmeasured target geometry keeps the native edge reachable");
            Require(!ShouldUseNativeEdgeFallback(true, true),
                "measured geometry uses the segment regions");
            Require(!ShouldUseNativeEdgeFallback(false, false),
                "non-hover mode keeps its existing behavior");
            Require(!TaskbarTakeover.ShouldIncludePeek(AutoHideGeometry.State.Hidden,
                true, false, true), "non-target hover position gets no peek hit target");
            Require(TaskbarTakeover.ShouldIncludePeek(AutoHideGeometry.State.Hidden,
                true, true, true), "targeted hover position gets a peek only when inset requires it");
            Require(!TaskbarTakeover.ShouldIncludePeek(AutoHideGeometry.State.Hidden,
                true, true, false), "zero-inset target needs no synthetic peek");
            Require(!TaskbarTakeover.ShouldIncludePeek(AutoHideGeometry.State.Visible,
                true, true, true), "visible taskbar needs no hidden peek");
            Require(TaskbarTakeover.ShouldApplyEmptyHoverRegion(true, true,
                AutoHideGeometry.State.Hidden), "non-target hidden state cannot reveal the whole bar");
            Require(!TaskbarTakeover.ShouldApplyEmptyHoverRegion(true, true,
                AutoHideGeometry.State.Visible), "visible state preserves the current region during retract");
            Require(TaskbarTakeover.ShouldKeepVisibleHoverRegionForRetraction(true, true,
                AutoHideGeometry.State.Visible), "visible state keeps the last segment for native retract animation");
            Require(!TaskbarTakeover.ShouldKeepVisibleHoverRegionForRetraction(true, true,
                AutoHideGeometry.State.Hidden), "hidden state removes the stale segment trigger");
            Require(!TaskbarTakeover.ShouldApplyEmptyHoverRegion(false, true,
                AutoHideGeometry.State.Visible), "enabled segment is applied under the cursor");
            Require(!TaskbarTakeover.ShouldApplyEmptyHoverRegion(true, false,
                AutoHideGeometry.State.Visible), "non-hover mode keeps its existing behavior");

            return "PASS: hover hit areas, exclusive edges, peek gating, margins, gaps, missing tray, and segment-only reveal";
        }

        private static void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException(check);
        }
    }
}
