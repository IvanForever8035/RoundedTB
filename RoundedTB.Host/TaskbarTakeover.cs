using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TrayProbe
{
    internal static class TaskbarTakeover
    {
        private sealed class TaskbarState
        {
            internal IntPtr Hwnd;
            internal bool Dynamic;
            internal int Top, Left, Bottom, Right, Radius; // simple margins
            internal DynamicInput Template;                // dynamic template
            internal string LastSignature = "";
            internal bool Applied;
            internal bool FilledForWindow;
            internal int TemplateRevision;
            internal ulong LastFastKey;
            internal bool HasFastKey;
            internal readonly DynamicPlan Plan = new DynamicPlan();
            internal AutoHideGeometry.Rect MonitorBounds;
            internal DockEdge Edge;
        }

        private static readonly Dictionary<IntPtr, TaskbarState> States = new Dictionary<IntPtr, TaskbarState>();
        private static readonly List<IntPtr> Order = new List<IntPtr>();

        private static bool compositionCompat;
        private static bool fillOnMaximise = true;
        private static bool fillOnTaskSwitch = true;

        internal static string Failure = "";
        internal static int AppliedCount;
        internal static string LastAppliedSignature = "";

        internal static void SetFillBehavior(bool maximise, bool taskSwitch)
        {
            fillOnMaximise = maximise;
            fillOnTaskSwitch = taskSwitch;
        }

        internal static void SetCompositionCompatibility(bool enabled)
        {
            compositionCompat = enabled;
        }

        internal static bool PointerOnEdge { get; private set; }

        internal static void SetPointer(int x, int y)
        {
            bool onEdge = false;
            foreach (TaskbarState state in States.Values)
            {
                if (state.MonitorBounds.Right <= state.MonitorBounds.Left) continue;
                if (TaskbarModel.Contains(TaskbarModel.EdgeTrigger(state.MonitorBounds, state.Edge, 2), x, y))
                {
                    onEdge = true;
                    break;
                }
            }
            PointerOnEdge = onEdge;
        }

        private static bool Validate(IntPtr hwnd)
        {
            Failure = "";
            if (hwnd == IntPtr.Zero) { Failure = "taskbar handle missing"; return false; }

            StringBuilder className = new StringBuilder(64);
            GetClassName(hwnd, className, className.Capacity);
            string name = className.ToString();
            if (name != "Shell_TrayWnd" && name != "Shell_SecondaryTrayWnd")
            {
                Failure = "target is not a taskbar window: " + name;
                return false;
            }

            uint processId;
            GetWindowThreadProcessId(hwnd, out processId);
            try
            {
                Process owner = Process.GetProcessById((int)processId);
                if (!string.Equals(owner.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase))
                {
                    Failure = "taskbar owner is " + owner.ProcessName;
                    return false;
                }
            }
            catch (Exception error)
            {
                Failure = "cannot verify taskbar owner: " + error.Message;
                return false;
            }

            return true;
        }

        internal static bool Start(IntPtr hwnd, int marginTop, int marginLeft, int marginBottom, int marginRight, int cornerRadius)
        {
            if (!Validate(hwnd)) return false;

            TaskbarState state = new TaskbarState();
            state.Hwnd = hwnd;
            state.Dynamic = false;
            state.Top = marginTop;
            state.Left = marginLeft;
            state.Bottom = marginBottom;
            state.Right = marginRight;
            state.Radius = cornerRadius;
            Set(hwnd, state);
            return true;
        }

        internal static bool StartDynamic(IntPtr hwnd, DynamicInput template)
        {
            if (!Validate(hwnd)) return false;

            TaskbarState state = new TaskbarState();
            state.Hwnd = hwnd;
            state.Dynamic = true;
            state.Template = template;
            Set(hwnd, state);
            return true;
        }

        internal static bool Rebind(IntPtr hwnd, DynamicInput template)
        {
            if (!Validate(hwnd)) return false;

            TaskbarState state;
            if (States.TryGetValue(hwnd, out state))
            {
                state.Dynamic = true;
                state.Template = template;
                state.LastSignature = "";
                state.Applied = false;
                state.FilledForWindow = false;
                state.TemplateRevision++;
                state.HasFastKey = false;
            }
            else
            {
                state = new TaskbarState();
                state.Hwnd = hwnd;
                state.Dynamic = true;
                state.Template = template;
                Set(hwnd, state);
            }
            return true;
        }

        internal static bool RebindSimple(IntPtr hwnd, int marginTop, int marginLeft, int marginBottom,
            int marginRight, int cornerRadius)
        {
            if (!Validate(hwnd)) return false;

            TaskbarState state;
            if (States.TryGetValue(hwnd, out state))
            {
                state.Dynamic = false;
                state.Top = marginTop;
                state.Left = marginLeft;
                state.Bottom = marginBottom;
                state.Right = marginRight;
                state.Radius = cornerRadius;
                state.LastSignature = "";
                state.Applied = false;
                state.FilledForWindow = false;
                state.TemplateRevision++;
                state.HasFastKey = false;
            }
            else
            {
                state = new TaskbarState();
                state.Hwnd = hwnd;
                state.Dynamic = false;
                state.Top = marginTop;
                state.Left = marginLeft;
                state.Bottom = marginBottom;
                state.Right = marginRight;
                state.Radius = cornerRadius;
                Set(hwnd, state);
            }
            return true;
        }

        private static void Set(IntPtr hwnd, TaskbarState state)
        {
            States[hwnd] = state;
            if (!Order.Contains(hwnd)) Order.Add(hwnd);
        }

        internal static bool Remove(IntPtr hwnd)
        {
            TaskbarState state;
            if (!States.TryGetValue(hwnd, out state)) return false;
            ClearRegion(hwnd);
            States.Remove(hwnd);
            Order.Remove(hwnd);
            return true;
        }

        internal static bool HasTaskbar(IntPtr hwnd)
        {
            return States.ContainsKey(hwnd);
        }

        internal static IntPtr CurrentTaskbar
        {
            get { return Order.Count > 0 ? Order[0] : IntPtr.Zero; }
        }

        internal static List<IntPtr> Taskbars
        {
            get { return new List<IntPtr>(Order); }
        }

        internal static void Tick()
        {
            // Snapshot so a removed state mid-loop does not invalidate iteration.
            List<IntPtr> handles = new List<IntPtr>(Order);
            foreach (IntPtr hwnd in handles)
            {
                TaskbarState state;
                if (States.TryGetValue(hwnd, out state)) TickState(state);
            }
        }

        private static void TickState(TaskbarState state)
        {
            IntPtr hwnd = state.Hwnd;

            RECT window;
            if (!GetWindowRect(hwnd, out window)) { AbortState(state, "cannot read taskbar rect"); return; }
            IntPtr monitor = MonitorFromWindow(hwnd, 2);
            MONITORINFO info = new MONITORINFO();
            info.Size = (uint)Marshal.SizeOf(typeof(MONITORINFO));
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) { AbortState(state, "cannot read taskbar monitor"); return; }

            uint dpi = GetDpiForWindow(hwnd);
            AutoHideGeometry.Rect monitorRect = ToGeometryRect(info.Monitor);
            state.MonitorBounds = monitorRect;
            state.Edge = TaskbarModel.DetectDockEdge(ToGeometryRect(window), monitorRect);

            AutoHideGeometry.Peek peek;
            AutoHideGeometry.State autoHideState = AutoHideGeometry.Classify(
                ToGeometryRect(window), monitorRect, dpi, out peek);
            if (autoHideState == AutoHideGeometry.State.Transitioning || autoHideState == AutoHideGeometry.State.OffScreen)
            {
                // Re-applying mid-animation makes the shell snap to the end.
                return;
            }
            if (autoHideState == AutoHideGeometry.State.Invalid) { AbortState(state, "invalid taskbar geometry"); return; }

            bool shouldFill = ShouldFillTaskbar(hwnd);
            if (shouldFill)
            {
                if (!state.FilledForWindow)
                {
                    if (SetWindowRgn(hwnd, IntPtr.Zero, true) == 0)
                    {
                        AbortState(state, "cannot reset region for maximized window fill");
                        return;
                    }
                    state.FilledForWindow = true;
                    state.Applied = true;
                    state.LastSignature = "filled";
                    AppliedCount++;
                    LastAppliedSignature = state.LastSignature;
                    RefreshComposition(hwnd);
                }
                return;
            }
            if (state.FilledForWindow)
            {
                state.FilledForWindow = false;
                state.Applied = false;
                state.LastSignature = "";
                state.HasFastKey = false;
            }

            if (state.Dynamic)
            {
                if (state.Edge == DockEdge.Left || state.Edge == DockEdge.Right)
                {
                    // Dynamic split mode is horizontal-only for now; a vertical
                    // taskbar is left alone rather than given a sideways layout.
                    // Simple mode is orientation-agnostic and still covers it.
                    return;
                }
                ApplyDynamic(state, window, monitorRect, dpi, autoHideState, peek);
                return;
            }

            // Simple mode is orientation-agnostic: it insets the taskbar's own
            // rectangle, which is correct on top/bottom/left/right taskbars.
            SimpleRegion.Bounds bounds;
            if (!SimpleRegion.TryCalculate(window.Right - window.Left, window.Bottom - window.Top, dpi,
                state.Left, state.Top, state.Right, state.Bottom, state.Radius, out bounds))
            {
                AbortState(state, "simple region bounds are empty");
                return;
            }

            bool includePeek = autoHideState == AutoHideGeometry.State.Hidden
                && AutoHideGeometry.HasInsetAtPeekEdge(bounds, peek);
            string signature = string.Format("{0}:{1},{2},{3},{4}:{5},{6},{7},{8}:{9}",
                autoHideState, window.Left, window.Top, window.Right, window.Bottom,
                bounds.Left, bounds.Top, bounds.Right, bounds.Bottom,
                includePeek ? string.Format("{0},{1},{2},{3}", peek.Bounds.Left, peek.Bounds.Top, peek.Bounds.Right, peek.Bounds.Bottom) : "none");
            if (state.Applied && signature == state.LastSignature) return;

            IntPtr region = SimpleRegion.BuildRegion(bounds, includePeek, peek.Bounds);
            if (region == IntPtr.Zero) { AbortState(state, "cannot build region"); return; }

            if (SetWindowRgn(hwnd, region, true) == 0)
            {
                DeleteObject(region);
                AbortState(state, "SetWindowRgn failed");
                return;
            }

            state.Applied = true;
            state.LastSignature = signature;
            AppliedCount++;
            LastAppliedSignature = signature;
            RefreshComposition(hwnd);
        }

        private static bool ShouldFillTaskbar(IntPtr hwnd)
        {
            if (!fillOnMaximise || hwnd == IntPtr.Zero) return false;

            if (fillOnTaskSwitch)
            {
                POINT origin = new POINT();
                StringBuilder className = new StringBuilder(128);
                IntPtr topWindow = WindowFromPoint(origin);
                if (topWindow != IntPtr.Zero
                    && GetClassName(topWindow, className, className.Capacity) > 0
                    && className.ToString() == "XamlExplorerHostIslandWindow")
                {
                    return true;
                }
            }

            IntPtr taskbarMonitor = MonitorFromWindow(hwnd, 2);
            if (taskbarMonitor == IntPtr.Zero) return false;

            bool fill = false;
            EnumWindows(delegate(IntPtr window, IntPtr state)
            {
                if (!IsWindowVisible(window) || MonitorFromWindow(window, 2) != taskbarMonitor)
                    return true;

                uint cloaked;
                if (DwmGetWindowAttribute(window, 14, out cloaked, sizeof(uint)) == 0 && cloaked != 0)
                    return true;

                WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
                placement.Length = (uint)Marshal.SizeOf(typeof(WINDOWPLACEMENT));
                if (GetWindowPlacement(window, ref placement) && placement.ShowCommand == 3)
                {
                    fill = true;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return fill;
        }

        private static bool ApplyEmpty(TaskbarState state, string signature)
        {
            IntPtr empty = CreateRectRgn(0, 0, 0, 0);
            if (empty != IntPtr.Zero && SetWindowRgn(state.Hwnd, empty, true) != 0)
            {
                state.Applied = true;
                state.LastSignature = signature;
                AppliedCount++;
                LastAppliedSignature = signature;
                RefreshComposition(state.Hwnd);
                return true;
            }
            if (empty != IntPtr.Zero) DeleteObject(empty);
            return false;
        }

        private static void ApplyDynamic(TaskbarState state, RECT window, AutoHideGeometry.Rect monitor, uint dpi,
            AutoHideGeometry.State autoHideState, AutoHideGeometry.Peek peek)
        {
            DynamicInput input = state.Template;
            input.BarLeft = window.Left;
            input.BarWidth = window.Right - window.Left;
            input.BarHeight = window.Bottom - window.Top;
            input.Dpi = dpi;

            bool edgeHovered = false;
            if (input.ShowSegmentsOnHover)
            {
                AutoHideGeometry.Rect visible;
                if (autoHideState == AutoHideGeometry.State.Hidden)
                {
                    // Trigger from the screen edge itself, not from a visible
                    // taskbar strip. The host can apply the segment when the
                    // pointer arrives, so no resting pixels are needed.
                    visible = TaskbarModel.EdgeTrigger(monitor, state.Edge, 2);
                }
                else
                {
                    visible = new AutoHideGeometry.Rect(window.Left, window.Top, window.Right, window.Bottom);
                }

                POINT cursor;
                bool haveCursor = false;
                bool cursorOutsideEdge = false;
                if (GetCursorPos(out cursor))
                {
                    // Sample the live cursor on each existing tick. Raw Input
                    // remains an immediate tick signal, but is not the position
                    // source because it may stop arriving across integrity levels.
                    SetPointer(cursor.X, cursor.Y);
                    haveCursor = true;
                }

                if (haveCursor && autoHideState == AutoHideGeometry.State.Hidden)
                {
                    cursorOutsideEdge = !TaskbarModel.Contains(
                        TaskbarModel.EdgeTrigger(monitor, state.Edge, 2), cursor.X, cursor.Y);
                }

                if (haveCursor && !cursorOutsideEdge)
                {
                    HoverGeometry.HitInput hit = new HoverGeometry.HitInput();
                    hit.Left = visible.Left;
                    hit.Top = visible.Top;
                    hit.Right = visible.Right;
                    hit.Bottom = visible.Bottom;
                    hit.AppListRight = input.AppListRight;
                    hit.TrayLeft = input.TrayLeft;
                    hit.WidgetsRight = visible.Left + Convert.ToInt32(168 * (dpi / 96.0));
                    hit.HasTray = input.HasTray;
                    HoverGeometry.HitResult result = HoverGeometry.Evaluate(hit, cursor.X, cursor.Y);
                    edgeHovered = result.AppList || result.Tray || result.Widgets;

                    input.AppListVisible = result.AppList;
                    input.ShowTray = input.TrayHoverEnabled && result.Tray;
                    if (input.ShowAppList)
                    {
                        input.ShowWidgets = result.Widgets;
                    }
                }
            }

            ulong fastKey = FastKey(state, autoHideState, window, dpi, input);
            if (state.HasFastKey && fastKey == state.LastFastKey) return;
            state.LastFastKey = fastKey;
            state.HasFastKey = true;

            DynamicPlan plan = DynamicGeometry.Build(input, state.Plan);
            bool topInset = HasTopInset(input, dpi);
            // A hidden auto-hide taskbar still needs a hit-testable edge while
            // hover mode has no segment selected. Without this fallback, a
            // host started after Explorer has already hidden the bar applies an
            // empty region and the shell can no longer trigger the reveal.
            bool includePeek = autoHideState == AutoHideGeometry.State.Hidden
                && (!input.ShowSegmentsOnHover || plan.Segments.Count == 0
                    || (edgeHovered && topInset));
            string signature = DynamicSignature(autoHideState, window, plan, includePeek, peek);
            if (state.Applied && signature == state.LastSignature) return;

            if (plan.Segments.Count == 0 && !includePeek)
            {
                if (!input.ShowSegmentsOnHover)
                {
                    // Static mode with every segment disabled: keep the taskbar.
                    return;
                }

                if (ShouldKeepHoverRegion(true, includePeek, true, autoHideState == AutoHideGeometry.State.Visible))
                {
                    // Keep the last segment so the shell can animate the retract.
                    // If the region is already empty, restore the full window so
                    // the shell still has a hittable edge to hide from.
                    if (IsWindowRegionEmpty(state.Hwnd))
                    {
                        ClearRegion(state.Hwnd);
                    }
                    return;
                }

                if (ApplyEmpty(state, signature)) return;
                AbortState(state, "cannot apply empty hover region");
                return;
            }

            if (plan.Segments.Count == 0 && includePeek)
            {
                string peekError;
                IntPtr peekRegion = DynamicRegion.Build(plan, true, peek.Bounds, out peekError);
                if (peekRegion == IntPtr.Zero)
                {
                    // A zero top margin means the enabled segment reaches the
                    // edge by itself, so hover resting legitimately has no peek.
                    if (input.ShowSegmentsOnHover && ApplyEmpty(state, signature)) return;
                    AbortState(state, peekError.Length > 0 ? peekError : "cannot build peek region");
                    return;
                }

                if (SetWindowRgn(state.Hwnd, peekRegion, true) == 0)
                {
                    DeleteObject(peekRegion);
                    AbortState(state, "SetWindowRgn failed");
                    return;
                }

                state.Applied = true;
                state.LastSignature = signature;
                AppliedCount++;
                LastAppliedSignature = signature;
                RefreshComposition(state.Hwnd);
                return;
            }

            string error;
            IntPtr region = DynamicRegion.Build(plan, includePeek, peek.Bounds, out error);
            if (region == IntPtr.Zero)
            {
                AbortState(state, error.Length > 0 ? error : "cannot build dynamic region");
                return;
            }

            if (SetWindowRgn(state.Hwnd, region, true) == 0)
            {
                DeleteObject(region);
                AbortState(state, "SetWindowRgn failed");
                return;
            }

            state.Applied = true;
            state.LastSignature = signature;
            AppliedCount++;
            LastAppliedSignature = signature;
            RefreshComposition(state.Hwnd);
        }

        private static ulong FastKey(TaskbarState state, AutoHideGeometry.State autoHideState, RECT window, uint dpi, DynamicInput input)
        {
            ulong hash = 1469598103934665603UL;
            hash = Mix(hash, (ulong)autoHideState);
            hash = Mix(hash, (ulong)(uint)window.Left);
            hash = Mix(hash, (ulong)(uint)window.Top);
            hash = Mix(hash, (ulong)(uint)window.Right);
            hash = Mix(hash, (ulong)(uint)window.Bottom);
            hash = Mix(hash, dpi);
            hash = Mix(hash, (ulong)(uint)state.TemplateRevision);
            hash = Mix(hash, input.AppListVisible ? 1UL : 0);
            hash = Mix(hash, input.ShowTray ? 1UL : 0);
            hash = Mix(hash, input.ShowWidgets ? 1UL : 0);
            hash = Mix(hash, (ulong)(uint)input.AppListRight);
            hash = Mix(hash, (ulong)(uint)input.TrayLeft);
            hash = Mix(hash, input.HasTray ? 1UL : 0);
            hash = Mix(hash, input.ShowSegmentsOnHover ? 1UL : 0);
            hash = Mix(hash, input.IsCentred ? 1UL : 0);
            hash = Mix(hash, input.ShowAppList ? 1UL : 0);
            return hash;
        }

        private static ulong Mix(ulong hash, ulong value)
        {
            unchecked
            {
                hash ^= value;
                hash *= 1099511628211UL;
                return hash;
            }
        }

        private static string DynamicSignature(AutoHideGeometry.State state, RECT window, DynamicPlan plan,
            bool includePeek, AutoHideGeometry.Peek peek)
        {
            StringBuilder signature = new StringBuilder();
            signature.Append(state).Append(':')
                .Append(window.Left).Append(',').Append(window.Top).Append(',')
                .Append(window.Right).Append(',').Append(window.Bottom);
            foreach (Segment segment in plan.Segments)
            {
                signature.Append('|').Append(segment.Left).Append(',').Append(segment.Top).Append(',')
                    .Append(segment.Right).Append(',').Append(segment.Bottom).Append(',').Append(segment.Radius);
            }
            foreach (SegmentRange range in plan.PeekRanges)
            {
                signature.Append('!').Append(range.Left).Append(',').Append(range.Right);
            }
            if (includePeek)
            {
                signature.Append('@').Append(peek.Bounds.Left).Append(',').Append(peek.Bounds.Top).Append(',')
                    .Append(peek.Bounds.Right).Append(',').Append(peek.Bounds.Bottom);
            }
            return signature.ToString();
        }

        private static bool HasTopInset(DynamicInput input, uint dpi)
        {
            double scale = dpi / 96.0;
            if (input.ShowAppList && Convert.ToInt32(input.App.MarginTop * scale) > 0) return true;
            if ((input.TrayHoverEnabled || input.ShowTray) && Convert.ToInt32(input.Tray.MarginTop * scale) > 0) return true;
            if (input.ShowWidgets && Convert.ToInt32(input.Widgets.MarginTop * scale) > 0) return true;
            return false;
        }

        internal static bool ShouldKeepHoverRegion(bool noSegments, bool includePeek, bool hover, bool taskbarVisible)
        {
            return noSegments && !includePeek && hover && taskbarVisible;
        }

        private static bool IsWindowRegionEmpty(IntPtr hwnd)
        {
            IntPtr scratch = CreateRectRgn(0, 0, 0, 0);
            if (scratch == IntPtr.Zero) return false;
            int kind = GetWindowRgn(hwnd, scratch);
            DeleteObject(scratch);
            return kind == 0;
        }

        private static void RefreshComposition(IntPtr hwnd)
        {
            if (!compositionCompat || hwnd == IntPtr.Zero) return;
            IntPtr worker = FindWindow("TTB_WorkerWindow", "TTB_WorkerWindow");
            uint message = RegisterWindowMessage("TTB_ForceRefreshTaskbar");
            if (worker != IntPtr.Zero && message != 0)
            {
                SendMessage(worker, message, IntPtr.Zero, hwnd);
            }
        }

        private static void ClearRegion(IntPtr hwnd)
        {
            if (hwnd != IntPtr.Zero) SetWindowRgn(hwnd, IntPtr.Zero, true);
        }

        internal static void Restore()
        {
            foreach (IntPtr hwnd in new List<IntPtr>(Order))
            {
                ClearRegion(hwnd);
                RefreshComposition(hwnd);
            }
            States.Clear();
            Order.Clear();
        }

        private static void AbortState(TaskbarState state, string reason)
        {
            Failure = reason;
            ClearRegion(state.Hwnd);
            state.Applied = false;
            state.FilledForWindow = false;
            state.LastSignature = "";
            state.HasFastKey = false;
        }

        private static AutoHideGeometry.Rect ToGeometryRect(RECT rect)
        {
            return new AutoHideGeometry.Rect(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
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
        private struct POINT
        {
            public int X, Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT
        {
            public uint Length;
            public uint Flags;
            public uint ShowCommand;
            public POINT MinPosition;
            public POINT MaxPosition;
            public RECT NormalPosition;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public uint Size;
            public RECT Monitor;
            public RECT WorkArea;
            public uint Flags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string message);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hwnd, ref WINDOWPLACEMENT placement);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, uint attribute, out uint value, uint size);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll")]
        private static extern int GetWindowRgn(IntPtr hwnd, IntPtr region);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr obj);

        [DllImport("shell32.dll")]
        private static extern UIntPtr SHAppBarMessage(uint message, ref APPBARDATA data);
    }
}
