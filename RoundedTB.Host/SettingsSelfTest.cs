using System;

namespace TrayProbe
{
    internal static class SettingsSelfTest
    {
        internal static string Run()
        {
            const string json = "{\"IsDynamic\":true,\"IsCentred\":false,\"ShowTray\":false,\"ShowWidgets\":false,\"ShowAppList\":false,\"TrayHoverEnabled\":true,\"CompositionCompat\":false,\"FillOnMaximise\":true,\"FillOnTaskSwitch\":true,\"ShowSegmentsOnHover\":true,\"AutoHide\":0,"
                + "\"SimpleTaskbarLayout\":{\"CornerRadius\":1,\"MarginTop\":2,\"MarginLeft\":3,\"MarginBottom\":4,\"MarginRight\":5},"
                + "\"DynamicAppListLayout\":{\"CornerRadius\":6,\"MarginTop\":7,\"MarginLeft\":8,\"MarginBottom\":9,\"MarginRight\":10},"
                + "\"DynamicTrayLayout\":{\"CornerRadius\":11,\"MarginTop\":12,\"MarginLeft\":13,\"MarginBottom\":14,\"MarginRight\":15},"
                + "\"DynamicWidgetsLayout\":{\"CornerRadius\":16,\"MarginTop\":17,\"MarginLeft\":18,\"MarginBottom\":19,\"MarginRight\":20}}";

            RuntimeSettings settings = RuntimeSettings.Parse(json);
            Require(settings != null, "parse valid settings");
            Require(settings.IsDynamic && !settings.IsCentred && !settings.ShowTray && !settings.ShowWidgets
                && !settings.CompositionCompat && settings.FillOnMaximise && settings.FillOnTaskSwitch
                && settings.ShouldFillOnTaskSwitch && settings.ShowSegmentsOnHover && settings.AutoHide == 0,
                "parse scalar settings");
            Require(settings.ShowAppList == false && settings.TrayHoverEnabled == true, "parse nullable settings");
            Require(Layout(settings.Simple, 1, 2, 3, 4, 5), "parse simple layout");
            Require(Layout(settings.App, 6, 7, 8, 9, 10), "parse app layout");
            Require(Layout(settings.Tray, 11, 12, 13, 14, 15), "parse tray layout");
            Require(Layout(settings.Widgets, 16, 17, 18, 19, 20), "parse widgets layout");

            DynamicInput input = settings.ToDynamicInput(451, 1618, true);
            Require(input.AppListRight == 451 && input.TrayLeft == 1618 && input.HasTray
                && input.ShowAppList == false && input.ShowTray == false && input.ShowWidgets == false
                && input.ShowSegmentsOnHover && !input.AppListVisible && input.TrayHoverEnabled,
                "dynamic input from settings");
            Require(input.App.CornerRadius == 6 && input.Tray.MarginTop == 12 && input.Widgets.MarginRight == 20,
                "layouts copied to dynamic input");

            settings.ToggleTray();
            Require(settings.TrayHoverEnabled == false, "hover tray toggle disables hover");
            settings.ToggleTray();
            Require(settings.TrayHoverEnabled == true, "hover tray toggle re-enables hover");

            settings.ToggleAppList();
            Require(settings.ShowAppList == true, "app list toggle enables the segment");
            settings.ToggleAppList();
            Require(settings.ShowAppList == false && settings.TrayHoverEnabled == true,
                "app list fallback leaves a hittable tray on hover");

            RuntimeSettings nonHover = RuntimeSettings.Parse("{\"IsDynamic\":true,\"ShowSegmentsOnHover\":false,\"ShowTray\":false,\"ShowAppList\":false}");
            Require(nonHover != null, "parse non-hover settings");
            nonHover.ToggleTray();
            Require(nonHover.ShowTray, "non-hover tray toggle enables the tray");
            nonHover.ToggleTray();
            Require(!nonHover.ShowTray, "non-hover tray toggle disables the tray");
            nonHover.ToggleAppList();
            Require(nonHover.ShowAppList == true && nonHover.ShowTray == false, "non-hover app list toggle");

            RuntimeSettings defaults = RuntimeSettings.Parse("{\"IsDynamic\":true}");
            Require(defaults != null, "parse minimal settings");
            Require(defaults.FillOnMaximise && defaults.FillOnTaskSwitch && defaults.ShouldFillOnTaskSwitch,
                "legacy fill settings default to WPF Windows 11 defaults");
            RuntimeSettings disabledFill = RuntimeSettings.Parse("{\"FillOnMaximise\":false,\"FillOnTaskSwitch\":true}");
            Require(disabledFill != null && !disabledFill.ShouldFillOnTaskSwitch,
                "task-switch fill depends on maximise fill");
            DynamicInput defaultInput = defaults.ToDynamicInput(0, 0, false);
            Require(defaultInput.ShowAppList && defaultInput.TrayHoverEnabled && defaultInput.AppListVisible,
                "nullable settings default to visible");

            Require(RuntimeSettings.Parse("{bad") == null, "reject malformed JSON");
            Require(RuntimeSettings.Parse("") == null, "reject empty JSON");

            return "PASS: settings parse, fill defaults, layouts, dynamic input, hover/non-hover hotkey toggles, fallbacks, and malformed JSON";
        }

        private static bool Layout(SegmentLayout layout, int radius, int top, int left, int bottom, int right)
        {
            return layout.CornerRadius == radius && layout.MarginTop == top && layout.MarginLeft == left
                && layout.MarginBottom == bottom && layout.MarginRight == right;
        }

        private static void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException(check);
        }
    }
}
