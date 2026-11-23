using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace TrayProbe
{
    internal sealed class RuntimeSettings
    {
        internal bool IsDynamic;
        internal bool IsCentred;
        internal bool ShowTray;
        internal bool ShowWidgets;
        internal bool CompositionCompat;
        internal bool FillOnMaximise;
        internal bool FillOnTaskSwitch;
        internal bool ShowSegmentsOnHover;
        internal bool? ShowAppList;
        internal bool? TrayHoverEnabled;
        internal int AutoHide;
        internal SegmentLayout Simple;
        internal SegmentLayout App;
        internal SegmentLayout Tray;
        internal SegmentLayout Widgets;

        internal bool ShouldFillOnTaskSwitch
        {
            get { return FillOnMaximise && FillOnTaskSwitch; }
        }

        internal static RuntimeSettings Load(string path)
        {
            try
            {
                return Parse(File.ReadAllText(path));
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static RuntimeSettings Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                Dictionary<string, object> root = serializer.DeserializeObject(json) as Dictionary<string, object>;
                if (root == null) return null;

                RuntimeSettings settings = new RuntimeSettings();
                settings.IsDynamic = Bool(root, "IsDynamic", false);
                settings.IsCentred = Bool(root, "IsCentred", false);
                settings.ShowTray = Bool(root, "ShowTray", false);
                settings.ShowWidgets = Bool(root, "ShowWidgets", false);
                settings.CompositionCompat = Bool(root, "CompositionCompat", false);
                settings.FillOnMaximise = Bool(root, "FillOnMaximise", true);
                settings.FillOnTaskSwitch = Bool(root, "FillOnTaskSwitch", true);
                settings.ShowSegmentsOnHover = Bool(root, "ShowSegmentsOnHover", false);
                settings.ShowAppList = NullableBool(root, "ShowAppList");
                settings.TrayHoverEnabled = NullableBool(root, "TrayHoverEnabled");
                settings.AutoHide = Int(root, "AutoHide", 0);
                settings.Simple = Layout(root, "SimpleTaskbarLayout");
                settings.App = Layout(root, "DynamicAppListLayout");
                settings.Tray = Layout(root, "DynamicTrayLayout");
                settings.Widgets = Layout(root, "DynamicWidgetsLayout");
                return settings;
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal DynamicInput ToDynamicInput(int appListRight, int trayLeft, bool hasTray)
        {
            DynamicInput input = new DynamicInput();
            input.AppListRight = appListRight;
            input.TrayLeft = trayLeft;
            input.HasTray = hasTray;
            input.Windows11 = true;
            input.IsCentred = IsCentred;
            input.ShowAppList = ShowAppList ?? true;
            input.ShowTray = ShowTray;
            input.ShowWidgets = ShowWidgets;
            input.ShowSegmentsOnHover = ShowSegmentsOnHover;
            input.AppListVisible = !ShowSegmentsOnHover;
            input.TrayHoverEnabled = TrayHoverEnabled ?? true;
            input.App = App;
            input.Tray = Tray;
            input.Widgets = Widgets;
            return input;
        }

        internal void ToggleTray()
        {
            if (IsDynamic && ShowSegmentsOnHover)
            {
                TrayHoverEnabled = !(TrayHoverEnabled ?? true);
            }
            else
            {
                ShowTray = !ShowTray;
            }
        }

        internal void ToggleAppList()
        {
            bool next = !(ShowAppList ?? true);
            ShowAppList = next;
            if (!next && !ShowTray && !ShowWidgets && !(TrayHoverEnabled ?? false))
            {
                if (ShowSegmentsOnHover)
                {
                    TrayHoverEnabled = true;
                }
                else
                {
                    ShowTray = true;
                }
            }
        }

        internal void ToggleWidgets()
        {
            ShowWidgets = !ShowWidgets;
            if (!ShowWidgets && !(ShowAppList ?? true) && !ShowTray && !(TrayHoverEnabled ?? false))
            {
                if (ShowSegmentsOnHover)
                {
                    TrayHoverEnabled = true;
                }
                else
                {
                    ShowTray = true;
                }
            }
        }

        internal bool Save(string path, out string error)
        {
            error = "";
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                Dictionary<string, object> root = null;
                if (File.Exists(path))
                {
                    root = serializer.DeserializeObject(File.ReadAllText(path)) as Dictionary<string, object>;
                }
                if (root == null) root = new Dictionary<string, object>();

                root["ShowAppList"] = ShowAppList ?? true;
                root["ShowTray"] = ShowTray;
                root["ShowWidgets"] = ShowWidgets;
                root["TrayHoverEnabled"] = TrayHoverEnabled ?? true;

                string json = serializer.Serialize(root);
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, json);
                try
                {
                    File.Replace(temporary, path, null);
                }
                catch (Exception)
                {
                    File.Copy(temporary, path, true);
                    File.Delete(temporary);
                }
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool Bool(Dictionary<string, object> root, string key, bool fallback)
        {
            object value;
            return root.TryGetValue(key, out value) && value is bool ? (bool)value : fallback;
        }

        private static bool? NullableBool(Dictionary<string, object> root, string key)
        {
            object value;
            if (!root.TryGetValue(key, out value) || !(value is bool)) return null;
            return (bool)value;
        }

        private static int Int(Dictionary<string, object> root, string key, int fallback)
        {
            object value;
            if (!root.TryGetValue(key, out value)) return fallback;
            if (value is int) return (int)value;
            if (value is long) return (int)(long)value;
            if (value is decimal) return (int)(decimal)value;
            return fallback;
        }

        private static SegmentLayout Layout(Dictionary<string, object> root, string key)
        {
            SegmentLayout layout = new SegmentLayout();
            object value;
            Dictionary<string, object> nested = root.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
            if (nested == null) return layout;
            layout.CornerRadius = Int(nested, "CornerRadius", 0);
            layout.MarginTop = Int(nested, "MarginTop", 0);
            layout.MarginLeft = Int(nested, "MarginLeft", 0);
            layout.MarginBottom = Int(nested, "MarginBottom", 0);
            layout.MarginRight = Int(nested, "MarginRight", 0);
            return layout;
        }
    }
}
