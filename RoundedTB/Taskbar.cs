using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using System.Windows.Automation;



namespace RoundedTB
{
    class Taskbar
    {
        /// <summary>
        /// Checks if the taskbar is centred.
        /// </summary>
        /// <returns>
        /// A bool indicating if the taskbar is centred.
        /// </returns>
        public static bool CheckIfCentred()
        {
            bool retVal;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced"))
                {
                    if (key != null)
                    {
                        int val = (int)key.GetValue("TaskbarAl");

                        if (val == 1)
                        {
                            retVal = true;
                        }
                        else
                        {
                            retVal = false;
                        }
                    }
                    else
                    {
                        retVal = false;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
            return retVal;
        }

        /// <summary>
        /// Compares two taskbars' rects to see if they've changed
        /// </summary>
        /// <returns>
        /// a bool indicating if the taskbar's, applist's, and tray's rects rects have changed.
        /// </returns>
        public static bool TaskbarRefreshRequired(Types.Taskbar currentTB, Types.Taskbar newTB, bool isDynamic)
        {
            // REMINDER: newTB will only have rect & hwnd info. Everything else will be null.


            bool taskbarRectChanged = true;
            bool appListRectChanged = true;
            bool trayRectChanged = true;

            if (
                currentTB.TaskbarRect.Left == newTB.TaskbarRect.Left &&
                currentTB.TaskbarRect.Top == newTB.TaskbarRect.Top &&
                currentTB.TaskbarRect.Right == newTB.TaskbarRect.Right &&
                currentTB.TaskbarRect.Bottom == newTB.TaskbarRect.Bottom)
            {
                taskbarRectChanged = false;
            }
            if (
                currentTB.AppListRect.Left == newTB.AppListRect.Left &&
                currentTB.AppListRect.Top == newTB.AppListRect.Top &&
                currentTB.AppListRect.Right == newTB.AppListRect.Right &&
                currentTB.AppListRect.Bottom == newTB.AppListRect.Bottom)
            {
                appListRectChanged = false;
            }
            if (
                (currentTB.TrayRect.Left + 5 >= newTB.TrayRect.Left && currentTB.TrayRect.Left - 5 <= newTB.TrayRect.Left) &&
                currentTB.TrayRect.Top == newTB.TrayRect.Top &&
                currentTB.TrayRect.Right == newTB.TrayRect.Right &&
                currentTB.TrayRect.Bottom == newTB.TrayRect.Bottom)
            {
                trayRectChanged = false;
            }

            if (isDynamic && (taskbarRectChanged || appListRectChanged || trayRectChanged))
            {
                return true;
            }
            else if (!isDynamic && taskbarRectChanged)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the rects of the three components of the taskbar from their respective handles.
        /// </summary>
        /// <returns>
        /// a partial Taskbar containing just rects and handles.
        /// </returns>
        public static Types.Taskbar GetQuickTaskbarRects(IntPtr taskbarHwnd, IntPtr trayHwnd, IntPtr appListHwnd)
        {
            LocalPInvoke.GetWindowRect(taskbarHwnd, out LocalPInvoke.RECT taskbarRectCheck);
            LocalPInvoke.GetWindowRect(trayHwnd, out LocalPInvoke.RECT trayRectCheck);
            LocalPInvoke.GetWindowRect(appListHwnd, out LocalPInvoke.RECT appListRectCheck);

            return new Types.Taskbar()
            {
                TaskbarHwnd = taskbarHwnd,
                TrayHwnd = trayHwnd,
                AppListHwnd = appListHwnd,
                TaskbarRect = taskbarRectCheck,
                TrayRect = trayRectCheck,
                AppListRect = appListRectCheck
            };
        }

        public enum AutoHideState
        {
            Visible,        // Fully on its monitor.
            Hidden,         // Settled at the screen edge with only the peek strip visible.
            Transitioning,  // Sliding in or out.
            OffScreen       // Entirely outside its monitor, so nothing is hit-testable.
        }

        /// <summary>
        /// Classifies where the shell has put the taskbar relative to its monitor.
        /// With auto-hide the shell slides the whole window past the screen edge.
        /// Because RoundedTB clips that window, the still-visible "peek" strip has
        /// to stay inside the region: Windows only reveals the bar when the cursor
        /// is over a hit-testable taskbar pixel at the screen edge.
        /// </summary>
        public static AutoHideState GetAutoHideState(IntPtr taskbarHwnd, LocalPInvoke.RECT taskbarRect, double scaleFactor, out LocalPInvoke.RECT peekRect)
        {
            peekRect = new LocalPInvoke.RECT();

            try
            {
                IntPtr monitor = LocalPInvoke.MonitorFromWindow(taskbarHwnd, 2u);
                if (monitor == IntPtr.Zero)
                {
                    return AutoHideState.Visible;
                }

                MonitorStuff.MONITORINFO monitorInfo = new MonitorStuff.MONITORINFO();
                monitorInfo.cbSize = (uint)Marshal.SizeOf(monitorInfo);
                if (!MonitorStuff.GetMonitorInfo(monitor, ref monitorInfo))
                {
                    return AutoHideState.Visible;
                }

                LocalPInvoke.RECT screen = monitorInfo.rcMonitor;
                int width = taskbarRect.Right - taskbarRect.Left;
                int height = taskbarRect.Bottom - taskbarRect.Top;
                if (width <= 0 || height <= 0)
                {
                    return AutoHideState.Visible;
                }

                // Fully on screen: normal operation.
                const int tolerance = 2;
                if (taskbarRect.Left >= screen.Left - tolerance &&
                    taskbarRect.Top >= screen.Top - tolerance &&
                    taskbarRect.Right <= screen.Right + tolerance &&
                    taskbarRect.Bottom <= screen.Bottom + tolerance)
                {
                    return AutoHideState.Visible;
                }

                // Work out which screen edge the taskbar is sliding past. A
                // high-DPI taskbar can overhang more than one edge; the docked
                // edge is the one it overhangs the most.
                int bottomOverhang = taskbarRect.Bottom - screen.Bottom;
                int topOverhang = screen.Top - taskbarRect.Top;
                int leftOverhang = screen.Left - taskbarRect.Left;
                int rightOverhang = taskbarRect.Right - screen.Right;

                int stripLeft = Math.Max(taskbarRect.Left, screen.Left) - taskbarRect.Left;
                int stripRight = Math.Min(taskbarRect.Right, screen.Right) - taskbarRect.Left;
                int stripTop = Math.Max(taskbarRect.Top, screen.Top) - taskbarRect.Top;
                int stripBottom = Math.Min(taskbarRect.Bottom, screen.Bottom) - taskbarRect.Top;

                int peekThickness;
                if (bottomOverhang > 0 && bottomOverhang >= topOverhang && bottomOverhang >= leftOverhang && bottomOverhang >= rightOverhang)
                {
                    // Bottom-docked: the top rows of the window are still visible.
                    peekThickness = Math.Min(height, Math.Max(0, screen.Bottom - taskbarRect.Top));
                    peekRect = new LocalPInvoke.RECT
                    {
                        Left = stripLeft,
                        Top = 0,
                        Right = stripRight,
                        Bottom = Math.Min(peekThickness, height)
                    };
                }
                else if (topOverhang > 0 && topOverhang >= leftOverhang && topOverhang >= rightOverhang)
                {
                    // Top-docked: the bottom rows of the window are still visible.
                    peekThickness = Math.Min(height, Math.Max(0, taskbarRect.Bottom - screen.Top));
                    peekRect = new LocalPInvoke.RECT
                    {
                        Left = stripLeft,
                        Top = Math.Max(0, height - peekThickness),
                        Right = stripRight,
                        Bottom = height
                    };
                }
                else if (leftOverhang > 0 && leftOverhang >= rightOverhang)
                {
                    // Left-docked: the right columns of the window are still visible.
                    peekThickness = Math.Min(width, Math.Max(0, taskbarRect.Right - screen.Left));
                    peekRect = new LocalPInvoke.RECT
                    {
                        Left = Math.Max(0, width - peekThickness),
                        Top = stripTop,
                        Right = width,
                        Bottom = stripBottom
                    };
                }
                else if (rightOverhang > 0)
                {
                    // Right-docked: the left columns of the window are still visible.
                    peekThickness = Math.Min(width, Math.Max(0, screen.Right - taskbarRect.Left));
                    peekRect = new LocalPInvoke.RECT
                    {
                        Left = 0,
                        Top = stripTop,
                        Right = Math.Min(peekThickness, width),
                        Bottom = stripBottom
                    };
                }
                else
                {
                    return AutoHideState.Visible;
                }

                // Entirely off the monitor: no pixel can receive a hover event.
                if (peekThickness <= 0 || peekRect.Right <= peekRect.Left || peekRect.Bottom <= peekRect.Top)
                {
                    return AutoHideState.OffScreen;
                }

                // The shell leaves only a couple of pixels visible while hidden.
                // Anything more than that means the bar is still sliding.
                int peekThreshold = Math.Max(3, Convert.ToInt32(4 * scaleFactor));
                return peekThickness <= peekThreshold ? AutoHideState.Hidden : AutoHideState.Transitioning;
            }
            catch (Exception)
            {
                return AutoHideState.Visible;
            }
        }

        /// <summary>
        /// Adds the auto-hide peek strip to a region so it remains hit-testable.
        /// Takes ownership of <paramref name="region"/> and returns the new region.
        /// </summary>
        private static IntPtr AddPeekRegion(IntPtr region, LocalPInvoke.RECT peekRect)
        {
            if (peekRect.Right <= peekRect.Left || peekRect.Bottom <= peekRect.Top)
            {
                return region;
            }

            IntPtr peekRegion = LocalPInvoke.CreateRectRgn(peekRect.Left, peekRect.Top, peekRect.Right, peekRect.Bottom);
            if (peekRegion == IntPtr.Zero)
            {
                return region;
            }

            if (region == IntPtr.Zero)
            {
                return peekRegion;
            }

            IntPtr combined = LocalPInvoke.CreateRectRgn(0, 0, 0, 0);
            if (combined == IntPtr.Zero || LocalPInvoke.CombineRgn(combined, region, peekRegion, 2) == 0)
            {
                if (combined != IntPtr.Zero)
                {
                    LocalPInvoke.DeleteObject(combined);
                }
                LocalPInvoke.DeleteObject(peekRegion);
                return region;
            }

            LocalPInvoke.DeleteObject(peekRegion);
            LocalPInvoke.DeleteObject(region);
            return combined;
        }

        /// <summary>
        /// Returns true while the shell is responsible for hiding the taskbar.
        /// </summary>
        public static bool IsAutoHideActive(IntPtr taskbarHwnd)
        {
            try
            {
                return (GetTaskbarState(taskbarHwnd) & LocalPInvoke.AppBarStates.AutoHide) == LocalPInvoke.AppBarStates.AutoHide;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Re-initialises the shell's auto-hide state. This is what toggling the
        /// Windows auto-hide setting does, and it is the only way to recover a
        /// taskbar that the shell left fully off-screen (nothing can be
        /// hit-tested at the screen edge, so the shell cannot slide it back).
        /// </summary>
        public static void ReinitializeAutoHide(IntPtr taskbarHwnd)
        {
            SetTaskbarState(LocalPInvoke.AppBarStates.AlwaysOnTop, taskbarHwnd);
            SetTaskbarState(LocalPInvoke.AppBarStates.AutoHide, taskbarHwnd);
        }

        /// <summary>
        /// Returns true when the foreground window covers the taskbar's whole
        /// monitor, e.g. a full-screen app. The shell may legitimately keep the
        /// taskbar fully hidden then, so the watchdog must not interfere.
        /// </summary>
        public static bool IsFullscreenWindowOnMonitor(IntPtr taskbarHwnd)
        {
            try
            {
                IntPtr monitor = LocalPInvoke.MonitorFromWindow(taskbarHwnd, 2u);
                if (monitor == IntPtr.Zero)
                {
                    return false;
                }

                MonitorStuff.MONITORINFO monitorInfo = new MonitorStuff.MONITORINFO();
                monitorInfo.cbSize = (uint)Marshal.SizeOf(monitorInfo);
                if (!MonitorStuff.GetMonitorInfo(monitor, ref monitorInfo))
                {
                    return false;
                }

                IntPtr foreground = LocalPInvoke.GetForegroundWindow();
                if (foreground == IntPtr.Zero || !LocalPInvoke.GetWindowRect(foreground, out LocalPInvoke.RECT rect))
                {
                    return false;
                }

                LocalPInvoke.RECT screen = monitorInfo.rcMonitor;
                return rect.Left <= screen.Left && rect.Top <= screen.Top &&
                       rect.Right >= screen.Right && rect.Bottom >= screen.Bottom;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Returns true when the taskbar currently has an empty window region,
        /// i.e. nothing of it can be hit-testable.
        /// </summary>
        private static bool IsWindowRegionEmpty(IntPtr taskbarHwnd)
        {
            IntPtr region = LocalPInvoke.CreateRectRgn(0, 0, 0, 0);
            if (region == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                // 0 means the window has no region (fully visible), 1 means the
                // region exists but is empty.
                return LocalPInvoke.GetWindowRgn(taskbarHwnd, region) == 1;
            }
            finally
            {
                LocalPInvoke.DeleteObject(region);
            }
        }

        /// <summary>
        /// Reads the taskbar's current rect. Callers can hold a stale rect (for
        /// example the settings window applies regions from its cached taskbar
        /// list), which would misclassify a hidden bar as visible.
        /// </summary>
        private static LocalPInvoke.RECT GetLiveTaskbarRect(Types.Taskbar taskbar)
        {
            if (LocalPInvoke.GetWindowRect(taskbar.TaskbarHwnd, out LocalPInvoke.RECT liveRect) &&
                liveRect.Right > liveRect.Left && liveRect.Bottom > liveRect.Top)
            {
                return liveRect;
            }

            return taskbar.TaskbarRect;
        }

        /// <summary>
        /// Resets the specified taskbar.
        /// </summary>
        public static void ResetTaskbar(Types.Taskbar taskbar, Types.Settings settings)
        {
            LocalPInvoke.SetWindowRgn(taskbar.TaskbarHwnd, IntPtr.Zero, true);
            LocalPInvoke.SetLayeredWindowAttributes(taskbar.TaskbarHwnd, 0, 255, LocalPInvoke.LWA_ALPHA);
            int style = LocalPInvoke.GetWindowLong(taskbar.TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32();
            if ((style & LocalPInvoke.WS_EX_LAYERED) == LocalPInvoke.WS_EX_LAYERED)
            {
                LocalPInvoke.SetWindowLong(taskbar.TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE, LocalPInvoke.GetWindowLong(taskbar.TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32() ^ LocalPInvoke.WS_EX_LAYERED);
            }
            style = LocalPInvoke.GetWindowLong(taskbar.TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32();
            if ((style & LocalPInvoke.WS_EX_TRANSPARENT) == LocalPInvoke.WS_EX_TRANSPARENT)
            {
                LocalPInvoke.SetWindowLong(taskbar.TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE, LocalPInvoke.GetWindowLong(taskbar.TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32() ^ LocalPInvoke.WS_EX_TRANSPARENT);
            }

            if (settings.CompositionCompat)
            {
                Interaction.UpdateTranslucentTB(taskbar.TaskbarHwnd);
            }
        }

        /// <summary>
        /// Creates a basic region for a specific taskbar and applies it.
        /// </summary>
        /// <returns>
        /// a bool indicating success.
        /// </returns>
        public static bool UpdateSimpleTaskbar(Types.Taskbar taskbar, Types.Settings settings)
        {
            try
            {
                // Create an effective region to be applied to the taskbar
                Types.EffectiveRegion taskbarEffectiveRegion = new Types.EffectiveRegion
                {
                    CornerRadius = Convert.ToInt32(settings.SimpleTaskbarLayout.CornerRadius * taskbar.ScaleFactor),
                    Top = Convert.ToInt32(settings.SimpleTaskbarLayout.MarginTop * taskbar.ScaleFactor),
                    Left = Convert.ToInt32(settings.SimpleTaskbarLayout.MarginLeft * taskbar.ScaleFactor),
                    Width = Convert.ToInt32(taskbar.TaskbarRect.Right - taskbar.TaskbarRect.Left - (settings.SimpleTaskbarLayout.MarginRight * taskbar.ScaleFactor)) + 1,
                    Height = Convert.ToInt32(taskbar.TaskbarRect.Bottom - taskbar.TaskbarRect.Top - (settings.SimpleTaskbarLayout.MarginBottom * taskbar.ScaleFactor)) + 1
                };

                IntPtr region = LocalPInvoke.CreateRoundRectRgn(taskbarEffectiveRegion.Left, taskbarEffectiveRegion.Top, taskbarEffectiveRegion.Width, taskbarEffectiveRegion.Height, taskbarEffectiveRegion.CornerRadius, taskbarEffectiveRegion.CornerRadius);
                if (GetAutoHideState(taskbar.TaskbarHwnd, GetLiveTaskbarRect(taskbar), taskbar.ScaleFactor, out LocalPInvoke.RECT peekRect) == AutoHideState.Hidden)
                {
                    region = AddPeekRegion(region, peekRect);
                }
                LocalPInvoke.SetWindowRgn(taskbar.TaskbarHwnd, region, true);
                if (settings.CompositionCompat)
                {
                    Interaction.UpdateTranslucentTB(taskbar.TaskbarHwnd);
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Creates a dynamic region for a specific taskbar and applies it.
        /// </summary>
        /// <returns>
        /// a bool indicating success.
        /// </returns>
        public static bool UpdateDynamicTaskbar(Types.Taskbar taskbar, Types.Settings settings)
        {
            try
            {
                IntPtr mainRegion = IntPtr.Zero;
                IntPtr workingRegion = LocalPInvoke.CreateRoundRectRgn(1, 1, 1, 1, 0, 0);
                int centredDistanceFromEdge = 0;
                AutoHideState autoHideState = GetAutoHideState(taskbar.TaskbarHwnd, GetLiveTaskbarRect(taskbar), taskbar.ScaleFactor, out LocalPInvoke.RECT autoHidePeekRect);
                bool showAppList = (settings.ShowAppList ?? true)
                    && (!settings.ShowSegmentsOnHover || taskbar.AppListVisible);

                // Create an effective region to be applied to the taskbar for the applist
                Types.EffectiveRegion taskbarEffectiveRegion = new Types.EffectiveRegion
                {
                    CornerRadius = Convert.ToInt32(settings.DynamicAppListLayout.CornerRadius * taskbar.ScaleFactor),
                    Top = Convert.ToInt32(settings.DynamicAppListLayout.MarginTop * taskbar.ScaleFactor),
                    Left = Convert.ToInt32(settings.DynamicAppListLayout.MarginLeft * taskbar.ScaleFactor),
                    Width = Convert.ToInt32(taskbar.TaskbarRect.Right - taskbar.TaskbarRect.Left - (settings.DynamicAppListLayout.MarginRight * taskbar.ScaleFactor)) + 1,
                    Height = Convert.ToInt32(taskbar.TaskbarRect.Bottom - taskbar.TaskbarRect.Top - (settings.DynamicAppListLayout.MarginBottom * taskbar.ScaleFactor)) + 1
                };

                // Create an effective region to be applied to the taskbar for the applist
                Types.EffectiveRegion centredEffectiveRegion = new Types.EffectiveRegion
                {
                    CornerRadius = Convert.ToInt32(settings.DynamicAppListLayout.CornerRadius * taskbar.ScaleFactor),
                    Top = Convert.ToInt32(settings.DynamicAppListLayout.MarginTop * taskbar.ScaleFactor),
                    Left = Convert.ToInt32(settings.DynamicAppListLayout.MarginRight * taskbar.ScaleFactor) - 1,
                    Width = Convert.ToInt32(taskbar.TaskbarRect.Right - taskbar.TaskbarRect.Left - (settings.DynamicAppListLayout.MarginRight * taskbar.ScaleFactor)) + 1,
                    Height = Convert.ToInt32(taskbar.TaskbarRect.Bottom - taskbar.TaskbarRect.Top - (settings.DynamicAppListLayout.MarginBottom * taskbar.ScaleFactor)) + 1
                };

                // Create an effective region to be applied to the taskbar for the tray
                Types.EffectiveRegion trayEffectiveRegion = new Types.EffectiveRegion
                {
                    CornerRadius = Convert.ToInt32(settings.DynamicTrayLayout.CornerRadius * taskbar.ScaleFactor),
                    Top = Convert.ToInt32(settings.DynamicTrayLayout.MarginTop * taskbar.ScaleFactor),
                    Left = Convert.ToInt32(settings.DynamicTrayLayout.MarginLeft * taskbar.ScaleFactor),
                    Width = Convert.ToInt32(taskbar.TaskbarRect.Right - taskbar.TaskbarRect.Left - (settings.DynamicTrayLayout.MarginRight * taskbar.ScaleFactor)) + 1,
                    Height = Convert.ToInt32(taskbar.TaskbarRect.Bottom - taskbar.TaskbarRect.Top - (settings.DynamicTrayLayout.MarginBottom * taskbar.ScaleFactor)) + 1
                };

                Types.EffectiveRegion widgetsEffectiveRegion = new Types.EffectiveRegion
                {
                    CornerRadius = Convert.ToInt32(settings.DynamicWidgetsLayout.CornerRadius * taskbar.ScaleFactor),
                    Top = Convert.ToInt32(settings.DynamicWidgetsLayout.MarginTop * taskbar.ScaleFactor),
                    Left = Convert.ToInt32(settings.DynamicWidgetsLayout.MarginLeft * taskbar.ScaleFactor),
                    Width = Convert.ToInt32(168 * taskbar.ScaleFactor - (settings.DynamicWidgetsLayout.MarginRight * taskbar.ScaleFactor)) + 1,
                    Height = Convert.ToInt32(taskbar.TaskbarRect.Bottom - taskbar.TaskbarRect.Top - (settings.DynamicWidgetsLayout.MarginBottom * taskbar.ScaleFactor)) + 1
                };

                centredDistanceFromEdge = taskbar.TaskbarRect.Right - taskbar.AppListRect.Right - Convert.ToInt32(2 * taskbar.ScaleFactor);

                // If on Windows 10, add an extra 20 logical pixels for the grabhandle
                if (!settings.IsWindows11)
                {
                    centredDistanceFromEdge -= Convert.ToInt32(20 * taskbar.ScaleFactor);
                }

                // Create the main (app list) segment unless the user has hidden it.
                if (showAppList)
                {
                    // Create region for if the taskbar is centred by take the right-to-right distance (centredDistanceFromEdge) off from both sides, as well as the margin
                    if (settings.IsCentred)
                    {
                        mainRegion = LocalPInvoke.CreateRoundRectRgn(
                            centredDistanceFromEdge + centredEffectiveRegion.Left,
                            centredEffectiveRegion.Top,
                            centredEffectiveRegion.Width - centredDistanceFromEdge,
                            centredEffectiveRegion.Height,
                            centredEffectiveRegion.CornerRadius,
                            centredEffectiveRegion.CornerRadius
                            );
                    }

                    // Create a region for if the taskbar is left-aligned, right-to-right distance (centredDistanceFromEdge) off from the right-hand side, as well as the margin
                    else
                    {

                        mainRegion = LocalPInvoke.CreateRoundRectRgn(
                            taskbarEffectiveRegion.Left,
                            taskbarEffectiveRegion.Top,
                            taskbarEffectiveRegion.Width - centredDistanceFromEdge,
                            taskbarEffectiveRegion.Height,
                            taskbarEffectiveRegion.CornerRadius,
                            taskbarEffectiveRegion.CornerRadius
                            );
                    }
                }

                // If the user has it enabled and the tray handle isn't null, create a region for the system tray and merge it with the taskbar region
                if (settings.ShowTray && taskbar.TrayHwnd != IntPtr.Zero)
                {
                    IntPtr trayRegion = LocalPInvoke.CreateRoundRectRgn(
                        (taskbar.TrayRect.Left - taskbar.TaskbarRect.Left) - trayEffectiveRegion.Left,
                        trayEffectiveRegion.Top,
                        trayEffectiveRegion.Width,
                        trayEffectiveRegion.Height,
                        trayEffectiveRegion.CornerRadius,
                        trayEffectiveRegion.CornerRadius
                        );

                    if (mainRegion == IntPtr.Zero)
                    {
                        mainRegion = trayRegion;
                    }
                    else
                    {
                        LocalPInvoke.CombineRgn(workingRegion, trayRegion, mainRegion, 2);
                        mainRegion = workingRegion;
                    }
                }

                if (settings.ShowWidgets)
                {
                    IntPtr widgetsRegion = LocalPInvoke.CreateRoundRectRgn(
                        widgetsEffectiveRegion.Left,
                        widgetsEffectiveRegion.Top,
                        widgetsEffectiveRegion.Width,
                        widgetsEffectiveRegion.Height,
                        widgetsEffectiveRegion.CornerRadius,
                        widgetsEffectiveRegion.CornerRadius
                        );

                    if (mainRegion == IntPtr.Zero)
                    {
                        mainRegion = widgetsRegion;
                    }
                    else
                    {
                        LocalPInvoke.CombineRgn(workingRegion, widgetsRegion, mainRegion, 2);
                        mainRegion = workingRegion;
                    }
                }

                if (mainRegion == IntPtr.Zero)
                {
                    if (autoHideState == AutoHideState.Hidden)
                    {
                        // Keep the peek strip hit-testable even though no segment
                        // is visible, otherwise the shell can never reveal the bar.
                        mainRegion = AddPeekRegion(IntPtr.Zero, autoHidePeekRect);
                    }

                    if (mainRegion == IntPtr.Zero && settings.ShowSegmentsOnHover)
                    {
                        // Never leave a shell-managed auto-hide taskbar without a
                        // hit-testable pixel. An empty region makes the bar
                        // impossible to trigger at the screen edge and can strand
                        // it invisible even after RoundedTB exits. Keep the last
                        // region instead; once the bar has slid away the Hidden
                        // branch installs the peek strip above.
                        if (IsAutoHideActive(taskbar.TaskbarHwnd))
                        {
                            if (!IsWindowRegionEmpty(taskbar.TaskbarHwnd))
                            {
                                return true;
                            }

                            // The region is already empty (for example auto-hide
                            // was switched on after hover mode had hidden every
                            // segment). Restore the full window so the shell can
                            // hit-test and hide it; the peek strip is applied once
                            // it has slid away.
                            ResetTaskbar(taskbar, settings);
                            return true;
                        }

                        // Hover mode intentionally hides every segment at rest.
                        // Apply an empty region so the full taskbar (including the
                        // app list) does not flash into view during auto-hide.
                        IntPtr emptyRegion = LocalPInvoke.CreateRectRgn(0, 0, 0, 0);
                        if (emptyRegion != IntPtr.Zero)
                        {
                            LocalPInvoke.SetWindowRgn(taskbar.TaskbarHwnd, emptyRegion, true);
                        }
                        else
                        {
                            ResetTaskbar(taskbar, settings);
                        }
                        if (settings.CompositionCompat)
                        {
                            Interaction.UpdateTranslucentTB(taskbar.TaskbarHwnd);
                        }
                        return true;
                    }

                    if (mainRegion == IntPtr.Zero)
                    {
                        // Every segment is hidden outside hover mode. Restore the taskbar
                        // rather than leaving the user with an invisible taskbar.
                        ResetTaskbar(taskbar, settings);
                        return true;
                    }
                }
                else if (autoHideState == AutoHideState.Hidden)
                {
                    mainRegion = AddPeekRegion(mainRegion, autoHidePeekRect);
                }

                // Apply the final region to the taskbar
                LocalPInvoke.SetWindowRgn(taskbar.TaskbarHwnd, mainRegion, true);
                if (settings.CompositionCompat)
                {
                    Interaction.UpdateTranslucentTB(taskbar.TaskbarHwnd);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }

        }

        /// <summary>
        /// Checks if there are any new taskbars, or if any taskbars are no longer present.
        /// </summary>
        /// <returns>
        /// a bool indicating success.
        /// </returns>
        public static bool TaskbarCountOrHandleChanged(int taskbarCount, IntPtr mainTaskbarHandle)
        {
            List<IntPtr> currentTaskbars = new List<IntPtr>();
            bool otherTaskbarsExist = true;
            IntPtr hwndPrevious = IntPtr.Zero;
            currentTaskbars.Add(LocalPInvoke.FindWindowExA(IntPtr.Zero, hwndPrevious, "Shell_TrayWnd", null));

            if (currentTaskbars[0] == IntPtr.Zero)
            {
                return false;
            }

            if (currentTaskbars[0] != mainTaskbarHandle)
            {
                return true;
            }

            while (otherTaskbarsExist)
            {
                IntPtr hwndCurrent = LocalPInvoke.FindWindowExA(IntPtr.Zero, hwndPrevious, "Shell_SecondaryTrayWnd", null);
                hwndPrevious = hwndCurrent;

                if (hwndCurrent == IntPtr.Zero)
                {
                    otherTaskbarsExist = false;
                }
                else
                {
                    currentTaskbars.Add(hwndCurrent);
                }
            }
            if (currentTaskbars.Count != taskbarCount)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if the provided update is valid.
        /// </summary>
        /// <returns>
        /// A bool indicating if the update is valid.
        /// </returns>
        public static bool CheckDynamicUpdateIsValid(Types.Taskbar currentTB, Types.Taskbar newTB, bool appListHidden = false)
        {
            // REMINDER: newTB will only have rect & hwnd info. Everything else will be null.

            // Check if either of the supplied taskbars are null
            if (currentTB == null || newTB == null)
            {
                return false;
            }

            // Check if the taskbar handles are different
            if (currentTB.TaskbarHwnd != newTB.TaskbarHwnd)
            {
                return false;
            }

            // The app list measurements are only used to build the main segment.
            // When that segment is hidden, they are irrelevant (and a full taskbar
            // would otherwise fail the width checks below).
            if (appListHidden)
            {
                return true;
            }

            // Get width of app list. Not strictly necessary as the applist is always measured from the left but doing so just in case
            int newAppListWidth = newTB.AppListRect.Right - newTB.AppListRect.Left;
            int currentAppListWidth = currentTB.AppListRect.Right - currentTB.AppListRect.Left;

            if (newTB.AppListRect.Right >= newTB.TrayRect.Left && newTB.TrayRect.Left != 0)
            {
                return false;
            }

            if (newAppListWidth == newTB.TrayRect.Left && newTB.TrayRect.Left != 0)
            {
                return false;
            }

            if (newAppListWidth <= 20 * currentTB.ScaleFactor && newAppListWidth != 0)
            {
                return false;
            }

            if (newAppListWidth >= newTB.TaskbarRect.Right - newTB.TaskbarRect.Left && newAppListWidth != 0)
            {
                return false;
            }

            Debug.WriteLine($"Old width: {currentAppListWidth}\nNew width: {newAppListWidth}");
            return true;
        }

        /// <summary>
        /// Reads the right edge of the real taskbar app buttons from the Windows 11
        /// XAML content bridge. The legacy MSTaskSwWClass window only covers part
        /// of the buttons on recent Windows 11 builds.
        /// </summary>
        public static int GetAppListRightFromUia(IntPtr bridgeHwnd)
        {
            if (bridgeHwnd == IntPtr.Zero)
            {
                return 0;
            }

            try
            {
                AutomationElement bridge = AutomationElement.FromHandle(bridgeHwnd);
                if (bridge == null)
                {
                    return 0;
                }

                System.Windows.Automation.Condition condition = new System.Windows.Automation.PropertyCondition(
                    AutomationElement.ClassNameProperty,
                    "Taskbar.TaskListButtonAutomationPeer");
                AutomationElementCollection buttons = bridge.FindAll(TreeScope.Descendants, condition);

                double right = 0;
                foreach (AutomationElement button in buttons)
                {
                    double buttonRight = button.Current.BoundingRectangle.Right;
                    if (buttonRight > right)
                    {
                        right = buttonRight;
                    }
                }

                return right > 0 ? (int)Math.Ceiling(right) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Reads the system tray bounds from the Windows 11 XAML taskbar.
        /// The legacy TrayNotifyWnd rect can stay 0,0,0,0 during logon.
        /// </summary>
        public static bool GetTrayRectFromUia(IntPtr bridgeHwnd, out LocalPInvoke.RECT trayRect)
        {
            trayRect = new LocalPInvoke.RECT();
            if (bridgeHwnd == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                AutomationElement bridge = AutomationElement.FromHandle(bridgeHwnd);
                if (bridge == null)
                {
                    return false;
                }

                AutomationElementCollection elements = bridge.FindAll(
                    TreeScope.Descendants,
                    System.Windows.Automation.Condition.TrueCondition);

                int left = int.MaxValue;
                int top = int.MaxValue;
                int right = int.MinValue;
                int bottom = int.MinValue;
                foreach (AutomationElement element in elements)
                {
                    string className = element.Current.ClassName;
                    if (string.IsNullOrEmpty(className) || !className.StartsWith("SystemTray."))
                    {
                        continue;
                    }

                    var rect = element.Current.BoundingRectangle;
                    if (rect.Width <= 0 || rect.Height <= 0)
                    {
                        continue;
                    }

                    left = Math.Min(left, (int)rect.Left);
                    top = Math.Min(top, (int)rect.Top);
                    right = Math.Max(right, (int)rect.Right);
                    bottom = Math.Max(bottom, (int)rect.Bottom);
                }

                if (right <= left)
                {
                    return false;
                }

                trayRect.Left = left;
                trayRect.Top = top;
                trayRect.Right = right;
                trayRect.Bottom = bottom;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Collects information on any currently-present taskbars.
        /// </summary>
        /// <returns>
        /// A list of taskbars populated with information about their size, handles etc.
        /// </returns>
        public static List<Types.Taskbar> GenerateTaskbarInfo()
        {
            List<Types.Taskbar> retVal = new List<Types.Taskbar>();

            IntPtr hwndMain = LocalPInvoke.FindWindowExA(IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", null); // Find main taskbar
            LocalPInvoke.GetWindowRect(hwndMain, out LocalPInvoke.RECT rectMain); // Get the RECT of the main taskbar
            IntPtr hrgnMain = IntPtr.Zero; // Set recovery region to IntPtr.Zero
            IntPtr hwndTray = LocalPInvoke.FindWindowExA(hwndMain, IntPtr.Zero, "TrayNotifyWnd", null); // Get handle to the main taskbar's tray
            LocalPInvoke.GetWindowRect(hwndTray, out LocalPInvoke.RECT rectTray); // Get the RECT for the main taskbar's tray
            IntPtr hwndAppList = LocalPInvoke.FindWindowExA(LocalPInvoke.FindWindowExA(hwndMain, IntPtr.Zero, "ReBarWindow32", null), IntPtr.Zero, "MSTaskSwWClass", null); // Get the handle to the main taskbar's app list
            LocalPInvoke.GetWindowRect(hwndAppList, out LocalPInvoke.RECT rectAppList);// Get the RECT for the main taskbar's app list
            IntPtr hwndBridge = LocalPInvoke.FindWindowExA(hwndMain, IntPtr.Zero, "Windows.UI.Composition.DesktopWindowContentBridge", null); // Windows 11 XAML taskbar content

            // The legacy tray window can report an empty rect while the XAML tray
            // is already laid out; use the UIA tray bounds in that case.
            if (rectTray.Right <= rectTray.Left && GetTrayRectFromUia(hwndBridge, out LocalPInvoke.RECT uiaTrayRect))
            {
                rectTray = uiaTrayRect;
            }

            // On recent Windows 11 builds the legacy app list window only covers the
            // first few buttons; the XAML bridge has the real button positions.
            int uiaAppListRight = GetAppListRightFromUia(hwndBridge);
            if (uiaAppListRight > 0)
            {
                rectAppList.Right = uiaAppListRight;
            }

            retVal.Add(new Types.Taskbar
            {
                TaskbarHwnd = hwndMain,
                TrayHwnd = hwndTray,
                AppListHwnd = hwndAppList,
                BridgeHwnd = hwndBridge,
                TaskbarRect = rectMain,
                TrayRect = rectTray,
                AppListRect = rectAppList,
                RecoveryHrgn = hrgnMain,
                ScaleFactor = Convert.ToDouble(LocalPInvoke.GetDpiForWindow(hwndMain)) / 96.00,
                TaskbarRes = $"{rectMain.Right - rectMain.Left} x {rectMain.Bottom - rectMain.Top}",
                Ignored = false
            });
            int style = LocalPInvoke.GetWindowLong(hwndMain, LocalPInvoke.GWL_EXSTYLE).ToInt32();
            if ((style & LocalPInvoke.WS_EX_LAYERED) != LocalPInvoke.WS_EX_LAYERED)
            {
                LocalPInvoke.SetWindowLong(hwndMain, LocalPInvoke.GWL_EXSTYLE, LocalPInvoke.GetWindowLong(hwndMain, LocalPInvoke.GWL_EXSTYLE).ToInt32() ^ LocalPInvoke.WS_EX_LAYERED);
                LocalPInvoke.SetLayeredWindowAttributes(hwndMain, 0, 255, LocalPInvoke.LWA_ALPHA);
            }




            bool i = true;
            IntPtr hwndPrevious = IntPtr.Zero;
            while (i)
            {
                IntPtr hwndCurrent = LocalPInvoke.FindWindowExA(IntPtr.Zero, hwndPrevious, "Shell_SecondaryTrayWnd", null);
                hwndPrevious = hwndCurrent;

                if (hwndCurrent == IntPtr.Zero)
                {
                    i = false;
                }
                else
                {
                    LocalPInvoke.GetWindowRect(hwndCurrent, out LocalPInvoke.RECT rectCurrent);
                    LocalPInvoke.GetWindowRgn(hwndCurrent, out IntPtr hrgnCurrent);
                    Interaction interaction = new Interaction();
                    IntPtr hwndSecTray = IntPtr.Zero;
                    if (interaction.IsWindows11())
                    {
                        IntPtr imd = LocalPInvoke.FindWindowExA(hwndCurrent, IntPtr.Zero, "Windows.UI.Composition.DesktopWindowContentBridge", null);
                        hwndSecTray = LocalPInvoke.FindWindowExA(hwndCurrent, imd, "Windows.UI.Composition.DesktopWindowContentBridge", null);
                    }
                    else
                    {
                        hwndSecTray = LocalPInvoke.FindWindowExA(hwndCurrent, IntPtr.Zero, "TrayNotifyWnd", null); // Get handle to this secondary taskbar's tray
                    }
                    LocalPInvoke.GetWindowRect(hwndSecTray, out LocalPInvoke.RECT rectSecTray); // Get the RECT for this secondary taskbar's tray
                    IntPtr hwndSecAppList = LocalPInvoke.FindWindowExA(LocalPInvoke.FindWindowExA(hwndCurrent, IntPtr.Zero, "WorkerW", null), IntPtr.Zero, "MSTaskListWClass", null); // Get the handle to the main taskbar's app list
                    LocalPInvoke.GetWindowRect(hwndSecAppList, out LocalPInvoke.RECT rectSecAppList);// Get the RECT for this secondary taskbar's app list
                    IntPtr hwndSecBridge = LocalPInvoke.FindWindowExA(hwndCurrent, IntPtr.Zero, "Windows.UI.Composition.DesktopWindowContentBridge", null);
                    if (rectSecTray.Right <= rectSecTray.Left && GetTrayRectFromUia(hwndSecBridge, out LocalPInvoke.RECT secUiaTrayRect))
                    {
                        rectSecTray = secUiaTrayRect;
                    }
                    int secUiaRight = GetAppListRightFromUia(hwndSecBridge);
                    if (secUiaRight > 0)
                    {
                        rectSecAppList.Right = secUiaRight;
                    }
                    retVal.Add(new Types.Taskbar
                    {
                        TaskbarHwnd = hwndCurrent,
                        TrayHwnd = hwndSecTray,
                        AppListHwnd = hwndSecAppList,
                        BridgeHwnd = hwndSecBridge,
                        TaskbarRect = rectCurrent,
                        TrayRect = rectSecTray,
                        AppListRect = rectSecAppList,
                        RecoveryHrgn = hrgnCurrent,
                        ScaleFactor = Convert.ToDouble(LocalPInvoke.GetDpiForWindow(hwndCurrent)) / 96.00,
                        TaskbarRes = $"{rectCurrent.Right - rectCurrent.Left} x {rectCurrent.Bottom - rectCurrent.Top}",
                        Ignored = false
                    });
                    style = LocalPInvoke.GetWindowLong(hwndCurrent, LocalPInvoke.GWL_EXSTYLE).ToInt32();
                    if ((style & LocalPInvoke.WS_EX_LAYERED) != LocalPInvoke.WS_EX_LAYERED)
                    {
                        LocalPInvoke.SetWindowLong(hwndCurrent, LocalPInvoke.GWL_EXSTYLE, LocalPInvoke.GetWindowLong(hwndCurrent, LocalPInvoke.GWL_EXSTYLE).ToInt32() ^ LocalPInvoke.WS_EX_LAYERED);
                        LocalPInvoke.SetLayeredWindowAttributes(hwndCurrent, 0, 255, LocalPInvoke.LWA_ALPHA);
                    }
                }
            }

            //foreach (var tb in retVal)
            //{
            //    TaskbarShouldBeFilled(tb.TaskbarHwnd);
            //}
            return retVal;
        }

        /// <summary>
        /// Checks if the given taskbar should be filled to the edge of the screen.
        /// </summary>
        /// <returns>
        /// A bool indicating whether or not the taskbar needs to be filled.
        /// </returns>
        public static bool TaskbarShouldBeFilled(IntPtr taskbarHwnd, Types.Settings settings)
        {
            bool retVal = false;

            if (settings.FillOnMaximise)
            {
                // Attempt to check for if alt+tab/task switcher is open (Windows 11 only)
                IntPtr topHwnd = LocalPInvoke.WindowFromPoint(new LocalPInvoke.POINT() { x = 0, y = 0 });
                StringBuilder windowClass = new StringBuilder(1024);
                try
                {
                    LocalPInvoke.GetClassName(topHwnd, windowClass, 1024);

                    if (windowClass.ToString() == "XamlExplorerHostIslandWindow" && settings.FillOnTaskSwitch)
                    {
                        return true;
                    }
                }
                catch (Exception) { }

                List<IntPtr> windowList = Interaction.GetTopLevelWindows();
                foreach (IntPtr windowHwnd in windowList)
                {
                    if (LocalPInvoke.IsWindowVisible(windowHwnd))
                    {
                        if (LocalPInvoke.MonitorFromWindow(taskbarHwnd, 2) == LocalPInvoke.MonitorFromWindow(windowHwnd, 2))
                        {
                            LocalPInvoke.DwmGetWindowAttribute(windowHwnd, LocalPInvoke.DWMWINDOWATTRIBUTE.Cloaked, out bool isCloaked, 0x4);
                            if (!isCloaked)
                            {
                                LocalPInvoke.WINDOWPLACEMENT lpwndpl = new LocalPInvoke.WINDOWPLACEMENT();
                                LocalPInvoke.GetWindowPlacement(windowHwnd, ref lpwndpl);
                                if (lpwndpl.ShowCmd == LocalPInvoke.ShowWindowCommands.ShowMaximized)
                                {
                                    retVal = true;
                                }
                            }
                        }
                    }
                }
            }

            return retVal;
        }

        /// <summary>
        /// Sets the appbar properties of the taskbar.
        /// </summary>
        public static LocalPInvoke.AppBarStates GetTaskbarState(IntPtr hwnd)
        {
            LocalPInvoke.APPBARDATA msgData = new LocalPInvoke.APPBARDATA();
            msgData.cbSize = (uint)Marshal.SizeOf(msgData);
            msgData.hWnd = hwnd;
            IntPtr result = LocalPInvoke.SHAppBarMessage(LocalPInvoke.ABM.GetState, ref msgData);
            return (LocalPInvoke.AppBarStates)result.ToInt32();
        }

        public static void SetTaskbarState(LocalPInvoke.AppBarStates option, IntPtr hwnd)
        {
            LocalPInvoke.APPBARDATA msgData = new LocalPInvoke.APPBARDATA();
            msgData.cbSize = (uint)Marshal.SizeOf(msgData);
            msgData.hWnd = hwnd;
            msgData.lParam = (int)option;
            LocalPInvoke.SHAppBarMessage(LocalPInvoke.ABM.SetState, ref msgData);
        }


    }
}
