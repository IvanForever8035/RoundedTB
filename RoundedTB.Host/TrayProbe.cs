using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using Microsoft.Win32;

namespace TrayProbe
{
    internal static class Program
    {
        private const string ClassName = "RoundedTBHostWindow";
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValue = "RoundedTB-Light";
        private const uint WM_APP = 0x8000;
        private const uint WM_TRAY = WM_APP + 1;
        private const uint WM_TIMER = 0x0113;
        private const uint WM_COMMAND = 0x0111;
        private const uint WM_DESTROY = 0x0002;
        private const uint WM_CLOSE = 0x0010;
        private const uint WM_ENDSESSION = 0x0016;
        private const uint WM_SHOW_SETTINGS = WM_APP + 2;
        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint WM_DISPLAYCHANGE = 0x007E;
        private const uint WM_POWERBROADCAST = 0x0218;
        private const uint WM_DPICHANGED = 0x02E0;
        private const uint WM_INPUT = 0x00FF;
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint WM_CONTEXTMENU = 0x007B;
        private const uint MF_STRING = 0x0000;
        private const uint MF_CHECKED = 0x0008;
        private const uint MF_SEPARATOR = 0x0800;
        private const uint TPM_RIGHTBUTTON = 0x0002;
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint NIF_MESSAGE = 0x1;
        private const uint NIF_ICON = 0x2;
        private const uint NIF_TIP = 0x4;
        private const uint NIM_ADD = 0;
        private const uint NIM_DELETE = 2;
        private const int IDI_APPLICATION = 32512;
        private const uint WS_EX_TOOLWINDOW = 0x80;
        private const uint WS_EX_NOACTIVATE = 0x08000000;
        private const uint WS_POPUP = 0x80000000;

        private static IntPtr hwnd;
        private static IntPtr trayIcon;
        private static bool trayIconOwned;
        private static WndProcDelegate wndProcDelegate;
        private static bool useUia;
        private static bool useLog;
        private static DateTime lastUiaUtc = DateTime.MinValue;
        private static DateTime lastLogUtc = DateTime.MinValue;
        private static bool takeoverMode;
        private static int takeoverSeconds;
        private static DateTime takeoverStartedUtc;
        private static RuntimeSettings activeSettings;
        private static readonly List<TaskbarEntry> taskbars = new List<TaskbarEntry>();
        private static int taskbarCreatedMessage;
        private static bool rebindPending;
        private static DateTime rebindAtUtc;
        private static int rebindAttempts;
        private static DateTime lastHandleCheckUtc;
        private static bool currentSimpleMode;
        private static int simpleTop, simpleLeft, simpleBottom, simpleRight, simpleRadius;
        private static string settingsPath;
        private static int shellHookMessage;
        private static bool appMeasurePending;
        private static DateTime appMeasureAtUtc;
        private static string settingsExePath;
        private static FileSystemWatcher settingsWatcher;
        private static readonly object settingsLock = new object();
        private static bool settingsReloadPending;
        private static DateTime settingsReloadAtUtc;
        private static readonly Debouncer autoHideAlign = new Debouncer();
        private static bool shutdownRestored;
        private static Mutex instanceMutex;
        private static EventWaitHandle showSettingsEvent;

        private sealed class TaskbarEntry
        {
            internal IntPtr Hwnd;
            internal int AppListRight;
            internal int TrayLeft;
            internal bool HasTray;
            internal AutoHideRecord AutoHide;
        }

        [STAThread]
        internal static int Run(string[] args)
        {
            bool testAutoHide = HasArg(args, "/autohide-self-test");
            bool testDynamicRegion = HasArg(args, "/dynamic-region-self-test");
            bool testRegion = HasArg(args, "/region-self-test") || testAutoHide || testDynamicRegion;
            bool dynamicTakeover = HasArg(args, "/dynamic");
            bool explicitTest = dynamicTakeover
                || HasArg(args, "/takeover")
                || HasArg(args, "/dynamic-self-test")
                || HasArg(args, "/hover-self-test")
                || HasArg(args, "/settings-self-test")
                || HasArg(args, "/rebind-self-test")
                || HasArg(args, "/entrypoint-self-test")
                || testRegion;
            bool settingsTakeover = HasArg(args, "/settings") || !explicitTest;
            takeoverMode = HasArg(args, "/takeover") || dynamicTakeover || settingsTakeover;
            takeoverSeconds = ParseIntArg(args, "/seconds:", 0);
            useUia = HasArg(args, "/uia");
            useLog = HasArg(args, "/log");
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            settingsExePath = Process.GetCurrentProcess().MainModule.FileName;
            if (useLog) Log(baseDir, "started uia=" + useUia);
            if (HasArg(args, "/dynamic-self-test"))
            {
                try
                {
                    Log(baseDir, DynamicSelfTest.Run());
                    return 0;
                }
                catch (Exception error)
                {
                    Log(baseDir, "FAIL: " + error.Message);
                    return 5;
                }
            }
            if (HasArg(args, "/hover-self-test"))
            {
                try
                {
                    Log(baseDir, HoverGeometry.Run());
                    return 0;
                }
                catch (Exception error)
                {
                    Log(baseDir, "FAIL: " + error.Message);
                    return 6;
                }
            }
            if (HasArg(args, "/settings-self-test"))
            {
                try
                {
                    Log(baseDir, SettingsSelfTest.Run());
                    return 0;
                }
                catch (Exception error)
                {
                    Log(baseDir, "FAIL: " + error.Message);
                    return 7;
                }
            }
            if (HasArg(args, "/rebind-self-test"))
            {
                try
                {
                    Log(baseDir, RebindSelfTest.Run());
                    return 0;
                }
                catch (Exception error)
                {
                    Log(baseDir, "FAIL: " + error.Message);
                    return 9;
                }
            }
            if (HasArg(args, "/taskbar-model-self-test"))
            {
                try
                {
                    Log(baseDir, TaskbarModelSelfTest.Run());
                    return 0;
                }
                catch (Exception error)
                {
                    Log(baseDir, "FAIL: " + error.Message);
                    return 10;
                }
            }
            if (HasArg(args, "/autohide-policy-self-test"))
            {
                try
                {
                    Log(baseDir, AutoHidePolicySelfTest.Run());
                    return 0;
                }
                catch (Exception error)
                {
                    Log(baseDir, "FAIL: " + error.Message);
                    return 11;
                }
            }
            if (HasArg(args, "/entrypoint-self-test"))
            {
                try
                {
                    Log(baseDir, RoundedTB.LightEntryPointSelfTest.Run());
                    return 0;
                }
                catch (Exception error)
                {
                    Log(baseDir, "FAIL: " + error.Message);
                    return 12;
                }
            }
            if (testAutoHide || takeoverMode) SetProcessDpiAwarenessContext(new IntPtr(-4)); // Per-monitor DPI awareness v2.

            bool createdNew;
            instanceMutex = new Mutex(true, "Local\\RoundedTB-Light", out createdNew);
            if (!createdNew && !testRegion)
            {
                try
                {
                    using (EventWaitHandle existing = EventWaitHandle.OpenExisting("Local\\RoundedTB-Light-ShowSettings"))
                    {
                        existing.Set();
                    }
                    Log(baseDir, "SECOND INSTANCE signaled existing host");
                }
                catch (Exception error)
                {
                    Log(baseDir, "SECOND INSTANCE signal failed: " + error.Message);
                }
                return 0;
            }

            IntPtr instance = GetModuleHandle(null);
            wndProcDelegate = WndProc;
            WNDCLASSEX wc = new WNDCLASSEX();
            wc.cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX));
            wc.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProcDelegate);
            wc.hInstance = instance;
            wc.hIcon = LoadIcon(IntPtr.Zero, (IntPtr)IDI_APPLICATION);
            wc.hCursor = LoadCursor(IntPtr.Zero, (IntPtr)IDI_APPLICATION);
            wc.lpszClassName = ClassName;
            if (RegisterClassEx(ref wc) == 0) return 1;

            hwnd = CreateWindowEx(testRegion ? WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE : WS_EX_TOOLWINDOW,
                ClassName, "RoundedTB", testRegion ? WS_POPUP : 0,
                0, 0, testRegion ? (testDynamicRegion ? 1920 : 800) : 0, testRegion ? 48 : 0,
                IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
            if (hwnd == IntPtr.Zero) return 2;
            taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
            try
            {
                showSettingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\RoundedTB-Light-ShowSettings");
                Thread settingsWaiter = new Thread(delegate()
                {
                    while (true)
                    {
                        try
                        {
                            showSettingsEvent.WaitOne();
                        }
                        catch (Exception)
                        {
                            return;
                        }
                        if (hwnd != IntPtr.Zero)
                        {
                            PostMessage(hwnd, WM_SHOW_SETTINGS, IntPtr.Zero, IntPtr.Zero);
                        }
                    }
                });
                settingsWaiter.IsBackground = true;
                settingsWaiter.Start();
            }
            catch (Exception error)
            {
                Log(baseDir, "SHOW SETTINGS event failed: " + error.Message);
            }
            shellHookMessage = RegisterWindowMessage("SHELLHOOK");
            bool shellHook = RegisterShellHookWindow(hwnd);
            Log(baseDir, "SHELLHOOK=" + shellHook);

            if (testRegion)
            {
                try
                {
                    Log(baseDir, testDynamicRegion ? DynamicRegion.TestHiddenWindow(hwnd)
                        : testAutoHide ? SimpleRegion.TestAutoHide(hwnd) : SimpleRegion.TestHiddenWindow(hwnd));
                    if (testAutoHide) Log(baseDir, InspectTaskbarPosition());
                    return 0;
                }
                catch (Exception error)
                {
                    Log(baseDir, "FAIL: " + error.Message);
                    return 3;
                }
                finally
                {
                    DestroyWindow(hwnd);
                }
            }

            IntPtr taskbarWindow = FindWindow("Shell_TrayWnd", null);
            bool started;
            if (settingsTakeover)
            {
                RoundedTB.AppPaths.Prepare();
                string configPath = RoundedTB.AppPaths.ConfigPath;
                settingsPath = configPath;
                activeSettings = RuntimeSettings.Load(configPath);
                TaskbarTakeover.SetCompositionCompatibility(activeSettings != null && activeSettings.CompositionCompat);
                TaskbarTakeover.SetFillBehavior(activeSettings != null && activeSettings.FillOnMaximise,
                    activeSettings != null && activeSettings.ShouldFillOnTaskSwitch);
                if (activeSettings == null)
                {
                    Log(baseDir, "TAKEOVER SETTINGS LOAD FAILED: " + configPath);
                    started = false;
                }
                else
                {
                    StartSettingsWatcher(configPath);
                    currentSimpleMode = !activeSettings.IsDynamic;
                    if (currentSimpleMode)
                    {
                        SegmentLayout simple = activeSettings.Simple;
                        simpleTop = simple.MarginTop;
                        simpleLeft = simple.MarginLeft;
                        simpleBottom = simple.MarginBottom;
                        simpleRight = simple.MarginRight;
                        simpleRadius = simple.CornerRadius;
                    }
                    string detail;
                    started = ApplyAllTaskbars(out detail);
                    Log(baseDir, string.Format("TAKEOVER SETTINGS START dynamic={0} taskbars={1} seconds={2}{3}",
                        activeSettings.IsDynamic, taskbars.Count, takeoverSeconds,
                        detail.Length > 0 ? " firstFailure=" + detail : ""));
                }
            }
            else if (dynamicTakeover)
            {
                IntPtr trayNotify = FindWindowEx(taskbarWindow, IntPtr.Zero, "TrayNotifyWnd", null);
                IntPtr bridge = FindWindowEx(taskbarWindow, IntPtr.Zero, "Windows.UI.Composition.DesktopWindowContentBridge", null);
                int appListRight = bridge != IntPtr.Zero ? ReadAppListRight(bridge) : 0;
                if (appListRight <= 0)
                {
                    IntPtr rebar = FindWindowEx(taskbarWindow, IntPtr.Zero, "ReBarWindow32", null);
                    IntPtr appList = FindWindowEx(rebar, IntPtr.Zero, "MSTaskSwWClass", null);
                    RECT appRect;
                    if (appList != IntPtr.Zero && GetWindowRect(appList, out appRect)) appListRight = appRect.Right;
                }

                RECT trayRect;
                int trayLeft = trayNotify != IntPtr.Zero && GetWindowRect(trayNotify, out trayRect) ? trayRect.Left : 0;
                DynamicInput template = new DynamicInput();
                template.AppListRight = appListRight;
                template.TrayLeft = trayLeft;
                template.HasTray = trayLeft != 0;
                template.Windows11 = true;
                template.IsCentred = ParseIntArg(args, "/centred:", 0) != 0;
                template.ShowAppList = ParseIntArg(args, "/showApp:", 1) != 0;
                template.ShowTray = ParseIntArg(args, "/showTray:", 1) != 0;
                template.ShowWidgets = ParseIntArg(args, "/showWidgets:", 0) != 0;
                template.ShowSegmentsOnHover = ParseIntArg(args, "/hover:", 0) != 0;
                template.AppListVisible = ParseIntArg(args, "/appVisible:", 1) != 0;
                template.TrayHoverEnabled = ParseIntArg(args, "/trayHover:", 0) != 0;
                template.App = ParseLayout(args, "/app:", Layout(18, 4, 4, 0, 4));
                template.Tray = ParseLayout(args, "/tray:", Layout(18, 4, 4, 0, 4));
                template.Widgets = ParseLayout(args, "/widgets:", Layout(18, 4, 4, 0, 4));
                currentSimpleMode = false;
                started = TaskbarTakeover.StartDynamic(taskbarWindow, template);
                Log(baseDir, string.Format("TAKEOVER DYNAMIC START appRight={0} trayLeft={1} app={2},{3},{4},{5},{6} tray={7},{8},{9},{10},{11} showApp={12} showTray={13} hover={14} seconds={15}",
                    appListRight, trayLeft,
                    template.App.CornerRadius, template.App.MarginTop, template.App.MarginLeft, template.App.MarginBottom, template.App.MarginRight,
                    template.Tray.CornerRadius, template.Tray.MarginTop, template.Tray.MarginLeft, template.Tray.MarginBottom, template.Tray.MarginRight,
                    template.ShowAppList, template.ShowTray, template.ShowSegmentsOnHover, takeoverSeconds));
            }
            else
            {
                int marginTop = ParseIntArg(args, "/top:", 0);
                int marginLeft = ParseIntArg(args, "/left:", 0);
                int marginBottom = ParseIntArg(args, "/bottom:", 0);
                int marginRight = ParseIntArg(args, "/right:", 0);
                int cornerRadius = ParseIntArg(args, "/radius:", 0);
                currentSimpleMode = true;
                simpleTop = marginTop;
                simpleLeft = marginLeft;
                simpleBottom = marginBottom;
                simpleRight = marginRight;
                simpleRadius = cornerRadius;
                started = TaskbarTakeover.Start(taskbarWindow, marginTop, marginLeft, marginBottom, marginRight, cornerRadius);
                Log(baseDir, string.Format("TAKEOVER START top={0} left={1} bottom={2} right={3} radius={4} seconds={5}",
                    marginTop, marginLeft, marginBottom, marginRight, cornerRadius, takeoverSeconds));
            }

            if (!started)
            {
                Log(baseDir, "TAKEOVER START FAILED: " + TaskbarTakeover.Failure);
                DestroyWindow(hwnd);
                return 4;
            }

            takeoverStartedUtc = DateTime.UtcNow;
            AppDomain.CurrentDomain.ProcessExit += delegate { ShutdownRestore("process exit"); };
            AppDomain.CurrentDomain.UnhandledException += delegate { ShutdownRestore("unhandled exception"); };

            bool useHover = settingsTakeover
                ? (activeSettings != null && activeSettings.ShowSegmentsOnHover)
                : (dynamicTakeover && ParseIntArg(args, "/hover:", 0) != 0);
            if (takeoverMode && useHover)
            {
                bool raw = RawInput.Start(hwnd);
                Log(baseDir, "RAWINPUT=" + raw);
            }

            // Load the embedded application icon. The tray icon is shared by
            // the module, so Windows owns its lifetime; no sidecar tray.ico is
            // needed in the single-EXE package.
            trayIcon = LoadIcon(instance, (IntPtr)IDI_APPLICATION);
            if (trayIcon == IntPtr.Zero)
            {
                trayIcon = LoadIcon(IntPtr.Zero, (IntPtr)IDI_APPLICATION);
            }
            AddTrayIcon();
            SetTimer(hwnd, (IntPtr)1, takeoverMode ? 100u : 1000u, IntPtr.Zero);
            if (takeoverMode) TaskbarTakeover.Tick();
            else Measure(baseDir);
            if (takeoverMode) RequestAutoHideAlign("startup");

            MSG msg;
            while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            RemoveTrayIcon();
            if (useLog) Log(baseDir, "exited");
            return 0;
        }

        private static List<IntPtr> DiscoverTaskbars()
        {
            List<IntPtr> result = new List<IntPtr>();
            IntPtr primary = FindWindow("Shell_TrayWnd", null);
            if (primary != IntPtr.Zero) result.Add(primary);
            IntPtr secondary = IntPtr.Zero;
            while ((secondary = FindWindowEx(IntPtr.Zero, secondary, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            {
                if (!result.Contains(secondary)) result.Add(secondary);
            }
            return result;
        }

        private static TaskbarEntry MeasureEntry(IntPtr hwnd)
        {
            TaskbarEntry entry = new TaskbarEntry();
            entry.Hwnd = hwnd;
            entry.AppListRight = MeasureAppListRight(hwnd);
            entry.TrayLeft = MeasureTrayLeft(hwnd);
            entry.HasTray = entry.TrayLeft != 0;
            return entry;
        }

        private static DynamicInput BuildTemplate(TaskbarEntry entry)
        {
            return activeSettings.ToDynamicInput(entry.AppListRight, entry.TrayLeft, entry.HasTray);
        }

        // (Re)register every discovered taskbar using the current settings and
        // refresh the taskbar list. Returns true if at least one started.
        private static bool ApplyAllTaskbars(out string detail)
        {
            detail = "";
            List<IntPtr> discovered = DiscoverTaskbars();

            Dictionary<IntPtr, AutoHideRecord> records = new Dictionary<IntPtr, AutoHideRecord>();
            foreach (TaskbarEntry existingEntry in taskbars)
            {
                if (existingEntry.AutoHide != null) records[existingEntry.Hwnd] = existingEntry.AutoHide;
            }

            List<IntPtr> existing = TaskbarTakeover.Taskbars;
            foreach (IntPtr old in existing)
            {
                if (!discovered.Contains(old)) TaskbarTakeover.Remove(old);
            }

            taskbars.Clear();
            bool any = false;
            foreach (IntPtr hwnd in discovered)
            {
                TaskbarEntry entry = MeasureEntry(hwnd);
                AutoHideRecord previousRecord;
                entry.AutoHide = records.TryGetValue(hwnd, out previousRecord) ? previousRecord : new AutoHideRecord();
                taskbars.Add(entry);

                bool ok;
                if (activeSettings.IsDynamic)
                {
                    ok = TaskbarTakeover.StartDynamic(entry.Hwnd, BuildTemplate(entry));
                }
                else
                {
                    SegmentLayout simple = activeSettings.Simple;
                    ok = TaskbarTakeover.Start(entry.Hwnd, simple.MarginTop, simple.MarginLeft,
                        simple.MarginBottom, simple.MarginRight, simple.CornerRadius);
                }
                if (ok) any = true;
                else if (detail.Length == 0) detail = TaskbarTakeover.Failure;
            }
            return any;
        }

        private static bool? ReadWindowsAutoHideSetting()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3"))
                {
                    byte[] settings = key != null ? key.GetValue("Settings") as byte[] : null;
                    if (settings != null && settings.Length > 8)
                    {
                        return (settings[8] & 0x02) != 0;
                    }
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        private static bool ReadTaskbarAutoHide(IntPtr hwnd)
        {
            APPBARDATA data = new APPBARDATA();
            data.Size = (uint)Marshal.SizeOf(typeof(APPBARDATA));
            data.Window = hwnd;
            const uint AbmGetState = 4;
            return (SHAppBarMessage(AbmGetState, ref data).ToUInt64() & 1) != 0;
        }

        private static void SetTaskbarAutoHide(IntPtr hwnd, bool autoHide)
        {
            APPBARDATA data = new APPBARDATA();
            data.Size = (uint)Marshal.SizeOf(typeof(APPBARDATA));
            data.Window = hwnd;
            const uint AbmSetState = 0x0A;
            const int AbsAutoHide = 1;
            const int AbsAlwaysOnTop = 2;
            data.LParam = new IntPtr(autoHide ? AbsAutoHide : AbsAlwaysOnTop);
            SHAppBarMessage(AbmSetState, ref data);
        }

        private static void RequestAutoHideAlign(string reason)
        {
            if (!takeoverMode || activeSettings == null) return;
            autoHideAlign.Request(DateTime.UtcNow, 1000);
            Log(AppDomain.CurrentDomain.BaseDirectory, "AUTOHIDE align requested: " + reason);
        }

        private static void AutoHideAlignTick()
        {
            if (!takeoverMode || activeSettings == null) return;
            if (!autoHideAlign.TryFire(DateTime.UtcNow)) return;

            bool wantAutoHide = AutoHidePolicy.WantsAutoHide(activeSettings.AutoHide, true);
            bool? saved = wantAutoHide ? (bool?)null : ReadWindowsAutoHideSetting();

            foreach (TaskbarEntry entry in taskbars)
            {
                if (entry.Hwnd == IntPtr.Zero) continue;
                bool observed = ReadTaskbarAutoHide(entry.Hwnd);
                AutoHideAction action = AutoHidePolicy.Evaluate(wantAutoHide, saved, observed, entry.AutoHide);
                if (!action.Change) continue;
                SetTaskbarAutoHide(entry.Hwnd, action.AutoHide);
                Log(AppDomain.CurrentDomain.BaseDirectory, "AUTOHIDE aligned hwnd=" + entry.Hwnd
                    + " autoHide=" + action.AutoHide + " want=" + wantAutoHide);
            }
        }

        private static void ShutdownRestore(string reason)
        {
            if (shutdownRestored) return;
            shutdownRestored = true;

            try
            {
                bool? saved = ReadWindowsAutoHideSetting();
                foreach (TaskbarEntry entry in taskbars)
                {
                    if (entry.Hwnd == IntPtr.Zero || !IsWindow(entry.Hwnd) || entry.AutoHide == null) continue;
                    bool observed = ReadTaskbarAutoHide(entry.Hwnd);
                    AutoHideAction action = AutoHidePolicy.RestoreForExit(saved, observed, entry.AutoHide);
                    if (!action.Change) continue;
                    SetTaskbarAutoHide(entry.Hwnd, action.AutoHide);
                    Log(AppDomain.CurrentDomain.BaseDirectory, "AUTOHIDE restored on exit hwnd=" + entry.Hwnd
                        + " autoHide=" + action.AutoHide + " registry="
                        + (saved.HasValue ? saved.Value.ToString() : "unavailable"));
                }
            }
            catch (Exception error)
            {
                Log(AppDomain.CurrentDomain.BaseDirectory, "AUTOHIDE exit restore failed: " + error.Message);
            }
            finally
            {
                StopSettingsWatcher();
                RawInput.Stop();
                TaskbarTakeover.Restore();
                Log(AppDomain.CurrentDomain.BaseDirectory, "SHUTDOWN restored: " + reason);
            }
        }

        private static string InspectTaskbarPosition()
        {
            IntPtr tray = FindWindow("Shell_TrayWnd", null);
            RECT window;
            MONITORINFO monitor = new MONITORINFO();
            monitor.Size = (uint)Marshal.SizeOf(typeof(MONITORINFO));
            if (tray == IntPtr.Zero || !GetWindowRect(tray, out window)
                || !GetMonitorInfo(MonitorFromWindow(tray, 2), ref monitor))
            {
                return "Read-only Explorer check: geometry unavailable.";
            }

            uint dpi = GetDpiForWindow(tray);
            AutoHideGeometry.Peek peek;
            AutoHideGeometry.State state = AutoHideGeometry.Classify(ToGeometryRect(window), ToGeometryRect(monitor.Monitor), dpi, out peek);
            APPBARDATA data = new APPBARDATA();
            data.Size = (uint)Marshal.SizeOf(typeof(APPBARDATA));
            data.Window = tray;
            const uint AbmGetState = 4;
            const ulong AbsAutoHide = 1;
            bool autoHide = (SHAppBarMessage(AbmGetState, ref data).ToUInt64() & AbsAutoHide) != 0;
            string strip = state == AutoHideGeometry.State.Hidden
                ? string.Format("; peek {0}, [{1},{2},{3},{4})", peek.Side, peek.Bounds.Left, peek.Bounds.Top,
                    peek.Bounds.Right, peek.Bounds.Bottom) : "";
            return "Read-only Explorer check: auto-hide=" + autoHide + "; position=" + state + "; DPI=" + dpi + strip;
        }

        private static AutoHideGeometry.Rect ToGeometryRect(RECT rect)
        {
            return new AutoHideGeometry.Rect(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }

        private static bool HasArg(string[] args, string value)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, value, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        internal static bool ShouldRebind(IntPtr current, IntPtr live, bool currentAlive)
        {
            if (current == IntPtr.Zero) return false;
            if (!currentAlive) return true;
            return live != IntPtr.Zero && live != current;
        }

        private static void StartSettingsWatcher(string path)
        {
            try
            {
                StopSettingsWatcher();
                string directory = Path.GetDirectoryName(path);
                string file = Path.GetFileName(path);
                if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(file)) return;

                settingsWatcher = new FileSystemWatcher(directory, file);
                settingsWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
                settingsWatcher.Changed += SettingsFileChanged;
                settingsWatcher.Created += SettingsFileChanged;
                settingsWatcher.Renamed += SettingsFileRenamed;
                settingsWatcher.EnableRaisingEvents = true;
            }
            catch (Exception error)
            {
                Log(AppDomain.CurrentDomain.BaseDirectory, "SETTINGS watcher failed: " + error.Message);
            }
        }

        private static void StopSettingsWatcher()
        {
            if (settingsWatcher == null) return;
            try
            {
                settingsWatcher.EnableRaisingEvents = false;
                settingsWatcher.Dispose();
            }
            catch (Exception)
            {
            }
            settingsWatcher = null;
        }

        private static void SettingsFileChanged(object sender, FileSystemEventArgs e)
        {
            RequestSettingsReload();
        }

        private static void SettingsFileRenamed(object sender, RenamedEventArgs e)
        {
            RequestSettingsReload();
        }

        private static void RequestSettingsReload()
        {
            lock (settingsLock)
            {
                settingsReloadPending = true;
                settingsReloadAtUtc = DateTime.UtcNow.AddMilliseconds(600);
            }
        }

        private static void ReloadSettingsTick()
        {
            if (settingsPath == null) return;

            bool pending;
            DateTime due;
            lock (settingsLock)
            {
                pending = settingsReloadPending;
                due = settingsReloadAtUtc;
            }
            if (!pending || DateTime.UtcNow < due) return;

            lock (settingsLock)
            {
                settingsReloadPending = false;
            }

            RuntimeSettings loaded = RuntimeSettings.Load(settingsPath);
            if (loaded == null)
            {
                Log(AppDomain.CurrentDomain.BaseDirectory, "SETTINGS reload failed");
                return;
            }

            activeSettings = loaded;
            TaskbarTakeover.SetCompositionCompatibility(loaded.CompositionCompat);
            TaskbarTakeover.SetFillBehavior(loaded.FillOnMaximise, loaded.ShouldFillOnTaskSwitch);

            currentSimpleMode = !loaded.IsDynamic;
            if (currentSimpleMode)
            {
                SegmentLayout simple = loaded.Simple;
                simpleTop = simple.MarginTop;
                simpleLeft = simple.MarginLeft;
                simpleBottom = simple.MarginBottom;
                simpleRight = simple.MarginRight;
                simpleRadius = simple.CornerRadius;
            }

            string detail;
            bool applied = ApplyAllTaskbars(out detail);
            if (!applied)
            {
                Log(AppDomain.CurrentDomain.BaseDirectory, "SETTINGS reload failed: " + detail);
                return;
            }

            TaskbarTakeover.Tick();
            RequestAutoHideAlign("settings reload");
            Log(AppDomain.CurrentDomain.BaseDirectory, "SETTINGS reloaded dynamic=" + loaded.IsDynamic
                + " taskbars=" + taskbars.Count
                + " showApp=" + (loaded.ShowAppList ?? true) + " trayHover=" + (loaded.TrayHoverEnabled ?? true));
        }

        private static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    return key != null && key.GetValue(RunValue) != null;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void SetStartup(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key == null) return;
                    if (enabled)
                    {
                        string exe = Process.GetCurrentProcess().MainModule.FileName;
                        key.SetValue(RunValue, "\"" + exe + "\"");
                    }
                    else
                    {
                        key.DeleteValue(RunValue, false);
                    }
                }
            }
            catch (Exception error)
            {
                Log(AppDomain.CurrentDomain.BaseDirectory, "STARTUP failed: " + error.Message);
            }
        }

        private static void LaunchSettings()
        {
            if (string.IsNullOrEmpty(settingsExePath))
            {
                Log(AppDomain.CurrentDomain.BaseDirectory, "SETTINGS launch skipped: no settings exe");
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo(settingsExePath, "--settings-only")
                {
                    UseShellExecute = true
                });
                Log(AppDomain.CurrentDomain.BaseDirectory, "SETTINGS launched: " + settingsExePath);
            }
            catch (Exception error)
            {
                Log(AppDomain.CurrentDomain.BaseDirectory, "SETTINGS launch failed: " + error.Message);
            }
        }

        private static void RequestAppMeasure()
        {
            if (!takeoverMode || activeSettings == null || !activeSettings.IsDynamic
                || !(activeSettings.ShowAppList ?? true))
            {
                return;
            }
            appMeasurePending = true;
            appMeasureAtUtc = DateTime.UtcNow.AddMilliseconds(500);
        }

        private static void MeasureAppTick()
        {
            if (!appMeasurePending || DateTime.UtcNow < appMeasureAtUtc) return;
            appMeasurePending = false;
            if (!takeoverMode || activeSettings == null || !activeSettings.IsDynamic
                || !(activeSettings.ShowAppList ?? true))
            {
                return;
            }

            bool changed = false;
            foreach (TaskbarEntry entry in taskbars)
            {
                int right = MeasureAppListRight(entry.Hwnd);
                if (right <= 0 || right == entry.AppListRight) continue;
                entry.AppListRight = right;
                changed = true;
                if (activeSettings.IsDynamic)
                {
                    TaskbarTakeover.Rebind(entry.Hwnd, BuildTemplate(entry));
                }
            }
            if (changed)
            {
                TaskbarTakeover.Tick();
                Log(AppDomain.CurrentDomain.BaseDirectory, "APP RIGHT updated across " + taskbars.Count + " taskbar(s)");
            }
        }

        private static void RequestRebind(string reason)
        {
            if (!takeoverMode) return;
            rebindPending = true;
            rebindAtUtc = DateTime.UtcNow.AddMilliseconds(800);
            rebindAttempts = 0;
            Log(AppDomain.CurrentDomain.BaseDirectory, "REBIND requested: " + reason);
        }

        private static void RebindTick()
        {
            if (!takeoverMode) return;

            if (!rebindPending)
            {
                if ((DateTime.UtcNow - lastHandleCheckUtc).TotalSeconds >= 2)
                {
                    lastHandleCheckUtc = DateTime.UtcNow;
                    List<IntPtr> discovered = DiscoverTaskbars();
                    bool changed = discovered.Count != TaskbarTakeover.Taskbars.Count;
                    if (!changed)
                    {
                        foreach (IntPtr hwnd in discovered)
                        {
                            if (!TaskbarTakeover.HasTaskbar(hwnd)) { changed = true; break; }
                        }
                    }
                    if (changed) RequestRebind("taskbar set changed or disappeared");
                }
                return;
            }

            if (DateTime.UtcNow < rebindAtUtc) return;
            rebindAttempts++;
            List<IntPtr> found = DiscoverTaskbars();
            if (found.Count == 0)
            {
                if (rebindAttempts < 20) rebindAtUtc = DateTime.UtcNow.AddMilliseconds(500);
                else
                {
                    rebindPending = false;
                    Log(AppDomain.CurrentDomain.BaseDirectory, "REBIND failed: taskbar not found");
                }
                return;
            }

            bool ok;
            string detail = "";
            if (activeSettings != null)
            {
                ok = ApplyAllTaskbars(out detail);
            }
            else if (currentSimpleMode)
            {
                // Args-driven debug mode re-applies only the primary simple bar.
                ok = TaskbarTakeover.RebindSimple(found[0], simpleTop, simpleLeft, simpleBottom, simpleRight, simpleRadius);
                if (!ok) detail = TaskbarTakeover.Failure;
            }
            else
            {
                // Args-driven dynamic template is not retained across rebinds.
                ok = true;
            }

            if (ok)
            {
                rebindPending = false;
                Log(AppDomain.CurrentDomain.BaseDirectory, "REBIND success taskbars=" + TaskbarTakeover.Taskbars.Count);
                TaskbarTakeover.Tick();
            }
            else if (rebindAttempts < 20)
            {
                rebindAtUtc = DateTime.UtcNow.AddMilliseconds(500);
            }
            else
            {
                rebindPending = false;
                Log(AppDomain.CurrentDomain.BaseDirectory, "REBIND failed: " + detail);
            }
        }

        private static int ParseIntArg(string[] args, string prefix, int fallback)
        {
            foreach (string arg in args)
            {
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    int value;
                    if (int.TryParse(arg.Substring(prefix.Length), out value)) return value;
                }
            }
            return fallback;
        }

        private static string GetArgValue(string[] args, string prefix)
        {
            foreach (string arg in args)
            {
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return arg.Substring(prefix.Length);
            }
            return null;
        }

        private static SegmentLayout ParseLayout(string[] args, string prefix, SegmentLayout fallback)
        {
            string value = GetArgValue(args, prefix);
            if (value == null) return fallback;
            string[] parts = value.Split(',');
            if (parts.Length != 5) return fallback;
            int[] numbers = new int[5];
            for (int i = 0; i < 5; i++)
            {
                if (!int.TryParse(parts[i], out numbers[i])) return fallback;
            }
            fallback.CornerRadius = numbers[0];
            fallback.MarginTop = numbers[1];
            fallback.MarginLeft = numbers[2];
            fallback.MarginBottom = numbers[3];
            fallback.MarginRight = numbers[4];
            return fallback;
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

        private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_INPUT)
            {
                RawInput.OnInput();
                return IntPtr.Zero;
            }
            if (msg == WM_SHOW_SETTINGS)
            {
                LaunchSettings();
                return IntPtr.Zero;
            }
            if (taskbarCreatedMessage != 0 && msg == taskbarCreatedMessage)
            {
                // Explorer restarts drop the shell hook registration.
                RegisterShellHookWindow(hWnd);
                RequestRebind("TaskbarCreated");
                RequestAutoHideAlign("TaskbarCreated");
                return IntPtr.Zero;
            }
            if (shellHookMessage != 0 && msg == shellHookMessage)
            {
                int code = wParam.ToInt32();
                if (code == 1 || code == 2) RequestAppMeasure();
                return IntPtr.Zero;
            }
            if (msg == WM_SETTINGCHANGE || msg == WM_DISPLAYCHANGE
                || msg == WM_POWERBROADCAST || msg == WM_DPICHANGED)
            {
                RequestRebind("system event " + msg);
                RequestAutoHideAlign("system event " + msg);
                return IntPtr.Zero;
            }
            if (msg == WM_TIMER)
            {
                ReloadSettingsTick();
                MeasureAppTick();
                RebindTick();
                AutoHideAlignTick();
                if (takeoverMode)
                {
                    TaskbarTakeover.Tick();
                    if (takeoverSeconds > 0 && (DateTime.UtcNow - takeoverStartedUtc).TotalSeconds >= takeoverSeconds)
                    {
                        ShutdownRestore("takeover test timeout");
                        Log(AppDomain.CurrentDomain.BaseDirectory, "TAKEOVER RESTORED applied="
                            + TaskbarTakeover.AppliedCount
                            + " edgeOn=" + RawInput.EdgeOnCount
                            + " edgeOff=" + RawInput.EdgeOffCount
                            + (TaskbarTakeover.Failure.Length > 0 ? " failure=" + TaskbarTakeover.Failure : ""));
                        DestroyWindow(hWnd);
                    }
                    return IntPtr.Zero;
                }

                Measure(AppDomain.CurrentDomain.BaseDirectory);
                return IntPtr.Zero;
            }
            if (msg == WM_TRAY)
            {
                int mouse = (int)(lParam.ToInt64() & 0xFFFF);
                if (mouse == WM_RBUTTONUP || mouse == WM_CONTEXTMENU) ShowMenu(hWnd);
                return IntPtr.Zero;
            }
            if (msg == WM_COMMAND)
            {
                if (wParam.ToInt64() == 1) DestroyWindow(hWnd);
                return IntPtr.Zero;
            }
            if (msg == WM_CLOSE)
            {
                ShutdownRestore("WM_CLOSE");
                DestroyWindow(hWnd);
                return IntPtr.Zero;
            }
            if (msg == WM_ENDSESSION)
            {
                ShutdownRestore("WM_ENDSESSION");
                return IntPtr.Zero;
            }
            if (msg == WM_DESTROY)
            {
                ShutdownRestore("WM_DESTROY");
                if (showSettingsEvent != null)
                {
                    showSettingsEvent.Close();
                    showSettingsEvent = null;
                }
                if (instanceMutex != null)
                {
                    try
                    {
                        instanceMutex.ReleaseMutex();
                    }
                    catch (Exception)
                    {
                    }
                    instanceMutex.Dispose();
                    instanceMutex = null;
                }
                PostQuitMessage(0);
                return IntPtr.Zero;
            }
            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private static void ShowMenu(IntPtr hWnd)
        {
            IntPtr menu = CreatePopupMenu();
            if (menu == IntPtr.Zero) return;

            if (activeSettings != null && activeSettings.IsDynamic)
            {
                bool tray = activeSettings.ShowSegmentsOnHover
                    ? (activeSettings.TrayHoverEnabled ?? true)
                    : activeSettings.ShowTray;
                AppendMenu(menu, MF_STRING | (tray ? MF_CHECKED : 0), 10,
                    activeSettings.ShowSegmentsOnHover ? "Tray hover" : "Show tray");
                AppendMenu(menu, MF_STRING | ((activeSettings.ShowAppList ?? true) ? MF_CHECKED : 0), 11, "App segment");
                if (!string.IsNullOrEmpty(settingsExePath))
                {
                    AppendMenu(menu, MF_STRING, 13, "Settings...");
                }
                AppendMenu(menu, MF_STRING | (IsStartupEnabled() ? MF_CHECKED : 0), 14, "Start with Windows");
                AppendMenu(menu, MF_SEPARATOR, 0, null);
            }

            AppendMenu(menu, MF_STRING, 1, "Exit");

            POINT pt;
            GetCursorPos(out pt);
            SetForegroundWindow(hWnd);
            int cmd = TrackPopupMenu(menu, TPM_RIGHTBUTTON | TPM_RETURNCMD, pt.X, pt.Y, 0, hWnd, IntPtr.Zero);
            DestroyMenu(menu);
            PostMessage(hWnd, 0, IntPtr.Zero, IntPtr.Zero);

            if (cmd == 1)
            {
                DestroyWindow(hWnd);
                return;
            }
            if (cmd == 13)
            {
                LaunchSettings();
                return;
            }
            if (cmd == 14)
            {
                bool enable = !IsStartupEnabled();
                SetStartup(enable);
                Log(AppDomain.CurrentDomain.BaseDirectory, "STARTUP enabled=" + enable);
                return;
            }
            if (activeSettings == null) return;
            if (cmd == 10) activeSettings.ToggleTray();
            else if (cmd == 11) activeSettings.ToggleAppList();
            else return;
            ApplyMenuChange();
        }

        private static void ApplyMenuChange()
        {
            if (activeSettings == null) return;

            // Re-measure app bounds so a newly enabled app segment has geometry.
            foreach (TaskbarEntry entry in taskbars)
            {
                int right = MeasureAppListRight(entry.Hwnd);
                if (right > 0) entry.AppListRight = right;
            }

            if (activeSettings.IsDynamic)
            {
                foreach (TaskbarEntry entry in taskbars)
                {
                    TaskbarTakeover.Rebind(entry.Hwnd, BuildTemplate(entry));
                }
            }
            TaskbarTakeover.Tick();

            string error = "";
            bool saved = settingsPath != null && activeSettings.Save(settingsPath, out error);
            Log(AppDomain.CurrentDomain.BaseDirectory, string.Format(
                "MENU showApp={0} showTray={1} trayHover={2} widgets={3} saved={4}{5}",
                activeSettings.ShowAppList ?? true, activeSettings.ShowTray,
                activeSettings.TrayHoverEnabled ?? true, activeSettings.ShowWidgets,
                saved, saved || error == null ? "" : " error=" + error));
        }

        private static void AddTrayIcon()
        {
            NOTIFYICONDATA data = new NOTIFYICONDATA();
            data.cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA));
            data.hWnd = hwnd;
            data.uID = 1;
            data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
            data.uCallbackMessage = WM_TRAY;
            data.hIcon = trayIcon;
            data.szTip = "RoundedTB";
            data.szInfo = "";
            data.szInfoTitle = "";
            Shell_NotifyIcon(NIM_ADD, ref data);
        }

        private static void RemoveTrayIcon()
        {
            NOTIFYICONDATA data = new NOTIFYICONDATA();
            data.cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA));
            data.hWnd = hwnd;
            data.uID = 1;
            Shell_NotifyIcon(NIM_DELETE, ref data);
            if (trayIconOwned && trayIcon != IntPtr.Zero)
            {
                DestroyIcon(trayIcon);
                trayIcon = IntPtr.Zero;
                trayIconOwned = false;
            }
        }

        private static void Measure(string baseDir)
        {
            IntPtr tray = FindWindow("Shell_TrayWnd", null);
            if (tray == IntPtr.Zero) return;
            IntPtr trayNotify = FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
            IntPtr rebar = FindWindowEx(tray, IntPtr.Zero, "ReBarWindow32", null);
            IntPtr appList = FindWindowEx(rebar, IntPtr.Zero, "MSTaskSwWClass", null);
            IntPtr bridge = FindWindowEx(tray, IntPtr.Zero, "Windows.UI.Composition.DesktopWindowContentBridge", null);

            RECT barRect;
            RECT trayRect;
            RECT appRect;
            GetWindowRect(tray, out barRect);
            GetWindowRect(trayNotify, out trayRect);
            GetWindowRect(appList, out appRect);

            int uiaAppRight = 0;
            RECT uiaTray = new RECT();
            bool uiaTrayOk = false;
            if (useUia && bridge != IntPtr.Zero && (DateTime.UtcNow - lastUiaUtc).TotalSeconds >= 10)
            {
                lastUiaUtc = DateTime.UtcNow;
                uiaAppRight = ReadAppListRight(bridge);
                uiaTrayOk = ReadTrayRect(bridge, out uiaTray);
            }

            if (useLog && (DateTime.UtcNow - lastLogUtc).TotalSeconds >= 60)
            {
                lastLogUtc = DateTime.UtcNow;
                Log(baseDir, string.Format(
                    "bar={0},{1},{2},{3} tray={4},{5},{6},{7} app={8},{9},{10},{11} uiaApp={12} uiaTray={13}",
                    barRect.Left, barRect.Top, barRect.Right, barRect.Bottom,
                    trayRect.Left, trayRect.Top, trayRect.Right, trayRect.Bottom,
                    appRect.Left, appRect.Top, appRect.Right, appRect.Bottom,
                    uiaAppRight,
                    uiaTrayOk ? string.Format("{0},{1},{2},{3}", uiaTray.Left, uiaTray.Top, uiaTray.Right, uiaTray.Bottom) : "none"));
            }
        }

        private static int ReadAppListRight(IntPtr bridge)
        {
            try
            {
                AutomationElement root = AutomationElement.FromHandle(bridge);
                if (root == null) return 0;
                Condition condition = new PropertyCondition(
                    AutomationElement.ClassNameProperty, "Taskbar.TaskListButtonAutomationPeer");
                AutomationElementCollection buttons = root.FindAll(TreeScope.Descendants, condition);
                double right = 0;
                foreach (AutomationElement button in buttons)
                {
                    double buttonRight = button.Current.BoundingRectangle.Right;
                    if (buttonRight > right) right = buttonRight;
                }
                return right > 0 ? (int)Math.Ceiling(right) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static int MeasureAppListRight(IntPtr taskbarWindow)
        {
            IntPtr bridge = FindWindowEx(taskbarWindow, IntPtr.Zero, "Windows.UI.Composition.DesktopWindowContentBridge", null);
            int right = bridge != IntPtr.Zero ? ReadAppListRight(bridge) : 0;
            if (right <= 0)
            {
                IntPtr rebar = FindWindowEx(taskbarWindow, IntPtr.Zero, "ReBarWindow32", null);
                IntPtr appList = FindWindowEx(rebar, IntPtr.Zero, "MSTaskSwWClass", null);
                RECT appRect;
                if (appList != IntPtr.Zero && GetWindowRect(appList, out appRect)) right = appRect.Right;
            }
            return right;
        }

        private static int MeasureTrayLeft(IntPtr taskbarWindow)
        {
            IntPtr tray = FindWindowEx(taskbarWindow, IntPtr.Zero, "TrayNotifyWnd", null);
            RECT trayRect;
            return tray != IntPtr.Zero && GetWindowRect(tray, out trayRect) ? trayRect.Left : 0;
        }

        private static bool ReadTrayRect(IntPtr bridge, out RECT rect)
        {
            rect = new RECT();
            try
            {
                AutomationElement root = AutomationElement.FromHandle(bridge);
                if (root == null) return false;
                AutomationElementCollection elements = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                int left = int.MaxValue;
                int top = int.MaxValue;
                int right = int.MinValue;
                int bottom = int.MinValue;
                foreach (AutomationElement element in elements)
                {
                    string className = element.Current.ClassName;
                    if (string.IsNullOrEmpty(className) || !className.StartsWith("SystemTray.", StringComparison.Ordinal)) continue;
                    System.Windows.Rect bounds = element.Current.BoundingRectangle;
                    if (bounds.Width <= 0 || bounds.Height <= 0) continue;
                    if ((int)bounds.Left < left) left = (int)bounds.Left;
                    if ((int)bounds.Top < top) top = (int)bounds.Top;
                    if ((int)bounds.Right > right) right = (int)bounds.Right;
                    if ((int)bounds.Bottom > bottom) bottom = (int)bounds.Bottom;
                }
                if (right <= left) return false;
                rect.Left = left;
                rect.Top = top;
                rect.Right = right;
                rect.Bottom = bottom;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static readonly object logLock = new object();

        private static void Log(string baseDir, string text)
        {
            try
            {
                RoundedTB.AppPaths.Prepare();
                lock (logLock)
                {
                    // Keep diagnostics out of the program folder. The settings
                    // UI log lives in the same folder under its own name.
                    string path = RoundedTB.AppPaths.HostLogPath;
                    FileInfo info = new FileInfo(path);
                    if (info.Exists && info.Length > 1024 * 1024) File.Delete(path);
                    File.AppendAllText(path, string.Format("[{0:yyyy-MM-dd HH:mm:ss.fff}] {1}{2}",
                        DateTime.Now, text, Environment.NewLine));
                }
            }
            catch (Exception)
            {
            }
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public uint Size;
            public RECT Monitor;
            public RECT WorkArea;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public uint Size;
            public IntPtr Window;
            public uint CallbackMessage;
            public uint Edge;
            public RECT Bounds;
            public IntPtr LParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string name);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX wc);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(uint exStyle, string className, string windowName,
            uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetMessage(out MSG msg, IntPtr hWnd, uint min, uint max);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG msg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG msg);

        [DllImport("user32.dll")]
        private static extern void PostQuitMessage(int code);

        [DllImport("user32.dll")]
        private static extern IntPtr SetTimer(IntPtr hWnd, IntPtr id, uint interval, IntPtr proc);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadIcon(IntPtr instance, IntPtr name);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr icon);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadCursor(IntPtr instance, IntPtr name);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegisterWindowMessage(string name);

        [DllImport("user32.dll")]
        private static extern bool RegisterShellHookWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string windowName);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr context);

        [DllImport("shell32.dll")]
        private static extern UIntPtr SHAppBarMessage(uint message, ref APPBARDATA data);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr menu, uint flags, uint id, string text);

        [DllImport("user32.dll")]
        private static extern int TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr hWnd, IntPtr rect);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr menu);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT pt);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint msg, ref NOTIFYICONDATA data);
    }
}
