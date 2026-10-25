using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using System.Runtime.InteropServices;

namespace RoundedTB
{
    public class Background
    {
        // Just have a reference point for the Dispatcher
        public MainWindow mw;
        bool redrawOverride = false;
        int infrequentCount = 0;
        int periodicRefresh = 0;

        public Background()
        {
            mw = (MainWindow)Application.Current.MainWindow;
        }

        /// <summary>
        /// Returns the taskbar rect as it appears on screen. With auto-hide the
        /// window itself stays partly off-screen, so hover hit-testing against
        /// GetWindowRect would miss the visible bar.
        /// </summary>
        private static LocalPInvoke.RECT GetHoverTaskbarRect(Types.Taskbar taskbar)
        {
            LocalPInvoke.RECT rect = taskbar.TaskbarRect;
            try
            {
                IntPtr monitor = LocalPInvoke.MonitorFromWindow(taskbar.TaskbarHwnd, 2u);
                if (monitor == IntPtr.Zero)
                {
                    return rect;
                }

                MonitorStuff.MONITORINFO mi = new MonitorStuff.MONITORINFO();
                mi.cbSize = (uint)Marshal.SizeOf(mi);
                if (!MonitorStuff.GetMonitorInfo(monitor, ref mi))
                {
                    return rect;
                }

                int height = rect.Bottom - rect.Top;
                if (rect.Top >= mi.rcMonitor.Bottom - 2)
                {
                    // Hidden below the monitor: the visible position is flush with the bottom.
                    rect.Top = mi.rcMonitor.Bottom - height;
                    rect.Bottom = mi.rcMonitor.Bottom;
                }
                else if (rect.Bottom <= mi.rcMonitor.Top + 2)
                {
                    // Hidden above the monitor.
                    rect.Top = mi.rcMonitor.Top;
                    rect.Bottom = mi.rcMonitor.Top + height;
                }
            }
            catch (Exception)
            {
            }
            return rect;
        }


        // Main method for the BackgroundWorker - runs indefinitely
        public void DoWork(object sender, DoWorkEventArgs e)
        {
            mw.interaction.AddLog("in bw");
            BackgroundWorker worker = sender as BackgroundWorker;
            while (true)
            {
                try
                {
                    if (worker.CancellationPending == true)
                    {
                        mw.interaction.AddLog("cancelling");
                        e.Cancel = true;
                        break;
                    }

                    // Primary loop for the running process
                    else
                    {
                        // Section for running less important things without requiring an additional thread
                        infrequentCount++;
                        if (infrequentCount == 10)
                        {
                            // Check to see if settings need to be shown
                            List<IntPtr> windowList = Interaction.GetTopLevelWindows();
                            foreach (IntPtr hwnd in windowList)
                            {
                                StringBuilder windowClass = new StringBuilder(1024);
                                StringBuilder windowTitle = new StringBuilder(1024);
                                try
                                {
                                    LocalPInvoke.GetClassName(hwnd, windowClass, 1024);
                                    LocalPInvoke.GetWindowText(hwnd, windowTitle, 1024);

                                    if (windowClass.ToString().Contains("HwndWrapper[RoundedTB.exe") && windowTitle.ToString() == "RoundedTB_SettingsRequest")
                                    {
                                        mw.Dispatcher.Invoke(() =>
                                        {
                                            if (mw.Visibility != Visibility.Visible)
                                            {
                                                mw.ShowMenuItem_Click(null, null);
                                            }
                                        });
                                        LocalPInvoke.SetWindowText(hwnd, "RoundedTB");
                                    }
                                }
                                catch (Exception) { }
                            }

                            // Update tray icon
                            mw.Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    mw.TrayIconCheck();

                                }
                                catch (Exception)
                                {

                                }
                            });

                            // Refresh the real app-button extent from the Windows 11
                            // XAML taskbar. The legacy window stops at the first few
                            // buttons, so poll this less often than the rects.
                            foreach (Types.Taskbar tb in mw.taskbarDetails)
                            {
                                if (tb.BridgeHwnd == IntPtr.Zero)
                                {
                                    continue;
                                }

                                try
                                {
                                    int uiaRight = (int)mw.Dispatcher.Invoke(
                                        new Func<int>(() => Taskbar.GetAppListRightFromUia(tb.BridgeHwnd)));
                                    if (uiaRight > 0 && uiaRight != tb.AppListRect.Right)
                                    {
                                        LocalPInvoke.RECT appListRect = tb.AppListRect;
                                        appListRect.Right = uiaRight;
                                        tb.AppListRect = appListRect;
                                        tb.Ignored = true;
                                    }
                                }
                                catch (Exception)
                                {
                                }

                                try
                                {
                                    if (Taskbar.GetTrayRectFromUia(tb.BridgeHwnd, out LocalPInvoke.RECT uiaTrayRect))
                                    {
                                        if (tb.TrayRect.Left != uiaTrayRect.Left || tb.TrayRect.Top != uiaTrayRect.Top ||
                                            tb.TrayRect.Right != uiaTrayRect.Right || tb.TrayRect.Bottom != uiaTrayRect.Bottom)
                                        {
                                            tb.TrayRect = uiaTrayRect;
                                            tb.Ignored = true;
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                }
                            }

                            infrequentCount = 0;
                        }

                        // Check if the taskbar is centred, and if it is, directly update the settings; using an interim bool to avoid delaying because I'm lazy
                        bool isCentred = Taskbar.CheckIfCentred();
                        mw.activeSettings.IsCentred = isCentred;

                        // Work with static values to avoid some null reference exceptions
                        List<Types.Taskbar> taskbars = mw.taskbarDetails;
                        List<Types.Taskbar> originalTaskbars = taskbars;
                        Types.Settings settings = mw.activeSettings;
                        bool showAppList = settings.ShowAppList ?? true;

                        // If the number of taskbars has changed, regenerate taskbar information
                        if (Taskbar.TaskbarCountOrHandleChanged(taskbars.Count, taskbars[0].TaskbarHwnd))
                        {
                            // Forcefully reset taskbars if the taskbar count or main taskbar handle has changed
                            taskbars = Taskbar.GenerateTaskbarInfo();
                            Debug.WriteLine("Regenerating taskbar info");
                        }

                        for (int current = 0; current < taskbars.Count; current++)
                        {
                            if (taskbars[current].TaskbarHwnd == IntPtr.Zero || taskbars[current].AppListHwnd == IntPtr.Zero)
                            {
                                taskbars = Taskbar.GenerateTaskbarInfo();
                                Debug.WriteLine("Regenerating taskbar info due to a missing handle");
                                break;
                            }
                            // Get the latest quick details of this taskbar
                            Types.Taskbar newTaskbar = Taskbar.GetQuickTaskbarRects(taskbars[current].TaskbarHwnd, taskbars[current].TrayHwnd, taskbars[current].AppListHwnd);

                            // Keep the UIA-measured app button right edge instead of the
                            // (possibly much shorter) legacy MSTaskSwWClass rect.
                            if (taskbars[current].BridgeHwnd != IntPtr.Zero && taskbars[current].AppListRect.Right > 0)
                            {
                                LocalPInvoke.RECT appListRect = newTaskbar.AppListRect;
                                appListRect.Right = taskbars[current].AppListRect.Right;
                                newTaskbar.AppListRect = appListRect;
                            }

                            // Keep the UIA-measured tray rect when the legacy
                            // TrayNotifyWnd still reports an empty rect.
                            if (taskbars[current].TrayRect.Right > taskbars[current].TrayRect.Left &&
                                newTaskbar.TrayRect.Right <= newTaskbar.TrayRect.Left)
                            {
                                newTaskbar.TrayRect = taskbars[current].TrayRect;
                            }


                            // If the taskbar's monitor has a maximised window, reset it so it's "filled"
                            if (Taskbar.TaskbarShouldBeFilled(taskbars[current].TaskbarHwnd, settings))
                            {
                                if (taskbars[current].Ignored == false)
                                {
                                    Taskbar.ResetTaskbar(taskbars[current], settings);
                                    taskbars[current].Ignored = true;
                                }
                                continue;
                            }

                            // Show/hide segments on hover. Each segment has its own
                            // hit area, so the app list and tray are independent.
                            if (settings.ShowSegmentsOnHover)
                            {
                                LocalPInvoke.GetCursorPos(out LocalPInvoke.POINT msPt);

                                // Auto-hide keeps the taskbar window partly off-screen.
                                // Use the visible position for hit-testing instead.
                                LocalPInvoke.RECT hoverTaskbarRect = GetHoverTaskbarRect(taskbars[current]);

                                // App list segment: Start plus the app buttons.
                                LocalPInvoke.RECT currentAppRect = hoverTaskbarRect;
                                int appLeft = hoverTaskbarRect.Left;
                                if (settings.IsCentred)
                                {
                                    int centredDistance = taskbars[current].TaskbarRect.Right
                                        - taskbars[current].AppListRect.Right
                                        - Convert.ToInt32(2 * taskbars[current].ScaleFactor);
                                    if (!settings.IsWindows11)
                                    {
                                        centredDistance -= Convert.ToInt32(20 * taskbars[current].ScaleFactor);
                                    }
                                    appLeft += centredDistance;
                                }
                                currentAppRect.Left = appLeft;
                                currentAppRect.Right = taskbars[current].AppListRect.Right;
                                bool appVisible = showAppList && LocalPInvoke.PtInRect(ref currentAppRect, msPt);
                                if (taskbars[current].AppListVisible != appVisible)
                                {
                                    taskbars[current].AppListVisible = appVisible;
                                    taskbars[current].Ignored = true;
                                }

                                LocalPInvoke.RECT currentTrayRect = taskbars[current].TrayRect;
                                int yOffset = hoverTaskbarRect.Top - taskbars[current].TaskbarRect.Top;
                                currentTrayRect.Top += yOffset;
                                currentTrayRect.Bottom += yOffset;
                                LocalPInvoke.RECT currentWidgetsRect = hoverTaskbarRect;
                                currentWidgetsRect.Right = Convert.ToInt32(currentWidgetsRect.Right - (currentWidgetsRect.Right - currentWidgetsRect.Left) + (168 * taskbars[current].ScaleFactor));

                                if (currentTrayRect.Left != 0)
                                {
                                    bool isHoveringOverTray = LocalPInvoke.PtInRect(ref currentTrayRect, msPt);
                                    bool isHoveringOverWidgets = LocalPInvoke.PtInRect(ref currentWidgetsRect, msPt);
                                    bool trayHoverEnabled = settings.TrayHoverEnabled ?? true;
                                    if (trayHoverEnabled)
                                    {
                                        if (isHoveringOverTray && !settings.ShowTray)
                                        {
                                            settings.ShowTray = true;
                                            taskbars[current].Ignored = true;
                                        }
                                        else if (!isHoveringOverTray)
                                        {
                                            taskbars[current].Ignored = true;
                                            settings.ShowTray = false;
                                        }
                                    }
                                    else if (settings.ShowTray)
                                    {
                                        settings.ShowTray = false;
                                        taskbars[current].Ignored = true;
                                    }

                                    // The widgets segment is a hard-coded 168px strip on
                                    // the left. When the app segment is hidden that strip
                                    // overlaps the app icons, so do not reveal it on hover.
                                    if (showAppList)
                                    {
                                        if (isHoveringOverWidgets && !settings.ShowWidgets)
                                        {
                                            settings.ShowWidgets = true;
                                            taskbars[current].Ignored = true;
                                        }
                                        else if (!isHoveringOverWidgets)
                                        {
                                            taskbars[current].Ignored = true;
                                            settings.ShowWidgets = false;
                                        }
                                    }

                                }
                            }

                            if (settings.AutoHide > 0)
                            {
                                LocalPInvoke.RECT currentTaskbarRect = taskbars[current].TaskbarRect;
                                LocalPInvoke.GetCursorPos(out LocalPInvoke.POINT msPt);
                                bool isHoveringOverTaskbar;
                                if (taskbars[current].TaskbarHidden)
                                {
                                    currentTaskbarRect.Top = currentTaskbarRect.Bottom - 2;
                                    isHoveringOverTaskbar = LocalPInvoke.PtInRect(ref currentTaskbarRect, msPt);

                                }
                                else
                                {
                                    isHoveringOverTaskbar = LocalPInvoke.PtInRect(ref currentTaskbarRect, msPt);
                                }
                                if (isHoveringOverTaskbar)
                                {
                                    Debug.WriteLine("___");
                                }
                                int animSpeed = 15;
                                byte taskbarOpacity = 0;
                                LocalPInvoke.GetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, out _, out taskbarOpacity, out _);
                                //Debug.WriteLine($"Taskbar opacity:  {taskbarOpacity}");
                                if (isHoveringOverTaskbar && taskbarOpacity == 1)
                                {
                                    int style = LocalPInvoke.GetWindowLong(taskbars[current].TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32();
                                    if ((style & LocalPInvoke.WS_EX_TRANSPARENT) == LocalPInvoke.WS_EX_TRANSPARENT)
                                    {
                                        LocalPInvoke.SetWindowLong(taskbars[current].TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE, LocalPInvoke.GetWindowLong(taskbars[current].TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32() ^ LocalPInvoke.WS_EX_TRANSPARENT);
                                    }
                                    LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 63, LocalPInvoke.LWA_ALPHA);
                                    System.Threading.Thread.Sleep(animSpeed);
                                    LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 127, LocalPInvoke.LWA_ALPHA);
                                    System.Threading.Thread.Sleep(animSpeed);
                                    LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 191, LocalPInvoke.LWA_ALPHA);
                                    System.Threading.Thread.Sleep(animSpeed);
                                    LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 255, LocalPInvoke.LWA_ALPHA);
                                    taskbars[current].Ignored = true;
                                    taskbars[current].TaskbarHidden = false;
                                    Debug.WriteLine("MouseOver TB");
                                }
                                else if (!isHoveringOverTaskbar && taskbarOpacity == 255)
                                {
                                    LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 191, LocalPInvoke.LWA_ALPHA);
                                    System.Threading.Thread.Sleep(animSpeed);
                                    LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 127, LocalPInvoke.LWA_ALPHA);
                                    System.Threading.Thread.Sleep(animSpeed);
                                    LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 63, LocalPInvoke.LWA_ALPHA);
                                    System.Threading.Thread.Sleep(animSpeed);
                                    LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 1, LocalPInvoke.LWA_ALPHA);
                                    int style = LocalPInvoke.GetWindowLong(taskbars[current].TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32();
                                    if ((style & LocalPInvoke.WS_EX_TRANSPARENT) != LocalPInvoke.WS_EX_TRANSPARENT)
                                    {
                                        LocalPInvoke.SetWindowLong(taskbars[current].TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE, LocalPInvoke.GetWindowLong(taskbars[current].TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32() ^ LocalPInvoke.WS_EX_TRANSPARENT);
                                    }
                                    taskbars[current].Ignored = true;
                                    taskbars[current].TaskbarHidden = true;
                                    Debug.WriteLine("MouseOff TB");
                                }
                            }
                            else
                            {
                                int animSpeed = 15;
                                byte taskbarOpacity = 0;
                                LocalPInvoke.GetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, out _, out taskbarOpacity, out _);
                                if (taskbarOpacity < 255)
                                {
                                    int style = LocalPInvoke.GetWindowLong(taskbars[current].TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32();
                                    if ((style & LocalPInvoke.WS_EX_TRANSPARENT) == LocalPInvoke.WS_EX_TRANSPARENT)
                                    {
                                        LocalPInvoke.SetWindowLong(taskbars[current].TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE, LocalPInvoke.GetWindowLong(taskbars[current].TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32() ^ LocalPInvoke.WS_EX_TRANSPARENT);
                                    }
                                    LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 63, LocalPInvoke.LWA_ALPHA);
                                    System.Threading.Thread.Sleep(animSpeed);
                                    LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 127, LocalPInvoke.LWA_ALPHA);
                                    System.Threading.Thread.Sleep(animSpeed);
                                    LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 191, LocalPInvoke.LWA_ALPHA);
                                    System.Threading.Thread.Sleep(animSpeed);
                                    LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 255, LocalPInvoke.LWA_ALPHA);
                                    taskbars[current].Ignored = true;
                                    taskbars[current].TaskbarHidden = false;
                                }
                            }


                            // If the taskbar's overall rect has changed, update it. If it's simple, just update. If it's dynamic, check it's a valid change, then update it.
                            if (Taskbar.TaskbarRefreshRequired(taskbars[current], newTaskbar, settings.IsDynamic) || taskbars[current].Ignored || redrawOverride)
                            {
                                Debug.WriteLine($"Refresh required on taskbar {current}");
                                taskbars[current].Ignored = false;
                                int isFullTest = newTaskbar.TrayRect.Left - newTaskbar.AppListRect.Right;
                                mw.interaction.AddLog($"Taskbar: {current} - AppList ends: {newTaskbar.AppListRect.Right} - Tray starts: {newTaskbar.TrayRect.Left} - Total gap: {isFullTest}");
                                if (!settings.IsDynamic || (showAppList && isFullTest <= taskbars[current].ScaleFactor * 25 && isFullTest > 0 && newTaskbar.TrayRect.Left != 0))
                                {
                                    // Add the rect changes to the temporary list of taskbars
                                    taskbars[current].TaskbarRect = newTaskbar.TaskbarRect;
                                    taskbars[current].AppListRect = newTaskbar.AppListRect;
                                    taskbars[current].TrayRect = newTaskbar.TrayRect;
                                    Taskbar.UpdateSimpleTaskbar(taskbars[current], settings);
                                    mw.interaction.AddLog($"Updated taskbar {current} simply");
                                }
                                else
                                {
                                    if (Taskbar.CheckDynamicUpdateIsValid(taskbars[current], newTaskbar, !showAppList))
                                    {
                                        // Add the rect changes to the temporary list of taskbars
                                        taskbars[current].TaskbarRect = newTaskbar.TaskbarRect;
                                        taskbars[current].AppListRect = newTaskbar.AppListRect;
                                        taskbars[current].TrayRect = newTaskbar.TrayRect;
                                        Taskbar.UpdateDynamicTaskbar(taskbars[current], settings);
                                        mw.interaction.AddLog($"Updated taskbar {current} dynamically");
                                    }
                                }
                            }
                        }
                        // Do not overwrite a newer taskbar list produced by the
                        // startup coordinator while this loop was running.
                        if (ReferenceEquals(mw.taskbarDetails, originalTaskbars))
                        {
                            mw.taskbarDetails = taskbars;
                        }

                        // Explorer can reset the taskbar region during logon and
                        // shell startup. Re-apply periodically so a transient reset
                        // does not leave the taskbar unmodified for minutes.
                        periodicRefresh++;
                        if (periodicRefresh >= 50)
                        {
                            periodicRefresh = 0;
                            foreach (Types.Taskbar taskbar in taskbars)
                            {
                                taskbar.Ignored = true;
                            }
                        }

                    System.Threading.Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    // A shell startup race must not kill the update loop.
                    mw.interaction.AddLog(ex.Message);
                    mw.interaction.AddLog(ex.InnerException?.Message);
                    System.Threading.Thread.Sleep(1000);
                }
            }
        }
    }
}
