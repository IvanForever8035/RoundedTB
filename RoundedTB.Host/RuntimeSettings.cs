using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

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
                Dictionary<string, object> root = DeserializeRoot(json);
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
            bool appNeeded = input.ShowAppList;
            bool trayNeeded = ShowSegmentsOnHover ? input.TrayHoverEnabled : ShowTray;
            input.GeometryReady = (!appNeeded || appListRight > 0)
                && (!trayNeeded || (hasTray && trayLeft != 0));
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
                Dictionary<string, object> root = null;
                if (File.Exists(path))
                {
                    root = DeserializeRoot(File.ReadAllText(path));
                }
                if (root == null) root = new Dictionary<string, object>();

                root["ShowAppList"] = ShowAppList ?? true;
                root["ShowTray"] = ShowTray;
                root["ShowWidgets"] = ShowWidgets;
                root["TrayHoverEnabled"] = TrayHoverEnabled ?? true;

                string json = JsonSerializer.Serialize(root);
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

        private static Dictionary<string, object> DeserializeRoot(string json)
        {
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
                return ConvertObject(document.RootElement) as Dictionary<string, object>;
            }
        }

        private static object ConvertObject(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    Dictionary<string, object> dictionary = new Dictionary<string, object>();
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        dictionary[property.Name] = ConvertObject(property.Value);
                    }
                    return dictionary;
                case JsonValueKind.Array:
                    List<object> list = new List<object>();
                    foreach (JsonElement item in element.EnumerateArray()) list.Add(ConvertObject(item));
                    return list;
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Number:
                    int intValue;
                    if (element.TryGetInt32(out intValue)) return intValue;
                    long longValue;
                    if (element.TryGetInt64(out longValue)) return longValue;
                    decimal decimalValue;
                    if (element.TryGetDecimal(out decimalValue)) return decimalValue;
                    return element.GetDouble();
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;
                default:
                    return null;
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
